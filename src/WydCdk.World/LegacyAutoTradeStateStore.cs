using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;
using WydCdk.Protocol;

namespace WydCdk.World;

public enum LegacyAutoTradeStateResult
{
    Saved,
    Removed,
    NotFound,
    InvalidOwner,
    InvalidListing,
}

/// <summary>
/// Durable identity and listing data for one autotrade offer. The connection
/// id is deliberately absent: it is a runtime transport identity, not a
/// persistence key. Rebinding it to a live MOB belongs to the offline-mob
/// stage that follows this storage block.
/// </summary>
public sealed record LegacyAutoTradePersistedListing(
    string AccountName,
    int CharacterSlot,
    short PositionX,
    short PositionY,
    string Title,
    IReadOnlyList<LegacyItem> Items,
    IReadOnlyList<sbyte> CarryPositions,
    IReadOnlyList<int> Prices,
    short Tax)
{
    public LegacyAutoTradeSnapshot BindToConnection(int connectionId) => new(
        connectionId,
        PositionX,
        PositionY,
        Title,
        Items.ToArray(),
        CarryPositions.ToArray(),
        Prices.ToArray(),
        Tax);
}

public interface ILegacyAutoTradeStateStore
{
    ValueTask<LegacyAutoTradeStateResult> SaveAsync(
        string accountName,
        int characterSlot,
        LegacyAutoTradeSnapshot snapshot,
        CancellationToken cancellationToken = default);

    ValueTask<LegacyAutoTradePersistedListing?> ReadAsync(
        string accountName,
        int characterSlot,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<LegacyAutoTradePersistedListing>> ReadAllAsync(
        CancellationToken cancellationToken = default);

    ValueTask<LegacyAutoTradeStateResult> RemoveAsync(
        string accountName,
        int characterSlot,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Versioned local persistence for the autotrade listing. It is an explicit
/// sandbox/runtime adapter, not a replacement for the MariaDB table adapter.
/// Writes use a temporary file followed by replacement so a process stop does
/// not intentionally truncate the active document.
/// </summary>
public sealed class LegacyAutoTradeFileStore : ILegacyAutoTradeStateStore, ILegacyAutoTradePurchaseCommitStore
{
    private const int CurrentVersion = 1;
    private const int JournalVersion = 1;
    private const int CharacterCount = 4;
    private const string PreparedPhase = "Prepared";
    private const string CommittedPhase = "Committed";
    private readonly string path;
    private readonly string journalPath;
    private readonly string? accountRoot;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public LegacyAutoTradeFileStore(string path, string worldKey, string? accountRoot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!LegacyServerModePolicy.TryParseWorldKey(worldKey, out var mode))
            throw new ArgumentException("World key must be UP or PVP.", nameof(worldKey));

        this.path = Path.GetFullPath(path);
        journalPath = this.path + ".journal";
        this.accountRoot = string.IsNullOrWhiteSpace(accountRoot) ? null : Path.GetFullPath(accountRoot);
        WorldKey = new LegacyServerModePolicy(mode).WorldKey;
    }

    public string WorldKey { get; }

    public async ValueTask<LegacyAutoTradeStateResult> SaveAsync(
        string accountName,
        int characterSlot,
        LegacyAutoTradeSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!TryNormalizeOwner(accountName, characterSlot, out var normalizedAccount))
            return LegacyAutoTradeStateResult.InvalidOwner;
        if (!TryCreatePersistedListing(normalizedAccount, characterSlot, snapshot, out var listing))
            return LegacyAutoTradeStateResult.InvalidListing;

        await gate.WaitAsync(cancellationToken);
        try
        {
            await RecoverPendingAsync(cancellationToken);
            var document = await ReadDocumentAsync(cancellationToken);
            var listings = document.Listings
                .Where(value => !IsSameOwner(value, normalizedAccount, characterSlot))
                .Append(listing)
                .OrderBy(static value => value.AccountName, StringComparer.Ordinal)
                .ThenBy(static value => value.CharacterSlot)
                .ToArray();
            await WriteDocumentAsync(new StateDocument(CurrentVersion, WorldKey, listings), cancellationToken);
            return LegacyAutoTradeStateResult.Saved;
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<LegacyAutoTradePersistedListing?> ReadAsync(
        string accountName,
        int characterSlot,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeOwner(accountName, characterSlot, out var normalizedAccount))
            return null;

        await gate.WaitAsync(cancellationToken);
        try
        {
            await RecoverPendingAsync(cancellationToken);
            var document = await ReadDocumentAsync(cancellationToken);
            return document.Listings.FirstOrDefault(value => IsSameOwner(value, normalizedAccount, characterSlot));
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<IReadOnlyList<LegacyAutoTradePersistedListing>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await RecoverPendingAsync(cancellationToken);
            var document = await ReadDocumentAsync(cancellationToken);
            return document.Listings.ToArray();
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<LegacyAutoTradeStateResult> RemoveAsync(
        string accountName,
        int characterSlot,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeOwner(accountName, characterSlot, out var normalizedAccount))
            return LegacyAutoTradeStateResult.InvalidOwner;

        await gate.WaitAsync(cancellationToken);
        try
        {
            await RecoverPendingAsync(cancellationToken);
            var document = await ReadDocumentAsync(cancellationToken);
            if (!document.Listings.Any(value => IsSameOwner(value, normalizedAccount, characterSlot)))
                return LegacyAutoTradeStateResult.NotFound;

            var listings = document.Listings
                .Where(value => !IsSameOwner(value, normalizedAccount, characterSlot))
                .ToArray();
            await WriteDocumentAsync(new StateDocument(CurrentVersion, WorldKey, listings), cancellationToken);
            return LegacyAutoTradeStateResult.Removed;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// File-backed implementation of the offline-shop purchase commit. It is
    /// enabled only when the state store was constructed with the legacy
    /// account root. The journal contains the exact before/after bytes for
    /// both account blobs and the listing document, so startup can roll back a
    /// prepared transaction or finish a transaction already marked committed.
    /// </summary>
    public async ValueTask<LegacyAutoTradePurchaseCommitResult> TryCommitAsync(
        LegacyAutoTradePurchaseCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (LegacyAutoTradePurchaseCommitRules.Validate(request) != LegacyAutoTradePurchaseCommitResult.Committed)
            return LegacyAutoTradePurchaseCommitResult.InvalidRequest;
        if (accountRoot is null)
            return LegacyAutoTradePurchaseCommitResult.NotSupported;

        await gate.WaitAsync(cancellationToken);
        try
        {
            await RecoverPendingAsync(cancellationToken);
            if (!TryResolveAccountPath(request.BuyerAccountName, out var buyerAccount, out var buyerPath) ||
                !TryResolveAccountPath(request.SellerAccountName, out var sellerAccount, out var sellerPath) ||
                string.Equals(buyerAccount, sellerAccount, StringComparison.Ordinal))
                return LegacyAutoTradePurchaseCommitResult.InvalidRequest;

            var buyerBlob = await ReadAccountFileAsync(buyerPath, cancellationToken);
            var sellerBlob = await ReadAccountFileAsync(sellerPath, cancellationToken);
            if (buyerBlob is null || sellerBlob is null)
                return LegacyAutoTradePurchaseCommitResult.AccountNotFound;
            if (buyerBlob.Length < LegacyAccountSnapshot.RequiredFileLength ||
                sellerBlob.Length < LegacyAccountSnapshot.RequiredFileLength)
                return LegacyAutoTradePurchaseCommitResult.CharacterNotAvailable;

            var buyerCharacterOffset = LegacyAccountSnapshot.CharactersOffset + (request.BuyerCharacterSlot * LegacyAccountSnapshot.CharacterStride);
            var sellerCharacterOffset = LegacyAccountSnapshot.CharactersOffset + (request.SellerCharacterSlot * LegacyAccountSnapshot.CharacterStride);
            if (buyerBlob[buyerCharacterOffset + LegacyAccountSnapshot.MobNameOffset] == 0 ||
                sellerBlob[sellerCharacterOffset + LegacyAccountSnapshot.MobNameOffset] == 0)
                return LegacyAutoTradePurchaseCommitResult.CharacterNotAvailable;

            var currentBuyerMob = buyerBlob.AsSpan(buyerCharacterOffset, LegacyAccountSnapshot.CharacterStride);
            if (!currentBuyerMob.SequenceEqual(request.ExpectedPersistedBuyerMob) ||
                LegacyItem.Read(currentBuyerMob.Slice(
                    LegacyAccountSnapshot.MobCarryOffset + (request.Plan.BuyerDestinationSlot * LegacyItem.SizeInBytes),
                    LegacyItem.SizeInBytes)) != default)
                return LegacyAutoTradePurchaseCommitResult.Conflict;

            var currentSellerCoin = BinaryPrimitives.ReadInt32LittleEndian(sellerBlob.AsSpan(LegacyAccountSnapshot.AccountCoinOffset));
            var currentSellerItem = LegacyItem.Read(sellerBlob.AsSpan(
                LegacyAccountSnapshot.CargoOffset + (request.Plan.SellerCargoPosition * LegacyItem.SizeInBytes),
                LegacyItem.SizeInBytes));
            if (currentSellerCoin != request.ExpectedSellerCoin || currentSellerItem != request.Plan.PurchasedItem)
                return LegacyAutoTradePurchaseCommitResult.Conflict;

            var currentDocument = await ReadDocumentAsync(cancellationToken);
            var currentListing = currentDocument.Listings.FirstOrDefault(value =>
                IsSameOwner(value, sellerAccount, request.SellerCharacterSlot));
            if (currentListing is null)
                return LegacyAutoTradePurchaseCommitResult.ListingNotFound;
            if (!LegacyAutoTradePurchaseCommitRules.SameListing(currentListing, request.ExpectedListing))
                return LegacyAutoTradePurchaseCommitResult.Conflict;

            var updatedBuyerBlob = buyerBlob.ToArray();
            request.BuyerMob.AsMemory().CopyTo(updatedBuyerBlob.AsMemory(
                buyerCharacterOffset,
                LegacyAccountSnapshot.CharacterStride));
            BinaryPrimitives.WriteInt16LittleEndian(
                updatedBuyerBlob.AsSpan(buyerCharacterOffset + LegacyAccountSnapshot.MobSavedPositionXOffset),
                request.BuyerPositionX);
            BinaryPrimitives.WriteInt16LittleEndian(
                updatedBuyerBlob.AsSpan(buyerCharacterOffset + LegacyAccountSnapshot.MobSavedPositionYOffset),
                request.BuyerPositionY);
            request.BuyerMobExtra.AsMemory().CopyTo(updatedBuyerBlob.AsMemory(
                LegacyAccountSnapshot.MobExtraOffset + (request.BuyerCharacterSlot * LegacyAccountSnapshot.MobExtraStride),
                LegacyAccountSnapshot.MobExtraStride));
            BinaryPrimitives.WriteInt32LittleEndian(
                updatedBuyerBlob.AsSpan(LegacyAccountSnapshot.AccountCoinOffset),
                request.Plan.BuyerCoin);

            var updatedSellerBlob = sellerBlob.ToArray();
            updatedSellerBlob.AsSpan(
                LegacyAccountSnapshot.CargoOffset + (request.Plan.SellerCargoPosition * LegacyItem.SizeInBytes),
                LegacyItem.SizeInBytes).Clear();
            BinaryPrimitives.WriteInt32LittleEndian(
                updatedSellerBlob.AsSpan(LegacyAccountSnapshot.AccountCoinOffset),
                request.Plan.SellerCoin);

            var updatedListings = currentDocument.Listings
                .Select(value => IsSameOwner(value, sellerAccount, request.SellerCharacterSlot)
                    ? request.UpdatedListing
                    : value)
                .ToArray();
            var updatedDocument = new StateDocument(CurrentVersion, WorldKey, updatedListings);
            var stateWasPresent = File.Exists(path);
            var stateBefore = stateWasPresent
                ? await File.ReadAllBytesAsync(path, cancellationToken)
                : null;
            var journal = new JournalDocument(
                JournalVersion,
                WorldKey,
                PreparedPhase,
                [
                    new JournalEntry(buyerPath, true, buyerBlob, updatedBuyerBlob),
                    new JournalEntry(sellerPath, true, sellerBlob, updatedSellerBlob),
                    new JournalEntry(path, stateWasPresent, stateBefore, SerializeDocument(updatedDocument)),
                ]);

            await WriteJournalAsync(journal, cancellationToken);
            try
            {
                foreach (var entry in journal.Files)
                    await WriteAtomicBytesAsync(entry.Path, entry.After, cancellationToken);

                await WriteJournalAsync(journal with { Phase = CommittedPhase }, cancellationToken);
                File.Delete(journalPath);
            }
            catch
            {
                await RecoverPendingAsync(CancellationToken.None);
                throw;
            }

            return LegacyAutoTradePurchaseCommitResult.Committed;
        }
        finally
        {
            gate.Release();
        }
    }

    private async ValueTask<StateDocument> ReadDocumentAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return new(CurrentVersion, WorldKey, []);

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        var document = await JsonSerializer.DeserializeAsync<StateDocument>(stream, jsonOptions, cancellationToken)
            ?? throw new InvalidDataException("The autotrade state document is empty.");
        if (document.Version != CurrentVersion || !string.Equals(document.WorldKey, WorldKey, StringComparison.Ordinal))
            throw new InvalidDataException("The autotrade state document version or world key is unsupported.");
        if (document.Listings is null)
            throw new InvalidDataException("The autotrade state document has no listings array.");
        return document;
    }

    private async ValueTask WriteDocumentAsync(StateDocument document, CancellationToken cancellationToken)
    {
        await WriteAtomicBytesAsync(path, SerializeDocument(document), cancellationToken);
    }

    private byte[] SerializeDocument(StateDocument document) => JsonSerializer.SerializeToUtf8Bytes(document, jsonOptions);

    private async ValueTask WriteJournalAsync(JournalDocument journal, CancellationToken cancellationToken) =>
        await WriteAtomicBytesAsync(journalPath, JsonSerializer.SerializeToUtf8Bytes(journal, jsonOptions), cancellationToken);

    private async ValueTask RecoverPendingAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(journalPath))
            return;

        var journalBytes = await File.ReadAllBytesAsync(journalPath, cancellationToken);
        var journal = JsonSerializer.Deserialize<JournalDocument>(journalBytes, jsonOptions)
            ?? throw new InvalidDataException("The autotrade purchase journal is empty.");
        if (journal.Version != JournalVersion || !string.Equals(journal.WorldKey, WorldKey, StringComparison.Ordinal) ||
            journal.Files is null || journal.Files.Count == 0 ||
            journal.Files.Select(static value => Path.GetFullPath(value.Path)).Distinct(StringComparer.OrdinalIgnoreCase).Count() != journal.Files.Count)
            throw new InvalidDataException("The autotrade purchase journal is unsupported.");

        foreach (var entry in journal.Files)
        {
            if (!IsAllowedJournalPath(entry.Path) || entry.After is null || (entry.WasPresent && entry.Before is null))
                throw new InvalidDataException("The autotrade purchase journal contains an invalid target.");
        }

        if (string.Equals(journal.Phase, PreparedPhase, StringComparison.Ordinal))
        {
            foreach (var entry in journal.Files.Reverse())
            {
                if (!entry.WasPresent)
                {
                    if (File.Exists(entry.Path))
                        File.Delete(entry.Path);
                }
                else
                    await WriteAtomicBytesAsync(entry.Path, entry.Before!, CancellationToken.None);
            }
        }
        else if (string.Equals(journal.Phase, CommittedPhase, StringComparison.Ordinal))
        {
            foreach (var entry in journal.Files)
                await WriteAtomicBytesAsync(entry.Path, entry.After, CancellationToken.None);
        }
        else
            throw new InvalidDataException("The autotrade purchase journal has an unknown phase.");

        File.Delete(journalPath);
    }

    private static async ValueTask WriteAtomicBytesAsync(string targetPath, byte[] bytes, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temporaryPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan))
            {
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private async ValueTask<byte[]?> ReadAccountFileAsync(string accountPath, CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllBytesAsync(accountPath, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    private bool IsAllowedJournalPath(string targetPath)
    {
        var fullPath = Path.GetFullPath(targetPath);
        if (string.Equals(fullPath, path, StringComparison.OrdinalIgnoreCase))
            return true;
        if (accountRoot is null)
            return false;

        var rootWithSeparator = Path.EndsInDirectorySeparator(accountRoot)
            ? accountRoot
            : accountRoot + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryResolveAccountPath(string accountName, out string normalized, out string accountPath)
    {
        normalized = string.Empty;
        accountPath = string.Empty;
        if (accountRoot is null || !TryNormalizeOwner(accountName, 0, out normalized))
            return false;

        var directory = normalized[0] is >= 'A' and <= 'Z' ? normalized[..1] : "etc";
        accountPath = Path.Combine(accountRoot, directory, normalized);
        var rootWithSeparator = Path.EndsInDirectorySeparator(accountRoot) ? accountRoot : accountRoot + Path.DirectorySeparatorChar;
        return accountPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryCreatePersistedListing(
        string accountName,
        int characterSlot,
        LegacyAutoTradeSnapshot snapshot,
        out LegacyAutoTradePersistedListing listing)
    {
        listing = default!;
        if (snapshot.ConnectionId <= 0 ||
            snapshot.Items.Count != AutoTradeListConfirmation.SlotCount ||
            snapshot.CarryPositions.Count != AutoTradeListConfirmation.SlotCount ||
            snapshot.Prices.Count != AutoTradeListConfirmation.SlotCount)
            return false;

        try
        {
            _ = new AutoTradeListConfirmation(
                snapshot.Title,
                snapshot.Items,
                snapshot.CarryPositions,
                snapshot.Prices,
                snapshot.Tax,
                targetId: 1);
        }
        catch (ArgumentException)
        {
            return false;
        }

        listing = new LegacyAutoTradePersistedListing(
            accountName,
            characterSlot,
            snapshot.PositionX,
            snapshot.PositionY,
            snapshot.Title,
            snapshot.Items.ToArray(),
            snapshot.CarryPositions.ToArray(),
            snapshot.Prices.ToArray(),
            snapshot.Tax);
        return true;
    }

    private static bool TryNormalizeOwner(string accountName, int characterSlot, out string normalizedAccount)
    {
        normalizedAccount = string.Empty;
        if (characterSlot is < 0 or >= CharacterCount || string.IsNullOrWhiteSpace(accountName))
            return false;

        normalizedAccount = accountName.Trim().ToUpperInvariant();
        return normalizedAccount.Length is > 0 and < 16 &&
            normalizedAccount.All(static value => value is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-');
    }

    private static bool IsSameOwner(LegacyAutoTradePersistedListing listing, string accountName, int characterSlot) =>
        listing.CharacterSlot == characterSlot && string.Equals(listing.AccountName, accountName, StringComparison.Ordinal);

    private sealed record StateDocument(int Version, string WorldKey, IReadOnlyList<LegacyAutoTradePersistedListing> Listings);
    private sealed record JournalDocument(int Version, string WorldKey, string Phase, IReadOnlyList<JournalEntry> Files);
    private sealed record JournalEntry(string Path, bool WasPresent, byte[]? Before, byte[] After);
}

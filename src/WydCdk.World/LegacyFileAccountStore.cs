using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using WydCdk.Protocol;

namespace WydCdk.World;

public enum AccountAuthenticationStatus
{
    Success,
    AccountNotFound,
    WrongPassword,
    InvalidAccountName,
    InvalidAccountFile,
}

public enum DeleteCharacterStoreResult
{
    Success,
    AccountNotFound,
    WrongPassword,
    NotAvailable,
}

public enum CharacterPositionSaveResult
{
    Success,
    AccountNotFound,
    SlotOutOfRange,
    NotAvailable,
}

public enum CharacterStateSaveResult
{
    Success,
    AccountNotFound,
    SlotOutOfRange,
    InvalidLength,
    NotAvailable,
}

public enum CharacterGuildSaveResult
{
    Success,
    AccountNotFound,
    SlotOutOfRange,
    NotAvailable,
}

public enum CharacterShortSkillSaveResult
{
    Success,
    AccountNotFound,
    SlotOutOfRange,
    InvalidLength,
    NotAvailable,
}

public enum DonateBalanceSaveResult
{
    Success,
    AccountNotFound,
    Conflict,
    InvalidValue,
    NotAvailable,
}

public enum ClientEquipmentStateSaveResult
{
    Success,
    AccountNotFound,
    SlotOutOfRange,
    CharacterNotAvailable,
    InvalidLength,
    Conflict,
    NotAvailable,
}

/// <summary>Authentication result without exposing the stored password.</summary>
public sealed record AccountAuthenticationResult(AccountAuthenticationStatus Status, string? AccountName)
{
    public bool IsSuccess => Status == AccountAuthenticationStatus.Success;
}

public interface IAccountStore
{
    ValueTask<AccountAuthenticationResult> AuthenticateAsync(AccountLoginRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Optional capability for account stores that can also read the character/cargo/coin snapshot.</summary>
public interface IAccountSnapshotStore
{
    ValueTask<LegacyAccountSnapshot?> ReadSnapshotAsync(string accountName, CancellationToken cancellationToken = default);
}

/// <summary>Minimal character-login read capability used by offline world rehydration.</summary>
public interface ILegacyCharacterLoginDataStore
{
    ValueTask<LegacyCharacterLoginData?> ReadCharacterLoginDataAsync(string accountName, int slot, CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional versioned state for the 7.69 client equipment extension. It is deliberately
/// separate from <see cref="ICharacterStore"/> so the confirmed 7.945-byte legacy blob
/// remains unchanged while a backend gains support for the two additional client slots.
/// </summary>
public interface IClientEquipmentStateStore
{
    ValueTask<IReadOnlyList<LegacyItem>?> ReadClientEquipmentAsync(string accountName, int slot, CancellationToken cancellationToken = default);
    ValueTask<ClientEquipmentStateSaveResult> TrySaveClientEquipmentAsync(string accountName, int slot, IReadOnlyList<LegacyItem> equipment, CancellationToken cancellationToken = default);
}

/// <summary>
/// Character/account-state contract shared by the legacy file adapter and the MariaDB world adapter.
/// The payloads intentionally remain the confirmed legacy byte layouts so the wire layer does not acquire
/// a second, partially equivalent representation while the storage backend is migrated.
/// </summary>
public interface ICharacterStore : IAccountSnapshotStore, ILegacyCharacterLoginDataStore
{
    ValueTask<byte[]?> ReadNumericTokenAsync(string accountName, CancellationToken cancellationToken = default);
    ValueTask<bool> TryWriteNumericTokenAsync(string accountName, ReadOnlyMemory<byte> token, CancellationToken cancellationToken = default);
    ValueTask<CharacterPositionSaveResult> TrySaveCharacterPositionAsync(string accountName, int slot, short positionX, short positionY, CancellationToken cancellationToken = default);
    ValueTask<CharacterStateSaveResult> TrySaveCharacterStateAsync(string accountName, int slot, ReadOnlyMemory<byte> mob, short positionX, short positionY, CancellationToken cancellationToken = default, ReadOnlyMemory<byte> mobExtra = default);
    ValueTask<CharacterGuildSaveResult> TrySaveCharacterGuildAsync(string accountName, int slot, int guildId, int coin, CancellationToken cancellationToken = default);
    ValueTask<CharacterShortSkillSaveResult> TrySaveCharacterShortSkillsAsync(string accountName, int slot, ReadOnlyMemory<byte> skillBar, ReadOnlyMemory<byte> shortSkills, CancellationToken cancellationToken = default);
    ValueTask<bool?> IsCharacterSlotEmptyAsync(string accountName, int slot, CancellationToken cancellationToken = default);
    ValueTask<bool> TryReserveCharacterNameAsync(string characterName, string accountName, CancellationToken cancellationToken = default);
    ValueTask<bool> TryWriteNewCharacterAsync(string accountName, int slot, string characterName, ReadOnlyMemory<byte> template, CancellationToken cancellationToken = default);
    ValueTask<DeleteCharacterStoreResult> TryDeleteCharacterAsync(string accountName, int slot, string suppliedPassword, CancellationToken cancellationToken = default);
}

/// <summary>
/// Raw per-slot bytes needed for <c>_MSG_CNFCharacterLogin</c>: the full STRUCT_MOB, ShortSkill, affect, and
/// STRUCT_MOBEXTRA for one character, plus the account-wide Donate value. Kept as opaque byte blobs, matching
/// how CFileDB.cpp:1064-1090 itself just struct-copies these fields rather than reinterpreting them.
/// </summary>
public sealed record LegacyCharacterLoginData(
    byte[] Mob,
    byte[] ShortSkill,
    byte[] Affect,
    byte[] MobExtra,
    int Donate,
    short SavedPositionX,
    short SavedPositionY,
    IReadOnlyList<LegacyItem>? ClientEquipment = null);

/// <summary>
/// Reader/writer for the legacy DBSrv account directory. Authentication reads only
/// STRUCT_ACCOUNTINFO.AccountName and AccountPass (28 bytes). ReadSnapshotAsync additionally reads
/// STRUCT_ACCOUNTFILE.Char/Cargo/Coin (up to offset 4508) once the caller already authenticated.
/// It never creates, extends, or deletes an account file itself (that remains external to this port);
/// the character-creation methods below only fill in already-existing, empty character slots inside one.
/// </summary>
public sealed class LegacyFileAccountStore : IAccountStore, ICharacterStore, IDonateBalanceStore, IAtomicCharacterStateStore
{
    private const int AccountNameLength = 16;
    private const int AccountPasswordLength = 12;
    private const int LoginHeaderLength = AccountNameLength + AccountPasswordLength;
    private const int CharacterNameFieldLength = 16; // NAME_LENGTH
    private const int AtomicJournalVersion = 1;
    private const string AtomicPreparedPhase = "Prepared";
    private const string AtomicCommittedPhase = "Committed";
    private readonly string accountRoot;
    private readonly string atomicJournalPath;
    private readonly SemaphoreSlim atomicStateGate = new(1, 1);

    public LegacyFileAccountStore(string accountRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountRoot);
        this.accountRoot = Path.GetFullPath(accountRoot);
        atomicJournalPath = Path.Combine(this.accountRoot, ".wyd-cdk-atomic-state.journal");
    }

    public async ValueTask<AccountAuthenticationResult> AuthenticateAsync(AccountLoginRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryResolveAccountPath(request.AccountName, out var accountName, out var path))
            return new(AccountAuthenticationStatus.InvalidAccountName, null);

        byte[] header = new byte[LoginHeaderLength];
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, LoginHeaderLength, FileOptions.SequentialScan);
            var offset = 0;
            while (offset < header.Length)
            {
                var read = await stream.ReadAsync(header.AsMemory(offset), cancellationToken);
                if (read == 0) return new(AccountAuthenticationStatus.InvalidAccountFile, null);
                offset += read;
            }
        }
        catch (FileNotFoundException)
        {
            return new(AccountAuthenticationStatus.AccountNotFound, null);
        }
        catch (DirectoryNotFoundException)
        {
            return new(AccountAuthenticationStatus.AccountNotFound, null);
        }

        var storedName = ReadCString(header.AsSpan(0, AccountNameLength));
        var storedPassword = ReadCString(header.AsSpan(AccountNameLength, AccountPasswordLength));
        if (!string.Equals(storedName, accountName, StringComparison.Ordinal))
            return new(AccountAuthenticationStatus.InvalidAccountFile, null);

        return string.Equals(storedPassword, request.AccountPassword, StringComparison.Ordinal)
            ? new(AccountAuthenticationStatus.Success, storedName)
            : new(AccountAuthenticationStatus.WrongPassword, null);
    }

    public async ValueTask<LegacyAccountSnapshot?> ReadSnapshotAsync(string accountName, CancellationToken cancellationToken = default)
    {
        if (!TryResolveAccountPath(accountName, out var normalizedName, out var path))
            return null;

        byte[] buffer = new byte[LegacyAccountSnapshot.RequiredFileLength];
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, buffer.Length, FileOptions.SequentialScan);
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
                if (read == 0) return null;
                offset += read;
            }
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        return LegacyAccountSnapshot.Read(normalizedName, buffer);
    }

    /// <summary>
    /// Reads the six raw <c>STRUCT_ACCOUNTINFO.NumericToken</c> bytes (the account's PIN). A leading
    /// <c>0xFF</c> byte is the legacy "never set" sentinel (<c>char NumericToken[0] == -1</c>), left for the
    /// caller to interpret, not this method - it just returns whatever six bytes are stored.
    /// </summary>
    public async ValueTask<byte[]?> ReadNumericTokenAsync(string accountName, CancellationToken cancellationToken = default)
    {
        if (!TryResolveAccountPath(accountName, out _, out var path)) return null;

        var buffer = new byte[LegacyAccountSnapshot.NumericTokenLength];
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, buffer.Length, FileOptions.RandomAccess);
            if (stream.Length < LegacyAccountSnapshot.NumericTokenOffset + buffer.Length) return null;
            stream.Position = LegacyAccountSnapshot.NumericTokenOffset;
            await stream.ReadExactlyAsync(buffer, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (EndOfStreamException)
        {
            return null;
        }

        return buffer;
    }

    /// <summary>
    /// Equivalent of the two <c>strncpy(...NumericToken, m-&gt;NumericToken, 6); DBWriteAccount(...)</c> writes in
    /// <c>CFileDB::_MSG_AccountSecure</c> (CFileDB.cpp:1400, 1427): first-time PIN setup and PIN change both just
    /// overwrite these six bytes.
    /// </summary>
    public async ValueTask<bool> TryWriteNumericTokenAsync(string accountName, ReadOnlyMemory<byte> token, CancellationToken cancellationToken = default)
    {
        if (token.Length != LegacyAccountSnapshot.NumericTokenLength)
            throw new ArgumentException("A legacy numeric token is exactly six bytes.", nameof(token));
        if (!TryResolveAccountPath(accountName, out _, out var path)) return false;

        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);
            if (stream.Length < LegacyAccountSnapshot.NumericTokenOffset + token.Length) return false;
            stream.Position = LegacyAccountSnapshot.NumericTokenOffset;
            await stream.WriteAsync(token, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Equivalent of the read side of <c>CFileDB::_MSG_DBCharacterLogin</c> (CFileDB.cpp:1038-1090). Returns
    /// null both when the account file is unavailable and when the target slot is empty (<c>MobName[0] == 0</c>,
    /// CFileDB.cpp:1083's "already charged"... "mobname empty" check) - the reference handler sends no response
    /// at all in either case, so callers should not distinguish them either.
    /// </summary>
    public async ValueTask<LegacyCharacterLoginData?> ReadCharacterLoginDataAsync(string accountName, int slot, CancellationToken cancellationToken = default)
    {
        if (!TryResolveAccountPath(accountName, out _, out var path)) return null;

        var buffer = new byte[LegacyAccountSnapshot.RequiredFileLength];
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, buffer.Length, FileOptions.SequentialScan);
            if (stream.Length < buffer.Length) return null;
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
                if (read == 0) return null;
                offset += read;
            }
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        var mob = buffer.AsSpan(LegacyAccountSnapshot.CharactersOffset + (slot * LegacyAccountSnapshot.CharacterStride), LegacyAccountSnapshot.CharacterStride).ToArray();
        if (mob[LegacyAccountSnapshot.MobNameOffset] == 0) return null;

        var shortSkill = buffer.AsSpan(LegacyAccountSnapshot.ShortSkillOffset + (slot * LegacyAccountSnapshot.ShortSkillStride), LegacyAccountSnapshot.ShortSkillStride).ToArray();
        var affect = buffer.AsSpan(LegacyAccountSnapshot.AffectOffset + (slot * LegacyAccountSnapshot.AffectStride), LegacyAccountSnapshot.AffectStride).ToArray();
        var mobExtra = buffer.AsSpan(LegacyAccountSnapshot.MobExtraOffset + (slot * LegacyAccountSnapshot.MobExtraStride), LegacyAccountSnapshot.MobExtraStride).ToArray();
        var donate = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(LegacyAccountSnapshot.DonateOffset));
        var savedPositionX = BinaryPrimitives.ReadInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobSavedPositionXOffset));
        var savedPositionY = BinaryPrimitives.ReadInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobSavedPositionYOffset));

        return new LegacyCharacterLoginData(mob, shortSkill, affect, mobExtra, donate, savedPositionX, savedPositionY);
    }

    public async ValueTask<int?> ReadDonateAsync(string accountName, CancellationToken cancellationToken = default)
    {
        if (!TryResolveAccountPath(accountName, out _, out var path)) return null;
        var bytes = new byte[sizeof(int)];
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bytes.Length, FileOptions.RandomAccess);
            if (stream.Length < LegacyAccountSnapshot.DonateOffset + bytes.Length) return null;
            stream.Position = LegacyAccountSnapshot.DonateOffset;
            await stream.ReadExactlyAsync(bytes, cancellationToken);
            return BinaryPrimitives.ReadInt32LittleEndian(bytes);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (EndOfStreamException)
        {
            return null;
        }
    }

    /// <summary>Persists the account-wide STRUCT_ACCOUNTFILE.Donate field used by DonateShop.</summary>
    public async ValueTask<DonateBalanceSaveResult> TrySaveDonateAsync(string accountName, int donate, int? expectedDonate = null, CancellationToken cancellationToken = default)
    {
        if (donate < 0)
            return DonateBalanceSaveResult.InvalidValue;
        if (!TryResolveAccountPath(accountName, out _, out var path))
            return DonateBalanceSaveResult.AccountNotFound;

        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.RandomAccess);
            if (stream.Length < LegacyAccountSnapshot.DonateOffset + sizeof(int))
                return DonateBalanceSaveResult.NotAvailable;

            var bytes = new byte[sizeof(int)];
            stream.Position = LegacyAccountSnapshot.DonateOffset;
            var read = await stream.ReadAsync(bytes, cancellationToken);
            if (read != bytes.Length)
                return DonateBalanceSaveResult.NotAvailable;
            var currentDonate = BinaryPrimitives.ReadInt32LittleEndian(bytes);
            if (expectedDonate is not null && currentDonate != expectedDonate.Value)
                return DonateBalanceSaveResult.Conflict;

            BinaryPrimitives.WriteInt32LittleEndian(bytes, donate);
            stream.Position = LegacyAccountSnapshot.DonateOffset;
            await stream.WriteAsync(bytes, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return DonateBalanceSaveResult.Success;
        }
        catch (FileNotFoundException)
        {
            return DonateBalanceSaveResult.AccountNotFound;
        }
        catch (DirectoryNotFoundException)
        {
            return DonateBalanceSaveResult.AccountNotFound;
        }
        catch (EndOfStreamException)
        {
            return DonateBalanceSaveResult.NotAvailable;
        }
        catch (IOException)
        {
            return DonateBalanceSaveResult.NotAvailable;
        }
    }

    /// <summary>
    /// Persists the current world position into STRUCT_MOB.SPX/SPY for the selected character. This is the
    /// position-bearing part of the legacy SaveUser/CharLogOut path; the remaining mutable character state is
    /// not yet represented by the C# world session.
    /// </summary>
    public async ValueTask<CharacterPositionSaveResult> TrySaveCharacterPositionAsync(string accountName, int slot, short positionX, short positionY, CancellationToken cancellationToken = default)
    {
        if (slot < 0 || slot >= LegacyAccountSnapshot.CharacterCount)
            return CharacterPositionSaveResult.SlotOutOfRange;
        if (!TryResolveAccountPath(accountName, out _, out var path))
            return CharacterPositionSaveResult.AccountNotFound;

        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);
            if (stream.Length < LegacyAccountSnapshot.RequiredFileLength)
                return CharacterPositionSaveResult.NotAvailable;

            var name = new byte[CharacterNameFieldLength];
            var characterOffset = LegacyAccountSnapshot.CharactersOffset + (slot * LegacyAccountSnapshot.CharacterStride);
            stream.Position = characterOffset + LegacyAccountSnapshot.MobNameOffset;
            await stream.ReadExactlyAsync(name, cancellationToken);
            if (name[0] == 0)
                return CharacterPositionSaveResult.NotAvailable;

            var position = new byte[sizeof(short) * 2];
            BinaryPrimitives.WriteInt16LittleEndian(position, positionX);
            BinaryPrimitives.WriteInt16LittleEndian(position.AsSpan(sizeof(short)), positionY);
            stream.Position = characterOffset + LegacyAccountSnapshot.MobSavedPositionXOffset;
            await stream.WriteAsync(position, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return CharacterPositionSaveResult.Success;
        }
        catch (FileNotFoundException)
        {
            return CharacterPositionSaveResult.AccountNotFound;
        }
        catch (DirectoryNotFoundException)
        {
            return CharacterPositionSaveResult.AccountNotFound;
        }
        catch (EndOfStreamException)
        {
            return CharacterPositionSaveResult.NotAvailable;
        }
    }

    /// <summary>
    /// Persists the authoritative STRUCT_MOB snapshot, including combat resources and Carry, plus the current
    /// world position. This is the state-bearing counterpart to the older position-only logout save.
    /// </summary>
    public async ValueTask<CharacterStateSaveResult> TrySaveCharacterStateAsync(string accountName, int slot, ReadOnlyMemory<byte> mob, short positionX, short positionY, CancellationToken cancellationToken = default, ReadOnlyMemory<byte> mobExtra = default)
    {
        if (slot < 0 || slot >= LegacyAccountSnapshot.CharacterCount)
            return CharacterStateSaveResult.SlotOutOfRange;
        if (mob.Length != LegacyAccountSnapshot.CharacterStride)
            return CharacterStateSaveResult.InvalidLength;
        if (!mobExtra.IsEmpty && mobExtra.Length != LegacyAccountSnapshot.MobExtraStride)
            return CharacterStateSaveResult.InvalidLength;
        if (!TryResolveAccountPath(accountName, out _, out var path))
            return CharacterStateSaveResult.AccountNotFound;

        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, LegacyAccountSnapshot.CharacterStride, FileOptions.RandomAccess);
            if (stream.Length < LegacyAccountSnapshot.RequiredFileLength)
                return CharacterStateSaveResult.NotAvailable;

            var name = new byte[CharacterNameFieldLength];
            var characterOffset = LegacyAccountSnapshot.CharactersOffset + (slot * LegacyAccountSnapshot.CharacterStride);
            stream.Position = characterOffset + LegacyAccountSnapshot.MobNameOffset;
            await stream.ReadExactlyAsync(name, cancellationToken);
            if (name[0] == 0)
                return CharacterStateSaveResult.NotAvailable;

            var snapshot = mob.ToArray();
            BinaryPrimitives.WriteInt16LittleEndian(snapshot.AsSpan(LegacyAccountSnapshot.MobSavedPositionXOffset), positionX);
            BinaryPrimitives.WriteInt16LittleEndian(snapshot.AsSpan(LegacyAccountSnapshot.MobSavedPositionYOffset), positionY);
            stream.Position = characterOffset;
            await stream.WriteAsync(snapshot, cancellationToken);
            var coin = BinaryPrimitives.ReadInt32LittleEndian(snapshot.AsSpan(LegacyAccountSnapshot.MobCoinOffset));
            var coinBytes = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(coinBytes, coin);
            stream.Position = LegacyAccountSnapshot.AccountCoinOffset;
            await stream.WriteAsync(coinBytes, cancellationToken);
            if (!mobExtra.IsEmpty)
            {
                stream.Position = LegacyAccountSnapshot.MobExtraOffset + (slot * LegacyAccountSnapshot.MobExtraStride);
                await stream.WriteAsync(mobExtra, cancellationToken);
            }
            await stream.FlushAsync(cancellationToken);
            return CharacterStateSaveResult.Success;
        }
        catch (FileNotFoundException)
        {
            return CharacterStateSaveResult.AccountNotFound;
        }
        catch (DirectoryNotFoundException)
        {
            return CharacterStateSaveResult.AccountNotFound;
        }
        catch (EndOfStreamException)
        {
            return CharacterStateSaveResult.NotAvailable;
        }
    }

    /// <summary>
    /// Persists both sides of a completed trade through a small write-ahead journal. The account files are
    /// replaced as complete blobs so an interrupted pair update can be rolled back or replayed before the next
    /// atomic operation. Same-account trades remain invalid because this adapter has no single-file two-slot
    /// contract yet.
    /// </summary>
    public async ValueTask<AtomicCharacterStateSaveResult> TrySaveCharacterStatesAtomicallyAsync(
        LegacyCharacterStateSaveRequest first,
        LegacyCharacterStateSaveRequest second,
        CancellationToken cancellationToken = default)
    {
        if (!TryPrepareAtomicRequest(first, out var firstRequest, out var firstPath) ||
            !TryPrepareAtomicRequest(second, out var secondRequest, out var secondPath) ||
            string.Equals(firstRequest.AccountName, secondRequest.AccountName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(firstPath, secondPath, StringComparison.OrdinalIgnoreCase))
            return AtomicCharacterStateSaveResult.InvalidRequest;

        await atomicStateGate.WaitAsync(cancellationToken);
        try
        {
            await RecoverAtomicJournalAsync(cancellationToken);

            byte[] firstBefore;
            byte[] secondBefore;
            try
            {
                firstBefore = await File.ReadAllBytesAsync(firstPath, cancellationToken);
                secondBefore = await File.ReadAllBytesAsync(secondPath, cancellationToken);
            }
            catch (FileNotFoundException)
            {
                return AtomicCharacterStateSaveResult.AccountNotFound;
            }
            catch (DirectoryNotFoundException)
            {
                return AtomicCharacterStateSaveResult.AccountNotFound;
            }

            if (firstBefore.Length < LegacyAccountSnapshot.RequiredFileLength ||
                secondBefore.Length < LegacyAccountSnapshot.RequiredFileLength)
                return AtomicCharacterStateSaveResult.NotSupported;

            var firstAfter = firstBefore.ToArray();
            var secondAfter = secondBefore.ToArray();
            var firstResult = ApplyAtomicCharacterState(firstAfter, firstRequest);
            if (firstResult != AtomicCharacterStateSaveResult.Success)
                return firstResult;
            var secondResult = ApplyAtomicCharacterState(secondAfter, secondRequest);
            if (secondResult != AtomicCharacterStateSaveResult.Success)
                return secondResult;

            var entries = new[]
            {
                new AtomicJournalEntry(firstPath, firstBefore, firstAfter),
                new AtomicJournalEntry(secondPath, secondBefore, secondAfter),
            };

            try
            {
                await WriteAtomicJournalAsync(new AtomicJournalDocument(AtomicJournalVersion, AtomicPreparedPhase, entries), cancellationToken);
                await WriteAtomicBlobAsync(firstPath, firstAfter, cancellationToken);
                await WriteAtomicBlobAsync(secondPath, secondAfter, cancellationToken);
                await WriteAtomicJournalAsync(new AtomicJournalDocument(AtomicJournalVersion, AtomicCommittedPhase, entries), cancellationToken);
                DeleteAtomicJournal();
                return AtomicCharacterStateSaveResult.Success;
            }
            catch (OperationCanceledException)
            {
                await RecoverAtomicJournalAsync(CancellationToken.None);
                throw;
            }
            catch (IOException)
            {
                await RecoverAtomicJournalAsync(CancellationToken.None);
                return AtomicCharacterStateSaveResult.NotSupported;
            }
            catch (UnauthorizedAccessException)
            {
                await RecoverAtomicJournalAsync(CancellationToken.None);
                return AtomicCharacterStateSaveResult.NotSupported;
            }
        }
        finally
        {
            atomicStateGate.Release();
        }
    }

    /// <summary>Persists the guild id and coin fields changed by the legacy guild-invite flow.</summary>
    public async ValueTask<CharacterGuildSaveResult> TrySaveCharacterGuildAsync(string accountName, int slot, int guildId, int coin, CancellationToken cancellationToken = default)
    {
        if (slot < 0 || slot >= LegacyAccountSnapshot.CharacterCount)
            return CharacterGuildSaveResult.SlotOutOfRange;
        if (!TryResolveAccountPath(accountName, out _, out var path))
            return CharacterGuildSaveResult.AccountNotFound;

        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);
            if (stream.Length < LegacyAccountSnapshot.RequiredFileLength)
                return CharacterGuildSaveResult.NotAvailable;

            var name = new byte[CharacterNameFieldLength];
            var characterOffset = LegacyAccountSnapshot.CharactersOffset + (slot * LegacyAccountSnapshot.CharacterStride);
            stream.Position = characterOffset + LegacyAccountSnapshot.MobNameOffset;
            await stream.ReadExactlyAsync(name, cancellationToken);
            if (name[0] == 0)
                return CharacterGuildSaveResult.NotAvailable;

            var guild = new byte[sizeof(short)];
            BinaryPrimitives.WriteUInt16LittleEndian(guild, checked((ushort)guildId));
            stream.Position = characterOffset + LegacyAccountSnapshot.MobGuildOffset;
            await stream.WriteAsync(guild, cancellationToken);

            var coinBytes = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(coinBytes, coin);
            stream.Position = characterOffset + LegacyAccountSnapshot.MobCoinOffset;
            await stream.WriteAsync(coinBytes, cancellationToken);
            stream.Position = LegacyAccountSnapshot.AccountCoinOffset;
            await stream.WriteAsync(coinBytes, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return CharacterGuildSaveResult.Success;
        }
        catch (FileNotFoundException)
        {
            return CharacterGuildSaveResult.AccountNotFound;
        }
        catch (DirectoryNotFoundException)
        {
            return CharacterGuildSaveResult.AccountNotFound;
        }
        catch (EndOfStreamException)
        {
            return CharacterGuildSaveResult.NotAvailable;
        }
    }

    /// <summary>Persists the two shortcut arrays copied by legacy <c>Exec_MSG_SetShortSkill</c>.</summary>
    public async ValueTask<CharacterShortSkillSaveResult> TrySaveCharacterShortSkillsAsync(string accountName, int slot, ReadOnlyMemory<byte> skillBar, ReadOnlyMemory<byte> shortSkills, CancellationToken cancellationToken = default)
    {
        if (slot < 0 || slot >= LegacyAccountSnapshot.CharacterCount)
            return CharacterShortSkillSaveResult.SlotOutOfRange;
        if (skillBar.Length != 4 || shortSkills.Length != LegacyAccountSnapshot.ShortSkillStride)
            return CharacterShortSkillSaveResult.InvalidLength;
        if (!TryResolveAccountPath(accountName, out _, out var path))
            return CharacterShortSkillSaveResult.AccountNotFound;

        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);
            if (stream.Length < LegacyAccountSnapshot.RequiredFileLength)
                return CharacterShortSkillSaveResult.NotAvailable;

            var name = new byte[CharacterNameFieldLength];
            var characterOffset = LegacyAccountSnapshot.CharactersOffset + (slot * LegacyAccountSnapshot.CharacterStride);
            stream.Position = characterOffset + LegacyAccountSnapshot.MobNameOffset;
            await stream.ReadExactlyAsync(name, cancellationToken);
            if (name[0] == 0)
                return CharacterShortSkillSaveResult.NotAvailable;

            stream.Position = characterOffset + LegacyAccountSnapshot.MobSkillBarOffset;
            await stream.WriteAsync(skillBar, cancellationToken);
            stream.Position = LegacyAccountSnapshot.ShortSkillOffset + (slot * LegacyAccountSnapshot.ShortSkillStride);
            await stream.WriteAsync(shortSkills, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return CharacterShortSkillSaveResult.Success;
        }
        catch (FileNotFoundException)
        {
            return CharacterShortSkillSaveResult.AccountNotFound;
        }
        catch (DirectoryNotFoundException)
        {
            return CharacterShortSkillSaveResult.AccountNotFound;
        }
        catch (EndOfStreamException)
        {
            return CharacterShortSkillSaveResult.NotAvailable;
        }
    }

    /// <summary>
    /// True/false mirror the target slot's <c>MobName[0] != 0</c> check that CFileDB.cpp:937 runs before
    /// creating a character ("already charged"); null means the account file could not be read.
    /// </summary>
    public async ValueTask<bool?> IsCharacterSlotEmptyAsync(string accountName, int slot, CancellationToken cancellationToken = default)
    {
        if (!TryResolveAccountPath(accountName, out _, out var path)) return null;

        var nameField = new byte[CharacterNameFieldLength];
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, nameField.Length, FileOptions.RandomAccess);
            if (stream.Length < LegacyAccountSnapshot.RequiredFileLength) return null;

            stream.Position = LegacyAccountSnapshot.CharactersOffset + (slot * LegacyAccountSnapshot.CharacterStride) + LegacyAccountSnapshot.MobNameOffset;
            var offset = 0;
            while (offset < nameField.Length)
            {
                var read = await stream.ReadAsync(nameField.AsMemory(offset), cancellationToken);
                if (read == 0) return null;
                offset += read;
            }
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        return nameField[0] == 0;
    }

    /// <summary>
    /// Equivalent of <c>CFileDB::CreateCharacter</c> (CFileDB.cpp:2261): reserves the character name globally
    /// by creating <c>&lt;DBSrv run root&gt;/char/&lt;FirstKey&gt;/&lt;NAME&gt;</c> (a sibling of the account
    /// directory, not a subdirectory of it) exclusively, storing the owning account name inside. Returns false
    /// when the name is already reserved (file exists) or the directory cannot be prepared, matching the
    /// original's EEXIST-is-a-failure behavior; it never removes a reservation once created, matching legacy
    /// behavior (character deletion is out of scope for this port).
    /// </summary>
    public async ValueTask<bool> TryReserveCharacterNameAsync(string characterName, string accountName, CancellationToken cancellationToken = default)
    {
        var upperName = characterName.ToUpperInvariant();
        if (upperName.Length is < 1 or >= CharacterNameFieldLength) return false;

        string directory;
        string path;
        try
        {
            var charRoot = Path.Combine(Path.GetDirectoryName(accountRoot) ?? accountRoot, "char");
            directory = Path.Combine(charRoot, GetLegacyDirectory(upperName));
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, upperName);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        try
        {
            await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var bytes = new byte[AccountNameLength];
            Encoding.ASCII.GetBytes(accountName.AsSpan(0, Math.Min(accountName.Length, AccountNameLength)), bytes);
            await stream.WriteAsync(bytes, cancellationToken);
            return true;
        }
        catch (IOException)
        {
            return false; // includes the expected case: the name is already reserved.
        }
    }

    /// <summary>
    /// Equivalent of the character-writing tail of <c>CFileDB::_MSG_DBCreateCharacter</c> (CFileDB.cpp:970-1009):
    /// fills the target slot's STRUCT_MOB with <paramref name="template"/> (a class template read via
    /// <see cref="LegacyCharacterTemplateStore"/>) with only the name field overwritten, clears that slot's
    /// ShortSkill/affect, and clears+re-derives that slot's STRUCT_MOBEXTRA (ClassMaster=MORTAL, MortalFace
    /// taken from the template's first equipment slot, matching <c>extra-&gt;MortalFace = mob-&gt;Equip[0].sIndex</c>
    /// which runs right after the template memcpy). Returns false if the slot is no longer empty (a race with
    /// another create on the same slot) or the account file is missing/short.
    /// </summary>
    public async ValueTask<bool> TryWriteNewCharacterAsync(string accountName, int slot, string characterName, ReadOnlyMemory<byte> template, CancellationToken cancellationToken = default)
    {
        if (template.Length != LegacyAccountSnapshot.CharacterStride)
            throw new ArgumentException("Character template must be a full STRUCT_MOB.", nameof(template));
        if (!TryResolveAccountPath(accountName, out _, out var path)) return false;

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);
        if (stream.Length < LegacyAccountSnapshot.RequiredFileLength) return false;

        var charOffset = LegacyAccountSnapshot.CharactersOffset + (slot * LegacyAccountSnapshot.CharacterStride);

        var existingName = new byte[CharacterNameFieldLength];
        stream.Position = charOffset + LegacyAccountSnapshot.MobNameOffset;
        try
        {
            await stream.ReadExactlyAsync(existingName, cancellationToken);
        }
        catch (EndOfStreamException)
        {
            return false;
        }
        if (existingName[0] != 0) return false;

        var mob = template.ToArray();
        WriteCString(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset, CharacterNameFieldLength), characterName);

        stream.Position = charOffset;
        await stream.WriteAsync(mob, cancellationToken);

        var shortSkill = new byte[LegacyAccountSnapshot.ShortSkillStride];
        shortSkill.AsSpan().Fill(0xFF);
        stream.Position = LegacyAccountSnapshot.ShortSkillOffset + (slot * LegacyAccountSnapshot.ShortSkillStride);
        await stream.WriteAsync(shortSkill, cancellationToken);

        var affect = new byte[LegacyAccountSnapshot.AffectStride]; // zero-filled: clears any leftover buffs from a previous character in this slot
        stream.Position = LegacyAccountSnapshot.AffectOffset + (slot * LegacyAccountSnapshot.AffectStride);
        await stream.WriteAsync(affect, cancellationToken);

        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteInt16LittleEndian(mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset), LegacyAccountSnapshot.ClassMasterMortal);
        var faceIndex = BinaryPrimitives.ReadInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset));
        BinaryPrimitives.WriteInt16LittleEndian(mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraMortalFaceOffset), faceIndex);
        stream.Position = LegacyAccountSnapshot.MobExtraOffset + (slot * LegacyAccountSnapshot.MobExtraStride);
        await stream.WriteAsync(mobExtra, cancellationToken);

        await stream.FlushAsync(cancellationToken);
        return true;
    }

    /// <summary>Equivalent to the active delete path in <c>CFileDB::_MSG_DBDeleteCharacter</c> (CFileDB.cpp:1301-1378).</summary>
    public async ValueTask<DeleteCharacterStoreResult> TryDeleteCharacterAsync(string accountName, int slot, string suppliedPassword, CancellationToken cancellationToken = default)
    {
        if (!TryResolveAccountPath(accountName, out _, out var path)) return DeleteCharacterStoreResult.AccountNotFound;

        var file = new byte[LegacyAccountSnapshot.RequiredFileLength];
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, file.Length, FileOptions.RandomAccess);
            if (stream.Length < file.Length) return DeleteCharacterStoreResult.AccountNotFound;
            await stream.ReadExactlyAsync(file, cancellationToken);

            var storedPassword = ReadCString(file.AsSpan(AccountNameLength, AccountPasswordLength));
            if (!string.Equals(storedPassword, suppliedPassword, StringComparison.Ordinal)) return DeleteCharacterStoreResult.WrongPassword;

            var characterOffset = LegacyAccountSnapshot.CharactersOffset + (slot * LegacyAccountSnapshot.CharacterStride);
            var name = ReadCString(file.AsSpan(characterOffset + LegacyAccountSnapshot.MobNameOffset, CharacterNameFieldLength));
            if (name.Length == 0) return DeleteCharacterStoreResult.NotAvailable;

            LegacyCharacterStorageDefaults.ResetDeletedCharacterSlot(file, slot);

            stream.Position = 0;
            await stream.WriteAsync(file, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            DeleteCharacterReservation(name);
            return DeleteCharacterStoreResult.Success;
        }
        catch (FileNotFoundException)
        {
            return DeleteCharacterStoreResult.AccountNotFound;
        }
        catch (DirectoryNotFoundException)
        {
            return DeleteCharacterStoreResult.AccountNotFound;
        }
    }

    private void DeleteCharacterReservation(string characterName)
    {
        var upperName = characterName.ToUpperInvariant();
        var path = Path.Combine(Path.GetDirectoryName(accountRoot) ?? accountRoot, "char", GetLegacyDirectory(upperName), upperName);
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private bool TryPrepareAtomicRequest(
        LegacyCharacterStateSaveRequest request,
        out LegacyCharacterStateSaveRequest normalizedRequest,
        out string path)
    {
        normalizedRequest = request;
        path = string.Empty;
        if (request is null ||
            request.Mob is null ||
            request.MobExtra is null ||
            request.CharacterSlot < 0 ||
            request.CharacterSlot >= LegacyAccountSnapshot.CharacterCount ||
            request.Mob.Length != LegacyAccountSnapshot.CharacterStride ||
            request.MobExtra.Length != LegacyAccountSnapshot.MobExtraStride ||
            !TryResolveAccountPath(request.AccountName, out var normalizedAccountName, out path))
            return false;

        normalizedRequest = request with { AccountName = normalizedAccountName };
        return true;
    }

    private static AtomicCharacterStateSaveResult ApplyAtomicCharacterState(
        byte[] accountFile,
        LegacyCharacterStateSaveRequest request)
    {
        var characterOffset = LegacyAccountSnapshot.CharactersOffset + (request.CharacterSlot * LegacyAccountSnapshot.CharacterStride);
        if (accountFile[characterOffset + LegacyAccountSnapshot.MobNameOffset] == 0)
            return AtomicCharacterStateSaveResult.CharacterNotAvailable;

        request.Mob.CopyTo(accountFile.AsSpan(characterOffset, LegacyAccountSnapshot.CharacterStride));
        BinaryPrimitives.WriteInt16LittleEndian(
            accountFile.AsSpan(characterOffset + LegacyAccountSnapshot.MobSavedPositionXOffset), request.PositionX);
        BinaryPrimitives.WriteInt16LittleEndian(
            accountFile.AsSpan(characterOffset + LegacyAccountSnapshot.MobSavedPositionYOffset), request.PositionY);
        request.MobExtra.CopyTo(accountFile.AsSpan(
            LegacyAccountSnapshot.MobExtraOffset + (request.CharacterSlot * LegacyAccountSnapshot.MobExtraStride),
            LegacyAccountSnapshot.MobExtraStride));

        var coin = BinaryPrimitives.ReadInt32LittleEndian(request.Mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset));
        BinaryPrimitives.WriteInt32LittleEndian(accountFile.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), coin);
        return AtomicCharacterStateSaveResult.Success;
    }

    private async ValueTask RecoverAtomicJournalAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(atomicJournalPath))
            return;

        AtomicJournalDocument? document;
        try
        {
            var json = await File.ReadAllTextAsync(atomicJournalPath, cancellationToken);
            document = JsonSerializer.Deserialize<AtomicJournalDocument>(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The legacy file account atomic journal is invalid.", exception);
        }

        ValidateAtomicJournal(document);
        var entries = document!.Files!;
        if (document.Phase == AtomicPreparedPhase)
        {
            for (var index = entries.Count - 1; index >= 0; index--)
                await WriteAtomicBlobAsync(entries[index].Path, entries[index].Before, cancellationToken);
        }
        else
        {
            foreach (var entry in entries)
                await WriteAtomicBlobAsync(entry.Path, entry.After, cancellationToken);
        }

        DeleteAtomicJournal();
    }

    private async ValueTask WriteAtomicJournalAsync(AtomicJournalDocument document, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(accountRoot);
        var temporaryPath = atomicJournalPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan))
            {
                await JsonSerializer.SerializeAsync(stream, document, cancellationToken: cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, atomicJournalPath, overwrite: true);
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
        }
    }

    private static async ValueTask WriteAtomicBlobAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidDataException("The legacy account atomic journal contains a path without a directory.");

        Directory.CreateDirectory(directory);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, FileOptions.SequentialScan))
            {
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
        }
    }

    private void ValidateAtomicJournal(AtomicJournalDocument? document)
    {
        if (document is null ||
            document.Version != AtomicJournalVersion ||
            document.Phase is not (AtomicPreparedPhase or AtomicCommittedPhase) ||
            document.Files is null ||
            document.Files.Count != 2)
            throw new InvalidDataException("The legacy file account atomic journal has an unsupported shape.");

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in document.Files)
        {
            if (entry is null ||
                string.IsNullOrWhiteSpace(entry.Path) ||
                !IsInsideAccountRoot(entry.Path) ||
                !paths.Add(Path.GetFullPath(entry.Path)) ||
                entry.Before is null ||
                entry.After is null ||
                entry.Before.Length < LegacyAccountSnapshot.RequiredFileLength ||
                entry.After.Length != entry.Before.Length)
                throw new InvalidDataException("The legacy file account atomic journal contains an invalid file entry.");
        }
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void DeleteAtomicJournal()
    {
        try { File.Delete(atomicJournalPath); }
        catch (FileNotFoundException) { }
    }

    private sealed record AtomicJournalDocument(int Version, string Phase, IReadOnlyList<AtomicJournalEntry> Files);

    private sealed record AtomicJournalEntry(string Path, byte[] Before, byte[] After);

    private static void WriteCString(Span<byte> destination, string value)
    {
        destination.Clear();
        var length = Math.Min(value.Length, destination.Length - 1);
        Encoding.ASCII.GetBytes(value.AsSpan(0, length), destination);
    }

    private bool TryResolveAccountPath(string accountName, out string normalized, out string path)
    {
        path = string.Empty;
        if (!TryNormalizeAccountName(accountName, out normalized)) return false;

        var candidate = Path.Combine(accountRoot, GetLegacyDirectory(normalized), normalized);
        if (!IsInsideAccountRoot(candidate)) return false;

        path = candidate;
        return true;
    }

    private bool IsInsideAccountRoot(string path)
    {
        var rootWithSeparator = Path.EndsInDirectorySeparator(accountRoot) ? accountRoot : accountRoot + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryNormalizeAccountName(string accountName, out string normalized)
    {
        normalized = accountName.ToUpperInvariant();
        if (normalized.Length is < 1 or >= AccountNameLength) return false;
        return normalized.All(static character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-');
    }

    private static string GetLegacyDirectory(string accountName) => accountName[0] is >= 'A' and <= 'Z' ? accountName[..1] : "etc";

    private static string ReadCString(ReadOnlySpan<byte> source)
    {
        var terminator = source.IndexOf((byte)0);
        return Encoding.ASCII.GetString(terminator < 0 ? source : source[..terminator]);
    }
}

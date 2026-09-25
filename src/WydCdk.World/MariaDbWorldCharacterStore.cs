using System.Buffers.Binary;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// MariaDB persistence for the confirmed legacy account/character payload.
/// The blob is deliberately opaque: it keeps the x86 STRUCT_ACCOUNTFILE offsets already validated by the
/// protocol tests while moving ownership out of the legacy account directory. World state is keyed by the
/// explicit UP/PVP string, so the same account can have an independent character snapshot in each world.
/// </summary>
public sealed class MariaDbWorldCharacterStore(Func<DbConnection> connectionFactory, string worldKey) : ICharacterStore, IClientEquipmentStateStore, IDonateBalanceStore, IAtomicCharacterStateStore, ILegacyAutoTradePurchaseCommitStore
{
    private const int AccountNameLength = 16;
    private const int CharacterNameLength = 16;
    private const byte UnsetPin = 0xFF;
    private readonly Func<DbConnection> connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    private readonly string worldKey = NormalizeWorldKey(worldKey);
    private readonly MariaDbDonateShopStore donateStore = new(connectionFactory);
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async ValueTask<LegacyAccountSnapshot?> ReadSnapshotAsync(string accountName, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAccountName(accountName);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var blob = await ReadBlobAsync(connection, null, normalized, forUpdate: false, cancellationToken);
        return blob is null ? null : LegacyAccountSnapshot.Read(normalized, blob);
    }

    public async ValueTask<byte[]?> ReadNumericTokenAsync(string accountName, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAccountName(accountName);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        if (!await AccountExistsAsync(connection, null, normalized, cancellationToken))
            return null;

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT numeric_token FROM wyd_account_security WHERE account_name = @account_name LIMIT 1";
        AddParameter(command, "@account_name", normalized);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is byte[] token && token.Length == LegacyAccountSnapshot.NumericTokenLength)
            return token;

        var unset = new byte[LegacyAccountSnapshot.NumericTokenLength];
        Array.Fill(unset, UnsetPin);
        return unset;
    }

    public async ValueTask<bool> TryWriteNumericTokenAsync(string accountName, ReadOnlyMemory<byte> token, CancellationToken cancellationToken = default)
    {
        if (token.Length != LegacyAccountSnapshot.NumericTokenLength)
            throw new ArgumentException("A numeric token must contain exactly six bytes.", nameof(token));

        var normalized = NormalizeAccountName(accountName);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        if (!await AccountExistsAsync(connection, null, normalized, cancellationToken))
            return false;

        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO wyd_account_security (account_name, numeric_token) VALUES (@account_name, @numeric_token) ON DUPLICATE KEY UPDATE numeric_token = VALUES(numeric_token)";
        AddParameter(command, "@account_name", normalized);
        AddParameter(command, "@numeric_token", token.ToArray());
        await command.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    public async ValueTask<LegacyCharacterLoginData?> ReadCharacterLoginDataAsync(string accountName, int slot, CancellationToken cancellationToken = default)
    {
        if (!IsValidSlot(slot)) return null;
        var normalized = NormalizeAccountName(accountName);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var blob = await ReadBlobAsync(connection, null, normalized, forUpdate: false, cancellationToken);
        if (blob is null) return null;

        var mob = Slice(blob, LegacyAccountSnapshot.CharactersOffset + (slot * LegacyAccountSnapshot.CharacterStride), LegacyAccountSnapshot.CharacterStride);
        if (mob[LegacyAccountSnapshot.MobNameOffset] == 0) return null;
        var shortSkill = Slice(blob, LegacyAccountSnapshot.ShortSkillOffset + (slot * LegacyAccountSnapshot.ShortSkillStride), LegacyAccountSnapshot.ShortSkillStride);
        var affect = Slice(blob, LegacyAccountSnapshot.AffectOffset + (slot * LegacyAccountSnapshot.AffectStride), LegacyAccountSnapshot.AffectStride);
        var mobExtra = Slice(blob, LegacyAccountSnapshot.MobExtraOffset + (slot * LegacyAccountSnapshot.MobExtraStride), LegacyAccountSnapshot.MobExtraStride);
        var donate = BinaryPrimitives.ReadInt32LittleEndian(blob.AsSpan(LegacyAccountSnapshot.DonateOffset, sizeof(int)));
        var savedX = BinaryPrimitives.ReadInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobSavedPositionXOffset));
        var savedY = BinaryPrimitives.ReadInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobSavedPositionYOffset));
        var clientEquipment = await ReadClientEquipmentAsync(connection, null, normalized, slot, forUpdate: false, cancellationToken);
        return new LegacyCharacterLoginData(mob, shortSkill, affect, mobExtra, donate, savedX, savedY, clientEquipment);
    }

    public async ValueTask<IReadOnlyList<LegacyItem>?> ReadClientEquipmentAsync(string accountName, int slot, CancellationToken cancellationToken = default)
    {
        if (!IsValidSlot(slot)) return null;
        var normalized = NormalizeAccountName(accountName);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return await ReadClientEquipmentAsync(connection, null, normalized, slot, forUpdate: false, cancellationToken);
    }

    public async ValueTask<ClientEquipmentStateSaveResult> TrySaveClientEquipmentAsync(
        string accountName,
        int slot,
        IReadOnlyList<LegacyItem> equipment,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidSlot(slot)) return ClientEquipmentStateSaveResult.SlotOutOfRange;
        if (equipment is null || equipment.Count != ClientEquipmentStateV769.EquipmentCount)
            return ClientEquipmentStateSaveResult.InvalidLength;

        var normalized = NormalizeAccountName(accountName);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var blob = await ReadBlobAsync(connection, transaction, normalized, forUpdate: true, cancellationToken);
            if (blob is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ClientEquipmentStateSaveResult.AccountNotFound;
            }

            var characterOffset = LegacyAccountSnapshot.CharactersOffset + (slot * LegacyAccountSnapshot.CharacterStride);
            if (blob[characterOffset + LegacyAccountSnapshot.MobNameOffset] == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ClientEquipmentStateSaveResult.CharacterNotAvailable;
            }

            await UpsertClientEquipmentAsync(
                connection,
                transaction,
                normalized,
                slot,
                new ClientEquipmentStateV769(equipment).ToBytes(),
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ClientEquipmentStateSaveResult.Success;
        }
        catch (DbException)
        {
            try { await transaction.RollbackAsync(CancellationToken.None); } catch (DbException) { }
            return ClientEquipmentStateSaveResult.NotAvailable;
        }
    }

    public ValueTask<int?> ReadDonateAsync(string accountName, CancellationToken cancellationToken = default) =>
        donateStore.ReadDonateAsync(accountName, cancellationToken);

    public ValueTask<DonateBalanceSaveResult> TrySaveDonateAsync(string accountName, int donate, int? expectedDonate = null, CancellationToken cancellationToken = default) =>
        donateStore.TrySaveDonateAsync(accountName, donate, expectedDonate, cancellationToken);

    public ValueTask<CharacterPositionSaveResult> TrySaveCharacterPositionAsync(string accountName, int slot, short positionX, short positionY, CancellationToken cancellationToken = default)
    {
        if (!IsValidSlot(slot)) return ValueTask.FromResult(CharacterPositionSaveResult.SlotOutOfRange);
        return MutateBlobAsync(accountName, slot, CharacterPositionSaveResult.AccountNotFound, (blob, characterOffset) =>
        {
            if (blob[characterOffset + LegacyAccountSnapshot.MobNameOffset] == 0)
                return (false, CharacterPositionSaveResult.NotAvailable);
            BinaryPrimitives.WriteInt16LittleEndian(blob.AsSpan(characterOffset + LegacyAccountSnapshot.MobSavedPositionXOffset), positionX);
            BinaryPrimitives.WriteInt16LittleEndian(blob.AsSpan(characterOffset + LegacyAccountSnapshot.MobSavedPositionYOffset), positionY);
            return (true, CharacterPositionSaveResult.Success);
        }, cancellationToken);
    }

    public ValueTask<CharacterStateSaveResult> TrySaveCharacterStateAsync(string accountName, int slot, ReadOnlyMemory<byte> mob, short positionX, short positionY, CancellationToken cancellationToken = default, ReadOnlyMemory<byte> mobExtra = default)
    {
        if (!IsValidSlot(slot)) return ValueTask.FromResult(CharacterStateSaveResult.SlotOutOfRange);
        if (mob.Length != LegacyAccountSnapshot.CharacterStride || (!mobExtra.IsEmpty && mobExtra.Length != LegacyAccountSnapshot.MobExtraStride))
            return ValueTask.FromResult(CharacterStateSaveResult.InvalidLength);

        return MutateBlobAsync(accountName, slot, CharacterStateSaveResult.AccountNotFound, (blob, characterOffset) =>
        {
            if (blob[characterOffset + LegacyAccountSnapshot.MobNameOffset] == 0)
                return (false, CharacterStateSaveResult.NotAvailable);
            mob.CopyTo(blob.AsMemory(characterOffset, LegacyAccountSnapshot.CharacterStride));
            BinaryPrimitives.WriteInt16LittleEndian(blob.AsSpan(characterOffset + LegacyAccountSnapshot.MobSavedPositionXOffset), positionX);
            BinaryPrimitives.WriteInt16LittleEndian(blob.AsSpan(characterOffset + LegacyAccountSnapshot.MobSavedPositionYOffset), positionY);
            var coin = BinaryPrimitives.ReadInt32LittleEndian(mob.Span[LegacyAccountSnapshot.MobCoinOffset..]);
            BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), coin);
            if (!mobExtra.IsEmpty)
                mobExtra.CopyTo(blob.AsMemory(LegacyAccountSnapshot.MobExtraOffset + (slot * LegacyAccountSnapshot.MobExtraStride), LegacyAccountSnapshot.MobExtraStride));
            return (true, CharacterStateSaveResult.Success);
        }, cancellationToken);
    }

    public async ValueTask<AtomicCharacterStateSaveResult> TrySaveCharacterStatesAtomicallyAsync(
        LegacyCharacterStateSaveRequest first,
        LegacyCharacterStateSaveRequest second,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidAtomicStateRequest(first) || !IsValidAtomicStateRequest(second))
            return AtomicCharacterStateSaveResult.InvalidRequest;

        var firstAccount = NormalizeAccountName(first.AccountName);
        var secondAccount = NormalizeAccountName(second.AccountName);
        if (string.Equals(firstAccount, secondAccount, StringComparison.Ordinal))
            return AtomicCharacterStateSaveResult.InvalidRequest;

        var ordered = new[]
        {
            (AccountName: firstAccount, Request: first),
            (AccountName: secondAccount, Request: second),
        }
        .OrderBy(static value => value.AccountName, StringComparer.Ordinal)
        .ToArray();

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var blobs = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (var entry in ordered)
        {
            if (!blobs.TryGetValue(entry.AccountName, out var blob))
            {
                blob = await ReadBlobAsync(connection, transaction, entry.AccountName, forUpdate: true, cancellationToken);
                if (blob is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return AtomicCharacterStateSaveResult.AccountNotFound;
                }

                blobs.Add(entry.AccountName, blob);
            }

            var characterOffset = LegacyAccountSnapshot.CharactersOffset + (entry.Request.CharacterSlot * LegacyAccountSnapshot.CharacterStride);
            if (blob[characterOffset + LegacyAccountSnapshot.MobNameOffset] == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return AtomicCharacterStateSaveResult.CharacterNotAvailable;
            }

            entry.Request.Mob.AsMemory().CopyTo(blob.AsMemory(characterOffset, LegacyAccountSnapshot.CharacterStride));
            BinaryPrimitives.WriteInt16LittleEndian(blob.AsSpan(characterOffset + LegacyAccountSnapshot.MobSavedPositionXOffset), entry.Request.PositionX);
            BinaryPrimitives.WriteInt16LittleEndian(blob.AsSpan(characterOffset + LegacyAccountSnapshot.MobSavedPositionYOffset), entry.Request.PositionY);
            entry.Request.MobExtra.AsMemory().CopyTo(blob.AsMemory(LegacyAccountSnapshot.MobExtraOffset + (entry.Request.CharacterSlot * LegacyAccountSnapshot.MobExtraStride), LegacyAccountSnapshot.MobExtraStride));

            // The legacy account snapshot exposes Coin both inside the MOB and
            // at the account-wide offset used by character selection.
            var coin = BinaryPrimitives.ReadInt32LittleEndian(entry.Request.Mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset));
            BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), coin);
        }

        foreach (var entry in blobs.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            await UpsertBlobAsync(connection, transaction, entry.Key, entry.Value, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return AtomicCharacterStateSaveResult.Success;
    }

    /// <summary>
    /// Commits the autotrade purchase and its listing transition in the same
    /// InnoDB transaction. Account rows are locked in deterministic order;
    /// the listing row is locked before its compare-and-swap check. The file
    /// adapter deliberately does not claim this capability because its account
    /// blob and listing JSON are separate files without a transaction log.
    /// </summary>
    public async ValueTask<LegacyAutoTradePurchaseCommitResult> TryCommitAsync(
        LegacyAutoTradePurchaseCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (LegacyAutoTradePurchaseCommitRules.Validate(request) != LegacyAutoTradePurchaseCommitResult.Committed)
            return LegacyAutoTradePurchaseCommitResult.InvalidRequest;

        string buyerAccount;
        string sellerAccount;
        try
        {
            buyerAccount = NormalizeAccountName(request.BuyerAccountName);
            sellerAccount = NormalizeAccountName(request.SellerAccountName);
        }
        catch (ArgumentException)
        {
            return LegacyAutoTradePurchaseCommitResult.InvalidRequest;
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var orderedAccounts = new[] { buyerAccount, sellerAccount }
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        var blobs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var account in orderedAccounts)
        {
            var blob = await ReadBlobAsync(connection, transaction, account, forUpdate: true, cancellationToken);
            if (blob is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return LegacyAutoTradePurchaseCommitResult.AccountNotFound;
            }

            blobs.Add(account, blob);
        }

        var buyerBlob = blobs[buyerAccount];
        var sellerBlob = blobs[sellerAccount];
        var buyerCharacterOffset = LegacyAccountSnapshot.CharactersOffset + (request.BuyerCharacterSlot * LegacyAccountSnapshot.CharacterStride);
        var sellerCharacterOffset = LegacyAccountSnapshot.CharactersOffset + (request.SellerCharacterSlot * LegacyAccountSnapshot.CharacterStride);
        if (buyerBlob[buyerCharacterOffset + LegacyAccountSnapshot.MobNameOffset] == 0 ||
            sellerBlob[sellerCharacterOffset + LegacyAccountSnapshot.MobNameOffset] == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return LegacyAutoTradePurchaseCommitResult.CharacterNotAvailable;
        }

        var currentBuyerMob = buyerBlob.AsSpan(buyerCharacterOffset, LegacyAccountSnapshot.CharacterStride);
        if (!currentBuyerMob.SequenceEqual(request.ExpectedPersistedBuyerMob) ||
            LegacyItem.Read(currentBuyerMob[(LegacyAccountSnapshot.MobCarryOffset + (request.Plan.BuyerDestinationSlot * LegacyItem.SizeInBytes))..]) != default)
        {
            await transaction.RollbackAsync(cancellationToken);
            return LegacyAutoTradePurchaseCommitResult.Conflict;
        }

        var currentSellerCoin = BinaryPrimitives.ReadInt32LittleEndian(sellerBlob.AsSpan(LegacyAccountSnapshot.AccountCoinOffset));
        var currentSellerItem = LegacyItem.Read(sellerBlob.AsSpan(
            LegacyAccountSnapshot.CargoOffset + (request.Plan.SellerCargoPosition * LegacyItem.SizeInBytes),
            LegacyItem.SizeInBytes));
        if (currentSellerCoin != request.ExpectedSellerCoin || currentSellerItem != request.Plan.PurchasedItem)
        {
            await transaction.RollbackAsync(cancellationToken);
            return LegacyAutoTradePurchaseCommitResult.Conflict;
        }

        var currentListing = await ReadAutoTradeListingAsync(
            connection,
            transaction,
            sellerAccount,
            request.SellerCharacterSlot,
            forUpdate: true,
            cancellationToken);
        if (currentListing is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return LegacyAutoTradePurchaseCommitResult.ListingNotFound;
        }

        if (!LegacyAutoTradePurchaseCommitRules.SameListing(currentListing, request.ExpectedListing))
        {
            await transaction.RollbackAsync(cancellationToken);
            return LegacyAutoTradePurchaseCommitResult.Conflict;
        }

        request.BuyerMob.AsMemory().CopyTo(buyerBlob.AsMemory(buyerCharacterOffset, LegacyAccountSnapshot.CharacterStride));
        BinaryPrimitives.WriteInt16LittleEndian(buyerBlob.AsSpan(buyerCharacterOffset + LegacyAccountSnapshot.MobSavedPositionXOffset), request.BuyerPositionX);
        BinaryPrimitives.WriteInt16LittleEndian(buyerBlob.AsSpan(buyerCharacterOffset + LegacyAccountSnapshot.MobSavedPositionYOffset), request.BuyerPositionY);
        request.BuyerMobExtra.AsMemory().CopyTo(buyerBlob.AsMemory(
            LegacyAccountSnapshot.MobExtraOffset + (request.BuyerCharacterSlot * LegacyAccountSnapshot.MobExtraStride),
            LegacyAccountSnapshot.MobExtraStride));
        BinaryPrimitives.WriteInt32LittleEndian(buyerBlob.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), request.Plan.BuyerCoin);

        sellerBlob.AsSpan(
            LegacyAccountSnapshot.CargoOffset + (request.Plan.SellerCargoPosition * LegacyItem.SizeInBytes),
            LegacyItem.SizeInBytes).Clear();
        BinaryPrimitives.WriteInt32LittleEndian(sellerBlob.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), request.Plan.SellerCoin);

        await UpsertBlobAsync(connection, transaction, buyerAccount, buyerBlob, cancellationToken);
        await UpsertBlobAsync(connection, transaction, sellerAccount, sellerBlob, cancellationToken);
        await UpsertAutoTradeListingAsync(connection, transaction, request.UpdatedListing, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return LegacyAutoTradePurchaseCommitResult.Committed;
    }

    public ValueTask<CharacterGuildSaveResult> TrySaveCharacterGuildAsync(string accountName, int slot, int guildId, int coin, CancellationToken cancellationToken = default)
    {
        if (!IsValidSlot(slot)) return ValueTask.FromResult(CharacterGuildSaveResult.SlotOutOfRange);
        return MutateBlobAsync(accountName, slot, CharacterGuildSaveResult.AccountNotFound, (blob, characterOffset) =>
        {
            if (blob[characterOffset + LegacyAccountSnapshot.MobNameOffset] == 0)
                return (false, CharacterGuildSaveResult.NotAvailable);
            BinaryPrimitives.WriteUInt16LittleEndian(blob.AsSpan(characterOffset + LegacyAccountSnapshot.MobGuildOffset), checked((ushort)guildId));
            BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(characterOffset + LegacyAccountSnapshot.MobCoinOffset), coin);
            BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), coin);
            return (true, CharacterGuildSaveResult.Success);
        }, cancellationToken);
    }

    public ValueTask<CharacterShortSkillSaveResult> TrySaveCharacterShortSkillsAsync(string accountName, int slot, ReadOnlyMemory<byte> skillBar, ReadOnlyMemory<byte> shortSkills, CancellationToken cancellationToken = default)
    {
        if (!IsValidSlot(slot)) return ValueTask.FromResult(CharacterShortSkillSaveResult.SlotOutOfRange);
        if (skillBar.Length != 4 || shortSkills.Length != LegacyAccountSnapshot.ShortSkillStride)
            return ValueTask.FromResult(CharacterShortSkillSaveResult.InvalidLength);

        return MutateBlobAsync(accountName, slot, CharacterShortSkillSaveResult.AccountNotFound, (blob, characterOffset) =>
        {
            if (blob[characterOffset + LegacyAccountSnapshot.MobNameOffset] == 0)
                return (false, CharacterShortSkillSaveResult.NotAvailable);
            skillBar.CopyTo(blob.AsMemory(characterOffset + LegacyAccountSnapshot.MobSkillBarOffset, skillBar.Length));
            shortSkills.CopyTo(blob.AsMemory(LegacyAccountSnapshot.ShortSkillOffset + (slot * LegacyAccountSnapshot.ShortSkillStride), shortSkills.Length));
            return (true, CharacterShortSkillSaveResult.Success);
        }, cancellationToken);
    }

    public async ValueTask<bool?> IsCharacterSlotEmptyAsync(string accountName, int slot, CancellationToken cancellationToken = default)
    {
        if (!IsValidSlot(slot)) return null;
        var normalized = NormalizeAccountName(accountName);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        if (!await AccountExistsAsync(connection, null, normalized, cancellationToken)) return null;
        var blob = await ReadBlobAsync(connection, null, normalized, forUpdate: false, cancellationToken);
        return blob is null || blob[LegacyAccountSnapshot.CharactersOffset + (slot * LegacyAccountSnapshot.CharacterStride) + LegacyAccountSnapshot.MobNameOffset] == 0;
    }

    public async ValueTask<bool> TryReserveCharacterNameAsync(string characterName, string accountName, CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeCharacterName(characterName);
        var normalizedAccount = NormalizeAccountName(accountName);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO wyd_character_names (world_key, character_name, account_name, slot_index) VALUES (@world_key, @character_name, @account_name, NULL)";
        AddParameter(command, "@world_key", worldKey);
        AddParameter(command, "@character_name", normalizedName);
        AddParameter(command, "@account_name", normalizedAccount);
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }

    public async ValueTask<bool> TryWriteNewCharacterAsync(string accountName, int slot, string characterName, ReadOnlyMemory<byte> template, CancellationToken cancellationToken = default)
    {
        if (!IsValidSlot(slot)) return false;
        if (template.Length != LegacyAccountSnapshot.CharacterStride)
            throw new ArgumentException("Character template must be a full STRUCT_MOB.", nameof(template));

        var normalizedAccount = NormalizeAccountName(accountName);
        var normalizedCharacter = NormalizeCharacterName(characterName);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (!await AccountExistsAsync(connection, transaction, normalizedAccount, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        var blob = await ReadBlobAsync(connection, transaction, normalizedAccount, forUpdate: true, cancellationToken) ?? new byte[LegacyAccountSnapshot.RequiredFileLength];
        var characterOffset = LegacyAccountSnapshot.CharactersOffset + (slot * LegacyAccountSnapshot.CharacterStride);
        if (blob[characterOffset + LegacyAccountSnapshot.MobNameOffset] != 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        var mob = template.ToArray();
        WriteCString(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset, CharacterNameLength), normalizedCharacter);
        mob.CopyTo(blob, characterOffset);
        blob.AsSpan(LegacyAccountSnapshot.ShortSkillOffset + (slot * LegacyAccountSnapshot.ShortSkillStride), LegacyAccountSnapshot.ShortSkillStride).Fill(0xFF);
        blob.AsSpan(LegacyAccountSnapshot.AffectOffset + (slot * LegacyAccountSnapshot.AffectStride), LegacyAccountSnapshot.AffectStride).Clear();
        var mobExtraOffset = LegacyAccountSnapshot.MobExtraOffset + (slot * LegacyAccountSnapshot.MobExtraStride);
        blob.AsSpan(mobExtraOffset, LegacyAccountSnapshot.MobExtraStride).Clear();
        BinaryPrimitives.WriteInt16LittleEndian(blob.AsSpan(mobExtraOffset + LegacyAccountSnapshot.MobExtraClassMasterOffset), LegacyAccountSnapshot.ClassMasterMortal);
        var faceIndex = BinaryPrimitives.ReadInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset));
        BinaryPrimitives.WriteInt16LittleEndian(blob.AsSpan(mobExtraOffset + LegacyAccountSnapshot.MobExtraMortalFaceOffset), faceIndex);

        await UpsertBlobAsync(connection, transaction, normalizedAccount, blob, cancellationToken);
        using var reservation = connection.CreateCommand();
        reservation.Transaction = transaction;
        reservation.CommandText = "UPDATE wyd_character_names SET slot_index = @slot_index WHERE world_key = @world_key AND character_name = @character_name AND account_name = @account_name";
        AddParameter(reservation, "@slot_index", slot);
        AddParameter(reservation, "@world_key", worldKey);
        AddParameter(reservation, "@character_name", normalizedCharacter);
        AddParameter(reservation, "@account_name", normalizedAccount);
        await reservation.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async ValueTask<DeleteCharacterStoreResult> TryDeleteCharacterAsync(string accountName, int slot, string suppliedPassword, CancellationToken cancellationToken = default)
    {
        if (!IsValidSlot(slot)) return DeleteCharacterStoreResult.NotAvailable;
        var normalizedAccount = NormalizeAccountName(accountName);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        using var auth = connection.CreateCommand();
        auth.Transaction = transaction;
        auth.CommandText = "SELECT password_hash, password_scheme FROM accounts WHERE UPPER(account_name) = @account_name LIMIT 1";
        AddParameter(auth, "@account_name", normalizedAccount);
        await using var reader = await auth.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return DeleteCharacterStoreResult.AccountNotFound;
        }
        var hash = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
        var scheme = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        await reader.DisposeAsync();
        if (!string.Equals(scheme, "password_hash", StringComparison.OrdinalIgnoreCase) || !BCrypt.Net.BCrypt.Verify(suppliedPassword, hash))
        {
            await transaction.RollbackAsync(cancellationToken);
            return DeleteCharacterStoreResult.WrongPassword;
        }

        var blob = await ReadBlobAsync(connection, transaction, normalizedAccount, forUpdate: true, cancellationToken);
        if (blob is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return DeleteCharacterStoreResult.NotAvailable;
        }
        var characterOffset = LegacyAccountSnapshot.CharactersOffset + (slot * LegacyAccountSnapshot.CharacterStride);
        var name = ReadCString(blob.AsSpan(characterOffset + LegacyAccountSnapshot.MobNameOffset, CharacterNameLength));
        if (name.Length == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return DeleteCharacterStoreResult.NotAvailable;
        }
        blob.AsSpan(LegacyAccountSnapshot.AffectOffset + (slot * LegacyAccountSnapshot.AffectStride), LegacyAccountSnapshot.AffectStride).Clear();
        LegacyCharacterStorageDefaults.ResetDeletedCharacterSlot(blob, slot);
        await UpsertBlobAsync(connection, transaction, normalizedAccount, blob, cancellationToken);

        using var reservation = connection.CreateCommand();
        reservation.Transaction = transaction;
        reservation.CommandText = "DELETE FROM wyd_character_names WHERE world_key = @world_key AND character_name = @character_name AND account_name = @account_name";
        AddParameter(reservation, "@world_key", worldKey);
        AddParameter(reservation, "@character_name", name);
        AddParameter(reservation, "@account_name", normalizedAccount);
        await reservation.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return DeleteCharacterStoreResult.Success;
    }

    private async ValueTask<T> MutateBlobAsync<T>(string accountName, int slot, T missingResult, Func<byte[], int, (bool Changed, T Result)> mutate, CancellationToken cancellationToken)
    {
        var normalized = NormalizeAccountName(accountName);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var blob = await ReadBlobAsync(connection, transaction, normalized, forUpdate: true, cancellationToken);
        if (blob is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return missingResult;
        }
        var result = mutate(blob, LegacyAccountSnapshot.CharactersOffset + (slot * LegacyAccountSnapshot.CharacterStride));
        if (!result.Changed)
        {
            await transaction.RollbackAsync(cancellationToken);
            return result.Result;
        }
        await UpsertBlobAsync(connection, transaction, normalized, blob, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result.Result;
    }

    private async ValueTask<byte[]?> ReadBlobAsync(DbConnection connection, DbTransaction? transaction, string accountName, bool forUpdate, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT account_blob FROM wyd_world_account_state WHERE account_name = @account_name AND world_key = @world_key LIMIT 1{(forUpdate ? " FOR UPDATE" : string.Empty)}";
        AddParameter(command, "@account_name", accountName);
        AddParameter(command, "@world_key", worldKey);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null || value is DBNull) return null;
        var bytes = value as byte[] ?? throw new InvalidDataException("MariaDB world account blob is not binary.");
        if (bytes.Length != LegacyAccountSnapshot.RequiredFileLength)
            throw new InvalidDataException($"MariaDB world account blob must be {LegacyAccountSnapshot.RequiredFileLength} bytes.");
        return bytes;
    }

    private async ValueTask<LegacyAutoTradePersistedListing?> ReadAutoTradeListingAsync(
        DbConnection connection,
        DbTransaction transaction,
        string accountName,
        int characterSlot,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT listing_json FROM wyd_autotrade_state WHERE world_key = @world_key AND account_name = @account_name AND character_slot = @character_slot LIMIT 1{(forUpdate ? " FOR UPDATE" : string.Empty)}";
        AddParameter(command, "@world_key", worldKey);
        AddParameter(command, "@account_name", accountName);
        AddParameter(command, "@character_slot", characterSlot);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null or DBNull)
            return null;

        var json = value switch
        {
            string text => text,
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            _ => throw new InvalidDataException("MariaDB autotrade listing_json is not text or bytes."),
        };
        return JsonSerializer.Deserialize<LegacyAutoTradePersistedListing>(json, jsonOptions)
            ?? throw new InvalidDataException("MariaDB autotrade listing_json is empty.");
    }

    private async ValueTask<IReadOnlyList<LegacyItem>?> ReadClientEquipmentAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string accountName,
        int characterSlot,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT equipment_blob FROM wyd_client_equipment_769 WHERE world_key = @world_key AND account_name = @account_name AND character_slot = @character_slot LIMIT 1{(forUpdate ? " FOR UPDATE" : string.Empty)}";
        AddParameter(command, "@world_key", worldKey);
        AddParameter(command, "@account_name", accountName);
        AddParameter(command, "@character_slot", characterSlot);

        // The extension table is optional during the migration window. A missing table
        // therefore means "legacy 16 slots + two empty client slots", not failed login.
        try
        {
            var value = await command.ExecuteScalarAsync(cancellationToken);
            if (value is null or DBNull) return null;
            var bytes = value as byte[] ?? throw new InvalidDataException("MariaDB client equipment is not binary.");
            return ClientEquipmentStateV769.FromBytes(bytes).Equipment;
        }
        catch (DbException)
        {
            return null;
        }
    }

    private async ValueTask UpsertClientEquipmentAsync(
        DbConnection connection,
        DbTransaction transaction,
        string accountName,
        int characterSlot,
        byte[] equipmentBlob,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO wyd_client_equipment_769 (world_key, account_name, character_slot, equipment_blob) VALUES (@world_key, @account_name, @character_slot, @equipment_blob) ON DUPLICATE KEY UPDATE equipment_blob = VALUES(equipment_blob), version = version + 1, updated_at = CURRENT_TIMESTAMP";
        AddParameter(command, "@world_key", worldKey);
        AddParameter(command, "@account_name", accountName);
        AddParameter(command, "@character_slot", characterSlot);
        AddParameter(command, "@equipment_blob", equipmentBlob);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async ValueTask UpsertAutoTradeListingAsync(
        DbConnection connection,
        DbTransaction transaction,
        LegacyAutoTradePersistedListing listing,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO wyd_autotrade_state (world_key, account_name, character_slot, listing_json) VALUES (@world_key, @account_name, @character_slot, @listing_json) ON DUPLICATE KEY UPDATE listing_json = VALUES(listing_json), updated_at = CURRENT_TIMESTAMP";
        AddParameter(command, "@world_key", worldKey);
        AddParameter(command, "@account_name", listing.AccountName);
        AddParameter(command, "@character_slot", listing.CharacterSlot);
        AddParameter(command, "@listing_json", JsonSerializer.Serialize(listing, jsonOptions));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async ValueTask<bool> AccountExistsAsync(DbConnection connection, DbTransaction? transaction, string accountName, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM accounts WHERE UPPER(account_name) = @account_name LIMIT 1";
        AddParameter(command, "@account_name", accountName);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private async ValueTask UpsertBlobAsync(DbConnection connection, DbTransaction transaction, string accountName, byte[] blob, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO wyd_world_account_state (account_name, world_key, account_blob) VALUES (@account_name, @world_key, @account_blob) ON DUPLICATE KEY UPDATE account_blob = VALUES(account_blob), version = version + 1";
        AddParameter(command, "@account_name", accountName);
        AddParameter(command, "@world_key", worldKey);
        AddParameter(command, "@account_blob", blob);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbConnection CreateConnection() => connectionFactory() ?? throw new InvalidOperationException("The MariaDB connection factory returned null.");

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static byte[] Slice(byte[] source, int offset, int length) => source.AsSpan(offset, length).ToArray();

    private static bool IsValidSlot(int slot) => slot >= 0 && slot < LegacyAccountSnapshot.CharacterCount;

    private static bool IsValidAtomicStateRequest(LegacyCharacterStateSaveRequest request) =>
        !string.IsNullOrWhiteSpace(request.AccountName) &&
        IsValidSlot(request.CharacterSlot) &&
        request.Mob.Length == LegacyAccountSnapshot.CharacterStride &&
        request.MobExtra.Length == LegacyAccountSnapshot.MobExtraStride;

    private static string NormalizeWorldKey(string value)
    {
        if (!LegacyServerModePolicy.TryParseWorldKey(value, out var mode))
            throw new ArgumentException("World key must be UP or PVP.", nameof(value));
        return new LegacyServerModePolicy(mode).WorldKey;
    }

    private static string NormalizeAccountName(string value)
    {
        var normalized = value.ToUpperInvariant();
        if (normalized.Length is < 1 or >= AccountNameLength || !normalized.All(static c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-'))
            throw new ArgumentException("Invalid account name.", nameof(value));
        return normalized;
    }

    private static string NormalizeCharacterName(string value)
    {
        var normalized = value.ToUpperInvariant();
        if (normalized.Length is < 1 or >= CharacterNameLength)
            throw new ArgumentException("Invalid character name.", nameof(value));
        return normalized;
    }

    private static string ReadCString(ReadOnlySpan<byte> source)
    {
        var end = source.IndexOf((byte)0);
        return Encoding.ASCII.GetString(end < 0 ? source : source[..end]);
    }

    private static void WriteCString(Span<byte> destination, string value)
    {
        destination.Clear();
        Encoding.ASCII.GetBytes(value.AsSpan(0, Math.Min(value.Length, destination.Length - 1)), destination);
    }
}

using System.Buffers.Binary;
using MySqlConnector;
using WydCdk.Protocol;
using WydCdk.World;

// ITEM-EQUIPMENT-EXTENSION-LIFECYCLE-001: delete/recreate in the same slot must not
// rehydrate the 18-slot client equipment sidecar, and a missing sidecar table must not
// break character CRUD. Runs only against the isolated local test schema.
const string database = "wyd_cdk_autotrade_close_test";
const string sidecarTable = "wyd_client_equipment_769";
try
{
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 0; i < args.Length; i += 2)
    {
        if (i + 1 >= args.Length || !options.TryAdd(args[i], args[i + 1]))
            throw new InvalidOperationException("Use --config <protected password file> [--host 127.0.0.1] [--port 3306] [--database wyd_cdk_autotrade_close_test].");
    }
    if (options.Keys.Any(key => key is not "--config" and not "--host" and not "--port" and not "--database"))
        throw new InvalidOperationException("Unsupported option.");
    var host = options.GetValueOrDefault("--host", "127.0.0.1");
    if (host is not "127.0.0.1" and not "::1" || options.GetValueOrDefault("--database", database) != database)
        throw new InvalidOperationException("Safety gate: literal loopback and exact isolated test database required.");
    if (!options.TryGetValue("--config", out var config)) throw new InvalidOperationException("Protected password file required.");
    // The local root credential is a single-line file. Do not duplicate or log it.
    var password = (await File.ReadAllTextAsync(config)).TrimEnd('\r', '\n');
    var builder = new MySqlConnectionStringBuilder
    {
        Server = host, Port = uint.Parse(options.GetValueOrDefault("--port", "3306")),
        Database = database, UserID = "root", Password = password, Pooling = false,
        ConnectionTimeout = 5, DefaultCommandTimeout = 10,
    };
    await using var connection = new MySqlConnection(builder.ConnectionString);
    await connection.OpenAsync();
    // Never create/drop a database here. The caller must provision this exact test schema.
    await ExecuteAsync(connection, "CREATE TABLE IF NOT EXISTS accounts (account_name VARCHAR(15) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, password_hash VARCHAR(255) NOT NULL, password_scheme VARCHAR(32) NOT NULL, donate INT NOT NULL DEFAULT 0, PRIMARY KEY(account_name)) ENGINE=InnoDB");
    await ExecuteAsync(connection, "CREATE TABLE IF NOT EXISTS wyd_world_account_state (account_name VARCHAR(15) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, world_key VARCHAR(3) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, account_blob BLOB NOT NULL, version BIGINT UNSIGNED NOT NULL DEFAULT 0, created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6), updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6), PRIMARY KEY (account_name, world_key), KEY ix_wyd_world_account_state_world (world_key, account_name), CONSTRAINT ck_wyd_world_account_state_world CHECK (world_key IN ('UP', 'PVP')), CONSTRAINT ck_wyd_world_account_state_blob CHECK (OCTET_LENGTH(account_blob) = 7945)) ENGINE=InnoDB");
    await ExecuteAsync(connection, "CREATE TABLE IF NOT EXISTS wyd_character_names (world_key VARCHAR(3) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, character_name VARCHAR(15) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, account_name VARCHAR(15) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, slot_index TINYINT UNSIGNED NULL, created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6), updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6), PRIMARY KEY (world_key, character_name), KEY ix_wyd_character_names_account (world_key, account_name, slot_index), CONSTRAINT ck_wyd_character_names_world CHECK (world_key IN ('UP', 'PVP')), CONSTRAINT ck_wyd_character_names_slot CHECK (slot_index IS NULL OR slot_index < 4)) ENGINE=InnoDB");
    await ExecuteAsync(connection, $"CREATE TABLE IF NOT EXISTS {sidecarTable} (world_key VARCHAR(3) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, account_name VARCHAR(15) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, character_slot TINYINT UNSIGNED NOT NULL, equipment_blob VARBINARY(144) NOT NULL, version BIGINT UNSIGNED NOT NULL DEFAULT 0, created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6), updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6), PRIMARY KEY (world_key, account_name, character_slot), KEY ix_wyd_client_equipment_769_account (world_key, account_name), CONSTRAINT ck_wyd_client_equipment_769_world CHECK (world_key IN ('UP', 'PVP')), CONSTRAINT ck_wyd_client_equipment_769_slot CHECK (character_slot < 4), CONSTRAINT ck_wyd_client_equipment_769_size CHECK (OCTET_LENGTH(equipment_blob) = 144)) ENGINE=InnoDB");

    var store = new MariaDbWorldCharacterStore(() => new MySqlConnection(builder.ConnectionString), "UP");
    var pvpStore = new MariaDbWorldCharacterStore(() => new MySqlConnection(builder.ConnectionString), "PVP");
    // Test-only names from this run; cleanup never touches rows of another run.
    var account = NewName("L");
    var guildTargetAccount = NewName("Z");
    var testPassword = Guid.NewGuid().ToString("N");
    var characters = Enumerable.Range(0, 6).Select(_ => NewName("N")).ToArray();
    var guildSourceCharacter = NewName("G");
    var guildTargetCharacter = NewName("G");
    var guildSameAccountCharacter = NewName("G");
    var testAccounts = new[] { account, guildTargetAccount };
    var parked = $"{sidecarTable}_parked_{Guid.NewGuid():N}"[..60];
    var parkedActive = false;
    try
    {
        await InsertAccountAsync(connection, account, testPassword);

        var template = new byte[LegacyAccountSnapshot.CharacterStride];
        var extension = new LegacyItem[ClientEquipmentStateV769.EquipmentCount];
        extension[16] = new LegacyItem(3200, 1, 2, 3, 4, 5, 6);

        // 1. Same-slot delete/recreate does not rehydrate the sidecar.
        await CreateAsync(store, account, 0, characters[0], template);
        Require(await store.TrySaveClientEquipmentAsync(account, 0, extension) == ClientEquipmentStateSaveResult.Success, "Sidecar seed failed.");
        Require((await store.ReadClientEquipmentAsync(account, 0))?[16] == extension[16], "Sidecar seed not readable.");
        // Another world's row for the same account/slot must survive the UP lifecycle.
        await CreateAsync(pvpStore, account, 0, characters[5], template);
        Require(await pvpStore.TrySaveClientEquipmentAsync(account, 0, extension) == ClientEquipmentStateSaveResult.Success, "PVP sidecar seed failed.");
        Require(await store.TryDeleteCharacterAsync(account, 0, "wrong-" + testPassword) == DeleteCharacterStoreResult.WrongPassword, "Wrong password accepted.");
        Require(await store.ReadClientEquipmentAsync(account, 0) is not null, "Rejected delete removed the sidecar.");
        Require(await store.TryDeleteCharacterAsync(account, 0, testPassword) == DeleteCharacterStoreResult.Success, "Delete failed.");
        Require(await store.ReadClientEquipmentAsync(account, 0) is null, "Delete left the sidecar row (new connection readback).");
        Require((await pvpStore.ReadClientEquipmentAsync(account, 0))?[16] == extension[16], "UP delete removed the PVP sidecar.");
        await CreateAsync(store, account, 0, characters[1], template);
        var login = await store.ReadCharacterLoginDataAsync(account, 0);
        Require(login is not null && login.ClientEquipment is null, "Recreated character rehydrated old equipment.");

        // 2. A stale row written before this fix is cleared by creation in the same transaction.
        Require(await store.TrySaveClientEquipmentAsync(account, 0, extension) == ClientEquipmentStateSaveResult.Success, "Second seed failed.");
        using (var stale = connection.CreateCommand())
        {
            stale.CommandText = $"INSERT INTO {sidecarTable} (world_key, account_name, character_slot, equipment_blob) VALUES ('UP', @account_name, 1, @blob)";
            stale.Parameters.AddWithValue("@account_name", account);
            stale.Parameters.AddWithValue("@blob", new ClientEquipmentStateV769(extension).ToBytes());
            await stale.ExecuteNonQueryAsync();
        }
        await CreateAsync(store, account, 1, characters[2], template);
        Require(await store.ReadClientEquipmentAsync(account, 1) is null, "Create kept a stale sidecar row.");
        Require((await store.ReadClientEquipmentAsync(account, 0))?[16] == extension[16], "Create in slot 1 touched slot 0.");

        // 3. Missing sidecar table (migration window) does not break delete/create.
        await ExecuteAsync(connection, $"RENAME TABLE {sidecarTable} TO `{parked}`");
        parkedActive = true;
        Require(await store.TryDeleteCharacterAsync(account, 1, testPassword) == DeleteCharacterStoreResult.Success, "Delete failed without sidecar table.");
        await CreateAsync(store, account, 1, characters[3], template);
        Require((await store.ReadCharacterLoginDataAsync(account, 1))?.ClientEquipment is null, "Login failed without sidecar table.");
        await ExecuteAsync(connection, $"RENAME TABLE `{parked}` TO {sidecarTable}");
        parkedActive = false;

        // 4. Rollback: a delete whose later statement fails keeps blob and sidecar together.
        var trigger = "eq_fail_" + Guid.NewGuid().ToString("N");
        await ExecuteAsync(connection, $"CREATE TRIGGER `{trigger}` BEFORE DELETE ON wyd_character_names FOR EACH ROW BEGIN IF OLD.account_name = '{account}' AND OLD.character_name = '{characters[1]}' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'isolated lifecycle fault'; END IF; END");
        try
        {
            var failed = false;
            try { await store.TryDeleteCharacterAsync(account, 0, testPassword); }
            catch (MySqlException) { failed = true; }
            Require(failed, "Injected delete fault did not surface.");
            Require((await store.ReadClientEquipmentAsync(account, 0))?[16] == extension[16], "Failed delete committed the sidecar removal.");
            Require(await store.IsCharacterSlotEmptyAsync(account, 0) == false, "Failed delete committed the blob reset.");
        }
        finally
        {
            await ExecuteAsync(connection, $"DROP TRIGGER IF EXISTS `{trigger}`");
        }
        Require(await store.TryDeleteCharacterAsync(account, 0, testPassword) == DeleteCharacterStoreResult.Success, "Retry delete failed.");
        Require(await store.ReadClientEquipmentAsync(account, 0) is null, "Retry delete left the sidecar.");

        await CheckpointCompositeRoundTripAndRollbackAsync(connection, store, account, characters[4], template);
        await CargoSaleCasRoundTripConflictConcurrencyAndRollbackAsync(connection, store, account);
        await GuildInvitePairRoundTripConflictAndRollbackAsync(
            connection,
            store,
            account,
            guildTargetAccount,
            guildSourceCharacter,
            guildTargetCharacter,
            guildSameAccountCharacter,
            testPassword,
            template);

        Console.WriteLine("PASS MariaDB character lifecycle, checkpoint, Cargo CAS and guild invite pair: sidecar/blob rollback, stale conflict, concurrent CAS and pair transaction rollback verified.");
    }
    finally
    {
        if (parkedActive) await ExecuteAsync(connection, $"RENAME TABLE `{parked}` TO {sidecarTable}");
        foreach (var testAccount in testAccounts)
        {
            foreach (var table in new[] { sidecarTable, "wyd_character_names", "wyd_world_account_state", "accounts" })
            {
                using var cleanup = connection.CreateCommand();
                cleanup.CommandText = $"DELETE FROM {table} WHERE account_name = @account_name";
                cleanup.Parameters.AddWithValue("@account_name", testAccount);
                await cleanup.ExecuteNonQueryAsync();
            }
        }
    }
    return 0;
}
catch (Exception error)
{
    // Only classifications: provider exception messages may contain host/user/connection details.
    Console.Error.WriteLine(error is MySqlException sql
        ? $"FAIL local MariaDB integration: provider code {sql.Number}."
        : $"FAIL local MariaDB integration: {error.GetType().Name}: {(error is InvalidOperationException ? error.Message : string.Empty)}");
    return 1;
}

static async Task InsertAccountAsync(MySqlConnection connection, string account, string password)
{
    using var insert = connection.CreateCommand();
    insert.CommandText = "INSERT INTO accounts (account_name, password_hash, password_scheme) VALUES (@account_name, @hash, 'password_hash')";
    insert.Parameters.AddWithValue("@account_name", account);
    insert.Parameters.AddWithValue("@hash", BCrypt.Net.BCrypt.HashPassword(password));
    await insert.ExecuteNonQueryAsync();
}

static async Task CreateAsync(MariaDbWorldCharacterStore store, string account, int slot, string name, byte[] template)
{
    Require(await store.IsCharacterSlotEmptyAsync(account, slot) != false, $"Slot {slot} unexpectedly occupied.");
    Require(await store.TryReserveCharacterNameAsync(name, account), "Name reservation failed.");
    Require(await store.TryWriteNewCharacterAsync(account, slot, name, template), $"Create in slot {slot} failed.");
}

static async Task CheckpointCompositeRoundTripAndRollbackAsync(
    MySqlConnection connection,
    MariaDbWorldCharacterStore store,
    string accountName,
    string characterName,
    byte[] template)
{
    const int slot = 2;
    const int dailyQuestOffset = 7_872;
    const int dailyQuestLength = 56;
    await CreateAsync(store, accountName, slot, characterName, template);

    var before = await ReadAccountBlobAsync(connection, accountName);
    var characterOffset = LegacyAccountSnapshot.CharactersOffset + (slot * LegacyAccountSnapshot.CharacterStride);
    var seeded = before.ToArray();
    new byte[] { 0x21, 0x32, 0x43, 0x54 }.CopyTo(seeded, characterOffset + LegacyAccountSnapshot.MobSkillBarOffset);
    for (var index = 0; index < LegacyAccountSnapshot.ShortSkillStride; index++)
        seeded[LegacyAccountSnapshot.ShortSkillOffset + (slot * LegacyAccountSnapshot.ShortSkillStride) + index] = (byte)(0x60 + index);
    for (var index = 0; index < LegacyAccountSnapshot.CargoCount * LegacyItem.SizeInBytes; index++)
        seeded[LegacyAccountSnapshot.CargoOffset + index] = (byte)(0x70 + (index % 31));
    BinaryPrimitives.WriteInt32LittleEndian(seeded.AsSpan(LegacyAccountSnapshot.DonateOffset), 0x12345678);
    BinaryPrimitives.WriteInt32LittleEndian(seeded.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), 987_654);
    for (var index = 0; index < dailyQuestLength; index++)
        seeded[dailyQuestOffset + index] = (byte)(0x40 + (index % 31));
    using (var update = connection.CreateCommand())
    {
        update.CommandText = "UPDATE wyd_world_account_state SET account_blob = @blob WHERE world_key = 'UP' AND account_name = @account_name";
        update.Parameters.AddWithValue("@blob", seeded);
        update.Parameters.AddWithValue("@account_name", accountName);
        await update.ExecuteNonQueryAsync();
    }

    var mob = seeded.AsSpan(characterOffset, LegacyAccountSnapshot.CharacterStride).ToArray();
    mob.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, sizeof(uint)).Fill(0xE1);
    BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), 0x76543210);
    var mobExtra = Enumerable.Range(0, LegacyAccountSnapshot.MobExtraStride).Select(static index => (byte)(index * 3 + 1)).ToArray();
    var affect = Enumerable.Range(0, LegacyAccountSnapshot.AffectStride).Select(static index => (byte)(index * 5 + 2)).ToArray();
    var equipment = Enumerable.Range(0, ClientEquipmentStateV769.EquipmentCount)
        .Select(static index => new LegacyItem((short)(2_000 + index), (byte)(index + 1), (byte)(index + 2), (byte)(index + 3), (byte)(index + 4), (byte)(index + 5), (byte)(index + 6)))
        .ToArray();
    var request = new LegacyCharacterCheckpointSaveRequest(accountName, slot, mob, 505, -77, mobExtra, affect, equipment);

    var expected = seeded.ToArray();
    var savedMob = mob.ToArray();
    seeded.AsSpan(characterOffset + LegacyAccountSnapshot.MobSkillBarOffset, sizeof(uint))
        .CopyTo(savedMob.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, sizeof(uint)));
    BinaryPrimitives.WriteInt16LittleEndian(savedMob.AsSpan(LegacyAccountSnapshot.MobSavedPositionXOffset), request.PositionX);
    BinaryPrimitives.WriteInt16LittleEndian(savedMob.AsSpan(LegacyAccountSnapshot.MobSavedPositionYOffset), request.PositionY);
    savedMob.CopyTo(expected, characterOffset);
    mobExtra.CopyTo(expected.AsMemory(LegacyAccountSnapshot.MobExtraOffset + (slot * LegacyAccountSnapshot.MobExtraStride)));
    affect.CopyTo(expected.AsMemory(LegacyAccountSnapshot.AffectOffset + (slot * LegacyAccountSnapshot.AffectStride)));

    Require(await store.TrySaveCharacterCheckpointAsync(request) == LegacyCharacterCheckpointSaveResult.Success,
        "MariaDB composite checkpoint did not commit.");
    var after = await ReadAccountBlobAsync(connection, accountName);
    Require(after.SequenceEqual(expected), "MariaDB checkpoint changed independently owned account fields or missed live character state.");
    Require(BinaryPrimitives.ReadInt32LittleEndian(after.AsSpan(LegacyAccountSnapshot.AccountCoinOffset)) == 987_654,
        "MariaDB checkpoint replaced the account bank balance with the character's carried Gold.");
    Require((await store.ReadClientEquipmentAsync(accountName, slot))?.SequenceEqual(equipment) == true,
        "MariaDB checkpoint did not persist the matching 18-slot equipment sidecar.");

    var beforeFailure = after.ToArray();
    var changedMob = mob.ToArray();
    BinaryPrimitives.WriteInt32LittleEndian(changedMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), 0x01020304);
    var changedEquipment = equipment.ToArray();
    changedEquipment[17] = new LegacyItem(3_100, 1, 2, 3, 4, 5, 6);
    var failingRequest = request with { Mob = changedMob, ClientEquipment = changedEquipment };
    var trigger = "checkpoint_fail_" + Guid.NewGuid().ToString("N");
    await ExecuteAsync(connection, $"CREATE TRIGGER `{trigger}` BEFORE INSERT ON wyd_client_equipment_769 FOR EACH ROW BEGIN IF NEW.account_name = '{accountName}' AND NEW.world_key = 'UP' AND NEW.character_slot = {slot} THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'checkpoint sidecar failure'; END IF; END");
    try
    {
        var failure = await store.TrySaveCharacterCheckpointAsync(failingRequest);
        Require(failure == LegacyCharacterCheckpointSaveResult.NotAvailable,
            $"Injected equipment-sidecar failure returned {failure} instead of NotAvailable.");
        Require((await ReadAccountBlobAsync(connection, accountName)).SequenceEqual(beforeFailure),
            "The MariaDB transaction left the account blob changed after sidecar failure.");
        Require((await store.ReadClientEquipmentAsync(accountName, slot))?.SequenceEqual(equipment) == true,
            "The MariaDB transaction changed equipment after the injected sidecar failure.");
    }
    finally
    {
        await ExecuteAsync(connection, $"DROP TRIGGER IF EXISTS `{trigger}`");
    }
}

static async Task CargoSaleCasRoundTripConflictConcurrencyAndRollbackAsync(
    MySqlConnection connection,
    MariaDbWorldCharacterStore store,
    string accountName)
{
    const int slot = 2;
    var before = await ReadAccountBlobAsync(connection, accountName);
    var cargoOffset = LegacyAccountSnapshot.CargoOffset;
    var cargoLength = LegacyAccountSnapshot.CargoCount * LegacyItem.SizeInBytes;
    var cargoBefore = before.AsSpan(cargoOffset, cargoLength).ToArray();
    var coinOffset = LegacyAccountSnapshot.CharactersOffset + slot * LegacyAccountSnapshot.CharacterStride + LegacyAccountSnapshot.MobCoinOffset;
    var coinBefore = BinaryPrimitives.ReadInt32LittleEndian(before.AsSpan(coinOffset));
    var cargoAfter = MutateCargo(cargoBefore, 0x31);
    var request = new LegacyNpcCargoSaleCommitRequest(accountName, slot, cargoBefore, cargoAfter, coinBefore, checked(coinBefore + 7));

    Require(await store.TryCommitNpcCargoSaleAsync(request) == LegacyNpcCargoSaleCommitResult.Success,
        "MariaDB NPC Cargo CAS did not commit.");
    var expectedAfter = before.ToArray();
    cargoAfter.CopyTo(expectedAfter, cargoOffset);
    BinaryPrimitives.WriteInt32LittleEndian(expectedAfter.AsSpan(coinOffset), request.NewMobCoin);
    Require((await ReadAccountBlobAsync(connection, accountName)).SequenceEqual(expectedAfter),
        "MariaDB Cargo CAS missed its after image or changed unrelated account bytes.");
    Require(BinaryPrimitives.ReadInt32LittleEndian(expectedAfter.AsSpan(LegacyAccountSnapshot.AccountCoinOffset)) == 987_654,
        "MariaDB Cargo CAS changed the account bank Coin.");

    Require(await store.TryCommitNpcCargoSaleAsync(request) == LegacyNpcCargoSaleCommitResult.Conflict,
        "Stale MariaDB Cargo CAS did not return Conflict.");
    Require((await ReadAccountBlobAsync(connection, accountName)).SequenceEqual(expectedAfter),
        "Stale MariaDB Cargo CAS changed account bytes.");

    var concurrentBefore = await ReadAccountBlobAsync(connection, accountName);
    var concurrentCargo = concurrentBefore.AsSpan(cargoOffset, cargoLength).ToArray();
    var concurrentCoin = BinaryPrimitives.ReadInt32LittleEndian(concurrentBefore.AsSpan(coinOffset));
    var firstCargo = MutateCargo(concurrentCargo, 0x12);
    var secondCargo = MutateCargo(concurrentCargo, 0x24);
    var first = new LegacyNpcCargoSaleCommitRequest(accountName, slot, concurrentCargo, firstCargo, concurrentCoin, checked(concurrentCoin + 1));
    var second = new LegacyNpcCargoSaleCommitRequest(accountName, slot, concurrentCargo, secondCargo, concurrentCoin, checked(concurrentCoin + 2));
    var results = await Task.WhenAll(
        store.TryCommitNpcCargoSaleAsync(first).AsTask(),
        store.TryCommitNpcCargoSaleAsync(second).AsTask());
    Require(results.Count(result => result == LegacyNpcCargoSaleCommitResult.Success) == 1 &&
            results.Count(result => result == LegacyNpcCargoSaleCommitResult.Conflict) == 1,
        $"Concurrent MariaDB Cargo CAS did not produce exactly one commit and one conflict (results: {string.Join(", ", results)}).");

    var winnerIndex = results[0] == LegacyNpcCargoSaleCommitResult.Success ? 0 : 1;
    var winningCargo = winnerIndex == 0 ? firstCargo : secondCargo;
    var winningCoin = winnerIndex == 0 ? first.NewMobCoin : second.NewMobCoin;
    var expectedConcurrentAfter = concurrentBefore.ToArray();
    winningCargo.CopyTo(expectedConcurrentAfter, cargoOffset);
    BinaryPrimitives.WriteInt32LittleEndian(expectedConcurrentAfter.AsSpan(coinOffset), winningCoin);
    Require((await ReadAccountBlobAsync(connection, accountName)).SequenceEqual(expectedConcurrentAfter),
        "Concurrent MariaDB Cargo CAS left an image other than the single winner.");

    var rollbackBefore = await ReadAccountBlobAsync(connection, accountName);
    var rollbackCargo = rollbackBefore.AsSpan(cargoOffset, cargoLength).ToArray();
    var rollbackCoin = BinaryPrimitives.ReadInt32LittleEndian(rollbackBefore.AsSpan(coinOffset));
    var rollbackRequest = new LegacyNpcCargoSaleCommitRequest(
        accountName, slot, rollbackCargo, MutateCargo(rollbackCargo, 0x45), rollbackCoin, checked(rollbackCoin + 3));
    var trigger = "cargo_fail_" + Guid.NewGuid().ToString("N");
    await ExecuteAsync(connection, $"CREATE TRIGGER `{trigger}` BEFORE UPDATE ON wyd_world_account_state FOR EACH ROW BEGIN IF OLD.account_name = '{accountName}' AND OLD.world_key = 'UP' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'isolated Cargo CAS fault'; END IF; END");
    try
    {
        Require(await store.TryCommitNpcCargoSaleAsync(rollbackRequest) == LegacyNpcCargoSaleCommitResult.Unavailable,
            "Injected MariaDB Cargo CAS failure did not return Unavailable.");
        Require((await ReadAccountBlobAsync(connection, accountName)).SequenceEqual(rollbackBefore),
            "Failed MariaDB Cargo CAS left the account blob changed.");
    }
    finally
    {
        await ExecuteAsync(connection, $"DROP TRIGGER IF EXISTS `{trigger}`");
    }
}

static async Task GuildInvitePairRoundTripConflictAndRollbackAsync(
    MySqlConnection connection,
    MariaDbWorldCharacterStore store,
    string sourceAccount,
    string targetAccount,
    string sourceCharacter,
    string targetCharacter,
    string sameAccountCharacter,
    string password,
    byte[] template)
{
    const int sourceSlot = 3;
    const int targetSlot = 0;
    const int guildId = 100;
    const int sourceCoinBefore = 1_000;
    const int sourceCoinAfter = 900;
    const int targetCoin = 2_000;

    Require(string.CompareOrdinal(sourceAccount, targetAccount) < 0,
        "The integration fixture must order source before target for the injected second-row failure.");
    await InsertAccountAsync(connection, targetAccount, password);
    await CreateAsync(store, sourceAccount, sourceSlot, sourceCharacter, template);
    await CreateAsync(store, targetAccount, targetSlot, targetCharacter, template);

    var sourceSeed = await ReadAccountBlobAsync(connection, sourceAccount);
    var targetSeed = await ReadAccountBlobAsync(connection, targetAccount);
    WriteGuildAndCoin(sourceSeed, sourceSlot, guildId, sourceCoinBefore);
    WriteGuildAndCoin(targetSeed, targetSlot, 0, targetCoin);
    await WriteAccountBlobAsync(connection, sourceAccount, sourceSeed);
    await WriteAccountBlobAsync(connection, targetAccount, targetSeed);

    var successRequest = CreateGuildInviteRequest(sourceAccount, targetAccount, sourceSlot, targetSlot,
        guildId, sourceCoinBefore, sourceCoinAfter, targetCoin);
    Require(await store.TrySaveGuildInvitePairAsync(successRequest) == GuildInvitePairSaveResult.Success,
        "MariaDB guild invite pair did not commit.");

    var sourceAfter = sourceSeed.ToArray();
    WriteGuildAndCoin(sourceAfter, sourceSlot, guildId, sourceCoinAfter);
    var targetAfter = targetSeed.ToArray();
    WriteGuildAndCoin(targetAfter, targetSlot, guildId, targetCoin);
    Require((await ReadAccountBlobAsync(connection, sourceAccount)).SequenceEqual(sourceAfter),
        "MariaDB guild invite did not persist the source character change exactly.");
    Require((await ReadAccountBlobAsync(connection, targetAccount)).SequenceEqual(targetAfter),
        "MariaDB guild invite did not persist the target character change exactly.");

    var staleRequest = CreateGuildInviteRequest(sourceAccount, targetAccount, sourceSlot, targetSlot,
        guildId, sourceCoinAfter, sourceCoinAfter - 1, targetCoin);
    Require(await store.TrySaveGuildInvitePairAsync(staleRequest) == GuildInvitePairSaveResult.Conflict,
        "Stale MariaDB guild invite pair did not return Conflict.");
    Require((await ReadAccountBlobAsync(connection, sourceAccount)).SequenceEqual(sourceAfter) &&
            (await ReadAccountBlobAsync(connection, targetAccount)).SequenceEqual(targetAfter),
        "Conflicting MariaDB guild invite changed one side of the pair.");

    var rollbackTarget = targetAfter.ToArray();
    WriteGuildAndCoin(rollbackTarget, targetSlot, 0, targetCoin);
    await WriteAccountBlobAsync(connection, targetAccount, rollbackTarget);
    var rollbackSourceBefore = await ReadAccountBlobAsync(connection, sourceAccount);
    var rollbackTargetBefore = await ReadAccountBlobAsync(connection, targetAccount);
    var rollbackRequest = CreateGuildInviteRequest(sourceAccount, targetAccount, sourceSlot, targetSlot,
        guildId, sourceCoinAfter, sourceCoinAfter - 1, targetCoin);
    var trigger = "guild_pair_fail_" + Guid.NewGuid().ToString("N");
    await ExecuteAsync(connection, $"CREATE TRIGGER `{trigger}` BEFORE UPDATE ON wyd_world_account_state FOR EACH ROW BEGIN IF OLD.account_name = '{targetAccount}' AND OLD.world_key = 'UP' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'isolated guild invite pair fault'; END IF; END");
    try
    {
        Require(await store.TrySaveGuildInvitePairAsync(rollbackRequest) == GuildInvitePairSaveResult.Unavailable,
            "Injected second-account guild invite failure did not return Unavailable.");
        Require((await ReadAccountBlobAsync(connection, sourceAccount)).SequenceEqual(rollbackSourceBefore) &&
                (await ReadAccountBlobAsync(connection, targetAccount)).SequenceEqual(rollbackTargetBefore),
            "Failed MariaDB guild invite did not roll both account blobs back.");
    }
    finally
    {
        await ExecuteAsync(connection, $"DROP TRIGGER IF EXISTS `{trigger}`");
    }

    // The same-account path reads and writes one row while updating two character slots.
    await CreateAsync(store, sourceAccount, 0, sameAccountCharacter, template);
    var sameAccountBefore = await ReadAccountBlobAsync(connection, sourceAccount);
    WriteGuildAndCoin(sameAccountBefore, 0, 0, 500);
    await WriteAccountBlobAsync(connection, sourceAccount, sameAccountBefore);
    var sameAccountRequest = CreateGuildInviteRequest(
        sourceAccount, sourceAccount, sourceSlot, 0, guildId, sourceCoinAfter, sourceCoinAfter - 1, 500);
    Require(await store.TrySaveGuildInvitePairAsync(sameAccountRequest) == GuildInvitePairSaveResult.Success,
        "MariaDB same-account guild invite pair did not commit.");
    var sameAccountAfter = sameAccountBefore.ToArray();
    WriteGuildAndCoin(sameAccountAfter, sourceSlot, guildId, sourceCoinAfter - 1);
    WriteGuildAndCoin(sameAccountAfter, 0, guildId, 500);
    Require((await ReadAccountBlobAsync(connection, sourceAccount)).SequenceEqual(sameAccountAfter),
        "MariaDB same-account guild invite did not update both slots in one exact blob image.");
}

static GuildInvitePairSaveRequest CreateGuildInviteRequest(
    string sourceAccount,
    string targetAccount,
    int sourceSlot,
    int targetSlot,
    int guildId,
    int sourceExpectedCoin,
    int sourceNewCoin,
    int targetCoin) => new(
        new GuildInviteCharacterUpdate(sourceAccount, sourceSlot, guildId, sourceExpectedCoin, guildId, sourceNewCoin),
        new GuildInviteCharacterUpdate(targetAccount, targetSlot, 0, targetCoin, guildId, targetCoin));

static void WriteGuildAndCoin(byte[] blob, int slot, int guildId, int coin)
{
    var offset = LegacyAccountSnapshot.CharactersOffset + slot * LegacyAccountSnapshot.CharacterStride;
    BinaryPrimitives.WriteUInt16LittleEndian(blob.AsSpan(offset + LegacyAccountSnapshot.MobGuildOffset), checked((ushort)guildId));
    BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(offset + LegacyAccountSnapshot.MobCoinOffset), coin);
}

static byte[] MutateCargo(byte[] cargo, byte mask)
{
    var changed = cargo.ToArray();
    changed[0] ^= mask;
    changed[^1] ^= unchecked((byte)(mask + 1));
    return changed;
}

static async Task WriteAccountBlobAsync(MySqlConnection connection, string accountName, byte[] blob)
{
    using var command = connection.CreateCommand();
    command.CommandText = "UPDATE wyd_world_account_state SET account_blob = @blob WHERE world_key = 'UP' AND account_name = @account_name";
    command.Parameters.AddWithValue("@blob", blob);
    command.Parameters.AddWithValue("@account_name", accountName);
    Require(await command.ExecuteNonQueryAsync() == 1, "Isolated MariaDB account blob seed was not updated.");
}

static async Task<byte[]> ReadAccountBlobAsync(MySqlConnection connection, string accountName)
{
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT account_blob FROM wyd_world_account_state WHERE world_key = 'UP' AND account_name = @account_name LIMIT 1";
    command.Parameters.AddWithValue("@account_name", accountName);
    var value = await command.ExecuteScalarAsync();
    return value as byte[] ?? throw new InvalidOperationException("Isolated MariaDB account blob is missing or not binary.");
}

static async Task ExecuteAsync(MySqlConnection connection, string sql)
{
    using var command = connection.CreateCommand();
    command.CommandText = sql;
    await command.ExecuteNonQueryAsync();
}

static string NewName(string prefix) => (prefix + Guid.NewGuid().ToString("N")[..12]).ToUpperInvariant();

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

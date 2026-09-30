using MySqlConnector;
using WydCdk.Protocol;
using WydCdk.World;

const string database = "wyd_cdk_autotrade_close_test";
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
    using (var command = connection.CreateCommand())
    {
        command.CommandText = "CREATE TABLE IF NOT EXISTS wyd_autotrade_state (world_key VARCHAR(3) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, account_name VARCHAR(15) CHARACTER SET ascii COLLATE ascii_bin NOT NULL, character_slot TINYINT UNSIGNED NOT NULL, listing_json LONGTEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL, created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6), updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6), PRIMARY KEY(world_key, account_name, character_slot), KEY ix_wyd_autotrade_state_world (world_key, account_name), CONSTRAINT ck_wyd_autotrade_state_world CHECK (world_key IN ('UP', 'PVP')), CONSTRAINT ck_wyd_autotrade_state_slot CHECK (character_slot < 4), CONSTRAINT ck_wyd_autotrade_state_json CHECK (JSON_VALID(listing_json))) ENGINE=InnoDB";
        await command.ExecuteNonQueryAsync();
        // Repair only the isolated test table from the first harness run.
        command.CommandText = "ALTER TABLE wyd_autotrade_state ADD COLUMN IF NOT EXISTS created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6), ADD COLUMN IF NOT EXISTS updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)";
        await command.ExecuteNonQueryAsync();
    }
    var store = new MariaDbAutoTradeStateStore(() => new MySqlConnection(builder.ConnectionString), "UP");
    // Test-only owner names, never an unrestricted DELETE or cleanup of another run.
    var names = new[] { "C" + Guid.NewGuid().ToString("N")[..12], "C" + Guid.NewGuid().ToString("N")[..12] };
    try
    {
        var items = new LegacyItem[AutoTradeListConfirmation.SlotCount]; items[0] = new LegacyItem(900, 1, 2, 3, 4, 5, 6);
        var positions = Enumerable.Repeat((sbyte)-1, items.Length).ToArray(); positions[0] = 4;
        var prices = new int[items.Length]; prices[0] = 100;
        var snapshot = new LegacyAutoTradeSnapshot(12, 2000, 2001, "TEST", items, positions, prices, 12);
        foreach (var name in names)
            Require(await store.SaveAsync(name, 0, snapshot) == LegacyAutoTradeStateResult.Saved, "Seed failed.");
        var one = (await store.ReadAsync(names[0], 0))!;
        var two = (await store.ReadAsync(names[1], 0))!;
        var request = new LegacyAutoTradeCloseRequest([two, one]);
        Require(await store.ReadCloseStateAsync(request) == LegacyAutoTradeCloseReadState.AllUnchanged, "Initial state mismatch.");
        var stalePrices = two.Prices.ToArray(); stalePrices[0] = 101;
        Require(await store.RemoveExpectedAsync(new([one, two with { Prices = stalePrices }])) == LegacyAutoTradeCloseStoreResult.Conflict,
            "Batch conflict did not reject.");
        Require(await store.ReadCloseStateAsync(request) == LegacyAutoTradeCloseReadState.AllUnchanged, "Conflict partially deleted rows.");
        var trigger = "close_fail_" + Guid.NewGuid().ToString("N");
        var failOwner = request.ExpectedListings[1].AccountName;
        using (var command = connection.CreateCommand())
        {
            // Fault belongs only to this run's second sorted owner. The first DELETE
            // executes before this server-side failure, exercising real rollback.
            command.CommandText = $"CREATE TRIGGER `{trigger}` BEFORE DELETE ON wyd_autotrade_state FOR EACH ROW BEGIN IF OLD.account_name = '{failOwner}' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'isolated close test fault'; END IF; END";
            await command.ExecuteNonQueryAsync();
        }
        try
        {
            Require(await store.RemoveExpectedAsync(request) == LegacyAutoTradeCloseStoreResult.Unavailable,
                "Mid-batch delete failure was not rejected.");
            Require(await store.ReadCloseStateAsync(request) == LegacyAutoTradeCloseReadState.AllUnchanged,
                "Mid-batch failure committed the first DELETE.");
        }
        finally
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP TRIGGER IF EXISTS `{trigger}`";
            await command.ExecuteNonQueryAsync();
        }
        Require(await store.RemoveExpectedAsync(request) == LegacyAutoTradeCloseStoreResult.Removed, "Batch delete failed.");
        Require(await store.ReadCloseStateAsync(request) == LegacyAutoTradeCloseReadState.AllAbsent, "Commit not visible on new connection.");
        Require(await store.RemoveExpectedAsync(request) == LegacyAutoTradeCloseStoreResult.AlreadyAbsent, "Delete not idempotent.");
        await store.SaveAsync(names[0], 0, snapshot);
        Require(await store.ReadCloseStateAsync(request) == LegacyAutoTradeCloseReadState.Conflict, "Mixed readback not conflict.");
        Require(await store.RemoveExpectedAsync(request) == LegacyAutoTradeCloseStoreResult.Removed, "Partly absent batch failed.");
        Console.WriteLine("PASS MariaDB conditional batch close, mismatch preservation, mid-batch rollback, new-connection readback and retry.");
    }
    finally
    {
        foreach (var name in names) await store.RemoveAsync(name, 0);
    }
    return 0;
}
catch (Exception error)
{
    // Only classifications: provider exception messages may contain host/user/connection details.
    Console.Error.WriteLine(error is MySqlException sql
        ? $"FAIL local MariaDB integration: provider code {sql.Number}."
        : $"FAIL local MariaDB integration: {error.GetType().Name}.");
    return 1;
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

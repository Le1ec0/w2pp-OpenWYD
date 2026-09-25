using System.Buffers.Binary;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using WydCdk.Protocol;
using WydCdk.World;

if (args.Length is < 1 or > 12)
    throw new ArgumentException("Usage: MariaDbMigrations <connection-config.json> [migration-directory] [password-file] or [username] [password-file] [--reset-server-schema] [--purchase-smoke] [--inspect-user <name>] [--inspect-table <name>] [--inspect-grants <name> <host>]");

var configPath = Path.GetFullPath(args[0]);
var migrationDirectory = Path.GetFullPath(args.Length == 2
    ? args[1]
    : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "db", "migrations"));

if (!File.Exists(configPath))
    throw new FileNotFoundException("MariaDB connection configuration was not found.", configPath);
if (!Directory.Exists(migrationDirectory))
    throw new DirectoryNotFoundException($"Migration directory was not found: {migrationDirectory}");
var optionalArgumentStart = args.Length >= 4 ? 4 : args.Length == 3 ? 3 : 2;
var resetServerSchema = false;
var purchaseSmoke = false;
string? inspectUser = null;
string? inspectTable = null;
string? inspectGrantsUser = null;
string? inspectGrantsHost = null;
for (var index = optionalArgumentStart; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--reset-server-schema":
            resetServerSchema = true;
            break;
        case "--purchase-smoke":
            purchaseSmoke = true;
            break;
        case "--inspect-user" when index + 1 < args.Length:
            inspectUser = args[++index];
            break;
        case "--inspect-table" when index + 1 < args.Length:
            inspectTable = args[++index];
            break;
        case "--inspect-grants" when index + 2 < args.Length:
            inspectGrantsUser = args[++index];
            inspectGrantsHost = args[++index];
            break;
        default:
            throw new ArgumentException("The optional switches are --reset-server-schema, --purchase-smoke, --inspect-user <name>, --inspect-table <name> and --inspect-grants <name> <host>.");
    }
}

var options = JsonSerializer.Deserialize<MariaDbConnectionOptions>(
    await File.ReadAllTextAsync(configPath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidDataException("MariaDB connection configuration is empty.");
var passwordPath = args.Length >= 4 ? args[3] : args.Length == 3 ? args[2] : null;
if (passwordPath is not null)
{
    passwordPath = Path.GetFullPath(passwordPath);
    if (!File.Exists(passwordPath))
        throw new FileNotFoundException("MariaDB password file was not found.", passwordPath);

    options = new MariaDbConnectionOptions
    {
        Provider = options.Provider,
        Host = options.Host,
        Port = options.Port,
        Database = options.Database,
        Username = args.Length >= 4 ? args[2] : options.Username,
        Password = (await File.ReadAllTextAsync(passwordPath)).Trim(),
        SslMode = options.SslMode,
    };
}

var migrationFiles = Directory.GetFiles(migrationDirectory, "*.sql")
    .Where(static path =>
    {
        var fileName = Path.GetFileName(path);
        return fileName.Length >= 3 && int.TryParse(fileName.AsSpan(0, 3), out _);
    })
    .OrderBy(static path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
    .ToArray();
if (migrationFiles.Length == 0)
    throw new InvalidOperationException("No numbered SQL migrations were found.");

var connectionFactory = MariaDbConnectionFactory.FromOptions(options);
await using var connection = connectionFactory();
await connection.OpenAsync();

if (resetServerSchema)
{
    const string resetSql = "DROP TABLE IF EXISTS wyd_client_equipment_769, wyd_autotrade_state, wyd_account_security, wyd_character_names, wyd_world_account_state, wyd_schema_migrations";
    await using var resetCommand = connection.CreateCommand();
    resetCommand.CommandText = resetSql;
    await resetCommand.ExecuteNonQueryAsync();
    Console.WriteLine($"RESET_SERVER_SCHEMA=6 DATABASE={options.Database}");
}

foreach (var migrationFile in migrationFiles)
{
    foreach (var statement in SplitStatements(await File.ReadAllTextAsync(migrationFile)))
    {
        await using var command = connection.CreateCommand();
        command.CommandText = statement;
        await command.ExecuteNonQueryAsync();
    }

    Console.WriteLine($"APPLIED {Path.GetFileName(migrationFile)}");
}

await using (var verificationConnection = connectionFactory())
{
    await verificationConnection.OpenAsync();
    await using var tableCommand = verificationConnection.CreateCommand();
    tableCommand.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name IN ('wyd_schema_migrations', 'wyd_world_account_state', 'wyd_character_names', 'wyd_account_security', 'wyd_autotrade_state', 'wyd_client_equipment_769')";
    var tableCount = Convert.ToInt32(await tableCommand.ExecuteScalarAsync());
    if (tableCount != 6)
        throw new InvalidOperationException($"Expected six WYD server tables after migration, found {tableCount}.");
    Console.WriteLine("SERVER_TABLES=6");

    if (inspectUser is not null)
    {
        await using var userCommand = verificationConnection.CreateCommand();
        userCommand.CommandText = "SELECT COUNT(*) FROM mysql.user WHERE User = @user";
        var parameter = userCommand.CreateParameter();
        parameter.ParameterName = "@user";
        parameter.Value = inspectUser;
        userCommand.Parameters.Add(parameter);
        Console.WriteLine($"DB_USER_MATCHES={Convert.ToInt32(await userCommand.ExecuteScalarAsync())}");
    }

    if (inspectTable is not null)
    {
        await using var columnCommand = verificationConnection.CreateCommand();
        columnCommand.CommandText = "SELECT COLUMN_NAME, IS_NULLABLE, DATA_TYPE, COLUMN_TYPE FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = @table ORDER BY ORDINAL_POSITION";
        var parameter = columnCommand.CreateParameter();
        parameter.ParameterName = "@table";
        parameter.Value = inspectTable;
        columnCommand.Parameters.Add(parameter);
        await using var reader = await columnCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            Console.WriteLine($"COLUMN {reader.GetString(0)} NULLABLE={reader.GetString(1)} TYPE={reader.GetString(2)}({reader.GetString(3)})");
    }

    if (inspectGrantsUser is not null && inspectGrantsHost is not null)
    {
        await using var grantsCommand = verificationConnection.CreateCommand();
        grantsCommand.CommandText = "SELECT Host, plugin, Select_priv, Insert_priv, Update_priv, Delete_priv, Create_priv, Alter_priv FROM mysql.user WHERE User = @user AND Host = @host";
        var userParameter = grantsCommand.CreateParameter();
        userParameter.ParameterName = "@user";
        userParameter.Value = inspectGrantsUser;
        grantsCommand.Parameters.Add(userParameter);
        var hostParameter = grantsCommand.CreateParameter();
        hostParameter.ParameterName = "@host";
        hostParameter.Value = inspectGrantsHost;
        grantsCommand.Parameters.Add(hostParameter);
        await using var grantsReader = await grantsCommand.ExecuteReaderAsync();
        while (await grantsReader.ReadAsync())
            Console.WriteLine($"DB_USER_AUTH HOST={grantsReader.GetString(0)} PLUGIN={grantsReader.GetString(1)} GLOBAL_SELECT={grantsReader.GetString(2)} GLOBAL_INSERT={grantsReader.GetString(3)} GLOBAL_UPDATE={grantsReader.GetString(4)} GLOBAL_DELETE={grantsReader.GetString(5)} GLOBAL_CREATE={grantsReader.GetString(6)} GLOBAL_ALTER={grantsReader.GetString(7)}");
    }
}

var stateStore = new MariaDbAutoTradeStateStore(connectionFactory, "UP");
var verificationAccount = "MIGRATIONCHECK";
var verificationSlot = 3;
var verificationSnapshot = new LegacyAutoTradeSnapshot(
    9001,
    2112,
    2042,
    "MIGRATION CHECK",
    new LegacyItem[AutoTradeListConfirmation.SlotCount],
    Enumerable.Repeat((sbyte)-1, AutoTradeListConfirmation.SlotCount).ToArray(),
    new int[AutoTradeListConfirmation.SlotCount],
    5);
try
{
    var saveResult = await stateStore.SaveAsync(verificationAccount, verificationSlot, verificationSnapshot);
    var loaded = await stateStore.ReadAsync(verificationAccount, verificationSlot);
    if (saveResult != LegacyAutoTradeStateResult.Saved || loaded is null ||
        loaded.AccountName != verificationAccount || loaded.CharacterSlot != verificationSlot ||
        loaded.Title != verificationSnapshot.Title || loaded.PositionX != verificationSnapshot.PositionX ||
        loaded.PositionY != verificationSnapshot.PositionY)
        throw new InvalidOperationException("MariaDB autotrade state did not round-trip the verification listing.");
    Console.WriteLine("AUTOTRADE_ROUNDTRIP=PASS");
}
finally
{
    await stateStore.RemoveAsync(verificationAccount, verificationSlot);
}

if (purchaseSmoke)
    await RunPurchaseSmokeAsync(connectionFactory);

static async Task RunPurchaseSmokeAsync(Func<DbConnection> connectionFactory)
{
    const string buyerAccount = "MIGBUYER";
    const string sellerAccount = "MIGSELLER";
    const string worldKey = "UP";
    const int sellerCharacterSlot = 1;
    const int sellerCargoPosition = 4;
    var item = new LegacyItem(900, 1, 2, 3, 4, 5, 6);
    var listingItems = Enumerable.Repeat(default(LegacyItem), AutoTradeListConfirmation.SlotCount).ToArray();
    var listingCarry = Enumerable.Repeat((sbyte)-1, AutoTradeListConfirmation.SlotCount).ToArray();
    var listingPrices = new int[AutoTradeListConfirmation.SlotCount];
    listingItems[0] = item;
    listingCarry[0] = (sbyte)sellerCargoPosition;
    listingPrices[0] = 100;
    var listing = new LegacyAutoTradeSnapshot(9001, 2100, 2100, "MIGRATION SHOP", listingItems, listingCarry, listingPrices, 12);
    var sellerCargo = Enumerable.Repeat(default(LegacyItem), LegacyAutoTradeBook.CargoSlotCount).ToArray();
    sellerCargo[sellerCargoPosition] = item;
    var buyerCarry = Enumerable.Repeat(default(LegacyItem), LegacyAccountSnapshot.MobCarryCount).ToArray();
    var context = new LegacyAutoTradePurchaseContext(
        RequesterHp: 1,
        RequesterInPlay: true,
        LiveTradeActive: false,
        LiveTradeOpponentId: 0,
        TargetId: 9001,
        TargetExists: true,
        TargetInPlay: false,
        TargetOfflineAutoTrade: true,
        TargetAutoTradeActive: true,
        TargetWithinView: true,
        Position: 0,
        RequestedPrice: 100,
        RequestedTax: 12,
        RequestedItem: item,
        Listing: listing,
        SellerCargo: sellerCargo,
        BuyerCarry: buyerCarry,
        BuyerCoin: 150,
        SellerCoin: 10,
        TargetVillage: 0);

    try
    {
        var buyerBlob = CreateSmokeAccountBlob((byte)'B', slot: 0, coin: 150);
        var sellerBlob = CreateSmokeAccountBlob((byte)'S', slot: sellerCharacterSlot, coin: 10);
        item.Write(sellerBlob.AsSpan(
            LegacyAccountSnapshot.CargoOffset + (sellerCargoPosition * LegacyItem.SizeInBytes),
            LegacyItem.SizeInBytes));
        await UpsertSmokeBlobAsync(connectionFactory, buyerAccount, worldKey, buyerBlob);
        await UpsertSmokeBlobAsync(connectionFactory, sellerAccount, worldKey, sellerBlob);

        var stateStore = new MariaDbAutoTradeStateStore(connectionFactory, worldKey);
        if (await stateStore.SaveAsync(sellerAccount, sellerCharacterSlot, listing) != LegacyAutoTradeStateResult.Saved)
            throw new InvalidOperationException("MariaDB purchase smoke could not save its listing fixture.");
        var planResult = LegacyAutoTradePurchasePlanBuilder.TryBuild(context, out var plan);
        if (planResult != LegacyAutoTradePurchaseResult.Accepted || plan is null)
            throw new InvalidOperationException("MariaDB purchase smoke could not build its pure purchase plan.");

        var expectedBuyerMob = buyerBlob.AsSpan(
            LegacyAccountSnapshot.CharactersOffset,
            LegacyAccountSnapshot.CharacterStride).ToArray();
        var buyerMob = expectedBuyerMob.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(buyerMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), plan.BuyerCoin);
        item.Write(buyerMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes));
        var expectedListing = new LegacyAutoTradePersistedListing(
            sellerAccount, sellerCharacterSlot, listing.PositionX, listing.PositionY,
            listing.Title, listing.Items, listing.CarryPositions, listing.Prices, listing.Tax);
        var updatedListing = new LegacyAutoTradePersistedListing(
            sellerAccount, sellerCharacterSlot, plan.UpdatedListing.PositionX, plan.UpdatedListing.PositionY,
            plan.UpdatedListing.Title, plan.UpdatedListing.Items, plan.UpdatedListing.CarryPositions,
            plan.UpdatedListing.Prices, plan.UpdatedListing.Tax);
        var request = new LegacyAutoTradePurchaseCommitRequest(
            buyerAccount, 0, expectedBuyerMob, expectedBuyerMob.ToArray(), buyerMob, 2100, 2100,
            new byte[LegacyAccountSnapshot.MobExtraStride], sellerAccount, sellerCharacterSlot, 10,
            plan, expectedListing, updatedListing);
        var store = new MariaDbWorldCharacterStore(connectionFactory, worldKey);
        if (await store.TryCommitAsync(request) != LegacyAutoTradePurchaseCommitResult.Committed)
            throw new InvalidOperationException("MariaDB purchase smoke rejected a valid atomic commit.");

        var committedBuyer = await ReadSmokeBlobAsync(connectionFactory, buyerAccount, worldKey);
        var committedSeller = await ReadSmokeBlobAsync(connectionFactory, sellerAccount, worldKey);
        var committedListing = await stateStore.ReadAsync(sellerAccount, sellerCharacterSlot);
        var buyerCoin = BinaryPrimitives.ReadInt32LittleEndian(committedBuyer.AsSpan(LegacyAccountSnapshot.AccountCoinOffset));
        var sellerCoin = BinaryPrimitives.ReadInt32LittleEndian(committedSeller.AsSpan(LegacyAccountSnapshot.AccountCoinOffset));
        var buyerItem = LegacyItem.Read(committedBuyer.AsSpan(
            LegacyAccountSnapshot.CharactersOffset + LegacyAccountSnapshot.MobCarryOffset,
            LegacyItem.SizeInBytes));
        var sellerItem = LegacyItem.Read(committedSeller.AsSpan(
            LegacyAccountSnapshot.CargoOffset + (sellerCargoPosition * LegacyItem.SizeInBytes),
            LegacyItem.SizeInBytes));
        if (buyerCoin != 50 || sellerCoin != 110 || buyerItem != item || sellerItem != default ||
            committedListing is null || committedListing.Items[0] != default || committedListing.CarryPositions[0] != -1 ||
            committedListing.Prices[0] != 0)
            throw new InvalidOperationException("MariaDB purchase smoke committed an unexpected buyer, seller, or listing state.");

        if (await store.TryCommitAsync(request) != LegacyAutoTradePurchaseCommitResult.Conflict)
            throw new InvalidOperationException("MariaDB purchase smoke did not reject a replayed compare-and-swap request.");
        Console.WriteLine("AUTOTRADE_PURCHASE_DB_SMOKE=PASS");
    }
    finally
    {
        await DeleteSmokeStateAsync(connectionFactory, buyerAccount, sellerAccount, worldKey, sellerCharacterSlot);
    }
}

static byte[] CreateSmokeAccountBlob(byte characterMarker, int slot, int coin)
{
    var blob = new byte[LegacyAccountSnapshot.RequiredFileLength];
    var characterOffset = LegacyAccountSnapshot.CharactersOffset + (slot * LegacyAccountSnapshot.CharacterStride);
    blob[characterOffset + LegacyAccountSnapshot.MobNameOffset] = characterMarker;
    BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(characterOffset + LegacyAccountSnapshot.MobCoinOffset), coin);
    BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), coin);
    return blob;
}

static async Task UpsertSmokeBlobAsync(Func<DbConnection> connectionFactory, string accountName, string worldKey, byte[] blob)
{
    await using var connection = connectionFactory();
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = "INSERT INTO wyd_world_account_state (account_name, world_key, account_blob, version) VALUES (@account_name, @world_key, @account_blob, 0) ON DUPLICATE KEY UPDATE account_blob = VALUES(account_blob), version = 0";
    AddParameter(command, "@account_name", accountName);
    AddParameter(command, "@world_key", worldKey);
    AddParameter(command, "@account_blob", blob);
    await command.ExecuteNonQueryAsync();
}

static async Task<byte[]> ReadSmokeBlobAsync(Func<DbConnection> connectionFactory, string accountName, string worldKey)
{
    await using var connection = connectionFactory();
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT account_blob FROM wyd_world_account_state WHERE account_name = @account_name AND world_key = @world_key";
    AddParameter(command, "@account_name", accountName);
    AddParameter(command, "@world_key", worldKey);
    var value = await command.ExecuteScalarAsync() as byte[];
    return value ?? throw new InvalidOperationException($"MariaDB purchase smoke blob missing for {accountName}.");
}

static async Task DeleteSmokeStateAsync(Func<DbConnection> connectionFactory, string buyerAccount, string sellerAccount, string worldKey, int sellerCharacterSlot)
{
    await using var connection = connectionFactory();
    await connection.OpenAsync();
    await using var listingCommand = connection.CreateCommand();
    listingCommand.CommandText = "DELETE FROM wyd_autotrade_state WHERE world_key = @world_key AND account_name = @seller_account AND character_slot = @character_slot";
    AddParameter(listingCommand, "@world_key", worldKey);
    AddParameter(listingCommand, "@seller_account", sellerAccount);
    AddParameter(listingCommand, "@character_slot", sellerCharacterSlot);
    await listingCommand.ExecuteNonQueryAsync();

    await using var blobCommand = connection.CreateCommand();
    blobCommand.CommandText = "DELETE FROM wyd_world_account_state WHERE world_key = @world_key AND account_name IN (@buyer_account, @seller_account)";
    AddParameter(blobCommand, "@world_key", worldKey);
    AddParameter(blobCommand, "@buyer_account", buyerAccount);
    AddParameter(blobCommand, "@seller_account", sellerAccount);
    await blobCommand.ExecuteNonQueryAsync();
}

static void AddParameter(DbCommand command, string name, object value)
{
    var parameter = command.CreateParameter();
    parameter.ParameterName = name;
    parameter.Value = value;
    command.Parameters.Add(parameter);
}

static IEnumerable<string> SplitStatements(string sql)
{
    var statement = new StringBuilder();
    var quote = '\0';
    var lineComment = false;
    for (var index = 0; index < sql.Length; index++)
    {
        var character = sql[index];
        var next = index + 1 < sql.Length ? sql[index + 1] : '\0';

        if (lineComment)
        {
            statement.Append(character);
            if (character is '\r' or '\n')
                lineComment = false;
            continue;
        }

        if (quote == '\0')
        {
            if (character == '#' || (character == '-' && next == '-' &&
                (index + 2 >= sql.Length || char.IsWhiteSpace(sql[index + 2]))))
            {
                lineComment = true;
                statement.Append(character);
                if (character == '-')
                {
                    statement.Append(next);
                    index++;
                }
                continue;
            }

            if (character is '\'' or '"' or '`')
                quote = character;
            else if (character == ';')
            {
                var completed = statement.ToString().Trim();
                if (completed.Any(static value => !char.IsWhiteSpace(value)))
                    yield return completed;
                statement.Clear();
                continue;
            }
        }
        else if (character == quote)
        {
            quote = '\0';
        }
        else if (character == '\\' && quote != '`' && index + 1 < sql.Length)
        {
            statement.Append(character);
            statement.Append(sql[++index]);
            continue;
        }

        statement.Append(character);
    }

    var last = statement.ToString().Trim();
    if (last.Any(static value => !char.IsWhiteSpace(value)))
        yield return last;
}

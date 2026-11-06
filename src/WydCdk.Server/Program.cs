using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.Server;
using WydCdk.World;

var options = ServerOptions.Parse(args);
int? tinyChanceBaseFromFile = options.CompRatePath is null
    ? null
    : LegacyTinyChanceConfiguration.ParseCompRate(
        await File.ReadAllLinesAsync(options.CompRatePath, CancellationToken.None));
var tinyChanceBase = LegacyTinyChanceConfiguration.ResolveBase(options.TinyChanceBaseOverride, tinyChanceBaseFromFile);
using var serverLog = ServerWireLog.TryOpen(options.WorldKey);
if (serverLog is not null)
{
    Console.SetOut(serverLog.CreateTee(Console.Out));
    Console.SetError(serverLog.CreateTee(Console.Error));
}
Console.WriteLine($"Tiny combine chance base: {tinyChanceBase} (source={(options.TinyChanceBaseOverride is not null ? "command-line override" : options.CompRatePath is not null ? "CompRate.txt" : "7.69 default")}).");

var autoDoubleConfiguration = options.AutoDoubleConfigPath is null
    ? null
    : LegacyAutoDoubleConfiguration.Parse(await File.ReadAllTextAsync(options.AutoDoubleConfigPath, CancellationToken.None));
var battleRoyaleConfiguration = options.BattleRoyaleConfigPath is null
    ? null
    : LegacyBattleRoyaleConfiguration.Parse(await File.ReadAllTextAsync(options.BattleRoyaleConfigPath, CancellationToken.None));
var guildNameCatalog = await LegacyGuildNameCatalog.LoadAsync(options.GuildNamesPath, options.ServerGroup, CancellationToken.None);

ICharacterStore? accounts = options.AccountRoot is not null
    ? new LegacyFileAccountStore(options.AccountRoot)
    : options.AccountDatabaseConfigPath is not null
        ? new MariaDbWorldCharacterStore(MariaDbConnectionFactory.FromJson(options.AccountDatabaseConfigPath), options.WorldKey)
        : null;
IAccountStore? accountAuthenticator = accounts as IAccountStore;
if (options.AccountDatabaseConfigPath is not null)
    accountAuthenticator = new MariaDbAccountAuthenticator(MariaDbConnectionFactory.FromJson(options.AccountDatabaseConfigPath));
IAccountReviewStore? accountReviewStore = options.AccountReviewRoot is null
    ? null
    : new FileAccountReviewStore(options.AccountReviewRoot);
if (accountAuthenticator is not null && accountReviewStore is null)
    throw new ArgumentException("--account-review-root is required whenever account authentication is enabled.");
IDonateBalanceStore? donateBalances = accounts as IDonateBalanceStore;
var templateRoot = options.AccountRoot is not null
    ? Path.Combine(Path.GetDirectoryName(options.AccountRoot)!, "BaseMob")
    : Path.Combine(AppContext.BaseDirectory, "BaseMob");
var templates = Directory.Exists(templateRoot) ? new LegacyCharacterTemplateStore(templateRoot) : null;
var skills = LoadSkillData(options.SkillDataPath);
var itemData = LoadItemData(options.ItemDataPath);
var mapGrid = options.HeightMapPath is null ? null : LegacyMapGrid.Load(options.HeightMapPath, options.AttributeMapPath!);
var mapItems = options.InitItemPath is null
    ? null
    : itemData is null
        ? throw new ArgumentException("--init-item requires --item-data so EF_GROUND and EF_KEYID can be resolved.")
        : LegacyMapItemCatalog.CreateInitialStates(LegacyMapItemCatalog.LoadDefinitions(options.InitItemPath), itemData, mapGrid);
var guildZones = options.GuildDataPath is null ? null : LegacyGuildZoneState.Load(options.GuildDataPath);
var summonCatalog = options.SummonRoot is null ? null : LegacySummonCatalog.Load(options.SummonRoot);
var npcGenerationCatalog = options.NpcGenerationPath is null ? null : LegacyNpcGenerationCatalog.Load(
    options.NpcGenerationPath,
    options.NpcRoot!,
    optionalTemplateNames: ["Vinha"]);
var donateShopCatalog = options.DonateShopCatalogPath is null ? null : LegacyDonateShopCatalog.Load(options.DonateShopCatalogPath);
if (options.DonateDatabaseConfigPath is not null)
{
    if (options.DonateShopCatalogPath is not null)
        throw new ArgumentException("--donate-catalog and --donate-db-config cannot be combined.");

    var mariaDbDonateStore = new MariaDbDonateShopStore(MariaDbConnectionFactory.FromJson(options.DonateDatabaseConfigPath));
    donateBalances = mariaDbDonateStore;
    donateShopCatalog = await mariaDbDonateStore.ReadDonateCatalogAsync()
        ?? throw new InvalidDataException("MariaDB Donate Shop catalog is empty.");
}
var world = new WorldHub(mapGrid, guildZones, options.MapCollisionMode, summonCatalog, itemData, skills, npcGenerationCatalog, donateShopCatalog, options.ServerMode);
var guildTreasuryFileStore = options.NpcRoot is null ? null : new LegacyGuildTreasuryFileStore(options.NpcRoot);
world.SetDoubleModeMultiplier(options.DoubleModeMultiplier);
var autoTradeBook = new LegacyAutoTradeBook();
var sessions = new LoginSessionRegistry();
var accountOperationGate = new LegacyAccountOperationGate(options.WorldKey);
var accountCargoRuntime = new LegacyAccountCargoRuntime(options.WorldKey);
ILegacyAutoTradeStateStore? autoTradeStateStore = options.AutoTradeStatePath is not null
    ? new LegacyAutoTradeFileStore(options.AutoTradeStatePath, options.WorldKey, options.AccountRoot)
    : options.AccountDatabaseConfigPath is not null
        ? new MariaDbAutoTradeStateStore(MariaDbConnectionFactory.FromJson(options.AccountDatabaseConfigPath), options.WorldKey)
        : null;
var autoTradePurchaseCommitStore = accounts as ILegacyAutoTradePurchaseCommitStore ??
    autoTradeStateStore as ILegacyAutoTradePurchaseCommitStore;
var autoTradeRehydrateReport = new LegacyAutoTradeRehydrateReport(0, []);
if (accounts is not null && autoTradeStateStore is not null)
{
    autoTradeRehydrateReport = await LegacyAutoTradeRehydrator.RestoreAsync(
        world,
        autoTradeBook,
        accounts,
        autoTradeStateStore,
        CancellationToken.None);
    foreach (var issue in autoTradeRehydrateReport.Issues)
        Console.WriteLine($"Autotrade rehydration skipped: account={issue.AccountName}, slot={issue.CharacterSlot}, reason={issue.Reason}.");
}
if (mapItems is not null)
    world.ConfigureMapItems(mapItems);
var cityNpcs = options.SpawnCityNpcs && npcGenerationCatalog is not null
    ? SpawnReferenceCityPerzens(world)
    : [];
var donateNpcs = options.SpawnDonateNpc && npcGenerationCatalog is not null
    ? SpawnReferenceDonateNpc(world)
    : [];
if (options.CastleQuestPath is not null)
    world.ConfigureCastleQuests(LegacyCastleQuestConfiguration.Load(options.CastleQuestPath));
var bindAddress = IPAddress.Parse(options.BindAddress);
var listener = new TcpListener(bindAddress, options.Port);
listener.Start();
var statusPublisher = options.StatusFilePath is null || options.StatusSlot is null
    ? null
    : new ServerStatusFilePublisher(options.StatusFilePath, options.StatusSlot.Value);
Console.WriteLine($"WydCdk listener: {options.BindAddress}:{options.Port}");
Console.WriteLine($"World: {options.WorldKey} (shared account/character base; world rules are server-authoritative).");
Console.WriteLine(accountAuthenticator is null
        ? "Diagnostic mode: validates MSG_AccountLogin only; account files and MariaDB are not accessed."
    : options.AccountDatabaseConfigPath is null
        ? $"Local account mode: authentication at {options.AccountRoot}; successful logins receive character selection, cargo, and coin read from the legacy account file, and empty slots accept MSG_CreateCharacter."
        : $"MariaDB account/world mode: authentication configured by {options.AccountDatabaseConfigPath}; character blobs are stored by account_name and world_key in MariaDB.");
Console.WriteLine(skills is null ? "Skill data: disabled; attack frames are not processed." : $"Skill data: loaded from {options.SkillDataPath}; attack metadata gate enabled.");
Console.WriteLine(itemData is null ? "Item data: disabled; skill combat inputs are not derived from ItemList.bin." : $"Item data: loaded from {options.ItemDataPath}; Magic and WeaponDamage inputs enabled.");
Console.WriteLine(mapItems is null ? "Static map items: disabled; use --init-item <InitItem.bin> with --item-data to load InitItem.bin." : $"Static map items: loaded from {options.InitItemPath}; {mapItems.Count} authoritative entries enabled.");
Console.WriteLine(summonCatalog is null ? "Summon data: disabled; InstanceType 11 will refund mana without creating NPCs." : $"Summon data: loaded from {options.SummonRoot}; BaseSummon catalog enabled.");
Console.WriteLine(npcGenerationCatalog is null ? "NPC generation data: disabled; generated respawns remain requests only." : $"NPC generation data: loaded from {options.NpcGenerationPath}; generated NPC catalog enabled.");
Console.WriteLine(!options.DungeonEventEnabled
    ? "Dungeon event: disabled by --dungeon-event false."
    : itemData is null
        ? "Dungeon event: enabled by default but prizes are skipped until --item-data loads ItemList.bin."
        : "Dungeon event: enabled; 7.69 timed dungeon prizes use the loaded ItemList.bin.");
Console.WriteLine(options.SpawnCityNpcs
    ? $"City NPCs: spawned {cityNpcs.Count} configured Perzen NPCs from the reference generator IDs."
    : "City NPCs: disabled; use --city-npcs with NPCGener.txt and npc templates to spawn the reference Perzen NPCs.");
Console.WriteLine(options.SpawnDonateNpc
    ? $"Donate NPCs: spawned {donateNpcs.Count} configured Donation Store NPCs from the selected generator."
    : "Donate NPCs: disabled; use --donate-npc with NPCGener.txt and npc templates for an explicit local shop smoke.");
Console.WriteLine(options.CastleQuestPath is null ? "Castle quest data: default boss IDs 0/3 with no configured rewards." : $"Castle quest data: loaded from {options.CastleQuestPath}; reward distribution enabled.");
Console.WriteLine(donateShopCatalog is null
    ? "Donate Shop data: disabled; purchases remain rejected."
    : options.DonateDatabaseConfigPath is null
        ? $"Donate Shop data: loaded from {options.DonateShopCatalogPath}; catalog and local Donate purchases enabled."
        : $"Donate Shop data: loaded from MariaDB configured by {options.DonateDatabaseConfigPath}; catalog and Donate persistence enabled.");
Console.WriteLine(options.DonateDatabaseConfigPath is not null
    ? $"Donate persistence: MariaDB configured by {options.DonateDatabaseConfigPath}; credentials are read only from that local config."
    : options.AccountDatabaseConfigPath is not null
        ? "Donate persistence: legacy Donate field inside the MariaDB world account blob; optional accounts.donate is not queried."
        : "Donate persistence: legacy account file.");
Console.WriteLine(autoTradeStateStore is null
    ? "Autotrade state persistence: disabled; use --autotrade-state <path> for the versioned local adapter."
    : options.AutoTradeStatePath is not null
        ? $"Autotrade state persistence: local versioned file at {options.AutoTradeStatePath}; offline MOB rehydrated={autoTradeRehydrateReport.RestoredCount}."
        : $"Autotrade state persistence: MariaDB world table; offline MOB rehydrated={autoTradeRehydrateReport.RestoredCount}.");
Console.WriteLine($"Map collision: {options.MapCollisionMode}.");
Console.WriteLine($"DoubleMode multiplier: {options.DoubleModeMultiplier}.");
Console.WriteLine(autoDoubleConfiguration is null
    ? "AutoDouble schedule: disabled."
    : $"AutoDouble schedule: loaded from {options.AutoDoubleConfigPath}; start={autoDoubleConfiguration.DayStart}/{autoDoubleConfiguration.HourStart:D2}:{autoDoubleConfiguration.MinStart:D2}, end={autoDoubleConfiguration.DayEnd}/{autoDoubleConfiguration.HourEnd:D2}:{autoDoubleConfiguration.MinEnd:D2}, multiplier={autoDoubleConfiguration.MultEXP}.");
Console.WriteLine(battleRoyaleConfiguration is null
    ? "Battle Royale schedule: disabled."
    : $"Battle Royale schedule: loaded from {options.BattleRoyaleConfigPath}; hour={battleRoyaleConfiguration.BRHour:D2}, item={battleRoyaleConfiguration.BRItem}, server-index={battleRoyaleConfiguration.ServerIndex}/{battleRoyaleConfiguration.NumServerInGroup}, test-server={battleRoyaleConfiguration.TestServer}, local-server={battleRoyaleConfiguration.LocalServer}; volatile initial state=0.");
Console.WriteLine(statusPublisher is null
    ? "HTTP status file: disabled."
    : $"HTTP status file: {options.StatusFilePath}; slot {options.StatusSlot} publishes player count and -1 on shutdown.");
Console.WriteLine("Press Ctrl+C to stop.");

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; shutdown.Cancel(); };
// Console.In can expose a synchronous ReadLineAsync implementation under the
// Windows PTY used by the local smoke. Keep that watcher off the listener
// startup path so AcceptTcpClientAsync is reached immediately.
var standardInputShutdown = Task.Run(() => RunStandardInputShutdownAsync(shutdown));
var connectionSlots = new LegacyConnectionSlotAllocator();
var connectionTaskDrain = new LegacyConnectionTaskDrain();
var characterCheckpointRuntime = accounts is ICharacterCheckpointStore checkpointStore
    ? new LegacyCharacterCheckpointRuntime(
        sessions,
        world,
        checkpointStore,
        (account, slot, x, y, cancellationToken) => accounts!.TrySaveCharacterPositionAsync(account, slot, x, y, cancellationToken),
        accountOperationGate: accountOperationGate)
    : null;
var characterCheckpointLoop = characterCheckpointRuntime?.RunAsync(shutdown.Token) ?? Task.CompletedTask;
if (characterCheckpointRuntime is not null)
{
    _ = characterCheckpointLoop.ContinueWith(static (completed, state) =>
    {
        var cancellation = (CancellationTokenSource)state!;
        if (completed.IsFaulted)
        {
            Console.Error.WriteLine($"Character checkpoint runtime failed: {completed.Exception?.GetBaseException().GetType().Name}.");
            cancellation.Cancel();
        }
        else if (completed.IsCanceled && !cancellation.IsCancellationRequested)
        {
            Console.Error.WriteLine("Character checkpoint runtime stopped unexpectedly.");
            cancellation.Cancel();
        }
    }, shutdown, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
}
var summonBattleLoop = RunSummonBattleLoopAsync(world, shutdown.Token);
var pistaScheduleLoop = RunPistaScheduleLoopAsync(world, shutdown.Token);
var guildTreasurySaveLoop = guildTreasuryFileStore is null
    ? Task.CompletedTask
    : RunGuildTreasurySaveLoopAsync(world, guildTreasuryFileStore, shutdown.Token);
var autoDoubleScheduleLoop = autoDoubleConfiguration is null
    ? Task.CompletedTask
    : RunAutoDoubleScheduleLoopAsync(world, autoDoubleConfiguration, shutdown.Token);
var battleRoyaleScheduleLoop = battleRoyaleConfiguration is null
    ? Task.CompletedTask
    : RunBattleRoyaleScheduleLoopAsync(world, sessions, accountOperationGate, battleRoyaleConfiguration, shutdown.Token);
var groundItemRelayCodec = LegacyFrameCodec.CreateDefault();
var npcGenerationRuntime = npcGenerationCatalog is null
    ? null
    : new LegacyNpcGenerationRuntime(
        world,
        npcGenerationCatalog,
        (group, cancellationToken) => new ValueTask(RelayGeneratedNpcGroupAsync(world, group, cancellationToken)),
        itemData: itemData,
        dungeonEventEnabled: options.DungeonEventEnabled,
        relayDungeonItem: (spawn, cancellationToken) => LegacyGroundItemRelay.RelaySpawnAsync(
            world, spawn, groundItemRelayCodec, cancellationToken));
var npcGenerationLoop = npcGenerationRuntime?.RunAsync(shutdown.Token) ?? Task.CompletedTask;
if (npcGenerationRuntime is not null)
{
    _ = npcGenerationLoop.ContinueWith(static (completed, state) =>
    {
        var cancellation = (CancellationTokenSource)state!;
        if (completed.IsFaulted)
        {
            Console.Error.WriteLine($"NPC generation runtime failed: {completed.Exception?.GetBaseException().Message}");
            cancellation.Cancel();
        }
        else if (completed.IsCanceled && !cancellation.IsCancellationRequested)
        {
            Console.Error.WriteLine("NPC generation runtime stopped unexpectedly.");
            cancellation.Cancel();
        }
    }, shutdown, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
}
var mobAiCodec = LegacyFrameCodec.CreateDefault();
var mobAiRuntime = new LegacyMobAiRuntime(world, itemData, (outcome, cancellationToken) =>
    LegacyMobAiRelay.RelayAsync(
        world,
        outcome,
        mobAiCodec,
        (connectionId, mob, itemSlot, item, recipients, token) => new ValueTask(SendResurrectionRefreshAsync(
            world, connectionId, mob, itemSlot, item, includeScoreAndResources: true, token, recipients)),
        cancellationToken));
var mobAiLoop = mobAiRuntime.RunAsync(shutdown.Token);
_ = mobAiLoop.ContinueWith(static (completed, state) =>
{
    var cancellation = (CancellationTokenSource)state!;
    if (completed.IsFaulted)
    {
        Console.Error.WriteLine($"Generated-NPC AI runtime failed: {completed.Exception?.GetBaseException().Message}");
        cancellation.Cancel();
    }
    else if (completed.IsCanceled && !cancellation.IsCancellationRequested)
    {
        Console.Error.WriteLine("Generated-NPC AI runtime stopped unexpectedly.");
        cancellation.Cancel();
    }
}, shutdown, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
var playerAffectCodec = LegacyFrameCodec.CreateDefault();
var playerAffectRuntime = new LegacyPlayerAffectRuntime(world, (outcome, cancellationToken) =>
    LegacyPlayerAffectRelay.RelayAsync(world, outcome, playerAffectCodec, cancellationToken));
var playerAffectLoop = playerAffectRuntime.RunAsync(shutdown.Token);
_ = playerAffectLoop.ContinueWith(static (completed, state) =>
{
    var cancellation = (CancellationTokenSource)state!;
    if (completed.IsFaulted)
    {
        Console.Error.WriteLine($"Player affect timer runtime failed: {completed.Exception?.GetBaseException().Message}");
        cancellation.Cancel();
    }
    else if (completed.IsCanceled && !cancellation.IsCancellationRequested)
    {
        Console.Error.WriteLine("Player affect timer runtime stopped unexpectedly.");
        cancellation.Cancel();
    }
}, shutdown, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
// Do not let the optional legacy status file delay the network gate. The
// heartbeat refreshes the real count after startup; the initial value is
// necessarily zero and is published off the accept-loop path.
_ = Task.Run(() => TryPublishStatus(statusPublisher, 0, "online"));
var statusLoop = RunStatusPublicationLoopAsync(statusPublisher, world, shutdown.Token);

try
{
    while (!shutdown.IsCancellationRequested)
    {
        var client = await listener.AcceptTcpClientAsync(shutdown.Token);
        if (!connectionSlots.TryAllocate(out var connectionId))
        {
            client.Dispose();
            Console.Error.WriteLine("Rejected accepted connection: all 999 player connection slots are occupied.");
            continue;
        }
        connectionTaskDrain.Track(RunAllocatedConnectionAsync(client, connectionId));
    }
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
}
finally
{
    listener.Stop();
    shutdown.Cancel();
    var shutdownFailures = new List<Exception>();
    await AwaitShutdownLoopAsync(standardInputShutdown, shutdown.Token, shutdownFailures);
    await AwaitShutdownLoopAsync(statusLoop, shutdown.Token, shutdownFailures);
    await AwaitShutdownLoopAsync(summonBattleLoop, shutdown.Token, shutdownFailures);
    await AwaitShutdownLoopAsync(pistaScheduleLoop, shutdown.Token, shutdownFailures);
    await AwaitShutdownLoopAsync(autoDoubleScheduleLoop, shutdown.Token, shutdownFailures);
    await AwaitShutdownLoopAsync(battleRoyaleScheduleLoop, shutdown.Token, shutdownFailures);
    await AwaitShutdownLoopAsync(characterCheckpointLoop, shutdown.Token, shutdownFailures);
    await AwaitShutdownLoopAsync(npcGenerationLoop, shutdown.Token, shutdownFailures);
    await AwaitShutdownLoopAsync(mobAiLoop, shutdown.Token, shutdownFailures);
    await AwaitShutdownLoopAsync(playerAffectLoop, shutdown.Token, shutdownFailures);
    await AwaitShutdownLoopAsync(connectionTaskDrain.StopAcceptingAndDrainAsync(), shutdown.Token, shutdownFailures);
    await AwaitShutdownLoopAsync(guildTreasurySaveLoop, shutdown.Token, shutdownFailures);
    if (guildTreasuryFileStore is not null)
        await AwaitShutdownLoopAsync(SaveGuildTreasuryShutdownCheckpointAsync(world, guildTreasuryFileStore), shutdown.Token, shutdownFailures);
    TryPublishStatus(statusPublisher, -1, "offline");
    if (shutdownFailures.Count > 0)
        throw new AggregateException("One or more Host loops failed during cooperative shutdown.", shutdownFailures);
}

async Task RunAllocatedConnectionAsync(TcpClient client, int connectionId)
{
    try
    {
        await InspectConnectionAsync(client, connectionId, accounts, characterCheckpointRuntime, accountAuthenticator, accountReviewStore, templates, skills, itemData, donateBalances, world, sessions, accountOperationGate, accountCargoRuntime, autoTradeBook, autoTradeStateStore, autoTradePurchaseCommitStore, options.DonateDatabaseConfigPath is not null, tinyChanceBase, guildTreasuryFileStore, guildNameCatalog, serverLog, shutdown.Token);
    }
    catch (Exception error)
    {
        Console.Error.WriteLine($"Connection wrapper failed: connection={connectionId}, error={error.GetType().Name}.");
        client.Dispose();
    }
    finally
    {
        connectionSlots.Release(connectionId);
    }
}

static async Task AwaitShutdownLoopAsync(Task loop, CancellationToken shutdownToken, List<Exception> failures)
{
    try
    {
        await loop;
    }
    catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
    {
    }
    catch (Exception error)
    {
        failures.Add(error);
    }
}

static async Task RunStandardInputShutdownAsync(CancellationTokenSource shutdown)
{
    try
    {
        while (await Console.In.ReadLineAsync(shutdown.Token) is { } line)
        {
            if (!string.Equals(line.Trim(), "stop", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(line.Trim(), "shutdown", StringComparison.OrdinalIgnoreCase))
                continue;

            Console.WriteLine("Cooperative shutdown requested through standard input.");
            shutdown.Cancel();
            return;
        }
    }
    catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
    {
    }
}

static async Task RunStatusPublicationLoopAsync(ServerStatusFilePublisher? publisher, WorldHub world, CancellationToken cancellationToken)
{
    if (publisher is null)
        return;

    using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
    try
    {
        while (await timer.WaitForNextTickAsync(cancellationToken))
            TryPublishStatus(publisher, world.Count, "heartbeat");
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
}

static async Task RunGuildTreasurySaveLoopAsync(
    WorldHub world,
    LegacyGuildTreasuryFileStore store,
    CancellationToken cancellationToken)
{
    using var timer = new PeriodicTimer(LegacyGuildTreasurySavePolicy.Interval);
    try
    {
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                var results = await store.SaveRegisteredAsync(world, cancellationToken);
                foreach (var result in results.Where(static result => result.Result == LegacyGuildTreasurySaveResult.NpcMissing))
                    Console.WriteLine($"Guild treasury periodic save skipped: category=GuildTaxNpc, npc={result.ConnectionId}, reason=NpcMissing.");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                Console.WriteLine($"Guild treasury periodic save failed: category=GuildTaxNpc, error={error.GetType().Name}; retry=next-interval.");
            }
        }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
}

static async Task SaveGuildTreasuryShutdownCheckpointAsync(WorldHub world, LegacyGuildTreasuryFileStore store)
{
    try
    {
        var results = await store.SaveRegisteredAsync(world, CancellationToken.None);
        var saved = results.Count(static result => result.Result == LegacyGuildTreasurySaveResult.Saved);
        var missing = results.Count(static result => result.Result == LegacyGuildTreasurySaveResult.NpcMissing);
        var dead = results.Count(static result => result.Result == LegacyGuildTreasurySaveResult.NpcDead);
        Console.WriteLine($"Guild treasury shutdown checkpoint completed: attempted={results.Count}, saved={saved}, npc-missing={missing}, npc-dead={dead}.");
    }
    catch (Exception error)
    {
        Console.Error.WriteLine($"Guild treasury shutdown checkpoint failed: error={error.GetType().Name}.");
        throw;
    }
}

static void TryPublishStatus(ServerStatusFilePublisher? publisher, int playerCount, string reason)
{
    if (publisher is null)
        return;

    try
    {
        if (playerCount < 0)
            publisher.PublishOffline();
        else
            publisher.PublishOnline(playerCount);
        Console.WriteLine($"HTTP status updated: value={(playerCount < 0 ? -1 : playerCount)} reason={reason}.");
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"HTTP status update failed: {exception.GetType().Name}: {exception.Message}");
    }
}

static async Task InspectConnectionAsync(TcpClient client, int connectionId, ICharacterStore? accounts, LegacyCharacterCheckpointRuntime? characterCheckpointRuntime, IAccountStore? accountAuthenticator, IAccountReviewStore? accountReviewStore, LegacyCharacterTemplateStore? templates, LegacySkillDataTable? skills, LegacyItemDataTable? itemData, IDonateBalanceStore? donateBalances, WorldHub world, LoginSessionRegistry sessions, LegacyAccountOperationGate accountOperationGate, LegacyAccountCargoRuntime accountCargoRuntime, LegacyAutoTradeBook autoTradeBook, ILegacyAutoTradeStateStore? autoTradeStateStore, ILegacyAutoTradePurchaseCommitStore? autoTradePurchaseCommitStore, bool explicitDonateDatabase, int tinyChanceBase, LegacyGuildTreasuryFileStore? guildTreasuryFileStore, LegacyGuildNameCatalog guildNameCatalog, ServerWireLog? serverLog, CancellationToken cancellationToken)
{
    using (client)
    {
        using var stream = new SerializedNetworkStream(client.GetStream());
        var decoder = new LegacyFrameStream(LegacyFrameCodec.CreateDefault());
        var donateShopRateLimiter = new LegacyDonateShopRateLimiter();
        var actionAccountReview = accountReviewStore is null ? null : new LegacyActionAccountReviewCoordinator(accountReviewStore);
        sessions.Open(connectionId);
        serverLog?.Write($"CONNECTION OPEN connection={connectionId} remote={client.Client.RemoteEndPoint}");
        var buffer = new byte[4096];

        async Task LogoutCharacterAsync()
        {
            if (sessions.TryGet(connectionId, out var logoutSession) && logoutSession!.State == LoginSessionState.Playing)
            {
                var logoutPlan = await LegacyAutoTradeDepartureCoordinator.DepartAsync(connectionId, world, autoTradeBook,
                    async (accountLease, ct) =>
                    {
                        await SaveCharacterOnDepartureAsync(accounts, characterCheckpointRuntime, world, logoutSession, disconnect: false, ct, accountLease);
                        sessions.CompleteCharacterLogout(connectionId);
                    },
                    async (shop, ct) => await RemoveAutoTradeStateAsync(autoTradeStateStore, logoutSession.AccountName, logoutSession.CharacterSlot, shop, ct),
                    LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256),
                    accountOperationGate, cancellationToken);
                await BroadcastAutoTradeDepartureAsync(world, logoutPlan, cancellationToken);
                await BroadcastSummonDespawnsAsync(world, logoutPlan.DespawnedSummonIds, cancellationToken);
            }
            // CharLogOut acknowledges even outside USER_PLAY; it does not close TCP.
            await stream.WriteAsync(CharacterLogoutConfirmation.ToFrame(LegacyFrameCodec.CreateDefault(), connectionId,
                unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256)), cancellationToken);
        }

        async Task<LegacyCrackErrorOutcome?> RecordCrackErrorAsync(int points, int type, bool deferCharacterLogout = false)
        {
            if (!sessions.TryAddCrackError(connectionId, points, type, out var error)) return null;
            if (error!.ShouldLog)
                Console.WriteLine($"Crack error: connection={connectionId}, points={error.AddedPoints}, type={error.Type}, total={error.TotalPoints}.");
            if (!error.RequiresCharacterLogout || deferCharacterLogout) return error;
            await stream.WriteAsync(new MessagePanelConfirmation(LegacyCrackErrorRules.BadNetworkNotice)
                .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256)), cancellationToken);
            await LogoutCharacterAsync();
            Console.WriteLine($"Crack character logout: connection={connectionId}, type={error.Type}.");
            return error;
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    serverLog?.Write($"RX EOF connection={connectionId}");
                    return;
                }
                decoder.Append(buffer.AsSpan(0, read));

                while (decoder.TryRead(out var frame))
                {
                    serverLog?.WriteFrame("RX", connectionId, frame);
                    // ProcessClientMessage rejects the legacy internal-server timestamp on every client-originated packet.
                    if (!ClientTickPolicy.IsAllowedFromClient(frame.Header.ClientTick))
                    {
                        Console.WriteLine($"Rejected client frame with reserved ClientTick: connection={connectionId}, type=0x{frame.Header.Type:X4}.");
                        continue;
                    }

                    if (AccountLoginRequest.TryParse(frame, out var login) && login is not null)
                    {
                        Console.WriteLine($"Account login frame: account={login.AccountName}, clientVersion={login.ClientVersion}, dbNeedSave={login.DbNeedSave}");
                        if (!ClientReleasePolicy.IsSupported(login.ClientVersion))
                        {
                            var failure = new MessagePanelConfirmation($"Cliente incompativel. Use a versao {ClientReleasePolicy.RequiredClientVersion / 1000.0:F3}.")
                                .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, failure, cancellationToken, "account-login-failure-client-version");
                            Console.WriteLine($"Account login rejected: unsupported clientVersion={login.ClientVersion}; required={ClientReleasePolicy.RequiredClientVersion}.");
                            return;
                        }

                        if (accountAuthenticator is null)
                        {
                            Console.WriteLine("Account authentication is disabled; no response sent.");
                            return;
                        }

                        var outcome = await new AccountLoginCoordinator(sessions, accountAuthenticator, accountReviewStore!).HandleAsync(connectionId, login, cancellationToken);
                        if (!outcome.IsSuccess)
                        {
                            var failureText = outcome.Rejection == AccountLoginRejection.None
                                ? AccountLoginFailureNotice.For(outcome.Authentication ?? AccountAuthenticationStatus.InvalidAccountFile)
                                : AccountLoginFailureNotice.ForReview(outcome.Rejection);
                            var failure = new MessagePanelConfirmation(failureText)
                                .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, failure, cancellationToken, "account-login-failure");
                            if (outcome.Rejection == AccountLoginRejection.ReviewStoreUnavailable)
                                Console.Error.WriteLine($"Account review lookup unavailable: connection={connectionId}; login denied fail-closed and generic notice sent.");
                            else
                                Console.WriteLine(outcome.Rejection == AccountLoginRejection.None
                                    ? $"Account login rejected: {outcome.Authentication ?? AccountAuthenticationStatus.InvalidAccountFile}; failure notice sent to client."
                                    : $"Account login rejected: {outcome.Rejection}; failure notice sent to client.");
                            return;
                        }

                        LegacyAccountSnapshot? snapshot;
                        LegacyAccountCargoSnapshot? cargoSnapshot = null;
                        await using (var accountLease = await accountOperationGate.AcquireAsync([login.AccountName], cancellationToken))
                        {
                            snapshot = accounts is IAccountSnapshotStore snapshotStore
                                ? await snapshotStore.ReadSnapshotAsync(login.AccountName, cancellationToken)
                                : null;
                            if (snapshot is not null)
                            {
                                cargoSnapshot = accountCargoRuntime.LoadAuthoritative(login.AccountName, snapshot.Cargo);
                                accountCargoRuntime.RegisterSession(login.AccountName, connectionId);
                                snapshot = snapshot with { Cargo = cargoSnapshot.Items };
                            }
                        }
                        if (cargoSnapshot is { WasReconciled: true, ChangedPositions.Count: > 0 })
                            await BroadcastCargoResyncAsync(cargoSnapshot, sessions, world, cancellationToken);
                        var confirmation = snapshot?.ToConfirmation() ?? AccountLoginConfirmation.CreateEmpty(login.AccountName);
                        var response = W2ppAccountLoginV1Adapter.Adapt(confirmation)
                            .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
                        await WriteLoggedFrameAsync(stream, serverLog, connectionId, response, cancellationToken, "account-login-confirmation");
                        Console.WriteLine(snapshot is null
                            ? $"Account login accepted: account={login.AccountName}; sent empty character-selection response (snapshot unavailable)."
                            : $"Account login accepted: account={login.AccountName}; sent character selection, cargo, and coin from the legacy account file.");
                        continue;
                    }

                    if (CreateCharacterRequest.TryParse(frame, out var createCharacter) && createCharacter is not null)
                    {
                        if (accounts is null || templates is null || !sessions.TryGet(connectionId, out var session) || session!.State != LoginSessionState.CharacterSelection)
                        {
                            Console.WriteLine("Rejected create-character frame: session is not in character selection.");
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, NewCharacterFailSignal.ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256)), cancellationToken, "new-character-failure-session");
                            continue;
                        }

                        if (sessions.BeginCharacterWait(connectionId) != LoginTransitionResult.Accepted)
                        {
                            Console.WriteLine("Rejected create-character frame: could not enter character wait state.");
                            continue;
                        }

                        var outcome = await new CreateCharacterCoordinator(accounts, templates).HandleAsync(session.AccountName!, createCharacter, session.SecureVerified, cancellationToken);
                        if (outcome.IsSuccess)
                        {
                            var selection = W2ppCharacterSelectionV1Adapter.Adapt(outcome.Characters!);
                            var response = new NewCharacterConfirmationV769(selection)
                                .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, response, cancellationToken, $"new-character-selection account={session.AccountName} slot={createCharacter.Slot} name={createCharacter.CharacterName} class={createCharacter.CharacterClass}");
                            Console.WriteLine($"Character created: account={session.AccountName}, slot={createCharacter.Slot}, name={createCharacter.CharacterName}, class={createCharacter.CharacterClass}.");
                        }
                        else if (outcome.Status == CreateCharacterStatus.SecureNotVerified)
                        {
                            // CFileDB.cpp:892-899 sends nothing at all for this specific rejection, unlike every other one here.
                            Console.WriteLine($"Character creation rejected: account={session.AccountName}, slot={createCharacter.Slot}, name={createCharacter.CharacterName}, reason={outcome.Status}.");
                        }
                        else
                        {
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, NewCharacterFailSignal.ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256)), cancellationToken, $"new-character-failure account={session.AccountName} slot={createCharacter.Slot} reason={outcome.Status}");
                            Console.WriteLine($"Character creation rejected: account={session.AccountName}, slot={createCharacter.Slot}, name={createCharacter.CharacterName}, reason={outcome.Status}.");
                        }
                        if (sessions.TryGet(connectionId, out var createCompletedSession) && createCompletedSession!.State == LoginSessionState.CharacterWait)
                            sessions.CompleteCharacterRefresh(connectionId);
                        continue;
                    }

                    if (DeleteCharacterRequest.TryParse(frame, out var deleteCharacter) && deleteCharacter is not null)
                    {
                        if (accounts is null || !sessions.TryGet(connectionId, out var deleteSession) || deleteSession!.State != LoginSessionState.CharacterSelection)
                        {
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, DeleteCharacterFailSignal.ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256)), cancellationToken, "delete-character-failure-session");
                            Console.WriteLine("Rejected delete-character frame: session is not in character selection.");
                            continue;
                        }

                        if (sessions.BeginCharacterWait(connectionId) != LoginTransitionResult.Accepted)
                        {
                            Console.WriteLine("Rejected delete-character frame: could not enter character wait state.");
                            continue;
                        }

                        var outcome = await new DeleteCharacterCoordinator(accounts).HandleAsync(deleteSession.AccountName!, deleteCharacter, deleteSession.SecureVerified, cancellationToken);
                        if (outcome.IsSuccess && outcome.Snapshot is not null)
                        {
                            var selection = W2ppCharacterSelectionV1Adapter.Adapt(outcome.Snapshot.Characters);
                            var response = new DeleteCharacterConfirmationV769(selection)
                                .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, response, cancellationToken, $"delete-character-confirmation account={deleteSession.AccountName} slot={deleteCharacter.Slot}");
                            Console.WriteLine($"Character deleted: account={deleteSession.AccountName}, slot={deleteCharacter.Slot}.");
                        }
                        else if (outcome.Status == DeleteCharacterStatus.SecureNotVerified)
                        {
                            // CFileDB.cpp:1320-1325 silently breaks when SecurePass is not verified.
                            Console.WriteLine($"Character deletion rejected: account={deleteSession.AccountName}, slot={deleteCharacter.Slot}, reason={outcome.Status}.");
                        }
                        else
                        {
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, DeleteCharacterFailSignal.ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256)), cancellationToken, $"delete-character-failure account={deleteSession.AccountName} slot={deleteCharacter.Slot} reason={outcome.Status}");
                            Console.WriteLine($"Character deletion rejected: account={deleteSession.AccountName}, slot={deleteCharacter.Slot}, reason={outcome.Status}.");
                        }
                        if (sessions.TryGet(connectionId, out var deleteCompletedSession) && deleteCompletedSession!.State == LoginSessionState.CharacterWait)
                            sessions.CompleteCharacterRefresh(connectionId);
                        continue;
                    }

                    if (AccountSecureRequest.TryParse(frame, out var accountSecure) && accountSecure is not null)
                    {
                        if (accounts is null || !sessions.TryGet(connectionId, out var secureSession) || secureSession!.AccountName is null)
                        {
                            Console.WriteLine("Ignored account-secure frame: no account associated with this connection yet.");
                            continue;
                        }

                        var outcome = await new AccountSecureCoordinator(sessions, accounts).HandleAsync(connectionId, secureSession.AccountName, accountSecure, cancellationToken);
                        switch (outcome.Status)
                        {
                            case AccountSecureStatus.Success:
                                await WriteLoggedFrameAsync(stream, serverLog, connectionId, AccountSecureSignal.Success(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256)), cancellationToken, "account-secure-success");
                                Console.WriteLine($"Account-secure accepted: account={secureSession.AccountName}, change={accountSecure.ChangeNumeric}.");
                                break;
                            case AccountSecureStatus.Fail:
                                await WriteLoggedFrameAsync(stream, serverLog, connectionId, AccountSecureSignal.Fail(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256)), cancellationToken, "account-secure-failure");
                                Console.WriteLine($"Account-secure rejected: account={secureSession.AccountName}, change={accountSecure.ChangeNumeric}, reason={outcome.Status}.");
                                break;
                            default:
                                // ChangeWithoutVerification/AccountNotFound send nothing at all (CFileDB.cpp:1391's silent break).
                                Console.WriteLine($"Account-secure rejected: account={secureSession.AccountName}, change={accountSecure.ChangeNumeric}, reason={outcome.Status}.");
                                break;
                        }
                        continue;
                    }

                    if (CharacterLoginRequest.TryParse(frame, out var characterLogin) && characterLogin is not null)
                    {
                        if (accounts is null || !sessions.TryGet(connectionId, out var loginSession) || loginSession!.State != LoginSessionState.CharacterSelection)
                        {
                            // The reference handler sends a "Wait a moment." chat message here; this port has no generic
                            // chat-message wire message yet, and a normal client cannot reach this path (see CharacterLoginCoordinator).
                            Console.WriteLine("Rejected character-login frame: session is not in character selection.");
                            continue;
                        }

                        if (sessions.BeginCharacterWait(connectionId) != LoginTransitionResult.Accepted)
                        {
                            Console.WriteLine("Rejected character-login frame: could not enter character wait state.");
                            continue;
                        }

                        var outcome = await new CharacterLoginCoordinator(accounts).HandleAsync(loginSession.AccountName!, characterLogin, loginSession.SecureVerified, cancellationToken);
                        if (outcome.IsSuccess)
                        {
                            LegacyCharacterLoginData data;
                            short classMaster;
                            (short X, short Y) loginPosition;
                            byte[] loginResponse;
                            byte[] spawnFrame;
                            byte[] etcFrame;
                            byte[] scoreFrame;
                            byte[]? loginAffectFrame;
                            byte[] pkInfoFrame;
                            int? sourceMobMagic = null;
                            try
                            {
                                if (itemData is null)
                                    throw new InvalidOperationException("Authenticated character login requires a loaded ItemList.");

                                var sourceCharacterData = outcome.Data!;
                                sourceMobMagic = LegacyMobCombatState.Read(sourceCharacterData.Mob).Magic;
                                data = LegacyCurrentScoreLoginProjector.Project(sourceCharacterData, itemData);
                                classMaster = data.MobExtra.Length >= sizeof(short)
                                    ? BinaryPrimitives.ReadInt16LittleEndian(data.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset))
                                    : (short)LegacyAccountSnapshot.ClassMasterMortal;
                                loginPosition = world.ResolveCharacterLoginPosition(connectionId, data.Mob, classMaster);
                                var codec = LegacyFrameCodec.CreateDefault();
                                loginResponse = W2ppCharacterLoginV1Adapter.Adapt(
                                        data,
                                        loginPosition.X,
                                        loginPosition.Y,
                                        (ushort)characterLogin.Slot,
                                        (ushort)connectionId,
                                        weather: 0)
                                    .ToFrame(codec, unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
                                spawnFrame = BuildClientV769CreateMobFrame(
                                    (ushort)connectionId,
                                    loginPosition.X,
                                    loginPosition.Y,
                                    data.Mob,
                                    data.Affect,
                                    npc: false,
                                    battleRoyaleState: world.GetCombatWorldState().BattleRoyaleState,
                                    clientEquipment: data.ClientEquipment);
                                etcFrame = BuildClientV769UpdateEtcFrame(data.Mob, data.MobExtra, (ushort)connectionId);
                                scoreFrame = BuildClientV769UpdateScoreFrame(data.Mob, data.Affect, (ushort)connectionId);
                                _ = TryBuildClientV769UpdateAffectFrame(
                                    data.Affect,
                                    (ushort)connectionId,
                                    unchecked((uint)Environment.TickCount64),
                                    (byte)RandomNumberGenerator.GetInt32(256),
                                    out loginAffectFrame);
                                pkInfoFrame = new PkInfoConfirmation(connectionId, state: 0)
                                    .ToFrame(codec, unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
                            }
                            catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException)
                            {
                                sessions.CompleteCharacterRefresh(connectionId);
                                Console.WriteLine($"Character login rejected: account={loginSession.AccountName}, slot={characterLogin.Slot}, reason=CurrentScoreProjectionFailed, error={error.GetType().Name}.");
                                continue;
                            }

                            var donate = data.Donate;
                            // The character-login payload already carries the legacy account Donate field
                            // from the world blob. Only override it when the explicit Donate DB adapter was
                            // configured; the regular MariaDB account/world schema may not yet have the
                            // optional accounts.donate column.
                            if (donateBalances is not null && explicitDonateDatabase)
                            {
                                var persistedDonate = await donateBalances.ReadDonateAsync(loginSession.AccountName!, cancellationToken);
                                if (persistedDonate is not null)
                                    donate = persistedDonate.Value;
                            }

                            if (autoTradeStateStore is not null)
                            {
                                try
                                {
                                    var reconnectPlan = await LegacyAutoTradeReconnectCoordinator.CloseAndRecoverAsync(
                                        sessions,
                                        world,
                                        autoTradeBook,
                                        autoTradeStateStore,
                                        loginSession.AccountName!,
                                        characterLogin.Slot,
                                        LegacyFrameCodec.CreateDefault(),
                                        unchecked((uint)Environment.TickCount64),
                                        (byte)RandomNumberGenerator.GetInt32(256),
                                        cancellationToken);
                                    // Captured under the saved pair leases, released before sockets.
                                    foreach (var framePlan in reconnectPlan.RecoveryFrames)
                                        foreach (var recipient in framePlan.RecipientIds)
                                            await world.SendAsync(recipient, framePlan.Frame, cancellationToken);
                                    var reconnect = reconnectPlan.Outcome;
                                    if (reconnect.Result == LegacyAutoTradeReconnectResult.PersistenceUnavailable)
                                    {
                                        sessions.CompleteCharacterRefresh(connectionId);
                                        Console.WriteLine($"Character login rejected: account={loginSession.AccountName}, slot={characterLogin.Slot}, reason=AutotradeReconnectPersistenceUnavailable.");
                                        continue;
                                    }

                                    foreach (var closedListing in reconnect.ClosedListings)
                                        await BroadcastClosedAutoTradeRemovalAsync(world, closedListing, cancellationToken);

                                    if (reconnect.Result == LegacyAutoTradeReconnectResult.Closed)
                                    {
                                        Console.WriteLine($"Persisted autotrade closed on character login: account={loginSession.AccountName}, slot={characterLogin.Slot}, listings={reconnect.ClosedListings.Count}.");
                                    }
                                }
                                catch (Exception error)
                                {
                                    sessions.CompleteCharacterRefresh(connectionId);
                                    Console.WriteLine($"Character login rejected: account={loginSession.AccountName}, slot={characterLogin.Slot}, reason=AutotradeReconnectFailed, error={error.GetType().Name}: {error.Message}.");
                                    continue;
                                }
                            }

                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, loginResponse, cancellationToken, $"character-login-confirmation account={loginSession.AccountName} slot={characterLogin.Slot} saved=({data.SavedPositionX},{data.SavedPositionY}) live=({loginPosition.X},{loginPosition.Y})");
                            sessions.CompleteCharacterLogin(connectionId, characterLogin.Slot, loginPosition.X, loginPosition.Y);
                            await world.EnterAsync(connectionId, loginSession.AccountName!, (frame, token) => WriteLoggedFrameAsync(stream, serverLog, connectionId, frame, token, "world-enter"), spawnFrame, cancellationToken);
                            foreach (var npc in world.GetNpcSnapshots())
                            {
                                byte[] npcFrame;
                                var npcLabel = "npc-spawn";
                                if (npc.AutoTradeSnapshot is not null &&
                                    LegacyAutoTradeVisualRelay.TryBuild(
                                        npc.AutoTradeSnapshot,
                                        npc.MobSnapshot,
                                        npc.AffectSnapshot,
                                        LegacyFrameCodec.CreateDefault(),
                                        unchecked((uint)Environment.TickCount64),
                                        (byte)RandomNumberGenerator.GetInt32(256),
                                        out var autoTradeVisual) &&
                                    autoTradeVisual is not null)
                                {
                                    npcFrame = autoTradeVisual.Frame;
                                    npcLabel = "autotrade-spawn";
                                }
                                else
                                {
                                    npcFrame = BuildClientV769CreateMobFrame(
                                        (ushort)npc.ConnectionId,
                                        npc.PositionX,
                                        npc.PositionY,
                                        npc.MobSnapshot,
                                        npc.AffectSnapshot,
                                        npc: true,
                                        battleRoyaleState: world.GetCombatWorldState().BattleRoyaleState);
                                }

                                await WriteLoggedFrameAsync(stream, serverLog, connectionId, npcFrame, cancellationToken, $"{npcLabel} npc={npc.ConnectionId}");
                            }
                            foreach (var mapItem in world.GetMapItemStates())
                            {
                                var mapItemFrame = new CreateItemConfirmation(
                                        checked((ushort)mapItem.PositionX),
                                        checked((ushort)mapItem.PositionY),
                                        checked((ushort)(mapItem.ItemId + LegacyMapItemStateCodes.WireIdOffset)),
                                        mapItem.Item,
                                        mapItem.Rotate,
                                        checked((byte)mapItem.State),
                                        checked((byte)Math.Clamp(mapItem.Height, 0, byte.MaxValue)))
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
                                await WriteLoggedFrameAsync(stream, serverLog, connectionId, mapItemFrame, cancellationToken, $"map-item-spawn item={mapItem.ItemId}");
                            }
                            world.SetCharacterState(
                                connectionId, characterLogin.Slot,
                                BinaryPrimitives.ReadUInt16LittleEndian(data.Mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset)),
                                data.Mob[LegacyAccountSnapshot.MobClanOffset],
                                data.Mob[LegacyAccountSnapshot.MobGuildLevelOffset],
                                 BinaryPrimitives.ReadInt32LittleEndian(data.Mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset)),
                                 loginPosition.X, loginPosition.Y, data.Mob,
                                 classMaster,
                                 data.Affect,
                                 data.MobExtra,
                                 data.ClientEquipment,
                                data.PursuitBaseResistances ?? Array.Empty<byte>(),
                                sourceMobMagic);
                            world.SetDonateBalance(connectionId, donate);
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, etcFrame, cancellationToken, "character-update-etc");
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, scoreFrame, cancellationToken, "character-update-score");
                            if (loginAffectFrame is not null)
                                await WriteLoggedFrameAsync(stream, serverLog, connectionId, loginAffectFrame, cancellationToken, "character-update-affect");
                            else
                                Console.WriteLine($"Character login affect relay skipped: account={loginSession.AccountName}, reason=7.69-narrowing");
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, pkInfoFrame, cancellationToken, "pk-info state=0");
                            if (world.TryGetDonateBalance(connectionId, out var loginDonate))
                            {
                                var loginPix = await ReadDonatePixAsync(donateBalances, loginSession.AccountName!, cancellationToken);
                                var balanceFrame = new DonateBalanceConfirmation(loginDonate, loginPix)
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                await WriteLoggedFrameAsync(stream, serverLog, connectionId, balanceFrame, cancellationToken, "donate-balance");
                            }
                            Console.WriteLine($"Character login accepted: account={loginSession.AccountName}, slot={characterLogin.Slot}.");
                        }
                        else
                        {
                            // The reference handler sends no response at all on these rejections (CFileDB.cpp logs and returns) - match that silence.
                            sessions.CompleteCharacterRefresh(connectionId);
                            Console.WriteLine($"Character login rejected: account={loginSession.AccountName}, slot={characterLogin.Slot}, reason={outcome.Status}.");
                        }
                        continue;
                    }

                    if (DonateShopOpenRequest.TryParse(frame, out var donateShopOpen) && donateShopOpen is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var donateOpenSession) || donateOpenSession!.State != LoginSessionState.Playing || donateOpenSession.AccountName is null)
                        {
                            Console.WriteLine("Rejected Donate Shop open request: session is not in USER_PLAY.");
                            continue;
                        }

                        // ClientPatch_v759 uses npcID=1000/Warp=100 as the internal Donate Shop entry point.
                        // The native bridge will open the visual window; the server answers the same request
                        // with the authoritative balance and the complete 3x5x15 catalog.
                        if (donateShopOpen.Target == 1000 && donateShopOpen.Warp == 100)
                        {
                            if (!donateShopRateLimiter.TryAccept(LegacyDonateShopRequestKind.Open, Environment.TickCount64))
                            {
                                Console.WriteLine($"Donate Shop open throttled: account={donateOpenSession.AccountName}.");
                                continue;
                            }
                            var codec = LegacyFrameCodec.CreateDefault();
                            if (world.TryGetDonateBalance(connectionId, out var openDonate))
                            {
                                var openPix = await ReadDonatePixAsync(donateBalances, donateOpenSession.AccountName, cancellationToken);
                                var balanceFrame = new DonateBalanceConfirmation(openDonate, openPix)
                                    .ToFrame(codec, frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                await stream.WriteAsync(balanceFrame, cancellationToken);
                            }

                            if (world.TryGetDonateShopCatalog(out var openCatalog) && openCatalog is not null)
                            {
                                var catalogFrame = openCatalog.ToConfirmation()
                                    .ToFrame(codec, frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                await stream.WriteAsync(catalogFrame, cancellationToken);
                                Console.WriteLine($"Donate Shop opened: account={donateOpenSession.AccountName}, target={donateShopOpen.Target}, warp={donateShopOpen.Warp}.");
                            }
                            else
                                Console.WriteLine($"Donate Shop open ignored: account={donateOpenSession.AccountName}; catalog is not configured.");
                        }
                        else
                            Console.WriteLine($"Donate Shop open ignored: account={donateOpenSession.AccountName}, target={donateShopOpen.Target}, warp={donateShopOpen.Warp}.");
                        continue;
                    }

                    if (RetailNpcShopRequest.TryParse(frame, out var retailNpcShop) && retailNpcShop is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var retailNpcSession) || retailNpcSession!.State != LoginSessionState.Playing || retailNpcSession.AccountName is null)
                        {
                            Console.WriteLine("Rejected retail NPC shop request: session is not in USER_PLAY.");
                            continue;
                        }

                        // The legacy bridge deliberately uses virtual target
                        // 1000. Retail 7.60 sends the real NPC connection ID;
                        // only an already registered explicit Donation Store
                        // NPC may use that path. Other merchant targets stay
                        // on the ordinary shop flow.
                        var isDonateNpc = world.IsDonateShopNpc(retailNpcShop.Target);
                        if (retailNpcShop.Target == 1000 || isDonateNpc)
                        {
                            if (!donateShopRateLimiter.TryAccept(LegacyDonateShopRequestKind.Open, Environment.TickCount64))
                            {
                                Console.WriteLine($"Donate Shop compact NPC open throttled: account={retailNpcSession.AccountName}.");
                                continue;
                            }

                            var codec = LegacyFrameCodec.CreateDefault();
                            if (world.TryGetDonateBalance(connectionId, out var retailOpenDonate))
                            {
                                var retailOpenPix = await ReadDonatePixAsync(donateBalances, retailNpcSession.AccountName, cancellationToken);
                                var balanceFrame = new DonateBalanceConfirmation(retailOpenDonate, retailOpenPix)
                                    .ToFrame(codec, frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                await stream.WriteAsync(balanceFrame, cancellationToken);
                            }

                            if (world.TryGetDonateShopCatalog(out var retailCatalog) && retailCatalog is not null)
                            {
                                var catalogFrame = retailCatalog.ToConfirmation()
                                    .ToFrame(codec, frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                await stream.WriteAsync(catalogFrame, cancellationToken);
                                var targetKind = isDonateNpc ? "registered Donation Store NPC" : "virtual bridge";
                                Console.WriteLine($"Donate Shop opened from compact 7.60 NPC request: account={retailNpcSession.AccountName}, target={retailNpcShop.Target}, kind={targetKind}.");
                            }
                            else
                                Console.WriteLine($"Donate Shop compact NPC open ignored: account={retailNpcSession.AccountName}; catalog is not configured.");
                        }
                        else
                            Console.WriteLine($"Retail NPC shop request ignored: account={retailNpcSession.AccountName}, target={retailNpcShop.Target}.");
                        continue;
                    }

                    if (DonateShopCatalogRequest.TryParse(frame, out var donateCatalogRequest) && donateCatalogRequest is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var donateCatalogSession) || donateCatalogSession!.State != LoginSessionState.Playing || donateCatalogSession.AccountName is null)
                        {
                            Console.WriteLine("Rejected Donate Shop catalog request: session is not in USER_PLAY.");
                            continue;
                        }

                        // ClientPatch_v759 maps ReqAlias(1) to UpdateDonate and
                        // ReqAlias(2) to SendShopDonate (the full 3x5x15 matrix).
                        if (donateCatalogRequest.Kind == 1)
                        {
                            if (!donateShopRateLimiter.TryAccept(LegacyDonateShopRequestKind.Balance, Environment.TickCount64))
                            {
                                Console.WriteLine($"Donate balance refresh throttled: account={donateCatalogSession.AccountName}.");
                                continue;
                            }
                            if (world.TryGetDonateBalance(connectionId, out var refreshedDonate))
                            {
                                var refreshedPix = await ReadDonatePixAsync(donateBalances, donateCatalogSession.AccountName, cancellationToken);
                                var balanceFrame = new DonateBalanceConfirmation(refreshedDonate, refreshedPix)
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                await stream.WriteAsync(balanceFrame, cancellationToken);
                            }
                            Console.WriteLine($"Donate balance sent: account={donateCatalogSession.AccountName}.");
                        }
                        else if (donateCatalogRequest.Kind == 2 && world.TryGetDonateShopCatalog(out var requestedCatalog) && requestedCatalog is not null)
                        {
                            if (!donateShopRateLimiter.TryAccept(LegacyDonateShopRequestKind.Catalog, Environment.TickCount64))
                            {
                                Console.WriteLine($"Donate Shop catalog throttled: account={donateCatalogSession.AccountName}.");
                                continue;
                            }
                            var catalogFrame = requestedCatalog.ToConfirmation()
                                .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                            await stream.WriteAsync(catalogFrame, cancellationToken);
                            if (world.TryGetDonateBalance(connectionId, out var requestedDonate))
                            {
                                var requestedPix = await ReadDonatePixAsync(donateBalances, donateCatalogSession.AccountName, cancellationToken);
                                var balanceFrame = new DonateBalanceConfirmation(requestedDonate, requestedPix)
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                await stream.WriteAsync(balanceFrame, cancellationToken);
                            }
                            Console.WriteLine($"Donate Shop catalog sent: account={donateCatalogSession.AccountName}.");
                        }
                        else
                            Console.WriteLine($"Donate Shop catalog ignored: account={donateCatalogSession.AccountName}, kind={donateCatalogRequest.Kind}.");
                        continue;
                    }

                    if (DonatePurchaseRequest.TryParse(frame, out var donatePurchase) && donatePurchase is not null)
                    {
                        if (donateBalances is null || !sessions.TryGet(connectionId, out var donateSession) || donateSession!.State != LoginSessionState.Playing || donateSession.AccountName is null)
                        {
                            Console.WriteLine("Rejected Donate purchase: session is not in USER_PLAY or account storage is disabled.");
                            continue;
                        }

                        if (!donateShopRateLimiter.TryAccept(LegacyDonateShopRequestKind.Purchase, Environment.TickCount64))
                        {
                            var throttledFrame = new MessagePanelConfirmation(LegacyDonateShopMessages.Throttled)
                                .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256));
                            await stream.WriteAsync(throttledFrame, cancellationToken);
                            Console.WriteLine($"Donate purchase throttled: account={donateSession.AccountName}.");
                            continue;
                        }

                        var purchaseResult = world.TryPurchaseDonateItem(connectionId, donatePurchase, out var purchaseOutcome);
                        if (purchaseResult == LegacyDonatePurchaseResult.Accepted && purchaseOutcome is not null)
                        {
                            var saveResult = await donateBalances.TrySaveDonateAsync(donateSession.AccountName, purchaseOutcome.RemainingDonate, purchaseOutcome.PreviousDonate, cancellationToken);
                            if (saveResult != DonateBalanceSaveResult.Success)
                            {
                                var rolledBack = world.TryRollbackDonatePurchase(connectionId, purchaseOutcome);
                                Console.WriteLine($"Donate purchase persistence failed: account={donateSession.AccountName}, item={purchaseOutcome.ItemIndex}, price={purchaseOutcome.TotalPrice}, save={saveResult}, rollback={rolledBack}.");
                                continue;
                            }
                            var codec = LegacyFrameCodec.CreateDefault();
                            foreach (var drop in purchaseOutcome.ItemDrops)
                            {
                                var itemFrame = new SendItemConfirmation(1, checked((short)drop.InventorySlot), drop.Item)
                                    .ToFrame(codec, frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                await stream.WriteAsync(itemFrame, cancellationToken);
                            }
                            var itemName = itemData?[purchaseOutcome.ItemIndex]?.Name ?? $"#{purchaseOutcome.ItemIndex}";
                            var purchaseNotice = new MessagePanelConfirmation(
                                    LegacyDonateShopMessages.PurchaseAccepted(purchaseOutcome.Quantity, itemName, purchaseOutcome.TotalPrice))
                                .ToFrame(codec, frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256));
                            await stream.WriteAsync(purchaseNotice, cancellationToken);
                            var purchasePix = await ReadDonatePixAsync(donateBalances, donateSession.AccountName, cancellationToken);
                            var balanceFrame = new DonateBalanceConfirmation(purchaseOutcome.RemainingDonate, purchasePix)
                                .ToFrame(codec, frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                            await stream.WriteAsync(balanceFrame, cancellationToken);
                            Console.WriteLine($"Donate purchase accepted: account={donateSession.AccountName}, item={purchaseOutcome.ItemIndex}, quantity={purchaseOutcome.Quantity}, price={purchaseOutcome.TotalPrice}, remaining={purchaseOutcome.RemainingDonate}, save={saveResult}.");
                        }
                        else
                        {
                            var rejectionMessage = LegacyDonateShopMessages.ForRejectedPurchase(purchaseResult);
                            if (rejectionMessage is not null)
                            {
                                var rejectionFrame = new MessagePanelConfirmation(rejectionMessage)
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256));
                                await stream.WriteAsync(rejectionFrame, cancellationToken);
                            }
                            Console.WriteLine($"Donate purchase rejected: account={donateSession.AccountName}, store={donatePurchase.Store}, page={donatePurchase.Page}, item={donatePurchase.ItemPosition}, quantity={donatePurchase.Quantity}, reason={purchaseResult}.");
                        }
                        continue;
                    }

                    if (ShopSellMessage.TryParse(frame, out var sellRequest) && sellRequest is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var sellSession) || sellSession is null)
                        {
                            Console.WriteLine($"NPC sale rejected: connection={connectionId}, reason=SessionUnavailable.");
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(sellSession.AccountName))
                        {
                            Console.WriteLine($"NPC sale rejected: connection={connectionId}, reason=AccountIdentityUnavailable.");
                            continue;
                        }

                        await using var sellAccountLease = await accountOperationGate.AcquireAsync(
                            [sellSession.AccountName], cancellationToken);

                        var preparation = await LegacyNpcSellPreparationCoordinator.PrepareAsync(
                            sellSession,
                            sellRequest,
                            world,
                            autoTradeBook,
                            accounts as IAccountSnapshotStore,
                            itemData,
                            cancellationToken,
                            cargoRuntime: accountCargoRuntime);
                        if (preparation.Status == LegacyNpcSellPreparationStatus.CargoStateReconciled &&
                            preparation.State is { CargoSnapshot: { } reconciledItems } reconciledState &&
                            reconciledState.CargoRevision is { } reconciledRevision)
                        {
                            var reconciledCargo = new LegacyAccountCargoSnapshot(
                                sellSession.AccountName,
                                reconciledRevision,
                                reconciledItems,
                                WasReconciled: true)
                            {
                                ChangedPositions = reconciledState.CargoChangedPositions,
                            };
                            await sellAccountLease.DisposeAsync();
                            await BroadcastCargoResyncAsync(reconciledCargo, sessions, world, cancellationToken);
                            Console.WriteLine($"NPC sale rejected after Cargo reconciliation: account={sellSession.AccountName}, changed={reconciledCargo.ChangedPositions.Count}.");
                            continue;
                        }
                        if (preparation.Status == LegacyNpcSellPreparationStatus.CargoOwnerUnavailable)
                        {
                            Console.WriteLine($"NPC sale rejected: account={sellSession.AccountName}, reason=CargoOwnerUnavailable.");
                            continue;
                        }
                        if (preparation.Flow?.Stage == LegacyNpcSellFlowStage.HpModeRejected)
                        {
                            await sellAccountLease.DisposeAsync();
                            var hp = world.TryGetResourceState(connectionId, out var sellResources, out _, out _) && sellResources is not null
                                ? sellResources.CurrentScore.Hp
                                : 0;
                            var hpModeFrame = new SetHpModeConfirmation(hp, (short)sellSession.State)
                                .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, checked((ushort)connectionId));
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, hpModeFrame, cancellationToken, "npc-sell-hp-mode-rejected");
                            await RecordCrackErrorAsync(
                                LegacyCrackErrorRules.NpcSellHpModePoints,
                                LegacyCrackErrorRules.NpcSellHpModeType);
                            continue;
                        }

                        if (preparation.Flow?.Stage == LegacyNpcSellFlowStage.TradeCancellationRequired)
                        {
                            await sellAccountLease.DisposeAsync();
                            var tradeClose = await LegacyAutoTradeCloseCoordinator.CloseAndCaptureAsync(
                                connectionId,
                                sessions,
                                world,
                                autoTradeBook,
                                autoTradeStateStore,
                                LegacyFrameCodec.CreateDefault(),
                                frame.Header.ClientTick,
                                frame.Header.KeywordIndex,
                                cancellationToken);
                            foreach (var closeFrame in tradeClose.Frames)
                                foreach (var recipientId in closeFrame.RecipientIds)
                                    await world.SendAsync(recipientId, closeFrame.Frame, cancellationToken);
                            Console.WriteLine($"NPC sale cancelled active trade: account={sellSession.AccountName}, result={tradeClose.Outcome.Result}.");
                            continue;
                        }

                        if (preparation.Flow?.Stage == LegacyNpcSellFlowStage.AccountBlocked)
                        {
                            await sellAccountLease.DisposeAsync();
                            var notice = new MessagePanelConfirmation("Sua conta está com o bloqueio ativo para essa operação.")
                                .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256));
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, notice, cancellationToken, "npc-sell-account-blocked");
                            continue;
                        }

                        if (preparation.Flow?.Stage == LegacyNpcSellFlowStage.TargetRejected &&
                            preparation.Flow.Target?.Status == LegacyNpcSellTargetStatus.TargetNotInView)
                        {
                            await sellAccountLease.DisposeAsync();
                            var closeShop = CloseShopSignal.ToFrame(LegacyFrameCodec.CreateDefault());
                            await WriteLoggedFrameAsync(
                                stream, serverLog, connectionId, closeShop, cancellationToken, "npc-sell-target-out-of-view");
                            continue;
                        }

                        var sellResyncCodec = LegacyFrameCodec.CreateDefault();
                        if (LegacyNpcSellCarryResync.TryBuildFrame(
                                preparation,
                                checked((ushort)connectionId),
                                sellResyncCodec,
                                out var carryResyncFrame) && carryResyncFrame is not null)
                        {
                            await sellAccountLease.DisposeAsync();
                            await WriteLoggedFrameAsync(
                                stream, serverLog, connectionId, carryResyncFrame, cancellationToken, "npc-sell-empty-carry-resync");
                            continue;
                        }

                        if (LegacyNpcSellRejectionNoticeMapper.For(preparation) is { } rejectionMessage)
                        {
                            await sellAccountLease.DisposeAsync();
                            // SendClientMsg zero-initializes MSG_MessagePanel and sets ID=0 in both 7.69 and W2PP.
                            var rejectionNotice = new MessagePanelConfirmation(rejectionMessage)
                                .ToFrame(LegacyFrameCodec.CreateDefault(), clientTick: 0, keywordIndex: 0);
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, rejectionNotice, cancellationToken, "npc-sell-localized-rejection");
                            continue;
                        }

                        if (preparation.Status != LegacyNpcSellPreparationStatus.Evaluated ||
                            preparation.Flow is not { Stage: LegacyNpcSellFlowStage.ReadyForSaleMutation })
                        {
                            Console.WriteLine($"NPC sale rejected: account={sellSession.AccountName}, status={preparation.Status}, stage={preparation.Flow?.Stage}.");
                            continue;
                        }

                        if (!world.TryGetRecycleBinSnapshot(connectionId, out var recycleBin) || recycleBin is null)
                        {
                            Console.WriteLine($"NPC sale rejected: account={sellSession.AccountName}, reason=RecycleBinUnavailable.");
                            continue;
                        }

                        var completionStatus = LegacyNpcSellCompletionRules.TryBuild(
                            preparation,
                            connectionId,
                            recycleBin,
                            unchecked((uint)RandomNumberGenerator.GetInt32(int.MaxValue)),
                            out var completionPlan);
                        if (completionStatus != LegacyNpcSellCompletionBuildStatus.Ready || completionPlan is null)
                        {
                            Console.WriteLine($"NPC sale rejected: account={sellSession.AccountName}, completion={completionStatus}.");
                            continue;
                        }

                        LegacyNpcCargoSaleRuntimeCoordinator? cargoSaleCoordinator = null;
                        LegacyNpcCargoSaleRuntimeCommitOutcome? cargoCommitOutcome = null;
                        if (completionPlan.Source.Kind == LegacyNpcSellSourceKind.Cargo)
                        {
                            cargoSaleCoordinator = new LegacyNpcCargoSaleRuntimeCoordinator(
                                accounts as IAccountSnapshotStore,
                                accounts as IAtomicNpcCargoSaleStore,
                                accountCargoRuntime,
                                accountOperationGate);
                            cargoCommitOutcome = await cargoSaleCoordinator.CommitAsync(
                                completionPlan,
                                sellSession.AccountName,
                                sellSession.CharacterSlot,
                                cancellationToken);
                            if (cargoCommitOutcome.Status != LegacyNpcCargoSaleRuntimeCommitStatus.Committed)
                            {
                                if (cargoCommitOutcome.Status == LegacyNpcCargoSaleRuntimeCommitStatus.RecoveryRequired)
                                {
                                    Console.Error.WriteLine($"NPC Cargo sale requires account recovery; closing session: account={sellSession.AccountName}.");
                                    return;
                                }

                                if (cargoCommitOutcome.CargoSnapshot is { ChangedPositions.Count: > 0 } cargoConflict)
                                {
                                    await sellAccountLease.DisposeAsync();
                                    await BroadcastCargoResyncAsync(cargoConflict, sessions, world, cancellationToken);
                                }
                                Console.WriteLine($"NPC Cargo sale rejected before runtime commit: account={sellSession.AccountName}, persistence={cargoCommitOutcome.Status}.");
                                continue;
                            }
                        }

                        var commitStatus = world.TryCommitNpcSell(completionPlan, out var completion);
                        if (commitStatus != LegacyNpcSellCommitResult.Committed || completion is null)
                        {
                            if (cargoCommitOutcome is not null &&
                                !await cargoSaleCoordinator!.TryCompensateAsync(cargoCommitOutcome, CancellationToken.None))
                            {
                                Console.Error.WriteLine($"NPC Cargo sale persistence could not be compensated; closing session: account={sellSession.AccountName}.");
                                return;
                            }
                            Console.WriteLine($"NPC sale commit rejected: account={sellSession.AccountName}, result={commitStatus}.");
                            continue;
                        }

                        var codec = LegacyFrameCodec.CreateDefault();
                        if (LegacyNpcSellResponsePlanner.TryBuild(world, frame, completion, codec, out var responsePlan) !=
                                LegacyNpcSellResponseBuildResult.Built || responsePlan is null)
                        {
                            var rolledBack = world.TryRollbackNpcSell(completion);
                            if (cargoCommitOutcome is not null &&
                                (!rolledBack || !await cargoSaleCoordinator!.TryCompensateAsync(cargoCommitOutcome, CancellationToken.None)))
                            {
                                accountOperationGate.MarkRecoveryRequired(sellSession.AccountName, "NPC Cargo response construction failed after commit");
                                Console.Error.WriteLine($"NPC Cargo sale response recovery is uncertain; closing session: account={sellSession.AccountName}.");
                                return;
                            }
                            Console.WriteLine($"NPC sale rolled back: account={sellSession.AccountName}, reason=ResponsePlanUnavailable, runtimeRollback={rolledBack}.");
                            continue;
                        }

                        await sellAccountLease.DisposeAsync();

                        foreach (var responseStep in responsePlan.Steps)
                        {
                            if (responseStep.Destination == LegacyNpcSellResponseDestination.Seller)
                            {
                                await WriteLoggedFrameAsync(
                                    stream,
                                    serverLog,
                                    connectionId,
                                    responseStep.Frame,
                                    cancellationToken,
                                    $"npc-sell-response type=0x{codec.Decode(responseStep.Frame).Header.Type:X4}");
                                continue;
                            }

                            foreach (var recipientId in responseStep.RecipientConnectionIds)
                                await world.SendAsync(recipientId, responseStep.Frame, cancellationToken);
                        }

                        if (cargoCommitOutcome?.CargoSnapshot is { ChangedPositions.Count: > 0 } committedCargo)
                            await BroadcastCargoResyncAsync(committedCargo, sessions, world, cancellationToken);

                        Console.WriteLine($"NPC sale committed: account={sellSession.AccountName}, item={completion.Plan.Sale.Item.Index}, source={completion.Plan.Source.Kind}:{completion.Plan.Source.Position}, gold={completion.Plan.Sale.NetPrice}, city={completion.Plan.Sale.NpcVillage}.");
                        continue;
                    }

                    if (RepurchaseItemsMessage.TryParse(frame, out var recycleRequest) && recycleRequest is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var recycleSession))
                        {
                            Console.WriteLine($"Rejected recycle-bin request: connection={connectionId}, reason=SessionUnavailable.");
                            continue;
                        }

                        var recycleResult = LegacyRecycleBinRequestHandler.TryCreateResponse(
                            world, connectionId, frame.Header.Id, recycleSession!.State, recycleRequest, out var recycleResponse);
                        if (recycleResult == LegacyRecycleBinRequestResult.HeaderIdMismatch)
                        {
                            serverLog?.Write($"REJECT recycle-bin request connection={connectionId} reason=HeaderIdMismatch headerId={frame.Header.Id}");
                            Console.WriteLine($"Rejected recycle-bin request: connection={connectionId}, reason=HeaderIdMismatch.");
                            return;
                        }

                        if (recycleResult != LegacyRecycleBinRequestResult.Ready || recycleResponse is null)
                        {
                            Console.WriteLine($"Rejected recycle-bin request: connection={connectionId}, reason=SessionUnavailable.");
                            continue;
                        }

                        var recycleResponseFrame = recycleResponse.ToFrame(
                            LegacyFrameCodec.CreateDefault(),
                            frame.Header.ClientTick,
                            frame.Header.KeywordIndex,
                            frame.Header.Id);
                        await WriteLoggedFrameAsync(
                            stream, serverLog, connectionId, recycleResponseFrame, cancellationToken, "repurchase-items");
                        continue;
                    }

                    if (TinyCombineRequest.TryParse(frame, out var tinyCombineRequest) && tinyCombineRequest is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var tinyCombineSession) || tinyCombineSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine("Rejected Tiny combine frame: session is not in USER_PLAY.");
                            continue;
                        }
                        if (tinyCombineSession.AccountName is null)
                        {
                            Console.WriteLine("Rejected Tiny combine frame: account identity is unavailable.");
                            continue;
                        }

                        var tinyDispatch = await LegacyTinyCombineDispatchCoordinator.ExecuteAsync(
                            accountOperationGate,
                            tinyCombineSession.AccountName,
                            world,
                            connectionId,
                            tinyCombineRequest.Items,
                            tinyCombineRequest.InventoryPositions,
                            tinyChanceBase,
                            RandomNumberGenerator.GetInt32(115),
                            localServer: false,
                            (outcome, _) =>
                            {
                                var codec = LegacyFrameCodec.CreateDefault();
                                if (!world.TryBuildUpdateEtcFrame(connectionId, codec, frame.Header.ClientTick,
                                        frame.Header.KeywordIndex, out var updateEtcFrame) || updateEtcFrame is null ||
                                    !LegacyTinyCombineRelay.TryBuild(
                                        outcome,
                                        updateEtcFrame,
                                        codec,
                                        frame.Header.ClientTick,
                                        frame.Header.KeywordIndex,
                                        checked((ushort)connectionId),
                                        LegacyTinyCombineRelay.ProcessingCompleteMessage,
                                        LegacyTinyCombineRelay.CombineFailedMessage,
                                        out var plannedFrames) || plannedFrames is null)
                                    return ValueTask.FromResult<IReadOnlyList<byte[]>?>(null);

                                return ValueTask.FromResult<IReadOnlyList<byte[]>?>(plannedFrames);
                            },
                            cancellationToken);

                        var prepareResult = tinyDispatch.PreparationResult;
                        var applyResult = tinyDispatch.ApplicationResult;
                        var tinyCombineOutcome = tinyDispatch.Outcome;
                        var tinyCombineFrames = tinyDispatch.ResponseFrames;
                        if (tinyDispatch.ResponsePlanningFailed && tinyCombineOutcome is not null)
                        {
                            Console.WriteLine($"Tiny combine rolled back: response planning failed; account={tinyCombineSession.AccountName}, runtimeRollback={tinyDispatch.RollbackSucceeded}.");
                            continue;
                        }

                        if (prepareResult == LegacyTinyCombinePreparationResult.Accepted &&
                            applyResult == LegacyTinyCombineApplicationResult.Accepted &&
                            tinyCombineOutcome is not null && tinyCombineFrames is not null)
                        {
                            if (tinyCombineOutcome.GuildTaxNpcUpdate is { } guildTaxUpdate)
                            {
                                if (guildTreasuryFileStore is null)
                                {
                                    Console.WriteLine($"Guild treasury save skipped: category=Combines, npc={guildTaxUpdate.ConnectionId}, reason=NpcRootUnavailable; runtime-credit=true.");
                                }
                                else
                                {
                                    try
                                    {
                                        var saveResult = await guildTreasuryFileStore.SaveAsync(world, guildTaxUpdate.ConnectionId, cancellationToken);
                                        Console.WriteLine($"Guild treasury save: category=Combines, npc={guildTaxUpdate.ConnectionId}, result={saveResult}; runtime-credit=true.");
                                    }
                                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
                                    {
                                        Console.WriteLine($"Guild treasury save failed: category=Combines, npc={guildTaxUpdate.ConnectionId}, error={error.GetType().Name}; runtime-credit=true.");
                                    }
                                }
                            }

                            foreach (var tinyCombineFrame in tinyCombineFrames)
                                await stream.WriteAsync(tinyCombineFrame, cancellationToken);

                            Console.WriteLine($"Tiny combine result: category=Combines, event={(tinyCombineOutcome.Succeeded ? "Succeeded" : "Failed")}, account={tinyCombineSession.AccountName}, raw-roll={tinyCombineOutcome.RawRoll}, normalized-roll={tinyCombineOutcome.NormalizedRoll}, threshold={tinyCombineOutcome.SuccessThreshold}, gold={tinyCombineOutcome.CoinAfter}, guild-tax={(tinyCombineOutcome.GuildTaxNpcUpdate is null ? "none" : "credited")}.");
                        }
                        else if (applyResult == LegacyTinyCombineApplicationResult.StateChanged)
                        {
                            var completeFrame = new TinyCombineCompleteConfirmation()
                                .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, result: 0);
                            await stream.WriteAsync(completeFrame, cancellationToken);
                            Console.WriteLine($"Tiny combine rejected after state changed: account={tinyCombineSession.AccountName}.");
                        }
                        else if (prepareResult == LegacyTinyCombinePreparationResult.ItemChanged)
                        {
                            var completeFrame = new TinyCombineCompleteConfirmation()
                                .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, result: 0);
                            await stream.WriteAsync(completeFrame, cancellationToken);
                            Console.WriteLine($"Tiny combine rejected: category=Combines, event=StaleItem, account={tinyCombineSession.AccountName}.");
                        }
                        else if (prepareResult is LegacyTinyCombinePreparationResult.InsufficientCoin or LegacyTinyCombinePreparationResult.InvalidRecipe)
                        {
                            var codec = LegacyFrameCodec.CreateDefault();
                            var wrongCombinationFrame = new MessagePanelConfirmation(LegacyTinyCombineRelay.WrongCombinationMessage)
                                .ToFrame(codec, frame.Header.ClientTick, frame.Header.KeywordIndex);
                            await stream.WriteAsync(wrongCombinationFrame, cancellationToken);
                            var completeFrame = new TinyCombineCompleteConfirmation()
                                .ToFrame(codec, frame.Header.ClientTick, frame.Header.KeywordIndex, result: 0);
                            await stream.WriteAsync(completeFrame, cancellationToken);
                            Console.WriteLine($"Tiny combine rejected: account={tinyCombineSession.AccountName}, reason={prepareResult}.");
                        }
                        else if (prepareResult == LegacyTinyCombinePreparationResult.InvalidInventoryPosition)
                        {
                            var closeDispatch = await LegacyAutoTradeCloseCoordinator.CloseForTinyCombineInvalidPositionAndCaptureAsync(
                                connectionId, sessions, world, autoTradeBook, autoTradeStateStore,
                                LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, cancellationToken);
                            foreach (var closePlan in closeDispatch.Frames)
                                foreach (var recipientId in closePlan.RecipientIds)
                                    await world.SendAsync(recipientId, closePlan.Frame, cancellationToken);
                            Console.WriteLine($"Tiny combine invalid position triggered trade cleanup: account={tinyCombineSession.AccountName}, close={closeDispatch.Outcome.Result}; legacy signal 900 maps to MSG_QuitTrade 0x0384.");
                        }
                        else if (prepareResult == LegacyTinyCombinePreparationResult.Accepted)
                            Console.WriteLine($"Tiny combine apply rejected: account={tinyCombineSession.AccountName}, reason={applyResult}.");
                        else
                            Console.WriteLine($"Tiny combine preparation rejected: account={tinyCombineSession.AccountName}, reason={prepareResult}.");
                        continue;
                    }
                    if (UseItemRequest.TryParse(frame, out var useItem) && useItem is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var useItemSession) || useItemSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine("Rejected use-item frame: session is not in USER_PLAY.");
                            continue;
                        }

                        var potionResult = world.TryApplyPotion(connectionId, useItem, out var potionOutcome, Environment.TickCount64);
                        if (potionResult != LegacyUseItemResult.UnsupportedItem)
                        {
                            if (potionResult == LegacyUseItemResult.Accepted && potionOutcome is not null && world.TryGetResourceState(connectionId, out var potionState, out var requestedHp, out var requestedMp) && potionState is not null)
                            {
                                var resourceFrame = new SetHpMpConfirmation(potionState.CurrentScore.Hp, potionState.CurrentMana, requestedHp, requestedMp)
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                await stream.WriteAsync(resourceFrame, cancellationToken);
                                Console.WriteLine($"Potion applied: account={useItemSession.AccountName}, source={potionOutcome.SourceSlot}, requestedHp={requestedHp}, requestedMp={requestedMp}.");
                            }
                        else
                            Console.WriteLine($"Potion rejected: account={useItemSession.AccountName}, source={useItem.SourceSlot}, reason={potionResult}.");
                            continue;
                        }

                        var bloodHandsResult = world.TryApplyBloodHandsBook(connectionId, useItem, out var bloodHandsOutcome);
                        if (bloodHandsResult != LegacyUseItemResult.UnsupportedItem)
                        {
                            if (bloodHandsResult == LegacyUseItemResult.Accepted && bloodHandsOutcome is not null)
                            {
                                var learnedNotice = new MessagePanelConfirmation("Mãos Sangrentas aprendida.")
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex);
                                await stream.WriteAsync(learnedNotice, cancellationToken);
                                if (world.TryBuildUpdateEtcFrame(connectionId, LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), out var bloodHandsEtcFrame) && bloodHandsEtcFrame is not null)
                                    await stream.WriteAsync(bloodHandsEtcFrame, cancellationToken);
                                Console.WriteLine($"Blood Hands book applied: account={useItemSession.AccountName}, source={bloodHandsOutcome.SourceSlot}, secondary-skills=0x{BinaryPrimitives.ReadUInt32LittleEndian(bloodHandsOutcome.MobExtraSnapshot.AsSpan(LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset)):X8}.");
                            }
                            else
                            {
                                if (bloodHandsOutcome is not null)
                                {
                                    var notice = bloodHandsResult switch
                                    {
                                        LegacyUseItemResult.LevelRequirementNotMet => "E necessario ter nivel 200 para aprender Mãos Sangrentas.",
                                        LegacyUseItemResult.ClassRestricted => "Mãos Sangrentas exige personagem Trans Celestial.",
                                        LegacyUseItemResult.CompetingSkillAlreadyLearned => "Apenas uma 9ª Skill por personagem.",
                                        LegacyUseItemResult.RequiredSkillNotLearned => "E necessario ter aprendido a 8ª Skill.",
                                        LegacyUseItemResult.AlreadyCompleted => "Voce ja aprendeu Mãos Sangrentas.",
                                        _ => null,
                                    };
                                    if (notice is not null)
                                    {
                                        var noticeFrame = new MessagePanelConfirmation(notice)
                                            .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex);
                                        await stream.WriteAsync(noticeFrame, cancellationToken);
                                    }

                                    var restoreItemFrame = new SendItemConfirmation(
                                            checked((short)useItem.SourceType),
                                            checked((short)useItem.SourceSlot),
                                            bloodHandsOutcome.SourceItem)
                                        .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, (ushort)connectionId);
                                    await stream.WriteAsync(restoreItemFrame, cancellationToken);
                                }
                                Console.WriteLine($"Blood Hands book rejected: account={useItemSession.AccountName}, source={useItem.SourceSlot}, reason={bloodHandsResult}.");
                            }
                            continue;
                        }

                        var magicMirrorResult = world.TryApplyMagicMirrorBook(connectionId, useItem, out var magicMirrorOutcome);
                        if (magicMirrorResult != LegacyUseItemResult.UnsupportedItem)
                        {
                            if (magicMirrorResult == LegacyUseItemResult.Accepted && magicMirrorOutcome is not null)
                            {
                                var learnedNotice = new MessagePanelConfirmation("Espelho Mágico aprendido.")
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex);
                                await stream.WriteAsync(learnedNotice, cancellationToken);
                                if (world.TryBuildUpdateEtcFrame(connectionId, LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), out var magicMirrorEtcFrame) && magicMirrorEtcFrame is not null)
                                    await stream.WriteAsync(magicMirrorEtcFrame, cancellationToken);
                                Console.WriteLine($"Magic Mirror book applied: account={useItemSession.AccountName}, source={magicMirrorOutcome.SourceSlot}, secondary-skills=0x{BinaryPrimitives.ReadUInt32LittleEndian(magicMirrorOutcome.MobExtraSnapshot.AsSpan(LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset)):X8}.");
                            }
                            else
                            {
                                if (magicMirrorOutcome is not null)
                                {
                                    var notice = magicMirrorResult switch
                                    {
                                        LegacyUseItemResult.LevelRequirementNotMet => "E necessario ter nivel 200 para aprender Espelho Mágico.",
                                        LegacyUseItemResult.ClassRestricted => "Espelho Mágico exige personagem Trans Celestial da classe Foema.",
                                        LegacyUseItemResult.CompetingSkillAlreadyLearned => "Apenas uma 9ª Skill por personagem.",
                                        LegacyUseItemResult.RequiredSkillNotLearned => "E necessario ter aprendido a 8ª Skill.",
                                        LegacyUseItemResult.AlreadyCompleted => "Voce ja aprendeu Espelho Mágico.",
                                        _ => null,
                                    };
                                    if (notice is not null)
                                    {
                                        var noticeFrame = new MessagePanelConfirmation(notice)
                                            .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex);
                                        await stream.WriteAsync(noticeFrame, cancellationToken);
                                    }

                                    var restoreItemFrame = new SendItemConfirmation(
                                            checked((short)useItem.SourceType),
                                            checked((short)useItem.SourceSlot),
                                            magicMirrorOutcome.SourceItem)
                                        .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, (ushort)connectionId);
                                    await stream.WriteAsync(restoreItemFrame, cancellationToken);
                                }
                                Console.WriteLine($"Magic Mirror book rejected: account={useItemSession.AccountName}, source={useItem.SourceSlot}, reason={magicMirrorResult}.");
                            }
                            continue;
                        }

                        var divineProtectionResult = world.TryApplyDivineProtectionBook(connectionId, useItem, out var divineProtectionOutcome);
                        if (divineProtectionResult != LegacyUseItemResult.UnsupportedItem)
                        {
                            if (divineProtectionResult == LegacyUseItemResult.Accepted && divineProtectionOutcome is not null)
                            {
                                var learnedNotice = new MessagePanelConfirmation("Livro de Proteção Divina utilizado.")
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex);
                                await stream.WriteAsync(learnedNotice, cancellationToken);
                                if (world.TryBuildUpdateEtcFrame(connectionId, LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), out var divineProtectionEtcFrame) && divineProtectionEtcFrame is not null)
                                    await stream.WriteAsync(divineProtectionEtcFrame, cancellationToken);
                                Console.WriteLine($"Divine Protection book applied: account={useItemSession.AccountName}, source={divineProtectionOutcome.SourceSlot}, secondary-skills=0x{BinaryPrimitives.ReadUInt32LittleEndian(divineProtectionOutcome.MobExtraSnapshot.AsSpan(LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset)):X8}.");
                            }
                            else
                            {
                                if (divineProtectionOutcome is not null)
                                {
                                    var notice = divineProtectionResult switch
                                    {
                                        LegacyUseItemResult.LevelRequirementNotMet => "E necessario ter nivel 200 para usar o Livro de Proteção Divina.",
                                        LegacyUseItemResult.ClassRestricted => "O Livro de Proteção Divina exige personagem Trans Celestial da classe Foema.",
                                        LegacyUseItemResult.CompetingSkillAlreadyLearned => "Apenas uma 9ª Skill por personagem.",
                                        LegacyUseItemResult.RequiredSkillNotLearned => "E necessario ter aprendido a 8ª Skill.",
                                        LegacyUseItemResult.AlreadyCompleted => "Voce ja utilizou o Livro de Proteção Divina.",
                                        _ => null,
                                    };
                                    if (notice is not null)
                                    {
                                        var noticeFrame = new MessagePanelConfirmation(notice)
                                            .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex);
                                        await stream.WriteAsync(noticeFrame, cancellationToken);
                                    }

                                    var restoreItemFrame = new SendItemConfirmation(
                                            checked((short)useItem.SourceType),
                                            checked((short)useItem.SourceSlot),
                                            divineProtectionOutcome.SourceItem)
                                        .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, (ushort)connectionId);
                                    await stream.WriteAsync(restoreItemFrame, cancellationToken);
                                }
                                Console.WriteLine($"Divine Protection book rejected: account={useItemSession.AccountName}, source={useItem.SourceSlot}, reason={divineProtectionResult}.");
                            }
                            continue;
                        }

                        var sephiraResult = world.TryApplySephiraSkillBook(connectionId, useItem, out var sephiraOutcome);
                        if (sephiraResult != LegacyUseItemResult.UnsupportedItem)
                        {
                            if (sephiraResult == LegacyUseItemResult.Accepted && sephiraOutcome is not null)
                            {
                                var itemName = itemData?[sephiraOutcome.SourceItem.Index]?.Name ?? $"#{sephiraOutcome.SourceItem.Index}";
                                var learnedNotice = new MessagePanelConfirmation($"Skill aprendida: {itemName}.")
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex);
                                await stream.WriteAsync(learnedNotice, cancellationToken);
                                if (world.TryBuildUpdateEtcFrame(connectionId, LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), out var sephiraEtcFrame) && sephiraEtcFrame is not null)
                                    await stream.WriteAsync(sephiraEtcFrame, cancellationToken);
                                Console.WriteLine($"Sephira skill book applied: account={useItemSession.AccountName}, source={sephiraOutcome.SourceSlot}, volatile={sephiraOutcome.Volatile}, learned-bit=0x{sephiraOutcome.LearnedSkillBit:X8}, affect44={sephiraOutcome.AffectApplied}.");
                            }
                            else
                            {
                                if (sephiraOutcome is not null)
                                {
                                    if (sephiraResult == LegacyUseItemResult.AlreadyCompleted)
                                    {
                                        var noticeFrame = new MessagePanelConfirmation("Voce ja aprendeu essa skill.")
                                            .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex);
                                        await stream.WriteAsync(noticeFrame, cancellationToken);
                                    }

                                    var restoreItemFrame = new SendItemConfirmation(
                                            checked((short)useItem.SourceType),
                                            checked((short)useItem.SourceSlot),
                                            sephiraOutcome.SourceItem)
                                        .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, (ushort)connectionId);
                                    await stream.WriteAsync(restoreItemFrame, cancellationToken);
                                }
                                Console.WriteLine($"Sephira skill book rejected: account={useItemSession.AccountName}, source={useItem.SourceSlot}, reason={sephiraResult}.");
                            }
                            continue;
                        }

                        var refinementResult = world.TryApplyRefinement(connectionId, useItem, out var refinementOutcome);
                        if (refinementResult != LegacyUseItemResult.UnsupportedItem)
                        {
                            if (refinementResult == LegacyUseItemResult.Accepted && refinementOutcome is not null)
                            {
                                var destinationFrame = new SendItemConfirmation(1, checked((short)refinementOutcome.DestinationSlot), refinementOutcome.DestinationItem)
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                await stream.WriteAsync(destinationFrame, cancellationToken);
                                Console.WriteLine($"Item refinement {(refinementOutcome.Succeeded ? "succeeded" : "failed")}: account={useItemSession.AccountName}, source={refinementOutcome.SourceSlot}, destination={refinementOutcome.DestinationSlot}.");
                            }
                        else
                            Console.WriteLine($"Item refinement rejected: account={useItemSession.AccountName}, source={useItem.SourceSlot}, destination={useItem.DestinationSlot}, reason={refinementResult}.");
                            continue;
                        }

                        var legendaryResult = world.TryApplyLegendaryUpgrade(connectionId, useItem, out var legendaryOutcome);
                        if (legendaryResult != LegacyUseItemResult.UnsupportedItem)
                        {
                            if (legendaryResult == LegacyUseItemResult.Accepted && legendaryOutcome is not null)
                            {
                                if (legendaryOutcome.Succeeded)
                                {
                                    var destinationFrame = new SendItemConfirmation(1, checked((short)legendaryOutcome.DestinationSlot), legendaryOutcome.DestinationItem)
                                        .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                    await stream.WriteAsync(destinationFrame, cancellationToken);
                                }
                                Console.WriteLine($"Legendary upgrade {(legendaryOutcome.Succeeded ? "succeeded" : "failed")}: account={useItemSession.AccountName}, source={legendaryOutcome.SourceSlot}, destination={legendaryOutcome.DestinationSlot}.");
                            }
                        else
                            Console.WriteLine($"Legendary upgrade rejected: account={useItemSession.AccountName}, source={useItem.SourceSlot}, destination={useItem.DestinationSlot}, reason={legendaryResult}.");
                            continue;
                        }

                        var orcPillResult = world.TryApplyOrcPill(connectionId, useItem, out var orcPillOutcome);
                        if (orcPillResult != LegacyUseItemResult.UnsupportedItem)
                        {
                            if (orcPillResult == LegacyUseItemResult.Accepted && orcPillOutcome is not null && world.TryBuildUpdateEtcFrame(connectionId, LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), out var orcEtcFrame) && orcEtcFrame is not null)
                            {
                                await stream.WriteAsync(orcEtcFrame, cancellationToken);
                                Console.WriteLine($"Orc Pill applied: account={useItemSession.AccountName}, source={orcPillOutcome.SourceSlot}, skillBonus={orcPillOutcome.SkillBonus}.");
                            }
                            else
                                Console.WriteLine($"Orc Pill rejected: account={useItemSession.AccountName}, source={useItem.SourceSlot}, reason={orcPillResult}.");
                            continue;
                        }

                        var experienceConsumableResult = world.TryApplyExperienceConsumable(connectionId, useItem, out var experienceConsumableOutcome);
                        if (experienceConsumableResult != LegacyUseItemResult.UnsupportedItem)
                        {
                            if (experienceConsumableResult == LegacyUseItemResult.Accepted && experienceConsumableOutcome is not null)
                            {
                                var etcFrame = BuildClientV769UpdateEtcFrame(
                                    experienceConsumableOutcome.MobSnapshot,
                                    ReadOnlySpan<byte>.Empty,
                                    (ushort)connectionId,
                                    frame.Header.ClientTick,
                                    (byte)RandomNumberGenerator.GetInt32(256));
                                await stream.WriteAsync(etcFrame, cancellationToken);
                                if (experienceConsumableOutcome.Stage > 0 && (experienceConsumableOutcome.Volatile == 7 || experienceConsumableOutcome.LeveledUp))
                                {
                                    var scoreFrame = BuildClientV769UpdateScoreWithoutAffectFrame(
                                        experienceConsumableOutcome.MobSnapshot,
                                        (ushort)connectionId,
                                        frame.Header.ClientTick,
                                        (byte)RandomNumberGenerator.GetInt32(256));
                                    await stream.WriteAsync(scoreFrame, cancellationToken);
                                }
                                Console.WriteLine($"Experience consumable applied: account={useItemSession.AccountName}, volatile={experienceConsumableOutcome.Volatile}, source={experienceConsumableOutcome.SourceSlot}, level={experienceConsumableOutcome.Level}.");
                            }
                            else
                                Console.WriteLine($"Experience consumable rejected: account={useItemSession.AccountName}, source={useItem.SourceSlot}, reason={experienceConsumableResult}.");
                            continue;
                        }

                        var affectConsumableResult = world.TryApplyAffectConsumable(connectionId, useItem, out var affectConsumableOutcome);
                        if (affectConsumableResult != LegacyUseItemResult.UnsupportedItem)
                        {
                            if (affectConsumableResult == LegacyUseItemResult.Accepted && affectConsumableOutcome is not null)
                            {
                                if (affectConsumableOutcome.AffectType == 33)
                                {
                                    var headItem = LegacyItem.Read(affectConsumableOutcome.MobSnapshot.AsSpan(
                                        LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
                                    var equipmentItemFrame = new SendItemConfirmation(0, 0, headItem)
                                        .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, (ushort)connectionId);
                                    await stream.WriteAsync(equipmentItemFrame, cancellationToken);
                                }

                                var scoreFrame = BuildClientV769UpdateScoreFrame(
                                    affectConsumableOutcome.MobSnapshot,
                                    affectConsumableOutcome.AffectSnapshot,
                                    (ushort)connectionId,
                                    frame.Header.ClientTick,
                                    (byte)RandomNumberGenerator.GetInt32(256));
                                await stream.WriteAsync(scoreFrame, cancellationToken);
                                if (affectConsumableOutcome.AffectType != 33 && TryBuildClientV769UpdateAffectFrame(
                                        affectConsumableOutcome.AffectSnapshot,
                                        (ushort)connectionId,
                                        frame.Header.ClientTick,
                                        frame.Header.KeywordIndex,
                                        out var affectFrame))
                                    await stream.WriteAsync(affectFrame, cancellationToken);
                                Console.WriteLine($"Affect consumable applied: account={useItemSession.AccountName}, source={affectConsumableOutcome.SourceSlot}, affect={affectConsumableOutcome.AffectValue}, time={affectConsumableOutcome.AffectTime}.");
                            }
                            else
                            {
                                if (affectConsumableOutcome is { AffectType: 33 })
                                {
                                    var restoreItemFrame = new SendItemConfirmation(
                                            checked((short)useItem.SourceType),
                                            checked((short)useItem.SourceSlot),
                                            affectConsumableOutcome.SourceItem)
                                        .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, (ushort)connectionId);
                                    await stream.WriteAsync(restoreItemFrame, cancellationToken);
                                    var noticeFrame = new MessagePanelConfirmation("Nao pode usar outra transformacao agora.")
                                        .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex);
                                    await stream.WriteAsync(noticeFrame, cancellationToken);
                                }
                                Console.WriteLine($"Affect consumable rejected: account={useItemSession.AccountName}, source={useItem.SourceSlot}, reason={affectConsumableResult}.");
                            }
                            continue;
                        }

                        var pvpJewelryResult = world.TryApplyPvpJewelry(connectionId, useItem, out var pvpJewelryOutcome);
                        if (pvpJewelryResult != LegacyUseItemResult.UnsupportedItem)
                        {
                            if (pvpJewelryResult == LegacyUseItemResult.Accepted && pvpJewelryOutcome is not null)
                            {
                                var scoreFrame = BuildClientV769UpdateScoreFrame(
                                    pvpJewelryOutcome.MobSnapshot,
                                    pvpJewelryOutcome.AffectSnapshot,
                                    (ushort)connectionId,
                                    frame.Header.ClientTick,
                                    (byte)RandomNumberGenerator.GetInt32(256));
                                await stream.WriteAsync(scoreFrame, cancellationToken);
                                if (TryBuildClientV769UpdateAffectFrame(
                                        pvpJewelryOutcome.AffectSnapshot,
                                        (ushort)connectionId,
                                        frame.Header.ClientTick,
                                        frame.Header.KeywordIndex,
                                        out var jewelryAffectFrame))
                                    await stream.WriteAsync(jewelryAffectFrame, cancellationToken);
                                Console.WriteLine($"PvP jewelry applied: account={useItemSession.AccountName}, source={pvpJewelryOutcome.SourceSlot}, affect={pvpJewelryOutcome.AffectSlot}, level={pvpJewelryOutcome.AffectLevel}, time={pvpJewelryOutcome.AffectTime}.");
                            }
                            else
                                Console.WriteLine($"PvP jewelry rejected: account={useItemSession.AccountName}, source={useItem.SourceSlot}, reason={pvpJewelryResult}.");
                            continue;
                        }

                        var movementConsumableResult = world.TryApplyMovementConsumable(connectionId, useItem, out var movementConsumableOutcome);
                        if (movementConsumableResult != LegacyUseItemResult.UnsupportedItem)
                        {
                            if (movementConsumableResult == LegacyUseItemResult.Accepted && movementConsumableOutcome is not null)
                            {
                                if (movementConsumableOutcome.Moved)
                                {
                                    sessions.SetServerPosition(connectionId, movementConsumableOutcome.PositionX, movementConsumableOutcome.PositionY);
                                    var movementFrame = new ActionRequest(
                                        movementConsumableOutcome.PositionX,
                                        movementConsumableOutcome.PositionY,
                                        Effect: 1,
                                        Speed: 0,
                                        Route: new byte[24],
                                        movementConsumableOutcome.PositionX,
                                        movementConsumableOutcome.PositionY)
                                        .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                    await stream.WriteAsync(movementFrame, cancellationToken);
                                    await world.BroadcastAsync(connectionId, movementFrame, cancellationToken);
                                }
                                Console.WriteLine($"Movement consumable applied: account={useItemSession.AccountName}, volatile={movementConsumableOutcome.Volatile}, source={movementConsumableOutcome.SourceSlot}, position=({movementConsumableOutcome.PositionX},{movementConsumableOutcome.PositionY}).");
                            }
                            else
                                Console.WriteLine($"Movement consumable rejected: account={useItemSession.AccountName}, source={useItem.SourceSlot}, reason={movementConsumableResult}.");
                            continue;
                        }

                        var mountCatalystResult = world.TryApplyMountCatalyst(connectionId, useItem, out var mountCatalystOutcome);
                        if (mountCatalystResult != LegacyUseItemResult.UnsupportedItem)
                        {
                            if (mountCatalystResult == LegacyUseItemResult.Accepted && mountCatalystOutcome is not null)
                            {
                                var destinationFrame = new SendItemConfirmation(0, checked((short)mountCatalystOutcome.DestinationSlot), mountCatalystOutcome.DestinationItem)
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                await stream.WriteAsync(destinationFrame, cancellationToken);
                                if (world.TryBuildUpdateEquipFrame(
                                        connectionId,
                                        LegacyFrameCodec.CreateDefault(),
                                        frame.Header.ClientTick,
                                        frame.Header.KeywordIndex,
                                        out var mountEquipFrame) &&
                                    mountEquipFrame is not null)
                                {
                                    await stream.WriteAsync(mountEquipFrame, cancellationToken);
                                    await world.BroadcastAsync(connectionId, mountEquipFrame, cancellationToken);
                                }
                                Console.WriteLine($"Mount catalyst applied: account={useItemSession.AccountName}, source={mountCatalystOutcome.SourceSlot}, destination={mountCatalystOutcome.DestinationSlot}, restored={mountCatalystOutcome.Restored}.");
                            }
                            else
                                Console.WriteLine($"Mount catalyst rejected: account={useItemSession.AccountName}, source={useItem.SourceSlot}, destination={useItem.DestinationSlot}, reason={mountCatalystResult}.");
                            continue;
                        }

                        if (useItemSession.AccountName is null)
                        {
                            Console.WriteLine($"Coin consumable rejected: connection={connectionId}, reason=AccountIdentityUnavailable.");
                            continue;
                        }
                        LegacyUseItemResult coinConsumableResult;
                        LegacyCoinConsumableOutcome? coinConsumableOutcome;
                        await using (var coinAccountLease = await accountOperationGate.AcquireAsync(
                                         [useItemSession.AccountName], cancellationToken))
                        {
                            coinConsumableResult = world.TryApplyCoinConsumable(connectionId, useItem, out coinConsumableOutcome);
                        }
                        if (coinConsumableResult != LegacyUseItemResult.UnsupportedItem)
                        {
                            if (coinConsumableResult == LegacyUseItemResult.Accepted && coinConsumableOutcome is not null && world.TryBuildUpdateEtcFrame(connectionId, LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), out var coinEtcFrame) && coinEtcFrame is not null)
                            {
                                await stream.WriteAsync(coinEtcFrame, cancellationToken);
                                Console.WriteLine($"Coin consumable applied: account={useItemSession.AccountName}, source={coinConsumableOutcome.SourceSlot}, added={coinConsumableOutcome.AddedCoin}, coin={coinConsumableOutcome.Coin}.");
                            }
                            else
                                Console.WriteLine($"Coin consumable rejected: account={useItemSession.AccountName}, source={useItem.SourceSlot}, reason={coinConsumableResult}.");
                            continue;
                        }

                        var donateConsumableResult = world.TryApplyDonateConsumable(connectionId, useItem, out var donateConsumableOutcome);
                        if (donateConsumableResult != LegacyUseItemResult.UnsupportedItem)
                        {
                            if (donateConsumableResult == LegacyUseItemResult.Accepted && donateConsumableOutcome is not null && donateBalances is not null && useItemSession.AccountName is not null)
                            {
                                var saveResult = await donateBalances.TrySaveDonateAsync(useItemSession.AccountName, donateConsumableOutcome.Donate, donateConsumableOutcome.PreviousDonate, cancellationToken);
                                if (saveResult == DonateBalanceSaveResult.Success)
                                {
                                    var codec = LegacyFrameCodec.CreateDefault();
                                    var itemFrame = new SendItemConfirmation(1, checked((short)donateConsumableOutcome.SourceSlot), donateConsumableOutcome.SourceItem)
                                        .ToFrame(codec, frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                    await stream.WriteAsync(itemFrame, cancellationToken);
                                    var donatePix = await ReadDonatePixAsync(donateBalances, useItemSession.AccountName!, cancellationToken);
                                    var balanceFrame = new DonateBalanceConfirmation(donateConsumableOutcome.Donate, donatePix)
                                        .ToFrame(codec, frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                    await stream.WriteAsync(balanceFrame, cancellationToken);
                                    Console.WriteLine($"Donate consumable applied: account={useItemSession.AccountName}, source={donateConsumableOutcome.SourceSlot}, added={donateConsumableOutcome.AddedDonate}, donate={donateConsumableOutcome.Donate}.");
                                }
                                else
                                {
                                    var rolledBack = world.TryRollbackDonateConsumable(connectionId, donateConsumableOutcome);
                                    Console.WriteLine($"Donate consumable persistence failed: account={useItemSession.AccountName}, source={donateConsumableOutcome.SourceSlot}, save={saveResult}, rollback={rolledBack}.");
                                }
                            }
                            else
                            {
                                if (donateConsumableOutcome is not null)
                                    world.TryRollbackDonateConsumable(connectionId, donateConsumableOutcome);
                                Console.WriteLine($"Donate consumable rejected: account={useItemSession.AccountName}, source={useItem.SourceSlot}, reason={donateConsumableResult}.");
                            }
                            continue;
                        }

                        var useResult = world.TryApplyClassReset(connectionId, useItem, out var useOutcome);
                        if (useResult == LegacyUseItemResult.Accepted && useOutcome is not null)
                        {
                            // The reference sends the changed destination item; the client consumes the source item locally.
                            var destinationFrame = new SendItemConfirmation(1, checked((short)useOutcome.DestinationSlot), useOutcome.DestinationItem)
                                .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                            await stream.WriteAsync(destinationFrame, cancellationToken);
                            Console.WriteLine($"Class reset applied: account={useItemSession.AccountName}, source={useOutcome.SourceSlot}, destination={useOutcome.DestinationSlot}.");
                        }
                        else
                            Console.WriteLine($"Use-item rejected: account={useItemSession.AccountName}, source={useItem.SourceSlot}, destination={useItem.DestinationSlot}, reason={useResult}.");
                        continue;
                    }

                    if (ApplyBonusRequest.TryParse(frame, out var applyBonusRequest) && applyBonusRequest is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var applyBonusSession) || applyBonusSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine("Rejected apply-bonus frame: session is not in USER_PLAY.");
                            if (applyBonusSession is not null)
                            {
                                var hp = world.TryGetResourceState(connectionId, out var nonPlayingBonusState, out _, out _) && nonPlayingBonusState is not null
                                    ? nonPlayingBonusState.CurrentScore.Hp
                                    : 0;
                                var hpModeFrame = new SetHpModeConfirmation(hp, (short)applyBonusSession.State)
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, checked((ushort)connectionId));
                                await WriteLoggedFrameAsync(stream, serverLog, connectionId, hpModeFrame, cancellationToken, "apply-bonus-not-playing");
                                await RecordCrackErrorAsync(10, 20);
                            }
                            continue;
                        }

                        if (applyBonusRequest.BonusType == 1)
                        {
                            var specialBonusResult = world.TryApplySpecialScoreBonus(
                                connectionId,
                                applyBonusRequest.Detail,
                                out var specialBonusOutcome);
                            if (specialBonusResult == LegacySpecialScoreBonusResult.Applied && specialBonusOutcome is not null)
                            {
                                var scoreFrame = BuildClientV769UpdateScoreFrame(
                                    specialBonusOutcome.Mob,
                                    specialBonusOutcome.Affect,
                                    checked((ushort)connectionId),
                                    frame.Header.ClientTick,
                                    (byte)RandomNumberGenerator.GetInt32(256));
                                await WriteLoggedFrameAsync(stream, serverLog, connectionId, scoreFrame, cancellationToken, "apply-special-bonus-update-score");

                                var etcFrame = BuildClientV769UpdateEtcFrame(
                                    specialBonusOutcome.Mob,
                                    specialBonusOutcome.MobExtra,
                                    checked((ushort)connectionId),
                                    frame.Header.ClientTick,
                                    (byte)RandomNumberGenerator.GetInt32(256));
                                await WriteLoggedFrameAsync(stream, serverLog, connectionId, etcFrame, cancellationToken, "apply-special-bonus-update-etc");
                                Console.WriteLine($"Special score bonus applied: account={applyBonusSession.AccountName}, detail={applyBonusRequest.Detail}.");
                            }
                            else if (specialBonusResult == LegacySpecialScoreBonusResult.NoPoints && specialBonusOutcome is not null)
                            {
                                var etcFrame = BuildClientV769UpdateEtcFrame(
                                    specialBonusOutcome.Mob,
                                    specialBonusOutcome.MobExtra,
                                    checked((ushort)connectionId),
                                    frame.Header.ClientTick,
                                    (byte)RandomNumberGenerator.GetInt32(256));
                                await WriteLoggedFrameAsync(stream, serverLog, connectionId, etcFrame, cancellationToken, "apply-special-bonus-no-points-update-etc");
                                Console.WriteLine($"Special score bonus refresh: account={applyBonusSession.AccountName}, no-points=true.");
                            }
                            else if (specialBonusResult is LegacySpecialScoreBonusResult.LevelCapReached or LegacySpecialScoreBonusResult.AttributeCapReached)
                            {
                                var message = LegacyApplyBonusMessages.ForSpecialScoreBonus(specialBonusResult)!;
                                var messageFrame = new MessagePanelConfirmation(message)
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex);
                                await WriteLoggedFrameAsync(stream, serverLog, connectionId, messageFrame, cancellationToken, "apply-special-bonus-cap-message");
                            }
                            else if (specialBonusResult == LegacySpecialScoreBonusResult.NotAlive &&
                                     world.TryGetResourceState(connectionId, out var specialBonusResources, out _, out _) &&
                                     specialBonusResources is not null)
                            {
                                var hpModeFrame = new SetHpModeConfirmation(specialBonusResources.CurrentScore.Hp, (short)applyBonusSession.State)
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, checked((ushort)connectionId));
                                await WriteLoggedFrameAsync(stream, serverLog, connectionId, hpModeFrame, cancellationToken, "apply-special-bonus-not-alive");
                                await RecordCrackErrorAsync(10, 20);
                            }
                            else
                                Console.WriteLine($"Special score bonus rejected: account={applyBonusSession.AccountName}, detail={applyBonusRequest.Detail}, reason={specialBonusResult}.");
                            continue;
                        }

                        if (applyBonusRequest.BonusType == 2)
                        {
                            if (applyBonusSession.AccountName is null)
                            {
                                Console.WriteLine($"Base skill learn rejected: connection={connectionId}, reason=AccountIdentityUnavailable.");
                                continue;
                            }
                            LegacySkillBonusResult skillBonusResult;
                            LegacySkillBonusOutcome? skillBonusOutcome;
                            int requiredCoin;
                            await using (var skillAccountLease = await accountOperationGate.AcquireAsync(
                                             [applyBonusSession.AccountName], cancellationToken))
                            {
                                skillBonusResult = world.TryApplySkillBonus(
                                    connectionId,
                                    applyBonusRequest.Detail,
                                    applyBonusRequest.TargetId,
                                    out skillBonusOutcome,
                                    out requiredCoin);
                            }
                            if (skillBonusResult == LegacySkillBonusResult.Applied && skillBonusOutcome is not null)
                            {
                                var scoreFrame = BuildClientV769UpdateScoreFrame(
                                    skillBonusOutcome.Mob,
                                    skillBonusOutcome.Affect,
                                    checked((ushort)connectionId),
                                    frame.Header.ClientTick,
                                    (byte)RandomNumberGenerator.GetInt32(256));
                                await WriteLoggedFrameAsync(stream, serverLog, connectionId, scoreFrame, cancellationToken, "apply-skill-bonus-update-score");

                                var etcFrame = BuildClientV769UpdateEtcFrame(
                                    skillBonusOutcome.Mob,
                                    skillBonusOutcome.MobExtra,
                                    checked((ushort)connectionId),
                                    frame.Header.ClientTick,
                                    (byte)RandomNumberGenerator.GetInt32(256));
                                await WriteLoggedFrameAsync(stream, serverLog, connectionId, etcFrame, cancellationToken, "apply-skill-bonus-update-etc");
                                Console.WriteLine($"Base skill learned: account={applyBonusSession.AccountName}, detail={applyBonusRequest.Detail}, target={applyBonusRequest.TargetId}.");
                            }
                            else if (skillBonusResult == LegacySkillBonusResult.NotAlive &&
                                     world.TryGetResourceState(connectionId, out var skillBonusResources, out _, out _) &&
                                     skillBonusResources is not null)
                            {
                                var hpModeFrame = new SetHpModeConfirmation(skillBonusResources.CurrentScore.Hp, (short)applyBonusSession.State)
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, checked((ushort)connectionId));
                                await WriteLoggedFrameAsync(stream, serverLog, connectionId, hpModeFrame, cancellationToken, "apply-skill-bonus-not-alive");
                                await RecordCrackErrorAsync(10, 20);
                            }
                            else if (LegacyApplyBonusMessages.ForSkillBonus(skillBonusResult, requiredCoin) is { } skillBonusMessage)
                            {
                                var messageFrame = new MessagePanelConfirmation(skillBonusMessage)
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex);
                                await WriteLoggedFrameAsync(stream, serverLog, connectionId, messageFrame, cancellationToken, "apply-skill-bonus-rejected-message");
                            }
                            else
                                Console.WriteLine($"Base skill learn rejected: account={applyBonusSession.AccountName}, detail={applyBonusRequest.Detail}, target={applyBonusRequest.TargetId}, reason={skillBonusResult}.");
                            continue;
                        }

                        if (applyBonusRequest.BonusType != 0)
                        {
                            Console.WriteLine($"Apply-bonus branch deferred: account={applyBonusSession.AccountName}, bonus-type={applyBonusRequest.BonusType}.");
                            continue;
                        }

                        var applyBonusResult = world.TryApplyBaseScoreBonus(
                            connectionId,
                            applyBonusRequest.Detail,
                            out var applyBonusOutcome);
                        if ((applyBonusResult is LegacyBaseScoreBonusResult.Applied or LegacyBaseScoreBonusResult.NoPoints) && applyBonusOutcome is not null)
                        {
                            if (applyBonusOutcome.SendEtc)
                            {
                                var etcFrame = BuildClientV769UpdateEtcFrame(
                                    applyBonusOutcome.Mob,
                                    applyBonusOutcome.MobExtra,
                                    checked((ushort)connectionId),
                                    frame.Header.ClientTick,
                                    (byte)RandomNumberGenerator.GetInt32(256));
                                await WriteLoggedFrameAsync(stream, serverLog, connectionId, etcFrame, cancellationToken, "apply-bonus-update-etc");
                            }

                            if (applyBonusOutcome.SendScore)
                            {
                                var scoreFrame = BuildClientV769UpdateScoreFrame(
                                    applyBonusOutcome.Mob,
                                    applyBonusOutcome.Affect,
                                    checked((ushort)connectionId),
                                    frame.Header.ClientTick,
                                    (byte)RandomNumberGenerator.GetInt32(256));
                                await WriteLoggedFrameAsync(stream, serverLog, connectionId, scoreFrame, cancellationToken, "apply-bonus-update-score");
                            }

                            Console.WriteLine(applyBonusResult == LegacyBaseScoreBonusResult.Applied
                                ? $"Base score bonus applied: account={applyBonusSession.AccountName}, detail={applyBonusRequest.Detail}."
                                : $"Base score bonus refresh: account={applyBonusSession.AccountName}, no-points=true.");
                        }
                        else if (applyBonusResult == LegacyBaseScoreBonusResult.NotAlive &&
                                 world.TryGetResourceState(connectionId, out var applyBonusResources, out _, out _) &&
                                 applyBonusResources is not null)
                        {
                            var hpModeFrame = new SetHpModeConfirmation(applyBonusResources.CurrentScore.Hp, (short)applyBonusSession.State)
                                .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, checked((ushort)connectionId));
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, hpModeFrame, cancellationToken, "apply-bonus-not-alive");
                            await RecordCrackErrorAsync(10, 20);
                        }
                        else
                            Console.WriteLine($"Base score bonus rejected: account={applyBonusSession.AccountName}, detail={applyBonusRequest.Detail}, reason={applyBonusResult}.");
                        continue;
                    }

                    if (DeleteItemRequest.TryParse(frame, out var deleteItemRequest) && deleteItemRequest is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var deleteSession) || deleteSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine("Rejected delete-item frame: session is not in USER_PLAY.");
                            continue;
                        }

                        var deleteAccount = deleteSession.AccountName is not null && accounts is not null
                            ? await accounts.ReadSnapshotAsync(deleteSession.AccountName, cancellationToken)
                            : null;
                        if (deleteAccount is null)
                        {
                            Console.WriteLine($"Carry item deletion rejected: account={deleteSession.AccountName}, reason=AccountSnapshotUnavailable.");
                            continue;
                        }

                        var deleteResult = world.TryDeleteCarryItem(
                            connectionId,
                            deleteItemRequest.Slot,
                            deleteItemRequest.ItemIndex,
                            out var cancelledTradeOpponentId,
                            deleteAccount.IsBlocked);
                        if (deleteResult == LegacyDeleteItemResult.Accepted)
                            Console.WriteLine($"Carry item deleted: account={deleteSession.AccountName}, slot={deleteItemRequest.Slot}, item={deleteItemRequest.ItemIndex}.");
                        else if (deleteResult == LegacyDeleteItemResult.TradeCancelled)
                        {
                            var codec = LegacyFrameCodec.CreateDefault();
                            if (cancelledTradeOpponentId > 0)
                            {
                                var opponentSignal = TradeCloseRequest.ToFrame(codec, frame.Header.ClientTick, frame.Header.KeywordIndex, checked((ushort)cancelledTradeOpponentId));
                                await world.SendAsync(cancelledTradeOpponentId, opponentSignal, cancellationToken);
                            }
                            var ownSignal = TradeCloseRequest.ToFrame(codec, frame.Header.ClientTick, frame.Header.KeywordIndex, checked((ushort)connectionId));
                            await world.SendAsync(connectionId, ownSignal, cancellationToken);
                            Console.WriteLine($"Carry item deletion cancelled trade: account={deleteSession.AccountName}, opponent={cancelledTradeOpponentId}.");
                        }
                        else if (deleteResult == LegacyDeleteItemResult.AccountBlocked)
                        {
                            var notice = new MessagePanelConfirmation("Sua conta está com o bloqueio ativo para essa operação.")
                                .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256));
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, notice, cancellationToken, "delete-item-account-blocked");
                            Console.WriteLine($"Carry item deletion rejected: account={deleteSession.AccountName}, reason={deleteResult}.");
                        }
                        else
                            Console.WriteLine($"Carry item deletion rejected: account={deleteSession.AccountName}, slot={deleteItemRequest.Slot}, item={deleteItemRequest.ItemIndex}, reason={deleteResult}.");
                        continue;
                    }

                    if (TradingItemRequest.TryParse(frame, out var tradingItemRequest) && tradingItemRequest is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var tradingSession) || tradingSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine("Rejected trading-item frame: session is not in USER_PLAY.");
                            continue;
                        }

                        var tradingResult = world.TryTradeItems(
                            connectionId,
                            tradingItemRequest.SourcePlace,
                            tradingItemRequest.SourceSlot,
                            tradingItemRequest.DestinationPlace,
                            tradingItemRequest.DestinationSlot,
                            out var tradingOutcome);
                        if (tradingResult == LegacyTradingItemResult.Accepted && tradingOutcome is not null)
                        {
                            var codec = LegacyFrameCodec.CreateDefault();
                            var confirmation = tradingItemRequest.ToFrame(codec, frame.Header.ClientTick, frame.Header.KeywordIndex, frame.Header.Id);
                            await stream.WriteAsync(confirmation, cancellationToken);

                            var sourceFrame = new SendItemConfirmation(checked((short)tradingOutcome.SourcePlace), checked((short)tradingOutcome.SourceSlot), tradingOutcome.SourceSlotItem)
                                .ToFrame(codec, frame.Header.ClientTick, frame.Header.KeywordIndex, checked((ushort)connectionId));
                            await stream.WriteAsync(sourceFrame, cancellationToken);
                            var destinationFrame = new SendItemConfirmation(checked((short)tradingOutcome.DestinationPlace), checked((short)tradingOutcome.DestinationSlot), tradingOutcome.DestinationSlotItem)
                                .ToFrame(codec, frame.Header.ClientTick, frame.Header.KeywordIndex, checked((ushort)connectionId));
                            await stream.WriteAsync(destinationFrame, cancellationToken);
                            if ((tradingOutcome.SourcePlace == LegacyItemPlace.Equip || tradingOutcome.DestinationPlace == LegacyItemPlace.Equip) &&
                                world.TryBuildUpdateEquipFrame(connectionId, codec, frame.Header.ClientTick, frame.Header.KeywordIndex, out var equipFrame) &&
                                equipFrame is not null)
                            {
                                await stream.WriteAsync(equipFrame, cancellationToken);
                                await world.BroadcastAsync(connectionId, equipFrame, cancellationToken);
                            }
                            Console.WriteLine($"Items {(tradingOutcome.WasMerged ? "merged" : "swapped")}: account={tradingSession.AccountName}, source={tradingOutcome.SourcePlace}:{tradingOutcome.SourceSlot}, destination={tradingOutcome.DestinationPlace}:{tradingOutcome.DestinationSlot}.");
                        }
                        else
                            Console.WriteLine($"Item swap rejected: account={tradingSession.AccountName}, source={tradingItemRequest.SourcePlace}:{tradingItemRequest.SourceSlot}, destination={tradingItemRequest.DestinationPlace}:{tradingItemRequest.DestinationSlot}, reason={tradingResult}.");
                        continue;
                    }

                    if (AutoTradeStartRequest.TryParse(frame, out var autoTradeRequest) && autoTradeRequest is not null)
                    {
                        await using var startLease = await autoTradeBook.AcquireAsync([connectionId], cancellationToken);
                        if (!sessions.TryGet(connectionId, out var autoTradeSession) || autoTradeSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine("Rejected autotrade-start frame: session is not in USER_PLAY.");
                            continue;
                        }
                        if (autoTradeSession.AccountName is null)
                        {
                            Console.WriteLine("Rejected autotrade-start frame: account identity is unavailable.");
                            continue;
                        }
                        await using var startAccountLease = await accountOperationGate.AcquireAsync(
                            [autoTradeSession.AccountName], cancellationToken);

                        var combatStateAvailable = world.TryGetCombatState(connectionId, out var autoTradeCombatState) && autoTradeCombatState is not null;
                        var characterPositionAvailable = world.TryGetCharacterSnapshot(connectionId, out _, out var autoTradeX, out var autoTradeY);
                        var locationAvailable = world.TryGetAutoTradeLocation(connectionId, out _, out var cityTax);
                        var accountSnapshot = autoTradeSession.AccountName is not null && accounts is IAccountSnapshotStore snapshotStore
                            ? await snapshotStore.ReadSnapshotAsync(autoTradeSession.AccountName, cancellationToken)
                            : null;
                        var cargo = accountSnapshot?.Cargo.Take(LegacyAutoTradeBook.CargoSlotCount).ToArray()
                            ?? Array.Empty<LegacyItem>();
                        var nonTradeableIndices = itemData is null
                            ? new HashSet<short>()
                            : autoTradeRequest.Items
                                .Where(static item => item.Index != 0)
                                .Where(item => itemData.GetItemAbility(item, LegacyItemEffect.NoTrade) != 0)
                                .Select(static item => item.Index)
                                .ToHashSet();
                        var autoTradeContext = new LegacyAutoTradeStartContext(
                            InPlay: autoTradeSession.State == LoginSessionState.Playing && characterPositionAvailable,
                            CurrentHp: combatStateAvailable ? autoTradeCombatState!.CurrentScore.Hp : 0,
                            LiveTradeActive: world.IsLiveTradeActive(connectionId),
                            InAllowedVillage: locationAvailable,
                            CityTax: cityTax,
                            PositionX: autoTradeX,
                            PositionY: autoTradeY,
                            Cargo: cargo,
                            NonTradeableItemIndices: nonTradeableIndices,
                            AccountBlocked: accountSnapshot?.IsBlocked ?? false,
                            ItemDataAvailable: itemData is not null);
                        var autoTradeResult = autoTradeBook.TryStart(startLease, connectionId,
                            new(autoTradeSession.AccountName ?? string.Empty, autoTradeSession.CharacterSlot),
                            autoTradeRequest, autoTradeContext, out var autoTradeHandle);
                        var autoTradeSnapshot = autoTradeHandle?.Snapshot;
                        var autoTradeStateSaved = true;
                        if (autoTradeResult == LegacyAutoTradeStartResult.Accepted && autoTradeSnapshot is not null && autoTradeStateStore is not null)
                        {
                            if (autoTradeSession.AccountName is null || autoTradeSession.CharacterSlot < 0)
                                autoTradeStateSaved = false;
                            else
                            {
                                try
                                {
                                    autoTradeStateSaved = await autoTradeStateStore.SaveAsync(
                                        autoTradeSession.AccountName,
                                        autoTradeSession.CharacterSlot,
                                        autoTradeSnapshot,
                                        cancellationToken) == LegacyAutoTradeStateResult.Saved;
                                }
                                catch (Exception error)
                                {
                                    autoTradeStateSaved = false;
                                    Console.WriteLine($"Autotrade state persistence failed: account={autoTradeSession.AccountName}, error={error.GetType().Name}: {error.Message}.");
                                }
                            }

                            if (!autoTradeStateSaved)
                            {
                                autoTradeBook.TryStop(startLease, autoTradeHandle!);
                                Console.WriteLine($"Autotrade start rejected: account={autoTradeSession.AccountName}, reason=StatePersistenceUnavailable.");
                                continue;
                            }
                        }

                        if (autoTradeResult == LegacyAutoTradeStartResult.Accepted && autoTradeSnapshot is not null &&
                            LegacyAutoTradeListingRelay.TryCapture(world, autoTradeBook, startLease, connectionId, connectionId,
                                true, LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, out var autoTradeFrames))
                        {
                            await startAccountLease.DisposeAsync();
                            await startLease.DisposeAsync(); // Capture before release; transport never owns the lease.
                            foreach (var plan in autoTradeFrames)
                                foreach (var recipient in plan.RecipientIds)
                                    if (recipient == connectionId) await stream.WriteAsync(plan.Frame, cancellationToken);
                                    else await world.SendAsync(recipient, plan.Frame, cancellationToken);
                            Console.WriteLine($"Autotrade started: account={autoTradeSession.AccountName}, position=({autoTradeX},{autoTradeY}), tax={autoTradeSnapshot.Tax}, title={autoTradeSnapshot.Title}.");
                        }
                        else
                            Console.WriteLine($"Autotrade start rejected: account={autoTradeSession.AccountName}, reason={autoTradeResult}, location={locationAvailable}, cargo={cargo.Length}.");
                        continue;
                    }

                    if (TradeListRequest.TryParse(frame, out var tradeListRequest) && tradeListRequest is not null)
                    {
                        await TradeListRequestHandler.HandleAsync(
                            connectionId,
                            tradeListRequest,
                            frame.Header.ClientTick,
                            frame.Header.KeywordIndex,
                            world,
                            sessions,
                            autoTradeBook,
                            (response, ct) => stream.WriteAsync(response, ct),
                            async (points, type) => { _ = await RecordCrackErrorAsync(points, type); },
                            Console.WriteLine,
                            cancellationToken);
                        continue;
                    }

                    if (AutoTradePurchaseRequest.TryParse(frame, out var autoTradePurchaseRequest) && autoTradePurchaseRequest is not null)
                    {
                        var hostPurchase = await LegacyAutoTradePurchaseCoordinator.ExecuteAsync(
                            connectionId,
                            autoTradePurchaseRequest,
                            sessions,
                            world,
                            autoTradeBook,
                            accounts as IAccountSnapshotStore,
                            accounts as ILegacyCharacterLoginDataStore,
                            autoTradePurchaseCommitStore,
                            cancellationToken,
                            accountOperationGate,
                            accountCargoRuntime);
                        if (hostPurchase.Result == LegacyAutoTradePurchaseHostResult.AccountRecoveryRequired)
                        {
                            Console.Error.WriteLine($"Autotrade purchase requires seller account recovery; closing buyer session: buyer={connectionId}, seller={hostPurchase.SellerAccountName}.");
                            return;
                        }
                        if (hostPurchase.Result == LegacyAutoTradePurchaseHostResult.SellerCargoReconciled)
                        {
                            if (hostPurchase.CargoSnapshot is { } reconciledCargo)
                                await BroadcastCargoResyncAsync(reconciledCargo, sessions, world, cancellationToken);
                            Console.WriteLine($"Autotrade purchase rejected after seller Cargo reconciliation: buyer={connectionId}, seller={hostPurchase.SellerAccountName}.");
                        }
                        else if (hostPurchase.Result == LegacyAutoTradePurchaseHostResult.Accepted &&
                            hostPurchase.Execution?.Outcome is not null)
                        {
                            var purchaseOutcome = hostPurchase.Execution.Outcome;
                            var targetOnline = hostPurchase.TargetOnline;
                            var targetOffline = hostPurchase.TargetOffline;
                            var codec = LegacyFrameCodec.CreateDefault();
                            LegacyWorldNpc? updatedNpc = null;
                            var updatedNpcAvailable = targetOffline &&
                                world.TryGetNpcSnapshot(autoTradePurchaseRequest.TargetId, out updatedNpc) &&
                                updatedNpc is not null;
                            var offlineMob = updatedNpcAvailable ? updatedNpc!.MobSnapshot : [];
                            var offlineAffect = updatedNpcAvailable ? updatedNpc!.AffectSnapshot : [];
                            if (!LegacyAutoTradePurchaseRelay.TryBuild(
                                    purchaseOutcome,
                                    codec,
                                    frame.Header.ClientTick,
                                    frame.Header.KeywordIndex,
                                    targetOffline,
                                    offlineMob,
                                    offlineAffect,
                                    out var relay) ||
                                relay is null)
                            {
                                Console.WriteLine($"Autotrade purchase relay rejected after commit: connection={connectionId}, target={autoTradePurchaseRequest.TargetId}, position={autoTradePurchaseRequest.Position}.");
                                continue;
                            }

                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, relay.BuyerCarryFrame, cancellationToken, $"autotrade-purchase-carry target={autoTradePurchaseRequest.TargetId} position={autoTradePurchaseRequest.Position}");
                            var sellerViewRecipients = targetOnline
                                ? world.GetParticipantIdsInPlayerView(autoTradePurchaseRequest.TargetId)
                                : world.GetParticipantIdsInNpcView(autoTradePurchaseRequest.TargetId);
                            foreach (var recipient in sellerViewRecipients)
                                await world.SendAsync(recipient, relay.ItemSoldFrame, cancellationToken);
                            if (!sellerViewRecipients.Contains(connectionId))
                                await world.SendAsync(connectionId, relay.ItemSoldFrame, cancellationToken);

                            if (relay.OfflineVisualFrame is not null)
                            {
                                foreach (var recipient in world.GetParticipantIdsInNpcView(autoTradePurchaseRequest.TargetId))
                                    await world.SendAsync(recipient, relay.OfflineVisualFrame, cancellationToken);
                            }

                            if (hostPurchase.CargoSnapshot is { ChangedPositions.Count: > 0 } purchasedCargo)
                                await BroadcastCargoResyncAsync(purchasedCargo, sessions, world, cancellationToken);

                            Console.WriteLine($"Autotrade purchase accepted: connection={connectionId}, seller={hostPurchase.SellerAccountName}, target={autoTradePurchaseRequest.TargetId}, position={autoTradePurchaseRequest.Position}, price={purchaseOutcome.Plan.Settlement.ItemPrice}, tax={purchaseOutcome.Plan.Settlement.TaxAmount}.");
                        }
                        else
                            Console.WriteLine($"Autotrade purchase rejected: connection={connectionId}, target={autoTradePurchaseRequest.TargetId}, position={autoTradePurchaseRequest.Position}, reason={hostPurchase.Execution?.Result.ToString() ?? hostPurchase.Result.ToString()}.");
                        continue;
                    }

                    if (TradeOfferRequest.TryParse(frame, out var tradeOffer) && tradeOffer is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var tradeSession) || tradeSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine("Rejected trade-offer frame: session is not in USER_PLAY.");
                            continue;
                        }

                        var tradeResult = world.TryStageTradeOffer(connectionId, tradeOffer, out var tradeOutcome);
                        if (tradeResult == LegacyTradeOfferResult.BothChecked)
                        {
                            if (tradeOutcome is null || tradeSession.AccountName is null ||
                                !sessions.TryGet(tradeOutcome.OpponentId, out var tradeOpponentSession) ||
                                tradeOpponentSession is not { State: LoginSessionState.Playing, AccountName: not null })
                            {
                                Console.WriteLine($"Trade completion rejected after both checks: connection={connectionId}, reason=AccountIdentityUnavailable.");
                                continue;
                            }
                            await using var tradeAccountLease = await accountOperationGate.AcquireAsync(
                                [tradeSession.AccountName, tradeOpponentSession.AccountName], cancellationToken);
                            if (world.GetTradeOpponentId(connectionId) != tradeOutcome.OpponentId ||
                                !sessions.TryGet(tradeOutcome.OpponentId, out var revalidatedTradeOpponent) ||
                                revalidatedTradeOpponent is not { State: LoginSessionState.Playing } ||
                                !string.Equals(revalidatedTradeOpponent.AccountName, tradeOpponentSession.AccountName, StringComparison.Ordinal))
                            {
                                Console.WriteLine($"Trade completion rejected after both checks: connection={connectionId}, reason=PairChanged.");
                                continue;
                            }

                            var completionResult = world.TryCompleteTrade(connectionId, out var completion);
                            if (completionResult == LegacyTradeCompletionResult.Accepted && completion is not null)
                            {
                                var tradeReadyToRelay = true;
                                if (completion.RequiresPersistence)
                                {
                                    if (!LegacyTradePersistence.TryBuild(completion, out var persistencePlan) || persistencePlan is null)
                                    {
                                        tradeReadyToRelay = false;
                                        Console.WriteLine($"Trade rolled back: persistence plan unavailable; first={completion.FirstConnectionId}, second={completion.SecondConnectionId}, runtimeRollback={world.TryRollbackTradeCompletion(completion)}.");
                                    }
                                    else if (accounts is not IAtomicCharacterStateStore atomicStore)
                                    {
                                        tradeReadyToRelay = false;
                                        Console.WriteLine($"Trade rolled back: atomic store unavailable; first={completion.FirstAccountName}, second={completion.SecondAccountName}, runtimeRollback={world.TryRollbackTradeCompletion(completion)}.");
                                    }
                                    else
                                    {
                                        try
                                        {
                                            var persistenceResult = await atomicStore.TrySaveCharacterStatesAtomicallyAsync(
                                                persistencePlan.First,
                                                persistencePlan.Second,
                                                cancellationToken);
                                            Console.WriteLine($"Trade persistence: first={completion.FirstAccountName}, second={completion.SecondAccountName}, result={persistenceResult}.");
                                            if (persistenceResult != AtomicCharacterStateSaveResult.Success)
                                            {
                                                tradeReadyToRelay = false;
                                                Console.WriteLine($"Trade rolled back after persistence rejection: first={completion.FirstAccountName}, second={completion.SecondAccountName}, reason={persistenceResult}, runtimeRollback={world.TryRollbackTradeCompletion(completion)}.");
                                            }
                                        }
                                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                                        {
                                            var runtimeRollback = world.TryRollbackTradeCompletion(completion);
                                            Console.WriteLine($"Trade rolled back on cancellation: first={completion.FirstAccountName}, second={completion.SecondAccountName}, runtimeRollback={runtimeRollback}.");
                                            throw;
                                        }
                                        catch (Exception exception)
                                        {
                                            tradeReadyToRelay = false;
                                            Console.Error.WriteLine($"Trade rolled back after persistence error: first={completion.FirstAccountName}, second={completion.SecondAccountName}, error={exception.GetType().Name}, runtimeRollback={world.TryRollbackTradeCompletion(completion)}.");
                                        }
                                    }
                                }

                                if (!tradeReadyToRelay)
                                    continue;

                                await tradeAccountLease.DisposeAsync();
                                var codec = LegacyFrameCodec.CreateDefault();
                                if (LegacyTradeCompletionRelay.TryBuild(completion, codec, frame.Header.ClientTick, frame.Header.KeywordIndex, out var relayPlan) && relayPlan is not null)
                                {
                                    await world.SendAsync(completion.FirstConnectionId, relayPlan.FirstCarryFrame, cancellationToken);
                                    await world.SendAsync(completion.SecondConnectionId, relayPlan.SecondCarryFrame, cancellationToken);
                                    Console.WriteLine($"Trade completed: first={completion.FirstConnectionId}, second={completion.SecondConnectionId}, coin={completion.FirstCoin}/{completion.SecondCoin}.");
                                }
                                else
                                    Console.WriteLine($"Trade completion relay rejected: first={completion.FirstConnectionId}, second={completion.SecondConnectionId}.");
                            }
                            else
                                Console.WriteLine($"Trade completion rejected after both checks: connection={connectionId}, reason={completionResult}.");

                            continue;
                        }

                        if (tradeResult == LegacyTradeOfferResult.Accepted && tradeOutcome is not null &&
                            LegacyTradeOfferRelay.TryBuild(tradeOutcome, LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, out var tradeRelay) && tradeRelay is not null)
                        {
                            await world.SendAsync(tradeRelay.RecipientConnectionId, tradeRelay.OfferFrame, cancellationToken);
                            if (tradeRelay.RequiresCheckConfirmation)
                            {
                                var checkFrame = TradeCheckConfirmation.ToFrame(
                                    LegacyFrameCodec.CreateDefault(),
                                    frame.Header.ClientTick,
                                    frame.Header.KeywordIndex,
                                    checked((ushort)connectionId));
                                await world.SendAsync(connectionId, checkFrame, cancellationToken);
                            }

                            Console.WriteLine($"Trade offer relayed: account={tradeSession.AccountName}, recipient={tradeRelay.RecipientConnectionId}, paired={tradeOutcome.Paired}, checked={tradeOutcome.OwnState.MyCheck == 1}.");
                        }
                        else
                            Console.WriteLine($"Trade offer rejected: account={tradeSession.AccountName}, opponent={tradeOffer.OpponentId}, reason={tradeResult}.");
                        continue;
                    }

                    if (TradeCloseRequest.IsValid(frame))
                    {
                        if (!sessions.TryGet(connectionId, out var closeTradeSession) || closeTradeSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine("Rejected trade-close frame: session is not in USER_PLAY.");
                            await RecordCrackErrorAsync(LegacyCrackErrorRules.QuitTradePoints, LegacyCrackErrorRules.QuitTradeType);
                            continue;
                        }

                        var closeDispatch = await LegacyAutoTradeCloseCoordinator.CloseAndCaptureAsync(
                            connectionId, sessions, world, autoTradeBook, autoTradeStateStore,
                            LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, cancellationToken);
                        var closeResult = closeDispatch.Outcome.Result;
                        var closeOutcome = closeDispatch.Outcome.Trade;
                        if (closeResult == LegacyAutoTradeCloseResult.NotAlive)
                        {
                            if (world.TryGetResourceState(connectionId, out var closeResources, out _, out _) && closeResources is not null)
                            {
                                var hpModeFrame = new SetHpModeConfirmation(closeResources.CurrentScore.Hp, (short)closeTradeSession.State)
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, checked((ushort)connectionId));
                                await world.SendAsync(connectionId, hpModeFrame, cancellationToken);
                            }
                            await RecordCrackErrorAsync(LegacyCrackErrorRules.QuitTradePoints, LegacyCrackErrorRules.QuitTradeType);
                        }
                        // All state/frames are committed and captured; no operation lease crosses socket I/O.
                        foreach (var plan in closeDispatch.Frames)
                            foreach (var recipientId in plan.RecipientIds)
                                await world.SendAsync(recipientId, plan.Frame, cancellationToken);
                        Console.WriteLine(closeOutcome is not null
                            ? $"Trade closed: account={closeTradeSession.AccountName}, opponent={closeOutcome!.OpponentId}."
                            : $"Trade close ignored: account={closeTradeSession.AccountName}, reason={closeResult}.");
                        continue;
                    }

                    if (SplitItemRequest.TryParse(frame, out var splitItemRequest) && splitItemRequest is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var splitSession) || splitSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine("Rejected split-item frame: session is not in USER_PLAY.");
                            continue;
                        }

                        var splitResult = world.TrySplitCarryItem(
                            connectionId,
                            splitItemRequest.Slot,
                            splitItemRequest.ItemIndex,
                            splitItemRequest.Quantity,
                            out var splitOutcome);
                        if (splitResult == LegacySplitItemResult.Accepted && splitOutcome is not null)
                        {
                            var codec = LegacyFrameCodec.CreateDefault();
                            var destinationFrame = new SendItemConfirmation(
                                    LegacyWorldItem.CarryDestinationType,
                                    checked((short)splitOutcome.DestinationSlot),
                                    splitOutcome.SplitItem)
                                .ToFrame(codec, frame.Header.ClientTick, frame.Header.KeywordIndex, checked((ushort)connectionId));
                            await stream.WriteAsync(destinationFrame, cancellationToken);

                            var sourceFrame = new SendItemConfirmation(
                                    LegacyWorldItem.CarryDestinationType,
                                    checked((short)splitOutcome.SourceSlot),
                                    splitOutcome.UpdatedSourceItem)
                                .ToFrame(codec, frame.Header.ClientTick, frame.Header.KeywordIndex, checked((ushort)connectionId));
                            await stream.WriteAsync(sourceFrame, cancellationToken);
                            Console.WriteLine($"Carry item split: account={splitSession.AccountName}, source={splitOutcome.SourceSlot}, destination={splitOutcome.DestinationSlot}, quantity={splitOutcome.Quantity}, requestedIndex={splitOutcome.RequestedItemIndex}.");
                        }
                        else
                            Console.WriteLine($"Carry item split rejected: account={splitSession.AccountName}, source={splitItemRequest.Slot}, quantity={splitItemRequest.Quantity}, reason={splitResult}.");
                        continue;
                    }

                    if (UpdateItemRequest.TryParse(frame, out var updateItemRequest) && updateItemRequest is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var updateItemSession) || updateItemSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine("Rejected update-item frame: session is not in USER_PLAY.");
                            continue;
                        }

                        var updateItemResult = world.TryUpdateMapItem(
                            connectionId,
                            updateItemRequest.ItemId,
                            updateItemRequest.State,
                            out var updateItemOutcome);
                        if (updateItemResult == LegacyUpdateItemResult.Accepted && updateItemOutcome is not null)
                        {
                            var codec = LegacyFrameCodec.CreateDefault();
                            var clientTick = frame.Header.ClientTick;
                            if (updateItemOutcome.CastleQuestStart is { } castleStart)
                            {
                                if (castleStart.RemovedNpcs.Count > 0)
                                    await BroadcastRemovedNpcsInAreaAsync(world, castleStart.RemovedNpcs, 2176, 1160, 2300, 1276, cancellationToken);
                                if (castleStart.SpawnedNpcs.Count > 0)
                                    await RelayGeneratedNpcGroupAsync(world, castleStart.SpawnedNpcs, cancellationToken);
                                var startTimeFrame = new StartTimeConfirmation(castleStart.TimeRemaining + 1)
                                    .ToFrame(codec, clientTick, frame.Header.KeywordIndex);
                                foreach (var partyConnectionId in castleStart.PartyConnectionIds.Distinct())
                                    await world.SendAsync(partyConnectionId, startTimeFrame, cancellationToken);
                                Console.WriteLine($"Castle quest started: level={castleStart.QuestLevel}, leader={castleStart.LeaderConnectionId}, time={castleStart.TimeRemaining + 1}, spawnedNpcs={castleStart.SpawnedNpcs.Count}.");
                            }
                            if (updateItemOutcome.ConsumedKeySlot >= 0)
                            {
                                var emptyKeySlot = new SendItemConfirmation(
                                        LegacyWorldItem.CarryDestinationType,
                                        checked((short)updateItemOutcome.ConsumedKeySlot),
                                        default)
                                    .ToFrame(codec, clientTick, frame.Header.KeywordIndex, checked((ushort)connectionId));
                                await stream.WriteAsync(emptyKeySlot, cancellationToken);
                            }

                            if (updateItemOutcome.StateChanged)
                            {
                                var updateFrame = new UpdateItemConfirmation(
                                        updateItemOutcome.WireItemId,
                                        updateItemOutcome.MapItem.State)
                                    .ToFrame(codec, clientTick, frame.Header.KeywordIndex, 30_000);
                                foreach (var recipient in world.GetParticipantIdsInMapItemView(updateItemOutcome.WireItemId))
                                    await world.SendAsync(recipient, updateFrame, cancellationToken);
                            }

                            Console.WriteLine($"Map item updated: account={updateItemSession.AccountName}, wireId={updateItemOutcome.WireItemId}, state={updateItemOutcome.MapItem.State}, changed={updateItemOutcome.StateChanged}, keySlot={updateItemOutcome.ConsumedKeySlot}.");
                        }
                        else
                            Console.WriteLine($"Map item update rejected: account={updateItemSession.AccountName}, wireId={updateItemRequest.ItemId}, state={updateItemRequest.State}, reason={updateItemResult}.");
                        continue;
                    }

                    if (DropItemRequest.TryParse(frame, out var dropItemRequest) && dropItemRequest is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var dropItemSession) || dropItemSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine("Rejected drop-item frame: session is not in USER_PLAY.");
                            continue;
                        }

                        var dropItemResult = world.TryDropItem(
                            connectionId,
                            dropItemRequest.SourceType,
                            dropItemRequest.SourceSlot,
                            dropItemRequest.Rotate,
                            dropItemRequest.GridX,
                            dropItemRequest.GridY,
                            out var dropItemOutcome);
                        if (dropItemResult == LegacyDropItemResult.Accepted && dropItemOutcome is not null)
                        {
                            var codec = LegacyFrameCodec.CreateDefault();
                            var clientTick = frame.Header.ClientTick;
                            var confirmation = new DropItemConfirmation(
                                    dropItemOutcome.SourceType,
                                    dropItemOutcome.SourceSlot,
                                    dropItemOutcome.Rotate,
                                    checked((ushort)dropItemOutcome.PositionX),
                                    checked((ushort)dropItemOutcome.PositionY))
                                .ToFrame(codec, clientTick, frame.Header.KeywordIndex, 30_000);
                            await stream.WriteAsync(confirmation, cancellationToken);

                            var create = new CreateItemConfirmation(
                                    checked((ushort)dropItemOutcome.PositionX),
                                    checked((ushort)dropItemOutcome.PositionY),
                                    checked((ushort)dropItemOutcome.WireItemId),
                                    dropItemOutcome.Item,
                                    dropItemOutcome.Rotate)
                                .ToFrame(codec, clientTick, frame.Header.KeywordIndex, 30_000);
                            foreach (var recipient in world.GetParticipantIdsInPlayerView(connectionId))
                                await world.SendAsync(recipient, create, cancellationToken);
                            Console.WriteLine($"Ground item dropped: account={dropItemSession.AccountName}, item={dropItemOutcome.Item.Index}, wireId={dropItemOutcome.WireItemId}, grid={dropItemOutcome.PositionX},{dropItemOutcome.PositionY}.");
                        }
                        else
                            Console.WriteLine($"Ground item drop rejected: account={dropItemSession.AccountName}, source={dropItemRequest.SourceType}:{dropItemRequest.SourceSlot}, reason={dropItemResult}.");
                        continue;
                    }

                    if (GetItemRequest.TryParse(frame, out var getItemRequest) && getItemRequest is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var getItemSession) || getItemSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine("Rejected get-item frame: session is not in USER_PLAY.");
                            continue;
                        }

                        var getItemResult = world.TryGetGroundItem(
                            connectionId,
                            getItemRequest.DestinationType,
                            getItemRequest.DestinationSlot,
                            getItemRequest.ItemId,
                            getItemRequest.GridX,
                            getItemRequest.GridY,
                            out var getItemOutcome);
                        if (getItemResult == LegacyGetItemResult.Accepted && getItemOutcome is not null)
                        {
                            var codec = LegacyFrameCodec.CreateDefault();
                            var clientTick = frame.Header.ClientTick;
                            var confirmation = new GetItemConfirmation(
                                    getItemOutcome.DestinationType,
                                    getItemOutcome.DestinationSlot,
                                    getItemOutcome.Item)
                                .ToFrame(codec, clientTick, frame.Header.KeywordIndex, 30_000);
                            await stream.WriteAsync(confirmation, cancellationToken);

                            var decay = new DecayItemConfirmation(getItemOutcome.WireItemId)
                                .ToFrame(codec, clientTick, frame.Header.KeywordIndex, 30_000);
                            foreach (var recipient in world.GetParticipantIdsInPlayerView(connectionId))
                                await world.SendAsync(recipient, decay, cancellationToken);

                            var itemFrame = new SendItemConfirmation(
                                    LegacyWorldItem.CarryDestinationType,
                                    checked((short)getItemOutcome.DestinationSlot),
                                    getItemOutcome.Item)
                                .ToFrame(codec, clientTick, frame.Header.KeywordIndex, checked((ushort)connectionId));
                            await stream.WriteAsync(itemFrame, cancellationToken);
                            Console.WriteLine($"Ground item collected: account={getItemSession.AccountName}, item={getItemOutcome.Item.Index}, wireId={getItemOutcome.WireItemId}, slot={getItemOutcome.DestinationSlot}.");
                        }
                        else
                            Console.WriteLine($"Ground item rejected: account={getItemSession.AccountName}, wireId={getItemRequest.ItemId}, reason={getItemResult}.");
                        continue;
                    }

                    if (MessageChatRequest.TryParse(frame, out var chatRequest) && chatRequest is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var chatSession) || chatSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine("Rejected chat frame: session is not in USER_PLAY.");
                            continue;
                        }

                        // TMProject 7.69 and W2PP consume these exact first-token commands. GuildDisable
                        // is volatile session state; SendScore projects Guild=0 without changing the saved MOB.
                        var chatCommand = chatRequest.Message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                        if (chatCommand is "guildon" or "guildoff")
                        {
                            var disabled = chatCommand == "guildoff";
                            var guildUpdate = world.TrySetGuildDisabled(connectionId, disabled);
                            if (guildUpdate is null)
                                continue;

                            var commandActorId = checked((ushort)connectionId);
                            var commandTick = frame.Header.ClientTick;
                            var commandKeyword = frame.Header.KeywordIndex;
                            var scoreFrame = BuildClientV769UpdateScoreFrame(
                                guildUpdate.ScoreMobSnapshot,
                                guildUpdate.AffectSnapshot,
                                commandActorId,
                                commandTick,
                                commandKeyword);
                            foreach (var recipient in guildUpdate.VisibleParticipantIds)
                                await world.SendAsync(recipient, scoreFrame, cancellationToken);

                            if (TryBuildClientV769UpdateAffectFrame(
                                    guildUpdate.AffectSnapshot,
                                    commandActorId,
                                    commandTick,
                                    commandKeyword,
                                    out var affectFrame) && affectFrame is not null)
                                await world.SendAsync(connectionId, affectFrame, cancellationToken);

                            var guildSignal = new GuildDisableConfirmation(disabled ? 1 : 0)
                                .ToFrame(LegacyFrameCodec.CreateDefault(), commandTick, commandKeyword);
                            await world.SendAsync(connectionId, guildSignal, cancellationToken);
                            Console.WriteLine($"Guild visibility changed: account={chatSession.AccountName}, disabled={disabled}, scoreRecipients={guildUpdate.VisibleParticipantIds.Count}.");
                            continue;
                        }

                        // Exec_MSG_MessageChat sets ID to the sender and calls GridMulticast
                        // around the sender, excluding the sender itself. Keep the original
                        // tick/keyword and only rebuild the encrypted header with that ID.
                        var chatMessage = world.ShouldMaskBattleRoyaleChat(connectionId)
                            ? LegacyBattleRoyaleArenaRules.ApplyChatMask(chatRequest.Message)
                            : chatRequest.Message;
                        var chatFrame = new MessageChatConfirmation(chatMessage)
                            .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, checked((ushort)connectionId));
                        foreach (var recipient in world.GetParticipantIdsInPlayerView(connectionId).Where(id => id != connectionId))
                            await world.SendAsync(recipient, chatFrame, cancellationToken);
                        Console.WriteLine($"Chat relayed: account={chatSession.AccountName}, recipients={world.GetParticipantIdsInPlayerView(connectionId).Count}, messageLength={chatRequest.Message.Length}.");
                        continue;
                    }

                    if (MessageWhisperRequest.TryParse(frame, out var whisperRequest) && whisperRequest is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var whisperSession) || whisperSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine("Rejected whisper frame: session is not in USER_PLAY.");
                            continue;
                        }

                        // The retail /cp command uses the whisper-shaped wire frame with
                        // MobName="cp" and an empty String.  TMSrv handles this before
                        // looking up a player target and reports GetPKPoint(conn)-75.
                        if (string.Equals(whisperRequest.TargetName, "cp", StringComparison.Ordinal))
                        {
                            if (!world.TryGetCharacterPkPoint(connectionId, out var pkPoint))
                            {
                                Console.WriteLine($"PK points query rejected: connection={connectionId}.");
                                continue;
                            }

                            var cpFrame = new MessagePanelConfirmation($"CP {pkPoint - 75}")
                                .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex);
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, cpFrame, cancellationToken, $"pk-points value={pkPoint - 75}");
                            Console.WriteLine($"PK points queried: account={whisperSession.AccountName}, value={pkPoint - 75}.");
                            continue;
                        }

                        if (!world.TryGetParticipantIdByCharacterName(whisperRequest.TargetName, out var targetConnectionId))
                        {
                            Console.WriteLine($"Whisper target not found: account={whisperSession.AccountName}, target={whisperRequest.TargetName}.");
                            continue;
                        }

                        if (!world.TryGetParticipantCharacterName(connectionId, out var senderName) || string.IsNullOrEmpty(senderName))
                        {
                            Console.WriteLine($"Whisper sender name unavailable: connection={connectionId}.");
                            continue;
                        }

                        var whisperFrame = new MessageWhisperConfirmation(senderName, whisperRequest.Message)
                            .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, checked((ushort)targetConnectionId));
                        await world.SendAsync(targetConnectionId, whisperFrame, cancellationToken);
                        Console.WriteLine($"Whisper delivered: account={whisperSession.AccountName}, target={whisperRequest.TargetName}, messageLength={whisperRequest.Message.Length}.");
                        continue;
                    }

                    if (QuestRequest.TryParse(frame, out var questRequest) && questRequest is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var questSession) || questSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine("Rejected quest frame: session is not in USER_PLAY.");
                            continue;
                        }

                        var perzenResult = world.TryExchangePerzenItem(connectionId, questRequest.NpcConnectionId, out var perzenOutcome, DateTime.Now);
                        if (perzenResult != LegacyPerzenExchangeResult.NotPerzen)
                        {
                            var codec = LegacyFrameCodec.CreateDefault();
                            var clientTick = frame.Header.ClientTick;
                            if (perzenResult == LegacyPerzenExchangeResult.Accepted && perzenOutcome is not null)
                            {
                                var itemFrame = new SendItemConfirmation(1, checked((short)perzenOutcome.InventorySlot), perzenOutcome.UpdatedItem)
                                    .ToFrame(codec, clientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                await stream.WriteAsync(itemFrame, cancellationToken);
                                Console.WriteLine($"Perzen exchange accepted: account={questSession.AccountName}, npc={questRequest.NpcConnectionId}, slot={perzenOutcome.InventorySlot}, required={perzenOutcome.RequiredItemIndex}, reward={perzenOutcome.RewardItemIndex}.");
                            }
                            else if (perzenResult == LegacyPerzenExchangeResult.MissingItem && perzenOutcome is not null)
                            {
                                var itemName = itemData?[perzenOutcome.RequiredItemIndex]?.Name;
                                var chatText = string.IsNullOrWhiteSpace(itemName)
                                    ? $"Traga o item {perzenOutcome.RequiredItemIndex}."
                                    : $"Traga {itemName}.";
                                var chat = new NpcChatConfirmation(chatText)
                                    .ToFrame(codec, clientTick, (byte)RandomNumberGenerator.GetInt32(256), checked((ushort)perzenOutcome.NpcConnectionId));
                                foreach (var recipient in world.GetParticipantIdsInNpcView(perzenOutcome.NpcConnectionId))
                                    await world.SendAsync(recipient, chat, cancellationToken);
                                Console.WriteLine($"Perzen exchange rejected: account={questSession.AccountName}, npc={questRequest.NpcConnectionId}, reason=missing item {perzenOutcome.RequiredItemIndex}.");
                            }
                            else
                                Console.WriteLine($"Perzen exchange rejected: connection={connectionId}, npc={questRequest.NpcConnectionId}, reason={perzenResult}.");
                            continue;
                        }

                        var registrationResult = world.TryRegisterPistaParty(connectionId, questRequest.NpcConnectionId, out var registration);
                        if (registrationResult == LegacyPistaRegistrationResult.Accepted && registration is not null)
                        {
                            var codec = LegacyFrameCodec.CreateDefault();
                            var clientTick = frame.Header.ClientTick;
                            var itemFrame = new SendItemConfirmation(1, checked((short)registration.InventorySlot), registration.UpdatedItem)
                                .ToFrame(codec, clientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                            await stream.WriteAsync(itemFrame, cancellationToken);
                            var notice = new MessagePanelConfirmation("Ingresso da Pista utilizado.")
                                .ToFrame(codec, clientTick, (byte)RandomNumberGenerator.GetInt32(256));
                            await stream.WriteAsync(notice, cancellationToken);
                            Console.WriteLine($"Pista party registered: account={questSession.AccountName}, level={registration.Level}, slot={registration.PartySlot}, ticketSlot={registration.InventorySlot}.");
                        }
                        else
                            Console.WriteLine($"Pista registration rejected: connection={connectionId}, npc={questRequest.NpcConnectionId}, reason={registrationResult}.");
                        continue;
                    }

                    if (PartyInviteRequest.TryParse(frame, out var partyInvite) && partyInvite is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var partyInviteSession) || partyInviteSession!.State != LoginSessionState.Playing || partyInvite.PartyId != connectionId)
                        {
                            Console.WriteLine("Rejected party-invite frame: session or leader id is invalid.");
                            continue;
                        }

                        var check = world.TryPreparePartyInvite(connectionId, partyInvite.TargetConnectionId, out var partyPlan);
                        if (check != PartyInviteCheckResult.Accepted || partyPlan is null)
                        {
                            Console.WriteLine($"Party invite rejected: connection={connectionId}, target={partyInvite.TargetConnectionId}, reason={check}.");
                            continue;
                        }

                        var codec = LegacyFrameCodec.CreateDefault();
                        var inviteFrame = new PartyInviteRequest(
                            checked((byte)partyPlan.Leader.CharacterClass),
                            0,
                            checked((short)Math.Clamp(partyPlan.Leader.Level, short.MinValue, short.MaxValue)),
                            checked((short)Math.Clamp(partyPlan.Leader.MaxHp, short.MinValue, short.MaxValue)),
                            checked((short)Math.Clamp(partyPlan.Leader.Hp, short.MinValue, short.MaxValue)),
                            checked((short)partyPlan.LeaderConnectionId),
                            partyPlan.Leader.MobName,
                            0,
                            0).ToFrame(codec, unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), 30000);
                        await world.SendAsync(partyPlan.TargetConnectionId, inviteFrame, cancellationToken);
                        Console.WriteLine($"Party invite sent: leader={connectionId}, target={partyPlan.TargetConnectionId}.");
                        continue;
                    }

                    if (PartyAcceptRequest.TryParse(frame, out var partyAccept) && partyAccept is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var partyAcceptSession) || partyAcceptSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine("Rejected party-accept frame: session is not in USER_PLAY.");
                            continue;
                        }

                        await using var partyAccountLease = await AcquirePartyAccountLeaseAsync(
                            accountOperationGate,
                            world,
                            partyAccept.LeaderConnectionId,
                            cancellationToken,
                            connectionId);
                        var result = world.TryAcceptParty(connectionId, partyAccept.LeaderConnectionId, partyAccept.MobName, out var formation);
                        await partyAccountLease.DisposeAsync();
                        if (result != PartyAcceptResult.Accepted || formation is null)
                        {
                            Console.WriteLine($"Party accept rejected: connection={connectionId}, leader={partyAccept.LeaderConnectionId}, reason={result}.");
                            continue;
                        }

                        var codec = LegacyFrameCodec.CreateDefault();
                        foreach (var viewer in formation.Members)
                        foreach (var member in formation.Members)
                        {
                            var leaderConnection = member.ConnectionId == formation.LeaderConnectionId ? member.ConnectionId : 30000;
                            var addFrame = new PartyAddConfirmation(
                                checked((short)leaderConnection),
                                checked((short)Math.Clamp(member.Level, short.MinValue, short.MaxValue)),
                                checked((short)Math.Clamp(member.MaxHp > short.MaxValue ? (member.MaxHp + 1) / 100 : member.MaxHp, short.MinValue, short.MaxValue)),
                                checked((short)Math.Clamp(member.Hp > short.MaxValue ? (member.Hp + 1) / 100 : member.Hp, short.MinValue, short.MaxValue)),
                                checked((short)member.ConnectionId),
                                member.MobName,
                                unchecked((short)52428)).ToFrame(codec, unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), 30000);
                            await world.SendAsync(viewer.ConnectionId, addFrame, cancellationToken);
                        }
                        Console.WriteLine($"Party accepted: leader={formation.LeaderConnectionId}, member={connectionId}, size={formation.Members.Count}.");
                        continue;
                    }

                    if (PartyRemoveRequest.TryParse(frame, out var partyRemove) && partyRemove is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var partyRemoveSession) || partyRemoveSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine("Rejected party-remove frame: session is not in USER_PLAY.");
                            continue;
                        }

                        await using var partyAccountLease = await AcquirePartyAccountLeaseAsync(
                            accountOperationGate,
                            world,
                            connectionId,
                            cancellationToken);
                        var partyRemoved = world.TryRemovePartyMember(connectionId, partyRemove.TargetConnectionId, out var removal);
                        await partyAccountLease.DisposeAsync();
                        if (!partyRemoved || removal is null)
                        {
                            Console.WriteLine($"Party remove ignored: connection={connectionId}, target={partyRemove.TargetConnectionId}.");
                            continue;
                        }

                        var codec = LegacyFrameCodec.CreateDefault();
                        foreach (var viewer in removal.NotifiedConnectionIds)
                        {
                            var removeFrame = new PartyRemoveConfirmation((short)(removal.Disbanded ? 0 : removal.RemovedConnectionId))
                                .ToFrame(codec, unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), 30000);
                            await world.SendAsync(viewer, removeFrame, cancellationToken);
                        }
                        Console.WriteLine($"Party removal applied: requester={connectionId}, removed={removal.RemovedConnectionId}, disbanded={removal.Disbanded}.");
                        continue;
                    }

                    if (InviteGuildRequest.TryParse(frame, out var invite) && invite is not null)
                    {
                        if (accounts is null || !sessions.TryGet(connectionId, out var inviteSession) || inviteSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine("Rejected guild-invite frame: session is not in USER_PLAY.");
                            continue;
                        }

                        var check = world.TryPrepareGuildInvite(connectionId, invite.TargetConnectionId, invite.InviteType, out var plan);
                        if (check != GuildInviteCheckResult.Accepted || plan is null)
                        {
                            Console.WriteLine($"Guild invite rejected: connection={connectionId}, target={invite.TargetConnectionId}, reason={check}.");
                            continue;
                        }

                        if (accounts is not IAtomicCharacterGuildInviteStore guildInviteStore)
                        {
                            Console.WriteLine("Guild invite persistence failed: the configured store does not support pair-atomic updates.");
                            continue;
                        }

                        await using var accountLease = await accountOperationGate.AcquireAsync(
                            [plan.SourceAccountName, plan.TargetAccountName], cancellationToken);
                        var lockedCheck = world.TryPrepareGuildInvite(connectionId, invite.TargetConnectionId, invite.InviteType, out var lockedPlan);
                        if (lockedCheck != GuildInviteCheckResult.Accepted || lockedPlan is null ||
                            lockedPlan.SourceCharacterSlot != plan.SourceCharacterSlot ||
                            lockedPlan.TargetCharacterSlot != plan.TargetCharacterSlot ||
                            !string.Equals(lockedPlan.SourceAccountName, plan.SourceAccountName, StringComparison.Ordinal) ||
                            !string.Equals(lockedPlan.TargetAccountName, plan.TargetAccountName, StringComparison.Ordinal))
                        {
                            Console.WriteLine($"Guild invite changed before persistence: connection={connectionId}, target={invite.TargetConnectionId}, reason={lockedCheck}.");
                            continue;
                        }
                        plan = lockedPlan;
                        if (!LegacyGuildInvitePersistence.TryBuild(plan, out var persistenceRequest) || persistenceRequest is null)
                        {
                            Console.WriteLine("Guild invite persistence failed: invalid expected-value pair.");
                            continue;
                        }

                        var persistenceResult = await guildInviteStore.TrySaveGuildInvitePairAsync(persistenceRequest, cancellationToken);
                        var durableCommitConfirmed = persistenceResult == GuildInvitePairSaveResult.Success;
                        if (persistenceResult == GuildInvitePairSaveResult.Ambiguous)
                        {
                            try
                            {
                                var sourceData = await accounts.ReadCharacterLoginDataAsync(plan.SourceAccountName, plan.SourceCharacterSlot, cancellationToken);
                                var targetData = await accounts.ReadCharacterLoginDataAsync(plan.TargetAccountName, plan.TargetCharacterSlot, cancellationToken);
                                durableCommitConfirmed = sourceData is not null && targetData is not null &&
                                    BinaryPrimitives.ReadUInt16LittleEndian(sourceData.Mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset)) == plan.GuildId &&
                                    BinaryPrimitives.ReadInt32LittleEndian(sourceData.Mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset)) == plan.SourceCoin - plan.Cost &&
                                    BinaryPrimitives.ReadUInt16LittleEndian(targetData.Mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset)) == plan.GuildId &&
                                    BinaryPrimitives.ReadInt32LittleEndian(targetData.Mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset)) == plan.TargetCoin;
                            }
                            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Data.Common.DbException)
                            {
                                Console.WriteLine($"Guild invite commit outcome remains unknown: accounts={plan.SourceAccountName}/{plan.TargetAccountName}, error={error.GetType().Name}.");
                            }
                        }
                        if (!durableCommitConfirmed)
                        {
                            Console.WriteLine($"Guild invite persistence did not confirm both sides: result={persistenceResult}.");
                            continue;
                        }
                        if (!world.ApplyGuildInvite(plan))
                        {
                            Console.WriteLine($"Guild invite committed but runtime reconciliation failed: guild={plan.GuildId}, source={plan.SourceAccountName}, target={plan.TargetAccountName}.");
                            continue;
                        }

                        await accountLease.DisposeAsync();

                        var codec = LegacyFrameCodec.CreateDefault();
                        var clientTick = unchecked((uint)Environment.TickCount64);
                        if (world.TryBuildUpdateEtcFrame(plan.SourceConnectionId, codec, clientTick, (byte)RandomNumberGenerator.GetInt32(256), out var sourceEtc) && sourceEtc is not null)
                            await world.SendAsync(plan.SourceConnectionId, sourceEtc, cancellationToken);

                        var notice = new MessagePanelConfirmation("Voce entrou na guilda.").ToFrame(codec, clientTick, (byte)RandomNumberGenerator.GetInt32(256));
                        await world.SendAsync(plan.TargetConnectionId, notice, cancellationToken);
                        if (world.TryBuildCreateMobFrame(plan.TargetConnectionId, codec, clientTick, (byte)RandomNumberGenerator.GetInt32(256), out var targetCreate) && targetCreate is not null)
                        {
                            await world.SendAsync(plan.TargetConnectionId, targetCreate, cancellationToken);
                            await world.BroadcastAsync(plan.TargetConnectionId, targetCreate, cancellationToken);
                        }
                        Console.WriteLine($"Guild invite accepted: guild={plan.GuildId}, source={plan.SourceAccountName}, target={plan.TargetAccountName}, cost={plan.Cost}.");
                        continue;
                    }

                    if (GuildAllyRequest.TryParse(frame, out var guildAllyRequest) && guildAllyRequest is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var guildAllySession) || guildAllySession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine("Rejected GuildAlly frame: session is not in USER_PLAY.");
                            continue;
                        }

                        using (await GuildWarTransitionRelayGate.Shared.AcquireAsync(cancellationToken))
                        {
                            if (!world.TryApplyGuildAllyRequest(connectionId, guildAllyRequest.GuildId, guildAllyRequest.AllyGuildId, out var guildAllyUpdate) || guildAllyUpdate is null)
                            {
                                Console.WriteLine($"Rejected GuildAlly request: connection={connectionId}, requestedGuild={guildAllyRequest.GuildId}, ally={guildAllyRequest.AllyGuildId}, reason=active-character-authorization.");
                                continue;
                            }

                            var guildAllyCodec = LegacyFrameCodec.CreateDefault();
                            foreach (var delivery in guildAllyUpdate.NoticeDeliveries)
                            {
                                var notice = delivery.Notice;
                                var guildName = guildNameCatalog.GetGuildName(notice.GuildId);
                                var allyGuildName = guildNameCatalog.GetGuildName(notice.AllyGuildId);
                                var message = notice.Kind switch
                                {
                                    LegacyGuildAllyNoticeKind.Declared => LegacyGuildAllyMessages.Declared(guildName, allyGuildName),
                                    LegacyGuildAllyNoticeKind.Cancelled => LegacyGuildAllyMessages.Cancelled(guildName, allyGuildName),
                                    _ => throw new InvalidOperationException($"Unsupported GuildAlly notice kind {notice.Kind}."),
                                };
                                var noticeFrame = new MessagePanelConfirmation(message)
                                    .ToFrame(guildAllyCodec, frame.Header.ClientTick, frame.Header.KeywordIndex);
                                foreach (var recipient in delivery.RecipientConnectionIds)
                                    await world.SendAsync(recipient, noticeFrame, cancellationToken);
                            }

                            foreach (var update in guildAllyUpdate.SendWarInfoUpdates)
                            {
                                var warInfoFrame = new SendWarInfoConfirmation(update.OpponentGuildId, update.Clan, update.AllyGuildId)
                                    .ToFrame(guildAllyCodec, frame.Header.ClientTick, frame.Header.KeywordIndex);
                                await world.SendAsync(update.ConnectionId, warInfoFrame, cancellationToken);
                            }

                            Console.WriteLine($"GuildAlly request applied: account={guildAllySession.AccountName}, guild={guildAllyRequest.GuildId}, ally={guildAllyRequest.AllyGuildId}, status={guildAllyUpdate.Transition.Status}, sendWarInfo={guildAllyUpdate.SendWarInfoUpdates.Count}.");
                        }
                        continue;
                    }

                    if (GuildWarRequest.TryParse(frame, out var guildWarRequest) && guildWarRequest is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var guildWarSession) || guildWarSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine("Rejected GuildWar frame: session is not in USER_PLAY.");
                            continue;
                        }

                        using (await GuildWarTransitionRelayGate.Shared.AcquireAsync(cancellationToken))
                        {
                            if (!world.TryApplyGuildWarRequest(connectionId, guildWarRequest.GuildId, guildWarRequest.EnemyGuildId, out var guildWarUpdate) || guildWarUpdate is null)
                            {
                                Console.WriteLine($"Rejected GuildWar request: connection={connectionId}, requestedGuild={guildWarRequest.GuildId}, enemyGuild={guildWarRequest.EnemyGuildId}, reason=active-character-authorization.");
                                continue;
                            }

                            var guildWarCodec = LegacyFrameCodec.CreateDefault();
                            foreach (var delivery in guildWarUpdate.NoticeDeliveries)
                            {
                                var notice = delivery.Notice;
                                var guildName = guildNameCatalog.GetGuildName(notice.GuildId);
                                var enemyGuildName = guildNameCatalog.GetGuildName(notice.EnemyGuildId);
                                var message = notice.Kind switch
                                {
                                    LegacyGuildWarNoticeKind.Declaration => LegacyGuildWarMessages.Declaration(guildName, enemyGuildName),
                                    LegacyGuildWarNoticeKind.DeclarationCancelled => LegacyGuildWarMessages.DeclarationCancelled(guildName, enemyGuildName),
                                    LegacyGuildWarNoticeKind.WarCancelled => LegacyGuildWarMessages.WarCancelled(guildName, enemyGuildName),
                                    LegacyGuildWarNoticeKind.WarStarted => LegacyGuildWarMessages.WarStarted(guildName, enemyGuildName),
                                    _ => throw new InvalidOperationException($"Unsupported GuildWar notice kind {notice.Kind}."),
                                };
                                var noticeFrame = new MessagePanelConfirmation(message)
                                    .ToFrame(guildWarCodec, frame.Header.ClientTick, frame.Header.KeywordIndex);
                                foreach (var recipient in delivery.RecipientConnectionIds)
                                    await world.SendAsync(recipient, noticeFrame, cancellationToken);
                            }

                            foreach (var update in guildWarUpdate.SendWarInfoUpdates)
                            {
                                var warInfoFrame = new SendWarInfoConfirmation(update.OpponentGuildId, update.Clan, update.AllyGuildId)
                                    .ToFrame(guildWarCodec, frame.Header.ClientTick, frame.Header.KeywordIndex);
                                await world.SendAsync(update.ConnectionId, warInfoFrame, cancellationToken);
                            }

                            Console.WriteLine($"GuildWar request applied: account={guildWarSession.AccountName}, guild={guildWarRequest.GuildId}, enemy={guildWarRequest.EnemyGuildId}, status={guildWarUpdate.Transition.Status}, notices={guildWarUpdate.NoticeDeliveries.Count}, sendWarInfo={guildWarUpdate.SendWarInfoUpdates.Count}.");
                        }
                        continue;
                    }

                    if (ActionRequest.TryParse(frame, out var action) && action is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var actionSession) || actionSession is null ||
                            !LegacyMovementRules.ShouldContinueAction(actionSession.State))
                        {
                            Console.WriteLine($"Rejected action: connection={connectionId}, reason={MovementResult.InvalidState}.");
                            continue;
                        }

                        // Action/Action2/Action3 share the same HP==0 gate in both references.
                        var hasActionResources = world.TryGetResourceState(connectionId, out var actionResources, out _, out _);
                        if (!hasActionResources || actionResources is null)
                        {
                            Console.WriteLine($"Rejected action: connection={connectionId}, reason=AuthoritativeStateUnavailable.");
                            continue;
                        }

                        var actionGate = LegacyMovementRules.ResolveActionPreconditions(
                            actionResources.CurrentScore.Hp,
                            actionResources.CurrentScore.AttackRun,
                            out var allowedSpeed);
                        if (actionGate == LegacyMovementRules.ActionGateResult.CharacterDead)
                        {
                            await stream.WriteAsync(new SetHpModeConfirmation(0, (short)actionSession.State)
                                .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, (ushort)connectionId), cancellationToken);
                            await RecordCrackErrorAsync(LegacyCrackErrorRules.ActionPoints, LegacyCrackErrorRules.ActionType);
                            continue;
                        }

                        if (actionGate != LegacyMovementRules.ActionGateResult.Ready)
                        {
                            Console.WriteLine($"Rejected action: connection={connectionId}, reason=AuthoritativeStateUnavailable.");
                            continue;
                        }

                        // RemoveTrade also resets TradeMode/AutoTrade when either side
                        // owns an active AFK shop. Close that durable state together
                        // with the bilateral pair before relaying the normal MOB.
                        var actionTradeOpponentId = world.GetTradeOpponentId(connectionId);
                        var actionHasShop = autoTradeBook.HasListing(connectionId) ||
                            actionTradeOpponentId > 0 && autoTradeBook.HasListing(actionTradeOpponentId);
                        if (actionHasShop)
                        {
                            var actionClose = await LegacyAutoTradeCloseCoordinator.CloseForActionAndCaptureAsync(
                                connectionId, sessions, world, autoTradeBook, autoTradeStateStore,
                                LegacyFrameCodec.CreateDefault(), cancellationToken);
                            var closeOutcome = actionClose.Outcome;
                            if (closeOutcome.Result == LegacyAutoTradeCloseResult.NotAlive)
                            {
                                if (world.TryGetResourceState(connectionId, out var closeActionResources, out _, out _) && closeActionResources is not null)
                                    await stream.WriteAsync(new SetHpModeConfirmation(closeActionResources.CurrentScore.Hp, (short)actionSession.State)
                                        .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, (ushort)connectionId), cancellationToken);
                                await RecordCrackErrorAsync(LegacyCrackErrorRules.ActionPoints, LegacyCrackErrorRules.ActionType);
                                continue;
                            }

                            if (closeOutcome.Result == LegacyAutoTradeCloseResult.NoShop && closeOutcome.Trade is null &&
                                closeOutcome.ClosedListings.Count == 0)
                            {
                                // A concurrent close removed the observed listing. Fall through
                                // to the atomic bilateral check below in case the pair changed.
                            }
                            else if ((closeOutcome.Result is LegacyAutoTradeCloseResult.Closed or LegacyAutoTradeCloseResult.NoShop) &&
                                actionClose.Frames.Count > 0)
                            {
                                foreach (var plan in actionClose.Frames)
                                    foreach (var recipient in plan.RecipientIds.Distinct())
                                        if (recipient == connectionId) await stream.WriteAsync(plan.Frame, cancellationToken);
                                        else await world.SendAsync(recipient, plan.Frame, cancellationToken);
                                Console.WriteLine($"Action closed AFK shop/trade state: connection={connectionId}, opponent={closeOutcome.Trade?.OpponentId ?? 0}, listings={closeOutcome.ClosedListings.Count}.");
                                continue;
                            }
                            else
                            {
                                Console.WriteLine($"Action stopped during AFK shop close: connection={connectionId}, reason={closeOutcome.Result}, frames={actionClose.Frames.Count}.");
                                continue;
                            }
                        }

                        // In both legacy sources, a non-zero-HP Action first cancels
                        // an active bilateral trade and returns without movement.
                        var actionTradeResult = world.TryCancelTradeForAction(connectionId, out var actionTrade);
                        if (actionTradeResult == LegacyActionTradeCancellationResult.CharacterDead)
                        {
                            await stream.WriteAsync(new SetHpModeConfirmation(0, (short)actionSession.State)
                                .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, (ushort)connectionId), cancellationToken);
                            await RecordCrackErrorAsync(LegacyCrackErrorRules.ActionPoints, LegacyCrackErrorRules.ActionType);
                            continue;
                        }
                        if (actionTradeResult == LegacyActionTradeCancellationResult.Cancelled && actionTrade is not null)
                        {
                            var tradeSignalCodec = LegacyFrameCodec.CreateDefault();
                            var opponentSignal = TradeCloseRequest.ToFrame(
                                tradeSignalCodec, 0, 0, checked((ushort)actionTrade.OpponentId));
                            await world.SendAsync(actionTrade.OpponentId, opponentSignal, cancellationToken);
                            var ownSignal = TradeCloseRequest.ToFrame(
                                tradeSignalCodec, 0, 0, checked((ushort)connectionId));
                            await stream.WriteAsync(ownSignal, cancellationToken);
                            Console.WriteLine($"Action cancelled bilateral trade: connection={connectionId}, opponent={actionTrade.OpponentId}.");
                            continue;
                        }
                        if (actionTradeResult != LegacyActionTradeCancellationResult.NoActiveTrade)
                        {
                            Console.WriteLine($"Action stopped during trade cancellation: connection={connectionId}, reason={actionTradeResult}.");
                            continue;
                        }

                        // 7.69 recalls from these origin sectors before the Action speed/time/range checks.
                        if (world.TryRecallForActionOriginWithViews(connectionId, out var originRecallTransition) && originRecallTransition is not null)
                        {
                            var originRecall = originRecallTransition.Recall;
                            sessions.SetServerPosition(connectionId, originRecall.ToX, originRecall.ToY);
                            var recallTick = unchecked((uint)Environment.TickCount64);
                            var recallKeyword = (byte)RandomNumberGenerator.GetInt32(256);
                            var recallCodec = LegacyFrameCodec.CreateDefault();
                            var recallFrame = new ActionRequest(
                                originRecall.FromX,
                                originRecall.FromY,
                                Effect: 1,
                                Speed: 2,
                                Route: new byte[24],
                                originRecall.ToX,
                                originRecall.ToY)
                                .ToFrame(recallCodec, recallTick, recallKeyword, (ushort)connectionId);
                            if (!LegacyRecallRelay.TryBuild(
                                world,
                                originRecallTransition,
                                recallFrame,
                                recallCodec,
                                recallTick,
                                recallKeyword,
                                out var recallRelayPlans))
                                Console.Error.WriteLine($"Action origin recall visibility plan failed: connection={connectionId}; no global relay was sent.");

                            await stream.WriteAsync(recallFrame, cancellationToken);
                            if (recallRelayPlans is not null)
                                foreach (var plan in recallRelayPlans)
                                    await world.SendAsync(plan.RecipientId, plan.Frame, cancellationToken);
                            Console.WriteLine($"Action origin recalled: account={actionSession.AccountName}, from=({originRecall.FromX},{originRecall.FromY}), to=({originRecall.ToX},{originRecall.ToY}).");
                            continue;
                        }

                        // Both TMSrv references reject a stop immediately after another stop;
                        // LastMoveType resets on login; accepted moves and occupied-destination corrections match the legacy assignment.
                        if (action.WireType == ActionRequest.StopMessageType &&
                            actionSession.LastMoveType == ActionRequest.StopMessageType)
                        {
                            await RecordCrackErrorAsync(LegacyCrackErrorRules.ActionStopPoints, LegacyCrackErrorRules.ActionStopType);
                            Console.WriteLine($"Rejected consecutive Action2: connection={connectionId}.");
                            continue;
                        }

                        // The 7.69 and W2PP Action branch validates its client clock here;
                        // Action2 bypasses this branch; Action3 checks eligibility before its own tick gate.
                        if (action.WireType == ActionRequest.MessageType)
                        {
                            var actionServerTick = unchecked((uint)Environment.TickCount64);
                            var oldestActionTick = actionServerTick <= 120_000u ? 0u : actionServerTick - 120_000u;
                            if (frame.Header.ClientTick > unchecked(actionServerTick + 15_000u) ||
                                frame.Header.ClientTick < oldestActionTick)
                            {
                                await RecordCrackErrorAsync(LegacyCrackErrorRules.ActionTickPoints, LegacyCrackErrorRules.ActionTickType);
                                Console.WriteLine($"Rejected Action timestamp: connection={connectionId}, clientTick={frame.Header.ClientTick}, serverTick={actionServerTick}.");
                                continue;
                            }
                        }

                        // 7.69 and W2PP apply the Action3 class/skill penalty before this tick check.
                        // Below MAX_LEVEL, the packet continues unless the shared crack threshold logs out.
                        if (action.WireType == ActionRequest.IllusionMessageType &&
                            LegacyIllusionRules.ShouldRecordEligibilityError(
                                actionResources.CharacterClass,
                                actionResources.LearnedSkill,
                                actionResources.CurrentScore.Level))
                        {
                            var eligibilityError = await RecordCrackErrorAsync(
                                LegacyCrackErrorRules.ActionIllusionEligibilityPoints,
                                LegacyCrackErrorRules.ActionIllusionEligibilityType);
                            if (eligibilityError?.RequiresCharacterLogout == true)
                                continue;
                        }

                        // 7.69 charges the fixed skill-73 mana cost before the Action3 tick gate.
                        // Its ReqMp decrement is commented out; keep the separately tracked value unchanged.
                        if (action.WireType == ActionRequest.IllusionMessageType)
                        {
                            var illusionSpell = skills?[LegacyIllusionRules.ManaCostSkillId];
                            if (illusionSpell is null || illusionSpell.ManaSpent < 0)
                            {
                                Console.WriteLine($"Rejected Action3: skill-73 mana data unavailable or invalid for connection={connectionId}.");
                                continue;
                            }

                            var mana = world.TryConsumeFixedMana(
                                connectionId,
                                illusionSpell.ManaSpent,
                                out _,
                                out _,
                                out _);
                            if (mana == LegacySkillManaResult.InsufficientMana)
                            {
                                if (world.TryGetResourceState(connectionId, out var resourceState, out var requestedHp, out var requestedMp) && resourceState is not null)
                                {
                                    var sync = new SetHpMpConfirmation(resourceState.CurrentScore.Hp, resourceState.CurrentMana, requestedHp, requestedMp)
                                        .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                    await stream.WriteAsync(sync, cancellationToken);
                                }
                                Console.WriteLine($"Rejected Action3: account={actionSession.AccountName}, reason={mana}.");
                                continue;
                            }
                            if (mana != LegacySkillManaResult.Accepted)
                            {
                                Console.WriteLine($"Rejected Action3: account={actionSession.AccountName}, reason={mana}.");
                                continue;
                            }
                        }

                        // Illusion uses the same client-tick window, with its own legacy error type (104).
                        if (action.WireType == ActionRequest.IllusionMessageType)
                        {
                            var illusionServerTick = unchecked((uint)Environment.TickCount64);
                            var oldestIllusionTick = illusionServerTick <= 120_000u ? 0u : illusionServerTick - 120_000u;
                            if (frame.Header.ClientTick > unchecked(illusionServerTick + 15_000u) ||
                                frame.Header.ClientTick < oldestIllusionTick)
                            {
                                await RecordCrackErrorAsync(
                                    LegacyCrackErrorRules.ActionIllusionTickPoints,
                                    LegacyCrackErrorRules.ActionIllusionTickType);
                                Console.WriteLine($"Rejected Action3 timestamp: connection={connectionId}, clientTick={frame.Header.ClientTick}, serverTick={illusionServerTick}.");
                                continue;
                            }
                        }

                        var requestedSpeed = action.Speed;
                        var attackRun = actionResources.CurrentScore.AttackRun;
                        if (LegacyMovementRules.RequiresAccountAnalysis(requestedSpeed, attackRun))
                        {
                            var crackError = await RecordCrackErrorAsync(
                                LegacyCrackErrorRules.ActionSpeedPoints,
                                LegacyCrackErrorRules.ActionSpeedType,
                                deferCharacterLogout: true);
                            var reviewAccount = actionSession.AccountName ?? string.Empty;
                            world.TryGetParticipantCharacterName(connectionId, out var reviewCharacter);
                            var remoteAddress = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString();
                            var result = actionAccountReview is null
                                ? ActionAccountReviewResult.PersistenceUnavailable
                                : await actionAccountReview.ReviewSpeedViolationAsync(
                                    reviewAccount,
                                    reviewCharacter,
                                    remoteAddress,
                                    requestedSpeed,
                                    allowedSpeed,
                                    attackRun,
                                    cancellationToken);
                            var marked = result is ActionAccountReviewResult.Marked or ActionAccountReviewResult.AlreadyMarked;
                            var notice = AccountLoginFailureNotice.ForReview(marked
                                ? AccountLoginRejection.AccountUnderAnalysis
                                : AccountLoginRejection.ReviewStoreUnavailable);
                            var noticeFrame = new MessagePanelConfirmation(notice).ToFrame(
                                LegacyFrameCodec.CreateDefault(),
                                unchecked((uint)Environment.TickCount64),
                                (byte)RandomNumberGenerator.GetInt32(256));
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, noticeFrame, cancellationToken,
                                marked ? "account-review-analysis" : "account-review-write-failure");
                            if (marked)
                            {
                                Console.WriteLine($"Account marked for Analysis: connection={connectionId}, account={reviewAccount}, character={reviewCharacter ?? "unknown"}, reason={FileAccountReviewStore.HighSeverityActionSpeedReason}, requested={requestedSpeed}, allowed={allowedSpeed}, attackRun={attackRun}, remote={remoteAddress ?? "unknown"}, crackLogoutThreshold={crackError?.RequiresCharacterLogout ?? false}, result={result}.");
                            }
                            else
                            {
                                Console.Error.WriteLine($"Account Analysis persistence failed: connection={connectionId}, account={reviewAccount}, reason={FileAccountReviewStore.HighSeverityActionSpeedReason}, result={result}; connection closed without movement relay.");
                            }
                            return;
                        }

                        if (action.Speed > allowedSpeed)
                        {
                            await RecordCrackErrorAsync(LegacyCrackErrorRules.ActionSpeedPoints, LegacyCrackErrorRules.ActionSpeedType);
                            if (!sessions.TryGet(connectionId, out var speedSession) || !LegacyMovementRules.ShouldContinueAction(speedSession?.State))
                                continue;
                            Console.WriteLine($"Action speed corrected: connection={connectionId}, attackRun={actionResources.CurrentScore.AttackRun}, requested={requestedSpeed}, allowed={allowedSpeed}.");
                            action = action with { Speed = allowedSpeed };
                        }

                        // Pista entry and other server teleports mutate WorldHub first. Sync the
                        // per-connection movement gate before applying the next client action.
                        var rangeX = actionSession.PositionX;
                        var rangeY = actionSession.PositionY;
                        if (world.TryGetCharacterSnapshot(connectionId, out _, out var authoritativeX, out var authoritativeY))
                        {
                            rangeX = authoritativeX;
                            rangeY = authoritativeY;
                            sessions.SetServerPosition(connectionId, authoritativeX, authoritativeY);
                        }

                        // Both references reject >33 before destination-map work and correct privately only at >66.
                        // GetAction starts from the authoritative position; the rejected client's origin/route are not echoed.
                        if (!LegacyMovementRules.IsTargetWithinViewGrid(rangeX, rangeY, action.TargetX, action.TargetY))
                        {
                            if (LegacyMovementRules.RequiresPositionCorrection(rangeX, rangeY, action.TargetX, action.TargetY))
                            {
                                var correction = new ActionRequest(rangeX, rangeY, 1, 6, new byte[24], rangeX, rangeY)
                                {
                                    WireType = ActionRequest.IllusionMessageType,
                                };
                                await stream.WriteAsync(correction.ToFrame(LegacyFrameCodec.CreateDefault(),
                                    unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256),
                                    (ushort)connectionId), cancellationToken);
                            }
                            await RecordCrackErrorAsync(LegacyCrackErrorRules.ActionRangePoints, LegacyCrackErrorRules.ActionRangeType);
                            continue;
                        }

                        var mapRestriction = world.EvaluateMovementMapTarget(connectionId, action.TargetX, action.TargetY);
                        if (mapRestriction != LegacyMovementMapRestriction.None)
                        {
                            var restrictionNotice = LegacyMovementMapNotice.For(mapRestriction);
                            if (restrictionNotice is not null)
                            {
                                var noticeFrame = new MessagePanelConfirmation(restrictionNotice)
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
                                await stream.WriteAsync(noticeFrame, cancellationToken);
                            }

                            if (world.TryRecallForMovementRestrictionWithViews(connectionId, out var recallTransition) && recallTransition is not null)
                            {
                                var recall = recallTransition.Recall;
                                sessions.SetServerPosition(connectionId, recall.ToX, recall.ToY);
                                var recallTick = unchecked((uint)Environment.TickCount64);
                                var recallKeyword = (byte)RandomNumberGenerator.GetInt32(256);
                                var recallCodec = LegacyFrameCodec.CreateDefault();
                                var recallFrame = new ActionRequest(
                                    recall.FromX,
                                    recall.FromY,
                                    Effect: 1,
                                    Speed: 2,
                                    Route: new byte[24],
                                    recall.ToX,
                                    recall.ToY)
                                    .ToFrame(recallCodec, recallTick, recallKeyword, (ushort)connectionId);
                                if (!LegacyRecallRelay.TryBuild(
                                    world,
                                    recallTransition,
                                    recallFrame,
                                    recallCodec,
                                    recallTick,
                                    recallKeyword,
                                    out var recallRelayPlans))
                                    Console.Error.WriteLine($"Movement recall visibility plan failed: connection={connectionId}; no global relay was sent.");

                                await stream.WriteAsync(recallFrame, cancellationToken);
                                if (recallRelayPlans is not null)
                                    foreach (var plan in recallRelayPlans)
                                        await world.SendAsync(plan.RecipientId, plan.Frame, cancellationToken);
                                Console.WriteLine($"Movement recalled: account={actionSession.AccountName}, reason={mapRestriction}, from=({recall.FromX},{recall.FromY}), to=({recall.ToX},{recall.ToY}).");
                            }
                            else
                                Console.WriteLine($"Movement rejected: account={actionSession.AccountName}, reason={mapRestriction}, recall unavailable.");
                            continue;
                        }

                        if (world.TryCorrectOccupiedAction(connectionId, action, out var correctedAction) && correctedAction is not null)
                        {
                            sessions.TryRecordMovementType(connectionId, action.WireType);
                            var correctedFrame = correctedAction.ToFrame(
                                LegacyFrameCodec.CreateDefault(),
                                frame.Header.ClientTick,
                                (byte)RandomNumberGenerator.GetInt32(256),
                                (ushort)connectionId);
                            await stream.WriteAsync(correctedFrame, cancellationToken);
                            Console.WriteLine($"Movement corrected for occupied destination: account={actionSession.AccountName}, from=({action.PositionX},{action.PositionY}), to=({correctedAction.TargetX},{correctedAction.TargetY}).");
                            continue;
                        }

                        var movement = sessions.TryApplyMovement(connectionId, action);
                        if (movement == MovementResult.Accepted)
                        {
                            world.UpdatePosition(connectionId, action.TargetX, action.TargetY);
                            // GridMulticast in the legacy TMSrv forwards the accepted MSG_Action itself, preserving
                            // the millisecond ClientTick stamped at input time instead of replacing it with server time.
                            var actionFrame = action.ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                            await stream.WriteAsync(actionFrame, cancellationToken);
                            foreach (var recipient in world.GetParticipantIdsInPlayerView(connectionId).Where(id => id != connectionId))
                                await world.SendAsync(recipient, actionFrame, cancellationToken);
                            if (action.WireType == ActionRequest.IllusionMessageType &&
                                world.TryGetResourceState(connectionId, out var illusionState, out var requestedHp, out var requestedMp) && illusionState is not null)
                            {
                                var resourceFrame = new SetHpMpConfirmation(illusionState.CurrentScore.Hp, illusionState.CurrentMana, requestedHp, requestedMp)
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                await stream.WriteAsync(resourceFrame, cancellationToken);
                            }
                            // 7.69 _MSG_Action.cpp:308-322: after GridMulticast, DoRecall by BrGrid level band.
                            if (world.TryRecallForBattleRoyaleArenaWithViews(connectionId, out var arenaRecallTransition) && arenaRecallTransition is not null)
                            {
                                var arenaRecall = arenaRecallTransition.Recall;
                                sessions.SetServerPosition(connectionId, arenaRecall.ToX, arenaRecall.ToY);
                                var arenaRecallTick = unchecked((uint)Environment.TickCount64);
                                var arenaRecallKeyword = (byte)RandomNumberGenerator.GetInt32(256);
                                var arenaRecallCodec = LegacyFrameCodec.CreateDefault();
                                var arenaRecallFrame = new ActionRequest(
                                    arenaRecall.FromX,
                                    arenaRecall.FromY,
                                    Effect: 1,
                                    Speed: 2,
                                    Route: new byte[24],
                                    arenaRecall.ToX,
                                    arenaRecall.ToY)
                                    .ToFrame(arenaRecallCodec, arenaRecallTick, arenaRecallKeyword, (ushort)connectionId);
                                if (!LegacyRecallRelay.TryBuild(
                                    world,
                                    arenaRecallTransition,
                                    arenaRecallFrame,
                                    arenaRecallCodec,
                                    arenaRecallTick,
                                    arenaRecallKeyword,
                                    out var arenaRecallPlans))
                                    Console.Error.WriteLine($"Battle Royale arena recall visibility plan failed: connection={connectionId}; no global relay was sent.");

                                await stream.WriteAsync(arenaRecallFrame, cancellationToken);
                                if (arenaRecallPlans is not null)
                                    foreach (var plan in arenaRecallPlans)
                                        await world.SendAsync(plan.RecipientId, plan.Frame, cancellationToken);
                                Console.WriteLine($"Battle Royale arena recall: account={actionSession.AccountName}, from=({arenaRecall.FromX},{arenaRecall.FromY}), to=({arenaRecall.ToX},{arenaRecall.ToY}).");
                            }
                            if (world.TryAdvanceSummons(connectionId, out var summonMovements))
                            {
                                foreach (var summonMovement in summonMovements)
                                {
                                    var summonAction = new ActionRequest(
                                        summonMovement.FromX,
                                        summonMovement.FromY,
                                        summonMovement.Effect,
                                        summonMovement.Speed,
                                        new byte[24],
                                        summonMovement.ToX,
                                        summonMovement.ToY)
                                        .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)summonMovement.ConnectionId);
                                    await stream.WriteAsync(summonAction, cancellationToken);
                                    await world.BroadcastAsync(connectionId, summonAction, cancellationToken);
                                }
                            }
                            Console.WriteLine($"Movement accepted: account={actionSession.AccountName}, from=({action.PositionX},{action.PositionY}), to=({action.TargetX},{action.TargetY}).");
                        }
                        else
                            Console.WriteLine($"Movement rejected: account={actionSession.AccountName}, reason={movement}, target=({action.TargetX},{action.TargetY}).");
                        continue;
                    }

                    if (MotionRequest.TryParse(frame, out var motion) && motion is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var motionSession) || motionSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine($"Rejected motion: connection={connectionId}, session is not in USER_PLAY.");
                            await RecordCrackErrorAsync(LegacyCrackErrorRules.MotionPoints, LegacyCrackErrorRules.MotionType);
                            continue;
                        }

                        // TMSrv::_MSG_Motion sends MSG_SetHpMode and stops before GridMulticast for a dead player.
                        if (world.TryGetResourceState(connectionId, out var motionResources, out _, out _)
                            && motionResources is not null
                            && motionResources.CurrentScore.Hp == 0)
                        {
                            var hpModeFrame = new SetHpModeConfirmation(
                                motionResources.CurrentScore.Hp,
                                (short)motionSession.State)
                                .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                            await stream.WriteAsync(hpModeFrame, cancellationToken);
                            Console.WriteLine($"Rejected motion: account={motionSession.AccountName}, character HP is zero.");
                            await RecordCrackErrorAsync(LegacyCrackErrorRules.MotionPoints, LegacyCrackErrorRules.MotionType);
                            continue;
                        }

                        // Exec_MSG_Motion calls GridMulticast with the received struct, preserving ClientTick and all fields.
                        var motionFrame = motion.ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                        await stream.WriteAsync(motionFrame, cancellationToken);
                        await world.BroadcastAsync(connectionId, motionFrame, cancellationToken);
                        Console.WriteLine($"Motion relayed: account={motionSession.AccountName}, motion={motion.Motion}, parm={motion.Parameter}.");
                        continue;
                    }

                    if (SetShortSkillRequest.TryParse(frame, out var shortSkill) && shortSkill is not null)
                    {
                        if (accounts is null || !sessions.TryGet(connectionId, out var shortcutSession) || shortcutSession!.State != LoginSessionState.Playing || shortcutSession.AccountName is null || shortcutSession.CharacterSlot < 0)
                        {
                            Console.WriteLine($"Rejected set-short-skill: connection={connectionId}, session is not in USER_PLAY.");
                            continue;
                        }

                        var save = await accounts.TrySaveCharacterShortSkillsAsync(shortcutSession.AccountName, shortcutSession.CharacterSlot, shortSkill.SkillBar, shortSkill.ShortSkills, cancellationToken);
                        Console.WriteLine(save == CharacterShortSkillSaveResult.Success
                            ? $"Short skills saved: account={shortcutSession.AccountName}, slot={shortcutSession.CharacterSlot}."
                            : $"Short skills rejected: account={shortcutSession.AccountName}, slot={shortcutSession.CharacterSlot}, reason={save}.");
                        continue;
                    }

                    if (AttackRequest.TryParse(frame, out var attack) && attack is not null)
                    {
                        // TMProject 7.69 and W2PP check TradeMode before USER_PLAY,
                        // HP, timing, mana, and combat validation. A listed shop owner
                        // receives only the legacy private MessagePanel 170.
                        if (autoTradeBook.HasListing(connectionId))
                        {
                            var notice = new MessagePanelConfirmation("N\u00E3o \u00E9 possi\u00EDvel durante a auto venda.")
                                .ToFrame(LegacyFrameCodec.CreateDefault(), 0, 0);
                            await stream.WriteAsync(notice, cancellationToken);
                            continue;
                        }

                        if (skills is null || !sessions.TryGet(connectionId, out var attackSession) || attackSession!.State != LoginSessionState.Playing || !world.TryGetAttackState(connectionId, out var attackState) || attackState is null)
                        {
                            Console.WriteLine($"Ignored attack: connection={connectionId}, skill={attack.SkillIndex}, reason=CombatNotReady.");
                            continue;
                        }

                        var combatState = attackState.CombatState;

                        // TMProject 7.69 rejects clanless attacks in RvR sector (13,15) before HP/tick/mana.
                        // W2PP has no matching branch; the selected 7.69 client/server source defines this release behavior.
                        if (LegacyAttackRvrGateRules.ShouldReject(attackState.PositionX, attackState.PositionY, attackState.Clan))
                        {
                            var notice = new MessagePanelConfirmation(LegacyAttackRvrGateRules.BlockedMessage)
                                .ToFrame(LegacyFrameCodec.CreateDefault(), 0, 0);
                            await stream.WriteAsync(notice, cancellationToken);
                            continue;
                        }

                        // W2PP rejects exactly-zero HP here, before timing, mana, and damage; skill 99 is resurrection.
                        if (LegacyAttackDeadGateRules.ShouldReject(combatState.CurrentScore.Hp, attack.SkillIndex))
                        {
                            var hpModeFrame = new SetHpModeConfirmation(combatState.CurrentScore.Hp, (short)attackSession.State)
                                .ToFrame(LegacyFrameCodec.CreateDefault(), 0, 0, (ushort)connectionId);
                            await stream.WriteAsync(hpModeFrame, cancellationToken);
                            await RecordCrackErrorAsync(LegacyCrackErrorRules.AttackDeadPoints, LegacyCrackErrorRules.AttackDeadType);
                            continue;
                        }

                        var timing = sessions.TryAcceptAttackTiming(connectionId, frame.Header.ClientTick,
                            unchecked((uint)Environment.TickCount64), out var hasBackwardTickError, out var previousAttackTick);
                        LegacyCrackErrorOutcome? backwardTickError = null;
                        if (hasBackwardTickError)
                        {
                            // 7.69 and W2PP add 4/type7 without returning before LastAttackTick update or
                            // the server-window gate. Defer this error's logout until a simultaneous 1/type107
                            // window rejection has also been recorded, preserving the source order.
                            backwardTickError = await RecordCrackErrorAsync(
                                LegacyCrackErrorRules.AttackBackstepPoints,
                                LegacyCrackErrorRules.AttackBackstepType,
                                deferCharacterLogout: timing != AttackTimingResult.Accepted);
                        }
                        if (timing != AttackTimingResult.Accepted)
                        {
                            if (timing is AttackTimingResult.TooSoon or AttackTimingResult.OutsideServerWindow)
                            {
                                // Both 7.69 and W2PP add 1/type107 for these timing failures. The extra 7.69
                                // ban/Analysis/SaveInfoDetect side effects are excluded by the project's Action-only Analysis policy.
                                await RecordCrackErrorAsync(LegacyCrackErrorRules.AttackTimingPoints, LegacyCrackErrorRules.AttackTimingType);
                            }
                            Console.WriteLine($"Rejected attack: account={attackSession.AccountName}, skill={attack.SkillIndex}, reason={timing}.");
                            continue;
                        }
                        if (backwardTickError?.RequiresCharacterLogout == true)
                        {
                            Console.WriteLine($"Rejected attack: account={attackSession.AccountName}, skill={attack.SkillIndex}, reason=CrackErrorThreshold.");
                            continue;
                        }

                        // TMProject 7.69 skill block: Passive, 700 ms delay (10/type30), other-class skill,
                        // then learned-skill bits whose AddCrackError only stops the attack at the logout threshold.
                        // SkillIndex -1 skips skill gates/mana; independently, physical -2 markers remain valid in mixed frames.
                        var hasPhysicalMarker = Enumerable.Range(0, AttackRequest.GetTargetCount(frame.Header.Type))
                            .Any(index => AttackRequest.TryReadDamageSlot(frame, index, out _, out var marker) && marker == -2);
                        var skillGate = LegacySkillAttackGate.Evaluate(
                            skills, attack.SkillIndex, combatState.CharacterClass, combatState.LearnedSkill,
                            frame.Header.ClientTick, previousAttackTick);
                        if (skillGate.ClearClassOneType6Affect)
                        {
                            var cleanupResult = world.TryClearFirstClassOneType6Affect(connectionId, out var cleanupOutcome);
                            if (cleanupResult == LegacyClassOneType6CleanupResult.Cleared && cleanupOutcome is not null)
                            {
                                var scoreFrame = BuildClientV769UpdateScoreFrame(
                                    cleanupOutcome.MobSnapshot,
                                    cleanupOutcome.AffectSnapshot,
                                    (ushort)connectionId);
                                await stream.WriteAsync(scoreFrame, cancellationToken);
                            }
                            else if (cleanupResult is not (LegacyClassOneType6CleanupResult.NotApplicable or LegacyClassOneType6CleanupResult.NoAffect))
                            {
                                Console.WriteLine($"Rejected attack: account={attackSession.AccountName}, skill={attack.SkillIndex}, reason=ClassOneType6CleanupUnavailable ({cleanupResult}).");
                                continue;
                            }
                        }
                        var allowPhysicalWithInvalidSkill = hasPhysicalMarker &&
                            skillGate.Result is SkillAttackValidationResult.InvalidSkillId or SkillAttackValidationResult.MissingSkillDefinition;
                        if (skillGate.Result == SkillAttackValidationResult.SkillDelayTooShort)
                        {
                            await RecordCrackErrorAsync(LegacySkillAttackGate.SkillDelayPoints, LegacySkillAttackGate.SkillDelayType);
                            Console.WriteLine($"Rejected attack: account={attackSession.AccountName}, skill={attack.SkillIndex}, reason={skillGate.Result}.");
                            continue;
                        }
                        if (!skillGate.IsAccepted && !allowPhysicalWithInvalidSkill)
                        {
                            Console.WriteLine($"Rejected attack: account={attackSession.AccountName}, skill={attack.SkillIndex}, reason={skillGate.Result}.");
                            continue;
                        }
                        if (skillGate.RequiresMortarItem &&
                            !world.HasMortarItemAtLegacyAttackPosition(attack.PositionX, attack.PositionY))
                        {
                            Console.WriteLine($"Rejected attack: account={attackSession.AccountName}, skill={attack.SkillIndex}, reason=MortarItemNotFound.");
                            continue;
                        }
                        if (skillGate.LearnedSkillError is { } learnedSkillError)
                        {
                            var learnedOutcome = await RecordCrackErrorAsync(learnedSkillError.Points, learnedSkillError.Type);
                            if (learnedOutcome?.RequiresCharacterLogout == true)
                            {
                                Console.WriteLine($"Rejected attack: account={attackSession.AccountName}, skill={attack.SkillIndex}, reason=CrackErrorThreshold.");
                                continue;
                            }
                        }

                        var skillDefinition = attack.SkillIndex == LegacyDivineBlessing.SkillId
                            ? LegacyDivineBlessing.ParentActionDefinition
                            : skills[attack.SkillIndex];

                        // TMProject 7.69 and W2PP charge skill 85 before the mana check and send
                        // SendEtc immediately. If the later mana check fails, the Gold stays spent.
                        if (attack.SkillIndex == LegacyGoldenShieldCoinMath.SkillId)
                        {
                            if (attackSession.AccountName is null)
                            {
                                Console.WriteLine($"Ignored attack: connection={connectionId}, skill={attack.SkillIndex}, reason=AccountIdentityUnavailable.");
                                continue;
                            }
                            LegacySkillCoinResult coinResult;
                            byte[]? coinMobSnapshot;
                            await using (var goldenShieldLease = await accountOperationGate.AcquireAsync(
                                             [attackSession.AccountName], cancellationToken))
                            {
                                coinResult = world.TryConsumeGoldenShieldCoin(
                                    connectionId,
                                    skills[attack.SkillIndex]!,
                                    out _,
                                    out coinMobSnapshot);
                            }
                            if (coinResult == LegacySkillCoinResult.InsufficientCoin)
                                continue;
                            if (coinResult != LegacySkillCoinResult.Accepted || coinMobSnapshot is null)
                            {
                                Console.WriteLine($"Ignored attack: connection={connectionId}, skill={attack.SkillIndex}, reason={coinResult}.");
                                continue;
                            }

                            var coinFrame = BuildClientV769UpdateEtcFrame(
                                coinMobSnapshot,
                                ReadOnlySpan<byte>.Empty,
                                (ushort)connectionId);
                            await stream.WriteAsync(coinFrame, cancellationToken);
                        }

                        LegacySkillManaResult mana;
                        byte[]? mobSnapshot;
                        int requestedMp;
                        if (skillGate.IsPhysical || allowPhysicalWithInvalidSkill)
                        {
                            mana = world.TryCapturePhysicalAttackSnapshot(connectionId, out mobSnapshot, out requestedMp)
                                ? LegacySkillManaResult.Accepted
                                : LegacySkillManaResult.ParticipantNotFound;
                        }
                        else
                            mana = world.TryConsumeSkillMana(connectionId, skillDefinition!, out _, out mobSnapshot, out requestedMp);
                        if (mana == LegacySkillManaResult.InsufficientMana)
                        {
                            if (world.TryGetResourceState(connectionId, out var resourceState, out var requestedHp, out var currentRequestedMp) && resourceState is not null)
                            {
                                var sync = new SetHpMpConfirmation(resourceState.CurrentScore.Hp, resourceState.CurrentMana, requestedHp, currentRequestedMp)
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                await stream.WriteAsync(sync, cancellationToken);
                            }
                            Console.WriteLine($"Rejected attack: account={attackSession.AccountName}, skill={attack.SkillIndex}, reason={mana}.");
                            continue;
                        }

                        if (mana != LegacySkillManaResult.Accepted || mobSnapshot is null || !AttackConfirmation.TryCreate(frame, mobSnapshot, out var attackConfirmation) || attackConfirmation is null)
                        {
                            Console.WriteLine($"Rejected attack: account={attackSession.AccountName}, skill={attack.SkillIndex}, reason={mana}.");
                            continue;
                        }

                        // TMProject 7.69 and W2PP call DoRemoveHide for a class 3 attacker after mana handling and before scanning target slots.
                        var attackerHideCleanup = world.TryClearFirstClassThreeHideAffect(connectionId, out var attackerHideOutcome);
                        if (attackerHideCleanup == LegacyAttackHideCleanupResult.Cleared && attackerHideOutcome is not null)
                        {
                            mobSnapshot = attackerHideOutcome.MobSnapshot;
                            var scoreFrame = BuildClientV769UpdateScoreFrame(
                                attackerHideOutcome.MobSnapshot,
                                attackerHideOutcome.AffectSnapshot,
                                (ushort)connectionId);
                            await stream.WriteAsync(scoreFrame, cancellationToken);
                            if (!AttackConfirmation.TryCreate(frame, attackerHideOutcome.MobSnapshot, out var updatedAttackConfirmation) ||
                                updatedAttackConfirmation is null)
                            {
                                Console.WriteLine($"Rejected attack: account={attackSession.AccountName}, skill={attack.SkillIndex}, reason=AttackHideResponseUnavailable.");
                                continue;
                            }
                            attackConfirmation = updatedAttackConfirmation;
                        }
                        else if (attackerHideCleanup is not (LegacyAttackHideCleanupResult.NotApplicable or LegacyAttackHideCleanupResult.NoAffect))
                        {
                            Console.WriteLine($"Rejected attack: account={attackSession.AccountName}, skill={attack.SkillIndex}, reason=AttackHideCleanupUnavailable ({attackerHideCleanup}).");
                            continue;
                        }

                        // TMProject 7.69 and W2PP call DoRemoveSamaritano for the attacker after the class 3 hide cleanup and before scanning target slots.
                        var attackerSamaritanCleanup = world.TryClearFirstSamaritanAffect(connectionId, out var attackerSamaritanOutcome);
                        if (attackerSamaritanCleanup == LegacyAttackSamaritanCleanupResult.Cleared && attackerSamaritanOutcome is not null)
                        {
                            mobSnapshot = attackerSamaritanOutcome.MobSnapshot;
                            var scoreFrame = BuildClientV769UpdateScoreFrame(
                                attackerSamaritanOutcome.MobSnapshot,
                                attackerSamaritanOutcome.AffectSnapshot,
                                (ushort)connectionId);
                            await stream.WriteAsync(scoreFrame, cancellationToken);
                            if (!AttackConfirmation.TryCreate(frame, attackerSamaritanOutcome.MobSnapshot, out var updatedAttackConfirmation) ||
                                updatedAttackConfirmation is null)
                            {
                                Console.WriteLine($"Rejected attack: account={attackSession.AccountName}, skill={attack.SkillIndex}, reason=AttackSamaritanResponseUnavailable.");
                                continue;
                            }
                            attackConfirmation = updatedAttackConfirmation;
                        }
                        else if (attackerSamaritanCleanup != LegacyAttackSamaritanCleanupResult.NoAffect)
                        {
                            Console.WriteLine($"Rejected attack: account={attackSession.AccountName}, skill={attack.SkillIndex}, reason=AttackSamaritanCleanupUnavailable ({attackerSamaritanCleanup}).");
                            continue;
                        }
                        if (skillGate.RequiresMortarItem && !attackConfirmation.TrySetMotion(1))
                        {
                            Console.WriteLine($"Rejected attack: account={attackSession.AccountName}, skill={attack.SkillIndex}, reason=MortarResponseUnavailable.");
                            continue;
                        }

                        if (itemData is not null && TryResolveSkillCombatInputs(mobSnapshot, itemData, out var magic, out var weaponDamage))
                            Console.WriteLine($"Skill combat inputs prepared: account={attackSession.AccountName}, skill={attack.SkillIndex}, magic={magic}, weaponDamage={weaponDamage}.");

                        var damageResolved = false;
                        var removedMobIds = new HashSet<int>();
                        var deferredAttackEffects = new List<(int SlotIndex, int Sequence, int? RemovedMobId, LegacyExperienceAward? Award, LegacyItemDrop? Drop)>();
                        var spawnedNpcs = new List<(int SlotIndex, LegacyWorldNpc Npc)>();
                        var physicalAttackRelaySent = false;
                        byte[]? deferredType9AggregateAttackFrame = null;
                        ushort targetConnectionId = 0;
                        var physicalSlots = new List<LegacyPhysicalAttackBatchSlotRequest>();
                        var actionSlots = new List<LegacyAttackActionBatchSlotRequest>();
                        var invalidDamageMarkerSlots = new List<(int SlotIndex, ushort TargetId)>();
                        var preprocessedTargetSlots = new List<(int SlotIndex, ushort TargetId, int Damage)>();
                        var preprocessedRemoveMobSlots = new List<(int SlotIndex, ushort TargetId)>();
                        var physicalTargetCount = AttackRequest.GetTargetCount(frame.Header.Type);
                        var attackHideCleanupUnavailable = false;
                        var skillApplicationSupported = skillDefinition is not null &&
                            (itemData is not null || skillDefinition.Id == 99 || skillDefinition.InstanceType is 6 or 8 or 9 or 11 or 12 || skillDefinition.AffectType > 0 || skillDefinition.TickType > 0);
                        for (var slotIndex = 0; slotIndex < physicalTargetCount; slotIndex++)
                        {
                            if (!AttackRequest.TryReadDamageSlot(frame, slotIndex, out var slotTargetId, out var slotDeclaredDamage))
                                continue;
                            var targetSlotState = world.GetAttackTargetSlotState(slotTargetId);
                            if (targetSlotState is LegacyAttackTargetSlotState.InvalidIndex or LegacyAttackTargetSlotState.Empty)
                            {
                                attackConfirmation.TrySetDamageSlot(slotIndex, slotTargetId, slotDeclaredDamage);
                                preprocessedTargetSlots.Add((slotIndex, slotTargetId, slotDeclaredDamage));
                                if (targetSlotState == LegacyAttackTargetSlotState.Empty)
                                    preprocessedRemoveMobSlots.Add((slotIndex, slotTargetId));
                                continue;
                            }

                            // The common 7.69 target-side DoRemoveHide call precedes NPC/head/dead/merchant/range and Damage[] checks.
                            var targetHideCleanup = world.TryClearFirstClassThreeHideAffect(slotTargetId, out var targetHideOutcome);
                            if (targetHideCleanup == LegacyAttackHideCleanupResult.Cleared && targetHideOutcome is not null)
                            {
                                var scoreFrame = BuildClientV769UpdateScoreFrame(
                                    targetHideOutcome.MobSnapshot,
                                    targetHideOutcome.AffectSnapshot,
                                    slotTargetId);
                                if (slotTargetId == connectionId)
                                    await stream.WriteAsync(scoreFrame, cancellationToken);
                                else
                                    await world.SendAsync(slotTargetId, scoreFrame, cancellationToken);
                            }
                            else if (targetHideCleanup is not (LegacyAttackHideCleanupResult.ParticipantNotFound or LegacyAttackHideCleanupResult.NotApplicable or LegacyAttackHideCleanupResult.NoAffect))
                            {
                                Console.WriteLine($"Rejected attack: account={attackSession.AccountName}, skill={attack.SkillIndex}, target={slotTargetId}, reason=AttackHideCleanupUnavailable ({targetHideCleanup}).");
                                attackHideCleanupUnavailable = true;
                                break;
                            }

                            var damageMarker = LegacyAttackDamageMarkerGate.Classify(slotDeclaredDamage);
                            if (damageMarker == LegacyAttackDamageMarkerKind.Invalid)
                            {
                                // The common 7.69 NPC-head/map gate precedes Damage[] validation.
                                if (world.IsAttackTargetBlockedByNpcHeadArea(slotTargetId))
                                {
                                    attackConfirmation.TrySetDamageSlot(slotIndex, 0, 0);
                                    continue;
                                }
                                if (world.ShouldClearDeadTargetInAttack(slotTargetId, attack.SkillIndex))
                                {
                                    attackConfirmation.TrySetDamageSlot(slotIndex, 0, 0);
                                    continue;
                                }
                                if (world.ShouldClearMerchantTargetInAttack(slotTargetId))
                                {
                                    attackConfirmation.TrySetDamageSlot(slotIndex, 0, 0);
                                    continue;
                                }
                                if (world.ShouldRemoveOutOfViewTargetInAttack(connectionId, slotTargetId, attack.SkillIndex))
                                {
                                    // The legacy handler relays the original MSG_Attack slot after SendRemoveMob.
                                    attackConfirmation.TrySetDamageSlot(slotIndex, slotTargetId, slotDeclaredDamage);
                                    preprocessedRemoveMobSlots.Add((slotIndex, slotTargetId));
                                    continue;
                                }
                                invalidDamageMarkerSlots.Add((slotIndex, slotTargetId));
                                attackConfirmation.TrySetDamageSlot(slotIndex, slotTargetId, 0);
                                await RecordCrackErrorAsync(
                                    LegacyAttackDamageMarkerGate.CrackErrorPoints,
                                    LegacyAttackDamageMarkerGate.CrackErrorType);
                                continue;
                            }
                            if (damageMarker == LegacyAttackDamageMarkerKind.Physical)
                            {
                                var physicalRolls = new LegacyPhysicalAttackBatchSlotRolls(
                                    RandomFactor: RandomNumberGenerator.GetInt32(99, 111));
                                physicalSlots.Add(new LegacyPhysicalAttackBatchSlotRequest(
                                    slotIndex,
                                    slotTargetId,
                                    physicalRolls));
                                actionSlots.Add(new LegacyAttackActionBatchSlotRequest(
                                    slotIndex,
                                    LegacyAttackActionSlotKind.Physical,
                                    slotTargetId,
                                    physicalRolls));
                            }
                            else if (damageMarker == LegacyAttackDamageMarkerKind.Skill && skillGate.IsAccepted && skillDefinition is not null)
                            {
                                actionSlots.Add(new LegacyAttackActionBatchSlotRequest(
                                    slotIndex,
                                    LegacyAttackActionSlotKind.Skill,
                                    slotTargetId,
                                    SkillTargetIndex: slotIndex));
                            }
                        }

                        if (attackHideCleanupUnavailable)
                            continue;

                        LegacyAttackActionBatchOutcome? actionBatch = null;
                        LegacyPhysicalAttackBatchOutcome? physicalBatch = null;
                        if (actionSlots.Count > 0)
                        {
                            var actionBatchRequest = new LegacyAttackActionBatchRequest(
                                connectionId,
                                actionSlots,
                                Skill: skillDefinition,
                                ItemData: itemData,
                                ClientProgress: attack.Progress,
                                SupportsSecondaryDamageSlot: physicalTargetCount >= 2,
                                ClientTick: frame.Header.ClientTick)
                            {
                                ApplySkillEffects = skillApplicationSupported,
                                IsShortTwoTargetPacket = attack.IsShortTwoTargetPacket,
                                SkillIndex = attack.SkillIndex,
                                TargetX = attack.TargetX,
                                TargetY = attack.TargetY,
                                DivineBlessingHealSkill = attack.SkillIndex == LegacyDivineBlessing.SkillId
                                    ? skills[LegacyDivineBlessing.HealSkillId]
                                    : null,
                            };
                            await using var attackRewardLease = await AcquirePartyAccountLeaseAsync(
                                accountOperationGate,
                                world,
                                connectionId,
                                cancellationToken);
                            var attackBatchApplied = world.TryApplyAttackActionBatch(actionBatchRequest, out actionBatch);
                            await attackRewardLease.DisposeAsync();
                            if (!attackBatchApplied || actionBatch is null)
                            {
                                Console.WriteLine($"Rejected attack action batch: account={attackSession.AccountName}, skill={attack.SkillIndex}, reason=BatchUnavailable.");
                                continue;
                            }

                            foreach (var removeEffect in actionBatch.ViewRemovalEffects
                                         .Select(static effect => (effect.SlotIndex, effect.TargetConnectionId, effect.ViewRecipientConnectionIds))
                                         .Concat(preprocessedRemoveMobSlots.Select(static effect => (
                                             effect.SlotIndex,
                                             TargetConnectionId: (int)effect.TargetId,
                                             ViewRecipientConnectionIds: (IReadOnlyList<int>?)null)))
                                         .OrderBy(static effect => effect.SlotIndex))
                            {
                                var removeFrame = new RemoveMobConfirmation(checked((ushort)removeEffect.TargetConnectionId), removeType: 1)
                                    .ToFrame(
                                        LegacyFrameCodec.CreateDefault(),
                                        frame.Header.ClientTick,
                                        (byte)RandomNumberGenerator.GetInt32(256));
                                await stream.WriteAsync(removeFrame, cancellationToken);
                                if (removeEffect.ViewRecipientConnectionIds is not null)
                                    await LegacyAttackViewRemovalRelay.RelayCapturedViewAsync(
                                        world,
                                        connectionId,
                                        new LegacyAttackViewRemovalEffect(
                                            removeEffect.SlotIndex,
                                            removeEffect.TargetConnectionId,
                                            removeEffect.ViewRecipientConnectionIds),
                                        removeFrame,
                                        cancellationToken);
                            }

                            foreach (var type24Slot in actionBatch.SlotOutcomes.Where(slot =>
                                         slot.PhysicalResult == LegacyPhysicalAttackResult.ShortPacketSecondaryTargetBlocked))
                                await RecordCrackErrorAsync(
                                    LegacyAttackType24Gate.CrackErrorPoints,
                                    LegacyAttackType24Gate.CrackErrorType);

                            foreach (var spawnedNpc in actionBatch.SpawnedNpcs)
                                spawnedNpcs.Add((spawnedNpc.ParentSlotIndex, spawnedNpc.Npc));

                            var secondaryRelayFailed = false;
                            foreach (var secondaryAction in actionBatch.SecondaryActions)
                            {
                                var codec = LegacyFrameCodec.CreateDefault();
                                if (secondaryAction.Kind == LegacyAttackActionSecondaryOutcomeKind.SetHpDam)
                                {
                                    if (secondaryAction.AffectSnapshot is { } attackerAffect)
                                    {
                                        var scorePayload = new UpdateScoreConfirmation(
                                            secondaryAction.MobSnapshot,
                                            attackerAffect).ToPayload();
                                        var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                                                scorePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize),
                                                LegacyMobCombatState.Read(secondaryAction.MobSnapshot).Rsv)
                                            .ToFrame(
                                                codec,
                                                frame.Header.ClientTick,
                                                (byte)RandomNumberGenerator.GetInt32(256),
                                                checked((ushort)secondaryAction.WireTargetId));
                                        await world.SendAsync(
                                            secondaryAction.WireTargetId,
                                            scoreFrame,
                                            cancellationToken);
                                    }

                                    var currentHp = LegacyMobCombatState.Read(secondaryAction.MobSnapshot).CurrentScore.Hp;
                                    var hpUpdate = new SetHpDamConfirmation(currentHp, secondaryAction.WireDamage)
                                        .ToFrame(
                                            codec,
                                            frame.Header.ClientTick,
                                            (byte)RandomNumberGenerator.GetInt32(256),
                                            checked((ushort)secondaryAction.WireTargetId));
                                    await world.BroadcastToParticipantsAsync(
                                        secondaryAction.RecipientConnectionIds,
                                        hpUpdate,
                                        cancellationToken);
                                    continue;
                                }

                                if (secondaryAction.Kind == LegacyAttackActionSecondaryOutcomeKind.SetHpMp)
                                {
                                    var state = LegacyMobCombatState.Read(secondaryAction.MobSnapshot);
                                    var correction = new SetHpMpConfirmation(
                                            state.CurrentScore.Hp,
                                            state.CurrentMana,
                                            secondaryAction.RequestedHp,
                                            secondaryAction.RequestedMp)
                                        .ToFrame(codec, frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                    await WriteLoggedFrameAsync(stream, serverLog, connectionId, correction, cancellationToken, "divine-blessing-child-set-hp-mp");
                                    continue;
                                }

                                if (secondaryAction.Kind != LegacyAttackActionSecondaryOutcomeKind.AttackOne ||
                                    !AttackConfirmation.TryCreateSyntheticOneTarget(
                                        secondaryAction.MobSnapshot,
                                        checked((ushort)connectionId),
                                        secondaryAction.PositionX,
                                        secondaryAction.PositionY,
                                        checked((ushort)secondaryAction.WireTargetId),
                                        secondaryAction.WireDamage,
                                        out var childAttackConfirmation) ||
                                    childAttackConfirmation is null)
                                {
                                    Console.WriteLine($"Rejected Divine Blessing child relay: connection={connectionId}, slot={secondaryAction.ParentSlotIndex}, reason=InvalidChildSnapshot.");
                                    secondaryRelayFailed = true;
                                    break;
                                }

                                var childAttackFrame = childAttackConfirmation.ToFrame(
                                    codec,
                                    ClientTickPolicy.SkipCheckTick,
                                    (byte)RandomNumberGenerator.GetInt32(256));
                                await world.BroadcastToParticipantsAsync(
                                    secondaryAction.RecipientConnectionIds,
                                    childAttackFrame,
                                    cancellationToken);
                            }
                            if (secondaryRelayFailed)
                                continue;

                            if (physicalSlots.Count > 0)
                            {
                                physicalBatch = new LegacyPhysicalAttackBatchOutcome(
                                    actionBatch.SlotOutcomes
                                        .Where(slot => slot.Kind == LegacyAttackActionSlotKind.Physical)
                                        .Select(slot => new LegacyPhysicalAttackBatchSlotOutcome(
                                            slot.SlotIndex,
                                            slot.PhysicalResult ?? LegacyPhysicalAttackResult.ParticipantNotFound,
                                            slot.PhysicalOutcome))
                                        .ToArray(),
                                    actionBatch.AbortSlotIndex,
                                    actionBatch.DoubleCritical,
                                    actionBatch.NextProgress,
                                    actionBatch.AttackerMobSnapshot,
                                    actionBatch.CompletedNormally,
                                    actionBatch.WireSlotOverrides);
                            }

                            if (actionBatch.CompletedNormally)
                            {
                                if (actionBatch.AttackerMobSnapshot is null ||
                                    !AttackConfirmation.TryCreate(frame, actionBatch.AttackerMobSnapshot, out var aggregateAttackConfirmation) ||
                                    aggregateAttackConfirmation is null)
                                {
                                    Console.WriteLine($"Rejected attack action batch: account={attackSession.AccountName}, skill={attack.SkillIndex}, reason=AttackerSnapshotUnavailable.");
                                    continue;
                                }
                                if (skillGate.RequiresMortarItem && !aggregateAttackConfirmation.TrySetMotion(1))
                                    continue;

                                if (skillDefinition?.InstanceType == 6 &&
                                    actionSlots.Any(slot => slot.Kind == LegacyAttackActionSlotKind.Skill) &&
                                    (actionBatch.HealingExperience.HasValue || physicalSlots.Count == 0))
                                    aggregateAttackConfirmation.TrySetFakeExp(actionBatch.HealingExperience ?? 0);

                                foreach (var slotOutcome in actionBatch.SlotOutcomes)
                                {
                                    if (slotOutcome.Kind == LegacyAttackActionSlotKind.Physical && slotOutcome.PhysicalOutcome is { } physicalOutcome)
                                        aggregateAttackConfirmation.TrySetDamageSlot(slotOutcome.SlotIndex, (ushort)physicalOutcome.TargetConnectionId, physicalOutcome.Damage);
                                    else if (slotOutcome.Kind == LegacyAttackActionSlotKind.Physical &&
                                             slotOutcome.PhysicalResult == LegacyPhysicalAttackResult.ShortPacketSecondaryTargetBlocked)
                                    {
                                        var requestedSlot = actionSlots.First(slot => slot.SlotIndex == slotOutcome.SlotIndex);
                                        // The C++ type24 branch continues before replacing the incoming -2 marker.
                                        aggregateAttackConfirmation.TrySetDamageSlot(slotOutcome.SlotIndex, (ushort)requestedSlot.TargetConnectionId, -2);
                                    }
                                    else if (slotOutcome.Kind == LegacyAttackActionSlotKind.Physical &&
                                             slotOutcome.PhysicalResult == LegacyPhysicalAttackResult.TargetOutOfView)
                                    {
                                        var requestedSlot = actionSlots.First(slot => slot.SlotIndex == slotOutcome.SlotIndex);
                                        aggregateAttackConfirmation.TrySetDamageSlot(slotOutcome.SlotIndex, (ushort)requestedSlot.TargetConnectionId, -2);
                                    }
                                    else if (slotOutcome.Kind == LegacyAttackActionSlotKind.Skill && slotOutcome.SkillOutcome is { } skillOutcome)
                                        // 7.69 `continue` paths (e.g. InstanceType 12) leave the client's -1 marker in Dam[i].
                                        aggregateAttackConfirmation.TrySetDamageSlot(slotOutcome.SlotIndex, (ushort)skillOutcome.TargetConnectionId, skillOutcome.PreserveSkillMarkerOnWire ? -1 : skillOutcome.Damage);
                                    else if (slotOutcome.Kind == LegacyAttackActionSlotKind.Skill && slotOutcome.SkillResult is LegacySkillAttackResult.TargetLimitExceeded or LegacySkillAttackResult.PartySkillBlocked or LegacySkillAttackResult.UnsupportedSkillType)
                                    {
                                        var requestedSlot = actionSlots.First(slot => slot.SlotIndex == slotOutcome.SlotIndex);
                                        aggregateAttackConfirmation.TrySetDamageSlot(slotOutcome.SlotIndex, (ushort)requestedSlot.TargetConnectionId, -1);
                                    }
                                    else if (slotOutcome.Kind == LegacyAttackActionSlotKind.Skill &&
                                             slotOutcome.SkillResult == LegacySkillAttackResult.TargetOutOfView)
                                    {
                                        var requestedSlot = actionSlots.First(slot => slot.SlotIndex == slotOutcome.SlotIndex);
                                        aggregateAttackConfirmation.TrySetDamageSlot(slotOutcome.SlotIndex, (ushort)requestedSlot.TargetConnectionId, -1);
                                    }
                                }
                                foreach (var wireSlotOverride in actionBatch.WireSlotOverrides)
                                    aggregateAttackConfirmation.TrySetDamageSlot(
                                        wireSlotOverride.SlotIndex,
                                        (ushort)wireSlotOverride.TargetConnectionId,
                                        wireSlotOverride.Damage);
                                foreach (var invalidDamageMarkerSlot in invalidDamageMarkerSlots)
                                    aggregateAttackConfirmation.TrySetDamageSlot(
                                        invalidDamageMarkerSlot.SlotIndex,
                                        invalidDamageMarkerSlot.TargetId,
                                        0);
                                if (allowPhysicalWithInvalidSkill)
                                {
                                    for (var slotIndex = 0; slotIndex < physicalTargetCount; slotIndex++)
                                        if (AttackRequest.TryReadDamageSlot(frame, slotIndex, out var targetId, out var marker) && marker == -1)
                                            aggregateAttackConfirmation.TrySetDamageSlot(slotIndex, targetId, -1);
                                }
                                foreach (var preprocessedTargetSlot in preprocessedTargetSlots)
                                    aggregateAttackConfirmation.TrySetDamageSlot(
                                        preprocessedTargetSlot.SlotIndex,
                                        preprocessedTargetSlot.TargetId,
                                        preprocessedTargetSlot.Damage);
                                aggregateAttackConfirmation.TrySetDoubleCritical(actionBatch.DoubleCritical);
                                aggregateAttackConfirmation.TrySetProgress(actionBatch.NextProgress);
                                attackConfirmation = aggregateAttackConfirmation;

                                var aggregateAttackFrame = aggregateAttackConfirmation.ToFrame(
                                    LegacyFrameCodec.CreateDefault(),
                                    frame.Header.ClientTick,
                                    (byte)RandomNumberGenerator.GetInt32(256));
                                if (skillDefinition?.InstanceType == 9)
                                    deferredType9AggregateAttackFrame = aggregateAttackFrame;
                                else
                                {
                                    await stream.WriteAsync(aggregateAttackFrame, cancellationToken);
                                    if (attack.SkillIndex != 102)
                                    {
                                        var attackRecipients = world.GetParticipantIdsInPlayerView(connectionId)
                                            .Where(recipientId => recipientId != connectionId)
                                            .ToArray();
                                        await world.BroadcastToParticipantsAsync(attackRecipients, aggregateAttackFrame, cancellationToken);
                                    }
                                    physicalAttackRelaySent = true;
                                }
                            }
                        }
                        else if (allowPhysicalWithInvalidSkill)
                        {
                            for (var slotIndex = 0; slotIndex < physicalTargetCount; slotIndex++)
                                if (AttackRequest.TryReadDamageSlot(frame, slotIndex, out var targetId, out var marker) && marker == -1)
                                    attackConfirmation.TrySetDamageSlot(slotIndex, targetId, -1);
                        }

                        if (actionSlots.Count == 0)
                        {
                            foreach (var removeEffect in preprocessedRemoveMobSlots.OrderBy(static effect => effect.SlotIndex))
                            {
                                var removeFrame = new RemoveMobConfirmation(removeEffect.TargetId, removeType: 1)
                                    .ToFrame(
                                        LegacyFrameCodec.CreateDefault(),
                                        frame.Header.ClientTick,
                                        (byte)RandomNumberGenerator.GetInt32(256));
                                await stream.WriteAsync(removeFrame, cancellationToken);
                            }
                        }

                        async Task RelayPhysicalSlotAsync(int requestedSlotIndex)
                        {
                            if (physicalBatch is null) return;
                            foreach (var slotOutcome in physicalBatch.SlotOutcomes)
                            {
                                if (slotOutcome.SlotIndex != requestedSlotIndex) continue;
                                var physicalResult = slotOutcome.Result;
                                var physicalOutcome = slotOutcome.Outcome;
                                targetConnectionId = (ushort)(physicalOutcome?.TargetConnectionId ?? physicalSlots.First(slot => slot.SlotIndex == slotOutcome.SlotIndex).TargetConnectionId);
                                if (physicalResult == LegacyPhysicalAttackResult.OutOfRange)
                                {
                                    Console.WriteLine($"Rejected attack batch: account={attackSession.AccountName}, skill={attack.SkillIndex}, slot={slotOutcome.SlotIndex}, target={targetConnectionId}, reason={physicalResult}.");
                                    continue;
                                }

                                if (physicalResult == LegacyPhysicalAttackResult.Accepted && physicalOutcome is not null)
                                {
                                    if (physicalOutcome.ZardMobLeftCount is { } zardMobLeftCount)
                                    {
                                        var mobLeftFrame = new MobLeftConfirmation(zardMobLeftCount)
                                            .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
                                        await stream.WriteAsync(mobLeftFrame, cancellationToken);
                                    }
                                    if (physicalOutcome.TargetRemoved && removedMobIds.Add(physicalOutcome.TargetConnectionId))
                                        deferredAttackEffects.Add((slotOutcome.SlotIndex, int.MaxValue, physicalOutcome.TargetConnectionId, null, null));
                                    if (physicalOutcome.PistaTransition?.SpawnGenerateIndex > 0 &&
                                        world.TrySpawnGeneratedNpcGroup(physicalOutcome.PistaTransition.SpawnGenerateIndex, out var physicalSpawnGroup) &&
                                        physicalSpawnGroup is not null)
                                        foreach (var physicalSpawnedNpc in physicalSpawnGroup)
                                            spawnedNpcs.Add((slotOutcome.SlotIndex, physicalSpawnedNpc));
                                    if (physicalOutcome.PistaTransition?.SpawnGenerateIndexes is { Count: > 0 } physicalSpawnIndexes)
                                    {
                                        foreach (var generateIndex in physicalSpawnIndexes)
                                            if (world.TrySpawnGeneratedNpcGroup(generateIndex, out var generatedGroup) && generatedGroup is not null)
                                                foreach (var generatedNpc in generatedGroup)
                                                    spawnedNpcs.Add((slotOutcome.SlotIndex, generatedNpc));
                                    }
                                    if (physicalOutcome.PistaTransition?.RemovedNpcConnectionIds is { Count: > 0 } physicalRemovedNpcIds)
                                        foreach (var removedNpcId in physicalRemovedNpcIds)
                                            if (removedMobIds.Add(removedNpcId))
                                                deferredAttackEffects.Add((slotOutcome.SlotIndex, int.MaxValue, removedNpcId, null, null));
                                    if (physicalOutcome.PistaTransition?.Teleports is { Count: > 0 } physicalPistaTeleports)
                                    await BroadcastPistaTeleportsAsync(world, physicalPistaTeleports, cancellationToken);
                                if (physicalOutcome.ExperienceAwards is { Count: > 0 } physicalExperienceAwards)
                                {
                                    foreach (var award in physicalExperienceAwards.Where(award => award.ConnectionId != connectionId))
                                        deferredAttackEffects.Add((slotOutcome.SlotIndex, deferredAttackEffects.Count, null, award, null));
                                }
                                if (physicalOutcome.ItemDrops is { Count: > 0 } physicalItemDrops)
                                {
                                    foreach (var drop in physicalItemDrops)
                                        deferredAttackEffects.Add((slotOutcome.SlotIndex, deferredAttackEffects.Count, null, null, drop));
                                }
                                    if (physicalOutcome.CoinUpdates is { Count: > 0 } physicalCoinUpdates)
                                    {
                                        foreach (var update in physicalCoinUpdates)
                                        {
                                            if (update.ConnectionId == connectionId && physicalOutcome.AttackerMobSnapshot is not null)
                                                continue;
                                            var coinFrame = BuildClientV769UpdateEtcFrame(
                                            update.MobSnapshot,
                                            ReadOnlySpan<byte>.Empty,
                                            (ushort)update.ConnectionId);
                                        await world.SendAsync(update.ConnectionId, coinFrame, cancellationToken);
                                        }
                                    }
                                    if (physicalOutcome.PrivateNotices is { Count: > 0 } physicalPrivateNotices)
                                        await SendPrivateNoticesAsync(world, physicalPrivateNotices, cancellationToken);
                                    if (physicalOutcome.GlobalNotices is { Count: > 0 } physicalGlobalNotices)
                                        await BroadcastGlobalNoticesAsync(world, physicalGlobalNotices, cancellationToken);
                                    if (physicalOutcome.AreaNotices is { Count: > 0 } physicalAreaNotices)
                                        await BroadcastAreaNoticesAsync(world, physicalAreaNotices, cancellationToken);
                                    if (physicalOutcome.MountOwnerConnectionId > 0 && physicalOutcome.UpdatedMountItem is { } updatedMountItem)
                                {
                                    var mountTick = unchecked((uint)Environment.TickCount64);
                                    var mountKeyword = (byte)RandomNumberGenerator.GetInt32(256);
                                    var mountFrame = new SendItemConfirmation(0, 14, updatedMountItem)
                                        .ToFrame(LegacyFrameCodec.CreateDefault(), mountTick, mountKeyword, (ushort)physicalOutcome.MountOwnerConnectionId);
                                    await world.SendAsync(physicalOutcome.MountOwnerConnectionId, mountFrame, cancellationToken);
                                    if (world.TryBuildUpdateEquipFrame(
                                            physicalOutcome.MountOwnerConnectionId,
                                            LegacyFrameCodec.CreateDefault(),
                                            mountTick,
                                            mountKeyword,
                                            out var mountEquipFrame) &&
                                        mountEquipFrame is not null)
                                    {
                                        await world.SendAsync(physicalOutcome.MountOwnerConnectionId, mountEquipFrame, cancellationToken);
                                        await world.BroadcastAsync(physicalOutcome.MountOwnerConnectionId, mountEquipFrame, cancellationToken);
                                    }
                                }
                                    if (physicalOutcome.CrimeStateUpdates is { Count: > 0 } crimeStateUpdates)
                                    {
                                        foreach (var crimeState in crimeStateUpdates)
                                        {
                                            var crimeFrame = BuildClientV769CreateMobFrame(
                                            (ushort)crimeState.ConnectionId,
                                            crimeState.PositionX,
                                            crimeState.PositionY,
                                                crimeState.MobSnapshot,
                                                ReadOnlySpan<byte>.Empty,
                                                npc: false,
                                                battleRoyaleState: world.GetCombatWorldState().BattleRoyaleState);
                                        await world.SendAsync(crimeState.ConnectionId, crimeFrame, cancellationToken);
                                            await world.BroadcastAsync(crimeState.ConnectionId, crimeFrame, cancellationToken);
                                        }
                                    }
                                    if (physicalOutcome.TargetAffectSnapshot is { } physicalTargetAffect)
                                    {
                                        if (physicalOutcome.TargetAffectViewRecipients.Count > 0)
                                        {
                                            await LegacyNpcAffectScoreRelay.RelayNpcAffectScoreAsync(
                                                world,
                                                physicalOutcome,
                                                LegacyFrameCodec.CreateDefault(),
                                                unchecked((uint)Environment.TickCount64),
                                                (byte)RandomNumberGenerator.GetInt32(256),
                                                cancellationToken);
                                        }
                                        else
                                        {
                                            var scoreFrame = BuildClientV769UpdateScoreFrame(
                                                physicalOutcome.TargetMobSnapshot,
                                                physicalTargetAffect,
                                                (ushort)physicalOutcome.TargetConnectionId);
                                            await world.SendAsync(physicalOutcome.TargetConnectionId, scoreFrame, cancellationToken);
                                            if (TryBuildClientV769UpdateAffectFrame(
                                                    physicalTargetAffect,
                                                    (ushort)physicalOutcome.TargetConnectionId,
                                                    unchecked((uint)Environment.TickCount64),
                                                    (byte)RandomNumberGenerator.GetInt32(256),
                                                    out var physicalAffectFrame))
                                                await world.SendAsync(physicalOutcome.TargetConnectionId, physicalAffectFrame, cancellationToken);
                                        }
                                    }
                                if (physicalOutcome.TargetRevived && physicalOutcome.ConsumedItem is { } consumedItem)
                                        await SendResurrectionRefreshAsync(world, physicalOutcome.TargetConnectionId, physicalOutcome.TargetMobSnapshot, physicalOutcome.ConsumedItemSlot, consumedItem, includeScoreAndResources: true, cancellationToken);
                                    if ((physicalOutcome.TargetHpChanged || physicalOutcome.TargetResourceChanged) && !physicalOutcome.TargetRevived && world.TryGetResourceState(physicalOutcome.TargetConnectionId, out var physicalTargetState, out var physicalTargetRequestedHp, out var physicalTargetRequestedMp) && physicalTargetState is not null)
                                {
                                        var resourceFrame = new SetHpMpConfirmation(physicalTargetState.CurrentScore.Hp, physicalTargetState.CurrentMana, physicalTargetRequestedHp, physicalTargetRequestedMp)
                                            .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)physicalOutcome.TargetConnectionId);
                                        await world.SendAsync(physicalOutcome.TargetConnectionId, resourceFrame, cancellationToken);
                                    }
                                    damageResolved = true;
                                Console.WriteLine($"Physical attack resolved: account={attackSession.AccountName}, slot={slotOutcome.SlotIndex}, target={targetConnectionId}, damage={physicalOutcome.Damage}, targetHp={physicalOutcome.RemainingHp}.");
                            }
                            else
                                Console.WriteLine($"Physical attack produced no damage: account={attackSession.AccountName}, target={targetConnectionId}, reason={physicalResult}.");
                        }
                        }
                        async Task RelaySkillSlotAsync(int requestedSlotIndex)
                        {
                            if (skillDefinition is null || actionBatch is null) return;
                            foreach (var batchSlot in actionBatch.SlotOutcomes.Where(slot => slot.Kind == LegacyAttackActionSlotKind.Skill))
                            {
                                if (batchSlot.SlotIndex != requestedSlotIndex) continue;
                                var targetIndex = batchSlot.SlotIndex;
                                var requestedSkillSlot = actionSlots.First(slot => slot.SlotIndex == targetIndex);
                                targetConnectionId = (ushort)requestedSkillSlot.TargetConnectionId;
                                var skillResult = batchSlot.SkillResult ?? LegacySkillAttackResult.UnsupportedSkillType;
                                var skillOutcome = batchSlot.SkillOutcome;
                                if (skillDefinition!.InstanceType is 9 or 12 &&
                                    LegacySkillSummonNoticeMapper.GetMessage(skillDefinition.InstanceType, skillResult) is { } summonNotice)
                                {
                                    var noticeFrame = new MessagePanelConfirmation(summonNotice)
                                        .ToFrame(LegacyFrameCodec.CreateDefault(), 0, 0);
                                    await stream.WriteAsync(noticeFrame, cancellationToken);
                                }
                                if (skillResult == LegacySkillAttackResult.TargetLimitExceeded)
                                {
                                    await RecordCrackErrorAsync(
                                        LegacyAttackTargetLimitGate.CrackErrorPoints,
                                        LegacyAttackTargetLimitGate.CrackErrorType);
                                    Console.WriteLine($"Ignored over-limit skill target: account={attackSession.AccountName}, skill={attack.SkillIndex}, slot={targetIndex}, maxTarget={skillDefinition.MaxTarget}.");
                                    continue;
                                }
                                if (skillResult == LegacySkillAttackResult.PartySkillBlocked)
                                {
                                    await RecordCrackErrorAsync(
                                        LegacyPartySkillTargetGate.CrackErrorPoints,
                                        LegacyPartySkillTargetGate.CrackErrorType);
                                    Console.WriteLine($"Ignored party-ineligible skill target: account={attackSession.AccountName}, skill={attack.SkillIndex}, slot={targetIndex}.");
                                    continue;
                                }
                                if (skillResult == LegacySkillAttackResult.Accepted && skillOutcome is not null)
                                {
                                    if (skillOutcome.ZardMobLeftCount is { } zardMobLeftCount)
                                    {
                                        var mobLeftFrame = new MobLeftConfirmation(zardMobLeftCount)
                                            .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
                                        await stream.WriteAsync(mobLeftFrame, cancellationToken);
                                    }
                                    if (skillOutcome.TargetRemoved && removedMobIds.Add(skillOutcome.TargetConnectionId))
                                        deferredAttackEffects.Add((batchSlot.SlotIndex, int.MaxValue, skillOutcome.TargetConnectionId, null, null));
                                    if (skillOutcome.PistaTransition?.SpawnGenerateIndex > 0 &&
                                        world.TrySpawnGeneratedNpcGroup(skillOutcome.PistaTransition.SpawnGenerateIndex, out var skillSpawnGroup) &&
                                        skillSpawnGroup is not null)
                                        foreach (var skillSpawnedNpc in skillSpawnGroup)
                                            spawnedNpcs.Add((batchSlot.SlotIndex, skillSpawnedNpc));
                                    if (skillOutcome.PistaTransition?.SpawnGenerateIndexes is { Count: > 0 } skillSpawnIndexes)
                                    {
                                        foreach (var generateIndex in skillSpawnIndexes)
                                            if (world.TrySpawnGeneratedNpcGroup(generateIndex, out var generatedGroup) && generatedGroup is not null)
                                                foreach (var generatedNpc in generatedGroup)
                                                    spawnedNpcs.Add((batchSlot.SlotIndex, generatedNpc));
                                    }
                                    if (skillOutcome.PistaTransition?.RemovedNpcConnectionIds is { Count: > 0 } skillRemovedNpcIds)
                                        foreach (var removedNpcId in skillRemovedNpcIds)
                                            if (removedMobIds.Add(removedNpcId))
                                                deferredAttackEffects.Add((batchSlot.SlotIndex, int.MaxValue, removedNpcId, null, null));
                                    if (skillOutcome.PistaTransition?.Teleports is { Count: > 0 } skillPistaTeleports)
                                        await BroadcastPistaTeleportsAsync(world, skillPistaTeleports, cancellationToken);
                                    if (skillOutcome.ExperienceAwards is { Count: > 0 } skillExperienceAwards)
                                    {
                                        foreach (var award in skillExperienceAwards.Where(award => award.ConnectionId != connectionId))
                                            deferredAttackEffects.Add((batchSlot.SlotIndex, deferredAttackEffects.Count, null, award, null));
                                    }
                                    if (skillOutcome.ItemDrops is { Count: > 0 } skillItemDrops)
                                    {
                                        foreach (var drop in skillItemDrops)
                                            deferredAttackEffects.Add((batchSlot.SlotIndex, deferredAttackEffects.Count, null, null, drop));
                                    }
                                    if (skillOutcome.CoinUpdates is { Count: > 0 } skillCoinUpdates)
                                    {
                                        foreach (var update in skillCoinUpdates)
                                        {
                                            if (update.ConnectionId == connectionId && skillOutcome.AttackerMobSnapshot is not null)
                                                continue;
                                            var coinFrame = BuildClientV769UpdateEtcFrame(
                                                update.MobSnapshot,
                                                ReadOnlySpan<byte>.Empty,
                                                (ushort)update.ConnectionId);
                                            await world.SendAsync(update.ConnectionId, coinFrame, cancellationToken);
                                        }
                                    }
                                    if (skillOutcome.MountOwnerConnectionId > 0 && skillOutcome.UpdatedMountItem is { } skillUpdatedMountItem)
                                    {
                                        var mountTick = unchecked((uint)Environment.TickCount64);
                                        var mountKeyword = (byte)RandomNumberGenerator.GetInt32(256);
                                        var mountFrame = new SendItemConfirmation(0, 14, skillUpdatedMountItem)
                                            .ToFrame(LegacyFrameCodec.CreateDefault(), mountTick, mountKeyword, (ushort)skillOutcome.MountOwnerConnectionId);
                                        await world.SendAsync(skillOutcome.MountOwnerConnectionId, mountFrame, cancellationToken);
                                        if (world.TryBuildUpdateEquipFrame(
                                                skillOutcome.MountOwnerConnectionId,
                                                LegacyFrameCodec.CreateDefault(),
                                                mountTick,
                                                mountKeyword,
                                                out var skillMountEquipFrame) &&
                                            skillMountEquipFrame is not null)
                                        {
                                            await world.SendAsync(skillOutcome.MountOwnerConnectionId, skillMountEquipFrame, cancellationToken);
                                            await world.BroadcastAsync(skillOutcome.MountOwnerConnectionId, skillMountEquipFrame, cancellationToken);
                                        }
                                    }
                                    if (skillOutcome.PrivateNotices is { Count: > 0 } skillPrivateNotices)
                                        await SendPrivateNoticesAsync(world, skillPrivateNotices, cancellationToken);
                                    if (skillOutcome.CrimeStateUpdates is { Count: > 0 } skillCrimeStateUpdates)
                                    {
                                        foreach (var crimeState in skillCrimeStateUpdates)
                                        {
                                            var crimeFrame = BuildClientV769CreateMobFrame(
                                                (ushort)crimeState.ConnectionId,
                                                crimeState.PositionX,
                                                crimeState.PositionY,
                                                crimeState.MobSnapshot,
                                                ReadOnlySpan<byte>.Empty,
                                                npc: false,
                                                battleRoyaleState: world.GetCombatWorldState().BattleRoyaleState);
                                            await world.SendAsync(crimeState.ConnectionId, crimeFrame, cancellationToken);
                                            await world.BroadcastAsync(crimeState.ConnectionId, crimeFrame, cancellationToken);
                                        }
                                    }
                                    if (skillOutcome.GlobalNotices is { Count: > 0 } skillGlobalNotices)
                                        await BroadcastGlobalNoticesAsync(world, skillGlobalNotices, cancellationToken);
                                    if (skillOutcome.AreaNotices is { Count: > 0 } skillAreaNotices)
                                        await BroadcastAreaNoticesAsync(world, skillAreaNotices, cancellationToken);
                                    if (skillDefinition?.Id == 31 && skillOutcome.AttackerResourceChanged &&
                                        world.TryGetResourceState(connectionId, out var renascimentoCasterState, out var renascimentoCasterHp, out var renascimentoCasterMp) &&
                                        renascimentoCasterState is not null)
                                    {
                                        // Renascimento sends the caster SetHpMp before its success check.
                                        var casterResourceFrame = new SetHpMpConfirmation(
                                            renascimentoCasterState.CurrentScore.Hp,
                                            renascimentoCasterState.CurrentMana,
                                            renascimentoCasterHp,
                                            renascimentoCasterMp)
                                            .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                        await stream.WriteAsync(casterResourceFrame, cancellationToken);
                                    }
                                    if (skillDefinition?.Id == 31 && skillOutcome.TargetResourceChanged)
                                        _ = sessions.TryResetCrackErrorPoints(skillOutcome.TargetConnectionId);
                                    if (skillOutcome.TargetAffectSnapshot is not null &&
                                        world.TryGetNpcSnapshot(skillOutcome.TargetConnectionId, out _))
                                    {
                                        await LegacyNpcAffectScoreRelay.RelayNpcAffectScoreAsync(
                                            world,
                                            skillOutcome,
                                            LegacyFrameCodec.CreateDefault(),
                                            frame.Header.ClientTick,
                                            (byte)RandomNumberGenerator.GetInt32(256),
                                            cancellationToken);
                                    }
                                    else if (skillOutcome.TargetAffectSnapshot is not null || (skillOutcome.TargetResourceChanged && !skillOutcome.TargetRevived))
                                    {
                                        var targetScoreAffect = skillOutcome.TargetAffectSnapshot;
                                        if (targetScoreAffect is null && skillDefinition?.Id == 31 &&
                                            world.TryGetCharacterSnapshot(
                                                skillOutcome.TargetConnectionId,
                                                out _,
                                                out _,
                                                out _,
                                                out _,
                                                out var currentTargetAffect))
                                            targetScoreAffect = currentTargetAffect;
                                        var encodedScoreFrame = targetScoreAffect is not null
                                            ? BuildClientV769UpdateScoreFrame(
                                                skillOutcome.TargetMobSnapshot,
                                                targetScoreAffect,
                                                (ushort)skillOutcome.TargetConnectionId)
                                            : BuildClientV769UpdateScoreWithoutAffectFrame(
                                                skillOutcome.TargetMobSnapshot,
                                                (ushort)skillOutcome.TargetConnectionId);
                                        var targetViewRecipients = skillOutcome.TargetEquipmentChanged
                                            ? world.GetParticipantIdsInPlayerView(skillOutcome.TargetConnectionId)
                                            : null;
                                        if (targetViewRecipients is { Count: > 0 })
                                            await world.BroadcastToParticipantsAsync(targetViewRecipients, encodedScoreFrame, cancellationToken);
                                        else
                                            await world.SendAsync(skillOutcome.TargetConnectionId, encodedScoreFrame, cancellationToken);
                                        if (skillOutcome.TargetAffectSnapshot is { } skillTargetAffect &&
                                            TryBuildClientV769UpdateAffectFrame(
                                                skillTargetAffect,
                                                (ushort)skillOutcome.TargetConnectionId,
                                                unchecked((uint)Environment.TickCount64),
                                                (byte)RandomNumberGenerator.GetInt32(256),
                                                out var skillAffectFrame))
                                            await world.SendAsync(skillOutcome.TargetConnectionId, skillAffectFrame, cancellationToken);

                                        if (skillOutcome.TargetEquipmentChanged &&
                                            world.TryBuildUpdateEquipFrame(
                                                skillOutcome.TargetConnectionId,
                                                LegacyFrameCodec.CreateDefault(),
                                                unchecked((uint)Environment.TickCount64),
                                                (byte)RandomNumberGenerator.GetInt32(256),
                                                out var transformationEquipFrame) &&
                                            transformationEquipFrame is not null &&
                                            targetViewRecipients is { Count: > 0 })
                                            await world.BroadcastToParticipantsAsync(targetViewRecipients, transformationEquipFrame, cancellationToken);
                                    }
                                    if (skillOutcome.TargetResourceChanged && !skillOutcome.TargetRevived && world.TryGetResourceState(skillOutcome.TargetConnectionId, out var targetResourceState, out var targetRequestedHp, out var targetRequestedMp) && targetResourceState is not null)
                                    {
                                        var resourceFrame = new SetHpMpConfirmation(targetResourceState.CurrentScore.Hp, targetResourceState.CurrentMana, targetRequestedHp, targetRequestedMp)
                                            .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)skillOutcome.TargetConnectionId);
                                        await world.SendAsync(skillOutcome.TargetConnectionId, resourceFrame, cancellationToken);
                                    }
                                    if (skillOutcome.TargetHpChanged && !skillOutcome.TargetRevived && world.TryGetResourceState(skillOutcome.TargetConnectionId, out var hpTargetState, out var hpTargetRequestedHp, out var hpTargetRequestedMp) && hpTargetState is not null)
                                    {
                                        var resourceFrame = new SetHpMpConfirmation(hpTargetState.CurrentScore.Hp, hpTargetState.CurrentMana, hpTargetRequestedHp, hpTargetRequestedMp)
                                            .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)skillOutcome.TargetConnectionId);
                                        await world.SendAsync(skillOutcome.TargetConnectionId, resourceFrame, cancellationToken);
                                    }
                                    if (skillDefinition?.Id == 31 && skillOutcome.TargetResourceChanged &&
                                        world.TryGetCharacterSnapshot(skillOutcome.TargetConnectionId, out _, out _, out _, out var renascimentoMobExtra))
                                    {
                                        var etcFrame = BuildClientV769UpdateEtcFrame(
                                            skillOutcome.TargetMobSnapshot,
                                            renascimentoMobExtra ?? Array.Empty<byte>(),
                                            (ushort)skillOutcome.TargetConnectionId);
                                        await world.SendAsync(skillOutcome.TargetConnectionId, etcFrame, cancellationToken);
                                    }
                                    if (skillDefinition?.Id == 31 && skillOutcome.TargetAffectSnapshot is { } renascimentoAffect &&
                                        world.TryGetCharacterSnapshot(
                                            skillOutcome.TargetConnectionId,
                                            out _,
                                            out var renascimentoPositionX,
                                            out var renascimentoPositionY,
                                            out _,
                                            out _,
                                            out var renascimentoClientEquipment))
                                    {
                                        var createMobFrame = BuildClientV769CreateMobFrame(
                                            checked((ushort)skillOutcome.TargetConnectionId),
                                            renascimentoPositionX,
                                            renascimentoPositionY,
                                            skillOutcome.TargetMobSnapshot,
                                            renascimentoAffect,
                                            npc: false,
                                            battleRoyaleState: world.GetCombatWorldState().BattleRoyaleState,
                                            clientEquipment: renascimentoClientEquipment);
                                        await world.BroadcastAsync(skillOutcome.TargetConnectionId, createMobFrame, cancellationToken);
                                    }
                                    if (skillOutcome.TargetRevived && skillOutcome.ConsumedItem is { } consumedItem)
                                        await SendResurrectionRefreshAsync(world, skillOutcome.TargetConnectionId, skillOutcome.TargetMobSnapshot, skillOutcome.ConsumedItemSlot, consumedItem, includeScoreAndResources: true, cancellationToken);
                                    if (skillOutcome.ActionRelay is { } skillActionRelay)
                                    {
                                        var actionActorConnectionId = skillActionRelay.ActorConnectionId > 0
                                            ? skillActionRelay.ActorConnectionId
                                            : connectionId;
                                        var actionFrame = new ActionRequest(
                                            skillActionRelay.PositionX,
                                            skillActionRelay.PositionY,
                                            skillActionRelay.Effect,
                                            skillActionRelay.Speed,
                                            new byte[24],
                                            skillActionRelay.TargetX,
                                            skillActionRelay.TargetY)
                                            .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), checked((ushort)actionActorConnectionId));
                                        if (actionActorConnectionId == connectionId)
                                            await stream.WriteAsync(actionFrame, cancellationToken);
                                        else
                                            await world.SendAsync(actionActorConnectionId, actionFrame, cancellationToken);
                                        await world.BroadcastAsync(actionActorConnectionId, actionFrame, cancellationToken);
                                    }
                                    if (skillDefinition?.Id != 30 && skillDefinition?.Id != 31 && skillOutcome.AttackerResourceChanged && world.TryGetResourceState(connectionId, out var summonResourceState, out var summonRequestedHp, out var summonRequestedMp) && summonResourceState is not null)
                                    {
                                        var resourceFrame = new SetHpMpConfirmation(summonResourceState.CurrentScore.Hp, summonResourceState.CurrentMana, summonRequestedHp, summonRequestedMp)
                                            .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                        await stream.WriteAsync(resourceFrame, cancellationToken);
                                    }
                                    if (skillOutcome.UpdatesAttackerState && world.TryGetResourceState(connectionId, out var revivedState, out var revivedHp, out var revivedMp) && revivedState is not null)
                                    {
                                        if (skillOutcome.HasRecallPosition)
                                        {
                                            sessions.SetServerPosition(connectionId, skillOutcome.RecallPositionX, skillOutcome.RecallPositionY);
                                            var recallFrame = new ActionRequest(
                                                skillOutcome.RecallPositionX,
                                                skillOutcome.RecallPositionY,
                                                Effect: 1,
                                                Speed: 0,
                                                Route: new byte[24],
                                                skillOutcome.RecallPositionX,
                                                skillOutcome.RecallPositionY)
                                                .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                            await stream.WriteAsync(recallFrame, cancellationToken);
                                            await world.BroadcastAsync(connectionId, recallFrame, cancellationToken);
                                        }
                                        var scoreFrame = BuildClientV769UpdateScoreWithoutAffectFrame(
                                            skillOutcome.TargetMobSnapshot,
                                            (ushort)connectionId);
                                        await stream.WriteAsync(scoreFrame, cancellationToken);
                                        var hpMpFrame = new SetHpMpConfirmation(revivedState.CurrentScore.Hp, revivedState.CurrentMana, revivedHp, revivedMp)
                                            .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                        await stream.WriteAsync(hpMpFrame, cancellationToken);
                                    }
                                    if (skillOutcome.HasTargetPosition)
                                    {
                                        var summonFrame = new ActionRequest(
                                            skillOutcome.TargetPositionX,
                                            skillOutcome.TargetPositionY,
                                            Effect: 1,
                                            Speed: 0,
                                            Route: new byte[24],
                                            skillOutcome.TargetPositionX,
                                            skillOutcome.TargetPositionY)
                                            .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)skillOutcome.TargetConnectionId);
                                        await world.SendAsync(skillOutcome.TargetConnectionId, summonFrame, cancellationToken);
                                        await world.BroadcastAsync(skillOutcome.TargetConnectionId, summonFrame, cancellationToken);
                                    }
                                    if (skillOutcome.SummonedMobs is not null)
                                    {
                                        foreach (var summonedMob in skillOutcome.SummonedMobs)
                                        {
                                            var createFrame = BuildClientV769CreateMobFrame(
                                                (ushort)summonedMob.ConnectionId,
                                                summonedMob.PositionX,
                                                summonedMob.PositionY,
                                                summonedMob.MobSnapshot,
                                                summonedMob.AffectSnapshot,
                                                npc: true,
                                                battleRoyaleState: world.GetCombatWorldState().BattleRoyaleState,
                                                summon: true);
                                            // 7.69 GenerateSummon: GridMulticast at the summon tile.
                                            await world.BroadcastToParticipantsAsync(
                                                world.GetParticipantIdsInView(summonedMob.PositionX, summonedMob.PositionY),
                                                createFrame,
                                                cancellationToken);
                                        }
                                    }
                                    foreach (var recalledSummon in skillOutcome.RecalledSummons ?? [])
                                    {
                                        // GetAction + GridMulticast(partyconn, posX, posY): Effect 8, Speed 6, old and new views.
                                        var recallFrame = new ActionRequest(
                                            recalledSummon.FromX,
                                            recalledSummon.FromY,
                                            recalledSummon.Effect,
                                            recalledSummon.Speed,
                                            new byte[24],
                                            recalledSummon.ToX,
                                            recalledSummon.ToY)
                                            .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)recalledSummon.ConnectionId);
                                        var recallRecipients = world.GetParticipantIdsInView(recalledSummon.FromX, recalledSummon.FromY)
                                            .Union(world.GetParticipantIdsInView(recalledSummon.ToX, recalledSummon.ToY))
                                            .ToArray();
                                        await world.BroadcastToParticipantsAsync(recallRecipients, recallFrame, cancellationToken);
                                    }
                                    if (skillOutcome.CapturedSummon is { } capturedSummon)
                                    {
                                        // 7.69 InstanceType 12 capture: plain CreateMob (no summon CreateType) by GridMulticast
                                        // at the mob tile, then the SendAddParty frames to the party users.
                                        var capturedCreateFrame = BuildClientV769CreateMobFrame(
                                            (ushort)capturedSummon.ConnectionId,
                                            capturedSummon.PositionX,
                                            capturedSummon.PositionY,
                                            capturedSummon.MobSnapshot,
                                            capturedSummon.AffectSnapshot,
                                            npc: true,
                                            battleRoyaleState: world.GetCombatWorldState().BattleRoyaleState);
                                        await world.BroadcastToParticipantsAsync(skillOutcome.CaptureViewRecipients ?? [], capturedCreateFrame, cancellationToken);
                                    }
                                    // InstanceType 11 GenerateSummon and the InstanceType 12 capture both end in SendAddParty.
                                    if (skillOutcome.PartyAddNotices is { Count: > 0 } partyAddNotices)
                                        await SendPartyAddNoticesAsync(world, partyAddNotices, cancellationToken);
                                    damageResolved = true;
                                    Console.WriteLine($"Skill attack resolved: account={attackSession.AccountName}, skill={attack.SkillIndex}, targetIndex={targetIndex}, target={targetConnectionId}, baseDamage={skillOutcome.BaseDamage}, resistance={skillOutcome.Resistance}, damage={skillOutcome.Damage}, targetHp={skillOutcome.RemainingHp}, summons={skillOutcome.SummonedMobs?.Count ?? 0}, summonResult={skillOutcome.SummonResult}.");
                                }
                                else
                                    Console.WriteLine($"Skill attack produced no damage: account={attackSession.AccountName}, skill={attack.SkillIndex}, targetIndex={targetIndex}, target={targetConnectionId}, reason={skillResult}.");
                            }
                        }

                        if (actionBatch is not null)
                        {
                            foreach (var batchSlot in actionBatch.SlotOutcomes)
                            {
                                if (batchSlot.Kind == LegacyAttackActionSlotKind.Physical)
                                    await RelayPhysicalSlotAsync(batchSlot.SlotIndex);
                                else
                                    await RelaySkillSlotAsync(batchSlot.SlotIndex);
                            }
                        }
                        if (deferredType9AggregateAttackFrame is not null && actionBatch?.CompletedNormally == true)
                        {
                            await stream.WriteAsync(deferredType9AggregateAttackFrame, cancellationToken);
                            if (attack.SkillIndex != 102)
                            {
                                var attackRecipients = world.GetParticipantIdsInPlayerView(connectionId)
                                    .Where(recipientId => recipientId != connectionId)
                                    .ToArray();
                                await world.BroadcastToParticipantsAsync(attackRecipients, deferredType9AggregateAttackFrame, cancellationToken);
                            }
                            physicalAttackRelaySent = true;
                        }
                        if (physicalBatch is { CompletedNormally: false })
                            Console.WriteLine($"Aborted physical attack batch: account={attackSession.AccountName}, skill={attack.SkillIndex}, slot={physicalBatch.AbortSlotIndex}; partial state retained, attack relay suppressed.");

                        // This is the legacy attack relay with authoritative HP/MP fields. Dam[] remains untouched
                        // for unsupported skill types; physical and elemental skill damage are filled only from WorldHub state.
                        if (!physicalAttackRelaySent && actionBatch?.CompletedNormally != false)
                        {
                            var attackFrame = attackConfirmation.ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256));
                            await stream.WriteAsync(attackFrame, cancellationToken);
                            if (attack.SkillIndex != 102)
                            {
                                var attackRecipients = world.GetParticipantIdsInPlayerView(connectionId)
                                    .Where(recipientId => recipientId != connectionId)
                                    .ToArray();
                                await world.BroadcastToParticipantsAsync(attackRecipients, attackFrame, cancellationToken);
                            }
                        }
                        if (skillDefinition?.Id == 30 &&
                            actionBatch?.CompletedNormally == true &&
                            actionBatch.SlotOutcomes.Any(slot => slot.SkillOutcome?.AttackerResourceChanged == true) &&
                            world.TryGetResourceState(connectionId, out var judgmentDivineState, out var judgmentDivineRequestedHp, out var judgmentDivineRequestedMp) &&
                            judgmentDivineState is not null)
                        {
                            var resourceFrame = new SetHpMpConfirmation(
                                    judgmentDivineState.CurrentScore.Hp,
                                    judgmentDivineState.CurrentMana,
                                    judgmentDivineRequestedHp,
                                    judgmentDivineRequestedMp)
                                .ToFrame(
                                    LegacyFrameCodec.CreateDefault(),
                                    unchecked((uint)Environment.TickCount64),
                                    (byte)RandomNumberGenerator.GetInt32(256),
                                    (ushort)connectionId);
                            await stream.WriteAsync(resourceFrame, cancellationToken);
                        }
                        foreach (var deferredEffect in deferredAttackEffects.OrderBy(effect => effect.SlotIndex).ThenBy(effect => effect.Sequence))
                        {
                            if (deferredEffect.RemovedMobId is { } removedMobIdValue)
                            {
                                var removeFrame = new RemoveMobConfirmation((ushort)removedMobIdValue)
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
                                await stream.WriteAsync(removeFrame, cancellationToken);
                                await world.BroadcastAsync(connectionId, removeFrame, cancellationToken);
                            }
                            else if (deferredEffect.Award is { } award)
                            {
                                var experienceFrame = BuildClientV769UpdateEtcFrame(
                                    award.MobSnapshot,
                                    ReadOnlySpan<byte>.Empty,
                                    (ushort)award.ConnectionId);
                                await world.SendAsync(award.ConnectionId, experienceFrame, cancellationToken);
                            }
                            else if (deferredEffect.Drop is { } drop)
                            {
                                var itemFrame = new SendItemConfirmation(1, checked((short)drop.InventorySlot), drop.Item)
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)drop.ConnectionId);
                                await world.SendAsync(drop.ConnectionId, itemFrame, cancellationToken);
                            }
                        }
                        var orderedGeneratedNpcs = spawnedNpcs
                            .OrderBy(static entry => entry.SlotIndex)
                            .Select(static entry => entry.Npc)
                            .ToArray();
                        await LegacyNpcGenerationRelay.RelayGroupAsync(
                            world,
                            orderedGeneratedNpcs,
                            spawnedNpc => BuildClientV769CreateMobFrame(
                                (ushort)spawnedNpc.ConnectionId,
                                spawnedNpc.PositionX,
                                spawnedNpc.PositionY,
                                spawnedNpc.MobSnapshot,
                                spawnedNpc.AffectSnapshot,
                                npc: true,
                                battleRoyaleState: world.GetCombatWorldState().BattleRoyaleState),
                            cancellationToken);
                        Console.WriteLine($"Attack mana accepted: account={attackSession.AccountName}, skill={attack.SkillIndex}, mp={requestedMp}; damage={(damageResolved ? "resolved" : "pending")}.");
                        continue;
                    }

                    if (CharacterLogoutRequest.IsValid(frame))
                    {
                        await LogoutCharacterAsync();
                        Console.WriteLine($"Character logout acknowledged: connection={connectionId}.");
                        continue;
                    }

                    Console.WriteLine($"Ignored frame: type=0x{frame.Header.Type:X4}, size={frame.Header.Size}, checksum={frame.IsChecksumValid}");
                }
            }
        }
        catch (InvalidDataException error)
        {
            serverLog?.Write($"CONNECTION INVALID connection={connectionId} error={error.Message}");
            Console.WriteLine($"Rejected connection: {error.Message}");
        }
        catch (Exception exception)
        {
            serverLog?.Write($"CONNECTION ERROR connection={connectionId} error={exception.GetType().Name}: {exception.Message}");
            Console.WriteLine($"Connection terminated before completion: connection={connectionId}, error={exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            try
            {
                var disconnectPlan = await LegacyAutoTradeDepartureCoordinator.DepartAsync(connectionId, world, autoTradeBook,
                    async (accountLease, ct) =>
                    {
                        serverLog?.Write($"CONNECTION FINALLY connection={connectionId} state={(sessions.TryGet(connectionId, out var finalSession) ? finalSession!.State : LoginSessionState.Closed)}");
                        if (sessions.TryGet(connectionId, out var disconnectedSession) && disconnectedSession?.State == LoginSessionState.Playing)
                            await SaveCharacterOnDepartureAsync(accounts, characterCheckpointRuntime, world, disconnectedSession, disconnect: true, ct, accountLease);
                    },
                    async (shop, ct) =>
                    {
                        if (sessions.TryGet(connectionId, out var endedSession))
                            await RemoveAutoTradeStateAsync(autoTradeStateStore, endedSession!.AccountName, endedSession.CharacterSlot, shop, ct);
                    },
                    LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256),
                    accountOperationGate, CancellationToken.None);
                await BroadcastAutoTradeDepartureAsync(world, disconnectPlan, CancellationToken.None);
                if (disconnectPlan.ClosedTrade is { } disconnectedTrade)
                    Console.WriteLine($"Trade cancelled by disconnect: connection={disconnectedTrade.ConnectionId}, opponent={disconnectedTrade.OpponentId}.");
                await BroadcastSummonDespawnsAsync(world, disconnectPlan.DespawnedSummonIds, CancellationToken.None);
                serverLog?.Write($"CONNECTION CLOSED connection={connectionId}");
            }
            finally
            {
                if (sessions.TryGet(connectionId, out var finalCargoSession) && finalCargoSession?.AccountName is { } cargoAccountName)
                    accountCargoRuntime.UnregisterSession(cargoAccountName, connectionId);
                sessions.Close(connectionId);
            }
        }
    }
}

static async ValueTask<LegacyAccountOperationLease> AcquirePartyAccountLeaseAsync(
    LegacyAccountOperationGate gate,
    WorldHub world,
    int partyAnchorConnectionId,
    CancellationToken cancellationToken,
    params int[] additionalConnectionIds)
{
    while (true)
    {
        var accountNames = world.GetPartyMemberAccountNames(partyAnchorConnectionId)
            .Concat(additionalConnectionIds.Select(world.GetParticipantAccountName).OfType<string>())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (accountNames.Length == 0)
            throw new InvalidOperationException("The party does not have an authoritative account set.");

        var lease = await gate.AcquireAsync(accountNames, cancellationToken);
        var currentNames = world.GetPartyMemberAccountNames(partyAnchorConnectionId)
            .Concat(additionalConnectionIds.Select(world.GetParticipantAccountName).OfType<string>())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (currentNames.All(accountName => lease.Covers(gate, accountName)))
            return lease;
        await lease.DisposeAsync();
    }
}

static async Task BroadcastCargoResyncAsync(
    LegacyAccountCargoSnapshot cargo,
    LoginSessionRegistry sessions,
    WorldHub world,
    CancellationToken cancellationToken)
{
    ArgumentNullException.ThrowIfNull(cargo);
    var recipients = sessions.GetPlayingForAccount(cargo.AccountName);
    if (recipients.Count == 0 || cargo.ChangedPositions.Count == 0)
        return;

    var codec = LegacyFrameCodec.CreateDefault();
    var tick = unchecked((uint)Environment.TickCount64);
    foreach (var session in recipients)
    {
        foreach (var position in cargo.ChangedPositions.Order())
        {
            if (position is < 0 or >= LegacyAccountSnapshot.CargoCount)
                continue;
            var frame = new SendItemConfirmation(
                    LegacyItemPlace.Cargo,
                    checked((short)position),
                    cargo.Items[position])
                .ToFrame(codec, tick, keywordIndex: 0, id: checked((ushort)session.ConnectionId));
            await world.SendAsync(session.ConnectionId, frame, cancellationToken);
        }
    }
}

static byte[] BuildClientV769CreateMobFrame(
    ushort mobId,
    short positionX,
    short positionY,
    ReadOnlySpan<byte> mob,
    ReadOnlySpan<byte> affect,
    bool npc,
    int battleRoyaleState,
    bool summon = false,
    IReadOnlyList<LegacyItem>? clientEquipment = null)
{
    var sourcePayload = new CreateMobConfirmation(
        mobId,
        positionX,
        positionY,
        mob,
        affect,
        npc,
        summon).ToPayload();
    var appearance = clientEquipment is null
        ? null
        : LegacyAutoTradeVisualRelay.BuildEquipmentAppearance(mob, clientEquipment);
    var codec = LegacyFrameCodec.CreateDefault();
    var createMobFrame = W2ppCreateMobV1Adapter.AdaptPayload(sourcePayload, appearance?.VisualEquipment, appearance?.AncientCodes)
        .ToFrame(codec, unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
    return LegacyBattleRoyaleCreateMobVisualRelay.Apply(createMobFrame, battleRoyaleState, codec);
}

static byte[] BuildClientV769UpdateEtcFrame(
    ReadOnlySpan<byte> mob,
    ReadOnlySpan<byte> mobExtra,
    ushort id,
    uint? clientTick = null,
    byte? keywordIndex = null)
{
    var sourcePayload = new UpdateEtcConfirmation(mob, mobExtra).ToPayload();
    return W2ppUpdateEtcV1Adapter.AdaptPayload(sourcePayload.AsSpan(0, W2ppUpdateEtcV1Adapter.SourcePayloadSize))
        .ToFrame(
            LegacyFrameCodec.CreateDefault(),
            clientTick ?? unchecked((uint)Environment.TickCount64),
            keywordIndex ?? (byte)RandomNumberGenerator.GetInt32(256),
            id);
}

static byte[] BuildClientV769UpdateScoreFrame(
    ReadOnlySpan<byte> mob,
    ReadOnlySpan<byte> affect,
    ushort id,
    uint? clientTick = null,
    byte? keywordIndex = null)
{
    var sourcePayload = new UpdateScoreConfirmation(mob, affect).ToPayload();
    return W2ppUpdateScoreV1Adapter.AdaptPayload(
            sourcePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize),
            LegacyMobCombatState.Read(mob).Rsv)
        .ToFrame(
            LegacyFrameCodec.CreateDefault(),
            clientTick ?? unchecked((uint)Environment.TickCount64),
            keywordIndex ?? (byte)RandomNumberGenerator.GetInt32(256),
            id);
}

static byte[] BuildClientV769UpdateScoreWithoutAffectFrame(
    ReadOnlySpan<byte> mob,
    ushort id,
    uint? clientTick = null,
    byte? keywordIndex = null)
{
    // The target UpdateScore contract always carries the complete 32-entry
    // affect projection. A missing legacy snapshot means all effects inactive.
    return BuildClientV769UpdateScoreFrame(
        mob,
        new byte[LegacyAccountSnapshot.AffectStride],
        id,
        clientTick,
        keywordIndex);
}

static bool TryBuildClientV769UpdateAffectFrame(
    ReadOnlySpan<byte> affect,
    ushort id,
    uint clientTick,
    byte keywordIndex,
    out byte[]? frame)
{
    frame = null;
    try
    {
        frame = W2ppUpdateAffectV1Adapter.AdaptPayload(affect)
            .ToFrame(LegacyFrameCodec.CreateDefault(), id, clientTick, keywordIndex);
        return true;
    }
    catch (ArgumentException)
    {
        return false;
    }
    catch (OverflowException)
    {
        // W2PP can store levels/times that do not fit the signed 7.69 affect
        // fields. Do not truncate an active effect into a misleading relay.
        return false;
    }
}

static async ValueTask WriteLoggedFrameAsync(Stream stream, ServerWireLog? serverLog, int connectionId, ReadOnlyMemory<byte> frame, CancellationToken cancellationToken, string? note = null)
{
    serverLog?.WriteRawFrame("TX", connectionId, frame.Span, LegacyFrameCodec.CreateDefault(), note);
    await stream.WriteAsync(frame, cancellationToken);
    await stream.FlushAsync(cancellationToken);
}

static async ValueTask<string> ReadDonatePixAsync(IDonateBalanceStore? donateBalances, string accountName, CancellationToken cancellationToken)
{
    if (donateBalances is not IDonatePixStore pixStore)
        return string.Empty;
    return await pixStore.ReadDonatePixAsync(accountName, cancellationToken) ?? string.Empty;
}

static async Task RunSummonBattleLoopAsync(WorldHub world, CancellationToken cancellationToken)
{
    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(800));
    var codec = LegacyFrameCodec.CreateDefault();
    try
    {
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            if (!world.TryApplyAllSummonBattleOpportunities(randomFactor: -1, out var opportunities))
                continue;

            foreach (var opportunity in opportunities)
            {
                if (opportunity.AffectUpdate is { } affectUpdate)
                {
                    await LegacyMobAiRelay.RelayAsync(
                        world,
                        affectUpdate,
                        codec,
                        sendResurrectionRefresh: null,
                        cancellationToken).ConfigureAwait(false);
                }

                if (opportunity.Despawn is { } despawn)
                {
                    await LegacyMobAiRelay.RelayAsync(
                        world,
                        despawn,
                        codec,
                        sendResurrectionRefresh: null,
                        cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (opportunity.Movement is { } movement)
                {
                    await LegacyMobAiRelay.RelayAsync(
                        world,
                        movement,
                        codec,
                        sendResurrectionRefresh: null,
                        cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (opportunity.Attack is not { } summonAttack)
                    continue;
                try
                {
                    var frame = new NpcAttackConfirmation(
                        summonAttack.SummonPositionX,
                        summonAttack.SummonPositionY,
                        summonAttack.TargetPositionX,
                        summonAttack.TargetPositionY,
                        checked((ushort)summonAttack.SummonConnectionId),
                        checked((ushort)summonAttack.TargetConnectionId),
                        motion: 0,
                        skillIndex: -1,
                        summonAttack.Damage)
                        .ToFrame(codec, unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
                    await world.SendAsync(summonAttack.TargetConnectionId, frame, cancellationToken);
                    await world.BroadcastAsync(summonAttack.TargetConnectionId, frame, cancellationToken);
                    Console.WriteLine($"Summon attack resolved: summon={summonAttack.SummonConnectionId}, target={summonAttack.TargetConnectionId}, damage={summonAttack.Damage}, targetHp={summonAttack.RemainingHp}.");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (IOException)
                {
                    // The connection cleanup path will remove the stale participant.
                }
                catch (ObjectDisposedException)
                {
                    // The connection cleanup path will remove the stale participant.
                }
            }
        }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
}

static async Task RunPistaScheduleLoopAsync(WorldHub world, CancellationToken cancellationToken)
{
    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
    try
    {
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var now = DateTime.Now;
            var mobCountCodec = LegacyFrameCodec.CreateDefault();
            if (world.TryProcessPistaLv6MobCount(
                    now,
                    mobCountCodec,
                    unchecked((uint)Environment.TickCount64),
                    (byte)RandomNumberGenerator.GetInt32(256),
                    out var mobCountFrames)
                && mobCountFrames is { Count: > 0 })
            {
                foreach (var mobCount in mobCountFrames)
                    await world.SendAsync(mobCount.ConnectionId, mobCount.Frame, cancellationToken);
            }

            if (world.TryProcessCastleQuestMinute(now, out var castleQuestPlan) && castleQuestPlan is not null)
            {
                await BroadcastAreaNoticesAsync(world, castleQuestPlan.AreaNotices, cancellationToken);
                if (castleQuestPlan.RemovedNpcs.Count > 0)
                    await BroadcastRemovedNpcsInAreaAsync(world, castleQuestPlan.RemovedNpcs, 2176, 1160, 2300, 1276, cancellationToken);
                Console.WriteLine($"Castle quest timer processed: slot={castleQuestPlan.MinuteSlot:yyyy-MM-dd HH:mm}, state={castleQuestPlan.PreviousState}->{castleQuestPlan.CurrentState}, removedNpcs={castleQuestPlan.RemovedNpcs.Count}.");
            }

            if (world.TryProcessMapItemMinute(now, out var mapItemMinutePlan) && mapItemMinutePlan is { ClosedItems.Count: > 0 })
            {
                var codec = LegacyFrameCodec.CreateDefault();
                foreach (var mapItem in mapItemMinutePlan.ClosedItems)
                {
                    var closeFrame = new CreateItemConfirmation(
                            checked((ushort)mapItem.PositionX),
                            checked((ushort)mapItem.PositionY),
                            checked((ushort)(mapItem.ItemId + LegacyMapItemStateCodes.WireIdOffset)),
                            mapItem.Item,
                            mapItem.Rotate,
                            checked((byte)mapItem.State),
                            checked((byte)Math.Clamp(mapItem.Height, 0, byte.MaxValue)))
                        .ToFrame(codec, unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
                    foreach (var recipient in world.GetParticipantIdsInMapItemView(mapItem.ItemId + LegacyMapItemStateCodes.WireIdOffset))
                        await world.SendAsync(recipient, closeFrame, cancellationToken);
                }
                Console.WriteLine($"Map item minute timer processed: slot={mapItemMinutePlan.MinuteSlot:yyyy-MM-dd HH:mm}, closedItems={mapItemMinutePlan.ClosedItems.Count}.");
            }

            if (world.TryProcessGroundItemSecond(now, out var groundItemDecayPlan) && groundItemDecayPlan is { RemovedItems.Count: > 0 })
            {
                var codec = LegacyFrameCodec.CreateDefault();
                foreach (var removedItem in groundItemDecayPlan.RemovedItems)
                {
                    var decayFrame = new DecayItemConfirmation(checked((short)(removedItem.Item.ItemId + LegacyWorldItem.WireIdOffset)))
                        .ToFrame(codec, unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), 30_000);
                    foreach (var recipient in removedItem.RecipientConnectionIds)
                        await world.SendAsync(recipient, decayFrame, cancellationToken);
                }
                Console.WriteLine($"Ground item decay timer processed: slot={groundItemDecayPlan.SecondSlot:yyyy-MM-dd HH:mm:ss}, removedItems={groundItemDecayPlan.RemovedItems.Count}.");
            }

            if (world.TryProcessCastleQuestSecond(now, out var castleQuestSecondPlan) && castleQuestSecondPlan is not null)
            {
                if (castleQuestSecondPlan.RemovedNpcs.Count > 0)
                    await BroadcastRemovedNpcsInAreaAsync(world, castleQuestSecondPlan.RemovedNpcs, 2176, 1160, 2300, 1276, cancellationToken);
                Console.WriteLine($"Castle quest second timer processed: slot={castleQuestSecondPlan.SecondSlot:yyyy-MM-dd HH:mm:ss}, time={castleQuestSecondPlan.PreviousTimeRemaining}->{castleQuestSecondPlan.CurrentTimeRemaining}, expired={castleQuestSecondPlan.Expired}, removedNpcs={castleQuestSecondPlan.RemovedNpcs.Count}.");
            }

            if (world.TryProcessPistaEntry(now, out var entryPlan) && entryPlan is not null)
            {
                await BroadcastPistaTeleportsAsync(world, entryPlan.Teleports, cancellationToken);
                await RelayGeneratedNpcGroupAsync(world, entryPlan.SpawnedNpcs, cancellationToken);
                Console.WriteLine($"Pista entry processed: slot={entryPlan.EntrySlot:yyyy-MM-dd HH:mm}, teleports={entryPlan.Teleports.Count}, generatedNpcs={entryPlan.SpawnedNpcs.Count}.");
            }

            if (world.TryProcessPistaExit(now, out var exitPlan) && exitPlan is not null)
            {
                await BroadcastPistaTeleportsAsync(world, exitPlan.Teleports, cancellationToken);
                await BroadcastPistaRemovedNpcsAsync(world, exitPlan.RemovedNpcs, cancellationToken);
                await SendPistaItemDropsAsync(world, exitPlan.ItemDrops, cancellationToken);
                Console.WriteLine($"Pista exit processed: slot={exitPlan.ExitSlot:yyyy-MM-dd HH:mm}, teleports={exitPlan.Teleports.Count}, removedNpcs={exitPlan.RemovedNpcs.Count}.");
            }
        }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
    catch (IOException)
    {
        // The connection cleanup path removes stale participants; the timer remains local-only.
    }
    catch (ObjectDisposedException)
    {
        // The connection cleanup path removes stale participants; the timer remains local-only.
    }
}

static async Task BroadcastPistaTeleportsAsync(WorldHub world, IReadOnlyList<LegacyPistaTeleport> teleports, CancellationToken cancellationToken)
{
    var codec = LegacyFrameCodec.CreateDefault();
    foreach (var teleport in teleports)
    {
        var frame = new ActionRequest(
            teleport.FromX,
            teleport.FromY,
            Effect: 1,
            Speed: 0,
            Route: new byte[24],
            teleport.ToX,
            teleport.ToY)
            .ToFrame(codec, unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)teleport.ConnectionId);
        await world.SendAsync(teleport.ConnectionId, frame, cancellationToken);
        await world.BroadcastAsync(teleport.ConnectionId, frame, cancellationToken);
    }
}

static async Task BroadcastGlobalNoticesAsync(WorldHub world, IReadOnlyList<LegacyGlobalNotice> notices, CancellationToken cancellationToken)
{
    var codec = LegacyFrameCodec.CreateDefault();
    foreach (var notice in notices)
    {
        var frame = new MessagePanelConfirmation(notice.Message)
            .ToFrame(codec, unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
        await world.BroadcastAsync(-1, frame, cancellationToken);
    }
}

static async Task BroadcastAutoDoubleTransitionAsync(WorldHub world, string message, CancellationToken cancellationToken)
{
    var codec = LegacyFrameCodec.CreateDefault();
    var clientTick = unchecked((uint)Environment.TickCount64);
    var keywordIndex = (byte)RandomNumberGenerator.GetInt32(256);
    var messagePanel = new MessagePanelConfirmation(message).ToFrame(codec, clientTick, keywordIndex);
    var magicTrumpet = new MagicTrumpetConfirmation($"[Event:]> {message}")
        .ToFrame(codec, clientTick, keywordIndex);

    await world.BroadcastAsync(-1, messagePanel, cancellationToken);
    await world.BroadcastAsync(-1, magicTrumpet, cancellationToken);
}

static async Task BroadcastAreaNoticesAsync(WorldHub world, IReadOnlyList<LegacyAreaNotice> notices, CancellationToken cancellationToken)
{
    var codec = LegacyFrameCodec.CreateDefault();
    foreach (var notice in notices)
    {
        var frame = new MessagePanelConfirmation(notice.Message)
            .ToFrame(codec, unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
        foreach (var connectionId in world.GetParticipantIdsInArea(notice.X1, notice.Y1, notice.X2, notice.Y2))
            await world.SendAsync(connectionId, frame, cancellationToken);
    }
}

static async Task SendPrivateNoticesAsync(WorldHub world, IReadOnlyList<LegacyPrivateNotice> notices, CancellationToken cancellationToken)
{
    var codec = LegacyFrameCodec.CreateDefault();
    foreach (var notice in notices)
    {
        var frame = new MessagePanelConfirmation(notice.Message)
            .ToFrame(codec, unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
        await world.SendAsync(notice.ConnectionId, frame, cancellationToken);
    }
}

/// <summary>Sends the 7.69 <c>SendAddParty</c> frames (MSG_CNFAddParty, ID ESCENE_FIELD) to their users.</summary>
static async Task SendPartyAddNoticesAsync(WorldHub world, IReadOnlyList<LegacyPartyAddNotice> notices, CancellationToken cancellationToken)
{
    var codec = LegacyFrameCodec.CreateDefault();
    foreach (var partyAdd in notices)
    {
        var frame = new PartyAddConfirmation(
            unchecked((short)partyAdd.LeaderField),
            (short)Math.Clamp(partyAdd.Level, short.MinValue, short.MaxValue),
            (short)Math.Clamp(partyAdd.MaxHp, short.MinValue, short.MaxValue),
            (short)Math.Clamp(partyAdd.Hp, short.MinValue, short.MaxValue),
            unchecked((short)partyAdd.MemberConnectionId),
            partyAdd.MobName,
            unchecked((short)52428)).ToFrame(codec, unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), 30000);
        await world.SendAsync(partyAdd.RecipientConnectionId, frame, cancellationToken);
    }
}

static async Task BroadcastRemovedNpcsInAreaAsync(WorldHub world, IReadOnlyList<LegacyWorldNpc> npcs, int x1, int y1, int x2, int y2, CancellationToken cancellationToken)
{
    var recipients = world.GetParticipantIdsInArea(x1, y1, x2, y2);
    foreach (var npc in npcs)
    {
        var frame = new RemoveMobConfirmation((ushort)npc.ConnectionId)
            .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
        foreach (var connectionId in recipients)
            await world.SendAsync(connectionId, frame, cancellationToken);
    }
}

static async Task RunAutoDoubleScheduleLoopAsync(WorldHub world, LegacyAutoDoubleConfiguration configuration, CancellationToken cancellationToken)
{
    var schedule = new LegacyAutoDoubleSchedule(configuration);
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
    try
    {
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var now = DateTime.Now;
            var transition = schedule.Tick(now);
            if (transition is null)
                continue;

            world.SetDoubleModeMultiplier(transition.DoubleModeMultiplier);
            try
            {
                await BroadcastAutoDoubleTransitionAsync(world, transition.Message, cancellationToken);
            }
            catch (IOException error)
            {
                Console.Error.WriteLine($"AutoDouble notice broadcast failed: {error.GetType().Name}.");
            }
            catch (ObjectDisposedException error)
            {
                Console.Error.WriteLine($"AutoDouble notice broadcast failed: {error.GetType().Name}.");
            }

            Console.WriteLine($"AutoDouble transition: active={transition.Active}, multiplier={transition.DoubleModeMultiplier}, localTime={now:yyyy-MM-dd HH:mm:ss}.");
        }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
}

static async Task RunBattleRoyaleScheduleLoopAsync(WorldHub world, LoginSessionRegistry sessions, LegacyAccountOperationGate accountOperationGate, LegacyBattleRoyaleConfiguration configuration, CancellationToken cancellationToken)
{
    var controller = new LegacyBattleRoyaleScheduleController(configuration, world);
    var environmentalTimerStartedAt = Stopwatch.GetTimestamp();
    long lastEnvironmentalWindow = 0;
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
    try
    {
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var now = DateTime.Now;
            var transition = controller.Tick(now);

            var elapsedSeconds = Stopwatch.GetElapsedTime(environmentalTimerStartedAt).TotalSeconds;
            var environmentalWindow = (long)(elapsedSeconds / LegacyBattleRoyaleEnvironmentalSchedule.IntervalSeconds);
            if (environmentalWindow > lastEnvironmentalWindow)
            {
                lastEnvironmentalWindow = environmentalWindow;
                var combatState = world.GetCombatWorldState();
                var environmentalStep = LegacyBattleRoyaleEnvironmentalSchedule.TryCreate(
                    combatState.BattleRoyaleState,
                    combatState.BattleRoyaleItem,
                    now.Minute % 20,
                    environmentalWindow * LegacyBattleRoyaleEnvironmentalSchedule.IntervalSeconds);
                if (environmentalStep is not null)
                {
                    var environmentalPlan = LegacyBattleRoyaleEnvironmentalRelay.TryCreate(
                        world,
                        environmentalStep,
                        LegacyFrameCodec.CreateDefault(),
                        unchecked((uint)Environment.TickCount64),
                        (byte)RandomNumberGenerator.GetInt32(256));
                    if (environmentalPlan is null)
                    {
                        Console.Error.WriteLine($"Battle Royale environmental plan failed: phase={environmentalStep.MinutePhase}.");
                    }
                    else
                    {
                        var damageDeliveries = environmentalPlan.Packets
                            .Where(static packet => packet.Kind == LegacyBattleRoyaleEnvironmentalPacketKind.PlayerDamage)
                            .Count();
                        try
                        {
                            await LegacyBattleRoyaleEnvironmentalRelay.SendAsync(world, environmentalPlan, cancellationToken);
                            Console.WriteLine($"Battle Royale environmental tick: phase={environmentalStep.MinutePhase}, damage-deliveries={damageDeliveries}, frames={environmentalPlan.Packets.Count}, localTime={now:yyyy-MM-dd HH:mm:ss}.");
                        }
                        catch (IOException error)
                        {
                            Console.Error.WriteLine($"Battle Royale environmental relay failed: {error.GetType().Name}; remaining frames were suppressed.");
                        }
                        catch (ObjectDisposedException error)
                        {
                            Console.Error.WriteLine($"Battle Royale environmental relay failed: {error.GetType().Name}; remaining frames were suppressed.");
                        }
                    }
                }
            }

            if (transition is null)
                continue;

            var rewardSpawned = false;
            var noticeSent = false;
            var areaClearActors = 0;
            var gridRefreshActors = 0;
            var gridRefreshFailedRecipients = 0;
            var doorPackets = await SendBattleRoyaleDoorCallsAsync(
                world,
                accountOperationGate,
                transition,
                LegacyBattleRoyaleDoorCallTiming.BeforeNotice,
                cancellationToken);
            if ((transition.PreviousState, transition.State) is (1, 2) or (3, 0))
                controller.PublishDoorTransition(transition);
            if (transition.PreviousState == 2 && transition.State == 3)
            {
                var codec = LegacyFrameCodec.CreateDefault();
                var rewardPlan = LegacyBattleRoyaleRewardRelay.TryCreate(
                    world,
                    transition,
                    RandomNumberGenerator.GetInt32(4),
                    codec,
                    unchecked((uint)Environment.TickCount64),
                    (byte)RandomNumberGenerator.GetInt32(256));
                controller.PublishRewardTransition(transition);
                rewardSpawned = rewardPlan is not null;

                if (rewardPlan is null)
                {
                    Console.Error.WriteLine($"Battle Royale reward spawn failed: item={transition.BRItem}; the reward notice was suppressed.");
                }
                else
                {
                    try
                    {
                        await LegacyBattleRoyaleRewardRelay.SendAsync(world, rewardPlan, cancellationToken);
                        noticeSent = transition.AreaNotice is not null;
                    }
                    catch (IOException error)
                    {
                        Console.Error.WriteLine($"Battle Royale reward relay failed: {error.GetType().Name}; remaining frames were suppressed.");
                    }
                    catch (ObjectDisposedException error)
                    {
                        Console.Error.WriteLine($"Battle Royale reward relay failed: {error.GetType().Name}; remaining frames were suppressed.");
                    }
                }
            }
            else if (transition.AreaNotice is { } areaNotice)
            {
                try
                {
                    await BroadcastAreaNoticesAsync(world, [areaNotice], cancellationToken);
                    noticeSent = true;
                }
                catch (IOException error)
                {
                    Console.Error.WriteLine($"Battle Royale notice broadcast failed: {error.GetType().Name}.");
                }
                catch (ObjectDisposedException error)
                {
                    Console.Error.WriteLine($"Battle Royale notice broadcast failed: {error.GetType().Name}.");
                }
            }

            if (transition.AreaClearLevels is not null)
            {
                var codec = LegacyFrameCodec.CreateDefault();
                var areaClearPlan = LegacyBattleRoyaleAreaClearRelay.TryCreate(
                    world,
                    transition,
                    codec,
                    unchecked((uint)Environment.TickCount64),
                    (byte)RandomNumberGenerator.GetInt32(256));
                if (areaClearPlan is null)
                {
                    Console.Error.WriteLine($"Battle Royale area-clear plan failed: transition={transition.PreviousState}->{transition.State}.");
                }
                else
                {
                    areaClearActors = areaClearPlan.Actors.Count;
                    try
                    {
                        await LegacyBattleRoyaleAreaClearRelay.SendAsync(world, sessions, areaClearPlan, cancellationToken);
                    }
                    catch (IOException error)
                    {
                        Console.Error.WriteLine($"Battle Royale area-clear relay failed: {error.GetType().Name}; remaining frames were suppressed.");
                    }
                    catch (ObjectDisposedException error)
                    {
                        Console.Error.WriteLine($"Battle Royale area-clear relay failed: {error.GetType().Name}; remaining frames were suppressed.");
                    }
                }
            }

            doorPackets += await SendBattleRoyaleDoorCallsAsync(
                world,
                accountOperationGate,
                transition,
                LegacyBattleRoyaleDoorCallTiming.AfterAreaClear,
                cancellationToken);

            if (transition.RefreshGridAndCleanupParties)
            {
                var codec = LegacyFrameCodec.CreateDefault();
                var refreshPlan = LegacyBattleRoyaleGridRefreshRelay.TryCreate(
                    world,
                    transition,
                    codec,
                    unchecked((uint)Environment.TickCount64),
                    (byte)RandomNumberGenerator.GetInt32(256));
                if (refreshPlan is null)
                {
                    Console.Error.WriteLine($"Battle Royale grid-refresh plan failed: transition={transition.PreviousState}->{transition.State}.");
                }
                else
                {
                    gridRefreshActors = refreshPlan.Actors.Count;
                    try
                    {
                        var sendResult = await LegacyBattleRoyaleGridRefreshRelay.SendAsync(world, refreshPlan, cancellationToken, accountOperationGate);
                        gridRefreshFailedRecipients = sendResult.FailedRecipientIds.Count;
                        if (gridRefreshFailedRecipients > 0)
                            Console.Error.WriteLine($"Battle Royale grid-refresh skipped disconnected recipients after send errors: ids={string.Join(',', sendResult.FailedRecipientIds)}.");
                    }
                    catch (IOException error)
                    {
                        Console.Error.WriteLine($"Battle Royale grid-refresh relay failed: {error.GetType().Name}; remaining frames were suppressed.");
                    }
                    catch (ObjectDisposedException error)
                    {
                        Console.Error.WriteLine($"Battle Royale grid-refresh relay failed: {error.GetType().Name}; remaining frames were suppressed.");
                    }
                }
            }

            Console.WriteLine($"Battle Royale state transition: {transition.PreviousState}->{transition.State}, item={transition.BRItem}, reward-spawned={rewardSpawned}, notice={noticeSent}, door-packets={doorPackets}, area-clear-recalled={areaClearActors}, grid-refresh={gridRefreshActors}, grid-refresh-send-failures={gridRefreshFailedRecipients}, localTime={now:yyyy-MM-dd HH:mm:ss}.");
        }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
}

static async Task<int> SendBattleRoyaleDoorCallsAsync(
    WorldHub world,
    LegacyAccountOperationGate accountOperationGate,
    LegacyBattleRoyaleTransition transition,
    LegacyBattleRoyaleDoorCallTiming timing,
    CancellationToken cancellationToken)
{
    if (!transition.DoorCalls.Any(call => call.Timing == timing))
        return 0;

    var codec = LegacyFrameCodec.CreateDefault();
    LegacyAccountOperationLease? accountLease = null;
    while (true)
    {
        var accountNames = world.GetAllParticipantAccountNames()
            .Where(accountName => !accountOperationGate.IsRecoveryRequired(accountName))
            .ToArray();
        if (accountNames.Length == 0)
            break;
        accountLease = await accountOperationGate.AcquireAsync(accountNames, cancellationToken);
        var currentAccounts = world.GetAllParticipantAccountNames()
            .Where(accountName => !accountOperationGate.IsRecoveryRequired(accountName))
            .ToArray();
        if (currentAccounts.All(accountName => accountLease.Covers(accountOperationGate, accountName)))
            break;
        await accountLease.DisposeAsync();
        accountLease = null;
    }

    LegacyBattleRoyaleDoorPlan? plan;
    try
    {
        plan = LegacyBattleRoyaleDoorRelay.TryCreate(
            world,
            transition,
            timing,
            codec,
            unchecked((uint)Environment.TickCount64),
            (byte)RandomNumberGenerator.GetInt32(256));
    }
    finally
    {
        if (accountLease is not null)
            await accountLease.DisposeAsync();
    }
    if (plan is null)
    {
        Console.Error.WriteLine($"Battle Royale door relay plan failed: transition={transition.PreviousState}->{transition.State}, timing={timing}.");
        return 0;
    }

    try
    {
        await LegacyBattleRoyaleDoorRelay.SendAsync(world, plan, cancellationToken);
        return plan.Packets.Count;
    }
    catch (IOException error)
    {
        Console.Error.WriteLine($"Battle Royale door relay failed: transition={transition.PreviousState}->{transition.State}, timing={timing}, error={error.GetType().Name}.");
    }
    catch (ObjectDisposedException error)
    {
        Console.Error.WriteLine($"Battle Royale door relay failed: transition={transition.PreviousState}->{transition.State}, timing={timing}, error={error.GetType().Name}.");
    }

    return 0;
}

static async ValueTask SaveCharacterOnDepartureAsync(ICharacterStore? accounts, LegacyCharacterCheckpointRuntime? characterCheckpointRuntime, WorldHub world, LoginSession session, bool disconnect, CancellationToken cancellationToken, LegacyAccountOperationLease? accountLease = null)
{
    if (characterCheckpointRuntime is not null)
    {
        await characterCheckpointRuntime.SaveOnDepartureAsync(session, disconnect, cancellationToken, accountLease);
        return;
    }
    if (accounts is null || session.CharacterSlot < 0 || session.AccountName is null) return;
    var label = disconnect ? "disconnect " : string.Empty;
    if (world.TryGetCharacterSnapshot(session.ConnectionId, out var mob, out var x, out var y, out var extra, out var affect, out var equipment) && mob is not null)
    {
        var result = await accounts.TrySaveCharacterStateAsync(
            session.AccountName,
            session.CharacterSlot,
            mob,
            x,
            y,
            cancellationToken,
            new ReadOnlyMemory<byte>(extra ?? Array.Empty<byte>()),
            new ReadOnlyMemory<byte>(affect ?? Array.Empty<byte>()));
        Console.WriteLine($"Character {label}state save: account={session.AccountName}, slot={session.CharacterSlot}, position=({x},{y}), result={result}.");
        if (accounts is IClientEquipmentStateStore equipmentStore && equipment is not null)
        {
            var equipmentResult = await equipmentStore.TrySaveClientEquipmentAsync(session.AccountName, session.CharacterSlot, equipment, cancellationToken);
            Console.WriteLine($"Client 7.69 equipment {label}save: account={session.AccountName}, slot={session.CharacterSlot}, result={equipmentResult}.");
        }
    }
    else
    {
        var result = await accounts.TrySaveCharacterPositionAsync(session.AccountName, session.CharacterSlot, session.PositionX, session.PositionY, cancellationToken);
        Console.WriteLine($"Character {label}position fallback save: account={session.AccountName}, slot={session.CharacterSlot}, position=({session.PositionX},{session.PositionY}), result={result}.");
    }
}

static async Task BroadcastAutoTradeDepartureAsync(WorldHub world, LegacyAutoTradeDeparturePlan plan, CancellationToken cancellationToken)
{
    foreach (var framePlan in plan.Frames)
        foreach (var recipient in framePlan.RecipientIds)
            await world.SendAsync(recipient, framePlan.Frame, cancellationToken);
}

static async Task BroadcastClosedAutoTradeRemovalAsync(
    WorldHub world,
    LegacyAutoTradeReconnectClosure closedListing,
    CancellationToken cancellationToken)
{
    if (!LegacyAutoTradeRemovalRelay.TryBuild(
            closedListing.Snapshot,
            LegacyFrameCodec.CreateDefault(),
            unchecked((uint)Environment.TickCount64),
            (byte)RandomNumberGenerator.GetInt32(256),
            out var plan) ||
        plan is null)
        return;

    foreach (var recipient in closedListing.RecipientIds.Where(id => id != plan.ShopConnectionId))
        await world.SendAsync(recipient, plan.Frame, cancellationToken);
}

static async Task RemoveAutoTradeStateAsync(
    ILegacyAutoTradeStateStore? store,
    string? accountName,
    int characterSlot,
    LegacyAutoTradeSnapshot? snapshot,
    CancellationToken cancellationToken)
{
    if (store is null || snapshot is null || accountName is null || characterSlot < 0)
        return;

    try
    {
        var result = await store.RemoveAsync(accountName, characterSlot, cancellationToken);
        if (result is not LegacyAutoTradeStateResult.Removed and not LegacyAutoTradeStateResult.NotFound)
            Console.WriteLine($"Autotrade state removal returned {result}: account={accountName}, slot={characterSlot}.");
    }
    catch (Exception error)
    {
        Console.WriteLine($"Autotrade state removal failed: account={accountName}, slot={characterSlot}, error={error.GetType().Name}: {error.Message}.");
    }
}

static Task RelayGeneratedNpcGroupAsync(WorldHub world, IReadOnlyList<LegacyWorldNpc> npcs, CancellationToken cancellationToken)
    => LegacyNpcGenerationRelay.RelayGroupAsync(
        world,
        npcs,
        npc => BuildClientV769CreateMobFrame(
            (ushort)npc.ConnectionId,
            npc.PositionX,
            npc.PositionY,
            npc.MobSnapshot,
            npc.AffectSnapshot,
            npc: true,
            battleRoyaleState: world.GetCombatWorldState().BattleRoyaleState),
        cancellationToken);

static async Task BroadcastPistaRemovedNpcsAsync(WorldHub world, IReadOnlyList<LegacyWorldNpc> npcs, CancellationToken cancellationToken)
{
    foreach (var npc in npcs)
    {
        var frame = new RemoveMobConfirmation((ushort)npc.ConnectionId)
            .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
        await world.BroadcastAsync(-1, frame, cancellationToken);
    }
}

static async Task SendPistaItemDropsAsync(WorldHub world, IReadOnlyList<LegacyItemDrop>? itemDrops, CancellationToken cancellationToken)
{
    if (itemDrops is null or { Count: 0 })
        return;

    var codec = LegacyFrameCodec.CreateDefault();
    foreach (var drop in itemDrops)
    {
        var frame = new SendItemConfirmation(1, checked((short)drop.InventorySlot), drop.Item)
            .ToFrame(codec, unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)drop.ConnectionId);
        await world.SendAsync(drop.ConnectionId, frame, cancellationToken);
    }
}

static IReadOnlyList<LegacyWorldNpc> SpawnReferenceCityPerzens(WorldHub world)
{
    // These are the five Perzen generators present in the local Reference759
    // NPCGener.txt: Erion, Azran, Noatum, and two positions in Armia.
    var spawned = new List<LegacyWorldNpc>(5);
    foreach (var generateIndex in new[] { 3427, 3428, 3429, 3430, 4866 })
        if (world.TrySpawnGeneratedNpcGroup(generateIndex, out var group) && group is not null)
            spawned.AddRange(group);
    return spawned;
}

static IReadOnlyList<LegacyWorldNpc> SpawnReferenceDonateNpc(WorldHub world)
{
    // Explicit opt-in smoke path. The selected NPCGener definition controls
    // the position; production startup does not spawn this extra NPC.
    return world.TrySpawnGeneratedNpcGroup(4639, out var group) && group is not null
        ? group
        : [];
}

static async Task BroadcastSummonDespawnsAsync(WorldHub world, IReadOnlyList<int> summonIds, CancellationToken cancellationToken)
{
    foreach (var summonId in summonIds)
    {
        var frame = new RemoveMobConfirmation((ushort)summonId)
            .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
        await world.BroadcastAsync(-1, frame, cancellationToken);
    }
}

static async Task SendResurrectionRefreshAsync(
    WorldHub world,
    int connectionId,
    byte[] mob,
    int itemSlot,
    LegacyItem item,
    bool includeScoreAndResources,
    CancellationToken cancellationToken,
    IReadOnlyList<int>? capturedViewRecipients = null)
{
    var codec = LegacyFrameCodec.CreateDefault();
    var clientTick = unchecked((uint)Environment.TickCount64);
    var keywordIndex = (byte)RandomNumberGenerator.GetInt32(256);
    var targetAvailable = true;

    async Task SendToTargetAsync(ReadOnlyMemory<byte> frame)
    {
        if (!targetAvailable)
            return;

        if (capturedViewRecipients is null)
        {
            await world.SendAsync(connectionId, frame, cancellationToken);
            return;
        }

        targetAvailable = await LegacyMobAiRelay.TrySendToViewerAsync(
            world, connectionId, frame, cancellationToken);
    }

    var itemFrame = new SendItemConfirmation(1, checked((short)itemSlot), item)
        .ToFrame(codec, clientTick, keywordIndex, (ushort)connectionId);
    await SendToTargetAsync(itemFrame);

    if (targetAvailable && includeScoreAndResources && world.TryGetResourceState(connectionId, out var resourceState, out var requestedHp, out var requestedMp) && resourceState is not null)
    {
        var scoreFrame = BuildClientV769UpdateScoreWithoutAffectFrame(mob, (ushort)connectionId, clientTick, keywordIndex);
        await SendToTargetAsync(scoreFrame);
        var hpMpFrame = new SetHpMpConfirmation(resourceState.CurrentScore.Hp, resourceState.CurrentMana, requestedHp, requestedMp)
            .ToFrame(codec, clientTick, keywordIndex, (ushort)connectionId);
        await SendToTargetAsync(hpMpFrame);
    }

    if (world.TryBuildCreateMobFrame(connectionId, codec, clientTick, keywordIndex, out var createFrame) && createFrame is not null)
    {
        await SendToTargetAsync(createFrame);
        if (capturedViewRecipients is null)
        {
            await world.BroadcastAsync(connectionId, createFrame, cancellationToken);
        }
        else
        {
            foreach (var recipient in capturedViewRecipients)
            {
                if (recipient == connectionId)
                    continue;
                await LegacyMobAiRelay.TrySendToViewerAsync(world, recipient, createFrame, cancellationToken);
            }
        }
    }
}

static LegacySkillDataTable? LoadSkillData(string? path)
{
    if (path is null) return null;
    var fullPath = Path.GetFullPath(path);
    if (!File.Exists(fullPath)) throw new FileNotFoundException("SkillData.csv was not found.", fullPath);
    using var reader = File.OpenText(fullPath);
    return LegacySkillDataTable.Load(reader);
}

static LegacyItemDataTable? LoadItemData(string? path)
{
    if (path is null) return null;
    var fullPath = Path.GetFullPath(path);
    if (!File.Exists(fullPath)) throw new FileNotFoundException("ItemList.bin was not found.", fullPath);
    return LegacyItemDataTable.Load(fullPath);
}

static bool TryResolveSkillCombatInputs(ReadOnlySpan<byte> mob, LegacyItemDataTable itemData, out int magic, out int weaponDamage)
{
    magic = 0;
    weaponDamage = 0;
    if (mob.Length < LegacyAccountSnapshot.CharacterStride) return false;

    var state = LegacyMobCombatState.Read(mob);
    var firstWeapon = LegacyItem.Read(mob.Slice(LegacyAccountSnapshot.MobEquipmentOffset + (6 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
    var secondWeapon = LegacyItem.Read(mob.Slice(LegacyAccountSnapshot.MobEquipmentOffset + (7 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
    magic = state.Magic;
    weaponDamage = LegacySkillCombatMath.GetWeaponDamage(state, itemData, firstWeapon, secondWeapon);
    return true;
}

sealed record ServerOptions(string BindAddress, int Port, string? AccountRoot, string? AccountDatabaseConfigPath, string? AccountReviewRoot, string? SkillDataPath, string? ItemDataPath, string? InitItemPath, string? HeightMapPath, string? AttributeMapPath, string? GuildDataPath, string? SummonRoot, string? NpcGenerationPath, string? NpcRoot, string? CastleQuestPath, string? DonateShopCatalogPath, string? DonateDatabaseConfigPath, bool SpawnCityNpcs, bool SpawnDonateNpc, LegacyMapCollisionMode MapCollisionMode, LegacyServerMode ServerMode, string? AutoTradeStatePath, string? StatusFilePath, int? StatusSlot, int DoubleModeMultiplier = 1, string? AutoDoubleConfigPath = null, string? BattleRoyaleConfigPath = null, string? GuildNamesPath = null, int ServerGroup = 0, bool DungeonEventEnabled = true, int? TinyChanceBaseOverride = null, string? CompRatePath = null)
{
    public string WorldKey => new LegacyServerModePolicy(ServerMode).WorldKey;
    public int TinyChanceBase => LegacyTinyChanceConfiguration.ResolveBase(TinyChanceBaseOverride, null);

    public static ServerOptions Parse(string[] args)
    {
        if (args.Length == 1 && int.TryParse(args[0], out var legacyPort)) return new("127.0.0.1", legacyPort, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, false, false, LegacyMapCollisionMode.LegacyCompatible, LegacyServerMode.Up, null, null, null);

        var port = 8281; // GAME_PORT — matches Basedef.h and WYD.EXE client expectation
        var bindAddress = "127.0.0.1";
        string? accountRoot = null;
        string? accountDatabaseConfigPath = null;
        string? accountReviewRoot = null;
        string? skillDataPath = null;
        string? itemDataPath = null;
        string? initItemPath = null;
        string? heightMapPath = null;
        string? attributeMapPath = null;
        string? guildDataPath = null;
        string? guildNamesPath = null;
        var serverGroup = 0;
        string? summonRoot = null;
        string? npcGenerationPath = null;
        string? npcRoot = null;
        string? castleQuestPath = null;
        string? donateShopCatalogPath = null;
        string? donateDatabaseConfigPath = null;
        var spawnCityNpcs = false;
        var spawnDonateNpc = false;
        var mapCollisionMode = LegacyMapCollisionMode.LegacyCompatible;
        var serverMode = LegacyServerMode.Up;
        string? autoTradeStatePath = null;
        string? statusFilePath = null;
        int? statusSlot = null;
        var doubleModeMultiplier = 1;
        string? autoDoubleConfigPath = null;
        string? battleRoyaleConfigPath = null;
        var dungeonEventEnabled = true;
        int? tinyChanceBaseOverride = null;
        string? compRatePath = null;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--bind" when index + 1 < args.Length:
                    bindAddress = args[++index];
                    break;
                case "--port" when index + 1 < args.Length && int.TryParse(args[++index], out var parsedPort):
                    port = parsedPort;
                    break;
                case "--accounts-root" when index + 1 < args.Length:
                    accountRoot = Path.GetFullPath(args[++index]);
                    break;
                case "--mariadb-account-config" when index + 1 < args.Length:
                    accountDatabaseConfigPath = Path.GetFullPath(args[++index]);
                    break;
                case "--account-review-root" when index + 1 < args.Length:
                    accountReviewRoot = Path.GetFullPath(args[++index]);
                    break;
                case "--skill-data" when index + 1 < args.Length:
                    skillDataPath = Path.GetFullPath(args[++index]);
                    break;
                case "--item-data" when index + 1 < args.Length:
                    itemDataPath = Path.GetFullPath(args[++index]);
                    break;
                case "--init-item" when index + 1 < args.Length:
                    initItemPath = Path.GetFullPath(args[++index]);
                    break;
                case "--heightmap" when index + 1 < args.Length:
                    heightMapPath = Path.GetFullPath(args[++index]);
                    break;
                case "--attribute-map" when index + 1 < args.Length:
                    attributeMapPath = Path.GetFullPath(args[++index]);
                    break;
                case "--guild-data" when index + 1 < args.Length:
                    guildDataPath = Path.GetFullPath(args[++index]);
                    break;
                case "--guild-names" when index + 1 < args.Length:
                    guildNamesPath = Path.GetFullPath(args[++index]);
                    break;
                case "--server-group" when index + 1 < args.Length && int.TryParse(args[++index], out var parsedServerGroup):
                    serverGroup = parsedServerGroup;
                    break;
                case "--base-summon" when index + 1 < args.Length:
                    summonRoot = Path.GetFullPath(args[++index]);
                    break;
                case "--npc-generation" when index + 1 < args.Length:
                    npcGenerationPath = Path.GetFullPath(args[++index]);
                    break;
                case "--npc-root" when index + 1 < args.Length:
                    npcRoot = Path.GetFullPath(args[++index]);
                    break;
                case "--castle-quest" when index + 1 < args.Length:
                    castleQuestPath = Path.GetFullPath(args[++index]);
                    break;
                case "--donate-catalog" when index + 1 < args.Length:
                    donateShopCatalogPath = Path.GetFullPath(args[++index]);
                    break;
                case "--donate-db-config" when index + 1 < args.Length:
                    donateDatabaseConfigPath = Path.GetFullPath(args[++index]);
                    break;
                case "--city-npcs":
                    spawnCityNpcs = true;
                    break;
                case "--donate-npc":
                    spawnDonateNpc = true;
                    break;
                case "--dungeon-event" when index + 1 < args.Length:
                    if (!bool.TryParse(args[++index], out dungeonEventEnabled))
                        throw new ArgumentException("--dungeon-event must be 'true' or 'false'.");
                    break;
                case "--tiny-chance-base" when index + 1 < args.Length:
                    if (!int.TryParse(args[++index], out var parsedTinyChanceBase) || parsedTinyChanceBase is < 0 or > 100)
                        throw new ArgumentException("--tiny-chance-base must be between 0 and 100.");
                    tinyChanceBaseOverride = parsedTinyChanceBase;
                    break;
                case "--comp-rate" when index + 1 < args.Length:
                    compRatePath = Path.GetFullPath(args[++index]);
                    break;
                case "--strict-map-collision":
                    mapCollisionMode = LegacyMapCollisionMode.CandidateAware;
                    break;
                case "--autotrade-state" when index + 1 < args.Length:
                    autoTradeStatePath = Path.GetFullPath(args[++index]);
                    break;
                case "--status-file" when index + 1 < args.Length:
                    statusFilePath = Path.GetFullPath(args[++index]);
                    break;
                case "--status-slot" when index + 1 < args.Length && int.TryParse(args[++index], out var parsedStatusSlot):
                    statusSlot = parsedStatusSlot;
                    break;
                case "--double-mode" when index + 1 < args.Length && int.TryParse(args[++index], out var parsedDoubleModeMultiplier):
                    doubleModeMultiplier = parsedDoubleModeMultiplier;
                    break;
                case "--auto-double-config" when index + 1 < args.Length:
                    autoDoubleConfigPath = Path.GetFullPath(args[++index]);
                    break;
                case "--battle-royale-config" when index + 1 < args.Length:
                    battleRoyaleConfigPath = Path.GetFullPath(args[++index]);
                    break;
                case "--mode" when index + 1 < args.Length:
                    if (!Enum.TryParse(args[++index], ignoreCase: true, out serverMode))
                        throw new ArgumentException("--mode must be 'up' or 'pvp'.");
                    break;
                case "--world" when index + 1 < args.Length:
                    if (!LegacyServerModePolicy.TryParseWorldKey(args[++index], out serverMode))
                        throw new ArgumentException("--world must be 'UP' or 'PVP'.");
                    break;
                default:
                    throw new ArgumentException("Usage: WydCdk.Server [--bind <ip-address>] [--port <port>] [--world <UP|PVP>] [--mode <up|pvp>] [--accounts-root <legacy-account-directory>] [--mariadb-account-config <mariadb-config.json>] [--account-review-root <persistent-directory>] [--skill-data <SkillData.csv>] [--item-data <ItemList.bin>] [--dungeon-event <true|false>] [--comp-rate <CompRate.txt>] [--tiny-chance-base <0..100>] [--init-item <InitItem.bin>] [--heightmap <heightmap.dat> --attribute-map <AttributeMap.dat>] [--guild-data <Guild.txt>] [--guild-names <Guilds.txt>] [--server-group <0..9>] [--base-summon <BaseSummon-directory>] [--npc-generation <NPCGener.txt> --npc-root <npc-directory>] [--city-npcs] [--donate-npc] [--castle-quest <CastleQuest.txt>] [--donate-catalog <DonateShop.csv>] [--donate-db-config <mariadb-config.json>] [--autotrade-state <path>] [--status-file <servtest.htm> --status-slot <1-9>] [--double-mode <multiplier>] [--auto-double-config <eventAuto.json>] [--battle-royale-config <battle-royale.json>] [--strict-map-collision]");
            }
        }

        if (!IPAddress.TryParse(bindAddress, out _))
            throw new ArgumentException("--bind must be a valid IPv4 or IPv6 address.");
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(args), "Port must be between 1 and 65535.");
        if (serverGroup is < 0 or >= LegacyGuildNameCatalog.ServerGroupCount)
            throw new ArgumentOutOfRangeException(nameof(args), "--server-group must be between 0 and 9.");
        if ((heightMapPath is null) != (attributeMapPath is null))
            throw new ArgumentException("--heightmap and --attribute-map must be provided together.");
        if ((npcGenerationPath is null) != (npcRoot is null))
            throw new ArgumentException("--npc-generation and --npc-root must be provided together.");
        if (spawnCityNpcs && npcGenerationPath is null)
            throw new ArgumentException("--city-npcs requires --npc-generation and --npc-root.");
        if (spawnDonateNpc && npcGenerationPath is null)
            throw new ArgumentException("--donate-npc requires --npc-generation and --npc-root.");
        if ((statusFilePath is null) != (statusSlot is null))
            throw new ArgumentException("--status-file and --status-slot must be provided together.");
        if ((accountRoot is not null || accountDatabaseConfigPath is not null) && itemDataPath is null)
            throw new ArgumentException("--item-data is required whenever account authentication is enabled.");
        if ((accountRoot is not null || accountDatabaseConfigPath is not null) && accountReviewRoot is null)
            throw new ArgumentException("--account-review-root is required whenever account authentication is enabled.");
        if (statusSlot is not null && (statusSlot < 1 || statusSlot >= ServerStatusFilePublisher.SlotCount))
            throw new ArgumentOutOfRangeException(nameof(args), "--status-slot must be between 1 and 9.");
        return new(bindAddress, port, accountRoot, accountDatabaseConfigPath, accountReviewRoot, skillDataPath, itemDataPath, initItemPath, heightMapPath, attributeMapPath, guildDataPath, summonRoot, npcGenerationPath, npcRoot, castleQuestPath, donateShopCatalogPath, donateDatabaseConfigPath, spawnCityNpcs, spawnDonateNpc, mapCollisionMode, serverMode, autoTradeStatePath, statusFilePath, statusSlot, doubleModeMultiplier, autoDoubleConfigPath, battleRoyaleConfigPath, guildNamesPath, serverGroup, dungeonEventEnabled, tinyChanceBaseOverride, compRatePath);
    }
}

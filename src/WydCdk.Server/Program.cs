using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

var options = ServerOptions.Parse(args);
using var serverLog = ServerWireLog.TryOpen(options.WorldKey);
if (serverLog is not null)
{
    Console.SetOut(serverLog.CreateTee(Console.Out));
    Console.SetError(serverLog.CreateTee(Console.Error));
}

ICharacterStore? accounts = options.AccountRoot is not null
    ? new LegacyFileAccountStore(options.AccountRoot)
    : options.AccountDatabaseConfigPath is not null
        ? new MariaDbWorldCharacterStore(MariaDbConnectionFactory.FromJson(options.AccountDatabaseConfigPath), options.WorldKey)
        : null;
IAccountStore? accountAuthenticator = accounts as IAccountStore;
if (options.AccountDatabaseConfigPath is not null)
    accountAuthenticator = new MariaDbAccountAuthenticator(MariaDbConnectionFactory.FromJson(options.AccountDatabaseConfigPath));
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
var npcGenerationCatalog = options.NpcGenerationPath is null ? null : LegacyNpcGenerationCatalog.Load(options.NpcGenerationPath, options.NpcRoot!);
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
var autoTradeBook = new LegacyAutoTradeBook();
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
var nextConnectionId = 0;
var summonBattleLoop = RunSummonBattleLoopAsync(world, shutdown.Token);
var pistaScheduleLoop = RunPistaScheduleLoopAsync(world, shutdown.Token);
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
        var connectionId = Interlocked.Increment(ref nextConnectionId);
        _ = InspectConnectionAsync(client, connectionId, accounts, accountAuthenticator, templates, skills, itemData, donateBalances, world, autoTradeBook, autoTradeStateStore, autoTradePurchaseCommitStore, options.DonateDatabaseConfigPath is not null, serverLog, shutdown.Token);
    }
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
}
finally
{
    listener.Stop();
    shutdown.Cancel();
    try { await standardInputShutdown; }
    catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
    try { await statusLoop; }
    catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
    try { await summonBattleLoop; }
    catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
    try { await pistaScheduleLoop; }
    catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
    TryPublishStatus(statusPublisher, -1, "offline");
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

static async Task InspectConnectionAsync(TcpClient client, int connectionId, ICharacterStore? accounts, IAccountStore? accountAuthenticator, LegacyCharacterTemplateStore? templates, LegacySkillDataTable? skills, LegacyItemDataTable? itemData, IDonateBalanceStore? donateBalances, WorldHub world, LegacyAutoTradeBook autoTradeBook, ILegacyAutoTradeStateStore? autoTradeStateStore, ILegacyAutoTradePurchaseCommitStore? autoTradePurchaseCommitStore, bool explicitDonateDatabase, ServerWireLog? serverLog, CancellationToken cancellationToken)
{
    using (client)
    {
        using var stream = new SerializedNetworkStream(client.GetStream());
        var decoder = new LegacyFrameStream(LegacyFrameCodec.CreateDefault());
        var sessions = new LoginSessionRegistry();
        var donateShopRateLimiter = new LegacyDonateShopRateLimiter();
        sessions.Open(connectionId);
        serverLog?.Write($"CONNECTION OPEN connection={connectionId} remote={client.Client.RemoteEndPoint}");
        var buffer = new byte[4096];

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

                        var outcome = await new AccountLoginCoordinator(sessions, accountAuthenticator).HandleAsync(connectionId, login, cancellationToken);
                        if (!outcome.IsSuccess)
                        {
                            var status = outcome.Authentication ?? AccountAuthenticationStatus.InvalidAccountFile;
                            var failure = new MessagePanelConfirmation(AccountLoginFailureNotice.For(status))
                                .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, failure, cancellationToken, "account-login-failure");
                            Console.WriteLine($"Account login rejected: {status}; failure notice sent to client.");
                            return;
                        }

                        var snapshot = accounts is IAccountSnapshotStore snapshotStore
                            ? await snapshotStore.ReadSnapshotAsync(login.AccountName, cancellationToken)
                            : null;
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
                            var data = outcome.Data!;
                            var classMaster = data.MobExtra.Length >= sizeof(short)
                                ? BinaryPrimitives.ReadInt16LittleEndian(data.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset))
                                : (short)LegacyAccountSnapshot.ClassMasterMortal;
                            var loginPosition = world.ResolveCharacterLoginPosition(connectionId, data.Mob, classMaster);
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
                                    var reconnect = await LegacyAutoTradeReconnectCoordinator.CloseAsync(
                                        world,
                                        autoTradeBook,
                                        autoTradeStateStore,
                                        loginSession.AccountName!,
                                        characterLogin.Slot,
                                        cancellationToken);
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

                            var loginResponse = W2ppCharacterLoginV1Adapter.Adapt(
                                    data,
                                    loginPosition.X,
                                    loginPosition.Y,
                                    (ushort)characterLogin.Slot,
                                    (ushort)connectionId,
                                    weather: 0)
                                .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, loginResponse, cancellationToken, $"character-login-confirmation account={loginSession.AccountName} slot={characterLogin.Slot} saved=({data.SavedPositionX},{data.SavedPositionY}) live=({loginPosition.X},{loginPosition.Y})");
                            sessions.CompleteCharacterLogin(connectionId, characterLogin.Slot, loginPosition.X, loginPosition.Y);
                            var spawnFrame = BuildClientV769CreateMobFrame(
                                (ushort)connectionId,
                                loginPosition.X,
                                loginPosition.Y,
                                data.Mob,
                                data.Affect,
                                npc: false,
                                clientEquipment: data.ClientEquipment);
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
                                        npc: true);
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
                                 data.ClientEquipment);
                            world.SetDonateBalance(connectionId, donate);
                            var etcFrame = BuildClientV769UpdateEtcFrame(
                                data.Mob,
                                data.MobExtra,
                                (ushort)connectionId);
                            var scoreFrame = BuildClientV769UpdateScoreFrame(
                                data.Mob,
                                data.Affect,
                                (ushort)connectionId);
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, etcFrame, cancellationToken, "character-update-etc");
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, scoreFrame, cancellationToken, "character-update-score");
                            if (TryBuildClientV769UpdateAffectFrame(
                                    data.Affect,
                                    (ushort)connectionId,
                                    unchecked((uint)Environment.TickCount64),
                                    (byte)RandomNumberGenerator.GetInt32(256),
                                    out var loginAffectFrame))
                                await WriteLoggedFrameAsync(stream, serverLog, connectionId, loginAffectFrame, cancellationToken, "character-update-affect");
                            else
                                Console.WriteLine($"Character login affect relay skipped: account={loginSession.AccountName}, reason=7.69-narrowing");
                            var pkInfoFrame = new PkInfoConfirmation(connectionId, state: 0)
                                .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
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
                                var scoreFrame = BuildClientV769UpdateScoreFrame(
                                    affectConsumableOutcome.MobSnapshot,
                                    affectConsumableOutcome.AffectSnapshot,
                                    (ushort)connectionId,
                                    frame.Header.ClientTick,
                                    (byte)RandomNumberGenerator.GetInt32(256));
                                await stream.WriteAsync(scoreFrame, cancellationToken);
                                if (TryBuildClientV769UpdateAffectFrame(
                                        affectConsumableOutcome.AffectSnapshot,
                                        (ushort)connectionId,
                                        frame.Header.ClientTick,
                                        frame.Header.KeywordIndex,
                                        out var affectFrame))
                                    await stream.WriteAsync(affectFrame, cancellationToken);
                                Console.WriteLine($"Affect consumable applied: account={useItemSession.AccountName}, source={affectConsumableOutcome.SourceSlot}, affect={affectConsumableOutcome.AffectValue}, time={affectConsumableOutcome.AffectTime}.");
                            }
                            else
                                Console.WriteLine($"Affect consumable rejected: account={useItemSession.AccountName}, source={useItem.SourceSlot}, reason={affectConsumableResult}.");
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

                        var coinConsumableResult = world.TryApplyCoinConsumable(connectionId, useItem, out var coinConsumableOutcome);
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

                    if (DeleteItemRequest.TryParse(frame, out var deleteItemRequest) && deleteItemRequest is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var deleteSession) || deleteSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine("Rejected delete-item frame: session is not in USER_PLAY.");
                            continue;
                        }

                        var deleteResult = world.TryDeleteCarryItem(
                            connectionId,
                            deleteItemRequest.Slot,
                            deleteItemRequest.ItemIndex);
                        if (deleteResult == LegacyDeleteItemResult.Accepted)
                            Console.WriteLine($"Carry item deleted: account={deleteSession.AccountName}, slot={deleteItemRequest.Slot}, item={deleteItemRequest.ItemIndex}.");
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
                        if (!sessions.TryGet(connectionId, out var autoTradeSession) || autoTradeSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine("Rejected autotrade-start frame: session is not in USER_PLAY.");
                            continue;
                        }

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
                        var autoTradeResult = autoTradeBook.TryStart(connectionId, autoTradeRequest, autoTradeContext, out var autoTradeSnapshot);
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
                                autoTradeBook.TryStop(connectionId, out _);
                                Console.WriteLine($"Autotrade start rejected: account={autoTradeSession.AccountName}, reason=StatePersistenceUnavailable.");
                                continue;
                            }
                        }

                        if (autoTradeResult == LegacyAutoTradeStartResult.Accepted && autoTradeSnapshot is not null &&
                            LegacyAutoTradeListRelay.TryBuild(connectionId, autoTradeSnapshot, LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, out var autoTradeRelay) &&
                            autoTradeRelay is not null)
                        {
                            await stream.WriteAsync(autoTradeRelay.ResponseFrame, cancellationToken);
                            if (world.TryGetCharacterSnapshot(connectionId, out var autoTradeVisualMob, out _, out _, out _, out var autoTradeVisualAffect) &&
                                autoTradeVisualMob is not null && autoTradeVisualAffect is not null &&
                                LegacyAutoTradeVisualRelay.TryBuild(
                                    autoTradeSnapshot,
                                    autoTradeVisualMob,
                                    autoTradeVisualAffect,
                                    LegacyFrameCodec.CreateDefault(),
                                    frame.Header.ClientTick,
                                    frame.Header.KeywordIndex,
                                    out var autoTradeVisualRelay) &&
                                autoTradeVisualRelay is not null)
                            {
                                await stream.WriteAsync(autoTradeVisualRelay.Frame, cancellationToken);
                                foreach (var recipient in world.GetParticipantIdsInPlayerView(connectionId).Where(id => id != connectionId))
                                    await world.SendAsync(recipient, autoTradeVisualRelay.Frame, cancellationToken);
                            }
                            Console.WriteLine($"Autotrade started: account={autoTradeSession.AccountName}, position=({autoTradeX},{autoTradeY}), tax={autoTradeSnapshot.Tax}, title={autoTradeSnapshot.Title}.");
                        }
                        else
                            Console.WriteLine($"Autotrade start rejected: account={autoTradeSession.AccountName}, reason={autoTradeResult}, location={locationAvailable}, cargo={cargo.Length}.");
                        continue;
                    }

                    if (TradeListRequest.TryParse(frame, out var tradeListRequest) && tradeListRequest is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var tradeListSession) || tradeListSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine("Rejected autotrade-list frame: session is not in USER_PLAY.");
                            continue;
                        }

                        if (!autoTradeBook.TryGet(tradeListRequest.TargetId, out var shopSnapshot) || shopSnapshot is null)
                        {
                            Console.WriteLine($"Autotrade list rejected: requester={tradeListSession.AccountName}, target={tradeListRequest.TargetId}, reason=TargetNotInAutoTrade.");
                            continue;
                        }

                        var requesterStateAvailable = world.TryGetCombatState(connectionId, out var requesterState) && requesterState is not null;
                        var requesterPositionAvailable = world.TryGetCharacterSnapshot(connectionId, out _, out var requesterX, out var requesterY);
                        var targetIsOnline = world.TryGetCharacterSnapshot(tradeListRequest.TargetId, out _, out var targetX, out var targetY);
                        var targetIsOfflineAutoTrade = world.TryGetNpcSnapshot(tradeListRequest.TargetId, out var targetNpc) &&
                            targetNpc is not null && targetNpc.AutoTradeSnapshot is not null;
                        if (!targetIsOnline && targetIsOfflineAutoTrade)
                        {
                            targetX = targetNpc!.PositionX;
                            targetY = targetNpc.PositionY;
                        }
                        var targetPositionAvailable = targetIsOnline || targetIsOfflineAutoTrade;
                        var listContext = new LegacyAutoTradeListContext(
                            requesterStateAvailable ? requesterState!.CurrentScore.Hp : 0,
                            tradeListSession.State == LoginSessionState.Playing && requesterPositionAvailable,
                            targetPositionAvailable,
                            targetIsOnline,
                            TargetAutoTradeActive: true,
                            requesterX,
                            requesterY,
                            targetX,
                            targetY,
                            TargetOfflineAutoTrade: targetIsOfflineAutoTrade);
                        var listResult = LegacyAutoTradeListRules.Validate(tradeListRequest.TargetId, listContext);
                        if (listResult == LegacyAutoTradeListResult.Accepted &&
                            LegacyAutoTradeListRelay.TryBuild(connectionId, shopSnapshot, LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, frame.Header.KeywordIndex, out var listRelay) &&
                            listRelay is not null)
                        {
                            await stream.WriteAsync(listRelay.ResponseFrame, cancellationToken);
                            var targetVisualAvailable = world.TryGetCharacterSnapshot(tradeListRequest.TargetId, out var targetVisualMob, out _, out _, out _, out var targetVisualAffect);
                            if (!targetVisualAvailable && targetIsOfflineAutoTrade)
                            {
                                targetVisualMob = targetNpc!.MobSnapshot;
                                targetVisualAffect = targetNpc.AffectSnapshot;
                                targetVisualAvailable = true;
                            }
                            if (targetVisualAvailable && targetVisualMob is not null && targetVisualAffect is not null &&
                                LegacyAutoTradeVisualRelay.TryBuild(
                                    shopSnapshot,
                                    targetVisualMob,
                                    targetVisualAffect,
                                    LegacyFrameCodec.CreateDefault(),
                                    frame.Header.ClientTick,
                                    frame.Header.KeywordIndex,
                                    out var targetVisualRelay) &&
                                targetVisualRelay is not null)
                            {
                                await stream.WriteAsync(targetVisualRelay.Frame, cancellationToken);
                            }
                            Console.WriteLine($"Autotrade list sent: requester={tradeListSession.AccountName}, target={tradeListRequest.TargetId}, title={shopSnapshot.Title}.");
                        }
                        else
                            Console.WriteLine($"Autotrade list rejected: requester={tradeListSession.AccountName}, target={tradeListRequest.TargetId}, reason={listResult}.");
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
                            cancellationToken);
                        if (hostPurchase.Result == LegacyAutoTradePurchaseHostResult.Accepted &&
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
                            continue;
                        }

                        var closeResult = world.TryCloseTrade(connectionId, out var closeOutcome);
                        Console.WriteLine(closeResult == LegacyTradeCloseResult.Accepted
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
                                    await BroadcastPistaGeneratedNpcsAsync(world, castleStart.SpawnedNpcs, cancellationToken);
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

                        // Exec_MSG_MessageChat sets ID to the sender and calls GridMulticast
                        // around the sender, excluding the sender itself. Keep the original
                        // tick/keyword and only rebuild the encrypted header with that ID.
                        var chatFrame = new MessageChatConfirmation(chatRequest.Message)
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

                        var result = world.TryAcceptParty(connectionId, partyAccept.LeaderConnectionId, partyAccept.MobName, out var formation);
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

                        if (!world.TryRemovePartyMember(connectionId, partyRemove.TargetConnectionId, out var removal) || removal is null)
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

                        var sourceSave = await accounts.TrySaveCharacterGuildAsync(plan.SourceAccountName, plan.SourceCharacterSlot, plan.GuildId, plan.SourceCoin - plan.Cost, cancellationToken);
                        var targetSave = sourceSave == CharacterGuildSaveResult.Success
                            ? await accounts.TrySaveCharacterGuildAsync(plan.TargetAccountName, plan.TargetCharacterSlot, plan.GuildId, plan.TargetCoin, cancellationToken)
                            : CharacterGuildSaveResult.NotAvailable;
                        if (sourceSave != CharacterGuildSaveResult.Success || targetSave != CharacterGuildSaveResult.Success || !world.ApplyGuildInvite(plan))
                        {
                            Console.WriteLine($"Guild invite persistence failed: source={sourceSave}, target={targetSave}.");
                            continue;
                        }

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

                    if (ActionRequest.TryParse(frame, out var action) && action is not null)
                    {
                        if (!sessions.TryGet(connectionId, out var actionSession) || actionSession!.State != LoginSessionState.Playing)
                        {
                            Console.WriteLine($"Rejected action: connection={connectionId}, reason={MovementResult.InvalidState}.");
                            continue;
                        }

                        // Pista entry and other server teleports mutate WorldHub first. Sync the
                        // per-connection movement gate before applying the next client action.
                        if (world.TryGetCharacterSnapshot(connectionId, out _, out var authoritativeX, out var authoritativeY))
                            sessions.SetServerPosition(connectionId, authoritativeX, authoritativeY);

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

                            if (world.TryRecallForMovementRestriction(connectionId, out var recall) && recall is not null)
                            {
                                sessions.SetServerPosition(connectionId, recall.ToX, recall.ToY);
                                var recallFrame = new ActionRequest(
                                    recall.FromX,
                                    recall.FromY,
                                    Effect: 1,
                                    Speed: 2,
                                    Route: new byte[24],
                                    recall.ToX,
                                    recall.ToY)
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                await stream.WriteAsync(recallFrame, cancellationToken);
                                await world.BroadcastAsync(connectionId, recallFrame, cancellationToken);
                                Console.WriteLine($"Movement recalled: account={actionSession.AccountName}, reason={mapRestriction}, from=({recall.FromX},{recall.FromY}), to=({recall.ToX},{recall.ToY}).");
                            }
                            else
                                Console.WriteLine($"Movement rejected: account={actionSession.AccountName}, reason={mapRestriction}, recall unavailable.");
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
                            await world.BroadcastAsync(connectionId, actionFrame, cancellationToken);
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
                        if (skills is null || !sessions.TryGet(connectionId, out var attackSession) || attackSession!.State != LoginSessionState.Playing || !world.TryGetCombatState(connectionId, out var combatState) || combatState is null)
                        {
                            Console.WriteLine($"Ignored attack: connection={connectionId}, skill={attack.SkillIndex}, reason=CombatNotReady.");
                            continue;
                        }

                        // C++ Exec_MSG_Attack rejects a dead attacker before timing, mana, or damage work.
                        // Dead attackers are rejected for ordinary attacks; skill 99 is the explicit legacy resurrection exception.
                        if (combatState.CurrentScore.Hp <= 0 && attack.SkillIndex != 99)
                        {
                            Console.WriteLine($"Rejected attack: account={attackSession.AccountName}, skill={attack.SkillIndex}, reason=AttackerNotAlive.");
                            continue;
                        }

                        var timing = sessions.TryAcceptAttackTiming(connectionId, frame.Header.ClientTick, unchecked((uint)Environment.TickCount64));
                        if (timing != AttackTimingResult.Accepted)
                        {
                            Console.WriteLine($"Rejected attack: account={attackSession.AccountName}, skill={attack.SkillIndex}, reason={timing}.");
                            continue;
                        }

                        var validation = LegacySkillAttackGate.Validate(
                            skills, attack, combatState.CharacterClass, combatState.LearnedSkill, targetIndex: 0);
                        if (validation != SkillAttackValidationResult.Accepted)
                        {
                            Console.WriteLine($"Rejected attack: account={attackSession.AccountName}, skill={attack.SkillIndex}, reason={validation}.");
                            continue;
                        }

                        var mana = world.TryConsumeSkillMana(connectionId, skills[attack.SkillIndex]!, out _, out var mobSnapshot, out var requestedMp);
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

                        if (mana != LegacySkillManaResult.Accepted || mobSnapshot is null || !AttackConfirmation.TryCreate(frame, mobSnapshot, requestedMp, out var attackConfirmation) || attackConfirmation is null)
                        {
                            Console.WriteLine($"Rejected attack: account={attackSession.AccountName}, skill={attack.SkillIndex}, reason={mana}.");
                            continue;
                        }

                        if (itemData is not null && TryResolveSkillCombatInputs(mobSnapshot, itemData, out var magic, out var weaponDamage))
                            Console.WriteLine($"Skill combat inputs prepared: account={attackSession.AccountName}, skill={attack.SkillIndex}, magic={magic}, weaponDamage={weaponDamage}.");

                        var damageResolved = false;
                        var removedMobIds = new HashSet<int>();
                        var spawnedNpcs = new List<LegacyWorldNpc>();
                        if (AttackRequest.TryReadDamageSlot(frame, 0, out var targetConnectionId, out var declaredDamage) && declaredDamage == -2)
                        {
                            var physicalResult = world.TryApplyPhysicalAttack(
                                connectionId,
                                targetConnectionId,
                                RandomNumberGenerator.GetInt32(99, 111),
                                out var physicalOutcome,
                                itemData: itemData);
                            if (physicalResult == LegacyPhysicalAttackResult.OutOfRange)
                            {
                                Console.WriteLine($"Rejected attack: account={attackSession.AccountName}, skill={attack.SkillIndex}, target={targetConnectionId}, reason={physicalResult}.");
                                continue;
                            }

                            if (physicalResult == LegacyPhysicalAttackResult.Accepted && physicalOutcome is not null)
                            {
                                attackConfirmation.TrySetDamageSlot(0, (ushort)physicalOutcome.TargetConnectionId, physicalOutcome.Damage);
                                if (physicalOutcome.TargetRemoved)
                                    removedMobIds.Add(physicalOutcome.TargetConnectionId);
                                if (physicalOutcome.PistaTransition?.SpawnGenerateIndex > 0 &&
                                    world.TrySpawnGeneratedNpc(physicalOutcome.PistaTransition.SpawnGenerateIndex, out var physicalSpawnedNpc))
                                    spawnedNpcs.Add(physicalSpawnedNpc!);
                                if (physicalOutcome.PistaTransition?.SpawnGenerateIndexes is { Count: > 0 } physicalSpawnIndexes)
                                {
                                    foreach (var generateIndex in physicalSpawnIndexes)
                                        if (world.TrySpawnGeneratedNpc(generateIndex, out var generatedNpc) && generatedNpc is not null)
                                            spawnedNpcs.Add(generatedNpc);
                                }
                                if (physicalOutcome.PistaTransition?.RemovedNpcConnectionIds is { Count: > 0 } physicalRemovedNpcIds)
                                    foreach (var removedNpcId in physicalRemovedNpcIds)
                                        removedMobIds.Add(removedNpcId);
                                if (physicalOutcome.PistaTransition?.Teleports is { Count: > 0 } physicalPistaTeleports)
                                    await BroadcastPistaTeleportsAsync(world, physicalPistaTeleports, cancellationToken);
                                if (physicalOutcome.AttackerMobSnapshot is not null &&
                                    AttackConfirmation.TryCreate(frame, physicalOutcome.AttackerMobSnapshot, requestedMp, out var experienceAttackConfirmation) &&
                                    experienceAttackConfirmation is not null)
                                {
                                    experienceAttackConfirmation.TrySetDamageSlot(0, (ushort)physicalOutcome.TargetConnectionId, physicalOutcome.Damage);
                                    attackConfirmation = experienceAttackConfirmation;
                                }
                                if (physicalOutcome.ExperienceAwards is { Count: > 0 } physicalExperienceAwards)
                                {
                                    foreach (var award in physicalExperienceAwards.Where(award => award.ConnectionId != connectionId))
                                    {
                                        var experienceFrame = BuildClientV769UpdateEtcFrame(
                                            award.MobSnapshot,
                                            ReadOnlySpan<byte>.Empty,
                                            (ushort)award.ConnectionId);
                                        await world.SendAsync(award.ConnectionId, experienceFrame, cancellationToken);
                                    }
                                }
                                if (physicalOutcome.ItemDrops is { Count: > 0 } physicalItemDrops)
                                {
                                    foreach (var drop in physicalItemDrops)
                                    {
                                        var itemFrame = new SendItemConfirmation(1, checked((short)drop.InventorySlot), drop.Item)
                                            .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)drop.ConnectionId);
                                        await world.SendAsync(drop.ConnectionId, itemFrame, cancellationToken);
                                    }
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
                                            npc: false);
                                        await world.SendAsync(crimeState.ConnectionId, crimeFrame, cancellationToken);
                                        await world.BroadcastAsync(crimeState.ConnectionId, crimeFrame, cancellationToken);
                                    }
                                }
                                if (physicalOutcome.TargetAffectSnapshot is { } physicalTargetAffect)
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
                                if (physicalOutcome.TargetRevived && physicalOutcome.ConsumedItem is { } consumedItem)
                                    await SendResurrectionRefreshAsync(world, physicalOutcome.TargetConnectionId, physicalOutcome.TargetMobSnapshot, physicalOutcome.ConsumedItemSlot, consumedItem, includeScoreAndResources: true, cancellationToken);
                                if (physicalOutcome.TargetHpChanged && !physicalOutcome.TargetRevived && world.TryGetResourceState(physicalOutcome.TargetConnectionId, out var physicalTargetState, out var physicalTargetRequestedHp, out var physicalTargetRequestedMp) && physicalTargetState is not null)
                                {
                                    var resourceFrame = new SetHpMpConfirmation(physicalTargetState.CurrentScore.Hp, physicalTargetState.CurrentMana, physicalTargetRequestedHp, physicalTargetRequestedMp)
                                        .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)physicalOutcome.TargetConnectionId);
                                    await world.SendAsync(physicalOutcome.TargetConnectionId, resourceFrame, cancellationToken);
                                }
                                damageResolved = true;
                                Console.WriteLine($"Physical attack resolved: account={attackSession.AccountName}, target={targetConnectionId}, damage={physicalOutcome.Damage}, targetHp={physicalOutcome.RemainingHp}.");
                            }
                            else
                                Console.WriteLine($"Physical attack produced no damage: account={attackSession.AccountName}, target={targetConnectionId}, reason={physicalResult}.");
                        }
                        else if (skills[attack.SkillIndex] is { } skillDefinition && (itemData is not null || skillDefinition.Id == 99 || skillDefinition.InstanceType is 6 or 8 or 9 or 11 or 12 || skillDefinition.AffectType > 0 || skillDefinition.TickType > 0))
                        {
                            var targetCount = Math.Min(skillDefinition.MaxTarget + 1, AttackRequest.GetTargetCount(frame.Header.Type));
                            for (var targetIndex = 0; targetIndex < targetCount; targetIndex++)
                            {
                                if (!AttackRequest.TryReadDamageSlot(frame, targetIndex, out targetConnectionId, out declaredDamage) || declaredDamage != -1)
                                    continue;

                                var skillResult = world.TryApplySkillAttack(
                                    connectionId,
                                    targetConnectionId,
                                    skillDefinition,
                                    itemData,
                                    weather: 0,
                                    randomFactor: -1,
                                    out var skillOutcome);
                                if (skillResult == LegacySkillAttackResult.Accepted && skillOutcome is not null)
                                {
                                    attackConfirmation.TrySetDamageSlot(targetIndex, (ushort)skillOutcome.TargetConnectionId, skillOutcome.Damage);
                                    if (skillOutcome.TargetRemoved)
                                        removedMobIds.Add(skillOutcome.TargetConnectionId);
                                    if (skillOutcome.PistaTransition?.SpawnGenerateIndex > 0 &&
                                        world.TrySpawnGeneratedNpc(skillOutcome.PistaTransition.SpawnGenerateIndex, out var skillSpawnedNpc))
                                        spawnedNpcs.Add(skillSpawnedNpc!);
                                    if (skillOutcome.PistaTransition?.SpawnGenerateIndexes is { Count: > 0 } skillSpawnIndexes)
                                    {
                                        foreach (var generateIndex in skillSpawnIndexes)
                                            if (world.TrySpawnGeneratedNpc(generateIndex, out var generatedNpc) && generatedNpc is not null)
                                                spawnedNpcs.Add(generatedNpc);
                                    }
                                    if (skillOutcome.PistaTransition?.RemovedNpcConnectionIds is { Count: > 0 } skillRemovedNpcIds)
                                        foreach (var removedNpcId in skillRemovedNpcIds)
                                            removedMobIds.Add(removedNpcId);
                                    if (skillOutcome.PistaTransition?.Teleports is { Count: > 0 } skillPistaTeleports)
                                        await BroadcastPistaTeleportsAsync(world, skillPistaTeleports, cancellationToken);
                                    if (skillOutcome.AttackerMobSnapshot is not null &&
                                        AttackConfirmation.TryCreate(frame, skillOutcome.AttackerMobSnapshot, requestedMp, out var experienceAttackConfirmation) &&
                                        experienceAttackConfirmation is not null)
                                    {
                                        experienceAttackConfirmation.TrySetDamageSlot(targetIndex, (ushort)skillOutcome.TargetConnectionId, skillOutcome.Damage);
                                        attackConfirmation = experienceAttackConfirmation;
                                    }
                                    if (skillOutcome.ExperienceAwards is { Count: > 0 } skillExperienceAwards)
                                    {
                                        foreach (var award in skillExperienceAwards.Where(award => award.ConnectionId != connectionId))
                                        {
                                            var experienceFrame = BuildClientV769UpdateEtcFrame(
                                                award.MobSnapshot,
                                                ReadOnlySpan<byte>.Empty,
                                                (ushort)award.ConnectionId);
                                            await world.SendAsync(award.ConnectionId, experienceFrame, cancellationToken);
                                        }
                                    }
                                    if (skillOutcome.ItemDrops is { Count: > 0 } skillItemDrops)
                                    {
                                        foreach (var drop in skillItemDrops)
                                        {
                                            var itemFrame = new SendItemConfirmation(1, checked((short)drop.InventorySlot), drop.Item)
                                                .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)drop.ConnectionId);
                                            await world.SendAsync(drop.ConnectionId, itemFrame, cancellationToken);
                                        }
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
                                    if (skillOutcome.PrivateNotices is { Count: > 0 } skillPrivateNotices)
                                        await SendPrivateNoticesAsync(world, skillPrivateNotices, cancellationToken);
                                    if (skillOutcome.GlobalNotices is { Count: > 0 } skillGlobalNotices)
                                        await BroadcastGlobalNoticesAsync(world, skillGlobalNotices, cancellationToken);
                                    if (skillOutcome.AreaNotices is { Count: > 0 } skillAreaNotices)
                                        await BroadcastAreaNoticesAsync(world, skillAreaNotices, cancellationToken);
                                    if (skillOutcome.TargetAffectSnapshot is not null || (skillOutcome.TargetResourceChanged && !skillOutcome.TargetRevived))
                                    {
                                        var encodedScoreFrame = skillOutcome.TargetAffectSnapshot is not null
                                            ? BuildClientV769UpdateScoreFrame(
                                                skillOutcome.TargetMobSnapshot,
                                                skillOutcome.TargetAffectSnapshot,
                                                (ushort)skillOutcome.TargetConnectionId)
                                            : BuildClientV769UpdateScoreWithoutAffectFrame(
                                                skillOutcome.TargetMobSnapshot,
                                                (ushort)skillOutcome.TargetConnectionId);
                                        await world.SendAsync(skillOutcome.TargetConnectionId, encodedScoreFrame, cancellationToken);
                                        if (skillOutcome.TargetAffectSnapshot is { } skillTargetAffect &&
                                            TryBuildClientV769UpdateAffectFrame(
                                                skillTargetAffect,
                                                (ushort)skillOutcome.TargetConnectionId,
                                                unchecked((uint)Environment.TickCount64),
                                                (byte)RandomNumberGenerator.GetInt32(256),
                                                out var skillAffectFrame))
                                            await world.SendAsync(skillOutcome.TargetConnectionId, skillAffectFrame, cancellationToken);
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
                                    if (skillOutcome.TargetRevived && skillOutcome.ConsumedItem is { } consumedItem)
                                        await SendResurrectionRefreshAsync(world, skillOutcome.TargetConnectionId, skillOutcome.TargetMobSnapshot, skillOutcome.ConsumedItemSlot, consumedItem, includeScoreAndResources: true, cancellationToken);
                                    if (skillOutcome.AttackerResourceChanged && world.TryGetResourceState(connectionId, out var summonResourceState, out var summonRequestedHp, out var summonRequestedMp) && summonResourceState is not null)
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
                                                summon: true);
                                            await stream.WriteAsync(createFrame, cancellationToken);
                                            await world.BroadcastAsync(connectionId, createFrame, cancellationToken);
                                        }
                                    }
                                    damageResolved = true;
                                    Console.WriteLine($"Skill attack resolved: account={attackSession.AccountName}, skill={attack.SkillIndex}, targetIndex={targetIndex}, target={targetConnectionId}, baseDamage={skillOutcome.BaseDamage}, resistance={skillOutcome.Resistance}, damage={skillOutcome.Damage}, targetHp={skillOutcome.RemainingHp}, summons={skillOutcome.SummonedMobs?.Count ?? 0}, summonResult={skillOutcome.SummonResult}.");
                                }
                                else
                                    Console.WriteLine($"Skill attack produced no damage: account={attackSession.AccountName}, skill={attack.SkillIndex}, targetIndex={targetIndex}, target={targetConnectionId}, reason={skillResult}.");
                            }
                        }

                        // This is the legacy attack relay with authoritative HP/MP fields. Dam[] remains untouched
                        // for unsupported skill types; physical and elemental skill damage are filled only from WorldHub state.
                        var attackFrame = attackConfirmation.ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256));
                        await stream.WriteAsync(attackFrame, cancellationToken);
                        await world.BroadcastAsync(connectionId, attackFrame, cancellationToken);
                        foreach (var removedMobIdValue in removedMobIds)
                        {
                            var removeFrame = new RemoveMobConfirmation((ushort)removedMobIdValue)
                                .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
                            await stream.WriteAsync(removeFrame, cancellationToken);
                            await world.BroadcastAsync(connectionId, removeFrame, cancellationToken);
                        }
                        foreach (var spawnedNpc in spawnedNpcs)
                        {
                            var generatedFrame = BuildClientV769CreateMobFrame(
                                (ushort)spawnedNpc.ConnectionId,
                                spawnedNpc.PositionX,
                                spawnedNpc.PositionY,
                                spawnedNpc.MobSnapshot,
                                spawnedNpc.AffectSnapshot,
                                npc: true);
                            await stream.WriteAsync(generatedFrame, cancellationToken);
                            await world.BroadcastAsync(connectionId, generatedFrame, cancellationToken);
                        }
                        Console.WriteLine($"Attack mana accepted: account={attackSession.AccountName}, skill={attack.SkillIndex}, mp={requestedMp}; damage={(damageResolved ? "resolved" : "pending")}.");
                        continue;
                    }

                    if (CharacterLogoutRequest.IsValid(frame))
                    {
                        if (!sessions.TryGet(connectionId, out var logoutSession) || logoutSession!.State != LoginSessionState.Playing)
                        {
                            // CharLogOut sends the confirmation even when the legacy user is not in USER_PLAY.
                            await stream.WriteAsync(CharacterLogoutConfirmation.ToFrame(LegacyFrameCodec.CreateDefault(), connectionId, unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256)), cancellationToken);
                            Console.WriteLine($"Character logout acknowledged outside USER_PLAY: connection={connectionId}.");
                            continue;
                        }

                        if (accounts is not null && logoutSession.CharacterSlot >= 0 && logoutSession.AccountName is not null)
                        {
                            if (world.TryGetCharacterSnapshot(connectionId, out var mobSnapshot, out var positionX, out var positionY, out var mobExtraSnapshot, out _, out var clientEquipmentSnapshot) && mobSnapshot is not null)
                            {
                                var saveResult = await accounts.TrySaveCharacterStateAsync(
                                    logoutSession.AccountName, logoutSession.CharacterSlot, mobSnapshot, positionX, positionY, cancellationToken, mobExtraSnapshot);
                                Console.WriteLine($"Character state save: account={logoutSession.AccountName}, slot={logoutSession.CharacterSlot}, position=({positionX},{positionY}), result={saveResult}.");
                                if (accounts is IClientEquipmentStateStore clientEquipmentStore && clientEquipmentSnapshot is not null)
                                {
                                    var equipmentResult = await clientEquipmentStore.TrySaveClientEquipmentAsync(
                                        logoutSession.AccountName, logoutSession.CharacterSlot, clientEquipmentSnapshot, cancellationToken);
                                    Console.WriteLine($"Client 7.69 equipment save: account={logoutSession.AccountName}, slot={logoutSession.CharacterSlot}, result={equipmentResult}.");
                                }
                            }
                            else
                            {
                                var saveResult = await accounts.TrySaveCharacterPositionAsync(
                                    logoutSession.AccountName, logoutSession.CharacterSlot, logoutSession.PositionX, logoutSession.PositionY, cancellationToken);
                                Console.WriteLine($"Character position fallback save: account={logoutSession.AccountName}, slot={logoutSession.CharacterSlot}, position=({logoutSession.PositionX},{logoutSession.PositionY}), result={saveResult}.");
                            }
                        }

                        sessions.CompleteCharacterLogout(connectionId);
                        autoTradeBook.TryStop(connectionId, out var logoutAutoTrade);
                        await RemoveAutoTradeStateAsync(autoTradeStateStore, logoutSession.AccountName, logoutSession.CharacterSlot, logoutAutoTrade, cancellationToken);
                        await BroadcastAutoTradeRemovalAsync(world, logoutAutoTrade, cancellationToken);
                        world.Leave(connectionId, out var logoutSummons);
                        await BroadcastSummonDespawnsAsync(world, logoutSummons, cancellationToken);
                        await stream.WriteAsync(CharacterLogoutConfirmation.ToFrame(LegacyFrameCodec.CreateDefault(), connectionId, unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256)), cancellationToken);
                        Console.WriteLine($"Character logout accepted: account={logoutSession.AccountName}, connection={connectionId}.");
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
            serverLog?.Write($"CONNECTION FINALLY connection={connectionId} state={(sessions.TryGet(connectionId, out var finalSession) ? finalSession!.State : LoginSessionState.Closed)}");
            if (accounts is not null && sessions.TryGet(connectionId, out var disconnectedSession) && disconnectedSession!.State == LoginSessionState.Playing && disconnectedSession.AccountName is not null && disconnectedSession.CharacterSlot >= 0)
            {
                if (world.TryGetCharacterSnapshot(connectionId, out var mobSnapshot, out var positionX, out var positionY, out var mobExtraSnapshot, out _, out var clientEquipmentSnapshot) && mobSnapshot is not null)
                {
                    var saveResult = await accounts.TrySaveCharacterStateAsync(
                        disconnectedSession.AccountName, disconnectedSession.CharacterSlot, mobSnapshot, positionX, positionY, CancellationToken.None, mobExtraSnapshot);
                    Console.WriteLine($"Character disconnect state save: account={disconnectedSession.AccountName}, slot={disconnectedSession.CharacterSlot}, position=({positionX},{positionY}), result={saveResult}.");
                    if (accounts is IClientEquipmentStateStore clientEquipmentStore && clientEquipmentSnapshot is not null)
                    {
                        var equipmentResult = await clientEquipmentStore.TrySaveClientEquipmentAsync(
                            disconnectedSession.AccountName, disconnectedSession.CharacterSlot, clientEquipmentSnapshot, CancellationToken.None);
                        Console.WriteLine($"Client 7.69 equipment disconnect save: account={disconnectedSession.AccountName}, slot={disconnectedSession.CharacterSlot}, result={equipmentResult}.");
                    }
                }
                else
                {
                    var saveResult = await accounts.TrySaveCharacterPositionAsync(
                        disconnectedSession.AccountName, disconnectedSession.CharacterSlot, disconnectedSession.PositionX, disconnectedSession.PositionY, CancellationToken.None);
                    Console.WriteLine($"Character disconnect position fallback save: account={disconnectedSession.AccountName}, slot={disconnectedSession.CharacterSlot}, position=({disconnectedSession.PositionX},{disconnectedSession.PositionY}), result={saveResult}.");
                }
            }

            autoTradeBook.TryStop(connectionId, out var disconnectedAutoTrade);
            if (sessions.TryGet(connectionId, out var endedSession))
                await RemoveAutoTradeStateAsync(autoTradeStateStore, endedSession!.AccountName, endedSession.CharacterSlot, disconnectedAutoTrade, CancellationToken.None);
            await BroadcastAutoTradeRemovalAsync(world, disconnectedAutoTrade, CancellationToken.None);
            world.Leave(connectionId, out var disconnectedSummons, out var disconnectedTrade);
            if (disconnectedTrade is not null)
                Console.WriteLine($"Trade cancelled by disconnect: connection={disconnectedTrade.ConnectionId}, opponent={disconnectedTrade.OpponentId}.");
            await BroadcastSummonDespawnsAsync(world, disconnectedSummons, CancellationToken.None);
            serverLog?.Write($"CONNECTION CLOSED connection={connectionId}");
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
    return W2ppCreateMobV1Adapter.AdaptPayload(sourcePayload, appearance?.VisualEquipment, appearance?.AncientCodes)
        .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
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
    return W2ppUpdateScoreV1Adapter.AdaptPayload(sourcePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize))
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
    try
    {
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            if (!world.TryApplyAllSummonAttacks(randomFactor: -1, out var summonAttacks))
                continue;

            foreach (var summonAttack in summonAttacks)
            {
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
                        .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
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
            var mobLeftCodec = LegacyFrameCodec.CreateDefault();
            if (world.TryProcessPistaLv6MobLeft(
                    now,
                    mobLeftCodec,
                    unchecked((uint)Environment.TickCount64),
                    (byte)RandomNumberGenerator.GetInt32(256),
                    out var mobLeftFrames)
                && mobLeftFrames is { Count: > 0 })
            {
                foreach (var mobLeft in mobLeftFrames)
                    await world.SendAsync(mobLeft.ConnectionId, mobLeft.Frame, cancellationToken);
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
                await BroadcastPistaGeneratedNpcsAsync(world, entryPlan.SpawnedNpcs, cancellationToken);
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

static async Task BroadcastAutoTradeRemovalAsync(WorldHub world, LegacyAutoTradeSnapshot? snapshot, CancellationToken cancellationToken)
{
    if (snapshot is null ||
        !LegacyAutoTradeRemovalRelay.TryBuild(
            snapshot,
            LegacyFrameCodec.CreateDefault(),
            unchecked((uint)Environment.TickCount64),
            (byte)RandomNumberGenerator.GetInt32(256),
            out var plan) ||
        plan is null)
        return;

    var recipients = world.GetParticipantIdsInPlayerView(plan.ShopConnectionId);
    if (recipients.Count == 0)
        recipients = world.GetParticipantIdsInNpcView(plan.ShopConnectionId);
    foreach (var recipient in recipients.Where(id => id != plan.ShopConnectionId))
        await world.SendAsync(recipient, plan.Frame, cancellationToken);
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

static async Task BroadcastPistaGeneratedNpcsAsync(WorldHub world, IReadOnlyList<LegacyWorldNpc> npcs, CancellationToken cancellationToken)
{
    foreach (var npc in npcs)
    {
        var frame = BuildClientV769CreateMobFrame(
            (ushort)npc.ConnectionId,
            npc.PositionX,
            npc.PositionY,
            npc.MobSnapshot,
            npc.AffectSnapshot,
            npc: true);
        await world.BroadcastAsync(-1, frame, cancellationToken);
    }
}

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
        if (world.TrySpawnGeneratedNpc(generateIndex, out var npc) && npc is not null)
            spawned.Add(npc);
    return spawned;
}

static IReadOnlyList<LegacyWorldNpc> SpawnReferenceDonateNpc(WorldHub world)
{
    // Explicit opt-in smoke path. The selected NPCGener definition controls
    // the position; production startup does not spawn this extra NPC.
    return world.TrySpawnGeneratedNpc(4639, out var npc) && npc is not null
        ? [npc]
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
    CancellationToken cancellationToken)
{
    var codec = LegacyFrameCodec.CreateDefault();
    var clientTick = unchecked((uint)Environment.TickCount64);
    var keywordIndex = (byte)RandomNumberGenerator.GetInt32(256);
    var itemFrame = new SendItemConfirmation(1, checked((short)itemSlot), item)
        .ToFrame(codec, clientTick, keywordIndex, (ushort)connectionId);
    await world.SendAsync(connectionId, itemFrame, cancellationToken);

    if (includeScoreAndResources && world.TryGetResourceState(connectionId, out var resourceState, out var requestedHp, out var requestedMp) && resourceState is not null)
    {
        var scoreFrame = BuildClientV769UpdateScoreWithoutAffectFrame(mob, (ushort)connectionId, clientTick, keywordIndex);
        await world.SendAsync(connectionId, scoreFrame, cancellationToken);
        var hpMpFrame = new SetHpMpConfirmation(resourceState.CurrentScore.Hp, resourceState.CurrentMana, requestedHp, requestedMp)
            .ToFrame(codec, clientTick, keywordIndex, (ushort)connectionId);
        await world.SendAsync(connectionId, hpMpFrame, cancellationToken);
    }

    if (world.TryBuildCreateMobFrame(connectionId, codec, clientTick, keywordIndex, out var createFrame) && createFrame is not null)
    {
        await world.SendAsync(connectionId, createFrame, cancellationToken);
        await world.BroadcastAsync(connectionId, createFrame, cancellationToken);
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

sealed record ServerOptions(string BindAddress, int Port, string? AccountRoot, string? AccountDatabaseConfigPath, string? SkillDataPath, string? ItemDataPath, string? InitItemPath, string? HeightMapPath, string? AttributeMapPath, string? GuildDataPath, string? SummonRoot, string? NpcGenerationPath, string? NpcRoot, string? CastleQuestPath, string? DonateShopCatalogPath, string? DonateDatabaseConfigPath, bool SpawnCityNpcs, bool SpawnDonateNpc, LegacyMapCollisionMode MapCollisionMode, LegacyServerMode ServerMode, string? AutoTradeStatePath, string? StatusFilePath, int? StatusSlot)
{
    public string WorldKey => new LegacyServerModePolicy(ServerMode).WorldKey;

    public static ServerOptions Parse(string[] args)
    {
        if (args.Length == 1 && int.TryParse(args[0], out var legacyPort)) return new("127.0.0.1", legacyPort, null, null, null, null, null, null, null, null, null, null, null, null, null, null, false, false, LegacyMapCollisionMode.LegacyCompatible, LegacyServerMode.Up, null, null, null);

        var port = 8281; // GAME_PORT — matches Basedef.h and WYD.EXE client expectation
        var bindAddress = "127.0.0.1";
        string? accountRoot = null;
        string? accountDatabaseConfigPath = null;
        string? skillDataPath = null;
        string? itemDataPath = null;
        string? initItemPath = null;
        string? heightMapPath = null;
        string? attributeMapPath = null;
        string? guildDataPath = null;
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
                case "--mode" when index + 1 < args.Length:
                    if (!Enum.TryParse(args[++index], ignoreCase: true, out serverMode))
                        throw new ArgumentException("--mode must be 'up' or 'pvp'.");
                    break;
                case "--world" when index + 1 < args.Length:
                    if (!LegacyServerModePolicy.TryParseWorldKey(args[++index], out serverMode))
                        throw new ArgumentException("--world must be 'UP' or 'PVP'.");
                    break;
                default:
                    throw new ArgumentException("Usage: WydCdk.Server [--bind <ip-address>] [--port <port>] [--world <UP|PVP>] [--mode <up|pvp>] [--accounts-root <legacy-account-directory>] [--mariadb-account-config <mariadb-config.json>] [--skill-data <SkillData.csv>] [--item-data <ItemList.bin>] [--init-item <InitItem.bin>] [--heightmap <heightmap.dat> --attribute-map <AttributeMap.dat>] [--guild-data <Guild.txt>] [--base-summon <BaseSummon-directory>] [--npc-generation <NPCGener.txt> --npc-root <npc-directory>] [--city-npcs] [--donate-npc] [--castle-quest <CastleQuest.txt>] [--donate-catalog <DonateShop.csv>] [--donate-db-config <mariadb-config.json>] [--autotrade-state <path>] [--status-file <servtest.htm> --status-slot <1-9>] [--strict-map-collision]");
            }
        }

        if (!IPAddress.TryParse(bindAddress, out _))
            throw new ArgumentException("--bind must be a valid IPv4 or IPv6 address.");
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(args), "Port must be between 1 and 65535.");
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
        if (statusSlot is not null && (statusSlot < 1 || statusSlot >= ServerStatusFilePublisher.SlotCount))
            throw new ArgumentOutOfRangeException(nameof(args), "--status-slot must be between 1 and 9.");
        return new(bindAddress, port, accountRoot, accountDatabaseConfigPath, skillDataPath, itemDataPath, initItemPath, heightMapPath, attributeMapPath, guildDataPath, summonRoot, npcGenerationPath, npcRoot, castleQuestPath, donateShopCatalogPath, donateDatabaseConfigPath, spawnCityNpcs, spawnDonateNpc, mapCollisionMode, serverMode, autoTradeStatePath, statusFilePath, statusSlot);
    }
}

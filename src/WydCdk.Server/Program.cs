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
Console.WriteLine($"WydCdk listener: {options.BindAddress}:{options.Port}");
Console.WriteLine($"World: {options.WorldKey} (shared account/character base; world rules are server-authoritative).");
Console.WriteLine(accountAuthenticator is null
        ? "Diagnostic mode: validates MSG_AccountLogin only; account files and MariaDB are not accessed."
    : options.AccountDatabaseConfigPath is null
        ? $"Local account mode: authentication at {options.AccountRoot}; successful logins receive character selection, cargo, and coin read from the legacy account file, and empty slots accept MSG_CreateCharacter."
        : $"MariaDB account/world mode: authentication configured by {options.AccountDatabaseConfigPath}; character blobs are stored by account_name and world_key in MariaDB.");
Console.WriteLine(skills is null ? "Skill data: disabled; attack frames are not processed." : $"Skill data: loaded from {options.SkillDataPath}; attack metadata gate enabled.");
Console.WriteLine(itemData is null ? "Item data: disabled; skill combat inputs are not derived from ItemList.bin." : $"Item data: loaded from {options.ItemDataPath}; Magic and WeaponDamage inputs enabled.");
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
Console.WriteLine($"Map collision: {options.MapCollisionMode}.");
Console.WriteLine("Press Ctrl+C to stop.");

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; shutdown.Cancel(); };
var nextConnectionId = 0;
var summonBattleLoop = RunSummonBattleLoopAsync(world, shutdown.Token);
var pistaScheduleLoop = RunPistaScheduleLoopAsync(world, shutdown.Token);

try
{
    while (!shutdown.IsCancellationRequested)
    {
        var client = await listener.AcceptTcpClientAsync(shutdown.Token);
        var connectionId = Interlocked.Increment(ref nextConnectionId);
        _ = InspectConnectionAsync(client, connectionId, accounts, accountAuthenticator, templates, skills, itemData, donateBalances, world, options.DonateDatabaseConfigPath is not null, serverLog, shutdown.Token);
    }
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
}
finally
{
    listener.Stop();
    shutdown.Cancel();
    try { await summonBattleLoop; }
    catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
    try { await pistaScheduleLoop; }
    catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
}

static async Task InspectConnectionAsync(TcpClient client, int connectionId, ICharacterStore? accounts, IAccountStore? accountAuthenticator, LegacyCharacterTemplateStore? templates, LegacySkillDataTable? skills, LegacyItemDataTable? itemData, IDonateBalanceStore? donateBalances, WorldHub world, bool explicitDonateDatabase, ServerWireLog? serverLog, CancellationToken cancellationToken)
{
    using (client)
    {
        using var stream = client.GetStream();
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
                        var response = confirmation.ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
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

                        var outcome = await new CreateCharacterCoordinator(accounts, templates).HandleAsync(session.AccountName!, createCharacter, session.SecureVerified, cancellationToken);
                        if (outcome.IsSuccess)
                        {
                            var confirmation = new NewCharacterConfirmation(outcome.Characters!);
                            var response = confirmation.ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
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

                        var outcome = await new DeleteCharacterCoordinator(accounts).HandleAsync(deleteSession.AccountName!, deleteCharacter, deleteSession.SecureVerified, cancellationToken);
                        if (outcome.IsSuccess && outcome.Snapshot is not null)
                        {
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, new DeleteCharacterConfirmation(outcome.Snapshot.Characters).ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256)), cancellationToken, $"delete-character-confirmation account={deleteSession.AccountName} slot={deleteCharacter.Slot}");
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

                        var outcome = await new CharacterLoginCoordinator(accounts).HandleAsync(loginSession.AccountName!, characterLogin, loginSession.SecureVerified, cancellationToken);
                        if (outcome.IsSuccess)
                        {
                            var data = outcome.Data!;
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
                            var confirmation = new CharacterLoginConfirmation(
                                (ushort)characterLogin.Slot, data.Mob, data.ShortSkill, data.Affect, data.MobExtra, donate,
                                data.SavedPositionX, data.SavedPositionY, clientId: (ushort)connectionId, weather: 0);
                            var loginResponse = confirmation.ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, loginResponse, cancellationToken, $"character-login-confirmation account={loginSession.AccountName} slot={characterLogin.Slot} position=({data.SavedPositionX},{data.SavedPositionY})");
                            sessions.CompleteCharacterLogin(connectionId, characterLogin.Slot, data.SavedPositionX, data.SavedPositionY);
                            var spawnFrame = new CreateMobConfirmation((ushort)connectionId, data.SavedPositionX, data.SavedPositionY, data.Mob)
                                .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
                            await world.EnterAsync(connectionId, loginSession.AccountName!, (frame, token) => WriteLoggedFrameAsync(stream, serverLog, connectionId, frame, token, "world-enter"), spawnFrame, cancellationToken);
                            foreach (var npc in world.GetNpcSnapshots())
                            {
                                var npcFrame = new CreateMobConfirmation(
                                    (ushort)npc.ConnectionId,
                                    npc.PositionX,
                                    npc.PositionY,
                                    npc.MobSnapshot,
                                    npc.AffectSnapshot,
                                    npc: true)
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
                                await WriteLoggedFrameAsync(stream, serverLog, connectionId, npcFrame, cancellationToken, $"npc-spawn npc={npc.ConnectionId}");
                            }
                            world.SetCharacterState(
                                connectionId, characterLogin.Slot,
                                BinaryPrimitives.ReadUInt16LittleEndian(data.Mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset)),
                                data.Mob[LegacyAccountSnapshot.MobClanOffset],
                                data.Mob[LegacyAccountSnapshot.MobGuildLevelOffset],
                                 BinaryPrimitives.ReadInt32LittleEndian(data.Mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset)),
                                 data.SavedPositionX, data.SavedPositionY, data.Mob,
                                 data.MobExtra.Length >= sizeof(short)
                                     ? BinaryPrimitives.ReadInt16LittleEndian(data.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset))
                                     : (short)LegacyAccountSnapshot.ClassMasterMortal,
                                 data.Affect,
                                 data.MobExtra);
                            world.SetDonateBalance(connectionId, donate);
                            var etcFrame = new UpdateEtcConfirmation(data.Mob, data.MobExtra)
                                .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                            var scoreFrame = new UpdateScoreConfirmation(data.Mob)
                                .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, etcFrame, cancellationToken, "character-update-etc");
                            await WriteLoggedFrameAsync(stream, serverLog, connectionId, scoreFrame, cancellationToken, "character-update-score");
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
                                var etcFrame = new UpdateEtcConfirmation(experienceConsumableOutcome.MobSnapshot)
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                await stream.WriteAsync(etcFrame, cancellationToken);
                                if (experienceConsumableOutcome.Stage > 0 && (experienceConsumableOutcome.Volatile == 7 || experienceConsumableOutcome.LeveledUp))
                                {
                                    var scoreFrame = new UpdateScoreConfirmation(experienceConsumableOutcome.MobSnapshot)
                                        .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
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
                                var scoreFrame = new UpdateScoreConfirmation(affectConsumableOutcome.MobSnapshot, affectConsumableOutcome.AffectSnapshot)
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                await stream.WriteAsync(scoreFrame, cancellationToken);
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
                                var scoreFrame = new UpdateScoreConfirmation(pvpJewelryOutcome.MobSnapshot, pvpJewelryOutcome.AffectSnapshot)
                                    .ToFrame(LegacyFrameCodec.CreateDefault(), frame.Header.ClientTick, (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
                                await stream.WriteAsync(scoreFrame, cancellationToken);
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
                                        var experienceFrame = new UpdateEtcConfirmation(award.MobSnapshot)
                                            .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)award.ConnectionId);
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
                                        var coinFrame = new UpdateEtcConfirmation(update.MobSnapshot)
                                            .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)update.ConnectionId);
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
                                    var mountFrame = new SendItemConfirmation(0, 14, updatedMountItem)
                                        .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)physicalOutcome.MountOwnerConnectionId);
                                    await world.SendAsync(physicalOutcome.MountOwnerConnectionId, mountFrame, cancellationToken);
                                }
                                if (physicalOutcome.CrimeStateUpdates is { Count: > 0 } crimeStateUpdates)
                                {
                                    foreach (var crimeState in crimeStateUpdates)
                                    {
                                        var crimeFrame = new CreateMobConfirmation(
                                            (ushort)crimeState.ConnectionId,
                                            crimeState.PositionX,
                                            crimeState.PositionY,
                                            crimeState.MobSnapshot)
                                            .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
                                        await world.SendAsync(crimeState.ConnectionId, crimeFrame, cancellationToken);
                                        await world.BroadcastAsync(crimeState.ConnectionId, crimeFrame, cancellationToken);
                                    }
                                }
                                if (physicalOutcome.TargetAffectSnapshot is { } physicalTargetAffect)
                                {
                                    var scoreFrame = new UpdateScoreConfirmation(physicalOutcome.TargetMobSnapshot, physicalTargetAffect)
                                        .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)physicalOutcome.TargetConnectionId);
                                    await world.SendAsync(physicalOutcome.TargetConnectionId, scoreFrame, cancellationToken);
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
                                            var experienceFrame = new UpdateEtcConfirmation(award.MobSnapshot)
                                                .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)award.ConnectionId);
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
                                            var coinFrame = new UpdateEtcConfirmation(update.MobSnapshot)
                                                .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)update.ConnectionId);
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
                                        var scoreFrame = skillOutcome.TargetAffectSnapshot is not null
                                            ? new UpdateScoreConfirmation(skillOutcome.TargetMobSnapshot, skillOutcome.TargetAffectSnapshot)
                                            : new UpdateScoreConfirmation(skillOutcome.TargetMobSnapshot);
                                        var encodedScoreFrame = scoreFrame
                                            .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)skillOutcome.TargetConnectionId);
                                        await world.SendAsync(skillOutcome.TargetConnectionId, encodedScoreFrame, cancellationToken);
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
                                        var scoreFrame = new UpdateScoreConfirmation(skillOutcome.TargetMobSnapshot)
                                            .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256), (ushort)connectionId);
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
                                            var createFrame = new CreateMobConfirmation(
                                                (ushort)summonedMob.ConnectionId,
                                                summonedMob.PositionX,
                                                summonedMob.PositionY,
                                                summonedMob.MobSnapshot,
                                                summonedMob.AffectSnapshot,
                                                npc: true,
                                                summon: true)
                                                .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
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
                            var generatedFrame = new CreateMobConfirmation(
                                (ushort)spawnedNpc.ConnectionId,
                                spawnedNpc.PositionX,
                                spawnedNpc.PositionY,
                                spawnedNpc.MobSnapshot,
                                spawnedNpc.AffectSnapshot,
                                npc: true)
                                .ToFrame(LegacyFrameCodec.CreateDefault(), unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
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
                            if (world.TryGetCharacterSnapshot(connectionId, out var mobSnapshot, out var positionX, out var positionY, out var mobExtraSnapshot) && mobSnapshot is not null)
                            {
                                var saveResult = await accounts.TrySaveCharacterStateAsync(
                                    logoutSession.AccountName, logoutSession.CharacterSlot, mobSnapshot, positionX, positionY, cancellationToken, mobExtraSnapshot);
                                Console.WriteLine($"Character state save: account={logoutSession.AccountName}, slot={logoutSession.CharacterSlot}, position=({positionX},{positionY}), result={saveResult}.");
                            }
                            else
                            {
                                var saveResult = await accounts.TrySaveCharacterPositionAsync(
                                    logoutSession.AccountName, logoutSession.CharacterSlot, logoutSession.PositionX, logoutSession.PositionY, cancellationToken);
                                Console.WriteLine($"Character position fallback save: account={logoutSession.AccountName}, slot={logoutSession.CharacterSlot}, position=({logoutSession.PositionX},{logoutSession.PositionY}), result={saveResult}.");
                            }
                        }

                        sessions.CompleteCharacterLogout(connectionId);
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
                if (world.TryGetCharacterSnapshot(connectionId, out var mobSnapshot, out var positionX, out var positionY, out var mobExtraSnapshot) && mobSnapshot is not null)
                {
                    var saveResult = await accounts.TrySaveCharacterStateAsync(
                        disconnectedSession.AccountName, disconnectedSession.CharacterSlot, mobSnapshot, positionX, positionY, CancellationToken.None, mobExtraSnapshot);
                    Console.WriteLine($"Character disconnect state save: account={disconnectedSession.AccountName}, slot={disconnectedSession.CharacterSlot}, position=({positionX},{positionY}), result={saveResult}.");
                }
                else
                {
                    var saveResult = await accounts.TrySaveCharacterPositionAsync(
                        disconnectedSession.AccountName, disconnectedSession.CharacterSlot, disconnectedSession.PositionX, disconnectedSession.PositionY, CancellationToken.None);
                    Console.WriteLine($"Character disconnect position fallback save: account={disconnectedSession.AccountName}, slot={disconnectedSession.CharacterSlot}, position=({disconnectedSession.PositionX},{disconnectedSession.PositionY}), result={saveResult}.");
                }
            }

            world.Leave(connectionId, out var disconnectedSummons);
            await BroadcastSummonDespawnsAsync(world, disconnectedSummons, CancellationToken.None);
            serverLog?.Write($"CONNECTION CLOSED connection={connectionId}");
        }
    }
}

static async ValueTask WriteLoggedFrameAsync(NetworkStream stream, ServerWireLog? serverLog, int connectionId, ReadOnlyMemory<byte> frame, CancellationToken cancellationToken, string? note = null)
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

static async Task BroadcastPistaGeneratedNpcsAsync(WorldHub world, IReadOnlyList<LegacyWorldNpc> npcs, CancellationToken cancellationToken)
{
    var codec = LegacyFrameCodec.CreateDefault();
    foreach (var npc in npcs)
    {
        var frame = new CreateMobConfirmation(
            (ushort)npc.ConnectionId,
            npc.PositionX,
            npc.PositionY,
            npc.MobSnapshot,
            npc.AffectSnapshot,
            npc: true)
            .ToFrame(codec, unchecked((uint)Environment.TickCount64), (byte)RandomNumberGenerator.GetInt32(256));
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
        var scoreFrame = new UpdateScoreConfirmation(mob)
            .ToFrame(codec, clientTick, keywordIndex, (ushort)connectionId);
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

sealed record ServerOptions(string BindAddress, int Port, string? AccountRoot, string? AccountDatabaseConfigPath, string? SkillDataPath, string? ItemDataPath, string? HeightMapPath, string? AttributeMapPath, string? GuildDataPath, string? SummonRoot, string? NpcGenerationPath, string? NpcRoot, string? CastleQuestPath, string? DonateShopCatalogPath, string? DonateDatabaseConfigPath, bool SpawnCityNpcs, bool SpawnDonateNpc, LegacyMapCollisionMode MapCollisionMode, LegacyServerMode ServerMode)
{
    public string WorldKey => new LegacyServerModePolicy(ServerMode).WorldKey;

    public static ServerOptions Parse(string[] args)
    {
        if (args.Length == 1 && int.TryParse(args[0], out var legacyPort)) return new("127.0.0.1", legacyPort, null, null, null, null, null, null, null, null, null, null, null, null, null, false, false, LegacyMapCollisionMode.LegacyCompatible, LegacyServerMode.Up);

        var port = 8281; // GAME_PORT — matches Basedef.h and WYD.EXE client expectation
        var bindAddress = "127.0.0.1";
        string? accountRoot = null;
        string? accountDatabaseConfigPath = null;
        string? skillDataPath = null;
        string? itemDataPath = null;
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
                case "--mode" when index + 1 < args.Length:
                    if (!Enum.TryParse(args[++index], ignoreCase: true, out serverMode))
                        throw new ArgumentException("--mode must be 'up' or 'pvp'.");
                    break;
                case "--world" when index + 1 < args.Length:
                    if (!LegacyServerModePolicy.TryParseWorldKey(args[++index], out serverMode))
                        throw new ArgumentException("--world must be 'UP' or 'PVP'.");
                    break;
                default:
                    throw new ArgumentException("Usage: WydCdk.Server [--bind <ip-address>] [--port <port>] [--world <UP|PVP>] [--mode <up|pvp>] [--accounts-root <legacy-account-directory>] [--mariadb-account-config <mariadb-config.json>] [--skill-data <SkillData.csv>] [--item-data <ItemList.bin>] [--heightmap <heightmap.dat> --attribute-map <AttributeMap.dat>] [--guild-data <Guild.txt>] [--base-summon <BaseSummon-directory>] [--npc-generation <NPCGener.txt> --npc-root <npc-directory>] [--city-npcs] [--donate-npc] [--castle-quest <CastleQuest.txt>] [--donate-catalog <DonateShop.csv>] [--donate-db-config <mariadb-config.json>] [--strict-map-collision]");
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
        return new(bindAddress, port, accountRoot, accountDatabaseConfigPath, skillDataPath, itemDataPath, heightMapPath, attributeMapPath, guildDataPath, summonRoot, npcGenerationPath, npcRoot, castleQuestPath, donateShopCatalogPath, donateDatabaseConfigPath, spawnCityNpcs, spawnDonateNpc, mapCollisionMode, serverMode);
    }
}

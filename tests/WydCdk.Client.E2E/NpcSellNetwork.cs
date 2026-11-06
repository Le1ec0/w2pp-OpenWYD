using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class NpcSellNetwork
{
    private const int CharacterOffset = 216;
    private const int TargetNpcId = 20_000;
    private const int ErionVillage = 2;
    private const int DefaultCityTax = 5;

    public static async Task RunAsync(string itemDataPath, CancellationToken cancellationToken = default)
        => await RunScenarioAsync(itemDataPath, expectOutOfViewSignal: false, expectEmptyCarryResync: false, sellFromCargo: false, cancellationToken);

    public static async Task RunCargoAsync(string itemDataPath, CancellationToken cancellationToken = default)
        => await RunScenarioAsync(itemDataPath, expectOutOfViewSignal: false, expectEmptyCarryResync: false, sellFromCargo: true, cancellationToken);

    public static async Task RunOutOfViewAsync(string itemDataPath, CancellationToken cancellationToken = default)
        => await RunScenarioAsync(itemDataPath, expectOutOfViewSignal: true, expectEmptyCarryResync: false, sellFromCargo: false, cancellationToken);

    public static async Task RunEmptyCarryResyncAsync(string itemDataPath, CancellationToken cancellationToken = default)
        => await RunScenarioAsync(itemDataPath, expectOutOfViewSignal: false, expectEmptyCarryResync: true, sellFromCargo: false, cancellationToken);

    private static async Task RunScenarioAsync(
        string itemDataPath,
        bool expectOutOfViewSignal,
        bool expectEmptyCarryResync,
        bool sellFromCargo,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(itemDataPath))
            throw new FileNotFoundException("NPC sell smoke ItemList.bin was not found.", itemDataPath);

        var checkoutRoot = FindCheckoutRoot();
        var projectRoot = Directory.GetParent(checkoutRoot)?.FullName ??
            throw new DirectoryNotFoundException("Could not locate the WYD CDK project root.");
        var sourceRoot = Path.Combine(projectRoot, "Backup", "Tools", "ReferenceSources", "TMProject2GlobalClient", "Servidor", "Server", "TMSrv", "run");
        var sourceNpcGenerationPath = Path.Combine(sourceRoot, "NPCGener.txt");
        var sourceNpcRoot = Path.Combine(sourceRoot, "npc");
        if (!File.Exists(sourceNpcGenerationPath) || !Directory.Exists(sourceNpcRoot))
            throw new DirectoryNotFoundException("The local TMProject 7.69 NPC fixture data is unavailable.");

        var itemData = LegacyItemDataTable.Load(itemDataPath);
        var (item, definition) = SelectSaleItem(itemData);
        var price = LegacyNpcSellPriceMath.Calculate(
            definition.Price,
            item.Index,
            sellerClass: 3,
            learnedSkill: 0x1000,
            special2: 0,
            npcVillage: ErionVillage,
            cityTax: DefaultCityTax);
        if (!price.IsSuccess || price.Amount <= 0)
            throw new InvalidDataException($"Selected sale item {item.Index} did not produce a positive fixture price: {price.Status}/{price.Amount}.");

        var smokeRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"wyd-cdk-npc-sell-{Guid.NewGuid():N}"));
        EnsureInsideTempRoot(smokeRoot);
        Directory.CreateDirectory(smokeRoot);
        Process? server = null;
        Task<string>? standardOutput = null;
        Task<string>? standardError = null;
        var gracefulShutdown = false;
        try
        {
            var port = ReserveLoopbackPort();
            var fixture = await GuildWarFixtureFactory.CreateAsync(
                smokeRoot,
                port,
                connectionId: 11,
                accountName: "NPCSELL",
                password: "SELLPASS",
                characterName: "NPCSELL_HERO",
                guildId: 7,
                guildLevel: 0,
                cancellationToken);
            var seededItem = expectEmptyCarryResync ? default : item;
            await SeedSellerAsync(
                fixture.AccountPath,
                seededItem,
                sellFromCargo ? LegacyNpcSellSourceKind.Cargo : LegacyNpcSellSourceKind.Carry,
                bypassMerchantViewGate: !expectOutOfViewSignal,
                cancellationToken);
            var (npcGenerationPath, npcRoot) = await PrepareCityNpcFixturesAsync(
                smokeRoot,
                sourceNpcGenerationPath,
                sourceNpcRoot,
                markPerzenAsMerchant: expectOutOfViewSignal,
                cancellationToken);
            server = StartServer(
                checkoutRoot,
                itemDataPath,
                npcGenerationPath,
                npcRoot,
                Path.Combine(smokeRoot, "accounts"),
                Path.Combine(smokeRoot, "review"),
                port);
            standardOutput = server.StandardOutput.ReadToEndAsync();
            standardError = server.StandardError.ReadToEndAsync();
            await WaitForServerAsync(server, port, cancellationToken);

            var codec = LegacyFrameCodec.CreateDefault();
            await using (var session = await E2eSession.ConnectAndLoginAsync(fixture.Options, fixture.Header, codec, "NPC_SELL_"))
            {
                var sellerId = BinaryPrimitives.ReadUInt16LittleEndian(
                    session.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);
                Ensure(sellerId is > 0 and < LegacyMobAiPlanner.MaximumPlayerId,
                    $"Authenticated seller received an invalid ring connection ID {sellerId}.");

                var sourceKind = sellFromCargo ? LegacyNpcSellSourceKind.Cargo : LegacyNpcSellSourceKind.Carry;
                var request = new ShopSellMessage(TargetNpcId, (short)sourceKind, 0);
                var requestFrame = request.ToFrame(codec, 0x12345678, 0x37, sellerId);
                await session.Stream.WriteAsync(requestFrame, cancellationToken);

                if (expectOutOfViewSignal)
                {
                    var closeShop = await ReadUntilTypeAsync(session.Stream, codec, CloseShopSignal.MessageType, cancellationToken);
                    Ensure(closeShop.Header.Id == CloseShopSignal.SceneId &&
                        closeShop.Header.Size == PacketHeader.SizeInBytes && closeShop.Header.ClientTick == 0 &&
                        closeShop.Header.KeywordIndex == 0 && closeShop.Payload.IsEmpty,
                        "An out-of-view NPC sale did not receive the empty ESCENE_FIELD close-shop signal.");
                }
                else if (expectEmptyCarryResync)
                {
                    var resyncFrame = await ReadUntilTypeAsync(session.Stream, codec, SendItemConfirmation.MessageType, cancellationToken);
                    Ensure(resyncFrame.Header.Id == sellerId && resyncFrame.Header.Size == SendItemConfirmation.PacketSize &&
                        SendItemConfirmation.TryParse(resyncFrame, out var resync) && resync is not null &&
                        resync.InventoryType == (short)LegacyNpcSellSourceKind.Carry && resync.Slot == 0 && resync.Item == default,
                        "An empty carry sale rejection did not resend the authoritative empty slot with MSG_SendItem.");
                }
                else
                {
                    var notice = await ReadUntilTypeAsync(session.Stream, codec, ExperienceMessagePanel.MessageType, cancellationToken);
                    var terminator = notice.Payload.Span[..ExperienceMessagePanel.MessageLength].IndexOf((byte)0);
                    Ensure(terminator >= 0, "The success panel text was not null-terminated.");
                    var noticeText = Encoding.Latin1.GetString(notice.Payload.Span[..terminator]);
                    Ensure(notice.Header.Id == sellerId && noticeText == $"Adquiriu {price.Amount} de gold" &&
                        BinaryPrimitives.ReadUInt32LittleEndian(notice.Payload.Span[ExperienceMessagePanel.ColorOffset..]) == ExperienceMessagePanel.Yellow,
                        "Accepted sale did not send the 7.69 yellow net-Gold panel to the seller.");

                    var echo = await ReadUntilTypeAsync(session.Stream, codec, ShopSellMessage.MessageType, cancellationToken);
                    Ensure(echo.Header.Id == sellerId && echo.Header.ClientTick == 0x12345678 && echo.Header.KeywordIndex == 0x37 &&
                        echo.Payload.Span.SequenceEqual(request.ToPayload()),
                        "Accepted sale did not echo the exact 0x037A request after the success panel.");

                    var updateEtc = await ReadUntilTypeAsync(session.Stream, codec, UpdateEtcConfirmationV769.MessageType, cancellationToken);
                    Ensure(BinaryPrimitives.ReadInt32LittleEndian(updateEtc.Payload.Span[UpdateEtcConfirmationV769.CoinOffset..]) == 1_000 + price.Amount,
                        "The final UpdateEtc did not expose the committed Gold balance.");

                    if (sellFromCargo)
                    {
                        var resyncFrame = await ReadUntilTypeAsync(session.Stream, codec, SendItemConfirmation.MessageType, cancellationToken);
                        Ensure(resyncFrame.Header.Id == sellerId &&
                            SendItemConfirmation.TryParse(resyncFrame, out var cargoResync) && cargoResync is not null &&
                            cargoResync.InventoryType == (short)LegacyNpcSellSourceKind.Cargo &&
                            cargoResync.Slot == 0 && cargoResync.Item == default,
                            "The committed Cargo sale did not resync its cleared account-owned slot to the seller.");
                    }
                }

                await server.StandardInput.WriteLineAsync("stop");
                using var shutdownTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await server.WaitForExitAsync(shutdownTimeout.Token);
                gracefulShutdown = server.ExitCode == 0;
                Ensure(gracefulShutdown, $"Isolated NPC sell Host did not drain the connected seller cleanly; exitCode={server.ExitCode}.");
            }

            var persisted = await File.ReadAllBytesAsync(fixture.AccountPath, cancellationToken);
            var mob = persisted.AsSpan(CharacterOffset, LegacyAccountSnapshot.CharacterStride);
            var expectedCoin = expectOutOfViewSignal || expectEmptyCarryResync ? 1_000 : 1_000 + price.Amount;
            var expectedCarry = expectOutOfViewSignal ? item : default;
            Ensure(BinaryPrimitives.ReadInt32LittleEndian(mob[LegacyAccountSnapshot.MobCoinOffset..]) == expectedCoin,
                "Character departure checkpoint persisted an unexpected Gold balance.");
            Ensure(LegacyItem.Read(mob.Slice(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes)) == expectedCarry,
                "Character departure checkpoint persisted an unexpected carry item.");
            var expectedCargo = default(LegacyItem);
            Ensure(LegacyItem.Read(persisted.AsSpan(LegacyAccountSnapshot.CargoOffset, LegacyItem.SizeInBytes)) == expectedCargo,
                "The account Cargo array persisted an unexpected first slot.");
            Ensure(BinaryPrimitives.ReadInt32LittleEndian(persisted.AsSpan(LegacyAccountSnapshot.AccountCoinOffset)) == 246_810,
                "NPC sale changed the separate account bank Coin balance.");

            var hostOutput = await standardOutput;
            Ensure(hostOutput.Contains("Guild treasury shutdown checkpoint completed:", StringComparison.Ordinal),
                "Host shutdown did not run the final configured treasury checkpoint.");
            Console.WriteLine(sellFromCargo
                ? $"NPC_SELL_CARGO_LOOPBACK_SMOKE_PASS item={item.Index} netGold={price.Amount} cargo-resync=sent bank-coin=preserved"
                : expectOutOfViewSignal
                ? $"NPC_SELL_OUT_OF_VIEW_LOOPBACK_SMOKE_PASS type=0x{CloseShopSignal.MessageType:X4} scene={CloseShopSignal.SceneId} item-retained=true"
                : expectEmptyCarryResync
                    ? $"NPC_SELL_EMPTY_CARRY_RESYNC_LOOPBACK_SMOKE_PASS type=0x{SendItemConfirmation.MessageType:X4} slot=0 item=0"
                    : $"NPC_SELL_LOOPBACK_SMOKE_PASS item={item.Index} netGold={price.Amount} response=5000-037A-0337 departure-checkpoint-confirmed");
        }
        catch (Exception error) when (server is not null && standardOutput is not null && standardError is not null)
        {
            if (!server.HasExited)
            {
                server.Kill(entireProcessTree: true);
                await server.WaitForExitAsync(CancellationToken.None);
            }
            throw new InvalidOperationException(
                $"NPC sell loopback smoke failed. Host stdout:\n{await standardOutput}\nHost stderr:\n{await standardError}", error);
        }
        finally
        {
            if (server is not null)
            {
                if (!server.HasExited)
                {
                    try
                    {
                        await server.StandardInput.WriteLineAsync("stop");
                        using var shutdownTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        await server.WaitForExitAsync(shutdownTimeout.Token);
                        gracefulShutdown = server.ExitCode == 0;
                    }
                    catch (OperationCanceledException)
                    {
                        server.Kill(entireProcessTree: true);
                        await server.WaitForExitAsync(CancellationToken.None);
                    }
                }

                server.Dispose();
            }

            EnsureInsideTempRoot(smokeRoot);
            if (Directory.Exists(smokeRoot))
                Directory.Delete(smokeRoot, recursive: true);
        }
    }

    private static async Task<(string GenerationPath, string NpcRoot)> PrepareCityNpcFixturesAsync(
        string smokeRoot,
        string sourceGenerationPath,
        string sourceNpcRoot,
        bool markPerzenAsMerchant,
        CancellationToken cancellationToken)
    {
        // The port's startup hook requests all reference city NPCs. Keeping
        // only generator 3429 makes it the first and only 20,000-series mob.
        var wantedIds = new HashSet<int>([3429]);
        var lines = await File.ReadAllLinesAsync(sourceGenerationPath, Encoding.Latin1, cancellationToken);
        var selectedBlocks = new List<string[]>();
        for (var index = 0; index < lines.Length; index++)
        {
            if (!TryReadNpcGenerationId(lines[index], out var generateId) || !wantedIds.Contains(generateId))
                continue;

            var end = index + 1;
            while (end < lines.Length && !TryReadNpcGenerationId(lines[end], out _))
                end++;
            selectedBlocks.Add(lines[index..end]);
            index = end - 1;
        }

        if (selectedBlocks.Count != wantedIds.Count)
            throw new InvalidDataException($"TMProject NPCGener.txt did not contain city generator row 3429 ({selectedBlocks.Count}/1).");

        // Hold the synthetic Perzen on its source tile while the 200 ms NPC-AI
        // runtime runs. Its route is irrelevant to this wire/commit smoke, and
        // allowing the live route to advance can correctly stale the sale plan.
        var stableMerchantBlock = selectedBlocks[0].ToList();
        var startWaitLine = stableMerchantBlock.FindIndex(static line =>
            line.TrimStart().StartsWith("StartWait:", StringComparison.Ordinal));
        if (startWaitLine >= 0)
            stableMerchantBlock[startWaitLine] = "\tStartWait:\t600";
        else
        {
            var startYLine = stableMerchantBlock.FindIndex(static line =>
                line.TrimStart().StartsWith("StartY:", StringComparison.Ordinal));
            if (startYLine < 0)
                throw new InvalidDataException("TMProject Perzen fixture is missing StartY.");
            stableMerchantBlock.Insert(startYLine + 1, "\tStartWait:\t600");
        }
        selectedBlocks[0] = stableMerchantBlock.ToArray();

        var fixtureNpcRoot = Path.Combine(smokeRoot, "npc");
        Directory.CreateDirectory(fixtureNpcRoot);
        var generationPath = Path.Combine(smokeRoot, "NPCGener.txt");
        var templateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in selectedBlocks.SelectMany(static block => block))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
                continue;
            var key = line[..separator].Trim();
            if (key is not ("Leader" or "Follower"))
                continue;
            var name = line[(separator + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name)
                throw new InvalidDataException($"NPCGener fixture contains an unsafe template name '{name}'.");
            templateNames.Add(name);
        }

        if (templateNames.Count == 0)
            throw new InvalidDataException("TMProject NPCGener fixture did not identify any city NPC templates.");

        await File.WriteAllTextAsync(generationPath, string.Join(Environment.NewLine, selectedBlocks.SelectMany(static block => block)), Encoding.Latin1, cancellationToken);
        foreach (var name in templateNames)
        {
            var source = Path.Combine(sourceNpcRoot, name);
            var bytes = await File.ReadAllBytesAsync(source, cancellationToken);
            if (bytes.Length != LegacyAccountSnapshot.CharacterStride)
                throw new InvalidDataException($"TMProject NPC template '{name}' is not an 816-byte MOB snapshot.");
            if (markPerzenAsMerchant && string.Equals(name, "Perzen_Normal", StringComparison.OrdinalIgnoreCase))
                bytes[LegacyAccountSnapshot.MobMerchantOffset] = 1;
            await File.WriteAllBytesAsync(Path.Combine(fixtureNpcRoot, name), bytes, cancellationToken);
        }

        if (markPerzenAsMerchant && !templateNames.Contains("Perzen_Normal"))
            throw new InvalidDataException("The out-of-view fixture could not select the Perzen merchant template.");

        return (generationPath, fixtureNpcRoot);
    }

    private static bool TryReadNpcGenerationId(string rawLine, out int generateId)
    {
        generateId = 0;
        var line = rawLine.Trim();
        if (!line.StartsWith('#'))
            return false;
        var open = line.IndexOf('[');
        var close = line.IndexOf(']', open + 1);
        return open >= 0 && close > open &&
            int.TryParse(line.AsSpan(open + 1, close - open - 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out generateId);
    }

    private static (LegacyItem Item, LegacyItemDefinition Definition) SelectSaleItem(LegacyItemDataTable itemData)
    {
        for (var index = 1; index < LegacyItemDataTable.MaxItemIndex; index++)
        {
            if (index is 747 or 3193 or 3194 || itemData[index] is not { Price: >= 40, Name.Length: > 0 } definition)
                continue;

            var item = new LegacyItem((short)index, 0, 0, 0, 0, 0, 0);
            if (itemData.GetItemAbility(item, LegacyItemEffect.Volatile) != 1)
                return (item, definition);
        }

        throw new InvalidDataException("ItemList.bin did not contain a saleable positive-price fixture item.");
    }

    private static async Task SeedSellerAsync(
        string accountPath,
        LegacyItem item,
        LegacyNpcSellSourceKind sourceKind,
        bool bypassMerchantViewGate,
        CancellationToken cancellationToken)
    {
        var account = await File.ReadAllBytesAsync(accountPath, cancellationToken);
        var mob = account.AsSpan(CharacterOffset, LegacyAccountSnapshot.CharacterStride);
        mob[LegacyAccountSnapshot.MobClassOffset] = bypassMerchantViewGate ? (byte)3 : (byte)0;
        BinaryPrimitives.WriteUInt32LittleEndian(
            mob[LegacyAccountSnapshot.MobLearnedSkillOffset..], bypassMerchantViewGate ? 0x1000u : 0u);
        var itemOffset = sourceKind == LegacyNpcSellSourceKind.Cargo
            ? LegacyAccountSnapshot.CargoOffset
            : LegacyAccountSnapshot.MobCarryOffset;
        item.Write(account.AsSpan(itemOffset, LegacyItem.SizeInBytes));
        BinaryPrimitives.WriteInt32LittleEndian(account.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), 246_810);
        await File.WriteAllBytesAsync(accountPath, account, cancellationToken);
    }

    private static Process StartServer(
        string checkoutRoot,
        string itemDataPath,
        string npcGenerationPath,
        string npcRoot,
        string accountsRoot,
        string accountReviewRoot,
        int port)
    {
        var serverDll = Path.Combine(checkoutRoot, "src", "WydCdk.Server", "bin", "Debug", "net10.0", "WydCdk.Server.Core.dll");
        if (!File.Exists(serverDll))
            throw new FileNotFoundException("Built WydCdk.Server.Core.dll is missing; build the Server project before this smoke.", serverDll);

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = checkoutRoot,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(serverDll);
        AddArgument(startInfo, "--bind", "127.0.0.1");
        AddArgument(startInfo, "--port", port.ToString(CultureInfo.InvariantCulture));
        AddArgument(startInfo, "--accounts-root", accountsRoot);
        AddArgument(startInfo, "--account-review-root", accountReviewRoot);
        AddArgument(startInfo, "--item-data", itemDataPath);
        AddArgument(startInfo, "--npc-generation", npcGenerationPath);
        AddArgument(startInfo, "--npc-root", npcRoot);
        startInfo.ArgumentList.Add("--city-npcs");
        AddArgument(startInfo, "--server-group", "0");
        return Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the isolated NPC sell Host.");
    }

    private static void AddArgument(ProcessStartInfo startInfo, string name, string value)
    {
        startInfo.ArgumentList.Add(name);
        startInfo.ArgumentList.Add(value);
    }

    private static async Task<DecodedFrame> ReadUntilTypeAsync(NetworkStream stream, LegacyFrameCodec codec, ushort expectedType, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        for (var index = 0; index < 128; index++)
        {
            var headerBytes = new byte[PacketHeader.SizeInBytes];
            await stream.ReadExactlyAsync(headerBytes, timeout.Token);
            var header = PacketHeader.Read(headerBytes);
            Ensure(header.Size >= PacketHeader.SizeInBytes && header.Size <= PacketHeader.MaxPacketSize,
                $"NPC sell smoke received invalid frame size {header.Size}.");
            var frameBytes = new byte[header.Size];
            headerBytes.CopyTo(frameBytes, 0);
            await stream.ReadExactlyAsync(frameBytes.AsMemory(PacketHeader.SizeInBytes), timeout.Token);
            var frame = codec.Decode(frameBytes);
            Ensure(frame.IsChecksumValid, "NPC sell smoke received an invalid frame checksum.");
            if (frame.Header.Type == expectedType)
                return frame;
        }

        throw new InvalidDataException($"Too many unrelated frames while waiting for type=0x{expectedType:X4}.");
    }

    private static int ReserveLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task WaitForServerAsync(Process process, int port, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        SocketException? lastSocketError = null;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited)
                throw new InvalidOperationException($"Isolated NPC sell Host exited during startup with code {process.ExitCode}.");
            using var probe = new TcpClient(AddressFamily.InterNetwork);
            try
            {
                await probe.ConnectAsync(IPAddress.Loopback, port, cancellationToken);
                return;
            }
            catch (SocketException error)
            {
                lastSocketError = error;
                await Task.Delay(100, cancellationToken);
            }
        }

        throw new TimeoutException(
            $"Isolated NPC sell Host did not bind its loopback listener within 20 seconds; last socket error={lastSocketError?.SocketErrorCode} ({lastSocketError?.NativeErrorCode}): {lastSocketError?.Message}");
    }

    private static string FindCheckoutRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WydCdk.Engine.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the Server checkout root from the E2E output directory.");
    }

    private static void EnsureInsideTempRoot(string path)
    {
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        var fullPath = Path.GetFullPath(path);
        var prefix = Path.EndsInDirectorySeparator(tempRoot) ? tempRoot : tempRoot + Path.DirectorySeparatorChar;
        Ensure(fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase),
            $"Refusing to remove NPC sell smoke data outside the temporary directory: {fullPath}.");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

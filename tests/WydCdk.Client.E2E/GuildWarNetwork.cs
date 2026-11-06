using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using WydCdk.Protocol;

internal static class GuildWarNetwork
{
    private const ushort GuildNoticeType = MessagePanelConfirmation.MessageType;
    private const ushort SendWarInfoType = SendWarInfoConfirmation.MessageType;

    public static async Task RunAsync(string itemDataPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(itemDataPath))
            throw new FileNotFoundException("GuildWar smoke ItemList.bin was not found.", itemDataPath);

        var smokeRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"wyd-cdk-guild-war-{Guid.NewGuid():N}"));
        EnsureInsideTempRoot(smokeRoot);
        Directory.CreateDirectory(smokeRoot);
        Process? server = null;
        try
        {
            var port = ReserveLoopbackPort();
            var actor = await GuildWarFixtureFactory.CreateAsync(smokeRoot, port, 11, "GWLEADERA", "WARPASSA", "GWLEADERA", 7, 9, cancellationToken);
            var peer = await GuildWarFixtureFactory.CreateAsync(smokeRoot, port, 12, "GWLEADERB", "WARPASSB", "GWLEADERB", 8, 9, cancellationToken);
            var guildDataPath = Path.Combine(smokeRoot, "Guild.txt");
            var guildNamesPath = Path.Combine(smokeRoot, "Guilds.txt");
            await File.WriteAllTextAsync(guildDataPath, CreateGuildData(clan: 37), Encoding.ASCII, cancellationToken);
            await File.WriteAllTextAsync(guildNamesPath, "0 0 7 ALPHA_GUILD\r\n0 0 8 BETA_GUILD\r\n", Encoding.ASCII, cancellationToken);

            server = StartServer(smokeRoot, itemDataPath, guildDataPath, guildNamesPath, port);
            await WaitForServerAsync(server, port, cancellationToken);

            var codec = LegacyFrameCodec.CreateDefault();
            await using var actorSession = await E2eSession.ConnectAndLoginAsync(actor.Options, actor.Header, codec, "GUILD_WAR_ACTOR_");
            await using var peerSession = await E2eSession.ConnectAndLoginAsync(peer.Options, peer.Header, codec, "GUILD_WAR_PEER_");

            await actorSession.Stream.WriteAsync(new GuildWarRequest(8, 7).ToFrame(codec, 100, 30, actor.Options.ConnectionId), cancellationToken);
            await actorSession.Stream.WriteAsync(new GuildWarRequest(7, 8).ToFrame(codec, 101, 31, actor.Options.ConnectionId), cancellationToken);
            var declaration = "Guilda ALPHA GUILD declarou guerra para a Guilda BETA GUILD.";
            await AssertNoticeAsync(actorSession.Stream, codec, declaration, "declaration to requester");
            await AssertNoticeAsync(peerSession.Stream, codec, declaration, "declaration to target");
            Console.WriteLine("GUILD_WAR_ONE_SIDED_DECLARATION_PASS spoofed-guild-request-did-not-mutate-state");

            await peerSession.Stream.WriteAsync(new GuildWarRequest(8, 7).ToFrame(codec, 102, 32, peer.Options.ConnectionId), cancellationToken);
            var started = "Guilda BETA GUILD aceitou o desafio da Guilda ALPHA GUILD.";
            await AssertNoticeAsync(actorSession.Stream, codec, started, "global started notice to first guild");
            await AssertNoticeAsync(peerSession.Stream, codec, started, "global started notice to second guild");
            await AssertWarInfoAsync(actorSession.Stream, codec, expectedOpponentGuild: 8, expectedClan: 37, "first guild SendWarInfo");
            await AssertWarInfoAsync(peerSession.Stream, codec, expectedOpponentGuild: 7, expectedClan: 37, "second guild SendWarInfo");
            Console.WriteLine("GUILD_WAR_RECIPROCAL_ACCEPTANCE_PASS notice-and-send-war-info");

            await actorSession.Stream.WriteAsync(new GuildAllyRequest(7, 8).ToFrame(codec, 103, 33, actor.Options.ConnectionId), cancellationToken);
            var allyDeclared = "Guilda ALPHA GUILD solicitou aliança com Guilda BETA GUILD.";
            await AssertNoticeAsync(actorSession.Stream, codec, allyDeclared, "alliance declaration to requester");
            await AssertNoticeAsync(peerSession.Stream, codec, allyDeclared, "alliance declaration to ally");
            await AssertWarInfoAsync(actorSession.Stream, codec, expectedOpponentGuild: 8, expectedClan: 37,
                expectedAllyGuild: 8, label: "requester SendWarInfo with alliance");
            Console.WriteLine("GUILD_ALLY_DIRECTED_DECLARATION_PASS global-notice-and-send-war-info");

            await actorSession.Stream.WriteAsync(new GuildAllyRequest(7, 9).ToFrame(codec, 104, 34, actor.Options.ConnectionId), cancellationToken);
            var allyCancelled = "Guilda ALPHA GUILD finalizou aliança com Guilda BETA GUILD.";
            await AssertNoticeAsync(actorSession.Stream, codec, allyCancelled, "alliance cancellation to requester");
            await AssertNoticeAsync(peerSession.Stream, codec, allyCancelled, "alliance cancellation to ally");
            await AssertWarInfoAsync(actorSession.Stream, codec, expectedOpponentGuild: 8, expectedClan: 37,
                expectedAllyGuild: 0, label: "requester SendWarInfo after cancellation");
            Console.WriteLine("GUILD_ALLY_TOGGLE_CANCELLATION_PASS retained-war-state");

            await actorSession.LogoutAsync();
            await peerSession.LogoutAsync();
            Console.WriteLine("GUILD_WAR_LOOPBACK_SMOKE_PASS host=127.0.0.1 external-database=none fixtures=generated-only");
        }
        finally
        {
            if (server is not null)
            {
                try
                {
                    if (!server.HasExited)
                    {
                        server.Kill(entireProcessTree: true);
                        await server.WaitForExitAsync(CancellationToken.None);
                    }
                }
                finally
                {
                    server.Dispose();
                }
            }

            EnsureInsideTempRoot(smokeRoot);
            if (Directory.Exists(smokeRoot))
                Directory.Delete(smokeRoot, recursive: true);
        }
    }

    private static Process StartServer(string smokeRoot, string itemDataPath, string guildDataPath, string guildNamesPath, int port)
    {
        var checkoutRoot = FindCheckoutRoot();
        var serverDll = Path.Combine(checkoutRoot, "src", "WydCdk.Server", "bin", "Debug", "net10.0", "WydCdk.Server.Core.dll");
        if (!File.Exists(serverDll))
            throw new FileNotFoundException("Built WydCdk.Server.dll is missing; the E2E project must build the Server project reference first.", serverDll);

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = checkoutRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(serverDll);
        AddArgument(startInfo, "--bind", "127.0.0.1");
        AddArgument(startInfo, "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AddArgument(startInfo, "--accounts-root", Path.Combine(smokeRoot, "accounts"));
        AddArgument(startInfo, "--account-review-root", Path.Combine(smokeRoot, "review"));
        AddArgument(startInfo, "--item-data", itemDataPath);
        AddArgument(startInfo, "--guild-data", guildDataPath);
        AddArgument(startInfo, "--guild-names", guildNamesPath);
        AddArgument(startInfo, "--server-group", "0");

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the local GuildWar Server child process.");
        _ = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();
        return process;
    }

    private static void AddArgument(ProcessStartInfo startInfo, string name, string value)
    {
        startInfo.ArgumentList.Add(name);
        startInfo.ArgumentList.Add(value);
    }

    private static async Task WaitForServerAsync(Process process, int port, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited)
                throw new InvalidOperationException($"GuildWar Server exited during loopback startup with code {process.ExitCode}.");

            using var probe = new TcpClient(AddressFamily.InterNetwork);
            try
            {
                await probe.ConnectAsync(IPAddress.Loopback, port, cancellationToken);
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(100, cancellationToken);
            }
        }

        throw new TimeoutException("GuildWar Server did not bind the loopback port within 10 seconds.");
    }

    private static async Task AssertNoticeAsync(NetworkStream stream, LegacyFrameCodec codec, string expected, string label)
    {
        var frame = await ReadUntilTypeAsync(stream, codec, GuildNoticeType, TimeSpan.FromSeconds(8), label);
        Ensure(frame.Header.Id == 0 && frame.Header.Size == MessagePanelConfirmation.PacketSize,
            $"{label} did not use the 136-byte MSG_MessagePanel frame.");
        var terminator = frame.Payload.Span.IndexOf((byte)0);
        var actualBytes = terminator < 0 ? frame.Payload.Span : frame.Payload.Span[..terminator];
        Ensure(actualBytes.SequenceEqual(Encoding.Latin1.GetBytes(expected)),
            $"{label} text differed from Language.txt. Actual='{Encoding.Latin1.GetString(actualBytes)}'.");
    }

    private static async Task AssertWarInfoAsync(NetworkStream stream, LegacyFrameCodec codec, int expectedOpponentGuild, int expectedClan, string label, int expectedAllyGuild = 0)
    {
        var frame = await ReadUntilTypeAsync(stream, codec, SendWarInfoType, TimeSpan.FromSeconds(8), label);
        Ensure(frame.Header.Id == SendWarInfoConfirmation.SceneId && frame.Header.Size == SendWarInfoConfirmation.PacketSize,
            $"{label} did not use the 24-byte scene signal.");
        Ensure(BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span) == expectedOpponentGuild &&
            BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[4..]) == expectedClan &&
            BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[8..]) == expectedAllyGuild,
            $"{label} carried unexpected opponent, clan, or ally values.");
    }

    private static async Task<DecodedFrame> ReadUntilTypeAsync(NetworkStream stream, LegacyFrameCodec codec, ushort expectedType, TimeSpan timeout, string label)
    {
        var deadline = DateTime.UtcNow + timeout;
        for (var index = 0; index < 32; index++)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                throw new TimeoutException($"Timed out waiting for {label} type=0x{expectedType:X4}.");
            var frame = await ReadFrameAsync(stream, codec, remaining, label);
            Ensure(frame.IsChecksumValid, $"{label} received a frame with invalid checksum.");
            if (frame.Header.Type == expectedType)
                return frame;
        }

        throw new InvalidDataException($"Too many unrelated frames while waiting for {label} type=0x{expectedType:X4}.");
    }

    private static async Task<DecodedFrame> ReadFrameAsync(NetworkStream stream, LegacyFrameCodec codec, TimeSpan timeout, string label)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        var headerBytes = new byte[PacketHeader.SizeInBytes];
        try
        {
            await stream.ReadExactlyAsync(headerBytes, cancellation.Token);
            var header = PacketHeader.Read(headerBytes);
            if (header.Size < PacketHeader.SizeInBytes || header.Size > PacketHeader.MaxPacketSize)
                throw new InvalidDataException($"{label} received invalid frame size {header.Size}.");
            var frame = new byte[header.Size];
            headerBytes.CopyTo(frame, 0);
            await stream.ReadExactlyAsync(frame.AsMemory(PacketHeader.SizeInBytes), cancellation.Token);
            return codec.Decode(frame);
        }
        catch (OperationCanceledException exception) when (cancellation.IsCancellationRequested)
        {
            throw new TimeoutException($"Timed out waiting for {label}.", exception);
        }
    }

    private static int ReserveLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string FindCheckoutRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WydCdk.Engine.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the WYD CDK Server checkout root from the E2E output directory.");
    }

    private static string CreateGuildData(int clan)
    {
        var values = new List<int> { 0 };
        values.AddRange(Enumerable.Repeat(0, 5)); // GuildZone.ChargeGuild
        values.AddRange(Enumerable.Repeat(0, 5)); // GuildZone.ChallengerGuild
        values.AddRange(Enumerable.Repeat(10, 5)); // GuildZone.CityTax
        values.AddRange([0, 0, 0, 0, clan]); // GuildZone.Clan, zone index 4
        values.AddRange(Enumerable.Repeat(0, 5)); // GuildZone.Victory
        return string.Join(' ', values);
    }

    private static void EnsureInsideTempRoot(string path)
    {
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        var fullPath = Path.GetFullPath(path);
        var prefix = Path.EndsInDirectorySeparator(tempRoot) ? tempRoot : tempRoot + Path.DirectorySeparatorChar;
        Ensure(fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(fullPath).StartsWith("wyd-cdk-guild-war-", StringComparison.Ordinal),
            $"Refusing GuildWar smoke cleanup outside its generated temp root: {fullPath}.");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using WydCdk.Protocol;

internal static class LegacyCharacterCheckpointNetwork
{
    private const int AccountFileLength = 7_945;
    private const int CharactersOffset = 216;
    private const int CharacterStride = 816;
    private const int MobSavedPositionXOffset = 40;
    private const int MobSavedPositionYOffset = 42;
    private const string SidecarRootName = ".wyd-client-equipment-v1";
    private const int SidecarHeaderLength = 12;
    private const int EquipmentBlobLength = 144;

    public static async Task RunAsync(string itemDataPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(itemDataPath))
            throw new FileNotFoundException("Character checkpoint smoke ItemList.bin was not found.", itemDataPath);

        var smokeRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"wyd-cdk-character-checkpoint-{Guid.NewGuid():N}"));
        EnsureInsideTempRoot(smokeRoot);
        Directory.CreateDirectory(smokeRoot);
        Process? server = null;
        var gracefulShutdown = false;
        try
        {
            var port = ReserveLoopbackPort();
            var fixture = await GuildWarFixtureFactory.CreateAsync(
                smokeRoot,
                port,
                connectionId: 11,
                accountName: "CHECKPOINT",
                password: "CHECKPASS",
                characterName: "CHECKPOINT_HERO",
                guildId: 7,
                guildLevel: 0,
                cancellationToken);
            server = StartServer(smokeRoot, itemDataPath, port);
            await WaitForServerAsync(server, port, cancellationToken);

            var codec = LegacyFrameCodec.CreateDefault();
            short targetX = 0;
            short targetY = 0;
            var sidecarPath = Path.Combine(
                smokeRoot,
                "accounts",
                SidecarRootName,
                fixture.Header.AccountName[..1],
                fixture.Header.AccountName,
                "0.bin");

            byte[] checkpointSidecar;
            await using (var session = await E2eSession.ConnectAndLoginAsync(fixture.Options, fixture.Header, codec, "CHARACTER_CHECKPOINT_"))
            {
                var loginX = BinaryPrimitives.ReadInt16LittleEndian(session.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.PosXOffset..]);
                var loginY = BinaryPrimitives.ReadInt16LittleEndian(session.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.PosYOffset..]);
                targetX = checked((short)(loginX + 1));
                targetY = loginY;

                var action = new ActionRequest(loginX, loginY, 0, 2, new byte[24], targetX, targetY);
                await session.Stream.WriteAsync(action.ToFrame(codec, unchecked((uint)Environment.TickCount64), 41, fixture.Options.ConnectionId), cancellationToken);
                var actionReply = await ReadUntilTypeAsync(session.Stream, codec, ActionRequest.MessageType, TimeSpan.FromSeconds(8), cancellationToken);
                Ensure(ActionRequest.TryParse(actionReply, out var accepted) && accepted is not null &&
                    accepted.TargetX == targetX && accepted.TargetY == targetY,
                    "Host did not relay the requested one-tile movement.");

                await WaitForCheckpointAsync(fixture.AccountPath, sidecarPath, targetX, targetY, cancellationToken);
                checkpointSidecar = await File.ReadAllBytesAsync(sidecarPath, cancellationToken);
                ValidateSidecar(checkpointSidecar);
                await session.LogoutAsync();
            }

            await using (var reconnected = await E2eSession.ConnectAndLoginAsync(fixture.Options with { ConnectionId = 12 }, fixture.Header, codec, "CHARACTER_CHECKPOINT_RELOGIN_"))
            {
                Ensure(reconnected.CharacterLoginFrame.Header.Type == CharacterLoginConfirmationV769.MessageType,
                    "Reconnect did not complete character login after the checkpoint.");
                var selectionOffset = AccountLoginConfirmationV769.SelectionOffset;
                var selectedX = BinaryPrimitives.ReadUInt16LittleEndian(
                    reconnected.AccountLoginFrame.Payload.Span.Slice(selectionOffset + CharacterSelectionV769.HomeTownXOffset, sizeof(ushort)));
                var selectedY = BinaryPrimitives.ReadUInt16LittleEndian(
                    reconnected.AccountLoginFrame.Payload.Span.Slice(selectionOffset + CharacterSelectionV769.HomeTownYOffset, sizeof(ushort)));
                Ensure(selectedX == targetX && selectedY == targetY,
                    $"Reconnect selection did not load checkpointed MOB state ({targetX},{targetY}); got ({selectedX},{selectedY}).");
                await reconnected.LogoutAsync();
            }

            var reloadedSidecar = await File.ReadAllBytesAsync(sidecarPath, cancellationToken);
            ValidateSidecar(reloadedSidecar);
            Ensure(reloadedSidecar.SequenceEqual(checkpointSidecar), "The 18-slot equipment sidecar changed across relogin.");
            Console.WriteLine("CHARACTER_CHECKPOINT_LOOPBACK_SMOKE_PASS periodic-position-persisted equipment-sidecar-18-slots relogin-confirmed");

            await server.StandardInput.WriteLineAsync("stop");
            await server.WaitForExitAsync(cancellationToken);
            gracefulShutdown = server.ExitCode == 0;
            Ensure(gracefulShutdown, $"Isolated Host did not exit cleanly; exitCode={server.ExitCode}.");
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

    private static Process StartServer(string smokeRoot, string itemDataPath, int port)
    {
        var checkoutRoot = FindCheckoutRoot();
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
        AddArgument(startInfo, "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AddArgument(startInfo, "--accounts-root", Path.Combine(smokeRoot, "accounts"));
        AddArgument(startInfo, "--account-review-root", Path.Combine(smokeRoot, "review"));
        AddArgument(startInfo, "--item-data", itemDataPath);
        AddArgument(startInfo, "--server-group", "0");

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the isolated character checkpoint Host.");
        _ = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();
        return process;
    }

    private static async Task WaitForCheckpointAsync(
        string accountPath,
        string sidecarPath,
        short expectedX,
        short expectedY,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var account = await File.ReadAllBytesAsync(accountPath, cancellationToken);
                if (account.Length == AccountFileLength &&
                    BinaryPrimitives.ReadInt16LittleEndian(account.AsSpan(CharactersOffset + MobSavedPositionXOffset)) == expectedX &&
                    BinaryPrimitives.ReadInt16LittleEndian(account.AsSpan(CharactersOffset + MobSavedPositionYOffset)) == expectedY &&
                    File.Exists(sidecarPath))
                    return;
            }
            catch (IOException)
            {
                // The file adapter uses atomic replacement; retry a transient read/replace boundary.
            }

            await Task.Delay(100, cancellationToken);
        }

        throw new TimeoutException("The live Host did not persist the moved character and equipment sidecar within 20 seconds.");
    }

    private static async Task<DecodedFrame> ReadUntilTypeAsync(
        NetworkStream stream,
        LegacyFrameCodec codec,
        ushort expectedType,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        for (var index = 0; index < 32; index++)
        {
            var headerBytes = new byte[PacketHeader.SizeInBytes];
            await stream.ReadExactlyAsync(headerBytes, timeoutSource.Token);
            var header = PacketHeader.Read(headerBytes);
            Ensure(header.Size >= PacketHeader.SizeInBytes && header.Size <= PacketHeader.MaxPacketSize,
                $"Checkpoint smoke received invalid frame size {header.Size}.");
            var bytes = new byte[header.Size];
            headerBytes.CopyTo(bytes, 0);
            await stream.ReadExactlyAsync(bytes.AsMemory(PacketHeader.SizeInBytes), timeoutSource.Token);
            var frame = codec.Decode(bytes);
            Ensure(frame.IsChecksumValid, "Checkpoint smoke received an invalid frame checksum.");
            if (frame.Header.Type == expectedType)
                return frame;
        }

        throw new InvalidDataException($"Too many unrelated frames while waiting for type=0x{expectedType:X4}.");
    }

    private static void ValidateSidecar(byte[] bytes)
    {
        Ensure(bytes.Length == SidecarHeaderLength + EquipmentBlobLength, "Equipment sidecar does not contain all eighteen client slots.");
        Ensure(bytes.AsSpan(0, 8).SequenceEqual(Encoding.ASCII.GetBytes("WYDEQ769")) &&
            BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8, sizeof(int))) == 1,
            "Equipment sidecar has an unexpected version header.");
    }

    private static int ReserveLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task WaitForServerAsync(Process process, int port, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited)
                throw new InvalidOperationException($"Isolated checkpoint Host exited during startup with code {process.ExitCode}.");
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

        throw new TimeoutException("Isolated checkpoint Host did not bind its loopback listener within 10 seconds.");
    }

    private static string FindCheckoutRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WydCdk.Engine.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the WYD CDK Server checkout root from the E2E output directory.");
    }

    private static void AddArgument(ProcessStartInfo startInfo, string name, string value)
    {
        startInfo.ArgumentList.Add(name);
        startInfo.ArgumentList.Add(value);
    }

    private static void EnsureInsideTempRoot(string path)
    {
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        var fullPath = Path.GetFullPath(path);
        var prefix = Path.EndsInDirectorySeparator(tempRoot) ? tempRoot : tempRoot + Path.DirectorySeparatorChar;
        Ensure(fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(fullPath).StartsWith("wyd-cdk-character-checkpoint-", StringComparison.Ordinal),
            "Refusing checkpoint smoke cleanup outside its generated temporary root.");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

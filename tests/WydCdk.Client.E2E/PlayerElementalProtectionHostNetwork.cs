using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

/// <summary>Authenticated loopback coverage of the player Type 25 attack through the real Server Host.</summary>
internal static class PlayerElementalProtectionHostNetwork
{
    private const string AccountName = "TYPE25TEST";
    private const string Password = "TYPE25E2E";
    private const string CharacterName = "PROTECT25";
    private const ushort FixtureConnectionId = 11;
    private const int CharacterClass = 2;
    private const int SkillId = 53;
    private const int StartingHp = 1_000;
    private const int StartingMp = 1_000;
    private const int StartingAc = 100;
    private const int Special2 = 50;
    private const int ExpectedMpAfterCast = 869;

    public static async Task RunAsync(string itemDataPath, string skillDataPath, CancellationToken cancellationToken = default)
    {
        EnsureFile(itemDataPath, "7.69 ItemList.bin");
        EnsureFile(skillDataPath, "7.69 SkillData.csv");

        var smokeRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"wyd-cdk-type25-auth-{Guid.NewGuid():N}"));
        EnsureInsideTempRoot(smokeRoot);
        Directory.CreateDirectory(smokeRoot);
        ChildServer? server = null;
        try
        {
            var port = ReserveLoopbackPort();
            var fixture = await GuildWarFixtureFactory.CreateAsync(
                smokeRoot, port, FixtureConnectionId, AccountName, Password, CharacterName, 1, 0, cancellationToken);
            await SeedType25CharacterAsync(fixture.AccountPath, cancellationToken);

            server = StartServer(smokeRoot, itemDataPath, skillDataPath, port);
            await WaitForServerAsync(server.Process, port, cancellationToken);

            var codec = LegacyFrameCodec.CreateDefault();
            await using var session = await E2eSession.ConnectAndLoginAsync(
                fixture.Options, fixture.Header, codec, "TYPE25_AUTHENTICATED_ACTOR_");
            var actorId = BinaryPrimitives.ReadUInt16LittleEndian(
                session.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);
            Ensure(actorId > 0, "Authenticated Type 25 fixture received an invalid character ID.");
            Ensure(session.CharacterLoginFrame.Payload.Span[
                    CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.ClassOffset] == CharacterClass,
                "Authenticated Type 25 fixture logged in with the wrong class.");
            var loginState = session.CharacterLoginFrame.Payload.Span;
            var loggedInSpecial2 = BinaryPrimitives.ReadUInt16LittleEndian(loginState.Slice(
                CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + 42, sizeof(ushort)));
            Ensure(loggedInSpecial2 == Special2,
                $"Authenticated Type 25 fixture logged in with Special2={loggedInSpecial2}; expected {Special2}.");

            var positionX = BinaryPrimitives.ReadInt16LittleEndian(session.CharacterLoginFrame.Payload.Span);
            var positionY = BinaryPrimitives.ReadInt16LittleEndian(session.CharacterLoginFrame.Payload.Span[2..]);
            await DrainUntilQuietAsync(session.Stream, codec, TimeSpan.FromMilliseconds(350), cancellationToken);

            var tick = unchecked((uint)Environment.TickCount64);
            var attackFrame = BuildSelfCast(codec, actorId, positionX, positionY, tick);
            await session.Stream.WriteAsync(attackFrame, cancellationToken);
            var responses = await ReadFramesUntilQuietAsync(
                session.Stream, codec, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(450), cancellationToken);

            AssertType25Responses(responses, actorId);
            await session.LogoutAsync();
            Console.WriteLine("HOST_AUTHENTICATED_TYPE25_PASS auth=account-file host=127.0.0.1 skill=53 score-before-affect=true generated-fixture=true external-database=none");
        }
        catch (Exception error)
        {
            var logs = server is null ? "" : await server.ReadLogsAsync();
            throw new InvalidOperationException(
                $"Authenticated Type 25 loopback smoke failed. Host output:\n{logs}", error);
        }
        finally
        {
            if (server is not null)
                await server.StopAsync();
            EnsureInsideTempRoot(smokeRoot);
            if (Directory.Exists(smokeRoot))
                Directory.Delete(smokeRoot, recursive: true);
        }
    }

    public static async Task PrepareVisualFixtureAsync(string fixtureRoot, CancellationToken cancellationToken = default)
    {
        var checkoutRoot = FindCheckoutRoot();
        EnsureInsideBuildRoot(checkoutRoot, fixtureRoot);
        if (Directory.Exists(fixtureRoot) && Directory.EnumerateFileSystemEntries(fixtureRoot).Any())
            throw new IOException($"Refusing to overwrite non-empty Type 25 visual fixture directory: {fixtureRoot}");

        Directory.CreateDirectory(fixtureRoot);
        var fixture = await GuildWarFixtureFactory.CreateAsync(
            fixtureRoot, 8281, FixtureConnectionId, AccountName, Password, CharacterName, 1, 0, cancellationToken);
        await SeedType25CharacterAsync(fixture.AccountPath, cancellationToken);
        Directory.CreateDirectory(Path.Combine(fixtureRoot, "review"));
        Console.WriteLine($"TYPE25_VISUAL_FIXTURE_READY root={fixtureRoot} account={AccountName} character={CharacterName} host=127.0.0.1:8281 credentials=local-test-only");
    }

    private static async Task SeedType25CharacterAsync(string accountPath, CancellationToken cancellationToken)
    {
        var account = await File.ReadAllBytesAsync(accountPath, cancellationToken);
        const int characterOffset = 216;
        var mob = account.AsSpan(characterOffset, LegacyAccountSnapshot.CharacterStride);
        mob[LegacyAccountSnapshot.MobClassOffset] = CharacterClass;
        BinaryPrimitives.WriteInt16LittleEndian(
            mob[LegacyAccountSnapshot.MobEquipmentOffset..], 32); // TMProject derives class 2 from this face index.
        AssertClientClassMatchesFace(mob);

        var score = LegacyScore.Read(mob[LegacyAccountSnapshot.MobCurrentScoreOffset..]) with
        {
            Ac = StartingAc,
            Hp = StartingHp,
            Mp = StartingMp,
            Special2 = Special2,
        };
        score.Write(mob[LegacyAccountSnapshot.MobBaseScoreOffset..]);
        score.Write(mob[LegacyAccountSnapshot.MobCurrentScoreOffset..]);

        var learnedSkillOffset = LegacyAccountSnapshot.MobLearnedSkillOffset;
        BinaryPrimitives.WriteUInt32LittleEndian(
            mob[learnedSkillOffset..], 1u << (SkillId % 24));
        mob.Slice(LegacyAccountSnapshot.MobSkillBarOffset, 4).Clear();
        mob[LegacyAccountSnapshot.MobSkillBarOffset] = SkillId;

        var mobExtra = account.AsSpan(LegacyAccountSnapshot.MobExtraOffset, LegacyAccountSnapshot.MobExtraStride);
        BinaryPrimitives.WriteInt16LittleEndian(mobExtra[LegacyAccountSnapshot.MobExtraClassMasterOffset..],
            LegacyAccountSnapshot.ClassMasterMortal);
        BinaryPrimitives.WriteUInt32LittleEndian(
            mobExtra[LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset..], 1u << (SkillId % 24));
        await File.WriteAllBytesAsync(accountPath, account, cancellationToken);
    }

    private static void AssertClientClassMatchesFace(ReadOnlySpan<byte> mob)
    {
        var serverClass = mob[LegacyAccountSnapshot.MobClassOffset];
        var face = BinaryPrimitives.ReadInt16LittleEndian(mob[LegacyAccountSnapshot.MobEquipmentOffset..]);
        var clientClass = face >= 40
            ? serverClass
            : face == 32
                ? 2
                : face % 10 < 6
                    ? face / 10
                    : face % 10 - 6;
        Ensure(clientClass == serverClass,
            $"Type 25 visual fixture class mismatch: Host={serverClass}, client={clientClass}, Equip[0].sIndex={face}.");
    }

    private static byte[] BuildSelfCast(LegacyFrameCodec codec, ushort actorId, short x, short y, uint tick)
    {
        var payload = new byte[AttackRequest.OneTargetPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(22), x);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(24), y);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(26), x);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(28), y);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), actorId);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), SkillId);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(AttackRequest.DamageArrayOffset), actorId);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(AttackRequest.DamageArrayOffset + 4), -1);
        return codec.Encode(AttackRequest.OneTargetMessageType, actorId, tick, payload, keywordIndex: 53);
    }

    private static void AssertType25Responses(IReadOnlyList<DecodedFrame> frames, ushort actorId)
    {
        var scoreIndex = IndexOf(frames, frame =>
            frame.Header.Type == UpdateScoreConfirmationV769.MessageType && frame.Header.Id == actorId &&
            frame.Header.Size == UpdateScoreConfirmationV769.PacketSize && frame.IsChecksumValid &&
            HasAffectType(frame, 25));
        Ensure(scoreIndex >= 0,
            $"Authenticated Host did not send the caster's Type 25 UpdateScore. Received frames: {FormatFrames(frames)}");

        var score = frames[scoreIndex];
        var ac = BinaryPrimitives.ReadInt32LittleEndian(score.Payload.Span[4..]);
        var hp = BinaryPrimitives.ReadInt32LittleEndian(score.Payload.Span[24..]);
        var mp = BinaryPrimitives.ReadInt32LittleEndian(score.Payload.Span[28..]);
        var resist = score.Payload.Span.Slice(UpdateScoreConfirmationV769.ResistOffset, 4);
        Ensure(ac > StartingAc && resist.SequenceEqual(new byte[] { 2, 2, 2, 2 }),
            $"Authenticated Type 25 UpdateScore has unexpected AC/resist: AC={ac}, resist={Convert.ToHexString(resist)}.");
        Ensure(hp == StartingHp && mp == ExpectedMpAfterCast,
            $"Authenticated Type 25 UpdateScore has unexpected resources: HP={hp}, MP={mp}.");

        var affectIndex = IndexOf(frames, frame =>
            frame.Header.Type == UpdateAffectConfirmationV769.MessageType && frame.Header.Id == actorId &&
            frame.Header.Size == UpdateAffectConfirmationV769.PacketSize && frame.IsChecksumValid &&
            frame.Payload.Span[0] == 25);
        Ensure(affectIndex > scoreIndex, "Authenticated Host did not send Type 25 UpdateAffect after UpdateScore.");
        var affect = frames[affectIndex];
        Ensure(affect.Payload.Span[1] == Special2 &&
            BinaryPrimitives.ReadInt16LittleEndian(affect.Payload.Span[2..]) == 10 &&
            BinaryPrimitives.ReadInt32LittleEndian(affect.Payload.Span[4..]) == 24,
            "Authenticated Type 25 UpdateAffect did not contain the seeded level, Special1, and 24-tick lifetime.");

        var attackIndex = IndexOf(frames, frame => frame.Header.Type == AttackRequest.OneTargetMessageType &&
            frame.Header.Id == AttackConfirmation.SceneId && frame.Header.Size == AttackRequest.OneTargetPacketSize && frame.IsChecksumValid &&
            BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[30..]) == actorId &&
            BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[44..]) == SkillId);
        Ensure(attackIndex >= 0, "Authenticated Host did not relay the successful skill AttackOne.");
    }

    private static bool HasAffectType(DecodedFrame frame, byte type)
    {
        for (var index = 0; index < UpdateScoreConfirmationV769.AffectCount; index++)
        {
            var affect = BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span.Slice(
                UpdateScoreConfirmationV769.AffectOffset + (index * sizeof(ushort)), sizeof(ushort)));
            if ((affect >> 8) == type)
                return true;
        }
        return false;
    }

    private static int IndexOf(IReadOnlyList<DecodedFrame> frames, Func<DecodedFrame, bool> predicate)
    {
        for (var index = 0; index < frames.Count; index++)
            if (predicate(frames[index]))
                return index;
        return -1;
    }

    private static string FormatFrames(IReadOnlyList<DecodedFrame> frames) =>
        string.Join(", ", frames.Select(frame =>
        {
            var marker = frame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                frame.Payload.Length >= UpdateScoreConfirmationV769.AffectOffset + (26 * sizeof(ushort))
                    ? $" hasType25={HasAffectType(frame, 25)}"
                    : frame.Header.Type == UpdateAffectConfirmationV769.MessageType && frame.Payload.Length > 0
                        ? $" affectType={frame.Payload.Span[0]}"
                        : string.Empty;
            return $"0x{frame.Header.Type:X4}/id={frame.Header.Id}/size={frame.Header.Size}{marker}";
        }));

    private static async Task DrainUntilQuietAsync(
        NetworkStream stream, LegacyFrameCodec codec, TimeSpan quietPeriod, CancellationToken cancellationToken)
    {
        _ = await ReadFramesUntilQuietAsync(stream, codec, TimeSpan.FromSeconds(2), quietPeriod, cancellationToken);
    }

    private static async Task<List<DecodedFrame>> ReadFramesUntilQuietAsync(
        NetworkStream stream,
        LegacyFrameCodec codec,
        TimeSpan overallTimeout,
        TimeSpan quietPeriod,
        CancellationToken cancellationToken)
    {
        var frames = new List<DecodedFrame>();
        var deadline = DateTime.UtcNow + overallTimeout;
        while (frames.Count < 32)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;
            var frame = await ReadFrameOrQuietAsync(stream, codec, remaining < quietPeriod ? remaining : quietPeriod, cancellationToken);
            if (frame is null)
                return frames;
            frames.Add(frame.Value);
        }
        Ensure(frames.Count < 32, "Authenticated Type 25 smoke received too many frames without reaching a quiet interval.");
        return frames;
    }

    private static async Task<DecodedFrame?> ReadFrameOrQuietAsync(
        NetworkStream stream, LegacyFrameCodec codec, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var token = timeoutSource.Token;
        var headerBytes = new byte[PacketHeader.SizeInBytes];
        try
        {
            await stream.ReadExactlyAsync(headerBytes, token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutSource.IsCancellationRequested)
        {
            return null;
        }
        var header = PacketHeader.Read(headerBytes);
        Ensure(header.Size >= PacketHeader.SizeInBytes && header.Size <= PacketHeader.MaxPacketSize,
            $"Authenticated Type 25 smoke received invalid frame size {header.Size}.");
        var bytes = new byte[header.Size];
        headerBytes.CopyTo(bytes, 0);
        await stream.ReadExactlyAsync(bytes.AsMemory(PacketHeader.SizeInBytes), token);
        return codec.Decode(bytes);
    }

    private static ChildServer StartServer(string smokeRoot, string itemDataPath, string skillDataPath, int port)
    {
        var checkoutRoot = FindCheckoutRoot();
        var serverDll = Path.Combine(checkoutRoot, "src", "WydCdk.Server", "bin", "Debug", "net10.0", "WydCdk.Server.Core.dll");
        if (!File.Exists(serverDll))
            throw new FileNotFoundException("Built WydCdk.Server.Core.dll is missing; build the Server project before this smoke.", serverDll);

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
        AddArgument(startInfo, "--skill-data", skillDataPath);
        AddArgument(startInfo, "--server-group", "0");

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the authenticated Type 25 Server child process.");
        return new ChildServer(process, process.StandardOutput.ReadToEndAsync(), process.StandardError.ReadToEndAsync());
    }

    private static async Task WaitForServerAsync(Process process, int port, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        SocketException? lastSocketError = null;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited)
                throw new InvalidOperationException($"Authenticated Type 25 Host exited during startup with code {process.ExitCode}.");
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
            $"Authenticated Type 25 Host did not accept a loopback probe on 127.0.0.1:{port} within 10 seconds; last socket error={lastSocketError?.SocketErrorCode} ({lastSocketError?.NativeErrorCode}): {lastSocketError?.Message}");
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

    private static void EnsureInsideBuildRoot(string checkoutRoot, string path)
    {
        var buildRoot = Path.GetFullPath(Path.Combine(checkoutRoot, "Backup", "Build"));
        var fullPath = Path.GetFullPath(path);
        var prefix = Path.EndsInDirectorySeparator(buildRoot) ? buildRoot : buildRoot + Path.DirectorySeparatorChar;
        Ensure(fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(fullPath).Equals("visual-host", StringComparison.OrdinalIgnoreCase),
            $"Refusing to prepare Type 25 visual fixture outside its dedicated Backup/Build/visual-host folder: {fullPath}.");
    }

    private static void EnsureInsideTempRoot(string path)
    {
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        var fullPath = Path.GetFullPath(path);
        var prefix = Path.EndsInDirectorySeparator(tempRoot) ? tempRoot : tempRoot + Path.DirectorySeparatorChar;
        Ensure(fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(fullPath).StartsWith("wyd-cdk-type25-auth-", StringComparison.Ordinal),
            $"Refusing Type 25 smoke cleanup outside its generated temp root: {fullPath}.");
    }

    private static void EnsureFile(string path, string label)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"{label} was not found.", path);
    }

    private static void AddArgument(ProcessStartInfo startInfo, string name, string value)
    {
        startInfo.ArgumentList.Add(name);
        startInfo.ArgumentList.Add(value);
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class ChildServer(Process process, Task<string> standardOutput, Task<string> standardError)
    {
        public Process Process { get; } = process;

        public async Task<string> ReadLogsAsync()
        {
            if (!Process.HasExited)
            {
                Process.Kill(entireProcessTree: true);
                await Process.WaitForExitAsync(CancellationToken.None);
            }
            return $"stdout:\n{await standardOutput}\nstderr:\n{await standardError}";
        }

        public async Task StopAsync()
        {
            if (!Process.HasExited)
            {
                Process.Kill(entireProcessTree: true);
                await Process.WaitForExitAsync(CancellationToken.None);
            }
            Process.Dispose();
            _ = await standardOutput;
            _ = await standardError;
        }
    }
}

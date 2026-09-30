using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using WydCdk.Protocol;

internal static class ActionAccountAnalysisNetwork
{
    private const string AnalysisNotice = "Estamos analisando sua conta. Entre em contato com o suporte.";
    private const string StoreFailureNotice = "Nao foi possivel consultar a conta. Tente novamente mais tarde.";

    public static Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        if (options.ActionAccountAnalysisThresholdSmoke) return RunThresholdsAsync(options, actorHeader, codec);
        if (options.ActionAccountAnalysisSmoke) return RunActionAsync(options, actorHeader, codec);
        if (options.AccountAnalysisLoginSmoke) return RunLoginAsync(options, actorHeader, codec);
        return RunUnavailableLoginAsync(options, actorHeader, codec);
    }

    private static async Task RunThresholdsAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AccountReviewPeerAccountFile!;
        var actorStateFile = options.AccountReviewActorStateFile ?? options.AccountFile;
        var peerStateFile = options.AccountReviewPeerStateFile ?? peerFile;
        var reviewRoot = options.AccountReviewRoot!;
        var peerHeader = await ReadAccountHeaderAsync(peerFile);
        var actorBefore = await File.ReadAllBytesAsync(actorStateFile);
        var peerBefore = await File.ReadAllBytesAsync(peerStateFile);
        var actorName = CString(actorBefore.AsSpan(0, 16));
        var peerName = CString(peerBefore.AsSpan(0, 16));
        Check(actorName == actorHeader.AccountName && peerName == peerHeader.AccountName,
            "The threshold smoke state files do not match their authenticated fixture identities.");
        var actorSidecar = Path.Combine(reviewRoot, actorName + ".review.json");
        var peerSidecar = Path.Combine(reviewRoot, peerName + ".review.json");
        Check(!File.Exists(actorSidecar) && !Directory.Exists(actorSidecar) &&
            !File.Exists(peerSidecar) && !Directory.Exists(peerSidecar),
            "An isolated threshold-smoke account already has review state.");

        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "ANALYSIS_THRESHOLD_ACTOR_"))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "ANALYSIS_THRESHOLD_PEER_"))
        {
            var actorId = Id(actor.CharacterLoginFrame);
            var peerId = Id(peer.CharacterLoginFrame);
            var peerPosition = Spawn(peer.CharacterLoginFrame);
            Check(actorId > 0 && peerId > 0 && actorId != peerId, "Account Analysis threshold fixture IDs are invalid or identical.");
            Check(Math.Abs(peerPosition.X - Spawn(actor.CharacterLoginFrame).X) <= 33 &&
                Math.Abs(peerPosition.Y - Spawn(actor.CharacterLoginFrame).Y) <= 33,
                "Account Analysis threshold peer is outside the movement observation area.");
            Check(AttackRun(peer.CharacterLoginFrame) == byte.MaxValue,
                "The maximum-byte AttackRun control fixture did not reach the authoritative character-login frame.");
            MotionGateNetwork.AssertLogin(actor.CharacterLoginFrame, actorBefore, options.Slot);
            MotionGateNetwork.AssertLogin(peer.CharacterLoginFrame, peerBefore, options.Slot);

            var allowed = new ActionRequest(peerPosition.X, peerPosition.Y, 0, 15, new byte[24],
                checked((short)(peerPosition.X + 1)), peerPosition.Y);
            await peer.Stream.WriteAsync(allowed.ToFrame(codec, 423, 34, peerId));
            await AssertActionRelayAsync(peer.Stream, codec, options.Timeout, allowed, peerId, 423, "AttackRun=255 allowed speed author");
            await AssertActionRelayAsync(actor.Stream, codec, options.Timeout, allowed, peerId, 423, "AttackRun=255 allowed speed observer");
            Check(!File.Exists(peerSidecar), "An allowed speed was marked for Analysis solely because AttackRun was 255.");

            var overLimit = new ActionRequest(allowed.TargetX, allowed.TargetY, 0, 19, new byte[24],
                checked((short)(allowed.TargetX + 1)), allowed.TargetY);
            await peer.Stream.WriteAsync(overLimit.ToFrame(codec, 424, 35, peerId));
            var corrected = overLimit with { Speed = 15 };
            await AssertActionRelayAsync(peer.Stream, codec, options.Timeout, corrected, peerId, 424, "Speed=19 corrected author");
            await AssertActionRelayAsync(actor.Stream, codec, options.Timeout, corrected, peerId, 424, "Speed=19 corrected observer");
            Check(!File.Exists(peerSidecar) && !File.Exists(actorSidecar),
                "A non-severe speed violation created an Account Analysis sidecar.");

            await actor.LogoutAsync();
            await peer.LogoutAsync();
        }

        await MotionGateNetwork.AssertDisk(actorStateFile, actorBefore, options.Slot);
        await MotionGateNetwork.AssertDisk(peerStateFile, peerBefore, options.Slot);
        Check(!File.Exists(peerSidecar) && !File.Exists(actorSidecar),
            "A threshold control account was marked during logout or persistence.");
        Console.WriteLine("ACCOUNT_ANALYSIS_THRESHOLDS_PASS attackrun255-speed15-allowed speed19-clamped-no-analysis");
    }

    private static async Task RunActionAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AccountReviewPeerAccountFile!;
        var actorStateFile = options.AccountReviewActorStateFile ?? options.AccountFile;
        var peerStateFile = options.AccountReviewPeerStateFile ?? peerFile;
        var reviewRoot = options.AccountReviewRoot!;
        var peerHeader = await ReadAccountHeaderAsync(peerFile);
        var actorBefore = await File.ReadAllBytesAsync(actorStateFile);
        var peerBefore = await File.ReadAllBytesAsync(peerStateFile);
        var accountName = CString(actorBefore.AsSpan(0, 16));
        Check(accountName == actorHeader.AccountName, "The actor state file does not match the authenticated fixture identity.");
        Check(CString(peerBefore.AsSpan(0, 16)) == peerHeader.AccountName, "The peer state file does not match the authenticated fixture identity.");
        var sidecar = Path.Combine(reviewRoot, accountName + ".review.json");
        Check(!File.Exists(sidecar) && !Directory.Exists(sidecar), "The isolated actor already has review state before the Action smoke.");
        (short X, short Y) startPosition;
        int attackRun;

        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "ANALYSIS_ACTOR_", verifyActionModeGate: true))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "ANALYSIS_PEER_"))
        {
            var actorId = Id(actor.CharacterLoginFrame);
            var peerId = Id(peer.CharacterLoginFrame);
            startPosition = Spawn(actor.CharacterLoginFrame);
            Check(actorId > 0 && peerId > 0 && actorId != peerId, "Account Analysis fixture IDs are invalid or identical.");
            Check(Math.Abs(startPosition.X - Spawn(peer.CharacterLoginFrame).X) <= 33 &&
                Math.Abs(startPosition.Y - Spawn(peer.CharacterLoginFrame).Y) <= 33,
                "Account Analysis peer is outside the movement observation area.");
            MotionGateNetwork.AssertLogin(actor.CharacterLoginFrame, actorBefore, options.Slot);
            MotionGateNetwork.AssertLogin(peer.CharacterLoginFrame, peerBefore, options.Slot);

            if (options.AccountReviewActionReadyFile is { } readyFile)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(readyFile)!);
                var marker = Encoding.ASCII.GetBytes("ready");
                await using (var readyStream = new FileStream(readyFile, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
                    marker.Length, FileOptions.Asynchronous))
                    await readyStream.WriteAsync(marker);
                Console.WriteLine("ACCOUNT_ANALYSIS_ACTION_BARRIER_READY");
                using var barrierTimeout = new CancellationTokenSource(options.Timeout);
                while (!File.Exists(options.AccountReviewActionReleaseFile))
                    await Task.Delay(TimeSpan.FromMilliseconds(20), barrierTimeout.Token);
                Console.WriteLine("ACCOUNT_ANALYSIS_ACTION_BARRIER_RELEASED");
            }

            if (options.ExpectReviewWriteFailure)
                Directory.CreateDirectory(sidecar); // Inject failure only after both logins have passed the read gate.

            attackRun = AttackRun(actor.CharacterLoginFrame);
            var action = new ActionRequest(startPosition.X, startPosition.Y, 0, 20, new byte[24],
                checked((short)(startPosition.X + 1)), startPosition.Y);
            await actor.Stream.WriteAsync(action.ToFrame(codec, 420, 29, actorId));
            var (notice, priorFrames) = await ReadAnalysisNoticeAsync(actor.Stream, codec, options.Timeout);
            Check(!priorFrames.Any(IsAction), "The high-severity Action was relayed to its author before the Analysis notice.");
            var expectedText = options.ExpectReviewWriteFailure ? StoreFailureNotice : AnalysisNotice;
            Check(ReadNotice(notice) == expectedText, "Action Analysis returned the wrong durability outcome or client text.");
            Check(await ReadToEofAsync(actor.Stream, options.Timeout), "The high-severity Action session did not close after its notice.");

            await peer.Stream.WriteAsync(TradeCloseRequest.ToFrame(codec, 421, 30, peerId));
            await peer.Stream.WriteAsync(codec.Encode(CharacterLogoutRequest.MessageType, options.ConnectionId, 422,
                ReadOnlySpan<byte>.Empty, 31));
            var peerFrames = await ReadThroughLogoutConfirmationAsync(peer.Stream, codec, options.Timeout);
            Check(!peerFrames.Any(IsAction), "The high-severity Action was relayed to the nearby control account.");
        }

        await MotionGateNetwork.AssertDisk(peerStateFile, peerBefore, options.Slot);
        var actorAfter = await File.ReadAllBytesAsync(actorStateFile);
        var actorOffset = 216 + options.Slot * 816;
        Check(BinaryPrimitives.ReadInt16LittleEndian(actorAfter.AsSpan(actorOffset + 40)) == startPosition.X &&
            BinaryPrimitives.ReadInt16LittleEndian(actorAfter.AsSpan(actorOffset + 42)) == startPosition.Y,
            "The rejected high-severity Action changed the persisted character coordinates.");
        await MotionGateNetwork.AssertDisk(actorStateFile, actorBefore, options.Slot);

        if (options.ExpectReviewWriteFailure)
        {
            Check(Directory.Exists(sidecar) && !File.Exists(sidecar), "The write-failure fixture unexpectedly produced a review record.");
            Console.WriteLine("ACCOUNT_REVIEW_WRITE_FAILURE_PASS notice-eof-no-action-relay-position-unchanged");
        }
        else
        {
            Check(File.Exists(sidecar), "The Action Analysis sidecar was not durable before disconnect.");
            using var document = JsonDocument.Parse(await File.ReadAllBytesAsync(sidecar));
            var root = document.RootElement;
            Check(root.GetProperty("accountName").GetString() == accountName &&
                root.GetProperty("status").GetString() == "Analysis" &&
                root.GetProperty("evidence").GetProperty("reasonCode").GetString() == "ActionSpeedViolationHighSeverity" &&
                root.GetProperty("evidence").GetProperty("requestedSpeed").GetInt32() == 20 &&
                root.GetProperty("evidence").GetProperty("allowedSpeed").GetInt32() == (attackRun & 0xF) &&
                root.GetProperty("evidence").GetProperty("attackRun").GetInt32() == attackRun,
                "The durable Analysis record does not contain the authoritative Action evidence.");
            Console.WriteLine("ACCOUNT_ANALYSIS_MARK_PASS durable-before-eof-no-action-relay-position-unchanged");
        }
    }

    private static async Task RunLoginAsync(E2eOptions options, AccountHeader markedHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AccountReviewPeerAccountFile!;
        var controlHeader = await ReadAccountHeaderAsync(peerFile);
        var wrongPassword = "WrongPass1!";
        Check(!string.Equals(wrongPassword, markedHeader.Password, StringComparison.Ordinal), "The post-restart negative password unexpectedly matches the actor fixture.");

        using (var markedClient = await ConnectAsync(options))
        {
            await SendAccountLoginAsync(markedClient.GetStream(), codec, options.ConnectionId, markedHeader, wrongPassword);
            var stream = markedClient.GetStream();
            var notice = await ReadFrameAsync(stream, codec, options.Timeout);
            Require(notice, MessagePanelConfirmation.MessageType, MessagePanelConfirmation.PacketSize, "post-restart Analysis notice");
            Check(ReadNotice(notice) == AnalysisNotice, "The marked account was not rejected with the Analysis notice before password authentication.");
            Check(await ReadToEofAsync(stream, options.Timeout), "The marked account connection remained open after login denial.");
        }

        using (var controlClient = await ConnectAsync(options))
        {
            await SendAccountLoginAsync(controlClient.GetStream(), codec, options.ConnectionId, controlHeader, controlHeader.Password);
            var response = await ReadFrameAsync(controlClient.GetStream(), codec, options.Timeout);
            Require(response, AccountLoginConfirmationV769.MessageType, AccountLoginConfirmationV769.PacketSize, "control account login confirmation");
        }

        Console.WriteLine("ACCOUNT_ANALYSIS_RELOGIN_PASS wrong-password-blocked-before-auth control-account-accepted");
    }

    private static async Task RunUnavailableLoginAsync(E2eOptions options, AccountHeader accountHeader, LegacyFrameCodec codec)
    {
        using var client = await ConnectAsync(options);
        await SendAccountLoginAsync(client.GetStream(), codec, options.ConnectionId, accountHeader, accountHeader.Password);
        var notice = await ReadFrameAsync(client.GetStream(), codec, options.Timeout);
        Require(notice, MessagePanelConfirmation.MessageType, MessagePanelConfirmation.PacketSize, "review-store-unavailable login notice");
        Check(ReadNotice(notice) == StoreFailureNotice, "An unavailable review root did not return the generic fail-closed notice.");
        Check(await ReadToEofAsync(client.GetStream(), options.Timeout), "A login with unavailable review storage remained open.");
        Console.WriteLine("ACCOUNT_REVIEW_READ_FAILURE_PASS generic-notice-eof-no-login-confirmation");
    }

    private static async Task<TcpClient> ConnectAsync(E2eOptions options)
    {
        var client = new TcpClient();
        try
        {
            await client.ConnectAsync(options.Host, options.Port);
            var handshake = new byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32LittleEndian(handshake, LegacyFrameCodec.InitCode);
            await client.GetStream().WriteAsync(handshake);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static async Task SendAccountLoginAsync(NetworkStream stream, LegacyFrameCodec codec, ushort id, AccountHeader account, string password)
    {
        var payload = new byte[AccountLoginRequest.PacketSize - PacketHeader.SizeInBytes];
        WriteCString(payload.AsSpan(0, 12), password);
        WriteCString(payload.AsSpan(12, 16), account.AccountName);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(80), ClientReleasePolicy.RequiredClientVersion);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(84), 0);
        await stream.WriteAsync(codec.Encode(AccountLoginRequest.MessageType, id, 33, payload, 16));
    }

    private static async Task<AccountHeader> ReadAccountHeaderAsync(string path)
    {
        var bytes = await File.ReadAllBytesAsync(path);
        if (bytes.Length < 208) throw new InvalidDataException("The isolated account fixture is shorter than its login header.");
        return new AccountHeader(CString(bytes.AsSpan(0, 16)), CString(bytes.AsSpan(16, 12)), bytes.AsSpan(202, 6).ToArray());
    }

    private static async Task<List<DecodedFrame>> ReadThroughLogoutConfirmationAsync(NetworkStream stream, LegacyFrameCodec codec, TimeSpan timeout)
    {
        var frames = new List<DecodedFrame>();
        while (true)
        {
            var frame = await ReadFrameAsync(stream, codec, timeout);
            frames.Add(frame);
            if (frame.Header.Type == CharacterLogoutConfirmation.MessageType)
            {
                Require(frame, CharacterLogoutConfirmation.MessageType, CharacterLogoutConfirmation.PacketSize, "peer logout barrier");
                return frames;
            }
        }
    }

    private static async Task<DecodedFrame> ReadFrameAsync(NetworkStream stream, LegacyFrameCodec codec, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        var headerBytes = new byte[PacketHeader.SizeInBytes];
        await stream.ReadExactlyAsync(headerBytes, cancellation.Token);
        var header = PacketHeader.Read(headerBytes);
        Check(header.Size is >= PacketHeader.SizeInBytes and <= PacketHeader.MaxPacketSize, "Invalid account-review response frame size.");
        var bytes = new byte[header.Size];
        headerBytes.CopyTo(bytes, 0);
        await stream.ReadExactlyAsync(bytes.AsMemory(PacketHeader.SizeInBytes), cancellation.Token);
        var frame = codec.Decode(bytes);
        Check(frame.IsChecksumValid, "Account-review response checksum is invalid.");
        return frame;
    }

    private static async Task AssertActionRelayAsync(
        NetworkStream stream,
        LegacyFrameCodec codec,
        TimeSpan timeout,
        ActionRequest expected,
        ushort senderId,
        uint tick,
        string label)
    {
        var frames = new List<DecodedFrame>();
        for (var index = 0; index < 32; index++)
        {
            var frame = await ReadFrameAsync(stream, codec, timeout);
            frames.Add(frame);
            if (!IsAction(frame)) continue;
            ActionGateNetwork.AssertRelay(frames, expected, senderId, tick);
            return;
        }

        throw new InvalidOperationException($"{label}: Action relay did not arrive within 32 ordered frames.");
    }

    private static async Task<(DecodedFrame Notice, IReadOnlyList<DecodedFrame> PriorFrames)> ReadAnalysisNoticeAsync(
        NetworkStream stream,
        LegacyFrameCodec codec,
        TimeSpan timeout)
    {
        var priorFrames = new List<DecodedFrame>();
        for (var index = 0; index < 32; index++)
        {
            var frame = await ReadFrameAsync(stream, codec, timeout);
            if (frame.Header.Type == MessagePanelConfirmation.MessageType)
            {
                Require(frame, MessagePanelConfirmation.MessageType, MessagePanelConfirmation.PacketSize, "Action Analysis notice");
                return (frame, priorFrames);
            }

            priorFrames.Add(frame);
        }

        var received = string.Join(", ", priorFrames.Select(static frame => $"0x{frame.Header.Type:X4}/{frame.Header.Size}"));
        throw new InvalidOperationException($"Action Analysis notice did not arrive within 32 ordered frames; preceding frames: {received}.");
    }

    private static async Task<bool> ReadToEofAsync(NetworkStream stream, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        var buffer = new byte[1];
        return await stream.ReadAsync(buffer, cancellation.Token) == 0;
    }

    private static string ReadNotice(DecodedFrame frame)
    {
        var bytes = frame.Payload.Span;
        var end = bytes.IndexOf((byte)0);
        return Encoding.Latin1.GetString(end < 0 ? bytes : bytes[..end]);
    }

    private static void Require(DecodedFrame frame, ushort type, int size, string label)
    {
        Check(frame.Header.Type == type && frame.Header.Size == size,
            $"{label} wire type/size mismatch: expected type=0x{type:X4}, size={size}; received type=0x{frame.Header.Type:X4}, size={frame.Header.Size}.");
    }

    private static bool IsAction(DecodedFrame frame) => ActionRequest.TryParse(frame, out _);
    private static int AttackRun(DecodedFrame frame) => frame.Payload.Span[CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + 13];
    private static ushort Id(DecodedFrame frame) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);
    private static (short X, short Y) Spawn(DecodedFrame frame) => (BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span), BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[2..]));
    private static string CString(ReadOnlySpan<byte> bytes) { var end = bytes.IndexOf((byte)0); return Encoding.ASCII.GetString(end < 0 ? bytes : bytes[..end]); }
    private static void WriteCString(Span<byte> destination, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        if (bytes.Length >= destination.Length) throw new InvalidDataException("Account field does not fit its legacy packet slot.");
        bytes.CopyTo(destination);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

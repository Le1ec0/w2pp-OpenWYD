using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using WydCdk.Protocol;

internal static class AttackRvrGateNetwork
{
    private const string ExpectedMessage = "Você não pode atacar nessa área.";

    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AutoTradeClosePeerAccountFile!;
        var actorBefore = await File.ReadAllBytesAsync(options.AccountFile);
        var peerBefore = await File.ReadAllBytesAsync(peerFile);
        var peerHeader = new AccountHeader(CString(peerBefore.AsSpan(0, 16)), CString(peerBefore.AsSpan(16, 12)), peerBefore.AsSpan(202, 6).ToArray());

        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "ATTACK_RVR_ACTOR_"))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "ATTACK_RVR_PEER_"))
        {
            var actorId = Id(actor.CharacterLoginFrame);
            var peerId = Id(peer.CharacterLoginFrame);
            Check(actorId > 0 && peerId > 0 && actorId != peerId, "RVR gate needs distinct authoritative IDs.");
            Check(Hp(actor.CharacterLoginFrame) > 0 && Hp(peer.CharacterLoginFrame) > 0, "RVR gate fixture requires two living characters.");
            Check(Clan(actor.CharacterLoginFrame) == 0 && Clan(peer.CharacterLoginFrame) != 0,
                "RVR fixture must have a clanless actor and a clanned observer.");
            Console.WriteLine($"ATTACK_RVR_ACTOR_ID={actorId}");

            var actorFinalMove = await MoveIntoRvrAreaAsync(actor.Stream, codec, actorId, actor.CharacterLoginFrame, options.Timeout);
            var peerFinalMove = await MoveIntoRvrAreaAsync(peer.Stream, codec, peerId, peer.CharacterLoginFrame, options.Timeout);
            Check(actorFinalMove.TargetX == 1664 && actorFinalMove.TargetY == 2047 &&
                peerFinalMove.TargetX == 1664 && peerFinalMove.TargetY == 2047,
                "RVR setup did not finish at the last legal cell before the blocked Action-origin transition.");
            await Phase("ATTACK-RVR-POSITIONED", peerFirst: false);

            await Phase("ATTACK-RVR-READY", peerFirst: false);
            await actor.Stream.WriteAsync(BuildAttack(codec, actorId, unchecked((uint)Environment.TickCount64), 24));
            var denied = await Phase("ATTACK-RVR-DENIED", peerFirst: false);
            AssertPrivateNotice(denied.Actor);
            Check(!denied.Actor.Concat(denied.Peer).Any(IsAttack), "RVR-blocked attack was relayed to the actor or observer.");
            Check(!denied.Actor.Concat(denied.Peer).Any(frame => frame.Header.Type is SetHpModeConfirmation.MessageType or SetHpMpConfirmation.MessageType),
                "RVR-blocked attack reached a later resource gate.");
            Check(!denied.Peer.Any(frame => frame.Header.Type == MessagePanelConfirmation.MessageType), "Private RVR notice leaked to the observer.");

            await actor.Stream.WriteAsync(BuildAttack(codec, actorId, 240, 24));
            var deniedBeforeTick = await Phase("ATTACK-RVR-BEFORE-TICK", peerFirst: false);
            AssertPrivateNotice(deniedBeforeTick.Actor);
            Check(!deniedBeforeTick.Actor.Concat(deniedBeforeTick.Peer).Any(IsAttack) &&
                !deniedBeforeTick.Actor.Concat(deniedBeforeTick.Peer).Any(frame => frame.Header.Type is SetHpModeConfirmation.MessageType or SetHpMpConfirmation.MessageType) &&
                !deniedBeforeTick.Peer.Any(frame => frame.Header.Type == MessagePanelConfirmation.MessageType),
                "RVR gate did not precede the stale ClientTick gate or leaked its private response.");
            MotionGateNetwork.AssertLogin(actor.CharacterLoginFrame, actorBefore, options.Slot);
            MotionGateNetwork.AssertLogin(peer.CharacterLoginFrame, peerBefore, options.Slot);
            await actor.LogoutAsync();
            await peer.LogoutAsync();

            Task<(IReadOnlyList<DecodedFrame> Actor, IReadOnlyList<DecodedFrame> Peer)> Phase(string label, bool peerFirst)
                => AutoTradeCloseNetwork.CapturePhase(actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, label, peerFirst);
        }

        await MotionGateNetwork.AssertDisk(options.AccountFile, actorBefore, options.Slot);
        await MotionGateNetwork.AssertDisk(peerFile, peerBefore, options.Slot);
        Console.WriteLine("CLIENT_E2E_PASS attack-rvr-private-notice-before-dead-gate-no-relay");
    }

    private static async Task<ActionRequest> MoveIntoRvrAreaAsync(
        NetworkStream stream, LegacyFrameCodec codec, ushort id, DecodedFrame login, TimeSpan timeout)
    {
        var (x, y) = Position(login);
        await MoveAxisAsync(x, y, 1664, y);
        var final = await MoveAxisAsync(1664, y, 1664, 2047);
        await WaitForMovementEchoAsync(stream, codec, id, final, timeout);
        return final;

        async Task<ActionRequest> MoveAxisAsync(short fromX, short fromY, short toX, short toY)
        {
            var dx = toX - fromX;
            var dy = toY - fromY;
            var steps = Math.Max(1, (Math.Max(Math.Abs(dx), Math.Abs(dy)) + 32) / 33);
            var currentX = fromX;
            var currentY = fromY;
            ActionRequest? last = null;
            for (var step = 1; step <= steps; step++)
            {
                var nextX = checked((short)(fromX + (dx * step / steps)));
                var nextY = checked((short)(fromY + (dy * step / steps)));
                last = new ActionRequest(currentX, currentY, 0, 0, new byte[24], nextX, nextY);
                await stream.WriteAsync(last.ToFrame(codec, unchecked((uint)Environment.TickCount64), 23, id));
                currentX = nextX;
                currentY = nextY;
            }
            return last ?? throw new InvalidOperationException("Movement route did not emit any packets.");
        }
    }

    private static async Task WaitForMovementEchoAsync(
        NetworkStream stream, LegacyFrameCodec codec, ushort id, ActionRequest expected, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        for (var i = 0; i < 128; i++)
        {
            var header = new byte[PacketHeader.SizeInBytes];
            await stream.ReadExactlyAsync(header, cancellation.Token);
            var size = PacketHeader.Read(header).Size;
            Check(size >= PacketHeader.SizeInBytes && size <= PacketHeader.MaxPacketSize, "RVR movement echo has an invalid frame size.");
            var bytes = new byte[size];
            header.CopyTo(bytes, 0);
            await stream.ReadExactlyAsync(bytes.AsMemory(PacketHeader.SizeInBytes), cancellation.Token);
            var frame = codec.Decode(bytes);
            Check(frame.IsChecksumValid, "RVR movement echo checksum is invalid.");
            if (!IsAction(frame) || frame.Header.Id != id || !ActionRequest.TryParse(frame, out var echoed) || echoed is null)
                continue;
            if (echoed.TargetX == expected.TargetX && echoed.TargetY == expected.TargetY)
                return;
        }
        throw new InvalidOperationException("RVR movement setup did not receive the server echo for its final legal cell.");
    }

    private static byte[] BuildAttack(LegacyFrameCodec codec, ushort actorId, uint tick, byte keyword)
    {
        var payload = new byte[AttackRequest.OneTargetPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), actorId);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), 1);
        var bytes = codec.Encode(AttackRequest.OneTargetMessageType, actorId, tick, payload, keyword);
        var frame = codec.Decode(bytes);
        Check(AttackRequest.TryParse(frame, out var parsed) && parsed is not null, "RVR smoke did not construct a valid MSG_AttackOne.");
        return bytes;
    }

    private static void AssertPrivateNotice(IReadOnlyList<DecodedFrame> frames)
    {
        var notices = frames.Where(frame => frame.Header.Type == MessagePanelConfirmation.MessageType).ToArray();
        var expected = new byte[MessagePanelConfirmation.MessageLength];
        Encoding.Latin1.GetBytes(ExpectedMessage).CopyTo(expected, 0);
        Check(notices.Length == 1 && notices[0].IsChecksumValid && notices[0].Header.Size == MessagePanelConfirmation.PacketSize &&
            notices[0].Header.Id == 0 && notices[0].Header.ClientTick == 0 && notices[0].Header.KeywordIndex == 0 &&
            notices[0].Payload.Span.SequenceEqual(expected), "RVR gate did not return the exact private legacy MessagePanel notice.");
    }

    private static bool IsAttack(DecodedFrame frame) => frame.Header.Type is
        AttackRequest.AreaMessageType or AttackRequest.OneTargetMessageType or AttackRequest.TwoTargetMessageType;
    private static bool IsAction(DecodedFrame frame) => frame.Header.Type is
        ActionRequest.MessageType or ActionRequest.StopMessageType or ActionRequest.IllusionMessageType;
    private static int Hp(DecodedFrame frame) => BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[
        (CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + 24)..]);
    private static byte Clan(DecodedFrame frame) => frame.Payload.Span[CharacterLoginConfirmationV769.MobOffset + 16];
    private static (short X, short Y) Position(DecodedFrame frame) =>
        (BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span), BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[2..]));
    private static ushort Id(DecodedFrame frame) => BinaryPrimitives.ReadUInt16LittleEndian(
        frame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);
    private static string CString(ReadOnlySpan<byte> bytes) => Encoding.ASCII.GetString(bytes).Split('\0')[0];
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

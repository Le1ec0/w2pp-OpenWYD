using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;

internal static class AttackDeadGateNetwork
{
    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AutoTradeClosePeerAccountFile!;
        var actorBefore = await File.ReadAllBytesAsync(options.AccountFile);
        var peerBefore = await File.ReadAllBytesAsync(peerFile);
        var peerHeader = new AccountHeader(CString(peerBefore.AsSpan(0, 16)), CString(peerBefore.AsSpan(16, 12)), peerBefore.AsSpan(202, 6).ToArray());

        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "ATTACK_DEAD_ACTOR_"))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "ATTACK_DEAD_PEER_"))
        {
            var actorId = Id(actor.CharacterLoginFrame);
            var peerId = Id(peer.CharacterLoginFrame);
            Check(actorId > 0 && peerId > 0 && actorId != peerId, "Attack gate needs distinct authoritative IDs.");
            Check(Hp(actor.CharacterLoginFrame) == 0 && Hp(peer.CharacterLoginFrame) > 0, "Attack gate fixture must have only the actor at HP zero.");
            Console.WriteLine($"ATTACK_DEAD_ACTOR_ID={actorId}");
            MotionGateNetwork.AssertLogin(actor.CharacterLoginFrame, actorBefore, options.Slot);
            MotionGateNetwork.AssertLogin(peer.CharacterLoginFrame, peerBefore, options.Slot);

            await Phase("ATTACK-DEAD-READY", false);
            const uint attackTick = 240;
            const byte attackKeyword = 24;
            await actor.Stream.WriteAsync(BuildAttack(codec, actorId, attackTick, attackKeyword));
            var rejected = await Phase("ATTACK-DEAD", false);
            AssertPrivateHpMode(rejected.Actor, actorId);
            MotionGateNetwork.AssertNoHpMode(rejected.Peer);
            Check(!rejected.Actor.Concat(rejected.Peer).Any(IsAttack), "Dead attack was relayed to the actor or observer.");

            // QuitTrade exposes the shared session counter after the suppressed type-8 increment.
            await actor.Stream.WriteAsync(TradeCloseRequest.ToFrame(codec, 241, 25, actorId));
            var counter = await Phase("ATTACK-DEAD-COUNTER", false);
            MotionGateNetwork.AssertHpMode(counter.Actor, actorId, 0);
            MotionGateNetwork.AssertNoHpMode(counter.Peer);
            Check(!counter.Actor.Concat(counter.Peer).Any(frame => frame.Header.Type is TradeCloseRequest.MessageType or PkInfoConfirmation.MessageType),
                "Dead QuitTrade emitted cancellation or PK while checking the shared counter.");

            await MotionGateNetwork.AssertDisk(options.AccountFile, actorBefore, options.Slot);
            await MotionGateNetwork.AssertDisk(peerFile, peerBefore, options.Slot);
            await actor.LogoutAsync();
            await peer.LogoutAsync();

            Task<(IReadOnlyList<DecodedFrame> Actor, IReadOnlyList<DecodedFrame> Peer)> Phase(string label, bool peerFirst)
                => AutoTradeCloseNetwork.CapturePhase(actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, label, peerFirst);
        }

        await MotionGateNetwork.AssertDisk(options.AccountFile, actorBefore, options.Slot);
        await MotionGateNetwork.AssertDisk(peerFile, peerBefore, options.Slot);
        Console.WriteLine("CLIENT_E2E_PASS attack-dead-gate hp-zero-private-hpmode-no-relay-type8-shared-counter");
    }

    private static byte[] BuildAttack(LegacyFrameCodec codec, ushort actorId, uint tick, byte keyword)
    {
        var payload = new byte[AttackRequest.OneTargetPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), actorId);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), 1);
        return codec.Encode(AttackRequest.OneTargetMessageType, actorId, tick, payload, keyword);
    }

    private static void AssertPrivateHpMode(IReadOnlyList<DecodedFrame> frames, ushort id)
    {
        var responses = frames.Where(frame => frame.Header.Type == SetHpModeConfirmation.MessageType).ToArray();
        Check(responses.Length == 1 && responses[0].IsChecksumValid && responses[0].Header.Size == SetHpModeConfirmation.PacketSize &&
            responses[0].Header.Id == id && responses[0].Header.ClientTick == 0 && responses[0].Header.KeywordIndex == 0 &&
            BinaryPrimitives.ReadInt32LittleEndian(responses[0].Payload.Span) == 0 &&
            BinaryPrimitives.ReadInt16LittleEndian(responses[0].Payload.Span[4..]) == 22,
            "HP-zero attack did not return one exact private SetHpMode response.");
    }

    private static bool IsAttack(DecodedFrame frame) => frame.Header.Type is
        AttackRequest.AreaMessageType or AttackRequest.OneTargetMessageType or AttackRequest.TwoTargetMessageType;
    private static int Hp(DecodedFrame frame) => BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[
        (CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + 24)..]);
    private static ushort Id(DecodedFrame frame) => BinaryPrimitives.ReadUInt16LittleEndian(
        frame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);
    private static string CString(ReadOnlySpan<byte> bytes) => Encoding.ASCII.GetString(bytes).Split('\0')[0];
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;

internal static class AttackTimingGateNetwork
{
    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AutoTradeClosePeerAccountFile!;
        var actorBefore = await File.ReadAllBytesAsync(options.AccountFile);
        var peerBefore = await File.ReadAllBytesAsync(peerFile);
        var peerHeader = new AccountHeader(
            CString(peerBefore.AsSpan(0, 16)), CString(peerBefore.AsSpan(16, 12)), peerBefore.AsSpan(202, 6).ToArray());

        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "ATTACK_TIMING_ACTOR_"))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "ATTACK_TIMING_PEER_"))
        {
            var actorId = Id(actor.CharacterLoginFrame);
            var peerId = Id(peer.CharacterLoginFrame);
            Check(actorId > 0 && peerId > 0 && actorId != peerId, "Attack timing fixture needs distinct authoritative IDs.");
            Check(Hp(actor.CharacterLoginFrame) > 0 && Hp(peer.CharacterLoginFrame) > 0, "Attack timing fixtures must be alive.");
            Console.WriteLine($"ATTACK_TIMING_ACTOR_ID={actorId}");
            Console.WriteLine($"ATTACK_TIMING_PEER_ID={peerId}");

            await Phase("ATTACK-TIMING-READY");

            var currentTick = unchecked((uint)Environment.TickCount64);
            Check(currentTick > 120_001, "Local monotonic clock is too small to construct the stale-window packet.");
            Check(currentTick < uint.MaxValue - 20_000u, "Local monotonic clock is too close to its 32-bit wrap for this smoke.");

            // Invalid skill -1 is rejected after timing but before mana or combat mutation.
            await actor.Stream.WriteAsync(BuildAttack(codec, actorId, currentTick, keyword: 23));
            await actor.Stream.WriteAsync(BuildAttack(codec, actorId, unchecked(currentTick + 799u), keyword: 24));

            // The server stores this tick before rejecting its server-window violation. The following
            // current-era tick crosses the uint wrap in LastAttackTick + 800 and exposes the legacy 4/type7 path.
            await actor.Stream.WriteAsync(BuildAttack(codec, actorId, uint.MaxValue - 300u, keyword: 26));
            await actor.Stream.WriteAsync(BuildAttack(codec, actorId, unchecked(currentTick + 1_000u), keyword: 27));

            // The peer has no prior attack tick, so this exercises the 120-second server-window branch directly.
            await peer.Stream.WriteAsync(BuildAttack(codec, peerId, currentTick - 120_001u, keyword: 25));
            var rejected = await Phase("ATTACK-TIMING-RESULTS");
            Check(!rejected.Actor.Concat(rejected.Peer).Any(IsAttack), "Invalid-skill/timing-rejected attack was relayed.");
            Check(!rejected.Actor.Concat(rejected.Peer).Any(frame => frame.Header.Type is SetHpMpConfirmation.MessageType or SetHpModeConfirmation.MessageType),
                "Invalid-skill/timing-rejected attack reached a resource/HP response.");
            await actor.LogoutAsync();
            await peer.LogoutAsync();

            await MotionGateNetwork.AssertDisk(options.AccountFile, actorBefore, options.Slot);
            await MotionGateNetwork.AssertDisk(peerFile, peerBefore, options.Slot);

            Task<(IReadOnlyList<DecodedFrame> Actor, IReadOnlyList<DecodedFrame> Peer)> Phase(string label)
                => AutoTradeCloseNetwork.CapturePhase(actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, label, peerFirst: false);
        }

        await MotionGateNetwork.AssertDisk(options.AccountFile, actorBefore, options.Slot);
        await MotionGateNetwork.AssertDisk(peerFile, peerBefore, options.Slot);
        Console.WriteLine("CLIENT_E2E_READY attack-timing-too-soon-outside-window-and-wrap-backstep");
    }

    private static byte[] BuildAttack(LegacyFrameCodec codec, ushort actorId, uint tick, byte keyword)
    {
        var payload = new byte[AttackRequest.OneTargetPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), actorId);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), -1);
        var bytes = codec.Encode(AttackRequest.OneTargetMessageType, actorId, tick, payload, keyword);
        var decoded = codec.Decode(bytes);
        Check(AttackRequest.TryParse(decoded, out var attack) && attack is { SkillIndex: -1 }, "Timing smoke did not create a valid-wire invalid-skill attack.");
        return bytes;
    }

    private static int Hp(DecodedFrame frame) => BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span.Slice(
        CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + 24, sizeof(int)));

    private static ushort Id(DecodedFrame frame) => BinaryPrimitives.ReadUInt16LittleEndian(
        frame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);

    private static bool IsAttack(DecodedFrame frame) => frame.Header.Type is
        AttackRequest.AreaMessageType or AttackRequest.OneTargetMessageType or AttackRequest.TwoTargetMessageType;

    private static string CString(ReadOnlySpan<byte> bytes) => Encoding.ASCII.GetString(bytes).Split('\0')[0];

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

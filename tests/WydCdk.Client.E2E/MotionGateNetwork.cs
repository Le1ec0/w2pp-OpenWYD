using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;

internal static class MotionGateNetwork
{
    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AutoTradeClosePeerAccountFile!;
        var actorBefore = await File.ReadAllBytesAsync(options.AccountFile);
        var peerBefore = await File.ReadAllBytesAsync(peerFile);
        var peerHeader = new AccountHeader(CString(peerBefore.AsSpan(0, 16)), CString(peerBefore.AsSpan(16, 12)), peerBefore.AsSpan(202, 6).ToArray());
        var expectedHp = options.MotionNegativeHp ? -1 : 0;
        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "MOTION_ACTOR_", verifyMotionModeGate: true))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "MOTION_PEER_"))
        {
            var actorId = Id(actor.CharacterLoginFrame);
            var peerId = Id(peer.CharacterLoginFrame);
            Check(actorId > 0 && peerId > 0 && actorId != peerId, "Distinct authoritative IDs missing.");
            Check(Hp(actor.CharacterLoginFrame) == expectedHp && Hp(peer.CharacterLoginFrame) > 0, "Wrong Motion HP fixture.");
            AssertLogin(actor.CharacterLoginFrame, actorBefore, options.Slot);
            AssertLogin(peer.CharacterLoginFrame, peerBefore, options.Slot);
            await Phase("MOTION-READY", false);

            var actorMotion = new MotionRequest(7, 19, 0x12345678);
            await actor.Stream.WriteAsync(actorMotion.ToFrame(codec, 111, 18, actorId));
            var actorFrames = await Phase("MOTION-ACTOR", false);
            if (expectedHp == 0)
            {
                AssertHpMode(actorFrames.Actor, actorId, expectedHp);
                AssertNoHpMode(actorFrames.Peer);
                Check(!actorFrames.Actor.Concat(actorFrames.Peer).Any(f => f.Header.Type == MotionRequest.MessageType), "Dead Motion relayed.");
            }
            else
            {
                AssertMotion(actorFrames.Actor, actorId, actorMotion, 111);
                AssertMotion(actorFrames.Peer, actorId, actorMotion, 111);
                Check(!actorFrames.Actor.Concat(actorFrames.Peer).Any(f => f.Header.Type == SetHpModeConfirmation.MessageType), "Negative HP incorrectly gated like zero.");
            }

            var peerMotion = new MotionRequest(11, 27, 0x23456789);
            await peer.Stream.WriteAsync(peerMotion.ToFrame(codec, 112, 19, peerId));
            var peerFrames = await Phase("MOTION-PEER", true);
            AssertMotion(peerFrames.Actor, peerId, peerMotion, 112);
            AssertMotion(peerFrames.Peer, peerId, peerMotion, 112);
            Check(!peerFrames.Actor.Concat(peerFrames.Peer).Any(f => f.Header.Type == SetHpModeConfirmation.MessageType), "Living Motion incorrectly gated.");

            // Both handlers must accumulate in the same session counter, not separate opcode counters.
            await actor.Stream.WriteAsync(TradeCloseRequest.ToFrame(codec, 113, 20, actorId));
            var quitFrames = await Phase("MOTION-QUIT", false);
            AssertHpMode(quitFrames.Actor, actorId, expectedHp);
            AssertNoHpMode(quitFrames.Peer);
            Check(!quitFrames.Actor.Concat(quitFrames.Peer).Any(f => f.Header.Type is TradeCloseRequest.MessageType or PkInfoConfirmation.MessageType), "Dead QuitTrade emitted cancel/PK.");
            await AssertDisk(options.AccountFile, actorBefore, options.Slot);
            await AssertDisk(peerFile, peerBefore, options.Slot);
            await actor.LogoutAsync(); await peer.LogoutAsync();

            Task<(IReadOnlyList<DecodedFrame> Actor, IReadOnlyList<DecodedFrame> Peer)> Phase(string label, bool peerFirst)
                => AutoTradeCloseNetwork.CapturePhase(actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, label, peerFirst);
        }
        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "MOTION_RELOGIN_"))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "MOTION_PEER_RELOGIN_"))
        {
            Check(Hp(actor.CharacterLoginFrame) == expectedHp, "Motion changed persisted HP.");
            AssertLogin(actor.CharacterLoginFrame, actorBefore, options.Slot);
            AssertLogin(peer.CharacterLoginFrame, peerBefore, options.Slot);
            await actor.LogoutAsync(); await peer.LogoutAsync();
        }
        await AssertDisk(options.AccountFile, actorBefore, options.Slot);
        await AssertDisk(peerFile, peerBefore, options.Slot);
        Console.WriteLine($"CLIENT_E2E_PASS motion-gates hp={expectedHp} alive-relay-fields-tick shared-counter carry-cargo-gold-relogin");
    }

    internal static void AssertNoHpMode(IReadOnlyList<DecodedFrame> frames)
        => Check(!frames.Any(f => f.Header.Type == SetHpModeConfirmation.MessageType), "Private HpMode leaked to peer.");

    internal static void AssertHpMode(IReadOnlyList<DecodedFrame> frames, ushort id, int hp)
    {
        var modes = frames.Where(f => f.Header.Type == SetHpModeConfirmation.MessageType).ToArray();
        Check(modes.Length == 1 && modes[0].Header.Size == 20 && modes[0].Header.Id == id &&
            BinaryPrimitives.ReadInt32LittleEndian(modes[0].Payload.Span) == hp &&
            BinaryPrimitives.ReadInt16LittleEndian(modes[0].Payload.Span[4..]) == 22, "HP/mode response mismatch.");
    }

    private static void AssertMotion(IReadOnlyList<DecodedFrame> frames, ushort id, MotionRequest expected, uint tick)
    {
        var motions = frames.Where(f => f.Header.Type == MotionRequest.MessageType).ToArray();
        Check(motions.Length == 1 && motions[0].Header.Size == 20 && motions[0].Header.Id == id &&
            motions[0].Header.ClientTick == tick && MotionRequest.TryParse(motions[0], out var parsed) && parsed == expected,
            "Motion relay changed ID/fields/tick or duplicated frame.");
    }

    private static int Hp(DecodedFrame frame) => BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[(CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + 24)..]);
    private static ushort Id(DecodedFrame frame) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);
    private static string CString(ReadOnlySpan<byte> bytes) => Encoding.ASCII.GetString(bytes).Split('\0')[0];
    internal static void AssertLogin(DecodedFrame frame, byte[] baseline, int slot)
    {
        var mob = frame.Payload.Span.Slice(CharacterLoginConfirmationV769.MobOffset, CharacterMobV769.SizeInBytes);
        var offset = 216 + slot * 816;
        Check(mob.Slice(CharacterMobV769.CarryOffset, 64 * 8).SequenceEqual(baseline.AsSpan(offset + 268, 64 * 8)) &&
            BinaryPrimitives.ReadInt32LittleEndian(mob[CharacterMobV769.CoinOffset..]) == BinaryPrimitives.ReadInt32LittleEndian(baseline.AsSpan(offset + 28)), "Motion changed carry/Gold in world.");
    }
    internal static async Task AssertDisk(string path, byte[] baseline, int slot)
    {
        var current = await File.ReadAllBytesAsync(path);
        var offset = 216 + slot * 816;
        Check(current.AsSpan(offset + 268, 64 * 8).SequenceEqual(baseline.AsSpan(offset + 268, 64 * 8)) &&
            current.AsSpan(3480, 128 * 8).SequenceEqual(baseline.AsSpan(3480, 128 * 8)) &&
            current.AsSpan(offset + 28, 4).SequenceEqual(baseline.AsSpan(offset + 28, 4)), "Motion changed durable carry/cargo/Gold.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

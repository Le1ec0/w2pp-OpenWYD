using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;

internal static class ActionIllusionEligibilityNetwork
{
    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AutoTradeClosePeerAccountFile!;
        var actorBefore = await File.ReadAllBytesAsync(options.AccountFile);
        var peerBefore = await File.ReadAllBytesAsync(peerFile);
        var peerHeader = new AccountHeader(CString(peerBefore.AsSpan(0, 16)), CString(peerBefore.AsSpan(16, 12)), peerBefore.AsSpan(202, 6).ToArray());

        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "ILLUSION_ELIGIBILITY_ACTOR_", verifyActionModeGate: true))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "ILLUSION_ELIGIBILITY_PEER_"))
        {
            var actorId = Id(actor.CharacterLoginFrame);
            var peerId = Id(peer.CharacterLoginFrame);
            var actorMob = actor.CharacterLoginFrame.Payload.Span.Slice(CharacterLoginConfirmationV769.MobOffset, CharacterMobV769.SizeInBytes);
            var actorClass = actorMob[CharacterMobV769.ClassOffset];
            var learnedSkill = BinaryPrimitives.ReadUInt32LittleEndian(actorMob[CharacterMobV769.LearnedSkillOffset..]);
            var level = BinaryPrimitives.ReadInt32LittleEndian(actorMob[CharacterMobV769.CurrentScoreOffset..]);
            Check(actorId > 0 && peerId > 0 && actorId != peerId && Hp(actor.CharacterLoginFrame) == -1 && Hp(peer.CharacterLoginFrame) > 0,
                "Wrong Action3 eligibility fixture or HP state.");
            Check(actorClass == 2 && (learnedSkill & 2) == 0 && level == 398, "Action3 invalid-class/skill level-398 fixture was not loaded.");

            var position = Spawn(actor.CharacterLoginFrame);
            var peerPosition = Spawn(peer.CharacterLoginFrame);
            Check(Math.Abs(position.X - peerPosition.X) <= 33 && Math.Abs(position.Y - peerPosition.Y) <= 33,
                "Action3 eligibility peer outside view.");
            Console.WriteLine($"ILLUSION_ELIGIBILITY_ACTOR_ID={actorId}");
            Console.WriteLine($"ILLUSION_ELIGIBILITY_PEER_ID={peerId}");
            await Phase("READY");

            var action = new ActionRequest(position.X, position.Y, 0, 0, new byte[24], (short)(position.X + 1), position.Y)
            { WireType = ActionRequest.IllusionMessageType };
            var tick = unchecked((uint)Environment.TickCount64);
            await actor.Stream.WriteAsync(action.ToFrame(codec, tick, 29, actorId));
            var moved = await Phase("INVALID-ELIGIBILITY-MOVE");
            ActionGateNetwork.AssertRelay(moved.Actor, action, actorId, tick);
            ActionGateNetwork.AssertRelay(moved.Peer, action, actorId, tick);
            MotionGateNetwork.AssertNoHpMode(moved.Actor.Concat(moved.Peer).ToArray());

            await actor.Stream.WriteAsync(TradeCloseRequest.ToFrame(codec, unchecked(tick + 1), 30, actorId));
            var quit = await Phase("SHARED-COUNTER");
            MotionGateNetwork.AssertHpMode(quit.Actor, actorId, -1);
            MotionGateNetwork.AssertNoHpMode(quit.Peer);
            Check(!quit.Actor.Concat(quit.Peer).Any(frame => ActionRequest.TryParse(frame, out _)), "QuitTrade counter probe emitted movement.");

            MotionGateNetwork.AssertLogin(actor.CharacterLoginFrame, actorBefore, options.Slot);
            MotionGateNetwork.AssertLogin(peer.CharacterLoginFrame, peerBefore, options.Slot);
            await MotionGateNetwork.AssertDisk(options.AccountFile, actorBefore, options.Slot);
            await MotionGateNetwork.AssertDisk(peerFile, peerBefore, options.Slot);
            await actor.LogoutAsync();
            await peer.LogoutAsync();

            Task<(IReadOnlyList<DecodedFrame> Actor, IReadOnlyList<DecodedFrame> Peer)> Phase(string label)
                => AutoTradeCloseNetwork.CapturePhase(actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, "ILLUSION-ELIGIBILITY-" + label, false);
        }

        Console.WriteLine("CLIENT_E2E_PASS illusion-eligibility type28 fallthrough-relay shared-counter hp-carry-cargo-gold");
    }

    private static int Hp(DecodedFrame frame)
        => BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[(CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + 24)..]);
    private static ushort Id(DecodedFrame frame)
        => BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);
    private static (short X, short Y) Spawn(DecodedFrame frame)
        => (BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span), BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[2..]));
    private static string CString(ReadOnlySpan<byte> bytes)
    {
        var end = bytes.IndexOf((byte)0);
        return Encoding.ASCII.GetString(end < 0 ? bytes : bytes[..end]);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;

internal static class ActionIllusionManaNetwork
{
    private const int Skill73ManaSpent = 45;

    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AutoTradeClosePeerAccountFile!;
        var actorBefore = await File.ReadAllBytesAsync(options.AccountFile);
        var peerBefore = await File.ReadAllBytesAsync(peerFile);
        var peerHeader = new AccountHeader(CString(peerBefore.AsSpan(0, 16)), CString(peerBefore.AsSpan(16, 12)), peerBefore.AsSpan(202, 6).ToArray());

        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "ILLUSION_MANA_ACTOR_", verifyActionModeGate: true))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "ILLUSION_MANA_PEER_"))
        {
            var actorId = Id(actor.CharacterLoginFrame);
            var peerId = Id(peer.CharacterLoginFrame);
            var actorMob = actor.CharacterLoginFrame.Payload.Span.Slice(CharacterLoginConfirmationV769.MobOffset, CharacterMobV769.SizeInBytes);
            var actorClass = actorMob[CharacterMobV769.ClassOffset];
            var learnedSkill = BinaryPrimitives.ReadUInt32LittleEndian(actorMob[CharacterMobV769.LearnedSkillOffset..]);
            Check(actorId > 0 && peerId > 0 && actorId != peerId && Hp(actor.CharacterLoginFrame) > 0,
                "Wrong Action3 mana fixture or HP state.");
            Check(actorClass == 3 && (learnedSkill & 2) != 0, "Action3 mana fixture is not eligible for Illusion.");
            Check(Mp(actor.CharacterLoginFrame) == Skill73ManaSpent * 2, "Action3 fixture did not load exactly two skill-73 mana costs.");

            var position = Spawn(actor.CharacterLoginFrame);
            var peerPosition = Spawn(peer.CharacterLoginFrame);
            Check(Math.Abs(position.X - peerPosition.X) <= 33 && Math.Abs(position.Y - peerPosition.Y) <= 33,
                "Action3 mana peer outside view.");
            Console.WriteLine($"ILLUSION_MANA_ACTOR_ID={actorId}");
            await Phase(actor, peer, actorId, peerId, codec, options, "READY");

            var rejectedByTick = Step(position);
            var staleTick = unchecked((uint)Environment.TickCount64 - 120_001u);
            await actor.Stream.WriteAsync(rejectedByTick.ToFrame(codec, staleTick, 29, actorId));
            var tickFrames = await Phase(actor, peer, actorId, peerId, codec, options, "MANA-BEFORE-TICK");
            Check(!tickFrames.Actor.Concat(tickFrames.Peer).Any(IsAction), "Out-of-window Action3 was relayed.");
            Check(!tickFrames.Actor.Concat(tickFrames.Peer).Any(IsResourceSync), "Tick rejection sent a SetHpMp frame unexpectedly.");

            var accepted = Step(position);
            var acceptedTick = CurrentTick();
            await actor.Stream.WriteAsync(accepted.ToFrame(codec, acceptedTick, 30, actorId));
            var acceptedFrames = await Phase(actor, peer, actorId, peerId, codec, options, "MANA-ACCEPTED");
            ActionGateNetwork.AssertRelay(acceptedFrames.Actor, accepted, actorId, acceptedTick);
            ActionGateNetwork.AssertRelay(acceptedFrames.Peer, accepted, actorId, acceptedTick);
            AssertResourceSync(acceptedFrames.Actor, actorId, acceptedTick, expectedMp: 0, expectedRequestedMp: Skill73ManaSpent * 2);
            Check(!acceptedFrames.Peer.Any(IsResourceSync), "Action3 MP synchronization leaked to the peer.");
            position = (accepted.TargetX, accepted.TargetY);

            var insufficient = Step(position);
            var insufficientTick = unchecked(acceptedTick + 1);
            await actor.Stream.WriteAsync(insufficient.ToFrame(codec, insufficientTick, 31, actorId));
            var insufficientFrames = await Phase(actor, peer, actorId, peerId, codec, options, "MANA-INSUFFICIENT");
            Check(!insufficientFrames.Actor.Concat(insufficientFrames.Peer).Any(IsAction), "Action3 with insufficient MP moved or relayed.");
            AssertResourceSync(insufficientFrames.Actor, actorId, insufficientTick, expectedMp: 0, expectedRequestedMp: Skill73ManaSpent * 2);
            Check(!insufficientFrames.Peer.Any(IsResourceSync), "Insufficient-MP synchronization leaked to the peer.");

            await actor.LogoutAsync();
            await peer.LogoutAsync();
            var actorOffset = 216 + options.Slot * 816;
            var saved = await File.ReadAllBytesAsync(options.AccountFile);
            Check(BinaryPrimitives.ReadInt32LittleEndian(saved.AsSpan(actorOffset + 120)) == 0,
                "Consumed Action3 MP was not persisted on logout.");
            await MotionGateNetwork.AssertDisk(options.AccountFile, actorBefore, options.Slot);
            await MotionGateNetwork.AssertDisk(peerFile, peerBefore, options.Slot);
        }

        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "ILLUSION_MANA_RELOGIN_"))
        {
            Check(Mp(actor.CharacterLoginFrame) == 0, "Consumed Action3 MP was not preserved through relogin.");
            await actor.LogoutAsync();
        }

        Console.WriteLine("CLIENT_E2E_PASS illusion-mana fixed-skill73 cost-before-tick reqmp-unchanged insufficient-sync private-sync persisted-relogin");

    }

    private static Task<(IReadOnlyList<DecodedFrame> Actor, IReadOnlyList<DecodedFrame> Peer)> Phase(
        E2eSession actor, E2eSession peer, ushort actorId, ushort peerId, LegacyFrameCodec codec, E2eOptions options, string label)
        => AutoTradeCloseNetwork.CapturePhase(actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, "ILLUSION-MANA-" + label, false);

    private static ActionRequest Step((short X, short Y) position)
        => new(position.X, position.Y, 0, 0, new byte[24], (short)(position.X + 1), position.Y)
        { WireType = ActionRequest.IllusionMessageType };

    private static void AssertResourceSync(IReadOnlyList<DecodedFrame> frames, ushort id, uint tick, int expectedMp, int expectedRequestedMp)
    {
        var sync = frames.Where(IsResourceSync).ToArray();
        Check(sync.Length == 1 && sync[0].Header.Id == id && sync[0].Header.ClientTick == tick && sync[0].Payload.Length == 16 &&
            BinaryPrimitives.ReadInt32LittleEndian(sync[0].Payload.Span[4..]) == expectedMp &&
            BinaryPrimitives.ReadInt32LittleEndian(sync[0].Payload.Span[12..]) == expectedRequestedMp,
            "Action3 SetHpMp did not carry the expected private CurrentMp/ReqMp values.");
    }

    private static int Hp(DecodedFrame frame)
        => BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[(CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + 24)..]);
    private static int Mp(DecodedFrame frame)
        => BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[(CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + 28)..]);
    private static ushort Id(DecodedFrame frame)
        => BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);
    private static (short X, short Y) Spawn(DecodedFrame frame)
        => (BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span), BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[2..]));
    private static uint CurrentTick() => unchecked((uint)Environment.TickCount64);
    private static bool IsAction(DecodedFrame frame) => ActionRequest.TryParse(frame, out _);
    private static bool IsResourceSync(DecodedFrame frame) => frame.Header.Type == SetHpMpConfirmation.MessageType;
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

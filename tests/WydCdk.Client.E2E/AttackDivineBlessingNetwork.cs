using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using WydCdk.Protocol;

/// <summary>Guarded loopback coverage for the parent 201 action and its server-created skill-29 child.</summary>
internal static class AttackDivineBlessingNetwork
{
    private const int StartingHp = 900;
    private const int MaximumHp = 1000;
    private const int StartingMp = 500;
    private const int InsufficientMp = 10;
    private const int ChildManaCost = 30;
    private const int ExpectedHeal = 100;

    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var observerFile = options.AutoTradeClosePeerAccountFile!;
        var distantFile = options.AttackDivineBlessingDistantObserverAccountFile!;
        var insufficientMana = options.AttackDivineBlessingInsufficientManaSmoke;
        var actorBefore = await File.ReadAllBytesAsync(options.AccountFile);
        var observerBefore = await File.ReadAllBytesAsync(observerFile);
        var distantBefore = await File.ReadAllBytesAsync(distantFile);
        var observerHeader = Header(observerBefore);
        var distantHeader = Header(distantBefore);
        var expectedActorHp = insufficientMana ? StartingHp : MaximumHp;
        var expectedActorMp = insufficientMana ? InsufficientMp : StartingMp - ChildManaCost;
        var expectedExperience = Experience(actorBefore, options.Slot);

        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "DIVINE_BLESSING_ACTOR_"))
        await using (var observer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = observerFile }, observerHeader, codec, "DIVINE_BLESSING_OBSERVER_"))
        await using (var distant = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = distantFile }, distantHeader, codec, "DIVINE_BLESSING_DISTANT_"))
        {
            var actorId = Id(actor.CharacterLoginFrame);
            var observerId = Id(observer.CharacterLoginFrame);
            var distantId = Id(distant.CharacterLoginFrame);
            Check(actorId > 0 && observerId > 0 && distantId > 0 && actorId != observerId && actorId != distantId && observerId != distantId,
                "Divine Blessing fixtures must enter as three distinct participants.");
            Check(actor.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.ClassOffset] == 3,
                "Divine Blessing actor fixture must be class 3.");
            Check(Score(actor.CharacterLoginFrame, 24) == StartingHp && Score(actor.CharacterLoginFrame, 16) == MaximumHp &&
                Score(actor.CharacterLoginFrame, 28) == (insufficientMana ? InsufficientMp : StartingMp),
                "Divine Blessing actor fixture entered with unexpected HP/MP.");

            var (ax, ay) = Position(actor.CharacterLoginFrame);
            var (ox, oy) = Position(observer.CharacterLoginFrame);
            var (dx, dy) = Position(distant.CharacterLoginFrame);
            Check(Math.Abs(ax - ox) <= 16 && Math.Abs(ay - oy) <= 16,
                $"The nearby observer is outside the captured 33x33 view: actor=({ax},{ay}), observer=({ox},{oy}).");
            Check(Math.Abs(ax - dx) > 16 || Math.Abs(ay - dy) > 16,
                $"The distant observer unexpectedly entered the captured view: actor=({ax},{ay}), distant=({dx},{dy}).");

            // Drain login/spawn traffic and prove the actor and nearby observer can exchange view-local markers.
            await CaptureNearbyPhase(actor, observer, actorId, observerId, codec, options.Timeout, "READY");
            await DrainUntilQuietAsync(distant.Stream, codec, options.Timeout);

            var tick = unchecked((uint)Environment.TickCount64);
            Check(tick > 120_001 && tick < uint.MaxValue - 20_000u, "Local monotonic clock cannot host this smoke's tick sequence.");
            await actor.Stream.WriteAsync(BuildBlessing(codec, actorId, observerId, (short)ax, (short)ay, (short)ox, (short)oy, tick - 50_000u));

            var nearby = await CaptureNearbyPhase(actor, observer, actorId, observerId, codec, options.Timeout,
                insufficientMana ? "LOW-MANA" : "HEAL");
            var distantParent = await ReadFrameAsync(distant.Stream, codec, options.Timeout);
            AssertParent(distantParent, actorId, expectedActorHp, expectedActorMp, expectedExperience);

            if (insufficientMana)
                AssertInsufficientMana(nearby.Actor, nearby.Observer, actorId, observerId, distantParent, expectedExperience);
            else
                AssertHeal(nearby.Actor, nearby.Observer, actorId, observerId, expectedExperience);

            await actor.LogoutAsync();
            await observer.LogoutAsync();
            await distant.LogoutAsync();
        }

        await AssertExperienceUnchangedAsync(options.AccountFile, options.Slot, expectedExperience);
        await AssertExperienceUnchangedAsync(observerFile, options.Slot, Experience(observerBefore, options.Slot));
        await AssertExperienceUnchangedAsync(distantFile, options.Slot, Experience(distantBefore, options.Slot));
        await MotionGateNetwork.AssertDisk(options.AccountFile, actorBefore, options.Slot);
        await MotionGateNetwork.AssertDisk(observerFile, observerBefore, options.Slot);
        await MotionGateNetwork.AssertDisk(distantFile, distantBefore, options.Slot);

        Console.WriteLine(insufficientMana
            ? "CLIENT_E2E_PASS attack-divine-blessing-insufficient-mana-private-correction-before-parent-no-child-no-heal"
            : "CLIENT_E2E_PASS attack-divine-blessing-child-before-parent-captured-view-authoritative-hp-mp-exp");
    }

    private static async Task<(IReadOnlyList<DecodedFrame> Actor, IReadOnlyList<DecodedFrame> Observer)> CaptureNearbyPhase(
        E2eSession actor, E2eSession observer, ushort actorId, ushort observerId, LegacyFrameCodec codec, TimeSpan timeout, string label) =>
        await AutoTradeCloseNetwork.CapturePhase(actor.Stream, observer.Stream, actorId, observerId, codec, timeout,
            "DIVINE-BLESSING-" + label, peerFirst: false);

    private static byte[] BuildBlessing(LegacyFrameCodec codec, ushort actorId, ushort observerId, short ax, short ay, short ox, short oy, uint tick)
    {
        var payload = new byte[AttackRequest.OneTargetPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(22), ax);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(24), ay);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(26), ox);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(28), oy);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), actorId);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), 201);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(AttackRequest.DamageArrayOffset), observerId);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(AttackRequest.DamageArrayOffset + 4), -1);
        return codec.Encode(AttackRequest.OneTargetMessageType, actorId, tick, payload, 42);
    }

    private static void AssertHeal(IReadOnlyList<DecodedFrame> actorFrames, IReadOnlyList<DecodedFrame> observerFrames,
        ushort actorId, ushort observerId, long experience)
    {
        var actorAttacks = actorFrames.Where(IsAttack).ToArray();
        var observerAttacks = observerFrames.Where(IsAttack).ToArray();
        Check(actorAttacks.Length == 2 && observerAttacks.Length == 2,
            $"Expected one synthetic child and one parent on actor/observer streams, got actor={actorAttacks.Length}, observer={observerAttacks.Length}.");
        AssertChild(actorAttacks[0], actorId, actorId, ExpectedHeal, MaximumHp, StartingMp - ChildManaCost, experience);
        AssertParent(actorAttacks[1], actorId, MaximumHp, StartingMp - ChildManaCost, experience);
        AssertChild(observerAttacks[0], actorId, actorId, ExpectedHeal, MaximumHp, StartingMp - ChildManaCost, experience);
        AssertParent(observerAttacks[1], actorId, MaximumHp, StartingMp - ChildManaCost, experience);
        Check(actorAttacks[0].Payload.Span.SequenceEqual(observerAttacks[0].Payload.Span) &&
            actorAttacks[1].Payload.Span.SequenceEqual(observerAttacks[1].Payload.Span),
            "Actor and in-view observer received different Divine Blessing child/parent payloads.");
        Check(!actorFrames.Concat(observerFrames).Any(frame => frame.Header.Type == SetHpMpConfirmation.MessageType),
            "Successful Divine Blessing unexpectedly sent a correction frame.");
    }

    private static void AssertInsufficientMana(IReadOnlyList<DecodedFrame> actorFrames, IReadOnlyList<DecodedFrame> observerFrames,
        ushort actorId, ushort observerId, DecodedFrame distantParent, long experience)
    {
        var actorAttacks = actorFrames.Where(IsAttack).ToArray();
        var observerAttacks = observerFrames.Where(IsAttack).ToArray();
        Check(actorAttacks.Length == 1 && observerAttacks.Length == 1,
            $"Insufficient mana must produce only the parent relay, got actor={actorAttacks.Length}, observer={observerAttacks.Length}.");
        var actorCorrectionIndex = IndexOf(actorFrames, frame => frame.Header.Type == SetHpMpConfirmation.MessageType && frame.Header.Id == actorId);
        var actorParentIndex = IndexOf(actorFrames, IsAttack);
        Check(actorCorrectionIndex >= 0 && actorCorrectionIndex < actorParentIndex,
            "Insufficient-mana correction did not precede the parent relay on the actor stream.");
        var correction = actorFrames[actorCorrectionIndex];
        Check(correction.Header.Size == SetHpMpConfirmation.PacketSize &&
            BinaryPrimitives.ReadInt32LittleEndian(correction.Payload.Span) == StartingHp &&
            BinaryPrimitives.ReadInt32LittleEndian(correction.Payload.Span[4..]) == InsufficientMp,
            "Insufficient-mana correction does not carry authoritative unchanged HP/MP.");
        Check(!observerFrames.Any(frame => frame.Header.Type == SetHpMpConfirmation.MessageType) && IsSkill(actorAttacks[0], 201) &&
            IsSkill(observerAttacks[0], 201), "Private mana correction leaked or an unexpected child AttackOne was relayed.");
        AssertParent(actorAttacks[0], actorId, StartingHp, InsufficientMp, experience);
        AssertParent(observerAttacks[0], actorId, StartingHp, InsufficientMp, experience);
        AssertParent(distantParent, actorId, StartingHp, InsufficientMp, experience);
        Check(actorAttacks[0].Payload.Span.SequenceEqual(observerAttacks[0].Payload.Span), "Actor and observer parent relays differ after insufficient mana.");
    }

    private static void AssertChild(DecodedFrame frame, ushort targetId, ushort actorId, int heal, int hp, int mp, long experience)
    {
        Check(frame.Header.Type == AttackRequest.OneTargetMessageType && frame.Header.Size == AttackRequest.OneTargetPacketSize && frame.IsChecksumValid &&
            IsSkill(frame, 29) && BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[30..]) == actorId &&
            AttackRequest.TryReadDamageSlot(frame, 0, out var slotTarget, out var slotDamage) && slotTarget == targetId && slotDamage == -heal &&
            BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[AttackConfirmation.FakeExpOffset..]) == 0 &&
            BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[AttackConfirmation.CurrentHpOffset..]) == hp &&
            BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[AttackConfirmation.CurrentMpOffset..]) == mp &&
            BinaryPrimitives.ReadInt64LittleEndian(frame.Payload.Span[AttackConfirmation.CurrentExpOffset..]) == experience &&
            BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[AttackConfirmation.RequestedMpOffset..]) == unchecked((short)mp),
            "Synthetic skill-29 AttackOne does not match the authoritative self-heal/resource snapshot.");
    }

    private static void AssertParent(DecodedFrame frame, ushort actorId, int hp, int mp, long experience)
    {
        Check(frame.Header.Type == AttackRequest.OneTargetMessageType && frame.Header.Size == AttackRequest.OneTargetPacketSize && frame.IsChecksumValid &&
            IsSkill(frame, 201) && BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[30..]) == actorId &&
            BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[AttackConfirmation.FakeExpOffset..]) == 0 &&
            BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[AttackConfirmation.CurrentHpOffset..]) == hp &&
            BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[AttackConfirmation.CurrentMpOffset..]) == mp &&
            BinaryPrimitives.ReadInt64LittleEndian(frame.Payload.Span[AttackConfirmation.CurrentExpOffset..]) == experience &&
            BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[AttackConfirmation.RequestedMpOffset..]) == unchecked((short)mp),
            "Parent skill-201 relay does not match its target marker or authoritative actor snapshot.");
    }

    private static async Task DrainUntilQuietAsync(NetworkStream stream, LegacyFrameCodec codec, TimeSpan timeout)
    {
        var quietSince = Stopwatch.GetTimestamp();
        var maximumQuiet = TimeSpan.FromMilliseconds(150);
        for (var count = 0; count < 64;)
        {
            if (!stream.DataAvailable)
            {
                if (Stopwatch.GetElapsedTime(quietSince) >= maximumQuiet) return;
                await Task.Delay(10);
                continue;
            }
            _ = await ReadFrameAsync(stream, codec, timeout);
            count++;
            quietSince = Stopwatch.GetTimestamp();
        }
        throw new InvalidOperationException("Distant observer startup drain exceeded 64 frames.");
    }

    private static async Task<DecodedFrame> ReadFrameAsync(NetworkStream stream, LegacyFrameCodec codec, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        var header = new byte[PacketHeader.SizeInBytes];
        await stream.ReadExactlyAsync(header, cancellation.Token);
        var size = PacketHeader.Read(header).Size;
        Check(size >= PacketHeader.SizeInBytes && size <= PacketHeader.MaxPacketSize, $"Invalid frame size {size} in Divine Blessing smoke.");
        var bytes = new byte[size];
        header.CopyTo(bytes, 0);
        await stream.ReadExactlyAsync(bytes.AsMemory(PacketHeader.SizeInBytes), cancellation.Token);
        var frame = codec.Decode(bytes);
        Check(frame.IsChecksumValid, "Divine Blessing loopback frame checksum failed.");
        return frame;
    }

    private static int IndexOf(IReadOnlyList<DecodedFrame> frames, Func<DecodedFrame, bool> predicate)
    {
        for (var index = 0; index < frames.Count; index++) if (predicate(frames[index])) return index;
        return -1;
    }

    private static bool IsAttack(DecodedFrame frame) => frame.Header.Type is
        AttackRequest.AreaMessageType or AttackRequest.OneTargetMessageType or AttackRequest.TwoTargetMessageType;

    private static bool IsSkill(DecodedFrame frame, int skill) =>
        BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[44..]) == skill;

    private static int Score(DecodedFrame frame, int offset) => BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span.Slice(
        CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + offset, sizeof(int)));

    private static (int X, int Y) Position(DecodedFrame frame) =>
        (BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span), BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[2..]));

    private static ushort Id(DecodedFrame frame) => BinaryPrimitives.ReadUInt16LittleEndian(
        frame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);

    private static long Experience(byte[] account, int slot) => BinaryPrimitives.ReadInt64LittleEndian(
        account.AsSpan(216 + (slot * 816) + CharacterMobV769.ExperienceOffset, sizeof(long)));

    private static async Task AssertExperienceUnchangedAsync(string path, int slot, long expected)
    {
        var account = await File.ReadAllBytesAsync(path);
        Check(Experience(account, slot) == expected, "Divine Blessing changed persisted MOB.Exp.");
    }

    private static AccountHeader Header(byte[] account) => new(
        CString(account.AsSpan(0, 16)), CString(account.AsSpan(16, 12)), account.AsSpan(202, 6).ToArray());

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

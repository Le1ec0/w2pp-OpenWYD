using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using WydCdk.Protocol;

/// <summary>
/// Loopback smoke for the TMProject 7.69 InstanceType 6 heal: a Mortal Foema casts skill 27 (Cura) on the observer.
/// With Special 300 the heal is 300*2+100=700, capped at the 7.69 Mortal limit 638 (the W2PP-based code capped at 1100).
/// </summary>
internal static class AttackSkillHealNetwork
{
    private const int ExpectedHeal = 638;
    private const int PeerStartHp = 1000;
    private const int ClientFakeExpSentinel = 77_777;

    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AutoTradeClosePeerAccountFile!;
        var actorBefore = await File.ReadAllBytesAsync(options.AccountFile);
        var peerBefore = await File.ReadAllBytesAsync(peerFile);
        var peerHeader = new AccountHeader(
            CString(peerBefore.AsSpan(0, 16)), CString(peerBefore.AsSpan(16, 12)), peerBefore.AsSpan(202, 6).ToArray());

        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "ATTACK_HEAL_ACTOR_"))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "ATTACK_HEAL_PEER_"))
        {
            var actorId = Id(actor.CharacterLoginFrame);
            var peerId = Id(peer.CharacterLoginFrame);
            Check(actorId > 0 && peerId > 0 && actorId != peerId, "Heal fixture needs distinct authoritative IDs.");
            Check(Score(peer.CharacterLoginFrame, 24) == PeerStartHp && Score(peer.CharacterLoginFrame, 16) == 5000, "Peer fixture must enter with HP 1000/5000.");
            Check(actor.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.MobOffset + 20] == 1, "Actor fixture must be class 1 (Foema).");
            var actorStart = Position(actor.CharacterLoginFrame);
            var peerStart = Position(peer.CharacterLoginFrame);
            var actorFinal = await MoveToPositionAsync(actor.Stream, codec, actorId,
                (checked((short)actorStart.X), checked((short)actorStart.Y)), 2100, 2170, options.Timeout);
            var peerFinal = await MoveToPositionAsync(peer.Stream, codec, peerId,
                (checked((short)peerStart.X), checked((short)peerStart.Y)), 2105, 2170, options.Timeout);
            await Phase("ATTACK-HEAL-POSITIONED");
            var (ax, ay) = actorFinal;
            var (px, py) = peerFinal;
            Check(Math.Max(Math.Abs(ax - px), Math.Abs(ay - py)) <= 23, $"Heal fixtures spawned out of skill range: ({ax},{ay}) vs ({px},{py}).");
            Check(!IsVillage(px, py), $"Heal target did not leave the village exclusion: ({px},{py}).");
            Console.WriteLine($"ATTACK_HEAL_TARGET_POS={px},{py}");
            Console.WriteLine($"ATTACK_HEAL_ACTOR_ID={actorId}");
            await Phase("ATTACK-HEAL-READY");

            var tick = unchecked((uint)Environment.TickCount64);
            Check(tick > 120_001 && tick < uint.MaxValue - 20_000u, "Local monotonic clock cannot host this smoke's tick sequence.");
            await actor.Stream.WriteAsync(BuildHeal(codec, actorId, peerId, (short)ax, (short)ay, (short)px, (short)py, tick - 50_000u));
            var healed = await Phase("ATTACK-HEAL-DONE");

            var actorRelay = healed.Actor.Where(IsAttack).ToArray();
            var peerRelay = healed.Peer.Where(IsAttack).ToArray();
            Check(actorRelay.Length == 1 && peerRelay.Length == 1 && actorRelay[0].Payload.Span.SequenceEqual(peerRelay[0].Payload.Span),
                "Heal attack was not relayed identically to actor and observer.");
            var slotTarget = BinaryPrimitives.ReadUInt16LittleEndian(actorRelay[0].Payload.Span[AttackRequest.DamageArrayOffset..]);
            var slotDamage = BinaryPrimitives.ReadInt32LittleEndian(actorRelay[0].Payload.Span[(AttackRequest.DamageArrayOffset + 4)..]);
            Console.WriteLine($"ATTACK_HEAL_SLOT={slotTarget},{slotDamage}");
            Check(slotTarget == peerId && slotDamage == -ExpectedHeal, $"Heal relay slot was ({slotTarget},{slotDamage}), expected ({peerId},{-ExpectedHeal}).");
            var relayedFakeExp = BinaryPrimitives.ReadInt32LittleEndian(actorRelay[0].Payload.Span[AttackConfirmation.FakeExpOffset..]);
            var scaledExperience = (long)Math.Min(120, ExpectedHeal >> 3) * options.ExpectedDoubleModeMultiplier;
            var expectedFakeExp = IsVillage(px, py) ? 0 : (int)Math.Clamp(scaledExperience, 0L, 200L);
            Check(relayedFakeExp == expectedFakeExp,
                $"The server relayed FakeExp={relayedFakeExp}, expected the authoritative heal value {expectedFakeExp} at target ({px},{py}); the client sent {ClientFakeExpSentinel}.");
            Console.WriteLine($"ATTACK_HEAL_FAKE_EXP={relayedFakeExp}");
            Check(ReadSlot(actorRelay[0], 2) == (peerId, -1) && ReadSlot(actorRelay[0], 3) == (peerId, -1),
                $"Excess skill markers were not preserved in the authoritative relay: slot2={ReadSlot(actorRelay[0], 2)}, slot3={ReadSlot(actorRelay[0], 3)}.");
            var hpSync = healed.Peer.Where(frame => frame.Header.Type == SetHpMpConfirmation.MessageType && frame.Header.Id == peerId).ToArray();
            Check(hpSync.Length == 1 && BinaryPrimitives.ReadInt32LittleEndian(hpSync[0].Payload.Span) == PeerStartHp + ExpectedHeal,
                "Observer did not receive SetHpMp with HP 1638.");
            Console.WriteLine($"ATTACK_HEAL_PEER_HP={PeerStartHp + ExpectedHeal}");

            await actor.LogoutAsync();
            await peer.LogoutAsync();

            Task<(IReadOnlyList<DecodedFrame> Actor, IReadOnlyList<DecodedFrame> Peer)> Phase(string label)
                => AutoTradeCloseNetwork.CapturePhase(actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, label, peerFirst: false);
        }

        await MotionGateNetwork.AssertDisk(options.AccountFile, actorBefore, options.Slot);
        await MotionGateNetwork.AssertDisk(peerFile, peerBefore, options.Slot);
            Console.WriteLine("CLIENT_E2E_PASS attack-skill-heal type28-excess-slots-preserved-mortal-cap-638-relay-and-hp-sync");
    }

    private static byte[] BuildHeal(LegacyFrameCodec codec, ushort actorId, ushort peerId, short ax, short ay, short px, short py, uint tick)
    {
        var payload = new byte[AttackRequest.AreaPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(AttackConfirmation.FakeExpOffset), ClientFakeExpSentinel);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(22), ax);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(24), ay);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(26), px);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(28), py);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), actorId);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), 27);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(AttackRequest.DamageArrayOffset), peerId);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(AttackRequest.DamageArrayOffset + 4), -1); // skill intent
        SetSkillSlot(payload, 2, peerId);
        SetSkillSlot(payload, 3, peerId);
        return codec.Encode(AttackRequest.AreaMessageType, actorId, tick, payload, 42);
    }

    private static async Task<(short X, short Y)> MoveToPositionAsync(
        NetworkStream stream, LegacyFrameCodec codec, ushort id, (short X, short Y) start,
        short targetX, short targetY, TimeSpan timeout)
    {
        var currentX = start.X;
        var currentY = start.Y;
        ActionRequest? last = null;
        var steps = Math.Max(1, (Math.Max(Math.Abs(targetX - currentX), Math.Abs(targetY - currentY)) + 32) / 33);
        for (var step = 1; step <= steps; step++)
        {
            var nextX = checked((short)(start.X + ((targetX - start.X) * step / steps)));
            var nextY = checked((short)(start.Y + ((targetY - start.Y) * step / steps)));
            last = new ActionRequest(currentX, currentY, 0, 0, new byte[24], nextX, nextY);
            await stream.WriteAsync(last.ToFrame(codec, unchecked((uint)Environment.TickCount64), 23, id));
            currentX = nextX;
            currentY = nextY;
        }

        if (last is null)
            throw new InvalidOperationException("Heal movement route did not emit any packets.");

        using var cancellation = new CancellationTokenSource(timeout);
        for (var index = 0; index < 128; index++)
        {
            var header = new byte[PacketHeader.SizeInBytes];
            await stream.ReadExactlyAsync(header, cancellation.Token);
            var size = PacketHeader.Read(header).Size;
            Check(size >= PacketHeader.SizeInBytes && size <= PacketHeader.MaxPacketSize,
                "Heal movement echo has an invalid frame size.");
            var bytes = new byte[size];
            header.CopyTo(bytes, 0);
            await stream.ReadExactlyAsync(bytes.AsMemory(PacketHeader.SizeInBytes), cancellation.Token);
            var frame = codec.Decode(bytes);
            Check(frame.IsChecksumValid, "Heal movement echo checksum is invalid.");
            if (frame.Header.Id != id || !IsAction(frame) || !ActionRequest.TryParse(frame, out var echoed) || echoed is null)
                continue;
            if (echoed.TargetX == last.TargetX && echoed.TargetY == last.TargetY)
                return (echoed.TargetX, echoed.TargetY);
        }

        throw new InvalidOperationException($"Heal movement did not receive the server echo for ({targetX},{targetY}).");
    }

    private static void SetSkillSlot(Span<byte> payload, int index, ushort targetId)
    {
        var offset = AttackRequest.DamageArrayOffset + (index * AttackRequest.DamageSlotSize);
        BinaryPrimitives.WriteUInt16LittleEndian(payload[offset..], targetId);
        BinaryPrimitives.WriteInt32LittleEndian(payload[(offset + 4)..], -1); // excess skill marker
    }

    private static (ushort TargetId, int Damage) ReadSlot(DecodedFrame frame, int index)
    {
        Check(AttackRequest.TryReadDamageSlot(frame, index, out var targetId, out var damage),
            $"Could not read attack wire slot {index}.");
        return (targetId, damage);
    }

    private static (int X, int Y) Position(DecodedFrame login) =>
        (BinaryPrimitives.ReadInt16LittleEndian(login.Payload.Span), BinaryPrimitives.ReadInt16LittleEndian(login.Payload.Span[2..]));

    private static int Score(DecodedFrame frame, int offset) => BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span.Slice(
        CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + offset, sizeof(int)));

    private static ushort Id(DecodedFrame frame) => BinaryPrimitives.ReadUInt16LittleEndian(
        frame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);

    private static bool IsAttack(DecodedFrame frame) => frame.Header.Type is
        AttackRequest.AreaMessageType or AttackRequest.OneTargetMessageType or AttackRequest.TwoTargetMessageType;

    private static bool IsAction(DecodedFrame frame) => frame.Header.Type is
        ActionRequest.MessageType or ActionRequest.StopMessageType or ActionRequest.IllusionMessageType;

    private static bool IsVillage(int x, int y) =>
        x is >= 2052 and <= 2171 && y is >= 2052 and <= 2163 ||
        x is >= 2432 and <= 2675 && y is >= 1672 and <= 1767 ||
        x is >= 2448 and <= 2476 && y is >= 1966 and <= 2024 ||
        x is >= 3605 and <= 3690 && y is >= 3090 and <= 3260 ||
        x is >= 1036 and <= 1072 && y is >= 1700 and <= 1760;

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

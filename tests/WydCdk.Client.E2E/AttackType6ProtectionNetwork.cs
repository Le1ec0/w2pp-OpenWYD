using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;

internal static class AttackType6ProtectionNetwork
{
    private const ushort InputProgress = 321;

    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AutoTradeClosePeerAccountFile
            ?? throw new InvalidOperationException("Type 6 protection smoke requires an isolated peer account.");
        var actorBefore = await File.ReadAllBytesAsync(options.AccountFile);
        var peerBefore = await File.ReadAllBytesAsync(peerFile);
        Check(HasType6Affect(peerBefore, options.Slot),
            "The Type 6 target fixture must contain Affect.Type 6 in one of its 32 slots.");
        var peerHeader = new AccountHeader(
            CString(peerBefore.AsSpan(0, 16)), CString(peerBefore.AsSpan(16, 12)), peerBefore.AsSpan(202, 6).ToArray());

        await using var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "TYPE6_ACTOR_");
        await using var peer = await E2eSession.ConnectAndLoginAsync(
            options with { AccountFile = peerFile }, peerHeader, codec, "TYPE6_PEER_");

        var actorId = Id(actor.CharacterLoginFrame);
        var peerId = Id(peer.CharacterLoginFrame);
        Check(actorId > 0 && peerId > 0 && actorId != peerId,
            "Type 6 protection smoke requires distinct authoritative participant IDs.");
        var actorHp = Hp(actor.CharacterLoginFrame);
        var peerHp = Hp(peer.CharacterLoginFrame);
        Check(actorHp > 0 && peerHp == 100_000,
            $"Type 6 protection fixtures must be alive and the target must start at 100000 HP: actor={actorHp}, peer={peerHp}.");
        Check(peer.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.MobOffset + 20] == 0,
            "The Type 6 protection target must be class 0.");

        var currentTick = unchecked((uint)Environment.TickCount64);
        Check(currentTick > 120_001 && currentTick < uint.MaxValue - 40_000u,
            "Local monotonic clock cannot host the Type 6 protection smoke tick.");
        await actor.Stream.WriteAsync(BuildAttack(codec, actorId, peerId, InputProgress, currentTick - 60_000u));

        var captured = await AutoTradeCloseNetwork.CapturePhase(
            actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, "TYPE6-PROTECTION", peerFirst: false);
        var actorAttacks = captured.Actor.Where(IsAttack).ToArray();
        var peerAttacks = captured.Peer.Where(IsAttack).ToArray();
        Check(actorAttacks.Length == 1 && peerAttacks.Length == 1 &&
            actorAttacks[0].Payload.Span.SequenceEqual(peerAttacks[0].Payload.Span),
            "The protected physical action was not relayed once with the same authoritative payload to both participants.");
        Check(ReadSlot(actorAttacks[0], 0) == (0, 0), "The empty physical slot 0 changed.");
        Check(ReadSlot(actorAttacks[0], 1) == (peerId, 0) && ReadSlot(actorAttacks[0], 2) == (peerId, 0),
            $"Type 6 did not neutralize both physical batch slots: slot1={ReadSlot(actorAttacks[0], 1)}, slot2={ReadSlot(actorAttacks[0], 2)}.");
        Check(!captured.Peer.Any(frame => frame.Header.Type == SetHpMpConfirmation.MessageType && frame.Header.Id == peerId),
            "A fully protected action emitted a target HP/MP mutation frame.");
        Check(Hp(peer.CharacterLoginFrame) == 100_000,
            "The Type 6 protection smoke changed the peer's login HP snapshot.");

        var actorClass = actor.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.MobOffset + 20];
        Check(actorClass <= 3, $"Type 6 skill smoke requires a playable attacker class, got {actorClass}.");
        var skill = checked((short)(actorClass * 24 + 10));
        await actor.Stream.WriteAsync(BuildSkillAttack(codec, actorId, peerId, skill, currentTick - 59_000u));
        var skillCaptured = await AutoTradeCloseNetwork.CapturePhase(
            actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, "TYPE6-SKILL-PROTECTION", peerFirst: false);
        var actorSkillAttacks = skillCaptured.Actor.Where(IsAttack).ToArray();
        var peerSkillAttacks = skillCaptured.Peer.Where(IsAttack).ToArray();
        Check(actorSkillAttacks.Length == 1 && peerSkillAttacks.Length == 1 &&
            actorSkillAttacks[0].Payload.Span.SequenceEqual(peerSkillAttacks[0].Payload.Span),
            "The Type 6 skill action was not relayed once with the same authoritative payload to both participants.");
        Check(BinaryPrimitives.ReadInt16LittleEndian(actorSkillAttacks[0].Payload.Span[44..]) == skill &&
            ReadSlot(actorSkillAttacks[0], 0) == (peerId, 0),
            $"Type 6 did not neutralize the class-{actorClass} damaging skill: skill={skill}, slot={ReadSlot(actorSkillAttacks[0], 0)}.");
        Check(!skillCaptured.Peer.Any(frame => frame.Header.Type == SetHpMpConfirmation.MessageType && frame.Header.Id == peerId),
            "A Type 6 protected skill emitted a target HP/MP mutation frame.");

        Console.WriteLine($"ATTACK_TYPE6_ACTOR_ID={actorId}");
        Console.WriteLine($"ATTACK_TYPE6_PEER_ID={peerId}");
        Console.WriteLine($"ATTACK_TYPE6_SKILL={skill}");
        Console.WriteLine("CLIENT_E2E_PASS attack-type6-physical-and-skill-damage-zero-hp-unchanged");
        await actor.LogoutAsync();
        await peer.LogoutAsync();
        await MotionGateNetwork.AssertDisk(options.AccountFile, actorBefore, options.Slot);
        await MotionGateNetwork.AssertDisk(peerFile, peerBefore, options.Slot);
    }

    private static byte[] BuildAttack(LegacyFrameCodec codec, ushort actorId, ushort peerId, ushort progress, uint tick)
    {
        var payload = new byte[AttackRequest.AreaPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), actorId);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(AttackRequest.ProgressOffset), progress);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), -1);
        SetPhysicalSlot(payload, 1, peerId);
        SetPhysicalSlot(payload, 2, peerId);
        var bytes = codec.Encode(AttackRequest.AreaMessageType, actorId, tick, payload, 42);
        var frame = codec.Decode(bytes);
        Check(AttackRequest.TryParse(frame, out var parsed) && parsed is
            { MessageType: AttackRequest.AreaMessageType, SkillIndex: -1, Progress: InputProgress } &&
            ReadSlot(frame, 0) == (0, 0) && ReadSlot(frame, 1) == (peerId, -2) && ReadSlot(frame, 2) == (peerId, -2),
            "Smoke did not build the intended checksum-valid physical action with slots 1 and 2 only.");
        return bytes;
    }

    private static byte[] BuildSkillAttack(LegacyFrameCodec codec, ushort actorId, ushort peerId, short skill, uint tick)
    {
        var payload = new byte[AttackRequest.OneTargetPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), actorId);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), skill);
        var slotOffset = AttackRequest.DamageArrayOffset;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(slotOffset), peerId);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(slotOffset + 4), -1);
        var bytes = codec.Encode(AttackRequest.OneTargetMessageType, actorId, tick, payload, 42);
        Check(AttackRequest.TryParse(codec.Decode(bytes), out var parsed) && parsed is
            { MessageType: AttackRequest.OneTargetMessageType, SkillIndex: var parsedSkill } && parsedSkill == skill &&
            ReadSlot(codec.Decode(bytes), 0) == (peerId, -1),
            "Smoke did not build the intended checksum-valid Type 6 skill hit.");
        return bytes;
    }

    private static void SetPhysicalSlot(Span<byte> payload, int index, ushort targetId)
    {
        var offset = AttackRequest.DamageArrayOffset + (index * AttackRequest.DamageSlotSize);
        BinaryPrimitives.WriteUInt16LittleEndian(payload[offset..], targetId);
        BinaryPrimitives.WriteInt32LittleEndian(payload[(offset + 4)..], -2);
    }

    private static (ushort TargetId, int Damage) ReadSlot(DecodedFrame frame, int index)
    {
        Check(AttackRequest.TryReadDamageSlot(frame, index, out var targetId, out var damage),
            $"Could not read attack wire slot {index}.");
        return (targetId, damage);
    }

    private static int Hp(DecodedFrame frame) => BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span.Slice(
        CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + 24, sizeof(int)));

    private static bool HasType6Affect(ReadOnlySpan<byte> accountFile, int characterSlot)
    {
        const int accountAffectOffset = 4_572;
        const int affectStride = 256;
        const int affectCount = 32;
        var start = accountAffectOffset + (characterSlot * affectStride);
        if (start < 0 || accountFile.Length < start + affectStride) return false;
        for (var index = 0; index < affectCount; index++)
            if (accountFile[start + (index * 8)] == 6)
                return true;
        return false;
    }

    private static ushort Id(DecodedFrame frame) => BinaryPrimitives.ReadUInt16LittleEndian(
        frame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);

    private static bool IsAttack(DecodedFrame frame) => frame.Header.Type is
        AttackRequest.AreaMessageType or AttackRequest.OneTargetMessageType or AttackRequest.TwoTargetMessageType;

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

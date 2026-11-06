using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;

internal static class AttackMixedSlotsNetwork
{
    private const ushort InputProgress = 321;
    private const int ExpectedHeal = 638;

    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AutoTradeClosePeerAccountFile
            ?? throw new InvalidOperationException("Mixed-slot Attack smoke requires an isolated peer account.");
        var farFile = options.AttackMixedOutOfRangeAccountFile
            ?? throw new InvalidOperationException("Mixed-slot abort smoke requires a third isolated out-of-range account.");
        var actorBefore = await File.ReadAllBytesAsync(options.AccountFile);
        var peerBefore = await File.ReadAllBytesAsync(peerFile);
        var farBefore = await File.ReadAllBytesAsync(farFile);
        var peerHeader = new AccountHeader(
            CString(peerBefore.AsSpan(0, 16)), CString(peerBefore.AsSpan(16, 12)), peerBefore.AsSpan(202, 6).ToArray());
        var farHeader = new AccountHeader(
            CString(farBefore.AsSpan(0, 16)), CString(farBefore.AsSpan(16, 12)), farBefore.AsSpan(202, 6).ToArray());

        await using var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "MIXED_ATTACK_ACTOR_");
        await using var peer = await E2eSession.ConnectAndLoginAsync(
            options with { AccountFile = peerFile }, peerHeader, codec, "MIXED_ATTACK_PEER_");
        await using var far = await E2eSession.ConnectAndLoginAsync(
            options with { AccountFile = farFile }, farHeader, codec, "MIXED_ATTACK_FAR_");

        var actorId = Id(actor.CharacterLoginFrame);
        var peerId = Id(peer.CharacterLoginFrame);
        var farId = Id(far.CharacterLoginFrame);
        Check(actorId > 0 && peerId > 0 && farId > 0 && actorId != peerId && actorId != farId && peerId != farId,
            "Mixed-slot smoke requires three distinct authoritative participants.");
        Check(actor.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.MobOffset + 20] == 1,
            "Mixed-slot actor must be a Foema with Cura learned.");
        Check(Score(peer.CharacterLoginFrame, 24) == 1000 && Score(peer.CharacterLoginFrame, 16) == 5000,
            "Mixed-slot peer must enter with HP 1000/5000.");
        Check(InRange(actor.CharacterLoginFrame, peer.CharacterLoginFrame), "Mixed-slot fixtures are outside the physical/skill range.");
        var actorPosition = Position(actor.CharacterLoginFrame);
        var farPosition = Position(far.CharacterLoginFrame);
        var farTargetX = checked((short)(actorPosition.X + 30));
        Check(farTargetX is > 0 and < 4096 && Math.Max(Math.Abs(farTargetX - farPosition.X), Math.Abs(actorPosition.Y - farPosition.Y)) <= 33,
            "The third fixture cannot reach the deterministic out-of-range smoke position in one accepted Action.");
        var farMove = new ActionRequest(farPosition.X, farPosition.Y, 0, 0, new byte[24], farTargetX, actorPosition.Y);
        var farMoveTick = unchecked((uint)Environment.TickCount64);
        await far.Stream.WriteAsync(farMove.ToFrame(codec, farMoveTick, 19, farId));
        var farMoveEcho = await ReadMatchingActionAsync(far.Stream, codec, farMove, farMoveTick, farId, options.Timeout);
        ActionGateNetwork.AssertRelay([farMoveEcho], farMove, farId, farMoveTick);
        Check(Math.Abs(farTargetX - actorPosition.X) > 23,
            "The third fixture did not move beyond physical attack range.");

        var tick = unchecked((uint)Environment.TickCount64);
        Check(tick > 120_001 && tick < uint.MaxValue - 20_000u, "Local monotonic clock cannot host the mixed-slot smoke tick.");
        await actor.Stream.WriteAsync(BuildAttack(codec, actorId, peerId, peerId, 27, InputProgress, tick - 60_000u,
            physicalFirst: true, includeSkill: true, includeInvalidMarker: true));
        var mixed = await AutoTradeCloseNetwork.CapturePhase(
            actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, "MIXED-VALID", peerFirst: false);
        var actorAttacks = mixed.Actor.Where(IsAttack).ToArray();
        var peerAttacks = mixed.Peer.Where(IsAttack).ToArray();
        Check(actorAttacks.Length == 1 && peerAttacks.Length == 1,
            $"One mixed action must produce one response per participant: actor={actorAttacks.Length}, peer={peerAttacks.Length}.");
        var actorAttack = actorAttacks[0];
        var peerAttack = peerAttacks[0];
        Check(actorAttack.Header.Type == AttackRequest.AreaMessageType && actorAttack.Header.Size == AttackRequest.AreaPacketSize &&
            actorAttack.Payload.Span.SequenceEqual(peerAttack.Payload.Span),
            "Mixed action changed the area wire shape or produced different participant payloads.");
        var currentMp = BinaryPrimitives.ReadInt32LittleEndian(actorAttack.Payload.Span[AttackConfirmation.CurrentMpOffset..]);
        var requestedMp = BinaryPrimitives.ReadInt16LittleEndian(actorAttack.Payload.Span[AttackConfirmation.RequestedMpOffset..]);
        Check(requestedMp == unchecked((short)currentMp), "Mixed area attack did not narrow authoritative CurrentMp into ReqMp.");
        var physicalSlot = ReadSlot(actorAttack, 0);
        var skillSlot = ReadSlot(actorAttack, 1);
        var invalidMarkerSlot = ReadSlot(actorAttack, 2);
        Check(physicalSlot.TargetId == peerId && physicalSlot.Damage > 0,
            $"Physical -2 at slot 0 was not resolved: {physicalSlot}.");
        Check(skillSlot == (peerId, -ExpectedHeal), $"Skill -1 at slot 1 was not resolved as the 638 heal: {skillSlot}.");
        Check(invalidMarkerSlot == (peerId, 0),
            $"Invalid nonzero marker at slot 2 was not relayed as zero damage with its target: {invalidMarkerSlot}.");
        Check(BinaryPrimitives.ReadUInt16LittleEndian(actorAttack.Payload.Span[AttackRequest.ProgressOffset..]) != InputProgress,
            "A physical slot at index 0 did not resolve the shared Progress state.");
        Check(actorAttack.Payload.Span[36] == 0, "The relay echoed client-controlled DoubleCritical.");

        var peerResourceIndex = IndexOf(mixed.Peer, frame => frame.Header.Type == SetHpMpConfirmation.MessageType && frame.Header.Id == peerId);
        var peerAttackIndex = Array.FindIndex(mixed.Peer.ToArray(), IsAttack);
        Check(peerResourceIndex >= 0 && peerAttackIndex >= 0 && peerAttackIndex < peerResourceIndex,
            "The aggregate AttackConfirmation must reach the peer before its deferred skill-resource frame.");
        var expectedPeerHp = 1000 - physicalSlot.Damage + ExpectedHeal;
        var peerHp = BinaryPrimitives.ReadInt32LittleEndian(mixed.Peer[peerResourceIndex].Payload.Span);
        Check(peerHp == expectedPeerHp, $"Peer HP was {peerHp}; physical damage and the later skill should leave {expectedPeerHp}.");
        var peerHpAfterFirstMixed = peerHp;

        var reversedTick = unchecked(tick - 58_000u);
        await actor.Stream.WriteAsync(BuildAttack(codec, actorId, peerId, peerId, 27, InputProgress, reversedTick,
            physicalFirst: false, includeSkill: true));
        var reversed = await AutoTradeCloseNetwork.CapturePhase(
            actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, "MIXED-SKILL-FIRST", peerFirst: false);
        actorAttacks = reversed.Actor.Where(IsAttack).ToArray();
        peerAttacks = reversed.Peer.Where(IsAttack).ToArray();
        Check(actorAttacks.Length == 1 && peerAttacks.Length == 1 && actorAttacks[0].Payload.Span.SequenceEqual(peerAttacks[0].Payload.Span),
            "A skill-first mixed action did not produce one identical aggregate response per participant.");
        var reversedSkill = ReadSlot(actorAttacks[0], 0);
        var reversedPhysical = ReadSlot(actorAttacks[0], 1);
        Check(reversedSkill == (peerId, -ExpectedHeal) && reversedPhysical.TargetId == peerId && reversedPhysical.Damage > 0,
            $"Skill-first wire slots lost their original order: skill={reversedSkill}, physical={reversedPhysical}.");
        Check(BinaryPrimitives.ReadUInt16LittleEndian(actorAttacks[0].Payload.Span[AttackRequest.ProgressOffset..]) == InputProgress,
            "A physical slot after skill slot 0 incorrectly advanced action Progress.");
        var reversedPeerResources = reversed.Peer
            .Where(frame => frame.Header.Type == SetHpMpConfirmation.MessageType && frame.Header.Id == peerId)
            .ToArray();
        var reversedAttackIndex = Array.FindIndex(reversed.Peer.ToArray(), IsAttack);
        var firstReversedResourceIndex = IndexOf(reversed.Peer,
            frame => frame.Header.Type == SetHpMpConfirmation.MessageType && frame.Header.Id == peerId);
        Check(reversedPeerResources.Length == 2 && reversedAttackIndex >= 0 && firstReversedResourceIndex > reversedAttackIndex,
            "Skill-first and physical effects must both follow the single aggregate attack relay.");
        expectedPeerHp += ExpectedHeal - reversedPhysical.Damage;
        peerHp = BinaryPrimitives.ReadInt32LittleEndian(reversedPeerResources[^1].Payload.Span);
        Check(peerHp == expectedPeerHp, $"Skill-first peer HP was {peerHp}; expected {expectedPeerHp}.");

        // Invalid SkillIndex must leave the -1 marker untouched while still processing the -2 physical slot.
        var invalidTick = unchecked(tick - 56_000u);
        await actor.Stream.WriteAsync(BuildAttack(codec, actorId, peerId, peerId, short.MaxValue, InputProgress, invalidTick,
            physicalFirst: true, includeSkill: true));
        var invalid = await AutoTradeCloseNetwork.CapturePhase(
            actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, "MIXED-INVALID-SKILL", peerFirst: false);
        actorAttacks = invalid.Actor.Where(IsAttack).ToArray();
        peerAttacks = invalid.Peer.Where(IsAttack).ToArray();
        Check(actorAttacks.Length == 1 && peerAttacks.Length == 1 && actorAttacks[0].Payload.Span.SequenceEqual(peerAttacks[0].Payload.Span),
            "An invalid skill header suppressed the physical slot or produced duplicate/mismatched relays.");
        Check(ReadSlot(actorAttacks[0], 0) is { TargetId: var physicalTarget, Damage: > 0 } && physicalTarget == peerId,
            "A physical -2 marker was not processed when SkillIndex had no definition.");
        Check(ReadSlot(actorAttacks[0], 1) == (peerId, -1), "An invalid skill -1 marker was changed or treated as authoritative damage.");
        var invalidPhysical = ReadSlot(actorAttacks[0], 0);

        // A prior skill effect remains immediate on a later physical range abort, while the final attack relay is suppressed.
        await actor.Stream.WriteAsync(BuildAttack(codec, actorId, farId, peerId, 27, InputProgress,
            unchecked(tick - 54_000u), physicalFirst: false, includeSkill: true));
        var aborted = await AutoTradeCloseNetwork.CapturePhase(
            actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, "MIXED-PHYSICAL-ABORT", peerFirst: false);
        var abortActorAttacks = aborted.Actor.Where(IsAttack).ToArray();
        var abortPeerAttacks = aborted.Peer.Where(IsAttack).ToArray();
        Check(abortActorAttacks.Length == 0 && abortPeerAttacks.Length == 0,
            $"Physical OutOfRange abort must suppress the final AttackConfirmation: actor={abortActorAttacks.Length}, peer={abortPeerAttacks.Length}.");
        var abortPeerResources = aborted.Peer.Where(frame => frame.Header.Type == SetHpMpConfirmation.MessageType && frame.Header.Id == peerId).ToArray();
        var expectedAbortHp = expectedPeerHp - invalidPhysical.Damage + ExpectedHeal;
        Check(abortPeerResources.Length == 1 && BinaryPrimitives.ReadInt32LittleEndian(abortPeerResources[0].Payload.Span) == expectedAbortHp,
            "The prior skill's immediate resource effect was not relayed after the later physical range abort.");

        Console.WriteLine($"ATTACK_MIXED_SLOT_IDS={actorId},{peerId},{farId}");
        Console.WriteLine($"ATTACK_MIXED_SLOT_VALID={physicalSlot.TargetId},{physicalSlot.Damage};{skillSlot.TargetId},{skillSlot.Damage};peerHp={peerHpAfterFirstMixed};reqMp={requestedMp};resourceAfterAttack=True");
        Console.WriteLine($"ATTACK_MIXED_SLOT_REVERSED={reversedSkill.TargetId},{reversedSkill.Damage};{reversedPhysical.TargetId},{reversedPhysical.Damage};skillSlot=0;physicalSlot=1;progress={InputProgress}");
        Console.WriteLine("ATTACK_MIXED_SLOT_INVALID_SKILL=physical-processed;skill-marker-preserved");
        Console.WriteLine($"ATTACK_MIXED_SLOT_ABORT=confirmation-suppressed;prior-skill-peer-hp={expectedAbortHp}");
        Console.WriteLine("CLIENT_E2E_PASS attack-mixed-physical-skill-one-batch-one-relay-order-and-invalid-skill-marker");
        await actor.LogoutAsync();
        await peer.LogoutAsync();
        await far.LogoutAsync();

        await MotionGateNetwork.AssertDisk(options.AccountFile, actorBefore, options.Slot);
        await MotionGateNetwork.AssertDisk(peerFile, peerBefore, options.Slot);
        await MotionGateNetwork.AssertDisk(farFile, farBefore, options.Slot);
    }

    private static byte[] BuildAttack(
        LegacyFrameCodec codec,
        ushort actorId,
        ushort physicalTargetId,
        ushort skillTargetId,
        short skillIndex,
        ushort progress,
        uint tick,
        bool physicalFirst,
        bool includeSkill,
        bool includeInvalidMarker = false)
    {
        var payload = new byte[AttackRequest.AreaPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), actorId);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(AttackRequest.ProgressOffset), progress);
        payload[36] = 0xFF;
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), skillIndex);
        SetSlot(payload, 0, physicalFirst ? physicalTargetId : skillTargetId, physicalFirst ? -2 : -1);
        if (includeSkill)
            SetSlot(payload, 1, physicalFirst ? skillTargetId : physicalTargetId, physicalFirst ? -1 : -2);
        if (includeInvalidMarker)
            SetSlot(payload, 2, physicalTargetId, 75);

        var bytes = codec.Encode(AttackRequest.AreaMessageType, actorId, tick, payload, 42);
        var frame = codec.Decode(bytes);
        var expectedPhysical = physicalFirst ? 0 : 1;
        var expectedSkill = 1 - expectedPhysical;
        Check(AttackRequest.TryParse(frame, out var parsed) && parsed?.SkillIndex == skillIndex && parsed.Progress == progress &&
            AttackRequest.GetTargetCount(frame.Header.Type) == 13 &&
            ReadSlot(frame, expectedPhysical) == (physicalTargetId, -2) && ReadSlot(frame, expectedSkill) == (skillTargetId, -1),
            "Smoke did not build the intended checksum-valid mixed 13-slot packet.");
        return bytes;
    }

    private static void SetSlot(Span<byte> payload, int index, ushort targetId, int marker)
    {
        var offset = AttackRequest.DamageArrayOffset + index * AttackRequest.DamageSlotSize;
        BinaryPrimitives.WriteUInt16LittleEndian(payload[offset..], targetId);
        BinaryPrimitives.WriteInt32LittleEndian(payload[(offset + 4)..], marker);
    }

    private static (ushort TargetId, int Damage) ReadSlot(DecodedFrame frame, int index)
    {
        Check(AttackRequest.TryReadDamageSlot(frame, index, out var targetId, out var damage), $"Could not read attack slot {index}.");
        return (targetId, damage);
    }

    private static int IndexOf(IReadOnlyList<DecodedFrame> frames, Func<DecodedFrame, bool> predicate)
    {
        for (var index = 0; index < frames.Count; index++)
            if (predicate(frames[index])) return index;
        return -1;
    }

    private static bool InRange(DecodedFrame actor, DecodedFrame peer)
    {
        var actorX = BinaryPrimitives.ReadInt16LittleEndian(actor.Payload.Span);
        var actorY = BinaryPrimitives.ReadInt16LittleEndian(actor.Payload.Span[2..]);
        var peerX = BinaryPrimitives.ReadInt16LittleEndian(peer.Payload.Span);
        var peerY = BinaryPrimitives.ReadInt16LittleEndian(peer.Payload.Span[2..]);
        return Math.Max(Math.Abs(actorX - peerX), Math.Abs(actorY - peerY)) <= 23;
    }

    private static int Score(DecodedFrame frame, int offset) => BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span.Slice(
        CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + offset, sizeof(int)));

    private static ushort Id(DecodedFrame frame) => BinaryPrimitives.ReadUInt16LittleEndian(
        frame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);

    private static (short X, short Y) Position(DecodedFrame frame) => (
        BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span),
        BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[2..]));

    private static async Task<DecodedFrame> ReadMatchingActionAsync(
        System.Net.Sockets.NetworkStream stream,
        LegacyFrameCodec codec,
        ActionRequest expected,
        uint tick,
        ushort id,
        TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var headerBytes = new byte[PacketHeader.SizeInBytes];
            await stream.ReadExactlyAsync(headerBytes, cancellation.Token);
            var header = PacketHeader.Read(headerBytes);
            Check(header.Size is >= PacketHeader.SizeInBytes and <= PacketHeader.MaxPacketSize, "Invalid movement response frame size.");
            var bytes = new byte[header.Size];
            headerBytes.CopyTo(bytes, 0);
            await stream.ReadExactlyAsync(bytes.AsMemory(PacketHeader.SizeInBytes), cancellation.Token);
            var frame = codec.Decode(bytes);
            Check(frame.IsChecksumValid, "Movement response checksum is invalid.");
            if (frame.Header.Type != expected.WireType || frame.Header.Id != id || frame.Header.ClientTick != tick)
                continue;
            Check(frame.Payload.Span.SequenceEqual(expected.ToPayload()), "Server movement echo changed the staged far-target position.");
            return frame;
        }
        throw new InvalidOperationException("The staged out-of-range participant did not receive its accepted movement echo.");
    }

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

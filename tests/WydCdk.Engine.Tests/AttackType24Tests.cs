using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackType24Tests
{
    public static Task Run()
    {
        Check(LegacyAttackType24Gate.CrackErrorPoints == 10 && LegacyAttackType24Gate.CrackErrorType == 24,
            "Type24 must use 10 crack-error points.");
        Check(LegacyCrackErrorRules.Add(0, LegacyAttackType24Gate.CrackErrorPoints, LegacyAttackType24Gate.CrackErrorType) is
                { TotalPoints: 10, AddedPoints: 10, Type: 24, ShouldLog: true, RequiresCharacterLogout: false },
            "Type24 did not use the shared crack-error counter policy.");
        Check(LegacyAttackType24Gate.ShouldRejectSecondaryPhysicalTarget(1, true, 0, 0),
            "A short second physical target without an exemption was not rejected.");
        Check(!LegacyAttackType24Gate.ShouldRejectSecondaryPhysicalTarget(0, true, 0, 0) &&
            !LegacyAttackType24Gate.ShouldRejectSecondaryPhysicalTarget(1, false, 0, 0) &&
            !LegacyAttackType24Gate.ShouldRejectSecondaryPhysicalTarget(1, true, 3, 0) &&
            !LegacyAttackType24Gate.ShouldRejectSecondaryPhysicalTarget(1, true, 0, 0x40),
            "Type24 rejected a primary, full-size, class-3, or learned-skill-exempt target.");

        var blocked = ResolveShortTwoTargetBatch(attackerClass: 0, learnedSkill: 0, shortPacket: true);
        Check(blocked.Batch.SlotOutcomes[0] is { PhysicalResult: LegacyPhysicalAttackResult.Accepted, PhysicalOutcome: not null } &&
            blocked.Batch.SlotOutcomes[1] is { PhysicalResult: LegacyPhysicalAttackResult.ShortPacketSecondaryTargetBlocked, PhysicalOutcome: null },
            "A short second physical target did not add a type24 result after the first target.");
        Check(blocked.Hub.TryGetCombatState(2, out var firstTarget) && firstTarget!.CurrentScore.Hp < 100_000 &&
            blocked.Hub.TryGetCombatState(3, out var secondTarget) && secondTarget!.CurrentScore.Hp == 100_000,
            "Type24 did not preserve the first hit while leaving the blocked second target unchanged.");
        VerifyBlockedSlotRelay(blocked.Batch);

        Check(ResolveShortTwoTargetBatch(attackerClass: 3, learnedSkill: 0, shortPacket: true)
                .Batch.SlotOutcomes[1].PhysicalResult == LegacyPhysicalAttackResult.Accepted,
            "Class 3 did not bypass the type24 gate.");
        Check(ResolveShortTwoTargetBatch(attackerClass: 0, learnedSkill: 0x40, shortPacket: true)
                .Batch.SlotOutcomes[1].PhysicalResult == LegacyPhysicalAttackResult.Accepted,
            "Learned skill bit 0x40 did not bypass the type24 gate.");
        Check(ResolveShortTwoTargetBatch(attackerClass: 0, learnedSkill: 0, shortPacket: false)
                .Batch.SlotOutcomes[1].PhysicalResult == LegacyPhysicalAttackResult.Accepted,
            "A full-size two-target attack was incorrectly blocked by type24.");

        return Task.CompletedTask;
    }

    private static (WorldHub Hub, LegacyAttackActionBatchOutcome Batch) ResolveShortTwoTargetBatch(
        byte attackerClass,
        uint learnedSkill,
        bool shortPacket)
    {
        var attackerMob = new byte[LegacyAccountSnapshot.CharacterStride];
        attackerMob[LegacyAccountSnapshot.MobClassOffset] = attackerClass;
        BinaryPrimitives.WriteUInt32LittleEndian(
            attackerMob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), learnedSkill);
        new LegacyScore(100, 0, 1000, 0, 0, 0, 0, 5000, 5000, 5000, 5000, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));

        var targetMob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(100_000, 10, 0, 0, 0, 0, 0, 100_000, 100_000, 100_000, 100_000, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));

        var hub = new WorldHub();
        Check(hub.Enter(1, $"TYPE24_ATTACKER_{attackerClass}_{learnedSkill}_{shortPacket}", (_, _) => ValueTask.CompletedTask),
            "Type24 attacker could not enter the world.");
        Check(hub.Enter(2, "TYPE24_TARGET_ONE", (_, _) => ValueTask.CompletedTask) &&
            hub.Enter(3, "TYPE24_TARGET_TWO", (_, _) => ValueTask.CompletedTask),
            "Type24 targets could not enter the world.");
        Check(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob) &&
            hub.SetCharacterState(2, 0, 0, 2, 0, 0, 2005, 2005, targetMob) &&
            hub.SetCharacterState(3, 0, 0, 3, 0, 0, 2006, 2006, targetMob),
            "Type24 participant state could not be initialized.");

        var request = new LegacyAttackActionBatchRequest(
            1,
            [
                new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Physical, 2,
                    PhysicalRolls: new LegacyPhysicalAttackBatchSlotRolls(RandomFactor: 99, AttackMissRoll: 99)),
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Physical, 3,
                    PhysicalRolls: new LegacyPhysicalAttackBatchSlotRolls(RandomFactor: 99, AttackMissRoll: 99)),
            ],
            SupportsSecondaryDamageSlot: false)
        {
            IsShortTwoTargetPacket = shortPacket,
        };
        Check(hub.TryApplyAttackActionBatch(request, out var batch) && batch is { CompletedNormally: true },
            "Type24 attack batch did not complete through the normal aggregate path.");
        return (hub, batch!);
    }

    private static void VerifyBlockedSlotRelay(LegacyAttackActionBatchOutcome batch)
    {
        var codec = LegacyFrameCodec.CreateDefault();
        var payload = new byte[AttackRequest.TwoTargetMinimumCompletePacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(48), 2);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(52), -2);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(56), 3);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(60), -2);
        var input = codec.Decode(codec.Encode(AttackRequest.TwoTargetMessageType, 4, 1000, payload, 16));
        var attackerMob = batch.AttackerMobSnapshot ?? throw new InvalidOperationException("Type24 attacker snapshot is missing.");
        Check(AttackConfirmation.TryCreate(input, attackerMob, out var confirmation) && confirmation is not null,
            "Type24 attack confirmation could not be created for the partial two-target packet.");
        var confirmed = confirmation ?? throw new InvalidOperationException("Type24 attack confirmation is missing.");

        foreach (var slot in batch.SlotOutcomes)
        {
            if (slot.PhysicalOutcome is { } outcome)
                Check(confirmed.TrySetDamageSlot(slot.SlotIndex, checked((ushort)outcome.TargetConnectionId), outcome.Damage),
                    "Accepted type24 fixture slot could not be written to the relay.");
            else if (slot.PhysicalResult == LegacyPhysicalAttackResult.ShortPacketSecondaryTargetBlocked)
                Check(confirmed.TrySetDamageSlot(slot.SlotIndex, 3, -2),
                    "Blocked type24 slot could not retain its legacy -2 marker.");
        }

        var response = codec.Decode(confirmed.ToFrame(codec, input.Header.ClientTick, 17));
        Check(response.Header.Size == AttackRequest.TwoTargetMinimumCompletePacketSize &&
            AttackRequest.TryReadDamageSlot(response, 0, out var firstTarget, out var firstDamage) && firstTarget == 2 && firstDamage > 0 &&
            AttackRequest.TryReadDamageSlot(response, 1, out var blockedTarget, out var blockedDamage) && blockedTarget == 3 && blockedDamage == -2,
            "Type24 aggregate relay did not preserve the successful first hit and original blocked target marker.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

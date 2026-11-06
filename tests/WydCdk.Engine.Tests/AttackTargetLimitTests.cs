using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackTargetLimitTests
{
    public static void Run()
    {
        Check(!LegacyAttackTargetLimitGate.IsExceeded(0, 1, -1, 100), "First target index was rejected.");
        Check(!LegacyAttackTargetLimitGate.IsExceeded(1, 1, -1, 100), "MaxTarget is inclusive.");
        Check(LegacyAttackTargetLimitGate.IsExceeded(2, 1, -1, 100), "First index above MaxTarget was accepted.");
        Check(!LegacyAttackTargetLimitGate.IsExceeded(2, 1, -2, 100), "Physical marker was treated as a skill target.");
        Check(!LegacyAttackTargetLimitGate.IsExceeded(2, 1, 0, 100), "Empty marker was treated as a skill target.");
        Check(!LegacyAttackTargetLimitGate.IsExceeded(2, 1, -1, ClientTickPolicy.SkipCheckTick), "Reserved internal tick was treated as a client violation.");

        var firstError = LegacyCrackErrorRules.Add(0, LegacyAttackTargetLimitGate.CrackErrorPoints, LegacyAttackTargetLimitGate.CrackErrorType);
        Check(firstError == new LegacyCrackErrorOutcome(10, 10, 28, true, false), "Max-target error did not add/log 10/type28.");
        var threshold = LegacyCrackErrorRules.Add(
            LegacyCrackErrorRules.LogoutThreshold - LegacyAttackTargetLimitGate.CrackErrorPoints,
            LegacyAttackTargetLimitGate.CrackErrorPoints,
            LegacyAttackTargetLimitGate.CrackErrorType);
        Check(threshold is { TotalPoints: LegacyCrackErrorRules.LogoutThreshold, RequiresCharacterLogout: true },
            "Max-target error bypassed the shared logout threshold.");

        var codec = LegacyFrameCodec.CreateDefault();
        var payload = new byte[AttackRequest.AreaPacketSize - PacketHeader.SizeInBytes];
        var slotOffset = AttackRequest.DamageArrayOffset + (2 * AttackRequest.DamageSlotSize);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(slotOffset), 17);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(slotOffset + 4), -1);
        var input = codec.Decode(codec.Encode(AttackRequest.AreaMessageType, 1, 123, payload, 7));
        Check(AttackConfirmation.TryCreate(input, new byte[LegacyAccountSnapshot.CharacterStride], out var confirmation) && confirmation is not null,
            "Authoritative confirmation could not be created.");
        Check(confirmation!.TrySetDamageSlot(2, 17, -1), "Excess skill marker could not be restored into the relay.");
        var relay = codec.Decode(confirmation.ToFrame(codec, input.Header.ClientTick, 8));
        Check(AttackRequest.TryReadDamageSlot(relay, 2, out var targetId, out var damage) && targetId == 17 && damage == -1,
            "Relay did not preserve the excess target ID and -1 skill marker.");

        TargetEligibilityPrecedesTheLimitGate();
    }

    private static void TargetEligibilityPrecedesTheLimitGate()
    {
        var attackerMob = new byte[LegacyAccountSnapshot.CharacterStride];
        var targetMob = new byte[LegacyAccountSnapshot.CharacterStride];
        var merchantMob = new byte[LegacyAccountSnapshot.CharacterStride];
        var deadMob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(50, 0, 0, 0, 0, 0, 0, 5000, 5000, 5000, 5000, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        new LegacyScore(50, 40, 0, 0, 0, 0, 0, 5000, 5000, 1000, 500, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        new LegacyScore(50, 40, 0, 1, 0, 0, 0, 5000, 5000, 1000, 500, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(merchantMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        new LegacyScore(50, 40, 0, 0, 0, 0, 0, 5000, 5000, 0, 500, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(deadMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var skill = new LegacySkillDefinition(27, 0, 0, 0, 0, 0, 6, 100, 0, 0, 0, 0, 0, [], [], 0, 0, 0, 1, 0, 0, 0, 0, "Cura");
        var world = new WorldHub();
        Check(world.Enter(1, "LIMIT_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
            world.Enter(2, "LIMIT_TARGET", (_, _) => ValueTask.CompletedTask) &&
            world.Enter(3, "LIMIT_FAR", (_, _) => ValueTask.CompletedTask) &&
            world.Enter(4, "LIMIT_MERCHANT", (_, _) => ValueTask.CompletedTask) &&
            world.Enter(5, "LIMIT_DEAD", (_, _) => ValueTask.CompletedTask), "Target-limit participants were not registered.");
        Check(world.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob) &&
            world.SetCharacterState(2, 0, 0, 2, 0, 0, 2005, 2005, targetMob) &&
            world.SetCharacterState(3, 0, 0, 3, 0, 0, 2024, 2000, targetMob) &&
            world.SetCharacterState(4, 0, 0, 4, 0, 0, 2005, 2005, merchantMob) &&
            world.SetCharacterState(5, 0, 0, 5, 0, 0, 2005, 2005, deadMob), "Target-limit participant states were not installed.");

        Check(world.TryApplySkillAttack(1, 2, skill, null, 0, 90, out var excessOutcome, targetIndex: 2, clientTick: 100) == LegacySkillAttackResult.TargetLimitExceeded && excessOutcome is null,
            "A live, in-range excess skill target did not reach type28 without combat effects.");
        Check(world.TryGetCombatState(2, out var unchangedTarget) && unchangedTarget!.CurrentScore.Hp == 1000,
            "An excess skill target was applied before being skipped.");
        Check(world.TryApplySkillAttack(1, 3, skill, null, 0, 90, out _, targetIndex: 2, clientTick: 100) == LegacySkillAttackResult.OutOfRange,
            "An out-of-range excess target incorrectly reached type28.");
        Check(world.TryApplySkillAttack(1, 99, skill, null, 0, 90, out _, targetIndex: 2, clientTick: 100) == LegacySkillAttackResult.ParticipantNotFound,
            "A missing excess target incorrectly reached type28.");
        Check(world.TryApplySkillAttack(1, 4, skill, null, 0, 90, out _, targetIndex: 2, clientTick: 100) == LegacySkillAttackResult.TargetMerchantBlocked,
            "A protected merchant excess target incorrectly reached type28.");
        Check(world.TryApplySkillAttack(1, 5, skill, null, 0, 90, out _, targetIndex: 2, clientTick: 100) == LegacySkillAttackResult.TargetNotAlive,
            "A dead excess target incorrectly reached type28.");
        var unportedEffectSkill = skill with { Id = 30, InstanceType = 7, Aggressive = 1 };
        Check(world.TryValidateSkillTargetLimit(1, 2, unportedEffectSkill, targetIndex: 2, clientTick: 100) == LegacySkillAttackResult.TargetLimitExceeded,
            "An excess target for a skill with an unported effect did not reach the independent type28 gate.");
        Check(world.TryGetCombatState(2, out unchangedTarget) && unchangedTarget!.CurrentScore.Hp == 1000,
            "The independent max-target gate applied an unported skill effect.");
        Check(world.SetCharacterState(1, 0, 7, 0, 0, 0, 2000, 2000, attackerMob) &&
            world.SetCharacterState(2, 0, 7, 0, 0, 0, 2005, 2005, targetMob), "Same-guild over-limit fixture was not installed.");
        Check(world.TryValidateSkillTargetLimit(1, 2, unportedEffectSkill, targetIndex: 2, clientTick: 100) == LegacySkillAttackResult.TargetLimitExceeded,
            "The later same-guild skill gate incorrectly suppressed the earlier type28 violation.");
        Check(world.SetCharacterState(1, 0, 0, 0, 0, 0, 2000, 2000, attackerMob) &&
            world.SetCharacterState(2, 0, 0, 0, 0, 0, 2005, 2005, targetMob), "Target-limit boundary fixture could not be restored.");
        Check(world.TryApplySkillAttack(1, 2, skill, null, 0, 90, out var boundaryOutcome, targetIndex: 1, clientTick: 100) == LegacySkillAttackResult.Accepted && boundaryOutcome is not null,
            "The inclusive MaxTarget index did not remain eligible for skill processing.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

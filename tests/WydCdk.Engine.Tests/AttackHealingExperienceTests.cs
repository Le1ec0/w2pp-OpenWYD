using System.Buffers.Binary;
using System.Reflection;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackHealingExperienceTests
{
    public static void DoubleModeUsesScalarMultiplier()
    {
        Check(LegacyAttackHealExperienceMath.ApplyDoubleModeAndCap(40, 3) == 120,
            "A 3x DoubleMode did not multiply the healing contribution before the final cap.");
        Check(LegacyAttackHealExperienceMath.ApplyDoubleModeAndCap(120, 3) == 200,
            "A scalar DoubleMode did not preserve the final FakeExp cap of 200.");
        Check(LegacyAttackHealExperienceMath.ApplyDoubleModeAndCap(40, 2) == 80,
            "The existing 2x DoubleMode did not retain its expected multiplier.");
        Check(LegacyAttackHealExperienceMath.ApplyDoubleModeAndCap(40, 1) == 40,
            "The normal 1x DoubleMode changed the healing contribution.");
        Check(LegacyAttackHealExperienceMath.ApplyDoubleModeAndCap(40, 0) == 0 &&
            LegacyAttackHealExperienceMath.ApplyDoubleModeAndCap(40, -1) == 0,
            "A non-positive DoubleMode produced positive FakeExp.");

        var eligibility = new LegacyExperienceEligibility(LegacyAccountSnapshot.ClassMasterMortal);
        var tripledPartyExperience = LegacyPartyExperienceMath.GetGenericMemberExperience(
            eligibility, 1_000, 50, 1_000, 50, doubleModeMultiplier: 3);
        var disabledPartyExperience = LegacyPartyExperienceMath.GetGenericMemberExperience(
            eligibility, 1_000, 50, 1_000, 50, doubleModeMultiplier: 0);
        Check(tripledPartyExperience == 2_550,
            $"A 3x DoubleMode did not multiply generic party XP before the ordinary reduction: {tripledPartyExperience}.");
        Check(disabledPartyExperience == 0,
            $"A zero DoubleMode multiplier did not suppress generic party XP: {disabledPartyExperience}.");
    }

    public static void ContributionCapsAndExcludesSelfAndVillage()
    {
        Check(LegacyAttackHealExperienceMath.GetContribution(1, 2, 3000, 3000, 1000, 3000) == 120,
            "A healing experience contribution above 120 was not capped.");
        Check(LegacyAttackHealExperienceMath.ApplyDoubleModeAndCap(120, doubleModeMultiplier: 2) == 200,
            "DoubleMode did not apply before the final 200 experience cap.");
        Check(LegacyAttackHealExperienceMath.ApplyDoubleModeAndCap(120, doubleModeMultiplier: 1) == 120,
            "A normal mode action changed the uncapped 120-point contribution.");
        Check(LegacyAttackHealExperienceMath.GetContribution(1, 2, 2100, 2100, 1000, 2000) is null,
            "Healing inside a village incorrectly generated experience.");
        Check(LegacyAttackHealExperienceMath.GetContribution(1, 1, 3000, 3000, 1000, 2000) is null,
            "Healing the caster incorrectly generated experience.");
    }

    public static void AbortSuppressesHealingExperienceOverride()
    {
        var world = new WorldHub();
        Check(world.Enter(1, "HEAL_EXP_ABORT_CASTER", (_, _) => ValueTask.CompletedTask) &&
            world.Enter(2, "HEAL_EXP_ABORT_TARGET", (_, _) => ValueTask.CompletedTask) &&
            world.Enter(3, "HEAL_EXP_ABORT_RANGE", (_, _) => ValueTask.CompletedTask),
            "Healing-abort participants were not registered.");
        Check(world.SetCharacterState(1, 0, 0, 1, 0, 0, 3000, 3000, CreateMob(5000, 300)) &&
            world.SetCharacterState(2, 0, 0, 1, 0, 0, 3001, 3000, CreateMob(1000, 0)) &&
            world.SetCharacterState(3, 0, 0, 1, 0, 0, 3024, 3000, CreateMob(5000, 0)),
            "Healing-abort character states were not installed.");

        var request = new LegacyAttackActionBatchRequest(
            1,
            [
                new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Skill, 2),
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Physical, 3,
                    new LegacyPhysicalAttackBatchSlotRolls(RandomFactor: 100, AttackMissRoll: 99)),
            ],
            Skill: AttackPartySkillGateTests.CreateHealingSkill(partyCheck: 0),
            ClientTick: ClientTickPolicy.SkipCheckTick);
        Check(world.TryApplyAttackActionBatch(request, out var action) && action is { AbortSlotIndex: 1, CompletedNormally: false },
            "The physical out-of-range slot did not abort the heal action.");
        Check(action!.HealingExperience is null,
            "An aborted attack action retained a healing experience override that must not be relayed.");
    }

    public static void AttackConfirmationCanSetFakeExp()
    {
        var codec = LegacyFrameCodec.CreateDefault();
        var payload = new byte[AttackRequest.AreaPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0), 77_777);
        var request = codec.Decode(codec.Encode(AttackRequest.AreaMessageType, 1, 123, payload, 7));
        var mob = CreateMob(5000, 0);
        Check(AttackConfirmation.TryCreate(request, mob, out var confirmation) && confirmation is not null,
            "The authoritative attack confirmation could not be created.");

        var setter = typeof(AttackConfirmation).GetMethod("TrySetFakeExp", BindingFlags.Instance | BindingFlags.Public);
        Check(setter is not null, "AttackConfirmation has no server-owned FakeExp setter.");
        Check((bool)setter!.Invoke(confirmation, [79])!, "The FakeExp setter rejected a valid attack response.");
        var response = codec.Decode(confirmation!.ToFrame(codec, request.Header.ClientTick, 8));
        Check(BinaryPrimitives.ReadInt32LittleEndian(response.Payload.Span) == 79,
            "The final attack payload did not contain the server-owned healing experience at the 7.69 FakeExp offset.");
    }

    public static void BatchUsesMaximumActualHealAndScalarDoubleMode()
    {
        var world = new WorldHub();
        world.SetCombatWorldState(new LegacyCombatWorldState(DoubleModeMultiplier: 3));
        Check(world.Enter(1, "HEAL_EXP_CASTER", (_, _) => ValueTask.CompletedTask) &&
            world.Enter(2, "HEAL_EXP_TARGET_A", (_, _) => ValueTask.CompletedTask) &&
            world.Enter(3, "HEAL_EXP_TARGET_B", (_, _) => ValueTask.CompletedTask),
            "Healing experience participants were not registered.");

        var casterMob = CreateMob(5000, 300);
        BinaryPrimitives.WriteInt64LittleEndian(casterMob.AsSpan(32), 1_234_567);
        var firstTargetMob = CreateMob(1000, 0);
        var secondTargetMob = CreateMob(4900, 0);
        Check(world.SetCharacterState(1, 0, 0, 1, 0, 0, 3000, 3000, casterMob) &&
            world.SetCharacterState(2, 0, 0, 1, 0, 0, 3001, 3000, firstTargetMob) &&
            world.SetCharacterState(3, 0, 0, 1, 0, 0, 3002, 3000, secondTargetMob),
            "Healing experience character states were not installed.");

        var skill = AttackPartySkillGateTests.CreateHealingSkill(partyCheck: 0);
        var request = new LegacyAttackActionBatchRequest(
            1,
            [
                new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Skill, 2),
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Skill, 3),
            ],
            Skill: skill,
            ClientTick: ClientTickPolicy.SkipCheckTick);

        Check(world.TryApplyAttackActionBatch(request, out var action) && action is { CompletedNormally: true },
            "Healing attack action did not complete.");
        var healingExperience = typeof(LegacyAttackActionBatchOutcome).GetProperty("HealingExperience", BindingFlags.Instance | BindingFlags.Public);
        Check(healingExperience is not null,
            "The batch outcome does not expose the action-level healing experience override.");
        Check((int?)healingExperience!.GetValue(action) == 200,
            $"The action should apply the 3x multiplier to the maximum actual-heal contribution (79), then cap at 200; got {healingExperience.GetValue(action)}.");
        Check(action!.AttackerMobSnapshot is not null &&
            BinaryPrimitives.ReadInt64LittleEndian(action.AttackerMobSnapshot.AsSpan(32)) == 1_234_567,
            "Healing experience changed the character's persisted MOB EXP.");

        Check(LegacyScore.Read(action.SlotOutcomes[0].SkillOutcome!.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)).Hp == 1638,
            "The first healing target did not receive the 638 HP cap.");
        Check(LegacyScore.Read(action.SlotOutcomes[1].SkillOutcome!.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)).Hp == 5000,
            "The second healing target did not stop at MaxHp.");
    }

    private static byte[] CreateMob(int hp, int special2)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(50, 0, 0, 0, 0, 0, 0, 5000, 5000, hp, 5000, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var score = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with { Special2 = checked((short)special2) };
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

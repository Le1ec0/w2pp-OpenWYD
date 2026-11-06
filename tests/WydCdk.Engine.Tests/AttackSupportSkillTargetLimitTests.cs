using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackSupportSkillTargetLimitTests
{
    private static readonly LegacyItemDataTable itemData = CreateItemDataTable();

    public static void Run()
    {
        TargetCountUsesTheSpecialFormulaAndLegacyBounds();
        SupportSkillsStopAtTheirSpecialBasedTargetCount();
    }

    private static void TargetCountUsesTheSpecialFormulaAndLegacyBounds()
    {
        foreach (var skillId in new[] { 41, 44 })
        {
            Check(LegacySupportSkillTargetLimit.GetTargetCount(skillId, 0) == 2,
                $"Skill {skillId} did not preserve the legacy minimum of two targets.");
            Check(LegacySupportSkillTargetLimit.GetTargetCount(skillId, 25) == 3,
                $"Skill {skillId} did not add one target for each 25 Special points.");
            Check(LegacySupportSkillTargetLimit.GetTargetCount(skillId, 300) == 13,
                $"Skill {skillId} did not clamp at MAX_TARGET=13.");
            Check(LegacySupportSkillTargetLimit.GetTargetCount(skillId, -100) == 2,
                $"Skill {skillId} did not clamp low Special to two targets.");
        }

        Check(LegacySupportSkillTargetLimit.GetTargetCount(40, 100) is null,
            "The support-skill target rule affected skill 40.");
    }

    private static void SupportSkillsStopAtTheirSpecialBasedTargetCount()
    {
        foreach (var (skillId, affectType, affectValue) in new[]
        {
            (41, 2, 1),
            (44, 9, 90),
        })
        {
            foreach (var (special4, expectedTargetCount) in new[] { (0, 2), (25, 3) })
            {
                var world = new WorldHub();
                var casterMob = CreateMob(special4);
                var targetMob = LegacyCurrentScoreLoginProjector.Project(
                    new LegacyCharacterLoginData(
                        CreateMob(special4: 0),
                        new byte[LegacyAccountSnapshot.ShortSkillStride],
                        new byte[LegacyAccountSnapshot.AffectStride],
                        new byte[LegacyAccountSnapshot.MobExtraStride],
                        0,
                        2001,
                        2000),
                    itemData).Mob;
                var originalDamage = LegacyMobCombatState.Read(targetMob).CurrentScore.Damage;
                var originalMagic = LegacyMobCombatState.Read(targetMob).Magic;
                Check(world.Enter(1, $"SUPPORT_{skillId}_{special4}_CASTER", (_, _) => ValueTask.CompletedTask) &&
                    world.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, casterMob),
                    $"Skill {skillId} caster fixture was not installed.");

                for (var targetIndex = 0; targetIndex < 3; targetIndex++)
                {
                    var connectionId = targetIndex + 2;
                    Check(world.Enter(connectionId, $"SUPPORT_{skillId}_{special4}_TARGET_{targetIndex}", (_, _) => ValueTask.CompletedTask) &&
                        world.SetCharacterState(connectionId, 0, 0, connectionId, 0, 0,
                            (short)(2001 + targetIndex), 2000, targetMob),
                        $"Skill {skillId} target {targetIndex} fixture was not installed.");
                }

                var skill = new LegacySkillDefinition(
                    skillId, 0, 0, 0, 0, 4, 0, 0, 0, 0, affectType, affectValue, 99,
                    [], [], 0, 0, 0, 13, 0, 0, 0, 0, $"Support_{skillId}");
                Check(LegacySkillCombatMath.GetSkillSpecial(skill, LegacyMobCombatState.Read(casterMob)) == special4,
                    $"Skill {skillId} fixture did not expose its [3] Special through the 7.69 skill-stage mapping.");
                var request = new LegacyAttackActionBatchRequest(
                    1,
                    [
                        new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Skill, 2, SkillTargetIndex: 0),
                        new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Skill, 3, SkillTargetIndex: 1),
                        new LegacyAttackActionBatchSlotRequest(2, LegacyAttackActionSlotKind.Skill, 4, SkillTargetIndex: 2),
                    ],
                    Skill: skill,
                    ItemData: itemData);

                Check(world.TryApplyAttackActionBatch(request, out var outcome) && outcome is not null,
                    $"Skill {skillId} support-skill batch was rejected.");
                Check(outcome!.SlotOutcomes.Count == expectedTargetCount &&
                    outcome.SlotOutcomes.Select((slot, index) => slot.SlotIndex == index &&
                        slot.SkillOutcome?.TargetAffectSnapshot is not null &&
                        slot.SkillOutcome.TargetMobSnapshot.Length >= LegacyAccountSnapshot.CharacterStride).All(static included => included),
                    $"Skill {skillId} at Special={special4} did not expose score/affect updates for exactly {expectedTargetCount} targets.");

                var firstUpdatedTarget = LegacyMobCombatState.Read(outcome.SlotOutcomes[0].SkillOutcome!.TargetMobSnapshot);
                if (skillId == 41)
                {
                    Check((firstUpdatedTarget.Rsv & LegacyCurrentScoreMath.HasteRsvFlag) != 0,
                        "Skill 41 applied Haste but did not rebuild the target's Haste RSV flag before its score update.");
                }
                else
                {
                    Check(firstUpdatedTarget.CurrentScore.Damage > originalDamage && firstUpdatedTarget.Magic == originalMagic,
                        "Skill 44 did not rebuild Damage while preserving the 7.69 Magic value before its score update.");
                }
            }
        }
    }

    private static byte[] CreateMob(int special4)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(50, 0, 0, 0, 0, 0, 0, 1000, 1000, 1000, 1000,
            0, 0, 0, 0, 0, 0, 0, 0) with { Special4 = checked((short)special4) };
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = 1;
        return mob;
    }

    private static LegacyItemDataTable CreateItemDataTable()
    {
        var encoded = new byte[LegacyItemDataTable.BodySizeInBytes];
        Array.Fill(encoded, (byte)0x5A);
        return LegacyItemDataTable.Load(encoded);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

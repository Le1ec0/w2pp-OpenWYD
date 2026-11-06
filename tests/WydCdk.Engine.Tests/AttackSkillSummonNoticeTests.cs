using WydCdk.Protocol;
using WydCdk.Server;
using WydCdk.World;

internal static class AttackSkillSummonNoticeTests
{
    public static void Run()
    {
        InstanceType9TerminalZoneFailureAbortsRemainingSlots();
        InstanceType9DeadTargetBreaksRemainingSlotsAndCompletesBatch();
        InstanceType9HighLevelTargetContinuesToNextSlot();
        InstanceType9ResultsMapToPrivateLegacyNoticeText();
    }

    private static void InstanceType9TerminalZoneFailureAbortsRemainingSlots()
    {
        var world = CreateWorld();
        var skill = CreateTeleportSkill();
        var request = new LegacyAttackActionBatchRequest(
            1,
            [
                new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Skill, 2, SkillTargetIndex: 0),
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Skill, 3, SkillTargetIndex: 1),
                new LegacyAttackActionBatchSlotRequest(2, LegacyAttackActionSlotKind.Skill, 4, SkillTargetIndex: 2),
            ],
            skill);

        Check(world.TryApplyAttackActionBatch(request, out var outcome) && outcome is not null,
            "InstanceType 9 batch was not accepted by the action processor.");
        Check(!outcome!.CompletedNormally && outcome.AbortSlotIndex == 1 && outcome.SlotOutcomes.Count == 2,
            $"7.69's terminal summon-zone failure did not stop later slots: completed={outcome.CompletedNormally}, abort={outcome.AbortSlotIndex}, slots={outcome.SlotOutcomes.Count}.");
        Check(outcome.SlotOutcomes[0].SkillResult == LegacySkillAttackResult.Accepted &&
              outcome.SlotOutcomes[1].SkillResult == LegacySkillAttackResult.SummonInvalidPosition,
            "The batch did not retain the earlier success followed by the invalid-position result.");
    }

    private static void InstanceType9DeadTargetBreaksRemainingSlotsAndCompletesBatch()
    {
        var world = CreateWorld(target2Hp: 0);
        var request = new LegacyAttackActionBatchRequest(
            1,
            [
                new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Skill, 2, SkillTargetIndex: 0),
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Skill, 4, SkillTargetIndex: 1),
            ],
            CreateTeleportSkill());

        Check(world.TryApplyAttackActionBatch(request, out var outcome) && outcome is not null,
            "InstanceType 9 dead-target batch was not accepted by the action processor.");
        Check(outcome!.CompletedNormally && outcome.AbortSlotIndex is null && outcome.SlotOutcomes.Count == 1 &&
              outcome.SlotOutcomes[0].SkillResult == LegacySkillAttackResult.TargetNotAlive,
            $"7.69's dead-target break did not stop later slots while completing the handler: completed={outcome.CompletedNormally}, abort={outcome.AbortSlotIndex}, slots={outcome.SlotOutcomes.Count}.");
    }

    private static void InstanceType9HighLevelTargetContinuesToNextSlot()
    {
        var world = CreateWorld(target3Level: 200);
        var request = new LegacyAttackActionBatchRequest(
            1,
            [
                new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Skill, 3, SkillTargetIndex: 0),
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Skill, 4, SkillTargetIndex: 1),
            ],
            CreateTeleportSkill());

        Check(world.TryApplyAttackActionBatch(request, out var outcome) && outcome is not null,
            "InstanceType 9 high-level batch was not accepted by the action processor.");
        Check(outcome!.CompletedNormally && outcome.AbortSlotIndex is null && outcome.SlotOutcomes.Count == 2 &&
              outcome.SlotOutcomes[0].SkillResult == LegacySkillAttackResult.TargetTooHighToSummon &&
              outcome.SlotOutcomes[1].SkillResult == LegacySkillAttackResult.Accepted,
            "7.69's high-level continue did not preserve processing of the following Type 9 slot.");
    }

    private static void InstanceType9ResultsMapToPrivateLegacyNoticeText()
    {
        var cases = new (LegacySkillAttackResult Result, string Expected)[]
        {
            (LegacySkillAttackResult.TargetNotAlive, "Personagem derrotado não pode ser invocado ou transferido."),
            (LegacySkillAttackResult.SummonNotAllowedHere, "Você não pode invocar ou transferir nesta zona."),
            (LegacySkillAttackResult.TargetTooHighToSummon, "O nível do oponente é muito alto para isto."),
            (LegacySkillAttackResult.SummonInvalidPosition, "Não está disponível nesta área."),
            (LegacySkillAttackResult.SummonZoneBlocked, "Não está disponível nesta área."),
        };

        foreach (var (result, expected) in cases)
            Check(LegacySkillSummonNoticeMapper.GetMessage(instanceType: 9, result) == expected,
                $"InstanceType 9 result {result} did not resolve to the 7.69 private notice text.");

        Check(LegacySkillSummonNoticeMapper.GetMessage(instanceType: 6, LegacySkillAttackResult.TargetNotAlive) is null &&
              LegacySkillSummonNoticeMapper.GetMessage(instanceType: 9, LegacySkillAttackResult.OutOfRange) is null,
            "A summon notice was produced for a non-summon skill or an unrelated result.");
    }

    private static WorldHub CreateWorld(int target2Hp = 1_000, int target3Level = 40)
    {
        var world = new WorldHub();
        var attackerMob = CreateMob(level: 50, hp: 1_000);
        var validTargetMob = CreateMob(level: 40, hp: target2Hp);
        var invalidTargetMob = CreateMob(level: 40, hp: 1_000);
        var highLevelMob = CreateMob(level: target3Level, hp: 1_000);
        var laterTargetMob = CreateMob(level: 40, hp: 1_000);

        for (var id = 1; id <= 4; id++)
            Check(world.Enter(id, $"SUMMON_NOTICE_{id}", (_, _) => ValueTask.CompletedTask),
                $"Summon notice fixture participant {id} was not registered.");

        Check(world.SetCharacterState(1, 0, 0, 1, 0, 0, 250, 250, attackerMob) &&
              world.SetCharacterState(2, 0, 0, 2, 0, 0, 256, 250, validTargetMob) &&
              world.SetCharacterState(3, 0, 0, 3, 0, 0, 240, 250, target3Level == 40 ? invalidTargetMob : highLevelMob) &&
              world.SetCharacterState(4, 0, 0, 4, 0, 0, 260, 250, laterTargetMob),
            "Summon notice fixture characters were not installed.");

        return world;
    }

    private static byte[] CreateMob(int level, int hp)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(level, 0, 100, 0, 0, 0, 0, 1_000, 1_000, hp, 1_000,
            0, 0, 1_000, 0, 0, 100, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static LegacySkillDefinition CreateTeleportSkill() =>
        new(42, 54, 0, 0, 0, 0, 9, 0, 0, 0, 0, 0, 0, [], [], 0, 0, 0, 13, 0, 0, 0, 0,
            "SUMMON_NOTICE_TELEPORT");

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

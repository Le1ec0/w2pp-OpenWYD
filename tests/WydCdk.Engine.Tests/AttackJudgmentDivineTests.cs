using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackJudgmentDivineTests
{
    private const int StartingHp = 800;
    private const string SkillRow = "30,102,4,150,10,1,4,200,0,0,0,0,0,24.0.0.8.0.0.0.0,19.0.0.8.0.0.0.0,4,0,1,5,0,3,0,1,Julgamento_Divino";

    public static void Run()
    {
        BatchUsesInitialHpDamageBonusAndChargesCurrentHpForEachTarget();
        MissedTargetStillChargesCasterHp();
    }

    private static void BatchUsesInitialHpDamageBonusAndChargesCurrentHpForEachTarget()
    {
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
        var world = CreateWorld(itemData, out var npcId);
        var skill = LegacySkillDataTable.Load(new StringReader(SkillRow))[30]
            ?? throw new InvalidOperationException("The Judgment Divine skill fixture did not load.");
        var batch = new LegacyAttackActionBatchRequest(
            1,
            [
                new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Skill, 2,
                    SkillRolls: new LegacySkillAttackBatchSlotRolls(RandomFactor: 100, AttackMissRoll: 99), SkillTargetIndex: 0),
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Skill, npcId,
                    SkillRolls: new LegacySkillAttackBatchSlotRolls(RandomFactor: 100), SkillTargetIndex: 1),
                new LegacyAttackActionBatchSlotRequest(2, LegacyAttackActionSlotKind.Skill, 3,
                    SkillRolls: new LegacySkillAttackBatchSlotRolls(RandomFactor: 100, AttackMissRoll: 99), SkillTargetIndex: 2),
            ],
            Skill: skill,
            ItemData: itemData,
            ClientTick: ClientTickPolicy.SkipCheckTick);

        Check(world.TryApplyAttackActionBatch(batch, out var outcome) && outcome is { CompletedNormally: true },
            "Judgment Divine attack batch did not complete.");
        Check(outcome!.SlotOutcomes.Count == 3 && outcome.SlotOutcomes.All(slot => slot.SkillResult == LegacySkillAttackResult.Accepted),
            "Judgment Divine did not resolve its player, NPC, and player targets in order.");

        foreach (var slot in outcome.SlotOutcomes)
        {
            var skillOutcome = slot.SkillOutcome;
            Check(skillOutcome is not null, $"Judgment Divine target slot {slot.SlotIndex} has no outcome.");
            var rolledDamage = LegacySkillCombatMath.GetSkillDamage(
                skillOutcome!.BaseDamage,
                armor: 0,
                combat: 0,
                randomFactor: 100);
            var expectedOrdinaryDamage = LegacySkillCombatMath.ApplySkillResistance(
                skill.Id,
                skill.InstanceType,
                rolledDamage,
                targetIsPlayer: true,
                targetLevel: 0,
                targetClassMaster: 0,
                targetLearnedSkill: 0,
                new byte[4],
                out _);
            Check(skillOutcome.Damage == expectedOrdinaryDamage + StartingHp,
                $"Judgment Divine slot {slot.SlotIndex} did not add the attack-start HP {StartingHp} to its resolved damage: {skillOutcome.Damage}.");
            Check(skillOutcome.AttackerResourceChanged,
                $"Judgment Divine slot {slot.SlotIndex} did not expose the caster HP mutation for the existing resource relay.");
        }

        Check(world.TryGetResourceState(1, out var attackerState, out var requestedHp, out _) &&
              attackerState is not null && attackerState.CurrentScore.Hp == 4 && requestedHp == 4,
            $"Judgment Divine did not apply the per-target HP sequence 800->134->23->4 to current and requested HP: current={attackerState?.CurrentScore.Hp}, requested={requestedHp}.");
        Check(outcome.AttackerMobSnapshot is not null &&
              BinaryPrimitives.ReadInt32LittleEndian(outcome.AttackerMobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24)) == 4,
            "The aggregate attack snapshot did not retain the caster's final Judgment Divine HP.");
    }

    private static void MissedTargetStillChargesCasterHp()
    {
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
        var world = CreateWorld(itemData, out _);
        var skill = LegacySkillDataTable.Load(new StringReader(SkillRow))[30]
            ?? throw new InvalidOperationException("The Judgment Divine skill fixture did not load.");
        var request = new LegacyAttackActionBatchRequest(
            1,
            [new LegacyAttackActionBatchSlotRequest(
                0,
                LegacyAttackActionSlotKind.Skill,
                2,
                SkillRolls: new LegacySkillAttackBatchSlotRolls(RandomFactor: 100, AttackMissRoll: 0),
                SkillTargetIndex: 0)],
            Skill: skill,
            ItemData: itemData,
            ClientTick: ClientTickPolicy.SkipCheckTick);

        Check(world.TryApplyAttackActionBatch(request, out var outcome) && outcome is { CompletedNormally: true },
            "The missed Judgment Divine action batch did not complete.");
        var skillOutcome = outcome!.SlotOutcomes.Single().SkillOutcome;
        Check(skillOutcome is { Damage: -3, TargetHpChanged: false, AttackerResourceChanged: true },
            $"The deterministic miss did not retain a miss outcome while recording the caster resource mutation: damage={skillOutcome?.Damage}, targetChanged={skillOutcome?.TargetHpChanged}, casterChanged={skillOutcome?.AttackerResourceChanged}.");
        Check(world.TryGetResourceState(1, out var attackerState, out var requestedHp, out _) &&
              attackerState is not null && attackerState.CurrentScore.Hp == 134 && requestedHp == 134,
            $"Judgment Divine did not charge the caster before miss resolution: current={attackerState?.CurrentScore.Hp}, requested={requestedHp}.");
    }

    private static WorldHub CreateWorld(LegacyItemDataTable itemData, out int npcId)
    {
        var world = new WorldHub(itemData: itemData);
        for (var id = 1; id <= 3; id++)
            Check(world.Enter(id, $"JUDGMENT_DIVINE_{id}", (_, _) => ValueTask.CompletedTask),
                $"Judgment Divine participant {id} was not registered.");

        Check(world.SetCharacterState(1, 0, 0, 0, 0, 0, 2_000, 2_000, CreateMob(characterClass: 2, hp: StartingHp)) &&
              world.SetCharacterState(2, 0, 0, 0, 0, 0, 2_002, 2_000, CreateMob(characterClass: 0, hp: 10_000)) &&
              world.SetCharacterState(3, 0, 0, 0, 0, 0, 2_004, 2_000, CreateMob(characterClass: 0, hp: 10_000)),
            "Judgment Divine player fixtures were not installed.");

        npcId = world.EnterNpc(CreateMob(characterClass: 0, hp: 10_000), 2_003, 2_000,
            new byte[LegacyAccountSnapshot.AffectStride], requestedConnectionId: 20_010);
        Check(npcId == 20_010, $"Judgment Divine NPC fixture was not registered: {npcId}.");
        return world;
    }

    private static byte[] CreateMob(int characterClass, int hp)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        mob[LegacyAccountSnapshot.MobClassOffset] = checked((byte)characterClass);
        var score = new LegacyScore(50, 0, 0, 0, 0, 0, 0,
            10_000, 1_000, hp, 1_000, 0, 0, 0, 0, 0, 100, 0, 0);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        return mob;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

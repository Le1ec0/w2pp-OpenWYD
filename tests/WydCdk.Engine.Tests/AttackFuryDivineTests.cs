using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackFuryDivineTests
{
    private const string SkillRow = "6,90,1,75,3,6,0,0,0,0,0,0,0,10.0.0.24.0.0.0.0,10.0.0.24.0.0.0.0,0,0,1,1,0,0,0,0,FURIA_DIVINA";
    private static readonly LegacySkillDefinition Skill = LegacySkillDataTable.Load(new StringReader(SkillRow))[6]
        ?? throw new InvalidOperationException("Furia Divina SkillData fixture did not load.");
    private static readonly LegacyItemDataTable ItemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    public static void Run()
    {
        KnockbackChanceKeepsTheLegacyMaxUserBoundaryAndUpperCap();
        SuccessfulKnockbackUsesTargetActionAndOneDamage();
        ChanceFailureKeepsTheAttackAndContinuesTheTargetBatch();
        ProtectedTargetStopsTheRemainingTargetBatch();
        SuccessfulNpcKnockbackSetsNpcCombatTarget();
    }

    private static void KnockbackChanceKeepsTheLegacyMaxUserBoundaryAndUpperCap()
    {
        var playerChance = LegacyFuryDivineMath.GetKnockbackChancePercent(
            targetConnectionId: 999, attackerSpecial1: 0, attackerLevel: 100,
            attackerClassMaster: LegacyAccountSnapshot.ClassMasterMortal,
            targetLevel: 500, targetClassMaster: LegacyAccountSnapshot.ClassMasterMortal);
        var exactMaxUserChance = LegacyFuryDivineMath.GetKnockbackChancePercent(
            targetConnectionId: LegacyBattleRoyaleGridRefreshRelay.MaxUserConnectionExclusive, attackerSpecial1: 0, attackerLevel: 100,
            attackerClassMaster: LegacyAccountSnapshot.ClassMasterMortal,
            targetLevel: 500, targetClassMaster: LegacyAccountSnapshot.ClassMasterMortal);
        var npcChance = LegacyFuryDivineMath.GetKnockbackChancePercent(
            targetConnectionId: LegacyBattleRoyaleGridRefreshRelay.MaxUserConnectionExclusive + 1, attackerSpecial1: 0, attackerLevel: 100,
            attackerClassMaster: LegacyAccountSnapshot.ClassMasterMortal,
            targetLevel: 500, targetClassMaster: LegacyAccountSnapshot.ClassMasterMortal);
        var cappedChance = LegacyFuryDivineMath.GetKnockbackChancePercent(
            targetConnectionId: 1, attackerSpecial1: 300, attackerLevel: 400,
            attackerClassMaster: LegacyExperienceMath.ClassMasterCelestial,
            targetLevel: 1, targetClassMaster: LegacyAccountSnapshot.ClassMasterMortal);

        Check(playerChance == 19 && exactMaxUserChance == 19 && npcChance == 39,
            $"Furia Divina did not preserve the strict target-index > MAX_USER formula: player={playerChance}, MAX_USER={exactMaxUserChance}, NPC={npcChance}.");
        Check(cappedChance == LegacyFuryDivineMath.MaximumChancePercent &&
            LegacyFuryDivineMath.IsKnockbackSuccessful(50, 49) && !LegacyFuryDivineMath.IsKnockbackSuccessful(50, 50) &&
            !LegacyFuryDivineMath.IsKnockbackSuccessful(0, 0),
            $"Furia Divina did not cap chance at 50% and compare the roll strictly: capped={cappedChance}.");
    }

    private static void SuccessfulKnockbackUsesTargetActionAndOneDamage()
    {
        var world = CreateWorld(special1: 100);
        var result = world.TryApplySkillAttack(
            1, 2, Skill, ItemData, weather: 0, randomFactor: 99, out var outcome,
            instanceRandomFactor: 0, attackMissRoll: 99, targetIndex: 0);

        Check(result == LegacySkillAttackResult.Accepted && outcome is not null,
            $"Furia Divina did not accept its player target: {result}.");
        var applied = outcome ?? throw new InvalidOperationException("Furia Divina returned no player outcome.");
        Check(applied.Damage == 1,
            $"7.69 Furia Divina did not replace the generic skill damage with 1: {applied.Damage}.");
        Check(applied.ActionRelay is { ActorConnectionId: 2, PositionX: 2_005, PositionY: 2_005, Effect: 2, Speed: 6, TargetX: 2_001, TargetY: 2_001 },
            $"Furia Divina did not relay the successful target knockback to the first available adjacent grid: {applied.ActionRelay}.");
    }

    private static void ChanceFailureKeepsTheAttackAndContinuesTheTargetBatch()
    {
        var world = CreateWorld(special1: 0, includeThirdTarget: true);
        var request = new LegacyAttackActionBatchRequest(
            1,
            [
                SkillSlot(0, 2, instanceRoll: 99),
                SkillSlot(1, 3, instanceRoll: 99),
            ],
            Skill,
            ItemData);

        Check(world.TryApplyAttackActionBatch(request, out var outcome) && outcome is { CompletedNormally: true },
            "Furia Divina chance failure aborted the attack batch.");
        var batch = outcome ?? throw new InvalidOperationException("Furia Divina returned no batch outcome.");
        Check(batch.SlotOutcomes.Count == 2 && batch.SlotOutcomes.All(slot => slot.SkillOutcome is { Damage: 1, ActionRelay: null }),
            $"7.69 chance failure did not retain one damage and continue through the remaining targets: {string.Join(';', batch.SlotOutcomes.Select(slot => $"{slot.SlotIndex}:{slot.SkillOutcome?.Damage}:{slot.SkillOutcome?.ActionRelay}"))}.");
    }

    private static void ProtectedTargetStopsTheRemainingTargetBatch()
    {
        foreach (var headIndex in new short[] { 219, 220, 362 })
            AssertProtectedPlayerBatchStops(headIndex, targetClan: 0);
        AssertProtectedPlayerBatchStops(targetHeadIndex: 0, targetClan: 6);

        foreach (var (merchant, generateIndex, clan) in new[]
                 {
                     ((byte)2, -1, (byte)0),
                     ((byte)0, 8, (byte)0),
                     ((byte)0, 9, (byte)0),
                     ((byte)0, -1, (byte)6),
                 })
        {
            var world = CreateWorld(special1: 100);
            var npcMob = CreateMob(hp: 10_000, special1: 0, merchant: merchant);
            npcMob[LegacyAccountSnapshot.MobClanOffset] = clan;
            var npcId = world.EnterNpc(npcMob, 2_005, 2_005, requestedConnectionId: 20_001, generateIndex: generateIndex);
            var result = world.TryApplySkillAttack(
                1, npcId, Skill, ItemData, weather: 0, randomFactor: 99, out var outcome,
                instanceRandomFactor: 0, attackMissRoll: 99, targetIndex: 0);

            Check(result == LegacySkillAttackResult.Accepted && outcome is { Damage: 0, StopRemainingTargets: true },
                $"Furia Divina did not terminate at the protected NPC (merchant={merchant}, generateIndex={generateIndex}, clan={clan}): result={result}, outcome={outcome}.");
        }
    }

    private static void SuccessfulNpcKnockbackSetsNpcCombatTarget()
    {
        var world = CreateWorld(special1: 100);
        var npcId = world.EnterNpc(CreateMob(hp: 10_000, special1: 0), 2_005, 2_005, requestedConnectionId: 20_001);
        var result = world.TryApplySkillAttack(
            1, npcId, Skill, ItemData, weather: 0, randomFactor: 99, out var outcome,
            instanceRandomFactor: 0, attackMissRoll: 99, targetIndex: 0);

        Check(result == LegacySkillAttackResult.Accepted && outcome is not null,
            $"Furia Divina did not accept its NPC target: {result}.");
        Check(outcome!.ActionRelay is { ActorConnectionId: 20_001, PositionX: 2_005, PositionY: 2_005, Effect: 2, Speed: 6, TargetX: 2_001, TargetY: 2_001 },
            $"Furia Divina did not relay a successful NPC knockback: {outcome.ActionRelay}.");
        Check(world.TryGetNpcCombatState(npcId, out var mode, out var currentTarget, out _) &&
            mode == LegacyNpcMode.Combat && currentTarget == 1,
            $"Furia Divina did not put the NPC into combat against its caster: mode={mode}, target={currentTarget}.");
    }

    private static LegacyAttackActionBatchSlotRequest SkillSlot(int slot, int target, int instanceRoll) =>
        new(slot, LegacyAttackActionSlotKind.Skill, target,
            SkillRolls: new LegacySkillAttackBatchSlotRolls(
                RandomFactor: 99,
                InstanceRandomFactor: instanceRoll,
                AttackMissRoll: 99));

    private static void AssertProtectedPlayerBatchStops(short targetHeadIndex, byte targetClan)
    {
        var world = CreateWorld(special1: 100, includeThirdTarget: true, targetHeadIndex: targetHeadIndex, targetClan: targetClan);
        var request = new LegacyAttackActionBatchRequest(
            1,
            [SkillSlot(0, 2, instanceRoll: 0), SkillSlot(1, 3, instanceRoll: 0)],
            Skill,
            ItemData);

        Check(world.TryApplyAttackActionBatch(request, out var outcome) && outcome is not null,
            "Furia Divina protected-target batch was rejected before the target loop.");
        var batch = outcome ?? throw new InvalidOperationException("Furia Divina returned no blocked-target batch outcome.");
        Check(batch.SlotOutcomes.Count == 1 && batch.SlotOutcomes[0].SkillOutcome is { Damage: 0, StopRemainingTargets: true },
            $"The protected Furia Divina target did not stop the remaining target loop (head={targetHeadIndex}, clan={targetClan}): slots={batch.SlotOutcomes.Count}, outcome={batch.SlotOutcomes.FirstOrDefault()?.SkillOutcome}.");
    }

    private static WorldHub CreateWorld(short special1, bool includeThirdTarget = false, short targetHeadIndex = 0, byte targetClan = 0)
    {
        var world = new WorldHub();
        Check(world.Enter(1, "FURIA_CASTER", (_, _) => ValueTask.CompletedTask), "Furia Divina caster was not registered.");
        Check(world.Enter(2, "FURIA_TARGET_ONE", (_, _) => ValueTask.CompletedTask), "Furia Divina first target was not registered.");
        if (includeThirdTarget)
            Check(world.Enter(3, "FURIA_TARGET_TWO", (_, _) => ValueTask.CompletedTask), "Furia Divina second target was not registered.");

        var casterMob = CreateMob(hp: 10_000, special1);
        Check(world.SetCharacterState(1, 0, 0, 0, 0, 0, 2_000, 2_000, casterMob), "Furia Divina caster state was not installed.");
        var firstTarget = CreateMob(hp: 10_000, special1: 0);
        if (targetHeadIndex != 0)
            BinaryPrimitives.WriteInt16LittleEndian(firstTarget.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset), targetHeadIndex);
        firstTarget[LegacyAccountSnapshot.MobClanOffset] = targetClan;
        Check(world.SetCharacterState(2, 0, 0, 0, 0, 0, 2_005, 2_005, firstTarget), "Furia Divina first target state was not installed.");
        if (includeThirdTarget)
            Check(world.SetCharacterState(3, 0, 0, 0, 0, 0, 2_006, 2_006, CreateMob(hp: 10_000, special1: 0)), "Furia Divina second target state was not installed.");

        return world;
    }

    private static byte[] CreateMob(int hp, short special1, byte merchant = 0)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(100, 0, 0, merchant, 0, 0, 0, 10_000, 1_000, hp, 1_000,
            0, 0, 1_000, 0, special1, 0, 0, 0);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        return mob;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

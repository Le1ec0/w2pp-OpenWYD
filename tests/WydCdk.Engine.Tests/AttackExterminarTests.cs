using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackExterminarTests
{
    private const string SkillRow = "22,90,1,0,9,3,2,90,0,0,0,0,0,9.0.0.7.0.0.0.0,9.0.0.7.0.0.0.0,1,0,1,1,0,1,0,1,Exterminar";
    private static readonly LegacyItemDataTable ItemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    public static void Run()
    {
        ExterminarAddsCurrentManaAndHalfIntelligenceThenZerosCurrentMana();
        ExterminarSkipsActionForSpecialTargetHeads();
        ExterminarAlsoAppliesToNpcTargets();
    }

    private static void ExterminarAddsCurrentManaAndHalfIntelligenceThenZerosCurrentMana()
    {
        var (world, skill) = CreateWorld(targetHeadIndex: 0);
        var result = world.TryApplySkillAttack(
            1, 2, skill, ItemData, weather: 0, randomFactor: 99, out var outcome,
            attackMissRoll: 99, targetIndex: 0);

        Check(result == LegacySkillAttackResult.Accepted && outcome is not null,
            $"Exterminar did not accept its player target: {result}.");
        var applied = outcome ?? throw new InvalidOperationException("Exterminar returned no outcome after accepting the target.");
        Check(applied.BaseDamage == 271 && applied.Damage == 936 && applied.RemainingHp == 9_064,
            $"Exterminar did not add CurrentMP + Intelligence/2 after the legacy skill roll: base={applied.BaseDamage}, damage={applied.Damage}, hp={applied.RemainingHp}.");
        Check(applied.ActionRelay is { PositionX: 2_000, PositionY: 2_000, Effect: 2, Speed: 2, TargetX: 2_004, TargetY: 2_004 },
            $"Exterminar did not produce the 7.69 caster action to the first available target-adjacent grid: {applied.ActionRelay}.");
        Check(world.TryGetResourceState(1, out var caster, out _, out var requestedMana) &&
            caster is { CurrentMana: 0 } && requestedMana == 300,
            $"Exterminar did not zero only current MP while preserving the 7.69 requested-MP value: current={caster?.CurrentMana}, requested={requestedMana}.");
    }

    private static void ExterminarSkipsActionForSpecialTargetHeads()
    {
        foreach (short targetHeadIndex in new short[] { 219, 220 })
        {
            var (world, skill) = CreateWorld(targetHeadIndex);
            var result = world.TryApplySkillAttack(
                1, 2, skill, ItemData, weather: 0, randomFactor: 99, out var outcome,
                attackMissRoll: 99, targetIndex: 0);

            Check(result == LegacySkillAttackResult.Accepted && outcome is { Damage: 936, ActionRelay: null },
                $"Exterminar did not suppress its action for target head {targetHeadIndex}: result={result}, damage={outcome?.Damage}, action={outcome?.ActionRelay}.");
        }
    }

    private static void ExterminarAlsoAppliesToNpcTargets()
    {
        var casterMob = CreateMob(hp: 10_000, mp: 300, intelligence: 200, dexterity: 1_000);
        casterMob[LegacyAccountSnapshot.MobClassOffset] = 1;
        var npcMob = CreateMob(hp: 10_000, mp: 0, intelligence: 0, dexterity: 1_000);
        var world = new WorldHub();
        Check(world.Enter(1, "EXTERMINAR_NPC_CASTER", (_, _) => ValueTask.CompletedTask),
            "Exterminar NPC caster was not registered.");
        Check(world.SetCharacterState(1, 0, 0, 0, 0, 0, 2_000, 2_000, casterMob),
            "Exterminar NPC caster state was not installed.");
        var npcId = world.EnterNpc(npcMob, 2_005, 2_005, requestedConnectionId: 20_001);
        Check(npcId == 20_001, "Exterminar NPC target was not registered.");
        var skill = LegacySkillDataTable.Load(new StringReader(SkillRow))[22]
            ?? throw new InvalidOperationException("The Exterminar SkillData fixture did not load.");

        var result = world.TryApplySkillAttack(
            1, npcId, skill, ItemData, weather: 0, randomFactor: 99, out var outcome,
            attackMissRoll: 99, targetIndex: 0);

        Check(result == LegacySkillAttackResult.Accepted && outcome is { Damage: 936, ActionRelay: { TargetX: 2_004, TargetY: 2_004 } },
            $"Exterminar did not apply its damage and action relay to the NPC target: result={result}, damage={outcome?.Damage}, action={outcome?.ActionRelay}.");
        Check(world.TryGetResourceState(1, out var caster, out _, out var requestedMana) &&
            caster is { CurrentMana: 0 } && requestedMana == 300,
            "Exterminar NPC target did not zero only caster Current MP.");
    }

    private static (WorldHub World, LegacySkillDefinition Skill) CreateWorld(short targetHeadIndex)
    {
        var casterMob = CreateMob(hp: 10_000, mp: 300, intelligence: 200, dexterity: 1_000);
        casterMob[LegacyAccountSnapshot.MobClassOffset] = 1;
        var targetMob = CreateMob(hp: 10_000, mp: 0, intelligence: 0, dexterity: 1_000);
        if (targetHeadIndex != 0)
            BitConverter.GetBytes(targetHeadIndex).CopyTo(targetMob, LegacyAccountSnapshot.MobEquipmentOffset);

        var world = new WorldHub();
        Check(world.Enter(1, "EXTERMINAR_CASTER", (_, _) => ValueTask.CompletedTask) &&
            world.Enter(2, "EXTERMINAR_TARGET", (_, _) => ValueTask.CompletedTask),
            "Exterminar participants were not registered.");
        Check(world.SetCharacterState(1, 0, 0, 0, 0, 0, 2_000, 2_000, casterMob) &&
            world.SetCharacterState(2, 0, 0, 0, 0, 0, 2_005, 2_005, targetMob),
            "Exterminar character states were not installed.");
        var skill = LegacySkillDataTable.Load(new StringReader(SkillRow))[22]
            ?? throw new InvalidOperationException("The Exterminar SkillData fixture did not load.");
        return (world, skill);
    }

    private static byte[] CreateMob(int hp, int mp, short intelligence, short dexterity)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(50, 0, 0, 0, 0, 0, 0, 10_000, 1_000, hp, mp,
            0, intelligence, dexterity, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        new LegacyScore(50, 0, 0, 0, 0, 0, 0, 10_000, 1_000, hp, mp,
            0, intelligence, dexterity, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        return mob;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

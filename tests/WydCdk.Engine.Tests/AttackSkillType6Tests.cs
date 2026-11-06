using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackSkillType6Tests
{
    private static readonly LegacyItemDataTable ItemData = LegacyItemDataTable.Load(
        new byte[LegacyItemDataTable.BodySizeInBytes]);

    public static void Run()
    {
        Type6BlocksPositiveSkillDamageForClassesZeroAndOne();
        OtherClassesAndAffectTypesKeepTakingSkillDamage();
        Type6DoesNotReplaceAnAttackMiss();
        Type6DoesNotBlockHealing();
        Type6BlocksPositiveSkillDamageForNpcClassesZeroAndOne();
        OtherNpcClassesAndAffectTypesKeepTakingSkillDamage();
    }

    private static void Type6BlocksPositiveSkillDamageForClassesZeroAndOne()
    {
        foreach (var targetClass in new byte[] { 0, 1 })
        {
            var (world, targetId) = CreateWorld(targetClass, affectType: 6);
            var result = world.TryApplySkillAttack(
                1, targetId, CreateLightningSkill(), ItemData, weather: 0, randomFactor: 99,
                out var outcome, attackMissRoll: 99);

            Check(result == LegacySkillAttackResult.Accepted && outcome is
                { BaseDamage: 100, Damage: 0, RemainingHp: 1_000, TargetHpChanged: false },
                $"Class {targetClass} with Type 6 did not suppress positive skill damage: result={result}, base={outcome?.BaseDamage}, damage={outcome?.Damage}, hp={outcome?.RemainingHp}.");
            Check(world.TryGetResourceState(targetId, out var state, out var requestedHp, out _) &&
                state is { CurrentScore.Hp: 1_000 } && requestedHp == 1_000,
                $"Class {targetClass} with Type 6 changed authoritative or requested HP.");
        }
    }

    private static void OtherClassesAndAffectTypesKeepTakingSkillDamage()
    {
        foreach (var (targetClass, affectType) in new[] { ((byte)2, (byte)6), ((byte)1, (byte)5) })
        {
            var (world, targetId) = CreateWorld(targetClass, affectType);
            var result = world.TryApplySkillAttack(
                1, targetId, CreateLightningSkill(), ItemData, weather: 0, randomFactor: 99,
                out var outcome, attackMissRoll: 99);

            Check(result == LegacySkillAttackResult.Accepted && outcome is
                { Damage: 100, RemainingHp: 900, TargetHpChanged: true },
                $"Class {targetClass} with affect {affectType} was incorrectly protected: result={result}, damage={outcome?.Damage}, hp={outcome?.RemainingHp}.");
        }
    }

    private static void Type6DoesNotReplaceAnAttackMiss()
    {
        var (world, targetId) = CreateWorld(targetClass: 1, affectType: 6, targetDexterity: 1_100);
        var result = world.TryApplySkillAttack(
            1, targetId, CreateLightningSkill(), ItemData, weather: 0, randomFactor: 99,
            out var outcome, attackMissRoll: 0);

        Check(result == LegacySkillAttackResult.Accepted && outcome is
            { Damage: -3, RemainingHp: 1_000, TargetHpChanged: false },
            $"Type 6 replaced the 7.69 miss result: result={result}, damage={outcome?.Damage}, hp={outcome?.RemainingHp}.");
    }

    private static void Type6DoesNotBlockHealing()
    {
        var attackerMob = CreateMob(hp: 1_000, damage: 0, dexterity: 1_000);
        new LegacyScore(50, 0, 0, 0, 0, 0, 0, 1_000, 1_000, 1_000, 1_000,
            0, 0, 1_000, 0, 0, 100, 0, 0)
            .Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));

        var targetMob = CreateMob(hp: 500, damage: 100, dexterity: 1_000);
        targetMob[LegacyAccountSnapshot.MobClassOffset] = 1;
        var targetAffect = CreateAffect(6);
        var world = new WorldHub();
        Check(world.Enter(1, "TYPE6_HEALER", (_, _) => ValueTask.CompletedTask) &&
            world.Enter(2, "TYPE6_HEALED", (_, _) => ValueTask.CompletedTask),
            "Type 6 healing participants were not registered.");
        Check(world.SetCharacterState(1, 0, 0, 1, 0, 0, 2_000, 2_000, attackerMob) &&
            world.SetCharacterState(2, 0, 0, 2, 0, 0, 2_005, 2_005, targetMob,
                LegacyAccountSnapshot.ClassMasterMortal, targetAffect),
            "Type 6 healing fixture was not installed.");

        var heal = new LegacySkillDefinition(28, 0, 0, 0, 0, 0, 6, 20, 0, 0, 0, 0, 0,
            [], [], 0, 0, 0, 1, 0, 0, 0, 0, "Cura");
        var result = world.TryApplySkillAttack(1, 2, heal, itemData: null, weather: 0,
            randomFactor: 90, out var outcome);

        Check(result == LegacySkillAttackResult.Accepted && outcome is
            { Damage: -320, RemainingHp: 820, TargetHpChanged: true },
            $"Type 6 blocked a healing skill: result={result}, damage={outcome?.Damage}, hp={outcome?.RemainingHp}.");
    }

    private static void Type6BlocksPositiveSkillDamageForNpcClassesZeroAndOne()
    {
        foreach (var npcClass in new byte[] { 0, 1 })
        {
            var (world, npcId) = CreateNpcWorld(npcClass, affectType: 6);
            var result = world.TryApplySkillAttack(
                1, npcId, CreateLightningSkill(), ItemData, weather: 0, randomFactor: 99,
                out var outcome);

            Check(result == LegacySkillAttackResult.Accepted && outcome is
                { Damage: 0, RemainingHp: 1_000, TargetHpChanged: false, TargetRemoved: false },
                $"NPC class {npcClass} with Type 6 did not suppress positive skill damage: result={result}, damage={outcome?.Damage}, hp={outcome?.RemainingHp}.");
            Check(world.TryGetNpcSnapshot(npcId, out var npc) && npc is not null &&
                LegacyMobCombatState.Read(npc.MobSnapshot).CurrentScore.Hp == 1_000,
                $"NPC class {npcClass} with Type 6 did not retain its authoritative HP.");
        }
    }

    private static void OtherNpcClassesAndAffectTypesKeepTakingSkillDamage()
    {
        foreach (var (npcClass, affectType) in new[] { ((byte)2, (byte)6), ((byte)1, (byte)5) })
        {
            var (world, npcId) = CreateNpcWorld(npcClass, affectType);
            var result = world.TryApplySkillAttack(
                1, npcId, CreateLightningSkill(), ItemData, weather: 0, randomFactor: 99,
                out var outcome);

            Check(result == LegacySkillAttackResult.Accepted && outcome is
                { Damage: 100, RemainingHp: 900, TargetRemoved: false } &&
                world.TryGetNpcSnapshot(npcId, out var npc) && npc is not null &&
                LegacyMobCombatState.Read(npc.MobSnapshot).CurrentScore.Hp == 900,
                $"NPC class {npcClass} with affect {affectType} was incorrectly protected: result={result}, damage={outcome?.Damage}, hp={outcome?.RemainingHp}.");
        }
    }

    private static (WorldHub World, int NpcId) CreateNpcWorld(byte npcClass, byte affectType)
    {
        var attackerMob = CreateMob(hp: 1_000, damage: 100, dexterity: 1_000);
        var npcMob = CreateMob(hp: 1_000, damage: 100, dexterity: 1_000);
        npcMob[LegacyAccountSnapshot.MobClassOffset] = npcClass;
        var world = new WorldHub();
        Check(world.Enter(1, "TYPE6_NPC_SKILL_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
            world.SetCharacterState(1, 0, 0, 1, 0, 0, 2_000, 2_000, attackerMob),
            "Type 6 NPC attacker was not registered or initialized.");
        var npcId = world.EnterNpc(npcMob, 2_005, 2_005, CreateAffect(affectType), requestedConnectionId: 20_010);
        Check(npcId == 20_010, $"Type 6 NPC target could not be registered: {npcId}.");
        return (world, npcId);
    }

    private static (WorldHub World, int TargetId) CreateWorld(
        byte targetClass,
        byte affectType,
        short targetDexterity = 1_000)
    {
        var attackerMob = CreateMob(hp: 1_000, damage: 100, dexterity: 1_000);
        var targetMob = CreateMob(hp: 1_000, damage: 100, dexterity: targetDexterity);
        targetMob[LegacyAccountSnapshot.MobClassOffset] = targetClass;
        var world = new WorldHub();
        Check(world.Enter(1, "TYPE6_SKILL_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
            world.Enter(2, "TYPE6_SKILL_TARGET", (_, _) => ValueTask.CompletedTask),
            "Type 6 skill participants were not registered.");
        Check(world.SetCharacterState(1, 0, 0, 1, 0, 0, 2_000, 2_000, attackerMob) &&
            world.SetCharacterState(2, 0, 0, 2, 0, 0, 2_005, 2_005, targetMob,
                LegacyAccountSnapshot.ClassMasterMortal, CreateAffect(affectType)),
            "Type 6 skill fixture was not installed.");
        return (world, 2);
    }

    private static byte[] CreateMob(int hp, int damage, short dexterity)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(50, 0, damage, 0, 0, 0, 0, 1_000, 1_000, hp, 1_000,
            0, 0, dexterity, 0, 0, 100, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static byte[] CreateAffect(byte type)
    {
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[^8] = type;
        return affect;
    }

    private static LegacySkillDefinition CreateLightningSkill() =>
        new(79, 69, 1, 0, 0, 1, 1, 40, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 0, 0, 0, 0, 0,
            "TYPE6_LIGHTNING");

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

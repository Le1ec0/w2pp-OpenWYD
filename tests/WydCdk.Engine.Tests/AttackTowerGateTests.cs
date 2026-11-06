using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackTowerGateTests
{
    private const int TowerGenerateIndex = 1076;
    private const int TowerConnectionId = 20_001;

    public static void Run()
    {
        GateUsesThe769GenerateIndexAndGuildRules();
        PhysicalAttackBlocksOnlyTowerGuildlessAndOwnGuild();
        SkillAndExcessTargetGateBlockBeforeEffectsAndType28();
    }

    private static void GateUsesThe769GenerateIndexAndGuildRules()
    {
        Check(!LegacyAttackTowerGate.IsBlocked(1_078, 0, 0), "W2PP's divergent GenerateIndex was treated as the 7.69 tower.");
        Check(!LegacyAttackTowerGate.IsBlocked(1_076, 9, 8), "An enemy guild was blocked from the 7.69 tower.");
        Check(LegacyAttackTowerGate.IsBlocked(1_076, 0, 8), "A guildless attacker was allowed to attack the 7.69 tower.");
        Check(LegacyAttackTowerGate.IsBlocked(1_076, 8, 8), "The tower's own guild was allowed to attack it.");
        Check(!LegacyAttackTowerGate.IsBlocked(1_076, 8, 9), "A tower owned by another guild was blocked.");
    }

    private static void PhysicalAttackBlocksOnlyTowerGuildlessAndOwnGuild()
    {
        var guildless = CreateWorld(attackerGuild: 0, towerGuild: 8, towerGenerateIndex: TowerGenerateIndex);
        Check(guildless.Hub.TryApplyPhysicalAttack(1, TowerConnectionId, 99, out var guildlessOutcome) == LegacyPhysicalAttackResult.TowerAttackBlocked && guildlessOutcome is null,
            "A guildless physical attack was not suppressed with a cleared slot outcome.");
        AssertTowerHp(guildless.Hub, 10_000, "A guildless physical attack changed tower HP before being blocked.");

        var ownGuild = CreateWorld(attackerGuild: 8, towerGuild: 8, towerGenerateIndex: TowerGenerateIndex);
        Check(ownGuild.Hub.TryApplyPhysicalAttack(1, TowerConnectionId, 99, out var ownGuildOutcome) == LegacyPhysicalAttackResult.TowerAttackBlocked && ownGuildOutcome is null,
            "A same-guild physical attack was not suppressed with a cleared slot outcome.");
        AssertTowerHp(ownGuild.Hub, 10_000, "A same-guild physical attack changed tower HP before being blocked.");

        var enemyGuild = CreateWorld(attackerGuild: 9, towerGuild: 8, towerGenerateIndex: TowerGenerateIndex);
        Check(enemyGuild.Hub.TryApplyPhysicalAttack(1, TowerConnectionId, 99, out var enemyOutcome) == LegacyPhysicalAttackResult.Accepted && enemyOutcome is { Damage: > 0 },
            "An enemy guild could not damage the 7.69 tower.");

        var ordinaryNpc = CreateWorld(attackerGuild: 0, towerGuild: 8, towerGenerateIndex: 1_078);
        Check(ordinaryNpc.Hub.TryApplyPhysicalAttack(1, TowerConnectionId, 99, out var ordinaryOutcome) == LegacyPhysicalAttackResult.Accepted && ordinaryOutcome is { Damage: > 0 },
            "The tower gate blocked a normal NPC attack.");
    }

    private static void SkillAndExcessTargetGateBlockBeforeEffectsAndType28()
    {
        var world = CreateWorld(attackerGuild: 0, towerGuild: 8, towerGenerateIndex: TowerGenerateIndex);
        var skill = new LegacySkillDefinition(10, 69, 1, 0, 0, 1, 1, 40, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 0, 0, 0, 0, 0, "TOWER_TEST");
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

        Check(world.Hub.TryApplySkillAttack(1, TowerConnectionId, skill, itemData, 0, 99, out var skillOutcome, targetIndex: 0, clientTick: 100) == LegacySkillAttackResult.TowerAttackBlocked && skillOutcome is null,
            "A skill attack changed a tower before applying the guild gate.");
        AssertTowerHp(world.Hub, 10_000, "A blocked skill attack mutated tower HP.");

        var overLimitSkill = skill with { MaxTarget = -1 };
        Check(world.Hub.TryApplySkillAttack(1, TowerConnectionId, overLimitSkill, itemData, 0, 99, out var excessOutcome, targetIndex: 0, clientTick: 100) == LegacySkillAttackResult.TowerAttackBlocked && excessOutcome is null,
            "An ineligible tower slot reached the type28 max-target result instead of being cleared first.");
        Check(world.Hub.TryValidateSkillTargetLimit(1, TowerConnectionId, overLimitSkill, targetIndex: 0, clientTick: 100) == LegacySkillAttackResult.TowerAttackBlocked,
            "The independent max-target gate allowed type28 to preempt tower eligibility.");
        AssertTowerHp(world.Hub, 10_000, "An over-limit tower slot mutated tower HP before type28 eligibility was skipped.");
    }

    private static void AssertTowerHp(WorldHub hub, int expectedHp, string message)
    {
        Check(hub.TryGetNpcSnapshot(TowerConnectionId, out var tower) && tower is not null &&
            LegacyMobCombatState.Read(tower.MobSnapshot).CurrentScore.Hp == expectedHp, message);
    }

    private static (WorldHub Hub, byte[] AttackerMob, byte[] TowerMob) CreateWorld(ushort attackerGuild, ushort towerGuild, int towerGenerateIndex)
    {
        var attackerMob = CreateMob("TOWER_ATTACKER", attackerGuild, hp: 1_000, damage: 100);
        var towerMob = CreateMob("GUILD_TOWER", towerGuild, hp: 10_000, damage: 0);
        var hub = new WorldHub();
        Check(hub.Enter(1, "TOWER_ATTACKER", (_, _) => ValueTask.CompletedTask), "Tower-test attacker was not registered.");

        // Deliberately disagree with the MOB guild field to prove the gate uses pMob[].MOB.Guild semantics.
        Check(hub.SetCharacterState(1, 0, guildId: 77, clan: 1, guildLevel: 0, coin: 0, 2_000, 2_000, attackerMob),
            "Tower-test attacker state was not installed.");
        var npcId = hub.EnterNpc(towerMob, 2_005, 2_000, requestedConnectionId: TowerConnectionId, generateIndex: towerGenerateIndex);
        Check(npcId == TowerConnectionId, "Tower-test NPC was not registered with its reference GenerateIndex.");
        return (hub, attackerMob, towerMob);
    }

    private static byte[] CreateMob(string name, ushort guild, int hp, int damage)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var encodedName = System.Text.Encoding.ASCII.GetBytes(name);
        encodedName.AsSpan(0, Math.Min(encodedName.Length, 15)).CopyTo(mob);
        BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset), guild);
        new LegacyScore(50, 0, damage, 0, 100, 0, 0, hp, 1_000, hp, 1_000, 100, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

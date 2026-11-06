using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackMissTests
{
    public static void Run()
    {
        RateUsesThe769AsymmetricDexterityFormula();
        RateAppliesAttackerResistanceBonusesBeforeItsCap();
        PhysicalAttackUsesReleaseMissRollAndIgnoresDefenderParry();
        SkillMissesPlayerTargetsButNotOrdinaryNpcs();
    }

    private static void RateUsesThe769AsymmetricDexterityFormula()
    {
        Check(LegacyAttackMissMath.GetRatePercent(targetDexterity: 1_100, attackerDexterity: 1_000, attackerRsv: 0) == 3,
            "A 100-point target Dexterity lead should produce 3% miss chance.");
        Check(LegacyAttackMissMath.GetRatePercent(targetDexterity: 1_099, attackerDexterity: 1_000, attackerRsv: 0) == 0,
            "A sub-100 target Dexterity lead should truncate to 0% before the target-side multiplier.");
        Check(LegacyAttackMissMath.GetRatePercent(targetDexterity: 6_000, attackerDexterity: 1_000, attackerRsv: 0) == 75,
            "A large target Dexterity lead should cap at 75%.");
        Check(LegacyAttackMissMath.GetRatePercent(targetDexterity: 1_000, attackerDexterity: 3_000, attackerRsv: 0) == 10,
            "A large attacker Dexterity lead should cap at 10% rather than using the target-side multiplier.");
        Check(LegacyAttackMissMath.GetRatePercent(targetDexterity: 1_000, attackerDexterity: 1_000, attackerRsv: 0) == 1,
            "Equal Dexterity should retain the 1% miss chance.");
        Check(LegacyAttackMissMath.GetRatePercent(targetDexterity: 1_000, attackerDexterity: 1_099, attackerRsv: 0) == 0,
            "An attacker Dexterity lead below 100 should truncate to 0%.");
    }

    private static void RateAppliesAttackerResistanceBonusesBeforeItsCap()
    {
        Check(LegacyAttackMissMath.GetRatePercent(targetDexterity: 1_300, attackerDexterity: 1_000, attackerRsv: 0x2E0) == 1,
            "Attacker Rsv precision bonuses were not added to attack Dexterity before comparing the target.");
        Check(LegacyAttackMissMath.GetRatePercent(targetDexterity: 1_000, attackerDexterity: 1_000, attackerRsv: 0x2E0) == 3,
            "The 7.69 Max-greater-than-Min branch did not produce its literal 3% rate after attacker Rsv bonuses.");
    }

    private static void PhysicalAttackUsesReleaseMissRollAndIgnoresDefenderParry()
    {
        var playerWorld = CreateWorld(targetIsNpc: false, targetRsv: 0x200);
        Check(playerWorld.Hub.TryApplyPhysicalAttack(1, playerWorld.TargetId, 99, out var miss, attackMissRoll: 2) == LegacyPhysicalAttackResult.Accepted &&
            miss is { Damage: -3, RemainingHp: 1_000, TargetHpChanged: false },
            "A release miss roll below the 3% rate did not return -3 while preserving target HP.");
        Check(playerWorld.Hub.TryApplyPhysicalAttack(1, playerWorld.TargetId, 99, out var hit, attackMissRoll: 3) == LegacyPhysicalAttackResult.Accepted &&
            hit is { Damage: > 0, RemainingHp: < 1_000 },
            "The exclusive 3% boundary or defender Rsv 0x200 incorrectly converted a release hit into legacy parry/-4.");

        var npcWorld = CreateWorld(targetIsNpc: true);
        Check(npcWorld.Hub.TryApplyPhysicalAttack(1, npcWorld.TargetId, 99, out var npcMiss, attackMissRoll: 2) == LegacyPhysicalAttackResult.Accepted &&
            npcMiss is { Damage: > 0, RemainingHp: < 1_000 },
            "An ordinary NPC target entered the 7.69 player/summoner miss branch.");
    }

    private static void SkillMissesPlayerTargetsButNotOrdinaryNpcs()
    {
        var skill = new LegacySkillDefinition(79, 69, 1, 0, 0, 1, 1, 40, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 0, 0, 0, 0, 0, "ATTACK_MISS_TEST");
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

        foreach (var targetIsNpc in new[] { false, true })
        {
            var world = CreateWorld(targetIsNpc);
            var result = world.Hub.TryApplySkillAttack(1, world.TargetId, skill, itemData, weather: 0, randomFactor: 99, out var outcome, attackMissRoll: 0);
            if (targetIsNpc)
                Check(result == LegacySkillAttackResult.Accepted && outcome is { Damage: > 0, RemainingHp: < 1_000 },
                    "An ordinary NPC skill target entered the 7.69 player/summoner miss branch.");
            else
                Check(result == LegacySkillAttackResult.Accepted && outcome is { Damage: -3, RemainingHp: 1_000, TargetHpChanged: false },
                    "The 7.69 miss result was not applied to a player skill target.");
        }
    }

    private static (WorldHub Hub, int TargetId) CreateWorld(bool targetIsNpc, ushort targetRsv = 0)
    {
        var attackerMob = CreateMob("ATTACK_MISSER", damage: 100, dexterity: 1_000);
        var targetMob = CreateMob("ATTACK_MISS_TARGET", damage: 100, dexterity: 1_100);
        BinaryPrimitives.WriteUInt16LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), targetRsv);

        var hub = new WorldHub();
        Check(hub.Enter(1, "ATTACK_MISSER", (_, _) => ValueTask.CompletedTask), "The miss-test attacker was not registered.");
        Check(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2_000, 2_000, attackerMob), "The miss-test attacker state was not installed.");

        if (targetIsNpc)
        {
            var npcId = hub.EnterNpc(targetMob, 2_005, 2_005, requestedConnectionId: 20_001);
            Check(npcId == 20_001, "The miss-test NPC was not registered.");
            return (hub, npcId);
        }

        Check(hub.Enter(2, "ATTACK_MISS_TARGET", (_, _) => ValueTask.CompletedTask), "The miss-test target was not registered.");
        Check(hub.SetCharacterState(2, 0, 0, 2, 0, 0, 2_005, 2_005, targetMob), "The miss-test target state was not installed.");
        return (hub, 2);
    }

    private static byte[] CreateMob(string name, int damage, short dexterity)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var encodedName = System.Text.Encoding.ASCII.GetBytes(name);
        encodedName.AsSpan(0, Math.Min(encodedName.Length, 15)).CopyTo(mob);
        new LegacyScore(50, 0, damage, 0, 0, 0, 0, 1_000, 1_000, 1_000, 1_000, 0, 0, dexterity, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

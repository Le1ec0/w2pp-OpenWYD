using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackClanSixDamageGateTests
{
    private const int AttackerId = 1;
    private const int TargetPlayerId = 2;
    private const int TargetNpcId = 20_001;
    private const int TargetHp = 10_000;

    public static void PhysicalAttackSuppressesClanSixDamage()
    {
        AssertPhysicalAttack(targetClan: 6, targetIsPlayer: false, attackerLevel: 399, shouldDamage: false);
        AssertPhysicalAttack(targetClan: 6, targetIsPlayer: true, attackerLevel: 399, shouldDamage: false);
        AssertPhysicalAttack(targetClan: 5, targetIsPlayer: false, attackerLevel: 50, shouldDamage: true);
    }

    public static void PlayerSkillAttackUsesLegacyHandlerLevelForClanSixGate()
    {
        AssertSkillAttack(targetClan: 6, targetIsPlayer: true, skillLevel: 398, shouldDamage: false);
        AssertSkillAttack(targetClan: 6, targetIsPlayer: true, skillLevel: 399, shouldDamage: true);
        AssertSkillAttack(targetClan: 5, targetIsPlayer: true, skillLevel: 398, shouldDamage: true);
    }

    public static void NpcSkillAttackUsesLegacyHandlerLevelForClanSixGate()
    {
        AssertSkillAttack(targetClan: 6, targetIsPlayer: false, skillLevel: 398, shouldDamage: false);
        AssertSkillAttack(targetClan: 6, targetIsPlayer: false, skillLevel: 399, shouldDamage: true);
        AssertSkillAttack(targetClan: 5, targetIsPlayer: false, skillLevel: 398, shouldDamage: true);
    }

    private static void AssertPhysicalAttack(byte targetClan, bool targetIsPlayer, int attackerLevel, bool shouldDamage)
    {
        var hub = CreateWorld(targetClan, targetIsPlayer, attackerLevel, special3: 0);
        var targetId = targetIsPlayer ? TargetPlayerId : TargetNpcId;
        var result = hub.TryApplyPhysicalAttack(
            AttackerId,
            targetId,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99);

        Check(result == LegacyPhysicalAttackResult.Accepted && outcome is not null,
            "The Clan 6 rule rejected the physical slot instead of resolving it.");
        AssertDamage(outcome!.Damage, outcome.RemainingHp, shouldDamage, "physical");
    }

    private static void AssertSkillAttack(byte targetClan, bool targetIsPlayer, int skillLevel, bool shouldDamage)
    {
        var hub = CreateWorld(targetClan, targetIsPlayer, attackerLevel: 50, special3: skillLevel);
        var targetId = targetIsPlayer ? TargetPlayerId : TargetNpcId;
        var skill = new LegacySkillDefinition(
            10, 69, 1, 0, 0, 1, 1, 40, 0, 0, 0, 0, 0,
            [], [], 0, 0, 1, 1, 0, 0, 0, 0, "CLAN_SIX_DAMAGE_TEST");
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
        var result = hub.TryApplySkillAttack(
            AttackerId,
            targetId,
            skill,
            itemData,
            weather: 0,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99,
            targetIndex: 0,
            clientTick: 100);

        Check(result == LegacySkillAttackResult.Accepted && outcome is not null,
            "The Clan 6 rule rejected the skill slot instead of resolving it.");
        AssertDamage(outcome!.Damage, outcome.RemainingHp, shouldDamage, "skill");
    }

    private static WorldHub CreateWorld(byte targetClan, bool targetIsPlayer, int attackerLevel, int special3)
    {
        var hub = new WorldHub();
        var attackerMob = CreateMob("CLAN_SIX_ATTACKER", clan: 1, level: attackerLevel, special3, damage: 2_000, magic: 2_000);
        Check(hub.Enter(AttackerId, "CLAN_SIX_ATTACKER", (_, _) => ValueTask.CompletedTask),
            "The Clan 6 test attacker was not registered.");
        Check(hub.SetCharacterState(AttackerId, 0, 0, 1, 0, 0, 2_000, 2_000, attackerMob),
            "The Clan 6 test attacker state was not installed.");

        var targetMob = CreateMob("CLAN_SIX_TARGET", targetClan, level: 50, special3: 0, damage: 0, magic: 0);
        if (targetIsPlayer)
        {
            Check(hub.Enter(TargetPlayerId, "CLAN_SIX_TARGET", (_, _) => ValueTask.CompletedTask),
                "The Clan 6 player target was not registered.");
            Check(hub.SetCharacterState(TargetPlayerId, 0, 0, targetClan, 0, 0, 2_005, 2_000, targetMob),
                "The Clan 6 player target state was not installed.");
        }
        else
        {
            var npcId = hub.EnterNpc(targetMob, 2_005, 2_000, requestedConnectionId: TargetNpcId, generateIndex: 1);
            Check(npcId == TargetNpcId, "The Clan 6 test NPC was not registered at its expected index.");
        }

        return hub;
    }

    private static byte[] CreateMob(string name, byte clan, int level, int special3, int damage, int magic)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).AsSpan(0, Math.Min(name.Length, 15)).CopyTo(mob);
        mob[LegacyAccountSnapshot.MobClanOffset] = clan;
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobMagicOffset), magic);
        var score = new LegacyScore(
            Level: level,
            Ac: 0,
            Damage: damage,
            Merchant: 0,
            AttackRun: 0,
            Direction: 0,
            ChaosRate: 0,
            MaxHp: TargetHp,
            MaxMp: 1_000,
            Hp: TargetHp,
            Mp: 1_000,
            Strength: 100,
            Intelligence: 0,
            Dexterity: 0,
            Constitution: 0,
            Special1: 0,
            Special2: 0,
            Special3: checked((short)special3),
            Special4: 0);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static void AssertDamage(int damage, int remainingHp, bool shouldDamage, string attackKind)
    {
        if (shouldDamage)
        {
            Check(damage > 0 && remainingHp < TargetHp,
                $"The Clan 6 gate blocked an allowed {attackKind} target or handler level.");
        }
        else
        {
            Check(damage == 0 && remainingHp == TargetHp,
                $"The {attackKind} attack damaged a Clan 6 target below the legacy handler-level threshold.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

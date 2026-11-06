using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackSameClanNpcGateTests
{
    private const int AttackerId = 1;
    private const int TargetPlayerId = 2;
    private const int TargetNpcId = 20_001;
    private const int TargetHp = 10_000;

    public static void PhysicalSameClanNpcDamageIsSuppressed()
    {
        AssertPhysicalAttack(7, 7, targetIsPlayer: false, shouldDamage: false);
        AssertPhysicalAttack(8, 8, targetIsPlayer: false, shouldDamage: false);
        AssertPhysicalAttack(7, 8, targetIsPlayer: false, shouldDamage: true);
        AssertPhysicalAttack(7, 7, targetIsPlayer: true, shouldDamage: true);
    }

    public static void SkillSameClanNpcDamageIsSuppressed()
    {
        AssertSkillAttack(7, 7, shouldDamage: false);
        AssertSkillAttack(8, 8, shouldDamage: false);
        AssertSkillAttack(7, 8, shouldDamage: true);
    }

    private static void AssertPhysicalAttack(byte attackerClan, byte targetClan, bool targetIsPlayer, bool shouldDamage)
    {
        var hub = CreateWorld(attackerClan, targetClan, targetIsPlayer);
        var targetId = targetIsPlayer ? TargetPlayerId : TargetNpcId;
        var result = hub.TryApplyPhysicalAttack(AttackerId, targetId, randomFactor: 99, out var outcome, attackMissRoll: 99);

        Check(result == LegacyPhysicalAttackResult.Accepted && outcome is not null,
            "The same-clan NPC rule rejected the attack instead of resolving its slot with zero damage.");
        if (shouldDamage)
        {
            Check(outcome!.Damage > 0 && outcome.RemainingHp < TargetHp,
                "The same-clan NPC rule blocked a different clan or a player target.");
        }
        else
        {
            Check(outcome!.Damage == 0 && outcome.RemainingHp == TargetHp,
                $"Physical damage was applied to an NPC from the attacker's clan {attackerClan}.");
        }
    }

    private static void AssertSkillAttack(byte attackerClan, byte targetClan, bool shouldDamage)
    {
        var hub = CreateWorld(attackerClan, targetClan, targetIsPlayer: false);
        var skill = new LegacySkillDefinition(10, 69, 1, 0, 0, 1, 1, 40, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "SAME_CLAN_NPC_TEST");
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
        var result = hub.TryApplySkillAttack(
            AttackerId,
            TargetNpcId,
            skill,
            itemData,
            weather: 0,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99,
            targetIndex: 0,
            clientTick: 100);

        Check(result == LegacySkillAttackResult.Accepted && outcome is not null,
            "The same-clan NPC rule rejected the skill instead of resolving its slot with zero damage.");
        if (shouldDamage)
        {
            Check(outcome!.Damage > 0 && outcome.RemainingHp < TargetHp,
                "The same-clan NPC rule blocked a different-clan skill target.");
        }
        else
        {
            Check(outcome!.Damage == 0 && outcome.RemainingHp == TargetHp,
                $"Skill damage was applied to an NPC from the attacker's clan {attackerClan}.");
        }
    }

    private static WorldHub CreateWorld(byte attackerClan, byte targetClan, bool targetIsPlayer)
    {
        var hub = new WorldHub();
        var attackerMob = CreateMob("SAME_CLAN_ATTACKER", attackerClan, hp: 5_000, damage: 2_000, magic: 2_000);
        Check(hub.Enter(AttackerId, "SAME_CLAN_ATTACKER", (_, _) => ValueTask.CompletedTask),
            "The same-clan test attacker was not registered.");
        Check(hub.SetCharacterState(AttackerId, 0, guildId: 0, clan: attackerClan, guildLevel: 0, coin: 0,
                2_000, 2_000, attackerMob),
            "The same-clan test attacker state was not installed.");

        var targetMob = CreateMob("SAME_CLAN_TARGET", targetClan, hp: TargetHp, damage: 0, magic: 0);
        if (targetIsPlayer)
        {
            Check(hub.Enter(TargetPlayerId, "SAME_CLAN_TARGET", (_, _) => ValueTask.CompletedTask),
                "The same-clan player target was not registered.");
            Check(hub.SetCharacterState(TargetPlayerId, 0, guildId: 0, clan: targetClan, guildLevel: 0, coin: 0,
                    2_005, 2_000, targetMob),
                "The same-clan player target state was not installed.");
        }
        else
        {
            var npcId = hub.EnterNpc(targetMob, 2_005, 2_000, requestedConnectionId: TargetNpcId, generateIndex: 1);
            Check(npcId == TargetNpcId, "The same-clan test NPC was not registered at its expected index.");
        }

        return hub;
    }

    private static byte[] CreateMob(string name, byte clan, int hp, int damage, int magic)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).AsSpan(0, Math.Min(name.Length, 15)).CopyTo(mob);
        mob[LegacyAccountSnapshot.MobClanOffset] = clan;
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobMagicOffset), magic);
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

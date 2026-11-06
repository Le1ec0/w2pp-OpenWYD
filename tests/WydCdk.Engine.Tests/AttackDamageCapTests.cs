using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackDamageCapTests
{
    private const int AttackerId = 1;
    private const int TargetPlayerId = 2;
    private const int TargetNpcId = 20_001;
    private const int DamageCap = 1_000_000;
    private const int TargetHp = 50_000_000;

    public static void PhysicalDamageIsCapped()
    {
        var hub = CreateWorld(targetClan: 5, targetIsPlayer: false, attackerDamage: 8_000_000);
        var itemData = CreateItemData();
        var result = hub.TryApplyPhysicalAttack(
            AttackerId,
            TargetNpcId,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99,
            itemData: itemData);

        Check(result == LegacyPhysicalAttackResult.Accepted && outcome is not null,
            "The damage-cap physical attack did not resolve its NPC slot.");
        Check(outcome!.Damage == DamageCap && outcome.RemainingHp == TargetHp - DamageCap,
            $"The physical MSG_Attack damage was not capped at {DamageCap}: damage={outcome.Damage}, hp={outcome.RemainingHp}.");
    }

    public static void PlayerSkillDamageIsCapped()
    {
        var hub = CreateWorld(targetClan: 5, targetIsPlayer: true, attackerDamage: 2_000);
        var result = hub.TryApplySkillAttack(
            AttackerId,
            TargetPlayerId,
            CreateHighDamageSkill(),
            CreateItemData(),
            weather: 0,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99,
            targetIndex: 0,
            clientTick: 100);

        Check(result == LegacySkillAttackResult.Accepted && outcome is not null,
            "The damage-cap skill attack did not resolve its player slot.");
        Check(outcome!.Damage == DamageCap && outcome.RemainingHp == TargetHp - DamageCap,
            $"The player skill damage was not capped at {DamageCap}: damage={outcome.Damage}, hp={outcome.RemainingHp}.");
    }

    public static void NpcSkillDamageIsCapped()
    {
        var hub = CreateWorld(targetClan: 5, targetIsPlayer: false, attackerDamage: 2_000);
        var result = hub.TryApplySkillAttack(
            AttackerId,
            TargetNpcId,
            CreateHighDamageSkill(),
            CreateItemData(),
            weather: 0,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99,
            targetIndex: 0,
            clientTick: 100);

        Check(result == LegacySkillAttackResult.Accepted && outcome is not null,
            "The damage-cap skill attack did not resolve its NPC slot.");
        Check(outcome!.Damage == DamageCap && outcome.RemainingHp == TargetHp - DamageCap,
            $"The NPC skill damage was not capped at {DamageCap}: damage={outcome.Damage}, hp={outcome.RemainingHp}.");
    }

    private static WorldHub CreateWorld(byte targetClan, bool targetIsPlayer, int attackerDamage)
    {
        var hub = new WorldHub();
        var attackerMob = CreateMob("DAMAGE_CAP_ATTACKER", clan: 1, damage: attackerDamage, magic: 0);
        Check(hub.Enter(AttackerId, "DAMAGE_CAP_ATTACKER", (_, _) => ValueTask.CompletedTask),
            "The damage-cap attacker was not registered.");
        Check(hub.SetCharacterState(AttackerId, 0, 0, 1, 0, 0, 2_000, 2_000, attackerMob),
            "The damage-cap attacker state was not installed.");

        var targetMob = CreateMob("DAMAGE_CAP_TARGET", targetClan, damage: 0, magic: 0);
        if (targetIsPlayer)
        {
            Check(hub.Enter(TargetPlayerId, "DAMAGE_CAP_TARGET", (_, _) => ValueTask.CompletedTask),
                "The damage-cap player target was not registered.");
            Check(hub.SetCharacterState(TargetPlayerId, 0, 0, targetClan, 0, 0, 2_005, 2_000, targetMob),
                "The damage-cap player state was not installed.");
        }
        else
        {
            var npcId = hub.EnterNpc(targetMob, 2_005, 2_000, requestedConnectionId: TargetNpcId, generateIndex: 1);
            Check(npcId == TargetNpcId, "The damage-cap NPC target was not registered at its expected index.");
        }

        return hub;
    }

    private static LegacySkillDefinition CreateHighDamageSkill() => new(
        10, 69, 1, 0, 0, 1, 1, 8_000_000, 0, 0, 0, 0, 0,
        [], [], 0, 0, 1, 1, 0, 0, 0, 0, "ATTACK_DAMAGE_CAP_TEST");

    private static LegacyItemDataTable CreateItemData() =>
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    private static byte[] CreateMob(string name, byte clan, int damage, int magic)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).AsSpan(0, Math.Min(name.Length, 15)).CopyTo(mob);
        mob[LegacyAccountSnapshot.MobClanOffset] = clan;
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobMagicOffset), magic);
        var score = new LegacyScore(
            Level: 50,
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
            Special3: 0,
            Special4: 0);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

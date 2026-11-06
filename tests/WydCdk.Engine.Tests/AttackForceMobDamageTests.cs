using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackForceMobDamageTests
{
    private const int AttackerId = 1;
    private const int PlayerTargetId = 2;
    private const int NpcTargetId = 20_001;
    private const int TargetHp = 50_000_000;
    private const int Type30Damage = 75;

    public static void PhysicalNpcDamageIncludesActiveType30Levels()
    {
        var baseline = ResolvePhysicalNpcDamage(new byte[LegacyAccountSnapshot.AffectStride]);
        var boosted = ResolvePhysicalNpcDamage(CreateAttackerAffects());

        Check(boosted == baseline + Type30Damage,
            $"Physical damage to an NPC did not add all Type 30 levels: base={baseline}, boosted={boosted}.");
    }

    public static void NpcSkillDamageIncludesActiveType30Levels()
    {
        var baseline = ResolveNpcSkillDamage(new byte[LegacyAccountSnapshot.AffectStride]);
        var boosted = ResolveNpcSkillDamage(CreateAttackerAffects());

        Check(boosted == baseline + Type30Damage,
            $"Skill damage to an NPC did not add all Type 30 levels: base={baseline}, boosted={boosted}.");
    }

    public static void PlayerSkillDamageDoesNotIncludeType30Levels()
    {
        var baseline = ResolvePlayerSkillDamage(new byte[LegacyAccountSnapshot.AffectStride]);
        var boosted = ResolvePlayerSkillDamage(CreateAttackerAffects());

        Check(boosted == baseline,
            $"Type 30 damage was incorrectly added against a player: base={baseline}, boosted={boosted}.");
    }

    public static void ClanSixZeroDamageIsNotRaisedByType30()
    {
        var damage = ResolvePhysicalNpcDamage(CreateAttackerAffects(), targetClan: 6);

        Check(damage == 0,
            $"Type 30 damage was added after the Clan 6 gate had set damage to zero: damage={damage}.");
    }

    private static int ResolvePhysicalNpcDamage(byte[] attackerAffect, byte targetClan = 5)
    {
        var hub = CreateWorld(targetIsPlayer: false, attackerAffect, targetClan);
        var result = hub.TryApplyPhysicalAttack(
            AttackerId,
            NpcTargetId,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99,
            itemData: CreateItemData());

        Check(result == LegacyPhysicalAttackResult.Accepted && outcome is not null,
            "The Type 30 physical NPC attack did not resolve.");
        Check(outcome!.RemainingHp == TargetHp - outcome.Damage,
            "Physical NPC HP did not use the final damage including the Type 30 stage.");
        return outcome.Damage;
    }

    private static int ResolveNpcSkillDamage(byte[] attackerAffect)
    {
        var hub = CreateWorld(targetIsPlayer: false, attackerAffect, targetClan: 5);
        var result = hub.TryApplySkillAttack(
            AttackerId,
            NpcTargetId,
            CreateSkill(),
            CreateItemData(),
            weather: 0,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99,
            targetIndex: 0,
            clientTick: 100);

        Check(result == LegacySkillAttackResult.Accepted && outcome is not null,
            "The Type 30 NPC skill attack did not resolve.");
        Check(outcome!.RemainingHp == TargetHp - outcome.Damage,
            "NPC skill HP did not use the final damage including the Type 30 stage.");
        return outcome.Damage;
    }

    private static int ResolvePlayerSkillDamage(byte[] attackerAffect)
    {
        var hub = CreateWorld(targetIsPlayer: true, attackerAffect, targetClan: 5);
        var result = hub.TryApplySkillAttack(
            AttackerId,
            PlayerTargetId,
            CreateSkill(),
            CreateItemData(),
            weather: 0,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99,
            targetIndex: 0,
            clientTick: 100);

        Check(result == LegacySkillAttackResult.Accepted && outcome is not null,
            "The Type 30 player skill attack did not resolve.");
        return outcome!.Damage;
    }

    private static WorldHub CreateWorld(bool targetIsPlayer, byte[] attackerAffect, byte targetClan)
    {
        var hub = new WorldHub();
        var attackerMob = CreateMob("FORCE_MOB_ATTACKER", clan: 1, damage: 1_000);
        Check(hub.Enter(AttackerId, "FORCE_MOB_ATTACKER", (_, _) => ValueTask.CompletedTask),
            "The Type 30 attacker was not registered.");
        Check(hub.SetCharacterState(
                AttackerId,
                0,
                0,
                1,
                0,
                0,
                2_000,
                2_000,
                attackerMob,
                LegacyAccountSnapshot.ClassMasterMortal,
                attackerAffect),
            "The Type 30 attacker state was not installed.");

        var targetMob = CreateMob("FORCE_MOB_TARGET", targetClan, damage: 0);
        if (targetIsPlayer)
        {
            Check(hub.Enter(PlayerTargetId, "FORCE_MOB_TARGET", (_, _) => ValueTask.CompletedTask),
                "The Type 30 player target was not registered.");
            Check(hub.SetCharacterState(PlayerTargetId, 0, 0, targetClan, 0, 0, 2_005, 2_000, targetMob),
                "The Type 30 player target state was not installed.");
        }
        else
        {
            var npcId = hub.EnterNpc(targetMob, 2_005, 2_000, requestedConnectionId: NpcTargetId, generateIndex: 1);
            Check(npcId == NpcTargetId, "The Type 30 NPC target was not registered at its expected index.");
        }

        return hub;
    }

    private static LegacySkillDefinition CreateSkill() => new(
        10, 69, 1, 0, 0, 1, 1, 1_000, 0, 0, 0, 0, 0,
        [], [], 0, 0, 1, 1, 0, 0, 0, 0, "FORCE_MOB_DAMAGE_TEST");

    private static LegacyItemDataTable CreateItemData() =>
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    private static byte[] CreateAttackerAffects()
    {
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = 30;
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(2, sizeof(ushort)), 25);
        affect[8] = 12;
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(10, sizeof(ushort)), 900);
        affect[16] = 30;
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(18, sizeof(ushort)), 50);
        return affect;
    }

    private static byte[] CreateMob(string name, byte clan, int damage)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).AsSpan(0, Math.Min(name.Length, 15)).CopyTo(mob);
        mob[LegacyAccountSnapshot.MobClanOffset] = clan;
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

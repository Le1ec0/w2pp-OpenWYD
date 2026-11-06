using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackRvrSameClanGateTests
{
    private const int AttackerId = 1;
    private const int TargetPlayerId = 2;
    private const int TargetNpcId = 20_201;
    private const int TargetHp = 10_000;

    public static void PhysicalSameClanTargetInsideActiveRvrHasNoDamage()
    {
        var world = CreateWorld(targetClan: 5, targetIsPlayer: true, rvrWarActive: true,
            targetX: 1_679, targetY: 1_940);
        var result = world.TryApplyPhysicalAttack(
            AttackerId, TargetPlayerId, randomFactor: 99, out var outcome, attackMissRoll: 99);

        Check(result == LegacyPhysicalAttackResult.Accepted && outcome is
            { Damage: 0, RemainingHp: TargetHp, ClearTargetIdOnWire: true },
            $"Same-clan physical damage inside active RvR was not suppressed: result={result}, outcome={outcome}.");
    }

    public static void PlayerSkillSameClanTargetInsideActiveRvrHasNoDamage()
    {
        var world = CreateWorld(targetClan: 5, targetIsPlayer: true, rvrWarActive: true,
            targetX: 1_774, targetY: 2_023);
        var result = world.TryApplySkillAttack(
            AttackerId,
            TargetPlayerId,
            CreateDamageSkill(),
            CreateItemData(),
            weather: 0,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99,
            targetIndex: 0,
            clientTick: 100);

        Check(result == LegacySkillAttackResult.Accepted && outcome is
            { Damage: 0, RemainingHp: TargetHp, ClearTargetIdOnWire: true },
            $"Same-clan player skill damage inside active RvR was not suppressed: result={result}, outcome={outcome}.");
    }

    public static void PhysicalRvrGateContinuesThroughMountedTargetSplit()
    {
        var world = CreateWorld(targetClan: 5, targetIsPlayer: true, rvrWarActive: true,
            targetX: 1_700, targetY: 1_970, mountedTarget: true);
        var result = world.TryApplyPhysicalAttack(
            AttackerId, TargetPlayerId, randomFactor: 99, out var outcome, attackMissRoll: 99);

        Check(result == LegacyPhysicalAttackResult.Accepted && outcome is
            { Damage: 1, RemainingHp: TargetHp - 1, ClearTargetIdOnWire: true },
            $"The RvR gate did not preserve the source's later mounted-target split: result={result}, outcome={outcome}.");
    }

    public static void PhysicalRvrGateContinuesThroughManaControl()
    {
        var targetAffect = new byte[LegacyAccountSnapshot.AffectStride];
        targetAffect[0] = 18;
        var world = CreateWorld(targetClan: 5, targetIsPlayer: true, rvrWarActive: true,
            targetX: 1_700, targetY: 1_970, targetAffect: targetAffect);
        var result = world.TryApplyPhysicalAttack(
            AttackerId, TargetPlayerId, randomFactor: 99, out var outcome, attackMissRoll: 99);

        Check(result == LegacyPhysicalAttackResult.Accepted && outcome is
            { Damage: 0, RemainingHp: TargetHp, TargetResourceChanged: true, ClearTargetIdOnWire: true },
            $"The RvR gate did not continue through ManaControl's zero-damage update: result={result}, outcome={outcome}.");
        Check(LegacyMobCombatState.Read(outcome!.TargetMobSnapshot).CurrentMana == 1_000,
            "Zero-damage ManaControl changed target MP after the RvR gate.");
    }

    public static void GateRequiresActiveWarSameClanAndTargetPlayerInsideInclusiveArea()
    {
        Check(AttackPhysical(targetClan: 5, targetIsPlayer: true, rvrWarActive: false,
                targetX: 1_700, targetY: 1_970) is { Damage: > 0, ClearTargetIdOnWire: false },
            "The RvR same-clan gate ran while the war was inactive.");
        Check(AttackPhysical(targetClan: 5, targetIsPlayer: true, rvrWarActive: true,
                targetX: 1_678, targetY: 1_970) is { Damage: > 0, ClearTargetIdOnWire: false } &&
            AttackPhysical(targetClan: 5, targetIsPlayer: true, rvrWarActive: true,
                targetX: 1_775, targetY: 1_970) is { Damage: > 0, ClearTargetIdOnWire: false } &&
            AttackPhysical(targetClan: 5, targetIsPlayer: true, rvrWarActive: true,
                targetX: 1_700, targetY: 1_939) is { Damage: > 0, ClearTargetIdOnWire: false } &&
            AttackPhysical(targetClan: 5, targetIsPlayer: true, rvrWarActive: true,
                targetX: 1_700, targetY: 2_024) is { Damage: > 0, ClearTargetIdOnWire: false },
            "An adjacent coordinate was included in the RvR area.");
        Check(AttackPhysical(targetClan: 4, targetIsPlayer: true, rvrWarActive: true,
                targetX: 1_700, targetY: 1_970) is { Damage: > 0, ClearTargetIdOnWire: false },
            "The RvR same-clan gate blocked different Clans.");
        Check(AttackPhysical(targetClan: 5, targetIsPlayer: false, rvrWarActive: true,
                targetX: 1_700, targetY: 1_970) is { Damage: > 0, ClearTargetIdOnWire: false },
            "The RvR same-clan gate blocked a non-player target.");
        Check(AttackPhysical(targetClan: 5, targetIsPlayer: true, rvrWarActive: true,
                targetX: 1_774, targetY: 2_023) is { Damage: 0, ClearTargetIdOnWire: true },
            "The inclusive upper boundary of the RvR area was not blocked.");
    }

    private static LegacyPhysicalAttackOutcome? AttackPhysical(
        byte targetClan,
        bool targetIsPlayer,
        bool rvrWarActive,
        short targetX,
        short targetY)
    {
        var world = CreateWorld(targetClan, targetIsPlayer, rvrWarActive, targetX, targetY);
        var result = world.TryApplyPhysicalAttack(
            AttackerId,
            targetIsPlayer ? TargetPlayerId : TargetNpcId,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99);
        Check(result == LegacyPhysicalAttackResult.Accepted, $"The RvR physical slot was not accepted: {result}.");
        return outcome;
    }

    private static WorldHub CreateWorld(
        byte targetClan,
        bool targetIsPlayer,
        bool rvrWarActive,
        short targetX,
        short targetY,
        byte[]? targetAffect = null,
        bool mountedTarget = false)
    {
        var world = new WorldHub();
        var attackerMob = CreateMob("RVR_CLAN_ATTACKER", clan: 5, guild: 7, damage: 2_000);
        Check(world.Enter(AttackerId, "RVR_CLAN_ATTACKER", (_, _) => ValueTask.CompletedTask),
            "The RvR same-clan attacker was not registered.");
        Check(world.SetCharacterState(AttackerId, 0, guildId: 7, clan: 5, guildLevel: 0, coin: 0,
                positionX: (short)(targetX - 5), positionY: (short)(targetY - 5), mob: attackerMob),
            "The RvR same-clan attacker state was not installed.");

        var targetGuild = targetIsPlayer ? 8 : 0;
        var targetMob = CreateMob("RVR_CLAN_TARGET", targetClan, targetGuild, damage: 0);
        if (mountedTarget)
        {
            var mountOffset = LegacyAccountSnapshot.MobEquipmentOffset + (14 * LegacyItem.SizeInBytes);
            new LegacyItem(2_360, 0, 0, 0, 0, 4, 0).Write(targetMob.AsSpan(mountOffset));
            BinaryPrimitives.WriteInt16LittleEndian(targetMob.AsSpan(mountOffset + 2), 100);
        }

        if (targetIsPlayer)
        {
            Check(world.Enter(TargetPlayerId, "RVR_CLAN_TARGET", (_, _) => ValueTask.CompletedTask),
                "The RvR same-clan player target was not registered.");
            Check(world.SetCharacterState(TargetPlayerId, 0, guildId: targetGuild, clan: targetClan,
                    guildLevel: 0, coin: 0, positionX: targetX, positionY: targetY, mob: targetMob,
                    LegacyAccountSnapshot.ClassMasterMortal,
                    targetAffect ?? new byte[LegacyAccountSnapshot.AffectStride]),
                "The RvR same-clan player target state was not installed.");
        }
        else
        {
            var registeredNpc = world.EnterNpc(targetMob, targetX, targetY,
                requestedConnectionId: TargetNpcId, generateIndex: 1);
            Check(registeredNpc == TargetNpcId, "The RvR same-clan NPC target was not registered.");
        }

        world.SetCombatWorldState(new LegacyCombatWorldState(RvrWarActive: rvrWarActive));
        return world;
    }

    private static byte[] CreateMob(string name, byte clan, int guild, int damage)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).AsSpan(0, Math.Min(name.Length, 15)).CopyTo(mob);
        mob[LegacyAccountSnapshot.MobClanOffset] = clan;
        BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset), checked((ushort)guild));
        var score = new LegacyScore(50, 0, damage, 0, 0, 0, 0, TargetHp, 1_000, TargetHp, 1_000, 100, 100, 0, 0, 0, 0, 0, 0);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static LegacySkillDefinition CreateDamageSkill() => new(
        10, 69, 1, 0, 0, 1, 1, 40, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0,
        "RVR_SAME_CLAN_DAMAGE");

    private static LegacyItemDataTable CreateItemData() =>
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

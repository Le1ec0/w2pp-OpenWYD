using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPhysicalNpcSecondaryAffectTests
{
    public static void Run()
    {
        RsvFrostProjectsType1OnNpc1000AndRestoresItsBaseline();
        RsvFrostHonorsSetAffectNpcBoundAndRsvBlock();
        RsvDrainUsesSetTickRulesForNpcTargets();
    }

    private static void RsvFrostProjectsType1OnNpc1000AndRestoresItsBaseline()
    {
        var fixture = CreateFixture(1_000, attackerRsv: 0x01);
        var result = Apply(fixture, frostRoll: 0, drainRoll: 1, out var outcome);
        Check(result == LegacyPhysicalAttackResult.Accepted && outcome is
              { Damage: > 0, TargetAffectSnapshot: { } affected, TargetAffectViewRecipients: var recipients } &&
              HasAffectType(affected, 1) && recipients.SequenceEqual([1]),
            "The frost physical hit did not resolve against NPC 1000.");
        Check(fixture.World.TryGetNpcSnapshot(fixture.NpcId, out var npc) && npc is not null &&
              HasAffectType(npc.AffectSnapshot, 1) &&
              BinaryPrimitives.ReadUInt32LittleEndian(npc.AffectSnapshot.AsSpan(4)) == 1 &&
              LegacyScore.Read(npc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x64, Intelligence: 60 } &&
              npc.HolyTouchBaseScore is { AttackRun: 0x96, Intelligence: 100 },
            "RSV_FROST did not project Type 1 and retain the NPC's pre-affect score baseline.");

        Check(Apply(fixture, frostRoll: 0, drainRoll: 1, out _) == LegacyPhysicalAttackResult.Accepted &&
              fixture.World.TryGetNpcSnapshot(fixture.NpcId, out npc) && npc is not null &&
              BinaryPrimitives.ReadUInt32LittleEndian(npc.AffectSnapshot.AsSpan(4)) == 4,
            "Reapplying RSV_FROST Type 1 did not reset its duration to four ticks.");
        for (var tick = 0; tick < 3; tick++)
            _ = fixture.World.TryProcessNonGeneratedNpcTimerAffects(fixture.NpcId, out _);
        Check(fixture.World.TryProcessNonGeneratedNpcTimerAffects(fixture.NpcId, out _),
            "The RSV_FROST Type 1 affect did not expire on the ordinary NPC timer.");
        Check(fixture.World.TryGetNpcSnapshot(fixture.NpcId, out npc) && npc is not null &&
              !HasAffectType(npc.AffectSnapshot, 1) &&
              LegacyScore.Read(npc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x96, Intelligence: 100 } &&
              npc.HolyTouchBaseScore is null,
            "Expiring the RSV_FROST Type 1 affect did not restore the original NPC score.");
    }

    private static void RsvFrostHonorsSetAffectNpcBoundAndRsvBlock()
    {
        var beyondBound = CreateFixture(1_001, attackerRsv: 0x01);
        Check(Apply(beyondBound, frostRoll: 0, drainRoll: 1, out _) == LegacyPhysicalAttackResult.Accepted &&
              beyondBound.World.TryGetNpcSnapshot(beyondBound.NpcId, out var laterNpc) && laterNpc is not null &&
              !HasAffectType(laterNpc.AffectSnapshot, 1),
            "RSV_FROST crossed SetAffect's NPC 1000 bound.");

        var blocked = CreateFixture(1_000, attackerRsv: 0x01, targetRsv: 0x80);
        Check(Apply(blocked, frostRoll: 0, drainRoll: 1, out _) == LegacyPhysicalAttackResult.Accepted &&
              blocked.World.TryGetNpcSnapshot(blocked.NpcId, out var blockedNpc) && blockedNpc is not null &&
              !HasAffectType(blockedNpc.AffectSnapshot, 1),
            "RSV_FROST ignored SetAffect's RSV_BLOCK gate.");
    }

    private static void RsvDrainUsesSetTickRulesForNpcTargets()
    {
        var laterNpc = CreateFixture(1_001, attackerRsv: 0x02);
        Check(Apply(laterNpc, frostRoll: 1, drainRoll: 0, out _) == LegacyPhysicalAttackResult.Accepted &&
              laterNpc.World.TryGetNpcSnapshot(laterNpc.NpcId, out var affectedNpc) && affectedNpc is not null &&
              HasAffectType(affectedNpc.AffectSnapshot, 20),
            "RSV_DRAIN did not apply Type 20 through SetTick on NPC 1001.");

        var blocked = CreateFixture(1_001, attackerRsv: 0x02, targetRsv: 0x80);
        Check(Apply(blocked, frostRoll: 1, drainRoll: 0, out _) == LegacyPhysicalAttackResult.Accepted &&
              blocked.World.TryGetNpcSnapshot(blocked.NpcId, out var blockedNpc) && blockedNpc is not null &&
              !HasAffectType(blockedNpc.AffectSnapshot, 20),
            "RSV_DRAIN ignored SetTick's RSV_BLOCK gate.");

        var merchantAboveBound = CreateFixture(1_001, attackerRsv: 0x02, targetMerchant: 1);
        Check(Apply(merchantAboveBound, frostRoll: 1, drainRoll: 0, out _) == LegacyPhysicalAttackResult.Accepted &&
              merchantAboveBound.World.TryGetNpcSnapshot(merchantAboveBound.NpcId, out var merchantNpc) && merchantNpc is not null &&
              !HasAffectType(merchantNpc.AffectSnapshot, 20),
            "RSV_DRAIN ignored SetTick's merchant-NPC gate above MAX_USER.");
    }

    private static (WorldHub World, int NpcId) CreateFixture(
        int npcId,
        ushort attackerRsv,
        ushort targetRsv = 0,
        byte targetMerchant = 0)
    {
        using var skillReader = new StringReader(
            "36,72,1,35,5,6,3,200,0,0,1,2,1,6.0.0.7.0.0.0.0,6.0.0.7.0.0.0.0,2,0,1,1,0,2,0,1,Nevasca\n" +
            "40,24,1,9,2,6,4,20,20,10,0,0,1,10.0.0.9.0.0.0.0,10.0.0.9.0.0.0.0,4,0,1,1,0,0,0,1,Neblina_Venenosa");
        var world = new WorldHub(skillData: LegacySkillDataTable.Load(skillReader));

        var attackerMob = CreateMob(new LegacyScore(
            1_500, 0, 100, 0, 0x96, 0, 0,
            1_000, 1_000, 1_000, 1_000,
            0, 100, 0, 0, 0, 40, 0, 0));
        BinaryPrimitives.WriteUInt16LittleEndian(
            attackerMob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), attackerRsv);
        Check(world.Enter(1, "RSV_SECONDARY_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(1, 0, 0, 1, 0, 0, 2_000, 2_000, attackerMob),
            "The RSV secondary-effect attacker fixture was not installed.");

        var targetMob = CreateMob(new LegacyScore(
            50, 10, 0, 0, 0x96, 0, 0,
            1_000_000, 1_000, 1_000_000, 1_000,
            0, 100, 0, 0, 0, 0, 0, 0));
        BinaryPrimitives.WriteUInt16LittleEndian(
            targetMob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), targetRsv);
        targetMob[LegacyAccountSnapshot.MobMerchantOffset] = targetMerchant;
        new LegacyItem(51, 0, 0, 0, 0, 0, 0)
            .Write(targetMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        var createdNpcId = world.EnterNpc(
            targetMob,
            2_005,
            2_005,
            new byte[LegacyAccountSnapshot.AffectStride],
            requestedConnectionId: npcId);
        Check(createdNpcId == npcId, $"NPC fixture {npcId} could not be registered.");
        return (world, npcId);
    }

    private static LegacyPhysicalAttackResult Apply(
        (WorldHub World, int NpcId) fixture,
        int frostRoll,
        int drainRoll,
        out LegacyPhysicalAttackOutcome? outcome) =>
        fixture.World.TryApplyPhysicalAttack(
            1,
            fixture.NpcId,
            randomFactor: 99,
            out outcome,
            attackMissRoll: 99,
            frostRoll: frostRoll,
            drainRoll: drainRoll);

    private static byte[] CreateMob(LegacyScore score)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static bool HasAffectType(ReadOnlySpan<byte> affect, byte type)
    {
        for (var offset = 0; offset < affect.Length; offset += 8)
            if (affect[offset] == type)
                return true;
        return false;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

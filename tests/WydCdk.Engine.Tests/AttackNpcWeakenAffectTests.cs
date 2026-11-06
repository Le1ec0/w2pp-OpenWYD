using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackNpcWeakenAffectTests
{
    public static void Run()
    {
        EnfraquecerAppliesType10OnlyAtFirstLegacyNpcIndex();
        EnfraquecerHonorsSourceNpcAffectGates();
        NonGeneratedNpcType10ExpiryRestoresCurrentDamage();
    }

    private static void EnfraquecerHonorsSourceNpcAffectGates()
    {
        var sameGuildMob = CreateMob(83);
        BinaryPrimitives.WriteUInt16LittleEndian(sameGuildMob.AsSpan(LegacyAccountSnapshot.MobGuildOffset), 12);
        AssertNotApplied(ApplyEnfraquecer(
            LegacyMobAiPlanner.MaximumPlayerId, npcMob: sameGuildMob, attackerGuildId: 12), "same guild");

        AssertNotApplied(ApplyEnfraquecer(
            LegacyMobAiPlanner.MaximumPlayerId, npcLeaderConnectionId: 1), "same leader");

        var clanSixMob = CreateMob(83);
        clanSixMob[LegacyAccountSnapshot.MobClanOffset] = 6;
        AssertNotApplied(ApplyEnfraquecer(
            LegacyMobAiPlanner.MaximumPlayerId, npcMob: clanSixMob), "Clan 6");

        var merchantMob = CreateMob(83);
        merchantMob[LegacyAccountSnapshot.MobMerchantOffset] = 1;
        AssertNotApplied(ApplyEnfraquecer(
            LegacyMobAiPlanner.MaximumPlayerId, npcMob: merchantMob), "merchant");

        var blockedMob = CreateMob(83);
        BinaryPrimitives.WriteUInt16LittleEndian(blockedMob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), 0x0080);
        AssertNotApplied(ApplyEnfraquecer(
            LegacyMobAiPlanner.MaximumPlayerId, npcMob: blockedMob), "RSV_BLOCK");
    }

    private static void EnfraquecerAppliesType10OnlyAtFirstLegacyNpcIndex()
    {
        var first = ApplyEnfraquecer(LegacyMobAiPlanner.MaximumPlayerId);
        Check(first.Result == LegacySkillAttackResult.Accepted,
            "Enfraquecer rejected a valid first-NPC target.");
        Check(first.World.TryGetNpcSnapshot(first.NpcId, out var firstNpc) && firstNpc is not null &&
                HasAffectType(firstNpc.AffectSnapshot, 10),
            "Enfraquecer did not apply Type 10 at the first legacy NPC index.");
        var initialDamage = LegacyMobCombatState.Read(first.InitialNpcMob).CurrentScore.Damage;
        var reducedDamage = LegacyMobCombatState.Read(firstNpc!.MobSnapshot).CurrentScore.Damage;
        var reduction = LegacyCurrentScoreMath.CalculateWeakenAffectStage(firstNpc.AffectSnapshot).DamageReduction;
        Check(reduction > 0 && reducedDamage == initialDamage - reduction,
            $"Enfraquecer did not project Type 10 damage reduction into NPC CurrentScore; initial={initialDamage}, current={reducedDamage}, reduction={reduction}.");
        Check(GetAffectTime(firstNpc.AffectSnapshot, 10) == 2,
            "The first Enfraquecer cast did not use its normal source duration before a Type 10 refresh.");
        Check(first.World.TryApplySkillAttack(
                  1,
                  first.NpcId,
                  Enfraquecer(),
                  itemData: null,
                  weather: 0,
                  randomFactor: -1,
                  out var refreshedOutcome,
                  affectRandomFactor: 0) == LegacySkillAttackResult.Accepted &&
              refreshedOutcome?.TargetAffectSnapshot is { } refreshedAffect &&
              GetAffectTime(refreshedAffect, 10) == 4,
            "Reapplying Enfraquecer did not reset an existing Type 10 affect to four legacy timer ticks.");

        var later = ApplyEnfraquecer(LegacyMobAiPlanner.MaximumPlayerId + 1);
        Check(later.Result == LegacySkillAttackResult.Accepted &&
                later.World.TryGetNpcSnapshot(later.NpcId, out var laterNpc) && laterNpc is not null &&
                !HasAffectType(laterNpc.AffectSnapshot, 10),
            "Enfraquecer applied Type 10 above the legacy SetAffect connection limit.");
    }

    private static void NonGeneratedNpcType10ExpiryRestoresCurrentDamage()
    {
        const int npcId = 20_048;
        const int weakenedDamage = 70;
        const int originalDamage = 83;
        var mob = CreateMob(weakenedDamage);
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = 10;
        affect[1] = 8;
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(2), 25);
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), 1);
        var world = CreateWorldWithNpc(npcId, mob, affect, out _);

        Check(world.TryProcessNonGeneratedNpcTimerAffects(npcId, out _),
            "The non-generated NPC timer ignored a finite Type 10 affect.");
        Check(world.TryGetNpcSnapshot(npcId, out var npc) && npc is not null &&
                npc.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]),
            "Type 10 expiry did not clear its complete eight-byte affect slot.");
        Check(LegacyMobCombatState.Read(npc!.MobSnapshot).CurrentScore.Damage == originalDamage,
            "Type 10 expiry did not restore the NPC's pre-affect CurrentScore damage.");

        const int generatedNpcId = LegacyMobAiPlanner.MaximumPlayerId;
        var generatedAffect = affect.ToArray();
        var generatedWorld = CreateWorldWithNpc(
            generatedNpcId,
            CreateMob(weakenedDamage),
            generatedAffect,
            out _,
            generateIndex: 0);
        Check(generatedWorld.TryProcessGeneratedNpcTimerAffects(generatedNpcId, out _),
            "The generated NPC timer ignored a finite Type 10 affect.");
        Check(generatedWorld.TryGetNpcSnapshot(generatedNpcId, out var generatedNpc) && generatedNpc is not null &&
                generatedNpc.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
                LegacyMobCombatState.Read(generatedNpc.MobSnapshot).CurrentScore.Damage == originalDamage,
            "Generated NPC Type 10 expiry did not clear the affect and restore CurrentScore damage.");
    }

    private static (WorldHub World, int NpcId, LegacySkillAttackResult Result, byte[] InitialNpcMob)
        ApplyEnfraquecer(
            int npcId,
            byte[]? npcMob = null,
            int attackerGuildId = 0,
            int npcLeaderConnectionId = 0)
    {
        var world = new WorldHub();
        var attackerMob = CreateMob(100);
        npcMob ??= CreateMob(83);
        Check(world.Enter(1, "WEAKEN_NPC_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
                world.SetCharacterState(1, 0, attackerGuildId, 1, 0, 0, 2_000, 2_000, attackerMob),
            "Enfraquecer attacker fixture was not installed.");
        var createdNpcId = world.EnterNpc(
            npcMob,
            2_005,
            2_005,
            new byte[LegacyAccountSnapshot.AffectStride],
            requestedConnectionId: npcId,
            leaderConnectionId: npcLeaderConnectionId);
        Check(createdNpcId == npcId, $"Enfraquecer NPC fixture {npcId} could not be registered.");

        var result = world.TryApplySkillAttack(
            1,
            npcId,
            Enfraquecer(),
            itemData: null,
            weather: 0,
            randomFactor: -1,
            out _,
            affectRandomFactor: 0);
        return (world, npcId, result, npcMob);
    }

    private static void AssertNotApplied(
        (WorldHub World, int NpcId, LegacySkillAttackResult Result, byte[] InitialNpcMob) attempt,
        string scenario)
    {
        Check(attempt.Result == LegacySkillAttackResult.Accepted,
            $"Enfraquecer rejected its {scenario} source-gate fixture.");
        Check(attempt.World.TryGetNpcSnapshot(attempt.NpcId, out var npc) && npc is not null &&
                !HasAffectType(npc.AffectSnapshot, 10) &&
                LegacyMobCombatState.Read(npc.MobSnapshot).CurrentScore.Damage ==
                LegacyMobCombatState.Read(attempt.InitialNpcMob).CurrentScore.Damage,
            $"Enfraquecer changed an NPC behind the {scenario} source gate.");
    }

    private static WorldHub CreateWorldWithNpc(
        int npcId,
        byte[] mob,
        byte[] affect,
        out int createdNpcId,
        int generateIndex = -1)
    {
        var world = new WorldHub();
        createdNpcId = world.EnterNpc(
            mob,
            2_005,
            2_005,
            affect,
            requestedConnectionId: npcId,
            generateIndex: generateIndex);
        Check(createdNpcId == npcId, $"Type 10 timer NPC fixture {npcId} could not be registered.");
        return world;
    }

    private static byte[] CreateMob(int damage)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(50, 0, damage, 0, 0, 0, 0, 1_000, 1_000, 1_000, 1_000,
            0, 0, 1_000, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static bool HasAffectType(ReadOnlySpan<byte> affect, byte type)
    {
        for (var offset = 0; offset < affect.Length; offset += 8)
            if (affect[offset] == type)
                return true;
        return false;
    }

    private static uint GetAffectTime(ReadOnlySpan<byte> affect, byte type)
    {
        for (var offset = 0; offset + 8 <= affect.Length; offset += 8)
            if (affect[offset] == type)
                return BinaryPrimitives.ReadUInt32LittleEndian(affect.Slice(offset + 4, sizeof(uint)));
        return 0;
    }

    private static LegacySkillDefinition Enfraquecer() =>
        new(51, 45, 3, 35, 2, 6, 0, 0, 0, 0, 10, 10, 1,
            [6, 0, 0, 8, 0, 0, 0, 0], [10, 0, 0, 8, 0, 0, 0, 0],
            0, 0, 1, 13, 0, 0, 0, 0, "Enfraquecer");

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

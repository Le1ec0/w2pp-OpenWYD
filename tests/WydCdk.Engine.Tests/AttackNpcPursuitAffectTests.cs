using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackNpcPursuitAffectTests
{
    public static void Run()
    {
        PursuicaoChangesAllFourResistancesOnlyAtTheFirstNpcIndex();
        PursuicaoHonorsTheSourceAffectResistRollAndTargetGates();
        PursuicaoExpiryRestoresGeneratedAndNonGeneratedNpcResistances();
        ResiDecreaseProjectsTickType3ForNpcTargetsAndRestoresItOnExpiry();
        ResiDecreaseHonorsAggressiveNpcGates();
    }

    private static void ResiDecreaseProjectsTickType3ForNpcTargetsAndRestoresItOnExpiry()
    {
        const int npcId = LegacyMobAiPlanner.MaximumPlayerId + 1;
        var attempt = ApplyResiDecrease(npcId);
        var npcFound = attempt.World.TryGetNpcSnapshot(npcId, out var npc);
        Check(attempt.Result == LegacySkillAttackResult.Accepted && npcFound && npc is not null &&
              HasAffectType(npc.AffectSnapshot, 3),
            "Resi_Decrease did not apply its Type 3 tick to the ordinary NPC slot.");
        Check(npc!.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4)
                  .SequenceEqual(new byte[] { 40, 50, 60, 0 }),
            "Resi_Decrease did not project its Type 3 resistance reduction immediately on the NPC.");
        Check(GetAffectTime(npc.AffectSnapshot, 3) == 2,
            "Resi_Decrease did not use the two-tick SetTick cap for Type 3.");

        _ = attempt.World.TryProcessNonGeneratedNpcTimerAffects(npcId, out _);
        _ = attempt.World.TryProcessNonGeneratedNpcTimerAffects(npcId, out _);
        Check(attempt.World.TryGetNpcSnapshot(npcId, out var expiredNpc) && expiredNpc is not null &&
              !HasAffectType(expiredNpc.AffectSnapshot, 3) &&
              expiredNpc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4)
                  .SequenceEqual(new byte[] { 80, 90, 100, 40 }),
            "Resi_Decrease NPC expiry did not clear Type 3 and restore all four base resistances.");
    }

    private static void ResiDecreaseHonorsAggressiveNpcGates()
    {
        AssertResiDecreaseNotApplied(ApplyResiDecrease(
            LegacyMobAiPlanner.MaximumPlayerId + 1, affectRandomFactor: 27), "AffectResist");

        var sameGuildMob = CreateMob(83, level: 50, resistances: [80, 90, 100, 40]);
        BinaryPrimitives.WriteUInt16LittleEndian(
            sameGuildMob.AsSpan(LegacyAccountSnapshot.MobGuildOffset), 12);
        AssertResiDecreaseNotApplied(ApplyResiDecrease(
            LegacyMobAiPlanner.MaximumPlayerId + 1, npcMob: sameGuildMob, attackerGuildId: 12), "same guild");

        AssertResiDecreaseNotApplied(ApplyResiDecrease(
            LegacyMobAiPlanner.MaximumPlayerId + 1, npcLeaderConnectionId: 1), "same leader");

        var clanSixMob = CreateMob(83, level: 50, resistances: [80, 90, 100, 40]);
        clanSixMob[LegacyAccountSnapshot.MobClanOffset] = 6;
        AssertResiDecreaseNotApplied(ApplyResiDecrease(
            LegacyMobAiPlanner.MaximumPlayerId + 1, npcMob: clanSixMob), "Clan 6");

        var blockedMob = CreateMob(83, level: 50, resistances: [80, 90, 100, 40]);
        BinaryPrimitives.WriteUInt16LittleEndian(blockedMob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), 0x0080);
        AssertResiDecreaseNotApplied(ApplyResiDecrease(
            LegacyMobAiPlanner.MaximumPlayerId + 1, npcMob: blockedMob), "RSV_BLOCK");
    }

    private static void PursuicaoChangesAllFourResistancesOnlyAtTheFirstNpcIndex()
    {
        var attempt = ApplyPursuicao(LegacyMobAiPlanner.MaximumPlayerId);
        Check(attempt.Result == LegacySkillAttackResult.Accepted,
            "Perseguição rejected a valid first-NPC target.");
        Check(attempt.World.TryGetNpcSnapshot(attempt.NpcId, out var npc) && npc is not null &&
              HasAffectType(npc.AffectSnapshot, 3),
            "Perseguição did not apply Type 3 at the first legacy NPC index.");
        Check(npc!.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4)
                  .SequenceEqual(new byte[] { 30, 40, 50, 0 }),
            "Perseguição did not subtract Value - 10 from all four NPC resistances when head index is 50.");
        Check(GetAffectTime(npc.AffectSnapshot, 3) == 1,
            "The first Perseguição cast did not use its normal source duration.");

        var lowHead = ApplyPursuicao(
            LegacyMobAiPlanner.MaximumPlayerId,
            npcMob: CreateMob(83, level: 50, resistances: [80, 90, 100, 40], headIndex: 49));
        Check(lowHead.World.TryGetNpcSnapshot(lowHead.NpcId, out var lowHeadNpc) && lowHeadNpc is not null &&
              lowHeadNpc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4)
                  .SequenceEqual(new byte[] { 50, 60, 70, 10 }),
            "Perseguição did not halve Value for a head item index below 50.");

        Check(attempt.World.TryApplySkillAttack(
                  1,
                  attempt.NpcId,
                  Pursuicao(),
                  itemData: null,
                  weather: 0,
                  randomFactor: -1,
                  out var refreshedOutcome,
                  affectRandomFactor: 27) == LegacySkillAttackResult.Accepted &&
              refreshedOutcome?.TargetAffectSnapshot is { } refreshedAffect &&
              GetAffectTime(refreshedAffect, 3) == 4,
            "Reapplying Perseguição did not reset Type 3 to four legacy timer ticks.");

        var later = ApplyPursuicao(LegacyMobAiPlanner.MaximumPlayerId + 1);
        Check(later.Result == LegacySkillAttackResult.Accepted &&
              later.World.TryGetNpcSnapshot(later.NpcId, out var laterNpc) && laterNpc is not null &&
              !HasAffectType(laterNpc.AffectSnapshot, 3) &&
              laterNpc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4)
                  .SequenceEqual(new byte[] { 80, 90, 100, 40 }),
            "Perseguição applied its SetAffect fallback above the legacy first NPC index.");
    }

    private static void PursuicaoHonorsTheSourceAffectResistRollAndTargetGates()
    {
        var resisted = ApplyPursuicao(LegacyMobAiPlanner.MaximumPlayerId, affectRandomFactor: 28);
        AssertNotApplied(resisted, "AffectResist roll above the source threshold");

        var sameGuildMob = CreateMob(83, level: 50, resistances: [80, 90, 100, 40]);
        BinaryPrimitives.WriteUInt16LittleEndian(
            sameGuildMob.AsSpan(LegacyAccountSnapshot.MobGuildOffset), 12);
        AssertNotApplied(ApplyPursuicao(
            LegacyMobAiPlanner.MaximumPlayerId,
            npcMob: sameGuildMob,
            attackerGuildId: 12), "same guild");

        AssertNotApplied(ApplyPursuicao(
            LegacyMobAiPlanner.MaximumPlayerId,
            npcLeaderConnectionId: 1), "same leader");

        var clanSixMob = CreateMob(83, level: 50, resistances: [80, 90, 100, 40]);
        clanSixMob[LegacyAccountSnapshot.MobClanOffset] = 6;
        AssertNotApplied(ApplyPursuicao(
            LegacyMobAiPlanner.MaximumPlayerId,
            npcMob: clanSixMob), "Clan 6");

        var merchantMob = CreateMob(83, level: 50, resistances: [80, 90, 100, 40]);
        merchantMob[LegacyAccountSnapshot.MobMerchantOffset] = 1;
        AssertNotApplied(ApplyPursuicao(
            LegacyMobAiPlanner.MaximumPlayerId,
            npcMob: merchantMob), "merchant");

        var blockedMob = CreateMob(83, level: 50, resistances: [80, 90, 100, 40]);
        BinaryPrimitives.WriteUInt16LittleEndian(blockedMob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), 0x0080);
        AssertNotApplied(ApplyPursuicao(
            LegacyMobAiPlanner.MaximumPlayerId,
            npcMob: blockedMob), "RSV_BLOCK");
    }

    private static void PursuicaoExpiryRestoresGeneratedAndNonGeneratedNpcResistances()
    {
        const int npcId = LegacyMobAiPlanner.MaximumPlayerId;
        var nonGenerated = ApplyPursuicao(npcId);
        Check(nonGenerated.Result == LegacySkillAttackResult.Accepted &&
              nonGenerated.World.TryProcessNonGeneratedNpcTimerAffects(npcId, out _),
            "The non-generated NPC did not process its one-tick Type 3 expiry.");
        Check(nonGenerated.World.TryGetNpcSnapshot(npcId, out var nonGeneratedNpc) &&
              nonGeneratedNpc is not null &&
              nonGeneratedNpc.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              nonGeneratedNpc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4)
                  .SequenceEqual(new byte[] { 80, 90, 100, 40 }),
            "Non-generated NPC Type 3 expiry did not clear the affect and restore all four resistances.");

        var generated = ApplyPursuicao(npcId, generateIndex: 0);
        Check(generated.Result == LegacySkillAttackResult.Accepted &&
              generated.World.TryProcessGeneratedNpcTimerAffects(npcId, out _),
            "The generated NPC did not process its one-tick Type 3 expiry.");
        Check(generated.World.TryGetNpcSnapshot(npcId, out var generatedNpc) &&
              generatedNpc is not null &&
              generatedNpc.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              generatedNpc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4)
                  .SequenceEqual(new byte[] { 80, 90, 100, 40 }),
            "Generated NPC Type 3 expiry did not clear the affect and restore all four resistances.");
    }

    private static (WorldHub World, int NpcId, LegacySkillAttackResult Result, byte[] InitialNpcMob)
        ApplyPursuicao(
            int npcId,
            int affectRandomFactor = 27,
            byte[]? npcMob = null,
            int attackerGuildId = 0,
            int npcLeaderConnectionId = 0,
            int generateIndex = -1)
    {
        var world = new WorldHub();
        var attackerMob = CreateMob(100, level: 500);
        npcMob ??= CreateMob(83, level: 50, resistances: [80, 90, 100, 40], headIndex: 50);
        Check(world.Enter(1, "PURSUIT_NPC_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(1, 0, attackerGuildId, 1, 0, 0, 2_000, 2_000, attackerMob),
            "Perseguição attacker fixture was not installed.");
        var createdNpcId = world.EnterNpc(
            npcMob,
            2_005,
            2_005,
            CreateEmptyAffectSnapshot(),
            requestedConnectionId: npcId,
            generateIndex: generateIndex,
            leaderConnectionId: npcLeaderConnectionId);
        Check(createdNpcId == npcId, $"Perseguição NPC fixture {npcId} could not be registered.");

        var result = world.TryApplySkillAttack(
            1,
            npcId,
            Pursuicao(),
            itemData: null,
            weather: 0,
            randomFactor: -1,
            out _,
            affectRandomFactor: affectRandomFactor);
        return (world, npcId, result, npcMob);
    }

    private static (WorldHub World, int NpcId, LegacySkillAttackResult Result, byte[] InitialNpcMob)
        ApplyResiDecrease(
            int npcId,
            int affectRandomFactor = 26,
            byte[]? npcMob = null,
            int attackerGuildId = 0,
            int npcLeaderConnectionId = 0)
    {
        var world = new WorldHub();
        var attackerMob = CreateMob(100, level: 500);
        npcMob ??= CreateMob(83, level: 50, resistances: [80, 90, 100, 40], headIndex: 50);
        Check(world.Enter(1, "RESI_DECREASE_NPC_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(1, 0, attackerGuildId, 1, 0, 0, 2_000, 2_000, attackerMob),
            "Resi_Decrease NPC attacker fixture was not installed.");
        var createdNpcId = world.EnterNpc(
            npcMob,
            2_005,
            2_005,
            CreateEmptyAffectSnapshot(),
            requestedConnectionId: npcId,
            leaderConnectionId: npcLeaderConnectionId);
        Check(createdNpcId == npcId, $"Resi_Decrease NPC fixture {npcId} could not be registered.");

        var result = world.TryApplySkillAttack(
            1,
            npcId,
            ResiDecrease(),
            itemData: null,
            weather: 0,
            randomFactor: -1,
            out _,
            affectRandomFactor: affectRandomFactor);
        return (world, npcId, result, npcMob);
    }

    private static void AssertResiDecreaseNotApplied(
        (WorldHub World, int NpcId, LegacySkillAttackResult Result, byte[] InitialNpcMob) attempt,
        string scenario)
    {
        Check(attempt.Result == LegacySkillAttackResult.Accepted,
            $"Resi_Decrease rejected its {scenario} fixture.");
        Check(attempt.World.TryGetNpcSnapshot(attempt.NpcId, out var npc) && npc is not null &&
              !HasAffectType(npc.AffectSnapshot, 3) &&
              npc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4)
                  .SequenceEqual(attempt.InitialNpcMob.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4)),
            $"Resi_Decrease changed an NPC behind the {scenario} gate.");
    }

    private static void AssertNotApplied(
        (WorldHub World, int NpcId, LegacySkillAttackResult Result, byte[] InitialNpcMob) attempt,
        string scenario)
    {
        Check(attempt.Result == LegacySkillAttackResult.Accepted,
            $"Perseguição rejected its {scenario} fixture.");
        Check(attempt.World.TryGetNpcSnapshot(attempt.NpcId, out var npc) && npc is not null &&
              !HasAffectType(npc.AffectSnapshot, 3) &&
              npc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4)
                  .SequenceEqual(attempt.InitialNpcMob.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4)),
            $"Perseguição changed an NPC behind the {scenario} gate.");
    }

    private static byte[] CreateMob(int damage, int level, byte[]? resistances = null, int headIndex = 0)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(level, 0, damage, 0, 0, 0, 0, 1_000, 1_000, 1_000, 1_000,
            0, 0, 1_000, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        if (resistances is not null)
            resistances.CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4));
        BinaryPrimitives.WriteUInt16LittleEndian(
            mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset), checked((ushort)headIndex));
        return mob;
    }

    private static byte[] CreateEmptyAffectSnapshot() => new byte[LegacyAccountSnapshot.AffectStride];

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

    private static LegacySkillDefinition Pursuicao() =>
        new(16, 24, 3, 25, 2, 5, 0, 0, 0, 0, 3, 60, 0,
            [10, 0, 0, 0, 0, 0, 0, 0], [10, 0, 0, 0, 0, 0, 0, 0],
            2, 0, 1, 13, 0, 2, 0, 0, "Perseguição");

    private static LegacySkillDefinition ResiDecrease() =>
        new(226, 350, 1, 300, 300, 6, 0, 0, 3, 50, 0, 0, 1,
            [24, 0, 0, 24, 0, 0, 0, 0], [24, 0, 0, 24, 0, 0, 0, 0],
            0, 0, 1, 1, 0, 1, 0, 0, "Resi_Decrease");

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

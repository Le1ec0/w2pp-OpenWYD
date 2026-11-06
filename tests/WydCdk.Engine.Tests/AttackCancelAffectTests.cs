using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackCancelAffectTests
{
    private const ushort BlockRsvFlag = 0x0080;

    public static void Run()
    {
        Type19AddsBlockRsvDuringScoreComposition();
        CancelamentoRemovesOnlyFirstType19AndClearsBlockWhenLastIsGone();
        CancelamentoStopsTheBatchAfterRemovingType19();
        CancelamentoFallsBackToGenericType32WhenType19IsAbsent();
        CancelamentoFallsBackToType32OnlyAtFirstNpcIndex();
        CancelamentoFallbackHonorsNpcAffectGates();
        CancelamentoRemovesType19FromNpcSnapshots();
    }

    private static void Type19AddsBlockRsvDuringScoreComposition()
    {
        var affect = CreateAffect((0, 19, 7, 23, 41, 59));
        var score = LegacyCurrentScoreMath.CalculateCurrentScoreAffectCompositionStage(
            affect,
            startingMaxHp: 100,
            startingMaxMp: 100,
            startingArmorClass: 10,
            startingDamage: 20,
            sourceMobMagic: 30,
            startingMagic: 30,
            startingLearnedSkills: 0);

        Check((score.RsvFlagsToAdd & BlockRsvFlag) != 0,
            "An active Type 19 affect did not contribute RSV_BLOCK during score composition.");
    }

    private static void CancelamentoRemovesOnlyFirstType19AndClearsBlockWhenLastIsGone()
    {
        var firstAffect = CreateAffect((0, 19, 7, 23, 41, 59), (2, 2, 1, 2, 3, 4));
        var (world, targetId) = CreatePlayerWorld(firstAffect, blockRsv: true);
        Check(world.TryApplySkillAttack(1, targetId, Cancelamento(), itemData: null, weather: 0,
                randomFactor: -1, out var outcome) == LegacySkillAttackResult.Accepted &&
            outcome?.TargetAffectSnapshot is not null,
            "Cancelamento did not return an updated target affect snapshot for Type 19 removal.");

        var updated = outcome!.TargetAffectSnapshot!;
        Check(updated.AsSpan(0, 8).SequenceEqual(new byte[8]),
            "Cancelamento did not clear all eight bytes of the first Type 19 slot.");
        Check(updated[16] == 2 && updated.AsSpan(17, 7).SequenceEqual(firstAffect.AsSpan(17, 7)),
            "Cancelamento changed a non-Type 19 affect while removing Type 19.");
        var singleRsv = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).Rsv;
        Check((singleRsv & BlockRsvFlag) == 0 &&
                (singleRsv & LegacyCurrentScoreMath.HasteRsvFlag) != 0,
            "Cancelamento did not clear only RSV_BLOCK after removing the last Type 19 affect.");

        var multipleAffects = CreateAffect(
            (0, 19, 1, 2, 3, 4),
            (1, 2, 9, 8, 7, 6),
            (3, 19, 5, 6, 7, 8));
        var (multiWorld, multiTargetId) = CreatePlayerWorld(multipleAffects, blockRsv: true);
        Check(multiWorld.TryApplySkillAttack(1, multiTargetId, Cancelamento(), itemData: null, weather: 0,
                randomFactor: -1, out var multiOutcome) == LegacySkillAttackResult.Accepted &&
            multiOutcome?.TargetAffectSnapshot is not null,
            "Cancelamento did not return an updated snapshot for multiple Type 19 affects.");

        var multiUpdated = multiOutcome!.TargetAffectSnapshot!;
        Check(multiUpdated.AsSpan(0, 8).SequenceEqual(new byte[8]) && multiUpdated[24] == 19 &&
                multiUpdated.AsSpan(25, 7).SequenceEqual(multipleAffects.AsSpan(25, 7)),
            "Cancelamento did not remove only the first Type 19 affect.");
        var multiRsv = LegacyMobCombatState.Read(multiOutcome.TargetMobSnapshot).Rsv;
        Check((multiRsv & BlockRsvFlag) != 0 &&
                (multiRsv & LegacyCurrentScoreMath.HasteRsvFlag) != 0,
            "Cancelamento failed to preserve the remaining Type 19 and unrelated RSV_HASTE flags.");
    }

    private static void CancelamentoStopsTheBatchAfterRemovingType19()
    {
        var firstAffect = CreateAffect((0, 19, 1, 2, 3, 4));
        var secondAffect = CreateAffect((0, 19, 5, 6, 7, 8));
        var world = CreateBatchWorld(firstAffect, secondAffect);
        var batch = new LegacyAttackActionBatchRequest(
            1,
            [
                new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Skill, 2, SkillTargetIndex: 0),
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Skill, 3, SkillTargetIndex: 1),
            ],
            Skill: Cancelamento(),
            ItemData: null);

        Check(world.TryApplyAttackActionBatch(batch, out var outcome) && outcome is not null,
            "Cancelamento batch was rejected.");
        Check(outcome!.SlotOutcomes.Count == 1 && outcome.SlotOutcomes[0].SkillOutcome?.TargetAffectSnapshot is not null,
            "A successful Type 19 removal did not stop processing later target slots.");
        Check(world.TryGetCharacterSnapshot(3, out _, out _, out _, out _, out var laterTargetAffect) &&
                laterTargetAffect is not null && laterTargetAffect.AsSpan().SequenceEqual(secondAffect),
            "Cancelamento changed a later target after it removed Type 19 from an earlier target.");
    }

    private static void CancelamentoFallsBackToGenericType32WhenType19IsAbsent()
    {
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var world = CreateBatchWorld(emptyAffect, emptyAffect);
        var batch = new LegacyAttackActionBatchRequest(
            1,
            [
                new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Skill, 2, SkillTargetIndex: 0),
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Skill, 3, SkillTargetIndex: 1),
            ],
            Skill: Cancelamento(),
            ItemData: null);

        Check(world.TryApplyAttackActionBatch(batch, out var outcome) && outcome is not null,
            "Cancelamento fallback batch was rejected.");
        Check(outcome!.SlotOutcomes.Count == 2 &&
                outcome.SlotOutcomes.All(static slot => slot.SkillResult == LegacySkillAttackResult.Accepted &&
                    slot.SkillOutcome?.TargetAffectSnapshot is not null),
            "Cancelamento without Type 19 did not use its generic Type 32 path for every target.");
        foreach (var targetId in new[] { 2, 3 })
        {
            Check(world.TryGetCharacterSnapshot(targetId, out _, out _, out _, out _, out var updated) &&
                    updated is not null && HasAffectType(updated, 32),
                $"Cancelamento fallback did not apply Type 32 to target {targetId}.");
        }
    }

    private static void CancelamentoRemovesType19FromNpcSnapshots()
    {
        var attackerMob = CreateMob(blockRsv: false);
        var npcMob = CreateMob(blockRsv: true);
        var npcAffect = CreateAffect((0, 19, 1, 2, 3, 4));
        var world = new WorldHub();
        Check(world.Enter(1, "CANCEL_NPC_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
            world.SetCharacterState(1, 0, 0, 1, 0, 0, 2_000, 2_000, attackerMob),
            "Cancelamento NPC attacker fixture was not installed.");
        var npcId = world.EnterNpc(npcMob, 2_005, 2_005, npcAffect, requestedConnectionId: 20_047);
        Check(npcId == 20_047, "Cancelamento NPC fixture could not be registered.");

        Check(world.TryApplySkillAttack(1, npcId, Cancelamento(), itemData: null, weather: 0,
                randomFactor: -1, out var outcome) == LegacySkillAttackResult.Accepted &&
            outcome?.TargetAffectSnapshot is not null,
            "Cancelamento did not remove Type 19 from an NPC target.");
        Check(outcome!.TargetAffectSnapshot!.AsSpan().SequenceEqual(new byte[LegacyAccountSnapshot.AffectStride]) &&
                (LegacyMobCombatState.Read(outcome.TargetMobSnapshot).Rsv & BlockRsvFlag) == 0,
            "Cancelamento did not return cleared NPC affect and RSV_BLOCK snapshots.");
        Check(world.TryGetNpcSnapshot(npcId, out var npc) && npc is not null &&
                npc.AffectSnapshot.AsSpan().SequenceEqual(new byte[LegacyAccountSnapshot.AffectStride]) &&
                (LegacyMobCombatState.Read(npc.MobSnapshot).Rsv & BlockRsvFlag) == 0,
            "Cancelamento did not update the authoritative NPC snapshots.");
    }

    private static void CancelamentoFallsBackToType32OnlyAtFirstNpcIndex()
    {
        var firstNpc = ApplyCancelamentoToNpc(
            LegacyMobAiPlanner.MaximumPlayerId,
            CreateMob(blockRsv: false),
            CreateMob(blockRsv: false));
        Check(firstNpc.Result == LegacySkillAttackResult.Accepted,
            "Cancelamento rejected the first NPC target without Type 19.");
        Check(firstNpc.World.TryGetNpcSnapshot(firstNpc.NpcId, out var firstNpcSnapshot) &&
                firstNpcSnapshot is not null && HasAffectType(firstNpcSnapshot.AffectSnapshot, 32) &&
                firstNpc.Outcome?.TargetAffectSnapshot is { } firstNpcOutcomeAffect &&
                HasAffectType(firstNpcOutcomeAffect, 32),
            "Cancelamento did not apply the generic Type 32 fallback at MAX_USER.");

        var laterNpc = ApplyCancelamentoToNpc(
            LegacyMobAiPlanner.MaximumPlayerId + 1,
            CreateMob(blockRsv: false),
            CreateMob(blockRsv: false));
        Check(laterNpc.Result == LegacySkillAttackResult.Accepted,
            "Cancelamento rejected the NPC above MAX_USER when its generic SetAffect was refused.");
        Check(laterNpc.World.TryGetNpcSnapshot(laterNpc.NpcId, out var laterNpcSnapshot) &&
                laterNpcSnapshot is not null && !HasAffectType(laterNpcSnapshot.AffectSnapshot, 32),
            "Cancelamento applied Type 32 above the legacy SetAffect connection limit.");
    }

    private static void CancelamentoFallbackHonorsNpcAffectGates()
    {
        var sameGuildAttacker = CreateMob(blockRsv: false);
        var sameGuildNpc = CreateMob(blockRsv: false);
        BinaryPrimitives.WriteUInt16LittleEndian(sameGuildAttacker.AsSpan(LegacyAccountSnapshot.MobGuildOffset), 12);
        BinaryPrimitives.WriteUInt16LittleEndian(sameGuildNpc.AsSpan(LegacyAccountSnapshot.MobGuildOffset), 12);
        AssertNpcFallbackNotApplied(ApplyCancelamentoToNpc(
            LegacyMobAiPlanner.MaximumPlayerId, sameGuildAttacker, sameGuildNpc, attackerGuildId: 12),
            "same-guild NPC");

        var sameLeaderNpc = CreateMob(blockRsv: false);
        AssertNpcFallbackNotApplied(ApplyCancelamentoToNpc(
            LegacyMobAiPlanner.MaximumPlayerId,
            CreateMob(blockRsv: false),
            sameLeaderNpc,
            npcLeaderConnectionId: 1),
            "same-leader NPC");

        var clanSixNpc = CreateMob(blockRsv: false);
        clanSixNpc[LegacyAccountSnapshot.MobClanOffset] = 6;
        AssertNpcFallbackNotApplied(ApplyCancelamentoToNpc(
            LegacyMobAiPlanner.MaximumPlayerId, CreateMob(blockRsv: false), clanSixNpc),
            "Clan 6 NPC");

        var merchantNpc = CreateMob(blockRsv: false);
        merchantNpc[LegacyAccountSnapshot.MobMerchantOffset] = 1;
        AssertNpcFallbackNotApplied(ApplyCancelamentoToNpc(
            LegacyMobAiPlanner.MaximumPlayerId, CreateMob(blockRsv: false), merchantNpc),
            "merchant NPC");

        AssertNpcFallbackNotApplied(ApplyCancelamentoToNpc(
            LegacyMobAiPlanner.MaximumPlayerId,
            CreateMob(blockRsv: false),
            CreateMob(blockRsv: true)),
            "RSV_BLOCK NPC");
    }

    private static void AssertNpcFallbackNotApplied(
        (WorldHub World, int NpcId, LegacySkillAttackResult Result, LegacySkillAttackOutcome? Outcome) attempt,
        string scenario)
    {
        Check(attempt.Result == LegacySkillAttackResult.Accepted,
            $"Cancelamento rejected the {scenario} when its generic SetAffect was refused.");
        Check(attempt.World.TryGetNpcSnapshot(attempt.NpcId, out var npc) && npc is not null &&
                !HasAffectType(npc.AffectSnapshot, 32),
            $"Cancelamento applied Type 32 to a {scenario}.");
    }

    private static (WorldHub World, int NpcId, LegacySkillAttackResult Result, LegacySkillAttackOutcome? Outcome)
        ApplyCancelamentoToNpc(
            int npcId,
            byte[] attackerMob,
            byte[] npcMob,
            int attackerGuildId = 0,
            int npcLeaderConnectionId = 0)
    {
        var world = new WorldHub();
        Check(world.Enter(1, "CANCEL_NPC_FALLBACK_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
            world.SetCharacterState(1, 0, attackerGuildId, 1, 0, 0, 2_000, 2_000, attackerMob),
            "Cancelamento NPC fallback attacker fixture was not installed.");
        var createdNpcId = world.EnterNpc(
            npcMob,
            2_005,
            2_005,
            new byte[LegacyAccountSnapshot.AffectStride],
            requestedConnectionId: npcId,
            leaderConnectionId: npcLeaderConnectionId);
        Check(createdNpcId == npcId, $"Cancelamento NPC fallback fixture {npcId} could not be registered.");
        var result = world.TryApplySkillAttack(
            1,
            npcId,
            Cancelamento(),
            itemData: null,
            weather: 0,
            randomFactor: -1,
            out var outcome);
        return (world, npcId, result, outcome);
    }

    private static (WorldHub World, int TargetId) CreatePlayerWorld(byte[] targetAffect, bool blockRsv)
    {
        var world = new WorldHub();
        Check(world.Enter(1, "CANCEL_PLAYER_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
            world.Enter(2, "CANCEL_PLAYER_TARGET", (_, _) => ValueTask.CompletedTask),
            "Cancelamento participants were not registered.");
        Check(world.SetCharacterState(1, 0, 0, 1, 0, 0, 2_000, 2_000, CreateMob(blockRsv: false)) &&
            world.SetCharacterState(2, 0, 0, 2, 0, 0, 2_005, 2_005, CreateMob(blockRsv),
                LegacyAccountSnapshot.ClassMasterMortal, targetAffect),
            "Cancelamento player fixtures were not installed.");
        return (world, 2);
    }

    private static WorldHub CreateBatchWorld(byte[] firstAffect, byte[] secondAffect)
    {
        var world = new WorldHub();
        Check(world.Enter(1, "CANCEL_BATCH_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
            world.Enter(2, "CANCEL_BATCH_TARGET_1", (_, _) => ValueTask.CompletedTask) &&
            world.Enter(3, "CANCEL_BATCH_TARGET_2", (_, _) => ValueTask.CompletedTask),
            "Cancelamento batch participants were not registered.");
        Check(world.SetCharacterState(1, 0, 0, 1, 0, 0, 2_000, 2_000, CreateMob(blockRsv: false)) &&
            world.SetCharacterState(2, 0, 0, 2, 0, 0, 2_005, 2_005, CreateMob(HasAffectType(firstAffect, 19)),
                LegacyAccountSnapshot.ClassMasterMortal, firstAffect) &&
            world.SetCharacterState(3, 0, 0, 3, 0, 0, 2_006, 2_006, CreateMob(HasAffectType(secondAffect, 19)),
                LegacyAccountSnapshot.ClassMasterMortal, secondAffect),
            "Cancelamento batch fixtures were not installed.");
        return world;
    }

    private static byte[] CreateMob(bool blockRsv)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(50, 0, 100, 0, 0, 0, 0, 1_000, 1_000, 1_000, 1_000,
            0, 0, 1_000, 0, 0, 100, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        if (blockRsv)
            BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobRsvOffset),
                (ushort)(BlockRsvFlag | LegacyCurrentScoreMath.HasteRsvFlag));
        return mob;
    }

    private static bool HasAffectType(ReadOnlySpan<byte> affect, byte type)
    {
        for (var offset = 0; offset < affect.Length; offset += 8)
            if (affect[offset] == type)
                return true;
        return false;
    }

    private static byte[] CreateAffect(params (int Slot, byte Type, byte Value, byte Level, byte Time, byte Reserved)[] records)
    {
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var (slot, type, value, level, time, reserved) in records)
        {
            var offset = checked(slot * 8);
            affect[offset] = type;
            affect[offset + 1] = value;
            affect[offset + 2] = level;
            affect[offset + 3] = time;
            affect[offset + 4] = reserved;
            affect[offset + 5] = 0xA5;
            affect[offset + 6] = 0x5A;
            affect[offset + 7] = 0xC3;
        }
        return affect;
    }

    private static LegacySkillDefinition Cancelamento() =>
        new(47, 269, 5, 34, 30, 5, 0, 0, 0, 0, 32, 0, 1,
            [], [], 0, 0, 1, 5, 0, 0, 0, 0, "Cancelamento");

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

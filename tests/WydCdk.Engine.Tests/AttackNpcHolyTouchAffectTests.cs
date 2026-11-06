using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackNpcHolyTouchAffectTests
{
    public static void Run()
    {
        ToqueSagradoProjectsType1AtTheFirstNpcIndexAndResetsFourTicks();
        ToqueSagradoHonorsSourceNpcAffectGates();
        Type1ExpiryRestoresGeneratedAndNonGeneratedNpcScore();
    }

    private static void ToqueSagradoProjectsType1AtTheFirstNpcIndexAndResetsFourTicks()
    {
        var first = ApplyToqueSagrado(LegacyMobAiPlanner.MaximumPlayerId);
        Check(first.Result == LegacySkillAttackResult.Accepted,
            "Toque Sagrado rejected a valid first-NPC target.");
        Check(first.World.TryGetNpcSnapshot(first.NpcId, out var firstNpc) && firstNpc is not null &&
              HasAffectType(firstNpc.AffectSnapshot, 1) && firstNpc.AffectSnapshot[1] == 2,
            "Toque Sagrado did not apply its Type 1 value at NPC 1000.");

        var appliedScore = LegacyScore.Read(firstNpc!.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(appliedScore.AttackRun == 0x64 && appliedScore.Intelligence == 60,
            $"Type 1 did not project Attack -30, Run -2, and head-gated Intelligence -40; AttackRun=0x{appliedScore.AttackRun:X2}, Int={appliedScore.Intelligence}.");
        Check(GetAffectTime(firstNpc.AffectSnapshot, 1) == 1,
            "The first Type 1 application did not use the one-tick source duration.");

        Check(first.World.TryApplySkillAttack(
                  1,
                  first.NpcId,
                  ToqueSagrado(),
                  itemData: null,
                  weather: 0,
                  randomFactor: -1,
                  out var refreshedOutcome,
                  affectRandomFactor: 0) == LegacySkillAttackResult.Accepted &&
              refreshedOutcome?.TargetAffectSnapshot is { } refreshedAffect &&
              GetAffectTime(refreshedAffect, 1) == 4,
            "Reapplying Toque Sagrado did not reset Type 1 to four legacy timer ticks.");
        Check(first.World.TryGetNpcSnapshot(first.NpcId, out var refreshedNpc) && refreshedNpc is not null &&
              LegacyScore.Read(refreshedNpc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x64, Intelligence: 60 },
            "Refreshing Type 1 applied its AttackRun or Intelligence penalty twice.");

        var lowHead = ApplyToqueSagrado(
            LegacyMobAiPlanner.MaximumPlayerId,
            npcMob: CreateMob(level: 50, attackRun: 0x96, intelligence: 100, headIndex: 50));
        Check(lowHead.World.TryGetNpcSnapshot(lowHead.NpcId, out var lowHeadNpc) && lowHeadNpc is not null &&
              LegacyScore.Read(lowHeadNpc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x64, Intelligence: 100 },
            "Type 1 changed Intelligence for a head item index at the 50 boundary.");

        var later = ApplyToqueSagrado(LegacyMobAiPlanner.MaximumPlayerId + 1);
        Check(later.Result == LegacySkillAttackResult.Accepted &&
              later.World.TryGetNpcSnapshot(later.NpcId, out var laterNpc) && laterNpc is not null &&
              !HasAffectType(laterNpc.AffectSnapshot, 1) &&
              LegacyScore.Read(laterNpc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x96, Intelligence: 100 },
            "Toque Sagrado applied beyond the legacy first NPC index.");
    }

    private static void ToqueSagradoHonorsSourceNpcAffectGates()
    {
        AssertNotApplied(ApplyToqueSagrado(
            LegacyMobAiPlanner.MaximumPlayerId,
            affectRandomFactor: 28), "AffectResist roll above the source threshold");

        var sameGuildMob = CreateMob(level: 50, attackRun: 0x96, intelligence: 100, headIndex: 51);
        BinaryPrimitives.WriteUInt16LittleEndian(
            sameGuildMob.AsSpan(LegacyAccountSnapshot.MobGuildOffset), 12);
        AssertNotApplied(ApplyToqueSagrado(
            LegacyMobAiPlanner.MaximumPlayerId,
            npcMob: sameGuildMob,
            attackerGuildId: 12), "same guild");

        AssertNotApplied(ApplyToqueSagrado(
            LegacyMobAiPlanner.MaximumPlayerId,
            npcLeaderConnectionId: 1), "same leader");

        var clanSixMob = CreateMob(level: 50, attackRun: 0x96, intelligence: 100, headIndex: 51);
        clanSixMob[LegacyAccountSnapshot.MobClanOffset] = 6;
        AssertNotApplied(ApplyToqueSagrado(
            LegacyMobAiPlanner.MaximumPlayerId,
            npcMob: clanSixMob), "Clan 6");

        var merchantMob = CreateMob(level: 50, attackRun: 0x96, intelligence: 100, headIndex: 51);
        merchantMob[LegacyAccountSnapshot.MobMerchantOffset] = 1;
        AssertNotApplied(ApplyToqueSagrado(
            LegacyMobAiPlanner.MaximumPlayerId,
            npcMob: merchantMob), "merchant");

        var blockedMob = CreateMob(level: 50, attackRun: 0x96, intelligence: 100, headIndex: 51);
        BinaryPrimitives.WriteUInt16LittleEndian(
            blockedMob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), LegacyCurrentScoreMath.BlockRsvFlag);
        AssertNotApplied(ApplyToqueSagrado(
            LegacyMobAiPlanner.MaximumPlayerId,
            npcMob: blockedMob), "RSV_BLOCK");
    }

    private static void Type1ExpiryRestoresGeneratedAndNonGeneratedNpcScore()
    {
        const int npcId = LegacyMobAiPlanner.MaximumPlayerId;
        var nonGenerated = ApplyToqueSagrado(npcId, npcMob: CreateMob(
            level: 50, attackRun: 0x12, intelligence: 100, headIndex: 51));
        Check(nonGenerated.Result == LegacySkillAttackResult.Accepted &&
              nonGenerated.World.TryGetNpcSnapshot(npcId, out var affectedNpc) && affectedNpc is not null &&
              LegacyScore.Read(affectedNpc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x00, Intelligence: 60 },
            "The Type 1 saturation fixture did not reach zero AttackRun before expiry.");
        Check(nonGenerated.World.TryProcessNonGeneratedNpcTimerAffects(npcId, out var nonGeneratedOutcome) &&
              nonGeneratedOutcome.AffectTick is { ScoreRefreshRequired: true, HpUpdateRequired: false },
            "The non-generated NPC timer did not report a score-only Type 1 expiry.");
        AssertRestoredType1Npc(nonGenerated.World, npcId, "non-generated");

        var generated = ApplyToqueSagrado(npcId, generateIndex: 0, npcMob: CreateMob(
            level: 50, attackRun: 0x12, intelligence: 100, headIndex: 51));
        Check(generated.Result == LegacySkillAttackResult.Accepted &&
              generated.World.TryProcessGeneratedNpcTimerAffects(npcId, out var generatedOutcome) &&
              generatedOutcome.AffectTick is { ScoreRefreshRequired: true, HpUpdateRequired: false },
            "The generated NPC timer did not report a score-only Type 1 expiry.");
        AssertRestoredType1Npc(generated.World, npcId, "generated");
    }

    private static void AssertRestoredType1Npc(WorldHub world, int npcId, string scenario)
    {
        Check(world.TryGetNpcSnapshot(npcId, out var npc) && npc is not null &&
              npc.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              LegacyScore.Read(npc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x12, Intelligence: 100 },
            $"The {scenario} NPC Type 1 expiry did not clear the record and restore its saturated pre-affect score.");
    }

    private static (WorldHub World, int NpcId, LegacySkillAttackResult Result) ApplyToqueSagrado(
        int npcId,
        int affectRandomFactor = 27,
        byte[]? npcMob = null,
        int attackerGuildId = 0,
        int npcLeaderConnectionId = 0,
        int generateIndex = -1)
    {
        var world = new WorldHub();
        var attackerMob = CreateMob(level: 500, attackRun: 0x96, intelligence: 100, headIndex: 0);
        Check(world.Enter(1, "HOLY_TOUCH_NPC_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(1, 0, attackerGuildId, 1, 0, 0, 2_000, 2_000, attackerMob),
            "Toque Sagrado attacker fixture was not installed.");

        npcMob ??= CreateMob(level: 50, attackRun: 0x96, intelligence: 100, headIndex: 51);
        var createdNpcId = world.EnterNpc(
            npcMob,
            2_005,
            2_005,
            new byte[LegacyAccountSnapshot.AffectStride],
            requestedConnectionId: npcId,
            generateIndex: generateIndex,
            leaderConnectionId: npcLeaderConnectionId);
        Check(createdNpcId == npcId, $"Toque Sagrado NPC fixture {npcId} could not be registered.");

        var result = world.TryApplySkillAttack(
            1,
            npcId,
            ToqueSagrado(),
            itemData: null,
            weather: 0,
            randomFactor: -1,
            out _,
            affectRandomFactor: affectRandomFactor);
        return (world, npcId, result);
    }

    private static void AssertNotApplied(
        (WorldHub World, int NpcId, LegacySkillAttackResult Result) attempt,
        string scenario)
    {
        Check(attempt.Result == LegacySkillAttackResult.Accepted,
            $"Toque Sagrado rejected its {scenario} fixture.");
        Check(attempt.World.TryGetNpcSnapshot(attempt.NpcId, out var npc) && npc is not null &&
              !HasAffectType(npc.AffectSnapshot, 1) &&
              LegacyScore.Read(npc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x96, Intelligence: 100 },
            $"Toque Sagrado changed an NPC behind the {scenario} gate.");
    }

    private static byte[] CreateMob(int level, byte attackRun, short intelligence, int headIndex)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(level, 0, 83, 0, attackRun, 0, 0, 1_000, 1_000, 1_000, 1_000,
            0, intelligence, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        new LegacyItem(checked((short)headIndex), 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
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

    private static LegacySkillDefinition ToqueSagrado() =>
        new(1, 48, 4, 20, 3, 0, 0, 0, 0, 0, 1, 2, 0,
            [10, 0, 0, 10, 0, 0, 0, 0], [10, 0, 0, 10, 0, 0, 0, 0],
            2, 0, 1, 13, 0, 2, 0, 0, "Toque_Sagrado");

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

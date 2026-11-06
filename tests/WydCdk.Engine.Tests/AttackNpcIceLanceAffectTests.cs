using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackNpcIceLanceAffectTests
{
    private static readonly LegacyItemDataTable ItemData =
        LegacyItemDataTable.Load(Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());

    public static void Run()
    {
        LancaDeGeloProjectsType1OnDamagingNpcSkillPath();
        LancaDeGeloHonorsNpcAffectResistanceAndSetAffectBound();
        LancaDeGeloType1ExpiryRestoresGeneratedAndNonGeneratedNpcScore();
    }

    private static void LancaDeGeloProjectsType1OnDamagingNpcSkillPath()
    {
        var first = ApplyLancaDeGelo(LegacyMobAiPlanner.MaximumPlayerId);
        Check(first.Result == LegacySkillAttackResult.Accepted && first.Outcome is { Damage: > 0 },
            "Lança de Gelo did not resolve a damaging InstanceType 3 NPC attack.");
        Check(first.World.TryGetNpcSnapshot(first.NpcId, out var firstNpc) && firstNpc is not null &&
              HasAffectType(firstNpc.AffectSnapshot, 1) && firstNpc.AffectSnapshot[1] == 2 &&
              GetAffectTime(firstNpc.AffectSnapshot, 1) == 2,
            "Lança de Gelo did not apply its two-tick Type 1 affect at NPC 1000.");

        var firstScore = LegacyScore.Read(firstNpc!.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(firstScore.AttackRun == 0x64 && firstScore.Intelligence == 60,
            $"Lança de Gelo Type 1 did not project Attack -30, Run -2, and head-gated Intelligence -40; AttackRun=0x{firstScore.AttackRun:X2}, Int={firstScore.Intelligence}.");
        Check(first.Outcome!.TargetAffectSnapshot is { } outcomeAffect && HasAffectType(outcomeAffect, 1),
            "The damaging NPC skill outcome omitted the applied Type 1 affect snapshot.");

        var refreshResult = first.World.TryApplySkillAttack(
            1, first.NpcId, LancaDeGelo(), ItemData, weather: 0, randomFactor: 100,
            out _, affectRandomFactor: 0);
        var refreshedFound = first.World.TryGetNpcSnapshot(first.NpcId, out var refreshedNpc);
        Check(refreshResult == LegacySkillAttackResult.Accepted && refreshedFound && refreshedNpc is not null,
            $"Reapplying Lança de Gelo returned {refreshResult} or lost NPC 1000.");
        var refreshedScore = LegacyScore.Read(refreshedNpc!.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(GetAffectTime(refreshedNpc.AffectSnapshot, 1) == 4 &&
              refreshedScore is { AttackRun: 0x64, Intelligence: 60 },
            $"Reapplying Lança de Gelo did not reset Type 1 to four ticks without stacking its score penalty; time={GetAffectTime(refreshedNpc.AffectSnapshot, 1)}, AttackRun=0x{refreshedScore.AttackRun:X2}, Int={refreshedScore.Intelligence}.");
    }

    private static void LancaDeGeloHonorsNpcAffectResistanceAndSetAffectBound()
    {
        var resisted = ApplyLancaDeGelo(
            LegacyMobAiPlanner.MaximumPlayerId,
            attackerLevel: 1,
            npcLevel: 100,
            affectRandomFactor: 0);
        AssertNotApplied(resisted, "AffectResist level gate");

        var laterNpc = ApplyLancaDeGelo(LegacyMobAiPlanner.MaximumPlayerId + 1);
        Check(laterNpc.Result == LegacySkillAttackResult.Accepted &&
              laterNpc.World.TryGetNpcSnapshot(laterNpc.NpcId, out var laterSnapshot) && laterSnapshot is not null &&
              !HasAffectType(laterSnapshot.AffectSnapshot, 1) &&
              LegacyScore.Read(laterSnapshot.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x96, Intelligence: 100 },
            "Lança de Gelo applied Type 1 beyond the legacy first-NPC SetAffect bound.");
    }

    private static void LancaDeGeloType1ExpiryRestoresGeneratedAndNonGeneratedNpcScore()
    {
        AssertType1Expires(generated: false);
        AssertType1Expires(generated: true);
    }

    private static void AssertType1Expires(bool generated)
    {
        const int npcId = LegacyMobAiPlanner.MaximumPlayerId;
        var attempt = ApplyLancaDeGelo(
            npcId,
            npcMob: CreateMob(level: 50, attackRun: 0x12, intelligence: 100, headIndex: 51),
            generateIndex: generated ? 0 : -1);
        Check(attempt.Result == LegacySkillAttackResult.Accepted &&
              attempt.World.TryGetNpcSnapshot(npcId, out var affectedNpc) && affectedNpc is not null &&
              LegacyScore.Read(affectedNpc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x00, Intelligence: 60 },
            $"The {(generated ? "generated" : "non-generated")} Lança de Gelo Type 1 fixture did not saturate AttackRun.");

        for (var tick = 0; tick < 2; tick++)
        {
            var processed = false;
            var timerVisits = generated ? 4 : 1;
            for (var visit = 0; visit < timerVisits; visit++)
            {
                processed = generated
                    ? attempt.World.TryProcessGeneratedNpcCombatTimerAffects(npcId, out _)
                    : attempt.World.TryProcessNonGeneratedNpcTimerAffects(npcId, out _);
            }
            if (tick == 0)
            {
                Check(!processed && attempt.World.TryGetNpcSnapshot(npcId, out var tickingNpc) && tickingNpc is not null &&
                      GetAffectTime(tickingNpc.AffectSnapshot, 1) == 1,
                    $"The {(generated ? "generated" : "non-generated")} NPC Type 1 timer did not consume its first countdown tick.");
            }
            else
            {
                Check(processed,
                    $"The {(generated ? "generated" : "non-generated")} NPC Type 1 timer did not report its score-refresh expiry.");
            }
        }

        Check(attempt.World.TryGetNpcSnapshot(npcId, out var expiredNpc) && expiredNpc is not null &&
              expiredNpc.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              LegacyScore.Read(expiredNpc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x12, Intelligence: 100 },
            $"The {(generated ? "generated" : "non-generated")} NPC Type 1 expiry did not clear the record and restore its saturated baseline.");
    }

    private static (WorldHub World, int NpcId, LegacySkillAttackResult Result, LegacySkillAttackOutcome? Outcome) ApplyLancaDeGelo(
        int npcId,
        int affectRandomFactor = 0,
        int attackerLevel = 1_500,
        int npcLevel = 50,
        byte[]? npcMob = null,
        int generateIndex = -1)
    {
        var world = new WorldHub();
        var attackerMob = CreateMob(level: attackerLevel, attackRun: 0x96, intelligence: 100, headIndex: 0);
        Check(world.Enter(1, "ICE_LANCE_NPC_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(1, 0, 0, 1, 0, 0, 2_000, 2_000, attackerMob),
            "Lança de Gelo attacker fixture was not installed.");

        npcMob ??= CreateMob(level: npcLevel, attackRun: 0x96, intelligence: 100, headIndex: 51);
        var createdNpcId = world.EnterNpc(
            npcMob,
            2_001,
            2_000,
            new byte[LegacyAccountSnapshot.AffectStride],
            requestedConnectionId: npcId,
            generateIndex: generateIndex);
        Check(createdNpcId == npcId, $"Lança de Gelo NPC fixture {npcId} could not be registered.");

        var result = world.TryApplySkillAttack(
            1,
            npcId,
            LancaDeGelo(),
            ItemData,
            weather: 0,
            randomFactor: 100,
            out var outcome,
            affectRandomFactor: affectRandomFactor);
        return (world, npcId, result, outcome);
    }

    private static void AssertNotApplied(
        (WorldHub World, int NpcId, LegacySkillAttackResult Result, LegacySkillAttackOutcome? Outcome) attempt,
        string scenario)
    {
        Check(attempt.Result == LegacySkillAttackResult.Accepted,
            $"Lança de Gelo rejected its {scenario} NPC fixture.");
        Check(attempt.World.TryGetNpcSnapshot(attempt.NpcId, out var npc) && npc is not null &&
              !HasAffectType(npc.AffectSnapshot, 1) &&
              LegacyScore.Read(npc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x96, Intelligence: 100 },
            $"Lança de Gelo changed an NPC behind the {scenario} gate.");
    }

    private static byte[] CreateMob(int level, byte attackRun, short intelligence, int headIndex)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(level, 0, 83, 0, attackRun, 0, 0, 1_000_000, 1_000, 1_000_000, 1_000,
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

    private static LegacySkillDefinition LancaDeGelo() =>
        new(34, 69, 1, 15, 3, 6, 3, 95, 0, 0, 1, 2, 1,
            [5, 0, 0, 7, 0, 0, 0, 0], [5, 0, 0, 7, 0, 0, 0, 0],
            2, 0, 1, 1, 0, 2, 0, 1, "Lanca_de_Gelo");

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

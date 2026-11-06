using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackNpcType1DamageSkillAffectTests
{
    private static readonly LegacyItemDataTable ItemData =
        LegacyItemDataTable.Load(Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());

    public static void Run()
    {
        TempestadeDeGeloAppliesType1AtTheFirstNpcIndex();
        NevascaAppliesType1AtTheFirstNpcIndex();
        CrossSkillType1RecastResetsDurationWithoutStackingScore();
        Type1DamageSkillsRespectTheSetAffectNpcBound();
    }

    private static void TempestadeDeGeloAppliesType1AtTheFirstNpcIndex() =>
        AssertType1Applied(TempestadeDeGelo(), expectedTime: 4);

    private static void NevascaAppliesType1AtTheFirstNpcIndex() =>
        AssertType1Applied(Nevasca(), expectedTime: 2);

    private static void AssertType1Applied(LegacySkillDefinition skill, uint expectedTime)
    {
        var fixture = CreateFixture(LegacyMobAiPlanner.MaximumPlayerId);
        var result = Apply(fixture, skill, out var outcome);
        Check(result == LegacySkillAttackResult.Accepted && outcome is { Damage: > 0 },
            $"Skill {skill.Id} did not resolve a damaging NPC attack.");
        Check(fixture.World.TryGetNpcSnapshot(fixture.NpcId, out var npc) && npc is not null &&
              HasAffectType(npc.AffectSnapshot, 1) && npc.AffectSnapshot[1] == 2 &&
              GetAffectTime(npc.AffectSnapshot, 1) == expectedTime,
            $"Skill {skill.Id} did not apply its Type 1 affect with timer {expectedTime} at NPC 1000.");

        var score = LegacyScore.Read(npc!.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(score is { AttackRun: 0x64, Intelligence: 60 },
            $"Skill {skill.Id} did not project the shared Type 1 score stage; AttackRun=0x{score.AttackRun:X2}, Int={score.Intelligence}.");
        Check(outcome!.TargetAffectSnapshot is { } affect && HasAffectType(affect, 1),
            $"Skill {skill.Id} outcome omitted the applied Type 1 affect snapshot.");
    }

    private static void CrossSkillType1RecastResetsDurationWithoutStackingScore()
    {
        var fixture = CreateFixture(LegacyMobAiPlanner.MaximumPlayerId);
        Check(Apply(fixture, TempestadeDeGelo(), out _) == LegacySkillAttackResult.Accepted,
            "Tempestade de Gelo did not apply before the cross-skill recast.");
        Check(Apply(fixture, Nevasca(), out _) == LegacySkillAttackResult.Accepted,
            "Nevasca did not resolve as a Type 1 recast.");
        Check(fixture.World.TryGetNpcSnapshot(fixture.NpcId, out var npc) && npc is not null &&
              GetAffectTime(npc.AffectSnapshot, 1) == 4 &&
              LegacyScore.Read(npc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x64, Intelligence: 60 },
            "Cross-skill Type 1 recast did not reset to four ticks while preserving the single score penalty.");
    }

    private static void Type1DamageSkillsRespectTheSetAffectNpcBound()
    {
        var fixture = CreateFixture(LegacyMobAiPlanner.MaximumPlayerId + 1);
        var result = Apply(fixture, TempestadeDeGelo(), out _);
        Check(result == LegacySkillAttackResult.Accepted &&
              fixture.World.TryGetNpcSnapshot(fixture.NpcId, out var npc) && npc is not null &&
              !HasAffectType(npc.AffectSnapshot, 1) &&
              LegacyScore.Read(npc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x96, Intelligence: 100 },
            "Tempestade de Gelo crossed SetAffect's first-NPC boundary.");
    }

    private static (WorldHub World, int NpcId) CreateFixture(int npcId)
    {
        var world = new WorldHub();
        var attackerMob = CreateMob(level: 1_500, attackRun: 0x96, intelligence: 100, headIndex: 0);
        Check(world.Enter(1, "TYPE1_DAMAGE_SKILL_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(1, 0, 0, 1, 0, 0, 2_000, 2_000, attackerMob),
            "Type 1 damage-skill attacker fixture was not installed.");

        var npcMob = CreateMob(level: 50, attackRun: 0x96, intelligence: 100, headIndex: 51);
        var createdNpcId = world.EnterNpc(
            npcMob,
            2_001,
            2_000,
            new byte[LegacyAccountSnapshot.AffectStride],
            requestedConnectionId: npcId);
        Check(createdNpcId == npcId, $"NPC fixture {npcId} could not be registered.");
        return (world, npcId);
    }

    private static LegacySkillAttackResult Apply(
        (WorldHub World, int NpcId) fixture,
        LegacySkillDefinition skill,
        out LegacySkillAttackOutcome? outcome) =>
        fixture.World.TryApplySkillAttack(
            1,
            fixture.NpcId,
            skill,
            ItemData,
            weather: 0,
            randomFactor: 100,
            out outcome,
            affectRandomFactor: 0);

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

    // Rows 23 and 36 from TMProject 7.69 Servidor/Server/Common/SkillData.csv.
    private static LegacySkillDefinition TempestadeDeGelo() =>
        new(23, 227, 6, 150, 6, 5, 3, 210, 0, 0, 1, 2, 3,
            [8, 0, 0, 7, 0, 0, 0, 0], [8, 0, 0, 7, 0, 0, 0, 0],
            2, 0, 1, 13, 0, 2, 0, 1, "Tempestade_de_Gelo");

    private static LegacySkillDefinition Nevasca() =>
        new(36, 72, 1, 35, 5, 6, 3, 200, 0, 0, 1, 2, 1,
            [6, 0, 0, 7, 0, 0, 0, 0], [6, 0, 0, 7, 0, 0, 0, 0],
            2, 0, 1, 1, 0, 2, 0, 1, "Nevasca");

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

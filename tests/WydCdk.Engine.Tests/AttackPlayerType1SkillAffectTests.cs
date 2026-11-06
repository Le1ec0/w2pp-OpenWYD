using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPlayerType1SkillAffectTests
{
    private static readonly LegacyItemDataTable ItemData =
        LegacyItemDataTable.Load(Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());

    public static void Run()
    {
        foreach (var skill in new[] { ToqueSagrado(), TempestadeDeGelo(), LancaDeGelo(), Nevasca() })
            PlayerType1ProducerProjectsScoreAndExpires(skill);
    }

    private static void PlayerType1ProducerProjectsScoreAndExpires(LegacySkillDefinition skill)
    {
        var world = new WorldHub(itemData: ItemData);
        var attackerMob = CreateMob(level: 1_500, attackRun: 0x96, intelligence: 100, hp: 1_000_000, headIndex: 0);
        var targetMob = CreateMob(level: 50, attackRun: 0x96, intelligence: 100, hp: 1_000_000, headIndex: 51);
        Check(world.Enter(1, $"TYPE1_PLAYER_ATTACKER_{skill.Id}", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(1, 0, 0, 1, 0, 0, 2_000, 2_000, attackerMob),
            $"Skill {skill.Id} attacker fixture was not installed.");
        Check(world.Enter(2, $"TYPE1_PLAYER_TARGET_{skill.Id}", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(2, 0, 0, 2, 0, 0, 2_001, 2_000, targetMob),
            $"Skill {skill.Id} target fixture was not installed.");

        var firstResult = world.TryApplySkillAttack(
            1, 2, skill, ItemData, weather: 0, randomFactor: 100, out var firstOutcome,
            affectRandomFactor: 0);
        Check(firstResult == LegacySkillAttackResult.Accepted && firstOutcome?.TargetAffectSnapshot is { } firstAffect &&
              HasAffectType(firstAffect, 1),
            $"Skill {skill.Id} did not apply Type 1 to its player target.");
        AssertType1Score(firstOutcome!.TargetMobSnapshot, skill.Id, "initial application");

        var recastResult = world.TryApplySkillAttack(
            1, 2, skill, ItemData, weather: 0, randomFactor: 100, out var recastOutcome,
            affectRandomFactor: 0);
        Check(recastResult == LegacySkillAttackResult.Accepted && recastOutcome?.TargetAffectSnapshot is { } recastAffect &&
              GetAffectTime(recastAffect, 1) == 4,
            $"Skill {skill.Id} did not reset the existing player Type 1 affect to four timer ticks.");
        AssertType1Score(recastOutcome!.TargetMobSnapshot, skill.Id, "recast");

        LegacyPlayerAffectTimerOutcome? expiry = null;
        for (var tick = 0; tick < 4; tick++)
            expiry = world.TryProcessPlayerTimerAffects(2);
        Check(expiry is { ScoreRefreshRequired: true } && !HasAffectType(expiry.AffectSnapshot, 1),
            $"Skill {skill.Id} player Type 1 did not expire with a score refresh.");
        Check(LegacyScore.Read(expiry!.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x96, Intelligence: 100 },
            $"Skill {skill.Id} Type 1 expiry did not restore the target's baseline score.");
    }

    private static void AssertType1Score(byte[] mob, int skillId, string phase)
    {
        var score = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(score is { AttackRun: 0x64, Intelligence: 60 },
            $"Skill {skillId} {phase} did not project Type 1 onto the player score; AttackRun=0x{score.AttackRun:X2}, Intelligence={score.Intelligence}.");
    }

    private static byte[] CreateMob(int level, byte attackRun, short intelligence, int hp, int headIndex)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(level, 0, 83, 0, attackRun, 0, 0,
            hp, 1_000, hp, 1_000, 0, intelligence, 0, 0, 0, 0, 0, 0);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = 1;
        new LegacyItem(checked((short)headIndex), 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        return mob;
    }

    private static LegacySkillDefinition ToqueSagrado() =>
        new(1, 48, 4, 20, 3, 0, 0, 0, 0, 0, 1, 2, 3,
            [10, 0, 0, 24, 0, 0, 0, 0], [9, 0, 0, 8, 0, 0, 0, 0],
            0, 0, 1, 13, 0, 2, 0, 0, "Toque_Sagrado");

    private static LegacySkillDefinition TempestadeDeGelo() =>
        new(23, 227, 6, 150, 6, 5, 3, 210, 0, 0, 1, 2, 3,
            [8, 0, 0, 7, 0, 0, 0, 0], [8, 0, 0, 7, 0, 0, 0, 0],
            2, 0, 1, 13, 0, 2, 0, 1, "Tempestade_de_Gelo");

    private static LegacySkillDefinition LancaDeGelo() =>
        new(34, 69, 1, 15, 3, 6, 3, 95, 0, 0, 1, 2, 1,
            [5, 0, 0, 7, 0, 0, 0, 0], [5, 0, 0, 7, 0, 0, 0, 0],
            2, 0, 1, 1, 0, 2, 0, 1, "Lanca_de_Gelo");

    private static LegacySkillDefinition Nevasca() =>
        new(36, 72, 1, 35, 5, 6, 3, 200, 0, 0, 1, 2, 1,
            [6, 0, 0, 7, 0, 0, 0, 0], [6, 0, 0, 7, 0, 0, 0, 0],
            2, 0, 1, 1, 0, 2, 0, 1, "Nevasca");

    private static bool HasAffectType(ReadOnlySpan<byte> affect, byte type)
    {
        for (var offset = 0; offset + 8 <= affect.Length; offset += 8)
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

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

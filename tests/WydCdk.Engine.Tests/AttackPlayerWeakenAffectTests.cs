using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPlayerWeakenAffectTests
{
    private static readonly LegacyItemDataTable ItemData =
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    public static void Run()
    {
        EnfraquecerProjectsAndRestoresPlayerDamage();
    }

    private static void EnfraquecerProjectsAndRestoresPlayerDamage()
    {
        var world = new WorldHub(itemData: ItemData);
        var attackerMob = CreateMob(level: 1_500, damage: 100, hp: 1_000, mp: 400, special2: 50);
        var targetMob = CreateMob(level: 50, damage: 200, hp: 700, mp: 250);
        var baseline = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                targetMob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                new byte[LegacyAccountSnapshot.AffectStride],
                new byte[LegacyAccountSnapshot.MobExtraStride],
                0,
                0,
                0),
            ItemData);
        var baselineScore = LegacyMobCombatState.Read(baseline.Mob).CurrentScore;

        Check(world.Enter(1, "WEAKEN_PLAYER_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(1, 0, 0, 1, 0, 0, 2_000, 2_000, attackerMob),
            "Enfraquecer player attacker fixture was not installed.");
        Check(world.Enter(2, "WEAKEN_PLAYER_TARGET", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(2, 0, 0, 2, 0, 0, 2_001, 2_000, targetMob),
            "Enfraquecer player target fixture was not installed.");

        var result = world.TryApplySkillAttack(
            1, 2, Enfraquecer(), ItemData, weather: 0, randomFactor: 100, out var outcome,
            affectRandomFactor: 0);
        Check(result == LegacySkillAttackResult.Accepted &&
              outcome?.TargetAffectSnapshot is { } firstAffect && HasAffectType(firstAffect, 10),
            "Enfraquecer did not apply Type 10 to its player target.");

        var reduction = LegacyCurrentScoreMath.CalculateWeakenAffectStage(outcome!.TargetAffectSnapshot!).DamageReduction;
        var weakenedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        Check(reduction == 20 && weakenedScore.Damage == baselineScore.Damage - reduction,
            $"Enfraquecer did not project Type 10 damage reduction to the player; baseline={baselineScore.Damage}, current={weakenedScore.Damage}, reduction={reduction}.");
        Check(outcome.Damage == 0 && weakenedScore.Hp == 700 && weakenedScore.Mp == 250,
            "Enfraquecer changed player HP/MP or reported direct damage while applying its effect.");
        Check(GetAffectLevel(outcome.TargetAffectSnapshot!, 10) == 50 &&
              GetAffectTime(outcome.TargetAffectSnapshot!, 10) == 3,
            "Enfraquecer did not store the attacker's Special2 level and source duration on the player.");

        var recastResult = world.TryApplySkillAttack(
            1, 2, Enfraquecer(), ItemData, weather: 0, randomFactor: 100, out var recastOutcome,
            affectRandomFactor: 0);
        Check(recastResult == LegacySkillAttackResult.Accepted &&
              recastOutcome?.TargetAffectSnapshot is { } recastAffect && GetAffectTime(recastAffect, 10) == 4,
            "Reapplying Enfraquecer did not reset the player Type 10 affect to four timer ticks.");
        Check(LegacyMobCombatState.Read(recastOutcome!.TargetMobSnapshot).CurrentScore.Damage ==
              baselineScore.Damage - reduction,
            "Reapplying Enfraquecer stacked the player's Type 10 damage reduction.");

        LegacyPlayerAffectTimerOutcome? expiry = null;
        for (var tick = 0; tick < 4; tick++)
            expiry = world.TryProcessPlayerTimerAffects(2);
        Check(expiry is { ScoreRefreshRequired: true } && !HasAffectType(expiry.AffectSnapshot, 10),
            "Player Type 10 expiry did not clear the affect with a score refresh.");
        var restoredScore = LegacyMobCombatState.Read(expiry!.MobSnapshot).CurrentScore;
        Check(restoredScore.Damage == baselineScore.Damage && restoredScore.Hp == 700 && restoredScore.Mp == 250,
            $"Player Type 10 expiry did not restore baseline damage while preserving resources; damage={restoredScore.Damage}, hp={restoredScore.Hp}, mp={restoredScore.Mp}.");
    }

    private static byte[] CreateMob(int level, int damage, int hp, int mp, short special2 = 0)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(level, 0, damage, 0, 0x96, 0, 0,
            1_000, 500, hp, mp, 0, 100, 0, 0, 0, special2, 0, 0);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = 1;
        new LegacyItem(0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        return mob;
    }

    private static LegacySkillDefinition Enfraquecer() =>
        new(51, 45, 3, 35, 2, 6, 0, 0, 0, 0, 10, 10, 1,
            [6, 0, 0, 8, 0, 0, 0, 0], [10, 0, 0, 8, 0, 0, 0, 0],
            0, 0, 1, 13, 0, 0, 0, 0, "Enfraquecer");

    private static bool HasAffectType(ReadOnlySpan<byte> affect, byte type)
    {
        for (var offset = 0; offset + 8 <= affect.Length; offset += 8)
            if (affect[offset] == type)
                return true;
        return false;
    }

    private static ushort GetAffectLevel(ReadOnlySpan<byte> affect, byte type)
    {
        for (var offset = 0; offset + 8 <= affect.Length; offset += 8)
            if (affect[offset] == type)
                return BinaryPrimitives.ReadUInt16LittleEndian(affect.Slice(offset + 2, sizeof(ushort)));
        return 0;
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

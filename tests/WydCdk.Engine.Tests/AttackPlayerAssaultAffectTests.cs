using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPlayerAssaultAffectTests
{
    private static readonly LegacyItemDataTable ItemData =
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    public static void Run()
    {
        AssaltoProjectsAndRestoresPlayerDamageMultiplier();
    }

    private static void AssaltoProjectsAndRestoresPlayerDamageMultiplier()
    {
        var world = new WorldHub(itemData: ItemData);
        var mob = CreateMob(level: 1_500, damage: 500, hp: 700, mp: 250, special3: 50);
        var baseline = Project(mob, new byte[LegacyAccountSnapshot.AffectStride]);
        var baselineScore = LegacyMobCombatState.Read(baseline.Mob).CurrentScore;

        Check(world.Enter(1, "ASSALTO_PLAYER", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(1, 0, 0, 1, 0, 0, 2_000, 2_000, mob),
            "Assalto player fixture was not installed.");

        var result = world.TryApplySkillAttack(
            1, 1, Assalto(), ItemData, weather: 0, randomFactor: 100, out var outcome);
        Check(result == LegacySkillAttackResult.Accepted &&
              outcome?.TargetAffectSnapshot is { } firstAffect && HasAffectType(firstAffect, 13),
            "Assalto did not apply Type 13 to its player caster.");

        var expectedProjection = Project(mob, outcome!.TargetAffectSnapshot!);
        var expectedScore = LegacyMobCombatState.Read(expectedProjection.Mob).CurrentScore;
        var assaultStage = LegacyCurrentScoreMath.CalculateAssaultAffectStage(
            outcome.TargetAffectSnapshot!, baselineScore.Damage, baselineScore.MaxHp);
        Check(GetAffectValue(outcome.TargetAffectSnapshot!, 13) == 7 &&
              GetAffectLevel(outcome.TargetAffectSnapshot!, 13) == 50 &&
              GetAffectTime(outcome.TargetAffectSnapshot!, 13) == 24 &&
              assaultStage.DamageMultiplierAdjustment == 12 &&
              assaultStage.Damage == baselineScore.Damage && assaultStage.MaxHp == baselineScore.MaxHp,
            "Assalto did not preserve the 7.69 Type 13 value, level, duration, and multiplier-only contract.");
        var appliedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        Check(appliedScore.Damage == expectedScore.Damage && appliedScore.Damage > baselineScore.Damage,
            $"Assalto did not project Type 13 into the player CurrentScore; baseline={baselineScore.Damage}, actual={appliedScore.Damage}, expected={expectedScore.Damage}.");
        Check(appliedScore.Hp == 700 && appliedScore.Mp == 250 && outcome.Damage == 0,
            "Assalto changed current HP/MP or reported direct damage while applying its self affect.");

        var recastResult = world.TryApplySkillAttack(
            1, 1, Assalto(), ItemData, weather: 0, randomFactor: 100, out var recastOutcome);
        Check(recastResult == LegacySkillAttackResult.Accepted &&
              recastOutcome?.TargetAffectSnapshot is { } recastAffect && GetAffectTime(recastAffect, 13) == 24,
            "Reapplying Assalto did not refresh its source-derived 24-tick duration.");
        var recastScore = LegacyMobCombatState.Read(recastOutcome!.TargetMobSnapshot).CurrentScore;
        Check(recastScore.Damage == expectedScore.Damage && recastScore.Hp == 700 && recastScore.Mp == 250,
            "Reapplying Assalto stacked the Type 13 multiplier or changed current resources.");

        LegacyPlayerAffectTimerOutcome? expiry = null;
        for (var tick = 0; tick < 24; tick++)
            expiry = world.TryProcessPlayerTimerAffects(1);
        Check(expiry is { ScoreRefreshRequired: true } && !HasAffectType(expiry.AffectSnapshot, 13),
            "Player Type 13 expiry did not clear Assalto with a score refresh.");
        var restoredScore = LegacyMobCombatState.Read(expiry!.MobSnapshot).CurrentScore;
        Check(restoredScore.Damage == baselineScore.Damage && restoredScore.Hp == 700 && restoredScore.Mp == 250,
            $"Player Type 13 expiry did not restore baseline damage and resources; damage={restoredScore.Damage}, hp={restoredScore.Hp}, mp={restoredScore.Mp}.");
    }

    private static LegacyCharacterLoginData Project(byte[] mob, byte[] affect) =>
        LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                mob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                affect.ToArray(),
                new byte[LegacyAccountSnapshot.MobExtraStride],
                0,
                0,
                0),
            ItemData);

    private static byte[] CreateMob(int level, int damage, int hp, int mp, short special3)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(level, 0, damage, 0, 0x96, 0, 0,
            1_000, 500, hp, mp, 0, 100, 0, 0, 0, 0, special3, 0);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = 1;
        new LegacyItem(0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        return mob;
    }

    private static LegacySkillDefinition Assalto() =>
        new(11, 78, 0, 47, 0, 0, 0, 0, 0, 0, 13, 7, 15,
            [19, 0, 0, 24, 0, 0, 0, 0], [20, 0, 0, 24, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Assalto");

    private static bool HasAffectType(ReadOnlySpan<byte> affect, byte type)
    {
        for (var offset = 0; offset + 8 <= affect.Length; offset += 8)
            if (affect[offset] == type)
                return true;
        return false;
    }

    private static byte GetAffectValue(ReadOnlySpan<byte> affect, byte type)
    {
        for (var offset = 0; offset + 8 <= affect.Length; offset += 8)
            if (affect[offset] == type)
                return affect[offset + 1];
        return 0;
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

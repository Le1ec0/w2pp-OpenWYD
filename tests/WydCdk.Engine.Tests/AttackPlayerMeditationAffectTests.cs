using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPlayerMeditationAffectTests
{
    private static readonly LegacyItemDataTable ItemData =
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    public static void Run()
    {
        MeditacaoProjectsAndRestoresPlayerScore();
    }

    private static void MeditacaoProjectsAndRestoresPlayerScore()
    {
        var world = new WorldHub(itemData: ItemData);
        var mob = CreateMob(level: 1_500, ac: 100, damage: 500, hp: 700, mp: 250, special2: 50);
        var mobExtra = CreateMobExtra(LegacyAccountSnapshot.ClassMasterMortal);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = Project(mob, emptyAffect, mobExtra);
        var baselineScore = LegacyMobCombatState.Read(baseline.Mob).CurrentScore;

        Check(world.Enter(1, "MEDITATION_PLAYER", static (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(
                  1, 0, 0, 1, 0, 0, 2_000, 2_000, mob,
                  LegacyAccountSnapshot.ClassMasterMortal, emptyAffect, mobExtra),
            "Meditacao player fixture was not installed.");

        var result = world.TryApplySkillAttack(
            1, 1, Meditacao(), ItemData, weather: 0, randomFactor: 100, out var outcome);
        Check(result == LegacySkillAttackResult.Accepted &&
              outcome?.Damage == 0 &&
              outcome.TargetAffectSnapshot is { } firstAffect && HasAffectType(firstAffect, 21),
            "Meditacao did not apply Type 21 to its player caster.");

        var affect = outcome!.TargetAffectSnapshot!;
        var expectedProjection = Project(mob, affect, mobExtra);
        var expectedScore = LegacyMobCombatState.Read(expectedProjection.Mob).CurrentScore;
        var meditationStage = LegacyCurrentScoreMath.CalculateMeditationAffectStage(affect);
        var appliedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        Check(GetAffectValue(affect, 21) == 15 &&
              GetAffectLevel(affect, 21) == 50 &&
              GetAffectTime(affect, 21) == 24 &&
              meditationStage.ArmorClassReduction == 26 &&
              meditationStage.DamageMultiplierAdjustment == 17,
            "Meditacao did not retain the 7.69 Type 21 value, Special2 level, duration, AC reduction, and multiplier formula.");
        Check(appliedScore.Ac == baselineScore.Ac - 26 &&
              appliedScore.Damage == expectedScore.Damage && appliedScore.Damage > baselineScore.Damage,
            $"Meditacao did not project Type 21 AC and target damage multiplier; baseline=({baselineScore.Ac},{baselineScore.Damage}), actual=({appliedScore.Ac},{appliedScore.Damage}), expectedDamage={expectedScore.Damage}.");
        Check(appliedScore.Hp == 700 && appliedScore.Mp == 250,
            "Meditacao changed current HP or MP while applying its self affect.");

        var recastResult = world.TryApplySkillAttack(
            1, 1, Meditacao(), ItemData, weather: 0, randomFactor: 100, out var recastOutcome);
        Check(recastResult == LegacySkillAttackResult.Accepted &&
              recastOutcome?.TargetAffectSnapshot is { } recastAffect &&
              GetAffectTime(recastAffect, 21) == 24 && GetAffectLevel(recastAffect, 21) == 50,
            "Reapplying Meditacao did not preserve its source-derived level and refresh the 24-tick duration.");
        var recastScore = LegacyMobCombatState.Read(recastOutcome!.TargetMobSnapshot).CurrentScore;
        Check(recastScore.Ac == appliedScore.Ac && recastScore.Damage == appliedScore.Damage &&
              recastScore.Hp == 700 && recastScore.Mp == 250,
            "Reapplying Meditacao stacked its Type 21 score effect or changed current resources.");

        LegacyPlayerAffectTimerOutcome? expiry = null;
        for (var tick = 0; tick < 24; tick++)
            expiry = world.TryProcessPlayerTimerAffects(1);
        Check(expiry is { ScoreRefreshRequired: true } && !HasAffectType(expiry.AffectSnapshot, 21),
            "Player Type 21 expiry did not clear Meditacao with a score refresh.");
        var restoredScore = LegacyMobCombatState.Read(expiry!.MobSnapshot).CurrentScore;
        Check(restoredScore.Ac == baselineScore.Ac && restoredScore.Damage == baselineScore.Damage &&
              restoredScore.Hp == 700 && restoredScore.Mp == 250,
            $"Player Type 21 expiry did not restore baseline score/resources; actual=({restoredScore.Ac},{restoredScore.Damage},{restoredScore.Hp},{restoredScore.Mp}).");
    }

    private static LegacyCharacterLoginData Project(byte[] mob, byte[] affect, byte[] mobExtra) =>
        LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                mob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                affect.ToArray(),
                mobExtra.ToArray(),
                0,
                0,
                0),
            ItemData);

    private static byte[] CreateMob(int level, int ac, int damage, int hp, int mp, short special2)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(level, ac, damage, 0, 0x96, 0, 0,
            1_000, 500, hp, mp, 0, 100, 0, 40, 30, special2, 20, 10);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = (byte)3;
        new LegacyItem(0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        return mob;
    }

    private static byte[] CreateMobExtra(short classMaster)
    {
        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteInt16LittleEndian(
            mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset), classMaster);
        return mobExtra;
    }

    private static LegacySkillDefinition Meditacao() =>
        new(77, 87, 0, 90, 6, 0, 0, 0, 0, 0, 21, 15, 15,
            [20, 0, 0, 9, 0, 0, 0, 0], [20, 0, 0, 8, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Meditacao");

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

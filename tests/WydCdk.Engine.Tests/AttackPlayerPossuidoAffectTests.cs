using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPlayerPossuidoAffectTests
{
    private static readonly LegacyItemDataTable ItemData =
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    public static void Run()
    {
        PossuidoProjectsAndRestoresMortalPlayerScore();
        PossuidoProjectsAndRestoresNonMortalPlayerScore();
    }

    private static void PossuidoProjectsAndRestoresMortalPlayerScore() =>
        PossuidoProjectsAndRestoresPlayerScore(LegacyAccountSnapshot.ClassMasterMortal);

    private static void PossuidoProjectsAndRestoresNonMortalPlayerScore() =>
        PossuidoProjectsAndRestoresPlayerScore(classMaster: 0);

    private static void PossuidoProjectsAndRestoresPlayerScore(short classMaster)
    {
        var world = new WorldHub(itemData: ItemData);
        var mob = CreateMob(level: 1_500, hp: 700, mp: 250, special2: 50);
        var baselineProjection = Project(mob, new byte[LegacyAccountSnapshot.AffectStride], classMaster);
        var baselineScore = LegacyMobCombatState.Read(baselineProjection.Mob).CurrentScore;

        Check(world.Enter(1, $"POSSUIDO_PLAYER_{classMaster}", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(
                  1,
                  0,
                  0,
                  1,
                  0,
                  0,
                  2_000,
                  2_000,
                  mob,
                  classMaster,
                  new byte[LegacyAccountSnapshot.AffectStride],
                  CreateMobExtra(classMaster)),
            "Possuído player fixture was not installed.");

        var result = world.TryApplySkillAttack(
            1, 1, Possuido(), ItemData, weather: 0, randomFactor: 100, out var outcome);
        Check(result == LegacySkillAttackResult.Accepted &&
              outcome?.TargetAffectSnapshot is { } firstAffect && HasAffectType(firstAffect, 14),
            "Possuído did not apply Type 14 to its player caster.");

        var affect = outcome!.TargetAffectSnapshot!;
        var expectedProjection = Project(mob, affect, classMaster);
        var expectedScore = LegacyMobCombatState.Read(expectedProjection.Mob).CurrentScore;
        var possessedStage = LegacyCurrentScoreMath.CalculatePossessedAffectStage(
            affect, classMaster, baselineScore.MaxHp, baselineScore.Constitution);
        var appliedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        var classMultiplier = classMaster is LegacyAccountSnapshot.ClassMasterMortal or LegacyAccountSnapshot.ClassMasterArch
            ? 2
            : 3;
        var expectedValue = ((50 * 3 / 4) + 10) * classMultiplier;

        Check(GetAffectValue(affect, 14) == 10 &&
              GetAffectLevel(affect, 14) == 50 &&
              GetAffectTime(affect, 14) == 24 &&
              possessedStage.MaxHp == baselineScore.MaxHp + expectedValue * 2 &&
              possessedStage.Constitution == baselineScore.Constitution + expectedValue,
            "Possuído did not retain the source-derived Type 14 value, level, duration, and class-master score formula.");
        Check(appliedScore.MaxHp == expectedScore.MaxHp && appliedScore.Constitution == expectedScore.Constitution &&
              appliedScore.MaxHp == possessedStage.MaxHp && appliedScore.Constitution == possessedStage.Constitution,
            $"Possuído did not project 7.69 MaxHp/Constitution; classMaster={classMaster}, actual=({appliedScore.MaxHp},{appliedScore.Constitution}), expected=({possessedStage.MaxHp},{possessedStage.Constitution}).");
        Check(appliedScore.Hp == 700 && appliedScore.Mp == 250 && outcome.Damage == 0,
            "Possuído changed current HP/MP or reported direct damage while applying its self affect.");

        var recastResult = world.TryApplySkillAttack(
            1, 1, Possuido(), ItemData, weather: 0, randomFactor: 100, out var recastOutcome);
        Check(recastResult == LegacySkillAttackResult.Accepted &&
              recastOutcome?.TargetAffectSnapshot is { } recastAffect &&
              GetAffectTime(recastAffect, 14) == 24,
            "Reapplying Possuído did not refresh its source-derived duration.");
        var recastScore = LegacyMobCombatState.Read(recastOutcome!.TargetMobSnapshot).CurrentScore;
        Check(recastScore.MaxHp == possessedStage.MaxHp && recastScore.Constitution == possessedStage.Constitution &&
              recastScore.Hp == 700 && recastScore.Mp == 250,
            "Reapplying Possuído stacked its score deltas or changed current resources.");

        LegacyPlayerAffectTimerOutcome? expiry = null;
        for (var tick = 0; tick < 24; tick++)
            expiry = world.TryProcessPlayerTimerAffects(1);
        Check(expiry is { ScoreRefreshRequired: true } && !HasAffectType(expiry.AffectSnapshot, 14),
            "Player Type 14 expiry did not clear Possuído with a score refresh.");
        var restoredScore = LegacyMobCombatState.Read(expiry!.MobSnapshot).CurrentScore;
        Check(restoredScore.MaxHp == baselineScore.MaxHp &&
              restoredScore.Constitution == baselineScore.Constitution &&
              restoredScore.Hp == 700 && restoredScore.Mp == 250,
            $"Player Type 14 expiry did not restore baseline MaxHp/Constitution/resources; classMaster={classMaster}, actual=({restoredScore.MaxHp},{restoredScore.Constitution},{restoredScore.Hp},{restoredScore.Mp}).");
    }

    private static LegacyCharacterLoginData Project(byte[] mob, byte[] affect, short classMaster)
    {
        return LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                mob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                affect.ToArray(),
                CreateMobExtra(classMaster),
                0,
                0,
                0),
            ItemData);
    }

    private static byte[] CreateMobExtra(short classMaster)
    {
        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteInt16LittleEndian(mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset), classMaster);
        return mobExtra;
    }

    private static byte[] CreateMob(int level, int hp, int mp, short special2)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(level, 0, 500, 0, 0x96, 0, 0,
            1_000, 500, hp, mp, 0, 100, 0, 40, 0, special2, 0, 0);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = 1;
        new LegacyItem(0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        return mob;
    }

    private static LegacySkillDefinition Possuido() =>
        new(3, 84, 0, 105, 0, 0, 0, 0, 0, 0, 14, 10, 15,
            [24, 0, 0, 24, 0, 0, 0, 0], [24, 0, 0, 24, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Possuído");

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

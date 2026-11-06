using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPlayerSamaritanAffectTests
{
    private static readonly LegacyItemDataTable ItemData =
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    public static void Run()
    {
        SamaritanProjectsRefreshesAndRestoresPlayerScore();
    }

    private static void SamaritanProjectsRefreshesAndRestoresPlayerScore()
    {
        var world = new WorldHub(itemData: ItemData);
        var mob = CreateMob();
        var mobExtra = CreateMobExtra(LegacyAccountSnapshot.ClassMasterMortal);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = Project(mob, emptyAffect, mobExtra);
        var baselineScore = LegacyMobCombatState.Read(baseline.Mob).CurrentScore;

        Check(world.Enter(1, "SAMARITAN_PLAYER", static (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(
                  1, 0, 0, 1, 0, 0, 2_000, 2_000, mob,
                  LegacyAccountSnapshot.ClassMasterMortal, emptyAffect, mobExtra),
            "The Samaritan player fixture was not installed.");

        var skill = Samaritan();
        var casterState = LegacyMobCombatState.Read(mob);
        var expectedLevel = LegacySkillCombatMath.GetSkillSpecial(skill, casterState);
        var expectedDelay = LegacySkillCombatMath.GetSkillAffectDelay(
            skill, casterState, LegacyAccountSnapshot.ClassMasterMortal);
        var expectedDuration = checked(expectedDelay * (skill.AffectTime + 1) / 100);

        var result = world.TryApplySkillAttack(
            1, 1, skill, ItemData, weather: 0, randomFactor: 100, out var outcome);
        Check(result == LegacySkillAttackResult.Accepted &&
              outcome?.Damage == 0 &&
              outcome.TargetAffectSnapshot is { } firstAffect && HasAffectType(firstAffect, 24),
            "Samaritano did not apply Type 24 to its player caster.");

        var affect = outcome!.TargetAffectSnapshot!;
        var appliedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        var expectedAc = LegacyCurrentScoreMath.CalculateSamaritanArmorClassAffectStage(
            affect, baselineScore.Ac).ArmorClass;
        Check(GetAffectValue(affect, 24) == 0 &&
              GetAffectLevel(affect, 24) == expectedLevel &&
              GetAffectTime(affect, 24) == expectedDuration &&
              appliedScore.Ac == expectedAc && appliedScore.Ac > baselineScore.Ac,
            $"Samaritano did not retain the source row and project Type 24 AC; baseline={baselineScore.Ac}, actual={appliedScore.Ac}, expected={expectedAc}.");
        Check(appliedScore.Hp == 700 && appliedScore.Mp == 250,
            "Samaritano changed current HP or MP while applying its self affect.");

        var recastResult = world.TryApplySkillAttack(
            1, 1, skill, ItemData, weather: 0, randomFactor: 100, out var recastOutcome);
        Check(recastResult == LegacySkillAttackResult.Accepted &&
              recastOutcome?.TargetAffectSnapshot is { } recastAffect &&
              CountAffectType(recastAffect, 24) == 1 &&
              GetAffectTime(recastAffect, 24) == expectedDuration,
            "Reapplying Samaritano did not refresh its single Type 24 record.");
        var recastScore = LegacyMobCombatState.Read(recastOutcome!.TargetMobSnapshot).CurrentScore;
        Check(recastScore.Ac == appliedScore.Ac && recastScore.Hp == 700 && recastScore.Mp == 250,
            "Reapplying Samaritano stacked Type 24 or changed current resources.");

        LegacyPlayerAffectTimerOutcome? expiry = null;
        for (var tick = 0; tick < expectedDuration; tick++)
            expiry = world.TryProcessPlayerTimerAffects(1);
        Check(expiry is { ScoreRefreshRequired: true } && !HasAffectType(expiry.AffectSnapshot, 24),
            "Player Type 24 expiry did not clear Samaritano with a score refresh.");
        var restoredScore = LegacyMobCombatState.Read(expiry!.MobSnapshot).CurrentScore;
        Check(restoredScore.Ac == baselineScore.Ac && restoredScore.Hp == 700 && restoredScore.Mp == 250,
            $"Player Type 24 expiry did not restore baseline AC/resources; actual=({restoredScore.Ac},{restoredScore.Hp},{restoredScore.Mp}).");
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

    private static byte[] CreateMob()
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(1_500, 100, 500, 0, 0x96, 0, 0,
            1_000, 500, 700, 250, 0, 100, 0, 40, 30, 50, 20, 10);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = 3;
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

    private static LegacySkillDefinition Samaritan() =>
        new(13, 72, 0, 25, 1, 5, 0, 0, 0, 0, 24, 0, 45,
            [7, 0, 0, 8, 0, 0, 0, 0], [6, 0, 0, 8, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Samaritano");

    private static bool HasAffectType(ReadOnlySpan<byte> affect, byte type) =>
        CountAffectType(affect, type) != 0;

    private static int CountAffectType(ReadOnlySpan<byte> affect, byte type)
    {
        var count = 0;
        for (var offset = 0; offset + 8 <= affect.Length; offset += 8)
            if (affect[offset] == type)
                count++;
        return count;
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

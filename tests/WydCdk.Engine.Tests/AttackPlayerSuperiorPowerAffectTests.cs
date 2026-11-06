using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPlayerSuperiorPowerAffectTests
{
    private static readonly LegacyItemDataTable ItemData =
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    public static void Run()
    {
        SuperiorPowerProjectsRefreshesAndRestoresPlayerScore();
    }

    private static void SuperiorPowerProjectsRefreshesAndRestoresPlayerScore()
    {
        var world = new WorldHub(itemData: ItemData);
        var mob = CreateMob();
        var mobExtra = CreateMobExtra(LegacyAccountSnapshot.ClassMasterMortal);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var sourceMobMagic = LegacyMobCombatState.Read(mob).Magic;
        var baseline = Project(mob, emptyAffect, mobExtra);
        var baselineState = LegacyMobCombatState.Read(baseline.Mob);
        var baselineScore = baselineState.CurrentScore;

        Check(world.Enter(1, "SUPERIOR_POWER_PLAYER", static (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(
                  1, 0, 0, 1, 0, 0, 2_000, 2_000, baseline.Mob,
                  LegacyAccountSnapshot.ClassMasterMortal, emptyAffect, mobExtra,
                  sourceMobMagic: sourceMobMagic),
            "The Superior Power player fixture was not installed.");

        var skill = SuperiorPower();
        var expectedLevel = LegacySkillCombatMath.GetSkillSpecial(skill, LegacyMobCombatState.Read(mob));
        var expectedDelay = LegacySkillCombatMath.GetSkillAffectDelay(
            skill, LegacyMobCombatState.Read(mob), LegacyAccountSnapshot.ClassMasterMortal);
        var expectedDuration = checked(expectedDelay * (skill.AffectTime + 1) / 100);

        var result = world.TryApplySkillAttack(
            1, 1, skill, ItemData, weather: 0, randomFactor: 100, out var outcome);
        Check(result == LegacySkillAttackResult.Accepted &&
              outcome?.Damage == 0 &&
              outcome.TargetAffectSnapshot is { } firstAffect && HasAffectType(firstAffect, 4),
            "Poder Superior did not apply Type 4 to its player caster.");

        var affect = outcome!.TargetAffectSnapshot!;
        var appliedState = LegacyMobCombatState.Read(outcome.TargetMobSnapshot);
        var appliedScore = appliedState.CurrentScore;
        Check(GetAffectValue(affect, 4) == 1 &&
              GetAffectLevel(affect, 4) == expectedLevel &&
              GetAffectTime(affect, 4) == expectedDuration,
            $"Poder Superior did not retain the source row's Type 4 value, level, and duration: value={GetAffectValue(affect, 4)}, level={GetAffectLevel(affect, 4)}, time={GetAffectTime(affect, 4)}.");

        var expectedMaxHp = baselineScore.MaxHp + baselineScore.MaxHp * 15 / 100;
        var expectedMaxMp = baselineScore.MaxMp + baselineScore.MaxMp * 10 / 100;
        var sourceBaseDamage = LegacyMobCombatState.Read(mob).BaseScore.Damage;
        var expectedDamage = baselineScore.Damage + sourceBaseDamage * 30 / 100;
        var expectedMagic = baselineState.Magic + sourceMobMagic * 20 / 100;
        Check(appliedScore.MaxHp == expectedMaxHp &&
              appliedScore.MaxMp == expectedMaxMp &&
              appliedScore.Damage == expectedDamage &&
              appliedState.Magic == expectedMagic,
            $"Poder Superior did not project the 7.69 Type 4 score deltas: expected HP/MP/Damage/Magic=({expectedMaxHp},{expectedMaxMp},{expectedDamage},{expectedMagic}), actual=({appliedScore.MaxHp},{appliedScore.MaxMp},{appliedScore.Damage},{appliedState.Magic}).");
        Check(appliedScore.Hp == baselineScore.Hp && appliedScore.Mp == baselineScore.Mp,
            "Poder Superior changed current HP or MP while applying its self affect.");

        var recastResult = world.TryApplySkillAttack(
            1, 1, skill, ItemData, weather: 0, randomFactor: 100, out var recastOutcome);
        Check(recastResult == LegacySkillAttackResult.Accepted &&
              recastOutcome?.TargetAffectSnapshot is { } recastAffect &&
              CountAffectType(recastAffect, 4) == 1 &&
              GetAffectTime(recastAffect, 4) == expectedDuration,
            "Reapplying Poder Superior did not refresh its single Type 4 record.");
        var recastState = LegacyMobCombatState.Read(recastOutcome!.TargetMobSnapshot);
        var recastScore = recastState.CurrentScore;
        Check(recastScore.MaxHp == appliedScore.MaxHp &&
              recastScore.MaxMp == appliedScore.MaxMp &&
              recastScore.Damage == appliedScore.Damage &&
              recastState.Magic == appliedState.Magic &&
              recastScore.Hp == baselineScore.Hp && recastScore.Mp == baselineScore.Mp,
            "Reapplying Poder Superior stacked Type 4 or changed current resources.");

        LegacyPlayerAffectTimerOutcome? expiry = null;
        for (var tick = 0; tick < expectedDuration; tick++)
            expiry = world.TryProcessPlayerTimerAffects(1);
        Check(expiry is { ScoreRefreshRequired: true } && !HasAffectType(expiry.AffectSnapshot, 4),
            "Player Type 4 expiry did not clear Poder Superior with a score refresh.");
        var restoredState = LegacyMobCombatState.Read(expiry!.MobSnapshot);
        var restoredScore = restoredState.CurrentScore;
        Check(restoredScore.MaxHp == baselineScore.MaxHp &&
              restoredScore.MaxMp == baselineScore.MaxMp &&
              restoredScore.Damage == baselineScore.Damage &&
              restoredState.Magic == baselineState.Magic &&
              restoredScore.Hp == baselineScore.Hp && restoredScore.Mp == baselineScore.Mp,
            "Player Type 4 expiry did not restore the baseline score and resources.");
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
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobMagicOffset), 500);
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

    private static LegacySkillDefinition SuperiorPower() =>
        new(96, 33, 0, 72, 2_040, 0, 0, 0, 0, 0, 4, 1, 102,
            [8, 0, 0, 8, 0, 0, 0, 0], [8, 0, 0, 8, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Poder_Superior");

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

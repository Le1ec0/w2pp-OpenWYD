using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPlayerSoulSwapAffectTests
{
    private static readonly LegacyItemDataTable ItemData =
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    public static void Run()
    {
        Type42UsesClassMasterLevelAndAffectLevelInLoginProjection();
        Skill216ProjectsRecastsAndExpiresType42ForPlayerSelfTargets();
    }

    private static void Type42UsesClassMasterLevelAndAffectLevelInLoginProjection()
    {
        var mob = CreateMob(level: 200, hp: 700, mp: 250);
        foreach (var classMaster in new short[]
                 {
                     (short)LegacyAccountSnapshot.ClassMasterArch,
                     (short)LegacyAccountSnapshot.ClassMasterMortal,
                     3,
                 })
        {
            var mobExtra = CreateMobExtra(classMaster);
            var baseline = Project(mob, new byte[LegacyAccountSnapshot.AffectStride], mobExtra);
            var baselineScore = LegacyMobCombatState.Read(baseline.Mob).CurrentScore;
            const int affectLevel = 40;
            var affect = CreateType42Affect(affectLevel);
            var projected = Project(mob, affect, mobExtra);
            var actual = LegacyMobCombatState.Read(projected.Mob).CurrentScore;
            var effectiveLevel = classMaster is LegacyAccountSnapshot.ClassMasterArch or LegacyAccountSnapshot.ClassMasterMortal
                ? baselineScore.Level
                : baselineScore.Level + LegacyCurrentScoreMath.TargetMaximumLevel;
            var expectedTransfer = baselineScore.MaxMp / 5 + (effectiveLevel + affectLevel) / 2;

            Check(actual.MaxHp == baselineScore.MaxHp + expectedTransfer &&
                  actual.MaxMp == baselineScore.MaxMp - expectedTransfer,
                $"Type 42 did not use class-master level and affect level; classMaster={classMaster}, expected transfer={expectedTransfer}, actual MaxHP/MaxMP=({actual.MaxHp},{actual.MaxMp}).");
            Check(actual.Hp == baselineScore.Hp && actual.Mp == baselineScore.Mp,
                "Type 42 changed current HP/MP while projecting maximum resources.");
        }
    }

    private static void Skill216ProjectsRecastsAndExpiresType42ForPlayerSelfTargets()
    {
        var world = new WorldHub(itemData: ItemData);
        var mob = CreateMob(level: 200, hp: 700, mp: 250);
        var mobExtra = CreateMobExtra(LegacyAccountSnapshot.ClassMasterMortal);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = Project(mob, emptyAffect, mobExtra);
        var baselineScore = LegacyMobCombatState.Read(baseline.Mob).CurrentScore;

        Check(world.Enter(1, "SOUL_SWAP_PLAYER", static (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(
                  1, 0, 0, 1, 0, 0, 2_000, 2_000, mob,
                  LegacyAccountSnapshot.ClassMasterMortal, emptyAffect, mobExtra),
            "Magia Misteriosa player fixture was not installed.");

        var result = world.TryApplySkillAttack(1, 1, MagiaMisteriosa(), ItemData, weather: 0, randomFactor: 100, out var outcome);
        Check(result == LegacySkillAttackResult.Accepted && outcome?.Damage == 0 &&
              outcome.TargetAffectSnapshot is { } firstAffect && HasAffectType(firstAffect, 42),
            "Skill 216 did not apply Type 42 to its player caster.");

        var affect = outcome!.TargetAffectSnapshot!;
        var appliedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        var affectLevel = GetAffectLevel(affect, 42);
        var expectedTransfer = baselineScore.MaxMp / 5 + (baselineScore.Level + affectLevel) / 2;
        Check(appliedScore.MaxHp == baselineScore.MaxHp + expectedTransfer &&
              appliedScore.MaxMp == baselineScore.MaxMp - expectedTransfer &&
              appliedScore.Hp == 700 && appliedScore.Mp == 250,
            $"Skill 216 did not project Type 42 MaxHP/MaxMP while preserving current HP/MP; expected transfer={expectedTransfer}, actual=({appliedScore.MaxHp},{appliedScore.MaxMp},{appliedScore.Hp},{appliedScore.Mp}).");

        var recastResult = world.TryApplySkillAttack(1, 1, MagiaMisteriosa(), ItemData, weather: 0, randomFactor: 100, out var recastOutcome);
        var recastAffect = recastOutcome?.TargetAffectSnapshot;
        Check(recastResult == LegacySkillAttackResult.Accepted &&
              recastAffect is not null && HasAffectType(recastAffect, 42),
            "Recasting skill 216 did not refresh Type 42.");
        var recastScore = LegacyMobCombatState.Read(recastOutcome!.TargetMobSnapshot).CurrentScore;
        Check(recastScore.MaxHp == appliedScore.MaxHp && recastScore.MaxMp == appliedScore.MaxMp &&
              recastScore.Hp == 700 && recastScore.Mp == 250,
            "Recasting skill 216 stacked Type 42 or changed current HP/MP.");

        LegacyPlayerAffectTimerOutcome? expiry = null;
        var duration = GetAffectTime(recastAffect!, 42);
        for (var tick = 0; tick < duration; tick++)
            expiry = world.TryProcessPlayerTimerAffects(1);
        Check(expiry is { ScoreRefreshRequired: true } && !HasAffectType(expiry.AffectSnapshot, 42),
            "Type 42 expiry did not clear Magia Misteriosa with a score refresh.");
        var restoredScore = LegacyMobCombatState.Read(expiry!.MobSnapshot).CurrentScore;
        Check(restoredScore.MaxHp == baselineScore.MaxHp && restoredScore.MaxMp == baselineScore.MaxMp &&
              restoredScore.Hp == 700 && restoredScore.Mp == 250,
            $"Type 42 expiry did not restore baseline MaxHP/MaxMP/current HP/MP; baseline=({baselineScore.MaxHp},{baselineScore.MaxMp},700,250), actual=({restoredScore.MaxHp},{restoredScore.MaxMp},{restoredScore.Hp},{restoredScore.Mp}).");
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

    private static byte[] CreateMob(int level, int hp, int mp)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(level, 100, 500, 0, 0x96, 0, 0,
            1_000, 1_001, hp, mp, 0, 100, 0, 40, 30, 20, 10, 10);
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

    private static byte[] CreateType42Affect(int level)
    {
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = 42;
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(2), checked((ushort)level));
        return affect;
    }

    private static LegacySkillDefinition MagiaMisteriosa() =>
        new(216, 325, 0, 125, 150, 0, 0, 0, 0, 0, 42, 0, 750,
            [24, 0, 0, 24, 0, 0, 0, 0], [24, 0, 0, 24, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Magia Misteriosa");

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

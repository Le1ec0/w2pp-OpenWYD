using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPlayerAthenaTouchAffectTests
{
    private static readonly LegacyItemDataTable ItemData =
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    public static void Run()
    {
        ToqueDeAthenaProjectsAndRestoresPlayerSpecials();
    }

    private static void ToqueDeAthenaProjectsAndRestoresPlayerSpecials()
    {
        var world = new WorldHub(itemData: ItemData);
        var mob = CreateMob(level: 1_500, hp: 700, mp: 250, special4: 50);
        var mobExtra = CreateMobExtra(LegacyAccountSnapshot.ClassMasterMortal);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = Project(mob, emptyAffect, mobExtra);
        var baselineScore = LegacyMobCombatState.Read(baseline.Mob).CurrentScore;

        Check(baselineScore.Special1 == 250 && baselineScore.Special2 == 247 &&
              baselineScore.Special3 == 254 && baselineScore.Special4 == 50,
            "Toque de Athena player baseline fixture did not preserve the four source Specials.");
        Check(world.Enter(1, "ATHENA_CASTER", static (_, _) => ValueTask.CompletedTask) &&
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
                  LegacyAccountSnapshot.ClassMasterMortal,
                  emptyAffect,
                  mobExtra),
            "Toque de Athena player fixture was not installed.");

        var result = world.TryApplySkillAttack(
            1, 1, ToqueDeAthena(), ItemData, weather: 0, randomFactor: 100, out var outcome);
        Check(result == LegacySkillAttackResult.Accepted &&
              outcome?.Damage == 0 &&
              outcome.TargetAffectSnapshot is { } firstAffect && HasAffectType(firstAffect, 15),
            "Toque de Athena did not apply Type 15 to its caster.");

        var affect = outcome!.TargetAffectSnapshot!;
        var appliedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        Check(GetAffectValue(affect, 15) == 7 &&
              GetAffectLevel(affect, 15) == 50 &&
              GetAffectTime(affect, 15) == 24,
            "Toque de Athena did not retain its SkillData value, Special4 level, and 24-tick source duration.");
        Check(appliedScore.Special1 == 255 && appliedScore.Special2 == 255 &&
              appliedScore.Special3 == 255 && appliedScore.Special4 == 62 &&
              appliedScore.Hp == 700 && appliedScore.Mp == 250,
            $"Toque de Athena did not apply the 7.69 Level/10 + Value projection with four 255 caps; actual=({appliedScore.Special1},{appliedScore.Special2},{appliedScore.Special3},{appliedScore.Special4},{appliedScore.Hp},{appliedScore.Mp}).");

        for (var tick = 0; tick < 5; tick++)
            world.TryProcessPlayerTimerAffects(1);

        var recastResult = world.TryApplySkillAttack(
            1, 1, ToqueDeAthena(), ItemData, weather: 0, randomFactor: 100, out var recastOutcome);
        var recastAffectTime = recastOutcome?.TargetAffectSnapshot is { } recastAffectSnapshot
            ? GetAffectTime(recastAffectSnapshot, 15)
            : 0;
        var recastAffectLevel = recastOutcome?.TargetAffectSnapshot is { } recastLevelSnapshot
            ? GetAffectLevel(recastLevelSnapshot, 15)
            : 0;
        Check(recastResult == LegacySkillAttackResult.Accepted &&
              recastAffectTime == 25 && recastAffectLevel == 62,
            $"Reapplying Toque de Athena did not refresh its 7.69 duration and current Special4 level; result={recastResult}, outcome={recastOutcome is not null}, level={recastAffectLevel}, affectTime={recastAffectTime}.");
        var recastScore = LegacyMobCombatState.Read(recastOutcome!.TargetMobSnapshot).CurrentScore;
        Check(recastScore.Special1 == 255 && recastScore.Special2 == 255 &&
              recastScore.Special3 == 255 && recastScore.Special4 == 63 &&
              recastScore.Hp == 700 && recastScore.Mp == 250,
            $"Reapplying Toque de Athena stacked its Special bonus or changed current resources; actual=({recastScore.Special1},{recastScore.Special2},{recastScore.Special3},{recastScore.Special4},{recastScore.Hp},{recastScore.Mp}).");

        LegacyPlayerAffectTimerOutcome? expiry = null;
        for (var tick = 0; tick < 25; tick++)
            expiry = world.TryProcessPlayerTimerAffects(1);
        Check(expiry is { ScoreRefreshRequired: true } && !HasAffectType(expiry.AffectSnapshot, 15),
            "Player Type 15 expiry did not clear Toque de Athena with a score refresh.");
        var restoredScore = LegacyMobCombatState.Read(expiry!.MobSnapshot).CurrentScore;
        Check(restoredScore.Special1 == 250 && restoredScore.Special2 == 247 &&
              restoredScore.Special3 == 254 && restoredScore.Special4 == 50 &&
              restoredScore.Hp == 700 && restoredScore.Mp == 250,
            $"Player Type 15 expiry did not restore baseline Specials/resources; actual=({restoredScore.Special1},{restoredScore.Special2},{restoredScore.Special3},{restoredScore.Special4},{restoredScore.Hp},{restoredScore.Mp}).");
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

    private static byte[] CreateMob(int level, int hp, int mp, short special4)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(level, 100, 500, 0, 0x96, 0, 0,
            1_000, 500, hp, mp, 0, 100, 0, 40, 250, 247, 254, special4);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = 1;
        new LegacyItem(0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        return mob;
    }

    private static byte[] CreateMobExtra(short classMaster)
    {
        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteInt16LittleEndian(mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset), classMaster);
        return mobExtra;
    }

    private static LegacySkillDefinition ToqueDeAthena() =>
        new(45, 72, 2, 98, 4, 6, 0, 0, 0, 0, 15, 7, 15,
            [19, 0, 0, 24, 0, 0, 0, 0], [19, 0, 0, 8, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Toque de Athena");

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

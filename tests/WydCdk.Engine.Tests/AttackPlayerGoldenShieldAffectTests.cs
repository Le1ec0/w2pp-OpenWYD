using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPlayerGoldenShieldAffectTests
{
    private static readonly LegacyItemDataTable ItemData =
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    public static void Run()
    {
        ExplosaoEtereaProjectsAndRestoresPlayerArmorClass();
    }

    private static void ExplosaoEtereaProjectsAndRestoresPlayerArmorClass()
    {
        var world = new WorldHub(itemData: ItemData);
        var mob = CreateMob(level: 1_500, ac: 100, hp: 700, mp: 250, special3: 50);
        var mobExtra = CreateMobExtra(LegacyAccountSnapshot.ClassMasterMortal);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = Project(mob, emptyAffect, mobExtra);
        var baselineScore = LegacyMobCombatState.Read(baseline.Mob).CurrentScore;

        Check(world.Enter(1, "GOLDEN_SHIELD_PLAYER", static (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(
                  1, 0, 0, 1, 0, 0, 2_000, 2_000, mob,
                  LegacyAccountSnapshot.ClassMasterMortal, emptyAffect, mobExtra),
            "Explosao Eterea player fixture was not installed.");

        var result = world.TryApplySkillAttack(
            1, 1, ExplosaoEterea(), ItemData, weather: 0, randomFactor: 100, out var outcome);
        Check(result == LegacySkillAttackResult.Accepted && outcome?.Damage == 0 &&
              outcome.TargetAffectSnapshot is { } firstAffect && HasAffectType(firstAffect, 31),
            "Explosao Eterea did not apply Type 31 to its player caster.");

        var affect = outcome!.TargetAffectSnapshot!;
        var appliedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        Check(GetAffectValue(affect, 31) == 150 && GetAffectLevel(affect, 31) == 50 &&
              GetAffectTime(affect, 31) == 12,
            $"Explosao Eterea did not preserve the 7.69 Type 31 value, Special3 level, and duration: value={GetAffectValue(affect, 31)}, level={GetAffectLevel(affect, 31)}, time={GetAffectTime(affect, 31)}.");
        Check(appliedScore.Ac == baselineScore.Ac + 175 &&
              appliedScore.Hp == 700 && appliedScore.Mp == 250,
            $"Explosao Eterea did not project Type 31 AC or preserve HP/MP; expected AC={baselineScore.Ac + 175}, actual=({appliedScore.Ac},{appliedScore.Hp},{appliedScore.Mp}).");

        var recastResult = world.TryApplySkillAttack(
            1, 1, ExplosaoEterea(), ItemData, weather: 0, randomFactor: 100, out var recastOutcome);
        Check(recastResult == LegacySkillAttackResult.Accepted &&
              recastOutcome?.TargetAffectSnapshot is { } recastAffect &&
              GetAffectTime(recastAffect, 31) == 12 && GetAffectLevel(recastAffect, 31) == 50,
            "Reapplying Explosao Eterea did not refresh its source-derived Type 31 affect.");
        var recastScore = LegacyMobCombatState.Read(recastOutcome!.TargetMobSnapshot).CurrentScore;
        Check(recastScore.Ac == appliedScore.Ac && recastScore.Hp == 700 && recastScore.Mp == 250,
            "Reapplying Explosao Eterea stacked Type 31 AC or changed current resources.");

        LegacyPlayerAffectTimerOutcome? expiry = null;
        for (var tick = 0; tick < 12; tick++)
            expiry = world.TryProcessPlayerTimerAffects(1);
        Check(expiry is { ScoreRefreshRequired: true } && !HasAffectType(expiry.AffectSnapshot, 31),
            "Player Type 31 expiry did not clear Explosao Eterea with a score refresh.");
        var restoredScore = LegacyMobCombatState.Read(expiry!.MobSnapshot).CurrentScore;
        Check(restoredScore.Ac == baselineScore.Ac && restoredScore.Hp == 700 && restoredScore.Mp == 250,
            "Player Type 31 expiry did not restore baseline score and resources.");
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

    private static byte[] CreateMob(int level, int ac, int hp, int mp, short special3)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(level, ac, 500, 0, 0x96, 0, 0,
            1_000, 500, hp, mp, 0, 100, 0, 40, 30, 20, special3, 10);
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

    private static LegacySkillDefinition ExplosaoEterea() =>
        new(85, 90, 0, 120, 5, 0, 0, 0, 0, 0, 31, 150, 7,
            [20, 0, 0, 24, 0, 0, 0, 0], [20, 0, 0, 8, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Explosao Eterea");

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

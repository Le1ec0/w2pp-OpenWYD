using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPlayerMagicShieldAffectTests
{
    private static readonly LegacyItemDataTable ItemData =
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    public static void Run()
    {
        EscudoMagicoProjectsAndRestoresPlayerArmorClass();
    }

    private static void EscudoMagicoProjectsAndRestoresPlayerArmorClass()
    {
        var world = new WorldHub(itemData: ItemData);
        var mob = CreateMob(level: 1_500, hp: 700, mp: 250, special4: 50);
        var mobExtra = CreateMobExtra(LegacyAccountSnapshot.ClassMasterMortal);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baselineProjection = Project(mob, emptyAffect, mobExtra);
        var baselineScore = LegacyMobCombatState.Read(baselineProjection.Mob).CurrentScore;

        Check(world.Enter(1, "MAGIC_SHIELD_PLAYER", (_, _) => ValueTask.CompletedTask) &&
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
            "Escudo Mágico player fixture was not installed.");

        var result = world.TryApplySkillAttack(
            1, 1, EscudoMagico(), ItemData, weather: 0, randomFactor: 100, out var outcome);
        Check(result == LegacySkillAttackResult.Accepted &&
              outcome?.Damage == 0 &&
              outcome.TargetAffectSnapshot is { } firstAffect && HasAffectType(firstAffect, 11),
            "Escudo Mágico did not apply Type 11 to the player target.");

        var affect = outcome!.TargetAffectSnapshot!;
        var expectedProjection = Project(mob, affect, mobExtra);
        var expectedScore = LegacyMobCombatState.Read(expectedProjection.Mob).CurrentScore;
        var shieldStage = LegacyCurrentScoreMath.CalculateMagicShieldAffectStage(affect);
        var appliedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        Check(GetAffectValue(affect, 11) == 15 &&
              GetAffectLevel(affect, 11) == 50 &&
              GetAffectTime(affect, 11) == 24 &&
              shieldStage.ArmorClassAddition == 25 &&
              expectedScore.Ac == baselineScore.Ac + 25,
            "Escudo Mágico did not retain the 7.69 Type 11 value, Special4 level, duration, and Level/5 AC delta.");
        Check(appliedScore.Ac == expectedScore.Ac &&
              appliedScore.Hp == 700 && appliedScore.Mp == 250 &&
              outcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset, LegacyScore.SizeInBytes)
                  .SequenceEqual(expectedProjection.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset, LegacyScore.SizeInBytes)),
            $"Escudo Mágico did not project 7.69 player score and preserve resources; baseline AC={baselineScore.Ac}, actual AC={appliedScore.Ac}, expected AC={expectedScore.Ac}.");

        var recastResult = world.TryApplySkillAttack(
            1, 1, EscudoMagico(), ItemData, weather: 0, randomFactor: 100, out var recastOutcome);
        Check(recastResult == LegacySkillAttackResult.Accepted &&
              recastOutcome?.TargetAffectSnapshot is { } recastAffect &&
              GetAffectTime(recastAffect, 11) == 24,
            "Reapplying Escudo Mágico did not refresh its source-derived duration.");
        var recastScore = LegacyMobCombatState.Read(recastOutcome!.TargetMobSnapshot).CurrentScore;
        Check(recastScore.Ac == expectedScore.Ac && recastScore.Hp == 700 && recastScore.Mp == 250,
            "Reapplying Escudo Mágico stacked Type 11 AC or changed current resources.");

        LegacyPlayerAffectTimerOutcome? expiry = null;
        for (var tick = 0; tick < 24; tick++)
            expiry = world.TryProcessPlayerTimerAffects(1);
        Check(expiry is { ScoreRefreshRequired: true } && !HasAffectType(expiry.AffectSnapshot, 11),
            "Player Type 11 expiry did not clear Escudo Mágico with a score refresh.");
        var restoredScore = LegacyMobCombatState.Read(expiry!.MobSnapshot).CurrentScore;
        Check(restoredScore.Ac == baselineScore.Ac && restoredScore.Hp == 700 && restoredScore.Mp == 250,
            $"Player Type 11 expiry did not restore baseline AC/resources; actual=({restoredScore.Ac},{restoredScore.Hp},{restoredScore.Mp}), baseline=({baselineScore.Ac},700,250).");
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
            1_000, 500, hp, mp, 0, 100, 0, 40, 0, 0, 0, special4);
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

    private static LegacySkillDefinition EscudoMagico() =>
        new(43, 63, 2, 52, 1, 6, 0, 0, 0, 0, 11, 15, 15,
            [24, 0, 0, 24, 0, 0, 0, 0], [9, 0, 0, 6, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Escudo Mágico");

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

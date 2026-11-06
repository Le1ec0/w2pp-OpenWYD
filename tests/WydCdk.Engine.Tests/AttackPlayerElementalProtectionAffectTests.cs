using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPlayerElementalProtectionAffectTests
{
    private static readonly LegacyItemDataTable ItemData =
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    public static void Run()
    {
        ProtecaoElementalProjectsAndRestoresPlayerScore();
    }

    private static void ProtecaoElementalProjectsAndRestoresPlayerScore()
    {
        var world = new WorldHub(itemData: ItemData);
        var mob = CreateMob(level: 1_500, ac: 100, damage: 500, hp: 700, mp: 250, special2: 50);
        var mobExtra = CreateMobExtra(LegacyAccountSnapshot.ClassMasterMortal);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = Project(mob, emptyAffect, mobExtra);
        var baselineScore = LegacyMobCombatState.Read(baseline.Mob).CurrentScore;

        Check(world.Enter(1, "ELEMENTAL_PROTECTION_PLAYER", static (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(
                  1, 0, 0, 1, 0, 0, 2_000, 2_000, mob,
                  LegacyAccountSnapshot.ClassMasterMortal, emptyAffect, mobExtra),
            "Protecao Elemental player fixture was not installed.");

        var result = world.TryApplySkillAttack(
            1, 1, ProtecaoElemental(), ItemData, weather: 0, randomFactor: 100, out var outcome);
        Check(result == LegacySkillAttackResult.Accepted && outcome?.Damage == 0 &&
              outcome.TargetAffectSnapshot is { } firstAffect && HasAffectType(firstAffect, 25),
            "Protecao Elemental did not apply Type 25 to its player caster.");

        var affect = outcome!.TargetAffectSnapshot!;
        var expectedProjection = Project(mob, affect, mobExtra);
        var expectedScore = LegacyMobCombatState.Read(expectedProjection.Mob).CurrentScore;
        var appliedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        Check(GetAffectValue(affect, 25) == 10 &&
              GetAffectLevel(affect, 25) == 50 &&
              GetAffectTime(affect, 25) == 24 &&
              expectedScore.Ac == baselineScore.Ac + 35,
            "Protecao Elemental did not retain the 7.69 Type 25 value, Special2 level, duration, and AC formula.");
        Check(appliedScore.Ac == expectedScore.Ac &&
              outcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4)
                  .SequenceEqual(expectedProjection.Mob.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4)) &&
              appliedScore.Hp == 700 && appliedScore.Mp == 250,
            $"Protecao Elemental did not project AC/resistances or preserve resources; baseline AC={baselineScore.Ac}, actual AC={appliedScore.Ac}.");
        Check(expectedProjection.Mob.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4)
                  .SequenceEqual(new byte[] { 2, 2, 2, 2 }),
            "The 7.69 Type 25 resistance stage did not add two to Holy, Thunder, Fire, and Ice.");

        var recastResult = world.TryApplySkillAttack(
            1, 1, ProtecaoElemental(), ItemData, weather: 0, randomFactor: 100, out var recastOutcome);
        Check(recastResult == LegacySkillAttackResult.Accepted &&
              recastOutcome?.TargetAffectSnapshot is { } recastAffect &&
              GetAffectTime(recastAffect, 25) == 24 && GetAffectLevel(recastAffect, 25) == 50,
            "Reapplying Protecao Elemental did not refresh its source-derived duration.");
        var recastScore = LegacyMobCombatState.Read(recastOutcome!.TargetMobSnapshot).CurrentScore;
        Check(recastScore.Ac == expectedScore.Ac &&
              recastOutcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4)
                  .SequenceEqual(expectedProjection.Mob.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4)) &&
              recastScore.Hp == 700 && recastScore.Mp == 250,
            "Reapplying Protecao Elemental stacked Type 25 score or changed current resources.");

        LegacyPlayerAffectTimerOutcome? expiry = null;
        for (var tick = 0; tick < 24; tick++)
            expiry = world.TryProcessPlayerTimerAffects(1);
        Check(expiry is { ScoreRefreshRequired: true } && !HasAffectType(expiry.AffectSnapshot, 25),
            "Player Type 25 expiry did not clear Protecao Elemental with a score refresh.");
        var restoredScore = LegacyMobCombatState.Read(expiry!.MobSnapshot).CurrentScore;
        Check(restoredScore.Ac == baselineScore.Ac &&
              expiry.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4)
                  .SequenceEqual(baseline.Mob.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4)) &&
              restoredScore.Hp == 700 && restoredScore.Mp == 250,
            "Player Type 25 expiry did not restore baseline score and resources.");
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

    private static LegacySkillDefinition ProtecaoElemental() =>
        new(53, 78, 0, 105, 15, 0, 0, 0, 0, 0, 25, 10, 15,
            [9, 0, 0, 8, 0, 0, 0, 0], [9, 0, 0, 8, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Protecao Elemental");

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

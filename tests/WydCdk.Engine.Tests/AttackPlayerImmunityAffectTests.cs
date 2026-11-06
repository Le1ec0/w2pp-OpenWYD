using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPlayerImmunityAffectTests
{
    private static readonly LegacyItemDataTable ItemData =
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    public static void Run()
    {
        ImunidadeProjectsAndRestoresPlayerScore();
    }

    private static void ImunidadeProjectsAndRestoresPlayerScore()
    {
        var world = new WorldHub(itemData: ItemData);
        var mob = CreateMob(level: 1_500, ac: 100, damage: 500, hp: 700, mp: 250, special2: 50);
        var mobExtra = CreateMobExtra(LegacyAccountSnapshot.ClassMasterMortal);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = Project(mob, emptyAffect, mobExtra);
        var baselineRsv = ReadRsv(baseline.Mob);
        var baselineScore = LegacyMobCombatState.Read(baseline.Mob).CurrentScore;

        Check(world.Enter(1, "IMMUNITY_PLAYER", static (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(
                  1, 0, 0, 1, 0, 0, 2_000, 2_000, mob,
                  LegacyAccountSnapshot.ClassMasterMortal, emptyAffect, mobExtra),
            "Imunidade player fixture was not installed.");

        var result = world.TryApplySkillAttack(
            1, 1, Imunidade(), ItemData, weather: 0, randomFactor: 100, out var outcome);
        Check(result == LegacySkillAttackResult.Accepted && outcome?.Damage == 0 &&
              outcome.TargetAffectSnapshot is { } firstAffect && HasAffectType(firstAffect, 19),
            "Imunidade did not apply Type 19 to its player caster.");

        var affect = outcome!.TargetAffectSnapshot!;
        var expectedProjection = Project(mob, affect, mobExtra);
        var expectedRsv = ReadRsv(expectedProjection.Mob);
        var appliedRsv = ReadRsv(outcome.TargetMobSnapshot);
        var appliedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        Check(GetAffectValue(affect, 19) == 15 && GetAffectLevel(affect, 19) == 50 &&
              (expectedRsv & LegacyCurrentScoreMath.BlockRsvFlag) != 0 &&
              (appliedRsv & LegacyCurrentScoreMath.BlockRsvFlag) != 0 && appliedRsv == expectedRsv,
            $"Imunidade did not project the 7.69 Type 19 RSV_BLOCK state; baseline=0x{baselineRsv:X4}, expected=0x{expectedRsv:X4}, actual=0x{appliedRsv:X4}.");
        Check(appliedScore.Hp == baselineScore.Hp && appliedScore.Mp == baselineScore.Mp,
            "Imunidade changed the player's current HP or MP while rebuilding score.");

        var recastResult = world.TryApplySkillAttack(
            1, 1, Imunidade(), ItemData, weather: 0, randomFactor: 100, out var recastOutcome);
        Check(recastResult == LegacySkillAttackResult.Accepted &&
              recastOutcome?.TargetAffectSnapshot is { } recastAffect &&
              CountAffectType(recastAffect, 19) == 1 &&
              ReadRsv(recastOutcome.TargetMobSnapshot) == expectedRsv,
            "Reapplying Imunidade stacked Type 19 or lost its projected RSV_BLOCK state.");

        var remainingTicks = GetAffectTime(recastOutcome!.TargetAffectSnapshot!, 19);
        Check(remainingTicks is > 0 and <= 1_000, "Imunidade did not produce a bounded finite affect duration.");
        LegacyPlayerAffectTimerOutcome? expiry = null;
        for (var tick = 0; tick < remainingTicks; tick++)
            expiry = world.TryProcessPlayerTimerAffects(1);

        Check(expiry is { ScoreRefreshRequired: true } && !HasAffectType(expiry.AffectSnapshot, 19),
            "Player Type 19 expiry did not clear Imunidade with a score refresh.");
        var restoredScore = LegacyMobCombatState.Read(expiry!.MobSnapshot).CurrentScore;
        Check(ReadRsv(expiry.MobSnapshot) == baselineRsv && restoredScore.Hp == baselineScore.Hp &&
              restoredScore.Mp == baselineScore.Mp,
            "Player Type 19 expiry did not restore baseline flags and preserve current resources.");
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

    private static LegacySkillDefinition Imunidade() =>
        new(76, 84, 0, 42, 6, 0, 0, 0, 0, 0, 19, 15, 60,
            [20, 0, 0, 9, 0, 0, 0, 0], [19, 0, 0, 8, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Imunidade");

    private static ushort ReadRsv(ReadOnlySpan<byte> mob) =>
        BinaryPrimitives.ReadUInt16LittleEndian(mob.Slice(LegacyAccountSnapshot.MobRsvOffset, sizeof(ushort)));

    private static bool HasAffectType(ReadOnlySpan<byte> affect, byte type) => CountAffectType(affect, type) > 0;

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

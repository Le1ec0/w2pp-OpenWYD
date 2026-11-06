using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPlayerSpiritExchangeAffectTests
{
    private static readonly LegacyItemDataTable ItemData =
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    public static void Run()
    {
        TrocaDeEspiritosProjectsAndRestoresPlayerMaxResources();
    }

    private static void TrocaDeEspiritosProjectsAndRestoresPlayerMaxResources()
    {
        var world = new WorldHub(itemData: ItemData);
        var mob = CreateMob(level: 1_500, hp: 700, mp: 250);
        var mobExtra = CreateMobExtra(LegacyAccountSnapshot.ClassMasterMortal);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = Project(mob, emptyAffect, mobExtra);
        var baselineScore = LegacyMobCombatState.Read(baseline.Mob).CurrentScore;

        Check(world.Enter(1, "SPIRIT_EXCHANGE_PLAYER", static (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(
                  1, 0, 0, 1, 0, 0, 2_000, 2_000, mob,
                  LegacyAccountSnapshot.ClassMasterMortal, emptyAffect, mobExtra),
            "Troca de Espiritos player fixture was not installed.");

        var result = world.TryApplySkillAttack(
            1, 1, TrocaDeEspiritos(), ItemData, weather: 0, randomFactor: 100, out var outcome);
        Check(result == LegacySkillAttackResult.Accepted && outcome?.Damage == 0 &&
              outcome.TargetAffectSnapshot is { } firstAffect && HasAffectType(firstAffect, 38),
            "Troca de Espiritos did not apply Type 38 to its player caster.");

        var affect = outcome!.TargetAffectSnapshot!;
        var appliedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        var manaTransferred = baselineScore.MaxMp / 2;
        Check(appliedScore.MaxHp == baselineScore.MaxHp + manaTransferred &&
              appliedScore.MaxMp == baselineScore.MaxMp - manaTransferred &&
              appliedScore.Hp == 700 && appliedScore.Mp == 250,
            $"Troca de Espiritos did not transfer half MaxMp to MaxHp while preserving current HP/MP; baseline=({baselineScore.MaxHp},{baselineScore.MaxMp}), actual=({appliedScore.MaxHp},{appliedScore.MaxMp},{appliedScore.Hp},{appliedScore.Mp}).");

        var recastResult = world.TryApplySkillAttack(
            1, 1, TrocaDeEspiritos(), ItemData, weather: 0, randomFactor: 100, out var recastOutcome);
        var recastAffect = recastOutcome?.TargetAffectSnapshot;
        Check(recastResult == LegacySkillAttackResult.Accepted &&
              recastAffect is not null && HasAffectType(recastAffect, 38),
            "Reapplying Troca de Espiritos did not refresh its Type 38 affect.");
        var recastScore = LegacyMobCombatState.Read(recastOutcome!.TargetMobSnapshot).CurrentScore;
        Check(recastScore.MaxHp == appliedScore.MaxHp && recastScore.MaxMp == appliedScore.MaxMp &&
              recastScore.Hp == 700 && recastScore.Mp == 250,
            "Reapplying Troca de Espiritos stacked the Type 38 resource transfer or changed current HP/MP.");

        LegacyPlayerAffectTimerOutcome? expiry = null;
        var duration = GetAffectTime(recastAffect!, 38);
        for (var tick = 0; tick < duration; tick++)
            expiry = world.TryProcessPlayerTimerAffects(1);
        Check(expiry is { ScoreRefreshRequired: true } && !HasAffectType(expiry.AffectSnapshot, 38),
            "Player Type 38 expiry did not clear Troca de Espiritos with a score refresh.");
        var restoredScore = LegacyMobCombatState.Read(expiry!.MobSnapshot).CurrentScore;
        Check(restoredScore.MaxHp == baselineScore.MaxHp && restoredScore.MaxMp == baselineScore.MaxMp &&
              restoredScore.Hp == 700 && restoredScore.Mp == 250,
            $"Player Type 38 expiry did not restore baseline max resources/current HP/MP; expected=({baselineScore.MaxHp},{baselineScore.MaxMp},700,250), actual=({restoredScore.MaxHp},{restoredScore.MaxMp},{restoredScore.Hp},{restoredScore.Mp}).");
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
            1_000, 500, hp, mp, 0, 100, 0, 40, 30, 20, 10, 10);
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

    private static LegacySkillDefinition TrocaDeEspiritos() =>
        new(87, 242, 0, 90, 10, 0, 0, 160, 0, 0, 38, 0, 3,
            [8, 0, 0, 7, 0, 0, 0, 0], [9, 0, 0, 7, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Troca de Espiritos");

    private static bool HasAffectType(ReadOnlySpan<byte> affect, byte type)
    {
        for (var offset = 0; offset + 8 <= affect.Length; offset += 8)
            if (affect[offset] == type)
                return true;
        return false;
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

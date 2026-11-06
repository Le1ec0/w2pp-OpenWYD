using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPlayerEvasionAffectTests
{
    private const ushort ParryRsvFlag = LegacyCurrentScoreMath.ParryRsvFlag;
    private static readonly LegacyItemDataTable ItemData = CreateItemDataTable();

    public static void Run()
    {
        Type26ScoreProjectionSetsParryRsv();
        Skill89ProjectsAndExpiresParryOnItsCaster();
    }

    private static void Type26ScoreProjectionSetsParryRsv()
    {
        var mob = CreateMob(hp: 700, mp: 250);
        var extra = CreateMobExtra(LegacyAccountSnapshot.ClassMasterMortal);
        var affect = CreateType26Affect(level: 20, time: 1);
        var projected = Project(mob, affect, extra);
        Check((LegacyMobCombatState.Read(projected.Mob).Rsv & ParryRsvFlag) != 0,
            "The target score projection did not set RSV_PARRY for active Type 26.");
    }

    private static void Skill89ProjectsAndExpiresParryOnItsCaster()
    {
        var world = new WorldHub(itemData: ItemData);
        var mob = CreateMob(hp: 700, mp: 250);
        var extra = CreateMobExtra(LegacyAccountSnapshot.ClassMasterMortal);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = Project(mob, emptyAffect, extra);
        Check((LegacyMobCombatState.Read(baseline.Mob).Rsv & ParryRsvFlag) == 0,
            "The Type 26 baseline unexpectedly contains RSV_PARRY.");

        Check(world.Enter(1, "EVASION_PLAYER", static (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(
                  1, 0, 0, 1, 0, 0, 2_000, 2_000, mob,
                  LegacyAccountSnapshot.ClassMasterMortal, emptyAffect, extra),
            "Evasao Aprimorada player fixture was not installed.");

        var result = world.TryApplySkillAttack(1, 1, Evasion(), ItemData, weather: 0, randomFactor: 100, out var outcome);
        Check(result == LegacySkillAttackResult.Accepted && outcome?.Damage == 0 &&
              outcome.TargetAffectSnapshot is { } firstAffect && HasAffectType(firstAffect, 26),
            "Skill 89 did not apply Type 26 to its player caster.");

        var affected = LegacyMobCombatState.Read(outcome!.TargetMobSnapshot);
        Check((affected.Rsv & ParryRsvFlag) != 0 &&
              affected.CurrentScore.Hp == 700 && affected.CurrentScore.Mp == 250,
            $"Skill 89 did not project RSV_PARRY while preserving current HP/MP; rsv=0x{affected.Rsv:X4}, hp={affected.CurrentScore.Hp}, mp={affected.CurrentScore.Mp}.");

        var recast = world.TryApplySkillAttack(1, 1, Evasion(), ItemData, weather: 0, randomFactor: 100, out var recastOutcome);
        var recastAffect = recastOutcome?.TargetAffectSnapshot;
        Check(recast == LegacySkillAttackResult.Accepted && recastAffect is not null &&
              HasExactlyOneAffectType(recastAffect, 26),
            "Recasting skill 89 did not refresh exactly one Type 26 record.");
        var confirmedAffect = recastAffect ?? throw new InvalidOperationException("The recast omitted its Type 26 affect snapshot.");
        var duration = GetAffectTime(confirmedAffect, 26);
        Check(duration == 9 && confirmedAffect[1] == 1 &&
              BinaryPrimitives.ReadUInt16LittleEndian(confirmedAffect.AsSpan(2)) == 20,
            $"Skill 89 Type 26 did not preserve the loaded 7.69 value/level/duration; value={confirmedAffect[1]}, level={BinaryPrimitives.ReadUInt16LittleEndian(confirmedAffect.AsSpan(2))}, time={duration}.");

        LegacyPlayerAffectTimerOutcome? expiry = null;
        for (var tick = 0; tick < duration; tick++)
            expiry = world.TryProcessPlayerTimerAffects(1);
        Check(expiry is { ScoreRefreshRequired: true } && !HasAffectType(expiry.AffectSnapshot, 26),
            "Player Type 26 expiry did not clear the affect with a score refresh.");
        var restored = LegacyMobCombatState.Read(expiry!.MobSnapshot);
        Check((restored.Rsv & ParryRsvFlag) == (LegacyMobCombatState.Read(baseline.Mob).Rsv & ParryRsvFlag) &&
              restored.CurrentScore.Hp == 700 && restored.CurrentScore.Mp == 250,
            "Player Type 26 expiry did not restore baseline RSV_PARRY and current HP/MP.");
    }

    private static LegacyCharacterLoginData Project(byte[] mob, byte[] affect, byte[] extra) =>
        LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                mob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                affect.ToArray(),
                extra.ToArray(),
                0,
                0,
                0),
            ItemData);

    private static byte[] CreateMob(int hp, int mp)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(1_500, 100, 500, 0, 0x96, 0, 0,
            1_000, 500, hp, mp, 0, 100, 0, 40, 30, 20, 10, 10) with { Special4 = 20 };
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = 3;
        return mob;
    }

    private static byte[] CreateMobExtra(short classMaster)
    {
        var extra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteInt16LittleEndian(extra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset), classMaster);
        return extra;
    }

    private static LegacyItemDataTable CreateItemDataTable() =>
        LegacyItemDataTable.Load(Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());

    private static byte[] CreateType26Affect(ushort level, uint time)
    {
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = 26;
        affect[1] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(2), level);
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), time);
        return affect;
    }

    private static LegacySkillDefinition Evasion() =>
        new(89, 42, 0, 24, 6, 0, 0, 0, 0, 0, 26, 1, 7,
            [8, 0, 0, 7, 0, 0, 0, 0], [9, 0, 0, 7, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Evasao Aprimorada");

    private static bool HasAffectType(ReadOnlySpan<byte> affect, byte type)
    {
        for (var offset = 0; offset + 8 <= affect.Length; offset += 8)
            if (affect[offset] == type)
                return true;
        return false;
    }

    private static bool HasExactlyOneAffectType(ReadOnlySpan<byte> affect, byte type)
    {
        var count = 0;
        for (var offset = 0; offset + 8 <= affect.Length; offset += 8)
            if (affect[offset] == type)
                count++;
        return count == 1;
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
        if (!condition) throw new InvalidOperationException(message);
    }
}

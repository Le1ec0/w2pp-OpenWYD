using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPlayerInvisibilityAffectTests
{
    private const ushort HideRsvFlag = LegacyCurrentScoreMath.HideRsvFlag;
    private static readonly LegacyItemDataTable ItemData = CreateItemDataTable();

    public static void Run()
    {
        Skill95ProjectsAndExpiresHideOnItsCaster();
    }

    private static void Skill95ProjectsAndExpiresHideOnItsCaster()
    {
        var world = new WorldHub(itemData: ItemData);
        var mob = CreateMob(hp: 700, mp: 250);
        var extra = CreateMobExtra(LegacyAccountSnapshot.ClassMasterMortal);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = Project(mob, emptyAffect, extra);
        Check((LegacyMobCombatState.Read(baseline.Mob).Rsv & HideRsvFlag) == 0,
            "The Type 28 baseline unexpectedly contains RSV_HIDE.");

        Check(world.Enter(1, "INVISIBILITY_PLAYER", static (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(
                  1, 0, 0, 1, 0, 0, 2_000, 2_000, mob,
                  LegacyAccountSnapshot.ClassMasterMortal, emptyAffect, extra),
            "Invisibility player fixture was not installed.");

        var result = world.TryApplySkillAttack(1, 1, Invisibility(), ItemData, weather: 0, randomFactor: 100, out var outcome);
        Check(result == LegacySkillAttackResult.Accepted && outcome?.Damage == 0 &&
              outcome.TargetAffectSnapshot is { } firstAffect && HasAffectType(firstAffect, 28),
            "Skill 95 did not apply Type 28 to its player caster.");

        var affected = LegacyMobCombatState.Read(outcome!.TargetMobSnapshot);
        Check((affected.Rsv & HideRsvFlag) != 0 &&
              affected.CurrentScore.Hp == 700 && affected.CurrentScore.Mp == 250,
            $"Skill 95 did not project RSV_HIDE while preserving current HP/MP; rsv=0x{affected.Rsv:X4}, hp={affected.CurrentScore.Hp}, mp={affected.CurrentScore.Mp}.");

        var recast = world.TryApplySkillAttack(1, 1, Invisibility(), ItemData, weather: 0, randomFactor: 100, out var recastOutcome);
        var recastAffect = recastOutcome?.TargetAffectSnapshot;
        Check(recast == LegacySkillAttackResult.Accepted && recastAffect is not null &&
              HasExactlyOneAffectType(recastAffect, 28),
            "Recasting skill 95 did not refresh exactly one Type 28 record.");
        var confirmedRecastAffect = recastAffect ?? throw new InvalidOperationException("The recast omitted its Type 28 affect snapshot.");
        var duration = GetAffectTime(confirmedRecastAffect, 28);
        Check(duration == 1 && confirmedRecastAffect[1] == 1 &&
              BinaryPrimitives.ReadUInt16LittleEndian(confirmedRecastAffect.AsSpan(2)) == 20,
            $"Skill 95 Type 28 did not preserve the loaded 7.69 value/level/duration; value={confirmedRecastAffect[1]}, level={BinaryPrimitives.ReadUInt16LittleEndian(confirmedRecastAffect.AsSpan(2))}, time={duration}.");

        var expiry = world.TryProcessPlayerTimerAffects(1);
        Check(expiry is { ScoreRefreshRequired: true } && !HasAffectType(expiry.AffectSnapshot, 28),
            "Player Type 28 expiry did not clear the affect with a score refresh.");
        var restored = LegacyMobCombatState.Read(expiry!.MobSnapshot);
        Check((restored.Rsv & HideRsvFlag) == (LegacyMobCombatState.Read(baseline.Mob).Rsv & HideRsvFlag) &&
              restored.CurrentScore.Hp == 700 && restored.CurrentScore.Mp == 250,
            "Player Type 28 expiry did not restore baseline RSV_HIDE and current HP/MP.");
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

    private static LegacyItemDataTable CreateItemDataTable()
    {
        var encoded = Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray();
        return LegacyItemDataTable.Load(encoded);
    }

    private static LegacySkillDefinition Invisibility() =>
        new(95, 233, 0, 90, 35, 0, 0, 160, 0, 0, 28, 1, 0,
            [8, 0, 0, 7, 0, 0, 0, 0], [9, 0, 0, 7, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Invisibilidade");

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

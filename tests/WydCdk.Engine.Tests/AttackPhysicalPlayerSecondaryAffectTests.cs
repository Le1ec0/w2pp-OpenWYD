using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPhysicalPlayerSecondaryAffectTests
{
    private static readonly LegacyItemDataTable ItemData =
        LegacyItemDataTable.Load(Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());

    public static void Run()
    {
        RsvFrostProjectsType1OnPlayerAndRestoresItsScoreAtExpiry();
    }

    private static void RsvFrostProjectsType1OnPlayerAndRestoresItsScoreAtExpiry()
    {
        using var skillReader = new StringReader(
            "36,72,1,35,5,6,3,200,0,0,1,2,1,6.0.0.7.0.0.0.0,6.0.0.7.0.0.0.0,2,0,1,1,0,2,0,1,Nevasca");
        var world = new WorldHub(
            itemData: ItemData,
            skillData: LegacySkillDataTable.Load(skillReader));

        var attackerMob = CreateMob(new LegacyScore(
            1_500, 0, 100, 0, 0x96, 0, 0,
            1_000_000, 1_000, 1_000_000, 1_000,
            0, 100, 0, 0, 0, 40, 0, 0));
        BinaryPrimitives.WriteUInt16LittleEndian(
            attackerMob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), 0x01);
        Check(world.Enter(1, "RSV_PLAYER_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(1, 0, 0, 1, 0, 0, 2_000, 2_000, attackerMob),
            "The RSV player attacker fixture was not installed.");

        var targetMob = CreateMob(new LegacyScore(
            50, 10, 0, 0, 0x96, 0, 0,
            1_000_000, 1_000, 1_000_000, 1_000,
            0, 100, 0, 0, 0, 0, 0, 0));
        new LegacyItem(51, 0, 0, 0, 0, 0, 0)
            .Write(targetMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        Check(world.Enter(2, "RSV_PLAYER_TARGET", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(2, 0, 0, 2, 0, 0, 2_005, 2_005, targetMob),
            "The RSV player target fixture was not installed.");

        var result = world.TryApplyPhysicalAttack(
            1,
            2,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99,
            itemData: ItemData,
            frostRoll: 0,
            drainRoll: 1);
        Check(result == LegacyPhysicalAttackResult.Accepted && outcome is { Damage: > 0 },
            "The frost physical hit did not resolve against the player target.");
        Check(world.TryGetCharacterSnapshot(2, out var affectedMob, out _, out _, out _, out var affectedAffect) &&
              affectedMob is not null && affectedAffect is not null && HasAffectType(affectedAffect, 1),
            "RSV_FROST did not apply its Type 1 affect to the player target.");

        var expectedActive = Project(targetMob, affectedAffect!);
        var expectedBase = Project(targetMob, new byte[LegacyAccountSnapshot.AffectStride]);
        var activeScore = LegacyScore.Read(affectedMob!.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var projectedActiveScore = LegacyScore.Read(expectedActive.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var projectedBaseScore = LegacyScore.Read(expectedBase.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(projectedActiveScore.Intelligence == projectedBaseScore.Intelligence - 40 &&
              activeScore.Intelligence == projectedActiveScore.Intelligence,
            $"RSV_FROST did not immediately project the player's Type 1 Intelligence penalty; actual={activeScore.Intelligence}, expected={projectedActiveScore.Intelligence}.");

        var timer = world.TryProcessPlayerTimerAffects(2);
        Check(timer is { ScoreRefreshRequired: true } && !HasAffectType(timer.AffectSnapshot, 1),
            "The one-tick RSV_FROST player affect did not expire through the ordinary player timer.");
        Check(world.TryGetCharacterSnapshot(2, out var expiredMob, out _, out _, out _, out var expiredAffect) &&
              expiredMob is not null && expiredAffect is not null && !HasAffectType(expiredAffect, 1),
            "The expired player Type 1 affect was not committed to the character snapshot.");
        var expiredScore = LegacyScore.Read(expiredMob!.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(expiredScore.Intelligence == projectedBaseScore.Intelligence,
            $"Expiring RSV_FROST did not restore the player's projected score; actual={expiredScore.Intelligence}, expected={projectedBaseScore.Intelligence}.");
    }

    private static LegacyCharacterLoginData Project(byte[] mob, byte[] affect) =>
        LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                mob,
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                affect,
                new byte[LegacyAccountSnapshot.MobExtraStride],
                0,
                2_005,
                2_005),
            ItemData);

    private static byte[] CreateMob(LegacyScore score)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static bool HasAffectType(ReadOnlySpan<byte> affect, byte type)
    {
        for (var offset = 0; offset < affect.Length; offset += 8)
            if (affect[offset] == type)
                return true;
        return false;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

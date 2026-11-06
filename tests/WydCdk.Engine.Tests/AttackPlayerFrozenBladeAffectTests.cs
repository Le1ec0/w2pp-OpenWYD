using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPlayerFrozenBladeAffectTests
{
    private static readonly LegacyItemDataTable ItemData =
        LegacyItemDataTable.Load(Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());

    public static void Run()
    {
        LaminaCongelanteProjectsType7OnDamagingPlayerSkillAndExpires();
        LaminaCongelanteRecastPreservesStrongerExistingType7();
    }

    private static void LaminaCongelanteProjectsType7OnDamagingPlayerSkillAndExpires()
    {
        var attempt = ApplyLaminaCongelante();
        Check(attempt.Result == LegacySkillAttackResult.Accepted && attempt.Outcome is { Damage: > 0 } &&
              attempt.Outcome.TargetAffectSnapshot is { } appliedAffect && HasAffectType(appliedAffect, 7),
            "Lamina Congelante did not apply Type 7 through a damaging player-target skill.");

        var firstOutcome = attempt.Outcome!;
        var firstAffect = firstOutcome.TargetAffectSnapshot!;
        var affectedScore = LegacyScore.Read(firstOutcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(affectedScore.AttackRun == 0x86 && affectedScore.Intelligence == 75,
            $"Lamina Congelante Type 7 did not project the player target score; AttackRun=0x{affectedScore.AttackRun:X2}, Int={affectedScore.Intelligence}.");
        Check(affectedScore.Hp == firstOutcome.RemainingHp && affectedScore.Mp == 1_000,
            "Lamina Congelante score projection did not preserve the target's live HP/MP.");
        Check(GetAffectLevel(firstAffect, 7) == 50 && GetAffectTime(firstAffect, 7) == 1,
            "Lamina Congelante did not use the source skill level and one-tick duration.");

        var recast = attempt.World.TryApplySkillAttack(
            1, 2, LaminaCongelante(), ItemData, weather: 0, randomFactor: 100, out var recastOutcome,
            affectRandomFactor: 0);
        Check(recast == LegacySkillAttackResult.Accepted && recastOutcome?.TargetAffectSnapshot is { } recastAffect &&
              GetAffectTime(recastAffect, 7) == 1,
            "Lamina Congelante incorrectly reset Type 7 to four ticks on recast.");

        var expiry = attempt.World.TryProcessPlayerTimerAffects(2);
        Check(expiry is { ScoreRefreshRequired: true } && !HasAffectType(expiry.AffectSnapshot, 7),
            "Player Type 7 expiry did not clear the affect with a score refresh.");
        var restoredScore = LegacyScore.Read(expiry!.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(restoredScore.AttackRun == 0xA6 && restoredScore.Intelligence == 100,
            $"Player Type 7 expiry did not restore the baseline score; AttackRun=0x{restoredScore.AttackRun:X2}, Int={restoredScore.Intelligence}.");
    }

    private static void LaminaCongelanteRecastPreservesStrongerExistingType7()
    {
        var existingAffect = new byte[LegacyAccountSnapshot.AffectStride];
        existingAffect[0] = 7;
        existingAffect[1] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(existingAffect.AsSpan(2), 60);
        BinaryPrimitives.WriteUInt32LittleEndian(existingAffect.AsSpan(4), 3);

        var attempt = ApplyLaminaCongelante(existingAffect);
        Check(attempt.Result == LegacySkillAttackResult.Accepted && attempt.Outcome?.TargetAffectSnapshot is { } affect &&
              GetAffectLevel(affect, 7) == 60 && GetAffectTime(affect, 7) == 3,
            "Lamina Congelante recast discarded the source's higher existing Type 7 level or duration.");
    }

    private static (WorldHub World, LegacySkillAttackResult Result, LegacySkillAttackOutcome? Outcome) ApplyLaminaCongelante(
        byte[]? existingTargetAffect = null)
    {
        var world = new WorldHub(itemData: ItemData);
        var attackerMob = CreateMob(level: 1_500, attackRun: 0x96, intelligence: 100, hp: 1_000_000, headIndex: 0);
        var attackerScore = LegacyScore.Read(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with { Special4 = 50 };
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        var targetMob = CreateMob(level: 50, attackRun: 0xA6, intelligence: 100, hp: 1_000_000, headIndex: 51);
        Check(world.Enter(1, "FROZEN_BLADE_PLAYER_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(1, 0, 0, 1, 0, 0, 2_000, 2_000, attackerMob),
            "Lamina Congelante player attacker fixture was not installed.");
        Check(world.Enter(2, "FROZEN_BLADE_PLAYER_TARGET", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(
                  2,
                  0,
                  0,
                  2,
                  0,
                  0,
                  2_001,
                  2_000,
                  targetMob,
                  LegacyAccountSnapshot.ClassMasterMortal,
                  existingTargetAffect ?? new byte[LegacyAccountSnapshot.AffectStride]),
            "Lamina Congelante player target fixture was not installed.");

        var result = world.TryApplySkillAttack(
            1, 2, LaminaCongelante(), ItemData, weather: 0, randomFactor: 100, out var outcome,
            affectRandomFactor: 0);
        return (world, result, outcome);
    }

    private static byte[] CreateMob(int level, byte attackRun, short intelligence, int hp, int headIndex)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(level, 0, 83, 0, attackRun, 0, 0,
            hp, 1_000, hp, 1_000, 0, intelligence, 0, 0, 0, 0, 0, 0);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = 1;
        new LegacyItem(checked((short)headIndex), 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        return mob;
    }

    private static LegacySkillDefinition LaminaCongelante() =>
        new(19, 78, 1, 22, 4, 5, 3, 50, 0, 0, 7, 1, 0,
            [8, 0, 0, 7, 0, 0, 0, 0], [8, 0, 0, 7, 0, 0, 0, 0],
            2, 0, 1, 1, 0, 2, 0, 1, "Lamina_Congelante");

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

using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPlayerPursuitAffectTests
{
    private static readonly LegacyItemDataTable ItemData =
        LegacyItemDataTable.Load(Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());

    public static void Run()
    {
        PursuicaoProjectsPlayerResistancesAndRestoresThemOnExpiry();
        ResiDecreaseProjectsTickType3AndExpiresWithoutTheSetAffectFourTickReset();
    }

    private static void ResiDecreaseProjectsTickType3AndExpiresWithoutTheSetAffectFourTickReset()
    {
        var world = new WorldHub(itemData: ItemData);
        var attackerMob = CreateMob(level: 2_500, hp: 1_000_000, resistances: [0, 0, 0, 0], headIndex: 0);
        var targetMob = CreateMob(level: 2_000, hp: 1_000_000, resistances: [30, 40, 50, 20], headIndex: 51);
        Check(world.Enter(1, "RESI_DECREASE_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(1, 0, 0, 1, 0, 0, 2_000, 2_000, attackerMob),
            "Resi_Decrease attacker fixture was not installed.");
        Check(world.Enter(2, "RESI_DECREASE_TARGET", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(2, 0, 0, 2, 0, 0, 2_001, 2_000, targetMob),
            "Resi_Decrease player target fixture was not installed.");

        var result = world.TryApplySkillAttack(
            1, 2, ResiDecrease(), ItemData, weather: 0, randomFactor: 100, out var outcome,
            affectRandomFactor: 0);
        Check(result == LegacySkillAttackResult.Accepted &&
              outcome?.TargetAffectSnapshot is { } affect && HasAffectType(affect, 3),
            "Resi_Decrease did not apply its Type 3 tick to the player target.");
        AssertPlayerResistances(outcome!.TargetMobSnapshot, [0, 0, 10, 0], "Resi_Decrease application");
        Check(LegacyScore.Read(outcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)).Hp == 1_000_000 &&
              LegacyScore.Read(outcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)).Mp == 1_000,
            "Resi_Decrease score projection did not preserve current HP/MP.");

        var recast = world.TryApplySkillAttack(
            1, 2, ResiDecrease(), ItemData, weather: 0, randomFactor: 100, out var recastOutcome,
            affectRandomFactor: 0);
        Check(recast == LegacySkillAttackResult.Accepted &&
              recastOutcome?.TargetAffectSnapshot is { } recastAffect && GetAffectTime(recastAffect, 3) == 2,
            "Resi_Decrease recast did not retain the source SetTick duration instead of SetAffect's four ticks.");

        LegacyPlayerAffectTimerOutcome? expiry = null;
        for (var tick = 0; tick < 2; tick++)
            expiry = world.TryProcessPlayerTimerAffects(2);
        Check(expiry is { ScoreRefreshRequired: true } && !HasAffectType(expiry.AffectSnapshot, 3),
            "Resi_Decrease Type 3 expiry did not clear the affect with a score refresh.");
        AssertPlayerResistances(expiry!.MobSnapshot, [30, 40, 50, 20], "Resi_Decrease expiry");
    }

    private static void PursuicaoProjectsPlayerResistancesAndRestoresThemOnExpiry()
    {
        var world = new WorldHub(itemData: ItemData);
        var attackerMob = CreateMob(level: 2_500, hp: 1_000_000, resistances: [0, 0, 0, 0], headIndex: 0);
        var targetMob = CreateMob(level: 2_000, hp: 1_000_000, resistances: [30, 40, 50, 20], headIndex: 51);
        Check(world.Enter(1, "PURSUIT_PLAYER_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(1, 0, 0, 1, 0, 0, 2_000, 2_000, attackerMob),
            "Perseguição attacker fixture was not installed.");
        Check(world.Enter(2, "PURSUIT_PLAYER_TARGET", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(2, 0, 0, 2, 0, 0, 2_001, 2_000, targetMob),
            "Perseguição player target fixture was not installed.");

        var firstResult = world.TryApplySkillAttack(
            1, 2, Pursuicao(), ItemData, weather: 0, randomFactor: 100, out var firstOutcome,
            affectRandomFactor: 0);
        Check(firstResult == LegacySkillAttackResult.Accepted &&
              firstOutcome?.TargetAffectSnapshot is { } firstAffect && HasAffectType(firstAffect, 3),
            "Perseguição did not apply Type 3 to its player target.");
        AssertPlayerResistances(firstOutcome!.TargetMobSnapshot, [0, 0, 0, 0], "initial application");
        Check(firstOutcome.RemainingHp > 0 &&
              LegacyScore.Read(firstOutcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)).Mp == 1_000,
            "Perseguição score projection did not preserve the target's current resources.");

        Check(world.TryGetCharacterSnapshot(2, out var persistedMob, out _, out _, out _, out var persistedAffect) &&
              persistedMob is not null && persistedAffect is not null && HasAffectType(persistedAffect, 3),
            "Perseguição character snapshot did not capture the active affect for persistence.");
        AssertPlayerResistances(persistedMob!, [30, 40, 50, 20], "persistence snapshot");
        var restoredLogin = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                persistedMob!,
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                persistedAffect!,
                new byte[LegacyAccountSnapshot.MobExtraStride],
                0,
                0,
                0),
            ItemData);
        AssertPlayerResistances(restoredLogin.Mob, [0, 0, 0, 0], "login restore");
        Check(restoredLogin.PursuitBaseResistances is { } loginPursuitBaseline &&
              loginPursuitBaseline.AsSpan().SequenceEqual(new byte[] { 30, 40, 50, 20 }),
            "The login projector did not preserve the unclamped Type 3 baseline for the online session.");

        var recastResult = world.TryApplySkillAttack(
            1, 2, Pursuicao(), ItemData, weather: 0, randomFactor: 100, out var recastOutcome,
            affectRandomFactor: 0);
        Check(recastResult == LegacySkillAttackResult.Accepted &&
              recastOutcome?.TargetAffectSnapshot is { } recastAffect && GetAffectTime(recastAffect, 3) == 4,
            "Reapplying Perseguição did not reset the player Type 3 affect to four timer ticks.");
        AssertPlayerResistances(recastOutcome!.TargetMobSnapshot, [0, 0, 0, 0], "recast");

        LegacyPlayerAffectTimerOutcome? expiry = null;
        for (var tick = 0; tick < 4; tick++)
            expiry = world.TryProcessPlayerTimerAffects(2);
        Check(expiry is { ScoreRefreshRequired: true } && !HasAffectType(expiry.AffectSnapshot, 3),
            "Player Type 3 expiry did not clear the affect with a score refresh.");
        AssertPlayerResistances(expiry!.MobSnapshot, [30, 40, 50, 20], "expiry");

        Check(world.TryApplySkillAttack(
                  1, 2, Pursuicao(), ItemData, weather: 0, randomFactor: 100, out var cleanseSeed,
                  affectRandomFactor: 0) == LegacySkillAttackResult.Accepted &&
              cleanseSeed?.TargetAffectSnapshot is { } cleanseSeedAffect && HasAffectType(cleanseSeedAffect, 3),
            "Perseguição could not be reapplied before the Detox cleanup check.");
        var detoxResult = world.TryApplySkillAttack(
            1, 2, Detox(), ItemData, weather: 0, randomFactor: 100, out var detoxOutcome);
        Check(detoxResult == LegacySkillAttackResult.Accepted &&
              detoxOutcome?.TargetAffectSnapshot is { } detoxAffect && !HasAffectType(detoxAffect, 3),
            "Detox did not clear the player Type 3 affect.");
        AssertPlayerResistances(detoxOutcome!.TargetMobSnapshot, [30, 40, 50, 20], "Detox cleanup");
    }

    private static void AssertPlayerResistances(byte[] mob, byte[] expected, string phase)
    {
        Check(mob.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4).SequenceEqual(expected),
            $"Perseguição {phase} did not project the player's four elemental resistances; actual=" +
            string.Join(',', mob.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4).ToArray()));
    }

    private static byte[] CreateMob(int level, int hp, byte[] resistances, int headIndex)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(level, 0, 100, 0, 0x96, 0, 0,
            hp, 1_000, hp, 1_000, 0, 100, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        new LegacyScore(level, 0, 100, 0, 0x96, 0, 0,
            hp, 1_000, hp, 1_000, 0, 100, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        resistances.CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4));
        mob[LegacyAccountSnapshot.MobClassOffset] = 1;
        new LegacyItem(checked((short)headIndex), 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        return mob;
    }

    private static LegacySkillDefinition Pursuicao() =>
        new(16, 24, 3, 25, 2, 5, 0, 0, 0, 0, 3, 60, 0,
            [10, 0, 0, 0, 0, 0, 0, 0], [10, 0, 0, 0, 0, 0, 0, 0],
            2, 0, 1, 13, 0, 2, 0, 0, "Perseguicao");

    private static LegacySkillDefinition ResiDecrease() =>
        new(226, 350, 1, 300, 300, 6, 0, 0, 3, 50, 0, 0, 1,
            [24, 0, 0, 24, 0, 0, 0, 0], [24, 0, 0, 24, 0, 0, 0, 0],
            0, 0, 1, 1, 0, 1, 0, 0, "Resi_Decrease");

    private static LegacySkillDefinition Detox() =>
        new(30, 0, 0, 0, 0, 0, 8, 0, 0, 0, 0, 0, 0, [], [], 0, 0, 0, 1, 0, 0, 0, 0, "Desintoxicar");

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

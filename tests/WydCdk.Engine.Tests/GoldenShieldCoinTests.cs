using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class GoldenShieldCoinTests
{
    public static void Run()
    {
        var skill = new LegacySkillDefinition(
            85, 90, 0, 120, 5, 0, 0, 0, 0, 0, 31, 150, 30,
            new byte[8], new byte[8], 0, 0, 0, 1, 0, 0, 0, 0, "Escudo Dourado");
        var exactCost = CreateHub(connectionId: 1, coin: 300, currentMana: 700, stageLevel: 3);
        Check(exactCost.Hub.TryGetAttackState(1, out var before) && before is not null, "Golden Shield combat state was unavailable.");
        Check(LegacyGoldenShieldCoinMath.GetCost(skill, before!.CombatState) == 300,
            "Golden Shield fee must use 100 * Special[2] (C# Special3), not character level or another skill stage.");

        var spentResult = exactCost.Hub.TryConsumeGoldenShieldCoin(1, skill, out var spent, out var snapshot);
        Check(spentResult == LegacySkillCoinResult.Accepted && spent == 300 && snapshot is not null,
            "A character with exactly the fee could not pay for Golden Shield.");
        Check(BinaryPrimitives.ReadInt32LittleEndian(snapshot!.AsSpan(LegacyAccountSnapshot.MobCoinOffset)) == 0,
            "Golden Shield did not debit authoritative MOB.Coin by the exact fee.");

        var insufficient = CreateHub(connectionId: 2, coin: 299, currentMana: 700, stageLevel: 3);
        var insufficientResult = insufficient.Hub.TryConsumeGoldenShieldCoin(2, skill, out var notSpent, out var noSnapshot);
        Check(insufficientResult == LegacySkillCoinResult.InsufficientCoin && notSpent == 300 && noSnapshot is null,
            "Gold below the fee must reject silently without producing a spent snapshot.");
        Check(insufficient.Hub.TryGetCharacterSnapshot(2, out var unchangedMob, out _, out _) && unchangedMob is not null &&
            BinaryPrimitives.ReadInt32LittleEndian(unchangedMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset)) == 299 &&
            LegacyMobCombatState.Read(unchangedMob).CurrentMana == 700,
            "An insufficient-Gold attempt mutated Coin or MP.");
    }

    private static (WorldHub Hub, byte[] Mob) CreateHub(int connectionId, int coin, int currentMana, short stageLevel)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(399, 0, 0, 0, 0, 0, 0, 1000, 1000, 1000, currentMana,
            0, 0, 0, 0, 17, 19, stageLevel, 31)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), coin);
        mob[LegacyAccountSnapshot.MobClassOffset] = 3;

        var hub = new WorldHub();
        Check(hub.Enter(connectionId, "GOLDEN_SHIELD", (_, _) => ValueTask.CompletedTask), "Golden Shield participant was not registered.");
        Check(hub.SetCharacterState(connectionId, 0, 0, 0, 0, coin, 2000, 2000, mob), "Golden Shield participant state was not set.");
        return (hub, mob);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

using WydCdk.World;

internal static class AttackRvrGateTests
{
    public static void Run()
    {
        Check(LegacyAttackRvrGateRules.ShouldReject(1664, 1920, 0), "The first cell of the clanless RVR sector was allowed to attack.");
        Check(LegacyAttackRvrGateRules.ShouldReject(1791, 2047, 0), "The last cell of the clanless RVR sector was allowed to attack.");
        Check(!LegacyAttackRvrGateRules.ShouldReject(1663, 1920, 0) &&
            !LegacyAttackRvrGateRules.ShouldReject(1792, 1920, 0) &&
            !LegacyAttackRvrGateRules.ShouldReject(1664, 1919, 0) &&
            !LegacyAttackRvrGateRules.ShouldReject(1664, 2048, 0), "An adjacent sector was blocked as RVR.");
        Check(!LegacyAttackRvrGateRules.ShouldReject(1664, 1920, 1), "A character with a nonzero MOB.Clan was blocked in the RVR sector.");

        var hub = new WorldHub();
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        mob[LegacyAccountSnapshot.MobClanOffset] = 0;
        Check(hub.Enter(7, "ATTACK_RVR", (_, _) => ValueTask.CompletedTask), "RVR attack snapshot participant did not enter the world.");
        Check(hub.SetCharacterState(7, 0, guildId: 0, clan: 1, guildLevel: 0, coin: 0,
            positionX: 1664, positionY: 2047, mob: mob), "RVR attack snapshot character state was not set.");
        Check(hub.TryGetAttackState(7, out var attackState) && attackState is not null &&
            attackState.PositionX == 1664 && attackState.PositionY == 2047 && attackState.Clan == 0 &&
            LegacyAttackRvrGateRules.ShouldReject(attackState.PositionX, attackState.PositionY, attackState.Clan),
            "Attack did not capture the live world position and MOB.Clan byte atomically.");
        Check(!hub.TryGetAttackState(8, out var missing) && missing is null, "Attack state lookup accepted an unknown connection.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

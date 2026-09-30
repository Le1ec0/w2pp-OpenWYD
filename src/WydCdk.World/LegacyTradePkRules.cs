namespace WydCdk.World;

/// <summary>PK indicator after accepted QuitTrade; canonical 7.69, not W2PP's RvR extension.</summary>
public static class LegacyTradePkRules
{
    public static int Resolve(int guilty, bool pkMode, int x, int y, LegacyCombatWorldState world) =>
        world.NewbieEventServer || guilty != 0 || pkMode ||
        world.CastleState != 0 && x >= 1024 && x <= 1153 && y >= 1664 && y <= 1793 ||
        world.GuildTowerState != 0 && x >= 2430 && x <= 2560 && y >= 1825 && y <= 1925 ? 1 : 0;
}

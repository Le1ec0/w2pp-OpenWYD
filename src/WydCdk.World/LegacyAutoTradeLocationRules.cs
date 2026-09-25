namespace WydCdk.World;

/// <summary>
/// Resolves the city and tax used by the 7.69 autotrade start handler.
/// The bounds come from the five <c>g_pGuildZone[].CityLimit*</c> entries;
/// the Armia rectangle is the explicit protected area in that handler.
/// </summary>
public static class LegacyAutoTradeLocationRules
{
    public const int DefaultCityTax = 5;

    public static bool TryResolve(
        int x,
        int y,
        IReadOnlyList<int>? cityTaxes,
        out int village,
        out int cityTax)
    {
        village = LegacyMovementMapRules.FindGuildZone(x, y);
        cityTax = DefaultCityTax;

        if (village < 0 || village >= LegacyGuildZoneState.ZoneCount || IsProtectedArmiaArea(x, y))
        {
            village = -1;
            return false;
        }

        if (cityTaxes is not null && cityTaxes.Count == LegacyGuildZoneState.ZoneCount)
            cityTax = cityTaxes[village];
        return true;
    }

    private static bool IsProtectedArmiaArea(int x, int y) =>
        x is >= 2_123 and <= 2_148 && y is >= 2_139 and <= 2_157;
}

namespace WydCdk.World;

public sealed partial class WorldHub
{
    /// <summary>
    /// Resolves the target's city tax from the same five inclusive rectangles
    /// used by TMProject/W2PP BASE_GetVillage. It rejects a changed target kind,
    /// position, or merchant flag while tolerating unrelated MOB updates.
    /// </summary>
    public bool TryResolveNpcSellLocation(
        LegacyWorldMobSnapshot expectedTarget,
        out LegacyNpcSellLocation? location)
    {
        ArgumentNullException.ThrowIfNull(expectedTarget);
        location = null;

        if (!TryGetMobSnapshot(expectedTarget.ConnectionId, out var currentTarget) ||
            currentTarget is null ||
            currentTarget.Kind != expectedTarget.Kind ||
            currentTarget.PositionX != expectedTarget.PositionX ||
            currentTarget.PositionY != expectedTarget.PositionY ||
            currentTarget.MobSnapshot.Length <= LegacyAccountSnapshot.MobMerchantOffset ||
            expectedTarget.MobSnapshot.Length <= LegacyAccountSnapshot.MobMerchantOffset ||
            currentTarget.MobSnapshot[LegacyAccountSnapshot.MobMerchantOffset] !=
            expectedTarget.MobSnapshot[LegacyAccountSnapshot.MobMerchantOffset])
            return false;

        var village = LegacyMovementMapRules.FindGuildZone(currentTarget.PositionX, currentTarget.PositionY);
        var cityTax = village is >= 0 and < LegacyGuildZoneState.ZoneCount
            ? guildZones?.CityTaxes[village] ?? LegacyAutoTradeLocationRules.DefaultCityTax
            : (int?)null;
        location = new(village, cityTax);
        return true;
    }
}

public sealed record LegacyNpcSellLocation(int Village, int? CityTax);

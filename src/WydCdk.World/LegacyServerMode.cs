using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// The ruleset selected by one running world process. UP and PVP share the
/// same account/character storage; the mode is authoritative on the server.
/// </summary>
public enum LegacyServerMode
{
    Up,
    Pvp,
}

/// <summary>
/// Centralizes mode-dependent commercial rules so the client cannot choose
/// prices by changing its local catalog or purchase packet.
/// </summary>
public sealed class LegacyServerModePolicy(LegacyServerMode mode)
{
    public const string UpWorldKey = "UP";
    public const string PvpWorldKey = "PVP";

    public LegacyServerMode Mode { get; } = mode;

    public string WorldKey => IsPvp ? PvpWorldKey : UpWorldKey;

    public bool IsPvp => Mode == LegacyServerMode.Pvp;

    public static bool TryParseWorldKey(string? value, out LegacyServerMode mode)
    {
        mode = LegacyServerMode.Up;
        if (string.Equals(value, UpWorldKey, StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(value, PvpWorldKey, StringComparison.OrdinalIgnoreCase))
        {
            mode = LegacyServerMode.Pvp;
            return true;
        }

        return false;
    }

    public int GetDonatePrice(int configuredPrice) => IsPvp ? 0 : configuredPrice;

    public int GetGoldPrice(int configuredPrice) => IsPvp ? 0 : configuredPrice;

    public DonateStoreEntry Apply(DonateStoreEntry entry) =>
        entry with { Price = GetDonatePrice(entry.Price) };

    public LegacyDonateShopCatalog Apply(LegacyDonateShopCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (!IsPvp)
            return catalog;

        return LegacyDonateShopCatalog.FromEntries(catalog.Entries.Select(Apply).ToArray());
    }
}

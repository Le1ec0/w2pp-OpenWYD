namespace WydCdk.World;

public enum LegacyDonateShopRequestKind
{
    Open,
    Balance,
    Catalog,
    Purchase,
}

/// <summary>
/// Per-connection request gates preserved from the 7.59 Donate Shop handlers.
/// The caller supplies a monotonic tick value so the behavior stays
/// deterministic in tests and does not depend on wall-clock time.
/// </summary>
public sealed class LegacyDonateShopRateLimiter
{
    public const long OpenDelayMilliseconds = 200;
    public const long BalanceDelayMilliseconds = 2000;
    public const long CatalogDelayMilliseconds = 15000;
    public const long PurchaseDelayMilliseconds = 3000;

    private long lastOpen = long.MinValue;
    private long lastBalance = long.MinValue;
    private long lastCatalog = long.MinValue;
    private long lastPurchase = long.MinValue;

    public bool TryAccept(LegacyDonateShopRequestKind kind, long tick)
    {
        if (tick < 0)
            throw new ArgumentOutOfRangeException(nameof(tick));

        lock (this)
        {
            return kind switch
            {
                LegacyDonateShopRequestKind.Open => TryAccept(ref lastOpen, tick, OpenDelayMilliseconds),
                LegacyDonateShopRequestKind.Balance => TryAccept(ref lastBalance, tick, BalanceDelayMilliseconds),
                LegacyDonateShopRequestKind.Catalog => TryAccept(ref lastCatalog, tick, CatalogDelayMilliseconds),
                LegacyDonateShopRequestKind.Purchase => TryAccept(ref lastPurchase, tick, PurchaseDelayMilliseconds),
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
        }
    }

    private static bool TryAccept(ref long lastTick, long tick, long delay)
    {
        if (lastTick != long.MinValue && tick - lastTick < delay)
            return false;
        lastTick = tick;
        return true;
    }
}

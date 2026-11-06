namespace WydCdk.World;

/// <summary>
/// Models the exact <c>EF_VOLATILE == 1</c> sale restriction shared by the
/// canonical 7.69 and W2PP <c>_MSG_Sell</c> handlers.
/// </summary>
public static class LegacyNpcSellVolatileRules
{
    public const int BlockedVolatileValue = 1;

    public static LegacyNpcSellVolatileResolution Resolve(int volatileValue) =>
        volatileValue == BlockedVolatileValue
            ? new(LegacyNpcSellVolatileStatus.PotionSaleBlocked)
            : new(LegacyNpcSellVolatileStatus.ContinueSelling);
}

public enum LegacyNpcSellVolatileStatus
{
    ContinueSelling,
    PotionSaleBlocked,
}

public readonly record struct LegacyNpcSellVolatileResolution(LegacyNpcSellVolatileStatus Status)
{
    public bool CanContinueSelling => Status == LegacyNpcSellVolatileStatus.ContinueSelling;

    public bool RequiresPotionNotice => Status == LegacyNpcSellVolatileStatus.PotionSaleBlocked;
}

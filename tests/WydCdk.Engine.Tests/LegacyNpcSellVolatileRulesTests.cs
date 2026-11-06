using WydCdk.World;

internal static class LegacyNpcSellVolatileRulesTests
{
    public static void Run()
    {
        OnlyVolatileValueOneBlocksAndRequestsThePotionNotice();
        OtherVolatileValuesContinueSelling();
    }

    private static void OnlyVolatileValueOneBlocksAndRequestsThePotionNotice()
    {
        var result = LegacyNpcSellVolatileRules.Resolve(volatileValue: 1);

        Check(result.Status == LegacyNpcSellVolatileStatus.PotionSaleBlocked &&
              !result.CanContinueSelling && result.RequiresPotionNotice,
            "EF_VOLATILE value 1 did not block the sale and request the legacy potion notice.");
    }

    private static void OtherVolatileValuesContinueSelling()
    {
        foreach (var value in new[] { int.MinValue, -1, 0, 2, 32, 206, int.MaxValue })
        {
            var result = LegacyNpcSellVolatileRules.Resolve(value);

            Check(result.Status == LegacyNpcSellVolatileStatus.ContinueSelling &&
                  result.CanContinueSelling && !result.RequiresPotionNotice,
                $"EF_VOLATILE value {value} was blocked even though the source checks only for 1.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

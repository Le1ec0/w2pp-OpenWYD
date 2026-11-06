using WydCdk.World;

internal static class LegacyNpcSellAccountRulesTests
{
    public static void Run()
    {
        UnblockedAccountContinuesToTheNextSellGate();
        BlockedAccountStopsAndRequestsTheLegacyNotice();
    }

    private static void UnblockedAccountContinuesToTheNextSellGate()
    {
        var result = LegacyNpcSellAccountRules.Resolve(isBlocked: false);

        Check(result.Status == LegacyNpcSellAccountStatus.ContinueSelling &&
              result.CanContinueSelling && !result.RequiresBlockedAccountNotice,
            "An account without the operation block did not continue to the next _MSG_Sell gate.");
    }

    private static void BlockedAccountStopsAndRequestsTheLegacyNotice()
    {
        var result = LegacyNpcSellAccountRules.Resolve(isBlocked: true);

        Check(result.Status == LegacyNpcSellAccountStatus.Blocked &&
              !result.CanContinueSelling && result.RequiresBlockedAccountNotice,
            "A blocked account did not stop _MSG_Sell and request the legacy notice.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

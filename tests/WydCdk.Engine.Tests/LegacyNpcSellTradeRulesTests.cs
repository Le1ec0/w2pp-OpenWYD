using WydCdk.World;

internal static class LegacyNpcSellTradeRulesTests
{
    public static void Run()
    {
        NoOpponentContinuesToTheNextSellGate();
        ActiveOpponentClosesPeerFirstAndAbortsSell();
        AnyNonzeroOpponentIdTriggersTheLegacyBranch();
    }

    private static void NoOpponentContinuesToTheNextSellGate()
    {
        var result = LegacyNpcSellTradeRules.Resolve(connectionId: 10, opponentId: 0);

        Check(result.Status == LegacyNpcSellTradeStatus.ContinueSelling && result.CanContinueSelling &&
              result.FirstRemoveTradeConnectionId is null && result.SecondRemoveTradeConnectionId is null,
            "A zero OpponentID did not continue to the next _MSG_Sell gate.");
    }

    private static void ActiveOpponentClosesPeerFirstAndAbortsSell()
    {
        var result = LegacyNpcSellTradeRules.Resolve(connectionId: 10, opponentId: 20);

        Check(result.Status == LegacyNpcSellTradeStatus.CancelTradeAndReturn && !result.CanContinueSelling &&
              result.FirstRemoveTradeConnectionId == 20 && result.SecondRemoveTradeConnectionId == 10,
            "The active-trade branch did not remove opponent then requester and return before sale.");
    }

    private static void AnyNonzeroOpponentIdTriggersTheLegacyBranch()
    {
        var result = LegacyNpcSellTradeRules.Resolve(connectionId: 10, opponentId: -1);

        Check(result.Status == LegacyNpcSellTradeStatus.CancelTradeAndReturn &&
              result.FirstRemoveTradeConnectionId == -1 && result.SecondRemoveTradeConnectionId == 10,
            "The C++ truthy OpponentID condition was narrowed to positive IDs.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

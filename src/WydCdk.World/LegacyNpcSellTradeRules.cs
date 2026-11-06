namespace WydCdk.World;

/// <summary>
/// Resolves the active-trade branch at the start of 7.69/W2PP
/// <c>_MSG_Sell</c>. It plans the legacy RemoveTrade call order but does not
/// clear trade state or relay packets.
/// </summary>
public static class LegacyNpcSellTradeRules
{
    public static LegacyNpcSellTradeResolution Resolve(int connectionId, int opponentId)
    {
        if (opponentId == 0)
            return new(LegacyNpcSellTradeStatus.ContinueSelling, null, null);

        return new(LegacyNpcSellTradeStatus.CancelTradeAndReturn, opponentId, connectionId);
    }
}

public enum LegacyNpcSellTradeStatus
{
    ContinueSelling,
    CancelTradeAndReturn,
}

public readonly record struct LegacyNpcSellTradeResolution(
    LegacyNpcSellTradeStatus Status,
    int? FirstRemoveTradeConnectionId,
    int? SecondRemoveTradeConnectionId)
{
    public bool CanContinueSelling => Status == LegacyNpcSellTradeStatus.ContinueSelling;
}

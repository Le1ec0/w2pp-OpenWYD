namespace WydCdk.World;

/// <summary>
/// Pure gates for the legacy <c>_MSG_ReqTradeList</c> path and the
/// <c>SendAutoTrade</c> handoff it invokes.
/// </summary>
public static class LegacyAutoTradeListRules
{
    /// <summary>
    /// Applies the effective guards observed in the W2PP and community 7.69
    /// handlers. The target's store contents are deliberately not part of this
    /// rule; that belongs to the later offline-store state/persistence block.
    /// </summary>
    public static LegacyAutoTradeListResult Validate(
        int targetId,
        LegacyAutoTradeListContext context)
    {
        if (context.RequesterHp <= 0)
            return LegacyAutoTradeListResult.RequesterNotAlive;
        if (!context.RequesterInPlay)
            return LegacyAutoTradeListResult.RequesterNotPlaying;
        if (targetId <= 0 || !context.TargetExists)
            return LegacyAutoTradeListResult.TargetNotFound;
        if (!context.TargetAutoTradeActive)
            return LegacyAutoTradeListResult.TargetNotInAutoTrade;
        if (!context.TargetInPlay && !context.TargetOfflineAutoTrade)
            return LegacyAutoTradeListResult.TargetNotPlaying;
        if (!LegacyMovementRules.IsTargetWithinViewGrid(
                context.RequesterX,
                context.RequesterY,
                context.TargetX,
                context.TargetY))
            return LegacyAutoTradeListResult.OutOfRange;

        return LegacyAutoTradeListResult.Accepted;
    }
}

/// <summary>
/// State needed by the autotrade-list gate. It is intentionally independent
/// from <c>Participant</c>; an explicitly rehydrated offline autotrade NPC is
/// accepted through <c>TargetOfflineAutoTrade</c> without becoming a session.
/// </summary>
public readonly record struct LegacyAutoTradeListContext(
    int RequesterHp,
    bool RequesterInPlay,
    bool TargetExists,
    bool TargetInPlay,
    bool TargetAutoTradeActive,
    int RequesterX,
    int RequesterY,
    int TargetX,
    int TargetY,
    bool TargetOfflineAutoTrade = false);

public enum LegacyAutoTradeListResult
{
    Accepted,
    RequesterNotAlive,
    RequesterNotPlaying,
    TargetNotFound,
    TargetNotInAutoTrade,
    TargetNotPlaying,
    OutOfRange,
}

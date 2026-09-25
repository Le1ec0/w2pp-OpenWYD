using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Frames emitted only after <see cref="LegacyAutoTradePurchaseExecutor"/>
/// reports a durable commit. Recipient selection remains a host concern.
/// </summary>
public static class LegacyAutoTradePurchaseRelay
{
    public static bool TryBuild(
        LegacyAutoTradePurchaseExecutionOutcome outcome,
        LegacyFrameCodec codec,
        uint clientTick,
        byte keywordIndex,
        bool targetIsOfflineNpc,
        ReadOnlySpan<byte> offlineMob,
        ReadOnlySpan<byte> offlineAffect,
        out LegacyAutoTradePurchaseRelayPlan? plan)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(codec);
        plan = null;

        var purchase = outcome.Plan;
        if (outcome.Buyer.ConnectionId <= 0 || outcome.Buyer.ConnectionId > ushort.MaxValue ||
            purchase.TargetId <= 0 || purchase.TargetId > ushort.MaxValue ||
            purchase.Position is < 0 or >= AutoTradeListConfirmation.SlotCount ||
            purchase.BuyerCarry.Count != LegacyAccountSnapshot.MobCarryCount)
            return false;

        var buyerCarryFrame = new UpdateCarryConfirmation(purchase.BuyerCarry, purchase.BuyerCoin)
            .ToFrame(codec, clientTick, keywordIndex, checked((ushort)outcome.Buyer.ConnectionId));
        var itemSoldFrame = new ItemSoldConfirmation(purchase.TargetId, purchase.Position)
            .ToFrame(codec, clientTick, keywordIndex);

        byte[]? offlineVisualFrame = null;
        if (targetIsOfflineNpc)
        {
            if (LegacyAutoTradeVisualRelay.TryBuild(
                    outcome.UpdatedListing,
                    offlineMob,
                    offlineAffect,
                    codec,
                    clientTick,
                    keywordIndex,
                    out var visual) &&
                visual is not null)
                offlineVisualFrame = visual.Frame;
        }

        plan = new LegacyAutoTradePurchaseRelayPlan(
            outcome.Buyer.ConnectionId,
            purchase.TargetId,
            purchase.Position,
            buyerCarryFrame,
            itemSoldFrame,
            offlineVisualFrame);
        return true;
    }
}

public sealed record LegacyAutoTradePurchaseRelayPlan(
    int BuyerConnectionId,
    int TargetId,
    int Position,
    byte[] BuyerCarryFrame,
    byte[] ItemSoldFrame,
    byte[]? OfflineVisualFrame);

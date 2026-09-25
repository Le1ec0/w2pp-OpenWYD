using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Builds the native MSG_Trade notification sent to the other participant.
/// It has no socket or state mutation side effect; the host decides when to send
/// the returned frame.
/// </summary>
public static class LegacyTradeOfferRelay
{
    public static bool TryBuild(
        LegacyTradeOfferOutcome outcome,
        LegacyFrameCodec codec,
        uint clientTick,
        byte keywordIndex,
        out LegacyTradeOfferRelayPlan? plan)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(codec);
        plan = null;

        if (!outcome.OwnState.HasOffer ||
            outcome.ConnectionId <= 0 || outcome.ConnectionId > ushort.MaxValue ||
            outcome.OpponentId <= 0 || outcome.OpponentId > ushort.MaxValue ||
            outcome.OwnState.Items.Count != TradeOfferRequest.ItemCount ||
            outcome.OwnState.InventoryPositions.Count != TradeOfferRequest.ItemCount)
            return false;

        // The receiver sees the sender as its OpponentID. The native handler
        // rewrites the header ID and this field before forwarding MSG_Trade.
        var request = new TradeOfferRequest(
            outcome.OwnState.Items,
            outcome.OwnState.InventoryPositions,
            outcome.OwnState.TradeMoney,
            outcome.OwnState.MyCheck,
            checked((ushort)outcome.ConnectionId));
        var frame = request.ToFrame(
            codec,
            clientTick,
            keywordIndex,
            checked((ushort)outcome.OpponentId));

        plan = new LegacyTradeOfferRelayPlan(
            outcome.ConnectionId,
            outcome.OpponentId,
            frame,
            RequiresCheckConfirmation: outcome.OwnState.MyCheck == 1 && outcome.OpponentState.MyCheck == 0);
        return true;
    }
}

public sealed record LegacyTradeOfferRelayPlan(
    int SenderConnectionId,
    int RecipientConnectionId,
    byte[] OfferFrame,
    bool RequiresCheckConfirmation);

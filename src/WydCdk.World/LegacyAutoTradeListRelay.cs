using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Builds the client-facing MSG_AutoTrade response after the list gates pass.
/// It does not access sockets or mutate the shop book.
/// </summary>
public static class LegacyAutoTradeListRelay
{
    public static bool TryBuild(
        int requesterConnectionId,
        LegacyAutoTradeSnapshot snapshot,
        LegacyFrameCodec codec,
        uint clientTick,
        byte keywordIndex,
        out LegacyAutoTradeListRelayPlan? plan)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(codec);
        plan = null;

        if (requesterConnectionId <= 0 || requesterConnectionId > ushort.MaxValue ||
            snapshot.ConnectionId <= 0 || snapshot.ConnectionId > ushort.MaxValue ||
            snapshot.Items.Count != AutoTradeListConfirmation.SlotCount ||
            snapshot.CarryPositions.Count != AutoTradeListConfirmation.SlotCount ||
            snapshot.Prices.Count != AutoTradeListConfirmation.SlotCount)
            return false;

        var frame = new AutoTradeListConfirmation(
            snapshot.Title,
            snapshot.Items,
            snapshot.CarryPositions,
            snapshot.Prices,
            snapshot.Tax,
            checked((ushort)snapshot.ConnectionId))
            .ToFrame(codec, clientTick, keywordIndex);

        plan = new LegacyAutoTradeListRelayPlan(
            requesterConnectionId,
            snapshot.ConnectionId,
            frame);
        return true;
    }
}

public sealed record LegacyAutoTradeListRelayPlan(
    int RequesterConnectionId,
    int ShopConnectionId,
    byte[] ResponseFrame);

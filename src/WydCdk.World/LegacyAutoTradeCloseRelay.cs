using WydCdk.Protocol;

namespace WydCdk.World;

public sealed record LegacyAutoTradeCloseFramePlan(byte[] Frame, IReadOnlyList<int> RecipientIds);

public static class LegacyAutoTradeCloseRelay
{
    /// <summary>
    /// Captures output without sending it. Runtime callers must retain the close
    /// operation leases through this call and release them before socket writes.
    /// An outcome from an earlier operation is not authorization to relay later.
    /// </summary>
    public static bool TryBuild(WorldHub world, LegacyAutoTradeCloseOutcome outcome,
        LegacyFrameCodec codec, uint clientTick, byte keywordIndex,
        out IReadOnlyList<LegacyAutoTradeCloseFramePlan> plans)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(codec);
        plans = [];
        if (outcome.Result is not (LegacyAutoTradeCloseResult.Closed or LegacyAutoTradeCloseResult.NoShop) || outcome.Trade is null)
            return false;
        var trade = outcome.Trade;
        if (trade.ConnectionId is <= 0 or > ushort.MaxValue || trade.OpponentId is < 0 or > ushort.MaxValue)
            return false;
        var relayTick = outcome.ActionTriggered ? 0u : clientTick;
        var relayKeyword = outcome.ActionTriggered ? (byte)0 : keywordIndex;
        var captured = new List<LegacyAutoTradeCloseFramePlan>();
        if (trade.OpponentId > 0)
            captured.Add(new(TradeCloseRequest.ToFrame(codec, relayTick, relayKeyword, (ushort)trade.OpponentId), [trade.OpponentId]));
        captured.Add(new(TradeCloseRequest.ToFrame(codec, relayTick, relayKeyword, (ushort)trade.ConnectionId), [trade.ConnectionId]));
        foreach (var listing in outcome.ClosedListings)
        {
            var id = listing.Snapshot.ConnectionId;
            if (id != trade.ConnectionId && id != trade.OpponentId) return false;
            if (!world.TryBuildCreateMobFrame(id, codec, relayTick, relayKeyword, out var visual) || visual is null)
                return false;
            captured.Add(new(visual, world.GetParticipantIdsInPlayerView(id).ToArray()));
        }
        if (!outcome.ActionTriggered)
            captured.Add(new(new PkInfoConfirmation(trade.ConnectionId, trade.PkState)
                .ToFrame(codec, clientTick, keywordIndex), trade.PkRecipients.ToArray()));
        plans = captured.AsReadOnly();
        return true;
    }
}

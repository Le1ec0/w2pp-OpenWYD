using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>Captures listing/visual output under a lease; never writes sockets.</summary>
public static class LegacyAutoTradeListingRelay
{
    public static bool TryCapture(WorldHub world, LegacyAutoTradeBook book, LegacyAutoTradeOperationLease lease,
        int requesterId, int shopId, bool broadcastVisual, LegacyFrameCodec codec, uint clientTick, byte keywordIndex,
        out IReadOnlyList<LegacyAutoTradeCloseFramePlan> frames)
    {
        ArgumentNullException.ThrowIfNull(world); ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(lease); ArgumentNullException.ThrowIfNull(codec);
        frames = [];
        if (!lease.Covers(requesterId) || !lease.Covers(shopId) ||
            !book.TryGet(lease, shopId, out var handle) || handle is null ||
            !LegacyAutoTradeListRelay.TryBuild(requesterId, handle.Snapshot, codec, clientTick, keywordIndex, out var listing) || listing is null)
            return false;
        var captured = new List<LegacyAutoTradeCloseFramePlan> { new(listing.ResponseFrame, Array.AsReadOnly(new[] { requesterId })) };
        var available = world.TryGetCharacterSnapshot(shopId, out var mob, out _, out _, out _, out var affect);
        if (!available && world.TryGetNpcSnapshot(shopId, out var npc) && npc?.AutoTradeSnapshot is not null)
        {
            mob = npc.MobSnapshot; affect = npc.AffectSnapshot; available = true;
        }
        if (available && mob is not null && affect is not null &&
            LegacyAutoTradeVisualRelay.TryBuild(handle.Snapshot, mob, affect, codec, clientTick, keywordIndex, out var visual) && visual is not null)
        {
            var recipients = broadcastVisual
                ? world.GetParticipantIdsInPlayerView(shopId).Append(requesterId).Distinct().ToArray()
                : new[] { requesterId };
            captured.Add(new(visual.Frame, Array.AsReadOnly(recipients)));
        }
        frames = captured.AsReadOnly();
        return true;
    }
}

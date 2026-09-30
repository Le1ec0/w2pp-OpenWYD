using WydCdk.Protocol;

namespace WydCdk.World;

public sealed record LegacyRecallFramePlan(byte[] Frame, int RecipientId);

/// <summary>Builds spatial Action, player-presence, and item-view changes for a legacy recall.</summary>
public static class LegacyRecallRelay
{
    public static bool TryBuild(
        WorldHub world,
        LegacyRecallTransition transition,
        byte[] recallActionFrame,
        LegacyFrameCodec codec,
        uint clientTick,
        byte keywordIndex,
        out IReadOnlyList<LegacyRecallFramePlan> plans)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(recallActionFrame);
        ArgumentNullException.ThrowIfNull(codec);
        plans = [];

        var actorId = transition.ConnectionId;
        if (actorId is <= 0 or > ushort.MaxValue ||
            !transition.OriginViewIds.Contains(actorId) || !transition.DestinationViewIds.Contains(actorId) ||
            transition.OriginViewIds.Any(static id => id is <= 0 or > ushort.MaxValue) ||
            transition.DestinationViewIds.Any(static id => id is <= 0 or > ushort.MaxValue))
            return false;

        var origin = transition.OriginViewIds.ToHashSet();
        var destination = transition.DestinationViewIds.ToHashSet();
        if (origin.Count != transition.OriginViewIds.Count || destination.Count != transition.DestinationViewIds.Count)
            return false;

        if (transition.OriginItems.Any(static item => item.WireItemId <= LegacyWorldItem.WireIdOffset || item.WireItemId > (ushort)short.MaxValue || item.Item.Index <= 0) ||
            transition.DestinationItems.Any(static item => item.WireItemId <= LegacyWorldItem.WireIdOffset || item.WireItemId > (ushort)short.MaxValue || item.Item.Index <= 0))
            return false;

        if (transition.OriginItems.Select(static item => item.WireItemId).Distinct().Count() != transition.OriginItems.Count ||
            transition.DestinationItems.Select(static item => item.WireItemId).Distinct().Count() != transition.DestinationItems.Count)
            return false;

        var originItems = transition.OriginItems.ToDictionary(static item => item.WireItemId);
        var destinationItems = transition.DestinationItems.ToDictionary(static item => item.WireItemId);
        if (originItems.Keys.Intersect(destinationItems.Keys).Any(id => originItems[id] != destinationItems[id]))
            return false;

        var captured = new List<LegacyRecallFramePlan>();
        foreach (var recipientId in transition.OriginViewIds)
            if (recipientId != actorId)
                captured.Add(new(recallActionFrame, recipientId));

        foreach (var leavingId in origin.Except(destination).Where(id => id != actorId).Order())
        {
            captured.Add(new(
                new RemoveMobConfirmation(checked((ushort)actorId), removeType: 0).ToFrame(codec, clientTick, keywordIndex),
                leavingId));
            captured.Add(new(
                new RemoveMobConfirmation(checked((ushort)leavingId), removeType: 0).ToFrame(codec, clientTick, keywordIndex),
                actorId));
        }

        foreach (var enteringId in destination.Except(origin).Where(id => id != actorId).Order())
        {
            if (!world.TryBuildCreateMobFrame(actorId, codec, clientTick, keywordIndex, out var actorCreate) || actorCreate is null ||
                !world.TryBuildCreateMobFrame(enteringId, codec, clientTick, keywordIndex, out var enteringCreate) || enteringCreate is null ||
                !world.TryGetParticipantPkInfoState(actorId, out var actorPkState) ||
                !world.TryGetParticipantPkInfoState(enteringId, out var enteringPkState))
                return false;

            captured.Add(new(actorCreate, enteringId));
            captured.Add(new(new PkInfoConfirmation(actorId, actorPkState).ToFrame(codec, clientTick, keywordIndex), enteringId));
            captured.Add(new(enteringCreate, actorId));
            captured.Add(new(new PkInfoConfirmation(enteringId, enteringPkState).ToFrame(codec, clientTick, keywordIndex), actorId));
            captured.Add(new(recallActionFrame, enteringId));
        }

        foreach (var item in transition.OriginItems.Where(item => !destinationItems.ContainsKey(item.WireItemId)))
            captured.Add(new(
                new DecayItemConfirmation(checked((short)item.WireItemId)).ToFrame(codec, clientTick, keywordIndex),
                actorId));

        foreach (var item in transition.DestinationItems.Where(item => !originItems.ContainsKey(item.WireItemId)))
            captured.Add(new(
                new CreateItemConfirmation(
                    item.GridX,
                    item.GridY,
                    item.WireItemId,
                    item.Item,
                    item.Rotate,
                    item.State,
                    item.Height).ToFrame(codec, clientTick, keywordIndex),
                actorId));

        plans = captured.AsReadOnly();
        return true;
    }
}

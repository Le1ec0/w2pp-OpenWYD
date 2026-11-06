using WydCdk.Protocol;

namespace WydCdk.World;

public sealed record LegacyBattleRoyaleAreaClearActorPlan(
    int ConnectionId,
    short DestinationX,
    short DestinationY,
    IReadOnlyList<LegacyRecallFramePlan> Frames);

public sealed record LegacyBattleRoyaleAreaClearPlan(IReadOnlyList<LegacyBattleRoyaleAreaClearActorPlan> Actors);

internal sealed record LegacyBattleRoyaleAreaClearSubject(
    LegacyRecallTransition RecallTransition,
    byte[]? RevivedMobSnapshot,
    byte[]? RevivedAffectSnapshot);

/// <summary>Applies the legacy ClearAreaLevel recall and captures its score and view relays.</summary>
public static class LegacyBattleRoyaleAreaClearRelay
{
    public const short MinX = 2_604;
    public const short MaxX = 2_648;
    public const short MinY = 1_708;
    public const short MaxY = 1_744;

    public static LegacyBattleRoyaleAreaClearPlan? TryCreate(
        WorldHub world,
        LegacyBattleRoyaleTransition transition,
        LegacyFrameCodec codec,
        uint clientTick,
        byte keywordIndex,
        int cityRandomX = -1,
        int cityRandomY = -1)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(codec);

        if (transition.AreaClearLevels is not { } levels ||
            (transition.PreviousState, transition.State) is not ((0, 1) or (1, 2)) ||
            levels.MaxLevel != 400 || levels.MinLevel is not (100 or 200))
            return null;

        var subjects = world.ApplyBattleRoyaleAreaClearLevels(
            MinX,
            MinY,
            MaxX,
            MaxY,
            levels,
            cityRandomX,
            cityRandomY);
        var actors = new List<LegacyBattleRoyaleAreaClearActorPlan>(subjects.Count);
        foreach (var subject in subjects)
        {
            var recall = subject.RecallTransition;
            if (recall.ConnectionId is <= 0 or > ushort.MaxValue)
                return null;

            var frames = new List<LegacyRecallFramePlan>();
            if (subject.RevivedMobSnapshot is { } mob && subject.RevivedAffectSnapshot is { } affect)
            {
                var sourcePayload = new UpdateScoreConfirmation(mob, affect).ToPayload();
                var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                        sourcePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize),
                        LegacyMobCombatState.Read(mob).Rsv)
                    .ToFrame(codec, clientTick, keywordIndex, checked((ushort)recall.ConnectionId));
                frames.Add(new LegacyRecallFramePlan(scoreFrame, recall.ConnectionId));
            }

            var actionFrame = new ActionRequest(
                    recall.Recall.FromX,
                    recall.Recall.FromY,
                    Effect: 1,
                    Speed: 2,
                    Route: new byte[24],
                    recall.Recall.ToX,
                    recall.Recall.ToY)
                .ToFrame(codec, clientTick, keywordIndex, checked((ushort)recall.ConnectionId));
            frames.Add(new LegacyRecallFramePlan(actionFrame, recall.ConnectionId));

            if (!LegacyRecallRelay.TryBuild(world, recall, actionFrame, codec, clientTick, keywordIndex, out var relayFrames))
                return null;

            frames.AddRange(relayFrames);
            actors.Add(new LegacyBattleRoyaleAreaClearActorPlan(
                recall.ConnectionId,
                recall.Recall.ToX,
                recall.Recall.ToY,
                Array.AsReadOnly(frames.ToArray())));
        }

        return new LegacyBattleRoyaleAreaClearPlan(Array.AsReadOnly(actors.ToArray()));
    }

    public static async ValueTask SendAsync(
        WorldHub world,
        LoginSessionRegistry sessions,
        LegacyBattleRoyaleAreaClearPlan plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(plan);

        foreach (var actor in plan.Actors)
        {
            sessions.SetServerPosition(actor.ConnectionId, actor.DestinationX, actor.DestinationY);
            foreach (var frame in actor.Frames)
                await world.SendAsync(frame.RecipientId, frame.Frame, cancellationToken);
        }
    }
}

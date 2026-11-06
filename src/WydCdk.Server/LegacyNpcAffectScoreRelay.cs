using WydCdk.Protocol;
using WydCdk.World;

namespace WydCdk.Server;

/// <summary>Relays NPC affect score changes to players in the NPC's visible grid.</summary>
internal static class LegacyNpcAffectScoreRelay
{
    public static async ValueTask RelayNpcAffectScoreAsync(
        WorldHub world,
        LegacySkillAttackOutcome outcome,
        LegacyFrameCodec codec,
        uint clientTick,
        byte keywordIndex,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(codec);

        if (outcome.TargetAffectSnapshot is not { } affectSnapshot)
            return;

        var recipients = world.GetParticipantIdsInNpcView(outcome.TargetConnectionId);
        await RelayNpcAffectScoreAsync(
            world,
            outcome.TargetConnectionId,
            outcome.TargetMobSnapshot,
            affectSnapshot,
            recipients,
            codec,
            clientTick,
            keywordIndex,
            cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask RelayNpcAffectScoreAsync(
        WorldHub world,
        LegacyPhysicalAttackOutcome outcome,
        LegacyFrameCodec codec,
        uint clientTick,
        byte keywordIndex,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(codec);

        if (outcome.TargetAffectSnapshot is not { } affectSnapshot)
            return;

        await RelayNpcAffectScoreAsync(
            world,
            outcome.TargetConnectionId,
            outcome.TargetMobSnapshot,
            affectSnapshot,
            outcome.TargetAffectViewRecipients,
            codec,
            clientTick,
            keywordIndex,
            cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask RelayNpcAffectScoreAsync(
        WorldHub world,
        int targetConnectionId,
        byte[] targetMobSnapshot,
        byte[] affectSnapshot,
        IReadOnlyList<int> recipients,
        LegacyFrameCodec codec,
        uint clientTick,
        byte keywordIndex,
        CancellationToken cancellationToken)
    {
        if (recipients.Count == 0)
            return;

        var sourcePayload = new UpdateScoreConfirmation(targetMobSnapshot, affectSnapshot).ToPayload();
        var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                sourcePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize),
                LegacyMobCombatState.Read(targetMobSnapshot).Rsv)
            .ToFrame(codec, clientTick, keywordIndex, checked((ushort)targetConnectionId));
        await world.BroadcastToParticipantsAsync(recipients, scoreFrame, cancellationToken).ConfigureAwait(false);
    }
}

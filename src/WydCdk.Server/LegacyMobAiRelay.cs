using System.Security.Cryptography;
using WydCdk.Protocol;
using WydCdk.World;

namespace WydCdk.Server;

/// <summary>Relays immutable, view-captured outcomes from supported generated-NPC AI branches.</summary>
public static class LegacyMobAiRelay
{
    public static async ValueTask RelayAsync(
        WorldHub world,
        LegacyNpcAiApplicationOutcome outcome,
        LegacyFrameCodec codec,
        Func<int, byte[], int, LegacyItem, IReadOnlyList<int>, CancellationToken, ValueTask>? sendResurrectionRefresh,
        CancellationToken cancellationToken,
        uint? clientTick = null,
        byte? keywordIndex = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(codec);
        var tick = clientTick ?? unchecked((uint)Environment.TickCount64);
        var keyword = keywordIndex ?? (byte)RandomNumberGenerator.GetInt32(256);

        if (outcome.AffectTick is { } affectTick)
        {
            if (affectTick.MountOwnerConnectionId > 0 && affectTick.UpdatedMountItem is { } updatedMountItem)
            {
                var mountFrame = new SendItemConfirmation(0, 14, updatedMountItem)
                    .ToFrame(codec, tick, keyword, checked((ushort)affectTick.MountOwnerConnectionId));
                await SendToOneAsync(world, affectTick.MountOwnerConnectionId, mountFrame, cancellationToken).ConfigureAwait(false);
            }

            if (affectTick.HpUpdateRequired)
            {
                var hpFrame = new SetHpDamConfirmation(affectTick.Hp, affectTick.Damage)
                    .ToFrame(codec, tick, keyword, checked((ushort)affectTick.ConnectionId));
                await SendToViewAsync(world, affectTick.ViewRecipientConnectionIds, hpFrame, cancellationToken).ConfigureAwait(false);
            }

            if (affectTick.ScoreRefreshRequired)
            {
                var scorePayload = new UpdateScoreConfirmation(affectTick.MobSnapshot, affectTick.AffectSnapshot).ToPayload();
                var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                        scorePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize),
                        LegacyMobCombatState.Read(affectTick.MobSnapshot).Rsv)
                    .ToFrame(codec, tick, keyword, checked((ushort)affectTick.ConnectionId));
                await SendToViewAsync(world, affectTick.ViewRecipientConnectionIds, scoreFrame, cancellationToken).ConfigureAwait(false);
            }
        }

        if (outcome.Movement is { } movement)
            await RelayMovementAsync(world, movement, codec, tick, keyword, cancellationToken).ConfigureAwait(false);

        var hasRemoval = false;
        if (outcome.Removal is { } removal)
        {
            var removeFrame = new RemoveMobConfirmation(
                checked((ushort)removal.ConnectionId), removal.RemoveType).ToFrame(codec, tick, keyword);
            await SendToViewAsync(world, removal.ViewRecipientConnectionIds, removeFrame, cancellationToken).ConfigureAwait(false);
            hasRemoval = true;
        }

        foreach (var additionalRemoval in outcome.AdditionalRemovals)
        {
            var removeFrame = new RemoveMobConfirmation(
                checked((ushort)additionalRemoval.ConnectionId), additionalRemoval.RemoveType).ToFrame(codec, tick, keyword);
            await SendToViewAsync(world, additionalRemoval.ViewRecipientConnectionIds, removeFrame, cancellationToken).ConfigureAwait(false);
            hasRemoval = true;
        }

        if (hasRemoval)
            return;

        if (outcome.Attack is not { } attack || attack.Missed || attack.Damage <= 0)
            return;
        if (attack.TargetRevived && (attack.ConsumedItem is null || sendResurrectionRefresh is null))
            throw new InvalidOperationException("A revived generated-NPC attack requires the consumed item and a resurrection refresh callback.");

        var recipients = attack.ViewRecipientConnectionIds ?? Array.Empty<int>();
        var attackFrame = new LegacyMobAttackConfirmation(
            attack.AttackerPositionX,
            attack.AttackerPositionY,
            attack.TargetPositionX,
            attack.TargetPositionY,
            attack.AttackerConnectionId,
            attack.TargetConnectionId,
            attack.Damage,
            skillIndex: checked((short)attack.SkillIndex),
            skillParm: checked((byte)attack.SkillParm),
            motion: checked((byte)attack.Motion)).ToFrame(codec, tick, keyword);
        await SendToViewAsync(world, recipients, attackFrame, cancellationToken).ConfigureAwait(false);

        if (attack.TargetIsNpc)
        {
            if (attack.TargetDied)
            {
                var removeFrame = new RemoveMobConfirmation(
                    checked((ushort)attack.TargetConnectionId), removeType: 1).ToFrame(codec, tick, keyword);
                await SendToViewAsync(world, recipients, removeFrame, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (attack.TargetAffectSnapshot is not { } affectSnapshot)
                throw new InvalidOperationException("A generated-NPC attack requires the target affect snapshot for its score refresh.");
            var scorePayload = new UpdateScoreConfirmation(attack.TargetMobSnapshot, affectSnapshot).ToPayload();
            var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                    scorePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize),
                    LegacyMobCombatState.Read(attack.TargetMobSnapshot).Rsv)
                .ToFrame(codec, tick, keyword, checked((ushort)attack.TargetConnectionId));
            await SendToViewAsync(world, recipients, scoreFrame, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (attack.TargetRevived &&
            attack.ConsumedItem is { } consumedItem &&
            sendResurrectionRefresh is { } refresh)
        {
            await refresh(
                attack.TargetConnectionId,
                attack.TargetMobSnapshot,
                attack.ConsumedItemSlot,
                consumedItem,
                recipients,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (attack.TargetDied && !attack.TargetRevived)
        {
            var deathFrame = new LegacyMobDeathConfirmation(
                attack.TargetConnectionId,
                attack.AttackerConnectionId).ToFrame(codec, tick, keyword);
            await SendToViewAsync(world, recipients, deathFrame, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (world.TryGetResourceState(attack.TargetConnectionId, out var targetState, out var requestedHp, out var requestedMp) &&
            targetState is not null)
        {
            var resourceFrame = new SetHpMpConfirmation(
                targetState.CurrentScore.Hp,
                targetState.CurrentMana,
                requestedHp,
                requestedMp).ToFrame(codec, tick, keyword, checked((ushort)attack.TargetConnectionId));
            await SendToOneAsync(world, attack.TargetConnectionId, resourceFrame, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask RelayMovementAsync(
        WorldHub world,
        LegacyGeneratedNpcMovementOutcome movement,
        LegacyFrameCodec codec,
        uint clientTick,
        byte keywordIndex,
        CancellationToken cancellationToken)
    {
        if (movement.ConnectionId is <= 0 or > ushort.MaxValue ||
            movement.Speed is < 1 or > 6 ||
            movement.Effect is not (0 or 1) ||
            movement.OriginViewConnectionIds.Any(static id => id is <= 0 or > LegacyMobAiPlanner.MaximumPlayerId) ||
            movement.DestinationViewConnectionIds.Any(static id => id is <= 0 or > LegacyMobAiPlanner.MaximumPlayerId) ||
            movement.OriginViewConnectionIds.Distinct().Count() != movement.OriginViewConnectionIds.Count ||
            movement.DestinationViewConnectionIds.Distinct().Count() != movement.DestinationViewConnectionIds.Count)
            return;

        var actionFrame = new ActionRequest(
            movement.FromX,
            movement.FromY,
            Effect: movement.Effect,
            movement.Speed,
            new byte[24],
            movement.ToX,
            movement.ToY).ToFrame(codec, clientTick, keywordIndex, checked((ushort)movement.ConnectionId));
        var oldView = movement.OriginViewConnectionIds.ToHashSet();
        var newView = movement.DestinationViewConnectionIds.ToHashSet();

        // GridMulticast sends the movement into the old view before removing observers who left it.
        foreach (var recipientId in movement.OriginViewConnectionIds.Order())
            await TrySendToViewerAsync(world, recipientId, actionFrame, cancellationToken).ConfigureAwait(false);

        var removeFrame = new RemoveMobConfirmation(checked((ushort)movement.ConnectionId), removeType: 0)
            .ToFrame(codec, clientTick, keywordIndex);
        foreach (var leavingId in oldView.Except(newView).Order())
            await TrySendToViewerAsync(world, leavingId, removeFrame, cancellationToken).ConfigureAwait(false);

        var sourcePayload = new CreateMobConfirmation(
            checked((ushort)movement.ConnectionId),
            movement.ToX,
            movement.ToY,
            movement.MobSnapshot,
            movement.AffectSnapshot,
            npc: true).ToPayload();
        var createFrame = W2ppCreateMobV1Adapter.AdaptPayload(sourcePayload)
            .ToFrame(codec, clientTick, keywordIndex);
        createFrame = LegacyBattleRoyaleCreateMobVisualRelay.Apply(createFrame, movement.BattleRoyaleState, codec);
        foreach (var enteringId in newView.Except(oldView).Order())
        {
            await TrySendToViewerAsync(world, enteringId, createFrame, cancellationToken).ConfigureAwait(false);
            await TrySendToViewerAsync(world, enteringId, actionFrame, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask SendToViewAsync(
        WorldHub world,
        IReadOnlyList<int> recipients,
        ReadOnlyMemory<byte> frame,
        CancellationToken cancellationToken)
    {
        foreach (var connectionId in recipients)
            await SendToOneAsync(world, connectionId, frame, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask SendToOneAsync(
        WorldHub world,
        int connectionId,
        ReadOnlyMemory<byte> frame,
        CancellationToken cancellationToken)
        => _ = await TrySendToViewerAsync(world, connectionId, frame, cancellationToken).ConfigureAwait(false);

    public static async ValueTask<bool> TrySendToViewerAsync(
        WorldHub world,
        int connectionId,
        ReadOnlyMemory<byte> frame,
        CancellationToken cancellationToken)
    {
        try
        {
            return await world.SendAsync(connectionId, frame, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (IOException)
        {
            // A disconnected viewer cannot prevent the other captured viewers from receiving the frame.
            return false;
        }
        catch (ObjectDisposedException)
        {
            // The connection cleanup path owns removal of a disposed participant.
            return false;
        }
    }
}

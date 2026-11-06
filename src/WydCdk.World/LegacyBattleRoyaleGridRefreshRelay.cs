using System.Buffers.Binary;
using WydCdk.Protocol;

namespace WydCdk.World;

public sealed record LegacyBattleRoyaleGridMobSnapshot(
    int ConnectionId,
    short PositionX,
    short PositionY,
    byte[] MobSnapshot,
    byte[] AffectSnapshot,
    bool IsNpc,
    bool IsSummon,
    int PkState,
    IReadOnlyList<LegacyItem>? ClientEquipment = null,
    LegacyAutoTradeSnapshot? AutoTradeSnapshot = null);

public sealed record LegacyBattleRoyaleGridRefreshRecipientSnapshot(
    int ConnectionId,
    IReadOnlyList<LegacyBattleRoyaleGridMobSnapshot> Mobs,
    IReadOnlyList<LegacyRecallItem> Items);

public sealed record LegacyBattleRoyaleGridRefreshActorPlan(int ConnectionId, IReadOnlyList<byte[]> Frames);

public sealed record LegacyBattleRoyaleGridRefreshPlan(IReadOnlyList<LegacyBattleRoyaleGridRefreshActorPlan> Actors);

public sealed record LegacyBattleRoyaleGridRefreshSendResult(IReadOnlyList<int> FailedRecipientIds);

/// <summary>Builds and sends the legacy arena SendGridMob refresh followed by member-only RemoveParty cleanup.</summary>
public static class LegacyBattleRoyaleGridRefreshRelay
{
    /// <summary>TMProject/W2PP define MAX_USER as 1000; SendGridMob and SendPKInfo only accept player slots below it.</summary>
    public const int MaxUserConnectionExclusive = 1_000;

    public static LegacyBattleRoyaleGridRefreshPlan? TryCreate(
        WorldHub world,
        LegacyBattleRoyaleTransition transition,
        LegacyFrameCodec codec,
        uint clientTick,
        byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(codec);
        if (!transition.RefreshGridAndCleanupParties ||
            (transition.PreviousState, transition.State) is not ((0, 1) or (1, 2)))
            return null;

        var snapshots = world.GetBattleRoyaleGridRefreshSnapshots(
            LegacyBattleRoyaleAreaClearRelay.MinX,
            LegacyBattleRoyaleAreaClearRelay.MinY,
            LegacyBattleRoyaleAreaClearRelay.MaxX,
            LegacyBattleRoyaleAreaClearRelay.MaxY);
        var actors = new List<LegacyBattleRoyaleGridRefreshActorPlan>(snapshots.Count);
        foreach (var snapshot in snapshots)
        {
            if (snapshot.ConnectionId is <= 0 or >= MaxUserConnectionExclusive)
                return null;

            var events = new List<GridFrameEvent>();
            foreach (var mob in snapshot.Mobs)
            {
                if (mob.ConnectionId is <= 0 or > ushort.MaxValue ||
                    mob.MobSnapshot.Length < LegacyAccountSnapshot.CharacterStride ||
                    mob.AffectSnapshot.Length != LegacyAccountSnapshot.AffectStride)
                    return null;

                byte[] createMobFrame;
                if (mob.AutoTradeSnapshot is { } autoTradeSnapshot &&
                    LegacyAutoTradeVisualRelay.TryBuild(
                        autoTradeSnapshot,
                        mob.MobSnapshot,
                        mob.AffectSnapshot,
                        codec,
                        clientTick,
                        keywordIndex,
                        out var autoTradeVisual,
                        mob.ClientEquipment) &&
                    autoTradeVisual is not null)
                {
                    createMobFrame = autoTradeVisual.Frame;
                }
                else
                {
                    var sourcePayload = new CreateMobConfirmation(
                        checked((ushort)mob.ConnectionId),
                        mob.PositionX,
                        mob.PositionY,
                        mob.MobSnapshot,
                        mob.AffectSnapshot,
                        mob.IsNpc,
                        mob.IsSummon).ToPayload();
                    var appearance = mob.ClientEquipment is null
                        ? null
                        : LegacyAutoTradeVisualRelay.BuildEquipmentAppearance(mob.MobSnapshot, mob.ClientEquipment);
                    createMobFrame = W2ppCreateMobV1Adapter.AdaptPayload(
                            sourcePayload,
                            appearance?.VisualEquipment,
                            appearance?.AncientCodes)
                        .ToFrame(codec, clientTick, keywordIndex);
                    createMobFrame = LegacyBattleRoyaleCreateMobVisualRelay.Apply(createMobFrame, transition.State, codec);
                }

                events.Add(new GridFrameEvent(
                    checked((ushort)mob.PositionY),
                    checked((ushort)mob.PositionX),
                    checked((long)mob.ConnectionId * 2),
                    mob.ConnectionId,
                    createMobFrame));
                if (mob.ConnectionId < MaxUserConnectionExclusive)
                    events.Add(new GridFrameEvent(
                        checked((ushort)mob.PositionY),
                        checked((ushort)mob.PositionX),
                        checked((long)(mob.ConnectionId * 2) + 1),
                        mob.ConnectionId,
                        new PkInfoConfirmation(mob.ConnectionId, mob.PkState).ToFrame(codec, clientTick, keywordIndex)));
            }

            foreach (var item in snapshot.Items)
            {
                if (item.WireItemId <= LegacyWorldItem.WireIdOffset || item.WireItemId > short.MaxValue || item.Item.Index <= 0)
                    return null;
                events.Add(new GridFrameEvent(
                    item.GridY,
                    item.GridX,
                    long.MaxValue,
                    item.WireItemId,
                    new CreateItemConfirmation(
                        item.GridX,
                        item.GridY,
                        item.WireItemId,
                        item.Item,
                        item.Rotate,
                        item.State,
                        item.Height).ToFrame(codec, clientTick, keywordIndex)));
            }

            var orderedFrames = events
                .OrderBy(static item => item.GridY)
                .ThenBy(static item => item.GridX)
                .ThenBy(static item => item.Order)
                .ThenBy(static item => item.EntityId)
                .Select(static item => item.Frame)
                .ToArray();
            actors.Add(new LegacyBattleRoyaleGridRefreshActorPlan(
                snapshot.ConnectionId,
                Array.AsReadOnly(orderedFrames)));
        }

        return new LegacyBattleRoyaleGridRefreshPlan(Array.AsReadOnly(actors.ToArray()));
    }

    public static async ValueTask<LegacyBattleRoyaleGridRefreshSendResult> SendAsync(
        WorldHub world,
        LegacyBattleRoyaleGridRefreshPlan plan,
        CancellationToken cancellationToken,
        LegacyAccountOperationGate? accountOperationGate = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(plan);

        var codec = LegacyFrameCodec.CreateDefault();
        var failedRecipients = new HashSet<int>();
        var despawnedSummonIds = new HashSet<int>();
        foreach (var actor in plan.Actors)
        {
            foreach (var frame in actor.Frames)
            {
                if (TryReadCreatedMobId(frame, codec, out var mobId) && despawnedSummonIds.Contains(mobId))
                    continue;
                if (!await TrySendAsync(world, actor.ConnectionId, frame, failedRecipients, cancellationToken))
                    break;
            }

            var removedPartyMember = await TryRemovePartyMemberAsync(world, actor.ConnectionId, accountOperationGate, cancellationToken);
            if (!removedPartyMember.Accepted || removedPartyMember.Plan is not { } removal)
                continue;

            var partyRemoval = removal.PartyRemoval;
            foreach (var recipientId in partyRemoval.NotifiedConnectionIds)
            {
                var removedId = recipientId == partyRemoval.RemovedConnectionId ? 0 : partyRemoval.RemovedConnectionId;
                var frame = new PartyRemoveConfirmation(checked((short)removedId))
                    .ToFrame(codec, unchecked((uint)Environment.TickCount64), (byte)Random.Shared.Next(256), 30_000);
                await TrySendAsync(world, recipientId, frame, failedRecipients, cancellationToken);
            }

            foreach (var summon in removal.DespawnedSummons)
            {
                if (summon.ConnectionId is <= 0 or > ushort.MaxValue)
                    continue;
                despawnedSummonIds.Add(summon.ConnectionId);
                var frame = new RemoveMobConfirmation(checked((ushort)summon.ConnectionId))
                    .ToFrame(codec, unchecked((uint)Environment.TickCount64), (byte)Random.Shared.Next(256));
                foreach (var recipientId in summon.NotifiedConnectionIds)
                    await TrySendAsync(world, recipientId, frame, failedRecipients, cancellationToken);
            }
        }

        return new LegacyBattleRoyaleGridRefreshSendResult(Array.AsReadOnly(failedRecipients.Order().ToArray()));
    }

    private static async ValueTask<(bool Accepted, LegacyBattleRoyalePartyMemberRemovalPlan? Plan)> TryRemovePartyMemberAsync(
        WorldHub world,
        int connectionId,
        LegacyAccountOperationGate? accountOperationGate,
        CancellationToken cancellationToken)
    {
        if (accountOperationGate is null)
        {
            var accepted = world.TryRemoveBattleRoyalePartyMember(
                connectionId,
                LegacyBattleRoyaleAreaClearRelay.MinX,
                LegacyBattleRoyaleAreaClearRelay.MinY,
                LegacyBattleRoyaleAreaClearRelay.MaxX,
                LegacyBattleRoyaleAreaClearRelay.MaxY,
                out var plan);
            return (accepted, plan);
        }

        while (true)
        {
            var accountNames = world.GetPartyMemberAccountNames(connectionId);
            if (accountNames.Count == 0)
            {
                var accepted = world.TryRemoveBattleRoyalePartyMember(
                    connectionId,
                    LegacyBattleRoyaleAreaClearRelay.MinX,
                    LegacyBattleRoyaleAreaClearRelay.MinY,
                    LegacyBattleRoyaleAreaClearRelay.MaxX,
                    LegacyBattleRoyaleAreaClearRelay.MaxY,
                    out var plan);
                return (accepted, plan);
            }

            await using var lease = await accountOperationGate.AcquireAsync(accountNames, cancellationToken);
            var currentAccounts = world.GetPartyMemberAccountNames(connectionId);
            if (currentAccounts.Any(accountName => !lease.Covers(accountOperationGate, accountName)))
                continue;

            var removed = world.TryRemoveBattleRoyalePartyMember(
                connectionId,
                LegacyBattleRoyaleAreaClearRelay.MinX,
                LegacyBattleRoyaleAreaClearRelay.MinY,
                LegacyBattleRoyaleAreaClearRelay.MaxX,
                LegacyBattleRoyaleAreaClearRelay.MaxY,
                out var removal);
            return (removed, removal);
        }
    }

    private static bool TryReadCreatedMobId(byte[] frame, LegacyFrameCodec codec, out int connectionId)
    {
        var decoded = codec.Decode(frame);
        connectionId = 0;
        if (decoded.Header.Type != CreateMobConfirmationV769.MessageType ||
            decoded.Payload.Length < CreateMobConfirmationV769.MobIdOffset + sizeof(ushort))
            return false;

        connectionId = BinaryPrimitives.ReadUInt16LittleEndian(decoded.Payload.Span[CreateMobConfirmationV769.MobIdOffset..]);
        return true;
    }

    private static async ValueTask<bool> TrySendAsync(
        WorldHub world,
        int recipientId,
        byte[] frame,
        HashSet<int> failedRecipients,
        CancellationToken cancellationToken)
    {
        if (failedRecipients.Contains(recipientId))
            return false;

        try
        {
            await world.SendAsync(recipientId, frame, cancellationToken);
            return true;
        }
        catch (IOException)
        {
            failedRecipients.Add(recipientId);
            return false;
        }
        catch (ObjectDisposedException)
        {
            failedRecipients.Add(recipientId);
            return false;
        }
    }

    private sealed record GridFrameEvent(ushort GridY, ushort GridX, long Order, int EntityId, byte[] Frame);
}

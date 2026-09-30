using WydCdk.Protocol;

namespace WydCdk.World;

public sealed record LegacyTradeCloseReservation(Guid Token, int ConnectionId, int OpponentId, bool ActionTriggered = false);
public enum LegacyTradeClosePrepareResult { Prepared, ParticipantNotFound, CombatStateUnavailable, NotAlive, Busy, PairChanged }
public enum LegacyAutoTradeCloseResult { Closed, NoShop, SessionNotPlaying, NotAlive, Conflict, PersistenceUnavailable, PendingRecovery }
public sealed record LegacyAutoTradeCloseOutcome(LegacyAutoTradeCloseResult Result,
    LegacyTradeCloseOutcome? Trade, IReadOnlyList<LegacyAutoTradeListingHandle> ClosedListings)
{
    public bool ActionTriggered { get; init; }
}
public sealed record LegacyAutoTradePendingClose(LegacyTradeCloseReservation Reservation,
    LegacyAutoTradeCloseRequest Request, IReadOnlyList<LegacyAutoTradeListingHandle> Listings)
{
    public IReadOnlyList<LegacyAutoTradeOwner> AffectedOwners { get; init; } = [];
    // Book-only close intent for an absent runtime owner; no WorldHub reservation.
    internal bool RuntimeDetached { get; init; }
}

public static class LegacyAutoTradeCloseCoordinator
{
    public static async ValueTask<LegacyAutoTradeCloseDispatchPlan> CloseAndCaptureAsync(int connectionId,
        LoginSessionRegistry sessions, WorldHub world, LegacyAutoTradeBook book,
        ILegacyAutoTradeStateStore? stateStore, LegacyFrameCodec codec, uint clientTick, byte keywordIndex,
        CancellationToken cancellationToken = default)
        => await CloseAndCaptureCoreAsync(connectionId, sessions, world, book, stateStore, codec, clientTick,
            keywordIndex, actionTriggered: false, cancellationToken);

    public static async ValueTask<LegacyAutoTradeCloseDispatchPlan> CloseForActionAndCaptureAsync(int connectionId,
        LoginSessionRegistry sessions, WorldHub world, LegacyAutoTradeBook book,
        ILegacyAutoTradeStateStore? stateStore, LegacyFrameCodec codec, CancellationToken cancellationToken = default)
        => await CloseAndCaptureCoreAsync(connectionId, sessions, world, book, stateStore, codec, 0, 0,
            actionTriggered: true, cancellationToken);

    private static async ValueTask<LegacyAutoTradeCloseDispatchPlan> CloseAndCaptureCoreAsync(int connectionId,
        LoginSessionRegistry sessions, WorldHub world, LegacyAutoTradeBook book,
        ILegacyAutoTradeStateStore? stateStore, LegacyFrameCodec codec, uint clientTick, byte keywordIndex,
        bool actionTriggered, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codec);
        IReadOnlyList<LegacyAutoTradeCloseFramePlan> frames = [];
        var outcome = await CloseCoreAsync(connectionId, sessions, world, book, stateStore, actionTriggered, cancellationToken,
            (lease, result) =>
            {
                if (result.Trade is not { } trade || !lease.Covers(trade.ConnectionId) ||
                    trade.OpponentId > 0 && !lease.Covers(trade.OpponentId)) return;
                if (LegacyAutoTradeCloseRelay.TryBuild(world, result, codec, clientTick, keywordIndex, out var captured))
                    frames = captured;
            });
        return new(outcome, frames);
    }

    public static ValueTask<LegacyAutoTradeCloseOutcome> CloseAsync(int connectionId,
        LoginSessionRegistry sessions, WorldHub world, LegacyAutoTradeBook book,
        ILegacyAutoTradeStateStore? stateStore, CancellationToken cancellationToken = default) =>
        CloseCoreAsync(connectionId, sessions, world, book, stateStore, actionTriggered: false, cancellationToken, null);

    private static async ValueTask<LegacyAutoTradeCloseOutcome> CloseCoreAsync(int connectionId,
        LoginSessionRegistry sessions, WorldHub world, LegacyAutoTradeBook book,
        ILegacyAutoTradeStateStore? stateStore, bool actionTriggered, CancellationToken cancellationToken,
        Action<LegacyAutoTradeOperationLease, LegacyAutoTradeCloseOutcome>? capture)
    {
        ArgumentNullException.ThrowIfNull(sessions); ArgumentNullException.ThrowIfNull(world); ArgumentNullException.ThrowIfNull(book);
        if (connectionId is <= 0 or > ushort.MaxValue) return Failure(LegacyAutoTradeCloseResult.SessionNotPlaying);
        sessions.TryGet(connectionId, out var discoveredSession);
        var identity = world.CaptureTradeCloseIdentity(connectionId);
        var opponentId = world.GetTradeOpponentId(connectionId);
        var opponentIdentity = opponentId > 0 ? world.CaptureTradeCloseIdentity(opponentId) : null;
        await using var lease = await book.AcquireAsync(opponentId > 0 ? [connectionId, opponentId] : [connectionId], cancellationToken);
        LegacyAutoTradeCloseOutcome Captured(LegacyAutoTradeCloseOutcome outcome)
        {
            capture?.Invoke(lease, outcome);
            return outcome;
        }
        if (book.TryGetPendingClose(lease, connectionId, out _))
        {
            await lease.DisposeAsync(); // recovery acquires the saved pair, including after logout
            return stateStore is null ? Failure(LegacyAutoTradeCloseResult.PendingRecovery) :
                await LegacyAutoTradeCloseRecovery.RecoverAndCaptureAsync(connectionId, sessions, world, book, stateStore, cancellationToken, capture);
        }
        if (!sessions.TryGet(connectionId, out var session) || session is null || session.State != LoginSessionState.Playing ||
            session.AccountName is null || session.CharacterSlot is < 0 or > 3)
            return Failure(LegacyAutoTradeCloseResult.SessionNotPlaying);
        if (!world.IsTradeCloseIdentityCurrent(connectionId, identity) || discoveredSession is null ||
            session.AccountName != discoveredSession.AccountName || session.CharacterSlot != discoveredSession.CharacterSlot ||
            !ReferenceEquals(session.AdapterName, discoveredSession.AdapterName) || world.GetTradeOpponentId(connectionId) != opponentId ||
            opponentId > 0 && !world.IsTradeCloseIdentityCurrent(opponentId, opponentIdentity))
            return Failure(LegacyAutoTradeCloseResult.Conflict);

        var handles = new List<LegacyAutoTradeListingHandle>();
        foreach (var id in opponentId > 0 ? new[] { connectionId, opponentId } : [connectionId])
        {
            if (!book.TryGet(lease, id, out var handle) || handle is null) continue;
            if (!sessions.TryGet(id, out var owner) || owner?.State != LoginSessionState.Playing || owner.AccountName is null ||
                handle.Owner != new LegacyAutoTradeOwner(owner.AccountName.Trim().ToUpperInvariant(), owner.CharacterSlot))
                return Failure(LegacyAutoTradeCloseResult.Conflict);
            handles.Add(handle);
        }
        // Invalid/duplicate durable owners must fail before reserving the pair.
        if (handles.Select(handle => handle.Owner).Distinct().Count() != handles.Count)
            return Failure(LegacyAutoTradeCloseResult.Conflict);
        if (actionTriggered && opponentId == 0 && handles.Count == 0)
            return Captured(new(LegacyAutoTradeCloseResult.NoShop, null, []) { ActionTriggered = true });
        LegacyTradeCloseReservation? reservation;
        var prepared = actionTriggered
            ? world.TryReserveTradeCloseForAction(connectionId, opponentId, out reservation)
            : world.TryReserveTradeClose(connectionId, opponentId, out reservation);
        if (prepared != LegacyTradeClosePrepareResult.Prepared || reservation is null)
            return Failure(prepared == LegacyTradeClosePrepareResult.NotAlive ? LegacyAutoTradeCloseResult.NotAlive : LegacyAutoTradeCloseResult.Conflict);
        if (handles.Count == 0)
        {
            return Captured(world.TryFinishTradeClose(reservation, out var trade) ?
                new(LegacyAutoTradeCloseResult.NoShop, trade, []) { ActionTriggered = reservation.ActionTriggered } :
                Failure(LegacyAutoTradeCloseResult.Conflict));
        }
        if (stateStore is not null && stateStore is not ILegacyAutoTradeCloseStore)
        {
            world.TryAbortTradeClose(reservation);
            return Failure(LegacyAutoTradeCloseResult.PersistenceUnavailable);
        }
        var request = new LegacyAutoTradeCloseRequest(handles.Select(handle => new LegacyAutoTradePersistedListing(
            handle.Owner.AccountName, handle.Owner.CharacterSlot, handle.Snapshot.PositionX, handle.Snapshot.PositionY,
            handle.Snapshot.Title, handle.Snapshot.Items, handle.Snapshot.CarryPositions, handle.Snapshot.Prices, handle.Snapshot.Tax)).ToArray());
        var owners = handles.Select(handle => handle.Owner).ToList();
        foreach (var id in opponentId > 0 ? new[] { connectionId, opponentId } : [connectionId])
            if (sessions.TryGet(id, out var affected) && affected?.AccountName is not null && affected.CharacterSlot is >= 0 and <= 3)
                owners.Add(new(affected.AccountName.Trim().ToUpperInvariant(), affected.CharacterSlot));
        var pending = new LegacyAutoTradePendingClose(reservation, request, handles)
        {
            AffectedOwners = Array.AsReadOnly(owners.Distinct().ToArray()),
        };
        if (!book.TrySetPendingClose(lease, pending))
        {
            world.TryAbortTradeClose(reservation);
            return Failure(LegacyAutoTradeCloseResult.Conflict);
        }
        if (stateStore is null) return Captured(Finish(world, book, lease, pending));
        var closeStore = (ILegacyAutoTradeCloseStore)stateStore;
        LegacyAutoTradeCloseStoreResult result;
        try { result = await closeStore.RemoveExpectedAsync(request, cancellationToken); }
        catch (Exception) { result = LegacyAutoTradeCloseStoreResult.Ambiguous; }
        if (result is LegacyAutoTradeCloseStoreResult.Removed or LegacyAutoTradeCloseStoreResult.AlreadyAbsent)
            return Captured(Finish(world, book, lease, pending)); // no socket cancellation after durable confirmation
        if (result == LegacyAutoTradeCloseStoreResult.Ambiguous)
            return Captured(await LegacyAutoTradeCloseRecovery.ReconcileUnderLeaseAsync(world, book, lease, pending, closeStore));
        return Abort(world, book, lease, pending, result == LegacyAutoTradeCloseStoreResult.Conflict ?
            LegacyAutoTradeCloseResult.Conflict : LegacyAutoTradeCloseResult.PersistenceUnavailable);
    }

    internal static LegacyAutoTradeCloseOutcome Finish(WorldHub world, LegacyAutoTradeBook book,
        LegacyAutoTradeOperationLease lease, LegacyAutoTradePendingClose pending)
    {
        if (!book.TryFinishPendingClose(lease, pending)) return Failure(LegacyAutoTradeCloseResult.PendingRecovery);
        if (pending.RuntimeDetached) return new(LegacyAutoTradeCloseResult.Closed, null, []);
        var online = world.TryFinishTradeClose(pending.Reservation, out var trade);
        return new(LegacyAutoTradeCloseResult.Closed, trade, online ? pending.Listings : [])
        {
            ActionTriggered = pending.Reservation.ActionTriggered,
        };
    }

    internal static LegacyAutoTradeCloseOutcome Abort(WorldHub world, LegacyAutoTradeBook book,
        LegacyAutoTradeOperationLease lease, LegacyAutoTradePendingClose pending, LegacyAutoTradeCloseResult result)
    {
        if (!book.TryClearPendingClose(lease, pending)) return Failure(LegacyAutoTradeCloseResult.PendingRecovery);
        if (!pending.RuntimeDetached) world.TryAbortTradeClose(pending.Reservation);
        return Failure(result);
    }

    internal static LegacyAutoTradeCloseOutcome Failure(LegacyAutoTradeCloseResult result) => new(result, null, []);
}

public sealed record LegacyAutoTradeCloseDispatchPlan(LegacyAutoTradeCloseOutcome Outcome,
    IReadOnlyList<LegacyAutoTradeCloseFramePlan> Frames);

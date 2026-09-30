namespace WydCdk.World;

public static class LegacyAutoTradeCloseRecovery
{
    public static async ValueTask<LegacyAutoTradeCloseDispatchPlan> RecoverOwnerAndCaptureAsync(
        LegacyAutoTradeOwner owner, LoginSessionRegistry sessions, WorldHub world, LegacyAutoTradeBook book,
        ILegacyAutoTradeStateStore stateStore, WydCdk.Protocol.LegacyFrameCodec codec, uint clientTick, byte keywordIndex,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessions); ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(book); ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(codec);
        if (!book.TryDiscoverPendingClose(owner, out var discovered) || discovered is null)
            return new(LegacyAutoTradeCloseCoordinator.Failure(LegacyAutoTradeCloseResult.NoShop), []);
        if (stateStore is not ILegacyAutoTradeCloseStore closeStore)
            return new(LegacyAutoTradeCloseCoordinator.Failure(LegacyAutoTradeCloseResult.PendingRecovery), []);
        await using var lease = await book.AcquireAsync(LegacyAutoTradeBook.PendingIds(discovered), cancellationToken);
        if (!book.TryGetPendingClose(lease, discovered.Reservation.ConnectionId, out var pending) || pending is null ||
            pending.Reservation != discovered.Reservation || !ReferenceEquals(pending.Request, discovered.Request))
            return new(LegacyAutoTradeCloseCoordinator.Failure(LegacyAutoTradeCloseResult.Conflict), []);
        var outcome = await ReconcileUnderLeaseAsync(world, book, lease, pending, closeStore);
        IReadOnlyList<LegacyAutoTradeCloseFramePlan> frames = [];
        if (outcome.Trade is { } trade && lease.Covers(trade.ConnectionId) &&
            (trade.OpponentId <= 0 || lease.Covers(trade.OpponentId)))
            LegacyAutoTradeCloseRelay.TryBuild(world, outcome, codec, clientTick, keywordIndex, out frames);
        return new(outcome, frames);
    }

    public static ValueTask<LegacyAutoTradeCloseOutcome> RecoverAsync(int connectionId,
        LoginSessionRegistry sessions, WorldHub world, LegacyAutoTradeBook book,
        ILegacyAutoTradeStateStore stateStore, CancellationToken cancellationToken = default) =>
        RecoverAndCaptureAsync(connectionId, sessions, world, book, stateStore, cancellationToken, null);

    internal static async ValueTask<LegacyAutoTradeCloseOutcome> RecoverAndCaptureAsync(int connectionId,
        LoginSessionRegistry sessions, WorldHub world, LegacyAutoTradeBook book,
        ILegacyAutoTradeStateStore stateStore, CancellationToken cancellationToken,
        Action<LegacyAutoTradeOperationLease, LegacyAutoTradeCloseOutcome>? capture)
    {
        ArgumentNullException.ThrowIfNull(sessions); ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(book); ArgumentNullException.ThrowIfNull(stateStore);
        await using var discovery = await book.AcquireAsync([connectionId], cancellationToken);
        if (!book.TryGetPendingClose(discovery, connectionId, out var discovered) || discovered is null)
            return LegacyAutoTradeCloseCoordinator.Failure(LegacyAutoTradeCloseResult.NoShop);
        await discovery.DisposeAsync();
        if (stateStore is not ILegacyAutoTradeCloseStore closeStore)
            return LegacyAutoTradeCloseCoordinator.Failure(LegacyAutoTradeCloseResult.PendingRecovery);
        await using var lease = await book.AcquireAsync(LegacyAutoTradeBook.PendingIds(discovered), cancellationToken);
        if (!book.TryGetPendingClose(lease, connectionId, out var pending) || pending is null || pending.Reservation != discovered.Reservation)
            return LegacyAutoTradeCloseCoordinator.Failure(LegacyAutoTradeCloseResult.Conflict);
        var outcome = await ReconcileUnderLeaseAsync(world, book, lease, pending, closeStore);
        capture?.Invoke(lease, outcome);
        return outcome;
    }

    internal static async ValueTask<LegacyAutoTradeCloseOutcome> ReconcileUnderLeaseAsync(WorldHub world,
        LegacyAutoTradeBook book, LegacyAutoTradeOperationLease lease, LegacyAutoTradePendingClose pending,
        ILegacyAutoTradeCloseStore closeStore)
    {
        LegacyAutoTradeCloseReadState state;
        try { state = await closeStore.ReadCloseStateAsync(pending.Request, CancellationToken.None); }
        catch (Exception) { state = LegacyAutoTradeCloseReadState.Unknown; }
        return state switch
        {
            LegacyAutoTradeCloseReadState.AllAbsent => LegacyAutoTradeCloseCoordinator.Finish(world, book, lease, pending),
            LegacyAutoTradeCloseReadState.AllUnchanged => LegacyAutoTradeCloseCoordinator.Abort(world, book, lease, pending, LegacyAutoTradeCloseResult.PersistenceUnavailable),
            _ => LegacyAutoTradeCloseCoordinator.Failure(LegacyAutoTradeCloseResult.PendingRecovery),
        };
    }
}

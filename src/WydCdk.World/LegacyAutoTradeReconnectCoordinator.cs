namespace WydCdk.World;

/// <summary>
/// Applies the reconnect boundary for the persisted offline-shop extension.
/// The W2PP login path clears the character trade state before entering the
/// field; it does not define a second live shop for the same character.
/// </summary>
public static class LegacyAutoTradeReconnectCoordinator
{
    public static async ValueTask<LegacyAutoTradeReconnectDispatchPlan> CloseAndRecoverAsync(
        LoginSessionRegistry sessions, WorldHub world, LegacyAutoTradeBook book,
        ILegacyAutoTradeStateStore stateStore, string accountName, int characterSlot,
        WydCdk.Protocol.LegacyFrameCodec codec, uint clientTick, byte keywordIndex,
        CancellationToken cancellationToken = default)
    {
        var recovery = await LegacyAutoTradeCloseRecovery.RecoverOwnerAndCaptureAsync(new(accountName, characterSlot),
            sessions, world, book, stateStore, codec, clientTick, keywordIndex, cancellationToken);
        if (recovery.Outcome.Result is not LegacyAutoTradeCloseResult.Closed and not LegacyAutoTradeCloseResult.NoShop &&
            !(recovery.Outcome.Result == LegacyAutoTradeCloseResult.PersistenceUnavailable && !book.IsOwnerPending(new(accountName, characterSlot))))
            return new(new(LegacyAutoTradeReconnectResult.PersistenceUnavailable, []), recovery.Frames);
        return new(await CloseAsync(world, book, stateStore, accountName, characterSlot, cancellationToken), recovery.Frames);
    }

    public static async ValueTask<LegacyAutoTradeReconnectOutcome> CloseAsync(
        WorldHub world,
        LegacyAutoTradeBook book,
        ILegacyAutoTradeStateStore stateStore,
        string accountName,
        int characterSlot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(stateStore);
        if (book.IsOwnerPending(new(accountName, characterSlot)))
            return new(LegacyAutoTradeReconnectResult.PersistenceUnavailable, []);

        // A live handle is not an offline NPC. An absent runtime owner may be
        // cleaned only by expected delete with a pending owner barrier.
        var owner = new LegacyAutoTradeOwner(accountName.Trim().ToUpperInvariant(), characterSlot);
        if (HasNonOfflineHandle(world, book, owner))
            return await CloseDetachedOwnerAsync(world, book, stateStore, owner, cancellationToken);

        var candidates = world.GetNpcSnapshots().Where(npc => npc.AutoTradeSnapshot is not null &&
            npc.AutoTradeCharacterSlot == characterSlot &&
            string.Equals(npc.AutoTradeOwnerAccount, accountName.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        // Discovery is not authorization: verify identity again after acquiring all runtime ids.
        await using var lease = candidates.Length == 0 ? null :
            await book.AcquireAsync(candidates.Select(npc => npc.ConnectionId), cancellationToken);
        if (book.IsOwnerPending(new(accountName, characterSlot)) || HasNonOfflineHandle(world, book, owner))
            return new(LegacyAutoTradeReconnectResult.PersistenceUnavailable, []);

        var persisted = await stateStore.ReadAsync(accountName, characterSlot, cancellationToken);
        if (persisted is not null)
        {
            var removeResult = await stateStore.RemoveAsync(accountName, characterSlot, cancellationToken);
            if (removeResult is not LegacyAutoTradeStateResult.Removed and not LegacyAutoTradeStateResult.NotFound)
                return new(LegacyAutoTradeReconnectResult.PersistenceUnavailable, []);
        }

        var closed = new List<LegacyAutoTradeReconnectClosure>();
        foreach (var candidate in candidates)
        {
            if (!world.TryGetNpcSnapshot(candidate.ConnectionId, out var npc) || npc is null) continue;
            if (npc.AutoTradeSnapshot is null ||
                npc.AutoTradeCharacterSlot != characterSlot ||
                !string.Equals(npc.AutoTradeOwnerAccount, accountName.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            // Capture the recipients while the NPC is still registered. After
            // removal, GetParticipantIdsInNpcView cannot resolve its position.
            var recipients = world.GetParticipantIdsInNpcView(npc.ConnectionId);
            if (lease is null || !book.TryGet(lease, npc.ConnectionId, out var handle) || handle is null ||
                handle.Owner != new LegacyAutoTradeOwner(accountName.Trim().ToUpperInvariant(), characterSlot) ||
                !book.TryStop(lease, handle)) continue;
            var snapshot = handle.Snapshot;
            if (!world.TryRemoveNpc(npc.ConnectionId, out _))
                continue;

            closed.Add(new(snapshot, recipients));
        }

        return new(
            persisted is null && closed.Count == 0
                ? LegacyAutoTradeReconnectResult.NoListing
                : LegacyAutoTradeReconnectResult.Closed,
            closed);
    }

    private static bool HasNonOfflineHandle(WorldHub world, LegacyAutoTradeBook book, LegacyAutoTradeOwner owner) =>
        book.Snapshot().Any(handle => handle.Owner == owner &&
            (!world.TryGetNpcSnapshot(handle.Snapshot.ConnectionId, out var npc) || npc is null ||
                npc.AutoTradeSnapshot is null || npc.AutoTradeCharacterSlot != owner.CharacterSlot ||
                !string.Equals(npc.AutoTradeOwnerAccount, owner.AccountName, StringComparison.OrdinalIgnoreCase)));

    private static async ValueTask<LegacyAutoTradeReconnectOutcome> CloseDetachedOwnerAsync(WorldHub world,
        LegacyAutoTradeBook book, ILegacyAutoTradeStateStore store, LegacyAutoTradeOwner owner, CancellationToken cancellationToken)
    {
        LegacyAutoTradeReconnectOutcome Unavailable() => new(LegacyAutoTradeReconnectResult.PersistenceUnavailable, []);
        if (store is not ILegacyAutoTradeCloseStore closeStore) return Unavailable();
        var discovered = book.Snapshot().Where(handle => handle.Owner == owner).ToArray();
        // Duplicate durable owners cannot be safely represented by a one-owner delete.
        if (discovered.Length != 1) return Unavailable();
        var candidate = discovered[0]; var id = candidate.Snapshot.ConnectionId;
        await using var lease = await book.AcquireAsync([id], cancellationToken);
        if (book.IsOwnerPending(owner) || !book.TryGet(lease, id, out var current) || current is null ||
            current.Generation != candidate.Generation || current.Owner != owner ||
            book.Snapshot().Count(handle => handle.Owner == owner) != 1 ||
            world.CaptureTradeCloseIdentity(id) is not null || world.TryGetNpcSnapshot(id, out _))
            return Unavailable();
        var snapshot = current.Snapshot;
        var request = new LegacyAutoTradeCloseRequest([new LegacyAutoTradePersistedListing(owner.AccountName, owner.CharacterSlot,
            snapshot.PositionX, snapshot.PositionY, snapshot.Title, snapshot.Items, snapshot.CarryPositions, snapshot.Prices, snapshot.Tax)]);
        var pending = new LegacyAutoTradePendingClose(new(Guid.NewGuid(), id, 0), request, [current])
        {
            AffectedOwners = Array.AsReadOnly(new[] { owner }), RuntimeDetached = true,
        };
        // Blocks every runtime id for this owner until the durable result is known.
        if (!book.TrySetPendingClose(lease, pending)) return Unavailable();
        LegacyAutoTradeCloseStoreResult result;
        try { result = await closeStore.RemoveExpectedAsync(request, cancellationToken); }
        catch (Exception) { result = LegacyAutoTradeCloseStoreResult.Ambiguous; }
        LegacyAutoTradeCloseOutcome outcome;
        if (result is LegacyAutoTradeCloseStoreResult.Removed or LegacyAutoTradeCloseStoreResult.AlreadyAbsent)
            outcome = LegacyAutoTradeCloseCoordinator.Finish(world, book, lease, pending);
        else if (result == LegacyAutoTradeCloseStoreResult.Ambiguous)
            outcome = await LegacyAutoTradeCloseRecovery.ReconcileUnderLeaseAsync(world, book, lease, pending, closeStore);
        else
            outcome = LegacyAutoTradeCloseCoordinator.Abort(world, book, lease, pending, LegacyAutoTradeCloseResult.PersistenceUnavailable);
        return outcome.Result == LegacyAutoTradeCloseResult.Closed
            ? new(LegacyAutoTradeReconnectResult.Closed, []) : Unavailable();
    }
}

public sealed record LegacyAutoTradeReconnectOutcome(
    LegacyAutoTradeReconnectResult Result,
    IReadOnlyList<LegacyAutoTradeReconnectClosure> ClosedListings);

public sealed record LegacyAutoTradeReconnectDispatchPlan(
    LegacyAutoTradeReconnectOutcome Outcome,
    IReadOnlyList<LegacyAutoTradeCloseFramePlan> RecoveryFrames);

public sealed record LegacyAutoTradeReconnectClosure(
    LegacyAutoTradeSnapshot Snapshot,
    IReadOnlyList<int> RecipientIds);

public enum LegacyAutoTradeReconnectResult
{
    NoListing,
    Closed,
    PersistenceUnavailable,
}

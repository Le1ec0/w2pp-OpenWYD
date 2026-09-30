namespace WydCdk.World;

/// <summary>Result of coordinating runtime CAS and durable account commit.</summary>
public enum LegacyAutoTradePurchaseExecutionResult
{
    Accepted,
    PersistenceUnavailable,
    InvalidPlan,
    ListingChanged,
    OfflineVisualChanged,
    BuyerStateChanged,
    CommitRejected,
    RollbackFailed,
    PendingRecovery,
}

/// <summary>
/// All state read before the execution boundary. ExpectedBuyerMob is the live
/// WorldHub snapshot; ExpectedPersistedBuyerMob is the storage baseline read by
/// the host. The executor does not read sockets or account snapshots itself,
/// which keeps the ordering of reads in the host explicit and testable.
/// </summary>
public sealed record LegacyAutoTradePurchaseExecutionRequest(
    int BuyerConnectionId,
    LegacyAutoTradePurchaseContext Context,
    string BuyerAccountName,
    int BuyerCharacterSlot,
    byte[] ExpectedBuyerMob,
    byte[] ExpectedPersistedBuyerMob,
    short BuyerPositionX,
    short BuyerPositionY,
    byte[] BuyerMobExtra,
    string SellerAccountName,
    int SellerCharacterSlot,
    LegacyAutoTradePersistedListing ExpectedListing,
    bool TargetIsOfflineNpc,
    long ExpectedGeneration);

public sealed record LegacyAutoTradePurchaseExecutionOutcome(
    LegacyAutoTradePurchasePlan Plan,
    LegacyAutoTradeBuyerPurchaseOutcome Buyer,
    LegacyAutoTradeSnapshot OriginalListing,
    LegacyAutoTradeSnapshot UpdatedListing,
    LegacyAutoTradePersistedListing UpdatedPersistedListing,
    bool OfflineVisualUpdated);

public sealed record LegacyAutoTradePurchaseExecution(
    LegacyAutoTradePurchaseExecutionResult Result,
    LegacyAutoTradePurchaseExecutionOutcome? Outcome);

/// <summary>
/// Coordinates the purchase without emitting a client frame. The order is:
/// book CAS, optional offline-NPC CAS, buyer MOB CAS, durable account CAS,
/// then rollback in reverse order on any rejected commit.
/// </summary>
public static class LegacyAutoTradePurchaseExecutor
{
    public static async ValueTask<LegacyAutoTradePurchaseExecution> ExecuteAsync(
        WorldHub world,
        LegacyAutoTradeBook book,
        ILegacyAutoTradePurchaseCommitStore? commitStore,
        LegacyAutoTradePurchaseExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(request);
        request = Freeze(request);
        await using var lease = await book.AcquireAsync(
            [request.BuyerConnectionId, request.Context.TargetId], cancellationToken);
        return await ExecuteUnderLeaseAsync(world, book, lease, commitStore, request, cancellationToken);
    }

    internal static async ValueTask<LegacyAutoTradePurchaseExecution> ExecuteUnderLeaseAsync(
        WorldHub world,
        LegacyAutoTradeBook book,
        LegacyAutoTradeOperationLease lease,
        ILegacyAutoTradePurchaseCommitStore? commitStore,
        LegacyAutoTradePurchaseExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(request);

        request = Freeze(request);

        if (commitStore is null)
            return new(LegacyAutoTradePurchaseExecutionResult.PersistenceUnavailable, null);

        if (book.TryGetPendingClose(lease, request.BuyerConnectionId, out _) ||
            book.IsOwnerPending(new(request.BuyerAccountName, request.BuyerCharacterSlot)))
            return new(LegacyAutoTradePurchaseExecutionResult.PendingRecovery, null);

        if (!lease.Covers(request.BuyerConnectionId) ||
            !book.TryGet(lease, request.Context.TargetId, out var original) || original is null ||
            original.Generation != request.ExpectedGeneration ||
            original.Owner != new LegacyAutoTradeOwner(request.SellerAccountName.Trim().ToUpperInvariant(), request.SellerCharacterSlot) ||
            !SameListing(original.Snapshot, request.Context.Listing))
            return new(LegacyAutoTradePurchaseExecutionResult.ListingChanged, null);

        var planResult = LegacyAutoTradePurchasePlanBuilder.TryBuild(request.Context, out var plan);
        if (planResult != LegacyAutoTradePurchaseResult.Accepted || plan is null)
            return new(LegacyAutoTradePurchaseExecutionResult.InvalidPlan, null);

        var bookResult = book.TryApplyPurchase(lease, original, plan, out var updatedHandle);
        if (bookResult != LegacyAutoTradePurchaseResult.Accepted || updatedHandle is null)
            return new(LegacyAutoTradePurchaseExecutionResult.ListingChanged, null);
        var updatedListing = updatedHandle.Snapshot;

        var offlineVisualUpdated = false;
        if (request.TargetIsOfflineNpc)
        {
            offlineVisualUpdated = world.TryUpdateAutoTradeNpcSnapshot(
                request.Context.TargetId,
                request.Context.Listing,
                updatedListing);
            if (!offlineVisualUpdated)
            {
                return book.TryRollbackPurchase(lease, updatedHandle, original)
                    ? new(LegacyAutoTradePurchaseExecutionResult.OfflineVisualChanged, null)
                    : new(LegacyAutoTradePurchaseExecutionResult.RollbackFailed, null);
            }
        }

        var buyerResult = world.TryApplyAutoTradeBuyerPurchase(
            request.BuyerConnectionId,
            plan,
            request.ExpectedBuyerMob,
            out var buyerOutcome);
        if (buyerResult != LegacyAutoTradeBuyerPurchaseResult.Accepted || buyerOutcome is null)
        {
            var visualRolledBack = !offlineVisualUpdated || world.TryUpdateAutoTradeNpcSnapshot(
                request.Context.TargetId,
                updatedListing,
                request.Context.Listing);
            var bookRolledBack = book.TryRollbackPurchase(
                lease, updatedHandle, original);
            return visualRolledBack && bookRolledBack
                ? new(LegacyAutoTradePurchaseExecutionResult.BuyerStateChanged, null)
                : new(LegacyAutoTradePurchaseExecutionResult.RollbackFailed, null);
        }

        var updatedPersistedListing = ToPersistedListing(
            request.SellerAccountName,
            request.SellerCharacterSlot,
            updatedListing);
        var commitRequest = new LegacyAutoTradePurchaseCommitRequest(
            request.BuyerAccountName,
            request.BuyerCharacterSlot,
            request.ExpectedBuyerMob,
            request.ExpectedPersistedBuyerMob,
            buyerOutcome.MobSnapshot,
            request.BuyerPositionX,
            request.BuyerPositionY,
            request.BuyerMobExtra,
            request.SellerAccountName,
            request.SellerCharacterSlot,
            request.Context.SellerCoin,
            plan,
            request.ExpectedListing,
            updatedPersistedListing);

        LegacyAutoTradePurchaseCommitResult commitResult;
        try
        {
            commitResult = await commitStore.TryCommitAsync(commitRequest, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!TryRollback(world, book, lease, original, updatedHandle, request, buyerOutcome, offlineVisualUpdated))
                return new(LegacyAutoTradePurchaseExecutionResult.RollbackFailed, null);
            throw;
        }
        catch
        {
            if (!TryRollback(world, book, lease, original, updatedHandle, request, buyerOutcome, offlineVisualUpdated))
                return new(LegacyAutoTradePurchaseExecutionResult.RollbackFailed, null);
            return new(LegacyAutoTradePurchaseExecutionResult.CommitRejected, null);
        }

        if (commitResult != LegacyAutoTradePurchaseCommitResult.Committed)
        {
            if (!TryRollback(world, book, lease, original, updatedHandle, request, buyerOutcome, offlineVisualUpdated))
                return new(LegacyAutoTradePurchaseExecutionResult.RollbackFailed, null);
            return new(LegacyAutoTradePurchaseExecutionResult.CommitRejected, null);
        }

        var outcome = new LegacyAutoTradePurchaseExecutionOutcome(
            plan,
            buyerOutcome,
            request.Context.Listing,
            updatedListing,
            updatedPersistedListing,
            offlineVisualUpdated);
        return new(LegacyAutoTradePurchaseExecutionResult.Accepted, outcome);
    }

    private static bool TryRollback(
        WorldHub world,
        LegacyAutoTradeBook book,
        LegacyAutoTradeOperationLease lease,
        LegacyAutoTradeListingHandle original,
        LegacyAutoTradeListingHandle updated,
        LegacyAutoTradePurchaseExecutionRequest request,
        LegacyAutoTradeBuyerPurchaseOutcome buyerOutcome,
        bool offlineVisualUpdated)
    {
        var buyerRolledBack = world.TryRollbackAutoTradeBuyerPurchase(buyerOutcome);
        var visualRolledBack = !offlineVisualUpdated || world.TryUpdateAutoTradeNpcSnapshot(
            request.Context.TargetId,
            updated.Snapshot,
            request.Context.Listing);
        var bookRolledBack = book.TryRollbackPurchase(
            lease, updated, original);
        return buyerRolledBack && visualRolledBack && bookRolledBack;
    }

    private static bool SameListing(LegacyAutoTradeSnapshot left, LegacyAutoTradeSnapshot right) =>
        left.ConnectionId == right.ConnectionId && left.PositionX == right.PositionX && left.PositionY == right.PositionY &&
        left.Title == right.Title && left.Tax == right.Tax && left.Items.SequenceEqual(right.Items) &&
        left.CarryPositions.SequenceEqual(right.CarryPositions) && left.Prices.SequenceEqual(right.Prices);

    private static LegacyAutoTradePurchaseExecutionRequest Freeze(LegacyAutoTradePurchaseExecutionRequest request) => request with
    {
        ExpectedBuyerMob = request.ExpectedBuyerMob.ToArray(),
        ExpectedPersistedBuyerMob = request.ExpectedPersistedBuyerMob.ToArray(),
        BuyerMobExtra = request.BuyerMobExtra.ToArray(),
        Context = request.Context with
        {
            Listing = request.Context.Listing with
            {
                Items = request.Context.Listing.Items.ToArray(),
                CarryPositions = request.Context.Listing.CarryPositions.ToArray(),
                Prices = request.Context.Listing.Prices.ToArray(),
            },
            SellerCargo = request.Context.SellerCargo.ToArray(),
            BuyerCarry = request.Context.BuyerCarry.ToArray(),
        },
        ExpectedListing = request.ExpectedListing with
        {
            Items = request.ExpectedListing.Items.ToArray(),
            CarryPositions = request.ExpectedListing.CarryPositions.ToArray(),
            Prices = request.ExpectedListing.Prices.ToArray(),
        },
    };

    private static LegacyAutoTradePersistedListing ToPersistedListing(
        string accountName,
        int characterSlot,
        LegacyAutoTradeSnapshot snapshot) => new(
            accountName.Trim().ToUpperInvariant(),
            characterSlot,
            snapshot.PositionX,
            snapshot.PositionY,
            snapshot.Title,
            snapshot.Items.ToArray(),
            snapshot.CarryPositions.ToArray(),
            snapshot.Prices.ToArray(),
            snapshot.Tax);
}

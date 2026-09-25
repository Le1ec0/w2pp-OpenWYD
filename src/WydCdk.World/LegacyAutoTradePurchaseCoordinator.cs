using System.Buffers.Binary;
using WydCdk.Protocol;

namespace WydCdk.World;

public enum LegacyAutoTradePurchaseHostResult
{
    Accepted,
    SessionNotPlaying,
    AtomicPersistenceUnavailable,
    BuyerStateUnavailable,
    BuyerPersistenceBaselineUnavailable,
    BuyerCombatStateUnavailable,
    TargetNotInAutoTrade,
    TargetNotPlaying,
    SellerIdentityUnavailable,
    SellerStateUnavailable,
    ExecutionRejected,
}

public sealed record LegacyAutoTradePurchaseHostOutcome(
    LegacyAutoTradePurchaseHostResult Result,
    LegacyAutoTradePurchaseExecution? Execution,
    string? SellerAccountName,
    bool TargetOnline,
    bool TargetOffline);

/// <summary>
/// Reads the host-owned session/world/storage state and builds one purchase
/// execution request. Frame emission remains in the server host so this
/// coordinator can be tested without a socket.
/// </summary>
public static class LegacyAutoTradePurchaseCoordinator
{
    public static async ValueTask<LegacyAutoTradePurchaseHostOutcome> ExecuteAsync(
        int buyerConnectionId,
        AutoTradePurchaseRequest purchaseRequest,
        LoginSessionRegistry sessions,
        WorldHub world,
        LegacyAutoTradeBook autoTradeBook,
        IAccountSnapshotStore? accountSnapshots,
        ILegacyCharacterLoginDataStore? characterLoginData,
        ILegacyAutoTradePurchaseCommitStore? commitStore,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(purchaseRequest);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(autoTradeBook);

        if (!sessions.TryGet(buyerConnectionId, out var purchaseSession) ||
            purchaseSession is null || purchaseSession.State != LoginSessionState.Playing)
            return new(LegacyAutoTradePurchaseHostResult.SessionNotPlaying, null, null, false, false);

        if (accountSnapshots is null || characterLoginData is null || commitStore is null)
            return new(LegacyAutoTradePurchaseHostResult.AtomicPersistenceUnavailable, null, null, false, false);

        if (purchaseSession.AccountName is null || purchaseSession.CharacterSlot < 0 ||
            !world.TryGetCharacterSnapshot(
                buyerConnectionId,
                out var buyerMob,
                out var buyerX,
                out var buyerY,
                out var buyerMobExtra,
                out _) ||
            buyerMob is null || buyerMobExtra is null ||
            !TryReadLegacyCarry(buyerMob, out var buyerCarry))
            return new(LegacyAutoTradePurchaseHostResult.BuyerStateUnavailable, null, null, false, false);

        var persistedBuyerData = await characterLoginData.ReadCharacterLoginDataAsync(
            purchaseSession.AccountName,
            purchaseSession.CharacterSlot,
            cancellationToken);
        if (persistedBuyerData is null || persistedBuyerData.Mob.Length != LegacyAccountSnapshot.CharacterStride)
            return new(LegacyAutoTradePurchaseHostResult.BuyerPersistenceBaselineUnavailable, null, null, false, false);

        if (!world.TryGetCombatState(buyerConnectionId, out var buyerCombatState) || buyerCombatState is null)
            return new(LegacyAutoTradePurchaseHostResult.BuyerCombatStateUnavailable, null, null, false, false);

        if (!autoTradeBook.TryGet(purchaseRequest.TargetId, out var purchaseListing) || purchaseListing is null)
            return new(LegacyAutoTradePurchaseHostResult.TargetNotInAutoTrade, null, null, false, false);

        var targetOnline = world.TryGetCharacterSnapshot(purchaseRequest.TargetId, out _, out var targetX, out var targetY);
        var targetOffline = world.TryGetNpcSnapshot(purchaseRequest.TargetId, out var purchaseNpc) &&
            purchaseNpc is not null && purchaseNpc.AutoTradeSnapshot is not null;
        LoginSession? sellerSession = null;
        var targetInPlay = targetOnline && sessions.TryGet(purchaseRequest.TargetId, out sellerSession) &&
            sellerSession is not null && sellerSession.State == LoginSessionState.Playing;
        if (!targetInPlay && !targetOffline)
            return new(LegacyAutoTradePurchaseHostResult.TargetNotPlaying, null, null, targetOnline, targetOffline);

        string? sellerAccountName;
        int sellerCharacterSlot;
        if (targetInPlay && sellerSession!.AccountName is not null && sellerSession.CharacterSlot >= 0)
        {
            sellerAccountName = sellerSession.AccountName;
            sellerCharacterSlot = sellerSession.CharacterSlot;
        }
        else if (targetOffline && purchaseNpc!.AutoTradeOwnerAccount is not null && purchaseNpc.AutoTradeCharacterSlot >= 0)
        {
            sellerAccountName = purchaseNpc.AutoTradeOwnerAccount;
            sellerCharacterSlot = purchaseNpc.AutoTradeCharacterSlot;
            targetX = purchaseNpc.PositionX;
            targetY = purchaseNpc.PositionY;
        }
        else
            return new(LegacyAutoTradePurchaseHostResult.SellerIdentityUnavailable, null, null, targetOnline, targetOffline);

        var sellerSnapshot = await accountSnapshots.ReadSnapshotAsync(sellerAccountName, cancellationToken);
        var sellerCargo = sellerSnapshot?.Cargo.Take(LegacyAutoTradeBook.CargoSlotCount).ToArray();
        if (sellerSnapshot is null || sellerCargo is null || sellerCargo.Length != LegacyAutoTradeBook.CargoSlotCount)
            return new(LegacyAutoTradePurchaseHostResult.SellerStateUnavailable, null, sellerAccountName, targetOnline, targetOffline);

        var targetVillage = targetOnline
            ? world.TryGetAutoTradeLocation(purchaseRequest.TargetId, out var resolvedVillage, out _)
                ? resolvedVillage
                : -1
            : LegacyAutoTradeLocationRules.TryResolve(targetX, targetY, null, out var offlineVillage, out _)
                ? offlineVillage
                : -1;
        var purchaseContext = new LegacyAutoTradePurchaseContext(
            buyerCombatState.CurrentScore.Hp,
            RequesterInPlay: true,
            world.IsLiveTradeActive(buyerConnectionId),
            LiveTradeOpponentId: 0,
            purchaseRequest.TargetId,
            TargetExists: true,
            targetInPlay,
            targetOffline,
            TargetAutoTradeActive: true,
            LegacyMovementRules.IsTargetWithinViewGrid(buyerX, buyerY, targetX, targetY),
            purchaseRequest.Position,
            purchaseRequest.Price,
            purchaseRequest.Tax,
            purchaseRequest.Item,
            purchaseListing,
            sellerCargo,
            buyerCarry,
            BinaryPrimitives.ReadInt32LittleEndian(buyerMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset)),
            sellerSnapshot.Coin,
            targetVillage);
        var expectedListing = new LegacyAutoTradePersistedListing(
            sellerAccountName.Trim().ToUpperInvariant(),
            sellerCharacterSlot,
            purchaseListing.PositionX,
            purchaseListing.PositionY,
            purchaseListing.Title,
            purchaseListing.Items.ToArray(),
            purchaseListing.CarryPositions.ToArray(),
            purchaseListing.Prices.ToArray(),
            purchaseListing.Tax);
        var executionRequest = new LegacyAutoTradePurchaseExecutionRequest(
            buyerConnectionId,
            purchaseContext,
            purchaseSession.AccountName,
            purchaseSession.CharacterSlot,
            buyerMob,
            persistedBuyerData.Mob,
            buyerX,
            buyerY,
            buyerMobExtra,
            sellerAccountName,
            sellerCharacterSlot,
            expectedListing,
            targetOffline);
        var execution = await LegacyAutoTradePurchaseExecutor.ExecuteAsync(
            world,
            autoTradeBook,
            commitStore,
            executionRequest,
            cancellationToken);
        return execution.Result == LegacyAutoTradePurchaseExecutionResult.Accepted
            ? new(LegacyAutoTradePurchaseHostResult.Accepted, execution, sellerAccountName, targetOnline, targetOffline)
            : new(LegacyAutoTradePurchaseHostResult.ExecutionRejected, execution, sellerAccountName, targetOnline, targetOffline);
    }

    private static bool TryReadLegacyCarry(ReadOnlySpan<byte> mob, out IReadOnlyList<LegacyItem> carry)
    {
        carry = Array.Empty<LegacyItem>();
        if (mob.Length < LegacyAccountSnapshot.MobCarryOffset + (LegacyAccountSnapshot.MobCarryCount * LegacyItem.SizeInBytes))
            return false;

        var result = new LegacyItem[LegacyAccountSnapshot.MobCarryCount];
        for (var index = 0; index < result.Length; index++)
            result[index] = LegacyItem.Read(mob.Slice(
                LegacyAccountSnapshot.MobCarryOffset + (index * LegacyItem.SizeInBytes),
                LegacyItem.SizeInBytes));
        carry = result;
        return true;
    }
}

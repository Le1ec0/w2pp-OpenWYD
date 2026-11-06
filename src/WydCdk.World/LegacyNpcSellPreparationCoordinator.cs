using WydCdk.Protocol;

namespace WydCdk.World;

public enum LegacyNpcSellPreparationStatus
{
    Evaluated,
    SellerStateUnavailable,
    AuthoritativeLookupUnavailable,
    CargoStateReconciled,
    CargoOwnerUnavailable,
    TradeModeChangedDuringPreparation,
    ItemDataUnavailable,
    TargetLocationChangedDuringPreparation,
    VillageFiveCityTaxUnverified,
}

public sealed record LegacyNpcSellPreparationOutcome(
    LegacyNpcSellPreparationStatus Status,
    LegacyNpcSellFlowResult? Flow,
    LegacyNpcSellAuthoritativeState? State,
    LegacyNpcSellLookupStatus? LookupStatus,
    LegacyNpcSellLocation? Location);

/// <summary>
/// Gathers the live/session/item-table inputs for the ordered sell evaluator.
/// It is a read-only preparation boundary; the caller must revalidate the
/// snapshots and atomically apply item, Gold, and recycle-bin changes later.
/// </summary>
public static class LegacyNpcSellPreparationCoordinator
{
    public static async ValueTask<LegacyNpcSellPreparationOutcome> PrepareAsync(
        LoginSession session,
        ShopSellMessage request,
        WorldHub world,
        LegacyAutoTradeBook autoTradeBook,
        IAccountSnapshotStore? accountSnapshots,
        LegacyItemDataTable? itemData,
        CancellationToken cancellationToken = default,
        LegacyAccountCargoRuntime? cargoRuntime = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(autoTradeBook);

        if (session.State != LoginSessionState.Playing)
        {
            var notPlaying = LegacyNpcSellFlowRules.Resolve(CreateContext(
                session, request, currentHp: 0, tradeOpponentId: 0, tradeMode: false,
                accountBlocked: false, selectedItemAvailable: false, default, null,
                sellerMob: null, sellerX: 0, sellerY: 0, itemBasePrice: 0,
                volatileValue: 0, npcVillage: 0, cityTax: 0));
            return Evaluated(notPlaying, null, null, null);
        }

        if (!world.TryGetCharacterSnapshot(session.ConnectionId, out var initialSellerMob, out var initialX, out var initialY) ||
            initialSellerMob is null || initialSellerMob.Length < LegacyAccountSnapshot.CharacterStride)
            return new(LegacyNpcSellPreparationStatus.SellerStateUnavailable, null, null, null, null);

        var initialCombat = LegacyMobCombatState.Read(initialSellerMob);
        var tradeMode = autoTradeBook.HasListing(session.ConnectionId);
        var early = LegacyNpcSellFlowRules.Resolve(CreateContext(
            session, request, initialCombat.CurrentScore.Hp, world.GetTradeOpponentId(session.ConnectionId), tradeMode,
            accountBlocked: false, selectedItemAvailable: false, default, null,
            initialSellerMob, initialX, initialY, itemBasePrice: 0,
            volatileValue: 0, npcVillage: 0, cityTax: 0));
        if (early.Stage is LegacyNpcSellFlowStage.HpModeRejected or
            LegacyNpcSellFlowStage.TradeCancellationRequired or
            LegacyNpcSellFlowStage.CargoBlockedDuringTradeMode)
            return Evaluated(early, null, null, null);

        var lookup = await LegacyNpcSellAuthoritativeLookup.CaptureAsync(
            session,
            request,
            world,
            accountSnapshots,
            tradeMode,
            cancellationToken,
            cargoRuntime);
        if (lookup.State is not { } state)
            return new(LegacyNpcSellPreparationStatus.AuthoritativeLookupUnavailable, null, null, lookup.Status, null);

        if (lookup.Status == LegacyNpcSellLookupStatus.CargoReconciled)
            return new(LegacyNpcSellPreparationStatus.CargoStateReconciled, null, state, lookup.Status, null);
        if (lookup.Status == LegacyNpcSellLookupStatus.CargoOwnerUnavailable)
            return new(LegacyNpcSellPreparationStatus.CargoOwnerUnavailable, null, state, lookup.Status, null);

        var currentTradeMode = autoTradeBook.HasListing(session.ConnectionId);
        if (currentTradeMode != tradeMode)
            return new(LegacyNpcSellPreparationStatus.TradeModeChangedDuringPreparation, null, state, lookup.Status, null);

        var sellerCombat = LegacyMobCombatState.Read(state.SellerMobSnapshot);
        var selectedItem = state.SelectedItem.GetValueOrDefault();
        var baseContext = CreateContext(
            session,
            request,
            sellerCombat.CurrentScore.Hp,
            world.GetTradeOpponentId(session.ConnectionId),
            currentTradeMode,
            state.AccountBlocked,
            state.SelectedItem.HasValue,
            selectedItem,
            state.Target,
            state.SellerMobSnapshot,
            state.SellerPositionX,
            state.SellerPositionY,
            itemBasePrice: 0,
            volatileValue: 0,
            npcVillage: 0,
            cityTax: 0);

        var preliminary = LegacyNpcSellFlowRules.Resolve(baseContext);
        if (preliminary.Stage != LegacyNpcSellFlowStage.ReadyForSaleMutation)
            return Evaluated(preliminary, state, null, lookup.Status);

        if (itemData is null || itemData[selectedItem.Index] is not { } itemDefinition)
            return new(LegacyNpcSellPreparationStatus.ItemDataUnavailable, preliminary, state, lookup.Status, null);

        if (state.Target is not { } target || !world.TryResolveNpcSellLocation(target, out var location) || location is null)
            return new(LegacyNpcSellPreparationStatus.TargetLocationChangedDuringPreparation, preliminary, state, lookup.Status, null);

        if (location.CityTax is not { } cityTax)
            return new(LegacyNpcSellPreparationStatus.VillageFiveCityTaxUnverified, null, state, lookup.Status, location);

        var flow = LegacyNpcSellFlowRules.Resolve(baseContext with
        {
            ItemBasePrice = itemDefinition.Price,
            Special2 = sellerCombat.CurrentScore.Special3,
            NpcVillage = location.Village,
            CityTax = cityTax,
            VolatileValue = itemData.GetItemAbility(selectedItem, LegacyItemEffect.Volatile),
        });
        return Evaluated(flow, state, location, lookup.Status);
    }

    private static LegacyNpcSellFlowContext CreateContext(
        LoginSession session,
        ShopSellMessage request,
        int currentHp,
        int tradeOpponentId,
        bool tradeMode,
        bool accountBlocked,
        bool selectedItemAvailable,
        LegacyItem selectedItem,
        LegacyWorldMobSnapshot? target,
        byte[]? sellerMob,
        short sellerX,
        short sellerY,
        int itemBasePrice,
        int volatileValue,
        int npcVillage,
        int cityTax)
    {
        var sellerCombat = sellerMob is { Length: >= LegacyAccountSnapshot.CharacterStride }
            ? LegacyMobCombatState.Read(sellerMob)
            : null;
        return new(
            session.State,
            currentHp,
            session.ConnectionId,
            tradeOpponentId,
            tradeMode,
            accountBlocked,
            request.MyType,
            request.MyPos,
            selectedItemAvailable,
            selectedItem,
            request.TargetId,
            target,
            sellerCombat?.CharacterClass ?? 0,
            unchecked((int)(sellerCombat?.LearnedSkill ?? 0)),
            sellerX,
            sellerY,
            itemBasePrice,
            sellerCombat?.CurrentScore.Special3 ?? 0,
            npcVillage,
            cityTax,
            volatileValue);
    }

    private static LegacyNpcSellPreparationOutcome Evaluated(
        LegacyNpcSellFlowResult flow,
        LegacyNpcSellAuthoritativeState? state,
        LegacyNpcSellLocation? location,
        LegacyNpcSellLookupStatus? lookupStatus) =>
        new(LegacyNpcSellPreparationStatus.Evaluated, flow, state, lookupStatus, location);
}

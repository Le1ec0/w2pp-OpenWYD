using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Pure gates for the legacy <c>_MSG_ReqBuy</c> autotrade path. It deliberately
/// does not debit coin, move an item, remove a listing or write persistence;
/// those operations must be committed atomically by the host/world adapter.
/// </summary>
public static class LegacyAutoTradePurchaseRules
{
    public const int SellerCoinLimit = 2_000_000_000;
    public const int BuyerCarryLimit = LegacyAccountSnapshot.MobCarryCount - 4;

    public static LegacyAutoTradePurchaseResult Validate(LegacyAutoTradePurchaseContext context)
    {
        if (context.RequesterHp <= 0)
            return LegacyAutoTradePurchaseResult.RequesterNotAlive;
        if (!context.RequesterInPlay)
            return LegacyAutoTradePurchaseResult.RequesterNotPlaying;
        if (context.LiveTradeActive)
            return LegacyAutoTradePurchaseResult.LiveTradeActive;
        if (context.LiveTradeOpponentId != 0)
            return LegacyAutoTradePurchaseResult.LiveTradeOpponentActive;
        if (context.TargetId <= 0 || !context.TargetExists)
            return LegacyAutoTradePurchaseResult.TargetNotFound;
        if (!context.TargetAutoTradeActive)
            return LegacyAutoTradePurchaseResult.TargetNotInAutoTrade;
        if (!context.TargetInPlay && !context.TargetOfflineAutoTrade)
            return LegacyAutoTradePurchaseResult.TargetNotPlaying;
        if (!context.TargetWithinView)
            return LegacyAutoTradePurchaseResult.OutOfRange;
        if (context.Listing.ConnectionId != context.TargetId ||
            context.Listing.Items.Count != AutoTradeListConfirmation.SlotCount ||
            context.Listing.CarryPositions.Count != AutoTradeListConfirmation.SlotCount ||
            context.Listing.Prices.Count != AutoTradeListConfirmation.SlotCount)
            return LegacyAutoTradePurchaseResult.InvalidListing;
        if (context.Position < 0 || context.Position >= AutoTradeListConfirmation.SlotCount)
            return LegacyAutoTradePurchaseResult.InvalidPosition;

        var listedItem = context.Listing.Items[context.Position];
        var listedPrice = context.Listing.Prices[context.Position];
        var listedCargoPosition = context.Listing.CarryPositions[context.Position];
        if (listedItem.Index == 0 || listedPrice <= 0 || listedPrice > LegacyAutoTradeBook.PriceLimit ||
            listedCargoPosition < 0 || listedCargoPosition >= LegacyAutoTradeBook.CargoSlotCount)
            return LegacyAutoTradePurchaseResult.InvalidListing;
        if (context.RequestedTax != context.Listing.Tax)
            return LegacyAutoTradePurchaseResult.TaxChanged;
        if (context.RequestedPrice != listedPrice)
            return LegacyAutoTradePurchaseResult.PriceChanged;
        if (context.RequestedItem != listedItem)
            return LegacyAutoTradePurchaseResult.ItemChanged;
        if (context.SellerCargo.Count <= listedCargoPosition || context.SellerCargo[listedCargoPosition] != listedItem)
            return LegacyAutoTradePurchaseResult.SellerItemChanged;
        if (context.BuyerCoin < listedPrice)
            return LegacyAutoTradePurchaseResult.InsufficientCoin;
        if ((long)context.SellerCoin + listedPrice > SellerCoinLimit)
            return LegacyAutoTradePurchaseResult.SellerCoinLimitReached;
        if (context.BuyerCarry.Count < BuyerCarryLimit)
            return LegacyAutoTradePurchaseResult.BuyerCarryUnavailable;
        if (!context.BuyerCarry.Take(BuyerCarryLimit).Any(static item => item.Index == 0))
            return LegacyAutoTradePurchaseResult.NoCarrySpace;
        if (context.TargetVillage < 0 || context.TargetVillage >= 5)
            return LegacyAutoTradePurchaseResult.InvalidVillage;

        return LegacyAutoTradePurchaseResult.Accepted;
    }
}

/// <summary>State snapshot consumed by the pure autotrade purchase gates.</summary>
public readonly record struct LegacyAutoTradePurchaseContext(
    int RequesterHp,
    bool RequesterInPlay,
    bool LiveTradeActive,
    int LiveTradeOpponentId,
    int TargetId,
    bool TargetExists,
    bool TargetInPlay,
    bool TargetOfflineAutoTrade,
    bool TargetAutoTradeActive,
    bool TargetWithinView,
    int Position,
    int RequestedPrice,
    int RequestedTax,
    LegacyItem RequestedItem,
    LegacyAutoTradeSnapshot Listing,
    IReadOnlyList<LegacyItem> SellerCargo,
    IReadOnlyList<LegacyItem> BuyerCarry,
    int BuyerCoin,
    int SellerCoin,
    int TargetVillage);

public enum LegacyAutoTradePurchaseResult
{
    Accepted,
    RequesterNotAlive,
    RequesterNotPlaying,
    LiveTradeActive,
    LiveTradeOpponentActive,
    TargetNotFound,
    TargetNotInAutoTrade,
    TargetNotPlaying,
    OutOfRange,
    InvalidListing,
    InvalidPosition,
    TaxChanged,
    PriceChanged,
    ItemChanged,
    SellerItemChanged,
    InsufficientCoin,
    SellerCoinLimitReached,
    BuyerCarryUnavailable,
    NoCarrySpace,
    InvalidVillage,
    SettlementOutOfRange,
}

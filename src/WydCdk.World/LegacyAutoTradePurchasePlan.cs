using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Builds the complete in-memory result of one validated autotrade purchase.
/// The inputs are copied and never mutated; a host/storage adapter can commit
/// this plan only after its atomic persistence capability has accepted it.
/// </summary>
public static class LegacyAutoTradePurchasePlanBuilder
{
    public static LegacyAutoTradePurchaseResult TryBuild(
        LegacyAutoTradePurchaseContext context,
        out LegacyAutoTradePurchasePlan? plan)
    {
        plan = null;
        var validation = LegacyAutoTradePurchaseRules.Validate(context);
        if (validation != LegacyAutoTradePurchaseResult.Accepted)
            return validation;

        var listingItem = context.Listing.Items[context.Position];
        var sellerCargoPosition = context.Listing.CarryPositions[context.Position];
        var destinationSlot = context.BuyerCarry
            .Take(LegacyAutoTradePurchaseRules.BuyerCarryLimit)
            .Select(static (item, index) => (item, index))
            .First(static value => value.item.Index == 0)
            .index;
        var settlement = LegacyAutoTradeSettlementMath.Calculate(context.RequestedPrice, context.RequestedTax);
        var sellerCoin = (long)context.SellerCoin + settlement.SellerProceeds;
        if (sellerCoin is < 0 or > LegacyAutoTradePurchaseRules.SellerCoinLimit || settlement.SellerProceeds is < int.MinValue or > int.MaxValue)
            return LegacyAutoTradePurchaseResult.SettlementOutOfRange;

        var buyerCarry = context.BuyerCarry.ToArray();
        buyerCarry[destinationSlot] = listingItem;
        var sellerCargo = context.SellerCargo.ToArray();
        sellerCargo[sellerCargoPosition] = default;

        var listingItems = context.Listing.Items.ToArray();
        var listingCarryPositions = context.Listing.CarryPositions.ToArray();
        var listingPrices = context.Listing.Prices.ToArray();
        listingItems[context.Position] = default;
        listingCarryPositions[context.Position] = -1;
        listingPrices[context.Position] = 0;

        plan = new LegacyAutoTradePurchasePlan(
            context.TargetId,
            context.Position,
            sellerCargoPosition,
            destinationSlot,
            listingItem,
            settlement,
            checked(context.BuyerCoin - context.RequestedPrice),
            checked((int)sellerCoin),
            buyerCarry,
            sellerCargo,
            context.Listing with
            {
                Items = listingItems,
                CarryPositions = listingCarryPositions,
                Prices = listingPrices,
            });
        return LegacyAutoTradePurchaseResult.Accepted;
    }
}

public sealed record LegacyAutoTradePurchasePlan(
    int TargetId,
    int Position,
    int SellerCargoPosition,
    int BuyerDestinationSlot,
    LegacyItem PurchasedItem,
    LegacyAutoTradeSettlement Settlement,
    int BuyerCoin,
    int SellerCoin,
    IReadOnlyList<LegacyItem> BuyerCarry,
    IReadOnlyList<LegacyItem> SellerCargo,
    LegacyAutoTradeSnapshot UpdatedListing);

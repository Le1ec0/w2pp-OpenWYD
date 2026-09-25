namespace WydCdk.World;

/// <summary>
/// Pure settlement arithmetic from W2PP <c>_MSG_ReqBuy</c>. The city tax is
/// applied only when the item price is at least 100,000 Gold; the buyer pays
/// the full price and the seller receives the post-tax amount.
/// </summary>
public static class LegacyAutoTradeSettlementMath
{
    public const int TaxThreshold = 100_000;

    public static LegacyAutoTradeSettlement Calculate(int itemPrice, int itemTax)
    {
        var taxAmount = itemPrice >= TaxThreshold
            ? (itemPrice / 100L) * itemTax
            : 0L;
        return new(itemPrice, itemTax, taxAmount, itemPrice - taxAmount);
    }
}

public readonly record struct LegacyAutoTradeSettlement(
    int ItemPrice,
    int ItemTax,
    long TaxAmount,
    long SellerProceeds);

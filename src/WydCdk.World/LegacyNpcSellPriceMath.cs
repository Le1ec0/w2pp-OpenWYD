namespace WydCdk.World;

/// <summary>
/// Pure NPC resale-price arithmetic from TMProject 7.69 and W2PP
/// <c>TMSrv/_MSG_Sell.cpp</c>. Merchant/item validation, item consumption,
/// city-guild revenue, and the player's Gold cap remain outside this helper.
/// </summary>
public static class LegacyNpcSellPriceMath
{
    public const uint SellerBonusLearnedMask = 0x200;

    public static LegacyNpcSellPriceResult Calculate(
        int itemBasePrice,
        int itemIndex,
        int sellerClass,
        uint learnedSkill,
        int special2,
        int npcVillage,
        int cityTax)
    {
        var price = itemBasePrice / 4;
        if (price <= 10_000)
        {
            if (price > 5_000)
                price = 2 * price / 3;
        }
        else
            price /= 2;

        if (price > 1_000 && itemIndex is not (693 or 694 or 695) &&
            sellerClass == 3 && (learnedSkill & SellerBonusLearnedMask) != 0)
        {
            var special = special2 / 10;
            var discount = special + 6;
            if (special + 6 >= 26)
                discount = 26;

            if (discount is >= 5 and <= 26)
            {
                discount /= 2;
                var multiplier = discount + 50;
                if (!TryMultiplyInt32(multiplier, price, out var product))
                    return new(LegacyNpcSellPriceStatus.Int32OverflowUnverified, 0);
                price = product / 50;
            }
        }

        if (price < 0)
            return new(LegacyNpcSellPriceStatus.NegativePrice, 0);

        var grossAmount = price;
        if (npcVillage is >= 0 and <= 5)
        {
            if (cityTax is < 0 or > 30)
                return new(LegacyNpcSellPriceStatus.InvalidCityTax, 0);

            var taxMultiplier = 100 - cityTax;
            if (!TryMultiplyInt32(price, taxMultiplier, out var taxedProduct))
                return new(LegacyNpcSellPriceStatus.Int32OverflowUnverified, 0);
            price = taxedProduct / 100;

            if (price < 0)
                return new(LegacyNpcSellPriceStatus.NegativePrice, 0);
        }

        return new(LegacyNpcSellPriceStatus.Success, price, npcVillage, grossAmount);
    }

    private static bool TryMultiplyInt32(int left, int right, out int product)
    {
        var wideProduct = (long)left * right;
        if (wideProduct is < int.MinValue or > int.MaxValue)
        {
            product = 0;
            return false;
        }

        product = (int)wideProduct;
        return true;
    }
}

public enum LegacyNpcSellPriceStatus
{
    Success,
    NegativePrice,
    InvalidCityTax,
    Int32OverflowUnverified,
}

public readonly record struct LegacyNpcSellPriceResult(
    LegacyNpcSellPriceStatus Status,
    int Amount,
    int NpcVillage = -1,
    int GrossAmount = 0)
{
    public bool IsSuccess => Status == LegacyNpcSellPriceStatus.Success;
}

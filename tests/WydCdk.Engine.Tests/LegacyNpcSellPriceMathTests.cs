using WydCdk.World;

internal static class LegacyNpcSellPriceMathTests
{
    public static void Run()
    {
        QuarterPriceUsesTheLegacyIntegerBands();
        SellerBonusUsesClassLearnedBitSpecialAndItemExclusions();
        CityTaxUsesTheLegacyVillageAndTaxBounds();
        PriceResultRetainsPreTaxAmount();
        NegativeAndOverflowCasesDoNotInventSaleAmounts();
    }

    private static void QuarterPriceUsesTheLegacyIntegerBands()
    {
        Check(Price(20_000).Amount == 5_000, "The 5,000 post-quarter sale price was changed.");
        Check(Price(20_004).Amount == 3_334, "The 5,001 post-quarter sale price did not use integer 2/3 truncation.");
        Check(Price(40_000).Amount == 6_666, "The 10,000 post-quarter sale price did not use integer 2/3 truncation.");
        Check(Price(40_004).Amount == 5_000, "The 10,001 post-quarter sale price did not halve.");
    }

    private static void SellerBonusUsesClassLearnedBitSpecialAndItemExclusions()
    {
        Check(Price(8_000, sellerClass: 3, learnedSkill: 0x200).Amount == 2_120,
            "The class-3 seller bonus did not use the default six-point Special-derived rate and integer half.");
        Check(Price(8_000, sellerClass: 3, learnedSkill: 0x200, special2: 1_000).Amount == 2_520,
            "The seller bonus did not cap its rate at 26 before halving.");
        Check(Price(8_000, itemIndex: 693, sellerClass: 3, learnedSkill: 0x200, special2: 1_000).Amount == 2_000 &&
              Price(8_000, itemIndex: 694, sellerClass: 3, learnedSkill: 0x200, special2: 1_000).Amount == 2_000 &&
              Price(8_000, itemIndex: 695, sellerClass: 3, learnedSkill: 0x200, special2: 1_000).Amount == 2_000,
            "The seller bonus changed excluded items 693-695.");
        Check(Price(8_000, sellerClass: 2, learnedSkill: 0x200, special2: 1_000).Amount == 2_000 &&
              Price(8_000, sellerClass: 3, special2: 1_000).Amount == 2_000,
            "The seller bonus bypassed its class-3 or learned-bit gate.");
        Check(Price(4_000, sellerClass: 3, learnedSkill: 0x200, special2: 1_000).Amount == 1_000,
            "The seller bonus applied at the exclusive 1,000 price boundary.");
    }

    private static void CityTaxUsesTheLegacyVillageAndTaxBounds()
    {
        Check(Price(799_992, village: 0, cityTax: 7).Amount == 92_999,
            "City tax below the 100,000 sale-price branch changed integer truncation.");
        Check(Price(800_000, village: 0, cityTax: 7).Amount == 93_000,
            "City tax at the 100,000 sale-price branch changed the result.");
        Check(Price(80_000, village: 0, cityTax: 30).Amount == 7_000,
            "The inclusive 30 percent city-tax bound changed.");
        Check(Price(80_000, village: -1, cityTax: 31).Amount == 10_000 &&
              Price(80_000, village: 6, cityTax: -1).Amount == 10_000,
            "An invalid village did not bypass city-tax lookup as the legacy handler does.");
        Check(Price(80_000, village: 0, cityTax: -1).Status == LegacyNpcSellPriceStatus.InvalidCityTax &&
              Price(80_000, village: 5, cityTax: 31).Status == LegacyNpcSellPriceStatus.InvalidCityTax,
            "A valid village accepted a city tax outside 0..30.");
    }

    private static void NegativeAndOverflowCasesDoNotInventSaleAmounts()
    {
        Check(Price(-40_000).Status == LegacyNpcSellPriceStatus.NegativePrice,
            "A negative post-quarter sell price was accepted.");
        Check(Price(300_000_000, itemIndex: 3443, village: 0, cityTax: 0).Status == LegacyNpcSellPriceStatus.Int32OverflowUnverified,
            "The known 300-million Gold item did not expose the overflowing city-tax intermediate.");
        Check(Price(300_000_000, itemIndex: 3443, sellerClass: 3, learnedSkill: 0x200, special2: 1_000)
                .Status == LegacyNpcSellPriceStatus.Int32OverflowUnverified,
            "The high-price seller bonus overflow was assigned an unproven result.");
    }

    private static void PriceResultRetainsPreTaxAmount()
    {
        var result = Price(80_000, village: 0, cityTax: 10);
        Check(result.Amount == 9_000 && result.GrossAmount == 10_000,
            "The sell-price result did not retain gross price before the city-tax reduction.");
    }

    private static LegacyNpcSellPriceResult Price(
        int itemBasePrice,
        int itemIndex = 1,
        int sellerClass = 0,
        uint learnedSkill = 0,
        int special2 = 0,
        int village = -1,
        int cityTax = 31) =>
        LegacyNpcSellPriceMath.Calculate(itemBasePrice, itemIndex, sellerClass, learnedSkill, special2, village, cityTax);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

using WydCdk.Protocol;
using WydCdk.Server;
using WydCdk.World;

internal static class LegacyNpcSellRecycleBinHandlerTests
{
    public static void Run()
    {
        RecordsThePostTaxPriceOnlyForAValidVillage();
        InvalidVillageContinuesWithoutRecording();
        FailedPriceStopsBeforeRecording();
        MissingOrInvalidSessionStopsBeforeSaleCanContinue();
    }

    private static void RecordsThePostTaxPriceOnlyForAValidVillage()
    {
        var world = new WorldHub();
        Check(world.Enter(63, "SELL-RECYCLE", NoopSend), "The test seller did not enter the world.");

        var item = new LegacyItem(501, 2, 3, 4, 5, 6, 7);
        var price = LegacyNpcSellPriceMath.Calculate(
            itemBasePrice: 40_000,
            itemIndex: item.Index,
            sellerClass: 0,
            learnedSkill: 0,
            special2: 0,
            npcVillage: 2,
            cityTax: 25);

        Check(price.IsSuccess && price.Amount == 4_999,
            "The sell price fixture did not produce the expected post-tax value.");

        var result = LegacyNpcSellRecycleBinHandler.TryRecordSale(
            world, 63, item, price, randomValue: 123_456, out var insertion);

        Check(result == LegacyNpcSellRecycleBinResult.Inserted &&
              insertion is { WasInserted: true, InsertedIndex: 0 },
            "A valid-village sale did not record its recycle-bin entry before continuing.");
        Check(world.TryGetRecycleBinSnapshot(63, out var entries) &&
              entries![0] == new LegacyRecycleBinEntry(4_999, item, 23_456),
            "The recycle bin did not receive the post-tax price, exact item, and legacy tick remainder.");
    }

    private static void InvalidVillageContinuesWithoutRecording()
    {
        var world = new WorldHub();
        Check(world.Enter(64, "SELL-NO-VILLAGE", NoopSend), "The test seller did not enter the world.");

        var item = new LegacyItem(502, 1, 2, 3, 4, 5, 6);
        var price = LegacyNpcSellPriceMath.Calculate(
            itemBasePrice: 40_000,
            itemIndex: item.Index,
            sellerClass: 0,
            learnedSkill: 0,
            special2: 0,
            npcVillage: -1,
            cityTax: 99);

        Check(price.IsSuccess, "An out-of-village sale should bypass city-tax validation in 7.69.");
        var result = LegacyNpcSellRecycleBinHandler.TryRecordSale(
            world, 64, item, price, randomValue: 7, out var insertion);

        Check(result == LegacyNpcSellRecycleBinResult.ContinueWithoutRecycleBin && insertion is null,
            "An out-of-village sale did not follow LABEL_S2 without calling GetRecycleBin.");
        Check(world.TryGetRecycleBinSnapshot(64, out var entries) && entries!.All(static entry => entry.Item.Index == 0),
            "The out-of-village branch changed the session recycle bin.");
    }

    private static void FailedPriceStopsBeforeRecording()
    {
        var world = new WorldHub();
        Check(world.Enter(65, "SELL-INVALID-PRICE", NoopSend), "The test seller did not enter the world.");

        var item = new LegacyItem(503, 0, 0, 0, 0, 0, 0);
        var invalidPrice = LegacyNpcSellPriceMath.Calculate(
            itemBasePrice: 20_000,
            itemIndex: item.Index,
            sellerClass: 0,
            learnedSkill: 0,
            special2: 0,
            npcVillage: 2,
            cityTax: 31);

        Check(LegacyNpcSellRecycleBinHandler.TryRecordSale(
                  world, 65, item, invalidPrice, randomValue: 8, out var insertion) ==
              LegacyNpcSellRecycleBinResult.PriceRejected && insertion is null,
            "An invalid city-tax price reached the recycle-bin insertion gate.");
        Check(world.TryGetRecycleBinSnapshot(65, out var entries) && entries!.All(static entry => entry.Item.Index == 0),
            "A rejected sale price changed the session recycle bin.");
    }

    private static void MissingOrInvalidSessionStopsBeforeSaleCanContinue()
    {
        var world = new WorldHub();
        var item = new LegacyItem(504, 0, 0, 0, 0, 0, 0);
        var price = LegacyNpcSellPriceMath.Calculate(4_000, item.Index, 0, 0, 0, 1, 0);

        Check(LegacyNpcSellRecycleBinHandler.TryRecordSale(
                  world, 66, item, price, randomValue: 9, out _) ==
              LegacyNpcSellRecycleBinResult.WorldSessionUnavailable,
            "A missing world session did not stop the sale at the GetRecycleBin gate.");

        Check(world.Enter(1000, "SELL-INVALID-ID", NoopSend), "The invalid-ID test seller did not enter the world.");
        Check(LegacyNpcSellRecycleBinHandler.TryRecordSale(
                  world, 1000, item, price, randomValue: 10, out var insertion) ==
              LegacyNpcSellRecycleBinResult.InsertionRejected && insertion is { WasInserted: false },
            "A connection rejected by GetRecycleBin did not stop the sale.");
    }

    private static ValueTask NoopSend(ReadOnlyMemory<byte> _, CancellationToken __) => ValueTask.CompletedTask;

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

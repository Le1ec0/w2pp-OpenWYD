using WydCdk.Protocol;
using WydCdk.World;

internal static class LegacyRecycleBinRulesTests
{
    public static void Run()
    {
        InsertsIntoTheFirstEmptyOrOutOfRangeItemSlot();
        FullBinShiftsOutTheOldestEntry();
        InvalidConnectionOrModeDoesNotInsert();
        RejectsWrongCapacityWithoutMutatingInput();
    }

    private static void InsertsIntoTheFirstEmptyOrOutOfRangeItemSlot()
    {
        var entries = FullBin();
        entries[3] = entries[3] with { Item = Item(6500) };
        entries[6] = entries[6] with { Item = Item(0) };
        var saleItem = new LegacyItem(3200, 61, 2, 4, 5, 6, 7);

        var result = LegacyRecycleBinRules.InsertSale(
            connectionId: 25,
            connectionState: LoginSessionState.Playing,
            existingEntries: entries,
            item: saleItem,
            price: 987654,
            randomValue: 123456);

        Check(result.Status == LegacyRecycleBinInsertStatus.Inserted && result.InsertedIndex == 3,
            "The first out-of-range item slot was not selected before a later empty slot.");
        Check(result.Entries.Count == LegacyRecycleBinRules.Capacity &&
              result.Entries[3] == new LegacyRecycleBinEntry(987654, saleItem, 23456),
            "The inserted recycle entry did not preserve sale price/item and rand() modulo 100000.");
        Check(entries[3].Item.Index == 6500 && entries[6].Item.Index == 0,
            "The pure recycle transition mutated its input collection.");
    }

    private static void FullBinShiftsOutTheOldestEntry()
    {
        var entries = FullBin();
        var before = entries.ToArray();
        var saleItem = Item(6200);

        var result = LegacyRecycleBinRules.InsertSale(
            25, LoginSessionState.Playing, entries, saleItem, 17, randomValue: 99999);

        Check(result.Status == LegacyRecycleBinInsertStatus.Inserted && result.InsertedIndex == 9,
            "A full recycle bin did not insert at the final slot after shifting.");
        for (var index = 0; index < LegacyRecycleBinRules.Capacity - 1; index++)
            Check(result.Entries[index] == before[index + 1], $"Full-bin shift changed entry {index}.");
        Check(result.Entries[9] == new LegacyRecycleBinEntry(17, saleItem, 99999),
            "The new sale was not written into slot 9 after the full-bin shift.");
        Check(entries.SequenceEqual(before), "The full-bin transition modified the input array.");
    }

    private static void InvalidConnectionOrModeDoesNotInsert()
    {
        var entries = FullBin();
        var before = entries.ToArray();

        foreach (var connectionId in new[] { int.MinValue, 0, 1000, int.MaxValue })
        {
            var result = LegacyRecycleBinRules.InsertSale(
                connectionId, LoginSessionState.Playing, entries, Item(500), 1, randomValue: 0);
            Check(result.Status == LegacyRecycleBinInsertStatus.InvalidConnection &&
                  result.InsertedIndex is null && result.Entries.SequenceEqual(before),
                $"Invalid connection ID {connectionId} changed or accepted the recycle bin.");
        }

        var notPlaying = LegacyRecycleBinRules.InsertSale(
            25, LoginSessionState.CharacterSelection, entries, Item(500), 1, randomValue: 0);
        Check(notPlaying.Status == LegacyRecycleBinInsertStatus.NotPlaying &&
              notPlaying.InsertedIndex is null && notPlaying.Entries.SequenceEqual(before),
            "A non-USER_PLAY connection changed or accepted the recycle bin.");
    }

    private static void RejectsWrongCapacityWithoutMutatingInput()
    {
        var entries = FullBin().Take(LegacyRecycleBinRules.Capacity - 1).ToArray();
        var before = entries.ToArray();

        var result = LegacyRecycleBinRules.InsertSale(
            25, LoginSessionState.Playing, entries, Item(500), 1, randomValue: 0);

        Check(result.Status == LegacyRecycleBinInsertStatus.InvalidCapacity &&
              result.InsertedIndex is null && result.Entries.SequenceEqual(before) &&
              entries.SequenceEqual(before),
            "The C# rule accepted a recycle bin with a shape different from the fixed legacy array.");
    }

    private static LegacyRecycleBinEntry[] FullBin() => Enumerable.Range(0, LegacyRecycleBinRules.Capacity)
        .Select(index => new LegacyRecycleBinEntry(100 + index, Item((short)(10 + index)), 200 + index))
        .ToArray();

    private static LegacyItem Item(short index) => new(index, 0, 0, 0, 0, 0, 0);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

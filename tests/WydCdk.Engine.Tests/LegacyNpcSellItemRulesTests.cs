using WydCdk.Protocol;
using WydCdk.World;

internal static class LegacyNpcSellItemRulesTests
{
    public static void Run()
    {
        ItemPointerIndexRangeMatchesLegacy();
        EmptyItemResynchronizesOnlyCarry();
        InvalidItemIndicesRemainUnresynced();
        ProtectedItemsAreRejected();
        InvalidSourceCannotResolveAnItem();
    }

    private static void ItemPointerIndexRangeMatchesLegacy()
    {
        Check(Resolve(1).Status == LegacyNpcSellItemStatus.Sellable &&
              Resolve(LegacyItemDataTable.MaxItemIndex - 1).Status == LegacyNpcSellItemStatus.Sellable,
            "The valid item-index boundaries were not accepted.");
        Check(Resolve(-1).Status == LegacyNpcSellItemStatus.ItemPointerUnavailable &&
              Resolve(LegacyItemDataTable.MaxItemIndex).Status == LegacyNpcSellItemStatus.ItemPointerUnavailable,
            "GetItemPointer's negative or upper-bound item-index rejection was not preserved.");
    }

    private static void EmptyItemResynchronizesOnlyCarry()
    {
        var carryResult = Resolve(0, sourceType: 1);
        var equipmentResult = Resolve(0, sourceType: 0);
        var cargoResult = Resolve(0, sourceType: 2);

        Check(carryResult.Status == LegacyNpcSellItemStatus.EmptyItem && carryResult.RequiresCarryItemResync,
            "An empty carry slot did not request the legacy SendItem resync.");
        Check(equipmentResult.Status == LegacyNpcSellItemStatus.EmptyItem && !equipmentResult.RequiresCarryItemResync &&
              cargoResult.Status == LegacyNpcSellItemStatus.EmptyItem && !cargoResult.RequiresCarryItemResync,
            "An empty non-carry slot incorrectly requested a carry resync.");
    }

    private static void InvalidItemIndicesRemainUnresynced()
    {
        var negativeCarryResult = Resolve(-1, sourceType: 1);
        var outOfRangeCarryResult = Resolve(LegacyItemDataTable.MaxItemIndex, sourceType: 1);
        var negativeEquipmentResult = Resolve(-1, sourceType: 0);
        var outOfRangeCargoResult = Resolve(LegacyItemDataTable.MaxItemIndex, sourceType: 2);

        Check(negativeCarryResult.Status == LegacyNpcSellItemStatus.ItemPointerUnavailable &&
              !negativeCarryResult.RequiresCarryItemResync &&
              outOfRangeCarryResult.Status == LegacyNpcSellItemStatus.ItemPointerUnavailable &&
              !outOfRangeCarryResult.RequiresCarryItemResync,
            "A negative or out-of-range carry index was given a SendItem resync even though GetItemPointer returns NULL.");
        Check(negativeEquipmentResult.Status == LegacyNpcSellItemStatus.ItemPointerUnavailable &&
              !negativeEquipmentResult.RequiresCarryItemResync &&
              outOfRangeCargoResult.Status == LegacyNpcSellItemStatus.ItemPointerUnavailable &&
              !outOfRangeCargoResult.RequiresCarryItemResync,
            "An invalid non-carry item index incorrectly requested a carry resync.");
    }

    private static void ProtectedItemsAreRejected()
    {
        foreach (var itemIndex in new short[] { 3193, 3194, 747 })
            Check(Resolve(itemIndex).Status == LegacyNpcSellItemStatus.ProtectedItem,
                $"Legacy protected item {itemIndex} was not rejected.");

        Check(Resolve(746).Status == LegacyNpcSellItemStatus.Sellable &&
              Resolve(748).Status == LegacyNpcSellItemStatus.Sellable &&
              Resolve(3192).Status == LegacyNpcSellItemStatus.Sellable &&
              Resolve(3195).Status == LegacyNpcSellItemStatus.Sellable,
            "The protected-item gate expanded beyond the three source IDs.");
    }

    private static void InvalidSourceCannotResolveAnItem()
    {
        var invalidSource = LegacyNpcSellSourceRules.Resolve(7, 0, tradeMode: false);
        var result = LegacyNpcSellItemRules.Resolve(invalidSource, Item(1));

        Check(result.Status == LegacyNpcSellItemStatus.InvalidSource && !result.IsSellable,
            "An unresolved source was treated as a sellable item.");
    }

    private static LegacyNpcSellItemResolution Resolve(int itemIndex, short sourceType = 1)
    {
        var source = LegacyNpcSellSourceRules.Resolve(sourceType, sourceType == 0 ? (short)1 : (short)0, tradeMode: false);
        return LegacyNpcSellItemRules.Resolve(source, Item(checked((short)itemIndex)));
    }

    private static LegacyItem Item(short index) => new(index, 0, 0, 0, 0, 0, 0);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

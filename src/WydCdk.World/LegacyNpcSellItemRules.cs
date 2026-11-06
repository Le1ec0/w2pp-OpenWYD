using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Resolves the selected item's eligibility gates shared by TMProject 7.69
/// and W2PP <c>_MSG_Sell</c>, after the source slot has been resolved.
/// </summary>
public static class LegacyNpcSellItemRules
{
    public static LegacyNpcSellItemResolution Resolve(LegacyNpcSellSourceResolution source, LegacyItem item)
    {
        if (!source.IsValid || source.Kind is not { } kind)
            return new(LegacyNpcSellItemStatus.InvalidSource, RequiresCarryItemResync: false);

        // GetItemPointer rejects negative and out-of-table indices, while it
        // still returns a pointer for an empty (zero-index) slot.
        if (item.Index < 0 || item.Index >= LegacyItemDataTable.MaxItemIndex)
            return new(LegacyNpcSellItemStatus.ItemPointerUnavailable, RequiresCarryItemResync: false);

        if (item.Index == 0)
            return new(LegacyNpcSellItemStatus.EmptyItem, RequiresCarryItemResync: kind == LegacyNpcSellSourceKind.Carry);

        if (item.Index is 3193 or 3194 or 747)
            return new(LegacyNpcSellItemStatus.ProtectedItem, RequiresCarryItemResync: false);

        return new(LegacyNpcSellItemStatus.Sellable, RequiresCarryItemResync: false);
    }
}

public enum LegacyNpcSellItemStatus
{
    Sellable,
    InvalidSource,
    ItemPointerUnavailable,
    EmptyItem,
    ProtectedItem,
}

public readonly record struct LegacyNpcSellItemResolution(
    LegacyNpcSellItemStatus Status,
    bool RequiresCarryItemResync)
{
    public bool IsSellable => Status == LegacyNpcSellItemStatus.Sellable;
}

using WydCdk.Protocol;
using WydCdk.World;

namespace WydCdk.Server;

/// <summary>
/// Adapts session recycle state to the client-side 7.69
/// <c>MSG_RepurchaseItems</c> layout. The client consumes only each
/// <c>STRUCT_ITEM</c> when displaying this response; its target, Order, and
/// Price response fields have no consumer in the inspected 7.69 source.
/// </summary>
internal static class LegacyRecycleBinClientProjection
{
    public static RepurchaseItemsMessage ToRepurchaseItemsMessage(
        int target,
        IReadOnlyList<LegacyRecycleBinEntry> recycleBin)
    {
        ArgumentNullException.ThrowIfNull(recycleBin);
        if (recycleBin.Count != LegacyRecycleBinRules.Capacity)
            throw new ArgumentException(
                $"The client recycle-bin projection requires exactly {LegacyRecycleBinRules.Capacity} entries.",
                nameof(recycleBin));

        var entries = new RepurchaseItemEntry[LegacyRecycleBinRules.Capacity];
        for (var index = 0; index < entries.Length; index++)
            entries[index] = new RepurchaseItemEntry(Order: 0, recycleBin[index].Item, Price: 0);

        return new RepurchaseItemsMessage(target, entries);
    }
}

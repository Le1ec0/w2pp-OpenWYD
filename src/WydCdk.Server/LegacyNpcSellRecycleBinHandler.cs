using WydCdk.Protocol;
using WydCdk.World;

namespace WydCdk.Server;

/// <summary>
/// Applies the GetRecycleBin stage of TMProject 7.69 _MSG_Sell after price
/// calculation and before the later gold/item mutations. W2PP's sell handler
/// does not call GetRecycleBin, so this stage follows the chosen 7.69 contract.
/// </summary>
internal static class LegacyNpcSellRecycleBinHandler
{
    public static LegacyNpcSellRecycleBinResult TryRecordSale(
        WorldHub world,
        int connectionId,
        LegacyItem item,
        LegacyNpcSellPriceResult price,
        uint randomValue,
        out LegacyRecycleBinInsertResolution? insertion)
    {
        ArgumentNullException.ThrowIfNull(world);
        insertion = null;

        if (!price.IsSuccess)
            return LegacyNpcSellRecycleBinResult.PriceRejected;

        // 7.69 jumps directly to LABEL_S2 for an invalid village. That path
        // still sells the item, but it bypasses tax revenue and GetRecycleBin.
        if (price.NpcVillage is < 0 or > 5)
            return LegacyNpcSellRecycleBinResult.ContinueWithoutRecycleBin;

        if (!world.TryRecordRecycleBinSale(connectionId, item, price.Amount, randomValue, out insertion))
            return LegacyNpcSellRecycleBinResult.WorldSessionUnavailable;

        return insertion is { WasInserted: true }
            ? LegacyNpcSellRecycleBinResult.Inserted
            : LegacyNpcSellRecycleBinResult.InsertionRejected;
    }
}

internal enum LegacyNpcSellRecycleBinResult
{
    Inserted,
    ContinueWithoutRecycleBin,
    PriceRejected,
    WorldSessionUnavailable,
    InsertionRejected,
}

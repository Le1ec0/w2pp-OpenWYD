using WydCdk.Protocol;

namespace WydCdk.World;

public sealed partial class WorldHub
{
    /// <summary>Returns a copy of the transient recycle-bin state owned by one active world session.</summary>
    public bool TryGetRecycleBinSnapshot(int connectionId, out LegacyRecycleBinEntry[]? entries)
    {
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant))
            {
                entries = null;
                return false;
            }

            entries = participant.RecycleBin.ToArray();
            return true;
        }
    }

    /// <summary>Applies the legacy sale insertion transition to one active world session.</summary>
    public bool TryRecordRecycleBinSale(
        int connectionId,
        LegacyItem item,
        int price,
        uint randomValue,
        out LegacyRecycleBinInsertResolution? resolution)
    {
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant))
            {
                resolution = null;
                return false;
            }

            var result = LegacyRecycleBinRules.InsertSale(
                connectionId,
                LoginSessionState.Playing,
                participant.RecycleBin,
                item,
                price,
                randomValue);
            resolution = result;
            if (result.WasInserted)
                participant.RecycleBin = result.Entries.ToArray();
            return true;
        }
    }
}

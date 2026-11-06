using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Models the session-local sale insertion performed by 7.69/W2PP
/// <c>GetRecycleBin</c>. It returns a new fixed-size state and never mutates
/// the caller's collection.
/// </summary>
public static class LegacyRecycleBinRules
{
    public const int Capacity = 10;

    private const int MaximumPlayerConnectionId = 1000;
    private const int MaximumItemIndex = 6500;
    private const uint TickModulo = 100_000;

    public static LegacyRecycleBinInsertResolution InsertSale(
        int connectionId,
        LoginSessionState connectionState,
        IReadOnlyList<LegacyRecycleBinEntry> existingEntries,
        LegacyItem item,
        int price,
        uint randomValue)
    {
        if (connectionId <= 0 || connectionId >= MaximumPlayerConnectionId)
            return new(LegacyRecycleBinInsertStatus.InvalidConnection, existingEntries, null);

        if (connectionState != LoginSessionState.Playing)
            return new(LegacyRecycleBinInsertStatus.NotPlaying, existingEntries, null);

        if (existingEntries.Count != Capacity)
            return new(LegacyRecycleBinInsertStatus.InvalidCapacity, existingEntries, null);

        var updatedEntries = existingEntries.ToArray();
        var insertIndex = Array.FindIndex(
            updatedEntries,
            static entry => entry.Item.Index <= 0 || entry.Item.Index >= MaximumItemIndex);

        if (insertIndex < 0)
        {
            Array.Copy(updatedEntries, 1, updatedEntries, 0, Capacity - 1);
            insertIndex = Capacity - 1;
        }

        updatedEntries[insertIndex] = new LegacyRecycleBinEntry(
            price,
            item,
            (int)(randomValue % TickModulo));

        return new(LegacyRecycleBinInsertStatus.Inserted, updatedEntries, insertIndex);
    }
}

public readonly record struct LegacyRecycleBinEntry(int Price, LegacyItem Item, int Ticks);

public enum LegacyRecycleBinInsertStatus
{
    Inserted,
    InvalidConnection,
    NotPlaying,
    InvalidCapacity,
}

public readonly record struct LegacyRecycleBinInsertResolution(
    LegacyRecycleBinInsertStatus Status,
    IReadOnlyList<LegacyRecycleBinEntry> Entries,
    int? InsertedIndex)
{
    public bool WasInserted => Status == LegacyRecycleBinInsertStatus.Inserted;
}

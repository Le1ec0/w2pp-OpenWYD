using WydCdk.Server;
using WydCdk.Protocol;
using WydCdk.World;

internal static class LegacyRecycleBinClientProjectionTests
{
    public static void Run()
    {
        ProjectsItemsIntoTheClientVisibleRows();
        RejectsWrongRecycleBinCapacity();
    }

    private static void ProjectsItemsIntoTheClientVisibleRows()
    {
        var source = Enumerable.Range(0, LegacyRecycleBinRules.Capacity)
            .Select(index => new LegacyRecycleBinEntry(
                10_000 + index,
                new LegacyItem((short)(500 + index), (byte)index, 2, 3, 4, 5, 6),
                20_000 + index))
            .ToArray();
        source[3] = default;
        var before = source.ToArray();

        var message = LegacyRecycleBinClientProjection.ToRepurchaseItemsMessage(20_025, source);

        Check(message.Target == 20_025 && message.Entries.Count == LegacyRecycleBinRules.Capacity,
            "The client projection did not preserve the request target and ten fixed rows.");
        for (var index = 0; index < source.Length; index++)
        {
            Check(message.Entries[index].Item == source[index].Item,
                $"Client-visible item {index} was not preserved at its recycle-bin position.");
            Check(message.Entries[index].Order == 0 && message.Entries[index].Price == 0,
                $"Server-only recycle fields were invented as client Order/Price values at row {index}.");
        }
        Check(source.SequenceEqual(before), "The client projection mutated authoritative recycle-bin state.");

        var codec = LegacyFrameCodec.CreateDefault();
        var frame = codec.Decode(message.ToFrame(codec, 123, 7, 25));
        Check(frame.IsChecksumValid && RepurchaseItemsMessage.TryParse(frame, out var parsed) && parsed is not null &&
              parsed.Target == message.Target && parsed.Entries.SequenceEqual(message.Entries),
            "The projected recycle-bin message did not preserve its items through client wire serialization.");
    }

    private static void RejectsWrongRecycleBinCapacity()
    {
        try
        {
            _ = LegacyRecycleBinClientProjection.ToRepurchaseItemsMessage(
                0, new LegacyRecycleBinEntry[LegacyRecycleBinRules.Capacity - 1]);
        }
        catch (ArgumentException)
        {
            return;
        }

        throw new InvalidOperationException("The recycle-bin client projection accepted a non-legacy capacity.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

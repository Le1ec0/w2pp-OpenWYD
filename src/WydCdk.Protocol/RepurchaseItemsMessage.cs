using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Client-side 7.69 <c>MSG_RepurchaseItems</c> used for both the request and
/// the recycle-bin display response. The server-side <c>MSG_Recycle</c> in the
/// current TMProject/W2PP TMSrv source has a different body layout and needs an
/// explicit adapter before it can be used for this client contract.
/// </summary>
public sealed class RepurchaseItemsMessage
{
    public const ushort MessageType = 0x03E8;
    public const int EntryCount = 10;
    public const int TargetOffset = 0;
    public const int EntriesOffset = sizeof(int);
    public const int PayloadSize = EntriesOffset + (EntryCount * RepurchaseItemEntry.SizeInBytes);
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    private readonly RepurchaseItemEntry[] entries;

    public RepurchaseItemsMessage(int target, IReadOnlyList<RepurchaseItemEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count != EntryCount)
            throw new ArgumentException($"Client MSG_RepurchaseItems requires exactly {EntryCount} entries.", nameof(entries));

        Target = target;
        this.entries = entries.ToArray();
    }

    public int Target { get; }
    public IReadOnlyList<RepurchaseItemEntry> Entries => entries;

    public static bool TryParse(DecodedFrame frame, out RepurchaseItemsMessage? message)
    {
        message = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType ||
            frame.Header.Size != PacketSize || frame.Payload.Length != PayloadSize)
            return false;

        var payload = frame.Payload.Span;
        var entries = new RepurchaseItemEntry[EntryCount];
        for (var index = 0; index < EntryCount; index++)
        {
            var offset = EntriesOffset + (index * RepurchaseItemEntry.SizeInBytes);
            entries[index] = RepurchaseItemEntry.Read(payload.Slice(offset, RepurchaseItemEntry.SizeInBytes));
        }

        message = new RepurchaseItemsMessage(
            BinaryPrimitives.ReadInt32LittleEndian(payload[TargetOffset..]), entries);
        return true;
    }

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(TargetOffset), Target);
        for (var index = 0; index < entries.Length; index++)
        {
            var offset = EntriesOffset + (index * RepurchaseItemEntry.SizeInBytes);
            entries[index].Write(payload.AsSpan(offset, RepurchaseItemEntry.SizeInBytes));
        }

        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort headerId)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, headerId, clientTick, ToPayload(), keywordIndex);
    }
}

/// <summary>Native client <c>STRUCT_REPURCHASEITEM</c>: Order, STRUCT_ITEM, Price.</summary>
public readonly record struct RepurchaseItemEntry(int Order, LegacyItem Item, int Price)
{
    public const int SizeInBytes = sizeof(int) + LegacyItem.SizeInBytes + sizeof(int);

    public static RepurchaseItemEntry Read(ReadOnlySpan<byte> source)
    {
        if (source.Length < SizeInBytes)
            throw new ArgumentException($"Repurchase item entry requires {SizeInBytes} bytes.", nameof(source));

        return new RepurchaseItemEntry(
            BinaryPrimitives.ReadInt32LittleEndian(source),
            LegacyItem.Read(source[sizeof(int)..]),
            BinaryPrimitives.ReadInt32LittleEndian(source[(sizeof(int) + LegacyItem.SizeInBytes)..]));
    }

    public void Write(Span<byte> destination)
    {
        if (destination.Length < SizeInBytes)
            throw new ArgumentException($"Repurchase item entry requires {SizeInBytes} bytes.", nameof(destination));

        BinaryPrimitives.WriteInt32LittleEndian(destination, Order);
        Item.Write(destination[sizeof(int)..]);
        BinaryPrimitives.WriteInt32LittleEndian(destination[(sizeof(int) + LegacyItem.SizeInBytes)..], Price);
    }
}

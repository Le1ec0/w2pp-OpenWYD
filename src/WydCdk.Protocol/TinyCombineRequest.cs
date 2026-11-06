namespace WydCdk.Protocol;

/// <summary>Exact 7.69 <c>MSG_CombineItemTiny</c> request.</summary>
public sealed record TinyCombineRequest(
    IReadOnlyList<LegacyItem> Items,
    IReadOnlyList<sbyte> InventoryPositions)
{
    public const ushort MessageType = 0x03C0; // 192 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int ItemCount = 8;
    public const int ItemOffset = 0;
    public const int InventoryPositionOffset = ItemCount * LegacyItem.SizeInBytes;
    public const int PayloadSize = InventoryPositionOffset + ItemCount;
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public static bool TryParse(DecodedFrame frame, out TinyCombineRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType ||
            frame.Header.Size != PacketSize || frame.Payload.Length != PayloadSize)
            return false;

        var payload = frame.Payload.Span;
        var items = new LegacyItem[ItemCount];
        var inventoryPositions = new sbyte[ItemCount];
        for (var index = 0; index < ItemCount; index++)
        {
            var itemOffset = ItemOffset + (index * LegacyItem.SizeInBytes);
            items[index] = LegacyItem.Read(payload.Slice(itemOffset, LegacyItem.SizeInBytes));
            inventoryPositions[index] = unchecked((sbyte)payload[InventoryPositionOffset + index]);
        }

        request = new TinyCombineRequest(items, inventoryPositions);
        return true;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort headerId)
    {
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(Items);
        ArgumentNullException.ThrowIfNull(InventoryPositions);
        if (Items.Count != ItemCount || InventoryPositions.Count != ItemCount)
            throw new ArgumentException($"Tiny combine requires exactly {ItemCount} items and inventory positions.");

        var payload = new byte[PayloadSize];
        for (var index = 0; index < ItemCount; index++)
        {
            var itemOffset = ItemOffset + (index * LegacyItem.SizeInBytes);
            Items[index].Write(payload.AsSpan(itemOffset, LegacyItem.SizeInBytes));
            payload[InventoryPositionOffset + index] = unchecked((byte)InventoryPositions[index]);
        }

        return codec.Encode(MessageType, headerId, clientTick, payload, keywordIndex);
    }
}

using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Parsed legacy <c>MSG_STANDARDPARM</c> used by <c>_MSG_ReqTradeList</c>.</summary>
public sealed record TradeListRequest(int TargetId)
{
    public const ushort MessageType = 0x039A; // 154 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int PayloadSize = sizeof(int);
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public static bool TryParse(DecodedFrame frame, out TradeListRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != PayloadSize)
            return false;

        request = new TradeListRequest(BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span));
        return true;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort headerId = 0)
    {
        ArgumentNullException.ThrowIfNull(codec);
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(payload, TargetId);
        return codec.Encode(MessageType, headerId, clientTick, payload, keywordIndex);
    }
}

/// <summary>Empty legacy <c>MSG_STANDARD</c> request used by <c>_MSG_QuitTrade</c>.</summary>
public static class TradeCloseRequest
{
    public const ushort MessageType = 0x0384; // 132 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int PacketSize = PacketHeader.SizeInBytes;

    public static bool IsValid(DecodedFrame frame) =>
        frame.IsChecksumValid &&
        frame.Header.Type == MessageType &&
        frame.Header.Size == PacketSize &&
        frame.Payload.IsEmpty;

    public static byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort headerId = 0)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, headerId, clientTick, ReadOnlySpan<byte>.Empty, keywordIndex);
    }
}

/// <summary>Empty legacy confirmation signal emitted after one trade side checks.</summary>
public static class TradeCheckConfirmation
{
    public const ushort MessageType = 0x0186; // 134 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int PacketSize = PacketHeader.SizeInBytes;

    public static bool IsValid(DecodedFrame frame) =>
        frame.IsChecksumValid &&
        frame.Header.Type == MessageType &&
        frame.Header.Size == PacketSize &&
        frame.Payload.IsEmpty;

    public static byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort headerId = 0)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, headerId, clientTick, ReadOnlySpan<byte>.Empty, keywordIndex);
    }
}

/// <summary>
/// Exact 7.69/W2PP <c>MSG_Trade</c> offer. The two ABI padding bytes are
/// explicit because the native struct is not packed.
/// </summary>
public sealed record TradeOfferRequest(
    IReadOnlyList<LegacyItem> Items,
    IReadOnlyList<sbyte> InventoryPositions,
    int TradeMoney,
    byte MyCheck,
    ushort OpponentId,
    byte AbiPaddingBeforeTradeMoney = 0,
    byte AbiPaddingAfterCheck = 0)
{
    public const ushort MessageType = 0x0383; // 131 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int ItemCount = 15;
    public const int ItemOffset = 0;
    public const int InventoryPositionOffset = ItemOffset + (ItemCount * LegacyItem.SizeInBytes);
    public const int AbiPaddingBeforeTradeMoneyOffset = InventoryPositionOffset + ItemCount;
    public const int TradeMoneyOffset = AbiPaddingBeforeTradeMoneyOffset + 1;
    public const int MyCheckOffset = TradeMoneyOffset + sizeof(int);
    public const int AbiPaddingAfterCheckOffset = MyCheckOffset + sizeof(byte);
    public const int OpponentIdOffset = AbiPaddingAfterCheckOffset + 1;
    public const int PayloadSize = OpponentIdOffset + sizeof(ushort);
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public static bool TryParse(DecodedFrame frame, out TradeOfferRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != PayloadSize)
            return false;

        var payload = frame.Payload.Span;
        var items = new LegacyItem[ItemCount];
        for (var index = 0; index < ItemCount; index++)
            items[index] = LegacyItem.Read(payload.Slice(ItemOffset + (index * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));

        var inventoryPositions = new sbyte[ItemCount];
        for (var index = 0; index < ItemCount; index++)
            inventoryPositions[index] = unchecked((sbyte)payload[InventoryPositionOffset + index]);

        request = new TradeOfferRequest(
            items,
            inventoryPositions,
            BinaryPrimitives.ReadInt32LittleEndian(payload[TradeMoneyOffset..]),
            payload[MyCheckOffset],
            BinaryPrimitives.ReadUInt16LittleEndian(payload[OpponentIdOffset..]),
            payload[AbiPaddingBeforeTradeMoneyOffset],
            payload[AbiPaddingAfterCheckOffset]);
        return true;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort headerId = 0)
    {
        ArgumentNullException.ThrowIfNull(codec);
        if (Items.Count != ItemCount)
            throw new ArgumentException($"Legacy trade requires exactly {ItemCount} items.", nameof(Items));
        if (InventoryPositions.Count != ItemCount)
            throw new ArgumentException($"Legacy trade requires exactly {ItemCount} inventory positions.", nameof(InventoryPositions));

        var payload = new byte[PayloadSize];
        for (var index = 0; index < ItemCount; index++)
            Items[index].Write(payload.AsSpan(ItemOffset + (index * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        for (var index = 0; index < ItemCount; index++)
            payload[InventoryPositionOffset + index] = unchecked((byte)InventoryPositions[index]);

        payload[AbiPaddingBeforeTradeMoneyOffset] = AbiPaddingBeforeTradeMoney;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(TradeMoneyOffset), TradeMoney);
        payload[MyCheckOffset] = MyCheck;
        payload[AbiPaddingAfterCheckOffset] = AbiPaddingAfterCheck;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(OpponentIdOffset), OpponentId);
        return codec.Encode(MessageType, headerId, clientTick, payload, keywordIndex);
    }
}

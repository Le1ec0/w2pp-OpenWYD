using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Exact 7.69 <c>MSG_ReqBuy</c> request used to purchase one item from an
/// autotrade listing. The two bytes between <see cref="TargetId"/> and
/// <see cref="Price"/> are the x86 ABI padding present in the native struct.
/// </summary>
public sealed record AutoTradePurchaseRequest(
    int Position,
    ushort TargetId,
    int Price,
    int Tax,
    LegacyItem Item,
    byte AbiPadding0 = 0,
    byte AbiPadding1 = 0)
{
    public const ushort MessageType = 0x0398; // 152 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int PositionOffset = 0;
    public const int TargetIdOffset = PositionOffset + sizeof(int);
    public const int AbiPaddingOffset = TargetIdOffset + sizeof(ushort);
    public const int PriceOffset = AbiPaddingOffset + 2;
    public const int TaxOffset = PriceOffset + sizeof(int);
    public const int ItemOffset = TaxOffset + sizeof(int);
    public const int PayloadSize = ItemOffset + LegacyItem.SizeInBytes;
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(PositionOffset), Position);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(TargetIdOffset), TargetId);
        payload[AbiPaddingOffset] = AbiPadding0;
        payload[AbiPaddingOffset + 1] = AbiPadding1;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(PriceOffset), Price);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(TaxOffset), Tax);
        Item.Write(payload.AsSpan(ItemOffset, LegacyItem.SizeInBytes));
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort headerId = 0)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, headerId, clientTick, ToPayload(), keywordIndex);
    }

    public static bool TryParse(DecodedFrame frame, out AutoTradePurchaseRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != PayloadSize)
            return false;

        var payload = frame.Payload.Span;
        request = new AutoTradePurchaseRequest(
            BinaryPrimitives.ReadInt32LittleEndian(payload[PositionOffset..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[TargetIdOffset..]),
            BinaryPrimitives.ReadInt32LittleEndian(payload[PriceOffset..]),
            BinaryPrimitives.ReadInt32LittleEndian(payload[TaxOffset..]),
            LegacyItem.Read(payload.Slice(ItemOffset, LegacyItem.SizeInBytes)),
            payload[AbiPaddingOffset],
            payload[AbiPaddingOffset + 1]);
        return true;
    }
}

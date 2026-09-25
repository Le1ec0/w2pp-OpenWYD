using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Legacy <c>MSG_ItemSold</c> notification. The 7.69 client removes the
/// matching row from its autotrade panel when <see cref="SellerId"/> and
/// <see cref="Position"/> identify the visible listing.
/// </summary>
public sealed record ItemSoldConfirmation(int SellerId, int Position)
{
    public const ushort MessageType = 0x039B; // 155 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const ushort SceneId = 30_000; // ESCENE_FIELD
    public const int SellerIdOffset = 0;
    public const int PositionOffset = SellerIdOffset + sizeof(int);
    public const int PayloadSize = PositionOffset + sizeof(int);
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(SellerIdOffset), SellerId);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(PositionOffset), Position);
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort headerId = SceneId)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, headerId, clientTick, ToPayload(), keywordIndex);
    }

    public static bool TryParse(DecodedFrame frame, out ItemSoldConfirmation? confirmation)
    {
        confirmation = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != PayloadSize)
            return false;

        confirmation = new ItemSoldConfirmation(
            BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[SellerIdOffset..]),
            BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[PositionOffset..]));
        return true;
    }
}

using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// The shared 7.69 <c>MSG_Buy</c> request/confirmation used by the retail
/// shop path. The client sends <see cref="Coin"/> as zero; only the server's
/// confirmation value is authoritative.
/// </summary>
public sealed record ShopBuyMessage(
    ushort TargetId,
    short TargetCarryPos,
    short MyCarryPos,
    int Coin,
    ushort AlignmentPadding = 0)
{
    public const ushort MessageType = 0x0379; // 121 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int TargetIdOffset = 0;
    public const int TargetCarryPosOffset = TargetIdOffset + sizeof(ushort);
    public const int MyCarryPosOffset = TargetCarryPosOffset + sizeof(short);
    public const int AlignmentPaddingOffset = MyCarryPosOffset + sizeof(short);
    public const int CoinOffset = AlignmentPaddingOffset + sizeof(ushort);
    public const int PayloadSize = CoinOffset + sizeof(int);
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(TargetIdOffset), TargetId);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(TargetCarryPosOffset), TargetCarryPos);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(MyCarryPosOffset), MyCarryPos);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(AlignmentPaddingOffset), AlignmentPadding);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(CoinOffset), Coin);
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id = 0)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, id, clientTick, ToPayload(), keywordIndex);
    }

    public static bool TryParse(DecodedFrame frame, out ShopBuyMessage? message)
    {
        message = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize ||
            frame.Payload.Length != PayloadSize)
            return false;

        message = new ShopBuyMessage(
            BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[TargetIdOffset..]),
            BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[TargetCarryPosOffset..]),
            BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[MyCarryPosOffset..]),
            BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[CoinOffset..]),
            BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[AlignmentPaddingOffset..]));
        return true;
    }
}

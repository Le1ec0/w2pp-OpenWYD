using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// The shared 7.69 <c>MSG_Sell</c> request/echo shape used by the retail NPC
/// sell flow. Inventory type and slot stay signed as in the legacy ABI.
/// </summary>
public sealed record ShopSellMessage(
    ushort TargetId,
    short MyType,
    short MyPos,
    ushort AlignmentPadding = 0)
{
    public const ushort MessageType = 0x037A; // 122 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int TargetIdOffset = 0;
    public const int MyTypeOffset = TargetIdOffset + sizeof(ushort);
    public const int MyPosOffset = MyTypeOffset + sizeof(short);
    public const int AlignmentPaddingOffset = MyPosOffset + sizeof(short);
    public const int PayloadSize = AlignmentPaddingOffset + sizeof(ushort);
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(TargetIdOffset), TargetId);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(MyTypeOffset), MyType);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(MyPosOffset), MyPos);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(AlignmentPaddingOffset), AlignmentPadding);
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id = 0)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, id, clientTick, ToPayload(), keywordIndex);
    }

    public static bool TryParse(DecodedFrame frame, out ShopSellMessage? message)
    {
        message = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize ||
            frame.Payload.Length != PayloadSize)
            return false;

        message = new ShopSellMessage(
            BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[TargetIdOffset..]),
            BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[MyTypeOffset..]),
            BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[MyPosOffset..]),
            BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[AlignmentPaddingOffset..]));
        return true;
    }
}

using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Parsed legacy <c>MSG_UseItem</c> request.</summary>
public sealed record UseItemRequest(
    int SourceType,
    int SourceSlot,
    int DestinationType,
    int DestinationSlot,
    ushort GridX,
    ushort GridY,
    ushort WarpId)
{
    public const ushort MessageType = 0x0373; // 115 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int PacketSize = 36;
    public const int PayloadSize = PacketSize - PacketHeader.SizeInBytes;

    public static bool TryParse(DecodedFrame frame, out UseItemRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != PayloadSize)
            return false;

        var payload = frame.Payload.Span;
        request = new UseItemRequest(
            BinaryPrimitives.ReadInt32LittleEndian(payload),
            BinaryPrimitives.ReadInt32LittleEndian(payload[4..]),
            BinaryPrimitives.ReadInt32LittleEndian(payload[8..]),
            BinaryPrimitives.ReadInt32LittleEndian(payload[12..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[16..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[18..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[20..]));
        return true;
    }
}

using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Parsed legacy <c>MSG_SplitItem</c> request.</summary>
public sealed record SplitItemRequest(int Slot, int ItemIndex, int Quantity)
{
    public const ushort MessageType = 0x02E5; // 229 | FLAG_CLIENT2GAME
    public const int PayloadSize = sizeof(int) * 3;
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public static bool TryParse(DecodedFrame frame, out SplitItemRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != PayloadSize)
            return false;

        var payload = frame.Payload.Span;
        request = new SplitItemRequest(
            BinaryPrimitives.ReadInt32LittleEndian(payload),
            BinaryPrimitives.ReadInt32LittleEndian(payload[sizeof(int)..]),
            BinaryPrimitives.ReadInt32LittleEndian(payload[(sizeof(int) * 2)..]));
        return true;
    }
}

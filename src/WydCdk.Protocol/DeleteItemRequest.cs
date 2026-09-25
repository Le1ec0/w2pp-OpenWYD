using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Parsed legacy <c>MSG_DeleteItem</c> request.</summary>
public sealed record DeleteItemRequest(int Slot, int ItemIndex)
{
    public const ushort MessageType = 0x02E4; // 228 | FLAG_CLIENT2GAME
    public const int PayloadSize = sizeof(int) * 2;
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public static bool TryParse(DecodedFrame frame, out DeleteItemRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != PayloadSize)
            return false;

        var payload = frame.Payload.Span;
        request = new DeleteItemRequest(
            BinaryPrimitives.ReadInt32LittleEndian(payload),
            BinaryPrimitives.ReadInt32LittleEndian(payload[sizeof(int)..]));
        return true;
    }
}

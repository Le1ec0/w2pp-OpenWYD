using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Parsed legacy <c>MSG_STANDARDPARM2</c> used by <c>_MSG_Quest</c>.</summary>
public sealed record QuestRequest(int NpcConnectionId, int Confirm)
{
    public const ushort MessageType = 0x028B; // 139 | FLAG_CLIENT2GAME
    public const int PacketSize = PacketHeader.SizeInBytes + (sizeof(int) * 2);

    public static bool TryParse(DecodedFrame frame, out QuestRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != PacketSize - PacketHeader.SizeInBytes)
            return false;

        request = new QuestRequest(
            BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span),
            BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[sizeof(int)..]));
        return true;
    }
}

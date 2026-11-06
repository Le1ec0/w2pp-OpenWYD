using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Parsed legacy <c>MSG_ApplyBonus</c> request.</summary>
public sealed record ApplyBonusRequest(short BonusType, short Detail, ushort TargetId)
{
    public const ushort MessageType = 0x0119; // 119 | FLAG_CLIENT2GAME
    public const int PayloadSize = 8;
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public static bool TryParse(DecodedFrame frame, out ApplyBonusRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType ||
            frame.Header.Size != PacketSize || frame.Payload.Length != PayloadSize)
            return false;

        var payload = frame.Payload.Span;
        request = new ApplyBonusRequest(
            BinaryPrimitives.ReadInt16LittleEndian(payload),
            BinaryPrimitives.ReadInt16LittleEndian(payload[sizeof(short)..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[(sizeof(short) * 2)..]));
        return true;
    }
}

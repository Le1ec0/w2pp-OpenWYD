using System.Buffers.Binary;
using System.Text;

namespace WydCdk.Protocol;

/// <summary>Fixed-layout server experience-panel message used by the 7.69 client.</summary>
public sealed record ExperienceMessagePanel(string Message, uint Color32 = 0xFFFFFF00)
{
    public const ushort MessageType = 0x5000;
    public const uint Yellow = 0xFFFFFF00;
    public const int MessageLength = 96;
    public const int ColorOffset = MessageLength;
    public const int PayloadSize = MessageLength + sizeof(uint);
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id)
    {
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(Message);

        var messageBytes = Encoding.Latin1.GetByteCount(Message);
        if (messageBytes >= MessageLength)
            throw new ArgumentOutOfRangeException(nameof(Message), "The legacy message must leave one null byte in char[96].");

        var payload = new byte[PayloadSize];
        Encoding.Latin1.GetBytes(Message.AsSpan(), payload.AsSpan(0, messageBytes));
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(ColorOffset), Color32);
        return codec.Encode(MessageType, id, clientTick, payload, keywordIndex);
    }
}

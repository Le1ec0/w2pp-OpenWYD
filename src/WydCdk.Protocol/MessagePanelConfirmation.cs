using System.Text;

namespace WydCdk.Protocol;

/// <summary>Encoder for the fixed-size legacy <c>MSG_MessagePanel</c> server notification.</summary>
public sealed class MessagePanelConfirmation(string message)
{
    public const ushort MessageType = 0x0101; // 1 | FLAG_GAME2CLIENT
    public const int MessageLength = 128;
    public const int PacketSize = PacketHeader.SizeInBytes + MessageLength;

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(codec);
        var payload = new byte[MessageLength];
        var count = Math.Min(Encoding.ASCII.GetByteCount(message), MessageLength - 1);
        Encoding.ASCII.GetBytes(message.AsSpan(), payload.AsSpan(0, count));
        return codec.Encode(MessageType, 0, clientTick, payload, keywordIndex);
    }
}

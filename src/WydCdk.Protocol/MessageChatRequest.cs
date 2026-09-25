using System.Text;

namespace WydCdk.Protocol;

/// <summary>Parsed fixed-size legacy <c>MSG_MessageChat</c> sent by a player.</summary>
public sealed record MessageChatRequest(string Message)
{
    public const ushort MessageType = 0x0333; // 51 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int MessageLength = 128;
    public const int PacketSize = PacketHeader.SizeInBytes + MessageLength;

    public static bool TryParse(DecodedFrame frame, out MessageChatRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != MessageLength)
            return false;

        var payload = frame.Payload.Span;
        var length = payload.IndexOf((byte)0);
        if (length < 0) length = MessageLength;
        if (length == 0) return false;

        request = new MessageChatRequest(Encoding.ASCII.GetString(payload[..length]));
        return true;
    }
}

/// <summary>Encoder for the same legacy chat wire when relaying it to nearby players.</summary>
public sealed class MessageChatConfirmation(string message)
{
    public const ushort MessageType = MessageChatRequest.MessageType;
    public const int MessageLength = MessageChatRequest.MessageLength;
    public const int PacketSize = MessageChatRequest.PacketSize;

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort senderId)
    {
        ArgumentNullException.ThrowIfNull(codec);
        var payload = new byte[MessageLength];
        var count = Math.Min(Encoding.ASCII.GetByteCount(message), MessageLength - 1);
        Encoding.ASCII.GetBytes(message.AsSpan(), payload.AsSpan(0, count));
        return codec.Encode(MessageType, senderId, clientTick, payload, keywordIndex);
    }
}

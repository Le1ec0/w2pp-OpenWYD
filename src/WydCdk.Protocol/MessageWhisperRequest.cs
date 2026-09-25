using System.Text;

namespace WydCdk.Protocol;

/// <summary>Parsed fixed-size legacy <c>MSG_MessageWhisper</c> sent by a player.</summary>
public sealed record MessageWhisperRequest(string TargetName, string Message)
{
    public const ushort MessageType = 0x0334; // 52 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int NameLength = 16;
    public const int MessageLength = 100;
    public const int PacketSize = PacketHeader.SizeInBytes + NameLength + MessageLength;

    public static bool TryParse(DecodedFrame frame, out MessageWhisperRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != NameLength + MessageLength)
            return false;

        var payload = frame.Payload.Span;
        var targetLength = payload[..NameLength].IndexOf((byte)0);
        if (targetLength < 0) targetLength = NameLength;
        var messageBytes = payload.Slice(NameLength, MessageLength);
        var messageLength = messageBytes.IndexOf((byte)0);
        if (messageLength < 0) messageLength = MessageLength;
        // The retail client emits /cp as a whisper-shaped frame whose target is
        // "cp" and whose fixed message field is empty.  The legacy handler
        // checks MobName before it ever requires String to be non-empty.
        if (targetLength == 0) return false;

        request = new MessageWhisperRequest(
            Encoding.ASCII.GetString(payload[..targetLength]),
            Encoding.ASCII.GetString(messageBytes[..messageLength]));
        return true;
    }
}

/// <summary>Encoder for the legacy private-message frame delivered to the target player.</summary>
public sealed class MessageWhisperConfirmation(string senderName, string message)
{
    public const ushort MessageType = MessageWhisperRequest.MessageType;
    public const int NameLength = MessageWhisperRequest.NameLength;
    public const int MessageLength = MessageWhisperRequest.MessageLength;
    public const int PacketSize = MessageWhisperRequest.PacketSize;

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort targetId)
    {
        ArgumentNullException.ThrowIfNull(codec);
        var payload = new byte[NameLength + MessageLength];
        var senderBytes = Encoding.ASCII.GetBytes(senderName);
        senderBytes.AsSpan(0, Math.Min(senderBytes.Length, NameLength - 1)).CopyTo(payload);
        var messageBytes = Encoding.ASCII.GetBytes(message);
        messageBytes.AsSpan(0, Math.Min(messageBytes.Length, MessageLength - 1)).CopyTo(payload.AsSpan(NameLength));
        return codec.Encode(MessageType, targetId, clientTick, payload, keywordIndex);
    }
}

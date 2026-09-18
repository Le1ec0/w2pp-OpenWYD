using System.Text;

namespace WydCdk.Protocol;

/// <summary>Encoder for legacy <c>MSG_MessageChat</c> emitted by an NPC.</summary>
public sealed class NpcChatConfirmation(string message)
{
    public const ushort MessageType = 0x0333; // 51 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int MessageLength = 128;
    public const int PacketSize = PacketHeader.SizeInBytes + MessageLength;

    public byte[] ToPayload()
    {
        var payload = new byte[MessageLength];
        var count = Math.Min(Encoding.ASCII.GetByteCount(message), MessageLength - 1);
        Encoding.ASCII.GetBytes(message.AsSpan(), payload.AsSpan(0, count));
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort npcId)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, npcId, clientTick, ToPayload(), keywordIndex);
    }
}

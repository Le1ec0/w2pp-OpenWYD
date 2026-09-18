using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Encoder for the legacy <c>MSG_STANDARDPARM</c> room counter signal
/// (<c>_MSG_MobLeft</c>). The client uses the integer parameter as the
/// remaining generated-mob count for the area it is currently in.
/// </summary>
public sealed class MobLeftConfirmation(int mobCount)
{
    public const ushort MessageType = 0x03B0; // 176 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const ushort SceneId = 30000; // ESCENE_FIELD
    public const int PacketSize = PacketHeader.SizeInBytes + sizeof(int);

    public byte[] ToPayload()
    {
        var payload = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(payload, mobCount);
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, SceneId, clientTick, ToPayload(), keywordIndex);
    }
}

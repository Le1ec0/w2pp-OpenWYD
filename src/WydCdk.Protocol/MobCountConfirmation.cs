using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Encodes TMProject 7.69's <c>_MSG_MobCount</c> room progress signal as
/// <c>MSG_STANDARDSHORTPARM2</c>. The two integer parameters are progress and
/// target; they are independent of the W2PP party <c>MobCount</c> field.
/// </summary>
public sealed class MobCountConfirmation(int mobsLeft, int targetCount)
{
    public const ushort MessageType = 0x03BB; // 187 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const ushort SceneId = 30000; // ESCENE_FIELD
    public const int PayloadSize = sizeof(int) * 2;
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(payload, mobsLeft);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int)), targetCount);
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, SceneId, clientTick, ToPayload(), keywordIndex);
    }
}

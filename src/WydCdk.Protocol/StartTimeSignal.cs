using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Encodes the legacy <c>MSG_STANDARDPARM</c> countdown signal used by
/// <c>CCastleZakum::OpenCastleGate</c> for <c>_MSG_StartTime</c>.
/// </summary>
public sealed class StartTimeConfirmation(int seconds)
{
    public const ushort MessageType = 0x03A1; // 161 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const ushort SceneId = 30_000; // ESCENE_FIELD
    public const int PayloadSize = sizeof(int);
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public int Seconds { get; } = seconds;

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort sceneId = SceneId)
    {
        ArgumentNullException.ThrowIfNull(codec);
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(payload, Seconds);
        return codec.Encode(MessageType, sceneId, clientTick, payload, keywordIndex);
    }
}

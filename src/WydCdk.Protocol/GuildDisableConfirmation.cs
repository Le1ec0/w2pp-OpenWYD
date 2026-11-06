using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>7.69 MSG_GuildDisable scene signal sent after a guild visibility command.</summary>
public sealed class GuildDisableConfirmation(int disabled)
{
    public const ushort MessageType = 0x03A4; // 164 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int PayloadSize = sizeof(int);
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(codec);
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(payload, disabled);
        return codec.Encode(MessageType, AttackConfirmation.SceneId, clientTick, payload, keywordIndex);
    }
}

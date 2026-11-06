using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Encoder for the fixed-size 7.69 <c>_MSG_SendWarInfo</c> state signal.</summary>
public sealed record SendWarInfoConfirmation(int OpponentGuildId, int Clan, int AllyGuildId)
{
    public const ushort MessageType = 0x03A8; // 168 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const ushort SceneId = 30000;
    public const int PacketSize = PacketHeader.SizeInBytes + 12;

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(codec);
        Span<byte> payload = stackalloc byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(payload, OpponentGuildId);
        BinaryPrimitives.WriteInt32LittleEndian(payload[4..], Clan);
        BinaryPrimitives.WriteInt32LittleEndian(payload[8..], AllyGuildId);
        return codec.Encode(MessageType, SceneId, clientTick, payload, keywordIndex);
    }
}

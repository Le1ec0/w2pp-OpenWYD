using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Encoder for the legacy <c>MSG_PKInfo</c> state notification.</summary>
public sealed class PkInfoConfirmation(int targetId, int state)
{
    public const ushort MessageType = 0x0366; // 102 | FLAG_GAME2CLIENT
    public const int PacketSize = PacketHeader.SizeInBytes + sizeof(int);

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(codec);
        var payload = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(payload, state);
        return codec.Encode(MessageType, checked((ushort)targetId), clientTick, payload, keywordIndex);
    }
}

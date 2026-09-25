using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Authoritative legacy <c>MSG_SetHpMode</c> state notification.</summary>
public sealed class SetHpModeConfirmation(int hp, short mode)
{
    public const ushort MessageType = 0x0292; // 146 | FLAG_CLIENT2GAME
    public const int PacketSize = PacketHeader.SizeInBytes + 8;

    public int Hp { get; } = hp;
    public short Mode { get; } = mode;

    public byte[] ToPayload()
    {
        var payload = new byte[PacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteInt32LittleEndian(payload, Hp);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(4), Mode);
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, id, clientTick, ToPayload(), keywordIndex);
    }
}

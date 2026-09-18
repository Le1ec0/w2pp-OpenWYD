using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Authoritative HP/MP synchronization sent when an attack cannot pay its mana cost.</summary>
public sealed class SetHpMpConfirmation
{
    public const ushort MessageType = 0x0181; // 129 | FLAG_GAME2CLIENT
    public const int PacketSize = 28;

    public SetHpMpConfirmation(int hp, int mp, int requestedHp, int requestedMp)
    {
        Hp = hp;
        Mp = mp;
        RequestedHp = requestedHp;
        RequestedMp = requestedMp;
    }

    public int Hp { get; }
    public int Mp { get; }
    public int RequestedHp { get; }
    public int RequestedMp { get; }

    public byte[] ToPayload()
    {
        var payload = new byte[PacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0), Hp);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4), Mp);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(8), RequestedHp);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(12), RequestedMp);
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, id, clientTick, ToPayload(), keywordIndex);
    }
}

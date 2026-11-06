using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>7.69 MSG_SetHpDam: authoritative HP and signed damage relayed around a player.</summary>
public sealed class SetHpDamConfirmation
{
    public const ushort MessageType = 0x018A; // 138 | FLAG_GAME2CLIENT
    public const int PacketSize = 20;

    public SetHpDamConfirmation(int hp, int damage)
    {
        Hp = hp;
        Damage = damage;
    }

    public int Hp { get; }
    public int Damage { get; }

    public byte[] ToPayload()
    {
        var payload = new byte[PacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(), Hp);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4), Damage);
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, id, clientTick, ToPayload(), keywordIndex);
    }
}

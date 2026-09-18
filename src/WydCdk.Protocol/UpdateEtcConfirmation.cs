using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Initial experience/bonus/currency snapshot sent by legacy <c>SendEtc</c>.</summary>
public sealed class UpdateEtcConfirmation
{
    public const ushort MessageType = 0x0337; // 55 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int PacketSize = 56;
    private readonly byte[] mob;
    private readonly int hold;

    public UpdateEtcConfirmation(ReadOnlySpan<byte> mob, ReadOnlySpan<byte> mobExtra = default)
    {
        if (mob.Length < 796) throw new ArgumentException("A complete STRUCT_MOB snapshot is required.", nameof(mob));
        this.mob = mob.ToArray();
        hold = mobExtra.Length >= 480 ? BinaryPrimitives.ReadInt32LittleEndian(mobExtra[476..]) : 0;
    }

    public byte[] ToPayload()
    {
        var payload = new byte[PacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteInt32LittleEndian(payload, hold); // Hold
        BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(4), BinaryPrimitives.ReadInt64LittleEndian(mob.AsSpan(32))); // Exp
        BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(12), BinaryPrimitives.ReadUInt32LittleEndian(mob.AsSpan(780))); // Learn
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(20), BinaryPrimitives.ReadUInt16LittleEndian(mob.AsSpan(788))); // ScoreBonus
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(22), BinaryPrimitives.ReadUInt16LittleEndian(mob.AsSpan(790))); // SpecialBonus
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(24), BinaryPrimitives.ReadUInt16LittleEndian(mob.AsSpan(792))); // SkillBonus
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(26), BinaryPrimitives.ReadUInt16LittleEndian(mob.AsSpan(784))); // Magic
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(28), BinaryPrimitives.ReadInt32LittleEndian(mob.AsSpan(28))); // Coin
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, id, clientTick, ToPayload(), keywordIndex);
    }
}

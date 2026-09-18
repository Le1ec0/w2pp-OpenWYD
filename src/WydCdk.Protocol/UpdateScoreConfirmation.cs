using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Initial score snapshot sent by legacy <c>SendScore</c>.</summary>
public sealed class UpdateScoreConfirmation
{
    public const ushort MessageType = 0x0336; // 54 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int PacketSize = 160;
    private readonly byte[] mob;
    private readonly byte[]? affect;

    public UpdateScoreConfirmation(ReadOnlySpan<byte> mob)
    {
        if (mob.Length < 140) throw new ArgumentException("A STRUCT_MOB snapshot is required.", nameof(mob));
        this.mob = mob.ToArray();
    }

    public UpdateScoreConfirmation(ReadOnlySpan<byte> mob, ReadOnlySpan<byte> affect) : this(mob)
    {
        if (affect.Length != 256) throw new ArgumentException("A 256-byte affect snapshot is required.", nameof(affect));
        this.affect = affect.ToArray();
    }

    public byte[] ToPayload()
    {
        var payload = new byte[PacketSize - PacketHeader.SizeInBytes];
        mob.AsSpan(92, 48).CopyTo(payload); // STRUCT_SCORE CurrentScore

        if (affect is not null)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(114), BinaryPrimitives.ReadUInt16LittleEndian(mob.AsSpan(18))); // Guild
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(116), BinaryPrimitives.ReadUInt16LittleEndian(mob.AsSpan(800))); // GuildLevel
            mob.AsSpan(806, 4).CopyTo(payload.AsSpan(118)); // Resist
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(124), BinaryPrimitives.ReadInt32LittleEndian(mob.AsSpan(116))); // Current HP
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(128), BinaryPrimitives.ReadInt32LittleEndian(mob.AsSpan(120))); // Current MP
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(132), BinaryPrimitives.ReadInt32LittleEndian(mob.AsSpan(784))); // Magic
            for (var index = 0; index < 32; index++)
            {
                var affectOffset = index * 8;
                var type = affect[affectOffset];
                var time = BinaryPrimitives.ReadUInt32LittleEndian(affect.AsSpan(affectOffset + 4));
                BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(50 + (index * 2)), (ushort)(((ushort)type << 8) | (ushort)(time & 0xFF)));
            }

            return payload;
        }

        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(116), BinaryPrimitives.ReadUInt16LittleEndian(mob.AsSpan(18))); // Guild
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(140), BinaryPrimitives.ReadInt32LittleEndian(mob.AsSpan(92 + 24))); // Current HP
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(144), BinaryPrimitives.ReadInt32LittleEndian(mob.AsSpan(92 + 28))); // Current MP
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, id, clientTick, ToPayload(), keywordIndex);
    }
}

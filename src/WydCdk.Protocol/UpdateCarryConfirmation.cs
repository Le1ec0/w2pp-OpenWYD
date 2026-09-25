using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Full carry/currency refresh matching legacy <c>MSG_UpdateCarry</c>.</summary>
public sealed class UpdateCarryConfirmation
{
    public const ushort MessageType = 0x0185; // 133 | FLAG_GAME2CLIENT
    public const int CarryCount = 64;
    public const int CarryOffset = 0;
    public const int CoinOffset = CarryCount * LegacyItem.SizeInBytes;
    public const int PayloadSize = (CarryCount * LegacyItem.SizeInBytes) + sizeof(int);
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    private readonly LegacyItem[] carry;

    public UpdateCarryConfirmation(IReadOnlyList<LegacyItem> carry, int coin)
    {
        ArgumentNullException.ThrowIfNull(carry);
        if (carry.Count != CarryCount)
            throw new ArgumentException($"MSG_UpdateCarry requires exactly {CarryCount} items.", nameof(carry));

        this.carry = carry.ToArray();
        Coin = coin;
    }

    public IReadOnlyList<LegacyItem> Carry => carry;
    public int Coin { get; }

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        for (var index = 0; index < carry.Length; index++)
            carry[index].Write(payload.AsSpan(CarryOffset + (index * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));

        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(CoinOffset), Coin);
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, id, clientTick, ToPayload(), keywordIndex);
    }
}

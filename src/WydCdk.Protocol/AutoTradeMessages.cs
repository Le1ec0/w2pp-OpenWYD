using System.Buffers.Binary;
using System.Text;

namespace WydCdk.Protocol;

/// <summary>
/// 7.69 client view of the legacy <c>MSG_SendAutoTrade</c> response. The
/// server source calls the same bytes <c>MSG_SendAutoTrade</c>; the client
/// consumes them as <c>MSG_AutoTrade</c> at opcode <c>0x0397</c>.
/// </summary>
public sealed class AutoTradeListConfirmation
{
    public const ushort MessageType = 0x0397; // 151 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const ushort SceneId = 30_000; // ESCENE_FIELD
    public const int DescriptionLength = 24;
    public const int DescriptionMaxBytes = DescriptionLength - 2;
    public const int SlotCount = 12;
    public const int DescriptionOffset = 0;
    public const int ItemOffset = DescriptionOffset + DescriptionLength;
    public const int CarryPositionOffset = ItemOffset + (SlotCount * LegacyItem.SizeInBytes);
    public const int TradeMoneyOffset = CarryPositionOffset + SlotCount;
    public const int TaxOffset = TradeMoneyOffset + (SlotCount * sizeof(int));
    public const int TargetIdOffset = TaxOffset + sizeof(short);
    public const int PayloadSize = TargetIdOffset + sizeof(ushort);
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public AutoTradeListConfirmation(
        string description,
        IReadOnlyList<LegacyItem> items,
        IReadOnlyList<sbyte> carryPositions,
        IReadOnlyList<int> tradeMoney,
        short tax,
        ushort targetId)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(carryPositions);
        ArgumentNullException.ThrowIfNull(tradeMoney);
        ValidateDescription(description);
        if (items.Count != SlotCount)
            throw new ArgumentException($"Autotrade listing requires exactly {SlotCount} item entries.", nameof(items));
        if (carryPositions.Count != SlotCount)
            throw new ArgumentException($"Autotrade listing requires exactly {SlotCount} carry positions.", nameof(carryPositions));
        if (tradeMoney.Count != SlotCount)
            throw new ArgumentException($"Autotrade listing requires exactly {SlotCount} price entries.", nameof(tradeMoney));

        Description = description;
        Items = items.ToArray();
        CarryPositions = carryPositions.ToArray();
        TradeMoney = tradeMoney.ToArray();
        Tax = tax;
        TargetId = targetId;
    }

    public string Description { get; }
    public IReadOnlyList<LegacyItem> Items { get; }
    public IReadOnlyList<sbyte> CarryPositions { get; }
    public IReadOnlyList<int> TradeMoney { get; }
    public short Tax { get; }
    public ushort TargetId { get; }

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        WriteDescription(payload.AsSpan(DescriptionOffset, DescriptionLength), Description);
        for (var index = 0; index < SlotCount; index++)
            Items[index].Write(payload.AsSpan(ItemOffset + (index * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        for (var index = 0; index < SlotCount; index++)
            payload[CarryPositionOffset + index] = unchecked((byte)CarryPositions[index]);
        for (var index = 0; index < SlotCount; index++)
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(TradeMoneyOffset + (index * sizeof(int))), TradeMoney[index]);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(TaxOffset), Tax);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(TargetIdOffset), TargetId);
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort headerId = SceneId)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, headerId, clientTick, ToPayload(), keywordIndex);
    }

    public static bool TryParse(DecodedFrame frame, out AutoTradeListConfirmation? confirmation)
    {
        confirmation = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != PayloadSize)
            return false;

        var payload = frame.Payload.Span;
        var items = new LegacyItem[SlotCount];
        for (var index = 0; index < SlotCount; index++)
            items[index] = LegacyItem.Read(payload.Slice(ItemOffset + (index * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));

        var carryPositions = new sbyte[SlotCount];
        for (var index = 0; index < SlotCount; index++)
            carryPositions[index] = unchecked((sbyte)payload[CarryPositionOffset + index]);

        var tradeMoney = new int[SlotCount];
        for (var index = 0; index < SlotCount; index++)
            tradeMoney[index] = BinaryPrimitives.ReadInt32LittleEndian(payload[(TradeMoneyOffset + (index * sizeof(int)))..]);

        confirmation = new AutoTradeListConfirmation(
            ReadDescription(payload.Slice(DescriptionOffset, DescriptionLength)),
            items,
            carryPositions,
            tradeMoney,
            BinaryPrimitives.ReadInt16LittleEndian(payload[TaxOffset..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[TargetIdOffset..]));
        return true;
    }

    private static void ValidateDescription(string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        if (bytes.Length > DescriptionMaxBytes || value.Any(static character => character > 0x7F || character == '\0'))
            throw new ArgumentOutOfRangeException(nameof(value), $"Autotrade description must fit in {DescriptionMaxBytes} ASCII bytes.");
    }

    private static void WriteDescription(Span<byte> destination, string value) =>
        Encoding.ASCII.GetBytes(value).CopyTo(destination);

    private static string ReadDescription(ReadOnlySpan<byte> source)
    {
        var terminator = source.IndexOf((byte)0);
        return Encoding.ASCII.GetString(terminator < 0 ? source : source[..terminator]);
    }
}

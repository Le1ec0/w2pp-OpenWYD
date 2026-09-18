using System.Buffers.Binary;
using System.Text;

namespace WydCdk.Protocol;

/// <summary>Custom 7.59 <c>MSG_UpdateDonate</c> carrying the account Donate balance and short Pix key.</summary>
public sealed record DonateBalanceConfirmation(int Cash, string Pix)
{
    public const ushort MessageType = 0x0406; // 262 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int PixLength = 5;
    public const int PayloadSize = sizeof(int) + PixLength;
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(payload, Cash);
        var bytes = Encoding.ASCII.GetBytes(Pix ?? string.Empty);
        bytes.AsSpan(0, Math.Min(bytes.Length, PixLength)).CopyTo(payload.AsSpan(sizeof(int)));
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id = 0)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, id, clientTick, ToPayload(), keywordIndex);
    }

    public static bool TryParse(DecodedFrame frame, out DonateBalanceConfirmation? confirmation)
    {
        confirmation = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != PayloadSize)
            return false;
        var pixBytes = frame.Payload.Span.Slice(sizeof(int), PixLength);
        var terminator = pixBytes.IndexOf((byte)0);
        confirmation = new(
            BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span),
            Encoding.ASCII.GetString(terminator < 0 ? pixBytes : pixBytes[..terminator]));
        return true;
    }
}

/// <summary>One of the 225 slots in the custom Donate Shop catalog.</summary>
public readonly record struct DonateStoreEntry(int Store, int Page, int ItemPosition, int ItemIndex, int Price, int Stock);

/// <summary>Legacy <c>MSG_DonateShop</c> response used when the Donate NPC opens one store page.</summary>
public sealed class DonateShopOpenConfirmation
{
    public const ushort MessageType = 0x0207; // 263 | FLAG_GAME2CLIENT
    public const int ItemCount = 15;
    public const int PayloadSize = sizeof(int) + (ItemCount * LegacyItem.SizeInBytes) + sizeof(int);
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public DonateShopOpenConfirmation(int shopType, IReadOnlyList<LegacyItem> items, int tax)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count != ItemCount)
            throw new ArgumentException("Donate Shop page requires exactly 15 items.", nameof(items));
        ShopType = shopType;
        Items = items.ToArray();
        Tax = tax;
    }

    public int ShopType { get; }
    public IReadOnlyList<LegacyItem> Items { get; }
    public int Tax { get; }

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(payload, ShopType);
        for (var index = 0; index < ItemCount; index++)
            Items[index].Write(payload.AsSpan(sizeof(int) + (index * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int) + (ItemCount * LegacyItem.SizeInBytes)), Tax);
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id = 0)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, id, clientTick, ToPayload(), keywordIndex);
    }

    public static bool TryParse(DecodedFrame frame, out DonateShopOpenConfirmation? confirmation)
    {
        confirmation = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != PayloadSize)
            return false;

        var items = new LegacyItem[ItemCount];
        for (var index = 0; index < ItemCount; index++)
            items[index] = LegacyItem.Read(frame.Payload.Span.Slice(sizeof(int) + (index * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        var taxOffset = sizeof(int) + (ItemCount * LegacyItem.SizeInBytes);
        confirmation = new DonateShopOpenConfirmation(
            BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span),
            items,
            BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[taxOffset..]));
        return true;
    }
}

/// <summary>Custom 7.59 <c>MSG_UpdateDonateStore</c> carrying 3 stores, 5 pages and 15 slots.</summary>
public sealed class DonateStoreCatalogConfirmation
{
    public const ushort MessageType = 0x041C; // 284 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int StoreCount = 3;
    public const int PageCount = 5;
    public const int ItemCountPerPage = 15;
    public const int FieldsPerItem = 3;
    public const int PayloadSize = StoreCount * PageCount * ItemCountPerPage * FieldsPerItem * sizeof(int);
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public DonateStoreCatalogConfirmation(IReadOnlyList<DonateStoreEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count != StoreCount * PageCount * ItemCountPerPage)
            throw new ArgumentException("Donate Shop catalog requires exactly 225 entries.", nameof(entries));
        Entries = entries.ToArray();
    }

    public IReadOnlyList<DonateStoreEntry> Entries { get; }

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        foreach (var entry in Entries)
        {
            if (entry.Store is < 0 or >= StoreCount || entry.Page is < 0 or >= PageCount || entry.ItemPosition is < 0 or >= ItemCountPerPage)
                throw new ArgumentOutOfRangeException(nameof(Entries), "Donate Shop coordinates are outside the legacy 3x5x15 matrix.");
            var linear = ((entry.Store * PageCount + entry.Page) * ItemCountPerPage + entry.ItemPosition) * FieldsPerItem * sizeof(int);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(linear), entry.ItemIndex);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(linear + sizeof(int)), entry.Price);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(linear + (2 * sizeof(int))), entry.Stock);
        }
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id = 0)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, id, clientTick, ToPayload(), keywordIndex);
    }

    public static bool TryParse(DecodedFrame frame, out DonateStoreCatalogConfirmation? confirmation)
    {
        confirmation = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != PayloadSize)
            return false;

        var entries = new DonateStoreEntry[StoreCount * PageCount * ItemCountPerPage];
        var offset = 0;
        var cursor = 0;
        for (var store = 0; store < StoreCount; store++)
        for (var page = 0; page < PageCount; page++)
        for (var position = 0; position < ItemCountPerPage; position++)
        {
            entries[offset++] = new DonateStoreEntry(
                store,
                page,
                position,
                BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[cursor..]),
                BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[(cursor + sizeof(int))..]),
                BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[(cursor + (2 * sizeof(int)))..]));
            cursor += FieldsPerItem * sizeof(int);
        }
        confirmation = new DonateStoreCatalogConfirmation(entries);
        return true;
    }
}

/// <summary>Custom 7.59 <c>MSG_ReqShopDonate</c> purchase request.</summary>
public sealed record DonatePurchaseRequest(int Store, int Page, int ItemPosition, int Quantity)
{
    public const ushort MessageType = 0x0411; // 273 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int PayloadSize = sizeof(int) * 4;
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(payload, Store);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int)), Page);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(2 * sizeof(int)), ItemPosition);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(3 * sizeof(int)), Quantity);
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id = 0)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, id, clientTick, ToPayload(), keywordIndex);
    }

    public static bool TryParse(DecodedFrame frame, out DonatePurchaseRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != PayloadSize)
            return false;
        request = new(
            BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span),
            BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[4..]),
            BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[8..]),
            BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[12..]));
        return true;
    }
}

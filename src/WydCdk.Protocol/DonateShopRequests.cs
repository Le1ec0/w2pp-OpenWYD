using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Legacy <c>MSG_REQShopList</c> used by the Donate Shop client bridge.</summary>
public sealed record DonateShopOpenRequest(ushort Target, ushort Warp, ushort Face, ushort Effect)
{
    public const ushort MessageType = 0x027B; // 123 | FLAG_CLIENT2GAME
    public const int PayloadSize = sizeof(ushort) * 4;
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, Target);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2), Warp);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4), Face);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(6), Effect);
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id = 0)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, id, clientTick, ToPayload(), keywordIndex);
    }

    public static bool TryParse(DecodedFrame frame, out DonateShopOpenRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize ||
            frame.Payload.Length != PayloadSize)
            return false;

        request = new(
            BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span),
            BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[2..]),
            BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[4..]),
            BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[6..]));
        return true;
    }
}

/// <summary>
/// The retail 7.60 NPC click path uses the same opcode with a compact
/// four-byte payload containing the legacy Target and Unk ushort fields. It
/// must remain distinct from the eight-byte Donate bridge payload above: the
/// server cannot infer Warp/Face/Effect from the compact frame without
/// changing the wire contract.
/// </summary>
public sealed record RetailNpcShopRequest(ushort Target, ushort Unk = 0)
{
    public const ushort MessageType = DonateShopOpenRequest.MessageType;
    public const int PayloadSize = sizeof(uint);
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, Target);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2), Unk);
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id = 0)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, id, clientTick, ToPayload(), keywordIndex);
    }

    public static bool TryParse(DecodedFrame frame, out RetailNpcShopRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize ||
            frame.Payload.Length != PayloadSize)
            return false;

        request = new(
            BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span),
            BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[2..]));
        return true;
    }
}

/// <summary>Legacy <c>MSG_ReqAlias</c>; type 1 refreshes Donate balance and type 2 requests the catalog.</summary>
public sealed record DonateShopCatalogRequest(int Kind)
{
    public const ushort MessageType = 0x0418; // 280 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int PayloadSize = sizeof(int);
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(payload, Kind);
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id = 0)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, id, clientTick, ToPayload(), keywordIndex);
    }

    public static bool TryParse(DecodedFrame frame, out DonateShopCatalogRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize ||
            frame.Payload.Length != PayloadSize)
            return false;

        request = new(BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span));
        return true;
    }
}

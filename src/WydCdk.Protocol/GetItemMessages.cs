using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Parsed legacy <c>MSG_GetItem</c> request.</summary>
public sealed record GetItemRequest(int DestinationType, int DestinationSlot, ushort ItemId, ushort GridX, ushort GridY)
{
    public const ushort MessageType = 0x0270; // 112 | FLAG_CLIENT2GAME
    public const int PayloadSize = sizeof(int) + sizeof(int) + (sizeof(ushort) * 4);
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public static bool TryParse(DecodedFrame frame, out GetItemRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != PayloadSize)
            return false;

        var payload = frame.Payload.Span;
        request = new GetItemRequest(
            BinaryPrimitives.ReadInt32LittleEndian(payload),
            BinaryPrimitives.ReadInt32LittleEndian(payload[sizeof(int)..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[(sizeof(int) * 2)..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[((sizeof(int) * 2) + (sizeof(ushort) * 2))..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[((sizeof(int) * 2) + (sizeof(ushort) * 3))..]));
        return true;
    }
}

/// <summary>Legacy <c>MSG_CNFGetItem</c> confirming the destination carry slot.</summary>
public sealed class GetItemConfirmation(int destinationType, int destinationSlot, LegacyItem item)
{
    public const ushort MessageType = 0x0171; // 113 | FLAG_GAME2CLIENT
    public const int PayloadSize = sizeof(int) + sizeof(int) + LegacyItem.SizeInBytes;
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort connectionId)
    {
        ArgumentNullException.ThrowIfNull(codec);
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(payload, destinationType);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int)), destinationSlot);
        item.Write(payload.AsSpan(sizeof(int) * 2));
        return codec.Encode(MessageType, connectionId, clientTick, payload, keywordIndex);
    }
}

/// <summary>Legacy <c>MSG_DecayItem</c> removing a ground item from nearby clients.</summary>
public sealed class DecayItemConfirmation(short itemId, short unknown = 0)
{
    public const ushort MessageType = 0x016F; // 111 | FLAG_GAME2CLIENT
    public const int PayloadSize = sizeof(short) * 2;
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort sceneId = 0)
    {
        ArgumentNullException.ThrowIfNull(codec);
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteInt16LittleEndian(payload, itemId);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(sizeof(short)), unknown);
        return codec.Encode(MessageType, sceneId, clientTick, payload, keywordIndex);
    }
}

using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Parsed legacy <c>MSG_DropItem</c> request.</summary>
public sealed record DropItemRequest(
    int SourceType,
    int SourceSlot,
    int Rotate,
    ushort GridX,
    ushort GridY,
    ushort ItemId)
{
    public const ushort MessageType = 0x0272; // 114 | FLAG_CLIENT2GAME
    public const int PayloadSize = (sizeof(int) * 3) + (sizeof(ushort) * 3);
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public static bool TryParse(DecodedFrame frame, out DropItemRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != PayloadSize)
            return false;

        var payload = frame.Payload.Span;
        request = new DropItemRequest(
            BinaryPrimitives.ReadInt32LittleEndian(payload),
            BinaryPrimitives.ReadInt32LittleEndian(payload[sizeof(int)..]),
            BinaryPrimitives.ReadInt32LittleEndian(payload[(sizeof(int) * 2)..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[(sizeof(int) * 3)..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[((sizeof(int) * 3) + sizeof(ushort))..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[((sizeof(int) * 3) + (sizeof(ushort) * 2))..]));
        return true;
    }
}

/// <summary>Legacy <c>MSG_CNFDropItem</c> confirming the source and final ground cell.</summary>
public sealed class DropItemConfirmation(int sourceType, int sourceSlot, int rotate, ushort gridX, ushort gridY)
{
    public const ushort MessageType = 0x0175; // 117 | FLAG_GAME2CLIENT
    public const int PayloadSize = (sizeof(int) * 3) + (sizeof(ushort) * 2);
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort sceneId = 30_000)
    {
        ArgumentNullException.ThrowIfNull(codec);
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(payload, sourceType);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int)), sourceSlot);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int) * 2), rotate);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(sizeof(int) * 3), gridX);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan((sizeof(int) * 3) + sizeof(ushort)), gridY);
        return codec.Encode(MessageType, sceneId, clientTick, payload, keywordIndex);
    }
}

/// <summary>Legacy <c>MSG_CreateItem</c> broadcast for a ground item.</summary>
public sealed class CreateItemConfirmation(
    ushort gridX,
    ushort gridY,
    ushort itemId,
    LegacyItem item,
    int rotate,
    byte state = 1,
    byte height = 0,
    byte create = 0)
{
    public const ushort MessageType = 0x026E; // 110 | FLAG_CLIENT2GAME (legacy server also broadcasts this type)
    public const int PayloadSize = (sizeof(ushort) * 3) + LegacyItem.SizeInBytes + (sizeof(byte) * 4);
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort sceneId = 30_000)
    {
        ArgumentNullException.ThrowIfNull(codec);
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, gridX);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(sizeof(ushort)), gridY);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(sizeof(ushort) * 2), itemId);
        item.Write(payload.AsSpan(sizeof(ushort) * 3));
        var flagsOffset = (sizeof(ushort) * 3) + LegacyItem.SizeInBytes;
        payload[flagsOffset] = checked((byte)rotate);
        payload[flagsOffset + 1] = state;
        payload[flagsOffset + 2] = height;
        payload[flagsOffset + 3] = create;
        return codec.Encode(MessageType, sceneId, clientTick, payload, keywordIndex);
    }
}

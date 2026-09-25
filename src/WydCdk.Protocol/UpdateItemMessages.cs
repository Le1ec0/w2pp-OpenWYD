using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Parsed legacy <c>MSG_UpdateItem</c> request for a world gate/item state change.</summary>
public sealed record UpdateItemRequest(int ItemId, int State)
{
    public const ushort MessageType = 0x0374; // 116 | FLAG_CLIENT2GAME | FLAG_GAME2CLIENT
    public const int PayloadSize = sizeof(int) * 2;
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public static bool TryParse(DecodedFrame frame, out UpdateItemRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != PayloadSize)
            return false;

        var payload = frame.Payload.Span;
        request = new UpdateItemRequest(
            BinaryPrimitives.ReadInt32LittleEndian(payload),
            BinaryPrimitives.ReadInt32LittleEndian(payload[sizeof(int)..]));
        return true;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort sceneId = 30_000) =>
        new UpdateItemConfirmation(ItemId, State).ToFrame(codec, clientTick, keywordIndex, sceneId);
}

/// <summary>Legacy scene broadcast confirming the resulting world item state.</summary>
public sealed class UpdateItemConfirmation(int itemId, int state)
{
    public const ushort MessageType = UpdateItemRequest.MessageType;
    public const int PayloadSize = UpdateItemRequest.PayloadSize;
    public const int PacketSize = UpdateItemRequest.PacketSize;

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort sceneId = 30_000)
    {
        ArgumentNullException.ThrowIfNull(codec);
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(payload, itemId);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int)), state);
        return codec.Encode(MessageType, sceneId, clientTick, payload, keywordIndex);
    }
}

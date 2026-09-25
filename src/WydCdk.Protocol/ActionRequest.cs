using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Parsed legacy movement request <c>MSG_Action</c> and its stop/illusion variants.</summary>
public sealed record ActionRequest(short PositionX, short PositionY, int Effect, int Speed, byte[] Route, short TargetX, short TargetY)
{
    public const ushort MessageType = 0x036C; // 108 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const ushort StopMessageType = 0x0366; // 102 | both directions
    public const ushort IllusionMessageType = 0x0368; // 104 | both directions
    public const int PacketSize = 52;
    private const int RouteLength = 24;
    public ushort WireType { get; init; } = MessageType;

    public static bool TryParse(DecodedFrame frame, out ActionRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type is not (MessageType or StopMessageType or IllusionMessageType) || frame.Header.Size != PacketSize || frame.Payload.Length != PacketSize - PacketHeader.SizeInBytes)
            return false;

        var payload = frame.Payload.Span;
        request = new ActionRequest(
            BinaryPrimitives.ReadInt16LittleEndian(payload),
            BinaryPrimitives.ReadInt16LittleEndian(payload[2..]),
            BinaryPrimitives.ReadInt32LittleEndian(payload[4..]),
            BinaryPrimitives.ReadInt32LittleEndian(payload[8..]),
            payload.Slice(12, RouteLength).ToArray(),
            BinaryPrimitives.ReadInt16LittleEndian(payload[36..]),
            BinaryPrimitives.ReadInt16LittleEndian(payload[38..]))
        {
            WireType = frame.Header.Type,
        };
        return true;
    }

    public byte[] ToPayload()
    {
        if (Route.Length != RouteLength) throw new InvalidOperationException($"Legacy action route must contain exactly {RouteLength} bytes.");
        var payload = new byte[PacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteInt16LittleEndian(payload, PositionX);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(2), PositionY);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4), Effect);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(8), Speed);
        Route.CopyTo(payload, 12);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(36), TargetX);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(38), TargetY);
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(WireType, id, clientTick, ToPayload(), keywordIndex);
    }
}

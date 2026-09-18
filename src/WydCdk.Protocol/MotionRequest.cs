using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Legacy <c>MSG_Motion</c> emotion/animation request relayed while the character is playing.</summary>
public sealed record MotionRequest(short Motion, short Parameter, int NotUsed)
{
    public const ushort MessageType = 0x036A; // 106 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int PacketSize = PacketHeader.SizeInBytes + 8;

    public static bool TryParse(DecodedFrame frame, out MotionRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != 8)
            return false;

        var payload = frame.Payload.Span;
        request = new MotionRequest(
            BinaryPrimitives.ReadInt16LittleEndian(payload),
            BinaryPrimitives.ReadInt16LittleEndian(payload[2..]),
            BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
        return true;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id)
    {
        ArgumentNullException.ThrowIfNull(codec);
        Span<byte> payload = stackalloc byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(payload, Motion);
        BinaryPrimitives.WriteInt16LittleEndian(payload[2..], Parameter);
        BinaryPrimitives.WriteInt32LittleEndian(payload[4..], NotUsed);
        return codec.Encode(MessageType, id, clientTick, payload, keywordIndex);
    }
}

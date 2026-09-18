using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Parsed legacy <c>MSG_STANDARDPARM2</c> used by <c>_MSG_InviteGuild</c>.</summary>
public sealed record InviteGuildRequest(int TargetConnectionId, int InviteType)
{
    public const ushort MessageType = 0x03D5; // 213 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int PacketSize = PacketHeader.SizeInBytes + 8;

    public static bool TryParse(DecodedFrame frame, out InviteGuildRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != 8)
            return false;

        request = new(
            BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span),
            BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[4..]));
        return true;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id = 0)
    {
        ArgumentNullException.ThrowIfNull(codec);
        Span<byte> payload = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(payload, TargetConnectionId);
        BinaryPrimitives.WriteInt32LittleEndian(payload[4..], InviteType);
        return codec.Encode(MessageType, id, clientTick, payload, keywordIndex);
    }
}

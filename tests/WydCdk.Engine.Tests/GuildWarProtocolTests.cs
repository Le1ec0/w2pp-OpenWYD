using System.Buffers.Binary;
using WydCdk.Protocol;

internal static class GuildWarProtocolTests
{
    public static void Run()
    {
        ServerWarRequestRoundTripsExactV769Frame();
        RejectsWrongTypeSizeChecksumAndGuildBounds();
        SendWarInfoUsesExactV769Signal();
    }

    private static void ServerWarRequestRoundTripsExactV769Frame()
    {
        var codec = LegacyFrameCodec.CreateDefault();
        var frame = new GuildWarRequest(0x1234, 0x2345).ToFrame(codec, 0x78563412, 7);
        var decoded = codec.Decode(frame);

        Check(decoded.IsChecksumValid && decoded.Header.Type == 0x0E0E && decoded.Header.Size == 20,
            "MSG_War must use the full 7.69 direction type and the 20-byte MSG_STANDARDPARM2 layout.");
        Check(decoded.Payload.Length == 8 &&
            BinaryPrimitives.ReadInt32LittleEndian(decoded.Payload.Span) == 0x1234 &&
            BinaryPrimitives.ReadInt32LittleEndian(decoded.Payload.Span[4..]) == 0x2345,
            "MSG_War guild IDs must be signed 32-bit values at payload offsets 0 and 4.");
        Check(GuildWarRequest.TryParse(decoded, out var request) && request == new GuildWarRequest(0x1234, 0x2345),
            "MSG_War did not round-trip through its parser.");
    }

    private static void RejectsWrongTypeSizeChecksumAndGuildBounds()
    {
        var codec = LegacyFrameCodec.CreateDefault();
        var validPayload = MakePayload(7, 8);

        Check(!GuildWarRequest.TryParse(codec.Decode(codec.Encode(0x000E, 0, 1, validPayload, 3)), out _),
            "The base opcode without direction flags must not be accepted as MSG_War.");
        Check(!GuildWarRequest.TryParse(codec.Decode(codec.Encode(0x0E0E, 0, 1, new byte[7], 3)), out _),
            "A truncated MSG_War frame was accepted.");
        Check(!GuildWarRequest.TryParse(codec.Decode(codec.Encode(0x0E0E, 0, 1, new byte[9], 3)), out _),
            "An oversized MSG_War frame was accepted.");

        var badChecksum = codec.Encode(0x0E0E, 0, 1, validPayload, 3);
        badChecksum[^1] ^= 0x01;
        Check(!GuildWarRequest.TryParse(codec.Decode(badChecksum), out _),
            "A MSG_War frame with an invalid checksum was accepted.");

        foreach (var (guild, enemy) in new[] { (0, 8), (7, 0), (-1, 8), (7, -1), (65536, 8), (7, 65536) })
            Check(!GuildWarRequest.TryParse(codec.Decode(codec.Encode(0x0E0E, 0, 1, MakePayload(guild, enemy), 3)), out _),
                $"Out-of-range guild IDs ({guild}, {enemy}) were accepted.");
    }

    private static void SendWarInfoUsesExactV769Signal()
    {
        var codec = LegacyFrameCodec.CreateDefault();
        var decoded = codec.Decode(new SendWarInfoConfirmation(0x1234, 2, 0x4567).ToFrame(codec, 99, 5));

        Check(decoded.IsChecksumValid && decoded.Header.Type == 0x03A8 && decoded.Header.Size == 24 && decoded.Header.Id == 30000,
            "MSG_SendWarInfo must use type 0x03A8, size 24, and ID 30000.");
        Check(decoded.Payload.Length == 12 &&
            BinaryPrimitives.ReadInt32LittleEndian(decoded.Payload.Span) == 0x1234 &&
            BinaryPrimitives.ReadInt32LittleEndian(decoded.Payload.Span[4..]) == 2 &&
            BinaryPrimitives.ReadInt32LittleEndian(decoded.Payload.Span[8..]) == 0x4567,
            "MSG_SendWarInfo must encode its three signed 32-bit parameters in order.");
    }

    private static byte[] MakePayload(int guild, int enemy)
    {
        var payload = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(payload, guild);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4), enemy);
        return payload;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

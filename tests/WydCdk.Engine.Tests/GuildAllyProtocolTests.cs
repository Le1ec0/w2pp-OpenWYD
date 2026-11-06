using System.Buffers.Binary;
using WydCdk.Protocol;

internal static class GuildAllyProtocolTests
{
    public static void Run()
    {
        RequestRoundTripsExactV769Frame();
        RejectsWrongTypeSizeChecksumAndGuildBounds();
    }

    private static void RequestRoundTripsExactV769Frame()
    {
        var codec = LegacyFrameCodec.CreateDefault();
        var frame = new GuildAllyRequest(0x1234, 0x2345).ToFrame(codec, 0x78563412, 7);
        var decoded = codec.Decode(frame);

        Check(decoded.IsChecksumValid && decoded.Header.Type == 0x1212 && decoded.Header.Size == 20,
            "MSG_GuildAlly must use the 7.69 direction type and 20-byte _MSG plus two-int layout.");
        Check(decoded.Payload.Length == 8 &&
            BinaryPrimitives.ReadInt32LittleEndian(decoded.Payload.Span) == 0x1234 &&
            BinaryPrimitives.ReadInt32LittleEndian(decoded.Payload.Span[4..]) == 0x2345,
            "MSG_GuildAlly must encode signed 32-bit Guild and Ally values at offsets 0 and 4.");
        Check(GuildAllyRequest.TryParse(decoded, out var request) && request == new GuildAllyRequest(0x1234, 0x2345),
            "MSG_GuildAlly did not round-trip through its parser.");
    }

    private static void RejectsWrongTypeSizeChecksumAndGuildBounds()
    {
        var codec = LegacyFrameCodec.CreateDefault();
        var payload = MakePayload(7, 8);

        Check(!GuildAllyRequest.TryParse(codec.Decode(codec.Encode(0x0012, 0, 1, payload, 3)), out _),
            "The base opcode without direction flags must not be accepted as MSG_GuildAlly.");
        Check(!GuildAllyRequest.TryParse(codec.Decode(codec.Encode(0x1212, 0, 1, new byte[7], 3)), out _),
            "A truncated MSG_GuildAlly frame was accepted.");
        Check(!GuildAllyRequest.TryParse(codec.Decode(codec.Encode(0x1212, 0, 1, new byte[9], 3)), out _),
            "An oversized MSG_GuildAlly frame was accepted.");

        var badChecksum = codec.Encode(0x1212, 0, 1, payload, 3);
        badChecksum[^1] ^= 0x01;
        Check(!GuildAllyRequest.TryParse(codec.Decode(badChecksum), out _),
            "A MSG_GuildAlly frame with an invalid checksum was accepted.");

        foreach (var (guild, ally) in new[] { (0, 8), (7, 0), (-1, 8), (7, -1), (65536, 8), (7, 65536) })
            Check(!GuildAllyRequest.TryParse(codec.Decode(codec.Encode(0x1212, 0, 1, MakePayload(guild, ally), 3)), out _),
                $"Out-of-range guild IDs ({guild}, {ally}) were accepted.");
    }

    private static byte[] MakePayload(int guild, int ally)
    {
        var payload = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(payload, guild);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4), ally);
        return payload;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

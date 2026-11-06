using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;

internal static class ExperienceMessagePanelTests
{
    public static void Run()
    {
        const string message = "Adquiriu 123 de gold";
        const int connectionId = 321;
        const uint yellow = 0xFFFFFF00;
        var codec = LegacyFrameCodec.CreateDefault();
        var bytes = new ExperienceMessagePanel(message, yellow)
            .ToFrame(codec, 0x12345678, 0x37, connectionId);
        var decoded = codec.Decode(bytes);

        Check(decoded.IsChecksumValid, "The experience panel frame checksum is invalid.");
        Check(decoded.Header.Type == 0x5000 && decoded.Header.Size == 112 && decoded.Header.Id == connectionId,
            "The experience panel frame header differs from the TMProject 7.69 struct.");
        Check(decoded.Payload.Length == 100, "The experience panel payload should contain char[96] plus Color32.");
        Check(Encoding.Latin1.GetString(decoded.Payload.Span[..message.Length]) == message,
            "The experience panel text bytes differ from the source message.");
        Check(decoded.Payload.Span[message.Length..96].IndexOfAnyExcept((byte)0) < 0,
            "The experience panel text tail was not zero-filled.");
        Check(BinaryPrimitives.ReadUInt32LittleEndian(decoded.Payload.Span[96..]) == yellow,
            "TNColor::Yellow was not encoded as 0xFFFFFF00 at offset 96.");

        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

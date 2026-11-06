using System.Text;
using WydCdk.Protocol;

internal static class MagicTrumpetTests
{
    public static void AutoDoublePacketMatches769Layout()
    {
        var codec = LegacyFrameCodec.CreateDefault();
        var frame = codec.Decode(new MagicTrumpetConfirmation("[Event:]> Double EXP started").ToFrame(codec, 0x12345678u, 17));
        Require(frame.IsChecksumValid, "MagicTrumpet frame checksum is invalid.");
        Require(frame.Header.Type == 0x0D1D, $"MagicTrumpet type was 0x{frame.Header.Type:X4}, expected 0x0D1D.");
        Require(frame.Header.Size == 108 && frame.Header.Id == 0, "MagicTrumpet header does not match MSG_ChatColor size and ID.");
        Require(frame.Payload.Length == 96, "MagicTrumpet payload is not 92 message bytes plus a 32-bit color.");
        var expectedText = Encoding.Latin1.GetBytes("[Event:]> Double EXP started");
        Require(frame.Payload.Span[..expectedText.Length].SequenceEqual(expectedText), "MagicTrumpet message bytes differ from the event banner.");
        Require(frame.Payload.Span[expectedText.Length] == 0, "MagicTrumpet message is not NUL-terminated.");
        Require(frame.Payload.Span[92..96].SequenceEqual(new byte[] { 0x00, 0xCD, 0x00, 0xFF }), "MagicTrumpet color differs from TNColor::Speak.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

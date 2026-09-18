using System.Buffers.Binary;
using System.Text;

namespace WydCdk.Protocol;

/// <summary>Parsed legacy <c>MSG_DeleteCharacter</c> (type 0x0211, 44 bytes on the wire).</summary>
public sealed record DeleteCharacterRequest(int Slot, string CharacterName, string AccountPassword)
{
    public const ushort MessageType = 0x0211; // 17 | FLAG_CLIENT2GAME
    public const int PacketSize = 44;
    private const int PayloadSize = PacketSize - PacketHeader.SizeInBytes;

    public static bool TryParse(DecodedFrame frame, out DeleteCharacterRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != PayloadSize) return false;

        var payload = frame.Payload.Span;
        var name = ReadCString(payload.Slice(4, 16));
        if (string.IsNullOrWhiteSpace(name)) return false;
        request = new DeleteCharacterRequest(
            BinaryPrimitives.ReadInt32LittleEndian(payload),
            name,
            ReadCString(payload.Slice(20, 12)));
        return true;
    }

    private static string ReadCString(ReadOnlySpan<byte> source)
    {
        var terminator = source.IndexOf((byte)0);
        return Encoding.ASCII.GetString(terminator < 0 ? source : source[..terminator]);
    }
}

using System.Buffers.Binary;
using System.Text;

namespace WydCdk.Protocol;

/// <summary>
/// Parsed legacy <c>MSG_CreateCharacter</c> (type 0x020F, 36 bytes on the wire).
/// Validation of slot availability, class range, and character-name policy belongs to
/// the character-creation service; this type preserves the packet contract only.
/// </summary>
public sealed record CreateCharacterRequest(int Slot, string CharacterName, int CharacterClass)
{
    public const ushort MessageType = 0x020F;
    public const int PacketSize = 36;
    private const int PayloadSize = PacketSize - PacketHeader.SizeInBytes;
    private const int SlotOffset = 0;
    private const int NameOffset = SlotOffset + sizeof(int);
    private const int NameLength = 16;
    private const int ClassOffset = NameOffset + NameLength;

    public static bool TryParse(DecodedFrame frame, out CreateCharacterRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != PayloadSize) return false;

        var payload = frame.Payload.Span;
        var characterName = ReadCString(payload.Slice(NameOffset, NameLength));
        if (string.IsNullOrWhiteSpace(characterName)) return false;

        request = new CreateCharacterRequest(
            BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(SlotOffset, sizeof(int))),
            characterName,
            BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(ClassOffset, sizeof(int))));
        return true;
    }

    private static string ReadCString(ReadOnlySpan<byte> source)
    {
        var terminator = source.IndexOf((byte)0);
        return Encoding.ASCII.GetString(terminator < 0 ? source : source[..terminator]);
    }
}

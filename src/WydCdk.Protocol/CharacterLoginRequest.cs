using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Parsed legacy <c>MSG_CharacterLogin</c> (type 0x0213). The original server struct is 20 bytes, but the
/// retail 7.60 client observed in the local E2E capture sends a 36-byte frame: the first 8 payload bytes are
/// still <see cref="Slot"/> and <see cref="Force"/>, followed by 16 zero bytes whose meaning is not established.
/// The tail is accepted for wire compatibility and deliberately ignored. <see cref="Force"/> is decoded for
/// wire completeness but unused by this port: the reference client sets it to request kicking an already
/// logged-in session for the same character (see <c>_MSG_DBAlreadyPlaying</c>/<c>_MSG_DBStillPlaying</c> in
/// Basedef.h), and this port does not yet track concurrent logins across connections.
/// </summary>
public sealed record CharacterLoginRequest(int Slot, int Force)
{
    public const ushort MessageType = 0x0213; // 19 | FLAG_CLIENT2GAME
    public const int PacketSize = 20;
    public const int Retail760PacketSize = 36;
    private const int PayloadSize = PacketSize - PacketHeader.SizeInBytes;
    private const int SlotOffset = 0;
    private const int ForceOffset = SlotOffset + sizeof(int);

    public static bool TryParse(DecodedFrame frame, out CharacterLoginRequest? request)
    {
        request = null;
        var acceptedSize = frame.Header.Size is PacketSize or Retail760PacketSize;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || !acceptedSize || frame.Payload.Length < PayloadSize) return false;

        var payload = frame.Payload.Span;
        request = new CharacterLoginRequest(
            BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(SlotOffset, sizeof(int))),
            BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(ForceOffset, sizeof(int))));
        return true;
    }
}

using System.Buffers.Binary;
using System.Text;

namespace WydCdk.Protocol;

/// <summary>
/// Parsed legacy <c>MSG_AccountSecure</c> (type 0x0FDE, 32 bytes on the wire): the six-digit numeric PIN
/// ("second password") the client sends on connect, and whenever the player sets or changes it. <c>Unknown[10]</c>
/// (Basedef.h:1594) is decoded away unread - nothing in the reference handler (CFileDB.cpp:1382-1446) touches it.
/// <see cref="NumericToken"/> keeps all six raw digit characters (no null-terminator trimming): the original
/// stores/compares it with <c>strncpy</c>/<c>strncmp</c> over exactly six bytes, not as a C string.
/// </summary>
public sealed record AccountSecureRequest(string NumericToken, bool ChangeNumeric)
{
    public const ushort MessageType = 0x0FDE; // 222 | FLAG_DB2GAME | FLAG_GAME2DB | FLAG_CLIENT2GAME | FLAG_GAME2CLIENT
    public const int PacketSize = 32;
    private const int PayloadSize = PacketSize - PacketHeader.SizeInBytes;
    private const int NumericTokenOffset = 0;
    private const int NumericTokenLength = 6;
    private const int ChangeNumericOffset = 16;

    public static bool TryParse(DecodedFrame frame, out AccountSecureRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != PayloadSize) return false;

        var payload = frame.Payload.Span;
        var token = Encoding.ASCII.GetString(payload.Slice(NumericTokenOffset, NumericTokenLength));
        var change = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(ChangeNumericOffset, sizeof(int))) != 0;
        request = new AccountSecureRequest(token, change);
        return true;
    }
}

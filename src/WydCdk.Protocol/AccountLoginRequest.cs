using System.Buffers.Binary;
using System.Text;

namespace WydCdk.Protocol;

/// <summary>
/// Parsed legacy <c>MSG_AccountLogin</c> (type 0x020D, 116 bytes on the wire).
/// Sensitive values are retained only to preserve the legacy authentication contract;
/// callers must not log <see cref="AccountPassword"/>.
/// </summary>
public sealed record AccountLoginRequest(
    string AccountName,
    string AccountPassword,
    byte[] ReconnectToken,
    int ClientVersion,
    int DbNeedSave,
    IReadOnlyList<uint> AdapterName)
{
    public const ushort MessageType = 0x020D;
    public const int PacketSize = 116;
    private const int PayloadSize = PacketSize - PacketHeader.SizeInBytes;
    private const int PasswordOffset = 0;
    private const int PasswordLength = 12;
    private const int AccountNameOffset = PasswordOffset + PasswordLength;
    private const int AccountNameLength = 16;
    private const int ReconnectTokenOffset = AccountNameOffset + AccountNameLength;
    private const int ReconnectTokenLength = 52;
    private const int ClientVersionOffset = ReconnectTokenOffset + ReconnectTokenLength;
    private const int DbNeedSaveOffset = ClientVersionOffset + sizeof(int);
    private const int AdapterNameOffset = DbNeedSaveOffset + sizeof(int);

    public static bool TryParse(DecodedFrame frame, out AccountLoginRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != PayloadSize) return false;

        var payload = frame.Payload.Span;
        var accountName = ReadCString(payload.Slice(AccountNameOffset, AccountNameLength));
        var accountPassword = ReadCString(payload.Slice(PasswordOffset, PasswordLength));
        // The legacy server defers password validation to its account store, including
        // the historical possibility of an empty password. Keep that wire behavior here.
        if (string.IsNullOrWhiteSpace(accountName)) return false;

        request = new AccountLoginRequest(
            accountName.ToUpperInvariant(),
            accountPassword,
            payload.Slice(ReconnectTokenOffset, ReconnectTokenLength).ToArray(),
            BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(ClientVersionOffset, sizeof(int))),
            BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(DbNeedSaveOffset, sizeof(int))),
            [
                BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(AdapterNameOffset, sizeof(uint))),
                BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(AdapterNameOffset + sizeof(uint), sizeof(uint))),
                BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(AdapterNameOffset + (2 * sizeof(uint)), sizeof(uint))),
                BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(AdapterNameOffset + (3 * sizeof(uint)), sizeof(uint))),
            ]);
        return true;
    }

    private static string ReadCString(ReadOnlySpan<byte> source)
    {
        var terminator = source.IndexOf((byte)0);
        return Encoding.ASCII.GetString(terminator < 0 ? source : source[..terminator]);
    }
}

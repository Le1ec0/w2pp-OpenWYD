using System.Buffers.Binary;
using System.Text;

namespace WydCdk.Protocol;

/// <summary>
/// Client 7.69 wire DTO for MSG_CNFAccountLogin. The payload includes the x86 alignment
/// after SecretCode and the structure's trailing alignment bytes; it is intentionally
/// separate from the W2PP-shaped AccountLoginConfirmation.
/// </summary>
public sealed class AccountLoginConfirmationV769
{
    public const ushort MessageType = 0x010A;
    public const ushort SceneId = 30_002;
    public const int CargoCount = 120;
    public const int SecretCodeLength = 16;
    public const int PayloadSize = 1_916;
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public const int SecretCodeOffset = 0;
    public const int SelectionOffset = 20;
    public const int CargoOffset = SelectionOffset + CharacterSelectionV769.SizeInBytes;
    public const int CoinOffset = CargoOffset + (CargoCount * LegacyItem.SizeInBytes);
    public const int AccountNameOffset = CoinOffset + sizeof(int);
    public const int Ssn1Offset = AccountNameOffset + 16;
    public const int Ssn2Offset = Ssn1Offset + sizeof(int);

    private readonly byte[] secretCode;
    private readonly LegacyItem[] cargo;

    public ReadOnlyMemory<byte> SecretCode => secretCode;
    public CharacterSelectionV769 Selection { get; }
    public IReadOnlyList<LegacyItem> Cargo => cargo;
    public int Coin { get; }
    public string AccountName { get; }
    public int Ssn1 { get; }
    public int Ssn2 { get; }

    public AccountLoginConfirmationV769(
        ReadOnlyMemory<byte> secretCode,
        CharacterSelectionV769 selection,
        IReadOnlyList<LegacyItem> cargo,
        int coin,
        string accountName,
        int ssn1,
        int ssn2)
    {
        if (secretCode.Length != SecretCodeLength)
            throw new ArgumentException("Client 7.69 SecretCode requires exactly sixteen bytes.", nameof(secretCode));
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(cargo);
        ArgumentNullException.ThrowIfNull(accountName);
        if (cargo.Count != CargoCount)
            throw new ArgumentException("Client 7.69 account login requires exactly 120 cargo items.", nameof(cargo));
        if (accountName.Length > 15 || accountName.Any(static character => character > 0x7F || character == '\0'))
            throw new ArgumentOutOfRangeException(nameof(accountName), "Account name must fit in fifteen ASCII bytes plus a terminator.");

        this.secretCode = secretCode.ToArray();
        Selection = selection;
        this.cargo = cargo.ToArray();
        Coin = coin;
        AccountName = accountName;
        Ssn1 = ssn1;
        Ssn2 = ssn2;
    }

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        secretCode.CopyTo(payload, SecretCodeOffset);
        // payload bytes 16..19 are the 4-byte x86 alignment before STRUCT_SELCHAR.
        Selection.ToBytes().CopyTo(payload, SelectionOffset);
        for (var index = 0; index < CargoCount; index++)
            cargo[index].Write(payload.AsSpan(CargoOffset + (index * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));

        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(CoinOffset), Coin);
        Encoding.ASCII.GetBytes(AccountName).CopyTo(payload.AsSpan(AccountNameOffset, 16));
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(Ssn1Offset), Ssn1);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(Ssn2Offset), Ssn2);
        // payload bytes 1912..1915 are the x86 structure's trailing 8-byte alignment padding.
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, SceneId, clientTick, ToPayload(), keywordIndex);
    }
}

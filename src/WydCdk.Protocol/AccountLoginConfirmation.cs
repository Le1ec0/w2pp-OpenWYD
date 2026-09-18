using System.Buffers.Binary;
using System.Text;

namespace WydCdk.Protocol;

/// <summary>Legacy STRUCT_QUEST as serialized by the 32-bit server build (52 bytes).</summary>
public readonly record struct LegacyDailyQuest(
    short IndexQuest, short Level, short Mob1Id, short Mob1Required, short Mob2Id, short Mob2Required, short Mob3Id, short Mob3Required,
    int ExperienceReward, int GoldReward, LegacyItem Reward1, LegacyItem Reward2, int LastTimeQuest, short Mob1Count, short Mob2Count, short Mob3Count)
{
    public const int SizeInBytes = 52;

    public void Write(Span<byte> destination)
    {
        if (destination.Length < SizeInBytes) throw new ArgumentException("Legacy daily quest requires 52 bytes.", nameof(destination));
        BinaryPrimitives.WriteInt16LittleEndian(destination, IndexQuest);
        BinaryPrimitives.WriteInt16LittleEndian(destination[2..], Level);
        BinaryPrimitives.WriteInt16LittleEndian(destination[4..], Mob1Id);
        BinaryPrimitives.WriteInt16LittleEndian(destination[6..], Mob1Required);
        BinaryPrimitives.WriteInt16LittleEndian(destination[8..], Mob2Id);
        BinaryPrimitives.WriteInt16LittleEndian(destination[10..], Mob2Required);
        BinaryPrimitives.WriteInt16LittleEndian(destination[12..], Mob3Id);
        BinaryPrimitives.WriteInt16LittleEndian(destination[14..], Mob3Required);
        BinaryPrimitives.WriteInt32LittleEndian(destination[16..], ExperienceReward);
        BinaryPrimitives.WriteInt32LittleEndian(destination[20..], GoldReward);
        Reward1.Write(destination[24..]);
        Reward2.Write(destination[32..]);
        BinaryPrimitives.WriteInt32LittleEndian(destination[40..], LastTimeQuest); // x86 time_t
        BinaryPrimitives.WriteInt16LittleEndian(destination[44..], Mob1Count);
        BinaryPrimitives.WriteInt16LittleEndian(destination[46..], Mob2Count);
        BinaryPrimitives.WriteInt16LittleEndian(destination[48..], Mob3Count);
        // bytes 50-51 are the C++ structure's trailing alignment padding and remain zero.
    }
}

/// <summary>
/// Encoder for the C++ <c>MSG_DBCNFAccountLogin</c> after TMSrv changes its type to
/// <c>_MSG_CNFAccountLogin</c>. The current legacy x86 wire size is 2,000 bytes including CPSock header.
/// </summary>
public sealed class AccountLoginConfirmation
{
    public const ushort MessageType = 0x010A; // 10 | FLAG_GAME2CLIENT
    public const ushort SceneId = 30002; // ESCENE_FIELD + 2
    public const int CargoCount = 128;
    public const int PayloadSize = 1988;
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;
    private const int HashKeyTableOffset = 0;
    private const int Unknown28Offset = 16;
    private const int SelectionOffset = 20;
    private const int CargoOffset = SelectionOffset + LegacyCharacterSelection.SizeInBytes;
    private const int CoinOffset = CargoOffset + (CargoCount * LegacyItem.SizeInBytes);
    private const int AccountNameOffset = CoinOffset + sizeof(int);
    private const int KeysOffset = AccountNameOffset + 16;
    private const int DailyQuestOffset = KeysOffset + 12;
    private const int BlockPasswordOffset = DailyQuestOffset + LegacyDailyQuest.SizeInBytes;
    private const int IsBlockedOffset = BlockPasswordOffset + 16;

    public byte[] HashKeyTable { get; }
    public int Unknown28 { get; }
    public LegacyCharacterSelection Selection { get; }
    public IReadOnlyList<LegacyItem> Cargo { get; }
    public int Coin { get; }
    public string AccountName { get; }
    public byte[] Keys { get; }
    public LegacyDailyQuest DailyQuest { get; }
    public string BlockPassword { get; }
    public bool IsBlocked { get; }

    public AccountLoginConfirmation(byte[] hashKeyTable, int unknown28, LegacyCharacterSelection selection, IReadOnlyList<LegacyItem> cargo, int coin, string accountName, byte[] keys, LegacyDailyQuest dailyQuest, string blockPassword, bool isBlocked)
    {
        ArgumentNullException.ThrowIfNull(hashKeyTable);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(cargo);
        ArgumentNullException.ThrowIfNull(keys);
        if (hashKeyTable.Length != 16) throw new ArgumentException("Hash key table must contain sixteen bytes.", nameof(hashKeyTable));
        if (cargo.Count != CargoCount) throw new ArgumentException("Legacy cargo requires exactly 128 items.", nameof(cargo));
        if (keys.Length != 12) throw new ArgumentException("Legacy keys must contain twelve bytes.", nameof(keys));
        EnsureCStringFits(accountName, 16, nameof(accountName));
        EnsureCStringFits(blockPassword, 16, nameof(blockPassword));

        HashKeyTable = hashKeyTable.ToArray();
        Unknown28 = unknown28;
        Selection = selection;
        Cargo = cargo.ToArray();
        Coin = coin;
        AccountName = accountName;
        Keys = keys.ToArray();
        DailyQuest = dailyQuest;
        BlockPassword = blockPassword;
        IsBlocked = isBlocked;
    }

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        HashKeyTable.CopyTo(payload, HashKeyTableOffset);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(Unknown28Offset), Unknown28);
        Selection.ToBytes().CopyTo(payload, SelectionOffset);
        for (var index = 0; index < CargoCount; index++)
            Cargo[index].Write(payload.AsSpan(CargoOffset + (index * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(CoinOffset), Coin);
        WriteCString(payload.AsSpan(AccountNameOffset, 16), AccountName);
        Keys.CopyTo(payload, KeysOffset);
        DailyQuest.Write(payload.AsSpan(DailyQuestOffset, LegacyDailyQuest.SizeInBytes));
        WriteCString(payload.AsSpan(BlockPasswordOffset, 16), BlockPassword);
        payload[IsBlockedOffset] = IsBlocked ? (byte)1 : (byte)0;
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex) => codec.Encode(MessageType, SceneId, clientTick, ToPayload(), keywordIndex);

    /// <summary>Safe placeholder used until character, cargo, quest, and block state are read from the legacy account file.</summary>
    public static AccountLoginConfirmation CreateEmpty(string accountName) => new(
        new byte[16],
        0,
        LegacyCharacterSelection.CreateEmpty(),
        new LegacyItem[CargoCount],
        0,
        accountName,
        new byte[12],
        default,
        string.Empty,
        false);

    private static void EnsureCStringFits(string value, int length, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (Encoding.ASCII.GetByteCount(value) >= length) throw new ArgumentOutOfRangeException(parameterName, $"Value must fit in {length - 1} ASCII bytes plus a terminator.");
    }

    private static void WriteCString(Span<byte> destination, string value) => Encoding.ASCII.GetBytes(value).CopyTo(destination);
}

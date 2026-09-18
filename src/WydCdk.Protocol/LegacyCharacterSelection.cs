using System.Buffers.Binary;
using System.Text;

namespace WydCdk.Protocol;

/// <summary>Exact eight-byte representation of legacy STRUCT_ITEM.</summary>
public readonly record struct LegacyItem(short Index, byte Effect1, byte Value1, byte Effect2, byte Value2, byte Effect3, byte Value3)
{
    public const int SizeInBytes = 8;

    public void Write(Span<byte> destination)
    {
        if (destination.Length < SizeInBytes) throw new ArgumentException("Legacy item requires eight bytes.", nameof(destination));
        BinaryPrimitives.WriteInt16LittleEndian(destination, Index);
        destination[2] = Effect1;
        destination[3] = Value1;
        destination[4] = Effect2;
        destination[5] = Value2;
        destination[6] = Effect3;
        destination[7] = Value3;
    }

    public static LegacyItem Read(ReadOnlySpan<byte> source)
    {
        if (source.Length < SizeInBytes) throw new ArgumentException("Legacy item requires eight bytes.", nameof(source));
        return new LegacyItem(BinaryPrimitives.ReadInt16LittleEndian(source), source[2], source[3], source[4], source[5], source[6], source[7]);
    }
}

/// <summary>Exact 48-byte representation of legacy STRUCT_SCORE.</summary>
public readonly record struct LegacyScore(
    int Level, int Ac, int Damage,
    byte Merchant, byte AttackRun, byte Direction, byte ChaosRate,
    int MaxHp, int MaxMp, int Hp, int Mp,
    short Strength, short Intelligence, short Dexterity, short Constitution,
    short Special1, short Special2, short Special3, short Special4)
{
    public const int SizeInBytes = 48;

    public void Write(Span<byte> destination)
    {
        if (destination.Length < SizeInBytes) throw new ArgumentException("Legacy score requires 48 bytes.", nameof(destination));
        BinaryPrimitives.WriteInt32LittleEndian(destination, Level);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], Ac);
        BinaryPrimitives.WriteInt32LittleEndian(destination[8..], Damage);
        destination[12] = Merchant;
        destination[13] = AttackRun;
        destination[14] = Direction;
        destination[15] = ChaosRate;
        BinaryPrimitives.WriteInt32LittleEndian(destination[16..], MaxHp);
        BinaryPrimitives.WriteInt32LittleEndian(destination[20..], MaxMp);
        BinaryPrimitives.WriteInt32LittleEndian(destination[24..], Hp);
        BinaryPrimitives.WriteInt32LittleEndian(destination[28..], Mp);
        BinaryPrimitives.WriteInt16LittleEndian(destination[32..], Strength);
        BinaryPrimitives.WriteInt16LittleEndian(destination[34..], Intelligence);
        BinaryPrimitives.WriteInt16LittleEndian(destination[36..], Dexterity);
        BinaryPrimitives.WriteInt16LittleEndian(destination[38..], Constitution);
        BinaryPrimitives.WriteInt16LittleEndian(destination[40..], Special1);
        BinaryPrimitives.WriteInt16LittleEndian(destination[42..], Special2);
        BinaryPrimitives.WriteInt16LittleEndian(destination[44..], Special3);
        BinaryPrimitives.WriteInt16LittleEndian(destination[46..], Special4);
    }

    public static LegacyScore Read(ReadOnlySpan<byte> source)
    {
        if (source.Length < SizeInBytes) throw new ArgumentException("Legacy score requires 48 bytes.", nameof(source));
        return new LegacyScore(
            BinaryPrimitives.ReadInt32LittleEndian(source), BinaryPrimitives.ReadInt32LittleEndian(source[4..]), BinaryPrimitives.ReadInt32LittleEndian(source[8..]),
            source[12], source[13], source[14], source[15],
            BinaryPrimitives.ReadInt32LittleEndian(source[16..]), BinaryPrimitives.ReadInt32LittleEndian(source[20..]), BinaryPrimitives.ReadInt32LittleEndian(source[24..]), BinaryPrimitives.ReadInt32LittleEndian(source[28..]),
            BinaryPrimitives.ReadInt16LittleEndian(source[32..]), BinaryPrimitives.ReadInt16LittleEndian(source[34..]), BinaryPrimitives.ReadInt16LittleEndian(source[36..]), BinaryPrimitives.ReadInt16LittleEndian(source[38..]),
            BinaryPrimitives.ReadInt16LittleEndian(source[40..]), BinaryPrimitives.ReadInt16LittleEndian(source[42..]), BinaryPrimitives.ReadInt16LittleEndian(source[44..]), BinaryPrimitives.ReadInt16LittleEndian(source[46..]));
    }
}

public sealed record LegacyCharacterSlot(short SavedPositionX, short SavedPositionY, string Name, LegacyScore Score, IReadOnlyList<LegacyItem> Equipment, ushort Guild, int Coin, long Experience);

/// <summary>Exact 840-byte representation of legacy STRUCT_SELCHAR used by account-login confirmation.</summary>
public sealed class LegacyCharacterSelection
{
    public const int CharacterCount = 4;
    public const int EquipmentCount = 16;
    public const int NameLength = 16;
    public const int SizeInBytes = 840;
    private const int SavedPositionXOffset = 0;
    private const int SavedPositionYOffset = 8;
    private const int NameOffset = 16;
    private const int ScoreOffset = 80;
    private const int EquipmentOffset = 272;
    private const int GuildOffset = 784;
    private const int CoinOffset = 792;
    private const int ExperienceOffset = 808;

    public IReadOnlyList<LegacyCharacterSlot> Slots { get; }

    public LegacyCharacterSelection(IReadOnlyList<LegacyCharacterSlot> slots)
    {
        if (slots.Count != CharacterCount) throw new ArgumentException("Legacy selection requires exactly four characters.", nameof(slots));
        if (slots.Any(static slot => slot.Equipment.Count != EquipmentCount)) throw new ArgumentException("Each legacy character requires exactly sixteen equipment entries.", nameof(slots));
        Slots = slots.ToArray();
    }

    public byte[] ToBytes()
    {
        var bytes = new byte[SizeInBytes];
        for (var character = 0; character < CharacterCount; character++)
        {
            var slot = Slots[character];
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(SavedPositionXOffset + (character * sizeof(short))), slot.SavedPositionX);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(SavedPositionYOffset + (character * sizeof(short))), slot.SavedPositionY);
            WriteCString(bytes.AsSpan(NameOffset + (character * NameLength), NameLength), slot.Name);
            slot.Score.Write(bytes.AsSpan(ScoreOffset + (character * LegacyScore.SizeInBytes), LegacyScore.SizeInBytes));
            for (var equipment = 0; equipment < EquipmentCount; equipment++)
                slot.Equipment[equipment].Write(bytes.AsSpan(EquipmentOffset + (((character * EquipmentCount) + equipment) * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(GuildOffset + (character * sizeof(ushort))), slot.Guild);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(CoinOffset + (character * sizeof(int))), slot.Coin);
            BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(ExperienceOffset + (character * sizeof(long))), slot.Experience);
        }
        return bytes;
    }

    public static LegacyCharacterSelection CreateEmpty() => new(Enumerable.Range(0, CharacterCount)
        .Select(static _ => new LegacyCharacterSlot(0, 0, string.Empty, default, new LegacyItem[EquipmentCount], 0, 0, 0))
        .ToArray());

    private static void WriteCString(Span<byte> destination, string value)
    {
        var encoded = Encoding.ASCII.GetBytes(value);
        if (encoded.Length >= destination.Length) throw new ArgumentOutOfRangeException(nameof(value), "Legacy name must fit in fifteen ASCII bytes plus a terminator.");
        encoded.CopyTo(destination);
    }
}

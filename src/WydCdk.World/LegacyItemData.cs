using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;

namespace WydCdk.World;

public static class LegacyItemEffect
{
    public const int Level = 1;
    public const int Damage = 2;
    public const int Ac = 3;
    public const int Strength = 7;
    public const int Intelligence = 8;
    public const int Dexterity = 9;
    public const int Constitution = 10;
    public const int Parry = 40;
    public const int Position = 17;
    public const int Magic = 60;
    public const int Sanctuary = 43;
    public const int Damage2 = 73;
    public const int Hp = 4;
    public const int Mp = 5;
    public const int Volatile = 38;
    public const int ItemLevel = 87;
    public const int MobType = 112;
    public const int NoSanctuary = 126;
    public const int PvpAttack = 56;
    public const int PvpDefense = 57;
    public const int Donate = 91;
    public const int AttackSpeed = 26;
    public const int RunSpeed = 29;
    public const int Critical = 42;
    public const int SaveMana = 44;
    public const int RegenHp = 47;
    public const int RegenMp = 48;
    public const int MagicAdd = 68;
    public const int Critical2 = 71;
    public const int DamageAdd = 67;
    public const int Range = 27;
    public const int AcAdd = 53;
    public const int Special1 = 11;
    public const int Special2 = 12;
    public const int Special3 = 13;
    public const int Special4 = 14;
    public const int SpecialAll = 74;
    public const int WDay = 106;
    public const int Year = 109;
    public const int WMonth = 110;
    public const int Ground = 34;
    public const int KeyId = 39;
    public const int Quest = 58;
    public const int Class = 18;
    public const int WeaponType = 21;
    public const int RequiredStrength = 22;
    public const int RequiredIntelligence = 23;
    public const int RequiredDexterity = 24;
    public const int RequiredConstitution = 25;
    public const int HwordGuild = 56;
    public const int LwordGuild = 57;
    public const int NoTrade = 127;
}

public sealed record LegacyItemStaticEffect(short Effect, short Value);

/// <summary>Decoded item definition from the community 7.69 ItemList.bin contract.</summary>
public sealed record LegacyItemDefinition(
    short Index,
    string Name,
    short RequiredLevel,
    short Unique,
    int Position,
    short Grade,
    IReadOnlyList<LegacyItemStaticEffect> StaticEffects,
    short Extra = 0,
    short RequiredStrength = 0,
    short RequiredIntelligence = 0,
    short RequiredDexterity = 0,
    short RequiredConstitution = 0)
{
    public short MeshIndex { get; init; }
    public short TextureIndex { get; init; }
    public short VisualEffectIndex { get; init; }
    public int Price { get; init; }
    public short Unknown1 { get; init; }
    public int Unknown2 { get; init; }
    public short ItemType { get; init; }
    public short ItemData { get; init; }
    public short Unknown3 { get; init; }
    public short Unknown4 { get; init; }
    public ushort UnknownNewValue0 { get; init; }
    public ushort UnknownNewValue1 { get; init; }
    public ushort UnknownNewValue2 { get; init; }
    public ushort UnknownNewValue3 { get; init; }
}

/// <summary>
/// Reader and the small BASE_GetItemAbility subset required by combat and equipment rules.
/// The target 7.69 client/converter layout stores 6500 164-byte records, XORed
/// with 0x5A. The distributed file has a 4-byte trailer which those readers
/// leave unread; this reader accepts either the record body alone or that trailer.
/// </summary>
public sealed class LegacyItemDataTable
{
    public const int MaxItemIndex = 6500;
    public const int RecordSizeInBytes = 164;
    public const int OpaqueTrailerSizeInBytes = sizeof(int);
    public const int BodySizeInBytes = MaxItemIndex * RecordSizeInBytes;
    public const int FileSizeInBytes = BodySizeInBytes + OpaqueTrailerSizeInBytes;
    private const byte XorKey = 0x5A;
    private const int StaticEffectCount = 12;
    private const int NameOffset = 0;
    private const int MeshIndexOffset = 64;
    private const int TextureIndexOffset = 66;
    private const int VisualEffectIndexOffset = 68;
    private const int RequiredLevelOffset = 70;
    private const int RequiredStrengthOffset = 72;
    private const int RequiredIntelligenceOffset = 74;
    private const int RequiredDexterityOffset = 76;
    private const int RequiredConstitutionOffset = 78;
    private const int StaticEffectsOffset = 80;
    private const int PriceOffset = 128;
    private const int UniqueOffset = 132;
    private const int Unknown1Offset = 134;
    private const int PositionOffset = 136;
    private const int ExtraOffset = 140;
    private const int GradeOffset = 142;
    private const int Unknown2Offset = 144;
    private const int ItemTypeOffset = 148;
    private const int ItemDataOffset = 150;
    private const int Unknown3Offset = 152;
    private const int Unknown4Offset = 154;
    private const int UnknownNewValue0Offset = 156;
    private const int UnknownNewValue1Offset = 158;
    private const int UnknownNewValue2Offset = 160;
    private const int UnknownNewValue3Offset = 162;

    private readonly LegacyItemDefinition?[] definitions;

    private LegacyItemDataTable(LegacyItemDefinition?[] definitions) => this.definitions = definitions;

    public int Count => definitions.Count(static definition => definition is not null);

    public LegacyItemDefinition? this[int itemIndex] =>
        itemIndex is >= 0 and < MaxItemIndex ? definitions[itemIndex] : null;

    public static LegacyItemDataTable Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Load(File.ReadAllBytes(path));
    }

    public static LegacyItemDataTable Load(ReadOnlySpan<byte> encodedFile)
    {
        if (encodedFile.Length != BodySizeInBytes && encodedFile.Length != FileSizeInBytes)
            throw new InvalidDataException($"7.69 ItemList.bin must contain {BodySizeInBytes} record bytes, optionally followed by {OpaqueTrailerSizeInBytes} opaque bytes.");

        var definitions = new LegacyItemDefinition?[MaxItemIndex];
        for (var index = 0; index < MaxItemIndex; index++)
        {
            var record = encodedFile.Slice(index * RecordSizeInBytes, RecordSizeInBytes);
            var decoded = new byte[RecordSizeInBytes];
            for (var offset = 0; offset < decoded.Length; offset++)
                decoded[offset] = (byte)(record[offset] ^ XorKey);

            var effects = new LegacyItemStaticEffect[StaticEffectCount];
            for (var effect = 0; effect < effects.Length; effect++)
            {
                var effectOffset = StaticEffectsOffset + (effect * sizeof(short) * 2);
                effects[effect] = new LegacyItemStaticEffect(
                    BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(effectOffset)),
                    BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(effectOffset + sizeof(short))));
            }

            definitions[index] = new LegacyItemDefinition(
                (short)index,
                ReadCString(decoded.AsSpan(NameOffset, 64)),
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(RequiredLevelOffset)),
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(UniqueOffset)),
                BinaryPrimitives.ReadInt32LittleEndian(decoded.AsSpan(PositionOffset)),
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(GradeOffset)),
                effects,
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(ExtraOffset)),
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(RequiredStrengthOffset)),
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(RequiredIntelligenceOffset)),
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(RequiredDexterityOffset)),
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(RequiredConstitutionOffset)))
            {
                MeshIndex = BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(MeshIndexOffset)),
                TextureIndex = BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(TextureIndexOffset)),
                VisualEffectIndex = BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(VisualEffectIndexOffset)),
                Price = BinaryPrimitives.ReadInt32LittleEndian(decoded.AsSpan(PriceOffset)),
                Unknown1 = BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(Unknown1Offset)),
                Unknown2 = BinaryPrimitives.ReadInt32LittleEndian(decoded.AsSpan(Unknown2Offset)),
                ItemType = BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(ItemTypeOffset)),
                ItemData = BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(ItemDataOffset)),
                Unknown3 = BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(Unknown3Offset)),
                Unknown4 = BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(Unknown4Offset)),
                UnknownNewValue0 = BinaryPrimitives.ReadUInt16LittleEndian(decoded.AsSpan(UnknownNewValue0Offset)),
                UnknownNewValue1 = BinaryPrimitives.ReadUInt16LittleEndian(decoded.AsSpan(UnknownNewValue1Offset)),
                UnknownNewValue2 = BinaryPrimitives.ReadUInt16LittleEndian(decoded.AsSpan(UnknownNewValue2Offset)),
                UnknownNewValue3 = BinaryPrimitives.ReadUInt16LittleEndian(decoded.AsSpan(UnknownNewValue3Offset)),
            };
        }

        return new LegacyItemDataTable(definitions);
    }

    /// <summary>Subset of BASE_GetItemAbility used by combat and weapon composition.</summary>
    public int GetItemAbility(LegacyItem item, int type)
    {
        var definition = this[item.Index];
        if (definition is null) return 0;

        if ((type is LegacyItemEffect.DamageAdd or LegacyItemEffect.MagicAdd) && (definition.Unique is < 41 or > 50))
            return 0;

        var resolvedType = type;
        if (type == LegacyItemEffect.Critical &&
            (item.Effect2 == LegacyItemEffect.Critical2 || item.Effect3 == LegacyItemEffect.Critical2))
        {
            resolvedType = LegacyItemEffect.Critical2;
        }

        if (type == LegacyItemEffect.Damage && definition.Position == 32 && HasDynamicEffect(item, LegacyItemEffect.Damage2))
            resolvedType = LegacyItemEffect.Damage2;

        var value = resolvedType switch
        {
            LegacyItemEffect.Position => definition.Position,
            LegacyItemEffect.Level when item.Index is >= 2330 and < 2360 => item.Effect2 - 1,
            LegacyItemEffect.Level => definition.RequiredLevel,
            LegacyItemEffect.RequiredStrength => definition.RequiredStrength,
            LegacyItemEffect.RequiredIntelligence => definition.RequiredIntelligence,
            LegacyItemEffect.RequiredDexterity => definition.RequiredDexterity,
            LegacyItemEffect.RequiredConstitution => definition.RequiredConstitution,
            LegacyItemEffect.ItemLevel when item.Index is >= 2330 and < 2360 => item.Effect2 - 1,
            LegacyItemEffect.ItemLevel => definition.RequiredLevel,
            _ => definition.StaticEffects.Where(effect => effect.Effect == resolvedType).Sum(effect => NormalizeEffectValue(resolvedType, effect.Value))
                + GetDynamicEffectValue(item, resolvedType),
        };

        var sanctuary = GetItemSanctuary(item);
        if (sanctuary == 9 && (definition.Position & 0xF00) != 0)
            sanctuary = 10;

        if (!IsSanctuaryExcludedType(resolvedType))
            value = value * (sanctuary + 10) / 10;

        if (resolvedType == LegacyItemEffect.RunSpeed)
        {
            if (value >= 3)
                value = 2;
            if (value > 0 && sanctuary == 9)
                value++;
        }

        return value;
    }

    public int GetItemSanctuary(LegacyItem item)
    {
        if (item.Index is >= 2330 and < 2390)
            return 0;

        var raw = FirstDynamicValue(item, static effect => effect is >= 116 and <= 125);
        if (raw is null)
            raw = FirstDynamicValue(item, static effect => effect == LegacyItemEffect.Sanctuary);
        if (raw is null) return 0;

        return raw.Value switch
        {
            9 => 9,
            >= 230 and <= 233 => 10,
            >= 234 and <= 237 => 12,
            >= 238 and <= 241 => 15,
            >= 242 and <= 245 => 18,
            >= 246 and <= 249 => 22,
            >= 250 and <= 253 => 27,
            _ => raw.Value % 10,
        };
    }

    public int GetItemPosition(LegacyItem item) => this[item.Index]?.Position ?? 0;

    public int GetItemGrade(LegacyItem item) => this[item.Index]?.Grade ?? -1;

    /// <summary>Ports BASE_GetItemGem, including the legacy sanction/gem encoding.</summary>
    public int GetItemGem(LegacyItem item)
    {
        if (item.Index is >= 2330 and < 2390)
            return 0;

        var encoded = FirstDynamicValue(item, static effect => effect is >= 116 and <= 125)
            ?? FirstDynamicValue(item, static effect => effect == LegacyItemEffect.Sanctuary);
        if (encoded is null || encoded.Value < 230)
            return -1;

        return (encoded.Value - 230) % 4;
    }

    private static bool IsSanctuaryExcludedType(int type) => type is
        33 or 18 or LegacyItemEffect.Position or 21 or 27 or 1 or 22 or 23 or 24 or 25 or LegacyItemEffect.Volatile or 78 or 84 or LegacyItemEffect.ItemLevel or LegacyItemEffect.MobType or 113 or LegacyItemEffect.NoSanctuary or 127 or 91;

    private static bool HasDynamicEffect(LegacyItem item, int effect) =>
        item.Effect1 == effect || item.Effect2 == effect || item.Effect3 == effect;

    private static int GetDynamicEffectValue(LegacyItem item, int effect)
    {
        var value = 0;
        if (item.Effect1 == effect) value += NormalizeEffectValue(effect, item.Value1);
        if (item.Effect2 == effect) value += NormalizeEffectValue(effect, item.Value2);
        if (item.Effect3 == effect) value += NormalizeEffectValue(effect, item.Value3);
        return value;
    }

    private static int NormalizeEffectValue(int effect, int value) =>
        effect == LegacyItemEffect.AttackSpeed && value == 1 ? 10 : value;

    private static int? FirstDynamicValue(LegacyItem item, Func<int, bool> predicate)
    {
        if (predicate(item.Effect1)) return item.Value1;
        if (predicate(item.Effect2)) return item.Value2;
        if (predicate(item.Effect3)) return item.Value3;
        return null;
    }

    private static string ReadCString(ReadOnlySpan<byte> source)
    {
        var length = source.IndexOf((byte)0);
        if (length < 0) length = source.Length;
        return Encoding.ASCII.GetString(source[..length]);
    }
}

using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;

namespace WydCdk.World;

public static class LegacyItemEffect
{
    public const int Damage = 2;
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
    public const int WDay = 106;
    public const int Year = 109;
    public const int WMonth = 110;
}

public sealed record LegacyItemStaticEffect(short Effect, short Value);

/// <summary>One decoded STRUCT_ITEMLIST record from the legacy ItemList.bin.</summary>
public sealed record LegacyItemDefinition(
    short Index,
    string Name,
    short RequiredLevel,
    short Unique,
    short Position,
    short Grade,
    IReadOnlyList<LegacyItemStaticEffect> StaticEffects,
    short Extra = 0);

/// <summary>
/// Reader and the small BASE_GetItemAbility subset required by combat.
/// ItemList.bin stores 6500 STRUCT_ITEMLIST records (140 bytes each), XORed
/// with 0x5A, followed by a legacy random/checksum integer. The checksum is
/// intentionally retained but not validated because the C++ server also has
/// its production comparison disabled.
/// </summary>
public sealed class LegacyItemDataTable
{
    public const int MaxItemIndex = 6500;
    public const int RecordSizeInBytes = 140;
    public const int ChecksumSizeInBytes = sizeof(int);
    public const int BodySizeInBytes = MaxItemIndex * RecordSizeInBytes;
    public const int FileSizeInBytes = BodySizeInBytes + ChecksumSizeInBytes;
    private const byte XorKey = 0x5A;
    private const int StaticEffectCount = 12;
    private const int NameOffset = 0;
    private const int StaticEffectsOffset = 80;
    private const int UniqueOffset = 132;
    private const int PositionOffset = 134;
    private const int ExtraOffset = 136;
    private const int GradeOffset = 138;

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
        if (encodedFile.Length != FileSizeInBytes)
            throw new InvalidDataException($"Legacy ItemList.bin must contain exactly {FileSizeInBytes} bytes.");

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
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(70)),
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(UniqueOffset)),
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(PositionOffset)),
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(GradeOffset)),
                effects,
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(ExtraOffset)));
        }

        return new LegacyItemDataTable(definitions);
    }

    /// <summary>Subset of BASE_GetItemAbility used by combat and weapon composition.</summary>
    public int GetItemAbility(LegacyItem item, int type)
    {
        var definition = this[item.Index];
        if (definition is null) return 0;

        var resolvedType = type;
        if (type == LegacyItemEffect.Damage && definition.Position == 32 && HasDynamicEffect(item, LegacyItemEffect.Damage2))
            resolvedType = LegacyItemEffect.Damage2;

        var value = resolvedType switch
        {
            LegacyItemEffect.Position => definition.Position,
            LegacyItemEffect.ItemLevel when item.Index is >= 2330 and < 2360 => item.Effect2 - 1,
            LegacyItemEffect.ItemLevel => definition.RequiredLevel,
            _ => definition.StaticEffects.Where(effect => effect.Effect == resolvedType).Sum(effect => effect.Value)
                + GetDynamicEffectValue(item, resolvedType),
        };

        var sanctuary = GetItemSanctuary(item);
        if (sanctuary == 9 && (definition.Position & 0xF00) != 0)
            sanctuary = 10;

        if (!IsSanctuaryExcludedType(resolvedType))
            value = value * (sanctuary + 10) / 10;

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
        if (item.Effect1 == effect) value += item.Value1;
        if (item.Effect2 == effect) value += item.Value2;
        if (item.Effect3 == effect) value += item.Value3;
        return value;
    }

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

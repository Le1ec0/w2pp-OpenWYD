using System.Security.Cryptography;
using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Compatibility port of TMSrv <c>SetItemBonus2</c>, used when a reset-item
/// is consumed. The tables are the 7.60 W2PP tables; later Reference759
/// releases have a different armor table and must not be mixed into this
/// profile without their own equivalence fixtures.
/// </summary>
public static class LegacyItemRefinementMath
{
    private readonly record struct BonusRow(byte Effect1, byte Value1, byte Effect2, byte Value2);

    private static readonly BonusRow[] Helmet =
    [
        new(4, 60, 26, 18), new(4, 60, 26, 15), new(4, 60, 26, 12),
        new(4, 50, 26, 18), new(4, 50, 26, 15), new(4, 50, 26, 12),
        new(4, 40, 26, 18), new(4, 40, 26, 15), new(4, 40, 26, 12),
        new(4, 30, 26, 18), new(4, 30, 26, 15), new(4, 30, 26, 12),
        new(4, 60, 60, 12), new(4, 60, 60, 10), new(4, 60, 60, 8), new(4, 60, 60, 6),
        new(4, 50, 60, 12), new(4, 50, 60, 10), new(4, 50, 60, 8), new(4, 50, 60, 6),
        new(4, 40, 60, 12), new(4, 40, 60, 10), new(4, 40, 60, 8), new(4, 40, 60, 6), new(4, 40, 60, 4),
    ];

    private static readonly BonusRow[] Armor =
    [
        new(2, 30, 3, 30), new(2, 30, 3, 25), new(2, 30, 3, 20), new(2, 30, 3, 10),
        new(2, 24, 3, 30), new(2, 24, 3, 25), new(2, 24, 3, 20), new(2, 24, 3, 15),
        new(2, 18, 3, 30), new(2, 18, 3, 25), new(2, 18, 3, 20), new(2, 18, 3, 15),
        new(2, 30, 71, 50), new(2, 30, 71, 60), new(2, 30, 71, 70),
        new(2, 24, 71, 50), new(2, 24, 71, 60), new(2, 24, 71, 70),
        new(2, 18, 71, 50), new(2, 18, 71, 60), new(2, 18, 71, 70),
        new(60, 10, 3, 30), new(60, 10, 3, 25), new(60, 10, 3, 20), new(60, 10, 3, 15), new(60, 10, 3, 10),
        new(60, 8, 3, 30), new(60, 8, 3, 25), new(60, 8, 3, 20), new(60, 8, 3, 15), new(60, 8, 3, 10),
        new(60, 6, 3, 30), new(60, 6, 3, 25), new(60, 6, 3, 20), new(60, 6, 3, 15), new(60, 6, 3, 10),
        new(60, 10, 71, 50), new(60, 10, 71, 60), new(60, 10, 71, 70),
        new(60, 8, 71, 50), new(60, 8, 71, 60), new(60, 8, 71, 70),
        new(60, 6, 71, 50), new(60, 6, 71, 60), new(60, 6, 71, 70),
        new(60, 4, 71, 50), new(60, 4, 71, 60), new(60, 4, 71, 70),
    ];

    private static readonly BonusRow[] Gloves =
    [
        new(2, 30, 72, 30), new(2, 30, 72, 25), new(2, 30, 72, 20), new(2, 30, 72, 15), new(2, 30, 72, 10),
        new(2, 24, 72, 30), new(2, 24, 72, 25), new(2, 24, 72, 20), new(2, 24, 72, 15), new(2, 24, 72, 10),
        new(2, 18, 72, 30), new(2, 18, 72, 25), new(2, 18, 72, 20), new(2, 18, 72, 15), new(2, 18, 72, 10),
        new(60, 10, 72, 30), new(60, 10, 72, 25), new(60, 10, 72, 20), new(60, 10, 72, 15),
        new(60, 8, 72, 30), new(60, 8, 72, 25), new(60, 8, 72, 20), new(60, 8, 72, 15),
        new(60, 6, 72, 30), new(60, 6, 72, 25), new(60, 6, 72, 20), new(60, 6, 72, 15),
    ];

    private static readonly BonusRow[] Boots =
    [
        new(2, 30, 74, 18), new(2, 30, 74, 15), new(2, 30, 74, 12),
        new(2, 24, 74, 18), new(2, 24, 74, 15), new(2, 24, 74, 12),
        new(2, 18, 74, 18), new(2, 18, 74, 15), new(2, 18, 74, 12),
        new(2, 12, 74, 18), new(2, 12, 74, 15), new(2, 12, 74, 12),
        new(2, 6, 74, 18), new(2, 6, 74, 15), new(2, 6, 74, 12),
        new(2, 30, 60, 10), new(2, 30, 60, 8), new(2, 30, 60, 6),
        new(2, 24, 60, 10), new(2, 24, 60, 8), new(2, 24, 60, 6),
        new(2, 18, 60, 10), new(2, 18, 60, 8), new(2, 18, 60, 6),
        new(2, 12, 60, 10), new(2, 12, 60, 8), new(2, 12, 60, 6),
        new(2, 6, 60, 10), new(2, 6, 60, 8), new(2, 6, 60, 6),
    ];

    public static LegacyItem Apply(
        LegacyItem item,
        LegacyItemDefinition definition,
        LegacyItemDataTable? itemData = null,
        Func<int, int>? roll = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var table = definition.Position switch
        {
            2 => Helmet,
            4 or 8 => Armor,
            16 => Gloves,
            32 => Boots,
            _ => null,
        };
        if (table is null)
            return item;

        var random = roll ?? (static maximum => RandomNumberGenerator.GetInt32(maximum));
        item = ApplySanctuary(item, random);
        var row = table[Next(random, table.Length)];
        item = item with { Effect2 = row.Effect1, Value2 = row.Value1, Effect3 = row.Effect2, Value3 = row.Value2 };

        if (definition.Position == 32 && itemData is not null)
            item = CapBootDamage(item, itemData);

        return item;
    }

    /// <summary>Applies BASE_SetItemSanc's 7.60 sanctuary encoding.</summary>
    public static LegacyItem SetSanctuary(LegacyItem item, int sanctuary, int success = 0)
    {
        sanctuary = Math.Clamp(sanctuary, 0, 15);
        success = Math.Clamp(success, 0, 20);
        var raw = sanctuary + (10 * success);
        if (sanctuary >= 10)
            raw = 230 + ((sanctuary - 10) * 4) + success;

        if (item.Effect1 == LegacyItemEffect.Sanctuary || item.Effect1 is >= 116 and <= 125)
            return item with { Value1 = checked((byte)raw) };
        if (item.Effect2 == LegacyItemEffect.Sanctuary || item.Effect2 is >= 116 and <= 125)
            return item with { Value2 = checked((byte)raw) };
        if (item.Effect3 == LegacyItemEffect.Sanctuary || item.Effect3 is >= 116 and <= 125)
            return item with { Value3 = checked((byte)raw) };
        return item;
    }

    /// <summary>Matches the use-item handler's first free sanctuary-effect slot.</summary>
    public static LegacyItem? TryEnsureSanctuaryEffect(LegacyItem item)
    {
        if (CanReceiveSanctuary(item.Effect1)) return item with { Effect1 = LegacyItemEffect.Sanctuary, Value1 = 0 };
        if (CanReceiveSanctuary(item.Effect2)) return item with { Effect2 = LegacyItemEffect.Sanctuary, Value2 = 0 };
        if (CanReceiveSanctuary(item.Effect3)) return item with { Effect3 = LegacyItemEffect.Sanctuary, Value3 = 0 };
        return null;
    }

    private static bool CanReceiveSanctuary(int effect) => effect == 0 || effect == LegacyItemEffect.Sanctuary || effect is >= 116 and <= 125;

    private static LegacyItem ApplySanctuary(LegacyItem item, Func<int, int> random)
    {
        if (item.Effect1 != LegacyItemEffect.Sanctuary)
            return item with { Effect1 = LegacyItemEffect.Sanctuary, Value1 = (byte)Next(random, 2) };

        var sanctuary = DecodeSanctuary(item.Value1);
        if (sanctuary >= 6)
            return item;

        sanctuary = Math.Min(6, sanctuary + Next(random, 2));
        return item with { Value1 = EncodeSanctuary(sanctuary) };
    }

    private static LegacyItem CapBootDamage(LegacyItem item, LegacyItemDataTable itemData)
    {
        item = CapBootDamageSlot(item, itemData, slot: 2);
        return CapBootDamageSlot(item, itemData, slot: 3);
    }

    private static LegacyItem CapBootDamageSlot(LegacyItem item, LegacyItemDataTable itemData, int slot)
    {
        var effect = slot == 2 ? item.Effect2 : item.Effect3;
        var value = slot == 2 ? item.Value2 : item.Value3;
        if (effect != LegacyItemEffect.Damage)
            return item;

        var ability = itemData.GetItemAbility(item, LegacyItemEffect.Damage);
        if (ability <= 30)
            return item;

        value = (byte)Math.Max(0, value - Math.Min(value, ability - 30));
        return slot == 2 ? item with { Value2 = value } : item with { Value3 = value };
    }

    private static int Next(Func<int, int> random, int exclusiveMax)
    {
        var value = random(exclusiveMax);
        if (value < 0 || value >= exclusiveMax)
            throw new ArgumentOutOfRangeException(nameof(random), $"Injected roll must be in [0, {exclusiveMax}).");
        return value;
    }

    private static int DecodeSanctuary(int raw) => raw switch
    {
        >= 230 and <= 233 => 10,
        >= 234 and <= 237 => 11,
        >= 238 and <= 241 => 12,
        >= 242 and <= 245 => 13,
        >= 246 and <= 249 => 14,
        >= 250 and <= 253 => 15,
        _ => raw % 10,
    };

    private static byte EncodeSanctuary(int sanctuary) => sanctuary switch
    {
        10 => 230,
        11 => 234,
        12 => 238,
        13 => 242,
        14 => 246,
        15 => 250,
        _ => (byte)Math.Clamp(sanctuary, 0, 15),
    };
}

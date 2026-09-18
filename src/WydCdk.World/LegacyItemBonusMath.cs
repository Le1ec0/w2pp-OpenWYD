using System.Security.Cryptography;
using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Compatibility port of the legacy TMSrv SetItemBonus routine.
/// The random source is injectable so each branch can be tested without
/// changing the server's production randomness.
/// </summary>
public static class LegacyItemBonusMath
{
    private static readonly int[] BonusTypes = [10, 30, 2, 5, 3, 10, 2, 8, 2, 8];

    // The legacy source declares g_pBonusValue[10][2][2]. Its SetItemBonus
    // indexes the second dimension with lvdif values up to 3, which is an
    // out-of-bounds read for high-level items. Clamp to the last declared
    // table tier instead of reproducing undefined memory reads.
    private static readonly int[,,] BonusValues =
    {
        { { 10, 30 }, { 2, 5 } },
        { { 3, 10 }, { 2, 8 } },
        { { 2, 8 }, { 2, 8 } },
        { { 2, 6 }, { 2, 6 } },
        { { 2, 6 }, { 2, 6 } },
        { { 20, 45 }, { 4, 8 } },
        { { 7, 20 }, { 5, 14 } },
        { { 5, 14 }, { 5, 14 } },
        { { 4, 12 }, { 4, 12 } },
        { { 4, 12 }, { 4, 12 } },
    };

    public static LegacyItem Apply(
        LegacyItem item,
        LegacyItemDefinition definition,
        int level,
        int dropBonus,
        bool specialVariant = false,
        Func<int, int>? roll = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var random = roll ?? (static maximum => RandomNumberGenerator.GetInt32(maximum));
        var addDropBonus = Math.Clamp(dropBonus / 8, 0, 2);
        var addGrade = -1;

        if (item.Effect1 is >= 100 and <= 105)
        {
            addGrade = item.Effect1 - 100;
            item = item with { Effect1 = 0, Value1 = 0 };
        }

        if (!specialVariant && level >= 210)
            level -= 47;

        var requestedLevelDifference = (level - definition.RequiredLevel + 1) / 25;
        if (addGrade >= 0)
            requestedLevelDifference = addGrade;

        var forceHigh = requestedLevelDifference >= 4;
        var levelDifference = Math.Clamp(requestedLevelDifference, 0, 3);
        if (specialVariant && levelDifference >= 3)
            levelDifference = 2;

        if ((definition.Position & 0xFE) != 0 && item.Value1 == 0 && definition.Position != 128)
        {
            var firstRandom = Next(random, 101);
            var firstChoice = GetFirstChoice(definition.Position, definition.Unique, FirstAttributeRoll(firstRandom, levelDifference, addDropBonus, specialVariant));
            var secondBaseRandom = Next(random, 100);
            var secondRandom = specialVariant ? (2 * secondBaseRandom / 3) : secondBaseRandom;
            var secondChoice = GetSecondChoice(definition.Position, definition.Unique, SecondAttributeRoll(secondRandom, levelDifference, specialVariant));
            var firstTierRandom = Next(random, 100);
            var firstTier = GetFirstTier(firstTierRandom, levelDifference, forceHigh, specialVariant);
            var firstLevel = firstTier + firstChoice.Offset;
            item = WriteFirstEffect(item, firstChoice, firstLevel, definition.Position, random);

            var secondTierRandom = Next(random, 100);
            var secondTier = GetSecondTier(secondTierRandom, levelDifference, forceHigh, specialVariant, firstTierRandom);
            var secondLevel = secondTier + secondChoice.Offset;
            item = WriteSecondEffect(item, secondChoice, secondLevel, random);

            if (item.Value1 == 0)
            {
                var sanctuaryRoll = Next(random, 100);
                if (specialVariant)
                    sanctuaryRoll /= 2;
                item = WriteSanctuaryOrBonus(item, levelDifference, sanctuaryRoll, addGrade, random);
            }
        }

        item = ApplyStaticEffects(item, definition, random);
        return ApplySpecialItemFillers(item, random);
    }

    private static int Next(Func<int, int> random, int exclusiveMax)
    {
        var value = random(exclusiveMax);
        if (value < 0 || value >= exclusiveMax)
            throw new ArgumentOutOfRangeException(nameof(random), $"Injected roll must be in [0, {exclusiveMax}).");
        return value;
    }

    private static int FirstAttributeRoll(int firstRandom, int levelDifference, int addDropBonus, bool specialVariant) =>
        specialVariant
            ? firstRandom % 3
            : levelDifference == 0
                ? firstRandom % (8 - addDropBonus)
                : levelDifference is 1 or 2
                    ? firstRandom % (6 - addDropBonus)
                    : firstRandom % 4;

    private static int SecondAttributeRoll(int secondRandom, int levelDifference, bool specialVariant) =>
        specialVariant
            ? secondRandom % 4
            : levelDifference == 0
                ? secondRandom % 8
                : levelDifference is 1 or 2
                    ? secondRandom % 6
                    : secondRandom % 4;

    private static int GetFirstTier(int firstRandom, int levelDifference, bool forceHigh, bool specialVariant)
    {
        var tier = GetTier(firstRandom % 100, levelDifference);
        if (forceHigh && tier < 4) tier = 4;
        if (specialVariant && tier >= 5) tier = 4;
        return tier;
    }

    private static int GetSecondTier(int secondRandom, int levelDifference, bool forceHigh, bool specialVariant, int firstTierRandom)
    {
        // The original mistakenly tests v35 (the first roll) for the level-0
        // branch, and writes v28 instead of v27 when that test fails.
        var tier = levelDifference == 0 && firstTierRandom < 2
            ? 0
            : GetTier(secondRandom % 100, levelDifference);
        if (forceHigh && tier < 3) tier = 3;
        if (specialVariant && tier >= 5) tier = 4;
        return tier;
    }

    private static int GetTier(int value, int levelDifference) => levelDifference switch
    {
        0 => value >= 2 ? value >= 6 ? value >= 24 ? value < 55 ? 1 : 0 : 2 : 3 : 4,
        1 => value >= 1 ? value >= 5 ? value >= 24 ? value >= 65 ? 1 : 2 : 3 : 4 : 5,
        2 => value >= 2 ? value >= 16 ? value >= 60 ? 2 : 3 : 4 : 5,
        _ => value >= 2 ? value >= 9 ? value >= 45 ? value >= 75 ? 2 : 3 : 4 : 5 : 6,
    };

    private static (byte Effect, int Value, int Offset) GetFirstChoice(int position, int unique, int roll) => position switch
    {
        2 => roll switch { 0 => ((byte)26, 3, 0), 1 => ((byte)60, 2, 0), _ => ((byte)59, 0, 0) },
        4 or 8 => ((byte)71, 10, 1),
        16 => ((byte)72, 5, 0),
        32 => ((byte)73, 6, -1),
        64 or 192 when unique is not (44 or 47) => roll switch { 0 => ((byte)26, 3, 1), 1 => ((byte)2, 9, -1), 2 => ((byte)74, 3, 0), _ => ((byte)59, 0, 0) },
        64 or 192 => roll switch { 0 => ((byte)60, 4, -1), 1 => ((byte)74, 3, 1), _ => ((byte)59, 0, 0) },
        _ => ((byte)59, 0, 0),
    };

    private static (byte Effect, int Value, int Offset) GetSecondChoice(int position, int unique, int roll) => position switch
    {
        2 => roll switch { 0 => ((byte)4, 10, 0), 1 => ((byte)3, 5, -1), _ => ((byte)59, 0, 0) },
        4 or 8 => roll switch { 0 => ((byte)60, 2, -1), 1 => ((byte)2, 6, -1), 2 => ((byte)3, 5, -1), _ => ((byte)59, 0, 0) },
        16 => roll switch { 0 => ((byte)60, 2, -1), 1 => ((byte)2, 6, -1), 2 => ((byte)74, 3, 0), 3 => ((byte)54, 3, 0), _ => ((byte)59, 0, 0) },
        32 => roll switch { 0 => ((byte)60, 2, -1), 1 => ((byte)74, 3, 0), _ => ((byte)59, 0, 0) },
        64 or 128 or 192 when unique is not (44 or 47) => roll switch { 0 => ((byte)26, 3, 1), 1 => ((byte)2, 9, -1), 2 => ((byte)74, 3, 1), _ => ((byte)59, 0, 0) },
        64 or 128 or 192 => roll switch { 0 => ((byte)60, 4, -1), 1 => ((byte)74, 3, 1), _ => ((byte)59, 0, 0) },
        _ => ((byte)59, 0, 0),
    };

    private static LegacyItem WriteFirstEffect(LegacyItem item, (byte Effect, int Value, int Offset) choice, int tier, int position, Func<int, int> random)
    {
        if (item.Value1 != 0) return item;
        if (tier > 0)
            return item with { Effect1 = choice.Effect, Value1 = ToByte(choice.Value * tier) };
        if (position == 32)
            return item with { Effect1 = choice.Effect, Value1 = 0 };
        return item with { Effect1 = 59, Value1 = ToByte(Next(random, 128)) };
    }

    private static LegacyItem WriteSecondEffect(LegacyItem item, (byte Effect, int Value, int Offset) choice, int tier, Func<int, int> random)
    {
        if (item.Value2 != 0) return item;
        return tier > 0
            ? item with { Effect2 = choice.Effect, Value2 = ToByte(choice.Value * tier) }
            : item with { Effect2 = 59, Value2 = ToByte(Next(random, 128)) };
    }

    private static LegacyItem WriteSanctuaryOrBonus(LegacyItem item, int levelDifference, int roll, int addGrade, Func<int, int> random)
    {
        var v24 = levelDifference >= 2 ? 35 : 22;
        var v23 = levelDifference >= 2 ? 85 : 75;
        var v22 = levelDifference >= 2 ? 100 : 90;

        if (roll < 6)
        {
            var sanctuary = 2;
            if (addGrade > 2)
            {
                var gradeRoll = Next(random, 100);
                sanctuary = addGrade switch
                {
                    3 => gradeRoll < 30 ? 3 : 2,
                    4 => gradeRoll < 10 ? 4 : gradeRoll < 40 ? 3 : 2,
                    5 => gradeRoll < 10 ? 5 : gradeRoll < 30 ? 4 : gradeRoll < 60 ? 3 : 2,
                    6 => gradeRoll < 10 ? 6 : gradeRoll < 20 ? 5 : gradeRoll < 40 ? 4 : gradeRoll < 60 ? 3 : 2,
                    7 => gradeRoll < 4 ? 7 : gradeRoll < 10 ? 6 : gradeRoll < 20 ? 5 : gradeRoll < 35 ? 4 : gradeRoll < 60 ? 3 : 2,
                    _ => 2,
                };
            }
            return item with { Effect1 = 43, Value1 = ToByte(sanctuary) };
        }
        if (roll < v24)
            return item with { Effect1 = 43, Value1 = 1 };
        if (roll < v23)
        {
            var index = Next(random, 10);
            var tier = Math.Min(levelDifference, 1);
            var min = BonusValues[index, tier, 0];
            var max = BonusValues[index, tier, 1];
            return item with { Effect1 = ToByte(BonusTypes[index]), Value1 = ToByte(Next(random, max + 1 - min) + min) };
        }
        if (roll < v22)
            return item with { Effect1 = 43, Value1 = 0 };

        return item with { Effect1 = 59, Value1 = ToByte(Next(random, 128)) };
    }

    private static LegacyItem ApplyStaticEffects(LegacyItem item, LegacyItemDefinition definition, Func<int, int> random)
    {
        foreach (var effect in definition.StaticEffects)
        {
            switch (effect.Effect)
            {
                case 0x2B:
                    item = item with { Effect1 = 43, Value1 = ToByte(effect.Value) };
                    break;
                case 0x3D:
                    item = item with { Effect1 = 61, Value1 = ToByte(effect.Value) };
                    break;
                case 0x4E:
                    item = item with { Effect1 = 78, Value1 = ToByte(Math.Min(9, Next(random, 4) + effect.Value)) };
                    break;
            }
        }
        return item;
    }

    private static LegacyItem ApplySpecialItemFillers(LegacyItem item, Func<int, int> random)
    {
        if (item.Index is 412 or 413 or 419 or 420 or 753 ||
            (item.Index is >= 447 and <= 450) || (item.Index is >= 692 and <= 695))
        {
            if (item.Effect1 == 0) item = item with { Effect1 = 59, Value1 = ToByte(Next(random, 256)) };
            if (item.Effect2 == 0) item = item with { Effect2 = 59, Value2 = ToByte(Next(random, 256)) };
            if (item.Effect3 == 0) item = item with { Effect3 = 59, Value3 = ToByte(Next(random, 256)) };
        }
        return item;
    }

    private static byte ToByte(int value) => (byte)Math.Clamp(value, 0, 255);
}

using System.Security.Cryptography;
using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>One common drop selected from an NPC carry slot.</summary>
public sealed record LegacyItemDropCandidate(int SourceSlot, LegacyItem Item);

public sealed record LegacyNpcEventDropConfiguration(
    bool Enabled,
    int StartIndex,
    int EndIndex,
    int CurrentIndex,
    int Rate,
    int ItemIndex,
    bool Indexed,
    bool Notice = true);

public sealed record LegacyNpcEventDropSnapshot(
    LegacyNpcEventDropConfiguration Configuration,
    int CurrentIndex);

public sealed record LegacyNpcRuneReward(int ItemIndex, int ProgressionValue);

/// <summary>
/// Deterministic port of the common-drop roll in <c>MobKilled.cpp</c>.
/// Inventory insertion, item-bonus generation, and event/boss-specific drops
/// remain separate so each rule can be covered against the legacy source.
/// </summary>
public static class LegacyNpcDropMath
{
    private static readonly int[] DropRates =
    [
        900, 900, 900, 900, 900, 900, 900, 900, 4, 4, 4, 4, 900, 900, 900, 900,
        20000, 20000, 20000, 20000, 20000, 20000, 20000, 20000,
        2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000,
        2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000,
        3000, 3000, 3000, 3000, 3000, 3000, 3000, 3000,
        1, 35, 500, 2500, 5000, 5000, 10000, 20000,
    ];

    public static IReadOnlyList<LegacyItemDropCandidate> SelectCommonDrops(
        ReadOnlySpan<byte> npcMob,
        int targetLevel,
        int attackerDropBonus = 0,
        Func<int, int>? roll = null,
        LegacyItemDataTable? itemData = null)
    {
        if (npcMob.Length < LegacyAccountSnapshot.MobCarryOffset + (LegacyAccountSnapshot.MobCarryCount * LegacyItem.SizeInBytes))
            return [];

        var drops = new List<LegacyItemDropCandidate>();
        var targetState = LegacyMobCombatState.Read(npcMob);
        var dropBonus = attackerDropBonus;
        var special2 = targetState.CurrentScore.Special3;
        var random = roll ?? (static maximum => RandomNumberGenerator.GetInt32(maximum));

        for (var slot = 0; slot < LegacyAccountSnapshot.MobCarryCount; slot++)
        {
            var item = LegacyItem.Read(npcMob.Slice(LegacyAccountSnapshot.MobCarryOffset + (slot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
            if (item.Index == 0) continue;

            var dropRate = DropRates[slot];
            var adjustedBonus = 100 + dropBonus;
            if (adjustedBonus != 100)
            {
                adjustedBonus = 10000 / (adjustedBonus + 1);
                dropRate = adjustedBonus * dropRate / 100;
            }

            if (slot < 60)
            {
                var position = slot / 8;
                if (position is 0 or 1 or 2)
                {
                    dropRate = targetLevel < 10 ? 4 * dropRate / 100
                        : targetLevel < 20 ? 5 * dropRate / 100
                        : targetLevel < 30 ? 6 * dropRate / 100
                        : targetLevel < 40 ? 7 * dropRate / 100
                        : targetLevel < 60 ? 8 * dropRate / 100
                        : 99 * dropRate / 100;
                }
            }
            else
            {
                dropRate = targetLevel < 170 ? 90 * dropRate / 100
                    : targetLevel < 200 ? 60 * dropRate / 100
                    : targetLevel < 230 ? 50 * dropRate / 100
                    : targetLevel < 255 ? 43 * dropRate / 100
                    : targetLevel < 320 ? 38 * dropRate / 100
                    : 50 * dropRate / 100;
            }

            if (special2 != 0 && (npcMob[LegacyAccountSnapshot.MobRsvOffset] & 4) != 0)
            {
                var specialDrop = 100 - (special2 / 10 + 10);
                dropRate = specialDrop * dropRate / 100;
            }

            if (slot is 8 or 9 or 10) dropRate = 4;
            if (slot == 11) dropRate = 1;
            dropRate = Math.Clamp(dropRate, 0, 32000);
            if (dropRate <= 0) continue;

            var dropRoll = random(dropRate);
            if (dropRoll != 0 && slot != 11) continue;
            if (item.Index <= 390 || item.Index >= LegacyItemDataTable.MaxItemIndex || item.Index == 454) continue;

            var requiredLevel = itemData?[item.Index]?.RequiredLevel ?? 0;
            if (requiredLevel >= 140 && (dropRoll % 2) != 1)
                continue;
            var definition = itemData?[item.Index];
            var finalizedItem = definition is null
                ? item
                : LegacyItemBonusMath.Apply(item, definition, targetLevel, attackerDropBonus, roll: roll);
            drops.Add(new LegacyItemDropCandidate(slot, finalizedItem));
            if (slot is 8 or 9 or 10) slot = 11;
        }

        return drops;
    }

    /// <summary>
    /// Selects the small GenerateIndex-specific boss drop block from
    /// <c>MobKilled.cpp</c>. The legacy handler only reaches this block when
    /// the map height is between -50 and 92; generators 3 and 5..7 have the
    /// narrower -40..36 gate as well.
    /// </summary>
    public static IReadOnlyList<LegacyItemDropCandidate> SelectBossDrops(
        int generateIndex,
        int? terrainHeight,
        LegacyItemDataTable? itemData = null,
        Func<int, int>? roll = null)
    {
        if (terrainHeight is not int height || height < -50 || height > 92)
            return [];

        if ((generateIndex is 3 or (>= 5 and <= 7)) && (height <= -40 || height >= 36))
            return [];

        var random = roll ?? (static maximum => RandomNumberGenerator.GetInt32(maximum));
        var itemIndex = generateIndex switch
        {
            0 or 1 or 2 => SelectFromFourteen(random, 419, 420),
            5 or 6 or 7 => SelectFromFourteen(random, 421, 419, 7),
            3 => SelectFromSeven(random, 1106, 1256, 1418, 1568),
            _ => 0,
        };
        if (itemIndex == 0)
            return [];

        var item = new LegacyItem((short)itemIndex, 0, 0, 0, 0, 0, 0);
        if (itemData?[itemIndex] is { } definition)
        {
            item = LegacyItemBonusMath.Apply(
                item,
                definition,
                level: generateIndex == 3 ? 75 : 0,
                dropBonus: 0,
                specialVariant: generateIndex == 3,
                roll: random);
        }

        return [new LegacyItemDropCandidate(-1, item)];
    }

    /// <summary>Port of the Coliseu (N) GenerateIndex 4623..4634 branch.</summary>
    public static IReadOnlyList<LegacyItemDropCandidate> SelectColiseumDrops(
        int generateIndex,
        int? terrainHeight,
        LegacyItemDataTable? itemData = null,
        Func<int, int>? roll = null)
    {
        if (generateIndex is < 4623 or > 4634 || terrainHeight is not int height || height < -50 || height > 92)
            return [];

        var random = roll ?? (static maximum => RandomNumberGenerator.GetInt32(maximum));
        var itemIndex = Next(random, 14) switch
        {
            0 => 419,
            1 => 420,
            2 => 4026,
            _ => 0,
        };
        if (itemIndex == 0)
            return [];

        var item = new LegacyItem((short)itemIndex, 0, 0, 0, 0, 0, 0);
        if (itemData?[itemIndex] is { } definition)
            item = LegacyItemBonusMath.Apply(item, definition, level: 0, dropBonus: 0, roll: random);
        return [new LegacyItemDropCandidate(-3, item)];
    }

    /// <summary>
    /// Selects the direct rune reward used by the Pista +2/+4/+5/+6 bosses.
    /// The room counters and boss respawns are deliberately separate state.
    /// </summary>
    public static LegacyNpcRuneReward? SelectRuneReward(
        int generateIndex,
        int? terrainHeight,
        Func<int, int>? roll = null,
        Func<int, int>? chanceRoll = null)
    {
        if (terrainHeight is not int height || height < -50 || height > 92)
            return null;

        var (items, progressionValue, chance) = generateIndex switch
        {
            5653 or 5654 => (new[] { 5110, 5112, 5115, 5113 }, 1, 20),
            5789 => (new[] { 5118, 5121, 5122, 5116, 5130 }, 3, 0),
            5849 => (new[] { 5125, 5126, 5124 }, 5, 0),
            5899 => (new[] { 5120, 5131, 5118, 5119, 5123, 5132 }, 6, 0),
            5767 => (new[] { 5130, 5131, 5119, 5133, 5120, 5123, 5132, 5129 }, 0, 0),
            _ => (Array.Empty<int>(), 0, 0),
        };
        if (items.Length == 0)
            return null;

        var random = roll ?? (static maximum => RandomNumberGenerator.GetInt32(maximum));
        if (chance > 0)
        {
            var chanceSource = chanceRoll ?? random;
            if (Next(chanceSource, 100) >= chance)
                return null;
        }
        return new LegacyNpcRuneReward(items[Next(random, items.Length)], progressionValue);
    }

    /// <summary>Builds the indexed item from the legacy global event drop.</summary>
    public static LegacyItem? CreateEventDrop(
        int itemIndex,
        int currentIndex,
        bool indexed,
        int targetLevel,
        LegacyItemDataTable? itemData = null,
        Func<int, int>? roll = null)
    {
        if (itemIndex <= 0 || itemIndex >= LegacyItemDataTable.MaxItemIndex)
            return null;

        var random = roll ?? (static maximum => RandomNumberGenerator.GetInt32(maximum));
        var item = new LegacyItem((short)itemIndex, 0, 0, 0, 0, 0, 0);
        if (indexed)
        {
            item = item with
            {
                Effect1 = 62,
                Value1 = ToByte(currentIndex / 256),
                Effect2 = 63,
                Value2 = ToByte(currentIndex),
                Effect3 = 59,
                Value3 = ToByte(Next(random, 256)),
            };
        }

        if (itemData?[itemIndex] is { } definition)
            item = LegacyItemBonusMath.Apply(item, definition, targetLevel, dropBonus: 0, roll: random);
        return item;
    }

    private static int SelectFromFourteen(Func<int, int> random, int first, int second, int? randomRange = null)
    {
        var value = Next(random, 14);
        if (value == 0)
            return randomRange is null ? first : first + Next(random, randomRange.Value);
        return value == 1 ? second : 0;
    }

    private static int SelectFromSeven(Func<int, int> random, params int[] items)
    {
        var value = Next(random, 7);
        return value < items.Length ? items[value] : 0;
    }

    private static int Next(Func<int, int> random, int exclusiveMax)
    {
        var value = random(exclusiveMax);
        if (value < 0 || value >= exclusiveMax)
            throw new ArgumentOutOfRangeException(nameof(random), $"Injected roll must be in [0, {exclusiveMax}).");
        return value;
    }

    private static byte ToByte(int value) => (byte)Math.Clamp(value, 0, 255);

}

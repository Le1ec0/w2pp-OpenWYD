using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>Ports TMProject 7.69 GetMatchCombineTiny without the W2PP predicate divergence.</summary>
public static class LegacyTinyCombineRules
{
    public const int CombineItemCount = 8;
    public const int CoinCost = 100_000_000;
    public const int ArchMobType = 1;
    public const int ForbiddenItemIndex = 747;

    /// <summary>
    /// Returns the configured success threshold for a valid Tiny recipe. The caller
    /// supplies CHANCEBASE from COMP_RATE; the 7.69 reader constrains it to 0..100.
    /// </summary>
    public static bool TryGetSuccessThreshold(
        IReadOnlyList<LegacyItem> items,
        LegacyItemDataTable itemData,
        int tinyBase,
        out int successThreshold)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(itemData);
        if (tinyBase is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(tinyBase), tinyBase, "Tiny CHANCEBASE must be between 0 and 100.");

        successThreshold = 0;
        if (items.Count != CombineItemCount || items.Any(static item => item.Index == ForbiddenItemIndex))
            return false;

        var target = items[0];
        var attribute = items[1];
        var catalyst = items[2];
        if (!IsItemIndex(target) || !IsItemIndex(attribute) || !IsItemIndex(catalyst))
            return false;

        if (itemData.GetItemAbility(target, LegacyItemEffect.MobType) != ArchMobType)
            return false;

        var targetDefinition = itemData[target.Index]!;
        var attributeDefinition = itemData[attribute.Index]!;
        if (!IsTinyGrade(targetDefinition.Grade) || !IsTinyGrade(attributeDefinition.Grade))
            return false;

        // The 7.69 handler rejects type 1. W2PP's `!= 4 || != 5` expression
        // rejects every value and is not the selected client/server contract.
        if (itemData.GetItemAbility(attribute, LegacyItemEffect.ItemType) == 1 ||
            itemData.GetItemAbility(catalyst, LegacyItemEffect.ItemType) == 1)
            return false;

        if (targetDefinition.Position != attributeDefinition.Position ||
            targetDefinition.Position is not (64 or 192))
            return false;

        if (itemData.GetItemSanctuary(target) < 9 ||
            itemData.GetItemSanctuary(catalyst) < 9 ||
            itemData.GetItemSanctuary(attribute) > 237)
            return false;

        var threshold = tinyBase + (itemData.GetItemAbility(attribute, LegacyItemEffect.ItemLevel) * 5);
        if (threshold == 0)
            return false; // GetMatchCombineTiny uses zero as its no-match sentinel; the handler rejects it.

        successThreshold = threshold;
        return true;
    }

    private static bool IsItemIndex(LegacyItem item) =>
        item.Index > 0 && item.Index < LegacyItemDataTable.MaxItemIndex;

    private static bool IsTinyGrade(short grade) => grade is >= 5 and <= 8;
}

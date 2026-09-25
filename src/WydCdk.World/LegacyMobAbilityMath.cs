using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Ports the equipment aggregation performed by the 7.69 BASE_GetMobAbility.
/// A 16-entry span is the legacy W2PP view; an 18-entry span is the client 7.69
/// view and includes NewSlot1/NewSlot2. This keeps the old blob contract intact
/// while allowing the target layout to participate once its state is supplied.
/// </summary>
public static class LegacyMobAbilityMath
{
    private const uint HunterWeaponMastery = 1u << 10;
    private const uint ArmsWeaponMastery = 1u << 9;
    private const uint FoemaRangeSkill = 1u << 20;

    public static int GetAbility(
        ReadOnlySpan<LegacyItem> equipment,
        byte characterClass,
        uint learnedSkills,
        int effect,
        LegacyItemDataTable itemData)
    {
        ArgumentNullException.ThrowIfNull(itemData);
        var equipmentCount = GetEquipmentCount(equipment);

        if (effect == LegacyItemEffect.Range)
        {
            var maximum = GetMaximumAbility(equipment, effect, itemData);
            var faceClass = equipment[0].Index / 10;
            if (maximum < 2 && faceClass == 3 && (learnedSkills & FoemaRangeSkill) != 0)
                maximum = 2;
            return maximum;
        }

        var value = 0;
        Span<int> uniqueBySlot = stackalloc int[CharacterMobV769.EquipmentCount];
        uniqueBySlot.Clear();

        for (var slot = 0; slot < equipmentCount; slot++)
        {
            var item = equipment[slot];
            if (item.Index == 0 && slot != 7)
                continue;

            if (slot is >= 1 and <= 5)
                uniqueBySlot[slot] = itemData[item.Index]?.Unique ?? 0;

            if (effect == LegacyItemEffect.Damage && slot == 6)
                continue;
            if (effect == LegacyItemEffect.Magic && slot == 7)
                continue;

            if (slot == 7 && effect == LegacyItemEffect.Damage)
            {
                var leftDamage = itemData.GetItemAbility(equipment[6], effect) + itemData.GetItemAbility(equipment[6], LegacyItemEffect.Damage2);
                var rightDamage = itemData.GetItemAbility(equipment[7], effect) + itemData.GetItemAbility(equipment[7], LegacyItemEffect.Damage2);
                var leftUnique = GetUnique(itemData, equipment[6]);
                var rightUnique = GetUnique(itemData, equipment[7]);

                if (leftUnique != 0 && rightUnique != 0)
                {
                    var contributionPercent = leftUnique == rightUnique ? 50 : 30;
                    if ((learnedSkills & HunterWeaponMastery) != 0 && characterClass == 3 ||
                        (learnedSkills & ArmsWeaponMastery) != 0 && characterClass == 0)
                    {
                        contributionPercent = 100;
                    }

                    value += leftDamage > rightDamage
                        ? leftDamage + (rightDamage * contributionPercent / 100)
                        : rightDamage + (leftDamage * contributionPercent / 100);
                }
                else
                {
                    value += Math.Max(leftDamage, rightDamage);
                }

                continue;
            }

            var itemValue = itemData.GetItemAbility(item, effect);
            if (effect == LegacyItemEffect.AttackSpeed && itemValue == 1)
                itemValue = 10;
            value += itemValue;
        }

        if (effect == LegacyItemEffect.Ac && uniqueBySlot[1] != 0 &&
            uniqueBySlot[1] == uniqueBySlot[2] && uniqueBySlot[2] == uniqueBySlot[3] &&
            uniqueBySlot[3] == uniqueBySlot[4] && uniqueBySlot[4] == uniqueBySlot[5])
        {
            value = value * 105 / 100;
        }

        return value;
    }

    private static int GetMaximumAbility(ReadOnlySpan<LegacyItem> equipment, int effect, LegacyItemDataTable itemData)
    {
        var maximum = 0;
        for (var slot = 0; slot < GetEquipmentCount(equipment); slot++)
        {
            var item = equipment[slot];
            if (item.Index == 0)
                continue;

            maximum = Math.Max(maximum, itemData.GetItemAbility(item, effect));
        }

        return maximum;
    }

    private static int GetUnique(LegacyItemDataTable itemData, LegacyItem item) =>
        item.Index > 0 && item.Index < LegacyItemDataTable.MaxItemIndex
            ? itemData[item.Index]?.Unique ?? 0
            : 0;

    private static int GetEquipmentCount(ReadOnlySpan<LegacyItem> equipment)
    {
        if (equipment.Length >= CharacterMobV769.EquipmentCount)
            return CharacterMobV769.EquipmentCount;
        if (equipment.Length >= LegacyCharacterSelection.EquipmentCount)
            return LegacyCharacterSelection.EquipmentCount;
        throw new ArgumentException($"Equipment requires at least {LegacyCharacterSelection.EquipmentCount} slots.", nameof(equipment));
    }
}

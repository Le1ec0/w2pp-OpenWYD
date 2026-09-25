using WydCdk.Protocol;

namespace WydCdk.World;

public enum LegacyEquipmentCheckFailure
{
    None,
    InvalidItem,
    InvalidPosition,
    PositionNotAllowed,
    EquipmentLayoutInvalid,
    TwoHandedWeaponConflict,
    InvalidCharacterClass,
    CharacterClassNotAllowed,
    ItemClassMasterMismatch,
    AdvancedHelmetRestriction,
    LevelRequirementNotMet,
    StrengthRequirementNotMet,
    IntelligenceRequirementNotMet,
    DexterityRequirementNotMet,
    ConstitutionRequirementNotMet,
}

public readonly record struct LegacyEquipmentCheckResult(LegacyEquipmentCheckFailure Failure)
{
    public bool Allowed => Failure == LegacyEquipmentCheckFailure.None;
}

/// <summary>Faithful, side-effect-free port of the 7.69 BASE_CanEquip rules.</summary>
public static class LegacyEquipmentRules
{
    private const int Mortal = 2;
    private const int Arch = 1;
    private const int Celestial = 3;

    public static LegacyEquipmentCheckResult Evaluate(
        LegacyItem item,
        LegacyScore score,
        int position,
        int characterClass,
        int classMaster,
        int mortalFace,
        IReadOnlyList<LegacyItem> baseEquipment,
        LegacyItemDataTable itemData)
    {
        ArgumentNullException.ThrowIfNull(baseEquipment);
        ArgumentNullException.ThrowIfNull(itemData);

        if (item.Index <= 0 || item.Index >= LegacyItemDataTable.MaxItemIndex)
            return Denied(LegacyEquipmentCheckFailure.InvalidItem);

        if (position == 15)
            return Denied(LegacyEquipmentCheckFailure.InvalidPosition);

        if (position != -1)
        {
            if (position < 0 || position >= 31)
                return Denied(LegacyEquipmentCheckFailure.InvalidPosition);

            var itemPosition = itemData.GetItemAbility(item, LegacyItemEffect.Position);
            if (((itemPosition >> position) & 1) == 0)
                return Denied(LegacyEquipmentCheckFailure.PositionNotAllowed);

            if (position is 6 or 7)
            {
                var otherPosition = position == 6 ? 7 : 6;
                if (baseEquipment.Count <= otherPosition)
                    return Denied(LegacyEquipmentCheckFailure.EquipmentLayoutInvalid);

                var otherItem = baseEquipment[otherPosition];
                if (otherItem.Index > 0 && otherItem.Index < LegacyItemDataTable.MaxItemIndex)
                {
                    var itemUnique = itemData[item.Index]?.Unique ?? 0;
                    var otherUnique = itemData[otherItem.Index]?.Unique ?? 0;
                    var otherItemPosition = itemData.GetItemAbility(otherItem, LegacyItemEffect.Position);

                    if (itemPosition == 64 || otherItemPosition == 64)
                    {
                        if (itemUnique == 46)
                        {
                            if (otherItemPosition != 128)
                                return Denied(LegacyEquipmentCheckFailure.TwoHandedWeaponConflict);
                        }
                        else if (otherUnique == 46)
                        {
                            if (itemPosition != 128)
                                return Denied(LegacyEquipmentCheckFailure.TwoHandedWeaponConflict);
                        }
                        else
                        {
                            return Denied(LegacyEquipmentCheckFailure.TwoHandedWeaponConflict);
                        }
                    }
                }
            }
        }

        var effectiveClass = classMaster != Mortal ? mortalFace / 10 : characterClass;
        if (effectiveClass is < 0 or > 31)
            return Denied(LegacyEquipmentCheckFailure.InvalidCharacterClass);

        var classMask = itemData.GetItemAbility(item, LegacyItemEffect.Class);
        var classAllowed = ((classMask >> effectiveClass) & 1) != 0;
        if (!classAllowed && classMaster == Mortal)
            return Denied(LegacyEquipmentCheckFailure.CharacterClassNotAllowed);
        if (!classAllowed && classMaster != Mortal && position is not (6 or 7))
            return Denied(LegacyEquipmentCheckFailure.CharacterClassNotAllowed);

        var mobType = itemData.GetItemAbility(item, LegacyItemEffect.MobType);
        if (mobType == Arch && classMaster == Mortal ||
            mobType == Mortal && classMaster != Mortal ||
            mobType == Celestial && classMaster is Mortal or Arch)
        {
            return Denied(LegacyEquipmentCheckFailure.ItemClassMasterMismatch);
        }

        if (classMaster != Mortal && position == 1 && item.Index != 747 && item.Index is not (>= 3500 and <= 3507))
            return Denied(LegacyEquipmentCheckFailure.AdvancedHelmetRestriction);

        var levelRequirement = classMaster == Mortal ? itemData.GetItemAbility(item, LegacyItemEffect.Level) : 0;
        var strengthRequirement = classMaster == Mortal ? itemData.GetItemAbility(item, LegacyItemEffect.RequiredStrength) : 0;
        var intelligenceRequirement = classMaster == Mortal ? itemData.GetItemAbility(item, LegacyItemEffect.RequiredIntelligence) : 0;
        var dexterityRequirement = classMaster == Mortal ? itemData.GetItemAbility(item, LegacyItemEffect.RequiredDexterity) : 0;
        var constitutionRequirement = classMaster == Mortal ? itemData.GetItemAbility(item, LegacyItemEffect.RequiredConstitution) : 0;
        var weaponType = itemData.GetItemAbility(item, LegacyItemEffect.WeaponType);

        weaponType %= 10;
        var modifiedWeaponType = weaponType;
        var dividedWeaponType = weaponType / 10;
        if (position == 7 && weaponType != 0)
        {
            var rate = 100;
            if (dividedWeaponType == 0 && modifiedWeaponType > 1)
                rate = 130;
            else if (dividedWeaponType == 6 && modifiedWeaponType > 1)
                rate = 150;

            levelRequirement = levelRequirement * rate / 100;
            strengthRequirement = strengthRequirement * rate / 100;
            intelligenceRequirement = intelligenceRequirement * rate / 100;
            dexterityRequirement = dexterityRequirement * rate / 100;
            constitutionRequirement = constitutionRequirement * rate / 100;
        }

        if (levelRequirement > score.Level)
            return Denied(LegacyEquipmentCheckFailure.LevelRequirementNotMet);
        if (strengthRequirement > score.Strength)
            return Denied(LegacyEquipmentCheckFailure.StrengthRequirementNotMet);
        if (intelligenceRequirement > score.Intelligence)
            return Denied(LegacyEquipmentCheckFailure.IntelligenceRequirementNotMet);
        if (dexterityRequirement > score.Dexterity)
            return Denied(LegacyEquipmentCheckFailure.DexterityRequirementNotMet);
        if (constitutionRequirement > score.Constitution)
            return Denied(LegacyEquipmentCheckFailure.ConstitutionRequirementNotMet);

        return new LegacyEquipmentCheckResult(LegacyEquipmentCheckFailure.None);
    }

    private static LegacyEquipmentCheckResult Denied(LegacyEquipmentCheckFailure failure) => new(failure);
}

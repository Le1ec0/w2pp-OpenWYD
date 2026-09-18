using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>Progress flags read from the legacy STRUCT_MOBEXTRA quest state.</summary>
public readonly record struct LegacyExperienceEligibility(
    int ClassMaster,
    bool ArchLevel355Completed = false,
    bool ArchLevel370Completed = false,
    bool CelestialLevel40Completed = false,
    bool CelestialLevel90Completed = false);

/// <summary>
/// Integer-compatible port of <c>GetExpApply</c> from TMSrv/GetFunc.cpp.
/// It only normalizes the experience for class/level difference; awarding EXP,
/// party distribution, holds, bonuses, and NPC death dispatch remain separate.
/// </summary>
public static class LegacyExperienceMath
{
    public const int MaxLevel = 399;
    public const int ClassMasterCelestial = 3;
    public const int ClassMasterCelestialCs = 4;
    public const int ClassMasterSCelestial = 5;

    /// <summary>
    /// Ports the ExpBonus accumulation performed by BASE_GetCurrentScore and CMob.
    /// The caller supplies the already-authoritative MOBEXTRA-independent snapshots;
    /// expired affects are assumed to have been cleared by the legacy tick path.
    /// </summary>
    public static int GetEquipmentExperienceBonus(ReadOnlySpan<byte> mob, ReadOnlySpan<byte> affect, LegacyItemDataTable? itemData)
    {
        var bonus = 0;
        if (mob.Length >= LegacyAccountSnapshot.MobEquipmentOffset + (LegacyCharacterSelection.EquipmentCount * LegacyItem.SizeInBytes))
        {
            var fairy = LegacyItem.Read(mob.Slice(LegacyAccountSnapshot.MobEquipmentOffset + (13 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
            bonus += fairy.Index switch
            {
                3900 or 3903 or 3906 or 3911 or 3912 or 3913 => 16,
                3902 or 3904 or 3905 or 3907 or 3908 => 32,
                _ => 0,
            };

            if (itemData is not null)
            {
                for (var slot = 0; slot < LegacyCharacterSelection.EquipmentCount; slot++)
                {
                    var item = LegacyItem.Read(mob.Slice(LegacyAccountSnapshot.MobEquipmentOffset + (slot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
                    if (item.Index <= 0 || item.Index >= LegacyItemDataTable.MaxItemIndex)
                        continue;
                    if (itemData.GetItemGrade(item) == 7)
                        bonus += 2;
                    if (itemData.GetItemGem(item) == 2)
                        bonus += 2;
                }
            }
        }

        for (var offset = 0; offset + 8 <= affect.Length; offset += 8)
            if (affect[offset] == 39)
                bonus += 100;

        return bonus;
    }

    /// <summary>Ports the equipment-only portion of CMob::GetCurrentScore DropBonus.</summary>
    public static int GetEquipmentDropBonus(ReadOnlySpan<byte> mob, LegacyItemDataTable? itemData)
    {
        if (itemData is null || mob.Length < LegacyAccountSnapshot.MobEquipmentOffset + (LegacyCharacterSelection.EquipmentCount * LegacyItem.SizeInBytes))
            return 0;

        var bonus = 0;
        var fairy = LegacyItem.Read(mob.Slice(LegacyAccountSnapshot.MobEquipmentOffset + (13 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        bonus += fairy.Index switch
        {
            3901 => 32,
            3902 or 3905 or 3908 => 16,
            _ => 0,
        };

        for (var slot = 0; slot < LegacyCharacterSelection.EquipmentCount; slot++)
        {
            var item = LegacyItem.Read(mob.Slice(LegacyAccountSnapshot.MobEquipmentOffset + (slot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
            if (item.Index <= 0 || item.Index >= LegacyItemDataTable.MaxItemIndex)
                continue;
            if (itemData.GetItemGrade(item) == 5)
                bonus += 8;
            if (itemData.GetItemGem(item) == 0)
                bonus += 8;
        }

        return bonus;
    }

    public static int GetExpApply(LegacyExperienceEligibility eligibility, int experience, int attackerLevel, int targetLevel)
    {
        if (eligibility.ClassMaster == LegacyAccountSnapshot.ClassMasterArch && experience > 0)
        {
            if (attackerLevel >= 354 && !eligibility.ArchLevel355Completed)
                return 0;
            if (attackerLevel >= 369 && !eligibility.ArchLevel370Completed)
                return 0;

            experience = experience * 50 / 100;
        }
        else if (eligibility.ClassMaster == ClassMasterCelestial && experience > 0)
        {
            if (attackerLevel >= 39 && !eligibility.CelestialLevel40Completed)
                return 0;
            if (attackerLevel >= 89 && !eligibility.CelestialLevel90Completed)
                return 0;
        }

        if (eligibility.ClassMaster is ClassMasterCelestial or ClassMasterSCelestial or ClassMasterCelestialCs && experience > 0)
            attackerLevel = MaxLevel;

        if (targetLevel > MaxLevel + 1 || attackerLevel < 0 || targetLevel < 0)
            return experience;

        attackerLevel++;
        targetLevel++;
        var multiplier = (targetLevel * 100) / attackerLevel;
        if (multiplier < 80 && attackerLevel >= 50)
            multiplier = multiplier * 2 - 100;
        else if (multiplier > 200)
            multiplier = 200;

        if (multiplier < 0)
            multiplier = 0;

        return (experience * multiplier + 1) / 100;
    }
}

using WydCdk.Protocol;

namespace WydCdk.World;

public enum LegacySkillManaResult
{
    Accepted,
    ParticipantNotFound,
    InsufficientMana,
}

/// <summary>Pure combat formulas copied from the legacy server and kept independent of wire input.</summary>
public static class LegacySkillCombatMath
{
    public const int LegacyMaxLevel = 399;

    /// <summary>Matches the small class-0 master bonus used as combat in BASE_GetSkillDamage.</summary>
    public static int GetMasterCombat(LegacyMobCombatState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.CharacterClass != 0 || (state.LearnedSkill & 0x4000) == 0) return 0;
        return Math.Clamp(state.CurrentScore.Special3 / 20, 0, 15);
    }

    public static int GetSkillSpecial(LegacySkillDefinition skill, LegacyMobCombatState state)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(state);
        return GetSpecial(state.CurrentScore, skill.Id % 24 / 8 + 1);
    }

    /// <summary>
    /// Matches the WeaponDamage section of TMSrv/CMob.cpp. The two damage
    /// values are the already-resolved BASE_GetItemAbility(EF_DAMAGE) values
    /// for equipment slots 6 and 7; item-list parsing remains a separate step.
    /// </summary>
    public static int GetWeaponDamage(
        LegacyMobCombatState state,
        int firstWeaponDamage,
        int secondWeaponDamage,
        int firstWeaponPosition,
        int secondWeaponPosition,
        int firstWeaponSanctuary,
        int secondWeaponSanctuary)
    {
        ArgumentNullException.ThrowIfNull(state);

        var firstFraction = firstWeaponDamage / 2;
        var secondFraction = secondWeaponDamage / 2;
        var hasFullWeaponMastery = state.CharacterClass == 3 && (state.LearnedSkill & (1u << 10)) != 0
            || state.CharacterClass == 0 && (state.LearnedSkill & (1u << 9)) != 0;
        if (hasFullWeaponMastery)
        {
            firstFraction = firstWeaponDamage;
            secondFraction = secondWeaponDamage;
        }

        var weaponDamage = firstWeaponDamage >= secondWeaponDamage
            ? firstWeaponDamage + secondFraction
            : secondWeaponDamage + firstFraction;

        if (firstWeaponSanctuary >= 9 && firstWeaponPosition is 64 or 192)
            weaponDamage += 40;
        if (secondWeaponSanctuary >= 9 && secondWeaponPosition is 64 or 192)
            weaponDamage += 40;

        return weaponDamage;
    }

    public static int GetWeaponDamage(LegacyMobCombatState state, LegacyItemDataTable itemData, LegacyItem firstWeapon, LegacyItem secondWeapon)
    {
        ArgumentNullException.ThrowIfNull(itemData);

        return GetWeaponDamage(
            state,
            itemData.GetItemAbility(firstWeapon, LegacyItemEffect.Damage),
            itemData.GetItemAbility(secondWeapon, LegacyItemEffect.Damage),
            itemData.GetItemPosition(firstWeapon),
            itemData.GetItemPosition(secondWeapon),
            itemData.GetItemSanctuary(firstWeapon),
            itemData.GetItemSanctuary(secondWeapon));
    }

    /// <summary>
    /// Matches BASE_GetSkillDamage(int skillnum, STRUCT_MOB*, int weather,
    /// int weapondamage). Magic and weapon damage remain explicit inputs so
    /// the pure formula can receive values derived from the authoritative MOB
    /// and ItemList snapshot by the listener.
    /// </summary>
    public static int GetSkillBaseDamage(LegacySkillDefinition skill, LegacyMobCombatState state, int weather, int weaponDamage, int magic)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(state);

        var instanceType = skill.InstanceType;
        var skillKind = skill.Id % 24 / 8 + 1;
        var level = Math.Clamp(state.CurrentScore.Level, 0, LegacyMaxLevel);
        var special = GetSpecial(state.CurrentScore, skillKind);
        var baseDamage = skill.InstanceValue;
        var affectBase = skill.AffectValue;
        var damage = 0;

        if (instanceType == 0)
        {
            damage = skill.Id switch
            {
                11 => special / 10 + affectBase,
                13 => 3 * special / 4 + affectBase,
                41 => special / 25 + 2,
                43 => special / 3 + affectBase,
                44 => 2 * (3 * special / 20 + affectBase),
                45 => special / 10 + affectBase,
                _ => 0,
            };
        }
        else if (instanceType is >= 1 and <= 5)
        {
            var skillCategory = skill.Id / 8;
            damage = skill.Id == 97
                ? 15 * level + baseDamage
                : state.CharacterClass switch
                {
                    0 when skillCategory == 1 => 3 * weaponDamage + 3 * state.CurrentScore.Strength + level + special + baseDamage,
                    0 => special + baseDamage + weaponDamage + level + state.CurrentScore.Intelligence / 4 + state.CurrentScore.Intelligence / 40,
                    1 or 2 => state.CurrentScore.Intelligence / 30 + state.CurrentScore.Intelligence / 3 + level + baseDamage + 2 * special,
                    3 => 3 * weaponDamage + 3 * state.CurrentScore.Strength + level / 2 + special + baseDamage,
                    _ => 0,
                };

            if (weather == 1)
            {
                if (instanceType == 2) damage = 90 * damage / 100;
                if (instanceType == 5) damage = 130 * damage / 100;
            }
            else if (weather == 2 && instanceType == 3)
            {
                damage = 120 * damage / 100;
            }

            if ((state.CharacterClass != 0 || skillCategory != 1) && state.CharacterClass != 3)
            {
                damage = (4 * magic + 100) * damage / 100;
                damage = 5 * damage / 4;
            }
            else
            {
                damage = 5 * damage / 4;
            }
        }
        else if (instanceType == 6)
        {
            damage = 3 * special / 2 + baseDamage;
        }
        else if (instanceType == 11)
        {
            damage = baseDamage;
        }
        else
        {
            damage = magic;
        }

        // Tempestade de raios replaces the normal instance calculation.
        if (skill.Id == 79)
            damage = state.CurrentScore.Damage * 180 / 100;

        return damage;
    }

    public static int GetSkillBaseDamage(
        LegacySkillDefinition skill,
        LegacyMobCombatState state,
        int weather,
        int firstWeaponDamage,
        int secondWeaponDamage,
        int firstWeaponPosition,
        int secondWeaponPosition,
        int firstWeaponSanctuary,
        int secondWeaponSanctuary,
        int magic)
    {
        var weaponDamage = GetWeaponDamage(
            state,
            firstWeaponDamage,
            secondWeaponDamage,
            firstWeaponPosition,
            secondWeaponPosition,
            firstWeaponSanctuary,
            secondWeaponSanctuary);
        return GetSkillBaseDamage(skill, state, weather, weaponDamage, magic);
    }

    /// <summary>
    /// Matches BASE_GetSkillDamage(int dam, int ac, int combat). The legacy
    /// server obtains the random factor from rand(); keeping it as an input
    /// makes the arithmetic deterministic and testable without changing the
    /// formula or trusting a client-provided damage value.
    /// </summary>
    public static int GetSkillDamage(int damage, int armor, int combat, int randomFactor)
    {
        var result = damage - (armor / 2);
        combat = Math.Min(combat, 15);
        var delta = 21 - combat;
        if (randomFactor < combat + 90 || randomFactor >= combat + 90 + delta)
            throw new ArgumentOutOfRangeException(nameof(randomFactor), "Legacy skill damage random factor is outside the BASE_GetSkillDamage range.");

        result = result * randomFactor / 100;
        if (result < -50)
            result = 0;
        else if (result > -50 && result < 0)
        {
            result += 50;
            result /= 10;
        }
        else if (result >= 0 && result <= 45)
        {
            result = 5 * result / 4;
            result += 5;
        }

        return result <= 0 ? 1 : result;
    }

    public static int GetPhysicalDamage(int damage, int armor, int combat, int randomFactor)
    {
        long result = damage - (armor / 2);
        combat /= 2;
        if (combat > 7) combat = 7;
        var delta = 12 - combat;
        if (randomFactor < combat + 99 || randomFactor >= combat + 99 + delta)
            throw new ArgumentOutOfRangeException(nameof(randomFactor), "Legacy physical damage random factor is outside the BASE_GetDamage range.");

        result = result * randomFactor / 100;
        if (result < -50) result = 0;
        else if (result < 0)
        {
            result += 50;
            result /= 7;
        }
        else if (result <= 50)
        {
            result = (5 * result) / 4;
            result += 7;
        }

        return result <= 0 ? 1 : checked((int)result);
    }

    /// <summary>Matches TMSrv/GetFunc.cpp::GetParryRate, including its clamps and Rsv bonuses.</summary>
    public static int GetParryRate(int targetDex, int parry, int attackerDex, ushort attackerRsv)
    {
        parry = Math.Clamp(parry, 0, 100);
        var rawTargetDex = targetDex;
        targetDex = Math.Min(targetDex, 1000);
        var parryRate1 = Math.Clamp(rawTargetDex - 1000, 0, 2000);
        var parryRate2 = Math.Max(0, rawTargetDex - 3000);
        var parryRate = targetDex / 2 + parry + parryRate1 / 4 + parryRate2 / 8 - attackerDex;
        if ((attackerRsv & 0x20) != 0) parryRate += 100;
        if ((attackerRsv & 0x80) != 0) parryRate += 50;
        if ((attackerRsv & 0x200) != 0) parryRate += 50;
        return Math.Clamp(parryRate, 1, 650);
    }

    /// <summary>
    /// Matches BASE_GetManaSpent: integer division is intentionally performed in
    /// the same order as the C++ expression.
    /// </summary>
    public static int GetManaSpent(LegacySkillDefinition skill, LegacyMobCombatState state)
    {
        ArgumentNullException.ThrowIfNull(skill);

        // The legacy mana helper has a separate branch for the 96+ skills:
        // it uses level as Special instead of the spell's class bucket.
        var special = skill.Id < 96
            ? GetSpecial(state.CurrentScore, skill.Id % 24 / 8 + 1)
            : state.CurrentScore.Level;
        var manaSpent = skill.ManaSpent * (special / 2 + 100) / 100;
        return (100 - state.SaveMana) * manaSpent / 100;
    }

    private static int GetSpecial(LegacyScore score, int specialIndex)
    {
        return specialIndex switch
        {
            1 => score.Special1,
            2 => score.Special2,
            3 => score.Special3,
            _ => 0,
        };
    }
}

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

    /// <summary>
    /// The handler-local <c>Special</c> of TMProject 7.69 Exec_MSG_Attack, shared by mana, heal, affect and tick
    /// levels: <c>CurrentScore.Special[skill % 24 / 8 + 1]</c> for 0..95, Level for 96..100, zero for 101+.
    /// </summary>
    public static int GetSkillSpecial(LegacySkillDefinition skill, LegacyMobCombatState state)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(state);
        return skill.Id switch
        {
            >= 0 and < 96 => GetStageSpecial(state.CurrentScore, skill.Id % 24 / 8 + 1),
            >= 96 and <= 100 => state.CurrentScore.Level,
            _ => 0,
        };
    }

    /// <summary>
    /// Computes the handler-local delay passed to SetAffect/SetTick in Exec_MSG_Attack.
    /// Skill 102 starts from the common 100 default, then overrides it for Mortal and
    /// non-Mortal/non-Arch casters in both TMProject 7.69 and W2PP.
    /// </summary>
    public static int GetSkillAffectDelay(LegacySkillDefinition skill, LegacyMobCombatState state, int classMaster)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(state);

        var delay = 100 + GetSkillSpecial(skill, state);
        if (skill.Id != 102)
            return delay;

        if (classMaster == LegacyAccountSnapshot.ClassMasterMortal)
            return 200;

        if (classMaster == LegacyAccountSnapshot.ClassMasterArch)
            return delay;

        var level = state.CurrentScore.Level;
        if (level < 39)
            return 200 - ((39 - level) * 4);
        return level >= 199 ? 400 : 200;
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
    /// Matches TMProject 7.69 BASE_GetSkillDamage(int skillnum, STRUCT_MOB*, int weather, int weapondamage) from the
    /// common Basedef.cpp compiled by TMSrv (the W2PP formula is kept there only as a "Not Usage" region).
    /// <paramref name="magic"/> is STRUCT_MOB.Magic, declared <c>char</c> in the 7.69 common header, so it is
    /// narrowed to a signed byte. Weapon damage remains an explicit input derived from the authoritative MOB.
    /// </summary>
    public static int GetSkillBaseDamage(LegacySkillDefinition skill, LegacyMobCombatState state, int weather, int weaponDamage, int magic)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(state);

        if (skill.Id == 79)
            return state.CurrentScore.Damage;

        var mobMagic = unchecked((sbyte)magic);
        var instanceType = skill.InstanceType;
        var spell = skill.Id % 24;
        var stage = spell / 8 + 1;
        var readCount0 = spell >> 3;
        var readCount1 = Math.Clamp(state.CurrentScore.Level, 0, LegacyMaxLevel);
        var readCount2 = GetStageSpecial(state.CurrentScore, stage) + readCount0;
        var attack0 = skill.InstanceValue;
        var attack1 = skill.TickValue;
        var result = 0;

        if (instanceType == 0)
        {
            switch (skill.Id)
            {
                case 11: result = readCount2 / 10 + attack1; break;
                case 13: result = ((readCount2 * 3) >> 2) + attack1; break;
                case 41: result = readCount2 / 25 + 2; break;
                case 43: result = readCount2 / 3 + attack1; break;
                case 44: result = ((readCount2 * 3) / 20 + attack1) << 1; break;
                case 45: result = readCount2 / 10 + attack1; break;
                default: return result;
            }
            // The listed cases `break` out of the switch and reach the final `else Result = mob->Magic;` below.
        }

        if (instanceType is >= 1 and <= 5)
        {
            var local8 = skill.Id >> 3;
            if (skill.Id == 97)
                result = readCount1 * 15 + attack0;
            else if (state.CharacterClass == 0)
                result = attack0 + readCount2 + (readCount1 >> 1) + (state.CurrentScore.Intelligence / 2) + ((weaponDamage + 1) / 2);
            else if (state.CharacterClass is 1 or 2)
                result = attack0 + readCount2 + (readCount1 >> 1) + (state.CurrentScore.Intelligence / 2);
            else if (state.CharacterClass == 3)
                result = attack0 + readCount2 + (readCount1 >> 1) + (state.CurrentScore.Strength >> 1) + (weaponDamage * 3);

            // 7.69 keys the weather adjustment by skill number (2, 5, 3), not by instance type.
            if (weather == 1)
            {
                if (skill.Id == 2)
                    result = result * 90 / 100;
                else if (skill.Id == 5)
                    result = result * 130 / 100;
            }
            else if (weather == 2 && skill.Id == 3)
            {
                result = result * 120 / 100;
            }

            if (skill.Id == 97)
                return result;
            if (state.CharacterClass != 0 || local8 != 1)
            {
                if (state.CharacterClass == 3)
                    return (result * 5) >> 2;
                result = ((mobMagic * 2) + 100) * result / 100;
                result = (result * 5) >> 2;
            }
            else
            {
                return (result * 5) >> 2;
            }
        }
        else if (instanceType == 6)
        {
            return readCount2 * 3 + attack0;
        }
        else if (instanceType == 11)
        {
            result = attack0;
            return result;
        }
        else
        {
            result = mobMagic;
        }

        return result;
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
        // TMProject 7.69 Basedef.cpp: `dam - (ac >> 1)`, the -50..-1 band includes -50, and the low band is 0..50.
        var result = damage - (armor >> 1);
        combat = Math.Min(combat, 15);
        var delta = 21 - combat;
        if (delta <= 0)
            delta = 2;
        if (randomFactor < combat + 90 || randomFactor >= combat + 90 + delta)
            throw new ArgumentOutOfRangeException(nameof(randomFactor), "Legacy skill damage random factor is outside the BASE_GetSkillDamage range.");

        result = result * randomFactor / 100;
        if (result < -50)
            result = 0;
        else if (result >= -50 && result < 0)
            result = (result + 50) / 10;
        else if (result >= 0 && result <= 50)
            result = ((result * 5) >> 2) + 5;

        return result <= 0 ? 1 : result;
    }

    /// <summary>7.69 Exterminar adds current MP and half of Intelligence to the already-resisted skill damage.</summary>
    public static int AddExterminarManaBonus(int damage, int currentMana, int intelligence) =>
        unchecked((damage + currentMana) + (intelligence / 2));

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
        ArgumentNullException.ThrowIfNull(state);

        // TMProject 7.69 Exec_MSG_Attack passes its local Special: the class bucket for 0..95,
        // Level for the shared skills 96..100 and zero for 101+ (neither branch assigns it).
        var special = GetSkillSpecial(skill, state);
        return GetManaSpent(skill, state, special);
    }

    /// <summary>Matches BASE_GetManaSpent with an explicit handler-local Special value.</summary>
    public static int GetManaSpent(LegacySkillDefinition skill, LegacyMobCombatState state, int special)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(state);
        var manaSpent = skill.ManaSpent * (special / 2 + 100) / 100;
        return (100 - state.SaveMana) * manaSpent / 100;
    }

    /// <summary>7.69 Exec_MSG_Attack clears the weather for attackers in sectors x&lt;12 and y&gt;25.</summary>
    public static int GetSkillWeather(int currentWeather, int attackerX, int attackerY) =>
        attackerX / 128 < 12 && attackerY / 128 > 25 ? 0 : currentWeather;

    /// <summary>
    /// 7.69 InstanceType 9 target zones answered with _NN_Cant_Use_That_Here: Kefra (2335..2395, 3896..3955),
    /// RvR (1710..1755, 1968..1995), and the Zumbi (x/128==13 || y/128==16) and Coliseu N (x/128==27 || y/128==11)
    /// bands. The source uses `||` for the last two, blocking whole columns/rows.
    /// </summary>
    public static bool IsSummonTargetZoneBlocked(int targetX, int targetY) =>
        (targetX >= 2335 && targetY >= 3896 && targetX <= 2395 && targetY <= 3955) ||
        (targetX >= 1710 && targetY >= 1968 && targetX <= 1755 && targetY <= 1995) ||
        targetX / 128 == 13 || targetY / 128 == 16 ||
        targetX / 128 == 27 || targetY / 128 == 11;

    /// <summary>7.69 elemental defense: player targets double Ac, class 1 (Foema) targets add half.</summary>
    public static int GetSkillDefense(int targetAc, bool targetIsPlayer, int targetClass)
    {
        var defense = targetIsPlayer ? targetAc * 2 : targetAc;
        if (targetClass == 1)
            defense = (defense * 3) / 2;
        return defense;
    }

    /// <summary>
    /// 7.69 elemental resistance after BASE_GetSkillDamage(dam, def, master). InstanceType 1 uses ResistPoint
    /// (players: 10, plus 32/16/15 above level 354 by ClassMaster; NPCs: LearnedSkill / 1000) with
    /// <c>(100 - ResistPoint) * dam / 100</c>. InstanceType 2..5 (except skill 97) use the signed Resist[type-2],
    /// halved for players, with <c>dam * (200 - resist) / 100</c>.
    /// </summary>
    public static int ApplySkillResistance(
        int skillId,
        int instanceType,
        int damage,
        bool targetIsPlayer,
        int targetLevel,
        int targetClassMaster,
        uint targetLearnedSkill,
        ReadOnlySpan<byte> targetResist,
        out int resistance)
    {
        resistance = 0;
        if (instanceType == 1)
        {
            if (targetIsPlayer)
            {
                resistance = 10;
                if (targetLevel > 354)
                    resistance += targetClassMaster >= 3 ? 32 : targetClassMaster == 1 ? 16 : 15;
            }
            else
            {
                resistance = unchecked((int)(targetLearnedSkill / 1000));
            }

            return unchecked((100 - resistance) * damage / 100);
        }

        if (instanceType is >= 2 and <= 5 && skillId != 97)
        {
            resistance = unchecked((sbyte)targetResist[instanceType - 2]);
            if (targetIsPlayer)
                resistance /= 2;
            return unchecked(damage * (200 - resistance) / 100);
        }

        return damage;
    }

    /// <summary>
    /// 7.69 InstanceType 6 heal: <c>Special*2</c> (skill 27) or <c>Special*3</c> plus InstanceValue, doubled for
    /// ClassMaster other than MORTAL/ARCH; those reach 1100 at 1090+, the others cap at 638; a 1..5 heal becomes 6.
    /// </summary>
    public static int GetHealAmount(LegacySkillDefinition skill, LegacyMobCombatState attacker, int attackerClassMaster)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(attacker);
        var special = GetSkillSpecial(skill, attacker);
        var heal = skill.Id == 27 ? (special * 2) + skill.InstanceValue : (special * 3) + skill.InstanceValue;
        var ascended = attackerClassMaster != 2 && attackerClassMaster != 1;
        if (ascended)
        {
            heal *= 2;
            if (heal >= 1090)
                heal = 1100;
        }
        else if (heal >= 638)
        {
            heal = 638;
        }

        // `dam = -dam; if (dam < 0 && dam > -6) dam = -6;`
        if (heal > 0 && heal < 6)
            heal = 6;
        return heal;
    }

    /// <summary>
    /// <c>CurrentScore.Special[stage]</c> with stage = skill % 24 / 8 + 1 (1..3) in both 7.69 and W2PP.
    /// STRUCT_SCORE.Special[0] is <see cref="LegacyScore.Special1"/>, so stages map to Special2..Special4.
    /// </summary>
    private static int GetStageSpecial(LegacyScore score, int stage)
    {
        return stage switch
        {
            1 => score.Special2,
            2 => score.Special3,
            3 => score.Special4,
            _ => 0,
        };
    }
}

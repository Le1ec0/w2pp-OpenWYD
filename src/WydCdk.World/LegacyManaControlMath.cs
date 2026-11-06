namespace WydCdk.World;

/// <summary>Applies the TMSrv 7.69 physical Affect.Type 18 mana-control override.</summary>
public static class LegacyManaControlMath
{
    public static LegacyManaControlResult Resolve(
        int damage,
        int currentMana,
        int maxMana,
        bool hasAffect18,
        bool learnedManaSkill,
        byte doubleCriticalFlags,
        int secondaryDamage)
    {
        if (!hasAffect18 || currentMana <= (maxMana / 100) * 20)
            return new(false, damage, currentMana, secondaryDamage);

        var manaDamage = damage;
        var remainingMana = currentMana - manaDamage;
        if (remainingMana < 0)
        {
            manaDamage -= remainingMana;
            remainingMana = 0;
        }

        // SendSetHpMp calls SetReqMp, which clamps CurrentScore.Mp to MaxMp.
        if (remainingMana > maxMana)
            remainingMana = maxMana;

        var divisor = learnedManaSkill ? 55 : 60;
        var reducedDamage = unchecked(((manaDamage >> 1) + (manaDamage << 4)) / divisor);
        var finalSecondaryDamage = (doubleCriticalFlags & 4) != 0 ? reducedDamage : secondaryDamage;
        return new(true, reducedDamage, remainingMana, finalSecondaryDamage);
    }
}

public readonly record struct LegacyManaControlResult(
    bool Applied,
    int Damage,
    int CurrentMana,
    int SecondaryDamage);

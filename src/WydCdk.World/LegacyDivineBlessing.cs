namespace WydCdk.World;

/// <summary>Server-only descriptors for the TMProject 7.69 Divine Blessing action.</summary>
public static class LegacyDivineBlessing
{
    public const int SkillId = 201;
    public const int HealSkillId = 29;

    /// <summary>
    /// Applies the 7.69 DivineBlessing Type 6 amount and mount divisor. The signed slot damage is the original
    /// <c>-RateRegen</c>, while CurrentHp uses integer division by the target mount sanction.
    /// </summary>
    public static LegacyDivineBlessingHealResult CalculateHeal(int currentHp, int maxHp, int mountIndex, int mountSanction)
    {
        var rateRegen = maxHp - currentHp;
        var wireDamage = -rateRegen;
        if (wireDamage < 0 && wireDamage > -6)
            wireDamage = -6;

        var divisor = mountIndex switch
        {
            786 => Math.Max(2, mountSanction),
            1936 => Math.Max(2, mountSanction) * 10,
            1937 => Math.Max(2, mountSanction) * 1000,
            _ => 1,
        };
        var healedHp = currentHp - wireDamage / divisor;
        if (healedHp > maxHp)
            healedHp = maxHp;

        return new LegacyDivineBlessingHealResult(healedHp, wireDamage);
    }

    /// <summary>
    /// A fresh zero-initialized parent entry: the 7.69 handler recognizes skill 201 explicitly,
    /// while no SkillData.csv row is loaded for it.
    /// </summary>
    public static LegacySkillDefinition ParentActionDefinition => new(
        SkillId,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        new byte[8],
        new byte[8],
        0, 0, 0, 0, 0, 0, 0, 0,
        string.Empty);
}

public sealed record LegacyDivineBlessingHealResult(int CurrentHp, int WireDamage);

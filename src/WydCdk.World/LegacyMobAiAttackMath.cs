namespace WydCdk.World;

/// <summary>Pure damage helpers for the source-backed generated-mob attack subset.</summary>
public static class LegacyMobAiAttackMath
{
    /// <summary>
    /// Applies TMProject 7.69 GetAttack's unsigned MOB.Resist factor when the selected SkillBar entry
    /// identifies an elemental attack; a negative or out-of-range index leaves ordinary damage intact.
    /// </summary>
    public static int ApplyElementalResistance(int damage, int resistanceIndex, ReadOnlySpan<byte> targetResist)
    {
        if (resistanceIndex is < 0 or > 3)
            return damage;
        if (targetResist.Length <= resistanceIndex)
            throw new ArgumentException("The target MOB resistance array does not contain the selected element.", nameof(targetResist));

        return unchecked((200 - targetResist[resistanceIndex]) * damage / 100);
    }
}

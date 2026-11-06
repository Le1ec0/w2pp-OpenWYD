namespace WydCdk.World;

/// <summary>Target-7.69 knockback chance for Fúria Divina (skill 6).</summary>
public static class LegacyFuryDivineMath
{
    public const int SkillId = 6;
    public const int MaximumChancePercent = 50;

    /// <summary>
    /// Matches the skill-6 branch in TMProject 7.69 <c>Exec_MSG_Attack</c>.
    /// Its target-index comparison is strictly <c>targetConnectionId &gt; MAX_USER</c>.
    /// </summary>
    public static int GetKnockbackChancePercent(
        int targetConnectionId,
        short attackerSpecial1,
        int attackerLevel,
        short attackerClassMaster,
        int targetLevel,
        short targetClassMaster)
    {
        var isNpcIndex = targetConnectionId > LegacyBattleRoyaleGridRefreshRelay.MaxUserConnectionExclusive;
        var chanceBase = isNpcIndex
            ? (attackerSpecial1 / 5) + 40
            : (attackerSpecial1 / 10) + 20;

        var attackerIsMortalOrArch = attackerClassMaster is LegacyAccountSnapshot.ClassMasterMortal or LegacyAccountSnapshot.ClassMasterArch;
        var targetIsMortalOrArch = targetClassMaster is LegacyAccountSnapshot.ClassMasterMortal or LegacyAccountSnapshot.ClassMasterArch;
        var levelDifference = attackerIsMortalOrArch
            ? attackerLevel + LegacyExperienceMath.MaxLevel - (targetIsMortalOrArch ? targetLevel : targetLevel + LegacyExperienceMath.MaxLevel)
            : attackerLevel;
        levelDifference >>= 2;

        return Math.Min(chanceBase + levelDifference, MaximumChancePercent);
    }

    public static bool IsKnockbackSuccessful(int chancePercent, int roll)
    {
        if (roll is < 0 or > 99)
            throw new ArgumentOutOfRangeException(nameof(roll), roll, "The 7.69 Furia Divina chance roll must be between 0 and 99.");

        return roll < chancePercent;
    }
}

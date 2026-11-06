namespace WydCdk.World;

public enum LegacySkillCoinResult
{
    Accepted,
    ParticipantNotFound,
    InsufficientCoin,
}

/// <summary>Pure fee formula for skill 85 (Golden Shield) in TMProject 7.69 and W2PP.</summary>
public static class LegacyGoldenShieldCoinMath
{
    public const int SkillId = 85;
    public const int CoinPerStageLevel = 100;

    /// <summary>
    /// Both handlers charge 100 times the local <c>Level</c>, which for skill 85 is
    /// <c>CurrentScore.Special[2]</c> (represented by C# <c>Special3</c>).
    /// </summary>
    public static int GetCost(LegacySkillDefinition skill, LegacyMobCombatState state)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(state);
        return skill.Id == SkillId
            ? CoinPerStageLevel * LegacySkillCombatMath.GetSkillSpecial(skill, state)
            : 0;
    }
}

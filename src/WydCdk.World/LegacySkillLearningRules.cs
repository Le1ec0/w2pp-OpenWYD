namespace WydCdk.World;

/// <summary>Pure gates and index mapping used by the 7.69 BonusType=2 skill-tree branch.</summary>
public static class LegacySkillLearningRules
{
    public const int FirstDetail = 5000;
    public const int LastDetail = 5095;
    public const int SkillClassCount = 4;
    public const int SkillsPerClass = 24;
    public const int FirstMobId = 1000;
    public const int EndMobId = 25000;

    private const uint FinalSkillMask = (1u << 7) | (1u << 15) | (1u << 23);

    public static bool TryDecodeDetail(int detail, out int skillClass, out int skillPosition, out int skillId)
    {
        skillClass = 0;
        skillPosition = 0;
        skillId = 0;
        if (detail is < FirstDetail or > LastDetail) return false;

        var localDetail = detail - FirstDetail;
        skillClass = localDetail / SkillsPerClass;
        skillPosition = localDetail % SkillsPerClass;
        skillId = skillPosition + (SkillsPerClass * skillClass);
        return skillClass < SkillClassCount;
    }

    public static bool IsSkillMasterTarget(int targetId) => targetId >= FirstMobId && targetId < EndMobId;

    public static int AvailableSkillPoints(short classMaster, short storedSkillBonus) =>
        classMaster is LegacyExperienceMath.ClassMasterCelestial or
            LegacyExperienceMath.ClassMasterCelestialCs or LegacyExperienceMath.ClassMasterSCelestial
            ? 1500
            : storedSkillBonus;

    public static bool IsFinalSkill(int skillPosition) => skillPosition is 7 or 15 or 23;

    public static bool HasAnyFinalSkill(uint learnedSkills) => (learnedSkills & FinalSkillMask) != 0;

    /// <summary>
    /// The legacy server counts the requested final-skill bit and the previous
    /// seven positions in its own eight-skill branch; exactly seven must exist.
    /// </summary>
    public static bool HasSevenBranchPrerequisites(uint learnedSkills, int finalSkillPosition)
    {
        if (!IsFinalSkill(finalSkillPosition)) return true;

        var learnedInBranch = 0;
        for (var offset = 0; offset < 8; offset++)
        {
            var position = finalSkillPosition - offset;
            if (position >= 0 && (learnedSkills & (1u << position)) != 0)
                learnedInBranch++;
        }

        return learnedInBranch == 7;
    }

    public static int FinalSkillCoinCost(short classMaster) => classMaster switch
    {
        LegacyAccountSnapshot.ClassMasterMortal => 10_000_000,
        LegacyAccountSnapshot.ClassMasterArch => 50_000_000,
        _ => 100_000_000,
    };
}

public enum LegacySkillBonusResult
{
    Applied,
    ParticipantNotFound,
    CharacterStateUnavailable,
    NotAlive,
    InvalidDetail,
    InvalidTarget,
    ClassMismatch,
    SkillDataUnavailable,
    ItemDataUnavailable,
    InsufficientSkillPoints,
    OnlyOneFinalSkill,
    MissingPrerequisites,
    InsufficientCoin,
    AlreadyLearned,
    RequiredLevel,
    RequiredAttributes,
}

public sealed record LegacySkillBonusOutcome(
    byte[] Mob,
    byte[] Affect,
    byte[] MobExtra,
    bool SendScore,
    bool SendEtc);

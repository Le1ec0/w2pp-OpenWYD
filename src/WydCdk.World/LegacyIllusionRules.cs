namespace WydCdk.World;

public static class LegacyIllusionRules
{
    public const byte RequiredCharacterClass = 3;
    public const uint RequiredLearnedSkillBit = 2;
    public const int ManaCostSkillId = 73;
    public const int LegacyMaxLevel = 399;

    public static bool ShouldRecordEligibilityError(byte characterClass, uint learnedSkill, int level)
        => (characterClass != RequiredCharacterClass || (learnedSkill & RequiredLearnedSkillBit) == 0) && level < LegacyMaxLevel;
}

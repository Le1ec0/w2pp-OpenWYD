using WydCdk.Protocol;

namespace WydCdk.World;

public enum SkillAttackValidationResult
{
    Accepted,
    InvalidSkillId,
    MissingSkillDefinition,
    PassiveSkill,
    WrongClass,
    SkillNotLearned,
    InvalidTargetIndex,
    TooManyTargets,
}

/// <summary>
/// Validates the skill metadata checks that precede damage in TMSrv's attack handler.
/// It deliberately does not calculate damage, consume mana, or mutate HP.
/// </summary>
public static class LegacySkillAttackGate
{
    public static SkillAttackValidationResult Validate(
        LegacySkillDataTable table,
        AttackRequest request,
        int characterClass,
        uint learnedSkill,
        int targetIndex,
        bool skipClientChecks = false)
    {
        ArgumentNullException.ThrowIfNull(table);

        var skillId = request.SkillIndex;
        if (skillId < 0 || skillId >= LegacySkillDataTable.MaxSkillIndex)
            return SkillAttackValidationResult.InvalidSkillId;

        var definition = table[skillId];
        if (definition is null)
            return SkillAttackValidationResult.MissingSkillDefinition;

        if (targetIndex < 0)
            return SkillAttackValidationResult.InvalidTargetIndex;
        if (targetIndex > definition.MaxTarget)
            return SkillAttackValidationResult.TooManyTargets;

        // SKIPCHECKTICK is an internal server timestamp. The outer protocol gate
        // rejects it from clients; this flag exists only for legacy server paths.
        if (skipClientChecks)
            return SkillAttackValidationResult.Accepted;

        if (definition.Passive == 1)
            return SkillAttackValidationResult.PassiveSkill;

        if (skillId < 96 && skillId / 24 != characterClass)
            return SkillAttackValidationResult.WrongClass;

        var learnedBit = GetLearnedSkillBit(skillId);
        if (learnedBit < 0 || (learnedSkill & (1u << learnedBit)) == 0)
            return SkillAttackValidationResult.SkillNotLearned;

        return SkillAttackValidationResult.Accepted;
    }

    private static int GetLearnedSkillBit(int skillId) =>
        skillId is >= 0 and < 96 ? skillId % 24 : skillId is >= 96 and <= 103 ? skillId - 72 : -1;
}

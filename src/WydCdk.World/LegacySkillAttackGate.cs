using WydCdk.Protocol;

namespace WydCdk.World;

public enum SkillAttackValidationResult
{
    Accepted,
    InvalidSkillId,
    MissingSkillDefinition,
    PassiveSkill,
    SkillDelayTooShort,
    WrongClass,
}

/// <summary>
/// Nonterminal AddCrackError side effect of the learned-skill checks: the legacy handler returns only
/// when AddCrackError reports the logout threshold, otherwise the attack continues.
/// </summary>
public sealed record SkillAttackCrackError(int Points, int Type);

public sealed record SkillAttackGateDecision(
    SkillAttackValidationResult Result,
    bool IsPhysical = false,
    SkillAttackCrackError? LearnedSkillError = null,
    bool ClearClassOneType6Affect = false,
    bool RequiresMortarItem = false)
{
    public bool IsAccepted => Result == SkillAttackValidationResult.Accepted;
}

/// <summary>
/// Ports the skill block of TMProject 7.69 <c>Exec_MSG_Attack</c> that follows the timing gates and precedes
/// Escudo Dourado/mana: Passive, the 700 ms skill delay, other-class skills and learned-skill bits.
/// SkillIndex -1 is the client's physical attack and skips the whole block. It does not consume mana,
/// calculate damage or mutate the MOB.
/// </summary>
public static class LegacySkillAttackGate
{
    public const int PhysicalSkillIndex = -1;
    public const int SkillDelayMinimumMilliseconds = 700;
    public const int SkillDelayPoints = 10;
    public const int SkillDelayType = 30;
    public const int ClassSkillNotLearnedPoints = 8;
    public const int ClassSkillNotLearnedType = 10;
    public const int SharedSkillNotLearnedPoints = 208;
    public const int SharedSkillNotLearnedType = 1;
    public const int MortarSkillIndex = 97;

    /// <param name="previousAttackTick">
    /// The handler-local copy of LastAttackTick: zero when the session had no previous attack,
    /// otherwise the previous accepted attack tick.
    /// </param>
    public static SkillAttackGateDecision Evaluate(
        LegacySkillDataTable table,
        int skillIndex,
        int characterClass,
        uint learnedSkill,
        uint clientTick,
        uint previousAttackTick)
    {
        ArgumentNullException.ThrowIfNull(table);

        // The 7.69 client sends -1 for a physical attack; the skill block and mana block both require skill >= 0.
        if (skillIndex == PhysicalSkillIndex)
            return new SkillAttackGateDecision(SkillAttackValidationResult.Accepted, IsPhysical: true);

        // Other negative or >= MAX_SKILLINDEX values are not produced by the client; fail closed.
        if (skillIndex < 0 || skillIndex >= LegacySkillDataTable.MaxSkillIndex)
            return new SkillAttackGateDecision(SkillAttackValidationResult.InvalidSkillId);

        var definition = table[skillIndex];
        var isDivineBlessing = skillIndex == LegacyDivineBlessing.SkillId;
        if (definition is null && !isDivineBlessing)
            return new SkillAttackGateDecision(SkillAttackValidationResult.MissingSkillDefinition);

        if (definition?.Passive == 1)
            return new SkillAttackGateDecision(SkillAttackValidationResult.PassiveSkill);

        // TMProject 7.69 clears the first Type 6 affect for class 1 immediately after
        // the passive-skill return, before delay, class, and learned-skill gates.
        var clearClassOneType6Affect = characterClass == 1 && clientTick != ClientTickPolicy.SkipCheckTick;
        SkillAttackGateDecision Decision(
            SkillAttackValidationResult result,
            SkillAttackCrackError? error = null,
            bool requiresMortarItem = false) =>
            new(result,
                LearnedSkillError: error,
                ClearClassOneType6Affect: clearClassOneType6Affect,
                RequiresMortarItem: requiresMortarItem);

        // `LastAttackTick != -1` compares the unsigned local copy; isTime is a signed int difference.
        if (previousAttackTick != uint.MaxValue &&
            unchecked((int)(clientTick - previousAttackTick)) < SkillDelayMinimumMilliseconds)
            return Decision(SkillAttackValidationResult.SkillDelayTooShort);

        if (isDivineBlessing)
            return Decision(SkillAttackValidationResult.Accepted);

        // `(skillnum / 24) <= 3` limits the class check to the four class books (0..95).
        if (skillIndex / 24 <= 3 && skillIndex / 24 != characterClass)
            return Decision(SkillAttackValidationResult.WrongClass);

        if (skillIndex < 96)
        {
            return HasLearnedBit(learnedSkill, skillIndex % 24)
                ? Decision(SkillAttackValidationResult.Accepted)
                : Decision(SkillAttackValidationResult.Accepted,
                    new SkillAttackCrackError(ClassSkillNotLearnedPoints, ClassSkillNotLearnedType));
        }

        if (skillIndex <= 100)
        {
            // TMProject 7.69's skill-97 branch validates the mortar instead of checking a learned bit.
            if (skillIndex == MortarSkillIndex)
                return Decision(SkillAttackValidationResult.Accepted, requiresMortarItem: true);

            return HasLearnedBit(learnedSkill, skillIndex - 72)
                ? Decision(SkillAttackValidationResult.Accepted)
                : Decision(SkillAttackValidationResult.Accepted,
                    new SkillAttackCrackError(SharedSkillNotLearnedPoints, SharedSkillNotLearnedType));
        }

        // 7.69 has no learned-skill or class check for 101..MAX_SKILLINDEX-1.
        return Decision(SkillAttackValidationResult.Accepted);
    }

    private static bool HasLearnedBit(uint learnedSkill, int bit) => (learnedSkill & (1u << bit)) != 0;
}

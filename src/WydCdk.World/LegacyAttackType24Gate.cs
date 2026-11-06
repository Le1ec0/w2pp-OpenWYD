namespace WydCdk.World;

/// <summary>Models the 7.69/W2PP protection against an extra physical target in a short two-target frame.</summary>
public static class LegacyAttackType24Gate
{
    public const int CrackErrorPoints = 10;
    public const int CrackErrorType = 24;
    public const byte ExemptCharacterClass = 3;
    public const uint ExemptLearnedSkillBit = 0x40;

    public static bool ShouldRejectSecondaryPhysicalTarget(
        int slotIndex,
        bool shortTwoTargetPacket,
        byte attackerClass,
        uint learnedSkill) =>
        slotIndex > 0 &&
        shortTwoTargetPacket &&
        attackerClass != ExemptCharacterClass &&
        (learnedSkill & ExemptLearnedSkillBit) == 0;
}

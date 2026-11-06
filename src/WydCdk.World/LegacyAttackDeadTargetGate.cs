namespace WydCdk.World;

/// <summary>Models the common dead-target branch in TMProject 7.69 MSG_Attack.</summary>
public static class LegacyAttackDeadTargetGate
{
    public const int RenascimentoSkillId = 31;
    public const int ResurrectionSkillId = 99;

    public static bool ShouldClearTargetSlot(int targetHp, int skillIndex) =>
        targetHp <= 0 && skillIndex is not RenascimentoSkillId and not ResurrectionSkillId;
}

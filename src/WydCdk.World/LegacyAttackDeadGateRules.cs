namespace WydCdk.World;

/// <summary>Legacy <c>Exec_MSG_Attack</c> gate for an exactly-zero-HP attacker.</summary>
public static class LegacyAttackDeadGateRules
{
    public const short ResurrectionSkillIndex = 99;

    public static bool ShouldReject(int currentHp, short skillIndex)
        => currentHp == 0 && skillIndex != ResurrectionSkillIndex;
}

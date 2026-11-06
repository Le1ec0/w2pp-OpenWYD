namespace WydCdk.World;

/// <summary>Models the 7.69 MSG_Attack view-window cleanup and skill 42 exception.</summary>
public static class LegacyAttackViewGate
{
    public const int ViewGridX = 33;
    public const int ViewGridY = 33;
    public const int ViewExemptSkillIndex = 42;

    public static bool ShouldSendRemoveMob(int attackerX, int attackerY, int targetX, int targetY, int skillIndex) =>
        skillIndex != ViewExemptSkillIndex &&
        (attackerX < targetX - ViewGridX || attackerX > targetX + ViewGridX ||
         attackerY < targetY - ViewGridY || attackerY > targetY + ViewGridY);
}

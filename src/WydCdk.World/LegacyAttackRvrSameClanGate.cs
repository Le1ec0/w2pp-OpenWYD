namespace WydCdk.World;

/// <summary>7.69 MSG_Attack protection against same-Clan player damage during active RvR.</summary>
public static class LegacyAttackRvrSameClanGate
{
    public const int MinimumX = 1_679;
    public const int MinimumY = 1_940;
    public const int MaximumX = 1_774;
    public const int MaximumY = 2_023;

    public static bool ShouldClearTargetId(
        int attackerConnectionId,
        int targetConnectionId,
        int maxUserConnectionExclusive,
        int targetX,
        int targetY,
        bool rvrWarActive,
        byte attackerClan,
        byte targetClan,
        int damage) =>
        attackerConnectionId < maxUserConnectionExclusive &&
        targetConnectionId < maxUserConnectionExclusive &&
        rvrWarActive &&
        targetX >= MinimumX && targetX <= MaximumX &&
        targetY >= MinimumY && targetY <= MaximumY &&
        attackerClan == targetClan &&
        damage > 0;
}

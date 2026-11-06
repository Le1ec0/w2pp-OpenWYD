namespace WydCdk.World;

/// <summary>Computes the healing contribution sent in the 7.69 MSG_Attack response.</summary>
public static class LegacyAttackHealExperienceMath
{
    public static int? GetContribution(
        int attackerConnectionId,
        int targetConnectionId,
        int targetX,
        int targetY,
        int previousHp,
        int currentHp)
    {
        if (attackerConnectionId <= 0 || attackerConnectionId == targetConnectionId)
            return null;

        // TMSrv's BASE_GetVillage uses the same five inclusive city rectangles as FindGuildZone.
        if (LegacyMovementMapRules.FindGuildZone(targetX, targetY) < LegacyGuildZoneState.ZoneCount)
            return null;

        var experience = (currentHp - previousHp) >> 3;
        return Math.Clamp(experience, 0, 120);
    }

    public static int? ApplyDoubleModeAndCap(int maximumContribution, int doubleModeMultiplier)
    {
        if (maximumContribution <= 0)
            return null;

        var experience = (long)maximumContribution * doubleModeMultiplier;
        if (experience <= 0)
            return 0;

        return (int)Math.Min(200L, experience);
    }
}

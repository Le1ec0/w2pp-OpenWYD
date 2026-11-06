namespace WydCdk.World;

/// <summary>
/// Resolves the NPC target gates in TMProject 7.69/W2PP <c>_MSG_Sell</c>.
/// The special class/skill mask is retained as a raw wire-era condition; no
/// skill name or UI crosswalk is inferred.
/// </summary>
public static class LegacyNpcSellTargetRules
{
    private const int MaximumUserId = 1000;
    private const int MaximumMobId = 25000;
    private const int ClassThreeMerchantBypassMask = 0x1000;

    public static LegacyNpcSellTargetResolution Resolve(
        ushort targetId,
        int playerClass,
        int learnedSkillWord0,
        byte targetMerchant,
        short sellerX,
        short sellerY,
        short targetX,
        short targetY)
    {
        // The legacy skill branch skips its target range check, then indexes
        // pMob[targetId]. Keep malformed wire IDs outside that table unsafe to
        // proceed with even when the merchant checks would otherwise be skipped.
        if (targetId >= MaximumMobId)
            return new(LegacyNpcSellTargetStatus.TargetIdOutsideMobTable);

        if (playerClass == 3 && (learnedSkillWord0 & ClassThreeMerchantBypassMask) != 0)
            return new(LegacyNpcSellTargetStatus.MerchantChecksBypassedBySkill);

        if (targetId < MaximumUserId)
            return new(LegacyNpcSellTargetStatus.NotNpcTargetId);

        if (targetMerchant != 1)
            return new(LegacyNpcSellTargetStatus.NotMerchant);

        if (!IsInView(sellerX, sellerY, targetX, targetY))
            return new(LegacyNpcSellTargetStatus.TargetNotInView);

        return new(LegacyNpcSellTargetStatus.Accepted);
    }

    /// <summary>
    /// Reproduces 7.69/W2PP <c>GetInView</c>: both coordinates may differ by
    /// at most VIEWGRIDX/VIEWGRIDY (33), inclusive.
    /// </summary>
    public static bool IsInView(short sellerX, short sellerY, short targetX, short targetY) =>
        sellerX >= targetX - LegacyMovementRules.ViewGridX &&
        sellerX <= targetX + LegacyMovementRules.ViewGridX &&
        sellerY >= targetY - LegacyMovementRules.ViewGridY &&
        sellerY <= targetY + LegacyMovementRules.ViewGridY;
}

public enum LegacyNpcSellTargetStatus
{
    Accepted,
    MerchantChecksBypassedBySkill,
    TargetIdOutsideMobTable,
    NotNpcTargetId,
    NotMerchant,
    TargetNotInView,
}

public readonly record struct LegacyNpcSellTargetResolution(LegacyNpcSellTargetStatus Status)
{
    public bool IsAccepted => Status is LegacyNpcSellTargetStatus.Accepted or LegacyNpcSellTargetStatus.MerchantChecksBypassedBySkill;
}

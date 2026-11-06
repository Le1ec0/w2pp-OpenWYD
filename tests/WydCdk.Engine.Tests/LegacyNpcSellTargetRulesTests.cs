using WydCdk.World;

internal static class LegacyNpcSellTargetRulesTests
{
    public static void Run()
    {
        NpcTargetIdRangeMatchesLegacy();
        MerchantAndViewGatesRunInOrder();
        ViewGridIsInclusiveOnBothAxes();
        ClassThreeSkillMaskBypassesMerchantGates();
        OutOfTableIdIsRejectedBeforeSkillBypass();
    }

    private static void NpcTargetIdRangeMatchesLegacy()
    {
        Check(Resolve(1000).Status == LegacyNpcSellTargetStatus.Accepted &&
              Resolve(24999).Status == LegacyNpcSellTargetStatus.Accepted,
            "The inclusive NPC target ID bounds were not accepted.");
        Check(Resolve(999).Status == LegacyNpcSellTargetStatus.NotNpcTargetId,
            "A player-range target ID passed the ordinary merchant path.");
    }

    private static void MerchantAndViewGatesRunInOrder()
    {
        Check(Resolve(1000, merchant: 0, targetX: 2034).Status == LegacyNpcSellTargetStatus.NotMerchant,
            "The exact Merchant==1 gate did not precede the view gate.");
        Check(Resolve(1000, merchant: 2).Status == LegacyNpcSellTargetStatus.NotMerchant,
            "A non-one Merchant byte was accepted.");
        Check(Resolve(1000, targetX: 2034).Status == LegacyNpcSellTargetStatus.TargetNotInView,
            "An out-of-view merchant was accepted.");
    }

    private static void ViewGridIsInclusiveOnBothAxes()
    {
        Check(LegacyNpcSellTargetRules.IsInView(2000, 2000, 2033, 1967),
            "The inclusive VIEWGRIDX/VIEWGRIDY corner was rejected.");
        Check(!LegacyNpcSellTargetRules.IsInView(2000, 2000, 2034, 2000) &&
              !LegacyNpcSellTargetRules.IsInView(2000, 2000, 2000, 1966),
            "A target beyond the 33-cell view rectangle was accepted.");
    }

    private static void ClassThreeSkillMaskBypassesMerchantGates()
    {
        var bypassed = Resolve(1, playerClass: 3, learnedSkillWord0: 0x1000, merchant: 0, targetX: 2034);
        Check(bypassed.Status == LegacyNpcSellTargetStatus.MerchantChecksBypassedBySkill && bypassed.IsAccepted,
            "Class 3 with LearnedSkill bit 0x1000 did not bypass target range, merchant and view gates.");

        Check(Resolve(1000, playerClass: 2, learnedSkillWord0: 0x1000).Status == LegacyNpcSellTargetStatus.Accepted &&
              Resolve(1, playerClass: 2, learnedSkillWord0: 0x1000).Status == LegacyNpcSellTargetStatus.NotNpcTargetId &&
              Resolve(1, playerClass: 3, learnedSkillWord0: 0x2000).Status == LegacyNpcSellTargetStatus.NotNpcTargetId &&
              Resolve(1, playerClass: 3, learnedSkillWord0: 0).Status == LegacyNpcSellTargetStatus.NotNpcTargetId,
            "The merchant-target bypass escaped its exact class/bit gate.");
    }

    private static void OutOfTableIdIsRejectedBeforeSkillBypass()
    {
        var result = Resolve(25000, playerClass: 3, learnedSkillWord0: 0x1000);
        Check(result.Status == LegacyNpcSellTargetStatus.TargetIdOutsideMobTable && !result.IsAccepted,
            "An out-of-table target ID reached the legacy skill bypass.");
    }

    private static LegacyNpcSellTargetResolution Resolve(
        ushort targetId,
        int playerClass = 0,
        int learnedSkillWord0 = 0,
        byte merchant = 1,
        short sellerX = 2000,
        short sellerY = 2000,
        short targetX = 2000,
        short targetY = 2000) =>
        LegacyNpcSellTargetRules.Resolve(targetId, playerClass, learnedSkillWord0, merchant, sellerX, sellerY, targetX, targetY);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

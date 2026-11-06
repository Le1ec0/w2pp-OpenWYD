namespace WydCdk.World;

/// <summary>Models the common protected-Merchant branch in TMProject 7.69 MSG_Attack.</summary>
public static class LegacyAttackMerchantGate
{
    public static bool ShouldClearTargetSlot(byte merchant) => merchant is 1 or 4 or 43 or 100;
}

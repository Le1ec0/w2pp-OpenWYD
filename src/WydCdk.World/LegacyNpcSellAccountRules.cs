namespace WydCdk.World;

/// <summary>
/// Models the account-operation block in TMProject 7.69 <c>_MSG_Sell</c>.
/// W2PP omits this gate, so the 7.69 release contract is authoritative here.
/// </summary>
public static class LegacyNpcSellAccountRules
{
    public static LegacyNpcSellAccountResolution Resolve(bool isBlocked) =>
        isBlocked
            ? new(LegacyNpcSellAccountStatus.Blocked)
            : new(LegacyNpcSellAccountStatus.ContinueSelling);
}

public enum LegacyNpcSellAccountStatus
{
    ContinueSelling,
    Blocked,
}

public readonly record struct LegacyNpcSellAccountResolution(LegacyNpcSellAccountStatus Status)
{
    public bool CanContinueSelling => Status == LegacyNpcSellAccountStatus.ContinueSelling;

    public bool RequiresBlockedAccountNotice => Status == LegacyNpcSellAccountStatus.Blocked;
}

namespace WydCdk.World;

/// <summary>Models the opening HP and USER_PLAY gates in 7.69/W2PP <c>_MSG_Sell</c>.</summary>
public static class LegacyNpcSellHpModeRules
{
    public const int CrackErrorPoints = 10;
    public const int CrackErrorType = 23;

    public static LegacyNpcSellHpModeResolution Resolve(int currentHp, LoginSessionState connectionState)
    {
        if (currentHp == 0 || connectionState != LoginSessionState.Playing)
            return new(LegacyNpcSellHpModeStatus.DeadOrNotPlaying);

        return new(LegacyNpcSellHpModeStatus.Ready);
    }
}

public enum LegacyNpcSellHpModeStatus
{
    Ready,
    DeadOrNotPlaying,
}

public readonly record struct LegacyNpcSellHpModeResolution(LegacyNpcSellHpModeStatus Status)
{
    public bool IsReady => Status == LegacyNpcSellHpModeStatus.Ready;

    public bool RequiresHpModeSync => Status == LegacyNpcSellHpModeStatus.DeadOrNotPlaying;

    public bool RequiresCrackError => Status == LegacyNpcSellHpModeStatus.DeadOrNotPlaying;
}

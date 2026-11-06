using WydCdk.World;

internal static class LegacyNpcSellHpModeRulesTests
{
    public static void Run()
    {
        ZeroHpRejectsAndRequestsLegacyResponses();
        OnlyPlayingStatePassesTheModeGate();
        NegativeHpPreservesTheExactLegacyComparison();
    }

    private static void ZeroHpRejectsAndRequestsLegacyResponses()
    {
        var result = Resolve(0, LoginSessionState.Playing);

        Check(result.Status == LegacyNpcSellHpModeStatus.DeadOrNotPlaying &&
              result.RequiresHpModeSync && result.RequiresCrackError,
            "The zero-HP gate did not request both legacy rejection responses.");
        Check(LegacyNpcSellHpModeRules.CrackErrorPoints == 10 &&
              LegacyNpcSellHpModeRules.CrackErrorType == 23,
            "The _MSG_Sell crack-error arguments differ from 7.69/W2PP.");
    }

    private static void OnlyPlayingStatePassesTheModeGate()
    {
        foreach (var state in Enum.GetValues<LoginSessionState>())
        {
            var result = Resolve(1, state);
            var expectedStatus = state == LoginSessionState.Playing
                ? LegacyNpcSellHpModeStatus.Ready
                : LegacyNpcSellHpModeStatus.DeadOrNotPlaying;

            Check(result.Status == expectedStatus,
                $"Connection state {state} did not match the USER_PLAY gate.");
        }
    }

    private static void NegativeHpPreservesTheExactLegacyComparison()
    {
        Check(Resolve(-1, LoginSessionState.Playing).Status == LegacyNpcSellHpModeStatus.Ready,
            "The port broadened the source's Hp == 0 check to reject negative HP.");
    }

    private static LegacyNpcSellHpModeResolution Resolve(int hp, LoginSessionState state) =>
        LegacyNpcSellHpModeRules.Resolve(hp, state);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

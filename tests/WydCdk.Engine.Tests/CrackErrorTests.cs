using WydCdk.Protocol;
using WydCdk.World;

internal static class CrackErrorTests
{
    public static Task Rules()
    {
        var first = LegacyCrackErrorRules.Add(0, 10, 17);
        Check(first == new LegacyCrackErrorOutcome(10, 10, 17, true, false), "QuitTrade increment/type/logging.");
        Check(!LegacyCrackErrorRules.Add(1_999_999_980, 10, 17).RequiresCharacterLogout, "Logout below threshold.");
        Check(LegacyCrackErrorRules.Add(1_999_999_990, 10, 17).RequiresCharacterLogout, "No logout at threshold.");
        Check(LegacyCrackErrorRules.Add(int.MaxValue, int.MaxValue, 17).TotalPoints == 2_000_000_000, "Overflow bypasses gate.");
        foreach (var type in new[] { 3, 8, 15 })
            Check(LegacyCrackErrorRules.Add(0, 10, type) is { ShouldLog: false, TotalPoints: 10 }, "Suppressed type lost increment.");
        return Task.CompletedTask;
    }

    public static Task SessionLifetime()
    {
        var sessions = new LoginSessionRegistry();
        Check(!sessions.TryAddCrackError(1, 10, 17, out _), "Unknown connection gained points.");
        Check(sessions.Open(1) && sessions.Open(2), "Open failed.");
        Check(sessions.TryAddCrackError(1, 10, 17, out var error) && error!.TotalPoints == 10, "Accepted mode not counted.");
        Check(sessions.TryGet(2, out var other) && other!.CrackErrorPoints == 0, "Points crossed sessions.");
        var login = new AccountLoginRequest("TESTE", "fixture", new byte[52], 7670, 0, new uint[4]);
        Check(sessions.BeginAccountLogin(1, login) == LoginTransitionResult.Accepted, "Begin account login.");
        Check(sessions.CompleteAccountLogin(1, "TESTE") == LoginTransitionResult.Accepted, "Complete account login.");
        Check(sessions.TryAddCrackError(1, 10, 17, out error) && error!.TotalPoints == 20, "Selection mode not counted.");
        Check(sessions.BeginCharacterWait(1) == LoginTransitionResult.Accepted, "Begin wait.");
        Check(sessions.CompleteCharacterRefresh(1) == LoginTransitionResult.Accepted, "Refresh.");
        Check(sessions.TryGet(1, out var selection) && selection!.CrackErrorPoints == 20, "Refresh incorrectly reset.");
        Check(sessions.BeginCharacterWait(1) == LoginTransitionResult.Accepted, "Second wait.");
        Check(sessions.CompleteCharacterLogin(1, 0, 2100, 2100) == LoginTransitionResult.Accepted, "Character login.");
        Check(sessions.TryGet(1, out var playing) && playing!.CrackErrorPoints == 0, "Character login did not reset.");
        Check(sessions.TryAddCrackError(1, 1_999_999_990, 17, out _), "Boundary seed.");
        Check(sessions.TryAddCrackError(1, 10, 17, out error) && error!.RequiresCharacterLogout, "Session boundary.");
        Check(sessions.CompleteCharacterLogout(1) == LoginTransitionResult.Accepted, "Character logout.");
        Check(sessions.TryAddCrackError(1, 10, 17, out error) && error!.RequiresCharacterLogout, "Logout incorrectly reset or wrapped.");
        Check(sessions.Close(1) && sessions.Open(1), "Reopen.");
        Check(sessions.TryGet(1, out var reopened) && reopened!.CrackErrorPoints == 0, "Reused ID inherited counter.");
        return Task.CompletedTask;
    }

    public static Task RenascimentoResetsOnlyTheRevivedPlayersCrackCounter()
    {
        var sessions = new LoginSessionRegistry();
        Check(sessions.Open(1) && sessions.Open(2), "Renascimento crack-counter fixtures could not open.");
        Check(sessions.TryAddCrackError(1, 40, 17, out _) && sessions.TryAddCrackError(2, 70, 17, out _),
            "Renascimento crack-counter fixtures could not seed their session-local points.");

        Check(sessions.TryResetCrackErrorPoints(1), "Renascimento could not reset the active target session counter.");
        Check(sessions.TryGet(1, out var revived) && revived!.CrackErrorPoints == 0 &&
            sessions.TryGet(2, out var other) && other!.CrackErrorPoints == 70,
            "Renascimento reset another session or retained the revived target's crack-error points.");
        Check(!sessions.TryResetCrackErrorPoints(99), "Renascimento reset reported success for an unknown target session.");
        return Task.CompletedTask;
    }

    public static Task MotionSharesQuitTradeCounter()
    {
        var sessions = new LoginSessionRegistry();
        sessions.Open(1);
        Check(sessions.TryAddCrackError(1, LegacyCrackErrorRules.MotionPoints, LegacyCrackErrorRules.MotionType, out var motion) &&
            motion == new LegacyCrackErrorOutcome(4, 4, 6, true, false), "Motion points/type mismatch.");
        Check(sessions.TryAddCrackError(1, LegacyCrackErrorRules.QuitTradePoints, LegacyCrackErrorRules.QuitTradeType, out var quit) &&
            quit!.TotalPoints == 14, "Motion and QuitTrade counters diverged.");
        var below = LegacyCrackErrorRules.Add(1_999_999_994, LegacyCrackErrorRules.MotionPoints, LegacyCrackErrorRules.MotionType);
        Check(below is { TotalPoints: 1_999_999_998, RequiresCharacterLogout: false }, "Motion threshold narrowing.");
        Check(LegacyCrackErrorRules.Add(below.TotalPoints, 10, 17) is { TotalPoints: 2_000_000_000, RequiresCharacterLogout: true }, "Mixed threshold bypass.");
        return Task.CompletedTask;
    }

    public static Task TradeListSharesCounter()
    {
        var sessions = new LoginSessionRegistry(); sessions.Open(1);
        Check(sessions.TryAddCrackError(1, LegacyCrackErrorRules.TradeListPoints, LegacyCrackErrorRules.TradeListType, out var list) &&
            list == new LegacyCrackErrorOutcome(10, 10, 87, true, false), "ReqTradeList points/type mismatch.");
        Check(sessions.TryAddCrackError(1, 4, 6, out var motion) && motion!.TotalPoints == 14, "ReqTradeList/Motion counters diverged.");
        Check(sessions.TryAddCrackError(1, 10, 17, out var quit) && quit!.TotalPoints == 24, "ReqTradeList/QuitTrade counters diverged.");
        Check(LegacyCrackErrorRules.Add(1_999_999_990, LegacyCrackErrorRules.TradeListPoints, LegacyCrackErrorRules.TradeListType)
            is { TotalPoints: 2_000_000_000, RequiresCharacterLogout: true }, "ReqTradeList threshold bypass.");
        return Task.CompletedTask;
    }

    public static Task ActionSuppressedSharedCounter()
    {
        var sessions = new LoginSessionRegistry(); sessions.Open(1);
        foreach (var expected in new[] { 5, 10, 15 })
            Check(sessions.TryAddCrackError(1, LegacyCrackErrorRules.ActionPoints, LegacyCrackErrorRules.ActionType, out var action) &&
                action == new LegacyCrackErrorOutcome(expected, 5, 3, false, false), "Action suppressed increment lost/type changed.");
        Check(sessions.TryAddCrackError(1, 10, 17, out var quit) && quit!.TotalPoints == 25, "Action/QuitTrade counter diverged.");
        Check(LegacyCrackErrorRules.Add(1_999_999_995, LegacyCrackErrorRules.ActionPoints, LegacyCrackErrorRules.ActionType)
            is { TotalPoints: 2_000_000_000, ShouldLog: false, RequiresCharacterLogout: true }, "Suppressed Action bypassed threshold.");
        return Task.CompletedTask;
    }

    public static Task AttackDeadSharesCounter()
    {
        var sessions = new LoginSessionRegistry(); sessions.Open(1);
        Check(sessions.TryAddCrackError(1, LegacyCrackErrorRules.AttackDeadPoints, LegacyCrackErrorRules.AttackDeadType, out var attack) &&
            attack == new LegacyCrackErrorOutcome(1, 1, 8, false, false), "Dead Attack must add suppressed 1/type8.");
        Check(sessions.TryAddCrackError(1, LegacyCrackErrorRules.QuitTradePoints, LegacyCrackErrorRules.QuitTradeType, out var quit) &&
            quit == new LegacyCrackErrorOutcome(11, 10, 17, true, false), "Dead Attack did not share the session counter with QuitTrade.");
        Check(LegacyCrackErrorRules.Add(LegacyCrackErrorRules.LogoutThreshold - 1,
            LegacyCrackErrorRules.AttackDeadPoints, LegacyCrackErrorRules.AttackDeadType)
            is { TotalPoints: 2_000_000_000, ShouldLog: false, RequiresCharacterLogout: true },
            "Suppressed Dead Attack increment bypassed the logout threshold.");
        return Task.CompletedTask;
    }

    public static Task AttackTimingSharesCounter()
    {
        var sessions = new LoginSessionRegistry(); sessions.Open(1);
        Check(sessions.TryAddCrackError(1, LegacyCrackErrorRules.AttackTimingPoints, LegacyCrackErrorRules.AttackTimingType, out var first) &&
            first == new LegacyCrackErrorOutcome(1, 1, 107, true, false), "Attack timing must add/log 1/type107.");
        Check(sessions.TryAddCrackError(1, LegacyCrackErrorRules.AttackTimingPoints, LegacyCrackErrorRules.AttackTimingType, out var second) &&
            second == new LegacyCrackErrorOutcome(2, 1, 107, true, false), "Repeated timing errors must accumulate on the session.");
        Check(sessions.TryAddCrackError(1, LegacyCrackErrorRules.QuitTradePoints, LegacyCrackErrorRules.QuitTradeType, out var quit) &&
            quit!.TotalPoints == 12, "Attack timing and QuitTrade counters diverged.");
        Check(LegacyCrackErrorRules.Add(LegacyCrackErrorRules.LogoutThreshold - 1,
            LegacyCrackErrorRules.AttackTimingPoints, LegacyCrackErrorRules.AttackTimingType)
            is { TotalPoints: 2_000_000_000, ShouldLog: true, RequiresCharacterLogout: true },
            "Attack timing increment bypassed the normal logout threshold.");
        return Task.CompletedTask;
    }

    public static Task AttackBackstepSharesCounter()
    {
        Check(LegacyCrackErrorRules.AttackBackstepPoints == 4, "Attack backstep must add 4 points.");
        Check(LegacyCrackErrorRules.AttackBackstepType == 7, "Attack backstep must use type 7.");

        var sessions = new LoginSessionRegistry(); sessions.Open(1);
        Check(sessions.TryAddCrackError(1, LegacyCrackErrorRules.AttackBackstepPoints,
            LegacyCrackErrorRules.AttackBackstepType, out var backstep) &&
            backstep == new LegacyCrackErrorOutcome(4, 4, 7, true, false), "Attack backstep increment/logging mismatch.");
        Check(sessions.TryAddCrackError(1, LegacyCrackErrorRules.AttackTimingPoints,
            LegacyCrackErrorRules.AttackTimingType, out var timing) && timing!.TotalPoints == 5,
            "Attack backstep did not share the type107 session counter.");
        Check(LegacyCrackErrorRules.Add(LegacyCrackErrorRules.LogoutThreshold - 4,
            LegacyCrackErrorRules.AttackBackstepPoints, LegacyCrackErrorRules.AttackBackstepType)
            is { TotalPoints: 2_000_000_000, ShouldLog: true, RequiresCharacterLogout: true },
            "Attack backstep must preserve the normal logout threshold.");
        return Task.CompletedTask;
    }

    public static Task AttackBackstepWrapPolicy()
    {
        var sessions = PlayingSession(1);
        Check(TryAttackTiming(sessions, 1, 1_000_000, 1_000_000, out var backstep) == AttackTimingResult.Accepted && !backstep,
            "First ordinary tick was incorrectly marked as a backstep.");
        var next = uint.MaxValue - 300u;
        Check(TryAttackTiming(sessions, 1, next, 1_000_000, out backstep) == AttackTimingResult.OutsideServerWindow && !backstep,
            "Forward high tick before wrap must update state without a backstep.");
        var exactHundred = uint.MaxValue - 400u;
        Check(TryAttackTiming(sessions, 1, exactHundred, exactHundred, out backstep) == AttackTimingResult.OutsideServerWindow && !backstep,
            "A tick exactly 100ms behind LastAttackTick must not be type7.");
        var oneHundredAndOne = uint.MaxValue - 501u;
        var result101 = TryAttackTiming(sessions, 1, oneHundredAndOne, oneHundredAndOne, out backstep);
        Check(result101 == AttackTimingResult.OutsideServerWindow && backstep,
            $"A tick 101ms behind after unsigned wrap must trigger type7 before the server-window return (result={result101}, backstep={backstep}).");

        var tooSoonSessions = PlayingSession(2);
        Check(TryAttackTiming(tooSoonSessions, 2, 1_000_000, 1_000_000, out backstep) == AttackTimingResult.Accepted && !backstep,
            "First ordinary tick was incorrectly marked as a backstep.");
        Check(TryAttackTiming(tooSoonSessions, 2, 1_000_799, 1_000_799, out backstep) == AttackTimingResult.TooSoon && !backstep,
            "TooSoon must return before the backstep gate and must not add type7.");

        var rolloverSessions = PlayingSession(3);
        Check(TryAttackTiming(rolloverSessions, 3, 1_000_000, 1_000_000, out backstep) == AttackTimingResult.Accepted && !backstep,
            "Initial ordinary tick was incorrectly marked as a backstep.");
        Check(TryAttackTiming(rolloverSessions, 3, uint.MaxValue - 300u, 1_000_000, out backstep) == AttackTimingResult.OutsideServerWindow && !backstep,
            "High tick that seeds LastAttackTick wrap was not handled before the window rejection.");
        Check(TryAttackTiming(rolloverSessions, 3, 1_000u, 50_000u, out backstep) == AttackTimingResult.Accepted && backstep,
            "Current-era tick after LastAttackTick wrap must trigger type7 and remain accepted.");
        return Task.CompletedTask;
    }

    private static LoginSessionRegistry PlayingSession(int connectionId)
    {
        var sessions = new LoginSessionRegistry();
        var login = new AccountLoginRequest("FIGHTER", "secret", new byte[52], 7670, 0, [0, 0, 0, 0]);
        Check(sessions.Open(connectionId) && sessions.BeginAccountLogin(connectionId, login) == LoginTransitionResult.Accepted &&
            sessions.CompleteAccountLogin(connectionId, "FIGHTER") == LoginTransitionResult.Accepted &&
            sessions.BeginCharacterWait(connectionId) == LoginTransitionResult.Accepted &&
            sessions.CompleteCharacterLogin(connectionId) == LoginTransitionResult.Accepted,
            "Attack backstep fixture did not enter USER_PLAY.");
        return sessions;
    }

    private static AttackTimingResult TryAttackTiming(LoginSessionRegistry sessions, int connectionId,
        uint clientTick, uint serverTick, out bool backstep)
        => sessions.TryAcceptAttackTiming(connectionId, clientTick, serverTick, out backstep);

    public static Task AttackDeadGatePolicy()
    {
        Check(LegacyAttackDeadGateRules.ShouldReject(0, 1), "Ordinary attack at exactly zero HP was not rejected.");
        Check(!LegacyAttackDeadGateRules.ShouldReject(0, LegacyAttackDeadGateRules.ResurrectionSkillIndex), "Skill 99 resurrection exception was rejected.");
        Check(!LegacyAttackDeadGateRules.ShouldReject(-1, 1), "Negative HP was incorrectly treated as exactly zero.");
        Check(!LegacyAttackDeadGateRules.ShouldReject(1, 1), "Living attacker was rejected.");
        return Task.CompletedTask;
    }

    public static async Task ConcurrentCounters()
    {
        var sessions = new LoginSessionRegistry();
        sessions.Open(1); sessions.Open(2);
        await Task.WhenAll(Enumerable.Range(0, 64).Select(i => Task.Run(() =>
        {
            for (var n = 0; n < 100; n++)
                Check(sessions.TryAddCrackError(1 + i % 2, 10, 17, out _), "Increment failed.");
        })));
        Check(sessions.TryGet(1, out var a) && a!.CrackErrorPoints == 32_000 &&
            sessions.TryGet(2, out var b) && b!.CrackErrorPoints == 32_000, "Concurrent increments lost.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

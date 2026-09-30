using WydCdk.Protocol;
using WydCdk.World;

internal static class MovementRangeTests
{
    public static void IllusionEligibilityUsesLegacyClassSkillAndLevelGate()
    {
        Check(!LegacyIllusionRules.ShouldRecordEligibilityError(3, 2, 398), "Eligible class/learned bit was penalized.");
        Check(LegacyIllusionRules.ShouldRecordEligibilityError(2, 2, 398), "Wrong Illusion class was not penalized below max level.");
        Check(LegacyIllusionRules.ShouldRecordEligibilityError(3, 0, 398), "Missing Illusion learned bit was not penalized below max level.");
        Check(!LegacyIllusionRules.ShouldRecordEligibilityError(2, 0, 399), "Invalid eligibility was penalized at MAX_LEVEL.");
        Check(!LegacyIllusionRules.ShouldRecordEligibilityError(2, 0, 400), "Invalid eligibility was penalized above MAX_LEVEL.");
        Check(LegacyCrackErrorRules.Add(0, LegacyCrackErrorRules.ActionIllusionEligibilityPoints,
                LegacyCrackErrorRules.ActionIllusionEligibilityType) is
            { TotalPoints: 10, AddedPoints: 10, Type: 28, ShouldLog: true, RequiresCharacterLogout: false },
            "Illusion eligibility error did not add 10/type28 below threshold.");
        Check(LegacyCrackErrorRules.Add(
                LegacyCrackErrorRules.LogoutThreshold - LegacyCrackErrorRules.ActionIllusionEligibilityPoints,
                LegacyCrackErrorRules.ActionIllusionEligibilityPoints,
                LegacyCrackErrorRules.ActionIllusionEligibilityType) is
            { TotalPoints: LegacyCrackErrorRules.LogoutThreshold, ShouldLog: true, RequiresCharacterLogout: true },
            "Illusion eligibility error bypassed the shared logout threshold.");
    }

    public static void Boundaries()
    {
        foreach (var sign in new[] { -1, 1 })
        foreach (var axis in new[] { 0, 1 })
        foreach (var vector in new (int Distance, bool Accept, bool Correct)[] { (33, true, false), (34, false, false), (66, false, false), (67, false, true) })
        {
            var x = 2100 + (axis == 0 ? sign * vector.Distance : 0);
            var y = 2100 + (axis == 1 ? sign * vector.Distance : 0);
            Check(LegacyMovementRules.IsTargetWithinViewGrid(2100, 2100, x, y) == vector.Accept, "Inclusive 33 window changed.");
            Check(LegacyMovementRules.RequiresPositionCorrection(2100, 2100, x, y) == vector.Correct, "Strict 66 correction window changed.");
        }
        Check(LegacyMovementRules.IsTargetWithinViewGrid(2100, 2100, 2133, 2133), "Window became radial instead of rectangular.");
        Check(!LegacyMovementRules.RequiresPositionCorrection(2100, 2100, 2166, 2166), "Diagonal 66 incorrectly corrected.");
        Check(LegacyMovementRules.RequiresPositionCorrection(int.MinValue, 0, int.MaxValue, 0), "Correction delta overflowed.");
    }

    public static void RejectionPreservesPositionAndSharedCounter()
    {
        var sessions = new LoginSessionRegistry(); sessions.Open(1);
        sessions.BeginAccountLogin(1, new AccountLoginRequest("TEST", "fixture", new byte[52], 7670, 0, new uint[4]));
        sessions.CompleteAccountLogin(1, "TEST");
        sessions.BeginCharacterWait(1); sessions.CompleteCharacterLogin(1, 0, 2100, 2100);
        foreach (var effect in new[] { 0, 1, 2 })
        {
            var request = new ActionRequest(100, 100, effect, 0, new byte[24], 2134, 2100);
            Check(sessions.TryApplyMovement(1, request) == MovementResult.StepTooLarge, "Effect bypassed authoritative range.");
            Check(sessions.TryAddCrackError(1, LegacyCrackErrorRules.ActionRangePoints, LegacyCrackErrorRules.ActionRangeType, out var outcome)
                && outcome!.ShouldLog && outcome.TotalPoints == effect + 1, "Range points/log missing.");
        }
        Check(sessions.TryGet(1, out var state) && state!.PositionX == 2100 && state.PositionY == 2100, "Rejected destination changed position.");
        Check(sessions.TryAddCrackError(1, 10, 17, out var quit) && quit!.TotalPoints == 13, "Range/QuitTrade counters diverged.");
        Check(LegacyCrackErrorRules.Add(1_999_999_999, 1, 5).RequiresCharacterLogout, "Range bypassed logout threshold.");
    }

    public static void AllowedSpeedRequiresAuthoritativeAttackRun()
    {
        Check(!LegacyMovementRules.TryGetAllowedSpeed(null, out _), "Missing AttackRun authorized action movement.");
        Check(LegacyMovementRules.TryGetAllowedSpeed(0, out var zero) && zero == 0, "Zero AttackRun allowed speed changed.");
        Check(LegacyMovementRules.TryGetAllowedSpeed(0x10, out var highOnly) && highOnly == 0, "AttackRun high nibble leaked into speed.");
        Check(LegacyMovementRules.TryGetAllowedSpeed(15, out var maximum) && maximum == 15, "Maximum low-nibble speed changed.");
        Check(LegacyMovementRules.TryGetAllowedSpeed(0x1F, out var highAndMaximum) && highAndMaximum == 15, "AttackRun high nibble changed maximum speed.");
    }

    public static void DeadActionPrecedesUnavailableSpeed()
    {
        Check(LegacyMovementRules.ResolveActionPreconditions(0, null, out _) == LegacyMovementRules.ActionGateResult.CharacterDead,
            "A dead character without an AttackRun score skipped the HP-zero action response.");
        Check(LegacyMovementRules.ResolveActionPreconditions(1, null, out _) == LegacyMovementRules.ActionGateResult.AuthoritativeStateUnavailable,
            "A live character without an AttackRun score was allowed to move.");
        Check(LegacyMovementRules.ResolveActionPreconditions(null, 15, out _) == LegacyMovementRules.ActionGateResult.AuthoritativeStateUnavailable,
            "An action without authoritative HP was allowed to continue.");
        Check(LegacyMovementRules.ResolveActionPreconditions(-1, 0x1F, out var allowedSpeed) == LegacyMovementRules.ActionGateResult.Ready
            && allowedSpeed == 15, "Negative HP or the AttackRun speed nibble changed.");
    }

    public static void SpeedViolationSharesCrackErrorCounter()
    {
        var sessions = new LoginSessionRegistry(); sessions.Open(1);
        Check(sessions.TryAddCrackError(1, 5, 4, out var speed) &&
            speed == new LegacyCrackErrorOutcome(5, 5, 4, true, false), "Movement-speed violation points/type/logging changed.");
        Check(sessions.TryAddCrackError(1, 1, 5, out var range) &&
            range == new LegacyCrackErrorOutcome(6, 1, 5, true, false), "Speed and range errors stopped sharing the session counter.");
    }

    public static void SpeedThresholdLogoutStopsActionContinuation()
    {
        var sessions = new LoginSessionRegistry(); sessions.Open(1);
        var login = new AccountLoginRequest("TESTE", "fixture", new byte[52], 7670, 0, new uint[4]);
        Check(sessions.BeginAccountLogin(1, login) == LoginTransitionResult.Accepted, "Begin account login failed.");
        Check(sessions.CompleteAccountLogin(1, "TESTE") == LoginTransitionResult.Accepted, "Complete account login failed.");
        Check(sessions.BeginCharacterWait(1) == LoginTransitionResult.Accepted, "Begin character wait failed.");
        Check(sessions.CompleteCharacterLogin(1, 0, 2100, 2100) == LoginTransitionResult.Accepted, "Complete character login failed.");
        Check(sessions.TryAddCrackError(1, LegacyCrackErrorRules.LogoutThreshold - LegacyCrackErrorRules.ActionSpeedPoints,
            LegacyCrackErrorRules.QuitTradeType, out var seed) && seed is { RequiresCharacterLogout: false }, "Threshold seed failed.");
        Check(sessions.TryAddCrackError(1, LegacyCrackErrorRules.ActionSpeedPoints, LegacyCrackErrorRules.ActionSpeedType,
            out var speed) && speed is { RequiresCharacterLogout: true }, "Speed violation did not cross logout threshold.");
        Check(sessions.CompleteCharacterLogout(1) == LoginTransitionResult.Accepted, "Threshold logout did not complete.");
        Check(sessions.TryGet(1, out var loggedOut) &&
            !LegacyMovementRules.ShouldContinueAction(loggedOut?.State),
            "Action processing continued after speed-error logout.");
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

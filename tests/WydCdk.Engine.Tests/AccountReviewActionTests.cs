using WydCdk.World;

internal static class AccountReviewActionTests
{
    public static Task HighSeverityRequiresAnActualSpeedViolation()
    {
        Check(LegacyMovementRules.RequiresAccountAnalysis(20, 5), "Requested speed 20 did not meet the inclusive speed threshold.");
        Check(LegacyMovementRules.RequiresAccountAnalysis(13, 300), "Full AttackRun 300 was not used for high-severity detection.");
        Check(!LegacyMovementRules.RequiresAccountAnalysis(19, 299), "Values below both high-severity thresholds were marked.");
        Check(!LegacyMovementRules.RequiresAccountAnalysis(12, 300), "High AttackRun marked an allowed low-nibble speed.");
        Check(!LegacyMovementRules.RequiresAccountAnalysis(5, 5), "Equal allowed speed was considered a violation.");
        Check(!LegacyMovementRules.RequiresAccountAnalysis(16, byte.MaxValue),
            "The 8-bit authoritative score maximum incorrectly reached the 300-only threshold.");
        return Task.CompletedTask;
    }

    public static async Task PersistenceFailureIsDistinctAndClosed()
    {
        var store = new FakeStore(new IOException("unavailable"));
        var coordinator = new LegacyActionAccountReviewCoordinator(store);
        var result = await coordinator.ReviewSpeedViolationAsync("TESTE", "Hero", "127.0.0.1", 20, 5, 5);
        Check(result == ActionAccountReviewResult.PersistenceUnavailable && store.MarkCalls == 1,
            "A failed account-review write was reported as a confirmed mark.");
    }

    public static async Task CrackLogoutThresholdDoesNotSuppressAnalysisMark()
    {
        var crack = LegacyCrackErrorRules.Add(1_999_999_995, 5, 4);
        Check(crack is { TotalPoints: 2_000_000_000, RequiresCharacterLogout: true },
            "The fixture did not reach the shared crack-error logout boundary.");

        var store = new FakeStore();
        var result = await new LegacyActionAccountReviewCoordinator(store)
            .ReviewSpeedViolationAsync("TESTE", "Hero", "127.0.0.1", 20, 5, 5);
        Check(result == ActionAccountReviewResult.Marked && store.MarkCalls == 1,
            "The threshold logout outcome suppressed the qualifying Analysis persistence operation.");
    }

    public static async Task NonSevereViolationDoesNotUseReviewStore()
    {
        var store = new FakeStore();
        var result = await new LegacyActionAccountReviewCoordinator(store)
            .ReviewSpeedViolationAsync("TESTE", "Hero", "127.0.0.1", 19, 5, 5);
        Check(result == ActionAccountReviewResult.NotRequired && store.MarkCalls == 0,
            "A non-severe speed violation attempted to mark the account for Analysis.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class FakeStore(Exception? exception = null) : IAccountReviewStore
    {
        public int MarkCalls { get; private set; }

        public ValueTask<AccountReviewStatus> ReadStatusAsync(string accountName, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(AccountReviewStatus.NotMarked);

        public ValueTask<AccountReviewMarkResult> MarkForAnalysisAsync(AccountReviewEvidence evidence, CancellationToken cancellationToken = default)
        {
            MarkCalls++;
            if (exception is not null) return ValueTask.FromException<AccountReviewMarkResult>(exception);
            return ValueTask.FromResult(AccountReviewMarkResult.Marked);
        }
    }
}

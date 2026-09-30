using WydCdk.Protocol;
using WydCdk.World;

internal static class AccountReviewLoginTests
{
    public static IAccountReviewStore CreateUnmarkedStore() => new FakeReviewStore(AccountReviewStatus.NotMarked);

    public static async Task AnalysisClosesBeforeAuthentication()
    {
        var sessions = new LoginSessionRegistry();
        sessions.Open(1);
        var accounts = new FakeAccountStore(new(AccountAuthenticationStatus.Success, "TESTE"));
        var reviews = new FakeReviewStore(AccountReviewStatus.Analysis);

        var outcome = await new AccountLoginCoordinator(sessions, accounts, reviews).HandleAsync(1, Request("TESTE"));

        Check(outcome.Rejection == AccountLoginRejection.AccountUnderAnalysis && !outcome.IsSuccess,
            "An account under Analysis was not returned as a distinct login rejection.");
        Check(accounts.Calls == 0 && reviews.ReadCalls == 1,
            "Password authentication ran before the account-review gate.");
        Check(!sessions.TryGet(1, out _), "The Analysis rejection left the pending connection open.");
    }

    public static async Task UnmarkedAccountUsesExistingAuthentication()
    {
        var sessions = new LoginSessionRegistry();
        sessions.Open(2);
        var accounts = new FakeAccountStore(new(AccountAuthenticationStatus.Success, "TESTE"));
        var reviews = new FakeReviewStore(AccountReviewStatus.NotMarked);

        var outcome = await new AccountLoginCoordinator(sessions, accounts, reviews).HandleAsync(2, Request("TESTE"));

        Check(outcome.IsSuccess && outcome.Rejection == AccountLoginRejection.None && accounts.Calls == 1 && reviews.ReadCalls == 1,
            "An unmarked account did not follow the existing successful authentication path.");
        Check(sessions.TryGet(2, out var session) && session!.State == LoginSessionState.CharacterSelection,
            "Successful authentication did not enter character selection.");
    }

    public static async Task ReviewReadFailureFailsClosed()
    {
        var sessions = new LoginSessionRegistry();
        sessions.Open(3);
        var accounts = new FakeAccountStore(new(AccountAuthenticationStatus.Success, "TESTE"));
        var reviews = new FakeReviewStore(exception: new IOException("review store unavailable"));

        var outcome = await new AccountLoginCoordinator(sessions, accounts, reviews).HandleAsync(3, Request("TESTE"));

        Check(outcome.Rejection == AccountLoginRejection.ReviewStoreUnavailable && accounts.Calls == 0,
            "A review-store read failure fell through to password authentication.");
        Check(!sessions.TryGet(3, out _), "A review-store failure left the pending connection open.");
    }

    public static async Task InvalidAccountNameKeepsAuthenticationRejection()
    {
        var sessions = new LoginSessionRegistry();
        sessions.Open(4);
        var accounts = new FakeAccountStore(new(AccountAuthenticationStatus.InvalidAccountName, null));
        var reviews = new FakeReviewStore(AccountReviewStatus.NotMarked);

        var outcome = await new AccountLoginCoordinator(sessions, accounts, reviews).HandleAsync(4, Request("BAD/NAME"));

        Check(outcome.Authentication == AccountAuthenticationStatus.InvalidAccountName &&
            outcome.Rejection == AccountLoginRejection.None && accounts.Calls == 1,
            "An invalid account name no longer receives the established authentication rejection.");
        Check(reviews.ReadCalls == 0, "An invalid account name reached the review-file lookup.");
        Check(!sessions.TryGet(4, out _), "An invalid account-name rejection left the pending connection open.");
    }

    public static Task ReviewFailureMessagesStayDistinct()
    {
        Check(AccountLoginFailureNotice.ForReview(AccountLoginRejection.AccountUnderAnalysis) ==
            "Estamos analisando sua conta. Entre em contato com o suporte.",
            "The client Analysis notice does not match the approved neutral text.");
        Check(AccountLoginFailureNotice.ForReview(AccountLoginRejection.ReviewStoreUnavailable) ==
            "Nao foi possivel consultar a conta. Tente novamente mais tarde.",
            "The client review-store failure notice exposes details or resembles a positive Analysis mark.");
        return Task.CompletedTask;
    }

    private static AccountLoginRequest Request(string accountName) =>
        new(accountName, "test-password", new byte[52], 7670, 0, new uint[4]);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class FakeAccountStore(AccountAuthenticationResult result) : IAccountStore
    {
        public int Calls { get; private set; }

        public ValueTask<AccountAuthenticationResult> AuthenticateAsync(AccountLoginRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FakeReviewStore(AccountReviewStatus status = AccountReviewStatus.NotMarked, Exception? exception = null) : IAccountReviewStore
    {
        public int ReadCalls { get; private set; }

        public ValueTask<AccountReviewStatus> ReadStatusAsync(string accountName, CancellationToken cancellationToken = default)
        {
            ReadCalls++;
            if (exception is not null) return ValueTask.FromException<AccountReviewStatus>(exception);
            return ValueTask.FromResult(status);
        }

        public ValueTask<AccountReviewMarkResult> MarkForAnalysisAsync(AccountReviewEvidence evidence, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(AccountReviewMarkResult.Marked);
    }
}

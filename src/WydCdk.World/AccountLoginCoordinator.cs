using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>Outcome of a complete account-login attempt; it contains no password or account-file data.</summary>
public enum AccountLoginRejection
{
    None,
    AccountUnderAnalysis,
    ReviewStoreUnavailable,
}

public sealed record AccountLoginOutcome(
    LoginTransitionResult Transition,
    AccountAuthenticationStatus? Authentication,
    AccountLoginRejection Rejection = AccountLoginRejection.None)
{
    public bool IsSuccess => Transition == LoginTransitionResult.Accepted &&
        Authentication == AccountAuthenticationStatus.Success && Rejection == AccountLoginRejection.None;
}

/// <summary>
/// Coordinates the legacy order: accept account-login packet, authenticate through DBSrv-equivalent storage,
/// then enter character selection. Authentication failures close the session, as TMSrv does on the matching
/// DB failure messages. It does not emit a client packet yet.
/// </summary>
public sealed class AccountLoginCoordinator(LoginSessionRegistry sessions, IAccountStore accounts, IAccountReviewStore accountReviews)
{
    public async ValueTask<AccountLoginOutcome> HandleAsync(int connectionId, AccountLoginRequest request, CancellationToken cancellationToken = default)
    {
        var transition = sessions.BeginAccountLogin(connectionId, request);
        if (transition != LoginTransitionResult.Accepted) return new(transition, null);

        // Preserve the existing authentication rejection for invalid names, but
        // never use untrusted input to probe a review-store path.
        if (LegacyAccountNameRules.TryNormalize(request.AccountName, out var normalizedAccount))
        {
            AccountReviewStatus reviewStatus;
            try
            {
                reviewStatus = await accountReviews.ReadStatusAsync(normalizedAccount, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                sessions.Close(connectionId);
                throw;
            }
            catch (Exception)
            {
                sessions.Close(connectionId);
                return new(LoginTransitionResult.Accepted, null, AccountLoginRejection.ReviewStoreUnavailable);
            }

            if (reviewStatus == AccountReviewStatus.Analysis)
            {
                sessions.Close(connectionId);
                return new(LoginTransitionResult.Accepted, null, AccountLoginRejection.AccountUnderAnalysis);
            }
            if (reviewStatus != AccountReviewStatus.NotMarked)
            {
                sessions.Close(connectionId);
                return new(LoginTransitionResult.Accepted, null, AccountLoginRejection.ReviewStoreUnavailable);
            }
        }

        var authentication = await accounts.AuthenticateAsync(request, cancellationToken);
        if (!authentication.IsSuccess || authentication.AccountName is null)
        {
            sessions.Close(connectionId);
            return new(LoginTransitionResult.Accepted, authentication.Status);
        }

        transition = sessions.CompleteAccountLogin(connectionId, authentication.AccountName);
        if (transition != LoginTransitionResult.Accepted) sessions.Close(connectionId);
        return new(transition, authentication.Status);
    }
}

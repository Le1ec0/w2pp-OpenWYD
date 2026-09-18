using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>Outcome of a complete account-login attempt; it contains no password or account-file data.</summary>
public sealed record AccountLoginOutcome(LoginTransitionResult Transition, AccountAuthenticationStatus? Authentication)
{
    public bool IsSuccess => Transition == LoginTransitionResult.Accepted && Authentication == AccountAuthenticationStatus.Success;
}

/// <summary>
/// Coordinates the legacy order: accept account-login packet, authenticate through DBSrv-equivalent storage,
/// then enter character selection. Authentication failures close the session, as TMSrv does on the matching
/// DB failure messages. It does not emit a client packet yet.
/// </summary>
public sealed class AccountLoginCoordinator(LoginSessionRegistry sessions, IAccountStore accounts)
{
    public async ValueTask<AccountLoginOutcome> HandleAsync(int connectionId, AccountLoginRequest request, CancellationToken cancellationToken = default)
    {
        var transition = sessions.BeginAccountLogin(connectionId, request);
        if (transition != LoginTransitionResult.Accepted) return new(transition, null);

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

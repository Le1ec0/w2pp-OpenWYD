using System.Text;
using WydCdk.Protocol;

namespace WydCdk.World;

public enum AccountSecureStatus
{
    Success,
    Fail, // wrong PIN, or an invalid change attempt -> CFileDB.cpp:1444-1445 sends _MSG_AccountSecureFail.
    ChangeWithoutVerification, // CFileDB.cpp:1391-1396's silent early "break" - callers must send nothing back.
    AccountNotFound,
}

public sealed record AccountSecureOutcome(AccountSecureStatus Status)
{
    public bool IsSuccess => Status == AccountSecureStatus.Success;
}

/// <summary>
/// Equivalent of TMSrv's <c>Exec_MSG_AccountSecure</c> (a stateless forward - no <c>pUser[conn].Mode</c> check at
/// all, unlike every other message this port handles) followed by <c>CFileDB::_MSG_AccountSecure</c>
/// (CFileDB.cpp:1382-1446). Four cases, checked in the reference's own order:
/// <list type="number">
/// <item>Requesting a PIN change without having verified the current one this session fails silently (no
/// response at all - CFileDB.cpp:1391's <c>break</c>, not a <c>SendDBSignal</c> call).</item>
/// <item>An account that has never set a PIN (<c>NumericToken[0] == -1</c>, i.e. the raw byte <c>0xFF</c>) accepts
/// whatever six digits are submitted as the new PIN, regardless of the change flag - this is first-time setup.</item>
/// <item>Verifying (<c>ChangeNumeric == false</c>) against a matching stored PIN succeeds.</item>
/// <item>Changing an already-verified PIN (<c>ChangeNumeric == true</c> with this session already verified)
/// succeeds and overwrites the stored PIN.</item>
/// </list>
/// Anything else (wrong PIN on verify, or an unreachable change combination) fails and resets verification.
/// </summary>
public sealed class AccountSecureCoordinator(LoginSessionRegistry sessions, ICharacterStore accounts)
{
    private const byte UnsetSentinel = 0xFF; // char NumericToken[6] == -1, interpreted as a signed byte.

    public async ValueTask<AccountSecureOutcome> HandleAsync(int connectionId, string accountName, AccountSecureRequest request, CancellationToken cancellationToken = default)
    {
        var verified = sessions.TryGet(connectionId, out var session) && session!.SecureVerified;

        if (request.ChangeNumeric && !verified)
            return new(AccountSecureStatus.ChangeWithoutVerification);

        var storedToken = await accounts.ReadNumericTokenAsync(accountName, cancellationToken);
        if (storedToken is null) return new(AccountSecureStatus.AccountNotFound);

        var submittedToken = Encoding.ASCII.GetBytes(request.NumericToken);

        if (storedToken[0] == UnsetSentinel)
        {
            if (!await accounts.TryWriteNumericTokenAsync(accountName, submittedToken, cancellationToken))
                return new(AccountSecureStatus.AccountNotFound);

            sessions.SetSecureVerified(connectionId, true);
            return new(AccountSecureStatus.Success);
        }

        if (!request.ChangeNumeric && !verified && storedToken.AsSpan().SequenceEqual(submittedToken))
        {
            sessions.SetSecureVerified(connectionId, true);
            return new(AccountSecureStatus.Success);
        }

        if (request.ChangeNumeric && verified)
        {
            if (!await accounts.TryWriteNumericTokenAsync(accountName, submittedToken, cancellationToken))
                return new(AccountSecureStatus.AccountNotFound);

            sessions.SetSecureVerified(connectionId, true);
            return new(AccountSecureStatus.Success);
        }

        sessions.SetSecureVerified(connectionId, false);
        return new(AccountSecureStatus.Fail);
    }
}

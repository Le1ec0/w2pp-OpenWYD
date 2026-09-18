namespace WydCdk.World;

/// <summary>
/// Text sent through the legacy <c>MSG_MessagePanel</c> path before TMSrv closes
/// a failed account-login session. The client has no useful state transition if
/// the signal is omitted and remains on "Conectando no servidor".
/// </summary>
public static class AccountLoginFailureNotice
{
    public static string For(AccountAuthenticationStatus status) => status switch
    {
        AccountAuthenticationStatus.AccountNotFound => "Conta nao encontrada.",
        AccountAuthenticationStatus.InvalidAccountName => "Conta nao encontrada.",
        AccountAuthenticationStatus.InvalidAccountFile => "Conta nao encontrada.",
        AccountAuthenticationStatus.WrongPassword => "Senha incorreta.",
        _ => "Nao foi possivel autenticar a conta.",
    };
}

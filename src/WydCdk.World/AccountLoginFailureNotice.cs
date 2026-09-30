namespace WydCdk.World;

/// <summary>
/// Text sent through the legacy <c>MSG_MessagePanel</c> path before TMSrv closes
/// a failed account-login session. The client has no useful state transition if
/// the signal is omitted and remains on "Conectando no servidor".
/// </summary>
public static class AccountLoginFailureNotice
{
    public static string ForReview(AccountLoginRejection rejection) => rejection switch
    {
        AccountLoginRejection.AccountUnderAnalysis => "Estamos analisando sua conta. Entre em contato com o suporte.",
        AccountLoginRejection.ReviewStoreUnavailable => "Nao foi possivel consultar a conta. Tente novamente mais tarde.",
        _ => "Nao foi possivel autenticar a conta.",
    };

    public static string For(AccountAuthenticationStatus status) => status switch
    {
        AccountAuthenticationStatus.AccountNotFound => "Conta nao encontrada.",
        AccountAuthenticationStatus.InvalidAccountName => "Conta nao encontrada.",
        AccountAuthenticationStatus.InvalidAccountFile => "Conta nao encontrada.",
        AccountAuthenticationStatus.WrongPassword => "Senha incorreta.",
        _ => "Nao foi possivel autenticar a conta.",
    };
}

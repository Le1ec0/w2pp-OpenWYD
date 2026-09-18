using System.Data.Common;
using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Account-login provider for the site account table. It reads only the
/// normalized account name and password hash; plaintext passwords are never
/// stored or written by the server.
/// </summary>
public sealed class MariaDbAccountAuthenticator(Func<DbConnection> connectionFactory) : IAccountStore
{
    internal const string AccountByNameSql = "SELECT account_name, password_hash, password_scheme FROM accounts WHERE UPPER(account_name) = @account_name LIMIT 1";

    public async ValueTask<AccountAuthenticationResult> AuthenticateAsync(AccountLoginRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeAccountName(request.AccountName, out var normalized))
            return new(AccountAuthenticationStatus.InvalidAccountName, null);

        await using var connection = connectionFactory() ?? throw new InvalidOperationException("The MariaDB account connection factory returned null.");
        await connection.OpenAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = AccountByNameSql;
        AddParameter(command, "@account_name", normalized);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return new(AccountAuthenticationStatus.AccountNotFound, null);

        var storedName = reader.IsDBNull(0) ? string.Empty : reader.GetString(0).ToUpperInvariant();
        var passwordHash = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        var passwordScheme = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
        if (storedName != normalized || string.IsNullOrWhiteSpace(passwordHash) ||
            !string.Equals(passwordScheme, "password_hash", StringComparison.OrdinalIgnoreCase))
            return new(AccountAuthenticationStatus.InvalidAccountFile, null);

        try
        {
            return BCrypt.Net.BCrypt.Verify(request.AccountPassword, passwordHash)
                ? new(AccountAuthenticationStatus.Success, storedName)
                : new(AccountAuthenticationStatus.WrongPassword, null);
        }
        catch (ArgumentException)
        {
            return new(AccountAuthenticationStatus.InvalidAccountFile, null);
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static bool TryNormalizeAccountName(string accountName, out string normalized)
    {
        normalized = accountName.ToUpperInvariant();
        return normalized.Length is >= 1 and < 16 && normalized.All(static character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-');
    }
}

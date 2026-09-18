using System.Data;
using System.Data.Common;
using WydCdk.Protocol;

namespace WydCdk.World;

public interface IDonateBalanceStore
{
    ValueTask<int?> ReadDonateAsync(string accountName, CancellationToken cancellationToken = default);
    ValueTask<DonateBalanceSaveResult> TrySaveDonateAsync(string accountName, int donate, int? expectedDonate = null, CancellationToken cancellationToken = default);
}

public interface IDonatePixStore
{
    ValueTask<string?> ReadDonatePixAsync(string accountName, CancellationToken cancellationToken = default);
}

public interface IDonateCatalogStore
{
    ValueTask<LegacyDonateShopCatalog?> ReadDonateCatalogAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// MariaDB adapter using the provider-neutral ADO.NET surface. The caller
/// supplies a provider connection factory, so this project does not embed
/// credentials or force a package/provider before deployment is authorized.
/// </summary>
public sealed class MariaDbDonateShopStore(Func<DbConnection> connectionFactory) : IDonateBalanceStore, IDonatePixStore, IDonateCatalogStore
{
    private const string AccountByNameSql = "SELECT donate FROM accounts WHERE account_name = @account_name LIMIT 1";
    private const string PixByNameSql = "SELECT pix_key FROM accounts WHERE account_name = @account_name LIMIT 1";
    private const string CatalogSql = "SELECT store, page, item_position, item_index, price, stock FROM donate_shop_catalog WHERE enabled = 1 ORDER BY store, page, item_position";

    public async ValueTask<int?> ReadDonateAsync(string accountName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = AccountByNameSql;
        AddParameter(command, "@account_name", accountName);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null || value is DBNull ? null : Convert.ToInt32(value);
    }

    public async ValueTask<string?> ReadDonatePixAsync(string accountName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = PixByNameSql;
        AddParameter(command, "@account_name", accountName);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null || value is DBNull ? null : Convert.ToString(value);
    }

    public async ValueTask<DonateBalanceSaveResult> TrySaveDonateAsync(string accountName, int donate, int? expectedDonate = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        if (donate < 0)
            return DonateBalanceSaveResult.InvalidValue;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        using var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = "SELECT donate FROM accounts WHERE account_name = @account_name LIMIT 1 FOR UPDATE";
        AddParameter(select, "@account_name", accountName);
        var current = await select.ExecuteScalarAsync(cancellationToken);
        if (current is null || current is DBNull)
        {
            await transaction.RollbackAsync(cancellationToken);
            return DonateBalanceSaveResult.AccountNotFound;
        }
        if (expectedDonate is not null && Convert.ToInt32(current) != expectedDonate.Value)
        {
            await transaction.RollbackAsync(cancellationToken);
            return DonateBalanceSaveResult.Conflict;
        }

        using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = "UPDATE accounts SET donate = @donate WHERE account_name = @account_name";
        AddParameter(update, "@donate", donate);
        AddParameter(update, "@account_name", accountName);
        await update.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return DonateBalanceSaveResult.Success;
    }

    public async ValueTask<LegacyDonateShopCatalog?> ReadDonateCatalogAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = CatalogSql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var entries = new List<DonateStoreEntry>(LegacyDonateShopCatalog.SlotCount);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new DonateStoreEntry(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5)));
        }

        return entries.Count == 0 ? null : LegacyDonateShopCatalog.FromEntries(entries);
    }

    private DbConnection CreateConnection() => connectionFactory() ?? throw new InvalidOperationException("The MariaDB connection factory returned null.");

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

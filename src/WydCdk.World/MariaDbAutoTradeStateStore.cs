using System.Data.Common;
using System.Text.Json;

namespace WydCdk.World;

/// <summary>
/// MariaDB adapter for the canonical autotrade listing. The legacy account
/// blob remains character storage; this table owns only the offer state.
/// </summary>
public sealed class MariaDbAutoTradeStateStore : ILegacyAutoTradeStateStore
{
    private const int CharacterCount = 4;
    private readonly Func<DbConnection> connectionFactory;
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public MariaDbAutoTradeStateStore(Func<DbConnection> connectionFactory, string worldKey)
    {
        this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        if (!LegacyServerModePolicy.TryParseWorldKey(worldKey, out var mode))
            throw new ArgumentException("World key must be UP or PVP.", nameof(worldKey));

        WorldKey = new LegacyServerModePolicy(mode).WorldKey;
    }

    public string WorldKey { get; }

    public async ValueTask<LegacyAutoTradeStateResult> SaveAsync(
        string accountName,
        int characterSlot,
        LegacyAutoTradeSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!TryNormalizeOwner(accountName, characterSlot, out var normalizedAccount))
            return LegacyAutoTradeStateResult.InvalidOwner;
        if (!TryCreatePersistedListing(normalizedAccount, characterSlot, snapshot, out var listing))
            return LegacyAutoTradeStateResult.InvalidListing;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO wyd_autotrade_state (world_key, account_name, character_slot, listing_json) VALUES (@world_key, @account_name, @character_slot, @listing_json) ON DUPLICATE KEY UPDATE listing_json = VALUES(listing_json), updated_at = CURRENT_TIMESTAMP";
        AddParameter(command, "@world_key", WorldKey);
        AddParameter(command, "@account_name", normalizedAccount);
        AddParameter(command, "@character_slot", characterSlot);
        AddParameter(command, "@listing_json", JsonSerializer.Serialize(listing, jsonOptions));
        await command.ExecuteNonQueryAsync(cancellationToken);
        return LegacyAutoTradeStateResult.Saved;
    }

    public async ValueTask<LegacyAutoTradePersistedListing?> ReadAsync(
        string accountName,
        int characterSlot,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeOwner(accountName, characterSlot, out var normalizedAccount))
            return null;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT listing_json FROM wyd_autotrade_state WHERE world_key = @world_key AND account_name = @account_name AND character_slot = @character_slot LIMIT 1";
        AddOwnerParameters(command, normalizedAccount, characterSlot);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull
            ? null
            : DeserializeListing(value, normalizedAccount, characterSlot);
    }

    public async ValueTask<IReadOnlyList<LegacyAutoTradePersistedListing>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT account_name, character_slot, listing_json FROM wyd_autotrade_state WHERE world_key = @world_key ORDER BY account_name, character_slot";
        AddParameter(command, "@world_key", WorldKey);

        var listings = new List<LegacyAutoTradePersistedListing>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var accountName = reader.GetString(0);
            var characterSlot = reader.GetByte(1);
            var listing = DeserializeListing(reader.GetValue(2), accountName, characterSlot);
            listings.Add(listing);
        }

        return listings;
    }

    public async ValueTask<LegacyAutoTradeStateResult> RemoveAsync(
        string accountName,
        int characterSlot,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeOwner(accountName, characterSlot, out var normalizedAccount))
            return LegacyAutoTradeStateResult.InvalidOwner;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM wyd_autotrade_state WHERE world_key = @world_key AND account_name = @account_name AND character_slot = @character_slot";
        AddOwnerParameters(command, normalizedAccount, characterSlot);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 0
            ? LegacyAutoTradeStateResult.NotFound
            : LegacyAutoTradeStateResult.Removed;
    }

    private LegacyAutoTradePersistedListing DeserializeListing(object value, string accountName, int characterSlot)
    {
        var json = value switch
        {
            string text => text,
            byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
            _ => throw new InvalidDataException("MariaDB autotrade listing_json is not text or bytes."),
        };
        var listing = JsonSerializer.Deserialize<LegacyAutoTradePersistedListing>(json, jsonOptions)
            ?? throw new InvalidDataException("MariaDB autotrade listing_json is empty.");
        if (!string.Equals(listing.AccountName, accountName, StringComparison.Ordinal) || listing.CharacterSlot != characterSlot ||
            listing.Items.Count != Protocol.AutoTradeListConfirmation.SlotCount ||
            listing.CarryPositions.Count != Protocol.AutoTradeListConfirmation.SlotCount ||
            listing.Prices.Count != Protocol.AutoTradeListConfirmation.SlotCount)
            throw new InvalidDataException("MariaDB autotrade listing_json owner or slot data does not match its row.");
        return listing;
    }

    private void AddOwnerParameters(DbCommand command, string accountName, int characterSlot)
    {
        AddParameter(command, "@world_key", WorldKey);
        AddParameter(command, "@account_name", accountName);
        AddParameter(command, "@character_slot", characterSlot);
    }

    private DbConnection CreateConnection() => connectionFactory() ?? throw new InvalidOperationException("The MariaDB connection factory returned null.");

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static bool TryCreatePersistedListing(
        string accountName,
        int characterSlot,
        LegacyAutoTradeSnapshot snapshot,
        out LegacyAutoTradePersistedListing listing)
    {
        listing = default!;
        if (snapshot.ConnectionId <= 0 ||
            snapshot.Items.Count != Protocol.AutoTradeListConfirmation.SlotCount ||
            snapshot.CarryPositions.Count != Protocol.AutoTradeListConfirmation.SlotCount ||
            snapshot.Prices.Count != Protocol.AutoTradeListConfirmation.SlotCount)
            return false;

        try
        {
            _ = new Protocol.AutoTradeListConfirmation(
                snapshot.Title,
                snapshot.Items,
                snapshot.CarryPositions,
                snapshot.Prices,
                snapshot.Tax,
                targetId: 1);
        }
        catch (ArgumentException)
        {
            return false;
        }

        listing = new LegacyAutoTradePersistedListing(
            accountName,
            characterSlot,
            snapshot.PositionX,
            snapshot.PositionY,
            snapshot.Title,
            snapshot.Items.ToArray(),
            snapshot.CarryPositions.ToArray(),
            snapshot.Prices.ToArray(),
            snapshot.Tax);
        return true;
    }

    private static bool TryNormalizeOwner(string accountName, int characterSlot, out string normalizedAccount)
    {
        normalizedAccount = string.Empty;
        if (characterSlot is < 0 or >= CharacterCount || string.IsNullOrWhiteSpace(accountName))
            return false;

        normalizedAccount = accountName.Trim().ToUpperInvariant();
        return normalizedAccount.Length is > 0 and < 16 &&
            normalizedAccount.All(static value => value is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-');
    }
}

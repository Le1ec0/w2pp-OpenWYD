using System.Data.Common;
using System.Reflection;
using System.Text.Json;

namespace WydCdk.World;

public sealed class MariaDbConnectionOptions
{
    public string Provider { get; init; } = "MySqlConnector";
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 3306;
    public string Database { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string SslMode { get; init; } = "Preferred";
}

/// <summary>Loads a provider-neutral MariaDB connection factory without persisting credentials in source.</summary>
public static class MariaDbConnectionFactory
{
    public static Func<DbConnection> FromJson(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var options = JsonSerializer.Deserialize<MariaDbConnectionOptions>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidDataException("MariaDB configuration JSON is empty.");

        if (string.IsNullOrWhiteSpace(options.Host) || options.Port is < 1 or > 65535 || string.IsNullOrWhiteSpace(options.Database) || string.IsNullOrWhiteSpace(options.Username))
            throw new InvalidDataException("MariaDB configuration requires host, valid port, database, and username.");

        return FromOptions(options);
    }

    public static Func<DbConnection> FromOptions(MariaDbConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Host) || options.Port is < 1 or > 65535 || string.IsNullOrWhiteSpace(options.Database) || string.IsNullOrWhiteSpace(options.Username))
            throw new InvalidDataException("MariaDB configuration requires host, valid port, database, and username.");

        var factory = ResolveFactory(options.Provider);
        var connectionString = $"Server={Escape(options.Host)};Port={options.Port};Database={Escape(options.Database)};User ID={Escape(options.Username)};Password={Escape(options.Password)};SslMode={Escape(options.SslMode)};";
        return () =>
        {
            var connection = factory.CreateConnection() ?? throw new InvalidOperationException("The configured MariaDB provider could not create a connection.");
            connection.ConnectionString = connectionString;
            return connection;
        };
    }

    private static DbProviderFactory ResolveFactory(string providerName)
    {
        try
        {
            return DbProviderFactories.GetFactory(providerName);
        }
        catch (ArgumentException)
        {
            // MySqlConnector is commonly used without a machine-level provider registration.
            if (!string.Equals(providerName, "MySqlConnector", StringComparison.OrdinalIgnoreCase))
                throw;
            var type = Type.GetType("MySqlConnector.MySqlConnectorFactory, MySqlConnector", throwOnError: false)
                ?? throw new InvalidOperationException("MySqlConnector is not installed. Install the provider before enabling --donate-db-config.");
            var instance = (type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                ?? type.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)) as DbProviderFactory
                ?? throw new InvalidOperationException("MySqlConnector factory instance was not available.");
            return instance;
        }
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace(";", "\\;", StringComparison.Ordinal);
}

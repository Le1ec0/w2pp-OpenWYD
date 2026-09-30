namespace WydCdk.World;

/// <summary>Shared account-name normalization used by both legacy and MariaDB authentication.</summary>
public static class LegacyAccountNameRules
{
    public static bool TryNormalize(string accountName, out string normalized)
    {
        normalized = accountName?.ToUpperInvariant() ?? string.Empty;
        return normalized.Length is >= 1 and < 16 &&
            normalized.All(static character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-');
    }
}

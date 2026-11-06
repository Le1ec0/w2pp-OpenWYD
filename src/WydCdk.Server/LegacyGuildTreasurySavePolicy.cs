namespace WydCdk.Server;

internal static class LegacyGuildTreasurySavePolicy
{
    public static TimeSpan Interval { get; } = TimeSpan.FromMilliseconds(12_000);
}

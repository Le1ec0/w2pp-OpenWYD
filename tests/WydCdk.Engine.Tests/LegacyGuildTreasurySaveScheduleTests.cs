using WydCdk.Server;

internal static class LegacyGuildTreasurySaveScheduleTests
{
    public static void Run()
    {
        if (LegacyGuildTreasurySavePolicy.Interval != TimeSpan.FromMilliseconds(12_000))
            throw new InvalidOperationException("The treasury checkpoint interval does not match TIMER_MIN at 12,000 ms.");
    }
}

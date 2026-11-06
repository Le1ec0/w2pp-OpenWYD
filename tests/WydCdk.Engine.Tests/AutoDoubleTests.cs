using WydCdk.World;

internal static class AutoDoubleTests
{
    public static void Run()
    {
        var configuration = LegacyAutoDoubleConfiguration.Parse(CreateEventJson(
            dayStart: 6, hourStart: 23, minStart: 58,
            dayEnd: 0, hourEnd: 0, minEnd: 2));
        Check(configuration.MultEXP == 3 && configuration.DayStart == 6,
            "The EVENT.DOUBLE values were not read from the legacy eventAuto.json shape.");

        var invalid = Capture(() => LegacyAutoDoubleConfiguration.Parse(CreateEventJson(
            dayStart: 7, hourStart: 23, minStart: 58,
            dayEnd: 0, hourEnd: 0, minEnd: 2)));
        Check(invalid is InvalidDataException,
            $"An out-of-range weekday was not rejected as invalid configuration: {invalid?.GetType().Name}.");

        var schedule = new LegacyAutoDoubleSchedule(configuration);
        Check(schedule.Tick(new DateTime(2026, 10, 3, 23, 57, 0)) is null,
            "AutoDouble activated before its configured start minute.");
        var started = schedule.Tick(new DateTime(2026, 10, 3, 23, 58, 0));
        Check(started is { DoubleModeMultiplier: 3, Message: "Double EXP started", Active: true },
            "AutoDouble did not activate with the configured multiplier and start notice.");
        Check(schedule.Tick(new DateTime(2026, 10, 3, 23, 59, 0)) is null,
            "AutoDouble emitted a duplicate start transition while already active.");
        Check(schedule.Tick(new DateTime(2026, 10, 4, 0, 1, 0)) is null,
            "AutoDouble ended before its configured end minute.");
        Check(schedule.Tick(new DateTime(2026, 10, 4, 0, 2, 0)) is
                { DoubleModeMultiplier: 1, Message: "Double EXP ended", Active: false },
            "AutoDouble did not reset to 1x and emit the end notice.");
        Check(schedule.Tick(new DateTime(2026, 10, 4, 0, 3, 0)) is null,
            "AutoDouble emitted a duplicate end transition after becoming inactive.");

        var sameHourConfiguration = LegacyAutoDoubleConfiguration.Parse(CreateEventJson(
            dayStart: 1, hourStart: 10, minStart: 15,
            dayEnd: 1, hourEnd: 10, minEnd: 45));
        var missedStart = new LegacyAutoDoubleSchedule(sameHourConfiguration);
        Check(missedStart.Tick(new DateTime(2026, 10, 5, 10, 45, 0)) is null,
            "The source same-day/hour guard allowed a late start after the end minute.");
        var sameHourSchedule = new LegacyAutoDoubleSchedule(sameHourConfiguration);
        Check(sameHourSchedule.Tick(new DateTime(2026, 10, 5, 10, 15, 0)) is { Active: true } &&
            sameHourSchedule.Tick(new DateTime(2026, 10, 5, 10, 45, 0)) is { Active: false },
            "An active same-day/hour AutoDouble did not transition at its configured end minute.");

        var world = new WorldHub();
        world.SetDoubleModeMultiplier(3);
        Check(world.GetCombatWorldState().DoubleModeMultiplier == 3,
            "WorldHub did not publish the active AutoDouble multiplier to combat consumers.");
        world.SetDoubleModeMultiplier(1);
        Check(world.GetCombatWorldState().DoubleModeMultiplier == 1,
            "WorldHub did not publish the AutoDouble end reset to combat consumers.");
    }

    private static string CreateEventJson(int dayStart, int hourStart, int minStart, int dayEnd, int hourEnd, int minEnd) => $$"""
        {
          "EVENT": {
            "DOUBLE": {
              "DayStart": {{dayStart}},
              "HourStart": {{hourStart}},
              "MinStart": {{minStart}},
              "MSGStart": "Double EXP started",
              "DayEnd": {{dayEnd}},
              "HourEnd": {{hourEnd}},
              "MinEnd": {{minEnd}},
              "MultEXP": 3,
              "MSGEnd": "Double EXP ended"
            }
          }
        }
        """;

    private static Exception? Capture(Action action)
    {
        try { action(); return null; }
        catch (Exception error) { return error; }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

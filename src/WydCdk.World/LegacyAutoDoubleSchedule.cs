using System.Text.Json;
using System.Text.Json.Serialization;

namespace WydCdk.World;

public sealed record LegacyAutoDoubleConfiguration(
    int DayStart,
    int HourStart,
    int MinStart,
    string MSGStart,
    int DayEnd,
    int HourEnd,
    int MinEnd,
    int MultEXP,
    string MSGEnd)
{
    public static LegacyAutoDoubleConfiguration Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        EventAutoDocument document;
        try
        {
            document = JsonSerializer.Deserialize<EventAutoDocument>(json)
                ?? throw new InvalidDataException("AutoDouble event configuration is empty.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("AutoDouble event configuration is not valid eventAuto.json.", error);
        }

        var values = document.Event?.Double
            ?? throw new InvalidDataException("AutoDouble event configuration must include EVENT.DOUBLE.");
        var configuration = new LegacyAutoDoubleConfiguration(
            values.DayStart,
            values.HourStart,
            values.MinStart,
            values.MessageStart ?? throw new InvalidDataException("EVENT.DOUBLE.MSGStart is required."),
            values.DayEnd,
            values.HourEnd,
            values.MinEnd,
            values.Multiplier ?? throw new InvalidDataException("EVENT.DOUBLE.MultEXP is required."),
            values.MessageEnd ?? throw new InvalidDataException("EVENT.DOUBLE.MSGEnd is required."));
        configuration.Validate();
        return configuration;
    }

    public void Validate()
    {
        if (DayStart is < 0 or > 6 || DayEnd is < 0 or > 6)
            throw new InvalidDataException("AutoDouble DayStart and DayEnd must use Sunday=0 through Saturday=6.");
        if (HourStart is < 0 or > 23 || HourEnd is < 0 or > 23)
            throw new InvalidDataException("AutoDouble start and end hours must be between 0 and 23.");
        if (MinStart is < 0 or > 59 || MinEnd is < 0 or > 59)
            throw new InvalidDataException("AutoDouble start and end minutes must be between 0 and 59.");
        ArgumentNullException.ThrowIfNull(MSGStart);
        ArgumentNullException.ThrowIfNull(MSGEnd);
    }

    private sealed class EventAutoDocument
    {
        [JsonPropertyName("EVENT")]
        [JsonRequired]
        public EventSection? Event { get; init; }
    }

    private sealed class EventSection
    {
        [JsonPropertyName("DOUBLE")]
        [JsonRequired]
        public DoubleSection? Double { get; init; }
    }

    private sealed class DoubleSection
    {
        [JsonPropertyName("DayStart")]
        [JsonRequired]
        public int DayStart { get; init; }

        [JsonPropertyName("HourStart")]
        [JsonRequired]
        public int HourStart { get; init; }

        [JsonPropertyName("MinStart")]
        [JsonRequired]
        public int MinStart { get; init; }

        [JsonPropertyName("MSGStart")]
        [JsonRequired]
        public string? MessageStart { get; init; }

        [JsonPropertyName("DayEnd")]
        [JsonRequired]
        public int DayEnd { get; init; }

        [JsonPropertyName("HourEnd")]
        [JsonRequired]
        public int HourEnd { get; init; }

        [JsonPropertyName("MinEnd")]
        [JsonRequired]
        public int MinEnd { get; init; }

        [JsonPropertyName("MultEXP")]
        [JsonRequired]
        public int? Multiplier { get; init; }

        [JsonPropertyName("MSGEnd")]
        [JsonRequired]
        public string? MessageEnd { get; init; }
    }
}

public sealed class LegacyAutoDoubleSchedule(LegacyAutoDoubleConfiguration configuration)
{
    private readonly LegacyAutoDoubleConfiguration configuration = GetValidatedConfiguration(configuration);
    private bool active;

    private static LegacyAutoDoubleConfiguration GetValidatedConfiguration(LegacyAutoDoubleConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.Validate();
        return configuration;
    }

    public LegacyAutoDoubleTransition? Tick(DateTime localNow)
    {
        var startDayAndHour = (int)localNow.DayOfWeek == configuration.DayStart &&
            localNow.Hour == configuration.HourStart;
        var endDayAndHour = (int)localNow.DayOfWeek == configuration.DayEnd &&
            localNow.Hour == configuration.HourEnd;

        if (startDayAndHour && localNow.Minute >= configuration.MinStart && !active)
        {
            var startAndEndShareHour = configuration.DayStart == configuration.DayEnd &&
                configuration.HourStart == configuration.HourEnd;
            if (startAndEndShareHour && localNow.Minute >= configuration.MinEnd)
                return null;

            active = true;
            return new LegacyAutoDoubleTransition(configuration.MultEXP, configuration.MSGStart, Active: true);
        }

        if (endDayAndHour && localNow.Minute >= configuration.MinEnd && active)
        {
            active = false;
            return new LegacyAutoDoubleTransition(1, configuration.MSGEnd, Active: false);
        }

        return null;
    }
}

public sealed record LegacyAutoDoubleTransition(int DoubleModeMultiplier, string Message, bool Active);

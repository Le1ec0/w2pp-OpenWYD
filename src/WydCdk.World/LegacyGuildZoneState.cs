using System.Globalization;

namespace WydCdk.World;

/// <summary>
/// Read-only compatibility view of TMSrv's Guild.txt state.
/// CReadFiles::ReadGuild reads five values for each guild-zone field.
/// </summary>
public sealed class LegacyGuildZoneState
{
    public const int ZoneCount = 5;

    public LegacyGuildZoneState(int guildCounter, IReadOnlyList<int> chargeGuilds, IReadOnlyList<int> challengerGuilds, IReadOnlyList<int> cityTaxes, IReadOnlyList<int> clans, IReadOnlyList<int> victories)
    {
        GuildCounter = guildCounter;
        ChargeGuilds = CopyZoneValues(chargeGuilds, nameof(chargeGuilds));
        ChallengerGuilds = CopyZoneValues(challengerGuilds, nameof(challengerGuilds));
        CityTaxes = CopyZoneValues(cityTaxes, nameof(cityTaxes));
        Clans = CopyZoneValues(clans, nameof(clans));
        Victories = CopyZoneValues(victories, nameof(victories));
    }

    public int GuildCounter { get; }
    public IReadOnlyList<int> ChargeGuilds { get; }
    public IReadOnlyList<int> ChallengerGuilds { get; }
    public IReadOnlyList<int> CityTaxes { get; }
    public IReadOnlyList<int> Clans { get; }
    public IReadOnlyList<int> Victories { get; }

    public static LegacyGuildZoneState Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var values = File.ReadAllText(path).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (values.Length < 1 + (ZoneCount * 5))
            throw new InvalidDataException($"Guild.txt must contain at least {1 + (ZoneCount * 5)} integer values.");

        var index = 0;
        var guildCounter = ParseInt(values[index++], "GuildCounter");
        var chargeGuilds = ReadValues(values, ref index);
        var challengerGuilds = ReadValues(values, ref index);
        var cityTaxes = ReadValues(values, ref index);
        for (var zone = 0; zone < ZoneCount; zone++)
            if (cityTaxes[zone] is < 0 or > 20)
                cityTaxes[zone] = 10;
        var clans = ReadValues(values, ref index);
        var victories = ReadValues(values, ref index);
        return new LegacyGuildZoneState(guildCounter, chargeGuilds, challengerGuilds, cityTaxes, clans, victories);
    }

    public int GetChargeGuild(int zone) => zone is >= 0 and < ZoneCount ? ChargeGuilds[zone] : 0;

    private static int[] ReadValues(string[] values, ref int index)
    {
        var result = new int[ZoneCount];
        for (var zone = 0; zone < ZoneCount; zone++)
            result[zone] = ParseInt(values[index++], $"Guild zone {zone}");
        return result;
    }

    private static int ParseInt(string value, string field) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new InvalidDataException($"Guild.txt field {field} is not an integer: '{value}'.");

    private static int[] CopyZoneValues(IReadOnlyList<int> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count != ZoneCount)
            throw new ArgumentException($"{name} must contain exactly {ZoneCount} values.", name);
        return values.ToArray();
    }
}

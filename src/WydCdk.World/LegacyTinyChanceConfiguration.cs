using System.Globalization;

namespace WydCdk.World;

/// <summary>Reads the Tiny ChanceBase setting from the legacy CompRate.txt format.</summary>
public static class LegacyTinyChanceConfiguration
{
    public const int DefaultChanceBase = 15;

    public static int ParseCompRate(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var chanceBase = DefaultChanceBase;
        var lineNumber = 0;
        foreach (var line in lines)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 2 ||
                !fields[0].Equals("TINY", StringComparison.OrdinalIgnoreCase) ||
                !fields[1].Equals("CHANCEBASE", StringComparison.OrdinalIgnoreCase))
                continue;

            if (fields.Length < 3 ||
                !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ||
                parsed is < 0 or > 100)
                throw new InvalidDataException($"CompRate.txt has an invalid Tiny ChanceBase on line {lineNumber}; expected an integer from 0 to 100.");

            chanceBase = parsed;
        }

        return chanceBase;
    }

    public static int ResolveBase(int? commandLineOverride, int? fileValue)
    {
        var chanceBase = commandLineOverride ?? fileValue ?? DefaultChanceBase;
        if (chanceBase is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(commandLineOverride), "Tiny ChanceBase must be between 0 and 100.");
        return chanceBase;
    }
}

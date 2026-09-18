using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// One Castle Zakum quest entry from TMSrv's CastleQuest.txt. The field order and
/// the unusual SubCelestial mapping intentionally follow CCastleZakum::ParseCastleString.
/// </summary>
public sealed record LegacyCastleQuestDefinition(
    int MobInitial,
    int MobEnd,
    int Boss1,
    int Boss2,
    IReadOnlyList<LegacyItem> Prizes,
    int CoinPrize,
    IReadOnlyList<int> ExpPrize,
    bool PartyPrize,
    int QuestTime)
{
    public bool MatchesBoss(int generateIndex) => generateIndex == Boss1 || generateIndex == Boss2;
}

public static class LegacyCastleQuestConfiguration
{
    public const int MaxQuestCount = 64;
    public const int MaxCarry = LegacyAccountSnapshot.MobCarryCount;

    public static IReadOnlyList<LegacyCastleQuestDefinition> Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var reader = File.OpenText(path);
        return Load(reader);
    }

    public static IReadOnlyList<LegacyCastleQuestDefinition> Load(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var definitions = new List<LegacyCastleQuestDefinition>();
        Builder? current = null;

        string? rawLine;
        while ((rawLine = reader.ReadLine()) is not null)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                continue;
            var comment = line.IndexOf("//", StringComparison.Ordinal);
            if (comment >= 0)
                line = line[..comment].Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith('#'))
            {
                if (current is not null)
                    definitions.Add(current.Build());
                if (definitions.Count >= MaxQuestCount)
                    throw new InvalidDataException($"CastleQuest.txt contains more than {MaxQuestCount} entries.");
                current = new Builder();
                continue;
            }

            if (current is null)
                continue;
            var separator = line.IndexOf(':');
            if (separator <= 0)
                continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            current.Apply(key, value);
        }

        if (current is not null)
            definitions.Add(current.Build());
        return definitions;
    }

    private sealed class Builder
    {
        private readonly LegacyItem[] prizes = new LegacyItem[MaxCarry];
        private readonly int[] expPrize = new int[6];
        private int mobInitial;
        private int mobEnd;
        private int boss1;
        private int boss2;
        private int coinPrize;
        private bool partyPrize;
        private int questTime;

        public void Apply(string rawKey, string rawValue)
        {
            var key = rawKey.Trim().ToUpperInvariant();
            var value = rawValue.Trim();
            switch (key)
            {
                case "MOB_INITIAL":
                    mobInitial = ParseInt(value, key);
                    break;
                case "MOB_END":
                    mobEnd = ParseInt(value, key);
                    break;
                case "BOSS1":
                    boss1 = ParseInt(value, key);
                    break;
                case "BOSS2":
                    boss2 = ParseInt(value, key);
                    break;
                case "COINPRIZE":
                    coinPrize = ParseInt(value, key);
                    break;
                case "EXPPRIZE_ARCH":
                    expPrize[1] = ParseInt(value, key);
                    break;
                case "EXPPRIZE_MORTAL":
                    expPrize[2] = ParseInt(value, key);
                    break;
                case "EXPPRIZE_CELESTIAL":
                    expPrize[3] = ParseInt(value, key);
                    expPrize[4] = expPrize[3];
                    break;
                case "EXPPRIZE_SUBCELESTIAL":
                    // This overwrites index 2 in the original parser, despite the name.
                    expPrize[2] = ParseInt(value, key);
                    break;
                case "PARTYPRIZE":
                    partyPrize = value.Equals("ON", StringComparison.OrdinalIgnoreCase);
                    break;
                case "QUESTTIME":
                    questTime = ParseInt(value, key);
                    break;
                default:
                    if (key.StartsWith("PRIZE_", StringComparison.Ordinal) &&
                        int.TryParse(key.AsSpan("PRIZE_".Length), out var slot) &&
                        slot >= 0 && slot < prizes.Length)
                        prizes[slot] = ParsePrize(value, key);
                    break;
            }
        }

        public LegacyCastleQuestDefinition Build()
            => new(mobInitial, mobEnd, boss1, boss2, prizes.ToArray(), coinPrize, expPrize.ToArray(), partyPrize, questTime);

        private static LegacyItem ParsePrize(string value, string key)
        {
            var fields = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 7)
                throw new InvalidDataException($"{key} requires seven integer item fields.");
            return new LegacyItem(
                checked((short)ParseInt(fields[0], key)),
                checked((byte)ParseInt(fields[1], key)),
                checked((byte)ParseInt(fields[2], key)),
                checked((byte)ParseInt(fields[3], key)),
                checked((byte)ParseInt(fields[4], key)),
                checked((byte)ParseInt(fields[5], key)),
                checked((byte)ParseInt(fields[6], key)));
        }

        private static int ParseInt(string value, string key)
            => int.TryParse(value, out var parsed)
                ? parsed
                : throw new InvalidDataException($"{key} has invalid integer value '{value}'.");
    }
}

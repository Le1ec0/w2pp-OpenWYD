using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace WydCdk.World;

/// <summary>
/// Read-only subset of the legacy CNPCGenerator data needed to create a leader
/// NPC from NPCGener.txt. The parser intentionally follows CNPCGene.cpp's
/// key/value names instead of inventing a new data format.
/// </summary>
public sealed class LegacyNpcGenerationCatalog
{
    private readonly Dictionary<int, LegacyNpcGenerationDefinition> definitions;
    private readonly Dictionary<string, byte[]> templates;

    private LegacyNpcGenerationCatalog(
        Dictionary<int, LegacyNpcGenerationDefinition> definitions,
        Dictionary<string, byte[]> templates)
    {
        this.definitions = definitions;
        this.templates = templates;
    }

    public static LegacyNpcGenerationCatalog Load(string npcGenerationPath, string npcRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(npcGenerationPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(npcRoot);

        var text = File.ReadAllText(npcGenerationPath, Encoding.Latin1);
        var definitions = ParseDefinitions(text);
        var templates = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in definitions.Values
                     .SelectMany(static definition => new[] { definition.LeaderName, definition.FollowerName })
                     .Where(static name => !string.IsNullOrWhiteSpace(name))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var path = Path.Combine(npcRoot, name);
            var mob = File.ReadAllBytes(path);
            if (mob.Length != LegacyAccountSnapshot.CharacterStride)
                throw new InvalidDataException($"NPC template '{path}' must contain exactly {LegacyAccountSnapshot.CharacterStride} bytes.");
            templates[name] = mob;
        }

        return new LegacyNpcGenerationCatalog(definitions, templates);
    }

    public static LegacyNpcGenerationCatalog Parse(string text, IReadOnlyDictionary<string, byte[]> templates)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(templates);

        var copiedTemplates = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in templates)
        {
            if (pair.Value.Length != LegacyAccountSnapshot.CharacterStride)
                throw new InvalidDataException($"NPC template '{pair.Key}' must contain exactly {LegacyAccountSnapshot.CharacterStride} bytes.");
            copiedTemplates[pair.Key] = pair.Value.ToArray();
        }

        return new LegacyNpcGenerationCatalog(ParseDefinitions(text), copiedTemplates);
    }

    public bool TryGet(int generateIndex, out LegacyNpcGenerationDefinition? definition)
    {
        if (definitions.TryGetValue(generateIndex, out var found))
        {
            definition = found;
            return true;
        }

        definition = null;
        return false;
    }

    public IReadOnlyList<LegacyNpcGenerationDefinition> Definitions => definitions.Values
        .OrderBy(static definition => definition.GenerateIndex)
        .ToArray();

    /// <summary>Creates the leader exactly once, using the legacy start range.</summary>
    public bool TryCreateLeader(int generateIndex, Func<int, int>? randomRoll, out LegacyGeneratedNpc? generated)
    {
        generated = null;
        if (!definitions.TryGetValue(generateIndex, out var definition) ||
            !templates.TryGetValue(definition.LeaderName, out var template))
            return false;

        var mob = template.ToArray();
        for (var offset = LegacyAccountSnapshot.MobNameOffset; offset < LegacyAccountSnapshot.MobNameOffset + 16; offset++)
        {
            if (mob[offset] is (byte)'_' or (byte)'@')
                mob[offset] = (byte)' ';
        }

        // GenerateMob clears BaseScore.Merchant and restores the leader's HP
        // to MaxHp after copying the NPC template.
        mob[LegacyAccountSnapshot.MobBaseScoreOffset + 12] = 0;
        var maxHp = BinaryPrimitives.ReadInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 16));
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), Math.Max(0, maxHp));

        randomRoll ??= static maximum => Random.Shared.Next(maximum);
        var roll = randomRoll;
        var positionX = ResolveStartCoordinate(definition.StartX, definition.StartRange, roll);
        var positionY = ResolveStartCoordinate(definition.StartY, definition.StartRange, roll);
        generated = new LegacyGeneratedNpc(
            generateIndex,
            checked((short)positionX),
            checked((short)positionY),
            mob,
            new byte[LegacyAccountSnapshot.AffectStride],
            definition.LeaderName);
        return true;
    }

    private static int ResolveStartCoordinate(int coordinate, int range, Func<int, int> randomRoll)
    {
        if (range <= 0)
            return coordinate;

        var maximum = checked(range + 1);
        var value = randomRoll(maximum);
        if (value < 0 || value >= maximum)
            throw new ArgumentOutOfRangeException(nameof(randomRoll), "The NPC generation roll must be inside its requested range.");
        return coordinate - range + value;
    }

    private static Dictionary<int, LegacyNpcGenerationDefinition> ParseDefinitions(string text)
    {
        var result = new Dictionary<int, LegacyNpcGenerationDefinition>();
        LegacyNpcGenerationBuilder? current = null;

        foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                continue;

            if (line[0] == '#')
            {
                var open = line.IndexOf('[');
                var close = line.IndexOf(']', open + 1);
                if (open >= 0 && close > open &&
                    int.TryParse(line.AsSpan(open + 1, close - open - 1).Trim().TrimStart('*'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
                {
                    CommitCurrent(result, current);
                    current = new LegacyNpcGenerationBuilder(index);
                }
                continue;
            }

            if (current is null)
                continue;

            var separator = line.IndexOf(':');
            if (separator <= 0)
                continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            current.Assign(key, value);
        }

        CommitCurrent(result, current);
        return result;
    }

    private static void CommitCurrent(Dictionary<int, LegacyNpcGenerationDefinition> result, LegacyNpcGenerationBuilder? current)
    {
        if (current is null || string.IsNullOrWhiteSpace(current.LeaderName))
            return;
        result[current.GenerateIndex] = current.Build();
    }

    private sealed class LegacyNpcGenerationBuilder(int generateIndex)
    {
        public int GenerateIndex { get; } = generateIndex;
        public int MinuteGenerate { get; private set; }
        public int MaxNumMob { get; private set; }
        public int MinGroup { get; private set; }
        public int MaxGroup { get; private set; }
        public string LeaderName { get; private set; } = string.Empty;
        public string FollowerName { get; private set; } = string.Empty;
        public int RouteType { get; private set; }
        public int Formation { get; private set; }
        public int StartX { get; private set; }
        public int StartY { get; private set; }
        public int StartRange { get; private set; }

        public void Assign(string key, string value)
        {
            switch (key)
            {
                case "MinuteGenerate": MinuteGenerate = ParseInt(value); break;
                case "MaxNumMob": MaxNumMob = ParseInt(value); break;
                case "MinGroup": MinGroup = ParseInt(value); break;
                case "MaxGroup": MaxGroup = ParseInt(value); break;
                case "Leader": LeaderName = value; break;
                case "Follower": FollowerName = value; break;
                case "RouteType": RouteType = ParseInt(value); break;
                case "Formation": Formation = ParseInt(value); break;
                case "StartX": StartX = ParseInt(value); break;
                case "StartY": StartY = ParseInt(value); break;
                case "StartRange": StartRange = ParseInt(value); break;
            }
        }

        public LegacyNpcGenerationDefinition Build() => new(
            GenerateIndex,
            MinuteGenerate,
            MaxNumMob,
            MinGroup,
            MaxGroup,
            LeaderName,
            FollowerName,
            RouteType,
            Formation,
            StartX,
            StartY,
            StartRange);

        private static int ParseInt(string value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }
}

public sealed record LegacyNpcGenerationDefinition(
    int GenerateIndex,
    int MinuteGenerate,
    int MaxNumMob,
    int MinGroup,
    int MaxGroup,
    string LeaderName,
    string FollowerName,
    int RouteType,
    int Formation,
    int StartX,
    int StartY,
    int StartRange);

public sealed record LegacyGeneratedNpc(
    int GenerateIndex,
    short PositionX,
    short PositionY,
    byte[] MobSnapshot,
    byte[] AffectSnapshot,
    string SourceName);

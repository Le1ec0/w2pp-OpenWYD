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
    public const int MaximumPartyMembers = 12;

    // TMProject declares this as char[5][12][2], while GenerateMob indexes it
    // as [follower][routePoint][formation]. Keep the exact contiguous source
    // bytes and bound the resulting pointer-style lookup below.
    private static readonly int[] FormationOffsets =
    [
        1, 1, -1, 1, 1, -1, -1, -1, 1, 0, -1, 0, 0, 1, 0, -1, 2, 0, -2, 0, 0, 2, 0, -2,
        1, 0, -1, 0, 2, 0, -2, 0, 3, 0, -3, 0, 4, 0, -4, 0, 5, 0, -5, 0, 0, 6, 0, -6,
        1, 1, -1, 1, 1, -1, -1, -1, 1, 0, -1, 0, 0, 1, 0, -1, 2, 0, -2, 0, 0, 2, 0, -2,
        1, 0, -1, 0, 2, 0, -2, 0, 3, 0, -3, 0, 4, 0, -4, 0, 5, 0, -5, 0, 0, 6, 0, -6,
        2, 0, 0, 2, 1, 1, 0, 1, 1, 0, -1, 3, 3, -1, -1, 2, 2, -1, -1, 1, 1, -1, 1, 2,
    ];

    private readonly Dictionary<int, LegacyNpcGenerationDefinition> definitions;
    private readonly Dictionary<string, byte[]> templates;

    private LegacyNpcGenerationCatalog(
        Dictionary<int, LegacyNpcGenerationDefinition> definitions,
        Dictionary<string, byte[]> templates)
    {
        this.definitions = definitions;
        this.templates = templates;
    }

    public static LegacyNpcGenerationCatalog Load(
        string npcGenerationPath,
        string npcRoot,
        IReadOnlyCollection<string>? optionalTemplateNames = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(npcGenerationPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(npcRoot);

        var root = Path.GetFullPath(npcRoot);
        var text = File.ReadAllText(npcGenerationPath, Encoding.Latin1);
        var definitions = ParseDefinitions(text);
        var templates = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in definitions.Values
                     .SelectMany(static definition => new[] { definition.LeaderName, definition.FollowerName })
                     .Where(static name => !string.IsNullOrWhiteSpace(name))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var path = Path.Combine(root, name);
            var mob = File.ReadAllBytes(path);
            if (mob.Length != LegacyAccountSnapshot.CharacterStride)
                throw new InvalidDataException($"NPC template '{path}' must contain exactly {LegacyAccountSnapshot.CharacterStride} bytes.");
            templates[name] = mob;
        }

        foreach (var name in optionalTemplateNames ?? [])
        {
            if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name)
                throw new ArgumentException("Optional NPC template names must be simple file names.", nameof(optionalTemplateNames));
            if (templates.ContainsKey(name))
                continue;

            var path = Path.Combine(root, name);
            if (!File.Exists(path))
                continue;
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

    /// <summary>Selects the follower count before leader route rolls, as GenerateMob does.</summary>
    public bool TrySelectFollowerCount(
        int generateIndex,
        int currentCount,
        Func<int, int>? randomRoll,
        out int followerCount)
    {
        followerCount = 0;
        if (!definitions.TryGetValue(generateIndex, out var definition))
            return false;

        randomRoll ??= static maximum => Random.Shared.Next(maximum);
        var rollRange = definition.MaxGroup - definition.MinGroup + 1;
        if (rollRange <= 0)
            rollRange = 1;

        var roll = randomRoll(rollRange);
        if (roll < 0 || roll >= rollRange)
            throw new ArgumentOutOfRangeException(nameof(randomRoll), "The NPC group roll must be inside its requested range.");

        var selectedCount = definition.MinGroup + roll;
        if (currentCount >= definition.MaxNumMob)
            return false;

        // The legacy count is clipped before the leader is incremented. This
        // can produce one more live mob than MaxNumMob for that spawn.
        if (currentCount + selectedCount > definition.MaxNumMob)
            selectedCount = definition.MaxNumMob - currentCount;

        followerCount = selectedCount;
        return true;
    }

    /// <summary>Creates the leader exactly once, using the legacy start range.</summary>
    public bool TryCreateLeader(int generateIndex, Func<int, int>? randomRoll, out LegacyGeneratedNpc? generated)
        => TryCreateLeader(generateIndex, generateIndex, randomRoll, out generated);

    /// <summary>Creates the source generator's leader on a separately selected route.</summary>
    public bool TryCreateLeader(
        int sourceGenerateIndex,
        int routeGenerateIndex,
        Func<int, int>? randomRoll,
        out LegacyGeneratedNpc? generated)
    {
        generated = null;
        if (!definitions.TryGetValue(sourceGenerateIndex, out var sourceDefinition) ||
            !definitions.TryGetValue(routeGenerateIndex, out var routeDefinition) ||
            !templates.TryGetValue(sourceDefinition.LeaderName, out var template))
            return false;

        var mob = template.ToArray();
        for (var offset = LegacyAccountSnapshot.MobNameOffset; offset < LegacyAccountSnapshot.MobNameOffset + 16; offset++)
        {
            if (mob[offset] is (byte)'_' or (byte)'@')
                mob[offset] = (byte)' ';
        }

        // GenerateMob clears BaseScore.Merchant (distinct from STRUCT_MOB.Merchant)
        // and restores the leader's HP to MaxHp after copying the NPC template.
        mob[LegacyAccountSnapshot.MobBaseScoreOffset + 12] = 0;
        var maxHp = BinaryPrimitives.ReadInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 16));
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), Math.Max(0, maxHp));

        randomRoll ??= static maximum => Random.Shared.Next(maximum);
        var route = ResolveGeneratedRoute(routeDefinition.Route, randomRoll);
        generated = new LegacyGeneratedNpc(
            routeGenerateIndex,
            checked((short)route.Start.X),
            checked((short)route.Start.Y),
            mob,
            new byte[LegacyAccountSnapshot.AffectStride],
            sourceDefinition.LeaderName)
        {
            GenerationRoute = route,
        };
        return true;
    }

    /// <summary>Creates one follower from the legacy template and its leader's resolved route.</summary>
    public bool TryCreateFollower(
        int generateIndex,
        int followerIndex,
        LegacyNpcGenerationRoute leaderRoute,
        Func<int, int>? randomRoll,
        short? forcedPositionX,
        short? forcedPositionY,
        out LegacyGeneratedNpc? generated)
        => TryCreateFollower(
            generateIndex,
            generateIndex,
            followerIndex,
            leaderRoute,
            randomRoll,
            forcedPositionX,
            forcedPositionY,
            out generated);

    /// <summary>Creates a source generator's follower using a separately selected route.</summary>
    public bool TryCreateFollower(
        int sourceGenerateIndex,
        int routeGenerateIndex,
        int followerIndex,
        LegacyNpcGenerationRoute leaderRoute,
        Func<int, int>? randomRoll,
        short? forcedPositionX,
        short? forcedPositionY,
        out LegacyGeneratedNpc? generated)
    {
        if (forcedPositionX.HasValue != forcedPositionY.HasValue)
            throw new ArgumentException("A forced follower position requires both X and Y.");

        generated = null;
        if (followerIndex < 0 || followerIndex >= MaximumPartyMembers ||
            !definitions.TryGetValue(sourceGenerateIndex, out var sourceDefinition) ||
            !definitions.TryGetValue(routeGenerateIndex, out var routeDefinition) ||
            !templates.TryGetValue(sourceDefinition.FollowerName, out var template))
            return false;

        randomRoll ??= static maximum => Random.Shared.Next(maximum);
        var mob = template.ToArray();
        mob[LegacyAccountSnapshot.MobBaseScoreOffset + 12] = 0;
        for (var offset = LegacyAccountSnapshot.MobNameOffset; offset < LegacyAccountSnapshot.MobNameOffset + 16; offset++)
        {
            if (mob[offset] == (byte)'_')
                mob[offset] = (byte)' ';
            else if (mob[offset] == (byte)'@')
                mob[offset] = 0;
        }

        var maxHp = BinaryPrimitives.ReadInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 16));
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), Math.Max(0, maxHp));
        var route = ResolveFollowerRoute(
            sourceDefinition,
            routeDefinition,
            leaderRoute,
            followerIndex,
            randomRoll,
            forcedPositionX,
            forcedPositionY);
        generated = new LegacyGeneratedNpc(
            routeGenerateIndex,
            checked((short)route.Start.X),
            checked((short)route.Start.Y),
            mob,
            new byte[LegacyAccountSnapshot.AffectStride],
            sourceDefinition.FollowerName)
        {
            GenerationRoute = route,
        };
        return true;
    }

    /// <summary>Creates a one-off NPC from a named template, matching the data initialization in TMSrv/CreateMob.</summary>
    public bool TryCreateNamedNpc(string mobName, short positionX, short positionY, out LegacyGeneratedNpc? generated)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mobName);
        generated = null;
        if (!templates.TryGetValue(mobName, out var template))
            return false;

        var mob = template.ToArray();
        var encodedName = Encoding.ASCII.GetBytes(mobName);
        if (encodedName.Length >= 16)
            throw new ArgumentOutOfRangeException(nameof(mobName), "Legacy NPC names must fit in the 16-byte MOB name field.");
        mob.AsSpan(LegacyAccountSnapshot.MobNameOffset, 16).Clear();
        encodedName.CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset));
        for (var offset = LegacyAccountSnapshot.MobNameOffset; offset < LegacyAccountSnapshot.MobNameOffset + 16; offset++)
        {
            if (mob[offset] == (byte)'_')
                mob[offset] = (byte)' ';
            else if (mob[offset] == (byte)'@')
                mob[offset] = 0;
        }

        // CreateMob also calls GetCurrentScore; the current C# NPC path preserves the loaded score fields,
        // clears BaseScore.Merchant (distinct from STRUCT_MOB.Merchant), initializes Affect, and restores current HP to the template MaxHP.
        mob[LegacyAccountSnapshot.MobBaseScoreOffset + 12] = 0;
        var maxHp = BinaryPrimitives.ReadInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 16));
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), Math.Max(0, maxHp));
        generated = new LegacyGeneratedNpc(
            -1,
            positionX,
            positionY,
            mob,
            new byte[LegacyAccountSnapshot.AffectStride],
            mobName);
        return true;
    }

    private static int ResolveStartCoordinate(int coordinate, int range, Func<int, int> randomRoll)
    {
        if (range <= 0)
            return coordinate;

        var value = Roll(randomRoll, checked(range + 1));
        return coordinate - range + value;
    }

    private static LegacyNpcGenerationRoute ResolveFollowerRoute(
        LegacyNpcGenerationDefinition sourceDefinition,
        LegacyNpcGenerationDefinition routeDefinition,
        LegacyNpcGenerationRoute leaderRoute,
        int followerIndex,
        Func<int, int> randomRoll,
        short? forcedPositionX,
        short? forcedPositionY)
    {
        var sourcePoints = new[]
        {
            routeDefinition.Route.Start,
            routeDefinition.Route.Segment1,
            routeDefinition.Route.Segment2,
            routeDefinition.Route.Segment3,
            routeDefinition.Route.Dest,
        };
        var leaderPoints = new[]
        {
            leaderRoute.Start,
            leaderRoute.Segment1,
            leaderRoute.Segment2,
            leaderRoute.Segment3,
            leaderRoute.Dest,
        };
        var followerPoints = new LegacyNpcGenerationRoutePoint[sourcePoints.Length];
        var hasForcedPosition = forcedPositionX is { } forcedX && forcedX != 0 &&
                                forcedPositionY is { } forcedY && forcedY != 0;

        for (var pointIndex = 0; pointIndex < sourcePoints.Length; pointIndex++)
        {
            var sourcePoint = sourcePoints[pointIndex];
            int x;
            int y;
            if (hasForcedPosition)
            {
                x = forcedPositionX!.Value + Roll(randomRoll, 5) - 2;
                y = forcedPositionY!.Value + Roll(randomRoll, 5) - 2;
            }
            else if (sourcePoint.Range != 0)
            {
                var offset = GetFormationOffset(followerIndex, pointIndex, sourceDefinition.Formation);
                x = leaderPoints[pointIndex].X + offset;
                y = leaderPoints[pointIndex].Y + offset;
            }
            else
            {
                x = sourcePoint.X;
                y = sourcePoint.Y;
            }

            followerPoints[pointIndex] = new LegacyNpcGenerationRoutePoint(x, y, 0, sourcePoint.Wait);
        }

        return new LegacyNpcGenerationRoute(
            followerPoints[0], followerPoints[1], followerPoints[2], followerPoints[3], followerPoints[4],
            routeDefinition.Route.StartAction,
            routeDefinition.Route.Segment1Action,
            routeDefinition.Route.Segment2Action,
            routeDefinition.Route.Segment3Action);
    }

    private static int GetFormationOffset(int followerIndex, int routePointIndex, int formation)
    {
        // Preserve the source's contiguous pointer-style read for in-bounds
        // cells. Several NPCGener entries use Formation=4 despite the declared
        // two-value tail dimension. Out-of-object C++ reads have no stable
        // contract, so those followers receive a zero offset.
        var flattenedIndex = checked(((followerIndex * 12) + routePointIndex) * 2 + formation);
        return (uint)flattenedIndex < (uint)FormationOffsets.Length ? FormationOffsets[flattenedIndex] : 0;
    }

    private static int Roll(Func<int, int> randomRoll, int maximum)
    {
        var value = randomRoll(maximum);
        if (value < 0 || value >= maximum)
            throw new ArgumentOutOfRangeException(nameof(randomRoll), "The NPC generation roll must be inside its requested range.");
        return value;
    }

    private static LegacyNpcGenerationRoute ResolveGeneratedRoute(
        LegacyNpcGenerationRoute route,
        Func<int, int> randomRoll)
    {
        LegacyNpcGenerationRoutePoint Resolve(LegacyNpcGenerationRoutePoint point)
        {
            if (point.X == 0)
                return new LegacyNpcGenerationRoutePoint(0, 0, 0, 0);

            if (point.Range <= 0)
                return point with { Range = 0 };

            return new LegacyNpcGenerationRoutePoint(
                ResolveStartCoordinate(point.X, point.Range, randomRoll),
                ResolveStartCoordinate(point.Y, point.Range, randomRoll),
                0,
                point.Wait);
        }

        return new LegacyNpcGenerationRoute(
            Resolve(route.Start),
            Resolve(route.Segment1),
            Resolve(route.Segment2),
            Resolve(route.Segment3),
            Resolve(route.Dest),
            route.StartAction,
            route.Segment1Action,
            route.Segment2Action,
            route.Segment3Action);
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
        public int StartWait { get; private set; }
        public string StartAction { get; private set; } = string.Empty;
        public int Segment1X { get; private set; }
        public int Segment1Y { get; private set; }
        public int Segment1Range { get; private set; }
        public int Segment1Wait { get; private set; }
        public string Segment1Action { get; private set; } = string.Empty;
        public int Segment2X { get; private set; }
        public int Segment2Y { get; private set; }
        public int Segment2Range { get; private set; }
        public int Segment2Wait { get; private set; }
        public string Segment2Action { get; private set; } = string.Empty;
        public int Segment3X { get; private set; }
        public int Segment3Y { get; private set; }
        public int Segment3Range { get; private set; }
        public int Segment3Wait { get; private set; }
        public string Segment3Action { get; private set; } = string.Empty;
        public int DestX { get; private set; }
        public int DestY { get; private set; }
        public int DestRange { get; private set; }
        public int DestWait { get; private set; }

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
                case "StartWait": StartWait = ParseInt(value); break;
                case "StartAction": StartAction = ParseAction(value, StartAction); break;
                case "Segment1X": Segment1X = ParseInt(value); break;
                case "Segment1Y": Segment1Y = ParseInt(value); break;
                case "Segment1Range": Segment1Range = ParseInt(value); break;
                case "Segment1Wait": Segment1Wait = ParseInt(value); break;
                case "Segment1Action": Segment1Action = ParseAction(value, Segment1Action); break;
                case "Segment2X": Segment2X = ParseInt(value); break;
                case "Segment2Y": Segment2Y = ParseInt(value); break;
                case "Segment2Range": Segment2Range = ParseInt(value); break;
                case "Segment2Wait": Segment2Wait = ParseInt(value); break;
                case "Segment2Action": Segment2Action = ParseAction(value, Segment2Action); break;
                case "Segment3X": Segment3X = ParseInt(value); break;
                case "Segment3Y": Segment3Y = ParseInt(value); break;
                case "Segment3Range": Segment3Range = ParseInt(value); break;
                case "Segment3Wait": Segment3Wait = ParseInt(value); break;
                case "Segment3Action": Segment3Action = ParseAction(value, Segment3Action); break;
                case "DestX": DestX = ParseInt(value); break;
                case "DestY": DestY = ParseInt(value); break;
                case "DestRange": DestRange = ParseInt(value); break;
                case "DestWait": DestWait = ParseInt(value); break;
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
            StartRange)
        {
            Route = new LegacyNpcGenerationRoute(
                new LegacyNpcGenerationRoutePoint(StartX, StartY, StartRange, StartWait),
                new LegacyNpcGenerationRoutePoint(Segment1X, Segment1Y, Segment1Range, Segment1Wait),
                new LegacyNpcGenerationRoutePoint(Segment2X, Segment2Y, Segment2Range, Segment2Wait),
                new LegacyNpcGenerationRoutePoint(Segment3X, Segment3Y, Segment3Range, Segment3Wait),
                new LegacyNpcGenerationRoutePoint(DestX, DestY, DestRange, DestWait),
                StartAction,
                Segment1Action,
                Segment2Action,
                Segment3Action)
        };

        private static int ParseInt(string value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

        private static string ParseAction(string value, string previous)
        {
            var tokens = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
                return string.Empty;

            // CNPCGene.cpp reads only str2 and SetAct leaves the existing value unchanged
            // when strlen(act) >= 79.
            return tokens[0].Length >= 79 ? previous : tokens[0];
        }
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
    int StartRange)
{
    public LegacyNpcGenerationRoute Route { get; init; } = LegacyNpcGenerationRoute.Empty;
}

public sealed record LegacyNpcGenerationRoutePoint(int X, int Y, int Range, int Wait);

public sealed record LegacyNpcGenerationRoute(
    LegacyNpcGenerationRoutePoint Start,
    LegacyNpcGenerationRoutePoint Segment1,
    LegacyNpcGenerationRoutePoint Segment2,
    LegacyNpcGenerationRoutePoint Segment3,
    LegacyNpcGenerationRoutePoint Dest,
    string StartAction,
    string Segment1Action,
    string Segment2Action,
    string Segment3Action)
{
    public static LegacyNpcGenerationRoute Empty { get; } = new(
        new(0, 0, 0, 0), new(0, 0, 0, 0), new(0, 0, 0, 0),
        new(0, 0, 0, 0), new(0, 0, 0, 0), string.Empty, string.Empty, string.Empty, string.Empty);
}

public sealed record LegacyGeneratedNpc(
    int GenerateIndex,
    short PositionX,
    short PositionY,
    byte[] MobSnapshot,
    byte[] AffectSnapshot,
    string SourceName)
{
    public LegacyNpcGenerationRoute GenerationRoute { get; init; } = LegacyNpcGenerationRoute.Empty;
}

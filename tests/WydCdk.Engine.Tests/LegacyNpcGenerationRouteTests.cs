using WydCdk.World;

internal static class LegacyNpcGenerationRouteTests
{
    public static void Run()
    {
        ParsesFivePointsRangesWaitsAndFourSegmentActions();
        GeneratedMobResolvesAllRouteRangesLikeGenerateMob();
        ForcedSpawnPositionReanchorsEveryRoutePoint();
        DefaultsOmittedRouteFieldsAndIgnoresUnsupportedActions();
        MalformedRouteValuesUseCatalogDefaults();
        ActionValuesUseTheLegacyFirstTokenAndLengthLimit();
    }

    private static void ParsesFivePointsRangesWaitsAndFourSegmentActions()
    {
        var catalog = LegacyNpcGenerationCatalog.Parse("""
            #[42]
            Leader: Wolf
            StartX: 10
            StartY: 11
            StartRange: 12
            StartWait: 13
            StartAction: greet everyone
            Segment1X: 20
            Segment1Y: 21
            Segment1Range: 22
            Segment1Wait: 23
            Segment1Action: walk
            Segment2X: 30
            Segment2Y: 31
            Segment2Range: 32
            Segment2Wait: 33
            Segment2Action: guard
            Segment3X: 40
            Segment3Y: 41
            Segment3Range: 42
            Segment3Wait: 43
            Segment3Action: rest
            DestX: 50
            DestY: 51
            DestRange: 52
            DestWait: 53
            """, new Dictionary<string, byte[]> { ["Wolf"] = new byte[LegacyAccountSnapshot.CharacterStride] });

        Assert(catalog.TryGet(42, out var definition) && definition is not null,
            "The route fixture was not parsed into an NPC generation definition.");
        var route = definition!.Route;
        Assert(route.Start == new LegacyNpcGenerationRoutePoint(10, 11, 12, 13) &&
               route.Segment1 == new LegacyNpcGenerationRoutePoint(20, 21, 22, 23) &&
               route.Segment2 == new LegacyNpcGenerationRoutePoint(30, 31, 32, 33) &&
               route.Segment3 == new LegacyNpcGenerationRoutePoint(40, 41, 42, 43) &&
               route.Dest == new LegacyNpcGenerationRoutePoint(50, 51, 52, 53),
            "The parser did not preserve all five route points, ranges, and waits.");
        Assert(route.StartAction == "greet" && route.Segment1Action == "walk" &&
               route.Segment2Action == "guard" && route.Segment3Action == "rest",
            "The parser did not preserve the source-backed start and segment actions.");
    }

    private static void GeneratedMobResolvesAllRouteRangesLikeGenerateMob()
    {
        var catalog = LegacyNpcGenerationCatalog.Parse("""
            #[42]
            Leader: Wolf
            MaxNumMob: 10
            StartX: 10
            StartY: 20
            StartRange: 2
            StartWait: 4
            Segment1X: 30
            Segment1Y: 40
            Segment1Range: 3
            Segment1Wait: 5
            Segment2X: 50
            Segment2Y: 60
            Segment2Wait: 6
            Segment3X: 0
            Segment3Y: 70
            Segment3Range: 4
            Segment3Wait: 7
            DestX: 80
            DestY: 90
            DestRange: 1
            DestWait: 8
            """, new Dictionary<string, byte[]> { ["Wolf"] = new byte[LegacyAccountSnapshot.CharacterStride] });

        Assert(catalog.TryCreateLeader(42, static maximum => maximum > 1 ? 1 : 0, out var generated) && generated is not null,
            "The NPC generator did not create the range fixture.");
        var route = generated!.GenerationRoute;
        Assert(route.Start == new LegacyNpcGenerationRoutePoint(9, 19, 0, 4) &&
               generated.PositionX == 9 && generated.PositionY == 19,
            "The generated start point and spawn position did not share the GenerateMob range rolls.");
        Assert(route.Segment1 == new LegacyNpcGenerationRoutePoint(28, 38, 0, 5) &&
               route.Segment2 == new LegacyNpcGenerationRoutePoint(50, 60, 0, 6) &&
               route.Segment3 == new LegacyNpcGenerationRoutePoint(0, 0, 0, 0) &&
               route.Dest == new LegacyNpcGenerationRoutePoint(80, 90, 0, 8),
            "GenerateMob did not resolve route ranges, waits, and empty segments into runtime points.");
    }

    private static void ForcedSpawnPositionReanchorsEveryRoutePoint()
    {
        var catalog = LegacyNpcGenerationCatalog.Parse("""
            #[42]
            Leader: Wolf
            MaxNumMob: 10
            StartX: 10
            StartY: 20
            StartRange: 2
            Segment1X: 30
            Segment1Y: 40
            Segment1Range: 3
            DestX: 80
            DestY: 90
            DestRange: 1
            """, new Dictionary<string, byte[]> { ["Wolf"] = new byte[LegacyAccountSnapshot.CharacterStride] });
        var hub = new WorldHub(npcGenerationCatalog: catalog);

        Assert(hub.TrySpawnGeneratedNpc(42, out var generated, static maximum => maximum > 1 ? 1 : 0,
                positionX: 100, positionY: 200, forceGenerationRoutePosition: true) && generated is not null,
            "WorldHub did not create the forced-position NPC.");
        var route = generated!.GenerationRoute;
        Assert(route.Start.X == 100 && route.Start.Y == 200 && route.Segment1.X == 100 && route.Segment1.Y == 200 &&
               route.Segment2.X == 100 && route.Segment2.Y == 200 && route.Segment3.X == 100 && route.Segment3.Y == 200 &&
               route.Dest.X == 100 && route.Dest.Y == 200,
            "A legacy PosX/PosY override did not rewrite every generated route coordinate.");
    }

    private static void DefaultsOmittedRouteFieldsAndIgnoresUnsupportedActions()
    {
        var catalog = LegacyNpcGenerationCatalog.Parse("""
            #[7]
            Leader: Wolf
            FightAction: do-not-load
            FleeAction: do-not-load
            """, new Dictionary<string, byte[]> { ["Wolf"] = new byte[LegacyAccountSnapshot.CharacterStride] });

        Assert(catalog.TryGet(7, out var definition) && definition is not null,
            "The minimal NPC generation definition was not parsed.");
        var route = definition!.Route;
        var emptyPoint = new LegacyNpcGenerationRoutePoint(0, 0, 0, 0);
        Assert(route.Start == emptyPoint && route.Segment1 == emptyPoint && route.Segment2 == emptyPoint &&
               route.Segment3 == emptyPoint && route.Dest == emptyPoint,
            "Omitted optional route fields did not retain their zero defaults.");
        Assert(route.StartAction == string.Empty && route.Segment1Action == string.Empty &&
               route.Segment2Action == string.Empty && route.Segment3Action == string.Empty,
            "Omitted route action fields did not retain empty defaults.");
    }

    private static void MalformedRouteValuesUseCatalogDefaults()
    {
        var catalog = LegacyNpcGenerationCatalog.Parse("""
            #[8]
            Leader: Wolf
            StartX: bad
            StartRange: 2x
            Segment2Wait: nope
            DestY: 3.5
            StartAction:
            """, new Dictionary<string, byte[]> { ["Wolf"] = new byte[LegacyAccountSnapshot.CharacterStride] });

        Assert(catalog.TryGet(8, out var definition) && definition is not null,
            "The malformed-field fixture was not parsed.");
        var route = definition!.Route;
        Assert(route.Start == new LegacyNpcGenerationRoutePoint(0, 0, 0, 0) &&
               route.Segment2 == new LegacyNpcGenerationRoutePoint(0, 0, 0, 0) &&
               route.Dest == new LegacyNpcGenerationRoutePoint(0, 0, 0, 0),
            "Malformed route integers did not follow the catalog's existing zero-default convention.");
        Assert(route.StartAction == string.Empty,
            "An empty action value was not normalized to the existing empty default.");
    }

    private static void ActionValuesUseTheLegacyFirstTokenAndLengthLimit()
    {
        var accepted = new string('a', 78);
        var rejected = new string('b', 79);
        var catalog = LegacyNpcGenerationCatalog.Parse($"""
            #[9]
            Leader: Wolf
            StartAction: {accepted}
            StartAction: {rejected}
            Segment1Action: {rejected}
            Segment2Action: hello world
            """, new Dictionary<string, byte[]> { ["Wolf"] = new byte[LegacyAccountSnapshot.CharacterStride] });

        Assert(catalog.TryGet(9, out var definition) && definition is not null,
            "The action length fixture was not parsed.");
        Assert(definition!.Route.StartAction == accepted && definition.Route.Segment1Action == string.Empty,
            "The legacy 78-character limit did not preserve a prior action or reject an oversized first value.");
        Assert(definition.Route.Segment2Action == "hello",
            "The action parser did not keep only the first whitespace-delimited token.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

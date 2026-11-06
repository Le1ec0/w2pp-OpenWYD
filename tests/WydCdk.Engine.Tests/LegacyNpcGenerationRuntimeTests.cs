using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.Server;
using WydCdk.World;

internal static class LegacyNpcGenerationRuntimeTests
{
    public static void Run()
    {
        MinuteScheduleUsesIndexPhaseAndSkipsDisabledAndReservedGenerators();
        RandomizedIntervalsPreserveLegacyBandsAfterNoSpawn();
        DungeonEventDropsWeightedPrizeAfterHighIntervalReroll();
        DungeonEventCanBeDisabledAndFailsClosedWithoutItemData();
        DungeonEventPlacementSkipsStaticItemsAndCapturesTheItemView();
        DungeonEventConsumesPrizeRollsWhenEveryGridIsBlocked();
        HighIntervalRelocatesRouteButKeepsSourceMobAndCapacityOwner();
        InvalidOrMissingRelocationRouteDoesNotSpawn();
    }

    private static void MinuteScheduleUsesIndexPhaseAndSkipsDisabledAndReservedGenerators()
    {
        var catalog = CreateCatalog((3, 2), (4, 2), (5, 1), (8, 0), (9, -1));
        var world = new WorldHub(npcGenerationCatalog: catalog);
        var relayedGroups = new List<IReadOnlyList<LegacyWorldNpc>>();
        var runtime = new LegacyNpcGenerationRuntime(
            world,
            catalog,
            (group, _) =>
            {
                relayedGroups.Add(group);
                return ValueTask.CompletedTask;
            },
            static _ => 0);

        var firstMinute = runtime.TickMinuteAsync().AsTask().GetAwaiter().GetResult();
        var secondMinute = runtime.TickMinuteAsync().AsTask().GetAwaiter().GetResult();

        Assert(firstMinute.MinuteCounter == 0 && firstMinute.DueGenerateIndices.SequenceEqual([4]),
            "The first NPC generator did not follow MinCounter modulo index phase or skip reserved/disabled entries.");
        Assert(secondMinute.MinuteCounter == 1 && secondMinute.DueGenerateIndices.SequenceEqual([3]),
            "The second minute did not schedule the generator whose index phase matches MinCounter.");
        Assert(firstMinute.SpawnedGroups is [{ GenerateIndex: 4, Members.Count: 1 }] &&
               secondMinute.SpawnedGroups is [{ GenerateIndex: 3, Members.Count: 1 }] &&
               relayedGroups.Count == 2 && world.GetGeneratedNpcSnapshotsForShard(0, 1).Count == 2,
            "Scheduled groups were not generated in the scheduler's order and relayed through the supplied group sink.");
    }

    private static void RandomizedIntervalsPreserveLegacyBandsAfterNoSpawn()
    {
        var intervals = new (int Index, int Initial, int RollMaximum, int Expected)[ ]
        {
            (9, 500, 500, 999),
            (10, 1000, 1000, 1999),
            (11, 2000, 1800, 3799),
            (12, 3800, 1000, 4799),
        };

        foreach (var (index, initial, rollMaximum, expected) in intervals)
        {
            var catalog = CreateCatalog((index, initial));
            var randomMaxima = new List<int>();
            var runtime = new LegacyNpcGenerationRuntime(
                new WorldHub(npcGenerationCatalog: catalog),
                catalog,
                static (_, _) => ValueTask.CompletedTask,
                maximum =>
                {
                    randomMaxima.Add(maximum);
                    return maximum - 1;
                },
                initialMinuteCounter: checked((uint)index));

            var tick = runtime.TickMinuteAsync().AsTask().GetAwaiter().GetResult();

            Assert(tick.DueGenerateIndices.SequenceEqual([index]) && tick.SpawnedGroups.Count == 0 &&
                   tick.RescheduledIntervals.TryGetValue(index, out var nextInterval) && nextInterval == expected,
                $"MinuteGenerate={initial} did not reschedule to {expected} in the legacy interval band.");
            Assert(randomMaxima.Count > 0 && randomMaxima[^1] == rollMaximum,
                "The interval roll did not use the expected range after an NPC generator produced no mob.");
        }
    }

    private static void DungeonEventDropsWeightedPrizeAfterHighIntervalReroll()
    {
        var catalog = CreateCatalog((9, 3800));
        var itemData = CreateNeutralItemData();
        var world = new WorldHub(itemData: itemData, npcGenerationCatalog: catalog);
        Assert(world.Enter(1, "DUNGEON_EVENT_VIEWER", static (_, _) => ValueTask.CompletedTask),
            "The dungeon event pickup viewer could not be registered.");
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(50, 0, 0, 0, 0, 0, 0, 5000, 5000, 5000, 5000, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Assert(world.SetCharacterState(1, 0, 0, 1, 0, 0, 352, 3780, mob),
            "The dungeon event pickup viewer's position was not installed.");

        var randomMaxima = new List<int>();
        var itemRollCount = 0;
        var runtime = new LegacyNpcGenerationRuntime(
            world,
            catalog,
            static (_, _) => ValueTask.CompletedTask,
            maximum =>
            {
                randomMaxima.Add(maximum);
                return maximum switch
                {
                    1000 => 12,
                    30 => 2,
                    5 => 0,
                    10 => itemRollCount++ == 0 ? 3 : 7,
                    256 => randomMaxima.Count(static value => value == 256) * 10,
                    4 => 3,
                    _ => throw new InvalidOperationException($"Unexpected dungeon event random range {maximum}."),
                };
            },
            initialMinuteCounter: 9,
            itemData: itemData);

        var tick = runtime.TickMinuteAsync().AsTask().GetAwaiter().GetResult();
        var pickup = world.TryGetGroundItem(1, 1, 0, LegacyWorldItem.WireIdOffset + 1, 352, 3780, out var outcome);

        Assert(tick.SpawnedGroups.Count == 0 &&
               tick.RescheduledIntervals.TryGetValue(9, out var nextInterval) && nextInterval == 3812,
            "The timed dungeon event did not run after the high-interval reroll even when the NPC group spawned no members.");
        Assert(randomMaxima.SequenceEqual([1000, 30, 5, 10, 256, 256, 256, 4, 10, 4, 10, 4, 10, 4, 10, 4]),
            "The dungeon event did not preserve source RNG order for interval, point, count, weighted prize, and rotation rolls.");
        Assert(pickup == LegacyGetItemResult.Accepted && outcome is
               { Item.Index: 419, Item.Effect1: 59, Item.Value1: 10, Item.Effect2: 59, Item.Value2: 20, Item.Effect3: 59, Item.Value3: 30 },
            "The dungeon event did not apply SetItemBonus's special-item fillers to the weighted item 419 before pickup.");
        Assert(tick.DungeonEventDrops.Count == 5 &&
               tick.DungeonEventDrops.All(static drop => drop.Item.Delay == 90 && drop.Item.Rotate == 3),
            "The dungeon event did not create the source-selected five prizes with dynamic ground-item lifetime and rotation.");
    }

    private static void DungeonEventCanBeDisabledAndFailsClosedWithoutItemData()
    {
        foreach (var (enabled, table, label) in new[]
                 {
                     (false, CreateNeutralItemData(), "disabled"),
                     (true, (LegacyItemDataTable?)null, "missing ItemList"),
                 })
        {
            var catalog = CreateCatalog((9, 3800));
            var randomMaxima = new List<int>();
            var runtime = new LegacyNpcGenerationRuntime(
                new WorldHub(npcGenerationCatalog: catalog),
                catalog,
                static (_, _) => ValueTask.CompletedTask,
                maximum =>
                {
                    randomMaxima.Add(maximum);
                    return maximum - 1;
                },
                initialMinuteCounter: 9,
                itemData: table,
                dungeonEventEnabled: enabled);

            var tick = runtime.TickMinuteAsync().AsTask().GetAwaiter().GetResult();

            Assert(tick.DungeonEventDrops.Count == 0 && randomMaxima.SequenceEqual([1000]),
                $"The dungeon event {label} path generated prizes or consumed prize rolls.");
        }
    }

    private static void DungeonEventPlacementSkipsStaticItemsAndCapturesTheItemView()
    {
        var catalog = CreateCatalog((9, 3800));
        var itemData = CreateNeutralItemData();
        var world = new WorldHub(itemData: itemData, npcGenerationCatalog: catalog);
        world.ConfigureMapItems([new LegacyMapItemState(1, new LegacyItem(500, 0, 0, 0, 0, 0, 0), 352, 3780, 0, 0, 0, 0, 0)]);
        AddDungeonEventViewer(world, 1, "DUNGEON_EVENT_EDGE", 367, 3795);
        AddDungeonEventViewer(world, 2, "DUNGEON_EVENT_OUTSIDE", 368, 3795);
        var runtime = new LegacyNpcGenerationRuntime(
            world,
            catalog,
            static (_, _) => ValueTask.CompletedTask,
            static maximum => maximum switch { 1000 => 0, 30 => 2, 5 => 0, 10 => 7, 4 => 0, _ => 0 },
            initialMinuteCounter: 9,
            itemData: itemData);

        var tick = runtime.TickMinuteAsync().AsTask().GetAwaiter().GetResult();
        var drops = tick.DungeonEventDrops;

        Assert(drops.Count == 5 && drops[0].Item.PositionX == 351 && drops[0].Item.PositionY == 3779,
            "A static map item at DungeonPos[2] was not skipped using the source's y-major radius-one search.");
        Assert(drops.Select(static drop => (drop.Item.PositionX, drop.Item.PositionY)).Distinct().Count() == 5 &&
               drops.All(static drop => Math.Abs(drop.Item.PositionX - 352) <= 1 && Math.Abs(drop.Item.PositionY - 3780) <= 1),
            "Dungeon event prizes did not occupy unique available cells within the source radius-one search.");
        Assert(drops[0].RecipientConnectionIds.SequenceEqual([1]),
            "The dungeon event did not capture inclusive 33x33 item viewers at the placement grid.");
    }

    private static void DungeonEventConsumesPrizeRollsWhenEveryGridIsBlocked()
    {
        var catalog = CreateCatalog((9, 3800));
        var itemData = CreateNeutralItemData();
        var blockedGrid = new LegacyMapGrid(
            Enumerable.Repeat((byte)127, LegacyMapGrid.HeightMapSize).ToArray(),
            new byte[LegacyMapGrid.AttributeMapSize]);
        var world = new WorldHub(blockedGrid, itemData: itemData, npcGenerationCatalog: catalog);
        var randomMaxima = new List<int>();
        var runtime = new LegacyNpcGenerationRuntime(
            world,
            catalog,
            static (_, _) => ValueTask.CompletedTask,
            maximum =>
            {
                randomMaxima.Add(maximum);
                return maximum switch { 1000 => 0, 30 => 2, 5 => 0, 10 => 7, 4 => 0, _ => 0 };
            },
            initialMinuteCounter: 9,
            itemData: itemData);

        var tick = runtime.TickMinuteAsync().AsTask().GetAwaiter().GetResult();

        Assert(tick.DungeonEventDrops.Count == 0 &&
               randomMaxima.SequenceEqual([1000, 30, 5, 10, 4, 10, 4, 10, 4, 10, 4, 10, 4]),
            "A failed CreateItem placement stopped prize RNG consumption or produced a partial drop set.");
    }

    private static void AddDungeonEventViewer(WorldHub world, int connectionId, string name, short x, short y)
    {
        Assert(world.Enter(connectionId, name, static (_, _) => ValueTask.CompletedTask),
            $"Dungeon event viewer {connectionId} could not be registered.");
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(50, 0, 0, 0, 0, 0, 0, 5000, 5000, 5000, 5000, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Assert(world.SetCharacterState(connectionId, 0, 0, 1, 0, 0, x, y, mob),
            $"Dungeon event viewer {connectionId}'s position was not installed.");
    }

    private static LegacyItemDataTable CreateNeutralItemData() =>
        LegacyItemDataTable.Load(Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());

    private static void HighIntervalRelocatesRouteButKeepsSourceMobAndCapacityOwner()
    {
        var catalog = CreateRelocationCatalog("""
            #[9]
            MinuteGenerate: 500
            MaxNumMob: 1
            MinGroup: 1
            MaxGroup: 1
            Leader: Source_Leader
            Follower: Source_Follower@
            RouteType: 3
            Formation: 0
            StartX: 12
            StartY: 10

            #[10]
            MinuteGenerate: 0
            MaxNumMob: 20
            MinGroup: 0
            MaxGroup: 0
            Leader: Route_Leader
            Follower: Route_Follower@
            RouteType: 8
            Formation: 2
            StartX: 100
            StartY: 200
            StartWait: 17
            Segment1X: 110
            Segment1Y: 210
            Segment1Wait: 23
            """);
        var world = new WorldHub(npcGenerationCatalog: catalog);
        var randomMaxima = new List<int>();
        var runtime = new LegacyNpcGenerationRuntime(
            world,
            catalog,
            static (_, _) => ValueTask.CompletedTask,
            maximum =>
            {
                randomMaxima.Add(maximum);
                return maximum == 500 ? 499 : 0;
            },
            initialMinuteCounter: 9);

        var tick = runtime.TickMinuteAsync().AsTask().GetAwaiter().GetResult();
        var group = tick.SpawnedGroups.SingleOrDefault();

        Assert(group is { GenerateIndex: 9, RouteGenerateIndex: 10, Members.Count: 2 },
            "The high-interval schedule did not preserve its source index while relocating to the selected route index.");
        var leader = group!.Members[0];
        var follower = group.Members[1];
        Assert(ReadMobName(leader.MobSnapshot) == "Source Leader" &&
               ReadMobName(follower.MobSnapshot) == "Source Follower" &&
               leader.GenerateIndex == 10 && follower.GenerateIndex == 10 &&
               leader.GenerationOwnerIndex == 9 && follower.GenerationOwnerIndex == 9,
            "The relocation did not keep source templates/capacity ownership separate from the NPC route identity.");
        Assert(leader.PositionX == 100 && leader.PositionY == 200 &&
               leader.GenerationRoute.Start == new LegacyNpcGenerationRoutePoint(100, 200, 0, 17) &&
               follower.GenerationRoute.Segment1 == new LegacyNpcGenerationRoutePoint(110, 210, 0, 23) &&
               leader.RouteType == 3 && follower.RouteType == 3,
            "The relocated group did not use route coordinates while retaining the source route type.");
        Assert(randomMaxima.SequenceEqual([12, 1, 500]) &&
               tick.RescheduledIntervals.TryGetValue(9, out var nextInterval) && nextInterval == 999,
            "The relocation, group-size, and interval random rolls did not follow the legacy consumption order.");
        Assert(!world.TrySpawnGeneratedNpcGroup(9, out _, static _ => 0, routeGenerateIndex: 10),
            "The source generator capacity did not count relocated NPCs by their origin index.");
    }

    private static void InvalidOrMissingRelocationRouteDoesNotSpawn()
    {
        var invalidBoundsCatalog = CreateRelocationCatalog("""
            #[9]
            MinuteGenerate: 500
            MaxNumMob: 1
            MinGroup: 0
            MaxGroup: 0
            Leader: Source_Leader
            Follower: Source_Follower@
            StartX: 8
            StartY: 10
            """);
        var invalidRollMaxima = new List<int>();
        var invalidRuntime = new LegacyNpcGenerationRuntime(
            new WorldHub(npcGenerationCatalog: invalidBoundsCatalog),
            invalidBoundsCatalog,
            static (_, _) => ValueTask.CompletedTask,
            maximum =>
            {
                invalidRollMaxima.Add(maximum);
                return maximum - 1;
            },
            initialMinuteCounter: 9);
        var invalidTick = invalidRuntime.TickMinuteAsync().AsTask().GetAwaiter().GetResult();

        Assert(invalidTick.DueGenerateIndices.SequenceEqual([9]) && invalidTick.SpawnedGroups.Count == 0 &&
               invalidRollMaxima.SequenceEqual([500]) &&
               invalidTick.RescheduledIntervals.TryGetValue(9, out var invalidNextInterval) && invalidNextInterval == 999,
            "A source-invalid relocation range spawned a regular NPC or skipped the caller's interval reroll.");

        var missingRouteCatalog = CreateRelocationCatalog("""
            #[9]
            MinuteGenerate: 500
            MaxNumMob: 1
            MinGroup: 0
            MaxGroup: 0
            Leader: Source_Leader
            Follower: Source_Follower@
            StartX: 12
            StartY: 10
            """);
        var missingRouteRollMaxima = new List<int>();
        var missingRouteRuntime = new LegacyNpcGenerationRuntime(
            new WorldHub(npcGenerationCatalog: missingRouteCatalog),
            missingRouteCatalog,
            static (_, _) => ValueTask.CompletedTask,
            maximum =>
            {
                missingRouteRollMaxima.Add(maximum);
                return maximum == 12 ? 11 : maximum - 1;
            },
            initialMinuteCounter: 9);
        var missingRouteTick = missingRouteRuntime.TickMinuteAsync().AsTask().GetAwaiter().GetResult();

        Assert(missingRouteTick.SpawnedGroups.Count == 0 && missingRouteRollMaxima.SequenceEqual([12, 500]) &&
               missingRouteTick.RescheduledIntervals.TryGetValue(9, out var missingRouteNextInterval) && missingRouteNextInterval == 999,
            "A relocation target absent from the catalog did not fail closed before group rolls while preserving rescheduling.");
    }

    private static LegacyNpcGenerationCatalog CreateCatalog(params (int Index, int MinuteGenerate)[] definitions)
    {
        var text = string.Join("\n\n", definitions.Select(static definition => $"""
            #[{definition.Index}]
            MinuteGenerate: {definition.MinuteGenerate}
            MaxNumMob: 20
            MinGroup: 0
            MaxGroup: 0
            Leader: Schedule_Leader
            Follower: Schedule_Follower@
            """));
        var leader = new byte[LegacyAccountSnapshot.CharacterStride];
        BinaryPrimitives.WriteInt32LittleEndian(
            leader.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 16),
            100);
        var follower = leader.ToArray();
        return LegacyNpcGenerationCatalog.Parse(text, new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Schedule_Leader"] = leader,
            ["Schedule_Follower@"] = follower,
        });
    }

    private static LegacyNpcGenerationCatalog CreateRelocationCatalog(string definitionText)
    {
        var templates = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Source_Leader"] = CreateTemplate("Source_Leader"),
            ["Source_Follower@"] = CreateTemplate("Source_Follower@"),
            ["Route_Leader"] = CreateTemplate("Route_Leader"),
            ["Route_Follower@"] = CreateTemplate("Route_Follower@"),
        };
        return LegacyNpcGenerationCatalog.Parse(definitionText, templates);
    }

    private static byte[] CreateTemplate(string name)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        System.Text.Encoding.ASCII.GetBytes(name).CopyTo(mob, LegacyAccountSnapshot.MobNameOffset);
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 16), 100);
        return mob;
    }

    private static string ReadMobName(byte[] mob) => System.Text.Encoding.ASCII.GetString(
        mob,
        LegacyAccountSnapshot.MobNameOffset,
        16).TrimEnd('\0');

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

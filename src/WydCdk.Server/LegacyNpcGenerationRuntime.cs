using WydCdk.Protocol;
using WydCdk.World;

namespace WydCdk.Server;

public sealed record LegacyNpcGenerationScheduledGroup(
    int GenerateIndex,
    int RouteGenerateIndex,
    IReadOnlyList<LegacyWorldNpc> Members);

public sealed record LegacyNpcGenerationMinuteResult(
    uint MinuteCounter,
    IReadOnlyList<int> DueGenerateIndices,
    IReadOnlyDictionary<int, int> RescheduledIntervals,
    IReadOnlyList<LegacyNpcGenerationScheduledGroup> SpawnedGroups,
    IReadOnlyList<LegacyGroundItemSpawn> DungeonEventDrops);

/// <summary>Runs the NPCGener minute schedule and relays generated groups.</summary>
public sealed class LegacyNpcGenerationRuntime
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);

    private static readonly HashSet<int> ReservedGeneratorIndices = [0, 1, 2, 5, 6, 7];
    private static readonly (ushort X, ushort Y)[] DungeonPositions =
    [
        (244, 3822), (380, 3810), (352, 3780), (349, 3748), (456, 3736),
        (562, 3802), (592, 3770), (612, 3795), (748, 3732), (740, 3776),
        (424, 3892), (478, 3928), (432, 3944), (402, 3876), (492, 4076),
        (448, 4078), (996, 3988), (922, 4062), (802, 4000), (774, 4048),
        (710, 3988), (660, 4076), (788, 3860), (876, 3892), (964, 3821),
        (956, 3732), (1032, 3820), (1104, 3737), (1124, 3804), (1132, 3756),
    ];
    private static readonly short[] DungeonItemIndices = [421, 422, 423, 419, 420, 685, 691, 696, 696, 696];

    private readonly WorldHub world;
    private readonly IReadOnlyList<LegacyNpcGenerationDefinition> definitions;
    private readonly IReadOnlyDictionary<int, LegacyNpcGenerationDefinition> definitionsByIndex;
    private readonly Dictionary<int, int> minuteGenerateByIndex;
    private readonly Func<IReadOnlyList<LegacyWorldNpc>, CancellationToken, ValueTask> relayGroup;
    private readonly Func<LegacyGroundItemSpawn, CancellationToken, ValueTask> relayDungeonItem;
    private readonly Func<int, int> nextRoll;
    private readonly LegacyItemDataTable? itemData;
    private readonly bool dungeonEventEnabled;
    private readonly SemaphoreSlim tickGate = new(1, 1);
    private uint minuteCounter;
    private int runStarted;

    public LegacyNpcGenerationRuntime(
        WorldHub world,
        LegacyNpcGenerationCatalog catalog,
        Func<IReadOnlyList<LegacyWorldNpc>, CancellationToken, ValueTask> relayGroup,
        Func<int, int>? nextRoll = null,
        uint initialMinuteCounter = 0,
        LegacyItemDataTable? itemData = null,
        bool dungeonEventEnabled = true,
        Func<LegacyGroundItemSpawn, CancellationToken, ValueTask>? relayDungeonItem = null)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        ArgumentNullException.ThrowIfNull(catalog);
        this.relayGroup = relayGroup ?? throw new ArgumentNullException(nameof(relayGroup));
        this.nextRoll = nextRoll ?? Random.Shared.Next;
        this.itemData = itemData;
        this.dungeonEventEnabled = dungeonEventEnabled;
        this.relayDungeonItem = relayDungeonItem ?? (static (_, _) => ValueTask.CompletedTask);
        definitions = catalog.Definitions;
        definitionsByIndex = definitions.ToDictionary(static definition => definition.GenerateIndex);
        minuteGenerateByIndex = definitions.ToDictionary(
            static definition => definition.GenerateIndex,
            static definition => definition.MinuteGenerate);
        minuteCounter = initialMinuteCounter;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref runStarted, 1, 0) != 0)
            throw new InvalidOperationException("The NPC generation runtime can only run once.");

        try
        {
            using var timer = new PeriodicTimer(TickInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                await TickMinuteAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref runStarted, 0);
        }
    }

    /// <summary>Processes one NPCGener minute in ascending generator order.</summary>
    public async ValueTask<LegacyNpcGenerationMinuteResult> TickMinuteAsync(CancellationToken cancellationToken = default)
    {
        await tickGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var processedMinute = minuteCounter;
            var dueGenerateIndices = new List<int>();
            var rescheduledIntervals = new SortedDictionary<int, int>();
            var spawnedGroups = new List<LegacyNpcGenerationScheduledGroup>();
            var dungeonEventDrops = new List<LegacyGroundItemSpawn>();

            foreach (var definition in definitions)
            {
                var generateIndex = definition.GenerateIndex;
                if (generateIndex < 0 || ReservedGeneratorIndices.Contains(generateIndex))
                    continue;

                var minuteGenerate = minuteGenerateByIndex[generateIndex];
                if (minuteGenerate <= 0)
                    continue;

                var interval = checked((uint)minuteGenerate);
                if (processedMinute % interval != checked((uint)(generateIndex % minuteGenerate)))
                    continue;

                dueGenerateIndices.Add(generateIndex);
                var routeGenerateIndex = SelectRouteGenerateIndex(definition, minuteGenerate);
                if (routeGenerateIndex is { } selectedRouteGenerateIndex &&
                    world.TrySpawnGeneratedNpcGroup(
                        generateIndex,
                        out var group,
                        nextRoll,
                        routeGenerateIndex: selectedRouteGenerateIndex) &&
                    group is { Count: > 0 })
                {
                    var members = group.ToArray();
                    spawnedGroups.Add(new LegacyNpcGenerationScheduledGroup(
                        generateIndex,
                        selectedRouteGenerateIndex,
                        members));
                    await relayGroup(members, cancellationToken).ConfigureAwait(false);
                }

                var nextInterval = SelectNextMinuteGenerate(minuteGenerate);
                if (nextInterval != minuteGenerate)
                {
                    minuteGenerateByIndex[generateIndex] = nextInterval;
                    rescheduledIntervals.Add(generateIndex, nextInterval);
                }

                if (minuteGenerate >= 3800 && dungeonEventEnabled && itemData is not null)
                    await SpawnDungeonEventItemsAsync(dungeonEventDrops, cancellationToken).ConfigureAwait(false);
            }

            unchecked
            {
                minuteCounter++;
            }

            return new LegacyNpcGenerationMinuteResult(
                processedMinute,
                dueGenerateIndices.AsReadOnly(),
                rescheduledIntervals,
                spawnedGroups.AsReadOnly(),
                dungeonEventDrops.AsReadOnly());
        }
        finally
        {
            tickGate.Release();
        }
    }

    private int? SelectRouteGenerateIndex(LegacyNpcGenerationDefinition definition, int minuteGenerate)
    {
        if (minuteGenerate < 500)
            return definition.GenerateIndex;

        // GenerateMob treats SegmentListX/Y[0] as the relocation range when
        // MinuteGenerate >= 500. In NPCGener those are StartX/StartY.
        var relocationRollMaximum = definition.Route.Start.X;
        var firstRouteIndex = definition.Route.Start.Y;
        if (relocationRollMaximum <= 0 ||
            (long)relocationRollMaximum - firstRouteIndex <= 0)
            return null;

        var roll = nextRoll(relocationRollMaximum);
        if (roll < 0 || roll >= relocationRollMaximum)
            throw new ArgumentOutOfRangeException(nameof(nextRoll), "The NPC route relocation roll must be inside its requested range.");

        // The source uses rand() % segx + segy (not rand() % (segx - segy)).
        // It logs an out-of-table result and continues into invalid memory; the
        // C# port fails closed if the selected generator has no catalog entry.
        var selectedRouteIndex = (long)firstRouteIndex + roll;
        if (selectedRouteIndex < 0 || selectedRouteIndex > int.MaxValue ||
            !definitionsByIndex.ContainsKey((int)selectedRouteIndex))
            return null;

        return (int)selectedRouteIndex;
    }

    private int SelectNextMinuteGenerate(int currentInterval)
    {
        int baseInterval;
        int rollMaximum;
        if (currentInterval < 500)
            return currentInterval;
        if (currentInterval < 1000)
        {
            baseInterval = 500;
            rollMaximum = 500;
        }
        else if (currentInterval < 2000)
        {
            baseInterval = 1000;
            rollMaximum = 1000;
        }
        else if (currentInterval < 3800)
        {
            baseInterval = 2000;
            rollMaximum = 1800;
        }
        else
        {
            baseInterval = 3800;
            rollMaximum = 1000;
        }

        var roll = nextRoll(rollMaximum);
        if (roll < 0 || roll >= rollMaximum)
            throw new ArgumentOutOfRangeException(nameof(nextRoll), "The NPC generation interval roll must be inside its requested range.");

        return baseInterval + roll;
    }

    private async ValueTask SpawnDungeonEventItemsAsync(
        List<LegacyGroundItemSpawn> spawnedItems,
        CancellationToken cancellationToken)
    {
        var positionRoll = NextRoll(DungeonPositions.Length);
        var (positionX, positionY) = DungeonPositions[positionRoll];
        var itemCount = NextRoll(5) + 5;

        for (var index = 0; index < itemCount; index++)
        {
            var itemIndex = DungeonItemIndices[NextRoll(DungeonItemIndices.Length)];
            var definition = itemData![itemIndex];
            if (definition is null)
                continue;

            var item = LegacyItemBonusMath.Apply(
                new LegacyItem(itemIndex, 0, 0, 0, 0, 0, 0),
                definition,
                level: 0,
                dropBonus: 0,
                roll: NextRoll);
            var rotate = NextRoll(4);
            var spawn = world.TryEnterDungeonEventGroundItemAndCaptureView(item, positionX, positionY, rotate);
            if (spawn is null)
                continue;

            spawnedItems.Add(spawn);
            await relayDungeonItem(spawn, cancellationToken).ConfigureAwait(false);
        }
    }

    private int NextRoll(int exclusiveMaximum)
    {
        var roll = nextRoll(exclusiveMaximum);
        if (roll < 0 || roll >= exclusiveMaximum)
            throw new ArgumentOutOfRangeException(nameof(nextRoll), $"The NPC generation roll must be inside [0, {exclusiveMaximum}).");
        return roll;
    }
}

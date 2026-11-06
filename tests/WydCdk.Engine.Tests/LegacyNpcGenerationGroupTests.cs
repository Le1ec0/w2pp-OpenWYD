using System.Buffers.Binary;
using System.Text;
using WydCdk.World;

internal static class LegacyNpcGenerationGroupTests
{
    public static void Run()
    {
        GeneratedGroupUsesMinMaxAndFollowerFormation();
        GeneratedGroupAppliesNewbieEventHpToLowLevelMembers();
        GeneratedGroupKeepsFullHpWhenNewbieEventIsDisabledOrLevelIsAtLeast120();
        GeneratedClanVariationUsesOneTenRollPerClanOneMember();
        GeneratedClanVariationSkipsRollsForOtherClans();
        GeneratedGroupClipsFollowersAgainstMaxMobLikeSource();
        GeneratedGroupCapsFollowerSlotsAtLegacyPartySize();
        ForcedGeneratedGroupUsesJitteredFollowerRoute();
        FormationFourUsesBoundedFlattenedOffsets();
        GeneratedGroupMovesLeaderAndFollowersToEmptyTiles();
        GeneratedGroupSearchesThroughRadiusNine();
        GeneratedGroupSkipsBlockedTerrainCells();
    }

    private static void GeneratedGroupUsesMinMaxAndFollowerFormation()
    {
        var catalog = CreateCatalog(
            """
            #[42]
            MaxNumMob: 20
            MinGroup: 2
            MaxGroup: 4
            Leader: Wolf_Leader
            Follower: Wolf_Follower@
            Formation: 0
            StartX: 10
            StartY: 20
            StartRange: 1
            StartWait: 5
            Segment1X: 30
            Segment1Y: 40
            Segment1Range: 3
            Segment1Wait: 7
            DestX: 50
            DestY: 60
            DestRange: 2
            DestWait: 8
            """);
        var hub = new WorldHub(npcGenerationCatalog: catalog);
        var randomMaxima = new List<int>();

        Assert(hub.TrySpawnGeneratedNpcGroup(42, out var spawnedGroup, maximum =>
        {
            randomMaxima.Add(maximum);
            return 0;
        }) && spawnedGroup is not null, "GenerateMob did not create the group.");

        var group = spawnedGroup!;
        var leader = group[0];
        var followers = group.Where(npc => npc.LeaderId == leader!.ConnectionId).ToArray();
        Assert(group.Count == 3 && followers.Length == 2,
            "MinGroup/MaxGroup did not create the selected number of follower mobs.");
        Assert(randomMaxima.SequenceEqual([3, 2, 2, 4, 4, 3, 3]),
            "The group size roll did not precede leader route range rolls.");

        var first = followers[0];
        Assert(ReadMobName(first.MobSnapshot) == "Wolf Follower" &&
               first.GenerationRoute.Start == new LegacyNpcGenerationRoutePoint(10, 20, 0, 5) &&
               first.PositionX == 10 && first.PositionY == 20 &&
               first.GenerationRoute.Segment1 == new LegacyNpcGenerationRoutePoint(26, 36, 0, 7) &&
               first.GenerationRoute.Dest == new LegacyNpcGenerationRoutePoint(49, 59, 0, 8) &&
               first.WaitSec == 5,
            "The follower template, formation offsets, route waits, or spawn position differ from GenerateMob.");
    }

    private static void GeneratedGroupClipsFollowersAgainstMaxMobLikeSource()
    {
        var catalog = CreateCatalog(
            """
            #[43]
            MaxNumMob: 4
            MinGroup: 4
            MaxGroup: 4
            Leader: Wolf_Leader
            Follower: Wolf_Follower@
            """);
        var hub = new WorldHub(npcGenerationCatalog: catalog);

        Assert(hub.TrySpawnGeneratedNpc(43, out _, static _ => 0),
            "GenerateMob rejected a group below MaxNumMob.");
        Assert(hub.GetGeneratedNpcSnapshotsForShard(0, 1).Count == 5,
            "The source's follower clipping rule or leader-plus-follower count changed.");
        Assert(!hub.TrySpawnGeneratedNpc(43, out _),
            "GenerateMob spawned another group after CurrentNumMob reached MaxNumMob.");
    }

    private static void GeneratedGroupAppliesNewbieEventHpToLowLevelMembers()
    {
        var catalog = CreateCatalog(
            """
            #[50]
            MaxNumMob: 10
            MinGroup: 1
            MaxGroup: 1
            Leader: Wolf_Leader
            Follower: Wolf_Follower@
            StartX: 500
            StartY: 500
            """,
            leaderMaxHp: 101,
            leaderLevel: 119,
            followerMaxHp: 55,
            followerLevel: 119);
        var hub = new WorldHub(npcGenerationCatalog: catalog);
        hub.SetCombatWorldState(new LegacyCombatWorldState(NewbieEventServer: true));

        Assert(hub.TrySpawnGeneratedNpcGroup(50, out var group, static _ => 0) && group is { Count: 2 },
            "GenerateMob did not create the low-level NewbieEventServer group.");

        Assert(ReadCurrentHp(group![0].MobSnapshot) == 75 && ReadCurrentHp(group[1].MobSnapshot) == 41,
            "NewbieEventServer did not initialize both generated members at three quarters of MaxHp with integer truncation.");
    }

    private static void GeneratedGroupKeepsFullHpWhenNewbieEventIsDisabledOrLevelIsAtLeast120()
    {
        foreach (var (generateIndex, level, newbieEventEnabled) in new[]
                 {
                     (51, 120, true),
                     (52, 121, true),
                     (53, 119, false),
                 })
        {
            var catalog = CreateCatalog(
                $"""
                #[{generateIndex}]
                MaxNumMob: 10
                MinGroup: 1
                MaxGroup: 1
                Leader: Wolf_Leader
                Follower: Wolf_Follower@
                StartX: {500 + generateIndex}
                StartY: 500
                """,
                leaderMaxHp: 101,
                leaderLevel: level,
                followerMaxHp: 55,
                followerLevel: level);
            var hub = new WorldHub(npcGenerationCatalog: catalog);
            hub.SetCombatWorldState(new LegacyCombatWorldState(NewbieEventServer: newbieEventEnabled));

            Assert(hub.TrySpawnGeneratedNpcGroup(generateIndex, out var group, static _ => 0) && group is { Count: 2 },
                $"GenerateMob did not create scenario {generateIndex}.");
            Assert(ReadCurrentHp(group![0].MobSnapshot) == 101 && ReadCurrentHp(group[1].MobSnapshot) == 55,
                $"NewbieEventServer changed generated HP when disabled or when the member level was at least 120 (index {generateIndex}).");
        }
    }

    private static void GeneratedClanVariationUsesOneTenRollPerClanOneMember()
    {
        var catalog = CreateCatalog(
            """
            #[54]
            MaxNumMob: 10
            MinGroup: 1
            MaxGroup: 1
            Leader: Wolf_Leader
            Follower: Wolf_Follower@
            """,
            leaderClan: 1,
            followerClan: 1);
        var hub = new WorldHub(npcGenerationCatalog: catalog);
        var maxima = new List<int>();
        var rolls = new Queue<int>([0, 1, 9]);

        Assert(hub.TrySpawnGeneratedNpcGroup(54, out var group, maximum =>
        {
            maxima.Add(maximum);
            return rolls.Dequeue();
        }) && group is { Count: 2 }, "GenerateMob did not create the Clan variation group.");

        Assert(maxima.SequenceEqual([1, 10, 10]) && rolls.Count == 0,
            "GenerateMob did not draw one independent 0..9 Clan roll for each Clan 1 member after group selection.");
        Assert(ReadClan(group![0].MobSnapshot) == 2 && ReadClan(group[1].MobSnapshot) == 1,
            "GenerateMob did not convert only the leader whose Clan roll was exactly 1.");
    }

    private static void GeneratedClanVariationSkipsRollsForOtherClans()
    {
        var catalog = CreateCatalog(
            """
            #[55]
            MaxNumMob: 10
            MinGroup: 1
            MaxGroup: 1
            Leader: Wolf_Leader
            Follower: Wolf_Follower@
            """,
            leaderClan: 2,
            followerClan: 1);
        var hub = new WorldHub(npcGenerationCatalog: catalog);
        var maxima = new List<int>();

        Assert(hub.TrySpawnGeneratedNpcGroup(55, out var group, maximum =>
        {
            maxima.Add(maximum);
            return maximum == 10 ? 1 : 0;
        }) && group is { Count: 2 }, "GenerateMob did not create the mixed-Clan variation group.");

        Assert(maxima.SequenceEqual([1, 10]) && ReadClan(group![0].MobSnapshot) == 2 &&
               ReadClan(group[1].MobSnapshot) == 2,
            "GenerateMob consumed a Clan roll for a non-Clan-1 leader or skipped the eligible follower roll.");
    }

    private static void GeneratedGroupCapsFollowerSlotsAtLegacyPartySize()
    {
        var catalog = CreateCatalog(
            """
            #[44]
            MaxNumMob: 100
            MinGroup: 19
            MaxGroup: 19
            Leader: Wolf_Leader
            Follower: Wolf_Follower@
            """);
        var hub = new WorldHub(npcGenerationCatalog: catalog);

        Assert(hub.TrySpawnGeneratedNpc(44, out var leader, static _ => 0) && leader is not null,
            "GenerateMob did not create the capped group.");
        var group = hub.GetGeneratedNpcSnapshotsForShard(0, 1);
        Assert(group.Count == 13 && group.Count(npc => npc.LeaderId == leader!.ConnectionId) == 12,
            "GenerateMob did not cap follower creation at MAX_PARTY=12.");
    }

    private static void ForcedGeneratedGroupUsesJitteredFollowerRoute()
    {
        var catalog = CreateCatalog(
            """
            #[45]
            MaxNumMob: 10
            MinGroup: 1
            MaxGroup: 1
            Leader: Wolf_Leader
            Follower: Wolf_Follower@
            StartX: 10
            StartY: 20
            """);
        var hub = new WorldHub(npcGenerationCatalog: catalog);

        Assert(hub.TrySpawnGeneratedNpc(45, out var leader, static maximum => maximum == 1 ? 0 : maximum - 1,
                positionX: 100, positionY: 200, forceGenerationRoutePosition: true) && leader is not null,
            "GenerateMob did not create a forced-position group.");
        var follower = hub.GetGeneratedNpcSnapshotsForShard(0, 1).Single(npc => npc.LeaderId == leader!.ConnectionId);
        Assert(follower.PositionX == 102 && follower.PositionY == 202 &&
               follower.GenerationRoute.Start == new LegacyNpcGenerationRoutePoint(102, 202, 0, 0) &&
               follower.GenerationRoute.Dest == new LegacyNpcGenerationRoutePoint(102, 202, 0, 0),
            "The PosX/PosY group override did not apply independent legacy jitter to follower route points.");
    }

    private static void FormationFourUsesBoundedFlattenedOffsets()
    {
        var leaderTemplate = CreateTemplate("Wolf_Leader", 1000);
        var followerTemplate = CreateTemplate("Wolf_Follower@", 500);
        var catalog = LegacyNpcGenerationCatalog.Parse(
            """
            #[46]
            MaxNumMob: 20
            MinGroup: 6
            MaxGroup: 6
            Leader: Wolf_Leader
            Follower: Wolf_Follower@
            Formation: 4
            StartX: 10
            StartY: 20
            StartRange: 1
            Segment1X: 20
            Segment1Y: 30
            Segment1Range: 1
            Segment2X: 30
            Segment2Y: 40
            Segment2Range: 1
            Segment3X: 40
            Segment3Y: 50
            Segment3Range: 1
            DestX: 50
            DestY: 60
            DestRange: 1
            """,
            new Dictionary<string, byte[]>
            {
                ["Wolf_Leader"] = leaderTemplate,
                ["Wolf_Follower@"] = followerTemplate,
            });
        Assert(catalog.TryCreateLeader(46, static _ => 0, out var leader) && leader is not null,
            "GenerateMob did not create the Formation=4 characterization leader.");
        Assert(catalog.TryCreateFollower(46, 4, leader!.GenerationRoute, static _ => 0, null, null,
                   out var inRangeFollower) && inRangeFollower is not null,
            "GenerateMob did not create the in-range Formation=4 follower.");
        Assert(catalog.TryCreateFollower(46, 5, leader.GenerationRoute, static _ => 0, null, null,
                   out var outOfRangeFollower) && outOfRangeFollower is not null,
            "GenerateMob did not create the out-of-range Formation=4 follower.");

        Assert(inRangeFollower!.GenerationRoute.Start == new LegacyNpcGenerationRoutePoint(10, 20, 0, 0) &&
               inRangeFollower.GenerationRoute.Dest == new LegacyNpcGenerationRoutePoint(52, 62, 0, 0),
            "Formation=4 no longer reads the bounded flattened table offsets for follower index 4.");
        Assert(outOfRangeFollower!.GenerationRoute.Start == new LegacyNpcGenerationRoutePoint(9, 19, 0, 0) &&
               outOfRangeFollower.GenerationRoute.Dest == new LegacyNpcGenerationRoutePoint(49, 59, 0, 0),
            "Formation=4 must use zero offsets after the flattened 120-value table ends.");
    }

    private static void GeneratedGroupMovesLeaderAndFollowersToEmptyTiles()
    {
        var catalog = CreateCatalog(
            """
            #[47]
            MaxNumMob: 10
            MinGroup: 1
            MaxGroup: 1
            Leader: Wolf_Leader
            Follower: Wolf_Follower@
            Formation: 0
            StartX: 100
            StartY: 100
            StartRange: 1
            """);
        var hub = new WorldHub(npcGenerationCatalog: catalog);
        Assert(hub.EnterNpc(CreateTemplate("Blocker_One", 100), 99, 99, requestedConnectionId: 301) != 0 &&
               hub.EnterNpc(CreateTemplate("Blocker_Two", 100), 100, 100, requestedConnectionId: 302) != 0,
            "Could not occupy the generated group's requested spawn cells.");

        Assert(hub.TrySpawnGeneratedNpcGroup(47, out var group, static _ => 0) && group is { Count: 2 },
            "GenerateMob did not create a leader and follower after finding free cells.");

        var leader = group![0];
        var follower = group[1];
        Assert(leader.PositionX == 98 && leader.PositionY == 98 &&
               leader.GenerationRoute.Start.X == 98 && leader.GenerationRoute.Start.Y == 98 &&
               leader.SegmentX == 98 && leader.SegmentY == 98,
            "The leader did not use the first empty radius-one grid cell for its start position.");
        Assert(follower.PositionX == 99 && follower.PositionY == 98 &&
               follower.GenerationRoute.Start.X == 99 && follower.GenerationRoute.Start.Y == 98 &&
               follower.SegmentX == 99 && follower.SegmentY == 98,
            "The follower did not apply GetEmptyMobGrid to its formation-adjusted start cell.");
    }

    private static void GeneratedGroupSearchesThroughRadiusNine()
    {
        var catalog = CreateCatalog(
            """
            #[48]
            MaxNumMob: 10
            MinGroup: 0
            MaxGroup: 0
            Leader: Wolf_Leader
            StartX: 100
            StartY: 100
            """);
        var hub = new WorldHub(npcGenerationCatalog: catalog);
        var blockerId = 400;
        for (var y = 92; y <= 108; y++)
        for (var x = 92; x <= 108; x++)
            Assert(hub.EnterNpc(CreateTemplate("GridBlocker", 100), checked((short)x), checked((short)y),
                       requestedConnectionId: blockerId++) != 0,
                "Could not fill all generated-NPC cells through radius eight.");

        Assert(hub.TrySpawnGeneratedNpcGroup(48, out var group, static _ => 0) && group is { Count: 1 },
            "GenerateMob did not continue its free-cell search through radius nine.");
        Assert(group![0].PositionX == 91 && group[0].PositionY == 91 &&
               group[0].GenerationRoute.Start.X == 91 && group[0].GenerationRoute.Start.Y == 91,
            "The radius-nine fallback did not choose the first free cell in legacy scan order.");
    }

    private static void GeneratedGroupSkipsBlockedTerrainCells()
    {
        var catalog = CreateCatalog(
            """
            #[49]
            MaxNumMob: 10
            MinGroup: 0
            MaxGroup: 0
            Leader: Wolf_Leader
            StartX: 100
            StartY: 100
            """);
        var height = new byte[LegacyMapGrid.HeightMapSize];
        var attributes = new byte[LegacyMapGrid.AttributeMapSize];
        height[(100 * LegacyMapGrid.HeightWidth) + 100] = 127;
        height[(99 * LegacyMapGrid.HeightWidth) + 99] = 127;
        var mapGrid = new LegacyMapGrid(height, attributes);
        var hub = new WorldHub(mapGrid: mapGrid, npcGenerationCatalog: catalog);

        Assert(hub.TrySpawnGeneratedNpcGroup(49, out var group, static _ => 0) && group is { Count: 1 },
            "GenerateMob did not search around a blocked height-grid origin.");
        Assert(group![0].PositionX == 100 && group[0].PositionY == 99,
            "GetEmptyMobGrid did not reject blocked candidate terrain while scanning the fallback ring.");
    }

    private static LegacyNpcGenerationCatalog CreateCatalog(
        string definition,
        int leaderMaxHp = 1000,
        int leaderLevel = 0,
        int followerMaxHp = 500,
        int followerLevel = 0,
        byte leaderClan = 0,
        byte followerClan = 0)
    {
        var leader = CreateTemplate("Wolf_Leader", leaderMaxHp, leaderLevel, leaderClan);
        var follower = CreateTemplate("Wolf_Follower@", followerMaxHp, followerLevel, followerClan);
        return LegacyNpcGenerationCatalog.Parse(definition, new Dictionary<string, byte[]>
        {
            ["Wolf_Leader"] = leader,
            ["Wolf_Follower@"] = follower,
        });
    }

    private static byte[] CreateTemplate(string name, int maxHp, int level = 0, byte clan = 0)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).CopyTo(mob, LegacyAccountSnapshot.MobNameOffset);
        mob[LegacyAccountSnapshot.MobClanOffset] = clan;
        BinaryPrimitives.WriteInt32LittleEndian(
            mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset, sizeof(int)), level);
        BinaryPrimitives.WriteInt32LittleEndian(
            mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 16, sizeof(int)), maxHp);
        return mob;
    }

    private static int ReadCurrentHp(byte[] mob) => BinaryPrimitives.ReadInt32LittleEndian(
        mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24, sizeof(int)));

    private static byte ReadClan(byte[] mob) => mob[LegacyAccountSnapshot.MobClanOffset];

    private static string ReadMobName(byte[] mob) => Encoding.ASCII.GetString(
        mob,
        LegacyAccountSnapshot.MobNameOffset,
        16).TrimEnd('\0');

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

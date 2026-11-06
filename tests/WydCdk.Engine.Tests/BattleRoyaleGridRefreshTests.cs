using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class BattleRoyaleGridRefreshTests
{
    public static void GridRefreshPrecedesArenaPartyMemberRemoval()
    {
        var world = new WorldHub(summonCatalog: LegacySummonCatalog.Load(Path.Combine(FindReference769TmsrvRun(), "BaseSummon")));
        var outbound = new List<(int RecipientId, DecodedFrame Frame)>();
        var codec = LegacyFrameCodec.CreateDefault();
        foreach (var player in new[]
        {
            (Id: 1, Name: "BR_GRID_LEADER", Level: 50, X: (short)2_638, Y: (short)1_726),
            (Id: 2, Name: "BR_GRID_MEMBER", Level: 50, X: (short)2_639, Y: (short)1_726),
            (Id: 3, Name: "BR_GRID_OUTSIDE", Level: 50, X: (short)2_656, Y: (short)1_726),
            (Id: 4, Name: "BR_GRID_VIEWER", Level: 50, X: (short)2_640, Y: (short)1_726),
            (Id: 5, Name: "BR_GRID_HIDDEN", Level: 50, X: (short)2_800, Y: (short)1_900),
        })
        {
            var mob = CreateMob(player.Name, player.Level);
            var guildId = 0;
            if (player.Id == 1)
            {
                guildId = 77;
                BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset), 77);
                new LegacyItem(1_234, 0, 0, 0, 0, 0, 0).Write(
                    mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (15 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
            }
            else if (player.Id == 3)
            {
                guildId = 88;
                BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset), 88);
                new LegacyItem(2_345, 0, 0, 0, 0, 0, 0).Write(
                    mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (15 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
            }
            AddPlayer(world, outbound, codec, player.Id, player.Name, player.Level, player.X, player.Y, guildId, mob);
        }

        Check(world.TrySetParty(1, [1, 2, 3]), "Battle Royale party fixture could not be formed.");
        var memberSummon = CreateSummon(world, 2);
        Check(world.SetCombatEligibilityState(2, pkMode: true, guilty: false, mapAttribute: 0),
            "Battle Royale PK-info fixture could not be prepared.");
        var npcMob = CreateMob("BR_GRID_NPC", level: 20);
        var npcId = world.EnterNpc(npcMob, 2_641, 1_727, new byte[LegacyAccountSnapshot.AffectStride], requestedConnectionId: 20_000);
        Check(npcId == 20_000, "Battle Royale grid NPC fixture could not be registered.");
        world.ConfigureMapItems([
            new LegacyMapItemState(1, new LegacyItem(501, 0, 0, 0, 0, 0, 0), 2_642, 1_727, 0, 0, LegacyMapItemStateCodes.Open, 0, 90),
        ]);
        Check(world.EnterGroundItem(new LegacyItem(502, 0, 0, 0, 0, 0, 0), 2_643, 1_727) == 10_002,
            "Battle Royale grid ground-item fixture could not be registered.");

        var transition = CreateBucketTwoReadyTransition();
        world.SetBattleRoyaleState(transition.State, transition.BRItem);
        Check(transition.AreaClearLevels is null && transition.RefreshGridAndCleanupParties,
            "The third Battle Royale bucket should refresh and clean the arena even without ClearAreaLevel.");
        var plan = LegacyBattleRoyaleGridRefreshRelay.TryCreate(world, transition, codec, clientTick: 33, keywordIndex: 16);
        Check(plan is not null && plan.Actors.Select(static actor => actor.ConnectionId).SequenceEqual([1, 2, 4]),
            "Grid refresh recipients were not limited to players still inside the inclusive arena rectangle.");

        var viewerFrames = plan!.Actors.Single(static actor => actor.ConnectionId == 4).Frames
            .Select(frame => codec.Decode(frame)).ToArray();
        var createdMobIds = viewerFrames
            .Where(static frame => frame.Header.Type == CreateMobConfirmationV769.MessageType)
            .Select(static frame => (int)BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[CreateMobConfirmationV769.MobIdOffset..]))
            .ToHashSet();
        Check(createdMobIds.SetEquals([1, 2, 3, 20_000, memberSummon.ConnectionId]) && !createdMobIds.Contains(4) && !createdMobIds.Contains(5),
            $"Grid refresh did not recreate every nearby live player/NPC while omitting the recipient and distant player (ids={string.Join(',', createdMobIds.Order())}).");
        var concealedPlayer = viewerFrames.Single(frame => frame.Header.Type == CreateMobConfirmationV769.MessageType &&
            BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[CreateMobConfirmationV769.MobIdOffset..]) == 1);
        Check(concealedPlayer.Payload.Span.Slice(CreateMobConfirmationV769.MobNameOffset, 16)
                .SequenceEqual(new byte[] { 63, 63, 63, 63, 63, 63, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }) &&
            BinaryPrimitives.ReadUInt16LittleEndian(concealedPlayer.Payload.Span[(CreateMobConfirmationV769.EquipmentOffset + (15 * sizeof(ushort)))..]) == 0 &&
            BinaryPrimitives.ReadUInt16LittleEndian(concealedPlayer.Payload.Span[CreateMobConfirmationV769.GuildOffset..]) == 0,
            "Battle Royale grid refresh exposed an active-arena player's name, slot-15 visual, or Guild.");
        var visibleOutsider = viewerFrames.Single(frame => frame.Header.Type == CreateMobConfirmationV769.MessageType &&
            BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[CreateMobConfirmationV769.MobIdOffset..]) == 3);
        Check(visibleOutsider.Payload.Span.Slice(CreateMobConfirmationV769.MobNameOffset, 8)
                .SequenceEqual(System.Text.Encoding.ASCII.GetBytes("BR_GRID_")) &&
            BinaryPrimitives.ReadUInt16LittleEndian(visibleOutsider.Payload.Span[(CreateMobConfirmationV769.EquipmentOffset + (15 * sizeof(ushort)))..]) == 2_345 &&
            BinaryPrimitives.ReadUInt16LittleEndian(visibleOutsider.Payload.Span[CreateMobConfirmationV769.GuildOffset..]) == 88,
            "Battle Royale grid refresh concealed a nearby player outside the inclusive arena rectangle.");
        var concealedNpc = viewerFrames.Single(frame => frame.Header.Type == CreateMobConfirmationV769.MessageType &&
            BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[CreateMobConfirmationV769.MobIdOffset..]) == npcId);
        Check(concealedNpc.Payload.Span.Slice(CreateMobConfirmationV769.MobNameOffset, 16)
                .SequenceEqual(new byte[] { 63, 63, 63, 63, 63, 63, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
            "Battle Royale grid refresh did not apply the same name mask to a visible NPC.");
        var pkInfo = viewerFrames.Single(static frame => frame.Header.Type == PkInfoConfirmation.MessageType && frame.Header.Id == 2);
        Check(BinaryPrimitives.ReadInt32LittleEndian(pkInfo.Payload.Span) == 1,
            "Grid refresh did not follow each visible mob with its current PK state.");
        Check(!viewerFrames.Any(static frame => frame.Header.Type == PkInfoConfirmation.MessageType && frame.Header.Id == 20_000),
            "Grid refresh sent PKInfo for an NPC ID at or above MAX_USER.");
        var normalPlayerPkInfo = viewerFrames.Single(static frame => frame.Header.Type == PkInfoConfirmation.MessageType && frame.Header.Id == 3);
        Check(BinaryPrimitives.ReadInt32LittleEndian(normalPlayerPkInfo.Payload.Span) == 0,
            "Grid refresh did not preserve the normal PK state for a non-PK player.");
        Check(viewerFrames.Count(static frame => frame.Header.Type == CreateItemConfirmation.MessageType) == 2,
            "Grid refresh did not recreate both visible static and ground items.");

        LegacyBattleRoyaleGridRefreshRelay.SendAsync(world, plan, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        var partyRemovals = outbound
            .Where(static message => message.Frame.Header.Type == PartyRemoveConfirmation.MessageType)
            .ToArray();
        var memberGridLastIndex = outbound.FindLastIndex(static message => message.RecipientId == 2 &&
            message.Frame.Header.Type is CreateMobConfirmationV769.MessageType or PkInfoConfirmation.MessageType or CreateItemConfirmation.MessageType);
        var memberPartyRemovalIndex = outbound.FindIndex(static message => message.RecipientId == 2 &&
            message.Frame.Header.Type == PartyRemoveConfirmation.MessageType);
        Check(partyRemovals.Select(static message => message.RecipientId).SequenceEqual([1, 2, 3]) &&
            memberGridLastIndex >= 0 && memberPartyRemovalIndex > memberGridLastIndex &&
            BinaryPrimitives.ReadInt16LittleEndian(partyRemovals.Single(static message => message.RecipientId == 2).Frame.Payload.Span) == 0 &&
            partyRemovals.Where(static message => message.RecipientId != 2).All(static message =>
                BinaryPrimitives.ReadInt16LittleEndian(message.Frame.Payload.Span) == 2),
            "Battle Royale party cleanup did not run after the member's grid refresh or notify the original roster with legacy self/peer values.");

        var summonRemovals = outbound
            .Where(message => message.Frame.Header.Type == RemoveMobConfirmation.MessageType && message.Frame.Header.Id == memberSummon.ConnectionId)
            .Select(static message => message.RecipientId)
            .Order()
            .ToArray();
        Check(summonRemovals.SequenceEqual([1, 2, 4]) &&
            world.GetBattleRoyaleGridRefreshSnapshots(2_604, 1_708, 2_648, 1_744)
                .SelectMany(static recipient => recipient.Mobs)
                .All(mob => mob.ConnectionId != memberSummon.ConnectionId),
            "Removing a Battle Royale party member did not despawn that member's summon and relay RemoveMob to every current viewer.");
        var lastSummonFrameForViewer = outbound.Last(message => message.RecipientId == 4 &&
            (message.Frame.Header.Type == RemoveMobConfirmation.MessageType && message.Frame.Header.Id == memberSummon.ConnectionId ||
             message.Frame.Header.Type == CreateMobConfirmationV769.MessageType &&
             BinaryPrimitives.ReadUInt16LittleEndian(message.Frame.Payload.Span[CreateMobConfirmationV769.MobIdOffset..]) == memberSummon.ConnectionId));
        Check(lastSummonFrameForViewer.Frame.Header.Type == RemoveMobConfirmation.MessageType,
            "A later recipient grid replayed a stale CreateMob after the removed summon had already been despawned.");

        Check(!world.TryRemoveBattleRoyalePartyMember(1, 2_604, 1_708, 2_648, 1_744, out _),
            "Battle Royale cleanup removed the party leader or a member a second time.");
        Check(world.TryRemovePartyMember(3, 0, out var outsideMember) && outsideMember is { RemovedConnectionId: 3, Disbanded: false },
            "Battle Royale cleanup removed a party member who remained outside the arena.");

        world.SetCombatWorldState(new LegacyCombatWorldState(RvrWarActive: true));
        var warPlan = LegacyBattleRoyaleGridRefreshRelay.TryCreate(world, transition, codec, clientTick: 35, keywordIndex: 18);
        var playerWarPkInfo = codec.Decode(warPlan!.Actors.Single(static actor => actor.ConnectionId == 4).Frames.Single(frame =>
            codec.Decode(frame).Header.Type == PkInfoConfirmation.MessageType && codec.Decode(frame).Header.Id == 3));
        Check(BinaryPrimitives.ReadInt32LittleEndian(playerWarPkInfo.Payload.Span) == 1,
            "Grid refresh did not apply the global legacy RvR PK-info state to a visible player.");
    }

    public static void SendFailureDoesNotInterruptLaterPartyCleanup()
    {
        var codec = LegacyFrameCodec.CreateDefault();
        var outbound = new List<(int RecipientId, DecodedFrame Frame)>();
        var failedRecipientCalls = 0;
        var world = new WorldHub();
        foreach (var player in new[]
        {
            (Id: 1, Name: "BR_FAILURE_LEADER1", X: (short)2_638, Y: (short)1_726),
            (Id: 2, Name: "BR_FAILURE_MEMBER1", X: (short)2_639, Y: (short)1_726),
            (Id: 3, Name: "BR_FAILURE_LEADER2", X: (short)2_640, Y: (short)1_726),
            (Id: 4, Name: "BR_FAILURE_MEMBER2", X: (short)2_641, Y: (short)1_726),
        })
        {
            var mob = CreateMob(player.Name, level: 50);
            Check(world.Enter(player.Id, player.Name, (frame, _) =>
            {
                if (player.Id == 2)
                {
                    failedRecipientCalls++;
                    throw new IOException("simulated broken recipient");
                }
                outbound.Add((player.Id, codec.Decode(frame.Span)));
                return ValueTask.CompletedTask;
            }), $"Failure-case Battle Royale participant {player.Id} could not be registered.");
            Check(world.SetCharacterState(player.Id, 0, 0, 1, 0, 0, player.X, player.Y, mob),
                $"Failure-case Battle Royale participant {player.Id} state could not be initialized.");
        }

        Check(world.TrySetParty(1, [1, 2]) && world.TrySetParty(3, [3, 4]),
            "Failure-case Battle Royale parties could not be formed.");
        var transition = CreateBucketTwoReadyTransition();
        var plan = LegacyBattleRoyaleGridRefreshRelay.TryCreate(world, transition, codec, clientTick: 36, keywordIndex: 19)
            ?? throw new InvalidOperationException("Failure-case Battle Royale grid plan was not created.");

        var sendResult = LegacyBattleRoyaleGridRefreshRelay.SendAsync(world, plan, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        Check(failedRecipientCalls == 1,
            $"Grid-refresh send failure was not isolated to one recipient attempt (calls={failedRecipientCalls}).");
        Check(sendResult.FailedRecipientIds.SequenceEqual([2]),
            $"Grid-refresh relay did not report the endpoint whose sends failed (ids={string.Join(',', sendResult.FailedRecipientIds)}).");
        Check(!world.TryRemovePartyMember(2, 0, out _) && !world.TryRemovePartyMember(4, 0, out _),
            "A broken connection stopped this transition from removing later in-arena party members.");
        Check(outbound.Any(static message => message.RecipientId == 1 && message.Frame.Header.Type == PartyRemoveConfirmation.MessageType &&
                BinaryPrimitives.ReadInt16LittleEndian(message.Frame.Payload.Span) == 2) &&
            outbound.Any(static message => message.RecipientId == 3 && message.Frame.Header.Type == PartyRemoveConfirmation.MessageType &&
                BinaryPrimitives.ReadInt16LittleEndian(message.Frame.Payload.Span) == 4) &&
            outbound.Any(static message => message.RecipientId == 4 && message.Frame.Header.Type == PartyRemoveConfirmation.MessageType &&
                BinaryPrimitives.ReadInt16LittleEndian(message.Frame.Payload.Span) == 0),
            "The transition did not continue sending party-cleanup confirmations to healthy members after an earlier send failed.");
    }

    public static void RecalledPartyMemberIsNotRemovedByTheFollowingGridPass()
    {
        var world = new WorldHub();
        var outbound = new List<(int RecipientId, DecodedFrame Frame)>();
        var codec = LegacyFrameCodec.CreateDefault();
        AddPlayer(world, outbound, codec, 1, "BR_RECALL_LEADER", 50, 2_620, 1_720);
        AddPlayer(world, outbound, codec, 2, "BR_RECALL_MEMBER", 100, 2_621, 1_721);
        AddPlayer(world, outbound, codec, 3, "BR_RECALL_MEMBER2", 401, 2_622, 1_722);
        Check(world.TrySetParty(1, [1, 2, 3]), "Battle Royale recall party fixture could not be formed.");

        var schedule = CreateSchedule();
        var transition = schedule.Tick(new DateTime(2026, 10, 5, 19, 0, 0)) ??
            throw new InvalidOperationException("Battle Royale ready transition was not produced.");
        Check(transition.AreaClearLevels == new LegacyBattleRoyaleLevelClear(100, 400),
            "The first Battle Royale bucket did not carry its expected area-clear band.");
        var clearPlan = LegacyBattleRoyaleAreaClearRelay.TryCreate(
            world, transition, codec, clientTick: 33, keywordIndex: 16, cityRandomX: 0, cityRandomY: 0);
        Check(clearPlan is { Actors.Count: 1 } && clearPlan.Actors[0].ConnectionId == 2,
            "The eligible Battle Royale party member was not recalled before the grid pass.");

        var refreshPlan = LegacyBattleRoyaleGridRefreshRelay.TryCreate(world, transition, codec, clientTick: 34, keywordIndex: 17);
        Check(refreshPlan is not null && refreshPlan.Actors.Select(static actor => actor.ConnectionId).SequenceEqual([1, 3]),
            "The grid pass included the member already recalled by ClearAreaLevel.");
        LegacyBattleRoyaleGridRefreshRelay.SendAsync(world, refreshPlan!, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        var partyRemovalFrames = outbound.Where(static message => message.Frame.Header.Type == PartyRemoveConfirmation.MessageType).ToArray();
        Check(partyRemovalFrames.All(static message => BinaryPrimitives.ReadInt16LittleEndian(message.Frame.Payload.Span) != 2) &&
            partyRemovalFrames.Select(static message => message.RecipientId).SequenceEqual([1, 2, 3]) &&
            BinaryPrimitives.ReadInt16LittleEndian(partyRemovalFrames.Single(static message => message.RecipientId == 1).Frame.Payload.Span) == 3 &&
            BinaryPrimitives.ReadInt16LittleEndian(partyRemovalFrames.Single(static message => message.RecipientId == 2).Frame.Payload.Span) == 3 &&
            BinaryPrimitives.ReadInt16LittleEndian(partyRemovalFrames.Single(static message => message.RecipientId == 3).Frame.Payload.Span) == 0,
            "The grid pass did not remove only the still-present member while keeping the recalled member in the party.");
        Check(world.TryRemovePartyMember(1, 2, out var recalledMember) && recalledMember is { RemovedConnectionId: 2 },
            "The previously recalled player did not remain in the party after the arena cleanup pass.");
    }

    private static LegacyBattleRoyaleSchedule CreateSchedule() => new(LegacyBattleRoyaleConfiguration.Parse("""
        { "BRItem": 700, "BRHour": 19, "ServerIndex": 0, "NumServerInGroup": 1, "TestServer": true, "LocalServer": false }
        """));

    private static LegacyBattleRoyaleTransition CreateBucketTwoReadyTransition()
    {
        var schedule = CreateSchedule();
        return schedule.Tick(new DateTime(2026, 10, 5, 19, 40, 0)) ??
            throw new InvalidOperationException("Third-bucket Battle Royale ready transition was not produced.");
    }

    private static LegacySummonedMob CreateSummon(WorldHub world, int summonerConnectionId)
    {
        var mob = CreateMob("BR_SUMMONER", level: 50);
        var score = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with { Special3 = 40 };
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        // The participant was entered by the caller, so replace its authoritative character state with the summon-capable fixture.
        Check(world.SetCharacterState(summonerConnectionId, 0, 0, 1, 0, 0, 2_639, 1_726, mob),
            "Battle Royale summon owner could not be prepared.");
        var skill = new LegacySkillDefinition(50, 20, 0, 20, 0, 0, 11, 8, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "BR_SUMMON");
        // The caller constructs WorldHub with the local 7.69 BaseSummon catalog.
        if (world.TryConsumeSkillMana(summonerConnectionId, skill, out _, out _, out _) != LegacySkillManaResult.Accepted)
            throw new InvalidOperationException("Battle Royale summon skill mana could not be consumed.");
        var result = world.TryApplySkillAttack(summonerConnectionId, summonerConnectionId, skill, null, 0, 90, out var outcome);
        if (result != LegacySkillAttackResult.Accepted || outcome?.SummonedMobs is not { Count: 1 })
            throw new InvalidOperationException($"Battle Royale summon could not be created: result={result}, outcome={outcome}.");
        return outcome.SummonedMobs[0];
    }

    private static string FindReference769TmsrvRun()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Backup", "Tools", "ReferenceSources", "TMProject2GlobalClient", "Servidor", "Server", "TMSrv", "run");
            if (Directory.Exists(candidate)) return candidate;
        }

        throw new DirectoryNotFoundException("The local 7.69 TMSrv run was not found from the test output directory.");
    }

    private static void AddPlayer(
        WorldHub world,
        List<(int RecipientId, DecodedFrame Frame)> outbound,
        LegacyFrameCodec codec,
        int connectionId,
        string name,
        int level,
        short x,
        short y,
        int guildId = 0,
        byte[]? mobSnapshot = null)
    {
        var mob = mobSnapshot ?? CreateMob(name, level);
        Check(world.Enter(connectionId, name, (frame, _) =>
        {
            outbound.Add((connectionId, codec.Decode(frame.Span)));
            return ValueTask.CompletedTask;
        }), $"Battle Royale grid participant {connectionId} could not be registered.");
        Check(world.SetCharacterState(connectionId, 0, guildId, 1, 0, 0, x, y, mob, LegacyAccountSnapshot.ClassMasterMortal),
            $"Battle Royale grid participant {connectionId} state could not be initialized.");
    }

    private static byte[] CreateMob(string name, int level)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        System.Text.Encoding.ASCII.GetBytes(name).CopyTo(mob, 0);
        new LegacyScore(level, 0, 0, 0, 0, 0, 0, 1_000, 1_000, 1_000, 250, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class BattleRoyaleScheduleTests
{
    public static void ConfigParsesAllRequiredFields()
    {
        var configuration = LegacyBattleRoyaleConfiguration.Parse(CreateJson(
            BRItem: 700,
            BRHour: 19,
            ServerIndex: 2,
            NumServerInGroup: 3,
            TestServer: true,
            LocalServer: false));

        Check(configuration.BRItem == 700 && configuration.BRHour == 19 &&
            configuration.ServerIndex == 2 && configuration.NumServerInGroup == 3 &&
            configuration.TestServer && !configuration.LocalServer,
            "The Battle Royale parser did not read all six PascalCase configuration values.");

        var zeroItem = LegacyBattleRoyaleConfiguration.Parse(CreateJson(BRItem: 0));
        var minimums = LegacyBattleRoyaleConfiguration.Parse(CreateJson(
            BRHour: 0, ServerIndex: 0, NumServerInGroup: 1));
        var maximums = LegacyBattleRoyaleConfiguration.Parse(CreateJson(
            BRHour: 23, ServerIndex: 9, NumServerInGroup: 10));
        Check(zeroItem.BRItem == 0 && minimums.BRHour == 0 &&
            minimums.ServerIndex == 0 && minimums.NumServerInGroup == 1 &&
            maximums.BRHour == 23 && maximums.ServerIndex == 9 &&
            maximums.NumServerInGroup == 10,
            "A valid disabled or boundary Battle Royale configuration was rejected or changed.");
    }

    public static void ConfigRejectsMissingOrWrongFields()
    {
        var missingItem = """
            { "BRHour": 19, "ServerIndex": 0, "NumServerInGroup": 1, "TestServer": false, "LocalServer": false }
            """;
        var wrongItemType = CreateJson().Replace("\"BRItem\": 700", "\"BRItem\": \"700\"", StringComparison.Ordinal);
        var wrongBooleanType = CreateJson().Replace("\"TestServer\": false", "\"TestServer\": \"false\"", StringComparison.Ordinal);

        foreach (var json in new[] { missingItem, wrongItemType, wrongBooleanType, "{" })
        {
            var error = Capture(() => LegacyBattleRoyaleConfiguration.Parse(json));
            Check(error is InvalidDataException,
                $"Invalid Battle Royale JSON was not rejected as InvalidDataException: {error?.GetType().Name ?? "no exception"}.");
        }
    }

    public static void ConfigRejectsInvalidRanges()
    {
        var invalidConfigurations = new[]
        {
            CreateJson(BRItem: -1),
            CreateJson(BRHour: -1),
            CreateJson(BRHour: 24),
            CreateJson(ServerIndex: -1),
            CreateJson(ServerIndex: 10),
            CreateJson(NumServerInGroup: 0),
            CreateJson(NumServerInGroup: 11),
        };

        foreach (var json in invalidConfigurations)
        {
            var error = Capture(() => LegacyBattleRoyaleConfiguration.Parse(json));
            Check(error is InvalidDataException,
                $"An out-of-range Battle Royale configuration was not rejected: {error?.GetType().Name ?? "no exception"}.");
        }
    }

    public static void ScheduleTransitionsAtPhasesZeroFiveThirteenAndFourteen()
    {
        var schedule = new LegacyBattleRoyaleSchedule(CreateConfiguration(TestServer: true));

        var ready = schedule.Tick(new DateTime(2026, 10, 5, 19, 0, 0));
        var battle = schedule.Tick(new DateTime(2026, 10, 5, 19, 5, 0));
        var reward = schedule.Tick(new DateTime(2026, 10, 5, 19, 13, 0));
        var ended = schedule.Tick(new DateTime(2026, 10, 5, 19, 14, 0));

        Check(ready is { PreviousState: 0, State: 1, BRItem: 700 } &&
            battle is { PreviousState: 1, State: 2, BRItem: 700 } &&
            reward is { PreviousState: 2, State: 3, BRItem: 700 } &&
            ended is { PreviousState: 3, State: 0, BRItem: 700 } &&
            schedule.CurrentState == 0,
            "The Battle Royale schedule did not transition through phases 0, 5, 13, and 14.");
    }

    public static void ScheduleEmitsLegacyAreaNotices()
    {
        var readyMessages = new[]
        {
            "Batalha Real para Nv < 100 inicia no coliseu em 2min.",
            "Batalha Real para Nv < 200 incia no coliseu em 2min.",
            "Batalha Real para qualquer nível inicia no coliseu em 2min.",
        };
        var startMessages = new[]
        {
            "Batalha Real está iniciando para Nv < 100.",
            "Batalha Real está iniciando para Nv < 200.",
            "Batalha Real está iniciando para qualquer nível.",
        };
        var readyArea = new LegacyAreaNotice(readyMessages[0], 2580, 1708, 2665, 1765);
        var startArea = new LegacyAreaNotice(startMessages[0], 2580, 1688, 2665, 1765);
        var rewardArea = new LegacyAreaNotice("Pr\u00EAmio do vencedor est\u00E1 no altar.", 2580, 1688, 2665, 1765);

        for (var bucket = 0; bucket < 3; bucket++)
        {
            var minuteBase = bucket * 20;
            var schedule = new LegacyBattleRoyaleSchedule(CreateConfiguration(TestServer: true));
            var ready = schedule.Tick(new DateTime(2026, 10, 5, 19, minuteBase, 0));
            var battle = schedule.Tick(new DateTime(2026, 10, 5, 19, minuteBase + 5, 0));
            var reward = schedule.Tick(new DateTime(2026, 10, 5, 19, minuteBase + 13, 0));
            var ended = schedule.Tick(new DateTime(2026, 10, 5, 19, minuteBase + 14, 0));

            Check(ready?.AreaNotice == readyArea with { Message = readyMessages[bucket] } &&
                battle?.AreaNotice == startArea with { Message = startMessages[bucket] } &&
                reward is { PreviousState: 2, State: 3, BRItem: 700 } &&
                reward?.AreaNotice == rewardArea &&
                ended is { State: 0, AreaNotice: null },
                $"The Battle Royale cycle {bucket} did not emit the legacy ready/start/reward messages while preserving the state transitions.");

            var codec = LegacyFrameCodec.CreateDefault();
            foreach (var notice in new[] { ready!.AreaNotice!, battle!.AreaNotice!, reward!.AreaNotice! })
            {
                var frame = codec.Decode(new MessagePanelConfirmation(notice.Message)
                    .ToFrame(codec, clientTick: 33, keywordIndex: 16));
                var expectedText = Encoding.Latin1.GetBytes(notice.Message);
                Check(frame.IsChecksumValid &&
                    frame.Header.Type == MessagePanelConfirmation.MessageType &&
                    frame.Header.Id == 0 &&
                    frame.Header.Size == MessagePanelConfirmation.PacketSize &&
                    frame.Payload.Span[..expectedText.Length].SequenceEqual(expectedText) &&
                    frame.Payload.Span[expectedText.Length] == 0,
                    $"The Battle Royale notice '{notice.Message}' did not preserve its 7.69 Latin-1 bytes in MSG_MessagePanel.");
            }
        }
    }

    public static void ScheduleCarriesLegacyAreaClearLevels()
    {
        for (var bucket = 0; bucket < 3; bucket++)
        {
            var minuteBase = bucket * 20;
            var schedule = new LegacyBattleRoyaleSchedule(CreateConfiguration(TestServer: true));
            var ready = schedule.Tick(new DateTime(2026, 10, 5, 19, minuteBase, 0));
            var battle = schedule.Tick(new DateTime(2026, 10, 5, 19, minuteBase + 5, 0));
            var reward = schedule.Tick(new DateTime(2026, 10, 5, 19, minuteBase + 13, 0));
            var ended = schedule.Tick(new DateTime(2026, 10, 5, 19, minuteBase + 14, 0));

            LegacyBattleRoyaleLevelClear? expected = bucket switch
            {
                0 => new LegacyBattleRoyaleLevelClear(100, 400),
                1 => new LegacyBattleRoyaleLevelClear(200, 400),
                _ => null,
            };
            Check(ready?.AreaClearLevels == expected && battle?.AreaClearLevels == expected &&
                reward?.AreaClearLevels is null && ended?.AreaClearLevels is null,
                $"The Battle Royale cycle {bucket} did not attach ClearAreaLevel bounds only to the first two ready/start bands.");
        }
    }

    public static void ScheduleCarriesGridRefreshAndPartyCleanupScope()
    {
        for (var bucket = 0; bucket < 3; bucket++)
        {
            var minuteBase = bucket * 20;
            var schedule = new LegacyBattleRoyaleSchedule(CreateConfiguration(TestServer: true));
            var ready = schedule.Tick(new DateTime(2026, 10, 5, 19, minuteBase, 0));
            var battle = schedule.Tick(new DateTime(2026, 10, 5, 19, minuteBase + 5, 0));
            var reward = schedule.Tick(new DateTime(2026, 10, 5, 19, minuteBase + 13, 0));
            var ended = schedule.Tick(new DateTime(2026, 10, 5, 19, minuteBase + 14, 0));

            Check(ready?.RefreshGridAndCleanupParties == true &&
                battle?.RefreshGridAndCleanupParties == true &&
                reward?.RefreshGridAndCleanupParties == false &&
                ended?.RefreshGridAndCleanupParties == false,
                $"Battle Royale cycle {bucket} did not refresh the arena grid and clean party members only on 0->1 and 1->2.");
        }
    }

    public static void ScheduleNoticesUseInclusiveAreaRecipients()
    {
        var schedule = new LegacyBattleRoyaleSchedule(CreateConfiguration(TestServer: true));
        var readyNotice = schedule.Tick(new DateTime(2026, 10, 5, 19, 0, 0))?.AreaNotice ??
            throw new InvalidOperationException("The Battle Royale schedule did not provide a ready notice for recipient selection.");
        var startNotice = schedule.Tick(new DateTime(2026, 10, 5, 19, 5, 0))?.AreaNotice ??
            throw new InvalidOperationException("The Battle Royale schedule did not provide a start notice for recipient selection.");

        var world = AttackPartySkillGateTests.CreateWorld(attackerGuild: 7, targetGuild: 8);
        Check(world.UpdatePosition(1, 2_580, 1_708) && world.UpdatePosition(2, 2_666, 1_765),
            "The Battle Royale notice boundary participants could not be positioned.");
        Check(world.GetParticipantIdsInArea(readyNotice!.X1, readyNotice.Y1, readyNotice.X2, readyNotice.Y2).SequenceEqual([1]) &&
            world.GetParticipantIdsInArea(startNotice!.X1, startNotice.Y1, startNotice.X2, startNotice.Y2).SequenceEqual([1]),
            "The Battle Royale notice included an outside peer or excluded the inclusive lower-left corner.");

        Check(world.UpdatePosition(2, 2_665, 1_765) &&
            world.GetParticipantIdsInArea(readyNotice.X1, readyNotice.Y1, readyNotice.X2, readyNotice.Y2).SequenceEqual([1, 2]) &&
            world.GetParticipantIdsInArea(startNotice.X1, startNotice.Y1, startNotice.X2, startNotice.Y2).SequenceEqual([1, 2]),
            "The Battle Royale notice excluded the inclusive upper-right corner.");

        Check(world.UpdatePosition(1, 2_580, 1_707) &&
            world.GetParticipantIdsInArea(readyNotice.X1, readyNotice.Y1, readyNotice.X2, readyNotice.Y2).SequenceEqual([2]) &&
            world.GetParticipantIdsInArea(startNotice.X1, startNotice.Y1, startNotice.X2, startNotice.Y2).SequenceEqual([1, 2]),
            "The Battle Royale ready and active notices did not use their distinct inclusive Y bounds.");
    }

    public static void RewardSpawnCreatesAltarItemBeforeRewardNotice()
    {
        var schedule = new LegacyBattleRoyaleSchedule(CreateConfiguration(TestServer: true));
        _ = schedule.Tick(new DateTime(2026, 10, 5, 19, 0, 0));
        _ = schedule.Tick(new DateTime(2026, 10, 5, 19, 5, 0));
        var transition = schedule.Tick(new DateTime(2026, 10, 5, 19, 13, 0)) ??
            throw new InvalidOperationException("The Battle Royale schedule did not produce its reward transition.");

        var codec = LegacyFrameCodec.CreateDefault();
        var outbound = new List<(int RecipientId, ushort MessageType)>();
        var world = CreateRewardWorld(codec, outbound);
        var plan = LegacyBattleRoyaleRewardRelay.TryCreate(
            world, transition, rotate: 2, codec, clientTick: 33, keywordIndex: 16);

        if (plan is null || plan.Frames.Count != 2)
            throw new InvalidOperationException("The Battle Royale reward did not produce a ground-item frame followed by its notice frame.");
        var create = codec.Decode(plan.Frames[0].Frame);
        var notice = codec.Decode(plan.Frames[1].Frame);
        Check(create.IsChecksumValid && create.Header.Type == CreateItemConfirmation.MessageType &&
            BinaryPrimitives.ReadUInt16LittleEndian(create.Payload.Span) == 2621 &&
            BinaryPrimitives.ReadUInt16LittleEndian(create.Payload.Span[sizeof(ushort)..]) == 1726 &&
            LegacyItem.Read(create.Payload.Span.Slice(sizeof(ushort) * 3, LegacyItem.SizeInBytes)) ==
                new LegacyItem(700, 0, 0, 0, 0, 0, 0) &&
            create.Payload.Span[(sizeof(ushort) * 3) + LegacyItem.SizeInBytes] == 2,
            "The Battle Royale reward MSG_CreateItem did not contain the configured altar item and rotation.");
        Check(notice.IsChecksumValid && notice.Header.Type == MessagePanelConfirmation.MessageType &&
            notice.Payload.Span[.."Pr\u00EAmio do vencedor est\u00E1 no altar.".Length]
                .SequenceEqual(Encoding.Latin1.GetBytes("Pr\u00EAmio do vencedor est\u00E1 no altar.")),
            "The Battle Royale reward MSG_MessagePanel did not preserve its legacy text bytes.");
        Check(plan.Frames[0].RecipientIds.SequenceEqual([1]) &&
            plan.Frames[1].RecipientIds.SequenceEqual([1, 2]),
            "The Battle Royale item and area notice did not use their separate legacy recipient regions.");
        LegacyBattleRoyaleRewardRelay.SendAsync(world, plan, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        Check(outbound.SequenceEqual([
                (1, CreateItemConfirmation.MessageType),
                (1, MessagePanelConfirmation.MessageType),
                (2, MessagePanelConfirmation.MessageType)]),
            "The Battle Royale relay did not deliver MSG_CreateItem before MessagePanel to the captured recipients.");

        var pickupResult = world.TryGetGroundItem(1, 1, 0, plan.WireItemId, 2621, 1726, out var pickup);
        Check(pickupResult == LegacyGetItemResult.Accepted && pickup?.Item == new LegacyItem(700, 0, 0, 0, 0, 0, 0),
            "The relayed Battle Royale prize was not present in authoritative ground-item state.");

        var invalidTransition = transition with { BRItem = LegacyItemDataTable.MaxItemIndex };
        var invalidPlan = LegacyBattleRoyaleRewardRelay.TryCreate(
            world, invalidTransition, rotate: 2, codec, clientTick: 33, keywordIndex: 16);
        var absentItemResult = world.TryGetGroundItem(2, 1, 0, 10_001, 2621, 1726, out _);
        Check(invalidPlan is null && absentItemResult == LegacyGetItemResult.GroundItemNotFound,
            "An out-of-table Battle Royale prize created an item or retained a reward relay.");
    }

    public static void ScheduleRepeatsDoNotRepeatTransitions()
    {
        var schedule = new LegacyBattleRoyaleSchedule(CreateConfiguration(TestServer: true));
        var ready = schedule.Tick(new DateTime(2026, 10, 5, 19, 0, 0));

        Check(ready is { State: 1 } &&
            schedule.Tick(new DateTime(2026, 10, 5, 19, 0, 30)) is null &&
            schedule.Tick(new DateTime(2026, 10, 5, 19, 6, 0)) is null &&
            schedule.CurrentState == 1,
            "Repeated or non-transition ticks changed the Battle Royale state or emitted a duplicate transition.");
    }

    public static void ScheduleStartsMidCycleAtZero()
    {
        var schedule = new LegacyBattleRoyaleSchedule(CreateConfiguration(TestServer: true));

        Check(schedule.Tick(new DateTime(2026, 10, 5, 19, 8, 0)) is null &&
            schedule.CurrentState == 0 &&
            schedule.Tick(new DateTime(2026, 10, 5, 19, 20, 0)) is { PreviousState: 0, State: 1 },
            "A Battle Royale schedule started in the middle of a cycle instead of waiting for the next phase zero.");
    }

    public static void ScheduleDoesNotCatchUpSkippedFiveOrThirteen()
    {
        var skippedBattleStart = new LegacyBattleRoyaleSchedule(CreateConfiguration(TestServer: true));
        Check(skippedBattleStart.Tick(new DateTime(2026, 10, 5, 19, 0, 0)) is { State: 1 } &&
            skippedBattleStart.Tick(new DateTime(2026, 10, 5, 19, 6, 0)) is null &&
            skippedBattleStart.Tick(new DateTime(2026, 10, 5, 19, 13, 0)) is null &&
            skippedBattleStart.CurrentState == 1,
            "The schedule caught up to battle/reward after phase 5 was skipped.");

        var skippedRewardStart = new LegacyBattleRoyaleSchedule(CreateConfiguration(TestServer: true));
        Check(skippedRewardStart.Tick(new DateTime(2026, 10, 5, 19, 0, 0)) is { State: 1 } &&
            skippedRewardStart.Tick(new DateTime(2026, 10, 5, 19, 5, 0)) is { State: 2 } &&
            skippedRewardStart.Tick(new DateTime(2026, 10, 5, 19, 14, 0)) is null &&
            skippedRewardStart.CurrentState == 2,
            "The schedule caught up to reward after phase 13 was skipped.");
    }

    public static void ScheduleUsesDailyServerRotationAndExplicitBypasses()
    {
        var firstChannel = CreateConfiguration(ServerIndex: 0);
        var secondChannel = CreateConfiguration(ServerIndex: 1);
        var thirdChannel = CreateConfiguration(ServerIndex: 2);
        var dayOne = new DateTime(2026, 10, 1, 19, 0, 0);
        var dayThree = new DateTime(2026, 10, 3, 19, 0, 0);

        Check(firstChannel.IsEligible(dayOne) && !secondChannel.IsEligible(dayOne) &&
            thirdChannel.IsEligible(dayThree) && !firstChannel.IsEligible(dayThree),
            "The Battle Royale schedule did not use (dayOfMonth - 1) modulo the group size.");
        Check(CreateConfiguration(ServerIndex: 1, TestServer: true).IsEligible(dayOne) &&
            CreateConfiguration(ServerIndex: 1, LocalServer: true).IsEligible(dayOne) &&
            !CreateConfiguration(BRItem: 0, ServerIndex: 1, TestServer: true).IsEligible(dayOne),
            "An explicit bypass did not override channel rotation or BRItem=0 did not disable it.");
        Check(new LegacyBattleRoyaleSchedule(CreateConfiguration(TestServer: true))
                .Tick(new DateTime(2026, 10, 5, 18, 0, 0)) is null,
            "The Battle Royale state changed outside BRHour.");
    }

    public static void ControllerPublishesStateAndItemWithoutOverwritingWorldFields()
    {
        var configuration = CreateConfiguration(TestServer: true);
        var world = new WorldHub();
        var initial = new LegacyCombatWorldState(
            RvrWarActive: true,
            RvrState: 4,
            CastleState: 2,
            GuildTowerState: 3,
            NewbieEventServer: true,
            DoubleModeMultiplier: 5,
            KefraLive: false,
            Kingdom1Clear: 6,
            Kingdom2Clear: 7,
            CastleQuestClear: 8,
            BattleRoyaleState: 9,
            BattleRoyaleItem: 901);
        world.SetCombatWorldState(initial);
        var controller = new LegacyBattleRoyaleScheduleController(configuration, world);
        var times = new[] { 0, 5, 13, 14 };
        var states = new[] { 1, 2, 3, 0 };
        var previous = new[] { 0, 1, 2, 3 };

        for (var index = 0; index < times.Length; index++)
        {
            var transition = controller.Tick(new DateTime(2026, 10, 5, 19, times[index], 0));
            var expected = initial with { BattleRoyaleState = states[index], BattleRoyaleItem = 700 };

            if (index == 2)
            {
                Check(transition is { PreviousState: 2, State: 3, BRItem: 700 } &&
                    world.GetCombatWorldState() == initial with { BattleRoyaleState = 2, BattleRoyaleItem = 700 },
                    "The controller published Battle Royale state 3 before the altar reward spawn attempt.");
                controller.PublishRewardTransition(transition!);
            }

            if (index is 1 or 3)
            {
                Check(world.GetCombatWorldState() == initial with { BattleRoyaleState = previous[index], BattleRoyaleItem = 700 },
                    $"The controller published Battle Royale state {states[index]} before the legacy door call at phase {times[index]}.");
                var publishDoorTransition = typeof(LegacyBattleRoyaleScheduleController).GetMethod("PublishDoorTransition");
                Check(publishDoorTransition is not null, "The controller has no publish step after the legacy door relay.");
                publishDoorTransition!.Invoke(controller, [transition]);
            }

            Check(transition is { } actual && actual.PreviousState == previous[index] &&
                actual.State == states[index] && actual.BRItem == 700 &&
                world.GetCombatWorldState() == expected,
                $"The controller did not publish Battle Royale transition {previous[index]}->{states[index]} while preserving other world state.");
        }

        Check(controller.Tick(new DateTime(2026, 10, 5, 19, 14, 30)) is null &&
            world.GetCombatWorldState() == initial with { BattleRoyaleState = 0, BattleRoyaleItem = 700 },
            "A duplicate schedule tick published another transition or changed unrelated world state.");
    }

    private static LegacyBattleRoyaleConfiguration CreateConfiguration(
        int BRItem = 700,
        int BRHour = 19,
        int ServerIndex = 0,
        int NumServerInGroup = 3,
        bool TestServer = false,
        bool LocalServer = false) => LegacyBattleRoyaleConfiguration.Parse(CreateJson(
            BRItem, BRHour, ServerIndex, NumServerInGroup, TestServer, LocalServer));

    private static WorldHub CreateRewardWorld(
        LegacyFrameCodec codec,
        List<(int RecipientId, ushort MessageType)> outbound)
    {
        var world = new WorldHub();
        foreach (var (connectionId, name, x) in new[]
                 {
                     (1, "BR_REWARD_NEAR", (short)2621),
                     (2, "BR_NOTICE_ONLY", (short)2638),
                 })
        {
            Check(world.Enter(connectionId, name, (frame, _) =>
            {
                outbound.Add((connectionId, codec.Decode(frame.Span).Header.Type));
                return ValueTask.CompletedTask;
            }), "A Battle Royale reward participant was not registered.");

            var mob = new byte[LegacyAccountSnapshot.CharacterStride];
            new LegacyScore(50, 0, 0, 0, 0, 0, 0, 5000, 5000, 5000, 5000, 0, 0, 0, 0, 0, 0, 0, 0)
                .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
            Check(world.SetCharacterState(connectionId, 0, 7, 1, 0, 0, x, 1726, mob),
                "A Battle Royale reward participant position was not installed.");
        }

        return world;
    }

    private static string CreateJson(
        int BRItem = 700,
        int BRHour = 19,
        int ServerIndex = 0,
        int NumServerInGroup = 3,
        bool TestServer = false,
        bool LocalServer = false) => $$"""
        {
          "BRItem": {{BRItem}},
          "BRHour": {{BRHour}},
          "ServerIndex": {{ServerIndex}},
          "NumServerInGroup": {{NumServerInGroup}},
          "TestServer": {{TestServer.ToString().ToLowerInvariant()}},
          "LocalServer": {{LocalServer.ToString().ToLowerInvariant()}}
        }
        """;

    private static Exception? Capture(Action action)
    {
        try { action(); return null; }
        catch (Exception error) { return error; }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

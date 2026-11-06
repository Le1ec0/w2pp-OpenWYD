using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;
using WydCdk.Server;
using WydCdk.World;

internal static class LegacyMobAiRuntimeTests
{
    public static void Run()
    {
        DirectTicksVisitEachGeneratedNpcOncePerFourTurns();
        ScheduledTicksProcessSourceAlignedPeaceBeforeCombat();
        DeadIdleAndPeaceGeneratedNpcsAreRemovedOnPeaceShards();
        GeneratedPeaceNpcLifeAuraHealsAndConsumesItsTimer();
        GeneratedPeaceNpcPoisonAffectAppliesTheLegacyNpcDamageCap();
        GeneratedPeaceNpcBestialAuraConsumesTimerAndExpiresWithScoreOnly();
        GeneratedCombatNpcLifeAuraRunsEveryFourthVisitWithoutATarget();
        GeneratedCombatNpcPoisonRunsEveryFourthVisitWithoutATarget();
        GeneratedCombatNpcThunderAffectConsumesTimerAndExpiresWithScoreOnly();
        NonGeneratedPeaceNpcThunderAffectConsumesTimerAndExpiresWithScoreOnly();
        NonGeneratedCombatNpcThunderAffectConsumesTimerAndExpiresWithScoreOnly();
        NonGeneratedPeaceNpcBestialAuraConsumesTimerAndExpiresWithScoreOnly();
        NonGeneratedCombatNpcBestialAuraConsumesTimerAndExpiresWithScoreOnly();
        NonGeneratedPeaceNpcTypeThirtyTwoConsumesTimerAndExpiresWithScoreOnly();
        NonGeneratedCombatNpcTypeThirtyTwoConsumesTimerWithoutATarget();
        NonGeneratedPeaceNpcLifeAuraHealsAndConsumesItsTimer();
        NonGeneratedCombatNpcLifeAuraHealsOnFourthOpportunityWithoutATarget();
        NonGeneratedPeaceNpcPoisonUsesLegacyDamageCapAndExpires();
        NonGeneratedPeaceNpcPoisonClampsToOneAndRefreshesOnExpiry();
        NonGeneratedCombatNpcPoisonRunsEveryFourthVisitWithoutATarget();
        GeneratedCombatNpcBestialAuraConsumesTimerAndExpiresWithScoreOnly();
        GeneratedPeaceNpcTypeThirtyTwoConsumesTimerAndExpiresWithScoreOnly();
        GeneratedCombatNpcTypeThirtyTwoConsumesTimerWithoutATarget();
        ExtendedSkillBleedingTypeFortySevenUsesGenericNpcAndSummonTimerTail();
        GeneratedPeaceNpcTypeTwentyFourExpiresWithTypeThreeRemoval();
        GeneratedCombatNpcTypeTwentyFourExpiresWithoutATarget();
        PeaceRouteTypeFiveSummonThunderConsumesItsTimer();
        CombatSummonThunderConsumesItsTimerWithoutSkippingAttack();
        PeaceRouteTypeFiveSummonTypeThirtyTwoConsumesItsTimer();
        CombatSummonTypeThirtyTwoConsumesItsTimerWithoutSkippingAttack();
        PeaceRouteTypeFiveSummonBestialAuraConsumesItsTimer();
        CombatSummonBestialAuraConsumesItsTimer();
        PeaceRouteTypeFiveSummonLifeAuraHealsAndExpiresWithoutSkippingMovement();
        CombatSummonLifeAuraHealsAndExpiresOnItsAffectOpportunity();
        PeaceRouteTypeFiveSummonPoisonDrainsHpAndPreservesFollowerMovement();
        CombatSummonPoisonUsesLearnedSkillCapAndPreservesAttack();
        CombatSummonPoisonClampsHpToOne();
        PeaceTypeTwentySynchronizesLinkedClanFourMount();
        AdjacentTurnsDoNotDuplicateOneShardAttack();
        OutOfRangeExplicitTargetIsChasedAndRelayedAcrossViewChanges();
        OutOfLeashCombatNpcReturnsToRouteAndRelaysPeaceTransition();
        PeaceNpcContinuesWalkingTowardItsCurrentSegmentAndRelaysAction();
        PeaceRouteTypeFiveSummonExpiresWhenItsTypeTwentyFourTimerEnds();
        CombatSummonExpiresOnItsTwentiethTypeTwentyFourTimerOpportunity();
        TargetlessCombatSummonExpiresOnItsTwentiethTypeTwentyFourTimerOpportunity();
        PeaceRouteTypeFiveSummonFollowsItsSummonerAndRelaysWithinCapturedViews();
        PeaceRouteTypeFiveTeleportUsesLegacySpeedAndGridSearchRadius();
        CombatRouteTypeFiveSummonAttacksAtNineteenAndTeleportsAtTwenty();
        CombatRouteTypeFiveSummonDoesNotAttackWhenTeleportGridIsBlocked();
        RouteTypeFiveGeneratedNpcWithInvalidLeaderDespawnRunsOnCombatShard();
        RunequestLv6AreaDespawnRunsBeforePeaceAndCombatRandomness();
        RunequestLv6AreaDespawnRemovesRouteTypeFiveSummonsInBothModes();
        ResurrectionRelayIsRequiredWhenTheTargetRevives();
        ResurrectionRelayContinuesAfterATargetDisconnect();
        CancellationAndSingleRuntimeLoopAreDeterministic().GetAwaiter().GetResult();
    }

    private static void DeadIdleAndPeaceGeneratedNpcsAreRemovedOnPeaceShards()
    {
        var hub = NewHub(allEmptySkills: true);
        AddPlayer(hub, 1, "DEAD_NPC_VIEW", 11, 10, hp: 1_000);
        var peaceId = hub.EnterNpc(CreateMob("DEAD_PEACE", hp: 0, clan: 2, emptySkills: true),
            10, 10, requestedConnectionId: 20_002, generateIndex: 42);
        var idleId = hub.EnterNpc(CreateMob("DEAD_IDLE", hp: 0, clan: 2, emptySkills: true),
            10, 10, requestedConnectionId: 20_008, generateIndex: 42);
        Check(peaceId == 20_002 && idleId == 20_008 &&
              hub.SetNpcCombatState(idleId, mode: 3, currentTarget: 0, new int[LegacyMobAiPlanner.EnemyCapacity]),
            "The dead generated Peace/Idle NPC fixture could not be registered.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();

        Check(tick.PeaceShardIndex == 0 && tick.VisitedPeaceNpcIds.Contains(peaceId) &&
              tick.VisitedPeaceNpcIds.Contains(idleId) &&
              !hub.TryGetNpcSnapshot(peaceId, out _) && !hub.TryGetNpcSnapshot(idleId, out _) &&
              outcomes.Count == 2 && outcomes.All(outcome => outcome.Removal is
                  { RemoveType: 1, ViewRecipientConnectionIds.Count: 1 } removal &&
                  (removal.ConnectionId == peaceId || removal.ConnectionId == idleId) &&
                  removal.ViewRecipientConnectionIds.SequenceEqual([1])),
            $"The source-aligned Peace shard did not remove both dead Peace/Idle generated NPCs with captured RemoveMob(type=1) views; shard={tick.PeaceShardIndex}, visited=[{string.Join(',', tick.VisitedPeaceNpcIds)}], remaining={hub.GetNpcSnapshots().Count}, removals={outcomes.Count}.");
    }

    private static void GeneratedPeaceNpcLifeAuraHealsAndConsumesItsTimer()
    {
        var hub = NewHub(allEmptySkills: true);
        var affect = CreateAffectSnapshot(type: 17, value: 7, level: 500, time: 2);
        var npcId = hub.EnterNpc(
            CreateMob("LIFE_AURA", hp: 800, clan: 2, emptySkills: true),
            10,
            10,
            affect,
            requestedConnectionId: 20_002,
            generateIndex: 42);
        Check(npcId == 20_002, "The generated Life Aura NPC fixture could not be registered.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        LegacyMobAiTickResult? visitedTick = null;
        for (var callback = 0; callback < LegacyMobAiRuntime.PeaceShardCount; callback++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            if (tick.VisitedPeaceNpcIds.Contains(npcId))
            {
                visitedTick = tick;
                break;
            }
        }

        var hasAfterTick = hub.TryGetNpcSnapshot(npcId, out var afterTick) && afterTick is not null;
        var afterHp = hasAfterTick ? LegacyMobCombatState.Read(afterTick!.MobSnapshot).CurrentScore.Hp : -1;
        var afterTime = hasAfterTick ? BinaryPrimitives.ReadUInt32LittleEndian(afterTick!.AffectSnapshot.AsSpan(4)) : uint.MaxValue;
        Check(visitedTick is not null && hasAfterTick && afterHp == 1_000 && afterTime == 1,
            $"The generated Peace timer must apply Type 17 HP regen before AI, cap HP at MaxHp, and decrement the finite affect once; visited={visitedTick is not null}, hp={afterHp}, time={afterTime}, outcomes={outcomes.Count}, affectTicks={outcomes.Count(static value => value.AffectTick is not null)}.");

        var firstAffectTick = outcomes.Single(static value => value.AffectTick is not null).AffectTick!;
        Check(firstAffectTick.Hp == 1_000 && firstAffectTick.Damage == 200 && firstAffectTick.ScoreRefreshRequired,
            "Life Aura must relay the capped HP and actual HP delta and request a score frame when HP changes.");

        visitedTick = null;
        for (var callback = 0; callback < LegacyMobAiRuntime.PeaceShardCount; callback++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            if (tick.VisitedPeaceNpcIds.Contains(npcId))
            {
                visitedTick = tick;
                break;
            }
        }

        Check(visitedTick is not null && hub.TryGetNpcSnapshot(npcId, out var afterExpiry) && afterExpiry is not null &&
              afterExpiry.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              LegacyMobCombatState.Read(afterExpiry.MobSnapshot).CurrentScore.Hp == 1_000,
            "A finite Life Aura reaching zero must clear its four fields without changing the capped HP.");
        var expiryTick = outcomes.Single(static value => value.AffectTick is { Damage: 0 }).AffectTick!;
        Check(expiryTick.ScoreRefreshRequired && outcomes.Count(static value => value.AffectTick is not null) == 2,
            "Life Aura expiry must send the final HP frame and one score refresh in that NPC timer visit.");
    }

    private static void GeneratedPeaceNpcPoisonAffectAppliesTheLegacyNpcDamageCap()
    {
        var hub = NewHub(allEmptySkills: true);
        var mob = CreateMob("POISON_AFFECT", hp: 10_000, clan: 2, emptySkills: true);
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), 0);
        var affect = CreateAffectSnapshot(type: 20, value: 10, level: 1, time: 2);
        var npcId = hub.EnterNpc(
            mob,
            10,
            10,
            affect,
            requestedConnectionId: 20_002,
            generateIndex: 42);
        Check(npcId == 20_002, "The generated Poison affect NPC fixture could not be registered.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        LegacyMobAiTickResult? firstTick = null;
        for (var callback = 0; callback < LegacyMobAiRuntime.PeaceShardCount; callback++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            if (tick.VisitedPeaceNpcIds.Contains(npcId))
            {
                firstTick = tick;
                break;
            }
        }

        var hasAfterFirstTick = hub.TryGetNpcSnapshot(npcId, out var afterFirstTick) && afterFirstTick is not null;
        Check(firstTick is not null && hasAfterFirstTick &&
              LegacyMobCombatState.Read(afterFirstTick!.MobSnapshot).CurrentScore.Hp == 9_000 &&
              BinaryPrimitives.ReadUInt32LittleEndian(afterFirstTick!.AffectSnapshot.AsSpan(4)) == 1,
            $"The source Type 20 tick must cap poison damage for a generated NPC at 1,000 HP and decrement its finite time once; visited={firstTick is not null}, snapshot={hasAfterFirstTick}.");

        var firstAffectTick = outcomes.Single(static value => value.AffectTick is not null).AffectTick!;
        Check(firstAffectTick.Hp == 9_000 && firstAffectTick.Damage == -1_000 && firstAffectTick.ScoreRefreshRequired,
            "Type 20 must preserve the legacy signed HP delta and request the score refresh after poison damage.");

        LegacyMobAiTickResult? expiryTickResult = null;
        for (var callback = 0; callback < LegacyMobAiRuntime.PeaceShardCount; callback++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            if (tick.VisitedPeaceNpcIds.Contains(npcId))
            {
                expiryTickResult = tick;
                break;
            }
        }

        Check(expiryTickResult is not null && hub.TryGetNpcSnapshot(npcId, out var afterExpiry) && afterExpiry is not null &&
              LegacyMobCombatState.Read(afterExpiry.MobSnapshot).CurrentScore.Hp == 8_000 &&
              afterExpiry.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]),
            "A second Type 20 tick must apply the NPC damage cap and clear the affect when its timer expires.");
        var expiryOutcome = outcomes.Where(static value => value.AffectTick is not null).Select(static value => value.AffectTick!).Last();
        Check(expiryOutcome.Hp == 8_000 && expiryOutcome.Damage == -1_000 && expiryOutcome.ScoreRefreshRequired &&
              outcomes.Count(static value => value.AffectTick is not null) == 2,
            "The poison expiry tick must relay its final HP delta and one score refresh.");
    }

    private static void GeneratedPeaceNpcBestialAuraConsumesTimerAndExpiresWithScoreOnly()
    {
        var hub = NewHub(allEmptySkills: true);
        var affect = CreateAffectSnapshot(type: 23, value: 100, level: 1, time: 2);
        var npcId = hub.EnterNpc(
            CreateMob("PEACE_AURA", hp: 1_000, clan: 1, emptySkills: true),
            10,
            10,
            affect,
            requestedConnectionId: 20_002,
            generateIndex: 42);
        Check(npcId == 20_002, "The generated Peace Bestial Aura NPC fixture could not be registered.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        LegacyMobAiTickResult? firstTimerTick = null;
        for (var callback = 0; callback < LegacyMobAiRuntime.PeaceShardCount; callback++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0, "Type 23 on a generated Peace NPC must not synthesize an area attack.");
            if (tick.VisitedPeaceNpcIds.Contains(npcId))
            {
                firstTimerTick = tick;
                break;
            }
        }

        Check(firstTimerTick is not null && hub.TryGetNpcSnapshot(npcId, out var afterFirstTick) && afterFirstTick is not null &&
              LegacyMobCombatState.Read(afterFirstTick.MobSnapshot).CurrentScore.Hp == 1_000 &&
              BinaryPrimitives.ReadUInt32LittleEndian(afterFirstTick.AffectSnapshot.AsSpan(4)) == 1 &&
              outcomes.All(static outcome => outcome.AffectTick is null),
            "The first Peace Type 23 timer visit must decrement its finite time without changing HP or relaying a frame.");

        LegacyMobAiTickResult? expiryTick = null;
        for (var callback = 0; callback < LegacyMobAiRuntime.PeaceShardCount; callback++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0, "A generated Peace Type 23 NPC must remain non-attacking through expiry.");
            if (tick.VisitedPeaceNpcIds.Contains(npcId))
            {
                expiryTick = tick;
                break;
            }
        }

        Check(expiryTick is not null && outcomes.Count(static outcome => outcome.AffectTick is not null) == 1 &&
              hub.TryGetNpcSnapshot(npcId, out var afterExpiry) && afterExpiry is not null &&
              LegacyMobCombatState.Read(afterExpiry.MobSnapshot).CurrentScore.Hp == 1_000 &&
              afterExpiry.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              outcomes.Single(static outcome => outcome.AffectTick is not null).AffectTick is
                  { Hp: 1_000, Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false },
            "Peace Type 23 expiry must clear the affect and refresh score only, without an HP update.");
    }

    private static void GeneratedCombatNpcLifeAuraRunsEveryFourthVisitWithoutATarget()
    {
        var hub = NewHub(allEmptySkills: true);
        var affect = CreateAffectSnapshot(type: 17, value: 7, level: 500, time: 2);
        var npcId = hub.EnterNpc(
            CreateMob("COMBAT_LIFE_AURA", hp: 800, clan: 2, emptySkills: true),
            10,
            10,
            affect,
            requestedConnectionId: 20_002,
            generateIndex: 42);
        Check(npcId == 20_002 &&
              hub.SetNpcCombatState(npcId, LegacyNpcMode.Combat, 0, new int[LegacyMobAiPlanner.EnemyCapacity]),
            "The generated Combat Life Aura NPC fixture could not be registered without a target.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        for (var tickIndex = 0; tickIndex < 11; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0,
                "A generated Combat NPC without an enemy list must not attack while its timer phase advances.");
        }

        Check(outcomes.Count == 0 && hub.TryGetNpcSnapshot(npcId, out var beforeFirstAffect) &&
              beforeFirstAffect is not null &&
              LegacyMobCombatState.Read(beforeFirstAffect.MobSnapshot).CurrentScore.Hp == 800 &&
              BinaryPrimitives.ReadUInt32LittleEndian(beforeFirstAffect.AffectSnapshot.AsSpan(4)) == 2,
            "Combat Life Aura must wait through the first three 800 ms opportunities for this NPC.");

        for (var tickIndex = 0; tickIndex < 4; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0,
                "A generated Combat NPC without an enemy list must remain non-attacking on its Life Aura timer visit.");
        }

        Check(outcomes.Count == 1 && hub.TryGetNpcSnapshot(npcId, out var afterFirstAffect) &&
              afterFirstAffect is not null &&
              LegacyMobCombatState.Read(afterFirstAffect.MobSnapshot).CurrentScore.Hp == 1_000 &&
              BinaryPrimitives.ReadUInt32LittleEndian(afterFirstAffect.AffectSnapshot.AsSpan(4)) == 1 &&
              outcomes[0].AffectTick is { Hp: 1_000, Damage: 200, ScoreRefreshRequired: true },
            "Combat Life Aura must heal/cap HP and decrement its timer on the fourth eligible visit.");

        for (var tickIndex = 0; tickIndex < 16; tickIndex++)
            runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();

        Check(outcomes.Count == 2 && hub.TryGetNpcSnapshot(npcId, out var afterExpiry) &&
              afterExpiry is not null && afterExpiry.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              outcomes[1].AffectTick is { Hp: 1_000, Damage: 0, ScoreRefreshRequired: true },
            "Combat Life Aura must clear and refresh the score when it expires on the next four-visit phase.");
    }

    private static void GeneratedCombatNpcPoisonRunsEveryFourthVisitWithoutATarget()
    {
        var hub = NewHub(allEmptySkills: true);
        var mob = CreateMob("COMBAT_POISON", hp: 10_000, clan: 2, emptySkills: true);
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), 0);
        var affect = CreateAffectSnapshot(type: 20, value: 10, level: 1, time: 2);
        var npcId = hub.EnterNpc(
            mob,
            10,
            10,
            affect,
            requestedConnectionId: 20_002,
            generateIndex: 42);
        Check(npcId == 20_002 &&
              hub.SetNpcCombatState(npcId, LegacyNpcMode.Combat, 0, new int[LegacyMobAiPlanner.EnemyCapacity]),
            "The generated Combat Poison NPC fixture could not be registered without a target.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        for (var tickIndex = 0; tickIndex < 11; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0,
                "A generated Combat NPC without an enemy list must not attack while its timer phase advances.");
        }

        Check(outcomes.Count == 0 && hub.TryGetNpcSnapshot(npcId, out var beforeFirstAffect) &&
              beforeFirstAffect is not null &&
              LegacyMobCombatState.Read(beforeFirstAffect.MobSnapshot).CurrentScore.Hp == 10_000 &&
              BinaryPrimitives.ReadUInt32LittleEndian(beforeFirstAffect.AffectSnapshot.AsSpan(4)) == 2,
            "Combat Poison must wait through the first three 800 ms opportunities for this NPC.");

        for (var tickIndex = 0; tickIndex < 4; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0,
                "A generated Combat NPC without an enemy list must remain non-attacking on its Poison timer visit.");
        }

        Check(outcomes.Count == 1 && hub.TryGetNpcSnapshot(npcId, out var afterFirstAffect) &&
              afterFirstAffect is not null &&
              LegacyMobCombatState.Read(afterFirstAffect.MobSnapshot).CurrentScore.Hp == 9_000 &&
              BinaryPrimitives.ReadUInt32LittleEndian(afterFirstAffect.AffectSnapshot.AsSpan(4)) == 1 &&
              outcomes[0].AffectTick is { Hp: 9_000, Damage: -1_000, ScoreRefreshRequired: true },
            "Combat Poison must apply the shared NPC damage cap and decrement on the fourth eligible visit.");

        for (var tickIndex = 0; tickIndex < 16; tickIndex++)
            runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();

        Check(outcomes.Count == 2 && hub.TryGetNpcSnapshot(npcId, out var afterExpiry) &&
              afterExpiry is not null &&
              LegacyMobCombatState.Read(afterExpiry.MobSnapshot).CurrentScore.Hp == 8_000 &&
              afterExpiry.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              outcomes[1].AffectTick is { Hp: 8_000, Damage: -1_000, ScoreRefreshRequired: true },
            "Combat Poison must apply its final NPC damage and clear the affect on its next four-visit phase.");
    }

    private static void GeneratedCombatNpcThunderAffectConsumesTimerAndExpiresWithScoreOnly()
    {
        var hub = NewHub(allEmptySkills: true);
        var affect = CreateAffectSnapshot(type: 22, value: 100, level: 1, time: 2);
        var npcId = hub.EnterNpc(
            CreateMob("COMBAT_THUNDER", hp: 1_000, clan: 1, emptySkills: true),
            10,
            10,
            affect,
            requestedConnectionId: 20_002,
            generateIndex: 42);
        Check(npcId == 20_002 &&
              hub.SetNpcCombatState(npcId, LegacyNpcMode.Combat, 0, new int[LegacyMobAiPlanner.EnemyCapacity]),
            "The generated Combat Thunder NPC fixture could not be registered without a target.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        for (var tickIndex = 0; tickIndex < 15; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0,
                "The source Type 22 mob path must not synthesize a lightning attack for an NPC.");
        }

        Check(outcomes.Count == 0 && hub.TryGetNpcSnapshot(npcId, out var afterFirstTick) && afterFirstTick is not null &&
              LegacyMobCombatState.Read(afterFirstTick.MobSnapshot).CurrentScore.Hp == 1_000 &&
              BinaryPrimitives.ReadUInt32LittleEndian(afterFirstTick.AffectSnapshot.AsSpan(4)) == 1,
            "The first Combat Type 22 opportunity must consume its timer without changing HP or relaying a frame.");

        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.ShardCount * 4; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0,
                "The generated Combat Type 22 NPC must remain non-attacking when its affect expires.");
        }

        Check(outcomes.Count == 1 && hub.TryGetNpcSnapshot(npcId, out var afterExpiry) && afterExpiry is not null &&
              LegacyMobCombatState.Read(afterExpiry.MobSnapshot).CurrentScore.Hp == 1_000 &&
              afterExpiry.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              outcomes[0].AffectTick is { Hp: 1_000, Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false },
            "An expired Combat Type 22 affect must clear all fields and refresh score without sending an HP update.");
    }

    private static void NonGeneratedPeaceNpcThunderAffectConsumesTimerAndExpiresWithScoreOnly()
    {
        var hub = NewHub(allEmptySkills: true);
        var affect = CreateAffectSnapshot(type: 22, value: 100, level: 1, time: 2);
        var npcId = hub.EnterNpc(
            CreateMob("STATIC_THUNDER", hp: 1_000, clan: 1, emptySkills: true),
            10,
            10,
            affect,
            requestedConnectionId: 20_002);
        Check(npcId == 20_002 && hub.TryGetNpcSnapshot(npcId, out var initialNpc) &&
              initialNpc is { GenerateIndex: -1 },
            "The non-generated Peace Thunder NPC fixture could not be registered.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.PeaceShardCount; tickIndex++)
            runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();

        Check(hub.TryGetNpcSnapshot(npcId, out var afterFirstTick) && afterFirstTick is not null &&
              LegacyMobCombatState.Read(afterFirstTick.MobSnapshot).CurrentScore.Hp == 1_000 &&
              BinaryPrimitives.ReadUInt32LittleEndian(afterFirstTick.AffectSnapshot.AsSpan(4)) == 1 &&
              outcomes.Count == 0,
            "The non-generated Peace Type 22 affect must decrement without changing HP or relaying before expiry.");

        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.PeaceShardCount; tickIndex++)
            runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();

        Check(hub.TryGetNpcSnapshot(npcId, out var afterExpiry) && afterExpiry is not null &&
              LegacyMobCombatState.Read(afterExpiry.MobSnapshot).CurrentScore.Hp == 1_000 &&
              afterExpiry.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              outcomes.Count == 1 &&
              outcomes[0].AffectTick is { Hp: 1_000, Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false },
            "An expired non-generated Type 22 affect must clear all fields and refresh score without HP or attack frames.");
    }

    private static void NonGeneratedCombatNpcThunderAffectConsumesTimerAndExpiresWithScoreOnly()
    {
        var hub = NewHub(allEmptySkills: true);
        var npcId = hub.EnterNpc(
            CreateMob("STATIC_CMB", hp: 1_000, clan: 1, emptySkills: true),
            10,
            10,
            CreateAffectSnapshot(type: 22, value: 100, level: 1, time: 2),
            requestedConnectionId: 20_002);
        Check(npcId == 20_002 && hub.SetNpcCombatState(
                npcId, LegacyNpcMode.Combat, 0, new int[LegacyMobAiPlanner.EnemyCapacity]),
            "The non-generated Combat Thunder NPC fixture could not be registered.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.ShardCount * 4; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0,
                "The non-generated Type 22 mob path must not synthesize a lightning attack.");
        }

        Check(hub.TryGetNpcSnapshot(npcId, out var afterFirstTick) && afterFirstTick is not null &&
              LegacyMobCombatState.Read(afterFirstTick.MobSnapshot).CurrentScore.Hp == 1_000 &&
              BinaryPrimitives.ReadUInt32LittleEndian(afterFirstTick.AffectSnapshot.AsSpan(4)) == 1 &&
              outcomes.Count == 0,
            "The fourth non-generated Combat affect opportunity must decrement without changing HP or relaying.");

        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.ShardCount * 4; tickIndex++)
            runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();

        Check(hub.TryGetNpcSnapshot(npcId, out var afterExpiry) && afterExpiry is not null &&
              LegacyMobCombatState.Read(afterExpiry.MobSnapshot).CurrentScore.Hp == 1_000 &&
              afterExpiry.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              outcomes.Count == 1 &&
              outcomes[0].AffectTick is { Hp: 1_000, Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false },
            "Non-generated Combat Type 22 expiry must clear all fields and refresh score only.");
    }

    private static void NonGeneratedPeaceNpcBestialAuraConsumesTimerAndExpiresWithScoreOnly()
    {
        var hub = NewHub(allEmptySkills: true);
        var npcId = hub.EnterNpc(
            CreateMob("STATIC_AURA", hp: 1_000, clan: 1, emptySkills: true),
            10,
            10,
            CreateAffectSnapshot(type: 23, value: 100, level: 1, time: 2),
            requestedConnectionId: 20_002);
        Check(npcId == 20_002 && hub.TryGetNpcSnapshot(npcId, out var initialNpc) &&
              initialNpc is { GenerateIndex: -1 },
            "The non-generated Peace Bestial Aura NPC fixture could not be registered.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.PeaceShardCount; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0,
                "The non-generated Peace Type 23 path must not synthesize an area attack.");
        }

        Check(hub.TryGetNpcSnapshot(npcId, out var afterFirstTick) && afterFirstTick is not null &&
              LegacyMobCombatState.Read(afterFirstTick.MobSnapshot).CurrentScore.Hp == 1_000 &&
              BinaryPrimitives.ReadUInt32LittleEndian(afterFirstTick.AffectSnapshot.AsSpan(4)) == 1 &&
              outcomes.Count == 0,
            "The non-generated Peace Type 23 affect must decrement without changing HP or relaying before expiry.");

        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.PeaceShardCount; tickIndex++)
            runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();

        Check(hub.TryGetNpcSnapshot(npcId, out var afterExpiry) && afterExpiry is not null &&
              LegacyMobCombatState.Read(afterExpiry.MobSnapshot).CurrentScore.Hp == 1_000 &&
              afterExpiry.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              outcomes.Count == 1 &&
              outcomes[0].AffectTick is { Hp: 1_000, Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false },
            "An expired non-generated Peace Type 23 affect must clear all fields and refresh score only.");
    }

    private static void NonGeneratedCombatNpcBestialAuraConsumesTimerAndExpiresWithScoreOnly()
    {
        var hub = NewHub(allEmptySkills: true);
        var npcId = hub.EnterNpc(
            CreateMob("STATIC_AURA_CMB", hp: 1_000, clan: 1, emptySkills: true),
            10,
            10,
            CreateAffectSnapshot(type: 23, value: 100, level: 1, time: 2),
            requestedConnectionId: 20_002);
        Check(npcId == 20_002 && hub.SetNpcCombatState(
                npcId, LegacyNpcMode.Combat, 0, new int[LegacyMobAiPlanner.EnemyCapacity]),
            "The non-generated Combat Bestial Aura NPC fixture could not be registered.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.ShardCount * 4; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0,
                "The non-generated Combat Type 23 path must not synthesize an area attack.");
        }

        Check(hub.TryGetNpcSnapshot(npcId, out var afterFirstTick) && afterFirstTick is not null &&
              LegacyMobCombatState.Read(afterFirstTick.MobSnapshot).CurrentScore.Hp == 1_000 &&
              BinaryPrimitives.ReadUInt32LittleEndian(afterFirstTick.AffectSnapshot.AsSpan(4)) == 1 &&
              outcomes.Count == 0,
            "The fourth non-generated Combat Type 23 opportunity must decrement without changing HP or relaying.");

        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.ShardCount * 4; tickIndex++)
            runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();

        Check(hub.TryGetNpcSnapshot(npcId, out var afterExpiry) && afterExpiry is not null &&
              LegacyMobCombatState.Read(afterExpiry.MobSnapshot).CurrentScore.Hp == 1_000 &&
              afterExpiry.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              outcomes.Count == 1 &&
              outcomes[0].AffectTick is { Hp: 1_000, Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false },
            "An expired non-generated Combat Type 23 affect must clear all fields and refresh score only.");
    }

    private static void NonGeneratedPeaceNpcTypeThirtyTwoConsumesTimerAndExpiresWithScoreOnly()
    {
        var hub = NewHub(allEmptySkills: true);
        var npcId = hub.EnterNpc(
            CreateMob("STATIC_CANCEL", hp: 1_000, clan: 1, emptySkills: true),
            10,
            10,
            CreateAffectSnapshot(type: 32, value: 1, level: 1, time: 2),
            requestedConnectionId: 20_002);
        Check(npcId == 20_002 && hub.TryGetNpcSnapshot(npcId, out var initialNpc) &&
              initialNpc is { GenerateIndex: -1 },
            "The non-generated Peace Type 32 NPC fixture could not be registered.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.PeaceShardCount; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0,
                "The non-generated Peace Type 32 timer must not synthesize an attack.");
        }

        Check(hub.TryGetNpcSnapshot(npcId, out var afterFirstTick) && afterFirstTick is not null &&
              LegacyMobCombatState.Read(afterFirstTick.MobSnapshot).CurrentScore.Hp == 1_000 &&
              BinaryPrimitives.ReadUInt32LittleEndian(afterFirstTick.AffectSnapshot.AsSpan(4)) == 1 &&
              outcomes.Count == 0,
            "The non-generated Peace Type 32 affect must decrement without changing HP or relaying before expiry.");

        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.PeaceShardCount; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0,
                "The non-generated Peace Type 32 expiry must not synthesize an attack.");
        }

        Check(hub.TryGetNpcSnapshot(npcId, out var afterExpiry) && afterExpiry is not null &&
              LegacyMobCombatState.Read(afterExpiry.MobSnapshot).CurrentScore.Hp == 1_000 &&
              afterExpiry.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              outcomes.Count == 1 &&
              outcomes[0].AffectTick is { Hp: 1_000, Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false },
            "An expired non-generated Peace Type 32 affect must clear all fields and refresh score only.");
    }

    private static void NonGeneratedCombatNpcTypeThirtyTwoConsumesTimerWithoutATarget()
    {
        var hub = NewHub(allEmptySkills: true);
        var npcId = hub.EnterNpc(
            CreateMob("ST32_CMB", hp: 1_000, clan: 1, emptySkills: true),
            10,
            10,
            CreateAffectSnapshot(type: 32, value: 1, level: 1, time: 2),
            requestedConnectionId: 20_002);
        Check(npcId == 20_002 && hub.SetNpcCombatState(
                npcId, LegacyNpcMode.Combat, 0, new int[LegacyMobAiPlanner.EnemyCapacity]),
            "The non-generated Combat Type 32 NPC fixture could not be registered without a target.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.ShardCount * 4; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0,
                "A targetless non-generated Combat Type 32 NPC must not synthesize an attack.");
        }

        Check(hub.TryGetNpcSnapshot(npcId, out var afterFirstTick) && afterFirstTick is not null &&
              LegacyMobCombatState.Read(afterFirstTick.MobSnapshot).CurrentScore.Hp == 1_000 &&
              BinaryPrimitives.ReadUInt32LittleEndian(afterFirstTick.AffectSnapshot.AsSpan(4)) == 1 &&
              outcomes.Count == 0,
            "The fourth non-generated Combat Type 32 opportunity must decrement without changing HP or relaying.");

        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.ShardCount * 4; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0,
                "A targetless non-generated Combat Type 32 expiry must not synthesize an attack.");
        }

        Check(hub.TryGetNpcSnapshot(npcId, out var afterExpiry) && afterExpiry is not null &&
              LegacyMobCombatState.Read(afterExpiry.MobSnapshot).CurrentScore.Hp == 1_000 &&
              afterExpiry.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              outcomes.Count == 1 &&
              outcomes[0].AffectTick is { Hp: 1_000, Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false },
            "Non-generated Combat Type 32 expiry must clear all fields and refresh score only.");
    }

    private static void NonGeneratedPeaceNpcLifeAuraHealsAndConsumesItsTimer()
    {
        var hub = NewHub(allEmptySkills: true);
        var npcId = hub.EnterNpc(
            CreateMob("STATIC_LIFE", hp: 800, clan: 1, emptySkills: true),
            10,
            10,
            CreateAffectSnapshot(type: 17, value: 7, level: 500, time: 2),
            requestedConnectionId: 20_002);
        Check(npcId == 20_002 && hub.TryGetNpcSnapshot(npcId, out var initialNpc) &&
              initialNpc is { GenerateIndex: -1 },
            "The non-generated Peace Life Aura NPC fixture could not be registered.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        LegacyMobAiTickResult? firstTick = null;
        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.PeaceShardCount; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            if (tick.VisitedPeaceNpcIds.Contains(npcId))
            {
                firstTick = tick;
                break;
            }
        }

        Check(firstTick is not null && hub.TryGetNpcSnapshot(npcId, out var afterHeal) && afterHeal is not null &&
              LegacyMobCombatState.Read(afterHeal.MobSnapshot).CurrentScore.Hp == 1_000 &&
              BinaryPrimitives.ReadUInt32LittleEndian(afterHeal.AffectSnapshot.AsSpan(4)) == 1,
            "The non-generated Peace Type 17 timer must apply the source heal, cap HP, and decrement once.");
        Check(outcomes.Count == 1 && outcomes[0].AffectTick is
              { Hp: 1_000, Damage: 200, ScoreRefreshRequired: true, HpUpdateRequired: true },
            "Peace Life Aura must preserve the capped HP, actual delta, and source HP/score relay flags.");

        LegacyMobAiTickResult? expiryTick = null;
        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.PeaceShardCount; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            if (tick.VisitedPeaceNpcIds.Contains(npcId))
            {
                expiryTick = tick;
                break;
            }
        }

        Check(expiryTick is not null && hub.TryGetNpcSnapshot(npcId, out var afterExpiry) && afterExpiry is not null &&
              LegacyMobCombatState.Read(afterExpiry.MobSnapshot).CurrentScore.Hp == 1_000 &&
              afterExpiry.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) && outcomes.Count == 2 &&
              outcomes[1].AffectTick is
                  { Hp: 1_000, Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: true },
            "Peace Type 17 expiry must clear all fields, preserve capped HP, and keep source relay ordering flags.");
    }

    private static void NonGeneratedCombatNpcLifeAuraHealsOnFourthOpportunityWithoutATarget()
    {
        var hub = NewHub(allEmptySkills: true);
        var npcId = hub.EnterNpc(
            CreateMob("STATIC_LIFE_CMB", hp: 500, clan: 1, emptySkills: true),
            10,
            10,
            CreateAffectSnapshot(type: 17, value: 7, level: 100, time: 2),
            requestedConnectionId: 20_002);
        Check(npcId == 20_002 && hub.SetNpcCombatState(
                npcId, LegacyNpcMode.Combat, 0, new int[LegacyMobAiPlanner.EnemyCapacity]),
            "The non-generated Combat Life Aura NPC fixture could not be registered without a target.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.ShardCount * 4; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0, "A non-generated Combat Life Aura NPC without a target must not attack.");
        }

        Check(hub.TryGetNpcSnapshot(npcId, out var afterHeal) && afterHeal is not null &&
              LegacyMobCombatState.Read(afterHeal.MobSnapshot).CurrentScore.Hp == 557 &&
              BinaryPrimitives.ReadUInt32LittleEndian(afterHeal.AffectSnapshot.AsSpan(4)) == 1 &&
              outcomes.Count == 1 && outcomes[0].AffectTick is
                  { Hp: 557, Damage: 57, ScoreRefreshRequired: true, HpUpdateRequired: true },
            "Combat Type 17 must heal by Level/2 + Value and decrement on its fourth assigned opportunity.");

        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.ShardCount * 4; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0, "Life Aura expiry without a combat target must not synthesize an attack.");
        }

        Check(hub.TryGetNpcSnapshot(npcId, out var afterExpiry) && afterExpiry is not null &&
              LegacyMobCombatState.Read(afterExpiry.MobSnapshot).CurrentScore.Hp == 614 &&
              afterExpiry.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) && outcomes.Count == 2 &&
              outcomes[1].AffectTick is
                  { Hp: 614, Damage: 57, ScoreRefreshRequired: true, HpUpdateRequired: true },
            "Combat Type 17 expiry must apply its final heal and clear the full affect record.");
    }

    private static void NonGeneratedPeaceNpcPoisonUsesLegacyDamageCapAndExpires()
    {
        var hub = NewHub(allEmptySkills: true);
        var mob = CreateMob("STATIC_POISON", hp: 10_000, clan: 2, emptySkills: true);
        BinaryPrimitives.WriteInt32LittleEndian(
            mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), 40_000);
        var npcId = hub.EnterNpc(
            mob,
            10,
            10,
            CreateAffectSnapshot(type: 20, value: 10, level: 1, time: 2),
            requestedConnectionId: 20_002);
        Check(npcId == 20_002 && hub.TryGetNpcSnapshot(npcId, out var initialNpc) &&
              initialNpc is { GenerateIndex: -1 },
            "The non-generated Peace Poison NPC fixture could not be registered.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        LegacyMobAiTickResult? firstTick = null;
        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.PeaceShardCount; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            if (tick.VisitedPeaceNpcIds.Contains(npcId))
            {
                firstTick = tick;
                break;
            }
        }

        Check(firstTick is not null && hub.TryGetNpcSnapshot(npcId, out var afterPoison) && afterPoison is not null &&
              LegacyMobCombatState.Read(afterPoison.MobSnapshot).CurrentScore.Hp == 9_400 &&
              BinaryPrimitives.ReadUInt32LittleEndian(afterPoison.AffectSnapshot.AsSpan(4)) == 1,
            "Peace Type 20 must apply the LearnedSkill-derived 600 HP cap and decrement its timer once.");
        Check(outcomes.Count == 1 && outcomes[0].AffectTick is
              { Hp: 9_400, Damage: -600, ScoreRefreshRequired: true, HpUpdateRequired: true },
            "Peace Poison must preserve its signed HP delta and request HP then score relays.");

        LegacyMobAiTickResult? expiryTick = null;
        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.PeaceShardCount; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            if (tick.VisitedPeaceNpcIds.Contains(npcId))
            {
                expiryTick = tick;
                break;
            }
        }

        Check(expiryTick is not null && hub.TryGetNpcSnapshot(npcId, out var afterExpiry) && afterExpiry is not null &&
              LegacyMobCombatState.Read(afterExpiry.MobSnapshot).CurrentScore.Hp == 8_800 &&
              afterExpiry.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) && outcomes.Count == 2 &&
              outcomes[1].AffectTick is
                  { Hp: 8_800, Damage: -600, ScoreRefreshRequired: true, HpUpdateRequired: true },
            "Peace Poison expiry must apply its final damage, clear all affect bytes, and request both relays.");
    }

    private static void NonGeneratedCombatNpcPoisonRunsEveryFourthVisitWithoutATarget()
    {
        var hub = NewHub(allEmptySkills: true);
        var mob = CreateMob("STPOISON_CMB", hp: 10_000, clan: 2, emptySkills: true);
        BinaryPrimitives.WriteInt32LittleEndian(
            mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), 0);
        var npcId = hub.EnterNpc(
            mob,
            10,
            10,
            CreateAffectSnapshot(type: 20, value: 10, level: 1, time: 2),
            requestedConnectionId: 20_002);
        Check(npcId == 20_002 && hub.SetNpcCombatState(
                npcId, LegacyNpcMode.Combat, 0, new int[LegacyMobAiPlanner.EnemyCapacity]),
            "The non-generated Combat Poison NPC fixture could not be registered without a target.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.ShardCount * 3; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0, "A targetless Combat Poison NPC must not synthesize attacks.");
        }

        Check(outcomes.Count == 0 && hub.TryGetNpcSnapshot(npcId, out var beforeFirstPoison) &&
              beforeFirstPoison is not null &&
              LegacyMobCombatState.Read(beforeFirstPoison.MobSnapshot).CurrentScore.Hp == 10_000 &&
              BinaryPrimitives.ReadUInt32LittleEndian(beforeFirstPoison.AffectSnapshot.AsSpan(4)) == 2,
            "Combat Poison must wait through the first three assigned opportunities.");

        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.ShardCount; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0, "A targetless Combat Poison NPC must remain non-attacking on its timer visit.");
        }

        Check(outcomes.Count == 1 && hub.TryGetNpcSnapshot(npcId, out var afterFirstPoison) && afterFirstPoison is not null &&
              LegacyMobCombatState.Read(afterFirstPoison.MobSnapshot).CurrentScore.Hp == 9_000 &&
              BinaryPrimitives.ReadUInt32LittleEndian(afterFirstPoison.AffectSnapshot.AsSpan(4)) == 1 &&
              outcomes[0].AffectTick is
                  { Hp: 9_000, Damage: -1_000, ScoreRefreshRequired: true, HpUpdateRequired: true },
            "Combat Type 20 must apply the NPC damage cap and consume its timer on the fourth opportunity.");

        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.ShardCount * 4; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0, "Poison expiry without a target must not synthesize a Combat attack.");
        }

        Check(outcomes.Count == 2 && hub.TryGetNpcSnapshot(npcId, out var afterExpiry) && afterExpiry is not null &&
              LegacyMobCombatState.Read(afterExpiry.MobSnapshot).CurrentScore.Hp == 8_000 &&
              afterExpiry.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              outcomes[1].AffectTick is
                  { Hp: 8_000, Damage: -1_000, ScoreRefreshRequired: true, HpUpdateRequired: true },
            "Combat Poison expiry must apply its final damage and clear the eight-byte affect.");
    }

    private static void NonGeneratedPeaceNpcPoisonClampsToOneAndRefreshesOnExpiry()
    {
        var hub = NewHub(allEmptySkills: true);
        var mob = CreateMob("STPOISON_FLOOR", hp: 1, clan: 2, emptySkills: true);
        BinaryPrimitives.WriteInt32LittleEndian(
            mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), 0);
        var npcId = hub.EnterNpc(
            mob,
            10,
            10,
            CreateAffectSnapshot(type: 20, value: 10, level: 1, time: 2),
            requestedConnectionId: 20_002);
        Check(npcId == 20_002, "The non-generated Poison HP-floor fixture could not be registered.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        for (var timerVisit = 0; timerVisit < 2; timerVisit++)
        {
            LegacyMobAiTickResult? visitedTick = null;
            for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.PeaceShardCount; tickIndex++)
            {
                var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
                if (tick.VisitedPeaceNpcIds.Contains(npcId))
                {
                    visitedTick = tick;
                    break;
                }
            }

            Check(visitedTick is not null && hub.TryGetNpcSnapshot(npcId, out var currentNpc) && currentNpc is not null &&
                  LegacyMobCombatState.Read(currentNpc.MobSnapshot).CurrentScore.Hp == 1,
                "Non-generated NPC Poison must preserve its positive HP floor.");
            var affectTick = outcomes[timerVisit].AffectTick;
            Check(affectTick is { Hp: 1, Damage: 0, HpUpdateRequired: true } poisonTick &&
                  poisonTick.ScoreRefreshRequired == (timerVisit == 1),
                "Poison must relay zero HP delta on the floor and refresh score only when its affect expires.");
        }

        Check(hub.TryGetNpcSnapshot(npcId, out var afterExpiry) && afterExpiry is not null &&
              afterExpiry.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) && outcomes.Count == 2,
            "The HP-floor Poison expiry must clear all affect fields after its final zero-delta HP update.");
    }

    private static void GeneratedCombatNpcBestialAuraConsumesTimerAndExpiresWithScoreOnly()
    {
        var hub = NewHub(allEmptySkills: true);
        var affect = CreateAffectSnapshot(type: 23, value: 100, level: 1, time: 2);
        var npcId = hub.EnterNpc(
            CreateMob("COMBAT_AURA", hp: 1_000, clan: 1, emptySkills: true),
            10,
            10,
            affect,
            requestedConnectionId: 20_002,
            generateIndex: 42);
        Check(npcId == 20_002 &&
              hub.SetNpcCombatState(npcId, LegacyNpcMode.Combat, 0, new int[LegacyMobAiPlanner.EnemyCapacity]),
            "The generated Combat Bestial Aura NPC fixture could not be registered without a target.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        for (var tickIndex = 0; tickIndex < 15; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0, "Type 23 on a generated Combat NPC must not synthesize an area attack.");
        }

        Check(outcomes.Count == 0 && hub.TryGetNpcSnapshot(npcId, out var afterFirstTick) && afterFirstTick is not null &&
              LegacyMobCombatState.Read(afterFirstTick.MobSnapshot).CurrentScore.Hp == 1_000 &&
              BinaryPrimitives.ReadUInt32LittleEndian(afterFirstTick.AffectSnapshot.AsSpan(4)) == 1,
            "The first Combat Type 23 timer visit must consume time without changing HP or relaying a frame.");

        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.ShardCount * 4; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0, "A generated Combat Type 23 NPC must remain non-attacking through expiry.");
        }

        Check(outcomes.Count == 1 && hub.TryGetNpcSnapshot(npcId, out var afterExpiry) && afterExpiry is not null &&
              LegacyMobCombatState.Read(afterExpiry.MobSnapshot).CurrentScore.Hp == 1_000 &&
              afterExpiry.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              outcomes[0].AffectTick is { Hp: 1_000, Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false },
            "Combat Type 23 expiry must clear the affect and refresh score only, without an HP update.");
    }

    private static void GeneratedPeaceNpcTypeThirtyTwoConsumesTimerAndExpiresWithScoreOnly()
    {
        var hub = NewHub(allEmptySkills: true);
        var npcId = hub.EnterNpc(
            CreateMob("PEACE_TYPE32", hp: 1_000, clan: 2, emptySkills: true),
            10,
            10,
            CreateAffectSnapshot(type: 32, value: 1, level: 1, time: 2),
            requestedConnectionId: 20_002,
            generateIndex: 42);
        Check(npcId == 20_002, "The generated Peace Type 32 NPC fixture could not be registered.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        for (var callback = 0; callback < LegacyMobAiRuntime.PeaceShardCount; callback++)
            runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();

        Check(hub.TryGetNpcSnapshot(npcId, out var afterFirstTick) && afterFirstTick is not null &&
              BinaryPrimitives.ReadUInt32LittleEndian(afterFirstTick.AffectSnapshot.AsSpan(4)) == 1 &&
              outcomes.All(static outcome => outcome.AffectTick is null),
            "The first Peace Type 32 timer visit must decrement finite time without relaying an unchanged score.");

        for (var callback = 0; callback < LegacyMobAiRuntime.PeaceShardCount; callback++)
            runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();

        Check(outcomes.Count(static outcome => outcome.AffectTick is not null) == 1 &&
              hub.TryGetNpcSnapshot(npcId, out var afterExpiry) && afterExpiry is not null &&
              LegacyMobCombatState.Read(afterExpiry.MobSnapshot).CurrentScore.Hp == 1_000 &&
              afterExpiry.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              outcomes.Single(static outcome => outcome.AffectTick is not null).AffectTick is
                  { Hp: 1_000, Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false },
            "Peace Type 32 expiry must clear all affect fields and refresh score without an HP update.");
    }

    private static void GeneratedCombatNpcTypeThirtyTwoConsumesTimerWithoutATarget()
    {
        var hub = NewHub(allEmptySkills: true);
        var npcId = hub.EnterNpc(
            CreateMob("COMBAT_TYPE32", hp: 1_000, clan: 2, emptySkills: true),
            10,
            10,
            CreateAffectSnapshot(type: 32, value: 1, level: 1, time: 2),
            requestedConnectionId: 20_002,
            generateIndex: 42);
        Check(npcId == 20_002 &&
              hub.SetNpcCombatState(npcId, LegacyNpcMode.Combat, 0, new int[LegacyMobAiPlanner.EnemyCapacity]),
            "The generated Combat Type 32 NPC fixture could not be registered without a target.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        for (var tickIndex = 0; tickIndex < 15; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0, "A targetless generated Combat Type 32 NPC must not synthesize an attack.");
        }

        Check(outcomes.Count == 0 && hub.TryGetNpcSnapshot(npcId, out var afterFirstTick) && afterFirstTick is not null &&
              BinaryPrimitives.ReadUInt32LittleEndian(afterFirstTick.AffectSnapshot.AsSpan(4)) == 1,
            "Combat Type 32 must decrement on its first fourth-visit timer opportunity without relaying a score.");

        for (var tickIndex = 0; tickIndex < 16; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0, "A targetless generated Combat Type 32 NPC must remain non-attacking.");
        }

        Check(outcomes.Count == 1 && hub.TryGetNpcSnapshot(npcId, out var afterExpiry) && afterExpiry is not null &&
              afterExpiry.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              outcomes[0].AffectTick is { Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false },
            "Combat Type 32 expiry must clear the affect and refresh score without changing HP.");
    }

    private static void PeaceRouteTypeFiveSummonTypeThirtyTwoConsumesItsTimer()
    {
        var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
        AddSummonOwner(hub, 1, "TYPE32_LEADER", 1_990, 2_000, 0, static (_, _) => ValueTask.CompletedTask);
        AddSummonOwner(hub, 2, "TYPE32_OWNER", 2_000, 2_000, 30, static (_, _) => ValueTask.CompletedTask);
        Check(hub.TrySetParty(1, [1, 2]), "The peaceful Type 32 summon fixture could not form its party.");

        var summonSkill = new LegacySkillDefinition(50, 20, 0, 20, 0, 0, 11, 1, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "TYPE32_SUMMON");
        Check(hub.TryApplySkillAttack(2, 2, summonSkill, itemData: null, weather: 0, randomFactor: 90, out var summonOutcome) ==
              LegacySkillAttackResult.Accepted && summonOutcome?.SummonedMobs is { Count: 1 },
            "The peaceful Type 32 fixture could not create its summon.");

        var summon = summonOutcome!.SummonedMobs!.Single();
        SeedSummonTypeThirtyTwo(summon, time: 2);

        _ = hub.TryApplyRouteTypeFiveSummonPeaceStep(summon.ConnectionId, static () => 0, out var firstTick);
        Check(firstTick.AffectTick is null,
            "The first Peace Type 32 visit must decrement time without relaying an unchanged score.");
        Check(hub.TryApplyRouteTypeFiveSummonPeaceStep(summon.ConnectionId, static () => 0, out var expiry) &&
              expiry.AffectTick is { Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false } tick &&
              tick.AffectSnapshot.AsSpan(8, 8).SequenceEqual(new byte[8]),
            "Peace Type 32 expiry must clear all affect fields and request only a score refresh.");
    }

    private static void CombatSummonTypeThirtyTwoConsumesItsTimerWithoutSkippingAttack()
    {
        var fixture = CreateCombatRouteTypeFiveSummonFixture(summonerOffset: 18);
        SeedSummonTypeThirtyTwo(fixture.Summon, time: 2);

        for (var opportunity = 0; opportunity < 4; opportunity++)
        {
            Check(fixture.Hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var outcomes),
                "The first Combat Type 32 summon timer opportunity could not be processed.");
            Check(outcomes.All(static outcome => outcome.AffectUpdate?.AffectTick is null),
                "The first Combat Type 32 timer visit must not relay an unchanged score.");
        }

        LegacyGeneratedNpcAffectTickOutcome? expiryTick = null;
        var expiryAttacks = new List<LegacySummonAttackOutcome>();
        for (var opportunity = 0; opportunity < 4; opportunity++)
        {
            Check(fixture.Hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var outcomes),
                "The Combat Type 32 summon expiry opportunity could not be processed.");
            expiryTick ??= outcomes.Select(static outcome => outcome.AffectUpdate?.AffectTick)
                .FirstOrDefault(static tick => tick is not null);
            expiryAttacks.AddRange(outcomes.Where(static outcome => outcome.Attack is not null)
                .Select(static outcome => outcome.Attack!));
        }

        Check(expiryTick is { Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false } tick &&
              tick.AffectSnapshot.AsSpan(8, 8).SequenceEqual(new byte[8]) &&
              expiryAttacks.Any(attack => attack.TargetConnectionId == fixture.TargetConnectionId),
            "Combat Type 32 expiry must refresh score and preserve the summon attack opportunity.");
    }

    private static void ExtendedSkillBleedingTypeFortySevenUsesGenericNpcAndSummonTimerTail()
    {
        var generated = NewHub(allEmptySkills: true);
        var generatedNpcId = generated.EnterNpc(
            CreateMob("BLEED_GEN", hp: 1_000, clan: 2, emptySkills: true),
            10,
            10,
            CreateAffectSnapshot(type: 47, value: 100, level: 10, time: 2),
            requestedConnectionId: 20_002,
            generateIndex: 42);
        Check(generatedNpcId == 20_002 &&
              !generated.TryProcessGeneratedNpcTimerAffects(generatedNpcId, out _) &&
              generated.TryGetNpcSnapshot(generatedNpcId, out var generatedAfterCountdown) &&
              generatedAfterCountdown is not null &&
              BinaryPrimitives.ReadUInt32LittleEndian(generatedAfterCountdown.AffectSnapshot.AsSpan(4)) == 1,
            "The generated-NPC timer must decrement the extended Bleeding Type 47 affect without relaying early.");
        Check(generated.TryProcessGeneratedNpcTimerAffects(generatedNpcId, out var generatedExpiry) &&
              generatedExpiry.AffectTick is { Hp: 1_000, Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false } generatedTick &&
              generatedTick.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]),
            "Generated-NPC Type 47 expiry must clear the full affect and refresh score without changing HP.");

        var ordinary = NewHub(allEmptySkills: true);
        var ordinaryNpcId = ordinary.EnterNpc(
            CreateMob("BLEED_NPC", hp: 1_000, clan: 1, emptySkills: true),
            10,
            10,
            CreateAffectSnapshot(type: 47, value: 100, level: 10, time: 2),
            requestedConnectionId: 20_002);
        Check(ordinaryNpcId == 20_002 &&
              !ordinary.TryProcessNonGeneratedNpcTimerAffects(ordinaryNpcId, out _) &&
              ordinary.TryGetNpcSnapshot(ordinaryNpcId, out var ordinaryAfterCountdown) &&
              ordinaryAfterCountdown is not null &&
              BinaryPrimitives.ReadUInt32LittleEndian(ordinaryAfterCountdown.AffectSnapshot.AsSpan(4)) == 1,
            "The non-generated-NPC timer must decrement the extended Bleeding Type 47 affect without relaying early.");
        Check(ordinary.TryProcessNonGeneratedNpcTimerAffects(ordinaryNpcId, out var ordinaryExpiry) &&
              ordinaryExpiry.AffectTick is { Hp: 1_000, Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false } ordinaryTick &&
              ordinaryTick.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]),
            "Non-generated-NPC Type 47 expiry must clear the full affect and refresh score without changing HP.");

        var summonHub = new WorldHub(summonCatalog: LoadSummonCatalog());
        AddSummonOwner(summonHub, 1, "BLEEDING_LEADER", 1_990, 2_000, 0, static (_, _) => ValueTask.CompletedTask);
        AddSummonOwner(summonHub, 2, "BLEEDING_CASTER", 2_000, 2_000, 30, static (_, _) => ValueTask.CompletedTask);
        Check(summonHub.TrySetParty(1, [1, 2]), "The Bleeding Type 47 summon fixture could not form its party.");
        var summonSkill = new LegacySkillDefinition(50, 20, 0, 20, 0, 0, 11, 1, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "TYPE47_SUMMON");
        Check(summonHub.TryApplySkillAttack(2, 2, summonSkill, itemData: null, weather: 0, randomFactor: 90, out var summonOutcome) ==
              LegacySkillAttackResult.Accepted && summonOutcome?.SummonedMobs is { Count: 1 },
            "The Bleeding Type 47 summon fixture could not create its summon.");
        var summon = summonOutcome!.SummonedMobs!.Single();
        summon.AffectSnapshot[8] = 47;
        summon.AffectSnapshot[9] = 100;
        BinaryPrimitives.WriteUInt16LittleEndian(summon.AffectSnapshot.AsSpan(10), 10);
        BinaryPrimitives.WriteUInt32LittleEndian(summon.AffectSnapshot.AsSpan(12), 2);

        _ = summonHub.TryApplyRouteTypeFiveSummonPeaceStep(summon.ConnectionId, static () => 0, out var summonCountdown);
        Check(summonCountdown.AffectTick is null,
            "The RouteType 5 summon must decrement Type 47 without relaying an unchanged score.");
        Check(summonHub.TryApplyRouteTypeFiveSummonPeaceStep(summon.ConnectionId, static () => 0, out var summonExpiry) &&
              summonExpiry.AffectTick is { Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false } summonTick &&
              summonTick.AffectSnapshot.AsSpan(8, 8).SequenceEqual(new byte[8]),
            "RouteType 5 summon Type 47 expiry must clear the affect and refresh score without changing HP.");
    }

    private static void GeneratedPeaceNpcTypeTwentyFourExpiresWithTypeThreeRemoval()
    {
        var hub = NewHub(allEmptySkills: true);
        var npcId = hub.EnterNpc(
            CreateMob("PEACE_TYPE24", hp: 1_000, clan: 2, emptySkills: true),
            10,
            10,
            CreateAffectSnapshot(type: 24, value: 0, level: 0, time: 2),
            requestedConnectionId: 20_002,
            generateIndex: 42);
        Check(npcId == 20_002, "The generated Peace Type 24 NPC fixture could not be registered.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.PeaceShardCount; tickIndex++)
            runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();

        Check(hub.TryGetNpcSnapshot(npcId, out var afterFirstOpportunity) && afterFirstOpportunity is not null &&
              BinaryPrimitives.ReadUInt32LittleEndian(afterFirstOpportunity.AffectSnapshot.AsSpan(4)) == 1 &&
              outcomes.All(static outcome => outcome.Removal is null),
            "A generated Peace NPC Type 24 timer must decrement once and stay registered before expiry.");

        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.PeaceShardCount; tickIndex++)
            runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();

        var peaceRemoval = outcomes.SingleOrDefault(static outcome => outcome.Removal is not null)?.Removal;
        Check(!hub.TryGetNpcSnapshot(npcId, out _) && peaceRemoval is { RemoveType: 3 } && peaceRemoval.ConnectionId == npcId,
            $"The second Peace Type 24 timer opportunity must remove a generated NPC with RemoveMob(type=3); stillRegistered={hub.TryGetNpcSnapshot(npcId, out _)}, removal={peaceRemoval?.RemoveType}:{peaceRemoval?.ConnectionId}, outcomeCount={outcomes.Count}.");
    }

    private static void GeneratedCombatNpcTypeTwentyFourExpiresWithoutATarget()
    {
        var hub = NewHub(allEmptySkills: true);
        var npcId = hub.EnterNpc(
            CreateMob("COMBAT_TYPE24", hp: 1_000, clan: 2, emptySkills: true),
            10,
            10,
            CreateAffectSnapshot(type: 24, value: 0, level: 0, time: 2),
            requestedConnectionId: 20_002,
            generateIndex: 42);
        Check(npcId == 20_002 &&
              hub.SetNpcCombatState(npcId, LegacyNpcMode.Combat, 0, new int[LegacyMobAiPlanner.EnemyCapacity]),
            "The generated Combat Type 24 NPC fixture could not be registered without a target.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        for (var tickIndex = 0; tickIndex < 15; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0, "A targetless generated Combat NPC must not attack while its Type 24 timer advances.");
        }

        Check(hub.TryGetNpcSnapshot(npcId, out var afterFirstOpportunity) && afterFirstOpportunity is not null &&
              BinaryPrimitives.ReadUInt32LittleEndian(afterFirstOpportunity.AffectSnapshot.AsSpan(4)) == 1 &&
              outcomes.All(static outcome => outcome.Removal is null),
            "The first four generated Combat Type 24 timer opportunities must decrement time without removing the NPC.");

        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.ShardCount * 4; tickIndex++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            Check(tick.Attacks.Count == 0, "A targetless generated Combat NPC must remain non-attacking through Type 24 expiry.");
        }

        var combatRemoval = outcomes.SingleOrDefault(static outcome => outcome.Removal is not null)?.Removal;
        Check(!hub.TryGetNpcSnapshot(npcId, out _) && combatRemoval is { RemoveType: 3 } && combatRemoval.ConnectionId == npcId,
            $"The next four generated Combat Type 24 timer opportunities must remove the NPC with RemoveMob(type=3); stillRegistered={hub.TryGetNpcSnapshot(npcId, out _)}, removal={combatRemoval?.RemoveType}:{combatRemoval?.ConnectionId}, outcomeCount={outcomes.Count}.");
    }

    private static void PeaceRouteTypeFiveSummonThunderConsumesItsTimer()
    {
        var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
        AddSummonOwner(hub, 1, "THUNDER_LEADER", 1_990, 2_000, 0, static (_, _) => ValueTask.CompletedTask);
        AddSummonOwner(hub, 2, "THUNDER_OWNER", 2_000, 2_000, 30, static (_, _) => ValueTask.CompletedTask);
        Check(hub.TrySetParty(1, [1, 2]), "The peaceful summon Thunder fixture could not form its party.");

        var summonSkill = new LegacySkillDefinition(50, 20, 0, 20, 0, 0, 11, 1, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "THUNDER_SUMMON");
        Check(hub.TryApplySkillAttack(2, 2, summonSkill, itemData: null, weather: 0, randomFactor: 90, out var summonOutcome) ==
              LegacySkillAttackResult.Accepted && summonOutcome?.SummonedMobs is { Count: 1 },
            "The peaceful Type 22 fixture could not create its summon.");

        var summon = summonOutcome!.SummonedMobs!.Single();
        SeedSummonThunder(summon, time: 2);

        _ = hub.TryApplyRouteTypeFiveSummonPeaceStep(summon.ConnectionId, static () => 0, out var firstTick);
        Check(firstTick.AffectTick is null,
            "The first Peace Type 22 visit must consume time without relaying an unchanged score.");
        Check(hub.TryApplyRouteTypeFiveSummonPeaceStep(summon.ConnectionId, static () => 0, out var expiry) &&
              expiry.AffectTick is { Hp: > 0, Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false } tick &&
              tick.AffectSnapshot.AsSpan(8, 8).SequenceEqual(new byte[8]),
            "Peace Type 22 expiry must clear all affect bytes and request only a score refresh.");
    }

    private static void CombatSummonThunderConsumesItsTimerWithoutSkippingAttack()
    {
        var fixture = CreateCombatRouteTypeFiveSummonFixture(summonerOffset: 18);
        SeedSummonThunder(fixture.Summon, time: 2);

        for (var opportunity = 0; opportunity < 4; opportunity++)
        {
            Check(fixture.Hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var outcomes),
                "The first Combat summon Thunder timer opportunity could not be processed.");
            Check(outcomes.All(static outcome => outcome.AffectUpdate?.AffectTick is null),
                "The first Combat Type 22 timer visit must not relay an unchanged score.");
        }

        LegacyGeneratedNpcAffectTickOutcome? expiryTick = null;
        var expiryAttacks = new List<LegacySummonAttackOutcome>();
        for (var opportunity = 0; opportunity < 4; opportunity++)
        {
            Check(fixture.Hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var outcomes),
                "The Combat summon Thunder expiry opportunity could not be processed.");
            expiryTick ??= outcomes.Select(static outcome => outcome.AffectUpdate?.AffectTick)
                .FirstOrDefault(static tick => tick is not null);
            expiryAttacks.AddRange(outcomes.Where(static outcome => outcome.Attack is not null)
                .Select(static outcome => outcome.Attack!));
        }

        Check(expiryTick is { Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false } tick &&
              tick.AffectSnapshot.AsSpan(8, 8).SequenceEqual(new byte[8]) &&
              expiryAttacks.Any(attack => attack.TargetConnectionId == fixture.TargetConnectionId),
            "Combat Type 22 expiry must refresh score only and preserve the summon attack opportunity.");
    }

    private static void PeaceRouteTypeFiveSummonBestialAuraConsumesItsTimer()
    {
        var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
        AddSummonOwner(hub, 1, "AURA_LEADER", 1_990, 2_000, 0, static (_, _) => ValueTask.CompletedTask);
        AddSummonOwner(hub, 2, "AURA_OWNER", 2_000, 2_000, 30, static (_, _) => ValueTask.CompletedTask);
        Check(hub.TrySetParty(1, [1, 2]), "The summon Bestial Aura fixture could not form its party.");

        var summonSkill = new LegacySkillDefinition(50, 20, 0, 20, 0, 0, 11, 1, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "AURA_SUMMON");
        Check(hub.TryApplySkillAttack(2, 2, summonSkill, itemData: null, weather: 0, randomFactor: 90, out var summonOutcome) ==
              LegacySkillAttackResult.Accepted && summonOutcome?.SummonedMobs is { Count: 1 },
            "The peaceful Type 23 fixture could not create its summon.");

        var summon = summonOutcome!.SummonedMobs!.Single();
        summon.AffectSnapshot[8] = 23;
        summon.AffectSnapshot[9] = 100;
        BinaryPrimitives.WriteUInt16LittleEndian(summon.AffectSnapshot.AsSpan(10), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(summon.AffectSnapshot.AsSpan(12), 2);

        _ = hub.TryApplyRouteTypeFiveSummonPeaceStep(summon.ConnectionId, static () => 0, out _);

        Check(hub.TryApplyRouteTypeFiveSummonPeaceStep(summon.ConnectionId, static () => 0, out var expiry) &&
              expiry.AffectTick is { Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false } tick &&
              tick.AffectSnapshot.AsSpan(8, 8).SequenceEqual(new byte[8]),
            "Peace Type 23 expiry must clear the affect and request a score-only refresh.");
    }

    private static void CombatSummonBestialAuraConsumesItsTimer()
    {
        var fixture = CreateCombatRouteTypeFiveSummonFixture(summonerOffset: 18);
        fixture.Summon.AffectSnapshot[8] = 23;
        fixture.Summon.AffectSnapshot[9] = 100;
        BinaryPrimitives.WriteUInt16LittleEndian(fixture.Summon.AffectSnapshot.AsSpan(10), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(fixture.Summon.AffectSnapshot.AsSpan(12), 2);

        for (var battleOpportunity = 0; battleOpportunity < 4; battleOpportunity++)
            Check(fixture.Hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out _),
                "The Combat summon timer opportunity could not be processed.");

        var expiryUpdate = (LegacyNpcAiApplicationOutcome?)null;

        for (var battleOpportunity = 0; battleOpportunity < 4; battleOpportunity++)
        {
            Check(fixture.Hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var outcomes),
                "The Combat summon expiry opportunity could not be processed.");
            expiryUpdate ??= outcomes.Select(static outcome => outcome.AffectUpdate)
                .FirstOrDefault(static update => update?.AffectTick is not null);
        }

        Check(expiryUpdate?.AffectTick is { Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false } tick &&
              tick.AffectSnapshot.AsSpan(8, 8).SequenceEqual(new byte[8]),
            "Combat Type 23 expiry must clear the affect and request a score-only refresh.");
    }

    private static void PeaceRouteTypeFiveSummonLifeAuraHealsAndExpiresWithoutSkippingMovement()
    {
        var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
        AddSummonOwner(hub, 1, "LIFEAURA_LEADER", 1_990, 2_000, 0, static (_, _) => ValueTask.CompletedTask);
        AddSummonOwner(hub, 2, "LIFEAURA_OWNER", 2_000, 2_000, 30, static (_, _) => ValueTask.CompletedTask);
        Check(hub.TrySetParty(1, [1, 2]), "The summon Life Aura fixture could not form its party.");

        var summonSkill = new LegacySkillDefinition(50, 20, 0, 20, 0, 0, 11, 1, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "LIFE_AURA_SUMMON");
        Check(hub.TryApplySkillAttack(2, 2, summonSkill, itemData: null, weather: 0, randomFactor: 90, out var summonOutcome) ==
              LegacySkillAttackResult.Accepted && summonOutcome?.SummonedMobs is { Count: 1 },
            "The peaceful Life Aura fixture could not create its summon.");

        var summon = summonOutcome!.SummonedMobs!.Single();
        Check(hub.UpdatePosition(2, checked((short)(summon.PositionX + 8)), summon.PositionY),
            "The Life Aura summoner could not be moved to the follower-step threshold.");
        SeedSummonLifeAura(summon, hp: 980, maxHp: 1_000, time: 2);

        Check(hub.TryApplyRouteTypeFiveSummonPeaceStep(summon.ConnectionId, static () => 0, out var healOutcome) &&
              healOutcome.AffectTick is { Hp: 1_000, Damage: 20, ScoreRefreshRequired: true, HpUpdateRequired: true } healTick &&
              healOutcome.Movement is { ConnectionId: var movementSummonId } && movementSummonId == summon.ConnectionId &&
              LegacyMobCombatState.Read(healTick.MobSnapshot).CurrentScore.Hp == 1_000 &&
              BinaryPrimitives.ReadUInt32LittleEndian(healTick.AffectSnapshot.AsSpan(12)) == 1,
            "Peace Type 17 must cap the heal, decrement its timer, and preserve the follower movement in the same outcome.");

        var expiryAccepted = hub.TryApplyRouteTypeFiveSummonPeaceStep(summon.ConnectionId, static () => 0, out var expiryOutcome);
        var expiryTick = expiryOutcome.AffectTick;
        Check(expiryAccepted && expiryTick is { Hp: 1_000, Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false } &&
              expiryTick.AffectSnapshot.AsSpan(8, 8).SequenceEqual(new byte[8]),
            "Peace Type 17 expiry at capped HP must clear the affect and refresh score without an HP frame.");
    }

    private static void CombatSummonLifeAuraHealsAndExpiresOnItsAffectOpportunity()
    {
        var fixture = CreateCombatRouteTypeFiveSummonFixture(summonerOffset: 18);
        SeedSummonLifeAura(fixture.Summon, hp: 980, maxHp: 1_000, time: 2);

        for (var opportunity = 0; opportunity < 3; opportunity++)
            Check(fixture.Hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out _),
                "A pre-timer Combat summon Life Aura opportunity could not be processed.");

        Check(fixture.Hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var healOutcomes),
            "The Combat summon Life Aura heal opportunity could not be processed.");
        var healTick = healOutcomes.Select(static outcome => outcome.AffectUpdate?.AffectTick)
            .FirstOrDefault(static tick => tick is not null);
        Check(healTick is { Hp: 1_000, Damage: 20, ScoreRefreshRequired: true, HpUpdateRequired: true } &&
              LegacyMobCombatState.Read(healTick.MobSnapshot).CurrentScore.Hp == 1_000 &&
              BinaryPrimitives.ReadUInt32LittleEndian(healTick.AffectSnapshot.AsSpan(12)) == 1 &&
              healOutcomes.Any(static outcome => outcome.Attack is { Damage: > 0 }),
            "Combat Type 17 must heal/cap HP and consume its timer on the fourth opportunity without skipping the summon attack.");

        for (var opportunity = 0; opportunity < 3; opportunity++)
            Check(fixture.Hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out _),
                "A pre-expiry Combat summon Life Aura opportunity could not be processed.");

        Check(fixture.Hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var expiryOutcomes),
            "The Combat summon Life Aura expiry opportunity could not be processed.");
        var expiryTick = expiryOutcomes.Select(static outcome => outcome.AffectUpdate?.AffectTick)
            .FirstOrDefault(static tick => tick is not null);
        Check(expiryTick is { Hp: 1_000, Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false } &&
              expiryTick.AffectSnapshot.AsSpan(8, 8).SequenceEqual(new byte[8]) &&
              expiryOutcomes.Any(static outcome => outcome.Attack is { Damage: > 0 }),
            "Combat Type 17 expiry at capped HP must clear the affect, refresh score, and preserve the summon attack.");
    }

    private static void PeaceRouteTypeFiveSummonPoisonDrainsHpAndPreservesFollowerMovement()
    {
        var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
        AddSummonOwner(hub, 1, "POISON_LEADER", 1_990, 2_000, 0, static (_, _) => ValueTask.CompletedTask);
        AddSummonOwner(hub, 2, "POISON_OWNER", 2_000, 2_000, 30, static (_, _) => ValueTask.CompletedTask);
        Check(hub.TrySetParty(1, [1, 2]), "The peaceful summon Poison fixture could not form its party.");

        var summonSkill = new LegacySkillDefinition(50, 20, 0, 20, 0, 0, 11, 1, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "POISON_SUMMON");
        Check(hub.TryApplySkillAttack(2, 2, summonSkill, itemData: null, weather: 0, randomFactor: 90, out var summonOutcome) ==
              LegacySkillAttackResult.Accepted && summonOutcome?.SummonedMobs is { Count: 1 },
            "The peaceful Poison fixture could not create its summon.");

        var summon = summonOutcome!.SummonedMobs!.Single();
        Check(hub.UpdatePosition(2, checked((short)(summon.PositionX + 8)), summon.PositionY),
            "The Poison summoner could not be moved to the follower-step threshold.");
        SeedSummonPoison(summon, hp: 10_000, maxHp: 10_000, learnedSkill: 0, time: 2);

        Check(hub.TryApplyRouteTypeFiveSummonPeaceStep(summon.ConnectionId, static () => 0, out var outcome) &&
              outcome.AffectTick is { Hp: 9_000, Damage: -1_000, ScoreRefreshRequired: true, HpUpdateRequired: true } tick &&
              outcome.Movement is { ConnectionId: var movementSummonId } && movementSummonId == summon.ConnectionId &&
              LegacyMobCombatState.Read(tick.MobSnapshot).CurrentScore.Hp == 9_000 &&
              BinaryPrimitives.ReadUInt32LittleEndian(tick.AffectSnapshot.AsSpan(12)) == 1,
            "Peace Type 20 must apply the 1,000 HP mob cap, consume its timer, and preserve follower movement in the same outcome.");

        Check(hub.TryApplyRouteTypeFiveSummonPeaceStep(summon.ConnectionId, static () => 0, out var expiryOutcome) &&
              expiryOutcome.AffectTick is { Hp: 8_000, Damage: -1_000, ScoreRefreshRequired: true, HpUpdateRequired: true } expiryTick &&
              expiryTick.AffectSnapshot.AsSpan(8, 8).SequenceEqual(new byte[8]),
            "Peace Type 20 must still apply its last HP tick, then clear all affect fields when finite time expires.");
    }

    private static void CombatSummonPoisonUsesLearnedSkillCapAndPreservesAttack()
    {
        var fixture = CreateCombatRouteTypeFiveSummonFixture(summonerOffset: 18);
        SeedSummonPoison(fixture.Summon, hp: 10_000, maxHp: 10_000, learnedSkill: 40_000, time: 2);

        for (var opportunity = 0; opportunity < 3; opportunity++)
            Check(fixture.Hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out _),
                "A pre-timer Combat summon Poison opportunity could not be processed.");

        Check(fixture.Hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var outcomes),
            "The Combat summon Poison opportunity could not be processed.");
        var poisonTick = outcomes.Select(static outcome => outcome.AffectUpdate?.AffectTick)
            .FirstOrDefault(static tick => tick is not null);
        Check(poisonTick is not null,
            "The fourth Combat summon opportunity did not return a Type 20 affect tick.");
        Check(poisonTick!.Hp == 9_400 && poisonTick.Damage == -600 && poisonTick.ScoreRefreshRequired &&
              poisonTick.HpUpdateRequired && BinaryPrimitives.ReadUInt32LittleEndian(poisonTick.AffectSnapshot.AsSpan(12)) == 1,
            $"Combat Type 20 did not use the LearnedSkill-derived 600 HP cap and consume its timer; hp={poisonTick.Hp}, damage={poisonTick.Damage}, time={BinaryPrimitives.ReadUInt32LittleEndian(poisonTick.AffectSnapshot.AsSpan(12))}.");
        Check(outcomes.Any(static outcome => outcome.Attack is { Damage: > 0 }),
            "The Combat Type 20 affect opportunity suppressed the summon attack.");
    }

    private static void CombatSummonPoisonClampsHpToOne()
    {
        var fixture = CreateCombatRouteTypeFiveSummonFixture(summonerOffset: 18);
        SeedSummonPoison(fixture.Summon, hp: 500, maxHp: 1_000, learnedSkill: 0, time: 2);

        for (var opportunity = 0; opportunity < 3; opportunity++)
            Check(fixture.Hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out _),
                "A Combat summon Poison HP-floor opportunity could not be processed.");

        Check(fixture.Hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var outcomes),
            "The Combat summon Poison HP-floor affect opportunity could not be processed.");

        var poisonTick = outcomes.Select(static outcome => outcome.AffectUpdate?.AffectTick)
            .FirstOrDefault(static tick => tick is not null);
        Check(poisonTick is { Hp: 1, Damage: -499, ScoreRefreshRequired: true, HpUpdateRequired: true } tick &&
              LegacyMobCombatState.Read(tick.MobSnapshot).CurrentScore.Hp == 1,
            "Combat Type 20 must clamp positive summon HP to 1 instead of killing it through poison arithmetic.");
    }

    private static void PeaceTypeTwentySynchronizesLinkedClanFourMount()
    {
        var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
        AddSummonOwner(hub, 1, "MOUNT_LEADER", 1_990, 2_000, 0, static (_, _) => ValueTask.CompletedTask);
        AddSummonOwner(hub, 2, "MOUNT_OWNER", 2_000, 2_000, 30, static (_, _) => ValueTask.CompletedTask);
        Check(hub.TrySetParty(1, [1, 2]), "The linked mount fixture could not form its summon party.");
        var summonSkill = new LegacySkillDefinition(50, 20, 0, 20, 0, 0, 11, 1, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "MOUNT_SUMMON");
        Check(hub.TryApplySkillAttack(2, 2, summonSkill, itemData: null, weather: 0, randomFactor: 90, out var summonOutcome) ==
              LegacySkillAttackResult.Accepted && summonOutcome?.SummonedMobs is { Count: 1 },
            "The linked mount fixture could not create its summon.");
        var summon = summonOutcome!.SummonedMobs!.Single();
        ConfigureLinkedMount(hub, summon, ownerConnectionId: 2, ownerX: 2_000, ownerY: 2_000, mountHp: 100);
        SeedSummonPoison(summon, hp: 10_000, maxHp: 10_000, learnedSkill: 0, time: 2);

        var mountOffset = LegacyAccountSnapshot.MobEquipmentOffset + (14 * LegacyItem.SizeInBytes);
        Check(hub.TryApplyRouteTypeFiveSummonPeaceStep(summon.ConnectionId, static () => 0, out var outcome) &&
              outcome.AffectTick is { Hp: 9_000, Damage: -1_000, MountOwnerConnectionId: 2, UpdatedMountItem: not null } tick &&
              hub.TryGetCharacterSnapshot(2, out var ownerMob, out _, out _) && ownerMob is not null &&
              BinaryPrimitives.ReadInt16LittleEndian(ownerMob.AsSpan(mountOffset + 2)) == 9_000 &&
              tick.UpdatedMountItem == LegacyItem.Read(ownerMob.AsSpan(mountOffset, LegacyItem.SizeInBytes)),
            "A peaceful Type 20 tick must copy the Clan 4 summon HP to the owner's matching equipped mount.");
    }

    private static void SeedSummonPoison(LegacySummonedMob summon, int hp, int maxHp, int learnedSkill, uint time)
    {
        var currentScore = LegacyMobCombatState.Read(summon.MobSnapshot).CurrentScore;
        (currentScore with { Hp = hp, MaxHp = maxHp }).Write(summon.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        BinaryPrimitives.WriteInt32LittleEndian(
            summon.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), learnedSkill);
        summon.AffectSnapshot[8] = 20;
        summon.AffectSnapshot[9] = 100;
        BinaryPrimitives.WriteUInt16LittleEndian(summon.AffectSnapshot.AsSpan(10), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(summon.AffectSnapshot.AsSpan(12), time);
    }

    private static void SeedSummonThunder(LegacySummonedMob summon, uint time)
    {
        summon.AffectSnapshot[8] = 22;
        summon.AffectSnapshot[9] = 100;
        BinaryPrimitives.WriteUInt16LittleEndian(summon.AffectSnapshot.AsSpan(10), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(summon.AffectSnapshot.AsSpan(12), time);
    }

    private static void SeedSummonTypeThirtyTwo(LegacySummonedMob summon, uint time)
    {
        summon.AffectSnapshot[8] = 32;
        summon.AffectSnapshot[9] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(summon.AffectSnapshot.AsSpan(10), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(summon.AffectSnapshot.AsSpan(12), time);
    }

    private static void ConfigureLinkedMount(
        WorldHub hub,
        LegacySummonedMob summon,
        int ownerConnectionId,
        short ownerX,
        short ownerY,
        short mountHp)
    {
        var ownerMob = CreateMob("P_MOUNT_OWNER", hp: 1_000, clan: 5, emptySkills: false);
        var mountOffset = LegacyAccountSnapshot.MobEquipmentOffset + (14 * LegacyItem.SizeInBytes);
        BinaryPrimitives.WriteInt16LittleEndian(ownerMob.AsSpan(mountOffset), 2_330);
        BinaryPrimitives.WriteInt16LittleEndian(ownerMob.AsSpan(mountOffset + 2), mountHp);
        Check(hub.SetCharacterState(ownerConnectionId, 0, 0, clan: 5, guildLevel: 0, coin: 0, ownerX, ownerY, ownerMob),
            "The linked mount owner snapshot could not be initialized.");

        summon.MobSnapshot[LegacyAccountSnapshot.MobClanOffset] = 4;
        BinaryPrimitives.WriteInt16LittleEndian(summon.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset), 315);
    }

    private static void SeedSummonLifeAura(LegacySummonedMob summon, int hp, int maxHp, uint time)
    {
        var currentScore = LegacyScore.Read(summon.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        (currentScore with { Hp = hp, MaxHp = maxHp }).Write(summon.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        summon.AffectSnapshot[8] = 17;
        summon.AffectSnapshot[9] = 20;
        BinaryPrimitives.WriteUInt16LittleEndian(summon.AffectSnapshot.AsSpan(10), 40);
        BinaryPrimitives.WriteUInt32LittleEndian(summon.AffectSnapshot.AsSpan(12), time);
    }

    private static void ResurrectionRelayIsRequiredWhenTheTargetRevives()
    {
        var hub = NewHub(allEmptySkills: true);
        var outcome = CreateRevivedAttackOutcome([1, 2]);
        var missingCallbackRejected = false;
        try
        {
            LegacyMobAiRelay.RelayAsync(
                hub,
                outcome,
                LegacyFrameCodec.CreateDefault(),
                sendResurrectionRefresh: null,
                CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }
        catch (InvalidOperationException)
        {
            missingCallbackRejected = true;
        }

        Check(missingCallbackRejected,
            "A revived target without a consumed-item refresh callback must fail instead of silently skipping its refresh.");
    }

    private static void ResurrectionRelayContinuesAfterATargetDisconnect()
    {
        var hub = NewHub(allEmptySkills: true);
        var observerFrames = 0;
        Check(hub.Enter(1, "DISCONNECTED_VIEWER", static (_, _) => throw new IOException("synthetic disconnect")),
            "Could not register the synthetic disconnected viewer.");
        Check(hub.Enter(2, "LIVE_VIEWER", (_, _) =>
        {
            observerFrames++;
            return ValueTask.CompletedTask;
        }), "Could not register the live viewer.");

        var refreshCalled = false;
        async ValueTask Refresh(int connectionId, byte[] mob, int itemSlot, LegacyItem item, IReadOnlyList<int> recipients, CancellationToken cancellationToken)
        {
            refreshCalled = true;
            await LegacyMobAiRelay.TrySendToViewerAsync(hub, connectionId, new byte[] { 2 }, cancellationToken);
            foreach (var recipient in recipients)
                if (recipient != connectionId)
                    await LegacyMobAiRelay.TrySendToViewerAsync(hub, recipient, new byte[] { 3 }, cancellationToken);
        }

        LegacyMobAiRelay.RelayAsync(
            hub,
            CreateRevivedAttackOutcome([1, 2]),
            LegacyFrameCodec.CreateDefault(),
            Refresh,
            CancellationToken.None,
            clientTick: 1,
            keywordIndex: 0).AsTask().GetAwaiter().GetResult();

        Check(refreshCalled && observerFrames == 2,
            "The real attack relay did not continue from its captured attack view into the resurrection observer refresh after the target disconnected.");
    }

    private static LegacyNpcAiApplicationOutcome CreateRevivedAttackOutcome(IReadOnlyList<int> recipients)
    {
        var attack = new LegacyGeneratedNpcPhysicalAttackOutcome(
            AttackerConnectionId: 1001,
            TargetConnectionId: 1,
            AttackerPositionX: 10,
            AttackerPositionY: 10,
            TargetPositionX: 10,
            TargetPositionY: 11,
            Damage: 100,
            RemainingHp: 1000,
            TargetMobSnapshot: [],
            TargetDied: false,
            Missed: false,
            TargetRevived: true,
            ConsumedItemSlot: 0,
            ConsumedItem: new LegacyItem(3463, 61, 2, 0, 0, 0, 0),
            ViewRecipientConnectionIds: recipients);
        return new LegacyNpcAiApplicationOutcome(
            LegacyNpcAiApplyStatus.Applied,
            LegacyMobAiDecisionKind.Attack,
            10,
            10,
            LegacyNpcMode.Combat,
            1,
            [1],
            10,
            10,
            0,
            0,
            0,
            null,
            attack);
    }

    private static void DirectTicksVisitEachGeneratedNpcOncePerFourTurns()
    {
        var hub = NewHub(allEmptySkills: true);
        var ids = SpawnNpcs(hub, 4);
        var runtime = NewRuntime(hub);

        var firstCycle = Enumerable.Range(0, 4)
            .Select(_ => runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult())
            .ToArray();
        Check(firstCycle.Select(static tick => tick.ShardIndex).SequenceEqual([0, 1, 2, 3]),
            "One nominal tick must select exactly one shard and advance through the four stable shard IDs.");
        Check(firstCycle.SelectMany(static tick => tick.VisitedNpcIds).Order().SequenceEqual(ids.Order()),
            "Four direct ticks must visit every generated NPC exactly once.");

        var secondCycle = Enumerable.Range(0, 4)
            .Select(_ => runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult())
            .ToArray();
        Check(secondCycle.Select(static tick => tick.ShardIndex).SequenceEqual([0, 1, 2, 3]) &&
              secondCycle.SelectMany(static tick => tick.VisitedNpcIds).Order().SequenceEqual(ids.Order()),
            "The next four direct ticks must revisit each generated NPC once, without a duplicate inside either cycle.");
    }

    private static void ScheduledTicksProcessSourceAlignedPeaceBeforeCombat()
    {
        var hub = NewHub(allEmptySkills: true);
        AddPlayer(hub, 1, "PASSIVE_TARGET", 11, 10, hp: 100_000, clan: 3);
        var ids = SpawnNpcs(hub, 12);
        Check(ids.SequenceEqual(Enumerable.Range(20_000, 12)), "The shard fixture did not allocate the expected generated-NPC IDs.");
        var runtime = NewRuntime(hub);
        var acquiredIds = new HashSet<int>();
        var peaceVisited = new List<int>();
        var combatVisited = new List<int>();
        var expectedPeaceBuckets = new[]
        {
            new[] { 20_002, 20_008 },
            new[] { 20_003, 20_009 },
            new[] { 20_004, 20_010 },
            new[] { 20_005, 20_011 },
            new[] { 20_000, 20_006 },
            new[] { 20_001, 20_007 },
        };
        var expectedCombatBuckets = new[]
        {
            new[] { 20_000, 20_004, 20_008 },
            new[] { 20_001, 20_005, 20_009 },
            new[] { 20_002, 20_006, 20_010 },
            new[] { 20_003, 20_007, 20_011 },
        };

        for (var callback = 0; callback < 6; callback++)
        {
            var positionsBeforeTick = ids.ToDictionary(id => id, id =>
            {
                Check(hub.TryGetNpcSnapshot(id, out var npc) && npc is not null, $"Could not capture generated NPC {id} before callback {callback}.");
                return (npc!.PositionX, npc.PositionY);
            });
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            var newlyAcquired = ids.Where(id =>
            {
                Check(hub.TryGetNpcSnapshot(id, out var npc) && npc is not null, $"Generated NPC {id} disappeared during the scheduled tick.");
                return npc!.Mode == LegacyNpcMode.Combat && !acquiredIds.Contains(id);
            }).Order().ToArray();

            Check(newlyAcquired.SequenceEqual(expectedPeaceBuckets[callback]),
                $"Callback {callback} must passively acquire exactly its MAX_USER-relative Peace shard before Combat; observed [{string.Join(',', newlyAcquired)}].");

            var peaceShardIndex = ReadIntResultProperty(tick, "PeaceShardIndex");
            var peaceNpcIds = ReadNpcIdResultProperty(tick, "VisitedPeaceNpcIds");
            Check(peaceShardIndex == callback && peaceNpcIds.SequenceEqual(expectedPeaceBuckets[callback]),
                $"Callback {callback} did not expose its source-aligned Peace shard and visited IDs.");
            Check(tick.ShardIndex == callback % 4 && tick.VisitedNpcIds.SequenceEqual(expectedCombatBuckets[callback % 4]),
                $"Callback {callback} changed the existing four-shard Combat bucket.");

            acquiredIds.UnionWith(newlyAcquired);
            peaceVisited.AddRange(peaceNpcIds);
            combatVisited.AddRange(tick.VisitedNpcIds);

            foreach (var id in ids)
            {
                if (tick.VisitedNpcIds.Contains(id))
                    continue;
                Check(hub.TryGetNpcSnapshot(id, out var npc) && npc is not null &&
                      (npc.PositionX, npc.PositionY) == positionsBeforeTick[id],
                    $"The scheduled Combat pass moved NPC {id} outside its selected shard.");
            }
            Check(tick.Attacks.Count == 0 &&
                  hub.TryGetCharacterSnapshot(1, out var targetMob, out _, out _) && targetMob is not null &&
                  LegacyMobCombatState.Read(targetMob).CurrentScore.Hp == 100_000,
                "Passive acquisition must not attack or damage a player when the generated NPC has no attack range.");

            if (callback == 0)
            {
                Check(hub.TryGetNpcSnapshot(20_008, out var sharedShardNpc) && sharedShardNpc is not null &&
                      sharedShardNpc.Mode == LegacyNpcMode.Combat && sharedShardNpc.CurrentTarget == 1 &&
                      sharedShardNpc.EnemyList.Contains(1),
                    "The mob selected by both shard phases must be acquired first and then consumed by Combat in the same callback.");
            }
        }

        Check(acquiredIds.Order().SequenceEqual(Enumerable.Range(20_000, 12)) &&
              peaceVisited.Order().SequenceEqual(Enumerable.Range(20_000, 12)) &&
              combatVisited.Order().SequenceEqual(expectedCombatBuckets.Take(2).Concat(expectedCombatBuckets.Take(2))
                  .Concat(expectedCombatBuckets.Skip(2)).SelectMany(static bucket => bucket).Order()),
            "Six callbacks must visit and acquire each generated NPC once in Peace while preserving repeated four-shard Combat visits.");
    }

    private static void AdjacentTurnsDoNotDuplicateOneShardAttack()
    {
        var hub = NewHub(allEmptySkills: true);
        var npcId = SpawnNpcs(hub, 1)[0];
        AddPlayer(hub, 1, "AI_TARGET", 10, 10, hp: 100_000);
        Check(hub.SetNpcCombatState(npcId, LegacyNpcMode.Combat, 1, [1]), "Could not seed explicit combat target.");
        var relayed = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, relayed.Add);
        var attacks = new List<LegacyGeneratedNpcPhysicalAttackOutcome>();
        var tickShards = new List<int>();
        var attackTicks = new List<int>();

        for (var index = 0; index < 8; index++)
        {
            var result = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            tickShards.Add(result.ShardIndex);
            if (result.Attacks.Count > 0)
                attackTicks.Add(index);
            attacks.AddRange(result.Attacks);
        }

        var expectedShard = npcId % LegacyMobAiRuntime.ShardCount;
        Check(tickShards.SequenceEqual([0, 1, 2, 3, 0, 1, 2, 3]) &&
              attacks.Count == 2 && attacks.All(attack => attack.AttackerConnectionId == npcId && attack.Damage > 0) &&
              relayed.Count(outcome => outcome.Attack is { Damage: > 0 }) == 2 &&
              attackTicks.SequenceEqual([expectedShard, expectedShard + LegacyMobAiRuntime.ShardCount]),
            "A generated NPC must attack once on its shard in each four-tick cycle, not on adjacent shard ticks.");
        Check(runtime.ActiveTickCount == 0 && runtime.ActiveWorkerCount == 0,
            "A completed direct-tick run left a worker or tick active.");
    }

    private static void OutOfRangeExplicitTargetIsChasedAndRelayedAcrossViewChanges()
    {
        var hub = NewHub(allEmptySkills: true);
        var codec = LegacyFrameCodec.CreateDefault();
        var framesByViewer = new Dictionary<int, List<byte[]>>();
        AddViewPlayer(1, "FAR_AI_TARGET", 26, 20);
        AddViewPlayer(2, "LEAVING_VIEW", 4, 20);
        AddViewPlayer(3, "ENTERING_VIEW", 41, 20);
        AddViewPlayer(4, "OUTSIDE_VIEW", 50, 20);
        Check(hub.TrySpawnGeneratedNpc(42, out var spawnedNpc, randomRoll: static _ => 0, positionX: 20, positionY: 20) && spawnedNpc is not null,
            "The generated chase fixture did not spawn.");
        var npcId = spawnedNpc!.ConnectionId;
        Check(hub.SetNpcCombatState(npcId, LegacyNpcMode.Combat, 1, [1]), "Could not seed out-of-range explicit target.");
        Check(hub.TryGetNpcSnapshot(npcId, out var before) && before is not null,
            "Could not capture generated NPC before its out-of-range tick.");
        var runtime = new LegacyMobAiRuntime(hub, itemData: null, (outcome, cancellationToken) =>
            LegacyMobAiRelay.RelayAsync(
                hub,
                outcome,
                codec,
                sendResurrectionRefresh: null,
                cancellationToken,
                clientTick: 5,
                keywordIndex: 0), nextRoll: static _ => 0);
        var tick = runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount).AsTask().GetAwaiter().GetResult();

        Check(tick.Attacks.Count == 0 && hub.TryGetCharacterSnapshot(1, out var targetMob, out _, out _) && targetMob is not null &&
              LegacyMobCombatState.Read(targetMob).CurrentScore.Hp == 100_000 &&
              hub.TryGetNpcSnapshot(npcId, out var after) && after is not null &&
              after.PositionX == 25 && after.PositionY == 19 &&
              before is not null && before.PositionX == 20 && before.PositionY == 20,
            "An explicit out-of-range target must drive the source-compatible generated-NPC chase step without applying attack damage.");

        var shared = Decode(framesByViewer[1]);
        var leaving = Decode(framesByViewer[2]);
        var entering = Decode(framesByViewer[3]);
        Check(shared.Count(frame => frame.Header.Type == ActionRequest.MessageType) == 1 &&
              ActionRequest.TryParse(shared.Single(frame => frame.Header.Type == ActionRequest.MessageType), out var action) &&
              action is { PositionX: 20, PositionY: 20, TargetX: 25, TargetY: 19, Effect: 0, Speed: 1 },
            "A viewer in both old and new NPC views must receive one source-shaped Action frame.");
        Check(leaving.Count(frame => frame.Header.Type == ActionRequest.MessageType) == 1 &&
              leaving.Count(frame => frame.Header.Type == RemoveMobConfirmation.MessageType) == 1,
            "A viewer leaving the NPC view must receive its final Action and one RemoveMob frame.");
        Check(entering.Count(frame => frame.Header.Type == CreateMobConfirmationV769.MessageType) == 1 &&
              entering.Count(frame => frame.Header.Type == ActionRequest.MessageType) == 1,
            "A viewer entering the NPC view must receive CreateMob before the movement Action.");
        Check(framesByViewer[4].Count == 0,
            "A viewer outside both NPC views must not receive movement or presence frames.");

        void AddViewPlayer(int id, string name, short x, short y)
        {
            framesByViewer.Add(id, []);
            Check(hub.Enter(id, name, (frame, _) =>
            {
                framesByViewer[id].Add(frame.ToArray());
                return ValueTask.CompletedTask;
            }), $"Could not register view fixture {id}.");
            Check(hub.SetCharacterState(id, 0, 0, clan: 5, guildLevel: 0, coin: 0, x, y,
                    CreateMob(name, hp: 100_000, clan: 5, emptySkills: false)),
                $"Could not set view fixture {id} state.");
        }

        List<DecodedFrame> Decode(IEnumerable<byte[]> frames) => frames.Select(frame => codec.Decode(frame)).ToList();
    }

    private static void OutOfLeashCombatNpcReturnsToRouteAndRelaysPeaceTransition()
    {
        var hub = NewHub(allEmptySkills: true, attackRun: 1);
        var codec = LegacyFrameCodec.CreateDefault();
        var frames = new List<byte[]>();
        Check(hub.Enter(1, "RETURN_TARGET", (frame, _) =>
        {
            frames.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        }), "Could not register the return target.");
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0, 28, 10,
            CreateMob("RETURN_TARGET", hp: 100_000, clan: 5, emptySkills: false)),
            "Could not set the return target state.");
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 27, positionY: 10) && npc is not null,
            "The out-of-leash generated NPC did not spawn.");
        Check(npc!.SegmentX == 6 && npc.SegmentY == 6 &&
              hub.SetNpcCombatState(npc.ConnectionId, LegacyNpcMode.Combat, 1, [1]),
            "Could not seed an out-of-leash Combat NPC with a live nearby target.");
        var runtime = new LegacyMobAiRuntime(hub, itemData: null,
            (outcome, cancellationToken) => LegacyMobAiRelay.RelayAsync(
                hub,
                outcome,
                codec,
                sendResurrectionRefresh: null,
                cancellationToken,
                clientTick: 7,
                keywordIndex: 0),
            nextRoll: static maximumExclusive => maximumExclusive == 5 ? 4 : 0);
        var tick = runtime.TickShardAsync(npc.ConnectionId % LegacyMobAiRuntime.ShardCount)
            .AsTask().GetAwaiter().GetResult();
        var hasCombatState = hub.TryGetNpcCombatState(npc.ConnectionId, out var mode, out var target, out var enemies);
        var hasReturnedSnapshot = hub.TryGetNpcSnapshot(npc.ConnectionId, out var returned);

        Check(tick.Attacks.Count == 0 &&
              hasCombatState &&
              mode == LegacyNpcMode.Peace && target == 0 && enemies.All(static id => id == 0) &&
              hasReturnedSnapshot && returned is not null &&
              returned.PositionX < npc.PositionX && returned.PositionX >= npc.SegmentX,
            $"A combat NPC beyond its 16-tile leash must step toward its route and clear its combat state; attacks={tick.Attacks.Count}, state={hasCombatState}/{mode}/{target}/{(hasCombatState ? string.Join(',', enemies) : "n/a")}, snapshot={hasReturnedSnapshot}/{returned?.PositionX},{returned?.PositionY}, origin={npc.PositionX},{npc.PositionY}, segment={npc.SegmentX},{npc.SegmentY}, frames={frames.Count}.");

        var actions = frames.Select(frame => codec.Decode(frame))
            .Where(frame => frame.Header.Type == ActionRequest.MessageType)
            .ToArray();
        Check(actions.Length == 1 && ActionRequest.TryParse(actions[0], out var action) &&
              action is { PositionX: 27, PositionY: 10, Effect: 0 } &&
              action.TargetX == returned!.PositionX && action.TargetY == returned.PositionY,
            "The return step must relay one source-shaped Action frame to the captured old view.");
    }

    private static void PeaceNpcContinuesWalkingTowardItsCurrentSegmentAndRelaysAction()
    {
        var hub = NewHub(allEmptySkills: true, attackRun: 1, strength: 1);
        var codec = LegacyFrameCodec.CreateDefault();
        var frames = new List<byte[]>();
        Check(hub.Enter(1, "PEACE_VIEWER", (frame, _) =>
        {
            frames.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        }), "Could not register the Peace movement viewer.");
        Check(hub.SetCharacterState(1, 0, 0, clan: 2, guildLevel: 0, coin: 0, 27, 11,
            CreateMob("PEACE_VIEWER", hp: 100_000, clan: 2, emptySkills: false)),
            "Could not set the Peace viewer state.");
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 27, positionY: 10) && npc is not null,
            "The Peace route generated NPC did not spawn.");
        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = new LegacyMobAiRuntime(hub, itemData: null, (outcome, cancellationToken) =>
        {
            outcomes.Add(outcome);
            return LegacyMobAiRelay.RelayAsync(
                hub,
                outcome,
                codec,
                sendResurrectionRefresh: null,
                cancellationToken,
                clientTick: 7,
                keywordIndex: 0);
        }, nextRoll: static _ => 0);
        LegacyMobAiTickResult? visitedTick = null;
        for (var callback = 0; callback < LegacyMobAiRuntime.PeaceShardCount; callback++)
        {
            var tick = runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            if (tick.VisitedPeaceNpcIds.Contains(npc!.ConnectionId))
            {
                visitedTick = tick;
                break;
            }
        }

        var hasMovedSnapshot = hub.TryGetNpcSnapshot(npc!.ConnectionId, out var moved);
        Check(visitedTick is not null && visitedTick.PeaceShardIndex >= 0 &&
              hasMovedSnapshot && moved is not null &&
              Math.Max(Math.Abs(moved.PositionX - npc.PositionX), Math.Abs(moved.PositionY - npc.PositionY)) == 1 &&
              Math.Max(Math.Abs(moved.PositionX - moved.SegmentX), Math.Abs(moved.PositionY - moved.SegmentY)) <
              Math.Max(Math.Abs(npc.PositionX - npc.SegmentX), Math.Abs(npc.PositionY - npc.SegmentY)) &&
              outcomes is [{ Status: LegacyNpcAiApplyStatus.Applied, DecisionKind: LegacyMobAiDecisionKind.Move }],
            "A Peace NPC away from its current segment must make one route step toward it on its six-shard turn.");

        var actions = frames.Select(frame => codec.Decode(frame))
            .Where(frame => frame.Header.Type == ActionRequest.MessageType)
            .ToArray();
        Check(actions.Length == 1 && ActionRequest.TryParse(actions[0], out var action) &&
              action is { PositionX: 27, PositionY: 10, Effect: 0 } &&
              action.TargetX == moved!.PositionX && action.TargetY == moved.PositionY,
            "The Peace route step must relay one ordinary Action frame to the captured view.");
    }

    private static void PeaceRouteTypeFiveSummonFollowsItsSummonerAndRelaysWithinCapturedViews()
    {
        var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
        var codec = LegacyFrameCodec.CreateDefault();
        var ownerFrames = new List<byte[]>();
        var observerFrames = new List<byte[]>();
        var destinationFrames = new List<byte[]>();
        var outsideFrames = new List<byte[]>();
        AddSummonOwner(hub, 1, "FOLLOW_LEADER", 1_990, 2_000, 0, static (_, _) => ValueTask.CompletedTask);
        AddSummonOwner(hub, 2, "FOLLOW_OWNER", 2_000, 2_000, 30, (frame, _) =>
        {
            ownerFrames.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
        Check(hub.TrySetParty(1, [1, 2]), "The follower caster could not join the leader's party.");
        AddViewer(hub, 3, "FOLLOW_OBSERVER", 1_990, 1_990, observerFrames);
        AddViewer(hub, 4, "FOLLOW_DEST", 2_050, 2_000, destinationFrames);
        AddViewer(hub, 5, "FOLLOW_OUTSIDE", 2_050, 2_000, outsideFrames);

        var skill = new LegacySkillDefinition(50, 20, 0, 20, 0, 0, 11, 1, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "Evocar");
        Check(hub.TryApplySkillAttack(2, 2, skill, itemData: null, weather: 0, randomFactor: 90, out var summonOutcome) ==
              LegacySkillAttackResult.Accepted && summonOutcome?.SummonedMobs is { Count: 1 },
            "The RouteType 5 summon fixture could not be created.");
        var summon = summonOutcome!.SummonedMobs!.Single();
        Check(hub.UpdatePosition(2, checked((short)(summon.PositionX + 8)), summon.PositionY),
            "The summoner could not move to the 5..12 follower threshold.");

        for (byte clan = 0; clan < 9; clan++)
        {
            var targetId = 10 + clan;
            var targetName = $"FOLLOW_T{clan}";
            var targetMob = CreateMob(targetName, 1_000, clan, false);
            Check(hub.Enter(targetId, targetName, static (_, _) => ValueTask.CompletedTask) &&
                  hub.SetCharacterState(targetId, 0, 0, clan, 0, 0,
                      checked((short)(summon.PositionX + 1)), summon.PositionY, targetMob),
                "A hostile-target candidate could not be registered for the summon fallback selector.");
        }

        Check(hub.TrySelectSummonTarget(summon.ConnectionId, out var selectedTarget) &&
              selectedTarget is { TargetConnectionId: >= 10 and <= 18 } &&
              hub.TryGetSummonEnemyList(summon.ConnectionId, out var selectedEnemyList) && selectedEnemyList.All(static id => id == 0),
            "The summon fallback selector must retain its selected target outside the legacy EnemyList.");
        foreach (var frames in new[] { ownerFrames, observerFrames, destinationFrames, outsideFrames })
            frames.Clear();

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = new LegacyMobAiRuntime(hub, itemData: null, async (outcome, cancellationToken) =>
        {
            outcomes.Add(outcome);
            await LegacyMobAiRelay.RelayAsync(
                hub,
                outcome,
                codec,
                sendResurrectionRefresh: null,
                cancellationToken,
                clientTick: 1,
                keywordIndex: 0);
        }, nextRoll: static _ => 0);

        for (var callback = 0; callback < LegacyMobAiRuntime.PeaceShardCount; callback++)
            runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();

        Check(outcomes.Count == 0 && hub.TryGetSummonTarget(summon.ConnectionId, out var currentTarget) &&
              currentTarget == selectedTarget!.TargetConnectionId,
            "A RouteType 5 summon with a live selected combat target must not run the peaceful follow branch even when EnemyList is empty.");

        Check(hub.Leave(selectedTarget!.TargetConnectionId), "The selected combat target could not be disconnected for stale-target cleanup.");
        foreach (var frames in new[] { ownerFrames, observerFrames, destinationFrames, outsideFrames })
            frames.Clear();
        for (var callback = 0; callback < LegacyMobAiRuntime.PeaceShardCount; callback++)
            runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
        Check(hub.TryGetSummonTarget(summon.ConnectionId, out var clearedTarget) && clearedTarget == 0,
            "A stale selected target must be cleared before the summon resumes peaceful following.");

        var movement = outcomes.SingleOrDefault()?.Movement;
        Check(outcomes is [{ Status: LegacyNpcAiApplyStatus.Applied, DecisionKind: LegacyMobAiDecisionKind.Move }] &&
              movement is not null && movement.ConnectionId == summon.ConnectionId &&
              (movement.FromX != movement.ToX || movement.FromY != movement.ToY),
            "A RouteType 5 summon outside four cells must follow its active summoner on its Peace shard.");

        var ownerActions = ownerFrames.Select(frame => codec.Decode(frame))
            .Where(frame => frame.Header.Type == ActionRequest.MessageType)
            .ToArray();
        var observerActions = observerFrames.Select(frame => codec.Decode(frame))
            .Where(frame => frame.Header.Type == ActionRequest.MessageType)
            .ToArray();
        var outsideActions = outsideFrames.Select(frame => codec.Decode(frame))
            .Where(frame => frame.Header.Type == ActionRequest.MessageType)
            .ToArray();
        Check(ownerActions.Length == 1 && observerActions.Length == 1 && outsideActions.Length == 0 &&
              ActionRequest.TryParse(ownerActions[0], out var action) &&
              action is { Effect: 0 } && action.TargetX == movement!.ToX && action.TargetY == movement.ToY,
            "The RouteType 5 follower must relay one ordinary Action only to viewers captured around its old tile.");

        var firstMovement = movement!;
        outcomes.Clear();
        ownerFrames.Clear();
        observerFrames.Clear();
        outsideFrames.Clear();
        var farSummonerX = checked((short)(firstMovement.ToX + 12));
        Check(hub.UpdatePosition(2, farSummonerX, firstMovement.ToY),
            "The party-member summoner could not move to the direct-follow threshold.");
        Check(hub.UpdatePosition(3, checked((short)(firstMovement.ToX - 16)), firstMovement.ToY) &&
              hub.UpdatePosition(4, checked((short)(farSummonerX + 15)), firstMovement.ToY),
            "The direct-follow fixture could not place exclusive origin and destination viewers.");
        for (var callback = 0; callback < LegacyMobAiRuntime.PeaceShardCount; callback++)
            runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();

        var farMovement = outcomes.SingleOrDefault()?.Movement;
        Check(outcomes is [{ Status: LegacyNpcAiApplyStatus.Applied, DecisionKind: LegacyMobAiDecisionKind.Move }] &&
              farMovement is not null && farMovement.ConnectionId == summon.ConnectionId &&
              Math.Max(Math.Abs(farSummonerX - farMovement.ToX), Math.Abs(firstMovement.ToY - farMovement.ToY)) <= 1,
            "At thirteen cells, a RouteType 5 follower must take the direct GetEmptyMobGrid destination near its party-member summoner.");

        var originFrames = observerFrames.Select(frame => codec.Decode(frame)).ToArray();
        var destinationDecoded = destinationFrames.Select(frame => codec.Decode(frame)).ToArray();
        var outsideDecoded = outsideFrames.Select(frame => codec.Decode(frame)).ToArray();
        Check(originFrames.Length == 2 &&
              originFrames[0].Header.Type == ActionRequest.MessageType &&
              ActionRequest.TryParse(originFrames[0], out var directAction) &&
              directAction is { Effect: 1, Speed: 2 } && directAction.TargetX == farMovement!.ToX && directAction.TargetY == farMovement.ToY &&
              originFrames[1].Header.Type == RemoveMobConfirmation.MessageType &&
              originFrames[1].Header.Id == farMovement.ConnectionId,
            "A viewer leaving the direct-follow origin must receive teleport Action with Effect=1, then RemoveMob.");
        Check(destinationDecoded.Length == 2 &&
              destinationDecoded[0].Header.Type == CreateMobConfirmationV769.MessageType &&
              destinationDecoded[1].Header.Type == ActionRequest.MessageType &&
              ActionRequest.TryParse(destinationDecoded[1], out var enteringAction) &&
              enteringAction is { Effect: 1, Speed: 2 } && enteringAction.TargetX == farMovement!.ToX && enteringAction.TargetY == farMovement.ToY,
            "A viewer entering the direct-follow destination must receive CreateMob before teleport Action with Effect=1.");
        Check(outsideDecoded.Length == 0,
            "A viewer outside both direct-follow views must not receive movement or presence frames.");

        var directMovement = farMovement!;
        outcomes.Clear();
        ownerFrames.Clear();
        Check(hub.UpdatePosition(2, checked((short)(directMovement.ToX + 4)), directMovement.ToY),
            "The summoner could not be moved inside the four-cell idle threshold.");
        for (var callback = 0; callback < LegacyMobAiRuntime.PeaceShardCount; callback++)
            runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
        Check(outcomes.Count == 0 && ownerFrames.Count == 0,
            "A RouteType 5 follower within four cells must remain still on its Peace shard.");
    }

    private static void PeaceRouteTypeFiveSummonExpiresWhenItsTypeTwentyFourTimerEnds()
    {
        var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
        AddSummonOwner(hub, 1, "EXP_LEAD", 1_990, 2_000, 0, static (_, _) => ValueTask.CompletedTask);
        AddSummonOwner(hub, 2, "SUMEXP", 2_000, 2_000, 30, static (_, _) => ValueTask.CompletedTask);
        Check(hub.TrySetParty(1, [1, 2]), "The summon expiry caster could not join its leader's party.");
        var summonSkill = new LegacySkillDefinition(
            50, 20, 0, 20, 0, 0, 11, 1, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "Evocar");
        Check(hub.TryApplySkillAttack(2, 2, summonSkill, itemData: null, weather: 0, randomFactor: 90, out var summonOutcome) ==
              LegacySkillAttackResult.Accepted && summonOutcome?.SummonedMobs is { Count: 1 },
            "The Type 24 expiry fixture could not create a source-shaped RouteType 5 summon.");

        var summon = summonOutcome!.SummonedMobs!.Single();
        Check(summon.RouteType == 5 && summon.AffectSnapshot[0] == 24 &&
              BinaryPrimitives.ReadUInt32LittleEndian(summon.AffectSnapshot.AsSpan(4)) == 20,
            "The ordinary 7.69 summon fixture must start with finite Type 24 time 20.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = NewRuntime(hub, outcomes.Add);
        for (var tick = 0; tick < LegacyMobAiRuntime.PeaceShardCount * 19; tick++)
            runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();

        Check(hub.TryGetSummonEnemyList(summon.ConnectionId, out _) &&
              outcomes.All(static outcome => outcome.Removal is null),
            "The peaceful summon must remain present before its twentieth Type 24 timer opportunity.");

        for (var tick = 0; tick < LegacyMobAiRuntime.PeaceShardCount; tick++)
            runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();

        Check(!hub.TryGetSummonEnemyList(summon.ConnectionId, out _) &&
              outcomes is [{ Removal: { RemoveType: 3, ViewRecipientConnectionIds.Count: 2 } removal }] &&
              removal.ConnectionId == summon.ConnectionId &&
              removal.ViewRecipientConnectionIds.Contains(1) && removal.ViewRecipientConnectionIds.Contains(2),
            "The twentieth peaceful Type 24 timer opportunity must remove the summon with captured RemoveMob(type=3) recipients.");
    }

    private static void PeaceRouteTypeFiveTeleportUsesLegacySpeedAndGridSearchRadius()
    {
        const short targetX = 250;
        const short targetY = 250;
        var height = new byte[LegacyMapGrid.HeightMapSize];
        for (var y = targetY - 4; y <= targetY + 4; y++)
        for (var x = targetX - 4; x <= targetX + 4; x++)
            height[(y * LegacyMapGrid.HeightWidth) + x] = 127;

        var map = new LegacyMapGrid(height, new byte[LegacyMapGrid.AttributeMapSize]);
        var hub = new WorldHub(map, mapCollisionMode: LegacyMapCollisionMode.CandidateAware, summonCatalog: LoadSummonCatalog());
        AddSummonOwner(hub, 1, "GRID_LEADER", 90, 100, 0, static (_, _) => ValueTask.CompletedTask);
        AddSummonOwner(hub, 2, "GRID_CASTER", 100, 100, 30, static (_, _) => ValueTask.CompletedTask);
        Check(hub.TrySetParty(1, [1, 2]), "The grid-search summon fixture could not form its party.");

        var skill = new LegacySkillDefinition(50, 20, 0, 20, 0, 0, 11, 1, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "Evocar");
        Check(hub.TryApplySkillAttack(2, 2, skill, itemData: null, weather: 0, randomFactor: 90, out var summonOutcome) ==
              LegacySkillAttackResult.Accepted && summonOutcome?.SummonedMobs is { Count: 1 },
            "The grid-search RouteType 5 summon could not be created.");
        var summon = summonOutcome!.SummonedMobs!.Single();
        Check(hub.UpdatePosition(2, targetX, targetY), "The summoner could not move to the blocked direct-follow center.");

        Check(hub.TryApplyRouteTypeFiveSummonPeaceStep(summon.ConnectionId, static () => 0, out var outcome) &&
              outcome.Movement is { Effect: 1, Speed: 2 } movement &&
              Math.Max(Math.Abs(targetX - movement.ToX), Math.Abs(targetY - movement.ToY)) == 5,
            "A direct RouteType 5 teleport must use DoTeleport speed 2 and search free cells through radius 9.");
    }

    private static void CombatSummonExpiresOnItsTwentiethTypeTwentyFourTimerOpportunity()
    {
        var fixture = CreateCombatRouteTypeFiveSummonFixture(summonerOffset: 18);
        Check(fixture.Summon.AffectSnapshot[0] == 24 &&
              BinaryPrimitives.ReadUInt32LittleEndian(fixture.Summon.AffectSnapshot.AsSpan(4)) == 20,
            "The combat summon fixture must start with ordinary Type 24 time 20.");

        for (var affectOpportunity = 0; affectOpportunity < 19; affectOpportunity++)
        for (var battleOpportunity = 0; battleOpportunity < 4; battleOpportunity++)
        {
            Check(fixture.Hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var outcomes) &&
                  outcomes is [{ Movement: null, Attack: { Damage: > 0 }, Despawn: null }],
                $"Combat Type 24 must leave the summon attacking through timer opportunity {affectOpportunity + 1}, battle step {battleOpportunity + 1}.");
        }

        Check(fixture.Hub.TryGetSummonEnemyList(fixture.Summon.ConnectionId, out _),
            "The combat summon must remain present after nineteen Type 24 timer opportunities.");

        for (var battleOpportunity = 0; battleOpportunity < 3; battleOpportunity++)
        {
            Check(fixture.Hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var outcomes) &&
                  outcomes is [{ Movement: null, Attack: { Damage: > 0 }, Despawn: null }],
                $"The twentieth Type 24 opportunity must wait for its fourth 800 ms battle cycle, step {battleOpportunity + 1}.");
        }

        Check(fixture.Hub.TryGetSummonEnemyList(fixture.Summon.ConnectionId, out _),
            "The combat summon must remain present before the fourth battle cycle of its twentieth Type 24 opportunity.");

        var expiryAccepted = fixture.Hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var expiry);
        var expiryDescription = string.Join(";", expiry.Select(static outcome =>
            $"move={outcome.Movement is not null},attack={outcome.Attack?.Damage},despawn={outcome.Despawn?.Removal?.RemoveType}:{outcome.Despawn?.Removal?.ConnectionId}:view=[{string.Join(',', outcome.Despawn?.Removal?.ViewRecipientConnectionIds ?? [])}]"));
        var summonStillPresent = fixture.Hub.TryGetSummonEnemyList(fixture.Summon.ConnectionId, out _);
        Check(expiryAccepted &&
              expiry is [{ Movement: null, Attack: null, Despawn: { DecisionKind: LegacyMobAiDecisionKind.Despawn,
                  Removal: { RemoveType: 3 } removal } }] &&
              removal.ConnectionId == fixture.Summon.ConnectionId &&
              removal.ViewRecipientConnectionIds.Contains(1) &&
              removal.ViewRecipientConnectionIds.Contains(fixture.TargetConnectionId) &&
              !removal.ViewRecipientConnectionIds.Contains(6) &&
              !fixture.Hub.TryGetSummonEnemyList(fixture.Summon.ConnectionId, out _),
            $"The twentieth Combat Type 24 timer opportunity must remove the summon before attack and capture only in-view recipients (accepted={expiryAccepted}, present={summonStillPresent}, outcomes=[{expiryDescription}]).");
    }

    private static void TargetlessCombatSummonExpiresOnItsTwentiethTypeTwentyFourTimerOpportunity()
    {
        var fixture = CreateCombatRouteTypeFiveSummonFixture(summonerOffset: 18);
        Check(fixture.Hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0, positionX: 90, positionY: 100,
                  CreateMob("COMBAT_LEADER", hp: 0, clan: 5, emptySkills: false)),
            "The RouteType 5 party leader could not be marked dead for the targetless Combat fixture.");
        foreach (var connectionId in new[] { 4, 5, 6, 10, 11, 12, 13, 14, 15, 16, 17, 18 })
            Check(fixture.Hub.Leave(connectionId), $"Combat target/view participant {connectionId} could not be removed to leave the summon without a target.");
        Check(fixture.Hub.TrySelectSummonTarget(fixture.Summon.ConnectionId, out var selection) &&
              selection is { TargetConnectionId: 0 },
            "The targetless Combat Type 24 fixture must have no live selected target.");

        for (var affectOpportunity = 0; affectOpportunity < 19; affectOpportunity++)
        for (var battleOpportunity = 0; battleOpportunity < 4; battleOpportunity++)
        {
            Check(fixture.Hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var outcomes) && outcomes.Count == 0,
                $"A targetless Combat summon must remain idle through Type 24 opportunity {affectOpportunity + 1}, battle step {battleOpportunity + 1}.");
        }

        Check(fixture.Hub.TryGetSummonEnemyList(fixture.Summon.ConnectionId, out _),
            "The targetless Combat summon must remain present after nineteen Type 24 opportunities.");
        for (var battleOpportunity = 0; battleOpportunity < 3; battleOpportunity++)
        {
            Check(fixture.Hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var outcomes) && outcomes.Count == 0,
                $"The twentieth targetless Type 24 opportunity must wait for battle step {battleOpportunity + 1} of four.");
        }

        Check(fixture.Hub.TryGetSummonEnemyList(fixture.Summon.ConnectionId, out _),
            "The targetless Combat summon must remain present before the fourth battle cycle of its twentieth Type 24 opportunity.");
        var expiryAccepted = fixture.Hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var expiry);
        var expiryDescription = string.Join(";", expiry.Select(static outcome =>
            $"move={outcome.Movement is not null},attack={outcome.Attack?.Damage},despawn={outcome.Despawn?.Removal?.RemoveType}:{outcome.Despawn?.Removal?.ConnectionId}"));
        Check(expiryAccepted &&
              expiry is [{ Movement: null, Attack: null, Despawn: { DecisionKind: LegacyMobAiDecisionKind.Despawn,
                  Removal: { RemoveType: 3 } removal } }] &&
              removal.ConnectionId == fixture.Summon.ConnectionId &&
              !fixture.Hub.TryGetSummonEnemyList(fixture.Summon.ConnectionId, out _),
            $"The targetless summon must expire before any attack on its twentieth Combat Type 24 timer opportunity (outcomes=[{expiryDescription}]).");
    }

    private static void CombatRouteTypeFiveSummonAttacksAtNineteenAndTeleportsAtTwenty()
    {
        var codec = LegacyFrameCodec.CreateDefault();
        var near = CreateCombatRouteTypeFiveSummonFixture(summonerOffset: 18);
        var nearAccepted = near.Hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var nearOpportunities);
        var nearDescription = string.Join(",", nearOpportunities.Select(static opportunity =>
            $"move={opportunity.Movement?.Movement is not null},attack={opportunity.Attack?.TargetConnectionId}:{opportunity.Attack?.Damage}"));
        Check(nearAccepted &&
              nearOpportunities is [{ Movement: null, Attack: { Damage: > 0 } }] &&
              nearOpportunities[0].Attack!.TargetConnectionId == near.TargetConnectionId,
            $"At legacy distance 19, a RouteType 5 summon must keep its physical attack opportunity (route={near.Summon.RouteType}, target={near.TargetConnectionId}, outcomes={nearDescription}).");
        Check(near.Hub.TryGetCharacterSnapshot(near.TargetConnectionId, out var nearTargetMob, out _, out _) &&
              nearTargetMob is not null && LegacyMobCombatState.Read(nearTargetMob).CurrentScore.Hp < 100_000,
            "The distance-19 attack must apply damage to its selected target.");

        var far = CreateCombatRouteTypeFiveSummonFixture(summonerOffset: 19);
        Check(far.Hub.TryApplyAllSummonBattleOpportunities(
                  randomFactor: 100,
                  out var farOpportunities) &&
              farOpportunities is [{ Movement: { Status: LegacyNpcAiApplyStatus.Applied, Movement: { Effect: 1, Speed: 2 } }, Attack: null }] &&
              far.Hub.TryGetSummonTarget(far.Summon.ConnectionId, out var selectedTarget) && selectedTarget == far.TargetConnectionId,
            "At legacy distance 20, a RouteType 5 summon must teleport with Effect 1 and Speed 2 instead of attacking.");
        var movementOutcome = farOpportunities.Single().Movement!;
        var movement = movementOutcome.Movement!;
        Check(movement.ConnectionId == far.Summon.ConnectionId &&
              Math.Max(Math.Abs(far.SummonerX - movement.ToX), Math.Abs(far.Summon.PositionY - movement.ToY)) <= 1,
            "The combat teleport must use a free tile adjacent to the active party-member summoner.");
        Check(far.Hub.TryGetCharacterSnapshot(far.TargetConnectionId, out var farTargetMob, out _, out _) &&
              farTargetMob is not null && LegacyMobCombatState.Read(farTargetMob).CurrentScore.Hp == 100_000,
            "A summon that teleports at distance 20 must defer its attack until the next battle opportunity.");

        LegacyMobAiRelay.RelayAsync(
            far.Hub,
            movementOutcome,
            codec,
            sendResurrectionRefresh: null,
            CancellationToken.None,
            clientTick: 1,
            keywordIndex: 0).AsTask().GetAwaiter().GetResult();

        var originFrames = far.OriginFrames.Select(frame => codec.Decode(frame)).ToArray();
        var destinationFrames = far.DestinationFrames.Select(frame => codec.Decode(frame)).ToArray();
        var outsideFrames = far.OutsideFrames.Select(frame => codec.Decode(frame)).ToArray();
        Check(originFrames.Length == 2 &&
              originFrames[0].Header.Type == ActionRequest.MessageType &&
              ActionRequest.TryParse(originFrames[0], out var originAction) &&
              originAction is { Effect: 1, Speed: 2 } && originAction.TargetX == movement.ToX && originAction.TargetY == movement.ToY &&
              originFrames[1].Header.Type == RemoveMobConfirmation.MessageType &&
              originFrames[1].Header.Id == movement.ConnectionId,
            "The combat teleport must send Action before RemoveMob to viewers leaving the captured origin view.");
        Check(destinationFrames.Length == 2 &&
              destinationFrames[0].Header.Type == CreateMobConfirmationV769.MessageType &&
              destinationFrames[1].Header.Type == ActionRequest.MessageType &&
              ActionRequest.TryParse(destinationFrames[1], out var destinationAction) &&
              destinationAction is { Effect: 1, Speed: 2 } && destinationAction.TargetX == movement.ToX && destinationAction.TargetY == movement.ToY,
            "The combat teleport must send CreateMob before Action to viewers entering the captured destination view.");
        Check(outsideFrames.Length == 0,
            "A viewer outside both captured teleport views must receive no movement frames.");

        Check(far.Hub.UpdatePosition(
                  far.TargetConnectionId,
                  checked((short)(movement.ToX + 1)),
                  movement.ToY),
            "The selected hostile target could not be moved into attack range for the next battle opportunity.");
        Check(far.Hub.TryApplyAllSummonBattleOpportunities(
                  randomFactor: 100,
                  out var deferredOpportunities) &&
              deferredOpportunities is [{ Movement: null, Attack: { Damage: > 0 } }] &&
              deferredOpportunities[0].Attack!.TargetConnectionId == far.TargetConnectionId,
            "A teleport must defer its attack only for the current tick; the summon must attack on the next opportunity once the target remains selectable.");
    }

    private static void CombatRouteTypeFiveSummonDoesNotAttackWhenTeleportGridIsBlocked()
    {
        var fixture = CreateCombatRouteTypeFiveSummonFixture(summonerOffset: 19, blockTeleportRadius: true);

        Check(fixture.Hub.TryApplyAllSummonBattleOpportunities(
                  randomFactor: 100,
                  out var opportunities) && opportunities.Count == 0,
            "When every GetEmptyMobGrid cell through radius 9 is blocked, the summon must consume the opportunity without moving or attacking.");
        Check(fixture.Hub.TryGetSummonTarget(fixture.Summon.ConnectionId, out var selectedTarget) && selectedTarget == fixture.TargetConnectionId &&
              fixture.Hub.TrySelectSummonTarget(fixture.Summon.ConnectionId, out var retainedSelection) &&
              retainedSelection is { TargetConnectionId: var retainedTarget, Distance: 1 } && retainedTarget == fixture.TargetConnectionId &&
              fixture.Hub.TryGetCharacterSnapshot(fixture.TargetConnectionId, out var targetMob, out _, out _) &&
              targetMob is not null && LegacyMobCombatState.Read(targetMob).CurrentScore.Hp == 100_000,
            "A blocked combat teleport must keep the summon in place, retain its adjacent target and leave HP unchanged instead of falling through to attack.");
    }

    private static void RouteTypeFiveGeneratedNpcWithInvalidLeaderDespawnRunsOnCombatShard()
    {
        var mob = CreateMob("ROUTE5_INVALID", hp: 1_000, clan: 2, emptySkills: true);
        var catalog = LegacyNpcGenerationCatalog.Parse("""
            #[42]
            MaxNumMob: 10
            Leader: ROUTE5_INVALID
            RouteType: 5
            StartX: 10
            StartY: 10
            StartRange: 4
            StartWait: 0
            Segment1X: 20
            Segment1Y: 10
            DestX: 30
            DestY: 10
            """, new Dictionary<string, byte[]> { ["ROUTE5_INVALID"] = mob });
        var hub = new WorldHub(npcGenerationCatalog: catalog);
        AddPlayer(hub, 1, "ROUTE5_TARGET", 11, 10, hp: 100_000);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "The generated RouteType 5 invalid-leader fixture did not spawn.");
        Check(npc!.LeaderId == 0 &&
              hub.SetNpcCombatState(npc.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "The generated RouteType 5 fixture must enter combat with its source-invalid Leader value.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var randomDraws = 0;
        var runtime = new LegacyMobAiRuntime(hub, itemData: null, (outcome, _) =>
        {
            outcomes.Add(outcome);
            return ValueTask.CompletedTask;
        }, nextRoll: _ =>
        {
            randomDraws++;
            return 0;
        });
        var tick = runtime.TickShardAsync(npc.ConnectionId % LegacyMobAiRuntime.ShardCount)
            .AsTask().GetAwaiter().GetResult();

        Check(tick.VisitedNpcIds.Contains(npc.ConnectionId) && randomDraws == 0 && outcomes is
                  [{ DecisionKind: LegacyMobAiDecisionKind.Despawn, Removal: { RemoveType: 3 } removal }] &&
              removal.ConnectionId == npc.ConnectionId &&
              !hub.TryGetNpcSnapshot(npc.ConnectionId, out _),
            $"A RouteType 5 generated NPC with invalid Leader must reach BattleProcessor's RemoveMob(type=3) before combat RNG; draws={randomDraws}, visited={string.Join(',', tick.VisitedNpcIds)}, outcomes={outcomes.Count}.");
    }

    private static void RunequestLv6AreaDespawnRunsBeforePeaceAndCombatRandomness()
    {
        static void Verify(bool combat, int routeType, byte clan)
        {
            const int generateIndex = 4284;
            const short x = 3423;
            const short y = 1492;
            var hub = NewHub(allEmptySkills: false, generateIndex: generateIndex, npcClan: clan, routeType: routeType);
            AddPlayer(hub, 1, "PSTA", x, checked((short)(y + 1)), hp: 100_000);
            Check(hub.ConfigurePistaLv6State(1, mobCount: 1), "Could not configure the runtime Pista Lv6 counter.");
            Check(hub.TrySpawnGeneratedNpc(generateIndex, out var npc, randomRoll: static _ => 0,
                    positionX: x, positionY: y, forceGenerationRoutePosition: true) && npc is not null,
                "The runtime Runequest Lv6 NPC could not spawn in the cleanup area.");
            if (combat)
                Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [0]),
                    "Could not set the area-cleanup NPC to targetless Combat.");

            var outcomes = new List<LegacyNpcAiApplicationOutcome>();
            var randomDraws = 0;
            var runtime = new LegacyMobAiRuntime(hub, itemData: null, (outcome, _) =>
            {
                outcomes.Add(outcome);
                return ValueTask.CompletedTask;
            }, nextRoll: _ =>
            {
                randomDraws++;
                return 0;
            });

            if (combat)
            {
                var tick = runtime.TickShardAsync(npc!.ConnectionId % LegacyMobAiRuntime.ShardCount)
                    .AsTask().GetAwaiter().GetResult();
                Check(tick.VisitedNpcIds.Contains(npc.ConnectionId),
                    "The combat shard skipped a Runequest Lv6 area cleanup before target selection.");
            }
            else
            {
                for (var callback = 0; callback < LegacyMobAiRuntime.PeaceShardCount && outcomes.Count == 0; callback++)
                    runtime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
            }

            Check(randomDraws == 0 && outcomes is
                    [{ DecisionKind: LegacyMobAiDecisionKind.Despawn, Removal: { RemoveType: 3 } removal }] &&
                  removal.ConnectionId == npc!.ConnectionId &&
                  !hub.TryGetNpcSnapshot(npc.ConnectionId, out _) &&
                  hub.TryGetPistaLv6State(out var pistaState) && pistaState?.MobCount == 2,
                $"Runequest Lv6 area cleanup must remove before Peace/Combat random processing and increment the second-wave counter; combat={combat}, route={routeType}, clan={clan}, randomDraws={randomDraws}, outcomes={outcomes.Count}.");
        }

        Verify(combat: false, routeType: 0, clan: 2);
        Verify(combat: true, routeType: 5, clan: 4);
    }

    private static void RunequestLv6AreaDespawnRemovesRouteTypeFiveSummonsInBothModes()
    {
        static (WorldHub Hub, LegacySummonedMob Summon) CreateAreaSummon(
            Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> send)
        {
            var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
            AddSummonOwner(hub, 1, "PSTA_OWNER", 3432, 1501, 30, send);
            Check(hub.ConfigurePistaLv6State(1, mobCount: 7), "Could not configure the summon Pista counter fixture.");
            var skill = new LegacySkillDefinition(50, 20, 0, 20, 0, 0, 11, 1, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "Evocar");
            Check(hub.TryApplySkillAttack(1, 1, skill, itemData: null, weather: 0, randomFactor: 90, out var result) ==
                  LegacySkillAttackResult.Accepted && result?.SummonedMobs is { Count: 1 },
                "The Runequest Lv6 Type 11 summon could not be created.");
            var summon = result!.SummonedMobs!.Single();
            Check(summon.RouteType == 5 && summon.PositionX is >= 3423 and <= 3442 && summon.PositionY is >= 1492 and <= 1511,
                $"The Type 11 summon did not spawn inside the Runequest Lv6 cleanup area: {summon.PositionX}:{summon.PositionY}.");
            return (hub, summon);
        }

        var peaceFrames = new List<byte[]>();
        var peace = CreateAreaSummon((frame, _) =>
        {
            peaceFrames.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
        var peaceOutcomes = new List<LegacyNpcAiApplicationOutcome>();
        var codec = LegacyFrameCodec.CreateDefault();
        var peaceRuntime = new LegacyMobAiRuntime(peace.Hub, itemData: null, (outcome, cancellationToken) =>
        {
            peaceOutcomes.Add(outcome);
            return LegacyMobAiRelay.RelayAsync(peace.Hub, outcome, codec,
                sendResurrectionRefresh: null, cancellationToken, clientTick: 1, keywordIndex: 0);
        });
        for (var callback = 0; callback < LegacyMobAiRuntime.PeaceShardCount && peaceOutcomes.Count == 0; callback++)
            peaceRuntime.TickNextShardAsync().AsTask().GetAwaiter().GetResult();
        Check(peaceOutcomes is [{ DecisionKind: LegacyMobAiDecisionKind.Despawn, Removal: { RemoveType: 3 } removal }] &&
              removal.ConnectionId == peace.Summon.ConnectionId && peaceFrames.Count == 1 &&
              codec.Decode(peaceFrames[0]).Header.Type == RemoveMobConfirmation.MessageType &&
              peace.Hub.TryGetPistaLv6State(out var peacePista) && peacePista?.MobCount == 7,
            "A peaceful Type 11 summon inside the Runequest Lv6 area must be removed with type 3 without incrementing a generated-NPC counter.");

        var combatFrames = new List<byte[]>();
        var combat = CreateAreaSummon((frame, _) =>
        {
            combatFrames.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
        Check(combat.Hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var opportunities) &&
              opportunities is [{ Movement: null, Attack: null, Despawn: { DecisionKind: LegacyMobAiDecisionKind.Despawn,
                  Removal: { RemoveType: 3, ViewRecipientConnectionIds.Count: 1 } combatRemoval } }] &&
              combatRemoval.ConnectionId == combat.Summon.ConnectionId,
            "The summon Combat timer must remove an in-area mob before target selection and attack.");
        LegacyMobAiRelay.RelayAsync(
            combat.Hub,
            opportunities.Single().Despawn!,
            LegacyFrameCodec.CreateDefault(),
            sendResurrectionRefresh: null,
            CancellationToken.None,
            clientTick: 1,
            keywordIndex: 0).AsTask().GetAwaiter().GetResult();
        Check(combatFrames.Count == 1 &&
              LegacyFrameCodec.CreateDefault().Decode(combatFrames[0]).Header.Type == RemoveMobConfirmation.MessageType &&
              combat.Hub.TryGetPistaLv6State(out var combatPista) && combatPista?.MobCount == 7,
            "A combat Type 11 summon removal must relay to the captured view and leave generated-NPC counters unchanged.");
    }

    private static CombatRouteTypeFiveSummonFixture CreateCombatRouteTypeFiveSummonFixture(int summonerOffset, bool blockTeleportRadius = false)
    {
        LegacyMapGrid? map = null;
        if (blockTeleportRadius)
        {
            var heightMap = new byte[LegacyMapGrid.HeightMapSize];
            var attributeMap = new byte[LegacyMapGrid.AttributeMapSize];
            var expectedSummonX = 99;
            var expectedSummonY = 99;
            var blockedCenterX = expectedSummonX + summonerOffset;
            var blockedCenterY = expectedSummonY;
            for (var y = blockedCenterY - 9; y <= blockedCenterY + 9; y++)
            for (var x = blockedCenterX - 9; x <= blockedCenterX + 9; x++)
                heightMap[(y * LegacyMapGrid.HeightWidth) + x] = 127;

            // Clan 4 attacks require the legacy walkable map attribute bit at both combat cells.
            attributeMap[((expectedSummonY >> 2) * LegacyMapGrid.AttributeWidth) + (expectedSummonX >> 2)] = 0x40;
            map = new LegacyMapGrid(heightMap, attributeMap);
        }

        var hub = new WorldHub(map, mapCollisionMode: LegacyMapCollisionMode.CandidateAware, summonCatalog: LoadSummonCatalog());
        AddSummonOwner(hub, 1, "COMBAT_LEADER", 90, 100, 0, static (_, _) => ValueTask.CompletedTask);
        AddSummonOwner(hub, 2, "COMBAT_CASTER", 100, 100, 30, static (_, _) => ValueTask.CompletedTask);
        Check(hub.TrySetParty(1, [1, 2]), "The combat RouteType 5 fixture could not form its party.");

        var skill = new LegacySkillDefinition(50, 20, 0, 20, 0, 0, 11, 1, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "Evocar");
        Check(hub.TryApplySkillAttack(2, 2, skill, itemData: null, weather: 0, randomFactor: 90, out var summonOutcome) ==
              LegacySkillAttackResult.Accepted && summonOutcome?.SummonedMobs is { Count: 1 },
            "The combat RouteType 5 fixture could not create a Type 11 summon.");
        var summon = summonOutcome!.SummonedMobs!.Single();
        Check(!blockTeleportRadius || (summon.PositionX, summon.PositionY) == (99, 99),
            "The blocked combat teleport fixture no longer matches the first free summon grid cell.");
        var summonerX = checked((short)(summon.PositionX + summonerOffset));
        Check(hub.UpdatePosition(2, summonerX, summon.PositionY),
            "The party-member summoner could not be moved to the requested combat distance.");
        for (byte clan = 0; clan < 9; clan++)
            AddPlayer(hub, 10 + clan, $"CBT{clan}", checked((short)(summon.PositionX + 1)), summon.PositionY, 100_000, clan);

        Check(hub.TrySelectSummonTarget(summon.ConnectionId, out var selectedTarget) &&
              selectedTarget is { TargetConnectionId: >= 10 and <= 18 },
            "The combat RouteType 5 fixture could not select a hostile player from the legacy clan relation table.");

        var originFrames = new List<byte[]>();
        var destinationFrames = new List<byte[]>();
        var outsideFrames = new List<byte[]>();
        AddViewer(hub, 4, "CB_ORIGIN", checked((short)(summon.PositionX - 16)), summon.PositionY, originFrames);
        AddViewer(hub, 5, "CB_DEST", checked((short)(summonerX + 15)), summon.PositionY, destinationFrames);
        AddViewer(hub, 6, "CB_OUT", checked((short)(summonerX + 40)), summon.PositionY, outsideFrames);
        return new CombatRouteTypeFiveSummonFixture(
            hub,
            summon,
            selectedTarget!.TargetConnectionId,
            summonerX,
            summon.PositionY,
            originFrames,
            destinationFrames,
            outsideFrames);
    }

    private sealed record CombatRouteTypeFiveSummonFixture(
        WorldHub Hub,
        LegacySummonedMob Summon,
        int TargetConnectionId,
        short SummonerX,
        short SummonerY,
        List<byte[]> OriginFrames,
        List<byte[]> DestinationFrames,
        List<byte[]> OutsideFrames);

    private static async Task CancellationAndSingleRuntimeLoopAreDeterministic()
    {
        var runtime = NewRuntime(NewHub(allEmptySkills: true));
        using var cancellation = new CancellationTokenSource();
        var run = runtime.RunAsync(cancellation.Token);
        Check(runtime.ActiveWorkerCount == 1, "The host loop did not start as one runtime worker.");

        var secondRunRejected = false;
        try
        {
            await runtime.RunAsync(cancellation.Token);
        }
        catch (InvalidOperationException)
        {
            secondRunRejected = true;
        }
        Check(secondRunRejected, "A second PeriodicTimer loop must be rejected instead of duplicating shard work.");

        cancellation.Cancel();
        var canceled = false;
        try
        {
            await run;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            canceled = true;
        }
        Check(canceled && runtime.ActiveWorkerCount == 0 && runtime.ActiveTickCount == 0,
            "Cancellation must stop and await the sole runtime loop without leaving an active tick.");
    }

    private static LegacyMobAiRuntime NewRuntime(WorldHub hub, Action<LegacyNpcAiApplicationOutcome>? observed = null)
    {
        return new LegacyMobAiRuntime(hub, itemData: null, (outcome, _) =>
        {
            observed?.Invoke(outcome);
            return ValueTask.CompletedTask;
        }, nextRoll: static _ => 0);
    }

    private static WorldHub NewHub(
        bool allEmptySkills,
        byte attackRun = 0,
        short strength = 0,
        int generateIndex = 42,
        byte npcClan = 2,
        int routeType = 0)
    {
        var mob = CreateMob("Wolf", hp: 10_000, clan: npcClan, emptySkills: allEmptySkills);
        var baseScore = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        (baseScore with { AttackRun = attackRun, Strength = strength }).Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        var currentScore = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        (currentScore with { AttackRun = attackRun, Strength = strength }).Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var catalog = LegacyNpcGenerationCatalog.Parse($"""
            #[{generateIndex}]
            MaxNumMob: 64
            Leader: Wolf
            RouteType: {routeType}
            StartX: 10
            StartY: 10
            StartRange: 4
            StartWait: 0
            Segment1X: 20
            Segment1Y: 10
            DestX: 30
            DestY: 10
            """, new Dictionary<string, byte[]> { ["Wolf"] = mob });
        return new WorldHub(npcGenerationCatalog: catalog);
    }

    private static LegacySummonCatalog LoadSummonCatalog()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Backup", "Tools", "ReferenceSources", "TMProject2GlobalClient", "Servidor", "Server", "TMSrv", "run", "BaseSummon");
            if (Directory.Exists(candidate))
                return LegacySummonCatalog.Load(candidate);
        }

        throw new DirectoryNotFoundException("The TMProject 7.69 BaseSummon fixtures were not found from the test output directory.");
    }

    private static void AddSummonOwner(
        WorldHub hub,
        int connectionId,
        string name,
        short x,
        short y,
        short special3,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> send)
    {
        var mob = CreateMob(name, hp: 1_000, clan: 5, emptySkills: false);
        new LegacyScore(100, 10, 30, 0, 0, 0, 0, 1_000, 1_000, 1_000, 100, 0, 0, 0, 0, 0, 0, special3, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        new LegacyScore(100, 10, 30, 0, 0, 0, 0, 1_000, 1_000, 1_000, 100, 0, 120, 0, 0, 0, 0, special3, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(hub.Enter(connectionId, name, send) &&
              hub.SetCharacterState(connectionId, 0, 0, clan: 5, guildLevel: 0, coin: 0, x, y, mob),
            "The summon owner could not be registered with an active character snapshot.");
    }

    private static void AddViewer(WorldHub hub, int connectionId, string name, short x, short y, List<byte[]> frames)
    {
        Check(hub.Enter(connectionId, name, (frame, _) =>
        {
            frames.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        }) && hub.SetCharacterState(connectionId, 0, 0, clan: 5, guildLevel: 0, coin: 0, x, y,
            CreateMob(name, hp: 1_000, clan: 5, emptySkills: false)),
            "The summon-view fixture participant could not be registered.");
    }

    private static int ReadIntResultProperty(LegacyMobAiTickResult result, string propertyName)
    {
        var property = typeof(LegacyMobAiTickResult).GetProperty(propertyName);
        Check(property?.PropertyType == typeof(int), $"LegacyMobAiTickResult is missing integer diagnostic property {propertyName}.");
        return (int)property!.GetValue(result)!;
    }

    private static int[] ReadNpcIdResultProperty(LegacyMobAiTickResult result, string propertyName)
    {
        var property = typeof(LegacyMobAiTickResult).GetProperty(propertyName);
        Check(property?.GetValue(result) is IReadOnlyList<int>, $"LegacyMobAiTickResult is missing NPC ID diagnostic property {propertyName}.");
        return ((IReadOnlyList<int>)property!.GetValue(result)!).ToArray();
    }

    private static int[] SpawnNpcs(WorldHub hub, int count)
    {
        var ids = new int[count];
        for (var index = 0; index < count; index++)
        {
            Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
                $"Generated NPC fixture {index} did not spawn.");
            ids[index] = npc!.ConnectionId;
        }
        return ids;
    }

    private static void AddPlayer(WorldHub hub, int id, string name, short x, short y, int hp, byte clan = 5)
    {
        Check(hub.Enter(id, name, static (_, _) => ValueTask.CompletedTask), "Player Enter failed.");
        Check(hub.SetCharacterState(id, 0, 0, clan: clan, guildLevel: 0, coin: 0, x, y,
            CreateMob(name, hp, clan: clan, emptySkills: false)), "Player character snapshot setup failed.");
    }

    private static byte[] CreateMob(string name, int hp, byte clan, bool emptySkills)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset, 16));
        mob[LegacyAccountSnapshot.MobClanOffset] = clan;
        new LegacyScore(100, 10, 1000, 0, 0, 0, 0, Math.Max(1000, hp), 1000, hp, 1000, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        new LegacyScore(100, 10, 1000, 0, 0, 0, 0, Math.Max(1000, hp), 1000, hp, 1000, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        if (emptySkills)
            mob.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4).Fill(byte.MaxValue);
        return mob;
    }

    private static byte[] CreateAffectSnapshot(byte type, byte value, ushort level, uint time)
    {
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = type;
        affect[1] = value;
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(2), level);
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), time);
        return affect;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

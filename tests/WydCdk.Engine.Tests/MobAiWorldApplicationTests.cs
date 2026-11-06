using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class MobAiWorldApplicationTests
{
    public static void Run()
    {
        GeneratedSnapshotsAreDefensiveAndSpawnKeepsRouteState();
        WorldHubAppliesRouteMoveWaitAndReturnDecisions();
        MerchantActivationWaitsForTypeZeroRouteEnd();
        RouteTypeThreeTerminalDespawnUpdatesPistaAndCapturesView();
        RunequestLv6AreaDespawnUpdatesOnlyItsSourceCounterRangeAndCapturesView();
        RunequestLv2PeaceMovementDespawnUsesPostMoveTileAndView();
        RouteTypeFiveCombatDespawnCapturesView();
        RangedNpcRetreatAppliesMovementAndCapturesRecipients();
        RetreatWithoutAValidRouteDoesNotInventMovement();
        PassiveGeneratedNpcLeaderAggroFansOutToItsFollowers();
        PassiveGeneratedNpcAggroAgainstSummonDoesNotFanOutToTargetPartySummons();
        GeneratedNpcClanFourReadsBaseSpecialFromMobSnapshot();
        StaleGeneratedNpcChasePlanIsRejectedAfterTargetMoves();
        StalePlansAreRejectedAndPhysicalAttackUpdatesAuthoritativeHp();
        AttackSnapshotIsRejectedWhenTargetMoves();
        PhysicalAttackUsesDamageRollRangeAndMissMarker();
        PhysicalAttackSetsAuthoritativeHpToZero();
        PhysicalAttackRunsPlayerResurrectionFinalizer();
        NpcsWithoutPlayingPlayerTargetsDoNotAttack();
        PhysicalRetaliationRunsOnlyAfterEligibleSourceTail();
        SkillRetaliationRunsOnlyAfterEligibleSourceTail();
        PlannerSnapshotUsesMerchantAndGridOccupancy();
    }

    private static void RouteTypeThreeTerminalDespawnUpdatesPistaAndCapturesView()
    {
        static void Verify(int generateIndex, int initialMobCount, int expectedMobCount)
        {
            var hub = NewHub(routeType: 3, startWait: 0, generateIndex: generateIndex, terminalRoute: true);
            AddPlayer(hub, 1, "PISTA_VIEWER", 10, 11, clan: 5);
            Check(hub.ConfigurePistaLv6State(1, initialMobCount), "Could not configure the Pista level 6 count fixture.");
            Check(hub.TrySpawnGeneratedNpc(generateIndex, out var npc, randomRoll: static _ => 0,
                    positionX: 10, positionY: 10, forceGenerationRoutePosition: true) && npc is not null,
                $"RouteType 3 generated NPC fixture {generateIndex} did not spawn.");
            var spawnedNpc = npc!;

            for (var expectedProgress = 1; expectedProgress <= 4; expectedProgress++)
            {
                Check(hub.TryCreateGeneratedNpcAiSnapshot(spawnedNpc.ConnectionId, attackRange: 0, routeStep: null,
                        randomIntRoll: 0, dexterityRoll: 0, out var advanceSnapshot) && advanceSnapshot is not null,
                    "WorldHub did not capture an intermediate RouteType 3 transition.");
                var advance = LegacyMobAiPlanner.Plan(advanceSnapshot!.PlannerInput);
                var applyStatus = hub.TryApplyGeneratedNpcAiDecision(advanceSnapshot, advance, out _);
                Check(advance.Kind == LegacyMobAiDecisionKind.Idle && advance.SegmentProgress == expectedProgress &&
                      applyStatus == LegacyNpcAiApplyStatus.Applied,
                    $"RouteType 3 did not advance the collapsed fixture route to point {expectedProgress}; kind={advance.Kind}, progress={advance.SegmentProgress}, wait={advance.WaitSec}, segment={advance.SegmentX}:{advance.SegmentY}, mob={advanceSnapshot.PlannerInput.Mob.X}:{advanceSnapshot.PlannerInput.Mob.Y}/{advanceSnapshot.PlannerInput.Mob.SegmentProgress}:{advanceSnapshot.PlannerInput.Mob.SegmentX}:{advanceSnapshot.PlannerInput.Mob.SegmentY}, apply={applyStatus}.");
            }

            Check(hub.TryCreateGeneratedNpcAiSnapshot(spawnedNpc.ConnectionId, attackRange: 0, routeStep: null,
                    randomIntRoll: 0, dexterityRoll: 0, out var terminalSnapshot) && terminalSnapshot is not null,
                "WorldHub did not capture the RouteType 3 terminal state.");
            var terminal = LegacyMobAiPlanner.Plan(terminalSnapshot!.PlannerInput);
            Check(terminal.Kind == LegacyMobAiDecisionKind.Despawn,
                "The RouteType 3 terminal state did not plan an explicit despawn.");

            var status = hub.TryApplyGeneratedNpcAiDecision(terminalSnapshot, terminal, out var outcome);
            var hasPistaState = hub.TryGetPistaLv6State(out var pistaState);
            Check(status == LegacyNpcAiApplyStatus.Applied && outcome.Removal is
                      { RemoveType: 3, ViewRecipientConnectionIds.Count: 1 } removal &&
                  removal.ConnectionId == spawnedNpc.ConnectionId && removal.ViewRecipientConnectionIds.SequenceEqual([1]) &&
                  !hub.TryGetNpcSnapshot(spawnedNpc.ConnectionId, out _) &&
                  hasPistaState && pistaState?.MobCount == expectedMobCount,
                $"RouteType 3 terminal despawn did not remove/recount/relay atomically; generate={generateIndex}, count={initialMobCount}->{pistaState?.MobCount}, status={status}.");
        }

        Verify(generateIndex: 4291, initialMobCount: 1, expectedMobCount: 2);
        Verify(generateIndex: 4301, initialMobCount: 99, expectedMobCount: 100);
        Verify(generateIndex: 4291, initialMobCount: 0, expectedMobCount: 0);
        Verify(generateIndex: 4291, initialMobCount: 100, expectedMobCount: 100);
        Verify(generateIndex: 42, initialMobCount: 12, expectedMobCount: 12);
    }

    private static void RouteTypeFiveCombatDespawnCapturesView()
    {
        var hub = NewHub(routeType: 5, startWait: 0, generateIndex: 4291);
        AddPlayer(hub, 1, "ROUTE5_VIEWER", 11, 10, clan: 5);
        Check(hub.ConfigurePistaLv6State(1, mobCount: 7), "Could not configure the RouteType 5 counter-isolation fixture.");
        Check(hub.TrySpawnGeneratedNpc(4291, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "RouteType 5 combat NPC fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed RouteType 5 combat target.");
        Check(hub.TryCreateGeneratedNpcAiSnapshot(npc.ConnectionId, attackRange: 4, routeStep: null,
                randomIntRoll: 0, dexterityRoll: 0, out var snapshot) && snapshot is not null,
            "WorldHub did not capture the RouteType 5 invalid-leader combat state.");

        var decision = LegacyMobAiPlanner.Plan(snapshot!.PlannerInput);
        var status = hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var outcome);
        Check(decision.Kind == LegacyMobAiDecisionKind.Despawn &&
              status == LegacyNpcAiApplyStatus.Applied &&
              outcome.Removal is { RemoveType: 3, ViewRecipientConnectionIds.Count: 1 } removal &&
              removal.ConnectionId == npc.ConnectionId && removal.ViewRecipientConnectionIds.SequenceEqual([1]) &&
              !hub.TryGetNpcSnapshot(npc.ConnectionId, out _) &&
              hub.TryGetPistaLv6State(out var pistaState) && pistaState?.MobCount == 7,
            $"RouteType 5 invalid-leader combat must remove with type 3 and capture the origin view; decision={decision.Kind}, apply={status}.");
    }

    private static void RunequestLv2PeaceMovementDespawnUsesPostMoveTileAndView()
    {
        static void Verify(int generateIndex, short fromX, short fromY, short toX, short toY, bool expectRemoval)
        {
            var npcMob = CreateMob("Wolf", hp: 1000);
            var baseScore = LegacyScore.Read(npcMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset)) with
            {
                AttackRun = 1,
                Strength = 10
            };
            baseScore.Write(npcMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
            var currentScore = LegacyScore.Read(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with { AttackRun = 1 };
            currentScore.Write(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
            var catalog = LegacyNpcGenerationCatalog.Parse($"""
                #[{generateIndex}]
                MaxNumMob: 1
                Leader: Wolf
                RouteType: 0
                StartX: {toX}
                StartY: {toY}
                StartRange: 0
                StartWait: 0
                Segment1X: {toX + 2}
                Segment1Y: {toY}
                DestX: {toX + 4}
                DestY: {toY}
                """, new Dictionary<string, byte[]> { ["Wolf"] = npcMob });
            var hub = new WorldHub(npcGenerationCatalog: catalog, skillData: MobAiSkillDataFixture.Create());
            AddPlayer(hub, 1, "LV2_SHARED", toX, checked((short)(toY + 1)), clan: 2);
            AddPlayer(hub, 2, "LV2_ENTER", checked((short)(toX + 16)), toY, clan: 2);
            AddPlayer(hub, 3, "LV2_LEAVE", checked((short)(fromX - 16)), fromY, clan: 2);
            AddPlayer(hub, 4, "LV2_OUT", checked((short)(toX + 17)), toY, clan: 2);
            Check(hub.TrySpawnGeneratedNpc(generateIndex, out var npc, randomRoll: static _ => 0,
                    positionX: fromX, positionY: fromY) && npc is not null,
                $"Pista Lv2 generated NPC {generateIndex} did not spawn before its exit step.");
            Check(hub.TryCreateGeneratedNpcAiPeaceSnapshot(npc!.ConnectionId, static () => 2, out var snapshot) && snapshot is not null,
                "WorldHub did not capture the peaceful Pista Lv2 route step.");

            var decision = LegacyMobAiPlanner.Plan(snapshot!.PlannerInput);
            var status = hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var outcome);
            var movement = outcome.Movement;
            var plannerMob = snapshot.PlannerInput.Mob;
            var attackRun = LegacyScore.Read(snapshot.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset)).AttackRun;
            Check(decision.Kind == LegacyMobAiDecisionKind.Move && status == LegacyNpcAiApplyStatus.Applied &&
                  movement is { FromX: var actualFromX, FromY: var actualFromY,
                      ToX: var actualToX, ToY: var actualToY } &&
                  actualFromX == fromX && actualFromY == fromY && actualToX == toX && actualToY == toY,
                $"Pista Lv2 fixture did not complete the expected one-tile peace movement; generate={generateIndex}, kind={decision.Kind}, status={status}, movement={movement?.FromX}:{movement?.FromY}->{movement?.ToX}:{movement?.ToY}, mob={plannerMob.X}:{plannerMob.Y}, segment={plannerMob.SegmentX}:{plannerMob.SegmentY}, step={snapshot.PlannerInput.RouteStep?.X}:{snapshot.PlannerInput.RouteStep?.Y}, target={decision.TargetX}:{decision.TargetY}, attackRun={attackRun}.");

            if (expectRemoval)
            {
                Check(outcome.Removal is { RemoveType: 3, ViewRecipientConnectionIds.Count: 2 } removal &&
                      removal.ConnectionId == npc.ConnectionId && removal.ViewRecipientConnectionIds.Order().SequenceEqual([1, 2]) &&
                      movement!.OriginViewConnectionIds.Order().SequenceEqual([1, 3]) &&
                      movement.DestinationViewConnectionIds.Order().SequenceEqual([1, 2]) &&
                      !hub.TryGetNpcSnapshot(npc.ConnectionId, out _),
                    $"Pista Lv2 movement into the exit rectangle did not atomically remove the NPC for post-move viewers; generate={generateIndex}.");
            }
            else
            {
                Check(outcome.Removal is null && hub.TryGetNpcSnapshot(npc.ConnectionId, out var stillActive) &&
                      stillActive is { PositionX: var actualX, PositionY: var actualY } && actualX == toX && actualY == toY,
                    $"Pista Lv2 movement despawned outside its generator or area bounds; generate={generateIndex}, destination={toX}:{toY}.");
            }
        }

        Verify(generateIndex: 4306, fromX: 3372, fromY: 1420, toX: 3373, toY: 1420, expectRemoval: true);
        Verify(generateIndex: 4364, fromX: 3406, fromY: 1453, toX: 3407, toY: 1453, expectRemoval: true);
        Verify(generateIndex: 4305, fromX: 3372, fromY: 1420, toX: 3373, toY: 1420, expectRemoval: false);
        Verify(generateIndex: 4365, fromX: 3406, fromY: 1453, toX: 3407, toY: 1453, expectRemoval: false);
        Verify(generateIndex: 4306, fromX: 3372, fromY: 1419, toX: 3373, toY: 1419, expectRemoval: false);
    }

    private static void RunequestLv6AreaDespawnUpdatesOnlyItsSourceCounterRangeAndCapturesView()
    {
        static void Verify(int generateIndex, int mode, int initialCount, int expectedCount, bool expectRemoval)
        {
            const short npcX = 3423;
            const short npcY = 1492;
            var hub = NewHub(routeType: 0, startWait: 0, generateIndex: generateIndex);
            AddPlayer(hub, 1, "PSTA_TARGET", npcX, checked((short)(npcY + 1)), clan: 5);
            AddPlayer(hub, 2, "PSTA_BOUNDARY", checked((short)(npcX + 16)), checked((short)(npcY + 16)), clan: 5);
            AddPlayer(hub, 3, "PSTA_OUTSIDE", checked((short)(npcX + 17)), checked((short)(npcY + 16)), clan: 5);
            Check(hub.ConfigurePistaLv6State(1, initialCount), "Could not configure the Pista Lv6 area-cleanup counter.");
            Check(hub.TrySpawnGeneratedNpc(generateIndex, out var npc, randomRoll: static _ => 0,
                    positionX: npcX, positionY: npcY, forceGenerationRoutePosition: true) && npc is not null,
                $"Runequest Lv6 generated NPC {generateIndex} did not spawn in the cleanup area.");
            if (mode == LegacyNpcMode.Combat)
                Check(hub.SetNpcCombatState(npc!.ConnectionId, mode, currentTarget: 0, [0]),
                    "Could not set the area-cleanup NPC to Combat with no selected target.");

            Check(hub.TryCreateGeneratedNpcAiSnapshot(npc!.ConnectionId, attackRange: 0, routeStep: null,
                    randomIntRoll: 0, dexterityRoll: 0, out var snapshot) && snapshot is not null,
                "WorldHub did not capture the Runequest Lv6 area-cleanup state.");
            var decision = LegacyMobAiPlanner.Plan(snapshot!.PlannerInput);
            var status = hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var outcome);
            var hasState = hub.TryGetPistaLv6State(out var pistaState);

            if (expectRemoval)
            {
                Check(decision.Kind == LegacyMobAiDecisionKind.Despawn && status == LegacyNpcAiApplyStatus.Applied &&
                      outcome.Removal is { RemoveType: 3, ViewRecipientConnectionIds.Count: 2 } removal &&
                      removal.ConnectionId == npc.ConnectionId && removal.ViewRecipientConnectionIds.SequenceEqual([1, 2]) &&
                      !hub.TryGetNpcSnapshot(npc.ConnectionId, out _) && hasState && pistaState?.MobCount == expectedCount,
                    $"Runequest Lv6 area cleanup failed to remove/recount/relay atomically for generator {generateIndex}; mode={mode}, count={pistaState?.MobCount}, decision={decision.Kind}, apply={status}.");
            }
            else
            {
                Check(decision.Kind != LegacyMobAiDecisionKind.Despawn && status == LegacyNpcAiApplyStatus.Applied &&
                      hub.TryGetNpcSnapshot(npc.ConnectionId, out _) && hasState && pistaState?.MobCount == expectedCount,
                    $"The excluded Runequest Lv6 boss generator {generateIndex} must remain and not change the event count.");
            }
        }

        Verify(generateIndex: 4284, LegacyNpcMode.Peace, initialCount: 1, expectedCount: 2, expectRemoval: true);
        Verify(generateIndex: 4290, LegacyNpcMode.Combat, initialCount: 99, expectedCount: 100, expectRemoval: true);
        Verify(generateIndex: 4291, LegacyNpcMode.Peace, initialCount: 9, expectedCount: 9, expectRemoval: true);
        Verify(generateIndex: 4283, LegacyNpcMode.Peace, initialCount: 9, expectedCount: 9, expectRemoval: false);
    }

    private static void GeneratedNpcClanFourReadsBaseSpecialFromMobSnapshot()
    {
        var hub = NewHub(
            routeType: 0,
            startWait: 0,
            npcSkillBar: [255, 255, 255, 255],
            npcClan: 4,
            npcBaseSpecial: [121, 0, 128, 0]);
        AddPlayer(hub, 1, "CLAN4_TARGET", 11, 10, clan: 5);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "The generated Clan 4 mob fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed the generated Clan 4 target.");
        Check(hub.TryCreateGeneratedNpcAiSnapshot(npc.ConnectionId, 4, routeStep: null,
                randomIntRoll: 0, dexterityRoll: 0, out var snapshot,
                skillBarSelectionRoll: 99) && snapshot is not null,
            "WorldHub did not capture the generated Clan 4 planner input.");

        var plannerInput = snapshot!.PlannerInput;
        var copiedSpecial = plannerInput.Mob.BaseSpecial as short[]
            ?? throw new InvalidOperationException("WorldHub did not expose the captured BaseScore.Special as an isolated snapshot array.");
        copiedSpecial[0] = 0;
        plannerInput = snapshot.PlannerInput;
        var decision = LegacyMobAiPlanner.Plan(plannerInput);
        Check(plannerInput.Mob.BaseSpecial.SequenceEqual(new short[] { 121, 0, 128, 0 }) &&
              decision.Kind == LegacyMobAiDecisionKind.Attack &&
              decision.Attack is { SkillIndex: 103, SkillParm: 5, Motion: 0, Resist: -1 },
            $"The generated NPC path did not propagate BaseScore.Special to GetAttack; decision={decision.Kind}/{decision.Attack}.");
    }

    private static void RangedNpcRetreatAppliesMovementAndCapturesRecipients()
    {
        var hub = NewHub(routeType: 0, startWait: 0, allEmptySkills: true,
            npcAttackRun: 4, npcBaseDexterity: 10);
        AddPlayer(hub, 1, "RETREAT_TARGET", 11, 10, clan: 5);
        AddPlayer(hub, 2, "RETREAT_EDGE", 26, 10, clan: 3);
        AddPlayer(hub, 3, "RETREAT_OUTSIDE", 27, 10, clan: 3);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "The ranged retreat NPC fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed the ranged retreat target.");
        Check(hub.TryCreateGeneratedNpcAiChaseSnapshot(
                npc.ConnectionId,
                attackRange: 4,
                randomIntRoll: 0,
                dexterityRoll: 11,
                randomFallbackOffset: static () => -3,
                randomReturnSeedRoll: static () => 0,
                out var snapshot,
                randomRetreatStepRoll: static () => 0) && snapshot is not null,
            "WorldHub did not create the ranged retreat snapshot.");

        var decision = LegacyMobAiPlanner.Plan(snapshot!.PlannerInput);
        var status = hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var outcome);
        Check(decision.Kind == LegacyMobAiDecisionKind.Retreat && status == LegacyNpcAiApplyStatus.Applied &&
              outcome.Movement is { FromX: 10, FromY: 10, ToX: 9, ToY: 10, Effect: 0 } movement &&
              movement.OriginViewConnectionIds.Order().SequenceEqual([1, 2]) &&
              movement.DestinationViewConnectionIds.SequenceEqual([1]) &&
              hub.TryGetNpcSnapshot(npc.ConnectionId, out var moved) && moved is { PositionX: 9, PositionY: 10 },
            $"A close ranged NPC's failed DEX roll did not move away and capture the old/new views; kind={decision.Kind}, status={status}, move={outcome.Movement?.FromX}:{outcome.Movement?.FromY}->{outcome.Movement?.ToX}:{outcome.Movement?.ToY}.");
    }

    private static void RetreatWithoutAValidRouteDoesNotInventMovement()
    {
        const int npcX = 10;
        const int npcY = 10;
        var height = new byte[LegacyMapGrid.HeightMapSize];
        for (var y = npcY - 1; y <= npcY + 1; y++)
        for (var x = npcX - 1; x <= npcX + 1; x++)
        {
            if (x == npcX && y == npcY)
                continue;
            height[(y * LegacyMapGrid.HeightWidth) + x] = 127;
        }

        var mapGrid = new LegacyMapGrid(height, new byte[LegacyMapGrid.AttributeMapSize]);
        var hub = NewHub(routeType: 0, startWait: 0, allEmptySkills: true,
            npcAttackRun: 4, npcBaseDexterity: 10, mapGrid: mapGrid);
        AddPlayer(hub, 1, "BLOCKED_RETREAT", 11, 10, clan: 5);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0,
                positionX: npcX, positionY: npcY) && npc is not null,
            "The blocked retreat NPC fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed the blocked retreat target.");

        var fallbackCalls = 0;
        var hasSnapshot = hub.TryCreateGeneratedNpcAiChaseSnapshot(
            npc.ConnectionId,
            attackRange: 4,
            randomIntRoll: 0,
            dexterityRoll: 11,
            randomFallbackOffset: () =>
            {
                fallbackCalls++;
                return -3;
            },
            randomReturnSeedRoll: static () => 0,
            out _,
            randomRetreatStepRoll: static () => 0);
        Check(!hasSnapshot && fallbackCalls == 0,
            "The 7.69 GetTargetPosDistance route-failure branch must keep the NPC at its current tile and emit no movement.");
    }

    private static void GeneratedSnapshotsAreDefensiveAndSpawnKeepsRouteState()
    {
        var hub = NewHub(routeType: 2, startWait: 0);
        Check(hub.TrySpawnGeneratedNpc(42, out var spawned, randomRoll: static _ => 0, positionX: 17, positionY: 18) && spawned is not null,
            "Generated NPC fixture did not spawn.");
        Check(spawned!.RouteType == 2 && spawned.GenerationRoute.Start == new LegacyNpcGenerationRoutePoint(6, 6, 0, 0) &&
              spawned.GenerationRoute.Segment1 == new LegacyNpcGenerationRoutePoint(20, 10, 0, 0) &&
              spawned.SegmentX == 6 && spawned.SegmentY == 6 && spawned.SegmentProgress == 0,
            "Generated NPC route type and initial route state were not copied from its definition.");

        Check(hub.TryCreateGeneratedNpcAiSnapshot(spawned.ConnectionId, attackRange: 4, routeStep: null,
            randomIntRoll: 0, dexterityRoll: 0, out var snapshot) && snapshot is not null,
            "WorldHub did not produce the generated NPC planner snapshot.");
        var mobCopy = snapshot!.MobSnapshot;
        var affectCopy = snapshot.AffectSnapshot;
        var enemiesCopy = snapshot.EnemyList;
        var inputCopy = snapshot.PlannerInput;
        var originalMobByte = mobCopy[0];
        var originalAffectByte = affectCopy[0];
        var originalEnemy = enemiesCopy[0];
        var originalSkillByte = inputCopy.Mob.SkillBar[0];
        mobCopy[0] ^= 0xFF;
        affectCopy[0] ^= 0xFF;
        ((int[])enemiesCopy)[0] = 123;
        ((int[])inputCopy.Mob.EnemyIds)[0] = 456;
        ((byte[])inputCopy.Mob.SkillBar)[0] = 0;

        Check(hub.TryGetNpcSnapshot(spawned.ConnectionId, out var current) && current is not null &&
              current.MobSnapshot[0] != mobCopy[0] && current.AffectSnapshot[0] != affectCopy[0] &&
              current.EnemyList[0] != 123 && current.EnemyList[0] != 456 &&
              current.MobSnapshot[0] == originalMobByte && current.AffectSnapshot[0] == originalAffectByte &&
              current.EnemyList[0] == originalEnemy && current.MobSnapshot[LegacyAccountSnapshot.MobSkillBarOffset] == originalSkillByte,
            "Mutating returned snapshot arrays changed authoritative WorldHub state.");
    }

    private static void WorldHubAppliesRouteMoveWaitAndReturnDecisions()
    {
        var waitHub = NewHub(routeType: 0, startWait: 4);
        Check(waitHub.TrySpawnGeneratedNpc(42, out var waitingNpc, randomRoll: static _ => 0) && waitingNpc is not null,
            "Wait fixture did not spawn.");
        var (waitingSnapshot, waiting) = Plan(waitHub, waitingNpc!.ConnectionId, attackRange: 4, routeStep: new(10, 10));
        Check(waiting.Kind == LegacyMobAiDecisionKind.Move && waiting.SegmentProgress == 1 && waiting.WaitSec == 0,
            "GenerateMob's initial WaitSec was not decremented by the legacy six-second Peace step before route advancement.");
        var waitResult = waitHub.TryApplyGeneratedNpcAiDecision(waitingSnapshot, waiting, out var waitApplied);
        Check(waitResult == LegacyNpcAiApplyStatus.Applied && waitApplied.WaitSec == 0 && waitApplied.SegmentProgress == 1,
            "WorldHub did not persist the generated route transition under its lock.");

        var moveHub = NewHub(routeType: 0, startWait: 0);
        Check(moveHub.TrySpawnGeneratedNpc(42, out var movingNpc, randomRoll: static _ => 0) && movingNpc is not null,
            "Move fixture did not spawn.");
        var (movingSnapshot, moving) = Plan(moveHub, movingNpc!.ConnectionId, attackRange: 4, routeStep: new(11, 10));
        Check(moving.Kind == LegacyMobAiDecisionKind.Move && moving.NextX == 11 && moving.NextY == 10 && moving.SegmentProgress == 1,
            "The deterministic route planner did not select the supplied next tile and segment transition.");
        Check(moveHub.TryApplyGeneratedNpcAiDecision(movingSnapshot, moving, out var moved) == LegacyNpcAiApplyStatus.Applied &&
              moveHub.TryGetNpcSnapshot(movingNpc.ConnectionId, out var movedNpc) && movedNpc is not null &&
              movedNpc.PositionX == 11 && movedNpc.PositionY == 10 && movedNpc.SegmentX == 20 && movedNpc.SegmentProgress == 1 &&
              moved.PositionX == 11 && moved.PositionY == 10,
            "WorldHub did not apply the accepted move and route transition atomically.");

        var blockedHub = NewHub(routeType: 0, startWait: 0);
        AddPlayer(blockedHub, 1, "BLOCK_TILE", 11, 10, clan: 5);
        Check(blockedHub.TrySpawnGeneratedNpc(42, out var blockedNpc, randomRoll: static _ => 0) && blockedNpc is not null,
            "Blocked destination fixture did not spawn.");
        var (blockedSnapshot, blockedMove) = Plan(blockedHub, blockedNpc!.ConnectionId, attackRange: 4, routeStep: new(11, 10));
        Check(blockedMove.Kind == LegacyMobAiDecisionKind.Move &&
              blockedHub.TryApplyGeneratedNpcAiDecision(blockedSnapshot, blockedMove, out _) == LegacyNpcAiApplyStatus.Blocked &&
              blockedHub.TryGetNpcSnapshot(blockedNpc.ConnectionId, out var unchangedBlocked) && unchangedBlocked is not null &&
              unchangedBlocked.PositionX == 6 && unchangedBlocked.PositionY == 6,
            "WorldHub accepted a route destination occupied at capture time.");

        var outOfRangeHub = NewHub(routeType: 0, startWait: 0);
        AddPlayer(outOfRangeHub, 1, "FAR_TARGET", 100, 100, clan: 3);
        Check(outOfRangeHub.TrySpawnGeneratedNpc(42, out var outOfRangeNpc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && outOfRangeNpc is not null,
            "Out-of-range target fixture did not spawn.");
        Check(outOfRangeHub.SetNpcCombatState(outOfRangeNpc!.ConnectionId, LegacyNpcMode.Combat, 1, [1]), "Could not set out-of-range enemy.");
        var (outOfRangeSnapshot, outOfRangeDecision) = Plan(outOfRangeHub, outOfRangeNpc.ConnectionId, attackRange: 4, routeStep: new(10, 10));
        Check(outOfRangeDecision.Kind == LegacyMobAiDecisionKind.Idle &&
              outOfRangeHub.TryApplyGeneratedNpcAiDecision(outOfRangeSnapshot, outOfRangeDecision, out _) == LegacyNpcAiApplyStatus.Applied &&
              outOfRangeHub.TryGetNpcCombatState(outOfRangeNpc.ConnectionId, out var outOfRangeMode, out var outOfRangeTarget, out var outOfRangeEnemies) &&
              outOfRangeMode == LegacyNpcMode.Peace && outOfRangeTarget == 0 && outOfRangeEnemies.All(static id => id == 0),
            "An out-of-range enemy was retained instead of following the planner's cleared peace decision.");

        var returnHub = NewHub(routeType: 0, startWait: 0);
        AddPlayer(returnHub, 1, "RETURN_TARGET", 28, 10, clan: 5);
        Check(returnHub.TrySpawnGeneratedNpc(42, out var returningNpc, randomRoll: static _ => 0, positionX: 27, positionY: 10) && returningNpc is not null,
            "Return fixture did not spawn.");
        Check(returnHub.SetNpcCombatState(returningNpc!.ConnectionId, LegacyNpcMode.Combat, 1, [1]), "Could not seed Combat state.");
        var (returnSnapshot, returning) = Plan(returnHub, returningNpc.ConnectionId, attackRange: 0, routeStep: new(26, 10));
        Check(returning.Kind == LegacyMobAiDecisionKind.ReturnToRoute && returning.NextX == 26,
            "The combat planner did not produce the deterministic route-return decision.");
        Check(returnHub.TryApplyGeneratedNpcAiDecision(returnSnapshot, returning, out _) == LegacyNpcAiApplyStatus.Applied &&
              returnHub.TryGetNpcCombatState(returningNpc.ConnectionId, out var mode, out var target, out var enemies) &&
              mode == LegacyNpcMode.Peace && target == 0 && enemies.All(static id => id == 0) &&
              returnHub.TryGetNpcSnapshot(returningNpc.ConnectionId, out var returnedNpc) && returnedNpc is not null && returnedNpc.PositionX == 26,
            "ReturnToRoute did not clear combat state and move to the accepted route step.");
    }

    private static void MerchantActivationWaitsForTypeZeroRouteEnd()
    {
        var hub = NewHub(
            routeType: 0,
            startWait: 0,
            terminalRoute: true,
            npcMerchant: 5,
            currentScoreMerchant: 0xA0);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0,
                positionX: 10, positionY: 10, forceGenerationRoutePosition: true) && npc is not null,
            "Merchant route fixture did not spawn.");

        for (var expectedProgress = 1; expectedProgress <= 4; expectedProgress++)
        {
            var (snapshot, decision) = Plan(hub, npc!.ConnectionId, attackRange: 4, routeStep: null);
            Check(decision.Kind == LegacyMobAiDecisionKind.Idle && decision.SegmentProgress == expectedProgress,
                $"RouteType 0 did not advance to route point {expectedProgress}: got {decision.Kind}/{decision.SegmentProgress}, " +
                $"wait={decision.WaitSec}, mob=({snapshot.PlannerInput.Mob.X},{snapshot.PlannerInput.Mob.Y}), " +
                $"segment=({snapshot.PlannerInput.Mob.SegmentX},{snapshot.PlannerInput.Mob.SegmentY}).");
            Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out _) == LegacyNpcAiApplyStatus.Applied,
                $"WorldHub rejected RouteType 0 point {expectedProgress}.");
            Check(hub.TryGetNpcSnapshot(npc.ConnectionId, out var beforeTerminal) && beforeTerminal is not null &&
                  LegacyScore.Read(beforeTerminal.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset)).Merchant == 0 &&
                  LegacyScore.Read(beforeTerminal.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)).Merchant == 0xA0,
                "Merchant score changed before RouteType 0 reached its terminal point.");
        }

        var (terminalSnapshot, terminalDecision) = Plan(hub, npc!.ConnectionId, attackRange: 4, routeStep: null);
        Check(terminalDecision.Kind == LegacyMobAiDecisionKind.Idle && terminalDecision.SegmentProgress == 4,
            "RouteType 0 terminal step did not remain at the destination.");
        Check(hub.TryApplyGeneratedNpcAiDecision(terminalSnapshot, terminalDecision, out _) == LegacyNpcAiApplyStatus.Applied &&
              hub.TryGetNpcSnapshot(npc.ConnectionId, out var afterTerminal) && afterTerminal is not null &&
              LegacyScore.Read(afterTerminal.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset)).Merchant == 5 &&
              LegacyScore.Read(afterTerminal.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)).Merchant == 0xA5,
            "RouteType 0 terminal step did not activate Merchant while preserving the current score's upper nibble.");
    }

    private static void StalePlansAreRejectedAndPhysicalAttackUpdatesAuthoritativeHp()
    {
        var staleHub = NewHub(routeType: 0, startWait: 0);
        AddPlayer(staleHub, 1, "PASSIVE", 11, 10, clan: 3);
        Check(staleHub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Stale-plan fixture did not spawn.");
        Check(staleHub.TryCreateGeneratedNpcAiSnapshot(npc!.ConnectionId, 4, new(10, 11), 0, 0, out var staleSnapshot) && staleSnapshot is not null,
            "WorldHub did not capture the stale-plan input.");
        var aggroDecision = LegacyMobAiPlanner.Plan(staleSnapshot!.PlannerInput);
        Check(aggroDecision.Kind == LegacyMobAiDecisionKind.Aggro && aggroDecision.AggroTargetId == 1,
            "The deterministic passive planner fixture did not request aggro.");
        Check(staleHub.UpdatePosition(1, 80, 80), "Could not invalidate target position after planning.");
        Check(staleHub.TryApplyGeneratedNpcAiDecision(staleSnapshot, aggroDecision, out _) == LegacyNpcAiApplyStatus.Stale &&
              staleHub.TryGetNpcCombatState(npc.ConnectionId, out var staleMode, out _, out var staleEnemies) &&
              staleMode == LegacyNpcMode.Peace && staleEnemies.All(static id => id == 0),
            "A stale target/world snapshot must be rejected without mutating NPC combat state.");

        var attackHub = NewHub(routeType: 0, startWait: 0, allEmptySkills: true);
        AddPlayer(attackHub, 1, "ATTACK_TARGET", 11, 10, clan: 5);
        Check(attackHub.TrySpawnGeneratedNpc(42, out var attackerNpc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && attackerNpc is not null,
            "Unsupported-attack fixture did not spawn.");
        Check(attackHub.SetNpcCombatState(attackerNpc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]), "Could not seed enemy list.");
        var (attackSnapshot, attack) = Plan(attackHub, attackerNpc.ConnectionId, attackRange: 4, routeStep: new(10, 10));
        Check(attack.Kind == LegacyMobAiDecisionKind.Attack,
            "The all-255 skill bar fixture did not produce the typed Attack decision.");
        var attackStatus = attackHub.TryApplyGeneratedNpcAiDecision(attackSnapshot, attack, out var attackResult);
        var hasCombatState = attackHub.TryGetNpcCombatState(attackerNpc.ConnectionId, out var attackMode, out var currentTarget, out var attackEnemies);
        var hasNpcSnapshot = attackHub.TryGetNpcSnapshot(attackerNpc.ConnectionId, out var unchanged);
        var hasTargetSnapshot = attackHub.TryGetCharacterSnapshot(1, out var targetMob, out _, out _);
        var targetHp = hasTargetSnapshot && targetMob is not null ? LegacyMobCombatState.Read(targetMob).CurrentScore.Hp : -1;
        Check(attackStatus == LegacyNpcAiApplyStatus.Applied && hasCombatState &&
              attackMode == LegacyNpcMode.Combat && currentTarget == 1 && attackEnemies.Count == 13 && attackEnemies[0] == 1 &&
              hasNpcSnapshot && unchanged is not null && unchanged.PositionX == 10 &&
              attackResult.DecisionKind == LegacyMobAiDecisionKind.Attack && targetHp == 5 &&
              attackResult.Attack is { Damage: 995, RemainingHp: 5, TargetDied: false } &&
              attackResult.Attack.AttackerPositionX == 10 && attackResult.Attack.TargetConnectionId == 1,
            $"A supported physical NPC attack should apply damage; status={attackStatus}, mode={attackMode}, target={currentTarget}, hp={targetHp}, damage={attackResult.Attack?.Damage}.");
    }

    private static void PassiveGeneratedNpcLeaderAggroFansOutToItsFollowers()
    {
        var leaderMob = CreateMob("Wolf_Leader", hp: 1000, clan: 2);
        var followerMob = CreateMob("Wolf_Follower@", hp: 1000, clan: 2);
        var catalog = LegacyNpcGenerationCatalog.Parse(
            """
            #[42]
            MaxNumMob: 10
            MinGroup: 1
            MaxGroup: 1
            Leader: Wolf_Leader
            Follower: Wolf_Follower@
            RouteType: 0
            StartX: 10
            StartY: 10
            StartRange: 0
            StartWait: 0
            Segment1X: 20
            Segment1Y: 10
            DestX: 30
            DestY: 10
            """,
            new Dictionary<string, byte[]>
            {
                ["Wolf_Leader"] = leaderMob,
                ["Wolf_Follower@"] = followerMob,
            });
        var hub = new WorldHub(npcGenerationCatalog: catalog, skillData: MobAiSkillDataFixture.Create());
        AddPlayer(hub, 1, "PASSGRPTARGET", 11, 10, clan: 3);
        AddPlayer(hub, 2, "PASSGRPFARVIEW", 100, 100, clan: 3);
        Check(hub.TrySpawnGeneratedNpcGroup(42, out var firstGroup, static _ => 0,
                positionX: 10, positionY: 10) && firstGroup is { Count: 2 },
            "The first generated NPC group did not spawn with one follower.");
        Check(hub.TrySpawnGeneratedNpcGroup(42, out var otherGroup, static _ => 0,
                positionX: 20, positionY: 10) && otherGroup is { Count: 2 },
            "The unrelated generated NPC group did not spawn.");

        var leader = firstGroup![0];
        var follower = firstGroup[1];
        var unrelatedLeader = otherGroup![0];
        var unrelatedFollower = otherGroup[1];
        var inclusiveFollowerId = hub.EnterNpc(CreateMob("R23_FOLLOWER", hp: 1000, clan: 2), 34, 10,
            requestedConnectionId: 20_041, generateIndex: 42, leaderConnectionId: leader.ConnectionId);
        var distantFollowerId = hub.EnterNpc(CreateMob("R24_FOLLOWER", hp: 1000, clan: 2), 35, 10,
            requestedConnectionId: 20_040, generateIndex: 42, leaderConnectionId: leader.ConnectionId);
        var deadFollowerId = hub.EnterNpc(CreateMob("DEAD_FOLLOWER", hp: 0, clan: 2), 12, 10,
            requestedConnectionId: 20_042, generateIndex: 42, leaderConnectionId: leader.ConnectionId);
        var distantDeadFollowerId = hub.EnterNpc(CreateMob("DEAD_FAR", hp: 0, clan: 2), 100, 100,
            requestedConnectionId: 20_043, generateIndex: 42, leaderConnectionId: leader.ConnectionId);
        var inactiveFollowerId = hub.EnterNpc(CreateMob("INACTIVE", hp: 1000, clan: 2), 13, 10,
            requestedConnectionId: 20_044, generateIndex: 42, leaderConnectionId: leader.ConnectionId);
        Check(inclusiveFollowerId == 20_041 && distantFollowerId == 20_040 && deadFollowerId == 20_042 &&
              distantDeadFollowerId == 20_043 && inactiveFollowerId == 20_044 &&
              hub.SetNpcCombatState(inactiveFollowerId, LegacyNpcMode.Empty, LegacyNpcMode.EmptyTarget, [0]),
            "The generated group follower boundary fixtures did not register.");
        Check(Math.Abs(unrelatedLeader.PositionX - 11) <= 23 && Math.Abs(unrelatedLeader.PositionY - 10) <= 23 &&
              Math.Abs(unrelatedFollower.PositionX - 11) <= 23 && Math.Abs(unrelatedFollower.PositionY - 10) <= 23,
            "The unrelated-group fixture must be within SetBattle's range to isolate LeaderId filtering.");
        Check(hub.TryCreateGeneratedNpcAiPeaceSnapshot(leader.ConnectionId, static () => 0, out var snapshot) &&
              snapshot is not null,
            "WorldHub did not capture the peaceful generated-group leader.");
        var decision = LegacyMobAiPlanner.Plan(snapshot!.PlannerInput);
        Check(decision.Kind == LegacyMobAiDecisionKind.Aggro && decision.AggroTargetId == 1,
            "The generated-group leader did not acquire the nearby hostile player.");

        var status = hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var outcome);
        Check(status == LegacyNpcAiApplyStatus.Applied &&
              hub.TryGetNpcCombatState(leader.ConnectionId, out var leaderMode, out _, out var leaderEnemies) &&
              leaderMode == LegacyNpcMode.Combat && leaderEnemies[0] == 1 &&
              hub.TryGetNpcCombatState(follower.ConnectionId, out var followerMode, out _, out var followerEnemies) &&
              followerMode == LegacyNpcMode.Combat && followerEnemies[0] == 1,
            $"Passive leader aggro did not put its living follower into combat against the same target; apply={status}.");
        Check(hub.TryGetNpcCombatState(inclusiveFollowerId, out var inclusiveFollowerMode, out _, out var inclusiveFollowerEnemies) &&
              inclusiveFollowerMode == LegacyNpcMode.Combat && inclusiveFollowerEnemies[0] == 1,
            "Passive leader aggro did not include the exact 23-tile SetBattle boundary.");
        Check(hub.TryGetNpcCombatState(unrelatedLeader.ConnectionId, out var unrelatedLeaderMode, out _, out _) &&
              unrelatedLeaderMode == LegacyNpcMode.Peace &&
              hub.TryGetNpcCombatState(unrelatedFollower.ConnectionId, out var unrelatedFollowerMode, out _, out _) &&
              unrelatedFollowerMode == LegacyNpcMode.Peace &&
              hub.TryGetNpcCombatState(distantFollowerId, out var distantFollowerMode, out _, out var distantFollowerEnemies) &&
              distantFollowerMode == LegacyNpcMode.Peace && distantFollowerEnemies.All(static id => id == 0),
            "Passive aggro propagated to another group, past the 24-tile boundary, or to a dead follower.");

        var nearRemoval = outcome.AdditionalRemovals.SingleOrDefault(removal => removal.ConnectionId == deadFollowerId);
        var farRemoval = outcome.AdditionalRemovals.SingleOrDefault(removal => removal.ConnectionId == distantDeadFollowerId);
        Check(!hub.TryGetNpcSnapshot(deadFollowerId, out _) && !hub.TryGetNpcSnapshot(distantDeadFollowerId, out _) &&
              outcome.AdditionalRemovals.Count == 2 &&
              nearRemoval is { RemoveType: 1, ViewRecipientConnectionIds.Count: 1 } &&
              nearRemoval.ViewRecipientConnectionIds.Contains(1) &&
              farRemoval is { RemoveType: 1, ViewRecipientConnectionIds.Count: 1 } &&
              farRemoval.ViewRecipientConnectionIds.Contains(2),
            "Passive leader aggro did not remove active-mode dead followers with type 1 and each follower's captured view.");

        Check(hub.TryGetNpcSnapshot(inactiveFollowerId, out var inactiveSnapshot) &&
              inactiveSnapshot is { Mode: LegacyNpcMode.Empty } &&
              inactiveSnapshot.LeaderId == leader.ConnectionId &&
              !inactiveSnapshot.IsGeneratedGroupPartyMember &&
              hub.SetNpcCombatState(leader.ConnectionId, LegacyNpcMode.Peace, LegacyNpcMode.EmptyTarget, [0]) &&
              hub.SetNpcCombatState(inactiveFollowerId, LegacyNpcMode.Peace, LegacyNpcMode.EmptyTarget, [0]),
            "The passive cleanup did not preserve the inactive follower for the membership follow-up.");
        var hasSecondSnapshot = hub.TryCreateGeneratedNpcAiPeaceSnapshot(
            leader.ConnectionId,
            static () => 0,
            out var secondSnapshot);
        Check(hasSecondSnapshot && secondSnapshot is not null,
            "The reset generated-group leader did not produce its second passive snapshot.");
        var secondDecision = LegacyMobAiPlanner.Plan(secondSnapshot!.PlannerInput);
        var secondStatus = hub.TryApplyGeneratedNpcAiDecision(secondSnapshot, secondDecision, out _);
        Check(secondStatus == LegacyNpcAiApplyStatus.Applied &&
              hub.TryGetNpcCombatState(inactiveFollowerId, out var inactiveMode, out _, out var inactiveEnemies) &&
              inactiveMode == LegacyNpcMode.Peace && inactiveEnemies.All(static id => id == 0),
            "A follower whose empty-mode PartyList slot was cleared rejoined the next passive leader aggro.");
    }

    private static void PassiveGeneratedNpcAggroAgainstSummonDoesNotFanOutToTargetPartySummons()
    {
        var leaderMob = CreateMob("Wolf_Leader", hp: 1000, clan: 5);
        var followerMob = CreateMob("Wolf_Follower@", hp: 1000, clan: 5);
        var npcCatalog = LegacyNpcGenerationCatalog.Parse(
            """
            #[42]
            MaxNumMob: 10
            MinGroup: 1
            MaxGroup: 1
            Leader: Wolf_Leader
            Follower: Wolf_Follower@
            RouteType: 0
            StartX: 10
            StartY: 10
            StartRange: 0
            StartWait: 0
            Segment1X: 20
            Segment1Y: 10
            DestX: 30
            DestY: 10
            """,
            new Dictionary<string, byte[]>
            {
                ["Wolf_Leader"] = leaderMob,
                ["Wolf_Follower@"] = followerMob,
            });
        var referenceRoot = FindReferenceRoot();
        var summonCatalog = LegacySummonCatalog.Load(Path.Combine(referenceRoot, "Server", "TMSrv", "run", "BaseSummon"));
        var hub = new WorldHub(
            npcGenerationCatalog: npcCatalog,
            summonCatalog: summonCatalog,
            skillData: MobAiSkillDataFixture.Create());

        AddSummonPartyMember(hub, 1, "TARGET_PARTY", 40, 40, special3: 30);
        AddSummonPartyMember(hub, 2, "TARGET_OWNER", 11, 10, special3: 60);
        Check(hub.TrySetParty(1, [1, 2]), "Could not create the hidden target party fixture.");
        var partySummons = CreateTestSummons(hub, 2).ToArray();
        Check(partySummons.Length >= 2, "The target party did not create enough summons for the no-fan-out fixture.");
        Check(partySummons.All(summon => Math.Abs(summon.PositionX - 10) <= 23 && Math.Abs(summon.PositionY - 10) <= 23),
            "All target-party summons must be within the leader's SetBattle rectangle for this gate fixture.");
        HideSummonPartyMember(hub, 1, "TARGET_PARTY", 40, 40, special3: 30);
        HideSummonPartyMember(hub, 2, "TARGET_OWNER", 11, 10, special3: 60);

        Check(hub.TrySpawnGeneratedNpcGroup(42, out var group, static _ => 0,
                positionX: 10, positionY: 10) && group is { Count: 2 },
            "Generated NPC group did not spawn for summon-target aggro fixture.");
        var leader = group![0];
        var follower = group[1];
        Check(hub.TryCreateGeneratedNpcAiPeaceSnapshot(leader.ConnectionId, static () => 0, out var snapshot) &&
              snapshot is not null,
            "WorldHub did not capture the peaceful leader for summon-target passive aggro.");
        var decision = LegacyMobAiPlanner.Plan(snapshot!.PlannerInput);
        var selectedSummonId = decision.AggroTargetId;
        var selectedSummon = partySummons.SingleOrDefault(summon => summon.ConnectionId == selectedSummonId);
        Check(decision.Kind == LegacyMobAiDecisionKind.Aggro && selectedSummon is not null,
            $"The generated leader did not select a hostile party summon as its passive target; kind={decision.Kind}, target={decision.AggroTargetId}.");

        var status = hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out _);
        Check(status == LegacyNpcAiApplyStatus.Applied &&
              hub.TryGetNpcCombatState(follower.ConnectionId, out var followerMode, out _, out var followerEnemies) &&
              followerMode == LegacyNpcMode.Combat && followerEnemies[0] == selectedSummonId,
            $"Passive aggro did not propagate the summon target to the generated follower; apply={status}.");
        Check(partySummons.Where(summon => summon.ConnectionId != selectedSummonId)
                  .All(summon => hub.TryGetSummonEnemyList(summon.ConnectionId, out var enemies) &&
                                 !enemies.Contains(leader.ConnectionId)),
            "Passive aggro against a summon incorrectly propagated to the target summon's party.");
    }

    private static void AddSummonPartyMember(WorldHub hub, int id, string name, short x, short y, short special3)
    {
        var mob = CreateMob(name, hp: 1000, clan: 5);
        foreach (var offset in new[] { LegacyAccountSnapshot.MobBaseScoreOffset, LegacyAccountSnapshot.MobCurrentScoreOffset })
        {
            var score = LegacyScore.Read(mob.AsSpan(offset)) with { Special3 = special3 };
            score.Write(mob.AsSpan(offset));
        }
        AddPlayerWithMob(hub, id, x, y, mob);
    }

    private static void HideSummonPartyMember(WorldHub hub, int id, string name, short x, short y, short special3)
    {
        var mob = CreateMob(name, hp: 1000, clan: 5);
        mob[LegacyAccountSnapshot.MobRsvOffset] |= 0x10;
        foreach (var offset in new[] { LegacyAccountSnapshot.MobBaseScoreOffset, LegacyAccountSnapshot.MobCurrentScoreOffset })
        {
            var score = LegacyScore.Read(mob.AsSpan(offset)) with { Special3 = special3 };
            score.Write(mob.AsSpan(offset));
        }
        Check(hub.SetCharacterState(id, 0, 0, 5, 0, 0, x, y, mob), "Could not hide target-party summon owner after summoning.");
    }

    private static IReadOnlyList<LegacySummonedMob> CreateTestSummons(WorldHub hub, int summonerId)
    {
        var skill = new LegacySkillDefinition(50, 20, 0, 20, 0, 0, 11, 1, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "Evocar");
        var result = hub.TryApplySkillAttack(summonerId, summonerId, skill, itemData: null, weather: 0,
            randomFactor: 90, out var outcome);
        Check(result == LegacySkillAttackResult.Accepted && outcome?.SummonedMobs is { Count: > 0 },
            $"Could not create hidden target-party summon fixture; result={result}, summons={outcome?.SummonedMobs?.Count ?? 0}, summonResult={outcome?.SummonResult}.");
        return outcome!.SummonedMobs!;
    }

    private static string FindReferenceRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Backup", "Tools", "ReferenceSources", "TMProject2GlobalClient", "Servidor");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("TMProject 7.69 reference tree was not found.");
    }

    private static void StaleGeneratedNpcChasePlanIsRejectedAfterTargetMoves()
    {
        var hub = NewHub(routeType: 0, startWait: 0);
        AddPlayer(hub, 1, "STALE_CHASE", 26, 20, clan: 5);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 20, positionY: 20) && npc is not null,
            "Stale chase NPC fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 1, [1]),
            "Could not set the stale chase target.");
        Check(hub.TryCreateGeneratedNpcAiChaseSnapshot(
                npc.ConnectionId,
                attackRange: 0,
                randomIntRoll: 0,
                dexterityRoll: 0,
                randomFallbackOffset: static () => -3,
                randomReturnSeedRoll: static () => 0,
                out var snapshot) && snapshot is not null,
            "WorldHub did not produce the source-compatible chase snapshot.");
        var decision = LegacyMobAiPlanner.Plan(snapshot!.PlannerInput);
        Check(decision.Kind == LegacyMobAiDecisionKind.Move && decision.NextX is not null && decision.NextY is not null,
            "The stale chase fixture did not plan a movement.");
        Check(hub.UpdatePosition(1, 80, 80), "Could not invalidate the chase target position.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out _) == LegacyNpcAiApplyStatus.Stale &&
              hub.TryGetNpcSnapshot(npc.ConnectionId, out var unchanged) && unchanged is not null &&
              unchanged.PositionX == 20 && unchanged.PositionY == 20,
            "WorldHub applied a chase route after its target moved away from the captured world state.");
    }

    private static void CarbunkleFireAttackAppliesTargetElementalResistance()
    {
        var hub = NewHub(routeType: 0, startWait: 0, npcSkillBar: [32, 32, 32, 255]);
        var target = CreateMob("FIRE_TARGET", hp: 100_000, clan: 5);
        target[LegacyAccountSnapshot.MobResistOffset] = 50;
        AddPlayerWithMob(hub, 1, 11, 10, target);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Carbunkle Fire-bar fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed the Carbunkle Fire-bar target.");

        var (snapshot, decision) = Plan(hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                  { SkillIndex: 32, SkillParm: 1, Motion: 4, Resist: 0 },
            "The generated Carbunkle Fire-bar snapshot did not retain its source attack fields.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { Damage: 1492, RemainingHp: 98_508, TargetDied: false },
            $"Carbunkle Fire damage did not apply target Resist[0] before the attack tail; status={applied.Status}, damage={applied.Attack?.Damage}.");
    }

    private static void CarbunkleFireRepeatedFourthSlotAppliesTargetElementalResistance()
    {
        var hub = NewHub(routeType: 0, startWait: 0, npcSkillBar: [32, 32, 32, 32]);
        var target = CreateMob("FIRE_SLOT4", hp: 100_000, clan: 5);
        target[LegacyAccountSnapshot.MobResistOffset] = 50;
        AddPlayerWithMob(hub, 1, 11, 10, target);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Repeated Carbunkle Fire fourth-slot fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed the repeated Carbunkle Fire fourth-slot target.");

        var (snapshot, decision) = Plan(hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                  { SkillIndex: 32, SkillParm: 1, Motion: 4, Resist: 0 },
            "The repeated Carbunkle Fire fourth-slot snapshot did not retain its source attack fields.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { Damage: 1492, RemainingHp: 98_508, TargetDied: false },
            $"Repeated Carbunkle Fire fourth-slot damage did not apply target Resist[0] before the attack tail; status={applied.Status}, damage={applied.Attack?.Damage}.");
    }

    private static void GaiaFuryAttackAppliesTargetElementalResistance()
    {
        var hub = NewHub(routeType: 0, startWait: 0, npcSkillBar: [52, 52, 52, 255]);
        var target = CreateMob("GAIA_TARGET", hp: 100_000, clan: 5);
        target[LegacyAccountSnapshot.MobResistOffset + 2] = 50;
        AddPlayerWithMob(hub, 1, 11, 10, target);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Gaia Fury-bar fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed the Gaia Fury-bar target.");

        var (snapshot, decision) = Plan(hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                  { SkillIndex: 52, SkillParm: 1, Motion: 4, Resist: 2 },
            "The generated Gaia Fury-bar snapshot did not retain its source attack fields.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { Damage: 1492, RemainingHp: 98_508, TargetDied: false },
            $"Gaia Fury damage did not apply target Resist[2] before the attack tail; status={applied.Status}, damage={applied.Attack?.Damage}.");
    }

    private static void ShadowBladeAttackKeepsPhysicalDamageWithoutElementalResistance()
    {
        var hub = NewHub(routeType: 0, startWait: 0, npcSkillBar: [88, 88, 88, 255]);
        var target = CreateMob("SHADOW_TARGET", hp: 100_000, clan: 5);
        target[LegacyAccountSnapshot.MobResistOffset] = 50;
        AddPlayerWithMob(hub, 1, 11, 10, target);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Shadow Blade-bar fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed the Shadow Blade-bar target.");

        var (snapshot, decision) = Plan(hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                  { SkillIndex: 88, SkillParm: 1, Motion: 4, Resist: -1 },
            "The generated Shadow Blade-bar snapshot did not retain its source attack fields.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { Damage: 995, RemainingHp: 99_005, TargetDied: false },
            $"Lamina_das_Sombras InstanceType 1 should preserve physical damage; status={applied.Status}, damage={applied.Attack?.Damage}.");
    }

    private static void LightningAttackAppliesTargetElementalResistance()
    {
        var hub = NewHub(routeType: 0, startWait: 0, npcSkillBar: [33, 33, 33, 255]);
        var target = CreateMob("LIGHTNING_TARGET", hp: 100_000, clan: 5);
        target[LegacyAccountSnapshot.MobResistOffset + 3] = 50;
        AddPlayerWithMob(hub, 1, 11, 10, target);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Lightning-bar fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed the Lightning-bar target.");

        var (snapshot, decision) = Plan(hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                  { SkillIndex: 33, SkillParm: 1, Motion: 4, Resist: 3 },
            "The generated Lightning-bar snapshot did not retain its source attack fields.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { Damage: 1492, RemainingHp: 98_508, TargetDied: false },
            $"Relampago InstanceType 5 should apply target Resist[3]; status={applied.Status}, damage={applied.Attack?.Damage}.");
    }

    private static void LightningRepeatedFourthSlotAppliesTargetElementalResistance()
    {
        var hub = NewHub(routeType: 0, startWait: 0, npcSkillBar: [33, 33, 33, 33]);
        var target = CreateMob("LIGHT_SLOT4_TGT", hp: 100_000, clan: 5);
        target[LegacyAccountSnapshot.MobResistOffset + 3] = 50;
        AddPlayerWithMob(hub, 1, 11, 10, target);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Repeated Lightning fourth-slot fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed the repeated Lightning fourth-slot target.");

        var (snapshot, decision) = Plan(hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                  { SkillIndex: 33, SkillParm: 1, Motion: 4, Resist: 3 },
            "The repeated Lightning fourth-slot snapshot did not retain its source attack fields.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { Damage: 1492, RemainingHp: 98_508, TargetDied: false },
            $"Repeated Relampago fourth-slot damage did not apply target Resist[3]; status={applied.Status}, damage={applied.Attack?.Damage}.");
    }

    private static void PhoenixFireRepeatedBarAppliesTargetElementalResistance()
    {
        var hub = NewHub(routeType: 0, startWait: 0, npcSkillBar: [38, 38, 38, 38]);
        var target = CreateMob("PHOENIX_TARGET", hp: 100_000, clan: 5);
        target[LegacyAccountSnapshot.MobResistOffset] = 50;
        AddPlayerWithMob(hub, 1, 11, 10, target);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Phoenix of Fire-bar fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed the Phoenix of Fire-bar target.");

        var (snapshot, decision) = Plan(hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                  { SkillIndex: 38, SkillParm: 1, Motion: 4, Resist: 0 },
            "The generated Phoenix of Fire-bar snapshot did not retain its source attack fields.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { Damage: 1492, RemainingHp: 98_508, TargetDied: false },
            $"Phoenix of Fire damage did not apply target Resist[0] before the attack tail; status={applied.Status}, damage={applied.Attack?.Damage}.");
    }

    private static void PoisonMistRepeatedBarAppliesTargetElementalResistance()
    {
        var hub = NewHub(routeType: 0, startWait: 0, npcSkillBar: [40, 40, 40, 40]);
        var target = CreateMob("POISON_TARGET", hp: 100_000, clan: 5);
        target[LegacyAccountSnapshot.MobResistOffset + 2] = 50;
        AddPlayerWithMob(hub, 1, 11, 10, target);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Poison Mist-bar fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed the Poison Mist-bar target.");

        var (snapshot, decision) = Plan(hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                  { SkillIndex: 40, SkillParm: 1, Motion: 4, Resist: 2 },
            "The generated Poison Mist-bar snapshot did not retain its source attack fields.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { Damage: 1492, RemainingHp: 98_508, TargetDied: false },
            $"Poison Mist damage did not apply target Resist[2] before the attack tail; status={applied.Status}, damage={applied.Attack?.Damage}.");
    }

    private static void InfernoRepeatedBarAppliesTargetElementalResistance()
    {
        var hub = NewHub(routeType: 0, startWait: 0, npcSkillBar: [39, 39, 39, 39]);
        var target = CreateMob("INFERNO_TARGET", hp: 100_000, clan: 5);
        target[LegacyAccountSnapshot.MobResistOffset] = 50;
        AddPlayerWithMob(hub, 1, 11, 10, target);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Inferno-bar fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed the Inferno-bar target.");

        var (snapshot, decision) = Plan(hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                  { SkillIndex: 39, SkillParm: 1, Motion: 4, Resist: 0 },
            "The generated Inferno-bar snapshot did not retain its source attack fields.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { Damage: 1492, RemainingHp: 98_508, TargetDied: false },
            $"Inferno damage did not apply target Resist[0] before the attack tail; status={applied.Status}, damage={applied.Attack?.Damage}.");
    }

    private static void FeraFlamejanteBarAppliesTargetElementalResistance()
    {
        var hub = NewHub(routeType: 0, startWait: 0, npcSkillBar: [48, 48, 48, 51]);
        var target = CreateMob("FERA_TARGET", hp: 100_000, clan: 5);
        target[LegacyAccountSnapshot.MobResistOffset] = 50;
        AddPlayerWithMob(hub, 1, 11, 10, target);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Fera Flamejante-bar fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed the Fera Flamejante-bar target.");

        var (snapshot, decision) = Plan(hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                  { SkillIndex: 48, SkillParm: 1, Motion: 4, Resist: 0 },
            "The generated Fera Flamejante-bar snapshot did not retain its source attack fields.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { Damage: 1492, RemainingHp: 98_508, TargetDied: false },
            $"Fera Flamejante damage did not apply target Resist[0] before the attack tail; status={applied.Status}, damage={applied.Attack?.Damage}.");
    }

    private static void ExterminarRepeatedBarAppliesTargetElementalResistance()
    {
        var hub = NewHub(routeType: 0, startWait: 0, npcSkillBar: [22, 22, 22, 22]);
        var target = CreateMob("EXTERMINAR", hp: 100_000, clan: 5);
        target[LegacyAccountSnapshot.MobResistOffset] = 50;
        AddPlayerWithMob(hub, 1, 11, 10, target);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Exterminar-bar fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed the Exterminar-bar target.");

        var (snapshot, decision) = Plan(hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                  { SkillIndex: 22, SkillParm: 1, Motion: 4, Resist: 0 },
            "The generated Exterminar-bar snapshot did not retain its source attack fields.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { Damage: 1492, RemainingHp: 98_508, TargetDied: false },
            $"Exterminar damage did not apply target Resist[0] before the attack tail; status={applied.Status}, damage={applied.Attack?.Damage}.");
    }

    private static void NevascaRepeatedBarAppliesTargetElementalResistance()
    {
        var hub = NewHub(routeType: 0, startWait: 0, npcSkillBar: [36, 36, 36, 36]);
        var target = CreateMob("NEVASCA_TARGET", hp: 100_000, clan: 5);
        target[LegacyAccountSnapshot.MobResistOffset + 1] = 50;
        AddPlayerWithMob(hub, 1, 11, 10, target);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Nevasca-bar fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed the Nevasca-bar target.");

        var (snapshot, decision) = Plan(hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                  { SkillIndex: 36, SkillParm: 1, Motion: 4, Resist: 1 },
            "The generated Nevasca-bar snapshot did not retain its source attack fields.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { Damage: 1492, RemainingHp: 98_508, TargetDied: false },
            $"Nevasca InstanceType 3 should apply target Resist[1]; status={applied.Status}, damage={applied.Attack?.Damage}.");
    }

    private static void EspadaFlamejanteRepeatedBarAppliesTargetElementalResistance()
    {
        var hub = NewHub(routeType: 0, startWait: 0, npcClan: 8, npcSkillBar: [17, 17, 17, 17]);
        var target = CreateMob("ESPADA_TARGET", hp: 100_000, clan: 5);
        target[LegacyAccountSnapshot.MobResistOffset] = 50;
        AddPlayerWithMob(hub, 1, 11, 10, target);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Espada Flamejante-bar fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed the Espada Flamejante-bar target.");

        var (snapshot, decision) = Plan(hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                  { SkillIndex: 17, SkillParm: 1, Motion: 4, Resist: 0 },
            "The generated Espada Flamejante-bar snapshot did not retain its source attack fields.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { Damage: 1492, RemainingHp: 98_508, TargetDied: false },
            $"Espada Flamejante damage did not apply target Resist[0] before the attack tail; status={applied.Status}, damage={applied.Attack?.Damage}.");
    }

    private static void EnfraquecerMixedBarKeepsPhysicalDamageWithoutElementalResistance()
    {
        var hub = NewHub(routeType: 0, startWait: 0, npcClan: 1, npcSkillBar: [255, 255, 51, 255]);
        var target = CreateMob("ENF_TARGET", hp: 100_000, clan: 5);
        target[LegacyAccountSnapshot.MobResistOffset] = 50;
        AddPlayerWithMob(hub, 1, 11, 10, target);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Enfraquecer-bar fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed the Enfraquecer-bar target.");

        var (snapshot, decision) = Plan(
            hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100, skillBarSelectionRoll: 85);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                  { SkillIndex: 51, SkillParm: 1, Motion: 4, Resist: -2 },
            "The generated Enfraquecer-bar snapshot did not retain the selected skill and source resistance index.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { Damage: 995, RemainingHp: 99_005, TargetDied: false },
            $"Enfraquecer InstanceType 0 should keep physical damage unchanged; status={applied.Status}, damage={applied.Attack?.Damage}.");
    }

    private static void ToqueSagradoMixedBarKeepsPhysicalDamageWithoutElementalResistance()
    {
        var hub = NewHub(routeType: 0, startWait: 0, npcClan: 1, npcSkillBar: [255, 255, 1, 255]);
        var target = CreateMob("SAGRADO_TARGET", hp: 100_000, clan: 5);
        target[LegacyAccountSnapshot.MobResistOffset] = 50;
        AddPlayerWithMob(hub, 1, 11, 10, target);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Toque Sagrado-bar fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed the Toque Sagrado-bar target.");

        var (snapshot, decision) = Plan(
            hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100, skillBarSelectionRoll: 85);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                  { SkillIndex: 1, SkillParm: 1, Motion: 4, Resist: -2 },
            "The generated Toque Sagrado-bar snapshot did not retain the selected skill and source resistance index.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { Damage: 995, RemainingHp: 99_005, TargetDied: false },
            $"Toque Sagrado InstanceType 0 should preserve physical damage despite target Resist[0]; status={applied.Status}, damage={applied.Attack?.Damage}.");
    }

    private static void GolpeDuploMixedBarKeepsPhysicalDamageWithoutElementalResistance()
    {
        var hub = NewHub(routeType: 0, startWait: 0, npcClan: 1, npcSkillBar: [255, 255, 2, 255]);
        var target = CreateMob("GOLPE_DUPLO", hp: 100_000, clan: 5);
        target[LegacyAccountSnapshot.MobResistOffset] = 50;
        AddPlayerWithMob(hub, 1, 11, 10, target);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Golpe Duplo-bar fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed the Golpe Duplo-bar target.");

        var (snapshot, decision) = Plan(
            hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100, skillBarSelectionRoll: 85);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                  { SkillIndex: 2, SkillParm: 1, Motion: 4, Resist: -1 },
            "The generated Golpe Duplo-bar snapshot did not retain the selected skill and source resistance index.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { Damage: 995, RemainingHp: 99_005, TargetDied: false },
            $"Golpe Duplo InstanceType 1 should preserve physical damage despite target Resist[0]; status={applied.Status}, damage={applied.Attack?.Damage}.");
    }

    private static void EspadaFenixMixedBarKeepsPhysicalDamageWithoutElementalResistance()
    {
        var hub = NewHub(routeType: 0, startWait: 0, npcClan: 1, npcSkillBar: [255, 255, 12, 255]);
        var target = CreateMob("ESPADA_FNX", hp: 100_000, clan: 5);
        target[LegacyAccountSnapshot.MobResistOffset] = 50;
        AddPlayerWithMob(hub, 1, 11, 10, target);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Espada da Fênix-bar fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed the Espada da Fênix-bar target.");

        var (snapshot, decision) = Plan(
            hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100, skillBarSelectionRoll: 85);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                  { SkillIndex: 12, SkillParm: 1, Motion: 4, Resist: -1 },
            "The generated Espada da Fênix-bar snapshot did not retain the selected skill and source attack fields.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { Damage: 995, RemainingHp: 99_005, TargetDied: false },
            $"Espada da Fênix InstanceType 1 should preserve physical damage; status={applied.Status}, damage={applied.Attack?.Damage}.");
    }

    private static void ArqueiroElfoMixedBarKeepsPhysicalDamageWithoutElementalResistance()
    {
        var hub = NewHub(routeType: 0, startWait: 0, npcClan: 1, npcSkillBar: [255, 255, 79, 255]);
        var target = CreateMob("ARQUEIRO_ELFO", hp: 100_000, clan: 5);
        target[LegacyAccountSnapshot.MobResistOffset] = 50;
        AddPlayerWithMob(hub, 1, 11, 10, target);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Arqueiro_Elfo-bar fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed the Arqueiro_Elfo-bar target.");

        var (snapshot, decision) = Plan(
            hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100, skillBarSelectionRoll: 85);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                  { SkillIndex: 79, SkillParm: 1, Motion: 4, Resist: -1 },
            "The generated Arqueiro_Elfo-bar snapshot did not retain the selected skill and source attack fields.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { Damage: 995, RemainingHp: 99_005, TargetDied: false },
            $"Tempestade_de_Raios InstanceType 1 should preserve physical damage despite target Resist[0]; status={applied.Status}, damage={applied.Attack?.Damage}.");
    }

    private static void KalitzMagoMixedBarAppliesLightningResistance()
    {
        foreach (var (selectionRoll, expectedSkill, expectedResist, expectedDamage) in new[]
                 {
                     (49, 33, 3, 1492),
                     (85, 37, -2, 995),
                 })
        {
            var hub = NewHub(routeType: 0, startWait: 0, npcClan: 1, npcSkillBar: [33, 33, 37, 255]);
            var target = CreateMob("KALITZ_TARGET", hp: 100_000, clan: 5);
            target[LegacyAccountSnapshot.MobResistOffset + 1] = 50;
            target[LegacyAccountSnapshot.MobResistOffset + 3] = 50;
            AddPlayerWithMob(hub, 1, 12, 10, target);
            Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
                "Kalitz_Mago-bar fixture did not spawn.");
            Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
                "Could not seed the Kalitz_Mago-bar target.");

            var (snapshot, decision) = Plan(
                hub, npc.ConnectionId, 4, new(12, 10), physicalDamageRandomFactor: 100, skillBarSelectionRoll: selectionRoll);
            Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                      { SkillIndex: var skill, SkillParm: 1, Motion: 4, Resist: var resist } &&
                  skill == expectedSkill && resist == expectedResist,
                $"The Kalitz_Mago roll {selectionRoll} did not retain the selected skill's source fields.");
            Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
                  applied.Attack is { Damage: var damage, RemainingHp: var remainingHp, TargetDied: false } &&
                  damage == expectedDamage && remainingHp == 100_000 - expectedDamage,
                $"Kalitz_Mago roll {selectionRoll} used the wrong SkillData resistance mapping; status={applied.Status}, damage={applied.Attack?.Damage}.");
        }
    }

    private static void BruxaElficaMixedBarAppliesSelectedIceResistance()
    {
        var hub = NewHub(routeType: 0, startWait: 0, npcClan: 1, npcSkillBar: [38, 34, 38, 51]);
        var target = CreateMob("BRUXA_TARGET", hp: 100_000, clan: 5);
        target[LegacyAccountSnapshot.MobResistOffset + 1] = 50;
        AddPlayerWithMob(hub, 1, 11, 10, target);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Bruxa_Elfica mixed-bar fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed the Bruxa_Elfica mixed-bar target.");

        var (snapshot, decision) = Plan(
            hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100, skillBarSelectionRoll: 50);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                  { SkillIndex: 34, SkillParm: 1, Motion: 4, Resist: 1 },
            "The generated Bruxa_Elfica snapshot did not retain the selected ice skill and source attack fields.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { Damage: 1492, RemainingHp: 98_508, TargetDied: false },
            $"Bruxa_Elfica Lanca_de_Gelo InstanceType 3 should apply target Resist[1]; status={applied.Status}, damage={applied.Attack?.Damage}.");
    }

    private static void LichDemoMixedBarAppliesOrSkipsElementalResistance()
    {
        foreach (var (selectionRoll, expectedSkill, expectedResist, expectedDamage) in new[]
                 {
                     (49, 52, 2, 1492),
                     (85, 51, -2, 995),
                 })
        {
            var hub = NewHub(routeType: 0, startWait: 0, npcClan: 1, npcSkillBar: [52, 52, 51, 255]);
            var target = CreateMob("LICH_DEMO_TARGET", hp: 100_000, clan: 5);
            target[LegacyAccountSnapshot.MobResistOffset + 2] = 50;
            AddPlayerWithMob(hub, 1, 12, 10, target);
            Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
                "Lich_Demo mixed-bar fixture did not spawn.");
            Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
                "Could not seed the Lich_Demo mixed-bar target.");

            var (snapshot, decision) = Plan(
                hub, npc.ConnectionId, 4, new(12, 10), physicalDamageRandomFactor: 100,
                skillBarSelectionRoll: selectionRoll);
            Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                      { SkillIndex: var skillIndex, SkillParm: 1, Motion: 4, Resist: var resist } &&
                  skillIndex == expectedSkill && resist == expectedResist,
                $"Lich_Demo roll {selectionRoll} produced the wrong skill fields.");
            Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
                  applied.Attack is { Damage: var damage, TargetDied: false } && damage == expectedDamage,
                $"Lich_Demo roll {selectionRoll} produced wrong target resistance damage; damage={applied.Attack?.Damage}.");
        }
    }

    private static void MagoInfMixedBarAppliesSelectedIceResistance()
    {
        foreach (var (selectionRoll, expectedSkill, expectedResistSlot) in new[]
                 {
                     (0, 36, 1),
                     (50, 34, 1),
                     (85, 39, 0),
                 })
        {
            var hub = NewHub(routeType: 0, startWait: 0, npcClan: 1, npcSkillBar: [36, 34, 39, byte.MaxValue]);
            var target = CreateMob("MAGO_INF_TARGET", hp: 100_000, clan: 5);
            target[LegacyAccountSnapshot.MobResistOffset] = 50;
            target[LegacyAccountSnapshot.MobResistOffset + 1] = 50;
            AddPlayerWithMob(hub, 1, 11, 10, target);
            Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
                "Mago_Inf mixed-bar fixture did not spawn.");
            Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
                "Could not seed the Mago_Inf mixed-bar target.");

            var (snapshot, decision) = Plan(
                hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100,
                skillBarSelectionRoll: selectionRoll);
            Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                      { SkillIndex: var skill, SkillParm: 1, Motion: 4, Resist: var resist } &&
                  skill == expectedSkill && resist == expectedResistSlot,
                $"Mago_Inf roll {selectionRoll} did not preserve the selected skill and InstanceType mapping.");
            Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
                  applied.Attack is { Damage: 1492, RemainingHp: 98_508, TargetDied: false },
                $"Mago_Inf roll {selectionRoll} did not apply the selected target resistance; status={applied.Status}, damage={applied.Attack?.Damage}.");
        }
    }

    private static void MagoAmaldMixedBarMapsSelectedResistance()
    {
        foreach (var (selectionRoll, expectedSkill, expectedResist, expectedDamage) in new[]
                 {
                     (0, 36, 1, 1492),
                     (50, 34, 1, 1492),
                     (85, 26, 5, 995),
                 })
        {
            var hub = NewHub(routeType: 0, startWait: 0, npcClan: 1, npcSkillBar: [36, 34, 26, byte.MaxValue]);
            var target = CreateMob("MAGO_AMALD_TGT", hp: 100_000, clan: 5);
            target[LegacyAccountSnapshot.MobResistOffset + 1] = 50;
            AddPlayerWithMob(hub, 1, 11, 10, target);
            Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
                "Mago_Amald mixed-bar fixture did not spawn.");
            Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
                "Could not seed the Mago_Amald mixed-bar target.");

            var (snapshot, decision) = Plan(
                hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100,
                skillBarSelectionRoll: selectionRoll);
            Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                      { SkillIndex: var skill, SkillParm: 1, Motion: 4, Resist: var resist } &&
                  skill == expectedSkill && resist == expectedResist,
                $"Mago_Amald roll {selectionRoll} did not retain its SkillData resistance index.");
            Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
                  applied.Attack is { Damage: var damage, TargetDied: false } && damage == expectedDamage,
                $"Mago_Amald roll {selectionRoll} applied the wrong elemental multiplier; status={applied.Status}, damage={applied.Attack?.Damage}.");
        }
    }

    private static void DarkShadowSingleFlashBarMapsSourceSelectorAndPreservesDamage()
    {
        foreach (var (distance, selectionRoll, expectedSkill, expectedResist) in new[]
                 {
                     (1, 0, 26, 5), (1, 50, 26, -1),
                     (2, 0, 26, 5), (2, 50, -1, -1), (2, 85, -1, -1),
                 })
        {
            var hub = NewHub(routeType: 0, startWait: 0, npcClan: 1,
                npcSkillBar: [26, byte.MaxValue, byte.MaxValue, byte.MaxValue]);
            var target = CreateMob("DARK_SHADOW_TGT", hp: 100_000, clan: 5);
            target[LegacyAccountSnapshot.MobResistOffset] = 50;
            target[LegacyAccountSnapshot.MobResistOffset + 1] = 50;
            target[LegacyAccountSnapshot.MobResistOffset + 2] = 50;
            target[LegacyAccountSnapshot.MobResistOffset + 3] = 50;
            AddPlayerWithMob(hub, 1, checked((short)(10 + distance)), 10, target);
            Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0,
                      positionX: 10, positionY: 10) && npc is not null,
                "Dark_Shadow_ single-slot bar fixture did not spawn.");
            Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
                "Could not seed the Dark_Shadow_ target.");

            var (snapshot, decision) = Plan(
                hub, npc.ConnectionId, 4, new(checked((short)(10 + distance)), 10),
                physicalDamageRandomFactor: 100, skillBarSelectionRoll: selectionRoll);
            Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                      { SkillIndex: var skill, SkillParm: 1, Motion: 4, Resist: var resist } &&
                  skill == expectedSkill && resist == expectedResist,
                $"Dark_Shadow_ distance {distance}, roll {selectionRoll} did not preserve 7.69 GetAttack fields.");
            Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
                  applied.Attack is { Damage: 995, RemainingHp: 99_005, TargetDied: false },
                $"Dark_Shadow_ distance {distance}, roll {selectionRoll} applied an elemental modifier or wrong damage; status={applied.Status}, damage={applied.Attack?.Damage}.");
        }
    }

    private static void RangerInfMixedBarKeepsPhysicalDamageWithoutElementalResistance()
    {
        foreach (var (distance, selectionRoll, expectedSkill, expectedResist) in new[]
                 {
                     (1, 85, 73, -2), (2, 0, 73, -1), (3, 84, 79, -1),
                 })
        {
            var hub = NewHub(routeType: 0, startWait: 0, npcClan: 1,
                npcSkillBar: [byte.MaxValue, 79, 73, byte.MaxValue]);
            var target = CreateMob("RANGER_INF_TGT", hp: 100_000, clan: 5);
            target[LegacyAccountSnapshot.MobResistOffset + 1] = 50;
            AddPlayerWithMob(hub, 1, checked((short)(10 + distance)), 10, target);
            Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0,
                      positionX: 10, positionY: 10) && npc is not null,
                "Ranger_Inf mixed-bar fixture did not spawn.");
            Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
                "Could not seed the Ranger_Inf target.");

            var (snapshot, decision) = Plan(
                hub, npc.ConnectionId, 4, new(checked((short)(10 + distance)), 10),
                physicalDamageRandomFactor: 100, skillBarSelectionRoll: selectionRoll);
            Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                      { SkillIndex: var skill, SkillParm: 1, Motion: 4, Resist: var resist } &&
                  skill == expectedSkill && resist == expectedResist,
                $"Ranger_Inf distance {distance}, roll {selectionRoll} did not preserve the source skill fields.");
            Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
                  applied.Attack is { Damage: 995, RemainingHp: 99_005, TargetDied: false },
                $"Ranger_Inf row {expectedSkill} should keep damage unchanged with Resist[1]=50; status={applied.Status}, damage={applied.Attack?.Damage}.");
        }
    }

    private static void ShamaAmaldMixedBarAppliesSelectedElementalResistance()
    {
        foreach (var (distance, selectionRoll, expectedSkill, expectedResistSlot) in new[]
                 {
                     (1, 0, 19, 1), (1, 50, 33, 3), (1, 85, 48, 0),
                     (2, 0, 19, 1), (2, 50, 33, 3), (2, 85, 48, 0),
                 })
        {
            var hub = NewHub(routeType: 0, startWait: 0, npcClan: 1, npcSkillBar: [19, 33, 48, byte.MaxValue]);
            var target = CreateMob("SHAMA_TARGET", hp: 100_000, clan: 5);
            target[LegacyAccountSnapshot.MobResistOffset] = 50;
            target[LegacyAccountSnapshot.MobResistOffset + 1] = 50;
            target[LegacyAccountSnapshot.MobResistOffset + 3] = 50;
            AddPlayerWithMob(hub, 1, checked((short)(10 + distance)), 10, target);
            Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
                "Shama_Amald mixed-bar fixture did not spawn.");
            Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
                "Could not seed the Shama_Amald mixed-bar target.");

            var (snapshot, decision) = Plan(
                hub, npc.ConnectionId, 4, new(checked((short)(10 + distance)), 10),
                physicalDamageRandomFactor: 100, skillBarSelectionRoll: selectionRoll);
            Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                      { SkillIndex: var skill, SkillParm: 1, Motion: var motion, Resist: var resist } &&
                  skill == expectedSkill && motion == 4 && resist == expectedResistSlot,
                $"Shama_Amald distance {distance}, roll {selectionRoll} did not preserve the source skill fields.");
            Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
                  applied.Attack is { Damage: 1492, RemainingHp: 98_508, TargetDied: false },
                $"Shama_Amald roll {selectionRoll} did not apply the selected target resistance; status={applied.Status}, damage={applied.Attack?.Damage}.");
        }
    }

    private static void ShamanInfMixedBarAppliesSelectedResistSlot()
    {
        foreach (var (distance, selectionRoll, expectedSkill, expectedResist, expectedDamage) in new[]
                 {
                     (1, 0, 52, 2, 1492), (1, 49, 52, 2, 1492),
                     (1, 50, 55, 2, 1492), (1, 84, 55, 2, 1492),
                     (1, 85, 63, 9, 995), (1, 99, 63, 9, 995),
                     (2, 0, 52, 2, 1492), (2, 50, 55, 2, 1492), (2, 85, 63, 9, 995),
                     (3, 0, 52, 2, 1492), (3, 50, 55, 2, 1492), (3, 85, 63, 9, 995),
                 })
        {
            var hub = NewHub(routeType: 0, startWait: 0, npcClan: 1, npcSkillBar: [52, 55, 63, byte.MaxValue]);
            var target = CreateMob("SHAMAN_TARGET", hp: 100_000, clan: 5);
            target[LegacyAccountSnapshot.MobResistOffset + 2] = 50;
            AddPlayerWithMob(hub, 1, checked((short)(10 + distance)), 10, target);
            Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
                "Shaman_Inf mixed-bar fixture did not spawn.");
            Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
                "Could not seed the Shaman_Inf mixed-bar target.");

            var (snapshot, decision) = Plan(
                hub, npc.ConnectionId, 4, new(checked((short)(10 + distance)), 10),
                physicalDamageRandomFactor: 100, skillBarSelectionRoll: selectionRoll);
            Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                      { SkillIndex: var skill, SkillParm: 1, Motion: 4, Resist: var resist } &&
                  skill == expectedSkill && resist == expectedResist,
                $"Shaman_Inf distance {distance}, roll {selectionRoll} did not preserve the selected source fields.");
            Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
                  applied.Attack is { Damage: var damage, RemainingHp: var remainingHp, TargetDied: false } &&
                  damage == expectedDamage && remainingHp == 100_000 - expectedDamage,
                $"Shaman_Inf roll {selectionRoll} did not apply the selected resistance; status={applied.Status}, damage={applied.Attack?.Damage}.");
        }
    }

    private static void OrcMedicoFourthSlotDamagesTheSelectedNpcAndCapturesItsView()
    {
        var hub = NewHub(routeType: 0, startWait: 0);
        AddPlayer(hub, 1, "MEDICO_TARGET", 11, 10, clan: 5);
        var mob = CreateMob("ORC_MEDICO", hp: 400, clan: 2);
        new byte[] { 40, 40, 51, 29 }.CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        var npcId = hub.EnterNpc(mob, 10, 10, requestedConnectionId: 20_042, generateIndex: 42);
        Check(npcId == 20_042 && hub.SetNpcCombatState(npcId, LegacyNpcMode.Combat, 0, [1]),
            "Could not set up the active Orc_Medico leader fixture.");

        var (snapshot, decision) = Plan(hub, npcId, 4, new(10, 10), skillBarSelectionRoll: 25);
        Check(decision.Attack is { SkillIndex: 6 } && decision.AttackTargetId == npcId,
            "The row 29 branch did not select the low-HP generated NPC itself.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { TargetConnectionId: 20_042, Damage: 100, RemainingHp: 300, TargetDied: false,
                  SkillIndex: 6, Motion: 4, ViewRecipientConnectionIds: [1] } &&
              hub.TryGetNpcSnapshot(npcId, out var updatedNpc) && updatedNpc is not null &&
              LegacyMobCombatState.Read(updatedNpc.MobSnapshot).CurrentScore.Hp == 300,
            "InstanceType 6 must preserve the source's positive MaxHp/10 damage against the selected NPC and capture its visible recipients.");
    }

    private static void OrcMedicoFourthSlotSelectsAndDamagesItsLowerHpLeader()
    {
        var hub = NewHub(routeType: 0, startWait: 0);
        AddPlayer(hub, 1, "MEDICO_TARGET", 11, 10, clan: 5);
        var leaderMob = CreateMob("ORC_LEADER", hp: 200, clan: 2);
        var leaderId = hub.EnterNpc(leaderMob, 10, 11, requestedConnectionId: 20_043, generateIndex: 43);
        var medicoMob = CreateMob("ORC_MEDICO", hp: 400, clan: 1);
        new byte[] { 40, 40, 51, 29 }.CopyTo(medicoMob.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        var medicoId = hub.EnterNpc(medicoMob, 10, 10, requestedConnectionId: 20_042,
            generateIndex: 42, leaderConnectionId: leaderId);
        Check(leaderId == 20_043 && medicoId == 20_042 &&
              hub.SetNpcCombatState(medicoId, LegacyNpcMode.Combat, 0, [1]),
            "Could not set up the Orc_Medico follower and leader fixtures.");

        var (snapshot, decision) = Plan(hub, medicoId, 4, new(10, 10), skillBarSelectionRoll: 64);
        Check(decision.Attack is { SkillIndex: 6 } && decision.AttackTargetId == leaderId &&
              hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { TargetConnectionId: 20_043, Damage: 100, RemainingHp: 100,
                  SkillIndex: 6, Motion: 4, TargetIsNpc: true, TargetDied: false, ViewRecipientConnectionIds: [1] } &&
              hub.TryGetNpcSnapshot(medicoId, out var unchangedMedico) && unchangedMedico is not null &&
              LegacyMobCombatState.Read(unchangedMedico.MobSnapshot).CurrentScore.Hp == 400 &&
              hub.TryGetNpcSnapshot(leaderId, out var updatedLeader) && updatedLeader is not null &&
              LegacyMobCombatState.Read(updatedLeader.MobSnapshot).CurrentScore.Hp == 100,
            "The lower-HP leader must receive the source MaxHp/10 damage while the Orc_Medico stays alive.");
    }

    private static void OrcMedicoFourthSlotFailsClosedForDeadLeaderAndZeroDamage()
    {
        var deadLeaderHub = NewHub(routeType: 0, startWait: 0);
        AddPlayer(deadLeaderHub, 1, "MEDICO_TARGET", 11, 10, clan: 5);
        var deadLeaderMob = CreateMob("DEAD_LEADER", hp: 0, clan: 2);
        var deadLeaderId = deadLeaderHub.EnterNpc(deadLeaderMob, 10, 11,
            requestedConnectionId: 20_043, generateIndex: 43);
        var medicoMob = CreateMob("ORC_MEDICO", hp: 400, clan: 1);
        new byte[] { 40, 40, 51, 29 }.CopyTo(medicoMob.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        var medicoId = deadLeaderHub.EnterNpc(medicoMob, 10, 10,
            requestedConnectionId: 20_042, generateIndex: 42, leaderConnectionId: deadLeaderId);
        Check(deadLeaderId == 20_043 && medicoId == 20_042 &&
              deadLeaderHub.SetNpcCombatState(medicoId, LegacyNpcMode.Combat, 0, [1]),
            "Could not set up the dead-leader Orc_Medico fixture.");

        var (deadLeaderSnapshot, deadLeaderDecision) = Plan(
            deadLeaderHub, medicoId, 4, new(10, 10), skillBarSelectionRoll: 25);
        Check(deadLeaderDecision.Kind == LegacyMobAiDecisionKind.Unsupported &&
              deadLeaderHub.TryApplyGeneratedNpcAiDecision(deadLeaderSnapshot, deadLeaderDecision, out _) ==
                  LegacyNpcAiApplyStatus.Unsupported,
            "A linked dead leader must be rejected in planning and application rather than becoming Stale or self-targeting.");

        var tinyMaxHub = NewHub(routeType: 0, startWait: 0);
        AddPlayer(tinyMaxHub, 1, "MEDICO_TARGET", 11, 10, clan: 5);
        var tinyMaxMob = CreateMob("TINY_MAXHP", hp: 8, clan: 2);
        var tinyScore = LegacyScore.Read(tinyMaxMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            MaxHp = 9,
            Hp = 8,
        };
        tinyScore.Write(tinyMaxMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        new byte[] { 40, 40, 51, 29 }.CopyTo(tinyMaxMob.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        var tinyId = tinyMaxHub.EnterNpc(tinyMaxMob, 10, 10, requestedConnectionId: 20_044, generateIndex: 44);
        Check(tinyId == 20_044 && tinyMaxHub.SetNpcCombatState(tinyId, LegacyNpcMode.Combat, 0, [1]),
            "Could not set up the sub-ten-MaxHP Orc_Medico fixture.");
        var (tinySnapshot, tinyDecision) = Plan(tinyMaxHub, tinyId, 4, new(10, 10), skillBarSelectionRoll: 25);
        Check(tinyDecision.Kind == LegacyMobAiDecisionKind.Attack && tinyDecision.AttackTargetId == tinyId &&
              tinyMaxHub.TryApplyGeneratedNpcAiDecision(tinySnapshot, tinyDecision, out _) == LegacyNpcAiApplyStatus.Unsupported &&
              tinyMaxHub.TryGetNpcSnapshot(tinyId, out var unchangedTinyNpc) && unchangedTinyNpc is not null &&
              LegacyMobCombatState.Read(unchangedTinyNpc.MobSnapshot).CurrentScore.Hp == 8,
            "MaxHp below ten yields no positive MaxHp/10 damage and must fail closed without changing authoritative HP.");
    }

    private static void FeiticeiraFourthSlotAppliesSelfDamageWithSourceMotion()
    {
        var hub = NewHub(routeType: 0, startWait: 0);
        AddPlayer(hub, 1, "FEIT_TARGET", 12, 10, clan: 5);
        var mob = CreateMob("FEITICEIRA", hp: 900, clan: 7);
        new byte[] { 33, 36, 38, 27 }.CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        var npcId = hub.EnterNpc(mob, 10, 10, requestedConnectionId: 20_045, generateIndex: 45);
        Check(npcId == 20_045 && hub.SetNpcCombatState(npcId, LegacyNpcMode.Combat, 0, [1]),
            "Could not set up the low-HP Feiticeira generated NPC.");

        var (snapshot, decision) = Plan(hub, npcId, 4, new(10, 10), skillBarSelectionRoll: 25,
            skillBarMotionRoll: 2);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.AttackTargetId == npcId &&
              decision.Attack is { SkillIndex: 6, SkillParm: 1, Motion: 7, Resist: -1 },
            "The 7.69 Feiticeira ranged slot-3 branch did not preserve its InstanceType-6 self target and Motion roll.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { TargetConnectionId: 20_045, Damage: 100, RemainingHp: 800,
                  TargetDied: false, TargetIsNpc: true, SkillIndex: 6, Motion: 7, ViewRecipientConnectionIds: [1] } &&
              hub.TryGetNpcSnapshot(npcId, out var updatedNpc) && updatedNpc is not null &&
              LegacyMobCombatState.Read(updatedNpc.MobSnapshot).CurrentScore.Hp == 800,
            "The Feiticeira row-27 override did not apply MaxHP/10 to its generated NPC using the captured Motion roll.");
    }

    private static void BruxaFourthSlotAppliesSelfOrLeaderDamageWithSourceMotion()
    {
        var selfHub = NewHub(routeType: 0, startWait: 0);
        AddPlayer(selfHub, 1, "BRUXA_TARGET", 12, 10, clan: 5);
        var bruxaMob = CreateMob("BRUXA", hp: 900, clan: 7);
        new byte[] { 24, 27, 28, 27 }.CopyTo(bruxaMob.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        var bruxaId = selfHub.EnterNpc(bruxaMob, 10, 10, requestedConnectionId: 20_046, generateIndex: 46);
        Check(bruxaId == 20_046 && selfHub.SetNpcCombatState(bruxaId, LegacyNpcMode.Combat, 0, [1]),
            "Could not set up the low-HP Bruxa generated NPC.");

        var (selfSnapshot, selfDecision) = Plan(selfHub, bruxaId, 4, new(10, 10),
            skillBarSelectionRoll: 25, skillBarMotionRoll: 2);
        Check(selfDecision.Kind == LegacyMobAiDecisionKind.Attack && selfDecision.AttackTargetId == bruxaId &&
              selfDecision.Attack is { SkillIndex: 6, SkillParm: 1, Motion: 7, Resist: -1 },
            "The 7.69 Bruxa row-27 branch did not select self damage with its Motion roll.");
        Check(selfHub.TryApplyGeneratedNpcAiDecision(selfSnapshot, selfDecision, out var selfApplied) ==
                  LegacyNpcAiApplyStatus.Applied &&
              selfApplied.Attack is { TargetConnectionId: 20_046, Damage: 100, RemainingHp: 800,
                  TargetDied: false, TargetIsNpc: true, SkillIndex: 6, Motion: 7,
                  ViewRecipientConnectionIds: [1] } &&
              selfHub.TryGetNpcSnapshot(bruxaId, out var updatedBruxa) && updatedBruxa is not null &&
              LegacyMobCombatState.Read(updatedBruxa.MobSnapshot).CurrentScore.Hp == 800,
            "The Bruxa slot-3 branch did not apply positive MaxHP/10 damage to its generated NPC.");

        var leaderHub = NewHub(routeType: 0, startWait: 0);
        AddPlayer(leaderHub, 1, "BRUXA_TARGET", 12, 10, clan: 5);
        var leaderMob = CreateMob("BRUXA_LEADER", hp: 700, clan: 7);
        var leaderId = leaderHub.EnterNpc(leaderMob, 10, 11,
            requestedConnectionId: 20_048, generateIndex: 48);
        var followerMob = CreateMob("BRUXA_FOLLOWER", hp: 900, clan: 8);
        new byte[] { 24, 27, 28, 27 }.CopyTo(followerMob.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        var followerId = leaderHub.EnterNpc(followerMob, 10, 10,
            requestedConnectionId: 20_047, generateIndex: 47, leaderConnectionId: leaderId);
        Check(leaderId == 20_048 && followerId == 20_047 &&
              leaderHub.SetNpcCombatState(followerId, LegacyNpcMode.Combat, 0, [1]),
            "Could not set up the Bruxa_ follower and leader fixtures.");

        var (leaderSnapshot, leaderDecision) = Plan(leaderHub, followerId, 4, new(10, 10),
            skillBarSelectionRoll: 64, skillBarMotionRoll: 3);
        Check(leaderDecision.Kind == LegacyMobAiDecisionKind.Attack && leaderDecision.AttackTargetId == leaderId &&
              leaderDecision.Attack is { SkillIndex: 6, SkillParm: 1, Motion: 8, Resist: -1 } &&
              leaderHub.TryApplyGeneratedNpcAiDecision(leaderSnapshot, leaderDecision, out var leaderApplied) ==
                  LegacyNpcAiApplyStatus.Applied &&
              leaderApplied.Attack is { TargetConnectionId: 20_048, Damage: 100, RemainingHp: 600,
                  TargetDied: false, TargetIsNpc: true, SkillIndex: 6, Motion: 8,
                  ViewRecipientConnectionIds: [1] } &&
              leaderHub.TryGetNpcSnapshot(followerId, out var unchangedFollower) && unchangedFollower is not null &&
              LegacyMobCombatState.Read(unchangedFollower.MobSnapshot).CurrentScore.Hp == 900 &&
              leaderHub.TryGetNpcSnapshot(leaderId, out var updatedLeader) && updatedLeader is not null &&
              LegacyMobCombatState.Read(updatedLeader.MobSnapshot).CurrentScore.Hp == 600,
            "The Bruxa_ row-27 branch did not damage its valid lower-HP generated leader using the source Motion.");
    }

    private static void TorreRunicaFourthSlotAppliesItsSourceDamageToTheGeneratedNpc()
    {
        var hub = NewHub(routeType: 0, startWait: 0);
        AddPlayer(hub, 1, "TORRE_TARGET", 12, 10, clan: 5);
        var torreMob = CreateMob("TORRE_RUNICA", hp: 900, clan: 1);
        new byte[] { 255, 255, 255, 27 }.CopyTo(torreMob.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        var npcId = hub.EnterNpc(torreMob, 10, 10, requestedConnectionId: 20_050, generateIndex: 50);
        Check(npcId == 20_050 && hub.SetNpcCombatState(npcId, LegacyNpcMode.Combat, 0, [1]),
            "Could not set up the active Torre_Runica generated NPC.");

        var (snapshot, decision) = Plan(hub, npcId, 4, new(12, 10), skillBarSelectionRoll: 25, skillBarMotionRoll: 3);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.AttackTargetId == npcId &&
              decision.Attack is { SkillIndex: 6, SkillParm: 1, Motion: 8, Resist: -1 },
            "Torre_Runica slot 3 must override the all-255 physical base with the source SkillIndex 6 and ranged Motion 8.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { TargetConnectionId: 20_050, Damage: 100, RemainingHp: 800,
                  TargetDied: false, TargetIsNpc: true, SkillIndex: 6, Motion: 8, ViewRecipientConnectionIds: [1] } &&
              hub.TryGetNpcSnapshot(npcId, out var updatedTorre) && updatedTorre is not null &&
              LegacyMobCombatState.Read(updatedTorre.MobSnapshot).CurrentScore.Hp == 800,
            "Torre_Runica row 27 did not apply positive MaxHp/10 to itself inside the captured viewer set.");
    }

    private static void TrollDualSkillBarUsesSelectedSkillAndFireResistance()
    {
        var hub = NewHub(routeType: 0, startWait: 0, npcClan: 2, npcSkillBar: [32, 35, 32, 35]);
        var target = CreateMob("TROLL_TARGET", hp: 100_000, clan: 5);
        target[LegacyAccountSnapshot.MobResistOffset] = 50;
        AddPlayerWithMob(hub, 1, 11, 10, target);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Troll dual-skill-bar fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed the Troll dual-skill-bar target.");

        var (snapshot, decision) = Plan(
            hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100, skillBarSelectionRoll: 50);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                  { SkillIndex: 35, SkillParm: 1, Motion: 4, Resist: 0 },
            "The generated Troll-bar snapshot did not retain the selected secondary skill and source attack fields.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { Damage: 1492, RemainingHp: 98_508, TargetDied: false },
            $"Troll Tempestade de Meteoros selection should apply target Resist[0]; status={applied.Status}, damage={applied.Attack?.Damage}.");
    }

    private static void AttackSnapshotIsRejectedWhenTargetMoves()
    {
        var hub = NewHub(routeType: 0, startWait: 0, allEmptySkills: true);
        AddPlayer(hub, 1, "STALE_ATTACK", 11, 10, clan: 5);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Stale attack NPC fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]), "Could not seed stale attack target.");
        var (snapshot, decision) = Plan(hub, npc.ConnectionId, 4, new(10, 10));
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack, "The stale attack fixture did not produce Attack.");
        Check(hub.UpdatePosition(1, 80, 80), "Could not invalidate the captured target position.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out _) == LegacyNpcAiApplyStatus.Stale &&
              hub.TryGetCharacterSnapshot(1, out var targetMob, out _, out _) && targetMob is not null &&
              LegacyMobCombatState.Read(targetMob).CurrentScore.Hp == 1000 &&
              hub.TryGetNpcCombatState(npc.ConnectionId, out var mode, out var currentTarget, out var enemies) &&
              mode == LegacyNpcMode.Combat && currentTarget == 0 && enemies.SequenceEqual([1]),
            "WorldHub applied an attack after its target moved outside the captured view.");
    }

    private static void PhysicalRetaliationRunsOnlyAfterEligibleSourceTail()
    {
        var positiveHub = NewHub(routeType: 0, startWait: 0);
        AddPlayer(positiveHub, 1, "HITTER", 10, 11, clan: 5);
        var positiveNpc = SpawnCombatTarget(positiveHub, "Wolf", 42, 10, 10, affect: null);
        Check(positiveHub.TryApplyPhysicalAttack(1, positiveNpc, randomFactor: 100, out var positiveOutcome,
            attackMissRoll: 99) == LegacyPhysicalAttackResult.Accepted && positiveOutcome is not null && positiveOutcome.Damage > 0,
            "Positive physical hit fixture did not resolve.");
        Check(positiveHub.TryGetNpcCombatState(positiveNpc, out var positiveMode, out _, out var positiveEnemies) &&
              positiveMode == LegacyNpcMode.Combat && positiveEnemies.Contains(1),
            "A live generated NPC did not retaliate after the source-backed positive-damage tail.");

        var protectedHub = NewHub(routeType: 0, startWait: 0, targetClass: 1);
        AddPlayer(protectedHub, 1, "HITTER", 10, 11, clan: 5);
        var protection = new byte[LegacyAccountSnapshot.AffectStride];
        protection[0] = 6;
        var protectedNpc = SpawnCombatTarget(protectedHub, "Wolf", 42, 10, 10, protection);
        Check(protectedHub.TryApplyPhysicalAttack(1, protectedNpc, 100, out var protectedOutcome, attackMissRoll: 99) ==
              LegacyPhysicalAttackResult.Accepted && protectedOutcome is { Damage: 0 },
            "Type 6 early-continue fixture did not produce zero damage.");
        Check(protectedHub.TryGetNpcCombatState(protectedNpc, out var protectedMode, out _, out var protectedEnemies) &&
              protectedMode == LegacyNpcMode.Peace && protectedEnemies.All(static id => id == 0),
            "A zero-damage Type 6 branch before the source tail must not trigger retaliation.");

        var zardHub = NewHub(routeType: 0, startWait: 0);
        AddPlayer(zardHub, 1, "HITTER", 10, 11, clan: 5);
        var zardNpc = SpawnCombatTarget(zardHub, "Zard", 42, 10, 10, affect: null);
        Check(zardHub.TryApplyPhysicalAttack(1, zardNpc, 100, out var zardOutcome, attackMissRoll: 99) ==
              LegacyPhysicalAttackResult.Accepted && zardOutcome?.ZardMobLeftCount == 1,
            "Late Zard zero-marker fixture did not resolve.");
        Check(zardHub.TryGetNpcCombatState(zardNpc, out var zardMode, out _, out var zardEnemies) &&
              zardMode == LegacyNpcMode.Combat && zardEnemies.Contains(1),
            "The late Zard zero-damage marker reaches the source tail and must still trigger retaliation.");
    }

    private static void PhysicalAttackUsesDamageRollRangeAndMissMarker()
    {
        for (var factor = 99; factor <= 110; factor++)
        {
            var hub = NewHub(routeType: 0, startWait: 0, allEmptySkills: true);
            var targetMob = CreateMob("DAMAGE_TARGET", hp: 100_000, clan: 5);
            targetMob.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4).Fill(100);
            AddPlayerWithMob(hub, 1, 11, 10, targetMob);
            Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
                $"Damage roll {factor} NPC fixture did not spawn.");
            Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]), "Could not seed damage-test target.");
            var (snapshot, decision) = Plan(hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: factor);
            Check(decision.Kind == LegacyMobAiDecisionKind.Attack &&
                  hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
                  applied.Attack is { Missed: false } &&
                  applied.Attack.Damage == LegacySkillCombatMath.GetPhysicalDamage(1000, 10, combat: 0, factor) &&
                  applied.Attack.RemainingHp == 100_000 - applied.Attack.Damage,
                $"BASE_GetDamage roll {factor} diverged or NPC attack incorrectly applied elemental resistance.");
        }

        var missHub = NewHub(routeType: 0, startWait: 0, allEmptySkills: true);
        var dodger = CreateMob("DODGER", hp: 1000, clan: 5);
        var score = LegacyScore.Read(dodger.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        (score with { Dexterity = 10_000 }).Write(dodger.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        AddPlayerWithMob(missHub, 1, 11, 10, dodger);
        Check(missHub.TrySpawnGeneratedNpc(42, out var missNpc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && missNpc is not null,
            "Miss-marker NPC fixture did not spawn.");
        Check(missHub.SetNpcCombatState(missNpc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]), "Could not seed miss-marker target.");
        var (missSnapshot, missDecision) = Plan(missHub, missNpc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100, attackMissRoll: 1);
        Check(missDecision.Kind == LegacyMobAiDecisionKind.Attack &&
              missHub.TryApplyGeneratedNpcAiDecision(missSnapshot, missDecision, out var missed) == LegacyNpcAiApplyStatus.Applied &&
              missed.Attack is { Missed: true, Damage: -3, RemainingHp: 1000 } &&
              missHub.TryGetCharacterSnapshot(1, out var unchangedMob, out _, out _) && unchangedMob is not null &&
              LegacyMobCombatState.Read(unchangedMob).CurrentScore.Hp == 1000,
            "The source 1..100 attack-miss roll did not emit marker -3 without reducing HP.");
    }

    private static void PhysicalAttackSetsAuthoritativeHpToZero()
    {
        var hub = NewHub(routeType: 0, startWait: 0, allEmptySkills: true);
        AddPlayerWithMob(hub, 1, 11, 10, CreateMob("LOW_HP", hp: 100, clan: 5));
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Lethal attack NPC fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]), "Could not seed lethal target.");
        var (snapshot, decision) = Plan(hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack &&
              hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { Damage: 995, RemainingHp: 0, TargetDied: true } &&
              LegacyMobCombatState.Read(applied.Attack.TargetMobSnapshot).CurrentScore.Hp == 0,
            "A lethal generated-NPC attack did not persist zero HP and return the death boundary to its caller.");
    }

    private static void PhysicalAttackRunsPlayerResurrectionFinalizer()
    {
        var hub = NewHub(routeType: 0, startWait: 0, allEmptySkills: true);
        var targetMob = CreateMob("SCROLL_TARGET", hp: 100, clan: 5);
        new LegacyItem(3463, 61, 2, 0, 0, 0, 0).Write(
            targetMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes));
        AddPlayerWithMob(hub, 1, 11, 10, targetMob);
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Resurrection-scroll NPC fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "Could not seed resurrection-scroll target.");
        var (snapshot, decision) = Plan(hub, npc.ConnectionId, 4, new(10, 10), physicalDamageRandomFactor: 100);
        Check(decision.Kind == LegacyMobAiDecisionKind.Attack &&
              hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var applied) == LegacyNpcAiApplyStatus.Applied &&
              applied.Attack is { TargetRevived: true, TargetDied: false, RemainingHp: 1000, ConsumedItemSlot: 0,
                  ConsumedItem: { Index: 3463, Effect1: 61, Value1: 1 } } &&
              LegacyMobCombatState.Read(applied.Attack.TargetMobSnapshot).CurrentScore is { Hp: 1000, Mp: 1000 },
            "A lethal generated-NPC hit did not run the existing resurrection-scroll finalizer for its player target.");
    }

    private static void NpcsWithoutPlayingPlayerTargetsDoNotAttack()
    {
        var hub = NewHub(routeType: 0, startWait: 0, allEmptySkills: true);
        Check(hub.Enter(1, "ENTER_ONLY", static (_, _) => ValueTask.CompletedTask), "Enter-only target setup failed.");
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && npc is not null,
            "Enter-only target NPC fixture did not spawn.");
        Check(hub.SetNpcCombatState(npc!.ConnectionId, LegacyNpcMode.Combat, 0, [1]), "Could not seed Enter-only enemy.");
        var (snapshot, decision) = Plan(hub, npc.ConnectionId, 4, new(10, 10));
        Check(decision.Kind != LegacyMobAiDecisionKind.Attack &&
              hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out _) == LegacyNpcAiApplyStatus.Applied,
            "An Enter-only connection without USER_PLAY character state became a physical NPC attack target.");
    }

    private static void SkillRetaliationRunsOnlyAfterEligibleSourceTail()
    {
        var positiveHub = NewHub(routeType: 0, startWait: 0);
        AddPlayer(positiveHub, 1, "SKILL_HITTER", 10, 11, clan: 5);
        var positiveNpc = SpawnCombatTarget(positiveHub, "Wolf", 42, 10, 10, affect: null);
        var skill = new LegacySkillDefinition(10, 69, 1, 20, 15, 1, 1, 40, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 2, 0, 0, 1, 0, "Golpe_Mortal");
        var itemData = CreateItemData();
        Check(positiveHub.TryApplySkillAttack(1, positiveNpc, skill, itemData, weather: 0, randomFactor: 90, out var outcome) ==
              LegacySkillAttackResult.Accepted && outcome is { Damage: > 0 },
            "Positive NPC skill fixture did not resolve.");
        Check(positiveHub.TryGetNpcCombatState(positiveNpc, out var mode, out _, out var enemies) &&
              mode == LegacyNpcMode.Combat && enemies.Contains(1),
            "A positive NPC skill slot did not retaliate after reaching the source tail.");

        var protectedHub = NewHub(routeType: 0, startWait: 0, targetClass: 1);
        AddPlayer(protectedHub, 1, "SKILL_HITTER", 10, 11, clan: 5);
        var protection = new byte[LegacyAccountSnapshot.AffectStride];
        protection[0] = 6;
        var protectedNpc = SpawnCombatTarget(protectedHub, "Wolf", 42, 10, 10, protection);
        Check(protectedHub.TryApplySkillAttack(1, protectedNpc, skill, itemData, weather: 0, randomFactor: 90, out var protectedOutcome) ==
              LegacySkillAttackResult.Accepted && protectedOutcome is { Damage: 0 },
            "Type 6 NPC skill fixture did not produce the source early-zero branch.");
        Check(protectedHub.TryGetNpcCombatState(protectedNpc, out var protectedMode, out _, out var protectedEnemies) &&
              protectedMode == LegacyNpcMode.Peace && protectedEnemies.All(static id => id == 0),
            "A zero-damage skill branch before the source tail must not trigger retaliation.");
    }

    private static void PlannerSnapshotUsesMerchantAndGridOccupancy()
    {
        var merchantHub = NewHub(routeType: 0, startWait: 0);
        AddPlayer(merchantHub, 1, "MERCHANT_TARGET", 11, 10, clan: 3, merchant: true);
        Check(merchantHub.TrySpawnGeneratedNpc(42, out var merchantNpc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && merchantNpc is not null,
            "Merchant planner fixture did not spawn.");
        Check(merchantHub.SetNpcCombatState(merchantNpc!.ConnectionId, LegacyNpcMode.Combat, 1, [1]), "Could not set merchant enemy.");
        Check(merchantHub.TryCreateGeneratedNpcAiSnapshot(merchantNpc.ConnectionId, 4, new(10, 10), 0, 0, out var merchantSnapshot) && merchantSnapshot is not null,
            "Merchant planner snapshot was unavailable.");
        var merchantTarget = merchantSnapshot!.PlannerInput.Targets.Single(static target => target.Id == 1);
        var merchantAdmission = LegacyMobAiPlanner.TrySetBattle(merchantSnapshot.PlannerInput.Mob, merchantTarget);
        Check(merchantTarget.Merchant && merchantAdmission.Kind == LegacyMobAiDecisionKind.Idle && merchantAdmission.Mode == LegacyMobAiMode.Combat,
            "The planner adapter must use MOB.Merchant bit 1 at offset 17 for EnemyList filtering.");

        var overlapHub = NewHub(routeType: 0, startWait: 0, allEmptySkills: true);
        AddPlayer(overlapHub, 1, "GRID_OCCUPANT", 11, 10, clan: 5);
        AddPlayer(overlapHub, 2, "OVERLAPPED", 11, 10, clan: 3);
        Check(overlapHub.TrySpawnGeneratedNpc(42, out var overlapNpc, randomRoll: static _ => 0, positionX: 10, positionY: 10) && overlapNpc is not null,
            "Overlap planner fixture did not spawn.");
        Check(overlapHub.TryCreateGeneratedNpcAiSnapshot(overlapNpc!.ConnectionId, 4, new(10, 10), 0, 0, out var overlapSnapshot) && overlapSnapshot is not null,
            "Overlap planner snapshot was unavailable.");
        var overlappedTarget = overlapSnapshot!.PlannerInput.Targets.Single(static target => target.Id == 2);
        Check(overlapSnapshot.PlannerInput.Targets.Single(static target => target.Id == 1).IsVisible && !overlappedTarget.IsVisible &&
              LegacyMobAiPlanner.Plan(overlapSnapshot.PlannerInput).Kind != LegacyMobAiDecisionKind.Aggro,
            "Peace acquisition should keep the overlapped target in the snapshot but ignore it as not visible in the grid.");
        Check(overlapHub.SetNpcCombatState(overlapNpc.ConnectionId, LegacyNpcMode.Combat, 0, [2]), "Could not set overlapped EnemyList target.");
        Check(overlapHub.TryCreateGeneratedNpcAiSnapshot(overlapNpc.ConnectionId, 4, new(10, 10), 0, 0, out var combatOverlapSnapshot) && combatOverlapSnapshot is not null,
            "Combat-overlap planner snapshot was unavailable.");
        var combatOverlapDecision = LegacyMobAiPlanner.Plan(combatOverlapSnapshot!.PlannerInput);
        Check(combatOverlapDecision.CurrentTarget == 2 && combatOverlapDecision.Kind == LegacyMobAiDecisionKind.Attack,
            "Combat SelectTarget must resolve the explicit EnemyList ID directly even when it is not the grid-visible occupant.");
    }

    private static (LegacyNpcAiPlanningSnapshot Snapshot, LegacyMobAiDecision Decision) Plan(
        WorldHub hub,
        int npcId,
        int attackRange,
        LegacyMobAiPoint? routeStep,
        int physicalDamageRandomFactor = 100,
        int attackMissRoll = 100,
        int skillBarSelectionRoll = 0,
        int skillBarMotionRoll = 0)
    {
        Check(hub.TryCreateGeneratedNpcAiSnapshot(npcId, attackRange, routeStep, 0, 0, out var snapshot,
                physicalDamageRandomFactor, attackMissRoll, skillBarSelectionRoll, skillBarMotionRoll) && snapshot is not null,
            "WorldHub did not create a planner snapshot.");
        return (snapshot!, LegacyMobAiPlanner.Plan(snapshot!.PlannerInput));
    }

    private static WorldHub NewHub(
        int routeType,
        int startWait,
        bool allEmptySkills = false,
        byte targetClass = 0,
        byte[]? npcSkillBar = null,
        byte npcClan = 2,
        short[]? npcBaseSpecial = null,
        int generateIndex = 42,
        bool terminalRoute = false,
        int npcAttackRun = 0,
        int npcBaseDexterity = 0,
        LegacyMapGrid? mapGrid = null,
        byte npcMerchant = 0,
        byte currentScoreMerchant = 0)
    {
        var mob = CreateMob("Wolf", hp: 1000, clan: npcClan, characterClass: targetClass, emptySkills: allEmptySkills);
        mob[LegacyAccountSnapshot.MobMerchantOffset] = npcMerchant;
        var baseMobScore = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset)) with
        {
            AttackRun = checked((byte)npcAttackRun),
            Dexterity = checked((short)npcBaseDexterity)
        };
        baseMobScore.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        var currentMobScore = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            AttackRun = checked((byte)npcAttackRun),
            Merchant = currentScoreMerchant
        };
        currentMobScore.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        if (npcBaseSpecial is not null)
        {
            if (npcBaseSpecial.Length != 4)
                throw new ArgumentException("A generated NPC BaseScore.Special fixture must have four entries.", nameof(npcBaseSpecial));
            var baseScore = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset)) with
            {
                Special1 = npcBaseSpecial[0],
                Special2 = npcBaseSpecial[1],
                Special3 = npcBaseSpecial[2],
                Special4 = npcBaseSpecial[3]
            };
            baseScore.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        }
        if (npcSkillBar is not null)
        {
            if (npcSkillBar.Length != 4)
                throw new ArgumentException("A generated NPC SkillBar fixture must have four entries.", nameof(npcSkillBar));
            npcSkillBar.AsSpan().CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        }
        var terminalSegments = terminalRoute
            ? "Segment2X: 10\nSegment2Y: 10\nSegment3X: 10\nSegment3Y: 10"
            : string.Empty;
        var text = $"""
            #[{generateIndex}]
            MaxNumMob: 10
            Leader: Wolf
            RouteType: {routeType}
            StartX: 10
            StartY: 10
            StartRange: 4
            StartWait: {startWait}
            Segment1X: {(terminalRoute ? 10 : 20)}
            Segment1Y: 10
            {terminalSegments}
            DestX: {(terminalRoute ? 10 : 30)}
            DestY: 10
            """;
        var catalog = LegacyNpcGenerationCatalog.Parse(text, new Dictionary<string, byte[]> { ["Wolf"] = mob });
        return new WorldHub(mapGrid: mapGrid, npcGenerationCatalog: catalog, skillData: MobAiSkillDataFixture.Create());
    }

    private static int SpawnCombatTarget(WorldHub hub, string name, int generateIndex, short x, short y, byte[]? affect)
    {
        var mob = CreateMob(name, hp: 1000, clan: 2);
        var id = hub.EnterNpc(mob, x, y, affect ?? default, requestedConnectionId: generateIndex + 20_000, generateIndex: generateIndex);
        Check(id == generateIndex + 20_000, $"Generated attack target {name} did not register.");
        Check(hub.SetNpcCombatState(id, LegacyNpcMode.Peace, 0, new int[13]), "Could not initialize target NPC state.");
        return id;
    }

    private static void AddPlayer(WorldHub hub, int id, string name, short x, short y, int clan, bool merchant = false)
    {
        Check(hub.Enter(id, name, static (_, _) => ValueTask.CompletedTask), "Player Enter failed.");
        Check(hub.SetCharacterState(id, 0, 0, clan, 0, 0, x, y, CreateMob(name, hp: 1000, clan: (byte)clan, merchant: merchant)),
            "Player character snapshot setup failed.");
    }

    private static void AddPlayerWithMob(WorldHub hub, int id, short x, short y, byte[] mob)
    {
        Check(hub.Enter(id, $"PLAYER_{id}", static (_, _) => ValueTask.CompletedTask), "Player Enter failed.");
        Check(hub.SetCharacterState(id, 0, 0, mob[LegacyAccountSnapshot.MobClanOffset], 0, 0, x, y, mob),
            "Player character snapshot setup failed.");
    }

    private static byte[] CreateMob(string name, int hp, byte clan = 2, byte characterClass = 0, bool emptySkills = false, bool merchant = false)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset, 16));
        mob[LegacyAccountSnapshot.MobClanOffset] = clan;
        mob[LegacyAccountSnapshot.MobClassOffset] = characterClass;
        if (merchant)
            mob[LegacyAccountSnapshot.MobMerchantOffset] |= 1;
        new LegacyScore(100, 10, 1000, 0, 0, 0, 0, 1000, 1000, 1000, 100, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        new LegacyScore(100, 10, 1000, 0, 0, 0, 0, 1000, 1000, hp, 100, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        if (emptySkills)
            mob.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4).Fill(byte.MaxValue);
        return mob;
    }

    private static LegacyItemDataTable CreateItemData()
        => LegacyItemDataTable.Load(Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

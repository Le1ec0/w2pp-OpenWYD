using WydCdk.World;

internal static class LegacyMobAiPlannerTests
{
    private static readonly LegacySkillDataTable SkillData = MobAiSkillDataFixture.Create();

    public static void Run()
    {
        EmptyAndInvalidTargetsDoNotSelect();
        InactiveTargetsCannotBeSelectedAndPassiveAcquisitionRequiresVisibility();
        PassiveAcquisitionUsesGridOrderHostilityAndLeaderGate();
        SelectsNearestTargetWithNpcPenaltyAndLastSlotTie();
        SelectionUsesSourceRangesAndDoesNotFilterMerchantOrHighLevelPlayers();
        SelectionClearsHiddenDeadAndOutOfWindowButPreservesInvalidIds();
        SetBattleUsesInclusiveWindowAndInsertionFiltersAndCapacity();
        GetAttackUsesSkillDataForAnUnlistedMixedSkillBar();
        GetAttackMapsSpecialTwoMotionForAnyMixedSkillBar();
        GetAttackUsesTheFourthSlotInstanceTypeForAnyMixedSkillBar();
        GetAttackDoesNotUseExactLayoutFallbackWithoutSkillData();
        GetAttackPreservesDefaultSkillParmWhenSpecialTwoIsZero();
        GetAttackUsesGenericSkillDataAndSupportsPhysicalFallback();
        GetAttackSelectsDistancePairsAndRawSlotBoundaries();
        GetAttackUsesBaseSpecialForClanFour();
        GetAttackMapsSpecialSkillCodes();
        GetAttackRejectsClanFourWithoutBaseSpecialAndNormalNpcTargetTails();
        BattleProcessorDropsToPeaceAndHonorsInclusiveLeashAndChanceRolls();
        DexterityGateRequestsRetreatForCloseRangedNpc();
        RouteTypeFiveCombatDespawnsWithoutAValidPlayerLeader();
        RunequestLv6AreaDespawnPrecedesPeaceAndCombatProcessing();
        FollowersUseTmRouteTypeFiveRulesAndExplicitSummonerPosition();
        RouteWaitAdvancesSegmentsAndMissingActionsAreNoOp();
    }

    private static void GetAttackUsesSkillDataForAnUnlistedMixedSkillBar()
    {
        var input = Input(
            Mob(mode: LegacyMobAiMode.Combat, enemyIds: [1], skillBar: [17, 255, 40, 255]),
            Targets(Player(1, 101, 100)),
            dexterityRoll: 0,
            skillBarSelectionRoll: 85) with
        {
            SkillData = CreateSkillData((17, 2), (40, 4))
        };

        var decision = LegacyMobAiPlanner.Plan(input);

        Assert(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is { SkillIndex: 40, SkillParm: 1, Motion: 4, Resist: 2 },
            $"GetAttack must apply the source's generic slot selector and derive resistance from SkillData for an unlisted layout; got {decision.Kind}/{decision.Attack}.");
    }

    private static LegacySkillDataTable CreateSkillData(params (int Id, int InstanceType)[] rows)
    {
        var text = string.Join(Environment.NewLine, rows.Select(static row =>
            $"{row.Id},0,0,0,0,0,{row.InstanceType},0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,Skill{row.Id}"));
        return LegacySkillDataTable.Load(new StringReader(text));
    }

    private static void GetAttackMapsSpecialTwoMotionForAnyMixedSkillBar()
    {
        var expectedBySpecialTwo = new Dictionary<int, int[]>
        {
            [3] = [5, 5, 4, 4],
            [6] = [5, 5, 6, 6],
            [7] = [6, 5, 6, 6],
            [15] = [4, 5, 6, 7],
            [24] = [7, 7, 8, 8],
            [27] = [4, 5, 7, 8],
            [23] = [4, 5, 6, 8]
        };
        foreach (var (specialTwo, expectedMotions) in expectedBySpecialTwo)
        for (var motionRoll = 0; motionRoll < expectedMotions.Length; motionRoll++)
        {
            var input = Input(
                Mob(mode: LegacyMobAiMode.Combat, enemyIds: [1], skillBar: [17, (byte)specialTwo, 40, (byte)specialTwo]),
                Targets(Player(1, 101, 100)),
                dexterityRoll: 0,
                skillBarSelectionRoll: 0,
                skillBarMotionRoll: motionRoll);

            var decision = LegacyMobAiPlanner.Plan(input);
            Assert(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is { SkillIndex: 17, SkillParm: 1 } attack &&
                   attack.Motion == expectedMotions[motionRoll] && attack.Resist == 0,
                $"GetAttack special2={specialTwo} produced the wrong Motion or selector result for roll {motionRoll}.");
        }
    }

    private static void GetAttackUsesTheFourthSlotInstanceTypeForAnyMixedSkillBar()
    {
        var input = Input(
            Mob(mode: LegacyMobAiMode.Combat, enemyIds: [1], skillBar: [17, 27, 40, 29], hp: 800, maxHp: 1000),
            Targets(Player(1, 101, 100)),
            dexterityRoll: 0,
            skillBarSelectionRoll: 25,
            skillBarMotionRoll: 2) with
        {
            SkillData = CreateSkillData((17, 2), (27, 6), (29, 6), (40, 4))
        };

        var decision = LegacyMobAiPlanner.Plan(input);

        Assert(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.AttackTargetId == 20_000 &&
               decision.Attack is { SkillIndex: 6, SkillParm: 1, Motion: 7, Resist: -1 },
            "GetAttack must use the selected fourth-slot SkillData.InstanceType and low-HP return for an unlisted layout.");
    }

    private static void GetAttackDoesNotUseExactLayoutFallbackWithoutSkillData()
    {
        var input = Input(
            Mob(mode: LegacyMobAiMode.Combat, enemyIds: [1], skillBar: [32, 32, 32, 255]),
            Targets(Player(1, 101, 100)),
            dexterityRoll: 0,
            skillBarSelectionRoll: 0) with { SkillData = null };

        var decision = LegacyMobAiPlanner.Plan(input);

        Assert(decision.Kind == LegacyMobAiDecisionKind.Unsupported,
            "GetAttack must fail closed when SkillData is absent instead of relying on an exact-layout fallback.");
    }

    private static void GetAttackPreservesDefaultSkillParmWhenSpecialTwoIsZero()
    {
        var input = Input(
            Mob(mode: LegacyMobAiMode.Combat, enemyIds: [1], skillBar: [17, 0, 40, 0]),
            Targets(Player(1, 101, 100)),
            dexterityRoll: 0,
            skillBarSelectionRoll: 0);

        var decision = LegacyMobAiPlanner.Plan(input);

        Assert(decision.Kind == LegacyMobAiDecisionKind.Attack &&
               decision.Attack is { SkillIndex: 17, SkillParm: 1, Motion: 0, Resist: 0 },
            "GetAttack must preserve SkillParm=1 from the common special branch and Motion=0 when special2 is zero.");
    }

    private static void GetAttackSelectsDistancePairsAndRawSlotBoundaries()
    {
        foreach (var (distance, expectedMotion) in new[] { (1, 4), (2, 7) })
        {
            var decision = LegacyMobAiPlanner.Plan(Input(
                Mob(mode: LegacyMobAiMode.Combat, enemyIds: [1], skillBar: [17, 3, 40, 27]),
                Targets(Player(1, 100 + distance, 100)),
                dexterityRoll: 0,
                skillBarSelectionRoll: 0,
                skillBarMotionRoll: 2));
            Assert(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                       { SkillIndex: 17, SkillParm: 1, Resist: 0 } attack && attack.Motion == expectedMotion,
                $"GetAttack did not switch its distance pair at the 7.69 boundary for distance {distance}.");
        }

        foreach (var (selectionRoll, expectedSkill, expectedResist) in new[]
                 { (0, 17, 0), (49, 17, 0), (50, 24, 2), (84, 24, 2), (85, 40, 2), (99, 40, 2) })
        {
            var decision = LegacyMobAiPlanner.Plan(Input(
                Mob(mode: LegacyMobAiMode.Combat, enemyIds: [1], skillBar: [17, 24, 40, 255]),
                Targets(Player(1, 101, 100)),
                dexterityRoll: 0,
                skillBarSelectionRoll: selectionRoll,
                skillBarMotionRoll: 0));
            Assert(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is
                       { SkillParm: 1, Motion: 7 } attack && attack.SkillIndex == expectedSkill && attack.Resist == expectedResist,
                $"GetAttack selected the wrong SkillBar slot for roll {selectionRoll}.");
        }
    }

    private static void GetAttackMapsSpecialSkillCodes()
    {
        var expected = new (int SkillIndex, int SkillParm, int Motion)[]
        {
            (103, 5, 0), (105, 1, 0), (101, 1, 0), (101, 2, 0),
            (40, 1, 0), (40, 2, 0), (40, 3, 0), (33, -4, -4)
        };
        foreach (var special in Enumerable.Range(121, 8))
        {
            var decision = LegacyMobAiPlanner.Plan(Input(
                Mob(mode: LegacyMobAiMode.Combat, enemyIds: [1], skillBar: [(byte)special, 0, 255, 255]),
                Targets(Player(1, 101, 100)), dexterityRoll: 0,
                skillBarSelectionRoll: 99));
            var mapped = expected[special - 121];
            Assert(decision.Kind == LegacyMobAiDecisionKind.Attack && decision.Attack is { Resist: -1 } attack &&
                   attack.SkillIndex == mapped.SkillIndex && attack.SkillParm == mapped.SkillParm && attack.Motion == mapped.Motion,
                $"GetAttack special {special} did not produce the 7.69 skill/parm/motion mapping; got {decision.Kind}/{decision.Attack}.");
        }
    }

    private static void GetAttackRejectsClanFourWithoutBaseSpecialAndNormalNpcTargetTails()
    {
        var clanFour = LegacyMobAiPlanner.Plan(Input(
            Mob(clan: 4, mode: LegacyMobAiMode.Combat, enemyIds: [1], skillBar: [17, 3, 40, 255]),
            Targets(Player(1, 101, 100)), dexterityRoll: 0));
        var npcTarget = LegacyMobAiPlanner.Plan(Input(
            Mob(mode: LegacyMobAiMode.Combat, enemyIds: [1002], skillBar: [17, 255, 40, 255]),
            Targets(Npc(1002, 101, 100) with { MaxHp = 1_000, IsGeneratedNpc = true }),
            dexterityRoll: 0));
        Assert(clanFour.Kind == LegacyMobAiDecisionKind.Unsupported &&
               npcTarget.Kind == LegacyMobAiDecisionKind.Unsupported,
            "Clan 4 must fail closed without its four BaseScore.Special values, and normal NPC-target tails remain outside this planner.");
    }

    private static void GetAttackUsesBaseSpecialForClanFour()
    {
        var mapped = LegacyMobAiPlanner.Plan(Input(
            Mob(clan: 4, mode: LegacyMobAiMode.Combat, enemyIds: [1], skillBar: [255, 255, 255, 255]) with
            {
                BaseSpecial = [121, 0, 0, 0]
            },
            Targets(Player(1, 101, 100)),
            dexterityRoll: 0,
            skillBarSelectionRoll: 99));

        Assert(mapped.Kind == LegacyMobAiDecisionKind.Attack &&
               mapped.Attack is { SkillIndex: 103, SkillParm: 5, Motion: 0, Resist: -1 },
            $"Clan 4 GetAttack must map BaseScore.Special[0]=121 to skill 103/parm 5; got {mapped.Kind}/{mapped.Attack}.");

        var ranged = LegacyMobAiPlanner.Plan(Input(
            Mob(clan: 4, mode: LegacyMobAiMode.Combat, enemyIds: [1], skillBar: [255, 255, 255, 255]) with
            {
                BaseSpecial = [121, 0, 128, 3]
            },
            Targets(Player(1, 102, 100)),
            dexterityRoll: 0,
            skillBarSelectionRoll: 99,
            skillBarMotionRoll: 1));
        Assert(ranged.Kind == LegacyMobAiDecisionKind.Attack &&
               ranged.Attack is { SkillIndex: 33, SkillParm: -4, Motion: 5, Resist: -1 },
            $"Clan 4 GetAttack must select BaseScore.Special[2..3] at distance 2 while retaining the source skill parameter; got {ranged.Kind}/{ranged.Attack}.");

        var selectedOverride = LegacyMobAiPlanner.Plan(Input(
            Mob(clan: 4, mode: LegacyMobAiMode.Combat, enemyIds: [1], skillBar: [17, 255, 255, 255]) with
            {
                BaseSpecial = [121, 0, 128, 3]
            },
            Targets(Player(1, 101, 100)),
            dexterityRoll: 0,
            skillBarSelectionRoll: 0) with
        {
            SkillData = CreateSkillData((17, 2))
        });
        Assert(selectedOverride.Kind == LegacyMobAiDecisionKind.Attack &&
               selectedOverride.Attack is { SkillIndex: 17, SkillParm: 5, Motion: 0, Resist: 0 },
            "Clan 4 GetAttack must apply the common SkillBar selector after the BaseScore.Special mapping.");

        var heal = LegacyMobAiPlanner.Plan(Input(
            Mob(clan: 4, mode: LegacyMobAiMode.Combat, enemyIds: [1], skillBar: [255, 255, 255, 29], hp: 800, maxHp: 1_000) with
            {
                BaseSpecial = [121, 0, 0, 0]
            },
            Targets(Player(1, 101, 100)),
            dexterityRoll: 0,
            skillBarSelectionRoll: 25) with
        {
            SkillData = CreateSkillData((29, 6))
        });
        Assert(heal.Kind == LegacyMobAiDecisionKind.Attack && heal.AttackTargetId == 20_000 &&
               heal.Attack is { SkillIndex: 6, SkillParm: 5, Motion: 0, Resist: -1 },
            "Clan 4 GetAttack's fourth SkillBar slot must retain the mapped SkillParm when it returns the low-HP self-heal.");

        var invalidSpecial = LegacyMobAiPlanner.Plan(Input(
            Mob(clan: 4, mode: LegacyMobAiMode.Combat, enemyIds: [1], skillBar: [255, 255, 255, 255]) with
            {
                BaseSpecial = [400, 0, 0, 0]
            },
            Targets(Player(1, 101, 100)), dexterityRoll: 0));
        Assert(invalidSpecial.Kind == LegacyMobAiDecisionKind.Unsupported,
            "Clan 4 GetAttack must reject an out-of-range BaseScore.Special instead of emitting a possible invalid skill index.");
    }

    private static void GetAttackUsesGenericSkillDataAndSupportsPhysicalFallback()
    {
        var selectedAbility = LegacyMobAiPlanner.Plan(Input(
            Mob(x: 0, y: 0, segmentX: 0, segmentY: 0, mode: LegacyMobAiMode.Combat,
                enemyIds: [1], skillBar: [0, 255, 255, 255], baseDex: 99),
            Targets(Player(1, 2, 0)), dexterityRoll: 99));
        var emptyBar = LegacyMobAiPlanner.Plan(Input(
            Mob(x: 0, y: 0, segmentX: 0, segmentY: 0, mode: LegacyMobAiMode.Combat,
                enemyIds: [1], skillBar: [255, 255, 255, 255], baseDex: 99),
            Targets(Player(1, 2, 0)), dexterityRoll: 99));

        Assert(selectedAbility.Kind == LegacyMobAiDecisionKind.Attack && selectedAbility.Attack is
                   { SkillIndex: 0, SkillParm: 1, Motion: 4, Resist: 2 } &&
               emptyBar.Kind == LegacyMobAiDecisionKind.Attack && emptyBar.Attack is
                   { SkillIndex: -1, SkillParm: 1, Motion: 4, Resist: -1 },
            "GetAttack must accept a selected valid skill from SkillData and retain the all-255 physical fields.");
    }

    private static void EmptyAndInvalidTargetsDoNotSelect()
    {
        var empty = LegacyMobAiPlanner.Plan(Input(Mob(mode: LegacyMobAiMode.Combat, enemyIds: [0, 0])));
        Assert(empty.Kind == LegacyMobAiDecisionKind.Idle && empty.CurrentTarget == 0,
            "An empty enemy list must leave the mob idle with no target.");

        var invalid = LegacyMobAiPlanner.Plan(Input(Mob(mode: LegacyMobAiMode.Combat, enemyIds: [25000])));
        Assert(invalid.Kind == LegacyMobAiDecisionKind.Idle && invalid.CurrentTarget == 0 &&
               invalid.EnemyIds[0] == 25000 && invalid.EnemyIds.Skip(1).All(static id => id == 0),
            "Invalid target ids must be skipped without clearing their enemy-list slots.");

        var missingAndInvalid = LegacyMobAiPlanner.Plan(Input(
            Mob(mode: LegacyMobAiMode.Combat, enemyIds: [1, 2, 25000]),
            Targets(Player(2, 2, 0) with { IsValid = false })));
        Assert(missingAndInvalid.EnemyIds[0] == 0 && missingAndInvalid.EnemyIds[1] == 0 &&
               missingAndInvalid.EnemyIds[2] == 25000,
            "A valid-range enemy that is absent or invalid in the supplied snapshot must be cleared, while an out-of-range id remains untouched.");
    }

    private static void SelectsNearestTargetWithNpcPenaltyAndLastSlotTie()
    {
        var input = Input(
            Mob(x: 0, y: 0, segmentX: 0, segmentY: 0, mode: LegacyMobAiMode.Combat, enemyIds: [1002, 1]),
            Targets(Npc(1002, 2, 0), Player(1, 3, 0)));
        var result = LegacyMobAiPlanner.Plan(input);
        Assert(result.CurrentTarget == 1,
            "The NPC's +2 distance penalty must make the farther player the better target.");
        var tie = LegacyMobAiPlanner.Plan(Input(Mob(x: 0, y: 0, segmentX: 0, segmentY: 0, mode: LegacyMobAiMode.Combat, enemyIds: [1, 3]),
            Targets(Player(1, 3, 0), Player(3, 3, 0))));
        Assert(tie.CurrentTarget == 3, "Equal scores must select the last enemy-list slot.");
    }

    private static void InactiveTargetsCannotBeSelectedAndPassiveAcquisitionRequiresVisibility()
    {
        var inactive = Player(1, 2, 0) with { IsActive = false };
        var invalidated = LegacyMobAiPlanner.Plan(Input(Mob(mode: LegacyMobAiMode.Combat, enemyIds: [1]), Targets(inactive)));
        Assert(invalidated.CurrentTarget == 0 && invalidated.EnemyIds[0] == 0,
            "A player outside USER_PLAY must be removed from the selectable target list.");

        var outsideView = Player(1, 2, 0) with { IsVisible = false };
        var noAcquire = LegacyMobAiPlanner.Plan(Input(Mob(), Targets(outsideView)));
        Assert(noAcquire.CurrentTarget == 0,
            "Passive acquisition must not select a candidate outside the supplied view.");
        var acquired = LegacyMobAiPlanner.Plan(Input(Mob(), Targets(Player(1, 102, 100))));
        Assert(acquired.Kind == LegacyMobAiDecisionKind.Aggro && acquired.AggroTargetId == 1 &&
               acquired.CurrentTarget == 0 && acquired.Mode == LegacyMobAiMode.Peace,
            "Passive acquisition must return a SetBattle request without changing Mode or CurrentTarget itself.");
    }

    private static void PassiveAcquisitionUsesGridOrderHostilityAndLeaderGate()
    {
        var mob = Mob(x: 100, y: 100, segmentX: 100, segmentY: 100);
        var ordered = LegacyMobAiPlanner.Plan(Input(mob with { CurrentTarget = 77 }, Targets(
            Npc(1002, 104, 101), Player(1, 101, 102))));
        Assert(ordered.AggroTargetId == 1002 && ordered.CurrentTarget == 77 && ordered.Mode == LegacyMobAiMode.Peace,
            "Passive view acquisition must use supplied grid scan order and include hostile NPCs, not nearest-distance order.");

        var hostile = Player(1, 101, 100) with { IsHostile = false };
        var notHostile = LegacyMobAiPlanner.Plan(Input(mob, Targets(hostile)));
        Assert(notHostile.AggroTargetId == 0,
            "Passive acquisition must honor the source ClanTable hostility result.");

        var leaderOwned = LegacyMobAiPlanner.Plan(Input(mob with { LeaderId = 9 }, Targets(Player(1, 101, 100))));
        Assert(leaderOwned.AggroTargetId == 0,
            "A mob with a leader must not use common passive target acquisition.");
        var merchant = LegacyMobAiPlanner.Plan(Input(mob, Targets(Player(2, 102, 100, merchant: true))));
        Assert(merchant.AggroTargetId == 2,
            "Passive GetEnemyFromView has no Merchant filter; that filter belongs only to AddEnemyList.");
    }

    private static void SelectionUsesSourceRangesAndDoesNotFilterMerchantOrHighLevelPlayers()
    {
        var normal = LegacyMobAiPlanner.Plan(Input(Mob(x: 0, y: 0, segmentX: 0, segmentY: 0, mode: LegacyMobAiMode.Combat, enemyIds: [1]), Targets(Player(1, 6, 6))));
        Assert(normal.CurrentTarget == 1,
            "The normal acquisition window must be the inclusive 6-tile rectangle per axis.");
        var outsideNormal = LegacyMobAiPlanner.Plan(Input(Mob(x: 0, y: 0, segmentX: 0, segmentY: 0, mode: LegacyMobAiMode.Combat, enemyIds: [1]), Targets(Player(1, 7, 0))));
        Assert(outsideNormal.CurrentTarget == 0, "A target beyond the 6-tile normal window must be removed.");
        var clan = LegacyMobAiPlanner.Plan(Input(Mob(x: 0, y: 0, segmentX: 0, segmentY: 0, clan: 7, mode: LegacyMobAiMode.Combat, enemyIds: [1]), Targets(Player(1, 12, 12, merchant: true, level: 500))));
        Assert(clan.CurrentTarget == 1,
            "Clan 7 must acquire at the inclusive 12-tile boundary without selector merchant or level filtering.");
        var idBoundary = LegacyMobAiPlanner.Plan(Input(
            Mob(x: 0, y: 0, segmentX: 0, segmentY: 0, mode: LegacyMobAiMode.Combat, enemyIds: [1000]),
            Targets(Npc(1000, 2, 0) with { Hidden = true })));
        Assert(idBoundary.CurrentTarget == 1000,
            "ID MAX_USER falls through the source NPC selector path, where hidden-player filtering does not run.");
        var sector = LegacyMobAiPlanner.Plan(Input(Mob(x: 100, y: 3400, segmentX: 100, segmentY: 3400, mode: LegacyMobAiMode.Combat, enemyIds: [1]), Targets(Player(1, 108, 3408))));
        Assert(sector.CurrentTarget == 1,
            "The special sector must use its inclusive 8-tile acquisition range.");
        var clanSectorConflict = LegacyMobAiPlanner.Plan(Input(
            Mob(x: 100, y: 3400, segmentX: 100, segmentY: 3400, clan: 7,
                mode: LegacyMobAiMode.Combat, enemyIds: [1]),
            Targets(Player(1, 112, 3400))));
        Assert(clanSectorConflict.CurrentTarget == 0 && clanSectorConflict.EnemyIds[0] == 0,
            "The special 8-tile sector override must take precedence over Clan 7's 12-tile range.");
        var kefra = LegacyMobAiPlanner.Plan(Input(Mob(generateIndex: LegacyMobAiPlanner.KefraBossGenerateIndex, mode: LegacyMobAiMode.Combat, enemyIds: [1]), Targets(Player(1, 116, 116))));
        Assert(kefra.CurrentTarget == 1,
            "Kefra acquisition must use the inclusive HALFGRIDX range.");
    }

    private static void SelectionClearsHiddenDeadAndOutOfWindowButPreservesInvalidIds()
    {
        var input = Input(
            Mob(x: 0, y: 0, segmentX: 0, segmentY: 0, mode: LegacyMobAiMode.Combat, enemyIds: [0, 1, 2, 3, 25000]),
            Targets(Player(1, 2, 0, hidden: true), Player(2, 2, 0, hp: 0), Player(3, 7, 0)));
        var result = LegacyMobAiPlanner.Plan(input);
        Assert(result.CurrentTarget == 0 && result.EnemyIds.SequenceEqual([0, 0, 0, 0, 25000, 0, 0, 0, 0, 0, 0, 0, 0]),
            "The selector must clear empty, dead, hidden, and out-of-window entries but retain invalid ids.");
    }

    private static void SetBattleUsesInclusiveWindowAndInsertionFiltersAndCapacity()
    {
        var mob = Mob(x: 0, y: 0, segmentX: 0, segmentY: 0, enemyIds: [0], currentTarget: 77);
        var edge = Player(1, 23, 23);
        var accepted = LegacyMobAiPlanner.TrySetBattle(mob, edge);
        Assert(accepted.Mode == LegacyMobAiMode.Combat && accepted.EnemyIds[0] == 1 && accepted.CurrentTarget == 77,
            "SetBattle must accept the inclusive 23-by-23 window and defer CurrentTarget selection.");
        var outside = LegacyMobAiPlanner.TrySetBattle(mob, Player(2, 24, 0));
        Assert(outside.EnemyIds.All(static id => id == 0) && outside.Mode == LegacyMobAiMode.Peace,
            "SetBattle must reject a target outside the 23-tile rectangle without entering combat.");
        var hidden = LegacyMobAiPlanner.TrySetBattle(mob, Player(3, 1, 1, hidden: true));
        var merchant = LegacyMobAiPlanner.TrySetBattle(mob, Player(4, 1, 1, merchant: true));
        Assert(hidden.EnemyIds[0] == 0 && merchant.EnemyIds[0] == 0,
            "SetBattle must filter hidden and merchant player entries.");
        Assert(hidden.Mode == LegacyMobAiMode.Combat && merchant.Mode == LegacyMobAiMode.Combat,
            "SetBattle must switch to combat before the insertion filter rejects hidden/merchant players.");
        var maxUserHidden = LegacyMobAiPlanner.TrySetBattle(mob, Player(1000, 1, 1, hidden: true));
        Assert(maxUserHidden.EnemyIds[0] == 0 && maxUserHidden.Mode == LegacyMobAiMode.Combat,
            "AddEnemyList's insertion filter applies to IDs <= MAX_USER, including the boundary ID.");
        var playerCaller = LegacyMobAiPlanner.TrySetBattle(Mob(id: 1, x: 0, y: 0), Player(5, 1, 1));
        Assert(playerCaller.Mode == LegacyMobAiMode.Peace && playerCaller.EnemyIds.All(static id => id == 0),
            "SetBattle must reject player callers because only generated NPC mobs enter this source helper.");
        var emptyModeCaller = LegacyMobAiPlanner.TrySetBattle(
            Mob(id: 1000, x: 0, y: 0, mode: LegacyMobAiMode.Empty), Player(5, 1, 1));
        Assert(emptyModeCaller.Kind != LegacyMobAiDecisionKind.Aggro &&
               emptyModeCaller.Mode == LegacyMobAiMode.Empty && emptyModeCaller.EnemyIds.All(static id => id == 0),
            "SetBattle must reject a source-empty mob even if a caller marks its snapshot active.");
        var orderedMob = Mob(x: 0, y: 0, segmentX: 0, segmentY: 0, enemyIds: [0, 7]);
        var inserted = LegacyMobAiPlanner.TrySetBattle(orderedMob, Player(8, 1, 1));
        var duplicate = LegacyMobAiPlanner.TrySetBattle(orderedMob with { EnemyIds = inserted.EnemyIds }, Player(7, 1, 1));
        Assert(inserted.EnemyIds[0] == 8 && inserted.EnemyIds[1] == 7 &&
               duplicate.EnemyIds.SequenceEqual(inserted.EnemyIds),
            "SetBattle must use the first empty list slot, preserve existing order, and avoid duplicate entries.");
        var self = LegacyMobAiPlanner.TrySetBattle(mob, Player(20_000, 0, 0));
        Assert(self.Mode == LegacyMobAiMode.Peace && self.EnemyIds.All(static id => id == 0),
            "SetBattle must reject self-targeting.");
        var full = Mob(x: 0, y: 0, segmentX: 0, segmentY: 0, enemyIds: Enumerable.Range(1, 13).ToArray());
        var fullResult = LegacyMobAiPlanner.TrySetBattle(full, Player(14, 1, 1));
        Assert(fullResult.Mode == LegacyMobAiMode.Combat && fullResult.EnemyIds.SequenceEqual(Enumerable.Range(1, 13)),
            "A full 13-slot list must stay intact while the mob enters combat.");
    }

    private static void BattleProcessorDropsToPeaceAndHonorsInclusiveLeashAndChanceRolls()
    {
        var invalidCaller = LegacyMobAiPlanner.Plan(Input(
            Mob(id: 1, x: 0, y: 0, segmentX: 0, segmentY: 0),
            Targets(Player(2, 1, 0))));
        var emptyMode = LegacyMobAiPlanner.Plan(Input(
            Mob(id: 1000, x: 0, y: 0, segmentX: 0, segmentY: 0, mode: LegacyMobAiMode.Empty),
            Targets(Player(2, 1, 0))));
        var unknownMode = LegacyMobAiPlanner.Plan(Input(
            Mob(mode: (LegacyMobAiMode)99), Targets(Player(2, 101, 100))));
        Assert(invalidCaller.Kind != LegacyMobAiDecisionKind.Aggro && emptyMode.Kind == LegacyMobAiDecisionKind.Idle &&
               unknownMode.Kind == LegacyMobAiDecisionKind.Unsupported,
            "The planner must accept only generated-mob ids and known Peace/Combat states; Empty and unknown modes cannot run StandingBy.");

        var lost = LegacyMobAiPlanner.Plan(Input(Mob(mode: LegacyMobAiMode.Combat, currentTarget: 1, enemyIds: [0])));
        Assert(lost.Kind == LegacyMobAiDecisionKind.Idle && lost.Mode == LegacyMobAiMode.Peace && lost.CurrentTarget == 0,
            "BattleProcessor without a selected target must clear CurrentTarget, switch to Peace, and return this tick.");

        var atEdge = LegacyMobAiPlanner.Plan(Input(
            Mob(x: 16, y: 0, segmentX: 0, segmentY: 0, mode: LegacyMobAiMode.Combat, enemyIds: [1], attackRange: 0),
            Targets(Player(1, 21, 0)), randomIntRoll: 0, routeStep: new(15, 0)));
        Assert(atEdge.Kind == LegacyMobAiDecisionKind.Move && atEdge.CurrentTarget == 1 && atEdge.Mode == LegacyMobAiMode.Combat,
            "The inclusive 16-tile segment edge must remain in combat instead of returning.");

        var outside = LegacyMobAiPlanner.Plan(Input(
            Mob(x: 17, y: 0, segmentX: 0, segmentY: 0, mode: LegacyMobAiMode.Combat, enemyIds: [1], attackRange: 0),
            Targets(Player(1, 22, 0)), randomIntRoll: 0, routeStep: new(16, 0)));
        Assert(outside.Kind == LegacyMobAiDecisionKind.ReturnToRoute && outside.Mode == LegacyMobAiMode.Peace &&
               outside.CurrentTarget == 0 && outside.EnemyIds.All(static id => id == 0) && outside.NextX == 16,
            "Crossing the segment leash must clear combat and return along the explicit route step.");

        var chanceMiss = LegacyMobAiPlanner.Plan(Input(
            Mob(x: 0, y: 0, segmentX: 0, segmentY: 0, mode: LegacyMobAiMode.Combat,
                enemyIds: [1], baseInt: 0, baseDex: 10, attackRange: 4),
            Targets(Player(1, 2, 0)), randomIntRoll: 1, dexterityRoll: 99));
        Assert(chanceMiss.Kind == LegacyMobAiDecisionKind.Wait,
            "An explicit INT or DEX roll that fails the source probability gate must not produce an attack.");

        var routeFiveOutsideLeash = LegacyMobAiPlanner.Plan(Input(
            Mob(x: 17, y: 0, segmentX: 0, segmentY: 0, routeType: 5, summonerId: 1,
                leaderId: 1, summonerAuthorized: true, mode: LegacyMobAiMode.Combat,
                enemyIds: [1], attackRange: 0),
            Targets(Player(1, 22, 0)), summoner: Player(1, 17, 0), randomIntRoll: 0,
            routeStep: new(18, 0)));
        Assert(routeFiveOutsideLeash.Kind == LegacyMobAiDecisionKind.Move &&
               routeFiveOutsideLeash.Mode == LegacyMobAiMode.Combat && routeFiveOutsideLeash.CurrentTarget == 1,
            "BattleProcessor must not apply the common Segment leash to RouteType 5 followers.");
    }

    private static void DexterityGateRequestsRetreatForCloseRangedNpc()
    {
        var retreat = LegacyMobAiPlanner.Plan(Input(
            Mob(x: 100, y: 100, segmentX: 100, segmentY: 100, mode: LegacyMobAiMode.Combat,
                enemyIds: [1], baseInt: 100, baseDex: 10, attackRange: 4),
            Targets(Player(1, 102, 100)), randomIntRoll: 0, dexterityRoll: 11));

        Assert(retreat.Kind == LegacyMobAiDecisionKind.Retreat && retreat.Mode == LegacyMobAiMode.Combat &&
               retreat.CurrentTarget == 1 && retreat.TargetX == 102 && retreat.TargetY == 100,
            "A ranged NPC inside distance four must request GetTargetPosDistance movement when its DEX roll exceeds BaseDex.");

        var dexBoundary = LegacyMobAiPlanner.Plan(Input(
            Mob(x: 100, y: 100, segmentX: 100, segmentY: 100, mode: LegacyMobAiMode.Combat,
                enemyIds: [1], baseInt: 100, baseDex: 10, attackRange: 4),
            Targets(Player(1, 102, 100)), randomIntRoll: 0, dexterityRoll: 10));
        var rangeBoundary = LegacyMobAiPlanner.Plan(Input(
            Mob(x: 100, y: 100, segmentX: 100, segmentY: 100, mode: LegacyMobAiMode.Combat,
                enemyIds: [1], baseInt: 100, baseDex: 10, attackRange: 3),
            Targets(Player(1, 102, 100)), randomIntRoll: 0, dexterityRoll: 11));
        var distanceBoundary = LegacyMobAiPlanner.Plan(Input(
            Mob(x: 100, y: 100, segmentX: 100, segmentY: 100, mode: LegacyMobAiMode.Combat,
                enemyIds: [1], baseInt: 100, baseDex: 10, attackRange: 5),
            Targets(Player(1, 105, 100)), randomIntRoll: 0, dexterityRoll: 11));
        Assert(dexBoundary.Kind != LegacyMobAiDecisionKind.Retreat &&
               rangeBoundary.Kind != LegacyMobAiDecisionKind.Retreat &&
               distanceBoundary.Kind != LegacyMobAiDecisionKind.Retreat,
            "Retreat must require DEX roll > BaseDex, AttackRange >= 4, and target distance <= 4.");
    }

    private static void RouteTypeFiveCombatDespawnsWithoutAValidPlayerLeader()
    {
        foreach (var leaderId in new[] { -1, 0, 1000 })
        {
            var missingLeader = LegacyMobAiPlanner.Plan(Input(
                Mob(routeType: 5, leaderId: leaderId, summonerId: 1, summonerAuthorized: true,
                    mode: LegacyMobAiMode.Combat, enemyIds: [1], attackRange: 0),
                Targets(Player(1, 101, 100)), summoner: Player(1, 100, 100), randomIntRoll: 0));
            Assert(missingLeader.Kind == LegacyMobAiDecisionKind.Despawn,
                $"BattleProcessor must request DeleteMob(type=3) for a RouteType 5 combat follower with Leader={leaderId}; got {missingLeader.Kind}.");
        }

        var noTarget = LegacyMobAiPlanner.Plan(Input(
            Mob(routeType: 5, leaderId: 0, mode: LegacyMobAiMode.Combat, enemyIds: [0]),
            Targets()));
        Assert(noTarget.Kind == LegacyMobAiDecisionKind.Idle && noTarget.Mode == LegacyMobAiMode.Peace,
            "BattleProcessor selects a target before checking the RouteType 5 leader and must not despawn a follower on an empty target list.");

        var validLeader = LegacyMobAiPlanner.Plan(Input(
            Mob(routeType: 5, leaderId: 1, summonerId: 1, summonerAuthorized: true,
                mode: LegacyMobAiMode.Combat, enemyIds: [1], attackRange: 0),
            Targets(Player(1, 101, 100)), summoner: Player(1, 100, 100), randomIntRoll: 0));
        Assert(validLeader.Kind != LegacyMobAiDecisionKind.Despawn,
            "A RouteType 5 combat follower with a player leader inside the valid id range must not take the invalid-leader despawn branch.");
    }

    private static void RunequestLv6AreaDespawnPrecedesPeaceAndCombatProcessing()
    {
        foreach (var point in new[] { (X: 3423, Y: 1492), (X: 3442, Y: 1511) })
        {
            var peace = LegacyMobAiPlanner.Plan(Input(
                Mob(x: point.X, y: point.Y, generateIndex: 4284, mode: LegacyMobAiMode.Peace),
                Targets()));
            Assert(peace.Kind == LegacyMobAiDecisionKind.Despawn,
                $"The inclusive Runequest Lv6 bounds must despawn generated NPCs in Peace at {point.X}:{point.Y}; got {peace.Kind}.");

            var combatWithoutTarget = LegacyMobAiPlanner.Plan(Input(
                Mob(x: point.X, y: point.Y, generateIndex: 4290, routeType: 5,
                    mode: LegacyMobAiMode.Combat, enemyIds: [0], leaderId: 1),
                Targets()));
            Assert(combatWithoutTarget.Kind == LegacyMobAiDecisionKind.Despawn,
                $"The Runequest Lv6 bounds must run before BattleProcessor target selection at {point.X}:{point.Y}; got {combatWithoutTarget.Kind}.");
        }

        foreach (var point in new[] { (X: 3422, Y: 1492), (X: 3443, Y: 1511), (X: 3423, Y: 1491), (X: 3442, Y: 1512) })
        {
            var outside = LegacyMobAiPlanner.Plan(Input(
                Mob(x: point.X, y: point.Y, generateIndex: 4284),
                Targets()));
            Assert(outside.Kind != LegacyMobAiDecisionKind.Despawn,
                $"The Runequest Lv6 cleanup bounds must remain inclusive and must not include {point.X}:{point.Y}.");
        }

        var boss = LegacyMobAiPlanner.Plan(Input(
            Mob(x: 3423, y: 1492, generateIndex: 4283),
            Targets()));
        Assert(boss.Kind != LegacyMobAiDecisionKind.Despawn,
            "TMProject 7.69 preserves Runequest Lv6 boss generator 4283 inside the cleanup bounds.");
    }

    private static void FollowersUseTmRouteTypeFiveRulesAndExplicitSummonerPosition()
    {
        var follower = Mob(routeType: 5, x: 100, y: 100, segmentX: 100, segmentY: 100,
            summonerId: 1, leaderId: 1, face: 0, summonerAuthorized: true);
        var input = Input(follower, targets: [], summoner: Player(1, 120, 100));
        var result = LegacyMobAiPlanner.Plan(input);
        Assert(result.Kind == LegacyMobAiDecisionKind.Move && result.NextX == 120 && result.NextY == 100,
            "TM RouteType 5 followers must follow the active summoner even when Face is outside 315..345.");
        var closeFollower = Mob(routeType: 5, x: 100, y: 100, summonerId: 1, leaderId: 1,
            face: 0, summonerAuthorized: true);
        var closing = LegacyMobAiPlanner.Plan(Input(closeFollower, targets: [], summoner: Player(1, 108, 100), routeStep: new(104, 100)));
        Assert(closing.Kind == LegacyMobAiDecisionKind.Move && closing.SegmentX == 108 && closing.SegmentY == 100,
            "A follower at distance 5..12 must track the summoner's coordinates and use the supplied path step.");
    }

    private static void RouteWaitAdvancesSegmentsAndMissingActionsAreNoOp()
    {
        var route = new LegacyNpcGenerationRoute(
            new(10, 10, 1, 12), new(20, 10, 1, 6), new(30, 10, 1, 0),
            new(40, 10, 1, 0), new(50, 10, 1, 0),
            string.Empty, string.Empty, string.Empty, string.Empty);
        var mob = Mob(x: 10, y: 10, segmentX: 10, segmentY: 10, routeType: 1, route: route,
            segmentProgress: 0, waitSec: 0);
        var initializedWait = LegacyMobAiPlanner.Plan(Input(mob, routeStep: new(11, 10)));
        Assert(initializedWait.Kind == LegacyMobAiDecisionKind.Wait && initializedWait.WaitSec == 12,
            "Arrival at a segment must initialize its configured wait.");
        mob = mob with { WaitSec = 12 };
        var waited = LegacyMobAiPlanner.Plan(Input(mob, routeStep: new(11, 10)));
        Assert(waited.Kind == LegacyMobAiDecisionKind.Wait && waited.WaitSec == 6 && waited.SegmentProgress == 0,
            "Arrival wait must tick down by six seconds without advancing the route segment.");
        var advancesWait = LegacyMobAiPlanner.Plan(Input(mob with { WaitSec = 6 }, routeStep: new(11, 10)));
        Assert(advancesWait.Kind == LegacyMobAiDecisionKind.Move && advancesWait.SegmentProgress == 1 &&
               advancesWait.NextX == 11 && advancesWait.NextY == 10 && advancesWait.ActionText is null,
            "Once its wait elapses, the mob must advance to the next segment and move without absent action text.");
        var destinationWait = LegacyMobAiPlanner.Plan(Input(mob with
        {
            X = 20, SegmentX = 20, SegmentY = 10, SegmentProgress = 1, WaitSec = 0
        }, routeStep: new(21, 10)));
        Assert(destinationWait.Kind == LegacyMobAiDecisionKind.Wait && destinationWait.WaitSec == 6,
            "Arrival at a segment with configured wait must initialize that segment's wait.");

        var reverseRoute = route with { Segment2 = route.Segment2 with { X = 30 } };
        var reverse = LegacyMobAiPlanner.Plan(Input(Mob(x: 50, y: 10, segmentX: 50, segmentY: 10,
            routeType: 2, route: reverseRoute, segmentProgress: 4), routeStep: new(49, 10)));
        Assert(reverse.Kind == LegacyMobAiDecisionKind.Move && reverse.SegmentProgress == 3 &&
               reverse.SegmentDirection == 1 && reverse.SegmentX == 40,
            "RouteType 2 must reverse direction at the fifth point and select the preceding nonempty point.");

        var noStartWait = route with { Start = route.Start with { Wait = 0 } };
        var reverseAtStart = LegacyMobAiPlanner.Plan(Input(Mob(x: 10, y: 10, segmentX: 10, segmentY: 10,
            routeType: 2, route: noStartWait, segmentProgress: 0, segmentDirection: 1), routeStep: new(11, 10)));
        Assert(reverseAtStart.Kind == LegacyMobAiDecisionKind.Move && reverseAtStart.SegmentProgress == 1 &&
               reverseAtStart.SegmentDirection == 0,
            "RouteType 2 must reverse direction at the start and advance toward the destination.");

        var sparseRoute = noStartWait with { Segment1 = route.Segment1 with { X = 0 } };
        var skippedEmpty = LegacyMobAiPlanner.Plan(Input(Mob(x: 10, y: 10, segmentX: 10, segmentY: 10,
            routeType: 1, route: sparseRoute, segmentProgress: 0), routeStep: new(11, 10)));
        Assert(skippedEmpty.Kind == LegacyMobAiDecisionKind.Move && skippedEmpty.SegmentProgress == 2,
            "SetSegment must skip route points whose X coordinate is zero.");

        var allEmpty = LegacyMobAiPlanner.Plan(Input(Mob(x: 0, y: 0, segmentX: 0, segmentY: 0,
            routeType: 2, route: LegacyNpcGenerationRoute.Empty), routeStep: new(1, 0)));
        Assert(allEmpty.Kind == LegacyMobAiDecisionKind.Unsupported,
            "An all-empty cyclic route must terminate as Unsupported instead of looping or emitting a move.");

        var actionRoute = noStartWait with { StartAction = "start" };
        var authoritativeSegment = LegacyMobAiPlanner.Plan(Input(Mob(x: 12, y: 10, segmentX: 12, segmentY: 10,
            routeType: 2, route: actionRoute, segmentProgress: 0), routeStep: new(13, 10)));
        Assert(authoritativeSegment.Kind == LegacyMobAiDecisionKind.Move && authoritativeSegment.SegmentX == 20 &&
               authoritativeSegment.SegmentY == 10 && authoritativeSegment.SegmentProgress == 1 &&
               authoritativeSegment.ActionText == "start",
            "Route arrival must compare the authoritative Segment coordinates and emit the prior index action once on a transition.");
        var continuingMove = LegacyMobAiPlanner.Plan(Input(Mob(x: 13, y: 10, segmentX: 20, segmentY: 10,
            routeType: 2, route: actionRoute, segmentProgress: 1), routeStep: new(14, 10)));
        Assert(continuingMove.Kind == LegacyMobAiDecisionKind.Move && continuingMove.ActionText is null,
            "Continuing movement on a segment must not repeat its action text.");
        var adjustedSegmentRoute = actionRoute with { Segment1 = actionRoute.Segment1 with { X = 18 } };
        var blockedPath = LegacyMobAiPlanner.Plan(Input(Mob(x: 13, y: 10, segmentX: 20, segmentY: 10,
            routeType: 2, route: adjustedSegmentRoute, segmentProgress: 1), routeStep: null));
        Assert(blockedPath.Kind == LegacyMobAiDecisionKind.Idle && blockedPath.SegmentX == 20 &&
               blockedPath.SegmentY == 10 && blockedPath.TargetX == 20 && blockedPath.TargetY == 10 &&
               blockedPath.ActionText is null,
            "A missing path step must preserve the authoritative segment coordinates and route destination when progress did not change.");

        var stopped = LegacyMobAiPlanner.Plan(Input(Mob(x: 50, y: 10, segmentX: 50, segmentY: 10,
            routeType: 0, route: route, segmentProgress: 4), routeStep: new(49, 10)));
        Assert(stopped.Kind == LegacyMobAiDecisionKind.Idle && stopped.Mode == LegacyMobAiMode.Peace &&
               stopped.SegmentProgress == 4 && stopped.ActivateMerchant,
            "RouteType 0 must stop at the destination and activate Merchant rather than repeat the final segment forever.");

        var routeOneEnd = LegacyMobAiPlanner.Plan(Input(Mob(x: 50, y: 10, segmentX: 50, segmentY: 10,
            routeType: 1, route: route, segmentProgress: 4), routeStep: new(49, 10)));
        Assert(routeOneEnd.Kind == LegacyMobAiDecisionKind.Unsupported && routeOneEnd.SegmentProgress == 5 &&
               !routeOneEnd.ActivateMerchant,
            "RouteType 1's source returns code 2 at progress 5 then indexes past the five-point arrays; the planner must expose that boundary as Unsupported.");

        var routeThreeEnd = LegacyMobAiPlanner.Plan(Input(Mob(x: 50, y: 10, segmentX: 50, segmentY: 10,
            routeType: 3, route: route, segmentProgress: 4), routeStep: new(49, 10)));
        Assert(routeThreeEnd.Kind.ToString() == "Despawn" && routeThreeEnd.NextX is null,
            "RouteType 3 must expose the terminal 0x10000 branch as a removal decision without moving away from its destination.");
        var routeThreeWait = LegacyMobAiPlanner.Plan(Input(Mob(x: 50, y: 10, segmentX: 50, segmentY: 10,
            routeType: 3, route: route, segmentProgress: 4, waitSec: 12), routeStep: new(49, 10)));
        Assert(routeThreeWait.Kind == LegacyMobAiDecisionKind.Wait && routeThreeWait.WaitSec == 6 && routeThreeWait.NextX is null,
            "RouteType 3 must decrement its destination wait and return before SetSegment.");

        var routeFourNegative = LegacyMobAiPlanner.Plan(Input(Mob(x: 10, y: 10, segmentX: 10, segmentY: 10,
            routeType: 4, route: noStartWait, segmentProgress: 0, segmentDirection: 1), routeStep: new(11, 10)));
        Assert(routeFourNegative.Kind == LegacyMobAiDecisionKind.Unsupported && routeFourNegative.NextX is null,
            "RouteType 4's negative-progress source branch has no initialized iterator and must remain Unsupported.");
    }

    private static LegacyMobAiInput Input(
        LegacyMobAiMob mob,
        IReadOnlyList<LegacyMobAiTarget>? targets = null,
        LegacyMobAiTarget? summoner = null,
        int randomIntRoll = 99,
        int dexterityRoll = 99,
        LegacyMobAiPoint? routeStep = null,
        int skillBarSelectionRoll = 0,
        int skillBarMotionRoll = 0) =>
        new(mob, targets ?? [], summoner, routeStep, randomIntRoll, dexterityRoll, skillBarSelectionRoll, skillBarMotionRoll)
        {
            SkillData = SkillData
        };

    private static LegacyMobAiMob Mob(
        int x = 100,
        int y = 100,
        int segmentX = 100,
        int segmentY = 100,
        int routeType = 0,
        int clan = 2,
        int generateIndex = 0,
        int[]? enemyIds = null,
        byte[]? skillBar = null,
        int baseInt = 100,
        int baseDex = 0,
        int attackRange = 4,
        int summonerId = 0,
        int leaderId = 0,
        int face = 0,
        LegacyNpcGenerationRoute? route = null,
        int segmentProgress = 0,
        int segmentDirection = 0,
        int waitSec = 0,
        bool summonerAuthorized = false,
        LegacyMobAiMode mode = LegacyMobAiMode.Peace,
        int currentTarget = 0,
        int id = 20_000,
        int hp = 100,
        int maxHp = 10_000) =>
        new(id, x, y, segmentX, segmentY, routeType, clan, generateIndex,
            mode, currentTarget, enemyIds ?? new int[13], skillBar ?? [255, 255, 255, 255],
            baseInt, baseDex, attackRange, 100, hp, maxHp, summonerId, leaderId, face,
            route ?? LegacyNpcGenerationRoute.Empty, segmentProgress, segmentDirection, waitSec, true, summonerAuthorized);

    private static IReadOnlyList<LegacyMobAiTarget> Targets(params LegacyMobAiTarget[] targets) => targets;

    private static LegacyMobAiTarget Player(int id, int x, int y, bool hidden = false, bool merchant = false, int level = 100, int hp = 100) =>
        new(id, x, y, false, true, hp, hidden, merchant, level);

    private static LegacyMobAiTarget Npc(int id, int x, int y, int hp = 100) =>
        new(id, x, y, true, true, hp, false, false, 1);

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

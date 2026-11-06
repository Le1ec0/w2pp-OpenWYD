namespace WydCdk.World;

/// <summary>Pure, deterministic subset of TMProject 7.69's generated mob decisions.</summary>
public static class LegacyMobAiPlanner
{
    public const int EnemyCapacity = 13;
    public const int KefraBossGenerateIndex = 394;
    public const int RunequestLv2MobInitialGenerateIndex = 4306;
    public const int RunequestLv2MobEndGenerateIndex = 4364;
    public const int RunequestLv2ExitMinX = 3373;
    public const int RunequestLv2ExitMaxX = 3407;
    public const int RunequestLv2ExitMinY = 1420;
    public const int RunequestLv2ExitMaxY = 1453;
    public const int RunequestLv6BossGenerateIndex = 4283;
    public const int RunequestLv6SecondWaveInitialGenerateIndex = 4284;
    public const int RunequestLv6SecondWaveEndGenerateIndex = 4290;
    public const int RunequestLv6MinX = 3423;
    public const int RunequestLv6MaxX = 3442;
    public const int RunequestLv6MinY = 1492;
    public const int RunequestLv6MaxY = 1511;
    public const int HalfGrid = 16;
    public const int MaximumPlayerId = 1000;
    public const int MaximumMobId = 25000;

    private static readonly int[,] DistanceTable =
    {
        { 0, 1, 2, 3, 4, 5, 6 },
        { 1, 1, 2, 3, 4, 5, 6 },
        { 2, 2, 3, 4, 4, 5, 6 },
        { 3, 3, 4, 4, 5, 5, 6 },
        { 4, 4, 4, 5, 5, 5, 6 },
        { 5, 5, 5, 5, 5, 6, 6 },
        { 6, 6, 6, 6, 6, 6, 6 }
    };

    public static LegacyMobAiDecision TrySetBattle(LegacyMobAiMob mob, LegacyMobAiTarget target)
    {
        ArgumentNullException.ThrowIfNull(mob);
        ArgumentNullException.ThrowIfNull(target);
        var enemies = NormalizeEnemyIds(mob.EnemyIds);
        var mode = LegacyMobAiMode.Combat;
        if (mob.Id < MaximumPlayerId || mob.Id >= MaximumMobId || !mob.IsActive || mob.Mode == LegacyMobAiMode.Empty || mob.Hp <= 0 ||
            target.Id <= 0 || target.Id >= MaximumMobId || target.Id == mob.Id ||
            !target.IsValid || !target.IsActive || target.Hp <= 0)
            return Decision(LegacyMobAiDecisionKind.Idle, mob, enemies);
        if (mob.Mode is not (LegacyMobAiMode.Peace or LegacyMobAiMode.Combat))
            return Decision(LegacyMobAiDecisionKind.Unsupported, mob, enemies);

        if (Math.Abs(target.X - mob.X) > 23 || Math.Abs(target.Y - mob.Y) > 23)
            return Decision(LegacyMobAiDecisionKind.Idle, mob, enemies);

        if (target.Id <= MaximumPlayerId && (target.Hidden || target.Merchant))
            return Decision(LegacyMobAiDecisionKind.Idle, mob, enemies, mode: mode);

        if (!enemies.Contains(target.Id) && enemies.Contains(0))
            enemies[Array.IndexOf(enemies, 0)] = target.Id;
        return Decision(LegacyMobAiDecisionKind.Aggro, mob, enemies, mode: mode);
    }

    public static LegacyMobAiDecision Plan(LegacyMobAiInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var mob = input.Mob;
        var enemies = NormalizeEnemyIds(mob.EnemyIds);
        if (!mob.IsActive || mob.Hp <= 0 || mob.Mode == LegacyMobAiMode.Empty)
            return Decision(LegacyMobAiDecisionKind.Idle, mob, enemies);
        if (mob.Id < MaximumPlayerId || mob.Id >= MaximumMobId ||
            mob.Mode is not (LegacyMobAiMode.Peace or LegacyMobAiMode.Combat))
            return Decision(LegacyMobAiDecisionKind.Unsupported, mob, enemies);

        if (IsRunequestLv6AreaDespawnCandidate(mob.GenerateIndex, mob.X, mob.Y))
            return Decision(LegacyMobAiDecisionKind.Despawn, mob, enemies,
                despawnReason: LegacyMobAiDespawnReason.RunequestLv6Area);

        if (mob.Mode != LegacyMobAiMode.Combat)
        {
            if (mob.RouteType == 5)
                return PlanStandingByFollower(input, enemies);
            var passive = mob.LeaderId == 0 ? input.Targets.FirstOrDefault(candidate =>
                candidate.IsValid && candidate.IsActive && candidate.Hp > 0 && candidate.IsVisible && candidate.IsHostile &&
                candidate.Id > 0 && candidate.Id < MaximumMobId &&
                (candidate.Id >= MaximumPlayerId || !candidate.Hidden) &&
                IsInsidePassiveView(mob, candidate)) : null;
            if (passive is not null)
                return Decision(LegacyMobAiDecisionKind.Aggro, mob, enemies, aggroTargetId: passive.Id);
            if (mob.RouteType == 6 && mob.X == mob.SegmentX && mob.Y == mob.SegmentY)
                return Decision(LegacyMobAiDecisionKind.Idle, mob, enemies, currentTarget: 0, mode: LegacyMobAiMode.Peace);
            return PlanRoute(input, enemies);
        }

        var target = SelectTarget(mob, enemies, input.Targets, out var selectedId);
        if (selectedId == 0)
            return Decision(LegacyMobAiDecisionKind.Idle, mob, enemies,
                currentTarget: 0, mode: LegacyMobAiMode.Peace);

        if (mob.RouteType == 5)
        {
            if (mob.LeaderId <= 0 || mob.LeaderId >= MaximumPlayerId)
                return Decision(LegacyMobAiDecisionKind.Despawn, mob, enemies, currentTarget: selectedId,
                    despawnReason: LegacyMobAiDespawnReason.RouteTypeFiveInvalidLeader);

            if (!TryGetSummoner(input, out var summoner))
                return Decision(LegacyMobAiDecisionKind.Unsupported, mob, enemies, currentTarget: selectedId);
            if (GetDistance(mob.X, mob.Y, summoner.X, summoner.Y) >= 20)
                return Decision(LegacyMobAiDecisionKind.Move, mob, enemies, currentTarget: selectedId,
                    mode: LegacyMobAiMode.Combat, nextX: summoner.X, nextY: summoner.Y);
        }

        if (input.RandomIntRoll < 0 || input.RandomIntRoll >= 100 ||
            mob.BaseInt < input.RandomIntRoll)
            return Decision(LegacyMobAiDecisionKind.Wait, mob, enemies, currentTarget: selectedId, mode: LegacyMobAiMode.Combat);

        if (mob.RouteType != 5 &&
            (Math.Abs(mob.X - mob.SegmentX) > HalfGrid || Math.Abs(mob.Y - mob.SegmentY) > HalfGrid))
        {
            var cleared = new int[enemies.Length];
            return MoveToward(input, mob with { CurrentTarget = 0, Mode = LegacyMobAiMode.Peace }, cleared,
                mob.SegmentX, mob.SegmentY, LegacyMobAiDecisionKind.ReturnToRoute, mode: LegacyMobAiMode.Peace);
        }

        var distance = GetDistance(mob.X, mob.Y, target!.X, target.Y);
        if (distance <= mob.AttackRange)
        {
            if (mob.AttackRange >= 4 && distance <= 4 && input.DexterityRoll > mob.BaseDex)
                return Decision(LegacyMobAiDecisionKind.Retreat, mob, enemies,
                    currentTarget: selectedId, mode: LegacyMobAiMode.Combat,
                    nextX: input.RouteStep?.X, nextY: input.RouteStep?.Y,
                    targetX: target!.X, targetY: target.Y);

            if (!TryBuildGetAttack(input, distance, out var genericAttack, out var genericAttackTargetId))
                return Decision(LegacyMobAiDecisionKind.Unsupported, mob, enemies,
                    currentTarget: selectedId, mode: LegacyMobAiMode.Combat);
            if (target.IsNpc && genericAttackTargetId is null)
                return Decision(LegacyMobAiDecisionKind.Unsupported, mob, enemies,
                    currentTarget: selectedId, mode: LegacyMobAiMode.Combat);

            return Decision(LegacyMobAiDecisionKind.Attack, mob, enemies, currentTarget: selectedId,
                mode: LegacyMobAiMode.Combat, attack: genericAttack, attackTargetId: genericAttackTargetId);
        }

        return MoveToward(input, mob with { CurrentTarget = selectedId }, enemies, target.X, target.Y,
            LegacyMobAiDecisionKind.Move, currentTarget: selectedId, mode: LegacyMobAiMode.Combat);
    }

    public static bool IsRunequestLv6AreaDespawnCandidate(int generateIndex, int x, int y)
        => generateIndex != RunequestLv6BossGenerateIndex &&
           x >= RunequestLv6MinX && x <= RunequestLv6MaxX &&
           y >= RunequestLv6MinY && y <= RunequestLv6MaxY;

    public static bool IsRunequestLv2ExitDespawnCandidate(int generateIndex, int x, int y)
        => generateIndex is >= RunequestLv2MobInitialGenerateIndex and <= RunequestLv2MobEndGenerateIndex &&
           x >= RunequestLv2ExitMinX && x <= RunequestLv2ExitMaxX &&
           y >= RunequestLv2ExitMinY && y <= RunequestLv2ExitMaxY;

    private static LegacyMobAiDecision PlanRoute(LegacyMobAiInput input, int[] enemies)
    {
        var mob = input.Mob;
        var points = new[] { mob.Route.Start, mob.Route.Segment1, mob.Route.Segment2, mob.Route.Segment3, mob.Route.Dest };
        if (mob.SegmentProgress is < 0 or >= 5)
            return Decision(LegacyMobAiDecisionKind.Unsupported, mob, enemies,
                segmentProgress: mob.SegmentProgress, segmentDirection: mob.SegmentDirection);
        var originalProgress = mob.SegmentProgress;
        var progress = originalProgress;
        var current = points[progress];
        if (mob.X == mob.SegmentX && mob.Y == mob.SegmentY)
        {
            if (mob.RouteType == 3 && progress == 4)
            {
                if (mob.WaitSec <= 0)
                    return Decision(LegacyMobAiDecisionKind.Despawn, mob, enemies,
                        despawnReason: LegacyMobAiDespawnReason.RouteTypeThreeTerminal);
                return Decision(LegacyMobAiDecisionKind.Wait, mob, enemies,
                    waitSec: Math.Max(0, mob.WaitSec - 6));
            }
            if (mob.WaitSec <= 0 || mob.RouteType == 6)
            {
                if (current.Wait > 0)
                    return Decision(LegacyMobAiDecisionKind.Wait, mob, enemies, waitSec: current.Wait);
            }
            else
            {
                var remainingWait = Math.Max(0, mob.WaitSec - 6);
                if (remainingWait > 0)
                    return Decision(LegacyMobAiDecisionKind.Wait, mob, enemies, waitSec: remainingWait);
            }

            var next = AdvanceSegment(mob.RouteType, progress, mob.SegmentDirection, points);
            if (next.Kind == LegacyMobAiRouteAdvanceKind.Stop)
                return Decision(LegacyMobAiDecisionKind.Idle, mob, enemies, currentTarget: 0,
                    mode: LegacyMobAiMode.Peace, segmentX: points[next.Progress].X,
                    segmentY: points[next.Progress].Y, segmentProgress: next.Progress,
                    segmentDirection: next.Direction, waitSec: 0,
                    activateMerchant: mob.RouteType == 0);
            if (next.Kind == LegacyMobAiRouteAdvanceKind.Unsupported)
                return Decision(LegacyMobAiDecisionKind.Unsupported, mob, enemies,
                    segmentProgress: next.Progress, segmentDirection: next.Direction, waitSec: 0);
            if (next.Kind == LegacyMobAiRouteAdvanceKind.None)
                return Decision(LegacyMobAiDecisionKind.Idle, mob, enemies, waitSec: 0);
            progress = next.Progress;
            current = points[progress];
            mob = mob with { SegmentProgress = progress, SegmentDirection = next.Direction };
        }

        if (input.RouteStep is not { } step)
            return Decision(LegacyMobAiDecisionKind.Idle, mob, enemies,
                segmentX: mob.SegmentProgress == originalProgress ? mob.SegmentX : current.X,
                segmentY: mob.SegmentProgress == originalProgress ? mob.SegmentY : current.Y,
                segmentProgress: progress, segmentDirection: mob.SegmentDirection, waitSec: 0,
                targetX: mob.SegmentProgress == originalProgress ? mob.SegmentX : current.X,
                targetY: mob.SegmentProgress == originalProgress ? mob.SegmentY : current.Y,
                actionText: mob.SegmentProgress == originalProgress ? null : GetSegmentAction(mob.Route, originalProgress));

        return Decision(LegacyMobAiDecisionKind.Move, mob, enemies, nextX: step.X, nextY: step.Y,
            segmentX: mob.SegmentProgress == originalProgress ? mob.SegmentX : current.X,
            segmentY: mob.SegmentProgress == originalProgress ? mob.SegmentY : current.Y,
            segmentProgress: progress,
            segmentDirection: mob.SegmentDirection, waitSec: 0,
            actionText: mob.SegmentProgress == originalProgress ? null : GetSegmentAction(mob.Route, originalProgress));
    }

    private static bool TryBuildGetAttack(
        LegacyMobAiInput input,
        int distance,
        out LegacyMobAiAttack attack,
        out int? attackTargetId)
    {
        attack = default!;
        attackTargetId = null;
        var mob = input.Mob;
        var clanFour = mob.Clan == 4;
        if (mob.SkillBar.Count != 4 || (clanFour && mob.BaseSpecial.Count != 4) ||
            input.SkillBarSelectionRoll is < 0 or >= 100)
            return false;

        // TMProject 7.69 selects the close SkillBar pair below distance 2 and the
        // ranged pair from distance 2. W2PP changes this boundary to distance 3.
        var primarySlot = distance >= 2 ? 2 : 0;
        var special = clanFour ? mob.BaseSpecial[primarySlot] : mob.SkillBar[primarySlot];
        var special2 = clanFour ? mob.BaseSpecial[primarySlot + 1] : mob.SkillBar[primarySlot + 1];
        if (special is < 0 or > byte.MaxValue || special2 < 0)
            return false;

        // The source switch translates these MOB special codes before the common
        // SkillBar selector may replace the skill index and elemental resistance.
        var skillIndex = special switch
        {
            byte.MaxValue => -1,
            121 => 103,
            122 => 105,
            123 or 124 => 101,
            125 or 126 or 127 => 40,
            128 => 33,
            _ => special
        };
        var skillParm = special switch
        {
            121 => 5,
            124 => 2,
            126 => 2,
            127 => 3,
            128 => -4,
            _ => 1
        };
        var motion = special == 128 ? -4 : 0;
        var resist = -1;
        if (special2 != 0)
        {
            motion = 4;
            var motionRoll = input.SkillBarMotionRoll;
            switch (special2)
            {
                case 3:
                    if (motionRoll is < 0 or > 3) return false;
                    if (motionRoll <= 1) motion++;
                    break;
                case 6:
                    if (motionRoll is < 0 or > 3) return false;
                    motion += motionRoll > 1 ? 2 : 1;
                    break;
                case 7:
                    if (motionRoll is < 0 or > 3) return false;
                    motion += motionRoll == 1 ? 1 : 2;
                    break;
                case 15:
                    if (motionRoll is < 0 or > 3) return false;
                    motion += motionRoll;
                    break;
                case 24:
                    if (motionRoll is < 0 or > 3) return false;
                    motion += motionRoll > 1 ? 4 : 3;
                    break;
                case 27:
                    if (motionRoll is < 0 or > 3) return false;
                    motion += motionRoll switch { 0 => 0, 1 => 1, 2 => 3, _ => 4 };
                    break;
                case 23:
                    if (motionRoll is < 0 or > 3) return false;
                    motion += motionRoll switch { 0 => 0, 1 => 1, 2 => 2, _ => 4 };
                    break;
                default:
                    skillParm = 1;
                    break;
            }
        }

        var slotThree = mob.SkillBar[3];
        if (slotThree != byte.MaxValue && input.SkillBarSelectionRoll is >= 25 and <= 64)
        {
            var slotThreeDefinition = input.SkillData?[slotThree];
            if (slotThreeDefinition is null)
                return false;

            if (slotThreeDefinition.InstanceType == 6)
            {
                LegacyMobAiTarget? leader = null;
                if (mob.LeaderId > 0 && mob.LeaderId != mob.Id)
                {
                    leader = input.Targets.FirstOrDefault(candidate => candidate.Id == mob.LeaderId &&
                        candidate.IsNpc && candidate.IsGeneratedNpc && candidate.IsValid && candidate.IsActive &&
                        candidate.Hp > 0 && candidate.MaxHp >= 0);
                    if (leader is null)
                        return false;
                }

                var mobHpFraction = GetHpFraction(mob.Hp, mob.MaxHp);
                var leaderHpFraction = leader is null
                    ? mobHpFraction
                    : GetHpFraction(leader.Hp, leader.MaxHp);
                if (mobHpFraction <= 8 || leaderHpFraction <= 8)
                {
                    attackTargetId = mobHpFraction > leaderHpFraction ? leader!.Id : mob.Id;
                    attack = new(6, skillParm, motion, -1);
                    return true;
                }
            }
        }

        var selectedSlot = input.SkillBarSelectionRoll < 50 ? 0 :
            input.SkillBarSelectionRoll < 85 ? 1 : 2;
        var selectedSkill = mob.SkillBar[selectedSlot];
        if (selectedSkill != byte.MaxValue)
        {
            var selectedDefinition = input.SkillData?[selectedSkill];
            if (selectedDefinition is null)
                return false;

            skillIndex = selectedSkill;
            resist = selectedDefinition.InstanceType - 2;
        }

        attack = new(skillIndex, skillParm, motion, resist);
        return true;
    }

    private static LegacyMobAiDecision PlanStandingByFollower(LegacyMobAiInput input, int[] enemies)
    {
        var mob = input.Mob;
        if (!TryGetSummoner(input, out var summoner))
            return Decision(LegacyMobAiDecisionKind.Unsupported, mob, enemies);
        var distance = GetDistance(mob.X, mob.Y, summoner.X, summoner.Y);
        if (distance >= 13)
            return Decision(LegacyMobAiDecisionKind.Move, mob, enemies, nextX: summoner.X, nextY: summoner.Y);
        if (distance > 4)
            return MoveToward(input, mob with { SegmentX = summoner.X, SegmentY = summoner.Y }, enemies,
                summoner.X, summoner.Y, LegacyMobAiDecisionKind.Move);
        return Decision(LegacyMobAiDecisionKind.Idle, mob, enemies);
    }

    private static LegacyMobAiRouteAdvance AdvanceSegment(
        int routeType,
        int progress,
        int direction,
        IReadOnlyList<LegacyNpcGenerationRoutePoint> points)
    {
        if (routeType == 6)
            return new(LegacyMobAiRouteAdvanceKind.Advance, 0, 0);
        if (routeType is < 0 or > 4)
            return new(LegacyMobAiRouteAdvanceKind.Unsupported, progress, direction);

        for (var attempt = 0; attempt < 32; attempt++)
        {
            progress += direction == 0 ? 1 : -1;
            if (progress == -1)
            {
                if (routeType == 0)
                    return new(LegacyMobAiRouteAdvanceKind.Advance, 0, 0, ReturnCode: 2);
                if (routeType is 1 or 2)
                {
                    progress = 0;
                    direction = 0;
                    continue;
                }
                return new(LegacyMobAiRouteAdvanceKind.Unsupported, progress, direction, ReturnCode: 2);
            }
            if (progress == 5)
            {
                if (routeType == 0)
                    return new(LegacyMobAiRouteAdvanceKind.Stop, 4, direction, ReturnCode: 1);
                if (routeType == 1)
                    return new(LegacyMobAiRouteAdvanceKind.Unsupported, progress, direction, ReturnCode: 2);
                if (routeType is 2 or 3)
                {
                    progress = 4;
                    direction = 1;
                    continue;
                }
                if (routeType == 4)
                {
                    progress = -1;
                    continue;
                }
            }
            if (progress is < 0 or >= 5)
                return new(LegacyMobAiRouteAdvanceKind.Unsupported, progress, direction);
            if (points[progress].X != 0)
                return new(LegacyMobAiRouteAdvanceKind.Advance, progress, direction);
        }
        return new(LegacyMobAiRouteAdvanceKind.Unsupported, progress, direction);
    }

    private static string? GetSegmentAction(LegacyNpcGenerationRoute route, int progress) => progress switch
    {
        0 => EmptyToNull(route.StartAction),
        1 => EmptyToNull(route.Segment1Action),
        2 => EmptyToNull(route.Segment2Action),
        3 => EmptyToNull(route.Segment3Action),
        _ => null
    };

    private static string? EmptyToNull(string value) => string.IsNullOrEmpty(value) ? null : value;

    private static LegacyMobAiDecision MoveToward(
        LegacyMobAiInput input,
        LegacyMobAiMob mob,
        int[] enemies,
        int targetX,
        int targetY,
        LegacyMobAiDecisionKind kind,
        int? currentTarget = null,
        LegacyMobAiMode? mode = null)
    {
        if (input.RouteStep is not { } step)
            return Decision(LegacyMobAiDecisionKind.Idle, mob, enemies, currentTarget: currentTarget, mode: mode,
                targetX: targetX, targetY: targetY);
        return Decision(kind, mob, enemies, currentTarget: currentTarget, mode: mode,
            nextX: step.X, nextY: step.Y, segmentX: mob.SegmentX, segmentY: mob.SegmentY,
            targetX: targetX, targetY: targetY);
    }

    private static LegacyMobAiTarget? SelectTarget(
        LegacyMobAiMob mob,
        int[] enemies,
        IReadOnlyList<LegacyMobAiTarget> targets,
        out int selectedId)
    {
        selectedId = 0;
        var range = mob.GenerateIndex == KefraBossGenerateIndex ? HalfGrid :
            mob.X / 128 < 12 && mob.Y / 128 > 25 ? 8 :
            mob.Clan is 4 or 7 or 8 ? 12 : 6;
        var bestScore = MaximumPlayerId;
        LegacyMobAiTarget? selected = null;
        for (var index = 0; index < enemies.Length; index++)
        {
            var id = enemies[index];
            if (id <= 0 || id >= MaximumMobId)
                continue;
            var candidate = targets.FirstOrDefault(target => target.Id == id);
            if (candidate is null || !candidate.IsValid)
            {
                enemies[index] = 0;
                continue;
            }
            if (!candidate.IsActive || candidate.Hp <= 0 ||
                candidate.Id < MaximumPlayerId && candidate.Hidden)
            {
                enemies[index] = 0;
                continue;
            }
            if (candidate.X < mob.X - range || candidate.X > mob.X + range ||
                candidate.Y < mob.Y - range || candidate.Y > mob.Y + range)
            {
                enemies[index] = 0;
                continue;
            }
            var score = GetDistance(mob.X, mob.Y, candidate.X, candidate.Y);
            if (candidate.Id > MaximumPlayerId)
                score += 2;
            if (bestScore >= score)
            {
                bestScore = score;
                selected = candidate;
                selectedId = id;
            }
        }
        return selected;
    }

    private static bool TryGetSummoner(LegacyMobAiInput input, out LegacyMobAiTarget summoner)
    {
        var mob = input.Mob;
        summoner = input.Summoner!;
        return mob.SummonerId > 0 && mob.SummonerId < MaximumPlayerId &&
               mob.LeaderId > 0 && mob.LeaderId < MaximumPlayerId && mob.SummonerAuthorized &&
               summoner is { IsValid: true, IsActive: true, Hp: > 0 } && summoner.Id == mob.SummonerId;
    }

    private static bool IsInsidePassiveView(LegacyMobAiMob mob, LegacyMobAiTarget target)
    {
        var startX = mob.X - (mob.Clan is 7 or 8 ? 6 : 4);
        var startY = mob.Y - (mob.Clan is 7 or 8 ? 6 : 4);
        var size = mob.Clan is 7 or 8 ? HalfGrid : 9;
        return target.X >= startX && target.X < startX + size &&
               target.Y >= startY && target.Y < startY + size &&
               (target.X != mob.X || target.Y != mob.Y) &&
               mob.X >= mob.SegmentX - HalfGrid && mob.X <= mob.SegmentX + HalfGrid &&
               mob.Y >= mob.SegmentY - HalfGrid && mob.Y <= mob.SegmentY + HalfGrid;
    }

    private static int GetDistance(int x1, int y1, int x2, int y2)
    {
        var dx = Math.Abs(x1 - x2);
        var dy = Math.Abs(y1 - y2);
        return dx <= 6 && dy <= 6 ? DistanceTable[dy, dx] : Math.Max(dx, dy) + 1;
    }

    private static int[] NormalizeEnemyIds(IReadOnlyList<int> ids)
    {
        var result = new int[EnemyCapacity];
        for (var i = 0; i < Math.Min(ids.Count, result.Length); i++)
            result[i] = ids[i];
        return result;
    }

    private static LegacyMobAiDecision Decision(
        LegacyMobAiDecisionKind kind,
        LegacyMobAiMob mob,
        int[] enemies,
        int? currentTarget = null,
        int? aggroTargetId = null,
        LegacyMobAiMode? mode = null,
        int? nextX = null,
        int? nextY = null,
        int? segmentX = null,
        int? segmentY = null,
        int? segmentProgress = null,
        int? segmentDirection = null,
        int? waitSec = null,
        int? targetX = null,
        int? targetY = null,
        LegacyMobAiAttack? attack = null,
        string? actionText = null,
        int? attackTargetId = null,
        LegacyMobAiDespawnReason despawnReason = LegacyMobAiDespawnReason.None,
        bool activateMerchant = false) =>
        new(kind, mode ?? mob.Mode, currentTarget ?? mob.CurrentTarget, aggroTargetId ?? 0, enemies.ToArray(),
            nextX, nextY, segmentX ?? mob.SegmentX, segmentY ?? mob.SegmentY,
            segmentProgress ?? mob.SegmentProgress, segmentDirection ?? mob.SegmentDirection,
            waitSec ?? mob.WaitSec, targetX, targetY, attack, actionText, attackTargetId, despawnReason, activateMerchant);

    private static long GetHpFraction(int hp, int maxHp)
        => (long)hp * 10 / ((long)maxHp + 1);
}

public enum LegacyMobAiMode { Empty = 0, Combat = 5, Peace = 4 }

public enum LegacyMobAiDecisionKind { Idle, Aggro, Move, ReturnToRoute, Wait, Attack, Unsupported, Despawn, Retreat }

public enum LegacyMobAiDespawnReason { None, RouteTypeThreeTerminal, RouteTypeFiveInvalidLeader, RunequestLv6Area }

internal enum LegacyMobAiRouteAdvanceKind { Advance, Stop, Unsupported, None }

internal sealed record LegacyMobAiRouteAdvance(
    LegacyMobAiRouteAdvanceKind Kind, int Progress, int Direction, int ReturnCode = 0);

public sealed record LegacyMobAiPoint(int X, int Y);

public sealed record LegacyMobAiTarget(
    int Id, int X, int Y, bool IsNpc, bool IsActive, int Hp,
    bool Hidden, bool Merchant, int Level, bool IsValid = true, bool IsVisible = true, bool IsHostile = true,
    int MaxHp = 0, bool IsGeneratedNpc = false);

public sealed record LegacyMobAiMob(
    int Id, int X, int Y, int SegmentX, int SegmentY, int RouteType, int Clan,
    int GenerateIndex, LegacyMobAiMode Mode, int CurrentTarget, IReadOnlyList<int> EnemyIds,
    IReadOnlyList<byte> SkillBar, int BaseInt, int BaseDex, int AttackRange,
    int Level, int Hp, int MaxHp, int SummonerId, int LeaderId, int Face,
    LegacyNpcGenerationRoute Route, int SegmentProgress, int SegmentDirection,
    int WaitSec, bool IsActive, bool SummonerAuthorized = false)
{
    public IReadOnlyList<short> BaseSpecial { get; init; } = [];
}

/// <summary>
/// Candidate order is the caller's source grid scan order (Y then X); IsHostile is the
/// precomputed 7.69 ClanTable[mobClan][targetClan] == 0 result, and RouteStep is the
/// explicit terrain/path result selected for the next movement tile.
/// </summary>
public sealed record LegacyMobAiInput(
    LegacyMobAiMob Mob,
    IReadOnlyList<LegacyMobAiTarget> Targets,
    LegacyMobAiTarget? Summoner,
    LegacyMobAiPoint? RouteStep,
    int RandomIntRoll,
    int DexterityRoll,
    int SkillBarSelectionRoll = 0,
    int SkillBarMotionRoll = 0)
{
    public LegacySkillDataTable? SkillData { get; init; }
}

public sealed record LegacyMobAiAttack(int SkillIndex, int SkillParm, int Motion, int Resist);

public sealed record LegacyMobAiDecision(
    LegacyMobAiDecisionKind Kind,
    LegacyMobAiMode Mode,
    int CurrentTarget,
    int AggroTargetId,
    IReadOnlyList<int> EnemyIds,
    int? NextX,
    int? NextY,
    int SegmentX,
    int SegmentY,
    int SegmentProgress,
    int SegmentDirection,
    int WaitSec,
    int? TargetX,
    int? TargetY,
    LegacyMobAiAttack? Attack,
    string? ActionText,
    int? AttackTargetId = null,
    LegacyMobAiDespawnReason DespawnReason = LegacyMobAiDespawnReason.None,
    bool ActivateMerchant = false);

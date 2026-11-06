using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>Resolves the common generated-NPC chase target used by TMProject's GetTargetPos/timer path.</summary>
internal static class LegacyMobAiMovementPlanner
{
    private const int SecondsPerBattle = 8;
    private const int MaximumRouteLength = 24;
    private const int MaximumServerCount = 10;
    private const int MaximumGridCoordinate = 4096;
    private const int MountHeadIndex = 219;
    private const int SpecialHeadIndex = 220;

    public static bool TryBuildChaseStep(
        LegacyWorldNpc npc,
        int targetX,
        int targetY,
        LegacyMapGrid? mapGrid,
        Func<int, int, int> occupantAt,
        Func<int> randomFallbackOffset,
        out LegacyNpcAiMovementPlan? movement)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(occupantAt);
        ArgumentNullException.ThrowIfNull(randomFallbackOffset);
        movement = null;
        if (npc.MobSnapshot.Length < LegacyAccountSnapshot.CharacterStride)
            return false;

        var head = LegacyItem.Read(npc.MobSnapshot.AsSpan(
            LegacyAccountSnapshot.MobEquipmentOffset,
            LegacyItem.SizeInBytes));
        // TMProject sends these two special heads to the mob's route TargetX/Y. That route target is
        // not represented by this combat-only snapshot, so keep this non-generic branch unsupported.
        if (head.Index is MountHeadIndex or SpecialHeadIndex)
            return false;

        var baseScore = LegacyScore.Read(npc.MobSnapshot.AsSpan(
            LegacyAccountSnapshot.MobBaseScoreOffset,
            LegacyScore.SizeInBytes));
        var currentScore = LegacyScore.Read(npc.MobSnapshot.AsSpan(
            LegacyAccountSnapshot.MobCurrentScoreOffset,
            LegacyScore.SizeInBytes));
        var destinationX = targetX - 1;
        var destinationY = targetY - 1;
        byte[] route = new byte[MaximumRouteLength];
        var baseRun = baseScore.AttackRun & 15;

        if (baseRun != 0)
        {
            if (!TryFindEmpty(0, ref destinationX, ref destinationY, mapGrid, occupantAt))
                return false;

            var speed = GetSpeed(currentScore.AttackRun) + 2;
            var distance = Math.Min((speed * SecondsPerBattle) / 4, MaximumRouteLength - 1);
            var foundOpenStep = false;
            var routeDistance = distance;
            for (; routeDistance >= 0; routeDistance--)
            {
                var candidateX = destinationX;
                var candidateY = destinationY;
                route = new byte[MaximumRouteLength];
                LegacyActionRouteBuilder.Rebuild(
                    npc.PositionX,
                    npc.PositionY,
                    ref candidateX,
                    ref candidateY,
                    route,
                    routeDistance,
                    GetTerrainHeight);
                destinationX = candidateX;
                destinationY = candidateY;

                if (occupantAt(destinationX, destinationY) == 0)
                {
                    foundOpenStep = true;
                    break;
                }

                if (routeDistance == distance)
                    continue;

                TryFindEmpty(0, ref destinationX, ref destinationY, mapGrid, occupantAt);
                candidateX = destinationX;
                candidateY = destinationY;
                route = new byte[MaximumRouteLength];
                LegacyActionRouteBuilder.Rebuild(
                    npc.PositionX,
                    npc.PositionY,
                    ref candidateX,
                    ref candidateY,
                    route,
                    routeDistance,
                    GetTerrainHeight);
                destinationX = candidateX;
                destinationY = candidateY;
                if (occupantAt(destinationX, destinationY) == 0)
                {
                    foundOpenStep = true;
                    break;
                }
            }

            if (!foundOpenStep || route[0] == 0)
            {
                // GetTargetPos falls back to a small random offset around its target if BASE_GetRoute
                // cannot produce a first route byte. The caller injects the two legacy rand()%5 values.
                destinationX = targetX + randomFallbackOffset();
                destinationY = targetY + randomFallbackOffset();
                route[0] = 0;
            }
        }

        // ProcessSecMinTimer calls GetEmptyMobGrid(index, NextX, NextY) after GetTargetPos.
        if (destinationX == npc.PositionX && destinationY == npc.PositionY)
            return false;
        if (!TryFindEmpty(npc.ConnectionId, ref destinationX, ref destinationY, mapGrid, occupantAt))
            return false;
        if (destinationX == npc.PositionX && destinationY == npc.PositionY)
            return false;

        var actionSpeed = GetSpeed(currentScore.AttackRun);
        if (!IsInsideGrid(destinationX, destinationY) || actionSpeed is < 1 or > 6)
            return false;

        movement = new LegacyNpcAiMovementPlan(
            npc.PositionX,
            npc.PositionY,
            checked((short)destinationX),
            checked((short)destinationY),
            actionSpeed);
        return true;

        int GetTerrainHeight(int x, int y) => mapGrid?.GetTerrainHeight(x, y) ?? 0;
    }

    public static bool TryBuildRetreatStep(
        LegacyWorldNpc npc,
        int targetX,
        int targetY,
        LegacyMapGrid? mapGrid,
        Func<int, int, int> occupantAt,
        Func<int> randomRetreatStepRoll,
        out LegacyNpcAiMovementPlan? movement)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(occupantAt);
        ArgumentNullException.ThrowIfNull(randomRetreatStepRoll);
        movement = null;
        if (npc.MobSnapshot.Length < LegacyAccountSnapshot.CharacterStride)
            return false;

        var baseScore = LegacyScore.Read(npc.MobSnapshot.AsSpan(
            LegacyAccountSnapshot.MobBaseScoreOffset,
            LegacyScore.SizeInBytes));
        var currentScore = LegacyScore.Read(npc.MobSnapshot.AsSpan(
            LegacyAccountSnapshot.MobCurrentScoreOffset,
            LegacyScore.SizeInBytes));
        var lastX = (int)npc.PositionX;
        var lastY = (int)npc.PositionY;
        var destinationX = lastX;
        var destinationY = lastY;
        if ((baseScore.AttackRun & 15) != 0)
        {
            if (targetX > lastX)
            {
                var roll = randomRetreatStepRoll();
                if (roll is < 0 or > 1) return false;
                destinationX -= 1 + roll;
            }
            else if (targetX < lastX)
            {
                var roll = randomRetreatStepRoll();
                if (roll is < 0 or > 1) return false;
                destinationX += 1 + roll;
            }

            if (targetY > lastY)
            {
                var roll = randomRetreatStepRoll();
                if (roll is < 0 or > 1) return false;
                destinationY -= 1 + roll;
            }
            else if (targetY < lastY)
            {
                var roll = randomRetreatStepRoll();
                if (roll is < 0 or > 1) return false;
                destinationY += 1 + roll;
            }

            if (!TryFindEmpty(0, ref destinationX, ref destinationY, mapGrid, occupantAt))
                return false;

            var speed = GetSpeed(currentScore.AttackRun);
            var distance = Math.Min((speed * SecondsPerBattle) / 4, MaximumRouteLength - 1);
            var foundOpenStep = false;
            var routeDistance = distance;
            var route = new byte[MaximumRouteLength];
            for (; routeDistance >= 0; routeDistance--)
            {
                var candidateX = destinationX;
                var candidateY = destinationY;
                route = new byte[MaximumRouteLength];
                LegacyActionRouteBuilder.Rebuild(
                    lastX,
                    lastY,
                    ref candidateX,
                    ref candidateY,
                    route,
                    routeDistance,
                    GetTerrainHeight);
                destinationX = candidateX;
                destinationY = candidateY;

                if (occupantAt(destinationX, destinationY) == 0)
                {
                    foundOpenStep = true;
                    break;
                }

                if (routeDistance == distance)
                    continue;

                TryFindEmpty(0, ref destinationX, ref destinationY, mapGrid, occupantAt);
                candidateX = destinationX;
                candidateY = destinationY;
                route = new byte[MaximumRouteLength];
                LegacyActionRouteBuilder.Rebuild(
                    lastX,
                    lastY,
                    ref candidateX,
                    ref candidateY,
                    route,
                    routeDistance,
                    GetTerrainHeight);
                destinationX = candidateX;
                destinationY = candidateY;
                if (occupantAt(destinationX, destinationY) == 0)
                {
                    foundOpenStep = true;
                    break;
                }
            }

            if (!foundOpenStep || route[0] == 0)
            {
                destinationX = lastX;
                destinationY = lastY;
                route[0] = 0;
            }
        }

        if ((destinationX == npc.PositionX && destinationY == npc.PositionY) ||
            !TryFindEmpty(npc.ConnectionId, ref destinationX, ref destinationY, mapGrid, occupantAt) ||
            (destinationX == npc.PositionX && destinationY == npc.PositionY))
            return false;

        var actionSpeed = GetSpeed(currentScore.AttackRun);
        if (!IsInsideGrid(destinationX, destinationY) || actionSpeed is < 1 or > 6)
            return false;

        movement = new LegacyNpcAiMovementPlan(
            npc.PositionX,
            npc.PositionY,
            checked((short)destinationX),
            checked((short)destinationY),
            actionSpeed);
        return true;

        int GetTerrainHeight(int x, int y) => mapGrid?.GetTerrainHeight(x, y) ?? 0;
    }

    public static bool TryBuildReturnToRouteStep(
        LegacyWorldNpc npc,
        LegacyMapGrid? mapGrid,
        Func<int, int, int> occupantAt,
        Func<int> randomSeedRoll,
        out LegacyNpcAiMovementPlan? movement)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(randomSeedRoll);
        return TryBuildSegmentStep(npc, npc.SegmentX, npc.SegmentY, battle: true,
            mapGrid, occupantAt, randomSeedRoll, out movement);
    }

    public static bool TryBuildPeaceRouteStep(
        LegacyWorldNpc npc,
        int targetX,
        int targetY,
        LegacyMapGrid? mapGrid,
        Func<int, int, int> occupantAt,
        Func<int> randomSeedRoll,
        out LegacyNpcAiMovementPlan? movement)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(randomSeedRoll);
        return TryBuildSegmentStep(npc, targetX, targetY, battle: false,
            mapGrid, occupantAt, randomSeedRoll, out movement);
    }

    private static bool TryBuildSegmentStep(
        LegacyWorldNpc npc,
        int targetX,
        int targetY,
        bool battle,
        LegacyMapGrid? mapGrid,
        Func<int, int, int> occupantAt,
        Func<int> randomSeedRoll,
        out LegacyNpcAiMovementPlan? movement)
    {
        ArgumentNullException.ThrowIfNull(occupantAt);
        movement = null;
        if (npc.MobSnapshot.Length < LegacyAccountSnapshot.CharacterStride)
            return false;

        var head = LegacyItem.Read(npc.MobSnapshot.AsSpan(
            LegacyAccountSnapshot.MobEquipmentOffset,
            LegacyItem.SizeInBytes));
        if (head.Index is MountHeadIndex or SpecialHeadIndex ||
            npc.GenerateIndex == LegacyMobAiPlanner.KefraBossGenerateIndex)
            return false;

        var baseScore = LegacyScore.Read(npc.MobSnapshot.AsSpan(
            LegacyAccountSnapshot.MobBaseScoreOffset,
            LegacyScore.SizeInBytes));
        var currentScore = LegacyScore.Read(npc.MobSnapshot.AsSpan(
            LegacyAccountSnapshot.MobCurrentScoreOffset,
            LegacyScore.SizeInBytes));
        if ((baseScore.AttackRun & 15) == 0)
            return false;

        var seedRollX = randomSeedRoll();
        var seedRollY = randomSeedRoll();
        if (seedRollX is < 0 or >= 5 || seedRollY is < 0 or >= 5)
            return false;
        var destinationX = targetX + seedRollX - 2;
        var destinationY = targetY + seedRollY - 2;
        if (!TryFindEmpty(0, ref destinationX, ref destinationY, mapGrid, occupantAt))
            return false;

        var speed = GetSpeed(currentScore.AttackRun);
        var distance = Math.Min((speed * SecondsPerBattle) / 4, MaximumRouteLength - 1);
        if (!battle)
            distance = Math.Min(distance, baseScore.Strength);
        if (distance < 0)
            return false;

        var foundOpenStep = false;
        byte[] route = new byte[MaximumRouteLength];
        var routeDistance = distance;
        for (; routeDistance >= 0; routeDistance--)
        {
            var candidateX = destinationX;
            var candidateY = destinationY;
            route = new byte[MaximumRouteLength];
            LegacyActionRouteBuilder.Rebuild(
                npc.PositionX,
                npc.PositionY,
                ref candidateX,
                ref candidateY,
                route,
                routeDistance,
                GetTerrainHeight);
            destinationX = candidateX;
            destinationY = candidateY;

            if (occupantAt(destinationX, destinationY) == 0)
            {
                foundOpenStep = true;
                break;
            }

            if (routeDistance == distance)
                continue;

            TryFindEmpty(0, ref destinationX, ref destinationY, mapGrid, occupantAt);
            candidateX = destinationX;
            candidateY = destinationY;
            route = new byte[MaximumRouteLength];
            LegacyActionRouteBuilder.Rebuild(
                npc.PositionX,
                npc.PositionY,
                ref candidateX,
                ref candidateY,
                route,
                routeDistance,
                GetTerrainHeight);
            destinationX = candidateX;
            destinationY = candidateY;
            if (occupantAt(destinationX, destinationY) == 0)
            {
                foundOpenStep = true;
                break;
            }
        }

        if (!foundOpenStep || route[0] == 0 ||
            !TryFindEmpty(npc.ConnectionId, ref destinationX, ref destinationY, mapGrid, occupantAt) ||
            destinationX == npc.PositionX && destinationY == npc.PositionY ||
            !IsInsideGrid(destinationX, destinationY) || speed is < 1 or > 6)
            return false;

        movement = new LegacyNpcAiMovementPlan(
            npc.PositionX,
            npc.PositionY,
            checked((short)destinationX),
            checked((short)destinationY),
            speed);
        return true;

        int GetTerrainHeight(int x, int y) => mapGrid?.GetTerrainHeight(x, y) ?? 0;
    }

    private static bool TryFindEmpty(
        int mobId,
        ref int x,
        ref int y,
        LegacyMapGrid? mapGrid,
        Func<int, int, int> occupantAt)
        => LegacyMobGridSearch.TryFindEmpty(
            mobId,
            ref x,
            ref y,
            occupantAt,
            mapGrid is null ? static (_, _) => false : mapGrid.IsBlocked,
            checkCandidateTerrain: true,
            maximumRadius: MaximumServerCount - 1);

    private static int GetSpeed(int attackRun)
    {
        var speed = attackRun & 15;
        return Math.Clamp(speed, 1, 6);
    }

    private static bool IsInsideGrid(int x, int y)
        => x >= 0 && y >= 0 && x < MaximumGridCoordinate && y < MaximumGridCoordinate;
}

internal sealed record LegacyNpcAiMovementPlan(
    short FromX,
    short FromY,
    short ToX,
    short ToY,
    int Speed);

namespace WydCdk.World;

/// <summary>Compatibility port of TMSrv/Basedef.cpp:BASE_GetRoute for MSG_Action collision correction.</summary>
internal static class LegacyActionRouteBuilder
{
    public const int RouteLength = 24;
    private const int MaximumHeightDifference = 8;

    public static void Rebuild(
        int originX,
        int originY,
        ref int targetX,
        ref int targetY,
        byte[] route,
        int distance,
        Func<int, int, int> terrainHeightAt)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(terrainHeightAt);
        if (route.Length != RouteLength)
            throw new ArgumentException($"Legacy Action route must contain exactly {RouteLength} bytes.", nameof(route));

        var x = originX;
        var y = originY;
        var lastX = x;
        var lastY = y;
        var requestedX = targetX;
        var requestedY = targetY;
        Array.Clear(route);

        for (var index = 0; index < distance && index < RouteLength - 1; index++)
        {
            if (x < 1 || y < 1 || x > LegacyMapGrid.HeightWidth - 2 || y > LegacyMapGrid.HeightHeight - 2)
            {
                route[index] = 0;
                break;
            }

            var currentHeight = terrainHeightAt(x, y);
            var north = terrainHeightAt(x, y - 1);
            var northEast = terrainHeightAt(x + 1, y - 1);
            var east = terrainHeightAt(x + 1, y);
            var southEast = terrainHeightAt(x + 1, y + 1);
            var south = terrainHeightAt(x, y + 1);
            var southWest = terrainHeightAt(x - 1, y + 1);
            var west = terrainHeightAt(x - 1, y);
            var northWest = terrainHeightAt(x - 1, y - 1);

            if (requestedX == x && requestedY == y)
            {
                route[index] = 0;
                break;
            }

            if (TryStep(requestedX == x && requestedY < y, '2', north, 0, -1)) continue;
            if (TryStep(requestedX > x && requestedY < y, '3', northEast, 1, -1)) continue;
            if (TryStep(requestedX > x && requestedY == y, '6', east, 1, 0)) continue;
            if (TryStep(requestedX > x && requestedY > y, '9', southEast, 1, 1)) continue;
            if (TryStep(requestedX == x && requestedY > y, '8', south, 0, 1)) continue;
            if (TryStep(requestedX < x && requestedY > y, '7', southWest, -1, 1)) continue;
            if (TryStep(requestedX < x && requestedY == y, '4', west, -1, 0)) continue;
            if (TryStep(requestedX < x && requestedY < y, '1', northWest, -1, -1)) continue;

            if (TryStep(requestedX > x && requestedY < y, '6', east, 1, 0)) continue;
            if (TryStep(requestedX > x && requestedY < y, '2', north, 0, -1)) continue;
            if (TryStep(requestedX > x && requestedY > y, '6', east, 1, 0)) continue;
            if (TryStep(requestedX > x && requestedY > y, '8', south, 0, 1)) continue;
            if (TryStep(requestedX < x && requestedY > y, '4', west, -1, 0)) continue;
            if (TryStep(requestedX < x && requestedY > y, '8', south, 0, 1)) continue;
            if (TryStep(requestedX < x && requestedY < y, '4', west, -1, 0)) continue;
            if (TryStep(requestedX < x && requestedY < y, '2', north, 0, -1)) continue;

            if (requestedX == x + 1 || requestedY == y + 1 || requestedX == x - 1 || requestedY == y - 1)
            {
                route[index] = 0;
                break;
            }

            if (TryStep(requestedX == x && requestedY > y, '9', southEast, 1, 1)) continue;
            if (TryStep(requestedX == x && requestedY > y, '7', southWest, -1, 1)) continue;
            if (TryStep(requestedX == x && requestedY < y, '3', northEast, 1, -1)) continue;
            if (TryStep(requestedX == x && requestedY < y, '1', northWest, -1, -1)) continue;
            if (TryStep(requestedX < x && requestedY == y, '7', southWest, -1, 1)) continue;
            if (TryStep(requestedX < x && requestedY == y, '1', northWest, -1, -1)) continue;
            if (TryStep(requestedX > x && requestedY == y, '9', southEast, 1, 1)) continue;
            if (TryStep(requestedX > x && requestedY == y, '3', northEast, 1, -1)) continue;

            route[index] = 0;
            break;

            bool TryStep(bool targetMatches, char routeDirection, int neighborHeight, int deltaX, int deltaY)
            {
                if (!targetMatches ||
                    neighborHeight >= currentHeight + MaximumHeightDifference ||
                    neighborHeight <= currentHeight - MaximumHeightDifference)
                    return false;

                route[index] = (byte)routeDirection;
                x += deltaX;
                y += deltaY;
                return true;
            }
        }

        if (lastX == x && lastY == y)
            return;

        targetX = x;
        targetY = y;
    }
}

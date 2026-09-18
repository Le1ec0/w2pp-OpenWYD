namespace WydCdk.World;

/// <summary>
/// Small compatibility port of TMSrv/GetFunc.cpp:GetEmptyMobGrid.
/// Terrain is supplied by the caller so the same search can run with or
/// without the optional legacy 4096x4096 height grid.
/// </summary>
internal static class LegacyMobGridSearch
{
    public const int MaxGridX = 4096;
    public const int MaxGridY = 4096;

    public static bool TryFindEmpty(
        int mob,
        ref int x,
        ref int y,
        Func<int, int, int> occupantAt,
        Func<int, int, bool> blockedAt,
        bool checkCandidateTerrain = false)
    {
        ArgumentNullException.ThrowIfNull(occupantAt);
        ArgumentNullException.ThrowIfNull(blockedAt);

        if (!IsInBounds(x, y))
            return false;

        if (occupantAt(x, y) == mob)
            return true;

        if (occupantAt(x, y) == 0 && !blockedAt(x, y))
            return true;

        var originX = x;
        var originY = y;
        // The legacy implementation checks pHeightGrid[*ty][*tx] rather than
        // the candidate cell in all fallback loops. Keep that quirk by
        // default; strict candidate checks are an explicit opt-in policy.
        var originBlocked = blockedAt(originX, originY);
        for (var radius = 1; radius <= 4; radius++)
        {
            for (var candidateY = originY - radius; candidateY <= originY + radius; candidateY++)
            {
                for (var candidateX = originX - radius; candidateX <= originX + radius; candidateX++)
                {
                    if (!IsInBounds(candidateX, candidateY) || occupantAt(candidateX, candidateY) != 0 || (checkCandidateTerrain ? blockedAt(candidateX, candidateY) : originBlocked))
                        continue;

                    x = candidateX;
                    y = candidateY;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsInBounds(int x, int y) => x >= 0 && y >= 0 && x < MaxGridX && y < MaxGridY;
}

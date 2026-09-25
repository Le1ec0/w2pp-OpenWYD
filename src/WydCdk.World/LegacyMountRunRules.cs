using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>Resolves the run-speed floor from the mount equipped in legacy slot 14.</summary>
public static class LegacyMountRunRules
{
    private const int StandardMountFirstIndex = 2360;
    private const int StandardMountLastIndex = 2389;
    private const int TemporaryMountFirstIndex = 3980;
    private const int TemporaryMountLastIndex = 3994;

    // Fifth column (Speed) of the 7.69 g_pMountBonus[30][5] table.
    private static readonly byte[] StandardMountRunFloors =
    [
        4, 4, 5, 5, 4, 5,
        6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
        6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
        6, 6, 6, 6
    ];

    // The used 7.69 g_pMountTempBonus entries (codes 0..14) all have Speed=6.
    private static readonly byte[] TemporaryMountRunFloors =
    [6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6];

    /// <summary>
    /// Resolves only the source table/effect gate. The caller applies the cached
    /// face <= 4 condition in the final AttackRun stage.
    /// </summary>
    public static bool TryResolveRunFloor(LegacyItem mount, out int runFloor)
    {
        runFloor = 0;
        var index = (int)mount.Index;

        if (index is >= TemporaryMountFirstIndex and <= TemporaryMountLastIndex)
        {
            runFloor = TemporaryMountRunFloors[index - TemporaryMountFirstIndex];
            return true;
        }

        // The C++ final block's inclusive upper bound admits 2390, then indexes
        // g_pMountBonus[30] out of bounds. Do not reproduce that undefined read.
        if (index is < StandardMountFirstIndex or > StandardMountLastIndex || mount.Value1 <= 0)
            return false;

        runFloor = StandardMountRunFloors[index - StandardMountFirstIndex];
        return true;
    }
}

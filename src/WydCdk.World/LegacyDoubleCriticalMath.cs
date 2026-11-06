namespace WydCdk.World;

/// <summary>Server-side 7.69 DoubleCritical base flags and normalized Progress.</summary>
public static class LegacyDoubleCriticalMath
{
    private static readonly int[] HitRate = CreateHitRate();

    public static LegacyDoubleCriticalResult Resolve(
        byte attackRun,
        sbyte critical,
        ushort sessionProgress,
        ushort clientProgress,
        int criticalChanceRoll)
    {
        if (criticalChanceRoll is < 0 or >= 255)
            throw new ArgumentOutOfRangeException(nameof(criticalChanceRoll), criticalChanceRoll, "The legacy Critical roll must be between 0 and 254.");

        // TMSrv adopts client Progress when it differs from the session counter.
        // Normalize first so malformed unsigned values cannot index beyond g_pHitRate[1024].
        var progress = sessionProgress & 0x03FF;
        var clientIndex = clientProgress & 0x03FF;
        if (clientIndex != progress)
            progress = clientIndex;
        var flags = 0;
        var hitValue = 100 * ((attackRun >> 4) - 5);
        if (HitRate[progress] < hitValue)
            flags |= 1;
        if (criticalChanceRoll < critical)
            flags |= 2;

        var nextProgress = (ushort)((progress + 1) & 0x03FF);
        return new LegacyDoubleCriticalResult((byte)flags, nextProgress);
    }

    private static int[] CreateHitRate()
    {
        var hitRate = new int[1024];
        var start = 0;
        var quadrant = 0;
        for (var index = 0; index < hitRate.Length; index++)
        {
            hitRate[index] = quadrant switch
            {
                0 => start,
                1 => 512 - start,
                2 => start + 512,
                _ => Math.Min(999, 1024 - start),
            };
            if (quadrant == 3)
                start++;
            quadrant = (quadrant + 1) & 3;
        }

        // BASE_InitializeHitRate overwrites this slot after its four-quadrant fill.
        hitRate[0] = 512;
        return hitRate;
    }
}

public readonly record struct LegacyDoubleCriticalResult(byte Flags, ushort NextProgress);

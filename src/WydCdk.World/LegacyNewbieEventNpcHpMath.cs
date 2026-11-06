namespace WydCdk.World;

/// <summary>Legacy initial HP adjustment for generated NPCs during NewbieEventServer.</summary>
public static class LegacyNewbieEventNpcHpMath
{
    public static int ApplyInitialHp(int maxHp, int level, bool newbieEventServer)
    {
        if (!newbieEventServer || level >= 120)
            return maxHp;

        return unchecked(3 * maxHp) / 4;
    }
}

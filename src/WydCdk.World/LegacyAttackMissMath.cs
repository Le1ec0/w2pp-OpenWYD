namespace WydCdk.World;

/// <summary>Reproduces the hit/miss rate used by the 7.69 TMSrv MSG_Attack handler.</summary>
public static class LegacyAttackMissMath
{
    public static int GetRatePercent(int targetDexterity, int attackerDexterity, ushort attackerRsv)
    {
        var min = targetDexterity;
        var max = attackerDexterity;

        if ((attackerRsv & 0x20) != 0) max += 100;
        if ((attackerRsv & 0x40) != 0) max += 100;
        if ((attackerRsv & 0x80) != 0) max += 50;
        if ((attackerRsv & 0x200) != 0) max += 50;

        var result = 1;
        const int targetLeadCap = 75;

        if (min > max)
        {
            result = (min - max) / 100;
            result *= 3;
            if (result > targetLeadCap) result = targetLeadCap;
        }

        if (max > min)
        {
            result = (max - min) / 100;
            if (result > 10) result = 10;
        }

        return result;
    }
}

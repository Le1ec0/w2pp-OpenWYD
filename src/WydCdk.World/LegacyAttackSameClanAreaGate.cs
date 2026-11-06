namespace WydCdk.World;

/// <summary>Reproduces the 7.69/W2PP same-Clan damage suppression area in MSG_Attack.</summary>
public static class LegacyAttackSameClanAreaGate
{
    public const short MinX = 1_017;
    public const short MaxX = 1_290;
    public const short MinY = 1_911;
    public const short MaxY = 2_183;

    public static bool ShouldClearTargetId(int attackerX, int attackerY, int attackerClan, int targetClan, int damage) =>
        damage > 0 &&
        attackerClan == targetClan &&
        attackerX >= MinX && attackerX <= MaxX &&
        attackerY >= MinY && attackerY <= MaxY;
}

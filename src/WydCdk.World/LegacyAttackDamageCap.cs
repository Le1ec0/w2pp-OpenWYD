namespace WydCdk.World;

/// <summary>Applies the TMProject 7.69 TMSrv damage ceiling used by the MSG_Attack common tail.</summary>
public static class LegacyAttackDamageCap
{
    public const int MaximumDamage = 1_000_000;

    public static int Apply(int damage) =>
        damage >= MaximumDamage ? MaximumDamage : damage;
}

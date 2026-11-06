namespace WydCdk.World;

/// <summary>Matches the Clan 6 damage suppression shared by TMProject 7.69 and W2PP MSG_Attack.</summary>
public static class LegacyAttackClanSixDamageGate
{
    public static bool ShouldSuppressDamage(byte targetClan, int handlerLevel) =>
        targetClan == 6 && handlerLevel < LegacySkillCombatMath.LegacyMaxLevel;
}

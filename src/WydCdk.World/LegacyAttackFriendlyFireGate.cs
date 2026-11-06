namespace WydCdk.World;

/// <summary>Applies the 7.69 hostile-skill same-leader/effective-guild damage gate.</summary>
public static class LegacyAttackFriendlyFireGate
{
    public static bool ShouldSuppressDamage(
        int attackerLeaderConnectionId,
        int targetLeaderConnectionId,
        int effectiveAttackerGuildId,
        int effectiveTargetGuildId) =>
        attackerLeaderConnectionId == targetLeaderConnectionId ||
        effectiveAttackerGuildId == effectiveTargetGuildId;
}

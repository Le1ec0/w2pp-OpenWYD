namespace WydCdk.World;

public enum LegacyAttackSamaritanCleanupResult
{
    Cleared,
    ParticipantNotFound,
    CombatStateUnavailable,
    NoAffect,
    ItemDataUnavailable,
    ScoreProjectionUnavailable,
}

public sealed record LegacyAttackSamaritanCleanupOutcome(byte[] MobSnapshot, byte[] AffectSnapshot);

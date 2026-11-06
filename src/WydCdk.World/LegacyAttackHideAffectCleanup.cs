namespace WydCdk.World;

public enum LegacyAttackHideCleanupResult
{
    Cleared,
    ParticipantNotFound,
    CombatStateUnavailable,
    NotApplicable,
    NoAffect,
    ItemDataUnavailable,
    ScoreProjectionUnavailable,
}

public sealed record LegacyAttackHideCleanupOutcome(byte[] MobSnapshot, byte[] AffectSnapshot);

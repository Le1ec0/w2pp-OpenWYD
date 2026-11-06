namespace WydCdk.World;

public enum LegacyClassOneType6CleanupResult
{
    Cleared,
    NotApplicable,
    NoAffect,
    ParticipantNotFound,
    CombatStateUnavailable,
    ItemDataUnavailable,
    ScoreProjectionUnavailable,
}

public sealed record LegacyClassOneType6CleanupOutcome(byte[] MobSnapshot, byte[] AffectSnapshot);

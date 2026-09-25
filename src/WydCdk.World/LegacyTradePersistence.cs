namespace WydCdk.World;

public sealed record LegacyCharacterStateSaveRequest(
    string AccountName,
    int CharacterSlot,
    byte[] Mob,
    short PositionX,
    short PositionY,
    byte[] MobExtra);

public enum AtomicCharacterStateSaveResult
{
    Success,
    InvalidRequest,
    AccountNotFound,
    CharacterNotAvailable,
    NotSupported,
}

/// <summary>
/// Optional storage capability for a trade completion. Implementations must
/// commit both character states together or leave both unchanged.
/// </summary>
public interface IAtomicCharacterStateStore
{
    ValueTask<AtomicCharacterStateSaveResult> TrySaveCharacterStatesAtomicallyAsync(
        LegacyCharacterStateSaveRequest first,
        LegacyCharacterStateSaveRequest second,
        CancellationToken cancellationToken = default);
}

public static class LegacyTradePersistence
{
    public static bool TryBuild(
        LegacyTradeCompletionOutcome outcome,
        out LegacyTradePersistencePlan? plan)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        plan = null;

        if (!outcome.RequiresPersistence ||
            string.IsNullOrWhiteSpace(outcome.FirstAccountName) ||
            string.IsNullOrWhiteSpace(outcome.SecondAccountName) ||
            outcome.FirstCharacterSlot < 0 ||
            outcome.SecondCharacterSlot < 0 ||
            outcome.FirstMobSnapshot.Length != LegacyAccountSnapshot.CharacterStride ||
            outcome.SecondMobSnapshot.Length != LegacyAccountSnapshot.CharacterStride ||
            outcome.FirstMobExtraSnapshot.Length != LegacyAccountSnapshot.MobExtraStride ||
            outcome.SecondMobExtraSnapshot.Length != LegacyAccountSnapshot.MobExtraStride ||
            string.Equals(outcome.FirstAccountName, outcome.SecondAccountName, StringComparison.OrdinalIgnoreCase))
            return false;

        plan = new LegacyTradePersistencePlan(
            new LegacyCharacterStateSaveRequest(
                outcome.FirstAccountName,
                outcome.FirstCharacterSlot,
                outcome.FirstMobSnapshot.ToArray(),
                outcome.FirstPositionX,
                outcome.FirstPositionY,
                outcome.FirstMobExtraSnapshot.ToArray()),
            new LegacyCharacterStateSaveRequest(
                outcome.SecondAccountName,
                outcome.SecondCharacterSlot,
                outcome.SecondMobSnapshot.ToArray(),
                outcome.SecondPositionX,
                outcome.SecondPositionY,
                outcome.SecondMobExtraSnapshot.ToArray()));
        return true;
    }
}

public sealed record LegacyTradePersistencePlan(
    LegacyCharacterStateSaveRequest First,
    LegacyCharacterStateSaveRequest Second);

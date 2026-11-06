using WydCdk.Protocol;
using WydCdk.World;

namespace WydCdk.Server;

internal sealed record LegacyTinyCombineDispatchResult(
    LegacyTinyCombinePreparationResult PreparationResult,
    LegacyTinyCombineApplicationResult? ApplicationResult,
    LegacyTinyCombineOutcome? Outcome,
    IReadOnlyList<byte[]>? ResponseFrames,
    bool ResponsePlanningFailed,
    bool RollbackAttempted,
    bool RollbackSucceeded);

/// <summary>
/// Keeps the account reservation across Tiny preparation, apply, response planning,
/// and conditional rollback. The caller handles trade cleanup, storage, and socket I/O
/// only after this method returns and releases the reservation.
/// </summary>
internal static class LegacyTinyCombineDispatchCoordinator
{
    public static async ValueTask<LegacyTinyCombineDispatchResult> ExecuteAsync(
        LegacyAccountOperationGate accountOperationGate,
        string accountName,
        WorldHub world,
        int connectionId,
        IReadOnlyList<LegacyItem> requestedItems,
        IReadOnlyList<sbyte> inventoryPositions,
        int tinyBase,
        int rawRoll,
        bool localServer,
        Func<LegacyTinyCombineOutcome, CancellationToken, ValueTask<IReadOnlyList<byte[]>?>> responsePlanner,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountOperationGate);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(requestedItems);
        ArgumentNullException.ThrowIfNull(inventoryPositions);
        ArgumentNullException.ThrowIfNull(responsePlanner);

        await using var accountLease = await accountOperationGate.AcquireAsync([accountName], cancellationToken);

        var preparationResult = world.TryPrepareTinyCombine(
            connectionId,
            requestedItems,
            inventoryPositions,
            tinyBase,
            out var plan);
        if (preparationResult != LegacyTinyCombinePreparationResult.Accepted || plan is null)
            return new(preparationResult, null, null, null, false, false, false);

        var applicationResult = world.TryApplyTinyCombine(
            connectionId,
            plan,
            rawRoll,
            localServer,
            out var outcome);
        if (applicationResult != LegacyTinyCombineApplicationResult.Accepted || outcome is null)
            return new(preparationResult, applicationResult, null, null, false, false, false);

        var responseFrames = await responsePlanner(outcome, cancellationToken);
        if (responseFrames is null || responseFrames.Count == 0)
        {
            var rollbackSucceeded = world.TryRollbackTinyCombine(outcome);
            return new(preparationResult, applicationResult, outcome, null, true, true, rollbackSucceeded);
        }

        return new(preparationResult, applicationResult, outcome, responseFrames, false, false, false);
    }
}

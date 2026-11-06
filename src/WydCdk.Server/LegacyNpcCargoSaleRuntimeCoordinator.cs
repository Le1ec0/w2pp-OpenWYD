using WydCdk.World;

namespace WydCdk.Server;

public enum LegacyNpcCargoSaleRuntimeCommitStatus
{
    Committed,
    Conflict,
    InvalidPlan,
    StoreUnavailable,
    RecoveryRequired,
}

public sealed record LegacyNpcCargoSaleRuntimeCommitOutcome(
    LegacyNpcCargoSaleRuntimeCommitStatus Status,
    LegacyAccountCargoSnapshot? CargoSnapshot,
    LegacyNpcCargoSaleCommitRequest? CommitRequest);

/// <summary>Coordinates the authoritative Cargo CAS and its revisioned in-process owner.</summary>
public sealed class LegacyNpcCargoSaleRuntimeCoordinator(
    IAccountSnapshotStore? snapshots,
    IAtomicNpcCargoSaleStore? mutations,
    LegacyAccountCargoRuntime cargoRuntime,
    LegacyAccountOperationGate accountOperationGate)
{
    public async ValueTask<LegacyNpcCargoSaleRuntimeCommitOutcome> CommitAsync(
        LegacyNpcSellCompletionPlan plan,
        string accountName,
        int characterSlot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Source.Kind != LegacyNpcSellSourceKind.Cargo ||
            plan.ExpectedCargo is not { } expectedCargo ||
            plan.UpdatedCargo is not { } newCargo ||
            plan.CargoRevision is not { } revision ||
            !LegacyAccountNameRules.TryNormalize(accountName, out var canonical) || canonical != accountName ||
            characterSlot is < 0 or >= LegacyAccountSnapshot.CharacterCount)
            return new(LegacyNpcCargoSaleRuntimeCommitStatus.InvalidPlan, null, null);
        if (mutations is null)
            return new(LegacyNpcCargoSaleRuntimeCommitStatus.StoreUnavailable, null, null);

        var request = new LegacyNpcCargoSaleCommitRequest(
            canonical,
            characterSlot,
            expectedCargo.ToArray(),
            newCargo.ToArray(),
            plan.CoinBefore,
            plan.CoinAfter);
        LegacyNpcCargoSaleCommitResult result;
        try
        {
            result = await mutations.TryCommitNpcCargoSaleAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result = LegacyNpcCargoSaleCommitResult.Ambiguous;
        }
        catch (Exception)
        {
            result = LegacyNpcCargoSaleCommitResult.Ambiguous;
        }
        if (result is LegacyNpcCargoSaleCommitResult.Conflict or LegacyNpcCargoSaleCommitResult.Ambiguous)
        {
            var current = await TryReadAuthoritativeAsync(canonical, CancellationToken.None).ConfigureAwait(false);
            if (current is null)
            {
                accountOperationGate.MarkRecoveryRequired(canonical, "NPC Cargo CAS outcome could not be reconciled from authoritative storage");
                return new(LegacyNpcCargoSaleRuntimeCommitStatus.RecoveryRequired, null, request);
            }

            var currentCargoBytes = LegacyNpcCargoSalePersistenceRules.SerializeCargo(current.Cargo);
            var currentCoin = current.Characters.Slots[characterSlot].Coin;
            var matchesNew = currentCoin == request.NewMobCoin &&
                currentCargoBytes.AsSpan().SequenceEqual(request.NewCargo);
            var matchesBefore = currentCoin == request.ExpectedMobCoin &&
                currentCargoBytes.AsSpan().SequenceEqual(request.ExpectedCargo);
            if (matchesNew)
                result = LegacyNpcCargoSaleCommitResult.Success;
            else
            {
                var owner = cargoRuntime.LoadAuthoritative(canonical, current.Cargo);
                if ((result == LegacyNpcCargoSaleCommitResult.Ambiguous && !matchesBefore) ||
                    currentCoin != request.ExpectedMobCoin)
                    accountOperationGate.MarkRecoveryRequired(canonical, "NPC Cargo CAS disagrees with live seller MOB.Coin or both before/after snapshots");
                return new(accountOperationGate.IsRecoveryRequired(canonical)
                    ? LegacyNpcCargoSaleRuntimeCommitStatus.RecoveryRequired
                    : LegacyNpcCargoSaleRuntimeCommitStatus.Conflict, owner, request);
            }
        }

        if (result != LegacyNpcCargoSaleCommitResult.Success)
        {
            return new(result == LegacyNpcCargoSaleCommitResult.Conflict
                ? LegacyNpcCargoSaleRuntimeCommitStatus.Conflict
                : LegacyNpcCargoSaleRuntimeCommitStatus.StoreUnavailable, null, request);
        }

        var beforeItems = LegacyNpcCargoSalePersistenceRules.DeserializeCargo(request.ExpectedCargo);
        var afterItems = LegacyNpcCargoSalePersistenceRules.DeserializeCargo(request.NewCargo);
        if (!cargoRuntime.TryCommit(canonical, revision, beforeItems, afterItems, out var committed) || committed is null)
        {
            accountOperationGate.MarkRecoveryRequired(canonical, "NPC Cargo CAS committed but runtime owner revision changed");
            return new(LegacyNpcCargoSaleRuntimeCommitStatus.RecoveryRequired, null, request);
        }

        accountOperationGate.ResolveRecovery(canonical);
        return new(LegacyNpcCargoSaleRuntimeCommitStatus.Committed, committed, request);
    }

    public async ValueTask<bool> TryCompensateAsync(
        LegacyNpcCargoSaleRuntimeCommitOutcome committed,
        CancellationToken cancellationToken = default)
    {
        if (committed.Status != LegacyNpcCargoSaleRuntimeCommitStatus.Committed ||
            committed.CargoSnapshot is not { } owner ||
            committed.CommitRequest is not { } request || mutations is null)
            return false;

        var reverse = new LegacyNpcCargoSaleCommitRequest(
            request.AccountName,
            request.CharacterSlot,
            request.NewCargo.ToArray(),
            request.ExpectedCargo.ToArray(),
            request.NewMobCoin,
            request.ExpectedMobCoin);
        LegacyNpcCargoSaleCommitResult reverseResult;
        try
        {
            reverseResult = await mutations.TryCommitNpcCargoSaleAsync(reverse, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            reverseResult = LegacyNpcCargoSaleCommitResult.Ambiguous;
        }
        if (reverseResult == LegacyNpcCargoSaleCommitResult.Success)
        {
            if (cargoRuntime.TryCommit(
                    request.AccountName,
                    owner.Revision,
                    LegacyNpcCargoSalePersistenceRules.DeserializeCargo(request.NewCargo),
                    LegacyNpcCargoSalePersistenceRules.DeserializeCargo(request.ExpectedCargo),
                    out _))
            {
                accountOperationGate.ResolveRecovery(request.AccountName);
                return true;
            }
        }

        if (await TryReadAuthoritativeAsync(request.AccountName, CancellationToken.None).ConfigureAwait(false) is { } current)
        {
            var reconciled = cargoRuntime.LoadAuthoritative(request.AccountName, current.Cargo);
            var cargoBytes = LegacyNpcCargoSalePersistenceRules.SerializeCargo(current.Cargo);
            var rolledBack = current.Characters.Slots[request.CharacterSlot].Coin == request.ExpectedMobCoin &&
                cargoBytes.AsSpan().SequenceEqual(request.ExpectedCargo);
            if (rolledBack)
            {
                accountOperationGate.ResolveRecovery(request.AccountName);
                return true;
            }

            _ = reconciled;
        }

        accountOperationGate.MarkRecoveryRequired(request.AccountName, "NPC Cargo CAS compensation could not be confirmed");
        return false;
    }

    private async ValueTask<LegacyAccountSnapshot?> TryReadAuthoritativeAsync(
        string accountName,
        CancellationToken cancellationToken)
    {
        if (snapshots is null)
            return null;
        try
        {
            return await snapshots.ReadSnapshotAsync(accountName, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

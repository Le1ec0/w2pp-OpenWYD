using System.Net;

namespace WydCdk.World;

public enum ActionAccountReviewResult
{
    NotRequired,
    Marked,
    AlreadyMarked,
    PersistenceUnavailable,
}

/// <summary>Persists the 7.69 high-severity Action speed decision before the host relays movement.</summary>
public sealed class LegacyActionAccountReviewCoordinator(IAccountReviewStore store)
{
    public async ValueTask<ActionAccountReviewResult> ReviewSpeedViolationAsync(
        string accountName,
        string? characterName,
        string? remoteAddress,
        int requestedSpeed,
        int allowedSpeed,
        int attackRun,
        CancellationToken cancellationToken = default)
    {
        if (!LegacyMovementRules.RequiresAccountAnalysis(requestedSpeed, attackRun) ||
            allowedSpeed != (attackRun & 0xF))
            return ActionAccountReviewResult.NotRequired;
        if (!LegacyAccountNameRules.TryNormalize(accountName, out var normalizedAccount))
            return ActionAccountReviewResult.PersistenceUnavailable;

        var normalizedAddress = remoteAddress is not null && IPAddress.TryParse(remoteAddress, out var address)
            ? address.ToString()
            : null;
        var evidence = new AccountReviewEvidence(
            normalizedAccount,
            FileAccountReviewStore.HighSeverityActionSpeedReason,
            string.IsNullOrWhiteSpace(characterName) ? null : characterName,
            normalizedAddress,
            requestedSpeed,
            allowedSpeed,
            attackRun,
            DateTimeOffset.UtcNow);

        try
        {
            var markResult = await store.MarkForAnalysisAsync(evidence, cancellationToken);
            return markResult switch
            {
                AccountReviewMarkResult.Marked => ActionAccountReviewResult.Marked,
                AccountReviewMarkResult.AlreadyMarked => ActionAccountReviewResult.AlreadyMarked,
                _ => ActionAccountReviewResult.PersistenceUnavailable,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ActionAccountReviewResult.PersistenceUnavailable;
        }
    }
}

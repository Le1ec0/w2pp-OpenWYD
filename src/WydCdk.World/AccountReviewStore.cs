namespace WydCdk.World;

public enum AccountReviewStatus
{
    NotMarked,
    Analysis,
}

public enum AccountReviewMarkResult
{
    Marked,
    AlreadyMarked,
}

public sealed record AccountReviewEvidence(
    string AccountName,
    string ReasonCode,
    string? CharacterName,
    string? RemoteAddress,
    int RequestedSpeed,
    int AllowedSpeed,
    int AttackRun,
    DateTimeOffset DetectedAtUtc);

public interface IAccountReviewStore
{
    ValueTask<AccountReviewStatus> ReadStatusAsync(string accountName, CancellationToken cancellationToken = default);
    ValueTask<AccountReviewMarkResult> MarkForAnalysisAsync(AccountReviewEvidence evidence, CancellationToken cancellationToken = default);
}

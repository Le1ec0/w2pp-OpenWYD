namespace WydCdk.World;

public enum GuildInvitePairSaveResult
{
    Success,
    Conflict,
    InvalidRequest,
    AccountNotFound,
    CharacterNotAvailable,
    Unavailable,
    Ambiguous,
}

public sealed record GuildInviteCharacterUpdate(
    string AccountName,
    int CharacterSlot,
    int ExpectedGuildId,
    int ExpectedCoin,
    int NewGuildId,
    int NewCoin);

public sealed record GuildInvitePairSaveRequest(
    GuildInviteCharacterUpdate Source,
    GuildInviteCharacterUpdate Target);

/// <summary>Optional storage capability for durably applying both sides of a guild invite as one operation.</summary>
public interface IAtomicCharacterGuildInviteStore
{
    ValueTask<GuildInvitePairSaveResult> TrySaveGuildInvitePairAsync(
        GuildInvitePairSaveRequest request,
        CancellationToken cancellationToken = default);
}

public static class LegacyGuildInvitePersistence
{
    public static bool TryBuild(GuildInvitePlan plan, out GuildInvitePairSaveRequest? request)
    {
        ArgumentNullException.ThrowIfNull(plan);
        request = null;
        if (plan.SourceConnectionId <= 0 || plan.TargetConnectionId <= 0 ||
            string.IsNullOrWhiteSpace(plan.SourceAccountName) || string.IsNullOrWhiteSpace(plan.TargetAccountName) ||
            plan.SourceCharacterSlot is < 0 or >= LegacyAccountSnapshot.CharacterCount ||
            plan.TargetCharacterSlot is < 0 or >= LegacyAccountSnapshot.CharacterCount ||
            plan.SourceConnectionId == plan.TargetConnectionId ||
            !LegacyAccountNameRules.TryNormalize(plan.SourceAccountName, out var sourceAccount) ||
            !LegacyAccountNameRules.TryNormalize(plan.TargetAccountName, out var targetAccount) ||
            string.Equals(sourceAccount, targetAccount, StringComparison.Ordinal) && plan.SourceCharacterSlot == plan.TargetCharacterSlot ||
            plan.GuildId is <= 0 or > ushort.MaxValue || plan.Cost < 0 || plan.SourceCoin < plan.Cost || plan.TargetCoin < 0)
            return false;

        request = new GuildInvitePairSaveRequest(
            new GuildInviteCharacterUpdate(sourceAccount, plan.SourceCharacterSlot, plan.GuildId, plan.SourceCoin, plan.GuildId, plan.SourceCoin - plan.Cost),
            new GuildInviteCharacterUpdate(targetAccount, plan.TargetCharacterSlot, 0, plan.TargetCoin, plan.GuildId, plan.TargetCoin));
        return true;
    }

    public static bool IsValid(GuildInvitePairSaveRequest? request)
    {
        if (request is null || !IsValid(request.Source) || !IsValid(request.Target))
            return false;

        var sameAccount = string.Equals(request.Source.AccountName, request.Target.AccountName, StringComparison.Ordinal);
        return (!sameAccount || request.Source.CharacterSlot != request.Target.CharacterSlot) &&
            request.Source.ExpectedGuildId > 0 &&
            request.Source.NewGuildId == request.Source.ExpectedGuildId &&
            request.Target.ExpectedGuildId == 0 &&
            request.Target.NewGuildId == request.Source.ExpectedGuildId &&
            request.Source.NewCoin < request.Source.ExpectedCoin &&
            request.Target.NewCoin == request.Target.ExpectedCoin;
    }

    private static bool IsValid(GuildInviteCharacterUpdate? update) =>
        update is not null &&
        LegacyAccountNameRules.TryNormalize(update.AccountName, out var normalized) && normalized == update.AccountName &&
        update.CharacterSlot is >= 0 and < LegacyAccountSnapshot.CharacterCount &&
        update.ExpectedGuildId is >= 0 and <= ushort.MaxValue &&
        update.NewGuildId is >= 0 and <= ushort.MaxValue;
}

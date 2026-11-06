using System.Collections.ObjectModel;

namespace WydCdk.World;

/// <summary>Pure directed GuildWar transitions from TMProject 7.69 TMSrv::DoWar.</summary>
public static class LegacyGuildWarControl
{
    public const int MaxGuildId = ushort.MaxValue;

    public static LegacyGuildWarTransitionResult Declare(
        IReadOnlyDictionary<int, int> guildWarPartners,
        int guildId,
        int enemyGuildId)
    {
        ArgumentNullException.ThrowIfNull(guildWarPartners);
        var next = new Dictionary<int, int>(guildWarPartners);
        if (!IsGuildId(guildId) || !IsGuildId(enemyGuildId))
            return CreateResult(LegacyGuildWarTransitionStatus.InvalidRequest, next);

        // DoWar treats an out-of-range current pointer as zero before deciding
        // whether this guild can declare another war.
        if (GetNormalizedPartner(next, guildId) != 0)
            return CreateResult(LegacyGuildWarTransitionStatus.RequesterAlreadyMapped, next);

        if (next.TryGetValue(enemyGuildId, out var enemyPartner) && enemyPartner == guildId)
        {
            next[guildId] = enemyGuildId;
            return CreateResult(
                LegacyGuildWarTransitionStatus.WarAccepted,
                next,
                [CreateNotice(LegacyGuildWarNoticeKind.WarStarted, guildId, enemyGuildId, isGlobal: true)],
                [guildId, enemyGuildId]);
        }

        next[guildId] = enemyGuildId;
        return CreateResult(
            LegacyGuildWarTransitionStatus.DeclarationCreated,
            next,
            [CreateNotice(LegacyGuildWarNoticeKind.Declaration, guildId, enemyGuildId)]);
    }

    public static LegacyGuildWarTransitionResult Cancel(
        IReadOnlyDictionary<int, int> guildWarPartners,
        int guildId)
    {
        ArgumentNullException.ThrowIfNull(guildWarPartners);
        var next = new Dictionary<int, int>(guildWarPartners);
        if (!IsGuildId(guildId))
            return CreateResult(LegacyGuildWarTransitionStatus.InvalidRequest, next);

        var enemyGuildId = GetNormalizedPartner(next, guildId);
        if (enemyGuildId == 0)
            return CreateResult(LegacyGuildWarTransitionStatus.NoWar, next);

        if (next.TryGetValue(enemyGuildId, out var enemyPartner) && enemyPartner == guildId)
        {
            next.Remove(enemyGuildId);
            next.Remove(guildId);
            return CreateResult(
                LegacyGuildWarTransitionStatus.WarCancelled,
                next,
                [CreateNotice(LegacyGuildWarNoticeKind.WarCancelled, guildId, enemyGuildId)],
                [guildId, enemyGuildId]);
        }

        next.Remove(guildId);
        return CreateResult(
            LegacyGuildWarTransitionStatus.PendingDeclarationCancelled,
            next,
            [CreateNotice(LegacyGuildWarNoticeKind.DeclarationCancelled, guildId, enemyGuildId)]);
    }

    public static bool IsGuildId(int guildId) => guildId is >= 1 and <= MaxGuildId;

    private static int GetNormalizedPartner(IReadOnlyDictionary<int, int> guildWarPartners, int guildId)
    {
        if (!guildWarPartners.TryGetValue(guildId, out var partner) || partner < 0 || partner > MaxGuildId)
            return 0;
        return partner;
    }

    private static LegacyGuildWarNotice CreateNotice(
        LegacyGuildWarNoticeKind kind,
        int guildId,
        int enemyGuildId,
        bool isGlobal = false) =>
        new(kind, guildId, enemyGuildId, isGlobal,
            isGlobal ? Array.Empty<int>() : Array.AsReadOnly(new[] { guildId, enemyGuildId }));

    private static LegacyGuildWarTransitionResult CreateResult(
        LegacyGuildWarTransitionStatus status,
        Dictionary<int, int> partners,
        IReadOnlyList<LegacyGuildWarNotice>? notices = null,
        IReadOnlyList<int>? guildIdsToRefresh = null) =>
        new(
            status,
            new ReadOnlyDictionary<int, int>(partners),
            notices ?? Array.Empty<LegacyGuildWarNotice>(),
            guildIdsToRefresh ?? Array.Empty<int>());
}

public enum LegacyGuildWarTransitionStatus
{
    InvalidRequest,
    RequesterAlreadyMapped,
    DeclarationCreated,
    WarAccepted,
    PendingDeclarationCancelled,
    WarCancelled,
    NoWar,
}

public enum LegacyGuildWarNoticeKind
{
    Declaration,
    DeclarationCancelled,
    WarCancelled,
    WarStarted,
}

public sealed record LegacyGuildWarNotice(
    LegacyGuildWarNoticeKind Kind,
    int GuildId,
    int EnemyGuildId,
    bool IsGlobal,
    IReadOnlyList<int> RecipientGuildIds);

public sealed record LegacyGuildWarTransitionResult(
    LegacyGuildWarTransitionStatus Status,
    IReadOnlyDictionary<int, int> GuildWarPartners,
    IReadOnlyList<LegacyGuildWarNotice> Notices,
    IReadOnlyList<int> GuildIdsToRefresh);

using System.Collections.ObjectModel;

namespace WydCdk.World;

/// <summary>Pure directed GuildAlly toggle from TMProject 7.69 TMSrv::DoAlly.</summary>
public static class LegacyGuildAllyControl
{
    public const int MaxGuildId = ushort.MaxValue;

    public static LegacyGuildAllyTransitionResult Toggle(
        IReadOnlyDictionary<int, int> guildAllies,
        int guildId,
        int requestedAllyGuildId)
    {
        ArgumentNullException.ThrowIfNull(guildAllies);
        var next = new Dictionary<int, int>(guildAllies);
        if (!IsGuildId(guildId) || !IsGuildId(requestedAllyGuildId))
            return CreateResult(LegacyGuildAllyTransitionStatus.InvalidRequest, next);

        var currentAllyGuildId = next.TryGetValue(guildId, out var current) && IsGuildId(current)
            ? current
            : 0;
        if (currentAllyGuildId == 0)
        {
            next[guildId] = requestedAllyGuildId;
            return CreateResult(
                LegacyGuildAllyTransitionStatus.AllyDeclared,
                next,
                new LegacyGuildAllyNotice(LegacyGuildAllyNoticeKind.Declared, guildId, requestedAllyGuildId),
                [guildId]);
        }

        next.Remove(guildId);
        return CreateResult(
            LegacyGuildAllyTransitionStatus.AllyCancelled,
            next,
            new LegacyGuildAllyNotice(LegacyGuildAllyNoticeKind.Cancelled, guildId, currentAllyGuildId),
            [guildId]);
    }

    public static bool IsGuildId(int guildId) => guildId is >= 1 and <= MaxGuildId;

    private static LegacyGuildAllyTransitionResult CreateResult(
        LegacyGuildAllyTransitionStatus status,
        Dictionary<int, int> guildAllies,
        LegacyGuildAllyNotice? notice = null,
        IReadOnlyList<int>? refreshGuildIds = null) =>
        new(
            status,
            new ReadOnlyDictionary<int, int>(guildAllies),
            notice,
            refreshGuildIds ?? Array.Empty<int>());
}

public enum LegacyGuildAllyTransitionStatus
{
    InvalidRequest,
    AllyDeclared,
    AllyCancelled,
}

public enum LegacyGuildAllyNoticeKind
{
    Declared,
    Cancelled,
}

public sealed record LegacyGuildAllyNotice(
    LegacyGuildAllyNoticeKind Kind,
    int GuildId,
    int AllyGuildId);

public sealed record LegacyGuildAllyTransitionResult(
    LegacyGuildAllyTransitionStatus Status,
    IReadOnlyDictionary<int, int> GuildAllies,
    LegacyGuildAllyNotice? Notice,
    IReadOnlyList<int> RefreshGuildIds);

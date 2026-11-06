using System.Collections.ObjectModel;

namespace WydCdk.World;

public sealed partial class WorldHub
{
    /// <summary>Applies a client declaration only when its active character owns Parm1 and is GuildLevel 9.</summary>
    public bool TryApplyGuildWarRequest(
        int connectionId,
        int requestedGuildId,
        int enemyGuildId,
        out LegacyGuildWarUpdate? update)
    {
        lock (gate)
        {
            update = null;
            if (!participants.TryGetValue(connectionId, out var participant) ||
                participant.Mob.Length < LegacyAccountSnapshot.CharacterStride ||
                participant.GuildLevel != 9 ||
                participant.GuildId != requestedGuildId ||
                !LegacyGuildWarControl.IsGuildId(requestedGuildId) ||
                !LegacyGuildWarControl.IsGuildId(enemyGuildId))
                return false;

            update = ApplyGuildWarTransitionLocked(LegacyGuildWarControl.Declare(
                combatWorldState.GuildWarPartners ?? EmptyGuildWarMap,
                requestedGuildId,
                enemyGuildId));
            return true;
        }
    }

    /// <summary>Applies the trusted internal GuildWar cancel command; client Enemy=0 is never routed here.</summary>
    public bool TryCancelGuildWar(int guildId, out LegacyGuildWarUpdate? update)
    {
        lock (gate)
        {
            update = null;
            if (!LegacyGuildWarControl.IsGuildId(guildId))
                return false;

            update = ApplyGuildWarTransitionLocked(LegacyGuildWarControl.Cancel(
                combatWorldState.GuildWarPartners ?? EmptyGuildWarMap,
                guildId));
            return true;
        }
    }

    private static readonly IReadOnlyDictionary<int, int> EmptyGuildWarMap =
        new ReadOnlyDictionary<int, int>(new Dictionary<int, int>());

    private LegacyGuildWarUpdate ApplyGuildWarTransitionLocked(LegacyGuildWarTransitionResult transition)
    {
        if (transition.Status is LegacyGuildWarTransitionStatus.DeclarationCreated or
            LegacyGuildWarTransitionStatus.WarAccepted or
            LegacyGuildWarTransitionStatus.PendingDeclarationCancelled or
            LegacyGuildWarTransitionStatus.WarCancelled)
        {
            combatWorldState = combatWorldState with
            {
                GuildWarPartners = new ReadOnlyDictionary<int, int>(new Dictionary<int, int>(transition.GuildWarPartners)),
            };
        }

        var noticeDeliveries = transition.Notices
            .Select(notice => new LegacyGuildWarNoticeDelivery(
                notice,
                Array.AsReadOnly(GetGuildWarNoticeRecipientsLocked(notice))))
            .ToArray();
        var clan = guildZones is not null && guildZones.Clans.Count > 4 ? guildZones.Clans[4] : 0;
        var sendWarInfoUpdates = transition.GuildIdsToRefresh
            .ToHashSet()
            .Count == 0
                ? Array.Empty<LegacyGuildWarInfoUpdate>()
                : participants.Values
                    .Where(IsGuildWarActivePlayer)
                    .Where(participant => transition.GuildIdsToRefresh.Contains(participant.GuildId))
                    .OrderBy(static participant => participant.ConnectionId)
                    .Select(participant => new LegacyGuildWarInfoUpdate(
                        participant.ConnectionId,
                        GetReciprocalGuildWarPartnerLocked(participant.GuildId),
                        clan,
                        GetAllyGuildIdLocked(participant.GuildId)))
                    .ToArray();

        return new LegacyGuildWarUpdate(
            transition,
            Array.AsReadOnly(noticeDeliveries),
            Array.AsReadOnly(sendWarInfoUpdates));
    }

    private int[] GetGuildWarNoticeRecipientsLocked(LegacyGuildWarNotice notice)
    {
        IEnumerable<Participant> recipients = participants.Values.Where(IsGuildWarActivePlayer);
        if (!notice.IsGlobal)
        {
            var guildIds = notice.RecipientGuildIds.ToHashSet();
            recipients = recipients.Where(participant => guildIds.Contains(participant.GuildId));
        }

        return recipients.Select(static participant => participant.ConnectionId).Distinct().Order().ToArray();
    }

    private static bool IsGuildWarActivePlayer(Participant participant) =>
        participant.Mob.Length >= LegacyAccountSnapshot.CharacterStride;

    private int GetReciprocalGuildWarPartnerLocked(int guildId)
    {
        var partners = combatWorldState.GuildWarPartners;
        if (partners is null || !partners.TryGetValue(guildId, out var partner) ||
            !LegacyGuildWarControl.IsGuildId(partner) ||
            !partners.TryGetValue(partner, out var reciprocal) || reciprocal != guildId)
            return 0;

        return partner;
    }

    private int GetAllyGuildIdLocked(int guildId)
    {
        var allies = combatWorldState.GuildAllies;
        return allies is not null && allies.TryGetValue(guildId, out var ally) && LegacyGuildAllyControl.IsGuildId(ally)
            ? ally
            : 0;
    }
}

public sealed record LegacyGuildWarNoticeDelivery(
    LegacyGuildWarNotice Notice,
    IReadOnlyList<int> RecipientConnectionIds);

public sealed record LegacyGuildWarInfoUpdate(
    int ConnectionId,
    int OpponentGuildId,
    int Clan,
    int AllyGuildId);

public sealed record LegacyGuildWarUpdate(
    LegacyGuildWarTransitionResult Transition,
    IReadOnlyList<LegacyGuildWarNoticeDelivery> NoticeDeliveries,
    IReadOnlyList<LegacyGuildWarInfoUpdate> SendWarInfoUpdates);

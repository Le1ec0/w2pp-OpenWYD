using System.Collections.ObjectModel;

namespace WydCdk.World;

public sealed partial class WorldHub
{
    /// <summary>Applies a client alliance toggle only for its active guild leader.</summary>
    public bool TryApplyGuildAllyRequest(
        int connectionId,
        int requestedGuildId,
        int requestedAllyGuildId,
        out LegacyGuildAllyUpdate? update)
    {
        lock (gate)
        {
            update = null;
            if (!participants.TryGetValue(connectionId, out var participant) ||
                participant.Mob.Length < LegacyAccountSnapshot.CharacterStride ||
                participant.GuildLevel != 9 ||
                participant.GuildId != requestedGuildId ||
                !LegacyGuildAllyControl.IsGuildId(requestedGuildId) ||
                !LegacyGuildAllyControl.IsGuildId(requestedAllyGuildId))
                return false;

            var transition = LegacyGuildAllyControl.Toggle(
                combatWorldState.GuildAllies ?? EmptyGuildAllyMap,
                requestedGuildId,
                requestedAllyGuildId);
            update = ApplyGuildAllyTransitionLocked(transition);
            return true;
        }
    }

    private static readonly IReadOnlyDictionary<int, int> EmptyGuildAllyMap =
        new ReadOnlyDictionary<int, int>(new Dictionary<int, int>());

    private LegacyGuildAllyUpdate ApplyGuildAllyTransitionLocked(LegacyGuildAllyTransitionResult transition)
    {
        if (transition.Status is LegacyGuildAllyTransitionStatus.AllyDeclared or
            LegacyGuildAllyTransitionStatus.AllyCancelled)
        {
            combatWorldState = combatWorldState with
            {
                GuildAllies = new ReadOnlyDictionary<int, int>(new Dictionary<int, int>(transition.GuildAllies)),
            };
        }

        var noticeDeliveries = transition.Notice is { } notice
            ? new[]
            {
                new LegacyGuildAllyNoticeDelivery(
                    notice,
                    Array.AsReadOnly(participants.Values
                        .Where(IsGuildWarActivePlayer)
                        .Select(static participant => participant.ConnectionId)
                        .Order()
                        .ToArray())),
            }
            : Array.Empty<LegacyGuildAllyNoticeDelivery>();
        var clan = guildZones is not null && guildZones.Clans.Count > 4 ? guildZones.Clans[4] : 0;
        var refreshGuildIds = transition.RefreshGuildIds.ToHashSet();
        var sendWarInfoUpdates = participants.Values
            .Where(IsGuildWarActivePlayer)
            .Where(participant => refreshGuildIds.Contains(participant.GuildId))
            .OrderBy(static participant => participant.ConnectionId)
            .Select(participant => new LegacyGuildWarInfoUpdate(
                participant.ConnectionId,
                GetReciprocalGuildWarPartnerLocked(participant.GuildId),
                clan,
                GetAllyGuildIdLocked(participant.GuildId)))
            .ToArray();

        return new LegacyGuildAllyUpdate(
            transition,
            Array.AsReadOnly(noticeDeliveries),
            Array.AsReadOnly(sendWarInfoUpdates));
    }
}

public sealed record LegacyGuildAllyNoticeDelivery(
    LegacyGuildAllyNotice Notice,
    IReadOnlyList<int> RecipientConnectionIds);

public sealed record LegacyGuildAllyUpdate(
    LegacyGuildAllyTransitionResult Transition,
    IReadOnlyList<LegacyGuildAllyNoticeDelivery> NoticeDeliveries,
    IReadOnlyList<LegacyGuildWarInfoUpdate> SendWarInfoUpdates);

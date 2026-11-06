using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class GuildWarWorldTests
{
    public static void Run()
    {
        ActiveGuildLeaderCanDeclareAndOnlyReciprocalStateBypassesPk();
        RequestAuthorizationUsesTheActiveMobGuildAndLeaderLevel();
        AcceptanceAndCancellationRefreshActiveGuildMembersAndSkillGates();
    }

    private static void ActiveGuildLeaderCanDeclareAndOnlyReciprocalStateBypassesPk()
    {
        var world = CreateWorld();

        Check(world.TryApplyGuildWarRequest(1, 7, 8, out var declaration) && declaration is not null,
            "An active guild leader could not declare GuildWar.");
        Check(declaration!.Transition.Status == LegacyGuildWarTransitionStatus.DeclarationCreated,
            "A first GuildWar request did not remain a one-sided declaration.");
        AssertMap(world.GetCombatWorldState().GuildWarPartners, 7, 8);
        Check(declaration.NoticeDeliveries.Count == 1 &&
            declaration.NoticeDeliveries[0].RecipientConnectionIds.Order().SequenceEqual(new[] { 1, 2, 3 }),
            "The pending declaration did not notify only active members of both guilds.");
        Check(declaration.SendWarInfoUpdates.Count == 0,
            "The one-sided declaration incorrectly refreshed SendWarInfo.");

        var playerGate = ResolvePlayerGate(world);
        var summonGate = ResolveSummonGate(world);
        Check(playerGate.PkPointGateBlocked && summonGate.PkPointGateBlocked,
            "A one-sided declaration incorrectly bypassed player/summon skill PK protection.");

        Check(world.TryApplyGuildWarRequest(2, 8, 7, out var acceptance) && acceptance is not null,
            "The target guild leader could not accept the reciprocal declaration.");
        Check(acceptance!.Transition.Status == LegacyGuildWarTransitionStatus.WarAccepted,
            "A reciprocal GuildWar request did not transition to an accepted war.");
        AssertMap(world.GetCombatWorldState().GuildWarPartners, 7, 8, 8, 7);
        Check(!ResolvePlayerGate(world).PkPointGateBlocked && !ResolveSummonGate(world).PkPointGateBlocked,
            "Reciprocal GuildWar state did not bypass the skill PK gate.");
        Check(acceptance.NoticeDeliveries.Count == 1 && acceptance.NoticeDeliveries[0].Notice.IsGlobal &&
            acceptance.NoticeDeliveries[0].RecipientConnectionIds.Order().SequenceEqual(new[] { 1, 2, 3 }),
            "War acceptance did not issue the global started notice to active players.");
    }

    private static void RequestAuthorizationUsesTheActiveMobGuildAndLeaderLevel()
    {
        var world = CreateWorld();

        Check(!world.TryApplyGuildWarRequest(3, 7, 8, out var nonLeader) && nonLeader is null,
            "A non-leader was allowed to declare GuildWar.");
        Check(!world.TryApplyGuildWarRequest(1, 8, 9, out var mismatchedGuild) && mismatchedGuild is null,
            "The client spoofed Parm1 to mutate a guild other than its active MOB.Guild.");
        Check(!world.TryApplyGuildWarRequest(1, 7, 0, out var invalidEnemy) && invalidEnemy is null,
            "A client request with Enemy=0 was accepted as a cancellation.");
        Check(!world.TryApplyGuildWarRequest(4, 9, 7, out var inactive) && inactive is null,
            "A participant without an active character MOB was allowed to declare GuildWar.");
        Check(world.GetCombatWorldState().GuildWarPartners is null,
            "Rejected GuildWar requests mutated the authoritative war map.");
    }

    private static void AcceptanceAndCancellationRefreshActiveGuildMembersAndSkillGates()
    {
        var world = CreateWorld(clan: 14);
        Check(world.TryApplyGuildWarRequest(1, 7, 8, out _),
            "GuildWar declaration fixture could not be created.");
        Check(world.TryApplyGuildWarRequest(2, 8, 7, out var accepted) && accepted is not null,
            "GuildWar reciprocal fixture could not be accepted.");

        Check(accepted!.SendWarInfoUpdates.Count == 3 &&
            accepted.SendWarInfoUpdates.Select(static update => update.ConnectionId).Order().SequenceEqual(new[] { 1, 2, 3 }) &&
            accepted.SendWarInfoUpdates.All(static update => update.Clan == 14 && update.AllyGuildId == 0) &&
            accepted.SendWarInfoUpdates.Single(static update => update.ConnectionId == 1).OpponentGuildId == 8 &&
            accepted.SendWarInfoUpdates.Single(static update => update.ConnectionId == 2).OpponentGuildId == 7 &&
            accepted.SendWarInfoUpdates.Single(static update => update.ConnectionId == 3).OpponentGuildId == 8,
            "Acceptance did not refresh active guild members with reciprocal enemy and GuildZone[4].Clan.");

        Check(world.TryCancelGuildWar(7, out var cancelled) && cancelled is not null,
            "The trusted internal GuildWar cancellation did not apply.");
        AssertMap(world.GetCombatWorldState().GuildWarPartners);
        Check(cancelled!.Transition.Status == LegacyGuildWarTransitionStatus.WarCancelled &&
            cancelled.SendWarInfoUpdates.Count == 3 && cancelled.SendWarInfoUpdates.All(static update => update.OpponentGuildId == 0),
            "Cancellation did not immediately clear reciprocal SendWarInfo state for every active member.");
        Check(ResolvePlayerGate(world).PkPointGateBlocked && ResolveSummonGate(world).PkPointGateBlocked,
            "Cancellation did not immediately restore player/summon skill PK protection.");
    }

    private static LegacySkillPkGateResult ResolvePlayerGate(WorldHub world) =>
        LegacySkillPkGate.Resolve(0, 11, 7, 8, 2_000, 2_000, 1, 50, world.GetCombatWorldState());

    private static LegacySkillPkGateResult ResolveSummonGate(WorldHub world) =>
        LegacySkillPkGate.ResolveSummon(0, 11, 7, 8, 2_000, 2_000, 1, 50, world.GetCombatWorldState());

    private static WorldHub CreateWorld(int clan = 0)
    {
        var guildZones = new LegacyGuildZoneState(0, new int[5], new int[5], new int[5], [0, 0, 0, 0, clan], new int[5]);
        var world = new WorldHub(guildZones: guildZones);
        foreach (var (connectionId, guildId, guildLevel) in new[] { (1, 7, 9), (2, 8, 9), (3, 7, 8) })
        {
            Check(world.Enter(connectionId, $"GUILD_WAR_{connectionId}", (_, _) => ValueTask.CompletedTask),
                "GuildWar player fixture could not enter the world.");
            var mob = CreateMob(guildId);
            Check(world.SetCharacterState(connectionId, 0, guildId, 0, guildLevel, 0, 2_000, 2_000, mob),
                "GuildWar active character fixture could not be installed.");
        }

        Check(world.Enter(4, "GUILD_WAR_INACTIVE", (_, _) => ValueTask.CompletedTask),
            "GuildWar inactive fixture could not enter the world.");
        return world;
    }

    private static byte[] CreateMob(int guildId)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset), checked((ushort)guildId));
        new LegacyScore(50, 0, 0, 0, 0, 0, 0, 5000, 5000, 1000, 5000, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static void AssertMap(IReadOnlyDictionary<int, int>? actual, params int[] expectedPairs)
    {
        Check(expectedPairs.Length % 2 == 0 && (actual?.Count ?? 0) == expectedPairs.Length / 2 &&
            Enumerable.Range(0, expectedPairs.Length / 2).All(index =>
                actual!.TryGetValue(expectedPairs[index * 2], out var enemy) && enemy == expectedPairs[(index * 2) + 1]),
            $"Guild-war map differed. Expected [{string.Join(",", Enumerable.Range(0, expectedPairs.Length / 2).Select(index => $"{expectedPairs[index * 2]}:{expectedPairs[(index * 2) + 1]}"))}], actual [{string.Join(",", actual?.Select(pair => $"{pair.Key}:{pair.Value}") ?? [])}].");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

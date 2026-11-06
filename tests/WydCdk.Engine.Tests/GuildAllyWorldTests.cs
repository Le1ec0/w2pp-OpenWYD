using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class GuildAllyWorldTests
{
    public static void Run()
    {
        ActiveGuildLeaderTogglesDirectedAllianceAndRefreshesMembers();
        AuthorizationRequiresActiveGuildLeaderAndValidWireIds();
        CombatStateSnapshotsAllianceMapDefensively();
    }

    private static void ActiveGuildLeaderTogglesDirectedAllianceAndRefreshesMembers()
    {
        var world = CreateWorld(clan: 14);
        world.SetCombatWorldState(new LegacyCombatWorldState(
            GuildWarPartners: new Dictionary<int, int> { [7] = 8, [8] = 7 }));

        Check(world.TryApplyGuildAllyRequest(1, 7, 8, out var declared) && declared is not null,
            "An active guild leader could not declare an alliance.");
        AssertMap(world.GetCombatWorldState().GuildAllies, 7, 8);
        Check(declared!.Transition.Status == LegacyGuildAllyTransitionStatus.AllyDeclared &&
              declared.NoticeDeliveries.Count == 1 && declared.NoticeDeliveries[0].RecipientConnectionIds.Order().SequenceEqual([1, 2, 3]),
            "Alliance declaration did not send its global notice to active characters.");
        Check(declared.SendWarInfoUpdates.Count == 2 &&
              declared.SendWarInfoUpdates.Select(static update => update.ConnectionId).Order().SequenceEqual([1, 3]) &&
              declared.SendWarInfoUpdates.All(static update => update.OpponentGuildId == 8 && update.Clan == 14 && update.AllyGuildId == 8),
            "Alliance declaration did not refresh each active requester-guild member with current war and ally data.");
        Check(world.GetCombatWorldState().GuildWarPartners is { Count: 2 } wars && wars[7] == 8 && wars[8] == 7,
            "Alliance changes must preserve the separate reciprocal GuildWar map.");

        Check(world.TryApplyGuildAllyRequest(1, 7, 9, out var cancelled) && cancelled is not null,
            "The active leader could not cancel an existing alliance.");
        AssertMap(world.GetCombatWorldState().GuildAllies);
        Check(cancelled!.Transition.Status == LegacyGuildAllyTransitionStatus.AllyCancelled &&
              cancelled.NoticeDeliveries.Single().Notice.AllyGuildId == 8 &&
              cancelled.SendWarInfoUpdates.Count == 2 && cancelled.SendWarInfoUpdates.All(static update => update.AllyGuildId == 0),
            "Alliance cancellation must clear only the requester pointer and refresh its active members.");
    }

    private static void AuthorizationRequiresActiveGuildLeaderAndValidWireIds()
    {
        var world = CreateWorld();

        Check(!world.TryApplyGuildAllyRequest(3, 7, 8, out var member) && member is null,
            "A non-leader was allowed to change its guild alliance.");
        Check(!world.TryApplyGuildAllyRequest(1, 8, 9, out var mismatchedGuild) && mismatchedGuild is null,
            "The client spoofed its active guild while changing alliance state.");
        Check(!world.TryApplyGuildAllyRequest(1, 7, 0, out var invalidAlly) && invalidAlly is null,
            "An invalid ally ID was accepted.");
        Check(!world.TryApplyGuildAllyRequest(4, 9, 7, out var inactive) && inactive is null,
            "A participant without an active character MOB changed alliance state.");
        Check(world.GetCombatWorldState().GuildAllies is null,
            "Rejected alliance requests mutated the authoritative map.");
    }

    private static void CombatStateSnapshotsAllianceMapDefensively()
    {
        var world = CreateWorld();
        var source = new Dictionary<int, int> { [7] = 8 };
        world.SetCombatWorldState(new LegacyCombatWorldState(GuildAllies: source));
        source[7] = 9;

        Check(world.GetCombatWorldState().GuildAllies is { } snapshot && snapshot[7] == 8,
            "SetCombatWorldState retained a mutable caller-owned alliance map.");
        var exposed = (IDictionary<int, int>)world.GetCombatWorldState().GuildAllies!;
        var mutationRejected = false;
        try
        {
            exposed[7] = 9;
        }
        catch (NotSupportedException)
        {
            mutationRejected = true;
        }
        Check(mutationRejected && world.GetCombatWorldState().GuildAllies![7] == 8,
            "GetCombatWorldState exposed a writable alliance map.");
    }

    private static WorldHub CreateWorld(int clan = 0)
    {
        var guildZones = new LegacyGuildZoneState(0, new int[5], new int[5], new int[5], [0, 0, 0, 0, clan], new int[5]);
        var world = new WorldHub(guildZones: guildZones);
        foreach (var (connectionId, guildId, guildLevel) in new[] { (1, 7, 9), (2, 8, 9), (3, 7, 8) })
        {
            Check(world.Enter(connectionId, $"GUILD_ALLY_{connectionId}", (_, _) => ValueTask.CompletedTask),
                "GuildAlly player fixture could not enter the world.");
            var mob = new byte[LegacyAccountSnapshot.CharacterStride];
            BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset), checked((ushort)guildId));
            new LegacyScore(50, 0, 0, 0, 0, 0, 0, 5000, 5000, 1000, 5000, 0, 0, 0, 0, 0, 0, 0, 0)
                .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
            Check(world.SetCharacterState(connectionId, 0, guildId, 0, guildLevel, 0, 2_000, 2_000, mob),
                "GuildAlly active character fixture could not be installed.");
        }

        Check(world.Enter(4, "GUILD_ALLY_INACTIVE", (_, _) => ValueTask.CompletedTask),
            "GuildAlly inactive fixture could not enter the world.");
        return world;
    }

    private static void AssertMap(IReadOnlyDictionary<int, int>? actual, params int[] expectedPairs)
    {
        Check(expectedPairs.Length % 2 == 0 && (actual?.Count ?? 0) == expectedPairs.Length / 2 &&
            Enumerable.Range(0, expectedPairs.Length / 2).All(index =>
                actual!.TryGetValue(expectedPairs[index * 2], out var ally) && ally == expectedPairs[(index * 2) + 1]),
            $"Guild-alliance map differed. Expected [{string.Join(",", Enumerable.Range(0, expectedPairs.Length / 2).Select(index => $"{expectedPairs[index * 2]}:{expectedPairs[(index * 2) + 1]}"))}], actual [{string.Join(",", actual?.Select(pair => $"{pair.Key}:{pair.Value}") ?? [])}].");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

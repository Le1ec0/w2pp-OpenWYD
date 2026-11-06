using WydCdk.World;

internal static class GuildAllyControlTests
{
    public static void Run()
    {
        FirstRequestSetsOnlyTheRequestingGuildPointer();
        ExistingAllyIsCancelledRegardlessOfRequestedTarget();
        InvalidRequestsDoNotMutateState();
    }

    private static void FirstRequestSetsOnlyTheRequestingGuildPointer()
    {
        var initial = new Dictionary<int, int> { [8] = 9 };
        var result = LegacyGuildAllyControl.Toggle(initial, 7, 8);

        Check(result.Status == LegacyGuildAllyTransitionStatus.AllyDeclared,
            "A guild with no ally must declare the requested one-sided alliance.");
        AssertMap(result.GuildAllies, 7, 8, 8, 9);
        Check(initial.Count == 1 && initial[8] == 9,
            "Declaring an alliance mutated the caller's map.");
        Check(result.Notice is { Kind: LegacyGuildAllyNoticeKind.Declared, GuildId: 7, AllyGuildId: 8 } &&
              result.RefreshGuildIds.SequenceEqual([7]),
            "A new directed alliance must announce and refresh only the requesting guild.");
    }

    private static void ExistingAllyIsCancelledRegardlessOfRequestedTarget()
    {
        var result = LegacyGuildAllyControl.Toggle(new Dictionary<int, int> { [7] = 8, [8] = 7 }, 7, 9);

        Check(result.Status == LegacyGuildAllyTransitionStatus.AllyCancelled,
            "A second request by an already-allied guild must cancel its current alliance.");
        AssertMap(result.GuildAllies, 8, 7);
        Check(result.Notice is { Kind: LegacyGuildAllyNoticeKind.Cancelled, GuildId: 7, AllyGuildId: 8 } &&
              result.RefreshGuildIds.SequenceEqual([7]),
            "Cancelling an alliance must retain the former target for notice text and refresh only the requester.");
    }

    private static void InvalidRequestsDoNotMutateState()
    {
        var initial = new Dictionary<int, int> { [7] = 8 };
        var invalidGuild = LegacyGuildAllyControl.Toggle(initial, 0, 9);
        var invalidAlly = LegacyGuildAllyControl.Toggle(initial, 9, 65536);

        Check(invalidGuild.Status == LegacyGuildAllyTransitionStatus.InvalidRequest &&
              invalidAlly.Status == LegacyGuildAllyTransitionStatus.InvalidRequest,
            "Guild or ally IDs outside 1..65535 must be rejected.");
        AssertMap(invalidGuild.GuildAllies, 7, 8);
        AssertMap(invalidAlly.GuildAllies, 7, 8);
        Check(invalidGuild.Notice is null && invalidAlly.Notice is null &&
              invalidGuild.RefreshGuildIds.Count == 0 && invalidAlly.RefreshGuildIds.Count == 0,
            "Invalid alliance requests must not emit notices or refreshes.");
    }

    private static void AssertMap(IReadOnlyDictionary<int, int> actual, params int[] expectedPairs)
    {
        Check(expectedPairs.Length % 2 == 0 && actual.Count == expectedPairs.Length / 2 &&
            Enumerable.Range(0, expectedPairs.Length / 2).All(index =>
                actual.TryGetValue(expectedPairs[index * 2], out var ally) && ally == expectedPairs[(index * 2) + 1]),
            $"Guild-alliance map differed. Expected [{string.Join(",", Enumerable.Range(0, expectedPairs.Length / 2).Select(index => $"{expectedPairs[index * 2]}:{expectedPairs[(index * 2) + 1]}"))}], actual [{string.Join(",", actual.Select(pair => $"{pair.Key}:{pair.Value}"))}].");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

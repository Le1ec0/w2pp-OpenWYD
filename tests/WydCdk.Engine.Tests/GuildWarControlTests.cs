using WydCdk.World;

internal static class GuildWarControlTests
{
    public static void Run()
    {
        DeclarationCreatesOnlyTheRequestingGuildPointer();
        ReciprocalRequestAcceptsAndEmitsStartedEffects();
        RequesterAlreadyMappedIsRejected();
        TargetMappedElsewhereStillReceivesOneSidedDeclaration();
        PendingAndReciprocalCancellationsMatchTheSource();
        InvalidRequestsDoNotMutateState();
    }

    private static void DeclarationCreatesOnlyTheRequestingGuildPointer()
    {
        var initial = new Dictionary<int, int>();
        var result = LegacyGuildWarControl.Declare(initial, 7, 8);

        Check(result.Status == LegacyGuildWarTransitionStatus.DeclarationCreated,
            "A valid first request must create a one-sided declaration.");
        AssertMap(result.GuildWarPartners, 7, 8);
        Check(initial.Count == 0, "The pure declaration mutated its input map.");
        Check(result.Notices.Count == 1 &&
            result.Notices[0].Kind == LegacyGuildWarNoticeKind.Declaration &&
            !result.Notices[0].IsGlobal &&
            result.Notices[0].RecipientGuildIds.SequenceEqual(new[] { 7, 8 }),
            "A pending declaration must notify both guilds without a global-start notice.");
        Check(result.GuildIdsToRefresh.Count == 0,
            "A one-sided declaration must not emit reciprocal SendWarInfo refreshes.");
    }

    private static void ReciprocalRequestAcceptsAndEmitsStartedEffects()
    {
        var result = LegacyGuildWarControl.Declare(new Dictionary<int, int> { [8] = 7 }, 7, 8);

        Check(result.Status == LegacyGuildWarTransitionStatus.WarAccepted,
            "A request matching the target's pointer must accept the war.");
        AssertMap(result.GuildWarPartners, 7, 8, 8, 7);
        Check(result.Notices.Count == 1 &&
            result.Notices[0].Kind == LegacyGuildWarNoticeKind.WarStarted &&
            result.Notices[0].IsGlobal &&
            result.Notices[0].RecipientGuildIds.Count == 0,
            "Mutual acceptance must emit only the global war-start notice.");
        Check(result.GuildIdsToRefresh.Order().SequenceEqual(new[] { 7, 8 }),
            "Mutual acceptance must refresh war info for both guilds.");
    }

    private static void RequesterAlreadyMappedIsRejected()
    {
        var initial = new Dictionary<int, int> { [7] = 8 };
        var result = LegacyGuildWarControl.Declare(initial, 7, 9);

        Check(result.Status == LegacyGuildWarTransitionStatus.RequesterAlreadyMapped,
            "The reference rejects a requester that already has a war pointer.");
        AssertMap(result.GuildWarPartners, 7, 8);
        Check(result.Notices.Count == 0 && result.GuildIdsToRefresh.Count == 0,
            "A rejected requester must not emit notices or refreshes.");
        Check(initial.Count == 1 && initial[7] == 8,
            "Rejecting an already-mapped requester mutated the input map.");
    }

    private static void TargetMappedElsewhereStillReceivesOneSidedDeclaration()
    {
        var result = LegacyGuildWarControl.Declare(new Dictionary<int, int> { [8] = 9 }, 7, 8);

        Check(result.Status == LegacyGuildWarTransitionStatus.DeclarationCreated,
            "The reference permits a declaration when only the target points to a third guild.");
        AssertMap(result.GuildWarPartners, 7, 8, 8, 9);
        Check(result.Notices.Count == 1 && result.Notices[0].Kind == LegacyGuildWarNoticeKind.Declaration,
            "A crossed pointer must remain a pending declaration, not start a war.");
        Check(result.GuildIdsToRefresh.Count == 0,
            "A crossed pointer must not trigger reciprocal war-info refreshes.");
    }

    private static void PendingAndReciprocalCancellationsMatchTheSource()
    {
        var pending = LegacyGuildWarControl.Cancel(new Dictionary<int, int> { [7] = 8 }, 7);
        Check(pending.Status == LegacyGuildWarTransitionStatus.PendingDeclarationCancelled,
            "Cancel must remove a pending declaration.");
        AssertMap(pending.GuildWarPartners);
        Check(pending.Notices.Count == 1 &&
            pending.Notices[0].Kind == LegacyGuildWarNoticeKind.DeclarationCancelled &&
            pending.Notices[0].RecipientGuildIds.SequenceEqual(new[] { 7, 8 }),
            "Cancelling a pending declaration must notify both guilds.");
        Check(pending.GuildIdsToRefresh.Count == 0,
            "Cancelling a one-sided declaration must not refresh war info.");

        var mutual = LegacyGuildWarControl.Cancel(new Dictionary<int, int> { [7] = 8, [8] = 7 }, 7);
        Check(mutual.Status == LegacyGuildWarTransitionStatus.WarCancelled,
            "Cancel must remove both pointers for a reciprocal war.");
        AssertMap(mutual.GuildWarPartners);
        Check(mutual.Notices.Count == 1 && mutual.Notices[0].Kind == LegacyGuildWarNoticeKind.WarCancelled,
            "Cancelling a reciprocal war must emit the source cancellation notice.");
        Check(mutual.GuildIdsToRefresh.Order().SequenceEqual(new[] { 7, 8 }),
            "Cancelling a reciprocal war must refresh both guilds.");

        var noWar = LegacyGuildWarControl.Cancel(new Dictionary<int, int>(), 7);
        Check(noWar.Status == LegacyGuildWarTransitionStatus.NoWar && noWar.Notices.Count == 0,
            "The source does nothing when asked to cancel a guild with no war pointer.");
    }

    private static void InvalidRequestsDoNotMutateState()
    {
        var initial = new Dictionary<int, int> { [7] = 8 };
        var invalidRequester = LegacyGuildWarControl.Declare(initial, 0, 9);
        var invalidEnemy = LegacyGuildWarControl.Declare(initial, 9, 65536);

        Check(invalidRequester.Status == LegacyGuildWarTransitionStatus.InvalidRequest &&
            invalidEnemy.Status == LegacyGuildWarTransitionStatus.InvalidRequest,
            "Guild IDs outside 1..65535 must be rejected.");
        AssertMap(invalidRequester.GuildWarPartners, 7, 8);
        AssertMap(invalidEnemy.GuildWarPartners, 7, 8);
        Check(invalidRequester.Notices.Count == 0 && invalidEnemy.Notices.Count == 0,
            "Invalid guild IDs must not produce notices.");
    }

    private static void AssertMap(IReadOnlyDictionary<int, int> actual, params int[] expectedPairs)
    {
        Check(expectedPairs.Length % 2 == 0 && actual.Count == expectedPairs.Length / 2 &&
            Enumerable.Range(0, expectedPairs.Length / 2).All(index =>
                actual.TryGetValue(expectedPairs[index * 2], out var enemy) && enemy == expectedPairs[(index * 2) + 1]),
            $"Guild-war map differed. Expected [{string.Join(",", Enumerable.Range(0, expectedPairs.Length / 2).Select(index => $"{expectedPairs[index * 2]}:{expectedPairs[(index * 2) + 1]}"))}], actual [{string.Join(",", actual.Select(pair => $"{pair.Key}:{pair.Value}"))}].");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

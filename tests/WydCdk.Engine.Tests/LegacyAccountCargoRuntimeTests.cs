using WydCdk.Protocol;
using WydCdk.World;

internal static class LegacyAccountCargoRuntimeTests
{
    public static void SharedAccountOwnerUsesRevisionedCompareAndSwap()
    {
        var runtime = new LegacyAccountCargoRuntime("UP");
        var initial = new LegacyItem[LegacyAccountSnapshot.CargoCount];
        initial[3] = new LegacyItem(100, 2, 3, 4, 5, 6, 7);
        runtime.RegisterSession("ALICE", 11);
        runtime.RegisterSession("alice", 12);

        var first = runtime.LoadAuthoritative("ALICE", initial);
        var second = runtime.LoadAuthoritative("alice", initial);
        Assert(first.Revision == 0 && second.Revision == 0 && !first.WasReconciled && !second.WasReconciled,
            "Case variants did not resolve to one initial account Cargo owner.");

        var updated = initial.ToArray();
        updated[3] = default;
        Assert(runtime.TryCommit("ALICE", first.Revision, initial, updated, out var committed),
            "Valid account Cargo revision did not commit.");
        Assert(committed is not null && committed.Revision == 1 && committed.Items[3] == default,
            "Cargo commit did not publish the next revision and item state.");
        Assert(runtime.TryGetSessions("ALICE", out var sessions) && sessions.SequenceEqual([11, 12]),
            "Duplicate active sessions were not retained on one account Cargo owner.");

        Assert(!runtime.TryCommit("ALICE", first.Revision, initial, initial, out _),
            "Stale owner revision was allowed to overwrite committed Cargo.");
        runtime.UnregisterSession("ALICE", 11);
        Assert(runtime.TryGet("ALICE", out _), "Removing one session deleted a still-shared account Cargo owner.");
        runtime.UnregisterSession("ALICE", 12);
        Assert(!runtime.TryGet("ALICE", out _), "Last account session did not release its runtime Cargo owner.");
    }

    public static void AuthoritativeReloadReconcilesAndAdvancesRevision()
    {
        var runtime = new LegacyAccountCargoRuntime("PVP");
        var initial = new LegacyItem[LegacyAccountSnapshot.CargoCount];
        runtime.RegisterSession("BOB", 23);
        var first = runtime.LoadAuthoritative("BOB", initial);
        var external = initial.ToArray();
        external[9] = new LegacyItem(222, 1, 0, 0, 0, 0, 0);
        var reconciled = runtime.LoadAuthoritative("BOB", external);

        Assert(reconciled.WasReconciled && reconciled.Revision == first.Revision + 1 && reconciled.Items[9] == external[9],
            "Authoritative reload did not replace stale runtime Cargo and advance its revision.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

using System.Collections.Concurrent;
using System.Text;
using WydCdk.Protocol;
using WydCdk.Server;
using WydCdk.World;

internal static class LegacyCharacterCheckpointRuntimeTests
{
    private const int CharacterSlot = 0;

    public static void Run()
    {
        SkipsEmptyAndNonPlayingSlotsAndSavesAtMostOnePlayerPerPass().GetAwaiter().GetResult();
        WrapsFromSlot999ToSlot1().GetAwaiter().GetResult();
        ClosedAndStaleParticipantsAreSkipped().GetAwaiter().GetResult();
        DepartureWaitsForAnInFlightCheckpoint().GetAwaiter().GetResult();
        CheckpointTakesAccountLeaseBeforeSnapshotAndStore().GetAwaiter().GetResult();
        FailedSaveDoesNotStopTheNextPass().GetAwaiter().GetResult();
        CheckpointPassesNeverOverlap().GetAwaiter().GetResult();
        DepartureUsesPositionFallbackWithoutWorldSnapshot().GetAwaiter().GetResult();
    }

    private static async Task SkipsEmptyAndNonPlayingSlotsAndSavesAtMostOnePlayerPerPass()
    {
        var fixture = new RuntimeFixture();
        fixture.AddPlayingCharacter(3, "THREE");
        fixture.AddPlayingCharacter(5, "FIVE");
        fixture.AddCharacterSelection(2, "SELECT");

        var first = await fixture.Runtime.RunSinglePassAsync();
        var second = await fixture.Runtime.RunSinglePassAsync();

        Require(first == new LegacyCharacterCheckpointPassResult(4, 3, LegacyCharacterCheckpointSaveResult.Success),
            $"Expected the first pass to skip empty/non-playing IDs and save slot 3, got {first}.");
        Require(second == new LegacyCharacterCheckpointPassResult(6, 5, LegacyCharacterCheckpointSaveResult.Success),
            $"Expected the second pass to resume at slot 4 and save slot 5, got {second}.");
        Require(fixture.Store.Requests.Count == 2 && fixture.Store.Requests.All(static request => request.CharacterSlot == CharacterSlot),
            "A checkpoint pass saved more than one eligible character.");
        Require(LegacyCharacterCheckpointPolicy.Interval == TimeSpan.FromSeconds(4), "Checkpoint interval changed from the 7.69 cadence.");
    }

    private static async Task WrapsFromSlot999ToSlot1()
    {
        var fixture = new RuntimeFixture();
        fixture.AddPlayingCharacter(999, "LAST");

        var last = await fixture.Runtime.RunSinglePassAsync();
        Require(last == new LegacyCharacterCheckpointPassResult(1, 999, LegacyCharacterCheckpointSaveResult.Success),
            $"Expected the ring to wrap from 999 to 1, got {last}.");

        fixture.AddPlayingCharacter(1, "FIRST");
        var first = await fixture.Runtime.RunSinglePassAsync();
        Require(first == new LegacyCharacterCheckpointPassResult(2, 1, LegacyCharacterCheckpointSaveResult.Success),
            $"Expected the pass after 999 to start at slot 1, got {first}.");
    }

    private static async Task ClosedAndStaleParticipantsAreSkipped()
    {
        var fixture = new RuntimeFixture();
        fixture.AddPlayingCharacter(1, "CLOSED");
        fixture.Sessions.Close(1);
        fixture.AddCharacterSelection(2, "SELECTION");

        var result = await fixture.Runtime.RunSinglePassAsync();

        Require(result == new LegacyCharacterCheckpointPassResult(1, null, null),
            $"A closed or non-playing session was selected for persistence: {result}.");
        Require(fixture.Store.IsEmpty, "A closed or non-playing session reached the checkpoint store.");

        var stale = new RuntimeFixture();
        stale.AddPlayingSessionOnly(1, "STALE");
        var staleResult = await stale.Runtime.RunSinglePassAsync();
        Require(staleResult == new LegacyCharacterCheckpointPassResult(1, null, null),
            "A Playing session without a current WorldHub participant was persisted.");
    }

    private static async Task DepartureWaitsForAnInFlightCheckpoint()
    {
        var fixture = new RuntimeFixture();
        var session = fixture.AddPlayingCharacter(1, "DEPART");
        fixture.Store.BlockNextWrite();

        var checkpoint = fixture.Runtime.RunSinglePassAsync().AsTask();
        await fixture.Store.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var departure = fixture.Runtime.SaveOnDepartureAsync(session, disconnect: true).AsTask();
        await Task.Delay(25);
        Require(!departure.IsCompleted, "Departure did not wait behind the active per-slot checkpoint lock.");

        fixture.Store.ReleaseBlockedWrite();
        await Task.WhenAll(checkpoint, departure);
        Require(fixture.Store.Requests.Count == 2, "Departure did not perform its own composite checkpoint after the periodic write.");
    }

    private static async Task CheckpointTakesAccountLeaseBeforeSnapshotAndStore()
    {
        var gate = new LegacyAccountOperationGate();
        var fixture = new RuntimeFixture(gate);
        fixture.AddPlayingCharacter(1, "LEASED");
        await using var accountLease = await gate.AcquireAsync(["LEASED"]);

        var checkpoint = fixture.Runtime.RunSinglePassAsync().AsTask();
        await Task.Delay(25);
        Require(fixture.Store.IsEmpty, "Checkpoint captured or persisted a character before acquiring its account lease.");

        await accountLease.DisposeAsync();
        await checkpoint.WaitAsync(TimeSpan.FromSeconds(5));
        Require(fixture.Store.Requests.Count == 1, "Checkpoint did not continue after the account lease was released.");
    }

    private static async Task FailedSaveDoesNotStopTheNextPass()
    {
        var fixture = new RuntimeFixture();
        fixture.AddPlayingCharacter(1, "RETRY");
        fixture.Store.EnqueueResult(LegacyCharacterCheckpointSaveResult.NotAvailable);
        fixture.Store.EnqueueResult(LegacyCharacterCheckpointSaveResult.Success);

        var failed = await fixture.Runtime.RunSinglePassAsync();
        var retried = await fixture.Runtime.RunSinglePassAsync();

        Require(failed.SaveResult == LegacyCharacterCheckpointSaveResult.NotAvailable && failed.SavedSlot == 1,
            "The runtime did not report a failed save attempt for its selected player.");
        Require(retried.SaveResult == LegacyCharacterCheckpointSaveResult.Success && retried.SavedSlot == 1,
            "The runtime did not resume and retry the player on a later ring pass.");
    }

    private static async Task CheckpointPassesNeverOverlap()
    {
        var fixture = new RuntimeFixture();
        fixture.AddPlayingCharacter(1, "SERIAL");
        fixture.Store.BlockNextWrite();

        var first = fixture.Runtime.RunSinglePassAsync().AsTask();
        await fixture.Store.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = fixture.Runtime.RunSinglePassAsync().AsTask();
        await Task.Delay(25);
        Require(!second.IsCompleted, "A second scheduler pass overlapped an in-flight store write.");

        fixture.Store.ReleaseBlockedWrite();
        await Task.WhenAll(first, second);
        Require(fixture.Store.MaximumConcurrentWrites == 1, "More than one checkpoint write ran at the same time.");
    }

    private static async Task DepartureUsesPositionFallbackWithoutWorldSnapshot()
    {
        var fixture = new RuntimeFixture();
        var session = fixture.AddPlayingSessionOnly(1, "FALLBACK");

        await fixture.Runtime.SaveOnDepartureAsync(session, disconnect: false);

        Require(fixture.FallbackCalls == 1 && fixture.FallbackAccount == "FALLBACK" && fixture.FallbackSlot == CharacterSlot,
            "Departure did not preserve the position-only fallback when the WorldHub snapshot was unavailable.");
        Require(fixture.Store.IsEmpty, "Position fallback also invoked the composite store without a live snapshot.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class RuntimeFixture
    {
        public RuntimeFixture(LegacyAccountOperationGate? accountOperationGate = null)
        {
            AccountOperations = accountOperationGate ?? new LegacyAccountOperationGate();
            Runtime = new LegacyCharacterCheckpointRuntime(
                Sessions,
                World,
                Store,
                (account, slot, x, y, _) =>
                {
                    FallbackCalls++;
                    FallbackAccount = account;
                    FallbackSlot = slot;
                    FallbackPosition = (x, y);
                    return ValueTask.FromResult(CharacterPositionSaveResult.Success);
                },
                accountOperationGate: AccountOperations);
        }

        public LoginSessionRegistry Sessions { get; } = new();
        public WorldHub World { get; } = new();
        public CheckpointStore Store { get; } = new();
        public LegacyAccountOperationGate AccountOperations { get; }
        public LegacyCharacterCheckpointRuntime Runtime { get; }
        public int FallbackCalls { get; private set; }
        public string? FallbackAccount { get; private set; }
        public int FallbackSlot { get; private set; } = -1;
        public (short X, short Y) FallbackPosition { get; private set; }

        public LoginSession AddPlayingCharacter(int connectionId, string accountName)
        {
            var session = AddPlayingSessionOnly(connectionId, accountName);
            var mob = CreateMob(accountName);
            if (!World.Enter(connectionId, accountName, static (_, _) => ValueTask.CompletedTask) ||
                !World.SetCharacterState(connectionId, CharacterSlot, 0, 0, 0, 100, 2_000, 2_001, mob))
                throw new InvalidOperationException("Could not add a character snapshot to WorldHub.");
            return session;
        }

        public LoginSession AddPlayingSessionOnly(int connectionId, string accountName)
        {
            var login = new AccountLoginRequest(accountName, string.Empty, new byte[52], ClientReleasePolicy.RequiredClientVersion, 0, []);
            if (!Sessions.Open(connectionId) ||
                Sessions.BeginAccountLogin(connectionId, login) != LoginTransitionResult.Accepted ||
                Sessions.CompleteAccountLogin(connectionId, accountName) != LoginTransitionResult.Accepted ||
                Sessions.BeginCharacterWait(connectionId) != LoginTransitionResult.Accepted ||
                Sessions.CompleteCharacterLogin(connectionId, CharacterSlot, 2_000, 2_001) != LoginTransitionResult.Accepted ||
                !Sessions.TryGet(connectionId, out var session) || session is null)
                throw new InvalidOperationException("Could not transition the test session to USER_PLAY.");
            return session;
        }

        public void AddCharacterSelection(int connectionId, string accountName)
        {
            var login = new AccountLoginRequest(accountName, string.Empty, new byte[52], ClientReleasePolicy.RequiredClientVersion, 0, []);
            if (!Sessions.Open(connectionId) ||
                Sessions.BeginAccountLogin(connectionId, login) != LoginTransitionResult.Accepted ||
                Sessions.CompleteAccountLogin(connectionId, accountName) != LoginTransitionResult.Accepted)
                throw new InvalidOperationException("Could not transition the test session to character selection.");
        }

        private static byte[] CreateMob(string name)
        {
            var mob = new byte[LegacyAccountSnapshot.CharacterStride];
            Encoding.ASCII.GetBytes(name).CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset, 16));
            return mob;
        }
    }

    private sealed class CheckpointStore : ICharacterCheckpointStore
    {
        private readonly ConcurrentQueue<LegacyCharacterCheckpointSaveResult> results = new();
        private readonly ConcurrentQueue<LegacyCharacterCheckpointSaveRequest> requests = new();
        private readonly SemaphoreSlim writeGate = new(1, 1);
        private TaskCompletionSource entered = NewSource();
        private TaskCompletionSource? blockedWrite;
        private int activeWrites;
        private int maximumConcurrentWrites;

        public IReadOnlyList<LegacyCharacterCheckpointSaveRequest> Requests => requests.ToArray();
        public bool IsEmpty => requests.IsEmpty;
        public TaskCompletionSource WriteEntered => entered;
        public int MaximumConcurrentWrites => Volatile.Read(ref maximumConcurrentWrites);

        public void BlockNextWrite()
        {
            blockedWrite = NewSource();
            entered = NewSource();
        }

        public void ReleaseBlockedWrite() => blockedWrite?.TrySetResult();

        public void EnqueueResult(LegacyCharacterCheckpointSaveResult result) => results.Enqueue(result);

        public async ValueTask<LegacyCharacterCheckpointSaveResult> TrySaveCharacterCheckpointAsync(
            LegacyCharacterCheckpointSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            await writeGate.WaitAsync(cancellationToken);
            var active = Interlocked.Increment(ref activeWrites);
            UpdateMaximum(active);
            try
            {
                requests.Enqueue(request);
                entered.TrySetResult();
                if (blockedWrite is { } block)
                    await block.Task.WaitAsync(cancellationToken);
                return results.TryDequeue(out var result) ? result : LegacyCharacterCheckpointSaveResult.Success;
            }
            finally
            {
                Interlocked.Decrement(ref activeWrites);
                writeGate.Release();
                blockedWrite = null;
            }
        }

        private void UpdateMaximum(int active)
        {
            while (true)
            {
                var current = Volatile.Read(ref maximumConcurrentWrites);
                if (active <= current || Interlocked.CompareExchange(ref maximumConcurrentWrites, active, current) == current)
                    return;
            }
        }

        private static TaskCompletionSource NewSource() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

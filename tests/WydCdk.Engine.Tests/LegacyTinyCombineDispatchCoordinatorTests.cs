using WydCdk.Protocol;
using WydCdk.Server;
using WydCdk.World;

internal static class LegacyTinyCombineDispatchCoordinatorTests
{
    private const string AccountName = "TINY_GUILD_TAX";
    private const int ConnectionId = 1;

    public static async Task RunAsync(
        Func<long, int, (WorldHub Hub, int NpcId, LegacyItem[] Items, sbyte[] Positions)> fixtureFactory)
    {
        await ResponsePlanFailureRollsBackBeforeCheckpoint(fixtureFactory);
        await SuccessfulPlanIsCheckpointedAndReleasesReservation(fixtureFactory);
        await InvalidPositionReleasesAccountBeforeAutoTradeBookWait(fixtureFactory);
    }

    private static async Task ResponsePlanFailureRollsBackBeforeCheckpoint(
        Func<long, int, (WorldHub Hub, int NpcId, LegacyItem[] Items, sbyte[] Positions)> fixtureFactory)
    {
        var fixture = fixtureFactory(0, 1_001);
        var original = GetMob(fixture.Hub);
        var originalTreasury = GetNpcMob(fixture.Hub, fixture.NpcId);
        var accountOperations = new LegacyAccountOperationGate();
        var (sessions, session) = CreatePlayingSession();
        var store = new CheckpointStore();
        var checkpoint = new LegacyCharacterCheckpointRuntime(
            sessions,
            fixture.Hub,
            store,
            static (_, _, _, _, _) => ValueTask.FromResult(CharacterPositionSaveResult.Success),
            accountOperationGate: accountOperations);
        var plannerEntered = NewSignal();
        var allowPlannerToFail = NewSignal();

        var dispatch = LegacyTinyCombineDispatchCoordinator.ExecuteAsync(
            accountOperations,
            AccountName,
            fixture.Hub,
            ConnectionId,
            fixture.Items,
            fixture.Positions,
            tinyBase: 100,
            rawRoll: 0,
            localServer: false,
            async (_, cancellationToken) =>
            {
                plannerEntered.TrySetResult();
                await allowPlannerToFail.Task.WaitAsync(cancellationToken);
                return null;
            });

        await plannerEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var applied = GetMob(fixture.Hub);
        Check(!applied.AsSpan().SequenceEqual(original), "The response planner was entered before Tiny applied its accepted outcome.");
        var checkpointPass = checkpoint.RunSinglePassAsync().AsTask();
        await Task.Delay(50);
        Check(!checkpointPass.IsCompleted && store.Requests.Count == 0,
            "Checkpoint captured the applied Tiny state while response planning still held the account reservation.");

        allowPlannerToFail.TrySetResult();
        var result = await dispatch.AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await checkpointPass.WaitAsync(TimeSpan.FromSeconds(5));

        Check(result.PreparationResult == LegacyTinyCombinePreparationResult.Accepted &&
              result.ApplicationResult == LegacyTinyCombineApplicationResult.Accepted &&
              result.ResponsePlanningFailed && result.RollbackAttempted && result.RollbackSucceeded,
            "A failed Tiny response plan did not report the conditional runtime rollback.");
        Check(GetMob(fixture.Hub).AsSpan().SequenceEqual(original) &&
              GetNpcMob(fixture.Hub, fixture.NpcId).AsSpan().SequenceEqual(originalTreasury),
            "A response-plan rollback left the player or guild treasury mutated.");
        Check(store.Requests.Count == 1 && store.Requests[0].Mob.Span.SequenceEqual(original),
            "Checkpoint persisted an intermediate Tiny state instead of the rolled-back MOB.");
        Check(sessions.TryGet(ConnectionId, out var currentSession) && currentSession == session,
            "The checkpoint fixture session changed unexpectedly.");
    }

    private static async Task SuccessfulPlanIsCheckpointedAndReleasesReservation(
        Func<long, int, (WorldHub Hub, int NpcId, LegacyItem[] Items, sbyte[] Positions)> fixtureFactory)
    {
        var fixture = fixtureFactory(0, 1_001);
        var accountOperations = new LegacyAccountOperationGate();
        var (sessions, _) = CreatePlayingSession();
        var store = new CheckpointStore();
        var checkpoint = new LegacyCharacterCheckpointRuntime(
            sessions,
            fixture.Hub,
            store,
            static (_, _, _, _, _) => ValueTask.FromResult(CharacterPositionSaveResult.Success),
            accountOperationGate: accountOperations);

        var result = await LegacyTinyCombineDispatchCoordinator.ExecuteAsync(
            accountOperations,
            AccountName,
            fixture.Hub,
            ConnectionId,
            fixture.Items,
            fixture.Positions,
            tinyBase: 100,
            rawRoll: 0,
            localServer: false,
            (outcome, _) => ValueTask.FromResult(BuildResponse(fixture.Hub, outcome)))
            .AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        Check(result.PreparationResult == LegacyTinyCombinePreparationResult.Accepted &&
              result.ApplicationResult == LegacyTinyCombineApplicationResult.Accepted &&
              result.Outcome is { Succeeded: true } && !result.ResponsePlanningFailed &&
              result.ResponseFrames is { Count: > 0 } && !result.RollbackAttempted,
            "A valid Tiny success did not return its planned relay frames.");

        await using (await accountOperations.AcquireAsync([AccountName]).AsTask().WaitAsync(TimeSpan.FromSeconds(2)))
        {
            // The reservation must be available before caller-owned treasury I/O or socket writes.
        }

        await checkpoint.RunSinglePassAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Check(store.Requests.Count == 1 && result.Outcome!.MobSnapshot.Span.SequenceEqual(store.Requests[0].Mob.Span),
            "Checkpoint did not persist the accepted Tiny result after dispatch released its reservation.");
    }

    private static async Task InvalidPositionReleasesAccountBeforeAutoTradeBookWait(
        Func<long, int, (WorldHub Hub, int NpcId, LegacyItem[] Items, sbyte[] Positions)> fixtureFactory)
    {
        var fixture = fixtureFactory(0, 1_001);
        var accountOperations = new LegacyAccountOperationGate();
        var autoTradeBook = new LegacyAutoTradeBook();
        var invalidPositions = fixture.Positions.ToArray();
        invalidPositions[0] = sbyte.MaxValue;

        await using var startBookLease = await autoTradeBook.AcquireAsync([ConnectionId]);
        var result = await LegacyTinyCombineDispatchCoordinator.ExecuteAsync(
            accountOperations,
            AccountName,
            fixture.Hub,
            ConnectionId,
            fixture.Items,
            invalidPositions,
            tinyBase: 100,
            rawRoll: 0,
            localServer: false,
            static (_, _) => ValueTask.FromResult<IReadOnlyList<byte[]>?>(null))
            .AsTask().WaitAsync(TimeSpan.FromSeconds(2));

        Check(result.PreparationResult == LegacyTinyCombinePreparationResult.InvalidInventoryPosition &&
              result.ApplicationResult is null && result.Outcome is null,
            "The invalid-position request was not rejected before apply.");

        // AutoTrade start holds book -> account. Prove that account is available even
        // while the book is held, then model caller cleanup waiting for the book.
        await using (await accountOperations.AcquireAsync([AccountName]).AsTask().WaitAsync(TimeSpan.FromSeconds(2)))
        {
        }

        var cleanup = Task.Run(async () =>
        {
            await using var cleanupBookLease = await autoTradeBook.AcquireAsync([ConnectionId]);
            await using var cleanupAccountLease = await accountOperations.AcquireAsync([AccountName]);
        });
        await Task.Delay(50);
        Check(!cleanup.IsCompleted, "Tiny invalid-position cleanup bypassed the held AutoTrade book lease.");
        await startBookLease.DisposeAsync();
        await cleanup.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private static IReadOnlyList<byte[]>? BuildResponse(WorldHub world, LegacyTinyCombineOutcome outcome)
    {
        var codec = LegacyFrameCodec.CreateDefault();
        if (!world.TryBuildUpdateEtcFrame(ConnectionId, codec, 0, 0, out var updateEtc) || updateEtc is null ||
            !LegacyTinyCombineRelay.TryBuild(
                outcome,
                updateEtc,
                codec,
                clientTick: 0,
                keywordIndex: 0,
                headerId: ConnectionId,
                LegacyTinyCombineRelay.ProcessingCompleteMessage,
                LegacyTinyCombineRelay.CombineFailedMessage,
                out var frames) || frames is null)
            return null;
        return frames.ToArray();
    }

    private static (LoginSessionRegistry Sessions, LoginSession Session) CreatePlayingSession()
    {
        var sessions = new LoginSessionRegistry();
        var login = new AccountLoginRequest(AccountName, string.Empty, new byte[52], ClientReleasePolicy.RequiredClientVersion, 0, []);
        Check(sessions.Open(ConnectionId) &&
              sessions.BeginAccountLogin(ConnectionId, login) == LoginTransitionResult.Accepted &&
              sessions.CompleteAccountLogin(ConnectionId, AccountName) == LoginTransitionResult.Accepted &&
              sessions.BeginCharacterWait(ConnectionId) == LoginTransitionResult.Accepted &&
              sessions.CompleteCharacterLogin(ConnectionId, 0, 2_100, 2_100) == LoginTransitionResult.Accepted,
            "Could not initialize the Tiny account checkpoint session.");
        Check(sessions.TryGet(ConnectionId, out var session) && session is not null,
            "Tiny account checkpoint session was not registered.");
        return (sessions, session!);
    }

    private static byte[] GetMob(WorldHub world)
    {
        Check(world.TryGetCharacterSnapshot(ConnectionId, out var mob, out _, out _) && mob is not null,
            "Tiny account MOB snapshot was unavailable.");
        return mob!;
    }

    private static byte[] GetNpcMob(WorldHub world, int npcId)
    {
        Check(world.TryGetNpcSnapshot(npcId, out var npc) && npc is not null,
            "Tiny guild treasury snapshot was unavailable.");
        return npc!.MobSnapshot.ToArray();
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class CheckpointStore : ICharacterCheckpointStore
    {
        private readonly List<LegacyCharacterCheckpointSaveRequest> requests = [];
        private readonly object gate = new();

        public IReadOnlyList<LegacyCharacterCheckpointSaveRequest> Requests
        {
            get { lock (gate) return requests.ToArray(); }
        }

        public ValueTask<LegacyCharacterCheckpointSaveResult> TrySaveCharacterCheckpointAsync(
            LegacyCharacterCheckpointSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            lock (gate)
                requests.Add(request with { Mob = request.Mob.ToArray() });
            return ValueTask.FromResult(LegacyCharacterCheckpointSaveResult.Success);
        }
    }
}

using WydCdk.Protocol;
using WydCdk.World;

namespace WydCdk.Server;

public static class LegacyCharacterCheckpointPolicy
{
    public const int FirstSlot = 1;
    public const int LastSlot = 999;
    public static TimeSpan Interval { get; } = TimeSpan.FromSeconds(4);
}

public sealed record LegacyCharacterCheckpointPassResult(
    int NextSlot,
    int? SavedSlot,
    LegacyCharacterCheckpointSaveResult? SaveResult);

/// <summary>TMProject-compatible serial round-robin checkpoints for live character snapshots.</summary>
public sealed class LegacyCharacterCheckpointRuntime
{
    private readonly LoginSessionRegistry sessions;
    private readonly WorldHub world;
    private readonly ICharacterCheckpointStore store;
    private readonly Func<string, int, short, short, CancellationToken, ValueTask<CharacterPositionSaveResult>> savePositionFallback;
    private readonly TimeProvider timeProvider;
    private readonly LegacyAccountOperationGate accountOperationGate;
    private readonly SemaphoreSlim passGate = new(1, 1);
    private readonly SemaphoreSlim[] slotGates = Enumerable.Range(0, LegacyCharacterCheckpointPolicy.LastSlot + 1)
        .Select(static _ => new SemaphoreSlim(1, 1))
        .ToArray();
    private int nextSlot = LegacyCharacterCheckpointPolicy.FirstSlot;

    public LegacyCharacterCheckpointRuntime(
        LoginSessionRegistry sessions,
        WorldHub world,
        ICharacterCheckpointStore store,
        Func<string, int, short, short, CancellationToken, ValueTask<CharacterPositionSaveResult>> savePositionFallback,
        TimeProvider? timeProvider = null,
        LegacyAccountOperationGate? accountOperationGate = null)
    {
        this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.savePositionFallback = savePositionFallback ?? throw new ArgumentNullException(nameof(savePositionFallback));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.accountOperationGate = accountOperationGate ?? new LegacyAccountOperationGate();
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(LegacyCharacterCheckpointPolicy.Interval, timeProvider);
        while (await timer.WaitForNextTickAsync(cancellationToken))
            await RunSinglePassAsync(cancellationToken);
    }

    public async ValueTask<LegacyCharacterCheckpointPassResult> RunSinglePassAsync(CancellationToken cancellationToken = default)
    {
        await passGate.WaitAsync(cancellationToken);
        try
        {
            var start = nextSlot;
            for (var scanned = 0; scanned < LegacyCharacterCheckpointPolicy.LastSlot; scanned++)
            {
                var slot = nextSlot;
                nextSlot = Advance(slot);

                if (!sessions.TryGet(slot, out var candidate) || !IsPlayingCharacter(candidate))
                    continue;

                var outcome = await TrySaveCurrentCharacterAsync(slot, candidate!, cancellationToken);
                if (!outcome.Attempted)
                    continue;

                nextSlot = Advance(slot);
                return new LegacyCharacterCheckpointPassResult(nextSlot, slot, outcome.Result);
            }

            // A complete empty scan wraps to its original starting position.
            nextSlot = start;
            return new LegacyCharacterCheckpointPassResult(nextSlot, null, null);
        }
        finally
        {
            passGate.Release();
        }
    }

    public async ValueTask SaveOnDepartureAsync(
        LoginSession session,
        bool disconnect,
        CancellationToken cancellationToken = default,
        LegacyAccountOperationLease? existingAccountLease = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!IsPlayingCharacter(session))
            return;

        var slot = session.ConnectionId;
        LegacyAccountOperationLease? ownedAccountLease = null;
        var slotAcquired = false;
        try
        {
            if (existingAccountLease is null)
                ownedAccountLease = await accountOperationGate.AcquireAsync([session.AccountName!], cancellationToken);
            else if (!existingAccountLease.Covers(accountOperationGate, session.AccountName!))
                throw new InvalidOperationException("The supplied account lease does not cover the departing character.");
            await slotGates[slot].WaitAsync(cancellationToken);
            slotAcquired = true;
            if (!sessions.TryGet(slot, out var current) || !IsSamePlayingCharacter(session, current))
                return;

            await PersistCurrentCharacterAsync(current!, disconnect, cancellationToken);
        }
        catch (LegacyAccountRecoveryRequiredException)
        {
            LogResult(slot, session.CharacterSlot, disconnect, "account-recovery-required");
        }
        finally
        {
            if (slotAcquired)
                slotGates[slot].Release();
            if (ownedAccountLease is not null)
                await ownedAccountLease.DisposeAsync();
        }
    }

    private async ValueTask<(bool Attempted, LegacyCharacterCheckpointSaveResult? Result)> TrySaveCurrentCharacterAsync(
        int slot,
        LoginSession candidate,
        CancellationToken cancellationToken)
    {
        await using var accountLease = await accountOperationGate.AcquireAsync([candidate.AccountName!], cancellationToken);
        await slotGates[slot].WaitAsync(cancellationToken);
        try
        {
            if (!sessions.TryGet(slot, out var current) || !IsSamePlayingCharacter(candidate, current))
                return (false, null);

            if (!world.TryGetCharacterSnapshot(slot, out var mob, out var x, out var y, out var extra, out var affect, out var equipment) ||
                mob is null || extra is null || affect is null || equipment is null)
                return (false, null);

            var result = await SaveSnapshotAsync(current!, mob, x, y, extra, affect, equipment, disconnect: false, cancellationToken);
            if (result != LegacyCharacterCheckpointSaveResult.Success)
                LogResult(slot, current!.CharacterSlot, departure: false, result.ToString());
            return (true, result);
        }
        catch (LegacyAccountRecoveryRequiredException)
        {
            LogResult(slot, candidate.CharacterSlot, departure: false, "account-recovery-required");
            return (false, null);
        }
        finally
        {
            slotGates[slot].Release();
        }
    }

    private async ValueTask PersistCurrentCharacterAsync(LoginSession session, bool disconnect, CancellationToken cancellationToken)
    {
        if (world.TryGetCharacterSnapshot(session.ConnectionId, out var mob, out var x, out var y, out var extra, out var affect, out var equipment) &&
            mob is not null && extra is not null && affect is not null && equipment is not null)
        {
            var result = await SaveSnapshotAsync(session, mob, x, y, extra, affect, equipment, disconnect, cancellationToken);
            LogDeparture(session, disconnect, result.ToString());
            return;
        }

        try
        {
            var result = await savePositionFallback(
                session.AccountName!,
                session.CharacterSlot,
                session.PositionX,
                session.PositionY,
                cancellationToken);
            LogDeparture(session, disconnect, $"position-fallback:{result}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            LogFailure(session.ConnectionId, session.CharacterSlot, disconnect, error);
        }
    }

    private async ValueTask<LegacyCharacterCheckpointSaveResult> SaveSnapshotAsync(
        LoginSession session,
        byte[] mob,
        short x,
        short y,
        byte[] extra,
        byte[] affect,
        IReadOnlyList<LegacyItem> equipment,
        bool disconnect,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await store.TrySaveCharacterCheckpointAsync(
                new LegacyCharacterCheckpointSaveRequest(
                    session.AccountName!,
                    session.CharacterSlot,
                    mob,
                    x,
                    y,
                    extra,
                    affect,
                    equipment),
                cancellationToken);
            if (result != LegacyCharacterCheckpointSaveResult.Success)
                LogResult(session.ConnectionId, session.CharacterSlot, disconnect, result.ToString());
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return LegacyCharacterCheckpointSaveResult.NotAvailable;
        }
    }

    private static int Advance(int slot) => slot >= LegacyCharacterCheckpointPolicy.LastSlot
        ? LegacyCharacterCheckpointPolicy.FirstSlot
        : slot + 1;

    private static bool IsPlayingCharacter(LoginSession? session) =>
        session is { State: LoginSessionState.Playing, AccountName: not null, CharacterSlot: >= 0 and < LegacyAccountSnapshot.CharacterCount } &&
        session.ConnectionId is >= LegacyCharacterCheckpointPolicy.FirstSlot and <= LegacyCharacterCheckpointPolicy.LastSlot;

    private static bool IsSamePlayingCharacter(LoginSession expected, LoginSession? current) =>
        IsPlayingCharacter(current) &&
        current!.ConnectionId == expected.ConnectionId &&
        current.CharacterSlot == expected.CharacterSlot &&
        string.Equals(current.AccountName, expected.AccountName, StringComparison.Ordinal);

    private static void LogResult(int connectionId, int characterSlot, bool departure, string result) =>
        Console.Error.WriteLine($"Character {(departure ? "departure " : string.Empty)}checkpoint failed: connection={connectionId}, slot={characterSlot}, result={result}.");

    private static void LogFailure(int connectionId, int characterSlot, bool departure, Exception error) =>
        Console.Error.WriteLine($"Character {(departure ? "departure " : string.Empty)}checkpoint failed: connection={connectionId}, slot={characterSlot}, error={error.GetType().Name}.");

    private static void LogDeparture(LoginSession session, bool disconnect, string result) =>
        Console.WriteLine($"Character {(disconnect ? "disconnect " : string.Empty)}checkpoint: connection={session.ConnectionId}, slot={session.CharacterSlot}, result={result}.");
}

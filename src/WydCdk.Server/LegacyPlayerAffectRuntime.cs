using WydCdk.World;

namespace WydCdk.Server;

/// <summary>Runs the source-aligned 16-slot player affect timer over one shard per second.</summary>
public sealed class LegacyPlayerAffectRuntime
{
    public const int ShardCount = 16;
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private readonly WorldHub world;
    private readonly Func<LegacyPlayerAffectTimerOutcome, CancellationToken, ValueTask> relay;
    private readonly SemaphoreSlim tickGate = new(1, 1);
    private int nextShard;
    private int runStarted;

    public LegacyPlayerAffectRuntime(
        WorldHub world,
        Func<LegacyPlayerAffectTimerOutcome, CancellationToken, ValueTask> relay)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.relay = relay ?? throw new ArgumentNullException(nameof(relay));
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref runStarted, 1, 0) != 0)
            throw new InvalidOperationException("The player affect runtime can only run once.");

        try
        {
            using var timer = new PeriodicTimer(TickInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                await TickNextShardAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref runStarted, 0);
        }
    }

    public async ValueTask<IReadOnlyList<LegacyPlayerAffectTimerOutcome>> TickNextShardAsync(
        CancellationToken cancellationToken = default)
    {
        await tickGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var shardIndex = nextShard++ % ShardCount;
            return await TickShardCoreAsync(shardIndex, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            tickGate.Release();
        }
    }

    public async ValueTask<IReadOnlyList<LegacyPlayerAffectTimerOutcome>> TickShardAsync(
        int shardIndex,
        CancellationToken cancellationToken = default)
    {
        if (shardIndex < 0 || shardIndex >= ShardCount)
            throw new ArgumentOutOfRangeException(nameof(shardIndex));

        await tickGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await TickShardCoreAsync(shardIndex, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            tickGate.Release();
        }
    }

    private async ValueTask<IReadOnlyList<LegacyPlayerAffectTimerOutcome>> TickShardCoreAsync(
        int shardIndex,
        CancellationToken cancellationToken)
    {
        var outcomes = new List<LegacyPlayerAffectTimerOutcome>();
        var connectionIds = world.GetParticipantIdsForTimerShard(shardIndex, ShardCount);
        foreach (var connectionId in connectionIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outcome = world.TryProcessPlayerTimerAffects(connectionId);
            if (outcome is null)
                continue;

            outcomes.Add(outcome);
            await relay(outcome, cancellationToken).ConfigureAwait(false);
        }

        return Array.AsReadOnly(outcomes.ToArray());
    }
}

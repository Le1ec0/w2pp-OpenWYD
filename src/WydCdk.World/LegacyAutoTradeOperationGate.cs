namespace WydCdk.World;

/// <summary>Host-lifetime, bounded registry. Never dispose a semaphore with live waiters.</summary>
internal sealed class LegacyAutoTradeOperationGate
{
    private readonly object sync = new();
    private readonly Dictionary<int, SemaphoreSlim> gates = [];

    internal async ValueTask<LegacyAutoTradeOperationLease> AcquireAsync(
        IEnumerable<int> connectionIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connectionIds);
        var ids = connectionIds.Distinct().Order().ToArray();
        if (ids.Length == 0 || ids.Any(static id => id is <= 0 or > ushort.MaxValue))
            throw new ArgumentOutOfRangeException(nameof(connectionIds));
        var acquired = new List<SemaphoreSlim>(ids.Length);
        try
        {
            foreach (var id in ids)
            {
                SemaphoreSlim semaphore;
                lock (sync)
                {
                    if (!gates.TryGetValue(id, out semaphore!))
                        gates.Add(id, semaphore = new SemaphoreSlim(1, 1));
                }
                await semaphore.WaitAsync(cancellationToken);
                acquired.Add(semaphore);
            }
            return new(this, ids, acquired.ToArray());
        }
        catch
        {
            for (var i = acquired.Count - 1; i >= 0; i--) acquired[i].Release();
            throw;
        }
    }
}

public sealed class LegacyAutoTradeOperationLease : IAsyncDisposable
{
    private readonly LegacyAutoTradeOperationGate owner;
    private readonly int[] ids;
    private SemaphoreSlim[]? acquired;

    internal LegacyAutoTradeOperationLease(LegacyAutoTradeOperationGate owner, int[] ids, SemaphoreSlim[] acquired)
    {
        this.owner = owner;
        this.ids = ids;
        this.acquired = acquired;
    }

    public bool Covers(int connectionId) => Volatile.Read(ref acquired) is not null &&
        Array.BinarySearch(ids, connectionId) >= 0;

    internal bool BelongsTo(LegacyAutoTradeOperationGate gate, int connectionId) =>
        ReferenceEquals(owner, gate) && Covers(connectionId);

    public ValueTask DisposeAsync()
    {
        var held = Interlocked.Exchange(ref acquired, null);
        if (held is not null)
            for (var i = held.Length - 1; i >= 0; i--) held[i].Release();
        return ValueTask.CompletedTask;
    }
}

public sealed record LegacyAutoTradeOwner(string AccountName, int CharacterSlot);
public sealed record LegacyAutoTradeListingHandle(
    long Generation, LegacyAutoTradeOwner Owner, LegacyAutoTradeSnapshot Snapshot);

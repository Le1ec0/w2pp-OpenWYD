namespace WydCdk.Server;

/// <summary>Serializes guild state transitions with the notices and SendWarInfo relays they produce.</summary>
public sealed class GuildWarTransitionRelayGate
{
    public static GuildWarTransitionRelayGate Shared { get; } = new();

    private readonly SemaphoreSlim semaphore = new(1, 1);

    public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        await semaphore.WaitAsync(cancellationToken);
        return new Lease(semaphore);
    }

    private sealed class Lease : IDisposable
    {
        private SemaphoreSlim? owner;

        public Lease(SemaphoreSlim owner) => this.owner = owner;

        public void Dispose() => Interlocked.Exchange(ref owner, null)?.Release();
    }
}

namespace WydCdk.World;

/// <summary>
/// Serializes live operations by canonical account within one backend namespace.
/// The host owns and shares one instance across checkpoint, trade, and shop paths.
/// </summary>
public sealed class LegacyAccountOperationGate
{
    private const int LockCount = 128;
    private readonly object recoveryGate = new();
    private readonly Dictionary<string, string> recoveryRequired = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim[] locks = Enumerable.Range(0, LockCount)
        .Select(static _ => new SemaphoreSlim(1, 1))
        .ToArray();

    public LegacyAccountOperationGate(string storeNamespace = "default")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeNamespace);
        StoreNamespace = storeNamespace.Trim().ToUpperInvariant();
    }

    public string StoreNamespace { get; }

    public void MarkRecoveryRequired(string accountName, string reason)
    {
        if (!LegacyAccountNameRules.TryNormalize(accountName, out var canonical))
            throw new ArgumentException("Account name is invalid.", nameof(accountName));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        lock (recoveryGate)
            recoveryRequired[canonical] = reason.Trim();
    }

    public bool ResolveRecovery(string accountName)
    {
        if (!LegacyAccountNameRules.TryNormalize(accountName, out var canonical))
            return false;
        lock (recoveryGate)
            return recoveryRequired.Remove(canonical);
    }

    public bool IsRecoveryRequired(string accountName)
    {
        if (!LegacyAccountNameRules.TryNormalize(accountName, out var canonical))
            return false;
        lock (recoveryGate)
            return recoveryRequired.ContainsKey(canonical);
    }

    public ValueTask<LegacyAccountOperationLease> AcquireAsync(
        IEnumerable<string> accountNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountNames);
        var normalized = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var accountName in accountNames)
        {
            if (!LegacyAccountNameRules.TryNormalize(accountName, out var canonical))
                throw new ArgumentException("Every account name must use the canonical legacy account alphabet and length.", nameof(accountNames));
            normalized.Add(canonical);
        }
        if (normalized.Count == 0)
            throw new ArgumentException("At least one account name is required.", nameof(accountNames));

        var canonicalAccounts = normalized.ToArray();
        ThrowIfRecoveryRequired(canonicalAccounts);
        var lockIndexes = canonicalAccounts.Select(LockIndex).Distinct().Order().ToArray();
        return AcquireCoreAsync(canonicalAccounts, lockIndexes, cancellationToken);
    }

    private async ValueTask<LegacyAccountOperationLease> AcquireCoreAsync(
        string[] accountNames,
        int[] lockIndexes,
        CancellationToken cancellationToken)
    {
        var acquired = new List<SemaphoreSlim>(lockIndexes.Length);
        try
        {
            foreach (var lockIndex in lockIndexes)
            {
                var semaphore = locks[lockIndex];
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                acquired.Add(semaphore);
            }

            ThrowIfRecoveryRequired(accountNames);
            return new LegacyAccountOperationLease(this, accountNames, acquired.ToArray());
        }
        catch
        {
            for (var index = acquired.Count - 1; index >= 0; index--)
                acquired[index].Release();
            throw;
        }
    }

    private void ThrowIfRecoveryRequired(IEnumerable<string> accountNames)
    {
        lock (recoveryGate)
        {
            var blocked = accountNames
                .Where(recoveryRequired.ContainsKey)
                .Select(account => $"{account}: {recoveryRequired[account]}")
                .ToArray();
            if (blocked.Length > 0)
                throw new LegacyAccountRecoveryRequiredException(blocked);
        }
    }

    private static int LockIndex(string accountName)
    {
        // Account names are canonical ASCII. A fixed table keeps host-lifetime
        // lock state bounded; collisions only serialize unrelated accounts.
        uint hash = 2_166_136_261;
        foreach (var character in accountName)
            hash = unchecked((hash ^ (byte)character) * 16_777_619);
        return (int)(hash % LockCount);
    }

    internal bool Owns(LegacyAccountOperationLease lease) =>
        ReferenceEquals(lease.Owner, this) && !lease.IsDisposed;
}

public sealed class LegacyAccountRecoveryRequiredException(IReadOnlyList<string> blockedAccounts)
    : InvalidOperationException($"Account operation blocked pending authoritative recovery: {string.Join(", ", blockedAccounts)}")
{
    public IReadOnlyList<string> BlockedAccounts { get; } = Array.AsReadOnly(blockedAccounts.ToArray());
}

public sealed class LegacyAccountOperationLease : IAsyncDisposable
{
    private readonly SemaphoreSlim[] semaphores;
    private int disposed;

    internal LegacyAccountOperationLease(
        LegacyAccountOperationGate owner,
        string[] accountNames,
        SemaphoreSlim[] semaphores)
    {
        Owner = owner;
        AccountNames = accountNames;
        this.semaphores = semaphores;
    }

    internal LegacyAccountOperationGate Owner { get; }
    internal bool IsDisposed => Volatile.Read(ref disposed) != 0;
    public IReadOnlyList<string> AccountNames { get; }

    public bool Covers(LegacyAccountOperationGate gate, string accountName) =>
        gate is not null && gate.Owns(this) && LegacyAccountNameRules.TryNormalize(accountName, out var canonical) &&
        Array.BinarySearch(AccountNames.ToArray(), canonical, StringComparer.Ordinal) >= 0;

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            for (var index = semaphores.Length - 1; index >= 0; index--)
                semaphores[index].Release();
        }
        return ValueTask.CompletedTask;
    }
}

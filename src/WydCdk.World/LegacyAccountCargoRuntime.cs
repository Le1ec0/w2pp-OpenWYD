using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Account/world scoped in-process owner for the complete Cargo array. Character
/// participants remain keyed by connection; active sessions for the same account
/// observe this shared revisioned value.
/// </summary>
public sealed class LegacyAccountCargoRuntime
{
    private readonly object gate = new();
    private readonly Dictionary<string, CargoOwner> owners = new(StringComparer.Ordinal);

    public LegacyAccountCargoRuntime(string worldKey)
    {
        if (!LegacyServerModePolicy.TryParseWorldKey(worldKey, out var mode))
            throw new ArgumentException("World key must be UP or PVP.", nameof(worldKey));
        WorldKey = new LegacyServerModePolicy(mode).WorldKey;
    }

    public string WorldKey { get; }

    public void RegisterSession(string accountName, int connectionId)
    {
        if (!LegacyAccountNameRules.TryNormalize(accountName, out var canonical))
            throw new ArgumentException("Account name is invalid.", nameof(accountName));
        if (connectionId <= 0)
            throw new ArgumentOutOfRangeException(nameof(connectionId));

        lock (gate)
        {
            var owner = GetOrCreateOwner(canonical);
            owner.ConnectionIds.Add(connectionId);
        }
    }

    public void UnregisterSession(string accountName, int connectionId)
    {
        if (!LegacyAccountNameRules.TryNormalize(accountName, out var canonical))
            return;

        lock (gate)
        {
            if (!owners.TryGetValue(canonical, out var owner))
                return;
            owner.ConnectionIds.Remove(connectionId);
            if (owner.ConnectionIds.Count == 0)
                owners.Remove(canonical);
        }
    }

    /// <summary>Initializes or reconciles runtime Cargo from the selected persistence adapter.</summary>
    public LegacyAccountCargoSnapshot LoadAuthoritative(string accountName, IReadOnlyList<LegacyItem> cargo)
    {
        var canonical = NormalizeAndValidate(accountName, cargo);
        lock (gate)
        {
            var owner = GetOrCreateOwner(canonical);
            var reconciled = false;
            IReadOnlyList<int> changedPositions = Array.Empty<int>();
            if (!owner.Initialized)
            {
                owner.Items = cargo.ToArray();
                owner.Initialized = true;
            }
            else if (!owner.Items.AsSpan().SequenceEqual(cargo.ToArray()))
            {
                changedPositions = Diff(owner.Items, cargo);
                owner.Items = cargo.ToArray();
                owner.Revision = checked(owner.Revision + 1);
                reconciled = true;
            }

            return Snapshot(canonical, owner, reconciled, changedPositions);
        }
    }

    public bool TryGet(string accountName, out LegacyAccountCargoSnapshot? snapshot)
    {
        snapshot = null;
        if (!LegacyAccountNameRules.TryNormalize(accountName, out var canonical))
            return false;
        lock (gate)
        {
            if (!owners.TryGetValue(canonical, out var owner) || !owner.Initialized)
                return false;
            snapshot = Snapshot(canonical, owner, wasReconciled: false);
            return true;
        }
    }

    public bool TryGetSessions(string accountName, out IReadOnlyList<int> connectionIds)
    {
        connectionIds = Array.Empty<int>();
        if (!LegacyAccountNameRules.TryNormalize(accountName, out var canonical))
            return false;
        lock (gate)
        {
            if (!owners.TryGetValue(canonical, out var owner))
                return false;
            connectionIds = owner.ConnectionIds.Order().ToArray();
            return true;
        }
    }

    public bool TryCommit(
        string accountName,
        long expectedRevision,
        IReadOnlyList<LegacyItem> expectedCargo,
        IReadOnlyList<LegacyItem> newCargo,
        out LegacyAccountCargoSnapshot? committed)
    {
        committed = null;
        if (!LegacyAccountNameRules.TryNormalize(accountName, out var canonical) ||
            expectedCargo.Count != LegacyAccountSnapshot.CargoCount ||
            newCargo.Count != LegacyAccountSnapshot.CargoCount)
            return false;

        lock (gate)
        {
            if (!owners.TryGetValue(canonical, out var owner) || !owner.Initialized ||
                owner.Revision != expectedRevision || !owner.Items.AsSpan().SequenceEqual(expectedCargo.ToArray()))
                return false;

            owner.Items = newCargo.ToArray();
            owner.Revision = checked(owner.Revision + 1);
            committed = Snapshot(canonical, owner, wasReconciled: false, Diff(expectedCargo, newCargo));
            return true;
        }
    }

    private CargoOwner GetOrCreateOwner(string canonical) =>
        owners.TryGetValue(canonical, out var owner)
            ? owner
            : owners[canonical] = new CargoOwner();

    private static string NormalizeAndValidate(string accountName, IReadOnlyList<LegacyItem> cargo)
    {
        if (!LegacyAccountNameRules.TryNormalize(accountName, out var canonical))
            throw new ArgumentException("Account name is invalid.", nameof(accountName));
        ArgumentNullException.ThrowIfNull(cargo);
        if (cargo.Count != LegacyAccountSnapshot.CargoCount)
            throw new ArgumentException($"Cargo must contain exactly {LegacyAccountSnapshot.CargoCount} items.", nameof(cargo));
        return canonical;
    }

    private static LegacyAccountCargoSnapshot Snapshot(
        string accountName,
        CargoOwner owner,
        bool wasReconciled,
        IReadOnlyList<int>? changedPositions = null) =>
        new(accountName, owner.Revision, Array.AsReadOnly(owner.Items.ToArray()), wasReconciled)
        {
            ChangedPositions = Array.AsReadOnly((changedPositions ?? Array.Empty<int>()).ToArray()),
        };

    private static IReadOnlyList<int> Diff(IReadOnlyList<LegacyItem> previous, IReadOnlyList<LegacyItem> next)
    {
        var changed = new List<int>();
        for (var index = 0; index < LegacyAccountSnapshot.CargoCount; index++)
            if (previous[index] != next[index])
                changed.Add(index);
        return changed;
    }

    private sealed class CargoOwner
    {
        public LegacyItem[] Items { get; set; } = new LegacyItem[LegacyAccountSnapshot.CargoCount];
        public long Revision { get; set; }
        public bool Initialized { get; set; }
        public HashSet<int> ConnectionIds { get; } = [];
    }
}

public sealed record LegacyAccountCargoSnapshot(
    string AccountName,
    long Revision,
    IReadOnlyList<LegacyItem> Items,
    bool WasReconciled)
{
    public IReadOnlyList<int> ChangedPositions { get; init; } = Array.Empty<int>();
}

using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// In-memory authoritative state for the legacy offline-shop offer. It does
/// not send frames or write persistence; the host dispatcher and relay consume
/// this state, while opening and offline persistence remain separate.
/// </summary>
public sealed class LegacyAutoTradeBook
{
    public const int CargoSlotCount = 120;
    public const int PriceLimit = 1_999_999_999;

    private static readonly HashSet<short> rejectedItemIndices =
    [
        446, 508, 509, 522, 747, 3993,
        526, 527, 528, 529, 530, 531,
    ];

    private readonly object gate = new();
    private readonly Dictionary<int, LegacyAutoTradeSnapshot> listings = [];
    private readonly Dictionary<int, (long Generation, LegacyAutoTradeOwner Owner)> identities = [];
    private readonly LegacyAutoTradeOperationGate operations = new();
    private long nextGeneration;
    private readonly Dictionary<int, LegacyAutoTradePendingClose> pendingCloses = [];

    public bool IsOwnerPending(LegacyAutoTradeOwner owner)
    {
        if (!TryNormalizeOwner(owner, out var normalized)) return false;
        lock (gate) return pendingCloses.Values.Any(pending => pending.AffectedOwners.Contains(normalized!) ||
            pending.Listings.Any(handle => handle.Owner == normalized));
    }

    // Discovery only. The caller must acquire the saved ids and revalidate the token.
    public bool TryDiscoverPendingClose(LegacyAutoTradeOwner owner, out LegacyAutoTradePendingClose? pending)
    {
        pending = null;
        if (!TryNormalizeOwner(owner, out var normalized)) return false;
        lock (gate)
        {
            var current = pendingCloses.Values.FirstOrDefault(value => value.AffectedOwners.Contains(normalized!) ||
                value.Listings.Any(handle => handle.Owner == normalized));
            if (current is null) return false;
            pending = CopyPending(current);
            return true;
        }
    }

    public bool TryGetPendingClose(LegacyAutoTradeOperationLease lease, int connectionId, out LegacyAutoTradePendingClose? pending)
    {
        pending = null;
        if (!ValidLease(lease, connectionId)) return false;
        lock (gate)
        {
            if (!pendingCloses.TryGetValue(connectionId, out var current)) return false;
            pending = CopyPending(current);
            return true;
        }
    }

    public bool TrySetPendingClose(LegacyAutoTradeOperationLease lease, LegacyAutoTradePendingClose pending)
    {
        ArgumentNullException.ThrowIfNull(pending);
        var ids = PendingIds(pending);
        lock (gate)
        {
            if (pending.Listings.Count is < 1 or > 2 || pending.Reservation.Token == Guid.Empty ||
                pending.Reservation.OpponentId < 0 || pending.Reservation.OpponentId == pending.Reservation.ConnectionId ||
                pending.AffectedOwners is null || pending.AffectedOwners.Any(owner => !TryNormalizeOwner(owner, out _) || IsOwnerPending(owner)) ||
                pending.Request.ExpectedListings.Count != pending.Listings.Count ||
                ids.Any(id => !ValidLease(lease, id) || pendingCloses.ContainsKey(id)) ||
                pending.Listings.Any(handle => !ids.Contains(handle.Snapshot.ConnectionId) || !Matches(handle)) ||
                pending.Listings.Any(handle => IsOwnerPending(handle.Owner) || !pending.Request.ExpectedListings.Any(expected =>
                    expected.AccountName == handle.Owner.AccountName && expected.CharacterSlot == handle.Owner.CharacterSlot &&
                    SameSnapshot(expected.BindToConnection(handle.Snapshot.ConnectionId), handle.Snapshot))) ||
                pending.Listings.Select(handle => handle.Snapshot.ConnectionId).Distinct().Count() != pending.Listings.Count)
                return false;
            // The owner barrier must cover every existing generation atomically.
            // Discovery before this lock cannot rule out a shop under another id.
            var owners = pending.AffectedOwners.Concat(pending.Listings.Select(handle => handle.Owner))
                .Select(owner => new LegacyAutoTradeOwner(owner.AccountName.Trim().ToUpperInvariant(), owner.CharacterSlot))
                .Distinct().ToArray();
            if (identities.Any(pair => !ids.Contains(pair.Key) && owners.Contains(pair.Value.Owner)))
                return false;
            var copy = CopyPending(pending);
            foreach (var id in ids) pendingCloses.Add(id, copy);
            return true;
        }
    }

    public bool TryClearPendingClose(LegacyAutoTradeOperationLease lease, LegacyAutoTradePendingClose expected) =>
        ResolvePending(lease, expected, removeListings: false);

    internal bool TryFinishPendingClose(LegacyAutoTradeOperationLease lease, LegacyAutoTradePendingClose expected) =>
        ResolvePending(lease, expected, removeListings: true);

    private bool ResolvePending(LegacyAutoTradeOperationLease lease, LegacyAutoTradePendingClose expected, bool removeListings)
    {
        ArgumentNullException.ThrowIfNull(expected);
        lock (gate)
        {
            var ids = PendingIds(expected);
            if (ids.Any(id => !ValidLease(lease, id) || !pendingCloses.TryGetValue(id, out var current) ||
                current.Reservation != expected.Reservation || !ReferenceEquals(current.Request, expected.Request) ||
                current.RuntimeDetached != expected.RuntimeDetached ||
                !current.AffectedOwners.SequenceEqual(expected.AffectedOwners) || current.Listings.Count != expected.Listings.Count ||
                !current.Listings.Zip(expected.Listings).All(pair => pair.First.Generation == pair.Second.Generation &&
                    pair.First.Owner == pair.Second.Owner && SameSnapshot(pair.First.Snapshot, pair.Second.Snapshot))) ||
                expected.Listings.Any(handle => !Matches(handle))) return false;
            if (removeListings)
                foreach (var handle in expected.Listings)
                {
                    listings.Remove(handle.Snapshot.ConnectionId);
                    identities.Remove(handle.Snapshot.ConnectionId);
                }
            foreach (var id in ids) pendingCloses.Remove(id);
            return true;
        }
    }

    internal static int[] PendingIds(LegacyAutoTradePendingClose pending) =>
        pending.Reservation.OpponentId > 0 ? [pending.Reservation.ConnectionId, pending.Reservation.OpponentId] : [pending.Reservation.ConnectionId];

    private static LegacyAutoTradePendingClose CopyPending(LegacyAutoTradePendingClose pending) => pending with
    {
        Listings = Array.AsReadOnly(pending.Listings.Select(handle => handle with { Snapshot = Copy(handle.Snapshot) }).ToArray()),
        AffectedOwners = Array.AsReadOnly(pending.AffectedOwners.Select(owner => new LegacyAutoTradeOwner(
            owner.AccountName.Trim().ToUpperInvariant(), owner.CharacterSlot)).Distinct().ToArray()),
    };

    public ValueTask<LegacyAutoTradeOperationLease> AcquireAsync(
        IEnumerable<int> connectionIds, CancellationToken cancellationToken = default) =>
        operations.AcquireAsync(connectionIds, cancellationToken);

    public bool TryGet(LegacyAutoTradeOperationLease lease, int connectionId,
        out LegacyAutoTradeListingHandle? handle)
    {
        handle = null;
        if (!ValidLease(lease, connectionId)) return false;
        lock (gate)
        {
            if (pendingCloses.ContainsKey(connectionId) || !listings.TryGetValue(connectionId, out var snapshot)) return false;
            var identity = identities[connectionId];
            handle = new(identity.Generation, identity.Owner, Copy(snapshot));
            return true;
        }
    }

    public bool HasListing(int connectionId)
    {
        lock (gate) return listings.ContainsKey(connectionId);
    }

    public LegacyAutoTradeStartResult TryStart(LegacyAutoTradeOperationLease lease,
        int connectionId, LegacyAutoTradeOwner owner, AutoTradeStartRequest request,
        LegacyAutoTradeStartContext context, out LegacyAutoTradeListingHandle? handle)
    {
        handle = null;
        if (!ValidLease(lease, connectionId) || !TryNormalizeOwner(owner, out var normalized))
            return LegacyAutoTradeStartResult.InvalidConnection;
        lock (gate)
        {
            if (pendingCloses.ContainsKey(connectionId) || IsOwnerPending(normalized!)) return LegacyAutoTradeStartResult.ClosePending;
            var result = TryStartCore(connectionId, request, context, out var snapshot);
            if (result == LegacyAutoTradeStartResult.Accepted)
            {
                identities.Add(connectionId, (checked(++nextGeneration), normalized!));
                handle = new(nextGeneration, normalized!, Copy(snapshot!));
            }
            return result;
        }
    }

    public bool TryRestore(LegacyAutoTradeOperationLease lease, int connectionId,
        LegacyAutoTradeOwner owner, LegacyAutoTradeSnapshot snapshot, out LegacyAutoTradeListingHandle? handle)
    {
        handle = null;
        if (!ValidLease(lease, connectionId) || !TryNormalizeOwner(owner, out var normalized)) return false;
        lock (gate)
        {
            if (pendingCloses.ContainsKey(connectionId) || IsOwnerPending(normalized!)) return false;
            if (!TryRestoreCore(connectionId, snapshot)) return false;
            identities.Add(connectionId, (checked(++nextGeneration), normalized!));
            handle = new(nextGeneration, normalized!, Copy(snapshot));
            return true;
        }
    }

    public bool TryStop(LegacyAutoTradeOperationLease lease, LegacyAutoTradeListingHandle expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (!ValidLease(lease, expected.Snapshot.ConnectionId)) return false;
        lock (gate)
        {
            if (pendingCloses.ContainsKey(expected.Snapshot.ConnectionId) || !Matches(expected)) return false;
            listings.Remove(expected.Snapshot.ConnectionId);
            identities.Remove(expected.Snapshot.ConnectionId);
            return true;
        }
    }

    public LegacyAutoTradePurchaseResult TryApplyPurchase(LegacyAutoTradeOperationLease lease,
        LegacyAutoTradeListingHandle expected, LegacyAutoTradePurchasePlan plan,
        out LegacyAutoTradeListingHandle? updated)
    {
        ArgumentNullException.ThrowIfNull(expected);
        updated = null;
        if (!ValidLease(lease, expected.Snapshot.ConnectionId))
            return LegacyAutoTradePurchaseResult.TargetNotInAutoTrade;
        lock (gate)
        {
            if (pendingCloses.ContainsKey(expected.Snapshot.ConnectionId) || !Matches(expected)) return LegacyAutoTradePurchaseResult.ItemChanged;
            var result = TryApplyPurchaseCore(expected.Snapshot.ConnectionId, plan, out var snapshot);
            if (snapshot is not null) updated = expected with { Snapshot = Copy(snapshot) };
            return result;
        }
    }

    public bool TryRollbackPurchase(LegacyAutoTradeOperationLease lease,
        LegacyAutoTradeListingHandle expectedUpdated, LegacyAutoTradeListingHandle original)
    {
        ArgumentNullException.ThrowIfNull(expectedUpdated);
        ArgumentNullException.ThrowIfNull(original);
        if (!ValidLease(lease, expectedUpdated.Snapshot.ConnectionId)) return false;
        lock (gate)
            return !pendingCloses.ContainsKey(expectedUpdated.Snapshot.ConnectionId) && expectedUpdated.Generation == original.Generation && expectedUpdated.Owner == original.Owner &&
                Matches(expectedUpdated) && TryRollbackPurchaseCore(expectedUpdated.Snapshot.ConnectionId,
                    expectedUpdated.Snapshot, original.Snapshot);
    }

    private bool ValidLease(LegacyAutoTradeOperationLease? lease, int id) =>
        lease is not null && lease.BelongsTo(operations, id);

    private bool Matches(LegacyAutoTradeListingHandle expected) =>
        identities.TryGetValue(expected.Snapshot.ConnectionId, out var identity) &&
        identity.Generation == expected.Generation && identity.Owner == expected.Owner &&
        listings.TryGetValue(expected.Snapshot.ConnectionId, out var snapshot) && SameSnapshot(snapshot, expected.Snapshot);

    private static bool TryNormalizeOwner(LegacyAutoTradeOwner? owner, out LegacyAutoTradeOwner? normalized)
    {
        normalized = null;
        if (owner is null || owner.CharacterSlot is < 0 or > 3 ||
            string.IsNullOrWhiteSpace(owner.AccountName) || owner.AccountName.Contains('\0')) return false;
        normalized = owner with { AccountName = owner.AccountName.Trim().ToUpperInvariant() };
        return true;
    }

    private static LegacyAutoTradeSnapshot Copy(LegacyAutoTradeSnapshot snapshot) => snapshot with
    {
        Items = snapshot.Items.ToArray(), CarryPositions = snapshot.CarryPositions.ToArray(), Prices = snapshot.Prices.ToArray(),
    };

    private LegacyAutoTradeStartResult TryStartCore(
        int connectionId,
        AutoTradeStartRequest request,
        LegacyAutoTradeStartContext context,
        out LegacyAutoTradeSnapshot? snapshot)
    {
        ArgumentNullException.ThrowIfNull(request);
        snapshot = null;

        lock (gate)
        {
            if (connectionId <= 0)
                return LegacyAutoTradeStartResult.InvalidConnection;
            if (!context.InPlay)
                return LegacyAutoTradeStartResult.RequesterNotPlaying;
            if (context.CurrentHp <= 0)
                return LegacyAutoTradeStartResult.RequesterNotAlive;
            if (context.AccountBlocked)
                return LegacyAutoTradeStartResult.AccountBlocked;
            if (context.LiveTradeActive)
                return LegacyAutoTradeStartResult.LiveTradeActive;
            if (listings.ContainsKey(connectionId))
                return LegacyAutoTradeStartResult.AlreadyActive;
            if (!context.InAllowedVillage)
                return LegacyAutoTradeStartResult.InvalidLocation;
            if (context.CityTax is < short.MinValue or > short.MaxValue)
                return LegacyAutoTradeStartResult.InvalidTax;
            if (context.Cargo.Count != CargoSlotCount)
                return LegacyAutoTradeStartResult.CargoUnavailable;
            if (!context.ItemDataAvailable && request.Items.Any(static item => item.Index != 0))
                return LegacyAutoTradeStartResult.ItemDataUnavailable;

            var validation = ValidateListing(request, context.Cargo, context.NonTradeableItemIndices);
            if (validation != LegacyAutoTradeStartResult.Accepted)
                return validation;

            var items = request.Items.ToArray();
            var carryPositions = request.CarryPositions.ToArray();
            var prices = request.Prices.ToArray();
            snapshot = new LegacyAutoTradeSnapshot(
                connectionId,
                context.PositionX,
                context.PositionY,
                request.Title,
                items,
                carryPositions,
                prices,
                checked((short)context.CityTax));
            listings.Add(connectionId, snapshot);
            return LegacyAutoTradeStartResult.Accepted;
        }
    }

    /// <summary>
    /// Atomically replaces one sold slot in the in-memory offer. The caller
    /// must have built the plan from the same snapshot; a changed price/item,
    /// or a concurrent purchase, is rejected without touching the book.
    /// </summary>
    private LegacyAutoTradePurchaseResult TryApplyPurchaseCore(
        int connectionId,
        LegacyAutoTradePurchasePlan plan,
        out LegacyAutoTradeSnapshot? updatedSnapshot)
    {
        ArgumentNullException.ThrowIfNull(plan);
        updatedSnapshot = null;

        lock (gate)
        {
            if (!listings.TryGetValue(connectionId, out var current))
                return LegacyAutoTradePurchaseResult.TargetNotInAutoTrade;
            if (plan.TargetId != connectionId || plan.Position is < 0 or >= AutoTradeListConfirmation.SlotCount)
                return LegacyAutoTradePurchaseResult.InvalidListing;
            if (current.Tax != plan.Settlement.ItemTax)
                return LegacyAutoTradePurchaseResult.TaxChanged;
            if (current.Items[plan.Position] != plan.PurchasedItem)
                return LegacyAutoTradePurchaseResult.ItemChanged;
            if (current.CarryPositions[plan.Position] != plan.SellerCargoPosition)
                return LegacyAutoTradePurchaseResult.SellerItemChanged;
            if (current.Prices[plan.Position] != plan.Settlement.ItemPrice)
                return LegacyAutoTradePurchaseResult.PriceChanged;

            var items = current.Items.ToArray();
            var carryPositions = current.CarryPositions.ToArray();
            var prices = current.Prices.ToArray();
            items[plan.Position] = default;
            carryPositions[plan.Position] = -1;
            prices[plan.Position] = 0;
            var candidate = current with
            {
                Items = items,
                CarryPositions = carryPositions,
                Prices = prices,
            };
            if (!SameSnapshot(candidate, plan.UpdatedListing))
                return LegacyAutoTradePurchaseResult.InvalidListing;

            listings[connectionId] = candidate;
            updatedSnapshot = candidate;
            return LegacyAutoTradePurchaseResult.Accepted;
        }
    }

    /// <summary>
    /// Restores a purchase only when the book still contains the exact result
    /// produced by that purchase. A concurrent sale or a newly started shop
    /// therefore cannot be overwritten by rollback.
    /// </summary>
    private bool TryRollbackPurchaseCore(
        int connectionId,
        LegacyAutoTradeSnapshot expectedUpdatedSnapshot,
        LegacyAutoTradeSnapshot originalSnapshot)
    {
        ArgumentNullException.ThrowIfNull(expectedUpdatedSnapshot);
        ArgumentNullException.ThrowIfNull(originalSnapshot);
        if (expectedUpdatedSnapshot.ConnectionId != connectionId || originalSnapshot.ConnectionId != connectionId)
            return false;

        lock (gate)
        {
            if (!listings.TryGetValue(connectionId, out var current) ||
                !SameSnapshot(current, expectedUpdatedSnapshot) ||
                !IsRestorable(originalSnapshot))
                return false;

            listings[connectionId] = originalSnapshot with
            {
                Items = originalSnapshot.Items.ToArray(),
                CarryPositions = originalSnapshot.CarryPositions.ToArray(),
                Prices = originalSnapshot.Prices.ToArray(),
            };
            return true;
        }
    }

    /// <summary>
    /// Restores a previously validated listing under a newly allocated world
    /// NPC id. Persistence owns the account/slot identity; this book owns only
    /// the runtime lookup used by list requests and visual relays.
    /// </summary>
    private bool TryRestoreCore(int connectionId, LegacyAutoTradeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (connectionId <= 0 || connectionId > ushort.MaxValue || snapshot.ConnectionId != connectionId)
            return false;
        if (!IsRestorable(snapshot))
            return false;

        lock (gate)
        {
            if (listings.ContainsKey(connectionId))
                return false;

            listings.Add(connectionId, snapshot with
            {
                Items = snapshot.Items.ToArray(),
                CarryPositions = snapshot.CarryPositions.ToArray(),
                Prices = snapshot.Prices.ToArray(),
            });
            return true;
        }
    }

    public IReadOnlyList<LegacyAutoTradeListingHandle> Snapshot()
    {
        lock (gate)
            return listings.Values.Select(snapshot => new LegacyAutoTradeListingHandle(
                identities[snapshot.ConnectionId].Generation, identities[snapshot.ConnectionId].Owner, Copy(snapshot))).ToArray();
    }

    private static LegacyAutoTradeStartResult ValidateListing(
        AutoTradeStartRequest request,
        IReadOnlyList<LegacyItem> cargo,
        IReadOnlySet<short> nonTradeableItemIndices)
    {
        if (request.Items.Count != AutoTradeListConfirmation.SlotCount ||
            request.CarryPositions.Count != AutoTradeListConfirmation.SlotCount ||
            request.Prices.Count != AutoTradeListConfirmation.SlotCount)
            return LegacyAutoTradeStartResult.InvalidListingShape;

        for (var index = 0; index < AutoTradeListConfirmation.SlotCount; index++)
        {
            var item = request.Items[index];
            var price = request.Prices[index];
            if (price < 0 || price > PriceLimit)
                return LegacyAutoTradeStartResult.InvalidPrice;

            if (item.Index == 0)
            {
                if (price != 0)
                    return LegacyAutoTradeStartResult.EmptyItemWithPrice;
                continue;
            }

            if (price == 0)
                return LegacyAutoTradeStartResult.ItemWithoutPrice;
            if (rejectedItemIndices.Contains(item.Index))
                return LegacyAutoTradeStartResult.ItemRejected;
            if (nonTradeableItemIndices.Contains(item.Index))
                return LegacyAutoTradeStartResult.ItemNotTradeable;

            var carryPosition = request.CarryPositions[index];
            if (carryPosition < 0 || carryPosition >= CargoSlotCount)
                return LegacyAutoTradeStartResult.InvalidCargoPosition;
            if (cargo[carryPosition] != item)
                return LegacyAutoTradeStartResult.ItemChanged;
        }

        return LegacyAutoTradeStartResult.Accepted;
    }

    private static bool IsRestorable(LegacyAutoTradeSnapshot snapshot)
    {
        if (snapshot.Items.Count != AutoTradeListConfirmation.SlotCount ||
            snapshot.CarryPositions.Count != AutoTradeListConfirmation.SlotCount ||
            snapshot.Prices.Count != AutoTradeListConfirmation.SlotCount)
            return false;

        try
        {
            _ = new AutoTradeListConfirmation(
                snapshot.Title,
                snapshot.Items,
                snapshot.CarryPositions,
                snapshot.Prices,
                snapshot.Tax,
                targetId: checked((ushort)snapshot.ConnectionId));
        }
        catch (ArgumentException)
        {
            return false;
        }

        for (var index = 0; index < AutoTradeListConfirmation.SlotCount; index++)
        {
            var item = snapshot.Items[index];
            var price = snapshot.Prices[index];
            var carryPosition = snapshot.CarryPositions[index];
            if (price < 0 || price > PriceLimit)
                return false;
            if (item.Index == 0)
            {
                if (price != 0)
                    return false;
            }
            else if (price == 0 || carryPosition < 0 || carryPosition >= CargoSlotCount)
                return false;
        }

        return true;
    }

    private static bool SameSnapshot(LegacyAutoTradeSnapshot left, LegacyAutoTradeSnapshot right) =>
        left.ConnectionId == right.ConnectionId &&
        left.PositionX == right.PositionX && left.PositionY == right.PositionY &&
        string.Equals(left.Title, right.Title, StringComparison.Ordinal) && left.Tax == right.Tax &&
        left.Items.SequenceEqual(right.Items) && left.CarryPositions.SequenceEqual(right.CarryPositions) &&
        left.Prices.SequenceEqual(right.Prices);
}

public readonly record struct LegacyAutoTradeStartContext(
    bool InPlay,
    int CurrentHp,
    bool LiveTradeActive,
    bool InAllowedVillage,
    int CityTax,
    short PositionX,
    short PositionY,
    IReadOnlyList<LegacyItem> Cargo,
    IReadOnlySet<short> NonTradeableItemIndices,
    bool AccountBlocked = false,
    bool ItemDataAvailable = true);

public sealed record LegacyAutoTradeSnapshot(
    int ConnectionId,
    short PositionX,
    short PositionY,
    string Title,
    IReadOnlyList<LegacyItem> Items,
    IReadOnlyList<sbyte> CarryPositions,
    IReadOnlyList<int> Prices,
    short Tax);

public enum LegacyAutoTradeStartResult
{
    Accepted,
    InvalidConnection,
    RequesterNotPlaying,
    RequesterNotAlive,
    AccountBlocked,
    LiveTradeActive,
    AlreadyActive,
    InvalidLocation,
    InvalidTax,
    CargoUnavailable,
    InvalidListingShape,
    InvalidPrice,
    EmptyItemWithPrice,
    ItemWithoutPrice,
    ItemRejected,
    ItemNotTradeable,
    ItemDataUnavailable,
    InvalidCargoPosition,
    ItemChanged,
    ClosePending,
}

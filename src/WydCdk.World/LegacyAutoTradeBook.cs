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

    public LegacyAutoTradeStartResult TryStart(
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

    public bool TryGet(int connectionId, out LegacyAutoTradeSnapshot? snapshot)
    {
        lock (gate)
        {
            if (listings.TryGetValue(connectionId, out var current))
            {
                snapshot = current;
                return true;
            }

            snapshot = null;
            return false;
        }
    }

    /// <summary>
    /// Atomically replaces one sold slot in the in-memory offer. The caller
    /// must have built the plan from the same snapshot; a changed price/item,
    /// or a concurrent purchase, is rejected without touching the book.
    /// </summary>
    public LegacyAutoTradePurchaseResult TryApplyPurchase(
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
    public bool TryRollbackPurchase(
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
    public bool TryRestore(int connectionId, LegacyAutoTradeSnapshot snapshot)
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

    public bool TryStop(int connectionId, out LegacyAutoTradeSnapshot? snapshot)
    {
        lock (gate)
        {
            if (!listings.Remove(connectionId, out var removed))
            {
                snapshot = null;
                return false;
            }

            snapshot = removed;
            return true;
        }
    }

    public IReadOnlyList<LegacyAutoTradeSnapshot> Snapshot()
    {
        lock (gate)
            return listings.Values.ToArray();
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
}

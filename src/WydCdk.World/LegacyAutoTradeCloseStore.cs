namespace WydCdk.World;

public enum LegacyAutoTradeCloseStoreResult { Removed, AlreadyAbsent, Conflict, InvalidRequest, Unavailable, Ambiguous }
public enum LegacyAutoTradeCloseReadState { AllAbsent, AllUnchanged, Conflict, Unknown }

public interface ILegacyAutoTradeCloseStore
{
    ValueTask<LegacyAutoTradeCloseStoreResult> RemoveExpectedAsync(
        LegacyAutoTradeCloseRequest request, CancellationToken cancellationToken = default);
    ValueTask<LegacyAutoTradeCloseReadState> ReadCloseStateAsync(
        LegacyAutoTradeCloseRequest request, CancellationToken cancellationToken = default);
}

/// <summary>One or two expected listings, copied at the application boundary.</summary>
public sealed class LegacyAutoTradeCloseRequest
{
    public IReadOnlyList<LegacyAutoTradePersistedListing> ExpectedListings { get; }

    public LegacyAutoTradeCloseRequest(IReadOnlyList<LegacyAutoTradePersistedListing> expectedListings)
    {
        ArgumentNullException.ThrowIfNull(expectedListings);
        if (expectedListings.Count is < 1 or > 2) throw new ArgumentException("Expected one or two listings.", nameof(expectedListings));
        var copies = new List<LegacyAutoTradePersistedListing>();
        foreach (var value in expectedListings)
        {
            if (value is null || string.IsNullOrWhiteSpace(value.AccountName) || value.AccountName.Contains('\0') ||
                value.CharacterSlot is < 0 or > 3 || value.Items is null || value.CarryPositions is null || value.Prices is null ||
                value.Items.Count != Protocol.AutoTradeListConfirmation.SlotCount ||
                value.CarryPositions.Count != Protocol.AutoTradeListConfirmation.SlotCount ||
                value.Prices.Count != Protocol.AutoTradeListConfirmation.SlotCount)
                throw new ArgumentException("Invalid expected listing.", nameof(expectedListings));
            var copy = value with
            {
                AccountName = value.AccountName.Trim().ToUpperInvariant(),
                Items = Array.AsReadOnly(value.Items.ToArray()),
                CarryPositions = Array.AsReadOnly(value.CarryPositions.ToArray()),
                Prices = Array.AsReadOnly(value.Prices.ToArray()),
            };
            if (copies.Any(previous => SameOwner(previous, copy)))
                throw new ArgumentException("Duplicate owner.", nameof(expectedListings));
            copies.Add(copy);
        }
        ExpectedListings = Array.AsReadOnly(copies.OrderBy(value => value.AccountName, StringComparer.Ordinal)
            .ThenBy(value => value.CharacterSlot).ToArray());
    }

    internal static bool SameOwner(LegacyAutoTradePersistedListing left, LegacyAutoTradePersistedListing right) =>
        left.AccountName == right.AccountName && left.CharacterSlot == right.CharacterSlot;

    internal LegacyAutoTradeCloseReadState Classify(IReadOnlyList<LegacyAutoTradePersistedListing> current)
    {
        var present = 0;
        foreach (var expected in ExpectedListings)
        {
            var matches = current.Where(value => SameOwner(value, expected)).ToArray();
            if (matches.Length > 1) return LegacyAutoTradeCloseReadState.Conflict;
            if (matches.Length == 0) continue;
            if (!LegacyAutoTradePurchaseCommitRules.SameListing(matches[0], expected)) return LegacyAutoTradeCloseReadState.Conflict;
            present++;
        }
        return present == 0 ? LegacyAutoTradeCloseReadState.AllAbsent : present == ExpectedListings.Count
            ? LegacyAutoTradeCloseReadState.AllUnchanged : LegacyAutoTradeCloseReadState.Conflict;
    }
}

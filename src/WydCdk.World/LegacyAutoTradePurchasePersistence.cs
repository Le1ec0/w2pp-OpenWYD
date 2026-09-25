using System.Buffers.Binary;
using WydCdk.Protocol;

namespace WydCdk.World;

public enum LegacyAutoTradePurchaseCommitResult
{
    Committed,
    InvalidRequest,
    AccountNotFound,
    CharacterNotAvailable,
    ListingNotFound,
    Conflict,
    NotSupported,
}

/// <summary>
/// Complete compare-and-swap request for one offline-shop purchase. The
/// buyer MOB is carried as an opaque legacy blob because the transaction must
/// preserve every field that was live when the pure plan was built. The runtime
/// expected MOB protects the WorldHub CAS; ExpectedPersistedBuyerMob protects
/// the storage CAS and may differ while unsaved runtime changes are in flight.
/// The seller side only changes the account-wide coin and one cargo item.
/// </summary>
public sealed record LegacyAutoTradePurchaseCommitRequest(
    string BuyerAccountName,
    int BuyerCharacterSlot,
    byte[] ExpectedBuyerMob,
    byte[] ExpectedPersistedBuyerMob,
    byte[] BuyerMob,
    short BuyerPositionX,
    short BuyerPositionY,
    byte[] BuyerMobExtra,
    string SellerAccountName,
    int SellerCharacterSlot,
    int ExpectedSellerCoin,
    LegacyAutoTradePurchasePlan Plan,
    LegacyAutoTradePersistedListing ExpectedListing,
    LegacyAutoTradePersistedListing UpdatedListing);

public interface ILegacyAutoTradePurchaseCommitStore
{
    ValueTask<LegacyAutoTradePurchaseCommitResult> TryCommitAsync(
        LegacyAutoTradePurchaseCommitRequest request,
        CancellationToken cancellationToken = default);
}

public static class LegacyAutoTradePurchaseCommitRules
{
    public static LegacyAutoTradePurchaseCommitResult Validate(LegacyAutoTradePurchaseCommitRequest? request)
    {
        if (request is null || request.Plan is null || request.ExpectedListing is null || request.UpdatedListing is null)
            return LegacyAutoTradePurchaseCommitResult.InvalidRequest;
        if (!IsAccountName(request.BuyerAccountName) || !IsAccountName(request.SellerAccountName) ||
            string.Equals(request.BuyerAccountName.Trim(), request.SellerAccountName.Trim(), StringComparison.OrdinalIgnoreCase))
            return LegacyAutoTradePurchaseCommitResult.InvalidRequest;
        if (!IsValidSlot(request.BuyerCharacterSlot) || !IsValidSlot(request.SellerCharacterSlot) ||
            request.ExpectedBuyerMob.Length != LegacyAccountSnapshot.CharacterStride ||
            request.ExpectedPersistedBuyerMob.Length != LegacyAccountSnapshot.CharacterStride ||
            request.BuyerMob.Length != LegacyAccountSnapshot.CharacterStride ||
            request.BuyerMobExtra.Length != LegacyAccountSnapshot.MobExtraStride ||
            request.Plan.BuyerCarry.Count != LegacyAccountSnapshot.MobCarryCount ||
            request.Plan.SellerCargo.Count != LegacyAutoTradeBook.CargoSlotCount ||
            request.Plan.BuyerDestinationSlot is < 0 or >= LegacyAutoTradePurchaseRules.BuyerCarryLimit ||
            request.Plan.SellerCargoPosition is < 0 or >= LegacyAutoTradeBook.CargoSlotCount ||
            request.ExpectedSellerCoin is < 0 or > LegacyAutoTradePurchaseRules.SellerCoinLimit ||
            request.Plan.SellerCoin is < 0 or > LegacyAutoTradePurchaseRules.SellerCoinLimit)
            return LegacyAutoTradePurchaseCommitResult.InvalidRequest;

        var expectedBuyerCoin = BinaryPrimitives.ReadInt32LittleEndian(request.ExpectedBuyerMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset));
        var buyerCoin = BinaryPrimitives.ReadInt32LittleEndian(request.BuyerMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset));
        var expectedSellerProceeds = (long)request.ExpectedSellerCoin + request.Plan.Settlement.SellerProceeds;
        if (buyerCoin != request.Plan.BuyerCoin ||
            expectedBuyerCoin - request.Plan.Settlement.ItemPrice != request.Plan.BuyerCoin ||
            expectedSellerProceeds != request.Plan.SellerCoin ||
            request.Plan.BuyerCoin < 0 ||
            LegacyItem.Read(request.BuyerMob.AsSpan(
                LegacyAccountSnapshot.MobCarryOffset + (request.Plan.BuyerDestinationSlot * LegacyItem.SizeInBytes),
                LegacyItem.SizeInBytes)) != request.Plan.PurchasedItem ||
            request.Plan.BuyerCarry[request.Plan.BuyerDestinationSlot] != request.Plan.PurchasedItem ||
            request.Plan.SellerCargo[request.Plan.SellerCargoPosition].Index != 0 ||
            request.ExpectedListing.Items.Count != AutoTradeListConfirmation.SlotCount ||
            request.ExpectedListing.CarryPositions.Count != AutoTradeListConfirmation.SlotCount ||
            request.ExpectedListing.Prices.Count != AutoTradeListConfirmation.SlotCount ||
            request.UpdatedListing.Items.Count != AutoTradeListConfirmation.SlotCount ||
            request.UpdatedListing.CarryPositions.Count != AutoTradeListConfirmation.SlotCount ||
            request.UpdatedListing.Prices.Count != AutoTradeListConfirmation.SlotCount ||
            request.ExpectedListing.AccountName != request.SellerAccountName.Trim().ToUpperInvariant() ||
            request.UpdatedListing.AccountName != request.ExpectedListing.AccountName ||
            request.ExpectedListing.CharacterSlot != request.SellerCharacterSlot ||
            request.UpdatedListing.CharacterSlot != request.SellerCharacterSlot ||
            !SameListingShape(request.ExpectedListing, request.UpdatedListing) ||
            !SameSnapshot(request.Plan.UpdatedListing, request.UpdatedListing))
            return LegacyAutoTradePurchaseCommitResult.InvalidRequest;

        var position = request.Plan.Position;
        if (position is < 0 or >= AutoTradeListConfirmation.SlotCount ||
            request.ExpectedListing.Items[position] != request.Plan.PurchasedItem ||
            request.ExpectedListing.CarryPositions[position] != request.Plan.SellerCargoPosition ||
            request.UpdatedListing.Items[position].Index != 0 ||
            request.UpdatedListing.CarryPositions[position] != -1 ||
            request.UpdatedListing.Prices[position] != 0)
            return LegacyAutoTradePurchaseCommitResult.InvalidRequest;

        return LegacyAutoTradePurchaseCommitResult.Committed;
    }

    public static bool SameListing(LegacyAutoTradePersistedListing left, LegacyAutoTradePersistedListing right)
    {
        if (!SameListingShape(left, right) ||
            left.PositionX != right.PositionX || left.PositionY != right.PositionY ||
            !string.Equals(left.Title, right.Title, StringComparison.Ordinal) || left.Tax != right.Tax)
            return false;

        return left.Items.SequenceEqual(right.Items) &&
            left.CarryPositions.SequenceEqual(right.CarryPositions) &&
            left.Prices.SequenceEqual(right.Prices);
    }

    private static bool SameListingShape(LegacyAutoTradePersistedListing left, LegacyAutoTradePersistedListing right) =>
        left.AccountName == right.AccountName && left.CharacterSlot == right.CharacterSlot &&
        left.PositionX == right.PositionX && left.PositionY == right.PositionY &&
        string.Equals(left.Title, right.Title, StringComparison.Ordinal) && left.Tax == right.Tax;

    private static bool SameSnapshot(LegacyAutoTradeSnapshot snapshot, LegacyAutoTradePersistedListing listing) =>
        snapshot.PositionX == listing.PositionX && snapshot.PositionY == listing.PositionY &&
        string.Equals(snapshot.Title, listing.Title, StringComparison.Ordinal) && snapshot.Tax == listing.Tax &&
        snapshot.Items.SequenceEqual(listing.Items) && snapshot.CarryPositions.SequenceEqual(listing.CarryPositions) &&
        snapshot.Prices.SequenceEqual(listing.Prices);

    private static bool IsValidSlot(int slot) => slot >= 0 && slot < LegacyAccountSnapshot.CharacterCount;

    private static bool IsAccountName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = value.Trim().ToUpperInvariant();
        return normalized.Length is > 0 and < 16 &&
            normalized.All(static character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-');
    }
}

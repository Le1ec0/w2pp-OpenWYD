using System.Buffers.Binary;
using WydCdk.Protocol;

namespace WydCdk.World;

public enum LegacyAutoTradeBuyerPurchaseResult
{
    Accepted,
    ParticipantNotFound,
    StateChanged,
    InvalidPlan,
    CoinChanged,
    DestinationOccupied,
}

/// <summary>In-memory buyer mutation produced only after the pure purchase plan was accepted.</summary>
public sealed record LegacyAutoTradeBuyerPurchaseOutcome(
    int ConnectionId,
    int DestinationSlot,
    LegacyItem PurchasedItem,
    int BuyerCoin,
    byte[] MobSnapshot,
    byte[] PreviousMob);

public static class LegacyAutoTradeRuntimePurchaseRules
{
    public static LegacyAutoTradeBuyerPurchaseResult ValidateBuyerPlan(
        LegacyAutoTradePurchasePlan? plan,
        ReadOnlySpan<byte> mob,
        out int expectedCoin)
    {
        expectedCoin = 0;
        if (plan is null ||
            mob.Length < LegacyAccountSnapshot.MobCarryOffset + (LegacyAccountSnapshot.MobCarryCount * LegacyItem.SizeInBytes) ||
            plan.BuyerCarry.Count != LegacyAccountSnapshot.MobCarryCount ||
            plan.BuyerDestinationSlot is < 0 or >= LegacyAutoTradePurchaseRules.BuyerCarryLimit ||
            plan.PurchasedItem.Index == 0 ||
            plan.BuyerCarry[plan.BuyerDestinationSlot] != plan.PurchasedItem)
            return LegacyAutoTradeBuyerPurchaseResult.InvalidPlan;

        try
        {
            expectedCoin = checked(plan.BuyerCoin + plan.Settlement.ItemPrice);
        }
        catch (OverflowException)
        {
            return LegacyAutoTradeBuyerPurchaseResult.InvalidPlan;
        }

        var currentCoin = BinaryPrimitives.ReadInt32LittleEndian(mob[LegacyAccountSnapshot.MobCoinOffset..]);
        if (currentCoin != expectedCoin)
            return LegacyAutoTradeBuyerPurchaseResult.CoinChanged;

        var currentItem = LegacyItem.Read(mob.Slice(
            LegacyAccountSnapshot.MobCarryOffset + (plan.BuyerDestinationSlot * LegacyItem.SizeInBytes),
            LegacyItem.SizeInBytes));
        return currentItem.Index == 0
            ? LegacyAutoTradeBuyerPurchaseResult.Accepted
            : LegacyAutoTradeBuyerPurchaseResult.DestinationOccupied;
    }
}

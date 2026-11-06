using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Builds a read-only Tiny combine plan from the request and the character's
/// current authoritative carry snapshot. Mutation and wire relays are separate.
/// </summary>
public static class LegacyTinyCombinePlanBuilder
{
    public static LegacyTinyCombinePreparationResult TryBuild(
        LegacyTinyCombinePreparationContext context,
        out LegacyTinyCombinePlan? plan)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.RequestedItems);
        ArgumentNullException.ThrowIfNull(context.InventoryPositions);
        ArgumentNullException.ThrowIfNull(context.AuthoritativeCarry);

        plan = null;
        if (context.RequestedItems.Count != LegacyTinyCombineRules.CombineItemCount ||
            context.InventoryPositions.Count != LegacyTinyCombineRules.CombineItemCount)
            return LegacyTinyCombinePreparationResult.InvalidRequestShape;

        if (context.MaxCarry < 0 || context.MaxCarry > LegacyAccountSnapshot.MobCarryCount - 4 ||
            context.AuthoritativeCarry.Count < context.MaxCarry ||
            context.ExpectedMob.Length < LegacyAccountSnapshot.MobCarryOffset +
                (LegacyAccountSnapshot.MobCarryCount * LegacyItem.SizeInBytes))
            return LegacyTinyCombinePreparationResult.CarryUnavailable;

        for (var index = 0; index < LegacyTinyCombineRules.CombineItemCount; index++)
        {
            var requestedItem = context.RequestedItems[index];
            if (requestedItem.Index == 0)
                continue;

            var inventoryPosition = context.InventoryPositions[index];
            if (inventoryPosition < 0 || inventoryPosition >= context.MaxCarry)
                return LegacyTinyCombinePreparationResult.InvalidInventoryPosition;

            if (context.AuthoritativeCarry[inventoryPosition] != requestedItem)
                return LegacyTinyCombinePreparationResult.ItemChanged;
        }

        if (context.Coin < LegacyTinyCombineRules.CoinCost)
            return LegacyTinyCombinePreparationResult.InsufficientCoin;
        if (context.ItemData is null)
            return LegacyTinyCombinePreparationResult.ItemDataUnavailable;
        if (!LegacyTinyCombineRules.TryGetSuccessThreshold(
                context.RequestedItems, context.ItemData, context.TinyBase, out var successThreshold))
            return LegacyTinyCombinePreparationResult.InvalidRecipe;

        plan = new LegacyTinyCombinePlan(
            context.ConnectionId,
            successThreshold,
            context.Coin,
            Array.AsReadOnly(context.RequestedItems.ToArray()),
            Array.AsReadOnly(context.InventoryPositions.ToArray()),
            context.ExpectedMob.ToArray());
        return LegacyTinyCombinePreparationResult.Accepted;
    }
}

public sealed record LegacyTinyCombinePreparationContext(
    int ConnectionId,
    IReadOnlyList<LegacyItem> RequestedItems,
    IReadOnlyList<sbyte> InventoryPositions,
    IReadOnlyList<LegacyItem> AuthoritativeCarry,
    int MaxCarry,
    int Coin,
    LegacyItemDataTable? ItemData,
    int TinyBase,
    ReadOnlyMemory<byte> ExpectedMob);

public sealed record LegacyTinyCombinePlan(
    int ConnectionId,
    int SuccessThreshold,
    int CoinBefore,
    IReadOnlyList<LegacyItem> RequestedItems,
    IReadOnlyList<sbyte> InventoryPositions,
    ReadOnlyMemory<byte> ExpectedMob);

public enum LegacyTinyCombinePreparationResult
{
    Accepted,
    ParticipantNotFound,
    CarryUnavailable,
    InvalidRequestShape,
    InvalidInventoryPosition,
    ItemChanged,
    InsufficientCoin,
    ItemDataUnavailable,
    InvalidRecipe,
}

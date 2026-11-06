using System.Buffers.Binary;
using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>Builds the deterministic inventory and coin result of a prepared 7.69 Tiny combine.</summary>
public static class LegacyTinyCombineOutcomeBuilder
{
    public static LegacyTinyCombineOutcomeBuildResult TryBuild(
        LegacyTinyCombinePlan? plan,
        int rawRoll,
        bool localServer,
        out LegacyTinyCombineOutcome? outcome)
    {
        outcome = null;
        if (rawRoll is < 0 or >= 115)
            return LegacyTinyCombineOutcomeBuildResult.InvalidRoll;

        if (plan is null ||
            plan.RequestedItems is null || plan.InventoryPositions is null ||
            plan.RequestedItems.Count != LegacyTinyCombineRules.CombineItemCount ||
            plan.InventoryPositions.Count != LegacyTinyCombineRules.CombineItemCount ||
            plan.RequestedItems[0].Index == 0 ||
            plan.RequestedItems[1].Index == 0 ||
            plan.RequestedItems[2].Index == 0 ||
            plan.ExpectedMob.Length < LegacyAccountSnapshot.MobCarryOffset +
                (LegacyAccountSnapshot.MobCarryCount * LegacyItem.SizeInBytes) ||
            plan.SuccessThreshold <= 0 ||
            plan.CoinBefore < LegacyTinyCombineRules.CoinCost)
            return LegacyTinyCombineOutcomeBuildResult.InvalidPlan;

        for (var index = 0; index < LegacyTinyCombineRules.CombineItemCount; index++)
        {
            if (plan.RequestedItems[index].Index == 0)
                continue;

            var position = plan.InventoryPositions[index];
            if (position < 0 || position >= LegacyAccountSnapshot.MobCarryCount)
                return LegacyTinyCombineOutcomeBuildResult.InvalidPlan;
        }

        // The handler clears the occupied catalyst slots before it draws.
        // Keeping this order also preserves legacy behavior for aliased positions.
        var mob = plan.ExpectedMob.ToArray();
        var itemUpdates = new List<LegacyTinyCombineItemUpdate>();
        for (var index = 2; index < LegacyTinyCombineRules.CombineItemCount; index++)
        {
            if (plan.RequestedItems[index].Index == 0)
                continue;

            ClearCarrySlot(mob, plan.InventoryPositions[index]);
            itemUpdates.Add(CaptureItemUpdate(mob, plan.InventoryPositions[index]));
        }

        var normalizedRoll = rawRoll >= 100 ? rawRoll - 15 : rawRoll;
        var succeeded = normalizedRoll <= plan.SuccessThreshold || localServer;
        if (succeeded)
        {
            var targetOffset = CarryItemOffset(plan.InventoryPositions[0]);
            var target = LegacyItem.Read(mob.AsSpan(targetOffset, LegacyItem.SizeInBytes));
            var attribute = plan.RequestedItems[1];
            target = target with
            {
                Effect1 = attribute.Effect1,
                Value1 = attribute.Value1,
                Effect2 = attribute.Effect2,
                Value2 = attribute.Value2,
                Effect3 = attribute.Effect3,
                Value3 = attribute.Value3,
            };
            target.Write(mob.AsSpan(targetOffset, LegacyItem.SizeInBytes));

            ClearCarrySlot(mob, plan.InventoryPositions[1]);
            itemUpdates.Add(CaptureItemUpdate(mob, plan.InventoryPositions[1]));
            ClearCarrySlot(mob, plan.InventoryPositions[2]);
            itemUpdates.Add(CaptureItemUpdate(mob, plan.InventoryPositions[2]));

            target = LegacyItem.Read(mob.AsSpan(targetOffset, LegacyItem.SizeInBytes));
            LegacyItemRefinementMath.SetSanctuary(target, sanctuary: 7, success: 0)
                .Write(mob.AsSpan(targetOffset, LegacyItem.SizeInBytes));
            itemUpdates.Add(CaptureItemUpdate(mob, plan.InventoryPositions[0]));
        }
        else
        {
            // The old handler repeats the clear and SendItem for position 2 on failure.
            ClearCarrySlot(mob, plan.InventoryPositions[2]);
            itemUpdates.Add(CaptureItemUpdate(mob, plan.InventoryPositions[2]));
        }

        var coinAfter = plan.CoinBefore - LegacyTinyCombineRules.CoinCost;
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), coinAfter);
        outcome = new LegacyTinyCombineOutcome(
            plan.ConnectionId,
            succeeded,
            rawRoll,
            normalizedRoll,
            plan.SuccessThreshold,
            plan.CoinBefore,
            coinAfter,
            Array.AsReadOnly(itemUpdates.Select(static update => update.Position).ToArray()),
            Array.AsReadOnly(itemUpdates.ToArray()),
            plan.ExpectedMob.ToArray(),
            mob);
        return LegacyTinyCombineOutcomeBuildResult.Accepted;
    }

    private static void ClearCarrySlot(byte[] mob, sbyte position) =>
        mob.AsSpan(CarryItemOffset(position), LegacyItem.SizeInBytes).Clear();

    private static LegacyTinyCombineItemUpdate CaptureItemUpdate(byte[] mob, sbyte position) =>
        new(position, LegacyItem.Read(mob.AsSpan(CarryItemOffset(position), LegacyItem.SizeInBytes)));

    private static int CarryItemOffset(sbyte position) =>
        LegacyAccountSnapshot.MobCarryOffset + (position * LegacyItem.SizeInBytes);
}

public sealed record LegacyTinyCombineOutcome(
    int ConnectionId,
    bool Succeeded,
    int RawRoll,
    int NormalizedRoll,
    int SuccessThreshold,
    int CoinBefore,
    int CoinAfter,
    IReadOnlyList<sbyte> ItemUpdatePositions,
    IReadOnlyList<LegacyTinyCombineItemUpdate> ItemUpdates,
    ReadOnlyMemory<byte> ExpectedMob,
    ReadOnlyMemory<byte> MobSnapshot)
{
    public int CompletionResult => Succeeded ? 1 : 2;
    public LegacyGuildTreasuryNpcUpdate? GuildTaxNpcUpdate { get; init; }
}

public sealed record LegacyTinyCombineItemUpdate(sbyte Position, LegacyItem Item);

public sealed record LegacyGuildTreasuryNpcUpdate(
    int ConnectionId,
    ReadOnlyMemory<byte> PreviousMobSnapshot,
    ReadOnlyMemory<byte> MobSnapshot);

public enum LegacyTinyCombineOutcomeBuildResult
{
    Accepted,
    InvalidPlan,
    InvalidRoll,
}

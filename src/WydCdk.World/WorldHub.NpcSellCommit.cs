using System.Buffers.Binary;
using WydCdk.Protocol;

namespace WydCdk.World;

public sealed partial class WorldHub
{
    private const long LegacyNpcSellCityTreasuryExperienceLimit = 200_000_000_000L;
    private const long LegacyNpcSellGlobalTreasuryExperienceLimit = 20_000_000_000_000L;

    /// <summary>
    /// Applies the WorldHub-owned portion of a prepared NPC sale. Persistent
    /// account writes and client relays remain the caller's later transaction
    /// stages; this method does not dispatch or persist them.
    /// </summary>
    public LegacyNpcSellCommitResult TryCommitNpcSell(
        LegacyNpcSellCompletionPlan? plan,
        out LegacyNpcSellCompletionOutcome? outcome)
    {
        outcome = null;
        if (plan is null)
            return LegacyNpcSellCommitResult.InvalidPlan;
        if (!IsValidNpcSellCompletionPlan(plan))
            return LegacyNpcSellCommitResult.InvalidPlan;

        lock (gate)
        {
            if (!participants.TryGetValue(plan.ConnectionId, out var participant))
                return LegacyNpcSellCommitResult.ParticipantNotFound;

            if (participant.PositionX != plan.SellerPositionX ||
                participant.PositionY != plan.SellerPositionY ||
                participant.Coin != plan.CoinBefore ||
                !participant.Mob.AsSpan().SequenceEqual(plan.ExpectedMob.Span) ||
                !participant.RecycleBin.SequenceEqual(plan.ExpectedRecycleBin))
                return LegacyNpcSellCommitResult.StateChanged;

            if (plan.Source.Kind == LegacyNpcSellSourceKind.Equipment &&
                (plan.Source.Position >= participant.ClientEquipment.Length ||
                 participant.ClientEquipment[plan.Source.Position] != plan.Sale.Item))
                return LegacyNpcSellCommitResult.StateChanged;

            if (!TryResolveNpcSellTargetLocked(plan, out var currentLocation) ||
                currentLocation != plan.Location)
                return LegacyNpcSellCommitResult.TargetChanged;

            var previousRuntimeParryAdjustment = participant.RuntimeParryAdjustment;
            participant.Mob = plan.UpdatedMob.ToArray();
            participant.Coin = plan.CoinAfter;
            participant.RecycleBin = plan.UpdatedRecycleBin.ToArray();
            if (plan.Source.Kind == LegacyNpcSellSourceKind.Equipment)
            {
                participant.ClientEquipment[plan.Source.Position] = default;
                participant.RuntimeParryAdjustment = 0;
            }
            var guildTaxNpcUpdates = TryApplyNpcSellTaxExperienceLocked(plan.Sale);
            outcome = new LegacyNpcSellCompletionOutcome(
                plan,
                previousRuntimeParryAdjustment,
                participant.RuntimeParryAdjustment,
                guildTaxNpcUpdates);
            return LegacyNpcSellCommitResult.Committed;
        }
    }

    /// <summary>
    /// Restores the previous MOB, equipment mirror, Coin, parry adjustment,
    /// and recycle-bin state only if no later WorldHub mutation replaced it.
    /// </summary>
    public bool TryRollbackNpcSell(LegacyNpcSellCompletionOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        var plan = outcome.Plan;
        if (!IsValidNpcSellCompletionPlan(plan))
            return false;

        lock (gate)
        {
            if (!participants.TryGetValue(plan.ConnectionId, out var participant) ||
                participant.Coin != plan.CoinAfter ||
                !participant.Mob.AsSpan().SequenceEqual(plan.UpdatedMob.Span) ||
                !participant.RecycleBin.SequenceEqual(plan.UpdatedRecycleBin) ||
                participant.RuntimeParryAdjustment != outcome.RuntimeParryAdjustmentAfter ||
                (plan.Source.Kind == LegacyNpcSellSourceKind.Equipment &&
                 (plan.Source.Position >= participant.ClientEquipment.Length ||
                  participant.ClientEquipment[plan.Source.Position] != default)))
                return false;

            foreach (var update in outcome.GuildTaxNpcUpdates)
                if (!guildImpostoIds.ContainsValue(update.ConnectionId) ||
                    !npcs.TryGetValue(update.ConnectionId, out var treasuryNpc) ||
                    !treasuryNpc.MobSnapshot.AsSpan().SequenceEqual(update.MobSnapshot.Span))
                    return false;

            participant.Mob = plan.ExpectedMob.ToArray();
            participant.Coin = plan.CoinBefore;
            participant.RecycleBin = plan.ExpectedRecycleBin.ToArray();
            if (plan.Source.Kind == LegacyNpcSellSourceKind.Equipment)
            {
                participant.ClientEquipment[plan.Source.Position] = plan.Sale.Item;
                participant.RuntimeParryAdjustment = outcome.RuntimeParryAdjustmentBefore;
            }
            foreach (var update in outcome.GuildTaxNpcUpdates)
                npcs[update.ConnectionId] = npcs[update.ConnectionId] with
                {
                    MobSnapshot = update.PreviousMobSnapshot.ToArray(),
                };
            return true;
        }
    }

    private IReadOnlyList<LegacyGuildTreasuryNpcUpdate> TryApplyNpcSellTaxExperienceLocked(
        LegacyNpcSellPreparedSale sale)
    {
        if (guildZones is null ||
            !LegacyNpcSellTaxExperienceMath.TryCalculate(sale.GrossPrice, sale.NetPrice, out var taxExperience) ||
            taxExperience <= 0)
            return Array.Empty<LegacyGuildTreasuryNpcUpdate>();

        var updates = new Dictionary<int, LegacyGuildTreasuryNpcUpdate>();
        var updateOrder = new List<int>(capacity: 2);
        ApplyRecipient(sale.NpcVillage, LegacyNpcSellCityTreasuryExperienceLimit);
        ApplyRecipient(4, LegacyNpcSellGlobalTreasuryExperienceLimit);
        return Array.AsReadOnly(updateOrder.Select(connectionId => updates[connectionId]).ToArray());

        void ApplyRecipient(int village, long experienceLimit)
        {
            if (!guildImpostoIds.TryGetValue(village, out var treasuryId) ||
                treasuryId < LegacyMobAiPlanner.MaximumPlayerId ||
                treasuryId >= LegacyMobAiPlanner.MaximumMobId ||
                !npcs.TryGetValue(treasuryId, out var treasuryNpc) ||
                treasuryNpc.Mode == LegacyNpcMode.Empty ||
                treasuryNpc.MobSnapshot.Length < LegacyAccountSnapshot.MobExperienceOffset + sizeof(long))
                return;

            var currentExperience = BinaryPrimitives.ReadInt64LittleEndian(
                treasuryNpc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobExperienceOffset));
            if (currentExperience >= experienceLimit)
                return;

            var previousMob = updates.TryGetValue(treasuryId, out var existingUpdate)
                ? existingUpdate.PreviousMobSnapshot.ToArray()
                : treasuryNpc.MobSnapshot.ToArray();
            var updatedMob = treasuryNpc.MobSnapshot.ToArray();
            BinaryPrimitives.WriteInt64LittleEndian(
                updatedMob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset),
                checked(currentExperience + taxExperience));
            npcs[treasuryId] = treasuryNpc with { MobSnapshot = updatedMob };

            if (existingUpdate is null)
                updateOrder.Add(treasuryId);
            updates[treasuryId] = new(treasuryId, previousMob, updatedMob);
        }
    }

    private bool TryResolveNpcSellTargetLocked(
        LegacyNpcSellCompletionPlan plan,
        out LegacyNpcSellLocation? location)
    {
        location = null;
        var expected = plan.Target;
        if (!TryGetMobSnapshot(expected.ConnectionId, out var current) ||
            current is null ||
            current.Kind != expected.Kind ||
            current.PositionX != expected.PositionX ||
            current.PositionY != expected.PositionY ||
            current.MobSnapshot.Length <= LegacyAccountSnapshot.MobMerchantOffset ||
            current.MobSnapshot[LegacyAccountSnapshot.MobMerchantOffset] != expected.Merchant)
            return false;

        var village = LegacyMovementMapRules.FindGuildZone(current.PositionX, current.PositionY);
        var cityTax = village is >= 0 and < LegacyGuildZoneState.ZoneCount
            ? guildZones?.CityTaxes[village] ?? LegacyAutoTradeLocationRules.DefaultCityTax
            : (int?)null;
        location = new(village, cityTax);
        return true;
    }

    private static bool IsValidNpcSellCompletionPlan(LegacyNpcSellCompletionPlan plan)
    {
        if (plan.ConnectionId is <= 0 or >= 1_000 ||
            plan.Source.Kind is not (LegacyNpcSellSourceKind.Equipment or LegacyNpcSellSourceKind.Carry or LegacyNpcSellSourceKind.Cargo) ||
            !plan.Source.IsValid ||
            plan.Sale.NetPrice < 0 ||
            !LegacyNpcSellTaxExperienceMath.TryCalculate(
                plan.Sale.GrossPrice,
                plan.Sale.NetPrice,
                out _) ||
            plan.Location.Village < 0 ||
            plan.Location.Village >= LegacyGuildZoneState.ZoneCount ||
            plan.Location.CityTax is null ||
            plan.Location.Village != plan.Sale.NpcVillage ||
            plan.Target.ConnectionId <= 0 ||
            plan.ExpectedMob.Length != LegacyAccountSnapshot.CharacterStride ||
            plan.UpdatedMob.Length != LegacyAccountSnapshot.CharacterStride ||
            plan.ExpectedRecycleBin.Count != LegacyRecycleBinRules.Capacity ||
            plan.UpdatedRecycleBin.Count != LegacyRecycleBinRules.Capacity ||
            plan.CoinBefore != BinaryPrimitives.ReadInt32LittleEndian(
                plan.ExpectedMob.Span[LegacyAccountSnapshot.MobCoinOffset..]) ||
            plan.CoinAfter != BinaryPrimitives.ReadInt32LittleEndian(
                plan.UpdatedMob.Span[LegacyAccountSnapshot.MobCoinOffset..]))
            return false;

        var verifiedSource = LegacyNpcSellSourceRules.Resolve(
            (short)plan.Source.Kind.Value,
            plan.Source.Position,
            tradeMode: false);
        if (verifiedSource != plan.Source)
            return false;

        var itemOffset = plan.Source.Kind switch
        {
            LegacyNpcSellSourceKind.Equipment => LegacyAccountSnapshot.MobEquipmentOffset + plan.Source.Position * LegacyItem.SizeInBytes,
            LegacyNpcSellSourceKind.Carry => LegacyAccountSnapshot.MobCarryOffset + plan.Source.Position * LegacyItem.SizeInBytes,
            _ => -1,
        };
        if (plan.Source.Kind == LegacyNpcSellSourceKind.Cargo)
        {
            var expectedCargo = plan.ExpectedCargo;
            var updatedCargo = plan.UpdatedCargo;
            if (expectedCargo is not { } cargoBefore || updatedCargo is not { } cargoAfter ||
                cargoBefore.Length != LegacyAccountSnapshot.CargoCount * LegacyItem.SizeInBytes ||
                cargoAfter.Length != cargoBefore.Length || plan.CargoRevision is null or < 0)
                return false;
            var cargoOffset = plan.Source.Position * LegacyItem.SizeInBytes;
            if (LegacyItem.Read(cargoBefore.Span.Slice(cargoOffset, LegacyItem.SizeInBytes)) != plan.Sale.Item ||
                LegacyItem.Read(cargoAfter.Span.Slice(cargoOffset, LegacyItem.SizeInBytes)) != default)
                return false;
            var reconstructedCargo = cargoBefore.ToArray();
            default(LegacyItem).Write(reconstructedCargo.AsSpan(cargoOffset, LegacyItem.SizeInBytes));
            if (!reconstructedCargo.AsSpan().SequenceEqual(cargoAfter.Span))
                return false;
        }
        else if (plan.ExpectedCargo is not null || plan.UpdatedCargo is not null || plan.CargoRevision is not null ||
                 itemOffset < 0 || itemOffset > LegacyAccountSnapshot.CharacterStride - LegacyItem.SizeInBytes ||
                  LegacyItem.Read(plan.ExpectedMob.Span.Slice(itemOffset, LegacyItem.SizeInBytes)) != plan.Sale.Item ||
                  LegacyItem.Read(plan.UpdatedMob.Span.Slice(itemOffset, LegacyItem.SizeInBytes)) != default)
            return false;

        var sum = (long)plan.CoinBefore + plan.Sale.NetPrice;
        if (sum > int.MaxValue)
            return false;
        var expectedCoinAfter = sum < LegacyNpcSellCompletionRules.MaximumCoin
            ? (int)sum
            : LegacyNpcSellCompletionRules.MaximumCoin;
        if (plan.CoinAfter != expectedCoinAfter)
            return false;

        var reconstructedMob = plan.ExpectedMob.ToArray();
        if (plan.Source.Kind != LegacyNpcSellSourceKind.Cargo)
            default(LegacyItem).Write(reconstructedMob.AsSpan(itemOffset, LegacyItem.SizeInBytes));
        BinaryPrimitives.WriteInt32LittleEndian(
            reconstructedMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset),
            expectedCoinAfter);
        if (!reconstructedMob.AsSpan().SequenceEqual(plan.UpdatedMob.Span))
            return false;

        return true;
    }
}

public enum LegacyNpcSellCommitResult
{
    Committed,
    ParticipantNotFound,
    StateChanged,
    TargetChanged,
    InvalidPlan,
}

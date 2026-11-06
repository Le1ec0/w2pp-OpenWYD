using System.Buffers.Binary;
using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Builds a copied before/after snapshot for the WorldHub-owned portion of a
/// prepared NPC sale. For account Cargo it also carries the expected/new Cargo
/// bytes and owner revision so the Host can persist through the account CAS.
/// </summary>
public static class LegacyNpcSellCompletionRules
{
    public const int MaximumCoin = 2_000_000_000;

    public static LegacyNpcSellCompletionBuildStatus TryBuild(
        LegacyNpcSellPreparationOutcome preparation,
        int connectionId,
        IReadOnlyList<LegacyRecycleBinEntry> recycleBinSnapshot,
        uint randomValue,
        out LegacyNpcSellCompletionPlan? plan)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        ArgumentNullException.ThrowIfNull(recycleBinSnapshot);
        plan = null;

        if (preparation.Status != LegacyNpcSellPreparationStatus.Evaluated ||
            preparation.Flow is not { Stage: LegacyNpcSellFlowStage.ReadyForSaleMutation } flow ||
            flow.PreparedSale is not { } sale ||
            flow.Source is not { IsValid: true } source ||
            flow.Item is not { IsSellable: true } ||
            flow.Target is not { IsAccepted: true } ||
            flow.Price is not { IsSuccess: true } price ||
            preparation.State is not { } state ||
            preparation.Location is not { } location)
            return LegacyNpcSellCompletionBuildStatus.PreparationNotReady;

        if (source != state.Source ||
            state.SelectedItem is not { } selectedItem ||
            selectedItem != sale.Item ||
            state.Target is not { } target ||
            target.ConnectionId != state.TargetId ||
            target.MobSnapshot.Length <= LegacyAccountSnapshot.MobMerchantOffset ||
            sale.NpcVillage != location.Village ||
            location.Village < 0 || location.Village >= LegacyGuildZoneState.ZoneCount ||
            location.CityTax is null ||
            sale.GrossPrice < sale.NetPrice ||
            sale.NetPrice < 0 ||
            !price.IsSuccess ||
            price.Amount != sale.NetPrice ||
            price.GrossAmount != sale.GrossPrice ||
            price.NpcVillage != sale.NpcVillage ||
            sale.RequiresRecycleBinInsertion != (sale.NpcVillage is >= 0 and <= 5))
            return LegacyNpcSellCompletionBuildStatus.InvalidPreparation;

        if (source.Kind is not (LegacyNpcSellSourceKind.Equipment or LegacyNpcSellSourceKind.Carry or LegacyNpcSellSourceKind.Cargo) ||
            connectionId is <= 0 or >= 1_000 ||
            state.SellerMobSnapshot.Length != LegacyAccountSnapshot.CharacterStride ||
            recycleBinSnapshot.Count != LegacyRecycleBinRules.Capacity)
            return LegacyNpcSellCompletionBuildStatus.InvalidSnapshot;

        if (source.Kind == LegacyNpcSellSourceKind.Cargo &&
            (state.CargoSnapshot is not { Count: LegacyAccountSnapshot.CargoCount } ||
             state.CargoRevision is null))
            return LegacyNpcSellCompletionBuildStatus.InvalidSnapshot;

        var verifiedSource = LegacyNpcSellSourceRules.Resolve(
            (short)source.Kind.Value,
            source.Position,
            tradeMode: false);
        if (verifiedSource != source)
            return LegacyNpcSellCompletionBuildStatus.InvalidPreparation;

        var itemOffset = source.Kind switch
        {
            LegacyNpcSellSourceKind.Equipment => LegacyAccountSnapshot.MobEquipmentOffset + source.Position * LegacyItem.SizeInBytes,
            LegacyNpcSellSourceKind.Carry => LegacyAccountSnapshot.MobCarryOffset + source.Position * LegacyItem.SizeInBytes,
            _ => -1,
        };
        if (source.Kind == LegacyNpcSellSourceKind.Cargo)
        {
            if (state.CargoSnapshot![source.Position] != sale.Item)
                return LegacyNpcSellCompletionBuildStatus.SelectedItemChanged;
        }
        else if (itemOffset < 0 || itemOffset > state.SellerMobSnapshot.Length - LegacyItem.SizeInBytes ||
                 LegacyItem.Read(state.SellerMobSnapshot.AsSpan(itemOffset, LegacyItem.SizeInBytes)) != sale.Item)
            return LegacyNpcSellCompletionBuildStatus.SelectedItemChanged;

        var coinBefore = BinaryPrimitives.ReadInt32LittleEndian(
            state.SellerMobSnapshot.AsSpan(LegacyAccountSnapshot.MobCoinOffset));
        var coinSum = (long)coinBefore + sale.NetPrice;
        if (coinSum > int.MaxValue)
            return LegacyNpcSellCompletionBuildStatus.CoinOverflowUnverified;

        var coinAfter = coinSum < MaximumCoin ? (int)coinSum : MaximumCoin;
        var updatedMob = state.SellerMobSnapshot.ToArray();
        if (source.Kind != LegacyNpcSellSourceKind.Cargo)
            default(LegacyItem).Write(updatedMob.AsSpan(itemOffset, LegacyItem.SizeInBytes));
        BinaryPrimitives.WriteInt32LittleEndian(
            updatedMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset),
            coinAfter);

        byte[]? expectedCargo = null;
        byte[]? updatedCargo = null;
        if (source.Kind == LegacyNpcSellSourceKind.Cargo)
        {
            expectedCargo = new byte[LegacyAccountSnapshot.CargoCount * LegacyItem.SizeInBytes];
            for (var index = 0; index < state.CargoSnapshot!.Count; index++)
                state.CargoSnapshot[index].Write(expectedCargo.AsSpan(index * LegacyItem.SizeInBytes, LegacyItem.SizeInBytes));
            updatedCargo = expectedCargo.ToArray();
            default(LegacyItem).Write(updatedCargo.AsSpan(source.Position * LegacyItem.SizeInBytes, LegacyItem.SizeInBytes));
        }

        var expectedRecycleBin = recycleBinSnapshot.ToArray();
        var updatedRecycleBin = expectedRecycleBin;
        if (sale.RequiresRecycleBinInsertion)
        {
            var insertion = LegacyRecycleBinRules.InsertSale(
                connectionId,
                LoginSessionState.Playing,
                expectedRecycleBin,
                sale.Item,
                sale.NetPrice,
                randomValue);
            if (!insertion.WasInserted)
                return LegacyNpcSellCompletionBuildStatus.RecycleBinInsertionRejected;

            updatedRecycleBin = insertion.Entries.ToArray();
        }

        var expectedTarget = new LegacyNpcSellTargetExpectation(
            target.ConnectionId,
            target.Kind,
            target.PositionX,
            target.PositionY,
            target.MobSnapshot[LegacyAccountSnapshot.MobMerchantOffset]);
        plan = new LegacyNpcSellCompletionPlan(
            connectionId,
            source,
            sale,
            expectedTarget,
            location,
            state.SellerPositionX,
            state.SellerPositionY,
            state.SellerMobSnapshot,
            updatedMob,
            coinBefore,
            coinAfter,
            expectedRecycleBin,
            updatedRecycleBin,
            expectedCargo,
            updatedCargo,
            source.Kind == LegacyNpcSellSourceKind.Cargo ? state.CargoRevision : null);
        return LegacyNpcSellCompletionBuildStatus.Ready;
    }
}

public enum LegacyNpcSellCompletionBuildStatus
{
    Ready,
    PreparationNotReady,
    InvalidPreparation,
    InvalidSnapshot,
    SelectedItemChanged,
    CargoSourceUnsupported,
    CoinOverflowUnverified,
    RecycleBinInsertionRejected,
}

public sealed record LegacyNpcSellTargetExpectation(
    int ConnectionId,
    LegacyWorldMobKind Kind,
    short PositionX,
    short PositionY,
    byte Merchant);

/// <summary>Immutable copied state required to conditionally commit a sale.</summary>
public sealed class LegacyNpcSellCompletionPlan
{
    internal LegacyNpcSellCompletionPlan(
        int connectionId,
        LegacyNpcSellSourceResolution source,
        LegacyNpcSellPreparedSale sale,
        LegacyNpcSellTargetExpectation target,
        LegacyNpcSellLocation location,
        short sellerPositionX,
        short sellerPositionY,
        ReadOnlyMemory<byte> expectedMob,
        ReadOnlyMemory<byte> updatedMob,
        int coinBefore,
        int coinAfter,
        IReadOnlyList<LegacyRecycleBinEntry> expectedRecycleBin,
        IReadOnlyList<LegacyRecycleBinEntry> updatedRecycleBin,
        byte[]? expectedCargo,
        byte[]? updatedCargo,
        long? cargoRevision)
    {
        ConnectionId = connectionId;
        Source = source;
        Sale = sale;
        Target = target;
        Location = location;
        SellerPositionX = sellerPositionX;
        SellerPositionY = sellerPositionY;
        ExpectedMob = expectedMob.ToArray();
        UpdatedMob = updatedMob.ToArray();
        CoinBefore = coinBefore;
        CoinAfter = coinAfter;
        ExpectedRecycleBin = Array.AsReadOnly(expectedRecycleBin.ToArray());
        UpdatedRecycleBin = Array.AsReadOnly(updatedRecycleBin.ToArray());
        ExpectedCargo = expectedCargo is null
            ? (ReadOnlyMemory<byte>?)null
            : new ReadOnlyMemory<byte>(expectedCargo.ToArray());
        UpdatedCargo = updatedCargo is null
            ? (ReadOnlyMemory<byte>?)null
            : new ReadOnlyMemory<byte>(updatedCargo.ToArray());
        CargoRevision = cargoRevision;
    }

    public int ConnectionId { get; }
    public LegacyNpcSellSourceResolution Source { get; }
    public LegacyNpcSellPreparedSale Sale { get; }
    public LegacyNpcSellTargetExpectation Target { get; }
    public LegacyNpcSellLocation Location { get; }
    public short SellerPositionX { get; }
    public short SellerPositionY { get; }
    public ReadOnlyMemory<byte> ExpectedMob { get; }
    public ReadOnlyMemory<byte> UpdatedMob { get; }
    public int CoinBefore { get; }
    public int CoinAfter { get; }
    public IReadOnlyList<LegacyRecycleBinEntry> ExpectedRecycleBin { get; }
    public IReadOnlyList<LegacyRecycleBinEntry> UpdatedRecycleBin { get; }
    public ReadOnlyMemory<byte>? ExpectedCargo { get; }
    public ReadOnlyMemory<byte>? UpdatedCargo { get; }
    public long? CargoRevision { get; }
}

public sealed class LegacyNpcSellCompletionOutcome
{
    internal LegacyNpcSellCompletionOutcome(
        LegacyNpcSellCompletionPlan plan,
        int runtimeParryAdjustmentBefore,
        int runtimeParryAdjustmentAfter,
        IReadOnlyList<LegacyGuildTreasuryNpcUpdate> guildTaxNpcUpdates)
    {
        Plan = plan;
        RuntimeParryAdjustmentBefore = runtimeParryAdjustmentBefore;
        RuntimeParryAdjustmentAfter = runtimeParryAdjustmentAfter;
        GuildTaxNpcUpdates = Array.AsReadOnly(guildTaxNpcUpdates
            .Select(static update => new LegacyGuildTreasuryNpcUpdate(
                update.ConnectionId,
                update.PreviousMobSnapshot.ToArray(),
                update.MobSnapshot.ToArray()))
            .ToArray());
    }

    public LegacyNpcSellCompletionPlan Plan { get; }
    public IReadOnlyList<LegacyGuildTreasuryNpcUpdate> GuildTaxNpcUpdates { get; }
    internal int RuntimeParryAdjustmentBefore { get; }
    internal int RuntimeParryAdjustmentAfter { get; }
}

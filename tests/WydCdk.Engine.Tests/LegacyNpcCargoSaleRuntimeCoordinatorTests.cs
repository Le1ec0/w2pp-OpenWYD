using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.Server;
using WydCdk.World;

internal static class LegacyNpcCargoSaleRuntimeCoordinatorTests
{
    public static void DurableCommitPublishesSharedCargoOwner()
    {
        var fixture = new Fixture();
        var result = fixture.Coordinator.CommitAsync(fixture.Plan, "ALICE", 0).AsTask().GetAwaiter().GetResult();

        Assert(result.Status == LegacyNpcCargoSaleRuntimeCommitStatus.Committed && result.CargoSnapshot is { Revision: 1 },
            "A durable Cargo CAS did not publish the next runtime owner revision.");
        Assert(fixture.Store.Current.Cargo[4] == default && fixture.Store.Current.Characters.Slots[0].Coin == 9_600,
            "The durable CAS did not update the stored Cargo and seller MOB.Coin.");
        Assert(fixture.Store.Current.BankCoin == 246_810 && fixture.Runtime.TryGet("ALICE", out var owner) && owner!.Items[4] == default,
            "Runtime publication changed bank Coin or retained the sold Cargo item.");
    }

    public static void ConflictReloadsOwnerBeforeNextRequest()
    {
        var fixture = new Fixture();
        var external = fixture.InitialCargo.ToArray();
        external[9] = new LegacyItem(777, 0, 0, 0, 0, 0, 0);
        fixture.Store.SetCurrent(external, expectedCoin: 100);
        fixture.Store.NextMode = MutationMode.Conflict;

        var result = fixture.Coordinator.CommitAsync(fixture.Plan, "ALICE", 0).AsTask().GetAwaiter().GetResult();

        Assert(result.Status == LegacyNpcCargoSaleRuntimeCommitStatus.Conflict && result.CargoSnapshot is { WasReconciled: true, Revision: 1 },
            "A CAS conflict did not reload the authoritative Cargo owner before retry.");
        Assert(fixture.Runtime.TryGet("ALICE", out var owner) && owner!.Items[9] == external[9] && owner.Items[4] == fixture.SoldItem,
            "Conflict reconciliation did not retain the authoritative unsold Cargo state.");
        Assert(!fixture.Gate.IsRecoveryRequired("ALICE"), "A successful authoritative conflict reload left the account blocked.");
    }

    public static void AmbiguousCommitMatchingAfterImagePublishesOnce()
    {
        var fixture = new Fixture();
        fixture.Store.NextMode = MutationMode.AmbiguousAfterCommit;

        var result = fixture.Coordinator.CommitAsync(fixture.Plan, "ALICE", 0).AsTask().GetAwaiter().GetResult();

        Assert(result.Status == LegacyNpcCargoSaleRuntimeCommitStatus.Committed && result.CargoSnapshot is { Revision: 1 },
            "An ambiguous commit matching the authoritative after-image was not reconciled as committed.");
        Assert(fixture.Runtime.TryGet("ALICE", out var owner) && owner!.Items[4] == default && owner.Revision == 1,
            "The after-image reconciliation did not publish exactly one runtime revision.");
    }

    public static void ConflictWithoutAuthoritativeReadRequiresRecovery()
    {
        var fixture = new Fixture();
        fixture.Store.NextMode = MutationMode.Conflict;
        fixture.Store.ReadFails = true;

        var result = fixture.Coordinator.CommitAsync(fixture.Plan, "ALICE", 0).AsTask().GetAwaiter().GetResult();

        Assert(result.Status == LegacyNpcCargoSaleRuntimeCommitStatus.RecoveryRequired && fixture.Gate.IsRecoveryRequired("ALICE"),
            "A conflict with unavailable authoritative state did not quarantine the account owner.");
    }

    public static void ConflictWithChangedSellerCoinRequiresRecovery()
    {
        var fixture = new Fixture();
        fixture.Store.SetCurrent(fixture.InitialCargo, expectedCoin: 101);
        fixture.Store.NextMode = MutationMode.Conflict;

        var result = fixture.Coordinator.CommitAsync(fixture.Plan, "ALICE", 0).AsTask().GetAwaiter().GetResult();

        Assert(result.Status == LegacyNpcCargoSaleRuntimeCommitStatus.RecoveryRequired && fixture.Gate.IsRecoveryRequired("ALICE"),
            "A persisted seller MOB.Coin conflict did not quarantine the stale live character state.");
    }

    public static void CompensationExceptionRequiresRecovery()
    {
        var fixture = new Fixture();
        var committed = fixture.Coordinator.CommitAsync(fixture.Plan, "ALICE", 0).AsTask().GetAwaiter().GetResult();
        Assert(committed.Status == LegacyNpcCargoSaleRuntimeCommitStatus.Committed && committed.CargoSnapshot is not null,
            "The compensation fixture did not first commit its Cargo sale.");
        fixture.Store.ThrowNextMutation = true;

        var compensated = fixture.Coordinator.TryCompensateAsync(committed).AsTask().GetAwaiter().GetResult();

        Assert(!compensated && fixture.Gate.IsRecoveryRequired("ALICE"),
            "A compensation exception escaped without putting the account behind its recovery barrier.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            InitialCargo = new LegacyItem[LegacyAccountSnapshot.CargoCount];
            SoldItem = new LegacyItem(501, 1, 2, 3, 4, 5, 6);
            InitialCargo[4] = SoldItem;
            Runtime.RegisterSession("ALICE", 40);
            var initialOwner = Runtime.LoadAuthoritative("ALICE", InitialCargo);
            Store = new MemoryStore(InitialCargo, expectedCoin: 100, bankCoin: 246_810);
            Gate = new LegacyAccountOperationGate("UP");
            Coordinator = new LegacyNpcCargoSaleRuntimeCoordinator(Store, Store, Runtime, Gate);

            var newCargo = InitialCargo.ToArray();
            newCargo[4] = default;
            var expectedMob = new byte[LegacyAccountSnapshot.CharacterStride];
            BinaryPrimitives.WriteInt32LittleEndian(expectedMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), 100);
            var updatedMob = expectedMob.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(updatedMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), 9_600);
            Plan = new LegacyNpcSellCompletionPlan(
                40,
                LegacyNpcSellSourceRules.Resolve((short)LegacyNpcSellSourceKind.Cargo, 4, tradeMode: false),
                new LegacyNpcSellPreparedSale(SoldItem, 10_000, 9_500, 0, RequiresRecycleBinInsertion: true),
                new LegacyNpcSellTargetExpectation(1_000, LegacyWorldMobKind.Npc, 2_088, 2_148, 1),
                new LegacyNpcSellLocation(0, 5),
                2_080,
                2_140,
                expectedMob,
                updatedMob,
                100,
                9_600,
                new LegacyRecycleBinEntry[LegacyRecycleBinRules.Capacity],
                new LegacyRecycleBinEntry[LegacyRecycleBinRules.Capacity],
                LegacyNpcCargoSalePersistenceRules.SerializeCargo(InitialCargo),
                LegacyNpcCargoSalePersistenceRules.SerializeCargo(newCargo),
                initialOwner.Revision);
        }

        public LegacyItem[] InitialCargo { get; }
        public LegacyItem SoldItem { get; }
        public LegacyAccountCargoRuntime Runtime { get; } = new("UP");
        public MemoryStore Store { get; }
        public LegacyAccountOperationGate Gate { get; }
        public LegacyNpcCargoSaleRuntimeCoordinator Coordinator { get; }
        public LegacyNpcSellCompletionPlan Plan { get; }
    }

    private enum MutationMode
    {
        Success,
        Conflict,
        AmbiguousAfterCommit,
        AmbiguousWithoutCommit,
    }

    private sealed class MemoryStore : IAccountSnapshotStore, IAtomicNpcCargoSaleStore
    {
        public MemoryStore(IReadOnlyList<LegacyItem> cargo, int expectedCoin, int bankCoin)
        {
            var slots = Enumerable.Range(0, LegacyAccountSnapshot.CharacterCount)
                .Select(index => new LegacyCharacterSlot(
                    0, 0, index == 0 ? "HERO" : string.Empty, default,
                    new LegacyItem[LegacyCharacterSelection.EquipmentCount],
                    0, index == 0 ? expectedCoin : 0, 0))
                .ToArray();
            Current = new LegacyAccountSnapshot(
                "ALICE",
                new LegacyCharacterSelection(slots),
                cargo.ToArray(),
                bankCoin,
                false,
                default,
                string.Empty,
                false);
        }

        public LegacyAccountSnapshot Current { get; private set; }
        public MutationMode NextMode { get; set; }
        public bool ReadFails { get; set; }
        public bool ThrowNextMutation { get; set; }

        public ValueTask<LegacyAccountSnapshot?> ReadSnapshotAsync(string accountName, CancellationToken cancellationToken = default) =>
            ReadFails
                ? ValueTask.FromException<LegacyAccountSnapshot?>(new IOException("Injected authoritative snapshot failure."))
                : ValueTask.FromResult<LegacyAccountSnapshot?>(Current);

        public ValueTask<LegacyNpcCargoSaleCommitResult> TryCommitNpcCargoSaleAsync(
            LegacyNpcCargoSaleCommitRequest request,
            CancellationToken cancellationToken = default)
        {
            if (ThrowNextMutation)
            {
                ThrowNextMutation = false;
                return ValueTask.FromException<LegacyNpcCargoSaleCommitResult>(new IOException("Injected CAS transport failure."));
            }

            var mode = NextMode;
            NextMode = MutationMode.Success;
            if (mode == MutationMode.Conflict)
                return ValueTask.FromResult(LegacyNpcCargoSaleCommitResult.Conflict);
            if (mode == MutationMode.AmbiguousWithoutCommit)
                return ValueTask.FromResult(LegacyNpcCargoSaleCommitResult.Ambiguous);

            var currentCargo = LegacyNpcCargoSalePersistenceRules.SerializeCargo(Current.Cargo);
            var currentCoin = Current.Characters.Slots[request.CharacterSlot].Coin;
            if (currentCoin != request.ExpectedMobCoin || !currentCargo.AsSpan().SequenceEqual(request.ExpectedCargo))
                return ValueTask.FromResult(LegacyNpcCargoSaleCommitResult.Conflict);

            SetCurrent(
                LegacyNpcCargoSalePersistenceRules.DeserializeCargo(request.NewCargo),
                request.NewMobCoin);
            return ValueTask.FromResult(mode == MutationMode.AmbiguousAfterCommit
                ? LegacyNpcCargoSaleCommitResult.Ambiguous
                : LegacyNpcCargoSaleCommitResult.Success);
        }

        public void SetCurrent(IReadOnlyList<LegacyItem> cargo, int expectedCoin)
        {
            var slots = Current.Characters.Slots.ToArray();
            slots[0] = slots[0] with { Coin = expectedCoin };
            Current = Current with
            {
                Characters = new LegacyCharacterSelection(slots),
                Cargo = cargo.ToArray(),
            };
        }
    }
}

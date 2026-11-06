namespace WydCdk.World;

public sealed record LegacyPhysicalAttackBatchSlotRequest(
    int SlotIndex,
    int TargetConnectionId,
    LegacyPhysicalAttackBatchSlotRolls? Rolls = null);

public sealed record LegacyPhysicalAttackBatchSlotRolls(
    int? RandomFactor = null,
    int? AttackMissRoll = null,
    int? AbsorptionRoll = null,
    int? FrostRoll = null,
    int? DrainRoll = null,
    int? SpecialDropRoll = null,
    int? SpecialItemRoll = null,
    int? EventDropRoll = null,
    int? RuneRoll = null,
    int? RuneChanceRoll = null,
    int? PistaRandomRoll = null,
    int? BloodHandsAmountRoll = null,
    int? BloodHandsChanceRoll = null);

public sealed record LegacyPhysicalAttackBatchActionRolls(
    int? DoubleCriticalRoll = null,
    int? SecondaryDamageRandomFactor = null,
    int? SecondaryDamageFloorRoll = null,
    int? DoubleCriticalChanceRoll = null,
    int? DoubleCriticalDamageRoll = null);

public sealed class LegacyPhysicalAttackBatchActionState(ushort clientProgress)
{
    private LegacyDoubleCriticalResult? doubleCriticalResult;
    private LegacyPhysicalAttackBatchWireSlotOverride? reservedWireSlot;
    private int secondaryDamage;

    public ushort ClientProgress { get; } = clientProgress;
    public bool HasResolvedDoubleCritical => doubleCriticalResult is not null;
    public byte DoubleCritical => doubleCriticalResult?.Flags ?? 0;
    public ushort NextProgress => doubleCriticalResult?.NextProgress ?? ClientProgress;
    public bool HasReservedWireSlotOne => reservedWireSlot is not null;
    public int SecondaryDamage => secondaryDamage;

    public LegacyDoubleCriticalResult ResolveDoubleCriticalForPhysicalSlot(
        int slotIndex,
        Func<LegacyDoubleCriticalResult> resolveSlotZero,
        out bool resolvedNow)
    {
        ArgumentNullException.ThrowIfNull(resolveSlotZero);
        resolvedNow = false;
        if (slotIndex == 0 && doubleCriticalResult is null)
        {
            doubleCriticalResult = resolveSlotZero();
            resolvedNow = true;
        }

        return doubleCriticalResult ?? new LegacyDoubleCriticalResult(0, ClientProgress);
    }

    public void ReserveSecondaryDamageSlot(int secondaryDamage)
    {
        if (doubleCriticalResult is null)
            throw new InvalidOperationException("The class-3 passive can reserve its wire slot only after slot 0 resolves DoubleCritical/Progress.");
        if (reservedWireSlot is not null)
            throw new InvalidOperationException("Physical wire slot 1 has already been reserved by the action.");

        doubleCriticalResult = doubleCriticalResult.Value with
        {
            Flags = (byte)(doubleCriticalResult.Value.Flags | 4),
        };
        this.secondaryDamage = secondaryDamage;
        reservedWireSlot = new LegacyPhysicalAttackBatchWireSlotOverride(1, 0, secondaryDamage);
    }

    public void UpdateSecondaryDamage(int value)
    {
        secondaryDamage = value;
        if (reservedWireSlot is not null)
            reservedWireSlot = reservedWireSlot with { Damage = value };
    }

    public bool IsReservedWireSlot(int slotIndex) => slotIndex == 1 && reservedWireSlot is not null;

    public IReadOnlyList<LegacyPhysicalAttackBatchWireSlotOverride> GetWireSlotOverrides() =>
        reservedWireSlot is null ? [] : [reservedWireSlot];
}

public sealed record LegacyPhysicalAttackBatchRequest(
    int AttackerConnectionId,
    IReadOnlyList<LegacyPhysicalAttackBatchSlotRequest> Slots,
    ushort ClientProgress = 0,
    bool SupportsSecondaryDamageSlot = false,
    LegacyItemDataTable? ItemData = null,
    LegacyPhysicalAttackBatchActionRolls? ActionRolls = null);

public sealed record LegacyPhysicalAttackBatchSlotOutcome(
    int SlotIndex,
    LegacyPhysicalAttackResult Result,
    LegacyPhysicalAttackOutcome? Outcome);

public sealed record LegacyPhysicalAttackBatchWireSlotOverride(
    int SlotIndex,
    int TargetConnectionId,
    int Damage);

public sealed record LegacyPhysicalAttackBatchOutcome(
    IReadOnlyList<LegacyPhysicalAttackBatchSlotOutcome> SlotOutcomes,
    int? AbortSlotIndex,
    byte DoubleCritical,
    ushort NextProgress,
    byte[]? AttackerMobSnapshot,
    bool CompletedNormally,
    IReadOnlyList<LegacyPhysicalAttackBatchWireSlotOverride> WireSlotOverrides)
{
    public IReadOnlyList<LegacyAttackViewRemovalEffect> ViewRemovalEffects { get; init; } = [];
}

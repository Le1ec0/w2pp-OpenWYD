using WydCdk.Protocol;

namespace WydCdk.World;

public enum LegacyAttackActionSlotKind : byte
{
    Physical,
    Skill,
}

public sealed record LegacySkillAttackBatchSlotRolls(
    int? RandomFactor = null,
    int? AffectRandomFactor = null,
    int? InstanceRandomFactor = null,
    int? BurnRandomFactor = null,
    int? AttackMissRoll = null,
    int? ResurrectionRoll = null,
    int? ResurrectionHpRoll = null,
    int? RecallCityRandomX = null,
    int? RecallCityRandomY = null,
    int? RecallNewbieRandomX = null,
    int? RecallNewbieRandomY = null,
    int? SpecialDropRoll = null,
    int? SpecialItemRoll = null,
    int? EventDropRoll = null,
    int? RuneRoll = null,
    int? RuneChanceRoll = null,
    int? PistaRandomRoll = null,
    int? FrostRoll = null,
    int? DrainRoll = null);

public sealed record LegacyAttackActionBatchSlotRequest(
    int SlotIndex,
    LegacyAttackActionSlotKind Kind,
    int TargetConnectionId,
    LegacyPhysicalAttackBatchSlotRolls? PhysicalRolls = null,
    LegacySkillAttackBatchSlotRolls? SkillRolls = null,
    int? SkillTargetIndex = null);

public sealed record LegacyAttackActionBatchRequest(
    int AttackerConnectionId,
    IReadOnlyList<LegacyAttackActionBatchSlotRequest> Slots,
    LegacySkillDefinition? Skill = null,
    LegacyItemDataTable? ItemData = null,
    int Weather = 0,
    ushort ClientProgress = 0,
    bool SupportsSecondaryDamageSlot = false,
    LegacyPhysicalAttackBatchActionRolls? PhysicalActionRolls = null,
    uint ClientTick = ClientTickPolicy.SkipCheckTick)
{
    public bool ApplySkillEffects { get; init; } = true;
    public bool IsShortTwoTargetPacket { get; init; }
    public LegacySkillDefinition? DivineBlessingHealSkill { get; init; }
    public int SkillIndex { get; init; } = -1;
    public ushort? TargetX { get; init; }
    public ushort? TargetY { get; init; }
}

public sealed record LegacyAttackActionBatchSpawnedNpc(int ParentSlotIndex, LegacyWorldNpc Npc);

public sealed record LegacyAttackViewRemovalEffect(
    int SlotIndex,
    int TargetConnectionId,
    IReadOnlyList<int>? ViewRecipientConnectionIds = null);

public sealed record LegacyAttackActionBatchSlotOutcome(
    int SlotIndex,
    LegacyAttackActionSlotKind Kind,
    LegacyPhysicalAttackResult? PhysicalResult = null,
    LegacyPhysicalAttackOutcome? PhysicalOutcome = null,
    LegacySkillAttackResult? SkillResult = null,
    LegacySkillAttackOutcome? SkillOutcome = null);

public enum LegacyAttackActionSecondaryOutcomeKind : byte
{
    SetHpMp,
    AttackOne,
    SetHpDam,
}

/// <summary>A captured server-created child relay produced while a parent action batch owns the WorldHub lock.</summary>
public sealed record LegacyAttackActionSecondaryOutcome(
    int ParentSlotIndex,
    LegacyAttackActionSecondaryOutcomeKind Kind,
    byte[] MobSnapshot,
    int RequestedHp,
    int RequestedMp,
    short PositionX,
    short PositionY,
    int WireTargetId,
    int WireDamage,
    IReadOnlyList<int> RecipientConnectionIds)
{
    public byte[]? AffectSnapshot { get; init; }
}

public sealed record LegacyAttackActionBatchOutcome(
    IReadOnlyList<LegacyAttackActionBatchSlotOutcome> SlotOutcomes,
    int? AbortSlotIndex,
    byte DoubleCritical,
    ushort NextProgress,
    byte[]? AttackerMobSnapshot,
    bool CompletedNormally,
    IReadOnlyList<LegacyPhysicalAttackBatchWireSlotOverride> WireSlotOverrides,
    int? HealingExperience = null)
{
    public IReadOnlyList<LegacyAttackActionSecondaryOutcome> SecondaryActions { get; init; } = [];
    public IReadOnlyList<LegacyAttackActionBatchSpawnedNpc> SpawnedNpcs { get; init; } = [];
    public IReadOnlyList<LegacyAttackViewRemovalEffect> ViewRemovalEffects { get; init; } = [];
}

/// <summary>A server-generated Type 22/23 player-timer attack and its captured combat recipients.</summary>
public sealed record LegacyPlayerAffectAreaAttackOutcome(
    int AttackerConnectionId,
    int SkillIndex,
    short PositionX,
    short PositionY,
    short TargetX,
    short TargetY,
    byte[] AttackerMobSnapshot,
    IReadOnlyList<int> TargetConnectionIds,
    LegacyAttackActionBatchOutcome? BatchOutcome,
    IReadOnlyList<int> ViewRecipientConnectionIds)
{
    public bool OneTargetMessage { get; init; }
}

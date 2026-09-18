using WydCdk.Protocol;

namespace WydCdk.World;

public enum LegacyUseItemResult
{
    Accepted,
    ParticipantNotFound,
    ItemDataUnavailable,
    AttackerNotAlive,
    PotionDelay,
    InvalidPlacement,
    SourceEmpty,
    DestinationEmpty,
    UnsupportedItem,
    InvalidDestination,
    ItemLevelMismatch,
    AlreadyCompleted,
    ClassRestricted,
    MapRestricted,
    CoinLimitReached,
    DonateLimitReached,
}

public sealed record LegacyUseItemOutcome(
    int SourceSlot,
    LegacyItem SourceItem,
    int DestinationSlot,
    LegacyItem DestinationItem);

public sealed record LegacyPotionOutcome(
    int SourceSlot,
    LegacyItem SourceItem,
    int RequestedHp,
    int RequestedMp);

public sealed record LegacyRefinementOutcome(
    int SourceSlot,
    LegacyItem SourceItem,
    int DestinationSlot,
    LegacyItem DestinationItem,
    bool Succeeded);

public sealed record LegacyLegendaryUpgradeOutcome(
    int SourceSlot,
    LegacyItem SourceItem,
    int DestinationSlot,
    LegacyItem DestinationItem,
    bool Succeeded);

public sealed record LegacyOrcPillOutcome(
    int SourceSlot,
    LegacyItem SourceItem,
    ushort SkillBonus,
    byte QuestFlag,
    byte[] MobSnapshot,
    byte[] MobExtraSnapshot);

public sealed record LegacyExperienceConsumableOutcome(
    int SourceSlot,
    LegacyItem SourceItem,
    int Volatile,
    long Experience,
    int Level,
    int Stage,
    bool LeveledUp,
    byte[] MobSnapshot);

public sealed record LegacyAffectConsumableOutcome(
    int SourceSlot,
    LegacyItem SourceItem,
    int AffectSlot,
    byte AffectValue,
    uint AffectTime,
    byte[] MobSnapshot,
    byte[] AffectSnapshot);

public sealed record LegacyPvpJewelryOutcome(
    int SourceSlot,
    LegacyItem SourceItem,
    int AffectSlot,
    ushort AffectLevel,
    uint AffectTime,
    byte[] MobSnapshot,
    byte[] AffectSnapshot);

public sealed record LegacyMovementConsumableOutcome(
    int SourceSlot,
    LegacyItem SourceItem,
    int Volatile,
    short PositionX,
    short PositionY,
    bool Moved,
    bool SavedWarp,
    byte[] MobSnapshot);

public sealed record LegacyMountCatalystOutcome(
    int SourceSlot,
    LegacyItem SourceItem,
    int DestinationSlot,
    LegacyItem DestinationItem,
    bool Restored);

public sealed record LegacyCoinConsumableOutcome(
    int SourceSlot,
    LegacyItem SourceItem,
    int AddedCoin,
    int Coin);

public sealed record LegacyDonateConsumableOutcome(
    int SourceSlot,
    LegacyItem SourceItem,
    int AddedDonate,
    int Donate,
    byte[] MobSnapshot,
    int PreviousDonate,
    byte[] PreviousMob);

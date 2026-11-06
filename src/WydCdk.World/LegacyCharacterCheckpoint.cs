using WydCdk.Protocol;

namespace WydCdk.World;

public enum LegacyCharacterCheckpointSaveResult
{
    Success,
    AccountNotFound,
    SlotOutOfRange,
    InvalidLength,
    CharacterNotAvailable,
    NotAvailable,
}

/// <summary>One immutable Host capture for an atomic periodic or departure checkpoint.</summary>
public sealed record LegacyCharacterCheckpointSaveRequest(
    string AccountName,
    int CharacterSlot,
    ReadOnlyMemory<byte> Mob,
    short PositionX,
    short PositionY,
    ReadOnlyMemory<byte> MobExtra,
    ReadOnlyMemory<byte> Affect,
    IReadOnlyList<LegacyItem> ClientEquipment);

/// <summary>
/// Capability for persisting the authoritative character snapshot and its client equipment extension
/// as one operation, preserving account fields owned by independent writers.
/// </summary>
public interface ICharacterCheckpointStore
{
    ValueTask<LegacyCharacterCheckpointSaveResult> TrySaveCharacterCheckpointAsync(
        LegacyCharacterCheckpointSaveRequest request,
        CancellationToken cancellationToken = default);
}

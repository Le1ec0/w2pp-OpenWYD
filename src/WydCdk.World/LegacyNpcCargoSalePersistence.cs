using WydCdk.Protocol;

namespace WydCdk.World;

public enum LegacyNpcCargoSaleCommitResult
{
    Success,
    Conflict,
    InvalidRequest,
    AccountNotFound,
    CharacterNotAvailable,
    Unavailable,
    Ambiguous,
}

public sealed record LegacyNpcCargoSaleCommitRequest(
    string AccountName,
    int CharacterSlot,
    byte[] ExpectedCargo,
    byte[] NewCargo,
    int ExpectedMobCoin,
    int NewMobCoin);

/// <summary>Optional compare-and-swap capability for an NPC sale sourced from account Cargo.</summary>
public interface IAtomicNpcCargoSaleStore
{
    ValueTask<LegacyNpcCargoSaleCommitResult> TryCommitNpcCargoSaleAsync(
        LegacyNpcCargoSaleCommitRequest request,
        CancellationToken cancellationToken = default);
}

public static class LegacyNpcCargoSalePersistenceRules
{
    public static byte[] SerializeCargo(IReadOnlyList<LegacyItem> cargo)
    {
        ArgumentNullException.ThrowIfNull(cargo);
        if (cargo.Count != LegacyAccountSnapshot.CargoCount)
            throw new ArgumentException($"Cargo must contain exactly {LegacyAccountSnapshot.CargoCount} items.", nameof(cargo));
        var bytes = new byte[LegacyAccountSnapshot.CargoCount * LegacyItem.SizeInBytes];
        for (var index = 0; index < cargo.Count; index++)
            cargo[index].Write(bytes.AsSpan(index * LegacyItem.SizeInBytes, LegacyItem.SizeInBytes));
        return bytes;
    }

    public static LegacyItem[] DeserializeCargo(ReadOnlySpan<byte> cargo)
    {
        if (cargo.Length != LegacyAccountSnapshot.CargoCount * LegacyItem.SizeInBytes)
            throw new ArgumentException("Cargo bytes do not have the complete legacy array length.", nameof(cargo));
        var items = new LegacyItem[LegacyAccountSnapshot.CargoCount];
        for (var index = 0; index < items.Length; index++)
            items[index] = LegacyItem.Read(cargo.Slice(index * LegacyItem.SizeInBytes, LegacyItem.SizeInBytes));
        return items;
    }

    public static bool IsValid(LegacyNpcCargoSaleCommitRequest? request) =>
        request is not null &&
        LegacyAccountNameRules.TryNormalize(request.AccountName, out var normalized) && normalized == request.AccountName &&
        request.CharacterSlot is >= 0 and < LegacyAccountSnapshot.CharacterCount &&
        request.ExpectedCargo is { Length: LegacyAccountSnapshot.CargoCount * LegacyItem.SizeInBytes } &&
        request.NewCargo is { Length: LegacyAccountSnapshot.CargoCount * LegacyItem.SizeInBytes };
}

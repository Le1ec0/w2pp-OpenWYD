using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Captures the read-only live state required by the next _MSG_Sell gates.
/// Inventory is split across the world MOB (equipment/carry) and account store
/// (cargo); the selected snapshots are returned without mutating either source.
/// </summary>
public static class LegacyNpcSellAuthoritativeLookup
{
    public static async ValueTask<LegacyNpcSellLookupOutcome> CaptureAsync(
        LoginSession session,
        ShopSellMessage request,
        WorldHub world,
        IAccountSnapshotStore? accountSnapshots,
        bool tradeMode,
        CancellationToken cancellationToken = default,
        LegacyAccountCargoRuntime? cargoRuntime = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(world);

        if (session.State != LoginSessionState.Playing)
            return new(LegacyNpcSellLookupStatus.SessionNotPlaying, null);

        if (string.IsNullOrWhiteSpace(session.AccountName) ||
            session.CharacterSlot is < 0 or >= LegacyAccountSnapshot.CharacterCount)
            return new(LegacyNpcSellLookupStatus.SessionIdentityUnavailable, null);

        if (!world.TryGetCharacterSnapshot(
                session.ConnectionId,
                out var sellerMob,
                out var sellerX,
                out var sellerY) ||
            sellerMob is null || sellerMob.Length < LegacyAccountSnapshot.CharacterStride)
            return new(LegacyNpcSellLookupStatus.SellerWorldStateUnavailable, null);

        if (accountSnapshots is null)
            return new(LegacyNpcSellLookupStatus.AccountSnapshotStoreUnavailable, null);

        var account = await accountSnapshots.ReadSnapshotAsync(session.AccountName, cancellationToken);
        if (account is null)
            return new(LegacyNpcSellLookupStatus.AccountSnapshotUnavailable, null);
        if (!string.Equals(account.AccountName, session.AccountName, StringComparison.OrdinalIgnoreCase))
            return new(LegacyNpcSellLookupStatus.AccountIdentityMismatch, null);

        var source = LegacyNpcSellSourceRules.Resolve(request.MyType, request.MyPos, tradeMode);
        LegacyAccountCargoSnapshot? cargoState = null;
        if (source.Kind == LegacyNpcSellSourceKind.Cargo && cargoRuntime is not null)
            cargoState = cargoRuntime.LoadAuthoritative(account.AccountName, account.Cargo);
        var state = new LegacyNpcSellAuthoritativeState(
            session.AccountName,
            session.CharacterSlot,
            request.TargetId,
            sellerMob,
            sellerX,
            sellerY,
            account.IsBlocked,
            source,
            SelectedItem: null,
            Target: null)
        {
            CargoSnapshot = cargoState?.Items,
            CargoRevision = cargoState?.Revision,
            CargoWasReconciled = cargoState?.WasReconciled ?? false,
            CargoChangedPositions = cargoState?.ChangedPositions ?? Array.Empty<int>(),
        };

        if (source.Kind == LegacyNpcSellSourceKind.Cargo && cargoRuntime is null)
            return new(LegacyNpcSellLookupStatus.CargoOwnerUnavailable, state);
        if (cargoState?.WasReconciled == true)
            return new(LegacyNpcSellLookupStatus.CargoReconciled, state);

        if (!source.IsValid)
            return new(LegacyNpcSellLookupStatus.SourceRejected, state);

        if (!TryReadSelectedItem(sellerMob, account, cargoState?.Items, source, out var selectedItem))
            return new(LegacyNpcSellLookupStatus.SelectedItemUnavailable, state);

        state = state with { SelectedItem = selectedItem };
        if (!world.TryGetMobSnapshot(request.TargetId, out var target) || target is null)
            return new(LegacyNpcSellLookupStatus.TargetUnavailable, state);

        state = state with { Target = target };
        return new(LegacyNpcSellLookupStatus.Ready, state);
    }

    private static bool TryReadSelectedItem(
        ReadOnlySpan<byte> sellerMob,
        LegacyAccountSnapshot account,
        IReadOnlyList<LegacyItem>? cargoSnapshot,
        LegacyNpcSellSourceResolution source,
        out LegacyItem item)
    {
        item = default;
        if (source.Kind is not { } kind)
            return false;

        if (kind == LegacyNpcSellSourceKind.Cargo)
        {
            var cargo = cargoSnapshot ?? account.Cargo;
            if (cargo.Count != LegacyAccountSnapshot.CargoCount ||
                source.Position < 0 || source.Position >= cargo.Count)
                return false;

            item = cargo[source.Position];
            return true;
        }

        var startOffset = kind switch
        {
            LegacyNpcSellSourceKind.Equipment => LegacyAccountSnapshot.MobEquipmentOffset,
            LegacyNpcSellSourceKind.Carry => LegacyAccountSnapshot.MobCarryOffset,
            _ => -1,
        };
        if (startOffset < 0)
            return false;

        var itemOffset = startOffset + (source.Position * LegacyItem.SizeInBytes);
        if (source.Position < 0 || itemOffset < 0 ||
            itemOffset > sellerMob.Length - LegacyItem.SizeInBytes)
            return false;

        item = LegacyItem.Read(sellerMob.Slice(itemOffset, LegacyItem.SizeInBytes));
        return true;
    }
}

public enum LegacyNpcSellLookupStatus
{
    Ready,
    SessionNotPlaying,
    SessionIdentityUnavailable,
    SellerWorldStateUnavailable,
    AccountSnapshotStoreUnavailable,
    AccountSnapshotUnavailable,
    AccountIdentityMismatch,
    SourceRejected,
    SelectedItemUnavailable,
    TargetUnavailable,
    CargoOwnerUnavailable,
    CargoReconciled,
}

public sealed record LegacyNpcSellAuthoritativeState(
    string AccountName,
    int CharacterSlot,
    ushort TargetId,
    byte[] SellerMobSnapshot,
    short SellerPositionX,
    short SellerPositionY,
    bool AccountBlocked,
    LegacyNpcSellSourceResolution Source,
    LegacyItem? SelectedItem,
    LegacyWorldMobSnapshot? Target)
{
    public IReadOnlyList<LegacyItem>? CargoSnapshot { get; init; }
    public long? CargoRevision { get; init; }
    public bool CargoWasReconciled { get; init; }
    public IReadOnlyList<int> CargoChangedPositions { get; init; } = Array.Empty<int>();
}

public sealed record LegacyNpcSellLookupOutcome(
    LegacyNpcSellLookupStatus Status,
    LegacyNpcSellAuthoritativeState? State);

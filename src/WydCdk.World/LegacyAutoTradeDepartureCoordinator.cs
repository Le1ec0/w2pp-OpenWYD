using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Serializes departure saves/cleanup against purchases, without socket I/O.
/// Callbacks are persistence-only: they must not acquire book leases or send frames.
/// </summary>
public static class LegacyAutoTradeDepartureCoordinator
{
    public static async ValueTask<LegacyAutoTradeDeparturePlan> DepartAsync(int connectionId, WorldHub world, LegacyAutoTradeBook book,
        Func<CancellationToken, ValueTask> saveCharacter,
        Func<LegacyAutoTradeSnapshot?, CancellationToken, ValueTask> removeState,
        LegacyFrameCodec codec, uint clientTick, byte keywordIndex, CancellationToken cancellationToken = default)
        => await DepartCoreAsync(connectionId, world, book, (_, ct) => saveCharacter(ct), removeState,
            codec, clientTick, keywordIndex, accountOperationGate: null, cancellationToken);

    public static async ValueTask<LegacyAutoTradeDeparturePlan> DepartAsync(int connectionId, WorldHub world, LegacyAutoTradeBook book,
        Func<LegacyAccountOperationLease?, CancellationToken, ValueTask> saveCharacter,
        Func<LegacyAutoTradeSnapshot?, CancellationToken, ValueTask> removeState,
        LegacyFrameCodec codec, uint clientTick, byte keywordIndex,
        LegacyAccountOperationGate accountOperationGate,
        CancellationToken cancellationToken = default)
        => await DepartCoreAsync(connectionId, world, book, saveCharacter, removeState,
            codec, clientTick, keywordIndex, accountOperationGate, cancellationToken);

    private static async ValueTask<LegacyAutoTradeDeparturePlan> DepartCoreAsync(
        int connectionId,
        WorldHub world,
        LegacyAutoTradeBook book,
        Func<LegacyAccountOperationLease?, CancellationToken, ValueTask> saveCharacter,
        Func<LegacyAutoTradeSnapshot?, CancellationToken, ValueTask> removeState,
        LegacyFrameCodec codec,
        uint clientTick,
        byte keywordIndex,
        LegacyAccountOperationGate? accountOperationGate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(world); ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(saveCharacter); ArgumentNullException.ThrowIfNull(removeState);
        ArgumentNullException.ThrowIfNull(codec);
        var identity = world.CaptureTradeCloseIdentity(connectionId);
        bool IsCurrent() => identity is null ? world.CaptureTradeCloseIdentity(connectionId) is null : world.IsTradeCloseIdentityCurrent(connectionId, identity);
        await using var lease = await book.AcquireAsync([connectionId], cancellationToken);
        if (!IsCurrent()) return new([], [], null);
        LegacyAccountOperationLease? accountLease = null;
        var saveAllowed = true;
        if (accountOperationGate is not null)
        {
            try
            {
                accountLease = await AcquireStablePartyLeaseAsync(accountOperationGate, world, connectionId, cancellationToken);
            }
            catch (LegacyAccountRecoveryRequiredException)
            {
                saveAllowed = false;
            }
        }
        await using var accountLeaseScope = accountLease;
        if (saveAllowed)
            await saveCharacter(accountLease, cancellationToken);
        if (!IsCurrent()) return new([], [], null);
        var shop = book.TryGet(lease, connectionId, out var handle) && handle is not null && book.TryStop(lease, handle)
            ? handle.Snapshot : null;
        await removeState(shop, cancellationToken);
        // A replacement participant must not receive old removal frames or Leave.
        if (!IsCurrent()) return new([], [], null);
        IReadOnlyList<LegacyAutoTradeCloseFramePlan> frames = [];
        if (shop is not null && LegacyAutoTradeRemovalRelay.TryBuild(shop, codec, clientTick, keywordIndex, out var removal) && removal is not null)
        {
            var recipients = world.GetParticipantIdsInPlayerView(connectionId);
            if (recipients.Count == 0) recipients = world.GetParticipantIdsInNpcView(connectionId);
            frames = Array.AsReadOnly(new[] { new LegacyAutoTradeCloseFramePlan(removal.Frame,
                Array.AsReadOnly(recipients.Where(id => id != connectionId).ToArray())) });
        }
        world.Leave(connectionId, out var summons, out var trade);
        return new(frames, summons, trade);
    }

    private static async ValueTask<LegacyAccountOperationLease?> AcquireStablePartyLeaseAsync(
        LegacyAccountOperationGate gate,
        WorldHub world,
        int connectionId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var accounts = world.GetPartyMemberAccountNames(connectionId);
            if (accounts.Count == 0)
                return null;
            var lease = await gate.AcquireAsync(accounts, cancellationToken);
            var currentAccounts = world.GetPartyMemberAccountNames(connectionId);
            if (currentAccounts.All(account => lease.Covers(gate, account)))
                return lease;
            await lease.DisposeAsync();
        }
    }
}

public sealed record LegacyAutoTradeDeparturePlan(IReadOnlyList<LegacyAutoTradeCloseFramePlan> Frames,
    IReadOnlyList<int> DespawnedSummonIds, LegacyTradeCloseOutcome? ClosedTrade);

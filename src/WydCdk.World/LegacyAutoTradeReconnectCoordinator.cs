namespace WydCdk.World;

/// <summary>
/// Applies the reconnect boundary for the persisted offline-shop extension.
/// The W2PP login path clears the character trade state before entering the
/// field; it does not define a second live shop for the same character.
/// </summary>
public static class LegacyAutoTradeReconnectCoordinator
{
    public static async ValueTask<LegacyAutoTradeReconnectOutcome> CloseAsync(
        WorldHub world,
        LegacyAutoTradeBook book,
        ILegacyAutoTradeStateStore stateStore,
        string accountName,
        int characterSlot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(stateStore);

        var persisted = await stateStore.ReadAsync(accountName, characterSlot, cancellationToken);
        if (persisted is not null)
        {
            var removeResult = await stateStore.RemoveAsync(accountName, characterSlot, cancellationToken);
            if (removeResult is not LegacyAutoTradeStateResult.Removed and not LegacyAutoTradeStateResult.NotFound)
                return new(LegacyAutoTradeReconnectResult.PersistenceUnavailable, []);
        }

        var closed = new List<LegacyAutoTradeReconnectClosure>();
        foreach (var npc in world.GetNpcSnapshots())
        {
            if (npc.AutoTradeSnapshot is null ||
                npc.AutoTradeCharacterSlot != characterSlot ||
                !string.Equals(npc.AutoTradeOwnerAccount, accountName.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            // Capture the recipients while the NPC is still registered. After
            // removal, GetParticipantIdsInNpcView cannot resolve its position.
            var recipients = world.GetParticipantIdsInNpcView(npc.ConnectionId);
            book.TryStop(npc.ConnectionId, out var runtimeSnapshot);
            var snapshot = runtimeSnapshot ?? npc.AutoTradeSnapshot;
            if (!world.TryRemoveNpc(npc.ConnectionId, out _))
                continue;

            closed.Add(new(snapshot, recipients));
        }

        return new(
            persisted is null && closed.Count == 0
                ? LegacyAutoTradeReconnectResult.NoListing
                : LegacyAutoTradeReconnectResult.Closed,
            closed);
    }
}

public sealed record LegacyAutoTradeReconnectOutcome(
    LegacyAutoTradeReconnectResult Result,
    IReadOnlyList<LegacyAutoTradeReconnectClosure> ClosedListings);

public sealed record LegacyAutoTradeReconnectClosure(
    LegacyAutoTradeSnapshot Snapshot,
    IReadOnlyList<int> RecipientIds);

public enum LegacyAutoTradeReconnectResult
{
    NoListing,
    Closed,
    PersistenceUnavailable,
}

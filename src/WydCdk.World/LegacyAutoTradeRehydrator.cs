namespace WydCdk.World;

/// <summary>
/// Rebinds persisted autotrade listings to runtime NPC identities during
/// startup. The owner remains offline: no login session or socket participant
/// is created, and the persisted character MOB is used only as the visual
/// source for the trade NPC.
/// </summary>
public static class LegacyAutoTradeRehydrator
{
    public static async ValueTask<LegacyAutoTradeRehydrateReport> RestoreAsync(
        WorldHub world,
        LegacyAutoTradeBook book,
        ILegacyCharacterLoginDataStore characterStore,
        ILegacyAutoTradeStateStore stateStore,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(characterStore);
        ArgumentNullException.ThrowIfNull(stateStore);

        var restored = 0;
        var issues = new List<LegacyAutoTradeRehydrateIssue>();
        var listings = await stateStore.ReadAllAsync(cancellationToken);
        foreach (var listing in listings)
        {
            try
            {
                var character = await characterStore.ReadCharacterLoginDataAsync(
                    listing.AccountName,
                    listing.CharacterSlot,
                    cancellationToken);
                if (character is null)
                {
                    issues.Add(new(listing.AccountName, listing.CharacterSlot, "CharacterUnavailable"));
                    continue;
                }

                var pendingSnapshot = listing.BindToConnection(1);
                var shopConnectionId = world.EnterNpc(
                    character.Mob,
                    listing.PositionX,
                    listing.PositionY,
                    character.Affect,
                    autoTradeSnapshot: pendingSnapshot,
                    autoTradeOwnerAccount: listing.AccountName,
                    autoTradeCharacterSlot: listing.CharacterSlot);
                if (shopConnectionId == 0)
                {
                    issues.Add(new(listing.AccountName, listing.CharacterSlot, "NpcIdUnavailable"));
                    continue;
                }

                var snapshot = pendingSnapshot with { ConnectionId = shopConnectionId };
                if (!book.TryRestore(shopConnectionId, snapshot))
                {
                    world.TryRemoveNpc(shopConnectionId, out _);
                    issues.Add(new(listing.AccountName, listing.CharacterSlot, "InvalidListing"));
                    continue;
                }

                restored++;
            }
            catch (Exception error) when (error is ArgumentException or InvalidDataException or InvalidOperationException)
            {
                issues.Add(new(listing.AccountName, listing.CharacterSlot, error.GetType().Name));
            }
        }

        return new(restored, issues);
    }
}

public sealed record LegacyAutoTradeRehydrateReport(
    int RestoredCount,
    IReadOnlyList<LegacyAutoTradeRehydrateIssue> Issues)
{
    public bool HasIssues => Issues.Count != 0;
}

public sealed record LegacyAutoTradeRehydrateIssue(
    string AccountName,
    int CharacterSlot,
    string Reason);

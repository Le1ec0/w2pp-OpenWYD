using WydCdk.Protocol;
using WydCdk.Server;
using WydCdk.World;

internal static class LegacyRecycleBinSessionTests
{
    public static void Run()
    {
        RecycleStateBelongsToOneWorldSessionAndIsDiscardedOnLeave();
        RequestReturnsOnlyTheAuthenticatedSessionsServerState();
    }

    private static void RecycleStateBelongsToOneWorldSessionAndIsDiscardedOnLeave()
    {
        var world = new WorldHub();
        Check(world.Enter(40, "RECYCLE-A", NoopSend), "The first test player did not enter the world.");
        Check(world.Enter(41, "RECYCLE-B", NoopSend), "The second test player did not enter the world.");

        Check(world.TryGetRecycleBinSnapshot(40, out var firstInitial) && IsEmpty(firstInitial),
            "A new world session did not start with ten empty recycle-bin slots.");

        var saleItem = new LegacyItem(501, 2, 3, 4, 5, 6, 7);
        Check(world.TryRecordRecycleBinSale(40, saleItem, 1234, 45678, out var insertion) &&
              insertion is { WasInserted: true, InsertedIndex: 0 },
            "The world session did not store the pure GetRecycleBin insertion result.");

        Check(world.TryGetRecycleBinSnapshot(40, out var firstSnapshot) &&
              firstSnapshot is { Length: LegacyRecycleBinRules.Capacity } &&
              firstSnapshot[0] == new LegacyRecycleBinEntry(1234, saleItem, 45678),
            "The recycle-bin snapshot did not return the sale recorded for the first session.");
        Check(world.TryGetRecycleBinSnapshot(41, out var secondSnapshot) && IsEmpty(secondSnapshot),
            "One character session observed another session's recycle-bin state.");

        firstSnapshot![0] = default;
        Check(world.TryGetRecycleBinSnapshot(40, out var unchangedSnapshot) &&
              unchangedSnapshot![0] == new LegacyRecycleBinEntry(1234, saleItem, 45678),
            "Mutating a recycle-bin snapshot changed authoritative session state.");

        Check(world.Leave(40), "The first test player did not leave the world.");
        Check(world.Enter(40, "RECYCLE-A", NoopSend), "The reconnected test player did not enter the world.");
        Check(world.TryGetRecycleBinSnapshot(40, out var reconnectedSnapshot) && IsEmpty(reconnectedSnapshot),
            "A new world session inherited the disconnected character's transient recycle-bin state.");
    }

    private static void RequestReturnsOnlyTheAuthenticatedSessionsServerState()
    {
        var world = new WorldHub();
        Check(world.Enter(52, "RECYCLE-REQUEST", NoopSend), "The request test player did not enter the world.");

        var serverItem = new LegacyItem(620, 1, 2, 3, 4, 5, 6);
        Check(world.TryRecordRecycleBinSale(52, serverItem, 777, 99, out var insertion) &&
              insertion is { WasInserted: true },
            "The request test could not seed the authoritative recycle-bin state.");

        var forgedItem = new LegacyItem(900, 9, 8, 7, 6, 5, 4);
        var clientEntries = Enumerable.Repeat(
            new RepurchaseItemEntry(Order: 123, forgedItem, Price: 456),
            RepurchaseItemsMessage.EntryCount).ToArray();
        var request = new RepurchaseItemsMessage(4321, clientEntries);

        Check(LegacyRecycleBinRequestHandler.TryCreateResponse(
                world, 52, 52, LoginSessionState.Playing, request, out var response) == LegacyRecycleBinRequestResult.Ready &&
              response is not null,
            "A valid USER_PLAY request did not produce a recycle-bin response.");
        Check(response!.Target == request.Target && response.Entries[0].Item == serverItem &&
              response.Entries.Skip(1).All(static entry => entry.Item.Index == 0),
            "The response did not use the session state or preserved client-supplied entries.");
        Check(response.Entries.All(static entry => entry.Order == 0 && entry.Price == 0),
            "The response invented client Order/Price values that the 7.69 display path does not consume.");

        Check(LegacyRecycleBinRequestHandler.TryCreateResponse(
                world, 52, 52, LoginSessionState.CharacterSelection, request, out var nonPlayingResponse) ==
              LegacyRecycleBinRequestResult.SessionNotPlaying &&
              nonPlayingResponse is null,
            "The recycle-bin request returned data before USER_PLAY.");
        Check(LegacyRecycleBinRequestHandler.TryCreateResponse(
                world, 53, 53, LoginSessionState.Playing, request, out var absentResponse) ==
              LegacyRecycleBinRequestResult.WorldSessionUnavailable &&
              absentResponse is null,
            "The recycle-bin request returned data without a matching world session.");
        Check(LegacyRecycleBinRequestHandler.TryCreateResponse(
                world, 52, 53, LoginSessionState.Playing, request, out var mismatchedIdResponse) ==
              LegacyRecycleBinRequestResult.HeaderIdMismatch &&
              mismatchedIdResponse is null,
            "The recycle-bin request accepted a header ID belonging to another connection.");
    }

    private static bool IsEmpty(LegacyRecycleBinEntry[]? entries) =>
        entries is { Length: LegacyRecycleBinRules.Capacity } &&
        entries.All(static entry => entry.Item.Index == 0 && entry.Price == 0 && entry.Ticks == 0);

    private static ValueTask NoopSend(ReadOnlyMemory<byte> _, CancellationToken __) => ValueTask.CompletedTask;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

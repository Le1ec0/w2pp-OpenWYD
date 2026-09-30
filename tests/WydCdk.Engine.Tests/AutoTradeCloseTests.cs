using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AutoTradeCloseTests
{
    public static void DepartureWaitsPurchase() => DepartureWaitsPurchaseAsync().GetAwaiter().GetResult();
    public static void ReconnectWaitsPurchase() => ReconnectWaitsPurchaseAsync().GetAwaiter().GetResult();

    private static async Task ReconnectWaitsPurchaseAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "wyd-reconnect-purchase-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var fixture = AutoTradeCloseFixtures.CreatePurchase(); var held = new HeldCommitStore();
            var file = new LegacyAutoTradeFileStore(Path.Combine(root, "UP.json"), "UP");
            await file.SaveAsync("SELLER", 1, fixture.Book.Snapshot().Single().Snapshot);
            var purchase = LegacyAutoTradePurchaseExecutor.ExecuteAsync(fixture.World, fixture.Book, held, fixture.Request).AsTask();
            await held.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var reconnect = LegacyAutoTradeReconnectCoordinator.CloseAsync(fixture.World, fixture.Book, file, "SELLER", 1).AsTask();
            Check(!reconnect.IsCompleted && (await file.ReadAllAsync()).Count == 1, "Reconnect deleted a shop during a pending purchase.");
            held.Complete.SetResult(LegacyAutoTradePurchaseCommitResult.Conflict);
            Check((await purchase.WaitAsync(TimeSpan.FromSeconds(5))).Result == LegacyAutoTradePurchaseExecutionResult.CommitRejected, "Reconnect broke sale rollback.");
            var result = await reconnect.WaitAsync(TimeSpan.FromSeconds(5));
            Check(result.Result == LegacyAutoTradeReconnectResult.Closed && result.ClosedListings.Single().Snapshot.Items[0].Index == 900 &&
                fixture.Book.Snapshot().Count == 0 && !fixture.World.TryGetNpcSnapshot(12, out _) && (await file.ReadAllAsync()).Count == 0,
                "Reconnect closed a partially rolled back shop.");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static async Task DepartureWaitsPurchaseAsync()
    {
        foreach (var commit in new[] { LegacyAutoTradePurchaseCommitResult.Committed, LegacyAutoTradePurchaseCommitResult.Conflict })
        {
            var fixture = AutoTradeCloseFixtures.CreatePurchase(); var store = new HeldCommitStore();
            var purchase = LegacyAutoTradePurchaseExecutor.ExecuteAsync(fixture.World, fixture.Book, store, fixture.Request).AsTask();
            await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            byte[]? saved = null;
            var departure = LegacyAutoTradeDepartureCoordinator.DepartAsync(7, fixture.World, fixture.Book,
                ct => { fixture.World.TryGetCharacterSnapshot(7, out saved, out _, out _); return ValueTask.CompletedTask; },
                (shop, _) => { Check(shop is null, "Buyer departure removed the seller shop."); return ValueTask.CompletedTask; },
                LegacyFrameCodec.CreateDefault(), 123, 0).AsTask();
            Check(!departure.IsCompleted && saved is null, "Departure saved/left during a pending purchase.");
            store.Complete.SetResult(commit);
            var result = await purchase.WaitAsync(TimeSpan.FromSeconds(5));
            await departure.WaitAsync(TimeSpan.FromSeconds(5));
            var committed = commit == LegacyAutoTradePurchaseCommitResult.Committed;
            Check(result.Result == (committed ? LegacyAutoTradePurchaseExecutionResult.Accepted : LegacyAutoTradePurchaseExecutionResult.CommitRejected) &&
                saved is not null && BinaryPrimitives.ReadInt32LittleEndian(saved.AsSpan(LegacyAccountSnapshot.MobCoinOffset)) == (committed ? 50 : 150) &&
                LegacyItem.Read(saved.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes)).Index == (committed ? 900 : 0) &&
                !fixture.World.TryGetCharacterSnapshot(7, out _, out _, out _), "Departure saved stale Gold/carry or broke purchase rollback.");
        }
    }

    public static void PurchaseCloseRace() => PurchaseCloseRaceAsync().GetAwaiter().GetResult();
    public static void PurchaseCommitBlocksClose() =>
        PurchaseCloseRaceAsync(LegacyAutoTradePurchaseCommitResult.Committed).GetAwaiter().GetResult();

    private static async Task PurchaseCloseRaceAsync(LegacyAutoTradePurchaseCommitResult commit = LegacyAutoTradePurchaseCommitResult.Conflict)
    {
        var fixture = AutoTradeCloseFixtures.CreatePurchase();
        var store = new HeldCommitStore();
        var purchase = LegacyAutoTradePurchaseExecutor.ExecuteAsync(
            fixture.World, fixture.Book, store, fixture.Request).AsTask();
        await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var closing = CloseAsync();
        Check(!closing.IsCompleted, "Close entered the shop during a pending durable purchase.");
        store.Complete.SetResult(commit);
        var result = await purchase.WaitAsync(TimeSpan.FromSeconds(5));
        var committed = commit == LegacyAutoTradePurchaseCommitResult.Committed;
        Check(result.Result == (committed ? LegacyAutoTradePurchaseExecutionResult.Accepted : LegacyAutoTradePurchaseExecutionResult.CommitRejected),
            $"Concurrent close broke rollback: {result.Result}.");
        var stopped = await closing.WaitAsync(TimeSpan.FromSeconds(5));
        Check(stopped?.Items[0].Index == (committed ? 0 : 900), "Close observed a partially completed listing.");
        Check(fixture.World.TryGetCharacterSnapshot(7, out var mob, out _, out _) && mob is not null &&
            BinaryPrimitives.ReadInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset)) == (committed ? 50 : 150) &&
            LegacyItem.Read(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes)).Index == (committed ? 900 : 0),
            "Purchase/close did not preserve the expected buyer inventory and Gold.");
        Check(fixture.World.TryGetNpcSnapshot(12, out var npc) && npc?.AutoTradeSnapshot?.Items[0].Index == (committed ? 0 : 900),
            "Purchase/close exposed partially completed NPC state.");

        async Task<LegacyAutoTradeSnapshot?> CloseAsync()
        {
            await using var lease = await fixture.Book.AcquireAsync([12]);
            Check(fixture.Book.TryGet(lease, 12, out var handle) && handle is not null,
                "Close lost the shop after waiting for purchase.");
            Check(fixture.Book.TryStop(lease, handle!), "Close could not remove the rolled back shop.");
            return handle!.Snapshot;
        }
    }

    public static void LeaseOrderAndCancellation() => LeaseOrderAndCancellationAsync().GetAwaiter().GetResult();

    private static async Task LeaseOrderAndCancellationAsync()
    {
        var book = new LegacyAutoTradeBook();
        var held = await book.AcquireAsync([13]);
        using var cancel = new CancellationTokenSource();
        var blocked = book.AcquireAsync([13, 12, 12], cancel.Token).AsTask();
        Check(!blocked.IsCompleted, "Multi-id operation did not wait for held id.");
        var firstWaiter = book.AcquireAsync([12]).AsTask();
        Check(!firstWaiter.IsCompleted, "Ids were not acquired in sorted order.");
        await using var unrelated = await book.AcquireAsync([14]);
        Check(unrelated.Covers(14), "Independent shop was blocked.");
        cancel.Cancel();
        try { await blocked; throw new InvalidOperationException("Cancellation was ignored."); }
        catch (OperationCanceledException) { }
        await using var releasedFirst = await firstWaiter.WaitAsync(TimeSpan.FromSeconds(5));
        Check(releasedFirst.Covers(12), "Cancellation leaked the first lease.");
        await releasedFirst.DisposeAsync();
        await held.DisposeAsync();
        await held.DisposeAsync();
        Check(!held.Covers(13), "Disposed lease still authorizes changes.");
        var reversed = await book.AcquireAsync([13, 12]);
        var ordered = book.AcquireAsync([12, 13]).AsTask();
        Check(!ordered.IsCompleted, "Reciprocal operation bypassed reservation.");
        await reversed.DisposeAsync();
        await using var completed = await ordered.WaitAsync(TimeSpan.FromSeconds(5));
        Check(completed.Covers(12) && completed.Covers(13), "Reciprocal operation leaked a lease.");
    }

    public static void LeaseBookOwnershipAndGeneration() => LeaseBookOwnershipAndGenerationAsync().GetAwaiter().GetResult();

    private static async Task LeaseBookOwnershipAndGenerationAsync()
    {
        var fixture = AutoTradeCloseFixtures.CreatePurchase();
        var other = new LegacyAutoTradeBook();
        await using var foreign = await other.AcquireAsync([12]);
        var original = fixture.Book.Snapshot().Single();
        Check(!fixture.Book.TryStop(foreign, original), "Foreign book lease removed a listing.");
        await using var lease = await fixture.Book.AcquireAsync([12]);
        Check(fixture.Book.TryStop(lease, original), "Cannot stop original listing.");
        Check(fixture.Book.TryRestore(lease, 12, new(" seller ", 1), original.Snapshot, out var reopened) &&
            reopened is not null && reopened.Generation != original.Generation && reopened.Owner.AccountName == "SELLER",
            "Reopen did not normalize owner or increment generation.");
        Check(!fixture.Book.TryStop(lease, original), "Stale generation removed reopened listing.");
        Check(!fixture.Book.TryRestore(foreign, 13, new("SELLER", 1), original.Snapshot with { ConnectionId = 13 }, out _),
            "Foreign lease restored a listing.");
        await lease.DisposeAsync();
        Check(!fixture.Book.TryStop(lease, reopened!), "Disposed lease removed a listing.");
        // Snapshot arrays are copies, not mutable references to book state.
        ((LegacyItem[])reopened!.Snapshot.Items)[0] = default;
        Check(fixture.Book.Snapshot().Single().Snapshot.Items[0].Index == 900, "Handle exposed mutable book state.");
        var stale = await LegacyAutoTradePurchaseExecutor.ExecuteAsync(fixture.World, fixture.Book, new ImmediateCommitStore(), fixture.Request);
        Check(stale.Result == LegacyAutoTradePurchaseExecutionResult.ListingChanged,
            "Old purchase request entered a reopened identical shop.");
    }

    public static void PurchaseLeaseAndCancellation() => PurchaseLeaseAndCancellationAsync().GetAwaiter().GetResult();
    public static void PurchaseRequestSnapshot() => PurchaseRequestSnapshotAsync().GetAwaiter().GetResult();

    private static async Task PurchaseRequestSnapshotAsync()
    {
        var fixture = AutoTradeCloseFixtures.CreatePurchase();
        var held = new HeldCommitStore();
        var purchase = LegacyAutoTradePurchaseExecutor.ExecuteAsync(fixture.World, fixture.Book, held, fixture.Request).AsTask();
        await held.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        ((LegacyItem[])fixture.Request.Context.Listing.Items)[0] = new(901, 0, 0, 0, 0, 0, 0);
        held.Complete.SetResult(LegacyAutoTradePurchaseCommitResult.Conflict);
        var result = await purchase.WaitAsync(TimeSpan.FromSeconds(5));
        Check(result.Result == LegacyAutoTradePurchaseExecutionResult.CommitRejected &&
            fixture.World.TryGetNpcSnapshot(12, out var npc) && npc?.AutoTradeSnapshot?.Items[0].Index == 900 &&
            fixture.Book.Snapshot().Single().Snapshot.Items[0].Index == 900,
            "External request array mutation changed the rollback snapshot.");
    }

    private static async Task PurchaseLeaseAndCancellationAsync()
    {
        var fixture = AutoTradeCloseFixtures.CreatePurchase();
        var held = new HeldCommitStore();
        var first = LegacyAutoTradePurchaseExecutor.ExecuteAsync(fixture.World, fixture.Book, held, fixture.Request).AsTask();
        await held.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Check(fixture.World.TryGetCharacterSnapshot(7, out var during, out _, out _) && during is not null,
            "Buyer disappeared during durable commit.");
        using var cancellation = new CancellationTokenSource();
        var cancelledStore = new HeldCommitStore();
        var cancelled = LegacyAutoTradePurchaseExecutor.ExecuteAsync(
            fixture.World, fixture.Book, cancelledStore, fixture.Request, cancellation.Token).AsTask();
        Check(!cancelled.IsCompleted && !cancelledStore.Entered.Task.IsCompleted,
            "Second buyer operation entered the pending purchase.");
        cancellation.Cancel();
        try { await cancelled.WaitAsync(TimeSpan.FromSeconds(5)); throw new InvalidOperationException("Purchase waiter ignored cancellation."); }
        catch (OperationCanceledException) { }
        Check(!cancelledStore.Entered.Task.IsCompleted &&
            fixture.World.TryGetCharacterSnapshot(7, out var afterCancel, out _, out _) && afterCancel is not null &&
            afterCancel.SequenceEqual(during!), "Cancelled waiter touched durable or runtime state.");
        var secondStore = new HeldCommitStore();
        var second = LegacyAutoTradePurchaseExecutor.ExecuteAsync(fixture.World, fixture.Book, secondStore, fixture.Request).AsTask();
        Check(!second.IsCompleted, "Concurrent sale did not await the first purchase.");
        held.Complete.SetResult(LegacyAutoTradePurchaseCommitResult.Committed);
        var accepted = await first.WaitAsync(TimeSpan.FromSeconds(5));
        var stale = await second.WaitAsync(TimeSpan.FromSeconds(5));
        Check(accepted.Result == LegacyAutoTradePurchaseExecutionResult.Accepted &&
            stale.Result == LegacyAutoTradePurchaseExecutionResult.ListingChanged && !secondStore.Entered.Task.IsCompleted,
            "Second sale committed a stale listing.");
        Check(fixture.World.TryGetCharacterSnapshot(7, out var mob, out _, out _) && mob is not null &&
            BinaryPrimitives.ReadInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset)) == 50 &&
            LegacyItem.Read(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes)).Index == 900,
            "Concurrent sale charged or delivered twice.");
        var wrongOwner = await LegacyAutoTradePurchaseExecutor.ExecuteAsync(
            fixture.World, fixture.Book, new HeldCommitStore(), fixture.Request with { SellerAccountName = "OTHER" });
        Check(wrongOwner.Result == LegacyAutoTradePurchaseExecutionResult.ListingChanged, "Purchase ignored the listing owner.");
        Check(fixture.Book.Snapshot().Single().Generation == fixture.Request.ExpectedGeneration,
            "A sale changed the shop generation.");
    }

    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void FileCloseBatch() => FileCloseBatchAsync().GetAwaiter().GetResult();

    public static void CloseRequestSnapshot()
    {
        var expected = AutoTradeCloseFixtures.CreatePurchase().Request.ExpectedListing;
        var items = expected.Items.ToArray();
        var request = new LegacyAutoTradeCloseRequest([expected with { AccountName = " seller ", Items = items }]);
        items[0] = default;
        Check(request.ExpectedListings[0].AccountName == "SELLER" && request.ExpectedListings[0].Items[0].Index == 900,
            "Close request exposed input arrays or did not normalize owner.");
        Reject([]); Reject([expected, expected with { AccountName = " seller " }]);
        Reject([expected with { CharacterSlot = 4 }]); Reject([expected with { AccountName = "S\0" }]);
        Reject([expected with { Items = [] }]);
        void Reject(IReadOnlyList<LegacyAutoTradePersistedListing> listings)
        {
            try { _ = new LegacyAutoTradeCloseRequest(listings); }
            catch (ArgumentException) { return; }
            throw new InvalidOperationException("Invalid close request was accepted.");
        }
    }

    private static async Task FileCloseBatchAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "wyd-close-batch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "UP.json");
            var accountRoot = Path.Combine(root, "accounts");
            Directory.CreateDirectory(Path.Combine(accountRoot, "S"));
            var accountPaths = new[] { Path.Combine(accountRoot, "S", "SELLER1"), Path.Combine(accountRoot, "S", "SELLER2") };
            var blob = new byte[LegacyAccountSnapshot.RequiredFileLength];
            BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), 5_000_000);
            BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(LegacyAccountSnapshot.CharactersOffset + LegacyAccountSnapshot.MobCoinOffset), 150);
            new LegacyItem(900, 1, 2, 3, 4, 5, 6).Write(blob.AsSpan(LegacyAccountSnapshot.CargoOffset + 4 * LegacyItem.SizeInBytes));
            foreach (var accountPath in accountPaths) await File.WriteAllBytesAsync(accountPath, blob);
            var store = new LegacyAutoTradeFileStore(path, "UP", accountRoot);
            var fixture = AutoTradeCloseFixtures.CreatePurchase();
            var snapshot = fixture.Request.Context.Listing;
            Check(await store.SaveAsync("SELLER1", 0, snapshot) == LegacyAutoTradeStateResult.Saved, "Cannot save first shop.");
            Check(await store.SaveAsync("SELLER2", 0, snapshot) == LegacyAutoTradeStateResult.Saved, "Cannot save second shop.");
            var one = (await store.ReadAsync("SELLER1", 0))!;
            var two = (await store.ReadAsync("SELLER2", 0))!;
            var request = new LegacyAutoTradeCloseRequest([two, one]);
            Check(await store.ReadCloseStateAsync(request) == LegacyAutoTradeCloseReadState.AllUnchanged,
                "Existing close batch was not classified as unchanged.");
            var prices = two.Prices.ToArray(); prices[0] = 101;
            var stale = new LegacyAutoTradeCloseRequest([one, two with { Prices = prices }]);
            var before = await File.ReadAllBytesAsync(path);
            Check(await store.RemoveExpectedAsync(stale) == LegacyAutoTradeCloseStoreResult.Conflict,
                "Stale second listing did not reject the entire close batch.");
            var after = await File.ReadAllBytesAsync(path);
            Check(before.SequenceEqual(after), "Rejected close rewrote or partially deleted listings.");
            Check(await store.RemoveExpectedAsync(request) == LegacyAutoTradeCloseStoreResult.Removed,
                "Matching close batch was not removed.");
            var restarted = new LegacyAutoTradeFileStore(path, "UP", accountRoot);
            Check((await restarted.ReadAllAsync()).Count == 0 &&
                await restarted.ReadCloseStateAsync(request) == LegacyAutoTradeCloseReadState.AllAbsent,
                "Closed shops reappeared after restart.");
            Check(await restarted.RemoveExpectedAsync(request) == LegacyAutoTradeCloseStoreResult.AlreadyAbsent,
                "Repeated durable close was not idempotent.");
            await restarted.SaveAsync("SELLER1", 0, snapshot);
            Check(await restarted.ReadCloseStateAsync(request) == LegacyAutoTradeCloseReadState.Conflict,
                "Mixed close readback was treated as complete.");
            Check(await restarted.RemoveExpectedAsync(request) == LegacyAutoTradeCloseStoreResult.Removed,
                "Missing second shop prevented idempotent batch close.");
            await restarted.SaveAsync("SELLER1", 0, snapshot);
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            var beforeCancellation = await File.ReadAllBytesAsync(path);
            try { await restarted.RemoveExpectedAsync(request, cancelled.Token); throw new InvalidOperationException("Pre-cancelled close entered store."); }
            catch (OperationCanceledException) { }
            var afterCancellation = await File.ReadAllBytesAsync(path);
            Check(beforeCancellation.SequenceEqual(afterCancellation), "Cancelled close changed storage.");
            await File.WriteAllTextAsync(path, "{\"Version\":1,\"WorldKey\":\"UP\",\"Listings\":[null]}");
            Check(await restarted.ReadCloseStateAsync(request) == LegacyAutoTradeCloseReadState.Unknown &&
                await restarted.RemoveExpectedAsync(request) == LegacyAutoTradeCloseStoreResult.Unavailable,
                "Corrupt close state did not fail closed.");
            foreach (var accountPath in accountPaths)
            {
                var preserved = await File.ReadAllBytesAsync(accountPath);
                Check(preserved.SequenceEqual(blob), "Close changed an account blob, cargo or Gold.");
            }
            Check(fixture.World.TryGetCharacterSnapshot(7, out var mob, out _, out _) && mob is not null &&
                mob.SequenceEqual(fixture.Request.ExpectedBuyerMob), "Listing-only close changed buyer MOB.");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private sealed class HeldCommitStore : ILegacyAutoTradePurchaseCommitStore
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<LegacyAutoTradePurchaseCommitResult> Complete { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<LegacyAutoTradePurchaseCommitResult> TryCommitAsync(
            LegacyAutoTradePurchaseCommitRequest request, CancellationToken cancellationToken = default)
        {
            Entered.SetResult();
            return await Complete.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class ImmediateCommitStore : ILegacyAutoTradePurchaseCommitStore
    {
        public ValueTask<LegacyAutoTradePurchaseCommitResult> TryCommitAsync(
            LegacyAutoTradePurchaseCommitRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(LegacyAutoTradePurchaseCommitResult.Committed);
    }
}

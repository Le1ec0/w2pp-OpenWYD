using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;
using static AutoTradeCloseTests;

internal static class AutoTradeCloseCoordinatorTests
{
    public static void DepartureCapturesBeforeLeave() => RunDepartureCapture().GetAwaiter().GetResult();
    public static void DisconnectPreservesPendingRecovery() => RunPendingDeparture().GetAwaiter().GetResult();
    public static void StaleCleanupConflictAndReusedId() => RunStaleConflict().GetAwaiter().GetResult();
    public static void StaleCleanupAmbiguityBlocksOwner() => RunStaleAmbiguity().GetAwaiter().GetResult();
    public static void DepartureDoesNotLeaveReplacement() => RunDepartureReplacement().GetAwaiter().GetResult();
    public static void PendingCloseRejectsOutsideOwner() => RunOutsideOwner().GetAwaiter().GetResult();

    private static async Task RunOutsideOwner()
    {
        var fixture = Create();
        await using var lease = await fixture.Book.AcquireAsync([7, 12, 13]);
        var handles = fixture.Book.Snapshot().ToArray();
        var first = handles.Single(handle => handle.Snapshot.ConnectionId == 7);
        Check(fixture.Book.TryRestore(lease, 13, first.Owner, first.Snapshot with { ConnectionId = 13 }, out _),
            "Cannot create outside owner generation.");
        Check(fixture.World.TryReserveTradeClose(7, 12, out var reservation) == LegacyTradeClosePrepareResult.Prepared && reservation is not null,
            "Cannot reserve close for outside owner test.");
        var request = new LegacyAutoTradeCloseRequest(handles.Select(handle => new LegacyAutoTradePersistedListing(
            handle.Owner.AccountName, handle.Owner.CharacterSlot, handle.Snapshot.PositionX, handle.Snapshot.PositionY,
            handle.Snapshot.Title, handle.Snapshot.Items, handle.Snapshot.CarryPositions, handle.Snapshot.Prices, handle.Snapshot.Tax)).ToArray());
        var pending = new LegacyAutoTradePendingClose(reservation!, request, handles)
        {
            AffectedOwners = handles.Select(handle => handle.Owner).ToArray(),
        };
        Check(!fixture.Book.TrySetPendingClose(lease, pending) && !fixture.Book.IsOwnerPending(first.Owner),
            "Outside owner generation entered close barrier.");
        Check(fixture.World.TryAbortTradeClose(reservation!), "Cannot release rejected close reservation.");
    }

    private static async Task RunDepartureReplacement()
    {
        var fixture = Create();
        var replacement = fixture.First.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(replacement.AsSpan(LegacyAccountSnapshot.MobCoinOffset), 999);
        var plan = await LegacyAutoTradeDepartureCoordinator.DepartAsync(7, fixture.World, fixture.Book, ct => ValueTask.CompletedTask,
            (shop, ct) =>
            {
                fixture.World.Leave(7);
                Check(fixture.World.Enter(7, "REUSED", (_, _) => ValueTask.CompletedTask) &&
                    fixture.World.SetCharacterState(7, 0, 0, 0, 0, 999, 2300, 2301, replacement), "Cannot replace departing participant.");
                return ValueTask.CompletedTask;
            }, LegacyFrameCodec.CreateDefault(), 1, 0);
        Check(plan.Frames.Count == 0 && fixture.World.TryGetCharacterSnapshot(7, out var mob, out var x, out var y) && mob is not null &&
            x == 2300 && y == 2301 && BinaryPrimitives.ReadInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset)) == 999,
            "Old departure removed or relayed to a replacement participant.");
    }

    private static async Task RunStaleConflict()
    {
        foreach (var reuse in new[] { false, true })
        {
            var fixture = Create(); await using var store = await HeldStore.Create(fixture);
            fixture.World.Leave(7); fixture.Sessions.Close(7);
            if (reuse) Check(fixture.World.Enter(7, "REUSED", (_, _) => ValueTask.CompletedTask), "Cannot reuse stale id without a MOB.");
            else await store.SaveAsync("OWNER7", 0, fixture.Book.Snapshot().Single(handle => handle.Snapshot.ConnectionId == 7).Snapshot with { Title = "CHANGED" });
            var result = await LegacyAutoTradeReconnectCoordinator.CloseAndRecoverAsync(fixture.Sessions, fixture.World, fixture.Book,
                store, "OWNER7", 0, LegacyFrameCodec.CreateDefault(), 1, 0);
            Check(result.Outcome.Result == LegacyAutoTradeReconnectResult.PersistenceUnavailable && result.RecoveryFrames.Count == 0 &&
                fixture.Book.Snapshot().Count == 2 && (await store.ReadAllAsync()).Count == 2 && store.BlindRemoveCalls == 0 &&
                (!reuse || fixture.World.Count == 2), "Stale cleanup removed conflicting state or touched a reused id.");
        }
    }

    private static async Task RunStaleAmbiguity()
    {
        var fixture = Create(); await using var store = await HeldStore.Create(fixture);
        store.ResultOverride = LegacyAutoTradeCloseStoreResult.Ambiguous; store.ReadOverride = LegacyAutoTradeCloseReadState.Unknown;
        Check((await Close(fixture, store)).Result == LegacyAutoTradeCloseResult.PendingRecovery, "Cannot create stale pending close.");
        fixture.World.Leave(7); fixture.Sessions.Close(7);
        store.ReadOverride = null; store.CommitBeforeOverride = true;
        store.AfterCommit = () => store.ReadOverride = LegacyAutoTradeCloseReadState.Unknown;
        var codec = LegacyFrameCodec.CreateDefault();
        var unresolved = await LegacyAutoTradeReconnectCoordinator.CloseAndRecoverAsync(fixture.Sessions, fixture.World, fixture.Book,
            store, "OWNER7", 0, codec, 1, 0);
        Check(unresolved.Outcome.Result == LegacyAutoTradeReconnectResult.PersistenceUnavailable && unresolved.RecoveryFrames.Count == 0 &&
            fixture.Book.IsOwnerPending(new("OWNER7", 0)) && !fixture.Book.IsOwnerPending(new("OWNER12", 0)) &&
            (await store.ReadAllAsync()).Single().AccountName == "OWNER12" && store.DeleteCalls == 2 && store.BlindRemoveCalls == 0,
            "Ambiguous stale cleanup lost its owner barrier or did not use expected delete.");
        await using var lease = await fixture.Book.AcquireAsync([13]);
        var stale = fixture.Book.Snapshot().Single(handle => handle.Owner.AccountName == "OWNER7").Snapshot;
        Check(!fixture.Book.TryRestore(lease, 13, new("OWNER7", 0), stale with { ConnectionId = 13 }, out _), "Unknown cleanup allowed the same owner to reopen.");
        await lease.DisposeAsync();
        Check(fixture.World.Enter(7, "REUSED", (_, _) => ValueTask.CompletedTask), "Cannot reuse detached runtime id.");
        store.ReadOverride = null;
        var resolved = await LegacyAutoTradeReconnectCoordinator.CloseAndRecoverAsync(fixture.Sessions, fixture.World, fixture.Book,
            store, "OWNER7", 0, codec, 2, 0);
        Check(resolved.Outcome.Result == LegacyAutoTradeReconnectResult.NoListing && resolved.RecoveryFrames.Count == 0 &&
            !fixture.Book.IsOwnerPending(new("OWNER7", 0)) && fixture.Book.Snapshot().Single().Owner.AccountName == "OWNER12" &&
            fixture.World.Count == 2 && store.DeleteCalls == 2, "Detached recovery touched a replacement or retried a confirmed delete.");
    }

    private static async Task RunDepartureCapture()
    {
        var fixture = Create(failSend: true); await using var store = await HeldStore.Create(fixture);
        foreach (var (id, x) in new[] { (13, 2128), (14, 2129) })
            Check(fixture.World.Enter(id, "EDGE", (_, _) => ValueTask.CompletedTask) &&
                fixture.World.SetCharacterState(id, 0, 0, 0, 0, 0, (short)x, 2042, fixture.First), "Cannot create departure view.");
        var codec = LegacyFrameCodec.CreateDefault();
        var plan = await LegacyAutoTradeDepartureCoordinator.DepartAsync(7, fixture.World, fixture.Book, _ => ValueTask.CompletedTask,
            async (shop, ct) => { Check(shop?.ConnectionId == 7, "Departure lost its own listing."); await store.RemoveAsync("OWNER7", 0, ct); }, codec, 432, 5);
        Check(plan.Frames.Count == 1 && plan.Frames[0].RecipientIds.Order().SequenceEqual(new[] {12, 13}) &&
            plan.Frames[0].Frame.Length == 16 && codec.Decode(plan.Frames[0].Frame).Header.ClientTick == 432 &&
            !fixture.World.TryGetCharacterSnapshot(7, out _, out _, out _) && fixture.Book.Snapshot().Single().Owner.AccountName == "OWNER12" &&
            (await store.ReadAllAsync()).Single().AccountName == "OWNER12", "Departure did not capture RemoveMob/view before Leave.");
        await using var released = await fixture.Book.AcquireAsync([7]);
        await released.DisposeAsync();
        var failed = false;
        try { await fixture.World.SendAsync(12, plan.Frames[0].Frame, CancellationToken.None); } catch (IOException) { failed = true; }
        Check(failed && (await store.ReadAllAsync()).Count == 1 && fixture.Book.Snapshot().Count == 1, "Send failure restored a departed shop.");
    }

    private static async Task RunPendingDeparture()
    {
        var fixture = Create(); await using var store = await HeldStore.Create(fixture);
        store.ResultOverride = LegacyAutoTradeCloseStoreResult.Ambiguous; store.ReadOverride = LegacyAutoTradeCloseReadState.Unknown;
        Check((await Close(fixture, store)).Result == LegacyAutoTradeCloseResult.PendingRecovery, "Cannot create pending departure.");
        var plan = await LegacyAutoTradeDepartureCoordinator.DepartAsync(7, fixture.World, fixture.Book, _ => ValueTask.CompletedTask,
            (shop, _) => { Check(shop is null, "Disconnect blindly deleted pending shop state."); return ValueTask.CompletedTask; },
            LegacyFrameCodec.CreateDefault(), 1, 0);
        fixture.Sessions.Close(7);
        Check(plan.Frames.Count == 0 && !fixture.World.TryGetCharacterSnapshot(7, out _, out _, out _) &&
            fixture.Book.IsOwnerPending(new("OWNER7", 0)) && fixture.Book.IsOwnerPending(new("OWNER12", 0)) &&
            fixture.Book.Snapshot().Count == 2 && (await store.ReadAllAsync()).Count == 2, "Disconnect lost unresolved close state or exposed success.");
    }

    public static void ListingCaptureFreezesBeforeSending() => RunListingCapture().GetAwaiter().GetResult();
    public static void ListingCaptureOfflineAndLeaseValidation() => RunOfflineListingCapture().GetAwaiter().GetResult();

    private static async Task RunListingCapture()
    {
        var fixture = Create(); var codec = LegacyFrameCodec.CreateDefault();
        Check(fixture.World.Enter(13, "EDGE", (_, _) => ValueTask.CompletedTask) &&
            fixture.World.SetCharacterState(13, 0, 0, 0, 0, 0, 2128, 2042, fixture.First) &&
            fixture.World.Enter(14, "OUTSIDE", (_, _) => ValueTask.CompletedTask) &&
            fixture.World.SetCharacterState(14, 0, 0, 0, 0, 0, 2129, 2042, fixture.First), "Cannot create listing neighbors.");
        await using var lease = await fixture.Book.AcquireAsync([7, 12]);
        Check(LegacyAutoTradeListingRelay.TryCapture(fixture.World, fixture.Book, lease, 7, 7, true, codec, 123, 4, out var opened) &&
            opened.Count == 2, "Listing capture omitted protected opening frames.");
        Check(opened[0].RecipientIds.SequenceEqual([7]) && opened[1].RecipientIds.Order().SequenceEqual(new[] {7, 12, 13}),
            "Opening capture changed owner/view recipients.");
        Check(LegacyAutoTradeListingRelay.TryCapture(fixture.World, fixture.Book, lease, 12, 7, false, codec, 124, 4, out var queried) &&
            queried.Count == 2 && queried.All(plan => plan.RecipientIds.SequenceEqual([12])), "Query visual leaked to shop neighbors.");
        foreach (var plans in new[] { opened, queried })
            Check(plans[0].Frame.Length == 196 && plans[1].Frame.Length == 260 && plans.All(plan => codec.Decode(plan.Frame).IsChecksumValid),
                "Listing capture changed 7.69 frame sizes/checksum.");
        Check(fixture.Book.TryGet(lease, 7, out var old) && old is not null && fixture.Book.TryStop(lease, old) &&
            fixture.Book.TryRestore(lease, 7, new("OWNER7", 0), old.Snapshot with { Title = "REOPENED" }, out _), "Cannot reopen captured listing.");
        await lease.DisposeAsync();
        Check(!LegacyAutoTradeListingRelay.TryCapture(fixture.World, fixture.Book, lease, 7, 7, true, codec, 0, 0, out var invalid) && invalid.Count == 0,
            "Disposed lease captured a reopened generation.");
        Check(AutoTradeListConfirmation.TryParse(codec.Decode(opened[0].Frame), out var list) && list?.Description == "TEST", "Captured list was rebuilt after reopening.");
        await using var sendingLease = await fixture.Book.AcquireAsync([7, 12]);
        Check(sendingLease.Covers(7), "Listing lease leaked into transport phase.");
    }

    private static async Task RunOfflineListingCapture()
    {
        var (world, book, _) = AutoTradeCloseFixtures.CreatePurchase(); var codec = LegacyFrameCodec.CreateDefault();
        await using var shopOnly = await book.AcquireAsync([12]);
        Check(!LegacyAutoTradeListingRelay.TryCapture(world, book, shopOnly, 7, 12, false, codec, 0, 0, out var invalid) && invalid.Count == 0,
            "Listing capture accepted an unprotected requester.");
        await shopOnly.DisposeAsync();
        await using var lease = await book.AcquireAsync([7, 12]);
        Check(LegacyAutoTradeListingRelay.TryCapture(world, book, lease, 7, 12, false, codec, 55, 3, out var frames) && frames.Count == 2 &&
            frames.All(plan => plan.RecipientIds.SequenceEqual([7])) && frames[1].Frame.Length == 260 && codec.Decode(frames[1].Frame).Header.ClientTick == 55,
            "Offline listing capture omitted NPC visual or changed its recipient.");
        var other = new LegacyAutoTradeBook(); await using var foreign = await other.AcquireAsync([7, 12]);
        Check(!LegacyAutoTradeListingRelay.TryCapture(world, book, foreign, 7, 12, false, codec, 0, 0, out invalid) && invalid.Count == 0,
            "Foreign book lease captured an offer.");
    }

    public static void OwnerRecoveryAfterDisconnect() => RunOwnerRecovery(false).GetAwaiter().GetResult();
    public static void OwnerRecoveryWithoutOwnListing() => RunOwnerRecovery(true).GetAwaiter().GetResult();
    public static void OwnerRecoveryUnknownAndUnchanged() => RunOwnerUnchanged().GetAwaiter().GetResult();
    public static void OwnerRecoveryCapturesOriginalAuthor() => RunLiveOwnerRecovery().GetAwaiter().GetResult();

    private static async Task RunLiveOwnerRecovery()
    {
        var fixture = Create(); await using var store = await HeldStore.Create(fixture);
        store.ResultOverride = LegacyAutoTradeCloseStoreResult.Ambiguous;
        store.ReadOverride = LegacyAutoTradeCloseReadState.Unknown;
        Check((await Close(fixture, store)).Result == LegacyAutoTradeCloseResult.PendingRecovery, "Cannot create live pending owner.");
        await store.File.RemoveExpectedAsync(store.LastRequest!); store.ReadOverride = null;
        var codec = LegacyFrameCodec.CreateDefault();
        var plan = await LegacyAutoTradeReconnectCoordinator.CloseAndRecoverAsync(fixture.Sessions, fixture.World, fixture.Book,
            store, " owner12 ", 0, codec, 543, 2);
        Check(plan.Outcome.Result == LegacyAutoTradeReconnectResult.NoListing && plan.RecoveryFrames.Count == 5 &&
            codec.Decode(plan.RecoveryFrames.Single(frame => codec.Decode(frame.Frame).Header.Type == 0x0366).Frame).Header.Id == 7 &&
            plan.RecoveryFrames.All(frame => codec.Decode(frame.Frame).Header.ClientTick == 543), "Owner reconnect attributed close relays to a new caller.");
        await using var lease = await fixture.Book.AcquireAsync([7, 12]);
        Check(!fixture.Book.IsOwnerPending(new("OWNER12", 0)) && store.DeleteCalls == 1, "Reconnect kept leases or repeated delete.");
    }

    private static async Task RunOwnerRecovery(bool withoutListing)
    {
        var fixture = Create();
        if (withoutListing)
        {
            await using var lease = await fixture.Book.AcquireAsync([7]);
            Check(fixture.Book.TryGet(lease, 7, out var handle) && handle is not null && fixture.Book.TryStop(lease, handle), "Cannot remove author shop.");
        }
        await using var store = await HeldStore.Create(fixture);
        store.ResultOverride = LegacyAutoTradeCloseStoreResult.Ambiguous;
        store.ReadOverride = LegacyAutoTradeCloseReadState.Unknown;
        Check((await Close(fixture, store)).Result == LegacyAutoTradeCloseResult.PendingRecovery, "Cannot create pending owner.");
        fixture.World.Leave(7); fixture.Sessions.Close(7);
        await store.File.RemoveExpectedAsync(store.LastRequest!); store.ReadOverride = null;
        var recovered = await LegacyAutoTradeCloseRecovery.RecoverOwnerAndCaptureAsync(new(" owner7 ", 0),
            fixture.Sessions, fixture.World, fixture.Book, store, LegacyFrameCodec.CreateDefault(), 321, 0);
        Check(recovered.Outcome.Result == LegacyAutoTradeCloseResult.Closed && recovered.Frames.Count == 0 &&
            !fixture.Book.IsOwnerPending(new("OWNER7", 0)) && !fixture.Book.IsOwnerPending(new("OWNER12", 0)) && fixture.Book.Snapshot().Count == 0,
            "Owner recovery did not resolve the saved pair after disconnect.");
        Check(store.DeleteCalls == 1 && (await store.ReadAllAsync()).Count == 0, "Owner recovery retried a blind durable delete.");
        var reconnected = await LegacyAutoTradeReconnectCoordinator.CloseAndRecoverAsync(fixture.Sessions, fixture.World, fixture.Book,
            store, "OWNER7", 0, LegacyFrameCodec.CreateDefault(), 322, 0);
        Check(reconnected.Outcome.Result == LegacyAutoTradeReconnectResult.NoListing && reconnected.RecoveryFrames.Count == 0,
            "Resolved disconnected owner remained blocked or received old success frames.");
    }

    private static async Task RunOwnerUnchanged()
    {
        var fixture = Create(); await using var store = await HeldStore.Create(fixture);
        store.ResultOverride = LegacyAutoTradeCloseStoreResult.Ambiguous;
        store.ReadOverride = LegacyAutoTradeCloseReadState.Unknown;
        Check((await Close(fixture, store)).Result == LegacyAutoTradeCloseResult.PendingRecovery, "Cannot create pending owner.");
        var codec = LegacyFrameCodec.CreateDefault();
        var unknown = await LegacyAutoTradeCloseRecovery.RecoverOwnerAndCaptureAsync(new("OWNER12", 0), fixture.Sessions, fixture.World, fixture.Book, store, codec, 123, 0);
        Check(unknown.Outcome.Result == LegacyAutoTradeCloseResult.PendingRecovery && unknown.Frames.Count == 0 && fixture.Book.IsOwnerPending(new("OWNER7", 0)), "Unknown owner recovery exposed success.");
        store.ReadOverride = LegacyAutoTradeCloseReadState.Conflict;
        var conflict = await LegacyAutoTradeCloseRecovery.RecoverOwnerAndCaptureAsync(new("OWNER7", 0), fixture.Sessions, fixture.World, fixture.Book, store, codec, 123, 0);
        Check(conflict.Outcome.Result == LegacyAutoTradeCloseResult.PendingRecovery && conflict.Frames.Count == 0 && fixture.Book.IsOwnerPending(new("OWNER12", 0)), "Conflicting owner recovery cleared pending.");
        store.ReadOverride = null;
        var unchanged = await LegacyAutoTradeCloseRecovery.RecoverOwnerAndCaptureAsync(new("OWNER12", 0), fixture.Sessions, fixture.World, fixture.Book, store, codec, 123, 0);
        Check(unchanged.Outcome.Result == LegacyAutoTradeCloseResult.PersistenceUnavailable && unchanged.Frames.Count == 0 &&
            !fixture.Book.IsOwnerPending(new("OWNER7", 0)) && fixture.Book.Snapshot().Count == 2 && fixture.World.GetTradeOpponentId(7) == 12 &&
            (await store.ReadAllAsync()).Count == 2 && store.DeleteCalls == 1, "Unchanged recovery deleted shops or reported success.");
        var rejected = await LegacyAutoTradeReconnectCoordinator.CloseAndRecoverAsync(fixture.Sessions, fixture.World, fixture.Book, store, "OWNER7", 0, codec, 123, 0);
        Check(rejected.Outcome.Result == LegacyAutoTradeReconnectResult.PersistenceUnavailable && rejected.RecoveryFrames.Count == 0 &&
            (await store.ReadAllAsync()).Count == 2, "Reconnect blindly deleted an unchanged online listing.");
        fixture.World.Leave(7); fixture.Sessions.Close(7);
        store.ResultOverride = null;
        rejected = await LegacyAutoTradeReconnectCoordinator.CloseAndRecoverAsync(fixture.Sessions, fixture.World, fixture.Book, store, "OWNER7", 0, codec, 123, 0);
        Check(rejected.Outcome.Result == LegacyAutoTradeReconnectResult.Closed && rejected.RecoveryFrames.Count == 0 &&
            (await store.ReadAllAsync()).Single().AccountName == "OWNER12" && fixture.Book.Snapshot().Single().Owner.AccountName == "OWNER12" &&
            store.DeleteCalls == 2 && store.BlindRemoveCalls == 0, "Reconnect did not conditionally clean the detached owner after AllUnchanged.");
    }

    public static void SharedSessionRegistryConcurrentConnections()
    {
        var sessions = new LoginSessionRegistry();
        Parallel.For(1, 4097, new ParallelOptions { MaxDegreeOfParallelism = 16 }, id =>
        {
            Check(sessions.Open(id), "Shared registry rejected a unique connection.");
            Check(sessions.BeginAccountLogin(id, new AccountLoginRequest("OWNER" + id, "synthetic", new byte[52], 7670, 0, [0, 0, 0, 0])) == LoginTransitionResult.Accepted &&
                sessions.CompleteAccountLogin(id, "OWNER" + id) == LoginTransitionResult.Accepted && sessions.BeginCharacterWait(id) == LoginTransitionResult.Accepted &&
                sessions.CompleteCharacterLogin(id, 0, 2112, 2042) == LoginTransitionResult.Accepted, "Shared registry lost concurrent transitions.");
        });
        for (var id = 1; id <= 4096; id++)
            Check(sessions.TryGet(id, out var session) && session?.State == LoginSessionState.Playing && session.AccountName == "OWNER" + id,
                "Shared registry lost a concurrent participant or mixed owners.");
        Parallel.For(1, 4097, id => Check(sessions.Close(id), "Shared registry lost a concurrent close."));
        for (var id = 1; id <= 4096; id++) Check(!sessions.TryGet(id, out _), "Closed connection remained visible.");
    }

    public static void CloseDispatchCapturesBeforeReuse() => RunDispatch().GetAwaiter().GetResult();
    public static void CloseDispatchRecoveryUsesOriginalAuthor() => RunRecoveryDispatch().GetAwaiter().GetResult();

    private static async Task RunDispatch()
    {
        var fixture = Create(failSend: true);
        await using var store = await HeldStore.Create(fixture);
        store.Hold = true;
        var codec = LegacyFrameCodec.CreateDefault();
        var closing = LegacyAutoTradeCloseCoordinator.CloseAndCaptureAsync(7, fixture.Sessions, fixture.World, fixture.Book, store, codec, 777, 9).AsTask();
        await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var waiting = fixture.Book.AcquireAsync([7, 12]).AsTask();
        Check(!closing.IsCompleted && !waiting.IsCompleted, "Close released the pair before durable delete.");
        store.Release.SetResult();
        var plan = await closing.WaitAsync(TimeSpan.FromSeconds(5));
        Check(plan.Outcome.Result == LegacyAutoTradeCloseResult.Closed && plan.Frames.Count == 5, "Close dispatch did not capture frames before returning.");
        await using var lease = await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        // Reopening immediately after return cannot change the already captured wire.
        Check(fixture.Book.TryRestore(lease, 7, new("OWNER7", 0), plan.Outcome.ClosedListings[0].Snapshot with { Title = "REOPENED" }, out _), "Dispatch retained operation leases during sending.");
        Check(fixture.World.SetCharacterState(7, 0, 0, 0, 0, 150, 2200, 2200, fixture.First), "Cannot move reopened participant.");
        var visual = plan.Frames.Select(frame => codec.Decode(frame.Frame)).Single(frame => frame.Header.Type == 0x0364 && BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span.Slice(4, 2)) == 7);
        Check(visual.Header.ClientTick == 777 && visual.Header.Size == 236 && BinaryPrimitives.ReadInt16LittleEndian(visual.Payload.Span) == 2112 &&
            BinaryPrimitives.ReadInt16LittleEndian(visual.Payload.Span.Slice(2, 2)) == 2042, "Dispatch recaptured a reopened/moved participant.");
        await lease.DisposeAsync();
        var failed = false;
        try { await fixture.World.SendAsync(7, plan.Frames[0].Frame, CancellationToken.None); }
        catch (IOException) { failed = true; }
        Check(failed && (await store.ReadAllAsync()).Count == 0 && fixture.Book.Snapshot().Single().Snapshot.Title == "REOPENED",
            "Failed send restored durable shops or overwrote a reopened generation.");
    }

    private static async Task RunRecoveryDispatch()
    {
        var fixture = Create(); await using var store = await HeldStore.Create(fixture);
        store.ResultOverride = LegacyAutoTradeCloseStoreResult.Ambiguous; store.ReadOverride = LegacyAutoTradeCloseReadState.Unknown;
        var codec = LegacyFrameCodec.CreateDefault();
        var pending = await LegacyAutoTradeCloseCoordinator.CloseAndCaptureAsync(7, fixture.Sessions, fixture.World, fixture.Book, store, codec, 1, 0);
        Check(pending.Outcome.Result == LegacyAutoTradeCloseResult.PendingRecovery && pending.Frames.Count == 0, "Pending dispatch exposed success frames.");
        await store.File.RemoveExpectedAsync(store.LastRequest!); store.ReadOverride = null;
        var recovered = await LegacyAutoTradeCloseCoordinator.CloseAndCaptureAsync(12, fixture.Sessions, fixture.World, fixture.Book, store, codec, 555, 3);
        Check(recovered.Outcome.Result == LegacyAutoTradeCloseResult.Closed && recovered.Frames.Count == 5 &&
            codec.Decode(recovered.Frames.Single(frame => codec.Decode(frame.Frame).Header.Type == 0x0366).Frame).Header.Id == 7,
            "Recovery dispatch omitted frames or attributed PK to the retry requester.");
        fixture.CheckMobs();
    }

    public static void CloseRelayNormalAndRecipients() => RunRelay().GetAwaiter().GetResult();
    public static void ActionClosePersistsAndUsesActionRelay() => RunActionClose().GetAwaiter().GetResult();
    public static void ActionCloseSoloNoopAndHpBoundary() => RunActionCloseEdges().GetAwaiter().GetResult();
    public static void ActionCloseRecoveryKeepsActionRelay() => RunActionCloseRecovery().GetAwaiter().GetResult();

    private static async Task RunRelay()
    {
        var fixture = Create();
        foreach (var (id, x, y) in new[] { (13, 2128, 2058), (14, 2129, 2042), (15, 2112, 2059) })
            Check(fixture.World.Enter(id, "NEIGHBOR", (_, _) => ValueTask.CompletedTask) &&
                fixture.World.SetCharacterState(id, 0, 0, 0, 0, 0, (short)x, (short)y, fixture.First.ToArray()), "Cannot create relay neighbor.");
        var outcome = await Close(fixture, null);
        var codec = LegacyFrameCodec.CreateDefault();
        Check(LegacyAutoTradeCloseRelay.TryBuild(fixture.World, outcome, codec, 1234, 7, out var plans), "Close relay did not build a success plan.");
        var visuals = plans.Where(plan => codec.Decode(plan.Frame).Header.Type == 0x0364).ToArray();
        Check(visuals.Length == 2, "Close relay did not restore both normal visuals.");
        var ids = new HashSet<int>();
        foreach (var plan in visuals)
        {
            var decoded = codec.Decode(plan.Frame);
            Check(decoded.IsChecksumValid && decoded.Header.Size == 236 && plan.Frame.Length == 236 && decoded.Header.ClientTick == 1234,
                "Close relay emitted a shop/remove frame instead of normal 7.69 CreateMob.");
            ids.Add(BinaryPrimitives.ReadUInt16LittleEndian(decoded.Payload.Span.Slice(4, 2)));
            Check(plan.RecipientIds.Order().SequenceEqual(new[] { 7, 12, 13 }), "Close visual recipients did not include owner/distance16 or excluded distance17 incorrectly.");
        }
        Check(ids.SetEquals([7, 12]), "Close visuals changed participant identities.");
        var signals = plans.Where(plan => codec.Decode(plan.Frame).Header.Type == 0x0384).ToArray();
        Check(signals.Length == 2 && signals.All(plan => plan.Frame.Length == 12 && plan.RecipientIds.Single() == codec.Decode(plan.Frame).Header.Id),
            "Close signals were not addressed to both affected participants.");
        var pk = plans.Single(plan => codec.Decode(plan.Frame).Header.Type == 0x0366);
        Check(pk.Frame.Length == 16 && codec.Decode(pk.Frame).Header.Id == 7 && pk.RecipientIds.Order().SequenceEqual(new[] {7, 12, 13}), "Close PK did not use author/view.");
        var retry = await Close(fixture, null);
        Check(LegacyAutoTradeCloseRelay.TryBuild(fixture.World, retry, codec, 1, 0, out var repeated) &&
            repeated.Count == 2 && repeated.All(plan => codec.Decode(plan.Frame).Header.Type != 0x0364), "Retry duplicated closed shop visuals.");
        foreach (var result in new[] { LegacyAutoTradeCloseResult.NotAlive, LegacyAutoTradeCloseResult.PendingRecovery, LegacyAutoTradeCloseResult.Conflict })
            Check(!LegacyAutoTradeCloseRelay.TryBuild(fixture.World, outcome with { Result = result }, codec, 1, 0, out var rejected) && rejected.Count == 0,
                "Rejected close exposed a success relay.");
        Check(!LegacyAutoTradeCloseRelay.TryBuild(fixture.World, outcome with { Trade = null, ClosedListings = [] }, codec, 1, 0, out var departed) && departed.Count == 0,
            "Departed session received an old close relay.");
        fixture.CheckMobs();
    }

    private static async Task RunActionClose()
    {
        var fixture = SetHp(Create(), -1);
        await using var store = await HeldStore.Create(fixture);
        foreach (var (id, x, y) in new[] { (13, 2128, 2058), (14, 2129, 2042), (15, 2112, 2059) })
            Check(fixture.World.Enter(id, "NEIGHBOR", (_, _) => ValueTask.CompletedTask) &&
                fixture.World.SetCharacterState(id, 0, 0, 0, 0, 0, (short)x, (short)y, fixture.First.ToArray()), "Cannot create Action relay neighbor.");

        var codec = LegacyFrameCodec.CreateDefault();
        var dispatch = await LegacyAutoTradeCloseCoordinator.CloseForActionAndCaptureAsync(
            7, fixture.Sessions, fixture.World, fixture.Book, store, codec);
        Check(dispatch.Outcome.Result == LegacyAutoTradeCloseResult.Closed && dispatch.Outcome.ActionTriggered &&
            dispatch.Outcome.Trade?.OpponentId == 12 && dispatch.Outcome.ClosedListings.Count == 2,
            "Negative-HP Action did not durably close both active shops and the bilateral trade.");
        Check(fixture.Book.Snapshot().Count == 0 && !fixture.World.IsLiveTradeActive(7) && !fixture.World.IsLiveTradeActive(12) &&
            (await store.ReadAllAsync()).Count == 0 && store.BlindRemoveCalls == 0,
            "Action close left a runtime/durable shop or one side of the trade active.");

        var signals = dispatch.Frames.Where(plan => codec.Decode(plan.Frame).Header.Type == 0x0384).ToArray();
        Check(signals.Length == 2 && signals.Select(plan => codec.Decode(plan.Frame).Header.Id).Order().SequenceEqual(new ushort[] { 7, 12 }) &&
            signals.All(plan => plan.Frame.Length == 12 && codec.Decode(plan.Frame).IsChecksumValid &&
                codec.Decode(plan.Frame).Header.ClientTick == 0 && codec.Decode(plan.Frame).Header.KeywordIndex == 0 &&
                plan.RecipientIds.SequenceEqual([codec.Decode(plan.Frame).Header.Id])),
            "Action close did not emit exact private, zero-header RemoveTrade signals to both participants.");
        var visuals = dispatch.Frames.Where(plan => codec.Decode(plan.Frame).Header.Type == 0x0364).ToArray();
        Check(visuals.Length == 2 && visuals.All(plan => plan.Frame.Length == 236 && codec.Decode(plan.Frame).IsChecksumValid &&
            codec.Decode(plan.Frame).Header.ClientTick == 0 && codec.Decode(plan.Frame).Header.KeywordIndex == 0 &&
            plan.RecipientIds.Order().SequenceEqual([7, 12, 13])) &&
            visuals.Select(plan => BinaryPrimitives.ReadUInt16LittleEndian(codec.Decode(plan.Frame).Payload.Span[4..])).Order().SequenceEqual(new ushort[] { 7, 12 }),
            "Action close did not restore one normal CreateMob per shop owner to the exact view.");
        Check(!dispatch.Frames.Any(plan => codec.Decode(plan.Frame).Header.Type is 0x0363 or 0x0366 or 0x0165),
            "Action close emitted a shop visual, PK refresh, or RemoveMob instead of normal CreateMob.");
        fixture.CheckMobs();
    }

    private static async Task RunActionCloseEdges()
    {
        var solo = SetHp(Create(includeTrade: false, includeSecondShop: false), -1);
        await using (var store = await HeldStore.Create(solo))
        {
            var dispatch = await LegacyAutoTradeCloseCoordinator.CloseForActionAndCaptureAsync(
                7, solo.Sessions, solo.World, solo.Book, store, LegacyFrameCodec.CreateDefault());
            Check(dispatch.Outcome.Result == LegacyAutoTradeCloseResult.Closed && dispatch.Outcome.ActionTriggered &&
                dispatch.Outcome.Trade is { OpponentId: 0 } && dispatch.Outcome.ClosedListings.Count == 1 &&
                dispatch.Frames.Count == 2 && (await store.ReadAllAsync()).Count == 0,
                "Action without a bilateral trade did not close only its own AFK shop.");
            var codec = LegacyFrameCodec.CreateDefault();
            Check(dispatch.Frames.Count(plan => codec.Decode(plan.Frame).Header.Type == 0x0384) == 1 &&
                dispatch.Frames.Count(plan => codec.Decode(plan.Frame).Header.Type == 0x0364) == 1 &&
                dispatch.Frames.All(plan => codec.Decode(plan.Frame).Header.Type != 0x0366),
                "Solo Action shop close sent the wrong signal/visual or an unrelated PK refresh.");
            var noop = await LegacyAutoTradeCloseCoordinator.CloseForActionAndCaptureAsync(
                7, solo.Sessions, solo.World, solo.Book, store, codec);
            Check(noop.Outcome.Result == LegacyAutoTradeCloseResult.NoShop && noop.Outcome.ActionTriggered &&
                noop.Outcome.Trade is null && noop.Frames.Count == 0,
                "Action with no shop or bilateral trade was not a movement-preserving no-op.");
        }

        var dead = Create(includeTrade: false, includeSecondShop: false);
        dead = SetHp(dead, 0);
        await using var deadStore = await HeldStore.Create(dead);
        var deadDispatch = await LegacyAutoTradeCloseCoordinator.CloseForActionAndCaptureAsync(
            7, dead.Sessions, dead.World, dead.Book, deadStore, LegacyFrameCodec.CreateDefault());
        Check(deadDispatch.Outcome.Result == LegacyAutoTradeCloseResult.NotAlive && deadDispatch.Frames.Count == 0 &&
            dead.Book.Snapshot().Count == 1 && (await deadStore.ReadAllAsync()).Count == 1,
            "HP-zero Action closed or relayed an AFK shop instead of preserving it.");
    }

    private static async Task RunActionCloseRecovery()
    {
        var fixture = Create();
        await using var store = await HeldStore.Create(fixture);
        store.ResultOverride = LegacyAutoTradeCloseStoreResult.Ambiguous;
        store.ReadOverride = LegacyAutoTradeCloseReadState.Unknown;
        var codec = LegacyFrameCodec.CreateDefault();
        var pending = await LegacyAutoTradeCloseCoordinator.CloseForActionAndCaptureAsync(
            7, fixture.Sessions, fixture.World, fixture.Book, store, codec);
        Check(pending.Outcome.Result == LegacyAutoTradeCloseResult.PendingRecovery && pending.Frames.Count == 0 &&
            fixture.Book.Snapshot().Count == 2 && fixture.World.IsLiveTradeActive(7) && (await store.ReadAllAsync()).Count == 2,
            "Ambiguous Action close exposed success or discarded the recoverable pair.");

        var pendingRequest = store.LastRequest;
        Check(pendingRequest is not null, "Action close did not preserve its pending persistence request.");
        Check(await store.File.RemoveExpectedAsync(pendingRequest!) == LegacyAutoTradeCloseStoreResult.Removed,
            "Cannot resolve the Action close persistence fixture.");
        store.ReadOverride = null;
        var recovered = await LegacyAutoTradeCloseCoordinator.CloseForActionAndCaptureAsync(
            7, fixture.Sessions, fixture.World, fixture.Book, store, codec);
        Check(recovered.Outcome.Result == LegacyAutoTradeCloseResult.Closed && recovered.Outcome.ActionTriggered &&
            recovered.Outcome.ClosedListings.Count == 2 && recovered.Frames.Count == 4 && store.DeleteCalls == 1 &&
            fixture.Book.Snapshot().Count == 0 && !fixture.World.IsLiveTradeActive(7) && (await store.ReadAllAsync()).Count == 0,
            "Action recovery repeated persistence or failed to finish the original pair.");
        Check(recovered.Frames.All(plan => codec.Decode(plan.Frame).Header.ClientTick == 0 && codec.Decode(plan.Frame).Header.KeywordIndex == 0) &&
            !recovered.Frames.Any(plan => codec.Decode(plan.Frame).Header.Type == 0x0366),
            "Recovered Action close lost its zero-header/no-PK relay semantics.");
    }

    public static void CloseAtomicAndRetry() => RunAtomicAndRetry().GetAwaiter().GetResult();
    public static void DeadClosePreservesEverything() => RunDead().GetAwaiter().GetResult();
    public static void TinyInvalidPositionCleanupClosesDeadAuthor() => RunTinyInvalidPositionCleanup().GetAwaiter().GetResult();
    public static void CloseWaitsDurabilityAndHpAcceptance() => RunHeld().GetAwaiter().GetResult();
    public static void CloseFailuresPreservePair() => RunFailures().GetAwaiter().GetResult();
    public static void CloseCancellation() => RunCancellation().GetAwaiter().GetResult();
    public static void CloseAmbiguityAndRecovery() => RunAmbiguity().GetAwaiter().GetResult();
    public static void RecoveryAfterDisconnectDoesNotTouchNewSession() => RunDisconnect().GetAwaiter().GetResult();
    public static void PairChangesWhileWaiting() => RunPairChanged().GetAwaiter().GetResult();
    public static void ReservationTokensAndNoShop() => RunTokens().GetAwaiter().GetResult();
    public static void PendingBuyerCannotPurchaseOtherShop() => RunPendingBuyer().GetAwaiter().GetResult();
    public static void PendingOwnerWithoutListingCannotReopen() => RunOwnerWithoutListing().GetAwaiter().GetResult();
    public static void DuplicateOwnersRejectBeforeReservation() => RunDuplicateOwners().GetAwaiter().GetResult();
    public static void MixedDurableRecoveryRemainsBlocked() => RunMixedRecovery().GetAwaiter().GetResult();

    private static async Task RunMixedRecovery()
    {
        var fixture = Create(); await using var store = await HeldStore.Create(fixture);
        store.ResultOverride = LegacyAutoTradeCloseStoreResult.Ambiguous; store.ReadOverride = LegacyAutoTradeCloseReadState.Unknown;
        await Close(fixture, store);
        LegacyAutoTradePendingClose pending;
        await using (var lease = await fixture.Book.AcquireAsync([7, 12]))
        {
            Check(fixture.Book.TryGetPendingClose(lease, 7, out var captured) && captured is not null, "Missing mixed recovery context."); pending = captured!;
        }
        // Simulate an external inconsistent writer, not a partial delete by the port.
        await store.File.RemoveAsync("OWNER7", 0); store.ReadOverride = null;
        var mixed = await LegacyAutoTradeCloseRecovery.RecoverAsync(12, fixture.Sessions, fixture.World, fixture.Book, store);
        Check(mixed.Result == LegacyAutoTradeCloseResult.PendingRecovery && mixed.Trade is null &&
            fixture.Book.IsOwnerPending(new("OWNER7", 0)) && fixture.Book.IsOwnerPending(new("OWNER12", 0)) &&
            fixture.Book.Snapshot().Count == 2 && fixture.World.IsLiveTradeActive(7) && (await store.ReadAllAsync()).Count == 1,
            "Mixed durable state was blindly restored or declared successful.");
        await store.File.RemoveExpectedAsync(pending.Request);
        var resolved = await Close(fixture, store); // retry must use recovery, not issue a second delete
        Check(resolved.Result == LegacyAutoTradeCloseResult.Closed && resolved.ClosedListings.Count == 2 && store.DeleteCalls == 1 &&
            fixture.Book.Snapshot().Count == 0 && !fixture.World.IsLiveTradeActive(7) && (await store.ReadAllAsync()).Count == 0,
            "Absent readback did not finalize retry without duplicate persistence.");
        fixture.CheckMobs();
    }

    private static async Task RunDuplicateOwners()
    {
        var fixture = Create(duplicateOwner: true);
        var result = await Close(fixture, null);
        Check(result.Result == LegacyAutoTradeCloseResult.Conflict && result.Trade is null && fixture.Book.Snapshot().Count == 2 &&
            fixture.World.TryCloseTrade(7, out _) == LegacyTradeCloseResult.Accepted, "Duplicate owners leaked a reservation or closed shops.");
        fixture.CheckMobs();
    }

    private static async Task RunOwnerWithoutListing()
    {
        var fixture = Create();
        await using (var lease = await fixture.Book.AcquireAsync([7]))
        {
            Check(fixture.Book.TryGet(lease, 7, out var own) && own is not null && fixture.Book.TryStop(lease, own), "Cannot remove caller's shop fixture.");
        }
        await using var store = await HeldStore.Create(fixture);
        store.ResultOverride = LegacyAutoTradeCloseStoreResult.Ambiguous; store.ReadOverride = LegacyAutoTradeCloseReadState.Unknown;
        var result = await Close(fixture, store);
        Check(result.Result == LegacyAutoTradeCloseResult.PendingRecovery && fixture.Book.IsOwnerPending(new("OWNER7", 0)),
            "Pending pair did not block the owner without a listing.");
        await using var extra = await fixture.Book.AcquireAsync([13]);
        var other = fixture.Book.Snapshot().Single().Snapshot with { ConnectionId = 13 };
        Check(!fixture.Book.TryRestore(extra, 13, new("OWNER7", 0), other, out _), "Unlisted owner bypassed pending close using a new id.");
        fixture.CheckMobs();
    }

    private static async Task RunPendingBuyer()
    {
        var fixture = Create(); await using var store = await HeldStore.Create(fixture);
        store.ResultOverride = LegacyAutoTradeCloseStoreResult.Ambiguous; store.ReadOverride = LegacyAutoTradeCloseReadState.Unknown;
        await Close(fixture, store);
        var purchase = AutoTradeCloseFixtures.CreatePurchase().Request;
        var listing = purchase.Context.Listing with { ConnectionId = 13 };
        LegacyAutoTradeListingHandle other;
        await using (var lease = await fixture.Book.AcquireAsync([13]))
        {
            Check(fixture.Book.TryRestore(lease, 13, new("OTHER", 0), listing, out var created) && created is not null, "Cannot add independent shop.");
            other = created!;
        }
        Check(fixture.World.EnterNpc(new byte[LegacyAccountSnapshot.CharacterStride], 2000, 2001,
            new byte[LegacyAccountSnapshot.AffectStride], requestedConnectionId: 13, autoTradeSnapshot: listing,
            autoTradeOwnerAccount: "OTHER", autoTradeCharacterSlot: 0) == 13, "Cannot add independent shop NPC.");
        var request = purchase with
        {
            BuyerAccountName = "OWNER7", ExpectedBuyerMob = fixture.First.ToArray(), ExpectedPersistedBuyerMob = fixture.First.ToArray(),
            SellerAccountName = "OTHER", SellerCharacterSlot = 0, ExpectedGeneration = other.Generation,
            Context = purchase.Context with { TargetId = 13, Listing = listing },
            ExpectedListing = purchase.ExpectedListing with { AccountName = "OTHER", CharacterSlot = 0 },
        };
        var result = await LegacyAutoTradePurchaseExecutor.ExecuteAsync(fixture.World, fixture.Book, new ImmediatePurchaseStore(), request);
        Check(result.Result == LegacyAutoTradePurchaseExecutionResult.PendingRecovery && result.Outcome is null &&
            fixture.Book.Snapshot().Single(handle => handle.Snapshot.ConnectionId == 13).Snapshot.Items[0].Index == 900,
            "Pending buyer purchased from an unrelated shop.");
        fixture.CheckMobs();
    }

    private sealed class ImmediatePurchaseStore : ILegacyAutoTradePurchaseCommitStore
    {
        public ValueTask<LegacyAutoTradePurchaseCommitResult> TryCommitAsync(LegacyAutoTradePurchaseCommitRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(LegacyAutoTradePurchaseCommitResult.Committed);
    }

    private static async Task RunDead()
    {
        foreach (var hp in new[] { 0, -1 })
        {
            var fixture = SetHp(Create(), hp);
            await using var store = await HeldStore.Create(fixture);
            var result = await Close(fixture, store);
            Check(result.Result == LegacyAutoTradeCloseResult.NotAlive && result.Trade is null && result.ClosedListings.Count == 0,
                "Dead close returned a success/PK/visual plan.");
            Check(store.DeleteCalls == 0 && fixture.Book.Snapshot().Count == 2 && fixture.World.IsLiveTradeActive(7) &&
                fixture.World.IsLiveTradeActive(12) && (await store.ReadAllAsync()).Count == 2, "Dead close changed pair or durable shops.");
            fixture.CheckMobs();
        }
    }

    private static async Task RunTinyInvalidPositionCleanup()
    {
        foreach (var hp in new[] { 0, -1 })
        {
            var fixture = SetHp(Create(), hp);
            await using var store = await HeldStore.Create(fixture);
            var codec = LegacyFrameCodec.CreateDefault();
            var dispatch = await LegacyAutoTradeCloseCoordinator.CloseForTinyCombineInvalidPositionAndCaptureAsync(
                7, fixture.Sessions, fixture.World, fixture.Book, store, codec, 19, 3);

            Check(dispatch.Outcome.Result == LegacyAutoTradeCloseResult.Closed && dispatch.Outcome.Trade?.OpponentId == 12 &&
                dispatch.Outcome.ClosedListings.Count == 2 && dispatch.Frames.Count == 5,
                $"Tiny invalid-position cleanup did not close the dead author and bilateral listings at HP {hp}.");
            Check(dispatch.Frames.Count(plan => codec.Decode(plan.Frame).Header.Type == 0x0384) == 2 &&
                store.DeleteCalls == 1 && fixture.Book.Snapshot().Count == 0 &&
                !fixture.World.IsLiveTradeActive(7) && !fixture.World.IsLiveTradeActive(12) &&
                (await store.ReadAllAsync()).Count == 0,
                $"Tiny invalid-position cleanup did not clear runtime and durable trade state at HP {hp}.");
            fixture.CheckMobs();
        }
    }

    private static async Task RunHeld()
    {
        var fixture = Create(); await using var store = await HeldStore.Create(fixture); store.Hold = true;
        var close = Close(fixture, store).AsTask();
        await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Check(!close.IsCompleted && fixture.Book.Snapshot().Count == 2 && fixture.World.IsLiveTradeActive(7) &&
            (await store.ReadAllAsync()).Count == 2, "Close mutated before durable removal.");
        Check(fixture.World.TryStageTradeOffer(12, Offer(7), out _) == LegacyTradeOfferResult.TradeClosePending &&
            fixture.World.TryCompleteTrade(7, out _) == LegacyTradeCompletionResult.TradeClosePending &&
            fixture.World.TryCloseTrade(12, out _) == LegacyTradeCloseResult.TradeClosePending &&
            fixture.World.TryDeleteCarryItem(7, 0, 900, out var cancelledOpponent) == LegacyDeleteItemResult.TradeClosePending && cancelledOpponent == 0,
            "Reserved pair accepted an offer/check/cancel/delete cancellation.");
        fixture.CheckMobs();
        fixture = SetHp(fixture, -1);
        store.Release.SetResult();
        var result = await close.WaitAsync(TimeSpan.FromSeconds(5));
        Check(result.Result == LegacyAutoTradeCloseResult.Closed && result.Trade?.OpponentId == 12 && result.ClosedListings.Count == 2 &&
            (await store.ReadAllAsync()).Count == 0 && fixture.Book.Snapshot().Count == 0 && !fixture.World.IsLiveTradeActive(7),
            "HP drop after acceptance reclassified a committed delete.");
        Check(store.LastRequest?.ExpectedListings.Count == 2, "Close did not send one batch for both shops.");
        fixture.CheckMobs();
    }

    private static async Task RunFailures()
    {
        foreach (var rejected in new[] { LegacyAutoTradeCloseStoreResult.Conflict, LegacyAutoTradeCloseStoreResult.Unavailable })
        {
            var fixture = Create(); await using var store = await HeldStore.Create(fixture); store.ResultOverride = rejected;
            var result = await Close(fixture, store);
            Check(result.Result == (rejected == LegacyAutoTradeCloseStoreResult.Conflict ? LegacyAutoTradeCloseResult.Conflict : LegacyAutoTradeCloseResult.PersistenceUnavailable) &&
                result.Trade is null && result.ClosedListings.Count == 0 && fixture.World.IsLiveTradeActive(7) &&
                fixture.World.IsLiveTradeActive(12) && fixture.Book.Snapshot().Count == 2 && (await store.ReadAllAsync()).Count == 2,
                "Known failure closed half a pair or exposed success frames.");
            await using var lease = await fixture.Book.AcquireAsync([7, 12]);
            Check(fixture.Book.TryGet(lease, 7, out _) && fixture.Book.TryGet(lease, 12, out _), "Known failure left shops blocked.");
            fixture.CheckMobs();
        }
        var unsupported = Create();
        var unavailable = await Close(unsupported, new UnsupportedStore());
        Check(unavailable.Result == LegacyAutoTradeCloseResult.PersistenceUnavailable && unsupported.Book.Snapshot().Count == 2 &&
            unsupported.World.IsLiveTradeActive(7), "Unsupported configured backend allowed runtime-only close.");
        unsupported.CheckMobs();
    }

    private static async Task RunCancellation()
    {
        var before = Create(); await using var beforeStore = await HeldStore.Create(before); beforeStore.Hold = true;
        using var cancel = new CancellationTokenSource();
        var waiting = Close(before, beforeStore, cancel.Token).AsTask();
        await beforeStore.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); cancel.Cancel();
        var rejected = await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        Check(rejected.Result == LegacyAutoTradeCloseResult.PersistenceUnavailable && before.World.IsLiveTradeActive(7) &&
            before.Book.Snapshot().Count == 2 && (await beforeStore.ReadAllAsync()).Count == 2,
            "Cancellation before commit left partial close.");
        before.CheckMobs();
        var after = Create(); await using var afterStore = await HeldStore.Create(after);
        using var afterCancel = new CancellationTokenSource(); afterStore.AfterCommit = afterCancel.Cancel;
        var accepted = await Close(after, afterStore, afterCancel.Token);
        Check(accepted.Result == LegacyAutoTradeCloseResult.Closed && after.Book.Snapshot().Count == 0 && !after.World.IsLiveTradeActive(7) &&
            (await afterStore.ReadAllAsync()).Count == 0, "Socket cancellation after commit interrupted runtime completion.");
        after.CheckMobs();
    }

    private static async Task RunAmbiguity()
    {
        foreach (var readback in new[] { LegacyAutoTradeCloseReadState.AllAbsent, LegacyAutoTradeCloseReadState.AllUnchanged,
            LegacyAutoTradeCloseReadState.Conflict, LegacyAutoTradeCloseReadState.Unknown })
        {
            var fixture = Create(); await using var store = await HeldStore.Create(fixture);
            store.ResultOverride = LegacyAutoTradeCloseStoreResult.Ambiguous;
            store.CommitBeforeOverride = readback == LegacyAutoTradeCloseReadState.AllAbsent;
            if (readback is LegacyAutoTradeCloseReadState.Conflict or LegacyAutoTradeCloseReadState.Unknown) store.ReadOverride = readback;
            var result = await Close(fixture, store);
            if (readback == LegacyAutoTradeCloseReadState.AllAbsent)
                Check(result.Result == LegacyAutoTradeCloseResult.Closed && fixture.Book.Snapshot().Count == 0 && !fixture.World.IsLiveTradeActive(7),
                    "Lost commit response was not reconciled from absent listings.");
            else if (readback == LegacyAutoTradeCloseReadState.AllUnchanged)
                Check(result.Result == LegacyAutoTradeCloseResult.PersistenceUnavailable && fixture.Book.Snapshot().Count == 2 &&
                    fixture.World.IsLiveTradeActive(7), "Unchanged readback did not abort reservation.");
            else
            {
                Check(result.Result == LegacyAutoTradeCloseResult.PendingRecovery && result.Trade is null && result.ClosedListings.Count == 0 &&
                    fixture.Book.Snapshot().Count == 2 && fixture.World.IsLiveTradeActive(7), "Uncertain close did not preserve a blocked pending operation.");
                await using (var lease = await fixture.Book.AcquireAsync([7, 12, 13]))
                {
                    LegacyAutoTradePendingClose? pending = null;
                    Check(!fixture.Book.TryGet(lease, 7, out _) && !fixture.Book.TryGet(lease, 12, out _) &&
                        fixture.Book.TryGetPendingClose(lease, 7, out pending) && pending is not null &&
                        !fixture.Book.TryStop(lease, fixture.Book.Snapshot()[0]) &&
                        !fixture.Book.TryRestore(lease, 13, new("owner7", 0), pending!.Listings[0].Snapshot with { ConnectionId = 13 }, out _),
                        "Pending operation permitted sales/stop/reopening on a new runtime id.");
                    Check(!fixture.Book.TryClearPendingClose(lease, pending! with { Reservation = pending!.Reservation with { Token = Guid.NewGuid() } }) &&
                        !fixture.Book.TryClearPendingClose(lease, pending! with { Listings = pending!.Listings.Select(handle => handle with { Generation = handle.Generation + 1 }).ToArray() }),
                        "Old token/generation cleared pending operation.");
                    ((LegacyItem[])pending!.Listings[0].Snapshot.Items)[0] = default;
                    Check(fixture.Book.Snapshot().All(handle => handle.Snapshot.Items[0].Index == 900), "Pending snapshot exposed mutable book arrays.");
                }
                var reconnect = await LegacyAutoTradeReconnectCoordinator.CloseAsync(fixture.World, fixture.Book, store, "OWNER7", 0);
                Check(reconnect.Result == LegacyAutoTradeReconnectResult.PersistenceUnavailable && (await store.ReadAllAsync()).Count == 2,
                    "Reconnect bypassed pending durable reconciliation.");
                store.ReadOverride = null;
                var recovered = await LegacyAutoTradeCloseRecovery.RecoverAsync(12, fixture.Sessions, fixture.World, fixture.Book, store);
                Check(recovered.Result == LegacyAutoTradeCloseResult.PersistenceUnavailable && !fixture.Book.IsOwnerPending(new("OWNER7", 0)) &&
                    fixture.Book.Snapshot().Count == 2 && fixture.World.IsLiveTradeActive(7), "Unchanged recovery lost shops or left owner blocked.");
            }
            fixture.CheckMobs();
        }
    }

    private static async Task RunDisconnect()
    {
        var fixture = Create(); await using var store = await HeldStore.Create(fixture);
        store.ResultOverride = LegacyAutoTradeCloseStoreResult.Ambiguous; store.ReadOverride = LegacyAutoTradeCloseReadState.Unknown;
        Check((await Close(fixture, store)).Result == LegacyAutoTradeCloseResult.PendingRecovery, "Disconnect fixture did not become pending.");
        LegacyAutoTradePendingClose pending;
        await using (var lease = await fixture.Book.AcquireAsync([7, 12]))
        {
            Check(fixture.Book.TryGetPendingClose(lease, 7, out var captured) && captured is not null, "Missing pending context."); pending = captured!;
        }
        fixture.World.Leave(7); fixture.Sessions.Close(7);
        Check(fixture.World.Enter(7, "REPLACEMENT", (_, _) => ValueTask.CompletedTask) &&
            fixture.World.SetCharacterState(7, 0, 0, 0, 0, 150, 2112, 2042, fixture.First), "Cannot reuse disconnected runtime id.");
        await store.File.RemoveExpectedAsync(pending.Request); store.ReadOverride = null;
        var recovered = await LegacyAutoTradeCloseRecovery.RecoverAsync(7, fixture.Sessions, fixture.World, fixture.Book, store);
        Check(recovered.Result == LegacyAutoTradeCloseResult.Closed && recovered.Trade is null && recovered.ClosedListings.Count == 0 &&
            fixture.Book.Snapshot().Count == 0 && !fixture.World.TryFinishTradeClose(pending.Reservation, out _) &&
            fixture.World.TryStageTradeOffer(7, Offer(12), out _) == LegacyTradeOfferResult.Accepted && fixture.World.IsLiveTradeActive(7),
            "Old recovery affected the replacement session or emitted an old visual/PK plan.");
        fixture.CheckMobs();
    }

    private static async Task RunPairChanged()
    {
        var fixture = Create(); var lease = await fixture.Book.AcquireAsync([7]);
        var close = Close(fixture, null).AsTask();
        Check(!close.IsCompleted && fixture.World.TryCloseTrade(7, out _) == LegacyTradeCloseResult.Accepted, "Cannot change pair during lease wait.");
        await lease.DisposeAsync(); var result = await close.WaitAsync(TimeSpan.FromSeconds(5));
        Check(result.Result == LegacyAutoTradeCloseResult.Conflict && result.Trade is null && fixture.Book.Snapshot().Count == 2,
            "Close reused a pair captured before waiting.");
        fixture.CheckMobs();
    }

    private static async Task RunTokens()
    {
        var fixture = Create();
        Check(fixture.World.TryReserveTradeClose(7, 0, out _) == LegacyTradeClosePrepareResult.PairChanged, "Reservation accepted stale pair.");
        Check(fixture.World.TryReserveTradeClose(7, 12, out var token) == LegacyTradeClosePrepareResult.Prepared && token is not null &&
            fixture.World.TryReserveTradeClose(12, 7, out _) == LegacyTradeClosePrepareResult.Busy, "Reservation did not enforce expected pair/exclusivity.");
        Check(!fixture.World.TryAbortTradeClose(token! with { Token = Guid.NewGuid() }) &&
            fixture.World.TryAbortTradeClose(token!) && !fixture.World.TryFinishTradeClose(token!, out _), "Stale token released or completed another reservation.");
        await Close(fixture, null);
        var noShop = await Close(fixture, new UnsupportedStore());
        Check(noShop.Result == LegacyAutoTradeCloseResult.NoShop && noShop.Trade?.OpponentId == 0 && noShop.ClosedListings.Count == 0,
            "NoShop omitted signals/PK plan or performed autotrade I/O.");
        fixture.CheckMobs();
    }

    private static ValueTask<LegacyAutoTradeCloseOutcome> Close(Fixture fixture, ILegacyAutoTradeStateStore? store, CancellationToken token = default) =>
        LegacyAutoTradeCloseCoordinator.CloseAsync(7, fixture.Sessions, fixture.World, fixture.Book, store, token);

    private static Fixture SetHp(Fixture fixture, int hp)
    {
        fixture.World.TryGetCharacterSnapshot(7, out var current, out _, out _);
        BinaryPrimitives.WriteInt32LittleEndian(current!.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), hp);
        Check(fixture.World.SetCharacterState(7, 0, 0, 0, 0, 150, 2112, 2042, current), "Cannot change HP.");
        fixture.World.TryGetCharacterSnapshot(7, out var loaded, out _, out _);
        return fixture with { First = loaded! };
    }

    private sealed class UnsupportedStore : ILegacyAutoTradeStateStore
    {
        public ValueTask<LegacyAutoTradeStateResult> SaveAsync(string account, int slot, LegacyAutoTradeSnapshot snapshot, CancellationToken token = default) => throw new InvalidOperationException("Unexpected state I/O.");
        public ValueTask<LegacyAutoTradePersistedListing?> ReadAsync(string account, int slot, CancellationToken token = default) => throw new InvalidOperationException("Unexpected state I/O.");
        public ValueTask<IReadOnlyList<LegacyAutoTradePersistedListing>> ReadAllAsync(CancellationToken token = default) => throw new InvalidOperationException("Unexpected state I/O.");
        public ValueTask<LegacyAutoTradeStateResult> RemoveAsync(string account, int slot, CancellationToken token = default) => throw new InvalidOperationException("Unexpected state I/O.");
    }

    private sealed class HeldStore : ILegacyAutoTradeStateStore, ILegacyAutoTradeCloseStore, IAsyncDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "wyd-close-coordinator-" + Guid.NewGuid().ToString("N"));
        internal LegacyAutoTradeFileStore File { get; }
        internal bool Hold, CommitBeforeOverride;
        internal LegacyAutoTradeCloseStoreResult? ResultOverride;
        internal LegacyAutoTradeCloseReadState? ReadOverride;
        internal Action? AfterCommit;
        internal int DeleteCalls, BlindRemoveCalls;
        internal LegacyAutoTradeCloseRequest? LastRequest;
        internal TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private HeldStore() { Directory.CreateDirectory(root); File = new(Path.Combine(root, "UP.json"), "UP"); }
        internal static async Task<HeldStore> Create(Fixture fixture)
        {
            var store = new HeldStore();
            foreach (var handle in fixture.Book.Snapshot()) await store.SaveAsync(handle.Owner.AccountName, handle.Owner.CharacterSlot, handle.Snapshot);
            return store;
        }
        public async ValueTask<LegacyAutoTradeCloseStoreResult> RemoveExpectedAsync(LegacyAutoTradeCloseRequest request, CancellationToken token = default)
        {
            DeleteCalls++; LastRequest = request; Entered.TrySetResult();
            if (Hold) await Release.Task.WaitAsync(token);
            if (ResultOverride is not null && !CommitBeforeOverride) return ResultOverride.Value;
            var result = await File.RemoveExpectedAsync(request, token);
            AfterCommit?.Invoke();
            return ResultOverride ?? result;
        }
        public ValueTask<LegacyAutoTradeCloseReadState> ReadCloseStateAsync(LegacyAutoTradeCloseRequest request, CancellationToken token = default) =>
            ReadOverride is { } read ? ValueTask.FromResult(read) : File.ReadCloseStateAsync(request, token);
        public ValueTask<LegacyAutoTradeStateResult> SaveAsync(string account, int slot, LegacyAutoTradeSnapshot snapshot, CancellationToken token = default) => File.SaveAsync(account, slot, snapshot, token);
        public ValueTask<LegacyAutoTradePersistedListing?> ReadAsync(string account, int slot, CancellationToken token = default) => File.ReadAsync(account, slot, token);
        public ValueTask<IReadOnlyList<LegacyAutoTradePersistedListing>> ReadAllAsync(CancellationToken token = default) => File.ReadAllAsync(token);
        public ValueTask<LegacyAutoTradeStateResult> RemoveAsync(string account, int slot, CancellationToken token = default)
        {
            BlindRemoveCalls++;
            return File.RemoveAsync(account, slot, token);
        }
        public ValueTask DisposeAsync() { Directory.Delete(root, recursive: true); return ValueTask.CompletedTask; }
    }

    private static async Task RunAtomicAndRetry()
    {
        var fixture = Create();
        var outcome = await LegacyAutoTradeCloseCoordinator.CloseAsync(7, fixture.Sessions, fixture.World, fixture.Book, null);
        Check(outcome.Result == LegacyAutoTradeCloseResult.Closed && outcome.Trade?.OpponentId == 12 && outcome.ClosedListings.Count == 2,
            "Close did not finish both shops and the reserved pair.");
        Check(fixture.Book.Snapshot().Count == 0 && !fixture.World.IsLiveTradeActive(7) && !fixture.World.IsLiveTradeActive(12),
            "Close retained a shop or half the pair.");
        fixture.CheckMobs();
        var repeat = await LegacyAutoTradeCloseCoordinator.CloseAsync(7, fixture.Sessions, fixture.World, fixture.Book, null);
        Check(repeat.Result == LegacyAutoTradeCloseResult.NoShop && repeat.Trade?.OpponentId == 0 && repeat.ClosedListings.Count == 0,
            "Repeated close returned a former shop visual/opponent.");
        fixture.CheckMobs();
    }

    private sealed record Fixture(WorldHub World, LegacyAutoTradeBook Book, LoginSessionRegistry Sessions, byte[] First, byte[] Second)
    {
        internal void CheckMobs()
        {
            Check(World.TryGetCharacterSnapshot(7, out var first, out _, out _) && first is not null && first.SequenceEqual(First),
                "Close changed first MOB/inventory/Gold.");
            Check(World.TryGetCharacterSnapshot(12, out var second, out _, out _) && second is not null && second.SequenceEqual(Second),
                "Close changed second MOB/inventory/Gold.");
        }
    }

    private static Fixture Create(bool duplicateOwner = false, bool failSend = false, bool includeTrade = true,
        bool includeFirstShop = true, bool includeSecondShop = true)
    {
        var world = new WorldHub(); var book = new LegacyAutoTradeBook(); var sessions = new LoginSessionRegistry();
        var mobs = new List<byte[]>();
        foreach (var id in new[] { 7, 12 })
        {
            var account = duplicateOwner ? "SHARED" : "OWNER" + id;
            var mob = new byte[LegacyAccountSnapshot.CharacterStride];
            BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), 100);
            BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), 150);
            Check(world.Enter(id, account, (_, _) => failSend ? ValueTask.FromException(new IOException("Synthetic disconnected transport.")) : ValueTask.CompletedTask) &&
                world.SetCharacterState(id, 0, 0, 0, 0, 150, 2112, 2042, mob), "Close fixture cannot enter world.");
            Check(sessions.Open(id) && sessions.BeginAccountLogin(id, new AccountLoginRequest(account, "synthetic", new byte[52], 7670, 0, [0, 0, 0, 0])) == LoginTransitionResult.Accepted &&
                sessions.CompleteAccountLogin(id, account) == LoginTransitionResult.Accepted &&
                sessions.BeginCharacterWait(id) == LoginTransitionResult.Accepted &&
                sessions.CompleteCharacterLogin(id, 0, 2112, 2042) == LoginTransitionResult.Accepted, "Close fixture cannot enter USER_PLAY.");
            if (id == 7 ? includeFirstShop : includeSecondShop)
            {
                var items = new LegacyItem[AutoTradeListConfirmation.SlotCount]; items[0] = new(900, 1, 2, 3, 4, 5, 6);
                var positions = Enumerable.Repeat((sbyte)-1, items.Length).ToArray(); positions[0] = 4;
                var prices = new int[items.Length]; prices[0] = 100;
                var lease = book.AcquireAsync([id]).GetAwaiter().GetResult();
                try { Check(book.TryRestore(lease, id, new(account, 0), new(id, 2112, 2042, "TEST", items, positions, prices, 5), out _), "Close fixture cannot restore shop."); }
                finally { lease.DisposeAsync().GetAwaiter().GetResult(); }
            }
            world.TryGetCharacterSnapshot(id, out var snapshot, out _, out _); mobs.Add(snapshot!);
        }
        if (includeTrade)
            Check(world.TryStageTradeOffer(7, Offer(12), out _) == LegacyTradeOfferResult.Accepted, "Close fixture cannot pair participants.");
        return new(world, book, sessions, mobs[0], mobs[1]);
    }

    private static TradeOfferRequest Offer(ushort opponent) => new(new LegacyItem[TradeOfferRequest.ItemCount],
        Enumerable.Repeat((sbyte)-1, TradeOfferRequest.ItemCount).ToArray(), 0, 0, opponent);
}

using WydCdk.Protocol;
using WydCdk.World;

namespace WydCdk.Server;

internal enum TradeListRequestDisposition
{
    RejectedNotPlaying,
    DeadBeforeLease,
    DeadAfterLease,
    InvalidTarget,
    TargetNotInAutoTrade,
    RejectedByRules,
    Listed,
}

/// <summary>Handles the host-side USER_PLAY, HP, lease, range, and response flow for MSG_ReqTradeList.</summary>
internal static class TradeListRequestHandler
{
    internal static async Task<TradeListRequestDisposition> HandleAsync(
        int connectionId,
        TradeListRequest request,
        uint clientTick,
        byte keywordIndex,
        WorldHub world,
        LoginSessionRegistry sessions,
        LegacyAutoTradeBook autoTradeBook,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> sendToRequesterAsync,
        Func<int, int, Task> recordCrackErrorAsync,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(autoTradeBook);
        ArgumentNullException.ThrowIfNull(sendToRequesterAsync);
        ArgumentNullException.ThrowIfNull(recordCrackErrorAsync);
        ArgumentNullException.ThrowIfNull(log);

        if (!sessions.TryGet(connectionId, out var tradeListSession) || tradeListSession!.State != LoginSessionState.Playing)
        {
            log("Rejected autotrade-list frame: session is not in USER_PLAY.");
            await recordCrackErrorAsync(LegacyCrackErrorRules.TradeListPoints, LegacyCrackErrorRules.TradeListType);
            return TradeListRequestDisposition.RejectedNotPlaying;
        }

        // ReqTradeList checks the requester before the target, exactly HP==0.
        var requesterStateAvailable = world.TryGetCombatState(connectionId, out var requesterState) && requesterState is not null;
        if (requesterStateAvailable && requesterState!.CurrentScore.Hp == 0)
        {
            await SendDeadHpModeAsync();
            await recordCrackErrorAsync(LegacyCrackErrorRules.TradeListPoints, LegacyCrackErrorRules.TradeListType);
            return TradeListRequestDisposition.DeadBeforeLease;
        }

        if (request.TargetId is <= 0 or > ushort.MaxValue)
            return TradeListRequestDisposition.InvalidTarget;

        await using var listLease = await autoTradeBook.AcquireAsync(
            [connectionId, request.TargetId], cancellationToken);

        // Re-read after waiting for leases: HP may have changed during the await.
        requesterStateAvailable = world.TryGetCombatState(connectionId, out requesterState) && requesterState is not null;
        if (requesterStateAvailable && requesterState!.CurrentScore.Hp == 0)
        {
            await listLease.DisposeAsync();
            await SendDeadHpModeAsync();
            await recordCrackErrorAsync(LegacyCrackErrorRules.TradeListPoints, LegacyCrackErrorRules.TradeListType);
            return TradeListRequestDisposition.DeadAfterLease;
        }

        if (!autoTradeBook.TryGet(listLease, request.TargetId, out var shopHandle) || shopHandle is null)
        {
            log($"Autotrade list rejected: requester={tradeListSession.AccountName}, target={request.TargetId}, reason=TargetNotInAutoTrade.");
            return TradeListRequestDisposition.TargetNotInAutoTrade;
        }

        var shopSnapshot = shopHandle.Snapshot;
        var requesterPositionAvailable = world.TryGetCharacterSnapshot(connectionId, out _, out var requesterX, out var requesterY);
        var targetIsOnline = world.TryGetCharacterSnapshot(request.TargetId, out _, out var targetX, out var targetY);
        var targetIsOfflineAutoTrade = world.TryGetNpcSnapshot(request.TargetId, out var targetNpc) &&
            targetNpc is not null && targetNpc.AutoTradeSnapshot is not null;
        if (!targetIsOnline && targetIsOfflineAutoTrade)
        {
            targetX = targetNpc!.PositionX;
            targetY = targetNpc.PositionY;
        }

        var targetPositionAvailable = targetIsOnline || targetIsOfflineAutoTrade;
        var listContext = new LegacyAutoTradeListContext(
            requesterStateAvailable ? requesterState!.CurrentScore.Hp : 0,
            tradeListSession.State == LoginSessionState.Playing && requesterPositionAvailable,
            targetPositionAvailable,
            targetIsOnline,
            TargetAutoTradeActive: true,
            requesterX,
            requesterY,
            targetX,
            targetY,
            TargetOfflineAutoTrade: targetIsOfflineAutoTrade);
        var listResult = LegacyAutoTradeListRules.Validate(request.TargetId, listContext);
        if (listResult == LegacyAutoTradeListResult.Accepted &&
            LegacyAutoTradeListingRelay.TryCapture(world, autoTradeBook, listLease, connectionId, request.TargetId,
                false, LegacyFrameCodec.CreateDefault(), clientTick, keywordIndex, out var listFrames))
        {
            await listLease.DisposeAsync();
            foreach (var plan in listFrames)
                await sendToRequesterAsync(plan.Frame, cancellationToken);
            log($"Autotrade list sent: requester={tradeListSession.AccountName}, target={request.TargetId}, title={shopSnapshot.Title}.");
            return TradeListRequestDisposition.Listed;
        }

        log($"Autotrade list rejected: requester={tradeListSession.AccountName}, target={request.TargetId}, reason={listResult}.");
        return TradeListRequestDisposition.RejectedByRules;

        ValueTask SendDeadHpModeAsync()
        {
            var frame = new SetHpModeConfirmation(0, (short)tradeListSession.State)
                .ToFrame(LegacyFrameCodec.CreateDefault(), clientTick, keywordIndex, (ushort)connectionId);
            return sendToRequesterAsync(frame, cancellationToken);
        }
    }
}

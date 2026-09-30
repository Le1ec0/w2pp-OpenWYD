using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.Server;
using WydCdk.World;

internal static class TradeListLeaseRaceTests
{
    private const int RequesterId = 7;
    private const int ShopId = 12;

    public static async Task RunAsync()
    {
        var (world, book, _) = AutoTradeCloseFixtures.CreatePurchase();
        var sessions = CreatePlayingSession(RequesterId);
        Check(SetRequesterHp(world, 100), "Cannot seed a living ReqTradeList requester.");

        var sentFrames = new List<byte[]>();
        var crackCalls = 0;
        async ValueTask SendAndProveLeaseWasReleased(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
        {
            await using var probe = await book.AcquireAsync([RequesterId, ShopId], cancellationToken)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            sentFrames.Add(frame.ToArray());
        }

        Task RecordCrackError(int points, int type)
        {
            crackCalls++;
            Check(points == 10 && type == 87, "Dead ReqTradeList used the wrong crack-error tuple.");
            Check(sessions.TryAddCrackError(RequesterId, points, type, out var outcome) &&
                outcome == new LegacyCrackErrorOutcome(10, 10, 87, true, false),
                "Dead ReqTradeList did not add exactly one 10/type87 strike.");
            return Task.CompletedTask;
        }

        await using (var blocker = await book.AcquireAsync([RequesterId, ShopId]))
        {
            var waiting = TradeListRequestHandler.HandleAsync(
                RequesterId,
                new TradeListRequest(ShopId),
                clientTick: 77,
                keywordIndex: 6,
                world,
                sessions,
                book,
                SendAndProveLeaseWasReleased,
                RecordCrackError,
                _ => { },
                CancellationToken.None);
            Check(!waiting.IsCompleted, "ReqTradeList did not wait behind the held owner/requester lease.");
            Check(SetRequesterHp(world, 0), "Cannot kill the requester while ReqTradeList is waiting.");
            await blocker.DisposeAsync();

            var outcome = await waiting.WaitAsync(TimeSpan.FromSeconds(2));
            Check(outcome == TradeListRequestDisposition.DeadAfterLease,
                "ReqTradeList did not reject the requester after the lease wait.");
        }

        Check(crackCalls == 1 && sessions.TryGet(RequesterId, out var deadSession) && deadSession!.CrackErrorPoints == 10,
            "Dead ReqTradeList did not record exactly one shared-counter increment.");
        Check(sentFrames.Count == 1, "Dead ReqTradeList emitted extra frames or leaked the shop listing.");
        var codec = LegacyFrameCodec.CreateDefault();
        var hpMode = codec.Decode(sentFrames[0]);
        Check(hpMode.IsChecksumValid && hpMode.Header.Type == SetHpModeConfirmation.MessageType &&
            hpMode.Header.Size == SetHpModeConfirmation.PacketSize && hpMode.Header.Id == RequesterId &&
            hpMode.Header.ClientTick == 77 && hpMode.Payload.Length == 8 &&
            BinaryPrimitives.ReadInt32LittleEndian(hpMode.Payload.Span) == 0 &&
            BinaryPrimitives.ReadInt16LittleEndian(hpMode.Payload.Span[4..]) == (short)LoginSessionState.Playing,
            "Dead ReqTradeList did not send exactly one private, valid HpMode response.");
        Check(!sentFrames.Any(frame =>
        {
            var decoded = codec.Decode(frame);
            return decoded.Header.Type is AutoTradeListConfirmation.MessageType or 0x0363;
        }), "Dead ReqTradeList leaked list196 or shop visual260.");

        Check(SetRequesterHp(world, 100), "Cannot restore requester HP for the living control.");
        sentFrames.Clear();
        var livingOutcome = await TradeListRequestHandler.HandleAsync(
            RequesterId,
            new TradeListRequest(ShopId),
            clientTick: 78,
            keywordIndex: 7,
            world,
            sessions,
            book,
            SendAndProveLeaseWasReleased,
            RecordCrackError,
            _ => { },
            CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
        Check(livingOutcome == TradeListRequestDisposition.Listed && crackCalls == 1 &&
            sessions.TryGet(RequesterId, out var livingSession) && livingSession!.CrackErrorPoints == 10,
            "Living ReqTradeList was rejected or incorrectly incremented the shared counter.");
        var livingFrames = sentFrames.Select(frame => codec.Decode(frame)).ToArray();
        Check(livingFrames.Length == 2 && livingFrames[0].Header.Type == AutoTradeListConfirmation.MessageType &&
            livingFrames[0].Header.Size == 196 && livingFrames[1].Header.Type == 0x0363 &&
            livingFrames[1].Header.Size == 260 && livingFrames.All(frame => frame.IsChecksumValid),
            "Living control did not receive the list196 and shop visual260.");
    }

    private static LoginSessionRegistry CreatePlayingSession(int connectionId)
    {
        var sessions = new LoginSessionRegistry();
        Check(sessions.Open(connectionId), "Cannot open requester session.");
        var login = new AccountLoginRequest("BUYER", "fixture", new byte[52], 7670, 0, new uint[4]);
        Check(sessions.BeginAccountLogin(connectionId, login) == LoginTransitionResult.Accepted &&
            sessions.CompleteAccountLogin(connectionId, "BUYER") == LoginTransitionResult.Accepted &&
            sessions.BeginCharacterWait(connectionId) == LoginTransitionResult.Accepted &&
            sessions.CompleteCharacterLogin(connectionId, 0, 2010, 2011) == LoginTransitionResult.Accepted,
            "Cannot move requester into USER_PLAY.");
        return sessions;
    }

    private static bool SetRequesterHp(WorldHub world, int hp)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        mob[LegacyAccountSnapshot.MobNameOffset] = (byte)'B';
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 16), 100);
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), hp);
        return world.SetCharacterState(RequesterId, 0, 0, 0, 0, 150, 2010, 2011, mob);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

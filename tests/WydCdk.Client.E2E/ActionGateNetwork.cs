using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;

internal static class ActionGateNetwork
{
    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AutoTradeClosePeerAccountFile!;
        var actorBefore = await File.ReadAllBytesAsync(options.AccountFile);
        var peerBefore = await File.ReadAllBytesAsync(peerFile);
        var peerHeader = new AccountHeader(CString(peerBefore.AsSpan(0, 16)), CString(peerBefore.AsSpan(16, 12)), peerBefore.AsSpan(202, 6).ToArray());
        var hp = options.ActionNegativeHp ? -1 : 0;
        var actorPosition = ((short)2100, (short)2100);
        var peerPosition = ((short)2101, (short)2100);
        var types = new[] { ActionRequest.MessageType, ActionRequest.StopMessageType, ActionRequest.IllusionMessageType };
        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "ACTION_ACTOR_", verifyActionModeGate: true))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "ACTION_PEER_"))
        {
            var actorId = Id(actor.CharacterLoginFrame); var peerId = Id(peer.CharacterLoginFrame);
            Check(actorId > 0 && peerId > 0 && actorId != peerId && Hp(actor.CharacterLoginFrame) == hp && Hp(peer.CharacterLoginFrame) > 0, "Wrong Action fixture.");
            Console.WriteLine($"ACTION_ACTOR_ID={actorId}");
            Console.WriteLine($"ACTION_PEER_ID={peerId}");
            actorPosition = Spawn(actor.CharacterLoginFrame); peerPosition = Spawn(peer.CharacterLoginFrame);
            Check(Math.Abs(actorPosition.Item1 - peerPosition.Item1) <= 33 && Math.Abs(actorPosition.Item2 - peerPosition.Item2) <= 33, "Action fixtures outside relay view.");
            MotionGateNetwork.AssertLogin(actor.CharacterLoginFrame, actorBefore, options.Slot);
            MotionGateNetwork.AssertLogin(peer.CharacterLoginFrame, peerBefore, options.Slot);
            await Phase("ACTION-READY", false);
            for (var index = 0; index < types.Length; index++)
            {
                var action = Step(actorPosition, types[index]); var tick = (uint)(130 + index);
                await actor.Stream.WriteAsync(action.ToFrame(codec, tick, 22, actorId));
                var frames = await Phase($"ACTION-{index}", false);
                if (hp == 0)
                {
                    MotionGateNetwork.AssertHpMode(frames.Actor, actorId, 0);
                    MotionGateNetwork.AssertNoHpMode(frames.Peer);
                    Check(!frames.Actor.Concat(frames.Peer).Any(IsAction), "Dead Action relayed or recalled.");
                }
                else
                {
                    AssertRelay(frames.Actor, action, actorId, tick); AssertRelay(frames.Peer, action, actorId, tick);
                    MotionGateNetwork.AssertNoHpMode(frames.Actor.Concat(frames.Peer).ToArray());
                    actorPosition = (action.TargetX, action.TargetY);
                }
            }
            for (var index = 0; index < types.Length; index++)
            {
                var action = Step(peerPosition, types[index]); var tick = (uint)(150 + index);
                await peer.Stream.WriteAsync(action.ToFrame(codec, tick, 23, peerId));
                var frames = await Phase($"ACTION-LIVING-{index}", true);
                AssertRelay(frames.Actor, action, peerId, tick); AssertRelay(frames.Peer, action, peerId, tick);
                MotionGateNetwork.AssertNoHpMode(frames.Actor.Concat(frames.Peer).ToArray());
                peerPosition = (action.TargetX, action.TargetY);
            }

            var firstStop = Step(peerPosition, ActionRequest.StopMessageType);
            await peer.Stream.WriteAsync(firstStop.ToFrame(codec, 153, 23, peerId));
            var firstStopFrames = await Phase("ACTION-STOP-FIRST", true);
            AssertRelay(firstStopFrames.Peer, firstStop, peerId, 153);
            AssertRelay(firstStopFrames.Actor, firstStop, peerId, 153);
            peerPosition = (firstStop.TargetX, firstStop.TargetY);

            var consecutiveStop = Step(peerPosition, ActionRequest.StopMessageType);
            await peer.Stream.WriteAsync(consecutiveStop.ToFrame(codec, 154, 23, peerId));
            var consecutiveStopFrames = await Phase("ACTION-STOP-REPEATED", false);
            Check(!consecutiveStopFrames.Actor.Concat(consecutiveStopFrames.Peer).Any(IsAction),
                "A consecutive Action2 was relayed instead of being rejected.");

            var movementAfterStop = Step(peerPosition, ActionRequest.MessageType);
            await peer.Stream.WriteAsync(movementAfterStop.ToFrame(codec, 155, 23, peerId));
            var movementAfterStopFrames = await Phase("ACTION-MOVE-AFTER-STOP", true);
            AssertRelay(movementAfterStopFrames.Peer, movementAfterStop, peerId, 155);
            AssertRelay(movementAfterStopFrames.Actor, movementAfterStop, peerId, 155);
            peerPosition = (movementAfterStop.TargetX, movementAfterStop.TargetY);

            var stopAfterMovement = Step(peerPosition, ActionRequest.StopMessageType);
            await peer.Stream.WriteAsync(stopAfterMovement.ToFrame(codec, 156, 23, peerId));
            var stopAfterMovementFrames = await Phase("ACTION-STOP-AFTER-MOVE", true);
            AssertRelay(stopAfterMovementFrames.Peer, stopAfterMovement, peerId, 156);
            AssertRelay(stopAfterMovementFrames.Actor, stopAfterMovement, peerId, 156);
            peerPosition = (stopAfterMovement.TargetX, stopAfterMovement.TargetY);

            var noItems = new LegacyItem[TradeOfferRequest.ItemCount];
            var noPositions = Enumerable.Repeat((sbyte)-1, TradeOfferRequest.ItemCount).ToArray();
            var stagedOffer = new TradeOfferRequest(noItems, noPositions, 0, 0, actorId);
            await peer.Stream.WriteAsync(stagedOffer.ToFrame(codec, 170, 26, peerId));
            var offerPhase = await Phase("ACTION-TRADE-OFFER", true);
            Check(offerPhase.Actor.Any(frame => frame.IsChecksumValid && frame.Header.Type == TradeOfferRequest.MessageType),
                "The bilateral trade fixture did not stage before the Action cancellation check.");

            if (hp == 0)
            {
                var deadAction = Step(actorPosition, ActionRequest.MessageType);
                await actor.Stream.WriteAsync(deadAction.ToFrame(codec, 171, 27, actorId));
                var deadTrade = await Phase("ACTION-DEAD-TRADE", false);
                MotionGateNetwork.AssertHpMode(deadTrade.Actor, actorId, 0);
                AssertNoTradeCancellation(deadTrade.Actor.Concat(deadTrade.Peer).ToArray());
                Check(!deadTrade.Actor.Concat(deadTrade.Peer).Any(IsAction), "HP-zero Action moved or cancelled the staged trade.");

                var cancelAction = Step(peerPosition, ActionRequest.MessageType);
                await peer.Stream.WriteAsync(cancelAction.ToFrame(codec, 172, 28, peerId));
                var cancelled = await Phase("ACTION-TRADE-CANCEL", true);
                AssertTradeCancellation(cancelled.Actor, actorId);
                AssertTradeCancellation(cancelled.Peer, peerId);
                Check(!cancelled.Actor.Concat(cancelled.Peer).Any(IsAction), "Action moved after cancelling the bilateral trade.");
            }
            else
            {
                var cancelAction = Step(actorPosition, ActionRequest.MessageType);
                await actor.Stream.WriteAsync(cancelAction.ToFrame(codec, 171, 27, actorId));
                var cancelled = await Phase("ACTION-TRADE-CANCEL", false);
                AssertTradeCancellation(cancelled.Actor, actorId);
                AssertTradeCancellation(cancelled.Peer, peerId);
                MotionGateNetwork.AssertNoHpMode(cancelled.Actor.Concat(cancelled.Peer).ToArray());
                Check(!cancelled.Actor.Concat(cancelled.Peer).Any(IsAction), "Negative-HP Action moved instead of cancelling the bilateral trade.");
            }

            await actor.Stream.WriteAsync(TradeCloseRequest.ToFrame(codec, 160, 24, actorId));
            var quit = await Phase("ACTION-COUNTER", false);
            MotionGateNetwork.AssertHpMode(quit.Actor, actorId, hp); MotionGateNetwork.AssertNoHpMode(quit.Peer);
            Check(!quit.Actor.Concat(quit.Peer).Any(IsAction), "QuitTrade emitted movement.");
            await MotionGateNetwork.AssertDisk(options.AccountFile, actorBefore, options.Slot);
            await MotionGateNetwork.AssertDisk(peerFile, peerBefore, options.Slot);
            await actor.LogoutAsync(); await peer.LogoutAsync();
            Task<(IReadOnlyList<DecodedFrame> Actor, IReadOnlyList<DecodedFrame> Peer)> Phase(string label, bool peerFirst)
                => AutoTradeCloseNetwork.CapturePhase(actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, label, peerFirst);
        }
        await AssertSavedPosition(options.AccountFile, options.Slot, actorPosition);
        await AssertSavedPosition(peerFile, options.Slot, peerPosition);
        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "ACTION_RELOGIN_"))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "ACTION_PEER_RELOGIN_"))
        {
            Check(Hp(actor.CharacterLoginFrame) == hp, "Action changed persisted HP.");
            // Character entry deliberately rerolls city/newbie spawn; saved destinations were checked before this login.
            actorPosition = Spawn(actor.CharacterLoginFrame); peerPosition = Spawn(peer.CharacterLoginFrame);
            MotionGateNetwork.AssertLogin(actor.CharacterLoginFrame, actorBefore, options.Slot);
            MotionGateNetwork.AssertLogin(peer.CharacterLoginFrame, peerBefore, options.Slot);
            var actorId = Id(actor.CharacterLoginFrame); var peerId = Id(peer.CharacterLoginFrame);
            Console.WriteLine($"ACTION_RELOGIN_ID={actorId}");
            await AutoTradeCloseNetwork.CapturePhase(actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, "ACTION-RELOGIN-READY", false);
            await actor.Stream.WriteAsync(TradeCloseRequest.ToFrame(codec, 161, 25, actorId));
            var quit = await AutoTradeCloseNetwork.CapturePhase(actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, "ACTION-RESET", false);
            MotionGateNetwork.AssertHpMode(quit.Actor, actorId, hp); MotionGateNetwork.AssertNoHpMode(quit.Peer);
            await actor.LogoutAsync(); await peer.LogoutAsync();
        }
        await MotionGateNetwork.AssertDisk(options.AccountFile, actorBefore, options.Slot);
        await MotionGateNetwork.AssertDisk(peerFile, peerBefore, options.Slot);
        await AssertSavedPosition(options.AccountFile, options.Slot, actorPosition);
        await AssertSavedPosition(peerFile, options.Slot, peerPosition);
            Console.WriteLine($"CLIENT_E2E_PASS action-gates hp={hp} three-opcodes repeated-action2-stop private-hp shared-suppressed-counter position-carry-cargo-gold-relogin");
    }

    private static ActionRequest Step((short X, short Y) p, ushort type)
        => new(p.X, p.Y, 0, 2, new byte[24], (short)(p.X + 1), p.Y) { WireType = type };
    private static bool IsAction(DecodedFrame frame) => ActionRequest.TryParse(frame, out _);
    internal static void AssertRelay(IReadOnlyList<DecodedFrame> frames, ActionRequest expected, ushort id, uint tick)
    {
        var actions = frames.Where(IsAction).ToArray();
        Check(actions.Length == 1 && actions[0].Header.Id == id && actions[0].Header.Type == expected.WireType &&
            actions[0].Header.ClientTick == tick && actions[0].Payload.Span.SequenceEqual(expected.ToPayload()), "Action relay changed opcode/ID/tick/payload or duplicated.");
    }
    private static void AssertTradeCancellation(IReadOnlyList<DecodedFrame> frames, ushort recipientId)
    {
        var signals = frames.Where(frame => frame.Header.Type == TradeCloseRequest.MessageType).ToArray();
        Check(signals.Length == 1 && signals[0].IsChecksumValid && signals[0].Header.Size == TradeCloseRequest.PacketSize &&
            signals[0].Header.Id == recipientId && signals[0].Header.ClientTick == 0 && signals[0].Header.KeywordIndex == 0,
            "Action did not emit the exact private RemoveTrade signal once to the expected player.");
    }
    private static void AssertNoTradeCancellation(IReadOnlyList<DecodedFrame> frames)
        => Check(!frames.Any(frame => frame.Header.Type == TradeCloseRequest.MessageType), "HP-zero Action cancelled the staged bilateral trade.");
    private static (short X, short Y) Spawn(DecodedFrame frame)
        => (BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span), BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[2..]));
    private static async Task AssertSavedPosition(string path, int slot, (short X, short Y) p)
    {
        var bytes = await File.ReadAllBytesAsync(path); var offset = 216 + slot * 816;
        Check(BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset + 40)) == p.X && BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset + 42)) == p.Y, "Saved Action position differs from authoritative destination.");
    }
    private static int Hp(DecodedFrame frame) => BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[(CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + 24)..]);
    private static ushort Id(DecodedFrame frame) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);
    private static string CString(ReadOnlySpan<byte> bytes) => Encoding.ASCII.GetString(bytes).Split('\0')[0];
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

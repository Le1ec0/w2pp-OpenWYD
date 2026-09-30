using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using WydCdk.Protocol;

internal static class ActionRangeNetwork
{
    public static async Task RunAsync(E2eOptions options, AccountHeader header, LegacyFrameCodec codec)
    {
        var peerFile = options.AutoTradeClosePeerAccountFile!;
        var actorBefore = await File.ReadAllBytesAsync(options.AccountFile);
        var peerBefore = await File.ReadAllBytesAsync(peerFile);
        var peerHeader = new AccountHeader(CString(peerBefore.AsSpan(0, 16)), CString(peerBefore.AsSpan(16, 12)), peerBefore.AsSpan(202, 6).ToArray());
        (short X, short Y) position = default;
        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, header, codec, "RANGE_", verifyActionModeGate: true))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "RANGE_PEER_"))
        {
            var id = Id(actor.CharacterLoginFrame); var peerId = Id(peer.CharacterLoginFrame);
            position = Spawn(actor.CharacterLoginFrame); var peerPosition = Spawn(peer.CharacterLoginFrame);
            Check(Hp(actor.CharacterLoginFrame) == -1 && Hp(peer.CharacterLoginFrame) > 0 && id != peerId, "Wrong range fixture.");
            Check(Math.Abs(position.X - peerPosition.X) <= 33 && Math.Abs(position.Y - peerPosition.Y) <= 33, "Range peer outside view.");
            Console.WriteLine($"RANGE_ACTOR_POS={position.X},{position.Y} RANGE_PEER_POS={peerPosition.X},{peerPosition.Y}");
            Console.WriteLine($"ACTION_ACTOR_ID={id}");
            await Phase("RANGE-READY");

            var staleTickAction = new ActionRequest(position.X, position.Y, 0, 0, new byte[24], (short)(position.X + 1), position.Y);
            var staleTick = unchecked((uint)Environment.TickCount64 - 120_001u);
            await actor.Stream.WriteAsync(staleTickAction.ToFrame(codec, staleTick, 25, id));
            var staleTickFrames = await Phase("ACTION-TICK-TOO-OLD");
            Check(!staleTickFrames.Actor.Concat(staleTickFrames.Peer).Any(IsAction),
                "Action with a ClientTick more than 120 seconds old was relayed.");

            var futureTickAction = new ActionRequest(position.X, position.Y, 0, 0, new byte[24], (short)(position.X + 1), position.Y);
            var futureTick = unchecked((uint)Environment.TickCount64 + 30_001u);
            await actor.Stream.WriteAsync(futureTickAction.ToFrame(codec, futureTick, 25, id));
            var futureTickFrames = await Phase("ACTION-TICK-TOO-FUTURE");
            Check(!futureTickFrames.Actor.Concat(futureTickFrames.Peer).Any(IsAction),
                "Action with a ClientTick more than 15 seconds in the future was relayed.");

            // Illusion has its own legacy crack-error type even though the tick window matches Action.
            var staleIllusionAction = staleTickAction with { WireType = ActionRequest.IllusionMessageType };
            await actor.Stream.WriteAsync(staleIllusionAction.ToFrame(codec, staleTick, 25, id));
            var staleIllusionFrames = await Phase("ILLUSION-TICK-TOO-OLD");
            Check(!staleIllusionFrames.Actor.Concat(staleIllusionFrames.Peer).Any(IsAction),
                "Action3 with a ClientTick more than 120 seconds old was relayed.");

            var futureIllusionAction = futureTickAction with { WireType = ActionRequest.IllusionMessageType };
            await actor.Stream.WriteAsync(futureIllusionAction.ToFrame(codec, futureTick, 25, id));
            var futureIllusionFrames = await Phase("ILLUSION-TICK-TOO-FUTURE");
            Check(!futureIllusionFrames.Actor.Concat(futureIllusionFrames.Peer).Any(IsAction),
                "Action3 with a ClientTick more than 15 seconds in the future was relayed.");

            // The references do not run the Action timestamp window for MSG_Action2.
            var stopWithFutureTick = new ActionRequest(position.X, position.Y, 0, 0, new byte[24], (short)(position.X + 1), position.Y)
                { WireType = ActionRequest.StopMessageType };
            await actor.Stream.WriteAsync(stopWithFutureTick.ToFrame(codec, uint.MaxValue, 25, id));
            var stopFrames = await Phase("ACTION2-TICK-BYPASS");
            ActionGateNetwork.AssertRelay(stopFrames.Actor, stopWithFutureTick, id, uint.MaxValue);
            ActionGateNetwork.AssertRelay(stopFrames.Peer, stopWithFutureTick, id, uint.MaxValue);
            position = (stopWithFutureTick.TargetX, stopWithFutureTick.TargetY);

            var allowedSpeed = AttackRun(actor.CharacterLoginFrame) & 0xF;
            var speedTypes = new[] { ActionRequest.MessageType, ActionRequest.StopMessageType, ActionRequest.IllusionMessageType };
            for (var speedIndex = 0; speedIndex < speedTypes.Length; speedIndex++)
            {
                var exactTick = CurrentTick();
                var exact = new ActionRequest(position.X, position.Y, 0, allowedSpeed, new byte[24], (short)(position.X + 1), position.Y)
                    { WireType = speedTypes[speedIndex] };
                await actor.Stream.WriteAsync(exact.ToFrame(codec, exactTick, 26, id));
                var exactFrames = await Phase($"RANGE-SPEED-EXACT-{speedIndex}");
                ActionGateNetwork.AssertRelay(exactFrames.Actor, exact, id, exactTick);
                ActionGateNetwork.AssertRelay(exactFrames.Peer, exact, id, exactTick);
                position = (exact.TargetX, exact.TargetY);

                if (speedTypes[speedIndex] == ActionRequest.StopMessageType)
                {
                    // The legacy Action2 anti-spam gate rejects consecutive stops, so separate
                    // this valid stop from its speed-clamp probe with a normal movement.
                    var separator = new ActionRequest(position.X, position.Y, 0, allowedSpeed, new byte[24], (short)(position.X + 1), position.Y);
                    var separatorTick = CurrentTick();
                    await actor.Stream.WriteAsync(separator.ToFrame(codec, separatorTick, 26, id));
                    var separatorFrames = await Phase("RANGE-SPEED-STOP-SEPARATOR");
                    ActionGateNetwork.AssertRelay(separatorFrames.Actor, separator, id, separatorTick);
                    ActionGateNetwork.AssertRelay(separatorFrames.Peer, separator, id, separatorTick);
                    position = (separator.TargetX, separator.TargetY);
                }

                var excess = exact with { PositionX = position.X, PositionY = position.Y, Speed = allowedSpeed + 1,
                    WireType = speedTypes[speedIndex], TargetX = (short)(position.X + 1), TargetY = position.Y };
                var excessTick = CurrentTick();
                await actor.Stream.WriteAsync(excess.ToFrame(codec, excessTick, 26, id));
                var excessFrames = await Phase($"RANGE-SPEED-EXCESS-{speedIndex}");
                var corrected = excess with { Speed = allowedSpeed };
                ActionGateNetwork.AssertRelay(excessFrames.Actor, corrected, id, excessTick);
                ActionGateNetwork.AssertRelay(excessFrames.Peer, corrected, id, excessTick);
                position = (excess.TargetX, excess.TargetY);
            }

            var dualViolation = new ActionRequest(100, 100, 0, allowedSpeed + 1, new byte[24], (short)(position.X + 34), position.Y)
                { WireType = ActionRequest.MessageType };
            await actor.Stream.WriteAsync(dualViolation.ToFrame(codec, CurrentTick(), 26, id));
            var dualFrames = await Phase("RANGE-SPEED-AND-RANGE");
            Check(!dualFrames.Actor.Concat(dualFrames.Peer).Any(IsAction), "Speed/range dual violation moved or relayed.");

            var index = 0;
            foreach (var type in new[] { ActionRequest.MessageType, ActionRequest.StopMessageType, ActionRequest.IllusionMessageType })
            {
                foreach (var delta in new (int X, int Y, bool Correct)[] {
                    (34,0,false),(66,0,false),(67,0,true),(-34,0,false),(-66,0,false),(-67,0,true),
                    (0,34,false),(0,66,false),(0,67,true),(0,-34,false),(0,-66,false),(0,-67,true),
                    (33,34,false),(33,67,true) })
                {
                    // Deliberately stale origin, nonzero route and all Effects: none may bypass or contaminate correction.
                    var request = new ActionRequest(100, 100, index % 3, 0, Enumerable.Repeat((byte)9, 24).ToArray(),
                        (short)(position.X + delta.X), (short)(position.Y + delta.Y)) { WireType = type };
                    await actor.Stream.WriteAsync(request.ToFrame(codec, CurrentTick(), 26, id));
                    var frames = await Phase($"RANGE-REJECT-{index++}");
                    var actions = frames.Actor.Where(IsAction).ToArray();
                    Check(!frames.Peer.Any(IsAction), "Rejected range leaked action to peer.");
                    MotionGateNetwork.AssertNoHpMode(frames.Actor.Concat(frames.Peer).ToArray());
                    if (delta.Correct) AssertCorrection(actions, id, position);
                    else Check(actions.Length == 0, "34..66 incorrectly corrected or relayed.");
                }
                // Inclusive rectangle boundary; move toward peer so the peer stays in the new 33x33 view.
                var approachX = peerPosition.X < position.X ? -17 : peerPosition.X > position.X ? 17 : 0;
                var approachY = peerPosition.Y < position.Y ? -17 : peerPosition.Y > position.Y ? 17 : 0;
                var viewStep = new ActionRequest(position.X, position.Y, 0, 0, new byte[24], (short)(position.X + approachX), (short)(position.Y + approachY)) { WireType = type };
                var viewInTick = CurrentTick();
                await actor.Stream.WriteAsync(viewStep.ToFrame(codec, viewInTick, 27, id));
                var viewIn = await Phase("RANGE-VIEW-IN");
                ActionGateNetwork.AssertRelay(viewIn.Actor, viewStep, id, viewInTick);
                ActionGateNetwork.AssertRelay(viewIn.Peer, viewStep, id, viewInTick);

                var viewBackType = type == ActionRequest.StopMessageType ? ActionRequest.MessageType : type;
                var viewBack = new ActionRequest(viewStep.TargetX, viewStep.TargetY, 0, 0, new byte[24], position.X, position.Y) { WireType = viewBackType };
                var viewBackTick = CurrentTick();
                await actor.Stream.WriteAsync(viewBack.ToFrame(codec, viewBackTick, 27, id));
                var viewOut = await Phase("RANGE-VIEW-OUT");
                ActionGateNetwork.AssertRelay(viewOut.Actor, viewBack, id, viewBackTick);
                ActionGateNetwork.AssertRelay(viewOut.Peer, viewBack, id, viewBackTick);

                var dx = peerPosition.X < position.X ? -33 : 33;
                var dy = peerPosition.Y < position.Y ? -33 : 33;
                var boundary = new ActionRequest(100, 100, 0, 0, new byte[24], (short)(position.X + dx), (short)(position.Y + dy)) { WireType = type };
                var boundaryTick = CurrentTick();
                await actor.Stream.WriteAsync(boundary.ToFrame(codec, boundaryTick, 27, id));
                var accepted = await ReadOne(actor.Stream, codec, options.Timeout);
                Check(accepted.Header.Id == id && accepted.Header.Type == type && accepted.Header.ClientTick == boundaryTick
                    && accepted.Payload.Span.SequenceEqual(boundary.ToPayload()), "Accepted 33-cell boundary was not echoed privately.");
                var peerCanSeeBoundary = Math.Abs(boundary.TargetX - peerPosition.X) <= 16
                    && Math.Abs(boundary.TargetY - peerPosition.Y) <= 16;
                if (peerCanSeeBoundary)
                {
                    var peerAccepted = await ReadOne(peer.Stream, codec, options.Timeout);
                    Check(peerAccepted.Header.Id == id && peerAccepted.Header.Type == type && peerAccepted.Header.ClientTick == boundaryTick
                        && peerAccepted.Payload.Span.SequenceEqual(boundary.ToPayload()), "Accepted boundary was not relayed within the peer's multicast view.");
                }
                var backType = type == ActionRequest.StopMessageType ? ActionRequest.MessageType : type;
                var back = new ActionRequest(boundary.TargetX, boundary.TargetY, 0, 0, new byte[24], position.X, position.Y) { WireType = backType };
                var returnTick = CurrentTick();
                await actor.Stream.WriteAsync(back.ToFrame(codec, returnTick, 27, id));
                var returned = await Phase("RANGE-RETURN");
                ActionGateNetwork.AssertRelay(returned.Actor, back, id, returnTick);
                ActionGateNetwork.AssertRelay(returned.Peer.Where(frame => frame.Header.ClientTick == returnTick && IsAction(frame)).ToArray(), back, id, returnTick);
                Check(!returned.Peer.Any(frame => frame.Header.ClientTick == 300 && IsAction(frame)), "Out-of-view boundary movement leaked to the peer.");
            }
            await actor.Stream.WriteAsync(TradeCloseRequest.ToFrame(codec, CurrentTick(), 28, id));
            var quit = await Phase("RANGE-COUNTER");
            MotionGateNetwork.AssertHpMode(quit.Actor, id, -1); MotionGateNetwork.AssertNoHpMode(quit.Peer);
            Check(!quit.Actor.Concat(quit.Peer).Any(IsAction), "Counter probe emitted movement.");
            await actor.LogoutAsync(); await peer.LogoutAsync();
            Task<(IReadOnlyList<DecodedFrame> Actor, IReadOnlyList<DecodedFrame> Peer)> Phase(string label)
                => AutoTradeCloseNetwork.CapturePhase(actor.Stream, peer.Stream, id, peerId, codec, options.Timeout, label, false);
        }
        var saved = await File.ReadAllBytesAsync(options.AccountFile); var offset = 216 + options.Slot * 816;
        Check(BinaryPrimitives.ReadInt16LittleEndian(saved.AsSpan(offset + 40)) == position.X && BinaryPrimitives.ReadInt16LittleEndian(saved.AsSpan(offset + 42)) == position.Y, "Range rejection changed saved position.");
        await MotionGateNetwork.AssertDisk(options.AccountFile, actorBefore, options.Slot);
        await MotionGateNetwork.AssertDisk(peerFile, peerBefore, options.Slot);
        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, header, codec, "RANGE_RELOGIN_"))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "RANGE_PEER_RELOGIN_"))
        {
            var id = Id(actor.CharacterLoginFrame); var peerId = Id(peer.CharacterLoginFrame);
            Console.WriteLine($"ACTION_RELOGIN_ID={id}");
            Check(Hp(actor.CharacterLoginFrame) == -1, "Range changed persisted HP.");
            MotionGateNetwork.AssertLogin(actor.CharacterLoginFrame, actorBefore, options.Slot);
            MotionGateNetwork.AssertLogin(peer.CharacterLoginFrame, peerBefore, options.Slot);
            await AutoTradeCloseNetwork.CapturePhase(actor.Stream, peer.Stream, id, peerId, codec, options.Timeout, "RANGE-RESET-READY", false);
            await actor.Stream.WriteAsync(TradeCloseRequest.ToFrame(codec, CurrentTick(), 28, id));
            var reset = await AutoTradeCloseNetwork.CapturePhase(actor.Stream, peer.Stream, id, peerId, codec, options.Timeout, "RANGE-RESET", false);
            MotionGateNetwork.AssertHpMode(reset.Actor, id, -1); MotionGateNetwork.AssertNoHpMode(reset.Peer);
            await actor.LogoutAsync(); await peer.LogoutAsync();
        }
        await MotionGateNetwork.AssertDisk(options.AccountFile, actorBefore, options.Slot);
        await MotionGateNetwork.AssertDisk(peerFile, peerBefore, options.Slot);
        Console.WriteLine("CLIENT_E2E_PASS action-range old-future-ticks-rejected illusion-old-future-ticks-rejected action2-tick-bypass speed-equal-speed-clamped three-opcodes dual-violation 33-34-66-67 private-correction position-carry-cargo-gold-relogin");
    }

    private static uint CurrentTick() => unchecked((uint)Environment.TickCount64);
    internal static void AssertCorrection(IReadOnlyList<DecodedFrame> frames, ushort id, (short X, short Y) position)
    {
        var expected = new ActionRequest(position.X, position.Y, 1, 6, new byte[24], position.X, position.Y) { WireType = ActionRequest.IllusionMessageType };
        Check(frames.Count == 1 && frames[0].Header.Type == 0x0368 && frames[0].Header.Size == 52 && frames[0].Header.Id == id
            && frames[0].Header.ClientTick != uint.MaxValue && frames[0].Payload.Span.SequenceEqual(expected.ToPayload()), "Private correction differs from GetAction/Action3 contract.");
    }
    private static bool IsAction(DecodedFrame frame) => ActionRequest.TryParse(frame, out _);
    private static void AssertActorAndPeerRelay(
        (IReadOnlyList<DecodedFrame> Actor, IReadOnlyList<DecodedFrame> Peer) frames,
        ActionRequest expected,
        ushort id,
        uint tick,
        (short X, short Y) peerPosition)
    {
        ActionGateNetwork.AssertRelay(frames.Actor, expected, id, tick);
        var peerCanSee = Math.Abs(expected.TargetX - peerPosition.X) <= 16 && Math.Abs(expected.TargetY - peerPosition.Y) <= 16;
        if (peerCanSee)
            ActionGateNetwork.AssertRelay(frames.Peer, expected, id, tick);
        else
            Check(!frames.Peer.Any(IsAction), "Movement separator leaked outside the peer's multicast view.");
    }
    private static async Task<DecodedFrame> ReadOne(NetworkStream stream, LegacyFrameCodec codec, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        var headerBytes = new byte[PacketHeader.SizeInBytes];
        await stream.ReadExactlyAsync(headerBytes, cancellation.Token);
        var header = PacketHeader.Read(headerBytes);
        Check(header.Size >= PacketHeader.SizeInBytes && header.Size <= PacketHeader.MaxPacketSize, "Invalid action response frame size.");
        var bytes = new byte[header.Size];
        headerBytes.CopyTo(bytes, 0);
        await stream.ReadExactlyAsync(bytes.AsMemory(PacketHeader.SizeInBytes), cancellation.Token);
        var frame = codec.Decode(bytes);
        Check(frame.IsChecksumValid, "Action response checksum invalid.");
        return frame;
    }
    private static (short X, short Y) Spawn(DecodedFrame frame) => (BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span), BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[2..]));
    private static int AttackRun(DecodedFrame frame) => frame.Payload.Span[CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + 13];
    private static ushort Id(DecodedFrame frame) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);
    private static int Hp(DecodedFrame frame) => BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[(CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + 24)..]);
    private static string CString(ReadOnlySpan<byte> bytes) => Encoding.ASCII.GetString(bytes).Split('\0')[0];
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

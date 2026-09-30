using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using WydCdk.Protocol;

internal static class ActionOriginRecallNetwork
{
    private const short RestrictedSectorOriginX = 1664;
    private const short RestrictedSectorOriginY = 1920;
    private const short MapRestrictionOriginX = 2063;
    private const short MapRestrictionOriginY = 2100;
    private const short MapRestrictionDistantObserverX = 1400;
    private const short MapRestrictionDistantObserverY = 1400;

    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec, bool mapRestriction = false)
    {
        var originObserverFile = options.AutoTradeClosePeerAccountFile!;
        var destinationObserverFile = options.ActionRecallDestinationObserverAccountFile!;
        var distantObserverFile = options.ActionRecallDistantObserverAccountFile!;
        var originObserverHeader = await ReadHeaderAsync(originObserverFile);
        var destinationObserverHeader = await ReadHeaderAsync(destinationObserverFile);
        var distantObserverHeader = await ReadHeaderAsync(distantObserverFile);
        ActionRequest recall;
        ushort originDropWireId;
        ushort destinationDropWireId;
        (short X, short Y) distantPosition = default;
        (short X, short Y) mapDestinationPosition = default;

        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "RECALL_ACTOR_"))
        await using (var originObserver = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = originObserverFile }, originObserverHeader, codec, "RECALL_ORIGIN_"))
        await using (var destinationObserver = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = destinationObserverFile }, destinationObserverHeader, codec, "RECALL_DESTINATION_"))
        await using (var distantObserver = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = distantObserverFile }, distantObserverHeader, codec, "RECALL_DISTANT_"))
        {
            var actorId = Id(actor.CharacterLoginFrame);
            var originObserverId = Id(originObserver.CharacterLoginFrame);
            var destinationObserverId = Id(destinationObserver.CharacterLoginFrame);
            var distantObserverId = Id(distantObserver.CharacterLoginFrame);
            var ids = new[] { actorId, originObserverId, destinationObserverId, distantObserverId };
            Check(ids.All(static id => id > 0) && ids.Distinct().Count() == ids.Length,
                "Recall fixture did not create four distinct player IDs.");

            var actorPosition = Spawn(actor.CharacterLoginFrame);
            var originPosition = Spawn(originObserver.CharacterLoginFrame);
            var destinationPosition = Spawn(destinationObserver.CharacterLoginFrame);
            distantPosition = Spawn(distantObserver.CharacterLoginFrame);
            mapDestinationPosition = destinationPosition;

            if (mapRestriction)
            {
                uint actorMapTick = 200;
                var actorMapMove = await MoveAxisToAsync(actor.Stream, actorId, actorPosition, moveX: false,
                    target: 2099, actorMapTick, codec, options.Timeout);
                actorPosition = (actorMapMove.X, actorMapMove.Y);
                actorMapTick = actorMapMove.NextTick;
                actorMapMove = await MoveAxisToAsync(actor.Stream, actorId, actorPosition, moveX: true,
                    target: MapRestrictionOriginX, actorMapTick, codec, options.Timeout);
                actorPosition = (actorMapMove.X, actorMapMove.Y);
                actorMapTick = actorMapMove.NextTick;
                actorMapMove = await MoveAxisToAsync(actor.Stream, actorId, actorPosition, moveX: false,
                    target: MapRestrictionOriginY, actorMapTick, codec, options.Timeout);
                actorPosition = (actorMapMove.X, actorMapMove.Y);

                uint originMapTick = 500;
                var originMapMove = await MoveAxisToAsync(originObserver.Stream, originObserverId, originPosition, moveX: false,
                    target: 2099, originMapTick, codec, options.Timeout);
                originPosition = (originMapMove.X, originMapMove.Y);
                originMapTick = originMapMove.NextTick;
                originMapMove = await MoveAxisToAsync(originObserver.Stream, originObserverId, originPosition, moveX: true,
                    target: MapRestrictionOriginX, originMapTick, codec, options.Timeout);
                originPosition = (originMapMove.X, originMapMove.Y);
                originMapTick = originMapMove.NextTick;
                originMapMove = await MoveAxisToAsync(originObserver.Stream, originObserverId, originPosition, moveX: false,
                    target: MapRestrictionOriginY, originMapTick, codec, options.Timeout);
                originPosition = (originMapMove.X, originMapMove.Y);

                uint distantTick = 1100;
                var distantMove = await MoveAxisToAsync(distantObserver.Stream, distantObserverId, distantPosition, moveX: false,
                    target: 2099, distantTick, codec, options.Timeout);
                distantPosition = (distantMove.X, distantMove.Y);
                distantTick = distantMove.NextTick;
                distantMove = await MoveAxisToAsync(distantObserver.Stream, distantObserverId, distantPosition, moveX: true,
                    target: MapRestrictionDistantObserverX, distantTick, codec, options.Timeout);
                distantPosition = (distantMove.X, distantMove.Y);
                distantTick = distantMove.NextTick;
                distantMove = await MoveAxisToAsync(distantObserver.Stream, distantObserverId, distantPosition, moveX: false,
                    target: MapRestrictionDistantObserverY, distantTick, codec, options.Timeout);
                distantPosition = (distantMove.X, distantMove.Y);

                Check(actorPosition == (MapRestrictionOriginX, MapRestrictionOriginY) && originPosition == actorPosition &&
                    destinationPosition.X is >= 2086 and <= 2100 && destinationPosition.Y is >= 2093 and <= 2107 &&
                    distantPosition == (MapRestrictionDistantObserverX, MapRestrictionDistantObserverY),
                    $"Map restriction recall fixture positions differ: actor={actorPosition}, origin={originPosition}, destination={destinationPosition}, distant={distantPosition}.");
            }
            else
            {
                uint actorTick = 200;
                var actorMove = await MoveAxisToAsync(actor.Stream, actorId, actorPosition, moveX: false,
                    target: (short)(RestrictedSectorOriginY - 1), actorTick, codec, options.Timeout);
                actorPosition = (actorMove.X, actorMove.Y);
                actorTick = actorMove.NextTick;
                actorMove = await MoveAxisToAsync(actor.Stream, actorId, actorPosition, moveX: true,
                    RestrictedSectorOriginX, actorTick, codec, options.Timeout);
                actorPosition = (actorMove.X, actorMove.Y);
                actorTick = actorMove.NextTick;
                actorMove = await MoveAxisToAsync(actor.Stream, actorId, actorPosition, moveX: false,
                    RestrictedSectorOriginY, actorTick, codec, options.Timeout);
                actorPosition = (actorMove.X, actorMove.Y);

                uint originTick = 500;
                var originMove = await MoveAxisToAsync(originObserver.Stream, originObserverId, originPosition, moveX: false,
                    (short)(RestrictedSectorOriginY - 1), originTick, codec, options.Timeout);
                originPosition = (originMove.X, originMove.Y);
                originTick = originMove.NextTick;
                originMove = await MoveAxisToAsync(originObserver.Stream, originObserverId, originPosition, moveX: true,
                    RestrictedSectorOriginX, originTick, codec, options.Timeout);
                originPosition = (originMove.X, originMove.Y);
                originTick = originMove.NextTick;
                originMove = await MoveAxisToAsync(originObserver.Stream, originObserverId, originPosition, moveX: false,
                    RestrictedSectorOriginY, originTick, codec, options.Timeout);
                originPosition = (originMove.X, originMove.Y);

                uint destinationTick = 800;
                var destinationMove = await MoveAxisToAsync(destinationObserver.Stream, destinationObserverId, destinationPosition, moveX: false,
                    2042, destinationTick, codec, options.Timeout);
                destinationPosition = (destinationMove.X, destinationMove.Y);
                destinationTick = destinationMove.NextTick;
                destinationMove = await MoveAxisToAsync(destinationObserver.Stream, destinationObserverId, destinationPosition, moveX: true,
                    2112, destinationTick, codec, options.Timeout);
                destinationPosition = (destinationMove.X, destinationMove.Y);
                Check(actorPosition == (RestrictedSectorOriginX, RestrictedSectorOriginY) && originPosition == actorPosition &&
                    destinationPosition == (2112, 2042),
                    $"Recall fixture positions differ after staging: actor={actorPosition}, origin={originPosition}, destination={destinationPosition}, distant={distantPosition}.");
            }
            await DrainInitialFramesAsync([actor.Stream, originObserver.Stream, destinationObserver.Stream, distantObserver.Stream], codec, options.Timeout);

            originDropWireId = await DropCarryItemAsync(
                actor.Stream, actorId, actorPosition.X, actorPosition.Y, 1200, codec, options.Timeout);
            destinationDropWireId = await DropCarryItemAsync(
                destinationObserver.Stream, destinationObserverId, destinationPosition.X, destinationPosition.Y, 1201, codec, options.Timeout);
            Check(originDropWireId == 10_001 && destinationDropWireId == 10_002,
                $"The recall fixture expected distinct source/destination drop IDs, got {originDropWireId} and {destinationDropWireId}.");
            await DrainInitialFramesAsync([actor.Stream, originObserver.Stream, destinationObserver.Stream, distantObserver.Stream], codec, options.Timeout);

            Console.WriteLine($"ACTION_RECALL_ACTOR_ID={actorId}");
            Console.WriteLine($"ACTION_RECALL_ORIGIN_OBSERVER_ID={originObserverId}");
            Console.WriteLine($"ACTION_RECALL_DESTINATION_OBSERVER_ID={destinationObserverId}");
            Console.WriteLine($"ACTION_RECALL_DISTANT_OBSERVER_ID={distantObserverId}");
            Console.WriteLine($"ACTION_RECALL_SCOPES_READY actor=({actorPosition.X},{actorPosition.Y}) origin=({originPosition.X},{originPosition.Y}) destination=({destinationPosition.X},{destinationPosition.Y}) distant=({distantPosition.X},{distantPosition.Y})");

            var trigger = new ActionRequest(
                actorPosition.X,
                actorPosition.Y,
                Effect: 0,
                Speed: 0,
                Route: new byte[24],
                TargetX: (short)(actorPosition.X + 1),
                TargetY: actorPosition.Y);
            await actor.Stream.WriteAsync(trigger.ToFrame(codec, 200, 27, actorId));
            var actorFramesTask = ReadUntilRecallAsync(actor.Stream, codec, options.Timeout, actorId);
            var originFramesTask = ReadUntilRecallAsync(originObserver.Stream, codec, options.Timeout, actorId);
            var destinationFramesTask = ReadUntilRecallAsync(destinationObserver.Stream, codec, options.Timeout, actorId);
            var distantFramesTask = ReadRecallOrQuietAsync(distantObserver.Stream, codec, TimeSpan.FromMilliseconds(350), actorId);
            await Task.WhenAll(actorFramesTask, originFramesTask, destinationFramesTask, distantFramesTask);
            recall = AssertRecall(actorFramesTask.Result, actorId, actorPosition.X, actorPosition.Y);
            var originRecall = AssertRecall(originFramesTask.Result, actorId, actorPosition.X, actorPosition.Y);
            var destinationRecall = AssertRecall(destinationFramesTask.Result, actorId, actorPosition.X, actorPosition.Y);
            Check(recall.ToPayload().SequenceEqual(originRecall.ToPayload()) && recall.ToPayload().SequenceEqual(destinationRecall.ToPayload()),
                "A visible observer received a different recall payload from the actor.");
            Check(!distantFramesTask.Result.Any(IsRecallFrame), "A player outside both 33x33 views received the recall Action.");
            if (mapRestriction)
            {
                Check(actorFramesTask.Result.Any(frame => frame.Header.Type == MessagePanelConfirmation.MessageType),
                    "The movement map gate did not send its user-facing restriction notice before recall.");
                Check(recall.TargetX != actorPosition.X || recall.TargetY != actorPosition.Y,
                    "Movement restriction recall left the character at the blocked target origin.");
            }
            else
                Check(recall.TargetX / 128 != 13 || recall.TargetY / 128 != 15,
                    "Recall destination remained inside restricted sector (13,15).");
            Check(Math.Abs(recall.TargetX - destinationPosition.X) <= 16 && Math.Abs(recall.TargetY - destinationPosition.Y) <= 16,
                $"Destination observer was not in the recall destination view: destination=({recall.TargetX},{recall.TargetY}), observer={destinationPosition}.");
            Check(Math.Abs(recall.TargetX - distantPosition.X) > 16 || Math.Abs(recall.TargetY - distantPosition.Y) > 16,
                $"Distant observer unexpectedly lies inside the recall destination view: destination=({recall.TargetX},{recall.TargetY}), observer={distantPosition}.");

            var actorTransition = await ReadUntilRequiredAsync(actor.Stream, codec, options.Timeout,
                frame => IsCreateMobFor(frame, destinationObserverId),
                frame => IsPkInfoFor(frame, destinationObserverId),
                frame => frame.Header.Type == RemoveMobConfirmation.MessageType && frame.Header.Id == originObserverId,
                frame => IsDecayItem(frame, originDropWireId),
                frame => IsCreateItem(frame, destinationDropWireId));
            Check(actorTransition.Any(frame => IsCreateMobFor(frame, destinationObserverId)) &&
                actorTransition.Any(frame => IsPkInfoFor(frame, destinationObserverId)) &&
                actorTransition.Any(frame => frame.Header.Type == RemoveMobConfirmation.MessageType && frame.Header.Id == originObserverId) &&
                actorTransition.Any(frame => IsDecayItem(frame, originDropWireId)) &&
                actorTransition.Any(frame => IsCreateItem(frame, destinationDropWireId)),
                "The recalling player did not receive destination CreateMob/PKInfo, origin RemoveMob, and the ground-item view delta.");

            var originRemoval = await ReadUntilRequiredAsync(originObserver.Stream, codec, options.Timeout,
                frame => frame.Header.Type == RemoveMobConfirmation.MessageType && frame.Header.Id == actorId);
            Check(originRemoval.Any(frame => frame.Header.Type == RemoveMobConfirmation.MessageType && frame.Header.Id == actorId),
                "The origin observer did not remove the recalled player.");
            Check(destinationFramesTask.Result.Any(frame => IsCreateMobFor(frame, actorId)) &&
                destinationFramesTask.Result.Any(frame => IsPkInfoFor(frame, actorId)),
                "The destination observer did not receive the recalling player's CreateMob and PKInfo.");
            Check(!originFramesTask.Result.Concat(destinationFramesTask.Result).Concat(distantFramesTask.Result)
                    .Any(frame => IsDecayItem(frame, originDropWireId) || IsCreateItem(frame, destinationDropWireId)),
                "A non-recalling player received the ground-item view delta.");

            var mode = mapRestriction ? "movement-map-recall" : "action-origin-recall";
            Console.WriteLine($"CLIENT_E2E_PASS {mode} from=({actorPosition.X},{actorPosition.Y}) to=({recall.TargetX},{recall.TargetY}) actor-origin-destination-distant");

            await actor.LogoutAsync();
            await originObserver.LogoutAsync();
            await destinationObserver.LogoutAsync();
            await distantObserver.LogoutAsync();
        }

        await AssertSavedPosition(options.AccountFile, options.Slot, recall.TargetX, recall.TargetY);
        var expectedOrigin = mapRestriction ? (MapRestrictionOriginX, MapRestrictionOriginY) : (RestrictedSectorOriginX, RestrictedSectorOriginY);
        var expectedDestination = mapRestriction
            ? mapDestinationPosition
            : ((short)2112, (short)2042);
        await AssertSavedPosition(originObserverFile, options.Slot, expectedOrigin.Item1, expectedOrigin.Item2);
        await AssertSavedPosition(destinationObserverFile, options.Slot, expectedDestination.Item1, expectedDestination.Item2);
        await AssertSavedPosition(distantObserverFile, options.Slot, distantPosition.X, distantPosition.Y);
    }

    private static async Task<AccountHeader> ReadHeaderAsync(string path)
    {
        var bytes = await File.ReadAllBytesAsync(path);
        return new AccountHeader(CString(bytes.AsSpan(0, 16)), CString(bytes.AsSpan(16, 12)), bytes.AsSpan(202, 6).ToArray());
    }

    private static async Task<ushort> DropCarryItemAsync(
        NetworkStream stream,
        ushort characterId,
        short gridX,
        short gridY,
        uint clientTick,
        LegacyFrameCodec codec,
        TimeSpan timeout)
    {
        var payload = new byte[DropItemRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(payload, 1);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int)), 0);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int) * 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(sizeof(int) * 3), checked((ushort)gridX));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan((sizeof(int) * 3) + sizeof(ushort)), checked((ushort)gridY));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan((sizeof(int) * 3) + (sizeof(ushort) * 2)), 0);
        await stream.WriteAsync(codec.Encode(DropItemRequest.MessageType, characterId, clientTick, payload, 27));

        var frames = await ReadUntilRequiredAsync(stream, codec, timeout,
            frame => frame.Header.Type == DropItemConfirmation.MessageType,
            frame => IsCreateItemAt(frame, gridX, gridY));
        var confirmation = frames.Single(frame => frame.Header.Type == DropItemConfirmation.MessageType);
        Check(confirmation.IsChecksumValid && confirmation.Header.Size == DropItemConfirmation.PacketSize,
            "Ground-item drop confirmation had an invalid checksum or layout.");
        var create = frames.Single(frame => IsCreateItemAt(frame, gridX, gridY));
        Check(create.IsChecksumValid && create.Header.Size == CreateItemConfirmation.PacketSize,
            "Ground-item create frame had an invalid checksum or layout.");
        return BinaryPrimitives.ReadUInt16LittleEndian(create.Payload.Span[4..]);
    }

    private static bool IsCreateItemAt(DecodedFrame frame, short gridX, short gridY)
        => frame.Header.Type == CreateItemConfirmation.MessageType &&
           frame.Payload.Length == CreateItemConfirmation.PayloadSize &&
           BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span) == gridX &&
           BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[2..]) == gridY;

    private static bool IsCreateItem(DecodedFrame frame, ushort wireItemId)
        => frame.Header.Type == CreateItemConfirmation.MessageType &&
           frame.Payload.Length == CreateItemConfirmation.PayloadSize &&
           BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[4..]) == wireItemId;

    private static bool IsDecayItem(DecodedFrame frame, ushort wireItemId)
        => frame.Header.Type == DecayItemConfirmation.MessageType &&
           frame.Payload.Length == DecayItemConfirmation.PayloadSize &&
           BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span) == checked((short)wireItemId);

    private static async Task DrainInitialFramesAsync(IReadOnlyList<NetworkStream> streams, LegacyFrameCodec codec, TimeSpan timeout)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        for (var round = 0; round < 8; round++)
        {
            var drained = false;
            foreach (var stream in streams)
            {
                while (stream.DataAvailable)
                {
                    _ = await ReadFrameAsync(stream, codec, timeout);
                    drained = true;
                }
            }
            if (!drained) return;
            await Task.Delay(TimeSpan.FromMilliseconds(20));
        }
    }

    internal static ActionRequest AssertRecall(IReadOnlyList<DecodedFrame> frames, ushort sourceId, short fromX, short fromY)
    {
        var actions = frames.Where(frame => ActionRequest.TryParse(frame, out _)).ToArray();
        Check(actions.Length == 1, "Expected exactly one Action frame for the origin recall.");
        var frame = actions[0];
        Check(frame.IsChecksumValid && frame.Header.Type == ActionRequest.MessageType &&
            frame.Header.Size == ActionRequest.PacketSize && frame.Header.Id == sourceId,
            "Recall Action frame has the wrong checksum, opcode, size, or source ID.");
        Check(ActionRequest.TryParse(frame, out var action) && action is not null,
            "Recall Action frame could not be parsed.");
        var recalledAction = action ?? throw new InvalidOperationException("Recall Action frame could not be parsed.");
        Check(recalledAction.PositionX == fromX && recalledAction.PositionY == fromY && recalledAction.Effect == 1 && recalledAction.Speed == 2,
            "Recall Action frame has the wrong source position, effect, or speed.");
        Check(recalledAction.Route.Length == 24 && recalledAction.Route.All(value => value == 0),
            "Recall Action frame unexpectedly includes a client route.");
        Check(recalledAction.TargetX is > 0 and < 4096 && recalledAction.TargetY is > 0 and < 4096 &&
            (recalledAction.TargetX != fromX || recalledAction.TargetY != fromY),
            "Recall Action frame did not move to a valid destination.");
        return recalledAction;
    }

    private static async Task<MoveProgress> MoveAxisToAsync(
        NetworkStream stream,
        ushort characterId,
        (short X, short Y) position,
        bool moveX,
        short target,
        uint tick,
        LegacyFrameCodec codec,
        TimeSpan timeout)
    {
        while ((moveX ? position.X : position.Y) != target)
        {
            var current = moveX ? position.X : position.Y;
            var next = checked((short)(current + Math.Clamp((int)target - current, -33, 33)));
            var destination = moveX ? (X: next, Y: position.Y) : (X: position.X, Y: next);
            var request = new ActionRequest(position.X, position.Y, 0, 0, new byte[24], destination.X, destination.Y);
            await stream.WriteAsync(request.ToFrame(codec, tick, 27, characterId));
            var response = await ReadOwnActionAsync(stream, codec, timeout, characterId);
            ActionGateNetwork.AssertRelay([response], request, characterId, tick);
            position = destination;
            tick++;
        }

        return new MoveProgress(position.X, position.Y, tick);
    }

    private static async Task<DecodedFrame> ReadOwnActionAsync(
        NetworkStream stream,
        LegacyFrameCodec codec,
        TimeSpan timeout,
        ushort characterId)
    {
        for (var index = 0; index < 64; index++)
        {
            var frame = await ReadFrameAsync(stream, codec, timeout);
            if (ActionRequest.TryParse(frame, out _) && frame.Header.Id == characterId)
                return frame;
        }

        throw new InvalidOperationException("The movement echo for the recall fixture was not received.");
    }

    private static async Task<IReadOnlyList<DecodedFrame>> ReadUntilRecallAsync(
        NetworkStream stream,
        LegacyFrameCodec codec,
        TimeSpan timeout,
        ushort characterId)
    {
        var frames = new List<DecodedFrame>();
        for (var index = 0; index < 64; index++)
        {
            var frame = await ReadFrameAsync(stream, codec, timeout);
            frames.Add(frame);
            if (frame.Header.Id == characterId && IsRecallFrame(frame))
                return frames;
        }

        throw new InvalidOperationException("The origin recall Action was not received before the frame limit.");
    }

    private static async Task<IReadOnlyList<DecodedFrame>> ReadRecallOrQuietAsync(
        NetworkStream stream,
        LegacyFrameCodec codec,
        TimeSpan quietWindow,
        ushort characterId)
    {
        var frames = new List<DecodedFrame>();
        var deadline = DateTime.UtcNow + quietWindow;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var frame = await ReadFrameAsync(stream, codec, deadline - DateTime.UtcNow);
                frames.Add(frame);
                if (frame.Header.Id == characterId && IsRecallFrame(frame)) return frames;
            }
            catch (OperationCanceledException)
            {
                return frames;
            }
        }
        return frames;
    }

    private static async Task<IReadOnlyList<DecodedFrame>> ReadUntilRequiredAsync(
        NetworkStream stream,
        LegacyFrameCodec codec,
        TimeSpan timeout,
        params Func<DecodedFrame, bool>[] requiredFrames)
    {
        var frames = new List<DecodedFrame>();
        var found = new bool[requiredFrames.Length];
        for (var index = 0; index < 64; index++)
        {
            var frame = await ReadFrameAsync(stream, codec, timeout);
            frames.Add(frame);
            for (var required = 0; required < requiredFrames.Length; required++)
                found[required] |= requiredFrames[required](frame);
            if (found.All(static value => value)) return frames;
        }
        throw new InvalidOperationException("Recall visibility-transition frames were incomplete before the frame limit.");
    }

    private static bool IsCreateMobFor(DecodedFrame frame, ushort targetId)
        => frame.Header.Type == CreateMobConfirmation.MessageType && frame.Payload.Length >= 6 &&
           BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[4..]) == targetId;

    private static bool IsPkInfoFor(DecodedFrame frame, ushort targetId)
        => frame.Header.Type == PkInfoConfirmation.MessageType && frame.Header.Id == targetId &&
           frame.Payload.Length == sizeof(int) && BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span) == 0;

    private static bool IsRecallFrame(DecodedFrame frame)
        => ActionRequest.TryParse(frame, out var action) && action is { Effect: 1, Speed: 2 };

    private static async Task<DecodedFrame> ReadFrameAsync(NetworkStream stream, LegacyFrameCodec codec, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        var headerBytes = new byte[PacketHeader.SizeInBytes];
        await stream.ReadExactlyAsync(headerBytes, cancellation.Token);
        var header = PacketHeader.Read(headerBytes);
        Check(header.Size is >= PacketHeader.SizeInBytes and <= PacketHeader.MaxPacketSize,
            $"Invalid recall smoke frame size {header.Size}.");
        var bytes = new byte[header.Size];
        headerBytes.CopyTo(bytes, 0);
        await stream.ReadExactlyAsync(bytes.AsMemory(PacketHeader.SizeInBytes), cancellation.Token);
        var frame = codec.Decode(bytes);
        Check(frame.IsChecksumValid, "Recall smoke received a frame with an invalid checksum.");
        return frame;
    }

    private static (short X, short Y) Spawn(DecodedFrame frame)
        => (BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span),
            BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[2..]));

    private static ushort Id(DecodedFrame frame)
        => BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);

    private static async Task AssertSavedPosition(string path, int slot, short x, short y)
    {
        var bytes = await File.ReadAllBytesAsync(path);
        var offset = 216 + slot * 816 + 40;
        Check(BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset)) == x &&
            BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset + 2)) == y,
            "The recalled authoritative position was not persisted on logout.");
    }

    private static string CString(ReadOnlySpan<byte> bytes)
    {
        var end = bytes.IndexOf((byte)0);
        return Encoding.ASCII.GetString(end < 0 ? bytes : bytes[..end]);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private readonly record struct MoveProgress(short X, short Y, uint NextTick);
}

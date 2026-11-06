using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using WydCdk.Protocol;

internal static class AttackViewGateNetwork
{
    private const int ViewGridSize = 33;

    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var targetFile = options.AutoTradeClosePeerAccountFile
            ?? throw new InvalidOperationException("View-gate smoke requires an isolated target account.");
        var actorBefore = await File.ReadAllBytesAsync(options.AccountFile);
        var targetBefore = await File.ReadAllBytesAsync(targetFile);
        var targetHeader = new AccountHeader(
            CString(targetBefore.AsSpan(0, 16)), CString(targetBefore.AsSpan(16, 12)), targetBefore.AsSpan(202, 6).ToArray());

        await using var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "VIEW_GATE_ACTOR_");
        await using var target = await E2eSession.ConnectAndLoginAsync(
            options with { AccountFile = targetFile }, targetHeader, codec, "VIEW_GATE_TARGET_");

        var actorId = Id(actor.CharacterLoginFrame);
        var targetId = Id(target.CharacterLoginFrame);
        Check(actorId > 0 && targetId > 0 && actorId != targetId,
            "View-gate smoke needs distinct authoritative participants.");
        var actorPosition = Position(actor.CharacterLoginFrame);
        var targetPosition = Position(target.CharacterLoginFrame);
        var offsetX = actorPosition.X <= short.MaxValue - ViewGridSize - 1 ? ViewGridSize + 1 : -(ViewGridSize + 1);
        var destinationX = checked((short)(actorPosition.X + offsetX));
        Check(destinationX is > 0 and < 4096,
            "Actor position cannot host the deterministic out-of-view target coordinate.");

        await MoveToAsync(target.Stream, codec, targetId, targetPosition,
            (destinationX, actorPosition.Y), options.Timeout);
        Check(!IsInView(actorPosition, (destinationX, actorPosition.Y)),
            "Target movement did not cross the inclusive 33-tile view boundary.");

        // Movement can itself remove a character from the attacker's view. Drain that relay before
        // measuring the separate RemoveMob produced by MSG_Attack.
        var movementFrames = await DrainAvailableAsync(actor.Stream, codec, options.Timeout);
        var movementRemovals = movementFrames.Count(frame => frame.Header.Type == RemoveMobConfirmation.MessageType && frame.Header.Id == targetId);

        await AssertOutOfViewAttackAsync(actor, target, actorId, targetId, codec,
            marker: -2, expectedMarker: -2, tick: unchecked((uint)Environment.TickCount64 + 2_000u), options.Timeout);
        await AssertOutOfViewAttackAsync(actor, target, actorId, targetId, codec,
            marker: 75, expectedMarker: 75, tick: unchecked((uint)Environment.TickCount64 + 4_000u), options.Timeout);

        Console.WriteLine($"ATTACK_VIEW_GATE_POSITION={actorPosition.X},{actorPosition.Y}->{destinationX},{actorPosition.Y};movementRemoveCount={movementRemovals}");
        Console.WriteLine("ATTACK_VIEW_GATE_VALID=private-remove-type1;damage-marker-minus2-preserved");
        Console.WriteLine("ATTACK_VIEW_GATE_INVALID=private-remove-type1;damage-marker-75-preserved-before-type77");
        Console.WriteLine("CLIENT_E2E_PASS attack-view-gate-private-remove-marker-preservation");

        await actor.LogoutAsync();
        await target.LogoutAsync();
        await MotionGateNetwork.AssertDisk(options.AccountFile, actorBefore, options.Slot);
        await MotionGateNetwork.AssertDisk(targetFile, targetBefore, options.Slot);
    }

    private static async Task AssertOutOfViewAttackAsync(
        E2eSession actor,
        E2eSession target,
        ushort actorId,
        ushort targetId,
        LegacyFrameCodec codec,
        int marker,
        int expectedMarker,
        uint tick,
        TimeSpan timeout)
    {
        await actor.Stream.WriteAsync(BuildAttack(codec, actorId, targetId, marker, tick));
        var frames = await ReadThroughAttackAsync(actor.Stream, codec, timeout);
        var removals = frames.Where(frame => frame.Header.Type == RemoveMobConfirmation.MessageType).ToArray();
        Check(removals.Length == 1 && removals[0].Header.Id == targetId &&
            BinaryPrimitives.ReadInt32LittleEndian(removals[0].Payload.Span) == 1,
            $"Out-of-view marker {marker} did not queue one private RemoveMob(type 1) for the target.");
        var attacks = frames.Where(IsAttack).ToArray();
        Check(attacks.Length == 1,
            $"Out-of-view marker {marker} did not receive one attack confirmation: count={attacks.Length}.");
        Check(AttackRequest.TryReadDamageSlot(attacks[0], 0, out var relayedTargetId, out var relayedDamage) &&
            relayedTargetId == targetId && relayedDamage == expectedMarker,
            $"Out-of-view marker {marker} changed on the attack wire: target={relayedTargetId}, damage={relayedDamage}.");
        Check(IndexOf(frames, frame => frame.Header.Type == RemoveMobConfirmation.MessageType) < IndexOf(frames, IsAttack),
            "RemoveMob was sent after the aggregate Attack confirmation.");
        await AssertNoIncomingAsync(target.Stream, timeout);
    }

    private static async Task MoveToAsync(
        NetworkStream stream,
        LegacyFrameCodec codec,
        ushort actorId,
        (short X, short Y) start,
        (short X, short Y) destination,
        TimeSpan timeout)
    {
        var current = start;
        var distance = Math.Max(Math.Abs(destination.X - start.X), Math.Abs(destination.Y - start.Y));
        var steps = Math.Max(1, (distance + ViewGridSize - 1) / ViewGridSize);
        for (var step = 1; step <= steps; step++)
        {
            var next = (
                checked((short)(start.X + ((destination.X - start.X) * step / steps))),
                checked((short)(start.Y + ((destination.Y - start.Y) * step / steps))));
            var request = new ActionRequest(current.X, current.Y, 0, 0, new byte[24], next.Item1, next.Item2);
            var tick = unchecked((uint)Environment.TickCount64);
            await stream.WriteAsync(request.ToFrame(codec, tick, 19, actorId));
            var echo = await ReadMatchingActionAsync(stream, codec, actorId, next, timeout);
            ActionGateNetwork.AssertRelay([echo], request, actorId, tick);
            current = next;
        }
    }

    private static async Task<DecodedFrame> ReadMatchingActionAsync(
        NetworkStream stream,
        LegacyFrameCodec codec,
        ushort actorId,
        (short X, short Y) destination,
        TimeSpan timeout)
    {
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var frame = await ReadFrameAsync(stream, codec, timeout);
            if (frame.Header.Id == actorId && IsAction(frame) &&
                ActionRequest.TryParse(frame, out var action) && action is not null &&
                action.TargetX == destination.X && action.TargetY == destination.Y)
                return frame;
        }
        throw new InvalidOperationException($"Target movement echo missing for ({destination.X},{destination.Y}).");
    }

    private static async Task<IReadOnlyList<DecodedFrame>> DrainAvailableAsync(
        NetworkStream stream,
        LegacyFrameCodec codec,
        TimeSpan timeout)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        var frames = new List<DecodedFrame>();
        while (stream.DataAvailable)
            frames.Add(await ReadFrameAsync(stream, codec, timeout));
        return frames;
    }

    private static async Task<IReadOnlyList<DecodedFrame>> ReadThroughAttackAsync(
        NetworkStream stream,
        LegacyFrameCodec codec,
        TimeSpan timeout)
    {
        var frames = new List<DecodedFrame>();
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var frame = await ReadFrameAsync(stream, codec, timeout);
            frames.Add(frame);
            if (IsAttack(frame))
                return frames;
        }
        throw new InvalidOperationException("Out-of-view attack confirmation missing.");
    }

    private static async Task<DecodedFrame> ReadFrameAsync(NetworkStream stream, LegacyFrameCodec codec, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        var headerBytes = new byte[PacketHeader.SizeInBytes];
        await stream.ReadExactlyAsync(headerBytes, cancellation.Token);
        var header = PacketHeader.Read(headerBytes);
        Check(header.Size is >= PacketHeader.SizeInBytes and <= PacketHeader.MaxPacketSize,
            "Received frame has an invalid size.");
        var bytes = new byte[header.Size];
        headerBytes.CopyTo(bytes, 0);
        await stream.ReadExactlyAsync(bytes.AsMemory(PacketHeader.SizeInBytes), cancellation.Token);
        var frame = codec.Decode(bytes);
        Check(frame.IsChecksumValid, "Received frame checksum is invalid.");
        return frame;
    }

    private static async Task AssertNoIncomingAsync(NetworkStream stream, TimeSpan timeout)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(150, timeout.TotalMilliseconds)));
        Check(!stream.DataAvailable, "Out-of-view attack leaked a frame to its target.");
    }

    private static byte[] BuildAttack(LegacyFrameCodec codec, ushort actorId, ushort targetId, int marker, uint tick)
    {
        var payload = new byte[AttackRequest.OneTargetPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), actorId);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), -1);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(AttackRequest.DamageArrayOffset), targetId);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(AttackRequest.DamageArrayOffset + 4), marker);
        return codec.Encode(AttackRequest.OneTargetMessageType, actorId, tick, payload, 42);
    }

    private static bool IsInView((short X, short Y) origin, (short X, short Y) target) =>
        Math.Abs(target.X - origin.X) <= ViewGridSize && Math.Abs(target.Y - origin.Y) <= ViewGridSize;

    private static bool IsAction(DecodedFrame frame) => frame.Header.Type == ActionRequest.MessageType;

    private static bool IsAttack(DecodedFrame frame) => frame.Header.Type is
        AttackRequest.AreaMessageType or AttackRequest.OneTargetMessageType or AttackRequest.TwoTargetMessageType;

    private static int IndexOf(IReadOnlyList<DecodedFrame> frames, Func<DecodedFrame, bool> predicate)
    {
        for (var index = 0; index < frames.Count; index++)
            if (predicate(frames[index]))
                return index;
        return -1;
    }

    private static ushort Id(DecodedFrame frame) => BinaryPrimitives.ReadUInt16LittleEndian(
        frame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);

    private static (short X, short Y) Position(DecodedFrame frame) => (
        BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span),
        BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[2..]));

    private static string CString(ReadOnlySpan<byte> bytes)
    {
        var terminator = bytes.IndexOf((byte)0);
        return Encoding.ASCII.GetString(terminator < 0 ? bytes : bytes[..terminator]);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using WydCdk.Protocol;

internal static class AttackEmptyTargetNetwork
{
    private const ushort EmptyTargetId = 24_999;
    private const ushort FirstInvalidTargetId = 25_000;

    public static async Task RunAsync(E2eOptions options, LegacyFrameCodec codec)
    {
        var targetFile = options.AutoTradeClosePeerAccountFile
            ?? throw new InvalidOperationException("Empty-target smoke requires a second isolated account.");
        var actorBytes = await File.ReadAllBytesAsync(options.AccountFile);
        var targetBytes = await File.ReadAllBytesAsync(targetFile);
        var actorHeader = ReadHeader(actorBytes);
        var targetHeader = ReadHeader(targetBytes);

        await using var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "EMPTY_ATTACK_ACTOR_");
        var targetOptions = options with
        {
            AccountFile = targetFile,
            ConnectionId = checked((ushort)(options.ConnectionId + 1)),
        };
        await using var target = await E2eSession.ConnectAndLoginAsync(targetOptions, targetHeader, codec, "EMPTY_ATTACK_TARGET_");

        var actorId = MobId(actor.CharacterLoginFrame);
        var targetId = MobId(target.CharacterLoginFrame);
        try
        {
            Check(actorId > 0 && targetId > 0 && actorId != targetId, "The smoke needs two distinct active participants.");

            await actor.Stream.WriteAsync(BuildTwoTargetAttack(
                codec,
                actorId,
                EmptyTargetId,
                declaredDamage: 75,
                targetId,
                targetDamage: -2,
                unchecked((uint)Environment.TickCount64 + 2_000u)));
            var batchFrames = await ReadThroughAttackAsync(actor.Stream, codec, options.Timeout);
            var removeIndex = batchFrames.FindIndex(frame => frame.Header.Type == RemoveMobConfirmation.MessageType && frame.Header.Id == EmptyTargetId);
            var attackIndex = batchFrames.FindIndex(IsAttack);
            Check(removeIndex >= 0 && attackIndex > removeIndex,
                "A valid empty target did not produce a private RemoveMob before the aggregate Attack.");
            var emptyRemove = batchFrames[removeIndex];
            Check(BinaryPrimitives.ReadInt32LittleEndian(emptyRemove.Payload.Span) == 1,
                "The empty target did not use RemoveMob type 1.");
            var batchAttack = batchFrames[attackIndex];
            Check(AttackRequest.TryReadDamageSlot(batchAttack, 0, out var emptyTarget, out var emptyDamage) &&
                emptyTarget == EmptyTargetId && emptyDamage == 75,
                $"The empty-target gate rewrote its incoming slot: target={emptyTarget}, damage={emptyDamage}.");
            Check(AttackRequest.TryReadDamageSlot(batchAttack, 1, out var activeTarget, out var activeDamage) &&
                activeTarget == targetId && activeDamage > 0,
                $"The slot after an empty target did not resolve normally: target={activeTarget}, damage={activeDamage}.");

            await actor.Stream.WriteAsync(BuildOneTargetAttack(
                codec,
                actorId,
                FirstInvalidTargetId,
                declaredDamage: 75,
                unchecked((uint)Environment.TickCount64 + 4_000u)));
            var invalidIndexFrames = await ReadThroughAttackAsync(actor.Stream, codec, options.Timeout);
            Check(!invalidIndexFrames.Any(frame => frame.Header.Type == RemoveMobConfirmation.MessageType && frame.Header.Id == FirstInvalidTargetId),
                "An out-of-range MOB index incorrectly produced RemoveMob.");
            var invalidIndexAttack = invalidIndexFrames.Single(IsAttack);
            Check(AttackRequest.TryReadDamageSlot(invalidIndexAttack, 0, out var invalidTarget, out var invalidDamage) &&
                invalidTarget == FirstInvalidTargetId && invalidDamage == 75,
                $"The early invalid-index gate rewrote its wire slot: target={invalidTarget}, damage={invalidDamage}.");

            Console.WriteLine("ATTACK_EMPTY_TARGET=private-remove-type1;slot-24999-damage75-preserved;next-slot-damaged");
            Console.WriteLine("ATTACK_INVALID_TARGET_INDEX=25000;slot-preserved;no-remove");
            Console.WriteLine("CLIENT_E2E_PASS attack-empty-target-remove-and-index-gates");
        }
        finally
        {
            try { await actor.LogoutAsync(); }
            finally { await target.LogoutAsync(); }
        }

        await MotionGateNetwork.AssertDisk(options.AccountFile, actorBytes, options.Slot);
        await MotionGateNetwork.AssertDisk(targetFile, targetBytes, options.Slot);
    }

    private static byte[] BuildTwoTargetAttack(
        LegacyFrameCodec codec,
        ushort actorId,
        ushort firstTargetId,
        int declaredDamage,
        ushort secondTargetId,
        int targetDamage,
        uint tick)
    {
        var payload = new byte[AttackRequest.TwoTargetPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), actorId);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), -1);
        SetSlot(payload, 0, firstTargetId, declaredDamage);
        SetSlot(payload, 1, secondTargetId, targetDamage);
        return codec.Encode(AttackRequest.TwoTargetMessageType, actorId, tick, payload, 42);
    }

    private static byte[] BuildOneTargetAttack(
        LegacyFrameCodec codec,
        ushort actorId,
        ushort targetId,
        int declaredDamage,
        uint tick)
    {
        var payload = new byte[AttackRequest.OneTargetPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), actorId);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), -1);
        SetSlot(payload, 0, targetId, declaredDamage);
        return codec.Encode(AttackRequest.OneTargetMessageType, actorId, tick, payload, 42);
    }

    private static void SetSlot(Span<byte> payload, int slot, ushort targetId, int damage)
    {
        var offset = AttackRequest.DamageArrayOffset + slot * AttackRequest.DamageSlotSize;
        BinaryPrimitives.WriteUInt16LittleEndian(payload[offset..], targetId);
        BinaryPrimitives.WriteInt32LittleEndian(payload[(offset + 4)..], damage);
    }

    private static async Task<List<DecodedFrame>> ReadThroughAttackAsync(NetworkStream stream, LegacyFrameCodec codec, TimeSpan timeout)
    {
        var frames = new List<DecodedFrame>();
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var frame = await ReadFrameAsync(stream, codec, timeout);
            frames.Add(frame);
            if (IsAttack(frame))
                return frames;
        }
        throw new InvalidOperationException("Attack confirmation was not received.");
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

    private static AccountHeader ReadHeader(ReadOnlySpan<byte> account)
    {
        static string CString(ReadOnlySpan<byte> bytes)
        {
            var terminator = bytes.IndexOf((byte)0);
            return Encoding.ASCII.GetString(terminator < 0 ? bytes : bytes[..terminator]);
        }
        return new AccountHeader(CString(account[..16]), CString(account.Slice(16, 12)), account.Slice(202, 6).ToArray());
    }

    private static ushort MobId(DecodedFrame frame) => BinaryPrimitives.ReadUInt16LittleEndian(
        frame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);

    private static bool IsAttack(DecodedFrame frame) => frame.Header.Type is
        AttackRequest.AreaMessageType or AttackRequest.OneTargetMessageType or AttackRequest.TwoTargetMessageType;

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

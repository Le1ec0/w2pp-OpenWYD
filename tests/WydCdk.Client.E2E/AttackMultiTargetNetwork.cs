using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;

internal static class AttackMultiTargetNetwork
{
    private const ushort InputProgress = 321;

    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AutoTradeClosePeerAccountFile
            ?? throw new InvalidOperationException("Multi-target Attack smoke requires an isolated peer account.");
        var peerBytes = await File.ReadAllBytesAsync(peerFile);
        var peerHeader = new AccountHeader(
            CString(peerBytes.AsSpan(0, 16)),
            CString(peerBytes.AsSpan(16, 12)),
            peerBytes.AsSpan(202, 6).ToArray());

        await using var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "MULTI_TARGET_ACTOR_");
        await using var peer = await E2eSession.ConnectAndLoginAsync(
            options with { AccountFile = peerFile }, peerHeader, codec, "MULTI_TARGET_PEER_");

        var actorId = Id(actor.CharacterLoginFrame);
        var peerId = Id(peer.CharacterLoginFrame);
        Check(actorId != 0 && peerId != 0 && actorId != peerId,
            "Multi-target smoke requires two distinct authoritative participants.");
        Check(Hp(actor.CharacterLoginFrame) > 0 && Hp(peer.CharacterLoginFrame) >= 100_000,
            "Multi-target fixtures must be alive and the peer must have at least 100000 HP.");

        var currentTick = unchecked((uint)Environment.TickCount64);
        Check(currentTick > 120_001 && currentTick < uint.MaxValue - 40_000u,
            "Local monotonic clock cannot host the multi-target smoke tick.");
        var attack = BuildAttack(codec, actorId, peerId, InputProgress, currentTick - 60_000u);
        await actor.Stream.WriteAsync(attack);

        var captured = await AutoTradeCloseNetwork.CapturePhase(
            actor.Stream,
            peer.Stream,
            actorId,
            peerId,
            codec,
            options.Timeout,
            "MULTI-TARGET",
            peerFirst: false);
        var actorAttacks = captured.Actor.Where(IsAttack).ToArray();
        var peerAttacks = captured.Peer.Where(IsAttack).ToArray();
        Check(actorAttacks.Length == 1 && peerAttacks.Length == 1,
            $"One attack action must produce one response per participant: actor={actorAttacks.Length}, peer={peerAttacks.Length}.");

        var actorAttack = actorAttacks[0];
        var peerAttack = peerAttacks[0];
        Check(actorAttack.Header.Type == AttackRequest.AreaMessageType && actorAttack.Header.Size == AttackRequest.AreaPacketSize,
            "The multi-target response changed the 13-slot MSG_Attack wire type or size.");
        var currentMp = BinaryPrimitives.ReadInt32LittleEndian(actorAttack.Payload.Span[AttackConfirmation.CurrentMpOffset..]);
        var reqMp = BinaryPrimitives.ReadInt16LittleEndian(actorAttack.Payload.Span[AttackConfirmation.RequestedMpOffset..]);
        Check(reqMp == unchecked((short)currentMp), "Area attack relay did not narrow authoritative CurrentMp into ReqMp.");
        Check(actorAttack.Payload.Span.SequenceEqual(peerAttack.Payload.Span),
            "Attacker and peer received different aggregate attack payloads.");
        Check(BinaryPrimitives.ReadUInt16LittleEndian(actorAttack.Payload.Span[AttackRequest.ProgressOffset..]) == InputProgress,
            "Skipped slot 0 must preserve Progress without advancing it.");
        Check(actorAttack.Payload.Span[36] == 0,
            "Skipped slot 0 must not echo client-controlled DoubleCritical=0xFF.");
        Check(ReadSlot(actorAttack, 0) == (0, 0), "The empty physical slot 0 was unexpectedly modified.");
        var slot1 = ReadSlot(actorAttack, 1);
        var slot2 = ReadSlot(actorAttack, 2);
        Check(slot1.TargetId == peerId && slot1.Damage > 0,
            $"Physical wire slot 1 was not authoritatively resolved: target={slot1.TargetId}, damage={slot1.Damage}.");
        Check(slot2.TargetId == peerId && slot2.Damage > 0,
            $"Physical wire slot 2 was not authoritatively resolved: target={slot2.TargetId}, damage={slot2.Damage}.");

        Console.WriteLine($"ATTACK_MULTI_TARGET_ACTOR_ID={actorId}");
        Console.WriteLine($"ATTACK_MULTI_TARGET_PEER_ID={peerId}");
        Console.WriteLine("CLIENT_E2E_PASS attack-multi-target-13-slot-indexed-authoritative-relay");
        await actor.LogoutAsync();
        await peer.LogoutAsync();
    }

    private static byte[] BuildAttack(LegacyFrameCodec codec, ushort actorId, ushort peerId, ushort progress, uint tick)
    {
        var payload = new byte[AttackRequest.AreaPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), actorId);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(AttackRequest.ProgressOffset), progress);
        payload[36] = 0xFF;
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), -1);
        SetPhysicalSlot(payload, 1, peerId);
        SetPhysicalSlot(payload, 2, peerId);

        var bytes = codec.Encode(AttackRequest.AreaMessageType, actorId, tick, payload, 42);
        var frame = codec.Decode(bytes);
        Check(AttackRequest.TryParse(frame, out var parsed) && parsed is { MessageType: AttackRequest.AreaMessageType, DoubleCritical: 0xFF } &&
            parsed.Progress == progress && AttackRequest.GetTargetCount(frame.Header.Type) == 13 &&
            ReadSlot(frame, 0) == (0, 0) && ReadSlot(frame, 1) == (peerId, -2) && ReadSlot(frame, 2) == (peerId, -2),
            "Smoke did not build the intended checksum-valid 13-slot action with physical indexes 1 and 2 only.");
        return bytes;
    }

    private static void SetPhysicalSlot(Span<byte> payload, int index, ushort targetId)
    {
        var offset = AttackRequest.DamageArrayOffset + (index * AttackRequest.DamageSlotSize);
        BinaryPrimitives.WriteUInt16LittleEndian(payload[offset..], targetId);
        BinaryPrimitives.WriteInt32LittleEndian(payload[(offset + 4)..], -2);
    }

    private static (ushort TargetId, int Damage) ReadSlot(DecodedFrame frame, int index)
    {
        Check(AttackRequest.TryReadDamageSlot(frame, index, out var targetId, out var damage),
            $"Could not read attack wire slot {index}.");
        return (targetId, damage);
    }

    private static bool IsAttack(DecodedFrame frame) => frame.Header.Type is
        AttackRequest.AreaMessageType or AttackRequest.OneTargetMessageType or AttackRequest.TwoTargetMessageType;

    private static int Hp(DecodedFrame frame) => BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span.Slice(
        CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + 24, sizeof(int)));

    private static ushort Id(DecodedFrame frame) => BinaryPrimitives.ReadUInt16LittleEndian(
        frame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);

    private static string CString(ReadOnlySpan<byte> bytes)
    {
        var end = bytes.IndexOf((byte)0);
        return Encoding.ASCII.GetString(end < 0 ? bytes : bytes[..end]);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

using System.Buffers.Binary;
using System.Text;
using System.Net.Sockets;
using WydCdk.Protocol;

internal static class AttackSkillSummonNoticeNetwork
{
    private const short TeleportSkillId = 42;
    private const string ExpectedMessage = "Não está disponível nesta área.";

    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AutoTradeClosePeerAccountFile!;
        var actorBefore = await File.ReadAllBytesAsync(options.AccountFile);
        var peerBefore = await File.ReadAllBytesAsync(peerFile);
        var peerHeader = new AccountHeader(
            CString(peerBefore.AsSpan(0, 16)),
            CString(peerBefore.AsSpan(16, 12)),
            peerBefore.AsSpan(202, 6).ToArray());

        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "ATTACK_SUMMON_NOTICE_ACTOR_"))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "ATTACK_SUMMON_NOTICE_PEER_"))
        {
            var actorId = Id(actor.CharacterLoginFrame);
            var peerId = Id(peer.CharacterLoginFrame);
            Check(actorId > 0 && peerId > 0 && actorId != peerId, "Summon notice fixture needs distinct authoritative IDs.");
            Check(actor.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.MobOffset + 20] == 1,
                "Summon notice actor must be a Foema for skill 42.");
            var actorGuild = BinaryPrimitives.ReadUInt16LittleEndian(
                actor.CharacterLoginFrame.Payload.Span[(CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.GuildOffset)..]);
            var peerGuild = BinaryPrimitives.ReadUInt16LittleEndian(
                peer.CharacterLoginFrame.Payload.Span[(CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.GuildOffset)..]);
            Check(actorGuild == 77 && peerGuild == actorGuild,
                "Summon notice actor and peer must share the synthetic Guild so skill 42 passes bParty.");
            var learnedSkills = BinaryPrimitives.ReadUInt32LittleEndian(
                actor.CharacterLoginFrame.Payload.Span[(CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.LearnedSkillOffset)..]);
            Check((learnedSkills & (1u << 18)) != 0, "Summon notice actor must have learned skill 42.");

            var actorStart = Position(actor.CharacterLoginFrame);
            var peerStart = Position(peer.CharacterLoginFrame);
            await MoveToPositionAsync(actor.Stream, codec, actorId, actorStart, 2_100, 2_170, options.Timeout);
            await MoveToPositionAsync(peer.Stream, codec, peerId, peerStart, 2_105, 2_170, options.Timeout);
            await Phase("ATTACK-SUMMON-NOTICE-POSITIONED");
            await Phase("ATTACK-SUMMON-NOTICE-READY");

            var tick = unchecked((uint)Environment.TickCount64);
            Check(tick > 50_000u, "Local monotonic clock cannot host the summon notice smoke.");
            await actor.Stream.WriteAsync(BuildTeleport(codec, actorId, peerId, tick - 50_000u));
            var denied = await Phase("ATTACK-SUMMON-NOTICE-DENIED");

            var notices = denied.Actor.Where(frame => frame.Header.Type == MessagePanelConfirmation.MessageType).ToArray();
            var expectedPayload = new byte[MessagePanelConfirmation.MessageLength];
            Encoding.Latin1.GetBytes(ExpectedMessage).CopyTo(expectedPayload, 0);
            Check(notices.Length == 1 && notices[0].IsChecksumValid &&
                  notices[0].Header.Size == MessagePanelConfirmation.PacketSize &&
                  notices[0].Header.Id == 0 && notices[0].Header.ClientTick == 0 &&
                  notices[0].Header.KeywordIndex == 0 &&
                  notices[0].Payload.Span.SequenceEqual(expectedPayload),
                "Blocked InstanceType 9 target did not receive the exact private 7.69 MessagePanel notice.");
            Check(!denied.Peer.Any(frame => frame.Header.Type == MessagePanelConfirmation.MessageType),
                "Blocked InstanceType 9 notice leaked to the observer.");
            Check(!denied.Actor.Concat(denied.Peer).Any(IsAttack),
                "A terminal InstanceType 9 zone rejection still relayed the aggregate Attack frame.");
            Check(!denied.Actor.Concat(denied.Peer).Any(frame => frame.Header.Type == SetHpMpConfirmation.MessageType),
                "A blocked InstanceType 9 target changed HP or MP.");

            await actor.LogoutAsync();
            await peer.LogoutAsync();

            Task<(IReadOnlyList<DecodedFrame> Actor, IReadOnlyList<DecodedFrame> Peer)> Phase(string label)
                => AutoTradeCloseNetwork.CapturePhase(actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, label, peerFirst: false);
        }

        await MotionGateNetwork.AssertDisk(options.AccountFile, actorBefore, options.Slot);
        await MotionGateNetwork.AssertDisk(peerFile, peerBefore, options.Slot);
        Console.WriteLine("CLIENT_E2E_PASS attack-summon-private-zone-notice-terminal-return-no-relay");
    }

    private static async Task MoveToPositionAsync(
        NetworkStream stream,
        LegacyFrameCodec codec,
        ushort id,
        (short X, short Y) start,
        short targetX,
        short targetY,
        TimeSpan timeout)
    {
        var steps = Math.Max(1, (Math.Max(Math.Abs(targetX - start.X), Math.Abs(targetY - start.Y)) + 32) / 33);
        var currentX = start.X;
        var currentY = start.Y;
        ActionRequest? last = null;
        for (var step = 1; step <= steps; step++)
        {
            var nextX = checked((short)(start.X + ((targetX - start.X) * step / steps)));
            var nextY = checked((short)(start.Y + ((targetY - start.Y) * step / steps)));
            last = new ActionRequest(currentX, currentY, 0, 0, new byte[24], nextX, nextY);
            await stream.WriteAsync(last.ToFrame(codec, unchecked((uint)Environment.TickCount64), 23, id));
            currentX = nextX;
            currentY = nextY;
        }

        if (last is null)
            throw new InvalidOperationException("Summon notice movement did not emit any packets.");

        using var cancellation = new CancellationTokenSource(timeout);
        for (var index = 0; index < 128; index++)
        {
            var header = new byte[PacketHeader.SizeInBytes];
            await stream.ReadExactlyAsync(header, cancellation.Token);
            var size = PacketHeader.Read(header).Size;
            Check(size >= PacketHeader.SizeInBytes && size <= PacketHeader.MaxPacketSize,
                "Summon notice movement echo has an invalid frame size.");
            var bytes = new byte[size];
            header.CopyTo(bytes, 0);
            await stream.ReadExactlyAsync(bytes.AsMemory(PacketHeader.SizeInBytes), cancellation.Token);
            var frame = codec.Decode(bytes);
            Check(frame.IsChecksumValid, "Summon notice movement echo checksum is invalid.");
            if (frame.Header.Id != id || !IsAction(frame) || !ActionRequest.TryParse(frame, out var echoed) || echoed is null)
                continue;
            if (echoed.TargetX == last.TargetX && echoed.TargetY == last.TargetY)
                return;
        }

        throw new InvalidOperationException($"Summon notice movement did not reach ({targetX},{targetY}).");
    }

    private static byte[] BuildTeleport(LegacyFrameCodec codec, ushort actorId, ushort targetId, uint tick)
    {
        var payload = new byte[AttackRequest.OneTargetPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), actorId);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), TeleportSkillId);
        var slot = AttackRequest.DamageArrayOffset;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(slot), targetId);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(slot + 4), -1);
        var bytes = codec.Encode(AttackRequest.OneTargetMessageType, actorId, tick, payload, 43);
        var frame = codec.Decode(bytes);
        Check(AttackRequest.TryParse(frame, out var attack) && attack is not null &&
              attack.SkillIndex == TeleportSkillId &&
              AttackRequest.TryReadDamageSlot(frame, 0, out var parsedTargetId, out var parsedDamage) &&
              parsedTargetId == targetId && parsedDamage == -1,
            "Summon notice smoke did not create a valid one-target Type 9 Attack.");
        return bytes;
    }

    private static bool IsAttack(DecodedFrame frame) => frame.Header.Type is
        AttackRequest.AreaMessageType or AttackRequest.OneTargetMessageType or AttackRequest.TwoTargetMessageType;

    private static bool IsAction(DecodedFrame frame) => frame.Header.Type is
        ActionRequest.MessageType or ActionRequest.StopMessageType or ActionRequest.IllusionMessageType;

    private static (short X, short Y) Position(DecodedFrame frame) =>
        (BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span),
         BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[2..]));

    private static ushort Id(DecodedFrame frame) => BinaryPrimitives.ReadUInt16LittleEndian(
        frame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);

    private static string CString(ReadOnlySpan<byte> bytes)
    {
        var end = bytes.IndexOf((byte)0);
        return Encoding.ASCII.GetString(end < 0 ? bytes : bytes[..end]);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

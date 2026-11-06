using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using WydCdk.Protocol;

internal static class AttackTransformationNetwork
{
    private const ushort UpdateEquipType = EquipmentAppearanceV769.MessageType;
    private const ushort UpdateScoreType = UpdateScoreConfirmationV769.MessageType;
    private const int TransformationSkillId = 64;
    private const ushort TransformationHeadIndex = 22;
    private const ushort TransformationVisualCode = 0x9016; // head 22 with the 7.69 sanctuary visual tier 9

    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        if (!IPAddress.TryParse(options.Host, out var hostAddress) || !IPAddress.IsLoopback(hostAddress) || options.Port != 18283)
            throw new ArgumentException("Transformation smoke requires an isolated server on loopback port 18283.");

        var accountDirectory = Path.GetDirectoryName(options.AccountFile)
            ?? throw new ArgumentException("Transformation actor account must have a parent directory.");
        var peerFile = Path.Combine(accountDirectory, "SCOREOBS");
        if (!File.Exists(peerFile))
            throw new FileNotFoundException("Transformation smoke observer fixture is missing.", peerFile);

        var fixturePath = Path.GetFullPath(options.AccountFile);
        if (!fixturePath.Contains(Path.Combine("Backup", "Build", "AttackTransformationSkills-20261013", "fixture"), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Transformation smoke is restricted to its disposable fixture under Backup/Build/AttackTransformationSkills-20261013.");

        var observerBefore = await File.ReadAllBytesAsync(peerFile);
        var observerHeader = ReadAccountHeader(observerBefore);
        var observerOptions = options with
        {
            AccountFile = peerFile,
            ConnectionId = checked((ushort)(options.ConnectionId + 1)),
        };

        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "TRANSFORM_ACTOR_"))
        await using (var observer = await E2eSession.ConnectAndLoginAsync(observerOptions, observerHeader, codec, "TRANSFORM_OBSERVER_"))
        {
            var actorId = Id(actor.CharacterLoginFrame);
            var observerId = Id(observer.CharacterLoginFrame);
            Ensure(actorId > 0 && observerId > 0 && actorId != observerId,
                "Transformation smoke requires distinct authoritative participant IDs.");

            var actorMob = actor.CharacterLoginFrame.Payload.Span.Slice(
                CharacterLoginConfirmationV769.MobOffset,
                CharacterMobV769.SizeInBytes);
            Ensure(actorMob[20] == 2 &&
                BinaryPrimitives.ReadUInt32LittleEndian(actorMob[CharacterMobV769.LearnedSkillOffset..]) == (1u << 16),
                "Actor fixture must be a BeastMaster with skill 64 learned.");

            var (actorX, actorY) = Position(actor.CharacterLoginFrame);
            var (observerX, observerY) = Position(observer.CharacterLoginFrame);
            Ensure(Math.Max(Math.Abs(actorX - observerX), Math.Abs(actorY - observerY)) <= 16,
                $"Transformation observer is outside the actor's 33x33 view: actor=({actorX},{actorY}), observer=({observerX},{observerY}).");

            _ = await AutoTradeCloseNetwork.CapturePhase(
                actor.Stream,
                observer.Stream,
                actorId,
                observerId,
                codec,
                options.Timeout,
                "TRANSFORMATION-READY",
                peerFirst: false);

            var tick = unchecked((uint)Environment.TickCount64);
            Ensure(tick > 120_001 && tick < uint.MaxValue - 20_000u,
                "Local monotonic clock cannot host this smoke's tick sequence.");
            await actor.Stream.WriteAsync(BuildTransformationAttack(
                codec, actorId, actorX, actorY, tick));

            var captured = await AutoTradeCloseNetwork.CapturePhase(
                actor.Stream,
                observer.Stream,
                actorId,
                observerId,
                codec,
                options.Timeout,
                "TRANSFORMATION",
                peerFirst: false);
            AssertRelayedProjection(captured.Actor, "actor", actorId);
            AssertRelayedProjection(captured.Peer, "observer", actorId);

            await actor.LogoutAsync();
            await observer.LogoutAsync();
        }

        var actorAfterLogout = await File.ReadAllBytesAsync(options.AccountFile);
        var persistedHead = BinaryPrimitives.ReadInt16LittleEndian(actorAfterLogout.AsSpan(216 + 140));
        var persistedAffect = actorAfterLogout.AsSpan(4_572, 8);
        Ensure(persistedHead == TransformationHeadIndex && persistedAffect[0] == 16 && persistedAffect[1] == 1 &&
            BinaryPrimitives.ReadUInt32LittleEndian(persistedAffect[4..]) > 0,
            $"Logout did not persist skill 64's Type 16 affect with its transformed head: head={persistedHead}, affectType={persistedAffect[0]}, value={persistedAffect[1]}.");

        await using (var relogin = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "TRANSFORM_RELOGIN_"))
        {
            var actorId = Id(relogin.CharacterLoginFrame);
            var reloginMob = relogin.CharacterLoginFrame.Payload.Span.Slice(
                CharacterLoginConfirmationV769.MobOffset,
                CharacterMobV769.SizeInBytes);
            Ensure(BinaryPrimitives.ReadInt16LittleEndian(reloginMob[CharacterMobV769.EquipmentOffset..]) == TransformationHeadIndex,
                "Relogin did not restore the transformed head item in the character MOB.");

            DecodedFrame? reloginScore = null;
            for (var index = 0; index < 16; index++)
            {
                var frame = await ReadFrameAsync(relogin.Stream, codec, options.Timeout);
                if (frame.Header.Type == UpdateScoreType && frame.Header.Id == actorId)
                {
                    reloginScore = frame;
                    break;
                }
            }
            Ensure(reloginScore is not null, "Relogin did not send UpdateScore for the transformation affect.");
            var restoredScore = reloginScore!.Value;
            Ensure(restoredScore.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                (BinaryPrimitives.ReadUInt16LittleEndian(restoredScore.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 16,
                "Relogin UpdateScore did not restore the Type 16 transformation affect.");
            await relogin.LogoutAsync();
        }

        Console.WriteLine("CLIENT_E2E_PASS attack-transformation-skill64-type16-score-equip-view-relay-logout-relogin");

        static void AssertRelayedProjection(IReadOnlyList<DecodedFrame> frames, string recipient, ushort actorId)
        {
            var scores = frames.Where(frame => frame.Header.Type == UpdateScoreType).ToArray();
            var equipment = frames.Where(frame => frame.Header.Type == UpdateEquipType).ToArray();
            Ensure(scores.Length == 1 && scores[0].Header.Id == actorId &&
                scores[0].Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                BinaryPrimitives.ReadUInt16LittleEndian(scores[0].Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8 == 16,
                $"{recipient} did not receive exactly one valid Type 16 UpdateScore for the transformed actor.");
            Ensure(equipment.Length == 1 && equipment[0].Header.Id == actorId &&
                equipment[0].Header.Size == EquipmentAppearanceV769.PacketSize &&
                BinaryPrimitives.ReadUInt16LittleEndian(equipment[0].Payload.Span) == TransformationVisualCode,
                $"{recipient} did not receive exactly one 7.69 UpdateEquip visual 0x{TransformationVisualCode:X4} for head {TransformationHeadIndex}: " +
                $"expectedId={actorId}, count={equipment.Length}, actual={string.Join(";", equipment.Select(frame => $"id={frame.Header.Id}/size={frame.Header.Size}/head={BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span)}"))}.");
        }
    }

    private static byte[] BuildTransformationAttack(
        LegacyFrameCodec codec,
        ushort actorId,
        short actorX,
        short actorY,
        uint tick)
    {
        var payload = new byte[AttackRequest.OneTargetPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(22), unchecked((ushort)actorX));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(24), unchecked((ushort)actorY));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(26), unchecked((ushort)actorX));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(28), unchecked((ushort)actorY));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), actorId);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), TransformationSkillId);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(48), actorId);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(52), -1);
        return codec.Encode(AttackRequest.OneTargetMessageType, actorId, tick, payload, 42);
    }

    private static AccountHeader ReadAccountHeader(ReadOnlySpan<byte> file)
    {
        if (file.Length < 208)
            throw new InvalidDataException("Transformation observer fixture has an invalid account length.");
        return new AccountHeader(
            CString(file[..16]),
            CString(file.Slice(16, 12)),
            file.Slice(202, 6).ToArray());
    }

    private static (short X, short Y) Position(DecodedFrame login) =>
        (BinaryPrimitives.ReadInt16LittleEndian(login.Payload.Span),
         BinaryPrimitives.ReadInt16LittleEndian(login.Payload.Span[2..]));

    private static ushort Id(DecodedFrame login) =>
        BinaryPrimitives.ReadUInt16LittleEndian(login.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);

    private static async Task<DecodedFrame> ReadFrameAsync(NetworkStream stream, LegacyFrameCodec codec, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        var headerBytes = new byte[PacketHeader.SizeInBytes];
        await stream.ReadExactlyAsync(headerBytes, cancellation.Token);
        var header = PacketHeader.Read(headerBytes);
        if (header.Size < PacketHeader.SizeInBytes || header.Size > PacketHeader.MaxPacketSize)
            throw new InvalidDataException($"Transformation response has an invalid frame size {header.Size}.");

        var bytes = new byte[header.Size];
        headerBytes.CopyTo(bytes, 0);
        await stream.ReadExactlyAsync(bytes.AsMemory(PacketHeader.SizeInBytes), cancellation.Token);
        var frame = codec.Decode(bytes);
        Ensure(frame.IsChecksumValid, "Transformation response has an invalid checksum.");
        return frame;
    }

    private static string CString(ReadOnlySpan<byte> source)
    {
        var end = source.IndexOf((byte)0);
        return Encoding.ASCII.GetString(end < 0 ? source : source[..end]);
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

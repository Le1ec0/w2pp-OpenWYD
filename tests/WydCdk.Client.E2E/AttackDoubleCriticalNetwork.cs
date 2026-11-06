using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;

internal static class AttackDoubleCriticalNetwork
{
    private const int Attempts = 24;
    private const int TotalAttacks = Attempts + 1;
    private const int LearnedDoubleCritical = 0x200000;

    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AutoTradeClosePeerAccountFile ?? throw new InvalidOperationException("DoubleCritical smoke requires an isolated peer account.");
        var peerBytes = await File.ReadAllBytesAsync(peerFile);
        var peerHeader = new AccountHeader(CString(peerBytes.AsSpan(0, 16)), CString(peerBytes.AsSpan(16, 12)), peerBytes.AsSpan(202, 6).ToArray());

        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "DOUBLE_CRITICAL_ACTOR_"))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "DOUBLE_CRITICAL_PEER_"))
        {
            var actorId = Id(actor.CharacterLoginFrame);
            var peerId = Id(peer.CharacterLoginFrame);
            Check(actorId > 0 && peerId > 0 && actorId != peerId, "DoubleCritical smoke needs distinct authoritative participant IDs.");
            Check(Hp(actor.CharacterLoginFrame) > 0 && Hp(peer.CharacterLoginFrame) >= 100_000, "DoubleCritical fixtures must be alive with a high-HP target.");
            Check(actor.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.MobOffset + 20] == 3, "DoubleCritical actor fixture did not log in as class 3.");
            Check((BinaryPrimitives.ReadUInt32LittleEndian(actor.CharacterLoginFrame.Payload.Span[(CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.LearnedSkillOffset)..]) & LearnedDoubleCritical) != 0,
                "DoubleCritical actor fixture did not retain learned bit 0x200000.");
            Console.WriteLine($"ATTACK_DOUBLE_CRITICAL_ACTOR_ID={actorId}");
            Console.WriteLine($"ATTACK_DOUBLE_CRITICAL_PEER_ID={peerId}");

            var currentTick = unchecked((uint)Environment.TickCount64);
            Check(currentTick > 120_001 && currentTick < uint.MaxValue - 40_000u, "Local monotonic clock cannot host the DoubleCritical smoke tick sequence.");
            var tick = currentTick - 60_000u;
            for (var index = 0; index < TotalAttacks; index++)
            {
                var progress = index == Attempts ? (ushort)1025 : (ushort)(6 + index);
                await actor.Stream.WriteAsync(BuildAttack(codec, actorId, peerId, progress, tick += 1000u, (byte)(40 + index)));
            }

            var relayed = await AutoTradeCloseNetwork.CapturePhase(
                actor.Stream,
                peer.Stream,
                actorId,
                peerId,
                codec,
                options.Timeout,
                "DOUBLE-CRITICAL",
                peerFirst: false);
            var actorAttacks = relayed.Actor.Where(IsAttack).ToArray();
            var peerAttacks = relayed.Peer.Where(IsAttack).ToArray();
            Check(actorAttacks.Length == TotalAttacks && peerAttacks.Length == TotalAttacks,
                $"Two-target physical attacks were not relayed to both participants: actor={actorAttacks.Length}, peer={peerAttacks.Length}.");

            var criticalCount = 0;
            for (var index = 0; index < Attempts; index++)
            {
                var actorAttack = actorAttacks[index];
                var peerAttack = peerAttacks[index];
                Check(actorAttack.Header.Type == AttackRequest.TwoTargetMessageType && actorAttack.Header.Size == AttackRequest.TwoTargetPacketSize,
                    "DoubleCritical response changed the two-target attack packet type or size.");
                Check(actorAttack.Payload.Span.SequenceEqual(peerAttack.Payload.Span), "The attacker and observer received different authoritative attack payloads.");
                var progress = BinaryPrimitives.ReadUInt16LittleEndian(actorAttack.Payload.Span[32..]);
                var expectedProgress = index == Attempts ? (ushort)2 : (ushort)(7 + index);
                Check(progress == expectedProgress,
                    $"The server did not normalize and advance attack Progress: index={index}, expected={expectedProgress}, actual={progress}.");
                Check(BinaryPrimitives.ReadUInt16LittleEndian(actorAttack.Payload.Span[48..]) == peerId &&
                    BinaryPrimitives.ReadInt32LittleEndian(actorAttack.Payload.Span[52..]) > 0,
                    "The first attack slot did not contain the server-calculated physical target and damage.");

                var doubleCritical = actorAttack.Payload.Span[36];
                var secondTarget = BinaryPrimitives.ReadUInt16LittleEndian(actorAttack.Payload.Span[56..]);
                var secondDamage = BinaryPrimitives.ReadInt32LittleEndian(actorAttack.Payload.Span[60..]);
                Check(doubleCritical <= 7 && secondTarget == 0,
                    $"Server relayed client-controlled DoubleCritical or second target: flag={doubleCritical}, target={secondTarget}.");
                if ((doubleCritical & 4) != 0)
                {
                    Check(secondDamage > 0, "The server set DoubleCritical without authoritative second-slot damage.");
                    criticalCount++;
                }
                else
                    Check(secondDamage == 0, "The server relayed a second-slot damage value when DoubleCritical did not proc.");
            }

            Check(criticalCount > 0, "The 24 eligible physical attacks did not produce the 7.69 1-in-4 DoubleCritical proc.");
            Console.WriteLine($"ATTACK_DOUBLE_CRITICAL_PROCS={criticalCount}/{TotalAttacks}");
            await actor.LogoutAsync();
            await peer.LogoutAsync();
        }

        Console.WriteLine("CLIENT_E2E_PASS attack-double-critical authoritative-two-target-slot-and-secondary-damage");
    }

    private static byte[] BuildAttack(LegacyFrameCodec codec, ushort actorId, ushort targetId, ushort progress, uint tick, byte keyword)
    {
        var payload = new byte[AttackRequest.TwoTargetPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), actorId);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(32), progress);
        payload[36] = 0xFF; // Client intent is deliberately hostile; server output must be authoritative.
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), -1); // SkillIndex -1 selects the physical attack branch.
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(48), targetId);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(52), -2);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(56), targetId);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(60), 123456);
        var bytes = codec.Encode(AttackRequest.TwoTargetMessageType, actorId, tick, payload, keyword);
        Check(AttackRequest.TryParse(codec.Decode(bytes), out var attack) && attack is { DoubleCritical: 0xFF, SkillIndex: -1 } && attack.Progress == progress,
            "DoubleCritical smoke did not construct a checksum-valid hostile two-target physical attack.");
        return bytes;
    }

    private static int Hp(DecodedFrame frame) => BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span.Slice(
        CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + 24, sizeof(int)));

    private static ushort Id(DecodedFrame frame) => BinaryPrimitives.ReadUInt16LittleEndian(
        frame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);

    private static bool IsAttack(DecodedFrame frame) => frame.Header.Type is
        AttackRequest.AreaMessageType or AttackRequest.OneTargetMessageType or AttackRequest.TwoTargetMessageType;

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

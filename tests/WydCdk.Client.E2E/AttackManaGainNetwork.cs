using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;

/// <summary>
/// Loopback smoke for the 7.69 mana block with a signed SaveMana above 100: BASE_GetManaSpent turns negative,
/// `Mp - ManaSpent < 0` accepts it, MP rises, and SetReqMp caps CurrentScore.Mp at MaxMp.
/// </summary>
internal static class AttackManaGainNetwork
{
    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AutoTradeClosePeerAccountFile!;
        var actorBefore = await File.ReadAllBytesAsync(options.AccountFile);
        var peerBefore = await File.ReadAllBytesAsync(peerFile);
        var peerHeader = new AccountHeader(
            CString(peerBefore.AsSpan(0, 16)), CString(peerBefore.AsSpan(16, 12)), peerBefore.AsSpan(202, 6).ToArray());

        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "ATTACK_MANA_ACTOR_"))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "ATTACK_MANA_PEER_"))
        {
            var actorId = Id(actor.CharacterLoginFrame);
            var peerId = Id(peer.CharacterLoginFrame);
            Check(actorId > 0 && peerId > 0 && actorId != peerId, "Attack mana fixture needs distinct authoritative IDs.");
            Check(Score(actor.CharacterLoginFrame, 20) == 107 && Score(actor.CharacterLoginFrame, 28) == 100,
                "Actor fixture must enter with MaxMp 107 and Mp 100.");
            Check(unchecked((sbyte)actor.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.SaveManaOffset]) == 120,
                "Actor fixture must enter with SaveMana 120.");
            Console.WriteLine($"ATTACK_MANA_ACTOR_ID={actorId}");
            await Phase("ATTACK-MANA-READY");

            var tick = unchecked((uint)Environment.TickCount64);
            Check(tick > 120_001 && tick < uint.MaxValue - 20_000u, "Local monotonic clock cannot host this smoke's tick sequence.");
            tick -= 100_000u;
            // Learned skill 10 costs 20 * (Special2/2 + 100) / 100 = 20; (100 - 120) * 20 / 100 = -4.
            await actor.Stream.WriteAsync(BuildAttack(codec, actorId, tick += 1000u, keyword: 41));
            await actor.Stream.WriteAsync(BuildAttack(codec, actorId, tick += 1000u, keyword: 42));
            var relayed = await Phase("ATTACK-MANA-GAIN");
            var actorAttacks = relayed.Actor.Where(IsAttack).ToArray();
            var peerAttacks = relayed.Peer.Where(IsAttack).ToArray();
            Check(actorAttacks.Length == 2 && peerAttacks.Length == 2, "Negative-cost skill attacks were not relayed to actor and observer.");
            var mp = actorAttacks.Select(frame => BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[AttackConfirmation.CurrentMpOffset..])).ToArray();
            Check(peerAttacks.Select(frame => BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[AttackConfirmation.CurrentMpOffset..])).SequenceEqual(mp),
                "Observer received a different CurrentMp.");
            Console.WriteLine($"ATTACK_MANA_MP={string.Join(',', mp)}");
            Check(mp[0] == 104 && mp[1] == 107, "Negative ManaSpent did not raise MP 100 -> 104 and cap 108 at MaxMp 107.");

            await actor.LogoutAsync();
            await peer.LogoutAsync();

            Task<(IReadOnlyList<DecodedFrame> Actor, IReadOnlyList<DecodedFrame> Peer)> Phase(string label)
                => AutoTradeCloseNetwork.CapturePhase(actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, label, peerFirst: false);
        }

        await MotionGateNetwork.AssertDisk(options.AccountFile, actorBefore, options.Slot);
        await MotionGateNetwork.AssertDisk(peerFile, peerBefore, options.Slot);
        Console.WriteLine("CLIENT_E2E_PASS attack-mana-signed-savemana-gain-capped-at-maxmp");
    }

    private static byte[] BuildAttack(LegacyFrameCodec codec, ushort actorId, uint tick, byte keyword)
    {
        var payload = new byte[AttackRequest.OneTargetPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), actorId);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), 10);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(52), -2);
        return codec.Encode(AttackRequest.OneTargetMessageType, actorId, tick, payload, keyword);
    }

    private static int Score(DecodedFrame frame, int offset) => BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span.Slice(
        CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + offset, sizeof(int)));

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

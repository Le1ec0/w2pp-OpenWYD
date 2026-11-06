using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;

/// <summary>
/// Loopback smoke for the TMProject 7.69 MSG_Attack skill block: physical SkillIndex -1, silent
/// other-class/passive rejections, the fail-closed mortar/invalid indices, and nonterminal learned-skill
/// crack errors for class skills (8/type10) and shared skills (208/type1).
/// </summary>
internal static class AttackSkillGateNetwork
{
    private const int FixtureMana = 5000;

    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AutoTradeClosePeerAccountFile!;
        var actorBefore = await File.ReadAllBytesAsync(options.AccountFile);
        var peerBefore = await File.ReadAllBytesAsync(peerFile);
        var peerHeader = new AccountHeader(
            CString(peerBefore.AsSpan(0, 16)), CString(peerBefore.AsSpan(16, 12)), peerBefore.AsSpan(202, 6).ToArray());

        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "ATTACK_SKILL_ACTOR_"))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "ATTACK_SKILL_PEER_"))
        {
            var actorId = Id(actor.CharacterLoginFrame);
            var peerId = Id(peer.CharacterLoginFrame);
            Check(actorId > 0 && peerId > 0 && actorId != peerId, "Attack skill fixture needs distinct authoritative IDs.");
            Check(Hp(actor.CharacterLoginFrame) > 0 && Hp(peer.CharacterLoginFrame) > 0, "Attack skill fixtures must be alive.");
            Check(Score(actor.CharacterLoginFrame, 28) == FixtureMana, "Actor fixture must enter with 5000 MP.");
            // LearnedSkill=0 is seeded by the runner in the isolated blob; the 7.69 MOB keeps Class at +20.
            Check(actor.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.MobOffset + 20] == 0, "Actor fixture must be class 0.");
            Console.WriteLine($"ATTACK_SKILL_ACTOR_ID={actorId}");
            Console.WriteLine($"ATTACK_SKILL_PEER_ID={peerId}");
            await Phase("ATTACK-SKILL-READY");

            var tick = unchecked((uint)Environment.TickCount64);
            Check(tick > 120_001 && tick < uint.MaxValue - 20_000u, "Local monotonic clock cannot host this smoke's tick sequence.");
            tick -= 100_000u; // Inside the 120 s window; each attack advances 1 s, passing the 800/700 ms gates.

            // Silent/fail-closed rejections first: other class, passive, mortar, invalid index.
            foreach (var skill in new short[] { 30, 9, 97, -2 })
                await actor.Stream.WriteAsync(BuildAttack(codec, actorId, tick += 1000u, skill, keyword: 31));
            var rejected = await Phase("ATTACK-SKILL-REJECTED");
            Check(!rejected.Actor.Concat(rejected.Peer).Any(IsAttack), "A rejected skill attack was relayed.");
            Check(!rejected.Actor.Concat(rejected.Peer).Any(frame => frame.Header.Type is SetHpMpConfirmation.MessageType or SetHpModeConfirmation.MessageType),
                "A rejected skill attack reached a resource/HP response.");

            // Physical -1 skips skill/mana; unlearned 10 and 96 continue after crack points; 103 has no 7.69 learned check.
            short[] accepted = [-1, 10, 96, 103];
            foreach (var skill in accepted)
                await actor.Stream.WriteAsync(BuildAttack(codec, actorId, tick += 1000u, skill, keyword: 32));
            var relayed = await Phase("ATTACK-SKILL-ACCEPTED");
            var actorAttacks = relayed.Actor.Where(IsAttack).ToArray();
            var peerAttacks = relayed.Peer.Where(IsAttack).ToArray();
            Check(actorAttacks.Select(Skill).SequenceEqual(accepted) && peerAttacks.Select(Skill).SequenceEqual(accepted),
                "Accepted attacks were not relayed to actor and observer in source order.");
            for (var i = 0; i < accepted.Length; i++)
            {
                Check(actorAttacks[i].Payload.Span.SequenceEqual(peerAttacks[i].Payload.Span), "Actor and observer received different attack relays.");
                Check(BinaryPrimitives.ReadUInt16LittleEndian(actorAttacks[i].Payload.Span[30..]) == actorId, "Relay lost the attacker ID.");
            }
            var mp = actorAttacks.Select(frame => BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[AttackConfirmation.CurrentMpOffset..])).ToArray();
            var reqMp = actorAttacks.Select(frame => BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[AttackConfirmation.RequestedMpOffset..])).ToArray();
            Check(mp[0] == FixtureMana, "Physical SkillIndex -1 consumed mana.");
            Check(mp[1] < mp[0] && mp[2] < mp[1], "Unlearned skills 10 and 96 did not continue into mana consumption.");
            Check(mp[3] == mp[2], "Skill 103 (ManaSpent 0) changed MP.");
            Check(reqMp.SequenceEqual(mp.Select(value => unchecked((short)value))), "One-target attack relay did not narrow authoritative CurrentMp into ReqMp.");
            Console.WriteLine($"ATTACK_SKILL_MP={string.Join(',', mp)}");

            await actor.LogoutAsync();
            await peer.LogoutAsync();

            Task<(IReadOnlyList<DecodedFrame> Actor, IReadOnlyList<DecodedFrame> Peer)> Phase(string label)
                => AutoTradeCloseNetwork.CapturePhase(actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, label, peerFirst: false);
        }

        await MotionGateNetwork.AssertDisk(options.AccountFile, actorBefore, options.Slot);
        await MotionGateNetwork.AssertDisk(peerFile, peerBefore, options.Slot);
        Console.WriteLine("CLIENT_E2E_PASS attack-skill-gate physical-silent-rejects-nonterminal-learned-crack");
    }

    private static byte[] BuildAttack(LegacyFrameCodec codec, ushort actorId, uint tick, short skill, byte keyword)
    {
        var payload = new byte[AttackRequest.OneTargetPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), actorId);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), skill);
        // Dam[0] has no target; physical intent -2 keeps the relay free of damage/HP mutations.
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(52), -2);
        var bytes = codec.Encode(AttackRequest.OneTargetMessageType, actorId, tick, payload, keyword);
        Check(AttackRequest.TryParse(codec.Decode(bytes), out var attack) && attack is not null && attack.SkillIndex == skill,
            "Skill gate smoke did not create a valid-wire attack.");
        return bytes;
    }

    private static short Skill(DecodedFrame frame) => BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[44..]);

    private static int Hp(DecodedFrame frame) => Score(frame, 24);

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

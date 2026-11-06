using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;

internal static class AttackManaControlNetwork
{
    private const int Attempts = 24;
    private const int TotalAttacks = Attempts + 1;

    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AutoTradeClosePeerAccountFile ?? throw new InvalidOperationException("ManaControl smoke requires an isolated peer account.");
        var actorBefore = await File.ReadAllBytesAsync(options.AccountFile);
        var peerBefore = await File.ReadAllBytesAsync(peerFile);
        var peerHeader = new AccountHeader(CString(peerBefore.AsSpan(0, 16)), CString(peerBefore.AsSpan(16, 12)), peerBefore.AsSpan(202, 6).ToArray());

        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "MANA_CONTROL_ACTOR_"))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "MANA_CONTROL_PEER_"))
        {
            var actorId = Id(actor.CharacterLoginFrame);
            var peerId = Id(peer.CharacterLoginFrame);
            Check(actorId > 0 && peerId > 0 && actorId != peerId, "ManaControl smoke needs distinct authoritative participant IDs.");
            Check(Hp(actor.CharacterLoginFrame) > 0 && Hp(peer.CharacterLoginFrame) >= 100_000, "ManaControl fixtures must be alive with a high-HP target.");
            Check(actor.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.MobOffset + 20] == 3, "ManaControl actor fixture must be class 3.");
            var actorLearned = BinaryPrimitives.ReadUInt32LittleEndian(actor.CharacterLoginFrame.Payload.Span[
                (CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.LearnedSkillOffset)..]);
            Check((actorLearned & 0xA00000) == 0xA00000, "ManaControl actor fixture must know passive bit 21 and divisor bit 23.");

            var initialMana = Score(peer.CharacterLoginFrame, 28);
            var maxMana = Score(peer.CharacterLoginFrame, 20);
            Check(initialMana > (maxMana / 100) * 20 && maxMana >= 100_000,
                $"ManaControl target must log in above the 20 percent threshold with high MaxMp; mp={initialMana}, maxMp={maxMana}.");
            Console.WriteLine($"ATTACK_MANA_CONTROL_ACTOR_ID={actorId}");
            Console.WriteLine($"ATTACK_MANA_CONTROL_PEER_ID={peerId}");

            await AutoTradeCloseNetwork.CapturePhase(actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, "MANA-CONTROL-READY", peerFirst: false);
            var currentTick = unchecked((uint)Environment.TickCount64);
            Check(currentTick > 120_001 && currentTick < uint.MaxValue - 40_000u, "Local monotonic clock cannot host the ManaControl smoke tick sequence.");
            var tick = currentTick - 60_000u;
            for (var index = 0; index < TotalAttacks; index++)
            {
                var progress = index == Attempts ? (ushort)1025 : (ushort)(6 + index);
                await actor.Stream.WriteAsync(BuildAttack(codec, actorId, peerId, progress, tick += 1000u, (byte)(90 + index)));
            }

            var relayed = await AutoTradeCloseNetwork.CapturePhase(actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, "MANA-CONTROL-RESULT", peerFirst: false);
            var actorAttacks = relayed.Actor.Where(IsAttack).ToArray();
            var peerAttacks = relayed.Peer.Where(IsAttack).ToArray();
            var targetResources = relayed.Peer.Where(frame => frame.Header.Type == SetHpMpConfirmation.MessageType && frame.Header.Id == peerId).ToArray();
            Check(actorAttacks.Length == TotalAttacks && peerAttacks.Length == TotalAttacks && targetResources.Length == TotalAttacks,
                $"ManaControl did not relay every two-target attack and target MP sync: actor={actorAttacks.Length}, peer={peerAttacks.Length}, resources={targetResources.Length}.");

            var previousMana = initialMana;
            var previousHp = Hp(peer.CharacterLoginFrame);
            var passiveProcs = 0;
            var positiveManaControlHits = 0;
            for (var index = 0; index < TotalAttacks; index++)
            {
                var attack = actorAttacks[index];
                var observerAttack = peerAttacks[index];
                var resource = targetResources[index];
                Check(attack.Header.Type == AttackRequest.TwoTargetMessageType && attack.Header.Size == AttackRequest.TwoTargetPacketSize &&
                    attack.Payload.Span.SequenceEqual(observerAttack.Payload.Span), "Attacker and observer did not receive the same authoritative physical attack frame.");
                Check(BinaryPrimitives.ReadUInt16LittleEndian(attack.Payload.Span[32..]) == (index == Attempts ? 2 : 7 + index),
                    "ManaControl changed the server-owned DoubleCritical Progress sequence.");
                var attackMp = BinaryPrimitives.ReadInt32LittleEndian(attack.Payload.Span[AttackConfirmation.CurrentMpOffset..]);
                var attackReqMp = BinaryPrimitives.ReadInt16LittleEndian(attack.Payload.Span[AttackConfirmation.RequestedMpOffset..]);
                Check(attackReqMp == unchecked((short)attackMp), "Two-target attack relay did not narrow authoritative CurrentMp into ReqMp.");

                var primaryTarget = BinaryPrimitives.ReadUInt16LittleEndian(attack.Payload.Span[48..]);
                var damage = BinaryPrimitives.ReadInt32LittleEndian(attack.Payload.Span[52..]);
                var flags = attack.Payload.Span[36];
                var secondTarget = BinaryPrimitives.ReadUInt16LittleEndian(attack.Payload.Span[56..]);
                var secondDamage = BinaryPrimitives.ReadInt32LittleEndian(attack.Payload.Span[60..]);
                var currentHp = BinaryPrimitives.ReadInt32LittleEndian(resource.Payload.Span);
                var currentMana = BinaryPrimitives.ReadInt32LittleEndian(resource.Payload.Span[4..]);
                var manaDebit = previousMana - currentMana;
                var parried = damage <= 0 && manaDebit is >= -4 and <= -1;
                Check(primaryTarget == peerId && currentMana > (maxMana / 100) * 20 &&
                    (parried || damage > 0 && manaDebit > 0),
                    $"ManaControl produced neither a positive drain nor the legacy MP refund for a parry on packet {index}: damage={damage}, mp={previousMana}->{currentMana}.");
                var expectedDamage = unchecked(((manaDebit >> 1) + (manaDebit << 4)) / 55);
                var expectedHp = damage > 0 ? previousHp - damage : previousHp;
                Check(damage == expectedDamage && currentHp == expectedHp,
                    $"Affect 18's 55-divisor damage/HP calculation differed on packet {index}: expected={expectedDamage}, damage={damage}, hp={previousHp}->{currentHp}.");
                if (!parried)
                    positiveManaControlHits++;
                Check(secondTarget == 0 && flags <= 7, "ManaControl relayed client-controlled DoubleCritical flags or a second target.");
                if ((flags & 4) != 0)
                {
                    Check(secondDamage == damage, "Affect 18 did not overwrite the DoubleCritical secondary slot with the reduced damage.");
                    passiveProcs++;
                }
                else
                    Check(secondDamage == 0, "A non-proc second slot retained client-declared damage.");
                previousMana = currentMana;
                previousHp = currentHp;
            }

            Check(positiveManaControlHits > 0, "The smoke did not exercise positive ManaControl HP/MP damage.");
            Check(passiveProcs > 0, "The 24 eligible attacks did not exercise ManaControl's DoubleCritical second-slot override.");
            Console.WriteLine($"ATTACK_MANA_CONTROL_STATE=mp:{initialMana}->{previousMana},hp:{Hp(peer.CharacterLoginFrame)}->{previousHp},doublecritical:{passiveProcs}/{TotalAttacks}");
            await actor.LogoutAsync();
            await peer.LogoutAsync();
        }

        await MotionGateNetwork.AssertDisk(options.AccountFile, actorBefore, options.Slot);
        await MotionGateNetwork.AssertDisk(peerFile, peerBefore, options.Slot);
        Console.WriteLine("CLIENT_E2E_PASS attack-mana-control-affect18-mp-drain-55-divisor-doublecritical-slot-override");
    }

    private static byte[] BuildAttack(LegacyFrameCodec codec, ushort actorId, ushort targetId, ushort progress, uint tick, byte keyword)
    {
        var payload = new byte[AttackRequest.TwoTargetPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), actorId);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(32), progress);
        payload[36] = 0xFF;
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), -1);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(48), targetId);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(52), -2);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(56), targetId);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(60), 123456);
        var bytes = codec.Encode(AttackRequest.TwoTargetMessageType, actorId, tick, payload, keyword);
        Check(AttackRequest.TryParse(codec.Decode(bytes), out var attack) && attack is { DoubleCritical: 0xFF, SkillIndex: -1 } && attack.Progress == progress,
            "ManaControl smoke did not construct a checksum-valid hostile two-target physical attack.");
        return bytes;
    }

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

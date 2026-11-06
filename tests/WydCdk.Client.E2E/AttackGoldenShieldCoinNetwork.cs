using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;

/// <summary>Headless loopback smoke for skill 85's private UpdateEtc-before-mana ordering.</summary>
internal static class AttackGoldenShieldCoinNetwork
{
    private const int Fee = 300;
    private const int StartingCoin = 600;
    private const int StartingMana = 121;

    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AutoTradeClosePeerAccountFile!;
        var actorBefore = await File.ReadAllBytesAsync(options.AccountFile);
        var peerBefore = await File.ReadAllBytesAsync(peerFile);
        var peerHeader = new AccountHeader(
            CString(peerBefore.AsSpan(0, 16)), CString(peerBefore.AsSpan(16, 12)), peerBefore.AsSpan(202, 6).ToArray());

        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "ATTACK_GOLDEN_SHIELD_ACTOR_"))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "ATTACK_GOLDEN_SHIELD_PEER_"))
        {
            var actorId = Id(actor.CharacterLoginFrame);
            var peerId = Id(peer.CharacterLoginFrame);
            Check(actorId > 0 && peerId > 0 && actorId != peerId, "Golden Shield smoke needs distinct authoritative actor and peer IDs.");
            Check(actor.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.ClassOffset] == 3,
                "Golden Shield fixture must be class 3.");
            Check(BinaryPrimitives.ReadUInt32LittleEndian(actor.CharacterLoginFrame.Payload.Span.Slice(
                CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.LearnedSkillOffset, sizeof(uint))) == (1u << 13),
                "Golden Shield fixture must have only learned-skill bit 13 set.");
            Check(BinaryPrimitives.ReadInt32LittleEndian(actor.CharacterLoginFrame.Payload.Span.Slice(
                CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CoinOffset, sizeof(int))) == StartingCoin,
                "Golden Shield fixture must enter with 600 Gold.");
            Check(Score(actor.CharacterLoginFrame, 28) == StartingMana, "Golden Shield fixture must enter with 121 MP.");
            Check(BinaryPrimitives.ReadInt16LittleEndian(actor.CharacterLoginFrame.Payload.Span.Slice(
                CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + 44, sizeof(short))) == 3,
                "Skill 85's authoritative stage (Special[2]/C# Special3) must be 3.");
            await Phase("ATTACK-GOLDEN-SHIELD-READY");

            var tick = unchecked((uint)Environment.TickCount64);
            Check(tick > 120_001 && tick < uint.MaxValue - 20_000u, "Local monotonic clock cannot host this smoke's tick sequence.");
            tick -= 100_000u;

            // One exact-cost cast succeeds and spends all 121 MP. The second fee is paid before mana fails.
            await actor.Stream.WriteAsync(BuildAttack(codec, actorId, tick += 1000u, 51));
            var paidWithMana = await Phase("ATTACK-GOLDEN-SHIELD-PAID-MANA");
            var firstCoinFrames = EtcFrames(paidWithMana.Actor);
            var firstAttackFrames = paidWithMana.Actor.Where(IsAttack).ToArray();
            Check(firstCoinFrames.Length == 1 && Coin(firstCoinFrames[0]) == StartingCoin - Fee,
                "First Golden Shield cast did not privately send UpdateEtc with Gold 300.");
            Check(firstAttackFrames.Length == 1 && CurrentMp(firstAttackFrames[0]) == 0,
                "The exact-cost Golden Shield cast did not relay after consuming its authoritative 121 MP.");
            Check(IndexOf(paidWithMana.Actor, UpdateEtcConfirmationV769.MessageType) < IndexOfAttack(paidWithMana.Actor),
                "Golden Shield's UpdateEtc did not precede the accepted attack relay.");
            Check(paidWithMana.Peer.Count(IsAttack) == 1 && EtcFrames(paidWithMana.Peer).Length == 0,
                "Golden Shield coin UpdateEtc was not private to the caster.");

            // The source sends UpdateEtc immediately after charging, then SetHpMp reports insufficient mana.
            await actor.Stream.WriteAsync(BuildAttack(codec, actorId, tick += 1000u, 52));
            var paidWithoutMana = await Phase("ATTACK-GOLDEN-SHIELD-PAID-NO-MANA");
            var secondCoinFrames = EtcFrames(paidWithoutMana.Actor);
            var manaSyncs = paidWithoutMana.Actor.Where(frame => frame.Header.Type == SetHpMpConfirmation.MessageType).ToArray();
            Check(secondCoinFrames.Length == 1 && Coin(secondCoinFrames[0]) == StartingCoin - (2 * Fee),
                "Second Golden Shield fee was not retained and sent as UpdateEtc before mana failure.");
            Check(manaSyncs.Length == 1 && BinaryPrimitives.ReadInt32LittleEndian(manaSyncs[0].Payload.Span[4..]) == 0,
                "Insufficient mana did not return the authoritative zero-MP sync.");
            Check(IndexOf(paidWithoutMana.Actor, UpdateEtcConfirmationV769.MessageType) < IndexOf(paidWithoutMana.Actor, SetHpMpConfirmation.MessageType),
                "Insufficient-mana response arrived before the source-ordered Gold update.");
            Check(paidWithoutMana.Actor.All(frame => !IsAttack(frame)) && paidWithoutMana.Peer.All(frame => !IsAttack(frame)),
                "An attack with insufficient mana was relayed.");

            // No Gold means a silent return before mana and without any response or attack relay.
            await actor.Stream.WriteAsync(BuildAttack(codec, actorId, tick += 1000u, 53));
            var noCoin = await Phase("ATTACK-GOLDEN-SHIELD-NO-COIN");
            Check(noCoin.Actor.Count == 0 && noCoin.Peer.Count == 0,
                "Insufficient Gold did not silently stop the handler before mana or relay.");

            await actor.LogoutAsync();
            await peer.LogoutAsync();

            Task<(IReadOnlyList<DecodedFrame> Actor, IReadOnlyList<DecodedFrame> Peer)> Phase(string label)
                => AutoTradeCloseNetwork.CapturePhase(actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, label, peerFirst: false);
        }

        await AssertActorDisk(options.AccountFile, actorBefore, options.Slot);
        await MotionGateNetwork.AssertDisk(peerFile, peerBefore, options.Slot);
        Console.WriteLine("CLIENT_E2E_PASS attack-golden-shield coin-update-before-mana private-relay-insufficient-coin-silent");
    }

    private static byte[] BuildAttack(LegacyFrameCodec codec, ushort actorId, uint tick, byte keyword)
    {
        var payload = new byte[AttackRequest.OneTargetPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), actorId);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), 85);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(AttackRequest.DamageArrayOffset), actorId);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(AttackRequest.DamageArrayOffset + 4), -2);
        return codec.Encode(AttackRequest.OneTargetMessageType, actorId, tick, payload, keyword);
    }

    private static int Coin(DecodedFrame frame)
    {
        Check(frame.Payload.Length >= UpdateEtcConfirmationV769.PayloadSize, "Golden Shield UpdateEtc payload was truncated.");
        return BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[UpdateEtcConfirmationV769.CoinOffset..]);
    }

    private static DecodedFrame[] EtcFrames(IReadOnlyList<DecodedFrame> frames) =>
        frames.Where(frame => frame.Header.Type == UpdateEtcConfirmationV769.MessageType).ToArray();

    private static int CurrentMp(DecodedFrame frame) =>
        BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[AttackConfirmation.CurrentMpOffset..]);

    private static int Score(DecodedFrame frame, int offset) => BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span.Slice(
        CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + offset, sizeof(int)));

    private static ushort Id(DecodedFrame frame) => BinaryPrimitives.ReadUInt16LittleEndian(
        frame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);

    private static bool IsAttack(DecodedFrame frame) => frame.Header.Type is
        AttackRequest.AreaMessageType or AttackRequest.OneTargetMessageType or AttackRequest.TwoTargetMessageType;

    private static int IndexOfAttack(IReadOnlyList<DecodedFrame> frames) =>
        frames.Select((frame, index) => (frame, index)).FirstOrDefault(pair => IsAttack(pair.frame)).index;

    private static int IndexOf(IReadOnlyList<DecodedFrame> frames, ushort messageType) =>
        frames.Select((frame, index) => (frame, index)).FirstOrDefault(pair => pair.frame.Header.Type == messageType).index;

    private static async Task AssertActorDisk(string path, byte[] baseline, int slot)
    {
        const int accountCoinOffset = 4504; // Active W2PP account-file ABI: account-wide Coin.
        var current = await File.ReadAllBytesAsync(path);
        var mobOffset = 216 + (slot * 816);
        Check(current.AsSpan(mobOffset + 268, 64 * 8).SequenceEqual(baseline.AsSpan(mobOffset + 268, 64 * 8)) &&
            current.AsSpan(3480, 128 * 8).SequenceEqual(baseline.AsSpan(3480, 128 * 8)),
            "Golden Shield changed durable carry or cargo.");
        Check(BinaryPrimitives.ReadInt32LittleEndian(current.AsSpan(mobOffset + 28, sizeof(int))) == 0 &&
            BinaryPrimitives.ReadInt32LittleEndian(current.AsSpan(accountCoinOffset, sizeof(int))) == 0,
            "Golden Shield Gold debit was not persisted to both MOB.Coin and account-wide Coin on logout.");
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
}

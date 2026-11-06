using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using WydCdk.Protocol;

/// <summary>
/// Loopback smoke for 7.69 MSG_Attack bParty and the guildon/guildoff chat commands:
/// a static NPC, same-guild player, unpartied player, and unpartied summon are rejected;
/// guildon restores the same-guild path and party eligibility includes a summoned mob.
/// </summary>
internal static class AttackPartySkillGateNetwork
{
    private const short SkillId = 29;
    private const short SummonSkillId = 56;
    private const int PeerStartHp = 1000;
    private const ushort StaticNpcId = 20_000;

    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AutoTradeClosePeerAccountFile
            ?? throw new InvalidOperationException("Party skill smoke requires an isolated peer fixture.");
        var peerBytes = await File.ReadAllBytesAsync(peerFile);
        var peerHeader = new AccountHeader(
            CString(peerBytes.AsSpan(0, 16)), CString(peerBytes.AsSpan(16, 12)), peerBytes.AsSpan(202, 6).ToArray());

        await using var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "ATTACK_PARTY_ACTOR_");
        await using var peer = await E2eSession.ConnectAndLoginAsync(
            options with { AccountFile = peerFile }, peerHeader, codec, "ATTACK_PARTY_PEER_");

        var actorId = Id(actor.CharacterLoginFrame);
        var peerId = Id(peer.CharacterLoginFrame);
        Check(actorId > 0 && peerId > 0 && actorId != peerId, "Party skill fixture needs distinct authoritative IDs.");
        var actorGuild = GuildId(actor.CharacterLoginFrame);
        var peerGuild = GuildId(peer.CharacterLoginFrame);
        Check(actorGuild != 0 && actorGuild == peerGuild,
            $"GuildDisable smoke requires both isolated characters in the same nonzero guild: {actorGuild}/{peerGuild}.");
        Check(actor.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.ClassOffset] == 1,
            "Party skill actor must be a Foema.");
        var learned = BinaryPrimitives.ReadUInt32LittleEndian(actor.CharacterLoginFrame.Payload.Span.Slice(
            CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.LearnedSkillOffset, sizeof(uint)));
        Check((learned & (1u << (SkillId - 24))) != 0, "Party skill fixture must have skill 29 learned.");
        var peerClass = peer.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.ClassOffset];
        var peerLearned = BinaryPrimitives.ReadUInt32LittleEndian(peer.CharacterLoginFrame.Payload.Span.Slice(
            CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.LearnedSkillOffset, sizeof(uint)));
        Check(peerClass == 2 && (peerLearned & (1u << (SummonSkillId % 24))) != 0,
            "Party skill peer fixture must be class 2 with learned Evocar Condor (skill 56).");
        Check(Hp(peer.CharacterLoginFrame) == PeerStartHp, "Party skill target must enter at 1000 HP.");

        var (ax, ay) = Position(actor.CharacterLoginFrame);
        var (px, py) = Position(peer.CharacterLoginFrame);
        Check(Math.Max(Math.Abs(ax - px), Math.Abs(ay - py)) <= 23,
            $"Party skill fixtures spawned out of range: ({ax},{ay}) vs ({px},{py}).");
        var actorName = CString(actor.CharacterLoginFrame.Payload.Span.Slice(
            CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.NameOffset, CharacterMobV769.NameLength));
        var peerName = CString(peer.CharacterLoginFrame.Payload.Span.Slice(
            CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.NameOffset, CharacterMobV769.NameLength));
        Console.WriteLine($"ATTACK_PARTY_ACTOR_ID={actorId}");
        Console.WriteLine($"ATTACK_PARTY_PEER_ID={peerId}");
        Console.WriteLine($"ATTACK_PARTY_NPC_ID={StaticNpcId}");

        await Phase("ATTACK-PARTY-READY");
        var tick = unchecked((uint)Environment.TickCount64);
        Check(tick > 120_001 && tick < uint.MaxValue - 20_000u,
            "Local monotonic clock cannot host the party skill tick sequence.");
        tick -= 100_000u;

        await actor.Stream.WriteAsync(BuildHeal(codec, actorId, StaticNpcId, (short)ax, (short)ay, (short)ax, (short)ay, tick += 1000u, 40));
        var npcBlocked = await Phase("ATTACK-PARTY-STATIC-NPC");
        AssertAttackRelay(npcBlocked, actorId, StaticNpcId, expectedDamage: -1, "static NPC rejection");
        Check(!npcBlocked.Actor.Concat(npcBlocked.Peer).Any(frame => frame.Header.Type == SetHpMpConfirmation.MessageType),
            "The blocked static-NPC healing skill changed player resources.");

        await actor.Stream.WriteAsync(new MessageChatConfirmation("guildoff").ToFrame(codec, tick += 1000u, 42, actorId));
        var guildOff = await Phase("ATTACK-PARTY-GUILDOFF");
        AssertGuildDisableFrames(guildOff, actorId, peerId, disabled: true, guildId: actorGuild);

        await actor.Stream.WriteAsync(BuildHeal(codec, actorId, peerId, (short)ax, (short)ay, (short)px, (short)py, tick += 1000u, 41));
        var blocked = await Phase("ATTACK-PARTY-GUILDOFF-SAME-GUILD");
        AssertAttackRelay(blocked, actorId, peerId, expectedDamage: -1, "guildoff same-guild rejection");
        Check(!blocked.Actor.Concat(blocked.Peer).Any(frame => frame.Header.Type == SetHpMpConfirmation.MessageType),
            "guildoff same-guild healing changed HP or emitted a resource synchronization.");

        await actor.Stream.WriteAsync(new MessageChatConfirmation("guildon").ToFrame(codec, tick += 1000u, 43, actorId));
        var guildOn = await Phase("ATTACK-PARTY-GUILDON");
        AssertGuildDisableFrames(guildOn, actorId, peerId, disabled: false, guildId: actorGuild);

        await actor.Stream.WriteAsync(BuildHeal(codec, actorId, peerId, (short)ax, (short)ay, (short)px, (short)py, tick += 1000u, 44));
        var sameGuild = await Phase("ATTACK-PARTY-SAME-GUILD");
        var sameGuildAttack = AssertAttackRelay(sameGuild, actorId, peerId, expectedDamage: null, "guildon same-guild heal");
        Check(ReadSlot(sameGuildAttack, 0).Damage < 0 && ReadSlot(sameGuildAttack, 0).Damage != -1,
            "guildon did not restore a heal result for the same guild.");
        var sameGuildHp = sameGuild.Peer.Where(frame => frame.Header.Type == SetHpMpConfirmation.MessageType && frame.Header.Id == peerId).ToArray();
        Check(sameGuildHp.Length == 1 && BinaryPrimitives.ReadInt32LittleEndian(sameGuildHp[0].Payload.Span) > PeerStartHp,
            "guildon did not heal the same-guild peer.");
        var hpBeforeParty = BinaryPrimitives.ReadInt32LittleEndian(sameGuildHp[0].Payload.Span);

        await actor.Stream.WriteAsync(new MessageChatConfirmation("guildoff").ToFrame(codec, tick += 1000u, 45, actorId));
        var actorGuildOffAgain = await Phase("ATTACK-PARTY-GUILDOFF-AGAIN");
        AssertGuildDisableFrames(actorGuildOffAgain, actorId, peerId, disabled: true, guildId: actorGuild);
        await actor.Stream.WriteAsync(new MessageChatConfirmation("guildoff").ToFrame(codec, tick += 1000u, 46, actorId));
        var guildOffNoOp = await Phase("ATTACK-PARTY-GUILDOFF-NOOP");
        Check(!guildOffNoOp.Actor.Concat(guildOffNoOp.Peer).Any(frame =>
                frame.Header.Type is UpdateScoreConfirmationV769.MessageType or UpdateAffectConfirmationV769.MessageType or GuildDisableConfirmation.MessageType),
            "Repeated guildoff emitted score, affect, or signal frames.");

        await peer.Stream.WriteAsync(new MessageChatConfirmation("guildoff").ToFrame(codec, tick += 1000u, 47, peerId));
        var peerGuildOff = await Phase("ATTACK-PARTY-PEER-GUILDOFF", peerFirst: true);
        AssertGuildDisableFrames((peerGuildOff.Peer, peerGuildOff.Actor), peerId, actorId, disabled: true, guildId: actorGuild);

        await actor.Stream.WriteAsync(BuildHeal(codec, actorId, peerId, (short)ax, (short)ay, (short)px, (short)py, tick += 1000u, 48));
        var disabledOutsiders = await Phase("ATTACK-PARTY-BOTH-GUILDOFF-OUTSIDERS");
        AssertAttackRelay(disabledOutsiders, actorId, peerId, expectedDamage: -1, "guildoff outsiders without party");
        Check(!disabledOutsiders.Actor.Concat(disabledOutsiders.Peer).Any(frame => frame.Header.Type == SetHpMpConfirmation.MessageType),
            "Two guildoff outsiders bypassed bParty or changed HP before party formation.");

        await peer.Stream.WriteAsync(BuildSkillAttack(codec, peerId, peerId, (short)px, (short)py, (short)px, (short)py,
            SummonSkillId, tick += 1000u, 49));
        var summonCreated = await Phase("ATTACK-PARTY-SUMMON-CREATED", peerFirst: true);
        var actorSummons = GetSummonCreateFrames(summonCreated.Actor);
        var peerSummons = GetSummonCreateFrames(summonCreated.Peer);
        Console.WriteLine($"ATTACK_PARTY_SUMMON_CREATE_FRAMES actor={DescribeCreateFrames(summonCreated.Actor)} peer={DescribeCreateFrames(summonCreated.Peer)}");
        Check(actorSummons.Length == 1 && peerSummons.Length == 1 &&
            actorSummons[0].Payload.Span.SequenceEqual(peerSummons[0].Payload.Span),
            "The summoned mob was not created once with the same authoritative wire state for both clients.");
        var summonId = BinaryPrimitives.ReadUInt16LittleEndian(actorSummons[0].Payload.Span[CreateMobConfirmationV769.MobIdOffset..]);
        var summonX = BinaryPrimitives.ReadInt16LittleEndian(actorSummons[0].Payload.Span[CreateMobConfirmationV769.PositionXOffset..]);
        var summonY = BinaryPrimitives.ReadInt16LittleEndian(actorSummons[0].Payload.Span[CreateMobConfirmationV769.PositionYOffset..]);
        Check(summonId >= 1000, $"The dynamic summon used a non-summon connection ID: {summonId}.");
        Console.WriteLine($"ATTACK_PARTY_SUMMON_ID={summonId}");

        await actor.Stream.WriteAsync(BuildHeal(codec, actorId, summonId, (short)ax, (short)ay, summonX, summonY, tick += 1000u, 50));
        var summonOutsider = await Phase("ATTACK-PARTY-SUMMON-OUTSIDER");
        var blockedSummonAttack = AssertAttackRelay(summonOutsider, actorId, summonId, expectedDamage: -1, "unpartied summon rejection");
        Check(ReadSlot(blockedSummonAttack, 0).Damage == -1 &&
            !summonOutsider.Actor.Concat(summonOutsider.Peer).Any(frame => frame.Header.Type == SetHpMpConfirmation.MessageType),
            "An outsider's party skill changed summon/player resources.");

        var invite = new PartyInviteRequest(
            1, 0, 1, 5000, 5000, checked((short)peerId), peerName, actorId, 0)
            .ToFrame(codec, tick += 1000u, 51, peerId);
        await peer.Stream.WriteAsync(invite);
        var inviteFrame = await ReadUntilAsync(actor.Stream, codec, options.Timeout, PartyInviteRequest.MessageType);
        Check(PartyInviteRequest.TryParse(inviteFrame, out var parsedInvite) && parsedInvite is not null &&
            parsedInvite.PartyId == peerId && parsedInvite.MobName == peerName,
            "Actor did not receive the authoritative party invite for the peer leader.");

        await actor.Stream.WriteAsync(new PartyAcceptRequest(checked((short)peerId), peerName)
            .ToFrame(codec, tick += 1000u, 52, actorId));
        var actorParty = await ReadPartyFormationAsync(actor.Stream, codec, options.Timeout);
        var peerParty = await ReadPartyFormationAsync(peer.Stream, codec, options.Timeout);
        AssertPartyFormation(actorParty, peerId, actorId);
        AssertPartyFormation(peerParty, peerId, actorId);

        await actor.Stream.WriteAsync(BuildHeal(codec, actorId, peerId, (short)ax, (short)ay, (short)px, (short)py, tick += 1000u, 53));
        var accepted = await Phase("ATTACK-PARTY-MEMBER");
        var actorAttack = AssertAttackRelay(accepted, actorId, peerId, expectedDamage: null, "same-party heal");
        var healedSlot = ReadSlot(actorAttack, 0);
        Check(healedSlot.Damage < 0 && healedSlot.Damage != -1,
            $"Same-party skill did not produce an authoritative heal result: {healedSlot}.");
        var hpSync = accepted.Peer.Where(frame => frame.Header.Type == SetHpMpConfirmation.MessageType && frame.Header.Id == peerId).ToArray();
        Check(hpSync.Length == 1 && BinaryPrimitives.ReadInt32LittleEndian(hpSync[0].Payload.Span) > hpBeforeParty,
            "Same-party skill did not heal the peer after party formation while both GuildDisable flags were set.");
        Console.WriteLine($"ATTACK_PARTY_HEALED_HP={BinaryPrimitives.ReadInt32LittleEndian(hpSync[0].Payload.Span)}");

        await actor.Stream.WriteAsync(BuildHeal(codec, actorId, summonId, (short)ax, (short)ay, summonX, summonY, tick += 1000u, 54));
        var summonPartyMember = await Phase("ATTACK-PARTY-SUMMON-MEMBER");
        var acceptedSummonAttack = AssertAttackRelay(summonPartyMember, actorId, targetId: 0, expectedDamage: 0, "same-party summon target without a ported effect");
        Check(ReadSlot(acceptedSummonAttack, 0) == (0, 0) &&
            !summonPartyMember.Actor.Concat(summonPartyMember.Peer).Any(frame => frame.Header.Type == SetHpMpConfirmation.MessageType),
            "The eligible but unported summon target did not retain the empty relay slot or changed resources.");

        await actor.LogoutAsync();
        await peer.LogoutAsync();
        Console.WriteLine("CLIENT_E2E_PASS attack-party-skill guilddisable-player-npc-summon-party-eligibility-without-summon-effect");

        Task<(IReadOnlyList<DecodedFrame> Actor, IReadOnlyList<DecodedFrame> Peer)> Phase(string label, bool peerFirst = false)
            => AutoTradeCloseNetwork.CapturePhase(actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, label, peerFirst);
    }

    private static byte[] BuildHeal(LegacyFrameCodec codec, ushort actorId, ushort peerId,
        short ax, short ay, short px, short py, uint tick, byte keyword)
        => BuildSkillAttack(codec, actorId, peerId, ax, ay, px, py, SkillId, tick, keyword);

    private static byte[] BuildSkillAttack(LegacyFrameCodec codec, ushort actorId, ushort targetId,
        short ax, short ay, short tx, short ty, short skillId, uint tick, byte keyword)
    {
        var payload = new byte[AttackRequest.AreaPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(22), ax);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(24), ay);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(26), tx);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(28), ty);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), actorId);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), skillId);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(AttackRequest.DamageArrayOffset), targetId);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(AttackRequest.DamageArrayOffset + 4), -1);
        return codec.Encode(AttackRequest.AreaMessageType, actorId, tick, payload, keyword);
    }

    private static DecodedFrame[] GetSummonCreateFrames(IReadOnlyList<DecodedFrame> frames) => frames.Where(frame =>
        frame.Header.Type == CreateMobConfirmationV769.MessageType &&
        frame.Payload.Length == CreateMobConfirmationV769.PayloadSize &&
        BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[CreateMobConfirmationV769.CreateTypeOffset..]) == 3).ToArray();

    private static string DescribeCreateFrames(IReadOnlyList<DecodedFrame> frames) => string.Join(',', frames
        .Where(frame => frame.Header.Type == CreateMobConfirmationV769.MessageType)
        .Select(frame => frame.Payload.Length < CreateMobConfirmationV769.MobIdOffset + sizeof(ushort)
            ? $"size={frame.Header.Size}/payload={frame.Payload.Length}/short"
            : $"id={BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[CreateMobConfirmationV769.MobIdOffset..])}/type={(frame.Payload.Length >= CreateMobConfirmationV769.CreateTypeOffset + sizeof(ushort) ? BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[CreateMobConfirmationV769.CreateTypeOffset..]) : -1)}/payload={frame.Payload.Length}/header={frame.Header.Id}"));

    private static async Task<DecodedFrame> ReadUntilAsync(NetworkStream stream, LegacyFrameCodec codec, TimeSpan timeout, ushort type)
    {
        for (var index = 0; index < 32; index++)
        {
            var frame = await ReadFrameAsync(stream, codec, timeout);
            if (frame.Header.Type == type)
                return frame;
        }
        throw new InvalidOperationException($"Expected party frame 0x{type:X4} was not received.");
    }

    private static async Task<IReadOnlyList<DecodedFrame>> ReadPartyFormationAsync(NetworkStream stream, LegacyFrameCodec codec, TimeSpan timeout)
    {
        var frames = new List<DecodedFrame>(2);
        for (var index = 0; index < 16 && frames.Count < 2; index++)
        {
            var frame = await ReadFrameAsync(stream, codec, timeout);
            if (frame.Header.Type == PartyAddConfirmation.MessageType)
                frames.Add(frame);
        }
        return frames;
    }

    private static void AssertPartyFormation(IReadOnlyList<DecodedFrame> frames, ushort leaderId, ushort memberId)
    {
        Check(frames.Count == 2 && frames.All(frame => frame.IsChecksumValid && frame.Header.Size == PartyAddConfirmation.PacketSize),
            "Each party member must receive two valid party-add confirmations.");
        var partyIds = frames.Select(frame => BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[8..]))
            .Select(value => unchecked((ushort)value)).ToHashSet();
        Check(partyIds.SetEquals(new[] { leaderId, memberId }), "Party-add confirmations did not identify both authoritative members.");
    }

    private static void AssertGuildDisableFrames(
        (IReadOnlyList<DecodedFrame> Actor, IReadOnlyList<DecodedFrame> Peer) phase,
        ushort actorId,
        ushort peerId,
        bool disabled,
        ushort guildId)
    {
        var expectedGuild = disabled ? (ushort)0 : guildId;
        var actorScores = phase.Actor.Where(frame => frame.Header.Type == UpdateScoreConfirmationV769.MessageType && frame.Header.Id == actorId).ToArray();
        var peerScores = phase.Peer.Where(frame => frame.Header.Type == UpdateScoreConfirmationV769.MessageType && frame.Header.Id == actorId).ToArray();
        var actorGuilds = actorScores.Select(frame => BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[UpdateScoreConfirmationV769.GuildOffset..])).ToArray();
        var peerGuilds = peerScores.Select(frame => BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[UpdateScoreConfirmationV769.GuildOffset..])).ToArray();
        Check(actorScores.Length == 1 && peerScores.Length == 1 &&
            actorScores[0].Header.Size == UpdateScoreConfirmationV769.PacketSize && peerScores[0].Header.Size == UpdateScoreConfirmationV769.PacketSize &&
            actorGuilds[0] == expectedGuild && peerGuilds[0] == expectedGuild,
            $"Guild {(disabled ? "off" : "on")} score mismatch: expected={expectedGuild}, actorId={actorId}, peerId={peerId}, actorScores={string.Join(',', phase.Actor.Where(frame => frame.Header.Type == UpdateScoreConfirmationV769.MessageType).Select(frame => $"{frame.Header.Id}:{BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[UpdateScoreConfirmationV769.GuildOffset..])}"))}, peerScores={string.Join(',', phase.Peer.Where(frame => frame.Header.Type == UpdateScoreConfirmationV769.MessageType).Select(frame => $"{frame.Header.Id}:{BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[UpdateScoreConfirmationV769.GuildOffset..])}"))}.");
        var actorAffects = phase.Actor.Where(frame => frame.Header.Type == UpdateAffectConfirmationV769.MessageType && frame.Header.Id == actorId).ToArray();
        Check(actorAffects.Length == 1 && actorAffects[0].Header.Size == UpdateAffectConfirmationV769.PacketSize,
            $"Guild {(disabled ? "off" : "on")} did not send the private 7.69 affect refresh: actorId={actorId}, affects={string.Join(',', phase.Actor.Where(frame => frame.Header.Type == UpdateAffectConfirmationV769.MessageType).Select(frame => $"{frame.Header.Id}:{frame.Header.Size}"))}.");
        Check(!phase.Peer.Any(frame => frame.Header.Type == UpdateAffectConfirmationV769.MessageType && frame.Header.Id == actorId),
            "The private 7.69 affect refresh was broadcast to the peer.");
        var signals = phase.Actor.Where(frame => frame.Header.Type == GuildDisableConfirmation.MessageType).ToArray();
        Check(signals.Length == 1 && signals[0].Header.Id == AttackConfirmation.SceneId &&
            BinaryPrimitives.ReadInt32LittleEndian(signals[0].Payload.Span) == (disabled ? 1 : 0),
            $"Guild {(disabled ? "off" : "on")} did not send the private MSG_GuildDisable Parm.");
        Check(!phase.Peer.Any(frame => frame.Header.Type == GuildDisableConfirmation.MessageType),
            "MSG_GuildDisable scene signal was broadcast beyond the command author.");
        Check(!phase.Actor.Concat(phase.Peer).Any(frame => frame.Header.Type == MessageChatRequest.MessageType &&
                CString(frame.Payload.Span) is "guildoff" or "guildon"),
            "A guild toggle command was relayed as public chat.");
    }

    private static DecodedFrame AssertAttackRelay(
        (IReadOnlyList<DecodedFrame> Actor, IReadOnlyList<DecodedFrame> Peer) phase,
        ushort actorId, ushort targetId, int? expectedDamage, string label)
    {
        var actorAttacks = phase.Actor.Where(IsAttack).ToArray();
        var peerAttacks = phase.Peer.Where(IsAttack).ToArray();
        if (actorAttacks.Length != 1 || peerAttacks.Length != 1 ||
            actorAttacks.Length == 1 && peerAttacks.Length == 1 && !actorAttacks[0].Payload.Span.SequenceEqual(peerAttacks[0].Payload.Span) ||
            actorAttacks.Length == 1 && (actorAttacks[0].Header.Id != AttackConfirmation.SceneId || ReadSlot(actorAttacks[0], 0).TargetId != targetId))
            Console.WriteLine($"ATTACK_PARTY_RELAY_DIAGNOSTIC label={label} actor={DescribeAttackFrames(actorAttacks)} peer={DescribeAttackFrames(peerAttacks)} expectedTarget={targetId}");
        Check(actorAttacks.Length == 1 && peerAttacks.Length == 1 &&
            actorAttacks[0].Payload.Span.SequenceEqual(peerAttacks[0].Payload.Span),
            $"{label} was not relayed once with identical payloads to actor and observer.");
        Check(actorAttacks[0].Header.Id == AttackConfirmation.SceneId && ReadSlot(actorAttacks[0], 0).TargetId == targetId,
            $"{label} relay changed the legacy scene header or authoritative slot target.");
        if (expectedDamage is int damage)
            Check(ReadSlot(actorAttacks[0], 0).Damage == damage, $"{label} relay slot was {ReadSlot(actorAttacks[0], 0)}, expected damage {damage}.");
        return actorAttacks[0];
    }

    private static async Task<DecodedFrame> ReadFrameAsync(NetworkStream stream, LegacyFrameCodec codec, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        var header = new byte[PacketHeader.SizeInBytes];
        await stream.ReadExactlyAsync(header, cancellation.Token);
        var packetSize = PacketHeader.Read(header).Size;
        Check(packetSize >= PacketHeader.SizeInBytes && packetSize <= PacketHeader.MaxPacketSize, "Party smoke received an invalid frame size.");
        var bytes = new byte[packetSize];
        header.CopyTo(bytes, 0);
        await stream.ReadExactlyAsync(bytes.AsMemory(PacketHeader.SizeInBytes), cancellation.Token);
        var frame = codec.Decode(bytes);
        Check(frame.IsChecksumValid, "Party smoke received an invalid frame checksum.");
        return frame;
    }

    private static (int X, int Y) Position(DecodedFrame frame) =>
        (BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span), BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[2..]));

    private static int Hp(DecodedFrame frame) => BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span.Slice(
        CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + 24, sizeof(int)));

    private static ushort Id(DecodedFrame frame) => BinaryPrimitives.ReadUInt16LittleEndian(
        frame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);

    private static ushort GuildId(DecodedFrame frame) => BinaryPrimitives.ReadUInt16LittleEndian(
        frame.Payload.Span.Slice(CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.GuildOffset, sizeof(ushort)));

    private static (ushort TargetId, int Damage) ReadSlot(DecodedFrame frame, int index)
    {
        Check(AttackRequest.TryReadDamageSlot(frame, index, out var targetId, out var damage), "Party smoke attack slot could not be decoded.");
        return (targetId, damage);
    }

    private static bool IsAttack(DecodedFrame frame) => frame.Header.Type is
        AttackRequest.AreaMessageType or AttackRequest.OneTargetMessageType or AttackRequest.TwoTargetMessageType;

    private static string DescribeAttackFrames(IEnumerable<DecodedFrame> frames) => string.Join(',', frames.Select(frame =>
        $"type=0x{frame.Header.Type:X4}/header={frame.Header.Id}/slot={ReadSlot(frame, 0)}"));

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

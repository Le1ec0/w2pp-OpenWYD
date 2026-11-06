using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using WydCdk.Protocol;

if (args.Length > 0 && args[0] == "--prepare-skill-second-bar-visual-fixture")
{
    if (args.Length != 4)
        throw new ArgumentException("Second-bar visual fixture preparation requires exactly --prepare-skill-second-bar-visual-fixture <Backup/Build/SkillSecondBarVisual> <base-word-uint32> <extra-word-uint32>.");
    await SkillSecondBarVisualFixture.PrepareAsync(
        Path.GetFullPath(args[1]),
        SkillSecondBarVisualFixture.ParseUInt32(args[2]),
        SkillSecondBarVisualFixture.ParseUInt32(args[3]));
    return;
}

if (args.Length > 0 && args[0] == "--prepare-skill-second-bar-blood-hands-book-fixture")
{
    if (args.Length != 2)
        throw new ArgumentException("Blood Hands book fixture preparation requires exactly --prepare-skill-second-bar-blood-hands-book-fixture <Backup/Build/SkillSecondBarBloodHandsBook>.");
    await SkillSecondBarVisualFixture.PrepareBloodHandsBookAsync(Path.GetFullPath(args[1]));
    return;
}

if (args.Length > 0 && args[0] == "--skill-second-bar-wire-proxy")
{
    var proxyOptions = SkillSecondBarWireProxyOptions.Parse(args);
    await SkillSecondBarWireProxy.RunAsync(proxyOptions);
    return;
}

if (args.Contains("--dungeon-event-smoke", StringComparer.Ordinal))
{
    if (!args.SequenceEqual(["--dungeon-event-smoke"]))
        throw new ArgumentException("Dungeon event loopback smoke requires exactly --dungeon-event-smoke and accepts no account or configuration arguments.");
    await DungeonEventNetwork.RunAsync();
    return;
}

if (args.Contains("--mob-ai-smoke", StringComparer.Ordinal))
{
    if (!args.SequenceEqual(["--mob-ai-smoke"]))
        throw new ArgumentException("Mob AI loopback smoke requires exactly --mob-ai-smoke and accepts no account or configuration arguments.");
    try
    {
        await MobAiNetwork.RunAsync();
    }
    catch (Exception error)
    {
        Console.Error.WriteLine($"FAIL --mob-ai-smoke: {error}");
        Environment.ExitCode = 1;
    }
    return;
}

if (args.Contains("--character-checkpoint-smoke", StringComparer.Ordinal))
{
    if (args.Count(value => value == "--character-checkpoint-smoke") != 1)
        throw new ArgumentException("Character checkpoint smoke flag must appear once.");
    var itemDataIndex = Array.IndexOf(args, "--item-data");
    if (itemDataIndex < 0 || itemDataIndex + 1 >= args.Length || args.Length != 3)
        throw new ArgumentException("Character checkpoint smoke requires exactly --character-checkpoint-smoke --item-data <ItemList.bin>.");
    await LegacyCharacterCheckpointNetwork.RunAsync(Path.GetFullPath(args[itemDataIndex + 1]));
    return;
}

if (args.Contains("--npc-sell-smoke", StringComparer.Ordinal))
{
    if (args.Count(value => value == "--npc-sell-smoke") != 1)
        throw new ArgumentException("NPC sell smoke flag must appear once.");
    var itemDataIndex = Array.IndexOf(args, "--item-data");
    if (itemDataIndex < 0 || itemDataIndex + 1 >= args.Length || args.Length != 3)
        throw new ArgumentException("NPC sell smoke requires exactly --npc-sell-smoke --item-data <ItemList.bin>.");
    await NpcSellNetwork.RunAsync(Path.GetFullPath(args[itemDataIndex + 1]));
    return;
}

if (args.Contains("--npc-sell-cargo-smoke", StringComparer.Ordinal))
{
    if (args.Count(value => value == "--npc-sell-cargo-smoke") != 1)
        throw new ArgumentException("NPC Cargo sell smoke flag must appear once.");
    var itemDataIndex = Array.IndexOf(args, "--item-data");
    if (itemDataIndex < 0 || itemDataIndex + 1 >= args.Length || args.Length != 3)
        throw new ArgumentException("NPC Cargo sell smoke requires exactly --npc-sell-cargo-smoke --item-data <ItemList.bin>.");
    await NpcSellNetwork.RunCargoAsync(Path.GetFullPath(args[itemDataIndex + 1]));
    return;
}

if (args.Contains("--npc-sell-out-of-view-smoke", StringComparer.Ordinal))
{
    if (args.Count(value => value == "--npc-sell-out-of-view-smoke") != 1)
        throw new ArgumentException("NPC sell out-of-view smoke flag must appear once.");
    var itemDataIndex = Array.IndexOf(args, "--item-data");
    if (itemDataIndex < 0 || itemDataIndex + 1 >= args.Length || args.Length != 3)
        throw new ArgumentException("NPC sell out-of-view smoke requires exactly --npc-sell-out-of-view-smoke --item-data <ItemList.bin>.");
    await NpcSellNetwork.RunOutOfViewAsync(Path.GetFullPath(args[itemDataIndex + 1]));
    return;
}

if (args.Contains("--npc-sell-empty-carry-resync-smoke", StringComparer.Ordinal))
{
    if (args.Count(value => value == "--npc-sell-empty-carry-resync-smoke") != 1)
        throw new ArgumentException("NPC sell empty-carry resync smoke flag must appear once.");
    var itemDataIndex = Array.IndexOf(args, "--item-data");
    if (itemDataIndex < 0 || itemDataIndex + 1 >= args.Length || args.Length != 3)
        throw new ArgumentException("NPC sell empty-carry resync smoke requires exactly --npc-sell-empty-carry-resync-smoke --item-data <ItemList.bin>.");
    await NpcSellNetwork.RunEmptyCarryResyncAsync(Path.GetFullPath(args[itemDataIndex + 1]));
    return;
}

if (args.SequenceEqual(new[] { "--self-test-guild-war-relay" }))
{
    await GuildWarTransitionRelayGateTests.RunAsync();
    return;
}

if (args.Contains("--guild-war-smoke", StringComparer.Ordinal))
{
    if (args.Count(value => value == "--guild-war-smoke") != 1)
        throw new ArgumentException("GuildWar smoke may be selected only once.");
    var itemDataIndex = Array.IndexOf(args, "--item-data");
    if (itemDataIndex < 0 || itemDataIndex + 1 >= args.Length || args.Length != 3)
        throw new ArgumentException("GuildWar smoke requires exactly --guild-war-smoke --item-data <ItemList.bin>.");
    await GuildWarNetwork.RunAsync(Path.GetFullPath(args[itemDataIndex + 1]));
    return;
}

if (args.Contains("--host-authenticated-type25-smoke", StringComparer.Ordinal))
{
    if (args.Count(value => value == "--host-authenticated-type25-smoke") != 1)
        throw new ArgumentException("Authenticated Type 25 smoke may be selected only once.");
    var itemDataIndex = Array.IndexOf(args, "--item-data");
    var skillDataIndex = Array.IndexOf(args, "--skill-data");
    if (itemDataIndex < 0 || itemDataIndex + 1 >= args.Length ||
        skillDataIndex < 0 || skillDataIndex + 1 >= args.Length || args.Length != 5)
        throw new ArgumentException("Authenticated Type 25 smoke requires exactly --host-authenticated-type25-smoke --item-data <ItemList.bin> --skill-data <SkillData.csv>.");
    await PlayerElementalProtectionHostNetwork.RunAsync(
        Path.GetFullPath(args[itemDataIndex + 1]),
        Path.GetFullPath(args[skillDataIndex + 1]));
    return;
}

if (args.Contains("--prepare-type25-visual-fixture", StringComparer.Ordinal))
{
    if (args.Length != 2 || args[0] != "--prepare-type25-visual-fixture")
        throw new ArgumentException("Type 25 visual fixture preparation requires exactly --prepare-type25-visual-fixture <dedicated-fixture-directory>.");
    await PlayerElementalProtectionHostNetwork.PrepareVisualFixtureAsync(Path.GetFullPath(args[1]));
    return;
}

if (args.SequenceEqual(new[] { "--self-test-autotrade-harness" }))
{
    await AutoTradeCloseHarnessTests.RunAsync();
    return;
}
if (args.Contains("--attack-transformation-smoke", StringComparer.Ordinal))
{
    if (args.Count(value => value == "--attack-transformation-smoke") != 1)
        throw new ArgumentException("Transformation smoke may be selected only once.");
    var transformationOptions = E2eOptions.Parse(args.Where(value => value != "--attack-transformation-smoke").ToArray());
    await AttackTransformationNetwork.RunAsync(
        transformationOptions,
        await ReadAccountHeaderAsync(transformationOptions.AccountFile),
        LegacyFrameCodec.CreateDefault());
    return;
}
if (args.Contains("--attack-empty-target-smoke", StringComparer.Ordinal))
{
    if (args.Count(value => value == "--attack-empty-target-smoke") != 1)
        throw new ArgumentException("Empty-target smoke may be selected only once.");
    var emptyTargetOptions = E2eOptions.Parse(args.Where(value => value != "--attack-empty-target-smoke").ToArray());
    if (emptyTargetOptions.AutoTradeClosePeerAccountFile is null)
        throw new ArgumentException("Empty-target smoke requires --autotrade-close-peer-account-file.");
    await AttackEmptyTargetNetwork.RunAsync(emptyTargetOptions, LegacyFrameCodec.CreateDefault());
    return;
}
if (args.Contains("--auto-double-magic-trumpet-smoke", StringComparer.Ordinal))
{
    if (args.Count(value => value == "--auto-double-magic-trumpet-smoke") != 1)
        throw new ArgumentException("AutoDouble MagicTrumpet smoke may be selected only once.");
    var autoDoubleOptions = E2eOptions.Parse(args.Where(value => value != "--auto-double-magic-trumpet-smoke").ToArray());
    if (autoDoubleOptions.AutoTradeClosePeerAccountFile is null)
        throw new ArgumentException("AutoDouble MagicTrumpet smoke requires --autotrade-close-peer-account-file.");
    await AutoDoubleMagicTrumpetNetwork.RunAsync(autoDoubleOptions, await ReadAccountHeaderAsync(autoDoubleOptions.AccountFile), LegacyFrameCodec.CreateDefault());
    return;
}
var options = E2eOptions.Parse(args);
var codec = LegacyFrameCodec.CreateDefault();
var accountHeader = await ReadAccountHeaderAsync(options.AccountFile);
if (options.CurrentScoreLoginProjectionSmoke || options.CurrentScoreLoginProjectionRejectSmoke)
{
    var observerPath = Path.Combine(Path.GetDirectoryName(options.AccountFile)!, "SCOREOBS");
    var observerOptions = options with { AccountFile = observerPath, ConnectionId = checked((ushort)(options.ConnectionId + 1)) };
    var observerHeader = await ReadAccountHeaderAsync(observerPath);
    if (options.CurrentScoreLoginProjectionRejectSmoke)
        await CurrentScoreLoginProjectionNetwork.RunRejectAsync(options, observerOptions, accountHeader, observerHeader, codec);
    else
        await CurrentScoreLoginProjectionNetwork.RunAsync(options, observerOptions, accountHeader, observerHeader, codec);
    return;
}
if (options.ActionAccountAnalysisSmoke || options.ActionAccountAnalysisThresholdSmoke ||
    options.AccountAnalysisLoginSmoke || options.AccountReviewUnavailableLoginSmoke)
{
    await ActionAccountAnalysisNetwork.RunAsync(options, accountHeader, codec);
    return;
}
if (options.AutoTradeClosePeerAccountFile is not null)
{
    try
    {
        if (options.ActionOriginRecallSmoke || options.MovementRestrictionRecallSmoke)
            await ActionOriginRecallNetwork.RunAsync(options, accountHeader, codec, options.MovementRestrictionRecallSmoke);
        else if (options.ActionIllusionManaSmoke) await ActionIllusionManaNetwork.RunAsync(options, accountHeader, codec);
        else if (options.ActionIllusionEligibilitySmoke) await ActionIllusionEligibilityNetwork.RunAsync(options, accountHeader, codec);
        else if (options.ActionRangeSmoke) await ActionRangeNetwork.RunAsync(options, accountHeader, codec);
        else if (options.ActionGateSmoke) await ActionGateNetwork.RunAsync(options, accountHeader, codec);
        else if (options.TradeListGateSmoke) await TradeListGateNetwork.RunAsync(options, accountHeader, codec);
        else if (options.MotionGateSmoke) await MotionGateNetwork.RunAsync(options, accountHeader, codec);
        else if (options.AttackDeadGateSmoke) await AttackDeadGateNetwork.RunAsync(options, accountHeader, codec);
        else if (options.AttackRvrGateSmoke) await AttackRvrGateNetwork.RunAsync(options, accountHeader, codec);
        else if (options.AttackTimingGateSmoke) await AttackTimingGateNetwork.RunAsync(options, accountHeader, codec);
        else if (options.AttackSkillGateSmoke) await AttackSkillGateNetwork.RunAsync(options, accountHeader, codec);
        else if (options.AttackPartySkillGateSmoke) await AttackPartySkillGateNetwork.RunAsync(options, accountHeader, codec);
        else if (options.AttackGoldenShieldCoinSmoke) await AttackGoldenShieldCoinNetwork.RunAsync(options, accountHeader, codec);
        else if (options.AttackType6ProtectionSmoke) await AttackType6ProtectionNetwork.RunAsync(options, accountHeader, codec);
        else if (options.AttackMultiTargetSmoke) await AttackMultiTargetNetwork.RunAsync(options, accountHeader, codec);
        else if (options.AttackViewGateSmoke) await AttackViewGateNetwork.RunAsync(options, accountHeader, codec);
        else if (options.AttackMixedSlotsSmoke) await AttackMixedSlotsNetwork.RunAsync(options, accountHeader, codec);
        else if (options.AttackDoubleCriticalSmoke) await AttackDoubleCriticalNetwork.RunAsync(options, accountHeader, codec);
        else if (options.AttackManaControlSmoke) await AttackManaControlNetwork.RunAsync(options, accountHeader, codec);
        else if (options.AttackManaGainSmoke) await AttackManaGainNetwork.RunAsync(options, accountHeader, codec);
        else if (options.AttackDivineBlessingSmoke || options.AttackDivineBlessingInsufficientManaSmoke)
            await AttackDivineBlessingNetwork.RunAsync(options, accountHeader, codec);
        else if (options.AttackSkillSummonNoticeSmoke)
            await AttackSkillSummonNoticeNetwork.RunAsync(options, accountHeader, codec);
        else if (options.AttackSkillHealSmoke) await AttackSkillHealNetwork.RunAsync(options, accountHeader, codec);
        else await AutoTradeCloseNetwork.RunAsync(options, accountHeader, codec);
    }
    catch (Exception error)
    {
        Console.Error.WriteLine($"CLIENT_E2E_FAIL {error}");
        Environment.ExitCode = 1;
    }
    return;
}
if (options.SeedStackSlot is int seedStackSlot)
{
    await SeedStackItemAsync(options.AccountFile, seedStackSlot);
    return;
}
if (options.SetAccountBlocked is bool blocked)
{
    await SetAccountBlockedAsync(options.AccountFile, blocked);
    return;
}
if (options.AdversarialPeerAccountFile is not null)
{
    await RunAdversarialItemTradeAsync(options, accountHeader, codec);
    return;
}
if (options.TradeReplayPeerAccountFile is not null)
{
    await RunCompletedTradeReplayAsync(options, accountHeader, codec);
    return;
}
if (options.TradeDeletePeerAccountFile is not null || options.TradeQuitPeerAccountFile is not null)
{
    await RunTradeCancellationAsync(options, accountHeader, codec);
    return;
}

int? expectedPersistedSourceAmount = null;
await using (var session = await E2eSession.ConnectAndLoginAsync(options, accountHeader, codec, ""))
{
    if (options.SplitSlot is int sourceSlot && options.SplitQuantity is int quantity)
    {
        var before = ReadCarry(session.CharacterLoginFrame);
        var source = before[sourceSlot];
        Ensure(source.Index != 0, $"Split source slot {sourceSlot} is empty.");
        Ensure(source.Value1 > quantity, $"Split source slot {sourceSlot} has amount {source.Value1}; quantity {quantity} is not splittable.");
        expectedPersistedSourceAmount = source.Value1 - quantity;
        var destinationSlot = Array.FindIndex(before, item => item.Index == 0);
        Ensure(destinationSlot >= 0, "The character carry has no empty destination slot for split.");

        var splitPayload = new byte[SplitItemRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(splitPayload.AsSpan(0), sourceSlot);
        BinaryPrimitives.WriteInt32LittleEndian(splitPayload.AsSpan(4), source.Index);
        BinaryPrimitives.WriteInt32LittleEndian(splitPayload.AsSpan(8), quantity);
        await SendAsync(session.Stream, codec, SplitItemRequest.MessageType, splitPayload, options.ConnectionId);

        var confirmations = new List<SendItemConfirmation>(capacity: 2);
        for (var index = 0; index < 12 && confirmations.Count < 2; index++)
        {
            var response = await ReadFrameAsync(session.Stream, codec, options.Timeout);
            PrintFrame($"SPLIT_RX[{index}]", response);
            if (response.Header.Type != SendItemConfirmation.MessageType)
                continue;

            Ensure(SendItemConfirmation.TryParse(response, out var confirmation) && confirmation is not null, "MSG_SendItem response failed its wire parser.");
            confirmations.Add(confirmation!);
        }

        Ensure(confirmations.Count == 2, $"Expected two MSG_SendItem responses for split, received {confirmations.Count}.");
        Ensure(confirmations[0].InventoryType == 1 && confirmations[0].Slot == destinationSlot, "Split destination response was not emitted first or used the wrong slot.");
        Ensure(confirmations[0].Item.Index == source.Index && confirmations[0].Item.Value1 == quantity, "Split destination response has the wrong item or amount.");
        Ensure(confirmations[1].InventoryType == 1 && confirmations[1].Slot == sourceSlot, "Split source response was not emitted second or used the wrong slot.");
        Ensure(confirmations[1].Item.Index == source.Index && confirmations[1].Item.Value1 == source.Value1 - quantity, "Split source response has the wrong resulting amount.");
        Console.WriteLine($"SPLIT_PASS source={sourceSlot} destination={destinationSlot} quantity={quantity}");
    }
    else if (options.DeleteSlot is int deleteSlot)
    {
        var before = ReadCarry(session.CharacterLoginFrame);
        var source = before[deleteSlot];
        Ensure(source.Index != 0, $"Delete source slot {deleteSlot} is empty.");
        var deletePayload = new byte[DeleteItemRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(deletePayload.AsSpan(0), deleteSlot);
        BinaryPrimitives.WriteInt32LittleEndian(deletePayload.AsSpan(4), source.Index);
        await SendAsync(session.Stream, codec, DeleteItemRequest.MessageType, deletePayload, options.ConnectionId);
        Console.WriteLine($"DELETE_SENT slot={deleteSlot} item={source.Index}");
        if (options.ExpectDeleteBlocked)
        {
            var noticeReceived = false;
            for (var index = 0; index < 12; index++)
            {
                var response = await ReadFrameAsync(session.Stream, codec, options.Timeout);
                PrintFrame($"DELETE_RX[{index}]", response);
                if (response.Header.Type != MessagePanelConfirmation.MessageType)
                    continue;
                Require(response, MessagePanelConfirmation.MessageType, MessagePanelConfirmation.PacketSize, "blocked delete notice");
                noticeReceived = true;
                break;
            }
            Ensure(noticeReceived, "Blocked DeleteItem did not return a MessagePanel notice.");
        }
    }

    await session.LogoutAsync();
}

if (options.SplitSlot is int persistedSourceSlot && options.SplitQuantity is int persistedQuantity)
{
    await using var persistedSession = await E2eSession.ConnectAndLoginAsync(options, accountHeader, codec, "PERSIST_");
    var after = ReadCarry(persistedSession.CharacterLoginFrame);
    var persistedSource = after[persistedSourceSlot];
    var persistedDestinationSlot = Array.FindIndex(after, item => item.Index == persistedSource.Index && item.Value1 == persistedQuantity);
    Ensure(persistedSource.Value1 == expectedPersistedSourceAmount, $"Persisted split source amount is {persistedSource.Value1}; expected {expectedPersistedSourceAmount}.");
    Ensure(persistedDestinationSlot >= 0, "Persisted split destination stack was not present after relogin.");
    Console.WriteLine($"SPLIT_PERSISTED source={persistedSourceSlot} destination={persistedDestinationSlot} sourceAmount={persistedSource.Value1}");
    await persistedSession.LogoutAsync();
}
else if (options.DeleteSlot is int persistedDeleteSlot)
{
    await using var persistedSession = await E2eSession.ConnectAndLoginAsync(options, accountHeader, codec, "PERSIST_");
    var after = ReadCarry(persistedSession.CharacterLoginFrame);
    if (options.ExpectDeleteBlocked)
    {
        Ensure(after[persistedDeleteSlot].Index != 0, $"Blocked DeleteItem removed carry slot {persistedDeleteSlot}.");
        Console.WriteLine($"DELETE_BLOCKED_PERSISTED slot={persistedDeleteSlot} retained=True");
    }
    else
    {
        Ensure(after[persistedDeleteSlot] == default, $"Deleted item remained in carry slot {persistedDeleteSlot} after relogin.");
        Console.WriteLine($"DELETE_PERSISTED slot={persistedDeleteSlot} empty=True");
    }
    await persistedSession.LogoutAsync();
}

Console.WriteLine(options.SplitSlot is not null
    ? "CLIENT_E2E_PASS account-login character-login split-persisted logout"
    : options.DeleteSlot is not null
        ? "CLIENT_E2E_PASS account-login character-login delete-persisted logout"
        : "CLIENT_E2E_PASS account-login character-login logout");

static async ValueTask RunAdversarialItemTradeAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
{
    var peerFile = options.AdversarialPeerAccountFile ?? throw new InvalidOperationException("Adversarial peer missing.");
    var peerHeader = await ReadAccountHeaderAsync(peerFile);
    var slot = options.DeleteSlot ?? throw new InvalidOperationException("Adversarial carry slot missing.");
    LegacyItem[] actorCarry;
    int actorCoin;
    await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "ABUSE_DELETE_"))
    {
        actorCarry = ReadCarry(actor.CharacterLoginFrame);
        actorCoin = ReadCoin(actor.CharacterLoginFrame);
        var item = actorCarry[slot];
        Ensure(item.Index > 0, "Adversarial fixture requires an occupied carry slot.");
        foreach (var vector in new[] { (Slot: -1, Index: (int)item.Index), (Slot: 60, Index: (int)item.Index),
            (Slot: slot, Index: -1), (Slot: slot, Index: 6500), (Slot: slot, Index: item.Index == 1 ? 2 : 1) })
        {
            var payload = new byte[DeleteItemRequest.PayloadSize];
            BinaryPrimitives.WriteInt32LittleEndian(payload, vector.Slot);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4), vector.Index);
            var frame = codec.Encode(DeleteItemRequest.MessageType, 0, 33, payload, 16);
            Ensure(codec.Decode(frame).IsChecksumValid, "Adversarial delete must have a valid checksum.");
            await actor.Stream.WriteAsync(frame);
            await actor.Stream.WriteAsync(frame);
            Console.WriteLine($"ABUSE_DELETE_SENT slot={vector.Slot} item={vector.Index} repeat=2 checksum=True");
        }
        await actor.LogoutAsync();
    }

    await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "ABUSE_TRADE_"))
    await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "ABUSE_PEER_"))
    {
        Ensure(ReadCarry(actor.CharacterLoginFrame).SequenceEqual(actorCarry) && ReadCoin(actor.CharacterLoginFrame) == actorCoin,
            "Invalid/repeated DeleteItem changed carry or Gold after relogin.");
        Console.WriteLine("ABUSE_DELETE_PASS carry-and-gold-unchanged");
        var peerCarry = ReadCarry(peer.CharacterLoginFrame);
        var peerCoin = ReadCoin(peer.CharacterLoginFrame);
        var actorId = BinaryPrimitives.ReadUInt16LittleEndian(actor.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);
        var peerId = BinaryPrimitives.ReadUInt16LittleEndian(peer.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);
        Ensure(actorId > 0 && peerId > 0 && actorId != peerId, "Adversarial trade requires distinct server IDs.");
        var item = actorCarry[slot];
        var positions = Enumerable.Repeat((sbyte)-1, TradeOfferRequest.ItemCount).ToArray();
        var items = new LegacyItem[TradeOfferRequest.ItemCount];
        positions[0] = checked((sbyte)slot);
        items[0] = item;
        var validOffer = new TradeOfferRequest(items, positions, 0, 0, peerId);
        var alteredItems = items.ToArray();
        alteredItems[0] = item with { Value1 = unchecked((byte)(item.Value1 + 1)) };
        var duplicateItems = items.ToArray();
        duplicateItems[1] = item;
        var duplicatePositions = positions.ToArray();
        duplicatePositions[1] = checked((sbyte)slot);
        var vectors = new[] {
            (Name: "self-target", Offer: validOffer with { OpponentId = actorId }),
            (Name: "absent-target", Offer: validOffer with { OpponentId = ushort.MaxValue }),
            (Name: "negative-gold", Offer: validOffer with { TradeMoney = -1 }),
            (Name: "excess-gold", Offer: validOffer with { TradeMoney = int.MaxValue }),
            (Name: "altered-item", Offer: validOffer with { Items = alteredItems }),
            (Name: "duplicate-slot", Offer: validOffer with { Items = duplicateItems, InventoryPositions = duplicatePositions }) };
        foreach (var vector in vectors)
        {
            var frame = vector.Offer.ToFrame(codec, 33, 16, actorId);
            Ensure(codec.Decode(frame).IsChecksumValid, "Adversarial trade must have a valid checksum.");
            await actor.Stream.WriteAsync(frame);
            await actor.Stream.WriteAsync(frame);
            Console.WriteLine($"ABUSE_TRADE_SENT case={vector.Name} repeat=2 checksum=True");
        }

        var repeatedOffer = validOffer.ToFrame(codec, 33, 16, actorId);
        await actor.Stream.WriteAsync(repeatedOffer);
        await actor.Stream.WriteAsync(repeatedOffer);
        await RequireFrameTypeAsync(peer.Stream, codec, options.Timeout, TradeOfferRequest.MessageType, "ABUSE_OFFER_RX");
        await RequireFrameTypeAsync(peer.Stream, codec, options.Timeout, TradeOfferRequest.MessageType, "ABUSE_REPLAY_OFFER_RX");
        var close = TradeCloseRequest.ToFrame(codec, 33, 16, actorId);
        await actor.Stream.WriteAsync(close);
        await actor.Stream.WriteAsync(close);
        var actorClose = await RequireFrameTypeAsync(actor.Stream, codec, options.Timeout, TradeCloseRequest.MessageType, "ABUSE_ACTOR_CLOSE_RX");
        var peerClose = await RequireFrameTypeAsync(peer.Stream, codec, options.Timeout, TradeCloseRequest.MessageType, "ABUSE_PEER_CLOSE_RX");
        Require(actorClose, TradeCloseRequest.MessageType, TradeCloseRequest.PacketSize, "adversarial actor close");
        Require(peerClose, TradeCloseRequest.MessageType, TradeCloseRequest.PacketSize, "adversarial peer close");
        Ensure(actorClose.Header.Id == actorId && peerClose.Header.Id == peerId, "Adversarial close used a wrong recipient ID.");
        await actor.LogoutAsync();
        await peer.LogoutAsync();
        await using var persistedActor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "ABUSE_PERSIST_ACTOR_");
        await using var persistedPeer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "ABUSE_PERSIST_PEER_");
        Ensure(ReadCarry(persistedActor.CharacterLoginFrame).SequenceEqual(actorCarry) && ReadCoin(persistedActor.CharacterLoginFrame) == actorCoin,
            "Altered/repeated trade changed actor carry or Gold after relogin.");
        Ensure(ReadCarry(persistedPeer.CharacterLoginFrame).SequenceEqual(peerCarry) && ReadCoin(persistedPeer.CharacterLoginFrame) == peerCoin,
            "Altered/repeated trade changed peer carry or Gold after relogin.");
        await persistedActor.LogoutAsync();
        await persistedPeer.LogoutAsync();
    }
    Console.WriteLine("CLIENT_E2E_PASS adversarial-delete-trade-replay carry-and-gold-unchanged");
}

static async ValueTask RunCompletedTradeReplayAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
{
    var peerFile = options.TradeReplayPeerAccountFile ?? throw new InvalidOperationException("Replay peer missing.");
    var peerHeader = await ReadAccountHeaderAsync(peerFile);
    var slot = options.DeleteSlot ?? 0;
    LegacyItem[] expectedActor;
    LegacyItem[] expectedPeer;
    int expectedActorCoin;
    int expectedPeerCoin;
    await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "REPLAY_ACTOR_"))
    await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "REPLAY_PEER_"))
    {
        expectedActor = ReadCarry(actor.CharacterLoginFrame);
        expectedPeer = ReadCarry(peer.CharacterLoginFrame);
        var item = expectedActor[slot];
        Ensure(options.TradeGoldOnly || item.Index > 0, "Replay fixture requires an occupied actor slot.");
        var destination = Array.FindIndex(expectedPeer, 0, 60, value => value.Index == 0);
        Ensure(options.TradeGoldOnly || destination >= 0, "Replay peer has no free carry slot.");
        var actorCoin = ReadCoin(actor.CharacterLoginFrame);
        var peerCoin = ReadCoin(peer.CharacterLoginFrame);
        Ensure(peerCoin >= 1 && actorCoin < 2_000_000_000, "Replay fixture requires peer Gold and actor balance below cap.");
        expectedActorCoin = actorCoin + 1;
        expectedPeerCoin = peerCoin - 1;
        if (!options.TradeGoldOnly)
        {
            expectedActor[slot] = default;
            expectedPeer[destination] = item;
        }
        var actorId = BinaryPrimitives.ReadUInt16LittleEndian(actor.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);
        var peerId = BinaryPrimitives.ReadUInt16LittleEndian(peer.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);
        Ensure(actorId > 0 && peerId > 0 && actorId != peerId, "Replay requires distinct server IDs.");
        var positions = Enumerable.Repeat((sbyte)-1, TradeOfferRequest.ItemCount).ToArray();
        var items = new LegacyItem[TradeOfferRequest.ItemCount];
        if (!options.TradeGoldOnly)
        {
            positions[0] = checked((sbyte)slot);
            items[0] = item;
        }
        var actorOffer = new TradeOfferRequest(items, positions, 0, 0, peerId);
        var peerOffer = new TradeOfferRequest(new LegacyItem[TradeOfferRequest.ItemCount],
            Enumerable.Repeat((sbyte)-1, TradeOfferRequest.ItemCount).ToArray(), 1, 0, actorId);
        await actor.Stream.WriteAsync(actorOffer.ToFrame(codec, 33, 16, actorId));
        var offerRx = await RequireFrameTypeAsync(peer.Stream, codec, options.Timeout, TradeOfferRequest.MessageType, "REPLAY_OPEN_RX");
        Require(offerRx, TradeOfferRequest.MessageType, TradeOfferRequest.PacketSize, "trade open");
        await peer.Stream.WriteAsync(peerOffer.ToFrame(codec, 33, 16, peerId));
        offerRx = await RequireFrameTypeAsync(actor.Stream, codec, options.Timeout, TradeOfferRequest.MessageType, "REPLAY_PEER_OFFER_RX");
        Require(offerRx, TradeOfferRequest.MessageType, TradeOfferRequest.PacketSize, "peer offer");
        var actorCheck = (actorOffer with { MyCheck = 1 }).ToFrame(codec, 34, 17, actorId);
        var peerCheck = (peerOffer with { MyCheck = 1 }).ToFrame(codec, 35, 18, peerId);
        await actor.Stream.WriteAsync(actorCheck);
        var checkRx = await RequireFrameTypeAsync(actor.Stream, codec, options.Timeout, TradeCheckConfirmation.MessageType, "REPLAY_CHECK_RX");
        Require(checkRx, TradeCheckConfirmation.MessageType, PacketHeader.SizeInBytes, "trade check");
        await peer.Stream.WriteAsync(peerCheck);
        AssertCarryRefresh(await RequireFrameTypeAsync(actor.Stream, codec, options.Timeout, UpdateCarryConfirmation.MessageType, "REPLAY_ACTOR_COMMIT_RX"), actorId, expectedActor, expectedActorCoin);
        AssertCarryRefresh(await RequireFrameTypeAsync(peer.Stream, codec, options.Timeout, UpdateCarryConfirmation.MessageType, "REPLAY_PEER_COMMIT_RX"), peerId, expectedPeer, expectedPeerCoin);
        await AssertPersistedTradeAsync(options.AccountFile, options.Slot, expectedActor, expectedActorCoin);
        await AssertPersistedTradeAsync(peerFile, options.Slot, expectedPeer, expectedPeerCoin);
        Console.WriteLine(options.TradeGoldOnly ? "TRADE_COMMIT_PASS goldOnly=True gold=1"
            : $"TRADE_COMMIT_PASS source={slot} destination={destination} gold=1");

        // Exact encoded frames (same tick/key/checksum), not freshly built offers.
        Ensure(codec.Decode(actorCheck).IsChecksumValid && codec.Decode(peerCheck).IsChecksumValid, "Replay checksums are invalid.");
        if (options.TradeGoldOnly)
        {
            await actor.Stream.WriteAsync(actorCheck);
            await TradeProcessingBarrierAsync(actor.Stream, peer.Stream, codec, options.Timeout, actorId);
            await peer.Stream.WriteAsync(peerCheck);
            await TradeProcessingBarrierAsync(peer.Stream, actor.Stream, codec, options.Timeout, peerId);
            await actor.Stream.WriteAsync(actorCheck);
            await TradeProcessingBarrierAsync(actor.Stream, peer.Stream, codec, options.Timeout, actorId);
            await AssertPersistedTradeAsync(options.AccountFile, options.Slot, expectedActor, expectedActorCoin);
            await AssertPersistedTradeAsync(peerFile, options.Slot, expectedPeer, expectedPeerCoin);
            Console.WriteLine("GOLD_REPLAY_PASS single-transfer-after-ordered-checks");

            // A genuinely new unchecked offer pair must still work in this session.
            await actor.Stream.WriteAsync(actorOffer.ToFrame(codec, 40, 20, actorId));
            Require(await RequireFrameTypeAsync(peer.Stream, codec, options.Timeout, TradeOfferRequest.MessageType, "GOLD_NEW_OPEN_RX"),
                TradeOfferRequest.MessageType, TradeOfferRequest.PacketSize, "new gold trade open");
            await peer.Stream.WriteAsync(peerOffer.ToFrame(codec, 41, 21, peerId));
            Require(await RequireFrameTypeAsync(actor.Stream, codec, options.Timeout, TradeOfferRequest.MessageType, "GOLD_NEW_PEER_RX"),
                TradeOfferRequest.MessageType, TradeOfferRequest.PacketSize, "new gold peer offer");
            await actor.Stream.WriteAsync((actorOffer with { MyCheck = 1 }).ToFrame(codec, 42, 22, actorId));
            Require(await RequireFrameTypeAsync(actor.Stream, codec, options.Timeout, TradeCheckConfirmation.MessageType, "GOLD_NEW_CHECK_RX"),
                TradeCheckConfirmation.MessageType, PacketHeader.SizeInBytes, "new gold check");
            await peer.Stream.WriteAsync((peerOffer with { MyCheck = 1 }).ToFrame(codec, 43, 23, peerId));
            expectedActorCoin++;
            expectedPeerCoin--;
            AssertCarryRefresh(await RequireFrameTypeAsync(actor.Stream, codec, options.Timeout, UpdateCarryConfirmation.MessageType, "GOLD_NEW_ACTOR_RX"), actorId, expectedActor, expectedActorCoin);
            AssertCarryRefresh(await RequireFrameTypeAsync(peer.Stream, codec, options.Timeout, UpdateCarryConfirmation.MessageType, "GOLD_NEW_PEER_COMMIT_RX"), peerId, expectedPeer, expectedPeerCoin);
            await AssertPersistedTradeAsync(options.AccountFile, options.Slot, expectedActor, expectedActorCoin);
            await AssertPersistedTradeAsync(peerFile, options.Slot, expectedPeer, expectedPeerCoin);
            Console.WriteLine("GOLD_NEW_TRADE_PASS same-session second-intentional-transfer");
        }
        else for (var repeat = 0; repeat < 2; repeat++)
        {
            await actor.Stream.WriteAsync(actorCheck);
            await peer.Stream.WriteAsync(peerCheck);
        }
        // Per-connection TCP ordering makes logout a processing barrier for its replays.
        await actor.LogoutAsync();
        await peer.LogoutAsync();
    }
    await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "REPLAY_PERSIST_ACTOR_"))
    await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "REPLAY_PERSIST_PEER_"))
    {
        Console.WriteLine($"REPLAY_PERSIST_GOLD actor={ReadCoin(actor.CharacterLoginFrame)} expectedActor={expectedActorCoin} peer={ReadCoin(peer.CharacterLoginFrame)} expectedPeer={expectedPeerCoin}");
        Ensure(ReadCarry(actor.CharacterLoginFrame).SequenceEqual(expectedActor) && ReadCoin(actor.CharacterLoginFrame) == expectedActorCoin,
            "Completed-trade replay changed actor carry or Gold beyond one transfer.");
        Ensure(ReadCarry(peer.CharacterLoginFrame).SequenceEqual(expectedPeer) && ReadCoin(peer.CharacterLoginFrame) == expectedPeerCoin,
            "Completed-trade replay changed peer carry or Gold beyond one transfer.");
        await actor.LogoutAsync();
        await peer.LogoutAsync();
    }
    Console.WriteLine(options.TradeGoldOnly
        ? "CLIENT_E2E_PASS gold-only-replay blocked new-trade-accepted persisted-both"
        : "CLIENT_E2E_PASS completed-trade-replay single-item-and-gold-transfer persisted-both");
}

static async ValueTask TradeProcessingBarrierAsync(NetworkStream sender, NetworkStream recipient, LegacyFrameCodec codec, TimeSpan timeout, ushort id)
{
    // Chat follows the check on its TCP stream; its relay proves the check ran.
    const string marker = "REPLAY-BARRIER";
    await sender.WriteAsync(new MessageChatConfirmation(marker).ToFrame(codec, 36, 19, id));
    var response = await RequireFrameTypeAsync(recipient, codec, timeout, MessageChatRequest.MessageType, "REPLAY_BARRIER_RX");
    Require(response, MessageChatRequest.MessageType, MessageChatRequest.PacketSize, "replay processing barrier");
    Ensure(response.Header.Id == id && ReadCString(response.Payload.Span) == marker, "Replay chat barrier has the wrong sender or marker.");
}

static void AssertCarryRefresh(DecodedFrame frame, ushort id, LegacyItem[] expected, int coin)
{
    Require(frame, UpdateCarryConfirmation.MessageType, UpdateCarryConfirmation.PacketSize, "trade carry refresh");
    Ensure(frame.Header.Id == id, "Trade carry refresh used the wrong recipient ID.");
    var carry = new LegacyItem[UpdateCarryConfirmation.CarryCount];
    for (var index = 0; index < carry.Length; index++)
        carry[index] = LegacyItem.Read(frame.Payload.Span.Slice(index * LegacyItem.SizeInBytes, LegacyItem.SizeInBytes));
    Ensure(carry.SequenceEqual(expected) && BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[UpdateCarryConfirmation.CoinOffset..]) == coin,
        "Trade carry refresh differs from the single-transfer expectation.");
}

static async ValueTask AssertPersistedTradeAsync(string path, int characterSlot, LegacyItem[] expected, int coin)
{
    // Measured W2PP storage ABI, independent of the server's trade plan builder.
    var account = await File.ReadAllBytesAsync(path);
    var mobOffset = 216 + characterSlot * 816;
    var carry = new LegacyItem[64];
    for (var index = 0; index < carry.Length; index++)
        carry[index] = LegacyItem.Read(account.AsSpan(mobOffset + 268 + index * 8, 8));
    Ensure(carry.SequenceEqual(expected) && BinaryPrimitives.ReadInt32LittleEndian(account.AsSpan(mobOffset + 28)) == coin,
        "Trade carry relay preceded durable single-transfer persistence.");
    Console.WriteLine("TRADE_DURABLE_PASS before-logout");
}

static int ReadCoin(DecodedFrame frame) => BinaryPrimitives.ReadInt32LittleEndian(
    frame.Payload.Span[(CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CoinOffset)..]);

static async ValueTask RunTradeCancellationAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
{
    var quitTrade = options.TradeQuitPeerAccountFile is not null;
    var peerFile = options.TradeQuitPeerAccountFile ?? options.TradeDeletePeerAccountFile ?? throw new InvalidOperationException("Trade peer account file missing.");
    if (options.TradeQuitPkActive)
    {
        // W2PP KILL_MARK is carry slot 63, effect 1. Parse permits isolated copies only.
        var callerFile = options.TradeQuitDead ? peerFile : options.AccountFile;
        var bytes = await File.ReadAllBytesAsync(callerFile);
        Ensure(bytes.Length >= 7945, "PK fixture is shorter than the W2PP account blob.");
        bytes[216 + options.Slot * 816 + 268 + 63 * 8 + 4] = 8;
        await File.WriteAllBytesAsync(callerFile, bytes);
    }
    if (options.TradeQuitDead)
    {
        // W2PP blob: first MOB + CurrentScore + Hp. Only isolated copies pass Parse.
        var bytes = await File.ReadAllBytesAsync(options.AccountFile);
        Ensure(bytes.Length >= 7945, "Dead trade fixture is shorter than the W2PP account blob.");
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(216 + options.Slot * 816 + 92 + 24),
            options.TradeQuitKilled ? 1 : options.TradeQuitNegativeHp ? -1 : 0);
        await File.WriteAllBytesAsync(options.AccountFile, bytes);
        if (options.TradeQuitKilled)
        {
            var attackerBytes = await File.ReadAllBytesAsync(peerFile);
            Ensure(attackerBytes.Length >= 7945, "Attack fixture is shorter than the W2PP account blob.");
            var mobOffset = 216 + options.Slot * 816;
            BinaryPrimitives.WriteInt32LittleEndian(attackerBytes.AsSpan(mobOffset + 92 + 8), 10000);
            var learned = BinaryPrimitives.ReadUInt32LittleEndian(attackerBytes.AsSpan(mobOffset + 780));
            BinaryPrimitives.WriteUInt32LittleEndian(attackerBytes.AsSpan(mobOffset + 780), learned | 0x80000000u);
            await File.WriteAllBytesAsync(peerFile, attackerBytes);
        }
    }
    var peerHeader = await ReadAccountHeaderAsync(peerFile);
    await using var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "TRADE_ACTOR_");
    await using var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "TRADE_PEER_");
    var actorId = BinaryPrimitives.ReadUInt16LittleEndian(actor.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);
    var peerId = BinaryPrimitives.ReadUInt16LittleEndian(peer.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);
    Ensure(actorId > 0 && peerId > 0 && actorId != peerId, "Trade participants did not receive distinct live server IDs.");
    var slot = options.DeleteSlot ?? throw new InvalidOperationException("Trade delete slot missing.");
    var item = ReadCarry(actor.CharacterLoginFrame)[slot];
    Ensure(item.Index != 0, "Trade delete fixture item is absent.");
    var actorCarry = ReadCarry(actor.CharacterLoginFrame);
    var peerCarry = ReadCarry(peer.CharacterLoginFrame);
    var actorCoin = ReadCoin(actor.CharacterLoginFrame);
    var peerCoin = ReadCoin(peer.CharacterLoginFrame);

    var trade = new TradeOfferRequest(new LegacyItem[TradeOfferRequest.ItemCount],
        Enumerable.Repeat((sbyte)-1, TradeOfferRequest.ItemCount).ToArray(), 0, 0, options.TradeQuitDead ? actorId : peerId);
    if (options.TradeQuitDead)
    {
        var hpOffset = CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + 24;
        Ensure(BinaryPrimitives.ReadInt32LittleEndian(actor.CharacterLoginFrame.Payload.Span[hpOffset..]) ==
            (options.TradeQuitKilled ? 1 : options.TradeQuitNegativeHp ? -1 : 0),
            "Dead QuitTrade fixture did not enter the world with the expected HP.");
        if (options.TradeQuitKilled)
        {
            // Stage both sides while alive, without either confirmation bit.
            var offeredItems = new LegacyItem[TradeOfferRequest.ItemCount];
            var offeredPositions = Enumerable.Repeat((sbyte)-1, TradeOfferRequest.ItemCount).ToArray();
            offeredItems[0] = item;
            offeredPositions[0] = checked((sbyte)slot);
            var actorOffer = new TradeOfferRequest(offeredItems, offeredPositions, 0, 0, peerId);
            await actor.Stream.WriteAsync(actorOffer.ToFrame(codec, 32, 15, actorId));
            await RequireFrameTypeAsync(peer.Stream, codec, options.Timeout, TradeOfferRequest.MessageType, "KILL_TRADE_ACTOR_OFFER_RX");
            Ensure(peerCoin >= 1, "Trade killer must have at least one Gold to offer.");
            trade = trade with { TradeMoney = 1 };
        }
        await peer.Stream.WriteAsync(trade.ToFrame(codec, 33, 16, peerId));
        await RequireFrameTypeAsync(actor.Stream, codec, options.Timeout, TradeOfferRequest.MessageType, "DEAD_TRADE_OFFER_RX");
        if (options.TradeQuitKilled)
        {
            var peerHpOffset = CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + 24;
            Ensure(BinaryPrimitives.ReadInt32LittleEndian(peer.CharacterLoginFrame.Payload.Span[peerHpOffset..]) > 0, "Trade killer must be alive.");
            var attackPayload = new byte[56]; // MSG_AttackOne: frame 68, header 12.
            // Positions, attacker, skill 103 and first damage slot from x86 wire layout.
            peer.CharacterLoginFrame.Payload.Span[..4].CopyTo(attackPayload.AsSpan(22));
            actor.CharacterLoginFrame.Payload.Span[..4].CopyTo(attackPayload.AsSpan(26));
            BinaryPrimitives.WriteUInt16LittleEndian(attackPayload.AsSpan(30), peerId);
            BinaryPrimitives.WriteInt16LittleEndian(attackPayload.AsSpan(44), 103);
            BinaryPrimitives.WriteUInt16LittleEndian(attackPayload.AsSpan(48), actorId);
            BinaryPrimitives.WriteInt32LittleEndian(attackPayload.AsSpan(52), -2); // Physical intent; not a damage value.
            await peer.Stream.WriteAsync(codec.Encode(AttackRequest.OneTargetMessageType, peerId,
                unchecked((uint)Environment.TickCount64), attackPayload, 22));
            var killed = await RequireUncancelledFrameAsync(actor.Stream, codec, options.Timeout, SetHpMpConfirmation.MessageType, "KILL_TRADE_HP_RX");
            Require(killed, SetHpMpConfirmation.MessageType, 28, "authoritative combat death");
            Ensure(killed.Header.Id == actorId && BinaryPrimitives.ReadInt32LittleEndian(killed.Payload.Span) == 0,
                "Real attack did not reduce the trading actor to HP zero.");
            Console.WriteLine("KILL_DURING_TRADE_PASS alive-bilateral-to-authoritative-hp-zero");
        }
    }
    else
    {
        await actor.Stream.WriteAsync(trade.ToFrame(codec, 33, 16, actorId));
        await RequireFrameTypeAsync(peer.Stream, codec, options.Timeout, TradeOfferRequest.MessageType, "TRADE_OFFER_RX");
    }

    if (options.TradeQuitDead || options.TradeQuitPk)
    {
        // Drain each stream's initial login relays before observing QuitTrade.
        // Inbound offers drain only the actor, leaving peer's initial PK queued.
        const string readyMarker = "QUIT-READY-BARRIER";
        await actor.Stream.WriteAsync(new MessageChatConfirmation(readyMarker).ToFrame(codec, 35, 18, actorId));
        var peerReady = await RequireFrameTypeAsync(peer.Stream, codec, options.Timeout, MessageChatRequest.MessageType, "QUIT_READY_PEER_RX");
        Require(peerReady, MessageChatRequest.MessageType, MessageChatRequest.PacketSize, "pre-quit peer barrier");
        Ensure(peerReady.Header.Id == actorId && ReadCString(peerReady.Payload.Span) == readyMarker, "Wrong pre-quit peer barrier.");
        await peer.Stream.WriteAsync(new MessageChatConfirmation(readyMarker).ToFrame(codec, 36, 19, peerId));
        var actorReady = await RequireFrameTypeAsync(actor.Stream, codec, options.Timeout, MessageChatRequest.MessageType, "QUIT_READY_ACTOR_RX");
        Require(actorReady, MessageChatRequest.MessageType, MessageChatRequest.PacketSize, "pre-quit actor barrier");
        Ensure(actorReady.Header.Id == peerId && ReadCString(actorReady.Payload.Span) == readyMarker, "Wrong pre-quit actor barrier.");
    }
    if (quitTrade)
        await SendAsync(actor.Stream, codec, TradeCloseRequest.MessageType, Array.Empty<byte>(), actorId);
    else
    {
        var deletePayload = new byte[DeleteItemRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(deletePayload.AsSpan(0), slot);
        BinaryPrimitives.WriteInt32LittleEndian(deletePayload.AsSpan(4), item.Index);
        await SendAsync(actor.Stream, codec, DeleteItemRequest.MessageType, deletePayload, actorId);
    }
    if (options.TradeQuitDead)
    {
        var hpMode = await RequireUncancelledFrameAsync(actor.Stream, codec, options.Timeout, SetHpModeConfirmation.MessageType, "DEAD_QUIT_HP_RX");
        Require(hpMode, SetHpModeConfirmation.MessageType, 20, "dead QuitTrade HP mode");
        Ensure(hpMode.Header.Id == actorId && BinaryPrimitives.ReadInt32LittleEndian(hpMode.Payload.Span) ==
            (options.TradeQuitNegativeHp ? -1 : 0) &&
            BinaryPrimitives.ReadInt16LittleEndian(hpMode.Payload.Span[4..]) == 22,
            "Dead QuitTrade returned incorrect HP, USER_PLAY mode or recipient.");
        // Both streams must reach a chat barrier without any cancellation signal.
        const string marker = "DEAD-QUIT-BARRIER";
        await actor.Stream.WriteAsync(new MessageChatConfirmation(marker).ToFrame(codec, 37, 20, actorId));
        var peerBarrier = await RequireUncancelledFrameAsync(peer.Stream, codec, options.Timeout, MessageChatRequest.MessageType, "DEAD_QUIT_PEER_BARRIER_RX");
        Require(peerBarrier, MessageChatRequest.MessageType, 140, "dead-quit peer processing barrier");
        Ensure(peerBarrier.Header.Id == actorId && ReadCString(peerBarrier.Payload.Span) == marker, "Wrong dead-quit peer barrier.");
        await peer.Stream.WriteAsync(new MessageChatConfirmation(marker).ToFrame(codec, 38, 21, peerId));
        var actorBarrier = await RequireUncancelledFrameAsync(actor.Stream, codec, options.Timeout, MessageChatRequest.MessageType, "DEAD_QUIT_ACTOR_BARRIER_RX");
        Require(actorBarrier, MessageChatRequest.MessageType, 140, "dead-quit actor processing barrier");
        Ensure(actorBarrier.Header.Id == peerId && ReadCString(actorBarrier.Payload.Span) == marker, "Wrong dead-quit actor barrier.");
        await AssertPersistedTradeAsync(options.AccountFile, options.Slot, actorCarry, actorCoin);
        await AssertPersistedTradeAsync(peerFile, options.Slot, peerCarry, peerCoin);
        // This live-side close must still find the staged pair and signal both sides.
        await SendAsync(peer.Stream, codec, TradeCloseRequest.MessageType, Array.Empty<byte>(), peerId);
    }
    var actorCancel = await RequireFrameTypeAsync(actor.Stream, codec, options.Timeout, TradeCloseRequest.MessageType, "TRADE_ACTOR_CANCEL_RX");
    var peerCancel = await RequireFrameTypeAsync(peer.Stream, codec, options.Timeout, TradeCloseRequest.MessageType, "TRADE_PEER_CANCEL_RX");
    Require(actorCancel, TradeCloseRequest.MessageType, TradeCloseRequest.PacketSize, "actor trade cancel");
    Require(peerCancel, TradeCloseRequest.MessageType, TradeCloseRequest.PacketSize, "peer trade cancel");
    Ensure(actorCancel.Header.Id == actorId && peerCancel.Header.Id == peerId, "Trade cancellation signal used the wrong recipient ID.");
    if (options.TradeQuitPk)
    {
        var callerId = options.TradeQuitDead ? peerId : actorId;
        var expectedPkState = options.TradeQuitPkActive ? 1 : 0;
        var actorPk = await RequireFrameTypeAsync(actor.Stream, codec, options.Timeout, PkInfoConfirmation.MessageType, "TRADE_ACTOR_PK_RX");
        var peerPk = await RequireFrameTypeAsync(peer.Stream, codec, options.Timeout, PkInfoConfirmation.MessageType, "TRADE_PEER_PK_RX");
        foreach (var pk in new[] { actorPk, peerPk })
        {
            Require(pk, PkInfoConfirmation.MessageType, 16, "trade-close PK refresh");
            var actualPkState = BinaryPrimitives.ReadInt32LittleEndian(pk.Payload.Span);
            Ensure(pk.Header.Id == callerId && actualPkState == expectedPkState,
                $"Trade-close PK refresh has the wrong subject or state: expected={expectedPkState}, actual={actualPkState}.");
        }
        Console.WriteLine($"TRADE_PK_PASS caller-and-neighbor state={expectedPkState}");
        await AssertPersistedTradeAsync(options.AccountFile, options.Slot, actorCarry, actorCoin);
        await AssertPersistedTradeAsync(peerFile, options.Slot, peerCarry, peerCoin);
    }
    if (options.TradeQuitIdle)
    {
        for (var replay = 0; replay < 2; replay++)
        {
            await SendAsync(actor.Stream, codec, TradeCloseRequest.MessageType, Array.Empty<byte>(), actorId);
            var idleCancel = await ReadFrameAsync(actor.Stream, codec, options.Timeout);
            Require(idleCancel, TradeCloseRequest.MessageType, 12, "idle QuitTrade own signal");
            Ensure(idleCancel.Header.Id == actorId, "Idle QuitTrade signal used the wrong recipient.");
            foreach (var stream in new[] { actor.Stream, peer.Stream })
            {
                var idlePk = await ReadFrameAsync(stream, codec, options.Timeout);
                Require(idlePk, PkInfoConfirmation.MessageType, 16, "idle QuitTrade PK refresh");
                Ensure(idlePk.Header.Id == actorId && BinaryPrimitives.ReadInt32LittleEndian(idlePk.Payload.Span) ==
                    (options.TradeQuitPkActive ? 1 : 0), "Idle QuitTrade PK used wrong subject/state.");
            }
            // No signal to the former peer and no duplicate refresh may remain queued.
            var marker = $"IDLE-QUIT-{replay}";
            await actor.Stream.WriteAsync(new MessageChatConfirmation(marker).ToFrame(codec, 40, 23, actorId));
            var peerBarrier = await RequireUncancelledFrameAsync(peer.Stream, codec, options.Timeout, MessageChatRequest.MessageType, "IDLE_QUIT_PEER_BARRIER_RX");
            Ensure(peerBarrier.Header.Id == actorId && ReadCString(peerBarrier.Payload.Span) == marker, "Wrong idle peer barrier.");
            await peer.Stream.WriteAsync(new MessageChatConfirmation(marker).ToFrame(codec, 41, 24, peerId));
            var actorBarrier = await RequireUncancelledFrameAsync(actor.Stream, codec, options.Timeout, MessageChatRequest.MessageType, "IDLE_QUIT_ACTOR_BARRIER_RX");
            Ensure(actorBarrier.Header.Id == peerId && ReadCString(actorBarrier.Payload.Span) == marker, "Wrong idle actor barrier.");
        }
        await AssertPersistedTradeAsync(options.AccountFile, options.Slot, actorCarry, actorCoin);
        await AssertPersistedTradeAsync(peerFile, options.Slot, peerCarry, peerCoin);
        Console.WriteLine("IDLE_QUIT_PASS repeated-own-signal-and-neighbor-pk-no-peer-cancel");
    }
    await actor.LogoutAsync();
    await peer.LogoutAsync();

    await using var persisted = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "TRADE_PERSIST_");
    Ensure(ReadCarry(persisted.CharacterLoginFrame)[slot] == item, "Trade cancellation mutated the carry item after relogin.");
    await persisted.LogoutAsync();
    if (options.TradeQuitDead || options.TradeQuitPk)
    {
        Ensure(ReadCarry(persisted.CharacterLoginFrame).SequenceEqual(actorCarry) && ReadCoin(persisted.CharacterLoginFrame) == actorCoin,
            "QuitTrade mutated the actor carry or Gold after relogin.");
        await using var persistedPeer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "DEAD_QUIT_PERSIST_PEER_");
        Ensure(ReadCarry(persistedPeer.CharacterLoginFrame).SequenceEqual(peerCarry) && ReadCoin(persistedPeer.CharacterLoginFrame) == peerCoin,
            "QuitTrade mutated the peer carry or Gold after relogin.");
        await persistedPeer.LogoutAsync();
        Console.WriteLine(!options.TradeQuitDead
            ? "CLIENT_E2E_PASS trade-quit-pk carry-and-gold-unchanged"
            : options.TradeQuitNegativeHp
            ? "CLIENT_E2E_PASS negative-hp-quit-trade hp-minus-one pair-preserved carry-and-gold-unchanged"
            : options.TradeQuitKilled
            ? "CLIENT_E2E_PASS killed-during-trade hp-mode pair-preserved carry-and-gold-unchanged"
            : "CLIENT_E2E_PASS dead-quit-trade hp-mode pair-preserved carry-and-gold-unchanged");
        return;
    }
    Console.WriteLine(quitTrade
        ? "CLIENT_E2E_PASS trade-quit-cancel-both item-retained"
        : "CLIENT_E2E_PASS trade-delete-cancel-both item-retained");
}

static async ValueTask<DecodedFrame> RequireUncancelledFrameAsync(NetworkStream stream, LegacyFrameCodec codec, TimeSpan timeout, ushort type, string label)
{
    for (var index = 0; index < 16; index++)
    {
        var frame = await ReadFrameAsync(stream, codec, timeout);
        PrintFrame($"{label}[{index}]", frame);
        Ensure(frame.Header.Type != TradeCloseRequest.MessageType, "Dead QuitTrade emitted a bilateral cancellation signal.");
        if (type != SetHpMpConfirmation.MessageType)
            Ensure(frame.Header.Type != PkInfoConfirmation.MessageType, "Dead QuitTrade emitted a PK refresh.");
        if (frame.Header.Type == type) return frame;
    }
    throw new InvalidOperationException($"Expected {label} type=0x{type:X4} was not received.");
}

static async ValueTask<DecodedFrame> RequireFrameTypeAsync(NetworkStream stream, LegacyFrameCodec codec, TimeSpan timeout, ushort type, string label)
{
    for (var index = 0; index < 12; index++)
    {
        var response = await ReadFrameAsync(stream, codec, timeout);
        PrintFrame($"{label}[{index}]", response);
        if (response.Header.Type == type)
            return response;
    }
    throw new InvalidOperationException($"Expected {label} type=0x{type:X4} was not received.");
}

static LegacyItem[] ReadCarry(DecodedFrame frame)
{
    Require(frame, CharacterLoginConfirmationV769.MessageType, CharacterLoginConfirmationV769.PacketSize, "character login confirmation");
    var mob = frame.Payload.Span.Slice(CharacterLoginConfirmationV769.MobOffset, CharacterLoginConfirmationV769.MobSize);
    var carry = new LegacyItem[CharacterMobV769.CarryCount];
    for (var index = 0; index < carry.Length; index++)
        carry[index] = LegacyItem.Read(mob.Slice(CharacterMobV769.CarryOffset + (index * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
    return carry;
}

static async ValueTask SeedStackItemAsync(string path, int slot)
{
    const int characterOffset = 216;
    const int carryOffset = 268;
    const short itemIndex = 413;
    const byte amount = 10;
    var bytes = await File.ReadAllBytesAsync(path);
    var itemOffset = characterOffset + carryOffset + (slot * LegacyItem.SizeInBytes);
    Ensure(itemOffset + LegacyItem.SizeInBytes <= bytes.Length, "The account fixture is shorter than the requested legacy carry slot.");
    Ensure(BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(itemOffset)) == 0, $"Legacy carry slot {slot} is not empty; fixture was not changed.");
    BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(itemOffset), itemIndex);
    bytes[itemOffset + 2] = 61;
    bytes[itemOffset + 3] = amount;
    bytes.AsSpan(itemOffset + 4, LegacyItem.SizeInBytes - 4).Clear();
    await File.WriteAllBytesAsync(path, bytes);
    Console.WriteLine($"SPLIT_FIXTURE_READY slot={slot} item={itemIndex} amount={amount}");
}

static async ValueTask SetAccountBlockedAsync(string path, bool blocked)
{
    const int blockedOffset = 7944;
    Ensure(Path.GetFileName(path).Equals("TESTE2", StringComparison.OrdinalIgnoreCase) &&
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Contains("E2E7670", StringComparer.OrdinalIgnoreCase),
        "Block-state fixture mode is restricted to the local E2E7670/TESTE2 sandbox account.");
    var bytes = await File.ReadAllBytesAsync(path);
    Ensure(bytes.Length > blockedOffset, "The sandbox account fixture is shorter than the block-state offset.");
    bytes[blockedOffset] = blocked ? (byte)1 : (byte)0;
    await File.WriteAllBytesAsync(path, bytes);
    Console.WriteLine($"ACCOUNT_FIXTURE_BLOCKED={blocked}");
}

static async ValueTask<AccountHeader> ReadAccountHeaderAsync(string path)
{
    var header = new byte[LegacyAccountSnapshotLayout.NumericTokenOffset + LegacyAccountSnapshotLayout.NumericTokenLength];
    await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, header.Length, FileOptions.SequentialScan);
    await file.ReadExactlyAsync(header);
    return new AccountHeader(
        ReadCString(header.AsSpan(0, 16)),
        ReadCString(header.AsSpan(16, 12)),
        header.AsSpan(LegacyAccountSnapshotLayout.NumericTokenOffset, LegacyAccountSnapshotLayout.NumericTokenLength).ToArray());
}

static async ValueTask SendAsync(NetworkStream stream, LegacyFrameCodec codec, ushort type, byte[] payload, ushort id)
{
    var frame = codec.Encode(type, id, 33, payload, 16);
    await stream.WriteAsync(frame);
}

static async ValueTask<DecodedFrame> ReadFrameAsync(NetworkStream stream, LegacyFrameCodec codec, TimeSpan timeout)
{
    using var cancellation = new CancellationTokenSource(timeout);
    var headerBytes = new byte[PacketHeader.SizeInBytes];
    await stream.ReadExactlyAsync(headerBytes, cancellation.Token);
    var header = PacketHeader.Read(headerBytes);
    if (header.Size < PacketHeader.SizeInBytes || header.Size > PacketHeader.MaxPacketSize)
        throw new InvalidDataException($"Invalid response frame size {header.Size}.");

    var frame = new byte[header.Size];
    headerBytes.CopyTo(frame, 0);
    await stream.ReadExactlyAsync(frame.AsMemory(PacketHeader.SizeInBytes), cancellation.Token);
    return codec.Decode(frame);
}

static void PrintFrame(string label, DecodedFrame frame) =>
    Console.WriteLine($"{label} type=0x{frame.Header.Type:X4} size={frame.Header.Size} checksum={frame.IsChecksumValid}");

static void Require(DecodedFrame frame, ushort type, int size, string label)
{
    Ensure(frame.IsChecksumValid, $"{label} has an invalid checksum.");
    Ensure(frame.Header.Type == type, $"Expected {label} type=0x{type:X4}, received 0x{frame.Header.Type:X4}.");
    Ensure(frame.Header.Size == size, $"Expected {label} size={size}, received {frame.Header.Size}.");
}

static void Ensure(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static string ReadCString(ReadOnlySpan<byte> source)
{
    var length = source.IndexOf((byte)0);
    return Encoding.ASCII.GetString(length < 0 ? source : source[..length]);
}

sealed record E2eOptions(string Host, int Port, string AccountFile, int Slot, ushort ConnectionId, TimeSpan Timeout, int? SplitSlot, int? SplitQuantity, int? SeedStackSlot, int? DeleteSlot, bool? SetAccountBlocked, bool ExpectDeleteBlocked, string? TradeDeletePeerAccountFile, string? TradeQuitPeerAccountFile, string? AdversarialPeerAccountFile, string? TradeReplayPeerAccountFile, bool TradeGoldOnly, bool TradeQuitDead, bool TradeQuitKilled, bool TradeQuitPk, bool TradeQuitPkActive)
{
    public bool TradeQuitNegativeHp { get; init; }
    public bool TradeQuitIdle { get; init; }
    public string? AutoTradeClosePeerAccountFile { get; init; }
    public string? AutoTradeStateFile { get; init; }
    public bool MotionGateSmoke { get; init; }
    public bool MotionNegativeHp { get; init; }
    public bool TradeListGateSmoke { get; init; }
    public bool TradeListNegativeHp { get; init; }
    public bool ActionGateSmoke { get; init; }
    public bool ActionNegativeHp { get; init; }
    public bool AttackDeadGateSmoke { get; init; }
    public bool AttackRvrGateSmoke { get; init; }
    public bool AttackTimingGateSmoke { get; init; }
    public bool AttackSkillGateSmoke { get; init; }
    public bool AttackPartySkillGateSmoke { get; init; }
    public bool AttackGoldenShieldCoinSmoke { get; init; }
    public bool AttackType6ProtectionSmoke { get; init; }
    public bool AttackMultiTargetSmoke { get; init; }
    public bool AttackViewGateSmoke { get; init; }
    public bool AttackMixedSlotsSmoke { get; init; }
    public string? AttackMixedOutOfRangeAccountFile { get; init; }
    public bool AttackDoubleCriticalSmoke { get; init; }
    public bool AttackManaControlSmoke { get; init; }
    public bool AttackManaGainSmoke { get; init; }
    public bool AttackSkillHealSmoke { get; init; }
    public bool AttackSkillSummonNoticeSmoke { get; init; }
    public int ExpectedDoubleModeMultiplier { get; init; } = 1;
    public bool AttackDivineBlessingSmoke { get; init; }
    public bool AttackDivineBlessingInsufficientManaSmoke { get; init; }
    public string? AttackDivineBlessingDistantObserverAccountFile { get; init; }
    public bool ActionRangeSmoke { get; init; }
    public bool ActionIllusionEligibilitySmoke { get; init; }
    public bool ActionIllusionManaSmoke { get; init; }
    public bool ActionOriginRecallSmoke { get; init; }
    public bool MovementRestrictionRecallSmoke { get; init; }
    public string? ActionRecallDestinationObserverAccountFile { get; init; }
    public string? ActionRecallDistantObserverAccountFile { get; init; }
    public string? AccountReviewRoot { get; init; }
    public string? AccountReviewPeerAccountFile { get; init; }
    public string? AccountReviewActorStateFile { get; init; }
    public string? AccountReviewPeerStateFile { get; init; }
    public string? AccountReviewActionReadyFile { get; init; }
    public string? AccountReviewActionReleaseFile { get; init; }
    public bool ActionAccountAnalysisSmoke { get; init; }
    public bool ActionAccountAnalysisThresholdSmoke { get; init; }
    public bool AccountAnalysisLoginSmoke { get; init; }
    public bool AccountReviewUnavailableLoginSmoke { get; init; }
    public bool ExpectReviewWriteFailure { get; init; }
    public bool CurrentScoreLoginProjectionSmoke { get; init; }
    public bool CurrentScoreLoginProjectionRejectSmoke { get; init; }

    public static E2eOptions Parse(string[] args)
    {
        string? host = null;
        string? accountFile = null;
        var port = 8281;
        var slot = 0;
        ushort connectionId = 4;
        var timeout = TimeSpan.FromSeconds(5);
        int? splitSlot = null;
        int? splitQuantity = null;
        int? seedStackSlot = null;
        int? deleteSlot = null;
        bool? setAccountBlocked = null;
        var expectDeleteBlocked = false;
        string? tradeDeletePeerAccountFile = null;
        string? tradeQuitPeerAccountFile = null;
        string? adversarialPeerAccountFile = null;
        string? tradeReplayPeerAccountFile = null;
        var tradeGoldOnly = false;
        var tradeQuitDead = false;
        var tradeQuitKilled = false;
        var tradeQuitPk = false;
        var tradeQuitPkActive = false;
        var tradeQuitNegativeHp = false;
        var tradeQuitIdle = false;
        string? autoTradeClosePeerAccountFile = null;
        string? attackMixedOutOfRangeAccountFile = null;
        string? autoTradeStateFile = null;
        var motionGateSmoke = false;
        var motionNegativeHp = false;
        var tradeListGateSmoke = false;
        var tradeListNegativeHp = false;
        var actionGateSmoke = false;
        var actionNegativeHp = false;
        var attackDeadGateSmoke = false;
        var attackRvrGateSmoke = false;
        var attackTimingGateSmoke = false;
        var attackSkillGateSmoke = false;
        var attackPartySkillGateSmoke = false;
        var attackGoldenShieldCoinSmoke = false;
        var attackType6ProtectionSmoke = false;
        var attackMultiTargetSmoke = false;
        var attackViewGateSmoke = false;
        var attackMixedSlotsSmoke = false;
        var attackDoubleCriticalSmoke = false;
        var attackManaControlSmoke = false;
        var attackManaGainSmoke = false;
        var attackSkillHealSmoke = false;
        var attackSkillSummonNoticeSmoke = false;
        var expectedDoubleModeMultiplier = 1;
        var attackDivineBlessingSmoke = false;
        var attackDivineBlessingInsufficientManaSmoke = false;
        string? attackDivineBlessingDistantObserverAccountFile = null;
        var actionRangeSmoke = false;
        var actionIllusionEligibilitySmoke = false;
        var actionIllusionManaSmoke = false;
        var actionOriginRecallSmoke = false;
        var movementRestrictionRecallSmoke = false;
        string? actionRecallDestinationObserverAccountFile = null;
        string? actionRecallDistantObserverAccountFile = null;
        string? accountReviewRoot = null;
        string? accountReviewPeerAccountFile = null;
        string? accountReviewActorStateFile = null;
        string? accountReviewPeerStateFile = null;
        string? accountReviewActionReadyFile = null;
        string? accountReviewActionReleaseFile = null;
        var actionAccountAnalysisSmoke = false;
        var actionAccountAnalysisThresholdSmoke = false;
        var accountAnalysisLoginSmoke = false;
        var accountReviewUnavailableLoginSmoke = false;
        var expectReviewWriteFailure = false;
        var currentScoreLoginProjectionSmoke = false;
        var currentScoreLoginProjectionRejectSmoke = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--current-score-login-projection-smoke":
                    currentScoreLoginProjectionSmoke = true;
                    break;
                case "--current-score-login-projection-reject-smoke":
                    currentScoreLoginProjectionRejectSmoke = true;
                    break;
                case "--action-range-smoke":
                    actionRangeSmoke = true;
                    actionGateSmoke = true;
                    break;
                case "--action-illusion-eligibility-smoke":
                    actionIllusionEligibilitySmoke = true;
                    actionGateSmoke = true;
                    break;
                case "--action-illusion-mana-smoke":
                    actionIllusionManaSmoke = true;
                    actionGateSmoke = true;
                    break;
                case "--action-origin-recall-smoke":
                    actionOriginRecallSmoke = true;
                    break;
                case "--movement-restriction-recall-smoke":
                    movementRestrictionRecallSmoke = true;
                    break;
                case "--action-recall-destination-observer-account-file" when index + 1 < args.Length:
                    actionRecallDestinationObserverAccountFile = Path.GetFullPath(args[++index]);
                    break;
                case "--action-recall-distant-observer-account-file" when index + 1 < args.Length:
                    actionRecallDistantObserverAccountFile = Path.GetFullPath(args[++index]);
                    break;
                case "--action-account-analysis-smoke":
                    actionAccountAnalysisSmoke = true;
                    break;
                case "--action-account-analysis-threshold-smoke":
                    actionAccountAnalysisThresholdSmoke = true;
                    break;
                case "--account-analysis-login-smoke":
                    accountAnalysisLoginSmoke = true;
                    break;
                case "--account-review-unavailable-login-smoke":
                    accountReviewUnavailableLoginSmoke = true;
                    break;
                case "--account-review-peer-account-file" when index + 1 < args.Length:
                    accountReviewPeerAccountFile = Path.GetFullPath(args[++index]);
                    break;
                case "--account-review-actor-state-file" when index + 1 < args.Length:
                    accountReviewActorStateFile = Path.GetFullPath(args[++index]);
                    break;
                case "--account-review-peer-state-file" when index + 1 < args.Length:
                    accountReviewPeerStateFile = Path.GetFullPath(args[++index]);
                    break;
                case "--account-review-action-ready-file" when index + 1 < args.Length:
                    accountReviewActionReadyFile = Path.GetFullPath(args[++index]);
                    break;
                case "--account-review-action-release-file" when index + 1 < args.Length:
                    accountReviewActionReleaseFile = Path.GetFullPath(args[++index]);
                    break;
                case "--account-review-root" when index + 1 < args.Length:
                    accountReviewRoot = Path.GetFullPath(args[++index]);
                    break;
                case "--expect-review-write-failure":
                    expectReviewWriteFailure = true;
                    break;
                case "--action-gate-smoke":
                    actionGateSmoke = true;
                    break;
                case "--attack-dead-gate-smoke":
                    attackDeadGateSmoke = true;
                    break;
                case "--attack-rvr-gate-smoke":
                    attackRvrGateSmoke = true;
                    break;
                case "--attack-skill-heal-smoke":
                    attackSkillHealSmoke = true;
                    break;
                case "--attack-summon-notice-smoke":
                    attackSkillSummonNoticeSmoke = true;
                    break;
                case "--expected-double-mode" when index + 1 < args.Length && int.TryParse(args[++index], out var parsedExpectedDoubleModeMultiplier):
                    expectedDoubleModeMultiplier = parsedExpectedDoubleModeMultiplier;
                    break;
                case "--attack-divine-blessing-smoke":
                    attackDivineBlessingSmoke = true;
                    break;
                case "--attack-divine-blessing-insufficient-mana-smoke":
                    attackDivineBlessingInsufficientManaSmoke = true;
                    break;
                case "--attack-divine-blessing-distant-observer-account-file" when index + 1 < args.Length:
                    attackDivineBlessingDistantObserverAccountFile = Path.GetFullPath(args[++index]);
                    break;
                case "--attack-mana-gain-smoke":
                    attackManaGainSmoke = true;
                    break;
                case "--attack-skill-gate-smoke":
                    attackSkillGateSmoke = true;
                    break;
                case "--attack-party-skill-gate-smoke":
                    attackPartySkillGateSmoke = true;
                    break;
                case "--attack-golden-shield-coin-smoke":
                    attackGoldenShieldCoinSmoke = true;
                    break;
                case "--attack-type6-protection-smoke":
                    attackType6ProtectionSmoke = true;
                    break;
                case "--attack-double-critical-smoke":
                    attackDoubleCriticalSmoke = true;
                    break;
                case "--attack-multi-target-smoke":
                    attackMultiTargetSmoke = true;
                    break;
                case "--attack-view-gate-smoke":
                    attackViewGateSmoke = true;
                    break;
                case "--attack-mixed-slots-smoke":
                    attackMixedSlotsSmoke = true;
                    break;
                case "--attack-mana-control-smoke":
                    attackManaControlSmoke = true;
                    break;
                case "--attack-timing-gate-smoke":
                    attackTimingGateSmoke = true;
                    break;
                case "--action-negative-hp":
                    actionNegativeHp = true;
                    actionGateSmoke = true;
                    break;
                case "--trade-list-gate-smoke":
                    tradeListGateSmoke = true;
                    break;
                case "--trade-list-negative-hp":
                    tradeListNegativeHp = true;
                    tradeListGateSmoke = true;
                    break;
                case "--motion-gate-smoke":
                    motionGateSmoke = true;
                    break;
                case "--motion-negative-hp":
                    motionNegativeHp = true;
                    motionGateSmoke = true;
                    break;
                case "--autotrade-close-peer-account-file" when index + 1 < args.Length:
                    autoTradeClosePeerAccountFile = Path.GetFullPath(args[++index]);
                    break;
                case "--autotrade-state-file" when index + 1 < args.Length:
                    autoTradeStateFile = Path.GetFullPath(args[++index]);
                    break;
                case "--attack-mixed-out-of-range-account-file" when index + 1 < args.Length:
                    attackMixedOutOfRangeAccountFile = Path.GetFullPath(args[++index]);
                    break;
                case "--host" when index + 1 < args.Length:
                    host = args[++index];
                    break;
                case "--port" when index + 1 < args.Length && int.TryParse(args[++index], out var parsedPort):
                    port = parsedPort;
                    break;
                case "--account-file" when index + 1 < args.Length:
                    accountFile = Path.GetFullPath(args[++index]);
                    break;
                case "--slot" when index + 1 < args.Length && int.TryParse(args[++index], out var parsedSlot):
                    slot = parsedSlot;
                    break;
                case "--connection-id" when index + 1 < args.Length && ushort.TryParse(args[++index], out var parsedConnectionId):
                    connectionId = parsedConnectionId;
                    break;
                case "--timeout-seconds" when index + 1 < args.Length && double.TryParse(args[++index], out var seconds):
                    timeout = TimeSpan.FromSeconds(seconds);
                    break;
                case "--split-slot" when index + 1 < args.Length && int.TryParse(args[++index], out var parsedSplitSlot):
                    splitSlot = parsedSplitSlot;
                    break;
                case "--split-quantity" when index + 1 < args.Length && int.TryParse(args[++index], out var parsedSplitQuantity):
                    splitQuantity = parsedSplitQuantity;
                    break;
                case "--seed-stack-slot" when index + 1 < args.Length && int.TryParse(args[++index], out var parsedSeedStackSlot):
                    seedStackSlot = parsedSeedStackSlot;
                    break;
                case "--delete-slot" when index + 1 < args.Length && int.TryParse(args[++index], out var parsedDeleteSlot):
                    deleteSlot = parsedDeleteSlot;
                    break;
                case "--set-account-blocked" when index + 1 < args.Length && bool.TryParse(args[++index], out var parsedBlocked):
                    setAccountBlocked = parsedBlocked;
                    break;
                case "--expect-delete-blocked":
                    expectDeleteBlocked = true;
                    break;
                case "--trade-delete-peer-account-file" when index + 1 < args.Length:
                    tradeDeletePeerAccountFile = Path.GetFullPath(args[++index]);
                    break;
                case "--trade-quit-peer-account-file" when index + 1 < args.Length:
                    tradeQuitPeerAccountFile = Path.GetFullPath(args[++index]);
                    break;
                case "--adversarial-peer-account-file" when index + 1 < args.Length:
                    adversarialPeerAccountFile = Path.GetFullPath(args[++index]);
                    break;
                case "--trade-replay-peer-account-file" when index + 1 < args.Length:
                    tradeReplayPeerAccountFile = Path.GetFullPath(args[++index]);
                    break;
                case "--trade-gold-only":
                    tradeGoldOnly = true;
                    break;
                case "--trade-quit-dead":
                    tradeQuitDead = true;
                    break;
                case "--trade-quit-negative-hp":
                    tradeQuitNegativeHp = true;
                    tradeQuitDead = true;
                    break;
                case "--trade-quit-idle":
                    tradeQuitIdle = true;
                    tradeQuitPk = true;
                    break;
                case "--trade-quit-killed":
                    tradeQuitKilled = true;
                    tradeQuitDead = true;
                    break;
                case "--trade-quit-pk":
                    tradeQuitPk = true;
                    break;
                case "--trade-quit-pk-active":
                    tradeQuitPk = true;
                    tradeQuitPkActive = true;
                    break;
                default:
                    throw new ArgumentException("Usage: WydCdk.Client.E2E --account-file <sandbox account file> [--host <host>] [--port <port>] [--slot <0-3>] [--split-slot <0-59> --split-quantity <1-119>] [--seed-stack-slot <0-59>] [--delete-slot <0-59>].");
            }
        }

        if (string.IsNullOrWhiteSpace(host)) host = "127.0.0.1";
        if (string.IsNullOrWhiteSpace(accountFile)) throw new ArgumentException("--account-file is required for the local sandbox smoke.");
        if ((currentScoreLoginProjectionSmoke || currentScoreLoginProjectionRejectSmoke) && (currentScoreLoginProjectionSmoke == currentScoreLoginProjectionRejectSmoke ||
            host != "127.0.0.1" || port != 18283 ||
            !Path.GetFileName(accountFile).Equals("SCORETEST", StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(Path.GetDirectoryName(accountFile)!).Equals("S", StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(accountFile)!)!).Equals("account", StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFullPath(accountFile).Contains(Path.Combine("Backup", "Build", "CurrentScoreLoginProjection-20261008"), StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("CurrentScore login projection smoke requires exactly one smoke mode and the disposable SCORETEST fixture under Backup/Build/CurrentScoreLoginProjection-20261008 on loopback port 18283.");
        if ((currentScoreLoginProjectionSmoke || currentScoreLoginProjectionRejectSmoke) && (splitSlot is not null || seedStackSlot is not null || deleteSlot is not null ||
            tradeDeletePeerAccountFile is not null || tradeQuitPeerAccountFile is not null || adversarialPeerAccountFile is not null ||
            tradeReplayPeerAccountFile is not null || autoTradeClosePeerAccountFile is not null || autoTradeStateFile is not null ||
            actionAccountAnalysisSmoke || actionAccountAnalysisThresholdSmoke || accountAnalysisLoginSmoke || accountReviewUnavailableLoginSmoke ||
            motionGateSmoke || tradeListGateSmoke || actionGateSmoke || attackDeadGateSmoke || attackRvrGateSmoke || attackTimingGateSmoke ||
            attackSkillGateSmoke || attackPartySkillGateSmoke || attackGoldenShieldCoinSmoke || attackType6ProtectionSmoke || attackMultiTargetSmoke ||
            attackMixedSlotsSmoke || attackDoubleCriticalSmoke || attackManaControlSmoke || attackManaGainSmoke || attackSkillHealSmoke ||
            attackSkillSummonNoticeSmoke || actionOriginRecallSmoke || movementRestrictionRecallSmoke))
            throw new ArgumentException("CurrentScore login projection smoke cannot be combined with another gameplay smoke mode.");
        var accountAnalysisMode = actionAccountAnalysisSmoke || actionAccountAnalysisThresholdSmoke ||
            accountAnalysisLoginSmoke || accountReviewUnavailableLoginSmoke;
        if (accountAnalysisMode != (accountReviewPeerAccountFile is not null) || accountAnalysisMode != (accountReviewRoot is not null))
            throw new ArgumentException("Account Analysis smoke modes require --account-review-peer-account-file and --account-review-root.");
        if ((accountReviewActorStateFile is null) != (accountReviewPeerStateFile is null))
            throw new ArgumentException("Per-world Account Analysis checks require both actor and peer state files.");
        if ((accountReviewActionReadyFile is null) != (accountReviewActionReleaseFile is null))
            throw new ArgumentException("The Account Analysis action barrier requires both ready and release files.");
        if ((accountReviewActorStateFile is not null || accountReviewActionReadyFile is not null) &&
            !(actionAccountAnalysisSmoke || actionAccountAnalysisThresholdSmoke))
            throw new ArgumentException("Per-world state files and the action barrier are valid only with an Action Account Analysis smoke.");
        if ((actionAccountAnalysisSmoke ? 1 : 0) + (actionAccountAnalysisThresholdSmoke ? 1 : 0) +
            (accountAnalysisLoginSmoke ? 1 : 0) + (accountReviewUnavailableLoginSmoke ? 1 : 0) > 1)
            throw new ArgumentException("Account Analysis network scenarios are separate invocations.");
        if (expectReviewWriteFailure && !actionAccountAnalysisSmoke)
            throw new ArgumentException("--expect-review-write-failure is valid only with --action-account-analysis-smoke.");
        if (accountAnalysisMode)
        {
            var expectedReviewRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(accountFile)!, "..", "..", "account-review"));
            var expectedBarrierRoot = Path.Combine(expectedReviewRoot, "action-barrier");
            var stateFilesValid = accountReviewActorStateFile is null ||
                (IsAccountAnalysisStateFile(accountReviewActorStateFile, accountFile) &&
                 IsAccountAnalysisStateFile(accountReviewPeerStateFile!, accountReviewPeerAccountFile!) &&
                 Path.GetDirectoryName(accountReviewActorStateFile)!.Equals(Path.GetDirectoryName(accountReviewPeerStateFile), StringComparison.OrdinalIgnoreCase) &&
                 !accountReviewActorStateFile.Equals(accountReviewPeerStateFile, StringComparison.OrdinalIgnoreCase));
            var actionBarrierValid = accountReviewActionReadyFile is null ||
                (Path.GetDirectoryName(accountReviewActionReadyFile)!.Equals(expectedBarrierRoot, StringComparison.OrdinalIgnoreCase) &&
                 Path.GetDirectoryName(accountReviewActionReleaseFile)!.Equals(expectedBarrierRoot, StringComparison.OrdinalIgnoreCase) &&
                 !accountReviewActionReadyFile.Equals(accountReviewActionReleaseFile, StringComparison.OrdinalIgnoreCase));
            if (!System.Net.IPAddress.TryParse(host, out var reviewIp) || !System.Net.IPAddress.IsLoopback(reviewIp) || port != 18283 ||
                !IsAccountAnalysisFixture(accountFile) || !IsAccountAnalysisFixture(accountReviewPeerAccountFile!) ||
                accountFile.Equals(accountReviewPeerAccountFile, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetDirectoryName(accountFile)!.Equals(Path.GetDirectoryName(accountReviewPeerAccountFile), StringComparison.OrdinalIgnoreCase) ||
                !expectedReviewRoot.Equals(accountReviewRoot, StringComparison.OrdinalIgnoreCase) || !stateFilesValid || !actionBarrierValid ||
                autoTradeClosePeerAccountFile is not null || autoTradeStateFile is not null || tradeDeletePeerAccountFile is not null ||
                tradeQuitPeerAccountFile is not null || adversarialPeerAccountFile is not null || tradeReplayPeerAccountFile is not null ||
                splitSlot is not null || splitQuantity is not null || seedStackSlot is not null || deleteSlot is not null ||
                setAccountBlocked is not null || expectDeleteBlocked || tradeGoldOnly || tradeQuitDead || tradeQuitKilled || tradeQuitPk ||
                motionGateSmoke || tradeListGateSmoke || actionGateSmoke || attackDeadGateSmoke || attackRvrGateSmoke || attackTimingGateSmoke || attackSkillGateSmoke || attackGoldenShieldCoinSmoke || attackType6ProtectionSmoke || attackManaControlSmoke || attackManaGainSmoke || attackSkillHealSmoke)
                throw new ArgumentException("Account Analysis smokes are restricted to distinct isolated fixture copies and loopback port 18283.");
        }
        if (motionGateSmoke && autoTradeClosePeerAccountFile is null)
            throw new ArgumentException("Motion smoke requires the guarded autotrade fixture pair and isolated port.");
        if (tradeListGateSmoke && (autoTradeClosePeerAccountFile is null || motionGateSmoke))
            throw new ArgumentException("ReqTradeList requires its own guarded autotrade fixture pair and isolated port.");
        if (attackDeadGateSmoke && (autoTradeClosePeerAccountFile is null || motionGateSmoke || tradeListGateSmoke || actionGateSmoke || attackRvrGateSmoke || attackTimingGateSmoke ||
            actionOriginRecallSmoke || movementRestrictionRecallSmoke || accountAnalysisMode))
            throw new ArgumentException("Dead Attack requires its own guarded autotrade fixture pair and isolated port.");
        if (attackRvrGateSmoke && (autoTradeClosePeerAccountFile is null || motionGateSmoke || tradeListGateSmoke || actionGateSmoke || attackDeadGateSmoke || attackTimingGateSmoke ||
            actionOriginRecallSmoke || movementRestrictionRecallSmoke || accountAnalysisMode))
            throw new ArgumentException("RVR Attack requires its own guarded autotrade fixture pair and isolated port.");
        if (attackTimingGateSmoke && (autoTradeClosePeerAccountFile is null || motionGateSmoke || tradeListGateSmoke || actionGateSmoke || attackDeadGateSmoke || attackRvrGateSmoke ||
            actionOriginRecallSmoke || movementRestrictionRecallSmoke || accountAnalysisMode))
            throw new ArgumentException("Attack timing requires its own guarded autotrade fixture pair and isolated port.");
        if (attackSkillGateSmoke && (autoTradeClosePeerAccountFile is null || motionGateSmoke || tradeListGateSmoke || actionGateSmoke || attackDeadGateSmoke || attackRvrGateSmoke ||
            attackTimingGateSmoke || actionOriginRecallSmoke || movementRestrictionRecallSmoke || accountAnalysisMode))
            throw new ArgumentException("Attack skill gate requires its own guarded autotrade fixture pair and isolated port.");
        if (attackPartySkillGateSmoke && (autoTradeClosePeerAccountFile is null || autoTradeStateFile is null ||
            motionGateSmoke || tradeListGateSmoke || actionGateSmoke || actionNegativeHp || actionRangeSmoke || actionIllusionEligibilitySmoke || actionIllusionManaSmoke ||
            attackDeadGateSmoke || attackRvrGateSmoke || attackTimingGateSmoke || attackSkillGateSmoke || attackGoldenShieldCoinSmoke || attackType6ProtectionSmoke ||
            attackMultiTargetSmoke || attackDoubleCriticalSmoke || attackManaControlSmoke || attackManaGainSmoke || attackSkillHealSmoke ||
            actionOriginRecallSmoke || movementRestrictionRecallSmoke || accountAnalysisMode || host != "127.0.0.1" || port != 18283 ||
            !IsAutoTradeFixture(accountFile) || !IsAutoTradeFixture(autoTradeClosePeerAccountFile) ||
            accountFile.Equals(autoTradeClosePeerAccountFile, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Party skill gate requires its own loopback port 18283 run and distinct isolated AutoTradeCloseSmoke account copies.");
        if (attackGoldenShieldCoinSmoke && (autoTradeClosePeerAccountFile is null || autoTradeStateFile is null ||
            motionGateSmoke || tradeListGateSmoke || actionGateSmoke || actionNegativeHp || actionRangeSmoke || actionIllusionEligibilitySmoke || actionIllusionManaSmoke ||
            attackDeadGateSmoke || attackRvrGateSmoke || attackTimingGateSmoke || attackSkillGateSmoke || attackType6ProtectionSmoke || attackMultiTargetSmoke ||
            attackDoubleCriticalSmoke || attackManaControlSmoke || attackManaGainSmoke || attackSkillHealSmoke ||
            actionOriginRecallSmoke || movementRestrictionRecallSmoke || accountAnalysisMode || host != "127.0.0.1" || port != 18283 ||
            !IsAutoTradeFixture(accountFile) || !IsAutoTradeFixture(autoTradeClosePeerAccountFile) ||
            accountFile.Equals(autoTradeClosePeerAccountFile, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Golden Shield coin smoke requires its own loopback port 18283 run and distinct isolated AutoTradeCloseSmoke account copies.");
        if (attackManaGainSmoke && (autoTradeClosePeerAccountFile is null || motionGateSmoke || tradeListGateSmoke || actionGateSmoke || attackDeadGateSmoke || attackRvrGateSmoke ||
            attackTimingGateSmoke || attackSkillGateSmoke || actionOriginRecallSmoke || movementRestrictionRecallSmoke || accountAnalysisMode))
            throw new ArgumentException("Attack mana gain requires its own guarded autotrade fixture pair and isolated port.");
        if (attackSkillHealSmoke && (autoTradeClosePeerAccountFile is null || motionGateSmoke || tradeListGateSmoke || actionGateSmoke || attackDeadGateSmoke || attackRvrGateSmoke ||
            attackTimingGateSmoke || attackSkillGateSmoke || attackManaGainSmoke || actionOriginRecallSmoke || movementRestrictionRecallSmoke || accountAnalysisMode))
            throw new ArgumentException("Attack skill heal requires its own guarded autotrade fixture pair and isolated port.");
        var attackDivineBlessingMode = attackDivineBlessingSmoke || attackDivineBlessingInsufficientManaSmoke;
        if (attackSkillSummonNoticeSmoke && (autoTradeClosePeerAccountFile is null || autoTradeStateFile is null ||
            motionGateSmoke || tradeListGateSmoke || actionGateSmoke || actionNegativeHp || actionRangeSmoke || actionIllusionEligibilitySmoke || actionIllusionManaSmoke ||
            attackDeadGateSmoke || attackRvrGateSmoke || attackTimingGateSmoke || attackSkillGateSmoke || attackPartySkillGateSmoke || attackGoldenShieldCoinSmoke ||
            attackType6ProtectionSmoke || attackMultiTargetSmoke || attackMixedSlotsSmoke || attackDoubleCriticalSmoke || attackManaControlSmoke ||
            attackManaGainSmoke || attackSkillHealSmoke || attackDivineBlessingMode || actionOriginRecallSmoke || movementRestrictionRecallSmoke ||
            accountAnalysisMode || splitSlot is not null || splitQuantity is not null || seedStackSlot is not null || deleteSlot is not null ||
            setAccountBlocked is not null || expectDeleteBlocked || tradeGoldOnly || tradeQuitDead || tradeQuitKilled || tradeQuitPk ||
            host != "127.0.0.1" || port != 18283 || !IsAutoTradeFixture(accountFile) ||
            !IsAutoTradeFixture(autoTradeClosePeerAccountFile) ||
            !Path.GetDirectoryName(accountFile)!.Equals(Path.GetDirectoryName(autoTradeClosePeerAccountFile), StringComparison.OrdinalIgnoreCase) ||
            accountFile.Equals(autoTradeClosePeerAccountFile, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Attack summon notice requires its own loopback port 18283 run and distinct isolated AutoTradeCloseSmoke account copies.");
        if (attackDivineBlessingSmoke && attackDivineBlessingInsufficientManaSmoke)
            throw new ArgumentException("Divine Blessing success and insufficient-mana cases require separate invocations.");
        if (attackDivineBlessingMode && (autoTradeClosePeerAccountFile is null || autoTradeStateFile is null ||
            attackDivineBlessingDistantObserverAccountFile is null || motionGateSmoke || tradeListGateSmoke || actionGateSmoke || actionNegativeHp ||
            actionRangeSmoke || actionIllusionEligibilitySmoke || actionIllusionManaSmoke || attackDeadGateSmoke || attackRvrGateSmoke ||
            attackTimingGateSmoke || attackSkillGateSmoke || attackPartySkillGateSmoke || attackGoldenShieldCoinSmoke || attackType6ProtectionSmoke ||
            attackMultiTargetSmoke || attackMixedSlotsSmoke || attackDoubleCriticalSmoke || attackManaControlSmoke || attackManaGainSmoke || attackSkillHealSmoke ||
            actionOriginRecallSmoke || movementRestrictionRecallSmoke || accountAnalysisMode || splitSlot is not null || splitQuantity is not null ||
            seedStackSlot is not null || deleteSlot is not null || setAccountBlocked is not null || expectDeleteBlocked || tradeGoldOnly || tradeQuitDead ||
            tradeQuitKilled || tradeQuitPk || host != "127.0.0.1" || port != 18283 ||
            !IsAutoTradeFixture(accountFile) || !IsAutoTradeFixture(autoTradeClosePeerAccountFile) || !IsAutoTradeFixture(attackDivineBlessingDistantObserverAccountFile) ||
            !Path.GetDirectoryName(accountFile)!.Equals(Path.GetDirectoryName(autoTradeClosePeerAccountFile), StringComparison.OrdinalIgnoreCase) ||
            !Path.GetDirectoryName(accountFile)!.Equals(Path.GetDirectoryName(attackDivineBlessingDistantObserverAccountFile), StringComparison.OrdinalIgnoreCase) ||
            accountFile.Equals(autoTradeClosePeerAccountFile, StringComparison.OrdinalIgnoreCase) ||
            accountFile.Equals(attackDivineBlessingDistantObserverAccountFile, StringComparison.OrdinalIgnoreCase) ||
            autoTradeClosePeerAccountFile.Equals(attackDivineBlessingDistantObserverAccountFile, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Divine Blessing requires its own loopback port 18283 run and three distinct isolated AutoTradeCloseSmoke account copies.");
        if (attackDivineBlessingDistantObserverAccountFile is not null && !attackDivineBlessingMode)
            throw new ArgumentException("The Divine Blessing distant observer fixture is valid only with a Divine Blessing smoke.");
        if (attackType6ProtectionSmoke && (autoTradeClosePeerAccountFile is null || autoTradeStateFile is null ||
            motionGateSmoke || tradeListGateSmoke || actionGateSmoke || actionNegativeHp || actionRangeSmoke || actionIllusionEligibilitySmoke || actionIllusionManaSmoke ||
            attackDeadGateSmoke || attackRvrGateSmoke || attackTimingGateSmoke || attackSkillGateSmoke || attackMultiTargetSmoke || attackDoubleCriticalSmoke || attackManaControlSmoke || attackManaGainSmoke || attackSkillHealSmoke ||
            actionOriginRecallSmoke || movementRestrictionRecallSmoke || accountAnalysisMode || host != "127.0.0.1" || port != 18283 ||
            !IsAutoTradeFixture(accountFile) || !IsAutoTradeFixture(autoTradeClosePeerAccountFile) ||
            accountFile.Equals(autoTradeClosePeerAccountFile, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Type 6 protection requires its own loopback port 18283 run and distinct isolated AutoTradeCloseSmoke account copies.");
        if (actionRangeSmoke && actionNegativeHp) throw new ArgumentException("Action range and HP modes are separate.");
        if (actionIllusionEligibilitySmoke && (actionRangeSmoke || actionNegativeHp || actionOriginRecallSmoke || movementRestrictionRecallSmoke))
            throw new ArgumentException("Action3 eligibility is a separate smoke mode.");
        if (actionIllusionManaSmoke && (actionRangeSmoke || actionIllusionEligibilitySmoke || actionNegativeHp || actionOriginRecallSmoke || movementRestrictionRecallSmoke))
            throw new ArgumentException("Action3 mana is a separate smoke mode.");
        if (actionOriginRecallSmoke && movementRestrictionRecallSmoke)
            throw new ArgumentException("Action-origin and movement-restriction recall are separate smoke modes.");
        if (actionOriginRecallSmoke || movementRestrictionRecallSmoke)
        {
            var recallAccounts = new[]
            {
                accountFile,
                autoTradeClosePeerAccountFile,
                actionRecallDestinationObserverAccountFile,
                actionRecallDistantObserverAccountFile,
            };
            if (autoTradeClosePeerAccountFile is null || actionRecallDestinationObserverAccountFile is null ||
                actionRecallDistantObserverAccountFile is null || actionGateSmoke || actionNegativeHp || actionRangeSmoke ||
                motionGateSmoke || tradeListGateSmoke || attackDeadGateSmoke || attackRvrGateSmoke || attackTimingGateSmoke || attackSkillGateSmoke || attackManaGainSmoke || attackSkillHealSmoke || accountAnalysisMode || host != "127.0.0.1" || port != 18283 ||
                recallAccounts.Any(path => path is null || !IsAutoTradeFixture(path)) ||
                recallAccounts.Select(path => Path.GetDirectoryName(path!)).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1 ||
                recallAccounts.Distinct(StringComparer.OrdinalIgnoreCase).Count() != recallAccounts.Length)
                throw new ArgumentException("Recall requires four distinct isolated account fixtures on loopback port 18283.");
        }
        else if (actionRecallDestinationObserverAccountFile is not null || actionRecallDistantObserverAccountFile is not null)
            throw new ArgumentException("Recall observer accounts are valid only with --action-origin-recall-smoke.");
        if (actionGateSmoke && (autoTradeClosePeerAccountFile is null || motionGateSmoke || tradeListGateSmoke))
            throw new ArgumentException("Action requires its own guarded fixture pair and isolated port.");
        if (autoTradeClosePeerAccountFile is not null || autoTradeStateFile is not null)
        {
            if (autoTradeClosePeerAccountFile is null || autoTradeStateFile is null ||
                host != "127.0.0.1" || port != 18283 ||
                !IsAutoTradeFixture(accountFile) || !IsAutoTradeFixture(autoTradeClosePeerAccountFile) ||
                !Path.GetDirectoryName(accountFile)!.Equals(Path.GetDirectoryName(autoTradeClosePeerAccountFile), StringComparison.OrdinalIgnoreCase) ||
                accountFile.Equals(autoTradeClosePeerAccountFile, StringComparison.OrdinalIgnoreCase) ||
                !autoTradeStateFile.Equals(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(accountFile)!, "..", "..", "autotrade.json")), StringComparison.OrdinalIgnoreCase) ||
                tradeDeletePeerAccountFile is not null || tradeQuitPeerAccountFile is not null || adversarialPeerAccountFile is not null ||
                tradeReplayPeerAccountFile is not null || deleteSlot is not null || seedStackSlot is not null || splitSlot is not null ||
                setAccountBlocked is not null || expectDeleteBlocked || tradeGoldOnly || tradeQuitDead || tradeQuitPk)
                throw new ArgumentException("Autotrade close requires loopback, distinct AutoTradeCloseSmoke/<copy>/account/T/TESTE* copies and their autotrade.json; no other gameplay modes.");
        }
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(args), "--port must be between 1 and 65535.");
        if (slot is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(args), "--slot must be between 0 and 3.");
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(args), "--timeout-seconds must be positive.");
        if (splitSlot.HasValue != splitQuantity.HasValue) throw new ArgumentException("--split-slot and --split-quantity must be supplied together.");
        if (splitSlot is < 0 or > 59) throw new ArgumentOutOfRangeException(nameof(args), "--split-slot must be between 0 and 59.");
        if (splitQuantity is < 1 or > 119) throw new ArgumentOutOfRangeException(nameof(args), "--split-quantity must be between 1 and 119.");
        if (seedStackSlot is < 0 or > 59) throw new ArgumentOutOfRangeException(nameof(args), "--seed-stack-slot must be between 0 and 59.");
        if (deleteSlot is < 0 or > 59) throw new ArgumentOutOfRangeException(nameof(args), "--delete-slot must be between 0 and 59.");
        if (seedStackSlot is not null && (splitSlot is not null || splitQuantity is not null)) throw new ArgumentException("--seed-stack-slot cannot be combined with --split-slot or --split-quantity.");
        if (deleteSlot is not null && (seedStackSlot is not null || splitSlot is not null)) throw new ArgumentException("--delete-slot cannot be combined with seed or split operations.");
        if (setAccountBlocked is not null && (seedStackSlot is not null || deleteSlot is not null || splitSlot is not null)) throw new ArgumentException("--set-account-blocked must be run separately from gameplay operations.");
        if (expectDeleteBlocked && deleteSlot is null) throw new ArgumentException("--expect-delete-blocked requires --delete-slot.");
        if (tradeDeletePeerAccountFile is not null && (deleteSlot is null || seedStackSlot is not null || splitSlot is not null || setAccountBlocked is not null || expectDeleteBlocked)) throw new ArgumentException("--trade-delete-peer-account-file requires only --delete-slot among gameplay options.");
        if (tradeQuitPeerAccountFile is not null && (tradeDeletePeerAccountFile is not null || deleteSlot is null || seedStackSlot is not null || splitSlot is not null || setAccountBlocked is not null || expectDeleteBlocked)) throw new ArgumentException("--trade-quit-peer-account-file requires --delete-slot only as a carry-retention check.");
        if (tradeQuitNegativeHp && tradeQuitKilled)
            throw new ArgumentException("--trade-quit-negative-hp cannot be combined with --trade-quit-killed.");
        if (tradeQuitIdle && tradeQuitDead)
            throw new ArgumentException("--trade-quit-idle cannot be combined with dead/negative/killed modes.");
        if ((tradeQuitDead || tradeQuitPk) && (tradeQuitPeerAccountFile is null || !System.Net.IPAddress.TryParse(host, out var deadIp) ||
            !System.Net.IPAddress.IsLoopback(deadIp) || !IsIsolatedFixture(accountFile) || !IsIsolatedFixture(tradeQuitPeerAccountFile) ||
            accountFile.Equals(tradeQuitPeerAccountFile, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Trade quit dead/PK modes require loopback and distinct isolated copies below E2E7670/<copy>/account/T.");
        if (adversarialPeerAccountFile is not null)
        {
            if (tradeDeletePeerAccountFile is not null || tradeQuitPeerAccountFile is not null || deleteSlot is null || seedStackSlot is not null || splitSlot is not null || setAccountBlocked is not null || expectDeleteBlocked)
                throw new ArgumentException("Adversarial mode requires --delete-slot only as a carry-retention check.");
            if (!System.Net.IPAddress.TryParse(host, out var ip) || !System.Net.IPAddress.IsLoopback(ip) ||
                !IsAdversarialFixture(accountFile) || !IsAdversarialFixture(adversarialPeerAccountFile) ||
                accountFile.Equals(adversarialPeerAccountFile, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Adversarial mode is restricted to loopback and distinct E2E7670/TESTE or TESTE2 fixtures.");
        }
        if (tradeGoldOnly && tradeReplayPeerAccountFile is null)
            throw new ArgumentException("--trade-gold-only requires --trade-replay-peer-account-file.");
        if (tradeReplayPeerAccountFile is not null)
        {
            if (adversarialPeerAccountFile is not null || tradeDeletePeerAccountFile is not null || tradeQuitPeerAccountFile is not null || (tradeGoldOnly ? deleteSlot is not null : deleteSlot is null) || seedStackSlot is not null || splitSlot is not null || setAccountBlocked is not null || expectDeleteBlocked)
                throw new ArgumentException("Completed-trade replay requires --delete-slot for item mode, or --trade-gold-only without any item operation.");
            if (!System.Net.IPAddress.TryParse(host, out var ip) || !System.Net.IPAddress.IsLoopback(ip) ||
                !IsAdversarialFixture(accountFile) || !IsAdversarialFixture(tradeReplayPeerAccountFile) ||
                accountFile.Equals(tradeReplayPeerAccountFile, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Completed-trade replay is restricted to loopback and distinct E2E7670/TESTE or TESTE2 fixtures.");
        }
        if (attackDoubleCriticalSmoke && (autoTradeClosePeerAccountFile is null ||
            !System.Net.IPAddress.TryParse(host, out var doubleCriticalIp) || !System.Net.IPAddress.IsLoopback(doubleCriticalIp) ||
            !IsAutoTradeFixture(accountFile) || !IsAutoTradeFixture(autoTradeClosePeerAccountFile) ||
            accountFile.Equals(autoTradeClosePeerAccountFile, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("DoubleCritical smoke requires loopback and distinct isolated AutoTradeCloseSmoke account copies.");
        if (attackMultiTargetSmoke && (autoTradeClosePeerAccountFile is null || autoTradeStateFile is null ||
            motionGateSmoke || tradeListGateSmoke || actionGateSmoke || actionNegativeHp || actionRangeSmoke || actionIllusionEligibilitySmoke || actionIllusionManaSmoke ||
            attackDeadGateSmoke || attackRvrGateSmoke || attackTimingGateSmoke || attackSkillGateSmoke || attackDoubleCriticalSmoke || attackManaControlSmoke || attackManaGainSmoke || attackSkillHealSmoke ||
            actionOriginRecallSmoke || movementRestrictionRecallSmoke || accountAnalysisMode || host != "127.0.0.1" || port != 18283 ||
            !IsAutoTradeFixture(accountFile) || !IsAutoTradeFixture(autoTradeClosePeerAccountFile) ||
            accountFile.Equals(autoTradeClosePeerAccountFile, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Multi-target Attack smoke requires its own loopback port 18283 run and distinct isolated AutoTradeCloseSmoke account copies.");
        if (attackMixedSlotsSmoke && (autoTradeClosePeerAccountFile is null || autoTradeStateFile is null || attackMixedOutOfRangeAccountFile is null ||
            motionGateSmoke || tradeListGateSmoke || actionGateSmoke || actionNegativeHp || actionRangeSmoke || actionIllusionEligibilitySmoke || actionIllusionManaSmoke ||
            attackDeadGateSmoke || attackRvrGateSmoke || attackTimingGateSmoke || attackSkillGateSmoke || attackPartySkillGateSmoke || attackGoldenShieldCoinSmoke ||
            attackType6ProtectionSmoke || attackMultiTargetSmoke || attackDoubleCriticalSmoke || attackManaControlSmoke || attackManaGainSmoke || attackSkillHealSmoke ||
            actionOriginRecallSmoke || movementRestrictionRecallSmoke || accountAnalysisMode || host != "127.0.0.1" || port != 18283 ||
            !IsAutoTradeFixture(accountFile) || !IsAutoTradeFixture(autoTradeClosePeerAccountFile) || !IsAutoTradeFixture(attackMixedOutOfRangeAccountFile) ||
            !Path.GetDirectoryName(accountFile)!.Equals(Path.GetDirectoryName(autoTradeClosePeerAccountFile), StringComparison.OrdinalIgnoreCase) ||
            !Path.GetDirectoryName(accountFile)!.Equals(Path.GetDirectoryName(attackMixedOutOfRangeAccountFile), StringComparison.OrdinalIgnoreCase) ||
            accountFile.Equals(autoTradeClosePeerAccountFile, StringComparison.OrdinalIgnoreCase) ||
            accountFile.Equals(attackMixedOutOfRangeAccountFile, StringComparison.OrdinalIgnoreCase) ||
            autoTradeClosePeerAccountFile.Equals(attackMixedOutOfRangeAccountFile, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Mixed-slot Attack smoke requires loopback port 18283 and three distinct isolated AutoTradeCloseSmoke account copies.");
        if (attackViewGateSmoke && (autoTradeClosePeerAccountFile is null || autoTradeStateFile is null ||
            motionGateSmoke || tradeListGateSmoke || actionGateSmoke || actionNegativeHp || actionRangeSmoke || actionIllusionEligibilitySmoke || actionIllusionManaSmoke ||
            attackDeadGateSmoke || attackRvrGateSmoke || attackTimingGateSmoke || attackSkillGateSmoke || attackPartySkillGateSmoke || attackGoldenShieldCoinSmoke ||
            attackType6ProtectionSmoke || attackMultiTargetSmoke || attackMixedSlotsSmoke || attackDoubleCriticalSmoke || attackManaControlSmoke || attackManaGainSmoke ||
            attackSkillHealSmoke || attackSkillSummonNoticeSmoke || attackDivineBlessingMode || actionOriginRecallSmoke || movementRestrictionRecallSmoke || accountAnalysisMode ||
            host != "127.0.0.1" || port != 18283 || !IsAutoTradeFixture(accountFile) || !IsAutoTradeFixture(autoTradeClosePeerAccountFile) ||
            !Path.GetDirectoryName(accountFile)!.Equals(Path.GetDirectoryName(autoTradeClosePeerAccountFile), StringComparison.OrdinalIgnoreCase) ||
            accountFile.Equals(autoTradeClosePeerAccountFile, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("View-gate smoke requires loopback port 18283 and two distinct isolated AutoTradeCloseSmoke account copies.");
        if (attackMixedOutOfRangeAccountFile is not null && !attackMixedSlotsSmoke)
            throw new ArgumentException("The mixed-slot out-of-range account fixture is valid only with --attack-mixed-slots-smoke.");
        if (attackManaControlSmoke && (autoTradeClosePeerAccountFile is null || attackDoubleCriticalSmoke || attackManaGainSmoke || attackSkillHealSmoke ||
            motionGateSmoke || tradeListGateSmoke || actionGateSmoke || attackDeadGateSmoke || attackRvrGateSmoke || attackTimingGateSmoke || attackSkillGateSmoke ||
            actionOriginRecallSmoke || movementRestrictionRecallSmoke || accountAnalysisMode ||
            !System.Net.IPAddress.TryParse(host, out var manaControlIp) || !System.Net.IPAddress.IsLoopback(manaControlIp) || port != 18283 ||
            !IsAutoTradeFixture(accountFile) || !IsAutoTradeFixture(autoTradeClosePeerAccountFile) ||
            accountFile.Equals(autoTradeClosePeerAccountFile, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("ManaControl smoke requires its own loopback port18283 run and distinct isolated AutoTradeCloseSmoke account copies.");
        return new(host, port, accountFile, slot, connectionId, timeout, splitSlot, splitQuantity, seedStackSlot, deleteSlot, setAccountBlocked, expectDeleteBlocked, tradeDeletePeerAccountFile, tradeQuitPeerAccountFile, adversarialPeerAccountFile, tradeReplayPeerAccountFile, tradeGoldOnly, tradeQuitDead, tradeQuitKilled, tradeQuitPk, tradeQuitPkActive)
        { TradeQuitNegativeHp = tradeQuitNegativeHp, TradeQuitIdle = tradeQuitIdle,
          AutoTradeClosePeerAccountFile = autoTradeClosePeerAccountFile, AutoTradeStateFile = autoTradeStateFile, AttackMixedOutOfRangeAccountFile = attackMixedOutOfRangeAccountFile,
          AttackSkillSummonNoticeSmoke = attackSkillSummonNoticeSmoke,
          MotionGateSmoke = motionGateSmoke, MotionNegativeHp = motionNegativeHp,
          TradeListGateSmoke = tradeListGateSmoke, TradeListNegativeHp = tradeListNegativeHp,
          ActionGateSmoke = actionGateSmoke, ActionNegativeHp = actionNegativeHp, AttackDeadGateSmoke = attackDeadGateSmoke,
          AttackRvrGateSmoke = attackRvrGateSmoke, AttackTimingGateSmoke = attackTimingGateSmoke, AttackSkillGateSmoke = attackSkillGateSmoke, AttackPartySkillGateSmoke = attackPartySkillGateSmoke, AttackGoldenShieldCoinSmoke = attackGoldenShieldCoinSmoke, AttackType6ProtectionSmoke = attackType6ProtectionSmoke, AttackMultiTargetSmoke = attackMultiTargetSmoke, AttackViewGateSmoke = attackViewGateSmoke, AttackMixedSlotsSmoke = attackMixedSlotsSmoke, AttackDoubleCriticalSmoke = attackDoubleCriticalSmoke, AttackManaControlSmoke = attackManaControlSmoke, AttackManaGainSmoke = attackManaGainSmoke, AttackSkillHealSmoke = attackSkillHealSmoke,
          ExpectedDoubleModeMultiplier = expectedDoubleModeMultiplier,
          AttackDivineBlessingSmoke = attackDivineBlessingSmoke, AttackDivineBlessingInsufficientManaSmoke = attackDivineBlessingInsufficientManaSmoke,
          AttackDivineBlessingDistantObserverAccountFile = attackDivineBlessingDistantObserverAccountFile, ActionRangeSmoke = actionRangeSmoke,
          ActionIllusionEligibilitySmoke = actionIllusionEligibilitySmoke,
          ActionIllusionManaSmoke = actionIllusionManaSmoke,
          ActionOriginRecallSmoke = actionOriginRecallSmoke,
          MovementRestrictionRecallSmoke = movementRestrictionRecallSmoke,
          ActionRecallDestinationObserverAccountFile = actionRecallDestinationObserverAccountFile,
          ActionRecallDistantObserverAccountFile = actionRecallDistantObserverAccountFile,
          AccountReviewRoot = accountReviewRoot, AccountReviewPeerAccountFile = accountReviewPeerAccountFile,
          AccountReviewActorStateFile = accountReviewActorStateFile, AccountReviewPeerStateFile = accountReviewPeerStateFile,
          AccountReviewActionReadyFile = accountReviewActionReadyFile, AccountReviewActionReleaseFile = accountReviewActionReleaseFile,
          ActionAccountAnalysisSmoke = actionAccountAnalysisSmoke,
          ActionAccountAnalysisThresholdSmoke = actionAccountAnalysisThresholdSmoke,
          AccountAnalysisLoginSmoke = accountAnalysisLoginSmoke,
          AccountReviewUnavailableLoginSmoke = accountReviewUnavailableLoginSmoke,
          ExpectReviewWriteFailure = expectReviewWriteFailure,
          CurrentScoreLoginProjectionSmoke = currentScoreLoginProjectionSmoke,
          CurrentScoreLoginProjectionRejectSmoke = currentScoreLoginProjectionRejectSmoke };
    }

    private static bool IsIsolatedFixture(string path)
    {
        if (!IsAdversarialFixture(path)) return false;
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = Array.FindIndex(parts, value => value.Equals("E2E7670", StringComparison.OrdinalIgnoreCase));
        return root >= 0 && parts.Length == root + 5 && !parts[root + 1].Equals("account", StringComparison.OrdinalIgnoreCase) &&
            parts[root + 2].Equals("account", StringComparison.OrdinalIgnoreCase) && parts[root + 3].Equals("T", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAutoTradeFixture(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = Array.FindIndex(parts, value => value.Equals("AutoTradeCloseSmoke", StringComparison.OrdinalIgnoreCase));
        return root > 1 && parts[root - 1].Equals("Build", StringComparison.OrdinalIgnoreCase) &&
            parts[root - 2].Equals("Backup", StringComparison.OrdinalIgnoreCase) && parts.Length == root + 5 &&
            !parts[root + 1].Equals("account", StringComparison.OrdinalIgnoreCase) &&
            parts[root + 2].Equals("account", StringComparison.OrdinalIgnoreCase) && parts[root + 3].Equals("T", StringComparison.OrdinalIgnoreCase) &&
            System.Text.RegularExpressions.Regex.IsMatch(parts[^1], "^TESTE[1-4]?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    private static bool IsAccountAnalysisFixture(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = Array.FindIndex(parts, value => value.Equals("AutoTradeCloseSmoke", StringComparison.OrdinalIgnoreCase));
        return root > 1 && parts[root - 1].Equals("Build", StringComparison.OrdinalIgnoreCase) &&
            parts[root - 2].Equals("Backup", StringComparison.OrdinalIgnoreCase) && parts.Length == root + 5 &&
            !parts[root + 1].Equals("account", StringComparison.OrdinalIgnoreCase) &&
            parts[root + 2].Equals("account", StringComparison.OrdinalIgnoreCase) && parts[root + 3].Equals("T", StringComparison.OrdinalIgnoreCase) &&
            (parts[^1].Equals("TESTE", StringComparison.OrdinalIgnoreCase) || parts[^1].Equals("TESTE2", StringComparison.OrdinalIgnoreCase) ||
             parts[^1].Equals("TESTE3", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAccountAnalysisStateFile(string stateFile, string fixtureFile)
    {
        var runRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fixtureFile)!, "..", ".."));
        var relative = Path.GetRelativePath(runRoot, Path.GetFullPath(stateFile));
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 4 &&
            (parts[0].Equals("UP", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("PVP", StringComparison.OrdinalIgnoreCase)) &&
            parts[1].Equals("accounts", StringComparison.OrdinalIgnoreCase) &&
            parts[2].Equals("T", StringComparison.OrdinalIgnoreCase) &&
            parts[3].Equals(Path.GetFileName(fixtureFile), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAdversarialFixture(string path) =>
        (Path.GetFileName(path).Equals("TESTE", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(path).Equals("TESTE2", StringComparison.OrdinalIgnoreCase)) &&
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains("E2E7670", StringComparer.OrdinalIgnoreCase);
}

sealed class E2eSession : IAsyncDisposable
{
    private readonly LegacyFrameCodec codec;
    private readonly E2eOptions options;
    private bool loggedOut;

    private E2eSession(TcpClient client, NetworkStream stream, DecodedFrame accountLoginFrame, DecodedFrame characterLoginFrame, LegacyFrameCodec codec, E2eOptions options)
    {
        Client = client;
        Stream = stream;
        AccountLoginFrame = accountLoginFrame;
        CharacterLoginFrame = characterLoginFrame;
        this.codec = codec;
        this.options = options;
    }

    public TcpClient Client { get; }
    public NetworkStream Stream { get; }
    public DecodedFrame AccountLoginFrame { get; }
    public DecodedFrame CharacterLoginFrame { get; }

    public static async ValueTask<E2eSession> ConnectAndLoginAsync(E2eOptions options, AccountHeader accountHeader, LegacyFrameCodec codec, string label, bool verifyQuitTradeModeGate = false, bool verifyMotionModeGate = false, bool verifyTradeListModeGate = false, bool verifyActionModeGate = false)
    {
        var client = new TcpClient
        {
            ReceiveTimeout = (int)options.Timeout.TotalMilliseconds,
            SendTimeout = (int)options.Timeout.TotalMilliseconds,
        };

        try
        {
            await client.ConnectAsync(options.Host, options.Port);
            var stream = client.GetStream();

            var handshake = new byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32LittleEndian(handshake, LegacyFrameCodec.InitCode);
            await stream.WriteAsync(handshake);

            async Task VerifyQuitTradeModeGateAsync()
            {
                // Ordered same-stream logout acknowledgment is a causal barrier, not a sleep.
                await stream.WriteAsync(TradeCloseRequest.ToFrame(codec, 33, 16, options.ConnectionId));
                await SendAsync(stream, codec, CharacterLogoutRequest.MessageType, [], options.ConnectionId);
                var reply = await ReadFrameAsync(stream, codec, options.Timeout);
                Require(reply, CharacterLogoutConfirmation.MessageType, CharacterLogoutConfirmation.PacketSize, "nonplaying QuitTrade barrier");
                Console.WriteLine($"{label}QUIT_MODE_GATE_PASS no-hp-no-cancel-no-pk");
            }
            if (verifyQuitTradeModeGate) await VerifyQuitTradeModeGateAsync();

            async Task VerifyMotionModeGateAsync()
            {
                await stream.WriteAsync(new MotionRequest(7, 19, 0x12345678).ToFrame(codec, 35, 18, options.ConnectionId));
                await SendAsync(stream, codec, CharacterLogoutRequest.MessageType, [], options.ConnectionId);
                var reply = await ReadFrameAsync(stream, codec, options.Timeout);
                Require(reply, CharacterLogoutConfirmation.MessageType, CharacterLogoutConfirmation.PacketSize, "nonplaying Motion barrier");
                Console.WriteLine($"{label}MOTION_MODE_GATE_PASS no-hp-no-motion");
            }
            if (verifyMotionModeGate) await VerifyMotionModeGateAsync();

            var loginPayload = new byte[AccountLoginRequest.PacketSize - PacketHeader.SizeInBytes];
            async Task VerifyActionModeGateAsync()
            {
                foreach (var type in new[] { ActionRequest.MessageType, ActionRequest.StopMessageType, ActionRequest.IllusionMessageType })
                    await stream.WriteAsync(new ActionRequest(2100, 2100, 0, 2, new byte[24], 2101, 2100) { WireType = type }.ToFrame(codec, 37, 20, options.ConnectionId));
                // QuitTrade exposes the suppressed type3 counter, then logout supplies a causal barrier.
                await stream.WriteAsync(TradeCloseRequest.ToFrame(codec, 38, 21, options.ConnectionId));
                await SendAsync(stream, codec, CharacterLogoutRequest.MessageType, [], options.ConnectionId);
                var reply = await ReadFrameAsync(stream, codec, options.Timeout);
                Require(reply, CharacterLogoutConfirmation.MessageType, CharacterLogoutConfirmation.PacketSize, "nonplaying Action barrier");
                Console.WriteLine($"{label}ACTION_MODE_GATE_PASS no-hp-no-action");
            }
            if (verifyActionModeGate) await VerifyActionModeGateAsync();
            async Task VerifyTradeListModeGateAsync()
            {
                await stream.WriteAsync(new TradeListRequest(0).ToFrame(codec, 36, 19, options.ConnectionId));
                await SendAsync(stream, codec, CharacterLogoutRequest.MessageType, [], options.ConnectionId);
                var reply = await ReadFrameAsync(stream, codec, options.Timeout);
                Require(reply, CharacterLogoutConfirmation.MessageType, CharacterLogoutConfirmation.PacketSize, "nonplaying ReqTradeList barrier");
                Console.WriteLine($"{label}LIST_MODE_GATE_PASS no-hp-no-list");
            }
            if (verifyTradeListModeGate) await VerifyTradeListModeGateAsync();
            WriteCString(loginPayload.AsSpan(0, 12), accountHeader.Password);
            WriteCString(loginPayload.AsSpan(12, 16), accountHeader.AccountName);
            BinaryPrimitives.WriteInt32LittleEndian(loginPayload.AsSpan(80), ClientReleasePolicy.RequiredClientVersion);
            BinaryPrimitives.WriteInt32LittleEndian(loginPayload.AsSpan(84), 0);
            await SendAsync(stream, codec, AccountLoginRequest.MessageType, loginPayload, options.ConnectionId);

            var loginResponse = await ReadFrameAsync(stream, codec, options.Timeout);
            PrintFrame($"{label}LOGIN_RX", loginResponse);
            Require(loginResponse, AccountLoginConfirmationV769.MessageType, AccountLoginConfirmationV769.PacketSize, "account login confirmation");
            if (verifyQuitTradeModeGate) await VerifyQuitTradeModeGateAsync();

            if (verifyMotionModeGate) await VerifyMotionModeGateAsync();

            var securePayload = new byte[AccountSecureRequest.PacketSize - PacketHeader.SizeInBytes];
            if (verifyTradeListModeGate) await VerifyTradeListModeGateAsync();
            accountHeader.NumericToken.CopyTo(securePayload, 0);
            if (verifyActionModeGate) await VerifyActionModeGateAsync();
            BinaryPrimitives.WriteInt32LittleEndian(securePayload.AsSpan(16), 0);
            await SendAsync(stream, codec, AccountSecureRequest.MessageType, securePayload, options.ConnectionId);
            var secureResponse = await ReadFrameAsync(stream, codec, options.Timeout);
            PrintFrame($"{label}SECURE_RX", secureResponse);
            Require(secureResponse, AccountSecureSignal.SuccessType, PacketHeader.SizeInBytes, "account secure confirmation");

            var characterLoginPayload = new byte[CharacterLoginRequest.Retail760PacketSize - PacketHeader.SizeInBytes];
            BinaryPrimitives.WriteInt32LittleEndian(characterLoginPayload.AsSpan(0), options.Slot);
            BinaryPrimitives.WriteInt32LittleEndian(characterLoginPayload.AsSpan(4), 0);
            await SendAsync(stream, codec, CharacterLoginRequest.MessageType, characterLoginPayload, options.ConnectionId);

            DecodedFrame? characterLogin = null;
            for (var index = 0; index < 12; index++)
            {
                var response = await ReadFrameAsync(stream, codec, options.Timeout);
                PrintFrame($"{label}CHAR_RX[{index}]", response);
                if (response.Header.Type == CharacterLoginConfirmationV769.MessageType)
                {
                    Require(response, CharacterLoginConfirmationV769.MessageType, CharacterLoginConfirmationV769.PacketSize, "character login confirmation");
                    characterLogin = response;
                    break;
                }
            }

            Ensure(characterLogin is not null, "character login confirmation was not received.");
            return new E2eSession(client, stream, loginResponse, characterLogin ?? throw new InvalidOperationException("character login confirmation was not received."), codec, options);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public async ValueTask LogoutAsync()
    {
        if (loggedOut)
            return;

        await SendAsync(Stream, codec, CharacterLogoutRequest.MessageType, Array.Empty<byte>(), options.ConnectionId);
        var logoutConfirmed = false;
        for (var index = 0; index < 12; index++)
        {
            var response = await ReadFrameAsync(Stream, codec, options.Timeout);
            PrintFrame($"LOGOUT_RX[{index}]", response);
            if (response.Header.Type == CharacterLogoutConfirmation.MessageType)
            {
                Require(response, CharacterLogoutConfirmation.MessageType, CharacterLogoutConfirmation.PacketSize, "character logout confirmation");
                logoutConfirmed = true;
                break;
            }
        }

        Ensure(logoutConfirmed, "character logout confirmation was not received.");
        loggedOut = true;
    }

    private static async ValueTask SendAsync(NetworkStream stream, LegacyFrameCodec codec, ushort type, byte[] payload, ushort id)
    {
        var frame = codec.Encode(type, id, 33, payload, 16);
        await stream.WriteAsync(frame);
    }

    private static async ValueTask<DecodedFrame> ReadFrameAsync(NetworkStream stream, LegacyFrameCodec codec, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        var headerBytes = new byte[PacketHeader.SizeInBytes];
        await stream.ReadExactlyAsync(headerBytes, cancellation.Token);
        var header = PacketHeader.Read(headerBytes);
        if (header.Size < PacketHeader.SizeInBytes || header.Size > PacketHeader.MaxPacketSize)
            throw new InvalidDataException($"Invalid response frame size {header.Size}.");

        var frame = new byte[header.Size];
        headerBytes.CopyTo(frame, 0);
        await stream.ReadExactlyAsync(frame.AsMemory(PacketHeader.SizeInBytes), cancellation.Token);
        return codec.Decode(frame);
    }

    private static void PrintFrame(string label, DecodedFrame frame) =>
        Console.WriteLine($"{label} type=0x{frame.Header.Type:X4} size={frame.Header.Size} checksum={frame.IsChecksumValid}");

    private static void Require(DecodedFrame frame, ushort type, int size, string label)
    {
        Ensure(frame.IsChecksumValid, $"{label} has an invalid checksum.");
        Ensure(frame.Header.Type == type, $"Expected {label} type=0x{type:X4}, received 0x{frame.Header.Type:X4}.");
        Ensure(frame.Header.Size == size, $"Expected {label} size={size}, received {frame.Header.Size}.");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void WriteCString(Span<byte> destination, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        if (bytes.Length >= destination.Length)
            throw new InvalidDataException($"Account field does not fit in {destination.Length} bytes.");
        bytes.CopyTo(destination);
    }

    public ValueTask DisposeAsync()
    {
        Stream.Dispose();
        Client.Dispose();
        return ValueTask.CompletedTask;
    }
}

readonly record struct AccountHeader(string AccountName, string Password, byte[] NumericToken);

static class LegacyAccountSnapshotLayout
{
    public const int NumericTokenOffset = 202;
    public const int NumericTokenLength = 6;
}

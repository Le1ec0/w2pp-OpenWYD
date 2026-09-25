using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using WydCdk.Protocol;
using WydCdk.World;

var tests = new (string Name, Action Run)[] { ("header round-trip", HeaderRoundTrip), ("frame round-trip with real legacy table", FrameRoundTrip), ("stream accepts partial and concatenated frames", StreamFraming), ("checksum detects incompatible key table", ChecksumDetectsIncompatibleKeyTable), ("account login parser", AccountLoginParser), ("create character parser", CreateCharacterParser), ("delete character wire", DeleteCharacterWire), ("character logout wire", CharacterLogoutWire), ("login session transitions", LoginSessionTransitions), ("legacy account store is read-only", LegacyAccountStoreIsReadOnly), ("account login coordinator", AccountLoginCoordinatorFlow), ("character selection wire layout", CharacterSelectionWireLayout), ("account login confirmation wire layout", AccountLoginConfirmationWireLayout), ("new character confirmation wire layout", NewCharacterConfirmationWireLayout), ("legacy account snapshot reads fixture", LegacyAccountSnapshotReadsFixture), ("legacy character name validation", LegacyCharacterNameValidation), ("legacy character template store reads class files", LegacyCharacterTemplateStoreReadsClassFiles), ("create character coordinator persists character", CreateCharacterCoordinatorPersistsCharacter), ("create character coordinator rejects invalid requests", CreateCharacterCoordinatorRejectsInvalidRequests), ("character login parser", CharacterLoginParser), ("character login confirmation wire layout", CharacterLoginConfirmationWireLayout), ("character login coordinator reads persisted character", CharacterLoginCoordinatorReadsPersistedCharacter), ("character login coordinator rejects invalid requests", CharacterLoginCoordinatorRejectsInvalidRequests), ("account secure parser", AccountSecureParser), ("account secure signal wire layout", AccountSecureSignalWireLayout), ("account secure coordinator first time setup", AccountSecureCoordinatorFirstTimeSetup), ("account secure coordinator verifies and changes pin", AccountSecureCoordinatorVerifiesAndChangesPin), ("account secure coordinator rejects wrong pin and unverified change", AccountSecureCoordinatorRejectsWrongPinAndUnverifiedChange) };
tests = tests.Append(("client tick policy", ClientTickPolicyRules)).Append(("attack timing gate", AttackTimingGate)).Append(("attack parser", AttackParser)).Append(("attack response wire", AttackResponseWire)).Append(("attack authoritative pipeline", AttackAuthoritativePipeline)).Append(("set hp mp wire", SetHpMpWire)).Append(("set hp mode wire", SetHpModeWire)).Append(("send item wire", SendItemWire)).Append(("update carry wire", UpdateCarryWire)).Append(("physical combat formula", PhysicalCombatFormula)).Append(("skill combat formula", SkillCombatFormula)).Append(("experience formula", ExperienceFormula)).Append(("weapon damage formula", WeaponDamageFormula)).Append(("item data table", ItemDataTable)).Append(("item bonus math", ItemBonusMath)).Append(("item refinement bonus math", ItemRefinementBonusMath)).Append(("use item wire and class reset", UseItemWireAndClassReset)).Append(("world physical attack", WorldPhysicalAttack)).Append(("world npc experience", WorldNpcExperience)).Append(("world npc skill", WorldNpcSkill)).Append(("world npc party experience", WorldNpcPartyExperience)).Append(("world experience hold", WorldExperienceHold)).Append(("party experience modifiers", PartyExperienceModifiers)).Append(("experience bonus", ExperienceBonus)).Append(("world skill attack", WorldSkillAttack)).Append(("world summon skill", WorldSummonSkill)).Append(("world npc summon", WorldNpcSummon)).Append(("world ethereal flames", WorldEtherealFlames)).Append(("skill data table", SkillDataTable)).Append(("skill attack gate", SkillAttackGate)).Append(("legacy mob combat layout", LegacyMobCombatLayout)).Append(("legacy map grid attributes", LegacyMapGridAttributes)).Append(("legacy release map recall", LegacyReleaseMapRecall)).Append(("character login spawn", CharacterLoginSpawnPosition)).Append(("legacy guild zone state", LegacyGuildZoneStateFile)).Append(("short skill wire and persistence", ShortSkillWireAndPersistence)).Append(("action parser", ActionParser)).Append(("motion wire", MotionWire)).Append(("world hub broadcast", WorldHubBroadcast)).Append(("world skill mana mutation", WorldSkillManaMutation)).Append(("world guild invite", WorldGuildInvite)).Append(("guild invite visual update", GuildInviteVisualUpdate)).Append(("create mob wire", CreateMobWire)).Append(("initial world state wire", InitialWorldStateWire)).Append(("guild invite wire", GuildInviteWire)).Append(("message panel wire", MessagePanelWire)).Append(("pk info wire", PkInfoWire)).Append(("npc chat wire", NpcChatWire)).Append(("message chat wire", MessageChatWire)).Append(("world player chat view", WorldPlayerChatView)).Append(("message whisper wire", MessageWhisperWire)).Append(("world whisper target", WorldWhisperTarget)).Append(("get item wire", GetItemWire)).Append(("world ground item pickup", WorldGroundItemPickup)).Append(("donate shop wire", DonateShopWire)).Append(("donate shop client state", DonateShopClientStateFlow)).Append(("donate shop catalog and purchase", DonateShopCatalogAndPurchase)).ToArray();
tests = tests.Append(("party wire", PartyWire)).Append(("world party lifecycle", WorldPartyLifecycle)).ToArray();
tests = tests.Append(("world npc common drop", WorldNpcCommonDrop)).Append(("world npc boss drop", WorldNpcBossDrop)).Append(("castle quest configuration", CastleQuestConfiguration)).Append(("castle quest rewards", CastleQuestRewards)).Append(("npc generation catalog", NpcGenerationCatalogFlow)).Append(("pista mob-left wire and area", PistaMobLeftWireAndArea)).Append(("quest request parser", QuestRequestParser)).Append(("pista registration flow", PistaRegistrationFlow)).Append(("pista entry schedule", PistaEntrySchedule)).Append(("pista fixed entry spawns", PistaFixedEntrySpawns)).Append(("pista entry generator matrix", PistaEntryGeneratorMatrix)).Append(("pista level 0 retry", PistaLevel0Retry)).Append(("pista level 2 reward", PistaLevel2Reward)).Append(("pista level 1 counters", PistaLevel1Counters)).Append(("pista level 1 exit reward", PistaLevel1ExitReward)).Append(("pista level 4 progression", PistaLevel4Progression)).Append(("pista level 3 progression", PistaLevel3Progression)).Append(("pista level 5 progression", PistaLevel5Progression)).ToArray();
tests = tests.Append(("world trade disconnect cleanup", WorldTradeDisconnectCleanup)).ToArray();
tests = tests.Append(("autotrade list gates", AutoTradeListGates)).ToArray();
tests = tests.Append(("autotrade location rules", AutoTradeLocationRules)).ToArray();
tests = tests.Append(("autotrade listing wire", AutoTradeListingWire)).ToArray();
tests = tests.Append(("autotrade start request wire", AutoTradeStartRequestWire)).Append(("world autotrade book", WorldAutoTradeBook)).Append(("autotrade runtime mutation", AutoTradeRuntimeMutation)).Append(("autotrade purchase executor", AutoTradePurchaseExecutor)).Append(("autotrade purchase coordinator", AutoTradePurchaseCoordinator)).Append(("autotrade file purchase commit", AutoTradeFilePurchaseCommit)).Append(("autotrade purchase relay", AutoTradePurchaseRelay)).ToArray();
tests = tests.Append(("autotrade purchase wire and gates", AutoTradePurchaseWireAndGates)).Append(("autotrade sale notification and settlement", AutoTradeSaleNotificationAndSettlement)).Append(("autotrade purchase commit contract", AutoTradePurchaseCommitContract)).ToArray();
tests = tests.Append(("autotrade list relay", AutoTradeListRelay)).ToArray();
tests = tests.Append(("world invisibility", WorldInvisibility)).Append(("server mode policy", ServerModePolicyFlow)).Append(("donate shop NPC target", DonateShopNpcTarget)).Append(("city Perzen NPCs", CityPerzenNpcs)).Append(("Perzen exchange", PerzenExchange)).Append(("donate shop rate limiter", DonateShopRateLimiter)).Append(("donate shop purchase messages", DonateShopPurchaseMessages)).Append(("account login failure notice", AccountLoginFailureNoticeFlow)).ToArray();
tests = tests.Append(("drop item wire", DropItemWire)).Append(("world ground item drop", WorldGroundItemDrop)).Append(("trading item wire", TradingItemWire)).Append(("world carry item swap", WorldCarryItemSwap)).Append(("world equipment item swap", WorldEquipmentItemSwap)).Append(("delete item wire", DeleteItemWire)).Append(("world delete carry item", WorldDeleteCarryItem)).Append(("split item wire", SplitItemWire)).Append(("world split item", WorldSplitItem)).Append(("update item wire", UpdateItemWire)).Append(("start time signal wire", StartTimeSignalWire)).Append(("official ground mask", OfficialGroundMaskTable)).Append(("map item catalog and ground mask", MapItemCatalogAndGroundMask)).Append(("world map item update", WorldMapItemUpdate)).Append(("map item minute timer", MapItemMinuteTimer)).Append(("ground item decay timer", GroundItemDecayTimer)).Append(("castle gate update", CastleGateUpdate)).Append(("server status file publisher", ServerStatusFilePublisherFlow)).ToArray();
tests = tests.Append(("world carry stack merge", WorldCarryItemMerge)).Append(("world trading item carry compatibility", WorldTradingItemCarryCompatibility)).ToArray();
tests = tests.Append(("legacy equipment eligibility", LegacyEquipmentEligibilityRules)).ToArray();
tests = tests.Append(("legacy movement map gate", LegacyMovementMapGate)).ToArray();
tests = tests.Append(("legacy mob ability aggregation", LegacyMobAbilityAggregation)).Append(("legacy mob ability includes client extension slots", LegacyMobAbilityIncludesClientExtensionSlots)).ToArray();
tests = tests.Append(("world client 18-slot equipment state", WorldClient18SlotEquipmentState)).ToArray();
tests = tests.Append(("legacy current score base equipment stage", LegacyCurrentScoreBaseEquipmentStage)).ToArray();
tests = tests.Append(("legacy current score special stage", LegacyCurrentScoreSpecialStage)).ToArray();
tests = tests.Append(("legacy login HP MP base stage", LegacyLoginHpMpBaseStage)).ToArray();
tests = tests.Append(("legacy login equipment ability stage", LegacyLoginEquipmentAbilityStage)).ToArray();
tests = tests.Append(("legacy login final clamps and resistances", LegacyLoginFinalScalarClamps)).ToArray();
tests = tests.Append(("legacy login resistance affect stage", LegacyLoginResistanceAffectStage)).ToArray();
tests = tests.Append(("legacy login final damage stage", LegacyLoginFinalDamageStage)).ToArray();
tests = tests.Append(("legacy login final attack run stage", LegacyLoginFinalAttackRunStage)).ToArray();
tests = tests.Append(("legacy login mount run floors", LegacyLoginMountRunFloors)).ToArray();
tests = tests.Append(("legacy login haste affect stage", LegacyLoginHasteAffectStage)).ToArray();
tests = tests.Append(("legacy login Holy Touch affect stage", LegacyLoginHolyTouchAffectStage)).ToArray();
tests = tests.Append(("legacy login Possessed affect stage", LegacyLoginPossessedAffectStage)).ToArray();
tests = tests.Append(("legacy login Assault affect stage", LegacyLoginAssaultAffectStage)).ToArray();
tests = tests.Append(("legacy login Samaritan ArmorClass affect stage", LegacyLoginSamaritanArmorClassAffectStage)).ToArray();
tests = tests.Append(("legacy login magic shield affect stage", LegacyLoginMagicShieldAffectStage)).ToArray();
tests = tests.Append(("legacy login magic weapon affect stage", LegacyLoginMagicWeaponAffectStage)).ToArray();
tests = tests.Append(("legacy login Athena Touch affect stage", LegacyLoginAthenaTouchAffectStage)).ToArray();
tests = tests.Append(("legacy login Fanaticism affect stage", LegacyLoginFanaticismAffectStage)).ToArray();
tests = tests.Append(("legacy login Dexterity affect stage", LegacyLoginDexterityAffectStage)).ToArray();
tests = tests.Append(("legacy login ArmorClass reduction affect stage", LegacyLoginArmorClassReductionAffectStage)).ToArray();
tests = tests.Append(("legacy login transformation speed affect stage", LegacyLoginTransformationSpeedAffectStage)).ToArray();
tests = tests.Append(("legacy login transformation damage affect stage", LegacyLoginTransformationDamageAffectStage)).ToArray();
tests = tests.Append(("legacy login transformation armor class affect stage", LegacyLoginTransformationArmorClassAffectStage)).ToArray();
tests = tests.Append(("legacy login transformation max hp affect stage", LegacyLoginTransformationMaxHpAffectStage)).ToArray();
tests = tests.Append(("legacy login transformation critical and equipment stages", LegacyLoginTransformationCriticalAndEquipmentStages)).ToArray();
tests = tests.Append(("legacy login transformation affect composition stage", LegacyLoginTransformationAffectCompositionStage)).ToArray();
tests = tests.Append(("legacy login score coordinator stage", LegacyLoginScoreCoordinatorStage)).ToArray();
tests = tests.Append(("legacy login Soul HP MP stage", LegacyLoginSoulHealthManaStage)).ToArray();
tests = tests.Append(("legacy login Kibita Soul stage", LegacyLoginKibitaSoulStage)).ToArray();
tests = tests.Append(("7.69 character selection golden", CharacterSelectionV769Golden))
    .Append(("W2PP selection to 7.69 adapter", W2ppCharacterSelectionAdapter))
    .Append(("7.69 account login golden", AccountLoginV769Golden))
    .Append(("7.69 character login golden", CharacterLoginV769Golden))
    .Append(("7.69 client equipment state wire", ClientEquipmentStateV769Wire))
    .Append(("W2PP MOB to 7.69 character login projection", W2ppCharacterMobProjection))
    .Append(("W2PP MOB projection accepts client 18-slot equipment", W2ppCharacterMobProjectionWithClientEquipment))
    .Append(("W2PP character login extensions to 7.69 adapter", W2ppCharacterLoginExtensionsProjection))
    .Append(("W2PP character login data to 7.69 confirmation", W2ppCharacterLoginComposition))
    .Append(("W2PP account login to 7.69 adapter", W2ppAccountLoginAdapterProjection))
    .Append(("7.69 UpdateEquip golden and W2PP adapter", UpdateEquipV769Golden))
    .Append(("7.69 CreateMob golden and W2PP adapter", CreateMobV769Golden))
    .Append(("7.69 CreateMobTrade wire", CreateMobTradeV769Wire))
    .Append(("W2PP CreateMobTrade to 7.69 adapter", W2ppCreateMobTradeAdapter))
    .Append(("autotrade visual relay", AutoTradeVisualRelay))
    .Append(("autotrade removal relay", AutoTradeRemovalRelay))
    .Append(("autotrade state file store", AutoTradeStateFileStore))
    .Append(("autotrade offline rehydration", AutoTradeOfflineRehydration))
    .Append(("autotrade reconnect cleanup", AutoTradeReconnectCleanup))
    .Append(("7.69 RemoveMob golden", RemoveMobV769Golden))
    .Append(("7.69 UpdateAffect golden and W2PP adapter", UpdateAffectV769Golden))
    .Append(("7.69 UpdateScore golden and W2PP adapter", UpdateScoreV769Golden))
    .Append(("7.69 UpdateEtc golden and W2PP adapter", UpdateEtcV769Golden))
    .Append(("server ItemList 140-byte layout", ServerItemDataTable))
    .Append(("serialized network stream concurrent writes", SerializedNetworkStreamConcurrentWrites))
    .Append(("trade wire", TradeWire))
    .Append(("world bilateral trade state", WorldBilateralTradeState))
    .Append(("world trade completion", WorldTradeCompletion))
    .Append(("legacy file atomic trade persistence", LegacyFileAtomicTradePersistence))
    .ToArray();
foreach (var test in tests) { test.Run(); Console.WriteLine($"PASS {test.Name}"); }

static void ServerStatusFilePublisherFlow()
{
    var root = Path.Combine(Path.GetTempPath(), "wyd-cdk-status-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var path = Path.Combine(root, "servtest.htm");
    File.WriteAllText(path, "10\n11\n12\n13\n14\n15\n16\n17\n18\n19\n");
    try
    {
        var publisher = new ServerStatusFilePublisher(path, 2);
        publisher.PublishOnline(7);
        var online = File.ReadAllText(path).Split("\\n", StringSplitOptions.RemoveEmptyEntries);
        Assert(online.Length == 10 && online[0] == "1" && online[1] == "11" && online[2] == "7", "The online status update changed the wrong slots.");
        Assert(File.Exists(publisher.HeartbeatPath), "The online status update did not create a heartbeat marker.");

        File.WriteAllText(path, "1\n0\n12\n13\n14\n15\n16\n17\n18\n19\n");
        new ServerStatusFilePublisher(path, 1).PublishOnline(0);
        var zero = File.ReadAllText(path).Split("\\n", StringSplitOptions.RemoveEmptyEntries);
        Assert(zero[1] == "0", "An online empty channel must preserve the real zero-player count.");

        publisher.PublishOffline();
        var offline = File.ReadAllText(path).Split("\\n", StringSplitOptions.RemoveEmptyEntries);
        Assert(offline[2] == "-1" && offline[0] == "1" && offline[1] == "0", "The offline status update did not preserve the other channel, including zero players.");
        Assert(!File.Exists(publisher.HeartbeatPath), "The offline status update did not remove the heartbeat marker.");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static void SerializedNetworkStreamConcurrentWrites()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();

    try
    {
        using var sender = new TcpClient();
        var acceptedTask = listener.AcceptTcpClientAsync();
        sender.Connect((IPEndPoint)listener.LocalEndpoint);
        using var receiver = acceptedTask.GetAwaiter().GetResult();
        using var serialized = new SerializedNetworkStream(sender.GetStream());

        var first = Enumerable.Repeat((byte)0xA1, 256 * 1024).ToArray();
        var second = Enumerable.Repeat((byte)0xB2, 256 * 1024).ToArray();
        var received = new byte[first.Length + second.Length];
        var barrier = new Barrier(2);

        var firstWrite = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            await serialized.WriteAsync(first, CancellationToken.None);
        });
        var secondWrite = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            await serialized.WriteAsync(second, CancellationToken.None);
        });
        var read = ReadExactlyAsync(receiver.GetStream(), received);

        Task.WhenAll(firstWrite, secondWrite, read).GetAwaiter().GetResult();

        var firstThenSecond = received.AsSpan(0, first.Length).SequenceEqual(first)
            && received.AsSpan(first.Length).SequenceEqual(second);
        var secondThenFirst = received.AsSpan(0, second.Length).SequenceEqual(second)
            && received.AsSpan(second.Length).SequenceEqual(first);
        Assert(firstThenSecond || secondThenFirst, "Concurrent writes were interleaved instead of producing two complete frames.");
    }
    finally
    {
        listener.Stop();
    }
}

static void TradeWire()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var items = Enumerable.Range(0, TradeOfferRequest.ItemCount)
        .Select(index => new LegacyItem((short)(500 + index), 1, (byte)index, 2, 3, 4, 5))
        .ToArray();
    var inventoryPositions = Enumerable.Repeat((sbyte)-1, TradeOfferRequest.ItemCount).ToArray();
    inventoryPositions[0] = 7;
    inventoryPositions[14] = 119;
    var offer = new TradeOfferRequest(items, inventoryPositions, 1_234_567, 1, 321, 0xA5, 0x5A);
    var offerFrame = codec.Decode(offer.ToFrame(codec, 41, 22, 17));
    Assert(offerFrame.IsChecksumValid && offerFrame.Header.Type == TradeOfferRequest.MessageType && offerFrame.Header.Size == TradeOfferRequest.PacketSize && offerFrame.Header.Id == 17 && TradeOfferRequest.PacketSize == 156 && TradeOfferRequest.PayloadSize == 144, "MSG_Trade did not preserve the confirmed 156-byte x86 frame size and header.");
    Assert(TradeOfferRequest.TryParse(offerFrame, out var parsedOffer) && parsedOffer is not null && parsedOffer.TradeMoney == 1_234_567 && parsedOffer.MyCheck == 1 && parsedOffer.OpponentId == 321 && parsedOffer.AbiPaddingBeforeTradeMoney == 0xA5 && parsedOffer.AbiPaddingAfterCheck == 0x5A, "MSG_Trade did not round-trip the offer scalars and ABI padding.");
    Assert(parsedOffer!.Items.SequenceEqual(items) && parsedOffer.InventoryPositions.SequenceEqual(inventoryPositions), "MSG_Trade did not round-trip all 15 items and inventory positions.");
    Assert(offerFrame.Payload.Span[TradeOfferRequest.AbiPaddingBeforeTradeMoneyOffset] == 0xA5 && offerFrame.Payload.Span[TradeOfferRequest.AbiPaddingAfterCheckOffset] == 0x5A, "MSG_Trade padding offsets differ from the native struct layout.");

    var listFrame = codec.Decode(new TradeListRequest(12).ToFrame(codec, 41, 22, 9));
    Assert(TradeListRequest.TryParse(listFrame, out var list) && list is not null && list.TargetId == 12 && listFrame.Header.Type == TradeListRequest.MessageType && listFrame.Header.Size == TradeListRequest.PacketSize, "MSG_ReqTradeList did not round-trip its target parameter.");

    var closeFrame = codec.Decode(TradeCloseRequest.ToFrame(codec, 41, 22, 9));
    Assert(TradeCloseRequest.IsValid(closeFrame) && closeFrame.Header.Type == TradeCloseRequest.MessageType && closeFrame.Header.Size == TradeCloseRequest.PacketSize, "MSG_QuitTrade did not preserve the empty MSG_STANDARD wire.");

    var checkFrame = codec.Decode(TradeCheckConfirmation.ToFrame(codec, 41, 22, 9));
    Assert(TradeCheckConfirmation.IsValid(checkFrame) && checkFrame.Header.Type == TradeCheckConfirmation.MessageType && checkFrame.Header.Size == TradeCheckConfirmation.PacketSize, "MSG_CNFCheck did not preserve the empty trade-confirmation signal.");
}

static void AutoTradeListGates()
{
    var atEdge = new LegacyAutoTradeListContext(
        RequesterHp: 1,
        RequesterInPlay: true,
        TargetExists: true,
        TargetInPlay: true,
        TargetAutoTradeActive: true,
        RequesterX: 2_000,
        RequesterY: 2_000,
        TargetX: 2_033,
        TargetY: 1_967);
    Assert(LegacyAutoTradeListRules.Validate(12, atEdge) == LegacyAutoTradeListResult.Accepted, "The autotrade list gate rejected the inclusive VIEWGRID edge.");
    Assert(LegacyAutoTradeListRules.Validate(12, atEdge with { TargetX = 2_034 }) == LegacyAutoTradeListResult.OutOfRange, "The autotrade list gate accepted a target one cell beyond VIEWGRIDX.");
    Assert(LegacyAutoTradeListRules.Validate(12, atEdge with { TargetAutoTradeActive = false }) == LegacyAutoTradeListResult.TargetNotInAutoTrade, "The autotrade list gate accepted an inactive target.");
    Assert(LegacyAutoTradeListRules.Validate(12, atEdge with { TargetInPlay = false }) == LegacyAutoTradeListResult.TargetNotPlaying, "The autotrade list gate accepted a target outside USER_PLAY.");
    Assert(LegacyAutoTradeListRules.Validate(12, atEdge with { TargetInPlay = false, TargetOfflineAutoTrade = true }) == LegacyAutoTradeListResult.Accepted, "The autotrade list gate rejected an explicitly rehydrated offline shop.");
    Assert(LegacyAutoTradeListRules.Validate(0, atEdge) == LegacyAutoTradeListResult.TargetNotFound, "The autotrade list gate accepted an invalid target id.");
    Assert(LegacyAutoTradeListRules.Validate(12, atEdge with { RequesterHp = 0 }) == LegacyAutoTradeListResult.RequesterNotAlive, "The autotrade list gate accepted a dead requester.");
    Assert(LegacyAutoTradeListRules.Validate(12, atEdge with { RequesterInPlay = false }) == LegacyAutoTradeListResult.RequesterNotPlaying, "The autotrade list gate accepted a requester outside USER_PLAY.");
}

static void AutoTradeLocationRules()
{
    Assert(LegacyAutoTradeLocationRules.TryResolve(2_100, 2_100, null, out var armia, out var defaultTax) &&
        armia == 0 && defaultTax == LegacyAutoTradeLocationRules.DefaultCityTax,
        "The autotrade location resolver did not use the Armia city bounds and default tax.");

    var taxes = new[] { 7, 8, 9, 10, 11 };
    Assert(LegacyAutoTradeLocationRules.TryResolve(2_500, 1_700, taxes, out var azran, out var configuredTax) &&
        azran == 1 && configuredTax == 8,
        "The autotrade location resolver did not select the configured city tax by village.");
    Assert(!LegacyAutoTradeLocationRules.TryResolve(2_123, 2_139, taxes, out _, out _),
        "The protected Armia area was accepted for autotrade.");
    Assert(!LegacyAutoTradeLocationRules.TryResolve(1_000, 1_000, taxes, out _, out _),
        "A coordinate outside all five city limits was accepted for autotrade.");
}

static void AutoTradeListingWire()
{
    var items = Enumerable.Range(0, AutoTradeListConfirmation.SlotCount)
        .Select(index => index == 0 ? new LegacyItem(900, 1, 2, 3, 4, 5, 6) : default)
        .ToArray();
    var carryPositions = new sbyte[] { -1, 7, -1, 119, -1, -1, -1, -1, -1, -1, -1, -1 };
    var tradeMoney = new[] { 100, 200, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
    var confirmation = new AutoTradeListConfirmation("AFK SHOP", items, carryPositions, tradeMoney, tax: 19, targetId: 321);
    var payload = confirmation.ToPayload();
    Assert(AutoTradeListConfirmation.PayloadSize == 184 && AutoTradeListConfirmation.PacketSize == 196, "MSG_SendAutoTrade did not preserve the 184-byte payload and 196-byte frame layout.");

    var expected = new byte[AutoTradeListConfirmation.PayloadSize];
    new byte[] { (byte)'A', (byte)'F', (byte)'K', (byte)' ', (byte)'S', (byte)'H', (byte)'O', (byte)'P' }.CopyTo(expected, AutoTradeListConfirmation.DescriptionOffset);
    items[0].Write(expected.AsSpan(AutoTradeListConfirmation.ItemOffset, LegacyItem.SizeInBytes));
    for (var index = 0; index < carryPositions.Length; index++)
        expected[AutoTradeListConfirmation.CarryPositionOffset + index] = unchecked((byte)carryPositions[index]);
    for (var index = 0; index < tradeMoney.Length; index++)
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(AutoTradeListConfirmation.TradeMoneyOffset + (index * sizeof(int))), tradeMoney[index]);
    BinaryPrimitives.WriteInt16LittleEndian(expected.AsSpan(AutoTradeListConfirmation.TaxOffset), 19);
    BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(AutoTradeListConfirmation.TargetIdOffset), 321);
    Assert(payload.SequenceEqual(expected), "MSG_SendAutoTrade payload bytes differ from the client 7.69 field layout.");

    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(confirmation.ToFrame(codec, 41, 22, headerId: 44));
    Assert(frame.IsChecksumValid && frame.Header.Type == AutoTradeListConfirmation.MessageType && frame.Header.Size == AutoTradeListConfirmation.PacketSize && frame.Header.Id == 44, "MSG_AutoTrade did not preserve the encoded frame header.");
    Assert(AutoTradeListConfirmation.TryParse(frame, out var parsed) && parsed is not null && parsed.Description == "AFK SHOP" && parsed.TargetId == 321 && parsed.Tax == 19, "MSG_AutoTrade did not parse its description, target and tax.");
    Assert(parsed!.Items.SequenceEqual(items) && parsed.CarryPositions.SequenceEqual(carryPositions) && parsed.TradeMoney.SequenceEqual(tradeMoney), "MSG_AutoTrade did not round-trip its 12 listing slots.");
}

static void AutoTradeStartRequestWire()
{
    var items = Enumerable.Range(0, AutoTradeListConfirmation.SlotCount)
        .Select(index => index == 0 ? new LegacyItem(900, 1, 2, 3, 4, 5, 6) : default)
        .ToArray();
    var carryPositions = Enumerable.Repeat((sbyte)-1, AutoTradeListConfirmation.SlotCount).ToArray();
    carryPositions[0] = 4;
    var prices = Enumerable.Repeat(0, AutoTradeListConfirmation.SlotCount).ToArray();
    prices[0] = 100;
    var request = new AutoTradeStartRequest("AFK SHOP", items, carryPositions, prices, RequestedTax: 77, Index: -7);
    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(request.ToFrame(codec, 41, 22, headerId: 12));

    Assert(frame.IsChecksumValid && frame.Header.Type == AutoTradeStartRequest.MessageType && frame.Header.Size == AutoTradeStartRequest.PacketSize,
        "MSG_SendAutoTrade did not preserve the shared 196-byte request frame.");
    Assert(AutoTradeStartRequest.TryParse(frame, out var parsed) && parsed is not null && parsed.Title == "AFK SHOP" &&
        parsed.RequestedTax == 77 && parsed.Index == -7 && parsed.CarryPositions[0] == 4 && parsed.Prices[0] == 100,
        "MSG_SendAutoTrade did not round-trip the request title, index, carry position and price.");
}

static void AutoTradePurchaseWireAndGates()
{
    var item = new LegacyItem(901, 1, 2, 3, 4, 5, 6);
    var items = Enumerable.Repeat(default(LegacyItem), AutoTradeListConfirmation.SlotCount).ToArray();
    var carryPositions = Enumerable.Repeat((sbyte)-1, AutoTradeListConfirmation.SlotCount).ToArray();
    var prices = new int[AutoTradeListConfirmation.SlotCount];
    items[0] = item;
    carryPositions[0] = 7;
    prices[0] = 100;
    var listing = new LegacyAutoTradeSnapshot(42, 2_000, 2_001, "AFK SHOP", items, carryPositions, prices, 17);
    var sellerCargo = Enumerable.Repeat(default(LegacyItem), LegacyAutoTradeBook.CargoSlotCount).ToArray();
    sellerCargo[7] = item;
    var buyerCarry = Enumerable.Repeat(default(LegacyItem), LegacyAccountSnapshot.MobCarryCount).ToArray();
    var request = new AutoTradePurchaseRequest(0, 42, 100, 17, item, 0xA1, 0xB2);
    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(request.ToFrame(codec, 33, 21, headerId: 44));

    Assert(AutoTradePurchaseRequest.PayloadSize == 24 && AutoTradePurchaseRequest.PacketSize == 36,
        "MSG_ReqBuy did not preserve the 24-byte payload and 36-byte x86 frame layout.");
    var expected = new byte[AutoTradePurchaseRequest.PayloadSize];
    BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(AutoTradePurchaseRequest.PositionOffset), 0);
    BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(AutoTradePurchaseRequest.TargetIdOffset), 42);
    expected[AutoTradePurchaseRequest.AbiPaddingOffset] = 0xA1;
    expected[AutoTradePurchaseRequest.AbiPaddingOffset + 1] = 0xB2;
    BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(AutoTradePurchaseRequest.PriceOffset), 100);
    BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(AutoTradePurchaseRequest.TaxOffset), 17);
    item.Write(expected.AsSpan(AutoTradePurchaseRequest.ItemOffset, LegacyItem.SizeInBytes));
    Assert(frame.IsChecksumValid && frame.Header.Type == AutoTradePurchaseRequest.MessageType && frame.Header.Size == 36 &&
        frame.Payload.Span.SequenceEqual(expected), "MSG_ReqBuy did not preserve the client 7.69 field offsets and ABI padding.");
    Assert(AutoTradePurchaseRequest.TryParse(frame, out var parsed) && parsed is not null && parsed == request,
        "MSG_ReqBuy did not round-trip position, target, price, tax, padding and item.");

    var context = new LegacyAutoTradePurchaseContext(
        RequesterHp: 1,
        RequesterInPlay: true,
        LiveTradeActive: false,
        LiveTradeOpponentId: 0,
        TargetId: 42,
        TargetExists: true,
        TargetInPlay: true,
        TargetOfflineAutoTrade: false,
        TargetAutoTradeActive: true,
        TargetWithinView: true,
        Position: 0,
        RequestedPrice: 100,
        RequestedTax: 17,
        RequestedItem: item,
        Listing: listing,
        SellerCargo: sellerCargo,
        BuyerCarry: buyerCarry,
        BuyerCoin: 150,
        SellerCoin: 10,
        TargetVillage: 0);
    Assert(LegacyAutoTradePurchaseRules.Validate(context) == LegacyAutoTradePurchaseResult.Accepted,
        "The autotrade purchase gates rejected a valid live listing purchase.");
    Assert(LegacyAutoTradePurchaseRules.Validate(context with { RequestedPrice = 101 }) == LegacyAutoTradePurchaseResult.PriceChanged,
        "The autotrade purchase gates accepted a stale price.");
    Assert(LegacyAutoTradePurchaseRules.Validate(context with { RequestedTax = 18 }) == LegacyAutoTradePurchaseResult.TaxChanged,
        "The autotrade purchase gates accepted a stale tax.");
    Assert(LegacyAutoTradePurchaseRules.Validate(context with { RequestedItem = item with { Value1 = 9 } }) == LegacyAutoTradePurchaseResult.ItemChanged,
        "The autotrade purchase gates accepted a stale item payload.");
    var changedSellerCargo = sellerCargo.ToArray();
    changedSellerCargo[7] = default;
    Assert(LegacyAutoTradePurchaseRules.Validate(context with { SellerCargo = changedSellerCargo }) == LegacyAutoTradePurchaseResult.SellerItemChanged,
        "The autotrade purchase gates accepted a seller cargo item that no longer matches the listing.");
    Assert(LegacyAutoTradePurchaseRules.Validate(context with { BuyerCoin = 99 }) == LegacyAutoTradePurchaseResult.InsufficientCoin,
        "The autotrade purchase gates accepted a buyer without enough coin.");
    Assert(LegacyAutoTradePurchaseRules.Validate(context with { SellerCoin = LegacyAutoTradePurchaseRules.SellerCoinLimit }) == LegacyAutoTradePurchaseResult.SellerCoinLimitReached,
        "The autotrade purchase gates accepted a seller balance beyond the 2G cap.");
    Assert(LegacyAutoTradePurchaseRules.Validate(context with { BuyerCarry = Enumerable.Repeat(item, LegacyAccountSnapshot.MobCarryCount).ToArray() }) == LegacyAutoTradePurchaseResult.NoCarrySpace,
        "The autotrade purchase gates accepted a buyer with no usable carry slot.");
    Assert(LegacyAutoTradePurchaseRules.Validate(context with { TargetWithinView = false }) == LegacyAutoTradePurchaseResult.OutOfRange,
        "The autotrade purchase gates accepted a target outside the authoritative view grid.");
    Assert(LegacyAutoTradePurchaseRules.Validate(context with { TargetInPlay = false }) == LegacyAutoTradePurchaseResult.TargetNotPlaying,
        "The autotrade purchase gates accepted a non-playing target without offline-shop state.");
    Assert(LegacyAutoTradePurchaseRules.Validate(context with { TargetInPlay = false, TargetOfflineAutoTrade = true }) == LegacyAutoTradePurchaseResult.Accepted,
        "The autotrade purchase gates rejected an explicitly rehydrated offline shop.");
    Assert(LegacyAutoTradePurchaseRules.Validate(context with { LiveTradeActive = true }) == LegacyAutoTradePurchaseResult.LiveTradeActive,
        "The autotrade purchase gates accepted a buyer already in a live trade.");

    Assert(LegacyAutoTradePurchasePlanBuilder.TryBuild(context, out var plan) == LegacyAutoTradePurchaseResult.Accepted && plan is not null,
        "The autotrade purchase plan rejected a valid transaction after the gates passed.");
    Assert(plan!.BuyerDestinationSlot == 0 && plan.SellerCargoPosition == 7 && plan.PurchasedItem == item &&
        plan.BuyerCoin == 50 && plan.SellerCoin == 110 && plan.Settlement.TaxAmount == 0,
        "The autotrade purchase plan did not calculate destination, coin and seller proceeds correctly.");
    Assert(plan.BuyerCarry[0] == item && plan.SellerCargo[7].Index == 0 &&
        plan.UpdatedListing.Items[0].Index == 0 && plan.UpdatedListing.CarryPositions[0] == -1 && plan.UpdatedListing.Prices[0] == 0,
        "The autotrade purchase plan did not clear the sold listing/cargo and insert the item into the buyer copy.");
    Assert(buyerCarry[0].Index == 0 && sellerCargo[7] == item && listing.Items[0] == item && listing.Prices[0] == 100,
        "Building an autotrade purchase plan mutated the source buyer, seller or listing snapshots.");
    Assert(LegacyAutoTradePurchasePlanBuilder.TryBuild(context with { SellerCoin = LegacyAutoTradePurchaseRules.SellerCoinLimit }, out _) == LegacyAutoTradePurchaseResult.SellerCoinLimitReached,
        "The autotrade purchase plan bypassed the seller 2G gate.");
}

static void AutoTradeSaleNotificationAndSettlement()
{
    var notification = new ItemSoldConfirmation(42, 7);
    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(notification.ToFrame(codec, 33, 21));
    var expected = new byte[ItemSoldConfirmation.PayloadSize];
    BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(ItemSoldConfirmation.SellerIdOffset), 42);
    BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(ItemSoldConfirmation.PositionOffset), 7);

    Assert(ItemSoldConfirmation.MessageType == 0x039B && ItemSoldConfirmation.PayloadSize == 8 && ItemSoldConfirmation.PacketSize == 20,
        "MSG_ItemSold did not preserve the standard-parm2 opcode and 20-byte frame layout.");
    Assert(frame.IsChecksumValid && frame.Header.Type == ItemSoldConfirmation.MessageType && frame.Header.Id == ItemSoldConfirmation.SceneId &&
        frame.Payload.Span.SequenceEqual(expected), "MSG_ItemSold did not preserve seller, position and ESCENE_FIELD header bytes.");
    Assert(ItemSoldConfirmation.TryParse(frame, out var parsed) && parsed is not null && parsed == notification,
        "MSG_ItemSold did not round-trip its two standard parameters.");

    var belowThreshold = LegacyAutoTradeSettlementMath.Calculate(99_999, 7);
    Assert(belowThreshold.TaxAmount == 0 && belowThreshold.SellerProceeds == 99_999,
        "Autotrade settlement applied city tax below the 100,000 Gold threshold.");
    var taxed = LegacyAutoTradeSettlementMath.Calculate(250_000, 7);
    Assert(taxed.TaxAmount == 17_500 && taxed.SellerProceeds == 232_500,
        "Autotrade settlement did not reproduce integer city-tax arithmetic.");
    var truncation = LegacyAutoTradeSettlementMath.Calculate(100_099, 7);
    Assert(truncation.TaxAmount == 7_000 && truncation.SellerProceeds == 93_099,
        "Autotrade settlement did not truncate the price division before multiplying the tax.");
}

static void AutoTradePurchaseCommitContract()
{
    var item = new LegacyItem(901, 1, 2, 3, 4, 5, 6);
    var items = Enumerable.Repeat(default(LegacyItem), AutoTradeListConfirmation.SlotCount).ToArray();
    var carryPositions = Enumerable.Repeat((sbyte)-1, AutoTradeListConfirmation.SlotCount).ToArray();
    var prices = new int[AutoTradeListConfirmation.SlotCount];
    items[0] = item;
    carryPositions[0] = 7;
    prices[0] = 100;
    var listing = new LegacyAutoTradeSnapshot(42, 2_000, 2_001, "AFK SHOP", items, carryPositions, prices, 17);
    var sellerCargo = Enumerable.Repeat(default(LegacyItem), LegacyAutoTradeBook.CargoSlotCount).ToArray();
    sellerCargo[7] = item;
    var buyerCarry = Enumerable.Repeat(default(LegacyItem), LegacyAccountSnapshot.MobCarryCount).ToArray();
    var context = new LegacyAutoTradePurchaseContext(
        RequesterHp: 1,
        RequesterInPlay: true,
        LiveTradeActive: false,
        LiveTradeOpponentId: 0,
        TargetId: 42,
        TargetExists: true,
        TargetInPlay: false,
        TargetOfflineAutoTrade: true,
        TargetAutoTradeActive: true,
        TargetWithinView: true,
        Position: 0,
        RequestedPrice: 100,
        RequestedTax: 17,
        RequestedItem: item,
        Listing: listing,
        SellerCargo: sellerCargo,
        BuyerCarry: buyerCarry,
        BuyerCoin: 150,
        SellerCoin: 10,
        TargetVillage: 0);

    Assert(LegacyAutoTradePurchasePlanBuilder.TryBuild(context, out var plan) == LegacyAutoTradePurchaseResult.Accepted && plan is not null,
        "The commit fixture could not build the pure purchase plan.");

    var expectedBuyerMob = new byte[LegacyAccountSnapshot.CharacterStride];
    expectedBuyerMob[LegacyAccountSnapshot.MobNameOffset] = (byte)'B';
    BinaryPrimitives.WriteInt32LittleEndian(expectedBuyerMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), 150);
    var buyerMob = expectedBuyerMob.ToArray();
    BinaryPrimitives.WriteInt32LittleEndian(buyerMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), plan!.BuyerCoin);
    item.Write(buyerMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes));
    var expectedListing = new LegacyAutoTradePersistedListing("SELLER", 1, 2_000, 2_001, "AFK SHOP", items, carryPositions, prices, 17);
    var updatedListing = new LegacyAutoTradePersistedListing("SELLER", 1, 2_000, 2_001, "AFK SHOP", plan.UpdatedListing.Items, plan.UpdatedListing.CarryPositions, plan.UpdatedListing.Prices, 17);
    var commit = new LegacyAutoTradePurchaseCommitRequest(
        "BUYER", 0, expectedBuyerMob, expectedBuyerMob.ToArray(), buyerMob, 2_010, 2_011, new byte[LegacyAccountSnapshot.MobExtraStride],
        "SELLER", 1, 10, plan, expectedListing, updatedListing);

    Assert(LegacyAutoTradePurchaseCommitRules.Validate(commit) == LegacyAutoTradePurchaseCommitResult.Committed,
        "The purchase commit contract rejected a complete compare-and-swap request.");
    var persistedBaseline = expectedBuyerMob.ToArray();
    BinaryPrimitives.WriteInt64LittleEndian(persistedBaseline.AsSpan(LegacyAccountSnapshot.MobExperienceOffset), 77);
    Assert(LegacyAutoTradePurchaseCommitRules.Validate(commit with { ExpectedPersistedBuyerMob = persistedBaseline }) == LegacyAutoTradePurchaseCommitResult.Committed,
        "The purchase commit contract coupled the persisted buyer baseline to the runtime MOB snapshot.");
    Assert(LegacyAutoTradePurchaseCommitRules.Validate(commit with { SellerAccountName = "BUYER" }) == LegacyAutoTradePurchaseCommitResult.InvalidRequest,
        "The purchase commit contract accepted a same-account buyer and seller request.");
    Assert(LegacyAutoTradePurchaseCommitRules.Validate(commit with { ExpectedSellerCoin = 11 }) == LegacyAutoTradePurchaseCommitResult.InvalidRequest,
        "The commit contract accepted a seller balance inconsistent with the pure settlement plan.");
    Assert(LegacyAutoTradePurchaseCommitRules.Validate(commit with { UpdatedListing = updatedListing with { Title = "CHANGED" } }) == LegacyAutoTradePurchaseCommitResult.InvalidRequest,
        "The purchase commit contract accepted a listing metadata change during item settlement.");
}

static void WorldAutoTradeBook()
{
    var item = new LegacyItem(900, 1, 2, 3, 4, 5, 6);
    var items = Enumerable.Range(0, AutoTradeListConfirmation.SlotCount)
        .Select(index => index == 0 ? item : default)
        .ToArray();
    var carryPositions = Enumerable.Repeat((sbyte)-1, AutoTradeListConfirmation.SlotCount).ToArray();
    carryPositions[0] = 4;
    var prices = new int[AutoTradeListConfirmation.SlotCount];
    prices[0] = 100;
    var request = new AutoTradeStartRequest("AFK SHOP", items, carryPositions, prices);
    var cargo = Enumerable.Repeat(default(LegacyItem), LegacyAutoTradeBook.CargoSlotCount).ToArray();
    cargo[4] = item;
    var context = new LegacyAutoTradeStartContext(
        InPlay: true,
        CurrentHp: 1,
        LiveTradeActive: false,
        InAllowedVillage: true,
        CityTax: 12,
        PositionX: 2000,
        PositionY: 2001,
        Cargo: cargo,
        NonTradeableItemIndices: new HashSet<short>());
    var book = new LegacyAutoTradeBook();

    Assert(book.TryStart(12, request, context, out var snapshot) == LegacyAutoTradeStartResult.Accepted && snapshot is not null,
        "The autotrade book rejected a valid live cargo listing.");
    Assert(snapshot!.ConnectionId == 12 && snapshot.PositionX == 2000 && snapshot.Tax == 12 && snapshot.Items[0] == item,
        "The autotrade book did not preserve the authoritative shop snapshot.");
    Assert(book.TryStart(12, request, context, out _) == LegacyAutoTradeStartResult.AlreadyActive,
        "The autotrade book allowed a second shop for the same connection.");
    Assert(book.TryGet(12, out var current) && current is not null && current.Title == "AFK SHOP",
        "The autotrade book did not expose the active shop snapshot.");

    var changedCargo = cargo.ToArray();
    changedCargo[4] = new LegacyItem(901, 0, 0, 0, 0, 0, 0);
    var changedContext = context with { Cargo = changedCargo };
    var secondBook = new LegacyAutoTradeBook();
    Assert(secondBook.TryStart(13, request, changedContext, out _) == LegacyAutoTradeStartResult.ItemChanged,
        "The autotrade book accepted a listing whose cargo item changed.");
    Assert(secondBook.TryStart(13, request, context with { InAllowedVillage = false }, out _) == LegacyAutoTradeStartResult.InvalidLocation,
        "The autotrade book accepted a listing outside an allowed village.");
    var blockedBook = new LegacyAutoTradeBook();
    Assert(blockedBook.TryStart(14, request, context with { AccountBlocked = true }, out _) == LegacyAutoTradeStartResult.AccountBlocked,
        "The autotrade book accepted a blocked account.");
    var missingItemDataBook = new LegacyAutoTradeBook();
    Assert(missingItemDataBook.TryStart(15, request, context with { ItemDataAvailable = false }, out _) == LegacyAutoTradeStartResult.ItemDataUnavailable,
        "The autotrade book accepted a non-empty listing without item metadata.");
    Assert(book.TryStop(12, out var stopped) && stopped is not null && !book.TryGet(12, out _),
        "The autotrade book did not remove the active shop.");
}

static void AutoTradeRuntimeMutation()
{
    var item = new LegacyItem(900, 1, 2, 3, 4, 5, 6);
    var items = Enumerable.Repeat(default(LegacyItem), AutoTradeListConfirmation.SlotCount).ToArray();
    var carryPositions = Enumerable.Repeat((sbyte)-1, AutoTradeListConfirmation.SlotCount).ToArray();
    var prices = new int[AutoTradeListConfirmation.SlotCount];
    items[0] = item;
    carryPositions[0] = 4;
    prices[0] = 100;
    var listing = new LegacyAutoTradeSnapshot(12, 2_000, 2_001, "AFK SHOP", items, carryPositions, prices, 12);
    var sellerCargo = Enumerable.Repeat(default(LegacyItem), LegacyAutoTradeBook.CargoSlotCount).ToArray();
    sellerCargo[4] = item;
    var buyerCarry = Enumerable.Repeat(default(LegacyItem), LegacyAccountSnapshot.MobCarryCount).ToArray();
    var context = new LegacyAutoTradePurchaseContext(
        100, true, false, 0, 12, true, true, false, true, true,
        0, 100, 12, item, listing, sellerCargo, buyerCarry, 150, 10, 0);
    Assert(LegacyAutoTradePurchasePlanBuilder.TryBuild(context, out var plan) == LegacyAutoTradePurchaseResult.Accepted && plan is not null,
        "The runtime mutation fixture could not build a valid purchase plan.");

    var book = new LegacyAutoTradeBook();
    var startRequest = new AutoTradeStartRequest("AFK SHOP", items, carryPositions, prices);
    var startContext = new LegacyAutoTradeStartContext(true, 100, false, true, 12, 2_000, 2_001, sellerCargo, new HashSet<short>());
    Assert(book.TryStart(12, startRequest, startContext, out _) == LegacyAutoTradeStartResult.Accepted,
        "The runtime mutation fixture could not start the shop book.");
    Assert(book.TryApplyPurchase(12, plan!, out var updatedListing) == LegacyAutoTradePurchaseResult.Accepted &&
        updatedListing is not null && updatedListing.Items[0].Index == 0,
        "The autotrade book did not atomically clear the sold listing slot.");
    Assert(book.TryApplyPurchase(12, plan!, out _) == LegacyAutoTradePurchaseResult.ItemChanged,
        "The autotrade book accepted the same purchase twice after clearing the slot.");

    var hub = new WorldHub();
    Assert(hub.Enter(7, "BUYER", (_, _) => ValueTask.CompletedTask), "The runtime buyer was not registered.");
    var buyerMob = new byte[LegacyAccountSnapshot.CharacterStride];
    buyerMob[LegacyAccountSnapshot.MobNameOffset] = (byte)'B';
    BinaryPrimitives.WriteInt32LittleEndian(buyerMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), 150);
    Assert(hub.SetCharacterState(7, 0, 0, 0, 0, 150, 2_010, 2_011, buyerMob), "The runtime buyer state was not initialized.");
    Assert(hub.TryApplyAutoTradeBuyerPurchase(7, plan!, out var buyerOutcome) == LegacyAutoTradeBuyerPurchaseResult.Accepted &&
        buyerOutcome is not null && buyerOutcome.BuyerCoin == 50,
        "The WorldHub did not apply the buyer half of the validated plan.");
    Assert(hub.TryGetCharacterSnapshot(7, out var afterPurchase, out _, out _) && afterPurchase is not null &&
        BinaryPrimitives.ReadInt32LittleEndian(afterPurchase.AsSpan(LegacyAccountSnapshot.MobCoinOffset)) == 50 &&
        LegacyItem.Read(afterPurchase.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes)) == item,
        "The buyer runtime mutation did not write the purchased item and Gold.");
    Assert(hub.TryApplyAutoTradeBuyerPurchase(7, plan!, out _) == LegacyAutoTradeBuyerPurchaseResult.CoinChanged,
        "The WorldHub accepted a stale buyer plan after the coin had changed.");
    Assert(hub.TryRollbackAutoTradeBuyerPurchase(buyerOutcome!), "The buyer runtime rollback failed after a simulated persistence rejection.");
    Assert(hub.TryGetCharacterSnapshot(7, out var rolledBack, out _, out _) && rolledBack is not null &&
        BinaryPrimitives.ReadInt32LittleEndian(rolledBack.AsSpan(LegacyAccountSnapshot.MobCoinOffset)) == 150 &&
        LegacyItem.Read(rolledBack.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes)).Index == 0,
        "The buyer runtime rollback did not restore the previous MOB.");
}

static void AutoTradePurchaseExecutor()
{
    var item = new LegacyItem(900, 1, 2, 3, 4, 5, 6);

    (WorldHub World, LegacyAutoTradeBook Book, LegacyAutoTradePurchaseExecutionRequest Request, LegacyAutoTradeSnapshot Listing) CreateFixture()
    {
        var items = Enumerable.Repeat(default(LegacyItem), AutoTradeListConfirmation.SlotCount).ToArray();
        var carryPositions = Enumerable.Repeat((sbyte)-1, AutoTradeListConfirmation.SlotCount).ToArray();
        var prices = new int[AutoTradeListConfirmation.SlotCount];
        items[0] = item;
        carryPositions[0] = 4;
        prices[0] = 100;
        var listing = new LegacyAutoTradeSnapshot(12, 2_000, 2_001, "AFK SHOP", items, carryPositions, prices, 12);
        var sellerCargo = Enumerable.Repeat(default(LegacyItem), LegacyAutoTradeBook.CargoSlotCount).ToArray();
        sellerCargo[4] = item;
        var buyerCarry = Enumerable.Repeat(default(LegacyItem), LegacyAccountSnapshot.MobCarryCount).ToArray();
        var context = new LegacyAutoTradePurchaseContext(
            100, true, false, 0, 12, true, false, true, true, true,
            0, 100, 12, item, listing, sellerCargo, buyerCarry, 150, 10, 0);
        var request = new AutoTradeStartRequest("AFK SHOP", items, carryPositions, prices);
        var startContext = new LegacyAutoTradeStartContext(true, 100, false, true, 12, 2_000, 2_001, sellerCargo, new HashSet<short>());
        var book = new LegacyAutoTradeBook();
        Assert(book.TryStart(12, request, startContext, out var started) == LegacyAutoTradeStartResult.Accepted && started is not null,
            "The executor fixture could not create the runtime listing.");

        var world = new WorldHub();
        Assert(world.Enter(7, "BUYER", (_, _) => ValueTask.CompletedTask), "The executor fixture could not register the buyer.");
        var buyerMob = new byte[LegacyAccountSnapshot.CharacterStride];
        buyerMob[LegacyAccountSnapshot.MobNameOffset] = (byte)'B';
        BinaryPrimitives.WriteInt32LittleEndian(buyerMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), 150);
        Assert(world.SetCharacterState(7, 0, 0, 0, 0, 150, 2_010, 2_011, buyerMob), "The executor fixture could not initialize the buyer.");
        Assert(world.EnterNpc(
            new byte[LegacyAccountSnapshot.CharacterStride],
            2_000,
            2_001,
            new byte[LegacyAccountSnapshot.AffectStride],
            requestedConnectionId: 12,
            autoTradeSnapshot: started,
            autoTradeOwnerAccount: "SELLER",
            autoTradeCharacterSlot: 1) == 12,
            "The executor fixture could not register the offline shop NPC.");

        var expectedBuyerMob = buyerMob.ToArray();
        var expectedPersistedBuyerMob = expectedBuyerMob.ToArray();
        BinaryPrimitives.WriteInt64LittleEndian(expectedPersistedBuyerMob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset), 77);
        var expectedListing = new LegacyAutoTradePersistedListing(
            "SELLER",
            1,
            listing.PositionX,
            listing.PositionY,
            listing.Title,
            listing.Items,
            listing.CarryPositions,
            listing.Prices,
            listing.Tax);
        var executionRequest = new LegacyAutoTradePurchaseExecutionRequest(
            7,
            context,
            "BUYER",
            0,
            expectedBuyerMob,
            expectedPersistedBuyerMob,
            2_010,
            2_011,
            new byte[LegacyAccountSnapshot.MobExtraStride],
            "SELLER",
            1,
            expectedListing,
            TargetIsOfflineNpc: true);
        return (world, book, executionRequest, listing);
    }

    var committed = CreateFixture();
    var committedStore = new FixedAutoTradePurchaseCommitStore(LegacyAutoTradePurchaseCommitResult.Committed);
    var committedExecution = LegacyAutoTradePurchaseExecutor.ExecuteAsync(
        committed.World,
        committed.Book,
        committedStore,
        committed.Request).GetAwaiter().GetResult();
    Assert(committedExecution.Result == LegacyAutoTradePurchaseExecutionResult.Accepted && committedExecution.Outcome is not null,
        "The purchase executor rejected a valid atomic sale.");
    Assert(committed.Book.TryGet(12, out var committedListing) && committedListing is not null && committedListing.Items[0].Index == 0,
        "The purchase executor did not retain the committed listing mutation.");
    Assert(committed.World.TryGetNpcSnapshot(12, out var committedNpc) && committedNpc?.AutoTradeSnapshot?.Items[0].Index == 0,
        "The purchase executor did not synchronize the offline NPC visual.");
    Assert(committed.World.TryGetCharacterSnapshot(7, out var committedMob, out _, out _) && committedMob is not null &&
        BinaryPrimitives.ReadInt32LittleEndian(committedMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset)) == 50 &&
        LegacyItem.Read(committedMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes)) == item,
        "The purchase executor did not commit the buyer runtime mutation.");
    Assert(committedStore.LastRequest is not null &&
        committedStore.LastRequest.ExpectedBuyerMob.SequenceEqual(committed.Request.ExpectedBuyerMob) &&
        committedStore.LastRequest.ExpectedPersistedBuyerMob.SequenceEqual(committed.Request.ExpectedPersistedBuyerMob),
        "The purchase executor did not preserve separate runtime and persisted buyer CAS baselines.");

    var rejected = CreateFixture();
    var rejectedExecution = LegacyAutoTradePurchaseExecutor.ExecuteAsync(
        rejected.World,
        rejected.Book,
        new FixedAutoTradePurchaseCommitStore(LegacyAutoTradePurchaseCommitResult.Conflict),
        rejected.Request).GetAwaiter().GetResult();
    Assert(rejectedExecution.Result == LegacyAutoTradePurchaseExecutionResult.CommitRejected,
        "The purchase executor did not surface a rejected durable commit.");
    Assert(rejected.Book.TryGet(12, out var restoredListing) && restoredListing is not null && restoredListing.Items[0] == item,
        "The purchase executor did not restore the listing after a rejected commit.");
    Assert(rejected.World.TryGetNpcSnapshot(12, out var restoredNpc) && restoredNpc?.AutoTradeSnapshot?.Items[0] == item,
        "The purchase executor did not restore the offline NPC after a rejected commit.");
    Assert(rejected.World.TryGetCharacterSnapshot(7, out var restoredMob, out _, out _) && restoredMob is not null &&
        BinaryPrimitives.ReadInt32LittleEndian(restoredMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset)) == 150 &&
        LegacyItem.Read(restoredMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes)).Index == 0,
        "The purchase executor did not roll back the buyer after a rejected commit.");
}

static void AutoTradePurchaseCoordinator()
{
    var item = new LegacyItem(900, 1, 2, 3, 4, 5, 6);
    var items = Enumerable.Repeat(default(LegacyItem), AutoTradeListConfirmation.SlotCount).ToArray();
    var carryPositions = Enumerable.Repeat((sbyte)-1, AutoTradeListConfirmation.SlotCount).ToArray();
    var prices = new int[AutoTradeListConfirmation.SlotCount];
    items[0] = item;
    carryPositions[0] = 4;
    prices[0] = 100;
    var sellerCargo = Enumerable.Repeat(default(LegacyItem), LegacyAutoTradeBook.CargoSlotCount).ToArray();
    sellerCargo[4] = item;
    var listing = new LegacyAutoTradeSnapshot(12, 2_100, 2_100, "AFK SHOP", items, carryPositions, prices, 12);
    var book = new LegacyAutoTradeBook();
    Assert(book.TryStart(12, new AutoTradeStartRequest("AFK SHOP", items, carryPositions, prices),
        new LegacyAutoTradeStartContext(true, 100, false, true, 12, 2_100, 2_100, sellerCargo, new HashSet<short>()),
        out var started) == LegacyAutoTradeStartResult.Accepted && started is not null,
        "The purchase coordinator fixture could not create the listing.");

    var buyerMob = new byte[LegacyAccountSnapshot.CharacterStride];
    buyerMob[LegacyAccountSnapshot.MobNameOffset] = (byte)'B';
    BinaryPrimitives.WriteInt32LittleEndian(buyerMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), 150);
    new LegacyScore(1, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(buyerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    var persistedBuyerMob = buyerMob.ToArray();
    BinaryPrimitives.WriteInt64LittleEndian(persistedBuyerMob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset), 77);
    var world = new WorldHub();
    Assert(world.Enter(7, "BUYER", (_, _) => ValueTask.CompletedTask) &&
        world.SetCharacterState(7, 0, 0, 0, 0, 150, 2_110, 2_110, buyerMob),
        "The purchase coordinator fixture could not initialize the buyer.");
    Assert(world.EnterNpc(new byte[LegacyAccountSnapshot.CharacterStride], 2_100, 2_100,
        new byte[LegacyAccountSnapshot.AffectStride], requestedConnectionId: 12, autoTradeSnapshot: started,
        autoTradeOwnerAccount: "SELLER", autoTradeCharacterSlot: 1) == 12,
        "The purchase coordinator fixture could not register the offline shop.");

    var sessions = new LoginSessionRegistry();
    var login = new AccountLoginRequest("BUYER", "secret", new byte[52], 7670, 0, [0, 0, 0, 0]);
    Assert(sessions.Open(7) && sessions.BeginAccountLogin(7, login) == LoginTransitionResult.Accepted &&
        sessions.CompleteAccountLogin(7, "BUYER") == LoginTransitionResult.Accepted &&
        sessions.BeginCharacterWait(7) == LoginTransitionResult.Accepted &&
        sessions.CompleteCharacterLogin(7, 0, 2_110, 2_110) == LoginTransitionResult.Accepted,
        "The purchase coordinator fixture could not enter USER_PLAY.");

    var buyerData = new LegacyCharacterLoginData(
        persistedBuyerMob,
        new byte[LegacyAccountSnapshot.ShortSkillStride],
        new byte[LegacyAccountSnapshot.AffectStride],
        new byte[LegacyAccountSnapshot.MobExtraStride],
        0,
        2_110,
        2_110);
    var sellerSnapshot = new LegacyAccountSnapshot(
        "SELLER",
        LegacyCharacterSelection.CreateEmpty(),
        sellerCargo.Concat(Enumerable.Repeat(default(LegacyItem), LegacyAccountSnapshot.CargoCount - sellerCargo.Length)).ToArray(),
        10,
        false,
        default,
        string.Empty,
        false);
    var commitStore = new FixedAutoTradePurchaseCommitStore(LegacyAutoTradePurchaseCommitResult.Committed);
    var outcome = LegacyAutoTradePurchaseCoordinator.ExecuteAsync(
        7,
        new AutoTradePurchaseRequest(0, 12, 100, 12, item),
        sessions,
        world,
        book,
        new FixedAccountSnapshotStore("SELLER", sellerSnapshot),
        new FixedCharacterLoginDataStore("BUYER", 0, buyerData),
        commitStore).GetAwaiter().GetResult();

    Assert(outcome.Result == LegacyAutoTradePurchaseHostResult.Accepted && outcome.Execution?.Outcome is not null,
        $"The host purchase coordinator rejected a valid offline-shop purchase: host={outcome.Result}, execution={outcome.Execution?.Result}.");
    Assert(outcome.TargetOffline && !outcome.TargetOnline && outcome.SellerAccountName == "SELLER",
        "The host purchase coordinator did not preserve the offline seller identity.");
    Assert(commitStore.LastRequest is not null &&
        commitStore.LastRequest.ExpectedPersistedBuyerMob.SequenceEqual(persistedBuyerMob) &&
        commitStore.LastRequest.ExpectedBuyerMob.SequenceEqual(buyerMob),
        "The host purchase coordinator did not pass distinct runtime and persisted buyer baselines.");
}

static void AutoTradePurchaseRelay()
{
    var item = new LegacyItem(900, 1, 2, 3, 4, 5, 6);
    var items = Enumerable.Repeat(default(LegacyItem), AutoTradeListConfirmation.SlotCount).ToArray();
    var carryPositions = Enumerable.Repeat((sbyte)-1, AutoTradeListConfirmation.SlotCount).ToArray();
    var prices = new int[AutoTradeListConfirmation.SlotCount];
    items[0] = item;
    carryPositions[0] = 4;
    prices[0] = 100;
    var originalListing = new LegacyAutoTradeSnapshot(12, 2_000, 2_001, "AFK SHOP", items, carryPositions, prices, 12);
    var updatedItems = items.ToArray();
    var updatedCarryPositions = carryPositions.ToArray();
    var updatedPrices = prices.ToArray();
    updatedItems[0] = default;
    updatedCarryPositions[0] = -1;
    updatedPrices[0] = 0;
    var updatedListing = originalListing with
    {
        Items = updatedItems,
        CarryPositions = updatedCarryPositions,
        Prices = updatedPrices,
    };
    var buyerCarry = Enumerable.Repeat(default(LegacyItem), LegacyAccountSnapshot.MobCarryCount).ToArray();
    buyerCarry[0] = item;
    var plan = new LegacyAutoTradePurchasePlan(
        12,
        0,
        4,
        0,
        item,
        new LegacyAutoTradeSettlement(100, 12, 0, 100),
        50,
        110,
        buyerCarry,
        Enumerable.Repeat(default(LegacyItem), LegacyAutoTradeBook.CargoSlotCount).ToArray(),
        updatedListing);
    var buyerMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var buyerOutcome = new LegacyAutoTradeBuyerPurchaseOutcome(7, 0, item, 50, buyerMob, buyerMob.ToArray());
    var outcome = new LegacyAutoTradePurchaseExecutionOutcome(
        plan,
        buyerOutcome,
        originalListing,
        updatedListing,
        new LegacyAutoTradePersistedListing("SELLER", 1, 2_000, 2_001, "AFK SHOP", updatedItems, updatedCarryPositions, updatedPrices, 12),
        OfflineVisualUpdated: true);
    var codec = LegacyFrameCodec.CreateDefault();
    var mob = new byte[LegacyAccountSnapshot.CharacterStride];
    var affect = new byte[LegacyAccountSnapshot.AffectStride];

    Assert(LegacyAutoTradePurchaseRelay.TryBuild(outcome, codec, 41, 22, true, mob, affect, out var relay) && relay is not null,
        "The post-commit autotrade relay did not build its core frames.");
    var buyerFrame = codec.Decode(relay!.BuyerCarryFrame);
    var soldFrame = codec.Decode(relay.ItemSoldFrame);
    Assert(buyerFrame.IsChecksumValid && buyerFrame.Header.Type == UpdateCarryConfirmation.MessageType &&
        buyerFrame.Header.Size == UpdateCarryConfirmation.PacketSize,
        "The post-commit relay did not preserve the full buyer carry frame.");
    Assert(ItemSoldConfirmation.TryParse(soldFrame, out var sold) && sold == new ItemSoldConfirmation(12, 0),
        "The post-commit relay did not preserve the seller and listing position.");
    Assert(relay.OfflineVisualFrame is not null,
        "The post-commit relay did not build the offline-shop visual refresh.");

    Assert(LegacyAutoTradePurchaseRelay.TryBuild(outcome, codec, 41, 22, true, [], [], out var degradedRelay) &&
        degradedRelay is not null && degradedRelay.OfflineVisualFrame is null,
        "A missing offline visual must not suppress the committed buyer and ItemSold frames.");
}

static void AutoTradeListRelay()
{
    var item = new LegacyItem(900, 1, 2, 3, 4, 5, 6);
    var items = Enumerable.Range(0, AutoTradeListConfirmation.SlotCount)
        .Select(index => index == 0 ? item : default)
        .ToArray();
    var carryPositions = Enumerable.Repeat((sbyte)-1, AutoTradeListConfirmation.SlotCount).ToArray();
    carryPositions[0] = 4;
    var prices = new int[AutoTradeListConfirmation.SlotCount];
    prices[0] = 100;
    var snapshot = new LegacyAutoTradeSnapshot(12, 2_000, 2_001, "AFK SHOP", items, carryPositions, prices, 12);
    var codec = LegacyFrameCodec.CreateDefault();

    Assert(LegacyAutoTradeListRelay.TryBuild(7, snapshot, codec, 41, 22, out var plan) && plan is not null &&
        plan.RequesterConnectionId == 7 && plan.ShopConnectionId == 12,
        "The autotrade list relay did not produce a response plan for the requester and shop owner.");

    var frame = codec.Decode(plan!.ResponseFrame);
    Assert(frame.IsChecksumValid && frame.Header.Type == AutoTradeListConfirmation.MessageType &&
        frame.Header.Id == AutoTradeListConfirmation.SceneId &&
        AutoTradeListConfirmation.TryParse(frame, out var parsed) && parsed is not null &&
        parsed.TargetId == 12 && parsed.Description == "AFK SHOP" && parsed.TradeMoney[0] == 100,
        "The autotrade list relay did not preserve the client response header and listing bytes.");
}

static void WorldBilateralTradeState()
{
    var firstItem = new LegacyItem(700, 1, 2, 3, 4, 5, 6);
    var secondItem = new LegacyItem(701, 6, 5, 4, 3, 2, 1);
    var firstMob = TradeMob(firstItem, 7);
    var secondMob = TradeMob(secondItem, 8);
    var hub = new WorldHub();
    Assert(hub.Enter(1, "TRADE_A", (_, _) => ValueTask.CompletedTask) && hub.Enter(2, "TRADE_B", (_, _) => ValueTask.CompletedTask), "Trade participants were not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 0, 0, 100, 2000, 2000, firstMob) && hub.SetCharacterState(2, 0, 0, 0, 0, 200, 2000, 2000, secondMob), "Trade participant state was not initialized.");

    var firstOffer = TradeOffer(1, 7, firstItem, 20, 0, 2);
    var firstResult = hub.TryStageTradeOffer(1, firstOffer, out var firstOutcome);
    Assert(firstResult == LegacyTradeOfferResult.Accepted && firstOutcome is not null && !firstOutcome.Paired, $"The first trade side was not staged as an unpaired offer: result={firstResult}, paired={firstOutcome?.Paired}, own={firstOutcome?.OwnState.HasOffer}, opponent={firstOutcome?.OpponentState.HasOffer}.");
    Assert(firstOutcome!.OwnState.HasOffer && firstOutcome.OwnState.TradeMoney == 0 && firstOutcome.OwnState.MyCheck == 0 && !firstOutcome.OpponentState.HasOffer, "The initial legacy offer did not reset money/check before the counterpart entered.");

    var changedItem = firstItem with { Value1 = 9 };
    var changedOffer = TradeOffer(1, 7, changedItem, 0, 0, 2);
    Assert(hub.TryStageTradeOffer(1, changedOffer, out _) == LegacyTradeOfferResult.ItemChanged, "A changed live carry item was accepted into the trade state.");
    Assert(hub.TryCloseTrade(1, out _) == LegacyTradeCloseResult.NoActiveTrade, "A changed offered item did not clear the bilateral trade state.");

    Assert(hub.TryStageTradeOffer(1, TradeOffer(1, 7, firstItem, 0, 0, 2), out _) == LegacyTradeOfferResult.Accepted, "The trade state could not be reopened after invalidation.");
    var secondOffer = TradeOffer(2, 8, secondItem, 30, 0, 1);
    Assert(hub.TryStageTradeOffer(2, secondOffer, out var paired) == LegacyTradeOfferResult.Accepted && paired is not null && paired.Paired, "The second side did not pair with the pending trade.");
    Assert(paired!.OwnState.TradeMoney == 30 && paired.OpponentState.TradeMoney == 0 && paired.OwnState.MyCheck == 0 && paired.OpponentState.MyCheck == 0, "The bilateral trade snapshot did not preserve each side's staged offer.");

    Assert(hub.TryStageTradeOffer(1, TradeOffer(1, 7, firstItem, 20, 1, 2), out var firstCheck) == LegacyTradeOfferResult.Accepted && firstCheck is not null && firstCheck.OwnState.MyCheck == 1 && firstCheck.OpponentState.MyCheck == 0, "The first trade check did not remain unilateral.");
    Assert(hub.TryStageTradeOffer(2, TradeOffer(2, 8, secondItem, 30, 1, 1), out var bothChecked) == LegacyTradeOfferResult.BothChecked && bothChecked is not null && bothChecked.OwnState.MyCheck == 1 && bothChecked.OpponentState.MyCheck == 1, "Both trade checks were not retained in the bilateral state.");
    Assert(hub.TryCloseTrade(1, out var closed) == LegacyTradeCloseResult.Accepted && closed?.OpponentId == 2, "Trade close did not clear both sides.");
    Assert(hub.TryCloseTrade(2, out _) == LegacyTradeCloseResult.NoActiveTrade, "Trade close left the counterpart state active.");

    var duplicateItems = new LegacyItem[TradeOfferRequest.ItemCount];
    duplicateItems[0] = firstItem;
    duplicateItems[1] = secondItem;
    var duplicateInventoryPositions = Enumerable.Repeat((sbyte)-1, TradeOfferRequest.ItemCount).ToArray();
    duplicateInventoryPositions[0] = 7;
    duplicateInventoryPositions[1] = 7;
    var duplicatePositions = new TradeOfferRequest(duplicateItems, duplicateInventoryPositions, 0, 0, 2);
    Assert(hub.TryStageTradeOffer(1, duplicatePositions, out _) == LegacyTradeOfferResult.DuplicateInventoryPosition, "Duplicate inventory positions were accepted.");

    Assert(hub.SetCombatEligibilityState(1, true, false, 0), "The trade PK-mode fixture could not be enabled.");
    Assert(hub.TryStageTradeOffer(1, TradeOffer(1, 7, firstItem, 0, 0, 2), out _) == LegacyTradeOfferResult.PkModeBlocked, "PK mode did not block trade staging.");
    Assert(hub.SetCombatEligibilityState(1, false, false, 0), "The trade PK-mode fixture could not be cleared.");

    var invalidMoney = TradeOffer(1, 7, firstItem, 101, 0, 2);
    Assert(hub.TryStageTradeOffer(1, invalidMoney, out _) == LegacyTradeOfferResult.InvalidMoney, "An offer above the authoritative coin balance was accepted.");
    Assert(hub.TryStageTradeOffer(1, TradeOffer(1, 7, firstItem, 0, 0, 99), out _) == LegacyTradeOfferResult.TargetNotFound, "An unknown trade target was accepted.");
    Assert(hub.TryStageTradeOffer(1, TradeOffer(1, 7, firstItem, 0, 0, 1), out _) == LegacyTradeOfferResult.SameParticipant, "A self-trade was accepted.");

    static TradeOfferRequest TradeOffer(int connectionId, int position, LegacyItem item, int money, byte check, int opponentId)
    {
        var items = new LegacyItem[TradeOfferRequest.ItemCount];
        var positions = Enumerable.Repeat((sbyte)-1, TradeOfferRequest.ItemCount).ToArray();
        items[0] = item;
        positions[0] = checked((sbyte)position);
        return new TradeOfferRequest(items, positions, money, check, checked((ushort)opponentId));
    }

    static byte[] TradeMob(LegacyItem item, int position)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(50, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        item.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (position * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        return mob;
    }
}

static void WorldTradeCompletion()
{
    var firstItem = new LegacyItem(710, 1, 2, 3, 4, 5, 6);
    var secondItem = new LegacyItem(711, 6, 5, 4, 3, 2, 1);
    var firstMob = TradeMob([(0, firstItem)]);
    var secondMob = TradeMob([(0, secondItem)]);
    var hub = new WorldHub();
    Assert(hub.Enter(1, "COMPLETE_A", (_, _) => ValueTask.CompletedTask) && hub.Enter(2, "COMPLETE_B", (_, _) => ValueTask.CompletedTask), "Completion participants were not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 0, 0, 100, 2000, 2000, firstMob) && hub.SetCharacterState(2, 0, 0, 0, 0, 200, 2000, 2000, secondMob), "Completion participant state was not initialized.");

    Assert(hub.TryStageTradeOffer(1, Offer(2, [(0, firstItem)], 20, 0), out _) == LegacyTradeOfferResult.Accepted, "The completion offer from the first participant was not staged.");
    Assert(hub.TryStageTradeOffer(2, Offer(1, [(0, secondItem)], 30, 0), out _) == LegacyTradeOfferResult.Accepted, "The completion offer from the second participant was not staged.");
    Assert(hub.TryStageTradeOffer(1, Offer(2, [(0, firstItem)], 20, 1), out _) == LegacyTradeOfferResult.Accepted, "The first completion check was not staged.");
    Assert(hub.TryStageTradeOffer(2, Offer(1, [(0, secondItem)], 30, 1), out _) == LegacyTradeOfferResult.BothChecked, "The second completion check did not close the check phase.");

    Assert(hub.TryCompleteTrade(1, out var completion) == LegacyTradeCompletionResult.Accepted && completion is not null, "A fully checked trade did not complete atomically.");
    Assert(completion!.FirstCoin == 110 && completion.SecondCoin == 190, "Trade Gold arithmetic did not transfer the offered amounts correctly.");
    Assert(BinaryPrimitives.ReadInt32LittleEndian(completion.FirstMobSnapshot.AsSpan(LegacyAccountSnapshot.MobCoinOffset)) == 110 && BinaryPrimitives.ReadInt32LittleEndian(completion.SecondMobSnapshot.AsSpan(LegacyAccountSnapshot.MobCoinOffset)) == 190, "Trade Gold was not written into both authoritative MOB snapshots.");
    Assert(LegacyItem.Read(completion.FirstMobSnapshot.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes)) == secondItem && LegacyItem.Read(completion.SecondMobSnapshot.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes)) == firstItem, "The completed trade did not exchange the two carry items.");
    var codec = LegacyFrameCodec.CreateDefault();
    Assert(LegacyTradeCompletionRelay.TryBuild(completion, codec, 123456, 17, out var relayPlan) && relayPlan is not null, "An accepted trade did not produce a complete carry relay plan.");
    var firstCarryFrame = codec.Decode(relayPlan!.FirstCarryFrame);
    var secondCarryFrame = codec.Decode(relayPlan.SecondCarryFrame);
    Assert(firstCarryFrame.IsChecksumValid && firstCarryFrame.Header.Type == UpdateCarryConfirmation.MessageType && firstCarryFrame.Header.Id == 1 &&
        BinaryPrimitives.ReadInt32LittleEndian(firstCarryFrame.Payload.Span[UpdateCarryConfirmation.CoinOffset..]) == 110 &&
        LegacyItem.Read(firstCarryFrame.Payload.Span) == secondItem &&
        secondCarryFrame.IsChecksumValid && secondCarryFrame.Header.Type == UpdateCarryConfirmation.MessageType && secondCarryFrame.Header.Id == 2 &&
        BinaryPrimitives.ReadInt32LittleEndian(secondCarryFrame.Payload.Span[UpdateCarryConfirmation.CoinOffset..]) == 190 &&
        LegacyItem.Read(secondCarryFrame.Payload.Span) == firstItem,
        "The trade relay plan did not encode the exchanged carries and final Gold for each participant.");

    var offerItems = new LegacyItem[TradeOfferRequest.ItemCount];
    offerItems[0] = firstItem;
    var offerPositions = Enumerable.Repeat((sbyte)-1, TradeOfferRequest.ItemCount).ToArray();
    offerPositions[0] = 7;
    var offerRelayOutcome = new LegacyTradeOfferOutcome(
        1,
        2,
        Paired: true,
        new LegacyTradeParticipantState(1, 2, true, offerItems, offerPositions, 20, 1),
        new LegacyTradeParticipantState(2, 1, true, new LegacyItem[TradeOfferRequest.ItemCount], Enumerable.Repeat((sbyte)-1, TradeOfferRequest.ItemCount).ToArray(), 30, 0));
    Assert(LegacyTradeOfferRelay.TryBuild(offerRelayOutcome, codec, 123456, 17, out var offerRelayPlan) && offerRelayPlan is not null && offerRelayPlan.RecipientConnectionId == 2 && offerRelayPlan.RequiresCheckConfirmation, "The trade offer relay plan did not preserve the sender offer and unilateral check state.");
    var relayedOffer = codec.Decode(offerRelayPlan!.OfferFrame);
    Assert(relayedOffer.IsChecksumValid && relayedOffer.Header.Type == TradeOfferRequest.MessageType && relayedOffer.Header.Id == 2 &&
        BinaryPrimitives.ReadUInt16LittleEndian(relayedOffer.Payload.Span[TradeOfferRequest.OpponentIdOffset..]) == 1 &&
        BinaryPrimitives.ReadInt32LittleEndian(relayedOffer.Payload.Span[TradeOfferRequest.TradeMoneyOffset..]) == 20 &&
        relayedOffer.Payload.Span[TradeOfferRequest.MyCheckOffset] == 1 &&
        LegacyItem.Read(relayedOffer.Payload.Span) == firstItem,
        "The trade offer relay did not encode the sender as the receiver's opponent.");
    Assert(completion.RequiresPersistence && hub.TryCloseTrade(1, out _) == LegacyTradeCloseResult.NoActiveTrade, "A completed trade was not removed from the in-memory session and marked for persistence.");
    Assert(LegacyTradePersistence.TryBuild(completion, out var persistencePlan) && persistencePlan is not null &&
        persistencePlan.First.AccountName == "COMPLETE_A" && persistencePlan.Second.AccountName == "COMPLETE_B" &&
        persistencePlan.First.CharacterSlot == 0 && persistencePlan.Second.CharacterSlot == 0 &&
        persistencePlan.First.MobExtra.Length == LegacyAccountSnapshot.MobExtraStride && persistencePlan.Second.MobExtra.Length == LegacyAccountSnapshot.MobExtraStride,
        "An accepted trade did not produce the two-character persistence plan with account, slot, position, MOB, and MOBEXTRA metadata.");
    Assert(hub.TryRollbackTradeCompletion(completion),
        "The completed trade could not restore both runtime participants after a simulated persistence rejection.");
    Assert(hub.TryGetCharacterSnapshot(1, out var rolledBackFirst, out _, out _) && rolledBackFirst is not null &&
        rolledBackFirst.SequenceEqual(firstMob) &&
        hub.TryGetCharacterSnapshot(2, out var rolledBackSecond, out _, out _) && rolledBackSecond is not null &&
        rolledBackSecond.SequenceEqual(secondMob),
        "Trade runtime rollback did not restore the exact pre-completion MOB snapshots.");
    Assert(!hub.TryRollbackTradeCompletion(completion),
        "Trade runtime rollback accepted a stale post-completion snapshot twice.");

    var fullMob = TradeMob(Enumerable.Range(0, 30).Select(slot => (slot, new LegacyItem((short)(800 + slot), 0, 0, 0, 0, 0, 0))).ToArray());
    var donorItem1 = new LegacyItem(900, 0, 0, 0, 0, 0, 0);
    var donorItem2 = new LegacyItem(901, 0, 0, 0, 0, 0, 0);
    var donorMob = TradeMob([(0, donorItem1), (1, donorItem2)]);
    var noSpaceHub = new WorldHub();
    Assert(noSpaceHub.Enter(1, "NOSPACE_A", (_, _) => ValueTask.CompletedTask) && noSpaceHub.Enter(2, "NOSPACE_B", (_, _) => ValueTask.CompletedTask), "No-space participants were not registered.");
    Assert(noSpaceHub.SetCharacterState(1, 0, 0, 0, 0, 100, 2000, 2000, fullMob) && noSpaceHub.SetCharacterState(2, 0, 0, 0, 0, 100, 2000, 2000, donorMob), "No-space participant state was not initialized.");
    var emptyOffer = Offer(2, [], 0, 0);
    var donorOffer = Offer(1, [(0, donorItem1), (1, donorItem2)], 0, 0);
    Assert(noSpaceHub.TryStageTradeOffer(1, emptyOffer, out _) == LegacyTradeOfferResult.Accepted && noSpaceHub.TryStageTradeOffer(2, donorOffer, out _) == LegacyTradeOfferResult.Accepted, "The no-space trade offers were not staged.");
    Assert(noSpaceHub.TryStageTradeOffer(1, Offer(2, [], 0, 1), out _) == LegacyTradeOfferResult.Accepted && noSpaceHub.TryStageTradeOffer(2, Offer(1, [(0, donorItem1), (1, donorItem2)], 0, 1), out _) == LegacyTradeOfferResult.BothChecked, "The no-space trade checks were not staged.");
    Assert(noSpaceHub.TryCompleteTrade(1, out _) == LegacyTradeCompletionResult.NoSpace, "A trade without destination capacity was completed.");
    Assert(noSpaceHub.TryGetCharacterSnapshot(1, out var unchangedMob, out _, out _) && unchangedMob is not null && LegacyItem.Read(unchangedMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes)).Index == 800, "A no-space trade mutated the receiver carry before failing.");

    static TradeOfferRequest Offer(int opponentId, IReadOnlyList<(int Position, LegacyItem Item)> selected, int money, byte check)
    {
        var items = new LegacyItem[TradeOfferRequest.ItemCount];
        var positions = Enumerable.Repeat((sbyte)-1, TradeOfferRequest.ItemCount).ToArray();
        for (var index = 0; index < selected.Count; index++)
        {
            items[index] = selected[index].Item;
            positions[index] = checked((sbyte)selected[index].Position);
        }
        return new TradeOfferRequest(items, positions, money, check, checked((ushort)opponentId));
    }

    static byte[] TradeMob(IReadOnlyList<(int Position, LegacyItem Item)> items)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(50, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        foreach (var (position, item) in items)
            item.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (position * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        return mob;
    }
}

static void LegacyFileAtomicTradePersistence()
{
    var root = Path.Combine(Path.GetTempPath(), "wyd-cdk-file-trade-" + Guid.NewGuid().ToString("N"));
    var accountRoot = Path.Combine(root, "accounts");
    Directory.CreateDirectory(accountRoot);
    try
    {
        var firstPath = Path.Combine(accountRoot, "A", "ALPHA");
        var secondPath = Path.Combine(accountRoot, "B", "BETA");
        Directory.CreateDirectory(Path.GetDirectoryName(firstPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(secondPath)!);

        var firstMob = FileTradeMob("ALPHA", 100);
        var secondMob = FileTradeMob("BETA", 200);
        var firstExtra = FileTradeMobExtra(11);
        var secondExtra = FileTradeMobExtra(22);
        WriteFileTradeAccount(firstPath, 0, firstMob, firstExtra, 100);
        WriteFileTradeAccount(secondPath, 1, secondMob, secondExtra, 200);

        var firstUpdatedMob = firstMob.ToArray();
        var secondUpdatedMob = secondMob.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(firstUpdatedMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), 410);
        BinaryPrimitives.WriteInt32LittleEndian(secondUpdatedMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), 390);
        var firstUpdatedExtra = FileTradeMobExtra(111);
        var secondUpdatedExtra = FileTradeMobExtra(222);
        var firstRequest = new LegacyCharacterStateSaveRequest("alpha", 0, firstUpdatedMob, 2_200, 2_201, firstUpdatedExtra);
        var secondRequest = new LegacyCharacterStateSaveRequest("beta", 1, secondUpdatedMob, 2_300, 2_301, secondUpdatedExtra);
        var store = new LegacyFileAccountStore(accountRoot);

        Assert(store.TrySaveCharacterStatesAtomicallyAsync(firstRequest, secondRequest).GetAwaiter().GetResult() == AtomicCharacterStateSaveResult.Success,
            "The file account store rejected a valid two-account atomic trade commit.");
        Assert(FileTradeAccountMatches(firstPath, 0, firstUpdatedMob, firstUpdatedExtra, 410, 2_200, 2_201) &&
            FileTradeAccountMatches(secondPath, 1, secondUpdatedMob, secondUpdatedExtra, 390, 2_300, 2_301) &&
            !File.Exists(Path.Combine(accountRoot, ".wyd-cdk-atomic-state.journal")),
            "The file account store did not persist both trade snapshots, account coins, positions, and MOBEXTRA atomically.");

        Assert(store.TrySaveCharacterStatesAtomicallyAsync(firstRequest, firstRequest).GetAwaiter().GetResult() == AtomicCharacterStateSaveResult.InvalidRequest,
            "The file account store accepted a same-account pair without a same-file contract.");

        var firstBeforeRecovery = File.ReadAllBytes(firstPath);
        var secondBeforeRecovery = File.ReadAllBytes(secondPath);
        var firstPreparedAfter = firstBeforeRecovery.ToArray();
        var secondPreparedAfter = secondBeforeRecovery.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(firstPreparedAfter.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), 999);
        BinaryPrimitives.WriteInt32LittleEndian(secondPreparedAfter.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), 998);
        File.WriteAllBytes(firstPath, firstPreparedAfter);
        File.WriteAllBytes(secondPath, secondPreparedAfter);
        File.WriteAllText(Path.Combine(accountRoot, ".wyd-cdk-atomic-state.journal"), JsonSerializer.Serialize(new
        {
            Version = 1,
            Phase = "Prepared",
            Files = new[]
            {
                new { Path = firstPath, Before = firstBeforeRecovery, After = firstPreparedAfter },
                new { Path = secondPath, Before = secondBeforeRecovery, After = secondPreparedAfter },
            },
        }));

        var recoveringStore = new LegacyFileAccountStore(accountRoot);
        Assert(recoveringStore.TrySaveCharacterStatesAtomicallyAsync(firstRequest, secondRequest).GetAwaiter().GetResult() == AtomicCharacterStateSaveResult.Success &&
            File.ReadAllBytes(firstPath).SequenceEqual(firstBeforeRecovery) &&
            File.ReadAllBytes(secondPath).SequenceEqual(secondBeforeRecovery) &&
            !File.Exists(Path.Combine(accountRoot, ".wyd-cdk-atomic-state.journal")),
            "The file account store did not roll back a prepared trade journal before the next commit.");

        var firstCommittedBefore = File.ReadAllBytes(firstPath);
        var secondCommittedBefore = File.ReadAllBytes(secondPath);
        var firstCommittedAfter = firstCommittedBefore.ToArray();
        var secondCommittedAfter = secondCommittedBefore.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(firstCommittedAfter.AsSpan(LegacyAccountSnapshot.CharactersOffset + LegacyAccountSnapshot.MobCoinOffset), 777);
        BinaryPrimitives.WriteInt32LittleEndian(secondCommittedAfter.AsSpan(LegacyAccountSnapshot.CharactersOffset + LegacyAccountSnapshot.CharacterStride + LegacyAccountSnapshot.MobCoinOffset), 666);
        BinaryPrimitives.WriteInt32LittleEndian(firstCommittedAfter.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), 777);
        BinaryPrimitives.WriteInt32LittleEndian(secondCommittedAfter.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), 666);
        File.WriteAllText(Path.Combine(accountRoot, ".wyd-cdk-atomic-state.journal"), JsonSerializer.Serialize(new
        {
            Version = 1,
            Phase = "Committed",
            Files = new[]
            {
                new { Path = firstPath, Before = firstCommittedBefore, After = firstCommittedAfter },
                new { Path = secondPath, Before = secondCommittedBefore, After = secondCommittedAfter },
            },
        }));
        File.WriteAllBytes(firstPath, firstCommittedBefore);
        File.WriteAllBytes(secondPath, secondCommittedBefore);

        var committedFirstMob = firstUpdatedMob.ToArray();
        var committedSecondMob = secondUpdatedMob.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(committedFirstMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), 777);
        BinaryPrimitives.WriteInt32LittleEndian(committedSecondMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), 666);
        var committedFirstRequest = firstRequest with { Mob = committedFirstMob };
        var committedSecondRequest = secondRequest with { Mob = committedSecondMob };
        var committedResult = recoveringStore.TrySaveCharacterStatesAtomicallyAsync(committedFirstRequest, committedSecondRequest).GetAwaiter().GetResult();
        var firstCommittedMatches = File.ReadAllBytes(firstPath).SequenceEqual(firstCommittedAfter);
        var secondCommittedMatches = File.ReadAllBytes(secondPath).SequenceEqual(secondCommittedAfter);
        var committedJournalRemoved = !File.Exists(Path.Combine(accountRoot, ".wyd-cdk-atomic-state.journal"));
        Assert(committedResult == AtomicCharacterStateSaveResult.Success && firstCommittedMatches && secondCommittedMatches && committedJournalRemoved,
            $"The file account store did not complete a committed trade journal during recovery (result={committedResult}, first={firstCommittedMatches}, second={secondCommittedMatches}, journalRemoved={committedJournalRemoved}).");
    }
    finally
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    static byte[] FileTradeMob(string name, int coin)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        System.Text.Encoding.ASCII.GetBytes(name).CopyTo(mob, LegacyAccountSnapshot.MobNameOffset);
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), coin);
        return mob;
    }

    static byte[] FileTradeMobExtra(uint hold)
    {
        var extra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteUInt32LittleEndian(extra.AsSpan(LegacyAccountSnapshot.MobExtraHoldOffset), hold);
        return extra;
    }

    static void WriteFileTradeAccount(string path, int slot, byte[] mob, byte[] mobExtra, int accountCoin)
    {
        var file = new byte[LegacyAccountSnapshot.RequiredFileLength];
        mob.CopyTo(file.AsSpan(LegacyAccountSnapshot.CharactersOffset + (slot * LegacyAccountSnapshot.CharacterStride)));
        mobExtra.CopyTo(file.AsSpan(LegacyAccountSnapshot.MobExtraOffset + (slot * LegacyAccountSnapshot.MobExtraStride)));
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), accountCoin);
        File.WriteAllBytes(path, file);
    }

    static bool FileTradeAccountMatches(string path, int slot, byte[] mob, byte[] mobExtra, int accountCoin, short positionX, short positionY)
    {
        var file = File.ReadAllBytes(path);
        var mobOffset = LegacyAccountSnapshot.CharactersOffset + (slot * LegacyAccountSnapshot.CharacterStride);
        var extraOffset = LegacyAccountSnapshot.MobExtraOffset + (slot * LegacyAccountSnapshot.MobExtraStride);
        var expectedMob = mob.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(expectedMob.AsSpan(LegacyAccountSnapshot.MobSavedPositionXOffset), positionX);
        BinaryPrimitives.WriteInt16LittleEndian(expectedMob.AsSpan(LegacyAccountSnapshot.MobSavedPositionYOffset), positionY);
        return file.AsSpan(mobOffset, LegacyAccountSnapshot.CharacterStride).SequenceEqual(expectedMob) &&
            BinaryPrimitives.ReadInt16LittleEndian(file.AsSpan(mobOffset + LegacyAccountSnapshot.MobSavedPositionXOffset)) == positionX &&
            BinaryPrimitives.ReadInt16LittleEndian(file.AsSpan(mobOffset + LegacyAccountSnapshot.MobSavedPositionYOffset)) == positionY &&
            file.AsSpan(extraOffset, LegacyAccountSnapshot.MobExtraStride).SequenceEqual(mobExtra) &&
            BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(LegacyAccountSnapshot.AccountCoinOffset)) == accountCoin;
    }
}

static void WorldTradeDisconnectCleanup()
{
    var offeredItem = new LegacyItem(720, 1, 2, 3, 4, 5, 6);
    var firstMob = CreateTradeMob([(0, offeredItem)]);
    var secondMob = CreateTradeMob([]);
    var hub = new WorldHub();
    Assert(hub.Enter(1, "DISCONNECT_A", (_, _) => ValueTask.CompletedTask) &&
        hub.Enter(2, "DISCONNECT_B", (_, _) => ValueTask.CompletedTask) &&
        hub.Enter(3, "DISCONNECT_C", (_, _) => ValueTask.CompletedTask),
        "Disconnect trade participants were not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 0, 0, 100, 2000, 2000, firstMob) &&
        hub.SetCharacterState(2, 0, 0, 0, 0, 200, 2000, 2000, secondMob) &&
        hub.SetCharacterState(3, 0, 0, 0, 0, 100, 2000, 2000, CreateTradeMob([])),
        "Disconnect trade participant state was not initialized.");

    var items = new LegacyItem[TradeOfferRequest.ItemCount];
    var positions = Enumerable.Repeat((sbyte)-1, TradeOfferRequest.ItemCount).ToArray();
    items[0] = offeredItem;
    positions[0] = 0;
    var offer = new TradeOfferRequest(items, positions, 25, 0, 2);
    Assert(hub.TryStageTradeOffer(1, offer, out _) == LegacyTradeOfferResult.Accepted,
        "The disconnect trade offer was not staged.");

    Assert(hub.Leave(1, out var summons, out var closedTrade) &&
        summons.Count == 0 && closedTrade is { ConnectionId: 1, OpponentId: 2 },
        "Disconnect did not report and clear the staged bilateral trade.");
    Assert(hub.TryCloseTrade(2, out _) == LegacyTradeCloseResult.NoActiveTrade,
        "Disconnect left the counterpart trade state active.");
    Assert(hub.TryGetCharacterSnapshot(2, out var unchangedMob, out _, out _) &&
        unchangedMob is not null &&
        unchangedMob.SequenceEqual(secondMob),
        "Disconnect cleanup mutated the remaining participant's carry or Gold.");
    var postDisconnectItems = new LegacyItem[TradeOfferRequest.ItemCount];
    var postDisconnectPositions = Enumerable.Repeat((sbyte)-1, TradeOfferRequest.ItemCount).ToArray();
    Assert(hub.TryStageTradeOffer(2, new TradeOfferRequest(postDisconnectItems, postDisconnectPositions, 201, 0, 3), out _) == LegacyTradeOfferResult.InvalidMoney,
        "Disconnect cleanup changed the remaining participant's authoritative Gold.");

    static byte[] CreateTradeMob(IReadOnlyList<(int Position, LegacyItem Item)> items)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(50, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        foreach (var (position, item) in items)
            item.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (position * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        return mob;
    }
}

static async Task ReadExactlyAsync(NetworkStream stream, byte[] destination)
{
    var offset = 0;
    while (offset < destination.Length)
    {
        var read = await stream.ReadAsync(destination.AsMemory(offset), CancellationToken.None);
        if (read == 0)
            throw new InvalidOperationException($"Loopback receiver ended after {offset} of {destination.Length} bytes.");

        offset += read;
    }
}

static void SkillDataTable()
{
    using var reader = new StringReader("""
        # Id, SkillPoint, TargetType, ManaSpent, Delay, Range, InstanceType, InstanceValue, TickType, TickValue, AffectType, AffectValue, AffectTime, Act123, Act123, InstanceAttribute, TickAttribute, Aggressive, Maxtarget, PartyCheck, AffectResist, Passive, Name
        9,45,0,0,0,0,0,0,0,0,0,0,0,20.0.0.24.0.0.0.0,20.0.0.8.0.0.0.0,0,0,0,0,0,0,1,0,Mestre_das_Armas
        10,69,1,20,15,1,1,40,0,0,0,0,600,8.0.0.7.0.0.0.0,8.0.0.7.0.0.0.0,0,0,1,2,0,0,0,1,Golpe_Mortal
        248,1,1,1,1,1,1,1,1,1,1,1,1,1.1.1.1.1.1,1.1.1.1.1.1,1,1,1,1,1,1,fora_do_limite
        """);

    var table = LegacySkillDataTable.Load(reader);
    Assert(table.Count == 2, "SkillData.csv did not load the two valid in-range rows.");

    var weaponMastery = table[9];
    Assert(weaponMastery is not null && weaponMastery.Name == "Mestre_das_Armas" && weaponMastery.Passive == 1, "Skill 9 differs from the legacy table.");
    Assert(weaponMastery!.Action1[3] == 24 && weaponMastery.Action2[3] == 8 && weaponMastery.EffectiveActions[3] == 8, "The duplicated Act-column behavior differs from the legacy server parser.");

    var mortalStrike = table[10];
    Assert(mortalStrike is not null && mortalStrike.Name == "Golpe_Mortal" && mortalStrike.ManaSpent == 20 && mortalStrike.Delay == 15 && mortalStrike.AffectTime == 150 && mortalStrike.InstanceValue == 40 && mortalStrike.Aggressive == 1 && mortalStrike.MaxTarget == 2 && mortalStrike.ClientForceDamage == 1, "Skill 10 differs from the project base table or AffectTime conversion.");

    using var olderReader = new StringReader("10,69,1,20,15,1,1,100,0,0,0,0,0,8.0.0.7.0.0.0.0,8.0.0.7.0.0.0.0,0,0,1,2,0,0,0,Golpe_Mortal");
    var olderTable = LegacySkillDataTable.Load(olderReader);
    Assert(olderTable[10] is { InstanceValue: 100, ClientForceDamage: 0, Name: "Golpe_Mortal" }, "The older 23-field SkillData.csv variant was not preserved.");
}

static void SkillAttackGate()
{
    using var reader = new StringReader("""
        9,45,0,0,0,0,0,0,0,0,0,0,0,20.0.0.24.0.0.0.0,20.0.0.8.0.0.0.0,0,0,0,0,0,0,1,0,Mestre_das_Armas
        10,69,1,20,15,1,1,40,0,0,0,0,0,8.0.0.7.0.0.0.0,8.0.0.7.0.0.0.0,0,0,1,2,0,0,0,1,Golpe_Mortal
        """);
    var table = LegacySkillDataTable.Load(reader);
    var codec = LegacyFrameCodec.CreateDefault();
    var requestPayload = new byte[AttackRequest.OneTargetPacketSize - PacketHeader.SizeInBytes];
    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(requestPayload.AsSpan(44), 10);
    var requestFrame = codec.Decode(codec.Encode(AttackRequest.OneTargetMessageType, 4, 123, requestPayload, 16));
    Assert(AttackRequest.TryParse(requestFrame, out var request) && request is not null, "Skill gate fixture attack was not parsed.");
    var parsedRequest = request ?? throw new InvalidOperationException("Skill gate fixture attack was not parsed.");

    var accepted = LegacySkillAttackGate.Validate(table, parsedRequest, characterClass: 0, learnedSkill: 1u << 10, targetIndex: 0);
    Assert(accepted == SkillAttackValidationResult.Accepted, "Learned skill 10 was rejected by the metadata gate.");
    Assert(LegacySkillAttackGate.Validate(table, parsedRequest, 0, 0, 0) == SkillAttackValidationResult.SkillNotLearned, "An unlearned skill was accepted.");
    Assert(LegacySkillAttackGate.Validate(table, parsedRequest, 1, 1u << 10, 0) == SkillAttackValidationResult.WrongClass, "A skill from another class was accepted.");
    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(requestPayload.AsSpan(44), 9);
    var passiveFrame = codec.Decode(codec.Encode(AttackRequest.OneTargetMessageType, 4, 123, requestPayload, 16));
    Assert(AttackRequest.TryParse(passiveFrame, out var passiveRequest) && passiveRequest is not null, "Passive skill fixture was not parsed.");
    Assert(LegacySkillAttackGate.Validate(table, passiveRequest!, 0, 1u << 9, 0) == SkillAttackValidationResult.PassiveSkill, "Passive skill 9 was accepted as an attack.");
    Assert(LegacySkillAttackGate.Validate(table, parsedRequest, 0, 1u << 10, 3) == SkillAttackValidationResult.TooManyTargets, "A target index above skill 10 MaxTarget was accepted.");
    Assert(LegacySkillAttackGate.Validate(table, parsedRequest, 0, 0, 0, skipClientChecks: true) == SkillAttackValidationResult.Accepted, "The explicit legacy skip-check path was not preserved.");
}

static void LegacyMobCombatLayout()
{
    var mob = new byte[LegacyAccountSnapshot.CharacterStride];
    mob[LegacyAccountSnapshot.MobClassOffset] = 3;
    BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), 0x80);
    BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobRegenMpOffset), 12);
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), unchecked((int)0x80000005));
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobMagicOffset), 321);
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCurrentMpOffset), 9876);

    var state = LegacyMobCombatState.Read(mob);
    Assert(state.CharacterClass == 3 && state.Rsv == 0x80 && state.RegenMp == 12 && state.LearnedSkill == 0x80000005u && state.Magic == 321 && state.CurrentMana == 9876, "STRUCT_MOB combat offsets differ from the confirmed x86 layout.");

    var score = new LegacyScore(100, 0, 0, 0, 0, 0, 0, 0, 0, 9876, 9876, 0, 0, 0, 0, 0, 40, 0, 0);
    score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    mob[LegacyAccountSnapshot.MobSaveManaOffset] = 10;
    state = LegacyMobCombatState.Read(mob);
    using var skillReader = new StringReader("10,69,1,20,15,1,1,40,0,0,0,0,0,8.0.0.7.0.0.0.0,8.0.0.7.0.0.0.0,0,0,1,2,0,0,0,1,Golpe_Mortal");
    var skill = LegacySkillDataTable.Load(skillReader)[10]!;
    Assert(LegacySkillCombatMath.GetManaSpent(skill, state) == 21, "Legacy BASE_GetManaSpent integer formula was not preserved.");
}

static void LegacyMapGridAttributes()
{
    var height = new byte[LegacyMapGrid.HeightMapSize];
    var attributes = new byte[LegacyMapGrid.AttributeMapSize];
    var blockedWorldX = 2088;
    var blockedWorldY = 2100;
    attributes[(blockedWorldY >> 2) * LegacyMapGrid.AttributeWidth + (blockedWorldX >> 2)] = 2;

    var map = new LegacyMapGrid(height, attributes);
    Assert(map.IsBlocked(blockedWorldX, blockedWorldY) && map.IsBlocked(blockedWorldX + 3, blockedWorldY + 3), "AttributeMap bit 0x02 was not expanded to the legacy 4x4 world-cell block.");
    Assert(!map.IsBlocked(blockedWorldX + 4, blockedWorldY) && !map.IsBlocked(0, 0), "AttributeMap blocking leaked outside its 4x4 tile.");
    Assert(map.IsBlocked(-1, 0) && map.IsBlocked(LegacyMapGrid.HeightWidth, 0), "Out-of-range map cells were not treated as blocked.");

    var releasedAttributes = new byte[LegacyMapGrid.AttributeMapSize + 4];
    releasedAttributes[(blockedWorldY >> 2) * LegacyMapGrid.AttributeWidth + (blockedWorldX >> 2)] = 2;
    var releasedMap = new LegacyMapGrid(height, releasedAttributes);
    Assert(releasedMap.IsBlocked(blockedWorldX, blockedWorldY), "The legacy four-byte AttributeMap trailer was not ignored.");
}

static void LegacyMovementMapGate()
{
    var height = new byte[LegacyMapGrid.HeightMapSize];
    var attributes = new byte[LegacyMapGrid.AttributeMapSize];
    var newbieX = 2100;
    var newbieY = 2100;
    var guildX = 2120;
    var guildY = 2120;
    attributes[(newbieY >> 2) * LegacyMapGrid.AttributeWidth + (newbieX >> 2)] = LegacyMovementMapRules.NewbieZoneAttribute;
    attributes[(guildY >> 2) * LegacyMapGrid.AttributeWidth + (guildX >> 2)] = LegacyMovementMapRules.GuildZoneAttribute;
    var map = new LegacyMapGrid(height, attributes);
    var guildState = new LegacyGuildZoneState(1, [77, 0, 0, 0, 0], [0, 0, 0, 0, 0], [10, 10, 10, 10, 10], [5, 5, 5, 5, 5], [0, 0, 0, 0, 0]);
    var hub = new WorldHub(map, guildState);
    var mob = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(35, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));

    Assert(hub.Enter(1, "MAPGATE", (_, _) => ValueTask.CompletedTask), "Movement map-gate participant was not registered.");
    Assert(hub.SetCharacterState(1, 0, 7, 1, 0, 0, 2000, 2000, mob), "Movement map-gate character state was not registered.");
    Assert(hub.EvaluateMovementMapTarget(1, 2000, 2000) == LegacyMovementMapRestriction.None, "The action handler applied a target-cell map gate when the target did not change.");
    Assert(hub.EvaluateMovementMapTarget(1, (short)newbieX, (short)newbieY) == LegacyMovementMapRestriction.NewbieZone, "A level-35 mortal was allowed into a 0x80 newbie cell.");

    new LegacyScore(34, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    Assert(hub.SetCharacterState(1, 0, 7, 1, 0, 0, 2000, 2000, mob), "Movement map-gate low-level state was not refreshed.");
    Assert(hub.EvaluateMovementMapTarget(1, (short)newbieX, (short)newbieY) == LegacyMovementMapRestriction.None, "A mortal below FREEEXP was blocked by a 0x80 newbie cell.");

    new LegacyScore(35, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    Assert(hub.SetCharacterState(1, 0, 7, 1, 0, 0, 2000, 2000, mob), "Movement map-gate guild state was not refreshed.");
    Assert(hub.EvaluateMovementMapTarget(1, (short)guildX, (short)guildY) == LegacyMovementMapRestriction.GuildZone, "A non-owner guild was allowed into a 0x20 city guild zone.");
    Assert(hub.TryRecallForMovementRestriction(1, out var recall, cityRandomX: 0, cityRandomY: 0) && recall is not null && recall.FromX == 2000 && recall.FromY == 2000 && recall.ToX == 2086 && recall.ToY == 2093, "A movement map restriction did not reproduce the deterministic DoRecall city position.");
    Assert(hub.TryGetCharacterSnapshot(1, out _, out var recalledX, out var recalledY) && recalledX == 2086 && recalledY == 2093, "DoRecall did not persist the authoritative position.");
    Assert(hub.SetCharacterState(1, 0, 77, 1, 0, 0, 2000, 2000, mob) && hub.EvaluateMovementMapTarget(1, (short)guildX, (short)guildY) == LegacyMovementMapRestriction.None, "The owner guild was blocked from its own city guild zone.");

    Assert(LegacyMovementMapNotice.For(LegacyMovementMapRestriction.None) is null, "An unrestricted movement target unexpectedly produced a notice.");
    Assert(LegacyMovementMapNotice.For(LegacyMovementMapRestriction.NewbieZone) == "Somente n\u00EDvel 35 ou inferior pode entrar no campo de treinamento.", "The newbie-zone notice differs from Language.txt entry 46.");
    Assert(LegacyMovementMapNotice.For(LegacyMovementMapRestriction.GuildZone) == "Voc\u00EA n\u00E3o pode entrar na zona de outra guilda.", "The guild-zone notice differs from Language.txt entry 47.");
    var noticeCodec = LegacyFrameCodec.CreateDefault();
    var notice = noticeCodec.Decode(new MessagePanelConfirmation(LegacyMovementMapNotice.GuildZone).ToFrame(noticeCodec, 33, 16));
    var expectedNoticeBytes = System.Text.Encoding.Latin1.GetBytes(LegacyMovementMapNotice.GuildZone);
    Assert(notice.IsChecksumValid && notice.Header.Type == MessagePanelConfirmation.MessageType && notice.Header.Size == MessagePanelConfirmation.PacketSize && notice.Payload.Span[..expectedNoticeBytes.Length].SequenceEqual(expectedNoticeBytes), "The movement restriction notice did not preserve the Language.txt ANSI bytes in MSG_MessagePanel.");

    new LegacyScore(400, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    Assert(hub.SetCharacterState(1, 0, 7, 1, 0, 0, 2000, 2000, mob) && hub.EvaluateMovementMapTarget(1, (short)guildX, (short)guildY) == LegacyMovementMapRestriction.None, "A level above MAX_LEVEL was blocked by a 0x20 city guild zone.");
}

static void LegacyReleaseMapRecall()
{
    var run = FindReference759TmsrvRun();
    var map = LegacyMapGrid.Load(Path.Combine(run, "HeightMap.dat"), Path.Combine(run, "AttributeMap.dat"));
    var guildZones = LegacyGuildZoneState.Load(Path.Combine(run, "Guild.txt"));

    // Erion city spawn + (0, 7) is walkable, while the first candidate
    // reached after the surrounding cells are occupied is terrain-blocked in
    // the real release map. The legacy fallback checks the origin height in
    // this loop, so it still accepts that blocked candidate.
    Assert(!map.IsBlocked(2453, 2007) && map.IsBlocked(2452, 2008), "The real release map no longer matches the recorded Erion recall fixture.");
    Assert(guildZones.GuildCounter == 2 && guildZones.GetChargeGuild(0) == 0, "The real release Guild.txt was not loaded.");

    var deadMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var score = new LegacyScore(100, 0, 0, 128, 0, 0, 0, 1000, 100, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    score.Write(deadMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    var resurrection = new LegacySkillDefinition(99, 35, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, [], [], 0, 0, 0, 1, 0, 0, 0, 0, "Ressureicao");
    var hub = new WorldHub(map, guildZones);
    Assert(hub.Enter(1, "RELEASEDEAD", (_, _) => ValueTask.CompletedTask), "Real release recall participant was not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, deadMob), "Real release recall dead state was not registered.");

    var occupied = new[] { (2452, 2006), (2453, 2006), (2454, 2006), (2452, 2007), (2453, 2007), (2454, 2007) };
    for (var i = 0; i < occupied.Length; i++)
    {
        var connectionId = i + 2;
        Assert(hub.Enter(connectionId, $"OCCUPIED{i}", (_, _) => ValueTask.CompletedTask), "Real release recall occupancy participant was not registered.");
        Assert(hub.SetCharacterState(connectionId, 0, 0, 0, 0, 0, (short)occupied[i].Item1, (short)occupied[i].Item2, ReadOnlyMemory<byte>.Empty), "Real release recall occupancy state was not registered.");
    }

    var result = hub.TryApplySkillAttack(1, 1, resurrection, null, 0, 90, out var outcome, resurrectionRoll: 40, resurrectionHpRoll: 0, resurrectionMpRoll: 0, recallCityRandomX: 0, recallCityRandomY: 7);
    Assert(result == LegacySkillAttackResult.Accepted && outcome?.RecallPositionX == 2452 && outcome.RecallPositionY == 2008, "Real release recall did not preserve the legacy candidate-height quirk.");

    var strictHub = new WorldHub(map, guildZones, LegacyMapCollisionMode.CandidateAware);
    Assert(strictHub.Enter(1, "STRICTDEAD", (_, _) => ValueTask.CompletedTask) && strictHub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, deadMob), "Strict release recall participant was not registered.");
    for (var i = 0; i < occupied.Length; i++)
    {
        var connectionId = i + 2;
        Assert(strictHub.Enter(connectionId, $"STRICTOCCUPIED{i}", (_, _) => ValueTask.CompletedTask), "Strict release occupancy participant was not registered.");
        Assert(strictHub.SetCharacterState(connectionId, 0, 0, 0, 0, 0, (short)occupied[i].Item1, (short)occupied[i].Item2, ReadOnlyMemory<byte>.Empty), "Strict release occupancy state was not registered.");
    }

    var strictResult = strictHub.TryApplySkillAttack(1, 1, resurrection, null, 0, 90, out var strictOutcome, resurrectionRoll: 40, resurrectionHpRoll: 0, resurrectionMpRoll: 0, recallCityRandomX: 0, recallCityRandomY: 7);
    Assert(strictResult == LegacySkillAttackResult.Accepted && strictOutcome?.RecallPositionX == 2451 && strictOutcome.RecallPositionY == 2005, $"Candidate-aware release recall did not skip the blocked terrain cell: result={strictResult}, position=({strictOutcome?.RecallPositionX},{strictOutcome?.RecallPositionY}).");
}

static string FindReference759TmsrvRun()
{
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
        var candidate = Path.Combine(directory.FullName, "Backup", "Tools", "Reference759", "SERVER", "TMSrv", "run");
        if (Directory.Exists(candidate)) return candidate;
    }

    throw new DirectoryNotFoundException("Backup\\Tools\\Reference759\\SERVER\\TMSrv\\run was not found from the test output directory.");
}

static string FindReference769TmsrvRun()
{
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
        var candidate = Path.Combine(directory.FullName, "Backup", "Tools", "ReferenceSources", "TMProject2GlobalClient", "Servidor", "Server", "TMSrv", "run");
        if (Directory.Exists(candidate)) return candidate;
    }

    throw new DirectoryNotFoundException("Backup\\Tools\\ReferenceSources\\TMProject2GlobalClient\\Servidor\\Server\\TMSrv\\run was not found from the test output directory.");
}

static void CharacterLoginSpawnPosition()
{
    var mob = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(1, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    BinaryPrimitives.WriteInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobSavedPositionXOffset), 2096);
    BinaryPrimitives.WriteInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobSavedPositionYOffset), 2096);

    var hub = new WorldHub();
    var position = hub.ResolveCharacterLoginPosition(1, mob, LegacyAccountSnapshot.ClassMasterMortal, newbieRandomX: 2, newbieRandomY: 3);
    Assert(position == (2100, 2101), $"Character login did not use the legacy newbie live spawn: ({position.X},{position.Y}).");
    Assert(position != (2096, 2096), "Character login incorrectly reused saved SPX/SPY as the live spawn.");
}

static void LegacyGuildZoneStateFile()
{
    var root = Path.Combine(Path.GetTempPath(), $"wyd-cdk-guild-zone-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    try
    {
        var path = Path.Combine(root, "Guild.txt");
        File.WriteAllText(path, "12 77 0 0 0 99\n0 0 0 0 0\n-1 21 3 4 5\n1 2 3 4 5\n5 4 3 2 1\n");
        var guildZones = LegacyGuildZoneState.Load(path);
        Assert(guildZones.GuildCounter == 12 && guildZones.GetChargeGuild(0) == 77 && guildZones.GetChargeGuild(4) == 99, "Guild.txt ChargeGuild values were not loaded in the legacy zone order.");
        Assert(guildZones.CityTaxes[0] == 10 && guildZones.CityTaxes[1] == 10 && guildZones.CityTaxes[2] == 3, "Guild.txt city-tax clamping differs from CReadFiles::ReadGuild.");

        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(40, 0, 0, 0, 0, 0, 0, 1000, 100, 1000, 100, 0, 0, 0, 0, 0, 0, 0, 0);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), 0);
        var skill = new LegacySkillDefinition(99, 35, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, [], [], 0, 0, 0, 1, 0, 0, 0, 0, "Ressureicao");
        var hub = new WorldHub(guildZones: guildZones);
        Assert(hub.Enter(1, "GUILDDEAD", (_, _) => ValueTask.CompletedTask) && hub.SetCharacterState(1, 0, 77, 1, 0, 0, 2000, 2000, mob), "Guild-zone recall fixture was not registered.");
        var result = hub.TryApplySkillAttack(1, 1, skill, null, 0, 90, out var outcome, resurrectionRoll: 40, resurrectionHpRoll: 0, resurrectionMpRoll: 0);
        Assert(result == LegacySkillAttackResult.Accepted && outcome?.RecallPositionX == 2088 && outcome.RecallPositionY == 2148, "ChargeGuild did not redirect resurrection to the configured guild spawn.");
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}

static void ActionParser()
{
    var payload = new byte[ActionRequest.PacketSize - PacketHeader.SizeInBytes];
    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(payload, 100);
    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(2), 200);
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4), 0);
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(8), 6);
    System.Text.Encoding.ASCII.GetBytes("0123456789").CopyTo(payload, 12);
    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(36), 120);
    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(38), 220);

    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(codec.Encode(ActionRequest.MessageType, 4, 33, payload, 16));
    Assert(ActionRequest.TryParse(frame, out var action), "Valid action packet was not parsed.");
    Assert(action!.PositionX == 100 && action.PositionY == 200 && action.Speed == 6 && action.TargetX == 120 && action.TargetY == 220 && action.Route[0] == (byte)'0', "Parsed action fields differ.");

    var stop = codec.Decode(codec.Encode(ActionRequest.StopMessageType, 4, 33, payload, 16));
    Assert(ActionRequest.TryParse(stop, out var stopAction) && stopAction!.WireType == ActionRequest.StopMessageType, "MSG_Action2 was not accepted with its opcode variant preserved.");
    var reemittedStop = codec.Decode(stopAction!.ToFrame(codec, 33, 16, 4));
    Assert(reemittedStop.Header.Type == ActionRequest.StopMessageType, "Re-emitted MSG_Action2 changed into MSG_Action.");

    var illusion = codec.Decode(codec.Encode(ActionRequest.IllusionMessageType, 4, 33, payload, 16));
    Assert(ActionRequest.TryParse(illusion, out var illusionAction) && illusionAction!.WireType == ActionRequest.IllusionMessageType, "MSG_Action3 was not accepted with its opcode variant preserved.");
    var reemittedIllusion = codec.Decode(illusionAction!.ToFrame(codec, 33, 16, 4));
    Assert(reemittedIllusion.Header.Type == ActionRequest.IllusionMessageType, "Re-emitted MSG_Action3 changed into MSG_Action.");

    var echoed = codec.Decode(new ActionRequest(100, 200, 0, 6, payload[12..36], 120, 220).ToFrame(codec, 33, 16, 4));
    Assert(ActionRequest.TryParse(echoed, out var echoedAction) && echoedAction!.TargetX == 120 && echoedAction.TargetY == 220 && echoed.Header.ClientTick == 33, "Action response did not preserve the input timestamp.");
}

static void ClientTickPolicyRules()
{
    Assert(!ClientTickPolicy.IsAllowedFromClient(ClientTickPolicy.SkipCheckTick), "Reserved internal-server ClientTick was accepted from a client.");
    Assert(ClientTickPolicy.IsAllowedFromClient(0) && ClientTickPolicy.IsAllowedFromClient(ClientTickPolicy.SkipCheckTick - 1), "Normal client timestamps were rejected.");
}

static void AttackTimingGate()
{
    var sessions = new LoginSessionRegistry();
    var login = new AccountLoginRequest("FIGHTER", "secret", new byte[52], 7640, 0, [0, 0, 0, 0]);
    Assert(sessions.Open(5) && sessions.BeginAccountLogin(5, login) == LoginTransitionResult.Accepted && sessions.CompleteAccountLogin(5, "FIGHTER") == LoginTransitionResult.Accepted && sessions.BeginCharacterWait(5) == LoginTransitionResult.Accepted && sessions.CompleteCharacterLogin(5) == LoginTransitionResult.Accepted, "Attack timing fixture did not enter USER_PLAY.");
    Assert(sessions.TryAcceptAttackTiming(5, 1_000_000, 1_000_000) == AttackTimingResult.Accepted, "First valid attack timestamp was rejected.");
    Assert(sessions.TryAcceptAttackTiming(5, 1_000_799, 1_000_799) == AttackTimingResult.TooSoon, "Attack inside the 800ms legacy limit was accepted.");
    Assert(sessions.TryAcceptAttackTiming(5, 1_000_800, 1_000_800) == AttackTimingResult.Accepted, "Attack exactly at the 800ms legacy limit was rejected.");
    Assert(sessions.TryAcceptAttackTiming(5, ClientTickPolicy.SkipCheckTick, 1_000_800) == AttackTimingResult.ReservedTimestamp, "Reserved attack timestamp was accepted.");
    Assert(sessions.TryAcceptAttackTiming(5, 1_136_000, 1_120_999) == AttackTimingResult.OutsideServerWindow, "Attack more than 15s ahead of server time was accepted.");

    var oldTimestampSession = new LoginSessionRegistry();
    Assert(oldTimestampSession.Open(6) && oldTimestampSession.BeginAccountLogin(6, login) == LoginTransitionResult.Accepted && oldTimestampSession.CompleteAccountLogin(6, "FIGHTER") == LoginTransitionResult.Accepted && oldTimestampSession.BeginCharacterWait(6) == LoginTransitionResult.Accepted && oldTimestampSession.CompleteCharacterLogin(6) == LoginTransitionResult.Accepted, "Old-timestamp fixture did not enter USER_PLAY.");
    Assert(oldTimestampSession.TryAcceptAttackTiming(6, 900_799, 1_020_800) == AttackTimingResult.OutsideServerWindow, "Attack more than 120s behind server time was accepted.");
}

static void AttackParser()
{
    var codec = LegacyFrameCodec.CreateDefault();
    foreach (var (type, size) in new[] { (AttackRequest.AreaMessageType, AttackRequest.AreaPacketSize), (AttackRequest.OneTargetMessageType, AttackRequest.OneTargetPacketSize), (AttackRequest.TwoTargetMessageType, AttackRequest.TwoTargetPacketSize) })
    {
        Assert(AttackRequest.GetTargetCount(type) == (type == AttackRequest.AreaMessageType ? 13 : type == AttackRequest.TwoTargetMessageType ? 2 : 1), "Attack target-count mapping differs from the three legacy packet layouts.");
        var payload = new byte[size - PacketHeader.SizeInBytes];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(22), 2000);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(24), 2001);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(26), 2010);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(28), 2011);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), 42);
        payload[34] = 7;
        payload[35] = 3;
        System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), 72);
        System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(46), 55);
        var frame = codec.Decode(codec.Encode(type, 4, 123456, payload, 16));
        Assert(AttackRequest.TryParse(frame, out var attack) && attack!.MessageType == type && attack.PositionX == 2000 && attack.PositionY == 2001 && attack.TargetX == 2010 && attack.TargetY == 2011 && attack.AttackerId == 42 && attack.Motion == 7 && attack.SkillParameter == 3 && attack.SkillIndex == 72 && attack.RequestedMp == 55, "Attack intent differs from the legacy packet layout.");
    }
}

static void AttackResponseWire()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var payload = new byte[AttackRequest.OneTargetPacketSize - PacketHeader.SizeInBytes];
    BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4), 999); // client-declared HP must be replaced
    BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(12), 111); // client-declared EXP must be replaced
    BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(40), 1); // client-declared MP must be replaced
    BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(46), 2); // client-declared ReqMp must be replaced
    BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), 10);
    BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(48), 2);
    BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(48), 999); // client-declared damage must not be relayed as authority
    BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(52), -2);
    var input = codec.Decode(codec.Encode(AttackRequest.OneTargetMessageType, 4, 123456, payload, 16));
    Assert(AttackRequest.TryReadDamageSlot(input, 0, out var targetId, out var declaredDamage) && targetId == 999 && declaredDamage == -2, "Attack target/damage intent was not read from the legacy damage slot.");
    var mob = new byte[LegacyAccountSnapshot.CharacterStride];
    BinaryPrimitives.WriteInt64LittleEndian(mob.AsSpan(32), 987654);
    BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(92 + 24), 700);
    BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(120), 79);

    Assert(AttackConfirmation.TryCreate(input, mob, 79, out var confirmation) && confirmation is not null, "Authoritative attack confirmation was not created.");
    var confirmed = confirmation ?? throw new InvalidOperationException("Authoritative attack confirmation was not created.");
    var response = codec.Decode(confirmed.ToFrame(codec, input.Header.ClientTick, 17));
    Assert(response.IsChecksumValid && response.Header.Type == AttackRequest.OneTargetMessageType && response.Header.Id == AttackConfirmation.SceneId && response.Header.ClientTick == 123456, "Attack response header differs from the legacy relay.");
    Assert(BinaryPrimitives.ReadInt32LittleEndian(response.Payload.Span[4..]) == 700 && BinaryPrimitives.ReadInt64LittleEndian(response.Payload.Span[12..]) == 987654 && BinaryPrimitives.ReadInt32LittleEndian(response.Payload.Span[40..]) == 79 && BinaryPrimitives.ReadInt16LittleEndian(response.Payload.Span[46..]) == 79, "Attack response did not replace HP/EXP/MP/ReqMp with authoritative values.");
    Assert(BinaryPrimitives.ReadInt32LittleEndian(response.Payload.Span[48..]) == 0, "Attack response relayed client-declared damage.");
    Assert(confirmed.TrySetDamageSlot(0, 2, 23), "Authoritative damage slot could not be filled.");
    var resolved = codec.Decode(confirmed.ToFrame(codec, input.Header.ClientTick, 17));
    Assert(BinaryPrimitives.ReadUInt16LittleEndian(resolved.Payload.Span[48..]) == 2 && BinaryPrimitives.ReadInt32LittleEndian(resolved.Payload.Span[52..]) == 23, "Authoritative target/damage slot was not encoded.");
}

static void AttackAuthoritativePipeline()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var payload = new byte[AttackRequest.OneTargetPacketSize - PacketHeader.SizeInBytes];
    BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(22), 2000);
    BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(24), 2000);
    BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(26), 2005);
    BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(28), 2005);
    BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), 10);
    BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(46), 999); // client MP is intent only
    BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(48), 2);
    BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(52), 999999); // client damage is intent only
    var input = codec.Decode(codec.Encode(AttackRequest.OneTargetMessageType, 4, 1_000_000, payload, 16));
    Assert(AttackRequest.TryParse(input, out var request) && request is not null, "Integrated attack fixture was not parsed.");
    Assert(AttackRequest.TryReadDamageSlot(input, 0, out var targetId, out var declaredDamage) && targetId == 2 && declaredDamage == 999999, "Integrated attack intent was not preserved for server-side resolution.");

    using var skillReader = new StringReader("10,69,1,20,15,1,1,40,0,0,0,0,0,8.0.0.7.0.0.0.0,8.0.0.7.0.0.0.0,0,0,1,2,0,0,0,1,Golpe_Mortal");
    var skills = LegacySkillDataTable.Load(skillReader);
    Assert(LegacySkillAttackGate.Validate(skills, request!, characterClass: 0, learnedSkill: 1u << 10, targetIndex: 0) == SkillAttackValidationResult.Accepted, "Integrated attack fixture did not pass the legacy skill gate.");

    var attackerMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var targetMob = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(100, 0, 100, 0, 0, 0, 0, 1000, 1000, 900, 100, 0, 0, 0, 0, 0, 40, 0, 0).Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    new LegacyScore(100, 10, 0, 0, 0, 0, 0, 100, 100, 100, 50, 0, 0, 0, 0, 0, 0, 0, 0).Write(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    attackerMob[LegacyAccountSnapshot.MobClassOffset] = 0;
    BinaryPrimitives.WriteUInt32LittleEndian(attackerMob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), 1u << 10);
    attackerMob[LegacyAccountSnapshot.MobSaveManaOffset] = 10;

    var world = new WorldHub();
    Assert(world.Enter(1, "ATTACKER", (_, _) => ValueTask.CompletedTask) && world.Enter(2, "TARGET", (_, _) => ValueTask.CompletedTask), "Integrated attack participants were not registered.");
    Assert(world.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob) && world.SetCharacterState(2, 0, 0, 2, 0, 0, 2005, 2005, targetMob), "Integrated attack participant state was not set.");

    Assert(world.TryConsumeSkillMana(1, skills[10]!, out _, out var authoritativeMob, out var requestedMp) == LegacySkillManaResult.Accepted && authoritativeMob is not null && requestedMp == 79, "Integrated attack did not consume server-calculated mana.");
    Assert(AttackConfirmation.TryCreate(input, authoritativeMob, requestedMp, out var confirmation) && confirmation is not null, "Integrated attack confirmation was not created.");
    Assert(world.TryApplyPhysicalAttack(1, 2, randomFactor: 99, out var physicalOutcome) == LegacyPhysicalAttackResult.Accepted && physicalOutcome is not null && physicalOutcome.Damage == 21, "Integrated attack did not resolve the legacy physical damage.");
    Assert(confirmation!.TrySetDamageSlot(0, (ushort)physicalOutcome!.TargetConnectionId, physicalOutcome.Damage), "Integrated attack damage slot was not filled from authoritative combat state.");

    var response = codec.Decode(confirmation.ToFrame(codec, input.Header.ClientTick, 17));
    Assert(BinaryPrimitives.ReadInt32LittleEndian(response.Payload.Span[4..]) == 900, "Integrated attack relay exposed non-authoritative attacker HP.");
    Assert(BinaryPrimitives.ReadInt32LittleEndian(response.Payload.Span[40..]) == 79 && BinaryPrimitives.ReadInt16LittleEndian(response.Payload.Span[46..]) == 79, "Integrated attack relay exposed client or stale mana.");
    Assert(BinaryPrimitives.ReadUInt16LittleEndian(response.Payload.Span[48..]) == 2 && BinaryPrimitives.ReadInt32LittleEndian(response.Payload.Span[52..]) == 21, "Integrated attack relay exposed client-declared target damage.");
    Assert(world.TryGetCombatState(2, out var targetState) && targetState!.CurrentScore.Hp == 79, "Integrated attack did not persist target HP after the relay was built.");
    Assert(world.TryGetResourceState(2, out var targetResources, out var targetRequestedHp, out _) && targetResources!.CurrentScore.Hp == 79 && targetRequestedHp == 79, "Integrated attack did not synchronize the target ReqHp state used by SendSetHpMp.");
}

static void SetHpMpWire()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(new SetHpMpConfirmation(700, 79, 700, 79).ToFrame(codec, 123456, 17, 4));
    Assert(frame.IsChecksumValid && frame.Header.Type == SetHpMpConfirmation.MessageType && frame.Header.Size == SetHpMpConfirmation.PacketSize && frame.Header.Id == 4, "SetHpMp response header differs from the legacy wire.");
    Assert(BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span) == 700 && BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[4..]) == 79 && BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[8..]) == 700 && BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[12..]) == 79, "SetHpMp response fields differ from the legacy layout.");
}

static void SetHpModeWire()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(new SetHpModeConfirmation(0, (short)LoginSessionState.Playing).ToFrame(codec, 123456, 17, 4));
    Assert(frame.IsChecksumValid && frame.Header.Type == SetHpModeConfirmation.MessageType && frame.Header.Size == SetHpModeConfirmation.PacketSize && frame.Header.Id == 4, "SetHpMode response header differs from the legacy wire.");
    Assert(BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span) == 0 && BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[4..]) == (short)LoginSessionState.Playing && frame.Payload.Span[6..].SequenceEqual(new byte[2]), "SetHpMode response fields or ABI padding differ from MSG_SetHpMode.");
}

static void SendItemWire()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var item = new LegacyItem(3463, 61, 2, 0, 0, 0, 0);
    var frame = codec.Decode(new SendItemConfirmation(1, 7, item).ToFrame(codec, 123456, 17, 4));
    Assert(frame.IsChecksumValid && frame.Header.Type == SendItemConfirmation.MessageType && frame.Header.Size == SendItemConfirmation.PacketSize && frame.Header.Id == 4, "SendItem response header differs from the legacy wire.");
    Assert(BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span) == 1 && BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[2..]) == 7 && LegacyItem.Read(frame.Payload.Span[4..]) == item, "SendItem response fields differ from MSG_SendItem.");
    var equipmentFrame = codec.Decode(new SendItemConfirmation(0, 14, item).ToFrame(codec, 123456, 17, 4));
    Assert(BinaryPrimitives.ReadInt16LittleEndian(equipmentFrame.Payload.Span) == 0 && BinaryPrimitives.ReadInt16LittleEndian(equipmentFrame.Payload.Span[2..]) == 14, "Equipment SendItem response did not preserve the legacy type and slot used by LinkMountHp.");
}

static void PhysicalCombatFormula()
{
    Assert(LegacySkillCombatMath.GetPhysicalDamage(100, 60, combat: 0, randomFactor: 99) == 69, "BASE_GetDamage integer formula differs for a deterministic physical hit.");
    Assert(LegacySkillCombatMath.GetPhysicalDamage(0, 60, combat: 0, randomFactor: 110) == 2, "BASE_GetDamage negative-damage adjustment differs from the legacy formula.");
}

static void SkillCombatFormula()
{
    Assert(LegacySkillCombatMath.GetSkillDamage(100, 60, combat: 0, randomFactor: 90) == 63, "BASE_GetSkillDamage integer formula differs for a deterministic skill hit.");
    Assert(LegacySkillCombatMath.GetSkillDamage(20, 60, combat: 0, randomFactor: 90) == 4, "BASE_GetSkillDamage negative-damage adjustment differs from the legacy formula.");
    Assert(LegacySkillCombatMath.GetSkillDamage(100, 0, combat: 20, randomFactor: 105) == 105, "BASE_GetSkillDamage did not clamp combat to fifteen before selecting its random range.");
    Assert(LegacySkillCombatMath.GetParryRate(1500, 0, 0, 0) == 625, "GetParryRate did not preserve the raw Dexterity contribution above the 1000 target-dex cap.");
    Assert(LegacySkillCombatMath.GetParryRate(100, 0, 0, 0x2A0) == 250, "GetParryRate did not apply the three legacy attacker-Rsv bonuses.");

    var skill = new LegacySkillDefinition(10, 69, 1, 20, 15, 1, 1, 40, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 2, 0, 0, 1, 0, "Golpe_Mortal");
    var physicalClassScore = new LegacyScore(50, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 100, 300, 0, 0, 0, 20, 0, 0);
    var physicalClass = new LegacyMobCombatState(0, 0, 0, physicalClassScore, 0);
    Assert(LegacySkillCombatMath.GetSkillBaseDamage(skill, physicalClass, weather: 0, weaponDamage: 30, magic: 0) == 625, "BASE_GetSkillDamage skill-base formula differs for a TK weapon skill.");

    var magicClassScore = physicalClassScore with { Intelligence = 300, Strength = 0 };
    var magicClass = new LegacyMobCombatState(1, 0, 50, magicClassScore, 0);
    Assert(LegacySkillCombatMath.GetSkillBaseDamage(skill, magicClass, weather: 0, weaponDamage: 0, magic: 50) == 900, "BASE_GetSkillDamage skill-base formula differs for a Foema magic skill.");

    var lightning = skill with { Id = 79, InstanceType = 4, InstanceValue = 999 };
    var lightningScore = physicalClassScore with { Damage = 200 };
    Assert(LegacySkillCombatMath.GetSkillBaseDamage(lightning, new LegacyMobCombatState(3, 0, 0, lightningScore, 0), weather: 0, weaponDamage: 0, magic: 0) == 360, "Skill 79 did not use the legacy lightning override.");
}

static void ExperienceFormula()
{
    Assert(LegacyExperienceMath.GetExpApply(new LegacyExperienceEligibility(LegacyAccountSnapshot.ClassMasterMortal), 1000, 49, 49) == 1000, "GetExpApply did not preserve equal-level mortal experience.");
    Assert(LegacyExperienceMath.GetExpApply(new LegacyExperienceEligibility(LegacyAccountSnapshot.ClassMasterMortal), 1000, 100, 50) == 0, "GetExpApply did not apply the legacy low-target-level reduction.");
    Assert(LegacyExperienceMath.GetExpApply(new LegacyExperienceEligibility(LegacyAccountSnapshot.ClassMasterMortal), 1000, 0, 399) == 2000, "GetExpApply did not clamp the high target multiplier to 200 percent.");
    Assert(LegacyExperienceMath.GetExpApply(new LegacyExperienceEligibility(LegacyAccountSnapshot.ClassMasterArch), 1000, 354, 354) == 0, "GetExpApply did not block an ARCH character before the level-355 quest.");
    Assert(LegacyExperienceMath.GetExpApply(new LegacyExperienceEligibility(LegacyAccountSnapshot.ClassMasterArch, ArchLevel355Completed: true), 1000, 354, 354) == 500, "GetExpApply did not apply the ARCH 50 percent experience rule.");
    Assert(LegacyExperienceMath.GetExpApply(new LegacyExperienceEligibility(LegacyExperienceMath.ClassMasterCelestial, CelestialLevel40Completed: true, CelestialLevel90Completed: true), 1000, 100, 399) == 1000, "GetExpApply did not normalize advanced-class level to MAX_LEVEL.");
    Assert(LegacyExperienceMath.GetExpApply(new LegacyExperienceEligibility(LegacyAccountSnapshot.ClassMasterMortal), 1000, 50, 401) == 1000, "GetExpApply changed experience outside the legacy target-level range.");
}

static void WeaponDamageFormula()
{
    var mastery = new LegacyMobCombatState(0, 1u << 9, 0, default, 0);
    Assert(LegacySkillCombatMath.GetWeaponDamage(mastery, 100, 40, 64, 192, 10, 9) == 220, "Weapon mastery or ancestral weapon bonuses differ from the legacy WeaponDamage formula.");

    var ordinary = new LegacyMobCombatState(1, 0, 0, default, 0);
    Assert(LegacySkillCombatMath.GetWeaponDamage(ordinary, 40, 100, 0, 0, 0, 0) == 120, "The legacy half-damage contribution from the weaker weapon was not preserved.");
}

static void ItemDataTable()
{
    var table = CreateCombatItemDataTable();
    Assert(table.Count == LegacyItemDataTable.MaxItemIndex && table[1] is
        { Name: "FIRST", Unique: 45, Position: 64, MeshIndex: 321, TextureIndex: 654, VisualEffectIndex: 987, Price: 123456, Unknown1: 17, Unknown2: 0x12345678, ItemType: 3, ItemData: 4, Unknown3: 5, Unknown4: 6, UnknownNewValue0: 11, UnknownNewValue1: 22, UnknownNewValue2: 33, UnknownNewValue3: 44 },
        "7.69 ItemList.bin field layout was not decoded.");
    Assert(table[100] is { Position: 0x12345678 }, "The 32-bit 7.69 item position was truncated to the older 16-bit field.");

    var encodedBodyOnly = new byte[LegacyItemDataTable.BodySizeInBytes];
    Array.Fill(encodedBodyOnly, (byte)0x5A);
    Assert(LegacyItemDataTable.Load(encodedBodyOnly).Count == LegacyItemDataTable.MaxItemIndex, "BASE_ReadItemList-compatible body without the ignored trailer was rejected.");
    var invalidLengthRejected = false;
    try
    {
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes + 1]);
    }
    catch (InvalidDataException)
    {
        invalidLengthRejected = true;
    }
    Assert(invalidLengthRejected, "An ItemList.bin with an unexpected partial trailer was accepted.");

    var first = new LegacyItem(1, 116, 230, 0, 0, 0, 0);
    Assert(table.GetItemSanctuary(first) == 10 && table.GetItemAbility(first, LegacyItemEffect.Damage) == 200 && table.GetItemAbility(first, LegacyItemEffect.Magic) == 60, "Static item effects or sanctuary scaling differ from BASE_GetItemAbility.");

    var alternate = new LegacyItem(3, 43, 230, LegacyItemEffect.Damage2, 4, 0, 0);
    Assert(table.GetItemAbility(alternate, LegacyItemEffect.Damage) == 22 && table.GetItemPosition(alternate) == 32, "Position-specific EF_DAMAGE2 selection differs from the legacy item resolver.");

    var state = new LegacyMobCombatState(0, 1u << 9, 0, default, 0);
    var second = new LegacyItem(2, 43, 9, 0, 0, 0, 0);
    Assert(LegacySkillCombatMath.GetWeaponDamage(state, table, first, second) == 356, "WeaponDamage did not consume decoded item abilities and sanctuary values.");
}

static void UpdateCarryWire()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var carry = Enumerable.Range(0, UpdateCarryConfirmation.CarryCount)
        .Select(index => new LegacyItem((short)(700 + index), 61, (byte)index, 0, 0, 0, 0))
        .ToArray();
    var frame = codec.Decode(new UpdateCarryConfirmation(carry, 1_234_567).ToFrame(codec, 123456, 17, 4));

    Assert(frame.IsChecksumValid && frame.Header.Type == UpdateCarryConfirmation.MessageType && frame.Header.Size == UpdateCarryConfirmation.PacketSize && frame.Header.Id == 4,
        "MSG_UpdateCarry response header differs from the legacy wire.");
    Assert(frame.Payload.Length == UpdateCarryConfirmation.PayloadSize &&
        LegacyItem.Read(frame.Payload.Span[UpdateCarryConfirmation.CarryOffset..]) == carry[0] &&
        LegacyItem.Read(frame.Payload.Span[(63 * LegacyItem.SizeInBytes)..]) == carry[63] &&
        BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[UpdateCarryConfirmation.CoinOffset..]) == 1_234_567,
        "MSG_UpdateCarry did not preserve all 64 carry slots or Coin at the measured offsets.");
}

static void ServerItemDataTable()
{
    var decodedBody = new byte[LegacyServerItemDataTable.BodySizeInBytes];
    var record = decodedBody.AsSpan(LegacyServerItemDataTable.RecordSizeInBytes);
    System.Text.Encoding.ASCII.GetBytes("SERVER ITEM").CopyTo(record);
    BinaryPrimitives.WriteInt16LittleEndian(record[64..], 321);
    BinaryPrimitives.WriteInt16LittleEndian(record[66..], 654);
    BinaryPrimitives.WriteInt16LittleEndian(record[68..], 987);
    BinaryPrimitives.WriteInt16LittleEndian(record[70..], 40);
    BinaryPrimitives.WriteInt16LittleEndian(record[72..], 41);
    BinaryPrimitives.WriteInt16LittleEndian(record[74..], 42);
    BinaryPrimitives.WriteInt16LittleEndian(record[76..], 43);
    BinaryPrimitives.WriteInt16LittleEndian(record[78..], 44);
    BinaryPrimitives.WriteInt16LittleEndian(record[80..], LegacyItemEffect.Damage);
    BinaryPrimitives.WriteInt16LittleEndian(record[82..], 100);
    BinaryPrimitives.WriteInt16LittleEndian(record[84..], LegacyItemEffect.Magic);
    BinaryPrimitives.WriteInt16LittleEndian(record[86..], 30);
    BinaryPrimitives.WriteInt32LittleEndian(record[128..], 123456);
    BinaryPrimitives.WriteInt16LittleEndian(record[132..], 45);
    BinaryPrimitives.WriteInt16LittleEndian(record[134..], 192);
    BinaryPrimitives.WriteInt16LittleEndian(record[136..], 17);
    BinaryPrimitives.WriteInt16LittleEndian(record[138..], 2);

    var encoded = new byte[LegacyServerItemDataTable.FileSizeInBytes];
    for (var offset = 0; offset < decodedBody.Length; offset++)
        encoded[offset] = (byte)(decodedBody[offset] ^ 0x5A);
    BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(LegacyServerItemDataTable.BodySizeInBytes), 0x12345678);

    var table = LegacyServerItemDataTable.Load(encoded);
    Assert(table.Count == LegacyServerItemDataTable.MaxItemIndex && table[1] is
        { Name: "SERVER ITEM", MeshIndex: 321, TextureIndex: 654, VisualEffectIndex: 987,
          RequiredLevel: 40, RequiredStrength: 41, RequiredIntelligence: 42,
          RequiredDexterity: 43, RequiredConstitution: 44, Price: 123456,
          Unique: 45, Position: 192, Extra: 17, Grade: 2 },
        "The 140-byte server ItemList layout was not decoded.");
    Assert(table[1]!.StaticEffects[0] == new LegacyItemStaticEffect(LegacyItemEffect.Damage, 100) &&
        table[1]!.StaticEffects[1] == new LegacyItemStaticEffect(LegacyItemEffect.Magic, 30),
        "The server ItemList static effects were not decoded.");
    Assert(LegacyServerItemDataTable.Load(decodedBody.Select(static value => (byte)(value ^ 0x5A)).ToArray()).Count == LegacyServerItemDataTable.MaxItemIndex,
        "The server ItemList body without its ignored trailer was rejected.");

    var invalidLengthRejected = false;
    try
    {
        LegacyServerItemDataTable.Load(new byte[LegacyServerItemDataTable.BodySizeInBytes + 1]);
    }
    catch (InvalidDataException)
    {
        invalidLengthRejected = true;
    }
    Assert(invalidLengthRejected, "A server ItemList with a partial trailer was accepted.");
}

static void LegacyEquipmentEligibilityRules()
{
    var body = new byte[LegacyItemDataTable.BodySizeInBytes];
    WriteItemListRecord(body, 20, "MORTAL ARMOR", unique: 0, position: 4, grade: 0, (LegacyItemEffect.Class, 1));
    WriteItemListRequirements(body, 20, level: 20, strength: 10, intelligence: 11, dexterity: 12, constitution: 13);
    WriteItemListRecord(body, 21, "WRONG CLASS", unique: 0, position: 4, grade: 0, (LegacyItemEffect.Class, 2));
    WriteItemListRecord(body, 22, "ARCH ARMOR", unique: 0, position: 4, grade: 0, (LegacyItemEffect.Class, 1), (LegacyItemEffect.MobType, 1));
    WriteItemListRecord(body, 23, "MORTAL ARMOR", unique: 0, position: 4, grade: 0, (LegacyItemEffect.Class, 1), (LegacyItemEffect.MobType, 2));
    WriteItemListRecord(body, 24, "CELESTIAL ARMOR", unique: 0, position: 4, grade: 0, (LegacyItemEffect.Class, 1), (LegacyItemEffect.MobType, 3));
    WriteItemListRecord(body, 25, "ADVANCED HELMET", unique: 0, position: 2, grade: 0, (LegacyItemEffect.Class, 4));
    WriteItemListRecord(body, 747, "SPECIAL HELMET", unique: 0, position: 2, grade: 0, (LegacyItemEffect.Class, 4));
    WriteItemListRecord(body, 3500, "ADVANCED HELMET SET", unique: 0, position: 2, grade: 0, (LegacyItemEffect.Class, 4));
    WriteItemListRecord(body, 26, "CLASS EXCEPTION WEAPON", unique: 0, position: 64, grade: 0, (LegacyItemEffect.Class, 1));
    WriteItemListRecord(body, 32, "WRONG CLASS ARMOR", unique: 0, position: 4, grade: 0, (LegacyItemEffect.Class, 1));
    WriteItemListRecord(body, 27, "SECONDARY WEAPON", unique: 0, position: 128, grade: 0, (LegacyItemEffect.Class, 1), (LegacyItemEffect.WeaponType, 12));
    WriteItemListRequirements(body, 27, level: 20, strength: 20);
    WriteItemListRecord(body, 28, "ONE HAND WEAPON", unique: 0, position: 64, grade: 0, (LegacyItemEffect.Class, 1));
    WriteItemListRecord(body, 29, "OTHER ONE HAND WEAPON", unique: 0, position: 64, grade: 0, (LegacyItemEffect.Class, 1));
    WriteItemListRecord(body, 30, "TWO HAND WEAPON", unique: 46, position: 64, grade: 0, (LegacyItemEffect.Class, 1));
    WriteItemListRecord(body, 31, "SHIELD", unique: 0, position: 128, grade: 0, (LegacyItemEffect.Class, 1));

    var table = LoadLegacyItemDataTable(body);
    var score = default(LegacyScore) with { Level = 30, Strength = 30, Intelligence = 30, Dexterity = 30, Constitution = 30 };
    var equipment = new LegacyItem[LegacyCharacterSelection.EquipmentCount];
    LegacyEquipmentCheckResult Check(short itemIndex, int position, int characterClass = 0, int classMaster = LegacyAccountSnapshot.ClassMasterMortal, int mortalFace = 0) =>
        LegacyEquipmentRules.Evaluate(new LegacyItem(itemIndex, 0, 0, 0, 0, 0, 0), score, position, characterClass, classMaster, mortalFace, equipment, table);

    Assert(table.GetItemAbility(new LegacyItem(20, 0, 0, 0, 0, 0, 0), LegacyItemEffect.Level) == 20 &&
        table.GetItemAbility(new LegacyItem(20, 0, 0, 0, 0, 0, 0), LegacyItemEffect.RequiredStrength) == 10 &&
        table.GetItemAbility(new LegacyItem(20, 0, 0, 0, 0, 0, 0), LegacyItemEffect.RequiredIntelligence) == 11 &&
        table.GetItemAbility(new LegacyItem(20, 0, 0, 0, 0, 0, 0), LegacyItemEffect.RequiredDexterity) == 12 &&
        table.GetItemAbility(new LegacyItem(20, 0, 0, 0, 0, 0, 0), LegacyItemEffect.RequiredConstitution) == 13,
        "The ItemList requirement columns were not decoded as BASE_GetItemAbility expects.");
    Assert(Check(20, 2).Allowed, "A mortal meeting all five requirements could not equip the item.");
    Assert(Check(20, 1).Failure == LegacyEquipmentCheckFailure.PositionNotAllowed, "An item was allowed in a position absent from EF_POS.");
    Assert(Check(21, 2).Failure == LegacyEquipmentCheckFailure.CharacterClassNotAllowed, "A mortal equipped an item restricted to a different class.");
    Assert(Check(20, 2, classMaster: LegacyAccountSnapshot.ClassMasterMortal, mortalFace: 0) is { Allowed: true }, "The mortal class setup changed unexpectedly.");
    Assert(Check(22, 2).Failure == LegacyEquipmentCheckFailure.ItemClassMasterMismatch, "A mortal equipped an ARCH-only item.");
    Assert(Check(23, 2, classMaster: LegacyAccountSnapshot.ClassMasterArch, mortalFace: 0).Failure == LegacyEquipmentCheckFailure.ItemClassMasterMismatch, "An ARCH equipped a MORTAL-only item.");
    Assert(Check(24, 2, classMaster: LegacyExperienceMath.ClassMasterCelestial, mortalFace: 0).Allowed, "A CELESTIAL was rejected by the legacy item-class-master rules.");
    Assert(Check(24, 2, classMaster: LegacyAccountSnapshot.ClassMasterArch, mortalFace: 0).Failure == LegacyEquipmentCheckFailure.ItemClassMasterMismatch, "An ARCH equipped a CELESTIAL-only item.");
    Assert(Check(25, 1, classMaster: LegacyAccountSnapshot.ClassMasterArch, mortalFace: 20).Failure == LegacyEquipmentCheckFailure.AdvancedHelmetRestriction, "An advanced character equipped a non-whitelisted helmet.");
    Assert(Check(747, 1, classMaster: LegacyAccountSnapshot.ClassMasterArch, mortalFace: 20).Allowed, "The advanced-class helmet exception for item 747 was lost.");
    Assert(Check(3500, 1, classMaster: LegacyAccountSnapshot.ClassMasterArch, mortalFace: 20).Allowed, "The advanced-class helmet exception for items 3500-3507 was lost.");
    Assert(Check(26, 6, classMaster: LegacyAccountSnapshot.ClassMasterArch, mortalFace: 20).Allowed, "The advanced-class weapon-slot exception was lost.");
    Assert(Check(32, 2, classMaster: LegacyAccountSnapshot.ClassMasterArch, mortalFace: 20).Failure == LegacyEquipmentCheckFailure.CharacterClassNotAllowed, "The advanced-class exception leaked outside weapon slots.");
    Assert(Check(20, 15).Failure == LegacyEquipmentCheckFailure.InvalidPosition && Check(20, -2).Failure == LegacyEquipmentCheckFailure.InvalidPosition && Check(0, 2).Failure == LegacyEquipmentCheckFailure.InvalidItem && Check(20, 2, characterClass: 32).Failure == LegacyEquipmentCheckFailure.InvalidCharacterClass, "An invalid item, class or equipment slot was accepted.");

    var lowLevelScore = score with { Level = 19 };
    Assert(LegacyEquipmentRules.Evaluate(new LegacyItem(20, 0, 0, 0, 0, 0, 0), lowLevelScore, 2, 0, LegacyAccountSnapshot.ClassMasterMortal, 0, equipment, table).Failure == LegacyEquipmentCheckFailure.LevelRequirementNotMet, "A character below the required level equipped the item.");
    var statRequirementCases = new (LegacyScore Score, LegacyEquipmentCheckFailure Failure)[]
    {
        (score with { Strength = 9 }, LegacyEquipmentCheckFailure.StrengthRequirementNotMet),
        (score with { Intelligence = 10 }, LegacyEquipmentCheckFailure.IntelligenceRequirementNotMet),
        (score with { Dexterity = 11 }, LegacyEquipmentCheckFailure.DexterityRequirementNotMet),
        (score with { Constitution = 12 }, LegacyEquipmentCheckFailure.ConstitutionRequirementNotMet),
    };
    foreach (var (belowRequirement, expectedFailure) in statRequirementCases)
    {
        var result = LegacyEquipmentRules.Evaluate(new LegacyItem(20, 0, 0, 0, 0, 0, 0), belowRequirement, 2, 0, LegacyAccountSnapshot.ClassMasterMortal, 0, equipment, table);
        Assert(result.Failure == expectedFailure, $"The {expectedFailure} item requirement was not enforced.");
    }
    Assert(Check(20, -1).Allowed, "The legacy position=-1 validation mode did not bypass EF_POS.");

    equipment[7] = new LegacyItem(29, 0, 0, 0, 0, 0, 0);
    Assert(Check(28, 6).Failure == LegacyEquipmentCheckFailure.TwoHandedWeaponConflict, "Two ordinary one-handed weapons bypassed the legacy two-hand conflict.");
    equipment[7] = new LegacyItem(31, 0, 0, 0, 0, 0, 0);
    Assert(Check(30, 6).Allowed, "A two-handed weapon could not use its required complementary offhand position.");
    equipment[6] = new LegacyItem(30, 0, 0, 0, 0, 0, 0);
    equipment[7] = default;
    var belowScaledRequirement = score with { Level = 25, Strength = 25 };
    Assert(LegacyEquipmentRules.Evaluate(new LegacyItem(27, 0, 0, 0, 0, 0, 0), belowScaledRequirement, 7, 0, LegacyAccountSnapshot.ClassMasterMortal, 0, equipment, table).Failure == LegacyEquipmentCheckFailure.LevelRequirementNotMet, "The legacy 130-percent secondary-weapon level requirement was not applied.");
    var enoughForSecondary = score with { Level = 26, Strength = 26 };
    Assert(LegacyEquipmentRules.Evaluate(new LegacyItem(27, 0, 0, 0, 0, 0, 0), enoughForSecondary, 7, 0, LegacyAccountSnapshot.ClassMasterMortal, 0, equipment, table).Allowed, "A character meeting the scaled secondary-weapon requirements was rejected.");
}

static void LegacyMobAbilityAggregation()
{
    var body = new byte[LegacyItemDataTable.BodySizeInBytes];
    WriteItemListRecord(body, 1, "LEFT WEAPON", unique: 45, position: 64, grade: 0, (LegacyItemEffect.Damage, 100), (LegacyItemEffect.Magic, 30));
    WriteItemListRecord(body, 2, "RIGHT WEAPON", unique: 48, position: 192, grade: 0, (LegacyItemEffect.Damage, 40));
    WriteItemListRecord(body, 3, "MATCHING WEAPON", unique: 45, position: 192, grade: 0, (LegacyItemEffect.Damage, 40));
    WriteItemListRecord(body, 4, "ATTACK SPEED", unique: 0, position: 0, grade: 0, (LegacyItemEffect.AttackSpeed, 1));
    for (var itemIndex = 10; itemIndex < 15; itemIndex++)
        WriteItemListRecord(body, itemIndex, $"SET {itemIndex}", unique: 55, position: 0, grade: 0, (LegacyItemEffect.Ac, 100));
    WriteItemListRecord(body, 15, "MIXED SET", unique: 56, position: 0, grade: 0, (LegacyItemEffect.Ac, 100));
    WriteItemListRecord(body, 20, "LOW RANGE", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Range, 1));
    WriteItemListRecord(body, 21, "HIGH RANGE", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Range, 7));

    var table = LoadLegacyItemDataTable(body);
    var equipment = new LegacyItem[LegacyCharacterSelection.EquipmentCount];
    equipment[6] = new LegacyItem(1, 0, 0, 0, 0, 0, 0);
    equipment[7] = new LegacyItem(2, 0, 0, 0, 0, 0, 0);
    Assert(LegacyMobAbilityMath.GetAbility(equipment, 0, 0, LegacyItemEffect.Damage, table) == 112, "Different weapon archetypes did not use the legacy 30-percent secondary contribution.");
    equipment[7] = new LegacyItem(3, 0, 0, 0, 0, 0, 0);
    Assert(LegacyMobAbilityMath.GetAbility(equipment, 0, 0, LegacyItemEffect.Damage, table) == 120, "Matching weapon archetypes did not use the legacy 50-percent secondary contribution.");
    Assert(LegacyMobAbilityMath.GetAbility(equipment, 0, 1u << 9, LegacyItemEffect.Damage, table) == 140, "Arms Weapon Mastery did not enable the full secondary weapon contribution.");
    Assert(LegacyMobAbilityMath.GetAbility(equipment, 3, 1u << 10, LegacyItemEffect.Damage, table) == 140, "Hunter Weapon Mastery did not enable the full secondary weapon contribution.");
    Assert(LegacyMobAbilityMath.GetAbility(equipment, 0, 0, LegacyItemEffect.Magic, table) == 30, "The secondary weapon's magic ability was included despite the legacy slot exclusion.");

    Array.Clear(equipment);
    equipment[1] = new LegacyItem(4, 0, 0, 0, 0, 0, 0);
    Assert(LegacyMobAbilityMath.GetAbility(equipment, 0, 0, LegacyItemEffect.AttackSpeed, table) == 10, "The legacy attack-speed value of one was not converted to ten.");

    Array.Clear(equipment);
    equipment[1] = new LegacyItem(20, 0, 0, 0, 0, 0, 0);
    equipment[2] = new LegacyItem(21, 0, 0, 0, 0, 0, 0);
    Assert(LegacyMobAbilityMath.GetAbility(equipment, 0, 0, LegacyItemEffect.Range, table) == 7, "Range did not select the maximum equipped ability.");
    equipment[2] = default;
    equipment[0] = new LegacyItem(30, 0, 0, 0, 0, 0, 0);
    Assert(LegacyMobAbilityMath.GetAbility(equipment, 0, 1u << 20, LegacyItemEffect.Range, table) == 2, "The class-three skill range floor was not applied.");
    Assert(LegacyMobAbilityMath.GetAbility(equipment, 0, 0, LegacyItemEffect.Range, table) == 1, "The class-three range floor applied without its learned skill.");

    Array.Clear(equipment);
    for (var slot = 1; slot <= 5; slot++)
        equipment[slot] = new LegacyItem((short)(9 + slot), 0, 0, 0, 0, 0, 0);
    Assert(LegacyMobAbilityMath.GetAbility(equipment, 0, 0, LegacyItemEffect.Ac, table) == 525, "A matching five-piece unique set did not receive the legacy five-percent AC bonus.");
    equipment[5] = new LegacyItem(15, 0, 0, 0, 0, 0, 0);
    Assert(LegacyMobAbilityMath.GetAbility(equipment, 0, 0, LegacyItemEffect.Ac, table) == 500, "The AC set bonus applied when one equipped unique differed.");
}

static void LegacyMobAbilityIncludesClientExtensionSlots()
{
    var body = new byte[LegacyItemDataTable.BodySizeInBytes];
    WriteItemListRecord(body, 16, "CLIENT EXTENSION ITEM", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Magic, 9));
    WriteItemListRecord(body, 17, "CLIENT EXTENSION ITEM 2", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Magic, 4));
    var table = LoadLegacyItemDataTable(body);

    var legacyEquipment = new LegacyItem[LegacyCharacterSelection.EquipmentCount];
    var clientEquipment = new LegacyItem[CharacterMobV769.EquipmentCount];
    clientEquipment[16] = new LegacyItem(16, 0, 0, 0, 0, 0, 0);
    clientEquipment[17] = new LegacyItem(17, 0, 0, 0, 0, 0, 0);

    Assert(LegacyMobAbilityMath.GetAbility(legacyEquipment, 0, 0, LegacyItemEffect.Magic, table) == 0,
        "The legacy 16-slot view unexpectedly read beyond its source layout.");
    Assert(LegacyMobAbilityMath.GetAbility(clientEquipment, 0, 0, LegacyItemEffect.Magic, table) == 13,
        "The 7.69 18-slot view did not include NewSlot1/NewSlot2 in equipment ability aggregation.");
}

static void WorldClient18SlotEquipmentState()
{
    var mob = new byte[LegacyAccountSnapshot.CharacterStride];
    mob[LegacyAccountSnapshot.MobNameOffset] = (byte)'C';
    BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), 100);
    var equipment = new LegacyItem[CharacterMobV769.EquipmentCount];
    equipment[16] = new LegacyItem(700, 0, 0, 0, 0, 0, 0);
    equipment[17] = new LegacyItem(701, 0, 0, 0, 0, 0, 0);

    var hub = new WorldHub();
    Assert(hub.Enter(1, "CLIENT18", (_, _) => ValueTask.CompletedTask), "The 7.69 equipment participant could not enter the world.");
    Assert(hub.SetCharacterState(1, 0, 0, 0, 0, 0, 2000, 2000, mob, LegacyAccountSnapshot.ClassMasterMortal, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, equipment),
        "The WorldHub did not accept the independent 18-slot state.");

    Assert(hub.TryBuildUpdateEquipFrame(1, LegacyFrameCodec.CreateDefault(), 1, 0, out var updateFrame) && updateFrame is not null,
        "The 7.69 UpdateEquip frame was not produced from the 18-slot session state.");
    var update = LegacyFrameCodec.CreateDefault().Decode(updateFrame!);
    Assert(BinaryPrimitives.ReadUInt16LittleEndian(update.Payload.Span.Slice(16 * sizeof(ushort))) == 700 &&
        BinaryPrimitives.ReadUInt16LittleEndian(update.Payload.Span.Slice(17 * sizeof(ushort))) == 701,
        "The client-only equipment slots were lost in the 18-entry appearance wire.");

    Assert(hub.TryTradeItems(1, LegacyItemPlace.Equip, 16, LegacyItemPlace.Carry, 0, out var moved) == LegacyTradingItemResult.Accepted && moved is not null,
        "The runtime rejected a valid move out of client slot 16.");
    Assert(hub.TryGetCharacterSnapshot(1, out _, out _, out _, out _, out _, out var snapshotEquipment) && snapshotEquipment is not null &&
        snapshotEquipment.Count == CharacterMobV769.EquipmentCount && snapshotEquipment[16].Index == 0 && snapshotEquipment[17].Index == 701,
        "The 18-slot runtime mutation was not preserved in the authoritative snapshot.");
}

static void LegacyCurrentScoreBaseEquipmentStage()
{
    var body = new byte[LegacyItemDataTable.BodySizeInBytes];
    WriteItemListRecord(body, 1, "SCORE TEST ITEM", unique: 0, position: 0, grade: 0,
        (LegacyItemEffect.Ac, 10), (LegacyItemEffect.AcAdd, 5), (LegacyItemEffect.Damage, 20),
        (LegacyItemEffect.Hp, 30), (LegacyItemEffect.Mp, 40), (LegacyItemEffect.Strength, 2),
        (LegacyItemEffect.Intelligence, 3), (LegacyItemEffect.Dexterity, 4), (LegacyItemEffect.Constitution, 5));
    var table = LoadLegacyItemDataTable(body);
    var equipment = new LegacyItem[LegacyCharacterSelection.EquipmentCount];
    equipment[1] = new LegacyItem(1, 0, 0, 0, 0, 0, 0);

    var baseScore = default(LegacyScore) with
    {
        Level = 50,
        Ac = 50,
        Damage = 60,
        MaxHp = 1000,
        MaxMp = 500,
        Hp = 900,
        Mp = 450,
        Strength = 10,
        Intelligence = 20,
        Dexterity = 30,
        Constitution = 40,
        Special1 = 6,
    };
    var staleCurrentScore = baseScore with
    {
        Ac = 999,
        Damage = 999,
        MaxHp = 2000,
        MaxMp = 900,
        Hp = 321,
        Mp = 123,
        Strength = 99,
        Intelligence = 99,
        Dexterity = 99,
        Constitution = 99,
        Special1 = 99,
    };
    var mob = new LegacyMobCombatState(0, 0, 0, staleCurrentScore, 0)
    {
        BaseScore = baseScore,
        Rsv = 77,
    };

    var rebuilt = LegacyCurrentScoreMath.RebuildBaseAndEquipmentScore(mob, equipment, table);
    Assert(rebuilt.Rsv == 0, "The initial BASE_GetCurrentScore stage did not clear MOB.Rsv.");
    Assert(rebuilt.CurrentScore.Level == 50 && rebuilt.CurrentScore.Ac == 65 && rebuilt.CurrentScore.Damage == 80, "Base AC or damage was not reset and rebuilt with equipment abilities.");
    Assert(rebuilt.CurrentScore.MaxHp == 1030 && rebuilt.CurrentScore.MaxMp == 540, "Maximum HP/MP were not rebuilt from BaseScore plus equipment.");
    Assert(rebuilt.CurrentScore.Hp == 321 && rebuilt.CurrentScore.Mp == 123, "The score rebuild did not preserve the current HP/MP values.");
    Assert(rebuilt.CurrentScore.Strength == 12 && rebuilt.CurrentScore.Intelligence == 23 && rebuilt.CurrentScore.Dexterity == 34 && rebuilt.CurrentScore.Constitution == 45, "Base attributes were not rebuilt with equipment abilities.");
    Assert(rebuilt.CurrentScore.Special1 == 6 && mob.CurrentScore.Special1 == 99, "The base reset failed to restore untouched score fields or mutated the input state.");
}

static void LegacyCurrentScoreSpecialStage()
{
    var body = new byte[LegacyItemDataTable.BodySizeInBytes];
    WriteItemListRecord(body, 1, "SPECIAL TEST ITEM", unique: 0, position: 0, grade: 0,
        (LegacyItemEffect.Special1, 10), (LegacyItemEffect.Special2, 20), (LegacyItemEffect.Special3, 30),
        (LegacyItemEffect.Special4, 40), (LegacyItemEffect.SpecialAll, 5));
    var table = LoadLegacyItemDataTable(body);
    var equipment = new LegacyItem[LegacyCharacterSelection.EquipmentCount];
    equipment[1] = new LegacyItem(1, 0, 0, 0, 0, 0, 0);
    var baseScore = default(LegacyScore) with { Special1 = 100, Special2 = 100, Special3 = 100, Special4 = 100 };
    var mob = new LegacyMobCombatState(0, 0, 0, baseScore, 0) { BaseScore = baseScore };

    var scored = LegacyCurrentScoreMath.ApplySpecialAbilityStage(mob, equipment, table);
    Assert(scored.CurrentScore.Special1 == 110 && scored.CurrentScore.Special2 == 125 && scored.CurrentScore.Special3 == 135 && scored.CurrentScore.Special4 == 145,
        "The Cur Special stage did not add the four item abilities and shared EF_SPECIALALL bonus correctly.");

    var nearCap = mob with { CurrentScore = baseScore with { Special1 = 250, Special2 = 250, Special3 = 250, Special4 = 250 } };
    var capped = LegacyCurrentScoreMath.ApplySpecialAbilityStage(nearCap, equipment, table);
    Assert(capped.CurrentScore.Special1 == 255 && capped.CurrentScore.Special2 == 255 && capped.CurrentScore.Special3 == 255 && capped.CurrentScore.Special4 == 255,
        "The Cur Special stage did not clamp each special score to 255.");
    Assert(mob.CurrentScore.Special1 == 100 && scored.CurrentScore.Special1 == 110, "The Cur Special stage mutated its input state.");
}

static void LegacyLoginHpMpBaseStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-hpmp.golden.json")
        ?? throw new InvalidOperationException("The 7.69 HP/MP golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);
    var capFixture = document.RootElement.GetProperty("caps");
    var expectedHpCap = capFixture.GetProperty("maxHp").GetInt32();
    var expectedMpCap = capFixture.GetProperty("maxMp").GetInt32();

    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var characterClass = vector.GetProperty("class").GetByte();
        var classMaster = vector.GetProperty("classMaster").GetInt16();
        var level = vector.GetProperty("level").GetInt32();
        var intelligence = vector.GetProperty("intelligence").GetInt16();
        var constitution = vector.GetProperty("constitution").GetInt16();
        var baseScore = default(LegacyScore) with
        {
            MaxHp = 12,
            MaxMp = 34,
            Hp = 56,
            Mp = 78,
            Intelligence = intelligence,
            Constitution = constitution,
        };
        var currentScore = default(LegacyScore) with
        {
            Level = level,
            MaxHp = 90,
            MaxMp = 123,
            Hp = 45,
            Mp = 67,
        };
        var mob = new LegacyMobCombatState(characterClass, 0, 0, currentScore, 0) { BaseScore = baseScore };

        Assert(LegacyHpMpMath.TryApplyBaseHpMp(mob, classMaster, out var updated), "BASE_GetHpMp rejected a valid class.");
        var expectedHp = vector.GetProperty("maxHp").GetInt32();
        var expectedMp = vector.GetProperty("maxMp").GetInt32();
        Assert(updated.BaseScore.MaxHp == expectedHp && updated.CurrentScore.MaxHp == expectedHp,
            $"BASE_GetHpMp HP differs for class {characterClass}, master {classMaster}.");
        Assert(updated.BaseScore.MaxMp == expectedMp && updated.CurrentScore.MaxMp == expectedMp,
            $"BASE_GetHpMp MP differs for class {characterClass}, master {classMaster}.");
        Assert(updated.CurrentScore.Hp == 45 && updated.CurrentScore.Mp == 67 &&
            updated.BaseScore.Hp == 56 && updated.BaseScore.Mp == 78,
            "BASE_GetHpMp changed current HP/MP instead of recalculating only their maxima.");
        Assert(mob.BaseScore == baseScore && mob.CurrentScore.MaxHp == 90 && mob.CurrentScore.MaxMp == 123,
            "BASE_GetHpMp mutated its input state.");
    }

    var cappedBase = default(LegacyScore) with { Intelligence = 20, Constitution = 16 };
    var cappedCurrent = default(LegacyScore) with { Level = 500_000, Hp = 321, Mp = 123 };
    var cappedMob = new LegacyMobCombatState(0, 0, 0, cappedCurrent, 0) { BaseScore = cappedBase };
    Assert(LegacyHpMpMath.TryApplyBaseHpMp(cappedMob, LegacyAccountSnapshot.ClassMasterMortal, out var capped),
        "BASE_GetHpMp rejected a valid class before its upper caps could be checked.");
    Assert(LegacyHpMpMath.MaximumHp == expectedHpCap && capped.BaseScore.MaxHp == expectedHpCap && capped.CurrentScore.MaxHp == expectedHpCap &&
        LegacyHpMpMath.MaximumMp == expectedMpCap && capped.BaseScore.MaxMp == expectedMpCap && capped.CurrentScore.MaxMp == expectedMpCap,
        "BASE_GetHpMp did not apply the selected 7.69 HP/MP upper caps.");
    var invalidMob = cappedMob with { CharacterClass = 4 };
    Assert(!LegacyHpMpMath.TryApplyBaseHpMp(invalidMob, LegacyAccountSnapshot.ClassMasterMortal, out var invalid) && invalid == invalidMob,
        "BASE_GetHpMp must reject an invalid class without changing the state.");
}

static void LegacyLoginEquipmentAbilityStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-ability-stage.golden.json")
        ?? throw new InvalidOperationException("The 7.69 derived-ability golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);
    var body = new byte[LegacyItemDataTable.BodySizeInBytes];
    foreach (var item in document.RootElement.GetProperty("itemData").EnumerateArray())
    {
        var effects = item.GetProperty("effects").EnumerateArray()
            .Select(effect => (effect[0].GetInt32(), effect[1].GetInt32()))
            .ToArray();
        WriteItemListRecord(body, item.GetProperty("index").GetInt32(), item.GetProperty("name").GetString()!,
            item.GetProperty("unique").GetInt16(), item.GetProperty("position").GetInt32(), grade: 0, effects);
    }

    var table = LoadLegacyItemDataTable(body);
    Assert(table.GetItemAbility(new LegacyItem(1, 0, 0, 0, 0, 0, 0), LegacyItemEffect.MagicAdd) == 1
        && table.GetItemAbility(new LegacyItem(2, 0, 0, 68, 9, 0, 0), LegacyItemEffect.MagicAdd) == 0
        && table.GetItemAbility(new LegacyItem(2, 0, 0, 0, 0, 0, 0), LegacyItemEffect.DamageAdd) == 0,
        "MagicAdd/DamageAdd ignored the legacy nUnique 41..50 eligibility gate.");
    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var equipment = new LegacyItem[LegacyCharacterSelection.EquipmentCount];
        foreach (var equipped in vector.GetProperty("equipment").EnumerateArray())
        {
            var dynamicEffects = equipped.GetProperty("dynamicEffects").EnumerateArray().ToArray();
            Assert(dynamicEffects.Length <= 3, "A golden equipment item exceeded the legacy three-option layout.");
            byte Effect(int index) => index < dynamicEffects.Length ? checked((byte)dynamicEffects[index][0].GetInt32()) : (byte)0;
            byte Value(int index) => index < dynamicEffects.Length ? checked((byte)dynamicEffects[index][1].GetInt32()) : (byte)0;
            equipment[equipped.GetProperty("slot").GetInt32()] = new LegacyItem(
                checked((short)equipped.GetProperty("index").GetInt32()),
                Effect(0), Value(0), Effect(1), Value(1), Effect(2), Value(2));
        }

        var baseScore = default(LegacyScore) with { AttackRun = vector.GetProperty("baseAttackRun").GetByte() };
        var currentScore = default(LegacyScore) with { AttackRun = vector.GetProperty("currentAttackRun").GetByte() };
        var mob = new LegacyMobCombatState(0, 0, 0, currentScore, 0) { BaseScore = baseScore };
        var actual = LegacyCurrentScoreMath.CalculateAbilityStage(mob, equipment, table);
        var expected = vector.GetProperty("expected");

        Assert(actual.SaveMana == expected.GetProperty("saveMana").GetInt32()
            && actual.Magic == expected.GetProperty("magic").GetInt32()
            && actual.RunSeed == expected.GetProperty("runSeed").GetInt32()
            && actual.AttackSpeedSeed == expected.GetProperty("attackSpeedSeed").GetInt32()
            && actual.RegenHpSeed == expected.GetProperty("regenHpSeed").GetInt32()
            && actual.RegenMpSeed == expected.GetProperty("regenMpSeed").GetInt32()
            && actual.CriticalSeed == expected.GetProperty("criticalSeed").GetInt32(),
            $"7.69 derived equipment ability stage differed for '{vector.GetProperty("name").GetString()}'.");
        Assert(mob.CurrentScore == currentScore && mob.BaseScore == baseScore,
            "The derived equipment ability stage mutated its input MOB.");
    }
}

static void LegacyLoginFinalScalarClamps()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-final-clamps.golden.json")
        ?? throw new InvalidOperationException("The 7.69 final scalar-clamp golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);

    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var input = vector.GetProperty("input");
        var actual = LegacyCurrentScoreMath.ApplyFinalScalarClamps(
            input.GetProperty("hp").GetInt32(),
            input.GetProperty("mp").GetInt32(),
            input.GetProperty("maxHp").GetInt32(),
            input.GetProperty("maxMp").GetInt32(),
            input.GetProperty("regenHp").GetInt32(),
            input.GetProperty("regenMp").GetInt32(),
            input.GetProperty("magic").GetInt32(),
            input.GetProperty("critical").GetInt32());
        var resistInput = input.GetProperty("resist");
        var actualResist = LegacyCurrentScoreMath.ApplyResistanceClamps(
            resistInput[0].GetInt32(), resistInput[1].GetInt32(), resistInput[2].GetInt32(), resistInput[3].GetInt32());
        var expected = vector.GetProperty("targetExpected");
        var expectedResist = expected.GetProperty("resist");

        Assert(actual.Hp == expected.GetProperty("hp").GetInt32()
            && actual.Mp == expected.GetProperty("mp").GetInt32()
            && actual.RegenHp == expected.GetProperty("regenHp").GetByte()
            && actual.RegenMp == expected.GetProperty("regenMp").GetByte()
            && actual.Magic == expected.GetProperty("magic").GetInt32()
            && actual.Critical == expected.GetProperty("critical").GetInt32()
            && actualResist.Holy == expectedResist[0].GetByte()
            && actualResist.Thunder == expectedResist[1].GetByte()
            && actualResist.Fire == expectedResist[2].GetByte()
            && actualResist.Ice == expectedResist[3].GetByte(),
            $"7.69 final scalar clamps differed for '{vector.GetProperty("name").GetString()}'.");

        if (vector.TryGetProperty("w2ppExpected", out var w2ppExpected))
        {
            var w2ppMagic = Math.Min(input.GetProperty("magic").GetInt32(), 1_000_000_000);
            Assert(w2ppExpected.GetProperty("magic").GetInt32() == w2ppMagic
                && actual.Magic != w2ppMagic,
                "The target/W2PP MAX_DAMAGE_MG divergence was not exercised by this vector.");
        }
    }
}

static void LegacyLoginResistanceAffectStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-resistance-affects.golden.json")
        ?? throw new InvalidOperationException("The 7.69 resistance-affect golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);

    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var input = vector.GetProperty("input");
        var initial = input.GetProperty("resist");
        var affectSnapshot = BuildResistanceAffectSnapshot(input.GetProperty("affects"));
        var originalAffects = affectSnapshot.ToArray();
        var characterClass = input.GetProperty("characterClass").GetInt32();
        var initialHeadIndex = input.GetProperty("headItemIndex").GetInt32();
        var learnedSkills = input.GetProperty("learnedSkills").GetInt32();

        var actual = LegacyCurrentScoreMath.ApplyResistanceAffectStage(
            initial[0].GetInt32(), initial[1].GetInt32(), initial[2].GetInt32(), initial[3].GetInt32(),
            affectSnapshot, characterClass, initialHeadIndex, learnedSkills);
        AssertResistanceModifierMatches(actual, vector.GetProperty("targetExpected"),
            $"7.69 resistance-affect stage differed for '{vector.GetProperty("name").GetString()}'.");
        Assert(affectSnapshot.SequenceEqual(originalAffects), "The resistance-affect stage mutated its affect snapshot.");

        if (vector.TryGetProperty("w2ppExpected", out var w2ppExpected))
        {
            var w2pp = CalculateW2ppResistanceFixture(
                initial[0].GetInt32(), initial[1].GetInt32(), initial[2].GetInt32(), initial[3].GetInt32(),
                affectSnapshot, characterClass, initialHeadIndex, learnedSkills);
            AssertResistanceModifierMatches(w2pp, w2ppExpected,
                $"The W2PP resistance comparison differed for '{vector.GetProperty("name").GetString()}'.");
        }
    }

    var rejectedShortSnapshot = false;
    try
    {
        LegacyCurrentScoreMath.ApplyResistanceAffectStage(0, 0, 0, 0, new byte[8], 2, 0, 0);
    }
    catch (ArgumentException)
    {
        rejectedShortSnapshot = true;
    }
    Assert(rejectedShortSnapshot, "The resistance-affect stage accepted a truncated affect snapshot.");
}

static byte[] BuildResistanceAffectSnapshot(JsonElement affects)
{
    var snapshot = new byte[32 * 8];
    var slot = 0;
    foreach (var affect in affects.EnumerateArray())
    {
        var offset = slot++ * 8;
        snapshot[offset] = affect.GetProperty("type").GetByte();
        snapshot[offset + 1] = affect.GetProperty("value").GetByte();
        BinaryPrimitives.WriteUInt16LittleEndian(snapshot.AsSpan(offset + 2, sizeof(ushort)), affect.GetProperty("level").GetUInt16());
    }
    return snapshot;
}

static LegacyCurrentResistanceModifierStage CalculateW2ppResistanceFixture(
    int holy,
    int thunder,
    int fire,
    int ice,
    ReadOnlySpan<byte> affectSnapshot,
    int characterClass,
    int headItemIndex,
    int learnedSkills)
{
    for (var index = 0; index < 32; index++)
    {
        var offset = index * 8;
        var type = affectSnapshot[offset];
        var value = affectSnapshot[offset + 1];
        var level = BinaryPrimitives.ReadUInt16LittleEndian(affectSnapshot.Slice(offset + 2, sizeof(ushort)));
        if (type == 3)
        {
            var reduction = headItemIndex < 50 ? value / 2 : value - 10;
            holy -= reduction;
            thunder -= reduction;
            fire -= reduction;
            ice -= reduction;
        }
        else if (type == 25)
        {
            var addition = (value + level / 4) / 10;
            if (level >= 255)
                addition += 20;
            thunder += addition;
            fire += addition;
            ice += addition;
        }
        else if (type == 16)
        {
            var transformation = value - 1;
            if (transformation < 0 || transformation >= 5 || characterClass != 2)
                continue;

            headItemIndex = transformation == 4 ? 32 : transformation + 22;
            var addition = headItemIndex switch
            {
                22 when (learnedSkills & 0x20000) != 0 => 0,
                23 when (learnedSkills & 0x80000) != 0 => 40,
                24 when (learnedSkills & 0x200000) != 0 => 15,
                32 => 10,
                _ => 0,
            };
            holy += addition;
            thunder += addition;
            fire += addition;
            ice += addition;
        }
        else if (type == 8 && (level & (1 << 1)) != 0)
        {
            holy += 25;
            thunder += 25;
            fire += 25;
            ice += 25;
        }
    }

    return new LegacyCurrentResistanceModifierStage(holy, thunder, fire, ice, headItemIndex);
}

static void AssertResistanceModifierMatches(
    LegacyCurrentResistanceModifierStage actual,
    JsonElement expected,
    string message)
{
    var resistance = expected.GetProperty("resist");
    Assert(actual.Holy == resistance[0].GetInt32()
        && actual.Thunder == resistance[1].GetInt32()
        && actual.Fire == resistance[2].GetInt32()
        && actual.Ice == resistance[3].GetInt32()
        && actual.HeadItemIndex == expected.GetProperty("headItemIndex").GetInt32(), message);
}

static void LegacyLoginFinalDamageStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-final-damage.golden.json")
        ?? throw new InvalidOperationException("The 7.69 final-damage golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);

    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var input = vector.GetProperty("input");
        var damage = input.GetProperty("damage").GetInt32();
        var strength = input.GetProperty("strength").GetInt32();
        var dexterity = input.GetProperty("dexterity").GetInt32();
        var special1 = input.GetProperty("special1").GetInt32();
        var level = input.GetProperty("level").GetInt32();
        var face = input.GetProperty("face").GetInt32();
        var classMaster = input.GetProperty("classMaster").GetInt32();
        var damageMultiplier = input.GetProperty("damageMultiplier").GetInt32();

        var actual = LegacyCurrentScoreMath.ApplyFinalDamageStage(
            damage, strength, dexterity, special1, level, face, classMaster, damageMultiplier);
        var expectedDamage = vector.GetProperty("targetExpected").GetProperty("damage").GetInt32();
        Assert(actual.Damage == expectedDamage,
            $"7.69 final damage stage differed for '{vector.GetProperty("name").GetString()}'.");

        var w2ppDamage = CalculateW2ppFinalDamageFixture(
            damage, strength, dexterity, special1, level, face, classMaster, damageMultiplier);
        Assert(w2ppDamage == vector.GetProperty("w2ppExpected").GetProperty("damage").GetInt32()
            && actual.Damage == w2ppDamage,
            $"The 7.69/W2PP common final damage block differed for '{vector.GetProperty("name").GetString()}'.");
    }
}

static int CalculateW2ppFinalDamageFixture(
    int damage,
    int strength,
    int dexterity,
    int special1,
    int level,
    int face,
    int classMaster,
    int damageMultiplier)
{
    if (face < 4)
    {
        var levelBonus = classMaster is LegacyAccountSnapshot.ClassMasterArch or LegacyAccountSnapshot.ClassMasterMortal
            ? level
            : unchecked(level + 399);
        damage = unchecked(damage + strength / 2 + dexterity / 3 + special1 + levelBonus);
    }

    if (damageMultiplier != 100)
        damage = unchecked(damage * damageMultiplier) / 100;

    return damage;
}

static void LegacyLoginFinalAttackRunStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-final-speed.golden.json")
        ?? throw new InvalidOperationException("The 7.69 final-speed golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);

    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var input = vector.GetProperty("input");
        var attackSpeedBeforeFinal = input.GetProperty("attackSpeedBeforeFinal").GetInt32();
        var runBeforeFinal = input.GetProperty("runBeforeFinal").GetInt32();
        var attackSpeedBonus = input.GetProperty("attackSpeedBonus").GetInt32();
        var runSpeedBonus = input.GetProperty("runSpeedBonus").GetInt32();
        var dexterity = input.GetProperty("dexterity").GetInt32();
        var face = input.GetProperty("face").GetInt32();
        var eligibleMountRunFloor = input.GetProperty("eligibleMountRunFloor").GetInt32();

        var actual = LegacyCurrentScoreMath.ApplyFinalAttackRunStage(
            attackSpeedBeforeFinal, runBeforeFinal, attackSpeedBonus, runSpeedBonus,
            dexterity, face, eligibleMountRunFloor);
        var target = vector.GetProperty("targetExpected");
        Assert(actual.Attack == target.GetProperty("attack").GetInt32()
            && actual.Run == target.GetProperty("run").GetInt32()
            && actual.PackedAttackRun == target.GetProperty("packedAttackRun").GetByte(),
            $"7.69 final AttackRun stage differed for '{vector.GetProperty("name").GetString()}'.");

        var w2pp = CalculateW2ppFinalAttackRunFixture(
            attackSpeedBeforeFinal, runBeforeFinal, attackSpeedBonus, runSpeedBonus,
            dexterity, face, eligibleMountRunFloor);
        var w2ppExpected = vector.GetProperty("w2ppExpected");
        Assert(w2pp.Attack == w2ppExpected.GetProperty("attack").GetInt32()
            && w2pp.Run == w2ppExpected.GetProperty("run").GetInt32()
            && w2pp.PackedAttackRun == w2ppExpected.GetProperty("packedAttackRun").GetByte()
            && actual == w2pp,
            $"The 7.69/W2PP common final AttackRun block differed for '{vector.GetProperty("name").GetString()}'.");
    }
}

static LegacyCurrentSpeedStage CalculateW2ppFinalAttackRunFixture(
    int attackSpeedBeforeFinal,
    int runBeforeFinal,
    int attackSpeedBonus,
    int runSpeedBonus,
    int dexterity,
    int face,
    int eligibleMountRunFloor)
{
    var attack = unchecked(attackSpeedBeforeFinal + attackSpeedBonus + dexterity / 5);
    var run = unchecked(runBeforeFinal + runSpeedBonus);
    if (face <= 4 && eligibleMountRunFloor > run)
        run = eligibleMountRunFloor;
    if (run <= 0)
        run = 0;
    if (run > 6)
        run = 6;
    if (attack < 0)
        attack = 0;
    if (attack > 150)
        attack = 150;
    attack /= 10;
    return new LegacyCurrentSpeedStage(attack, run, checked((byte)(attack * 16 + run)));
}

static void LegacyLoginMountRunFloors()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-mount-speed.golden.json")
        ?? throw new InvalidOperationException("The 7.69 mount-speed golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);

    foreach (var table in document.RootElement.GetProperty("tables").EnumerateArray())
    {
        var firstIndex = table.GetProperty("firstIndex").GetInt32();
        var targetFloors = table.GetProperty("targetRunFloors").EnumerateArray().Select(static value => value.GetByte()).ToArray();
        var w2ppFloors = table.GetProperty("w2ppRunFloors").EnumerateArray().Select(static value => value.GetByte()).ToArray();
        Assert(targetFloors.Length == w2ppFloors.Length, "The target/W2PP mount table fixtures have different lengths.");

        for (var offset = 0; offset < targetFloors.Length; offset++)
        {
            var mountIndex = firstIndex + offset;
            var isTemporary = table.GetProperty("kind").GetString() == "temporary";
            var firstEffectValue = isTemporary ? (byte)0 : (byte)1;
            var mount = new LegacyItem((short)mountIndex, 1, firstEffectValue, 0, 0, 0, 0);
            var found = LegacyMountRunRules.TryResolveRunFloor(mount, out var actualFloor);
            Assert(found && actualFloor == targetFloors[offset],
                $"7.69 mount run floor differed for item {mountIndex}.");

            var w2pp = CalculateW2ppMountRunFloor(mount, out var w2ppFloor);
            Assert(w2pp && w2ppFloor == w2ppFloors[offset],
                $"W2PP mount run floor fixture differed for item {mountIndex}.");
        }
    }

    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var input = vector.GetProperty("input");
        var mount = new LegacyItem(
            checked((short)input.GetProperty("mountIndex").GetInt32()),
            1, input.GetProperty("firstEffectValue").GetByte(), 0, 0, 0, 0);
        var found = LegacyMountRunRules.TryResolveRunFloor(mount, out var actualFloor);
        var target = vector.GetProperty("targetExpected");
        Assert(found == target.GetProperty("hasFloor").GetBoolean()
            && actualFloor == target.GetProperty("runFloor").GetInt32(),
            $"7.69 mount eligibility differed for '{vector.GetProperty("name").GetString()}'.");

        var w2ppExpected = vector.GetProperty("w2ppExpected");
        if (w2ppExpected.ValueKind != JsonValueKind.Null)
        {
            var w2pp = CalculateW2ppMountRunFloor(mount, out var w2ppFloor);
            Assert(w2pp == w2ppExpected.GetProperty("hasFloor").GetBoolean()
                && w2ppFloor == w2ppExpected.GetProperty("runFloor").GetInt32(),
                $"W2PP mount gate fixture differed for '{vector.GetProperty("name").GetString()}'.");
        }
        else
        {
            Assert(vector.GetProperty("w2ppNote").GetString() == "out-of-bounds-read",
                "An undefined W2PP mount-table result was not marked explicitly.");
        }
    }
}

static bool CalculateW2ppMountRunFloor(LegacyItem mount, out int runFloor)
{
    runFloor = 0;
    if (mount.Index is >= 3980 and <= 3994)
    {
        runFloor = 6;
        return true;
    }

    if (mount.Index is < 2360 or > 2389 || mount.Value1 <= 0)
        return false;

    // W2PP's fifth table column is 6 for every standard mount row.
    runFloor = 6;
    return true;
}

static void LegacyLoginHasteAffectStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-haste-affect.golden.json")
        ?? throw new InvalidOperationException("The 7.69 Haste-affect golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);

    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var snapshot = new byte[LegacyAccountSnapshot.AffectStride];
        if (vector.TryGetProperty("records", out var records))
        {
            foreach (var record in records.EnumerateArray())
            {
                var slot = record.GetProperty("slot").GetInt32();
                if (slot is < 0 or >= LegacyAccountSnapshot.AffectStride / 8)
                    throw new InvalidOperationException($"Haste fixture slot {slot} is out of bounds.");

                var offset = slot * 8;
                snapshot[offset] = record.GetProperty("type").GetByte();
                snapshot[offset + 1] = record.GetProperty("value").GetByte();
                BinaryPrimitives.WriteUInt16LittleEndian(snapshot.AsSpan(offset + 2), record.GetProperty("level").GetUInt16());
                BinaryPrimitives.WriteUInt32LittleEndian(snapshot.AsSpan(offset + 4), record.GetProperty("time").GetUInt32());
            }
        }
        else if (vector.TryGetProperty("repeat", out var repeat))
        {
            var count = repeat.GetInt32();
            var type = vector.GetProperty("repeatedType").GetByte();
            var value = vector.GetProperty("repeatedValue").GetByte();
            if (count is < 0 or > LegacyAccountSnapshot.AffectStride / 8)
                throw new InvalidOperationException("Haste fixture repeat count exceeds the 32 affect slots.");
            for (var slot = 0; slot < count; slot++)
            {
                snapshot[slot * 8] = type;
                snapshot[slot * 8 + 1] = value;
            }
        }

        var actual = LegacyCurrentScoreMath.CalculateHasteAffectStage(snapshot);
        var target = vector.GetProperty("targetExpected");
        Assert(actual.RunAddition == target.GetProperty("runAddition").GetInt32()
            && actual.RsvFlagsToAdd == target.GetProperty("rsvFlagsToAdd").GetUInt16(),
            $"7.69 Haste affect differed for '{vector.GetProperty("name").GetString()}'.");

        var w2pp = CalculateW2ppHasteAffectFixture(snapshot);
        var w2ppExpected = vector.GetProperty("w2ppExpected");
        Assert(w2pp.RunAddition == w2ppExpected.GetProperty("runAddition").GetInt32()
            && w2pp.RsvFlagsToAdd == w2ppExpected.GetProperty("rsvFlagsToAdd").GetUInt16(),
            $"W2PP Haste affect fixture differed for '{vector.GetProperty("name").GetString()}'.");
        Assert(actual == w2pp,
            $"The 7.69/W2PP Haste-affect stage differed for '{vector.GetProperty("name").GetString()}'.");
    }
}

static LegacyHasteAffectStage CalculateW2ppHasteAffectFixture(ReadOnlySpan<byte> affectSnapshot)
{
    var runAddition = 0;
    ushort rsvFlags = 0;
    for (var slot = 0; slot < LegacyAccountSnapshot.AffectStride / 8; slot++)
    {
        var offset = slot * 8;
        if (affectSnapshot[offset] != 2)
            continue;
        runAddition = unchecked(runAddition + affectSnapshot[offset + 1]);
        rsvFlags |= 0x0020;
    }
    return new LegacyHasteAffectStage(runAddition, rsvFlags);
}

static void LegacyLoginHolyTouchAffectStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-holy-touch.golden.json")
        ?? throw new InvalidOperationException("The 7.69 Holy Touch golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);

    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var snapshot = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var record in vector.GetProperty("records").EnumerateArray())
        {
            var slot = record.GetProperty("slot").GetInt32();
            if (slot is < 0 or >= LegacyAccountSnapshot.AffectStride / 8)
                throw new InvalidOperationException($"Holy Touch fixture slot {slot} is out of bounds.");

            var offset = slot * 8;
            snapshot[offset] = record.GetProperty("type").GetByte();
            snapshot[offset + 1] = record.GetProperty("value").GetByte();
            BinaryPrimitives.WriteUInt16LittleEndian(snapshot.AsSpan(offset + 2), record.GetProperty("level").GetUInt16());
        }

        var headItemIndex = vector.GetProperty("headItemIndex").GetInt32();
        var actual = LegacyCurrentScoreMath.CalculateHolyTouchAffectStage(snapshot, headItemIndex);
        AssertHolyTouch(actual, vector.GetProperty("targetExpected"), "7.69", vector);

        var w2pp = CalculateW2ppHolyTouchFixture(snapshot, headItemIndex);
        AssertHolyTouch(w2pp, vector.GetProperty("w2ppExpected"), "W2PP", vector);
        Assert(actual == w2pp,
            $"The 7.69/W2PP Holy Touch stage differed for '{vector.GetProperty("name").GetString()}'.");
    }

    var failed = false;
    try { LegacyCurrentScoreMath.CalculateHolyTouchAffectStage(new byte[8], 7); }
    catch (ArgumentException) { failed = true; }
    Assert(failed, "The Holy Touch stage accepted a truncated affect snapshot.");
}

static void AssertHolyTouch(
    LegacyHolyTouchAffectStage actual,
    JsonElement expected,
    string source,
    JsonElement vector) =>
    Assert(actual.RunDelta == expected.GetProperty("runDelta").GetInt32()
        && actual.AttackDelta == expected.GetProperty("attackDelta").GetInt32()
        && actual.IntelligenceDelta == expected.GetProperty("intelligenceDelta").GetInt32(),
        $"{source} Holy Touch differed for '{vector.GetProperty("name").GetString()}'.");

static LegacyHolyTouchAffectStage CalculateW2ppHolyTouchFixture(
    ReadOnlySpan<byte> affectSnapshot,
    int headItemIndex)
{
    var runDelta = 0;
    var attackDelta = 0;
    var intelligenceDelta = 0;
    for (var slot = 0; slot < LegacyAccountSnapshot.AffectStride / 8; slot++)
    {
        var offset = slot * 8;
        if (affectSnapshot[offset] != 1)
            continue;

        runDelta = unchecked(runDelta - affectSnapshot[offset + 1]);
        attackDelta = unchecked(attackDelta - 30);
        if (headItemIndex > 50)
            intelligenceDelta = unchecked(intelligenceDelta - 40);
    }

    return new LegacyHolyTouchAffectStage(runDelta, attackDelta, intelligenceDelta);
}

static void LegacyLoginPossessedAffectStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-samaritan.golden.json")
        ?? throw new InvalidOperationException("The 7.69 Possessed golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);

    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var snapshot = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var record in vector.GetProperty("records").EnumerateArray())
        {
            var slot = record.GetProperty("slot").GetInt32();
            if (slot is < 0 or >= LegacyAccountSnapshot.AffectStride / 8)
                throw new InvalidOperationException($"Possessed fixture slot {slot} is out of bounds.");

            var offset = slot * 8;
            snapshot[offset] = record.GetProperty("type").GetByte();
            snapshot[offset + 1] = record.GetProperty("value").GetByte();
            BinaryPrimitives.WriteUInt16LittleEndian(snapshot.AsSpan(offset + 2), record.GetProperty("level").GetUInt16());
        }

        var classMaster = vector.GetProperty("classMaster").GetInt32();
        var startingMaxHp = vector.GetProperty("startingMaxHp").GetInt32();
        var startingConstitution = vector.GetProperty("startingConstitution").GetInt32();
        var actual = LegacyCurrentScoreMath.CalculatePossessedAffectStage(
            snapshot,
            classMaster,
            startingMaxHp,
            startingConstitution);
        AssertPossessed(actual, vector.GetProperty("targetExpected"), "7.69", vector);

        var w2pp = CalculateW2ppPossessedFixture(snapshot, classMaster, startingMaxHp, startingConstitution);
        AssertPossessed(w2pp, vector.GetProperty("w2ppExpected"), "W2PP", vector);
    }

    var failed = false;
    try { LegacyCurrentScoreMath.CalculatePossessedAffectStage(new byte[8], LegacyAccountSnapshot.ClassMasterMortal, 1000, 40); }
    catch (ArgumentException) { failed = true; }
    Assert(failed, "The Possessed stage accepted a truncated affect snapshot.");
}

static void AssertPossessed(
    LegacyPossessedAffectStage actual,
    JsonElement expected,
    string source,
    JsonElement vector) =>
    Assert(actual.MaxHp == expected.GetProperty("maxHp").GetInt32()
        && actual.Constitution == expected.GetProperty("constitution").GetInt16(),
        $"{source} Possessed differed for '{vector.GetProperty("name").GetString()}': actual ({actual.MaxHp}, {actual.Constitution}), expected ({expected.GetProperty("maxHp").GetInt32()}, {expected.GetProperty("constitution").GetInt16()}).");

static LegacyPossessedAffectStage CalculateW2ppPossessedFixture(
    ReadOnlySpan<byte> affectSnapshot,
    int classMaster,
    int startingMaxHp,
    int startingConstitution)
{
    var maxHp = startingMaxHp;
    var constitution = startingConstitution;
    for (var slot = 0; slot < LegacyAccountSnapshot.AffectStride / 8; slot++)
    {
        var offset = slot * 8;
        if (affectSnapshot[offset] != 14)
            continue;

        var value = BinaryPrimitives.ReadUInt16LittleEndian(affectSnapshot.Slice(offset + 2, sizeof(ushort))) * 3 / 4 + affectSnapshot[offset + 1];
        if (classMaster != LegacyAccountSnapshot.ClassMasterArch && classMaster != LegacyAccountSnapshot.ClassMasterMortal)
            value *= 3;

        maxHp = unchecked(maxHp + value * 22);
        var tv = constitution + value;
        constitution = unchecked((short)(constitution + tv * 125 / 100));
    }

    return new LegacyPossessedAffectStage(maxHp, unchecked((short)constitution));
}

static void LegacyLoginAssaultAffectStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-assault.golden.json")
        ?? throw new InvalidOperationException("The 7.69 Assault golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);

    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var snapshot = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var record in vector.GetProperty("records").EnumerateArray())
        {
            var slot = record.GetProperty("slot").GetInt32();
            if (slot is < 0 or >= LegacyAccountSnapshot.AffectStride / 8)
                throw new InvalidOperationException($"Assault fixture slot {slot} is out of bounds.");

            var offset = slot * 8;
            snapshot[offset] = record.GetProperty("type").GetByte();
            snapshot[offset + 1] = record.GetProperty("value").GetByte();
            BinaryPrimitives.WriteUInt16LittleEndian(snapshot.AsSpan(offset + 2), record.GetProperty("level").GetUInt16());
        }

        var startingDamage = vector.GetProperty("startingDamage").GetInt32();
        var startingMaxHp = vector.GetProperty("startingMaxHp").GetInt32();
        var actual = LegacyCurrentScoreMath.CalculateAssaultAffectStage(snapshot, startingDamage, startingMaxHp);
        AssertAssault(actual, vector.GetProperty("targetExpected"), "7.69", vector);

        var w2pp = CalculateW2ppAssaultFixture(snapshot, startingDamage, startingMaxHp);
        AssertAssault(w2pp, vector.GetProperty("w2ppExpected"), "W2PP", vector);
    }

    var failed = false;
    try { LegacyCurrentScoreMath.CalculateAssaultAffectStage(new byte[8], 1000, 1000); }
    catch (ArgumentException) { failed = true; }
    Assert(failed, "The Assault stage accepted a truncated affect snapshot.");
}

static void AssertAssault(
    LegacyAssaultAffectStage actual,
    JsonElement expected,
    string source,
    JsonElement vector) =>
    Assert(actual.DamageMultiplierAdjustment == expected.GetProperty("damageMultiplierAdjustment").GetInt32()
        && actual.Damage == expected.GetProperty("damage").GetInt32()
        && actual.MaxHp == expected.GetProperty("maxHp").GetInt32(),
        $"{source} Assault differed for '{vector.GetProperty("name").GetString()}': actual ({actual.DamageMultiplierAdjustment}, {actual.Damage}, {actual.MaxHp}), expected ({expected.GetProperty("damageMultiplierAdjustment").GetInt32()}, {expected.GetProperty("damage").GetInt32()}, {expected.GetProperty("maxHp").GetInt32()}).");

static LegacyAssaultAffectStage CalculateW2ppAssaultFixture(
    ReadOnlySpan<byte> affectSnapshot,
    int startingDamage,
    int startingMaxHp)
{
    var damageMultiplierAdjustment = 0;
    var damage = startingDamage;
    var maxHp = startingMaxHp;
    for (var slot = 0; slot < LegacyAccountSnapshot.AffectStride / 8; slot++)
    {
        var offset = slot * 8;
        if (affectSnapshot[offset] != 13)
            continue;

        var level = BinaryPrimitives.ReadUInt16LittleEndian(affectSnapshot.Slice(offset + 2, sizeof(ushort)));
        var value = level / 10 + affectSnapshot[offset + 1];
        var totalDamage = unchecked(damage + (damage / 100) * 15);
        damage = totalDamage >= 1_000_000_000 ? 1_000_000_000 : totalDamage;
        damageMultiplierAdjustment = unchecked(damageMultiplierAdjustment + value);
        maxHp = unchecked(maxHp * 9 / 10);
    }

    return new LegacyAssaultAffectStage(damageMultiplierAdjustment, damage, maxHp);
}

static void LegacyLoginSamaritanArmorClassAffectStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-possessed.golden.json")
        ?? throw new InvalidOperationException("The 7.69 Samaritan golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);

    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var snapshot = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var record in vector.GetProperty("records").EnumerateArray())
        {
            var slot = record.GetProperty("slot").GetInt32();
            if (slot is < 0 or >= LegacyAccountSnapshot.AffectStride / 8)
                throw new InvalidOperationException($"Samaritan fixture slot {slot} is out of bounds.");

            var offset = slot * 8;
            snapshot[offset] = record.GetProperty("type").GetByte();
            snapshot[offset + 1] = record.GetProperty("value").GetByte();
            BinaryPrimitives.WriteUInt16LittleEndian(snapshot.AsSpan(offset + 2), record.GetProperty("level").GetUInt16());
        }

        var startingArmorClass = vector.GetProperty("startingArmorClass").GetInt32();
        var actual = LegacyCurrentScoreMath.CalculateSamaritanArmorClassAffectStage(snapshot, startingArmorClass);
        AssertSamaritanArmorClass(actual, vector.GetProperty("targetExpected"), "7.69", vector);

        var w2pp = CalculateW2ppSamaritanArmorClassFixture(snapshot, startingArmorClass);
        AssertSamaritanArmorClass(w2pp, vector.GetProperty("w2ppExpected"), "W2PP", vector);
    }

    var failed = false;
    try { LegacyCurrentScoreMath.CalculateSamaritanArmorClassAffectStage(new byte[8], 100); }
    catch (ArgumentException) { failed = true; }
    Assert(failed, "The Samaritan ArmorClass stage accepted a truncated affect snapshot.");
}

static void AssertSamaritanArmorClass(
    LegacySamaritanArmorClassAffectStage actual,
    JsonElement expected,
    string source,
    JsonElement vector) =>
    Assert(actual.ArmorClass == expected.GetProperty("armorClass").GetInt32(),
        $"{source} Samaritan ArmorClass differed for '{vector.GetProperty("name").GetString()}': actual {actual.ArmorClass}, expected {expected.GetProperty("armorClass").GetInt32()}.");

static LegacySamaritanArmorClassAffectStage CalculateW2ppSamaritanArmorClassFixture(
    ReadOnlySpan<byte> affectSnapshot,
    int startingArmorClass)
{
    var armorClass = startingArmorClass;
    for (var slot = 0; slot < LegacyAccountSnapshot.AffectStride / 8; slot++)
    {
        var offset = slot * 8;
        if (affectSnapshot[offset] != 24)
            continue;

        var value = affectSnapshot[offset + 1];
        var addition = unchecked(armorClass / 4 + value);
        armorClass = unchecked(armorClass + addition);
    }

    return new LegacySamaritanArmorClassAffectStage(armorClass);
}

static void LegacyLoginMagicShieldAffectStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-magic-shield.golden.json")
        ?? throw new InvalidOperationException("The 7.69 Magic Shield golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);

    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var snapshot = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var record in vector.GetProperty("records").EnumerateArray())
        {
            var slot = record.GetProperty("slot").GetInt32();
            if (slot is < 0 or >= LegacyAccountSnapshot.AffectStride / 8)
                throw new InvalidOperationException($"Magic Shield fixture slot {slot} is out of bounds.");

            var offset = slot * 8;
            snapshot[offset] = record.GetProperty("type").GetByte();
            snapshot[offset + 1] = record.GetProperty("value").GetByte();
            BinaryPrimitives.WriteUInt16LittleEndian(snapshot.AsSpan(offset + 2), record.GetProperty("level").GetUInt16());
        }

        var actual = LegacyCurrentScoreMath.CalculateMagicShieldAffectStage(snapshot);
        var targetExpected = vector.GetProperty("targetExpected").GetProperty("armorClassAddition").GetInt32();
        Assert(actual.ArmorClassAddition == targetExpected,
            $"7.69 Magic Shield differed for '{vector.GetProperty("name").GetString()}'.");

        var w2pp = CalculateW2ppMagicShieldFixture(snapshot);
        var w2ppExpected = vector.GetProperty("w2ppExpected").GetProperty("armorClassAddition").GetInt32();
        Assert(w2pp.ArmorClassAddition == w2ppExpected,
            $"W2PP Magic Shield differed for '{vector.GetProperty("name").GetString()}'.");
    }

    var failed = false;
    try { LegacyCurrentScoreMath.CalculateMagicShieldAffectStage(new byte[8]); }
    catch (ArgumentException) { failed = true; }
    Assert(failed, "The Magic Shield stage accepted a truncated affect snapshot.");
}

static LegacyMagicShieldAffectStage CalculateW2ppMagicShieldFixture(ReadOnlySpan<byte> affectSnapshot)
{
    var armorClassAddition = 0;
    for (var slot = 0; slot < LegacyAccountSnapshot.AffectStride / 8; slot++)
    {
        var offset = slot * 8;
        if (affectSnapshot[offset] != 11)
            continue;

        var value = affectSnapshot[offset + 1];
        var level = BinaryPrimitives.ReadUInt16LittleEndian(affectSnapshot.Slice(offset + 2, sizeof(ushort)));
        armorClassAddition = unchecked(armorClassAddition + level / 3 + value);
    }

    return new LegacyMagicShieldAffectStage(armorClassAddition);
}

static void LegacyLoginMagicWeaponAffectStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-magic-weapon.golden.json")
        ?? throw new InvalidOperationException("The 7.69 Magic Weapon golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);

    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var snapshot = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var record in vector.GetProperty("records").EnumerateArray())
        {
            var slot = record.GetProperty("slot").GetInt32();
            if (slot is < 0 or >= LegacyAccountSnapshot.AffectStride / 8)
                throw new InvalidOperationException($"Magic Weapon fixture slot {slot} is out of bounds.");

            var offset = slot * 8;
            snapshot[offset] = record.GetProperty("type").GetByte();
            snapshot[offset + 1] = record.GetProperty("value").GetByte();
            BinaryPrimitives.WriteUInt16LittleEndian(snapshot.AsSpan(offset + 2), record.GetProperty("level").GetUInt16());
        }

        var characterClass = vector.GetProperty("characterClass").GetInt32();
        var learnedSkills = vector.GetProperty("learnedSkills").GetInt32();
        var actual = LegacyCurrentScoreMath.CalculateMagicWeaponAffectStage(snapshot, characterClass, learnedSkills);
        AssertMagicWeapon(actual, vector.GetProperty("targetExpected"), "7.69", vector);

        var w2pp = CalculateW2ppMagicWeaponFixture(snapshot, characterClass, learnedSkills);
        AssertMagicWeapon(w2pp, vector.GetProperty("w2ppExpected"), "W2PP", vector);
        var hasMagicWeapon = vector.GetProperty("records").EnumerateArray()
            .Any(record => record.GetProperty("type").GetByte() == 9);
        Assert(actual.DamageAddition == w2pp.DamageAddition
            && actual.DamageMultiplierAdjustment == w2pp.DamageMultiplierAdjustment
            && (!hasMagicWeapon || actual.MagicAddition != w2pp.MagicAddition),
            $"The 7.69/W2PP Magic Weapon divergence was not preserved for '{vector.GetProperty("name").GetString()}'.");
    }

    var failed = false;
    try { LegacyCurrentScoreMath.CalculateMagicWeaponAffectStage(new byte[8], 1, 0x80000); }
    catch (ArgumentException) { failed = true; }
    Assert(failed, "The Magic Weapon stage accepted a truncated affect snapshot.");
}

static void AssertMagicWeapon(
    LegacyMagicWeaponAffectStage actual,
    JsonElement expected,
    string source,
    JsonElement vector) =>
    Assert(actual.DamageAddition == expected.GetProperty("damageAddition").GetInt32()
        && actual.DamageMultiplierAdjustment == expected.GetProperty("damageMultiplierAdjustment").GetInt32()
        && actual.MagicAddition == expected.GetProperty("magicAddition").GetInt32(),
        $"{source} Magic Weapon differed for '{vector.GetProperty("name").GetString()}'.");

static LegacyMagicWeaponAffectStage CalculateW2ppMagicWeaponFixture(
    ReadOnlySpan<byte> affectSnapshot,
    int characterClass,
    int learnedSkills)
{
    var damageAddition = 0;
    var damageMultiplierAdjustment = 0;
    var magicAddition = 0;
    for (var slot = 0; slot < LegacyAccountSnapshot.AffectStride / 8; slot++)
    {
        var offset = slot * 8;
        if (affectSnapshot[offset] != 9)
            continue;

        var value = affectSnapshot[offset + 1];
        var level = BinaryPrimitives.ReadUInt16LittleEndian(affectSnapshot.Slice(offset + 2, sizeof(ushort)));
        var add = unchecked(level * 5 / 20 + value);
        add = unchecked(add * 3 / 2);
        damageMultiplierAdjustment = unchecked(damageMultiplierAdjustment + 5);
        magicAddition = unchecked(magicAddition + 5);
        if (characterClass == 1 && (learnedSkills & 0x80000) != 0)
        {
            add = unchecked(add * 3);
            damageMultiplierAdjustment = unchecked(damageMultiplierAdjustment + 10);
        }
        damageAddition = unchecked(damageAddition + add);
    }

    return new LegacyMagicWeaponAffectStage(damageAddition, damageMultiplierAdjustment, magicAddition);
}

static void LegacyLoginAthenaTouchAffectStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-athena-touch.golden.json")
        ?? throw new InvalidOperationException("The 7.69 Athena Touch golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);

    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var snapshot = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var record in vector.GetProperty("records").EnumerateArray())
        {
            var slot = record.GetProperty("slot").GetInt32();
            if (slot is < 0 or >= LegacyAccountSnapshot.AffectStride / 8)
                throw new InvalidOperationException($"Athena Touch fixture slot {slot} is out of bounds.");

            var offset = slot * 8;
            snapshot[offset] = record.GetProperty("type").GetByte();
            snapshot[offset + 1] = record.GetProperty("value").GetByte();
            BinaryPrimitives.WriteUInt16LittleEndian(snapshot.AsSpan(offset + 2), record.GetProperty("level").GetUInt16());
        }

        var starting = vector.GetProperty("startingSpecials");
        var actual = LegacyCurrentScoreMath.CalculateAthenaTouchAffectStage(
            snapshot,
            starting[0].GetInt32(),
            starting[1].GetInt32(),
            starting[2].GetInt32(),
            starting[3].GetInt32());
        AssertAthenaTouch(actual, vector.GetProperty("targetExpected"), "7.69", vector);

        var w2pp = CalculateW2ppAthenaTouchFixture(snapshot, starting);
        AssertAthenaTouch(w2pp, vector.GetProperty("w2ppExpected"), "W2PP", vector);
    }

    var failed = false;
    try { LegacyCurrentScoreMath.CalculateAthenaTouchAffectStage(new byte[8], 0, 0, 0, 0); }
    catch (ArgumentException) { failed = true; }
    Assert(failed, "The Athena Touch stage accepted a truncated affect snapshot.");
}

static void AssertAthenaTouch(
    LegacyAthenaTouchAffectStage actual,
    JsonElement expected,
    string source,
    JsonElement vector) =>
    Assert(actual.Special1 == expected[0].GetInt32()
        && actual.Special2 == expected[1].GetInt32()
        && actual.Special3 == expected[2].GetInt32()
        && actual.Special4 == expected[3].GetInt32(),
        $"{source} Athena Touch differed for '{vector.GetProperty("name").GetString()}'.");

static LegacyAthenaTouchAffectStage CalculateW2ppAthenaTouchFixture(
    ReadOnlySpan<byte> affectSnapshot,
    JsonElement starting)
{
    var special1 = starting[0].GetInt32();
    var special2 = starting[1].GetInt32();
    var special3 = starting[2].GetInt32();
    var special4 = starting[3].GetInt32();
    for (var slot = 0; slot < LegacyAccountSnapshot.AffectStride / 8; slot++)
    {
        var offset = slot * 8;
        if (affectSnapshot[offset] != 15)
            continue;

        var value = affectSnapshot[offset + 1];
        var level = BinaryPrimitives.ReadUInt16LittleEndian(affectSnapshot.Slice(offset + 2, sizeof(ushort)));
        var addition = level / 10 + value;
        special1 = Math.Min(200, unchecked(special1 + addition));
        special2 = Math.Min(255, unchecked(special2 + addition));
        special3 = Math.Min(255, unchecked(special3 + addition));
        special4 = Math.Min(255, unchecked(special4 + addition));
    }

    return new LegacyAthenaTouchAffectStage(special1, special2, special3, special4);
}

static void LegacyLoginFanaticismAffectStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-fanaticism.golden.json")
        ?? throw new InvalidOperationException("The 7.69 Fanaticism golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);

    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var snapshot = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var record in vector.GetProperty("records").EnumerateArray())
        {
            var slot = record.GetProperty("slot").GetInt32();
            if (slot is < 0 or >= LegacyAccountSnapshot.AffectStride / 8)
                throw new InvalidOperationException($"Fanaticism fixture slot {slot} is out of bounds.");

            var offset = slot * 8;
            snapshot[offset] = record.GetProperty("type").GetByte();
            snapshot[offset + 1] = record.GetProperty("value").GetByte();
            BinaryPrimitives.WriteUInt16LittleEndian(snapshot.AsSpan(offset + 2), record.GetProperty("level").GetUInt16());
        }

        var startingDexterity = vector.GetProperty("startingDexterity").GetInt32();
        var actual = LegacyCurrentScoreMath.CalculateFanaticismAffectStage(snapshot, startingDexterity);
        Assert(actual.Dexterity == vector.GetProperty("targetExpected").GetInt16(),
            $"7.69 Fanaticism differed for '{vector.GetProperty("name").GetString()}'.");

        var w2pp = CalculateW2ppFanaticismFixture(snapshot, startingDexterity);
        Assert(w2pp.Dexterity == vector.GetProperty("w2ppExpected").GetInt16(),
            $"W2PP Fanaticism differed for '{vector.GetProperty("name").GetString()}'.");
        Assert(actual == w2pp,
            $"The 7.69/W2PP Fanaticism stage differed for '{vector.GetProperty("name").GetString()}'.");
    }

    var failed = false;
    try { LegacyCurrentScoreMath.CalculateFanaticismAffectStage(new byte[8], 100); }
    catch (ArgumentException) { failed = true; }
    Assert(failed, "The Fanaticism stage accepted a truncated affect snapshot.");
}

static LegacyFanaticismAffectStage CalculateW2ppFanaticismFixture(
    ReadOnlySpan<byte> affectSnapshot,
    int startingDexterity)
{
    var dexterity = startingDexterity;
    for (var slot = 0; slot < LegacyAccountSnapshot.AffectStride / 8; slot++)
    {
        var offset = slot * 8;
        if (affectSnapshot[offset] != 5)
            continue;

        var factor = (100 - affectSnapshot[offset + 1]) / 100.0f;
        dexterity = unchecked((short)(dexterity * factor));
    }

    return new LegacyFanaticismAffectStage(unchecked((short)dexterity));
}

static void LegacyLoginDexterityAffectStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-dexterity.golden.json")
        ?? throw new InvalidOperationException("The 7.69 Dexterity golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);

    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var snapshot = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var record in vector.GetProperty("records").EnumerateArray())
        {
            var slot = record.GetProperty("slot").GetInt32();
            if (slot is < 0 or >= LegacyAccountSnapshot.AffectStride / 8)
                throw new InvalidOperationException($"Dexterity fixture slot {slot} is out of bounds.");

            var offset = slot * 8;
            snapshot[offset] = record.GetProperty("type").GetByte();
            snapshot[offset + 1] = record.GetProperty("value").GetByte();
            BinaryPrimitives.WriteUInt16LittleEndian(snapshot.AsSpan(offset + 2), record.GetProperty("level").GetUInt16());
        }

        var startingDexterity = vector.GetProperty("startingDexterity").GetInt32();
        var actual = LegacyCurrentScoreMath.CalculateDexterityAffectStage(snapshot, startingDexterity);
        Assert(actual.Dexterity == vector.GetProperty("targetExpected").GetInt16(),
            $"7.69 Dexterity differed for '{vector.GetProperty("name").GetString()}'.");

        var w2pp = CalculateW2ppDexterityFixture(snapshot, startingDexterity);
        Assert(w2pp.Dexterity == vector.GetProperty("w2ppExpected").GetInt16(),
            $"W2PP Dexterity differed for '{vector.GetProperty("name").GetString()}'.");
        Assert(actual == w2pp,
            $"The 7.69/W2PP Dexterity stage differed for '{vector.GetProperty("name").GetString()}'.");
    }

    var failed = false;
    try { LegacyCurrentScoreMath.CalculateDexterityAffectStage(new byte[8], 100); }
    catch (ArgumentException) { failed = true; }
    Assert(failed, "The Dexterity stage accepted a truncated affect snapshot.");
}

static LegacyDexterityAffectStage CalculateW2ppDexterityFixture(
    ReadOnlySpan<byte> affectSnapshot,
    int startingDexterity)
{
    var dexterity = startingDexterity;
    for (var slot = 0; slot < LegacyAccountSnapshot.AffectStride / 8; slot++)
    {
        var offset = slot * 8;
        if (affectSnapshot[offset] != 6)
            continue;

        var factor = (affectSnapshot[offset + 1] + 100) / 100.0f;
        dexterity = unchecked((short)(dexterity * factor));
    }

    return new LegacyDexterityAffectStage(unchecked((short)dexterity));
}

static void LegacyLoginArmorClassReductionAffectStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-armor-class-reduction.golden.json")
        ?? throw new InvalidOperationException("The 7.69 ArmorClass-reduction golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);

    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var snapshot = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var record in vector.GetProperty("records").EnumerateArray())
        {
            var slot = record.GetProperty("slot").GetInt32();
            if (slot is < 0 or >= LegacyAccountSnapshot.AffectStride / 8)
                throw new InvalidOperationException($"ArmorClass-reduction fixture slot {slot} is out of bounds.");

            var offset = slot * 8;
            snapshot[offset] = record.GetProperty("type").GetByte();
            snapshot[offset + 1] = record.GetProperty("value").GetByte();
            BinaryPrimitives.WriteUInt16LittleEndian(snapshot.AsSpan(offset + 2), record.GetProperty("level").GetUInt16());
        }

        var startingArmorClass = vector.GetProperty("startingArmorClass").GetInt32();
        var actual = LegacyCurrentScoreMath.CalculateArmorClassReductionAffectStage(snapshot, startingArmorClass);
        Assert(actual.ArmorClass == vector.GetProperty("targetExpected").GetInt32(),
            $"7.69 ArmorClass reduction differed for '{vector.GetProperty("name").GetString()}'.");

        var w2pp = CalculateW2ppArmorClassReductionFixture(snapshot, startingArmorClass);
        Assert(w2pp.ArmorClass == vector.GetProperty("w2ppExpected").GetInt32(),
            $"W2PP ArmorClass reduction differed for '{vector.GetProperty("name").GetString()}'.");
        Assert(actual == w2pp,
            $"The 7.69/W2PP ArmorClass-reduction stage differed for '{vector.GetProperty("name").GetString()}'.");
    }

    var failed = false;
    try { LegacyCurrentScoreMath.CalculateArmorClassReductionAffectStage(new byte[8], 100); }
    catch (ArgumentException) { failed = true; }
    Assert(failed, "The ArmorClass-reduction stage accepted a truncated affect snapshot.");
}

static LegacyArmorClassReductionAffectStage CalculateW2ppArmorClassReductionFixture(
    ReadOnlySpan<byte> affectSnapshot,
    int startingArmorClass)
{
    var armorClass = startingArmorClass;
    for (var slot = 0; slot < LegacyAccountSnapshot.AffectStride / 8; slot++)
    {
        var offset = slot * 8;
        if (affectSnapshot[offset] != 12)
            continue;

        var factor = (100 - affectSnapshot[offset + 1]) / 100.0f;
        armorClass = (int)(armorClass * factor);
    }

    return new LegacyArmorClassReductionAffectStage(armorClass);
}

static void LegacyLoginTransformationSpeedAffectStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-transformation-speed.golden.json")
        ?? throw new InvalidOperationException("The 7.69 transformation-speed golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);
    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var snapshot = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var record in vector.GetProperty("records").EnumerateArray())
        {
            var offset = record.GetProperty("slot").GetInt32() * 8;
            snapshot[offset] = record.GetProperty("type").GetByte();
            snapshot[offset + 1] = record.GetProperty("value").GetByte();
            if (record.TryGetProperty("level", out var level))
                BinaryPrimitives.WriteUInt16LittleEndian(snapshot.AsSpan(offset + 2), level.GetUInt16());
            if (record.TryGetProperty("time", out var time))
                BinaryPrimitives.WriteUInt32LittleEndian(snapshot.AsSpan(offset + 4), time.GetUInt32());
        }
        var characterClass = vector.GetProperty("characterClass").GetInt32();
        var learnedSkills = vector.GetProperty("learnedSkills").GetInt32();
        var actual = LegacyCurrentScoreMath.CalculateTransformationSpeedAffectStage(snapshot, characterClass, learnedSkills);
        AssertTransformationSpeed(actual, vector.GetProperty("targetExpected"), "7.69", vector);
        var w2pp = CalculateW2ppTransformationSpeedFixture(snapshot, characterClass, learnedSkills);
        AssertTransformationSpeed(w2pp, vector.GetProperty("w2ppExpected"), "W2PP", vector);
    }

    var failed = false;
    try { LegacyCurrentScoreMath.CalculateTransformationSpeedAffectStage(new byte[8], 2, 0); }
    catch (ArgumentException) { failed = true; }
    Assert(failed, "The transformation-speed stage accepted a truncated affect snapshot.");
}

static void AssertTransformationSpeed(LegacyTransformationSpeedAffectStage actual, JsonElement expected, string source, JsonElement vector) =>
    Assert(actual.HasActiveTransformation == expected.GetProperty("hasActiveTransformation").GetBoolean()
        && actual.AttackSpeedBonus == expected.GetProperty("attackSpeedBonus").GetInt32()
        && actual.RunSpeedBonus == expected.GetProperty("runSpeedBonus").GetInt32(),
        $"{source} transformation speed differed for '{vector.GetProperty("name").GetString()}'.");

static LegacyTransformationSpeedAffectStage CalculateW2ppTransformationSpeedFixture(ReadOnlySpan<byte> affects, int characterClass, int learnedSkills)
{
    var result = new LegacyTransformationSpeedAffectStage(false, 0, 0);
    for (var slot = 0; slot < 32; slot++)
    {
        var offset = slot * 8;
        if (affects[offset] != 16) continue;
        var form = affects[offset + 1] - 1;
        if (form < 0 || form >= 5 || characterClass != 2) continue;
        var attack = new[] { 15, 60, 115, 155, 155 }[form];
        var run = new[] { 1, 0, 1, 0, 3 }[form];
        var add = form switch
        {
            1 when (learnedSkills & 0x80000) != 0 => 40,
            2 when (learnedSkills & 0x200000) != 0 => 20,
            3 => 30,
            4 => 20,
            _ => 0,
        };
        result = new LegacyTransformationSpeedAffectStage(true, attack + add, run);
    }
    return result;
}

static void LegacyLoginTransformationDamageAffectStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-transformation-damage.golden.json")
        ?? throw new InvalidOperationException("The 7.69 transformation-damage golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);
    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var snapshot = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var record in vector.GetProperty("records").EnumerateArray())
        {
            var offset = record.GetProperty("slot").GetInt32() * 8;
            snapshot[offset] = record.GetProperty("type").GetByte();
            snapshot[offset + 1] = record.GetProperty("value").GetByte();
            BinaryPrimitives.WriteUInt16LittleEndian(snapshot.AsSpan(offset + 2), record.GetProperty("level").GetUInt16());
        }

        var characterClass = vector.GetProperty("characterClass").GetInt32();
        var learnedSkills = vector.GetProperty("learnedSkills").GetInt32();
        var actual = LegacyCurrentScoreMath.CalculateTransformationDamageAffectStage(snapshot, characterClass, learnedSkills);
        AssertTransformationDamage(actual, vector.GetProperty("targetExpected"), "7.69", vector);
        var w2pp = CalculateW2ppTransformationDamageFixture(snapshot, characterClass, learnedSkills);
        AssertTransformationDamage(w2pp, vector.GetProperty("w2ppExpected"), "W2PP", vector);
    }

    var failed = false;
    try { LegacyCurrentScoreMath.CalculateTransformationDamageAffectStage(new byte[8], 2, 0); }
    catch (ArgumentException) { failed = true; }
    Assert(failed, "The transformation-damage stage accepted a truncated affect snapshot.");
}

static void AssertTransformationDamage(LegacyTransformationDamageAffectStage actual, JsonElement expected, string source, JsonElement vector) =>
    Assert(actual.HasActiveTransformation == expected.GetProperty("hasActiveTransformation").GetBoolean()
        && actual.DamageAddition == expected.GetProperty("damageAddition").GetInt32()
        && actual.DamageMultiplierAdjustment == expected.GetProperty("damageMultiplierAdjustment").GetInt32(),
        $"{source} transformation damage differed for '{vector.GetProperty("name").GetString()}'.");

static LegacyTransformationDamageAffectStage CalculateW2ppTransformationDamageFixture(ReadOnlySpan<byte> affects, int characterClass, int learnedSkills)
{
    var hasTransform = false;
    var damageAddition = 0;
    var multiplierAdjustment = 0;
    for (var slot = 0; slot < 32; slot++)
    {
        var offset = slot * 8;
        if (affects[offset] != 16) continue;
        var form = affects[offset + 1] - 1;
        if (form < 0 || form >= 5 || characterClass != 2) continue;
        var level = BinaryPrimitives.ReadUInt16LittleEndian(affects.Slice(offset + 2, 2));
        var (baseMin, baseMax) = form switch
        {
            0 => (110, 130), 1 => (80, 100), 2 => (100, 120), 3 => (90, 110), 4 => (105, 120),
            _ => throw new InvalidOperationException("W2PP fixture form passed its source gate."),
        };
        var damAdd = form switch
        {
            0 when (learnedSkills & 0x20000) != 0 => 10,
            2 when (learnedSkills & 0x200000) != 0 => 10,
            4 => 10,
            _ => 0,
        };
        var minimum = baseMin + damAdd;
        var maximum = baseMax + damAdd;
        multiplierAdjustment = unchecked(multiplierAdjustment + (maximum - minimum) * level / 200 + minimum - 100);
        if (form == 0) damageAddition = unchecked(damageAddition + 10);
        hasTransform = true;
    }
    return new LegacyTransformationDamageAffectStage(hasTransform, damageAddition, multiplierAdjustment);
}

static void LegacyLoginTransformationArmorClassAffectStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-transformation-ac.golden.json")
        ?? throw new InvalidOperationException("The 7.69 transformation-AC golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);
    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var snapshot = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var record in vector.GetProperty("records").EnumerateArray())
        {
            var offset = record.GetProperty("slot").GetInt32() * 8;
            snapshot[offset] = record.GetProperty("type").GetByte();
            snapshot[offset + 1] = record.GetProperty("value").GetByte();
            BinaryPrimitives.WriteUInt16LittleEndian(snapshot.AsSpan(offset + 2), record.GetProperty("level").GetUInt16());
        }

        var startingArmorClass = vector.GetProperty("startingArmorClass").GetInt32();
        var characterClass = vector.GetProperty("characterClass").GetInt32();
        var learnedSkills = vector.GetProperty("learnedSkills").GetInt32();
        var actual = LegacyCurrentScoreMath.CalculateTransformationArmorClassAffectStage(
            snapshot, startingArmorClass, characterClass, learnedSkills);
        AssertTransformationArmorClass(actual, vector.GetProperty("targetExpected"), "7.69", vector);
        var w2pp = CalculateW2ppTransformationArmorClassFixture(
            snapshot, startingArmorClass, characterClass, learnedSkills);
        AssertTransformationArmorClass(w2pp, vector.GetProperty("w2ppExpected"), "W2PP", vector);
    }

    var failed = false;
    try { LegacyCurrentScoreMath.CalculateTransformationArmorClassAffectStage(new byte[8], 100, 2, 0); }
    catch (ArgumentException) { failed = true; }
    Assert(failed, "The transformation-AC stage accepted a truncated affect snapshot.");
}

static void AssertTransformationArmorClass(
    LegacyTransformationArmorClassAffectStage actual,
    JsonElement expected,
    string source,
    JsonElement vector) =>
    Assert(actual.HasActiveTransformation == expected.GetProperty("hasActiveTransformation").GetBoolean()
        && actual.ArmorClass == expected.GetProperty("armorClass").GetInt32(),
        $"{source} transformation AC differed for '{vector.GetProperty("name").GetString()}'.");

static LegacyTransformationArmorClassAffectStage CalculateW2ppTransformationArmorClassFixture(
    ReadOnlySpan<byte> affects,
    int startingArmorClass,
    int characterClass,
    int learnedSkills)
{
    var hasTransform = false;
    var armorClass = startingArmorClass;
    for (var slot = 0; slot < 32; slot++)
    {
        var offset = slot * 8;
        if (affects[offset] != 16) continue;
        var form = affects[offset + 1] - 1;
        if (form < 0 || form >= 5 || characterClass != 2) continue;
        var level = BinaryPrimitives.ReadUInt16LittleEndian(affects.Slice(offset + 2, 2));
        var (baseMin, baseMax) = form switch
        {
            0 => (95, 105), 1 => (100, 110), 2 => (105, 115), 3 => (110, 125), 4 => (110, 120),
            _ => throw new InvalidOperationException("W2PP fixture form passed its source gate."),
        };
        var acAdd = form switch
        {
            0 when (learnedSkills & 0x20000) != 0 => 3,
            2 when (learnedSkills & 0x200000) != 0 => 5,
            3 => 10,
            4 => 5,
            _ => 0,
        };
        var minimum = baseMin + acAdd;
        var multiplier = (baseMax - baseMin) * level / 200 + minimum;
        armorClass = unchecked(armorClass * multiplier) / 100;
        if (form == 0)
            armorClass = unchecked(armorClass + 5);
        hasTransform = true;
    }

    return new LegacyTransformationArmorClassAffectStage(hasTransform, armorClass);
}

static void LegacyLoginTransformationMaxHpAffectStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-transformation-maxhp.golden.json")
        ?? throw new InvalidOperationException("The 7.69 transformation-MaxHp golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);
    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var snapshot = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var record in vector.GetProperty("records").EnumerateArray())
        {
            var offset = record.GetProperty("slot").GetInt32() * 8;
            snapshot[offset] = record.GetProperty("type").GetByte();
            snapshot[offset + 1] = record.GetProperty("value").GetByte();
            BinaryPrimitives.WriteUInt16LittleEndian(snapshot.AsSpan(offset + 2), record.GetProperty("level").GetUInt16());
        }

        var startingMaxHp = vector.GetProperty("startingMaxHp").GetInt32();
        var characterClass = vector.GetProperty("characterClass").GetInt32();
        var learnedSkills = vector.GetProperty("learnedSkills").GetInt32();
        var actual = LegacyCurrentScoreMath.CalculateTransformationMaxHpAffectStage(
            snapshot, startingMaxHp, characterClass, learnedSkills);
        AssertTransformationMaxHp(actual, vector.GetProperty("targetExpected"), "7.69", vector);
        var w2pp = CalculateW2ppTransformationMaxHpFixture(
            snapshot, startingMaxHp, characterClass, learnedSkills);
        AssertTransformationMaxHp(w2pp, vector.GetProperty("w2ppExpected"), "W2PP", vector);
    }

    var failed = false;
    try { LegacyCurrentScoreMath.CalculateTransformationMaxHpAffectStage(new byte[8], 1000, 2, 0); }
    catch (ArgumentException) { failed = true; }
    Assert(failed, "The transformation-MaxHp stage accepted a truncated affect snapshot.");
}

static void AssertTransformationMaxHp(
    LegacyTransformationMaxHpAffectStage actual,
    JsonElement expected,
    string source,
    JsonElement vector) =>
    Assert(actual.HasActiveTransformation == expected.GetProperty("hasActiveTransformation").GetBoolean()
        && actual.MaxHp == expected.GetProperty("maxHp").GetInt32(),
        $"{source} transformation MaxHp differed for '{vector.GetProperty("name").GetString()}'.");

static LegacyTransformationMaxHpAffectStage CalculateW2ppTransformationMaxHpFixture(
    ReadOnlySpan<byte> affects,
    int startingMaxHp,
    int characterClass,
    int learnedSkills)
{
    var hasTransform = false;
    var maxHp = startingMaxHp;
    for (var slot = 0; slot < 32; slot++)
    {
        var offset = slot * 8;
        if (affects[offset] != 16) continue;
        var form = affects[offset + 1] - 1;
        if (form < 0 || form >= 5 || characterClass != 2) continue;
        var level = BinaryPrimitives.ReadUInt16LittleEndian(affects.Slice(offset + 2, 2));
        var (baseMin, baseMax) = form switch
        {
            0 => (95, 105), 1 => (110, 140), 2 => (100, 120), 3 => (105, 110), 4 => (105, 115),
            _ => throw new InvalidOperationException("W2PP fixture form passed its source gate."),
        };
        var hpAdd = form switch
        {
            0 when (learnedSkills & 0x20000) != 0 => 5,
            1 when (learnedSkills & 0x80000) != 0 => 15,
            2 when (learnedSkills & 0x200000) != 0 => 5,
            3 => 5,
            4 => 10,
            _ => 0,
        };
        var minimum = baseMin + hpAdd;
        var multiplier = (baseMax - baseMin) * level / 200 + minimum;
        maxHp = unchecked(maxHp * multiplier) / 100;
        hasTransform = true;
    }

    return new LegacyTransformationMaxHpAffectStage(hasTransform, maxHp);
}

static void LegacyLoginTransformationCriticalAndEquipmentStages()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-transformation-critical-equipment.golden.json")
        ?? throw new InvalidOperationException("The 7.69 transformation-critical-equipment golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);
    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var snapshot = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var record in vector.GetProperty("records").EnumerateArray())
        {
            var offset = record.GetProperty("slot").GetInt32() * 8;
            snapshot[offset] = record.GetProperty("type").GetByte();
            snapshot[offset + 1] = record.GetProperty("value").GetByte();
            BinaryPrimitives.WriteUInt16LittleEndian(snapshot.AsSpan(offset + 2), record.GetProperty("level").GetUInt16());
        }

        var startingHeadItem = ParseLegacyItem(vector.GetProperty("startingHeadItem"));
        var characterClass = vector.GetProperty("characterClass").GetInt32();
        var learnedSkills = vector.GetProperty("learnedSkills").GetInt32();
        var characterLevel = vector.GetProperty("characterLevel").GetInt32();
        var classMaster = vector.GetProperty("classMaster").GetInt32();
        var special3 = vector.GetProperty("special3").GetInt32();

        var critical = LegacyCurrentScoreMath.CalculateTransformationCriticalAffectStage(
            snapshot, characterClass, learnedSkills);
        AssertTransformationCritical(critical, vector.GetProperty("targetCriticalExpected"), "7.69", vector);
        var equipment = LegacyCurrentScoreMath.CalculateTransformationEquipmentAffectStage(
            snapshot, startingHeadItem, characterClass, characterLevel, classMaster, special3);
        AssertTransformationEquipment(equipment, vector.GetProperty("targetEquipmentExpected"), "7.69", vector);

        var w2ppCritical = CalculateW2ppTransformationCriticalFixture(snapshot, characterClass, learnedSkills);
        AssertTransformationCritical(w2ppCritical, vector.GetProperty("w2ppCriticalExpected"), "W2PP", vector);
        var w2ppEquipment = CalculateW2ppTransformationEquipmentFixture(
            snapshot, startingHeadItem, characterClass, characterLevel, classMaster, special3);
        AssertTransformationEquipment(w2ppEquipment, vector.GetProperty("w2ppEquipmentExpected"), "W2PP", vector);
    }

    var failedCritical = false;
    try { LegacyCurrentScoreMath.CalculateTransformationCriticalAffectStage(new byte[8], 2, 0); }
    catch (ArgumentException) { failedCritical = true; }
    Assert(failedCritical, "The transformation-critical stage accepted a truncated affect snapshot.");

    var failedEquipment = false;
    try { LegacyCurrentScoreMath.CalculateTransformationEquipmentAffectStage(new byte[8], new LegacyItem(7, 2, 3, 4, 5, 6, 7), 2, 100, 2, 0); }
    catch (ArgumentException) { failedEquipment = true; }
    Assert(failedEquipment, "The transformation-equipment stage accepted a truncated affect snapshot.");
}

static LegacyItem ParseLegacyItem(JsonElement element) => new(
    checked((short)element.GetProperty("index").GetInt32()),
    element.GetProperty("effect1").GetByte(),
    element.GetProperty("value1").GetByte(),
    element.GetProperty("effect2").GetByte(),
    element.GetProperty("value2").GetByte(),
    element.GetProperty("effect3").GetByte(),
    element.GetProperty("value3").GetByte());

static void AssertTransformationCritical(
    LegacyTransformationCriticalAffectStage actual,
    JsonElement expected,
    string source,
    JsonElement vector) =>
    Assert(actual.HasActiveTransformation == expected.GetProperty("hasActiveTransformation").GetBoolean()
        && actual.CriticalAddition == expected.GetProperty("criticalAddition").GetInt32(),
        $"{source} transformation Critical differed for '{vector.GetProperty("name").GetString()}'.");

static void AssertTransformationEquipment(
    LegacyTransformationEquipmentAffectStage actual,
    JsonElement expected,
    string source,
    JsonElement vector)
{
    var expectedItem = ParseLegacyItem(expected.GetProperty("headItem"));
    Assert(actual.HasActiveTransformation == expected.GetProperty("hasActiveTransformation").GetBoolean()
        && actual.HeadItem == expectedItem,
        $"{source} transformation equipment differed for '{vector.GetProperty("name").GetString()}'.");
}

static LegacyTransformationCriticalAffectStage CalculateW2ppTransformationCriticalFixture(
    ReadOnlySpan<byte> affects,
    int characterClass,
    int learnedSkills)
{
    var hasTransform = false;
    var criticalAddition = 0;
    for (var slot = 0; slot < 32; slot++)
    {
        var offset = slot * 8;
        if (affects[offset] != 16) continue;
        var form = affects[offset + 1] - 1;
        if (form < 0 || form >= 5 || characterClass != 2) continue;
        var addition = form switch
        {
            0 when (learnedSkills & 0x20000) != 0 => 5,
            2 when (learnedSkills & 0x200000) != 0 => 5,
            4 => 6,
            _ => 0,
        };
        criticalAddition = unchecked(criticalAddition + addition);
        hasTransform = true;
    }
    return new LegacyTransformationCriticalAffectStage(hasTransform, criticalAddition);
}

static LegacyTransformationEquipmentAffectStage CalculateW2ppTransformationEquipmentFixture(
    ReadOnlySpan<byte> affects,
    LegacyItem startingHeadItem,
    int characterClass,
    int characterLevel,
    int classMaster,
    int special3)
{
    var hasTransform = false;
    var headItem = startingHeadItem;
    for (var slot = 0; slot < 32; slot++)
    {
        var offset = slot * 8;
        if (affects[offset] != 16) continue;
        var form = affects[offset + 1] - 1;
        if (form < 0 || form >= 5 || characterClass != 2) continue;
        var itemIndex = form == 4 ? 32 : form + 22;
        var baseSanctuary = form == 0 ? 15 : 100;
        var levelTerm = classMaster is not LegacyAccountSnapshot.ClassMasterArch
            and not LegacyAccountSnapshot.ClassMasterMortal
            ? characterLevel + LegacyCurrentScoreMath.TargetMaximumLevel
            : characterLevel;
        var sanctuary = (special3 + levelTerm * 2) / 3;
        sanctuary = Math.Clamp((sanctuary - baseSanctuary) / 12, 0, 9);
        headItem = headItem with
        {
            Index = checked((short)itemIndex),
            Effect1 = checked((byte)LegacyItemEffect.Sanctuary),
            Value1 = checked((byte)sanctuary),
        };
        hasTransform = true;
    }
    return new LegacyTransformationEquipmentAffectStage(hasTransform, headItem);
}

static void LegacyLoginTransformationAffectCompositionStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-transformation-composition.golden.json")
        ?? throw new InvalidOperationException("The 7.69 transformation-composition golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);

    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var snapshot = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var record in vector.GetProperty("records").EnumerateArray())
        {
            var slot = record.GetProperty("slot").GetInt32();
            if (slot is < 0 or >= LegacyAccountSnapshot.AffectStride / 8)
                throw new InvalidOperationException($"Transformation composition fixture slot {slot} is out of bounds.");

            var offset = slot * 8;
            snapshot[offset] = record.GetProperty("type").GetByte();
            snapshot[offset + 1] = record.GetProperty("value").GetByte();
            BinaryPrimitives.WriteUInt16LittleEndian(snapshot.AsSpan(offset + 2), record.GetProperty("level").GetUInt16());
        }

        var input = vector;
        var startingHeadItem = ParseLegacyItem(input.GetProperty("startingHeadItem"));
        var resist = input.GetProperty("resist");
        var actual = LegacyCurrentScoreMath.CalculateTransformationAffectCompositionStage(
            snapshot,
            startingHeadItem,
            input.GetProperty("startingArmorClass").GetInt32(),
            input.GetProperty("startingMaxHp").GetInt32(),
            resist[0].GetInt32(),
            resist[1].GetInt32(),
            resist[2].GetInt32(),
            resist[3].GetInt32(),
            input.GetProperty("characterClass").GetInt32(),
            input.GetProperty("learnedSkills").GetInt32(),
            input.GetProperty("characterLevel").GetInt32(),
            input.GetProperty("classMaster").GetInt32(),
            input.GetProperty("special3").GetInt32());
        AssertTransformationComposition(actual, input.GetProperty("targetExpected"), "7.69", input);

        var w2pp = CalculateW2ppTransformationCompositionFixture(
            snapshot,
            startingHeadItem,
            input.GetProperty("startingArmorClass").GetInt32(),
            input.GetProperty("startingMaxHp").GetInt32(),
            resist[0].GetInt32(),
            resist[1].GetInt32(),
            resist[2].GetInt32(),
            resist[3].GetInt32(),
            input.GetProperty("characterClass").GetInt32(),
            input.GetProperty("learnedSkills").GetInt32(),
            input.GetProperty("characterLevel").GetInt32(),
            input.GetProperty("classMaster").GetInt32(),
            input.GetProperty("special3").GetInt32());
        AssertTransformationComposition(w2pp, input.GetProperty("w2ppExpected"), "W2PP", input);
    }

    var failed = false;
    try
    {
        LegacyCurrentScoreMath.CalculateTransformationAffectCompositionStage(
            new byte[8], new LegacyItem(7, 2, 3, 4, 5, 6, 7),
            100, 1000, 0, 0, 0, 0, 2, 0, 100, LegacyAccountSnapshot.ClassMasterMortal, 0);
    }
    catch (ArgumentException)
    {
        failed = true;
    }
    Assert(failed, "The transformation composition stage accepted a truncated affect snapshot.");
}

static void AssertTransformationComposition(
    LegacyTransformationAffectCompositionStage actual,
    JsonElement expected,
    string source,
    JsonElement vector)
{
    var name = vector.GetProperty("name").GetString();
    Assert(actual.HasActiveTransformation == expected.GetProperty("hasActiveTransformation").GetBoolean(),
        $"{source} transformation composition gate differed for '{name}'.");

    var speed = expected.GetProperty("speed");
    Assert(actual.Speed.AttackSpeedBonus == speed.GetProperty("attackSpeedBonus").GetInt32()
        && actual.Speed.RunSpeedBonus == speed.GetProperty("runSpeedBonus").GetInt32(),
        $"{source} transformation composition speed differed for '{name}'.");

    var damage = expected.GetProperty("damage");
    Assert(actual.Damage.DamageAddition == damage.GetProperty("damageAddition").GetInt32()
        && actual.Damage.DamageMultiplierAdjustment == damage.GetProperty("damageMultiplierAdjustment").GetInt32(),
        $"{source} transformation composition damage differed for '{name}'.");

    Assert(actual.ArmorClass.ArmorClass == expected.GetProperty("armorClass").GetInt32(),
        $"{source} transformation composition AC differed for '{name}'.");
    Assert(actual.MaxHp.MaxHp == expected.GetProperty("maxHp").GetInt32(),
        $"{source} transformation composition MaxHp differed for '{name}'.");
    Assert(actual.Critical.CriticalAddition == expected.GetProperty("criticalAddition").GetInt32(),
        $"{source} transformation composition Critical differed for '{name}'.");
    Assert(actual.Equipment.HeadItem == ParseLegacyItem(expected.GetProperty("headItem")),
        $"{source} transformation composition equipment differed for '{name}'.");

    var resistance = expected.GetProperty("resistance");
    Assert(actual.Resistance.Holy == resistance.GetProperty("holy").GetInt32()
        && actual.Resistance.Thunder == resistance.GetProperty("thunder").GetInt32()
        && actual.Resistance.Fire == resistance.GetProperty("fire").GetInt32()
        && actual.Resistance.Ice == resistance.GetProperty("ice").GetInt32()
        && actual.Resistance.HeadItemIndex == resistance.GetProperty("headItemIndex").GetInt32(),
        $"{source} transformation composition resistance differed for '{name}'.");
}

static LegacyTransformationAffectCompositionStage CalculateW2ppTransformationCompositionFixture(
    ReadOnlySpan<byte> affects,
    LegacyItem startingHeadItem,
    int startingArmorClass,
    int startingMaxHp,
    int holy,
    int thunder,
    int fire,
    int ice,
    int characterClass,
    int learnedSkills,
    int characterLevel,
    int classMaster,
    int special3)
{
    var speed = CalculateW2ppTransformationSpeedFixture(affects, characterClass, learnedSkills);
    var damage = CalculateW2ppTransformationDamageFixture(affects, characterClass, learnedSkills);
    var armorClass = CalculateW2ppTransformationArmorClassFixture(
        affects, startingArmorClass, characterClass, learnedSkills);
    var maxHp = CalculateW2ppTransformationMaxHpFixture(affects, startingMaxHp, characterClass, learnedSkills);
    var critical = CalculateW2ppTransformationCriticalFixture(affects, characterClass, learnedSkills);
    var equipment = CalculateW2ppTransformationEquipmentFixture(
        affects, startingHeadItem, characterClass, characterLevel, classMaster, special3);
    var resistance = CalculateW2ppResistanceFixture(
        holy, thunder, fire, ice, affects, characterClass, startingHeadItem.Index, learnedSkills);

    return new LegacyTransformationAffectCompositionStage(
        speed.HasActiveTransformation,
        speed,
        damage,
        armorClass,
        maxHp,
        critical,
        equipment,
        resistance);
}

static void LegacyLoginScoreCoordinatorStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-score-coordinator.golden.json")
        ?? throw new InvalidOperationException("The 7.69 score-coordinator golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);
    var itemData = LoadLegacyItemDataTable(new byte[LegacyItemDataTable.BodySizeInBytes]);

    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var baseScore = ParseCoordinatorScore(vector.GetProperty("baseScore"));
        var currentScore = ParseCoordinatorScore(vector.GetProperty("currentScore"));
        var mob = new LegacyMobCombatState(
            vector.GetProperty("characterClass").GetByte(),
            vector.GetProperty("learnedSkill").GetUInt32(),
            0,
            currentScore,
            0)
        {
            BaseScore = baseScore,
            Rsv = 0x0040,
        };
        var originalMob = mob;
        var equipment = new LegacyItem[LegacyCharacterSelection.EquipmentCount];
        equipment[0] = ParseLegacyItem(vector.GetProperty("headItem"));

        var snapshot = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var record in vector.GetProperty("records").EnumerateArray())
        {
            var slot = record.GetProperty("slot").GetInt32();
            if (slot is < 0 or >= LegacyAccountSnapshot.AffectStride / 8)
                throw new InvalidOperationException($"Score coordinator fixture slot {slot} is out of bounds.");

            var offset = slot * 8;
            snapshot[offset] = record.GetProperty("type").GetByte();
            snapshot[offset + 1] = record.GetProperty("value").GetByte();
            BinaryPrimitives.WriteUInt16LittleEndian(snapshot.AsSpan(offset + 2), record.GetProperty("level").GetUInt16());
        }

        var resist = vector.GetProperty("resist");
        var actual = LegacyCurrentScoreMath.CalculateCurrentScoreCoordinatorStage(
            mob,
            equipment,
            itemData,
            snapshot,
            vector.GetProperty("classMaster").GetInt32(),
            vector.GetProperty("soul").GetByte(),
            vector.GetProperty("isSummon").GetBoolean(),
            resist[0].GetInt32(),
            resist[1].GetInt32(),
            resist[2].GetInt32(),
            resist[3].GetInt32());
        var expected = vector.GetProperty("expected");
        AssertCoordinatorTransformation(actual.Transformation, expected.GetProperty("transformation"), vector);
        AssertCoordinatorScore(actual.AfterTransformation.CurrentScore, expected.GetProperty("afterTransformation"), vector, "after transformation");
        var expectedMagicShieldAddition = expected.TryGetProperty("magicShield", out var magicShield)
            ? magicShield.GetProperty("armorClassAddition").GetInt32()
            : 0;
        Assert(actual.MagicShield.ArmorClassAddition == expectedMagicShieldAddition,
            $"Score coordinator Magic Shield differed for '{vector.GetProperty("name").GetString()}'.");
        var expectedAfterMagicShield = expected.TryGetProperty("afterMagicShield", out var afterMagicShield)
            ? afterMagicShield
            : expected.GetProperty("afterTransformation");
        AssertCoordinatorScore(actual.AfterMagicShield.CurrentScore, expectedAfterMagicShield, vector, "after Magic Shield");
        var expectedMagicWeapon = expected.TryGetProperty("magicWeapon", out var magicWeapon)
            ? magicWeapon
            : default;
        if (expectedMagicWeapon.ValueKind == JsonValueKind.Object)
        {
            Assert(actual.MagicWeapon.DamageAddition == expectedMagicWeapon.GetProperty("damageAddition").GetInt32()
                && actual.MagicWeapon.DamageMultiplierAdjustment == expectedMagicWeapon.GetProperty("damageMultiplierAdjustment").GetInt32()
                && actual.MagicWeapon.MagicAddition == expectedMagicWeapon.GetProperty("magicAddition").GetInt32(),
                $"Score coordinator Magic Weapon differed for '{vector.GetProperty("name").GetString()}'.");
        }
        var expectedAfterMagicWeapon = expected.TryGetProperty("afterMagicWeapon", out var afterMagicWeapon)
            ? afterMagicWeapon
            : expectedAfterMagicShield;
        AssertCoordinatorScore(actual.AfterMagicWeapon.CurrentScore, expectedAfterMagicWeapon, vector, "after Magic Weapon");
        if (expected.TryGetProperty("athenaTouch", out var athenaTouch))
        {
            Assert(actual.AthenaTouch.Special1 == athenaTouch[0].GetInt32()
                && actual.AthenaTouch.Special2 == athenaTouch[1].GetInt32()
                && actual.AthenaTouch.Special3 == athenaTouch[2].GetInt32()
                && actual.AthenaTouch.Special4 == athenaTouch[3].GetInt32(),
                $"Score coordinator Athena Touch differed for '{vector.GetProperty("name").GetString()}'.");
        }
        var expectedAfterAthenaTouch = expected.TryGetProperty("afterAthenaTouch", out var afterAthenaTouch)
            ? afterAthenaTouch
            : expectedAfterMagicWeapon;
        AssertCoordinatorScore(actual.AfterAthenaTouch.CurrentScore, expectedAfterAthenaTouch, vector, "after Athena Touch");
        if (expectedAfterAthenaTouch.TryGetProperty("specials", out var afterAthenaSpecials))
        {
            Assert(actual.AfterAthenaTouch.CurrentScore.Special1 == afterAthenaSpecials[0].GetInt16()
                && actual.AfterAthenaTouch.CurrentScore.Special2 == afterAthenaSpecials[1].GetInt16()
                && actual.AfterAthenaTouch.CurrentScore.Special3 == afterAthenaSpecials[2].GetInt16()
                && actual.AfterAthenaTouch.CurrentScore.Special4 == afterAthenaSpecials[3].GetInt16(),
                $"Score coordinator Athena Touch projection differed for '{vector.GetProperty("name").GetString()}'.");
        }
        if (expected.TryGetProperty("fanaticism", out var expectedFanaticism))
        {
            Assert(actual.Fanaticism.Dexterity == expectedFanaticism.GetInt16()
                && actual.AfterFanaticism.CurrentScore.Dexterity == expectedFanaticism.GetInt16(),
                $"Score coordinator Fanaticism projection differed for '{vector.GetProperty("name").GetString()}'.");
        }
        if (expected.TryGetProperty("dexterity", out var expectedDexterity))
        {
            Assert(actual.Dexterity.Dexterity == expectedDexterity.GetInt16()
                && actual.AfterDexterity.CurrentScore.Dexterity == expectedDexterity.GetInt16(),
                $"Score coordinator Dexterity projection differed for '{vector.GetProperty("name").GetString()}'.");
        }
        if (expected.TryGetProperty("armorClassReduction", out var expectedArmorClassReduction))
        {
            Assert(actual.ArmorClassReduction.ArmorClass == expectedArmorClassReduction.GetInt32()
                && actual.AfterArmorClassReduction.CurrentScore.Ac == expectedArmorClassReduction.GetInt32(),
                $"Score coordinator ArmorClass reduction projection differed for '{vector.GetProperty("name").GetString()}'.");
        }
        AssertCoordinatorScoreTail(actual.AfterSoul.CurrentScore, expected.GetProperty("afterSoul"), vector, "after Soul");
        AssertCoordinatorScoreTail(actual.AfterKibita.CurrentScore, expected.GetProperty("afterKibita"), vector, "after Kibita");

        var haste = expected.GetProperty("haste");
        Assert(actual.Haste.RunAddition == haste.GetProperty("runAddition").GetInt32()
            && actual.Haste.RsvFlagsToAdd == haste.GetProperty("rsvFlagsToAdd").GetUInt16(),
            $"Score coordinator Haste differed for '{vector.GetProperty("name").GetString()}'.");
        var resistance = expected.GetProperty("resistance");
        Assert(actual.Resistance.Holy == resistance[0].GetByte()
            && actual.Resistance.Thunder == resistance[1].GetByte()
            && actual.Resistance.Fire == resistance[2].GetByte()
            && actual.Resistance.Ice == resistance[3].GetByte()
            && actual.RsvWithAffects == expected.GetProperty("rsvWithAffects").GetUInt16(),
            $"Score coordinator terminal resistance/Rsv differed for '{vector.GetProperty("name").GetString()}'.");

        Assert(actual.Abilities.SaveMana == 0 && actual.Abilities.Magic == 0 && actual.Abilities.RunSeed == 0
            && actual.Abilities.AttackSpeedSeed == 0 && actual.Abilities.RegenHpSeed == 0
            && actual.Abilities.RegenMpSeed == 0 && actual.Abilities.CriticalSeed == 0,
            $"The empty-item coordinator fixture produced unexpected ability seeds for '{vector.GetProperty("name").GetString()}'.");
        Assert(mob == originalMob, "The score coordinator mutated its input MOB.");
    }

    var failed = false;
    try
    {
        LegacyCurrentScoreMath.CalculateCurrentScoreCoordinatorStage(
            new LegacyMobCombatState(2, 0, 0, default, 0),
            Array.Empty<LegacyItem>(),
            itemData,
            new byte[LegacyAccountSnapshot.AffectStride],
            LegacyAccountSnapshot.ClassMasterMortal,
            0,
            false,
            0, 0, 0, 0);
    }
    catch (ArgumentException)
    {
        failed = true;
    }
    Assert(failed, "The score coordinator accepted equipment without slot 0.");
}

static LegacyScore ParseCoordinatorScore(JsonElement element) => default(LegacyScore) with
{
    Level = element.GetProperty("level").GetInt32(),
    Ac = element.GetProperty("ac").GetInt32(),
    Damage = element.GetProperty("damage").GetInt32(),
    AttackRun = element.GetProperty("attackRun").GetByte(),
    MaxHp = element.GetProperty("maxHp").GetInt32(),
    MaxMp = element.GetProperty("maxMp").GetInt32(),
    Hp = element.GetProperty("hp").GetInt32(),
    Mp = element.GetProperty("mp").GetInt32(),
    Strength = element.GetProperty("strength").GetInt16(),
    Intelligence = element.GetProperty("intelligence").GetInt16(),
    Dexterity = element.GetProperty("dexterity").GetInt16(),
    Constitution = element.GetProperty("constitution").GetInt16(),
    Special1 = element.GetProperty("special1").GetInt16(),
    Special2 = element.GetProperty("special2").GetInt16(),
    Special3 = element.GetProperty("special3").GetInt16(),
    Special4 = element.GetProperty("special4").GetInt16(),
};

static void AssertCoordinatorTransformation(LegacyTransformationAffectCompositionStage actual, JsonElement expected, JsonElement vector)
{
    var name = vector.GetProperty("name").GetString();
    var speed = expected;
    var resistance = expected.GetProperty("resistance");
    Assert(actual.Speed.AttackSpeedBonus == speed.GetProperty("attackSpeedBonus").GetInt32()
        && actual.Speed.RunSpeedBonus == speed.GetProperty("runSpeedBonus").GetInt32()
        && actual.Damage.DamageAddition == speed.GetProperty("damageAddition").GetInt32()
        && actual.Damage.DamageMultiplierAdjustment == speed.GetProperty("damageMultiplierAdjustment").GetInt32()
        && actual.ArmorClass.ArmorClass == speed.GetProperty("armorClass").GetInt32()
        && actual.MaxHp.MaxHp == speed.GetProperty("maxHp").GetInt32()
        && actual.Critical.CriticalAddition == speed.GetProperty("criticalAddition").GetInt32()
        && actual.Equipment.HeadItem.Index == speed.GetProperty("headItemIndex").GetInt16()
        && actual.Resistance.Holy == resistance[0].GetInt32()
        && actual.Resistance.Thunder == resistance[1].GetInt32()
        && actual.Resistance.Fire == resistance[2].GetInt32()
        && actual.Resistance.Ice == resistance[3].GetInt32(),
        $"Score coordinator transformation differed for '{name}'.");
}

static void AssertCoordinatorScore(LegacyScore actual, JsonElement expected, JsonElement vector, string stage)
{
    Assert(actual.Level == expected.GetProperty("level").GetInt32()
        && actual.Ac == expected.GetProperty("ac").GetInt32()
        && actual.Damage == expected.GetProperty("damage").GetInt32()
        && actual.MaxHp == expected.GetProperty("maxHp").GetInt32()
        && actual.MaxMp == expected.GetProperty("maxMp").GetInt32()
        && actual.Hp == expected.GetProperty("hp").GetInt32()
        && actual.Mp == expected.GetProperty("mp").GetInt32(),
        $"Score coordinator {stage} score differed for '{vector.GetProperty("name").GetString()}'.");
}

static void AssertCoordinatorScoreTail(LegacyScore actual, JsonElement expected, JsonElement vector, string stage)
{
    var hasConstitution = expected.TryGetProperty("constitution", out var constitution);
    Assert(actual.MaxHp == expected.GetProperty("maxHp").GetInt32()
        && actual.MaxMp == expected.GetProperty("maxMp").GetInt32()
        && actual.Strength == expected.GetProperty("strength").GetInt16()
        && actual.Intelligence == expected.GetProperty("intelligence").GetInt16()
        && (!hasConstitution || actual.Constitution == constitution.GetInt16()),
        $"Score coordinator {stage} score differed for '{vector.GetProperty("name").GetString()}'.");
}

static void LegacyLoginSoulHealthManaStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-soul.golden.json")
        ?? throw new InvalidOperationException("The 7.69 Soul golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);
    var inputs = document.RootElement.GetProperty("inputs");

    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var active in vector.GetProperty("activeAffects").EnumerateArray())
        {
            var slot = active.GetProperty("slot").GetInt32();
            var type = active.GetProperty("type").GetByte();
            if (slot is < 0 or >= LegacyAccountSnapshot.AffectStride / 8)
                throw new InvalidOperationException($"Soul fixture slot {slot} is out of bounds.");
            affect[slot * 8] = type;
        }

        var current = default(LegacyScore) with
        {
            Level = inputs.GetProperty("level").GetInt32(),
            Hp = inputs.GetProperty("currentHp").GetInt32(),
            Mp = inputs.GetProperty("currentMp").GetInt32(),
            MaxHp = inputs.GetProperty("maxHp").GetInt32(),
            MaxMp = inputs.GetProperty("maxMp").GetInt32(),
            Intelligence = vector.TryGetProperty("intelligence", out var vectorIntelligence)
                ? vectorIntelligence.GetInt16()
                : inputs.GetProperty("intelligence").GetInt16(),
            Constitution = inputs.GetProperty("constitution").GetInt16(),
        };
        var mob = new LegacyMobCombatState(0, 0, 0, current, 0) { BaseScore = current };
        var isSummon = vector.TryGetProperty("isSummon", out var summon) && summon.GetBoolean();
        var scored = LegacyCurrentScoreMath.ApplySoulHealthManaStage(
            mob,
            affect,
            vector.GetProperty("classMaster").GetInt32(),
            vector.GetProperty("soul").GetByte(),
            isSummon);

        Assert(scored.CurrentScore.MaxHp == vector.GetProperty("expectedMaxHp").GetInt32(), $"Soul vector {vector.GetProperty("name").GetString()} produced the wrong MaxHP.");
        Assert(scored.CurrentScore.MaxMp == vector.GetProperty("expectedMaxMp").GetInt32(), $"Soul vector {vector.GetProperty("name").GetString()} produced the wrong MaxMP.");
        Assert(scored.CurrentScore.Hp == current.Hp && scored.CurrentScore.Mp == current.Mp, "The Soul stage changed current HP/MP.");
        Assert(scored.CurrentScore.Intelligence == current.Intelligence && scored.CurrentScore.Constitution == current.Constitution, "The Soul stage changed the visible Int/Con attributes.");
        Assert(scored.BaseScore == current && mob.CurrentScore == current, "The Soul stage mutated the base or input score.");
    }

    var invalidAffect = new byte[LegacyAccountSnapshot.AffectStride - 1];
    var failed = false;
    try
    {
        LegacyCurrentScoreMath.ApplySoulHealthManaStage(
            new LegacyMobCombatState(0, 0, 0, default, 0),
            invalidAffect,
            LegacyAccountSnapshot.ClassMasterMortal,
            3);
    }
    catch (ArgumentException)
    {
        failed = true;
    }

    Assert(failed, "The Soul stage accepted a truncated affect snapshot.");
    var summonBypass = LegacyCurrentScoreMath.ApplySoulHealthManaStage(
        new LegacyMobCombatState(0, 0, 0, default, 0),
        ReadOnlySpan<byte>.Empty,
        LegacyExperienceMath.ClassMasterCelestial,
        3,
        isSummon: true);
    Assert(summonBypass.CurrentScore == default, "The summon path did not bypass Soul processing before reading affects.");
}

static void LegacyLoginKibitaSoulStage()
{
    using var fixture = typeof(TestAssemblyMarker).Assembly.GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login-kibita-soul.golden.json")
        ?? throw new InvalidOperationException("The 7.69 Kibita Soul golden fixture is missing.");
    using var document = JsonDocument.Parse(fixture);
    var inputs = document.RootElement.GetProperty("inputs");

    foreach (var vector in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var active in vector.GetProperty("activeAffects").EnumerateArray())
        {
            var slot = active.GetProperty("slot").GetInt32();
            if (slot is < 0 or >= LegacyAccountSnapshot.AffectStride / 8)
                throw new InvalidOperationException($"Kibita Soul fixture slot {slot} is out of bounds.");
            affect[slot * 8] = active.GetProperty("type").GetByte();
        }

        var level = vector.GetProperty("level").GetInt32();
        var isSummon = vector.TryGetProperty("isSummon", out var summon) && summon.GetBoolean();
        var current = default(LegacyScore) with
        {
            Level = level,
            Hp = inputs.GetProperty("currentHp").GetInt32(),
            Mp = inputs.GetProperty("currentMp").GetInt32(),
            MaxHp = inputs.GetProperty("maxHp").GetInt32(),
            MaxMp = inputs.GetProperty("maxMp").GetInt32(),
            Strength = inputs.GetProperty("strength").GetInt16(),
            Intelligence = inputs.GetProperty("intelligence").GetInt16(),
            Constitution = inputs.GetProperty("constitution").GetInt16(),
        };
        var baseScore = current with
        {
            Intelligence = vector.TryGetProperty("baseIntelligence", out var baseIntelligence)
                ? baseIntelligence.GetInt16()
                : current.Intelligence,
        };
        var original = new LegacyMobCombatState(0, 0, 0, current, 0) { BaseScore = baseScore };
        var classMaster = vector.GetProperty("classMaster").GetInt32();
        var soulStage = LegacyCurrentScoreMath.ApplySoulHealthManaStage(
            original, affect, classMaster, vector.GetProperty("soul").GetByte(), isSummon);
        var afterSoul = vector.GetProperty("afterSoulStage");
        Assert(soulStage.CurrentScore.MaxHp == afterSoul.GetProperty("maxHp").GetInt32()
            && soulStage.CurrentScore.MaxMp == afterSoul.GetProperty("maxMp").GetInt32(),
            $"Kibita pre-stage Soul result differed for '{vector.GetProperty("name").GetString()}'.");

        var result = LegacyCurrentScoreMath.ApplyKibitaSoulStage(soulStage, affect, classMaster, isSummon);
        var expected = vector.GetProperty("targetExpected");
        Assert(result.CurrentScore.Strength == expected.GetProperty("strength").GetInt16()
            && result.CurrentScore.Intelligence == expected.GetProperty("intelligence").GetInt16()
            && result.CurrentScore.MaxHp == expected.GetProperty("maxHp").GetInt32()
            && result.CurrentScore.MaxMp == expected.GetProperty("maxMp").GetInt32()
            && result.CurrentScore.Hp == current.Hp
            && result.CurrentScore.Mp == current.Mp,
            $"7.69 Kibita Soul stage differed for '{vector.GetProperty("name").GetString()}'.");
        Assert(original.CurrentScore == current && original.BaseScore == baseScore,
            "The Kibita Soul calculation mutated its input MOB.");

        if (vector.TryGetProperty("w2ppExpected", out var w2ppExpected))
        {
            var itemInt = 2000 - baseScore.Intelligence > 2000 ? 2000 : baseScore.Intelligence;
            var mpDelta = itemInt * 2;
            var w2ppMaxMp = unchecked(soulStage.CurrentScore.MaxMp + (soulStage.CurrentScore.MaxMp + mpDelta));
            Assert(w2ppExpected.GetProperty("maxMp").GetInt32() == w2ppMaxMp
                && result.CurrentScore.MaxMp != w2ppMaxMp,
                "The target/W2PP Kibita MaxMP formula divergence was not reproduced.");
        }
    }
}

static void ItemBonusMath()
{
    var armor = new LegacyItemDefinition(1, "ARMOR", 0, 48, 4, 0, []);
    var rolls = new Queue<int>([0, 0, 2, 0]);
    var generated = LegacyItemBonusMath.Apply(new LegacyItem(1, 0, 0, 0, 0, 0, 0), armor, 1, 0, roll: _ => rolls.Dequeue());
    Assert(generated.Effect1 == 71 && generated.Value1 == 40 && generated.Effect2 == 60 && generated.Value2 == 6, "SetItemBonus armor generation did not preserve the legacy position, tier and offset mapping.");

    var staticEffect = armor with { StaticEffects = [new LegacyItemStaticEffect(0x3D, 7)] };
    var overridden = LegacyItemBonusMath.Apply(new LegacyItem(1, 0, 0, 0, 0, 0, 0), staticEffect, 1, 0, roll: _ => 0);
    Assert(overridden.Effect1 == 61 && overridden.Value1 == 7, "The ItemList static effect pass did not override the generated first effect.");

    var special = new LegacyItemDefinition(419, "SPECIAL", 0, 0, 0, 0, []);
    var filled = LegacyItemBonusMath.Apply(new LegacyItem(419, 0, 0, 0, 0, 0, 0), special, 0, 0, roll: _ => 5);
    Assert(filled.Effect1 == 59 && filled.Value1 == 5 && filled.Effect2 == 59 && filled.Value2 == 5 && filled.Effect3 == 59 && filled.Value3 == 5, "SetItemBonus did not fill the legacy special-item effect slots.");
}

static void ItemRefinementBonusMath()
{
    var helmet = new LegacyItemDefinition(1, "HELMET", 0, 0, 2, 0, []);
    var refinedHelmet = LegacyItemRefinementMath.Apply(
        new LegacyItem(1, LegacyItemEffect.Sanctuary, 5, 0, 0, 0, 0),
        helmet,
        roll: maximum => maximum switch { 2 => 1, 25 => 24, _ => 0 });
    Assert(refinedHelmet.Effect1 == LegacyItemEffect.Sanctuary && refinedHelmet.Value1 == 6 && refinedHelmet.Effect2 == 4 && refinedHelmet.Value2 == 40 && refinedHelmet.Effect3 == 60 && refinedHelmet.Value3 == 4, "SetItemBonus2 helmet table or sanctuary progression differs from the legacy source.");

    var armor = new LegacyItemDefinition(2, "ARMOR", 0, 0, 4, 0, []);
    var refinedArmor = LegacyItemRefinementMath.Apply(
        new LegacyItem(2, 0, 0, 0, 0, 0, 0),
        armor,
        roll: maximum => maximum switch { 2 => 1, 48 => 47, _ => 0 });
    Assert(refinedArmor.Effect1 == LegacyItemEffect.Sanctuary && refinedArmor.Value1 == 1 && refinedArmor.Effect2 == 60 && refinedArmor.Value2 == 4 && refinedArmor.Effect3 == 71 && refinedArmor.Value3 == 70, "SetItemBonus2 armor table mapping differs from the 7.60 legacy table.");

    var boots = new LegacyItemDefinition(6, "BOOTS", 0, 0, 32, 0, [new LegacyItemStaticEffect(LegacyItemEffect.Damage, 100)]);
    var refinedBoots = LegacyItemRefinementMath.Apply(
        new LegacyItem(6, 0, 0, 0, 0, 0, 0),
        boots,
        CreateCombatItemDataTable(),
        roll: maximum => 0);
    Assert(refinedBoots.Effect2 == LegacyItemEffect.Damage && refinedBoots.Value2 == 0 && refinedBoots.Effect3 == 74 && refinedBoots.Value3 == 18, "SetItemBonus2 boot damage cap did not preserve BASE_GetItemAbility behavior.");
}

static void UseItemWireAndClassReset()
{
    var payload = new byte[UseItemRequest.PayloadSize];
    BinaryPrimitives.WriteInt32LittleEndian(payload, 1);
    BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4), 0);
    BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(8), 1);
    BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(12), 1);
    BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(16), 20);
    BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(18), 30);
    BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(20), 4);
    var codec = LegacyFrameCodec.CreateDefault();
    var decoded = codec.Decode(codec.Encode(UseItemRequest.MessageType, 1, 123, payload, 16));
    Assert(UseItemRequest.TryParse(decoded, out var request) && request is { SourceType: 1, SourceSlot: 0, DestinationType: 1, DestinationSlot: 1, GridX: 20, GridY: 30, WarpId: 4 }, "MSG_UseItem was not decoded with the legacy 36-byte layout.");

    var table = CreateCombatItemDataTable();
    var mob = new byte[LegacyAccountSnapshot.CharacterStride];
    var score = new LegacyScore(1, 0, 0, 0, 0, 0, 0, 1000, 500, 200, 100, 0, 0, 0, 0, 0, 0, 0, 0);
    score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
    score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), 100);
    var resetItem = new LegacyItem(4016, 61, 2, 0, 0, 0, 0);
    var destination = new LegacyItem(7, 0, 0, 0, 0, 0, 0);
    var potion = new LegacyItem(4017, 61, 2, 0, 0, 0, 0);
    resetItem.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
    destination.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + LegacyItem.SizeInBytes));
    potion.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (2 * LegacyItem.SizeInBytes)));
    var sealedSource = new LegacyItem(4018, 61, 2, 0, 0, 0, 0);
    var sealedTarget = new LegacyItem(8, 0, 0, 0, 0, 0, 0);
    var earringSource = new LegacyItem(4019, 61, 2, 0, 0, 0, 0);
    var earringTarget = new LegacyItem(10, (byte)LegacyItemEffect.Sanctuary, 9, 0, 0, 0, 0);
    var celestialSource = new LegacyItem(4019, 61, 2, 0, 0, 0, 0);
    var celestialTarget = new LegacyItem(11, 0, 0, 0, 0, 0, 0);
    var legendarySource = new LegacyItem(575, 61, 2, 0, 0, 0, 0);
    var legendaryTarget = new LegacyItem(12, 0, 0, 0, 0, 0, 0);
    var legendaryFailureSource = new LegacyItem(575, 61, 2, 0, 0, 0, 0);
    var legendaryFailureTarget = new LegacyItem(12, 0, 0, 0, 0, 0, 0);
    var orcPill = new LegacyItem(4021, 61, 2, 0, 0, 0, 0);
    var fairyDust = new LegacyItem(4022, 61, 2, 0, 0, 0, 0);
    var crescentEye = new LegacyItem(4023, 61, 2, 0, 0, 0, 0);
    var kappa = new LegacyItem(787, 61, 2, 0, 0, 0, 0);
    var recall = new LegacyItem(4024, 61, 2, 0, 0, 0, 0);
    var starGem = new LegacyItem(4025, 61, 2, 0, 0, 0, 0);
    var portal = new LegacyItem(4026, 61, 2, 0, 0, 0, 0);
    var catalyst = new LegacyItem(3344, 61, 2, 0, 0, 0, 0);
    var restorer = new LegacyItem(3351, 61, 2, 0, 0, 0, 0);
    var mount = new LegacyItem(2333, 60, 1, 4, 10, 5, 9);
    var silverBar = new LegacyItem(4010, 61, 2, 0, 0, 0, 0);
    var donateVoucher = new LegacyItem(184, 61, 2, 0, 0, 0, 0);
    var pvpRuby = new LegacyItem(3200, 61, 2, 0, 0, 0, 0);
    var pvpSapphire = new LegacyItem(3201, 61, 2, 0, 0, 0, 0);
    sealedSource.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (3 * LegacyItem.SizeInBytes)));
    sealedTarget.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (4 * LegacyItem.SizeInBytes)));
    earringSource.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (5 * LegacyItem.SizeInBytes)));
    celestialSource.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (6 * LegacyItem.SizeInBytes)));
    celestialTarget.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (7 * LegacyItem.SizeInBytes)));
    earringTarget.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (8 * LegacyItem.SizeInBytes)));
    legendarySource.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (9 * LegacyItem.SizeInBytes)));
    legendaryTarget.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (10 * LegacyItem.SizeInBytes)));
    legendaryFailureSource.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (11 * LegacyItem.SizeInBytes)));
    legendaryFailureTarget.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (12 * LegacyItem.SizeInBytes)));
    orcPill.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (13 * LegacyItem.SizeInBytes)));
    fairyDust.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (14 * LegacyItem.SizeInBytes)));
    crescentEye.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (15 * LegacyItem.SizeInBytes)));
    kappa.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (16 * LegacyItem.SizeInBytes)));
    recall.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (17 * LegacyItem.SizeInBytes)));
    starGem.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (18 * LegacyItem.SizeInBytes)));
    portal.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (19 * LegacyItem.SizeInBytes)));
    catalyst.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (20 * LegacyItem.SizeInBytes)));
    restorer.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (21 * LegacyItem.SizeInBytes)));
    mount.Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (14 * LegacyItem.SizeInBytes)));
    silverBar.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (22 * LegacyItem.SizeInBytes)));
    donateVoucher.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (25 * LegacyItem.SizeInBytes)));
    pvpRuby.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (23 * LegacyItem.SizeInBytes)));
    pvpSapphire.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (24 * LegacyItem.SizeInBytes)));
    var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
    BinaryPrimitives.WriteInt16LittleEndian(mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset), LegacyAccountSnapshot.ClassMasterMortal);
    var world = new WorldHub(itemData: table);
    Assert(world.Enter(1, "TEST", static (_, _) => ValueTask.CompletedTask), "Class-reset fixture participant was not registered.");
    Assert(world.SetCharacterState(1, 0, 0, 0, 0, 0, 0, 0, mob, LegacyAccountSnapshot.ClassMasterMortal, ReadOnlyMemory<byte>.Empty, mobExtra), "Class-reset fixture state was not registered.");

    var result = world.TryApplyClassReset(1, request!, out var outcome, roll: static _ => 0);
    Assert(result == LegacyUseItemResult.Accepted && outcome is not null, "Vol 190 class reset was rejected for a matching 7.60 fixture.");
    Assert(outcome!.SourceItem.Index == 4016 && outcome.SourceItem.Effect1 == 61 && outcome.SourceItem.Value1 == 1, "Class-reset source amount was not decremented in the authoritative MOB.");
    Assert(outcome.DestinationItem.Effect1 == LegacyItemEffect.Sanctuary && outcome.DestinationItem.Effect2 == LegacyItemEffect.Damage && outcome.DestinationItem.Value2 == 30 && outcome.DestinationItem.Effect3 == 3 && outcome.DestinationItem.Value3 == 30, "Class-reset destination did not receive the legacy armor table row.");

    var potionRequest = new UseItemRequest(1, 2, 1, 1, 20, 30, 4);
    var potionResult = world.TryApplyPotion(1, potionRequest, out var potionOutcome, nowMilliseconds: 1000);
    Assert(potionResult == LegacyUseItemResult.Accepted && potionOutcome is { RequestedHp: 200, RequestedMp: 130, SourceItem.Value1: 1 }, "Vol 1 potion did not update the authoritative requested HP/MP buffers or consume one unit.");
    Assert(world.TryApplyPotion(1, potionRequest, out _, nowMilliseconds: 1050) == LegacyUseItemResult.PotionDelay, "PotionDelay did not reject a second potion inside the legacy cooldown.");

    var refinementRequest = new UseItemRequest(1, 3, 1, 4, 20, 30, 4);
    var refinementResult = world.TryApplyRefinement(1, refinementRequest, out var refinementOutcome, roll: static _ => 0);
    Assert(refinementResult == LegacyUseItemResult.Accepted && refinementOutcome is { Succeeded: true, SourceItem.Value1: 1 } && refinementOutcome.DestinationItem.Effect1 == LegacyItemEffect.Sanctuary && refinementOutcome.DestinationItem.Value1 == 1, "Vol 4 sealed-item refinement did not add sanctuary, advance it, and consume one unit.");

    var earringRequest = new UseItemRequest(1, 5, 1, 8, 20, 30, 4);
    var earringResult = world.TryApplyRefinement(1, earringRequest, out var earringOutcome, roll: static _ => 0);
    Assert(earringResult == LegacyUseItemResult.Accepted && earringOutcome is { Succeeded: true } && earringOutcome.DestinationItem.Value1 == 230, "Vol 5 earring refinement did not use the fixed 15 percent branch or BASE_SetItemSanc encoding.");

    var celestialRequest = new UseItemRequest(1, 6, 1, 7, 20, 30, 4);
    var celestialResult = world.TryApplyRefinement(1, celestialRequest, out var celestialOutcome, roll: static _ => 0);
    Assert(celestialResult == LegacyUseItemResult.Accepted && celestialOutcome is { Succeeded: true } && celestialOutcome.DestinationItem.Effect1 == LegacyItemEffect.Sanctuary && celestialOutcome.DestinationItem.Value1 == 1, "Vol 5 celestial refinement did not add and advance sanctuary.");

    var legendaryRequest = new UseItemRequest(1, 9, 1, 10, 20, 30, 4);
    var legendaryResult = world.TryApplyLegendaryUpgrade(1, legendaryRequest, out var legendaryOutcome, roll: static _ => 0);
    Assert(legendaryResult == LegacyUseItemResult.Accepted && legendaryOutcome is { Succeeded: true, SourceItem.Value1: 1, DestinationItem.Index: 13 }, "Vol 9 legendary upgrade did not replace the destination with ItemList.Extra or consume one catalyst.");

    var legendaryFailureRequest = new UseItemRequest(1, 11, 1, 12, 20, 30, 4);
    var legendaryFailureResult = world.TryApplyLegendaryUpgrade(1, legendaryFailureRequest, out var legendaryFailureOutcome, roll: static _ => 99);
    Assert(legendaryFailureResult == LegacyUseItemResult.Accepted && legendaryFailureOutcome is { Succeeded: false, SourceItem.Value1: 1, DestinationItem.Index: 12 }, "Vol 9 legendary failure did not preserve the destination and consume the catalyst.");

    var orcPillRequest = new UseItemRequest(1, 13, 1, 0, 20, 30, 4);
    var orcPillResult = world.TryApplyOrcPill(1, orcPillRequest, out var orcPillOutcome);
    Assert(orcPillResult == LegacyUseItemResult.Accepted && orcPillOutcome is { SkillBonus: 9, QuestFlag: 1, SourceItem.Value1: 1 }, "Vol 6 Orc Pill did not add nine skill points, set the quest flag, and consume one unit.");
    Assert(world.TryApplyOrcPill(1, orcPillRequest, out _) == LegacyUseItemResult.AlreadyCompleted, "Vol 6 Orc Pill was not idempotently blocked after the quest flag was set.");

    var fairyDustRequest = new UseItemRequest(1, 14, 1, 0, 20, 30, 4);
    var fairyDustResult = world.TryApplyExperienceConsumable(1, fairyDustRequest, out var fairyDustOutcome);
    Assert(fairyDustResult == LegacyUseItemResult.Accepted && fairyDustOutcome is { Volatile: 7, LeveledUp: true, Level: 2, SourceItem.Value1: 1 } && fairyDustOutcome.Experience == 1124, "Vol 7 Fairy Dust did not set the next legacy threshold, level up once, and consume one unit.");

    var crescentEyeRequest = new UseItemRequest(1, 15, 1, 0, 20, 30, 4);
    var crescentEyeResult = world.TryApplyExperienceConsumable(1, crescentEyeRequest, out var crescentEyeOutcome);
    Assert(crescentEyeResult == LegacyUseItemResult.Accepted && crescentEyeOutcome is { Volatile: 8, LeveledUp: true, Level: 3, SourceItem.Value1: 1 } && crescentEyeOutcome.Experience == 3624, "Vol 8 Crescent Eye did not add 2500 XP, process the level-up, and consume one unit.");

    var affectRequest = new UseItemRequest(1, 16, 1, 0, 20, 30, 4);
    var affectResult = world.TryApplyAffectConsumable(1, affectRequest, out var affectOutcome);
    Assert(affectResult == LegacyUseItemResult.Accepted && affectOutcome is { AffectSlot: 0, AffectValue: 1, AffectTime: 80, SourceItem.Value1: 1 } && affectOutcome.AffectSnapshot[0] == 4 && affectOutcome.AffectSnapshot[1] == 1 && BinaryPrimitives.ReadUInt32LittleEndian(affectOutcome.AffectSnapshot.AsSpan(4)) == 80, "Vol 10 Kappa did not write the legacy Type 4 affect or consume one unit.");

    Assert(world.UpdatePosition(1, 300, 400), "Movement-consumable position fixture was not updated.");
    var starGemRequest = new UseItemRequest(1, 18, 1, 0, 20, 30, 4);
    var starGemResult = world.TryApplyMovementConsumable(1, starGemRequest, out var starGemOutcome);
    Assert(starGemResult == LegacyUseItemResult.Accepted && starGemOutcome is { SavedWarp: true, Moved: false, PositionX: 300, PositionY: 400, SourceItem.Value1: 1 } && BinaryPrimitives.ReadInt16LittleEndian(starGemOutcome.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobSavedPositionXOffset)) == 300 && BinaryPrimitives.ReadInt16LittleEndian(starGemOutcome.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobSavedPositionYOffset)) == 400, "Vol 12 Star Gem did not save the authoritative world position or consume one unit.");

    Assert(world.UpdatePosition(1, 500, 600), "Portal position fixture was not updated.");
    var portalRequest = new UseItemRequest(1, 19, 1, 0, 20, 30, 4);
    var portalResult = world.TryApplyMovementConsumable(1, portalRequest, out var portalOutcome);
    Assert(portalResult == LegacyUseItemResult.Accepted && portalOutcome is { Moved: true, PositionX: 300, PositionY: 400, SourceItem.Value1: 1 }, "Vol 13 Portal did not teleport to the saved Star Gem position or consume one unit.");

    var recallRequest = new UseItemRequest(1, 17, 1, 0, 20, 30, 4);
    var recallResult = world.TryApplyMovementConsumable(1, recallRequest, out var recallOutcome, cityRandomX: 2, cityRandomY: 2, newbieRandomX: 2, newbieRandomY: 2);
    Assert(recallResult == LegacyUseItemResult.Accepted && recallOutcome is { Moved: true, PositionX: 2100, PositionY: 2100, SourceItem.Value1: 1 }, "Vol 11 Return did not reproduce the deterministic legacy newbie recall position or consume one unit.");

    var catalystRequest = new UseItemRequest(1, 20, 0, 14, 20, 30, 4);
    var catalystResult = world.TryApplyMountCatalyst(1, catalystRequest, out var catalystOutcome, roll: static _ => 3);
    Assert(catalystResult == LegacyUseItemResult.Accepted && catalystOutcome is { DestinationSlot: 14, Restored: false, SourceItem.Value1: 1, DestinationItem.Index: 2363, DestinationItem.Effect2: 0, DestinationItem.Value2: 17, DestinationItem.Value3: 0 }, "Vol 94 catalyst did not convert the matching mount, roll growth, and consume one unit.");

    var restorerRequest = new UseItemRequest(1, 21, 0, 14, 20, 30, 4);
    var restorerResult = world.TryApplyMountCatalyst(1, restorerRequest, out var restorerOutcome, roll: static _ => 0);
    Assert(restorerResult == LegacyUseItemResult.Accepted && restorerOutcome is { DestinationSlot: 14, Restored: true, SourceItem.Value1: 1, DestinationItem.Index: 2363, DestinationItem.Value2: 18 }, "Vol 93 restorer did not increment the matching adult mount durability or consume one unit.");

    var coinRequest = new UseItemRequest(1, 22, 1, 0, 20, 30, 4);
    var coinResult = world.TryApplyCoinConsumable(1, coinRequest, out var coinOutcome);
    Assert(coinResult == LegacyUseItemResult.Accepted && coinOutcome is { AddedCoin: 100_000_000, Coin: 100_000_000, SourceItem.Value1: 1 }, "Vol 185 silver bar did not add the legacy coin value or consume one unit.");

    var donateRequest = new UseItemRequest(1, 25, 1, 0, 20, 30, 4);
    var donateResult = world.TryApplyDonateConsumable(1, donateRequest, out var donateOutcome);
    Assert(donateResult == LegacyUseItemResult.Accepted && donateOutcome is { AddedDonate: 50, Donate: 50, SourceItem.Value1: 1 }, "Vol 184 Donate voucher did not credit the configured EF_DONATE value or consume one unit.");
    Assert(world.TryRollbackDonateConsumable(1, donateOutcome!), "Vol 184 Donate rollback was not accepted.");
    Assert(world.TryGetDonateBalance(1, out var restoredDonate) && restoredDonate == 0, "Vol 184 Donate rollback did not restore the previous balance.");

    var pvpRubyRequest = new UseItemRequest(1, 23, 1, 0, 20, 30, 4);
    var pvpRubyResult = world.TryApplyPvpJewelry(1, pvpRubyRequest, out var pvpRubyOutcome);
    Assert(pvpRubyResult == LegacyUseItemResult.Accepted && pvpRubyOutcome is { AffectSlot: 1, AffectLevel: 1, AffectTime: 450, SourceItem.Value1: 1 } && pvpRubyOutcome.AffectSnapshot[8] == 8 && BinaryPrimitives.ReadUInt16LittleEndian(pvpRubyOutcome.AffectSnapshot.AsSpan(10)) == 1 && BinaryPrimitives.ReadUInt32LittleEndian(pvpRubyOutcome.AffectSnapshot.AsSpan(12)) == 450, "Vol 242 first PvP jewel did not create the Type 8 bitmask affect or consume one unit.");
    var pvpScore = codec.Decode(new UpdateScoreConfirmation(pvpRubyOutcome!.MobSnapshot, pvpRubyOutcome.AffectSnapshot).ToFrame(codec, 33, 16, 1));
    Assert(pvpScore.Header.Type == UpdateScoreConfirmation.MessageType && pvpScore.Header.Size == UpdateScoreConfirmation.PacketSize, "Vol 242 PvP jewel did not produce the legacy score/affect wire frame.");

    var pvpSapphireRequest = new UseItemRequest(1, 24, 1, 0, 20, 30, 4);
    var pvpSapphireResult = world.TryApplyPvpJewelry(1, pvpSapphireRequest, out var pvpSapphireOutcome);
    Assert(pvpSapphireResult == LegacyUseItemResult.Accepted && pvpSapphireOutcome is { AffectSlot: 1, AffectLevel: 3, AffectTime: 450, SourceItem.Value1: 1 } && BinaryPrimitives.ReadUInt16LittleEndian(pvpSapphireOutcome.AffectSnapshot.AsSpan(10)) == 3, "Vol 242 second PvP jewel did not reuse Type 8 and OR its bit into the existing mask.");
}

static LegacyItemDataTable CreateCombatItemDataTable()
{
    var decodedBody = new byte[LegacyItemDataTable.BodySizeInBytes];
    WriteItemListRecord(decodedBody, 1, "FIRST", unique: 45, position: 64, grade: 2, (LegacyItemEffect.Damage, 100), (LegacyItemEffect.Magic, 30));
    WriteItemListExtendedFields(decodedBody, 1, 321, 654, 987, 123456, 17, 0x12345678, 3, 4, 5, 6, 11, 22, 33, 44);
    WriteItemListRecord(decodedBody, 2, "SECOND", unique: 48, position: 192, grade: 3, (LegacyItemEffect.Damage, 40));
    WriteItemListRecord(decodedBody, 3, "ALT", unique: 48, position: 32, grade: 1, (LegacyItemEffect.Damage2, 7));
    WriteItemListRecord(decodedBody, 100, "WIDE POS", unique: 0, position: 0x12345678, grade: 0);
    WriteItemListRecord(decodedBody, 4, "PARRY", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Parry, 100));
    WriteItemListRecord(decodedBody, 5, "XP GRADE", unique: 0, position: 0, grade: 7);
    WriteItemListRecord(decodedBody, 6, "BOOT CAP", unique: 0, position: 32, grade: 0, (LegacyItemEffect.Damage, 100));
    WriteItemListRecord(decodedBody, 14, "FORCE GEM", unique: 0, position: 0, grade: 6);
    WriteItemListRecord(decodedBody, 15, "PVP ATTACK", unique: 0, position: 0, grade: 0, (LegacyItemEffect.PvpAttack, 50));
    WriteItemListRecord(decodedBody, 16, "REFLECT", unique: 0, position: 0, grade: 8, (LegacyItemEffect.PvpDefense, 50));
    WriteItemListRecord(decodedBody, 17, "REFLECT GEM", unique: 0, position: 0, grade: 8);
    WriteItemListRecord(decodedBody, 419, "BOSS DROP", unique: 0, position: 0, grade: 0);
    WriteItemListRecord(decodedBody, 7, "RESET ARMOR", unique: 0, position: 4, grade: 0, (LegacyItemEffect.MobType, 0), (LegacyItemEffect.Class, 1));
    WriteItemListRequiredLevel(decodedBody, 7, 1);
    WriteItemListRecord(decodedBody, 4016, "CLASS RESET 1", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Volatile, 190));
    WriteItemListRecord(decodedBody, 4017, "HP MP POTION", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Volatile, 1), (LegacyItemEffect.Hp, 100), (LegacyItemEffect.Mp, 30));
    WriteItemListRecord(decodedBody, 4018, "PO VOL 4", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Volatile, 4));
    WriteItemListRecord(decodedBody, 4019, "PL VOL 5", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Volatile, 5));
    WriteItemListRecord(decodedBody, 4021, "ORC PILL", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Volatile, 6));
    WriteItemListRecord(decodedBody, 4022, "FAIRY DUST", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Volatile, 7));
    WriteItemListRecord(decodedBody, 4023, "CRESCENT EYE", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Volatile, 8));
    WriteItemListRecord(decodedBody, 787, "KAPPA", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Volatile, 10));
    WriteItemListRecord(decodedBody, 4024, "RETURN", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Volatile, 11));
    WriteItemListRecord(decodedBody, 4025, "STAR GEM", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Volatile, 12));
    WriteItemListRecord(decodedBody, 4026, "PORTAL", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Volatile, 13));
    WriteItemListRecord(decodedBody, 3344, "MOUNT CATALYST", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Volatile, 94));
    WriteItemListRecord(decodedBody, 3351, "MOUNT RESTORER", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Volatile, 93));
    WriteItemListRecord(decodedBody, 4010, "SILVER BAR", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Volatile, 185));
    WriteItemListRecord(decodedBody, 184, "DONATE VOUCHER", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Volatile, 184), (LegacyItemEffect.Donate, 50));
    WriteItemListRecord(decodedBody, 3200, "PVP RUBY", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Volatile, 242));
    WriteItemListRecord(decodedBody, 3201, "PVP SAPPHIRE", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Volatile, 242));
    WriteItemListRecord(decodedBody, 8, "SEALED ARMOR", unique: 0, position: 4, grade: 0, (LegacyItemEffect.MobType, 5));
    WriteItemListRecord(decodedBody, 10, "EARRING", unique: 0, position: 256, grade: 0);
    WriteItemListRecord(decodedBody, 11, "CELESTIAL ARMOR", unique: 0, position: 4, grade: 0, (LegacyItemEffect.MobType, 3));
    WriteItemListRecord(decodedBody, 575, "ADAMANTITA", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Volatile, 9));
    WriteItemListRecord(decodedBody, 12, "LEGENDARY BASE", unique: 5, position: 4, grade: 2);
    WriteItemListRecord(decodedBody, 13, "LEGENDARY RESULT", unique: 0, position: 4, grade: 2);
    WriteItemListExtra(decodedBody, 12, 13);
    WriteItemListRecord(decodedBody, 500, "DROP", unique: 0, position: 0, grade: 0, (61, 4));

    var encoded = new byte[LegacyItemDataTable.FileSizeInBytes];
    for (var index = 0; index < decodedBody.Length; index++)
        encoded[index] = (byte)(decodedBody[index] ^ 0x5A);
    BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(LegacyItemDataTable.BodySizeInBytes), 123456789);
    return LegacyItemDataTable.Load(encoded);
}

static void WriteItemListRecord(byte[] body, int index, string name, short unique, int position, short grade, params (int Effect, int Value)[] effects)
{
    var offset = index * LegacyItemDataTable.RecordSizeInBytes;
    System.Text.Encoding.ASCII.GetBytes(name).CopyTo(body, offset);
    BinaryPrimitives.WriteInt16LittleEndian(body.AsSpan(offset + 132), unique);
    BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(offset + 136), position);
    BinaryPrimitives.WriteInt16LittleEndian(body.AsSpan(offset + 142), grade);
    for (var effectIndex = 0; effectIndex < effects.Length; effectIndex++)
    {
        BinaryPrimitives.WriteInt16LittleEndian(body.AsSpan(offset + 80 + (effectIndex * 4)), checked((short)effects[effectIndex].Effect));
        BinaryPrimitives.WriteInt16LittleEndian(body.AsSpan(offset + 82 + (effectIndex * 4)), checked((short)effects[effectIndex].Value));
    }
}

static void WriteItemListRequiredLevel(byte[] body, int index, short requiredLevel)
{
    BinaryPrimitives.WriteInt16LittleEndian(body.AsSpan(index * LegacyItemDataTable.RecordSizeInBytes + 70), requiredLevel);
}

static void WriteItemListExtendedFields(byte[] body, int index, short meshIndex, short textureIndex, short visualEffectIndex, int price, short unknown1, int unknown2, short itemType, short itemData, short unknown3, short unknown4, ushort value0, ushort value1, ushort value2, ushort value3)
{
    var record = body.AsSpan(index * LegacyItemDataTable.RecordSizeInBytes);
    BinaryPrimitives.WriteInt16LittleEndian(record[64..], meshIndex);
    BinaryPrimitives.WriteInt16LittleEndian(record[66..], textureIndex);
    BinaryPrimitives.WriteInt16LittleEndian(record[68..], visualEffectIndex);
    BinaryPrimitives.WriteInt32LittleEndian(record[128..], price);
    BinaryPrimitives.WriteInt16LittleEndian(record[134..], unknown1);
    BinaryPrimitives.WriteInt32LittleEndian(record[144..], unknown2);
    BinaryPrimitives.WriteInt16LittleEndian(record[148..], itemType);
    BinaryPrimitives.WriteInt16LittleEndian(record[150..], itemData);
    BinaryPrimitives.WriteInt16LittleEndian(record[152..], unknown3);
    BinaryPrimitives.WriteInt16LittleEndian(record[154..], unknown4);
    BinaryPrimitives.WriteUInt16LittleEndian(record[156..], value0);
    BinaryPrimitives.WriteUInt16LittleEndian(record[158..], value1);
    BinaryPrimitives.WriteUInt16LittleEndian(record[160..], value2);
    BinaryPrimitives.WriteUInt16LittleEndian(record[162..], value3);
}

static void WriteItemListRequirements(byte[] body, int index, short level = 0, short strength = 0, short intelligence = 0, short dexterity = 0, short constitution = 0)
{
    var record = body.AsSpan(index * LegacyItemDataTable.RecordSizeInBytes);
    BinaryPrimitives.WriteInt16LittleEndian(record[70..], level);
    BinaryPrimitives.WriteInt16LittleEndian(record[72..], strength);
    BinaryPrimitives.WriteInt16LittleEndian(record[74..], intelligence);
    BinaryPrimitives.WriteInt16LittleEndian(record[76..], dexterity);
    BinaryPrimitives.WriteInt16LittleEndian(record[78..], constitution);
}

static LegacyItemDataTable LoadLegacyItemDataTable(byte[] decodedBody)
{
    if (decodedBody.Length != LegacyItemDataTable.BodySizeInBytes)
        throw new ArgumentException("Decoded ItemList fixture has the wrong size.", nameof(decodedBody));

    var encoded = new byte[LegacyItemDataTable.FileSizeInBytes];
    for (var offset = 0; offset < decodedBody.Length; offset++)
        encoded[offset] = (byte)(decodedBody[offset] ^ 0x5A);
    BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(LegacyItemDataTable.BodySizeInBytes), 123456789);
    return LegacyItemDataTable.Load(encoded);
}

static void WriteItemListExtra(byte[] body, int index, short extra)
{
    BinaryPrimitives.WriteInt16LittleEndian(body.AsSpan(index * LegacyItemDataTable.RecordSizeInBytes + 140), extra);
}

static void WorldPhysicalAttack()
{
    var attackerMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var targetMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var attackerScore = new LegacyScore(100, 0, 100, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 40, 0, 0, 0);
    var targetScore = new LegacyScore(100, 10, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0);
    attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    targetScore.Write(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));

    var hub = new WorldHub();
    Assert(hub.Enter(1, "ATTACKER", (_, _) => ValueTask.CompletedTask) && hub.Enter(2, "TARGET", (_, _) => ValueTask.CompletedTask), "Physical-combat participants were not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob) && hub.SetCharacterState(2, 0, 0, 2, 0, 0, 2005, 2005, targetMob), "Physical-combat participant state was not set.");

    var result = hub.TryApplyPhysicalAttack(1, 2, randomFactor: 99, out var outcome);
    Assert(result == LegacyPhysicalAttackResult.Accepted && outcome is not null && outcome.Damage == 21 && outcome.RemainingHp == 79, "Physical target damage did not match the legacy player penetration path.");
    Assert(hub.TryGetCombatState(2, out var targetState) && targetState!.CurrentScore.Hp == 79, "Physical attack did not mutate target HP atomically.");
    Assert(hub.TryGetResourceState(2, out var targetResources, out var targetRequestedHp, out _) && targetResources!.CurrentScore.Hp == 79 && targetRequestedHp == 79 && outcome!.TargetHpChanged, "Physical attack did not expose the authoritative target HP/ReqHp update for SendSetHpMp.");

    var absorptionAffect = new byte[256];
    absorptionAffect[0] = 8;
    BinaryPrimitives.WriteUInt16LittleEndian(absorptionAffect.AsSpan(2), 1 << 3);
    var absorptionAttackerMob = attackerMob.ToArray();
    (attackerScore with { Hp = 50 })
        .Write(absorptionAttackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    var absorptionHub = new WorldHub();
    Assert(absorptionHub.Enter(1, "ABSORBER", (_, _) => ValueTask.CompletedTask) && absorptionHub.Enter(2, "ABSORB_TARGET", (_, _) => ValueTask.CompletedTask), "HP-absorption participants were not registered.");
    Assert(absorptionHub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, absorptionAttackerMob, LegacyAccountSnapshot.ClassMasterMortal, absorptionAffect) && absorptionHub.SetCharacterState(2, 0, 0, 2, 0, 0, 2005, 2005, targetMob), "HP-absorption fixture was not set.");
    var absorptionResult = absorptionHub.TryApplyPhysicalAttack(1, 2, randomFactor: 99, out var absorptionOutcome, absorptionRoll: 0);
    Assert(absorptionResult == LegacyPhysicalAttackResult.Accepted && absorptionOutcome is not null && absorptionOutcome.Damage == 21 && absorptionOutcome.AttackerHpAbsorbed == 4 && absorptionOutcome.AttackerRequestedHp == 4, $"HpAbs did not preserve the legacy Type 8 bit 3 calculation and ReqHp assignment: result={absorptionResult}, damage={absorptionOutcome?.Damage}, absorbed={absorptionOutcome?.AttackerHpAbsorbed}, requested={absorptionOutcome?.AttackerRequestedHp}.");
    Assert(absorptionHub.TryGetCombatState(1, out var absorptionState) && absorptionState!.CurrentScore.Hp == 50, "HpAbs incorrectly mutated the attacker's authoritative MOB HP instead of only ReqHp.");

    var modifierAttackerMob = attackerMob.ToArray();
    new LegacyItem(14, 116, 231, 0, 0, 0, 0).Write(modifierAttackerMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset));
    new LegacyItem(15, 0, 0, 0, 0, 0, 0).Write(modifierAttackerMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + LegacyItem.SizeInBytes));
    var modifierHub = new WorldHub();
    Assert(modifierHub.Enter(1, "MODIFIER", (_, _) => ValueTask.CompletedTask) && modifierHub.Enter(2, "MODIFIER_TARGET", (_, _) => ValueTask.CompletedTask), "Physical modifier participants were not registered.");
    Assert(modifierHub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, modifierAttackerMob) && modifierHub.SetCharacterState(2, 0, 0, 2, 0, 0, 2005, 2005, targetMob), "Physical modifier fixture was not set.");
    var modifierResult = modifierHub.TryApplyPhysicalAttack(1, 2, randomFactor: 99, out var modifierOutcome, parryRandomRoll: 1000, itemData: CreateCombatItemDataTable(), absorptionRoll: 1);
    Assert(modifierResult == LegacyPhysicalAttackResult.Accepted && modifierOutcome is not null && modifierOutcome.Damage == 106 && modifierOutcome.ForceDamage == 80 && modifierOutcome.PvpDamage == 5, $"ForceDamage/PvPDamage did not preserve the legacy post-penetration integer order: result={modifierResult}, damage={modifierOutcome?.Damage}, force={modifierOutcome?.ForceDamage}, pvp={modifierOutcome?.PvpDamage}.");

    using var secondarySkillReader = new StringReader("36,72,1,35,5,6,3,200,0,0,1,2,1,6.0.0.7.0.0.0.0,6.0.0.7.0.0.0.0,2,0,1,1,0,2,0,1,Nevasca\n40,24,1,9,2,6,4,20,20,10,0,0,1,10.0.0.9.0.0.0.0,10.0.0.9.0.0.0.0,4,0,1,1,0,0,0,1,Névoa_Venenosa");
    var secondarySkills = LegacySkillDataTable.Load(secondarySkillReader);
    var secondaryAttackerMob = attackerMob.ToArray();
    (attackerScore with { Special2 = 40 }).Write(secondaryAttackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    BinaryPrimitives.WriteUInt16LittleEndian(secondaryAttackerMob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), 0x03);
    var secondaryHub = new WorldHub(skillData: secondarySkills);
    Assert(secondaryHub.Enter(1, "SECONDARY_ATTACKER", (_, _) => ValueTask.CompletedTask) && secondaryHub.Enter(2, "SECONDARY_TARGET", (_, _) => ValueTask.CompletedTask), "Secondary-effect participants were not registered.");
    Assert(secondaryHub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, secondaryAttackerMob) && secondaryHub.SetCharacterState(2, 0, 0, 2, 0, 0, 2005, 2005, targetMob), "Secondary-effect fixture was not set.");
    var secondaryResult = secondaryHub.TryApplyPhysicalAttack(1, 2, randomFactor: 99, out var secondaryOutcome, parryRandomRoll: 1000, frostRoll: 0, drainRoll: 0);
    var secondaryAffect = secondaryOutcome?.TargetAffectSnapshot ?? [];
    Assert(secondaryResult == LegacyPhysicalAttackResult.Accepted && secondaryOutcome is not null && secondaryOutcome.Damage > 0 && secondaryAffect.Length == LegacyAccountSnapshot.AffectStride && secondaryAffect[0] == 1 && secondaryAffect[1] == 2 && BinaryPrimitives.ReadUInt16LittleEndian(secondaryAffect.AsSpan(2)) == 40 && BinaryPrimitives.ReadUInt32LittleEndian(secondaryAffect.AsSpan(4)) == 1 && secondaryAffect[8] == 20 && secondaryAffect[9] == 10, "RSV_FROST/RSV_DRAIN did not apply the legacy skill 36/40 affect values, level, and duration to the target.");

    var reflectingTargetMob = targetMob.ToArray();
    reflectingTargetMob[LegacyAccountSnapshot.MobClassOffset] = 2;
    BinaryPrimitives.WriteUInt32LittleEndian(reflectingTargetMob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), 1u << 17);
    (targetScore with { Special4 = 59 }).Write(reflectingTargetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    new LegacyItem(16, 0, 0, 0, 0, 0, 0).Write(reflectingTargetMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset));
    new LegacyItem(17, 116, 245, 0, 0, 0, 0).Write(reflectingTargetMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + LegacyItem.SizeInBytes));
    var reflectingHub = new WorldHub();
    Assert(reflectingHub.Enter(1, "REFLECT_ATTACKER", (_, _) => ValueTask.CompletedTask) && reflectingHub.Enter(2, "REFLECT_TARGET", (_, _) => ValueTask.CompletedTask), "Reflection participants were not registered.");
    Assert(reflectingHub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob) && reflectingHub.SetCharacterState(2, 0, 0, 2, 0, 0, 2005, 2005, reflectingTargetMob), "Reflection fixture was not set.");
    var reflectionTable = CreateCombatItemDataTable();
    var reflectionResult = reflectingHub.TryApplyPhysicalAttack(1, 2, randomFactor: 99, out var reflectionOutcome, parryRandomRoll: 1000, itemData: reflectionTable, absorptionRoll: 1);
    Assert(reflectionResult == LegacyPhysicalAttackResult.Accepted && reflectionOutcome is not null && reflectionOutcome.Damage == 1 && reflectionOutcome.RemainingHp == 99 && reflectionOutcome.ReflectDamage == 370 && reflectionOutcome.ReflectPvp == 5, $"ReflectDamage/ReflectPvP did not preserve the legacy class, grade, gem/sanctuary, minimum-one, and integer percentage order: result={reflectionResult}, damage={reflectionOutcome?.Damage}, hp={reflectionOutcome?.RemainingHp}, reflect={reflectionOutcome?.ReflectDamage}, pvp={reflectionOutcome?.ReflectPvp}.");

    var adultMountCatalog = LegacySummonCatalog.Load(Path.Combine(FindReference759TmsrvRun(), "BaseSummon"), LegacySummonCatalog.HistoricalLoadedSlotCount);
    var mountedTargetMob = targetMob.ToArray();
    var adultMountOffset = LegacyAccountSnapshot.MobEquipmentOffset + (14 * LegacyItem.SizeInBytes);
    new LegacyItem(2360, 0, 0, 0, 0, 4, 0).Write(mountedTargetMob.AsSpan(adultMountOffset));
    BinaryPrimitives.WriteInt16LittleEndian(mountedTargetMob.AsSpan(adultMountOffset + 2), 100);
    var adultMountHub = new WorldHub(summonCatalog: adultMountCatalog);
    Assert(adultMountHub.Enter(1, "ADULT_MOUNT_ATTACKER", (_, _) => ValueTask.CompletedTask) && adultMountHub.Enter(2, "ADULT_MOUNT_TARGET", (_, _) => ValueTask.CompletedTask), "Adult-mount participants were not registered.");
    Assert(adultMountHub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob) && adultMountHub.SetCharacterState(2, 0, 0, 2, 0, 0, 2005, 2005, mountedTargetMob), "Adult-mount fixture was not set.");
    var adultMountResult = adultMountHub.TryApplyPhysicalAttack(1, 2, randomFactor: 99, out var adultMountOutcome, parryRandomRoll: 1000);
    var adultMountHp = adultMountOutcome is null ? -1 : BinaryPrimitives.ReadInt16LittleEndian(adultMountOutcome.TargetMobSnapshot.AsSpan(adultMountOffset + 2));
    Assert(adultMountResult == LegacyPhysicalAttackResult.Accepted && adultMountOutcome is not null && adultMountOutcome.Damage == 15 && adultMountOutcome.RemainingHp == 85 && adultMountOutcome.MountDurabilityLoss == 3 && adultMountOutcome.MountOwnerConnectionId == 2 && adultMountHp == 97 && adultMountOutcome.UpdatedMountItem is { Effect1: 97, Value1: 0, Effect3: 4 }, $"ProcessAdultMount did not preserve the 75/25 player split and half-loss durability update: result={adultMountResult}, damage={adultMountOutcome?.Damage}, hp={adultMountOutcome?.RemainingHp}, mountLoss={adultMountOutcome?.MountDurabilityLoss}, mountHp={adultMountHp}.");

    var scrollTargetMob = new byte[LegacyAccountSnapshot.CharacterStride];
    (targetScore with { Hp = 1 }).Write(scrollTargetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    var scrollSlot = 7;
    new LegacyItem(3463, 61, 2, 0, 0, 0, 0).Write(scrollTargetMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (scrollSlot * LegacyItem.SizeInBytes)));
    var scrollHub = new WorldHub();
    Assert(scrollHub.Enter(1, "ATTACKER", (_, _) => ValueTask.CompletedTask) && scrollHub.Enter(2, "SCROLL_TARGET", (_, _) => ValueTask.CompletedTask), "Resurrection-scroll participants were not registered.");
    Assert(scrollHub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob) && scrollHub.SetCharacterState(2, 0, 0, 2, 0, 0, 2005, 2005, scrollTargetMob), "Resurrection-scroll fixture was not set.");
    var scrollResult = scrollHub.TryApplyPhysicalAttack(1, 2, randomFactor: 99, out var scrollOutcome);
    Assert(scrollResult == LegacyPhysicalAttackResult.Accepted && scrollOutcome is not null && scrollOutcome.TargetRevived && !scrollOutcome.TargetDied && scrollOutcome.RemainingHp == 100 && scrollOutcome.ConsumedItemSlot == scrollSlot && scrollOutcome.ConsumedItem is { Index: 3463, Effect1: 61, Value1: 1 }, "A lethal attack did not consume one unit of the legacy resurrection scroll and restore maximum HP.");
    Assert(scrollHub.TryGetResourceState(2, out var scrollState, out _, out var scrollRequestedMp) && scrollState!.CurrentScore.Hp == 100 && scrollState.CurrentMana == 100 && scrollRequestedMp == 100, "Resurrection scroll did not restore authoritative HP/MP resources.");
    Assert(LegacyItem.Read(scrollOutcome!.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (scrollSlot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes)) is { Index: 3463, Effect1: 61, Value1: 1 }, "Resurrection scroll stack amount was not decremented in the carry slot.");

    var deadAttackerMob = attackerMob.ToArray();
    BinaryPrimitives.WriteInt32LittleEndian(deadAttackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), 0);
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, deadAttackerMob), "Dead-attacker fixture could not be set.");
    Assert(hub.TryApplyPhysicalAttack(1, 2, 99, out _) == LegacyPhysicalAttackResult.AttackerNotAlive, "A dead attacker was allowed to resolve physical damage.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob), "Physical attacker fixture could not be restored after the dead-attacker case.");

    targetMob[LegacyAccountSnapshot.MobEquipmentOffset + LegacyItem.SizeInBytes] = 4;
    Assert(hub.SetCharacterState(2, 0, 0, 2, 0, 0, 2005, 2005, targetMob), "Parry target fixture could not be equipped.");
    var parryResult = hub.TryApplyPhysicalAttack(1, 2, randomFactor: 99, out var parryOutcome, parryRandomRoll: 50, itemData: CreateCombatItemDataTable());
    Assert(parryResult == LegacyPhysicalAttackResult.Accepted && parryOutcome is not null && parryOutcome.Damage == -3 && parryOutcome.RemainingHp == 100, "Physical parry did not emit -3 while preserving HP.");
    BinaryPrimitives.WriteUInt16LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), 0x200);
    Assert(hub.SetCharacterState(2, 0, 0, 2, 0, 0, 2005, 2005, targetMob), "Special-parry target fixture could not be reset.");
    var specialParryResult = hub.TryApplyPhysicalAttack(1, 2, randomFactor: 99, out var specialParryOutcome, parryRandomRoll: 50, itemData: CreateCombatItemDataTable());
    Assert(specialParryResult == LegacyPhysicalAttackResult.Accepted && specialParryOutcome is not null && specialParryOutcome.Damage == -4 && specialParryOutcome.RemainingHp == 100, "Rsv 0x200 parry did not emit the legacy -4 code.");

    Assert(hub.SetCombatEligibilityState(1, pkMode: false, guilty: false, mapAttribute: 0x40) && hub.SetCombatEligibilityState(2, pkMode: false, guilty: false, mapAttribute: 0), "Peaceful-map eligibility fixture was not set.");
    Assert(hub.SetCharacterState(2, 0, 0, 2, 0, 0, 2005, 2005, targetMob), "Peaceful-map target fixture could not be reset.");
    Assert(hub.TryApplyPhysicalAttack(1, 2, 99, out _) == LegacyPhysicalAttackResult.PeacefulZoneBlocked, "Physical damage was accepted against a non-PK, non-guilty target in a peaceful map.");
    Assert(hub.SetCombatEligibilityState(2, pkMode: true, guilty: false, mapAttribute: 0) && hub.TryApplyPhysicalAttack(1, 2, 99, out var pkOutcome) == LegacyPhysicalAttackResult.Accepted && pkOutcome is not null, "PK-mode target was incorrectly blocked by the peaceful-map gate.");

    var killMarkOffset = LegacyAccountSnapshot.MobCarryOffset + (LegacyAccountSnapshot.LegacyKillMarkCarrySlot * LegacyItem.SizeInBytes);
    var lowPkMob = attackerMob.ToArray();
    var highPkTargetMob = targetMob.ToArray();
    highPkTargetMob[killMarkOffset + 2] = 11;
    var crimeHub = new WorldHub();
    Assert(crimeHub.Enter(1, "LOW_PK", (_, _) => ValueTask.CompletedTask) && crimeHub.Enter(2, "HIGH_PK", (_, _) => ValueTask.CompletedTask), "PK-point participants were not registered.");
    Assert(crimeHub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, lowPkMob) && crimeHub.SetCharacterState(2, 0, 0, 2, 0, 0, 2005, 2005, highPkTargetMob), "PK-point fixture was not set.");
    Assert(crimeHub.SetCombatEligibilityState(1, pkMode: false, guilty: false, mapAttribute: 0x40) && crimeHub.SetCombatEligibilityState(2, pkMode: true, guilty: false, mapAttribute: 0), "PK-point protected-map fixture was not configured.");
    var protectedPkResult = crimeHub.TryApplyPhysicalAttack(1, 2, 99, out var protectedPkOutcome);
    Assert(protectedPkResult == LegacyPhysicalAttackResult.Accepted && protectedPkOutcome is not null && protectedPkOutcome.PkPointGateBlocked && protectedPkOutcome.Damage == 0 && protectedPkOutcome.RemainingHp == 100 && protectedPkOutcome.CrimeStateUpdates is { Count: 0 }, "The legacy PK-point gate did not suppress low-PK damage against a high-PK target.");

    var highPkAttackerMob = attackerMob.ToArray();
    highPkAttackerMob[killMarkOffset + 2] = 11;
    Assert(crimeHub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, highPkAttackerMob) && crimeHub.SetCharacterState(2, 0, 0, 2, 0, 0, 2005, 2005, highPkTargetMob), "PK-point damage fixture could not be reset.");
    Assert(crimeHub.SetCombatEligibilityState(1, pkMode: false, guilty: false, mapAttribute: 0x40) && crimeHub.SetCombatEligibilityState(2, pkMode: true, guilty: false, mapAttribute: 0), "PK-point damage eligibility fixture could not be reset.");
    var guiltyPkResult = crimeHub.TryApplyPhysicalAttack(1, 2, 99, out var guiltyPkOutcome, parryRandomRoll: 1000);
    var guiltyUpdates = guiltyPkOutcome?.CrimeStateUpdates ?? [];
    Assert(guiltyPkResult == LegacyPhysicalAttackResult.Accepted && guiltyPkOutcome is not null && !guiltyPkOutcome.PkPointGateBlocked && guiltyPkOutcome.Damage > 0 && guiltyUpdates.Count == 2 && guiltyUpdates.All(update => update.MobSnapshot[killMarkOffset + 4] == 8), "A permitted high-PK attack did not apply guilty=8 to both players and expose the visual updates.");

    Assert(hub.SetCharacterState(1, 0, 7, 1, 0, 0, 2000, 2000, attackerMob) && hub.SetCharacterState(2, 0, 7, 2, 0, 0, 2005, 2005, targetMob), "Friendly-fire fixture could not be reset.");
    Assert(hub.TryApplyPhysicalAttack(1, 2, 99, out _) == LegacyPhysicalAttackResult.FriendlyFireBlocked, "Same-guild physical damage was accepted.");
    Assert(hub.SetCharacterState(2, 0, 0, 2, 0, 0, 2100, 2100, targetMob), "Out-of-range fixture could not be reset.");
    Assert(hub.TryApplyPhysicalAttack(1, 2, 99, out _) == LegacyPhysicalAttackResult.OutOfRange, "Out-of-range physical damage was accepted.");
}

static void WorldNpcExperience()
{
    var attackerMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var npcMob = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(50, 0, 100, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 40, 0, 0, 0)
        .Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    new LegacyScore(50, 10, 0, 0, 0, 0, 0, 100, 100, 1, 100, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    npcMob[LegacyAccountSnapshot.MobClanOffset] = 1;
    BinaryPrimitives.WriteInt64LittleEndian(npcMob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset), 1_000);

    var hub = new WorldHub();
    Assert(hub.Enter(1, "NPC_HUNTER", (_, _) => ValueTask.CompletedTask), "NPC-experience attacker was not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob), "NPC-experience attacker state was not set.");
    var npcId = hub.EnterNpc(npcMob, 2005, 2005, requestedConnectionId: 20_000);
    Assert(npcId == 20_000, "Persistent NPC did not retain its requested connection id.");

    var result = hub.TryApplyPhysicalAttack(1, npcId, randomFactor: 99, out var outcome);
    Assert(result == LegacyPhysicalAttackResult.Accepted && outcome is not null && outcome.TargetRemoved && outcome.TargetDied && outcome.RemainingHp == 0, "A lethal physical attack did not remove the persistent NPC.");
    Assert(outcome!.ExperienceAwarded == 1_000 && outcome.AttackerMobSnapshot is not null, "Solo NPC experience was not awarded from the target STRUCT_MOB.");
    Assert(hub.TryGetCombatState(1, out var attackerState) && attackerState!.Experience == 1_000, "NPC experience did not mutate the attacker's authoritative MOB state.");
    Assert(hub.TryApplyPhysicalAttack(1, npcId, 99, out _) == LegacyPhysicalAttackResult.ParticipantNotFound, "A removed NPC remained attackable after death.");
}

static void WorldNpcCommonDrop()
{
    var attackerMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var npcMob = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(50, 0, 100, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 40, 0, 0, 0)
        .Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    new LegacyScore(50, 0, 0, 0, 0, 0, 0, 100, 100, 1, 100, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    npcMob[LegacyAccountSnapshot.MobClanOffset] = 1;
    BinaryPrimitives.WriteInt64LittleEndian(npcMob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset), 1_000);
    new LegacyItem(500, 0, 0, 0, 0, 0, 0).Write(npcMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (11 * LegacyItem.SizeInBytes)));

    var hub = new WorldHub(itemData: CreateCombatItemDataTable());
    Assert(hub.Enter(1, "DROP_HUNTER", (_, _) => ValueTask.CompletedTask), "Drop attacker was not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob), "Drop attacker state was not set.");
    var npcId = hub.EnterNpc(npcMob, 2005, 2005, requestedConnectionId: 20_004);
    var result = hub.TryApplyPhysicalAttack(1, npcId, randomFactor: 99, out var outcome, itemData: CreateCombatItemDataTable());
    Assert(result == LegacyPhysicalAttackResult.Accepted && outcome is not null && outcome.TargetRemoved, "Drop fixture did not kill the NPC.");
    Assert(outcome!.ItemDrops is { Count: 1 } && outcome.ItemDrops[0].InventorySlot == 0 && outcome.ItemDrops[0].Item.Index == 500 && outcome.ItemDrops[0].Item.Effect1 == 61 && outcome.ItemDrops[0].Item.Value1 == 4, "The guaranteed legacy carry slot 11 drop or its static ItemList effect was not inserted into the first free player slot.");
    Assert(hub.TryGetCharacterSnapshot(1, out var updatedMob, out _, out _) && updatedMob is not null && LegacyItem.Read(updatedMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset)).Index == 500, "The NPC drop did not mutate the authoritative player MOB carry.");
}

static void CastleQuestConfiguration()
{
    using var reader = new StringReader("""
        // Castle fixture
        # [ 0 ]
        Mob_Initial: 1559
        Mob_End: 1604
        Boss1: 900
        Boss2: 901
        Prize_0: 700 61 5 62 6 63 7
        CoinPrize: 50
        ExpPrize_Arch: 100
        ExpPrize_Mortal: 200
        ExpPrize_Celestial: 300
        ExpPrize_SubCelestial: 400
        PartyPrize: ON
        QuestTime: 500
        """);
    var definitions = LegacyCastleQuestConfiguration.Load(reader);
    var definition = definitions.Single();
    Assert(definition.MobInitial == 1559 && definition.MobEnd == 1604 && definition.Boss1 == 900 && definition.Boss2 == 901, "Castle quest mob/boss fields were not parsed.");
    Assert(definition.Prizes[0] == new LegacyItem(700, 61, 5, 62, 6, 63, 7) && definition.Prizes.Skip(1).All(static item => item.Index == 0), "Castle quest prize item fields were not parsed.");
    Assert(definition.CoinPrize == 50 && definition.PartyPrize && definition.QuestTime == 500, "Castle quest coin/party/time fields were not parsed.");
    Assert(definition.ExpPrize.SequenceEqual(new[] { 0, 100, 400, 300, 300, 0 }), "Castle quest EXP class mapping did not preserve the legacy SubCelestial overwrite.");

    var path = Path.GetFullPath(Path.Combine(FindReference759TmsrvRun(), "..", "..", "Common", "Settings", "CastleQuest.txt"));
    var releaseDefinition = LegacyCastleQuestConfiguration.Load(path).Single();
    Assert(releaseDefinition.Boss1 == 0 && releaseDefinition.Boss2 == 3 && releaseDefinition.QuestTime == 500, "The local Reference759 CastleQuest.txt was not loaded as the legacy definition.");
}

static void CastleQuestRewards()
{
    static byte[] Mob(string name, byte characterClass, int hp, int damage, int coin, long experience)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        System.Text.Encoding.ASCII.GetBytes(name).CopyTo(mob, LegacyAccountSnapshot.MobNameOffset);
        mob[LegacyAccountSnapshot.MobClassOffset] = characterClass;
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), coin);
        BinaryPrimitives.WriteInt64LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset), experience);
        new LegacyScore(50, 0, damage, 0, 0, 0, 0, hp, 100, hp, 100, 0, 0, 0, 0, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    var prizes = new LegacyItem[LegacyCastleQuestConfiguration.MaxCarry];
    prizes[0] = new LegacyItem(700, 61, 5, 62, 6, 63, 7);
    var definition = new LegacyCastleQuestDefinition(1559, 1604, 900, 901, prizes, 50, new[] { 0, 100, 200, 300, 400, 500 }, true, 500);
    var hub = new WorldHub();
    hub.ConfigureCastleQuests([definition]);
    Assert(hub.Enter(1, "CASTLE_LEADER", (_, _) => ValueTask.CompletedTask) && hub.Enter(2, "CASTLE_MEMBER", (_, _) => ValueTask.CompletedTask), "Castle reward participants were not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 100, 2000, 2000, Mob("CASTLE_LEADER", 1, 100, 200, 100, 10), classMaster: 1), "Castle reward leader state was not set.");
    Assert(hub.SetCharacterState(2, 0, 0, 1, 0, 200, 2005, 2005, Mob("CASTLE_MEMBER", 3, 100, 0, 200, 20), classMaster: 3), "Castle reward member state was not set.");
    Assert(hub.TrySetParty(1, [1, 2]), "Castle reward party was not formed.");

    var npcId = hub.EnterNpc(Mob("CASTLE_BOSS", 0, 1, 0, 0, 0), 2005, 2005, requestedConnectionId: 20_090, generateIndex: 900);
    Assert(hub.TryApplyPhysicalAttack(1, npcId, randomFactor: 99, out var outcome) == LegacyPhysicalAttackResult.Accepted && outcome is not null && outcome.TargetRemoved, "Configured Castle boss was not killed through the physical combat path.");
    Assert(outcome!.ItemDrops is { Count: 2 } && outcome.ItemDrops.All(drop => drop.Item.Index == 700), "Castle quest item prize was not delivered to the leader and party member.");
    Assert(outcome.ExperienceAwards is { Count: 2 } && outcome.ExperienceAwards.Single(award => award.ConnectionId == 1).Experience == 100 && outcome.ExperienceAwards.Single(award => award.ConnectionId == 2).Experience == 300, "Castle quest class-specific EXP was not awarded to the party.");
    Assert(outcome.CoinUpdates is { Count: 2 } && outcome.CoinUpdates.Single(update => update.ConnectionId == 1).Coin == 150 && outcome.CoinUpdates.Single(update => update.ConnectionId == 2).Coin == 250, "Castle quest Gold was not persisted with the 2B rule path.");
    Assert(hub.TryGetCharacterSnapshot(1, out var leaderMob, out _, out _) && leaderMob is not null && BinaryPrimitives.ReadInt64LittleEndian(leaderMob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset)) == 110 && BinaryPrimitives.ReadInt32LittleEndian(leaderMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset)) == 150, "Castle quest reward did not mutate the authoritative leader MOB.");

    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 1_999_999_980, 2000, 2000, Mob("CASTLE_LEADER", 1, 100, 200, 1_999_999_980, 110), classMaster: 1), "Castle 2B-limit leader state was not reset.");
    var cappedNpcId = hub.EnterNpc(Mob("CASTLE_BOSS", 0, 1, 0, 0, 0), 2005, 2005, requestedConnectionId: 20_091, generateIndex: 901);
    Assert(hub.TryApplyPhysicalAttack(1, cappedNpcId, randomFactor: 99, out var cappedOutcome) == LegacyPhysicalAttackResult.Accepted && cappedOutcome is not null && cappedOutcome.TargetRemoved, "Castle 2B-limit boss was not killed.");
    Assert(cappedOutcome!.CoinUpdates is { Count: 1 } && cappedOutcome.CoinUpdates[0].ConnectionId == 2 && cappedOutcome.PrivateNotices!.Any(notice => notice.ConnectionId == 1 && notice.Message.Contains("2 bilhões", StringComparison.Ordinal)), "Castle Gold overflow did not reject only the capped recipient and notify it.");
}

static void NpcGenerationCatalogFlow()
{
    var template = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(50, 0, 0, 0, 0, 0, 0, 321, 100, 1, 100, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(template.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    System.Text.Encoding.ASCII.GetBytes("Rei_Carbuncle").CopyTo(template.AsSpan(LegacyAccountSnapshot.MobNameOffset));

    var catalog = LegacyNpcGenerationCatalog.Parse("""
        // Pista +6 boss
        #   [5767]
        MinuteGenerate: -1
        MaxNumMob: 1
        MinGroup: 0
        MaxGroup: 0
        Leader: Rei_Carbuncle
        Follower: Rei_Carbuncle
        RouteType: 2
        Formation: 0
        StartX: 3432
        StartY: 1501
        StartRange: 2
        """, new Dictionary<string, byte[]> { ["Rei_Carbuncle"] = template });

    Assert(catalog.TryGet(5767, out var definition) && definition is { MaxNumMob: 1, LeaderName: "Rei_Carbuncle", StartX: 3432, StartY: 1501, StartRange: 2 }, "The NPCGener parser did not preserve the Pista +6 boss definition.");
    Assert(catalog.TryCreateLeader(5767, static _ => 0, out var generated) && generated is not null, "The NPC generation catalog did not create the configured leader template.");
    Assert(generated!.PositionX == 3430 && generated.PositionY == 1499, "The NPC generation start-range calculation differs from GenerateMob's legacy lower-bound roll.");
    Assert(System.Text.Encoding.ASCII.GetString(generated.MobSnapshot, LegacyAccountSnapshot.MobNameOffset, 13) == "Rei Carbuncle", "The NPC name normalization did not replace the legacy underscore.");
    Assert(LegacyMobCombatState.Read(generated.MobSnapshot).CurrentScore.Hp == 321 && generated.AffectSnapshot.All(static value => value == 0), "Generated NPC HP or affects were not initialized like GenerateMob.");

    var hub = new WorldHub(npcGenerationCatalog: catalog);
    Assert(hub.TrySpawnGeneratedNpc(5767, out var spawned, static _ => 0) && spawned is not null, "WorldHub did not register the generated NPC.");
    Assert(spawned!.ConnectionId >= 20_000 && spawned.GenerateIndex == 5767 && spawned.PositionX == 3430 && spawned.PositionY == 1499, "WorldHub changed the generated NPC identity or position.");
    Assert(!hub.TrySpawnGeneratedNpc(5767, out _, static _ => 0), "WorldHub exceeded the NPCGener MaxNumMob limit for the generated boss.");
}

static void PistaMobLeftWireAndArea()
{
    var hub = new WorldHub();
    var mob = new byte[LegacyAccountSnapshot.CharacterStride];
    Assert(hub.Enter(1, "PISTA_COUNTER", (_, _) => ValueTask.CompletedTask), "Pista counter participant was not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 3330, 1475, mob), "Pista counter participant state was not set.");
    Assert(hub.ConfigurePistaLv6State(1, 7), "Pista counter state was not configured.");

    var codec = LegacyFrameCodec.CreateDefault();
    Assert(hub.TryBuildPistaLv6MobLeftFrame(1, codec, 33, 16, out var rawFrame) && rawFrame is not null, "The Pista counter frame was not built inside the legacy area.");
    var frame = codec.Decode(rawFrame!);
    Assert(frame.IsChecksumValid && frame.Header.Type == MobLeftConfirmation.MessageType && frame.Header.Id == MobLeftConfirmation.SceneId && frame.Header.Size == MobLeftConfirmation.PacketSize, "The _MSG_MobLeft header differs from MSG_STANDARDPARM.");
    Assert(BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span) == 7, "The _MSG_MobLeft parameter did not carry the authoritative Pista mob count.");

    Assert(hub.Enter(2, "PISTA_COUNTER_2", (_, _) => ValueTask.CompletedTask), "Second Pista counter participant was not registered.");
    Assert(hub.SetCharacterState(2, 0, 0, 1, 0, 0, 3331, 1476, mob), "Second Pista counter participant state was not set.");
    var firstMobLeftAt = new DateTime(2026, 9, 16, 12, 0, 0);
    Assert(hub.TryProcessPistaLv6MobLeft(firstMobLeftAt, codec, 33, 16, out var mobLeftFrames) && mobLeftFrames is { Count: 2 }, "The periodic Pista counter did not target both players inside the area.");
    Assert(mobLeftFrames![0].ConnectionId == 1 && mobLeftFrames[1].ConnectionId == 2, "The periodic Pista counter targeted the wrong connections.");
    Assert(BinaryPrimitives.ReadInt32LittleEndian(codec.Decode(mobLeftFrames[0].Frame).Payload.Span) == 7, "The periodic Pista counter did not carry the authoritative count.");
    Assert(!hub.TryProcessPistaLv6MobLeft(firstMobLeftAt.AddMilliseconds(2399), codec, 33, 16, out _), "The periodic Pista counter fired before the legacy 2.4-second cadence.");
    Assert(hub.TryProcessPistaLv6MobLeft(firstMobLeftAt.AddMilliseconds(2400), codec, 33, 16, out var secondMobLeftFrames) && secondMobLeftFrames is { Count: 2 }, "The periodic Pista counter did not fire at the legacy 2.4-second cadence.");

    Assert(hub.UpdatePosition(1, 3448, 1525), "Pista counter participant could not be moved outside the legacy area.");
    Assert(!hub.TryBuildPistaLv6MobLeftFrame(1, codec, 33, 16, out _), "The Pista counter frame crossed the legacy exclusive upper bounds.");

    Assert(hub.ConfigurePistaLv6State(1, 100), "Pista counter state could not be reconfigured for cleanup coverage.");
    var pistaMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var firstPistaNpc = hub.EnterNpc(pistaMob, 3432, 1501, requestedConnectionId: 20_101, generateIndex: 5767);
    var secondPistaNpc = hub.EnterNpc(pistaMob, 3432, 1501, requestedConnectionId: 20_102, generateIndex: 5775);
    var unrelatedNpc = hub.EnterNpc(pistaMob, 3432, 1501, requestedConnectionId: 20_103, generateIndex: 5000);
    Assert(firstPistaNpc == 20_101 && secondPistaNpc == 20_102 && unrelatedNpc == 20_103, "Pista cleanup fixture NPCs were not registered.");
    var removed = hub.ResetPistaLv6Room();
    Assert(removed.Select(static npc => npc.ConnectionId).OrderBy(static id => id).SequenceEqual([20_101, 20_102]), "Pista cleanup removed the wrong generator range.");
    Assert(!hub.TryGetPistaLv6State(out _) && hub.TryGetNpcCombatState(unrelatedNpc, out _, out _, out _), "Pista cleanup did not reset the room state or preserve an unrelated NPC.");
}

static void QuestRequestParser()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var payload = new byte[8];
    BinaryPrimitives.WriteInt32LittleEndian(payload, 20_001);
    BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4), 1);
    var frame = codec.Decode(codec.Encode(QuestRequest.MessageType, 30000, 33, payload, 16));
    Assert(QuestRequest.TryParse(frame, out var request) && request is { NpcConnectionId: 20_001, Confirm: 1 }, "The _MSG_Quest MSG_STANDARDPARM2 parser did not preserve both parameters.");
}

static void PistaRegistrationFlow()
{
    var mob = new byte[LegacyAccountSnapshot.CharacterStride];
    System.Text.Encoding.ASCII.GetBytes("PISTA_LEADER").CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset));
    new LegacyItem(5134, 43, 6, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
    var npcMob = new byte[LegacyAccountSnapshot.CharacterStride];
    npcMob[LegacyAccountSnapshot.MobMerchantOffset] = 72;

    var hub = new WorldHub();
    Assert(hub.Enter(1, "PISTA_ACCOUNT", (_, _) => ValueTask.CompletedTask), "Pista registration leader was not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, mob), "Pista registration leader state was not set.");
    var npcId = hub.EnterNpc(npcMob, 2010, 2000, requestedConnectionId: 20_201);
    Assert(npcId == 20_201, "Pista registration NPC was not registered.");

    var result = hub.TryRegisterPistaParty(1, npcId, out var outcome);
    Assert(result == LegacyPistaRegistrationResult.Accepted && outcome is { Level: 6, PartySlot: 0, InventorySlot: 0, UpdatedItem.Index: 0 }, "Pista registration did not reserve the +6 slot and consume the ticket.");
    Assert(hub.TryGetCharacterSnapshot(1, out var updatedMob, out _, out _) && updatedMob is not null && LegacyItem.Read(updatedMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset)).Index == 0, "Pista registration did not clear the authoritative ticket slot.");
    Assert(hub.TryGetPistaLv6State(out var state) && state is { LeaderConnectionId: 1, MobCount: 0 }, "Pista +6 registration did not initialize the room state.");

    var wrongNpcMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var wrongNpcId = hub.EnterNpc(wrongNpcMob, 2010, 2000, requestedConnectionId: 20_202);
    Assert(hub.TryRegisterPistaParty(1, wrongNpcId, out _) == LegacyPistaRegistrationResult.WrongNpc, "Pista registration accepted an NPC with the wrong merchant id.");
}

static void PistaEntrySchedule()
{
    var leaderMob = new byte[LegacyAccountSnapshot.CharacterStride];
    System.Text.Encoding.ASCII.GetBytes("PISTA_ENTRY").CopyTo(leaderMob.AsSpan(LegacyAccountSnapshot.MobNameOffset));
    new LegacyItem(5134, 43, 6, 0, 0, 0, 0).Write(leaderMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
    var memberMob = new byte[LegacyAccountSnapshot.CharacterStride];
    System.Text.Encoding.ASCII.GetBytes("PISTA_MEMBER").CopyTo(memberMob.AsSpan(LegacyAccountSnapshot.MobNameOffset));
    var npcMob = new byte[LegacyAccountSnapshot.CharacterStride];
    npcMob[LegacyAccountSnapshot.MobMerchantOffset] = 72;

    var hub = new WorldHub();
    Assert(hub.Enter(1, "PISTA_ENTRY_ACCOUNT", (_, _) => ValueTask.CompletedTask) && hub.Enter(2, "PISTA_MEMBER_ACCOUNT", (_, _) => ValueTask.CompletedTask), "Pista entry participants were not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 3200, 1664, leaderMob) && hub.SetCharacterState(2, 0, 0, 1, 0, 0, 3201, 1665, memberMob), "Pista entry participant state was not set.");
    Assert(hub.TrySetParty(1, [1, 2]), "Pista entry party was not formed.");
    var npcId = hub.EnterNpc(npcMob, 3204, 1664, requestedConnectionId: 20_301);
    Assert(hub.TryRegisterPistaParty(1, npcId, out var registration) == LegacyPistaRegistrationResult.Accepted && registration is { Level: 6, PartySlot: 0 }, "Pista entry registration fixture was not accepted.");

    Assert(!hub.TryProcessPistaEntry(new DateTime(2026, 9, 16, 12, 19, 59), out _), "Pista entry started outside the legacy minute boundary.");
    Assert(hub.TryProcessPistaEntry(new DateTime(2026, 9, 16, 12, 20, 0), out var plan) && plan is not null, "Pista entry did not run at minute 20:00.");
    Assert(plan!.Teleports.Select(static teleport => teleport.ConnectionId).OrderBy(static id => id).SequenceEqual([1, 2]), "Pista entry did not teleport the eligible leader and party member.");
    Assert(plan.Teleports.All(static teleport => teleport.ToX == 3404 && teleport.ToY == 1517), "Pista +6 entry used coordinates different from PistaPos[6][0].");
    Assert(hub.TryGetPistaLv6State(out var state) && state is { LeaderConnectionId: 1, MobCount: 100 }, "Pista +6 entry did not initialize MobCount to 100.");
    Assert(hub.TryGetCharacterSnapshot(1, out _, out var leaderX, out var leaderY, out _) && leaderX == 3404 && leaderY == 1517, "Pista entry did not persist the leader teleport in WorldHub.");
    Assert(!hub.TryProcessPistaEntry(new DateTime(2026, 9, 16, 12, 20, 0), out _), "Pista entry was processed twice in the same minute slot.");

    var generatedNpcId = hub.EnterNpc(npcMob, 3406, 1517, requestedConnectionId: 20_302, generateIndex: 5775);
    var staleNpcId = hub.EnterNpc(npcMob, 3500, 1600, requestedConnectionId: 20_303, generateIndex: 5000);
    Assert(generatedNpcId == 20_302 && staleNpcId == 20_303, "Pista exit NPC fixtures were not registered.");
    Assert(hub.TryProcessPistaExit(new DateTime(2026, 9, 16, 12, 35, 0), out var exitPlan) && exitPlan is not null, "Pista exit did not run at minute 35:00.");
    Assert(exitPlan!.Teleports.Select(static teleport => teleport.ConnectionId).OrderBy(static id => id).SequenceEqual([1, 2]) && exitPlan.Teleports.All(static teleport => teleport.ToX == 3294 && teleport.ToY == 1701), "Pista exit did not teleport the room party to the legacy outside position.");
    Assert(exitPlan.RemovedNpcs.Select(static npc => npc.ConnectionId).OrderBy(static id => id).SequenceEqual([20_302, 20_303]), "Pista exit did not clear every NPC inside the legacy arena.");
    Assert(!hub.TryGetPistaLv6State(out _) && !hub.TryProcessPistaExit(new DateTime(2026, 9, 16, 12, 35, 0), out _), "Pista exit was not idempotent for the same minute slot.");
}

static void PistaLevel3Progression()
{
    var attackerMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var npcMob = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(50, 0, 100, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 40, 0, 0, 0)
        .Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    new LegacyScore(50, 0, 0, 0, 0, 0, 0, 100, 100, 1, 100, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    npcMob[LegacyAccountSnapshot.MobClanOffset] = 1;

    var merchantMob = new byte[LegacyAccountSnapshot.CharacterStride];
    merchantMob[LegacyAccountSnapshot.MobMerchantOffset] = 72;
    var leaderMob = attackerMob.ToArray();
    System.Text.Encoding.ASCII.GetBytes("PISTA_LV3").CopyTo(leaderMob.AsSpan(LegacyAccountSnapshot.MobNameOffset));
    new LegacyItem(5134, 43, 3, 0, 0, 0, 0).Write(leaderMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));

    var hub = new WorldHub();
    Assert(hub.Enter(1, "PISTA_LV3_ACCOUNT", (_, _) => ValueTask.CompletedTask), "Pista +3 leader was not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, leaderMob), "Pista +3 leader state was not set.");
    var merchantId = hub.EnterNpc(merchantMob, 2010, 2000, requestedConnectionId: 20_401);
    Assert(hub.TryRegisterPistaParty(1, merchantId, out var registration) == LegacyPistaRegistrationResult.Accepted && registration is { Level: 3, PartySlot: 0 }, "Pista +3 registration did not reserve party slot zero.");

    var sulrangId = hub.EnterNpc(npcMob, 2005, 2005, requestedConnectionId: 20_402, generateIndex: 5972, terrainHeight: 0);
    var sulrangResult = hub.TryApplyPhysicalAttack(1, sulrangId, randomFactor: 99, out var sulrangOutcome, pistaRandomRoll: 2);
    Assert(sulrangResult == LegacyPhysicalAttackResult.Accepted && sulrangOutcome?.PistaTransition is
        { PistaLevel: 3, LeaderConnectionId: 1, PreviousMobCount: 0, RemainingMobCount: 1, SpawnGenerateIndex: 5950 }, "A Pista +3 Sulrang death did not start the room and request boss 5950.");

    var bossId = hub.EnterNpc(npcMob, 2005, 2005, requestedConnectionId: 20_403, generateIndex: 5950, terrainHeight: 0);
    var bossResult = hub.TryApplyPhysicalAttack(1, bossId, randomFactor: 99, out var bossOutcome, pistaRandomRoll: 4);
    Assert(bossResult == LegacyPhysicalAttackResult.Accepted && bossOutcome?.PistaTransition is
        { PistaLevel: 3, LeaderConnectionId: 1, PreviousMobCount: 1, RemainingMobCount: 2, SpawnGenerateIndex: 5952 }, "A Pista +3 boss death did not increment the room count and request the next boss.");

    Assert(hub.TryProcessPistaExit(new DateTime(2026, 9, 16, 16, 35, 0), out var exitPlan, pistaRuneRoll: 1) && exitPlan is not null, "Pista +3 exit did not run for the reward fixture.");
    Assert(exitPlan!.ItemDrops is { Count: 2 } drops && drops.Any(static drop => drop.Item.Index == 5126) && drops.Any(static drop => drop.Item is { Index: 5134, Effect1: 43, Value1: 4 }), "Pista +3 winner reward did not award the rune and the +4 progression ticket.");
}

static void PistaFixedEntrySpawns()
{
    var template = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(50, 0, 0, 0, 0, 0, 0, 321, 100, 1, 100, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(template.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    System.Text.Encoding.ASCII.GetBytes("Torre_Pista").CopyTo(template.AsSpan(LegacyAccountSnapshot.MobNameOffset));

    var catalog = LegacyNpcGenerationCatalog.Parse("""
        # [5706]
        MaxNumMob: 1
        Leader: Torre_Pista
        StartX: 1
        StartY: 1
        # [5707]
        MaxNumMob: 1
        Leader: Torre_Pista
        StartX: 2
        StartY: 2
        # [5708]
        MaxNumMob: 1
        Leader: Torre_Pista
        StartX: 3
        StartY: 3
        """, new Dictionary<string, byte[]> { ["Torre_Pista"] = template });

    var leaderMob = new byte[LegacyAccountSnapshot.CharacterStride];
    System.Text.Encoding.ASCII.GetBytes("PISTA_TOWER").CopyTo(leaderMob.AsSpan(LegacyAccountSnapshot.MobNameOffset));
    new LegacyItem(5134, 43, 1, 0, 0, 0, 0).Write(leaderMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
    var merchantMob = new byte[LegacyAccountSnapshot.CharacterStride];
    merchantMob[LegacyAccountSnapshot.MobMerchantOffset] = 72;

    var hub = new WorldHub(npcGenerationCatalog: catalog);
    Assert(hub.Enter(1, "PISTA_TOWER_ACCOUNT", (_, _) => ValueTask.CompletedTask), "Pista +1 entry leader was not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 3200, 1664, leaderMob), "Pista +1 entry leader state was not set.");
    var merchantId = hub.EnterNpc(merchantMob, 3204, 1664, requestedConnectionId: 20_501);
    Assert(hub.TryRegisterPistaParty(1, merchantId, out var registration) == LegacyPistaRegistrationResult.Accepted && registration is { Level: 1, PartySlot: 0 }, "Pista +1 entry registration was not accepted.");

    Assert(hub.TryProcessPistaEntry(new DateTime(2026, 9, 16, 13, 20, 0), out var plan) && plan is not null, "Pista +1 entry did not run at the legacy minute boundary.");
    Assert(plan!.Teleports is [{ ToX: 3362, ToY: 1574 }], "Pista +1 entry used coordinates different from PistaPos[1][0].");
    Assert(plan.SpawnedNpcs.Count == 3, "Pista +1 entry did not request the three tower generators.");
    Assert(plan.SpawnedNpcs.Select(static npc => (npc.GenerateIndex, npc.PositionX, npc.PositionY)).SequenceEqual([
        (5706, (short)3358, (short)1582),
        (5707, (short)3386, (short)1548),
        (5708, (short)3418, (short)1582)]), "Pista +1 tower generators did not preserve the fixed legacy positions.");
}

static void PistaEntryGeneratorMatrix()
{
    var template = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(50, 0, 0, 0, 0, 0, 0, 321, 100, 1, 100, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(template.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    System.Text.Encoding.ASCII.GetBytes("Pista_Entry_Mob").CopyTo(template.AsSpan(LegacyAccountSnapshot.MobNameOffset));

    var allPistaGenerators = new[] { 5653, 5654 }
        .Concat(Enumerable.Range(5706, 59))
        .Concat(Enumerable.Range(5767, 23))
        .Concat(Enumerable.Range(5789, 60))
        .Concat(Enumerable.Range(5849, 51))
        .Concat(Enumerable.Range(5948, 8))
        .Concat(Enumerable.Range(5972, 4))
        .Distinct()
        .ToArray();
    var catalogText = string.Join(Environment.NewLine, allPistaGenerators.SelectMany(static index => new[]
    {
        $"# [{index}]",
        "MaxNumMob: 1",
        "Leader: Pista_Entry_Mob",
        "StartX: 1",
        "StartY: 1",
    }));
    var catalog = LegacyNpcGenerationCatalog.Parse(catalogText, new Dictionary<string, byte[]> { ["Pista_Entry_Mob"] = template });

    static byte[] CreateLeaderMob(string name, int sanctuary)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        System.Text.Encoding.ASCII.GetBytes(name).CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset));
        new LegacyItem(5134, 43, (byte)sanctuary, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
        return mob;
    }

    static void Register(WorldHub hub, int connectionId, int level, byte[] mob, int merchantId)
    {
        Assert(hub.Enter(connectionId, $"PISTA_MATRIX_{connectionId}", (_, _) => ValueTask.CompletedTask), $"Pista matrix leader {connectionId} was not registered.");
        Assert(hub.SetCharacterState(connectionId, 0, 0, 1, 0, 0, (short)(3200 + connectionId), 1664, mob), $"Pista matrix leader {connectionId} state was not set.");
        Assert(hub.TryRegisterPistaParty(connectionId, merchantId, out var registration) == LegacyPistaRegistrationResult.Accepted && registration is { Level: var registeredLevel } && registeredLevel == level, $"Pista matrix level {level} registration was not accepted.");
    }

    var merchantMob = new byte[LegacyAccountSnapshot.CharacterStride];
    merchantMob[LegacyAccountSnapshot.MobMerchantOffset] = 72;
    var hub = new WorldHub(npcGenerationCatalog: catalog);
    var merchantId = hub.EnterNpc(merchantMob, 3204, 1664, requestedConnectionId: 20_701);
    Register(hub, 1, 0, CreateLeaderMob("PISTA_MATRIX_1", 0), merchantId);
    Register(hub, 2, 1, CreateLeaderMob("PISTA_MATRIX_2", 1), merchantId);
    Register(hub, 3, 2, CreateLeaderMob("PISTA_MATRIX_3", 2), merchantId);
    Register(hub, 4, 4, CreateLeaderMob("PISTA_MATRIX_4", 4), merchantId);

    Assert(hub.TryProcessPistaEntry(new DateTime(2026, 9, 16, 17, 20, 0), out var plan) && plan is not null, "Pista entry generator matrix did not run at the legacy boundary.");
    Assert(plan!.SpawnedNpcs.Count == 106, "Pista entry did not create exactly the legacy +0/+1/+2/+4 generator set.");
    Assert(plan.SpawnedNpcs.Count(npc => npc.GenerateIndex == 5654) == 1, "Pista +0 entry did not create the slot-zero Lich generator.");
    Assert(plan.SpawnedNpcs.Count(npc => npc.GenerateIndex is >= 5706 and <= 5764) == 59, "Pista +1 entry did not create the three towers and 56 mob generators.");
    Assert(plan.SpawnedNpcs.Count(npc => npc.GenerateIndex == 5789) == 1, "Pista +2 entry did not create the Amon generator.");
    Assert(plan.SpawnedNpcs.Count(npc => npc.GenerateIndex is >= 5854 and <= 5898) == 45, "Pista +4 entry did not create the 45 labyrinth generators.");

    var noInitialSpawnHub = new WorldHub(npcGenerationCatalog: catalog);
    var noInitialSpawnMerchant = noInitialSpawnHub.EnterNpc(merchantMob, 3204, 1664, requestedConnectionId: 20_702);
    Register(noInitialSpawnHub, 5, 3, CreateLeaderMob("PISTA_MATRIX_5", 3), noInitialSpawnMerchant);
    Register(noInitialSpawnHub, 6, 5, CreateLeaderMob("PISTA_MATRIX_6", 5), noInitialSpawnMerchant);
    Register(noInitialSpawnHub, 7, 6, CreateLeaderMob("PISTA_MATRIX_7", 6), noInitialSpawnMerchant);
    Assert(noInitialSpawnHub.TryProcessPistaEntry(new DateTime(2026, 9, 16, 17, 40, 0), out var noInitialSpawnPlan) && noInitialSpawnPlan is not null && noInitialSpawnPlan.SpawnedNpcs.Count == 0, "Pista +3/+5/+6 entry generated mobs that the legacy entry path does not generate.");
}

static void PistaLevel5Progression()
{
    var attackerMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var npcMob = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(50, 0, 100, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 40, 0, 0, 0)
        .Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    new LegacyScore(50, 0, 0, 0, 0, 0, 0, 100, 100, 1, 100, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    npcMob[LegacyAccountSnapshot.MobClanOffset] = 1;

    var leaderMob = attackerMob.ToArray();
    System.Text.Encoding.ASCII.GetBytes("PISTA_LV5").CopyTo(leaderMob.AsSpan(LegacyAccountSnapshot.MobNameOffset));
    new LegacyItem(5134, 43, 5, 0, 0, 0, 0).Write(leaderMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
    var merchantMob = new byte[LegacyAccountSnapshot.CharacterStride];
    merchantMob[LegacyAccountSnapshot.MobMerchantOffset] = 72;

    var hub = new WorldHub();
    Assert(hub.Enter(1, "PISTA_LV5_ACCOUNT", (_, _) => ValueTask.CompletedTask), "Pista +5 leader was not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, leaderMob), "Pista +5 leader state was not set.");
    var merchantId = hub.EnterNpc(merchantMob, 2010, 2000, requestedConnectionId: 20_601);
    Assert(hub.TryRegisterPistaParty(1, merchantId, out var registration) == LegacyPistaRegistrationResult.Accepted && registration is { Level: 5, PartySlot: 0 }, "Pista +5 registration was not accepted.");

    var bossId = hub.EnterNpc(npcMob, 2005, 2005, requestedConnectionId: 20_602, generateIndex: 5899, terrainHeight: 0);
    var result = hub.TryApplyPhysicalAttack(1, bossId, randomFactor: 99, out var outcome, runeRoll: 0);
    Assert(result == LegacyPhysicalAttackResult.Accepted && outcome?.PistaTransition is
        { PistaLevel: 5, LeaderConnectionId: 1, PreviousMobCount: 0, RemainingMobCount: 1, SpawnGenerateIndex: 0 }, "A Pista +5 Balrog death did not mark MobCount=1.");
    Assert(outcome?.ItemDrops is { Count: 2 } drops && drops.Any(static drop => drop.Item.Index == 5120) && drops.Any(static drop => drop.Item is { Index: 5134, Effect1: 43, Value1: 6 }), "The Pista +5 death did not award the rune and the +6 progression ticket through the real attack path.");
}

static void PistaLevel1Counters()
{
    var attackerMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var npcMob = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(50, 0, 100, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 40, 0, 0, 0)
        .Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    new LegacyScore(50, 0, 0, 0, 0, 0, 0, 100, 100, 1, 100, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    npcMob[LegacyAccountSnapshot.MobClanOffset] = 1;

    var leaderMob = attackerMob.ToArray();
    System.Text.Encoding.ASCII.GetBytes("PISTA_LV1").CopyTo(leaderMob.AsSpan(LegacyAccountSnapshot.MobNameOffset));
    new LegacyItem(5134, 43, 1, 0, 0, 0, 0).Write(leaderMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
    var merchantMob = new byte[LegacyAccountSnapshot.CharacterStride];
    merchantMob[LegacyAccountSnapshot.MobMerchantOffset] = 72;

    var hub = new WorldHub();
    Assert(hub.Enter(1, "PISTA_LV1_ACCOUNT", (_, _) => ValueTask.CompletedTask), "Pista +1 leader was not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, leaderMob), "Pista +1 leader state was not set.");
    var merchantId = hub.EnterNpc(merchantMob, 2010, 2000, requestedConnectionId: 20_701);
    Assert(hub.TryRegisterPistaParty(1, merchantId, out var registration) == LegacyPistaRegistrationResult.Accepted && registration is { Level: 1, PartySlot: 0 }, "Pista +1 registration was not accepted.");

    var towerId = hub.EnterNpc(npcMob, 2005, 2005, requestedConnectionId: 20_702, generateIndex: 5706);
    var mobId = hub.EnterNpc(npcMob, 2006, 2005, requestedConnectionId: 20_703, generateIndex: 5709);
    var mobResult = hub.TryApplyPhysicalAttack(1, mobId, randomFactor: 99, out var mobOutcome);
    Assert(mobResult == LegacyPhysicalAttackResult.Accepted && mobOutcome?.PistaTransition is
        { PistaLevel: 1, LeaderConnectionId: 1, PreviousMobCount: 0, RemainingMobCount: 1, SpawnGenerateIndex: 0 }, "A Pista +1 mob death did not increment the counter while its tower remained alive.");

    var towerResult = hub.TryApplyPhysicalAttack(1, towerId, randomFactor: 99, out var towerOutcome);
    Assert(towerResult == LegacyPhysicalAttackResult.Accepted && towerOutcome?.PistaTransition is
        { PistaLevel: 1, LeaderConnectionId: 1, PreviousMobCount: 1, RemainingMobCount: 0, SpawnGenerateIndex: 0 }, "A Pista +1 tower death did not reset its party counter.");
}

static void PistaLevel2Reward()
{
    var attackerMob = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(50, 0, 100, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 40, 0, 0, 0)
        .Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    System.Text.Encoding.ASCII.GetBytes("PISTA_LV2").CopyTo(attackerMob.AsSpan(LegacyAccountSnapshot.MobNameOffset));
    new LegacyItem(5134, 43, 2, 0, 0, 0, 0).Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));

    var amonTemplate = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(50, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(amonTemplate.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    amonTemplate[LegacyAccountSnapshot.MobClanOffset] = 1;
    System.Text.Encoding.ASCII.GetBytes("Amon_Pista").CopyTo(amonTemplate.AsSpan(LegacyAccountSnapshot.MobNameOffset));
    var catalog = LegacyNpcGenerationCatalog.Parse("""
        # [5789]
        MaxNumMob: 1
        Leader: Amon_Pista
        StartX: 3410
        StartY: 1453
        """, new Dictionary<string, byte[]> { ["Amon_Pista"] = amonTemplate });

    var merchantMob = new byte[LegacyAccountSnapshot.CharacterStride];
    merchantMob[LegacyAccountSnapshot.MobMerchantOffset] = 72;
    var hub = new WorldHub(npcGenerationCatalog: catalog);
    Assert(hub.Enter(1, "PISTA_LV2_ACCOUNT", (_, _) => ValueTask.CompletedTask), "Pista +2 leader was not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 3200, 1664, attackerMob), "Pista +2 leader state was not set.");
    var merchantId = hub.EnterNpc(merchantMob, 3204, 1664, requestedConnectionId: 20_450);
    Assert(hub.TryRegisterPistaParty(1, merchantId, out var registration) == LegacyPistaRegistrationResult.Accepted && registration is { Level: 2, PartySlot: 0 }, "Pista +2 registration was not accepted.");
    Assert(hub.TryProcessPistaEntry(new DateTime(2026, 9, 16, 17, 20, 0), out var entryPlan) && entryPlan is not null, "Pista +2 entry did not run at the legacy minute boundary.");
    Assert(entryPlan!.Teleports is [{ ToX: 3410, ToY: 1453 }] && entryPlan.SpawnedNpcs is [{ GenerateIndex: 5789 }], "Pista +2 entry did not teleport the leader and create Amon 5789.");
    var boss = entryPlan.SpawnedNpcs.Single();

    var result = hub.TryApplyPhysicalAttack(1, boss.ConnectionId, randomFactor: 99, out var outcome, runeRoll: 4);
    Assert(result == LegacyPhysicalAttackResult.Accepted && outcome?.ItemDrops is { Count: 2 } drops &&
        drops.Any(static drop => drop.Item.Index == 5130) && drops.Any(static drop => drop.Item is { Index: 5134, Effect1: 43, Value1: 3 }), $"Pista +2 Amon death did not award the selected rune and the +3 progression ticket through the real attack path (result={result}, removed={outcome?.TargetRemoved}, hp={outcome?.RemainingHp}, drops={outcome?.ItemDrops?.Count ?? -1}, items={string.Join(',', outcome?.ItemDrops?.Select(static drop => drop.Item.Index) ?? [])}).");
}

static void PistaLevel0Retry()
{
    var attackerMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var lichMob = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(50, 0, 100, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 40, 0, 0, 0)
        .Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    new LegacyScore(50, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(lichMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    lichMob[LegacyAccountSnapshot.MobClanOffset] = 1;

    var retryHub = new WorldHub();
    Assert(retryHub.Enter(1, "PISTA_LV0_RETRY", (_, _) => ValueTask.CompletedTask), "Pista +0 retry leader was not registered.");
    Assert(retryHub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob), "Pista +0 retry leader state was not set.");
    var firstLichId = retryHub.EnterNpc(lichMob, 2005, 2005, requestedConnectionId: 20_401, generateIndex: 5653, terrainHeight: 0);
    var secondLichId = retryHub.EnterNpc(lichMob, 2006, 2005, requestedConnectionId: 20_402, generateIndex: 5654, terrainHeight: 0);
    var retryResult = retryHub.TryApplyPhysicalAttack(1, firstLichId, randomFactor: 99, out var retryOutcome, runeChanceRoll: 20);
    Assert(retryResult == LegacyPhysicalAttackResult.Accepted && retryOutcome?.PistaTransition is
        { PistaLevel: 0, SpawnGenerateIndexes: [5653, 5653, 5654, 5654] } transition &&
        retryOutcome.TargetRemoved && transition.RemovedNpcConnectionIds is { Count: 1 } removed && removed.Contains(secondLichId) &&
        retryOutcome.ItemDrops is { Count: 0 }, "A failed Pista +0 roll did not clear the Lich room and request the four legacy respawns.");

    var successHub = new WorldHub();
    Assert(successHub.Enter(1, "PISTA_LV0_SUCCESS", (_, _) => ValueTask.CompletedTask), "Pista +0 success leader was not registered.");
    Assert(successHub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob), "Pista +0 success leader state was not set.");
    var successLichId = successHub.EnterNpc(lichMob, 2005, 2005, requestedConnectionId: 20_403, generateIndex: 5654, terrainHeight: 0);
    var successResult = successHub.TryApplyPhysicalAttack(1, successLichId, randomFactor: 99, out var successOutcome, runeRoll: 0, runeChanceRoll: 0);
    Assert(successResult == LegacyPhysicalAttackResult.Accepted && successOutcome?.PistaTransition is
        { PistaLevel: 0, SpawnGenerateIndexes: [] } && successOutcome.ItemDrops is { Count: 2 } drops &&
        drops.Any(static drop => drop.Item.Index == 5110) && drops.Any(static drop => drop.Item is { Index: 5134, Effect1: 43, Value1: 1 }), "A successful Pista +0 roll did not award the Lich rune and the +1 progression ticket.");
}

static void PistaLevel1ExitReward()
{
    var attackerMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var npcMob = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(50, 0, 100, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 40, 0, 0, 0)
        .Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    new LegacyScore(50, 0, 0, 0, 0, 0, 0, 100, 100, 1, 100, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    npcMob[LegacyAccountSnapshot.MobClanOffset] = 1;

    var leaderMob = attackerMob.ToArray();
    System.Text.Encoding.ASCII.GetBytes("PISTA_REWARD").CopyTo(leaderMob.AsSpan(LegacyAccountSnapshot.MobNameOffset));
    new LegacyItem(5134, 43, 1, 0, 0, 0, 0).Write(leaderMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
    var merchantMob = new byte[LegacyAccountSnapshot.CharacterStride];
    merchantMob[LegacyAccountSnapshot.MobMerchantOffset] = 72;

    var hub = new WorldHub();
    Assert(hub.Enter(1, "PISTA_REWARD_LEADER", (_, _) => ValueTask.CompletedTask) && hub.Enter(2, "PISTA_REWARD_MEMBER", (_, _) => ValueTask.CompletedTask), "Pista +1 reward party was not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, leaderMob) && hub.SetCharacterState(2, 0, 0, 1, 0, 0, 2001, 2000, attackerMob), "Pista +1 reward party state was not set.");
    Assert(hub.TrySetParty(1, [1, 2]), "Pista +1 reward party was not formed.");
    var merchantId = hub.EnterNpc(merchantMob, 2010, 2000, requestedConnectionId: 20_801);
    Assert(hub.TryRegisterPistaParty(1, merchantId, out var registration) == LegacyPistaRegistrationResult.Accepted && registration is { Level: 1, PartySlot: 0 }, "Pista +1 reward registration was not accepted.");

    var towerId = hub.EnterNpc(npcMob, 2005, 2005, requestedConnectionId: 20_802, generateIndex: 5706);
    var mobId = hub.EnterNpc(npcMob, 2006, 2005, requestedConnectionId: 20_803, generateIndex: 5709);
    Assert(hub.TryApplyPhysicalAttack(1, mobId, randomFactor: 99, out var mobOutcome) == LegacyPhysicalAttackResult.Accepted && mobOutcome?.PistaTransition?.RemainingMobCount == 1, "Pista +1 reward fixture did not record the winning counter.");
    Assert(hub.UpdatePosition(1, 3400, 1500) && hub.UpdatePosition(2, 3401, 1500), "Pista +1 reward party could not enter the exit area.");

    Assert(hub.TryProcessPistaExit(new DateTime(2026, 9, 16, 14, 35, 0), out var plan, pistaRuneRoll: 1) && plan is not null, "Pista +1 exit did not run for the reward fixture.");
    Assert(plan!.Teleports.Select(static teleport => teleport.ConnectionId).OrderBy(static id => id).SequenceEqual([1, 2]), "Pista +1 exit reward fixture did not teleport the party.");
    Assert(plan.ItemDrops is { Count: 3 } drops && drops.Count(drop => drop.Item.Index == 5113) == 2 && drops.Any(static drop => drop.Item is { Index: 5134, Effect1: 43, Value1: 2 }), "Pista +1 winner reward did not distribute two runes and the +2 progression ticket.");
    Assert(plan.RemovedNpcs.Any(npc => npc.ConnectionId == towerId) && !plan.RemovedNpcs.Any(npc => npc.ConnectionId == mobId), "Pista +1 exit reward fixture removed an already-dead NPC or missed the live tower.");
}

static void PistaLevel4Progression()
{
    var attackerMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var npcMob = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(50, 0, 100, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 40, 0, 0, 0)
        .Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    new LegacyScore(50, 0, 0, 0, 0, 0, 0, 100, 100, 1, 100, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    npcMob[LegacyAccountSnapshot.MobClanOffset] = 1;

    var merchantMob = new byte[LegacyAccountSnapshot.CharacterStride];
    merchantMob[LegacyAccountSnapshot.MobMerchantOffset] = 72;
    var hub = new WorldHub();
    Assert(hub.Enter(1, "PISTA_LV4_A", (_, _) => ValueTask.CompletedTask) && hub.Enter(2, "PISTA_LV4_B", (_, _) => ValueTask.CompletedTask) && hub.Enter(3, "PISTA_LV4_C", (_, _) => ValueTask.CompletedTask), "Pista +4 leaders were not registered.");

    var leaders = new[] { "PISTA_LV4_A", "PISTA_LV4_B", "PISTA_LV4_C" };
    for (var index = 0; index < leaders.Length; index++)
    {
        var mob = attackerMob.ToArray();
        System.Text.Encoding.ASCII.GetBytes(leaders[index]).CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset));
        new LegacyItem(5134, 43, 4, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
        Assert(hub.SetCharacterState(index + 1, 0, 0, 1, 0, 0, 3200, 1664, mob), $"Pista +4 leader {index} state was not set.");
    }

    var merchantId = hub.EnterNpc(merchantMob, 3205, 1664, requestedConnectionId: 20_901);
    Assert(hub.TryRegisterPistaParty(1, merchantId, out var firstRegistration) == LegacyPistaRegistrationResult.Accepted && firstRegistration is { Level: 4, PartySlot: 0 }, "Pista +4 party zero registration was not accepted.");
    Assert(hub.TryRegisterPistaParty(2, merchantId, out var secondRegistration) == LegacyPistaRegistrationResult.Accepted && secondRegistration is { Level: 4, PartySlot: 1 }, "Pista +4 party one registration was not accepted.");
    Assert(hub.TryRegisterPistaParty(3, merchantId, out var thirdRegistration) == LegacyPistaRegistrationResult.Accepted && thirdRegistration is { Level: 4, PartySlot: 2 }, "Pista +4 party two registration was not accepted.");

    Assert(hub.TryProcessPistaEntry(new DateTime(2026, 9, 16, 15, 20, 0), out var entryPlan, pistaLevel4MobRoll: 0) && entryPlan is not null, "Pista +4 entry did not initialize the three rooms.");
    Assert(entryPlan!.Teleports.Select(static teleport => teleport.ConnectionId).OrderBy(static id => id).SequenceEqual([1, 2, 3]), "Pista +4 entry did not teleport all registered leaders.");

    LegacyPistaTransition? lastTransition = null;
    for (var count = 0; count < 8; count++)
    {
        var mobId = hub.EnterNpc(npcMob, 3345, 1394, requestedConnectionId: 20_910 + count, generateIndex: 5854);
        var result = hub.TryApplyPhysicalAttack(1, mobId, randomFactor: 99, out var outcome);
        Assert(result == LegacyPhysicalAttackResult.Accepted && outcome is not null, $"Pista +4 mob {count} did not resolve.");
        lastTransition = outcome!.PistaTransition;
    }

    Assert(lastTransition is
        { PistaLevel: 4, LeaderConnectionId: 1, PreviousMobCount: 1, RemainingMobCount: 0, SpawnGenerateIndex: 5849, Teleports: [{ ConnectionId: 1, ToX: 3351, ToY: 1334 }] }, "The final Pista +4 labyrinth mob did not teleport the party and request boss 5849.");
    Assert(hub.TryGetCharacterSnapshot(1, out _, out var leaderX, out var leaderY, out _) && leaderX == 3351 && leaderY == 1334, "Pista +4 completion did not persist the leader teleport.");

    var bossId = hub.EnterNpc(npcMob, 3355, 1334, requestedConnectionId: 20_999, generateIndex: 5849, terrainHeight: 0);
    var bossResult = hub.TryApplyPhysicalAttack(1, bossId, randomFactor: 99, out var bossOutcome, runeRoll: 0);
    Assert(bossResult == LegacyPhysicalAttackResult.Accepted && bossOutcome?.ItemDrops is { Count: 2 } drops && drops.Any(static drop => drop.Item.Index == 5125) && drops.Any(static drop => drop.Item is { Index: 5134, Effect1: 43, Value1: 5 }), "Pista +4 boss death did not award the rune and the +5 progression ticket through the real attack path.");
}

static void WorldNpcBossDrop()
{
    var attackerMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var npcMob = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(50, 0, 100, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 40, 0, 0, 0)
        .Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    new LegacyScore(50, 0, 0, 0, 0, 0, 0, 100, 100, 1, 100, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    npcMob[LegacyAccountSnapshot.MobClanOffset] = 1;

    var noHeight = LegacyNpcDropMath.SelectBossDrops(0, null, roll: static _ => 0);
    Assert(noHeight.Count == 0, "A boss drop was generated without the legacy height-grid gate.");
    var generatorThree = LegacyNpcDropMath.SelectBossDrops(3, 0, roll: static _ => 2);
    Assert(generatorThree.Count == 1 && generatorThree[0].Item.Index == 1418, "GenerateIndex 3 did not select the third legacy boss item from rand()%7.");
    var bossRolls = new Queue<int>([0, 3]);
    int BossRoll(int maximum)
    {
        if (bossRolls.Count == 0)
            throw new InvalidOperationException("The deterministic boss-drop fixture ran out of rolls.");
        var value = bossRolls.Dequeue();
        if (value < 0 || value >= maximum)
            throw new ArgumentOutOfRangeException(nameof(value));
        return value;
    }

    var generatorFive = LegacyNpcDropMath.SelectBossDrops(5, 0, roll: BossRoll);
    Assert(generatorFive.Count == 1 && generatorFive[0].Item.Index == 424, "GenerateIndex 5 did not select item 421..427 using the second legacy roll.");
    var blockedHeight = LegacyNpcDropMath.SelectBossDrops(5, 36, roll: static _ => 0);
    Assert(blockedHeight.Count == 0, "The narrow -40..36 boss height gate was not preserved.");
    var coliseumDrop = LegacyNpcDropMath.SelectColiseumDrops(4623, 0, CreateCombatItemDataTable(), roll: static _ => 2);
    Assert(coliseumDrop.Count == 1 && coliseumDrop[0].Item.Index == 4026, "The Coliseu N branch did not select item 4026 from rand()%14.");
    Assert(LegacyNpcDropMath.SelectColiseumDrops(4623, null, roll: static _ => 0).Count == 0, "The Coliseu N branch bypassed the legacy height-grid gate.");
    var bonusRolls = new Queue<int>([0, 7, 7, 7]);
    int BonusRoll(int maximum) => bonusRolls.Dequeue();
    var bonusTable = CreateCombatItemDataTable();
    var bonusDrop = LegacyNpcDropMath.SelectBossDrops(0, 0, bonusTable, BonusRoll);
    Assert(bonusDrop.Count == 1 && bonusDrop[0].Item is { Index: 419, Effect1: 59, Value1: 7, Effect2: 59, Value2: 7, Effect3: 59, Value3: 7 }, "Boss drops did not run the legacy SetItemBonus filler path when ItemList data was available.");

    var hub = new WorldHub();
    Assert(hub.Enter(1, "BOSS_HUNTER", (_, _) => ValueTask.CompletedTask), "Boss-drop attacker was not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob), "Boss-drop attacker state was not set.");
    var npcId = hub.EnterNpc(npcMob, 2005, 2005, requestedConnectionId: 20_005, generateIndex: 5, terrainHeight: 0);
    var result = hub.TryApplyPhysicalAttack(1, npcId, randomFactor: 99, out var outcome, specialDropRoll: 0, specialItemRoll: 3);
    Assert(result == LegacyPhysicalAttackResult.Accepted && outcome?.ItemDrops is { Count: 1 } && outcome.ItemDrops[0].Item.Index == 424, "The GenerateIndex-specific boss drop was not inserted into the attacker's carry.");

    var eventHub = new WorldHub();
    Assert(eventHub.Enter(1, "EVENT_HUNTER", (_, _) => ValueTask.CompletedTask), "Event-drop attacker was not registered.");
    var eventAttackerMob = attackerMob.ToArray();
    System.Text.Encoding.ASCII.GetBytes("EVENT_HUNTER").CopyTo(eventAttackerMob.AsSpan(LegacyAccountSnapshot.MobNameOffset));
    Assert(eventHub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, eventAttackerMob), "Event-drop attacker state was not set.");
    eventHub.ConfigureNpcEventDrop(new LegacyNpcEventDropConfiguration(true, 1, 3, 1, 1, 419, true));
    var eventNpcId = eventHub.EnterNpc(npcMob, 2005, 2005, requestedConnectionId: 20_006, terrainHeight: 0);
    var eventResult = eventHub.TryApplyPhysicalAttack(1, eventNpcId, randomFactor: 99, out var eventOutcome, eventDropRoll: 0);
    Assert(eventResult == LegacyPhysicalAttackResult.Accepted && eventOutcome?.ItemDrops is { Count: 1 } eventDrops && eventDrops[0].Item is { Index: 419, Effect1: 62, Value1: 0, Effect2: 63, Value2: 1, Effect3: 59 }, "The indexed global event drop did not preserve effects 62/63/59 or insertion order.");
    Assert(eventOutcome is not null && eventOutcome.GlobalNotices is { Count: 1 } && eventOutcome.GlobalNotices[0].Message == "EVENT_HUNTER recebeu item 419 (1).", "The enabled global event notice did not preserve the attacker, item, and event index.");
    var noticeCodec = LegacyFrameCodec.CreateDefault();
    var noticeFrame = noticeCodec.Decode(new MessagePanelConfirmation(eventOutcome!.GlobalNotices![0].Message).ToFrame(noticeCodec, 33, 16));
    Assert(noticeFrame.IsChecksumValid && noticeFrame.Header.Type == MessagePanelConfirmation.MessageType && noticeFrame.Header.Id == 0 && noticeFrame.Header.Size == MessagePanelConfirmation.PacketSize, "The global event notice did not use the legacy MSG_MessagePanel wire.");
    Assert(eventHub.TryGetNpcEventDropState(out var eventState) && eventState?.CurrentIndex == 2, "The global event drop index was not incremented atomically after the reward.");

    var runeReward = LegacyNpcDropMath.SelectRuneReward(5899, 0, roll: static _ => 0);
    Assert(runeReward is { ItemIndex: 5120, ProgressionValue: 6 }, "The Pista +5 boss rune table or progression item value differs from the legacy source.");
    Assert(LegacyNpcDropMath.SelectRuneReward(5653, 0, roll: static _ => 3, chanceRoll: static _ => 19) is { ItemIndex: 5113, ProgressionValue: 1 }, "The Pista +0 Lich did not preserve the 20 percent success gate and four-item rune table.");
    Assert(LegacyNpcDropMath.SelectRuneReward(5654, 0, roll: static _ => 0, chanceRoll: static _ => 20) is null, "The Pista +0 Lich reward ignored the legacy rand()%100 < 20 boundary.");
    var runeHub = new WorldHub();
    Assert(runeHub.Enter(1, "RUNE_LEADER", (_, _) => ValueTask.CompletedTask) && runeHub.Enter(2, "RUNE_MEMBER", (_, _) => ValueTask.CompletedTask), "Rune-party participants were not registered.");
    Assert(runeHub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob) && runeHub.SetCharacterState(2, 0, 0, 1, 0, 0, 2006, 2006, attackerMob), "Rune-party participant state was not set.");
    Assert(runeHub.TrySetParty(1, [1, 2]), "Rune-party fixture was not formed.");
    var runeNpcId = runeHub.EnterNpc(npcMob, 2005, 2005, requestedConnectionId: 20_007, generateIndex: 5767, terrainHeight: 0);
    var runeResult = runeHub.TryApplyPhysicalAttack(1, runeNpcId, randomFactor: 99, out var runeOutcome, runeRoll: 0);
    Assert(runeResult == LegacyPhysicalAttackResult.Accepted && runeOutcome?.ItemDrops is { Count: 2 } runeDrops && runeDrops.All(drop => drop.Item.Index == 5130) && runeDrops.Select(drop => drop.ConnectionId).OrderBy(id => id).SequenceEqual([1, 2]), "The Pista +6 boss did not distribute one selected rune to every connected party member.");
    Assert(runeHub.TryGetCharacterSnapshot(2, out var runeMemberMob, out _, out _) && runeMemberMob is not null && LegacyItem.Read(runeMemberMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset)).Index == 5130, "The Pista rune was not persisted in the non-leader member carry.");

    var pistaHub = new WorldHub();
    Assert(pistaHub.Enter(1, "PISTA_LEADER", (_, _) => ValueTask.CompletedTask), "Pista leader was not registered.");
    Assert(pistaHub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob), "Pista leader state was not set.");
    Assert(pistaHub.ConfigurePistaLv6State(1, 1), "Pista level 6 state was not configured.");
    var pistaNpcId = pistaHub.EnterNpc(npcMob, 2005, 2005, requestedConnectionId: 20_008, generateIndex: 5775, terrainHeight: 0);
    var pistaResult = pistaHub.TryApplyPhysicalAttack(1, pistaNpcId, randomFactor: 99, out var pistaOutcome);
    Assert(pistaResult == LegacyPhysicalAttackResult.Accepted && pistaOutcome?.TargetRemoved == true && pistaOutcome.PistaTransition is
        { PistaLevel: 6, LeaderConnectionId: 1, PreviousMobCount: 1, RemainingMobCount: 0, SpawnGenerateIndex: 5767 }, "The final Pista level 6 room mob did not decrement the counter and request boss 5767.");
    Assert(pistaHub.TryGetPistaLv6State(out var pistaState) && pistaState?.MobCount == 0, "The Pista level 6 mob counter was not persisted after the lethal attack.");

    var pendingPistaHub = new WorldHub();
    Assert(pendingPistaHub.Enter(1, "PISTA_PENDING", (_, _) => ValueTask.CompletedTask), "Second Pista leader was not registered.");
    Assert(pendingPistaHub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob), "Second Pista leader state was not set.");
    Assert(pendingPistaHub.ConfigurePistaLv6State(1, 2), "Second Pista level 6 state was not configured.");
    var pendingNpcId = pendingPistaHub.EnterNpc(npcMob, 2005, 2005, requestedConnectionId: 20_009, generateIndex: 5785, terrainHeight: 0);
    var pendingResult = pendingPistaHub.TryApplyPhysicalAttack(1, pendingNpcId, randomFactor: 99, out var pendingOutcome);
    Assert(pendingResult == LegacyPhysicalAttackResult.Accepted && pendingOutcome?.PistaTransition is
        { PistaLevel: 6, PreviousMobCount: 2, RemainingMobCount: 1, SpawnGenerateIndex: 0 }, "A non-final Pista level 6 mob incorrectly requested the boss spawn.");
}

static void WorldNpcSkill()
{
    var attackerMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var npcMob = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(50, 0, 0, 0, 0, 0, 0, 5000, 500, 5000, 500, 300, 0, 0, 0, 100, 20, 0, 0)
        .Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    attackerMob[LegacyAccountSnapshot.MobClassOffset] = 0;
    BinaryPrimitives.WriteInt32LittleEndian(attackerMob.AsSpan(LegacyAccountSnapshot.MobMagicOffset), 50);
    attackerMob[LegacyAccountSnapshot.MobEquipmentOffset + (6 * LegacyItem.SizeInBytes)] = 1;
    attackerMob[LegacyAccountSnapshot.MobEquipmentOffset + (7 * LegacyItem.SizeInBytes)] = 2;
    new LegacyScore(50, 40, 0, 0, 0, 0, 0, 100, 100, 1, 100, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    npcMob[LegacyAccountSnapshot.MobClanOffset] = 1;
    BinaryPrimitives.WriteInt64LittleEndian(npcMob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset), 1_000);

    var skill = new LegacySkillDefinition(10, 69, 1, 20, 15, 1, 1, 40, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 2, 0, 0, 1, 0, "Golpe_Mortal");
    var hub = new WorldHub();
    Assert(hub.Enter(1, "NPC_SKILL_HUNTER", (_, _) => ValueTask.CompletedTask), "NPC-skill attacker was not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob), "NPC-skill attacker state was not set.");
    var npcId = hub.EnterNpc(npcMob, 2005, 2005, requestedConnectionId: 20_001);
    var result = hub.TryApplySkillAttack(1, npcId, skill, CreateCombatItemDataTable(), weather: 0, randomFactor: 90, out var outcome);
    Assert(result == LegacySkillAttackResult.Accepted && outcome is not null && outcome.TargetRemoved && outcome.RemainingHp == 0, "An offensive skill did not resolve against and remove a persistent NPC.");
    Assert(outcome!.ExperienceAwarded == 1_000 && outcome.AttackerMobSnapshot is not null, "NPC skill death did not award solo experience or return the updated attacker snapshot.");
    Assert(hub.TryGetCombatState(1, out var attackerState) && attackerState!.Experience == 1_000, "NPC skill experience did not mutate the attacker's authoritative MOB state.");

    var lightningNpcId = hub.EnterNpc(npcMob, 2005, 2005, requestedConnectionId: 20_002);
    var lightningResult = hub.TryApplySkillAttack(1, lightningNpcId, skill with { Id = 79 }, CreateCombatItemDataTable(), weather: 0, randomFactor: 99, out var lightningOutcome);
    Assert(lightningResult == LegacySkillAttackResult.Accepted && lightningOutcome is not null && lightningOutcome.TargetRemoved, "Skill 79 did not use the NPC physical-damage branch.");

    var npcEffectSkill = new LegacySkillDefinition(8, 0, 0, 0, 0, 0, 0, 0, 4, 7, 4, 9, 20, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "NPC_EFEITO");
    var npcEffectId = hub.EnterNpc(npcMob, 2005, 2005, new byte[LegacyAccountSnapshot.AffectStride], requestedConnectionId: 20_003);
    var npcEffectResult = hub.TryApplySkillAttack(1, npcEffectId, npcEffectSkill, itemData: null, weather: 0, randomFactor: 90, out var npcEffectOutcome, affectRandomFactor: 0);
    Assert(npcEffectResult == LegacySkillAttackResult.Accepted && npcEffectOutcome is not null && npcEffectOutcome.TargetAffectSnapshot is { Length: LegacyAccountSnapshot.AffectStride } npcEffectAffect && npcEffectAffect[0] == 4 && npcEffectAffect[1] == 7 && BinaryPrimitives.ReadUInt16LittleEndian(npcEffectAffect.AsSpan(2)) == 20 && BinaryPrimitives.ReadUInt32LittleEndian(npcEffectAffect.AsSpan(4)) == 25, "An InstanceType 0 effect skill did not apply only its legacy Tick affect to a persistent NPC.");

    var npcHealingMob = npcMob.ToArray();
    new LegacyScore(50, 40, 0, 0, 0, 0, 0, 500, 100, 250, 100, 0, 0, 0, 0, 0, 0, 0, 0).Write(npcHealingMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    var npcHealingId = hub.EnterNpc(npcHealingMob, 2005, 2005, requestedConnectionId: 20_005);
    var npcHealingSkill = new LegacySkillDefinition(28, 0, 0, 0, 0, 0, 6, 20, 0, 0, 0, 0, 0, [], [], 0, 0, 0, 1, 0, 0, 0, 0, "Cura_NPC");
    var npcHealingResult = hub.TryApplySkillAttack(1, npcHealingId, npcHealingSkill, itemData: null, weather: 0, randomFactor: 90, out var npcHealingOutcome);
    Assert(npcHealingResult == LegacySkillAttackResult.Accepted && npcHealingOutcome is not null && npcHealingOutcome.Damage == -170 && npcHealingOutcome.RemainingHp == 420 && npcHealingOutcome.TargetHpChanged && LegacyMobCombatState.Read(npcHealingOutcome.TargetMobSnapshot).CurrentScore.Hp == 420, "The legacy InstanceType 6 NPC-healing path did not apply the authoritative heal or HP cap.");

    var npcDetoxAffect = new byte[LegacyAccountSnapshot.AffectStride];
    npcDetoxAffect[0] = 1;
    npcDetoxAffect[8] = 32;
    npcDetoxAffect[16] = 99;
    var npcDetoxId = hub.EnterNpc(npcMob, 2005, 2005, npcDetoxAffect, requestedConnectionId: 20_006);
    var npcDetoxSkill = new LegacySkillDefinition(30, 0, 0, 0, 0, 0, 8, 0, 0, 0, 0, 0, 0, [], [], 0, 0, 0, 1, 0, 0, 0, 0, "Desintoxicar_NPC");
    var npcDetoxResult = hub.TryApplySkillAttack(1, npcDetoxId, npcDetoxSkill, itemData: null, weather: 0, randomFactor: 90, out var npcDetoxOutcome);
    Assert(npcDetoxResult == LegacySkillAttackResult.Accepted && npcDetoxOutcome?.TargetAffectSnapshot is { Length: LegacyAccountSnapshot.AffectStride } npcDetoxSnapshot && npcDetoxSnapshot[0] == 0 && npcDetoxSnapshot[8] == 32 && npcDetoxSnapshot[16] == 99, "The NPC InstanceType 8 route did not preserve the legacy learned-skill exception or clear the detoxifiable affect.");

    var kingdomHub = new WorldHub();
    var kingdomNpcMob = npcMob.ToArray();
    BinaryPrimitives.WriteInt32LittleEndian(kingdomNpcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), 1);
    Assert(kingdomHub.Enter(1, "KINGDOM_ATTACKER", (_, _) => ValueTask.CompletedTask) && kingdomHub.Enter(2, "KINGDOM_INSIDE", (_, _) => ValueTask.CompletedTask) && kingdomHub.Enter(3, "KINGDOM_OUTSIDE", (_, _) => ValueTask.CompletedTask), "Kingdom-notice participants were not registered.");
    Assert(kingdomHub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob) && kingdomHub.SetCharacterState(2, 0, 0, 1, 0, 0, 1700, 1600, attackerMob) && kingdomHub.SetCharacterState(3, 0, 0, 1, 0, 0, 1800, 1600, attackerMob), "Kingdom-notice participant positions were not set.");
    var kingdomNpcId = kingdomHub.EnterNpc(kingdomNpcMob, 2005, 2005, requestedConnectionId: 20_007, generateIndex: 8, terrainHeight: 0);
    var kingdomResult = kingdomHub.TryApplySkillAttack(1, kingdomNpcId, skill, CreateCombatItemDataTable(), weather: 0, randomFactor: 99, out var kingdomOutcome);
    Assert(kingdomResult == LegacySkillAttackResult.Accepted && kingdomOutcome?.TargetRemoved == true && kingdomOutcome.AreaNotices is { Count: 1 } kingdomNotices && kingdomNotices[0].Message == "Reiniciar reino Hekalotia para guerreiro do rei Harbalade." && kingdomHub.GetCombatWorldState().Kingdom1Clear == 1, "The king death did not set the legacy kingdom-clear state or produce the area notice.");
    Assert(kingdomHub.GetParticipantIdsInArea(1676, 1556, 1776, 1636).SequenceEqual([2]), "The kingdom notice area filter did not match SendNoticeArea's inclusive rectangle.");

    var castleHub = new WorldHub();
    var castleAttackerMob = attackerMob.ToArray();
    System.Text.Encoding.ASCII.GetBytes("CASTLE_HERO").CopyTo(castleAttackerMob.AsSpan(LegacyAccountSnapshot.MobNameOffset));
    var castleNpcMob = npcMob.ToArray();
    BinaryPrimitives.WriteInt32LittleEndian(castleNpcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), 1);
    Assert(castleHub.Enter(1, "CASTLE_HERO", (_, _) => ValueTask.CompletedTask) && castleHub.Enter(2, "CASTLE_INSIDE", (_, _) => ValueTask.CompletedTask) && castleHub.Enter(3, "CASTLE_OUTSIDE", (_, _) => ValueTask.CompletedTask), "Castle-notice participants were not registered.");
    Assert(castleHub.SetCharacterState(1, 0, 0, 1, 0, 0, 2200, 1200, castleAttackerMob) && castleHub.SetCharacterState(2, 0, 0, 1, 0, 0, 2200, 1200, attackerMob) && castleHub.SetCharacterState(3, 0, 0, 1, 0, 0, 2301, 1200, attackerMob), "Castle-notice participant positions were not set.");
    var castleNpcId = castleHub.EnterNpc(castleNpcMob, 2200, 1200, requestedConnectionId: 20_008, generateIndex: 0, terrainHeight: 0);
    var castleResult = castleHub.TryApplySkillAttack(1, castleNpcId, skill, CreateCombatItemDataTable(), weather: 0, randomFactor: 99, out var castleOutcome);
    Assert(castleResult == LegacySkillAttackResult.Accepted && castleOutcome?.TargetRemoved == true && castleOutcome.AreaNotices is { Count: 1 } castleNotices && castleNotices[0].Message == "CASTLE_HERO derrotou Boss da Quest 2 Castelos.." && castleHub.GetCombatWorldState().CastleQuestClear == 1, "The Castle Zakum boss death did not set CastleQuestClear or produce the legacy area notice.");
    Assert(castleHub.GetParticipantIdsInArea(2176, 1160, 2300, 1276).SequenceEqual([1, 2]), "The Castle Zakum notice did not preserve the inclusive area bounds.");
    Assert(castleHub.TryProcessCastleQuestMinute(new DateTime(2026, 9, 16, 12, 0, 0), out var castleWarningPlan) && castleWarningPlan is not null && castleWarningPlan.PreviousState == 1 && castleWarningPlan.CurrentState == 2 && castleWarningPlan.AreaNotices is { Count: 1 } && castleWarningPlan.AreaNotices[0].Message == "Quest 2 Castelos reiniciar\u00e1 em 10 segundos.", "The Castle Zakum minute timer did not advance to state 2 or emit the reset warning.");
    var castleLeftoverId = castleHub.EnterNpc(castleNpcMob, 2201, 1201, requestedConnectionId: 20_009, generateIndex: 777, terrainHeight: 0);
    Assert(castleHub.TryProcessCastleQuestMinute(new DateTime(2026, 9, 16, 12, 1, 0), out var castleResetPlan) && castleResetPlan is not null && castleResetPlan.PreviousState == 2 && castleResetPlan.CurrentState == 0 && castleResetPlan.RemovedNpcs.Any(npc => npc.ConnectionId == castleLeftoverId) && castleHub.GetCombatWorldState().CastleQuestClear == 0, "The Castle Zakum minute timer did not clear the quest arena and reset state 2.");

    var npcFlameAffect = new byte[LegacyAccountSnapshot.AffectStride];
    npcFlameAffect[0] = 18;
    npcFlameAffect[8] = 16;
    npcFlameAffect[16] = 7;
    var npcFlameId = hub.EnterNpc(npcMob, 2005, 2005, npcFlameAffect, requestedConnectionId: 20_004);
    var npcFlameSkill = new LegacySkillDefinition(12, 0, 0, 0, 0, 0, 12, 0, 0, 0, 0, 0, 0, [], [], 0, 0, 0, 1, 0, 0, 0, 0, "CHAMAS_NPC");
    var npcFlameResult = hub.TryApplySkillAttack(1, npcFlameId, npcFlameSkill, itemData: null, weather: 0, randomFactor: 90, out var npcFlameOutcome);
    Assert(npcFlameResult == LegacySkillAttackResult.Accepted && npcFlameOutcome?.TargetAffectSnapshot is { Length: LegacyAccountSnapshot.AffectStride } npcFlameSnapshot && npcFlameSnapshot[0] == 0 && npcFlameSnapshot[8] == 0 && npcFlameSnapshot[16] == 7, "InstanceType 12 did not clear the legacy elemental affects on an NPC without applying the player-only MP burn.");

    Assert(hub.SetNpcCombatState(npcFlameId, mode: 1, currentTarget: 99, enemyList: [1, 2]), "NPC combat state fixture could not be prepared for the flash skill.");
    var flashSkill = new LegacySkillDefinition(7, 0, 0, 0, 0, 0, 7, 0, 0, 0, 0, 0, 0, [], [], 0, 0, 0, 1, 0, 0, 0, 0, "FLASH_NPC");
    var flashResult = hub.TryApplySkillAttack(1, npcFlameId, flashSkill, itemData: null, weather: 0, randomFactor: 90, out var flashOutcome);
    Assert(flashResult == LegacySkillAttackResult.Accepted && flashOutcome is not null && hub.TryGetNpcCombatState(npcFlameId, out var flashMode, out var flashTarget, out var flashEnemies) && flashMode == LegacyNpcMode.Peace && flashTarget == LegacyNpcMode.EmptyTarget && flashEnemies.Count == 0, "InstanceType 7 did not put the persistent NPC into legacy peace mode and clear its target/enemy list.");
}

static void WorldInvisibility()
{
    var casterMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var targetMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var npcMob = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(50, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    new LegacyScore(50, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    new LegacyScore(50, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));

    var hub = new WorldHub();
    Assert(hub.Enter(1, "INVISIBILITY_CASTER", (_, _) => ValueTask.CompletedTask), "Invisibility caster was not registered.");
    Assert(hub.Enter(2, "INVISIBILITY_TARGET", (_, _) => ValueTask.CompletedTask), "Invisibility target was not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, casterMob), "Invisibility caster state was not set.");
    Assert(hub.SetCharacterState(2, 0, 0, 1, 0, 0, 2005, 2005, targetMob), "Invisibility target state was not set.");
    var redirectedNpcId = hub.EnterNpc(npcMob, 2005, 2005, requestedConnectionId: 20_010);
    var untouchedNpcId = hub.EnterNpc(npcMob, 2005, 2005, requestedConnectionId: 20_011);
    Assert(hub.SetNpcCombatState(redirectedNpcId, LegacyNpcMode.Combat, 2, [2, 99, 2]), "The redirecting NPC combat state was not set.");
    Assert(hub.SetNpcCombatState(untouchedNpcId, LegacyNpcMode.Combat, 99, [2, 99]), "The unrelated NPC combat state was not set.");

    var invisibility = new LegacySkillDefinition(40, 0, 0, 0, 0, 0, 10, 0, 0, 0, 0, 0, 0, [], [], 0, 0, 0, 1, 0, 0, 0, 0, "Invisibilidade");
    var result = hub.TryApplySkillAttack(1, 2, invisibility, itemData: null, weather: 0, randomFactor: 90, out var outcome);
    var acceptedOutcome = outcome ?? throw new InvalidOperationException("InstanceType 10 did not return an outcome.");
    Assert(result == LegacySkillAttackResult.Accepted && acceptedOutcome.Damage == 0 && acceptedOutcome.TargetResourceChanged == false, "InstanceType 10 did not resolve as a non-damaging skill.");
    Assert(acceptedOutcome.NpcCombatStateUpdates is [{ ConnectionId: var updateId, Mode: LegacyNpcMode.Combat, CurrentTarget: 1, EnemyList: var enemies }]
        && updateId == redirectedNpcId
        && enemies.SequenceEqual([1, 99, 1]), "InstanceType 10 did not retarget the NPC and rewrite every matching enemy-list entry.");
    Assert(hub.TryGetNpcCombatState(redirectedNpcId, out _, out var redirectedTarget, out var redirectedEnemies)
        && redirectedTarget == 1 && redirectedEnemies.SequenceEqual([1, 99, 1]), "The authoritative NPC combat state was not mutated by invisibility.");
    Assert(hub.TryGetNpcCombatState(untouchedNpcId, out _, out var untouchedTarget, out var untouchedEnemies)
        && untouchedTarget == 99 && untouchedEnemies.SequenceEqual([2, 99]), "InstanceType 10 changed an NPC that was not targeting the invisible player.");
}

static void WorldNpcPartyExperience()
{
    var attackerMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var memberMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var npcMob = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(50, 0, 100, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 40, 0, 0, 0)
        .Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset, LegacyScore.SizeInBytes).CopyTo(memberMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    new LegacyScore(50, 10, 0, 0, 0, 0, 0, 100, 100, 1, 100, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    npcMob[LegacyAccountSnapshot.MobClanOffset] = 1;
    BinaryPrimitives.WriteInt64LittleEndian(npcMob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset), 1_000);

    var hub = new WorldHub();
    Assert(hub.Enter(1, "PARTY_LEADER", (_, _) => ValueTask.CompletedTask) && hub.Enter(2, "PARTY_MEMBER", (_, _) => ValueTask.CompletedTask), "Party-experience players were not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob) && hub.SetCharacterState(2, 0, 0, 1, 0, 0, 2006, 2006, memberMob), "Party-experience player state was not set.");
    Assert(hub.TrySetParty(1, [1, 2]), "The legacy party membership was not registered.");
    var npcId = hub.EnterNpc(npcMob, 2005, 2005, requestedConnectionId: 20_003);

    var result = hub.TryApplyPhysicalAttack(1, npcId, randomFactor: 99, out var outcome);
    Assert(result == LegacyPhysicalAttackResult.Accepted && outcome is not null && outcome.ExperienceAwards?.Count == 2, "A party kill did not produce two eligible experience awards.");
    Assert(outcome!.ExperienceAwarded == 850, "The generic legacy party experience adjustment did not apply the 15 percent ordinary-server reduction.");
    Assert(outcome.ExperienceAwards!.All(award => award.Experience == 850), "Party members did not receive the same generic legacy experience share in the deterministic fixture.");
    Assert(hub.TryGetCombatState(1, out var leaderState) && leaderState!.Experience == 850 && hub.TryGetCombatState(2, out var memberState) && memberState!.Experience == 850, "Party experience did not mutate both authoritative MOB snapshots.");
}

static void WorldExperienceHold()
{
    var attackerMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var npcMob = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(50, 0, 100, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 40, 0, 0, 0)
        .Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    new LegacyScore(50, 10, 0, 0, 0, 0, 0, 100, 100, 1, 100, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    npcMob[LegacyAccountSnapshot.MobClanOffset] = 1;
    BinaryPrimitives.WriteInt64LittleEndian(npcMob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset), 1_000);

    var hub = new WorldHub();
    Assert(hub.Enter(1, "HOLD_HUNTER", (_, _) => ValueTask.CompletedTask), "Hold-experience player was not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob), "Hold-experience player state was not set.");
    Assert(hub.TrySetExperienceHold(1, 250), "Experience hold was not written into the authoritative MOBEXTRA state.");
    var npcId = hub.EnterNpc(npcMob, 2005, 2005, requestedConnectionId: 20_004);

    var result = hub.TryApplyPhysicalAttack(1, npcId, randomFactor: 99, out var outcome);
    Assert(result == LegacyPhysicalAttackResult.Accepted && outcome is not null && outcome.ExperienceAwarded == 750, "Experience hold was not consumed before awarding NPC experience.");
    Assert(hub.TryGetExperienceHold(1, out var remainingHold) && remainingHold == 0, "Consumed experience hold was not cleared from MOBEXTRA.");
    Assert(hub.TryGetExperienceDayLog(1, out var dayLogExperience, out var dayLogYear) && dayLogExperience == 1_000 && dayLogYear == DateTime.Now.DayOfYear - 1, "Daily experience log did not record the pre-Hold legacy XP amount.");
    Assert(hub.TryGetCombatState(1, out var attackerState) && attackerState!.Experience == 750, "Held experience did not mutate the authoritative MOB state by the remaining amount.");
}

static void PartyExperienceModifiers()
{
    var eligibility = new LegacyExperienceEligibility(LegacyAccountSnapshot.ClassMasterMortal);
    Assert(LegacyPartyExperienceMath.GetGenericMemberExperience(eligibility, 1_000, 50, 1_000, 50) == 850, "Ordinary party XP did not keep the legacy 15 percent reduction.");
    Assert(LegacyPartyExperienceMath.GetGenericMemberExperience(eligibility, 1_000, 50, 1_000, 50, newbieEventServer: true) == 1_250, "Newbie event XP did not apply the legacy 25 percent increase.");
    Assert(LegacyPartyExperienceMath.GetGenericMemberExperience(eligibility, 1_000, 50, 1_000, 50, doubleMode: true) == 1_700, "Double XP mode did not apply before the ordinary-server reduction.");
    Assert(LegacyPartyExperienceMath.GetGenericMemberExperience(eligibility, 1_000, 50, 1_000, 50, kefraLive: false) == 425, "Kefra offline mode did not halve XP before the ordinary-server reduction.");
}

static void ExperienceBonus()
{
    var itemData = CreateCombatItemDataTable();
    var mob = new byte[LegacyAccountSnapshot.CharacterStride];
    var affect = new byte[LegacyAccountSnapshot.AffectStride];
    var gradeAndGem = new LegacyItem(5, 116, 232, 0, 0, 0, 0);
    gradeAndGem.Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset));
    new LegacyItem(3902, 0, 0, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (13 * LegacyItem.SizeInBytes)));
    affect[0] = 39;

    Assert(itemData.GetItemGem(gradeAndGem) == 2, "The legacy sanction encoding did not decode gem level 2.");
    Assert(LegacyExperienceMath.GetEquipmentExperienceBonus(mob, affect, itemData) == 136, "Equipment, fairy, and affect XP bonuses did not match the legacy accumulation.");

    new LegacyScore(50, 0, 100, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 40, 0, 0, 0)
        .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    var npcMob = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(50, 10, 0, 0, 0, 0, 0, 100, 100, 1, 100, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    npcMob[LegacyAccountSnapshot.MobClanOffset] = 1;
    BinaryPrimitives.WriteInt64LittleEndian(npcMob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset), 1_000);
    var hub = new WorldHub(itemData: itemData);
    Assert(hub.Enter(1, "BONUS_HUNTER", (_, _) => ValueTask.CompletedTask) && hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, mob, LegacyAccountSnapshot.ClassMasterMortal, affect), "XP-bonus player state was not set.");
    var npcId = hub.EnterNpc(npcMob, 2005, 2005, requestedConnectionId: 20_005);
    var result = hub.TryApplyPhysicalAttack(1, npcId, randomFactor: 99, out var outcome);
    Assert(result == LegacyPhysicalAttackResult.Accepted && outcome?.ExperienceAwarded == 2_360, "The authoritative NPC XP path did not apply the attacker ExpBonus before Hold.");
}

static void WorldSkillAttack()
{
    var attackerMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var targetMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var secondTargetMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var targetAffect = new byte[LegacyAccountSnapshot.AffectStride];
    var attackerScore = new LegacyScore(50, 0, 0, 0, 0, 0, 0, 5000, 500, 5000, 500, 300, 0, 0, 0, 100, 20, 0, 0);
    var targetScore = new LegacyScore(50, 40, 0, 0, 0, 0, 0, 5000, 500, 5000, 500, 0, 0, 0, 0, 0, 0, 0, 0);
    attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    targetScore.Write(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    targetScore.Write(secondTargetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    attackerMob[LegacyAccountSnapshot.MobClassOffset] = 0;
    targetMob[LegacyAccountSnapshot.MobClassOffset] = 0;
    secondTargetMob[LegacyAccountSnapshot.MobClassOffset] = 0;
    BinaryPrimitives.WriteInt32LittleEndian(attackerMob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), (1 << 9) | (1 << 7));
    BinaryPrimitives.WriteInt32LittleEndian(attackerMob.AsSpan(LegacyAccountSnapshot.MobMagicOffset), 50);
    attackerMob[LegacyAccountSnapshot.MobEquipmentOffset + (6 * LegacyItem.SizeInBytes)] = 1;
    attackerMob[LegacyAccountSnapshot.MobEquipmentOffset + (7 * LegacyItem.SizeInBytes)] = 2;
    targetAffect[0] = 1;
    targetAffect[8] = 32;
    targetAffect[16] = 99;
    BinaryPrimitives.WriteUInt16LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), 0x80);

    var skill = new LegacySkillDefinition(10, 69, 1, 20, 15, 1, 1, 40, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 2, 0, 0, 1, 0, "Golpe_Mortal");
    var hub = new WorldHub();
    var itemData = CreateCombatItemDataTable();
    var attackerState = LegacyMobCombatState.Read(attackerMob);
    var firstWeapon = LegacyItem.Read(attackerMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (6 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
    var secondWeapon = LegacyItem.Read(attackerMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (7 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
    Assert(LegacySkillCombatMath.GetWeaponDamage(attackerState, itemData, firstWeapon, secondWeapon) == 140, $"Skill fixture weapon damage was not 140: {LegacySkillCombatMath.GetWeaponDamage(attackerState, itemData, firstWeapon, secondWeapon)}.");
    var directBaseDamage = LegacySkillCombatMath.GetSkillBaseDamage(skill, attackerState, weather: 0, weaponDamage: 140, magic: 50);
    Assert(directBaseDamage == 1787, $"Skill fixture base damage was not 1787: {directBaseDamage}; class={attackerState.CharacterClass}, level={attackerState.CurrentScore.Level}, strength={attackerState.CurrentScore.Strength}, special2={attackerState.CurrentScore.Special2}, special3={attackerState.CurrentScore.Special3}, type={skill.InstanceType}, id={skill.Id}.");
    Assert(hub.Enter(1, "ATTACKER", (_, _) => ValueTask.CompletedTask) && hub.Enter(2, "TARGET", (_, _) => ValueTask.CompletedTask) && hub.Enter(3, "TARGET_TWO", (_, _) => ValueTask.CompletedTask), "Skill-combat participants were not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob) && hub.SetCharacterState(2, 0, 0, 2, 0, 0, 2005, 2005, targetMob, LegacyAccountSnapshot.ClassMasterMortal, targetAffect) && hub.SetCharacterState(3, 0, 0, 2, 0, 0, 2006, 2006, secondTargetMob), "Skill-combat participant state was not set.");

    var result = hub.TryApplySkillAttack(1, 2, skill, itemData, weather: 0, randomFactor: 90, out var outcome);
    Assert(result == LegacySkillAttackResult.Accepted && outcome is not null && outcome.BaseDamage == 1787 && outcome.Damage == 2358 && outcome.RemainingHp == 2642, $"Elemental skill damage did not match the legacy base, defense, and resistance path: result={result}, base={outcome?.BaseDamage}, damage={outcome?.Damage}, hp={outcome?.RemainingHp}, resistance={outcome?.Resistance}.");
    Assert(hub.TryGetCombatState(2, out var targetState) && targetState!.CurrentScore.Hp == 2642, "Elemental skill damage did not mutate target HP atomically.");
    Assert(hub.TryGetResourceState(2, out var targetResources, out var targetRequestedHp, out _) && targetResources!.CurrentScore.Hp == 2642 && targetRequestedHp == 2642 && outcome!.TargetHpChanged, "Elemental skill did not synchronize the target ReqHp state used by SendSetHpMp.");
    var skillScrollTargetMob = new byte[LegacyAccountSnapshot.CharacterStride];
    (targetScore with { Hp = 1 }).Write(skillScrollTargetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    new LegacyItem(3463, 61, 1, 0, 0, 0, 0).Write(skillScrollTargetMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
    var skillScrollHub = new WorldHub();
    Assert(skillScrollHub.Enter(1, "SKILL_ATTACKER", (_, _) => ValueTask.CompletedTask) && skillScrollHub.Enter(2, "SKILL_SCROLL_TARGET", (_, _) => ValueTask.CompletedTask), "Skill resurrection-scroll participants were not registered.");
    Assert(skillScrollHub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob) && skillScrollHub.SetCharacterState(2, 0, 0, 2, 0, 0, 2005, 2005, skillScrollTargetMob), "Skill resurrection-scroll fixture was not set.");
    var skillScrollResult = skillScrollHub.TryApplySkillAttack(1, 2, skill, itemData, weather: 0, randomFactor: 90, out var skillScrollOutcome);
    Assert(skillScrollResult == LegacySkillAttackResult.Accepted && skillScrollOutcome is not null && skillScrollOutcome.TargetRevived && skillScrollOutcome.TargetResourceChanged && skillScrollOutcome.RemainingHp == 5000 && skillScrollOutcome.ConsumedItemSlot == 0 && skillScrollOutcome.ConsumedItem is { Index: 0, Effect1: 0, Value1: 0, Effect2: 0, Value2: 0, Effect3: 0, Value3: 0 }, "A lethal skill did not consume the legacy resurrection scroll and restore maximum HP/MP.");

    var skillParryTarget = targetMob.ToArray();
    skillParryTarget[LegacyAccountSnapshot.MobEquipmentOffset + LegacyItem.SizeInBytes] = 4;
    Assert(hub.SetCharacterState(2, 0, 0, 2, 0, 0, 2005, 2005, skillParryTarget, LegacyAccountSnapshot.ClassMasterMortal, targetAffect), "Skill parry target fixture could not be equipped.");
    var skillParryResult = hub.TryApplySkillAttack(1, 2, skill, itemData, weather: 0, randomFactor: 90, out var skillParryOutcome, parryRandomRoll: 50);
    Assert(skillParryResult == LegacySkillAttackResult.Accepted && skillParryOutcome is not null && skillParryOutcome.Damage == -3 && skillParryOutcome.RemainingHp == 5000, "Elemental parry did not emit -3 while preserving HP.");
    var secondResult = hub.TryApplySkillAttack(1, 3, skill, itemData, weather: 0, randomFactor: 90, out var secondOutcome);
    Assert(secondResult == LegacySkillAttackResult.Accepted && secondOutcome is not null && secondOutcome.Damage == 2358 && hub.TryGetCombatState(3, out var secondTargetState) && secondTargetState!.CurrentScore.Hp == 2642, "The same elemental resolver did not apply independently to a second target.");
    Assert(hub.SetCombatEligibilityState(1, pkMode: false, guilty: false, mapAttribute: 0x40) && hub.SetCombatEligibilityState(3, pkMode: false, guilty: false, mapAttribute: 0), "Skill peaceful-map eligibility fixture was not set.");
    Assert(hub.TryApplySkillAttack(1, 3, skill, itemData, weather: 0, randomFactor: 90, out _) == LegacySkillAttackResult.PeacefulZoneBlocked, "Aggressive skill damage was accepted against a non-PK, non-guilty target in a peaceful map.");
    hub.SetCombatWorldState(new LegacyCombatWorldState(CastleState: 1));
    Assert(hub.TryApplySkillAttack(1, 3, skill, itemData, weather: 0, randomFactor: 90, out _) == LegacySkillAttackResult.Accepted, "Active castle-war state did not disable the peaceful-map gate.");
    hub.SetCombatWorldState(new LegacyCombatWorldState());
    Assert(hub.SetCombatEligibilityState(1, pkMode: false, guilty: false, mapAttribute: 0), "Skill combat fixture could not leave the peaceful-map area.");

    var healTarget = skillParryTarget.ToArray();
    BinaryPrimitives.WriteInt32LittleEndian(healTarget.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), 2642);
    Assert(hub.SetCharacterState(2, 0, 0, 2, 0, 0, 2005, 2005, healTarget, LegacyAccountSnapshot.ClassMasterMortal, targetAffect), "Healing target fixture could not be restored after the parry case.");
    var healSkill = new LegacySkillDefinition(28, 0, 0, 0, 0, 0, 6, 20, 0, 0, 0, 0, 0, [], [], 0, 0, 0, 1, 0, 0, 0, 0, "Cura");
    var healResult = hub.TryApplySkillAttack(1, 2, healSkill, itemData: null, weather: 0, randomFactor: 90, out var healOutcome);
    Assert(healResult == LegacySkillAttackResult.Accepted && healOutcome is not null && healOutcome.Damage == -170 && healOutcome.RemainingHp == 2812, $"Healing did not match the legacy InstanceType 6 path: result={healResult}, damage={healOutcome?.Damage}, hp={healOutcome?.RemainingHp}.");
    Assert(hub.TryGetCombatState(2, out var healedTargetState) && healedTargetState!.CurrentScore.Hp == 2812, "Healing did not mutate target HP atomically.");
    Assert(hub.TryGetResourceState(2, out var healedResources, out var healedRequestedHp, out _) && healedResources!.CurrentScore.Hp == 2812 && healedRequestedHp == 2812 && healOutcome!.TargetHpChanged, "Healing did not synchronize the target ReqHp state used by SendSetHpMp.");

    var detoxSkill = new LegacySkillDefinition(30, 0, 0, 0, 0, 0, 8, 0, 0, 0, 0, 0, 0, [], [], 0, 0, 0, 1, 0, 0, 0, 0, "Desintoxicar");
    var detoxResult = hub.TryApplySkillAttack(1, 2, detoxSkill, itemData: null, weather: 0, randomFactor: 90, out var detoxOutcome);
    Assert(detoxResult == LegacySkillAttackResult.Accepted && detoxOutcome is not null && detoxOutcome.Damage == 0 && detoxOutcome.RemainingHp == 2812 && detoxOutcome.TargetAffectSnapshot is not null, "Detox did not accept the legacy InstanceType 8 route and produce an affect snapshot.");
    Assert(detoxOutcome!.TargetAffectSnapshot![0] == 0 && detoxOutcome.TargetAffectSnapshot[8] == 0 && detoxOutcome.TargetAffectSnapshot[16] == 99, "Detox cleared the wrong affect types or missed the learned type-32 exception.");
    var detoxScore = LegacyFrameCodec.CreateDefault().Decode(new UpdateScoreConfirmation(detoxOutcome.TargetMobSnapshot, detoxOutcome.TargetAffectSnapshot).ToFrame(LegacyFrameCodec.CreateDefault(), 33, 16, 2));
    Assert(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(detoxScore.Payload.Span[50..]) == 0 && System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(detoxScore.Payload.Span[124..]) == 2812, "Detox score relay did not encode the cleared affect and authoritative HP.");

    var buffSkill = new LegacySkillDefinition(8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 7, 20, [], [], 0, 0, 0, 1, 0, 0, 0, 0, "Buff");
    var buffResult = hub.TryApplySkillAttack(1, 2, buffSkill, itemData: null, weather: 0, randomFactor: 90, out var buffOutcome);
    Assert(buffResult == LegacySkillAttackResult.Accepted && buffOutcome?.TargetAffectSnapshot is not null && buffOutcome.TargetAffectSnapshot[0] == 4 && buffOutcome.TargetAffectSnapshot[1] == 7 && BinaryPrimitives.ReadUInt16LittleEndian(buffOutcome.TargetAffectSnapshot.AsSpan(2)) == 20 && BinaryPrimitives.ReadUInt32LittleEndian(buffOutcome.TargetAffectSnapshot.AsSpan(4)) == 25, "Generic SetAffect did not reproduce type, value, level, and duration.");

    var aggressiveSkill = buffSkill with { AffectType = 5, AffectValue = 9, Aggressive = 1 };
    var blockedResult = hub.TryApplySkillAttack(1, 2, aggressiveSkill, itemData: null, weather: 0, randomFactor: 90, out var blockedOutcome, affectRandomFactor: 0);
    Assert(blockedResult == LegacySkillAttackResult.Accepted && blockedOutcome?.TargetAffectSnapshot is null, "RSV_BLOCK did not prevent an aggressive affect while preserving the attack result.");

    var targetWithoutRsvBlock = targetMob.ToArray();
    BinaryPrimitives.WriteUInt16LittleEndian(targetWithoutRsvBlock.AsSpan(LegacyAccountSnapshot.MobRsvOffset), 0);
    Assert(hub.SetCharacterState(2, 0, 0, 2, 0, 0, 2005, 2005, targetWithoutRsvBlock, LegacyAccountSnapshot.ClassMasterMortal, new byte[LegacyAccountSnapshot.AffectStride]), "Could not reset the target fixture for affect-resistance validation.");
    var resistedSkill = aggressiveSkill with { AffectType = 6, AffectResist = 3 };
    var resistedResult = hub.TryApplySkillAttack(1, 2, resistedSkill, itemData: null, weather: 0, randomFactor: 90, out var resistedOutcome, affectRandomFactor: 4);
    Assert(resistedResult == LegacySkillAttackResult.Accepted && resistedOutcome?.TargetAffectSnapshot is null, "AffectResist did not reject an aggressive affect above the legacy resistance threshold.");

    var resurrectionHub = new WorldHub();
    var deadResurrectionMob = attackerMob.ToArray();
    BinaryPrimitives.WriteInt32LittleEndian(deadResurrectionMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), 0);
    var resurrectionSkill = new LegacySkillDefinition(99, 35, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, [], [], 0, 0, 0, 1, 0, 0, 0, 0, "Ressureicao");
    Assert(resurrectionHub.Enter(1, "DEAD", (_, _) => ValueTask.CompletedTask) && resurrectionHub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, deadResurrectionMob), "Resurrection fixture was not registered.");
    var resurrectionResult = resurrectionHub.TryApplySkillAttack(1, 1, resurrectionSkill, itemData: null, weather: 0, randomFactor: 90, out var resurrectionOutcome, resurrectionRoll: 40, resurrectionHpRoll: 9, resurrectionMpRoll: 19, recallCityRandomX: 3, recallCityRandomY: 7);
    Assert(resurrectionResult == LegacySkillAttackResult.Accepted && resurrectionOutcome is not null && resurrectionOutcome.UpdatesAttackerState && resurrectionOutcome.HasRecallPosition && resurrectionOutcome.RecallPositionX == 2089 && resurrectionOutcome.RecallPositionY == 2100 && resurrectionOutcome.RemainingHp == 500 && BinaryPrimitives.ReadInt32LittleEndian(resurrectionOutcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 28)) == 100, "Skill 99 did not reproduce the deterministic recall and final HP/MP resurrection rolls.");
    Assert(resurrectionHub.TryGetResourceState(1, out var revivedState, out var requestedHp, out var requestedMp) && revivedState!.CurrentScore.Hp == 500 && revivedState.CurrentMana == 100 && requestedHp == 500 && requestedMp == 100, "Skill 99 did not update authoritative HP/MP and requested mana state.");

    var deathFlowHub = new WorldHub();
    var deathFlowVictim = targetMob.ToArray();
    BinaryPrimitives.WriteInt32LittleEndian(deathFlowVictim.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), 1);
    Assert(deathFlowHub.Enter(1, "KILLER", (_, _) => ValueTask.CompletedTask) && deathFlowHub.Enter(2, "VICTIM", (_, _) => ValueTask.CompletedTask), "Death/resurrection flow participants were not registered.");
    Assert(deathFlowHub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob) && deathFlowHub.SetCharacterState(2, 0, 0, 1, 0, 0, 2005, 2005, deathFlowVictim), "Death/resurrection flow fixture was not registered.");
    Assert(deathFlowHub.TryApplyPhysicalAttack(1, 2, 99, out var lethalOutcome) == LegacyPhysicalAttackResult.Accepted && lethalOutcome is not null && lethalOutcome.TargetDied && lethalOutcome.RemainingHp == 0, "A lethal physical attack did not transition the victim to HP zero.");
    var flowReviveResult = deathFlowHub.TryApplySkillAttack(2, 2, resurrectionSkill, itemData: null, weather: 0, randomFactor: 90, out var flowReviveOutcome, resurrectionRoll: 40, resurrectionHpRoll: 9, resurrectionMpRoll: 19, recallCityRandomX: 3, recallCityRandomY: 7);
    Assert(flowReviveResult == LegacySkillAttackResult.Accepted && flowReviveOutcome is not null && flowReviveOutcome.UpdatesAttackerState && flowReviveOutcome.RemainingHp == 500 && flowReviveOutcome.HasRecallPosition && flowReviveOutcome.RecallPositionX == 2089 && flowReviveOutcome.RecallPositionY == 2100, "The integrated lethal-attack to skill-99 resurrection flow did not restore the legacy HP/MP and recall state.");
    Assert(deathFlowHub.TryGetResourceState(2, out var flowRevivedState, out var flowRequestedHp, out var flowRequestedMp) && flowRevivedState!.CurrentScore.Hp == 500 && flowRevivedState.CurrentMana == 100 && flowRequestedHp == 500 && flowRequestedMp == 100, "The integrated resurrection flow did not update authoritative resources.");

    var occupiedRecallMob = new byte[LegacyAccountSnapshot.CharacterStride];
    Assert(resurrectionHub.Enter(2, "OCCUPIED", (_, _) => ValueTask.CompletedTask) && resurrectionHub.SetCharacterState(2, 0, 0, 0, 0, 0, 2089, 2100, occupiedRecallMob), "Occupied recall cell fixture was not registered.");
    Assert(resurrectionHub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, deadResurrectionMob), "Dead resurrection state could not be restored for the occupied-cell check.");
    var occupiedRecallResult = resurrectionHub.TryApplySkillAttack(1, 1, resurrectionSkill, itemData: null, weather: 0, randomFactor: 90, out var occupiedRecallOutcome, resurrectionRoll: 40, resurrectionHpRoll: 9, resurrectionMpRoll: 19, recallCityRandomX: 3, recallCityRandomY: 7);
    Assert(occupiedRecallResult == LegacySkillAttackResult.Accepted && occupiedRecallOutcome?.RecallPositionX == 2088 && occupiedRecallOutcome.RecallPositionY == 2099, "Skill 99 recall did not move to the first free cell in the legacy radius-1 scan.");
    Assert(resurrectionHub.TryApplySkillAttack(1, 1, resurrectionSkill, itemData: null, weather: 0, randomFactor: 90, out var aliveResurrectionOutcome, resurrectionRoll: 114, resurrectionHpRoll: 49, resurrectionMpRoll: 49) == LegacySkillAttackResult.Accepted && aliveResurrectionOutcome?.RemainingHp == 500, "Skill 99 changed a living attacker instead of following the legacy dead-only branch.");
}

static void WorldSummonSkill()
{
    var attackerMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var targetMob = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(100, 0, 0, 0, 0, 0, 0, 1000, 1000, 1000, 100, 0, 0, 0, 0, 0, 40, 0, 0).Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    new LegacyScore(100, 0, 0, 0, 0, 0, 0, 1000, 1000, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0).Write(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    attackerMob[LegacyAccountSnapshot.MobClassOffset] = 1;
    BinaryPrimitives.WriteUInt32LittleEndian(attackerMob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), 1u << 18);

    var teleport = new LegacySkillDefinition(42, 39, 0, 0, 1, 0, 9, 0, 0, 0, 2, 1, 99, [], [], 0, 0, 0, 1, 1, 0, 0, 0, "Teleporte");
    var hub = new WorldHub();
    Assert(hub.Enter(1, "CASTER", (_, _) => ValueTask.CompletedTask) && hub.Enter(2, "TARGET", (_, _) => ValueTask.CompletedTask), "Teleport-skill participants were not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob) && hub.SetCharacterState(2, 0, 0, 1, 0, 0, 2005, 2005, targetMob), "Teleport-skill participant state was not set.");

    var result = hub.TryApplySkillAttack(1, 2, teleport, itemData: null, weather: 0, randomFactor: 90, out var outcome);
    Assert(result == LegacySkillAttackResult.Accepted && outcome is not null && outcome.HasTargetPosition && outcome.TargetPositionX == 1999 && outcome.TargetPositionY == 1999, $"InstanceType 9 did not reproduce DoSummon's first free-cell movement: result={result}, position=({outcome?.TargetPositionX},{outcome?.TargetPositionY}).");
    Assert(hub.TryGetCombatState(2, out var targetState) && targetState!.CurrentScore.Hp == 100, "Teleport skill changed target HP instead of only moving the target.");

    var blockedHub = new WorldHub();
    Assert(blockedHub.Enter(1, "CASTER", (_, _) => ValueTask.CompletedTask) && blockedHub.Enter(2, "TARGET", (_, _) => ValueTask.CompletedTask), "Teleport map-gate participants were not registered.");
    Assert(blockedHub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob) && blockedHub.SetCharacterState(2, 0, 0, 1, 0, 0, 2005, 2005, targetMob) && blockedHub.SetCombatEligibilityState(1, false, false, 0x04), "Teleport map-gate fixture was not set.");
    Assert(blockedHub.TryApplySkillAttack(1, 2, teleport, itemData: null, weather: 0, randomFactor: 90, out _) == LegacySkillAttackResult.SummonNotAllowedHere, "InstanceType 9 ignored the legacy map attribute summon restriction.");
}

static void WorldEtherealFlames()
{
    var attackerMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var targetMob = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(100, 0, 0, 0, 0, 0, 0, 1000, 1000, 1000, 100, 0, 0, 0, 0, 0, 70, 0, 0).Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
    new LegacyScore(100, 0, 0, 0, 0, 0, 0, 1000, 1000, 1000, 100, 0, 0, 0, 0, 0, 0, 0, 0).Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    new LegacyScore(100, 0, 0, 0, 0, 0, 0, 1000, 1000, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0).Write(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));

    var skill = new LegacySkillDefinition(49, 36, 1, 35, 30, 6, 12, 0, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "Chamas_Eter...");
    var targetAffect = new byte[LegacyAccountSnapshot.AffectStride];
    targetAffect[0] = 18;
    var hub = new WorldHub();
    Assert(hub.Enter(1, "CASTER", (_, _) => ValueTask.CompletedTask) && hub.Enter(2, "TARGET", (_, _) => ValueTask.CompletedTask), "Ethereal-flame participants were not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob) && hub.SetCharacterState(2, 0, 0, 1, 0, 0, 2005, 2005, targetMob, LegacyAccountSnapshot.ClassMasterMortal, targetAffect), "Ethereal-flame participant state was not set.");

    var burnResult = hub.TryApplySkillAttack(1, 2, skill, itemData: null, weather: 0, randomFactor: 90, out var burnOutcome, instanceRandomFactor: 11, burnRandomFactor: 0);
    Assert(burnResult == LegacySkillAttackResult.Accepted && burnOutcome?.TargetResourceChanged == true, "InstanceType 12 did not accept the deterministic burn branch.");
    Assert(hub.TryGetResourceState(2, out var burnedTarget, out var requestedHp, out var requestedMp) && burnedTarget!.CurrentMana == 90 && requestedHp == 100 && requestedMp == 90, "Ethereal Flames did not apply the legacy 10 percent MP burn and requested resource update.");

    BinaryPrimitives.WriteInt32LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentMpOffset), 100);
    targetAffect[0] = 18;
    Assert(hub.SetCharacterState(2, 0, 0, 1, 0, 0, 2005, 2005, targetMob, LegacyAccountSnapshot.ClassMasterMortal, targetAffect), "Ethereal-flame target could not be reset for the cleanse branch.");
    var cleanseResult = hub.TryApplySkillAttack(1, 2, skill, itemData: null, weather: 0, randomFactor: 90, out var cleanseOutcome, instanceRandomFactor: 0, burnRandomFactor: 0);
    Assert(cleanseResult == LegacySkillAttackResult.Accepted && cleanseOutcome?.TargetAffectSnapshot is not null && cleanseOutcome.TargetAffectSnapshot[0] == 0, "InstanceType 12 did not clear the legacy elemental affects when the burn roll failed.");
}

static void WorldNpcSummon()
{
    var run = FindReference769TmsrvRun();
    var catalog = LegacySummonCatalog.Load(Path.Combine(run, "BaseSummon"));
    Assert(catalog.TryGet(0, out var condor) && condor is not null && condor.MobSnapshot.Length == LegacyAccountSnapshot.CharacterStride, "The real BaseSummon catalog did not load slot 0.");
    Assert(catalog.TryGet(40, out var cavaleiroArcano) && cavaleiroArcano?.SourceName == "Cav._Arcano" &&
        catalog.TryGet(41, out var arqueiroArcano) && arqueiroArcano?.SourceName == "Arq._Arcano" &&
        catalog.TryGet(42, out var magoArcano) && magoArcano?.SourceName == "Mag._Arcano" &&
        !catalog.TryGet(43, out _), "The 7.69 Arcano summon templates were not loaded into slots 40..42 or an unavailable slot was accepted.");

    var attackerMob = new byte[LegacyAccountSnapshot.CharacterStride];
    var baseScore = new LegacyScore(100, 10, 30, 0, 0, 0, 0, 1000, 1000, 1000, 100, 0, 0, 0, 0, 0, 0, 0, 0);
    baseScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
    new LegacyScore(100, 10, 30, 0, 0, 0, 0, 1000, 1000, 1000, 100, 0, 120, 0, 0, 0, 0, 40, 0).Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    attackerMob[LegacyAccountSnapshot.MobSaveManaOffset] = 0;
    var mountOffset = LegacyAccountSnapshot.MobEquipmentOffset + (14 * LegacyItem.SizeInBytes);
    new LegacyItem(2330, 0, 0, 0, 0, 0, 0).Write(attackerMob.AsSpan(mountOffset, LegacyItem.SizeInBytes));
    BinaryPrimitives.WriteInt16LittleEndian(attackerMob.AsSpan(mountOffset + 2), 1);

    var skill = new LegacySkillDefinition(50, 20, 0, 20, 0, 0, 11, 8, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "Evocar");
    var hub = new WorldHub(summonCatalog: catalog, itemData: CreateCombatItemDataTable());
    Assert(hub.Enter(1, "SUMMONER", (_, _) => ValueTask.CompletedTask) && hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob), "NPC-summon participant was not registered.");
    Assert(hub.TryConsumeSkillMana(1, skill, out _, out _, out _) == LegacySkillManaResult.Accepted, "NPC-summon mana could not be consumed for the success path.");

    var result = hub.TryApplySkillAttack(1, 1, skill, itemData: null, weather: 0, randomFactor: 90, out var outcome);
    Assert(result == LegacySkillAttackResult.Accepted && outcome?.SummonResult == LegacySummonGenerationResult.Created && outcome.SummonedMobs?.Count == 1, "InstanceType 11 did not create the requested legacy summon.");
    var summoned = outcome!.SummonedMobs![0];
    Assert(summoned.ConnectionId >= 1000 && summoned.PositionX == 1999 && summoned.PositionY == 1999 && summoned.AffectSnapshot[0] == 24, "NPC summon did not use a MAX_USER slot, first free grid cell, or legacy affect 24.");
    var summonedScore = LegacyScore.Read(summoned.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    Assert(summonedScore.Hp == summonedScore.MaxHp && summonedScore.Level == 100 && summoned.MobSnapshot[LegacyAccountSnapshot.MobClanOffset] == 4, "NPC summon did not receive the legacy level, full HP, and Clan 4 state.");

    var codec = LegacyFrameCodec.CreateDefault();
    var createFrame = new CreateMobConfirmation((ushort)summoned.ConnectionId, summoned.PositionX, summoned.PositionY, summoned.MobSnapshot, summoned.AffectSnapshot, npc: true, summon: true).ToFrame(codec, 1, 0);
    var create = codec.Decode(createFrame);
    Assert(BinaryPrimitives.ReadUInt16LittleEndian(create.Payload.Span[54..]) == ((24 << 8) | 20) && BinaryPrimitives.ReadInt32LittleEndian(create.Payload.Span[128..]) == 0 && BinaryPrimitives.ReadUInt16LittleEndian(create.Payload.Span[172..]) == 3, "NPC summon CreateMob wire did not carry affect 24, forced NPC AC, and CreateType 3.");

    var enemyMob = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(100, 0, 10, 0, 0, 0, 0, 1000, 1000, 1000, 100, 0, 0, 0, 0, 0, 0, 0, 0).Write(enemyMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    BinaryPrimitives.WriteInt64LittleEndian(enemyMob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset), 777);
    Assert(hub.Enter(2, "SUMMON_TARGET", (_, _) => ValueTask.CompletedTask) && hub.SetCharacterState(2, 0, 0, 5, 0, 0, 2005, 2005, enemyMob), "NPC-summon target was not registered.");
    Assert(hub.TrySelectSummonTarget(summoned.ConnectionId, out var selectedTarget) && selectedTarget?.TargetConnectionId == 2 && selectedTarget.Distance == 6, "NPC summon did not select the nearest hostile clan target in legacy view range.");
    Assert(hub.TryGetSummonTarget(summoned.ConnectionId, out var currentTarget) && currentTarget == 2, "NPC summon target state was not retained.");

    Assert(hub.TryApplyAllSummonAttacks(randomFactor: 99, out var summonAttacks) && summonAttacks.Count == 1, "NPC summon battle tick did not resolve the leader's summon attack opportunity.");
    var appliedSummonAttack = summonAttacks[0];
    Assert(appliedSummonAttack.TargetConnectionId == 2 && appliedSummonAttack.Damage > 0 && appliedSummonAttack.RemainingHp < 1000, "NPC summon did not apply authoritative physical damage to its selected target.");
    Assert(hub.TryGetCombatState(2, out var damagedTarget) && damagedTarget!.CurrentScore.Hp == appliedSummonAttack.RemainingHp, "NPC summon damage was not persisted in the target combat state.");
    var summonAttackFrame = codec.Decode(new NpcAttackConfirmation(
        appliedSummonAttack.SummonPositionX,
        appliedSummonAttack.SummonPositionY,
        appliedSummonAttack.TargetPositionX,
        appliedSummonAttack.TargetPositionY,
        (ushort)appliedSummonAttack.SummonConnectionId,
        (ushort)appliedSummonAttack.TargetConnectionId,
        motion: 0,
        skillIndex: -1,
        appliedSummonAttack.Damage).ToFrame(codec, 1, 0));
    Assert(summonAttackFrame.IsChecksumValid && summonAttackFrame.Header.Type == AttackRequest.OneTargetMessageType && summonAttackFrame.Header.Id == NpcAttackConfirmation.SceneId && BinaryPrimitives.ReadUInt16LittleEndian(summonAttackFrame.Payload.Span[48..]) == 2 && BinaryPrimitives.ReadInt32LittleEndian(summonAttackFrame.Payload.Span[52..]) == appliedSummonAttack.Damage, "NPC summon attack wire did not carry the authoritative attacker, target, and damage fields.");

    var parryTargetMob = enemyMob.ToArray();
    parryTargetMob[LegacyAccountSnapshot.MobEquipmentOffset + LegacyItem.SizeInBytes] = 4;
    Assert(hub.SetCharacterState(2, 0, 0, 5, 0, 0, 2005, 2005, parryTargetMob), "NPC-summon parry target could not be equipped.");
    var summonParryResult = hub.TryApplySummonAttack(summoned.ConnectionId, randomFactor: 99, out var summonParry, parryRandomRoll: 50);
    Assert(summonParryResult == LegacySummonAttackResult.Accepted && summonParry is not null && summonParry.Damage == -3 && summonParry.RemainingHp == 1000, "NPC summon parry did not emit -3 while preserving target HP.");
    BinaryPrimitives.WriteUInt16LittleEndian(parryTargetMob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), 0x200);
    Assert(hub.SetCharacterState(2, 0, 0, 5, 0, 0, 2005, 2005, parryTargetMob), "NPC-summon special-parry target could not be reset.");
    var summonSpecialParryResult = hub.TryApplySummonAttack(summoned.ConnectionId, randomFactor: 99, out var summonSpecialParry, parryRandomRoll: 50);
    Assert(summonSpecialParryResult == LegacySummonAttackResult.Accepted && summonSpecialParry is not null && summonSpecialParry.Damage == -4 && summonSpecialParry.RemainingHp == 1000, "NPC summon special parry did not emit -4 while preserving target HP.");

    Assert(hub.SetCharacterState(2, 0, 0, 5, 0, 0, 2005, 2005, enemyMob), "NPC-summon map-attribute target could not be reset.");
    Assert(hub.SetCombatEligibilityState(1, pkMode: false, guilty: false, mapAttribute: 0x40) && hub.SetCombatEligibilityState(2, pkMode: false, guilty: false, mapAttribute: 0x40), "NPC-summon allowed map attributes could not be configured.");
    var mapAllowedResult = hub.TryApplySummonAttack(summoned.ConnectionId, randomFactor: 99, out var mapAllowedAttack, parryRandomRoll: 1000);
    Assert(mapAllowedResult == LegacySummonAttackResult.Accepted && mapAllowedAttack is not null && mapAllowedAttack.Damage > 0 && mapAllowedAttack.Damage < appliedSummonAttack.Damage, "NPC summon did not apply the legacy 3/10 map damage branch before Clan 4 reduction.");
    Assert(hub.SetCharacterState(2, 0, 0, 5, 0, 0, 2005, 2005, enemyMob) && hub.SetCombatEligibilityState(1, pkMode: false, guilty: false, mapAttribute: 0) && hub.SetCombatEligibilityState(2, pkMode: false, guilty: false, mapAttribute: 0), "NPC-summon blocked map attributes could not be configured.");
    var mapBlockedResult = hub.TryApplySummonAttack(summoned.ConnectionId, randomFactor: 99, out var mapBlockedAttack, parryRandomRoll: 1000);
    Assert(mapBlockedResult == LegacySummonAttackResult.Accepted && mapBlockedAttack is not null && mapBlockedAttack.Damage == 0 && mapBlockedAttack.RemainingHp == 1000, "NPC summon did not suppress damage on blocked map attributes.");

    Assert(hub.TryAdvanceSummons(1, out var stillNear) && stillNear.Count == 0, "A summon within four cells moved despite the legacy follow threshold.");
    Assert(hub.UpdatePosition(1, 2010, 2010) && hub.TryAdvanceSummons(1, out var followMoves) && followMoves.Count == 1 && followMoves[0].Effect == 0 && followMoves[0].ToX == 2000 && followMoves[0].ToY == 2000, "A summon five-to-twelve cells from its leader did not follow by one grid cell.");
    Assert(hub.UpdatePosition(1, 2030, 2030) && hub.TryAdvanceSummons(1, out var teleportMoves) && teleportMoves.Count == 1 && teleportMoves[0].Effect == 1 && teleportMoves[0].ToX == 2029 && teleportMoves[0].ToY == 2029, "A summon thirteen or more cells from its leader did not use the legacy teleport follow path.");

    Assert(hub.TryConsumeSkillMana(1, skill, out _, out _, out _) == LegacySkillManaResult.Accepted, "NPC-summon mana could not be consumed for the duplicate-type failure path.");
    var duplicateResult = hub.TryApplySkillAttack(1, 1, skill, itemData: null, weather: 0, randomFactor: 90, out var duplicateOutcome);
    Assert(duplicateResult == LegacySkillAttackResult.Accepted && duplicateOutcome?.SummonResult == LegacySummonGenerationResult.AlreadyPresent && duplicateOutcome.AttackerResourceChanged, "A duplicate legacy summon did not fail with an MP refund.");
    Assert(hub.TryGetResourceState(1, out var refundedState, out _, out _) && refundedState!.CurrentMana == 80, "Failed NPC summon did not restore the consumed MP.");

    var removeFrame = codec.Decode(new RemoveMobConfirmation((ushort)summoned.ConnectionId).ToFrame(codec, 1, 0));
    Assert(removeFrame.Header.Type == RemoveMobConfirmation.MessageType && removeFrame.Header.Id == summoned.ConnectionId && BinaryPrimitives.ReadInt32LittleEndian(removeFrame.Payload.Span) == 3, "NPC summon removal wire differs from MSG_RemoveMob.");
    BinaryPrimitives.WriteUInt16LittleEndian(summoned.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset), 315);
    BinaryPrimitives.WriteInt32LittleEndian(summoned.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), 1);
    Assert(hub.UpdatePosition(2, 2029, 2029), "NPC-summon attacker could not be moved into range.");
    var summonDeathResult = hub.TryApplyPhysicalAttack(2, summoned.ConnectionId, randomFactor: 99, out var summonDeath, parryRandomRoll: 1000, itemData: CreateCombatItemDataTable());
    var linkedMountHp = summonDeath?.UpdatedMountItem is { } linkedMount
        ? (short)(linkedMount.Effect1 | (linkedMount.Value1 << 8))
        : -1;
    Assert(summonDeathResult == LegacyPhysicalAttackResult.Accepted && summonDeath is not null && summonDeath.TargetRemoved && summonDeath.TargetDied && summonDeath.RemainingHp == 0 && summonDeath.MountOwnerConnectionId == 1 && linkedMountHp == 0 && !hub.TryGetSummonTarget(summoned.ConnectionId, out _), "A lethal player attack did not link the mount HP to the owner's equipment before removing the summon.");
    Assert(hub.TryGetCombatState(2, out var killerState) && killerState!.Experience == 777, "Killing a Clan 4 InstanceType 11 summon incorrectly awarded PvE experience.");
    Assert(hub.Leave(1, out var despawned) && despawned.Count == 0 && hub.Count == 1, "Leaving the world reported an already-despawned NPC summon again.");
    Assert(hub.Leave(2) && hub.Count == 0, "The hostile test participant was not removed after the summon lifecycle test.");
}

static void ShortSkillWireAndPersistence()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var payload = Enumerable.Range(1, 20).Select(value => (byte)value).ToArray();
    var frame = codec.Decode(codec.Encode(SetShortSkillRequest.MessageType, 4, 123456, payload, 16));
    Assert(SetShortSkillRequest.TryParse(frame, out var request) && request!.SkillBar.SequenceEqual(payload[..4]) && request.ShortSkills.SequenceEqual(payload[4..]), "Set-short-skill wire differs from the legacy layout.");
    var parsed = request ?? throw new InvalidOperationException("Set-short-skill parser returned no request after a successful parse.");

    var root = Path.Combine(Path.GetTempPath(), $"wyd-cdk-short-skill-test-{Guid.NewGuid():N}");
    var accountRoot = Path.Combine(root, "account");
    try
    {
        Directory.CreateDirectory(Path.Combine(accountRoot, "S"));
        var file = new byte[LegacyAccountSnapshot.RequiredFileLength];
        file[LegacyAccountSnapshot.CharactersOffset] = (byte)'H';
        File.WriteAllBytes(Path.Combine(accountRoot, "S", "SANDBOX"), file);
        var store = new LegacyFileAccountStore(accountRoot);
        Assert(store.TrySaveCharacterShortSkillsAsync("SANDBOX", 0, parsed.SkillBar, parsed.ShortSkills).GetAwaiter().GetResult() == CharacterShortSkillSaveResult.Success, "Set-short-skill persistence failed.");
        var stored = File.ReadAllBytes(Path.Combine(accountRoot, "S", "SANDBOX"));
        Assert(stored.AsSpan(LegacyAccountSnapshot.CharactersOffset + LegacyAccountSnapshot.MobSkillBarOffset, 4).SequenceEqual(payload[..4]) && stored.AsSpan(LegacyAccountSnapshot.ShortSkillOffset, 16).SequenceEqual(payload[4..]), "Set-short-skill values were not persisted at the legacy offsets.");
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}

static void MotionWire()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(new MotionRequest(12, 34, 0).ToFrame(codec, 123456, 16, 7));
    Assert(MotionRequest.TryParse(frame, out var motion) && motion!.Motion == 12 && motion.Parameter == 34 && motion.NotUsed == 0, "Motion fields differ after wire round-trip.");
    Assert(frame.Header.Type == MotionRequest.MessageType && frame.Header.Size == MotionRequest.PacketSize && frame.Header.Id == 7 && frame.Header.ClientTick == 123456, "Motion wire header differs from the legacy relay.");
}

static void WorldHubBroadcast()
{
    var first = new List<byte[]>();
    var second = new List<byte[]>();
    var hub = new WorldHub();
    Assert(hub.Enter(1, "ONE", (frame, _) => { first.Add(frame.ToArray()); return ValueTask.CompletedTask; }), "First world participant was not registered.");
    Assert(hub.Enter(2, "TWO", (frame, _) => { second.Add(frame.ToArray()); return ValueTask.CompletedTask; }), "Second world participant was not registered.");

    hub.BroadcastAsync(1, new byte[] { 1, 2, 3 }).GetAwaiter().GetResult();
    Assert(first.Count == 0 && second.Count == 1 && second[0].SequenceEqual(new byte[] { 1, 2, 3 }), "World movement was not broadcast only to other participants.");
    Assert(hub.Leave(2) && hub.Count == 1, "World participant was not removed.");
}

static void WorldSkillManaMutation()
{
    var mob = new byte[LegacyAccountSnapshot.CharacterStride];
    var score = new LegacyScore(100, 0, 0, 0, 0, 0, 0, 0, 0, 1000, 100, 0, 0, 0, 0, 0, 40, 0, 0);
    score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    mob[LegacyAccountSnapshot.MobClassOffset] = 0;
    mob[LegacyAccountSnapshot.MobSaveManaOffset] = 10;
    var hub = new WorldHub();
    Assert(hub.Enter(1, "MANA", (_, _) => ValueTask.CompletedTask), "Mana participant was not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 0, 0, 0, 0, 0, mob), "Mana participant state was not set.");

    using var reader = new StringReader("10,69,1,20,15,1,1,40,0,0,0,0,0,8.0.0.7.0.0.0.0,8.0.0.7.0.0.0.0,0,0,1,2,0,0,0,1,Golpe_Mortal");
    var skill = LegacySkillDataTable.Load(reader)[10]!;
    var result = hub.TryConsumeSkillMana(1, skill, out var after, out var snapshot);
    Assert(result == LegacySkillManaResult.Accepted && after is not null && after.CurrentMana == 79 && snapshot is not null && BinaryPrimitives.ReadInt32LittleEndian(snapshot.AsSpan(LegacyAccountSnapshot.MobCurrentMpOffset)) == 79, "Server-authoritative skill mana was not deducted using the legacy formula.");

    var lowManaMob = mob.ToArray();
    BinaryPrimitives.WriteInt32LittleEndian(lowManaMob.AsSpan(LegacyAccountSnapshot.MobCurrentMpOffset), 10);
    var lowHub = new WorldHub();
    Assert(lowHub.Enter(2, "LOWMANA", (_, _) => ValueTask.CompletedTask) && lowHub.SetCharacterState(2, 0, 0, 0, 0, 0, 0, 0, lowManaMob), "Low-mana participant state was not set.");
    Assert(lowHub.TryConsumeSkillMana(2, skill, out var unchanged, out _) == LegacySkillManaResult.InsufficientMana && unchanged is null, "Insufficient mana was accepted or mutated.");
    Assert(lowHub.TryGetCombatState(2, out var lowState) && lowState!.CurrentMana == 10, "Insufficient-mana attempt changed current MP.");
}

static void WorldGuildInvite()
{
    var hub = new WorldHub();
    Assert(hub.Enter(1, "LEADER", (_, _) => ValueTask.CompletedTask), "Guild leader was not registered.");
    Assert(hub.Enter(2, "MEMBER", (_, _) => ValueTask.CompletedTask), "Guild target was not registered.");
    Assert(hub.SetCharacterState(1, 0, 77, 1, 9, 100_000_000), "Guild leader state was not set.");
    Assert(hub.SetCharacterState(2, 0, 0, 1, 0, 0), "Guild target state was not set.");
    var check = hub.TryPrepareGuildInvite(1, 2, 1, out var plan);
    if (DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
    {
        Assert(check == GuildInviteCheckResult.Sunday && plan is null, "Sunday guild invite restriction was not preserved.");
        return;
    }
    Assert(check == GuildInviteCheckResult.Accepted && plan is not null && plan.Cost == 100_000_000, "Valid guild invite was rejected or cost differs.");
    Assert(hub.ApplyGuildInvite(plan!), "Prepared guild invite was not applied.");
    Assert(hub.TryPrepareGuildInvite(1, 2, 0, out _) == GuildInviteCheckResult.TargetAlreadyHasGuild, "A second guild invite was accepted for a member already in a guild.");
}

static void GuildInviteVisualUpdate()
{
    var hub = new WorldHub();
    var sourceMob = new byte[816];
    var targetMob = new byte[816];
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(sourceMob.AsSpan(28), 100_000_000);
    Assert(hub.Enter(1, "LEADER", (_, _) => ValueTask.CompletedTask), "Guild leader visual participant was not registered.");
    Assert(hub.Enter(2, "MEMBER", (_, _) => ValueTask.CompletedTask), "Guild target visual participant was not registered.");
    Assert(hub.SetCharacterState(1, 0, 77, 1, 9, 100_000_000, 2000, 2001, sourceMob), "Guild leader visual state was not set.");
    Assert(hub.SetCharacterState(2, 0, 0, 1, 0, 0, 2100, 2200, targetMob), "Guild target visual state was not set.");
    if (DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
    {
        Assert(hub.TryPrepareGuildInvite(1, 2, 1, out _) == GuildInviteCheckResult.Sunday, "Sunday guild invite visual restriction was not preserved.");
        return;
    }
    Assert(hub.TryPrepareGuildInvite(1, 2, 1, out var plan) == GuildInviteCheckResult.Accepted && plan is not null && hub.ApplyGuildInvite(plan), "Guild visual update could not be applied.");

    var codec = LegacyFrameCodec.CreateDefault();
    Assert(hub.TryBuildCreateMobFrame(2, codec, 33, 16, out var createFrame) && createFrame is not null, "Guild member CreateMob refresh was not built.");
    var create = codec.Decode(createFrame!);
    Assert(create.Header.Type == CreateMobConfirmationV769.MessageType && create.Header.Size == CreateMobConfirmationV769.PacketSize &&
           System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(create.Payload.Span) == 2100 &&
           System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(create.Payload.Span[2..]) == 2200 &&
           System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(create.Payload.Span[CreateMobConfirmationV769.GuildOffset..]) == 77,
        "Guild member visual refresh has incorrect 7.69 position, size, or guild.");
    Assert(hub.TryBuildUpdateEtcFrame(1, codec, 33, 17, out var etcFrame) && etcFrame is not null, "Guild leader UpdateEtc refresh was not built.");
    var etc = codec.Decode(etcFrame!);
    Assert(etc.Header.Type == UpdateEtcConfirmationV769.MessageType && etc.Header.Size == UpdateEtcConfirmationV769.PacketSize &&
           System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(etc.Payload.Span[UpdateEtcConfirmationV769.CoinOffset..]) == 0,
        "Guild leader visual refresh has incorrect 7.69 wire or coin.");
}

static void CreateMobWire()
{
    var mob = new byte[816];
    System.Text.Encoding.ASCII.GetBytes("HERO").CopyTo(mob, 0);
    var killMarkOffset = LegacyAccountSnapshot.MobCarryOffset + (LegacyAccountSnapshot.LegacyKillMarkCarrySlot * LegacyItem.SizeInBytes);
    new LegacyItem(547, 4, 4, 0, 0x34, 0, 0x12).Write(mob.AsSpan(killMarkOffset));
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(18), 7);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(140), 1103);
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(92), 42);
    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(new CreateMobConfirmation(9, 2096, 2096, mob).ToFrame(codec, 33, 16));
    Assert(frame.IsChecksumValid && frame.Header.Type == CreateMobConfirmation.MessageType && frame.Header.Size == CreateMobConfirmation.PacketSize, "Create-mob frame header differs from the legacy wire.");
    Assert(System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span) == 2096 && System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[4..]) == 9, "Create-mob position or id was misplaced.");
    Assert(frame.Payload.Span[6 + 12] == 4 && frame.Payload.Span[6 + 13] == 4 && frame.Payload.Span[6 + 14] == 0x34 && frame.Payload.Span[6 + 15] == 0x12, "Create-mob PK metadata was not projected into the legacy name tail.");
    Assert(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[22..]) == 1103 && System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[124..]) == 42, "Create-mob equipment or score was misplaced.");
}

static void InitialWorldStateWire()
{
    var mob = new byte[816];
    System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(mob.AsSpan(32), 123456);
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(28), 789);
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(92 + 24), 100);
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(92 + 28), 50);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(mob.AsSpan(780), 0x11223344);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(784), 12);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(788), 13);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(790), 14);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(792), 15);
    var mobExtra = new byte[552];
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(mobExtra.AsSpan(476), 321);
    var codec = LegacyFrameCodec.CreateDefault();
    var etc = codec.Decode(new UpdateEtcConfirmation(mob, mobExtra).ToFrame(codec, 33, 16, 4));
    var score = codec.Decode(new UpdateScoreConfirmation(mob).ToFrame(codec, 33, 16, 4));
    Assert(etc.Header.Type == UpdateEtcConfirmation.MessageType && etc.Header.Size == 56 &&
           System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(etc.Payload.Span) == 321 &&
           System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(etc.Payload.Span[4..]) == 123456 &&
           System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(etc.Payload.Span[12..]) == 0x11223344 &&
           System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(etc.Payload.Span[20..]) == 13 &&
           System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(etc.Payload.Span[22..]) == 14 &&
           System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(etc.Payload.Span[24..]) == 15 &&
           System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(etc.Payload.Span[26..]) == 12 &&
           System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(etc.Payload.Span[28..]) == 789,
           "Update-etc wire differs from the initial legacy state.");
    Assert(score.Header.Type == UpdateScoreConfirmation.MessageType && score.Header.Size == 160 && System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(score.Payload.Span[140..]) == 100 && System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(score.Payload.Span[144..]) == 50, "Update-score wire differs from the initial legacy state.");
}

static void GuildInviteWire()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var request = new InviteGuildRequest(42, 3);
    var frame = codec.Decode(request.ToFrame(codec, 33, 16, 7));
    Assert(InviteGuildRequest.TryParse(frame, out var parsed) && parsed!.TargetConnectionId == 42 && parsed.InviteType == 3, "Guild-invite STANDARDPARM2 fields differ.");
    Assert(frame.Header.Size == InviteGuildRequest.PacketSize && frame.Header.Id == 7, "Guild-invite frame header differs.");
}

static void PartyWire()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var invite = new PartyInviteRequest(3, 1, 55, 1000, 900, 7, "LEADER", 42, 0);
    var inviteFrame = codec.Decode(invite.ToFrame(codec, 33, 16, 30000));
    Assert(PartyInviteRequest.TryParse(inviteFrame, out var parsedInvite) && parsedInvite is not null, "Party invite was not parsed.");
    Assert(parsedInvite!.Class == 3 && parsedInvite.PartyPosition == 1 && parsedInvite.Level == 55 && parsedInvite.MaxHp == 1000 && parsedInvite.Hp == 900 && parsedInvite.PartyId == 7 && parsedInvite.MobName == "LEADER" && parsedInvite.TargetConnectionId == 42 && parsedInvite.Target == 0, "Party invite fields differ from the legacy payload.");
    Assert(inviteFrame.Header.Type == PartyInviteRequest.MessageType && inviteFrame.Header.Size == PartyInviteRequest.PacketSize, "Party invite header differs from the legacy wire.");

    var add = new PartyAddConfirmation(7, 55, 1000, 900, 42, "MEMBER", unchecked((short)52428));
    var addFrame = codec.Decode(add.ToFrame(codec, 33, 16));
    Assert(addFrame.IsChecksumValid && addFrame.Header.Type == PartyAddConfirmation.MessageType && addFrame.Header.Size == PartyAddConfirmation.PacketSize, "Party-add confirmation header differs from the legacy wire.");
    Assert(BinaryPrimitives.ReadInt16LittleEndian(addFrame.Payload.Span) == 7 && BinaryPrimitives.ReadInt16LittleEndian(addFrame.Payload.Span[2..]) == 55 && BinaryPrimitives.ReadInt16LittleEndian(addFrame.Payload.Span[8..]) == 42 && System.Text.Encoding.ASCII.GetString(addFrame.Payload.Span[10..26]).TrimEnd('\0') == "MEMBER" && BinaryPrimitives.ReadInt16LittleEndian(addFrame.Payload.Span[26..]) == unchecked((short)52428), "Party-add fields were misplaced.");

    var accept = codec.Decode(new PartyAcceptRequest(7, "LEADER").ToFrame(codec, 33, 16));
    Assert(PartyAcceptRequest.TryParse(accept, out var parsedAccept) && parsedAccept is not null && parsedAccept.LeaderConnectionId == 7 && parsedAccept.MobName == "LEADER", "Party-accept fields differ.");

    var remove = codec.Decode(new PartyRemoveRequest(42).ToFrame(codec, 33, 16));
    Assert(PartyRemoveRequest.TryParse(remove, out var parsedRemove) && parsedRemove is not null && parsedRemove.TargetConnectionId == 42, "Party-remove request did not preserve its target connection.");
    var removeConfirmation = codec.Decode(new PartyRemoveConfirmation(42).ToFrame(codec, 33, 16));
    Assert(removeConfirmation.Header.Type == PartyRemoveConfirmation.MessageType && removeConfirmation.Header.Size == PartyRemoveConfirmation.PacketSize && BinaryPrimitives.ReadInt16LittleEndian(removeConfirmation.Payload.Span) == 42, "Party-remove confirmation differs from the legacy wire.");
}

static void WorldPartyLifecycle()
{
    static byte[] PartyMob(string name, byte characterClass, int level, int maxHp, int hp)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        System.Text.Encoding.ASCII.GetBytes(name).CopyTo(mob, 0);
        mob[LegacyAccountSnapshot.MobClassOffset] = characterClass;
        new LegacyScore(level, 10, 20, 0, 0, 0, 0, maxHp, 100, hp, 50, 10, 10, 10, 10, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    var hub = new WorldHub();
    Assert(hub.Enter(1, "ACCOUNT1", (_, _) => ValueTask.CompletedTask) && hub.Enter(2, "ACCOUNT2", (_, _) => ValueTask.CompletedTask), "Party participants were not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 0, 0, 0, 100, 100, PartyMob("LEADER", 0, 55, 1000, 900)) && hub.SetCharacterState(2, 0, 0, 0, 0, 0, 101, 100, PartyMob("MEMBER", 1, 56, 800, 700)), "Party participant state was not set.");

    Assert(hub.TryPreparePartyInvite(1, 2, out var invite) == PartyInviteCheckResult.Accepted && invite is not null && invite.Leader.MobName == "LEADER" && invite.Target.MobName == "MEMBER", "A compatible party invite was rejected.");
    Assert(hub.TryAcceptParty(2, 1, "LEADER", out var formation) == PartyAcceptResult.Accepted && formation is not null && formation.Members.Count == 2 && formation.Members.Any(member => member.ConnectionId == 1 && member.Level == 55) && formation.Members.Any(member => member.ConnectionId == 2 && member.Hp == 700), "Party acceptance did not form the expected two-member group.");

    Assert(hub.TryRemovePartyMember(1, 2, out var removal) && removal is not null && !removal.Disbanded && removal.RemovedConnectionId == 2 && removal.NotifiedConnectionIds.SequenceEqual(new[] { 1, 2 }), "Leader removal did not notify the original party members.");
    Assert(hub.TryRemovePartyMember(1, 0, out var disband) && disband is not null && disband.Disbanded && disband.RemovedConnectionId == 0, "Leader disband did not clear the remaining party.");
}

static void MessagePanelWire()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(new MessagePanelConfirmation("Guild joined").ToFrame(codec, 33, 16));
    Assert(frame.IsChecksumValid && frame.Header.Type == MessagePanelConfirmation.MessageType && frame.Header.Size == MessagePanelConfirmation.PacketSize && frame.Header.Id == 0, "Message-panel frame header differs from SendClientMessage.");
    Assert(System.Text.Encoding.ASCII.GetString(frame.Payload.Span[..12]).StartsWith("Guild joined", StringComparison.Ordinal), "Message-panel text was not encoded.");
}

static void PkInfoWire()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(new PkInfoConfirmation(7, 0).ToFrame(codec, 33, 16));
    Assert(frame.IsChecksumValid && frame.Header.Type == PkInfoConfirmation.MessageType && frame.Header.Size == PkInfoConfirmation.PacketSize && frame.Header.Id == 7, "PK-info frame header differs from the legacy MSG_STANDARDPARM.");
    Assert(BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span) == 0, "PK-info state was not encoded as the normal state.");
}

static void NpcChatWire()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(new NpcChatConfirmation("Traga Esfera_da_Sorte.").ToFrame(codec, 34, 17, 20_601));
    Assert(frame.IsChecksumValid && frame.Header.Type == NpcChatConfirmation.MessageType && frame.Header.Size == NpcChatConfirmation.PacketSize && frame.Header.Id == 20_601, "NPC chat frame header differs from MSG_MessageChat.");
    Assert(System.Text.Encoding.ASCII.GetString(frame.Payload.Span[..23]).StartsWith("Traga Esfera_da_Sorte.", StringComparison.Ordinal), "NPC chat text was not encoded.");
}

static void MessageChatWire()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(new MessageChatConfirmation("Oi, mundo!").ToFrame(codec, 35, 18, 7));
    Assert(MessageChatRequest.TryParse(frame, out var request) && request is not null && request.Message == "Oi, mundo!", "MSG_MessageChat did not round-trip the fixed 128-byte text field.");
    Assert(frame.Header.Type == MessageChatRequest.MessageType && frame.Header.Size == MessageChatRequest.PacketSize && frame.Header.Id == 7, "Relayed chat frame does not preserve the legacy type/size/sender ID.");
}

static void WorldPlayerChatView()
{
    static byte[] Mob()
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(10, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    var hub = new WorldHub();
    Assert(hub.Enter(1, "CHAT_ONE", (_, _) => ValueTask.CompletedTask) && hub.Enter(2, "CHAT_TWO", (_, _) => ValueTask.CompletedTask) && hub.Enter(3, "CHAT_THREE", (_, _) => ValueTask.CompletedTask), "Chat participants were not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 0, 0, 0, 1000, 1000, Mob()) && hub.SetCharacterState(2, 0, 0, 0, 0, 0, 1016, 1016, Mob()) && hub.SetCharacterState(3, 0, 0, 0, 0, 0, 1033, 1000, Mob()), "Chat participant state was not set.");

    Assert(hub.GetParticipantIdsInPlayerView(1).OrderBy(static id => id).SequenceEqual([1, 2]), "Player chat view did not preserve the inclusive 33x33 GridMulticast bounds or exclude the distant player.");
    Assert(hub.GetParticipantIdsInPlayerView(99).Count == 0, "Unknown player unexpectedly received a chat view.");
}

static void MessageWhisperWire()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var payloadFrame = codec.Encode(MessageWhisperRequest.MessageType, 0, 36, BuildWhisperPayload("TARGET", "hello privately"), 19);
    var frame = codec.Decode(payloadFrame);
    Assert(MessageWhisperRequest.TryParse(frame, out var request) && request is not null && request.TargetName == "TARGET" && request.Message == "hello privately", "MSG_MessageWhisper did not parse its fixed target/message fields.");

    var cpFrame = codec.Decode(codec.Encode(MessageWhisperRequest.MessageType, 0, 36, BuildWhisperPayload("cp", string.Empty), 19));
    Assert(MessageWhisperRequest.TryParse(cpFrame, out var cpRequest) && cpRequest is not null && cpRequest.TargetName == "cp" && cpRequest.Message.Length == 0, "The retail empty-message /cp whisper frame was rejected.");

    var confirmation = codec.Decode(new MessageWhisperConfirmation("SENDER", "hello privately").ToFrame(codec, 36, 19, 2));
    Assert(confirmation.IsChecksumValid && confirmation.Header.Type == MessageWhisperRequest.MessageType && confirmation.Header.Size == MessageWhisperRequest.PacketSize && confirmation.Header.Id == 2, "Whisper confirmation header differs from the legacy target-directed frame.");
    Assert(MessageWhisperRequest.TryParse(confirmation, out var delivered) && delivered is not null && delivered.TargetName == "SENDER" && delivered.Message == "hello privately", "Whisper confirmation did not carry the sender name in the legacy MobName field.");
}

static void WorldWhisperTarget()
{
    static byte[] Mob(string name)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        System.Text.Encoding.ASCII.GetBytes(name).CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset));
        new LegacyScore(10, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    var hub = new WorldHub();
    Assert(hub.Enter(1, "WHISPER_ONE", (_, _) => ValueTask.CompletedTask) && hub.Enter(2, "WHISPER_TWO", (_, _) => ValueTask.CompletedTask), "Whisper participants were not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 0, 0, 0, 1000, 1000, Mob("SENDER")) && hub.SetCharacterState(2, 0, 0, 0, 0, 0, 1000, 1000, Mob("TARGET")), "Whisper participant state was not set.");
    Assert(hub.TryGetParticipantIdByCharacterName("TARGET", out var target) && target == 2, "Connected whisper target was not found by exact character name.");
    Assert(hub.TryGetParticipantCharacterName(1, out var sender) && sender == "SENDER", "Connected whisper sender name was not read from the authoritative MOB.");
    Assert(!hub.TryGetParticipantIdByCharacterName("target", out _), "Whisper lookup unexpectedly changed the legacy exact-name comparison.");
}

static byte[] BuildWhisperPayload(string targetName, string message)
{
    var payload = new byte[MessageWhisperRequest.NameLength + MessageWhisperRequest.MessageLength];
    var targetBytes = System.Text.Encoding.ASCII.GetBytes(targetName);
    targetBytes.AsSpan(0, Math.Min(targetBytes.Length, MessageWhisperRequest.NameLength - 1)).CopyTo(payload);
    var messageBytes = System.Text.Encoding.ASCII.GetBytes(message);
    messageBytes.AsSpan(0, Math.Min(messageBytes.Length, MessageWhisperRequest.MessageLength - 1)).CopyTo(payload.AsSpan(MessageWhisperRequest.NameLength));
    return payload;
}

static void GetItemWire()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var payload = new byte[GetItemRequest.PayloadSize];
    BinaryPrimitives.WriteInt32LittleEndian(payload, LegacyWorldItem.CarryDestinationType);
    BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int)), 3);
    BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(sizeof(int) * 2), 10_001);
    BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan((sizeof(int) * 2) + sizeof(ushort)), 0);
    BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan((sizeof(int) * 2) + (sizeof(ushort) * 2)), 1000);
    BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan((sizeof(int) * 2) + (sizeof(ushort) * 3)), 1001);
    var requestFrame = codec.Decode(codec.Encode(GetItemRequest.MessageType, 0, 37, payload, 20));
    Assert(GetItemRequest.TryParse(requestFrame, out var request) && request is not null && request.DestinationType == 1 && request.DestinationSlot == 3 && request.ItemId == 10_001 && request.GridX == 1000 && request.GridY == 1001, "MSG_GetItem did not parse the legacy destination, wire item ID, and grid coordinates.");

    var confirmation = codec.Decode(new GetItemConfirmation(1, 3, new LegacyItem(500, 0, 0, 0, 0, 0, 0)).ToFrame(codec, 37, 20, 30_000));
    Assert(confirmation.IsChecksumValid && confirmation.Header.Type == GetItemConfirmation.MessageType && confirmation.Header.Size == GetItemConfirmation.PacketSize && confirmation.Header.Id == 30_000, "MSG_CNFGetItem header differs from the legacy scene confirmation.");
    var decay = codec.Decode(new DecayItemConfirmation(10_001).ToFrame(codec, 37, 20, 30_000));
    Assert(decay.IsChecksumValid && decay.Header.Type == DecayItemConfirmation.MessageType && decay.Header.Size == DecayItemConfirmation.PacketSize, "MSG_DecayItem was not encoded with the legacy fixed payload.");
}

static void WorldGroundItemPickup()
{
    static byte[] Mob()
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(10, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    var hub = new WorldHub();
    Assert(hub.Enter(1, "GROUND_ITEM", (_, _) => ValueTask.CompletedTask), "Ground-item participant was not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 0, 0, 0, 1000, 1000, Mob()), "Ground-item participant state was not set.");
    var wireItemId = hub.EnterGroundItem(new LegacyItem(500, 0, 0, 0, 0, 0, 0), 1001, 1001, requestedItemId: 1);
    Assert(wireItemId == 10_001, "Ground item did not receive the legacy 10000 wire-ID offset.");
    Assert(hub.TryGetGroundItem(1, 1, 0, wireItemId, 1001, 1001, out var outcome) == LegacyGetItemResult.Accepted && outcome is not null && outcome.Item.Index == 500 && outcome.DestinationSlot == 0, "A valid ground-item pickup was rejected.");
    Assert(hub.TryGetCharacterSnapshot(1, out var mobAfterPickup, out _, out _) && mobAfterPickup is not null && LegacyItem.Read(mobAfterPickup.AsSpan(LegacyAccountSnapshot.MobCarryOffset)).Index == 500, "Ground-item pickup did not mutate the authoritative carry.");
    Assert(hub.TryGetGroundItem(1, 1, 1, wireItemId, 1001, 1001, out _) == LegacyGetItemResult.GroundItemNotFound, "A collected ground item remained available for a second pickup.");

    var occupiedWireId = hub.EnterGroundItem(new LegacyItem(501, 0, 0, 0, 0, 0, 0), 1001, 1001, requestedItemId: 2);
    Assert(hub.TryGetGroundItem(1, 1, 0, occupiedWireId, 1001, 1001, out _) == LegacyGetItemResult.DestinationOccupied, "An occupied carry slot accepted a ground item.");
    var distantWireId = hub.EnterGroundItem(new LegacyItem(502, 0, 0, 0, 0, 0, 0), 1005, 1000, requestedItemId: 3);
    Assert(hub.TryGetGroundItem(1, 1, 1, distantWireId, 1005, 1000, out _) == LegacyGetItemResult.OutOfRange, "A pickup outside the legacy three-cell square was accepted.");
}

  static void DropItemWire()
  {
      var codec = LegacyFrameCodec.CreateDefault();
      var payload = new byte[DropItemRequest.PayloadSize];
      BinaryPrimitives.WriteInt32LittleEndian(payload, LegacyWorldItem.CarryDestinationType);
      BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4), 2);
      BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(8), 3);
      BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(12), 1000);
      BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(14), 1001);
      BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(16), 10002);
      var requestFrame = codec.Decode(codec.Encode(DropItemRequest.MessageType, 1, 38, payload, 21));
      Assert(DropItemRequest.TryParse(requestFrame, out var request) && request is not null && request.SourceType == 1 && request.SourceSlot == 2 && request.Rotate == 3 && request.GridX == 1000 && request.GridY == 1001 && request.ItemId == 10002, "MSG_DropItem did not parse the legacy carry source, rotation, grid and item ID.");

      var confirmation = codec.Decode(new DropItemConfirmation(1, 2, 3, 1000, 1001).ToFrame(codec, 38, 21));
      Assert(confirmation.IsChecksumValid && confirmation.Header.Type == DropItemConfirmation.MessageType && confirmation.Header.Size == DropItemConfirmation.PacketSize, "MSG_CNFDropItem was not encoded with the legacy 28-byte layout.");
      var create = codec.Decode(new CreateItemConfirmation(1000, 1001, 10002, new LegacyItem(500, 1, 2, 0, 0, 0, 0), 3).ToFrame(codec, 38, 21));
      Assert(create.IsChecksumValid && create.Header.Type == CreateItemConfirmation.MessageType && create.Header.Size == CreateItemConfirmation.PacketSize && create.Payload.Length == CreateItemConfirmation.PayloadSize, "MSG_CreateItem was not encoded with the legacy item broadcast layout.");
  }

  static void WorldGroundItemDrop()
  {
      var mob = new byte[LegacyAccountSnapshot.CharacterStride];
      new LegacyScore(10, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
      var item = new LegacyItem(500, 1, 2, 0, 0, 0, 0);
      item.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
      var hub = new WorldHub();
      Assert(hub.Enter(1, "DROP_ITEM", (_, _) => ValueTask.CompletedTask) && hub.SetCharacterState(1, 0, 0, 0, 0, 0, 1000, 1000, mob), "Drop-item participant state was not set.");
      var blocker = hub.EnterGroundItem(new LegacyItem(501, 0, 0, 0, 0, 0, 0), 1000, 1000, requestedItemId: 1);
      Assert(blocker == 10001, "Drop-item ground blocker was not registered.");
      Assert(hub.TryDropItem(1, 1, 0, 2, 1000, 1000, out var outcome) == LegacyDropItemResult.Accepted && outcome is not null && outcome.WireItemId == 10002 && outcome.PositionX == 999 && outcome.PositionY == 999, "A carry item was not dropped into the first legacy adjacent free cell.");
      Assert(hub.TryGetCharacterSnapshot(1, out var after, out _, out _) && after is not null && LegacyItem.Read(after.AsSpan(LegacyAccountSnapshot.MobCarryOffset)).Index == 0, "The dropped carry slot was not cleared authoritatively.");

      var protectedMob = new byte[LegacyAccountSnapshot.CharacterStride];
      new LegacyScore(10, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0).Write(protectedMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
      new LegacyItem(508, 0, 0, 0, 0, 0, 0).Write(protectedMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
      Assert(hub.Enter(2, "DROP_PROTECTED", (_, _) => ValueTask.CompletedTask) && hub.SetCharacterState(2, 0, 0, 0, 0, 0, 1000, 1000, protectedMob), "Protected drop-item participant state was not set.");
      Assert(hub.TryDropItem(2, 1, 0, 0, 1002, 1000, out _) == LegacyDropItemResult.ProtectedItem, "A protected item was not rejected.");
  }

  static void GroundItemDecayTimer()
  {
      var mob = new byte[LegacyAccountSnapshot.CharacterStride];
      new LegacyScore(10, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0)
          .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
      var hub = new WorldHub();
      Assert(hub.Enter(1, "GROUND_DECAY_NEAR", (_, _) => ValueTask.CompletedTask) && hub.SetCharacterState(1, 0, 0, 0, 0, 0, 1000, 1000, mob), "Ground-decay participant was not registered.");
      var farMob = mob.ToArray();
      Assert(hub.Enter(2, "GROUND_DECAY_FAR", (_, _) => ValueTask.CompletedTask) && hub.SetCharacterState(2, 0, 0, 0, 0, 0, 1100, 1100, farMob), "Far ground-decay participant was not registered.");

      var wireItemId = hub.EnterGroundItem(new LegacyItem(500, 0, 0, 0, 0, 0, 0), 1001, 1001, requestedItemId: 1);
      Assert(wireItemId == 10_001, "Ground-decay fixture did not receive the expected wire ID.");
      var firstSecond = new DateTime(2026, 9, 18, 12, 0, 0);
      Assert(!hub.TryProcessGroundItemSecond(firstSecond.AddSeconds(1), out _), "Ground-item decay ran on an odd second.");
      Assert(hub.TryProcessGroundItemSecond(firstSecond, out var firstPlan) && firstPlan is null, "The first ground-item decay slot should only decrement Delay.");
      Assert(!hub.TryProcessGroundItemSecond(firstSecond, out _), "The same ground-item decay second was applied twice.");

      for (var tick = 1; tick < 90; tick++)
          Assert(hub.TryProcessGroundItemSecond(firstSecond.AddSeconds(tick * 2), out var decrementPlan) && decrementPlan is null, "A ground item decayed before its 90-delay countdown completed.");

      Assert(hub.TryProcessGroundItemSecond(firstSecond.AddSeconds(180), out var removalPlan) && removalPlan is { RemovedItems.Count: 1 }, "The ground item did not decay on the slot after Delay reached zero.");
      Assert(removalPlan!.RemovedItems[0].Item.ItemId == 1 && removalPlan.RemovedItems[0].RecipientConnectionIds.SequenceEqual([1]), "Ground-item decay did not remove the correct item or preserve the 33x33 recipient view.");
      Assert(!hub.TryProcessGroundItemSecond(firstSecond.AddSeconds(180), out _), "The same ground-item removal slot was applied twice.");
  }

  static void TradingItemWire()
  {
      var codec = LegacyFrameCodec.CreateDefault();
      var payload = new byte[TradingItemRequest.PayloadSize];
      payload[0] = 1;
      payload[1] = 7;
      payload[2] = 1;
      payload[3] = 5;
      BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4), 0x1234);
      BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(6), 0xA55A);
      var requestFrame = codec.Decode(codec.Encode(TradingItemRequest.MessageType, 1, 39, payload, 22));
      Assert(TradingItemRequest.TryParse(requestFrame, out var request) && request is not null && request.SourcePlace == 1 && request.SourceSlot == 7 && request.DestinationPlace == 1 && request.DestinationSlot == 5 && request.TargetId == 0x1234 && request.OpaquePadding == 0xA55A, "7.69 MSG_SwapItem did not parse the source/destination fields, TargetID, and opaque ABI tail.");

      var confirmation = codec.Decode(request!.ToFrame(codec, requestFrame.Header.ClientTick, requestFrame.Header.KeywordIndex, requestFrame.Header.Id));
      Assert(confirmation.IsChecksumValid && confirmation.Header.Type == TradingItemConfirmation.MessageType && confirmation.Header.Id == requestFrame.Header.Id && confirmation.Header.ClientTick == requestFrame.Header.ClientTick && confirmation.Header.KeywordIndex == requestFrame.Header.KeywordIndex && confirmation.Header.Size == 20 && confirmation.Header.Size == TradingItemConfirmation.PacketSize && confirmation.Payload.Span.SequenceEqual(payload), "7.69 MSG_SwapItem confirmation did not echo the request header and preserve its 20-byte frame payload.");

      var undersizedPayload = new byte[6];
      var undersizedFrame = codec.Decode(codec.Encode(TradingItemRequest.MessageType, 1, 39, undersizedPayload, 22));
      Assert(!TradingItemRequest.TryParse(undersizedFrame, out _), "An 18-byte MSG_SwapItem frame without its native ABI tail was accepted.");
  }

static void WorldCarryItemSwap()
{
      var mob = new byte[LegacyAccountSnapshot.CharacterStride];
      new LegacyScore(10, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
      new LegacyItem(500, 1, 0, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
      new LegacyItem(501, 2, 0, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + LegacyItem.SizeInBytes));
      var hub = new WorldHub();
      Assert(hub.Enter(1, "TRADING_ITEM", (_, _) => ValueTask.CompletedTask) && hub.SetCharacterState(1, 0, 0, 0, 0, 0, 1000, 1000, mob), "Trading-item participant state was not set.");
      Assert(hub.TryTradeCarryItems(1, 0, 1, out var outcome) == LegacyTradingItemResult.Accepted && outcome is not null && !outcome.WasMerged && outcome.SourceSlotItem.Index == 501 && outcome.DestinationSlotItem.Index == 500, "A valid carry swap was rejected or returned the wrong post-swap slot state.");
      Assert(hub.TryGetCharacterSnapshot(1, out var after, out _, out _) && after is not null && LegacyItem.Read(after.AsSpan(LegacyAccountSnapshot.MobCarryOffset)).Index == 501 && LegacyItem.Read(after.AsSpan(LegacyAccountSnapshot.MobCarryOffset + LegacyItem.SizeInBytes)).Index == 500, "The carry swap did not mutate both authoritative slots.");
      Assert(hub.TryTradeCarryItems(1, 60, 1, out _) == LegacyTradingItemResult.InvalidSlot, "A reserved tail carry slot was accepted for trading.");
      Assert(hub.TryTradeCarryItems(1, 2, 1, out _) == LegacyTradingItemResult.SourceEmpty, "An empty source carry slot was accepted for trading.");
      Assert(hub.TryTradeCarryItems(1, 0, 0, out _) == LegacyTradingItemResult.SameSlot, "A same-slot carry trade was accepted.");

      static WorldHub CreateCapacityHub(int connectionId, int sourceSlot, bool firstExpansion = false, bool secondExpansion = false)
      {
          var capacityMob = new byte[LegacyAccountSnapshot.CharacterStride];
          new LegacyScore(10, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0)
              .Write(capacityMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
          new LegacyItem(500, 0, 0, 0, 0, 0, 0)
              .Write(capacityMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + sourceSlot * LegacyItem.SizeInBytes));
          if (firstExpansion)
              new LegacyItem(3467, 0, 0, 0, 0, 0, 0)
                  .Write(capacityMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (60 * LegacyItem.SizeInBytes)));
          if (secondExpansion)
              new LegacyItem(3467, 0, 0, 0, 0, 0, 0)
                  .Write(capacityMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (61 * LegacyItem.SizeInBytes)));

          var capacityHub = new WorldHub();
          Assert(capacityHub.Enter(connectionId, $"TRADING_CAPACITY_{connectionId}", (_, _) => ValueTask.CompletedTask) &&
              capacityHub.SetCharacterState(connectionId, 0, 0, 0, 0, 0, 1000, 1000, capacityMob), "Carry-capacity participant state was not set.");
          return capacityHub;
      }

      Assert(CreateCapacityHub(2, 0).TryTradeCarryItems(2, 0, 30, out _) == LegacyTradingItemResult.InvalidSlot,
          "A carry destination beyond the base 30-slot capacity was accepted.");
      Assert(CreateCapacityHub(3, 0, firstExpansion: true).TryTradeCarryItems(3, 0, 44, out _) == LegacyTradingItemResult.Accepted,
          "The first expansion item did not raise destination capacity to 45 slots.");
      Assert(CreateCapacityHub(4, 0, firstExpansion: true).TryTradeCarryItems(4, 0, 45, out _) == LegacyTradingItemResult.InvalidSlot,
          "The first expansion item incorrectly raised destination capacity beyond 45 slots.");
      Assert(CreateCapacityHub(5, 0, firstExpansion: true, secondExpansion: true).TryTradeCarryItems(5, 0, 59, out _) == LegacyTradingItemResult.Accepted,
          "Two expansion items did not raise destination capacity to 60 slots.");
      Assert(CreateCapacityHub(6, 30).TryTradeCarryItems(6, 30, 0, out _) == LegacyTradingItemResult.Accepted,
          "The source rejected a legacy-valid carry index above MaxCarry even though only DestPos is checked.");
  }

static void WorldEquipmentItemSwap()
{
    var mob = new byte[LegacyAccountSnapshot.CharacterStride];
    mob[LegacyAccountSnapshot.MobClassOffset] = 0;
    new LegacyScore(10, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    var source = new LegacyItem(7, 0, 0, 0, 0, 0, 0);
    source.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));

    var hub = new WorldHub(itemData: CreateCombatItemDataTable());
    Assert(hub.Enter(1, "EQUIP", (_, _) => ValueTask.CompletedTask), "Equipment-swap participant was not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 0, 0, 0, 1000, 1000, mob, LegacyAccountSnapshot.ClassMasterMortal, affect: ReadOnlyMemory<byte>.Empty, mobExtra: new byte[LegacyAccountSnapshot.MobExtraStride]), "Equipment-swap state was not registered.");

    var accepted = hub.TryTradeItems(1, LegacyItemPlace.Carry, 0, LegacyItemPlace.Equip, 2, out var outcome);
    Assert(accepted == LegacyTradingItemResult.Accepted && outcome is { SourcePlace: LegacyItemPlace.Carry, SourceSlot: 0, DestinationPlace: LegacyItemPlace.Equip, DestinationSlot: 2, SourceSlotItem.Index: 0, DestinationSlotItem.Index: 7 }, "Carry-to-equipment swap did not preserve the 7.69 places, slots, and authoritative items.");

    var cargoResult = hub.TryTradeItems(1, LegacyItemPlace.Equip, 2, LegacyItemPlace.Cargo, 0, out _);
    Assert(cargoResult == LegacyTradingItemResult.CargoUnavailable, "Cargo was accepted before account-cargo state/persistence was modeled.");

    var noDataMob = new byte[LegacyAccountSnapshot.CharacterStride];
    noDataMob[LegacyAccountSnapshot.MobClassOffset] = 0;
    new LegacyScore(10, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(noDataMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    source.Write(noDataMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
    var noDataHub = new WorldHub();
    Assert(noDataHub.Enter(2, "NODATA", (_, _) => ValueTask.CompletedTask) && noDataHub.SetCharacterState(2, 0, 0, 0, 0, 0, 1000, 1000, noDataMob), "No-data equipment-swap participant was not registered.");
    Assert(noDataHub.TryTradeItems(2, LegacyItemPlace.Carry, 0, LegacyItemPlace.Equip, 2, out _) == LegacyTradingItemResult.ItemDataUnavailable, "Equipment swap without ItemList data did not fail closed.");
}

static void WorldCarryItemMerge()
{
      var mob = new byte[LegacyAccountSnapshot.CharacterStride];
      new LegacyScore(10, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
      new LegacyItem(413, 9, 7, 61, 4, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
      new LegacyItem(413, 8, 6, 0, 0, 61, 6).Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + LegacyItem.SizeInBytes));
      var hub = new WorldHub();
      Assert(hub.Enter(1, "STACK_MERGE", (_, _) => ValueTask.CompletedTask) && hub.SetCharacterState(1, 0, 0, 0, 0, 0, 1000, 1000, mob), "Stack-merge participant state was not set.");

      Assert(hub.TryTradeCarryItems(1, 0, 1, out var outcome) == LegacyTradingItemResult.Accepted && outcome is { WasMerged: true }, "Matching stackable carry items were not merged.");
      var mergedOutcome = outcome ?? throw new InvalidOperationException("Accepted stack merge did not return its slot states.");
      Assert(mergedOutcome.SourceSlotItem.Index == 0 && mergedOutcome.DestinationSlotItem is { Index: 413, Effect1: 61, Value1: 10 }, "A merge at or below 254 did not clear the source and place the combined stack in the destination.");
      Assert(hub.TryGetCharacterSnapshot(1, out var after, out _, out _) && after is not null &&
          LegacyItem.Read(after.AsSpan(LegacyAccountSnapshot.MobCarryOffset)).Index == 0 &&
          LegacyItem.Read(after.AsSpan(LegacyAccountSnapshot.MobCarryOffset + LegacyItem.SizeInBytes)) is { Index: 413, Effect1: 61, Value1: 10 },
          "The merged stack was not committed to the authoritative carry slots.");

      var overflowMob = new byte[LegacyAccountSnapshot.CharacterStride];
      new LegacyScore(10, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0).Write(overflowMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
      new LegacyItem(412, 61, 119, 0, 0, 0, 0).Write(overflowMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
      new LegacyItem(412, 61, 5, 0, 0, 0, 0).Write(overflowMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + LegacyItem.SizeInBytes));
      Assert(hub.Enter(2, "STACK_OVERFLOW", (_, _) => ValueTask.CompletedTask) && hub.SetCharacterState(2, 0, 0, 0, 0, 0, 1000, 1000, overflowMob), "Overflow stack-merge participant state was not set.");

      Assert(hub.TryTradeCarryItems(2, 0, 1, out var overflow) == LegacyTradingItemResult.Accepted && overflow is { WasMerged: true } &&
          overflow.SourceSlotItem is { Index: 412, Effect1: 61, Value1: 4 } && overflow.DestinationSlotItem is { Index: 412, Effect1: 61, Value1: 120 },
          "Stack overflow did not preserve the 7.69 120/120 split behavior.");

      var configuredStackMob = new byte[LegacyAccountSnapshot.CharacterStride];
      new LegacyScore(10, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0).Write(configuredStackMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
      new LegacyItem(4900, 61, 2, 0, 0, 0, 0).Write(configuredStackMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
      new LegacyItem(4900, 61, 3, 0, 0, 0, 0).Write(configuredStackMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + LegacyItem.SizeInBytes));
      Assert(hub.Enter(3, "STACK_CONFIG", (_, _) => ValueTask.CompletedTask) && hub.SetCharacterState(3, 0, 0, 0, 0, 0, 1000, 1000, configuredStackMob), "Configured-stack participant state was not set.");
      Assert(hub.TryTradeCarryItems(3, 0, 1, out var configuredStack) == LegacyTradingItemResult.Accepted &&
          configuredStack is { WasMerged: true, DestinationSlotItem: { Index: 4900, Effect1: 61, Value1: 5 } },
          "An item present in the 7.69 groupItens.json was not stackable.");

      var excludedStackMob = new byte[LegacyAccountSnapshot.CharacterStride];
      new LegacyScore(10, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0).Write(excludedStackMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
      new LegacyItem(416, 61, 2, 0, 0, 0, 0).Write(excludedStackMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
      new LegacyItem(416, 61, 3, 0, 0, 0, 0).Write(excludedStackMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + LegacyItem.SizeInBytes));
      Assert(hub.Enter(4, "STACK_NOT_CONFIG", (_, _) => ValueTask.CompletedTask) && hub.SetCharacterState(4, 0, 0, 0, 0, 0, 1000, 1000, excludedStackMob), "Non-configured-stack participant state was not set.");
      Assert(hub.TryTradeCarryItems(4, 0, 1, out var nonConfiguredStack) == LegacyTradingItemResult.Accepted &&
          nonConfiguredStack is { WasMerged: false, SourceSlotItem.Index: 416, DestinationSlotItem.Index: 416 },
          "An item omitted from the 7.69 groupItens.json was merged.");
  }

static void WorldTradingItemCarryCompatibility()
{
    static WorldHub CreateHub(int connectionId, LegacyItem source, LegacyItem destination)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(10, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        source.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
        destination.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + LegacyItem.SizeInBytes));
        var hub = new WorldHub();
        Assert(hub.Enter(connectionId, $"TRADING_RULES_{connectionId}", (_, _) => ValueTask.CompletedTask) &&
            hub.SetCharacterState(connectionId, 0, 0, 0, 0, 0, 1000, 1000, mob), "Trading-rule participant state was not set.");
        return hub;
    }

    var costume = new LegacyItem(4150, 0, 0, 0, 0, 0, 0);
    var costumeHub = CreateHub(1, costume, new LegacyItem(500, 0, 0, 0, 0, 0, 0));
    Assert(costumeHub.TryTradeCarryItems(1, 0, 1, out var costumeOutcome) == LegacyTradingItemResult.Accepted &&
        costumeOutcome is { SourceSlotItem.Index: 500, DestinationSlotItem: var movedCostume } && movedCostume == costume,
        "Carry-to-carry trade applied the equipment-only costume date rule or changed the item effects.");

    var expiredAccessory = new LegacyItem(3980, LegacyItemEffect.WDay, 18, LegacyItemEffect.WMonth, 9, LegacyItemEffect.Year, 26);
    var expiryHub = CreateHub(2, expiredAccessory, new LegacyItem(501, 0, 0, 0, 0, 0, 0));
    Assert(expiryHub.TryTradeCarryItems(2, 0, 1, out var expiryOutcome) == LegacyTradingItemResult.Accepted &&
        expiryOutcome is { SourceSlotItem.Index: 501, DestinationSlotItem: var movedExpiredAccessory } && movedExpiredAccessory == expiredAccessory,
        "Carry-to-carry trade applied equipment-only expiry cleanup or changed the item's date effects.");

    var undatedFairy = new LegacyItem(3913, 0, 0, 0, 0, 0, 0);
    var fairyHub = CreateHub(3, undatedFairy, new LegacyItem(502, 0, 0, 0, 0, 0, 0));
    Assert(fairyHub.TryTradeCarryItems(3, 0, 1, out var fairyOutcome) == LegacyTradingItemResult.Accepted &&
        fairyOutcome is { DestinationSlotItem: var movedFairy } && movedFairy == undatedFairy,
        "Carry-to-carry trade applied the equipment-only fairy initialization rule.");

    var december = new DateTime(2026, 12, 20, 12, 0, 0);
    var datedCostume = LegacyItemDateMath.SetItemDate(costume, 30, december);
    Assert(datedCostume is { Effect1: LegacyItemEffect.WDay, Value1: 21, Effect2: LegacyItemEffect.WMonth, Value2: 1, Effect3: LegacyItemEffect.Year, Value3: 27 },
        "The faithful date helper failed to roll a 30-day item into January of the next year.");
}

static void DeleteItemWire()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var payload = new byte[DeleteItemRequest.PayloadSize];
    BinaryPrimitives.WriteInt32LittleEndian(payload, 4);
    BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int)), 500);
    var encoded = codec.Encode(DeleteItemRequest.MessageType, 1, 40, payload, 23);
    var frame = codec.Decode(encoded);
    Assert(DeleteItemRequest.TryParse(frame, out var request) && request is { Slot: 4, ItemIndex: 500 }, "MSG_DeleteItem did not parse the legacy Slot and sIndex fields.");
    Assert(frame.Header.Size == 20 && frame.Payload.Length == DeleteItemRequest.PayloadSize, "MSG_DeleteItem did not preserve the 20-byte legacy layout.");

    encoded[3] ^= 0x40;
    Assert(!DeleteItemRequest.TryParse(codec.Decode(encoded), out _), "MSG_DeleteItem accepted a frame with an invalid checksum.");
}

static void WorldDeleteCarryItem()
{
    var mob = new byte[LegacyAccountSnapshot.CharacterStride];
    new LegacyScore(10, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0)
        .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    new LegacyItem(500, 1, 2, 3, 4, 5, 6)
        .Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (4 * LegacyItem.SizeInBytes)));
    new LegacyItem(501, 7, 8, 9, 10, 11, 12)
        .Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (5 * LegacyItem.SizeInBytes)));

    var hub = new WorldHub();
    Assert(hub.Enter(1, "DELETE_ITEM", (_, _) => ValueTask.CompletedTask) &&
        hub.SetCharacterState(1, 0, 0, 0, 0, 0, 1000, 1000, mob), "Delete-item participant state was not set.");

    Assert(hub.TryDeleteCarryItem(1, 4, 500) == LegacyDeleteItemResult.Accepted, "A matching authoritative carry item was not deleted.");
    Assert(hub.TryGetCharacterSnapshot(1, out var after, out _, out _) && after is not null &&
        LegacyItem.Read(after.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (4 * LegacyItem.SizeInBytes))) == default,
        "Deleting a carry item did not clear the authoritative slot.");
    Assert(hub.TryDeleteCarryItem(1, 4, 500) == LegacyDeleteItemResult.SourceEmpty, "A repeated delete did not reject an empty source slot.");
    Assert(hub.TryDeleteCarryItem(1, 5, 500) == LegacyDeleteItemResult.ItemChanged, "A stale item index was allowed to delete a different authoritative item.");
    Assert(hub.TryGetCharacterSnapshot(1, out var unchanged, out _, out _) && unchanged is not null &&
        LegacyItem.Read(unchanged.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (5 * LegacyItem.SizeInBytes))) is { Index: 501, Effect1: 7, Value1: 8 },
        "Rejecting a stale delete changed the replacement item.");
    Assert(hub.TryDeleteCarryItem(1, 60, 501) == LegacyDeleteItemResult.InvalidSlot, "The four reserved tail slots were accepted for deletion.");
    Assert(hub.TryDeleteCarryItem(1, 5, LegacyItemDataTable.MaxItemIndex) == LegacyDeleteItemResult.InvalidItemIndex, "An out-of-range item index was accepted for deletion.");
    Assert(hub.TryDeleteCarryItem(99, 0, 500) == LegacyDeleteItemResult.ParticipantNotFound, "Deletion against an unknown participant was accepted.");
}

static void SplitItemWire()
{
      var codec = LegacyFrameCodec.CreateDefault();
      var payload = new byte[SplitItemRequest.PayloadSize];
      BinaryPrimitives.WriteInt32LittleEndian(payload, 4);
      BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4), 9999);
      BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(8), 3);
      var frame = codec.Decode(codec.Encode(SplitItemRequest.MessageType, 1, 40, payload, 23));
      Assert(SplitItemRequest.TryParse(frame, out var request) && request is not null && request.Slot == 4 && request.ItemIndex == 9999 && request.Quantity == 3, "MSG_SplitItem did not preserve the legacy Slot, sIndex, and Num fields.");
      Assert(frame.Header.Size == SplitItemRequest.PacketSize && frame.Payload.Length == SplitItemRequest.PayloadSize, "MSG_SplitItem did not preserve the 24-byte legacy layout.");
  }

  static void WorldSplitItem()
  {
      var mob = new byte[LegacyAccountSnapshot.CharacterStride];
      new LegacyScore(10, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
      var stack = new LegacyItem(413, 61, 10, 0, 0, 0, 0);
      stack.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (4 * LegacyItem.SizeInBytes)));
      new LegacyItem(500, 0, 0, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
      var hub = new WorldHub();
      Assert(hub.Enter(1, "SPLIT_ITEM", (_, _) => ValueTask.CompletedTask) && hub.SetCharacterState(1, 0, 0, 0, 0, 0, 1000, 1000, mob), "Split-item participant state was not set.");

      Assert(hub.TrySplitCarryItem(1, 4, 9999, 3, out var outcome) == LegacySplitItemResult.Accepted && outcome is not null, "A valid stack split was rejected.");
      Assert(outcome!.SourceSlot == 4 && outcome.DestinationSlot == 1 && outcome.RequestedItemIndex == 9999, "Split-item slots or ignored client index were not preserved.");
      Assert(outcome.UpdatedSourceItem.Index == 413 && outcome.UpdatedSourceItem.Effect1 == 61 && outcome.UpdatedSourceItem.Value1 == 7, "The original stack did not retain seven units.");
      Assert(outcome.SplitItem.Index == 413 && outcome.SplitItem.Effect1 == 61 && outcome.SplitItem.Value1 == 3 && outcome.SplitItem.Effect2 == 0, "The new split stack did not reproduce the legacy zeroed-item construction.");
      Assert(hub.TrySplitCarryItem(1, 4, 413, 7, out _) == LegacySplitItemResult.InsufficientAmount, "A split equal to the source amount was accepted.");
      Assert(hub.TrySplitCarryItem(1, 4, 413, 0, out _) == LegacySplitItemResult.InvalidQuantity, "A zero split quantity was accepted.");
      Assert(hub.TrySplitCarryItem(1, 60, 413, 1, out _) == LegacySplitItemResult.InvalidSlot, "A reserved carry tail slot was accepted as a split source.");
      Assert(hub.TrySplitCarryItem(1, 0, 413, 1, out _) == LegacySplitItemResult.UnsupportedItem, "A non-stackable item was accepted for splitting.");

      var fullMob = new byte[LegacyAccountSnapshot.CharacterStride];
      new LegacyScore(10, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0).Write(fullMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
      for (var slot = 0; slot < LegacyAccountSnapshot.MobCarryCount; slot++)
          new LegacyItem(413, 61, 10, 0, 0, 0, 0).Write(fullMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (slot * LegacyItem.SizeInBytes)));
      var fullHub = new WorldHub();
      Assert(fullHub.Enter(2, "SPLIT_FULL", (_, _) => ValueTask.CompletedTask) && fullHub.SetCharacterState(2, 0, 0, 0, 0, 0, 1000, 1000, fullMob), "Full split-item participant state was not set.");
      Assert(fullHub.TrySplitCarryItem(2, 4, 413, 1, out _) == LegacySplitItemResult.InventoryFull, "A full carry accepted a split without a destination slot.");

      var baseCapacityMob = new byte[LegacyAccountSnapshot.CharacterStride];
      new LegacyScore(10, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0).Write(baseCapacityMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
      for (var slot = 0; slot < 30; slot++)
          new LegacyItem(500, 0, 0, 0, 0, 0, 0).Write(baseCapacityMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (slot * LegacyItem.SizeInBytes)));
      new LegacyItem(413, 61, 10, 0, 0, 0, 0).Write(baseCapacityMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (4 * LegacyItem.SizeInBytes)));
      var baseCapacityHub = new WorldHub();
      Assert(baseCapacityHub.Enter(3, "SPLIT_BASE_CAPACITY", (_, _) => ValueTask.CompletedTask) && baseCapacityHub.SetCharacterState(3, 0, 0, 0, 0, 0, 1000, 1000, baseCapacityMob), "Base-capacity split participant state was not set.");
      Assert(baseCapacityHub.TrySplitCarryItem(3, 4, 413, 1, out _) == LegacySplitItemResult.InventoryFull,
          "SplitItem inserted into an empty array slot beyond the base MaxCarry of 30.");

      var expandedCapacityMob = baseCapacityMob.ToArray();
      new LegacyItem(3467, 0, 0, 0, 0, 0, 0).Write(expandedCapacityMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (60 * LegacyItem.SizeInBytes)));
      var expandedCapacityHub = new WorldHub();
      Assert(expandedCapacityHub.Enter(4, "SPLIT_EXPANDED_CAPACITY", (_, _) => ValueTask.CompletedTask) && expandedCapacityHub.SetCharacterState(4, 0, 0, 0, 0, 0, 1000, 1000, expandedCapacityMob), "Expanded-capacity split participant state was not set.");
      Assert(expandedCapacityHub.TrySplitCarryItem(4, 4, 413, 1, out var expandedSplit) == LegacySplitItemResult.Accepted && expandedSplit is { DestinationSlot: 30 },
          "SplitItem did not use the first slot unlocked by a 3467 carry expansion item.");
  }

  static void UpdateItemWire()
  {
      var codec = LegacyFrameCodec.CreateDefault();
      var payload = new byte[UpdateItemRequest.PayloadSize];
      BinaryPrimitives.WriteInt32LittleEndian(payload, 10_042);
      BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int)), 1);
      var requestFrame = codec.Decode(codec.Encode(UpdateItemRequest.MessageType, 30_000, 41, payload, 24));
      Assert(UpdateItemRequest.TryParse(requestFrame, out var request) && request is not null && request.ItemId == 10_042 && request.State == 1, "MSG_UpdateItem did not parse the legacy ItemID and State fields.");
      Assert(requestFrame.Header.Size == UpdateItemRequest.PacketSize && requestFrame.Payload.Length == UpdateItemRequest.PayloadSize, "MSG_UpdateItem did not preserve the 20-byte legacy layout.");

      var confirmation = codec.Decode(new UpdateItemConfirmation(10_042, 1).ToFrame(codec, 41, 24, 30_000));
      Assert(confirmation.IsChecksumValid && confirmation.Header.Type == UpdateItemConfirmation.MessageType && confirmation.Header.Size == UpdateItemConfirmation.PacketSize && confirmation.Payload.Span.SequenceEqual(payload), "MSG_UpdateItem confirmation did not preserve the legacy fields and scene destination.");
  }

  static void OfficialGroundMaskTable()
  {
      var table = LegacyGroundMaskTable.CreateOfficial();
      var nonZero = 0;
      for (var mask = 0; mask < LegacyGroundMaskTable.MaskCount; mask++)
          for (var rotation = 0; rotation < LegacyGroundMaskTable.RotationCount; rotation++)
              for (var y = 0; y < LegacyGroundMaskTable.Height; y++)
                  for (var x = 0; x < LegacyGroundMaskTable.Width; x++)
                      if (table[mask, rotation, y, x] != 0)
                          nonZero++;

      Assert(nonZero == 100, "The official server ground-mask slice did not preserve the 100 non-zero cells from the 7.59 client table.");
      Assert(table[1, 0, 2, 1] == 18 && table[1, 1, 1, 2] == 18 && table[2, 0, 2, 0] == 18 && table[3, 1, 2, 2] == 18 && table[5, 0, 1, 5] == 18, "The official ground-mask values or rotations differ from the Reference759 table.");
      Assert(table[0, 0, 2, 2] == 0 && table[1, 0, 1, 1] == 0 && table[5, 0, 2, 2] == 0, "The official ground-mask zero cells were not preserved.");

      var map = new LegacyMapGrid(new byte[LegacyMapGrid.HeightMapSize], new byte[LegacyMapGrid.AttributeMapSize]);
      Assert(table.TryApply(map, 1, LegacyMapItemStateCodes.Open, LegacyMapItemStateCodes.Locked, 100, 100, 0, out var lockedHeight) && lockedHeight == 18 && map.GetTerrainHeight(99, 100) == 18 && map.GetTerrainHeight(101, 100) == 18, "The official mask did not apply the expected 18-point horizontal height delta.");
      Assert(table.TryApply(map, 1, LegacyMapItemStateCodes.Locked, LegacyMapItemStateCodes.Open, 100, 100, 0, out var openHeight) && openHeight == 0 && map.GetTerrainHeight(99, 100) == 0 && map.GetTerrainHeight(101, 100) == 0, "The official mask did not reverse the legacy height delta.");
  }

  static void MapItemCatalogAndGroundMask()
  {
      var itemDataBody = new byte[LegacyItemDataTable.BodySizeInBytes];
      WriteItemListRecord(itemDataBody, 471, "GATE", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Ground, 2), (LegacyItemEffect.KeyId, 10));
      var encodedItemData = new byte[LegacyItemDataTable.FileSizeInBytes];
      for (var offset = 0; offset < itemDataBody.Length; offset++)
          encodedItemData[offset] = (byte)(itemDataBody[offset] ^ 0x5A);
      var itemData = LegacyItemDataTable.Load(encodedItemData);

      var init = new byte[24];
      WriteInitItem(init, 0, 217, 215, 471, 1);
      WriteInitItem(init, 8, 223, 221, 471, 3);
      WriteInitItem(init, 16, 0, 0, 0, 0);
      var definitions = LegacyMapItemCatalog.LoadDefinitions(init);
      Assert(definitions.Count == 2 && definitions[0] is { PositionX: 217, PositionY: 215, ItemIndex: 471, Rotate: 1 }, "InitItem.bin rows or the zero-position sentinel were not decoded like BASE_ReadInitItem.");

      var map = new LegacyMapGrid(new byte[LegacyMapGrid.HeightMapSize], new byte[LegacyMapGrid.AttributeMapSize]);
      var states = LegacyMapItemCatalog.CreateInitialStates(definitions, itemData, map);
      Assert(states.Count == 2 && states[0] is { ItemId: 1, GridCharge: 2, State: LegacyMapItemStateCodes.Open, Delay: 90 }, "Static map-item state did not preserve the legacy CreateItem defaults or EF_GROUND.");

      var masks = new int[LegacyGroundMaskTable.MaskCount, LegacyGroundMaskTable.RotationCount, LegacyGroundMaskTable.Height, LegacyGroundMaskTable.Width];
      masks[0, 0, 2, 2] = 3;
      masks[0, 0, 2, 3] = 2;
      var maskTable = new LegacyGroundMaskTable(masks);
      Assert(maskTable.TryApply(map, 0, LegacyMapItemStateCodes.Open, LegacyMapItemStateCodes.Locked, 100, 100, 0, out var lockedHeight) && lockedHeight == 2 && map.GetTerrainHeight(100, 100) == 3 && map.GetTerrainHeight(101, 100) == 2, "BASE_UpdateItem's opening-to-locked height delta was not applied to the dynamic height grid.");
      Assert(maskTable.TryApply(map, 0, LegacyMapItemStateCodes.Locked, LegacyMapItemStateCodes.Open, 100, 100, 0, out var openHeight) && openHeight == 0 && map.GetTerrainHeight(100, 100) == 0 && map.GetTerrainHeight(101, 100) == 0, "BASE_UpdateItem's locked-to-open reverse delta was not applied.");
      Assert(!maskTable.TryApply(map, 0, LegacyMapItemStateCodes.Open, LegacyMapItemStateCodes.Open, 100, 100, 0, out _), "A no-op map-item state transition incorrectly mutated the height grid.");
  }

  static void WorldMapItemUpdate()
  {
      var itemDataBody = new byte[LegacyItemDataTable.BodySizeInBytes];
      WriteItemListRecord(itemDataBody, 471, "GATE", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Ground, 2), (LegacyItemEffect.KeyId, 10));
      WriteItemListRecord(itemDataBody, 472, "KEY", unique: 0, position: 0, grade: 0, (LegacyItemEffect.KeyId, 10));
      var encodedItemData = new byte[LegacyItemDataTable.FileSizeInBytes];
      for (var offset = 0; offset < itemDataBody.Length; offset++)
          encodedItemData[offset] = (byte)(itemDataBody[offset] ^ 0x5A);
      var itemData = LegacyItemDataTable.Load(encodedItemData);

      var init = new byte[16];
      WriteInitItem(init, 0, 217, 215, 471, 1);
      WriteInitItem(init, 8, 0, 0, 0, 0);
      var definitions = LegacyMapItemCatalog.LoadDefinitions(init);
      var map = new LegacyMapGrid(new byte[LegacyMapGrid.HeightMapSize], new byte[LegacyMapGrid.AttributeMapSize]);
      var states = LegacyMapItemCatalog.CreateInitialStates(definitions, itemData, map);
      var masks = new int[LegacyGroundMaskTable.MaskCount, LegacyGroundMaskTable.RotationCount, LegacyGroundMaskTable.Height, LegacyGroundMaskTable.Width];
      masks[2, 1, 2, 2] = 3;
      var maskTable = new LegacyGroundMaskTable(masks);
      var mob = new byte[LegacyAccountSnapshot.CharacterStride];
      new LegacyScore(50, 0, 100, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 40, 0, 0, 0)
          .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
      new LegacyItem(472, 0, 0, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));

      var hub = new WorldHub(mapGrid: map, itemData: itemData);
      hub.ConfigureMapItems(states, maskTable);
      Assert(hub.TryGetMapItem(10_001, out var locked) && locked is { State: LegacyMapItemStateCodes.Locked, Delay: 0 }, "InitItem gate with EF_KEYID was not locked during world configuration.");
      Assert(hub.Enter(1, "MAP_GATE", (_, _) => ValueTask.CompletedTask) && hub.SetCharacterState(1, 0, 0, 1, 0, 0, 217, 215, mob), "Map-item update participant was not registered with a live MOB.");

      var result = hub.TryUpdateMapItem(1, 10_001, LegacyMapItemStateCodes.Open, out var outcome);
      Assert(result == LegacyUpdateItemResult.Accepted && outcome is { StateChanged: true, ConsumedKeySlot: 0, MapItem.State: LegacyMapItemStateCodes.Open }, "MSG_UpdateItem did not unlock the gate, consume the matching key, and expose the resulting state.");
      Assert(LegacyItem.Read(outcome!.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes)).Index == 0, "The consumed map-item key remained in the authoritative carry snapshot.");
      Assert(map.GetTerrainHeight(217, 215) == 0, "Unlocking the gate did not reverse the legacy ground-mask height delta.");

      result = hub.TryUpdateMapItem(1, 10_001, LegacyMapItemStateCodes.Locked, out _);
      Assert(result == LegacyUpdateItemResult.MissingKey, "A gate transition requiring EF_KEYID was accepted without a matching carry key.");
  }

  static void MapItemMinuteTimer()
  {
      var itemDataBody = new byte[LegacyItemDataTable.BodySizeInBytes];
      WriteItemListRecord(itemDataBody, 471, "GATE", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Ground, 1), (LegacyItemEffect.KeyId, 1));
      WriteItemListRecord(itemDataBody, 472, "KEY", unique: 0, position: 0, grade: 0, (LegacyItemEffect.KeyId, 1));
      var encodedItemData = new byte[LegacyItemDataTable.FileSizeInBytes];
      for (var offset = 0; offset < itemDataBody.Length; offset++)
          encodedItemData[offset] = (byte)(itemDataBody[offset] ^ 0x5A);
      var itemData = LegacyItemDataTable.Load(encodedItemData);

      var definitions = Enumerable.Range(0, 18)
          .Select(index => new LegacyMapItemDefinition((short)(100 + index), 100, (short)(index == 17 ? 471 : 470), 0))
          .ToArray();
      var map = new LegacyMapGrid(new byte[LegacyMapGrid.HeightMapSize], new byte[LegacyMapGrid.AttributeMapSize]);
      var states = LegacyMapItemCatalog.CreateInitialStates(definitions, itemData, map);
      var mob = new byte[LegacyAccountSnapshot.CharacterStride];
      new LegacyScore(50, 0, 100, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 40, 0, 0, 0)
          .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
      new LegacyItem(472, 0, 0, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));

      var hub = new WorldHub(mapGrid: map, itemData: itemData);
      hub.ConfigureMapItems(states);
      Assert(hub.TryGetMapItem(10_018, out var initial) && initial is { State: LegacyMapItemStateCodes.Locked, Delay: 0 }, "The static keyed gate did not start locked with zero delay.");
      Assert(hub.Enter(1, "MAP_TIMER", (_, _) => ValueTask.CompletedTask) && hub.SetCharacterState(1, 0, 0, 1, 0, 0, 117, 100, mob), "Map-item timer participant was not registered.");

      Assert(hub.TryUpdateMapItem(1, 10_018, LegacyMapItemStateCodes.Open, out var opened) == LegacyUpdateItemResult.Accepted && opened is { MapItem.State: LegacyMapItemStateCodes.Open, MapItem.Delay: 0 }, "The fixture gate did not open with the matching key.");
      var firstMinute = new DateTime(2026, 9, 18, 12, 0, 0);
      Assert(hub.TryProcessMapItemMinute(firstMinute, out var firstPlan) && firstPlan is null, "The first ProcessMinTimer slot should arm the gate delay without closing it.");
      Assert(hub.TryGetMapItem(10_018, out var armed) && armed is { State: LegacyMapItemStateCodes.Open, Delay: 1 }, "The first minute timer did not change Delay from zero to one.");
      Assert(!hub.TryProcessMapItemMinute(firstMinute, out _), "The same ProcessMinTimer minute was applied twice.");
      Assert(hub.TryProcessMapItemMinute(firstMinute.AddMinutes(1), out var closedPlan) && closedPlan is { ClosedItems.Count: 1 } && closedPlan.ClosedItems[0] is { ItemId: 18, State: LegacyMapItemStateCodes.Locked, Delay: 0 }, "The second ProcessMinTimer slot did not lock the gate and produce a CreateItem plan.");
      Assert(hub.TryGetMapItem(10_018, out var closed) && closed is { State: LegacyMapItemStateCodes.Locked, Delay: 0 }, "The gate state after the minute timer was not retained authoritatively.");
  }

  static void StartTimeSignalWire()
  {
      var codec = LegacyFrameCodec.CreateDefault();
      var frame = codec.Decode(new StartTimeConfirmation(300).ToFrame(codec, 41, 24));
      Assert(frame.IsChecksumValid && frame.Header.Type == StartTimeConfirmation.MessageType && frame.Header.Id == StartTimeConfirmation.SceneId && frame.Header.Size == StartTimeConfirmation.PacketSize, "_MSG_StartTime did not preserve the MSG_STANDARDPARM header and size.");
      Assert(BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span) == 300, "_MSG_StartTime did not carry the quest duration in Parm.");
  }

  static void CastleGateUpdate()
  {
      var itemDataBody = new byte[LegacyItemDataTable.BodySizeInBytes];
      WriteItemListRecord(itemDataBody, 471, "CASTLE GATE", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Ground, 2), (LegacyItemEffect.KeyId, 10));
      WriteItemListRecord(itemDataBody, 473, "CASTLE INNER GATE", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Ground, 2), (LegacyItemEffect.KeyId, 11));
      WriteItemListRecord(itemDataBody, 472, "CASTLE KEY", unique: 0, position: 0, grade: 0, (LegacyItemEffect.KeyId, 10));
      WriteItemListRecord(itemDataBody, 474, "INNER KEY", unique: 0, position: 0, grade: 0, (LegacyItemEffect.KeyId, 11));
      var encodedItemData = new byte[LegacyItemDataTable.FileSizeInBytes];
      for (var offset = 0; offset < itemDataBody.Length; offset++)
          encodedItemData[offset] = (byte)(itemDataBody[offset] ^ 0x5A);
      var itemData = LegacyItemDataTable.Load(encodedItemData);

      var init = new byte[24];
      WriteInitItem(init, 0, 2200, 1200, 471, 0);
      WriteInitItem(init, 8, 2202, 1200, 473, 0);
      WriteInitItem(init, 16, 0, 0, 0, 0);
      var map = new LegacyMapGrid(new byte[LegacyMapGrid.HeightMapSize], new byte[LegacyMapGrid.AttributeMapSize]);
      var states = LegacyMapItemCatalog.CreateInitialStates(LegacyMapItemCatalog.LoadDefinitions(init), itemData, map);
      var masks = new int[LegacyGroundMaskTable.MaskCount, LegacyGroundMaskTable.RotationCount, LegacyGroundMaskTable.Height, LegacyGroundMaskTable.Width];
      masks[2, 0, 2, 2] = 1;
      var castleQuest = new LegacyCastleQuestDefinition(
          MobInitial: 100,
          MobEnd: 99,
          Boss1: 200,
          Boss2: 201,
          Prizes: new LegacyItem[LegacyCastleQuestConfiguration.MaxCarry],
          CoinPrize: 0,
          ExpPrize: new int[6],
          PartyPrize: false,
          QuestTime: 4);
      var mob = new byte[LegacyAccountSnapshot.CharacterStride];
      new LegacyScore(50, 0, 100, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 40, 0, 0, 0)
          .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
      new LegacyItem(472, LegacyItemEffect.Quest, 0, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));

      var hub = new WorldHub(mapGrid: map, itemData: itemData);
      hub.ConfigureMapItems(states, new LegacyGroundMaskTable(masks));
      hub.ConfigureCastleQuests([castleQuest]);
      Assert(hub.Enter(1, "CASTLE_GATE", (_, _) => ValueTask.CompletedTask) && hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2200, 1200, mob), "Castle gate participant was not registered.");

      var result = hub.TryUpdateMapItem(1, 10_001, LegacyMapItemStateCodes.Open, out var outcome);
      Assert(result == LegacyUpdateItemResult.Accepted && outcome?.CastleQuestStart is { QuestLevel: 0, TimeRemaining: 3, SpawnedNpcs.Count: 0 } start &&
          outcome.MapItem.State == LegacyMapItemStateCodes.Open && start.PartyConnectionIds.SequenceEqual([1]), "The Castle entrance key did not start the selected quest and open the gate with the legacy timer semantics.");
      Assert(hub.TryGetCastleQuestState(out var active) && active is { QuestLevel: 0, TimeRemaining: 3, LeaderConnectionId: 1 }, "The active Castle quest state was not retained authoritatively.");
      Assert(LegacyItem.Read(outcome!.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes)).Index == 0, "The Castle entrance key was not consumed.");

      var wrongQuestKeyMob = outcome.MobSnapshot.ToArray();
      new LegacyItem(474, LegacyItemEffect.Quest, 1, 0, 0, 0, 0).Write(wrongQuestKeyMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
      Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2200, 1200, wrongQuestKeyMob), "Castle inner-gate wrong-key fixture could not refresh the participant state.");
      Assert(hub.TryUpdateMapItem(1, 10_002, LegacyMapItemStateCodes.Open, out _) == LegacyUpdateItemResult.MissingCastleQuestKey, "A Castle inner gate accepted a key from a different quest level.");

      var rightQuestKeyMob = wrongQuestKeyMob.ToArray();
      new LegacyItem(474, LegacyItemEffect.Quest, 0, 0, 0, 0, 0).Write(rightQuestKeyMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
      Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2200, 1200, rightQuestKeyMob), "Castle inner-gate right-key fixture could not refresh the participant state.");
      Assert(hub.TryUpdateMapItem(1, 10_002, LegacyMapItemStateCodes.Open, out var innerOutcome) == LegacyUpdateItemResult.Accepted && innerOutcome is { StateChanged: true, MapItem.State: LegacyMapItemStateCodes.Open }, "A matching Castle quest key did not open the inner gate.");

      var leftoverNpcId = hub.EnterNpc(new byte[LegacyAccountSnapshot.CharacterStride], 2200, 1200, requestedConnectionId: 20_501);
      Assert(leftoverNpcId == 20_501, "The Castle timer fixture could not register an NPC inside the quest arena.");
      var timerStart = new DateTime(2026, 9, 18, 12, 0, 0);
      Assert(hub.TryProcessCastleQuestSecond(timerStart, out var tick1) && tick1 is { PreviousTimeRemaining: 3, CurrentTimeRemaining: 2, Expired: false }, "Castle ProcessSecTimer did not decrement the quest at the first even-second slot.");
      Assert(!hub.TryProcessCastleQuestSecond(timerStart, out _), "Castle ProcessSecTimer applied the same two-second slot twice.");
      Assert(!hub.TryProcessCastleQuestSecond(timerStart.AddSeconds(1), out _), "Castle ProcessSecTimer ran on an odd-second slot.");
      Assert(hub.TryProcessCastleQuestSecond(timerStart.AddSeconds(2), out var tick2) && tick2 is { PreviousTimeRemaining: 2, CurrentTimeRemaining: 1, Expired: false }, "Castle ProcessSecTimer did not preserve the two-second cadence.");
      Assert(hub.TryProcessCastleQuestSecond(timerStart.AddSeconds(4), out var tick3) && tick3 is { PreviousTimeRemaining: 1, CurrentTimeRemaining: 0, Expired: false }, "Castle ProcessSecTimer did not reach the zero countdown state.");
      Assert(hub.TryProcessCastleQuestSecond(timerStart.AddSeconds(6), out var expiration) && expiration is { PreviousTimeRemaining: 0, CurrentTimeRemaining: -1, Expired: true } && expiration.RemovedNpcs.Any(npc => npc.ConnectionId == leftoverNpcId), "Castle ProcessSecTimer did not expire the quest and remove arena NPCs.");
      Assert(hub.TryGetCastleQuestState(out var expired) && expired is { QuestLevel: 0, TimeRemaining: -1 }, "The expired Castle quest did not expose the legacy -1 timer state.");
  }

  static void WriteInitItem(byte[] encoded, int offset, short positionX, short positionY, short itemIndex, short rotate)
  {
      BinaryPrimitives.WriteInt16LittleEndian(encoded.AsSpan(offset), unchecked((short)(positionX ^ unchecked((short)0xFFFF))));
      BinaryPrimitives.WriteInt16LittleEndian(encoded.AsSpan(offset + 2), unchecked((short)(positionY ^ unchecked((short)0xFFFF))));
      BinaryPrimitives.WriteInt16LittleEndian(encoded.AsSpan(offset + 4), unchecked((short)(itemIndex ^ unchecked((short)0xFFFF))));
      BinaryPrimitives.WriteInt16LittleEndian(encoded.AsSpan(offset + 6), unchecked((short)(rotate ^ unchecked((short)0xFFFF))));
  }

  static void AccountLoginFailureNoticeFlow()
{
    Assert(AccountLoginFailureNotice.For(AccountAuthenticationStatus.AccountNotFound) == "Conta nao encontrada.", "Missing-account notice differs from the client-facing legacy message.");
    Assert(AccountLoginFailureNotice.For(AccountAuthenticationStatus.WrongPassword) == "Senha incorreta.", "Wrong-password notice differs from the client-facing legacy message.");
    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(new MessagePanelConfirmation(AccountLoginFailureNotice.For(AccountAuthenticationStatus.AccountNotFound)).ToFrame(codec, 33, 16));
    Assert(frame.IsChecksumValid && frame.Header.Type == MessagePanelConfirmation.MessageType && frame.Header.Size == MessagePanelConfirmation.PacketSize, "Account-login failure notice was not encoded as MSG_MessagePanel.");
}

static void DonateShopPurchaseMessages()
{
    Assert(LegacyDonateShopMessages.Throttled == "Aguarde 3 segundo para uma nova Tentativa.", "Donate purchase throttling text differs from the legacy handler.");
    Assert(LegacyDonateShopMessages.ForRejectedPurchase(LegacyDonatePurchaseResult.InsufficientDonate) == "Saldo de Rubis Insuficiente", "Insufficient Donate text differs from the legacy handler.");
    Assert(LegacyDonateShopMessages.ForRejectedPurchase(LegacyDonatePurchaseResult.InventoryFull) == "Não há espaço disponível no Inventário", "Inventory-full text differs from the legacy handler.");
    Assert(LegacyDonateShopMessages.ForRejectedPurchase(LegacyDonatePurchaseResult.InvalidQuantity) is null, "A silently ignored invalid quantity unexpectedly gained a client message.");
    Assert(LegacyDonateShopMessages.PurchaseAccepted(12, "Gema Ancient", 120) == "Comprou [x12] Gema Ancient por [120] Rubis", "Accepted Donate purchase text differs from the legacy format.");

    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(new MessagePanelConfirmation(LegacyDonateShopMessages.PurchaseAccepted(2, "VOL", 20)).ToFrame(codec, 33, 16));
    Assert(frame.IsChecksumValid && frame.Header.Type == MessagePanelConfirmation.MessageType && frame.Header.Size == MessagePanelConfirmation.PacketSize, "Donate purchase notice did not use MSG_MessagePanel.");
}

static void DonateShopWire()
{
    var codec = LegacyFrameCodec.CreateDefault();

    var balanceFrame = codec.Decode(new DonateBalanceConfirmation(1234, "AB123").ToFrame(codec, 33, 16));
    Assert(DonateBalanceConfirmation.TryParse(balanceFrame, out var balance) && balance is not null && balance.Cash == 1234 && balance.Pix == "AB123", "Donate balance wire did not round-trip.");

    var entries = new List<DonateStoreEntry>();
    for (var store = 0; store < DonateStoreCatalogConfirmation.StoreCount; store++)
    for (var page = 0; page < DonateStoreCatalogConfirmation.PageCount; page++)
    for (var position = 0; position < DonateStoreCatalogConfirmation.ItemCountPerPage; position++)
        entries.Add(new DonateStoreEntry(store, page, position, 0, 0, 0));
    entries[^1] = new DonateStoreEntry(2, 4, 14, 184, 1, 120);

    var catalogFrame = codec.Decode(new DonateStoreCatalogConfirmation(entries).ToFrame(codec, 34, 16));
    Assert(DonateStoreCatalogConfirmation.TryParse(catalogFrame, out var catalog) && catalog is not null && catalog.Entries.Count == 225, "Donate catalog wire did not round-trip its 3x5x15 matrix.");
    var last = catalog!.Entries[^1];
    Assert(last.Store == 2 && last.Page == 4 && last.ItemPosition == 14 && last.ItemIndex == 184 && last.Price == 1 && last.Stock == 120, "Donate catalog last slot differs from the legacy item/price/stock triplet.");

    var purchaseFrame = codec.Decode(new DonatePurchaseRequest(2, 4, 14, 120).ToFrame(codec, 35, 16));
    Assert(DonatePurchaseRequest.TryParse(purchaseFrame, out var purchase) && purchase is not null && purchase.Store == 2 && purchase.Page == 4 && purchase.ItemPosition == 14 && purchase.Quantity == 120, "Donate purchase request wire did not round-trip.");

    var openFrame = codec.Decode(new DonateShopOpenRequest(1000, 100, 0, 0).ToFrame(codec, 35, 16));
    Assert(DonateShopOpenRequest.TryParse(openFrame, out var open) && open is not null && open.Target == 1000 && open.Warp == 100 && open.Face == 0 && open.Effect == 0, "Donate Shop entry request wire did not round-trip the NPC bridge fields.");

    var retailNpcFrame = codec.Decode(new RetailNpcShopRequest(12345, 0x4503).ToFrame(codec, 35, 16));
    Assert(RetailNpcShopRequest.TryParse(retailNpcFrame, out var retailNpc) && retailNpc is not null && retailNpc.Target == 12345 && retailNpc.Unk == 0x4503, "Retail 7.60 compact NPC shop request did not round-trip its Target/Unk ushort fields.");
    Assert(!DonateShopOpenRequest.TryParse(retailNpcFrame, out _), "The compact retail NPC frame was incorrectly accepted as the eight-byte Donate bridge request.");

    var catalogRequestFrame = codec.Decode(new DonateShopCatalogRequest(2).ToFrame(codec, 36, 16));
    Assert(DonateShopCatalogRequest.TryParse(catalogRequestFrame, out var catalogRequest) && catalogRequest is not null && catalogRequest.Kind == 2, "Donate Shop catalog request wire did not round-trip the alias kind.");
    var balanceRequestFrame = codec.Decode(new DonateShopCatalogRequest(1).ToFrame(codec, 37, 16));
    Assert(DonateShopCatalogRequest.TryParse(balanceRequestFrame, out var balanceRequest) && balanceRequest is not null && balanceRequest.Kind == 1, "Donate balance refresh request did not preserve the alias kind.");

    var shopItems = Enumerable.Range(0, DonateShopOpenConfirmation.ItemCount)
        .Select(index => new LegacyItem((short)(100 + index), 1, 2, 3, 4, 5, 6))
        .ToArray();
    var shopFrame = codec.Decode(new DonateShopOpenConfirmation(2, shopItems, 7).ToFrame(codec, 36, 16));
    Assert(DonateShopOpenConfirmation.TryParse(shopFrame, out var shop) && shop is not null && shop.ShopType == 2 && shop.Tax == 7 && shop.Items.Count == 15, "Donate shop-open wire did not round-trip.");
    Assert(shop!.Items[14].Index == 114 && shop.Items[14].Effect3 == 5 && shop.Items[14].Value3 == 6, "Donate shop-open item payload differs from the legacy 15-item page.");
}

static void DonateShopClientStateFlow()
{
    var entries = new List<DonateStoreEntry>();
    for (var store = 0; store < DonateStoreCatalogConfirmation.StoreCount; store++)
    for (var page = 0; page < DonateStoreCatalogConfirmation.PageCount; page++)
    for (var position = 0; position < DonateStoreCatalogConfirmation.ItemCountPerPage; position++)
        entries.Add(new DonateStoreEntry(store, page, position, 1000 + position, 10, 120));

    var state = new DonateShopClientState();
    state.Open();
    state.ApplyBalance(new DonateBalanceConfirmation(25, string.Empty));
    state.ApplyCatalog(new DonateStoreCatalogConfirmation(entries));
    Assert(state.IsOpen && state.Store == 0 && state.Page == 0 && state.Quantity == 0, "C# Donate client state did not reproduce OPENSTORE defaults.");
    Assert(state.SelectStorePage(2, 4) && state.SelectItem(3), "C# Donate client state did not select the requested store/page/slot.");
    Assert(state.Quantity == 1 && state.SelectedEntry is { Store: 2, Page: 4, ItemPosition: 3, ItemIndex: 1003 }, "C# Donate client selection differs from the selected catalog entry.");
    Assert(state.AdjustQuantity(10) && state.Quantity == 2, "C# Donate client quantity did not respect the 120 cap and current Donate balance.");
    Assert(state.TryBuildPurchase(out var request) && request is not null && request.Store == 2 && request.Page == 4 && request.ItemPosition == 3 && request.Quantity == 2, "C# Donate client did not build the authoritative purchase request.");
    Assert(state.AdjustQuantity(-1) && state.Quantity == 1, "C# Donate client quantity decrement did not work.");
    state.Close();
    Assert(!state.IsOpen && state.SelectedEntry is null && !state.TryBuildPurchase(out _), "C# Donate client close did not clear the selection.");

    var noBalance = new DonateShopClientState();
    noBalance.Open();
    noBalance.ApplyBalance(new DonateBalanceConfirmation(5, string.Empty));
    noBalance.ApplyCatalog(new DonateStoreCatalogConfirmation(entries));
    Assert(noBalance.SelectItem(0) && noBalance.Quantity == 0 && !noBalance.TryBuildPurchase(out _), "C# Donate client allowed an unaffordable item to become a purchase request.");
    noBalance.ApplyBalance(new DonateBalanceConfirmation(10, string.Empty));
    Assert(noBalance.Quantity == 1 && noBalance.TryBuildPurchase(out _), "C# Donate client did not re-enable the selected item after the balance became sufficient.");
}

static void DonateShopCatalogAndPurchase()
{
    var lines = new List<string> { "# store,page,itemPosition,itemIndex,price,stock" };
    for (var store = 0; store < LegacyDonateShopCatalog.StoreCount; store++)
    for (var page = 0; page < LegacyDonateShopCatalog.PageCount; page++)
    for (var position = 0; position < LegacyDonateShopCatalog.ItemCountPerPage; position++)
    {
        var itemIndex = store == 0 && page == 0 && position == 0 ? 3314 : 184;
        var price = store == 0 && page == 0 && position == 0 ? 10 : 1;
        lines.Add($"{store},{page},{position},{itemIndex},{price},120");
    }

    var catalog = LegacyDonateShopCatalog.Parse(string.Join(Environment.NewLine, lines));
    Assert(catalog.Entries.Count == LegacyDonateShopCatalog.SlotCount && catalog.TryGet(0, 0, 0, out var groupedEntry) && groupedEntry is { ItemIndex: 3314, Price: 10, Stock: 120 }, "Donate catalog did not preserve the explicit 3x5x15 coordinates.");

    var mob = new byte[LegacyAccountSnapshot.CharacterStride];
    var hub = new WorldHub(donateShopCatalog: catalog);
    Assert(hub.Enter(1, "DONATE_USER", (_, _) => ValueTask.CompletedTask) && hub.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, mob) && hub.SetDonateBalance(1, 100), "Donate purchase participant was not initialized.");

    var groupedResult = hub.TryPurchaseDonateItem(1, new DonatePurchaseRequest(0, 0, 0, 5), out var groupedOutcome);
    Assert(groupedResult == LegacyDonatePurchaseResult.Accepted && groupedOutcome is not null && groupedOutcome.TotalPrice == 50 && groupedOutcome.RemainingDonate == 50 && groupedOutcome.ItemDrops.Count == 1, "Grouped Donate purchase was not applied atomically.");
    Assert(hub.TryGetCharacterSnapshot(1, out var groupedMob, out _, out _) && groupedMob is not null, "Grouped Donate purchase did not expose the updated MOB.");
    var groupedItem = LegacyItem.Read(groupedMob!.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes));
    Assert(groupedItem.Index == 3314 && groupedItem.Effect1 == 61 && groupedItem.Value1 == 5, "Grouped Donate item did not preserve EF_AMOUNT and quantity.");

    var individualResult = hub.TryPurchaseDonateItem(1, new DonatePurchaseRequest(0, 0, 1, 3), out var individualOutcome);
    Assert(individualResult == LegacyDonatePurchaseResult.Accepted && individualOutcome is not null && individualOutcome.TotalPrice == 3 && individualOutcome.RemainingDonate == 47 && individualOutcome.ItemDrops.Count == 3, "Non-grouped Donate purchase did not create one inventory slot per unit.");
    Assert(hub.TryRollbackDonatePurchase(1, individualOutcome!), "Donate purchase rollback was not accepted for the connected participant.");
    Assert(hub.TryGetDonateBalance(1, out var restoredDonate) && restoredDonate == 50, "Donate purchase rollback did not restore the previous balance.");
    Assert(hub.TryGetCharacterSnapshot(1, out var restoredMob, out _, out _) && restoredMob is not null && LegacyItem.Read(restoredMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes)).Index == 3314 && Enumerable.Range(1, 3).All(slot => LegacyItem.Read(restoredMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (slot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes)).Index == 0), "Donate purchase rollback did not restore the previous inventory.");
    Assert(hub.TryPurchaseDonateItem(1, new DonatePurchaseRequest(0, 0, 1, 51), out _) == LegacyDonatePurchaseResult.InsufficientDonate, "Donate purchase accepted a balance larger than the account balance.");
    Assert(hub.TryPurchaseDonateItem(1, new DonatePurchaseRequest(0, 0, 1, 0), out _) == LegacyDonatePurchaseResult.InvalidQuantity, "Donate purchase accepted a zero quantity.");
}

static void ServerModePolicyFlow()
{
    var lines = new List<string>();
    for (var store = 0; store < LegacyDonateShopCatalog.StoreCount; store++)
    for (var page = 0; page < LegacyDonateShopCatalog.PageCount; page++)
    for (var position = 0; position < LegacyDonateShopCatalog.ItemCountPerPage; position++)
        lines.Add($"{store},{page},{position},184,25,120");

    var catalog = LegacyDonateShopCatalog.Parse(string.Join(Environment.NewLine, lines));
    var upPolicy = new LegacyServerModePolicy(LegacyServerMode.Up);
    var pvpPolicy = new LegacyServerModePolicy(LegacyServerMode.Pvp);
    Assert(upPolicy.WorldKey == "UP" && pvpPolicy.WorldKey == "PVP", "World keys did not use the explicit UP/PVP strings.");
    Assert(LegacyServerModePolicy.TryParseWorldKey("up", out var parsedUp) && parsedUp == LegacyServerMode.Up, "UP world key was not parsed.");
    Assert(LegacyServerModePolicy.TryParseWorldKey("PVP", out var parsedPvp) && parsedPvp == LegacyServerMode.Pvp, "PVP world key was not parsed.");
    Assert(!LegacyServerModePolicy.TryParseWorldKey("0", out _), "Numeric world IDs were accepted instead of explicit world keys.");
    Assert(upPolicy.GetDonatePrice(25) == 25 && upPolicy.GetGoldPrice(25) == 25, "UP mode changed configured prices.");
    Assert(pvpPolicy.GetDonatePrice(25) == 0 && pvpPolicy.GetGoldPrice(25) == 0, "PVP mode did not make both supported price channels free.");

    var up = new WorldHub(donateShopCatalog: catalog, serverMode: LegacyServerMode.Up);
    var pvp = new WorldHub(donateShopCatalog: catalog, serverMode: LegacyServerMode.Pvp);
    Assert(up.ServerMode == LegacyServerMode.Up && !up.IsPvp, "UP mode was not retained by the world instance.");
    Assert(pvp.ServerMode == LegacyServerMode.Pvp && pvp.IsPvp, "PVP mode was not retained by the world instance.");
    Assert(up.WorldKey == "UP" && pvp.WorldKey == "PVP", "WorldHub did not expose the explicit world key.");
    Assert(up.TryGetDonateShopCatalog(out var upCatalog) && upCatalog!.TryGet(0, 0, 0, out var upEntry) && upEntry.Price == 25, "UP catalog price was not preserved.");
    Assert(pvp.TryGetDonateShopCatalog(out var pvpCatalog) && pvpCatalog!.TryGet(0, 0, 0, out var pvpEntry) && pvpEntry.Price == 0, "PVP catalog was not exposed with a zero Donate price.");

    var mob = new byte[LegacyAccountSnapshot.CharacterStride];
    Assert(pvp.Enter(1, "PVP_USER", (_, _) => ValueTask.CompletedTask) && pvp.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, mob), "PVP purchase participant was not initialized.");
    var result = pvp.TryPurchaseDonateItem(1, new DonatePurchaseRequest(0, 0, 0, 10), out var outcome);
    Assert(result == LegacyDonatePurchaseResult.Accepted && outcome is not null && outcome.TotalPrice == 0 && outcome.RemainingDonate == 0, "PVP Donate purchase still charged the configured price.");
}

static void DonateShopRateLimiter()
{
    var limiter = new LegacyDonateShopRateLimiter();
    Assert(limiter.TryAccept(LegacyDonateShopRequestKind.Open, 0), "The first Donate Shop open request was throttled.");
    Assert(!limiter.TryAccept(LegacyDonateShopRequestKind.Open, 199) && limiter.TryAccept(LegacyDonateShopRequestKind.Open, 200), "Donate Shop open did not preserve the 200 ms gate.");
    Assert(limiter.TryAccept(LegacyDonateShopRequestKind.Balance, 0) && !limiter.TryAccept(LegacyDonateShopRequestKind.Balance, 1999) && limiter.TryAccept(LegacyDonateShopRequestKind.Balance, 2000), "Donate balance did not preserve the 2 second gate.");
    Assert(limiter.TryAccept(LegacyDonateShopRequestKind.Catalog, 0) && !limiter.TryAccept(LegacyDonateShopRequestKind.Catalog, 14999) && limiter.TryAccept(LegacyDonateShopRequestKind.Catalog, 15000), "Donate catalog did not preserve the 15 second gate.");
    Assert(limiter.TryAccept(LegacyDonateShopRequestKind.Purchase, 0) && !limiter.TryAccept(LegacyDonateShopRequestKind.Purchase, 2999) && limiter.TryAccept(LegacyDonateShopRequestKind.Purchase, 3000), "Donate purchase did not preserve the 3 second gate.");
    Assert(limiter.TryAccept(LegacyDonateShopRequestKind.Balance, 4000), "Donate balance and catalog gates incorrectly shared state with another request kind.");
}

static void CityPerzenNpcs()
{
    static byte[] Mob(string name, byte merchant)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        System.Text.Encoding.ASCII.GetBytes(name).CopyTo(mob, LegacyAccountSnapshot.MobNameOffset);
        mob[LegacyAccountSnapshot.MobMerchantOffset] = merchant;
        new LegacyScore(50, 0, 0, 0, 0, 0, 0, 9000, 9000, 9000, 9000, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    var templates = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["Perzen_Normal"] = Mob("Perzen_Normal", 1),
        ["Perzen_Arcano"] = Mob("Perzen_Arcano", 1),
        ["Perzen"] = Mob("Perzen", 1),
        ["Perzen_Mistico"] = Mob("Perzen_Mistico", 1),
    };
    var catalog = LegacyNpcGenerationCatalog.Parse("""
        # [3427]
        MinuteGenerate: 1
        MaxNumMob: 1
        Leader: Perzen_Normal
        Follower: Perzen_Normal
        StartX: 2467
        StartY: 2010
        # [3428]
        MinuteGenerate: 1
        MaxNumMob: 1
        Leader: Perzen_Arcano
        Follower: Perzen_Arcano
        StartX: 2480
        StartY: 1705
        # [3429]
        MinuteGenerate: -1
        MaxNumMob: 1
        Leader: Perzen
        Follower: Perzen
        StartX: 1052
        StartY: 1721
        # [3430]
        MinuteGenerate: 1
        MaxNumMob: 1
        Leader: Perzen_Mistico
        Follower: Perzen_Mistico
        StartX: 2133
        StartY: 2080
        # [4866]
        MinuteGenerate: -1
        MaxNumMob: 1
        Leader: Perzen_Normal
        Follower: Perzen_Normal
        StartX: 2120
        StartY: 2047
        """, templates);
    var hub = new WorldHub(npcGenerationCatalog: catalog);
    var expected = new Dictionary<int, (short X, short Y)>
    {
        [3427] = (2467, 2010), [3428] = (2480, 1705), [3429] = (1052, 1721),
        [3430] = (2133, 2080), [4866] = (2120, 2047),
    };

    foreach (var (generateIndex, position) in expected)
    {
        Assert(hub.TrySpawnGeneratedNpc(generateIndex, out var npc) && npc is not null, $"Reference Perzen generator {generateIndex} did not spawn.");
        Assert(npc!.PositionX == position.X && npc.PositionY == position.Y && npc.GenerateIndex == generateIndex, $"Reference Perzen generator {generateIndex} changed its city position or identity.");
    }
    Assert(hub.GetNpcSnapshots().Count == 5, "The reference city Perzen set did not contain exactly five NPCs.");
}

static void PerzenExchange()
{
    static byte[] PlayerMob(int itemIndex)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        System.Text.Encoding.ASCII.GetBytes("PERZEN_PLAYER").CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset));
        new LegacyItem((short)itemIndex, 61, 4, 12, 99, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (4 * LegacyItem.SizeInBytes)));
        return mob;
    }

    static byte[] PerzenMob(int grade, int requiredIndex, int rewardIndex)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        System.Text.Encoding.ASCII.GetBytes("Perzen Normal").CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset));
        mob[LegacyAccountSnapshot.MobMerchantOffset] = 100;
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset), grade);
        new LegacyItem((short)requiredIndex, 0, 0, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
        new LegacyItem((short)rewardIndex, 0, 0, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + LegacyItem.SizeInBytes));
        return mob;
    }

    var hub = new WorldHub();
    var playerMob = PlayerMob(4110);
    Assert(hub.Enter(1, "PERZEN_ACCOUNT", (_, _) => ValueTask.CompletedTask) && hub.Enter(2, "PERZEN_NEARBY", (_, _) => ValueTask.CompletedTask) && hub.Enter(3, "PERZEN_FAR", (_, _) => ValueTask.CompletedTask), "Perzen exchange participants were not registered.");
    Assert(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 1050, 1721, playerMob) && hub.SetCharacterState(2, 0, 0, 1, 0, 0, 1068, 1721, new byte[LegacyAccountSnapshot.CharacterStride]) && hub.SetCharacterState(3, 0, 0, 1, 0, 0, 1069, 1721, new byte[LegacyAccountSnapshot.CharacterStride]), "Perzen exchange participant state was not initialized.");
    var perzenId = hub.EnterNpc(PerzenMob(7, 4110, 3989), 1052, 1721, requestedConnectionId: 20_601);
    Assert(hub.GetParticipantIdsInNpcView(perzenId).SequenceEqual([1, 2]), "NPC chat view did not preserve the legacy inclusive 33x33 area.");

    var now = new DateTime(2026, 9, 17, 12, 0, 0);
    var result = hub.TryExchangePerzenItem(1, perzenId, out var outcome, now);
    var expected = LegacyItemDateMath.SetItemDate(new LegacyItem(3989, 0, 0, 0, 0, 0, 0), 30, now);
    Assert(result == LegacyPerzenExchangeResult.Accepted && outcome is { InventorySlot: 4, RequiredItemIndex: 4110, RewardItemIndex: 3989, UpdatedItem: var updated } && updated == expected, "Perzen did not exchange the required item in-place with a 30-day dated reward.");
    Assert(hub.TryGetCharacterSnapshot(1, out var updatedMob, out _, out _, out _) && updatedMob is not null && LegacyItem.Read(updatedMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (4 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes)) == expected, "Perzen exchange did not persist the replacement in the authoritative MOB.");
    Assert(expected.Effect1 == LegacyItemEffect.WDay && expected.Value1 == 18 && expected.Effect2 == LegacyItemEffect.WMonth && expected.Value2 == 10 && expected.Effect3 == LegacyItemEffect.Year && expected.Value3 == 26, "Perzen date encoding differs from BASE_SetItemDate for the September boundary.");

    var missingHub = new WorldHub();
    Assert(missingHub.Enter(1, "PERZEN_MISSING", (_, _) => ValueTask.CompletedTask) && missingHub.SetCharacterState(1, 0, 0, 1, 0, 0, 1050, 1721, new byte[LegacyAccountSnapshot.CharacterStride]), "Missing-item Perzen player was not initialized.");
    var missingNpc = missingHub.EnterNpc(PerzenMob(8, 4128, 3986), 2467, 2010, requestedConnectionId: 20_602);
    Assert(missingHub.TryExchangePerzenItem(1, missingNpc, out var missingOutcome, now) == LegacyPerzenExchangeResult.MissingItem && missingOutcome is { InventorySlot: -1, RequiredItemIndex: 4128, RewardItemIndex: 3986 }, "Perzen accepted an exchange without the required sphere.");

    var ordinaryNpc = missingHub.EnterNpc(PerzenMob(6, 4110, 3989), 2467, 2010, requestedConnectionId: 20_603);
    Assert(missingHub.TryExchangePerzenItem(1, ordinaryNpc, out _) == LegacyPerzenExchangeResult.NotPerzen, "Perzen dispatch accepted a merchant outside the legacy grade range.");
}

static void DonateShopNpcTarget()
{
    static byte[] Mob(string name, byte merchant)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        System.Text.Encoding.ASCII.GetBytes(name).CopyTo(mob, LegacyAccountSnapshot.MobNameOffset);
        mob[LegacyAccountSnapshot.MobMerchantOffset] = merchant;
        return mob;
    }

    Assert(WorldHub.IsDonateShopNpcName("Donation Store"), "The normalized Donation Store template name was not recognized.");
    Assert(WorldHub.IsDonateShopNpcName("Donation_Store5"), "The raw Donation_Store5 template name was not recognized.");
    Assert(!WorldHub.IsDonateShopNpcName("Armas F"), "An ordinary category NPC was incorrectly classified as Donation Store.");

    var hub = new WorldHub();
    var donationNpc = hub.EnterNpc(Mob("Donation Store", merchant: 1), 4000, 4000, requestedConnectionId: 20_100);
    var ordinaryNpc = hub.EnterNpc(Mob("Armas F", merchant: 1), 4026, 4000, requestedConnectionId: 20_101);
    Assert(hub.IsDonateShopNpc(donationNpc), "A live Donation Store NPC target was not accepted.");
    Assert(!hub.IsDonateShopNpc(ordinaryNpc), "An ordinary merchant NPC target was accepted as Donate Store.");
    Assert(!hub.IsDonateShopNpc(20_102), "An unknown NPC target was accepted as Donate Store.");
}

static void HeaderRoundTrip()
{
    var expected = new PacketHeader(24, 19, 7, 0x1234, 42, 0x12345678);
    Span<byte> bytes = stackalloc byte[PacketHeader.SizeInBytes];
    expected.Write(bytes);
    Assert(PacketHeader.Read(bytes) == expected, "Header differs after wire round-trip.");
}

static void FrameRoundTrip()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var encoded = codec.Encode(0x0012, 33, 1000, [0xAA, 0xBB, 0xCC, 0xDD, 0xEE], 7);
    var decoded = codec.Decode(encoded);
    Assert(decoded.IsChecksumValid, "Expected a valid checksum.");
    Assert(decoded.Header.Type == 0x0012 && decoded.Header.Id == 33 && decoded.Header.ClientTick == 1000, "Header mismatch.");
    Assert(decoded.Payload.Span.SequenceEqual(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE }), "Payload differs after round-trip.");
}

static void StreamFraming()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var first = codec.Encode(1, 2, 3, [4, 5], 11);
    var second = codec.Encode(6, 7, 8, [9], 22);
    var stream = new LegacyFrameStream(codec);
    stream.Append(BitConverter.GetBytes(LegacyFrameCodec.InitCode));
    stream.Append(first.AsSpan(0, 5));
    Assert(!stream.TryRead(out _), "Partial frame was accepted.");
    stream.Append(first.AsSpan(5));
    stream.Append(second);
    Assert(stream.TryRead(out var decodedFirst) && decodedFirst.Header.Type == 1, "First frame was not parsed.");
    Assert(stream.TryRead(out var decodedSecond) && decodedSecond.Header.Type == 6, "Second concatenated frame was not parsed.");
}

static void ChecksumDetectsIncompatibleKeyTable()
{
    var encoded = LegacyFrameCodec.CreateDefault().Encode(4, 0, 1, [1, 2, 3, 4], 1);
    var incompatibleCodec = new LegacyFrameCodec(new byte[512]);
    Assert(!incompatibleCodec.Decode(encoded).IsChecksumValid, "Incompatible key table was accepted.");
}

static void AccountLoginParser()
{
    var payload = new byte[104];
    System.Text.Encoding.ASCII.GetBytes("secret").CopyTo(payload, 0);
    System.Text.Encoding.ASCII.GetBytes("sandbox").CopyTo(payload, 12);
    Enumerable.Range(1, 52).Select(static value => (byte)value).ToArray().CopyTo(payload, 28);
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(80), 7640);
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(84), 1);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(88), 0xAABBCCDD);
    var frame = LegacyFrameCodec.CreateDefault().Decode(LegacyFrameCodec.CreateDefault().Encode(AccountLoginRequest.MessageType, 4, 33, payload, 16));
    Assert(AccountLoginRequest.TryParse(frame, out var login), "Valid login packet was not parsed.");
    Assert(login!.AccountName == "SANDBOX" && login.AccountPassword == "secret" && login.ReconnectToken.SequenceEqual(Enumerable.Range(1, 52).Select(static value => (byte)value)) && login.ClientVersion == 7640 && login.AdapterName[0] == 0xAABBCCDD, "Parsed login fields differ.");

    var otherClientVersionPayload = payload.ToArray();
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(otherClientVersionPayload.AsSpan(80), 1758);
    var otherClientVersionFrame = LegacyFrameCodec.CreateDefault().Decode(LegacyFrameCodec.CreateDefault().Encode(AccountLoginRequest.MessageType, 4, 33, otherClientVersionPayload, 16));
    Assert(AccountLoginRequest.TryParse(otherClientVersionFrame, out var otherVersionLogin) && otherVersionLogin!.ClientVersion == 1758, "A different client version using the same supported login wire was rejected.");
    Assert(ClientReleasePolicy.IsSupported(7670), "The required 7.670 client version was rejected by admission policy.");
    Assert(!ClientReleasePolicy.IsSupported(login.ClientVersion) && !ClientReleasePolicy.IsSupported(1758), "Admission policy accepted a non-7.670 client version.");

    Array.Clear(payload, 0, 12); // The legacy server defers an empty password to account storage.
    var emptyPasswordFrame = LegacyFrameCodec.CreateDefault().Decode(LegacyFrameCodec.CreateDefault().Encode(AccountLoginRequest.MessageType, 4, 33, payload, 16));
    Assert(AccountLoginRequest.TryParse(emptyPasswordFrame, out var emptyPasswordLogin) && emptyPasswordLogin!.AccountPassword.Length == 0, "Legacy empty-password packet was rejected.");
}

static void CreateCharacterParser()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var payload = new byte[24];
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(payload, 2);
    System.Text.Encoding.ASCII.GetBytes("HERO").CopyTo(payload, 4);
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(20), 3);
    var frame = codec.Decode(codec.Encode(CreateCharacterRequest.MessageType, 4, 33, payload, 16));

    Assert(CreateCharacterRequest.TryParse(frame, out var request), "Valid create-character packet was not parsed.");
    Assert(request!.Slot == 2 && request.CharacterName == "HERO" && request.CharacterClass == 3, "Parsed create-character fields differ.");

    Array.Clear(payload, 4, 16);
    var emptyNameFrame = codec.Decode(codec.Encode(CreateCharacterRequest.MessageType, 4, 33, payload, 16));
    Assert(!CreateCharacterRequest.TryParse(emptyNameFrame, out _), "Empty character name was accepted.");
}

static void CharacterLogoutWire()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var request = codec.Decode(codec.Encode(CharacterLogoutRequest.MessageType, 42, 33, ReadOnlySpan<byte>.Empty, 16));
    Assert(CharacterLogoutRequest.IsValid(request), "Valid character-logout request was not parsed.");

    var confirmation = codec.Decode(CharacterLogoutConfirmation.ToFrame(codec, 42, 33, 16));
    Assert(confirmation.IsChecksumValid && confirmation.Header.Type == CharacterLogoutConfirmation.MessageType && confirmation.Header.Id == 42 && confirmation.Payload.IsEmpty, "Character-logout confirmation wire differs from SendClientSignal(conn, conn, ...).");
}

static void DeleteCharacterWire()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var payload = new byte[32];
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(payload, 2);
    System.Text.Encoding.ASCII.GetBytes("HERO").CopyTo(payload, 4);
    System.Text.Encoding.ASCII.GetBytes("secret").CopyTo(payload, 20);
    var requestFrame = codec.Decode(codec.Encode(DeleteCharacterRequest.MessageType, 42, 33, payload, 16));
    Assert(DeleteCharacterRequest.TryParse(requestFrame, out var request) && request!.Slot == 2 && request.CharacterName == "HERO" && request.AccountPassword == "secret", "Delete-character request was not parsed.");

    var selection = LegacyCharacterSelection.CreateEmpty();
    var confirmation = codec.Decode(new DeleteCharacterConfirmation(selection).ToFrame(codec, 33, 16));
    Assert(confirmation.IsChecksumValid && confirmation.Header.Type == DeleteCharacterConfirmation.MessageType && confirmation.Header.Id == DeleteCharacterConfirmation.SceneId && confirmation.Payload.Length == LegacyCharacterSelection.SizeInBytes, "Delete-character confirmation wire differs from the 7.670 target client message.");
    var fail = codec.Decode(DeleteCharacterFailSignal.ToFrame(codec, 33, 16));
    Assert(fail.IsChecksumValid && fail.Header.Type == DeleteCharacterFailSignal.MessageType && fail.Payload.IsEmpty, "Delete-character failure signal differs from the legacy message.");
}

static void LoginSessionTransitions()
{
    var sessions = new LoginSessionRegistry();
    var request = new AccountLoginRequest("SANDBOX", "secret", new byte[52], 7640, 0, [1, 2, 3, 4]);

    Assert(!sessions.Open(0), "Legacy reserved connection zero was accepted.");
    Assert(sessions.Open(42), "Connection was not accepted.");
    Assert(sessions.BeginAccountLogin(42, request) == LoginTransitionResult.Accepted, "Accepted connection did not enter login pending state.");
    Assert(sessions.BeginAccountLogin(42, request) == LoginTransitionResult.InvalidState, "Duplicate login changed pending state.");
    Assert(sessions.CompleteAccountLogin(42, "OTHER") == LoginTransitionResult.AccountMismatch, "Mismatched account confirmation was accepted.");
    Assert(sessions.CompleteAccountLogin(42, "SANDBOX") == LoginTransitionResult.Accepted, "Matching account confirmation did not enter character selection.");
    Assert(sessions.TryGet(42, out var session) && session!.State == LoginSessionState.CharacterSelection && session.AdapterName!.SequenceEqual(new uint[] { 1, 2, 3, 4 }), "Final login session differs from legacy transition.");
    Assert(sessions.BeginCharacterWait(42) == LoginTransitionResult.Accepted, "Character DB operation did not enter USER_CHARWAIT.");
    Assert(sessions.TryGet(42, out session) && session!.State == LoginSessionState.CharacterWait, "Character DB operation state was not USER_CHARWAIT.");
    Assert(sessions.CompleteCharacterRefresh(42) == LoginTransitionResult.Accepted, "Character refresh did not return to USER_SELCHAR.");
    Assert(sessions.TryGet(42, out session) && session!.State == LoginSessionState.CharacterSelection, "Character refresh state was not USER_SELCHAR.");
    Assert(sessions.CompleteCharacterLogin(42, characterSlot: 0) == LoginTransitionResult.InvalidState, "Character login completed without entering USER_CHARWAIT.");
    Assert(sessions.BeginCharacterWait(42) == LoginTransitionResult.Accepted, "Character login did not enter USER_CHARWAIT.");
    Assert(sessions.CompleteCharacterLogin(42, characterSlot: 0, positionX: 2000, positionY: 2000) == LoginTransitionResult.Accepted, "Character login did not enter USER_PLAY state.");
    Assert(sessions.TryGet(42, out session) && session!.State == LoginSessionState.Playing, "Character login state was not USER_PLAY.");
    var movement = new ActionRequest(2000, 2000, 0, 6, new byte[24], 2020, 2010);
    Assert(sessions.TryApplyMovement(42, movement) == MovementResult.Accepted, "A valid movement was rejected.");
    Assert(sessions.TryGet(42, out session) && session!.PositionX == 2020 && session.PositionY == 2010, "Accepted movement did not update the world position.");
    Assert(sessions.TryApplyMovement(42, movement with { PositionX = 1999, TargetX = 2030, TargetY = 2010 }) == MovementResult.Accepted, "A movement with a stale client-reported origin (not validated by the legacy TMSrv) was rejected.");
    Assert(sessions.TryGet(42, out session) && session!.PositionX == 2030 && session.PositionY == 2010, "Accepted movement with a stale origin did not update the world position from the target.");
    var viewGridEdge = movement with { PositionX = 2030, PositionY = 2010, TargetX = 2063, TargetY = 2010 };
    Assert(sessions.TryApplyMovement(42, viewGridEdge) == MovementResult.Accepted, "A movement exactly at VIEWGRIDX was rejected.");
    Assert(sessions.TryGet(42, out session) && session!.PositionX == 2063 && session.PositionY == 2010, "A movement at the VIEWGRIDX edge did not update the authoritative position.");
    Assert(sessions.TryApplyMovement(42, viewGridEdge with { PositionX = 2063, TargetX = 2097 }) == MovementResult.StepTooLarge, "A movement one cell beyond VIEWGRIDX was accepted.");
    Assert(sessions.TryApplyMovement(42, viewGridEdge with { PositionX = 2063, TargetX = 4096 }) == MovementResult.OutOfBounds, "Out-of-bounds movement was accepted.");
    Assert(sessions.CompleteCharacterLogout(42) == LoginTransitionResult.Accepted, "Character logout did not return to USER_SELCHAR.");
    Assert(sessions.TryGet(42, out session) && session!.State == LoginSessionState.CharacterSelection, "Character logout state was not USER_SELCHAR.");
}

static void LegacyAccountStoreIsReadOnly()
{
    var root = Path.Combine(Path.GetTempPath(), $"wyd-cdk-account-test-{Guid.NewGuid():N}");
    var accountPath = Path.Combine(root, "S", "SANDBOX");
    try
    {
        Directory.CreateDirectory(Path.GetDirectoryName(accountPath)!);
        var header = new byte[28];
        System.Text.Encoding.ASCII.GetBytes("SANDBOX").CopyTo(header, 0);
        System.Text.Encoding.ASCII.GetBytes("secret").CopyTo(header, 16);
        File.WriteAllBytes(accountPath, header);
        var original = File.ReadAllBytes(accountPath);
        var store = new LegacyFileAccountStore(root);

        var success = store.AuthenticateAsync(new AccountLoginRequest("sandbox", "secret", new byte[52], 7640, 0, [0, 0, 0, 0])).GetAwaiter().GetResult();
        var incorrectPassword = store.AuthenticateAsync(new AccountLoginRequest("sandbox", "wrong", new byte[52], 7640, 0, [0, 0, 0, 0])).GetAwaiter().GetResult();
        var missing = store.AuthenticateAsync(new AccountLoginRequest("unknown", "secret", new byte[52], 7640, 0, [0, 0, 0, 0])).GetAwaiter().GetResult();

        Assert(success.Status == AccountAuthenticationStatus.Success && success.AccountName == "SANDBOX", "Legacy account was not authenticated.");
        Assert(incorrectPassword.Status == AccountAuthenticationStatus.WrongPassword, "Wrong password was accepted.");
        Assert(missing.Status == AccountAuthenticationStatus.AccountNotFound, "Missing account was accepted.");
        Assert(File.ReadAllBytes(accountPath).SequenceEqual(original), "Read-only account lookup modified the legacy file.");
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}

static void AccountLoginCoordinatorFlow()
{
    var request = new AccountLoginRequest("SANDBOX", "secret", new byte[52], 7640, 0, [1, 2, 3, 4]);
    var successfulSessions = new LoginSessionRegistry();
    successfulSessions.Open(1);
    var successful = new AccountLoginCoordinator(successfulSessions, new FixedAccountStore(new(AccountAuthenticationStatus.Success, "SANDBOX"))).HandleAsync(1, request).GetAwaiter().GetResult();
    Assert(successful.IsSuccess && successfulSessions.TryGet(1, out var successSession) && successSession!.State == LoginSessionState.CharacterSelection, "Successful account login did not enter character selection.");

    var failedSessions = new LoginSessionRegistry();
    failedSessions.Open(2);
    var failed = new AccountLoginCoordinator(failedSessions, new FixedAccountStore(new(AccountAuthenticationStatus.WrongPassword, null))).HandleAsync(2, request).GetAwaiter().GetResult();
    Assert(failed.Authentication == AccountAuthenticationStatus.WrongPassword && !failedSessions.TryGet(2, out _), "Failed account login did not close the session.");
}

static void CharacterSelectionWireLayout()
{
    var slots = LegacyCharacterSelection.CreateEmpty().Slots.ToArray();
    slots[0] = new LegacyCharacterSlot(7, 9, "HERO", new LegacyScore(3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21), [new LegacyItem(42, 1, 2, 3, 4, 5, 6), .. new LegacyItem[15]], 55, 66, 77);
    var bytes = new LegacyCharacterSelection(slots).ToBytes();

    Assert(bytes.Length == LegacyCharacterSelection.SizeInBytes, "Character selection wire size differs from STRUCT_SELCHAR.");
    Assert(System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(bytes) == 7 && System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(8)) == 9, "Saved position offsets differ.");
    Assert(System.Text.Encoding.ASCII.GetString(bytes, 16, 4) == "HERO", "Character name offset differs.");
    Assert(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(80)) == 3 && System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(272)) == 42, "Score or equipment offset differs.");
    Assert(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(784)) == 55 && System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(792)) == 66 && System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(808)) == 77, "Guild, coin, or experience offset differs.");
}

static void AccountLoginConfirmationWireLayout()
{
    var confirmation = new AccountLoginConfirmation(Enumerable.Range(1, 16).Select(static value => (byte)value).ToArray(), 99, LegacyCharacterSelection.CreateEmpty(), [new LegacyItem(44, 1, 2, 3, 4, 5, 6), .. new LegacyItem[127]], 123, "SANDBOX", Enumerable.Range(20, 12).Select(static value => (byte)value).ToArray(), new LegacyDailyQuest(7, 8, 0, 0, 0, 0, 0, 0, 9, 10, default, default, 11, 12, 13, 14), "BLOCK", true);
    var payload = confirmation.ToPayload();
    var frame = confirmation.ToFrame(LegacyFrameCodec.CreateDefault(), 1234, 7);
    var decoded = LegacyFrameCodec.CreateDefault().Decode(frame);

    Assert(payload.Length == AccountLoginConfirmation.PayloadSize && frame.Length == AccountLoginConfirmation.PacketSize, "Account login confirmation size differs from legacy x86 message.");
    Assert(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(16)) == 99 && System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(860)) == 44, "Confirmation preamble or cargo offset differs.");
    Assert(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(1884)) == 123 && System.Text.Encoding.ASCII.GetString(payload, 1888, 7) == "SANDBOX", "Confirmation coin or account offset differs.");
    Assert(System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(1916)) == 7 && System.Text.Encoding.ASCII.GetString(payload, 1968, 5) == "BLOCK" && payload[1984] == 1, "Confirmation quest or block offset differs.");
    Assert(decoded.IsChecksumValid && decoded.Header.Type == AccountLoginConfirmation.MessageType && decoded.Header.Id == AccountLoginConfirmation.SceneId, "Confirmation frame header differs.");
}

static void NewCharacterConfirmationWireLayout()
{
    var slots = LegacyCharacterSelection.CreateEmpty().Slots.ToArray();
    slots[2] = new LegacyCharacterSlot(0, 0, "HERO", default, new LegacyItem[16], 0, 0, 0);
    var confirmation = new NewCharacterConfirmation(new LegacyCharacterSelection(slots));
    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(confirmation.ToFrame(codec, 1234, 7));

    Assert(confirmation.ToPayload().Length == NewCharacterConfirmation.PayloadSize && NewCharacterConfirmation.PacketSize == 852, "New-character confirmation size differs from the 7.670 target client message.");
    Assert(frame.IsChecksumValid && frame.Header.Type == NewCharacterConfirmation.MessageType && frame.Header.Id == NewCharacterConfirmation.SceneId, "New-character confirmation frame header differs.");
    Assert(System.Text.Encoding.ASCII.GetString(frame.Payload.Span.Slice(48, 4)) == "HERO", "New-character confirmation did not preserve the 7.670 selection layout.");
}

static void CharacterSelectionV769Golden()
{
    using var fixtureStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-selection.golden.hex")
        ?? throw new InvalidOperationException("The 7.69 character-selection golden fixture is missing.");
    using var fixtureReader = new StreamReader(fixtureStream);
    var golden = Convert.FromHexString(fixtureReader.ReadToEnd().Trim());

    var slots = Enumerable.Range(0, CharacterSelectionV769.CharacterCount).Select(character =>
    {
        var name = new byte[CharacterSelectionV769.NameLength];
        System.Text.Encoding.ASCII.GetBytes($"CHAR{character}").CopyTo(name, 0);
        var score = new ClientScoreV769(
            (short)(100 + character), 0x01020304 + character, 0x11121314 + character,
            (byte)(0x30 + character), (byte)(0x40 + character),
            1000 + character, 2000 + character, 3000 + character, 4000 + character,
            (short)(10 + character), (short)(20 + character), (short)(30 + character), (short)(40 + character),
            (ushort)(50 + (character * 4)), (ushort)(51 + (character * 4)),
            (ushort)(52 + (character * 4)), (ushort)(53 + (character * 4)));
        var equipment = Enumerable.Range(0, CharacterSelectionV769.EquipmentCount)
            .Select(item => new LegacyItem(
                (short)(1000 + (character * 32) + item),
                (byte)(0x10 + character), (byte)(0x20 + item),
                (byte)(0x30 + character), (byte)(0x40 + item),
                (byte)(0x50 + character), (byte)(0x60 + item)))
            .ToArray();
        return new CharacterSelectionSlotV769(
            (ushort)(0x1100 + character), (ushort)(0x2200 + character), name,
            score, equipment, (ushort)(0x3300 + character),
            0x01010100 + character, 0x0102030405060700L + character);
    }).ToArray();

    var selection = new CharacterSelectionV769(slots);
    var bytes = selection.ToBytes();
    Assert(golden.Length == CharacterSelectionV769.SizeInBytes && bytes.AsSpan().SequenceEqual(golden),
        "The 7.69 character-selection DTO differs from the compiled Win32 header golden.");

    var confirmation = new NewCharacterConfirmationV769(selection);
    var payload = confirmation.ToPayload();
    Assert(payload.Length == 908 && payload.AsSpan(0, 4).SequenceEqual(new byte[4]) &&
        payload.AsSpan(NewCharacterConfirmationV769.AlignmentSize).SequenceEqual(golden),
        "7.69 NewCharacter alignment or selection offset differs from the x86 ABI.");
    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(confirmation.ToFrame(codec, 1234, 7));
    Assert(frame.IsChecksumValid && frame.Header.Size == 920 &&
        frame.Header.Type == NewCharacterConfirmationV769.MessageType &&
        frame.Header.Id == NewCharacterConfirmationV769.SceneId &&
        frame.Payload.Span.SequenceEqual(payload),
        "7.69 NewCharacter frame differs from the 920-byte x86 message contract.");

    var deleteFrame = codec.Decode(new DeleteCharacterConfirmationV769(selection).ToFrame(codec, 1234, 7));
    Assert(deleteFrame.IsChecksumValid && deleteFrame.Header.Size == 920 &&
        deleteFrame.Header.Type == DeleteCharacterConfirmationV769.MessageType &&
        deleteFrame.Header.Id == DeleteCharacterConfirmationV769.SceneId &&
        deleteFrame.Payload.Span.SequenceEqual(payload),
        "7.69 DeleteCharacter frame differs from the shared 920-byte x86 message contract.");
}

static void W2ppCharacterSelectionAdapter()
{
    var sourceSlots = LegacyCharacterSelection.CreateEmpty().Slots.ToArray();
    var equipment = Enumerable.Range(0, LegacyCharacterSelection.EquipmentCount)
        .Select(item => new LegacyItem((short)(200 + item), 1, (byte)item, 2, (byte)(item + 1), 3, (byte)(item + 2)))
        .ToArray();
    var score = new LegacyScore(
        123, 456, 789, Merchant: 9, AttackRun: 7, Direction: 8, ChaosRate: 6,
        MaxHp: 900, MaxMp: 800, Hp: 700, Mp: 600,
        Strength: 11, Intelligence: 12, Dexterity: 13, Constitution: 14,
        Special1: 15, Special2: 16, Special3: 17, Special4: 18);
    sourceSlots[0] = new LegacyCharacterSlot(
        unchecked((short)0x8123), unchecked((short)0x9234), "HERO", score,
        equipment, 0x3456, 123456, 0x0102030405060708L);

    var projected = W2ppCharacterSelectionV1Adapter.Adapt(new LegacyCharacterSelection(sourceSlots));
    var bytes = projected.ToBytes();
    var first = projected.Slots[0];
    Assert(first.HomeTownX == 0x8123 && first.HomeTownY == 0x9234 &&
        System.Text.Encoding.ASCII.GetString(first.MobName.Span[..4]) == "HERO",
        "The W2PP-to-7.69 adapter changed fixed-width name or coordinate bits.");
    Assert(first.Score.Level == 123 && first.Score.Ac == 456 && first.Score.Damage == 789 &&
        first.Score.Reserved == 0 && first.Score.AttackRun == 7 &&
        first.Score.Strength == 11 && first.Score.Special4 == 18,
        "The adapter did not project shared score semantics or clear target-reserved data.");
    Assert(first.Equipment.Count == 18 && first.Equipment[15] == equipment[15] &&
        first.Equipment[16] == default && first.Equipment[17] == default,
        "The adapter must preserve W2PP equipment and leave the two new 7.69 positions empty.");
    Assert(first.Guild == 0x3456 && first.Coin == 123456 && first.Experience == 0x0102030405060708L &&
        System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(CharacterSelectionV769.CoinOffset)) == 123456,
        "The adapter changed selection guild, currency, or experience.");
}

static void AccountLoginV769Golden()
{
    using var fixtureStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.account-login.golden.hex")
        ?? throw new InvalidOperationException("The 7.69 account-login golden fixture is missing.");
    using var fixtureReader = new StreamReader(fixtureStream);
    var golden = Convert.FromHexString(fixtureReader.ReadToEnd().Trim());

    var secretCode = Enumerable.Range(0, 16).Select(static index => (byte)(0xA0 + index)).ToArray();
    var cargo = Enumerable.Range(0, AccountLoginConfirmationV769.CargoCount)
        .Select(index => new LegacyItem(
            (short)(3000 + index),
            (byte)(0x10 + (index % 16)), (byte)(0x20 + (index % 32)),
            (byte)(0x30 + (index % 16)), (byte)(0x40 + (index % 32)),
            (byte)(0x50 + (index % 16)), (byte)(0x60 + (index % 32))))
        .ToArray();
    var confirmation = new AccountLoginConfirmationV769(
        secretCode,
        CreateZeroedCharacterSelectionV769(),
        cargo,
        0x11223344,
        "SYNTHETIC",
        unchecked((int)0x55667788),
        0x12345678);

    var payload = confirmation.ToPayload();
    Assert(golden.Length == AccountLoginConfirmationV769.PayloadSize && payload.AsSpan().SequenceEqual(golden),
        "The 7.69 account-login DTO differs from the compiled Win32 client-header golden.");
    Assert(AccountLoginConfirmationV769.PacketSize == 1928 &&
        AccountLoginConfirmationV769.SelectionOffset == 20 &&
        AccountLoginConfirmationV769.CargoOffset == 924 &&
        AccountLoginConfirmationV769.CoinOffset == 1884 &&
        AccountLoginConfirmationV769.Ssn1Offset == 1904 &&
        AccountLoginConfirmationV769.Ssn2Offset == 1908 &&
        payload.AsSpan(16, 4).SequenceEqual(new byte[4]) &&
        payload.AsSpan(1912, 4).SequenceEqual(new byte[4]),
        "The 7.69 account-login padding or payload offsets differ from the x86 ABI.");

    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(confirmation.ToFrame(codec, 1234, 7));
    Assert(frame.IsChecksumValid && frame.Header.Size == AccountLoginConfirmationV769.PacketSize &&
        frame.Header.Type == AccountLoginConfirmationV769.MessageType &&
        frame.Header.Id == AccountLoginConfirmationV769.SceneId &&
        frame.Payload.Span.SequenceEqual(payload),
        "The 7.69 account-login frame differs from the 1928-byte x86 client message contract.");
}

static void UpdateEquipV769Golden()
{
    using var fixtureStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.update-equip.golden.hex")
        ?? throw new InvalidOperationException("The 7.69 UpdateEquip golden fixture is missing.");
    using var fixtureReader = new StreamReader(fixtureStream);
    var golden = Convert.FromHexString(fixtureReader.ReadToEnd().Trim());

    var equipment = Enumerable.Range(0, EquipmentAppearanceV769.EquipmentCount)
        .Select(index => (ushort)(0x1000 + index)).ToArray();
    var ancientCodes = Enumerable.Range(0, EquipmentAppearanceV769.EquipmentCount)
        .Select(index => (byte)(0x40 + index)).ToArray();
    var appearance = new EquipmentAppearanceV769(equipment, ancientCodes);
    var payload = appearance.ToPayload();

    Assert(golden.Length == EquipmentAppearanceV769.PayloadSize && payload.AsSpan().SequenceEqual(golden),
        "The 7.69 UpdateEquip DTO differs from the compiled Win32 client-header golden.");
    Assert(EquipmentAppearanceV769.PacketSize == 68 &&
        BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(0)) == 0x1000 &&
        BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(34)) == 0x1011 &&
        payload[36] == 0x40 && payload[53] == 0x51 &&
        payload.AsSpan(54, 2).SequenceEqual(new byte[2]),
        "The 7.69 UpdateEquip offsets or x86 tail padding differ from the measured ABI.");

    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(appearance.ToFrame(codec, 1234, 7, 4));
    Assert(frame.IsChecksumValid && frame.Header.Size == EquipmentAppearanceV769.PacketSize &&
        frame.Header.Type == EquipmentAppearanceV769.MessageType && frame.Header.Id == 4 &&
        frame.Payload.Span.SequenceEqual(payload),
        "The 7.69 UpdateEquip frame differs from the 68-byte client contract.");

    var sourceEquipment = equipment[..W2ppEquipmentAppearanceV1Adapter.SourceEquipmentCount];
    var sourceAncientCodes = ancientCodes[..W2ppEquipmentAppearanceV1Adapter.SourceEquipmentCount];
    var projected = W2ppEquipmentAppearanceV1Adapter.Adapt(sourceEquipment, sourceAncientCodes);
    Assert(projected.VisualEquipment.Count == 18 && projected.AncientCodes.Count == 18 &&
        projected.VisualEquipment[15] == equipment[15] && projected.VisualEquipment[16] == 0 &&
        projected.VisualEquipment[17] == 0 && projected.AncientCodes[15] == ancientCodes[15] &&
        projected.AncientCodes[16] == 0 && projected.AncientCodes[17] == 0,
        "The W2PP-to-7.69 appearance adapter must preserve 16 slots and clear 16/17.");

    var wrongEquipmentRejected = false;
    try { W2ppEquipmentAppearanceV1Adapter.Adapt(equipment, sourceAncientCodes); }
    catch (ArgumentException) { wrongEquipmentRejected = true; }
    Assert(wrongEquipmentRejected, "The W2PP appearance adapter accepted an 18-slot source array.");
}

static void CreateMobV769Golden()
{
    using var fixtureStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.create-mob.golden.hex")
        ?? throw new InvalidOperationException("The 7.69 CreateMob golden fixture is missing.");
    using var fixtureReader = new StreamReader(fixtureStream);
    var golden = Convert.FromHexString(fixtureReader.ReadToEnd().Trim());

    var name = Convert.FromHexString("4142434445464748494A4B4C4B073412");
    var equipment = Enumerable.Range(0, CreateMobConfirmationV769.EquipmentCount)
        .Select(index => (ushort)(0x1000 + index)).ToArray();
    var affects = Enumerable.Range(0, CreateMobConfirmationV769.AffectCount)
        .Select(index => (ushort)(0x2000 + index)).ToArray();
    var ancientCodes = Enumerable.Range(0, CreateMobConfirmationV769.AncientCodeCount)
        .Select(index => (byte)(0x40 + index)).ToArray();
    var nick = new byte[CreateMobConfirmationV769.NickLength];
    Convert.FromHexString("4E49434B2D372E3639").CopyTo(nick, 0);
    var score = new ClientScoreV769(
        321,
        0x01020304,
        0x11121314,
        0x55,
        0x07,
        0x21222324,
        0x31323334,
        0x41424344,
        0x51525354,
        11,
        12,
        13,
        14,
        15,
        16,
        17,
        18);
    var confirmation = new CreateMobConfirmationV769(
        -321,
        654,
        0x1234,
        name,
        equipment,
        affects,
        0x3344,
        0x55,
        score,
        0x0042,
        ancientCodes,
        nick,
        0x07);
    var payload = confirmation.ToPayload();

    Assert(golden.Length == CreateMobConfirmationV769.PayloadSize && payload.AsSpan().SequenceEqual(golden),
        "The 7.69 CreateMob DTO differs from the compiled Win32 client-header golden.");
    Assert(CreateMobConfirmationV769.PacketSize == 236 &&
        BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(CreateMobConfirmationV769.PositionXOffset)) == -321 &&
        BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(CreateMobConfirmationV769.EquipmentOffset + 17 * 2)) == 0x1011 &&
        BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(CreateMobConfirmationV769.AffectOffset + 31 * 2)) == 0x201F &&
        payload[CreateMobConfirmationV769.GuildLevelOffset] == 0x55 &&
        payload.AsSpan(CreateMobConfirmationV769.GuildLevelOffset + 1, 3).SequenceEqual(new byte[3]) &&
        BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(CreateMobConfirmationV769.ScoreOffset)) == 321 &&
        BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(CreateMobConfirmationV769.CreateTypeOffset)) == 0x0042 &&
        payload[CreateMobConfirmationV769.AncientCodeOffset + 17] == 0x51 &&
        payload[CreateMobConfirmationV769.ServerOffset] == 0x07,
        "The 7.69 CreateMob offsets or deterministic padding differ from the measured ABI.");

    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(confirmation.ToFrame(codec, 1234, 7));
    Assert(frame.IsChecksumValid && frame.Header.Size == CreateMobConfirmationV769.PacketSize &&
        frame.Header.Type == CreateMobConfirmationV769.MessageType &&
        frame.Header.Id == CreateMobConfirmationV769.SceneId &&
        frame.Payload.Span.SequenceEqual(payload),
        "The 7.69 CreateMob frame differs from the 236-byte client contract.");

    var sourcePayload = new byte[W2ppCreateMobV1Adapter.SourcePayloadSize];
    BinaryPrimitives.WriteInt16LittleEndian(sourcePayload, -321);
    BinaryPrimitives.WriteInt16LittleEndian(sourcePayload.AsSpan(2), 654);
    BinaryPrimitives.WriteUInt16LittleEndian(sourcePayload.AsSpan(4), 0x1234);
    name.CopyTo(sourcePayload.AsSpan(W2ppCreateMobV1Adapter.SourceMobNameOffset));
    for (var index = 0; index < W2ppCreateMobV1Adapter.SourceEquipmentCount; index++)
        BinaryPrimitives.WriteUInt16LittleEndian(sourcePayload.AsSpan(W2ppCreateMobV1Adapter.SourceEquipmentOffset + index * 2), equipment[index]);
    for (var index = 0; index < CreateMobConfirmationV769.AffectCount; index++)
        BinaryPrimitives.WriteUInt16LittleEndian(sourcePayload.AsSpan(W2ppCreateMobV1Adapter.SourceAffectOffset + index * 2), affects[index]);
    BinaryPrimitives.WriteUInt16LittleEndian(sourcePayload.AsSpan(W2ppCreateMobV1Adapter.SourceGuildOffset), 0x3344);
    sourcePayload[W2ppCreateMobV1Adapter.SourceGuildLevelOffset] = 0x55;
    var sourceScore = new LegacyScore(
        321,
        0x01020304,
        0x11121314,
        0xAA,
        0x07,
        0xBB,
        0xCC,
        0x21222324,
        0x31323334,
        0x41424344,
        0x51525354,
        11,
        12,
        13,
        14,
        15,
        16,
        17,
        18);
    sourceScore.Write(sourcePayload.AsSpan(W2ppCreateMobV1Adapter.SourceScoreOffset, LegacyScore.SizeInBytes));
    BinaryPrimitives.WriteUInt16LittleEndian(sourcePayload.AsSpan(W2ppCreateMobV1Adapter.SourceCreateTypeOffset), 0x0042);
    ancientCodes[..W2ppCreateMobV1Adapter.SourceAncientCodeCount].CopyTo(sourcePayload.AsSpan(W2ppCreateMobV1Adapter.SourceAncientCodeOffset));
    nick.CopyTo(sourcePayload.AsSpan(W2ppCreateMobV1Adapter.SourceNickOffset));

    var projected = W2ppCreateMobV1Adapter.AdaptPayload(sourcePayload);
    Assert(projected.PositionX == -321 && projected.PositionY == 654 && projected.MobId == 0x1234 &&
        projected.VisualEquipment[15] == equipment[15] && projected.VisualEquipment[16] == 0 && projected.VisualEquipment[17] == 0 &&
        projected.Affects[31] == 0x201F && projected.AncientCodes.Span[15] == ancientCodes[15] &&
        projected.AncientCodes.Span[16] == 0 && projected.AncientCodes.Span[17] == 0 &&
        projected.Score.Level == 321 && projected.Score.Reserved == 0 && projected.Score.AttackRun == 0x07 &&
        projected.Nick.Span.SequenceEqual(nick) && projected.Server == 0,
        "The W2PP-to-7.69 CreateMob adapter did not preserve source fields or clear target-only slots.");

    var wrongPayloadRejected = false;
    try { W2ppCreateMobV1Adapter.AdaptPayload(sourcePayload[..^1]); }
    catch (ArgumentException) { wrongPayloadRejected = true; }
    Assert(wrongPayloadRejected, "The W2PP CreateMob adapter accepted a payload with the wrong size.");
}

static void CreateMobTradeV769Wire()
{
    var name = Convert.FromHexString("545241444553484F5000000000000000");
    var equipment = Enumerable.Range(0, CreateMobTradeConfirmationV769.EquipmentCount)
        .Select(index => (ushort)(0x1000 + index)).ToArray();
    var affects = Enumerable.Range(0, CreateMobTradeConfirmationV769.AffectCount)
        .Select(index => (ushort)(0x2000 + index)).ToArray();
    var secondaryAppearance = Enumerable.Range(0, CreateMobTradeConfirmationV769.SecondaryAppearanceLength)
        .Select(index => (byte)(0x40 + index)).ToArray();
    var nick = new byte[CreateMobTradeConfirmationV769.NickLength];
    Convert.FromHexString("4E49434B2D5452414445").CopyTo(nick, 0);
    var description = new byte[CreateMobTradeConfirmationV769.DescriptionLength];
    Convert.FromHexString("4C4F4A412041464B2037").CopyTo(description, 0);
    var score = new ClientScoreV769(
        321,
        0x01020304,
        0x11121314,
        0x55,
        0x07,
        0x21222324,
        0x31323334,
        0x41424344,
        0x51525354,
        11,
        12,
        13,
        14,
        15,
        16,
        17,
        18);
    var confirmation = new CreateMobTradeConfirmationV769(
        -321,
        654,
        0x1234,
        name,
        equipment,
        affects,
        0x3344,
        0x55,
        score,
        0x0042,
        secondaryAppearance,
        nick,
        description,
        0x07);
    var payload = confirmation.ToPayload();

    Assert(CreateMobTradeConfirmationV769.PayloadSize == 248 && CreateMobTradeConfirmationV769.PacketSize == 260,
        "The measured 7.69 CreateMobTrade size differs from the client Win32 ABI.");
    Assert(BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(CreateMobTradeConfirmationV769.PositionXOffset)) == -321 &&
        BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(CreateMobTradeConfirmationV769.EquipmentOffset + (17 * 2))) == 0x1011 &&
        BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(CreateMobTradeConfirmationV769.AffectOffset + (31 * 2))) == 0x201F &&
        payload[CreateMobTradeConfirmationV769.SecondaryAppearanceOffset + 17] == 0x51 &&
        payload.AsSpan(CreateMobTradeConfirmationV769.DescriptionOffset, 10).SequenceEqual(description.AsSpan(0, 10)) &&
        payload[CreateMobTradeConfirmationV769.ServerOffset] == 0x07 &&
        payload[CreateMobTradeConfirmationV769.ServerOffset + 1] == 0,
        "The 7.69 CreateMobTrade fields or trailing ABI padding differ from the measured offsets.");

    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(confirmation.ToFrame(codec, 1234, 7));
    Assert(frame.IsChecksumValid && frame.Header.Size == CreateMobTradeConfirmationV769.PacketSize &&
        frame.Header.Type == CreateMobTradeConfirmationV769.MessageType &&
        frame.Header.Id == CreateMobTradeConfirmationV769.SceneId &&
        frame.Payload.Span.SequenceEqual(payload),
        "The 7.69 CreateMobTrade frame differs from the 260-byte client contract.");
}

static void W2ppCreateMobTradeAdapter()
{
    var sourcePayload = new byte[W2ppCreateMobTradeV1Adapter.SourcePayloadSize];
    BinaryPrimitives.WriteInt16LittleEndian(sourcePayload, -321);
    BinaryPrimitives.WriteInt16LittleEndian(sourcePayload.AsSpan(2), 654);
    BinaryPrimitives.WriteUInt16LittleEndian(sourcePayload.AsSpan(4), 0x1234);

    var name = Convert.FromHexString("545241444553484F5000000000000000");
    name.CopyTo(sourcePayload.AsSpan(W2ppCreateMobTradeV1Adapter.SourceMobNameOffset));

    for (var index = 0; index < W2ppCreateMobTradeV1Adapter.SourceEquipmentCount; index++)
        BinaryPrimitives.WriteUInt16LittleEndian(
            sourcePayload.AsSpan(W2ppCreateMobTradeV1Adapter.SourceEquipmentOffset + (index * 2)),
            (ushort)(0x1000 + index));
    for (var index = 0; index < CreateMobTradeConfirmationV769.AffectCount; index++)
        BinaryPrimitives.WriteUInt16LittleEndian(
            sourcePayload.AsSpan(W2ppCreateMobTradeV1Adapter.SourceAffectOffset + (index * 2)),
            (ushort)(0x2000 + index));

    BinaryPrimitives.WriteUInt16LittleEndian(
        sourcePayload.AsSpan(W2ppCreateMobTradeV1Adapter.SourceGuildOffset), 0x3344);
    sourcePayload[W2ppCreateMobTradeV1Adapter.SourceGuildLevelOffset] = 0x55;

    var sourceScore = new LegacyScore(
        321,
        0x01020304,
        0x11121314,
        0xAA,
        0x07,
        0xBB,
        0xCC,
        0x21222324,
        0x31323334,
        0x41424344,
        0x51525354,
        11,
        12,
        13,
        14,
        15,
        16,
        17,
        18);
    sourceScore.Write(sourcePayload.AsSpan(W2ppCreateMobTradeV1Adapter.SourceScoreOffset, LegacyScore.SizeInBytes));
    BinaryPrimitives.WriteUInt16LittleEndian(
        sourcePayload.AsSpan(W2ppCreateMobTradeV1Adapter.SourceCreateTypeOffset), 0x0042);

    for (var index = 0; index < W2ppCreateMobTradeV1Adapter.SourceAppearanceCount; index++)
        sourcePayload[W2ppCreateMobTradeV1Adapter.SourceAppearanceOffset + index] = (byte)(0x40 + index);

    var nick = new byte[W2ppCreateMobTradeV1Adapter.SourceNickLength];
    Convert.FromHexString("4E49434B2D5452414445").CopyTo(nick, 0);
    nick.CopyTo(sourcePayload.AsSpan(W2ppCreateMobTradeV1Adapter.SourceNickOffset));

    var description = new byte[W2ppCreateMobTradeV1Adapter.SourceDescriptionLength];
    Convert.FromHexString("4C4F4A412041464B2037").CopyTo(description, 0);
    description.CopyTo(sourcePayload.AsSpan(W2ppCreateMobTradeV1Adapter.SourceDescriptionOffset));

    var projected = W2ppCreateMobTradeV1Adapter.AdaptPayload(sourcePayload);
    Assert(projected.PositionX == -321 && projected.PositionY == 654 && projected.MobId == 0x1234 &&
        projected.VisualEquipment[15] == 0x100F && projected.VisualEquipment[16] == 0 && projected.VisualEquipment[17] == 0 &&
        projected.Affects[31] == 0x201F && projected.SecondaryAppearance.Span[15] == 0x4F &&
        projected.SecondaryAppearance.Span[16] == 0 && projected.SecondaryAppearance.Span[17] == 0 &&
        projected.Score.Level == 321 && projected.Score.Reserved == 0 && projected.Score.AttackRun == 0x07 &&
        projected.Nick.Span.SequenceEqual(nick) && projected.Description.Span.SequenceEqual(description) &&
        projected.Server == 0,
        "The W2PP CreateMobTrade adapter did not preserve source fields or clear client-only entries.");

    var wrongPayloadRejected = false;
    try { W2ppCreateMobTradeV1Adapter.AdaptPayload(sourcePayload[..^1]); }
    catch (ArgumentException) { wrongPayloadRejected = true; }
    Assert(wrongPayloadRejected, "The W2PP CreateMobTrade adapter accepted a payload with the wrong size.");
}

static void AutoTradeVisualRelay()
{
    var mob = new byte[LegacyAccountSnapshot.CharacterStride];
    Convert.FromHexString("53484F50").CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset));
    BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset), 0x3344);
    mob[LegacyAccountSnapshot.MobGuildLevelOffset] = 9;
    var score = new LegacyScore(
        321, 100, 200, 0, 7, 0, 0, 3000, 1500, 2900, 1400,
        11, 12, 13, 14, 15, 16, 17, 18);
    score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset, LegacyScore.SizeInBytes));

    var equipment = new LegacyItem(100, 43, 233, 0, 0, 0, 0);
    equipment.Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + LegacyItem.SizeInBytes, LegacyItem.SizeInBytes));
    var killMark = new LegacyItem(547, 42, 7, 0, 3, 0, 4);
    killMark.Write(mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (LegacyAccountSnapshot.LegacyKillMarkCarrySlot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));

    var affect = new byte[LegacyAccountSnapshot.AffectStride];
    affect[0] = 5;
    BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), 300);
    affect[8] = 7;
    BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(12), 9);

    var snapshot = new LegacyAutoTradeSnapshot(
        7,
        2112,
        2042,
        "LOJA",
        new LegacyItem[AutoTradeListConfirmation.SlotCount],
        new sbyte[AutoTradeListConfirmation.SlotCount],
        new int[AutoTradeListConfirmation.SlotCount],
        5);
    var built = LegacyAutoTradeVisualRelay.TryBuild(
        snapshot,
        mob,
        affect,
        LegacyFrameCodec.CreateDefault(),
        1234,
        7,
        out var plan);

    Assert(built && plan is not null && plan.ShopConnectionId == 7 && plan.Frame.Length == CreateMobTradeConfirmationV769.PacketSize,
        "The autotrade visual relay did not build a client frame.");
    var decoded = LegacyFrameCodec.CreateDefault().Decode(plan!.Frame);
    var payload = decoded.Payload.Span;
    Assert(decoded.IsChecksumValid && decoded.Header.Size == CreateMobTradeConfirmationV769.PacketSize &&
        decoded.Header.Type == CreateMobTradeConfirmationV769.MessageType && decoded.Header.Id == CreateMobTradeConfirmationV769.SceneId &&
        BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(CreateMobTradeConfirmationV769.MobIdOffset)) == 7 &&
        payload[CreateMobTradeConfirmationV769.MobNameOffset + 12] == 42 &&
        payload[CreateMobTradeConfirmationV769.MobNameOffset + 13] == 7 &&
        payload[CreateMobTradeConfirmationV769.MobNameOffset + 14] == 3 &&
        payload[CreateMobTradeConfirmationV769.MobNameOffset + 15] == 4 &&
        BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(CreateMobTradeConfirmationV769.EquipmentOffset + 2)) == 0xA064 &&
        payload[CreateMobTradeConfirmationV769.SecondaryAppearanceOffset + 1] == 0x40 &&
        BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(CreateMobTradeConfirmationV769.AffectOffset)) == 0x05FF &&
        BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(CreateMobTradeConfirmationV769.AffectOffset + 2)) == 0x0709 &&
        BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(CreateMobTradeConfirmationV769.GuildOffset)) == 0x3344 &&
        BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(CreateMobTradeConfirmationV769.CreateTypeOffset)) == 0x00C0 &&
        payload.Slice(CreateMobTradeConfirmationV769.DescriptionOffset, 4).SequenceEqual(Convert.FromHexString("4C4F4A41")),
        "The autotrade visual relay changed the 7.69 MOB, equipment, affect, guild, or description projection.");

    var invalidTitle = snapshot with { Title = "LOJA\0" };
    Assert(!LegacyAutoTradeVisualRelay.TryBuild(invalidTitle, mob, affect, LegacyFrameCodec.CreateDefault(), 1, 0, out _),
        "The autotrade visual relay accepted a title containing an embedded NUL.");
}

static void AutoTradeRemovalRelay()
{
    var snapshot = new LegacyAutoTradeSnapshot(
        0x1234,
        2112,
        2042,
        "LOJA",
        new LegacyItem[AutoTradeListConfirmation.SlotCount],
        new sbyte[AutoTradeListConfirmation.SlotCount],
        new int[AutoTradeListConfirmation.SlotCount],
        5);
    var codec = LegacyFrameCodec.CreateDefault();
    var built = LegacyAutoTradeRemovalRelay.TryBuild(snapshot, codec, 1234, 7, out var plan);

    Assert(built && plan is not null && plan.ShopConnectionId == snapshot.ConnectionId &&
        plan.PositionX == snapshot.PositionX && plan.PositionY == snapshot.PositionY &&
        plan.Frame.Length == RemoveMobConfirmation.PacketSize,
        "The autotrade removal relay did not preserve the shop identity and position.");

    var decoded = codec.Decode(plan!.Frame);
    Assert(decoded.IsChecksumValid && decoded.Header.Type == RemoveMobConfirmation.MessageType &&
        decoded.Header.Size == RemoveMobConfirmation.PacketSize && decoded.Header.Id == snapshot.ConnectionId &&
        BinaryPrimitives.ReadInt32LittleEndian(decoded.Payload.Span) == 3,
        "The autotrade removal relay did not build the measured MSG_RemoveMob frame.");

    var invalid = snapshot with { ConnectionId = 0 };
    Assert(!LegacyAutoTradeRemovalRelay.TryBuild(invalid, codec, 1, 0, out _),
        "The autotrade removal relay accepted an invalid shop connection id.");
}

static void AutoTradeStateFileStore()
{
    var root = Path.Combine(Path.GetTempPath(), "wyd-cdk-autotrade-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var path = Path.Combine(root, "UP.json");
    try
    {
        var item = new LegacyItem(1234, 43, 233, 0, 0, 0, 0);
        var items = new LegacyItem[AutoTradeListConfirmation.SlotCount];
        var carryPositions = Enumerable.Repeat((sbyte)-1, AutoTradeListConfirmation.SlotCount).ToArray();
        var prices = new int[AutoTradeListConfirmation.SlotCount];
        items[0] = item;
        carryPositions[0] = 17;
        prices[0] = 456_789;
        var snapshot = new LegacyAutoTradeSnapshot(
            12,
            2112,
            2042,
            "LOJA PERSISTENTE",
            items,
            carryPositions,
            prices,
            5);
        var store = new LegacyAutoTradeFileStore(path, "UP");

        Assert(store.SaveAsync("shopper", 2, snapshot).GetAwaiter().GetResult() == LegacyAutoTradeStateResult.Saved,
            "The autotrade state store did not save a valid canonical listing.");
        var loaded = store.ReadAsync("SHOPPER", 2).GetAwaiter().GetResult();
        Assert(loaded is not null && loaded.AccountName == "SHOPPER" && loaded.CharacterSlot == 2 &&
            loaded.PositionX == 2112 && loaded.PositionY == 2042 && loaded.Title == snapshot.Title &&
            loaded.Items[0] == item && loaded.CarryPositions[0] == 17 && loaded.Prices[0] == 456_789 && loaded.Tax == 5,
            "The autotrade state store did not round-trip the owner, location, title, item, position, price, and tax.");

        var rebound = loaded!.BindToConnection(77);
        Assert(rebound.ConnectionId == 77 && rebound.Items.SequenceEqual(snapshot.Items) &&
            rebound.CarryPositions.SequenceEqual(snapshot.CarryPositions) && rebound.Prices.SequenceEqual(snapshot.Prices),
            "Rebinding a persisted listing to a runtime connection changed its canonical listing data.");
        Assert(store.ReadAllAsync().GetAwaiter().GetResult().Count == 1,
            "The autotrade state store returned an unexpected number of world listings.");

        Assert(store.RemoveAsync("shopper", 2).GetAwaiter().GetResult() == LegacyAutoTradeStateResult.Removed &&
            store.ReadAsync("SHOPPER", 2).GetAwaiter().GetResult() is null &&
            store.RemoveAsync("SHOPPER", 2).GetAwaiter().GetResult() == LegacyAutoTradeStateResult.NotFound,
            "The autotrade state store did not remove an owner listing atomically or report a missing listing.");

        File.WriteAllText(path, "{\"Version\":99,\"WorldKey\":\"UP\",\"Listings\":[]}");
        var rejectedDocument = false;
        try { _ = store.ReadAllAsync().GetAwaiter().GetResult(); }
        catch (InvalidDataException) { rejectedDocument = true; }
        Assert(rejectedDocument, "The autotrade state store accepted an unsupported document version.");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static void AutoTradeFilePurchaseCommit()
{
    var root = Path.Combine(Path.GetTempPath(), "wyd-cdk-autotrade-file-" + Guid.NewGuid().ToString("N"));
    var accountRoot = Path.Combine(root, "accounts");
    var statePath = Path.Combine(root, "UP.json");
    Directory.CreateDirectory(accountRoot);
    try
    {
        var item = new LegacyItem(900, 1, 2, 3, 4, 5, 6);
        var listingItems = Enumerable.Repeat(default(LegacyItem), AutoTradeListConfirmation.SlotCount).ToArray();
        var listingCarry = Enumerable.Repeat((sbyte)-1, AutoTradeListConfirmation.SlotCount).ToArray();
        var listingPrices = new int[AutoTradeListConfirmation.SlotCount];
        listingItems[0] = item;
        listingCarry[0] = 4;
        listingPrices[0] = 100;
        var listing = new LegacyAutoTradeSnapshot(12, 2_100, 2_100, "AFK SHOP", listingItems, listingCarry, listingPrices, 12);
        var persistedListing = new LegacyAutoTradePersistedListing(
            "SELLER", 1, listing.PositionX, listing.PositionY, listing.Title,
            listing.Items, listing.CarryPositions, listing.Prices, listing.Tax);
        var sellerCargo = Enumerable.Repeat(default(LegacyItem), LegacyAutoTradeBook.CargoSlotCount).ToArray();
        sellerCargo[4] = item;
        var buyerCarry = Enumerable.Repeat(default(LegacyItem), LegacyAccountSnapshot.MobCarryCount).ToArray();
        var context = new LegacyAutoTradePurchaseContext(
            100, true, false, 0, 12, true, false, true, true, true,
            0, 100, 12, item, listing, sellerCargo, buyerCarry, 150, 10, 0);
        Assert(LegacyAutoTradePurchasePlanBuilder.TryBuild(context, out var plan) == LegacyAutoTradePurchaseResult.Accepted && plan is not null,
            "The file purchase fixture could not build its pure plan.");

        var buyerMob = new byte[LegacyAccountSnapshot.CharacterStride];
        buyerMob[LegacyAccountSnapshot.MobNameOffset] = (byte)'B';
        BinaryPrimitives.WriteInt32LittleEndian(buyerMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), 150);
        var buyerUpdatedMob = buyerMob.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(buyerUpdatedMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), plan!.BuyerCoin);
        item.Write(buyerUpdatedMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (plan.BuyerDestinationSlot * LegacyItem.SizeInBytes)));
        var sellerMob = new byte[LegacyAccountSnapshot.CharacterStride];
        sellerMob[LegacyAccountSnapshot.MobNameOffset] = (byte)'S';

        void WriteAccount(string accountName, byte[] mob, int slot, int accountCoin, LegacyItem? cargoItem)
        {
            var directory = Path.Combine(accountRoot, accountName[..1]);
            Directory.CreateDirectory(directory);
            var file = new byte[LegacyAccountSnapshot.RequiredFileLength];
            mob.CopyTo(file.AsSpan(LegacyAccountSnapshot.CharactersOffset + (slot * LegacyAccountSnapshot.CharacterStride)));
            BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), accountCoin);
            if (cargoItem is { } value)
                value.Write(file.AsSpan(LegacyAccountSnapshot.CargoOffset + (4 * LegacyItem.SizeInBytes)));
            File.WriteAllBytes(Path.Combine(directory, accountName), file);
        }

        WriteAccount("BUYER", buyerMob, 0, 150, null);
        WriteAccount("SELLER", sellerMob, 1, 10, item);
        var stateStore = new LegacyAutoTradeFileStore(statePath, "UP", accountRoot);
        Assert(stateStore.SaveAsync("SELLER", 1, listing).GetAwaiter().GetResult() == LegacyAutoTradeStateResult.Saved,
            "The file purchase fixture could not persist the original listing.");

        var updatedPersistedListing = new LegacyAutoTradePersistedListing(
            "SELLER", 1, plan.UpdatedListing.PositionX, plan.UpdatedListing.PositionY,
            plan.UpdatedListing.Title, plan.UpdatedListing.Items, plan.UpdatedListing.CarryPositions,
            plan.UpdatedListing.Prices, plan.UpdatedListing.Tax);
        var request = new LegacyAutoTradePurchaseCommitRequest(
            "BUYER", 0, buyerMob, buyerMob, buyerUpdatedMob,
            2_110, 2_110, new byte[LegacyAccountSnapshot.MobExtraStride],
            "SELLER", 1, 10, plan, persistedListing, updatedPersistedListing);
        Assert(stateStore.TryCommitAsync(request).GetAwaiter().GetResult() == LegacyAutoTradePurchaseCommitResult.Committed,
            "The file purchase store rejected a valid journaled commit.");

        var buyerPath = Path.Combine(accountRoot, "B", "BUYER");
        var sellerPath = Path.Combine(accountRoot, "S", "SELLER");
        var committedBuyer = File.ReadAllBytes(buyerPath);
        var committedSeller = File.ReadAllBytes(sellerPath);
        Assert(BinaryPrimitives.ReadInt32LittleEndian(committedBuyer.AsSpan(LegacyAccountSnapshot.AccountCoinOffset)) == 50 &&
            LegacyItem.Read(committedBuyer.AsSpan(
                LegacyAccountSnapshot.CharactersOffset + LegacyAccountSnapshot.MobCarryOffset,
                LegacyItem.SizeInBytes)) == item &&
            BinaryPrimitives.ReadInt32LittleEndian(committedSeller.AsSpan(LegacyAccountSnapshot.AccountCoinOffset)) == 110 &&
            LegacyItem.Read(committedSeller.AsSpan(LegacyAccountSnapshot.CargoOffset + (4 * LegacyItem.SizeInBytes))) == default &&
            stateStore.ReadAsync("SELLER", 1).GetAwaiter().GetResult()!.Items[0].Index == 0 &&
            !File.Exists(statePath + ".journal"),
            "The file purchase store did not atomically persist buyer, seller, listing, and journal cleanup.");

        var beforeRecovery = File.ReadAllBytes(buyerPath);
        var afterRecovery = beforeRecovery.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(afterRecovery.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), 49);
        var recoveryJournal = new
        {
            Version = 1,
            WorldKey = "UP",
            Phase = "Prepared",
            Files = new[] { new { Path = buyerPath, WasPresent = true, Before = beforeRecovery, After = afterRecovery } },
        };
        File.WriteAllBytes(buyerPath, afterRecovery);
        File.WriteAllText(statePath + ".journal", JsonSerializer.Serialize(recoveryJournal));
        var recoveringStore = new LegacyAutoTradeFileStore(statePath, "UP", accountRoot);
        _ = recoveringStore.ReadAllAsync().GetAwaiter().GetResult();
        Assert(File.ReadAllBytes(buyerPath).SequenceEqual(beforeRecovery) && !File.Exists(statePath + ".journal"),
            "The file purchase store did not roll back a prepared journal during recovery.");

        var committedBefore = File.ReadAllBytes(sellerPath);
        var committedAfter = committedBefore.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(committedAfter.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), 111);
        var committedJournal = new
        {
            Version = 1,
            WorldKey = "UP",
            Phase = "Committed",
            Files = new[] { new { Path = sellerPath, WasPresent = true, Before = committedBefore, After = committedAfter } },
        };
        File.WriteAllBytes(sellerPath, committedBefore);
        File.WriteAllText(statePath + ".journal", JsonSerializer.Serialize(committedJournal));
        _ = recoveringStore.ReadAllAsync().GetAwaiter().GetResult();
        Assert(BinaryPrimitives.ReadInt32LittleEndian(File.ReadAllBytes(sellerPath).AsSpan(LegacyAccountSnapshot.AccountCoinOffset)) == 111 &&
            !File.Exists(statePath + ".journal"),
            "The file purchase store did not finish a committed journal during recovery.");
    }
    finally
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}

static void AutoTradeOfflineRehydration()
{
    var root = Path.Combine(Path.GetTempPath(), "wyd-cdk-autotrade-rehydrate-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var items = new LegacyItem[AutoTradeListConfirmation.SlotCount];
        var carryPositions = Enumerable.Repeat((sbyte)-1, AutoTradeListConfirmation.SlotCount).ToArray();
        var prices = new int[AutoTradeListConfirmation.SlotCount];
        var persisted = new LegacyAutoTradeSnapshot(12, 2112, 2042, "OFFLINE SHOP", items, carryPositions, prices, 5);
        var stateStore = new LegacyAutoTradeFileStore(Path.Combine(root, "UP.json"), "UP");
        Assert(stateStore.SaveAsync("offline", 1, persisted).GetAwaiter().GetResult() == LegacyAutoTradeStateResult.Saved,
            "The rehydration fixture could not save its canonical listing.");

        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Convert.FromHexString("4F46464C494E45").CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset));
        var character = new LegacyCharacterLoginData(
            mob,
            new byte[LegacyAccountSnapshot.ShortSkillStride],
            new byte[LegacyAccountSnapshot.AffectStride],
            new byte[LegacyAccountSnapshot.MobExtraStride],
            Donate: 0,
            SavedPositionX: 2112,
            SavedPositionY: 2042);
        var characterStore = new FixedCharacterLoginDataStore("OFFLINE", 1, character);
        var world = new WorldHub();
        var book = new LegacyAutoTradeBook();

        var report = LegacyAutoTradeRehydrator.RestoreAsync(world, book, characterStore, stateStore).GetAwaiter().GetResult();
        Assert(report.RestoredCount == 1 && !report.HasIssues, "Offline autotrade rehydration did not restore the valid listing.");

        var npc = world.GetNpcSnapshots().Single();
        Assert(npc.AutoTradeSnapshot is not null && npc.AutoTradeSnapshot.ConnectionId == npc.ConnectionId &&
            npc.AutoTradeOwnerAccount == "OFFLINE" && npc.AutoTradeCharacterSlot == 1 &&
            npc.PositionX == 2112 && npc.PositionY == 2042,
            "The rehydrated listing did not become a world NPC with its owner and position metadata.");
        Assert(book.TryGet(npc.ConnectionId, out var restored) && restored is not null && restored.Title == "OFFLINE SHOP",
            "The rehydrated listing was not registered in the runtime autotrade book.");
        Assert(LegacyAutoTradeVisualRelay.TryBuild(
                restored!,
                npc.MobSnapshot,
                npc.AffectSnapshot,
                LegacyFrameCodec.CreateDefault(),
                41,
                22,
                out var visual) && visual is not null && visual.Frame.Length == CreateMobTradeConfirmationV769.PacketSize,
            "The rehydrated NPC did not produce the client trade visual frame.");

        Assert(world.TryRemoveNpc(npc.ConnectionId, out var removed) && removed is not null &&
            world.GetNpcSnapshots().Count == 0,
            "The rehydrated offline NPC could not be removed from the world registry.");
    }
    finally
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}

static void AutoTradeReconnectCleanup()
{
    var root = Path.Combine(Path.GetTempPath(), "wyd-cdk-autotrade-reconnect-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var items = new LegacyItem[AutoTradeListConfirmation.SlotCount];
        var carryPositions = Enumerable.Repeat((sbyte)-1, AutoTradeListConfirmation.SlotCount).ToArray();
        var prices = new int[AutoTradeListConfirmation.SlotCount];
        var persisted = new LegacyAutoTradeSnapshot(12, 2112, 2042, "RECONNECT SHOP", items, carryPositions, prices, 5);
        var stateStore = new LegacyAutoTradeFileStore(Path.Combine(root, "UP.json"), "UP");
        Assert(stateStore.SaveAsync("offline", 1, persisted).GetAwaiter().GetResult() == LegacyAutoTradeStateResult.Saved,
            "The reconnect fixture could not save its canonical listing.");

        var world = new WorldHub();
        var playerMob = new byte[LegacyAccountSnapshot.CharacterStride];
        Assert(world.Enter(7, "BUYER", (_, _) => ValueTask.CompletedTask),
            "The reconnect fixture could not register the nearby player.");
        Assert(world.SetCharacterState(7, 0, 0, 0, 0, 0, 2112, 2042, playerMob),
            "The reconnect fixture could not set the nearby player position.");

        var shopMob = new byte[LegacyAccountSnapshot.CharacterStride];
        var shopId = world.EnterNpc(
            shopMob,
            2112,
            2042,
            autoTradeSnapshot: persisted,
            autoTradeOwnerAccount: "OFFLINE",
            autoTradeCharacterSlot: 1);
        Assert(shopId > 0, "The reconnect fixture could not register the offline shop NPC.");
        var book = new LegacyAutoTradeBook();
        Assert(book.TryRestore(shopId, persisted with { ConnectionId = shopId }),
            "The reconnect fixture could not register the offline listing.");

        var outcome = LegacyAutoTradeReconnectCoordinator.CloseAsync(world, book, stateStore, "offline", 1)
            .GetAwaiter().GetResult();
        Assert(outcome.Result == LegacyAutoTradeReconnectResult.Closed && outcome.ClosedListings.Count == 1,
            "Reconnect did not close the matching persisted offline shop.");
        Assert(outcome.ClosedListings[0].RecipientIds.Contains(7),
            "Reconnect did not capture the nearby player for the RemoveMob relay.");
        Assert(world.GetNpcSnapshots().Count == 0 && book.Snapshot().Count == 0,
            "Reconnect left the offline shop in the runtime world or autotrade book.");
        Assert(stateStore.ReadAsync("offline", 1).GetAwaiter().GetResult() is null,
            "Reconnect did not remove the persisted listing.");

        var second = LegacyAutoTradeReconnectCoordinator.CloseAsync(world, book, stateStore, "offline", 1)
            .GetAwaiter().GetResult();
        Assert(second.Result == LegacyAutoTradeReconnectResult.NoListing,
            "Reconnect reported a closed listing after the owner state was already cleared.");
    }
    finally
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}

static void RemoveMobV769Golden()
{
    using var fixtureStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.remove-mob.golden.hex")
        ?? throw new InvalidOperationException("The 7.69 RemoveMob golden fixture is missing.");
    using var fixtureReader = new StreamReader(fixtureStream);
    var golden = Convert.FromHexString(fixtureReader.ReadToEnd().Trim());

    var confirmation = new RemoveMobConfirmation(0x1234, 0x10203040);
    var payload = confirmation.ToPayload();
    Assert(golden.Length == 4 && payload.AsSpan().SequenceEqual(golden) &&
        RemoveMobConfirmation.PacketSize == 16 &&
        BinaryPrimitives.ReadInt32LittleEndian(payload) == 0x10203040,
        "The C# RemoveMob payload differs from the client 7.69 Win32 golden.");

    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(confirmation.ToFrame(codec, 1234, 7));
    Assert(frame.IsChecksumValid && frame.Header.Size == 16 &&
        frame.Header.Type == RemoveMobConfirmation.MessageType && frame.Header.Id == 0x1234 &&
        frame.Payload.Span.SequenceEqual(payload),
        "The RemoveMob frame differs from the measured 16-byte client/W2PP contract.");
}

static void UpdateAffectV769Golden()
{
    using var fixtureStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.update-affect.golden.hex")
        ?? throw new InvalidOperationException("The 7.69 UpdateAffect golden fixture is missing.");
    using var fixtureReader = new StreamReader(fixtureStream);
    var golden = Convert.FromHexString(fixtureReader.ReadToEnd().Trim());

    var affects = Enumerable.Range(0, UpdateAffectConfirmationV769.AffectCount)
        .Select(index => new ClientAffectV769(
            (byte)(0x10 + index),
            (sbyte)(-20 + index),
            (short)(0x2000 + index),
            0x01020300 + index))
        .ToArray();
    var confirmation = new UpdateAffectConfirmationV769(affects);
    var payload = confirmation.ToPayload();

    Assert(golden.Length == UpdateAffectConfirmationV769.PayloadSize && payload.AsSpan().SequenceEqual(golden),
        "The 7.69 UpdateAffect DTO differs from the compiled Win32 client-header golden.");
    Assert(UpdateAffectConfirmationV769.PacketSize == 268 &&
        payload[0] == 0x10 && payload[1] == 0xEC &&
        BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(2)) == 0x2000 &&
        BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(4)) == 0x01020300 &&
        payload[248] == 0x2F && payload[249] == 0x0B &&
        BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(250)) == 0x201F &&
        BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(252)) == 0x0102031F,
        "The 7.69 UpdateAffect offsets or field order differ from the measured ABI.");

    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(confirmation.ToFrame(codec, 0x1234, 1234, 7));
    Assert(frame.IsChecksumValid && frame.Header.Size == UpdateAffectConfirmationV769.PacketSize &&
        frame.Header.Type == UpdateAffectConfirmationV769.MessageType && frame.Header.Id == 0x1234 &&
        frame.Payload.Span.SequenceEqual(payload),
        "The 7.69 UpdateAffect frame differs from the 268-byte client contract.");

    var sourcePayload = new byte[W2ppUpdateAffectV1Adapter.SourcePayloadSize];
    for (var index = 0; index < W2ppUpdateAffectV1Adapter.SourceAffectCount; index++)
    {
        var offset = index * W2ppUpdateAffectV1Adapter.SourceAffectSize;
        sourcePayload[offset] = (byte)(0x10 + index);
        sourcePayload[offset + 1] = (byte)(index + 1);
        BinaryPrimitives.WriteUInt16LittleEndian(sourcePayload.AsSpan(offset + 2), (ushort)(20 + index));
        BinaryPrimitives.WriteUInt32LittleEndian(sourcePayload.AsSpan(offset + 4), (uint)(0x01020300 + index));
    }

    var projected = W2ppUpdateAffectV1Adapter.AdaptPayload(sourcePayload);
    Assert(projected.Affects[0].Type == 0x10 && projected.Affects[0].Value == 1 && projected.Affects[0].Level == 20 &&
        projected.Affects[31].Type == 0x2F && projected.Affects[31].Value == 32 && projected.Affects[31].Level == 51 &&
        projected.Affects[31].Time == 0x0102031F,
        "The W2PP-to-7.69 UpdateAffect adapter did not reorder or preserve fields.");

    BinaryPrimitives.WriteUInt16LittleEndian(sourcePayload.AsSpan(2), 500);
    var levelOverflowRejected = false;
    try { W2ppUpdateAffectV1Adapter.AdaptPayload(sourcePayload); }
    catch (OverflowException) { levelOverflowRejected = true; }
    Assert(levelOverflowRejected, "The W2PP affect adapter silently narrowed an oversized level.");

    BinaryPrimitives.WriteUInt16LittleEndian(sourcePayload.AsSpan(2), 20);
    BinaryPrimitives.WriteUInt32LittleEndian(sourcePayload.AsSpan(4), uint.MaxValue);
    var timeOverflowRejected = false;
    try { W2ppUpdateAffectV1Adapter.AdaptPayload(sourcePayload); }
    catch (OverflowException) { timeOverflowRejected = true; }
    Assert(timeOverflowRejected, "The W2PP affect adapter silently narrowed an oversized time.");
}

static void UpdateScoreV769Golden()
{
    using var fixtureStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.update-score.golden.hex")
        ?? throw new InvalidOperationException("The 7.69 UpdateScore golden fixture is missing.");
    using var fixtureReader = new StreamReader(fixtureStream);
    var golden = Convert.FromHexString(fixtureReader.ReadToEnd().Trim());

    var score = new ClientScoreV769(
        Level: 321,
        Ac: 0x11223344,
        Damage: 0x55667788,
        Reserved: 0x12,
        AttackRun: 0x34,
        MaxHp: 0x01020304,
        MaxMp: 0x11121314,
        Hp: 0x21222324,
        Mp: 0x31323334,
        Strength: 101,
        Intelligence: 202,
        Dexterity: 303,
        Constitution: 404,
        Special1: 0x0102,
        Special2: 0x0304,
        Special3: 0x0506,
        Special4: 0x0708);
    var affects = Enumerable.Range(0, UpdateScoreConfirmationV769.AffectCount)
        .Select(index => (ushort)(0x1000 + index))
        .ToArray();
    var confirmation = new UpdateScoreConfirmationV769(
        score,
        critical: 0x45,
        saveMana: 0x56,
        affects,
        guild: 0x2345,
        guildLevel: 0x67,
        resist: new byte[] { 0x78, 0x89, 0x9A, 0xAB },
        reqHp: 0x12345678,
        reqMp: 0x23456789,
        magic: 0x3456,
        rsv: 0x789A,
        learnedSkill: 0xBC);
    var payload = confirmation.ToPayload();

    Assert(golden.Length == UpdateScoreConfirmationV769.PayloadSize && payload.AsSpan().SequenceEqual(golden),
        "The 7.69 UpdateScore DTO differs from the compiled Win32 client-header golden.");
    Assert(UpdateScoreConfirmationV769.PacketSize == 152 &&
        BinaryPrimitives.ReadInt16LittleEndian(payload) == 321 &&
        BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(4)) == 0x11223344 &&
        payload[48] == 0x45 && payload[49] == 0x56 &&
        BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(50)) == 0x1000 &&
        BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(112)) == 0x101F &&
        BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(114)) == 0x2345 &&
        BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(116)) == 0x67 &&
        payload[118] == 0x78 && payload[121] == 0xAB &&
        BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(124)) == 0x12345678 &&
        BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(128)) == 0x23456789 &&
        BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(132)) == 0x3456 &&
        BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(134)) == 0x789A &&
        payload[136] == 0xBC && payload[137] == 0 && payload[138] == 0 && payload[139] == 0,
        "The 7.69 UpdateScore offsets or padding differ from the measured ABI.");

    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(confirmation.ToFrame(codec, 1234, 7, 0x1234));
    Assert(frame.IsChecksumValid && frame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
        frame.Header.Type == UpdateScoreConfirmationV769.MessageType && frame.Header.Id == 0x1234 &&
        frame.Payload.Span.SequenceEqual(payload),
        "The 7.69 UpdateScore frame differs from the 152-byte client contract.");

    var sourcePayload = new byte[W2ppUpdateScoreV1Adapter.SourcePayloadSize];
    var sourceScore = new LegacyScore(
        321,
        0x11223344,
        0x55667788,
        Merchant: 0x99,
        AttackRun: 0x34,
        Direction: 0xAA,
        ChaosRate: 0xBB,
        0x01020304,
        0x11121314,
        0x21222324,
        0x31323334,
        101,
        202,
        303,
        404,
        unchecked((short)0x0102),
        unchecked((short)0x0304),
        unchecked((short)0x0506),
        unchecked((short)0x0708));
    sourceScore.Write(sourcePayload);
    sourcePayload[W2ppUpdateScoreV1Adapter.SourceCriticalOffset] = 0x45;
    sourcePayload[W2ppUpdateScoreV1Adapter.SourceSaveManaOffset] = 0x56;
    for (var index = 0; index < UpdateScoreConfirmationV769.AffectCount; index++)
        BinaryPrimitives.WriteUInt16LittleEndian(sourcePayload.AsSpan(W2ppUpdateScoreV1Adapter.SourceAffectOffset + (index * sizeof(ushort))), (ushort)(0x1000 + index));
    BinaryPrimitives.WriteUInt16LittleEndian(sourcePayload.AsSpan(W2ppUpdateScoreV1Adapter.SourceGuildOffset), 0x2345);
    BinaryPrimitives.WriteUInt16LittleEndian(sourcePayload.AsSpan(W2ppUpdateScoreV1Adapter.SourceGuildLevelOffset), 0x67);
    new byte[] { 0x78, 0x89, 0x9A, 0xAB }.CopyTo(sourcePayload, W2ppUpdateScoreV1Adapter.SourceResistOffset);
    BinaryPrimitives.WriteInt32LittleEndian(sourcePayload.AsSpan(W2ppUpdateScoreV1Adapter.SourceCurrentHpOffset), 0x12345678);
    BinaryPrimitives.WriteInt32LittleEndian(sourcePayload.AsSpan(W2ppUpdateScoreV1Adapter.SourceCurrentMpOffset), 0x23456789);
    BinaryPrimitives.WriteInt32LittleEndian(sourcePayload.AsSpan(W2ppUpdateScoreV1Adapter.SourceMagicOffset), 0x3456);
    BinaryPrimitives.WriteUInt32LittleEndian(sourcePayload.AsSpan(W2ppUpdateScoreV1Adapter.SourceSpecialOffset), 0xAABBCCDD);

    var projected = W2ppUpdateScoreV1Adapter.AdaptPayload(sourcePayload);
    Assert(projected.Score.Level == 321 && projected.Score.Reserved == 0 && projected.Score.AttackRun == 0x34 &&
        projected.Score.Special1 == 0x0102 && projected.Score.Special4 == 0x0708 &&
        projected.Affects[0] == 0x1000 && projected.Affects[31] == 0x101F &&
        projected.ReqHp == 0x12345678 && projected.ReqMp == 0x23456789 &&
        projected.Magic == 0x3456 && projected.Rsv == 0 && projected.LearnedSkill == 0 &&
        projected.Resist.SequenceEqual(new byte[] { 0x78, 0x89, 0x9A, 0xAB }),
        "The W2PP-to-7.69 UpdateScore adapter did not map the shared fields or clear target-only fields.");

    BinaryPrimitives.WriteInt32LittleEndian(sourcePayload.AsSpan(0), short.MaxValue + 1);
    var levelOverflowRejected = false;
    try { W2ppUpdateScoreV1Adapter.AdaptPayload(sourcePayload); }
    catch (OverflowException) { levelOverflowRejected = true; }
    Assert(levelOverflowRejected, "The W2PP score adapter silently narrowed an oversized level.");

    sourceScore.Write(sourcePayload);
    BinaryPrimitives.WriteInt32LittleEndian(sourcePayload.AsSpan(W2ppUpdateScoreV1Adapter.SourceMagicOffset), ushort.MaxValue + 1);
    var magicOverflowRejected = false;
    try { W2ppUpdateScoreV1Adapter.AdaptPayload(sourcePayload); }
    catch (OverflowException) { magicOverflowRejected = true; }
    Assert(magicOverflowRejected, "The W2PP score adapter silently narrowed an oversized Magic value.");
}

static void UpdateEtcV769Golden()
{
    using var fixtureStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.update-etc.golden.hex")
        ?? throw new InvalidOperationException("The 7.69 UpdateEtc golden fixture is missing.");
    using var fixtureReader = new StreamReader(fixtureStream);
    var golden = Convert.FromHexString(fixtureReader.ReadToEnd().Trim());

    var confirmation = new UpdateEtcConfirmationV769(
        fakeExp: 0x11223344,
        experience: 0x0102030405060708,
        learnedSkill: new uint[] { 0x11121314, 0x21222324 },
        scoreBonus: unchecked((short)0x3132),
        specialBonus: unchecked((short)0x4142),
        skillBonus: unchecked((short)0x5152),
        coin: 0x61626364);
    var payload = confirmation.ToPayload();

    Assert(golden.Length == UpdateEtcConfirmationV769.PayloadSize && payload.AsSpan().SequenceEqual(golden),
        "The 7.69 UpdateEtc DTO differs from the compiled Win32 client-header golden.");
    Assert(UpdateEtcConfirmationV769.PacketSize == 48 &&
        BinaryPrimitives.ReadInt32LittleEndian(payload) == 0x11223344 &&
        BinaryPrimitives.ReadInt64LittleEndian(payload.AsSpan(4)) == 0x0102030405060708 &&
        BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(12)) == 0x11121314 &&
        BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(16)) == 0x21222324 &&
        BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(20)) == unchecked((short)0x3132) &&
        BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(22)) == unchecked((short)0x4142) &&
        BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(24)) == unchecked((short)0x5152) &&
        payload[26] == 0 && payload[27] == 0 &&
        BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(28)) == 0x61626364 &&
        payload[32] == 0 && payload[33] == 0 && payload[34] == 0 && payload[35] == 0,
        "The 7.69 UpdateEtc offsets or padding differ from the measured ABI.");

    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(confirmation.ToFrame(codec, 1234, 7, 0x1234));
    Assert(frame.IsChecksumValid && frame.Header.Size == UpdateEtcConfirmationV769.PacketSize &&
        frame.Header.Type == UpdateEtcConfirmationV769.MessageType && frame.Header.Id == 0x1234 &&
        frame.Payload.Span.SequenceEqual(payload),
        "The 7.69 UpdateEtc frame differs from the 48-byte client contract.");

    var sourcePayload = new byte[W2ppUpdateEtcV1Adapter.SourcePayloadSize];
    BinaryPrimitives.WriteUInt32LittleEndian(sourcePayload.AsSpan(W2ppUpdateEtcV1Adapter.SourceHoldOffset), 0x11223344);
    BinaryPrimitives.WriteInt64LittleEndian(sourcePayload.AsSpan(W2ppUpdateEtcV1Adapter.SourceExperienceOffset), 0x0102030405060708);
    BinaryPrimitives.WriteInt64LittleEndian(sourcePayload.AsSpan(W2ppUpdateEtcV1Adapter.SourceLearnOffset), 0x2122232411121314);
    BinaryPrimitives.WriteUInt16LittleEndian(sourcePayload.AsSpan(W2ppUpdateEtcV1Adapter.SourceScoreBonusOffset), 0x3132);
    BinaryPrimitives.WriteUInt16LittleEndian(sourcePayload.AsSpan(W2ppUpdateEtcV1Adapter.SourceSpecialBonusOffset), 0x4142);
    BinaryPrimitives.WriteUInt16LittleEndian(sourcePayload.AsSpan(W2ppUpdateEtcV1Adapter.SourceSkillBonusOffset), 0x5152);
    BinaryPrimitives.WriteUInt16LittleEndian(sourcePayload.AsSpan(W2ppUpdateEtcV1Adapter.SourceMagicOffset), 0x9999);
    BinaryPrimitives.WriteInt32LittleEndian(sourcePayload.AsSpan(W2ppUpdateEtcV1Adapter.SourceCoinOffset), 0x61626364);

    var projected = W2ppUpdateEtcV1Adapter.AdaptPayload(sourcePayload);
    Assert(projected.FakeExp == 0x11223344 && projected.Experience == 0x0102030405060708 &&
        projected.LearnedSkill[0] == 0x11121314 && projected.LearnedSkill[1] == 0x21222324 &&
        projected.ScoreBonus == unchecked((short)0x3132) &&
        projected.SpecialBonus == unchecked((short)0x4142) &&
        projected.SkillBonus == unchecked((short)0x5152) && projected.Coin == 0x61626364,
        "The W2PP-to-7.69 UpdateEtc adapter did not preserve shared fields or split Learn correctly.");

    BinaryPrimitives.WriteUInt32LittleEndian(sourcePayload.AsSpan(W2ppUpdateEtcV1Adapter.SourceHoldOffset), uint.MaxValue);
    var holdOverflowRejected = false;
    try { W2ppUpdateEtcV1Adapter.AdaptPayload(sourcePayload); }
    catch (OverflowException) { holdOverflowRejected = true; }
    Assert(holdOverflowRejected, "The W2PP UpdateEtc adapter silently narrowed an oversized Hold value.");

    var wrongPayloadRejected = false;
    try { W2ppUpdateEtcV1Adapter.AdaptPayload(sourcePayload[..^1]); }
    catch (ArgumentException) { wrongPayloadRejected = true; }
    Assert(wrongPayloadRejected, "The W2PP UpdateEtc adapter accepted a payload with the wrong size.");
}

static void CharacterLoginV769Golden()
{
    using var fixtureStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-login.golden.hex")
        ?? throw new InvalidOperationException("The 7.69 character-login golden fixture is missing.");
    using var fixtureReader = new StreamReader(fixtureStream);
    var golden = Convert.FromHexString(fixtureReader.ReadToEnd().Trim());

    static byte[] Pattern(int length, byte seed, byte step) =>
        Enumerable.Range(0, length).Select(index => unchecked((byte)(seed + index * step))).ToArray();

    var mob = Pattern(CharacterLoginConfirmationV769.MobSize, 0x10, 7);
    var shortSkill = Pattern(CharacterLoginConfirmationV769.ShortSkillSize, 0x20, 3);
    var ext1 = Pattern(CharacterLoginConfirmationV769.Ext1Size, 0x40, 5);
    var ext2 = Pattern(CharacterLoginConfirmationV769.Ext2Size, 0x60, 11);
    var confirmation = new CharacterLoginConfirmationV769(
        0x1234,
        0x5678,
        mob,
        0x9ABC,
        0xDEF0,
        0x1357,
        shortSkill,
        ext1,
        ext2);

    var payload = confirmation.ToPayload();
    Assert(golden.Length == CharacterLoginConfirmationV769.PayloadSize && payload.AsSpan().SequenceEqual(golden),
        "The 7.69 character-login DTO differs from the compiled Win32 client-header golden.");
    Assert(CharacterLoginConfirmationV769.PacketSize == 1728 &&
        CharacterLoginConfirmationV769.MobOffset == 4 &&
        CharacterLoginConfirmationV769.SlotOffset == 1044 &&
        CharacterLoginConfirmationV769.ClientIdOffset == 1046 &&
        CharacterLoginConfirmationV769.WeatherOffset == 1048 &&
        CharacterLoginConfirmationV769.ShortSkillOffset == 1050 &&
        CharacterLoginConfirmationV769.Ext1Offset == 1068 &&
        CharacterLoginConfirmationV769.Ext2Offset == 1356 &&
        payload.AsSpan(1066, 2).SequenceEqual(new byte[2]),
        "The 7.69 character-login offsets or x86 alignment differ from the Win32 ABI.");

    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(confirmation.ToFrame(codec, 1234, 7));
    Assert(frame.IsChecksumValid && frame.Header.Size == CharacterLoginConfirmationV769.PacketSize &&
        frame.Header.Type == CharacterLoginConfirmationV769.MessageType &&
        frame.Header.Id == CharacterLoginConfirmationV769.DefaultSceneId &&
        frame.Payload.Span.SequenceEqual(payload),
        "The 7.69 character-login frame differs from the 1728-byte x86 client message contract.");
}

static void W2ppCharacterLoginExtensionsProjection()
{
    var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
    const uint heldExperience = 123_456;
    BinaryPrimitives.WriteUInt32LittleEndian(mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraHoldOffset), heldExperience);

    var extensions = W2ppCharacterLoginExtensionsV1Adapter.Adapt(mobExtra);
    var ext1 = extensions.ToExt1();
    var ext2 = extensions.ToExt2();
    Assert(extensions.FakeExp == heldExperience && BinaryPrimitives.ReadInt32LittleEndian(ext1) == heldExperience,
        "W2PP Extra.Hold was not projected to 7.69 EXT1.Data[0] FakeExp.");
    Assert(ext1.Length == CharacterLoginExtensionsV769.Ext1Size && ext1.AsSpan(sizeof(int)).IndexOfAnyExcept((byte)0) < 0,
        "Unsupported EXT1 data/affects must be deterministically zeroed.");
    Assert(ext2.Length == CharacterLoginExtensionsV769.Ext2Size && ext2.AsSpan().IndexOfAnyExcept((byte)0) < 0,
        "Unmapped EXT2 state must be deterministically zeroed.");
    var confirmation = new CharacterLoginConfirmationV769(2100, 2100,
        new byte[CharacterLoginConfirmationV769.MobSize], 1, 7, 0,
        new byte[CharacterLoginConfirmationV769.ShortSkillSize], extensions);
    var payload = confirmation.ToPayload();
    Assert(payload.AsSpan(CharacterLoginConfirmationV769.Ext1Offset, ext1.Length).SequenceEqual(ext1) &&
        payload.AsSpan(CharacterLoginConfirmationV769.Ext2Offset, ext2.Length).SequenceEqual(ext2),
        "The typed 7.69 extension DTO was not assembled at the correct login offsets.");

    var wrongSizeRejected = false;
    try { W2ppCharacterLoginExtensionsV1Adapter.Adapt(mobExtra.AsSpan(1)); }
    catch (ArgumentException) { wrongSizeRejected = true; }
    Assert(wrongSizeRejected, "The W2PP MOBEXTRA adapter accepted a truncated source block.");

    BinaryPrimitives.WriteUInt32LittleEndian(mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraHoldOffset), uint.MaxValue);
    var overflowRejected = false;
    try { W2ppCharacterLoginExtensionsV1Adapter.Adapt(mobExtra); }
    catch (OverflowException) { overflowRejected = true; }
    Assert(overflowRejected, "A FakeExp value not representable by the target signed int was silently wrapped.");
}

static void W2ppCharacterLoginComposition()
{
    var mob = new byte[W2ppCharacterMobV1Adapter.SourceMobSize];
    System.Text.Encoding.ASCII.GetBytes("COMPOSED").CopyTo(mob, 0);
    mob[16] = 2;
    mob[17] = 1;
    BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(18), 0x1234);
    mob[20] = 3;
    var currentScore = LegacyScore.Read(new byte[LegacyScore.SizeInBytes]) with
    {
        Level = 42,
        Hp = 120,
        Mp = 35
    };
    currentScore.Write(mob.AsSpan(92, LegacyScore.SizeInBytes));

    var shortSkill = Enumerable.Range(0, 16).Select(index => (byte)(0x20 + index)).ToArray();
    var affect = Enumerable.Repeat((byte)0xA5, LegacyAccountSnapshot.AffectStride).ToArray();
    var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
    const uint fakeExperience = 0x01020304;
    BinaryPrimitives.WriteUInt32LittleEndian(mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraHoldOffset), fakeExperience);
    var data = new LegacyCharacterLoginData(
        mob,
        shortSkill,
        affect,
        mobExtra,
        Donate: 321,
        SavedPositionX: 1998,
        SavedPositionY: 1999);

    const short spawnX = 2100;
    const short spawnY = 2101;
    const ushort slot = 2;
    const ushort clientId = 7;
    const ushort weather = 3;
    var confirmation = W2ppCharacterLoginV1Adapter.Adapt(
        data, spawnX, spawnY, slot, clientId, weather);
    var payload = confirmation.ToPayload();
    var expectedMob = W2ppCharacterMobV1Adapter.Adapt(mob, spawnX, spawnY).ToBytes();

    Assert(payload.Length == CharacterLoginConfirmationV769.PayloadSize &&
        BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(CharacterLoginConfirmationV769.PosXOffset)) == spawnX &&
        BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(CharacterLoginConfirmationV769.PosYOffset)) == spawnY,
        "The composed 7.69 login did not use the resolved spawn in its leading coordinates.");
    Assert(payload.AsSpan(CharacterLoginConfirmationV769.MobOffset, expectedMob.Length).SequenceEqual(expectedMob) &&
        BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(CharacterLoginConfirmationV769.SlotOffset)) == slot &&
        BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(CharacterLoginConfirmationV769.ClientIdOffset)) == clientId &&
        BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(CharacterLoginConfirmationV769.WeatherOffset)) == weather,
        "The composed 7.69 login MOB, slot, client ID, or weather differs from its component contracts.");
    Assert(payload.AsSpan(CharacterLoginConfirmationV769.ShortSkillOffset, shortSkill.Length).SequenceEqual(shortSkill),
        "The W2PP 16-byte account short-skill block was not copied to the client login confirmation.");

    var ext1 = payload.AsSpan(CharacterLoginConfirmationV769.Ext1Offset, CharacterLoginConfirmationV769.Ext1Size);
    var ext2 = payload.AsSpan(CharacterLoginConfirmationV769.Ext2Offset, CharacterLoginConfirmationV769.Ext2Size);
    Assert(BinaryPrimitives.ReadInt32LittleEndian(ext1) == fakeExperience &&
        ext1[sizeof(int)..].IndexOfAnyExcept((byte)0) < 0 &&
        ext2.IndexOfAnyExcept((byte)0) < 0,
        "Only the mapped W2PP Hold -> FakeExp value should populate login extensions; Affect/EXT2 remain separate/unmapped.");

    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(confirmation.ToFrame(codec, 1234, 7));
    Assert(frame.IsChecksumValid && frame.Header.Size == 1728 &&
        frame.Header.Type == CharacterLoginConfirmationV769.MessageType &&
        frame.Payload.Span.SequenceEqual(payload),
        "The composed W2PP login did not serialize as a valid client 7.69 confirmation frame.");

    var invalidSlotRejected = false;
    try { W2ppCharacterLoginV1Adapter.Adapt(data, spawnX, spawnY, 4, clientId, weather); }
    catch (ArgumentOutOfRangeException) { invalidSlotRejected = true; }
    Assert(invalidSlotRejected, "The W2PP-to-7.69 login composer accepted a slot outside 0..3.");
}

static void ClientEquipmentStateV769Wire()
{
    var equipment = Enumerable.Range(0, ClientEquipmentStateV769.EquipmentCount)
        .Select(index => new LegacyItem((short)(900 + index), 1, (byte)index, 2, (byte)(index + 1), 3, (byte)(index + 2)))
        .ToArray();
    var state = new ClientEquipmentStateV769(equipment);
    var bytes = state.ToBytes();
    var roundTrip = ClientEquipmentStateV769.FromBytes(bytes);

    Assert(bytes.Length == 144 && roundTrip.Equipment.SequenceEqual(equipment) &&
        BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(17 * LegacyItem.SizeInBytes)) == 917,
        "The versioned 7.69 equipment state did not preserve all eighteen eight-byte items.");

    var rejected = false;
    try { ClientEquipmentStateV769.FromBytes(bytes.AsSpan(1)); }
    catch (ArgumentException) { rejected = true; }
    Assert(rejected, "The versioned 7.69 equipment state accepted a truncated 143-byte payload.");
}

static void W2ppCharacterMobProjection()
{
    var source = new byte[W2ppCharacterMobV1Adapter.SourceMobSize];
    System.Text.Encoding.ASCII.GetBytes("ABCDEFGHIJKLMNO").CopyTo(source, 0);
    source[16] = 1; // Clan
    source[17] = 2; // Merchant
    BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(18), 0x1234);
    source[20] = 3; // Class
    BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(22), 0x005A); // W2PP Rsv -> target byte
    source[24] = 0x81; // Quest -> zero-extended ushort
    BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(28), 0x10203040);
    BinaryPrimitives.WriteInt64LittleEndian(source.AsSpan(32), 0x0102030405060708L);

    var baseScore = new LegacyScore(
        10, 101, 202, Merchant: 9, AttackRun: 7, Direction: 8, ChaosRate: 6,
        MaxHp: 900, MaxMp: 800, Hp: 700, Mp: 600,
        Strength: 11, Intelligence: 12, Dexterity: 13, Constitution: 14,
        Special1: 15, Special2: 16, Special3: 17, Special4: 18);
    var currentScore = baseScore with { Level = 11, Hp = 555, Mp = 444 };
    baseScore.Write(source.AsSpan(44, LegacyScore.SizeInBytes));
    currentScore.Write(source.AsSpan(92, LegacyScore.SizeInBytes));

    for (var index = 0; index < 16; index++)
        new LegacyItem((short)(100 + index), 1, (byte)index, 2, (byte)(index + 1), 3, (byte)(index + 2))
            .Write(source.AsSpan(140 + index * LegacyItem.SizeInBytes, LegacyItem.SizeInBytes));

    BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(780), 0x89ABCDEF);
    BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(784), 6); // Magic
    BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(788), 123);
    BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(790), 124);
    BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(792), 125);
    source[794] = 9; // Critical
    source[795] = 8; // SaveMana
    source[796] = 1;
    source[797] = 2;
    source[798] = 3;
    source[799] = 4;
    source[800] = 7; // GuildLevel
    BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(802), 4);
    BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(804), 5);
    source[806] = 1;
    source[807] = 2;
    source[808] = 3;
    source[809] = 4;

    const int sourceCarryOffset = 268;
    var ordinaryKillMark = new LegacyItem(547, 75, 7, 76, 0x34, 77, 0x12);
    ordinaryKillMark.Write(source.AsSpan(sourceCarryOffset + 63 * LegacyItem.SizeInBytes, LegacyItem.SizeInBytes));

    var projected = W2ppCharacterMobV1Adapter.Adapt(source, 2100, 2101);
    var bytes = projected.ToBytes();
    using var fixtureStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("WydCdk.Engine.Tests.Fixtures.V769.character-mob.golden.hex")
        ?? throw new InvalidOperationException("The 7.69 character-MOB golden fixture is missing.");
    using var fixtureReader = new StreamReader(fixtureStream);
    var golden = Convert.FromHexString(fixtureReader.ReadToEnd().Trim());
    var word1Offset = CharacterMobV769.LearnedSkillOffset + sizeof(uint);
    Assert(BinaryPrimitives.ReadUInt32LittleEndian(golden.AsSpan(word1Offset)) == 0x12345678,
        "The compiled Win32 golden no longer exercises the second LearnedSkill word at its measured offset.");
    var w2ppProjectionGolden = golden.ToArray();
    w2ppProjectionGolden.AsSpan(word1Offset, sizeof(uint)).Clear();
    Assert(golden.Length == CharacterMobV769.SizeInBytes && bytes.AsSpan().SequenceEqual(w2ppProjectionGolden),
        "The W2PP projection differs from the compiled client golden after zeroing the 7.69-only skill word.");
    Assert(CharacterMobV769.SizeInBytes == 1040 && CharacterMobV769.EquipmentOffset == 140 &&
        CharacterMobV769.CarryOffset == 284 && CharacterMobV769.LearnedSkillOffset == 796 &&
        CharacterMobV769.CurrentKillOffset == 1036 && CharacterMobV769.TotalKillOffset == 1038,
        "The 7.69 MOB DTO offsets differ from the compiled Win32 layout probe.");
    Assert(bytes.Length == 1040 && projected.Clan == 1 && projected.Merchant == 2 && projected.Guild == 0x1234 &&
        projected.CharacterClass == 3 && projected.Reserved == 0x5A && projected.Quest == 0x81 &&
        projected.Coin == 0x10203040 && projected.Experience == 0x0102030405060708L &&
        projected.HomeTownX == 2100 && projected.HomeTownY == 2101,
        "The W2PP MOB prefix, widened Quest, or resolved login coordinates were not mapped correctly.");
    Assert(BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(CharacterMobV769.BaseScoreOffset)) == 10 &&
        bytes[CharacterMobV769.BaseScoreOffset + 12] == 0 &&
        bytes[CharacterMobV769.BaseScoreOffset + 13] == 7 &&
        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(CharacterMobV769.CurrentScoreOffset + 24)) == 555,
        "7.69 score projection did not narrow Level, clear reserved score bytes, or preserve current HP.");
    Assert(projected.Equipment.Count == 18 && projected.Equipment[0].Index == 100 &&
        projected.Equipment[15].Index == 115 && projected.Equipment[16] == default && projected.Equipment[17] == default &&
        projected.Carry.Count == 64 && projected.Carry[63] == ordinaryKillMark,
        "Equipment expansion or the 64-slot carry projection changed W2PP items.");
    Assert(System.Text.Encoding.ASCII.GetString(bytes, 0, 12) == "ABCDEFGHIJKL" &&
        bytes[12] == 75 && bytes[13] == 7 && bytes[14] == 0x34 && bytes[15] == 0x12 &&
        projected.CurrentKill == 7 && projected.TotalKill == 0x1234 &&
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(CharacterMobV769.CurrentKillOffset)) == 7 &&
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(CharacterMobV769.TotalKillOffset)) == 0x1234,
        "The 7.69 in-world name suffix was not constructed from W2PP kill-mark fields.");
    Assert(projected.LearnedSkill0 == 0x89ABCDEF && projected.LearnedSkill1 == 0 &&
        BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(CharacterMobV769.ScoreBonusOffset)) == 123 &&
        BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(CharacterMobV769.SpecialBonusOffset)) == 124 &&
        BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(CharacterMobV769.SkillBonusOffset)) == 125 &&
        bytes.AsSpan(CharacterMobV769.ShortSkillOffset, 4).SequenceEqual(new byte[] { 1, 2, 3, 4 }) &&
        bytes.AsSpan(CharacterMobV769.ResistOffset, 4).SequenceEqual(new byte[] { 1, 2, 3, 4 }) &&
        bytes.AsSpan(CharacterMobV769.DummyOffset, CharacterMobV769.DummyLength).IndexOfAnyExcept((byte)0) < 0,
        "The skills, bonuses, narrow fields, or deterministic target dummy region differ from the audited map.");

    var guiltySource = source.ToArray();
    guiltySource[sourceCarryOffset + 63 * LegacyItem.SizeInBytes + 4] = 8;
    Assert(W2ppCharacterMobV1Adapter.Adapt(guiltySource, 2100, 2101).MobName.Span[12] == 0,
        "W2PP GetCreateMob chaos was not cleared for a guilty player.");

    static bool Rejects(Action action, Type expected)
    {
        try { action(); }
        catch (Exception exception) when (expected.IsInstanceOfType(exception)) { return true; }
        return false;
    }

    var truncatedSource = source.AsSpan(1).ToArray();
    Assert(Rejects(() => W2ppCharacterMobV1Adapter.Adapt(truncatedSource, 2100, 2101), typeof(ArgumentException)),
        "The W2PP MOB adapter accepted an incorrect source size.");
    var highRsv = source.ToArray();
    BinaryPrimitives.WriteUInt16LittleEndian(highRsv.AsSpan(22), 0x0100);
    Assert(Rejects(() => W2ppCharacterMobV1Adapter.Adapt(highRsv, 2100, 2101), typeof(InvalidOperationException)),
        "The W2PP Rsv high byte was silently discarded.");
    var highMagic = source.ToArray();
    BinaryPrimitives.WriteUInt32LittleEndian(highMagic.AsSpan(784), 256);
    Assert(Rejects(() => W2ppCharacterMobV1Adapter.Adapt(highMagic, 2100, 2101), typeof(OverflowException)),
        "W2PP Magic overflow was silently truncated to one byte.");
    var highLevel = source.ToArray();
    BinaryPrimitives.WriteInt32LittleEndian(highLevel.AsSpan(44), 32768);
    Assert(Rejects(() => W2ppCharacterMobV1Adapter.Adapt(highLevel, 2100, 2101), typeof(OverflowException)),
        "W2PP score Level overflow was silently narrowed to the signed 7.69 field.");
    var highBonus = source.ToArray();
    BinaryPrimitives.WriteUInt16LittleEndian(highBonus.AsSpan(788), 32768);
    Assert(Rejects(() => W2ppCharacterMobV1Adapter.Adapt(highBonus, 2100, 2101), typeof(OverflowException)),
        "W2PP score bonus overflow was silently narrowed to the signed 7.69 field.");
    Assert(Rejects(() => W2ppCharacterMobV1Adapter.Adapt(source, -1, 2101), typeof(OverflowException)),
        "A negative live spawn coordinate was silently written to the unsigned client MOB.");
}

static void W2ppCharacterMobProjectionWithClientEquipment()
{
    var source = new byte[W2ppCharacterMobV1Adapter.SourceMobSize];
    System.Text.Encoding.ASCII.GetBytes("EXTENDED").CopyTo(source, 0);

    var targetEquipment = Enumerable.Range(0, CharacterMobV769.EquipmentCount)
        .Select(index => new LegacyItem((short)(700 + index), 1, (byte)index, 2, 3, 4, 5))
        .ToArray();
    var projected = W2ppCharacterMobV1Adapter.Adapt(source, 2100, 2101, targetEquipment);
    Assert(projected.Equipment.Count == CharacterMobV769.EquipmentCount &&
        projected.Equipment[15].Index == 715 && projected.Equipment[16].Index == 716 && projected.Equipment[17].Index == 717,
        "The 7.69 MOB projection did not preserve all eighteen supplied equipment slots.");

    var bytes = projected.ToBytes();
    Assert(BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(CharacterMobV769.EquipmentOffset + (16 * LegacyItem.SizeInBytes))) == 716 &&
        BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(CharacterMobV769.EquipmentOffset + (17 * LegacyItem.SizeInBytes))) == 717,
        "The client 7.69 MOB encoder did not place NewSlot1/NewSlot2 at the measured offsets.");

    var rejected = false;
    try { W2ppCharacterMobV1Adapter.Adapt(source, 2100, 2101, new LegacyItem[LegacyCharacterSelection.EquipmentCount]); }
    catch (ArgumentException) { rejected = true; }
    Assert(rejected, "The 7.69 projection accepted a partial equipment list and could have silently shifted slots.");
}

static void W2ppAccountLoginAdapterProjection()
{
    var slots = LegacyCharacterSelection.CreateEmpty().Slots.ToArray();
    slots[0] = new LegacyCharacterSlot(123, 456, "PLAYER", default, new LegacyItem[16], 7, 8, 9);
    var cargo = new LegacyItem[AccountLoginConfirmation.CargoCount];
    cargo[0] = new LegacyItem(42, 1, 2, 3, 4, 5, 6);
    cargo[119] = new LegacyItem(84, 6, 5, 4, 3, 2, 1);
    var source = new AccountLoginConfirmation(
        Enumerable.Range(1, 16).Select(static value => (byte)value).ToArray(),
        1,
        new LegacyCharacterSelection(slots),
        cargo,
        900,
        "SANDBOX",
        Enumerable.Range(20, 12).Select(static value => (byte)value).ToArray(),
        default,
        "BLOCK",
        true);

    var projected = W2ppAccountLoginV1Adapter.Adapt(source);
    Assert(projected.Cargo.Count == 120 && projected.Cargo[0] == cargo[0] && projected.Cargo[119] == cargo[119],
        "The W2PP account-login adapter did not preserve the 120 representable cargo items.");
    Assert(projected.Coin == 900 && projected.AccountName == "SANDBOX" &&
        projected.Selection.Slots[0].HomeTownX == 123 && projected.Selection.Slots[0].HomeTownY == 456,
        "The W2PP account-login adapter changed common account or selection fields.");
    Assert(projected.SecretCode.Span.SequenceEqual(new byte[16]) && projected.Ssn1 == 0 && projected.Ssn2 == 0,
        "The W2PP adapter must not conflate HashKeyTable with SecretCode or invent SSN values.");
    var projectedFrame = LegacyFrameCodec.CreateDefault().Decode(
        projected.ToFrame(LegacyFrameCodec.CreateDefault(), 1234, 7));
    Assert(projectedFrame.IsChecksumValid && projectedFrame.Header.Size == AccountLoginConfirmationV769.PacketSize &&
        projectedFrame.Header.Type == AccountLoginConfirmationV769.MessageType &&
        projectedFrame.Payload.Length == AccountLoginConfirmationV769.PayloadSize,
        "The listener account-login projection did not produce the 1928-byte 7.69 frame contract.");

    cargo[127] = new LegacyItem(99, 0, 0, 0, 0, 0, 0);
    var sourceWithOverflow = new AccountLoginConfirmation(
        new byte[16], 0, new LegacyCharacterSelection(slots), cargo, 0, "SANDBOX", new byte[12], default, "", false);
    var rejectedOverflow = false;
    try { _ = W2ppAccountLoginV1Adapter.Adapt(sourceWithOverflow); }
    catch (InvalidOperationException) { rejectedOverflow = true; }
    Assert(rejectedOverflow, "The W2PP adapter silently discarded an occupied cargo item beyond slot 119.");
}

static CharacterSelectionV769 CreateZeroedCharacterSelectionV769() => new(
    Enumerable.Range(0, CharacterSelectionV769.CharacterCount)
        .Select(_ => new CharacterSelectionSlotV769(
            0, 0, new byte[CharacterSelectionV769.NameLength], default,
            new LegacyItem[CharacterSelectionV769.EquipmentCount], 0, 0, 0))
        .ToArray());

static void LegacyAccountSnapshotReadsFixture()
{
    var file = new byte[LegacyAccountSnapshot.RequiredFileLength];
    const int charactersOffset = 216;
    const int characterStride = 816;
    const int cargoOffset = 3480;
    const int coinOffset = 4504;
    const int mobExtraOffset = 5600;
    const int mobExtraStride = 552;
    const int receivedItemOffset = 7864;
    const int questOffset = 7872;
    const int blockPassOffset = 7928;
    const int isBlockedOffset = 7944;

    var firstCharacter = file.AsSpan(charactersOffset, characterStride);
    System.Text.Encoding.ASCII.GetBytes("HERO").CopyTo(firstCharacter);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(firstCharacter[18..], 55); // Guild
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(firstCharacter[28..], 66); // Coin
    System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(firstCharacter[32..], 77); // Exp
    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(firstCharacter[40..], 8); // SPX
    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(firstCharacter[42..], 9); // SPY
    new LegacyScore(3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21).Write(firstCharacter.Slice(92, LegacyScore.SizeInBytes));
    new LegacyItem(42, 1, 2, 3, 4, 5, 6).Write(firstCharacter.Slice(140, LegacyItem.SizeInBytes));
    var firstMobExtra = file.AsSpan(mobExtraOffset, mobExtraStride);
    firstMobExtra[LegacyAccountSnapshot.MobExtraCitizenOffset] = 7;

    // Second character wears one of the five "empty face" placeholders; DBGetSelChar replaces it using mobExtra.
    var secondCharacter = file.AsSpan(charactersOffset + characterStride, characterStride);
    System.Text.Encoding.ASCII.GetBytes("MORTAL").CopyTo(secondCharacter);
    new LegacyItem(22, 0, 0, 0, 0, 0, 0).Write(secondCharacter.Slice(140, LegacyItem.SizeInBytes));
    var secondMobExtra = file.AsSpan(mobExtraOffset + mobExtraStride, mobExtraStride);
    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(secondMobExtra, 2); // ClassMaster == MORTAL
    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(secondMobExtra[14..], 5); // MortalFace (unused when MORTAL)

    // Third character is not a mortal: face comes from MortalFace + 7.
    var thirdCharacter = file.AsSpan(charactersOffset + (2 * characterStride), characterStride);
    System.Text.Encoding.ASCII.GetBytes("ARCH").CopyTo(thirdCharacter);
    new LegacyItem(32, 0, 0, 0, 0, 0, 0).Write(thirdCharacter.Slice(140, LegacyItem.SizeInBytes));
    var thirdMobExtra = file.AsSpan(mobExtraOffset + (2 * mobExtraStride), mobExtraStride);
    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(thirdMobExtra, 0); // ClassMaster != MORTAL
    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(thirdMobExtra[14..], 5); // MortalFace

    new LegacyItem(99, 1, 1, 1, 1, 1, 1).Write(file.AsSpan(cargoOffset, LegacyItem.SizeInBytes));
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(coinOffset), 12345);
    file[receivedItemOffset] = 1;
    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(questOffset), 7); // IndexQuest
    System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(file.AsSpan(questOffset + 40), 1_700_000_000); // LastTimeQuest
    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(questOffset + 48), 3); // MobCount1
    System.Text.Encoding.ASCII.GetBytes("SECRET").CopyTo(file.AsSpan(blockPassOffset));
    file[isBlockedOffset] = 1;

    var snapshot = LegacyAccountSnapshot.Read("SANDBOX", file);
    var hero = snapshot.Characters.Slots[0];
    Assert(hero.Name == "HERO" && hero.Guild == 55 && hero.Coin == 66 && hero.Experience == 77, "Character name, guild, coin, or experience differ from fixture.");
    Assert(hero.SavedPositionX == 8 && hero.SavedPositionY == 9, "SPX/SPY were not projected into the character-selection snapshot.");
    Assert(hero.Score.Level == 3 && hero.Equipment[0].Index == 42 && hero.Equipment[0].Effect3 == 28 && hero.Equipment[0].Value3 == 7, "Character score, equipment, or citizen marker differs from fixture.");
    Assert(snapshot.Characters.Slots[1].Equipment[0].Index == 21, "Mortal face substitution (ClassMaster == MORTAL) was not applied.");
    Assert(snapshot.Characters.Slots[2].Equipment[0].Index == 12, "Non-mortal face substitution (MortalFace + 7) was not applied.");
    Assert(snapshot.Characters.Slots[3].SavedPositionX == LegacyCharacterStorageDefaults.EmptyCharacterPosition && snapshot.Characters.Slots[3].SavedPositionY == LegacyCharacterStorageDefaults.EmptyCharacterPosition, "A zeroed empty slot was not projected with BASE_ClearMob's default position.");
    Assert(snapshot.Cargo[0].Index == 99 && snapshot.Coin == 12345, "Cargo or account coin offset differs from fixture.");
    Assert(snapshot.WelcomeItemReceived && snapshot.IsBlocked && snapshot.BlockPassword == "SECRET", "ReceivedItem, IsBlocked, or BlockPass offset differs from fixture.");
    Assert(snapshot.DailyQuest.IndexQuest == 7 && snapshot.DailyQuest.LastTimeQuest == unchecked((int)1_700_000_000L) && snapshot.DailyQuest.Mob1Count == 3, "QuestDiaria offset or 64-bit-to-wire truncation differs from fixture.");

    var confirmation = snapshot.ToConfirmation();
    Assert(confirmation.Unknown28 == 1 && confirmation.IsBlocked && confirmation.BlockPassword == "SECRET", "ToConfirmation did not carry ReceivedItem/IsBlocked/BlockPassword from the snapshot.");

    var root = Path.Combine(Path.GetTempPath(), $"wyd-cdk-snapshot-test-{Guid.NewGuid():N}");
    var accountPath = Path.Combine(root, "S", "SANDBOX");
    try
    {
        Directory.CreateDirectory(Path.GetDirectoryName(accountPath)!);
        File.WriteAllBytes(accountPath, file);
        var original = File.ReadAllBytes(accountPath);

        var fromStore = new LegacyFileAccountStore(root).ReadSnapshotAsync("sandbox").GetAwaiter().GetResult();
        Assert(fromStore is not null && fromStore.AccountName == "SANDBOX" && fromStore.Characters.Slots[0].Name == "HERO", "Store-based snapshot read differs from direct parse.");
        Assert(File.ReadAllBytes(accountPath).SequenceEqual(original), "Read-only snapshot lookup modified the legacy file.");
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}

static void LegacyCharacterNameValidation()
{
    Assert(LegacyCharacterName.IsValid("HERO-1"), "Valid alphanumeric-with-dash name was rejected.");
    Assert(!LegacyCharacterName.IsValid("ABC"), "Name shorter than four characters was accepted.");
    Assert(!LegacyCharacterName.IsValid(new string('A', 16)), "Name of sixteen characters (>= NAME_LENGTH) was accepted.");
    Assert(!LegacyCharacterName.IsValid("king"), "Reserved client command name (BASE_CheckValidString) was accepted.");
    Assert(!LegacyCharacterName.IsValid("KING"), "Reserved server command name was accepted regardless of case.");
    Assert(!LegacyCharacterName.IsValid("COM1"), "Reserved device name was accepted.");
    Assert(!LegacyCharacterName.IsValid("HE!RO"), "Name with a disallowed symbol was accepted.");
}

static void LegacyCharacterTemplateStoreReadsClassFiles()
{
    var root = Path.Combine(Path.GetTempPath(), $"wyd-cdk-basemob-test-{Guid.NewGuid():N}");
    try
    {
        Directory.CreateDirectory(root);
        foreach (var (fileName, classIndex) in new[] { ("TK", 0), ("FM", 1), ("BM", 2), ("HT", 3) })
        {
            var mob = new byte[LegacyAccountSnapshot.CharacterStride];
            System.Text.Encoding.ASCII.GetBytes(fileName).CopyTo(mob, 0);
            new LegacyScore(classIndex + 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0).Write(mob.AsSpan(92, LegacyScore.SizeInBytes)); // CurrentScore
            File.WriteAllBytes(Path.Combine(root, fileName), mob);
        }

        var templates = new LegacyCharacterTemplateStore(root);
        for (var classIndex = 0; classIndex < 4; classIndex++)
        {
            var template = templates.ReadTemplateAsync(classIndex).GetAwaiter().GetResult();
            Assert(template is not null && template.Length == LegacyAccountSnapshot.CharacterStride, "Class template was not read.");
            var baseScore = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(template.AsSpan(44));
            Assert(baseScore == classIndex + 1, "BaseScore was not synced from CurrentScore, unlike Server.cpp's post-load fixup.");
        }

        Assert(templates.ReadTemplateAsync(4).GetAwaiter().GetResult() is null, "Out-of-range class was accepted.");
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}

static void CreateCharacterCoordinatorPersistsCharacter()
{
    var root = Path.Combine(Path.GetTempPath(), $"wyd-cdk-createchar-test-{Guid.NewGuid():N}");
    var accountRoot = Path.Combine(root, "account");
    var baseMobRoot = Path.Combine(root, "BaseMob");
    try
    {
        Directory.CreateDirectory(Path.Combine(accountRoot, "S"));
        Directory.CreateDirectory(baseMobRoot);
        File.WriteAllBytes(Path.Combine(accountRoot, "S", "SANDBOX"), new byte[LegacyAccountSnapshot.RequiredFileLength]);

        var template = new byte[LegacyAccountSnapshot.CharacterStride];
        System.Text.Encoding.ASCII.GetBytes("TEMPLATE").CopyTo(template, 0); // must be fully replaced by the chosen name
        new LegacyItem(22, 1, 2, 3, 4, 5, 6).Write(template.AsSpan(140, LegacyItem.SizeInBytes)); // placeholder ("empty face") slot
        for (var equipment = 1; equipment < 16; equipment++)
            new LegacyItem((short)(100 + equipment), 0, 0, 0, 0, 0, 0).Write(template.AsSpan(140 + (equipment * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        File.WriteAllBytes(Path.Combine(baseMobRoot, "TK"), template);

        var accounts = new LegacyFileAccountStore(accountRoot);
        var coordinator = new CreateCharacterCoordinator(accounts, new LegacyCharacterTemplateStore(baseMobRoot));
        var request = new CreateCharacterRequest(1, "HERO", 0);

        var outcome = coordinator.HandleAsync("SANDBOX", request, secureVerified: true).GetAwaiter().GetResult();
        Assert(outcome.IsSuccess, $"Character creation failed unexpectedly: {outcome.Status}.");
        var created = outcome.Characters!.Slots[1];
        Assert(created.Name == "HERO", "Created character name differs from the request.");
        Assert(created.Equipment[0].Index == 21, "New character's placeholder face was not replaced with the MORTAL face (21), as DBGetSelChar would.");
        Assert(created.Equipment[1].Index == 101, "Non-placeholder equipment from the template was not preserved.");

        var occupied = coordinator.HandleAsync("SANDBOX", request with { CharacterName = "OTHER" }, secureVerified: true).GetAwaiter().GetResult();
        Assert(occupied.Status == CreateCharacterStatus.SlotOccupied, "Re-creating into an occupied slot was accepted.");

        Directory.CreateDirectory(Path.Combine(accountRoot, "O"));
        File.WriteAllBytes(Path.Combine(accountRoot, "O", "OTHERACC"), new byte[LegacyAccountSnapshot.RequiredFileLength]);
        var duplicateName = coordinator.HandleAsync("OTHERACC", new CreateCharacterRequest(0, "HERO", 0), secureVerified: true).GetAwaiter().GetResult();
        Assert(duplicateName.Status == CreateCharacterStatus.NameTaken, "A character name already reserved by another account was accepted.");

        var unverified = coordinator.HandleAsync("SANDBOX", new CreateCharacterRequest(0, "OTHER", 0), secureVerified: false).GetAwaiter().GetResult();
        Assert(unverified.Status == CreateCharacterStatus.SecureNotVerified, "Character creation was accepted without a verified PIN.");

        var raw = File.ReadAllBytes(Path.Combine(accountRoot, "S", "SANDBOX"));
        var shortSkill = raw.AsSpan(LegacyAccountSnapshot.ShortSkillOffset + LegacyAccountSnapshot.ShortSkillStride, LegacyAccountSnapshot.ShortSkillStride);
        Assert(shortSkill.ToArray().All(static value => value == 0xFF), "New character's ShortSkill slot was not reset to 0xFF.");
        var affect = raw.AsSpan(LegacyAccountSnapshot.AffectOffset + LegacyAccountSnapshot.AffectStride, LegacyAccountSnapshot.AffectStride);
        Assert(affect.ToArray().All(static value => value == 0), "New character's affect slot was not cleared.");

        var deleted = new DeleteCharacterCoordinator(accounts).HandleAsync("SANDBOX", new DeleteCharacterRequest(1, "HERO", ""), secureVerified: true).GetAwaiter().GetResult();
        var deletedSnapshot = deleted.Snapshot;
        Assert(deleted.IsSuccess && deletedSnapshot is not null && deletedSnapshot.Characters.Slots[1].Name.Length == 0, "Character deletion did not clear the persisted slot.");
        Assert(deletedSnapshot is not null && deletedSnapshot.Characters.Slots[1].SavedPositionX == LegacyCharacterStorageDefaults.EmptyCharacterPosition && deletedSnapshot.Characters.Slots[1].SavedPositionY == LegacyCharacterStorageDefaults.EmptyCharacterPosition, "Deleted character slot did not retain BASE_ClearMob's default position.");
        var deletedRaw = File.ReadAllBytes(Path.Combine(accountRoot, "S", "SANDBOX"));
        var deletedOffset = LegacyAccountSnapshot.CharactersOffset + LegacyAccountSnapshot.CharacterStride;
        Assert(BinaryPrimitives.ReadInt16LittleEndian(deletedRaw.AsSpan(deletedOffset + LegacyAccountSnapshot.MobSavedPositionXOffset)) == LegacyCharacterStorageDefaults.EmptyCharacterPosition && BinaryPrimitives.ReadInt16LittleEndian(deletedRaw.AsSpan(deletedOffset + LegacyAccountSnapshot.MobSavedPositionYOffset)) == LegacyCharacterStorageDefaults.EmptyCharacterPosition, "Deleted character storage did not persist BASE_ClearMob's default position.");
        Assert(!File.Exists(Path.Combine(root, "char", "H", "HERO")), "Character deletion did not release the global name reservation.");
        Assert(new DeleteCharacterCoordinator(accounts).HandleAsync("SANDBOX", new DeleteCharacterRequest(1, "HERO", ""), secureVerified: true).GetAwaiter().GetResult().Status == DeleteCharacterStatus.NotAvailable, "Deleting an empty slot was accepted.");
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}

static void CreateCharacterCoordinatorRejectsInvalidRequests()
{
    var root = Path.Combine(Path.GetTempPath(), $"wyd-cdk-createchar-invalid-test-{Guid.NewGuid():N}");
    var accountRoot = Path.Combine(root, "account");
    var baseMobRoot = Path.Combine(root, "BaseMob");
    try
    {
        Directory.CreateDirectory(Path.Combine(accountRoot, "S"));
        Directory.CreateDirectory(baseMobRoot);
        File.WriteAllBytes(Path.Combine(accountRoot, "S", "SANDBOX"), new byte[LegacyAccountSnapshot.RequiredFileLength]);
        File.WriteAllBytes(Path.Combine(baseMobRoot, "TK"), new byte[LegacyAccountSnapshot.CharacterStride]);

        var coordinator = new CreateCharacterCoordinator(new LegacyFileAccountStore(accountRoot), new LegacyCharacterTemplateStore(baseMobRoot));

        Assert(coordinator.HandleAsync("SANDBOX", new CreateCharacterRequest(-1, "HERO", 0), secureVerified: true).GetAwaiter().GetResult().Status == CreateCharacterStatus.SlotOutOfRange, "Negative slot was accepted.");
        Assert(coordinator.HandleAsync("SANDBOX", new CreateCharacterRequest(0, "HERO", 9), secureVerified: true).GetAwaiter().GetResult().Status == CreateCharacterStatus.ClassOutOfRange, "Out-of-range class was accepted.");
        Assert(coordinator.HandleAsync("SANDBOX", new CreateCharacterRequest(0, "king", 0), secureVerified: true).GetAwaiter().GetResult().Status == CreateCharacterStatus.InvalidName, "Reserved character name was accepted.");
        Assert(coordinator.HandleAsync("UNKNOWN", new CreateCharacterRequest(0, "HERO", 0), secureVerified: true).GetAwaiter().GetResult().Status == CreateCharacterStatus.AccountNotFound, "Unknown account was accepted.");
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}

static void CharacterLoginParser()
{
    const int RetailCharacterLoginPayloadSize = 24;
    var codec = LegacyFrameCodec.CreateDefault();
    var payload = new byte[8];
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(payload, 2);
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4), 1);
    var frame = codec.Decode(codec.Encode(CharacterLoginRequest.MessageType, 4, 33, payload, 16));

    Assert(CharacterLoginRequest.TryParse(frame, out var request), "Valid character-login packet was not parsed.");
    Assert(request!.Slot == 2 && request.Force == 1, "Parsed character-login fields differ.");

    var retailPayload = new byte[RetailCharacterLoginPayloadSize];
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(retailPayload, 3);
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(retailPayload.AsSpan(4), 0);
    var retailFrame = codec.Decode(codec.Encode(CharacterLoginRequest.MessageType, 4, 33, retailPayload, 16));
    Assert(CharacterLoginRequest.TryParse(retailFrame, out var retailRequest), "Retail 7.60 character-login packet was not parsed.");
    Assert(retailRequest!.Slot == 3 && retailRequest.Force == 0, "Parsed retail character-login fields differ.");

    var wrongTypeFrame = codec.Decode(codec.Encode(0x9999, 4, 33, payload, 16));
    Assert(!CharacterLoginRequest.TryParse(wrongTypeFrame, out _), "A frame with the wrong message type was accepted.");

}

static void CharacterLoginConfirmationWireLayout()
{
    var mob = new byte[816];
    System.Text.Encoding.ASCII.GetBytes("HERO").CopyTo(mob, 0);
    var shortSkill = Enumerable.Repeat((byte)0xFF, 16).ToArray();
    var affect = new byte[256];
    var mobExtra = new byte[552];
    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(mobExtra, 2); // ClassMaster == MORTAL

    var confirmation = new CharacterLoginConfirmation(2, mob, shortSkill, affect, mobExtra, 77, posX: 111, posY: 222, clientId: 9, weather: 3);
    var payload = confirmation.ToPayload();
    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(confirmation.ToFrame(codec, 1234, 7));

    Assert(payload.Length == CharacterLoginConfirmation.PayloadSize && CharacterLoginConfirmation.PacketSize == 2648, "Character login confirmation size differs from the legacy MSG_CNFCharacterLogin.");
    Assert(System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(payload) == 111 && System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(2)) == 222, "PosX/PosY offset differs.");
    Assert(System.Text.Encoding.ASCII.GetString(payload, 4, 4) == "HERO", "Mob offset differs.");
    Assert(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(1028)) == 2, "Slot offset differs.");
    Assert(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(1030)) == 9, "ClientID offset differs.");
    Assert(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(1032)) == 3, "Weather offset differs.");
    Assert(payload.AsSpan(1034, 16).ToArray().All(static value => value == 0xFF), "ShortSkill offset differs.");
    Assert(System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(2076)) == 2, "MobExtra offset differs.");
    Assert(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(2628)) == 77, "Donate offset differs.");
    Assert(frame.IsChecksumValid && frame.Header.Type == CharacterLoginConfirmation.MessageType && frame.Header.Id == CharacterLoginConfirmation.SceneId, "Confirmation frame header differs.");
}

static void CharacterLoginCoordinatorReadsPersistedCharacter()
{
    var root = Path.Combine(Path.GetTempPath(), $"wyd-cdk-charlogin-test-{Guid.NewGuid():N}");
    var accountRoot = Path.Combine(root, "account");
    var baseMobRoot = Path.Combine(root, "BaseMob");
    try
    {
        Directory.CreateDirectory(Path.Combine(accountRoot, "S"));
        Directory.CreateDirectory(baseMobRoot);
        File.WriteAllBytes(Path.Combine(accountRoot, "S", "SANDBOX"), new byte[LegacyAccountSnapshot.RequiredFileLength]);
        var template = new byte[LegacyAccountSnapshot.CharacterStride];
        System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(template.AsSpan(LegacyAccountSnapshot.MobSavedPositionXOffset), 2100); // class template's default SPX/SPY
        System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(template.AsSpan(LegacyAccountSnapshot.MobSavedPositionYOffset), 2100);
        File.WriteAllBytes(Path.Combine(baseMobRoot, "TK"), template);

        var accounts = new LegacyFileAccountStore(accountRoot);
        var createOutcome = new CreateCharacterCoordinator(accounts, new LegacyCharacterTemplateStore(baseMobRoot)).HandleAsync("SANDBOX", new CreateCharacterRequest(2, "HERO", 0), secureVerified: true).GetAwaiter().GetResult();
        Assert(createOutcome.IsSuccess, $"Fixture character creation failed: {createOutcome.Status}.");

        var loginOutcome = new CharacterLoginCoordinator(accounts).HandleAsync("SANDBOX", new CharacterLoginRequest(2, 0), secureVerified: true).GetAwaiter().GetResult();
        Assert(loginOutcome.IsSuccess, $"Character login failed unexpectedly: {loginOutcome.Status}.");
        var data = loginOutcome.Data!;
        Assert(System.Text.Encoding.ASCII.GetString(data.Mob, 0, 4) == "HERO", "Logged-in character's mob bytes do not carry the created name.");
        var killMark = LegacyItem.Read(data.Mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (LegacyAccountSnapshot.LegacyKillMarkCarrySlot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        Assert(killMark.Index == 547 && killMark.Effect1 == 75 && killMark.Effect2 == 76 && killMark.Effect3 == 77, "Character login did not initialize the legacy kill-mark item and neutral PK value.");
        Assert(data.ShortSkill.All(static value => value == 0xFF), "Logged-in character's ShortSkill was not the freshly reset value.");
        Assert(data.Affect.All(static value => value == 0), "Logged-in character's affect was not the freshly reset value.");
        Assert(System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(data.MobExtra) == LegacyAccountSnapshot.ClassMasterMortal, "Logged-in character's ClassMaster differs from what creation wrote.");
        Assert(data.Donate == 0, "Donate differs from the fixture's zeroed account file.");
        Assert(data.SavedPositionX == 2100 && data.SavedPositionY == 2100, "Logged-in character's saved position does not carry the template's SPX/SPY through creation into login.");
        Assert(accounts.ReadDonateAsync("SANDBOX").GetAwaiter().GetResult() == 0, "The file account store did not read the account-wide Donate field through the shared store contract.");
        Assert(accounts.TrySaveDonateAsync("SANDBOX", 125).GetAwaiter().GetResult() == DonateBalanceSaveResult.Success, "The file account store did not persist the account-wide Donate field.");
        Assert(accounts.TrySaveDonateAsync("SANDBOX", 200, expectedDonate: 0).GetAwaiter().GetResult() == DonateBalanceSaveResult.Conflict, "The file account store ignored an optimistic Donate conflict.");
        Assert(accounts.ReadDonateAsync("SANDBOX").GetAwaiter().GetResult() == 125, "The persisted Donate balance was not readable through the shared store contract.");

        Assert(accounts.TrySaveCharacterPositionAsync("SANDBOX", 2, 2200, 2210).GetAwaiter().GetResult() == CharacterPositionSaveResult.Success, "Character position was not persisted on logout.");
        var relogin = new CharacterLoginCoordinator(accounts).HandleAsync("SANDBOX", new CharacterLoginRequest(2, 0), secureVerified: true).GetAwaiter().GetResult();
        Assert(relogin.IsSuccess && relogin.Data!.SavedPositionX == 2200 && relogin.Data.SavedPositionY == 2210, "Persisted character position was not restored on the next login.");

        var reloginData = relogin.Data!;
        var mutableMob = reloginData.Mob.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(mutableMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), 4321);
        BinaryPrimitives.WriteInt32LittleEndian(mutableMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), 17);
        BinaryPrimitives.WriteInt32LittleEndian(mutableMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 28), 19);
        new LegacyItem(3463, 61, 1, 0, 0, 0, 0).Write(mutableMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
        var mutableExtra = reloginData.MobExtra.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(mutableExtra.AsSpan(LegacyAccountSnapshot.MobExtraHoldOffset), 1234);
        Assert(accounts.TrySaveCharacterStateAsync("SANDBOX", 2, mutableMob, 2230, 2240, mobExtra: mutableExtra).GetAwaiter().GetResult() == CharacterStateSaveResult.Success, "Full character state and MOBEXTRA were not persisted.");
        var stateRelogin = new CharacterLoginCoordinator(accounts).HandleAsync("SANDBOX", new CharacterLoginRequest(2, 0), secureVerified: true).GetAwaiter().GetResult();
        Assert(stateRelogin.IsSuccess && stateRelogin.Data!.SavedPositionX == 2230 && stateRelogin.Data.SavedPositionY == 2240 && BinaryPrimitives.ReadInt32LittleEndian(stateRelogin.Data.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24)) == 17 && BinaryPrimitives.ReadInt32LittleEndian(stateRelogin.Data.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 28)) == 19 && LegacyItem.Read(stateRelogin.Data.Mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes)).Index == 3463 && BinaryPrimitives.ReadUInt32LittleEndian(stateRelogin.Data.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraHoldOffset)) == 1234, "Full character state did not restore position, HP/MP, Carry, and MOBEXTRA hold on the next login.");
        var persistedSnapshot = accounts.ReadSnapshotAsync("SANDBOX").GetAwaiter().GetResult();
        Assert(persistedSnapshot is not null && persistedSnapshot.Coin == 4321, "Full character state did not mirror the MOB Coin into the account-wide selection Coin field.");

        var unverified = new CharacterLoginCoordinator(accounts).HandleAsync("SANDBOX", new CharacterLoginRequest(2, 0), secureVerified: false).GetAwaiter().GetResult();
        Assert(unverified.Status == CharacterLoginStatus.SecureNotVerified, "Character login was accepted without a verified PIN.");
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}

static void CharacterLoginCoordinatorRejectsInvalidRequests()
{
    var root = Path.Combine(Path.GetTempPath(), $"wyd-cdk-charlogin-invalid-test-{Guid.NewGuid():N}");
    var accountRoot = Path.Combine(root, "account");
    try
    {
        Directory.CreateDirectory(Path.Combine(accountRoot, "S"));
        File.WriteAllBytes(Path.Combine(accountRoot, "S", "SANDBOX"), new byte[LegacyAccountSnapshot.RequiredFileLength]); // every slot empty

        var coordinator = new CharacterLoginCoordinator(new LegacyFileAccountStore(accountRoot));

        Assert(coordinator.HandleAsync("SANDBOX", new CharacterLoginRequest(-1, 0), secureVerified: true).GetAwaiter().GetResult().Status == CharacterLoginStatus.SlotOutOfRange, "Negative slot was accepted.");
        Assert(coordinator.HandleAsync("SANDBOX", new CharacterLoginRequest(4, 0), secureVerified: true).GetAwaiter().GetResult().Status == CharacterLoginStatus.SlotOutOfRange, "Out-of-range slot was accepted.");
        Assert(coordinator.HandleAsync("SANDBOX", new CharacterLoginRequest(0, 0), secureVerified: true).GetAwaiter().GetResult().Status == CharacterLoginStatus.NotAvailable, "Login into an empty slot was accepted.");
        Assert(coordinator.HandleAsync("UNKNOWN", new CharacterLoginRequest(0, 0), secureVerified: true).GetAwaiter().GetResult().Status == CharacterLoginStatus.NotAvailable, "Login on an unknown account was accepted.");
        Assert(coordinator.HandleAsync("SANDBOX", new CharacterLoginRequest(0, 0), secureVerified: false).GetAwaiter().GetResult().Status == CharacterLoginStatus.SecureNotVerified, "Character login was accepted without a verified PIN.");
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}

static void AccountSecureParser()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var payload = new byte[20];
    System.Text.Encoding.ASCII.GetBytes("123456").CopyTo(payload, 0);
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(16), 1);
    var frame = codec.Decode(codec.Encode(AccountSecureRequest.MessageType, 4, 33, payload, 16));

    Assert(AccountSecureRequest.TryParse(frame, out var request), "Valid account-secure packet was not parsed.");
    Assert(request!.NumericToken == "123456" && request.ChangeNumeric, "Parsed account-secure fields differ.");

    var wrongTypeFrame = codec.Decode(codec.Encode(0x9999, 4, 33, payload, 16));
    Assert(!AccountSecureRequest.TryParse(wrongTypeFrame, out _), "A frame with the wrong message type was accepted.");
}

static void AccountSecureSignalWireLayout()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var success = codec.Decode(AccountSecureSignal.Success(codec, 1234, 7));
    var fail = codec.Decode(AccountSecureSignal.Fail(codec, 1234, 7));

    Assert(success.IsChecksumValid && success.Header.Type == AccountSecureSignal.SuccessType && success.Header.Id == AccountSecureSignal.SceneId && success.Payload.Length == 0, "Account-secure success signal differs from the legacy empty-payload signal.");
    Assert(fail.IsChecksumValid && fail.Header.Type == AccountSecureSignal.FailType && fail.Header.Id == AccountSecureSignal.SceneId && fail.Payload.Length == 0, "Account-secure fail signal differs from the legacy empty-payload signal.");
    Assert(AccountSecureSignal.SuccessType == AccountSecureRequest.MessageType, "Success signal does not reuse _MSG_AccountSecure's own type, unlike the reference server.");
}

static void AccountSecureCoordinatorFirstTimeSetup()
{
    var root = Path.Combine(Path.GetTempPath(), $"wyd-cdk-secure-setup-test-{Guid.NewGuid():N}");
    var accountRoot = Path.Combine(root, "account");
    try
    {
        Directory.CreateDirectory(Path.Combine(accountRoot, "S"));
        var file = new byte[LegacyAccountSnapshot.RequiredFileLength];
        file[LegacyAccountSnapshot.NumericTokenOffset] = 0xFF; // never set
        File.WriteAllBytes(Path.Combine(accountRoot, "S", "SANDBOX"), file);

        var sessions = new LoginSessionRegistry();
        sessions.Open(1);
        var accounts = new LegacyFileAccountStore(accountRoot);
        var coordinator = new AccountSecureCoordinator(sessions, accounts);

        var outcome = coordinator.HandleAsync(1, "SANDBOX", new AccountSecureRequest("135790", ChangeNumeric: false)).GetAwaiter().GetResult();
        Assert(outcome.IsSuccess, $"First-time PIN setup failed unexpectedly: {outcome.Status}.");
        Assert(sessions.TryGet(1, out var session) && session!.SecureVerified, "Session was not marked verified after first-time PIN setup.");

        var stored = File.ReadAllBytes(Path.Combine(accountRoot, "S", "SANDBOX"));
        Assert(System.Text.Encoding.ASCII.GetString(stored, LegacyAccountSnapshot.NumericTokenOffset, 6) == "135790", "First-time PIN setup did not persist the submitted token.");
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}

static void AccountSecureCoordinatorVerifiesAndChangesPin()
{
    var root = Path.Combine(Path.GetTempPath(), $"wyd-cdk-secure-verify-test-{Guid.NewGuid():N}");
    var accountRoot = Path.Combine(root, "account");
    try
    {
        Directory.CreateDirectory(Path.Combine(accountRoot, "S"));
        var file = new byte[LegacyAccountSnapshot.RequiredFileLength];
        System.Text.Encoding.ASCII.GetBytes("111111").CopyTo(file, LegacyAccountSnapshot.NumericTokenOffset);
        File.WriteAllBytes(Path.Combine(accountRoot, "S", "SANDBOX"), file);

        var sessions = new LoginSessionRegistry();
        sessions.Open(1);
        var accounts = new LegacyFileAccountStore(accountRoot);
        var coordinator = new AccountSecureCoordinator(sessions, accounts);

        var verify = coordinator.HandleAsync(1, "SANDBOX", new AccountSecureRequest("111111", ChangeNumeric: false)).GetAwaiter().GetResult();
        Assert(verify.IsSuccess, $"PIN verification failed unexpectedly: {verify.Status}.");
        Assert(sessions.TryGet(1, out var verifiedSession) && verifiedSession!.SecureVerified, "Session was not marked verified after a correct PIN.");

        var change = coordinator.HandleAsync(1, "SANDBOX", new AccountSecureRequest("222222", ChangeNumeric: true)).GetAwaiter().GetResult();
        Assert(change.IsSuccess, $"PIN change failed unexpectedly: {change.Status}.");

        var stored = File.ReadAllBytes(Path.Combine(accountRoot, "S", "SANDBOX"));
        Assert(System.Text.Encoding.ASCII.GetString(stored, LegacyAccountSnapshot.NumericTokenOffset, 6) == "222222", "PIN change did not persist the new token.");
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}

static void AccountSecureCoordinatorRejectsWrongPinAndUnverifiedChange()
{
    var root = Path.Combine(Path.GetTempPath(), $"wyd-cdk-secure-reject-test-{Guid.NewGuid():N}");
    var accountRoot = Path.Combine(root, "account");
    try
    {
        Directory.CreateDirectory(Path.Combine(accountRoot, "S"));
        var file = new byte[LegacyAccountSnapshot.RequiredFileLength];
        System.Text.Encoding.ASCII.GetBytes("111111").CopyTo(file, LegacyAccountSnapshot.NumericTokenOffset);
        File.WriteAllBytes(Path.Combine(accountRoot, "S", "SANDBOX"), file);

        var sessions = new LoginSessionRegistry();
        sessions.Open(1);
        var coordinator = new AccountSecureCoordinator(sessions, new LegacyFileAccountStore(accountRoot));

        var changeWithoutVerification = coordinator.HandleAsync(1, "SANDBOX", new AccountSecureRequest("222222", ChangeNumeric: true)).GetAwaiter().GetResult();
        Assert(changeWithoutVerification.Status == AccountSecureStatus.ChangeWithoutVerification, "A PIN change was accepted without prior verification.");
        Assert(sessions.TryGet(1, out var stillUnverified) && !stillUnverified!.SecureVerified, "An unverified-change attempt marked the session verified.");

        var wrongPin = coordinator.HandleAsync(1, "SANDBOX", new AccountSecureRequest("999999", ChangeNumeric: false)).GetAwaiter().GetResult();
        Assert(wrongPin.Status == AccountSecureStatus.Fail, "A wrong PIN was accepted as verification.");
        Assert(sessions.TryGet(1, out var stillUnverifiedAfterFail) && !stillUnverifiedAfterFail!.SecureVerified, "A failed verification left the session marked verified.");

        var stored = File.ReadAllBytes(Path.Combine(accountRoot, "S", "SANDBOX"));
        Assert(System.Text.Encoding.ASCII.GetString(stored, LegacyAccountSnapshot.NumericTokenOffset, 6) == "111111", "A rejected request modified the stored PIN.");
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}

static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

sealed class FixedAccountStore(AccountAuthenticationResult result) : IAccountStore
{
    public ValueTask<AccountAuthenticationResult> AuthenticateAsync(AccountLoginRequest request, CancellationToken cancellationToken = default) => ValueTask.FromResult(result);
}

sealed class FixedCharacterLoginDataStore(string accountName, int slot, LegacyCharacterLoginData data) : ILegacyCharacterLoginDataStore
{
    public ValueTask<LegacyCharacterLoginData?> ReadCharacterLoginDataAsync(string requestedAccountName, int requestedSlot, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<LegacyCharacterLoginData?>(
            string.Equals(requestedAccountName, accountName, StringComparison.OrdinalIgnoreCase) && requestedSlot == slot
                ? data
                : null);
}

sealed class FixedAccountSnapshotStore(string accountName, LegacyAccountSnapshot snapshot) : IAccountSnapshotStore
{
    public ValueTask<LegacyAccountSnapshot?> ReadSnapshotAsync(string requestedAccountName, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<LegacyAccountSnapshot?>(
            string.Equals(requestedAccountName, accountName, StringComparison.OrdinalIgnoreCase)
                ? snapshot
                : null);
}

sealed class FixedAutoTradePurchaseCommitStore(LegacyAutoTradePurchaseCommitResult result) : ILegacyAutoTradePurchaseCommitStore
{
    public LegacyAutoTradePurchaseCommitRequest? LastRequest { get; private set; }

    public ValueTask<LegacyAutoTradePurchaseCommitResult> TryCommitAsync(
        LegacyAutoTradePurchaseCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return ValueTask.FromResult(result);
    }
}

static class TestAssemblyMarker
{
}

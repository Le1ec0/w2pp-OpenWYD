using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

var tests = new (string Name, Action Run)[] { ("header round-trip", HeaderRoundTrip), ("frame round-trip with real legacy table", FrameRoundTrip), ("stream accepts partial and concatenated frames", StreamFraming), ("checksum detects incompatible key table", ChecksumDetectsIncompatibleKeyTable), ("account login parser", AccountLoginParser), ("create character parser", CreateCharacterParser), ("delete character wire", DeleteCharacterWire), ("character logout wire", CharacterLogoutWire), ("login session transitions", LoginSessionTransitions), ("legacy account store is read-only", LegacyAccountStoreIsReadOnly), ("account login coordinator", AccountLoginCoordinatorFlow), ("character selection wire layout", CharacterSelectionWireLayout), ("account login confirmation wire layout", AccountLoginConfirmationWireLayout), ("new character confirmation wire layout", NewCharacterConfirmationWireLayout), ("legacy account snapshot reads fixture", LegacyAccountSnapshotReadsFixture), ("legacy character name validation", LegacyCharacterNameValidation), ("legacy character template store reads class files", LegacyCharacterTemplateStoreReadsClassFiles), ("create character coordinator persists character", CreateCharacterCoordinatorPersistsCharacter), ("create character coordinator rejects invalid requests", CreateCharacterCoordinatorRejectsInvalidRequests), ("character login parser", CharacterLoginParser), ("character login confirmation wire layout", CharacterLoginConfirmationWireLayout), ("character login coordinator reads persisted character", CharacterLoginCoordinatorReadsPersistedCharacter), ("character login coordinator rejects invalid requests", CharacterLoginCoordinatorRejectsInvalidRequests), ("account secure parser", AccountSecureParser), ("account secure signal wire layout", AccountSecureSignalWireLayout), ("account secure coordinator first time setup", AccountSecureCoordinatorFirstTimeSetup), ("account secure coordinator verifies and changes pin", AccountSecureCoordinatorVerifiesAndChangesPin), ("account secure coordinator rejects wrong pin and unverified change", AccountSecureCoordinatorRejectsWrongPinAndUnverifiedChange) };
tests = tests.Append(("client tick policy", ClientTickPolicyRules)).Append(("attack timing gate", AttackTimingGate)).Append(("attack parser", AttackParser)).Append(("attack response wire", AttackResponseWire)).Append(("attack authoritative pipeline", AttackAuthoritativePipeline)).Append(("set hp mp wire", SetHpMpWire)).Append(("send item wire", SendItemWire)).Append(("physical combat formula", PhysicalCombatFormula)).Append(("skill combat formula", SkillCombatFormula)).Append(("experience formula", ExperienceFormula)).Append(("weapon damage formula", WeaponDamageFormula)).Append(("item data table", ItemDataTable)).Append(("item bonus math", ItemBonusMath)).Append(("item refinement bonus math", ItemRefinementBonusMath)).Append(("use item wire and class reset", UseItemWireAndClassReset)).Append(("world physical attack", WorldPhysicalAttack)).Append(("world npc experience", WorldNpcExperience)).Append(("world npc skill", WorldNpcSkill)).Append(("world npc party experience", WorldNpcPartyExperience)).Append(("world experience hold", WorldExperienceHold)).Append(("party experience modifiers", PartyExperienceModifiers)).Append(("experience bonus", ExperienceBonus)).Append(("world skill attack", WorldSkillAttack)).Append(("world summon skill", WorldSummonSkill)).Append(("world npc summon", WorldNpcSummon)).Append(("world ethereal flames", WorldEtherealFlames)).Append(("skill data table", SkillDataTable)).Append(("skill attack gate", SkillAttackGate)).Append(("legacy mob combat layout", LegacyMobCombatLayout)).Append(("legacy map grid attributes", LegacyMapGridAttributes)).Append(("legacy release map recall", LegacyReleaseMapRecall)).Append(("legacy guild zone state", LegacyGuildZoneStateFile)).Append(("short skill wire and persistence", ShortSkillWireAndPersistence)).Append(("action parser", ActionParser)).Append(("motion wire", MotionWire)).Append(("world hub broadcast", WorldHubBroadcast)).Append(("world skill mana mutation", WorldSkillManaMutation)).Append(("world guild invite", WorldGuildInvite)).Append(("guild invite visual update", GuildInviteVisualUpdate)).Append(("create mob wire", CreateMobWire)).Append(("initial world state wire", InitialWorldStateWire)).Append(("guild invite wire", GuildInviteWire)).Append(("message panel wire", MessagePanelWire)).Append(("npc chat wire", NpcChatWire)).Append(("donate shop wire", DonateShopWire)).Append(("donate shop client state", DonateShopClientStateFlow)).Append(("donate shop catalog and purchase", DonateShopCatalogAndPurchase)).ToArray();
tests = tests.Append(("party wire", PartyWire)).Append(("world party lifecycle", WorldPartyLifecycle)).ToArray();
tests = tests.Append(("world npc common drop", WorldNpcCommonDrop)).Append(("world npc boss drop", WorldNpcBossDrop)).Append(("castle quest configuration", CastleQuestConfiguration)).Append(("castle quest rewards", CastleQuestRewards)).Append(("npc generation catalog", NpcGenerationCatalogFlow)).Append(("pista mob-left wire and area", PistaMobLeftWireAndArea)).Append(("quest request parser", QuestRequestParser)).Append(("pista registration flow", PistaRegistrationFlow)).Append(("pista entry schedule", PistaEntrySchedule)).Append(("pista fixed entry spawns", PistaFixedEntrySpawns)).Append(("pista entry generator matrix", PistaEntryGeneratorMatrix)).Append(("pista level 0 retry", PistaLevel0Retry)).Append(("pista level 2 reward", PistaLevel2Reward)).Append(("pista level 1 counters", PistaLevel1Counters)).Append(("pista level 1 exit reward", PistaLevel1ExitReward)).Append(("pista level 4 progression", PistaLevel4Progression)).Append(("pista level 3 progression", PistaLevel3Progression)).Append(("pista level 5 progression", PistaLevel5Progression)).ToArray();
tests = tests.Append(("world invisibility", WorldInvisibility)).Append(("server mode policy", ServerModePolicyFlow)).Append(("donate shop NPC target", DonateShopNpcTarget)).Append(("city Perzen NPCs", CityPerzenNpcs)).Append(("Perzen exchange", PerzenExchange)).Append(("donate shop rate limiter", DonateShopRateLimiter)).Append(("donate shop purchase messages", DonateShopPurchaseMessages)).Append(("account login failure notice", AccountLoginFailureNoticeFlow)).ToArray();
foreach (var test in tests) { test.Run(); Console.WriteLine($"PASS {test.Name}"); }

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
        var candidate = Path.Combine(directory.FullName, "Tools", "Reference759", "SERVER", "TMSrv", "run");
        if (Directory.Exists(candidate)) return candidate;
    }

    throw new DirectoryNotFoundException("Tools\\Reference759\\SERVER\\TMSrv\\run was not found from the test output directory.");
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
    Assert(ActionRequest.TryParse(stop, out _), "MSG_Action2 was not accepted by the action parser.");

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
    Assert(sessions.Open(5) && sessions.BeginAccountLogin(5, login) == LoginTransitionResult.Accepted && sessions.CompleteAccountLogin(5, "FIGHTER") == LoginTransitionResult.Accepted && sessions.CompleteCharacterLogin(5) == LoginTransitionResult.Accepted, "Attack timing fixture did not enter USER_PLAY.");
    Assert(sessions.TryAcceptAttackTiming(5, 1_000_000, 1_000_000) == AttackTimingResult.Accepted, "First valid attack timestamp was rejected.");
    Assert(sessions.TryAcceptAttackTiming(5, 1_000_799, 1_000_799) == AttackTimingResult.TooSoon, "Attack inside the 800ms legacy limit was accepted.");
    Assert(sessions.TryAcceptAttackTiming(5, 1_000_800, 1_000_800) == AttackTimingResult.Accepted, "Attack exactly at the 800ms legacy limit was rejected.");
    Assert(sessions.TryAcceptAttackTiming(5, ClientTickPolicy.SkipCheckTick, 1_000_800) == AttackTimingResult.ReservedTimestamp, "Reserved attack timestamp was accepted.");
    Assert(sessions.TryAcceptAttackTiming(5, 1_136_000, 1_120_999) == AttackTimingResult.OutsideServerWindow, "Attack more than 15s ahead of server time was accepted.");

    var oldTimestampSession = new LoginSessionRegistry();
    Assert(oldTimestampSession.Open(6) && oldTimestampSession.BeginAccountLogin(6, login) == LoginTransitionResult.Accepted && oldTimestampSession.CompleteAccountLogin(6, "FIGHTER") == LoginTransitionResult.Accepted && oldTimestampSession.CompleteCharacterLogin(6) == LoginTransitionResult.Accepted, "Old-timestamp fixture did not enter USER_PLAY.");
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
    Assert(table.Count == LegacyItemDataTable.MaxItemIndex && table[1] is { Name: "FIRST", Unique: 45, Position: 64 }, "ItemList.bin record layout was not decoded.");

    var first = new LegacyItem(1, 116, 230, 0, 0, 0, 0);
    Assert(table.GetItemSanctuary(first) == 10 && table.GetItemAbility(first, LegacyItemEffect.Damage) == 200 && table.GetItemAbility(first, LegacyItemEffect.Magic) == 60, "Static item effects or sanctuary scaling differ from BASE_GetItemAbility.");

    var alternate = new LegacyItem(3, 43, 230, LegacyItemEffect.Damage2, 4, 0, 0);
    Assert(table.GetItemAbility(alternate, LegacyItemEffect.Damage) == 22 && table.GetItemPosition(alternate) == 32, "Position-specific EF_DAMAGE2 selection differs from the legacy item resolver.");

    var state = new LegacyMobCombatState(0, 1u << 9, 0, default, 0);
    var second = new LegacyItem(2, 43, 9, 0, 0, 0, 0);
    Assert(LegacySkillCombatMath.GetWeaponDamage(state, table, first, second) == 356, "WeaponDamage did not consume decoded item abilities and sanctuary values.");
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
    WriteItemListRecord(decodedBody, 2, "SECOND", unique: 48, position: 192, grade: 3, (LegacyItemEffect.Damage, 40));
    WriteItemListRecord(decodedBody, 3, "ALT", unique: 48, position: 32, grade: 1, (LegacyItemEffect.Damage2, 7));
    WriteItemListRecord(decodedBody, 4, "PARRY", unique: 0, position: 0, grade: 0, (LegacyItemEffect.Parry, 100));
    WriteItemListRecord(decodedBody, 5, "XP GRADE", unique: 0, position: 0, grade: 7);
    WriteItemListRecord(decodedBody, 6, "BOOT CAP", unique: 0, position: 32, grade: 0, (LegacyItemEffect.Damage, 100));
    WriteItemListRecord(decodedBody, 14, "FORCE GEM", unique: 0, position: 0, grade: 6);
    WriteItemListRecord(decodedBody, 15, "PVP ATTACK", unique: 0, position: 0, grade: 0, (LegacyItemEffect.PvpAttack, 50));
    WriteItemListRecord(decodedBody, 16, "REFLECT", unique: 0, position: 0, grade: 8, (LegacyItemEffect.PvpDefense, 50));
    WriteItemListRecord(decodedBody, 17, "REFLECT GEM", unique: 0, position: 0, grade: 8);
    WriteItemListRecord(decodedBody, 419, "BOSS DROP", unique: 0, position: 0, grade: 0);
    WriteItemListRecord(decodedBody, 7, "RESET ARMOR", unique: 0, position: 4, grade: 0, (LegacyItemEffect.MobType, 0));
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

static void WriteItemListRecord(byte[] body, int index, string name, short unique, short position, short grade, params (int Effect, int Value)[] effects)
{
    var offset = index * LegacyItemDataTable.RecordSizeInBytes;
    System.Text.Encoding.ASCII.GetBytes(name).CopyTo(body, offset);
    BinaryPrimitives.WriteInt16LittleEndian(body.AsSpan(offset + 132), unique);
    BinaryPrimitives.WriteInt16LittleEndian(body.AsSpan(offset + 134), position);
    BinaryPrimitives.WriteInt16LittleEndian(body.AsSpan(offset + 138), grade);
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

static void WriteItemListExtra(byte[] body, int index, short extra)
{
    BinaryPrimitives.WriteInt16LittleEndian(body.AsSpan(index * LegacyItemDataTable.RecordSizeInBytes + 136), extra);
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

    var adultMountCatalog = LegacySummonCatalog.Load(Path.Combine(FindReference759TmsrvRun(), "BaseSummon"));
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
    var run = FindReference759TmsrvRun();
    var catalog = LegacySummonCatalog.Load(Path.Combine(run, "BaseSummon"));
    Assert(catalog.TryGet(0, out var condor) && condor is not null && condor.MobSnapshot.Length == LegacyAccountSnapshot.CharacterStride, "The real BaseSummon catalog did not load slot 0.");
    Assert(!catalog.TryGet(40, out _), "An uninitialized legacy summon slot was treated as a valid template.");

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
    Assert(hub.TryPrepareGuildInvite(1, 2, 1, out var plan) == GuildInviteCheckResult.Accepted && plan is not null && hub.ApplyGuildInvite(plan), "Guild visual update could not be applied.");

    var codec = LegacyFrameCodec.CreateDefault();
    Assert(hub.TryBuildCreateMobFrame(2, codec, 33, 16, out var createFrame) && createFrame is not null, "Guild member CreateMob refresh was not built.");
    var create = codec.Decode(createFrame!);
    Assert(System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(create.Payload.Span) == 2100 && System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(create.Payload.Span[2..]) == 2200 && System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(create.Payload.Span[118..]) == 77, "Guild member visual refresh has incorrect position or guild.");
    Assert(hub.TryBuildUpdateEtcFrame(1, codec, 33, 17, out var etcFrame) && etcFrame is not null, "Guild leader UpdateEtc refresh was not built.");
    var etc = codec.Decode(etcFrame!);
    Assert(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(etc.Payload.Span[36..]) == 0, "Guild leader visual refresh did not update coin.");
}

static void CreateMobWire()
{
    var mob = new byte[816];
    System.Text.Encoding.ASCII.GetBytes("HERO").CopyTo(mob, 0);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(18), 7);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(140), 1103);
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(92), 42);
    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(new CreateMobConfirmation(9, 2096, 2096, mob).ToFrame(codec, 33, 16));
    Assert(frame.IsChecksumValid && frame.Header.Type == CreateMobConfirmation.MessageType && frame.Header.Size == CreateMobConfirmation.PacketSize, "Create-mob frame header differs from the legacy wire.");
    Assert(System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span) == 2096 && System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[4..]) == 9, "Create-mob position or id was misplaced.");
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

static void NpcChatWire()
{
    var codec = LegacyFrameCodec.CreateDefault();
    var frame = codec.Decode(new NpcChatConfirmation("Traga Esfera_da_Sorte.").ToFrame(codec, 34, 17, 20_601));
    Assert(frame.IsChecksumValid && frame.Header.Type == NpcChatConfirmation.MessageType && frame.Header.Size == NpcChatConfirmation.PacketSize && frame.Header.Id == 20_601, "NPC chat frame header differs from MSG_MessageChat.");
    Assert(System.Text.Encoding.ASCII.GetString(frame.Payload.Span[..23]).StartsWith("Traga Esfera_da_Sorte.", StringComparison.Ordinal), "NPC chat text was not encoded.");
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
    Assert(confirmation.IsChecksumValid && confirmation.Header.Type == DeleteCharacterConfirmation.MessageType && confirmation.Header.Id == DeleteCharacterConfirmation.SceneId && confirmation.Payload.Length == LegacyCharacterSelection.SizeInBytes, "Delete-character confirmation wire differs from the legacy message.");
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
    Assert(sessions.CompleteCharacterLogin(42, characterSlot: 0, positionX: 2000, positionY: 2000) == LoginTransitionResult.Accepted, "Character login did not enter USER_PLAY state.");
    Assert(sessions.TryGet(42, out session) && session!.State == LoginSessionState.Playing, "Character login state was not USER_PLAY.");
    var movement = new ActionRequest(2000, 2000, 0, 6, new byte[24], 2020, 2010);
    Assert(sessions.TryApplyMovement(42, movement) == MovementResult.Accepted, "A valid movement was rejected.");
    Assert(sessions.TryGet(42, out session) && session!.PositionX == 2020 && session.PositionY == 2010, "Accepted movement did not update the world position.");
    Assert(sessions.TryApplyMovement(42, movement with { PositionX = 1999, TargetX = 2030, TargetY = 2010 }) == MovementResult.Accepted, "A movement with a stale client-reported origin (not validated by the legacy TMSrv) was rejected.");
    Assert(sessions.TryGet(42, out session) && session!.PositionX == 2030 && session.PositionY == 2010, "Accepted movement with a stale origin did not update the world position from the target.");
    Assert(sessions.TryApplyMovement(42, movement with { PositionX = 2030, PositionY = 2010, TargetX = 2200, TargetY = 2010 }) == MovementResult.StepTooLarge, "An oversized step was accepted.");
    Assert(sessions.TryApplyMovement(42, movement with { PositionX = 2030, PositionY = 2010, TargetX = 4096 }) == MovementResult.OutOfBounds, "Out-of-bounds movement was accepted.");
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

    Assert(confirmation.ToPayload().Length == NewCharacterConfirmation.PayloadSize && NewCharacterConfirmation.PacketSize == 852, "New-character confirmation size differs from legacy message.");
    Assert(frame.IsChecksumValid && frame.Header.Type == NewCharacterConfirmation.MessageType && frame.Header.Id == NewCharacterConfirmation.SceneId, "New-character confirmation frame header differs.");
    Assert(System.Text.Encoding.ASCII.GetString(frame.Payload.Span.Slice(48, 4)) == "HERO", "New-character confirmation did not preserve the selection snapshot.");
}

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
    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(firstCharacter[42..], 9); // SPY (the only half of the SPX/SPY bug that survives)
    new LegacyScore(3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21).Write(firstCharacter.Slice(92, LegacyScore.SizeInBytes));
    new LegacyItem(42, 1, 2, 3, 4, 5, 6).Write(firstCharacter.Slice(140, LegacyItem.SizeInBytes));

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
    Assert(hero.SavedPositionX == 9 && hero.SavedPositionY == 0, "SPX/SPY legacy bug was not reproduced (SPY survives into SavedPositionX, SavedPositionY stays zero).");
    Assert(hero.Score.Level == 3 && hero.Equipment[0].Index == 42, "Character score or equipment offset differs from fixture.");
    Assert(snapshot.Characters.Slots[1].Equipment[0].Index == 21, "Mortal face substitution (ClassMaster == MORTAL) was not applied.");
    Assert(snapshot.Characters.Slots[2].Equipment[0].Index == 12, "Non-mortal face substitution (MortalFace + 7) was not applied.");
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
        Assert(deleted.IsSuccess && deleted.Snapshot!.Characters.Slots[1].Name.Length == 0, "Character deletion did not clear the persisted slot.");
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
        BinaryPrimitives.WriteInt32LittleEndian(mutableMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), 17);
        BinaryPrimitives.WriteInt32LittleEndian(mutableMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 28), 19);
        new LegacyItem(3463, 61, 1, 0, 0, 0, 0).Write(mutableMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset));
        var mutableExtra = reloginData.MobExtra.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(mutableExtra.AsSpan(LegacyAccountSnapshot.MobExtraHoldOffset), 1234);
        Assert(accounts.TrySaveCharacterStateAsync("SANDBOX", 2, mutableMob, 2230, 2240, mobExtra: mutableExtra).GetAwaiter().GetResult() == CharacterStateSaveResult.Success, "Full character state and MOBEXTRA were not persisted.");
        var stateRelogin = new CharacterLoginCoordinator(accounts).HandleAsync("SANDBOX", new CharacterLoginRequest(2, 0), secureVerified: true).GetAwaiter().GetResult();
        Assert(stateRelogin.IsSuccess && stateRelogin.Data!.SavedPositionX == 2230 && stateRelogin.Data.SavedPositionY == 2240 && BinaryPrimitives.ReadInt32LittleEndian(stateRelogin.Data.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24)) == 17 && BinaryPrimitives.ReadInt32LittleEndian(stateRelogin.Data.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 28)) == 19 && LegacyItem.Read(stateRelogin.Data.Mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes)).Index == 3463 && BinaryPrimitives.ReadUInt32LittleEndian(stateRelogin.Data.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraHoldOffset)) == 1234, "Full character state did not restore position, HP/MP, Carry, and MOBEXTRA hold on the next login.");

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

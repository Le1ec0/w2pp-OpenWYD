using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>Shared in-memory presence for the connected players in the current world instance.</summary>
public sealed partial class WorldHub
{
    // Pinned TMProject 7.69 default groupItens.json (Servidor/Server/Common/Settings).
    // The legacy server loads these IDs from configuration and uses the same list
    // for TradingItem merges and SplitItem eligibility.
    private static readonly HashSet<short> legacyStackableItemIndices =
    [
        412, 413, 419, 420,
        777,
        2390, 2391, 2392, 2393, 2394, 2395, 2396, 2397, 2398, 2399,
        2400, 2401, 2402, 2403, 2404, 2405, 2406, 2407, 2408, 2409,
        2410, 2411, 2412, 2413, 2414, 2415, 2416, 2417, 2418, 2419,
        2426, 2427, 2428, 2429,
        3173, 3182,
        3201, 3202, 3203, 3204, 3205, 3206, 3207, 3208, 3209,
        3314, 3324, 3325, 3326, 3330,
        4016, 4017, 4018, 4019, 4020, 4021, 4022, 4023, 4024, 4025,
        4038, 4039, 4040, 4041, 4042,
        4117, 4118, 4119, 4120, 4121,
        4140,
        4900,
    ];

    private const int LegacyBaseCarryCapacity = 30;
    private const int PistaLv6DespawnInitialGenerateIndex = 4291;
    private const int PistaLv6DespawnEndGenerateIndex = 4301;
    private const int LegacyCarryExpansionItemIndex = 3467;
    private const int LegacyFirstCarryExpansionSlot = 60;
    private const int LegacySecondCarryExpansionSlot = 61;
    private const int LegacyCarryExpansionSize = 15;

    private readonly object gate = new();
    private readonly Dictionary<int, Participant> participants = [];
    private readonly Dictionary<int, int> partyLeaders = [];
    private readonly Dictionary<int, int> pendingPartyLeaders = [];
    private readonly Dictionary<int, LegacyWorldNpc> npcs = [];
    private readonly Dictionary<int, LegacyWorldItem> groundItems = [];
    private readonly Dictionary<int, LegacyMapItemState> mapItems = [];
    private readonly HashSet<int> battleRoyaleDoorRemovedMobGridIds = [];
    private readonly Dictionary<(int Level, int PartySlot), LegacyPistaRegistration> pistaRegistrations = [];
    private readonly Dictionary<(int Level, int PartySlot), int> pistaMobCounts = [];
    private static readonly int[] pistaLevel1Runes = [5114, 5113, 5117, 5111, 5115, 5112];
    private static readonly int[] pistaLevel3Runes = [5122, 5126, 5121, 5116, 5119];
    private DateTime? lastPistaEntrySlot;
    private DateTime? lastPistaExitSlot;
    private DateTime? lastPistaLv6MobCountAt;
    private int pistaLv6MobsLeft;
    private DateTime? lastMapItemMinute;
    private DateTime? lastGroundItemSecond;
    private DateTime? lastCastleQuestMinute;
    private DateTime? lastCastleQuestSecond;
    private readonly LegacyMapGrid? mapGrid;
    private readonly LegacyGuildZoneState? guildZones;
    private readonly LegacyMapCollisionMode mapCollisionMode;
    private readonly LegacySummonCatalog? summonCatalog;
    private readonly LegacyItemDataTable? itemData;
    private readonly LegacySkillDataTable? skillData;
    private readonly LegacyNpcGenerationCatalog? npcGenerationCatalog;
    private LegacyGroundMaskTable? groundMaskTable;
    private readonly LegacyServerModePolicy serverModePolicy;
    private LegacyDonateShopCatalog? donateShopCatalog;
    private LegacyNpcEventDropConfiguration? npcEventDrop;
    private int castleQuestLevel = -1;
    private int castleQuestTime = -1;
    private int castleQuestLeaderConnectionId;
    private readonly HashSet<int> castleQuestParty = [];
    private IReadOnlyList<LegacyCastleQuestDefinition> castleQuests =
        [new LegacyCastleQuestDefinition(1559, 1604, 0, 3, new LegacyItem[LegacyCastleQuestConfiguration.MaxCarry], 0, new int[6], false, 500)];
    private int npcEventCurrentIndex;
    private LegacyPistaLv6State? pistaLv6State;
    private LegacyCombatWorldState combatWorldState = new();
    private readonly Dictionary<int, LegacySummonedMob> summons = [];
    private readonly Dictionary<int, int> summonTargets = [];
    private byte summonCombatAffectOpportunityPhase;
    private int nextSummonConnectionId = 1000;
    private int nextNpcConnectionId = 20_000;

    public WorldHub(LegacyMapGrid? mapGrid = null, LegacyGuildZoneState? guildZones = null, LegacyMapCollisionMode mapCollisionMode = LegacyMapCollisionMode.LegacyCompatible, LegacySummonCatalog? summonCatalog = null, LegacyItemDataTable? itemData = null, LegacySkillDataTable? skillData = null, LegacyNpcGenerationCatalog? npcGenerationCatalog = null, LegacyDonateShopCatalog? donateShopCatalog = null, LegacyServerMode serverMode = LegacyServerMode.Up)
    {
        this.mapGrid = mapGrid;
        this.guildZones = guildZones;
        this.mapCollisionMode = mapCollisionMode;
        this.summonCatalog = summonCatalog;
        this.itemData = itemData;
        this.skillData = skillData;
        this.npcGenerationCatalog = npcGenerationCatalog;
        serverModePolicy = new LegacyServerModePolicy(serverMode);
        this.donateShopCatalog = donateShopCatalog is null ? null : serverModePolicy.Apply(donateShopCatalog);
    }

    public LegacyServerMode ServerMode => serverModePolicy.Mode;

    public string WorldKey => serverModePolicy.WorldKey;

    public bool IsPvp => serverModePolicy.IsPvp;

    public void ConfigureDonateShopCatalog(LegacyDonateShopCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        lock (gate)
            donateShopCatalog = serverModePolicy.Apply(catalog);
    }

    public bool TryGetDonateShopCatalog(out LegacyDonateShopCatalog? catalog)
    {
        lock (gate)
        {
            catalog = donateShopCatalog;
            return catalog is not null;
        }
    }

    public void ConfigureNpcEventDrop(LegacyNpcEventDropConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        lock (gate)
        {
            npcEventDrop = configuration;
            npcEventCurrentIndex = configuration.CurrentIndex;
        }
    }

    public void ConfigureCastleQuests(IReadOnlyList<LegacyCastleQuestDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if (definitions.Count > LegacyCastleQuestConfiguration.MaxQuestCount)
            throw new ArgumentOutOfRangeException(nameof(definitions), definitions.Count, $"Castle quest count cannot exceed {LegacyCastleQuestConfiguration.MaxQuestCount}.");
        if (definitions.Any(static definition => definition.Prizes.Count != LegacyCastleQuestConfiguration.MaxCarry || definition.ExpPrize.Count != 6))
            throw new ArgumentException("Every castle quest must contain MAX_CARRY prizes and six class EXP values.", nameof(definitions));

        lock (gate)
            castleQuests = definitions.ToArray();
    }

    /// <summary>
    /// Installs the static InitItem.bin entries in the authoritative world.
    /// The legacy server creates them in slots 1..N and immediately locks
    /// entries whose EF_KEYID is in the protected gate range.
    /// </summary>
    public void ConfigureMapItems(IReadOnlyList<LegacyMapItemState> definitions, LegacyGroundMaskTable? groundMasks = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if (definitions.Count > LegacyMapItemCatalog.MaxMapItems)
            throw new ArgumentOutOfRangeException(nameof(definitions), definitions.Count, $"Map item count cannot exceed {LegacyMapItemCatalog.MaxMapItems}.");

        lock (gate)
        {
            if (definitions.Any(definition => groundItems.ContainsKey(definition.ItemId)))
                throw new ArgumentException("Configured map-item IDs cannot overlap dynamic ground-item IDs.", nameof(definitions));

            mapItems.Clear();
            lastMapItemMinute = null;
            groundMaskTable = groundMasks ?? LegacyGroundMaskTable.CreateOfficial();
            foreach (var definition in definitions)
            {
                if (definition.ItemId <= 0 || definition.ItemId >= LegacyMapItemCatalog.MaxMapItems)
                    throw new ArgumentOutOfRangeException(nameof(definitions), definition.ItemId, "Map item IDs must match the legacy pItem slot range.");
                if (!mapItems.TryAdd(definition.ItemId, definition))
                    throw new ArgumentException($"Duplicate map item ID {definition.ItemId}.", nameof(definitions));

                var keyId = itemData?.GetItemAbility(definition.Item, LegacyItemEffect.KeyId) ?? 0;
                if (keyId is > 0 and < 15 && definition.State == LegacyMapItemStateCodes.Open)
                {
                    var height = definition.Height;
                    if (groundMaskTable is not null && mapGrid is not null)
                        groundMaskTable.TryApply(mapGrid, definition.GridCharge, definition.State, LegacyMapItemStateCodes.Locked, definition.PositionX, definition.PositionY, definition.Rotate, out height);
                    mapItems[definition.ItemId] = definition with
                    {
                        State = LegacyMapItemStateCodes.Locked,
                        Height = height,
                        Delay = 0,
                    };
                }
            }

        }
    }

    public bool TryGetMapItem(int wireItemId, out LegacyMapItemState? state)
    {
        lock (gate)
        {
            state = mapItems.TryGetValue(wireItemId - LegacyMapItemStateCodes.WireIdOffset, out var mapItem)
                ? mapItem
                : null;
            return state is not null;
        }
    }

    public IReadOnlyList<LegacyMapItemState> GetMapItemStates()
    {
        lock (gate)
            return mapItems.Values.OrderBy(static item => item.ItemId).ToArray();
    }

    /// <summary>
    /// Reproduces the static-gate branch of the legacy <c>ProcessMinTimer</c>.
    /// InitItem rows 0..16 are intentionally excluded; the C++ loop starts at
    /// row 17. An opened keyed gate spends one minute moving Delay 0 -> 1 and
    /// closes on the following minute, broadcasting MSG_CreateItem.
    /// </summary>
    public bool TryProcessMapItemMinute(DateTime localNow, out LegacyMapItemMinutePlan? plan)
    {
        lock (gate)
        {
            plan = null;
            if (localNow.Second != 0)
                return false;

            var minuteSlot = new DateTime(localNow.Year, localNow.Month, localNow.Day, localNow.Hour, localNow.Minute, 0, localNow.Kind);
            if (lastMapItemMinute == minuteSlot)
                return false;
            lastMapItemMinute = minuteSlot;

            var closedItems = new List<LegacyMapItemState>();
            foreach (var mapItem in mapItems.Values.OrderBy(static item => item.ItemId).ToArray())
            {
                if (mapItem.ItemId <= 17 || mapItem.State != LegacyMapItemStateCodes.Open)
                    continue;

                var keyId = itemData?.GetItemAbility(mapItem.Item, LegacyItemEffect.KeyId) ?? 0;
                if (keyId is <= 0 or >= 15)
                    continue;

                if (mapItem.Delay == 0)
                {
                    mapItems[mapItem.ItemId] = mapItem with { Delay = 1 };
                    continue;
                }

                var nextHeight = mapItem.Height;
                if (groundMaskTable is not null && mapGrid is not null)
                    groundMaskTable.TryApply(
                        mapGrid,
                        mapItem.GridCharge,
                        mapItem.State,
                        LegacyMapItemStateCodes.Locked,
                        mapItem.PositionX,
                        mapItem.PositionY,
                        mapItem.Rotate,
                        out nextHeight);

                var closedItem = mapItem with
                {
                    State = LegacyMapItemStateCodes.Locked,
                    Height = nextHeight,
                    Delay = 0,
                };
                mapItems[mapItem.ItemId] = closedItem;
                closedItems.Add(closedItem);
            }

            if (closedItems.Count > 0)
                plan = new LegacyMapItemMinutePlan(minuteSlot, closedItems);
            return true;
        }
    }

    public bool TryGetCastleQuestState(out LegacyCastleQuestState? state)
    {
        lock (gate)
        {
            state = castleQuestLevel < 0 && castleQuestTime < 0
                ? null
                : new LegacyCastleQuestState(castleQuestLevel, castleQuestTime, castleQuestLeaderConnectionId, castleQuestParty.ToArray());
            return state is not null;
        }
    }

    /// <summary>Ports the normal-gate branch of Exec_MSG_UpdateItem.</summary>
    public LegacyUpdateItemResult TryUpdateMapItem(int connectionId, int wireItemId, int requestedState, out LegacyUpdateItemOutcome? outcome)
    {
        outcome = null;
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyUpdateItemResult.ParticipantNotFound;
            if (participant.Mob.Length < LegacyAccountSnapshot.MobCurrentScoreOffset + LegacyScore.SizeInBytes)
                return LegacyUpdateItemResult.CombatStateUnavailable;
            if (LegacyMobCombatState.Read(participant.Mob).CurrentScore.Hp <= 0)
                return LegacyUpdateItemResult.AttackerNotAlive;
            if (requestedState is < 0 or > 5)
                return LegacyUpdateItemResult.InvalidState;
            if (wireItemId < LegacyMapItemStateCodes.WireIdOffset || wireItemId >= LegacyMapItemStateCodes.WireIdOffset + LegacyMapItemCatalog.MaxMapItems)
                return LegacyUpdateItemResult.InvalidItemId;

            var itemId = wireItemId - LegacyMapItemStateCodes.WireIdOffset;
            if (!mapItems.TryGetValue(itemId, out var mapItem))
                return LegacyUpdateItemResult.MapItemNotFound;

            if (itemData?.GetItemAbility(mapItem.Item, LegacyItemEffect.KeyId) is >= 10 and <= 14)
                return TryOpenCastleGateLocked(connectionId, wireItemId, requestedState, participant, mapItem, out outcome);

            if (participant.Mob.Length < LegacyAccountSnapshot.MobCarryOffset + (LegacyAccountSnapshot.MobCarryCount * LegacyItem.SizeInBytes))
                return LegacyUpdateItemResult.CombatStateUnavailable;

            var keyId = itemData?.GetItemAbility(mapItem.Item, LegacyItemEffect.KeyId) ?? 0;
            var keySlot = -1;
            LegacyItem consumedKey = default;
            if (mapItem.State != LegacyMapItemStateCodes.Locked && requestedState != LegacyMapItemStateCodes.Locked)
                keyId = 0;
            if (keyId != 0)
            {
                for (var slot = 0; slot < LegacyAccountSnapshot.MobCarryCount; slot++)
                {
                    var offset = LegacyAccountSnapshot.MobCarryOffset + (slot * LegacyItem.SizeInBytes);
                    var candidate = LegacyItem.Read(participant.Mob.AsSpan(offset, LegacyItem.SizeInBytes));
                    if (itemData?.GetItemAbility(candidate, LegacyItemEffect.KeyId) != keyId)
                        continue;
                    keySlot = slot;
                    consumedKey = candidate;
                    participant.Mob.AsSpan(offset, LegacyItem.SizeInBytes).Clear();
                    break;
                }

                if (keySlot < 0)
                    return LegacyUpdateItemResult.MissingKey;
            }

            var nextHeight = mapItem.Height;
            var stateChanged = mapItem.State != requestedState;
            if (stateChanged && groundMaskTable is not null && mapGrid is not null)
                stateChanged = groundMaskTable.TryApply(mapGrid, mapItem.GridCharge, mapItem.State, requestedState, mapItem.PositionX, mapItem.PositionY, mapItem.Rotate, out nextHeight);

            if (stateChanged)
            {
                mapItem = mapItem with { State = requestedState, Height = nextHeight, Delay = 0 };
                mapItems[itemId] = mapItem;
            }

            outcome = new LegacyUpdateItemOutcome(
                connectionId,
                wireItemId,
                mapItem,
                stateChanged,
                keySlot,
                consumedKey,
                participant.Mob.ToArray(),
                SuppressMissingKeyNotice: mapItem.Item.Index == 773);
            return LegacyUpdateItemResult.Accepted;
        }
    }

    private LegacyUpdateItemResult TryOpenCastleGateLocked(
        int connectionId,
        int wireItemId,
        int requestedState,
        Participant participant,
        LegacyMapItemState mapItem,
        out LegacyUpdateItemOutcome? outcome)
    {
        outcome = null;
        var gateKey = itemData!.GetItemAbility(mapItem.Item, LegacyItemEffect.KeyId);
        var keySlot = -1;
        var key = default(LegacyItem);
        var quest = -1;
        for (var slot = 0; slot < LegacyAccountSnapshot.MobCarryCount; slot++)
        {
            var offset = LegacyAccountSnapshot.MobCarryOffset + (slot * LegacyItem.SizeInBytes);
            var candidate = LegacyItem.Read(participant.Mob.AsSpan(offset, LegacyItem.SizeInBytes));
            if (itemData.GetItemAbility(candidate, LegacyItemEffect.KeyId) != gateKey)
                continue;

            var candidateQuest = itemData.GetItemAbility(candidate, LegacyItemEffect.Quest);
            if (gateKey != 10 && candidateQuest != castleQuestLevel)
                continue;

            keySlot = slot;
            key = candidate;
            quest = candidateQuest;
            break;
        }

        if (keySlot < 0)
            return gateKey is >= 11 and <= 14 ? LegacyUpdateItemResult.MissingCastleQuestKey : LegacyUpdateItemResult.MissingKey;

        List<LegacyWorldNpc>? removed = null;
        List<LegacyWorldNpc>? spawned = null;
        LegacyCastleQuestStart? questStart = null;
        if (gateKey == 10)
        {
            if (quest < 0 || quest >= castleQuests.Count)
                return LegacyUpdateItemResult.InvalidCastleQuest;
            if (castleQuestTime != -1)
                return LegacyUpdateItemResult.CastleQuestActive;

            var definition = castleQuests[quest];
            removed = npcs.Values
                .Where(static npc => npc.PositionX >= 2180 && npc.PositionX <= 2296 && npc.PositionY >= 1160 && npc.PositionY <= 1269)
                .ToList();
            foreach (var npc in removed)
            {
                npcs.Remove(npc.ConnectionId);
                ForgetGuildTreasuryNpcRegistrationLocked(npc.ConnectionId);
            }

            spawned = [];
            for (var generateIndex = definition.MobInitial; generateIndex <= definition.MobEnd; generateIndex++)
            {
                // The C++ path enables MinuteGenerate here; the C# catalog is already explicit.
                for (var copy = 0; copy < 2; copy++)
                    if (TrySpawnGeneratedNpcGroup(generateIndex, out var generated) && generated is not null)
                        spawned.AddRange(generated);
            }

            castleQuestLevel = quest;
            castleQuestTime = definition.QuestTime - 1;
            castleQuestLeaderConnectionId = partyLeaders.TryGetValue(connectionId, out var configuredLeader)
                ? configuredLeader
                : connectionId;
            castleQuestParty.Clear();
            castleQuestParty.Add(castleQuestLeaderConnectionId);
            foreach (var member in partyLeaders.Where(pair => pair.Value == castleQuestLeaderConnectionId).Select(static pair => pair.Key))
                castleQuestParty.Add(member);
            questStart = new LegacyCastleQuestStart(
                quest,
                castleQuestTime,
                castleQuestLeaderConnectionId,
                castleQuestParty.ToArray(),
                removed,
                spawned);
        }

        var keyOffset = LegacyAccountSnapshot.MobCarryOffset + (keySlot * LegacyItem.SizeInBytes);
        participant.Mob.AsSpan(keyOffset, LegacyItem.SizeInBytes).Clear();
        var nextHeight = mapItem.Height;
        var stateChanged = mapItem.State != LegacyMapItemStateCodes.Open;
        if (stateChanged && groundMaskTable is not null && mapGrid is not null)
            stateChanged = groundMaskTable.TryApply(mapGrid, mapItem.GridCharge, mapItem.State, LegacyMapItemStateCodes.Open, mapItem.PositionX, mapItem.PositionY, mapItem.Rotate, out nextHeight);
        if (stateChanged)
        {
            mapItem = mapItem with { State = LegacyMapItemStateCodes.Open, Height = nextHeight, Delay = 0 };
            mapItems[mapItem.ItemId] = mapItem;
        }

        outcome = new LegacyUpdateItemOutcome(
            connectionId,
            wireItemId,
            mapItem,
            stateChanged,
            keySlot,
            key,
            participant.Mob.ToArray(),
            SuppressMissingKeyNotice: mapItem.Item.Index == 773,
            CastleQuestStart: questStart);
        return LegacyUpdateItemResult.Accepted;
    }

    public bool TryGetNpcEventDropState(out LegacyNpcEventDropSnapshot? snapshot)
    {
        lock (gate)
        {
            snapshot = npcEventDrop is null
                ? null
                : new LegacyNpcEventDropSnapshot(npcEventDrop, npcEventCurrentIndex);
            return snapshot is not null;
        }
    }

    public bool ConfigurePistaLv6State(int leaderConnectionId, int mobCount, int mobsLeft = 0)
    {
        if (leaderConnectionId <= 0 || mobCount < 0 || mobsLeft is < 0 or > 100)
            return false;
        lock (gate)
        {
            if (!participants.ContainsKey(leaderConnectionId))
                return false;
            pistaLv6MobsLeft = mobsLeft;
            pistaLv6State = new LegacyPistaLv6State(leaderConnectionId, mobCount, pistaLv6MobsLeft);
            lastPistaLv6MobCountAt = null;
            return true;
        }
    }

    public bool TryGetPistaLv6State(out LegacyPistaLv6State? state)
    {
        lock (gate)
        {
            state = pistaLv6State;
            return state is not null;
        }
    }

    /// <summary>
    /// Ports the RuneQuest branch of <c>Exec_MSG_Quest</c>: only the Pista
    /// merchant NPC (Merchant=72) is accepted, the caller must be the party
    /// leader, and one ticket (item 5134) is consumed atomically.
    /// </summary>
    public LegacyPistaRegistrationResult TryRegisterPistaParty(int connectionId, int npcConnectionId, out LegacyPistaRegistrationOutcome? outcome)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyPistaRegistrationResult.ParticipantNotFound;
            if (!npcs.TryGetValue(npcConnectionId, out var npc))
                return LegacyPistaRegistrationResult.NpcNotFound;
            if (npc.MobSnapshot.Length < LegacyAccountSnapshot.MobMerchantOffset + 1 || npc.MobSnapshot[LegacyAccountSnapshot.MobMerchantOffset] != 72)
                return LegacyPistaRegistrationResult.WrongNpc;
            if (partyLeaders.TryGetValue(connectionId, out var partyLeader) && partyLeader != connectionId)
                return LegacyPistaRegistrationResult.NotPartyLeader;
            if (participant.Mob.Length < LegacyAccountSnapshot.MobCarryOffset + (LegacyAccountSnapshot.MobCarryCount * LegacyItem.SizeInBytes))
                return LegacyPistaRegistrationResult.InventoryUnavailable;

            var ticketSlot = -1;
            LegacyItem ticket = default;
            for (var slot = 0; slot < LegacyAccountSnapshot.MobCarryCount; slot++)
            {
                var candidate = LegacyItem.Read(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (slot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
                if (candidate.Index != 5134)
                    continue;
                ticketSlot = slot;
                ticket = candidate;
                break;
            }

            if (ticketSlot < 0)
                return LegacyPistaRegistrationResult.MissingTicket;

            var level = Math.Min(GetLegacyItemSanctuary(ticket), 6);
            var partyCount = level == 0 ? 2 : 3;
            if (pistaRegistrations.Values.Any(registration => registration.Level == level && registration.LeaderConnectionId == connectionId))
                return LegacyPistaRegistrationResult.AlreadyRegistered;
            if (level == 6 && pistaLv6State is { LeaderConnectionId: var activeLeader } && activeLeader != connectionId)
                return LegacyPistaRegistrationResult.RoomFull;

            var partySlot = Enumerable.Range(0, partyCount).FirstOrDefault(slot => !pistaRegistrations.ContainsKey((level, slot)), -1);
            if (partySlot < 0)
                return LegacyPistaRegistrationResult.RoomFull;

            var emptyTicket = default(LegacyItem);
            emptyTicket.Write(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (ticketSlot * LegacyItem.SizeInBytes)));
            var registration = new LegacyPistaRegistration(level, partySlot, connectionId, ReadMobName(participant.Mob));
            pistaRegistrations.Add((level, partySlot), registration);
            if (level is 1 or 3 or 4)
                pistaMobCounts[(level, partySlot)] = 0;
            if (level == 6)
                pistaLv6State = new LegacyPistaLv6State(connectionId, 0, pistaLv6MobsLeft);

            outcome = new LegacyPistaRegistrationOutcome(level, partySlot, connectionId, ticketSlot, emptyTicket, registration.LeaderName);
            return LegacyPistaRegistrationResult.Accepted;
        }
    }

    /// <summary>
    /// Processes the legacy Pista entry instant (minute 00/20/40, second 00).
    /// The player and party positions are updated under the same world lock;
    /// callers use the returned plan to relay the corresponding MSG_Action
    /// frames and generated NPCs.
    /// </summary>
    public bool TryProcessPistaEntry(DateTime localNow, out LegacyPistaEntryPlan? plan, int pistaLevel4MobRoll = -1)
    {
        if (pistaLevel4MobRoll is < -1 or >= 8)
            throw new ArgumentOutOfRangeException(nameof(pistaLevel4MobRoll), pistaLevel4MobRoll, "The injected Pista +4 mob roll must be -1 or in the range 0..7.");

        lock (gate)
        {
            plan = null;
            if (localNow.Second != 0 || localNow.Minute % 20 != 0)
                return false;

            var entrySlot = new DateTime(localNow.Year, localNow.Month, localNow.Day, localNow.Hour, localNow.Minute, 0, localNow.Kind);
            if (lastPistaEntrySlot == entrySlot)
                return false;
            lastPistaEntrySlot = entrySlot;

            var teleports = new List<LegacyPistaTeleport>();
            var spawned = new List<LegacyWorldNpc>();
            foreach (var registration in pistaRegistrations.Values.OrderBy(static registration => registration.Level).ThenBy(static registration => registration.PartySlot).ToArray())
            {
                if (!participants.TryGetValue(registration.LeaderConnectionId, out var leader))
                    continue;
                if (!string.Equals(ReadMobName(leader.Mob), registration.LeaderName, StringComparison.Ordinal))
                    continue;
                if (partyLeaders.TryGetValue(leader.ConnectionId, out var mappedLeader) && mappedLeader != leader.ConnectionId)
                    continue;
                if (leader.PositionX / 128 != 25 || leader.PositionY / 128 != 13)
                    continue;

                var destination = GetPistaPosition(registration.Level, registration.PartySlot);
                if (destination is null)
                    continue;

                AddPistaTeleportLocked(leader, registration, destination.Value, teleports);
                foreach (var memberId in partyLeaders.Where(pair => pair.Value == leader.ConnectionId).Select(pair => pair.Key).ToArray())
                {
                    if (memberId == leader.ConnectionId || !participants.TryGetValue(memberId, out var member))
                        continue;
                    if (member.PositionX / 128 != 25 || member.PositionY / 128 != 13)
                        continue;
                    AddPistaTeleportLocked(member, registration, destination.Value, teleports);
                }

                if (registration.Level == 6)
                {
                    pistaLv6State = new LegacyPistaLv6State(registration.LeaderConnectionId, 100, pistaLv6MobsLeft);
                    lastPistaLv6MobCountAt = null;
                    foreach (var generateIndex in GetPistaEntryGenerators(registration.Level, registration.PartySlot))
                        if (TrySpawnPistaEntryNpcs(generateIndex, registration.Level, registration.PartySlot, out var generated) && generated is not null)
                            spawned.AddRange(generated);
                }
                else
                {
                    if (registration.Level == 4)
                        pistaMobCounts[(4, registration.PartySlot)] = 8 + (pistaLevel4MobRoll >= 0 ? pistaLevel4MobRoll : System.Security.Cryptography.RandomNumberGenerator.GetInt32(8));
                    foreach (var generateIndex in GetPistaEntryGenerators(registration.Level, registration.PartySlot))
                        if (TrySpawnPistaEntryNpcs(generateIndex, registration.Level, registration.PartySlot, out var generated) && generated is not null)
                            spawned.AddRange(generated);
                }
            }

            plan = new LegacyPistaEntryPlan(entrySlot, teleports, spawned);
            return true;
        }
    }

    /// <summary>Processes the legacy Pista exit instant (minute 15/35/55, second 00).</summary>
    public bool TryProcessPistaExit(DateTime localNow, out LegacyPistaExitPlan? plan, int pistaRuneRoll = -1)
    {
        if (pistaRuneRoll is < -1 or >= 5)
            throw new ArgumentOutOfRangeException(nameof(pistaRuneRoll), pistaRuneRoll, "The injected Pista exit rune roll must be -1 or in the range 0..4.");

        lock (gate)
        {
            plan = null;
            if (localNow.Second != 0 || localNow.Minute is not (15 or 35 or 55))
                return false;

            var exitSlot = new DateTime(localNow.Year, localNow.Month, localNow.Day, localNow.Hour, localNow.Minute, 0, localNow.Kind);
            if (lastPistaExitSlot == exitSlot)
                return false;
            lastPistaExitSlot = exitSlot;

            var itemDrops = new List<LegacyItemDrop>();
            itemDrops.AddRange(AwardPistaWinnerLocked(1, pistaLevel1Runes, 2, pistaRuneRoll));
            itemDrops.AddRange(AwardPistaWinnerLocked(3, pistaLevel3Runes, 4, pistaRuneRoll));
            // The legacy exit uses DeleteMob over the complete Pista arena,
            // rather than filtering only by GenerateIndex. Keep the known
            // Pista ownership fallback as well so generated fixtures whose
            // saved position is not in the arena cannot leak between rooms.
            var removed = npcs.Values
                .Where(static npc => IsPistaGeneratedIndex(npc.GenerateIndex)
                    || (npc.PositionX >= 3310 && npc.PositionX <= 3588 && npc.PositionY >= 1005 && npc.PositionY <= 1663))
                .ToArray();
            foreach (var npc in removed)
            {
                npcs.Remove(npc.ConnectionId);
                ForgetGuildTreasuryNpcRegistrationLocked(npc.ConnectionId);
            }

            pistaRegistrations.Clear();
            pistaMobCounts.Clear();
            pistaLv6State = null;
            lastPistaLv6MobCountAt = null;

            var teleports = new List<LegacyPistaTeleport>();
            foreach (var participant in participants.Values.ToArray())
            {
                if (participant.PositionX < 3310 || participant.PositionX > 3588 || participant.PositionY < 1005 || participant.PositionY > 1663)
                    continue;

                var fromX = participant.PositionX;
                var fromY = participant.PositionY;
                participant.PositionX = 3294;
                participant.PositionY = 1701;
                teleports.Add(new LegacyPistaTeleport(participant.ConnectionId, -1, -1, fromX, fromY, 3294, 1701));
            }

            plan = new LegacyPistaExitPlan(exitSlot, teleports, removed, itemDrops);
            return true;
        }
    }

    private IReadOnlyList<LegacyItemDrop> AwardPistaWinnerLocked(int level, IReadOnlyList<int> runes, int progressionValue, int pistaRuneRoll)
    {
        var counts = Enumerable.Range(0, 3).Select(slot => pistaMobCounts.GetValueOrDefault((level, slot))).ToArray();
        var winnerSlot = counts[0] > counts[1] && counts[0] > counts[2]
            ? 0
            : counts[1] > counts[0] && counts[1] > counts[2]
                ? 1
                : counts[2] > counts[0] && counts[2] > counts[1]
                    ? 2
                    : -1;
        if (winnerSlot < 0 || !pistaRegistrations.TryGetValue((level, winnerSlot), out var registration) || !participants.TryGetValue(registration.LeaderConnectionId, out var leader))
            return [];
        if (!string.Equals(ReadMobName(leader.Mob), registration.LeaderName, StringComparison.Ordinal))
            return [];

        var runeIndex = pistaRuneRoll >= 0
            ? pistaRuneRoll
            : System.Security.Cryptography.RandomNumberGenerator.GetInt32(5);
        var leaderConnectionId = leader.ConnectionId;
        var recipientIds = partyLeaders
            .Where(pair => pair.Value == leaderConnectionId)
            .Select(static pair => pair.Key)
            .Distinct()
            .ToList();
        if (!recipientIds.Contains(leaderConnectionId))
            recipientIds.Insert(0, leaderConnectionId);

        var drops = new List<LegacyItemDrop>(recipientIds.Count + 1);
        var rune = new LegacyItem((short)runes[runeIndex], 0, 0, 0, 0, 0, 0);
        foreach (var recipientId in recipientIds)
        {
            if (!participants.TryGetValue(recipientId, out var recipient) || !TryInsertItemLocked(recipient, rune, out var slot))
                continue;
            drops.Add(new LegacyItemDrop(recipient.ConnectionId, slot, rune));
        }

        var nextStage = new LegacyItem(5134, 43, (byte)progressionValue, 0, 0, 0, 0);
        if (TryInsertItemLocked(leader, nextStage, out var nextStageSlot))
            drops.Add(new LegacyItemDrop(leader.ConnectionId, nextStageSlot, nextStage));
        return drops;
    }

    private void AddPistaTeleportLocked(Participant participant, LegacyPistaRegistration registration, (short X, short Y) destination, ICollection<LegacyPistaTeleport> teleports)
    {
        var fromX = participant.PositionX;
        var fromY = participant.PositionY;
        participant.PositionX = destination.X;
        participant.PositionY = destination.Y;
        teleports.Add(new LegacyPistaTeleport(participant.ConnectionId, registration.Level, registration.PartySlot, fromX, fromY, destination.X, destination.Y));
    }

    private static (short X, short Y)? GetPistaPosition(int level, int partySlot)
    {
        short[,] positions =
        {
            { 3350, 1622, 3431, 1634, 0, 0 },
            { 3362, 1574, 3385, 1555, 3414, 1575 },
            { 3410, 1453, 3419, 1426, 3358, 1436 },
            { 3376, 1096, 3400, 1089, 3392, 1073 },
            { 3342, 1394, 3444, 1394, 3442, 1293 },
            { 3421, 1217, 3426, 1211, 3424, 1226 },
            { 3404, 1517, 3392, 1479, 3383, 1501 },
        };
        if (level is < 0 or >= 7 || partySlot is < 0 or >= 3)
            return null;
        var offset = partySlot * 2;
        var x = positions[level, offset];
        var y = positions[level, offset + 1];
        return x == 0 && y == 0 ? null : (x, y);
    }

    private static IEnumerable<int> GetPistaEntryGenerators(int level, int partySlot)
    {
        if (level == 0)
            return partySlot == 0 ? [5654] : [5653];
        if (level == 1)
            return [5706, 5707, 5708, .. Enumerable.Range(5709, 56)];
        if (level == 2)
            return [5789];
        if (level == 4 && partySlot == 0)
            return Enumerable.Range(5854, 45);
        return [];
    }

    private bool TrySpawnPistaEntryNpcs(int generateIndex, int level, int partySlot, out IReadOnlyList<LegacyWorldNpc>? spawned)
    {
        var position = GetPistaEntryPosition(level, partySlot, generateIndex);
        return TrySpawnGeneratedNpcGroup(generateIndex, out spawned, positionX: position?.X, positionY: position?.Y,
            forceGenerationRoutePosition: position is not null);
    }

    private static (short X, short Y)? GetPistaEntryPosition(int level, int partySlot, int generateIndex)
    {
        if (level != 1)
            return null;

        return generateIndex switch
        {
            5706 => (3358, 1582),
            5707 => (3386, 1548),
            5708 => (3418, 1582),
            _ => null,
        };
    }

    private static bool IsPistaGeneratedIndex(int generateIndex) =>
        generateIndex is >= 4283 and <= 4301 or >= 5653 and <= 5899 or >= 5948 and <= 5955 or >= 5972 and <= 5975;

    /// <summary>
    /// Ends the currently tracked Pista +6 room and removes its known +6
    /// generator range. The minute-based Pista exit separately clears the
    /// complete legacy arena, including mobs without a generator identity.
    /// </summary>
    public IReadOnlyList<LegacyWorldNpc> ResetPistaLv6Room()
    {
        lock (gate)
        {
            var removed = npcs.Values
                .Where(static npc => npc.GenerateIndex is >= LegacyMobAiPlanner.RunequestLv6BossGenerateIndex and <= PistaLv6DespawnEndGenerateIndex)
                .ToArray();
            foreach (var npc in removed)
            {
                npcs.Remove(npc.ConnectionId);
                ForgetGuildTreasuryNpcRegistrationLocked(npc.ConnectionId);
            }

            foreach (var key in pistaRegistrations.Keys.Where(static key => key.Level == 6).ToArray())
                pistaRegistrations.Remove(key);
            pistaLv6State = null;
            lastPistaLv6MobCountAt = null;
            return removed;
        }
    }

    private int GetLegacyItemSanctuary(LegacyItem item)
    {
        if (item.Index is >= 2330 and < 2390)
            return 0;

        var raw = item.Effect1 is >= 116 and <= 125 ? item.Value1
            : item.Effect2 is >= 116 and <= 125 ? item.Value2
            : item.Effect3 is >= 116 and <= 125 ? item.Value3
            : item.Effect1 == LegacyItemEffect.Sanctuary ? item.Value1
            : item.Effect2 == LegacyItemEffect.Sanctuary ? item.Value2
            : item.Effect3 == LegacyItemEffect.Sanctuary ? item.Value3
            : 0;
        return raw switch
        {
            9 => 9,
            >= 230 and <= 233 => 10,
            >= 234 and <= 237 => 12,
            >= 238 and <= 241 => 15,
            >= 242 and <= 245 => 18,
            >= 246 and <= 249 => 22,
            >= 250 and <= 253 => 27,
            _ => raw % 10,
        };
    }

    /// <summary>
    /// Builds the TMProject 7.69 <c>_MSG_MobCount</c> signal for a player
    /// inside the Pista +6 rectangle. Its two parameters are MobsLeft and 100.
    /// </summary>
    public bool TryBuildPistaLv6MobCountFrame(int connectionId, LegacyFrameCodec codec, uint clientTick, byte keywordIndex, out byte[]? frame)
    {
        ArgumentNullException.ThrowIfNull(codec);
        lock (gate)
        {
            frame = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return false;
            if (participant.PositionX < 3330 || participant.PositionX >= 3448 || participant.PositionY < 1475 || participant.PositionY >= 1525)
                return false;

            frame = new MobCountConfirmation(pistaLv6MobsLeft, 100).ToFrame(codec, clientTick, keywordIndex);
            return true;
        }
    }

    /// <summary>
    /// Emits the 7.69 +6 progress signal on the same wall-clock cadence as
    /// <c>ProcessSecMinTimer</c>: twelve 200 ms ticks (2.4 seconds). Each
    /// participant inside the original exclusive rectangle receives the two
    /// <c>_MSG_MobCount</c> parameters: <c>MobsLeft</c> and 100.
    /// </summary>
    public bool TryProcessPistaLv6MobCount(DateTime localNow, LegacyFrameCodec codec, uint clientTick, byte keywordIndex, out IReadOnlyList<LegacyPistaMobCountFrame>? frames)
    {
        ArgumentNullException.ThrowIfNull(codec);
        lock (gate)
        {
            frames = null;
            if (lastPistaLv6MobCountAt is { } last && localNow - last < TimeSpan.FromMilliseconds(2400))
                return false;

            lastPistaLv6MobCountAt = localNow;
            var frame = new MobCountConfirmation(pistaLv6MobsLeft, 100).ToFrame(codec, clientTick, keywordIndex);
            frames = participants.Values
                .Where(static participant => participant.PositionX >= 3330 && participant.PositionX < 3448 && participant.PositionY >= 1475 && participant.PositionY < 1525)
                .Select(participant => new LegacyPistaMobCountFrame(participant.ConnectionId, frame))
                .ToArray();
            return true;
        }
    }

    /// <summary>Creates one generated leader NPC using the legacy NPCGener/template pair.</summary>
    public bool TrySpawnGeneratedNpc(int generateIndex, out LegacyWorldNpc? spawned, Func<int, int>? randomRoll = null,
        short? positionX = null, short? positionY = null, int? terrainHeight = null, bool forceGenerationRoutePosition = false)
    {
        spawned = null;
        if (!TrySpawnGeneratedNpcGroup(
                generateIndex,
                out var group,
                randomRoll,
                positionX,
                positionY,
                terrainHeight,
                forceGenerationRoutePosition) ||
            group is null || group.Count == 0)
            return false;

        spawned = group[0];
        return true;
    }

    /// <summary>Creates the full generated NPC group so every member can be relayed to viewers.</summary>
    public bool TrySpawnGeneratedNpcGroup(int generateIndex, out IReadOnlyList<LegacyWorldNpc>? spawnedGroup, Func<int, int>? randomRoll = null,
        short? positionX = null, short? positionY = null, int? terrainHeight = null, bool forceGenerationRoutePosition = false,
        int? routeGenerateIndex = null)
    {
        if (positionX.HasValue != positionY.HasValue)
            throw new ArgumentException("A generated NPC position override requires both X and Y.");

        lock (gate)
        {
            spawnedGroup = null;
            var effectiveRouteGenerateIndex = routeGenerateIndex ?? generateIndex;
            if (npcGenerationCatalog is null ||
                !npcGenerationCatalog.TryGet(generateIndex, out var definition) || definition is null ||
                !npcGenerationCatalog.TryGet(effectiveRouteGenerateIndex, out var routeDefinition) || routeDefinition is null)
                return false;

            randomRoll ??= static maximum => Random.Shared.Next(maximum);
            var currentCount = npcs.Values.Count(npc => npc.GenerationOwnerIndex == generateIndex);
            if (!npcGenerationCatalog.TrySelectFollowerCount(generateIndex, currentCount, randomRoll, out var followerCount))
                return false;
            if (!npcGenerationCatalog.TryCreateLeader(generateIndex, effectiveRouteGenerateIndex, randomRoll, out var generated) || generated is null)
                return false;
            var leaderMobSnapshot = ApplyNewbieEventGeneratedNpcHp(generated.MobSnapshot);
            leaderMobSnapshot = ApplyGeneratedNpcClanVariation(leaderMobSnapshot, randomRoll);

            var generationRoute = generated.GenerationRoute;
            if (forceGenerationRoutePosition && positionX is { } forcedX && positionY is { } forcedY)
            {
                static LegacyNpcGenerationRoutePoint Reanchor(LegacyNpcGenerationRoutePoint point, int x, int y) =>
                    point with { X = x, Y = y, Range = 0 };

                generationRoute = generationRoute with
                {
                    Start = Reanchor(generationRoute.Start, forcedX, forcedY),
                    Segment1 = Reanchor(generationRoute.Segment1, forcedX, forcedY),
                    Segment2 = Reanchor(generationRoute.Segment2, forcedX, forcedY),
                    Segment3 = Reanchor(generationRoute.Segment3, forcedX, forcedY),
                    Dest = Reanchor(generationRoute.Dest, forcedX, forcedY),
                };
            }

            var connectionId = AllocateNpcConnectionId();
            var spawnX = (int)(positionX ?? generated.PositionX);
            var spawnY = (int)(positionY ?? generated.PositionY);
            if (!TryFindEmptyGeneratedNpcGridLocked(connectionId, ref spawnX, ref spawnY))
                return false;

            if ((positionX is null && positionY is null) || forceGenerationRoutePosition)
            {
                generationRoute = generationRoute with
                {
                    Start = generationRoute.Start with { X = spawnX, Y = spawnY },
                };
            }
            var effectiveTerrainHeight = terrainHeight
                ?? mapGrid?.GetTerrainHeight(spawnX, spawnY)
                ?? (IsPistaGeneratedIndex(effectiveRouteGenerateIndex) ? 0 : null);
            var leaderNpc = new LegacyWorldNpc(
                connectionId,
                checked((short)spawnX),
                checked((short)spawnY),
                leaderMobSnapshot,
                generated.AffectSnapshot,
                GenerateIndex: generated.GenerateIndex,
                TerrainHeight: effectiveTerrainHeight,
                initialEnemyList: new int[LegacyMobAiPlanner.EnemyCapacity],
                RouteType: definition.RouteType)
            {
                GenerationOwnerIndex = generateIndex,
                GenerationRoute = generationRoute,
                SegmentX = checked((short)generationRoute.Start.X),
                SegmentY = checked((short)generationRoute.Start.Y),
                SegmentProgress = 0,
                SegmentDirection = 0,
                WaitSec = generationRoute.Start.Wait,
            };
            leaderNpc = RegisterGuildTreasuryNpcLocked(leaderNpc);
            npcs.Add(connectionId, leaderNpc);
            var group = new List<LegacyWorldNpc>(1 + Math.Min(Math.Max(0, followerCount), LegacyNpcGenerationCatalog.MaximumPartyMembers))
            {
                leaderNpc,
            };

            var forcedFollowerX = forceGenerationRoutePosition ? positionX : null;
            var forcedFollowerY = forceGenerationRoutePosition ? positionY : null;
            for (var followerIndex = 0;
                 followerIndex < followerCount && followerIndex < LegacyNpcGenerationCatalog.MaximumPartyMembers;
                 followerIndex++)
            {
                if (!npcGenerationCatalog.TryCreateFollower(
                        generateIndex,
                        effectiveRouteGenerateIndex,
                        followerIndex,
                        generationRoute,
                        randomRoll,
                        forcedFollowerX,
                        forcedFollowerY,
                        out var generatedFollower) || generatedFollower is null)
                    continue;
                var followerMobSnapshot = ApplyNewbieEventGeneratedNpcHp(generatedFollower.MobSnapshot);
                followerMobSnapshot = ApplyGeneratedNpcClanVariation(followerMobSnapshot, randomRoll);

                var followerId = AllocateNpcConnectionId();
                var followerX = (int)generatedFollower.PositionX;
                var followerY = (int)generatedFollower.PositionY;
                if (!TryFindEmptyGeneratedNpcGridLocked(followerId, ref followerX, ref followerY))
                    continue;

                var followerRoute = generatedFollower.GenerationRoute with
                {
                    Start = generatedFollower.GenerationRoute.Start with { X = followerX, Y = followerY },
                };
                var followerTerrainHeight = mapGrid?.GetTerrainHeight(followerX, followerY)
                    ?? (IsPistaGeneratedIndex(effectiveRouteGenerateIndex) ? 0 : null);
                var follower = new LegacyWorldNpc(
                    followerId,
                    checked((short)followerX),
                    checked((short)followerY),
                    followerMobSnapshot,
                    generatedFollower.AffectSnapshot,
                    GenerateIndex: generatedFollower.GenerateIndex,
                    TerrainHeight: followerTerrainHeight,
                    initialEnemyList: new int[LegacyMobAiPlanner.EnemyCapacity],
                    RouteType: definition.RouteType)
                {
                    GenerationOwnerIndex = generateIndex,
                    LeaderId = connectionId,
                    IsGeneratedGroupPartyMember = true,
                    GenerationRoute = followerRoute,
                    SegmentX = checked((short)followerRoute.Start.X),
                    SegmentY = checked((short)followerRoute.Start.Y),
                    SegmentProgress = 0,
                    SegmentDirection = 0,
                    WaitSec = generatedFollower.GenerationRoute.Start.Wait,
                };
                follower = RegisterGuildTreasuryNpcLocked(follower);
                npcs.Add(followerId, follower);
                group.Add(follower);
            }

            spawnedGroup = group.AsReadOnly();
            return true;
        }
    }

    private byte[] ApplyNewbieEventGeneratedNpcHp(byte[] mobSnapshot)
    {
        var score = LegacyScore.Read(mobSnapshot.AsSpan(
            LegacyAccountSnapshot.MobCurrentScoreOffset,
            LegacyScore.SizeInBytes));
        var initialHp = LegacyNewbieEventNpcHpMath.ApplyInitialHp(
            score.MaxHp,
            score.Level,
            combatWorldState.NewbieEventServer);
        if (score.Hp == initialHp)
            return mobSnapshot;

        var updatedSnapshot = mobSnapshot.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(
            updatedSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24, sizeof(int)),
            initialHp);
        return updatedSnapshot;
    }

    private static byte[] ApplyGeneratedNpcClanVariation(byte[] mobSnapshot, Func<int, int> randomRoll)
    {
        if (mobSnapshot[LegacyAccountSnapshot.MobClanOffset] != 1)
            return mobSnapshot;

        var roll = randomRoll(10);
        if (roll < 0 || roll >= 10)
            throw new ArgumentOutOfRangeException(nameof(randomRoll), "The generated NPC Clan roll must be inside its requested range.");
        if (roll != 1)
            return mobSnapshot;

        var updatedSnapshot = mobSnapshot.ToArray();
        updatedSnapshot[LegacyAccountSnapshot.MobClanOffset] = 2;
        return updatedSnapshot;
    }

    public bool Enter(int connectionId, string accountName, Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> send, ReadOnlyMemory<byte> spawnFrame = default)
    {
        ArgumentNullException.ThrowIfNull(accountName);
        ArgumentNullException.ThrowIfNull(send);
        lock (gate)
        {
            if (participants.ContainsKey(connectionId)) return false;
            participants.Add(connectionId, new Participant(connectionId, accountName, send, spawnFrame));
            battleRoyaleDoorRemovedMobGridIds.Remove(connectionId);
            return true;
        }
    }

    /// <summary>Registers a persistent non-summon NPC using a full legacy STRUCT_MOB snapshot.</summary>
    public int EnterNpc(ReadOnlyMemory<byte> mob, short positionX, short positionY, ReadOnlyMemory<byte> affect = default, int requestedConnectionId = 0, int generateIndex = -1, int? terrainHeight = null, LegacyAutoTradeSnapshot? autoTradeSnapshot = null, string? autoTradeOwnerAccount = null, int autoTradeCharacterSlot = -1, int leaderConnectionId = 0)
    {
        if (mob.Length != LegacyAccountSnapshot.CharacterStride)
            throw new ArgumentException("An NPC requires a full STRUCT_MOB snapshot.", nameof(mob));
        if (!affect.IsEmpty && affect.Length != LegacyAccountSnapshot.AffectStride)
            throw new ArgumentException("An NPC affect snapshot must contain 256 bytes.", nameof(affect));

        lock (gate)
        {
            var connectionId = requestedConnectionId > 0 ? requestedConnectionId : AllocateNpcConnectionId();
            if (participants.ContainsKey(connectionId) || summons.ContainsKey(connectionId) || npcs.ContainsKey(connectionId))
                return 0;

            var npc = new LegacyWorldNpc(
                connectionId,
                positionX,
                positionY,
                mob.ToArray(),
                affect.IsEmpty ? new byte[LegacyAccountSnapshot.AffectStride] : affect.ToArray(),
                GenerateIndex: generateIndex,
                TerrainHeight: terrainHeight,
                AutoTradeSnapshot: autoTradeSnapshot is null ? null : autoTradeSnapshot with { ConnectionId = connectionId },
                AutoTradeOwnerAccount: autoTradeOwnerAccount,
                AutoTradeCharacterSlot: autoTradeCharacterSlot)
            {
                LeaderId = leaderConnectionId,
                IsGeneratedGroupPartyMember = generateIndex >= 0 && leaderConnectionId != 0,
            };
            npcs.Add(connectionId, RegisterGuildTreasuryNpcLocked(npc));
            return connectionId;
        }
    }

    /// <summary>Removes one persistent NPC and returns its last world snapshot.</summary>
    public bool TryRemoveNpc(int connectionId, out LegacyWorldNpc? removed)
    {
        lock (gate)
        {
            if (!npcs.Remove(connectionId, out var current))
            {
                removed = null;
                return false;
            }

            ForgetGuildTreasuryNpcRegistrationLocked(connectionId);
            ClearRemovedTargetLocked(connectionId);
            removed = current;
            return true;
        }
    }

    /// <summary>Registers a server-authoritative item lying on the field.</summary>
    public int EnterGroundItem(LegacyItem item, short positionX, short positionY, int requestedItemId = 0)
    {
        if (item.Index <= 0)
            throw new ArgumentOutOfRangeException(nameof(item), "A ground item must have a positive legacy index.");

        lock (gate)
        {
            var itemId = AllocateGroundItemIdLocked(requestedItemId);
            if (itemId == 0)
                return 0;
            groundItems.Add(itemId, new LegacyWorldItem(itemId, positionX, positionY, item, Delay: 90));
            return itemId + LegacyWorldItem.WireIdOffset;
        }
    }

    /// <summary>
    /// Checks the legacy skill-97 item gate without consuming the ground item.
    /// TMProject 7.69 indexes pItemGrid as [PosX][PosY], while world coordinates
    /// are stored as [Y][X]; preserve that transposed lookup for compatibility.
    /// </summary>
    public bool HasMortarItemAtLegacyAttackPosition(ushort positionX, ushort positionY)
    {
        if (positionX is 0 or >= LegacyWorldItem.MaxGridCoordinate ||
            positionY is 0 or >= LegacyWorldItem.MaxGridCoordinate)
            return false;

        lock (gate)
        {
            return mapItems.Values.Any(item =>
                    item.State != LegacyMapItemStateCodes.Nothing && item.Item.Index == 746 &&
                    item.PositionX == positionY && item.PositionY == positionX) ||
                groundItems.Values.Any(item =>
                    item.Item.Index == 746 && item.PositionX == positionY && item.PositionY == positionX);
        }
    }

    /// <summary>Applies TMProject 7.69 SetColoseumDoor to static slots 13 and 14.</summary>
    public LegacyBattleRoyaleDoorUpdate? TryUpdateBattleRoyaleDoors(int requestedState)
    {
        if (requestedState is not (LegacyMapItemStateCodes.Open or LegacyMapItemStateCodes.Locked))
            return null;

        lock (gate)
        {
            var updatedGates = new List<LegacyBattleRoyaleDoorGateUpdate>(2);
            foreach (var itemId in new[] { 13, 14 })
            {
                if (!mapItems.TryGetValue(itemId, out var mapItem) ||
                    mapItem.Item.Index <= 0 || mapItem.Item.Index >= LegacyItemDataTable.MaxItemIndex)
                    continue;

                var actorEffects = new List<LegacyBattleRoyaleDoorActorEffect>();
                var height = mapItem.Height;
                var stateChanged = mapItem.State != requestedState;
                var maskApplied = false;
                if (stateChanged && groundMaskTable is not null && mapGrid is not null)
                {
                    maskApplied = groundMaskTable.TryApply(
                        mapGrid,
                        mapItem.GridCharge,
                        mapItem.State,
                        requestedState,
                        mapItem.PositionX,
                        mapItem.PositionY,
                        mapItem.Rotate,
                        out height);
                    stateChanged = maskApplied;
                }

                mapItem = mapItem with
                {
                    State = stateChanged ? requestedState : mapItem.State,
                    Height = stateChanged ? height : mapItem.Height,
                    Delay = 0,
                };
                mapItems[itemId] = mapItem;

                // Server.cpp calls UpdateItem before sending CreateItem. The update
                // walks cells 0..4 and relays GetAction/RemoveMob for occupants.
                if (maskApplied && mapGrid is not null && groundMaskTable is not null)
                {
                    for (var y = 0; y <= LegacyGroundMaskTable.Height - 2; y++)
                    {
                        for (var x = 0; x <= LegacyGroundMaskTable.Width - 2; x++)
                        {
                            if (groundMaskTable[mapItem.GridCharge, mapItem.Rotate, y, x] == 0)
                                continue;

                            var worldX = mapItem.PositionX + x - 2;
                            var worldY = mapItem.PositionY + y - 2;
                            if (worldX < 1 || worldY < 1 || worldX > LegacyMapGrid.HeightWidth - 2 || worldY > LegacyMapGrid.HeightHeight - 2)
                                continue;

                            var actorId = GetParticipantAtPosition(worldX, worldY);
                            if (actorId == 0 || !TryGetDoorMobSnapshotLocked(actorId, out var mobSnapshot, out var actorX, out var actorY))
                                continue;

                            var itemAtFace = mobSnapshot.Length >= LegacyAccountSnapshot.MobEquipmentOffset + LegacyItem.SizeInBytes
                                ? LegacyItem.Read(mobSnapshot.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes))
                                : default;
                            if (itemAtFace.Index == 220)
                            {
                                var recipients = GetBattleRoyaleDoorMobRecipientsLocked(actorId, actorX, actorY, includeActor: false);
                                var partyEffect = DeleteBattleRoyaleDoorMobLocked(actorId);
                                actorEffects.Add(new LegacyBattleRoyaleDoorActorEffect(
                                    actorId,
                                    actorX,
                                    actorY,
                                    actorX,
                                    actorY,
                                    Deleted: true,
                                    recipients,
                                    partyEffect,
                                    RemoveDoorOwnedSummonsLocked(actorId)));
                                continue;
                            }

                            var targetX = worldX;
                            var targetY = worldY;
                            if (!LegacyMobGridSearch.TryFindEmpty(
                                    actorId,
                                    ref targetX,
                                    ref targetY,
                                    GetParticipantAtPosition,
                                    mapGrid.IsBlocked))
                                continue;

                            if (TrySetDoorMobPositionLocked(actorId, checked((short)targetX), checked((short)targetY)))
                            {
                                var recipients = GetBattleRoyaleDoorMobRecipientsLocked(actorId, targetX, targetY);
                                actorEffects.Add(new LegacyBattleRoyaleDoorActorEffect(
                                    actorId,
                                    actorX,
                                    actorY,
                                    checked((short)targetX),
                                    checked((short)targetY),
                                    Deleted: false,
                                    recipients,
                                    PartyEffect: null,
                                    DespawnedSummons: []));
                            }
                        }
                    }
                }

                updatedGates.Add(new LegacyBattleRoyaleDoorGateUpdate(
                    mapItem,
                    Array.AsReadOnly(GetParticipantIdsInViewLocked(mapItem.PositionX, mapItem.PositionY)),
                    Array.AsReadOnly(actorEffects.ToArray())));
            }

            return new LegacyBattleRoyaleDoorUpdate(Array.AsReadOnly(updatedGates.ToArray()));
        }
    }

    /// <summary>Registers a ground item and captures its inclusive 33x33 recipients under the same world lock.</summary>
    public LegacyGroundItemSpawn? TryEnterGroundItemAndCaptureView(LegacyItem item, short positionX, short positionY, int rotate)
    {
        if (item.Index <= 0)
            throw new ArgumentOutOfRangeException(nameof(item), "A ground item must have a positive legacy index.");
        if (rotate is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(rotate), "A Battle Royale altar item rotation must be between zero and three.");

        lock (gate)
        {
            var itemId = AllocateGroundItemIdLocked(0);
            if (itemId == 0)
                return null;

            var groundItem = new LegacyWorldItem(itemId, positionX, positionY, item, rotate, Delay: 90);
            groundItems.Add(itemId, groundItem);
            return new LegacyGroundItemSpawn(
                groundItem,
                itemId + LegacyWorldItem.WireIdOffset,
                GetParticipantIdsInViewLocked(positionX, positionY));
        }
    }

    /// <summary>
    /// Creates one timed dungeon-event prize using the legacy empty-item-grid
    /// search, including static map items, dynamic drops, and blocked terrain.
    /// </summary>
    public LegacyGroundItemSpawn? TryEnterDungeonEventGroundItemAndCaptureView(
        LegacyItem item,
        ushort requestedGridX,
        ushort requestedGridY,
        int rotate)
    {
        if (item.Index <= 0 || item.Index >= LegacyItemDataTable.MaxItemIndex)
            return null;
        if (rotate is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(rotate), "A dungeon event item rotation must be between zero and three.");
        if (requestedGridX >= LegacyWorldItem.MaxGridCoordinate || requestedGridY >= LegacyWorldItem.MaxGridCoordinate)
            return null;

        lock (gate)
        {
            if (!TryFindEmptyGroundGridLocked(requestedGridX, requestedGridY, out var gridX, out var gridY, includeMapItems: true))
                return null;

            var itemId = AllocateGroundItemIdLocked(0);
            if (itemId == 0)
                return null;

            var groundItem = new LegacyWorldItem(itemId, gridX, gridY, item, rotate, Delay: 90);
            groundItems.Add(itemId, groundItem);
            return new LegacyGroundItemSpawn(
                groundItem,
                itemId + LegacyWorldItem.WireIdOffset,
                GetParticipantIdsInViewLocked(gridX, gridY));
        }
    }

    /// <summary>
    /// Reproduces the dynamic-mode portion of legacy <c>ProcessDecayItem</c>.
    /// Ground drops are runtime-only: Delay starts at 90 and is decremented on
    /// every even-second slot; the following slot removes the item and sends
    /// MSG_DecayItem to the players who were inside its 33x33 view.
    /// </summary>
    public bool TryProcessGroundItemSecond(DateTime localNow, out LegacyGroundItemDecayPlan? plan)
    {
        lock (gate)
        {
            plan = null;
            if ((localNow.Second % 2) != 0)
                return false;

            var secondSlot = new DateTime(localNow.Year, localNow.Month, localNow.Day, localNow.Hour, localNow.Minute, localNow.Second, localNow.Kind);
            if (lastGroundItemSecond == secondSlot)
                return false;
            lastGroundItemSecond = secondSlot;

            var removedItems = new List<LegacyGroundItemDecay>();
            foreach (var groundItem in groundItems.Values.OrderBy(static item => item.ItemId).ToArray())
            {
                if (groundItem.Delay >= 1)
                {
                    groundItems[groundItem.ItemId] = groundItem with { Delay = groundItem.Delay - 1 };
                    continue;
                }

                var recipients = participants.Values
                    .Where(participant => participant.PositionX >= groundItem.PositionX - 16 && participant.PositionX <= groundItem.PositionX + 16 &&
                                          participant.PositionY >= groundItem.PositionY - 16 && participant.PositionY <= groundItem.PositionY + 16)
                    .Select(static participant => participant.ConnectionId)
                    .ToArray();
                groundItems.Remove(groundItem.ItemId);
                removedItems.Add(new LegacyGroundItemDecay(groundItem, recipients));
            }

            if (removedItems.Count > 0)
                plan = new LegacyGroundItemDecayPlan(secondSlot, removedItems);
            return true;
        }
    }

    /// <summary>Resolves the legacy pickup request and moves exactly one ground item into the requested carry slot.</summary>
    public LegacyGetItemResult TryGetGroundItem(int connectionId, int destinationType, int destinationSlot, int wireItemId, ushort gridX, ushort gridY, out LegacyGetItemOutcome? outcome)
    {
        outcome = null;
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyGetItemResult.ParticipantNotFound;
            if (participant.Mob.Length < LegacyAccountSnapshot.MobCarryOffset + (LegacyAccountSnapshot.MobCarryCount * LegacyItem.SizeInBytes))
                return LegacyGetItemResult.CombatStateUnavailable;
            var currentScore = LegacyMobCombatState.Read(participant.Mob).CurrentScore;
            if (currentScore.Hp <= 0)
                return LegacyGetItemResult.AttackerNotAlive;
            if (destinationType != LegacyWorldItem.CarryDestinationType)
                return LegacyGetItemResult.InvalidDestinationType;
            if (destinationSlot is < 0 or >= LegacyAccountSnapshot.MobCarryCount)
                return LegacyGetItemResult.InvalidDestinationSlot;

            var itemId = wireItemId - LegacyWorldItem.WireIdOffset;
            if (itemId <= 0 || !groundItems.TryGetValue(itemId, out var groundItem))
                return LegacyGetItemResult.GroundItemNotFound;
            if (Math.Abs(participant.PositionX - groundItem.PositionX) > 3 || Math.Abs(participant.PositionY - groundItem.PositionY) > 3)
                return LegacyGetItemResult.OutOfRange;
            if (gridX != groundItem.PositionX || gridY != groundItem.PositionY)
                return LegacyGetItemResult.GridMismatch;

            var destinationOffset = LegacyAccountSnapshot.MobCarryOffset + (destinationSlot * LegacyItem.SizeInBytes);
            if (LegacyItem.Read(participant.Mob.AsSpan(destinationOffset, LegacyItem.SizeInBytes)).Index != 0)
                return LegacyGetItemResult.DestinationOccupied;

            groundItem.Item.Write(participant.Mob.AsSpan(destinationOffset, LegacyItem.SizeInBytes));
            groundItems.Remove(itemId);
            outcome = new LegacyGetItemOutcome(
                connectionId,
                checked((short)wireItemId),
                groundItem.PositionX,
                groundItem.PositionY,
                destinationType,
                destinationSlot,
                groundItem.Item,
                participant.Mob.ToArray());
            return LegacyGetItemResult.Accepted;
        }
    }

    /// <summary>
    /// Resolves the carry-only portion of legacy <c>Exec_MSG_DropItem</c>.
    /// Cargo is deliberately rejected until the connected participant carries
    /// the account cargo state as well as the MOB snapshot.
    /// </summary>
    public LegacyDropItemResult TryDropItem(int connectionId, int sourceType, int sourceSlot, int rotate, ushort requestedGridX, ushort requestedGridY, out LegacyDropItemOutcome? outcome)
    {
        outcome = null;
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyDropItemResult.ParticipantNotFound;
            if (participant.Mob.Length < LegacyAccountSnapshot.MobCarryOffset + (LegacyAccountSnapshot.MobCarryCount * LegacyItem.SizeInBytes))
                return LegacyDropItemResult.CombatStateUnavailable;
            if (LegacyMobCombatState.Read(participant.Mob).CurrentScore.Hp <= 0)
                return LegacyDropItemResult.AttackerNotAlive;
            if (requestedGridX >= LegacyWorldItem.MaxGridCoordinate || requestedGridY >= LegacyWorldItem.MaxGridCoordinate)
                return LegacyDropItemResult.InvalidGrid;
            if (sourceType != LegacyWorldItem.CarryDestinationType)
                return LegacyDropItemResult.UnsupportedSourceType;
            if (sourceSlot is < 0 or >= LegacyAccountSnapshot.MobCarryCount)
                return LegacyDropItemResult.InvalidSourceSlot;

            var sourceOffset = LegacyAccountSnapshot.MobCarryOffset + (sourceSlot * LegacyItem.SizeInBytes);
            var item = LegacyItem.Read(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            if (item.Index <= 0 || item.Index >= LegacyItemDataTable.MaxItemIndex)
                return LegacyDropItemResult.InvalidItem;
            if (LegacyWorldItem.ProtectedDropItemIndexes.Contains(item.Index))
                return LegacyDropItemResult.ProtectedItem;
            if (!TryFindEmptyGroundGridLocked(requestedGridX, requestedGridY, out var gridX, out var gridY))
                return LegacyDropItemResult.NoGroundSpace;

            var itemId = AllocateGroundItemIdLocked(0);
            if (itemId == 0)
                return LegacyDropItemResult.NoGroundSpace;

            participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes).Clear();
            groundItems.Add(itemId, new LegacyWorldItem(itemId, gridX, gridY, item, rotate, Delay: 90));
            outcome = new LegacyDropItemOutcome(
                connectionId,
                sourceType,
                sourceSlot,
                rotate,
                gridX,
                gridY,
                itemId + LegacyWorldItem.WireIdOffset,
                item,
                participant.Mob.ToArray());
            return LegacyDropItemResult.Accepted;
        }
    }

    /// <summary>Compatibility entry point retained for carry-only callers.</summary>
    public LegacyTradingItemResult TryTradeCarryItems(int connectionId, int sourceSlot, int destinationSlot, out LegacyTradingItemOutcome? outcome) =>
        TryTradeItems(connectionId, LegacyItemPlace.Carry, sourceSlot, LegacyItemPlace.Carry, destinationSlot, out outcome);

    /// <summary>
    /// Resolves the carry/equipment subset of 7.69 <c>Exec_MSG_TradingItem</c>.
    /// Cargo deliberately remains unavailable: it belongs to the account snapshot,
    /// not the live MOB participant, and needs explicit persistence before enabling.
    /// </summary>
    public LegacyTradingItemResult TryTradeItems(int connectionId, int sourcePlace, int sourceSlot, int destinationPlace, int destinationSlot, out LegacyTradingItemOutcome? outcome)
    {
        outcome = null;
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyTradingItemResult.ParticipantNotFound;
            if (participant.Mob.Length < LegacyAccountSnapshot.MobCarryOffset + (LegacyAccountSnapshot.MobCarryCount * LegacyItem.SizeInBytes))
                return LegacyTradingItemResult.CombatStateUnavailable;
            if (LegacyMobCombatState.Read(participant.Mob).CurrentScore.Hp <= 0)
                return LegacyTradingItemResult.AttackerNotAlive;

            if (sourcePlace == LegacyItemPlace.Cargo || destinationPlace == LegacyItemPlace.Cargo)
                return LegacyTradingItemResult.CargoUnavailable;
            if (!IsSupportedLiveItemPlace(sourcePlace) || !IsSupportedLiveItemPlace(destinationPlace))
                return LegacyTradingItemResult.InvalidPlace;
            if (!IsValidTradeSlot(participant, sourcePlace, sourceSlot, isSource: true) ||
                !IsValidTradeSlot(participant, destinationPlace, destinationSlot, isSource: false))
                return LegacyTradingItemResult.InvalidSlot;
            if (sourcePlace == destinationPlace && sourceSlot == destinationSlot)
                return LegacyTradingItemResult.SameSlot;

            var source = ReadLiveItem(participant, sourcePlace, sourceSlot);
            if (source.Index == 0)
                return LegacyTradingItemResult.SourceEmpty;

            var destination = ReadLiveItem(participant, destinationPlace, destinationSlot);
            if ((destinationPlace == LegacyItemPlace.Equip && source.Index != 0) ||
                (sourcePlace == LegacyItemPlace.Equip && destination.Index != 0))
            {
                if (itemData is null)
                    return LegacyTradingItemResult.ItemDataUnavailable;

                var equipment = ReadEquipment(participant);
                var state = LegacyMobCombatState.Read(participant.Mob);
                var mortalFace = participant.MobExtra.Length >= LegacyAccountSnapshot.MobExtraMortalFaceOffset + sizeof(short)
                    ? BinaryPrimitives.ReadInt16LittleEndian(participant.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraMortalFaceOffset))
                    : 0;
                if (destinationPlace == LegacyItemPlace.Equip && !LegacyEquipmentRules.Evaluate(source, state.CurrentScore, destinationSlot, state.CharacterClass, participant.ClassMaster, mortalFace, equipment, itemData).Allowed ||
                    sourcePlace == LegacyItemPlace.Equip && !LegacyEquipmentRules.Evaluate(destination, state.CurrentScore, sourceSlot, state.CharacterClass, participant.ClassMaster, mortalFace, equipment, itemData).Allowed)
                    return LegacyTradingItemResult.EquipmentRejected;
            }

            var save1 = source;
            var save2 = destination;
            var merged = false;

            if (sourcePlace == LegacyItemPlace.Carry && destinationPlace == LegacyItemPlace.Carry &&
                source.Index == destination.Index && IsLegacyStackableItem(source.Index) &&
                GetLegacyItemAmount(source) < 120 && GetLegacyItemAmount(destination) < 120)
            {
                var sourceAmount = GetLegacyItemAmount(source);
                var destinationAmount = GetLegacyItemAmount(destination);
                var totalAmount = (sourceAmount <= 0 ? 1 : sourceAmount) + (destinationAmount <= 0 ? 1 : destinationAmount);

                // The reference forces EF_AMOUNT into effect 0 for save1,
                // caps it at 120 and leaves any overflow in save2.
                save1 = source with { Effect1 = 61, Value1 = checked((byte)Math.Min(totalAmount, 120)) };
                if (totalAmount <= 119)
                    save2 = default;
                else
                {
                    var overflowAmount = totalAmount - 120;
                    save2 = overflowAmount >= 1
                        ? SetLegacyItemAmount(destination, Math.Min(overflowAmount, 120))
                        : default;
                }
                merged = true;
            }

            var sourceAfter = save2;
            var destinationAfter = save1;
            WriteLiveItem(participant, sourcePlace, sourceSlot, sourceAfter);
            WriteLiveItem(participant, destinationPlace, destinationSlot, destinationAfter);
            outcome = new LegacyTradingItemOutcome(
                connectionId,
                sourcePlace,
                sourceSlot,
                destinationPlace,
                destinationSlot,
                sourceAfter,
                destinationAfter,
                merged,
                participant.Mob.ToArray());
            return LegacyTradingItemResult.Accepted;
        }
    }

    /// <summary>
    /// Reproduces the carry-only portion of legacy <c>Exec_MSG_SplitItem</c>.
    /// The client-provided item index is deliberately not trusted because the
    /// reference handler also ignores that field and reads the authoritative
    /// item from the source carry slot.
    /// </summary>
    public LegacySplitItemResult TrySplitCarryItem(int connectionId, int sourceSlot, int requestedItemIndex, int quantity, out LegacySplitItemOutcome? outcome)
    {
        outcome = null;
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacySplitItemResult.ParticipantNotFound;
            if (participant.Mob.Length < LegacyAccountSnapshot.MobCarryOffset + (LegacyAccountSnapshot.MobCarryCount * LegacyItem.SizeInBytes))
                return LegacySplitItemResult.CombatStateUnavailable;
            if (sourceSlot < 0 || sourceSlot >= LegacyAccountSnapshot.MobCarryCount - 4)
                return LegacySplitItemResult.InvalidSlot;
            if (quantity <= 0 || quantity >= 120)
                return LegacySplitItemResult.InvalidQuantity;

            var source = ReadCarryItem(participant, sourceSlot);
            if (source.Index == 0)
                return LegacySplitItemResult.SourceEmpty;
            if (!IsLegacyStackableItem(source.Index))
                return LegacySplitItemResult.UnsupportedItem;

            var amount = GetLegacyItemAmount(source);
            if (amount == 0 || amount == 1 || amount <= quantity)
                return LegacySplitItemResult.InsufficientAmount;
            if (!TryFindEmptyCarrySlotLocked(participant, out var destinationSlot))
                return LegacySplitItemResult.InventoryFull;

            var updatedSource = SetLegacyItemAmount(source, amount - quantity);
            updatedSource.Write(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (sourceSlot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));

            // The legacy code zeroes STRUCT_ITEM before copying only sIndex and
            // the requested amount. Preserve that behavior instead of cloning
            // effects from the source item.
            var splitItem = SetLegacyItemAmount(new LegacyItem(source.Index, 0, 0, 0, 0, 0, 0), quantity);
            splitItem.Write(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (destinationSlot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
            outcome = new LegacySplitItemOutcome(
                connectionId,
                sourceSlot,
                destinationSlot,
                requestedItemIndex,
                quantity,
                updatedSource,
                splitItem,
                participant.Mob.ToArray());
            return LegacySplitItemResult.Accepted;
        }
    }

    private bool TryFindEmptyGroundGridLocked(
        ushort requestedGridX,
        ushort requestedGridY,
        out short gridX,
        out short gridY,
        bool includeMapItems = false)
    {
        bool IsAvailable(int x, int y) =>
            x >= 0 && y >= 0 && x < LegacyWorldItem.MaxGridCoordinate && y < LegacyWorldItem.MaxGridCoordinate &&
            (mapGrid is null || !mapGrid.IsBlocked(x, y)) &&
            (!includeMapItems || !mapItems.Values.Any(item => item.PositionX == x && item.PositionY == y)) &&
            !groundItems.Values.Any(item => item.PositionX == x && item.PositionY == y);

        if (IsAvailable(requestedGridX, requestedGridY))
        {
            gridX = checked((short)requestedGridX);
            gridY = checked((short)requestedGridY);
            return true;
        }

        for (var y = requestedGridY - 1; y <= requestedGridY + 1; y++)
        for (var x = requestedGridX - 1; x <= requestedGridX + 1; x++)
            if (IsAvailable(x, y))
            {
                gridX = checked((short)x);
                gridY = checked((short)y);
                return true;
            }

        gridX = 0;
        gridY = 0;
        return false;
    }

    private int AllocateGroundItemIdLocked(int requestedItemId)
    {
        bool IsAvailable(int itemId) =>
            itemId > 0 && itemId < LegacyMapItemCatalog.MaxMapItems &&
            !mapItems.ContainsKey(itemId) && !groundItems.ContainsKey(itemId);

        if (requestedItemId > 0)
        {
            return IsAvailable(requestedItemId) ? requestedItemId : 0;
        }

        for (var itemId = 1; itemId < LegacyMapItemCatalog.MaxMapItems; itemId++)
        {
            if (!IsAvailable(itemId))
                continue;
            return itemId;
        }

        return 0;
    }

    public bool SetNpcCombatState(int connectionId, int mode, int currentTarget, IReadOnlyList<int> enemyList)
    {
        ArgumentNullException.ThrowIfNull(enemyList);
        lock (gate)
        {
            if (!npcs.TryGetValue(connectionId, out var npc))
                return false;
            npcs[connectionId] = npc with { Mode = mode, CurrentTarget = currentTarget, EnemyList = enemyList.ToArray() };
            return true;
        }
    }

    /// <summary>Applies the locked SetBattle subset and its existing party-summon effect to a live generated NPC.</summary>
    public bool TrySetNpcBattle(int npcConnectionId, int targetConnectionId)
        => TrySetNpcBattle(npcConnectionId, targetConnectionId, propagateTargetPartySummons: true);

    private bool TrySetNpcBattle(
        int npcConnectionId,
        int targetConnectionId,
        bool propagateTargetPartySummons)
    {
        lock (gate)
        {
            if (npcConnectionId is < LegacyMobAiPlanner.MaximumPlayerId or >= LegacyMobAiPlanner.MaximumMobId ||
                targetConnectionId <= 0 || npcConnectionId == targetConnectionId ||
                !npcs.TryGetValue(npcConnectionId, out var npc) || npc.GenerateIndex < 0 ||
                npc.AutoTradeSnapshot is not null || npc.Mode == LegacyNpcMode.Empty ||
                npc.MobSnapshot.Length != LegacyAccountSnapshot.CharacterStride ||
                LegacyMobCombatState.Read(npc.MobSnapshot).CurrentScore.Hp <= 0 ||
                !TryGetBattleTargetLocked(targetConnectionId, out var targetX, out var targetY, out var targetMob, out var targetPartyLeader))
                return false;

            if (Math.Abs(targetX - npc.PositionX) > 23 || Math.Abs(targetY - npc.PositionY) > 23)
                return false;

            // SetBattle enters combat before AddEnemyList's player-only filter.
            npc = npc with { Mode = LegacyNpcMode.Combat, EnemyList = NormalizeNpcEnemyList(npc.EnemyList) };
            npcs[npcConnectionId] = npc;
            if (!IsFilteredBattlePlayer(targetConnectionId, targetMob))
                AddNpcEnemyLocked(npcConnectionId, targetConnectionId);

            if (propagateTargetPartySummons && targetPartyLeader > 0)
            {
                foreach (var summon in summons.Values.Where(summon => summon.LeaderConnectionId == targetPartyLeader).ToArray())
                {
                    if (!IsLiveSummonLocked(summon))
                    {
                        RemoveSummonLocked(summon.ConnectionId);
                        continue;
                    }

                    // Each party summon runs SetBattle from its own grid position.
                    // Keep its inclusive source rectangle and AddEnemyList's MAX_USER boundary.
                    if (Math.Abs(npc.PositionX - summon.PositionX) > 23 || Math.Abs(npc.PositionY - summon.PositionY) > 23 ||
                        IsFilteredBattlePlayer(npcConnectionId, npc.MobSnapshot))
                        continue;

                    AddSummonEnemyLocked(summon.ConnectionId, npcConnectionId);
                }
            }

            return true;
        }
    }

    /// <summary>Captures copied authoritative state for deterministic generated-NPC planning.</summary>
    public bool TryCreateGeneratedNpcAiSnapshot(
        int npcConnectionId,
        int attackRange,
        LegacyMobAiPoint? routeStep,
        int randomIntRoll,
        int dexterityRoll,
        out LegacyNpcAiPlanningSnapshot? snapshot,
        int physicalDamageRandomFactor = 99,
        int attackMissRoll = 100,
        int skillBarSelectionRoll = 0,
        int skillBarMotionRoll = 0)
    {
        if (physicalDamageRandomFactor is < 99 or > 110)
            throw new ArgumentOutOfRangeException(nameof(physicalDamageRandomFactor), physicalDamageRandomFactor,
                "The 7.69 BASE_GetDamage random factor must be between 99 and 110.");
        if (attackMissRoll is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(attackMissRoll), attackMissRoll,
                "The 7.69 NPC attack miss roll must be between 1 and 100.");
        if (skillBarSelectionRoll is < 0 or >= 100)
            throw new ArgumentOutOfRangeException(nameof(skillBarSelectionRoll), skillBarSelectionRoll,
                "The 7.69 NPC SkillBar selection roll must be between 0 and 99.");
        if (skillBarMotionRoll is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(skillBarMotionRoll), skillBarMotionRoll,
                "The 7.69 NPC SkillBar motion roll must be between 0 and 3.");

        lock (gate)
        {
            snapshot = null;
            if (!npcs.TryGetValue(npcConnectionId, out var npc) || npc.GenerateIndex < 0 ||
                npc.AutoTradeSnapshot is not null || !TryBuildGeneratedNpcAiInputLocked(npc, attackRange, routeStep,
                    randomIntRoll, dexterityRoll, out var input, skillBarSelectionRoll, skillBarMotionRoll))
                return false;
            snapshot = new LegacyNpcAiPlanningSnapshot(npcConnectionId, npc, input, npc.MobSnapshot, npc.AffectSnapshot,
                ComputeNpcAiWorldFingerprintLocked(), physicalDamageRandomFactor, attackMissRoll);
            return true;
        }
    }

    /// <summary>Captures a Peace decision and resolves its current route step using the authoritative grid.</summary>
    public bool TryCreateGeneratedNpcAiPeaceSnapshot(
        int npcConnectionId,
        Func<int> randomSegmentSeedRoll,
        out LegacyNpcAiPlanningSnapshot? snapshot)
    {
        ArgumentNullException.ThrowIfNull(randomSegmentSeedRoll);
        lock (gate)
        {
            snapshot = null;
            if (!npcs.TryGetValue(npcConnectionId, out var npc) || npc.GenerateIndex < 0 ||
                npc.AutoTradeSnapshot is not null || npc.Mode != LegacyNpcMode.Peace ||
                (npc.RouteType == 5 && !LegacyMobAiPlanner.IsRunequestLv6AreaDespawnCandidate(
                    npc.GenerateIndex, npc.PositionX, npc.PositionY)) ||
                npc.MobSnapshot.Length < LegacyAccountSnapshot.CharacterStride ||
                (npc.MobSnapshot[LegacyAccountSnapshot.MobClanOffset] == 4 &&
                 !LegacyMobAiPlanner.IsRunequestLv6AreaDespawnCandidate(npc.GenerateIndex, npc.PositionX, npc.PositionY)) ||
                !TryBuildGeneratedNpcAiInputLocked(npc, attackRange: 0, routeStep: null,
                    randomIntRoll: 0, dexterityRoll: 0, out var previewInput))
                return false;

            var preview = LegacyMobAiPlanner.Plan(previewInput);
            if (preview.Kind != LegacyMobAiDecisionKind.Aggro &&
                preview.TargetX is { } targetX && preview.TargetY is { } targetY &&
                LegacyMobAiMovementPlanner.TryBuildPeaceRouteStep(
                    npc,
                    targetX,
                    targetY,
                    mapGrid,
                    GetParticipantAtPosition,
                    randomSegmentSeedRoll,
                    out var movementPlan) &&
                movementPlan is not null)
            {
                if (!TryBuildGeneratedNpcAiInputLocked(
                        npc,
                        attackRange: 0,
                        new LegacyMobAiPoint(movementPlan.ToX, movementPlan.ToY),
                        randomIntRoll: 0,
                        dexterityRoll: 0,
                        out var movementInput))
                    return false;

                snapshot = new LegacyNpcAiPlanningSnapshot(
                    npcConnectionId,
                    npc,
                    movementInput,
                    npc.MobSnapshot,
                    npc.AffectSnapshot,
                    ComputeNpcAiWorldFingerprintLocked(),
                    physicalDamageRandomFactor: 99,
                    attackMissRoll: 100,
                    movementPlan);
                return true;
            }

            snapshot = new LegacyNpcAiPlanningSnapshot(
                npcConnectionId,
                npc,
                previewInput,
                npc.MobSnapshot,
                npc.AffectSnapshot,
                ComputeNpcAiWorldFingerprintLocked(),
                physicalDamageRandomFactor: 99,
                attackMissRoll: 100);
            return true;
        }
    }

    /// <summary>Applies the source timer's type-1 cleanup for dead generated NPCs in Idle or Peace mode.</summary>
    public bool TryRemoveDeadGeneratedNpc(
        int npcConnectionId,
        out LegacyNpcAiApplicationOutcome outcome)
    {
        lock (gate)
        {
            outcome = LegacyNpcAiApplicationOutcome.Empty(LegacyMobAiDecisionKind.Idle);
            if (!npcs.TryGetValue(npcConnectionId, out var npc) || npc.GenerateIndex < 0 ||
                npc.AutoTradeSnapshot is not null || npc.Mode is not (LegacyNpcMode.Idle or LegacyNpcMode.Peace) ||
                npc.MobSnapshot.Length < WorldHubLegacyOffsets.LegacyAccountMobSize ||
                LegacyMobCombatState.Read(npc.MobSnapshot).CurrentScore.Hp > 0)
                return false;

            var recipients = Array.AsReadOnly(GetParticipantIdsInViewLocked(npc.PositionX, npc.PositionY));
            if (!RemoveNpcLocked(npc.ConnectionId))
                return false;

            outcome = new LegacyNpcAiApplicationOutcome(
                LegacyNpcAiApplyStatus.Applied,
                LegacyMobAiDecisionKind.Idle,
                npc.PositionX,
                npc.PositionY,
                npc.Mode,
                npc.CurrentTarget,
                npc.EnemyList.ToArray(),
                npc.SegmentX,
                npc.SegmentY,
                npc.SegmentProgress,
                npc.SegmentDirection,
                npc.WaitSec,
                ActionText: null,
                Removal: new LegacyGeneratedNpcRemovalOutcome(
                    npc.ConnectionId,
                    recipients,
                    RemoveType: 1));
            return true;
        }
    }

    /// <summary>Processes the supported generated-NPC timer affect branches in Idle/Peace mode.</summary>
    public bool TryProcessGeneratedNpcTimerAffects(
        int npcConnectionId,
        out LegacyNpcAiApplicationOutcome outcome) =>
        TryProcessGeneratedNpcTimerAffects(npcConnectionId, processCombatOpportunity: false, out outcome);

    /// <summary>Processes generated-NPC timer affects once per four Combat shard visits.</summary>
    public bool TryProcessGeneratedNpcCombatTimerAffects(
        int npcConnectionId,
        out LegacyNpcAiApplicationOutcome outcome) =>
        TryProcessGeneratedNpcTimerAffects(npcConnectionId, processCombatOpportunity: true, out outcome);

    private bool TryProcessGeneratedNpcTimerAffects(
        int npcConnectionId,
        bool processCombatOpportunity,
        out LegacyNpcAiApplicationOutcome outcome)
    {
        const int affectSizeInBytes = 8;
        const byte lifeAuraType = 17;
        const byte poisonType = 20;
        const byte holyTouchType = 1;
        const byte frozenBladeType = 7;
        const byte pursuitType = 3;
        const byte weakenType = 10;
        const byte thunderType = 22;
        const byte bestialAuraType = 23;
        const byte mobLifetimeType = 24;
        const byte genericTimerOnlyAffectType = 32;
        // SkillData 246 uses Type 47; both C++ sources fall through to the generic affect timer.
        const byte bleedingAffectType = 47;
        const uint persistentAffectThreshold = 32_400_000;
        const byte combatAffectOpportunitiesPerTimerTick = 4;

        lock (gate)
        {
            outcome = LegacyNpcAiApplicationOutcome.Empty(LegacyMobAiDecisionKind.Idle);
            if (!npcs.TryGetValue(npcConnectionId, out var npc) || npc.GenerateIndex < 0 ||
                npc.AutoTradeSnapshot is not null ||
                (processCombatOpportunity
                    ? npc.Mode != LegacyNpcMode.Combat
                    : npc.Mode is not (LegacyNpcMode.Idle or LegacyNpcMode.Peace)) ||
                npc.MobSnapshot.Length < LegacyAccountSnapshot.CharacterStride ||
                npc.AffectSnapshot.Length != LegacyAccountSnapshot.AffectStride ||
                LegacyMobCombatState.Read(npc.MobSnapshot).CurrentScore.Hp <= 0)
                return false;

            if (processCombatOpportunity)
            {
                var nextPhase = (byte)(npc.CombatAffectOpportunityPhase + 1);
                if (nextPhase < combatAffectOpportunitiesPerTimerTick)
                {
                    npcs[npcConnectionId] = npc with { CombatAffectOpportunityPhase = nextPhase };
                    return false;
                }

                npc = npc with { CombatAffectOpportunityPhase = 0 };
                npcs[npcConnectionId] = npc;
            }

            var mob = npc.MobSnapshot.ToArray();
            var affect = npc.AffectSnapshot.ToArray();
            var pursuitBaseResistances = npc.PursuitBaseResistances;
            var holyTouchBaseScore = npc.HolyTouchBaseScore;
            var score = LegacyMobCombatState.Read(mob).CurrentScore;
            var hp = score.Hp;
            var hasProcessedAffect = false;
            var hasTimerAffect = false;
            var scoreRefreshRequired = false;
            var weakenAffectExpired = false;
            var holyTouchAffectExpired = false;
            var frozenBladeAffectExpired = false;
            var pursuitAffectExpired = false;
            var removeMob = false;
            var damage = 0;

            for (var index = 0; index < LegacyAccountSnapshot.AffectStride / affectSizeInBytes; index++)
            {
                var offset = index * affectSizeInBytes;
                var affectType = affect[offset];
                if (affectType is not (lifeAuraType or poisonType or holyTouchType or frozenBladeType or pursuitType or weakenType or thunderType or bestialAuraType or mobLifetimeType or genericTimerOnlyAffectType or bleedingAffectType))
                    continue;

                hasTimerAffect = true;
                if (affectType is lifeAuraType or poisonType)
                {
                    hasProcessedAffect = true;
                    var previousHp = hp;
                    if (affectType == lifeAuraType)
                    {
                        var affectLevel = BinaryPrimitives.ReadUInt16LittleEndian(affect.AsSpan(offset + 2));
                        hp += (affectLevel / 2) + affect[offset + 1];
                        if (hp < 1)
                            hp = 1;
                        if (hp > score.MaxHp)
                            hp = score.MaxHp;
                    }
                    else
                    {
                        var learnedSkill = BinaryPrimitives.ReadInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset));
                        var affectValue = 100;
                        if (npcConnectionId >= LegacyMobAiPlanner.MaximumPlayerId)
                            affectValue -= learnedSkill / LegacyMobAiPlanner.MaximumPlayerId;
                        affectValue /= 10;

                        var hpLose = (int)((100 - affectValue) / 100.0);
                        var poisonDamageCap = 500;
                        if (npcConnectionId >= LegacyMobAiPlanner.MaximumPlayerId)
                            poisonDamageCap = 10 * (100 - learnedSkill / LegacyMobAiPlanner.MaximumPlayerId);
                        var minimumRemainingHp = hp - poisonDamageCap;

                        hp = (int)((double)hp * hpLose);
                        if (hp < minimumRemainingHp)
                            hp = minimumRemainingHp;
                        if (hp < 1)
                            hp = 1;
                        if (hp > score.MaxHp)
                            hp = score.MaxHp;
                    }

                    if (hp != previousHp)
                        scoreRefreshRequired = true;
                    damage = hp - previousHp;
                }

                var remainingTime = BinaryPrimitives.ReadUInt32LittleEndian(affect.AsSpan(offset + 4));
                if (remainingTime < persistentAffectThreshold)
                    remainingTime = unchecked(remainingTime - 1);

                if (remainingTime == 0)
                {
                    weakenAffectExpired |= affectType == weakenType;
                    holyTouchAffectExpired |= affectType == holyTouchType;
                    frozenBladeAffectExpired |= affectType == frozenBladeType;
                    pursuitAffectExpired |= affectType == pursuitType;
                    affect.AsSpan(offset, affectSizeInBytes).Clear();
                    scoreRefreshRequired = true;
                    removeMob |= affectType == mobLifetimeType;
                }
                else
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(offset + 4), remainingTime);
                }
            }

            if (removeMob)
            {
                var removalRecipients = Array.AsReadOnly(GetParticipantIdsInViewLocked(npc.PositionX, npc.PositionY));
                if (!RemoveNpcLocked(npcConnectionId))
                    return false;

                outcome = new LegacyNpcAiApplicationOutcome(
                    LegacyNpcAiApplyStatus.Applied,
                    LegacyMobAiDecisionKind.Despawn,
                    npc.PositionX,
                    npc.PositionY,
                    npc.Mode,
                    npc.CurrentTarget,
                    npc.EnemyList.ToArray(),
                    npc.SegmentX,
                    npc.SegmentY,
                    npc.SegmentProgress,
                    npc.SegmentDirection,
                    npc.WaitSec,
                    ActionText: null,
                    Removal: new LegacyGeneratedNpcRemovalOutcome(
                        npcConnectionId,
                        removalRecipients,
                        RemoveType: 3));
                return true;
            }

            if (!hasProcessedAffect && !scoreRefreshRequired)
            {
                if (hasTimerAffect)
                    npcs[npcConnectionId] = npc with { AffectSnapshot = affect };
                return false;
            }

            (score with { Hp = hp }).Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
            if (weakenAffectExpired)
                ApplyNpcWeakenAffectDamageDelta(mob, npc.AffectSnapshot, affect);
            if ((holyTouchAffectExpired || frozenBladeAffectExpired) && itemData is not null)
            {
                ApplyNpcAffectScoreProjection(mob, affect, itemData);
                holyTouchBaseScore = null;
            }
            else
            {
                if (holyTouchAffectExpired)
                    holyTouchBaseScore = ApplyNpcHolyTouchAffectScoreTransition(
                        mob, npc.AffectSnapshot, affect, holyTouchBaseScore);
                if (frozenBladeAffectExpired)
                    RestoreNpcFrozenBladeBaseline(mob, npc.FrozenBladeBaseScore);
            }
            if (pursuitAffectExpired)
                pursuitBaseResistances = ApplyNpcPursuitAffectResistanceTransition(
                    mob, npc.AffectSnapshot, affect, pursuitBaseResistances);
            var recipients = Array.AsReadOnly(GetParticipantIdsInViewLocked(npc.PositionX, npc.PositionY));
            npcs[npcConnectionId] = npc with
            {
                MobSnapshot = mob.ToArray(),
                AffectSnapshot = affect.ToArray(),
                PursuitBaseResistances = pursuitBaseResistances,
                HolyTouchBaseScore = holyTouchBaseScore,
                FrozenBladeBaseScore = frozenBladeAffectExpired ? null : npc.FrozenBladeBaseScore,
            };
            outcome = new LegacyNpcAiApplicationOutcome(
                LegacyNpcAiApplyStatus.Applied,
                LegacyMobAiDecisionKind.Idle,
                npc.PositionX,
                npc.PositionY,
                npc.Mode,
                npc.CurrentTarget,
                npc.EnemyList.ToArray(),
                npc.SegmentX,
                npc.SegmentY,
                npc.SegmentProgress,
                npc.SegmentDirection,
                npc.WaitSec,
                ActionText: null)
            {
                AffectTick = new LegacyGeneratedNpcAffectTickOutcome(
                    npcConnectionId,
                    hp,
                    damage,
                    mob.ToArray(),
                    affect.ToArray(),
                    recipients,
                    scoreRefreshRequired)
                {
                    HpUpdateRequired = hasProcessedAffect,
                },
            };
            return true;
        }
    }

    /// <summary>Processes source Type 1/3/10/17/20/22/23/32 timer affects for an ordinary non-generated NPC on a Peace shard visit.</summary>
    public bool TryProcessNonGeneratedNpcTimerAffects(
        int npcConnectionId,
        out LegacyNpcAiApplicationOutcome outcome) =>
        TryProcessNonGeneratedNpcTimerAffects(npcConnectionId, processCombatOpportunity: false, out outcome);

    /// <summary>Processes source Type 1/3/10/17/20/22/23/32 timer affects for an ordinary non-generated NPC once per four Combat shard visits.</summary>
    public bool TryProcessNonGeneratedNpcCombatTimerAffects(
        int npcConnectionId,
        out LegacyNpcAiApplicationOutcome outcome) =>
        TryProcessNonGeneratedNpcTimerAffects(npcConnectionId, processCombatOpportunity: true, out outcome);

    private bool TryProcessNonGeneratedNpcTimerAffects(
        int npcConnectionId,
        bool processCombatOpportunity,
        out LegacyNpcAiApplicationOutcome outcome)
    {
        const int affectSizeInBytes = 8;
        const byte lifeAuraType = 17;
        const byte poisonType = 20;
        const byte holyTouchType = 1;
        const byte frozenBladeType = 7;
        const byte pursuitType = 3;
        const byte weakenType = 10;
        const byte thunderType = 22;
        const byte bestialAuraType = 23;
        const byte genericTimerOnlyAffectType = 32;
        // SkillData 246 uses Type 47; both C++ sources fall through to the generic affect timer.
        const byte bleedingAffectType = 47;
        const uint persistentAffectThreshold = 32_400_000;
        const byte combatAffectOpportunitiesPerTimerTick = 4;

        lock (gate)
        {
            outcome = LegacyNpcAiApplicationOutcome.Empty(LegacyMobAiDecisionKind.Idle);
            if (!npcs.TryGetValue(npcConnectionId, out var npc) || npc.GenerateIndex >= 0 ||
                npc.LeaderId > 0 || npc.AutoTradeSnapshot is not null ||
                (processCombatOpportunity
                    ? npc.Mode != LegacyNpcMode.Combat
                    : npc.Mode is not (LegacyNpcMode.Idle or LegacyNpcMode.Peace)) ||
                npc.MobSnapshot.Length < LegacyAccountSnapshot.CharacterStride ||
                npc.AffectSnapshot.Length != LegacyAccountSnapshot.AffectStride ||
                LegacyMobCombatState.Read(npc.MobSnapshot).CurrentScore.Hp <= 0)
                return false;

            if (processCombatOpportunity)
            {
                var nextPhase = (byte)(npc.CombatAffectOpportunityPhase + 1);
                if (nextPhase < combatAffectOpportunitiesPerTimerTick)
                {
                    npcs[npcConnectionId] = npc with { CombatAffectOpportunityPhase = nextPhase };
                    return false;
                }

                npc = npc with { CombatAffectOpportunityPhase = 0 };
                npcs[npcConnectionId] = npc;
            }

            var affect = npc.AffectSnapshot.ToArray();
            var mob = npc.MobSnapshot.ToArray();
            var pursuitBaseResistances = npc.PursuitBaseResistances;
            var holyTouchBaseScore = npc.HolyTouchBaseScore;
            var score = LegacyMobCombatState.Read(mob).CurrentScore;
            var hp = score.Hp;
            var damage = 0;
            var hasProcessedAffect = false;
            var hasSupportedAffect = false;
            var affectChanged = false;
            var scoreRefreshRequired = false;
            var weakenAffectExpired = false;
            var holyTouchAffectExpired = false;
            var frozenBladeAffectExpired = false;
            var pursuitAffectExpired = false;
            for (var offset = 0; offset < affect.Length; offset += affectSizeInBytes)
            {
                var affectType = affect[offset];
                if (affectType is not (lifeAuraType or poisonType or holyTouchType or frozenBladeType or pursuitType or weakenType or thunderType or bestialAuraType or genericTimerOnlyAffectType or bleedingAffectType))
                    continue;

                hasSupportedAffect = true;
                if (affectType is lifeAuraType or poisonType)
                {
                    hasProcessedAffect = true;
                    var previousHp = hp;
                    if (affectType == lifeAuraType)
                    {
                        var affectLevel = BinaryPrimitives.ReadUInt16LittleEndian(affect.AsSpan(offset + 2));
                        hp += (affectLevel / 2) + affect[offset + 1];
                        if (hp < 1)
                            hp = 1;
                        if (hp > score.MaxHp)
                            hp = score.MaxHp;
                    }
                    else
                    {
                        var learnedSkill = BinaryPrimitives.ReadInt32LittleEndian(
                            mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset));
                        var affectValue = 100;
                        if (npcConnectionId >= LegacyMobAiPlanner.MaximumPlayerId)
                            affectValue -= learnedSkill / LegacyMobAiPlanner.MaximumPlayerId;
                        affectValue /= 10;

                        var hpLose = (int)((100 - affectValue) / 100.0);
                        var poisonDamageCap = 500;
                        if (npcConnectionId >= LegacyMobAiPlanner.MaximumPlayerId)
                            poisonDamageCap = 10 * (100 - learnedSkill / LegacyMobAiPlanner.MaximumPlayerId);
                        var minimumRemainingHp = hp - poisonDamageCap;

                        hp = (int)((double)hp * hpLose);
                        if (hp < minimumRemainingHp)
                            hp = minimumRemainingHp;
                        if (hp < 1)
                            hp = 1;
                        if (hp > score.MaxHp)
                            hp = score.MaxHp;
                    }

                    damage = hp - previousHp;
                    if (hp != previousHp)
                        scoreRefreshRequired = true;
                }

                var remainingTime = BinaryPrimitives.ReadUInt32LittleEndian(affect.AsSpan(offset + 4));
                if (remainingTime < persistentAffectThreshold)
                    remainingTime = unchecked(remainingTime - 1);

                if (remainingTime == 0)
                {
                    weakenAffectExpired |= affectType == weakenType;
                    holyTouchAffectExpired |= affectType == holyTouchType;
                    frozenBladeAffectExpired |= affectType == frozenBladeType;
                    pursuitAffectExpired |= affectType == pursuitType;
                    affect.AsSpan(offset, affectSizeInBytes).Clear();
                    affectChanged = true;
                    scoreRefreshRequired = true;
                }
                else if (BinaryPrimitives.ReadUInt32LittleEndian(affect.AsSpan(offset + 4)) != remainingTime)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(offset + 4), remainingTime);
                    affectChanged = true;
                }
            }

            if (!hasProcessedAffect && !scoreRefreshRequired)
            {
                if (hasSupportedAffect && affectChanged)
                    npcs[npcConnectionId] = npc with { AffectSnapshot = affect };
                return false;
            }

            if (hasProcessedAffect)
                (score with { Hp = hp }).Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
            if (weakenAffectExpired)
                ApplyNpcWeakenAffectDamageDelta(mob, npc.AffectSnapshot, affect);
            if ((holyTouchAffectExpired || frozenBladeAffectExpired) && itemData is not null)
            {
                ApplyNpcAffectScoreProjection(mob, affect, itemData);
                holyTouchBaseScore = null;
            }
            else
            {
                if (holyTouchAffectExpired)
                    holyTouchBaseScore = ApplyNpcHolyTouchAffectScoreTransition(
                        mob, npc.AffectSnapshot, affect, holyTouchBaseScore);
                if (frozenBladeAffectExpired)
                    RestoreNpcFrozenBladeBaseline(mob, npc.FrozenBladeBaseScore);
            }
            if (pursuitAffectExpired)
                pursuitBaseResistances = ApplyNpcPursuitAffectResistanceTransition(
                    mob, npc.AffectSnapshot, affect, pursuitBaseResistances);

            var recipients = Array.AsReadOnly(GetParticipantIdsInViewLocked(npc.PositionX, npc.PositionY));
            npcs[npcConnectionId] = npc with
            {
                MobSnapshot = mob,
                AffectSnapshot = affect,
                PursuitBaseResistances = pursuitBaseResistances,
                HolyTouchBaseScore = holyTouchBaseScore,
                FrozenBladeBaseScore = frozenBladeAffectExpired ? null : npc.FrozenBladeBaseScore,
            };
            outcome = new LegacyNpcAiApplicationOutcome(
                LegacyNpcAiApplyStatus.Applied,
                LegacyMobAiDecisionKind.Idle,
                npc.PositionX,
                npc.PositionY,
                npc.Mode,
                npc.CurrentTarget,
                npc.EnemyList.ToArray(),
                npc.SegmentX,
                npc.SegmentY,
                npc.SegmentProgress,
                npc.SegmentDirection,
                npc.WaitSec,
                ActionText: null)
            {
                AffectTick = new LegacyGeneratedNpcAffectTickOutcome(
                    npcConnectionId,
                    hp,
                    damage,
                    mob,
                    affect,
                    recipients,
                    scoreRefreshRequired)
                {
                    HpUpdateRequired = hasProcessedAffect,
                },
            };
            return true;
        }
    }

    /// <summary>Captures a combat plan and resolves its chase or leash-return step using the current world grid.</summary>
    public bool TryCreateGeneratedNpcAiChaseSnapshot(
        int npcConnectionId,
        int attackRange,
        int randomIntRoll,
        int dexterityRoll,
        Func<int> randomFallbackOffset,
        Func<int> randomReturnSeedRoll,
        out LegacyNpcAiPlanningSnapshot? snapshot,
        int physicalDamageRandomFactor = 99,
        int attackMissRoll = 100,
        int skillBarSelectionRoll = 0,
        int skillBarMotionRoll = 0,
        Func<int>? randomRetreatStepRoll = null)
    {
        ArgumentNullException.ThrowIfNull(randomFallbackOffset);
        ArgumentNullException.ThrowIfNull(randomReturnSeedRoll);
        randomRetreatStepRoll ??= static () => 0;
        if (physicalDamageRandomFactor is < 99 or > 110)
            throw new ArgumentOutOfRangeException(nameof(physicalDamageRandomFactor), physicalDamageRandomFactor,
                "The 7.69 BASE_GetDamage random factor must be between 99 and 110.");
        if (attackMissRoll is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(attackMissRoll), attackMissRoll,
                "The 7.69 NPC attack miss roll must be between 1 and 100.");
        if (skillBarSelectionRoll is < 0 or >= 100)
            throw new ArgumentOutOfRangeException(nameof(skillBarSelectionRoll), skillBarSelectionRoll,
                "The 7.69 NPC SkillBar selection roll must be between 0 and 99.");
        if (skillBarMotionRoll is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(skillBarMotionRoll), skillBarMotionRoll,
                "The 7.69 NPC SkillBar motion roll must be between 0 and 3.");

        lock (gate)
        {
            snapshot = null;
            if (!npcs.TryGetValue(npcConnectionId, out var npc) || npc.GenerateIndex < 0 ||
                npc.AutoTradeSnapshot is not null ||
                (npc.RouteType == 5 && npc.LeaderId is > 0 and < LegacyMobAiPlanner.MaximumPlayerId &&
                 !LegacyMobAiPlanner.IsRunequestLv6AreaDespawnCandidate(npc.GenerateIndex, npc.PositionX, npc.PositionY)) ||
                !TryBuildGeneratedNpcAiInputLocked(
                    npc,
                    attackRange,
                    new LegacyMobAiPoint(npc.PositionX, npc.PositionY),
                    randomIntRoll,
                    dexterityRoll,
                    out var previewInput,
                    skillBarSelectionRoll,
                    skillBarMotionRoll))
                return false;

            var preview = LegacyMobAiPlanner.Plan(previewInput);
            LegacyNpcAiMovementPlan? movementPlan = null;
            LegacyMobAiInput input;
            if (preview.Kind == LegacyMobAiDecisionKind.Move && preview.TargetX is { } targetX && preview.TargetY is { } targetY)
            {
                if (npc.MobSnapshot[LegacyAccountSnapshot.MobClanOffset] == 4 ||
                    !LegacyMobAiMovementPlanner.TryBuildChaseStep(
                        npc,
                        targetX,
                        targetY,
                        mapGrid,
                        GetParticipantAtPosition,
                        randomFallbackOffset,
                        out movementPlan) || movementPlan is null ||
                    !TryBuildGeneratedNpcAiInputLocked(
                        npc,
                        attackRange,
                        new LegacyMobAiPoint(movementPlan.ToX, movementPlan.ToY),
                        randomIntRoll,
                        dexterityRoll,
                        out input,
                        skillBarSelectionRoll,
                        skillBarMotionRoll))
                    return false;
            }
            else if (preview.Kind == LegacyMobAiDecisionKind.Retreat &&
                     preview.TargetX is { } retreatTargetX && preview.TargetY is { } retreatTargetY)
            {
                if (!LegacyMobAiMovementPlanner.TryBuildRetreatStep(
                        npc,
                        retreatTargetX,
                        retreatTargetY,
                        mapGrid,
                        GetParticipantAtPosition,
                        randomRetreatStepRoll,
                        out movementPlan) || movementPlan is null ||
                    !TryBuildGeneratedNpcAiInputLocked(
                        npc,
                        attackRange,
                        new LegacyMobAiPoint(movementPlan.ToX, movementPlan.ToY),
                        randomIntRoll,
                        dexterityRoll,
                        out input,
                        skillBarSelectionRoll,
                        skillBarMotionRoll))
                    return false;
            }
            else if (preview.Kind == LegacyMobAiDecisionKind.ReturnToRoute)
            {
                if (npc.MobSnapshot[LegacyAccountSnapshot.MobClanOffset] != 4 &&
                    LegacyMobAiMovementPlanner.TryBuildReturnToRouteStep(
                        npc,
                        mapGrid,
                        GetParticipantAtPosition,
                        randomReturnSeedRoll,
                        out movementPlan) &&
                    movementPlan is not null)
                {
                    if (!TryBuildGeneratedNpcAiInputLocked(
                            npc,
                            attackRange,
                            new LegacyMobAiPoint(movementPlan.ToX, movementPlan.ToY),
                            randomIntRoll,
                            dexterityRoll,
                            out input,
                            skillBarSelectionRoll,
                            skillBarMotionRoll))
                        return false;
                }
                else if (!TryBuildGeneratedNpcAiInputLocked(
                             npc,
                             attackRange,
                             routeStep: null,
                             randomIntRoll,
                             dexterityRoll,
                             out input,
                             skillBarSelectionRoll,
                             skillBarMotionRoll))
                {
                    return false;
                }
            }
            else if (!TryBuildGeneratedNpcAiInputLocked(
                         npc,
                         attackRange,
                         routeStep: null,
                         randomIntRoll,
                         dexterityRoll,
                         out input,
                         skillBarSelectionRoll,
                         skillBarMotionRoll))
            {
                return false;
            }

            snapshot = new LegacyNpcAiPlanningSnapshot(
                npcConnectionId,
                npc,
                input,
                npc.MobSnapshot,
                npc.AffectSnapshot,
                ComputeNpcAiWorldFingerprintLocked(),
                physicalDamageRandomFactor,
                attackMissRoll,
                movementPlan);
            return true;
        }
    }

    /// <summary>
    /// Revalidates and replans under the WorldHub gate before applying a decision.
    /// Attack supports the audited non-Clan-4 all-255 physical branch and SkillBar subsets.
    /// </summary>
    public LegacyNpcAiApplyStatus TryApplyGeneratedNpcAiDecision(
        LegacyNpcAiPlanningSnapshot snapshot,
        LegacyMobAiDecision decision,
        out LegacyNpcAiApplicationOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(decision);
        lock (gate)
        {
            outcome = LegacyNpcAiApplicationOutcome.Empty(decision.Kind);
            if (!npcs.TryGetValue(snapshot.NpcConnectionId, out var npc) || !ReferenceEquals(npc, snapshot.InstanceToken) || npc.GenerateIndex < 0 ||
                npc.AutoTradeSnapshot is not null || !snapshot.WorldFingerprint.SequenceEqual(ComputeNpcAiWorldFingerprintLocked()) ||
                !TryBuildGeneratedNpcAiInputLocked(npc, snapshot.AttackRange, snapshot.RouteStep,
                    snapshot.RandomIntRoll, snapshot.DexterityRoll, out var currentInput,
                    snapshot.SkillBarSelectionRoll, snapshot.SkillBarMotionRoll))
                return LegacyNpcAiApplyStatus.Stale;

            var currentDecision = LegacyMobAiPlanner.Plan(currentInput);
            if (!DecisionsEqual(currentDecision, decision))
                return LegacyNpcAiApplyStatus.Stale;
            if (currentDecision.Kind == LegacyMobAiDecisionKind.Unsupported)
                return LegacyNpcAiApplyStatus.Unsupported;
            if (snapshot.MovementPlan is { } capturedMovement &&
                (currentDecision.Kind is not (LegacyMobAiDecisionKind.Move or LegacyMobAiDecisionKind.ReturnToRoute or LegacyMobAiDecisionKind.Retreat) ||
                 currentDecision.NextX != capturedMovement.ToX || currentDecision.NextY != capturedMovement.ToY ||
                 npc.PositionX != capturedMovement.FromX || npc.PositionY != capturedMovement.FromY))
                return LegacyNpcAiApplyStatus.Stale;

            if (currentDecision.Kind == LegacyMobAiDecisionKind.Despawn)
            {
                var recipients = Array.AsReadOnly(GetParticipantIdsInViewLocked(npc.PositionX, npc.PositionY));
                if (!RemoveNpcLocked(npc.ConnectionId))
                    return LegacyNpcAiApplyStatus.Stale;

                var incrementsPistaMobCount = currentDecision.DespawnReason switch
                {
                    LegacyMobAiDespawnReason.RouteTypeThreeTerminal =>
                        npc.GenerateIndex is >= PistaLv6DespawnInitialGenerateIndex and <= PistaLv6DespawnEndGenerateIndex,
                    LegacyMobAiDespawnReason.RunequestLv6Area =>
                        npc.GenerateIndex is >= LegacyMobAiPlanner.RunequestLv6SecondWaveInitialGenerateIndex and
                            <= LegacyMobAiPlanner.RunequestLv6SecondWaveEndGenerateIndex,
                    _ => false
                };
                if (incrementsPistaMobCount && pistaLv6State is { MobCount: > 0 and < 100 } pistaState)
                    pistaLv6State = pistaState with { MobCount = pistaState.MobCount + 1 };

                outcome = new LegacyNpcAiApplicationOutcome(
                    LegacyNpcAiApplyStatus.Applied,
                    currentDecision.Kind,
                    npc.PositionX,
                    npc.PositionY,
                    npc.Mode,
                    npc.CurrentTarget,
                    npc.EnemyList.ToArray(),
                    npc.SegmentX,
                    npc.SegmentY,
                    npc.SegmentProgress,
                    npc.SegmentDirection,
                    npc.WaitSec,
                    currentDecision.ActionText,
                    Removal: new LegacyGeneratedNpcRemovalOutcome(
                        npc.ConnectionId,
                        recipients,
                        RemoveType: 3));
                return LegacyNpcAiApplyStatus.Applied;
            }

            var wasPeaceMode = npc.Mode == LegacyNpcMode.Peace;
            LegacyGeneratedNpcPhysicalAttackOutcome? attackOutcome = null;
            LegacyGeneratedNpcMovementOutcome? movementOutcome = null;
            LegacyGeneratedNpcRemovalOutcome? removalOutcome = null;
            IReadOnlyList<LegacyGeneratedNpcRemovalOutcome> additionalRemovals = [];
            if (currentDecision.Kind == LegacyMobAiDecisionKind.Attack &&
                currentDecision.AttackTargetId is { } npcAttackTargetId)
            {
                if (currentDecision.Attack is not { } attackFields ||
                    npc.MobSnapshot[LegacyAccountSnapshot.MobClanOffset] == 4 ||
                    !npcs.TryGetValue(npcAttackTargetId, out var targetNpc) ||
                    targetNpc.GenerateIndex < 0 || targetNpc.AutoTradeSnapshot is not null ||
                    targetNpc.MobSnapshot.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                    return LegacyNpcAiApplyStatus.Stale;

                var attackerState = LegacyMobCombatState.Read(npc.MobSnapshot);
                var targetState = LegacyMobCombatState.Read(targetNpc.MobSnapshot);
                if (attackerState.CurrentScore.Hp <= 0 || targetState.CurrentScore.Hp <= 0 ||
                    targetState.CurrentScore.MaxHp < 0)
                    return LegacyNpcAiApplyStatus.Stale;

                var targetMob = targetNpc.MobSnapshot.ToArray();
                var damage = targetState.CurrentScore.MaxHp / 10;
                if (damage <= 0)
                    return LegacyNpcAiApplyStatus.Unsupported;
                var remainingHp = Math.Max(0, targetState.CurrentScore.Hp - damage);
                BinaryPrimitives.WriteInt32LittleEndian(targetMob.AsSpan(
                    LegacyAccountSnapshot.MobCurrentScoreOffset + 24, sizeof(int)), remainingHp);
                var recipients = Array.AsReadOnly(GetParticipantIdsInViewLocked(targetNpc.PositionX, targetNpc.PositionY));
                var targetDied = remainingHp == 0;
                if (targetDied)
                {
                    if (!RemoveNpcLocked(npcAttackTargetId))
                        return LegacyNpcAiApplyStatus.Stale;
                }
                else
                {
                    var updatedTarget = targetNpc with { MobSnapshot = targetMob };
                    if (npcAttackTargetId == npc.ConnectionId)
                    {
                        updatedTarget = updatedTarget with
                        {
                            Mode = (int)currentDecision.Mode,
                            CurrentTarget = currentDecision.CurrentTarget,
                            EnemyList = currentDecision.EnemyIds.ToArray(),
                        };
                        npc = updatedTarget;
                    }
                    npcs[npcAttackTargetId] = updatedTarget;
                }

                if (npcAttackTargetId != npc.ConnectionId)
                {
                    npc = npc with
                    {
                        Mode = (int)currentDecision.Mode,
                        CurrentTarget = currentDecision.CurrentTarget,
                        EnemyList = currentDecision.EnemyIds.ToArray(),
                    };
                    npcs[npc.ConnectionId] = npc;
                }

                attackOutcome = new LegacyGeneratedNpcPhysicalAttackOutcome(
                    npc.ConnectionId,
                    npcAttackTargetId,
                    npc.PositionX,
                    npc.PositionY,
                    targetNpc.PositionX,
                    targetNpc.PositionY,
                    damage,
                    remainingHp,
                    targetMob,
                    TargetDied: targetDied,
                    Missed: false,
                    ViewRecipientConnectionIds: recipients,
                    SkillIndex: attackFields.SkillIndex,
                    SkillParm: attackFields.SkillParm,
                    Motion: attackFields.Motion,
                    TargetIsNpc: true,
                    TargetAffectSnapshot: targetNpc.AffectSnapshot.ToArray());
            }
            else if (currentDecision.Kind == LegacyMobAiDecisionKind.Attack)
            {
                if (currentDecision.CurrentTarget <= 0 ||
                    !participants.TryGetValue(currentDecision.CurrentTarget, out var target) ||
                    target.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                    return LegacyNpcAiApplyStatus.Stale;

                var attackerState = LegacyMobCombatState.Read(npc.MobSnapshot);
                var targetState = LegacyMobCombatState.Read(target.Mob);
                if (attackerState.CurrentScore.Hp <= 0 || targetState.CurrentScore.Hp <= 0 ||
                    npc.MobSnapshot[LegacyAccountSnapshot.MobClanOffset] == 4)
                    return LegacyNpcAiApplyStatus.Unsupported;
                if (currentDecision.Attack is not { } attackFields)
                    return LegacyNpcAiApplyStatus.AttackUnsupported;

                var damage = LegacySkillCombatMath.GetPhysicalDamage(
                    attackerState.CurrentScore.Damage,
                    targetState.CurrentScore.Ac,
                    combat: 0,
                    snapshot.PhysicalDamageRandomFactor);
                damage = LegacyMobAiAttackMath.ApplyElementalResistance(
                    damage,
                    attackFields.Resist,
                    target.Mob.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4));
                var reflectDamage = GetLegacyReflectDamage(target.Mob, target.ClientEquipment, targetState, itemData);
                if (damage > 0 && reflectDamage > 0)
                {
                    damage -= reflectDamage;
                    if (damage <= 0) damage = 1;
                }

                var targetLeaderId = partyLeaders.TryGetValue(target.ConnectionId, out var configuredTargetLeader)
                    ? configuredTargetLeader
                    : target.ConnectionId;
                var targetLeader = participants.TryGetValue(targetLeaderId, out var configuredLeaderParticipant) &&
                                   configuredLeaderParticipant.Mob.Length >= WorldHubLegacyOffsets.LegacyAccountMobSize
                    ? configuredLeaderParticipant
                    : target;
                var npcClan = npc.MobSnapshot[LegacyAccountSnapshot.MobClanOffset];
                if (damage > 0 && combatWorldState.RvrWarActive &&
                    npc.PositionX is >= LegacyAttackRvrSameClanGate.MinimumX and <= LegacyAttackRvrSameClanGate.MaximumX &&
                    npc.PositionY is >= LegacyAttackRvrSameClanGate.MinimumY and <= LegacyAttackRvrSameClanGate.MaximumY &&
                    npcClan == targetLeader.Mob[LegacyAccountSnapshot.MobClanOffset])
                {
                    damage = 0;
                    currentDecision = currentDecision with { CurrentTarget = 0 };
                }

                if (damage > 0)
                {
                    var npcGuild = (int)BinaryPrimitives.ReadUInt16LittleEndian(npc.MobSnapshot.AsSpan(
                        WorldHubLegacyOffsets.LegacyAccountMobGuildOffset, sizeof(ushort)));
                    var targetGuild = target.GuildDisabled ? -2 :
                        BinaryPrimitives.ReadUInt16LittleEndian(target.Mob.AsSpan(
                            WorldHubLegacyOffsets.LegacyAccountMobGuildOffset, sizeof(ushort)));
                    if (npcGuild == 0 && targetGuild == 0)
                        npcGuild = -1;
                    if (npc.ConnectionId == targetLeaderId || npcGuild == targetGuild)
                    {
                        damage = 0;
                        currentDecision = currentDecision with { CurrentTarget = 0 };
                    }
                }

                if (damage > 0)
                {
                    var missRate = LegacyAttackMissMath.GetRatePercent(
                        targetState.CurrentScore.Dexterity,
                        attackerState.CurrentScore.Dexterity,
                        attackerState.Rsv);
                    if (snapshot.AttackMissRoll < missRate)
                        damage = -3;
                }

                if (damage > 0)
                    TrySetNpcBattle(npc.ConnectionId, target.ConnectionId);
                if (npcs.TryGetValue(npc.ConnectionId, out var updatedNpc))
                    npc = updatedNpc;
                npc = npc with
                {
                    Mode = (int)currentDecision.Mode,
                    CurrentTarget = currentDecision.CurrentTarget,
                    EnemyList = currentDecision.EnemyIds.ToArray(),
                };
                npcs[npc.ConnectionId] = npc;

                var remainingHp = targetState.CurrentScore.Hp;
                var targetRevived = false;
                var consumedItemSlot = -1;
                LegacyItem? consumedItem = null;
                if (damage > 0)
                {
                    remainingHp = Math.Max(0, remainingHp - damage);
                    BinaryPrimitives.WriteInt32LittleEndian(target.Mob.AsSpan(
                        LegacyAccountSnapshot.MobCurrentScoreOffset + 24, sizeof(int)), remainingHp);
                    target.RequestedHp = remainingHp;
                    if (remainingHp == 0 && TryConsumeResurrectionScroll(target, out consumedItemSlot, out var revivedItem))
                    {
                        targetRevived = true;
                        consumedItem = revivedItem;
                        remainingHp = LegacyMobCombatState.Read(target.Mob).CurrentScore.Hp;
                    }
                }

                attackOutcome = new LegacyGeneratedNpcPhysicalAttackOutcome(
                    npc.ConnectionId,
                    target.ConnectionId,
                    npc.PositionX,
                    npc.PositionY,
                    target.PositionX,
                    target.PositionY,
                    damage,
                    remainingHp,
                    target.Mob.ToArray(),
                    TargetDied: remainingHp == 0,
                    Missed: damage == -3,
                    TargetRevived: targetRevived,
                    ConsumedItemSlot: consumedItemSlot,
                    ConsumedItem: consumedItem,
                    ViewRecipientConnectionIds: Array.AsReadOnly(GetParticipantIdsInViewLocked(target.PositionX, target.PositionY)),
                    SkillIndex: attackFields.SkillIndex,
                    SkillParm: attackFields.SkillParm,
                    Motion: attackFields.Motion);
            }
            else if (currentDecision.Kind == LegacyMobAiDecisionKind.Aggro)
            {
                if (!TrySetNpcBattle(
                        npc.ConnectionId,
                        currentDecision.AggroTargetId,
                        propagateTargetPartySummons: currentDecision.AggroTargetId < LegacyMobAiPlanner.MaximumPlayerId))
                    return LegacyNpcAiApplyStatus.Stale;
                npcs.TryGetValue(npc.ConnectionId, out npc);
                additionalRemovals = PropagateGeneratedNpcGroupAggroLocked(
                    npc!.ConnectionId,
                    currentDecision.AggroTargetId);
            }
            else
            {
                var movementOriginViewIds = snapshot.MovementPlan is null
                    ? null
                    : GetParticipantIdsInViewLocked(npc.PositionX, npc.PositionY);
                if (currentDecision.NextX is { } nextX && currentDecision.NextY is { } nextY)
                {
                    if (nextX is < 0 or >= 4096 || nextY is < 0 or >= 4096 ||
                        mapGrid?.IsBlocked(nextX, nextY) == true ||
                        (GetParticipantAtPosition(nextX, nextY) is var occupant && occupant != 0 && occupant != npc.ConnectionId))
                        return LegacyNpcAiApplyStatus.Blocked;
                    if (currentDecision.CurrentTarget > 0 && !IsLiveBattleTargetLocked(currentDecision.CurrentTarget))
                        return LegacyNpcAiApplyStatus.Stale;
                    npc = npc! with { PositionX = checked((short)nextX), PositionY = checked((short)nextY) };
                }

                npc = npc! with
                {
                    MobSnapshot = currentDecision.ActivateMerchant
                        ? ActivateGeneratedNpcMerchant(npc!.MobSnapshot)
                        : npc!.MobSnapshot,
                    Mode = (int)currentDecision.Mode,
                    CurrentTarget = currentDecision.CurrentTarget,
                    EnemyList = currentDecision.EnemyIds.ToArray(),
                    SegmentX = checked((short)currentDecision.SegmentX),
                    SegmentY = checked((short)currentDecision.SegmentY),
                    SegmentProgress = currentDecision.SegmentProgress,
                    SegmentDirection = currentDecision.SegmentDirection,
                    WaitSec = currentDecision.WaitSec,
                };
                npcs[npc.ConnectionId] = npc;

                if (snapshot.MovementPlan is { } appliedMovement && movementOriginViewIds is not null)
                {
                    movementOutcome = new LegacyGeneratedNpcMovementOutcome(
                        npc.ConnectionId,
                        appliedMovement.FromX,
                        appliedMovement.FromY,
                        npc.PositionX,
                        npc.PositionY,
                        appliedMovement.Speed,
                        npc.MobSnapshot.ToArray(),
                        npc.AffectSnapshot.ToArray(),
                        Array.AsReadOnly(movementOriginViewIds),
                        Array.AsReadOnly(GetParticipantIdsInViewLocked(npc.PositionX, npc.PositionY)),
                        combatWorldState.BattleRoyaleState);

                    if (wasPeaceMode && LegacyMobAiPlanner.IsRunequestLv2ExitDespawnCandidate(
                            npc.GenerateIndex, npc.PositionX, npc.PositionY))
                    {
                        var removalRecipients = movementOutcome.DestinationViewConnectionIds;
                        if (!RemoveNpcLocked(npc.ConnectionId))
                            return LegacyNpcAiApplyStatus.Stale;
                        removalOutcome = new LegacyGeneratedNpcRemovalOutcome(
                            npc.ConnectionId,
                            removalRecipients,
                            RemoveType: 3);
                    }
                }
            }

            outcome = new LegacyNpcAiApplicationOutcome(
                LegacyNpcAiApplyStatus.Applied,
                currentDecision.Kind,
                npc!.PositionX,
                npc.PositionY,
                npc.Mode,
                npc.CurrentTarget,
                npc.EnemyList.ToArray(),
                npc.SegmentX,
                npc.SegmentY,
                npc.SegmentProgress,
                npc.SegmentDirection,
                npc.WaitSec,
                currentDecision.ActionText,
                attackOutcome,
                movementOutcome,
                removalOutcome)
            {
                AdditionalRemovals = additionalRemovals,
            };
            return LegacyNpcAiApplyStatus.Applied;
        }
    }

    private bool TryBuildGeneratedNpcAiInputLocked(
        LegacyWorldNpc npc,
        int attackRange,
        LegacyMobAiPoint? routeStep,
        int randomIntRoll,
        int dexterityRoll,
        out LegacyMobAiInput input,
        int skillBarSelectionRoll = 0,
        int skillBarMotionRoll = 0)
    {
        input = null!;
        if (npc.MobSnapshot.Length < WorldHubLegacyOffsets.LegacyAccountMobSize || npc.AffectSnapshot.Length != LegacyAccountSnapshot.AffectStride)
            return false;
        var state = LegacyMobCombatState.Read(npc.MobSnapshot);
        var mob = new LegacyMobAiMob(
            npc.ConnectionId, npc.PositionX, npc.PositionY, npc.SegmentX, npc.SegmentY,
            npc.RouteType, npc.MobSnapshot[LegacyAccountSnapshot.MobClanOffset], npc.GenerateIndex,
            (LegacyMobAiMode)npc.Mode, npc.CurrentTarget, npc.EnemyList.ToArray(),
            npc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4).ToArray(),
            state.BaseScore.Intelligence, state.BaseScore.Dexterity, attackRange,
            state.CurrentScore.Level, state.CurrentScore.Hp, state.CurrentScore.MaxHp,
            0, npc.LeaderId, 0, npc.GenerationRoute, npc.SegmentProgress, npc.SegmentDirection, npc.WaitSec,
            npc.Mode != LegacyNpcMode.Empty)
        {
            BaseSpecial = [state.BaseScore.Special1, state.BaseScore.Special2,
                state.BaseScore.Special3, state.BaseScore.Special4]
        };

        var targets = new List<LegacyMobAiTarget>();
        foreach (var participant in participants.Values)
            AddAiTarget(targets, participant.ConnectionId, participant.PositionX, participant.PositionY, participant.Mob, participant.Affect, isNpc: false, isActive: true);
        foreach (var summon in summons.Values)
            AddAiTarget(targets, summon.ConnectionId, summon.PositionX, summon.PositionY, summon.MobSnapshot, summon.AffectSnapshot, isNpc: true, isActive: IsLiveSummonLocked(summon));
        foreach (var candidate in npcs.Values)
            AddAiTarget(targets, candidate.ConnectionId, candidate.PositionX, candidate.PositionY, candidate.MobSnapshot,
                candidate.AffectSnapshot, isNpc: true, isActive: candidate.Mode != LegacyNpcMode.Empty,
                isGeneratedNpc: candidate.GenerateIndex >= 0);
        input = new LegacyMobAiInput(mob, targets.OrderBy(static target => target.Y).ThenBy(static target => target.X).ToArray(),
            null, routeStep, randomIntRoll, dexterityRoll, skillBarSelectionRoll, skillBarMotionRoll)
        {
            SkillData = skillData
        };
        return true;

        void AddAiTarget(List<LegacyMobAiTarget> output, int id, short x, short y, byte[] targetMob, byte[] targetAffect,
            bool isNpc, bool isActive, bool isGeneratedNpc = false)
        {
            if (id == npc.ConnectionId || targetMob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                return;
            var targetState = LegacyMobCombatState.Read(targetMob);
            var targetClan = targetMob[LegacyAccountSnapshot.MobClanOffset];
            var clan = mob.Clan;
            var hidden = (targetState.Rsv & 0x10) != 0;
            var merchant = (targetMob[LegacyAccountSnapshot.MobMerchantOffset] & 1) != 0;
            var isVisible = GetParticipantAtPosition(x, y) == id;
            var hostile = clan < LegacyClanRelations.Table.Length && targetClan < LegacyClanRelations.Table[clan].Length &&
                          LegacyClanRelations.Table[clan][targetClan] == 0;
            output.Add(new LegacyMobAiTarget(id, x, y, isNpc, isActive, targetState.CurrentScore.Hp,
                hidden, merchant, targetState.CurrentScore.Level, IsValid: true, IsVisible: isVisible, IsHostile: hostile,
                MaxHp: targetState.CurrentScore.MaxHp, IsGeneratedNpc: isGeneratedNpc));
        }
    }

    private bool IsLiveBattleTargetLocked(int id)
        => TryGetBattleTargetLocked(id, out _, out _, out var mob, out _) &&
           mob.Length >= WorldHubLegacyOffsets.LegacyAccountMobSize && LegacyMobCombatState.Read(mob).CurrentScore.Hp > 0;

    private static bool DecisionsEqual(LegacyMobAiDecision left, LegacyMobAiDecision right)
        => left.Kind == right.Kind && left.Mode == right.Mode && left.CurrentTarget == right.CurrentTarget &&
           left.AggroTargetId == right.AggroTargetId && left.EnemyIds.SequenceEqual(right.EnemyIds) &&
           left.NextX == right.NextX && left.NextY == right.NextY && left.SegmentX == right.SegmentX &&
           left.SegmentY == right.SegmentY && left.SegmentProgress == right.SegmentProgress &&
           left.SegmentDirection == right.SegmentDirection && left.WaitSec == right.WaitSec &&
           left.TargetX == right.TargetX && left.TargetY == right.TargetY && left.Attack == right.Attack &&
            left.AttackTargetId == right.AttackTargetId && left.DespawnReason == right.DespawnReason &&
            left.ActivateMerchant == right.ActivateMerchant &&
           left.ActionText == right.ActionText;

    private static byte[] ActivateGeneratedNpcMerchant(byte[] mobSnapshot)
    {
        var activated = mobSnapshot.ToArray();
        var merchant = activated[LegacyAccountSnapshot.MobMerchantOffset];
        var baseScore = LegacyScore.Read(activated.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset)) with
        {
            Merchant = merchant
        };
        baseScore.Write(activated.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));

        var currentScore = LegacyScore.Read(activated.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        currentScore = currentScore with
        {
            Merchant = (byte)((currentScore.Merchant & 0xF0) | merchant)
        };
        currentScore.Write(activated.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return activated;
    }

    private byte[] ComputeNpcAiWorldFingerprintLocked()
    {
        using var stream = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(participants.Count);
        foreach (var participant in participants.Values.OrderBy(static value => value.ConnectionId))
        {
            writer.Write(participant.ConnectionId); writer.Write(participant.PositionX); writer.Write(participant.PositionY);
            WriteBytes(writer, participant.Mob); WriteBytes(writer, participant.Affect);
        }
        writer.Write(summons.Count);
        foreach (var summon in summons.Values.OrderBy(static value => value.ConnectionId))
        {
            writer.Write(summon.ConnectionId); writer.Write(summon.PositionX); writer.Write(summon.PositionY);
            writer.Write(summon.LeaderConnectionId); writer.Write(summon.SummonerConnectionId);
            WriteBytes(writer, summon.MobSnapshot); WriteBytes(writer, summon.AffectSnapshot);
        }
        writer.Write(partyLeaders.Count);
        foreach (var party in partyLeaders.OrderBy(static pair => pair.Key))
        {
            writer.Write(party.Key); writer.Write(party.Value);
        }
        writer.Write(npcs.Count);
        foreach (var npc in npcs.Values.OrderBy(static value => value.ConnectionId))
        {
            writer.Write(npc.ConnectionId); writer.Write(npc.PositionX); writer.Write(npc.PositionY);
            writer.Write(npc.Mode); writer.Write(npc.CurrentTarget); writer.Write(npc.GenerateIndex); writer.Write(npc.RouteType); writer.Write(npc.LeaderId);
            writer.Write(npc.IsGeneratedGroupPartyMember);
            writer.Write(npc.SegmentX); writer.Write(npc.SegmentY); writer.Write(npc.SegmentProgress); writer.Write(npc.SegmentDirection); writer.Write(npc.WaitSec);
            WriteBytes(writer, npc.MobSnapshot); WriteBytes(writer, npc.AffectSnapshot);
            writer.Write(npc.EnemyList.Count);
            foreach (var enemy in npc.EnemyList) writer.Write(enemy);
            WriteRoutePoint(writer, npc.GenerationRoute.Start);
            WriteRoutePoint(writer, npc.GenerationRoute.Segment1);
            WriteRoutePoint(writer, npc.GenerationRoute.Segment2);
            WriteRoutePoint(writer, npc.GenerationRoute.Segment3);
            WriteRoutePoint(writer, npc.GenerationRoute.Dest);
        }
        writer.Flush();
        return SHA256.HashData(stream.ToArray());

        static void WriteRoutePoint(System.IO.BinaryWriter target, LegacyNpcGenerationRoutePoint point)
        {
            target.Write(point.X); target.Write(point.Y); target.Write(point.Range); target.Write(point.Wait);
        }

        static void WriteBytes(System.IO.BinaryWriter target, byte[] value)
        {
            target.Write(value.Length);
            target.Write(value);
        }
    }

    private bool TryGetBattleTargetLocked(
        int targetConnectionId,
        out short x,
        out short y,
        out byte[] mob,
        out int targetPartyLeader)
    {
        x = 0;
        y = 0;
        mob = [];
        targetPartyLeader = 0;
        if (participants.TryGetValue(targetConnectionId, out var participant))
        {
            if (participant.Mob.Length != LegacyAccountSnapshot.CharacterStride ||
                LegacyMobCombatState.Read(participant.Mob).CurrentScore.Hp <= 0)
                return false;
            x = participant.PositionX;
            y = participant.PositionY;
            mob = participant.Mob;
            targetPartyLeader = GetPartyLeaderConnectionIdLocked(targetConnectionId);
            return true;
        }

        if (summons.TryGetValue(targetConnectionId, out var summon) && IsLiveSummonLocked(summon))
        {
            x = summon.PositionX;
            y = summon.PositionY;
            mob = summon.MobSnapshot;
            targetPartyLeader = summon.LeaderConnectionId;
            return true;
        }

        if (npcs.TryGetValue(targetConnectionId, out var npc) && npc.Mode != LegacyNpcMode.Empty &&
            npc.MobSnapshot.Length == LegacyAccountSnapshot.CharacterStride && LegacyMobCombatState.Read(npc.MobSnapshot).CurrentScore.Hp > 0)
        {
            x = npc.PositionX;
            y = npc.PositionY;
            mob = npc.MobSnapshot;
            return true;
        }

        return false;
    }

    private static bool IsFilteredBattlePlayer(int targetConnectionId, byte[] mob) =>
        targetConnectionId <= LegacyMobAiPlanner.MaximumPlayerId &&
        ((LegacyMobCombatState.Read(mob).Rsv & 0x10) != 0 ||
         (mob[LegacyAccountSnapshot.MobMerchantOffset] & 1) != 0);

    private IReadOnlyList<LegacyGeneratedNpcRemovalOutcome> PropagateGeneratedNpcGroupAggroLocked(
        int leaderConnectionId,
        int targetConnectionId)
    {
        var removals = new List<LegacyGeneratedNpcRemovalOutcome>();
        // TMProject 7.69 and W2PP scan PartyList after a leader acquires a passive target.
        // LeaderId identifies the parent; IsGeneratedGroupPartyMember tracks list membership.
        if (!npcs.TryGetValue(leaderConnectionId, out var leader) || leader.LeaderId != 0 ||
            !TryGetBattleTargetLocked(targetConnectionId, out var targetX, out var targetY, out var targetMob, out _))
            return removals;

        // The leader's TrySetNpcBattle mirrors the timer's separate target-party summon loop once.
        foreach (var follower in npcs.Values.Where(candidate =>
                     candidate.IsGeneratedGroupPartyMember && candidate.LeaderId == leaderConnectionId).ToArray())
        {
            if (follower.ConnectionId <= LegacyMobAiPlanner.MaximumPlayerId || follower.GenerateIndex < 0 ||
                follower.AutoTradeSnapshot is not null ||
                follower.MobSnapshot.Length != LegacyAccountSnapshot.CharacterStride)
                continue;

            if (follower.Mode == LegacyNpcMode.Empty)
            {
                npcs[follower.ConnectionId] = follower with { IsGeneratedGroupPartyMember = false };
                continue;
            }

            var followerHp = LegacyMobCombatState.Read(follower.MobSnapshot).CurrentScore.Hp;
            if (followerHp <= 0)
            {
                var recipients = Array.AsReadOnly(GetParticipantIdsInViewLocked(follower.PositionX, follower.PositionY));
                if (RemoveNpcLocked(follower.ConnectionId))
                {
                    removals.Add(new LegacyGeneratedNpcRemovalOutcome(
                        follower.ConnectionId,
                        recipients,
                        RemoveType: 1));
                }
                continue;
            }

            if (Math.Abs(targetX - follower.PositionX) > 23 || Math.Abs(targetY - follower.PositionY) > 23)
                continue;

            npcs[follower.ConnectionId] = follower with
            {
                Mode = LegacyNpcMode.Combat,
                EnemyList = NormalizeNpcEnemyList(follower.EnemyList),
            };
            if (!IsFilteredBattlePlayer(targetConnectionId, targetMob))
                AddNpcEnemyLocked(follower.ConnectionId, targetConnectionId);
        }

        return Array.AsReadOnly(removals.ToArray());
    }

    private void AddNpcEnemyLocked(int npcConnectionId, int targetConnectionId)
    {
        if (!npcs.TryGetValue(npcConnectionId, out var npc) || npc.EnemyList.Contains(targetConnectionId))
            return;
        var enemyList = NormalizeNpcEnemyList(npc.EnemyList);
        var empty = Array.IndexOf(enemyList, 0);
        if (empty < 0)
            return;
        enemyList[empty] = targetConnectionId;
        npcs[npcConnectionId] = npc with { EnemyList = enemyList };
    }

    private void AddSummonEnemyLocked(int summonConnectionId, int targetConnectionId)
    {
        if (!summons.TryGetValue(summonConnectionId, out var summon) || summon.EnemyList.Contains(targetConnectionId))
            return;
        var enemyList = NormalizeNpcEnemyList(summon.EnemyList);
        var empty = Array.IndexOf(enemyList, 0);
        if (empty < 0)
            return;
        enemyList[empty] = targetConnectionId;
        summons[summonConnectionId] = summon with { EnemyList = enemyList };
    }

    /// <summary>
    /// Applies the target-alive _MSG_Attack tail to a player-attacked summon.
    /// The legacy loops first prune dead NPC party members from both parties,
    /// then apply aggro to the remaining live summons represented in this world.
    /// </summary>
    private IReadOnlyList<LegacyAttackViewRemovalEffect> ApplySummonAttackAggroLocked(
        int attackedSummonConnectionId,
        int attackerConnectionId,
        int slotIndex)
    {
        if (!summons.TryGetValue(attackedSummonConnectionId, out var attackedSummon) ||
            !IsLiveSummonLocked(attackedSummon) ||
            !TryGetBattleTargetLocked(attackerConnectionId, out var attackerX, out var attackerY, out var attackerMob, out _))
            return [];

        var attackerLeaderConnectionId = GetPartyLeaderConnectionIdLocked(attackerConnectionId);
        var viewRemovalEffects = PruneDeadPartySummonsForAttackTailLocked(
            attackerConnectionId,
            attackedSummon.LeaderConnectionId,
            slotIndex);

        var attackerIsFiltered = IsFilteredBattlePlayer(attackerConnectionId, attackerMob);
        if (!attackerIsFiltered &&
            Math.Abs(attackedSummon.PositionX - attackerX) <= 23 &&
            Math.Abs(attackedSummon.PositionY - attackerY) <= 23)
            AddSummonEnemyLocked(attackedSummonConnectionId, attackerConnectionId);

        // First _MSG_Attack PartyList loop: summons from the attacker's party
        // receive the mob that the player just hit as their explicit target.
        foreach (var partySummon in summons.Values
                     .Where(summon => summon.LeaderConnectionId == attackerLeaderConnectionId)
                     .ToArray())
        {
            if (!IsLiveSummonLocked(partySummon))
                continue;

            if (partySummon.ConnectionId == attackedSummonConnectionId ||
                Math.Abs(partySummon.PositionX - attackedSummon.PositionX) > 23 ||
                Math.Abs(partySummon.PositionY - attackedSummon.PositionY) > 23)
                continue;

            AddSummonEnemyLocked(partySummon.ConnectionId, attackedSummonConnectionId);
        }

        // Second _MSG_Attack PartyList loop: the attacked summon and its party
        // summons receive the attacking player, subject to AddEnemyList's
        // USER_PLAY and hidden/merchant insertion gates.
        foreach (var partySummon in summons.Values
                     .Where(summon => summon.LeaderConnectionId == attackedSummon.LeaderConnectionId)
                     .ToArray())
        {
            if (!IsLiveSummonLocked(partySummon))
                continue;

            if (attackerIsFiltered || Math.Abs(partySummon.PositionX - attackerX) > 23 ||
                Math.Abs(partySummon.PositionY - attackerY) > 23)
                continue;

            AddSummonEnemyLocked(partySummon.ConnectionId, attackerConnectionId);
        }

        return viewRemovalEffects;
    }

    private IReadOnlyList<LegacyAttackViewRemovalEffect> PruneDeadPartySummonsForAttackTailLocked(
        int attackerConnectionId,
        int targetPartyLeaderConnectionId,
        int slotIndex)
    {
        var viewRemovalEffects = new List<LegacyAttackViewRemovalEffect>();
        PruneDeadPartySummonsLocked(
            GetPartyLeaderConnectionIdLocked(attackerConnectionId),
            slotIndex,
            viewRemovalEffects);
        PruneDeadPartySummonsLocked(targetPartyLeaderConnectionId, slotIndex, viewRemovalEffects);
        return Array.AsReadOnly(viewRemovalEffects.ToArray());
    }

    private void PruneDeadPartySummonsLocked(
        int partyLeaderConnectionId,
        int slotIndex,
        ICollection<LegacyAttackViewRemovalEffect> viewRemovalEffects)
    {
        if (partyLeaderConnectionId <= 0)
            return;

        foreach (var partySummon in summons.Values
                     .Where(summon => summon.LeaderConnectionId == partyLeaderConnectionId)
                     .ToArray())
        {
            if (IsLiveSummonLocked(partySummon))
                continue;

            var recipients = Array.AsReadOnly(GetParticipantIdsInViewLocked(partySummon.PositionX, partySummon.PositionY));
            if (RemoveSummonLocked(partySummon.ConnectionId))
                viewRemovalEffects.Add(new LegacyAttackViewRemovalEffect(
                    slotIndex,
                    partySummon.ConnectionId,
                    recipients));
        }
    }

    private static int[] NormalizeNpcEnemyList(IReadOnlyList<int> enemyList)
    {
        var normalized = new int[LegacyMobAiPlanner.EnemyCapacity];
        for (var index = 0; index < Math.Min(enemyList.Count, normalized.Length); index++)
            normalized[index] = enemyList[index];
        return normalized;
    }

    private static bool IsLiveSummonLocked(LegacySummonedMob summon) =>
        summon.MobSnapshot.Length == LegacyAccountSnapshot.CharacterStride &&
        LegacyMobCombatState.Read(summon.MobSnapshot).CurrentScore.Hp > 0;

    public bool TryGetNpcCombatState(int connectionId, out int mode, out int currentTarget, out IReadOnlyList<int> enemyList)
    {
        lock (gate)
        {
            if (!npcs.TryGetValue(connectionId, out var npc))
            {
                mode = 0;
                currentTarget = 0;
                enemyList = [];
                return false;
            }

            mode = npc.Mode;
            currentTarget = npc.CurrentTarget;
            enemyList = npc.EnemyList.ToArray();
            return true;
        }
    }

    /// <summary>
    /// Checks whether a compact 7.60 NPC click targeted one of the explicit
    /// Donate Store NPC templates. The target must already be a live world
    /// NPC; arbitrary player/NPC connection IDs never become a Donate entry
    /// point just because they arrived in a client packet.
    /// </summary>
    public bool IsDonateShopNpc(int connectionId)
    {
        lock (gate)
        {
            return npcs.TryGetValue(connectionId, out var npc) &&
                   IsDonateShopNpcName(ReadMobName(npc.MobSnapshot));
        }
    }

    public IReadOnlyList<LegacyWorldNpc> GetNpcSnapshots()
    {
        lock (gate)
            return npcs.Values.OrderBy(static npc => npc.ConnectionId).Select(CloneNpcSnapshot).ToArray();
    }

    /// <summary>Captures generated NPCs assigned to one stable AI shard.</summary>
    public IReadOnlyList<LegacyWorldNpc> GetGeneratedNpcSnapshotsForShard(int shardIndex, int shardCount)
        => GetGeneratedNpcSnapshotsForShard(shardIndex, shardCount, shardOffset: 0);

    /// <summary>Captures generated NPCs assigned to one stable shard relative to the legacy ID offset.</summary>
    public IReadOnlyList<LegacyWorldNpc> GetGeneratedNpcSnapshotsForShard(int shardIndex, int shardCount, int shardOffset)
    {
        if (shardCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(shardCount));
        if (shardIndex < 0 || shardIndex >= shardCount)
            throw new ArgumentOutOfRangeException(nameof(shardIndex));

        lock (gate)
        {
            return npcs.Values
                .Where(npc => npc.GenerateIndex >= 0 && npc.AutoTradeSnapshot is null &&
                                (npc.ConnectionId - shardOffset) % shardCount == shardIndex)
                .OrderBy(static npc => npc.ConnectionId)
                .Select(CloneNpcSnapshot)
                .ToArray();
        }
    }

    /// <summary>Captures ordinary ungenerated NPCs assigned to one stable affect-timer shard.</summary>
    public IReadOnlyList<LegacyWorldNpc> GetNonGeneratedNpcTimerSnapshotsForShard(
        int shardIndex,
        int shardCount,
        int shardOffset)
    {
        if (shardCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(shardCount));
        if (shardIndex < 0 || shardIndex >= shardCount)
            throw new ArgumentOutOfRangeException(nameof(shardIndex));

        lock (gate)
        {
            return npcs.Values
                .Where(npc => npc.ConnectionId >= shardOffset && npc.GenerateIndex < 0 && npc.LeaderId <= 0 &&
                              npc.AutoTradeSnapshot is null &&
                              (npc.ConnectionId - shardOffset) % shardCount == shardIndex)
                .OrderBy(static npc => npc.ConnectionId)
                .Select(CloneNpcSnapshot)
                .ToArray();
        }
    }

    /// <summary>Captures RouteType 5 summons assigned to one stable Peace shard.</summary>
    public IReadOnlyList<int> GetRouteTypeFiveSummonIdsForShard(int shardIndex, int shardCount, int shardOffset)
    {
        if (shardCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(shardCount));
        if (shardIndex < 0 || shardIndex >= shardCount)
            throw new ArgumentOutOfRangeException(nameof(shardIndex));

        lock (gate)
        {
            return summons.Values
                .Where(summon => summon.RouteType == 5 && summon.ConnectionId >= shardOffset &&
                                 (summon.ConnectionId - shardOffset) % shardCount == shardIndex)
                .OrderBy(static summon => summon.ConnectionId)
                .Select(static summon => summon.ConnectionId)
                .ToArray();
        }
    }

    public bool TryGetNpcSnapshot(int connectionId, out LegacyWorldNpc? snapshot)
    {
        lock (gate)
        {
            if (npcs.TryGetValue(connectionId, out var npc))
            {
                snapshot = CloneNpcSnapshot(npc);
                return true;
            }

            snapshot = null;
            return false;
        }
    }

    private static LegacyWorldNpc CloneNpcSnapshot(LegacyWorldNpc npc) => npc with
    {
        MobSnapshot = npc.MobSnapshot.ToArray(),
        AffectSnapshot = npc.AffectSnapshot.ToArray(),
        EnemyList = npc.EnemyList.ToArray(),
    };

    /// <summary>Checks the common 7.69 MSG_Attack NPC-head/map gate before per-slot marker validation.</summary>
    public bool IsAttackTargetBlockedByNpcHeadArea(int targetConnectionId)
    {
        lock (gate)
            return IsAttackTargetBlockedByNpcHeadAreaLocked(targetConnectionId);
    }

    /// <summary>Checks the 7.69 dead-target cleanup before Host-side damage-marker validation.</summary>
    public bool ShouldClearDeadTargetInAttack(int targetConnectionId, int skillIndex)
    {
        lock (gate)
            return ShouldClearDeadTargetInAttackLocked(targetConnectionId, skillIndex);
    }

    /// <summary>Checks the common 7.69 MSG_Attack Merchant gate before marker/range processing.</summary>
    public bool ShouldClearMerchantTargetInAttack(int targetConnectionId)
    {
        lock (gate)
            return ShouldClearMerchantTargetInAttackLocked(targetConnectionId);
    }

    /// <summary>Checks the 7.69 per-target view window before Host-side damage-marker validation.</summary>
    public bool ShouldRemoveOutOfViewTargetInAttack(int attackerConnectionId, int targetConnectionId, int skillIndex)
    {
        lock (gate)
            return ShouldRemoveOutOfViewTargetInAttackLocked(attackerConnectionId, targetConnectionId, skillIndex);
    }

    public LegacyAttackTargetSlotState GetAttackTargetSlotState(int targetConnectionId)
    {
        lock (gate)
            return GetAttackTargetSlotStateLocked(targetConnectionId);
    }

    private bool IsAttackTargetBlockedByNpcHeadAreaLocked(int targetConnectionId)
    {
        if (participants.ContainsKey(targetConnectionId))
            return false;

        if (summons.TryGetValue(targetConnectionId, out var summon))
            return IsNpcHeadAreaBlocked(summon.MobSnapshot, summon.PositionX, summon.PositionY);

        if (npcs.TryGetValue(targetConnectionId, out var npc))
            return IsNpcHeadAreaBlocked(npc.MobSnapshot, npc.PositionX, npc.PositionY);

        return false;
    }

    private LegacyAttackTargetSlotState GetAttackTargetSlotStateLocked(int targetConnectionId) =>
        LegacyAttackTargetSlotGate.Classify(
            targetConnectionId,
            participants.ContainsKey(targetConnectionId) ||
            summons.ContainsKey(targetConnectionId) ||
            npcs.ContainsKey(targetConnectionId));

    private bool ShouldClearDeadTargetInAttackLocked(int targetConnectionId, int skillIndex)
    {
        byte[]? targetMob = null;
        if (participants.TryGetValue(targetConnectionId, out var participant))
            targetMob = participant.Mob;
        else if (summons.TryGetValue(targetConnectionId, out var summon))
            targetMob = summon.MobSnapshot;
        else if (npcs.TryGetValue(targetConnectionId, out var npc))
            targetMob = npc.MobSnapshot;

        if (targetMob is null || targetMob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
            return false;

        var targetHp = LegacyMobCombatState.Read(targetMob).CurrentScore.Hp;
        return LegacyAttackDeadTargetGate.ShouldClearTargetSlot(targetHp, skillIndex);
    }

    private bool ShouldClearMerchantTargetInAttackLocked(int targetConnectionId)
    {
        byte[]? targetMob = null;
        if (participants.TryGetValue(targetConnectionId, out var participant))
            targetMob = participant.Mob;
        else if (summons.TryGetValue(targetConnectionId, out var summon))
            targetMob = summon.MobSnapshot;
        else if (npcs.TryGetValue(targetConnectionId, out var npc))
            targetMob = npc.MobSnapshot;

        if (targetMob is null || targetMob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
            return false;

        var merchant = LegacyMobCombatState.Read(targetMob).CurrentScore.Merchant;
        return LegacyAttackMerchantGate.ShouldClearTargetSlot(merchant);
    }

    private bool ShouldRemoveOutOfViewTargetInAttackLocked(int attackerConnectionId, int targetConnectionId, int skillIndex)
    {
        if (skillIndex == LegacyAttackViewGate.ViewExemptSkillIndex ||
            !participants.TryGetValue(attackerConnectionId, out var attacker))
            return false;

        short targetX;
        short targetY;
        if (participants.TryGetValue(targetConnectionId, out var targetParticipant))
        {
            targetX = targetParticipant.PositionX;
            targetY = targetParticipant.PositionY;
        }
        else if (summons.TryGetValue(targetConnectionId, out var targetSummon))
        {
            targetX = targetSummon.PositionX;
            targetY = targetSummon.PositionY;
        }
        else if (npcs.TryGetValue(targetConnectionId, out var targetNpc))
        {
            targetX = targetNpc.PositionX;
            targetY = targetNpc.PositionY;
        }
        else
        {
            return false;
        }

        return LegacyAttackViewGate.ShouldSendRemoveMob(
            attacker.PositionX,
            attacker.PositionY,
            targetX,
            targetY,
            skillIndex);
    }

    private static bool IsNpcHeadAreaBlocked(ReadOnlySpan<byte> targetMob, short targetX, short targetY)
    {
        var headEnd = LegacyAccountSnapshot.MobEquipmentOffset + LegacyItem.SizeInBytes;
        if (targetMob.Length < headEnd)
            return false;

        var headIndex = LegacyItem.Read(targetMob.Slice(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes)).Index;
        return LegacyAttackNpcHeadAreaGate.ShouldBlock(true, headIndex, targetX, targetY);
    }

    /// <summary>
    /// Replaces an offline autotrade visual only when its current listing is
    /// the expected snapshot. This keeps the NPC visual synchronized with the
    /// book without allowing a stale purchase to overwrite a newer state.
    /// </summary>
    public bool TryUpdateAutoTradeNpcSnapshot(
        int connectionId,
        LegacyAutoTradeSnapshot expectedSnapshot,
        LegacyAutoTradeSnapshot updatedSnapshot)
    {
        ArgumentNullException.ThrowIfNull(expectedSnapshot);
        ArgumentNullException.ThrowIfNull(updatedSnapshot);
        if (expectedSnapshot.ConnectionId != connectionId || updatedSnapshot.ConnectionId != connectionId)
            return false;

        lock (gate)
        {
            if (!npcs.TryGetValue(connectionId, out var npc) ||
                npc.AutoTradeSnapshot is null ||
                !SameAutoTradeSnapshot(npc.AutoTradeSnapshot, expectedSnapshot))
                return false;

            npcs[connectionId] = npc with
            {
                AutoTradeSnapshot = updatedSnapshot with
                {
                    Items = updatedSnapshot.Items.ToArray(),
                    CarryPositions = updatedSnapshot.CarryPositions.ToArray(),
                    Prices = updatedSnapshot.Prices.ToArray(),
                },
            };
            return true;
        }
    }

    /// <summary>
    /// Ports the Perzen branch of <c>Exec_MSG_Quest</c>. The legacy dispatch
    /// identifies Perzen by Merchant=100 and BaseScore.Level 7/8/9, then
    /// exchanges the first matching carry item for the NPC's Carry[1] item in
    /// the same player slot, with the legacy 30-day date encoding.
    /// </summary>
    public LegacyPerzenExchangeResult TryExchangePerzenItem(int connectionId, int npcConnectionId, out LegacyPerzenExchangeOutcome? outcome, DateTime? localNow = null)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyPerzenExchangeResult.ParticipantNotFound;
            if (!npcs.TryGetValue(npcConnectionId, out var npc))
                return LegacyPerzenExchangeResult.NpcNotFound;
            if (!IsPerzenNpc(npc.MobSnapshot))
                return LegacyPerzenExchangeResult.NotPerzen;
            if (npc.MobSnapshot.Length < LegacyAccountSnapshot.MobCarryOffset + (2 * LegacyItem.SizeInBytes))
                return LegacyPerzenExchangeResult.InvalidNpc;
            if (participant.Mob.Length < LegacyAccountSnapshot.MobCarryOffset + (LegacyAccountSnapshot.MobCarryCount * LegacyItem.SizeInBytes))
                return LegacyPerzenExchangeResult.InventoryUnavailable;

            var requiredItem = LegacyItem.Read(npc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes));
            var rewardTemplate = LegacyItem.Read(npc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCarryOffset + LegacyItem.SizeInBytes, LegacyItem.SizeInBytes));
            if (requiredItem.Index <= 0 || rewardTemplate.Index <= 0)
                return LegacyPerzenExchangeResult.InvalidNpc;

            var inventorySlot = -1;
            for (var slot = 0; slot < LegacyAccountSnapshot.MobCarryCount; slot++)
            {
                var candidate = LegacyItem.Read(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (slot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
                if (candidate.Index == requiredItem.Index)
                {
                    inventorySlot = slot;
                    break;
                }
            }

            if (inventorySlot < 0)
            {
                outcome = new LegacyPerzenExchangeOutcome(npcConnectionId, -1, requiredItem.Index, rewardTemplate.Index, default);
                return LegacyPerzenExchangeResult.MissingItem;
            }

            var updatedItem = LegacyItemDateMath.SetItemDate(
                new LegacyItem((short)rewardTemplate.Index, 0, 0, 0, 0, 0, 0),
                30,
                localNow ?? DateTime.Now);
            updatedItem.Write(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (inventorySlot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
            outcome = new LegacyPerzenExchangeOutcome(npcConnectionId, inventorySlot, requiredItem.Index, rewardTemplate.Index, updatedItem);
            return LegacyPerzenExchangeResult.Accepted;
        }
    }

    private static bool IsPerzenNpc(ReadOnlySpan<byte> mob)
    {
        if (mob.Length < LegacyAccountSnapshot.MobBaseScoreOffset + sizeof(int) || mob.Length < LegacyAccountSnapshot.MobMerchantOffset + 1)
            return false;

        var merchant = mob[LegacyAccountSnapshot.MobMerchantOffset];
        var grade = BinaryPrimitives.ReadInt32LittleEndian(mob[LegacyAccountSnapshot.MobBaseScoreOffset..]);
        return merchant == 100 && grade is 7 or 8 or 9;
    }

    public static bool IsDonateShopNpcName(string name) => name.Trim().ToUpperInvariant() switch
    {
        "DONATION STORE" or
        "DONATION STORE1" or
        "DONATION STORE2" or
        "DONATION STORE3" or
        "DONATION STORE4" or
        "DONATION STORE5" or
        "DONATION_STORE" or
        "DONATION_STORE1" or
        "DONATION_STORE2" or
        "DONATION_STORE3" or
        "DONATION_STORE4" or
        "DONATION_STORE5" => true,
        _ => false,
    };

    public async ValueTask<bool> EnterAsync(int connectionId, string accountName, Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> send, ReadOnlyMemory<byte> spawnFrame, CancellationToken cancellationToken = default)
    {
        Participant[] existing;
        lock (gate)
        {
            if (participants.ContainsKey(connectionId)) return false;
            existing = participants.Values.ToArray();
            participants.Add(connectionId, new Participant(connectionId, accountName, send, spawnFrame));
            battleRoyaleDoorRemovedMobGridIds.Remove(connectionId);
        }

        foreach (var participant in existing)
            if (!participant.SpawnFrame.IsEmpty) await send(participant.SpawnFrame, cancellationToken);
        if (!spawnFrame.IsEmpty) await BroadcastAsync(connectionId, spawnFrame, cancellationToken);
        return true;
    }

    public bool Leave(int connectionId) => Leave(connectionId, out _);

    /// <summary>Removes a player and all NPC summons owned by that player.</summary>
    public bool Leave(int connectionId, out IReadOnlyList<int> despawnedSummonIds) =>
        Leave(connectionId, out despawnedSummonIds, out _);

    /// <summary>
    /// Removes a player, all NPC summons owned by that player, and any staged
    /// bilateral trade. The trade outcome is observational only: staged offers
    /// have not mutated carry or Gold, so disconnect cleanup must not invent a
    /// persistence write or a rollback mutation.
    /// </summary>
    public bool Leave(int connectionId, out IReadOnlyList<int> despawnedSummonIds, out LegacyTradeCloseOutcome? closedTrade)
    {
        lock (gate)
        {
            despawnedSummonIds = [];
            closedTrade = null;
            if (!participants.TryGetValue(connectionId, out var participant)) return false;

            var despawned = new List<int>(participant.SummonIds.Count);
            foreach (var summonId in participant.SummonIds.ToArray())
            {
                if (RemoveSummonLocked(summonId))
                {
                    despawned.Add(summonId);
                }
            }

            var partyLeader = partyLeaders.TryGetValue(connectionId, out var currentLeader) ? currentLeader : connectionId;
            foreach (var member in partyLeaders.Where(pair => pair.Value == partyLeader).Select(pair => pair.Key).ToArray())
                partyLeaders.Remove(member);
            foreach (var request in pendingPartyLeaders.Where(pair => pair.Key == connectionId || pair.Value == connectionId).Select(pair => pair.Key).ToArray())
                pendingPartyLeaders.Remove(request);
            if (tradeStates.TryGetValue(connectionId, out var tradeState))
            {
                closedTrade = new LegacyTradeCloseOutcome(connectionId, tradeState.OpponentId);
                ClearTradePairLocked(connectionId);
            }
            participants.Remove(connectionId);
            ClearRemovedTargetLocked(connectionId);
            battleRoyaleDoorRemovedMobGridIds.Remove(connectionId);
            despawnedSummonIds = despawned;
            return true;
        }
    }

    public int Count
    {
        get { lock (gate) return participants.Count; }
    }

    public void SetCombatWorldState(LegacyCombatWorldState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var stateSnapshot = state with
        {
            GuildWarPartners = state.GuildWarPartners is { } guildWarPartners
                ? new System.Collections.ObjectModel.ReadOnlyDictionary<int, int>(new Dictionary<int, int>(guildWarPartners))
                : null,
            GuildAllies = state.GuildAllies is { } guildAllies
                ? new System.Collections.ObjectModel.ReadOnlyDictionary<int, int>(new Dictionary<int, int>(guildAllies))
                : null,
        };
        lock (gate) combatWorldState = stateSnapshot;
    }

    public void SetDoubleModeMultiplier(int multiplier)
    {
        lock (gate)
            combatWorldState = combatWorldState with { DoubleModeMultiplier = multiplier };
    }

    public void SetBattleRoyaleState(int state, int item)
    {
        lock (gate)
            combatWorldState = combatWorldState with
            {
                BattleRoyaleState = state,
                BattleRoyaleItem = item,
            };
    }

    /// <summary>7.69 refreshes <c>BrGrid = tm_min / 20</c> on every timer tick of an eligible BR server.</summary>
    public void SetBattleRoyaleGrid(int grid)
    {
        lock (gate)
            combatWorldState = combatWorldState with { BattleRoyaleGrid = grid };
    }

    public LegacyCombatWorldState GetCombatWorldState()
    {
        lock (gate) return combatWorldState;
    }

    /// <summary>Whether this player's chat text gets the 7.69 Battle Royale <c>??????</c> mask.</summary>
    public bool ShouldMaskBattleRoyaleChat(int connectionId)
    {
        lock (gate)
            return participants.TryGetValue(connectionId, out var participant) &&
                LegacyBattleRoyaleArenaRules.MasksChat(combatWorldState, participant.PositionX, participant.PositionY);
    }

    /// <summary>
    /// 7.69 <c>_MSG_Action.cpp:308-322</c>: after an accepted move inside the Battle Royale arena,
    /// <c>DoRecall</c> players in the <c>BrGrid</c> level band, capturing both views atomically.
    /// </summary>
    public bool TryRecallForBattleRoyaleArenaWithViews(int connectionId, out LegacyRecallTransition? transition)
    {
        lock (gate)
        {
            transition = null;
            if (!participants.TryGetValue(connectionId, out var participant) ||
                participant.Mob.Length < LegacyAccountSnapshot.MobCurrentScoreOffset + LegacyScore.SizeInBytes)
                return false;

            var state = LegacyMobCombatState.Read(participant.Mob);
            if (!LegacyBattleRoyaleArenaRules.RecallsOnAction(combatWorldState, participant.PositionX, participant.PositionY, state.CurrentScore.Level))
                return false;

            return TryCaptureRecallTransitionLocked(
                participant,
                state,
                out transition,
                cityRandomX: -1,
                cityRandomY: -1,
                newbieRandomX: -1,
                newbieRandomY: -1);
        }
    }

    public LegacyBattleRoyaleEnvironmentalSnapshot? TryApplyBattleRoyaleEnvironmentalDamage(
        LegacyBattleRoyaleEnvironmentalStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(step.Areas);

        lock (gate)
        {
            if (combatWorldState.BattleRoyaleState != 2 || combatWorldState.BattleRoyaleItem <= 0)
                return null;

            var damageTargets = new List<LegacyBattleRoyaleEnvironmentalDamageTargetSnapshot>();
            var effects = new List<LegacyBattleRoyaleEnvironmentalEffectSnapshot>(step.Areas.Count);
            foreach (var area in step.Areas)
            {
                if (area.X1 > area.X2 || area.Y1 > area.Y2)
                    continue;

                var effectRecipients = GetParticipantIdsInViewLocked(
                    checked((short)area.CenterX),
                    checked((short)area.CenterY));
                effects.Add(new LegacyBattleRoyaleEnvironmentalEffectSnapshot(
                    area,
                    Array.AsReadOnly(effectRecipients)));

                foreach (var participant in participants.Values.OrderBy(static participant => participant.ConnectionId))
                {
                    if (participant.ConnectionId is <= 0 or >= LegacyBattleRoyaleGridRefreshRelay.MaxUserConnectionExclusive ||
                        participant.Mob.Length < LegacyAccountSnapshot.MobCurrentScoreOffset + LegacyScore.SizeInBytes ||
                        !area.Contains(participant.PositionX, participant.PositionY))
                        continue;

                    var scoreSpan = participant.Mob.AsSpan(
                        LegacyAccountSnapshot.MobCurrentScoreOffset,
                        LegacyScore.SizeInBytes);
                    var score = LegacyScore.Read(scoreSpan);
                    if (score.Hp <= 0)
                        continue;

                    // The approved BR rule keeps living participants at 1 HP, including the exact 2,000 HP boundary.
                    var nextHp = Math.Max(score.Hp - 2_000, 1);
                    var nextMp = score.Mp > score.MaxMp ? score.MaxMp : score.Mp;
                    (score with { Hp = nextHp, Mp = nextMp }).Write(scoreSpan);

                    var recipients = GetParticipantIdsInViewLocked(participant.PositionX, participant.PositionY);
                    damageTargets.Add(new LegacyBattleRoyaleEnvironmentalDamageTargetSnapshot(
                        participant.ConnectionId,
                        participant.PositionX,
                        participant.PositionY,
                        nextHp,
                        nextHp - score.Hp,
                        Array.AsReadOnly(recipients)));
                }
            }

            return new LegacyBattleRoyaleEnvironmentalSnapshot(
                Array.AsReadOnly(damageTargets.ToArray()),
                Array.AsReadOnly(effects.ToArray()));
        }
    }

    /// <summary>Returns connected players inside an inclusive legacy notice rectangle.</summary>
    public IReadOnlyList<int> GetParticipantIdsInArea(int x1, int y1, int x2, int y2)
    {
        if (x1 > x2 || y1 > y2)
            return [];

        lock (gate)
        {
            return participants.Values
                .Where(participant => participant.PositionX >= x1 && participant.PositionX <= x2 && participant.PositionY >= y1 && participant.PositionY <= y2)
                .Select(static participant => participant.ConnectionId)
                .Order()
                .ToArray();
        }
    }

    /// <summary>Captures each in-arena player's complete visible mob and item grid for a legacy SendGridMob refresh.</summary>
    public IReadOnlyList<LegacyBattleRoyaleGridRefreshRecipientSnapshot> GetBattleRoyaleGridRefreshSnapshots(
        int arenaX1,
        int arenaY1,
        int arenaX2,
        int arenaY2)
    {
        if (arenaX1 > arenaX2 || arenaY1 > arenaY2)
            return [];

        lock (gate)
        {
            var result = new List<LegacyBattleRoyaleGridRefreshRecipientSnapshot>();
            foreach (var recipient in participants.Values
                         .Where(participant => participant.ConnectionId is > 0 and < LegacyBattleRoyaleGridRefreshRelay.MaxUserConnectionExclusive &&
                                               participant.PositionX >= arenaX1 && participant.PositionX <= arenaX2 &&
                                               participant.PositionY >= arenaY1 && participant.PositionY <= arenaY2)
                         .OrderBy(static participant => participant.ConnectionId))
            {
                const int halfView = 16;
                bool InView(short x, short y) =>
                    x >= recipient.PositionX - halfView && x <= recipient.PositionX + halfView &&
                    y >= recipient.PositionY - halfView && y <= recipient.PositionY + halfView;

                var mobs = new List<LegacyBattleRoyaleGridMobSnapshot>();
                foreach (var participant in participants.Values)
                {
                    if (participant.ConnectionId == recipient.ConnectionId || !InView(participant.PositionX, participant.PositionY) ||
                        participant.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                        continue;

                    var pkState = ResolveGridRefreshPkStateLocked(participant.Mob, participant.PkMode);
                    mobs.Add(new LegacyBattleRoyaleGridMobSnapshot(
                        participant.ConnectionId,
                        participant.PositionX,
                        participant.PositionY,
                        participant.Mob.ToArray(),
                        participant.Affect.ToArray(),
                        IsNpc: false,
                        IsSummon: false,
                        pkState,
                        participant.ClientEquipment.ToArray()));
                }

                foreach (var npc in npcs.Values)
                {
                    if (!InView(npc.PositionX, npc.PositionY) ||
                        npc.MobSnapshot.Length < WorldHubLegacyOffsets.LegacyAccountMobSize ||
                        npc.AffectSnapshot.Length != LegacyAccountSnapshot.AffectStride)
                        continue;

                    mobs.Add(new LegacyBattleRoyaleGridMobSnapshot(
                        npc.ConnectionId,
                        npc.PositionX,
                        npc.PositionY,
                        npc.MobSnapshot.ToArray(),
                        npc.AffectSnapshot.ToArray(),
                        IsNpc: true,
                        IsSummon: false,
                        ResolveGridRefreshPkStateLocked(npc.MobSnapshot, pkMode: false),
                        ClientEquipment: null,
                        npc.AutoTradeSnapshot));
                }

                foreach (var summon in summons.Values)
                {
                    if (!InView(summon.PositionX, summon.PositionY) ||
                        summon.MobSnapshot.Length < WorldHubLegacyOffsets.LegacyAccountMobSize ||
                        summon.AffectSnapshot.Length != LegacyAccountSnapshot.AffectStride)
                        continue;

                    mobs.Add(new LegacyBattleRoyaleGridMobSnapshot(
                        summon.ConnectionId,
                        summon.PositionX,
                        summon.PositionY,
                        summon.MobSnapshot.ToArray(),
                        summon.AffectSnapshot.ToArray(),
                        IsNpc: true,
                        IsSummon: true,
                        ResolveGridRefreshPkStateLocked(summon.MobSnapshot, pkMode: false),
                        ClientEquipment: null,
                        AutoTradeSnapshot: null));
                }

                result.Add(new LegacyBattleRoyaleGridRefreshRecipientSnapshot(
                    recipient.ConnectionId,
                    Array.AsReadOnly(mobs.ToArray()),
                    Array.AsReadOnly(GetItemsInViewLocked(recipient.PositionX, recipient.PositionY))));
            }

            return Array.AsReadOnly(result.ToArray());
        }
    }

    private int ResolveGridRefreshPkStateLocked(byte[] mob, bool pkMode)
    {
        if (combatWorldState.NewbieEventServer)
            return 1;

        return ReadLegacyGuilty(mob) != 0 || pkMode || combatWorldState.RvrWarActive ||
               combatWorldState.RvrState != 0 || combatWorldState.CastleState != 0 ||
               combatWorldState.GuildTowerState != 0
            ? 1
            : 0;
    }

    internal IReadOnlyList<LegacyBattleRoyaleAreaClearSubject> ApplyBattleRoyaleAreaClearLevels(
        int x1,
        int y1,
        int x2,
        int y2,
        LegacyBattleRoyaleLevelClear levels,
        int cityRandomX,
        int cityRandomY)
    {
        ArgumentNullException.ThrowIfNull(levels);
        if (x1 > x2 || y1 > y2 || levels.MinLevel < 1 || levels.MinLevel > levels.MaxLevel || levels.MaxLevel > 400)
            return [];

        lock (gate)
        {
            var cleared = new List<LegacyBattleRoyaleAreaClearSubject>();
            foreach (var participant in participants.Values.OrderBy(static participant => participant.ConnectionId))
            {
                if (participant.PositionX < x1 || participant.PositionX > x2 ||
                    participant.PositionY < y1 || participant.PositionY > y2 ||
                    participant.Mob.Length < LegacyAccountSnapshot.MobCurrentScoreOffset + LegacyScore.SizeInBytes)
                    continue;

                var state = LegacyMobCombatState.Read(participant.Mob);
                if (state.CurrentScore.Level < levels.MinLevel || state.CurrentScore.Level > levels.MaxLevel)
                    continue;

                byte[]? revivedMobSnapshot = null;
                byte[]? revivedAffectSnapshot = null;
                if (state.CurrentScore.Hp <= 0)
                {
                    var revivedScore = state.CurrentScore with { Hp = 2 };
                    revivedScore.Write(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
                    participant.RequestedHp = 2;
                    state = state with { CurrentScore = revivedScore };
                    revivedMobSnapshot = participant.Mob.ToArray();
                    revivedAffectSnapshot = participant.Affect.ToArray();
                }

                if (!TryCaptureRecallTransitionLocked(
                        participant,
                        state,
                        out var recallTransition,
                        cityRandomX,
                        cityRandomY,
                        newbieRandomX: -1,
                        newbieRandomY: -1) || recallTransition is null)
                    continue;

                cleared.Add(new LegacyBattleRoyaleAreaClearSubject(
                    recallTransition,
                    revivedMobSnapshot,
                    revivedAffectSnapshot));
            }

            return Array.AsReadOnly(cleared.ToArray());
        }
    }

    /// <summary>Returns players in the inclusive 33x33 legacy GridMulticast view around an NPC.</summary>
    public IReadOnlyList<int> GetParticipantIdsInNpcView(int npcConnectionId)
    {
        lock (gate)
        {
            if (!npcs.TryGetValue(npcConnectionId, out var npc))
                return [];

            const int halfView = 16;
            return participants.Values
                .Where(participant => participant.PositionX >= npc.PositionX - halfView && participant.PositionX <= npc.PositionX + halfView &&
                                      participant.PositionY >= npc.PositionY - halfView && participant.PositionY <= npc.PositionY + halfView)
                .Select(static participant => participant.ConnectionId)
                .ToArray();
        }
    }

    /// <summary>Returns connected players in the inclusive 33x33 view around a tile (GridMulticast recipients).</summary>
    public IReadOnlyList<int> GetParticipantIdsInView(short x, short y)
    {
        lock (gate)
            return GetParticipantIdsInViewLocked(x, y);
    }

    /// <summary>Returns connected players in the inclusive 33x33 view around a player.</summary>
    public IReadOnlyList<int> GetParticipantIdsInPlayerView(int connectionId)
    {
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var origin))
                return [];

            return GetParticipantIdsInViewLocked(origin.PositionX, origin.PositionY);
        }
    }

    /// <summary>Returns online player slots assigned to one legacy timer shard.</summary>
    public IReadOnlyList<int> GetParticipantIdsForTimerShard(int shardIndex, int shardCount)
    {
        if (shardCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(shardCount));
        if (shardIndex < 0 || shardIndex >= shardCount)
            throw new ArgumentOutOfRangeException(nameof(shardIndex));

        lock (gate)
            return participants.Keys
                .Where(connectionId => connectionId % shardCount == shardIndex)
                .OrderBy(static connectionId => connectionId)
                .ToArray();
    }

    /// <summary>
    /// Applies the 7.69 BonusType=0 score-point allocation while the participant is alive.
    /// Special and skill bonus allocation remain separate protocol branches.
    /// </summary>
    public LegacyBaseScoreBonusResult TryApplyBaseScoreBonus(
        int connectionId,
        int detail,
        out LegacyBaseScoreBonusOutcome? outcome)
    {
        outcome = null;
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyBaseScoreBonusResult.ParticipantNotFound;
            if (participant.Mob.Length < LegacyAccountSnapshot.CharacterStride ||
                participant.Affect.Length != LegacyAccountSnapshot.AffectStride ||
                participant.MobExtra.Length != LegacyAccountSnapshot.MobExtraStride)
                return LegacyBaseScoreBonusResult.CharacterStateUnavailable;

            var state = LegacyMobCombatState.Read(participant.Mob);
            if (state.CurrentScore.Hp <= 0)
                return LegacyBaseScoreBonusResult.NotAlive;

            var scoreBonus = BinaryPrimitives.ReadInt16LittleEndian(
                participant.Mob.AsSpan(LegacyAccountSnapshot.MobScoreBonusOffset));
            if (scoreBonus <= 0)
            {
                outcome = new LegacyBaseScoreBonusOutcome(
                    participant.Mob.ToArray(), participant.Affect.ToArray(), participant.MobExtra.ToArray(),
                    SendEtc: false, SendScore: true);
                return LegacyBaseScoreBonusResult.NoPoints;
            }
            if (detail is < 0 or > 3)
                return LegacyBaseScoreBonusResult.InvalidDetail;
            if (itemData is null)
                return LegacyBaseScoreBonusResult.ItemDataUnavailable;

            var points = scoreBonus >= 300 ? 100 : 1;
            var nextMob = participant.Mob.ToArray();
            var baseScore = LegacyScore.Read(nextMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
            baseScore = detail switch
            {
                0 => baseScore with { Strength = unchecked((short)(baseScore.Strength + points)) },
                1 => baseScore with
                {
                    Intelligence = unchecked((short)(baseScore.Intelligence + points)),
                    MaxMp = unchecked(baseScore.MaxMp + (2 * points)),
                },
                2 => baseScore with { Dexterity = unchecked((short)(baseScore.Dexterity + points)) },
                3 => baseScore with
                {
                    Constitution = unchecked((short)(baseScore.Constitution + points)),
                    MaxHp = unchecked(baseScore.MaxHp + (2 * points)),
                },
                _ => throw new InvalidOperationException("Detail was range-checked above."),
            };
            baseScore.Write(nextMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset, LegacyScore.SizeInBytes));
            BinaryPrimitives.WriteInt16LittleEndian(
                nextMob.AsSpan(LegacyAccountSnapshot.MobScoreBonusOffset),
                checked((short)(scoreBonus - points)));

            var projected = ProjectPlayerScoreLocked(
                participant,
                nextMob,
                participant.Affect,
                itemData,
                preserveHpMp: true);
            participant.Mob = projected.Mob;
            participant.ClientEquipment = projected.ClientEquipment?.ToArray() ?? participant.ClientEquipment;
            outcome = new LegacyBaseScoreBonusOutcome(
                participant.Mob.ToArray(), participant.Affect.ToArray(), participant.MobExtra.ToArray(),
                SendEtc: true, SendScore: true);
            return LegacyBaseScoreBonusResult.Applied;
        }
    }

    /// <summary>
    /// Applies the TMProject 7.69 BonusType=1 Special point branch while the
    /// participant is alive. The W2PP-only SecLearnedSkill cap extension is excluded.
    /// </summary>
    public LegacySpecialScoreBonusResult TryApplySpecialScoreBonus(
        int connectionId,
        int detail,
        out LegacySpecialScoreBonusOutcome? outcome)
    {
        outcome = null;
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacySpecialScoreBonusResult.ParticipantNotFound;
            if (participant.Mob.Length < LegacyAccountSnapshot.CharacterStride ||
                participant.Affect.Length != LegacyAccountSnapshot.AffectStride ||
                participant.MobExtra.Length != LegacyAccountSnapshot.MobExtraStride)
                return LegacySpecialScoreBonusResult.CharacterStateUnavailable;

            var state = LegacyMobCombatState.Read(participant.Mob);
            if (state.CurrentScore.Hp <= 0)
                return LegacySpecialScoreBonusResult.NotAlive;

            var specialBonus = BinaryPrimitives.ReadInt16LittleEndian(
                participant.Mob.AsSpan(LegacyAccountSnapshot.MobSpecialBonusOffset));
            if (specialBonus <= 0)
            {
                outcome = new LegacySpecialScoreBonusOutcome(
                    participant.Mob.ToArray(), participant.Affect.ToArray(), participant.MobExtra.ToArray(),
                    SendEtc: true, SendScore: false);
                return LegacySpecialScoreBonusResult.NoPoints;
            }
            if (detail is < 0 or > 3)
                return LegacySpecialScoreBonusResult.InvalidDetail;

            var baseScore = LegacyScore.Read(participant.Mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
            var maxSpecialLevel = unchecked(3 * (baseScore.Level + 1));
            if (participant.ClassMaster is LegacyExperienceMath.ClassMasterCelestial or
                LegacyExperienceMath.ClassMasterCelestialCs or LegacyExperienceMath.ClassMasterSCelestial)
                maxSpecialLevel = unchecked(maxSpecialLevel + (3 * 400));

            var currentSpecial = detail switch
            {
                0 => baseScore.Special1,
                1 => baseScore.Special2,
                2 => baseScore.Special3,
                3 => baseScore.Special4,
                _ => throw new InvalidOperationException("Detail was range-checked above."),
            };
            if (currentSpecial >= (maxSpecialLevel >> 1))
                return LegacySpecialScoreBonusResult.LevelCapReached;

            var maxSpecial = 200;
            var learnedSkills = BinaryPrimitives.ReadUInt32LittleEndian(
                participant.Mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset));
            if ((detail == 1 && (learnedSkills & (1u << 7)) != 0) ||
                (detail == 2 && (learnedSkills & (1u << 15)) != 0) ||
                (detail == 3 && (learnedSkills & (1u << 23)) != 0))
                maxSpecial = 255;

            // W2PP adds a SecLearnedSkill mask that raises this cap to 320.
            // That fork-only rule is not present in the release-reference 7.69 handler.
            if (currentSpecial >= maxSpecial)
                return LegacySpecialScoreBonusResult.AttributeCapReached;
            if (itemData is null)
                return LegacySpecialScoreBonusResult.ItemDataUnavailable;

            var nextMob = participant.Mob.ToArray();
            baseScore = detail switch
            {
                0 => baseScore with { Special1 = unchecked((short)(baseScore.Special1 + 1)) },
                1 => baseScore with { Special2 = unchecked((short)(baseScore.Special2 + 1)) },
                2 => baseScore with { Special3 = unchecked((short)(baseScore.Special3 + 1)) },
                3 => baseScore with { Special4 = unchecked((short)(baseScore.Special4 + 1)) },
                _ => throw new InvalidOperationException("Detail was range-checked above."),
            };
            baseScore.Write(nextMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset, LegacyScore.SizeInBytes));
            BinaryPrimitives.WriteInt16LittleEndian(
                nextMob.AsSpan(LegacyAccountSnapshot.MobSpecialBonusOffset), checked((short)(specialBonus - 1)));

            var projected = ProjectPlayerScoreLocked(
                participant,
                nextMob,
                participant.Affect,
                itemData,
                preserveHpMp: true);
            participant.Mob = projected.Mob;
            participant.ClientEquipment = projected.ClientEquipment?.ToArray() ?? participant.ClientEquipment;
            outcome = new LegacySpecialScoreBonusOutcome(
                participant.Mob.ToArray(), participant.Affect.ToArray(), participant.MobExtra.ToArray(),
                SendEtc: true, SendScore: true);
            return LegacySpecialScoreBonusResult.Applied;
        }
    }

    /// <summary>
    /// Applies the TMProject 7.69 BonusType=2 base skill-tree learn request.
    /// This changes the first base LearnedSkill mask, signed SkillBonus, Gold
    /// for a final skill, and the projected CurrentScore; it does not create a
    /// second learned word or bridge MOBEXTRA into that state.
    /// </summary>
    public LegacySkillBonusResult TryApplySkillBonus(
        int connectionId,
        int detail,
        int targetId,
        out LegacySkillBonusOutcome? outcome,
        out int requiredCoin)
    {
        outcome = null;
        requiredCoin = 0;
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacySkillBonusResult.ParticipantNotFound;
            if (participant.Mob.Length < LegacyAccountSnapshot.CharacterStride ||
                participant.Affect.Length != LegacyAccountSnapshot.AffectStride ||
                participant.MobExtra.Length != LegacyAccountSnapshot.MobExtraStride)
                return LegacySkillBonusResult.CharacterStateUnavailable;

            var state = LegacyMobCombatState.Read(participant.Mob);
            if (state.CurrentScore.Hp <= 0)
                return LegacySkillBonusResult.NotAlive;
            if (!LegacySkillLearningRules.TryDecodeDetail(detail, out var skillClass, out var skillPosition, out var skillId))
                return LegacySkillBonusResult.InvalidDetail;
            if (!LegacySkillLearningRules.IsSkillMasterTarget(targetId))
                return LegacySkillBonusResult.InvalidTarget;

            if (participant.Mob[LegacyAccountSnapshot.MobClassOffset] != skillClass)
                return LegacySkillBonusResult.ClassMismatch;

            if (skillData is null || skillData[skillId] is not { } skill)
                return LegacySkillBonusResult.SkillDataUnavailable;

            var skillBonus = BinaryPrimitives.ReadInt16LittleEndian(
                participant.Mob.AsSpan(LegacyAccountSnapshot.MobSkillBonusOffset));
            var availableSkillPoints = LegacySkillLearningRules.AvailableSkillPoints(participant.ClassMaster, skillBonus);
            if (skill.SkillPoint > availableSkillPoints)
                return LegacySkillBonusResult.InsufficientSkillPoints;

            var learnedSkills = BinaryPrimitives.ReadUInt32LittleEndian(
                participant.Mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset));
            var isFinalSkill = LegacySkillLearningRules.IsFinalSkill(skillPosition);
            var coin = BinaryPrimitives.ReadInt32LittleEndian(
                participant.Mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset));
            if (isFinalSkill)
            {
                if (LegacySkillLearningRules.HasAnyFinalSkill(learnedSkills))
                    return LegacySkillBonusResult.OnlyOneFinalSkill;
                if (!LegacySkillLearningRules.HasSevenBranchPrerequisites(learnedSkills, skillPosition))
                    return LegacySkillBonusResult.MissingPrerequisites;

                requiredCoin = LegacySkillLearningRules.FinalSkillCoinCost(participant.ClassMaster);
                if (coin < requiredCoin)
                    return LegacySkillBonusResult.InsufficientCoin;
            }

            var learnedBit = 1u << skillPosition;
            if ((learnedSkills & learnedBit) != 0)
                return LegacySkillBonusResult.AlreadyLearned;

            if (itemData is null || itemData[detail] is not { } item)
                return LegacySkillBonusResult.ItemDataUnavailable;

            var requiredLevel = participant.ClassMaster is LegacyAccountSnapshot.ClassMasterMortal or LegacyAccountSnapshot.ClassMasterArch
                ? item.RequiredLevel
                : 0;
            if (state.CurrentScore.Level < requiredLevel)
                return LegacySkillBonusResult.RequiredLevel;
            if (state.CurrentScore.Special2 < item.RequiredIntelligence ||
                state.CurrentScore.Special3 < item.RequiredDexterity ||
                state.CurrentScore.Special4 < item.RequiredConstitution)
                return LegacySkillBonusResult.RequiredAttributes;

            var nextMob = participant.Mob.ToArray();
            BinaryPrimitives.WriteUInt32LittleEndian(
                nextMob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), learnedSkills | learnedBit);
            BinaryPrimitives.WriteInt16LittleEndian(
                nextMob.AsSpan(LegacyAccountSnapshot.MobSkillBonusOffset), unchecked((short)(skillBonus - skill.SkillPoint)));
            if (isFinalSkill)
                BinaryPrimitives.WriteInt32LittleEndian(
                    nextMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), coin - requiredCoin);

            // TMProject CMob::GetCurrentScore saves and restores CurrentScore.Hp/Mp
            // around BASE_GetCurrentScore; preserve those live resources here too.
            var projected = ProjectPlayerScoreLocked(
                participant,
                nextMob,
                participant.Affect,
                itemData,
                preserveHpMp: true);
            participant.Mob = projected.Mob;
            participant.Coin = BinaryPrimitives.ReadInt32LittleEndian(
                participant.Mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset));
            participant.ClientEquipment = projected.ClientEquipment?.ToArray() ?? participant.ClientEquipment;
            outcome = new LegacySkillBonusOutcome(
                participant.Mob.ToArray(),
                participant.Affect.ToArray(),
                participant.MobExtra.ToArray(),
                SendScore: true,
                SendEtc: true);
            return LegacySkillBonusResult.Applied;
        }
    }

    /// <summary>
    /// Advances active player affect timers once, applies the supported HP ticks and expiry projections,
    /// and captures the player view atomically. Type 22/23 attack dispatch remains a separate combat path.
    /// Transformation expiry may also request an equipment refresh.
    /// </summary>
    public LegacyPlayerAffectTimerOutcome? TryProcessPlayerTimerAffects(int connectionId)
    {
        const byte lifeAuraAffectType = 17;
        const byte poisonAffectType = 20;
        const byte thunderAffectType = 22;
        const byte bestialAuraAffectType = 23;
        const byte skillTransformationAffectType = 16;
        const byte itemTransformationAffectType = 33;
        const int affectSizeInBytes = 8;
        const uint maximumFiniteAffectTime = 32_400_000;

        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant) ||
                participant.Mob.Length < LegacyAccountSnapshot.CharacterStride ||
                participant.Affect.Length != LegacyAccountSnapshot.AffectStride ||
                participant.MobExtra.Length != LegacyAccountSnapshot.MobExtraStride ||
                LegacyMobCombatState.Read(participant.Mob).CurrentScore.Hp <= 0)
                return null;

            var nextAffect = participant.Affect.ToArray();
            var nextMob = participant.Mob.ToArray();
            var score = LegacyMobCombatState.Read(nextMob).CurrentScore;
            var hp = score.Hp;
            var requestedHp = participant.RequestedHp;
            var damage = 0;
            var hasActiveAffect = false;
            var hpUpdateRequired = false;
            var scoreRefreshRequired = false;
            var equipmentAffectExpired = false;
            var areaAttacks = new List<LegacyPlayerAffectAreaAttackOutcome>();
            var fairyAttack = TryRunPlayerFairyTimerAttackLocked(participant, out var resourceRefreshRequired);
            if (fairyAttack is not null)
                areaAttacks.Add(fairyAttack);
            nextMob = participant.Mob.ToArray();
            score = LegacyMobCombatState.Read(nextMob).CurrentScore;
            hp = score.Hp;
            requestedHp = participant.RequestedHp;
            for (var offset = 0; offset < nextAffect.Length; offset += affectSizeInBytes)
            {
                var affectType = nextAffect[offset];
                if (affectType == 0)
                    continue;

                hasActiveAffect = true;
                if ((affectType is thunderAffectType or bestialAuraAffectType) &&
                    TryRunPlayerAffectAreaAttackLocked(participant, affectType) is { } areaAttack)
                {
                    areaAttacks.Add(areaAttack);
                    var preAttackScore = LegacyMobCombatState.Read(participant.Mob).CurrentScore;
                    if (preAttackScore.Hp != hp)
                    {
                        (preAttackScore with { Hp = hp }).Write(
                            participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
                        participant.RequestedHp = Math.Max(Math.Max(requestedHp, 0), hp);
                    }
                    nextMob = participant.Mob.ToArray();
                    score = LegacyMobCombatState.Read(nextMob).CurrentScore;
                    hp = score.Hp;
                    requestedHp = participant.RequestedHp;
                }

                if (affectType == lifeAuraAffectType)
                {
                    hpUpdateRequired = true;
                    var previousHp = hp;
                    var affectLevel = BinaryPrimitives.ReadUInt16LittleEndian(nextAffect.AsSpan(offset + 2));
                    var healedHp = (long)hp + (affectLevel / 2) + nextAffect[offset + 1];
                    if (healedHp < 1)
                        healedHp = 1;
                    if (healedHp > score.MaxHp)
                        healedHp = score.MaxHp;
                    hp = checked((int)healedHp);
                    damage = hp - previousHp;
                    scoreRefreshRequired |= hp != previousHp;
                }
                else if (affectType == poisonAffectType)
                {
                    hpUpdateRequired = true;
                    var previousHp = hp;
                    // 7.69 truncates (100 - 100 / 10) / 100.0 to integer zero for players.
                    // The remaining per-tick loss is therefore bounded by its player Perda=500.
                    const int hpLose = (int)((100 - 100 / 10) / 100.0);
                    const int poisonDamageCap = 500;
                    var minimumRemainingHp = hp - poisonDamageCap;
                    var poisonedHp = (int)((double)hp * hpLose);
                    if (poisonedHp < minimumRemainingHp)
                        poisonedHp = minimumRemainingHp;
                    if (poisonedHp < 1)
                        poisonedHp = 1;
                    if (poisonedHp > score.MaxHp)
                        poisonedHp = score.MaxHp;

                    hp = poisonedHp;
                    damage = hp - previousHp;
                    scoreRefreshRequired |= hp != previousHp;
                    requestedHp = Math.Max(Math.Max(requestedHp + damage, 0), hp);
                }

                var remainingTime = BinaryPrimitives.ReadUInt32LittleEndian(nextAffect.AsSpan(offset + 4));
                if (remainingTime < maximumFiniteAffectTime)
                    remainingTime = unchecked(remainingTime - 1);

                if (remainingTime == 0)
                {
                    nextAffect.AsSpan(offset, affectSizeInBytes).Clear();
                    scoreRefreshRequired = true;
                    equipmentAffectExpired |= affectType is skillTransformationAffectType or itemTransformationAffectType;
                }
                else
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(nextAffect.AsSpan(offset + 4), remainingTime);
                }
            }

            if (!hasActiveAffect && areaAttacks.Count == 0 && !resourceRefreshRequired)
                return null;
            if (!scoreRefreshRequired)
            {
                participant.Affect = nextAffect;
                participant.RequestedHp = requestedHp;
                if (!hpUpdateRequired && areaAttacks.Count == 0 && !resourceRefreshRequired)
                    return null;

                if (!hpUpdateRequired)
                {
                    return new LegacyPlayerAffectTimerOutcome(
                        connectionId,
                        participant.Mob.ToArray(),
                        participant.Affect.ToArray(),
                        EquipmentChanged: false,
                        Array.AsReadOnly(GetParticipantIdsInViewLocked(participant.PositionX, participant.PositionY)))
                    {
                        ScoreRefreshRequired = false,
                        AreaAttacks = Array.AsReadOnly(areaAttacks.ToArray()),
                        ResourceRefreshRequired = resourceRefreshRequired,
                    };
                }

                var nextScore = score with { Hp = hp, Mp = Math.Min(score.Mp, score.MaxMp) };
                nextScore.Write(nextMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
                participant.Mob = nextMob;
                participant.RequestedHp = Math.Max(Math.Max(participant.RequestedHp, 0), nextScore.Hp);
                participant.RequestedMana = Math.Max(Math.Max(participant.RequestedMana, 0), nextScore.Mp);
                return new LegacyPlayerAffectTimerOutcome(
                    connectionId,
                    participant.Mob.ToArray(),
                    participant.Affect.ToArray(),
                    EquipmentChanged: false,
                    Array.AsReadOnly(GetParticipantIdsInViewLocked(participant.PositionX, participant.PositionY)))
                {
                    ScoreRefreshRequired = false,
                    HpUpdateRequired = true,
                    Hp = nextScore.Hp,
                    Damage = damage,
                    AreaAttacks = Array.AsReadOnly(areaAttacks.ToArray()),
                    ResourceRefreshRequired = resourceRefreshRequired,
                };
            }
            if (itemData is null)
                return null;

            (score with { Hp = hp, Mp = Math.Min(score.Mp, score.MaxMp) })
                .Write(nextMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
            LegacyCharacterLoginData projected;
            try
            {
                projected = ProjectPlayerScoreLocked(participant, nextMob, nextAffect, itemData);
            }
            catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException)
            {
                return null;
            }

            participant.Mob = projected.Mob;
            participant.Affect = projected.Affect;
            participant.MobExtra = projected.MobExtra;
            if (projected.ClientEquipment is not null)
                participant.ClientEquipment = projected.ClientEquipment.ToArray();
            var projectedScore = LegacyMobCombatState.Read(participant.Mob).CurrentScore;
            participant.RequestedHp = Math.Max(Math.Max(requestedHp, 0), projectedScore.Hp);
            participant.RequestedMana = Math.Max(Math.Max(participant.RequestedMana, 0), projectedScore.Mp);

            return new LegacyPlayerAffectTimerOutcome(
                connectionId,
                participant.Mob.ToArray(),
                participant.Affect.ToArray(),
                EquipmentChanged: equipmentAffectExpired,
                Array.AsReadOnly(GetParticipantIdsInViewLocked(participant.PositionX, participant.PositionY)))
            {
                ScoreRefreshRequired = true,
                HpUpdateRequired = hpUpdateRequired,
                Hp = projectedScore.Hp,
                Damage = damage,
                AreaAttacks = Array.AsReadOnly(areaAttacks.ToArray()),
                ResourceRefreshRequired = resourceRefreshRequired,
            };
        }
    }

    private LegacyPlayerAffectAreaAttackOutcome? TryRunPlayerAffectAreaAttackLocked(
        Participant attacker,
        byte affectType)
    {
        if (itemData is null || skillData is null || attacker.Mob.Length < LegacyAccountSnapshot.CharacterStride)
            return null;

        var skillIndex = affectType switch
        {
            22 => 33,
            23 => 52,
            _ => -1,
        };
        if (skillIndex < 0 || skillData[skillIndex] is not { } skill)
            return null;

        var attackerState = LegacyMobCombatState.Read(attacker.Mob);
        if (attackerState.CurrentScore.Hp <= 0)
            return null;

        var targets = SelectPlayerAffectAreaAttackTargetsLocked(attacker);
        if (targets.Length == 0)
            return null;

        var recipients = Array.AsReadOnly(GetParticipantIdsInViewLocked(attacker.PositionX, attacker.PositionY));
        var manaResult = TryConsumeSkillManaLocked(
            attacker,
            skill,
            specialOverride: null,
            applyMagicMirror: true,
            out _,
            out var manaSnapshot,
            out _);
        if (manaResult != LegacySkillManaResult.Accepted || manaSnapshot is null)
            return null;

        var slots = targets.Select(static (targetId, slotIndex) =>
            new LegacyAttackActionBatchSlotRequest(
                slotIndex,
                LegacyAttackActionSlotKind.Skill,
                targetId,
                SkillTargetIndex: slotIndex)).ToArray();

        LegacyAttackActionBatchOutcome? batchOutcome = null;
        if (slots.Length > 0)
        {
            var request = new LegacyAttackActionBatchRequest(
                attacker.ConnectionId,
                slots,
                Skill: skill,
                ItemData: itemData,
                Weather: 0,
                ClientTick: ClientTickPolicy.SkipCheckTick)
            {
                SkillIndex = skillIndex,
                TargetX = checked((ushort)attacker.PositionX),
                TargetY = checked((ushort)attacker.PositionY),
            };
            if (!TryApplyAttackActionBatch(request, out batchOutcome) || batchOutcome is null)
                return null;
        }

        var attackerMob = batchOutcome?.AttackerMobSnapshot ?? attacker.Mob;
        return new LegacyPlayerAffectAreaAttackOutcome(
            attacker.ConnectionId,
            skillIndex,
            attacker.PositionX,
            attacker.PositionY,
            attacker.PositionX,
            attacker.PositionY,
            attackerMob.ToArray(),
            Array.AsReadOnly(targets),
            batchOutcome,
            recipients);
    }

    private LegacyPlayerAffectAreaAttackOutcome? TryRunPlayerFairyTimerAttackLocked(
        Participant attacker,
        out bool resourceRefreshRequired)
    {
        resourceRefreshRequired = false;
        if (itemData is null || skillData is null || attacker.Mob.Length < LegacyAccountSnapshot.CharacterStride)
            return null;

        var fairyIndex = LegacyItem.Read(attacker.Mob.AsSpan(
            LegacyAccountSnapshot.MobEquipmentOffset + (13 * LegacyItem.SizeInBytes),
            LegacyItem.SizeInBytes)).Index;
        var skillIndex = fairyIndex switch
        {
            753 => 32,
            769 => 34,
            1726 => 36,
            _ => -1,
        };
        if (skillIndex < 0 || skillData[skillIndex] is not { } skill || attacker.CurrentTarget == 0)
            return null;

        var targetId = attacker.CurrentTarget;
        if (targetId == attacker.ConnectionId)
        {
            attacker.CurrentTarget = 0;
            return null;
        }

        if (!TryGetBattleTargetLocked(targetId, out var targetX, out var targetY, out var targetMob, out _))
        {
            attacker.CurrentTarget = 0;
            return null;
        }

        var targetParticipant = participants.GetValueOrDefault(targetId);
        var targetSummon = summons.GetValueOrDefault(targetId);
        var targetNpc = npcs.GetValueOrDefault(targetId);
        var targetIsPlayer = targetParticipant is not null;
        var targetIsNpc = !targetIsPlayer;
        var targetClan = targetMob[LegacyAccountSnapshot.MobClanOffset];
        var attackerMapAttribute = mapGrid?.GetAttribute(attacker.PositionX, attacker.PositionY) ??
            (attacker.HasMapAttribute ? attacker.MapAttribute : (byte)0);
        if ((attackerMapAttribute & 0x40) == 0 && (!targetIsNpc || targetClan == 4))
        {
            attacker.CurrentTarget = 0;
            return null;
        }
        if (!attacker.PkMode && targetIsPlayer)
        {
            attacker.CurrentTarget = 0;
            return null;
        }

        var attackerLeader = GetPartyLeaderConnectionIdLocked(attacker.ConnectionId);
        var targetLeader = targetParticipant is not null
            ? GetPartyLeaderConnectionIdLocked(targetId)
            : targetSummon is not null
                ? targetSummon.LeaderConnectionId > 0 ? targetSummon.LeaderConnectionId : targetId
                : targetNpc is not null && targetNpc.LeaderId > 0 ? targetNpc.LeaderId : targetId;
        var attackerGuild = attacker.GuildDisabled ? 0 : attacker.GuildId != 0
            ? attacker.GuildId
            : BinaryPrimitives.ReadUInt16LittleEndian(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset));
        var targetGuild = targetParticipant is not null
            ? targetParticipant.GuildDisabled ? 0 : targetParticipant.GuildId != 0
                ? targetParticipant.GuildId
                : BinaryPrimitives.ReadUInt16LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobGuildOffset))
            : BinaryPrimitives.ReadUInt16LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobGuildOffset));
        if (attackerGuild == 0 && targetGuild == 0)
            attackerGuild = -1;

        if (attackerLeader != targetLeader && attackerGuild != targetGuild)
        {
            if (targetParticipant is not null)
            {
                var targetState = LegacyMobCombatState.Read(targetMob);
                var isMaxLevelMerchant = targetState.BaseScore.Level >= LegacyExperienceMath.MaxLevel &&
                    (targetMob[LegacyAccountSnapshot.MobMerchantOffset] & 1) != 0;
                if ((targetState.Rsv & 0x10) != 0 || isMaxLevelMerchant)
                {
                    attacker.CurrentTarget = 0;
                    return null;
                }
            }

            var attackerClan = attacker.Mob[LegacyAccountSnapshot.MobClanOffset];
            var sameLegacyClan = attackerClan == targetClan && attackerClan is 7 or 8;
            if ((!sameLegacyClan || attacker.PkMode) && (!sameLegacyClan || targetIsPlayer))
            {
                // Keep the 7.69/W2PP OR expression: its Y bound is permissive;
                // the common skill resolver still enforces the actual attack range.
                var withinLegacyTargetCoordinates =
                    targetX >= attacker.PositionX - 16 && targetX <= attacker.PositionX + 16 &&
                    (targetY >= attacker.PositionY - 16 || targetY <= attacker.PositionY + 16);
                if (!withinLegacyTargetCoordinates)
                {
                    attacker.CurrentTarget = 0;
                    return null;
                }

                var manaResult = TryConsumeSkillManaLocked(
                    attacker,
                    skill,
                    specialOverride: null,
                    applyMagicMirror: true,
                    out _,
                    out _,
                    out _);
                if (manaResult == LegacySkillManaResult.InsufficientMana)
                {
                    resourceRefreshRequired = true;
                    return null;
                }
                if (manaResult != LegacySkillManaResult.Accepted)
                    return null;

                var request = new LegacyAttackActionBatchRequest(
                    attacker.ConnectionId,
                    [new LegacyAttackActionBatchSlotRequest(
                        0,
                        LegacyAttackActionSlotKind.Skill,
                        targetId,
                        SkillTargetIndex: 0)],
                    Skill: skill,
                    ItemData: itemData,
                    Weather: 0,
                    ClientTick: ClientTickPolicy.SkipCheckTick)
                {
                    SkillIndex = skillIndex,
                    TargetX = checked((ushort)attacker.PositionX),
                    TargetY = checked((ushort)attacker.PositionY),
                };
                if (!TryApplyAttackActionBatch(request, out var batch) || batch is null)
                {
                    resourceRefreshRequired = true;
                    return null;
                }

                var skillSlot = batch.SlotOutcomes.FirstOrDefault(static slot => slot.SlotIndex == 0);
                if (skillSlot?.SkillResult != LegacySkillAttackResult.Accepted || skillSlot.SkillOutcome is null)
                {
                    resourceRefreshRequired = true;
                    return null;
                }

                var recipients = Array.AsReadOnly(GetParticipantIdsInViewLocked(attacker.PositionX, attacker.PositionY));
                return new LegacyPlayerAffectAreaAttackOutcome(
                    attacker.ConnectionId,
                    skillIndex,
                    attacker.PositionX,
                    attacker.PositionY,
                    attacker.PositionX,
                    attacker.PositionY,
                    (batch.AttackerMobSnapshot ?? attacker.Mob).ToArray(),
                    Array.AsReadOnly([targetId]),
                    batch,
                    recipients)
                {
                    OneTargetMessage = true,
                };
            }

            // When the legacy same-clan gate suppresses this branch, it leaves
            // CurrentTarget intact and retries the check on the next timer.
            return null;
        }

        attacker.CurrentTarget = 0;
        return null;
    }

    private int[] SelectPlayerAffectAreaAttackTargetsLocked(Participant attacker)
    {
        const int maximumTargets = 6;
        var attackerMapAttribute = mapGrid?.GetAttribute(attacker.PositionX, attacker.PositionY) ??
            (attacker.HasMapAttribute ? attacker.MapAttribute : (byte)0);
        if ((attackerMapAttribute & 1) != 0)
            return [];

        var attackerClan = attacker.Mob[LegacyAccountSnapshot.MobClanOffset];
        var attackerLeader = GetPartyLeaderConnectionIdLocked(attacker.ConnectionId);
        var attackerGuild = attacker.GuildDisabled
            ? 0
            : attacker.GuildId != 0
                ? attacker.GuildId
                : BinaryPrimitives.ReadUInt16LittleEndian(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset));
        var selected = new List<int>(maximumTargets);
        var seen = new HashSet<int>();

        AddRectangle(radius: 1);
        if (selected.Count < maximumTargets)
            AddRectangle(radius: 4);
        return selected.ToArray();

        void AddRectangle(int radius)
        {
            for (var y = attacker.PositionY - radius; y <= attacker.PositionY; y++)
            for (var x = attacker.PositionX - radius; x <= attacker.PositionX; x++)
            {
                if (selected.Count >= maximumTargets)
                    return;
                if (x < 0 || x >= LegacyWorldItem.MaxGridCoordinate ||
                    y < 0 || y >= LegacyWorldItem.MaxGridCoordinate)
                    continue;

                var targetId = GetParticipantAtPosition(x, y);
                if (targetId <= 0 || targetId == attacker.ConnectionId || !seen.Add(targetId))
                    continue;
                if (!TryGetPlayerAffectAreaTargetLocked(targetId, out var targetMob, out var targetParticipant,
                        out var targetLeader, out var isNpc, out var targetMapAttribute))
                    continue;

                var targetState = LegacyMobCombatState.Read(targetMob);
                var targetClan = targetMob[LegacyAccountSnapshot.MobClanOffset];
                var sameClanNpc = isNpc && attackerClan is 7 or 8 && targetClan == attackerClan;
                var allowedNpc = isNpc && targetClan != 4 && !sameClanNpc;
                if (targetState.CurrentScore.Hp <= 0 || (targetState.CurrentScore.Merchant & 1) != 0 ||
                    (targetState.Rsv & 0x10) != 0 || targetClan == 6 || sameClanNpc ||
                    targetParticipant is not null && GetPartyLeaderConnectionIdLocked(targetId) == attackerLeader ||
                    targetParticipant is null && targetLeader == attackerLeader)
                    continue;
                if (!attacker.PkMode && !allowedNpc)
                    continue;
                if ((attackerMapAttribute & 0x40) == 0 && !allowedNpc)
                    continue;
                var targetGuild = targetParticipant is null
                    ? BinaryPrimitives.ReadUInt16LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobGuildOffset))
                    : targetParticipant.GuildDisabled
                        ? 0
                        : targetParticipant.GuildId;
                var allyGuild = GetAllyGuildIdLocked(attackerGuild);
                if (attackerGuild != 0 &&
                    (targetGuild == attackerGuild || allyGuild != 0 && targetGuild == allyGuild))
                    continue;

                var targetAttribute = mapGrid?.GetAttribute(x, y) ?? targetMapAttribute;
                if ((targetAttribute & 1) != 0)
                    continue;

                selected.Add(targetId);
            }
        }
    }

    private bool TryGetPlayerAffectAreaTargetLocked(
        int targetId,
        out byte[] mob,
        out Participant? targetParticipant,
        out int targetLeader,
        out bool isNpc,
        out byte targetMapAttribute)
    {
        mob = [];
        targetParticipant = null;
        targetLeader = targetId;
        isNpc = true;
        targetMapAttribute = 0;

        if (participants.TryGetValue(targetId, out var participant))
        {
            if (participant.Mob.Length < LegacyAccountSnapshot.CharacterStride)
                return false;
            mob = participant.Mob;
            targetParticipant = participant;
            targetLeader = GetPartyLeaderConnectionIdLocked(targetId);
            targetMapAttribute = participant.HasMapAttribute ? participant.MapAttribute : (byte)0;
            isNpc = false;
            return true;
        }

        if (summons.TryGetValue(targetId, out var summon))
        {
            if (!IsLiveSummonLocked(summon) || summon.MobSnapshot.Length < LegacyAccountSnapshot.CharacterStride)
                return false;
            mob = summon.MobSnapshot;
            targetLeader = summon.LeaderConnectionId > 0 ? summon.LeaderConnectionId : targetId;
            return true;
        }

        if (npcs.TryGetValue(targetId, out var npc))
        {
            if (npc.Mode == LegacyNpcMode.Empty || npc.MobSnapshot.Length < LegacyAccountSnapshot.CharacterStride)
                return false;
            mob = npc.MobSnapshot;
            targetLeader = npc.LeaderId > 0 ? npc.LeaderId : targetId;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Applies a volatile <c>guildon</c>/<c>guildoff</c> state change and captures the 7.69 score
    /// projection and recipients atomically. The persistent character MOB keeps its real Guild.
    /// </summary>
    public LegacyGuildDisableUpdate? TrySetGuildDisabled(int connectionId, bool disabled)
    {
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant) ||
                participant.Mob.Length < LegacyAccountSnapshot.CharacterStride ||
                participant.GuildDisabled == disabled)
                return null;

            participant.GuildDisabled = disabled;
            var scoreMob = participant.Mob.ToArray();
            if (disabled)
                BinaryPrimitives.WriteUInt16LittleEndian(scoreMob.AsSpan(LegacyAccountSnapshot.MobGuildOffset), 0);

            return new LegacyGuildDisableUpdate(
                connectionId,
                disabled,
                scoreMob,
                participant.Affect.ToArray(),
                GetParticipantIdsInViewLocked(participant.PositionX, participant.PositionY));
        }
    }

    private int[] GetParticipantIdsInViewLocked(short x, short y)
    {
        const int halfView = 16;
        return participants.Values
            .Where(participant => participant.PositionX >= x - halfView && participant.PositionX <= x + halfView &&
                                  participant.PositionY >= y - halfView && participant.PositionY <= y + halfView)
            .Select(static participant => participant.ConnectionId)
            .ToArray();
    }

    private LegacyRecallItem[] GetItemsInViewLocked(short x, short y)
    {
        const int halfView = 16;
        var items = new List<LegacyRecallItem>();
        foreach (var mapItem in mapItems.Values)
        {
            if (mapItem.PositionX < x - halfView || mapItem.PositionX > x + halfView ||
                mapItem.PositionY < y - halfView || mapItem.PositionY > y + halfView)
                continue;

            items.Add(new LegacyRecallItem(
                checked((ushort)(mapItem.ItemId + LegacyMapItemStateCodes.WireIdOffset)),
                checked((ushort)mapItem.PositionX),
                checked((ushort)mapItem.PositionY),
                mapItem.Item,
                mapItem.Rotate,
                checked((byte)mapItem.State),
                checked((byte)Math.Clamp(mapItem.Height, 0, byte.MaxValue))));
        }

        foreach (var groundItem in groundItems.Values)
        {
            if (groundItem.PositionX < x - halfView || groundItem.PositionX > x + halfView ||
                groundItem.PositionY < y - halfView || groundItem.PositionY > y + halfView)
                continue;

            items.Add(new LegacyRecallItem(
                checked((ushort)(groundItem.ItemId + LegacyWorldItem.WireIdOffset)),
                checked((ushort)groundItem.PositionX),
                checked((ushort)groundItem.PositionY),
                groundItem.Item,
                groundItem.Rotate,
                checked((byte)LegacyMapItemStateCodes.Open),
                0));
        }

        return items
            .OrderBy(static item => item.GridY)
            .ThenBy(static item => item.GridX)
            .ThenBy(static item => item.WireItemId)
            .ToArray();
    }

    /// <summary>Returns players inside the legacy 33x33 GridMulticast view around a map item.</summary>
    public IReadOnlyList<int> GetParticipantIdsInMapItemView(int wireItemId)
    {
        lock (gate)
        {
            if (!mapItems.TryGetValue(wireItemId - LegacyMapItemStateCodes.WireIdOffset, out var mapItem))
                return [];

            const int halfView = 16;
            return participants.Values
                .Where(participant => participant.PositionX >= mapItem.PositionX - halfView && participant.PositionX <= mapItem.PositionX + halfView &&
                                      participant.PositionY >= mapItem.PositionY - halfView && participant.PositionY <= mapItem.PositionY + halfView)
                .Select(static participant => participant.ConnectionId)
                .ToArray();
        }
    }

    /// <summary>Finds a connected character by the exact legacy character name.</summary>
    public bool TryGetParticipantIdByCharacterName(string characterName, out int connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(characterName);
        lock (gate)
        {
            var participant = participants.Values.FirstOrDefault(candidate =>
                string.Equals(ReadMobName(candidate.Mob), characterName, StringComparison.Ordinal));
            connectionId = participant?.ConnectionId ?? 0;
            return participant is not null;
        }
    }

    /// <summary>Reads the character name for a connected participant.</summary>
    public bool TryGetParticipantCharacterName(int connectionId, out string? characterName)
    {
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant))
            {
                characterName = null;
                return false;
            }

            characterName = ReadMobName(participant.Mob);
            return true;
        }
    }

    /// <summary>Advances the two-step Castle Zakum reset driven by ProcessMinTimer.</summary>
    public bool TryProcessCastleQuestMinute(DateTime localNow, out LegacyCastleQuestPlan? plan)
    {
        lock (gate)
        {
            plan = null;
            if (localNow.Second != 0)
                return false;

            var minuteSlot = new DateTime(localNow.Year, localNow.Month, localNow.Day, localNow.Hour, localNow.Minute, 0, localNow.Kind);
            if (lastCastleQuestMinute == minuteSlot)
                return false;
            lastCastleQuestMinute = minuteSlot;

            if (combatWorldState.CastleQuestClear == 1)
            {
                combatWorldState = combatWorldState with { CastleQuestClear = 2 };
                plan = new LegacyCastleQuestPlan(
                    minuteSlot,
                    PreviousState: 1,
                    CurrentState: 2,
                    AreaNotices: [new LegacyAreaNotice("Quest 2 Castelos reiniciar\u00e1 em 10 segundos.", 2176, 1160, 2300, 1276)],
                    RemovedNpcs: []);
                return true;
            }

            if (combatWorldState.CastleQuestClear != 2)
                return false;

            var removed = npcs.Values
                .Where(static npc => npc.PositionX >= 2180 && npc.PositionX <= 2296 && npc.PositionY >= 1160 && npc.PositionY <= 1269)
                .ToArray();
            foreach (var npc in removed)
            {
                npcs.Remove(npc.ConnectionId);
                ForgetGuildTreasuryNpcRegistrationLocked(npc.ConnectionId);
            }
            combatWorldState = combatWorldState with { CastleQuestClear = 0 };
            plan = new LegacyCastleQuestPlan(minuteSlot, PreviousState: 2, CurrentState: 0, AreaNotices: [], RemovedNpcs: removed);
            return true;
        }
    }

    /// <summary>
    /// Advances the Castle Zakum countdown at the same two-second cadence as
    /// <c>CCastleZakum::ProcessSecTimer</c>. The reference decrements only
    /// when <c>SecCounter % 2 == 0</c>; the slot guard keeps the 100 ms host
    /// scheduler from applying the same legacy tick more than once.
    /// </summary>
    public bool TryProcessCastleQuestSecond(DateTime localNow, out LegacyCastleQuestSecondPlan? plan)
    {
        lock (gate)
        {
            plan = null;
            if ((localNow.Second % 2) != 0)
                return false;

            var secondSlot = new DateTime(localNow.Year, localNow.Month, localNow.Day, localNow.Hour, localNow.Minute, localNow.Second, localNow.Kind);
            if (lastCastleQuestSecond == secondSlot)
                return false;
            lastCastleQuestSecond = secondSlot;

            if (castleQuestTime < 0)
                return false;

            var previousTimeRemaining = castleQuestTime;
            if (castleQuestTime == 0)
            {
                castleQuestTime = -1;
                var removed = npcs.Values
                    .Where(static npc => npc.PositionX >= 2180 && npc.PositionX <= 2296 && npc.PositionY >= 1160 && npc.PositionY <= 1269)
                    .ToArray();
                foreach (var npc in removed)
                {
                    npcs.Remove(npc.ConnectionId);
                    ForgetGuildTreasuryNpcRegistrationLocked(npc.ConnectionId);
                }

                plan = new LegacyCastleQuestSecondPlan(secondSlot, previousTimeRemaining, castleQuestTime, Expired: true, removed);
                return true;
            }

            castleQuestTime--;
            plan = new LegacyCastleQuestSecondPlan(secondSlot, previousTimeRemaining, castleQuestTime, Expired: false, RemovedNpcs: []);
            return true;
        }
    }

    /// <summary>Assigns connected players to one legacy party for PvE experience.</summary>
    public bool TrySetParty(int leaderConnectionId, IReadOnlyList<int> memberConnectionIds)
    {
        ArgumentNullException.ThrowIfNull(memberConnectionIds);
        lock (gate)
        {
            if (!participants.ContainsKey(leaderConnectionId) || memberConnectionIds.Count is < 1 or > 12)
                return false;
            if (memberConnectionIds.Distinct().Count() != memberConnectionIds.Count || !memberConnectionIds.Contains(leaderConnectionId))
                return false;
            if (memberConnectionIds.Any(connectionId => !participants.ContainsKey(connectionId)))
                return false;

            foreach (var connectionId in partyLeaders.Keys.Where(connectionId => memberConnectionIds.Contains(connectionId)).ToArray())
                partyLeaders.Remove(connectionId);
            foreach (var connectionId in memberConnectionIds)
                partyLeaders[connectionId] = leaderConnectionId;
            return true;
        }
    }

    public PartyInviteCheckResult TryPreparePartyInvite(int leaderConnectionId, int targetConnectionId, out PartyInvitePlan? plan)
    {
        lock (gate)
        {
            plan = null;
            if (!participants.TryGetValue(leaderConnectionId, out var leader) || !participants.TryGetValue(targetConnectionId, out var target) || leaderConnectionId == targetConnectionId)
                return PartyInviteCheckResult.ParticipantNotFound;
            if (partyLeaders.TryGetValue(leaderConnectionId, out var currentLeader) && currentLeader != leaderConnectionId)
                return PartyInviteCheckResult.SourceIsPartyMember;
            if (partyLeaders.ContainsKey(targetConnectionId))
                return PartyInviteCheckResult.TargetAlreadyInParty;
            if (GetPartyMemberIdsLocked(leaderConnectionId).Count >= 12)
                return PartyInviteCheckResult.PartyFull;
            if (!ArePartyLevelsCompatible(leader, target))
                return PartyInviteCheckResult.LevelLimit;
            // 7.69 drops the request silently (no LastReqParty, no SendReqParty) from inside the BR arena.
            if (LegacyBattleRoyaleArenaRules.BlocksParty(combatWorldState, leader.PositionX, leader.PositionY))
                return PartyInviteCheckResult.BattleRoyaleArenaBlocked;

            pendingPartyLeaders[targetConnectionId] = leaderConnectionId;
            plan = new PartyInvitePlan(leaderConnectionId, targetConnectionId, CreatePartyMemberSnapshot(leader), CreatePartyMemberSnapshot(target));
            return PartyInviteCheckResult.Accepted;
        }
    }

    public PartyAcceptResult TryAcceptParty(int targetConnectionId, int leaderConnectionId, string leaderName, out PartyFormationPlan? plan)
    {
        lock (gate)
        {
            plan = null;
            if (!participants.TryGetValue(targetConnectionId, out var target) || !participants.TryGetValue(leaderConnectionId, out var leader))
                return PartyAcceptResult.ParticipantNotFound;
            if (!pendingPartyLeaders.TryGetValue(targetConnectionId, out var pendingLeader) || pendingLeader != leaderConnectionId)
                return PartyAcceptResult.NoPendingInvite;
            pendingPartyLeaders.Remove(targetConnectionId);
            if (!string.Equals(ReadMobName(leader.Mob), leaderName, StringComparison.Ordinal))
                return PartyAcceptResult.NameMismatch;
            if (partyLeaders.ContainsKey(targetConnectionId))
                return PartyAcceptResult.TargetAlreadyInParty;
            if (partyLeaders.TryGetValue(leaderConnectionId, out var currentLeader) && currentLeader != leaderConnectionId)
                return PartyAcceptResult.SourceIsPartyMember;
            if (!ArePartyLevelsCompatible(leader, target))
                return PartyAcceptResult.LevelLimit;
            // 7.69 ignores the acceptance silently while the accepting player is inside the BR arena.
            if (LegacyBattleRoyaleArenaRules.BlocksParty(combatWorldState, target.PositionX, target.PositionY))
                return PartyAcceptResult.BattleRoyaleArenaBlocked;

            var members = GetPartyMemberIdsLocked(leaderConnectionId);
            if (members.Count >= 12)
                return PartyAcceptResult.PartyFull;
            members.Add(targetConnectionId);
            foreach (var member in members)
                partyLeaders[member] = leaderConnectionId;
            plan = new PartyFormationPlan(leaderConnectionId, members.Select(id => CreatePartyMemberSnapshot(participants[id])).ToArray());
            return PartyAcceptResult.Accepted;
        }
    }

    public bool TryRemovePartyMember(int connectionId, int requestedTargetConnectionId, out PartyRemovalPlan? plan)
    {
        lock (gate)
        {
            plan = null;
            if (!partyLeaders.TryGetValue(connectionId, out var leaderConnectionId))
                return false;
            var members = GetPartyMemberIdsLocked(leaderConnectionId);
            var target = requestedTargetConnectionId > 0 && members.Contains(requestedTargetConnectionId) ? requestedTargetConnectionId : connectionId;
            var disbanded = target == leaderConnectionId;
            if (disbanded)
            {
                foreach (var member in members)
                    partyLeaders.Remove(member);
                plan = new PartyRemovalPlan(leaderConnectionId, 0, members, true);
                return true;
            }

            partyLeaders.Remove(target);
            plan = new PartyRemovalPlan(leaderConnectionId, target, members, false);
            return true;
        }
    }

    private List<int> GetPartyMemberIdsLocked(int leaderConnectionId) =>
        partyLeaders.Where(pair => pair.Value == leaderConnectionId).Select(pair => pair.Key).ToList() is { Count: > 0 } members
            ? members
            : [leaderConnectionId];

    public IReadOnlyList<string> GetPartyMemberAccountNames(int connectionId)
    {
        lock (gate)
        {
            if (!participants.ContainsKey(connectionId))
                return Array.Empty<string>();
            var leaderConnectionId = GetPartyLeaderConnectionIdLocked(connectionId);
            return GetPartyMemberIdsLocked(leaderConnectionId)
                .Where(participants.ContainsKey)
                .Select(memberId => participants[memberId].AccountName)
                .Where(static accountName => LegacyAccountNameRules.TryNormalize(accountName, out _))
                .Select(static accountName => accountName.Trim().ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }
    }

    public string? GetParticipantAccountName(int connectionId)
    {
        lock (gate)
            return participants.TryGetValue(connectionId, out var participant) ? participant.AccountName : null;
    }

    public IReadOnlyList<string> GetAllParticipantAccountNames()
    {
        lock (gate)
            return participants.Values
                .Select(static participant => participant.AccountName)
                .Where(static accountName => LegacyAccountNameRules.TryNormalize(accountName, out _))
                .Select(static accountName => accountName.Trim().ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
    }

    private int GetPartyLeaderConnectionIdLocked(int connectionId) =>
        partyLeaders.TryGetValue(connectionId, out var leaderConnectionId) ? leaderConnectionId : connectionId;

    private static bool ArePartyLevelsCompatible(Participant leader, Participant target)
    {
        var leaderState = LegacyMobCombatState.Read(leader.Mob);
        var targetState = LegacyMobCombatState.Read(target.Mob);
        var leaderLevel = GetPartyLevel(leader.ClassMaster, leaderState.CurrentScore.Level);
        var targetLevel = GetPartyLevel(target.ClassMaster, targetState.CurrentScore.Level);
        return targetLevel >= leaderLevel - 200 && targetLevel < leaderLevel + 200 || targetLevel >= 1000 || leaderLevel >= 1000 || leader.ClassMaster == target.ClassMaster;
    }

    private static int GetPartyLevel(short classMaster, int level) => classMaster is LegacyAccountSnapshot.ClassMasterMortal or LegacyAccountSnapshot.ClassMasterArch ? level : level + LegacyExperienceMath.MaxLevel + 1;

    private static PartyMemberSnapshot CreatePartyMemberSnapshot(Participant participant)
    {
        var state = LegacyMobCombatState.Read(participant.Mob);
        return new PartyMemberSnapshot(participant.ConnectionId, ReadMobName(participant.Mob), state.CharacterClass, state.CurrentScore.Level, state.CurrentScore.MaxHp, state.CurrentScore.Hp);
    }

    private static string ReadMobName(ReadOnlySpan<byte> mob)
    {
        if (mob.Length < 16) return string.Empty;
        var length = mob[..16].IndexOf((byte)0);
        if (length < 0) length = 16;
        return Encoding.ASCII.GetString(mob[..length]);
    }

    public bool ClearParty(int connectionId)
    {
        lock (gate)
        {
            if (!participants.ContainsKey(connectionId)) return false;
            var leader = partyLeaders.TryGetValue(connectionId, out var currentLeader) ? currentLeader : connectionId;
            foreach (var member in partyLeaders.Where(pair => pair.Value == leader).Select(pair => pair.Key).ToArray())
                partyLeaders.Remove(member);
            return true;
        }
    }

    public bool TrySetExperienceHold(int connectionId, uint hold)
    {
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant)) return false;
            BinaryPrimitives.WriteUInt32LittleEndian(participant.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraHoldOffset), hold);
            return true;
        }
    }

    public bool TryGetExperienceHold(int connectionId, out uint hold)
    {
        lock (gate)
        {
            hold = 0;
            if (!participants.TryGetValue(connectionId, out var participant)) return false;
            hold = BinaryPrimitives.ReadUInt32LittleEndian(participant.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraHoldOffset));
            return true;
        }
    }

    public bool TryGetExperienceDayLog(int connectionId, out long experience, out int yearDay)
    {
        lock (gate)
        {
            experience = 0;
            yearDay = 0;
            if (!participants.TryGetValue(connectionId, out var participant)) return false;
            experience = BinaryPrimitives.ReadInt64LittleEndian(participant.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraDayLogExpOffset));
            yearDay = BinaryPrimitives.ReadInt32LittleEndian(participant.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraDayLogYearDayOffset));
            return true;
        }
    }

    public bool SetCombatEligibilityState(int connectionId, bool pkMode, bool guilty, byte mapAttribute)
    {
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant)) return false;
            participant.PkMode = pkMode;
            participant.Guilty = guilty;
            participant.MapAttribute = mapAttribute;
            participant.HasMapAttribute = true;
            return true;
        }
    }

    public bool SetCharacterState(int connectionId, int characterSlot, int guildId, int clan, int guildLevel, int coin) =>
        SetCharacterState(connectionId, characterSlot, guildId, clan, guildLevel, coin, 0, 0, ReadOnlyMemory<byte>.Empty);

    public bool SetCharacterState(int connectionId, int characterSlot, int guildId, int clan, int guildLevel, int coin, short positionX, short positionY, ReadOnlyMemory<byte> mob)
        => SetCharacterState(connectionId, characterSlot, guildId, clan, guildLevel, coin, positionX, positionY, mob, LegacyAccountSnapshot.ClassMasterMortal);

    public bool SetCharacterState(int connectionId, int characterSlot, int guildId, int clan, int guildLevel, int coin, short positionX, short positionY, ReadOnlyMemory<byte> mob, short classMaster)
        => SetCharacterState(connectionId, characterSlot, guildId, clan, guildLevel, coin, positionX, positionY, mob, classMaster, ReadOnlyMemory<byte>.Empty);

    public bool SetCharacterState(int connectionId, int characterSlot, int guildId, int clan, int guildLevel, int coin, short positionX, short positionY, ReadOnlyMemory<byte> mob, short classMaster, ReadOnlyMemory<byte> affect)
        => SetCharacterState(connectionId, characterSlot, guildId, clan, guildLevel, coin, positionX, positionY, mob, classMaster, affect, ReadOnlyMemory<byte>.Empty);

    public bool SetCharacterState(int connectionId, int characterSlot, int guildId, int clan, int guildLevel, int coin, short positionX, short positionY, ReadOnlyMemory<byte> mob, short classMaster, ReadOnlyMemory<byte> affect, ReadOnlyMemory<byte> mobExtra, int? sourceMobMagic = null)
        => SetCharacterState(connectionId, characterSlot, guildId, clan, guildLevel, coin, positionX, positionY, mob, classMaster, affect, mobExtra, clientEquipment: null, sourceMobMagic: sourceMobMagic);

    /// <summary>
    /// Registers the legacy MOB plus the independent 7.69 eighteen-slot equipment state.
    /// The latter is copied into the session and is never appended to the W2PP MOB blob.
    /// </summary>
    public bool SetCharacterState(
        int connectionId,
        int characterSlot,
        int guildId,
        int clan,
        int guildLevel,
        int coin,
        short positionX,
        short positionY,
        ReadOnlyMemory<byte> mob,
        short classMaster,
        ReadOnlyMemory<byte> affect,
        ReadOnlyMemory<byte> mobExtra,
        IReadOnlyList<LegacyItem>? clientEquipment,
        ReadOnlyMemory<byte> pursuitBaseResistances = default,
        int? sourceMobMagic = null)
    {
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant)) return false;
            participant.CharacterSlot = characterSlot;
            participant.GuildId = guildId;
            participant.Clan = clan;
            participant.GuildLevel = guildLevel;
            participant.GuildDisabled = false;
            participant.Coin = coin;
            participant.PositionX = positionX;
            participant.PositionY = positionY;
            participant.Mob = mob.ToArray();
            participant.SourceMobMagic = sourceMobMagic ??
                (participant.Mob.Length >= LegacyAccountSnapshot.MobMagicOffset + sizeof(int)
                    ? BinaryPrimitives.ReadInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobMagicOffset))
                    : null);
            participant.ClientEquipment = CreateClientEquipment(participant.Mob, clientEquipment);
            participant.HasPersistedClientEquipment = clientEquipment is not null;
            participant.RuntimeParryAdjustment = 0;
            participant.ClassMaster = classMaster;
            participant.ExperienceSegment = 0;
            participant.CriticalProgress = 0;
            participant.Affect = affect.Length == LegacyAccountSnapshot.AffectStride ? affect.ToArray() : new byte[LegacyAccountSnapshot.AffectStride];
            participant.PursuitBaseResistances = pursuitBaseResistances.Length >= 4
                ? pursuitBaseResistances[..4].ToArray()
                : HasLegacyAffectType(participant.Affect, type: 3)
                    ? CapturePlayerPursuitBaseResistances(participant.Mob, participant.Affect)
                    : null;
            participant.MobExtra = mobExtra.Length == LegacyAccountSnapshot.MobExtraStride ? mobExtra.ToArray() : new byte[LegacyAccountSnapshot.MobExtraStride];
            participant.Guilty = ReadLegacyGuilty(participant.Mob) > 0;
            participant.RequestedHp = participant.Mob.Length >= LegacyAccountSnapshot.MobCurrentScoreOffset + 28
                ? BinaryPrimitives.ReadInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24))
                : 0;
            participant.RequestedMana = participant.Mob.Length >= LegacyAccountSnapshot.MobCurrentMpOffset + sizeof(int)
                ? System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentMpOffset))
                : 0;
            return true;
        }
    }

    /// <summary>Resolves the live grid position used by TMSrv when a character enters the world.</summary>
    public (short X, short Y) ResolveCharacterLoginPosition(
        int connectionId,
        ReadOnlySpan<byte> mob,
        short classMaster,
        int cityRandomX = -1,
        int cityRandomY = -1,
        int newbieRandomX = -1,
        int newbieRandomY = -1)
    {
        if (mob.Length < LegacyAccountSnapshot.CharacterStride)
            throw new ArgumentException($"A full {LegacyAccountSnapshot.CharacterStride}-byte STRUCT_MOB is required.", nameof(mob));

        var state = LegacyMobCombatState.Read(mob);
        var guildId = BinaryPrimitives.ReadUInt16LittleEndian(mob.Slice(LegacyAccountSnapshot.MobGuildOffset, sizeof(ushort)));
        lock (gate)
        {
            return GetLegacyPosition(
                connectionId,
                guildId,
                classMaster,
                state,
                cityRandomX,
                cityRandomY,
                newbieRandomX,
                newbieRandomY,
                // TMSrv's character-entry branch (ProcessDBMessage.cpp) sends
                // mortal newcomers to Armia around (2100,2100). The (2112,2042)
                // point belongs to DoRecall, not the initial world entry.
                newbieOriginX: 2100,
                newbieOriginY: 2100);
        }
    }

    public bool SetDonateBalance(int connectionId, int donate)
    {
        if (donate < 0)
            return false;
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant))
                return false;
            participant.Donate = donate;
            return true;
        }
    }

    public bool TryGetDonateBalance(int connectionId, out int donate)
    {
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant))
            {
                donate = 0;
                return false;
            }
            donate = participant.Donate;
            return true;
        }
    }

    /// <summary>
    /// Executes the legacy DonateShop purchase atomically against the connected
    /// participant. The reference server does not decrement the catalog stock
    /// here; stock is display data loaded into the 3x5x15 matrix.
    /// </summary>
    public LegacyDonatePurchaseResult TryPurchaseDonateItem(int connectionId, DonatePurchaseRequest request, out LegacyDonatePurchaseOutcome? outcome)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyDonatePurchaseResult.ParticipantNotFound;
            if (donateShopCatalog is null)
                return LegacyDonatePurchaseResult.CatalogUnavailable;
            if (!donateShopCatalog.TryGet(request.Store, request.Page, request.ItemPosition, out var entry))
                return LegacyDonatePurchaseResult.InvalidCoordinates;
            if (request.Quantity is <= 0 or > 120)
                return LegacyDonatePurchaseResult.InvalidQuantity;
            if (entry.ItemIndex <= 0 || entry.ItemIndex > short.MaxValue)
                return LegacyDonatePurchaseResult.InvalidItem;
            if (entry.Price < 0)
                return LegacyDonatePurchaseResult.InvalidPrice;

            var totalPrice = (long)entry.Price * request.Quantity;
            if (totalPrice > int.MaxValue || participant.Donate < totalPrice)
                return LegacyDonatePurchaseResult.InsufficientDonate;
            if (participant.Mob.Length < LegacyAccountSnapshot.MobCarryOffset + (LegacyAccountSnapshot.MobCarryCount * LegacyItem.SizeInBytes))
                return LegacyDonatePurchaseResult.InventoryUnavailable;

            var grouped = IsDonateGroupedItem(entry.ItemIndex);
            var requiredSlots = grouped ? 1 : request.Quantity;
            var capacity = GetLegacyMaxCarry(participant);

            var freeSlots = new List<int>(requiredSlots);
            for (var slot = 0; slot < Math.Min(capacity, LegacyAccountSnapshot.MobCarryCount); slot++)
            {
                if (ReadCarryItem(participant, slot).Index == 0)
                    freeSlots.Add(slot);
            }
            if (freeSlots.Count < requiredSlots)
                return LegacyDonatePurchaseResult.InventoryFull;

            var previousMob = participant.Mob.ToArray();
            var previousDonate = participant.Donate;
            var item = grouped
                ? new LegacyItem((short)entry.ItemIndex, 61, checked((byte)request.Quantity), 0, 0, 0, 0)
                : new LegacyItem((short)entry.ItemIndex, 0, 0, 0, 0, 0, 0);
            var drops = new List<LegacyItemDrop>(requiredSlots);
            for (var index = 0; index < requiredSlots; index++)
            {
                var slot = freeSlots[index];
                item.Write(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (slot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
                drops.Add(new LegacyItemDrop(connectionId, slot, item));
            }

            participant.Donate -= checked((int)totalPrice);
            outcome = new LegacyDonatePurchaseOutcome(
                request.Store,
                request.Page,
                request.ItemPosition,
                entry.ItemIndex,
                request.Quantity,
                checked((int)totalPrice),
                participant.Donate,
                drops,
                participant.Mob.ToArray(),
                previousDonate,
                previousMob);
            return LegacyDonatePurchaseResult.Accepted;
        }
    }

    /// <summary>
    /// Restores the participant state captured by a Donate purchase when its
    /// durable balance could not be committed. The listener calls this before
    /// sending any item frames, so a failed persistence operation cannot grant
    /// items or consume the in-memory balance.
    /// </summary>
    public bool TryRollbackDonatePurchase(int connectionId, LegacyDonatePurchaseOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant) || participant.Mob.Length != outcome.PreviousMob.Length)
                return false;
            outcome.PreviousMob.AsSpan().CopyTo(participant.Mob);
            participant.Donate = outcome.PreviousDonate;
            return true;
        }
    }

    private static LegacyItem ReadCarryItem(Participant participant, int slot) =>
        LegacyItem.Read(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (slot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));

    private static bool IsSupportedLiveItemPlace(int place) => place is LegacyItemPlace.Equip or LegacyItemPlace.Carry;

    private static bool IsValidTradeSlot(Participant participant, int place, int slot, bool isSource)
    {
        if (place == LegacyItemPlace.Carry)
        {
            var tradeCarryCount = LegacyAccountSnapshot.MobCarryCount - 4;
            if (slot < 0 || slot >= tradeCarryCount) return false;
            return isSource || slot < GetLegacyMaxCarry(participant);
        }

        // The target 7.69 handler bounds equipment positions with
        // `position < MAX_EQUIP - 1`: slot 17 is not addressable. Only enable
        // slot 16 when this session has its independent 18-slot state persisted.
        // Slot 0 remains protected as a source (face); slot 15 is rejected by
        // LegacyEquipmentRules.
        var equipmentCount = participant.HasPersistedClientEquipment
            ? participant.ClientEquipment.Length
            : LegacyCharacterSelection.EquipmentCount;
        var equipmentLimit = equipmentCount - 1;
        return isSource ? slot > 0 && slot < equipmentLimit
                        : slot >= 0 && slot < equipmentLimit;
    }

    private static LegacyItem ReadLiveItem(Participant participant, int place, int slot) =>
        place == LegacyItemPlace.Carry ? ReadCarryItem(participant, slot) : participant.ClientEquipment[slot];

    private static void WriteLiveItem(Participant participant, int place, int slot, LegacyItem item)
    {
        if (place == LegacyItemPlace.Carry)
        {
            item.Write(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (slot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
            return;
        }

        participant.ClientEquipment[slot] = item;
        participant.RuntimeParryAdjustment = 0;
        if (slot < LegacyCharacterSelection.EquipmentCount)
            item.Write(participant.Mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (slot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
    }

    private static LegacyItem[] ReadEquipment(Participant participant)
        => participant.ClientEquipment.ToArray();

    private static LegacyItem[] CreateClientEquipment(ReadOnlySpan<byte> mob, IReadOnlyList<LegacyItem>? clientEquipment)
    {
        if (clientEquipment is not null)
        {
            if (clientEquipment.Count != CharacterMobV769.EquipmentCount)
                throw new ArgumentException($"Client equipment requires exactly {CharacterMobV769.EquipmentCount} entries.", nameof(clientEquipment));
            return clientEquipment.ToArray();
        }

        var equipment = new LegacyItem[CharacterMobV769.EquipmentCount];
        if (mob.Length >= LegacyAccountSnapshot.MobEquipmentOffset + (LegacyCharacterSelection.EquipmentCount * LegacyItem.SizeInBytes))
        {
            for (var slot = 0; slot < LegacyCharacterSelection.EquipmentCount; slot++)
                equipment[slot] = LegacyItem.Read(mob.Slice(LegacyAccountSnapshot.MobEquipmentOffset + (slot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        }
        return equipment;
    }

    private static void SyncClientEquipmentFromLegacyMob(Participant participant)
    {
        if (participant.Mob.Length < LegacyAccountSnapshot.MobEquipmentOffset + (LegacyCharacterSelection.EquipmentCount * LegacyItem.SizeInBytes))
            return;
        for (var slot = 0; slot < LegacyCharacterSelection.EquipmentCount; slot++)
            participant.ClientEquipment[slot] = LegacyItem.Read(participant.Mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (slot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
    }

    private static int GetLegacyMaxCarry(Participant participant)
    {
        var capacity = LegacyBaseCarryCapacity;
        if (ReadCarryItem(participant, LegacyFirstCarryExpansionSlot).Index == LegacyCarryExpansionItemIndex)
            capacity += LegacyCarryExpansionSize;
        if (ReadCarryItem(participant, LegacySecondCarryExpansionSlot).Index == LegacyCarryExpansionItemIndex)
            capacity += LegacyCarryExpansionSize;
        return capacity;
    }

    private static void WriteCarryItem(Participant participant, int slot, LegacyItem item) =>
        item.Write(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (slot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));

    private static bool IsDonateGroupedItem(int itemIndex) => itemIndex is
        3314 or 4140 or 3312 or 3343 or 3336 or 3310 or 3311 or
        3407 or 3408 or 3409 or 3410 or 3411 or 3412 or 3413 or
        3414 or 3415 or 3416 or 3417 or 2426 or 3330 or 777;

    /// <summary>
    /// Applies the legacy Vol 190 class-reset item operation. Cargo is not in
    /// the current in-memory snapshot, so this intentionally accepts carry
    /// source/destination only until the cargo wire is ported.
    /// </summary>
    public LegacyUseItemResult TryApplyClassReset(int connectionId, UseItemRequest request, out LegacyUseItemOutcome? outcome, Func<int, int>? roll = null)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyUseItemResult.ParticipantNotFound;
            if (itemData is null)
                return LegacyUseItemResult.ItemDataUnavailable;
            if (participant.Mob.Length < LegacyAccountSnapshot.MobCurrentScoreOffset + 28)
                return LegacyUseItemResult.InvalidPlacement;
            if (BinaryPrimitives.ReadInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24)) <= 0)
                return LegacyUseItemResult.AttackerNotAlive;
            if (request.SourceType != 1 || request.DestinationType != 1 || request.SourceSlot is < 0 or >= LegacyAccountSnapshot.MobCarryCount || request.DestinationSlot is < 0 or >= LegacyAccountSnapshot.MobCarryCount || request.SourceSlot == request.DestinationSlot)
                return LegacyUseItemResult.InvalidPlacement;

            var sourceOffset = LegacyAccountSnapshot.MobCarryOffset + (request.SourceSlot * LegacyItem.SizeInBytes);
            var destinationOffset = LegacyAccountSnapshot.MobCarryOffset + (request.DestinationSlot * LegacyItem.SizeInBytes);
            var source = LegacyItem.Read(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            var destination = LegacyItem.Read(participant.Mob.AsSpan(destinationOffset, LegacyItem.SizeInBytes));
            if (source.Index == 0)
                return LegacyUseItemResult.SourceEmpty;
            if (destination.Index == 0)
                return LegacyUseItemResult.DestinationEmpty;
            if (itemData.GetItemAbility(source, LegacyItemEffect.Volatile) != 190)
                return LegacyUseItemResult.UnsupportedItem;

            var destinationDefinition = itemData[destination.Index];
            if (destinationDefinition is null)
                return LegacyUseItemResult.InvalidDestination;

            var sanctuary = itemData.GetItemSanctuary(destination);
            if (sanctuary > 9)
                return LegacyUseItemResult.InvalidDestination;
            var mobType = itemData.GetItemAbility(destination, LegacyItemEffect.MobType);
            if (mobType is not (0 or 2))
                return LegacyUseItemResult.InvalidDestination;

            var replacementLevel = source.Index is >= 4016 and <= 4020
                ? source.Index - 4015
                : source.Index - 4020;
            if (itemData.GetItemAbility(destination, LegacyItemEffect.ItemLevel) != replacementLevel)
                return LegacyUseItemResult.ItemLevelMismatch;

            var refinedDestination = LegacyItemRefinementMath.Apply(destination, destinationDefinition, itemData, roll);
            var updatedSource = GetLegacyItemAmount(source) > 1
                ? SetLegacyItemAmount(source, GetLegacyItemAmount(source) - 1)
                : default;
            updatedSource.Write(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            refinedDestination.Write(participant.Mob.AsSpan(destinationOffset, LegacyItem.SizeInBytes));
            outcome = new LegacyUseItemOutcome(request.SourceSlot, updatedSource, request.DestinationSlot, refinedDestination);
            return LegacyUseItemResult.Accepted;
        }
    }

    /// <summary>
    /// Applies the carry-only Vol 4/5 refinement branches from the legacy
    /// _MSG_UseItem handler. The random roll is injectable so success and
    /// failure preserve the exact item mutation in tests.
    /// </summary>
    public LegacyUseItemResult TryApplyRefinement(int connectionId, UseItemRequest request, out LegacyRefinementOutcome? outcome, Func<int, int>? roll = null)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyUseItemResult.ParticipantNotFound;
            if (itemData is null)
                return LegacyUseItemResult.ItemDataUnavailable;
            if (participant.Mob.Length < LegacyAccountSnapshot.MobCurrentScoreOffset + 28)
                return LegacyUseItemResult.InvalidPlacement;
            if (BinaryPrimitives.ReadInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24)) <= 0)
                return LegacyUseItemResult.AttackerNotAlive;
            if (request.SourceType != 1 || request.DestinationType != 1 || request.SourceSlot is < 0 or >= LegacyAccountSnapshot.MobCarryCount || request.DestinationSlot is < 0 or >= LegacyAccountSnapshot.MobCarryCount || request.SourceSlot == request.DestinationSlot)
                return LegacyUseItemResult.InvalidPlacement;

            var sourceOffset = LegacyAccountSnapshot.MobCarryOffset + (request.SourceSlot * LegacyItem.SizeInBytes);
            var destinationOffset = LegacyAccountSnapshot.MobCarryOffset + (request.DestinationSlot * LegacyItem.SizeInBytes);
            var source = LegacyItem.Read(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            var destination = LegacyItem.Read(participant.Mob.AsSpan(destinationOffset, LegacyItem.SizeInBytes));
            if (source.Index == 0)
                return LegacyUseItemResult.SourceEmpty;
            if (destination.Index == 0)
                return LegacyUseItemResult.DestinationEmpty;

            var volatileValue = itemData.GetItemAbility(source, LegacyItemEffect.Volatile);
            if (volatileValue is not (4 or 5))
                return LegacyUseItemResult.UnsupportedItem;
            var destinationDefinition = itemData[destination.Index];
            if (destinationDefinition is null)
                return LegacyUseItemResult.InvalidDestination;
            if (itemData.GetItemAbility(destination, LegacyItemEffect.Volatile) != 0 || itemData.GetItemAbility(destination, LegacyItemEffect.NoSanctuary) != 0)
                return LegacyUseItemResult.InvalidDestination;

            var sanctuary = itemData.GetItemSanctuary(destination);
            var mobType = itemData.GetItemAbility(destination, LegacyItemEffect.MobType);
            var sealedItem = mobType == 5;
            var celestialItem = mobType == 3;
            var earring = volatileValue == 5 && sanctuary is >= 9 and <= 22 && request.DestinationSlot == 8 && destinationDefinition.Position == 256;
            if (!sealedItem && !celestialItem && !earring)
                return LegacyUseItemResult.InvalidDestination;
            if (sealedItem && (sanctuary >= 9 || volatileValue == 4 && sanctuary >= 6))
                return LegacyUseItemResult.InvalidDestination;
            if (celestialItem && (sanctuary >= 15 || volatileValue == 4 && sanctuary >= 6))
                return LegacyUseItemResult.InvalidDestination;

            var preparedDestination = destination;
            if (!earring && sanctuary == 0)
            {
                preparedDestination = LegacyItemRefinementMath.TryEnsureSanctuaryEffect(preparedDestination) ?? destination;
                if (preparedDestination == destination && !HasSanctuaryEffect(destination))
                    return LegacyUseItemResult.InvalidDestination;
            }

            var randomMaximum = earring ? 100 : 115;
            var randomValue = roll?.Invoke(randomMaximum) ?? Random.Shared.Next(randomMaximum);
            if (randomValue < 0 || randomValue >= randomMaximum)
                throw new ArgumentOutOfRangeException(nameof(roll), $"Injected roll must be in [0, {randomMaximum}).");
            if (!earring && randomValue > 100)
                randomValue -= 15;
            var sanctuaryIndex = sanctuary switch
            {
                10 => 10,
                12 => 11,
                15 => 12,
                18 => 13,
                22 => 14,
                _ => sanctuary,
            };
            var chance = earring ? 15 : new[] { 100, 100, 100, 100, 100, 100, 100, 100, 70, 50, 40, 40, 40, 10, 5 }[sanctuaryIndex];
            var nextSanctuary = sanctuaryIndex + 1;
            var succeeded = randomValue <= chance;
            var refinedDestination = earring
                ? (succeeded ? LegacyItemRefinementMath.SetSanctuary(preparedDestination, nextSanctuary) : default)
                : celestialItem
                    ? (succeeded ? LegacyItemRefinementMath.SetSanctuary(preparedDestination, nextSanctuary) : LegacyItemRefinementMath.SetSanctuary(preparedDestination, 0))
                    : (succeeded ? LegacyItemRefinementMath.SetSanctuary(preparedDestination, nextSanctuary) : default);
            var updatedSource = GetLegacyItemAmount(source) > 1
                ? SetLegacyItemAmount(source, GetLegacyItemAmount(source) - 1)
                : default;
            updatedSource.Write(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            refinedDestination.Write(participant.Mob.AsSpan(destinationOffset, LegacyItem.SizeInBytes));
            outcome = new LegacyRefinementOutcome(request.SourceSlot, updatedSource, request.DestinationSlot, refinedDestination, succeeded);
            return LegacyUseItemResult.Accepted;
        }
    }

    /// <summary>Applies the legacy Vol 9 Adamantita/Beril legendary upgrade.</summary>
    public LegacyUseItemResult TryApplyLegendaryUpgrade(int connectionId, UseItemRequest request, out LegacyLegendaryUpgradeOutcome? outcome, Func<int, int>? roll = null)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyUseItemResult.ParticipantNotFound;
            if (itemData is null)
                return LegacyUseItemResult.ItemDataUnavailable;
            if (participant.Mob.Length < LegacyAccountSnapshot.MobCurrentScoreOffset + 28)
                return LegacyUseItemResult.InvalidPlacement;
            if (BinaryPrimitives.ReadInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24)) <= 0)
                return LegacyUseItemResult.AttackerNotAlive;
            if (request.SourceType != 1 || request.DestinationType != 1 || request.SourceSlot is < 0 or >= LegacyAccountSnapshot.MobCarryCount || request.DestinationSlot is < 0 or >= LegacyAccountSnapshot.MobCarryCount || request.SourceSlot == request.DestinationSlot)
                return LegacyUseItemResult.InvalidPlacement;

            var sourceOffset = LegacyAccountSnapshot.MobCarryOffset + (request.SourceSlot * LegacyItem.SizeInBytes);
            var destinationOffset = LegacyAccountSnapshot.MobCarryOffset + (request.DestinationSlot * LegacyItem.SizeInBytes);
            var source = LegacyItem.Read(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            var destination = LegacyItem.Read(participant.Mob.AsSpan(destinationOffset, LegacyItem.SizeInBytes));
            if (source.Index == 0)
                return LegacyUseItemResult.SourceEmpty;
            if (destination.Index == 0)
                return LegacyUseItemResult.DestinationEmpty;
            if (itemData.GetItemAbility(source, LegacyItemEffect.Volatile) != 9)
                return LegacyUseItemResult.UnsupportedItem;

            var catalystType = source.Index - 575;
            if (catalystType is < 0 or >= 4)
                return LegacyUseItemResult.UnsupportedItem;
            var destinationDefinition = itemData[destination.Index];
            if (destinationDefinition is null)
                return LegacyUseItemResult.InvalidDestination;
            var uniqueType = destinationDefinition.Unique switch
            {
                5 or 14 or 24 or 34 => 0,
                6 or 15 or 25 or 35 => 1,
                7 or 16 or 26 or 36 => 2,
                8 or 10 or 17 or 20 or 27 or 30 or 37 or 40 => 3,
                _ => -1,
            };
            if (uniqueType != catalystType || destinationDefinition.Grade is <= 0 or >= 4 || destinationDefinition.Extra <= 0)
                return LegacyUseItemResult.InvalidDestination;

            var randomValue = roll?.Invoke(100) ?? Random.Shared.Next(100);
            if (randomValue < 0 || randomValue >= 100)
                throw new ArgumentOutOfRangeException(nameof(roll), "Injected roll must be in [0, 100).");
            var succeeded = randomValue <= 50;
            var upgradedDestination = succeeded ? destination with { Index = destinationDefinition.Extra } : destination;
            var updatedSource = GetLegacyItemAmount(source) > 1
                ? SetLegacyItemAmount(source, GetLegacyItemAmount(source) - 1)
                : default;
            updatedSource.Write(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            upgradedDestination.Write(participant.Mob.AsSpan(destinationOffset, LegacyItem.SizeInBytes));
            outcome = new LegacyLegendaryUpgradeOutcome(request.SourceSlot, updatedSource, request.DestinationSlot, upgradedDestination, succeeded);
            return LegacyUseItemResult.Accepted;
        }
    }

    /// <summary>Applies the 7.69 item 5504 Mãos Sangrentas acquisition.</summary>
    public LegacyUseItemResult TryApplyBloodHandsBook(int connectionId, UseItemRequest request, out LegacyBloodHandsBookOutcome? outcome)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyUseItemResult.ParticipantNotFound;

            if (participant.Mob.Length < LegacyAccountSnapshot.CharacterStride ||
                participant.MobExtra.Length < LegacyAccountSnapshot.MobExtraStride)
                return LegacyUseItemResult.InvalidPlacement;
            if (BinaryPrimitives.ReadInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24)) <= 0)
                return LegacyUseItemResult.AttackerNotAlive;
            if (request.SourceType != 1 || request.SourceSlot is < 0 or >= LegacyAccountSnapshot.MobCarryCount)
                return LegacyUseItemResult.InvalidPlacement;

            var sourceOffset = LegacyAccountSnapshot.MobCarryOffset + (request.SourceSlot * LegacyItem.SizeInBytes);
            var source = LegacyItem.Read(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            if (source.Index == 0)
                return LegacyUseItemResult.SourceEmpty;
            if (source.Index != 5504)
                return LegacyUseItemResult.UnsupportedItem;

            // Keep the source item available to the wire handler on every rejected 5504 use.
            outcome = new LegacyBloodHandsBookOutcome(request.SourceSlot, source, participant.Mob.ToArray(), participant.MobExtra.ToArray());
            var baseScore = LegacyScore.Read(participant.Mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset, LegacyScore.SizeInBytes));
            if (baseScore.Level <= 199)
                return LegacyUseItemResult.LevelRequirementNotMet;

            var classMaster = BinaryPrimitives.ReadInt16LittleEndian(
                participant.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset));
            if (classMaster is LegacyAccountSnapshot.ClassMasterMortal or LegacyAccountSnapshot.ClassMasterArch ||
                participant.Mob[LegacyAccountSnapshot.MobClassOffset] != 0)
                return LegacyUseItemResult.ClassRestricted;

            var secondaryLearnedSkill = BinaryPrimitives.ReadUInt32LittleEndian(
                participant.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset));
            // The 7.69 5504 handler checks Extra bits 0 and 8 here. Bit 1 is
            // separately consumed by the existing Divine Blessing combat gate.
            if ((secondaryLearnedSkill & ((1u << 0) | (1u << 8))) != 0)
                return LegacyUseItemResult.CompetingSkillAlreadyLearned;

            var learnedSkill = BinaryPrimitives.ReadUInt32LittleEndian(
                participant.Mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset));
            if ((learnedSkill & (1u << 15)) == 0)
                return LegacyUseItemResult.RequiredSkillNotLearned;
            if ((secondaryLearnedSkill & LegacyMobExtra.BloodHandsLearnedBit) != 0)
                return LegacyUseItemResult.AlreadyCompleted;

            if (!LegacyMobExtra.TrySetBloodHandsLearnedBit(participant.MobExtra, enabled: true))
                return LegacyUseItemResult.InvalidPlacement;

            var updatedSource = GetLegacyItemAmount(source) > 1
                ? SetLegacyItemAmount(source, GetLegacyItemAmount(source) - 1)
                : default;
            updatedSource.Write(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            outcome = new LegacyBloodHandsBookOutcome(
                request.SourceSlot,
                updatedSource,
                participant.Mob.ToArray(),
                participant.MobExtra.ToArray());
            return LegacyUseItemResult.Accepted;
        }
    }

    /// <summary>Applies the 7.69 item 5508 Espelho Mágico acquisition.</summary>
    public LegacyUseItemResult TryApplyMagicMirrorBook(int connectionId, UseItemRequest request, out LegacyMagicMirrorBookOutcome? outcome)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyUseItemResult.ParticipantNotFound;

            if (participant.Mob.Length < LegacyAccountSnapshot.CharacterStride ||
                participant.MobExtra.Length < LegacyAccountSnapshot.MobExtraStride)
                return LegacyUseItemResult.InvalidPlacement;
            if (BinaryPrimitives.ReadInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24)) <= 0)
                return LegacyUseItemResult.AttackerNotAlive;
            if (request.SourceType != 1 || request.SourceSlot is < 0 or >= LegacyAccountSnapshot.MobCarryCount)
                return LegacyUseItemResult.InvalidPlacement;

            var sourceOffset = LegacyAccountSnapshot.MobCarryOffset + (request.SourceSlot * LegacyItem.SizeInBytes);
            var source = LegacyItem.Read(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            if (source.Index == 0)
                return LegacyUseItemResult.SourceEmpty;
            if (source.Index != 5508)
                return LegacyUseItemResult.UnsupportedItem;

            // Keep the source item available to the wire handler on every rejected 5508 use.
            outcome = new LegacyMagicMirrorBookOutcome(request.SourceSlot, source, participant.Mob.ToArray(), participant.MobExtra.ToArray());
            var baseScore = LegacyScore.Read(participant.Mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset, LegacyScore.SizeInBytes));
            if (baseScore.Level <= 199)
                return LegacyUseItemResult.LevelRequirementNotMet;

            var classMaster = BinaryPrimitives.ReadInt16LittleEndian(
                participant.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset));
            if (classMaster is LegacyAccountSnapshot.ClassMasterMortal or LegacyAccountSnapshot.ClassMasterArch ||
                participant.Mob[LegacyAccountSnapshot.MobClassOffset] != 0)
                return LegacyUseItemResult.ClassRestricted;

            var secondaryLearnedSkill = BinaryPrimitives.ReadUInt32LittleEndian(
                participant.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset));
            // TMProject 7.69 excludes the bit-0 and bit-4 books here; bit 1 is a separate Divine Blessing consumer.
            if ((secondaryLearnedSkill & ((1u << 0) | (1u << 4))) != 0)
                return LegacyUseItemResult.CompetingSkillAlreadyLearned;

            var learnedSkill = BinaryPrimitives.ReadUInt32LittleEndian(
                participant.Mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset));
            if ((learnedSkill & (1u << 23)) == 0)
                return LegacyUseItemResult.RequiredSkillNotLearned;
            if ((secondaryLearnedSkill & LegacyMobExtra.MagicMirrorLearnedBit) != 0)
                return LegacyUseItemResult.AlreadyCompleted;

            if (!LegacyMobExtra.TrySetMagicMirrorLearnedBit(participant.MobExtra, enabled: true))
                return LegacyUseItemResult.InvalidPlacement;

            var updatedSource = GetLegacyItemAmount(source) > 1
                ? SetLegacyItemAmount(source, GetLegacyItemAmount(source) - 1)
                : default;
            updatedSource.Write(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            outcome = new LegacyMagicMirrorBookOutcome(
                request.SourceSlot,
                updatedSource,
                participant.Mob.ToArray(),
                participant.MobExtra.ToArray());
            return LegacyUseItemResult.Accepted;
        }
    }

    /// <summary>Applies the 7.69 item 5500 Divine Protection book acquisition.</summary>
    public LegacyUseItemResult TryApplyDivineProtectionBook(int connectionId, UseItemRequest request, out LegacyDivineProtectionBookOutcome? outcome)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyUseItemResult.ParticipantNotFound;

            if (participant.Mob.Length < LegacyAccountSnapshot.CharacterStride ||
                participant.MobExtra.Length < LegacyAccountSnapshot.MobExtraStride)
                return LegacyUseItemResult.InvalidPlacement;
            if (BinaryPrimitives.ReadInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24)) <= 0)
                return LegacyUseItemResult.AttackerNotAlive;
            if (request.SourceType != 1 || request.SourceSlot is < 0 or >= LegacyAccountSnapshot.MobCarryCount)
                return LegacyUseItemResult.InvalidPlacement;

            var sourceOffset = LegacyAccountSnapshot.MobCarryOffset + (request.SourceSlot * LegacyItem.SizeInBytes);
            var source = LegacyItem.Read(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            if (source.Index == 0)
                return LegacyUseItemResult.SourceEmpty;
            if (source.Index != 5500)
                return LegacyUseItemResult.UnsupportedItem;

            // TMProject 7.69 writes Extra bit 0 here; Divine Blessing's combat consumer separately uses bit 1.
            outcome = new LegacyDivineProtectionBookOutcome(request.SourceSlot, source, participant.Mob.ToArray(), participant.MobExtra.ToArray());
            var baseScore = LegacyScore.Read(participant.Mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset, LegacyScore.SizeInBytes));
            if (baseScore.Level <= 199)
                return LegacyUseItemResult.LevelRequirementNotMet;

            var classMaster = BinaryPrimitives.ReadInt16LittleEndian(
                participant.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset));
            if (classMaster is LegacyAccountSnapshot.ClassMasterMortal or LegacyAccountSnapshot.ClassMasterArch ||
                participant.Mob[LegacyAccountSnapshot.MobClassOffset] != 0)
                return LegacyUseItemResult.ClassRestricted;

            var secondaryLearnedSkill = BinaryPrimitives.ReadUInt32LittleEndian(
                participant.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset));
            if ((secondaryLearnedSkill & ((1u << 4) | (1u << 8))) != 0)
                return LegacyUseItemResult.CompetingSkillAlreadyLearned;

            var learnedSkill = BinaryPrimitives.ReadUInt32LittleEndian(
                participant.Mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset));
            if ((learnedSkill & (1u << 7)) == 0)
                return LegacyUseItemResult.RequiredSkillNotLearned;
            if ((secondaryLearnedSkill & LegacyMobExtra.DivineProtectionBookLearnedBit) != 0)
                return LegacyUseItemResult.AlreadyCompleted;

            if (!LegacyMobExtra.TrySetDivineProtectionBookLearnedBit(participant.MobExtra, enabled: true))
                return LegacyUseItemResult.InvalidPlacement;

            var updatedSource = GetLegacyItemAmount(source) > 1
                ? SetLegacyItemAmount(source, GetLegacyItemAmount(source) - 1)
                : default;
            updatedSource.Write(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            outcome = new LegacyDivineProtectionBookOutcome(
                request.SourceSlot,
                updatedSource,
                participant.Mob.ToArray(),
                participant.MobExtra.ToArray());
            return LegacyUseItemResult.Accepted;
        }
    }

    /// <summary>Applies the generic 7.69 Sephira skill-book path for Volatile 31-38.</summary>
    public LegacyUseItemResult TryApplySephiraSkillBook(int connectionId, UseItemRequest request, out LegacySephiraSkillBookOutcome? outcome)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyUseItemResult.ParticipantNotFound;
            if (itemData is null)
                return LegacyUseItemResult.ItemDataUnavailable;
            if (participant.Mob.Length < LegacyAccountSnapshot.CharacterStride ||
                participant.Affect.Length != LegacyAccountSnapshot.AffectStride)
                return LegacyUseItemResult.InvalidPlacement;
            if (BinaryPrimitives.ReadInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24)) <= 0)
                return LegacyUseItemResult.AttackerNotAlive;
            if (request.SourceType != 1 || request.SourceSlot is < 0 or >= LegacyAccountSnapshot.MobCarryCount)
                return LegacyUseItemResult.InvalidPlacement;

            var sourceOffset = LegacyAccountSnapshot.MobCarryOffset + (request.SourceSlot * LegacyItem.SizeInBytes);
            var source = LegacyItem.Read(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            if (source.Index == 0)
                return LegacyUseItemResult.SourceEmpty;

            var volatileValue = itemData.GetItemAbility(source, LegacyItemEffect.Volatile);
            if (volatileValue is < 31 or > 38)
                return LegacyUseItemResult.UnsupportedItem;

            var learnedSkillBit = 1u << (volatileValue - 7);
            outcome = new LegacySephiraSkillBookOutcome(
                request.SourceSlot,
                source,
                volatileValue,
                learnedSkillBit,
                AffectApplied: false,
                participant.Mob.ToArray(),
                participant.Affect.ToArray());
            var learnedSkill = BinaryPrimitives.ReadUInt32LittleEndian(
                participant.Mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset));
            if ((learnedSkill & learnedSkillBit) != 0)
                return LegacyUseItemResult.AlreadyCompleted;

            var nextMob = participant.Mob.ToArray();
            var nextAffect = participant.Affect.ToArray();
            BinaryPrimitives.WriteUInt32LittleEndian(
                nextMob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset),
                learnedSkill | learnedSkillBit);

            // The source handler always consumes the book even when SetAffect
            // cannot install SkillData 44 (for example, a full affect list).
            var skill = skillData?[44];
            var rsv = BinaryPrimitives.ReadUInt16LittleEndian(nextMob.AsSpan(LegacyAccountSnapshot.MobRsvOffset));
            var canApplyAffect = skill is not null &&
                connectionId <= LegacyMobAiPlanner.MaximumPlayerId &&
                nextMob[LegacyAccountSnapshot.MobMerchantOffset] != 1 &&
                (skill.Aggressive == 0 || (rsv & LegacyCurrentScoreMath.BlockRsvFlag) == 0);
            var affectApplied = canApplyAffect && TryApplyLegacyAffects(nextAffect, skill, delay: 20, level: 20);

            var amount = GetLegacyItemAmount(source);
            var updatedSource = amount > 1 ? SetLegacyItemAmount(source, amount - 1) : default;
            updatedSource.Write(nextMob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));

            if (affectApplied)
            {
                LegacyCharacterLoginData projected;
                try
                {
                    projected = ProjectPlayerScoreLocked(participant, nextMob, nextAffect, itemData, preserveHpMp: true);
                }
                catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException)
                {
                    return LegacyUseItemResult.InvalidPlacement;
                }

                nextMob = projected.Mob;
                nextAffect = projected.Affect;
                if (projected.ClientEquipment is not null)
                    participant.ClientEquipment = projected.ClientEquipment.ToArray();
            }

            participant.Mob = nextMob;
            participant.Affect = nextAffect;
            outcome = new LegacySephiraSkillBookOutcome(
                request.SourceSlot,
                updatedSource,
                volatileValue,
                learnedSkillBit,
                affectApplied,
                participant.Mob.ToArray(),
                participant.Affect.ToArray());
            return LegacyUseItemResult.Accepted;
        }
    }

    /// <summary>Applies the one-time Vol 6 Orc Pill quest reward.</summary>
    public LegacyUseItemResult TryApplyOrcPill(int connectionId, UseItemRequest request, out LegacyOrcPillOutcome? outcome)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyUseItemResult.ParticipantNotFound;
            if (itemData is null)
                return LegacyUseItemResult.ItemDataUnavailable;
            if (participant.Mob.Length < LegacyAccountSnapshot.MobSkillBonusOffset + sizeof(ushort) || participant.MobExtra.Length <= LegacyAccountSnapshot.MobExtraPilulaOrcOffset)
                return LegacyUseItemResult.InvalidPlacement;
            if (BinaryPrimitives.ReadInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24)) <= 0)
                return LegacyUseItemResult.AttackerNotAlive;
            if (request.SourceType != 1 || request.SourceSlot is < 0 or >= LegacyAccountSnapshot.MobCarryCount)
                return LegacyUseItemResult.InvalidPlacement;

            var sourceOffset = LegacyAccountSnapshot.MobCarryOffset + (request.SourceSlot * LegacyItem.SizeInBytes);
            var source = LegacyItem.Read(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            if (source.Index == 0)
                return LegacyUseItemResult.SourceEmpty;
            if (itemData.GetItemAbility(source, LegacyItemEffect.Volatile) != 6)
                return LegacyUseItemResult.UnsupportedItem;
            if (participant.MobExtra[LegacyAccountSnapshot.MobExtraPilulaOrcOffset] != 0)
                return LegacyUseItemResult.AlreadyCompleted;

            var skillBonus = unchecked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobSkillBonusOffset)) + 9));
            BinaryPrimitives.WriteUInt16LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobSkillBonusOffset), skillBonus);
            participant.MobExtra[LegacyAccountSnapshot.MobExtraPilulaOrcOffset] = 1;
            var updatedSource = GetLegacyItemAmount(source) > 1
                ? SetLegacyItemAmount(source, GetLegacyItemAmount(source) - 1)
                : default;
            updatedSource.Write(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            outcome = new LegacyOrcPillOutcome(request.SourceSlot, updatedSource, skillBonus, 1, participant.Mob.ToArray(), participant.MobExtra.ToArray());
            return LegacyUseItemResult.Accepted;
        }
    }

    /// <summary>Applies the legacy Vol 7 Fairy Dust and Vol 8 Crescent Eye.</summary>
    public LegacyUseItemResult TryApplyExperienceConsumable(int connectionId, UseItemRequest request, out LegacyExperienceConsumableOutcome? outcome)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyUseItemResult.ParticipantNotFound;
            if (itemData is null)
                return LegacyUseItemResult.ItemDataUnavailable;
            if (participant.Mob.Length < LegacyAccountSnapshot.MobCurrentScoreOffset + LegacyScore.SizeInBytes || participant.MobExtra.Length < sizeof(short))
                return LegacyUseItemResult.InvalidPlacement;
            if (BinaryPrimitives.ReadInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24)) <= 0)
                return LegacyUseItemResult.AttackerNotAlive;
            if (request.SourceType != 1 || request.SourceSlot is < 0 or >= LegacyAccountSnapshot.MobCarryCount)
                return LegacyUseItemResult.InvalidPlacement;

            var sourceOffset = LegacyAccountSnapshot.MobCarryOffset + (request.SourceSlot * LegacyItem.SizeInBytes);
            var source = LegacyItem.Read(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            if (source.Index == 0)
                return LegacyUseItemResult.SourceEmpty;

            var volatileValue = itemData.GetItemAbility(source, LegacyItemEffect.Volatile);
            if (volatileValue is not (7 or 8))
                return LegacyUseItemResult.UnsupportedItem;
            var classMaster = BinaryPrimitives.ReadInt16LittleEndian(participant.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset));
            if (volatileValue == 8 && classMaster != LegacyAccountSnapshot.ClassMasterMortal)
                return LegacyUseItemResult.ClassRestricted;

            if (volatileValue == 7)
            {
                var level = LegacyScore.Read(participant.Mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset)).Level;
                BinaryPrimitives.WriteInt64LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset), LegacyLevelProgressionMath.GetNextLevelExperience(classMaster, level));
            }
            else
            {
                var experience = BinaryPrimitives.ReadInt64LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset));
                BinaryPrimitives.WriteInt64LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset), checked(experience + 2500));
            }

            var experienceSegment = participant.ExperienceSegment;
            var progression = LegacyLevelProgressionMath.Apply(participant.Mob, participant.MobExtra, ref experienceSegment);
            participant.ExperienceSegment = experienceSegment;
            var amount = GetLegacyItemAmount(source);
            var updatedSource = amount > 1 ? SetLegacyItemAmount(source, amount - 1) : default;
            updatedSource.Write(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            outcome = new LegacyExperienceConsumableOutcome(
                request.SourceSlot,
                updatedSource,
                volatileValue,
                BinaryPrimitives.ReadInt64LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset)),
                progression.Level,
                progression.Stage,
                progression.LeveledUp,
                participant.Mob.ToArray());
            return LegacyUseItemResult.Accepted;
        }
    }

    /// <summary>Applies legacy Type 33 transformation items and the Vol 10 Type 4 consumables.</summary>
    public LegacyUseItemResult TryApplyAffectConsumable(int connectionId, UseItemRequest request, out LegacyAffectConsumableOutcome? outcome)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyUseItemResult.ParticipantNotFound;
            if (itemData is null)
                return LegacyUseItemResult.ItemDataUnavailable;
            if (participant.Mob.Length < LegacyAccountSnapshot.MobCurrentScoreOffset + LegacyScore.SizeInBytes || participant.Affect.Length != LegacyAccountSnapshot.AffectStride)
                return LegacyUseItemResult.InvalidPlacement;
            if (BinaryPrimitives.ReadInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24)) <= 0)
                return LegacyUseItemResult.AttackerNotAlive;
            if (request.SourceType != 1 || request.SourceSlot is < 0 or >= LegacyAccountSnapshot.MobCarryCount)
                return LegacyUseItemResult.InvalidPlacement;

            var sourceOffset = LegacyAccountSnapshot.MobCarryOffset + (request.SourceSlot * LegacyItem.SizeInBytes);
            var source = LegacyItem.Read(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            if (source.Index == 0)
                return LegacyUseItemResult.SourceEmpty;

            if (source.Index is >= 3418 and <= 3425)
            {
                var transformationAffectSlot = -1;
                for (var index = 0; index < LegacyAccountSnapshot.AffectStride / 8; index++)
                {
                    if (participant.Affect[index * 8] == 33)
                    {
                        transformationAffectSlot = index;
                        break;
                    }
                }
                if (transformationAffectSlot < 0)
                {
                    for (var index = 0; index < LegacyAccountSnapshot.AffectStride / 8; index++)
                    {
                        if (participant.Affect[index * 8] == 0)
                        {
                            transformationAffectSlot = index;
                            break;
                        }
                    }
                }

                var affectValue = checked((byte)(source.Index - 3417));
                var affectTime = source.Index == 3425 ? 4u : 450u;
                if (transformationAffectSlot < 0 || participant.Affect[transformationAffectSlot * 8] == 33)
                {
                    outcome = new LegacyAffectConsumableOutcome(
                        request.SourceSlot,
                        source,
                        transformationAffectSlot,
                        transformationAffectSlot >= 0 ? participant.Affect[transformationAffectSlot * 8 + 1] : affectValue,
                        transformationAffectSlot >= 0 ? BinaryPrimitives.ReadUInt32LittleEndian(participant.Affect.AsSpan((transformationAffectSlot * 8) + 4)) : 0,
                        participant.Mob.ToArray(),
                        participant.Affect.ToArray(),
                        AffectType: 33);
                    return transformationAffectSlot < 0 ? LegacyUseItemResult.InvalidPlacement : LegacyUseItemResult.AlreadyCompleted;
                }

                var nextAffect = participant.Affect.ToArray();
                var transformationAffectOffset = transformationAffectSlot * 8;
                nextAffect[transformationAffectOffset] = 33;
                nextAffect[transformationAffectOffset + 1] = affectValue;
                BinaryPrimitives.WriteUInt16LittleEndian(nextAffect.AsSpan(transformationAffectOffset + 2), 1);
                BinaryPrimitives.WriteUInt32LittleEndian(nextAffect.AsSpan(transformationAffectOffset + 4), affectTime);

                var transformationAmount = GetLegacyItemAmount(source);
                var updatedTransformationSource = transformationAmount > 1 ? SetLegacyItemAmount(source, transformationAmount - 1) : default;
                updatedTransformationSource.Write(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
                participant.Affect = nextAffect;
                outcome = new LegacyAffectConsumableOutcome(
                    request.SourceSlot,
                    updatedTransformationSource,
                    transformationAffectSlot,
                    affectValue,
                    affectTime,
                    participant.Mob.ToArray(),
                    nextAffect,
                    AffectType: 33);
                return LegacyUseItemResult.Accepted;
            }

            var volatileValue = itemData.GetItemAbility(source, LegacyItemEffect.Volatile);
            if (volatileValue is not (10 or 52 or 53 or 55 or 56 or 57 or 200 or 201 or 202))
                return LegacyUseItemResult.UnsupportedItem;

            var effect = source.Index switch
            {
                787 => (Value: (byte)1, Time: (uint)80),
                1764 => (Value: (byte)2, Time: (uint)80),
                1765 => (Value: (byte)3, Time: (uint)80),
                3310 => (Value: (byte)1, Time: (uint)225),
                3311 => (Value: (byte)2, Time: (uint)450),
                3312 => (Value: (byte)3, Time: (uint)450),
                3319 => (Value: (byte)1, Time: (uint)9000),
                3320 => (Value: (byte)2, Time: (uint)9000),
                3321 => (Value: (byte)3, Time: (uint)9000),
                3361 => (Value: (byte)4, Time: (uint)75600),
                3362 => (Value: (byte)4, Time: (uint)162000),
                3363 => (Value: (byte)4, Time: (uint)324000),
                416 => (Value: (byte)5, Time: (uint)225),
                _ => (Value: (byte)0, Time: (uint)80),
            };
            var affectSlot = -1;
            for (var index = 0; index < LegacyAccountSnapshot.AffectStride / 8; index++)
            {
                var offset = index * 8;
                if (participant.Affect[offset] == 4 && participant.Affect[offset + 1] == effect.Value)
                {
                    affectSlot = index;
                    break;
                }
            }
            if (affectSlot < 0)
            {
                for (var index = 0; index < LegacyAccountSnapshot.AffectStride / 8; index++)
                {
                    if (participant.Affect[index * 8] == 0)
                    {
                        affectSlot = index;
                        break;
                    }
                }
            }

            if (affectSlot >= 0)
            {
                var offset = affectSlot * 8;
                participant.Affect[offset] = 4;
                participant.Affect[offset + 1] = effect.Value;
                BinaryPrimitives.WriteUInt16LittleEndian(participant.Affect.AsSpan(offset + 2), 0);
                BinaryPrimitives.WriteUInt32LittleEndian(participant.Affect.AsSpan(offset + 4), effect.Time);
            }

            var amount = GetLegacyItemAmount(source);
            var updatedSource = amount > 1 ? SetLegacyItemAmount(source, amount - 1) : default;
            updatedSource.Write(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            outcome = new LegacyAffectConsumableOutcome(request.SourceSlot, updatedSource, affectSlot, effect.Value, effect.Time, participant.Mob.ToArray(), participant.Affect.ToArray());
            return LegacyUseItemResult.Accepted;
        }
    }

    /// <summary>Applies the legacy Vol 242 PvP-jewelry bitmask affect.</summary>
    public LegacyUseItemResult TryApplyPvpJewelry(int connectionId, UseItemRequest request, out LegacyPvpJewelryOutcome? outcome)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyUseItemResult.ParticipantNotFound;
            if (itemData is null)
                return LegacyUseItemResult.ItemDataUnavailable;
            if (participant.Mob.Length < LegacyAccountSnapshot.MobCurrentScoreOffset + LegacyScore.SizeInBytes || participant.Affect.Length != LegacyAccountSnapshot.AffectStride)
                return LegacyUseItemResult.InvalidPlacement;
            if (BinaryPrimitives.ReadInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24)) <= 0)
                return LegacyUseItemResult.AttackerNotAlive;
            if (request.SourceType != 1 || request.SourceSlot is < 0 or >= LegacyAccountSnapshot.MobCarryCount)
                return LegacyUseItemResult.InvalidPlacement;

            var sourceOffset = LegacyAccountSnapshot.MobCarryOffset + (request.SourceSlot * LegacyItem.SizeInBytes);
            var source = LegacyItem.Read(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            if (source.Index == 0)
                return LegacyUseItemResult.SourceEmpty;
            if (itemData.GetItemAbility(source, LegacyItemEffect.Volatile) != 242)
                return LegacyUseItemResult.UnsupportedItem;

            // The legacy code defaults unknown Vol 242 indexes to the first bit.
            var jewelryBit = source.Index switch
            {
                3201 => 1,
                3202 => 2,
                3204 => 3,
                3205 => 4,
                3206 => 5,
                3208 => 6,
                3209 => 7,
                _ => 0,
            };
            var jewelryMask = (ushort)(1 << jewelryBit);

            // Reproduce GetEmptyAffect(conn, 8): prefer the existing Type 8
            // slot, then the first empty slot, and fail only when both are full.
            var affectSlot = -1;
            var affectCount = LegacyAccountSnapshot.AffectStride / 8;
            for (var index = 0; index < affectCount; index++)
            {
                if (participant.Affect[index * 8] == 8)
                {
                    affectSlot = index;
                    break;
                }
            }
            if (affectSlot < 0)
            {
                for (var index = 0; index < affectCount; index++)
                {
                    if (participant.Affect[index * 8] == 0)
                    {
                        affectSlot = index;
                        break;
                    }
                }
            }
            if (affectSlot < 0)
                return LegacyUseItemResult.InvalidDestination;

            var affectOffset = affectSlot * 8;
            if (participant.Affect[affectOffset] != 8)
            {
                participant.Affect[affectOffset] = 8;
                BinaryPrimitives.WriteUInt16LittleEndian(participant.Affect.AsSpan(affectOffset + 2), jewelryMask);
                participant.Affect[affectOffset + 1] = 0;
            }
            else
            {
                var currentLevel = BinaryPrimitives.ReadUInt16LittleEndian(participant.Affect.AsSpan(affectOffset + 2));
                BinaryPrimitives.WriteUInt16LittleEndian(participant.Affect.AsSpan(affectOffset + 2), (ushort)(currentLevel | jewelryMask));
            }
            BinaryPrimitives.WriteUInt32LittleEndian(participant.Affect.AsSpan(affectOffset + 4), 450);

            var amount = GetLegacyItemAmount(source);
            var updatedSource = amount > 1 ? SetLegacyItemAmount(source, amount - 1) : default;
            updatedSource.Write(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            var affectLevel = BinaryPrimitives.ReadUInt16LittleEndian(participant.Affect.AsSpan(affectOffset + 2));
            outcome = new LegacyPvpJewelryOutcome(request.SourceSlot, updatedSource, affectSlot, affectLevel, 450, participant.Mob.ToArray(), participant.Affect.ToArray());
            return LegacyUseItemResult.Accepted;
        }
    }

    /// <summary>Applies the legacy Vol 11 recall, Vol 12 star gem, and Vol 13 portal.</summary>
    public LegacyUseItemResult TryApplyMovementConsumable(
        int connectionId,
        UseItemRequest request,
        out LegacyMovementConsumableOutcome? outcome,
        int cityRandomX = -1,
        int cityRandomY = -1,
        int newbieRandomX = -1,
        int newbieRandomY = -1)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyUseItemResult.ParticipantNotFound;
            if (itemData is null)
                return LegacyUseItemResult.ItemDataUnavailable;
            if (participant.Mob.Length < LegacyAccountSnapshot.MobCurrentScoreOffset + LegacyScore.SizeInBytes)
                return LegacyUseItemResult.InvalidPlacement;
            if (BinaryPrimitives.ReadInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24)) <= 0)
                return LegacyUseItemResult.AttackerNotAlive;
            if (request.SourceType != 1 || request.SourceSlot is < 0 or >= LegacyAccountSnapshot.MobCarryCount)
                return LegacyUseItemResult.InvalidPlacement;

            var sourceOffset = LegacyAccountSnapshot.MobCarryOffset + (request.SourceSlot * LegacyItem.SizeInBytes);
            var source = LegacyItem.Read(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            if (source.Index == 0)
                return LegacyUseItemResult.SourceEmpty;
            var volatileValue = itemData.GetItemAbility(source, LegacyItemEffect.Volatile);
            if (volatileValue is not (11 or 12 or 13))
                return LegacyUseItemResult.UnsupportedItem;

            var state = LegacyMobCombatState.Read(participant.Mob);
            var targetX = participant.PositionX;
            var targetY = participant.PositionY;
            var savedWarp = false;
            if (volatileValue == 12)
            {
                var mapAttribute = mapGrid?.GetAttribute(participant.PositionX, participant.PositionY) ?? participant.MapAttribute;
                var specialMap = participant.PositionX / 128 == 9 && participant.PositionY / 128 == 1 || participant.PositionX / 128 == 8 && participant.PositionY / 128 == 2;
                if (!specialMap && (mapAttribute & 4) != 0 && state.CurrentScore.Level < 1000)
                    return LegacyUseItemResult.MapRestricted;

                BinaryPrimitives.WriteInt16LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobSavedPositionXOffset), participant.PositionX);
                BinaryPrimitives.WriteInt16LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobSavedPositionYOffset), participant.PositionY);
                savedWarp = true;
            }
            else if (volatileValue == 13)
            {
                var savedX = BinaryPrimitives.ReadInt16LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobSavedPositionXOffset));
                var savedY = BinaryPrimitives.ReadInt16LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobSavedPositionYOffset));
                var specialSavedMap = savedX / 128 == 9 && savedY / 128 == 1 || savedX / 128 == 8 && savedY / 128 == 2;
                var mapAttribute = mapGrid?.GetAttribute(participant.PositionX, participant.PositionY) ?? participant.MapAttribute;
                if (specialSavedMap || (mapAttribute & 4) != 0 && state.CurrentScore.Level < 1000)
                    return LegacyUseItemResult.MapRestricted;
                targetX = savedX;
                targetY = savedY;
            }
            else
            {
                (targetX, targetY) = GetLegacyRecallPosition(participant, state, cityRandomX, cityRandomY, newbieRandomX, newbieRandomY);
            }

            var moved = volatileValue is 11 or 13;
            if (moved)
            {
                participant.PositionX = targetX;
                participant.PositionY = targetY;
            }
            var amount = GetLegacyItemAmount(source);
            var updatedSource = amount > 1 ? SetLegacyItemAmount(source, amount - 1) : default;
            updatedSource.Write(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            outcome = new LegacyMovementConsumableOutcome(request.SourceSlot, updatedSource, volatileValue, targetX, targetY, moved, savedWarp, participant.Mob.ToArray());
            return LegacyUseItemResult.Accepted;
        }
    }

    /// <summary>Applies the legacy Vol 93 mount restorer and Vol 94 catalyst.</summary>
    public LegacyUseItemResult TryApplyMountCatalyst(int connectionId, UseItemRequest request, out LegacyMountCatalystOutcome? outcome, Func<int, int>? roll = null)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyUseItemResult.ParticipantNotFound;
            if (itemData is null)
                return LegacyUseItemResult.ItemDataUnavailable;
            if (participant.Mob.Length < LegacyAccountSnapshot.MobEquipmentOffset + (15 * LegacyItem.SizeInBytes) + LegacyItem.SizeInBytes)
                return LegacyUseItemResult.InvalidPlacement;
            if (BinaryPrimitives.ReadInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24)) <= 0)
                return LegacyUseItemResult.AttackerNotAlive;
            if (request.SourceType != 1 || request.SourceSlot is < 0 or >= LegacyAccountSnapshot.MobCarryCount || request.DestinationType != 0 || request.DestinationSlot != 14)
                return LegacyUseItemResult.InvalidPlacement;

            var sourceOffset = LegacyAccountSnapshot.MobCarryOffset + (request.SourceSlot * LegacyItem.SizeInBytes);
            var destinationOffset = LegacyAccountSnapshot.MobEquipmentOffset + (request.DestinationSlot * LegacyItem.SizeInBytes);
            var source = LegacyItem.Read(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            var destination = LegacyItem.Read(participant.Mob.AsSpan(destinationOffset, LegacyItem.SizeInBytes));
            if (source.Index == 0)
                return LegacyUseItemResult.SourceEmpty;
            if (destination.Index == 0)
                return LegacyUseItemResult.DestinationEmpty;

            var volatileValue = itemData.GetItemAbility(source, LegacyItemEffect.Volatile);
            if (volatileValue is not (93 or 94))
                return LegacyUseItemResult.UnsupportedItem;
            var catalystType = source.Index - (volatileValue == 94 ? 3344 : 3351);
            if (catalystType is < 0 or > 6)
                return LegacyUseItemResult.UnsupportedItem;

            var mount = destination.Index - (volatileValue == 94 ? 2333 : 2363);
            var mountType = GetLegacyMountType(mount);
            if (mountType != catalystType)
                return LegacyUseItemResult.InvalidDestination;

            LegacyItem updatedDestination;
            if (volatileValue == 94)
            {
                var randomValue = roll?.Invoke(20) ?? Random.Shared.Next(20);
                if (randomValue is < 0 or >= 20)
                    throw new ArgumentOutOfRangeException(nameof(roll), "Injected roll must be in [0, 20).");
                updatedDestination = destination with
                {
                    Index = checked((short)(destination.Index + 30)),
                    Effect2 = 0,
                    Value2 = unchecked((byte)(destination.Value2 + randomValue + destination.Effect2)),
                    Value3 = 0,
                };
            }
            else
            {
                if (destination.Value2 >= 50 || destination.Value2 <= 5 || destination.Value1 <= 0)
                    return LegacyUseItemResult.InvalidDestination;
                var randomValue = roll?.Invoke(2) ?? Random.Shared.Next(2);
                if (randomValue is < 0 or >= 2)
                    throw new ArgumentOutOfRangeException(nameof(roll), "Injected roll must be in [0, 2).");
                updatedDestination = destination with { Value2 = unchecked((byte)(destination.Value2 + randomValue + 1)) };
            }

            var amount = GetLegacyItemAmount(source);
            var updatedSource = amount > 1 ? SetLegacyItemAmount(source, amount - 1) : default;
            updatedSource.Write(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            updatedDestination.Write(participant.Mob.AsSpan(destinationOffset, LegacyItem.SizeInBytes));
            participant.ClientEquipment[request.DestinationSlot] = updatedDestination;
            participant.RuntimeParryAdjustment = 0;
            outcome = new LegacyMountCatalystOutcome(request.SourceSlot, updatedSource, request.DestinationSlot, updatedDestination, volatileValue == 93);
            return LegacyUseItemResult.Accepted;
        }
    }

    /// <summary>Applies the legacy Vol 185 silver-bar coin consumable.</summary>
    public LegacyUseItemResult TryApplyCoinConsumable(int connectionId, UseItemRequest request, out LegacyCoinConsumableOutcome? outcome)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyUseItemResult.ParticipantNotFound;
            if (itemData is null)
                return LegacyUseItemResult.ItemDataUnavailable;
            if (participant.Mob.Length < LegacyAccountSnapshot.MobCoinOffset + sizeof(int) || participant.Mob.Length < LegacyAccountSnapshot.MobCurrentScoreOffset + 28)
                return LegacyUseItemResult.InvalidPlacement;
            if (BinaryPrimitives.ReadInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24)) <= 0)
                return LegacyUseItemResult.AttackerNotAlive;
            if (request.SourceType != 1 || request.SourceSlot is < 0 or >= LegacyAccountSnapshot.MobCarryCount)
                return LegacyUseItemResult.InvalidPlacement;

            var sourceOffset = LegacyAccountSnapshot.MobCarryOffset + (request.SourceSlot * LegacyItem.SizeInBytes);
            var source = LegacyItem.Read(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            if (source.Index == 0)
                return LegacyUseItemResult.SourceEmpty;
            if (itemData.GetItemAbility(source, LegacyItemEffect.Volatile) != 185)
                return LegacyUseItemResult.UnsupportedItem;

            var addedCoin = source.Index switch
            {
                4010 => 100_000_000,
                4011 => 1_000_000_000,
                4026 => 1_000_000,
                4027 => 5_000_000,
                4028 => 10_000_000,
                4029 => 50_000_000,
                _ => 0,
            };
            var currentCoin = BinaryPrimitives.ReadInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset));
            var finalCoin = (long)currentCoin + addedCoin;
            if (finalCoin > 2_000_000_000L)
                return LegacyUseItemResult.CoinLimitReached;

            BinaryPrimitives.WriteInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), checked((int)finalCoin));
            participant.Coin = checked((int)finalCoin);
            var amount = GetLegacyItemAmount(source);
            var updatedSource = amount > 1 ? SetLegacyItemAmount(source, amount - 1) : default;
            updatedSource.Write(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            outcome = new LegacyCoinConsumableOutcome(request.SourceSlot, updatedSource, addedCoin, checked((int)finalCoin));
            return LegacyUseItemResult.Accepted;
        }
    }

    /// <summary>Applies the legacy Vol 184 Donate voucher and consumes one unit.</summary>
    public LegacyUseItemResult TryApplyDonateConsumable(int connectionId, UseItemRequest request, out LegacyDonateConsumableOutcome? outcome)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyUseItemResult.ParticipantNotFound;
            if (itemData is null)
                return LegacyUseItemResult.ItemDataUnavailable;
            if (participant.Mob.Length < LegacyAccountSnapshot.MobCurrentScoreOffset + 28)
                return LegacyUseItemResult.InvalidPlacement;
            if (BinaryPrimitives.ReadInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24)) <= 0)
                return LegacyUseItemResult.AttackerNotAlive;
            if (request.SourceType != 1 || request.SourceSlot is < 0 or >= LegacyAccountSnapshot.MobCarryCount)
                return LegacyUseItemResult.InvalidPlacement;

            var sourceOffset = LegacyAccountSnapshot.MobCarryOffset + (request.SourceSlot * LegacyItem.SizeInBytes);
            var source = LegacyItem.Read(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            if (source.Index == 0)
                return LegacyUseItemResult.SourceEmpty;
            if (itemData.GetItemAbility(source, LegacyItemEffect.Volatile) != 184)
                return LegacyUseItemResult.UnsupportedItem;

            var addedDonate = itemData.GetItemAbility(source, LegacyItemEffect.Donate);
            var previousDonate = participant.Donate;
            var previousMob = participant.Mob.ToArray();
            var finalDonate = (long)previousDonate + addedDonate;
            if (finalDonate > int.MaxValue)
                return LegacyUseItemResult.DonateLimitReached;

            participant.Donate = checked((int)finalDonate);
            var amount = GetLegacyItemAmount(source);
            var updatedSource = amount > 1 ? SetLegacyItemAmount(source, amount - 1) : default;
            updatedSource.Write(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            outcome = new LegacyDonateConsumableOutcome(
                request.SourceSlot,
                updatedSource,
                addedDonate,
                participant.Donate,
                participant.Mob.ToArray(),
                previousDonate,
                previousMob);
            return LegacyUseItemResult.Accepted;
        }
    }

    /// <summary>Rolls back a Vol 184 mutation when its Donate balance cannot be persisted.</summary>
    public bool TryRollbackDonateConsumable(int connectionId, LegacyDonateConsumableOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant) || participant.Mob.Length != outcome.PreviousMob.Length)
                return false;
            outcome.PreviousMob.AsSpan().CopyTo(participant.Mob);
            participant.Donate = outcome.PreviousDonate;
            return true;
        }
    }

    /// <summary>Applies the legacy Vol 1 HP/MP potion operation to the carry.</summary>
    public LegacyUseItemResult TryApplyPotion(int connectionId, UseItemRequest request, out LegacyPotionOutcome? outcome, long nowMilliseconds = -1)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyUseItemResult.ParticipantNotFound;
            if (itemData is null)
                return LegacyUseItemResult.ItemDataUnavailable;
            if (participant.Mob.Length < LegacyAccountSnapshot.MobCurrentScoreOffset + 32)
                return LegacyUseItemResult.InvalidPlacement;
            if (request.SourceType != 1 || request.SourceSlot is < 0 or >= LegacyAccountSnapshot.MobCarryCount)
                return LegacyUseItemResult.InvalidPlacement;

            var sourceOffset = LegacyAccountSnapshot.MobCarryOffset + (request.SourceSlot * LegacyItem.SizeInBytes);
            var source = LegacyItem.Read(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            if (source.Index == 0)
                return LegacyUseItemResult.SourceEmpty;
            if (itemData.GetItemAbility(source, LegacyItemEffect.Volatile) != 1)
                return LegacyUseItemResult.UnsupportedItem;

            var current = LegacyMobCombatState.Read(participant.Mob);
            if (current.CurrentScore.Hp <= 0)
                return LegacyUseItemResult.AttackerNotAlive;

            nowMilliseconds = nowMilliseconds < 0 ? Environment.TickCount64 : nowMilliseconds;
            if (participant.LastPotionAt != long.MinValue && nowMilliseconds - participant.LastPotionAt < 100)
                return LegacyUseItemResult.PotionDelay;

            var hp = itemData.GetItemAbility(source, LegacyItemEffect.Hp);
            var mp = itemData.GetItemAbility(source, LegacyItemEffect.Mp);
            participant.RequestedHp = Math.Clamp(Math.Max(participant.RequestedHp, current.CurrentScore.Hp) + hp, 0, current.CurrentScore.MaxHp);
            participant.RequestedMana = Math.Clamp(Math.Max(participant.RequestedMana, current.CurrentMana) + mp, 0, current.CurrentScore.MaxMp);
            participant.LastPotionAt = nowMilliseconds;

            var amount = GetLegacyItemAmount(source);
            var updatedSource = amount > 1 ? SetLegacyItemAmount(source, amount - 1) : default;
            updatedSource.Write(participant.Mob.AsSpan(sourceOffset, LegacyItem.SizeInBytes));
            outcome = new LegacyPotionOutcome(request.SourceSlot, updatedSource, participant.RequestedHp, participant.RequestedMana);
            return LegacyUseItemResult.Accepted;
        }
    }

    /// <summary>Updates the current world tile; accepted Action maps to legacy GridMulticast TargetX/TargetY.</summary>
    public bool UpdatePosition(int connectionId, short positionX, short positionY)
    {
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant)) return false;
            participant.PositionX = positionX;
            participant.PositionY = positionY;
            return true;
        }
    }

    /// <summary>
    /// Rebuilds the private Effect=0 correction sent by TMProject 7.69 when an
    /// Action destination is occupied. This path deliberately leaves the
    /// participant's authoritative position unchanged.
    /// </summary>
    public bool TryCorrectOccupiedAction(int connectionId, ActionRequest request, out ActionRequest? corrected)
    {
        lock (gate)
        {
            corrected = null;
            if (!participants.ContainsKey(connectionId))
                return false;

            var occupantId = GetParticipantAtPosition(request.TargetX, request.TargetY);
            if (occupantId == 0 || occupantId == connectionId)
                return false;

            var destinationX = (int)request.TargetX;
            var destinationY = (int)request.TargetY;
            Func<int, int, bool> blockedAt = mapGrid is null ? static (_, _) => false : mapGrid.IsBlocked;
            LegacyMobGridSearch.TryFindEmpty(
                connectionId,
                ref destinationX,
                ref destinationY,
                GetParticipantAtPosition,
                blockedAt,
                checkCandidateTerrain: true,
                maximumRadius: 9);

            var route = request.Route.ToArray();
            var routeLength = Array.IndexOf(route, (byte)0);
            if (routeLength < 0)
                routeLength = route.Length;

            for (var distance = routeLength; distance >= 0; distance--)
            {
                var currentOccupant = GetParticipantAtPosition(destinationX, destinationY);
                if (currentOccupant != 0 && currentOccupant != connectionId)
                    continue;

                LegacyActionRouteBuilder.Rebuild(
                    request.PositionX,
                    request.PositionY,
                    ref destinationX,
                    ref destinationY,
                    route,
                    distance,
                    GetTerrainHeight);
            }

            corrected = request with
            {
                Effect = 0,
                Route = route,
                TargetX = checked((short)destinationX),
                TargetY = checked((short)destinationY),
            };
            return true;
        }

        int GetTerrainHeight(int x, int y) => mapGrid?.GetTerrainHeight(x, y) ?? 0;
    }

    /// <summary>
    /// Advances summons owned by a leader after the leader moves. This mirrors
    /// CMob::StandingByProcessor for the legacy summon affect 24 path: no move
    /// within four cells, one-cell follow between five and twelve, and a
    /// teleport to the first free cell at distance thirteen or more.
    /// </summary>
    public bool TryAdvanceSummons(int leaderConnectionId, out IReadOnlyList<LegacySummonMovement> movements)
    {
        lock (gate)
        {
            movements = [];
            if (!participants.TryGetValue(leaderConnectionId, out var leader)) return false;

            var moved = new List<LegacySummonMovement>();
            foreach (var summonId in leader.SummonIds.ToArray())
            {
                if (!summons.TryGetValue(summonId, out var summon)) continue;
                var distance = LegacyCombatMath.GetDistance(summon.PositionX, summon.PositionY, leader.PositionX, leader.PositionY);
                if (distance <= 4) continue;

                var oldX = summon.PositionX;
                var oldY = summon.PositionY;
                var nextX = (int)oldX;
                var nextY = (int)oldY;
                var effect = 0;
                if (distance >= 13)
                {
                    nextX = leader.PositionX;
                    nextY = leader.PositionY;
                    if (!LegacyMobGridSearch.TryFindEmpty(
                            summon.ConnectionId,
                            ref nextX,
                            ref nextY,
                            GetParticipantAtPosition,
                            mapGrid is null ? static (_, _) => false : mapGrid.IsBlocked,
                            checkCandidateTerrain: mapCollisionMode == LegacyMapCollisionMode.CandidateAware))
                        continue;
                    effect = 1;
                }
                else if (!TryFindSummonFollowCell(summon, leader, ref nextX, ref nextY))
                    continue;

                var updated = summon with { PositionX = checked((short)nextX), PositionY = checked((short)nextY) };
                summons[summon.ConnectionId] = updated;
                moved.Add(new LegacySummonMovement(summon.ConnectionId, oldX, oldY, updated.PositionX, updated.PositionY, effect, effect == 1 ? 0 : 6));
            }

            movements = moved;
            return true;
        }
    }

    /// <summary>Runs one peaceful RouteType 5 summon follower opportunity under the WorldHub gate.</summary>
    public bool TryApplyRouteTypeFiveSummonPeaceStep(
        int summonConnectionId,
        Func<int> randomFallbackOffset,
        out LegacyNpcAiApplicationOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(randomFallbackOffset);
        lock (gate)
        {
            outcome = LegacyNpcAiApplicationOutcome.Empty(LegacyMobAiDecisionKind.Idle);
            if (!summons.TryGetValue(summonConnectionId, out var summon) || !IsLiveSummonLocked(summon))
                return false;

            if (LegacyMobAiPlanner.IsRunequestLv6AreaDespawnCandidate(-1, summon.PositionX, summon.PositionY))
            {
                var recipients = Array.AsReadOnly(GetParticipantIdsInViewLocked(summon.PositionX, summon.PositionY));
                if (!RemoveSummonLocked(summon.ConnectionId))
                    return false;
                outcome = LegacyNpcAiApplicationOutcome.Empty(LegacyMobAiDecisionKind.Despawn) with
                {
                    Status = LegacyNpcAiApplyStatus.Applied,
                    PositionX = summon.PositionX,
                    PositionY = summon.PositionY,
                    Removal = new LegacyGeneratedNpcRemovalOutcome(summon.ConnectionId, recipients, RemoveType: 3)
                };
                return true;
            }

            if (summon.RouteType != 5 ||
                summon.SummonerConnectionId is <= 0 or >= LegacyMobAiPlanner.MaximumPlayerId ||
                summon.LeaderConnectionId is <= 0 or >= LegacyMobAiPlanner.MaximumPlayerId ||
                !participants.TryGetValue(summon.SummonerConnectionId, out var summoner) ||
                summoner.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize ||
                LegacyMobCombatState.Read(summoner.Mob).CurrentScore.Hp <= 0 ||
                !participants.TryGetValue(summon.LeaderConnectionId, out var leader) ||
                leader.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                return false;

            if (summon.EnemyList.Any(static id => id != 0))
                return false;

            if (summonTargets.TryGetValue(summon.ConnectionId, out var selectedTargetId) && selectedTargetId != 0)
            {
                var selectedTargetView = summon.MobSnapshot[LegacyAccountSnapshot.MobClanOffset] is 7 or 8 ? 16 : 12;
                var hasLiveVisibleTarget = selectedTargetId != summon.ConnectionId &&
                    TryGetBattleTargetLocked(selectedTargetId, out var selectedTargetX, out var selectedTargetY,
                        out var selectedTargetMob, out _) &&
                    (selectedTargetId >= LegacyMobAiPlanner.MaximumPlayerId ||
                     (LegacyMobCombatState.Read(selectedTargetMob).Rsv & 0x10) == 0) &&
                    Math.Abs(selectedTargetX - summon.PositionX) <= selectedTargetView &&
                    Math.Abs(selectedTargetY - summon.PositionY) <= selectedTargetView;
                if (hasLiveVisibleTarget)
                    return false;

                // Target selection may outlive a disconnect, death, hide, or range change.
                summonTargets[summon.ConnectionId] = 0;
            }

            const byte summonLifetimeAffectType = 24;
            const int affectSizeInBytes = 8;
            const uint persistentAffectThreshold = 32_400_000;
            if (summon.AffectSnapshot.Length == LegacyAccountSnapshot.AffectStride)
            {
                var affect = summon.AffectSnapshot.ToArray();
                var affectChanged = false;
                for (var index = 0; index < affect.Length / affectSizeInBytes; index++)
                {
                    var offset = index * affectSizeInBytes;
                    if (affect[offset] != summonLifetimeAffectType)
                        continue;

                    var remainingTime = BinaryPrimitives.ReadUInt32LittleEndian(affect.AsSpan(offset + 4));
                    if (remainingTime < persistentAffectThreshold)
                        remainingTime = unchecked(remainingTime - 1);

                    if (remainingTime == 0)
                    {
                        var recipients = Array.AsReadOnly(GetParticipantIdsInViewLocked(summon.PositionX, summon.PositionY));
                        if (!RemoveSummonLocked(summon.ConnectionId))
                            return false;

                        outcome = LegacyNpcAiApplicationOutcome.Empty(LegacyMobAiDecisionKind.Despawn) with
                        {
                            Status = LegacyNpcAiApplyStatus.Applied,
                            PositionX = summon.PositionX,
                            PositionY = summon.PositionY,
                            Removal = new LegacyGeneratedNpcRemovalOutcome(
                                summon.ConnectionId,
                                recipients,
                                RemoveType: 3),
                        };
                        return true;
                    }

                    if (remainingTime != BinaryPrimitives.ReadUInt32LittleEndian(affect.AsSpan(offset + 4)))
                    {
                        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(offset + 4), remainingTime);
                        affectChanged = true;
                    }
                }

                if (affectChanged)
                {
                    summon = summon with { AffectSnapshot = affect };
                    summons[summon.ConnectionId] = summon;
                }
            }

            var summonAffectUpdate = TryProcessSummonTimerAffectsLocked(summon, out summon);

            var authorizedSummoner = summon.LeaderConnectionId == summon.SummonerConnectionId ||
                                     partyLeaders.TryGetValue(summon.SummonerConnectionId, out var partyLeaderId) &&
                                     partyLeaderId == summon.LeaderConnectionId;
            if (!authorizedSummoner)
                return TryReturnSummonAffectUpdate(summonAffectUpdate, out outcome);

            var state = LegacyMobCombatState.Read(summon.MobSnapshot);
            var currentScore = state.CurrentScore;
            var baseScore = state.BaseScore;
            var equipment = LegacyItem.Read(summon.MobSnapshot.AsSpan(
                LegacyAccountSnapshot.MobEquipmentOffset,
                LegacyItem.SizeInBytes));
            var inputMob = new LegacyMobAiMob(
                summon.ConnectionId,
                summon.PositionX,
                summon.PositionY,
                summon.PositionX,
                summon.PositionY,
                summon.RouteType,
                summon.MobSnapshot[LegacyAccountSnapshot.MobClanOffset],
                GenerateIndex: -1,
                LegacyMobAiMode.Peace,
                CurrentTarget: 0,
                summon.EnemyList.ToArray(),
                summon.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4).ToArray(),
                baseScore.Intelligence,
                baseScore.Dexterity,
                AttackRange: 0,
                currentScore.Level,
                currentScore.Hp,
                currentScore.MaxHp,
                summon.SummonerConnectionId,
                summon.LeaderConnectionId,
                equipment.Index,
                LegacyNpcGenerationRoute.Empty,
                SegmentProgress: 0,
                SegmentDirection: 0,
                WaitSec: 0,
                IsActive: true,
                SummonerAuthorized: true)
            {
                BaseSpecial = [baseScore.Special1, baseScore.Special2,
                    baseScore.Special3, baseScore.Special4]
            };
            var summonerState = LegacyMobCombatState.Read(summoner.Mob);
            var summonerTarget = new LegacyMobAiTarget(
                summoner.ConnectionId,
                summoner.PositionX,
                summoner.PositionY,
                IsNpc: false,
                IsActive: true,
                summonerState.CurrentScore.Hp,
                Hidden: (summonerState.Rsv & 0x10) != 0,
                Merchant: (summoner.Mob[LegacyAccountSnapshot.MobMerchantOffset] & 1) != 0,
                summonerState.BaseScore.Level);
            var input = new LegacyMobAiInput(inputMob, [], summonerTarget, RouteStep: null,
                RandomIntRoll: 0, DexterityRoll: 0)
            {
                SkillData = skillData
            };
            var preview = LegacyMobAiPlanner.Plan(input);
            var targetX = preview.NextX ?? preview.TargetX;
            var targetY = preview.NextY ?? preview.TargetY;
            if (preview.Kind is not (LegacyMobAiDecisionKind.Move or LegacyMobAiDecisionKind.Idle) ||
                targetX is null || targetY is null)
                return TryReturnSummonAffectUpdate(summonAffectUpdate, out outcome);

            var followX = targetX.Value;
            var followY = targetY.Value;
            var distance = LegacyCombatMath.GetDistance(summon.PositionX, summon.PositionY, followX, followY);
            if (distance <= 4)
                return TryReturnSummonAffectUpdate(summonAffectUpdate, out outcome);

            LegacyNpcAiMovementPlan? movementPlan;
            LegacyMobAiDecision decision;
            if (distance >= 13)
            {
                var destinationX = followX;
                var destinationY = followY;
                if (!LegacyMobGridSearch.TryFindEmpty(
                        summon.ConnectionId,
                        ref destinationX,
                        ref destinationY,
                        GetParticipantAtPosition,
                        mapGrid is null ? static (_, _) => false : mapGrid.IsBlocked,
                        checkCandidateTerrain: mapCollisionMode == LegacyMapCollisionMode.CandidateAware,
                        maximumRadius: 9) ||
                    destinationX == summon.PositionX && destinationY == summon.PositionY)
                    return TryReturnSummonAffectUpdate(summonAffectUpdate, out outcome);

                movementPlan = new LegacyNpcAiMovementPlan(
                    summon.PositionX,
                    summon.PositionY,
                    checked((short)destinationX),
                    checked((short)destinationY),
                    Speed: 2);
                decision = preview with { Kind = LegacyMobAiDecisionKind.Move, NextX = destinationX, NextY = destinationY };
            }
            else
            {
                var movementNpc = new LegacyWorldNpc(
                    summon.ConnectionId,
                    summon.PositionX,
                    summon.PositionY,
                    summon.MobSnapshot,
                    summon.AffectSnapshot,
                    GenerateIndex: -1,
                    RouteType: summon.RouteType);
                if (!LegacyMobAiMovementPlanner.TryBuildChaseStep(
                        movementNpc,
                        followX,
                        followY,
                        mapGrid,
                        GetParticipantAtPosition,
                        randomFallbackOffset,
                        out movementPlan) || movementPlan is null)
                    return TryReturnSummonAffectUpdate(summonAffectUpdate, out outcome);

                input = input with { RouteStep = new LegacyMobAiPoint(movementPlan.ToX, movementPlan.ToY) };
                decision = LegacyMobAiPlanner.Plan(input);
                if (decision.Kind != LegacyMobAiDecisionKind.Move ||
                    decision.NextX != movementPlan.ToX || decision.NextY != movementPlan.ToY)
                    return TryReturnSummonAffectUpdate(summonAffectUpdate, out outcome);
            }

            var movementOriginViewIds = GetParticipantIdsInViewLocked(summon.PositionX, summon.PositionY);
            summon = summon with { PositionX = movementPlan.ToX, PositionY = movementPlan.ToY };
            summons[summon.ConnectionId] = summon;
            var movementOutcome = new LegacyGeneratedNpcMovementOutcome(
                summon.ConnectionId,
                movementPlan.FromX,
                movementPlan.FromY,
                movementPlan.ToX,
                movementPlan.ToY,
                movementPlan.Speed,
                summon.MobSnapshot.ToArray(),
                summon.AffectSnapshot.ToArray(),
                Array.AsReadOnly(movementOriginViewIds),
                Array.AsReadOnly(GetParticipantIdsInViewLocked(summon.PositionX, summon.PositionY)),
                combatWorldState.BattleRoyaleState,
                Effect: distance >= 13 ? 1 : 0);
            outcome = new LegacyNpcAiApplicationOutcome(
                LegacyNpcAiApplyStatus.Applied,
                decision.Kind,
                summon.PositionX,
                summon.PositionY,
                LegacyNpcMode.Peace,
                0,
                summon.EnemyList.ToArray(),
                summon.PositionX,
                summon.PositionY,
                0,
                0,
                0,
                null,
                Movement: movementOutcome)
            {
                AffectTick = summonAffectUpdate?.AffectTick,
            };
            return true;
        }
    }

    private bool TryFindSummonFollowCell(LegacySummonedMob summon, Participant leader, ref int nextX, ref int nextY)
    {
        var currentDistance = LegacyCombatMath.GetDistance(summon.PositionX, summon.PositionY, leader.PositionX, leader.PositionY);
        var bestDistance = currentDistance;
        var found = false;
        for (var candidateY = summon.PositionY - 1; candidateY <= summon.PositionY + 1; candidateY++)
        {
            for (var candidateX = summon.PositionX - 1; candidateX <= summon.PositionX + 1; candidateX++)
            {
                if (candidateX == summon.PositionX && candidateY == summon.PositionY) continue;
                if (GetParticipantAtPosition(candidateX, candidateY) != 0) continue;
                if (mapGrid is not null && mapGrid.IsBlocked(candidateX, candidateY)) continue;
                var candidateDistance = LegacyCombatMath.GetDistance(candidateX, candidateY, leader.PositionX, leader.PositionY);
                if (candidateDistance >= bestDistance) continue;
                bestDistance = candidateDistance;
                nextX = candidateX;
                nextY = candidateY;
                found = true;
            }
        }

        return found;
    }

    private bool TryGetSummonMapAttributes(LegacySummonedMob summon, Participant target, out byte summonAttribute, out byte targetAttribute)
    {
        summonAttribute = 0;
        targetAttribute = 0;
        participants.TryGetValue(summon.SummonerConnectionId, out var summoner);
        var hasConfiguredAttributes = mapGrid is not null || target.HasMapAttribute || summoner?.HasMapAttribute == true;
        if (!hasConfiguredAttributes) return false;

        summonAttribute = mapGrid?.GetAttribute(summon.PositionX, summon.PositionY) ?? summoner?.MapAttribute ?? 0;
        targetAttribute = mapGrid?.GetAttribute(target.PositionX, target.PositionY) ?? target.MapAttribute;
        return true;
    }

    /// <summary>Rebuilds one summon enemy list and selects its nearest visible target.</summary>
    public bool TrySelectSummonTarget(int summonConnectionId, out LegacySummonTargetSelection? selection)
    {
        lock (gate)
        {
            selection = null;
            if (!summons.TryGetValue(summonConnectionId, out var summon)) return false;

            var view = summon.MobSnapshot[LegacyAccountSnapshot.MobClanOffset] is 7 or 8 ? 16 : 12;
            var explicitTargets = NormalizeNpcEnemyList(summon.EnemyList);
            for (var index = 0; index < explicitTargets.Length; index++)
            {
                var targetId = explicitTargets[index];
                if (targetId <= 0 || targetId >= LegacyMobAiPlanner.MaximumMobId)
                    continue;
                if (!TryGetBattleTargetLocked(targetId, out var targetX, out var targetY, out var targetMob, out _) ||
                    targetId == summonConnectionId ||
                    targetId < LegacyMobAiPlanner.MaximumPlayerId &&
                    (LegacyMobCombatState.Read(targetMob).Rsv & 0x10) != 0 ||
                    Math.Abs(targetX - summon.PositionX) > view || Math.Abs(targetY - summon.PositionY) > view)
                {
                    explicitTargets[index] = 0;
                    continue;
                }

                var explicitDistance = LegacyCombatMath.GetDistance(summon.PositionX, summon.PositionY, targetX, targetY);
                summons[summonConnectionId] = summon with { EnemyList = explicitTargets };
                summonTargets[summonConnectionId] = targetId;
                selection = new LegacySummonTargetSelection(summonConnectionId, targetId, explicitDistance);
                return true;
            }

            if (!explicitTargets.SequenceEqual(summon.EnemyList))
            {
                summon = summon with { EnemyList = explicitTargets };
                summons[summonConnectionId] = summon;
            }

            var bestTarget = 0;
            var bestDistance = int.MaxValue;
            foreach (var participant in participants.Values)
            {
                if (participant.ConnectionId == summon.SummonerConnectionId || participant.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                    continue;

                var targetState = LegacyMobCombatState.Read(participant.Mob);
                if (targetState.CurrentScore.Hp <= 0 || (targetState.Rsv & 0x10) != 0)
                    continue;
                if (targetState.BaseScore.Level > LegacySkillCombatMath.LegacyMaxLevel && (participant.Mob[12] & 1) == 0)
                    continue;
                if (Math.Abs(participant.PositionX - summon.PositionX) > view || Math.Abs(participant.PositionY - summon.PositionY) > view)
                    continue;

                var summonClan = summon.MobSnapshot[LegacyAccountSnapshot.MobClanOffset];
                var targetClan = participant.Clan;
                if (summonClan >= LegacyClanRelations.Table.Length || targetClan < 0 || targetClan >= LegacyClanRelations.Table.Length || LegacyClanRelations.Table[summonClan][targetClan] != 0)
                    continue;

                var distance = LegacyCombatMath.GetDistance(summon.PositionX, summon.PositionY, participant.PositionX, participant.PositionY);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestTarget = participant.ConnectionId;
            }

            summonTargets[summonConnectionId] = bestTarget;
            selection = new LegacySummonTargetSelection(summonConnectionId, bestTarget, bestTarget == 0 ? null : bestDistance);
            return true;
        }
    }

    public bool TryGetSummonTarget(int summonConnectionId, out int targetConnectionId)
    {
        lock (gate) return summonTargets.TryGetValue(summonConnectionId, out targetConnectionId);
    }

    public bool TryGetSummonEnemyList(int summonConnectionId, out IReadOnlyList<int> enemyList)
    {
        lock (gate)
        {
            if (!summons.TryGetValue(summonConnectionId, out var summon))
            {
                enemyList = [];
                return false;
            }

            enemyList = NormalizeNpcEnemyList(summon.EnemyList);
            return true;
        }
    }

    /// <summary>
    /// Applies one autonomous summon attack to its server-selected target.
    /// This is the basic physical branch of TMSrv's GetAttack/BattleProcessor:
    /// the summon damage and target armor are authoritative, while the random
    /// factor is injectable so wire and combat tests remain deterministic.
    /// </summary>
    public LegacySummonAttackResult TryApplySummonAttack(
        int summonConnectionId,
        int randomFactor,
        out LegacySummonAttackOutcome? outcome,
        int parryRandomRoll = -1)
    {
        outcome = null;
        lock (gate)
        {
            if (!summons.TryGetValue(summonConnectionId, out var summon))
                return LegacySummonAttackResult.SummonNotFound;
            if (!summonTargets.TryGetValue(summonConnectionId, out var targetConnectionId) || targetConnectionId == 0)
                return LegacySummonAttackResult.NoTarget;
            if (!participants.TryGetValue(targetConnectionId, out var target))
                return LegacySummonAttackResult.TargetNotFound;
            if (target.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize || summon.MobSnapshot.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                return LegacySummonAttackResult.CombatStateUnavailable;

            var summonState = LegacyMobCombatState.Read(summon.MobSnapshot);
            var targetState = LegacyMobCombatState.Read(target.Mob);
            if (summonState.CurrentScore.Hp <= 0)
                return LegacySummonAttackResult.SummonNotAlive;
            if (targetState.CurrentScore.Hp <= 0)
                return LegacySummonAttackResult.TargetNotAlive;

            var distance = LegacyCombatMath.GetDistance(summon.PositionX, summon.PositionY, target.PositionX, target.PositionY);
            if (distance > 23)
                return LegacySummonAttackResult.OutOfRange;

            var effectiveRandomFactor = randomFactor >= 0
                ? randomFactor
                : System.Security.Cryptography.RandomNumberGenerator.GetInt32(99, 111);
            var damage = LegacySkillCombatMath.GetPhysicalDamage(
                summonState.CurrentScore.Damage,
                targetState.CurrentScore.Ac,
                combat: 0,
                effectiveRandomFactor);

            var mapBlocked = false;
            if (summon.MobSnapshot[LegacyAccountSnapshot.MobClanOffset] == 4 && TryGetSummonMapAttributes(summon, target, out var summonAttribute, out var targetAttribute))
            {
                damage = (3 * damage) / 10;
                mapBlocked = (summonAttribute & 0x01) != 0 || (summonAttribute & 0x40) == 0
                    || (targetAttribute & 0x01) != 0 || (targetAttribute & 0x40) == 0;
                if (mapBlocked) damage = 0;
            }

            // TMSrv resolves player parry before applying the clan-4 NPC reduction.
            if (!mapBlocked)
            {
                damage = ResolveLegacyParry(target.Mob, target.ClientEquipment, target.RuntimeParryAdjustment, summonState, targetState, itemData, skillId: -1, parryRandomRoll: parryRandomRoll, damage: damage);
                if (damage > 0 && summon.MobSnapshot[LegacyAccountSnapshot.MobClanOffset] == 4)
                    damage = (2 * damage) / 5;
                if (damage == 0) damage = 1;
            }

            var remainingHp = damage > 0 ? Math.Max(0, targetState.CurrentScore.Hp - damage) : targetState.CurrentScore.Hp;
            if (damage > 0)
                BinaryPrimitives.WriteInt32LittleEndian(target.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), remainingHp);
            outcome = new LegacySummonAttackOutcome(
                summonConnectionId,
                targetConnectionId,
                summon.PositionX,
                summon.PositionY,
                target.PositionX,
                target.PositionY,
                damage,
                remainingHp,
                target.Mob.ToArray(),
                TargetDied: remainingHp == 0);
            return LegacySummonAttackResult.Accepted;
        }
    }

    /// <summary>Runs one autonomous attack opportunity for every summon owned by a leader.</summary>
    public bool TryApplySummonAttacks(
        int leaderConnectionId,
        int randomFactor,
        out IReadOnlyList<LegacySummonAttackOutcome> outcomes,
        int parryRandomRoll = -1)
    {
        lock (gate)
        {
            outcomes = [];
            if (!participants.TryGetValue(leaderConnectionId, out var leader)) return false;

            var accepted = new List<LegacySummonAttackOutcome>();
            foreach (var summonId in leader.SummonIds.ToArray())
            {
                // The legacy enemy list is rebuilt as the summon gets a battle opportunity.
                if (!TrySelectSummonTarget(summonId, out _)) continue;
                if (TryApplySummonAttack(summonId, randomFactor, out var attack, parryRandomRoll) == LegacySummonAttackResult.Accepted && attack is not null)
                    accepted.Add(attack);
            }

            outcomes = accepted;
            return true;
        }
    }

    /// <summary>Runs one battle opportunity for every connected leader with summons.</summary>
    public bool TryApplyAllSummonAttacks(
        int randomFactor,
        out IReadOnlyList<LegacySummonAttackOutcome> outcomes,
        int parryRandomRoll = -1)
    {
        lock (gate)
        {
            var accepted = new List<LegacySummonAttackOutcome>();
            foreach (var leaderConnectionId in participants.Keys.ToArray())
            {
                if (!TryApplySummonAttacks(leaderConnectionId, randomFactor, out var leaderAttacks, parryRandomRoll)) continue;
                accepted.AddRange(leaderAttacks);
            }

            outcomes = accepted;
            return true;
        }
    }

    /// <summary>Runs one summon battle opportunity, applying the RouteType 5 Combat teleport before its physical attack.</summary>
    public bool TryApplyAllSummonBattleOpportunities(
        int randomFactor,
        out IReadOnlyList<LegacySummonBattleOpportunityOutcome> outcomes,
        int parryRandomRoll = -1)
    {
        lock (gate)
        {
            var accepted = new List<LegacySummonBattleOpportunityOutcome>();
            summonCombatAffectOpportunityPhase++;
            var processCombatSummonAffects = summonCombatAffectOpportunityPhase == 4;
            if (processCombatSummonAffects)
                summonCombatAffectOpportunityPhase = 0;

            foreach (var ownerConnectionId in participants.Keys.ToArray())
            {
                if (!participants.TryGetValue(ownerConnectionId, out var owner))
                    continue;

                foreach (var summonId in owner.SummonIds.ToArray())
                {
                    if (!summons.TryGetValue(summonId, out var summon) || !IsLiveSummonLocked(summon))
                        continue;
                    if (LegacyMobAiPlanner.IsRunequestLv6AreaDespawnCandidate(-1, summon.PositionX, summon.PositionY))
                    {
                        var recipients = Array.AsReadOnly(GetParticipantIdsInViewLocked(summon.PositionX, summon.PositionY));
                        if (!RemoveSummonLocked(summon.ConnectionId))
                            continue;
                        var despawn = LegacyNpcAiApplicationOutcome.Empty(LegacyMobAiDecisionKind.Despawn) with
                        {
                            Status = LegacyNpcAiApplyStatus.Applied,
                            PositionX = summon.PositionX,
                            PositionY = summon.PositionY,
                            Removal = new LegacyGeneratedNpcRemovalOutcome(summon.ConnectionId, recipients, RemoveType: 3)
                        };
                        accepted.Add(new LegacySummonBattleOpportunityOutcome(Movement: null, Attack: null, Despawn: despawn));
                        continue;
                    }

                    if (processCombatSummonAffects)
                    {
                        var expiry = TryProcessSummonCombatTypeTwentyFourLocked(summon);
                        if (expiry is not null)
                        {
                            accepted.Add(new LegacySummonBattleOpportunityOutcome(Movement: null, Attack: null, Despawn: expiry));
                            continue;
                        }

                        if (!summons.TryGetValue(summonId, out summon))
                            continue;

                        var summonAffectUpdate = TryProcessSummonTimerAffectsLocked(summon, out summon);
                        if (summonAffectUpdate is not null)
                            accepted.Add(new LegacySummonBattleOpportunityOutcome(
                                Movement: null,
                                Attack: null)
                            {
                                AffectUpdate = summonAffectUpdate,
                            });
                    }

                    // Target selection is the first operation of BattleProcessor; a summon without a live target stays idle.
                    if (!TrySelectSummonTarget(summonId, out var selection) || selection is null || selection.TargetConnectionId == 0)
                        continue;
                    if (!summons.TryGetValue(summonId, out summon))
                        continue;

                    if (summon.RouteType == 5)
                    {
                        if (!TryGetActiveRouteTypeFiveSummonerLocked(summon, out var summoner))
                            continue;

                        var distance = LegacyCombatMath.GetDistance(
                            summon.PositionX,
                            summon.PositionY,
                            summoner.PositionX,
                            summoner.PositionY);
                        if (distance >= 20)
                        {
                            if (TryTeleportRouteTypeFiveSummonForCombatLocked(summon, summoner, selection.TargetConnectionId, out var movement))
                                accepted.Add(new LegacySummonBattleOpportunityOutcome(movement, Attack: null));

                            // BattleProcessor returns after setting NextX/Y; even a blocked DoTeleport consumes this tick.
                            continue;
                        }
                    }

                    if (TryApplySummonAttack(summonId, randomFactor, out var attack, parryRandomRoll) == LegacySummonAttackResult.Accepted && attack is not null)
                        accepted.Add(new LegacySummonBattleOpportunityOutcome(Movement: null, attack));
                }
            }

            outcomes = accepted;
            return true;
        }
    }

    private LegacyNpcAiApplicationOutcome? TryProcessSummonCombatTypeTwentyFourLocked(LegacySummonedMob summon)
    {
        const int affectSizeInBytes = 8;
        const byte summonLifetimeAffectType = 24;
        const uint persistentAffectThreshold = 32_400_000;
        if (summon.AffectSnapshot.Length != LegacyAccountSnapshot.AffectStride)
            return null;

        var affect = summon.AffectSnapshot.ToArray();
        var affectChanged = false;
        for (var index = 0; index < affect.Length / affectSizeInBytes; index++)
        {
            var offset = index * affectSizeInBytes;
            if (affect[offset] != summonLifetimeAffectType)
                continue;

            var remainingTime = BinaryPrimitives.ReadUInt32LittleEndian(affect.AsSpan(offset + 4));
            if (remainingTime < persistentAffectThreshold)
                remainingTime = unchecked(remainingTime - 1);

            if (remainingTime == 0)
            {
                var recipients = Array.AsReadOnly(GetParticipantIdsInViewLocked(summon.PositionX, summon.PositionY));
                if (!RemoveSummonLocked(summon.ConnectionId))
                    return null;

                return LegacyNpcAiApplicationOutcome.Empty(LegacyMobAiDecisionKind.Despawn) with
                {
                    Status = LegacyNpcAiApplyStatus.Applied,
                    PositionX = summon.PositionX,
                    PositionY = summon.PositionY,
                    Removal = new LegacyGeneratedNpcRemovalOutcome(
                        summon.ConnectionId,
                        recipients,
                        RemoveType: 3),
                };
            }

            if (remainingTime != BinaryPrimitives.ReadUInt32LittleEndian(affect.AsSpan(offset + 4)))
            {
                BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(offset + 4), remainingTime);
                affectChanged = true;
            }
        }

        if (affectChanged)
            summons[summon.ConnectionId] = summon with { AffectSnapshot = affect };

        return null;
    }

    private LegacyNpcAiApplicationOutcome? TryProcessSummonTimerAffectsLocked(
        LegacySummonedMob summon,
        out LegacySummonedMob updatedSummon)
    {
        const int affectSizeInBytes = 8;
        const byte lifeAuraType = 17;
        const byte poisonType = 20;
        const byte thunderType = 22;
        const byte bestialAuraType = 23;
        const byte genericTimerOnlyAffectType = 32;
        // SkillData 246 uses Type 47; both C++ sources fall through to the generic affect timer.
        const byte bleedingAffectType = 47;
        const uint persistentAffectThreshold = 32_400_000;
        updatedSummon = summon;
        if (summon.MobSnapshot.Length < LegacyAccountSnapshot.CharacterStride ||
            summon.AffectSnapshot.Length != LegacyAccountSnapshot.AffectStride)
            return null;

        var mob = summon.MobSnapshot.ToArray();
        var affect = summon.AffectSnapshot.ToArray();
        var score = LegacyMobCombatState.Read(mob).CurrentScore;
        var hp = score.Hp;
        var damage = 0;
        var affectChanged = false;
        var scoreRefreshRequired = false;
        var hpUpdateRequired = false;
        var mountOwnerConnectionId = 0;
        LegacyItem? updatedMountItem = null;
        for (var index = 0; index < affect.Length / affectSizeInBytes; index++)
        {
            var offset = index * affectSizeInBytes;
            var affectType = affect[offset];
            if (affectType is not (lifeAuraType or poisonType or thunderType or bestialAuraType or genericTimerOnlyAffectType or bleedingAffectType))
                continue;

            affectChanged = true;
            if (affectType is lifeAuraType or poisonType)
            {
                var previousHp = hp;
                if (affectType == lifeAuraType)
                {
                    var affectValue = affect[offset + 1];
                    var affectLevel = BinaryPrimitives.ReadUInt16LittleEndian(affect.AsSpan(offset + 2));
                    hp += (affectLevel / 2) + affectValue;
                    if (hp < 1)
                        hp = 1;
                    if (hp > score.MaxHp)
                        hp = score.MaxHp;

                    if (hp != previousHp)
                    {
                        damage = hp - previousHp;
                        hpUpdateRequired = true;
                        scoreRefreshRequired = true;
                    }
                }
                else
                {
                    var learnedSkill = BinaryPrimitives.ReadInt32LittleEndian(
                        mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset));
                    var affectValue = 100;
                    var poisonDamageCap = 1_000;
                    if (summon.ConnectionId >= LegacyMobAiPlanner.MaximumPlayerId)
                    {
                        var learnedSkillDivisor = learnedSkill / LegacyMobAiPlanner.MaximumPlayerId;
                        affectValue -= learnedSkillDivisor;
                        poisonDamageCap = 10 * (100 - learnedSkillDivisor);
                    }

                    affectValue /= 10;
                    var hpLose = (int)((100 - affectValue) / 100.0);
                    var minimumRemainingHp = hp - poisonDamageCap;
                    hp = (int)((double)hp * hpLose);
                    if (hp < minimumRemainingHp)
                        hp = minimumRemainingHp;
                    if (hp < 1)
                        hp = 1;
                    if (hp > score.MaxHp)
                        hp = score.MaxHp;

                    damage = hp - previousHp;
                    hpUpdateRequired = true;
                    if (hp != previousHp)
                        scoreRefreshRequired = true;

                    (score with { Hp = hp }).Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
                    if (TryLinkMountHpLocked(summon, mob, out var linkedMountOwner, out var linkedMountItem))
                    {
                        mountOwnerConnectionId = linkedMountOwner;
                        updatedMountItem = linkedMountItem;
                    }
                }
            }

            var previousTime = BinaryPrimitives.ReadUInt32LittleEndian(affect.AsSpan(offset + 4));
            var remainingTime = previousTime;
            if (remainingTime < persistentAffectThreshold)
                remainingTime = unchecked(remainingTime - 1);

            if (remainingTime == 0)
            {
                affect.AsSpan(offset, affectSizeInBytes).Clear();
                scoreRefreshRequired = true;
            }
            else if (remainingTime != previousTime)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(offset + 4), remainingTime);
            }
        }

        if (!affectChanged)
            return null;

        if (hpUpdateRequired)
            (score with { Hp = hp }).Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));

        updatedSummon = summon with
        {
            MobSnapshot = mob,
            AffectSnapshot = affect,
        };
        summons[summon.ConnectionId] = updatedSummon;
        if (!scoreRefreshRequired && !hpUpdateRequired)
            return null;

        var recipients = Array.AsReadOnly(GetParticipantIdsInViewLocked(summon.PositionX, summon.PositionY));
        return LegacyNpcAiApplicationOutcome.Empty(LegacyMobAiDecisionKind.Idle) with
        {
            Status = LegacyNpcAiApplyStatus.Applied,
            PositionX = summon.PositionX,
            PositionY = summon.PositionY,
            AffectTick = new LegacyGeneratedNpcAffectTickOutcome(
                summon.ConnectionId,
                hp,
                damage,
                mob,
                affect.ToArray(),
                recipients,
                ScoreRefreshRequired: true)
            {
                HpUpdateRequired = hpUpdateRequired,
                MountOwnerConnectionId = mountOwnerConnectionId,
                UpdatedMountItem = updatedMountItem,
            },
        };
    }

    private static bool TryReturnSummonAffectUpdate(
        LegacyNpcAiApplicationOutcome? affectUpdate,
        out LegacyNpcAiApplicationOutcome outcome)
    {
        if (affectUpdate is null)
        {
            outcome = LegacyNpcAiApplicationOutcome.Empty(LegacyMobAiDecisionKind.Idle);
            return false;
        }

        outcome = affectUpdate;
        return true;
    }

    private bool TryGetActiveRouteTypeFiveSummonerLocked(LegacySummonedMob summon, out Participant summoner)
    {
        summoner = null!;
        if (!IsLiveSummonLocked(summon) ||
            summon.SummonerConnectionId is <= 0 or >= LegacyMobAiPlanner.MaximumPlayerId ||
            summon.LeaderConnectionId is <= 0 or >= LegacyMobAiPlanner.MaximumPlayerId ||
            !participants.TryGetValue(summon.SummonerConnectionId, out var activeSummoner) ||
            activeSummoner.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize ||
            LegacyMobCombatState.Read(activeSummoner.Mob).CurrentScore.Hp <= 0 ||
            !participants.TryGetValue(summon.LeaderConnectionId, out var leader) ||
            leader.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
            return false;

        var authorized = summon.LeaderConnectionId == summon.SummonerConnectionId ||
                         partyLeaders.TryGetValue(summon.SummonerConnectionId, out var partyLeaderId) &&
                         partyLeaderId == summon.LeaderConnectionId;
        if (!authorized)
            return false;

        summoner = activeSummoner;
        return true;
    }

    private bool TryTeleportRouteTypeFiveSummonForCombatLocked(
        LegacySummonedMob summon,
        Participant summoner,
        int targetConnectionId,
        out LegacyNpcAiApplicationOutcome outcome)
    {
        outcome = LegacyNpcAiApplicationOutcome.Empty(LegacyMobAiDecisionKind.Move);
        var destinationX = (int)summoner.PositionX;
        var destinationY = (int)summoner.PositionY;
        if (!LegacyMobGridSearch.TryFindEmpty(
                summon.ConnectionId,
                ref destinationX,
                ref destinationY,
                GetParticipantAtPosition,
                mapGrid is null ? static (_, _) => false : mapGrid.IsBlocked,
                checkCandidateTerrain: mapCollisionMode == LegacyMapCollisionMode.CandidateAware,
                maximumRadius: 9) ||
            destinationX == summon.PositionX && destinationY == summon.PositionY)
            return false;

        var fromX = summon.PositionX;
        var fromY = summon.PositionY;
        var originViewConnectionIds = GetParticipantIdsInViewLocked(fromX, fromY);
        summon = summon with { PositionX = checked((short)destinationX), PositionY = checked((short)destinationY) };
        summons[summon.ConnectionId] = summon;
        var movement = new LegacyGeneratedNpcMovementOutcome(
            summon.ConnectionId,
            fromX,
            fromY,
            summon.PositionX,
            summon.PositionY,
            Speed: 2,
            summon.MobSnapshot.ToArray(),
            summon.AffectSnapshot.ToArray(),
            Array.AsReadOnly(originViewConnectionIds),
            Array.AsReadOnly(GetParticipantIdsInViewLocked(summon.PositionX, summon.PositionY)),
            combatWorldState.BattleRoyaleState,
            Effect: 1);
        outcome = new LegacyNpcAiApplicationOutcome(
            LegacyNpcAiApplyStatus.Applied,
            LegacyMobAiDecisionKind.Move,
            summon.PositionX,
            summon.PositionY,
            LegacyNpcMode.Combat,
            targetConnectionId,
            summon.EnemyList.ToArray(),
            summon.PositionX,
            summon.PositionY,
            0,
            0,
            0,
            null,
            Movement: movement);
        return true;
    }

    public bool TryGetCombatState(int connectionId, out LegacyMobCombatState? state)
    {
        lock (gate)
        {
            state = null;
            if (!participants.TryGetValue(connectionId, out var participant) || participant.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                return false;

            state = LegacyMobCombatState.Read(participant.Mob);
            if (LegacyMobExtra.TryReadSecondaryLearnedSkill(participant.MobExtra, out var secondaryLearnedSkill))
                state = state with { SecondaryLearnedSkill = secondaryLearnedSkill };
            return true;
        }
    }

    /// <summary>Returns the authoritative STRUCT_MOB and current world position for account persistence.</summary>
    public bool TryGetCharacterSnapshot(int connectionId, out byte[]? mob, out short positionX, out short positionY)
        => TryGetCharacterSnapshot(connectionId, out mob, out positionX, out positionY, out _);

    public bool TryGetCharacterSnapshot(int connectionId, out byte[]? mob, out short positionX, out short positionY, out byte[]? mobExtra)
        => TryGetCharacterSnapshot(connectionId, out mob, out positionX, out positionY, out mobExtra, out _);

    public bool TryGetCharacterSnapshot(int connectionId, out byte[]? mob, out short positionX, out short positionY, out byte[]? mobExtra, out byte[]? affect)
        => TryGetCharacterSnapshot(connectionId, out mob, out positionX, out positionY, out mobExtra, out affect, out _);

    public bool TryGetCharacterSnapshot(
        int connectionId,
        out byte[]? mob,
        out short positionX,
        out short positionY,
        out byte[]? mobExtra,
        out byte[]? affect,
        out IReadOnlyList<LegacyItem>? clientEquipment)
    {
        lock (gate)
        {
            mob = null;
            mobExtra = null;
            affect = null;
            clientEquipment = null;
            positionX = 0;
            positionY = 0;
            if (!participants.TryGetValue(connectionId, out var participant) || participant.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                return false;

            SyncClientEquipmentFromLegacyMob(participant);
            mob = participant.Mob.ToArray();
            if (HasLegacyAffectType(participant.Affect, type: 3) &&
                participant.PursuitBaseResistances is { Length: >= 4 } pursuitBaseResistances)
                pursuitBaseResistances.AsSpan(0, 4).CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4));
            mobExtra = participant.MobExtra.ToArray();
            affect = participant.Affect.ToArray();
            clientEquipment = participant.ClientEquipment.ToArray();
            positionX = participant.PositionX;
            positionY = participant.PositionY;
            return true;
        }
    }

    /// <summary>Captures the live position, MOB clan byte, and combat state used by the early 7.69 Attack gates.</summary>
    public bool TryGetAttackState(int connectionId, out LegacyAttackState? state)
    {
        lock (gate)
        {
            state = null;
            if (!participants.TryGetValue(connectionId, out var participant) ||
                participant.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize ||
                participant.Mob.Length <= LegacyAccountSnapshot.MobClanOffset)
                return false;

            state = new LegacyAttackState(
                LegacyMobCombatState.Read(participant.Mob),
                participant.PositionX,
                participant.PositionY,
                participant.Mob[LegacyAccountSnapshot.MobClanOffset]);
            return true;
        }
    }

    /// <summary>Clears the first Type 28 affect from a class 3 player and rebuilds its audited score projection.</summary>
    public LegacyAttackHideCleanupResult TryClearFirstClassThreeHideAffect(
        int connectionId,
        out LegacyAttackHideCleanupOutcome? outcome)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyAttackHideCleanupResult.ParticipantNotFound;
            if (participant.Mob.Length < LegacyAccountSnapshot.CharacterStride ||
                participant.Affect.Length != LegacyAccountSnapshot.AffectStride ||
                participant.MobExtra.Length != LegacyAccountSnapshot.MobExtraStride)
                return LegacyAttackHideCleanupResult.CombatStateUnavailable;
            if (participant.Mob[LegacyAccountSnapshot.MobClassOffset] != 3)
                return LegacyAttackHideCleanupResult.NotApplicable;

            var affectOffset = -1;
            for (var offset = 0; offset < participant.Affect.Length; offset += 8)
            {
                if (participant.Affect[offset] != 28)
                    continue;

                affectOffset = offset;
                break;
            }
            if (affectOffset < 0)
                return LegacyAttackHideCleanupResult.NoAffect;
            if (itemData is null)
                return LegacyAttackHideCleanupResult.ItemDataUnavailable;

            var nextAffect = participant.Affect.ToArray();
            nextAffect.AsSpan(affectOffset, 8).Clear();
            LegacyCharacterLoginData projected;
            try
            {
                projected = ProjectPlayerScoreLocked(participant, participant.Mob, nextAffect, itemData);
            }
            catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException)
            {
                return LegacyAttackHideCleanupResult.ScoreProjectionUnavailable;
            }

            participant.Mob = projected.Mob;
            participant.Affect = projected.Affect;
            participant.MobExtra = projected.MobExtra;
            if (projected.ClientEquipment is not null)
                participant.ClientEquipment = projected.ClientEquipment.ToArray();

            outcome = new LegacyAttackHideCleanupOutcome(participant.Mob.ToArray(), participant.Affect.ToArray());
            return LegacyAttackHideCleanupResult.Cleared;
        }
    }

    /// <summary>Clears the first Type 24 Samaritan affect from a player and rebuilds its audited score projection.</summary>
    public LegacyAttackSamaritanCleanupResult TryClearFirstSamaritanAffect(
        int connectionId,
        out LegacyAttackSamaritanCleanupOutcome? outcome)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyAttackSamaritanCleanupResult.ParticipantNotFound;
            if (participant.Mob.Length < LegacyAccountSnapshot.CharacterStride ||
                participant.Affect.Length != LegacyAccountSnapshot.AffectStride ||
                participant.MobExtra.Length != LegacyAccountSnapshot.MobExtraStride)
                return LegacyAttackSamaritanCleanupResult.CombatStateUnavailable;

            var affectOffset = -1;
            for (var offset = 0; offset < participant.Affect.Length; offset += 8)
            {
                if (participant.Affect[offset] != 24)
                    continue;

                affectOffset = offset;
                break;
            }
            if (affectOffset < 0)
                return LegacyAttackSamaritanCleanupResult.NoAffect;
            if (itemData is null)
                return LegacyAttackSamaritanCleanupResult.ItemDataUnavailable;

            var nextAffect = participant.Affect.ToArray();
            nextAffect.AsSpan(affectOffset, 8).Clear();
            LegacyCharacterLoginData projected;
            try
            {
                projected = ProjectPlayerScoreLocked(participant, participant.Mob, nextAffect, itemData);
            }
            catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException)
            {
                return LegacyAttackSamaritanCleanupResult.ScoreProjectionUnavailable;
            }

            participant.Mob = projected.Mob;
            participant.Affect = projected.Affect;
            participant.MobExtra = projected.MobExtra;
            if (projected.ClientEquipment is not null)
                participant.ClientEquipment = projected.ClientEquipment.ToArray();

            outcome = new LegacyAttackSamaritanCleanupOutcome(participant.Mob.ToArray(), participant.Affect.ToArray());
            return LegacyAttackSamaritanCleanupResult.Cleared;
        }
    }

    /// <summary>Clears the first self Type 6 affect for a class 1 skill attempt and rebuilds its audited score projection.</summary>
    public LegacyClassOneType6CleanupResult TryClearFirstClassOneType6Affect(
        int connectionId,
        out LegacyClassOneType6CleanupOutcome? outcome)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyClassOneType6CleanupResult.ParticipantNotFound;
            if (participant.Mob.Length < LegacyAccountSnapshot.CharacterStride ||
                participant.Affect.Length != LegacyAccountSnapshot.AffectStride ||
                participant.MobExtra.Length != LegacyAccountSnapshot.MobExtraStride)
                return LegacyClassOneType6CleanupResult.CombatStateUnavailable;
            if (participant.Mob[LegacyAccountSnapshot.MobClassOffset] != 1)
                return LegacyClassOneType6CleanupResult.NotApplicable;

            var affectOffset = -1;
            for (var offset = 0; offset < participant.Affect.Length; offset += 8)
            {
                if (participant.Affect[offset] != 6)
                    continue;

                affectOffset = offset;
                break;
            }
            if (affectOffset < 0)
                return LegacyClassOneType6CleanupResult.NoAffect;
            if (itemData is null)
                return LegacyClassOneType6CleanupResult.ItemDataUnavailable;

            var nextAffect = participant.Affect.ToArray();
            nextAffect.AsSpan(affectOffset, 8).Clear();
            LegacyCharacterLoginData projected;
            try
            {
                projected = ProjectPlayerScoreLocked(participant, participant.Mob, nextAffect, itemData);
            }
            catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException)
            {
                return LegacyClassOneType6CleanupResult.ScoreProjectionUnavailable;
            }

            participant.Mob = projected.Mob;
            participant.Affect = projected.Affect;
            participant.MobExtra = projected.MobExtra;
            if (projected.ClientEquipment is not null)
                participant.ClientEquipment = projected.ClientEquipment.ToArray();

            outcome = new LegacyClassOneType6CleanupOutcome(participant.Mob.ToArray(), participant.Affect.ToArray());
            return LegacyClassOneType6CleanupResult.Cleared;
        }
    }

    /// <summary>Returns the authoritative city/tax pair used by autotrade start.</summary>
    public bool TryGetAutoTradeLocation(int connectionId, out int village, out int cityTax)
    {
        lock (gate)
        {
            village = -1;
            cityTax = LegacyAutoTradeLocationRules.DefaultCityTax;
            if (!participants.TryGetValue(connectionId, out var participant))
                return false;

            return LegacyAutoTradeLocationRules.TryResolve(
                participant.PositionX,
                participant.PositionY,
                guildZones?.CityTaxes,
                out village,
                out cityTax);
        }
    }

    /// <summary>Returns the legacy kill-mark/PK value currently held by a playing character.</summary>
    public bool TryGetCharacterPkPoint(int connectionId, out int pkPoint)
    {
        lock (gate)
        {
            pkPoint = 0;
            if (!participants.TryGetValue(connectionId, out var participant) || participant.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                return false;

            pkPoint = ReadLegacyPkPoint(participant.Mob);
            return true;
        }
    }

    private static bool SameAutoTradeSnapshot(LegacyAutoTradeSnapshot left, LegacyAutoTradeSnapshot right) =>
        left.ConnectionId == right.ConnectionId &&
        left.PositionX == right.PositionX && left.PositionY == right.PositionY &&
        string.Equals(left.Title, right.Title, StringComparison.Ordinal) && left.Tax == right.Tax &&
        left.Items.SequenceEqual(right.Items) &&
        left.CarryPositions.SequenceEqual(right.CarryPositions) &&
        left.Prices.SequenceEqual(right.Prices);

    /// <summary>
    /// Evaluates the target-cell map restrictions from the legacy action handler.
    /// This method is deliberately non-mutating: recall and its client notification
    /// require a separate wire/position step in the listener.
    /// </summary>
    public LegacyMovementMapRestriction EvaluateMovementMapTarget(int connectionId, short targetX, short targetY)
    {
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant) || (participant.PositionX == targetX && participant.PositionY == targetY))
                return LegacyMovementMapRestriction.None;

            if (mapGrid is null && !participant.HasMapAttribute)
                return LegacyMovementMapRestriction.None;

            var mapAttribute = mapGrid?.GetAttribute(targetX, targetY) ?? participant.MapAttribute;
            var level = participant.Mob.Length >= LegacyAccountSnapshot.MobCurrentScoreOffset + LegacyScore.SizeInBytes
                ? LegacyMobCombatState.Read(participant.Mob).CurrentScore.Level
                : 0;
            return LegacyMovementMapRules.GetRestriction(mapAttribute, level, participant.ClassMaster, participant.GuildId, targetX, targetY, guildZones);
        }
    }

    /// <summary>
    /// Applies the legacy <c>DoRecall</c> position change after a movement map gate.
    /// The caller owns the client frame and any user-facing notice.
    /// </summary>
    public bool TryRecallForMovementRestriction(
        int connectionId,
        out LegacyMovementRecallOutcome? outcome,
        int cityRandomX = -1,
        int cityRandomY = -1,
        int newbieRandomX = -1,
        int newbieRandomY = -1)
    {
        var recalled = TryRecallForMovementRestrictionWithViews(
            connectionId,
            out var transition,
            cityRandomX,
            cityRandomY,
            newbieRandomX,
            newbieRandomY);
        outcome = transition?.Recall;
        return recalled;
    }

    public bool TryRecallForMovementRestrictionWithViews(
        int connectionId,
        out LegacyRecallTransition? transition,
        int cityRandomX = -1,
        int cityRandomY = -1,
        int newbieRandomX = -1,
        int newbieRandomY = -1)
    {
        lock (gate)
        {
            transition = null;
            if (!participants.TryGetValue(connectionId, out var participant)
                || participant.Mob.Length < LegacyAccountSnapshot.MobCurrentScoreOffset + LegacyScore.SizeInBytes)
                return false;

            var state = LegacyMobCombatState.Read(participant.Mob);
            return TryCaptureRecallTransitionLocked(
                participant,
                state,
                out transition,
                cityRandomX,
                cityRandomY,
                newbieRandomX,
                newbieRandomY);
        }
    }

    public bool TryRecallForActionOrigin(
        int connectionId,
        out LegacyMovementRecallOutcome? outcome,
        int cityRandomX = -1,
        int cityRandomY = -1)
    {
        var recalled = TryRecallForActionOriginWithViews(connectionId, out var transition, cityRandomX, cityRandomY);
        outcome = transition?.Recall;
        return recalled;
    }

    public bool TryRecallForActionOriginWithViews(
        int connectionId,
        out LegacyRecallTransition? transition,
        int cityRandomX = -1,
        int cityRandomY = -1)
    {
        lock (gate)
        {
            transition = null;
            if (!participants.TryGetValue(connectionId, out var participant) ||
                participant.Mob.Length < LegacyAccountSnapshot.MobCurrentScoreOffset + LegacyScore.SizeInBytes ||
                participant.Mob.Length <= LegacyAccountSnapshot.MobClanOffset)
                return false;

            var state = LegacyMobCombatState.Read(participant.Mob);
            if (!LegacyActionOriginRecallRules.ShouldRecall(
                participant.PositionX,
                participant.PositionY,
                participant.ClassMaster,
                state.CurrentScore.Level,
                participant.Mob[LegacyAccountSnapshot.MobClanOffset]))
                return false;

            return TryCaptureRecallTransitionLocked(
                participant,
                state,
                out transition,
                cityRandomX,
                cityRandomY,
                newbieRandomX: -1,
                newbieRandomY: -1);
        }
    }

    private bool TryCaptureRecallTransitionLocked(
        Participant participant,
        LegacyMobCombatState state,
        out LegacyRecallTransition? transition,
        int cityRandomX,
        int cityRandomY,
        int newbieRandomX,
        int newbieRandomY)
    {
        var fromX = participant.PositionX;
        var fromY = participant.PositionY;
        var originViewIds = GetParticipantIdsInViewLocked(fromX, fromY);
        var originItems = GetItemsInViewLocked(fromX, fromY);
        var (toX, toY) = GetLegacyPosition(
            participant.ConnectionId,
            participant.GuildId,
            participant.ClassMaster,
            state,
            cityRandomX,
            cityRandomY,
            newbieRandomX,
            newbieRandomY,
            newbieOriginX: 2112,
            newbieOriginY: 2042);
        participant.PositionX = toX;
        participant.PositionY = toY;
        var destinationViewIds = GetParticipantIdsInViewLocked(toX, toY);
        var destinationItems = GetItemsInViewLocked(toX, toY);
        var recall = new LegacyMovementRecallOutcome(fromX, fromY, toX, toY);
        transition = new LegacyRecallTransition(
            participant.ConnectionId,
            recall,
            Array.AsReadOnly(originViewIds),
            Array.AsReadOnly(destinationViewIds),
            Array.AsReadOnly(originItems),
            Array.AsReadOnly(destinationItems));
        return true;
    }

    /// <summary>
    /// Reproduces the carry-slot mutation and trade/block gates from 7.69 <c>Exec_MSG_DeleteItem</c>.
    /// Unlike the reference handler, the requested item index must match the authoritative slot,
    /// preventing a stale client request from deleting a replacement item.
    /// </summary>
    public LegacyDeleteItemResult TryDeleteCarryItem(int connectionId, int slot, int requestedItemIndex, bool accountBlocked = false)
        => TryDeleteCarryItem(connectionId, slot, requestedItemIndex, out _, accountBlocked);

    public LegacyDeleteItemResult TryDeleteCarryItem(int connectionId, int slot, int requestedItemIndex, out int cancelledTradeOpponentId, bool accountBlocked = false)
    {
        lock (gate)
        {
            cancelledTradeOpponentId = 0;
            if (tradeCloseReservations.ContainsKey(connectionId)) return LegacyDeleteItemResult.TradeClosePending;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyDeleteItemResult.ParticipantNotFound;
            if (participant.Mob.Length < LegacyAccountSnapshot.MobCarryOffset + (LegacyAccountSnapshot.MobCarryCount * LegacyItem.SizeInBytes))
                return LegacyDeleteItemResult.CombatStateUnavailable;
            if (slot < 0 || slot >= LegacyAccountSnapshot.MobCarryCount - 4)
                return LegacyDeleteItemResult.InvalidSlot;
            if (requestedItemIndex <= 0 || requestedItemIndex >= LegacyItemDataTable.MaxItemIndex)
                return LegacyDeleteItemResult.InvalidItemIndex;

            if (tradeStates.ContainsKey(connectionId))
            {
                cancelledTradeOpponentId = tradeStates[connectionId].OpponentId;
                ClearTradePairLocked(connectionId);
                return LegacyDeleteItemResult.TradeCancelled;
            }
            if (accountBlocked)
                return LegacyDeleteItemResult.AccountBlocked;

            var current = ReadCarryItem(participant, slot);
            if (current.Index == 0)
                return LegacyDeleteItemResult.SourceEmpty;
            if (current.Index != requestedItemIndex)
                return LegacyDeleteItemResult.ItemChanged;

            WriteCarryItem(participant, slot, default);
            return LegacyDeleteItemResult.Accepted;
        }
    }

    /// <summary>
    /// Applies the buyer half of an autotrade plan to the connected MOB. The
    /// current coin and destination slot are compared again while holding the
    /// world lock, so a stale request cannot overwrite a newer inventory state.
    /// </summary>
    public LegacyAutoTradeBuyerPurchaseResult TryApplyAutoTradeBuyerPurchase(
        int connectionId,
        LegacyAutoTradePurchasePlan plan,
        out LegacyAutoTradeBuyerPurchaseOutcome? outcome)
        => TryApplyAutoTradeBuyerPurchase(connectionId, plan, expectedMob: null, out outcome);

    /// <summary>
    /// Applies the buyer half only if the caller's full MOB snapshot is still
    /// current. The full compare prevents a later rollback from erasing an
    /// unrelated inventory or combat mutation made between read and commit.
    /// </summary>
    public LegacyAutoTradeBuyerPurchaseResult TryApplyAutoTradeBuyerPurchase(
        int connectionId,
        LegacyAutoTradePurchasePlan plan,
        ReadOnlyMemory<byte>? expectedMob,
        out LegacyAutoTradeBuyerPurchaseOutcome? outcome)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyAutoTradeBuyerPurchaseResult.ParticipantNotFound;
            if (expectedMob is not null && !participant.Mob.AsSpan().SequenceEqual(expectedMob.Value.Span))
                return LegacyAutoTradeBuyerPurchaseResult.StateChanged;

            var validation = LegacyAutoTradeRuntimePurchaseRules.ValidateBuyerPlan(plan, participant.Mob, out _);
            if (validation != LegacyAutoTradeBuyerPurchaseResult.Accepted)
                return validation;

            var previousMob = participant.Mob.ToArray();
            WriteCarryItem(participant, plan.BuyerDestinationSlot, plan.PurchasedItem);
            BinaryPrimitives.WriteInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), plan.BuyerCoin);
            participant.Coin = plan.BuyerCoin;
            outcome = new LegacyAutoTradeBuyerPurchaseOutcome(
                connectionId,
                plan.BuyerDestinationSlot,
                plan.PurchasedItem,
                plan.BuyerCoin,
                participant.Mob.ToArray(),
                previousMob);
            return LegacyAutoTradeBuyerPurchaseResult.Accepted;
        }
    }

    /// <summary>Restores the buyer snapshot when a later persistence gate rejects the sale.</summary>
    public bool TryRollbackAutoTradeBuyerPurchase(LegacyAutoTradeBuyerPurchaseOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        lock (gate)
        {
            if (!participants.TryGetValue(outcome.ConnectionId, out var participant) ||
                participant.Mob.Length != outcome.PreviousMob.Length ||
                !participant.Mob.AsSpan().SequenceEqual(outcome.MobSnapshot))
                return false;

            outcome.PreviousMob.AsSpan().CopyTo(participant.Mob);
            participant.Coin = BinaryPrimitives.ReadInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset));
            return true;
        }
    }

    /// <summary>
    /// Atomically charges skill 85's source-defined Gold fee before the attack mana gate.
    /// On success the caller sends <c>UpdateEtc</c> before attempting mana, matching both legacy handlers.
    /// </summary>
    public LegacySkillCoinResult TryConsumeGoldenShieldCoin(
        int connectionId,
        LegacySkillDefinition skill,
        out int coinSpent,
        out byte[]? mobSnapshot)
    {
        ArgumentNullException.ThrowIfNull(skill);
        if (skill.Id != LegacyGoldenShieldCoinMath.SkillId)
            throw new ArgumentException("Only MSG_Attack skill 85 has this Gold fee.", nameof(skill));

        lock (gate)
        {
            coinSpent = 0;
            mobSnapshot = null;
            if (!participants.TryGetValue(connectionId, out var participant) ||
                participant.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                return LegacySkillCoinResult.ParticipantNotFound;

            var state = LegacyMobCombatState.Read(participant.Mob);
            coinSpent = LegacyGoldenShieldCoinMath.GetCost(skill, state);
            var currentCoin = BinaryPrimitives.ReadInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset));
            if (currentCoin < coinSpent)
                return LegacySkillCoinResult.InsufficientCoin;

            var remainingCoin = currentCoin - coinSpent;
            BinaryPrimitives.WriteInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), remainingCoin);
            participant.Coin = remainingCoin;
            mobSnapshot = participant.Mob.ToArray();
            return LegacySkillCoinResult.Accepted;
        }
    }

    /// <summary>Removes one non-leader party member only while that player remains inside the Battle Royale arena.</summary>
    public bool TryRemoveBattleRoyalePartyMember(
        int connectionId,
        int arenaX1,
        int arenaY1,
        int arenaX2,
        int arenaY2,
        out LegacyBattleRoyalePartyMemberRemovalPlan? plan)
    {
        lock (gate)
        {
            plan = null;
            if (arenaX1 > arenaX2 || arenaY1 > arenaY2 ||
                !participants.TryGetValue(connectionId, out var participant) ||
                participant.PositionX < arenaX1 || participant.PositionX > arenaX2 ||
                participant.PositionY < arenaY1 || participant.PositionY > arenaY2 ||
                !partyLeaders.TryGetValue(connectionId, out var leaderConnectionId) ||
                leaderConnectionId == connectionId)
                return false;

            var members = GetPartyMemberIdsLocked(leaderConnectionId);
            if (!members.Contains(connectionId))
                return false;

            partyLeaders.Remove(connectionId);
            var removedSummons = new List<LegacyBattleRoyaleSummonDespawnPlan>();
            foreach (var summonId in participant.SummonIds.ToArray())
            {
                if (!summons.TryGetValue(summonId, out var summon))
                    continue;

                var notifiedConnectionIds = GetParticipantIdsInViewLocked(summon.PositionX, summon.PositionY);
                if (RemoveSummonLocked(summonId))
                    removedSummons.Add(new LegacyBattleRoyaleSummonDespawnPlan(
                        summonId,
                        Array.AsReadOnly(notifiedConnectionIds)));
            }

            plan = new LegacyBattleRoyalePartyMemberRemovalPlan(
                new PartyRemovalPlan(
                    leaderConnectionId,
                    connectionId,
                    Array.AsReadOnly(members.ToArray()),
                    Disbanded: false),
                Array.AsReadOnly(removedSummons.ToArray()));
            return true;
        }
    }

    /// <summary>
    /// Atomically applies the server-calculated mana cost to the in-world MOB.
    /// The caller must validate the skill first; packet-declared MP is never used.
    /// </summary>
    public LegacySkillManaResult TryConsumeSkillMana(int connectionId, LegacySkillDefinition skill, out LegacyMobCombatState? state, out byte[]? mobSnapshot)
    {
        return TryConsumeSkillMana(connectionId, skill, out state, out mobSnapshot, out _);
    }

    public LegacySkillManaResult TryConsumeSkillMana(int connectionId, LegacySkillDefinition skill, out LegacyMobCombatState? state, out byte[]? mobSnapshot, out int requestedMana)
    {
        ArgumentNullException.ThrowIfNull(skill);
        lock (gate)
        {
            state = null;
            mobSnapshot = null;
            requestedMana = 0;
            if (!participants.TryGetValue(connectionId, out var participant) || participant.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                return LegacySkillManaResult.ParticipantNotFound;

            var result = TryConsumeSkillManaLocked(
                participant,
                skill,
                specialOverride: null,
                applyMagicMirror: true,
                out state,
                out mobSnapshot,
                out requestedMana);
            if (result != LegacySkillManaResult.Accepted)
            {
                state = null;
                mobSnapshot = null;
                requestedMana = 0;
            }
            return result;
        }
    }

    private LegacySkillManaResult TryConsumeSkillManaLocked(
        Participant participant,
        LegacySkillDefinition skill,
        int? specialOverride,
        bool applyMagicMirror,
        out LegacyMobCombatState? state,
        out byte[]? mobSnapshot,
        out int requestedMana)
    {
        state = null;
        mobSnapshot = null;
        requestedMana = 0;
        if (participant.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
            return LegacySkillManaResult.ParticipantNotFound;

        var before = LegacyMobCombatState.Read(participant.Mob);
        var special = specialOverride ?? LegacySkillCombatMath.GetSkillSpecial(skill, before);
        var manaSpent = LegacySkillCombatMath.GetManaSpent(skill, before, special);
        var hasMagicMirror = applyMagicMirror && before.CharacterClass == 0 &&
            LegacyMobExtra.TryReadSecondaryLearnedSkill(participant.MobExtra, out var secondarySkills) &&
            (secondarySkills & LegacyMobExtra.MagicMirrorLearnedBit) != 0;
        requestedMana = participant.RequestedMana;
        // 7.69/W2PP only test `Mp - ManaSpent < 0`: a negative cost (SaveMana > 100) is accepted and raises MP.
        if (before.CurrentMana - manaSpent < 0)
        {
            state = before;
            mobSnapshot = participant.Mob.ToArray();
            return LegacySkillManaResult.InsufficientMana;
        }

        // SetReqMp, called by both sources after the debit, caps CurrentScore.Mp at MaxMp.
        var newMana = before.CurrentMana - manaSpent;
        if (newMana > before.CurrentScore.MaxMp)
            newMana = before.CurrentScore.MaxMp;
        participant.RequestedMana = Math.Max(0, Math.Max(participant.RequestedMana - manaSpent, newMana));

        // TMProject 7.69 checks sufficiency for the base cost first, then charges
        // ManaSpent / 2 to class 0 with SecLearnedSkill bit 0x100. The second
        // debit is not guarded by another insufficiency check.
        if (hasMagicMirror)
        {
            var mirrorManaSpent = manaSpent / 2;
            newMana -= mirrorManaSpent;
            if (newMana > before.CurrentScore.MaxMp)
                newMana = before.CurrentScore.MaxMp;
            participant.RequestedMana = Math.Max(0, Math.Max(participant.RequestedMana - mirrorManaSpent, newMana));
        }

        BinaryPrimitives.WriteInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentMpOffset), newMana);
        state = LegacyMobCombatState.Read(participant.Mob);
        mobSnapshot = participant.Mob.ToArray();
        requestedMana = participant.RequestedMana;
        return LegacySkillManaResult.Accepted;
    }

    /// <summary>
    /// Atomically consumes a fixed legacy mana cost from CurrentScore.Mp.
    /// Unlike skill attacks, Action3 leaves the separately tracked ReqMp unchanged in 7.69.
    /// </summary>
    public LegacySkillManaResult TryConsumeFixedMana(int connectionId, int manaSpent, out LegacyMobCombatState? state, out byte[]? mobSnapshot, out int requestedMana)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(manaSpent);
        lock (gate)
        {
            state = null;
            mobSnapshot = null;
            requestedMana = 0;
            if (!participants.TryGetValue(connectionId, out var participant) || participant.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                return LegacySkillManaResult.ParticipantNotFound;

            var before = LegacyMobCombatState.Read(participant.Mob);
            if (before.CurrentMana < manaSpent)
                return LegacySkillManaResult.InsufficientMana;

            var newMana = before.CurrentMana - manaSpent;
            BinaryPrimitives.WriteInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentMpOffset), newMana);
            state = LegacyMobCombatState.Read(participant.Mob);
            mobSnapshot = participant.Mob.ToArray();
            requestedMana = participant.RequestedMana;
            return LegacySkillManaResult.Accepted;
        }
    }

    /// <summary>
    /// Captures the attacker MOB for a physical MSG_Attack (SkillIndex -1). 7.69 skips its mana block for
    /// physical attacks, so CurrentScore.Mp and ReqMp are copied unchanged and no mana gate is applied.
    /// </summary>
    public bool TryCapturePhysicalAttackSnapshot(int connectionId, out byte[]? mobSnapshot, out int requestedMana)
    {
        lock (gate)
        {
            mobSnapshot = null;
            requestedMana = 0;
            if (!participants.TryGetValue(connectionId, out var participant) || participant.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                return false;

            mobSnapshot = participant.Mob.ToArray();
            requestedMana = participant.RequestedMana;
            return true;
        }
    }

    public bool TryGetResourceState(int connectionId, out LegacyMobCombatState? state, out int requestedHp, out int requestedMp)
    {
        lock (gate)
        {
            state = null;
            requestedHp = 0;
            requestedMp = 0;
            if (!participants.TryGetValue(connectionId, out var participant) || participant.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                return false;

            state = LegacyMobCombatState.Read(participant.Mob);
            requestedHp = Math.Max(participant.RequestedHp, state.CurrentScore.Hp);
            requestedMp = participant.RequestedMana;
            return true;
        }
    }

    public bool TryApplyAttackActionBatch(
        LegacyAttackActionBatchRequest request,
        out LegacyAttackActionBatchOutcome? outcome)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Slots);
        if (request.Slots.Count is < 1 or > 13)
            throw new ArgumentOutOfRangeException(nameof(request), "An attack action batch must contain between 1 and 13 wire slots.");

        var previousSlotIndex = -1;
        foreach (var slot in request.Slots)
        {
            if (slot is null)
                throw new ArgumentException("Attack action batch slots cannot contain null entries.", nameof(request));
            if (slot.SlotIndex is < 0 or > 12 || slot.SlotIndex <= previousSlotIndex)
                throw new ArgumentException("Attack action batch slots must have unique, ascending wire indexes from 0 through 12.", nameof(request));
            if (slot.Kind is not LegacyAttackActionSlotKind.Physical and not LegacyAttackActionSlotKind.Skill)
                throw new ArgumentOutOfRangeException(nameof(request), "Attack action batch slots require a known physical or skill kind.");
            if (slot.Kind == LegacyAttackActionSlotKind.Skill && request.Skill is null)
                throw new ArgumentException("Skill action slots require an authoritative skill definition.", nameof(request));

            previousSlotIndex = slot.SlotIndex;
        }

        var slotOutcomes = new List<LegacyAttackActionBatchSlotOutcome>(request.Slots.Count);
        var secondaryActions = new List<LegacyAttackActionSecondaryOutcome>();
        var spawnedNpcs = new List<LegacyAttackActionBatchSpawnedNpc>();
        var viewRemovalEffects = new List<LegacyAttackViewRemovalEffect>();
        var wireSlotOverrides = new List<LegacyPhysicalAttackBatchWireSlotOverride>();
        var actionState = new LegacyPhysicalAttackBatchActionState(request.ClientProgress);
        var maximumHealingExperience = 0;
        lock (gate)
        {
            var judgmentDivineStartingHp = 0;
            if (request.Skill is { Id: 30 } &&
                participants.TryGetValue(request.AttackerConnectionId, out var judgmentDivineCaster) &&
                judgmentDivineCaster.Mob.Length >= WorldHubLegacyOffsets.LegacyAccountMobSize)
                judgmentDivineStartingHp = LegacyMobCombatState.Read(judgmentDivineCaster.Mob).CurrentScore.Hp;

            int? supportSkillTargetCount = null;
            if (request.Skill is { Id: 41 or 44 } supportSkill &&
                participants.TryGetValue(request.AttackerConnectionId, out var supportSkillCaster) &&
                supportSkillCaster.Mob.Length >= LegacyAccountSnapshot.CharacterStride)
            {
                var casterState = LegacyMobCombatState.Read(supportSkillCaster.Mob);
                supportSkillTargetCount = LegacySupportSkillTargetLimit.GetTargetCount(
                    supportSkill.Id,
                    LegacySkillCombatMath.GetSkillSpecial(supportSkill, casterState));
            }

            int? abortSlotIndex = null;
            foreach (var slot in request.Slots)
            {
                if (actionState.IsReservedWireSlot(slot.SlotIndex))
                {
                    slotOutcomes.Add(slot.Kind == LegacyAttackActionSlotKind.Physical
                        ? new LegacyAttackActionBatchSlotOutcome(slot.SlotIndex, slot.Kind, LegacyPhysicalAttackResult.Accepted)
                        : new LegacyAttackActionBatchSlotOutcome(slot.SlotIndex, slot.Kind));
                    continue;
                }

                if (IsAttackTargetBlockedByNpcHeadAreaLocked(slot.TargetConnectionId))
                {
                    wireSlotOverrides.Add(new LegacyPhysicalAttackBatchWireSlotOverride(slot.SlotIndex, 0, 0));
                    slotOutcomes.Add(slot.Kind == LegacyAttackActionSlotKind.Physical
                        ? new LegacyAttackActionBatchSlotOutcome(slot.SlotIndex, slot.Kind, LegacyPhysicalAttackResult.NpcHeadAreaBlocked)
                        : new LegacyAttackActionBatchSlotOutcome(slot.SlotIndex, slot.Kind, SkillResult: LegacySkillAttackResult.NpcHeadAreaBlocked));
                    continue;
                }

                if (ShouldClearDeadTargetInAttackLocked(slot.TargetConnectionId, request.Skill?.Id ?? -1))
                {
                    wireSlotOverrides.Add(new LegacyPhysicalAttackBatchWireSlotOverride(slot.SlotIndex, 0, 0));
                    slotOutcomes.Add(slot.Kind == LegacyAttackActionSlotKind.Physical
                        ? new LegacyAttackActionBatchSlotOutcome(slot.SlotIndex, slot.Kind, LegacyPhysicalAttackResult.TargetNotAlive)
                        : new LegacyAttackActionBatchSlotOutcome(slot.SlotIndex, slot.Kind, SkillResult: LegacySkillAttackResult.TargetNotAlive));
                    if (slot.Kind == LegacyAttackActionSlotKind.Skill && request.Skill?.InstanceType == 9)
                        break;
                    continue;
                }

                if (ShouldClearMerchantTargetInAttackLocked(slot.TargetConnectionId))
                {
                    wireSlotOverrides.Add(new LegacyPhysicalAttackBatchWireSlotOverride(slot.SlotIndex, 0, 0));
                    slotOutcomes.Add(slot.Kind == LegacyAttackActionSlotKind.Physical
                        ? new LegacyAttackActionBatchSlotOutcome(slot.SlotIndex, slot.Kind, LegacyPhysicalAttackResult.TargetMerchantBlocked)
                        : new LegacyAttackActionBatchSlotOutcome(slot.SlotIndex, slot.Kind, SkillResult: LegacySkillAttackResult.TargetMerchantBlocked));
                    continue;
                }

                var skillIndex = request.SkillIndex >= 0 ? request.SkillIndex : request.Skill?.Id ?? -1;
                if (ShouldRemoveOutOfViewTargetInAttackLocked(
                        request.AttackerConnectionId,
                        slot.TargetConnectionId,
                        skillIndex))
                {
                    viewRemovalEffects.Add(new LegacyAttackViewRemovalEffect(slot.SlotIndex, slot.TargetConnectionId));
                    slotOutcomes.Add(slot.Kind == LegacyAttackActionSlotKind.Physical
                        ? new LegacyAttackActionBatchSlotOutcome(slot.SlotIndex, slot.Kind, LegacyPhysicalAttackResult.TargetOutOfView)
                        : new LegacyAttackActionBatchSlotOutcome(slot.SlotIndex, slot.Kind, SkillResult: LegacySkillAttackResult.TargetOutOfView));
                    continue;
                }

                if (slot.Kind == LegacyAttackActionSlotKind.Skill &&
                    supportSkillTargetCount is int targetCount &&
                    (slot.SkillTargetIndex ?? slot.SlotIndex) >= targetCount)
                    continue;

                if (slot.Kind == LegacyAttackActionSlotKind.Skill)
                {
                    if (request.Skill!.Id == 98)
                    {
                        // TMProject 7.69 Book Vinha reads the target grid coordinates, attempts CreateMob("Vinha", ..., "npc", 3),
                        // then ends the target loop. Its map check compares a value masked by 0x40 with 1, so it never rejects.
                        var spawnResult = LegacySkillAttackResult.Accepted;
                        if (!participants.TryGetValue(request.AttackerConnectionId, out var vineAttacker))
                        {
                            spawnResult = LegacySkillAttackResult.ParticipantNotFound;
                        }
                        else if (vineAttacker.Mob.Length < LegacyAccountSnapshot.CharacterStride)
                        {
                            spawnResult = LegacySkillAttackResult.CombatStateUnavailable;
                        }
                        else if (LegacyMobCombatState.Read(vineAttacker.Mob).CurrentScore.Hp <= 0)
                        {
                            spawnResult = LegacySkillAttackResult.AttackerNotAlive;
                        }

                        if (spawnResult == LegacySkillAttackResult.Accepted &&
                            request.TargetX is ushort targetX && request.TargetY is ushort targetY &&
                            targetX < LegacyMapGrid.HeightWidth &&
                            targetY < LegacyMapGrid.HeightHeight &&
                            TrySpawnNamedNpcLocked("Vinha", checked((short)targetX), checked((short)targetY), routeType: 3, out var spawnedVinha) &&
                            spawnedVinha is not null)
                        {
                            spawnedNpcs.Add(new LegacyAttackActionBatchSpawnedNpc(slot.SlotIndex, spawnedVinha));
                        }

                        slotOutcomes.Add(new LegacyAttackActionBatchSlotOutcome(
                            slot.SlotIndex,
                            LegacyAttackActionSlotKind.Skill,
                            SkillResult: spawnResult));
                        break;
                    }

                    if (request.Skill!.Id == LegacyDivineBlessing.SkillId)
                    {
                        var blessingResult = TryApplyDivineBlessingParentSlotLocked(request, slot, secondaryActions);
                        slotOutcomes.Add(new LegacyAttackActionBatchSlotOutcome(
                            slot.SlotIndex,
                            LegacyAttackActionSlotKind.Skill,
                            SkillResult: blessingResult));
                        continue;
                    }

                    var skillRolls = slot.SkillRolls;
                    var skillResult = TryApplySkillAttackLocked(
                        request.AttackerConnectionId,
                        slot.TargetConnectionId,
                        request.Skill!,
                        request.ItemData,
                        request.Weather,
                        skillRolls?.RandomFactor ?? -1,
                        out var skillOutcome,
                        deferDeathResolution: true,
                        applySkillEffects: request.ApplySkillEffects,
                        affectRandomFactor: skillRolls?.AffectRandomFactor ?? -1,
                        instanceRandomFactor: skillRolls?.InstanceRandomFactor ?? -1,
                        burnRandomFactor: skillRolls?.BurnRandomFactor ?? -1,
                        attackMissRoll: skillRolls?.AttackMissRoll ?? -1,
                        resurrectionRoll: skillRolls?.ResurrectionRoll ?? -1,
                        resurrectionHpRoll: skillRolls?.ResurrectionHpRoll ?? -1,
                        recallCityRandomX: skillRolls?.RecallCityRandomX ?? -1,
                        recallCityRandomY: skillRolls?.RecallCityRandomY ?? -1,
                        recallNewbieRandomX: skillRolls?.RecallNewbieRandomX ?? -1,
                        recallNewbieRandomY: skillRolls?.RecallNewbieRandomY ?? -1,
                        specialDropRoll: skillRolls?.SpecialDropRoll ?? -1,
                        specialItemRoll: skillRolls?.SpecialItemRoll ?? -1,
                        eventDropRoll: skillRolls?.EventDropRoll ?? -1,
                        runeRoll: skillRolls?.RuneRoll ?? -1,
                        runeChanceRoll: skillRolls?.RuneChanceRoll ?? -1,
                        pistaRandomRoll: skillRolls?.PistaRandomRoll ?? -1,
                        targetIndex: slot.SkillTargetIndex ?? slot.SlotIndex,
                        attackActionSlotIndex: slot.SlotIndex,
                        clientTick: request.ClientTick,
                        judgmentDivineStartingHp: judgmentDivineStartingHp,
                        frostRoll: skillRolls?.FrostRoll ?? -1,
                        drainRoll: skillRolls?.DrainRoll ?? -1);
                    slotOutcomes.Add(new LegacyAttackActionBatchSlotOutcome(
                        slot.SlotIndex,
                        LegacyAttackActionSlotKind.Skill,
                        SkillResult: skillResult,
                        SkillOutcome: skillOutcome));
                    if (skillOutcome?.ViewRemovalEffects is { Count: > 0 } skillViewRemovals)
                        viewRemovalEffects.AddRange(skillViewRemovals);
                    if (skillOutcome?.HealingExperienceContribution is int healingExperience)
                        maximumHealingExperience = Math.Max(maximumHealingExperience, healingExperience);
                    if (skillOutcome?.StopRemainingTargets == true)
                    {
                        // TMProject 7.69 breaks the target loop after Cancelamento removes Type 19,
                        // then continues through the ordinary post-loop relay/finalization path.
                        break;
                    }
                    if (request.Skill!.InstanceType == 9 && skillResult == LegacySkillAttackResult.TargetNotAlive)
                    {
                        // TMProject 7.69 breaks the target loop here, then still executes the normal post-loop relay/finalization.
                        break;
                    }
                    if ((request.Skill!.InstanceType == 9 &&
                         skillResult is LegacySkillAttackResult.SummonInvalidPosition or LegacySkillAttackResult.SummonZoneBlocked) ||
                        (request.Skill.InstanceType == 12 && skillResult == LegacySkillAttackResult.PartyFullCantSummon))
                    {
                        // TMProject 7.69 returns from Exec_MSG_Attack for blocked zones; earlier slot mutations remain staged.
                        // Later slots and normal post-loop relay/death finalization are skipped.
                        abortSlotIndex = slot.SlotIndex;
                        break;
                    }
                    continue;
                }

                var rolls = slot.PhysicalRolls;
                var actionRolls = request.PhysicalActionRolls;
                var result = TryApplyPhysicalAttackLocked(
                    request.AttackerConnectionId,
                    slot.TargetConnectionId,
                    slot.SlotIndex,
                    actionState,
                    rolls?.RandomFactor ?? System.Security.Cryptography.RandomNumberGenerator.GetInt32(99, 111),
                    out var slotOutcome,
                    attackMissRoll: rolls?.AttackMissRoll ?? -1,
                    itemData: request.ItemData,
                    absorptionRoll: rolls?.AbsorptionRoll ?? -1,
                    frostRoll: rolls?.FrostRoll ?? -1,
                    drainRoll: rolls?.DrainRoll ?? -1,
                    specialDropRoll: rolls?.SpecialDropRoll ?? -1,
                    specialItemRoll: rolls?.SpecialItemRoll ?? -1,
                    eventDropRoll: rolls?.EventDropRoll ?? -1,
                    runeRoll: rolls?.RuneRoll ?? -1,
                    runeChanceRoll: rolls?.RuneChanceRoll ?? -1,
                    pistaRandomRoll: rolls?.PistaRandomRoll ?? -1,
                    supportsSecondaryDamageSlot: request.SupportsSecondaryDamageSlot,
                    doubleCriticalRoll: actionRolls?.DoubleCriticalRoll ?? -1,
                    secondaryDamageRandomFactor: actionRolls?.SecondaryDamageRandomFactor ?? -1,
                    secondaryDamageFloorRoll: actionRolls?.SecondaryDamageFloorRoll ?? -1,
                    doubleCriticalChanceRoll: actionRolls?.DoubleCriticalChanceRoll ?? -1,
                    doubleCriticalDamageRoll: actionRolls?.DoubleCriticalDamageRoll ?? -1,
                    isShortTwoTargetPacket: request.IsShortTwoTargetPacket,
                    allowDeadTarget: request.Skill?.Id is LegacyAttackDeadTargetGate.RenascimentoSkillId or LegacyAttackDeadTargetGate.ResurrectionSkillId,
                    bloodHandsAmountRoll: rolls?.BloodHandsAmountRoll ?? -1,
                    bloodHandsChanceRoll: rolls?.BloodHandsChanceRoll ?? -1);
                slotOutcomes.Add(new LegacyAttackActionBatchSlotOutcome(
                    slot.SlotIndex,
                    LegacyAttackActionSlotKind.Physical,
                    result,
                    slotOutcome));
                if (result == LegacyPhysicalAttackResult.Accepted &&
                    slotOutcome is { AttackerHpHealProcTriggered: true } bloodHandsOutcome &&
                    participants.TryGetValue(request.AttackerConnectionId, out var bloodHandsAttacker))
                {
                    secondaryActions.Add(new LegacyAttackActionSecondaryOutcome(
                        slot.SlotIndex,
                        LegacyAttackActionSecondaryOutcomeKind.SetHpDam,
                        bloodHandsAttacker.Mob.ToArray(),
                        bloodHandsAttacker.RequestedHp,
                        bloodHandsAttacker.RequestedMana,
                        bloodHandsAttacker.PositionX,
                        bloodHandsAttacker.PositionY,
                        bloodHandsAttacker.ConnectionId,
                        bloodHandsOutcome.AttackerHpHealed,
                        bloodHandsOutcome.AttackerHpViewRecipients)
                    {
                        AffectSnapshot = bloodHandsAttacker.Affect.ToArray(),
                    });
                }
                if (slotOutcome?.ViewRemovalEffects is { Count: > 0 } physicalViewRemovals)
                    viewRemovalEffects.AddRange(physicalViewRemovals);
                if (result == LegacyPhysicalAttackResult.OutOfRange)
                {
                    abortSlotIndex = slot.SlotIndex;
                    break;
                }
            }

            if (abortSlotIndex is null)
            {
                for (var outcomeIndex = 0; outcomeIndex < slotOutcomes.Count; outcomeIndex++)
                {
                    var stagedSlot = slotOutcomes[outcomeIndex];
                    var requestSlot = request.Slots.First(slot => slot.SlotIndex == stagedSlot.SlotIndex);
                    if (stagedSlot.Kind == LegacyAttackActionSlotKind.Skill && stagedSlot.SkillOutcome is { } stagedSkillOutcome)
                    {
                        var finalizedSkillOutcome = FinalizeSkillAttackDeathLocked(
                            request.AttackerConnectionId,
                            requestSlot,
                            stagedSkillOutcome);
                        slotOutcomes[outcomeIndex] = stagedSlot with { SkillOutcome = finalizedSkillOutcome };
                        continue;
                    }

                    if (stagedSlot.PhysicalOutcome is not { TargetDied: true } stagedPhysicalDeath)
                        continue;

                    var physicalRequestSlot = new LegacyPhysicalAttackBatchSlotRequest(
                        requestSlot.SlotIndex,
                        requestSlot.TargetConnectionId,
                        requestSlot.PhysicalRolls);
                    var finalizedPhysicalDeath = FinalizePhysicalAttackDeathLocked(
                        request.AttackerConnectionId,
                        physicalRequestSlot,
                        stagedPhysicalDeath);
                    slotOutcomes[outcomeIndex] = stagedSlot with { PhysicalOutcome = finalizedPhysicalDeath };
                }
            }

            wireSlotOverrides.AddRange(slotOutcomes
                .Where(static slot => slot.PhysicalOutcome?.ClearTargetIdOnWire == true ||
                    slot.SkillOutcome?.ClearTargetIdOnWire == true)
                .Select(static slot => new LegacyPhysicalAttackBatchWireSlotOverride(slot.SlotIndex, 0, 0)));

            participants.TryGetValue(request.AttackerConnectionId, out var attacker);
            if (attacker is not null)
                RememberAcceptedPlayerAttackTargetsLocked(attacker, request.Slots, slotOutcomes);

            outcome = new LegacyAttackActionBatchOutcome(
                slotOutcomes,
                abortSlotIndex,
                actionState.DoubleCritical,
                actionState.NextProgress,
                attacker?.Mob.ToArray(),
                abortSlotIndex is null,
                wireSlotOverrides.Concat(actionState.GetWireSlotOverrides()).ToArray(),
                abortSlotIndex is null
                    ? LegacyAttackHealExperienceMath.ApplyDoubleModeAndCap(maximumHealingExperience, combatWorldState.DoubleModeMultiplier)
                    : null)
            {
                SecondaryActions = secondaryActions.ToArray(),
                SpawnedNpcs = spawnedNpcs.ToArray(),
                ViewRemovalEffects = viewRemovalEffects.ToArray(),
            };
            return true;
        }
    }

    private void RememberAcceptedPlayerAttackTargetsLocked(
        Participant attacker,
        IReadOnlyList<LegacyAttackActionBatchSlotRequest> requestedSlots,
        IReadOnlyList<LegacyAttackActionBatchSlotOutcome> slotOutcomes)
    {
        foreach (var slotOutcome in slotOutcomes)
        {
            var accepted = slotOutcome.Kind == LegacyAttackActionSlotKind.Physical
                ? slotOutcome.PhysicalResult == LegacyPhysicalAttackResult.Accepted && slotOutcome.PhysicalOutcome is not null
                : slotOutcome.SkillResult == LegacySkillAttackResult.Accepted && slotOutcome.SkillOutcome is not null;
            if (!accepted)
                continue;

            var requestedSlot = requestedSlots.FirstOrDefault(candidate => candidate.SlotIndex == slotOutcome.SlotIndex);
            if (requestedSlot is null ||
                !TryGetBattleTargetLocked(requestedSlot.TargetConnectionId, out _, out _, out _, out _))
                continue;

            // TMProject 7.69 and W2PP assign pMob[attacker].CurrentTarget at the
            // living-target tail of each _MSG_Attack slot. Later living slots win.
            attacker.CurrentTarget = requestedSlot.TargetConnectionId;
        }
    }

    private bool TrySpawnNamedNpcLocked(string mobName, short positionX, short positionY, int routeType, out LegacyWorldNpc? spawned)
    {
        spawned = null;
        if (npcGenerationCatalog is null ||
            !npcGenerationCatalog.TryCreateNamedNpc(mobName, positionX, positionY, out var generated) ||
            generated is null)
            return false;

        var connectionId = AllocateNpcConnectionId();
        spawned = new LegacyWorldNpc(
            connectionId,
            positionX,
            positionY,
            generated.MobSnapshot,
            generated.AffectSnapshot,
            GenerateIndex: -1,
            TerrainHeight: mapGrid?.GetTerrainHeight(positionX, positionY),
            RouteType: routeType);
        spawned = RegisterGuildTreasuryNpcLocked(spawned);
        npcs.Add(connectionId, spawned);
        return true;
    }

    private LegacySkillAttackResult TryApplyDivineBlessingParentSlotLocked(
        LegacyAttackActionBatchRequest request,
        LegacyAttackActionBatchSlotRequest slot,
        List<LegacyAttackActionSecondaryOutcome> secondaryActions)
    {
        if (!participants.TryGetValue(request.AttackerConnectionId, out var attacker))
            return LegacySkillAttackResult.ParticipantNotFound;
        if (attacker.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
            return LegacySkillAttackResult.CombatStateUnavailable;

        var parentSkill = request.Skill!;
        var targetIndex = slot.SkillTargetIndex ?? slot.SlotIndex;
        if (LegacyAttackTargetLimitGate.IsExceeded(targetIndex, parentSkill.MaxTarget, -1, request.ClientTick))
            return LegacySkillAttackResult.TargetLimitExceeded;

        byte[] targetMob;
        short targetX;
        short targetY;
        if (participants.TryGetValue(slot.TargetConnectionId, out var targetParticipant))
        {
            if (targetParticipant.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                return LegacySkillAttackResult.CombatStateUnavailable;
            targetMob = targetParticipant.Mob;
            targetX = targetParticipant.PositionX;
            targetY = targetParticipant.PositionY;
        }
        else if (npcs.TryGetValue(slot.TargetConnectionId, out var targetNpc))
        {
            if (targetNpc.MobSnapshot.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                return LegacySkillAttackResult.CombatStateUnavailable;
            if (LegacyAttackTowerGate.IsBlocked(targetNpc.GenerateIndex, attacker.Mob, targetNpc.MobSnapshot))
                return LegacySkillAttackResult.TowerAttackBlocked;
            targetMob = targetNpc.MobSnapshot;
            targetX = targetNpc.PositionX;
            targetY = targetNpc.PositionY;
        }
        else
        {
            return LegacySkillAttackResult.ParticipantNotFound;
        }

        var targetState = LegacyMobCombatState.Read(targetMob);
        if (targetState.CurrentScore.Hp <= 0)
            return LegacySkillAttackResult.TargetNotAlive;
        if (targetState.CurrentScore.Merchant is 1 or 4 or 43 or 100)
            return LegacySkillAttackResult.TargetMerchantBlocked;
        if (parentSkill.Id != 42 &&
            (attacker.PositionX < targetX - 16 || attacker.PositionX > targetX + 16 ||
             attacker.PositionY < targetY - 16 || attacker.PositionY > targetY + 16))
            return LegacySkillAttackResult.OutOfRange;
        var secondaryTargetGuildId = targetParticipant is { GuildDisabled: false } ? targetParticipant.GuildId : 0;
        if (LegacyPartySkillTargetGate.IsBlocked(
                parentSkill.PartyCheck,
                GetPartyLeaderConnectionIdLocked(attacker.ConnectionId),
                participants.ContainsKey(slot.TargetConnectionId)
                    ? GetPartyLeaderConnectionIdLocked(slot.TargetConnectionId)
                    : slot.TargetConnectionId,
                GetPartySkillAttackerGuildIdLocked(attacker, secondaryTargetGuildId),
                secondaryTargetGuildId))
            return LegacySkillAttackResult.PartySkillBlocked;

        if (!LegacyMobExtra.TryReadSecondaryLearnedSkill(attacker.MobExtra, out var secondarySkills) ||
            (secondarySkills & LegacyMobExtra.DivineBlessingLearnedBit) == 0)
            return LegacySkillAttackResult.Accepted;

        var healSkill = request.DivineBlessingHealSkill;
        if (healSkill is null || healSkill.Id != LegacyDivineBlessing.HealSkillId)
            return LegacySkillAttackResult.UnsupportedSkillType;

        // This is the server-generated recursive MSG_AttackOne: CurrentMp=-2 and SKIPCHECKTICK are never accepted
        // from or read from the client request. Its skill 29 debit runs before its self-target alive checks.
        var manaResult = TryConsumeSkillManaLocked(
            attacker,
            healSkill,
            specialOverride: 0,
            applyMagicMirror: false,
            out var childState,
            out var childMobSnapshot,
            out var childRequestedMp);
        if (manaResult == LegacySkillManaResult.ParticipantNotFound || childState is null || childMobSnapshot is null)
            return LegacySkillAttackResult.ParticipantNotFound;

        var currentState = LegacyMobCombatState.Read(attacker.Mob);
        var requestedHp = Math.Max(attacker.RequestedHp, currentState.CurrentScore.Hp);
        if (manaResult == LegacySkillManaResult.InsufficientMana)
        {
            secondaryActions.Add(new LegacyAttackActionSecondaryOutcome(
                slot.SlotIndex,
                LegacyAttackActionSecondaryOutcomeKind.SetHpMp,
                childMobSnapshot,
                requestedHp,
                childRequestedMp,
                attacker.PositionX,
                attacker.PositionY,
                0,
                0,
                [attacker.ConnectionId]));
            return LegacySkillAttackResult.Accepted;
        }

        var wireTargetId = attacker.ConnectionId;
        var wireDamage = -1;
        if (currentState.CurrentScore.Hp <= 0)
        {
            wireTargetId = 0;
            wireDamage = 0;
        }
        else if (attacker.Clan != 4)
        {
            var casterAttribute = mapGrid?.GetAttribute(attacker.PositionX, attacker.PositionY) ?? attacker.MapAttribute;
            var healTargetAttribute = mapGrid?.GetAttribute(targetX, targetY) ?? targetParticipant?.MapAttribute ?? 0;
            if ((casterAttribute & 0x40) == 0 && (healTargetAttribute & 0x40) != 0)
            {
                wireTargetId = 0;
                wireDamage = 0;
            }
            else
            {
                var mountOffset = LegacyAccountSnapshot.MobEquipmentOffset + (13 * LegacyItem.SizeInBytes);
                var mount = LegacyItem.Read(attacker.Mob.AsSpan(mountOffset, LegacyItem.SizeInBytes));
                var heal = LegacyDivineBlessing.CalculateHeal(
                    currentState.CurrentScore.Hp,
                    currentState.CurrentScore.MaxHp,
                    mount.Index,
                    mount.Value1);
                BinaryPrimitives.WriteInt32LittleEndian(
                    attacker.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24),
                    heal.CurrentHp);
                wireDamage = heal.WireDamage;
            }
        }

        currentState = LegacyMobCombatState.Read(attacker.Mob);
        secondaryActions.Add(new LegacyAttackActionSecondaryOutcome(
            slot.SlotIndex,
            LegacyAttackActionSecondaryOutcomeKind.AttackOne,
            attacker.Mob.ToArray(),
            Math.Max(attacker.RequestedHp, currentState.CurrentScore.Hp),
            attacker.RequestedMana,
            attacker.PositionX,
            attacker.PositionY,
            wireTargetId,
            wireDamage,
            GetParticipantIdsInViewLocked(attacker.PositionX, attacker.PositionY)));
        return LegacySkillAttackResult.Accepted;
    }

    public bool TryApplyPhysicalAttackBatch(
        LegacyPhysicalAttackBatchRequest request,
        out LegacyPhysicalAttackBatchOutcome? outcome)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Slots);
        if (request.Slots.Count is < 1 or > 13)
            throw new ArgumentOutOfRangeException(nameof(request), "A physical attack batch must contain between 1 and 13 wire slots.");

        var previousSlotIndex = -1;
        foreach (var slot in request.Slots)
        {
            if (slot is null)
                throw new ArgumentException("Physical attack batch slots cannot contain null entries.", nameof(request));
            if (slot.SlotIndex is < 0 or > 12 || slot.SlotIndex <= previousSlotIndex)
                throw new ArgumentException("Physical attack batch slots must have unique, ascending wire indexes from 0 through 12.", nameof(request));
            previousSlotIndex = slot.SlotIndex;
        }

        var actionRequest = new LegacyAttackActionBatchRequest(
            request.AttackerConnectionId,
            request.Slots.Select(slot => new LegacyAttackActionBatchSlotRequest(
                slot.SlotIndex,
                LegacyAttackActionSlotKind.Physical,
                slot.TargetConnectionId,
                slot.Rolls)).ToArray(),
            ItemData: request.ItemData,
            ClientProgress: request.ClientProgress,
            SupportsSecondaryDamageSlot: request.SupportsSecondaryDamageSlot,
            PhysicalActionRolls: request.ActionRolls);
        if (!TryApplyAttackActionBatch(actionRequest, out var actionOutcome) || actionOutcome is null)
        {
            outcome = null;
            return false;
        }

        outcome = new LegacyPhysicalAttackBatchOutcome(
            actionOutcome.SlotOutcomes.Select(slot => new LegacyPhysicalAttackBatchSlotOutcome(
                slot.SlotIndex,
                slot.PhysicalResult ?? LegacyPhysicalAttackResult.ParticipantNotFound,
                slot.PhysicalOutcome)).ToArray(),
            actionOutcome.AbortSlotIndex,
            actionOutcome.DoubleCritical,
            actionOutcome.NextProgress,
            actionOutcome.AttackerMobSnapshot,
            actionOutcome.CompletedNormally,
            actionOutcome.WireSlotOverrides)
        {
            ViewRemovalEffects = actionOutcome.ViewRemovalEffects,
        };
        return true;
    }

    private LegacySkillAttackOutcome FinalizeSkillAttackDeathLocked(
        int attackerConnectionId,
        LegacyAttackActionBatchSlotRequest slot,
        LegacySkillAttackOutcome stagedOutcome)
    {
        var rolls = slot.SkillRolls;
        var physicalSlot = new LegacyPhysicalAttackBatchSlotRequest(
            slot.SlotIndex,
            stagedOutcome.TargetConnectionId,
            new LegacyPhysicalAttackBatchSlotRolls(
                SpecialDropRoll: rolls?.SpecialDropRoll,
                SpecialItemRoll: rolls?.SpecialItemRoll,
                EventDropRoll: rolls?.EventDropRoll,
                RuneRoll: rolls?.RuneRoll,
                RuneChanceRoll: rolls?.RuneChanceRoll,
                PistaRandomRoll: rolls?.PistaRandomRoll));
        var stagedPhysicalOutcome = new LegacyPhysicalAttackOutcome(
            stagedOutcome.TargetConnectionId,
            stagedOutcome.Damage,
            stagedOutcome.RemainingHp,
            stagedOutcome.TargetMobSnapshot,
            TargetDied: true,
            TargetHpChanged: stagedOutcome.TargetHpChanged,
            TargetAffectSnapshot: stagedOutcome.TargetAffectSnapshot,
            MountOwnerConnectionId: stagedOutcome.MountOwnerConnectionId,
            UpdatedMountItem: stagedOutcome.UpdatedMountItem);
        var finalized = FinalizePhysicalAttackDeathLocked(attackerConnectionId, physicalSlot, stagedPhysicalOutcome);
        var privateNotices = stagedOutcome.PrivateNotices;
        if (finalized.PrivateNotices is { Count: > 0 } finalizedPrivateNotices)
            privateNotices = privateNotices is { Count: > 0 } stagedPrivateNotices
                ? [.. stagedPrivateNotices, .. finalizedPrivateNotices]
                : finalizedPrivateNotices;
        IReadOnlyList<LegacyItemDrop>? itemDrops = stagedOutcome.ItemDrops is { Count: > 0 } stagedItemDrops && finalized.ItemDrops is { Count: > 0 } finalizedItemDrops
            ? [.. stagedItemDrops, .. finalizedItemDrops]
            : stagedOutcome.ItemDrops is { Count: > 0 } onlyStagedItemDrops
                ? onlyStagedItemDrops
                : finalized.ItemDrops;
        return stagedOutcome with
        {
            RemainingHp = finalized.RemainingHp,
            TargetMobSnapshot = finalized.TargetMobSnapshot,
            TargetResourceChanged = stagedOutcome.TargetResourceChanged || finalized.TargetRevived,
            TargetRevived = finalized.TargetRevived,
            ConsumedItemSlot = finalized.ConsumedItemSlot,
            ConsumedItem = finalized.ConsumedItem,
            TargetRemoved = finalized.TargetRemoved,
            ExperienceAwarded = finalized.ExperienceAwarded,
            AttackerMobSnapshot = finalized.AttackerMobSnapshot,
            ExperienceAwards = finalized.ExperienceAwards,
            ItemDrops = itemDrops,
            TargetHpChanged = finalized.TargetHpChanged,
            PistaTransition = finalized.PistaTransition,
            GlobalNotices = finalized.GlobalNotices,
            AreaNotices = finalized.AreaNotices,
            CoinUpdates = finalized.CoinUpdates,
            PrivateNotices = privateNotices,
            MountOwnerConnectionId = finalized.MountOwnerConnectionId,
            UpdatedMountItem = finalized.UpdatedMountItem,
        };
    }

    private LegacyPhysicalAttackOutcome FinalizePhysicalAttackDeathLocked(
        int attackerConnectionId,
        LegacyPhysicalAttackBatchSlotRequest slot,
        LegacyPhysicalAttackOutcome stagedOutcome)
    {
        if (!participants.TryGetValue(attackerConnectionId, out var attacker))
            return stagedOutcome;

        var targetConnectionId = stagedOutcome.TargetConnectionId;
        if (participants.TryGetValue(targetConnectionId, out var targetParticipant))
        {
            if (LegacyMobCombatState.Read(targetParticipant.Mob).CurrentScore.Hp != 0)
                return stagedOutcome with { TargetDied = false, TargetMobSnapshot = targetParticipant.Mob.ToArray() };

            if (!TryConsumeResurrectionScroll(targetParticipant, out var consumedItemSlot, out var consumedItem))
                return stagedOutcome with { TargetMobSnapshot = targetParticipant.Mob.ToArray() };

            var revivedHp = LegacyMobCombatState.Read(targetParticipant.Mob).CurrentScore.Hp;
            targetParticipant.RequestedHp = revivedHp;
            return stagedOutcome with
            {
                RemainingHp = revivedHp,
                TargetMobSnapshot = targetParticipant.Mob.ToArray(),
                TargetDied = false,
                TargetRevived = true,
                ConsumedItemSlot = consumedItemSlot,
                ConsumedItem = consumedItem,
                TargetHpChanged = true,
            };
        }

        if (summons.TryGetValue(targetConnectionId, out var targetSummon))
        {
            var targetMob = targetSummon.MobSnapshot;
            if (LegacyMobCombatState.Read(targetMob).CurrentScore.Hp != 0)
                return stagedOutcome with { TargetDied = false, TargetMobSnapshot = targetMob.ToArray() };

            TryLinkMountHpLocked(targetSummon, targetMob, out var mountOwnerConnectionId, out var updatedMountItem);
            var targetRemoved = RemoveSummonLocked(targetConnectionId);
            if (!targetRemoved)
                summons[targetConnectionId] = targetSummon with { MobSnapshot = targetMob };

            return stagedOutcome with
            {
                TargetMobSnapshot = targetMob.ToArray(),
                TargetRemoved = targetRemoved,
                MountOwnerConnectionId = mountOwnerConnectionId,
                UpdatedMountItem = updatedMountItem,
            };
        }

        if (!npcs.TryGetValue(targetConnectionId, out var targetNpc))
            return stagedOutcome;

        var deadNpcMob = targetNpc.MobSnapshot;
        var targetState = LegacyMobCombatState.Read(deadNpcMob);
        if (targetState.CurrentScore.Hp != 0)
            return stagedOutcome with { TargetDied = false, TargetMobSnapshot = deadNpcMob.ToArray() };

        if (!RemoveNpcLocked(targetConnectionId))
            return stagedOutcome with { TargetMobSnapshot = deadNpcMob.ToArray() };

        var attackerState = LegacyMobCombatState.Read(attacker.Mob);
        long experienceAwarded = 0;
        byte[]? attackerMobSnapshot = null;
        IReadOnlyList<LegacyExperienceAward> experienceAwards = [];
        IReadOnlyList<LegacyItemDrop> itemDrops = stagedOutcome.ItemDrops ?? [];
        IReadOnlyList<LegacyCoinUpdate> coinUpdates = [];
        IReadOnlyList<LegacyPrivateNotice> privateNotices = stagedOutcome.PrivateNotices ?? [];
        IReadOnlyList<LegacyGlobalNotice> globalNotices = [];
        IReadOnlyList<LegacyAreaNotice> areaNotices = [];
        if (deadNpcMob[LegacyAccountSnapshot.MobClanOffset] != 4)
        {
            experienceAwards = AwardNpcExperienceLocked(attacker, attackerState, targetState, targetNpc.PositionX, targetNpc.PositionY);
            var attackerAward = experienceAwards.FirstOrDefault(award => award.ConnectionId == attackerConnectionId);
            experienceAwarded = attackerAward?.Experience ?? 0;
            attackerMobSnapshot = attackerAward?.MobSnapshot;
        }

        var rolls = slot.Rolls;
        var effectivePistaChanceRoll = rolls?.RuneChanceRoll ?? -1;
        if (targetNpc.GenerateIndex is 5653 or 5654 && effectivePistaChanceRoll < 0)
            effectivePistaChanceRoll = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100);
        var pistaTransition = ApplyPistaDeathLocked(attacker, targetNpc, rolls?.PistaRandomRoll ?? -1, effectivePistaChanceRoll);
        areaNotices = ApplyNpcDeathNoticesLocked(targetNpc, ReadMobName(attacker.Mob));
        var dropResult = AwardNpcDropsLocked(
            attacker,
            targetNpc,
            deadNpcMob,
            targetState.CurrentScore.Level,
            rolls?.SpecialDropRoll ?? -1,
            rolls?.SpecialItemRoll ?? -1,
            rolls?.EventDropRoll ?? -1,
            rolls?.RuneRoll ?? -1,
            effectivePistaChanceRoll);
        if (dropResult.ItemDrops.Count > 0)
            itemDrops = [.. itemDrops, .. dropResult.ItemDrops];
        globalNotices = dropResult.GlobalNotices;
        if (itemDrops.Count > 0)
            attackerMobSnapshot = attacker.Mob.ToArray();
        var castleReward = AwardCastleQuestLocked(attacker, targetNpc);
        if (castleReward is not null)
        {
            itemDrops = [.. itemDrops, .. castleReward.ItemDrops];
            experienceAwards = [.. experienceAwards, .. castleReward.ExperienceAwards];
            coinUpdates = castleReward.CoinUpdates;
            if (castleReward.PrivateNotices.Count > 0)
                privateNotices = [.. privateNotices, .. castleReward.PrivateNotices];
            attackerMobSnapshot = castleReward.AttackerMobSnapshot ?? attackerMobSnapshot;
        }

        return stagedOutcome with
        {
            TargetMobSnapshot = deadNpcMob.ToArray(),
            TargetRemoved = true,
            ExperienceAwarded = experienceAwarded,
            AttackerMobSnapshot = attackerMobSnapshot,
            ExperienceAwards = experienceAwards,
            ItemDrops = itemDrops,
            PistaTransition = pistaTransition,
            GlobalNotices = globalNotices,
            AreaNotices = areaNotices,
            CoinUpdates = coinUpdates,
            PrivateNotices = privateNotices,
        };
    }

    public LegacyPhysicalAttackResult TryApplyPhysicalAttack(
        int attackerConnectionId,
        int targetConnectionId,
        int randomFactor,
        out LegacyPhysicalAttackOutcome? outcome,
        int attackMissRoll = -1,
        LegacyItemDataTable? itemData = null,
        int absorptionRoll = -1,
        int frostRoll = -1,
        int drainRoll = -1,
        int specialDropRoll = -1,
        int specialItemRoll = -1,
        int eventDropRoll = -1,
        int runeRoll = -1,
        int runeChanceRoll = -1,
        int pistaRandomRoll = -1,
        bool supportsSecondaryDamageSlot = false,
        int doubleCriticalRoll = -1,
        int secondaryDamageRandomFactor = -1,
        int secondaryDamageFloorRoll = -1,
        ushort clientProgress = 0,
        int doubleCriticalChanceRoll = -1,
        int doubleCriticalDamageRoll = -1,
        int bloodHandsAmountRoll = -1,
        int bloodHandsChanceRoll = -1)
    {
        var request = new LegacyPhysicalAttackBatchRequest(
            attackerConnectionId,
            [new LegacyPhysicalAttackBatchSlotRequest(
                0,
                targetConnectionId,
                new LegacyPhysicalAttackBatchSlotRolls(
                    RandomFactor: randomFactor,
                    AttackMissRoll: attackMissRoll,
                    AbsorptionRoll: absorptionRoll,
                    FrostRoll: frostRoll,
                    DrainRoll: drainRoll,
                    SpecialDropRoll: specialDropRoll,
                    SpecialItemRoll: specialItemRoll,
                    EventDropRoll: eventDropRoll,
                    RuneRoll: runeRoll,
                    RuneChanceRoll: runeChanceRoll,
                    PistaRandomRoll: pistaRandomRoll,
                    BloodHandsAmountRoll: bloodHandsAmountRoll,
                    BloodHandsChanceRoll: bloodHandsChanceRoll))],
            clientProgress,
            supportsSecondaryDamageSlot,
            itemData,
            new LegacyPhysicalAttackBatchActionRolls(
                doubleCriticalRoll,
                secondaryDamageRandomFactor,
                secondaryDamageFloorRoll,
                doubleCriticalChanceRoll,
                doubleCriticalDamageRoll));
        if (!TryApplyPhysicalAttackBatch(request, out var batch) || batch is null || batch.SlotOutcomes.Count != 1)
        {
            outcome = null;
            return LegacyPhysicalAttackResult.ParticipantNotFound;
        }

        var slot = batch.SlotOutcomes[0];
        outcome = slot.Outcome;
        return slot.Result;
    }

    private LegacyPhysicalAttackResult TryApplyPhysicalAttackLocked(
        int attackerConnectionId,
        int targetConnectionId,
        int slotIndex,
        LegacyPhysicalAttackBatchActionState actionState,
        int randomFactor,
        out LegacyPhysicalAttackOutcome? outcome,
        int attackMissRoll = -1,
        LegacyItemDataTable? itemData = null,
        int absorptionRoll = -1,
        int frostRoll = -1,
        int drainRoll = -1,
        int specialDropRoll = -1,
        int specialItemRoll = -1,
        int eventDropRoll = -1,
        int runeRoll = -1,
        int runeChanceRoll = -1,
        int pistaRandomRoll = -1,
        bool supportsSecondaryDamageSlot = false,
        int doubleCriticalRoll = -1,
        int secondaryDamageRandomFactor = -1,
        int secondaryDamageFloorRoll = -1,
        int doubleCriticalChanceRoll = -1,
        int doubleCriticalDamageRoll = -1,
        bool isShortTwoTargetPacket = false,
        bool allowDeadTarget = false,
        int bloodHandsAmountRoll = -1,
        int bloodHandsChanceRoll = -1)
    {
        if (attackMissRoll is < -1 or > 99)
            throw new ArgumentOutOfRangeException(nameof(attackMissRoll), attackMissRoll, "The injected 7.69 attack-miss roll must be -1 or between 0 and 99.");
        // Retain the legacy injection parameter for API compatibility. TMProject 7.69
        // applies Type=8 bit 3 deterministically, so this roll does not affect healing.
        if (absorptionRoll is < -1 or > 1)
            throw new ArgumentOutOfRangeException(nameof(absorptionRoll), absorptionRoll, "The injected legacy absorption roll must be -1, 0, or 1.");
        if (frostRoll is < -1 or > 1)
            throw new ArgumentOutOfRangeException(nameof(frostRoll), frostRoll, "The injected legacy frost roll must be -1, 0, or 1.");
        if (drainRoll is < -1 or > 1)
            throw new ArgumentOutOfRangeException(nameof(drainRoll), drainRoll, "The injected legacy drain roll must be -1, 0, or 1.");
        if (doubleCriticalRoll is < -1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(doubleCriticalRoll), doubleCriticalRoll, "The injected legacy DoubleCritical roll must be -1 or between 0 and 3.");
        if (bloodHandsAmountRoll is < -1 or > 14)
            throw new ArgumentOutOfRangeException(nameof(bloodHandsAmountRoll), bloodHandsAmountRoll, "The injected Mãos Sangrentas amount roll must be -1 or between 0 and 14.");
        if (bloodHandsChanceRoll is < -1 or > 99)
            throw new ArgumentOutOfRangeException(nameof(bloodHandsChanceRoll), bloodHandsChanceRoll, "The injected Mãos Sangrentas chance roll must be -1 or between 0 and 99.");
        if (secondaryDamageRandomFactor != -1 && secondaryDamageRandomFactor is < 99 or >= 111)
            throw new ArgumentOutOfRangeException(nameof(secondaryDamageRandomFactor), secondaryDamageRandomFactor, "The injected secondary physical damage factor must be -1 or between 99 and 110.");
        if (secondaryDamageFloorRoll is < -1 or > 59)
            throw new ArgumentOutOfRangeException(nameof(secondaryDamageFloorRoll), secondaryDamageFloorRoll, "The injected legacy DoubleCritical floor roll must be -1 or between 0 and 59.");
        if (doubleCriticalChanceRoll is < -1 or >= 255)
            throw new ArgumentOutOfRangeException(nameof(doubleCriticalChanceRoll), doubleCriticalChanceRoll, "The injected legacy Critical roll must be -1 or between 0 and 254.");
        if (doubleCriticalDamageRoll is < -1 or > 1)
            throw new ArgumentOutOfRangeException(nameof(doubleCriticalDamageRoll), doubleCriticalDamageRoll, "The injected legacy DoubleCritical damage roll must be -1, 0, or 1.");

        outcome = null;
        {
            if (!participants.TryGetValue(attackerConnectionId, out var attacker))
                return LegacyPhysicalAttackResult.ParticipantNotFound;
            if (attackerConnectionId == targetConnectionId)
                return LegacyPhysicalAttackResult.SameParticipant;
            Participant? targetParticipant = null;
            LegacySummonedMob? targetSummon = null;
            LegacyWorldNpc? targetNpc = null;
            byte[] targetMob;
            short targetPositionX;
            short targetPositionY;
            if (participants.TryGetValue(targetConnectionId, out targetParticipant))
            {
                targetMob = targetParticipant.Mob;
                targetPositionX = targetParticipant.PositionX;
                targetPositionY = targetParticipant.PositionY;
            }
            else if (summons.TryGetValue(targetConnectionId, out targetSummon))
            {
                targetMob = targetSummon.MobSnapshot.ToArray();
                targetPositionX = targetSummon.PositionX;
                targetPositionY = targetSummon.PositionY;
            }
            else if (npcs.TryGetValue(targetConnectionId, out targetNpc))
            {
                targetMob = targetNpc.MobSnapshot.ToArray();
                targetPositionX = targetNpc.PositionX;
                targetPositionY = targetNpc.PositionY;
            }
            else
                return LegacyPhysicalAttackResult.ParticipantNotFound;

            if (attacker.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize || targetMob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                return LegacyPhysicalAttackResult.CombatStateUnavailable;
            if (LegacyAttackNpcHeadAreaGate.ShouldBlock(
                    targetParticipant is null,
                    LegacyItem.Read(targetMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes)).Index,
                    targetPositionX,
                    targetPositionY))
                return LegacyPhysicalAttackResult.NpcHeadAreaBlocked;
            if (targetNpc is not null && LegacyAttackTowerGate.IsBlocked(targetNpc.GenerateIndex, attacker.Mob, targetMob))
                return LegacyPhysicalAttackResult.TowerAttackBlocked;

            var attackerState = LegacyMobCombatState.Read(attacker.Mob);
            var targetState = LegacyMobCombatState.Read(targetMob);
            var targetAffect = targetParticipant?.Affect ?? targetSummon?.AffectSnapshot.ToArray() ?? targetNpc?.AffectSnapshot.ToArray() ?? new byte[LegacyAccountSnapshot.AffectStride];
            var playerHadHolyTouchAffect = targetParticipant is not null && HasLegacyAffectType(targetAffect, 1);
            var previousNpcAffect = targetNpc is null ? null : targetAffect.ToArray();
            var npcHolyTouchBaseScore = targetNpc?.HolyTouchBaseScore;
            if (attackerState.CurrentScore.Hp <= 0)
                return LegacyPhysicalAttackResult.AttackerNotAlive;
            if (targetState.CurrentScore.Hp <= 0 && !allowDeadTarget)
                return LegacyPhysicalAttackResult.TargetNotAlive;

            var distance = LegacyCombatMath.GetDistance(attacker.PositionX, attacker.PositionY, targetPositionX, targetPositionY);
            if (distance > 23)
                return LegacyPhysicalAttackResult.OutOfRange;

            if (LegacyAttackType24Gate.ShouldRejectSecondaryPhysicalTarget(
                    slotIndex,
                    isShortTwoTargetPacket,
                    attackerState.CharacterClass,
                    attackerState.LearnedSkill))
                return LegacyPhysicalAttackResult.ShortPacketSecondaryTargetBlocked;

            var doubleCriticalResult = actionState.ResolveDoubleCriticalForPhysicalSlot(
                slotIndex,
                () =>
                {
                    var effectiveDoubleCriticalChanceRoll = doubleCriticalChanceRoll >= 0
                        ? doubleCriticalChanceRoll
                        : System.Security.Cryptography.RandomNumberGenerator.GetInt32(255);
                    return LegacyDoubleCriticalMath.Resolve(
                        attackerState.CurrentScore.AttackRun,
                        attackerState.Critical,
                        attacker.CriticalProgress,
                        actionState.ClientProgress,
                        effectiveDoubleCriticalChanceRoll);
                },
                out var criticalResolvedNow);
            if (criticalResolvedNow)
                attacker.CriticalProgress = doubleCriticalResult.NextProgress;
            var doubleCritical = actionState.DoubleCritical;

            var physicalBaseDamage = attackerState.CurrentScore.Damage;
            var bloodHandsHealed = 0;
            var bloodHandsProcTriggered = false;
            if ((doubleCritical & 2) != 0)
            {
                var multiplierRoll = doubleCriticalDamageRoll >= 0
                    ? doubleCriticalDamageRoll
                    : System.Security.Cryptography.RandomNumberGenerator.GetInt32(2);
                var multiplierBase = targetParticipant is not null ? 13 : 15;
                physicalBaseDamage = (int)(((long)(multiplierBase + multiplierRoll) * physicalBaseDamage) / 10);

                if (attackerState.CharacterClass == 0 &&
                    LegacyMobExtra.TryReadSecondaryLearnedSkill(attacker.MobExtra, out var secondaryLearnedSkill) &&
                    (secondaryLearnedSkill & LegacyMobExtra.BloodHandsLearnedBit) != 0 &&
                    itemData is not null)
                {
                    var weapon6 = LegacyItem.Read(attacker.Mob.AsSpan(
                        LegacyAccountSnapshot.MobEquipmentOffset + (6 * LegacyItem.SizeInBytes),
                        LegacyItem.SizeInBytes));
                    var weapon7 = LegacyItem.Read(attacker.Mob.AsSpan(
                        LegacyAccountSnapshot.MobEquipmentOffset + (7 * LegacyItem.SizeInBytes),
                        LegacyItem.SizeInBytes));
                    var hasWeapon = (weapon6.Index is > 0 && weapon6.Index < LegacyItemDataTable.MaxItemIndex) ||
                        (weapon7.Index is > 0 && weapon7.Index < LegacyItemDataTable.MaxItemIndex);
                    if (hasWeapon && itemData[weapon6.Index]?.Position == 192 && itemData[weapon7.Index]?.Position == 192)
                    {
                        var amountRoll = bloodHandsAmountRoll >= 0
                            ? bloodHandsAmountRoll
                            : System.Security.Cryptography.RandomNumberGenerator.GetInt32(15);
                        var chanceRoll = bloodHandsChanceRoll >= 0
                            ? bloodHandsChanceRoll
                            : System.Security.Cryptography.RandomNumberGenerator.GetInt32(100);
                        var bloodHandsResolution = LegacyBloodHandsMath.Resolve(
                            attackerState.CurrentScore.Special3,
                            amountRoll,
                            chanceRoll);
                        if (bloodHandsResolution.Triggered)
                        {
                            bloodHandsProcTriggered = true;
                            bloodHandsHealed = bloodHandsResolution.Healing;
                            var healedHp = Math.Min(
                                unchecked(attackerState.CurrentScore.Hp + bloodHandsHealed),
                                attackerState.CurrentScore.MaxHp);
                            BinaryPrimitives.WriteInt32LittleEndian(
                                attacker.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24),
                                healedHp);
                            attacker.RequestedHp = healedHp;
                            attackerState = LegacyMobCombatState.Read(attacker.Mob);
                        }
                    }
                }
            }

            var defense = targetParticipant is not null ? targetState.CurrentScore.Ac * 3 : targetState.CurrentScore.Ac;
            var damage = LegacySkillCombatMath.GetPhysicalDamage(physicalBaseDamage, defense, combat: 0, randomFactor);
            var secondaryDamage = actionState.SecondaryDamage;
            if (slotIndex == 0 &&
                supportsSecondaryDamageSlot &&
                attackerState.CharacterClass == 3 &&
                (attackerState.LearnedSkill & 0x200000) != 0)
            {
                var effectiveDoubleCriticalRoll = doubleCriticalRoll >= 0
                    ? doubleCriticalRoll
                    : System.Security.Cryptography.RandomNumberGenerator.GetInt32(4);
                if (effectiveDoubleCriticalRoll == 0)
                {
                    var secondaryDamageFactor = secondaryDamageRandomFactor >= 0
                        ? secondaryDamageRandomFactor
                        : System.Security.Cryptography.RandomNumberGenerator.GetInt32(99, 111);
                    secondaryDamage = LegacySkillCombatMath.GetPhysicalDamage(
                        attackerState.CurrentScore.Special4 + attackerState.CurrentScore.Strength,
                        targetState.CurrentScore.Ac,
                        LegacySkillCombatMath.GetMasterCombat(attackerState),
                        secondaryDamageFactor);
                    if (secondaryDamage > 0)
                        secondaryDamage /= 2;
                    if (secondaryDamage < 60)
                    {
                        var effectiveFloorRoll = secondaryDamageFloorRoll >= 0
                            ? secondaryDamageFloorRoll
                            : System.Security.Cryptography.RandomNumberGenerator.GetInt32(60);
                        secondaryDamage = effectiveFloorRoll + 10;
                    }

                    actionState.ReserveSecondaryDamageSlot(secondaryDamage);
                    doubleCritical = actionState.DoubleCritical;
                    damage += secondaryDamage;
                }
            }

            if ((doubleCritical & 1) != 0)
                damage *= 2;

            if (targetParticipant is not null || targetMob[LegacyAccountSnapshot.MobClanOffset] == 4)
            {
                damage = (doubleCritical & 4) != 0
                    ? secondaryDamage + (damage >> 2)
                    : damage / 4; // TMSrv's penetration step applies to players and clan-4 NPCs.
            }
            if (damage <= 0) damage = 1;

            var forceDamage = 0;
            var pvpDamage = 0;
            if (damage > 0)
            {
                forceDamage = GetLegacyForceDamage(
                    attacker.Mob, attacker.ClientEquipment, itemData, attacker.Affect, attackerState.CurrentScore.Special3);
                if (forceDamage != 0)
                    damage = damage <= 1 ? forceDamage : damage + forceDamage;

                if (targetParticipant is not null)
                {
                    pvpDamage = GetLegacyPvpDamage(attacker.Mob, attacker.ClientEquipment, itemData);
                    if (pvpDamage != 0)
                        damage = damage <= 1
                            ? damage + (damage * pvpDamage / 100)
                            : damage + (damage / 100 * pvpDamage);
                }
            }

            if (ShouldSuppressLegacySameClanNpcDamage(
                    targetConnectionId,
                    attacker.Mob[LegacyAccountSnapshot.MobClanOffset],
                    targetMob[LegacyAccountSnapshot.MobClanOffset]))
                damage = 0;

            var pkPointGateBlocked = false;
            IReadOnlyList<LegacyCrimeStateUpdate> crimeStateUpdates = [];
            var summonerParticipant = targetSummon is not null && participants.TryGetValue(targetSummon.SummonerConnectionId, out var ownerParticipant)
                ? ownerParticipant
                : null;
            var pkTarget = targetParticipant ?? summonerParticipant;
            if (pkTarget is not null && IsLegacyPkProtectedMap(attacker))
            {
                var attackerPkPoint = ReadLegacyPkPoint(attacker.Mob);
                var targetPkPoint = ReadLegacyPkPoint(pkTarget.Mob);
                if (attackerPkPoint <= 10 && targetPkPoint > 10)
                {
                    damage = 0;
                    pkPointGateBlocked = true;
                }
                else if (targetPkPoint > 10 && damage > 0)
                {
                    var updates = new List<LegacyCrimeStateUpdate>(2);
                    if (SetLegacyGuilty(attacker, 8))
                        updates.Add(new LegacyCrimeStateUpdate(attacker.ConnectionId, attacker.PositionX, attacker.PositionY, attacker.Mob.ToArray()));
                    if (pkTarget.ConnectionId != attacker.ConnectionId && SetLegacyGuilty(pkTarget, 8))
                        updates.Add(new LegacyCrimeStateUpdate(pkTarget.ConnectionId, pkTarget.PositionX, pkTarget.PositionY, pkTarget.Mob.ToArray()));
                    crimeStateUpdates = updates;
                }
            }

            // TMSrv 7.69 resolves MSG_Attack misses after penetration, force/PvP
            // damage, and the PK gate; a denied PK hit can therefore still relay -3.
            if (pkTarget is not null)
                damage = ResolveLegacyAttackMiss(attackerState, targetState, attackMissRoll, damage);

            // TMProject 7.69 applies Type 6 Divine/Absolute Protection in
            // MSG_Attack after miss resolution and before positive-damage effects.
            if (damage > 0 && (targetState.CharacterClass is 0 or 1) && HasLegacyAffectType(targetAffect, type: 6))
                damage = 0;

            // Physical MSG_Attack slots keep the handler-local Level at zero.
            if (LegacyAttackClanSixDamageGate.ShouldSuppressDamage(
                    targetMob[LegacyAccountSnapshot.MobClanOffset],
                    handlerLevel: 0))
                damage = 0;

            if (targetParticipant is null)
                damage = LegacyForceMobDamageMath.ApplyToMobTargetDamage(damage, attacker.Affect);

            damage = LegacyAttackDamageCap.Apply(damage);
            // This marker models the source loop's `dam <= 0` continue. Later
            // source branches may zero damage and still reach the tail.
            var sourceRetaliationTailReached = damage > 0;

            var secondaryEffectsChanged = damage > 0 && TryApplyLegacyRsvSecondaryAttackAffects(
                targetAffect,
                targetMob,
                attackerState,
                targetState,
                targetConnectionId,
                frostRoll,
                drainRoll);

            if (targetNpc is not null && secondaryEffectsChanged && previousNpcAffect is not null)
                npcHolyTouchBaseScore = ApplyNpcHolyTouchAffectScoreTransition(
                    targetMob, previousNpcAffect, targetAffect, npcHolyTouchBaseScore);
            else if (targetParticipant is not null &&
                     !playerHadHolyTouchAffect &&
                     HasLegacyAffectType(targetAffect, 1) &&
                     itemData is not null)
            {
                try
                {
                    var projected = ProjectPlayerScoreLocked(targetParticipant, targetMob, targetAffect, itemData);
                    targetMob = projected.Mob;
                    targetAffect = projected.Affect;
                    targetParticipant.MobExtra = projected.MobExtra;
                    if (projected.ClientEquipment is not null)
                        targetParticipant.ClientEquipment = projected.ClientEquipment.ToArray();
                }
                catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException)
                {
                    // Keep the attack path available if an out-of-contract MOB cannot be projected.
                    // Normal authenticated player state has already passed this projector at login.
                }
            }

            var reflectDamage = 0;
            var reflectPvp = 0;
            if (targetParticipant is not null && damage > 0)
            {
                reflectDamage = GetLegacyReflectDamage(targetMob, targetParticipant.ClientEquipment, targetState, itemData);
                reflectPvp = GetLegacyReflectPvp(targetMob, targetParticipant.ClientEquipment, itemData);
                if (reflectDamage > 0)
                {
                    damage -= reflectDamage;
                    if (damage <= 0) damage = 1;
                }

                if (damage > 0)
                {
                    targetParticipant.RuntimeParryAdjustment = LegacyPvpJewelParryMath.ApplyParryPenalty(
                        attacker.Affect,
                        targetParticipant.RuntimeParryAdjustment,
                        damage,
                        isPlayerTarget: true);
                }

                if (reflectPvp > 0)
                {
                    damage -= damage / 100 * reflectPvp;
                    if (damage <= 0) damage = 1;
                }
            }

            // TMProject 7.69 and W2PP continue the target loop before mount splitting
            // or ManaControl when same-Clan damage is blocked in this protected area.
            var sameClanAreaGate = LegacyAttackSameClanAreaGate.ShouldClearTargetId(
                attacker.PositionX,
                attacker.PositionY,
                attacker.Mob[LegacyAccountSnapshot.MobClanOffset],
                targetMob[LegacyAccountSnapshot.MobClanOffset],
                damage);
            if (sameClanAreaGate)
                sourceRetaliationTailReached = false;
            // TMProject 7.69 has a second player-only RvR gate here; unlike the
            // protected-area gate above, it does not continue before mount/ManaControl.
            var rvrSameClanGate = !sameClanAreaGate && targetParticipant is not null &&
                LegacyAttackRvrSameClanGate.ShouldClearTargetId(
                    attacker.ConnectionId,
                    targetParticipant.ConnectionId,
                    LegacyBattleRoyaleGridRefreshRelay.MaxUserConnectionExclusive,
                    targetParticipant.PositionX,
                    targetParticipant.PositionY,
                    combatWorldState.RvrWarActive,
                    attacker.Mob[LegacyAccountSnapshot.MobClanOffset],
                    targetMob[LegacyAccountSnapshot.MobClanOffset],
                    damage);
            var clearTargetIdOnWire = sameClanAreaGate || rvrSameClanGate;
            if (clearTargetIdOnWire)
                damage = 0;

            var adultMountDamage = 0;
            var adultMountAttached = false;
            if (targetParticipant is not null && (damage > 0 || rvrSameClanGate))
            {
                var mountOffset = LegacyAccountSnapshot.MobEquipmentOffset + (14 * LegacyItem.SizeInBytes);
                if (targetMob.Length >= mountOffset + LegacyItem.SizeInBytes)
                {
                    var mountItem = LegacyItem.Read(targetMob.AsSpan(mountOffset, LegacyItem.SizeInBytes));
                    var mountHp = BinaryPrimitives.ReadInt16LittleEndian(targetMob.AsSpan(mountOffset + 2, sizeof(short)));
                    if (mountItem.Index is >= 2360 and < 2390 && mountHp > 0)
                    {
                        var playerDamage = (damage * 3) >> 2;
                        adultMountDamage = damage - playerDamage;
                        if (playerDamage <= 0) playerDamage = 1;
                        damage = playerDamage;
                        adultMountAttached = true;
                    }
                }
            }

            var hasManaControlAffect = false;
            for (var affectIndex = 0; affectIndex < targetAffect.Length / 8; affectIndex++)
            {
                if (targetAffect[affectIndex * 8] != 18)
                    continue;

                hasManaControlAffect = true;
                break;
            }
            var manaControl = sameClanAreaGate
                ? new LegacyManaControlResult(false, damage, targetState.CurrentScore.Mp, secondaryDamage)
                : LegacyManaControlMath.Resolve(
                    damage,
                    targetState.CurrentScore.Mp,
                    targetState.CurrentScore.MaxMp,
                    hasManaControlAffect,
                    (attackerState.LearnedSkill & (1u << 23)) != 0,
                    doubleCritical,
                    secondaryDamage);
            damage = manaControl.Damage;
            secondaryDamage = manaControl.SecondaryDamage;
            if (!sameClanAreaGate)
                actionState.UpdateSecondaryDamage(secondaryDamage);
            if (manaControl.Applied)
                BinaryPrimitives.WriteInt32LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentMpOffset), manaControl.CurrentMana);

            // TMProject 7.69 advances the Zard quest after the final target damage
            // is computed, then sets only the local dam variable to zero. The
            // serialized target damage remains intact; that zero suppresses Type 8
            // bit 3 HP absorption for the attacker.
            var zardMobLeftCount = ApplyZardQuestLocked(attacker, targetMob, out var zardRewardDrop, out var zardPrivateNotice);
            var remainingHp = damage <= 0 ? targetState.CurrentScore.Hp : Math.Max(0, targetState.CurrentScore.Hp - damage);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), remainingHp);
            var targetRevived = false;
            var consumedItemSlot = -1;
            LegacyItem? consumedItem = null;
            if (targetParticipant is not null)
            {
                targetParticipant.RequestedHp = damage > 0
                    ? Math.Max(0, targetParticipant.RequestedHp - damage)
                    : targetParticipant.RequestedHp;
            }
            var attackerHpAbsorbed = zardMobLeftCount.HasValue ? 0 : ApplyPvpJewelAbsorptionLocked(attacker, damage);
            var targetRemoved = false;
            LegacyPistaTransition? pistaTransition = null;
            long experienceAwarded = 0;
            byte[]? attackerMobSnapshot = null;
            IReadOnlyList<LegacyExperienceAward> experienceAwards = [];
            IReadOnlyList<LegacyItemDrop> itemDrops = zardRewardDrop is { } physicalZardReward ? [physicalZardReward] : [];
            IReadOnlyList<LegacyCoinUpdate> coinUpdates = [];
            IReadOnlyList<LegacyPrivateNotice> privateNotices = zardPrivateNotice is { } physicalZardNotice ? [physicalZardNotice] : [];
            IReadOnlyList<LegacyGlobalNotice> globalNotices = [];
            IReadOnlyList<LegacyAreaNotice> areaNotices = [];
            var mountOwnerConnectionId = 0;
            LegacyItem? updatedMountItem = null;
            var mountDurabilityLoss = 0;
            if (adultMountAttached && targetParticipant is not null && TryProcessAdultMountLocked(targetParticipant, adultMountDamage / 2, out updatedMountItem))
            {
                mountOwnerConnectionId = targetParticipant.ConnectionId;
                mountDurabilityLoss = adultMountDamage / 2;
            }
            IReadOnlyList<LegacyAttackViewRemovalEffect> summonPartyRemovalEffects = [];
            if (targetSummon is not null)
            {
                if (remainingHp > 0)
                    TryLinkMountHpLocked(targetSummon, targetMob, out mountOwnerConnectionId, out updatedMountItem);
                summons[targetConnectionId] = targetSummon with { MobSnapshot = targetMob, AffectSnapshot = targetAffect };
                if (!targetRemoved && sourceRetaliationTailReached && remainingHp > 0)
                    summonPartyRemovalEffects = ApplySummonAttackAggroLocked(targetConnectionId, attackerConnectionId, slotIndex);
            }
            else if (targetNpc is not null)
            {
                if (!targetRemoved)
                {
                    npcs[targetConnectionId] = targetNpc with
                    {
                        MobSnapshot = targetMob,
                        AffectSnapshot = targetAffect,
                        HolyTouchBaseScore = npcHolyTouchBaseScore,
                    };
                    if (sourceRetaliationTailReached && remainingHp > 0)
                    {
                        // TrySetNpcBattle also walks the attacker's party, so capture
                        // dead-summon removals before that propagation can discard them.
                        summonPartyRemovalEffects = PruneDeadPartySummonsForAttackTailLocked(
                            attackerConnectionId,
                            targetConnectionId,
                            slotIndex);
                        if (targetNpc.GenerateIndex >= 0 && targetNpc.AutoTradeSnapshot is null)
                            TrySetNpcBattle(targetConnectionId, attackerConnectionId);
                    }
                }
            }
            else
            {
                targetParticipant!.Mob = targetMob;
                targetParticipant.Affect = targetAffect;
                if (sourceRetaliationTailReached && remainingHp > 0)
                    summonPartyRemovalEffects = PruneDeadPartySummonsForAttackTailLocked(
                        attackerConnectionId,
                        GetPartyLeaderConnectionIdLocked(targetConnectionId),
                        slotIndex);
            }

            IReadOnlyList<int> targetAffectViewRecipients = secondaryEffectsChanged && targetParticipant is null
                ? Array.AsReadOnly(GetParticipantIdsInViewLocked(targetPositionX, targetPositionY))
                : Array.Empty<int>();
            outcome = new LegacyPhysicalAttackOutcome(targetConnectionId, damage, remainingHp, targetMob.ToArray(), targetRemoved, remainingHp == 0, targetRevived, consumedItemSlot, consumedItem, experienceAwarded, attackerMobSnapshot, experienceAwards, itemDrops, TargetHpChanged: targetParticipant is not null && (damage > 0 || targetRevived), TargetResourceChanged: targetParticipant is not null && manaControl.Applied, AttackerHpAbsorbed: attackerHpAbsorbed, AttackerRequestedHp: attacker.RequestedHp, ForceDamage: forceDamage, PvpDamage: pvpDamage, MountOwnerConnectionId: mountOwnerConnectionId, UpdatedMountItem: updatedMountItem, PkPointGateBlocked: pkPointGateBlocked, CrimeStateUpdates: crimeStateUpdates, TargetAffectSnapshot: secondaryEffectsChanged ? targetAffect.ToArray() : null, ReflectDamage: reflectDamage, ReflectPvp: reflectPvp, MountDurabilityLoss: mountDurabilityLoss, PistaTransition: pistaTransition, GlobalNotices: globalNotices, AreaNotices: areaNotices, CoinUpdates: coinUpdates, PrivateNotices: privateNotices, DoubleCritical: doubleCritical, SecondaryDamage: secondaryDamage, NextProgress: doubleCriticalResult.NextProgress, ClearTargetIdOnWire: clearTargetIdOnWire, ZardMobLeftCount: zardMobLeftCount, ViewRemovalEffects: summonPartyRemovalEffects)
            {
                TargetAffectViewRecipients = targetAffectViewRecipients,
                AttackerHpHealed = bloodHandsHealed,
                AttackerHpHealProcTriggered = bloodHandsProcTriggered,
                AttackerHpViewRecipients = bloodHandsProcTriggered
                    ? Array.AsReadOnly(GetParticipantIdsInViewLocked(attacker.PositionX, attacker.PositionY))
                    : Array.Empty<int>(),
            };
            return LegacyPhysicalAttackResult.Accepted;
        }
    }

    /// <summary>
    /// Resolves the first authoritative elemental/healing skill paths from _MSG_Attack.
    /// The calculation and target HP mutation happen under the world lock; the
    /// client-provided damage remains only a marker (-1), never an amount.
    /// </summary>
    public LegacySkillAttackResult TryApplySkillAttack(
        int attackerConnectionId,
        int targetConnectionId,
        LegacySkillDefinition skill,
        LegacyItemDataTable? itemData,
        int weather,
        int randomFactor,
        out LegacySkillAttackOutcome? outcome,
        int affectRandomFactor = -1,
        int instanceRandomFactor = -1,
        int burnRandomFactor = -1,
        int attackMissRoll = -1,
        int resurrectionRoll = -1,
        int resurrectionHpRoll = -1,
        int recallCityRandomX = -1,
        int recallCityRandomY = -1,
        int recallNewbieRandomX = -1,
        int recallNewbieRandomY = -1,
        int specialDropRoll = -1,
        int specialItemRoll = -1,
        int eventDropRoll = -1,
        int runeRoll = -1,
        int runeChanceRoll = -1,
        int pistaRandomRoll = -1,
        int targetIndex = -1,
        uint clientTick = ClientTickPolicy.SkipCheckTick,
        int frostRoll = -1,
        int drainRoll = -1)
    {
        ArgumentNullException.ThrowIfNull(skill);
        var request = new LegacyAttackActionBatchRequest(
            attackerConnectionId,
            [new LegacyAttackActionBatchSlotRequest(
                0,
                LegacyAttackActionSlotKind.Skill,
                targetConnectionId,
                SkillRolls: new LegacySkillAttackBatchSlotRolls(
                    RandomFactor: randomFactor,
                    AffectRandomFactor: affectRandomFactor,
                    InstanceRandomFactor: instanceRandomFactor,
                    BurnRandomFactor: burnRandomFactor,
                    AttackMissRoll: attackMissRoll,
                    ResurrectionRoll: resurrectionRoll,
                    ResurrectionHpRoll: resurrectionHpRoll,
                    RecallCityRandomX: recallCityRandomX,
                    RecallCityRandomY: recallCityRandomY,
                    RecallNewbieRandomX: recallNewbieRandomX,
                    RecallNewbieRandomY: recallNewbieRandomY,
                    SpecialDropRoll: specialDropRoll,
                    SpecialItemRoll: specialItemRoll,
                    EventDropRoll: eventDropRoll,
                    RuneRoll: runeRoll,
                    RuneChanceRoll: runeChanceRoll,
                    PistaRandomRoll: pistaRandomRoll,
                    FrostRoll: frostRoll,
                    DrainRoll: drainRoll),
                SkillTargetIndex: targetIndex)],
            Skill: skill,
            ItemData: itemData,
            Weather: weather,
            ClientTick: clientTick);
        if (!TryApplyAttackActionBatch(request, out var batch) || batch is null || batch.SlotOutcomes.Count != 1)
        {
            outcome = null;
            return LegacySkillAttackResult.ParticipantNotFound;
        }

        var slot = batch.SlotOutcomes[0];
        outcome = slot.SkillOutcome;
        return slot.SkillResult ?? LegacySkillAttackResult.ParticipantNotFound;
    }

    private LegacySkillAttackResult TryApplySkillAttackLocked(
        int attackerConnectionId,
        int targetConnectionId,
        LegacySkillDefinition skill,
        LegacyItemDataTable? itemData,
        int weather,
        int randomFactor,
        out LegacySkillAttackOutcome? outcome,
        bool deferDeathResolution,
        bool applySkillEffects = true,
        int affectRandomFactor = -1,
        int instanceRandomFactor = -1,
        int burnRandomFactor = -1,
        int attackMissRoll = -1,
        int resurrectionRoll = -1,
        int resurrectionHpRoll = -1,
        int recallCityRandomX = -1,
        int recallCityRandomY = -1,
        int recallNewbieRandomX = -1,
        int recallNewbieRandomY = -1,
        int specialDropRoll = -1,
        int specialItemRoll = -1,
        int eventDropRoll = -1,
        int runeRoll = -1,
        int runeChanceRoll = -1,
        int pistaRandomRoll = -1,
        int targetIndex = -1,
        int attackActionSlotIndex = -1,
        uint clientTick = ClientTickPolicy.SkipCheckTick,
        int judgmentDivineStartingHp = 0,
        int frostRoll = -1,
        int drainRoll = -1)
    {
        ArgumentNullException.ThrowIfNull(skill);
        if (attackMissRoll is < -1 or > 99)
            throw new ArgumentOutOfRangeException(nameof(attackMissRoll), attackMissRoll, "The injected 7.69 attack-miss roll must be -1 or between 0 and 99.");
        if (skill.Id == LegacyFuryDivineMath.SkillId && instanceRandomFactor is < -1 or > 99)
            throw new ArgumentOutOfRangeException(nameof(instanceRandomFactor), instanceRandomFactor, "The injected 7.69 Furia Divina roll must be -1 or between 0 and 99.");
        if (skill.Id == 31 && resurrectionRoll is < -1 or > 99)
            throw new ArgumentOutOfRangeException(nameof(resurrectionRoll), resurrectionRoll, "The injected Renascimento success roll must be -1 or between 0 and 99.");
        if (skill.Id == 31 && resurrectionHpRoll is < -1 or > 9)
            throw new ArgumentOutOfRangeException(nameof(resurrectionHpRoll), resurrectionHpRoll, "The injected Renascimento HP roll must be -1 or between 0 and 9.");
        if (frostRoll is < -1 or > 1)
            throw new ArgumentOutOfRangeException(nameof(frostRoll), frostRoll, "The injected RSV_FROST roll must be -1 or 0 or 1.");
        if (drainRoll is < -1 or > 1)
            throw new ArgumentOutOfRangeException(nameof(drainRoll), drainRoll, "The injected RSV_DRAIN roll must be -1 or 0 or 1.");
        outcome = null;
        var isHealing = skill.InstanceType == 6;
        var isDetox = skill.InstanceType == 8;
        var isSummon = skill.InstanceType == 9;
        var isNpcSummon = skill.InstanceType == 11;
        var isEtherealFlame = skill.InstanceType == 12;
        var isInvisibility = skill.InstanceType == 10;
        var isRenascimento = skill.Id == 31;
        var isResurrection = skill.Id == 99;
        var isEffectOnly = skill.InstanceType == 0 && (skill.AffectType > 0 || skill.TickType > 0);
        var isSupport = isHealing || isDetox || isSummon || isNpcSummon || isEtherealFlame || isInvisibility || isRenascimento || isResurrection || isEffectOnly;
        var isBeneficial = isHealing || isDetox || isSummon || isNpcSummon || isInvisibility || isRenascimento || isResurrection || (isEffectOnly && skill.Aggressive == 0);
        if (applySkillEffects && !isSupport && skill.InstanceType != 7) ArgumentNullException.ThrowIfNull(itemData);

            if (!participants.TryGetValue(attackerConnectionId, out var attacker))
                return LegacySkillAttackResult.ParticipantNotFound;
            if (IsAttackTargetBlockedByNpcHeadAreaLocked(targetConnectionId))
                return LegacySkillAttackResult.NpcHeadAreaBlocked;
            if (npcs.TryGetValue(targetConnectionId, out var towerNpcTarget))
            {
                if (attacker.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize ||
                    towerNpcTarget.MobSnapshot.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                    return LegacySkillAttackResult.CombatStateUnavailable;
                if (LegacyAttackTowerGate.IsBlocked(towerNpcTarget.GenerateIndex, attacker.Mob, towerNpcTarget.MobSnapshot))
                    return LegacySkillAttackResult.TowerAttackBlocked;
            }
            if (!participants.TryGetValue(targetConnectionId, out var target))
            {
                if (summons.TryGetValue(targetConnectionId, out var partySummonTarget))
                {
                    if (LegacyAttackTargetLimitGate.IsExceeded(targetIndex, skill.MaxTarget, -1, clientTick))
                        return ValidateSummonTargetLimitLocked(attacker, partySummonTarget, skill);

                    var summonPartyResult = ValidateSummonPartySkillTargetLocked(attacker, partySummonTarget, skill);
                    if (summonPartyResult != LegacySkillAttackResult.Accepted)
                        return summonPartyResult;

                    if (!applySkillEffects || isSupport || isBeneficial || isEffectOnly ||
                        (skill.Id != 79 && skill.InstanceType is not (>= 1 and <= 5) and not 0))
                        return LegacySkillAttackResult.UnsupportedSkillType;

                    return TryApplySummonSkillAttackLocked(
                        attacker,
                        partySummonTarget,
                        targetConnectionId,
                        skill,
                        itemData!,
                        weather,
                        randomFactor,
                        instanceRandomFactor,
                        attackMissRoll,
                        frostRoll,
                        drainRoll,
                        deferDeathResolution,
                        judgmentDivineStartingHp,
                        attackActionSlotIndex,
                        out outcome);
                }

                if (npcs.TryGetValue(targetConnectionId, out var partyNpcTarget))
                {
                    if (LegacyAttackTargetLimitGate.IsExceeded(targetIndex, skill.MaxTarget, -1, clientTick))
                        return ValidateNpcTargetLimitLocked(attacker, partyNpcTarget, skill);

                    var partyResult = ValidateNpcPartySkillTargetLocked(attacker, partyNpcTarget, skill);
                    if (partyResult != LegacySkillAttackResult.Accepted)
                        return partyResult;
                }

                if (!npcs.ContainsKey(targetConnectionId))
                    return LegacySkillAttackResult.ParticipantNotFound;
                if (!applySkillEffects)
                    return LegacySkillAttackResult.UnsupportedSkillType;
                if (isRenascimento)
                    return LegacySkillAttackResult.UnsupportedSkillType;

                if (npcs.TryGetValue(targetConnectionId, out var flashNpcTarget) && skill.InstanceType == 7)
                    return TryApplyNpcFlashLocked(flashNpcTarget, targetConnectionId, out outcome);
                if (npcs.TryGetValue(targetConnectionId, out var flameNpcTarget) && isEtherealFlame)
                    return TryApplyNpcEtherealFlameLocked(attackerConnectionId, attacker, flameNpcTarget, targetConnectionId, instanceRandomFactor, out outcome);
                if (npcs.TryGetValue(targetConnectionId, out var healingNpcTarget) && isHealing)
                    return TryApplyNpcHealingLocked(attacker, healingNpcTarget, targetConnectionId, skill, out outcome);
                if (npcs.TryGetValue(targetConnectionId, out var detoxNpcTarget) && isDetox)
                    return TryApplyNpcDetoxLocked(attacker, detoxNpcTarget, targetConnectionId, out outcome);
                if (npcs.TryGetValue(targetConnectionId, out var effectNpcTarget) && isEffectOnly)
                    return TryApplyNpcEffectSkillAttackLocked(attacker, effectNpcTarget, targetConnectionId, skill, affectRandomFactor, out outcome);
                if (npcs.TryGetValue(targetConnectionId, out var npcTarget) && !isSupport && !isBeneficial)
                    return TryApplyNpcSkillAttackLocked(attacker, npcTarget, targetConnectionId, skill, itemData!, weather, randomFactor, affectRandomFactor, instanceRandomFactor, specialDropRoll, specialItemRoll, eventDropRoll, runeRoll, runeChanceRoll, pistaRandomRoll, attackActionSlotIndex, deferDeathResolution, judgmentDivineStartingHp, frostRoll, drainRoll, out outcome);
                return LegacySkillAttackResult.ParticipantNotFound;
            }
            if (attacker.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize || target.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                return LegacySkillAttackResult.CombatStateUnavailable;

            var attackerState = LegacyMobCombatState.Read(attacker.Mob);
            var targetState = LegacyMobCombatState.Read(target.Mob);
            var targetValidation = ValidatePlayerSkillTargetLocked(
                attacker,
                target,
                skill,
                targetIndex,
                clientTick,
                attackerState,
                targetState);
            if (targetValidation != LegacySkillAttackResult.Accepted)
                return targetValidation;
            if (!applySkillEffects)
                return LegacySkillAttackResult.UnsupportedSkillType;
            if (isRenascimento)
            {
                // TMProject 7.69 draws the HP roll before the 70% success roll,
                // zeros only CurrentScore.Mp, then sends SetHpMp (whose ReqMp
                // helper clamps CurrentScore.Mp without replacing RequestedMana).
                var hpRoll = resurrectionHpRoll >= 0
                    ? resurrectionHpRoll
                    : System.Security.Cryptography.RandomNumberGenerator.GetInt32(10);
                var hpPercent = (attackerState.CurrentScore.MaxHp + 1) / 100;
                var revivedHp = (hpRoll + 10) * hpPercent;
                BinaryPrimitives.WriteInt32LittleEndian(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentMpOffset), 0);

                var reviveRoll = resurrectionRoll >= 0
                    ? resurrectionRoll
                    : System.Security.Cryptography.RandomNumberGenerator.GetInt32(100);
                if (reviveRoll >= 70)
                {
                    outcome = new LegacySkillAttackOutcome(
                        targetConnectionId,
                        0,
                        0,
                        0,
                        targetState.CurrentScore.Hp,
                        target.Mob.ToArray(),
                        AttackerResourceChanged: true);
                    return LegacySkillAttackResult.Accepted;
                }

                BinaryPrimitives.WriteInt32LittleEndian(
                    target.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24),
                    revivedHp);

                byte[]? targetAffectSnapshot = null;
                var affectFreeSlot = -1;
                var hasType55 = false;
                for (var slot = 0; slot < LegacyAccountSnapshot.AffectStride / 8; slot++)
                {
                    var type = target.Affect[slot * 8];
                    if (type == 55)
                    {
                        hasType55 = true;
                        break;
                    }
                    if (type == 0 && affectFreeSlot < 0)
                        affectFreeSlot = slot;
                }

                if (!hasType55 && affectFreeSlot >= 0)
                {
                    target.Affect = target.Affect.ToArray();
                    var affectOffset = affectFreeSlot * 8;
                    target.Affect[affectOffset] = 55;
                    target.Affect[affectOffset + 1] = 0;
                    BinaryPrimitives.WriteUInt16LittleEndian(target.Affect.AsSpan(affectOffset + 2), 0);
                    BinaryPrimitives.WriteUInt32LittleEndian(target.Affect.AsSpan(affectOffset + 4), 2);
                    targetAffectSnapshot = target.Affect.ToArray();
                }

                outcome = new LegacySkillAttackOutcome(
                    targetConnectionId,
                    0,
                    0,
                    0,
                    revivedHp,
                    target.Mob.ToArray(),
                    targetAffectSnapshot,
                    TargetResourceChanged: true,
                    AttackerResourceChanged: true);
                return LegacySkillAttackResult.Accepted;
            }
            if (isResurrection)
            {
                if (attackerState.CurrentScore.Hp > 0)
                {
                    outcome = new LegacySkillAttackOutcome(attackerConnectionId, 0, 0, 0, attackerState.CurrentScore.Hp, attacker.Mob.ToArray());
                    return LegacySkillAttackResult.Accepted;
                }

                var reviveRoll = resurrectionRoll >= 0
                    ? resurrectionRoll
                    : System.Security.Cryptography.RandomNumberGenerator.GetInt32(115);
                if (reviveRoll > 100)
                    reviveRoll -= 15;

                // The common attack prelude has already charged configured mana.
                // Skill 99 then applies its final 1..50% HP roll without writing MP again.
                var hasRecallPosition = false;
                short recallPositionX = 0;
                short recallPositionY = 0;
                if (reviveRoll >= 40)
                {
                    BinaryPrimitives.WriteInt32LittleEndian(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), 2);
                    (recallPositionX, recallPositionY) = GetLegacyRecallPosition(
                        attacker,
                        attackerState,
                        recallCityRandomX,
                        recallCityRandomY,
                        recallNewbieRandomX,
                        recallNewbieRandomY);
                    attacker.PositionX = recallPositionX;
                    attacker.PositionY = recallPositionY;
                    hasRecallPosition = true;
                }

                var maxHpPercent = Math.Max(0, (attackerState.CurrentScore.MaxHp + 1) / 100);
                var hpRoll = resurrectionHpRoll >= 0
                    ? resurrectionHpRoll
                    : System.Security.Cryptography.RandomNumberGenerator.GetInt32(50);
                var revivedHp = (Math.Clamp(hpRoll, 0, 49) + 1) * maxHpPercent;
                BinaryPrimitives.WriteInt32LittleEndian(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), revivedHp);
                outcome = new LegacySkillAttackOutcome(attackerConnectionId, 0, 0, 0, revivedHp, attacker.Mob.ToArray(), UpdatesAttackerState: true, HasRecallPosition: hasRecallPosition, RecallPositionX: recallPositionX, RecallPositionY: recallPositionY);
                return LegacySkillAttackResult.Accepted;
            }

            if (targetState.CurrentScore.Hp <= 0)
                return LegacySkillAttackResult.TargetNotAlive;

            var distance = LegacyCombatMath.GetDistance(attacker.PositionX, attacker.PositionY, target.PositionX, target.PositionY);
            if (distance > 23)
                return LegacySkillAttackResult.OutOfRange;

            if (isInvisibility)
            {
                var updates = new List<LegacyNpcCombatStateUpdate>();
                foreach (var (npcConnectionId, npc) in npcs.ToArray())
                {
                    if (npc.Mode != LegacyNpcMode.Combat || npc.CurrentTarget != targetConnectionId)
                        continue;

                    var enemyList = npc.EnemyList
                        .Select(enemyId => enemyId == targetConnectionId ? attackerConnectionId : enemyId)
                        .ToArray();
                    npcs[npcConnectionId] = npc with
                    {
                        CurrentTarget = attackerConnectionId,
                        EnemyList = enemyList,
                    };
                    updates.Add(new LegacyNpcCombatStateUpdate(npcConnectionId, npc.Mode, attackerConnectionId, enemyList));
                }

                outcome = new LegacySkillAttackOutcome(
                    targetConnectionId,
                    0,
                    0,
                    0,
                    targetState.CurrentScore.Hp,
                    target.Mob.ToArray(),
                    NpcCombatStateUpdates: updates);
                return LegacySkillAttackResult.Accepted;
            }

            if (isSummon)
            {
                var mapAttribute = mapGrid?.GetAttribute(attacker.PositionX, attacker.PositionY) ?? attacker.MapAttribute;
                if ((mapAttribute & 0x04) != 0 && attackerState.CurrentScore.Level < 1000)
                    return LegacySkillAttackResult.SummonNotAllowedHere;

                // 7.69 comments out the W2PP `Hp > Hp + Special + 30` check and gates summons by sector and level.
                if (attacker.PositionX / 128 == 13 && attacker.PositionY / 128 == 15)
                    return LegacySkillAttackResult.SummonNotAllowedHere;
                if (targetState.CurrentScore.Level > attackerState.CurrentScore.Level)
                    return LegacySkillAttackResult.TargetTooHighToSummon;

                if ((target.PositionX & 0xFF00) == 0 && (target.PositionY & 0xFF00) == 0)
                    return LegacySkillAttackResult.SummonInvalidPosition;
                if (LegacySkillCombatMath.IsSummonTargetZoneBlocked(target.PositionX, target.PositionY))
                    return LegacySkillAttackResult.SummonZoneBlocked;

                var summonX = (int)attacker.PositionX;
                var summonY = (int)attacker.PositionY;
                var foundPosition = LegacyMobGridSearch.TryFindEmpty(
                    targetConnectionId,
                    ref summonX,
                    ref summonY,
                    GetParticipantAtPosition,
                    mapGrid is null ? static (_, _) => false : mapGrid.IsBlocked,
                    checkCandidateTerrain: mapCollisionMode == LegacyMapCollisionMode.CandidateAware);
                if (foundPosition)
                {
                    target.PositionX = checked((short)summonX);
                    target.PositionY = checked((short)summonY);
                }

                outcome = new LegacySkillAttackOutcome(
                    targetConnectionId,
                    0,
                    0,
                    0,
                    targetState.CurrentScore.Hp,
                    target.Mob.ToArray(),
                    HasTargetPosition: foundPosition,
                    TargetPositionX: checked((short)summonX),
                    TargetPositionY: checked((short)summonY));
                return LegacySkillAttackResult.Accepted;
            }

            if (isNpcSummon)
            {
                var requestedSummons = GetLegacySummonCount(skill.InstanceValue, attackerState.CurrentScore.Special3);
                var partyLeaderConnectionId = GetPartyLeaderConnectionIdLocked(attackerConnectionId);
                var summonResult = TryGenerateSummons(
                    partyLeaderConnectionId,
                    attackerConnectionId,
                    skill.InstanceValue - 1,
                    requestedSummons,
                    attacker,
                    attackerState,
                    out var createdSummons,
                    out var recalledSummons,
                    out var summonPartyAdds);
                var refunded = summonResult is not LegacySummonGenerationResult.Created;
                if (refunded)
                    RefundSkillMana(attacker, skill, attackerState);

                outcome = new LegacySkillAttackOutcome(
                    targetConnectionId,
                    0,
                    0,
                    0,
                    targetState.CurrentScore.Hp,
                    target.Mob.ToArray(),
                    SummonedMobs: createdSummons,
                    SummonResult: summonResult,
                    AttackerResourceChanged: refunded,
                    // Language.txt:33 _NN_Party_Full_Cant_Summon; GenerateSummon returns 0 (MP restored).
                    PrivateNotices: summonResult == LegacySummonGenerationResult.PartyFull
                        ? [new LegacyPrivateNotice(attackerConnectionId, LegacyPartyFullCantSummonMessage)]
                        : null,
                    PartyAddNotices: summonPartyAdds,
                    RecalledSummons: recalledSummons);
                return LegacySkillAttackResult.Accepted;
            }

            if (isEtherealFlame)
            {
                var instanceRoll = instanceRandomFactor >= 0
                    ? instanceRandomFactor
                    : System.Security.Cryptography.RandomNumberGenerator.GetInt32(100);
                var burnChance = (attackerState.BaseScore.Special2 + 1) / 7;
                if (instanceRoll > burnChance)
                {
                    var burnRoll = burnRandomFactor >= 0
                        ? burnRandomFactor
                        : System.Security.Cryptography.RandomNumberGenerator.GetInt32(10);
                    var burnMana = ((targetState.CurrentMana + 1) / 100) * (10 + Math.Clamp(burnRoll, 0, 9));
                    var remainingMana = burnMana > targetState.CurrentMana ? 0 : targetState.CurrentMana - burnMana;
                    // TMProject 7.69 (_MSG_Attack.cpp:1321-1340) only lowers CurrentScore.Mp and calls
                    // SendSetHpMp/SendScore; W2PP also overwrites ReqMp, which the port does not follow.
                    BinaryPrimitives.WriteInt32LittleEndian(target.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentMpOffset), remainingMana);
                    outcome = new LegacySkillAttackOutcome(
                        targetConnectionId,
                        0,
                        0,
                        0,
                        targetState.CurrentScore.Hp,
                        target.Mob.ToArray(),
                        TargetResourceChanged: true,
                        PreserveSkillMarkerOnWire: true);
                    return LegacySkillAttackResult.Accepted;
                }

                // TMProject 7.69 (_MSG_Attack.cpp:1342-1362) clears the whole record of every Type
                // 18/16/14/19/32 affect (W2PP omits 32), then always sends SendEquip plus a rebuilt
                // GetCurrentScore/SendScore, so removing a transformation restores score and visuals.
                if (itemData is null)
                    return LegacySkillAttackResult.CombatStateUnavailable;

                var nextAffect = target.Affect.ToArray();
                for (var slot = 0; slot < LegacyAccountSnapshot.AffectStride / 8; slot++)
                {
                    var offset = slot * 8;
                    if (nextAffect[offset] is 18 or 16 or 14 or 19 or 32)
                        nextAffect.AsSpan(offset, 8).Clear();
                }

                LegacyCharacterLoginData projectedFlameTarget;
                try
                {
                    projectedFlameTarget = ProjectPlayerScoreLocked(target, target.Mob, nextAffect, itemData);
                }
                catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException)
                {
                    return LegacySkillAttackResult.CombatStateUnavailable;
                }

                target.Mob = projectedFlameTarget.Mob;
                target.Affect = projectedFlameTarget.Affect;
                target.MobExtra = projectedFlameTarget.MobExtra;
                if (projectedFlameTarget.ClientEquipment is not null)
                    target.ClientEquipment = projectedFlameTarget.ClientEquipment.ToArray();

                outcome = new LegacySkillAttackOutcome(
                    targetConnectionId,
                    0,
                    0,
                    0,
                    LegacyMobCombatState.Read(target.Mob).CurrentScore.Hp,
                    target.Mob.ToArray(),
                    target.Affect.ToArray(),
                    TargetEquipmentChanged: true,
                    PreserveSkillMarkerOnWire: true);
                return LegacySkillAttackResult.Accepted;
            }

            if (skill.Id != 79 && skill.InstanceType is not (>= 1 and <= 5) and not 6 and not 8 and not 0 and not 12)
                return LegacySkillAttackResult.UnsupportedSkillType;

            if (isHealing)
            {
                if (target.Clan == 4)
                    return LegacySkillAttackResult.TargetClanBlocked;

                // 7.69 clears the heal when the caster is outside a 0x40 cell and the target is inside one.
                var casterAttribute = mapGrid?.GetAttribute(attacker.PositionX, attacker.PositionY) ?? attacker.MapAttribute;
                var healTargetAttribute = mapGrid?.GetAttribute(target.PositionX, target.PositionY) ?? target.MapAttribute;
                if ((casterAttribute & 0x40) == 0 && (healTargetAttribute & 0x40) != 0)
                {
                    outcome = new LegacySkillAttackOutcome(targetConnectionId, 0, 0, 0, targetState.CurrentScore.Hp, target.Mob.ToArray());
                    return LegacySkillAttackResult.Accepted;
                }

                var healing = LegacySkillCombatMath.GetHealAmount(skill, attackerState, attacker.ClassMaster);

                var targetMount = LegacyItem.Read(target.Mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (13 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
                var divisor = targetMount.Index switch
                {
                    786 => Math.Max(2, (int)targetMount.Value1),
                    1936 => Math.Max(2, (int)targetMount.Value1) * 100,
                    1937 => Math.Max(2, (int)targetMount.Value1) * 20_000,
                    _ => 1,
                };
                var appliedHealing = healing / divisor;
                var healingRemainingHp = Math.Min(targetState.CurrentScore.MaxHp, targetState.CurrentScore.Hp + appliedHealing);
                BinaryPrimitives.WriteInt32LittleEndian(target.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), healingRemainingHp);
                target.RequestedHp = Math.Min(targetState.CurrentScore.MaxHp, target.RequestedHp + appliedHealing);
                var healingExperience = LegacyAttackHealExperienceMath.GetContribution(
                    attacker.ConnectionId,
                    targetConnectionId,
                    target.PositionX,
                    target.PositionY,
                    targetState.CurrentScore.Hp,
                    healingRemainingHp);
                outcome = new LegacySkillAttackOutcome(
                    targetConnectionId,
                    healing,
                    -appliedHealing,
                    0,
                    healingRemainingHp,
                    target.Mob.ToArray(),
                    TargetHpChanged: appliedHealing > 0,
                    HealingExperienceContribution: healingExperience);
                return LegacySkillAttackResult.Accepted;
            }

            if (isDetox)
            {
                var previousPlayerAffect = target.Affect.ToArray();
                var nextPlayerAffect = previousPlayerAffect.ToArray();
                var changed = false;
                for (var slot = 0; slot < LegacyAccountSnapshot.AffectStride / 8; slot++)
                {
                    var offset = slot * 8;
                    var type = nextPlayerAffect[offset];
                    if (type is 1 or 3 or 5 or 7 or 10 or 12 or 20 || (type == 32 && (attackerState.LearnedSkill & (1u << 7)) != 0))
                    {
                        nextPlayerAffect.AsSpan(offset, 8).Clear();
                        changed = true;
                    }
                }

                if (changed && HasLegacyAffectType(previousPlayerAffect, type: 3))
                {
                    if (itemData is null)
                        return LegacySkillAttackResult.CombatStateUnavailable;

                    LegacyCharacterLoginData projected;
                    try
                    {
                        projected = ProjectPlayerScoreLocked(target, target.Mob, nextPlayerAffect, itemData);
                    }
                    catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException)
                    {
                        return LegacySkillAttackResult.CombatStateUnavailable;
                    }

                    target.Mob = projected.Mob;
                    target.Affect = projected.Affect;
                    target.MobExtra = projected.MobExtra;
                    if (projected.ClientEquipment is not null)
                        target.ClientEquipment = projected.ClientEquipment.ToArray();
                }
                else if (changed)
                {
                    target.Affect = nextPlayerAffect;
                }

                outcome = new LegacySkillAttackOutcome(
                    targetConnectionId,
                    0,
                    0,
                    0,
                    LegacyMobCombatState.Read(target.Mob).CurrentScore.Hp,
                    target.Mob.ToArray(),
                    changed ? target.Affect.ToArray() : null);
                return LegacySkillAttackResult.Accepted;
            }

            if (isEffectOnly)
            {
                if (skill.Id == 47 && TryCancelFirstLegacyAffect(target.Affect, target.Mob, affectType: 19))
                {
                    outcome = new LegacySkillAttackOutcome(
                        targetConnectionId,
                        0,
                        0,
                        0,
                        targetState.CurrentScore.Hp,
                        target.Mob.ToArray(),
                        target.Affect.ToArray(),
                        StopRemainingTargets: true);
                    return LegacySkillAttackResult.Accepted;
                }

                var isTransformationSkill = skill.Id is 64 or 66 or 68 or 70 or 71;
                var isSelfTransformation = isTransformationSkill && targetConnectionId == attackerConnectionId;
                var rebuildTargetScore = skill.Id is 41 or 44 || isTransformationSkill ||
                    IsAuditedType4PlayerSkill(skill) ||
                    IsAuditedType1PlayerSkill(skill) || IsAuditedType3PlayerSkill(skill) ||
                    IsAuditedType7PlayerSkill(skill) || IsAuditedType10PlayerSkill(skill) ||
                    IsAuditedType11PlayerSkill(skill) ||
                    IsAuditedType19PlayerSkill(skill) ||
                    IsAuditedType21PlayerSkill(skill) ||
                    IsAuditedType24SamaritanPlayerSkill(skill) ||
                    IsAuditedType25PlayerSkill(skill) ||
                    IsAuditedType31PlayerSkill(skill) ||
                    IsAuditedType38PlayerSkill(skill) ||
                    IsAuditedType42PlayerSkill(skill) ||
                    IsAuditedType28PlayerSkill(skill) ||
                    IsAuditedType26PlayerSkill(skill) ||
                    IsAuditedType27PlayerSkill(skill) ||
                    IsAuditedType36PlayerSkill(skill) ||
                    IsAuditedType29PlayerSkill(skill) ||
                    IsAuditedType13PlayerSkill(skill) || IsAuditedType14PlayerSkill(skill) ||
                    IsAuditedType15PlayerSkill(skill);
                if (rebuildTargetScore && itemData is null)
                    return LegacySkillAttackResult.CombatStateUnavailable;

                var supportAffectLevel = LegacySkillCombatMath.GetSkillSpecial(skill, attackerState);
                var supportAffectDelay = LegacySkillCombatMath.GetSkillAffectDelay(skill, attackerState, attacker.ClassMaster);
                var nextPlayerAffect = target.Affect.ToArray();
                var auditedPlayerAffectType = GetAuditedPlayerScoreAffectType(skill);
                var targetHadAuditedAffect = auditedPlayerAffectType != 0 &&
                    HasLegacyAffectType(nextPlayerAffect, auditedPlayerAffectType);
                var effectChanged = CanApplyLegacyAffect(attacker, target, attackerState, targetState, skill, affectRandomFactor)
                    && TryApplyLegacyAffects(nextPlayerAffect, skill, supportAffectDelay, supportAffectLevel);
                if (effectChanged && targetHadAuditedAffect &&
                    (auditedPlayerAffectType is 1 or 3 or 10) && skill.AffectType == auditedPlayerAffectType)
                    ResetLegacyAffectDuration(nextPlayerAffect, auditedPlayerAffectType, time: 4);
                var projectTargetScore = effectChanged || isSelfTransformation;
                if (projectTargetScore)
                {
                    if (rebuildTargetScore)
                    {
                        LegacyCharacterLoginData projected;
                        try
                        {
                            projected = ProjectPlayerScoreLocked(
                                target,
                                target.Mob,
                                nextPlayerAffect,
                                itemData!,
                                preserveHpMp: auditedPlayerAffectType != 0);
                        }
                        catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException)
                        {
                            return LegacySkillAttackResult.CombatStateUnavailable;
                        }

                        target.Mob = projected.Mob;
                        target.Affect = projected.Affect;
                        target.MobExtra = projected.MobExtra;
                        if (projected.ClientEquipment is not null)
                            target.ClientEquipment = projected.ClientEquipment.ToArray();
                        target.Affect = projected.Affect;
                    }
                    else
                    {
                        target.Affect = nextPlayerAffect;
                    }
                }

                outcome = new LegacySkillAttackOutcome(
                    targetConnectionId,
                    0,
                    0,
                    0,
                    LegacyMobCombatState.Read(target.Mob).CurrentScore.Hp,
                    target.Mob.ToArray(),
                    projectTargetScore ? target.Affect.ToArray() : null,
                    TargetEquipmentChanged: isTransformationSkill && projectTargetScore);
                return LegacySkillAttackResult.Accepted;
            }

            var firstWeapon = LegacyItem.Read(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (6 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
            var secondWeapon = LegacyItem.Read(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (7 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
            var weaponDamage = LegacySkillCombatMath.GetWeaponDamage(attackerState, itemData!, firstWeapon, secondWeapon);
            var skillWeather = LegacySkillCombatMath.GetSkillWeather(weather, attacker.PositionX, attacker.PositionY);
            var baseDamage = LegacySkillCombatMath.GetSkillBaseDamage(skill, attackerState, skillWeather, weaponDamage, attackerState.Magic);
            var combat = LegacySkillCombatMath.GetMasterCombat(attackerState);
            var effectiveRandomFactor = randomFactor >= 0
                ? randomFactor
                : System.Security.Cryptography.RandomNumberGenerator.GetInt32(combat + 90, combat + 90 + (21 - Math.Min(combat, 15)));

            int damage;
            var resistance = 0;
            if (skill.Id == 79)
            {
                // 7.69 Tempestade de Raios discards the elemental result and uses the target's CurrentScore.Damage.
                damage = targetState.CurrentScore.Damage;
            }
            else
            {
                var defense = LegacySkillCombatMath.GetSkillDefense(targetState.CurrentScore.Ac, targetIsPlayer: true, targetState.CharacterClass);
                damage = LegacySkillCombatMath.GetSkillDamage(baseDamage, defense, combat, effectiveRandomFactor);
                damage = LegacySkillCombatMath.ApplySkillResistance(
                    skill.Id,
                    skill.InstanceType,
                    damage,
                    targetIsPlayer: true,
                    targetState.CurrentScore.Level,
                    target.ClassMaster,
                    targetState.LearnedSkill,
                    target.Mob.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4),
                    out resistance);
            }

            LegacySkillActionRelay? actionRelay = null;
            if (skill.Id == LegacyFuryDivineMath.SkillId)
            {
                if (!TryCreateFuryDivineActionRelayLocked(
                        attacker,
                        targetConnectionId,
                        target.Mob,
                        target.PositionX,
                        target.PositionY,
                        target.ClassMaster,
                        targetIsNpc: false,
                        targetGenerateIndex: -1,
                        instanceRandomFactor,
                        out actionRelay))
                {
                    outcome = new LegacySkillAttackOutcome(
                        targetConnectionId,
                        baseDamage,
                        0,
                        resistance,
                        targetState.CurrentScore.Hp,
                        target.Mob.ToArray(),
                        StopRemainingTargets: true);
                    return LegacySkillAttackResult.Accepted;
                }

                // TMProject 7.69 replaces generic skill damage with 1 before
                // the shared ForceDamage/PvPDamage, miss, and protection stages.
                damage = 1;
            }
            if (skill.Id == 22)
            {
                damage = LegacySkillCombatMath.AddExterminarManaBonus(
                    damage,
                    attackerState.CurrentMana,
                    attackerState.CurrentScore.Intelligence);
                BinaryPrimitives.WriteInt32LittleEndian(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentMpOffset), 0);
                actionRelay = TryCreateExterminarActionRelayLocked(attacker, target.Mob, target.PositionX, target.PositionY);
            }

            if (skill.Id == 30)
            {
                damage += judgmentDivineStartingHp;
                ApplyJudgmentDivineHpCost(attacker, attackerState.CurrentScore.Hp);
            }

            // TMProject 7.69 and W2PP apply equipment ForceDamage, then the
            // player-only PvPDamage percentage, before the MSG_Attack miss gate.
            damage = ApplyLegacyPostDamageModifiers(
                damage,
                GetLegacyForceDamage(
                    attacker.Mob, attacker.ClientEquipment, itemData, attacker.Affect, attackerState.CurrentScore.Special3),
                GetLegacyPvpDamage(attacker.Mob, attacker.ClientEquipment, itemData));

            if (ShouldSuppressLegacyFriendlyFireSkillDamageLocked(
                    attacker,
                    target.ConnectionId,
                    target,
                null,
                target.Mob))
                damage = 0;

            var skillPkGate = LegacySkillPkGate.Resolve(
                ReadLegacyPkPoint(attacker.Mob),
                ReadLegacyPkPoint(target.Mob),
                BinaryPrimitives.ReadUInt16LittleEndian(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset)),
                BinaryPrimitives.ReadUInt16LittleEndian(target.Mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset)),
                attacker.PositionX,
                attacker.PositionY,
                skill.Id,
                damage,
                combatWorldState);
            damage = skillPkGate.Damage;
            var skillPrivateNotices = skillPkGate.SendCannotKillNotice
                ? new[] { new LegacyPrivateNotice(attacker.ConnectionId, LegacySkillPkNotice.CannotKillUser(ReadLegacyPkPoint(attacker.Mob))) }
                : null;
            IReadOnlyList<LegacyCrimeStateUpdate>? skillCrimeStateUpdates = null;
            if (skillPkGate.MarkAttackerGuilty && SetLegacyGuilty(attacker, 8))
                skillCrimeStateUpdates =
                [
                    new LegacyCrimeStateUpdate(attacker.ConnectionId, attacker.PositionX, attacker.PositionY, attacker.Mob.ToArray()),
                ];

            // In TMProject 7.69 the miss roll runs immediately after the PK
            // branch for every other player target, even when that branch zeroed damage.
            if (damage > 0 || skillPkGate.PkPointGateBlocked || skill.Id == 6)
                damage = ResolveLegacyAttackMiss(attackerState, targetState, attackMissRoll, damage);

            // TMProject 7.69 applies Divine/Absolute Protection after miss resolution
            // for positive MSG_Attack damage. W2PP has no equivalent active gate.
            if (damage > 0 && targetState.CharacterClass is 0 or 1 && HasLegacyAffectType(target.Affect, type: 6))
                damage = 0;

            if (LegacyAttackClanSixDamageGate.ShouldSuppressDamage(
                    target.Mob[LegacyAccountSnapshot.MobClanOffset],
                    LegacySkillCombatMath.GetSkillSpecial(skill, attackerState)))
                damage = 0;

            damage = LegacyAttackDamageCap.Apply(damage);
            var sourceRetaliationTailReached = damage > 0;

            // TMProject 7.69 and W2PP apply the player's flat ReflectDamage
            // before the attacker's PvP-jewel Parry penalty, then apply the
            // target's ReflectPvP percentage to the remaining positive damage.
            var reflectDamage = damage > 0
                ? GetLegacyReflectDamage(target.Mob, target.ClientEquipment, targetState, itemData)
                : 0;
            if (reflectDamage > 0)
            {
                damage -= reflectDamage;
                if (damage <= 0)
                    damage = 1;
            }

            if (damage > 0)
            {
                target.RuntimeParryAdjustment = LegacyPvpJewelParryMath.ApplyParryPenalty(
                    attacker.Affect,
                    target.RuntimeParryAdjustment,
                    damage,
                    isPlayerTarget: true);
            }

            var reflectPvp = damage > 0
                ? GetLegacyReflectPvp(target.Mob, target.ClientEquipment, itemData)
                : 0;
            if (reflectPvp > 0)
            {
                damage -= damage / 100 * reflectPvp;
                if (damage <= 0)
                    damage = 1;
            }

            var sameClanAreaGate = LegacyAttackSameClanAreaGate.ShouldClearTargetId(
                attacker.PositionX,
                attacker.PositionY,
                attacker.Mob[LegacyAccountSnapshot.MobClanOffset],
                target.Mob[LegacyAccountSnapshot.MobClanOffset],
                damage);
            if (sameClanAreaGate)
                sourceRetaliationTailReached = false;
            var rvrSameClanGate = !sameClanAreaGate &&
                LegacyAttackRvrSameClanGate.ShouldClearTargetId(
                    attacker.ConnectionId,
                    target.ConnectionId,
                    LegacyBattleRoyaleGridRefreshRelay.MaxUserConnectionExclusive,
                    target.PositionX,
                    target.PositionY,
                    combatWorldState.RvrWarActive,
                    attacker.Mob[LegacyAccountSnapshot.MobClanOffset],
                    target.Mob[LegacyAccountSnapshot.MobClanOffset],
                    damage);
            var clearTargetIdOnWire = sameClanAreaGate || rvrSameClanGate;
            if (clearTargetIdOnWire)
                damage = 0;

            var zardMobLeftCount = ApplyZardQuestLocked(attacker, target.Mob, out var zardRewardDrop, out var zardPrivateNotice);
            if (zardPrivateNotice is { } zardNotice)
                skillPrivateNotices = skillPrivateNotices is { Length: > 0 } existingSkillNotices
                    ? [.. existingSkillNotices, zardNotice]
                    : [zardNotice];
            IReadOnlyList<LegacyItemDrop>? zardItemDrops = zardRewardDrop is { } playerSkillReward ? [playerSkillReward] : null;

            var remainingHp = damage <= 0 ? targetState.CurrentScore.Hp : Math.Max(0, targetState.CurrentScore.Hp - damage);
            BinaryPrimitives.WriteInt32LittleEndian(target.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), remainingHp);
            var targetRevived = false;
            var consumedItemSlot = -1;
            LegacyItem? consumedItem = null;
            if (!deferDeathResolution && remainingHp == 0 && TryConsumeResurrectionScroll(target, out consumedItemSlot, out var revivedItem))
            {
                targetRevived = true;
                consumedItem = revivedItem;
                remainingHp = BinaryPrimitives.ReadInt32LittleEndian(target.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24));
            }
            target.RequestedHp = targetRevived
                ? remainingHp
                : damage > 0
                    ? Math.Max(0, target.RequestedHp - damage)
                    : target.RequestedHp;
            if (!zardMobLeftCount.HasValue)
                ApplyPvpJewelAbsorptionLocked(attacker, damage);
            var effectLevel = LegacySkillCombatMath.GetSkillSpecial(skill, attackerState);
            var effectDelay = LegacySkillCombatMath.GetSkillAffectDelay(skill, attackerState, attacker.ClassMaster);
            var previousTargetAffect = target.Affect.ToArray();
            var nextTargetAffect = previousTargetAffect.ToArray();
            var combatAffectChanged = !sameClanAreaGate && CanApplyLegacyAffect(attacker, target, attackerState, targetState, skill, affectRandomFactor)
                && TryApplyLegacyAffects(nextTargetAffect, skill, effectDelay, effectLevel);
            var secondaryAffectChanged = damage > 0 && TryApplyLegacyRsvSecondaryAttackAffects(
                nextTargetAffect,
                target.Mob,
                attackerState,
                targetState,
                target.ConnectionId,
                frostRoll,
                drainRoll);
            var damagePlayerAffectType = GetAuditedPlayerScoreAffectType(skill);
            var frostAddedType1 = secondaryAffectChanged &&
                !HasLegacyAffectType(previousTargetAffect, type: 1) &&
                HasLegacyAffectType(nextTargetAffect, type: 1);
            var scoreProjectionAffectType = frostAddedType1 ? (byte)1 : damagePlayerAffectType;
            if (scoreProjectionAffectType != 0 &&
                HasLegacyAffectType(nextTargetAffect, scoreProjectionAffectType) &&
                ((combatAffectChanged && damagePlayerAffectType == scoreProjectionAffectType) || frostAddedType1))
            {
                if (HasLegacyAffectType(previousTargetAffect, scoreProjectionAffectType) &&
                    scoreProjectionAffectType is 1 or 3 or 10)
                    ResetLegacyAffectDuration(nextTargetAffect, scoreProjectionAffectType, time: 4);

                LegacyCharacterLoginData projected;
                try
                {
                    projected = ProjectPlayerScoreLocked(
                        target,
                        target.Mob,
                        nextTargetAffect,
                        itemData!,
                        preserveHpMp: true);
                }
                catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException)
                {
                    return LegacySkillAttackResult.CombatStateUnavailable;
                }

                target.Mob = projected.Mob;
                target.Affect = projected.Affect;
                target.MobExtra = projected.MobExtra;
                if (projected.ClientEquipment is not null)
                    target.ClientEquipment = projected.ClientEquipment.ToArray();
            }
            else if (combatAffectChanged || secondaryAffectChanged)
            {
                target.Affect = nextTargetAffect;
            }
            IReadOnlyList<LegacyAttackViewRemovalEffect> viewRemovalEffects = sourceRetaliationTailReached && remainingHp > 0
                ? PruneDeadPartySummonsForAttackTailLocked(
                    attacker.ConnectionId,
                    GetPartyLeaderConnectionIdLocked(target.ConnectionId),
                    attackActionSlotIndex)
                : [];
            outcome = new LegacySkillAttackOutcome(targetConnectionId, baseDamage, damage, resistance, remainingHp, target.Mob.ToArray(), combatAffectChanged || secondaryAffectChanged ? target.Affect.ToArray() : null, TargetResourceChanged: targetRevived, TargetRevived: targetRevived, ConsumedItemSlot: consumedItemSlot, ConsumedItem: consumedItem, TargetHpChanged: damage > 0 || targetRevived, AttackerResourceChanged: skill.Id is 22 or 30, ActionRelay: actionRelay, ClearTargetIdOnWire: clearTargetIdOnWire, PrivateNotices: skillPrivateNotices, CrimeStateUpdates: skillCrimeStateUpdates, ItemDrops: zardItemDrops, ZardMobLeftCount: zardMobLeftCount, ViewRemovalEffects: viewRemovalEffects);
            return LegacySkillAttackResult.Accepted;
    }

    /// <summary>
    /// Runs only the common eligibility checks needed by the legacy max-target
    /// gate. It deliberately does not apply the skill effect.
    /// </summary>
    public LegacySkillAttackResult TryValidateSkillTargetLimit(
        int attackerConnectionId,
        int targetConnectionId,
        LegacySkillDefinition skill,
        int targetIndex,
        uint clientTick)
    {
        ArgumentNullException.ThrowIfNull(skill);
        if (!LegacyAttackTargetLimitGate.IsExceeded(targetIndex, skill.MaxTarget, -1, clientTick))
            return LegacySkillAttackResult.Accepted;

        var isResurrection = skill.Id == 99;

        lock (gate)
        {
            if (!participants.TryGetValue(attackerConnectionId, out var attacker))
                return LegacySkillAttackResult.ParticipantNotFound;
            if (!participants.TryGetValue(targetConnectionId, out var target))
            {
                if (summons.TryGetValue(targetConnectionId, out var summon))
                    return ValidateSummonTargetLimitLocked(attacker, summon, skill);

                if (npcs.TryGetValue(targetConnectionId, out var npc))
                {
                    if (attacker.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize ||
                        npc.MobSnapshot.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                        return LegacySkillAttackResult.CombatStateUnavailable;
                    if (LegacyAttackTowerGate.IsBlocked(npc.GenerateIndex, attacker.Mob, npc.MobSnapshot))
                        return LegacySkillAttackResult.TowerAttackBlocked;
                    return ValidateNpcTargetLimitLocked(attacker, npc, skill);
                }
                return LegacySkillAttackResult.ParticipantNotFound;
            }
            if (attacker.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize || target.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                return LegacySkillAttackResult.CombatStateUnavailable;

            var attackerState = LegacyMobCombatState.Read(attacker.Mob);
            var targetState = LegacyMobCombatState.Read(target.Mob);
            if (!isResurrection && attackerState.CurrentScore.Hp <= 0)
                return LegacySkillAttackResult.AttackerNotAlive;

            return ValidatePlayerTargetLimitLocked(
                attacker,
                target,
                skill,
                targetIndex,
                clientTick,
                targetState.CurrentScore.Hp,
                targetState.CurrentScore.Merchant);
        }
    }

    /// <summary>
    /// Applies bParty eligibility when the skill effect itself is not implemented.
    /// No skill effect is applied. Static NPCs use their connection ID as Leader;
    /// dynamic summons retain separate party Leader and Summoner IDs. GuildDisable is volatile player-session state.
    /// </summary>
    public LegacySkillAttackResult TryValidatePartySkillTarget(
        int attackerConnectionId,
        int targetConnectionId,
        LegacySkillDefinition skill,
        int targetIndex,
        uint clientTick)
    {
        ArgumentNullException.ThrowIfNull(skill);
        if (skill.PartyCheck == 0)
            return LegacySkillAttackResult.Accepted;

        lock (gate)
        {
            if (!participants.TryGetValue(attackerConnectionId, out var attacker))
                return LegacySkillAttackResult.ParticipantNotFound;

            if (participants.TryGetValue(targetConnectionId, out var target))
            {
                if (attacker.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize ||
                    target.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                    return LegacySkillAttackResult.CombatStateUnavailable;

                return ValidatePlayerSkillTargetLocked(
                    attacker,
                    target,
                    skill,
                    targetIndex,
                    clientTick,
                    LegacyMobCombatState.Read(attacker.Mob),
                    LegacyMobCombatState.Read(target.Mob));
            }

            if (npcs.TryGetValue(targetConnectionId, out var npc))
            {
                if (LegacyAttackTargetLimitGate.IsExceeded(targetIndex, skill.MaxTarget, -1, clientTick))
                    return ValidateNpcTargetLimitLocked(attacker, npc, skill);

                return ValidateNpcPartySkillTargetLocked(attacker, npc, skill);
            }

            if (!summons.TryGetValue(targetConnectionId, out var summon))
                return LegacySkillAttackResult.ParticipantNotFound;

            if (LegacyAttackTargetLimitGate.IsExceeded(targetIndex, skill.MaxTarget, -1, clientTick))
                return ValidateSummonTargetLimitLocked(attacker, summon, skill);

            return ValidateSummonPartySkillTargetLocked(attacker, summon, skill);
        }
    }

    private LegacySkillAttackResult ValidateSummonPartySkillTargetLocked(
        Participant attacker,
        LegacySummonedMob target,
        LegacySkillDefinition skill)
    {
        if (skill.PartyCheck == 0)
            return LegacySkillAttackResult.Accepted;
        if (attacker.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize ||
            target.MobSnapshot.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
            return LegacySkillAttackResult.CombatStateUnavailable;
        if (LegacyMobCombatState.Read(attacker.Mob).CurrentScore.Hp <= 0)
            return LegacySkillAttackResult.AttackerNotAlive;

        var targetGuildId = BinaryPrimitives.ReadUInt16LittleEndian(
            target.MobSnapshot.AsSpan(WorldHubLegacyOffsets.LegacyAccountMobGuildOffset, sizeof(ushort)));
        var attackerLeader = GetPartyLeaderConnectionIdLocked(attacker.ConnectionId);
        return LegacyPartySkillTargetGate.IsBlocked(
            skill.PartyCheck,
            attackerLeader,
            target.LeaderConnectionId,
            GetPartySkillAttackerGuildIdLocked(attacker, targetGuildId),
            targetGuildId)
            ? LegacySkillAttackResult.PartySkillBlocked
            : LegacySkillAttackResult.Accepted;
    }

    private int GetPartySkillAttackerGuildIdLocked(Participant attacker, int targetGuildId) =>
        LegacyPartySkillTargetGate.ResolveAttackerGuildId(
            attacker.GuildId,
            attacker.GuildDisabled,
            targetGuildId,
            combatWorldState.BattleRoyaleState,
            combatWorldState.BattleRoyaleItem,
            attacker.PositionX,
            attacker.PositionY);

    private LegacySkillAttackResult ValidateSummonTargetLimitLocked(
        Participant attacker,
        LegacySummonedMob target,
        LegacySkillDefinition skill)
    {
        if (attacker.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize ||
            target.MobSnapshot.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
            return LegacySkillAttackResult.CombatStateUnavailable;

        var attackerState = LegacyMobCombatState.Read(attacker.Mob);
        var targetState = LegacyMobCombatState.Read(target.MobSnapshot);
        if (skill.Id != 99 && attackerState.CurrentScore.Hp <= 0)
            return LegacySkillAttackResult.AttackerNotAlive;
        if (targetState.CurrentScore.Merchant is 1 or 4 or 43 or 100)
            return LegacySkillAttackResult.TargetMerchantBlocked;
        if (skill.Id is not 31 and not 99 && targetState.CurrentScore.Hp <= 0)
            return LegacySkillAttackResult.TargetNotAlive;

        var distance = LegacyCombatMath.GetDistance(attacker.PositionX, attacker.PositionY, target.PositionX, target.PositionY);
        if (skill.Id != 42 && distance > 23)
            return LegacySkillAttackResult.OutOfRange;

        return LegacySkillAttackResult.TargetLimitExceeded;
    }

    private LegacySkillAttackResult ValidateNpcPartySkillTargetLocked(
        Participant attacker,
        LegacyWorldNpc target,
        LegacySkillDefinition skill)
    {
        if (skill.PartyCheck == 0)
            return LegacySkillAttackResult.Accepted;
        if (attacker.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize ||
            target.MobSnapshot.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
            return LegacySkillAttackResult.CombatStateUnavailable;
        if (LegacyMobCombatState.Read(attacker.Mob).CurrentScore.Hp <= 0)
            return LegacySkillAttackResult.AttackerNotAlive;

        var targetGuildId = BinaryPrimitives.ReadUInt16LittleEndian(
            target.MobSnapshot.AsSpan(WorldHubLegacyOffsets.LegacyAccountMobGuildOffset, sizeof(ushort)));
        var attackerLeader = GetPartyLeaderConnectionIdLocked(attacker.ConnectionId);
        return LegacyPartySkillTargetGate.IsBlocked(
            skill.PartyCheck,
            attackerLeader,
            target.ConnectionId,
            GetPartySkillAttackerGuildIdLocked(attacker, targetGuildId),
            targetGuildId)
            ? LegacySkillAttackResult.PartySkillBlocked
            : LegacySkillAttackResult.Accepted;
    }

    private LegacySkillAttackResult ValidatePlayerSkillTargetLocked(
        Participant attacker,
        Participant target,
        LegacySkillDefinition skill,
        int targetIndex,
        uint clientTick,
        LegacyMobCombatState attackerState,
        LegacyMobCombatState targetState)
    {
        var isHealing = skill.InstanceType == 6;
        var isDetox = skill.InstanceType == 8;
        var isSummon = skill.InstanceType == 9;
        var isNpcSummon = skill.InstanceType == 11;
        var isInvisibility = skill.InstanceType == 10;
        var isRenascimento = skill.Id == 31;
        var isResurrection = skill.Id == 99;
        var isEffectOnly = skill.InstanceType == 0 && (skill.AffectType > 0 || skill.TickType > 0);
        var isBeneficial = isHealing || isDetox || isSummon || isNpcSummon || isInvisibility || isRenascimento || isResurrection ||
            (isEffectOnly && skill.Aggressive == 0);

        if (!isResurrection && attackerState.CurrentScore.Hp <= 0)
            return LegacySkillAttackResult.AttackerNotAlive;

        var targetLimitResult = ValidatePlayerTargetLimitLocked(
            attacker,
            target,
            skill,
            targetIndex,
            clientTick,
            targetState.CurrentScore.Hp,
            targetState.CurrentScore.Merchant);
        if (targetLimitResult != LegacySkillAttackResult.Accepted)
            return targetLimitResult;
        if (!isBeneficial && attacker.ConnectionId == target.ConnectionId)
            return LegacySkillAttackResult.SameParticipant;
        var attackerLeader = GetPartyLeaderConnectionIdLocked(attacker.ConnectionId);
        var targetLeader = GetPartyLeaderConnectionIdLocked(target.ConnectionId);
        return LegacyPartySkillTargetGate.IsBlocked(
            skill.PartyCheck,
            attackerLeader,
            targetLeader,
            GetPartySkillAttackerGuildIdLocked(attacker, target.GuildDisabled ? 0 : target.GuildId),
            target.GuildDisabled ? 0 : target.GuildId)
            ? LegacySkillAttackResult.PartySkillBlocked
            : LegacySkillAttackResult.Accepted;
    }

    private LegacySkillAttackResult ValidatePlayerTargetLimitLocked(
        Participant attacker,
        Participant target,
        LegacySkillDefinition skill,
        int targetIndex,
        uint clientTick,
        int targetHp,
        int targetMerchant)
    {
        if (!LegacyAttackTargetLimitGate.IsExceeded(targetIndex, skill.MaxTarget, -1, clientTick))
            return LegacySkillAttackResult.Accepted;
        if (targetMerchant is 1 or 4 or 43 or 100)
            return LegacySkillAttackResult.TargetMerchantBlocked;
        if (targetHp <= 0 && skill.Id is not 31 and not 99)
            return LegacySkillAttackResult.TargetNotAlive;
        var distance = LegacyCombatMath.GetDistance(attacker.PositionX, attacker.PositionY, target.PositionX, target.PositionY);
        if (skill.Id != 42 && distance > 23)
            return LegacySkillAttackResult.OutOfRange;
        return LegacySkillAttackResult.TargetLimitExceeded;
    }

    private LegacySkillAttackResult ValidateNpcTargetLimitLocked(
        Participant attacker,
        LegacyWorldNpc npc,
        LegacySkillDefinition skill)
    {
        var targetMob = npc.MobSnapshot;
        if (attacker.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize || targetMob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
            return LegacySkillAttackResult.CombatStateUnavailable;

        var attackerState = LegacyMobCombatState.Read(attacker.Mob);
        var targetState = LegacyMobCombatState.Read(targetMob);
        if (skill.Id != 99 && attackerState.CurrentScore.Hp <= 0)
            return LegacySkillAttackResult.AttackerNotAlive;
        if (targetState.CurrentScore.Merchant is 1 or 4 or 43 or 100)
            return LegacySkillAttackResult.TargetMerchantBlocked;
        if (skill.Id is not 31 and not 99 && targetState.CurrentScore.Hp <= 0)
            return LegacySkillAttackResult.TargetNotAlive;

        var distance = LegacyCombatMath.GetDistance(attacker.PositionX, attacker.PositionY, npc.PositionX, npc.PositionY);
        if (skill.Id != 42 && distance > 23)
            return LegacySkillAttackResult.OutOfRange;

        return LegacySkillAttackResult.TargetLimitExceeded;
    }

    private LegacySkillAttackResult TryApplyNpcHealingLocked(
        Participant attacker,
        LegacyWorldNpc npc,
        int targetConnectionId,
        LegacySkillDefinition skill,
        out LegacySkillAttackOutcome? outcome)
    {
        outcome = null;
        var targetMob = npc.MobSnapshot.ToArray();
        if (attacker.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize || targetMob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
            return LegacySkillAttackResult.CombatStateUnavailable;

        var attackerState = LegacyMobCombatState.Read(attacker.Mob);
        var targetState = LegacyMobCombatState.Read(targetMob);
        if (attackerState.CurrentScore.Hp <= 0)
            return LegacySkillAttackResult.AttackerNotAlive;
        if (targetState.CurrentScore.Hp <= 0)
            return LegacySkillAttackResult.TargetNotAlive;

        var distance = LegacyCombatMath.GetDistance(attacker.PositionX, attacker.PositionY, npc.PositionX, npc.PositionY);
        if (distance > 23)
            return LegacySkillAttackResult.OutOfRange;
        if (targetMob[LegacyAccountSnapshot.MobClanOffset] == 4)
            return LegacySkillAttackResult.TargetClanBlocked;

        var attackerAttribute = mapGrid?.GetAttribute(attacker.PositionX, attacker.PositionY) ?? attacker.MapAttribute;
        var targetAttribute = mapGrid?.GetAttribute(npc.PositionX, npc.PositionY) ?? 0;
        if ((attackerAttribute & 0x40) == 0 && (targetAttribute & 0x40) != 0)
        {
            outcome = new LegacySkillAttackOutcome(targetConnectionId, 0, 0, 0, targetState.CurrentScore.Hp, targetMob);
            return LegacySkillAttackResult.Accepted;
        }

        var healing = LegacySkillCombatMath.GetHealAmount(skill, attackerState, attacker.ClassMaster);

        var targetMount = LegacyItem.Read(targetMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (13 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        var divisor = targetMount.Index switch
        {
            786 => Math.Max(2, (int)targetMount.Value1),
            1936 => Math.Max(2, (int)targetMount.Value1) * 100,
            1937 => Math.Max(2, (int)targetMount.Value1) * 20_000,
            _ => 1,
        };
        var appliedHealing = healing / divisor;
        var remainingHp = Math.Min(targetState.CurrentScore.MaxHp, targetState.CurrentScore.Hp + appliedHealing);
        BinaryPrimitives.WriteInt32LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), remainingHp);
        npcs[targetConnectionId] = npc with { MobSnapshot = targetMob };
        var healingExperience = LegacyAttackHealExperienceMath.GetContribution(
            attacker.ConnectionId,
            targetConnectionId,
            npc.PositionX,
            npc.PositionY,
            targetState.CurrentScore.Hp,
            remainingHp);
        outcome = new LegacySkillAttackOutcome(
            targetConnectionId,
            healing,
            -appliedHealing,
            0,
            remainingHp,
            targetMob,
            TargetHpChanged: appliedHealing > 0,
            HealingExperienceContribution: healingExperience);
        return LegacySkillAttackResult.Accepted;
    }

    private LegacySkillAttackResult TryApplyNpcDetoxLocked(
        Participant attacker,
        LegacyWorldNpc npc,
        int targetConnectionId,
        out LegacySkillAttackOutcome? outcome)
    {
        outcome = null;
        var targetMob = npc.MobSnapshot.ToArray();
        var targetAffect = npc.AffectSnapshot.ToArray();
        if (attacker.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize || targetMob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
            return LegacySkillAttackResult.CombatStateUnavailable;

        var attackerState = LegacyMobCombatState.Read(attacker.Mob);
        var targetState = LegacyMobCombatState.Read(targetMob);
        if (attackerState.CurrentScore.Hp <= 0)
            return LegacySkillAttackResult.AttackerNotAlive;
        if (targetState.CurrentScore.Hp <= 0)
            return LegacySkillAttackResult.TargetNotAlive;

        var distance = LegacyCombatMath.GetDistance(attacker.PositionX, attacker.PositionY, npc.PositionX, npc.PositionY);
        if (distance > 23)
            return LegacySkillAttackResult.OutOfRange;

        var changed = false;
        for (var slot = 0; slot < targetAffect.Length / 8; slot++)
        {
            var offset = slot * 8;
            var type = targetAffect[offset];
            if (type is 1 or 3 or 5 or 7 or 10 or 12 or 20 || (type == 32 && (attackerState.LearnedSkill & (1u << 7)) != 0))
            {
                targetAffect.AsSpan(offset, 8).Clear();
                changed = true;
            }
        }

        npcs[targetConnectionId] = npc with { AffectSnapshot = targetAffect };
        outcome = new LegacySkillAttackOutcome(targetConnectionId, 0, 0, 0, targetState.CurrentScore.Hp, targetMob, changed ? targetAffect : null);
        return LegacySkillAttackResult.Accepted;
    }

    private LegacySkillAttackResult TryApplyNpcEffectSkillAttackLocked(
        Participant attacker,
        LegacyWorldNpc npc,
        int targetConnectionId,
        LegacySkillDefinition skill,
        int affectRandomFactor,
        out LegacySkillAttackOutcome? outcome)
    {
        outcome = null;
        var targetMob = npc.MobSnapshot.ToArray();
        var targetAffect = npc.AffectSnapshot.ToArray();
        var pursuitBaseResistances = npc.PursuitBaseResistances;
        var holyTouchBaseScore = npc.HolyTouchBaseScore;
        if (attacker.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize || targetMob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
            return LegacySkillAttackResult.CombatStateUnavailable;

        var attackerState = LegacyMobCombatState.Read(attacker.Mob);
        var targetState = LegacyMobCombatState.Read(targetMob);
        if (attackerState.CurrentScore.Hp <= 0)
            return LegacySkillAttackResult.AttackerNotAlive;
        if (targetState.CurrentScore.Hp <= 0)
            return LegacySkillAttackResult.TargetNotAlive;

        var distance = LegacyCombatMath.GetDistance(attacker.PositionX, attacker.PositionY, npc.PositionX, npc.PositionY);
        if (distance > 23)
            return LegacySkillAttackResult.OutOfRange;

        if (skill.Id == 47 && TryCancelFirstLegacyAffect(targetAffect, targetMob, affectType: 19))
        {
            npcs[targetConnectionId] = npc with
            {
                MobSnapshot = targetMob,
                AffectSnapshot = targetAffect,
            };
            outcome = new LegacySkillAttackOutcome(
                targetConnectionId,
                0,
                0,
                0,
                targetState.CurrentScore.Hp,
                targetMob.ToArray(),
                targetAffect.ToArray(),
                StopRemainingTargets: true);
            return LegacySkillAttackResult.Accepted;
        }

        if (skill.Aggressive != 0 && (targetState.Rsv & 0x80) != 0)
        {
            outcome = new LegacySkillAttackOutcome(targetConnectionId, 0, 0, 0, targetState.CurrentScore.Hp, targetMob);
            return LegacySkillAttackResult.Accepted;
        }

        var effectChanged = false;
        var effectLevel = LegacySkillCombatMath.GetSkillSpecial(skill, attackerState);
        var effectDelay = LegacySkillCombatMath.GetSkillAffectDelay(skill, attackerState, attacker.ClassMaster);
        if (skill.Id == 102 && targetState.CurrentScore.Merchant != 1)
        {
            var canApply = true;
            if (skill.AffectResist is >= 1 and <= 4)
            {
                var levelDifference = (attackerState.CurrentScore.Level - targetState.CurrentScore.Level) / 2;
                var resistanceLimit = targetState.RegenMp + skill.AffectResist + levelDifference;
                var random = affectRandomFactor >= 0
                    ? affectRandomFactor
                    : System.Security.Cryptography.RandomNumberGenerator.GetInt32(100);
                canApply = random <= resistanceLimit;
            }
            if (canApply)
                effectChanged = TryApplyLegacyAffects(targetAffect, skill, effectDelay, effectLevel);
        }
        else if (IsAuditedResiDecreaseSkill(skill) &&
                 (targetState.CurrentScore.Merchant != 1 || targetConnectionId <= LegacyMobAiPlanner.MaximumPlayerId))
        {
            // TMProject 7.69 and W2PP route SkillData 226 through the shared
            // aggressive-affect gates, then SetTick. Type 3 changes NPC score
            // resistances immediately and restores them when the timer expires.
            var attackerLeader = GetPartyLeaderConnectionIdLocked(attacker.ConnectionId);
            var targetLeader = npc.LeaderId > 0 ? npc.LeaderId : targetConnectionId;
            var attackerGuild = attacker.GuildDisabled ? 0 : attacker.GuildId != 0
                ? attacker.GuildId
                : BinaryPrimitives.ReadUInt16LittleEndian(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset));
            var targetGuild = BinaryPrimitives.ReadUInt16LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobGuildOffset));
            if (attackerGuild == 0 && targetGuild == 0)
                attackerGuild = -1;

            var targetIsClanSix = attacker.ConnectionId < LegacyMobAiPlanner.MaximumPlayerId &&
                targetMob[LegacyAccountSnapshot.MobClanOffset] == 6;
            if (attackerLeader != targetLeader && attackerGuild != targetGuild && !targetIsClanSix)
            {
                var targetClassMaster = 0;
                var targetLevel = targetClassMaster is LegacyAccountSnapshot.ClassMasterMortal or LegacyAccountSnapshot.ClassMasterArch
                    ? targetState.CurrentScore.Level
                    : targetState.CurrentScore.Level + LegacySkillCombatMath.LegacyMaxLevel;
                var attackerLevel = attacker.ClassMaster == LegacyAccountSnapshot.ClassMasterMortal ||
                                    targetClassMaster == LegacyAccountSnapshot.ClassMasterArch
                    ? attackerState.CurrentScore.Level
                    : attackerState.CurrentScore.Level + LegacySkillCombatMath.LegacyMaxLevel;
                var levelDifference = -((targetLevel - attackerLevel) / 2);
                var resistanceLimit = targetState.RegenMp + skill.AffectResist + levelDifference;
                var affectRoll = affectRandomFactor >= 0
                    ? affectRandomFactor
                    : System.Security.Cryptography.RandomNumberGenerator.GetInt32(100);
                if (affectRoll <= resistanceLimit)
                {
                    var previousAffect = targetAffect.ToArray();
                    effectChanged = TrySetLegacyAffect(
                        targetAffect, skill.TickType, skill.TickValue, skill.AffectTime, effectDelay, effectLevel, tick: true);
                    if (effectChanged)
                        pursuitBaseResistances = ApplyNpcPursuitAffectResistanceTransition(
                            targetMob, previousAffect, targetAffect, pursuitBaseResistances);
                }
            }
        }
        else if (skill.TickType > 0 && targetState.CurrentScore.Merchant != 1)
        {
            if (skill.AffectResist is >= 1 and <= 4)
            {
                var levelDifference = (attackerState.CurrentScore.Level - targetState.CurrentScore.Level) / 2;
                var resistanceLimit = targetState.RegenMp + skill.AffectResist + levelDifference;
                var random = affectRandomFactor >= 0
                    ? affectRandomFactor
                    : System.Security.Cryptography.RandomNumberGenerator.GetInt32(100);
                if (random <= resistanceLimit)
                    effectChanged = TrySetLegacyAffect(targetAffect, skill.TickType, skill.TickValue, skill.AffectTime, effectDelay, effectLevel, tick: true);
            }
            else
            {
                effectChanged = TrySetLegacyAffect(targetAffect, skill.TickType, skill.TickValue, skill.AffectTime, effectDelay, effectLevel, tick: true);
            }
        }
        else if (skill.Id == 47 &&
                 targetConnectionId == LegacyMobAiPlanner.MaximumPlayerId &&
                 targetMob[LegacyAccountSnapshot.MobMerchantOffset] != 1)
        {
            // TMProject 7.69 and W2PP fall through from Cancelamento's Type 19
            // removal into the generic SetAffect path. SetAffect accepts
            // conn == MAX_USER (the first NPC index) and rejects conn > MAX_USER.
            var attackerLeader = GetPartyLeaderConnectionIdLocked(attacker.ConnectionId);
            var targetLeader = npc.LeaderId > 0 ? npc.LeaderId : targetConnectionId;
            var attackerGuild = attacker.GuildDisabled ? 0 : attacker.GuildId != 0
                ? attacker.GuildId
                : BinaryPrimitives.ReadUInt16LittleEndian(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset));
            var targetGuild = BinaryPrimitives.ReadUInt16LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobGuildOffset));
            if (attackerGuild == 0 && targetGuild == 0)
                attackerGuild = -1;

            var targetIsClanSix = attacker.ConnectionId < LegacyMobAiPlanner.MaximumPlayerId &&
                targetMob[LegacyAccountSnapshot.MobClanOffset] == 6;
            if (attackerLeader != targetLeader && attackerGuild != targetGuild && !targetIsClanSix)
                effectChanged = TryApplyLegacyAffects(targetAffect, skill, effectDelay, effectLevel);
        }
        else if (skill.Id == 51 &&
                 targetConnectionId == LegacyMobAiPlanner.MaximumPlayerId &&
                 targetMob[LegacyAccountSnapshot.MobMerchantOffset] != 1)
        {
            // TMProject 7.69/W2PP route Enfraquecer through generic SetAffect.
            // SetAffect accepts only conn == MAX_USER for NPCs; the aggressive
            // SameLeaderGuild, RSV_BLOCK, and Clan 6 gates run before that call.
            var attackerLeader = GetPartyLeaderConnectionIdLocked(attacker.ConnectionId);
            var targetLeader = npc.LeaderId > 0 ? npc.LeaderId : targetConnectionId;
            var attackerGuild = attacker.GuildDisabled ? 0 : attacker.GuildId != 0
                ? attacker.GuildId
                : BinaryPrimitives.ReadUInt16LittleEndian(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset));
            var targetGuild = BinaryPrimitives.ReadUInt16LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobGuildOffset));
            if (attackerGuild == 0 && targetGuild == 0)
                attackerGuild = -1;

            var targetIsClanSix = attacker.ConnectionId < LegacyMobAiPlanner.MaximumPlayerId &&
                targetMob[LegacyAccountSnapshot.MobClanOffset] == 6;
            if (attackerLeader != targetLeader && attackerGuild != targetGuild && !targetIsClanSix)
            {
                var previousAffect = targetAffect.ToArray();
                effectChanged = TryApplyLegacyAffects(targetAffect, skill, effectDelay, effectLevel);
                if (effectChanged)
                {
                    if (HasLegacyAffectType(previousAffect, type: 10))
                    {
                        for (var offset = 0; offset < targetAffect.Length; offset += 8)
                        {
                            if (targetAffect[offset] != 10)
                                continue;

                            // SetAffect uses the existing slot's Type for this
                            // legacy special case: refreshing Types 1/3/10 sets
                            // their duration to four server timer ticks.
                            BinaryPrimitives.WriteUInt32LittleEndian(targetAffect.AsSpan(offset + 4), 4);
                            break;
                        }
                    }
                    ApplyNpcWeakenAffectDamageDelta(targetMob, previousAffect, targetAffect);
                }
            }
        }
        else if (skill.Id == 16 &&
                 targetConnectionId == LegacyMobAiPlanner.MaximumPlayerId &&
                 targetMob[LegacyAccountSnapshot.MobMerchantOffset] != 1)
        {
            // TMProject 7.69/W2PP route Perseguição through generic SetAffect.
            // Their CMob constructors clear Extra, so the first NPC's ClassMaster
            // is zero for the legacy level-resistance calculation. SetAffect accepts
            // this exact first NPC index and rejects later mob indexes.
            var attackerLeader = GetPartyLeaderConnectionIdLocked(attacker.ConnectionId);
            var targetLeader = npc.LeaderId > 0 ? npc.LeaderId : targetConnectionId;
            var attackerGuild = attacker.GuildDisabled ? 0 : attacker.GuildId != 0
                ? attacker.GuildId
                : BinaryPrimitives.ReadUInt16LittleEndian(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset));
            var targetGuild = BinaryPrimitives.ReadUInt16LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobGuildOffset));
            if (attackerGuild == 0 && targetGuild == 0)
                attackerGuild = -1;

            var targetIsClanSix = attacker.ConnectionId < LegacyMobAiPlanner.MaximumPlayerId &&
                targetMob[LegacyAccountSnapshot.MobClanOffset] == 6;
            if (attackerLeader != targetLeader && attackerGuild != targetGuild && !targetIsClanSix)
            {
                var targetClassMaster = 0;
                var targetLevel = targetClassMaster is LegacyAccountSnapshot.ClassMasterMortal or LegacyAccountSnapshot.ClassMasterArch
                    ? targetState.CurrentScore.Level
                    : targetState.CurrentScore.Level + LegacySkillCombatMath.LegacyMaxLevel;
                var attackerLevel = attacker.ClassMaster == LegacyAccountSnapshot.ClassMasterMortal ||
                                    targetClassMaster == LegacyAccountSnapshot.ClassMasterArch
                    ? attackerState.CurrentScore.Level
                    : attackerState.CurrentScore.Level + LegacySkillCombatMath.LegacyMaxLevel;
                var levelDifference = -((targetLevel - attackerLevel) / 2);
                var resistanceLimit = targetState.RegenMp + skill.AffectResist + levelDifference;
                var affectRoll = affectRandomFactor >= 0
                    ? affectRandomFactor
                    : System.Security.Cryptography.RandomNumberGenerator.GetInt32(100);
                if (affectRoll <= resistanceLimit)
                {
                    var previousAffect = targetAffect.ToArray();
                    effectChanged = TryApplyLegacyAffects(targetAffect, skill, effectDelay, effectLevel);
                    if (effectChanged)
                    {
                        if (HasLegacyAffectType(previousAffect, type: 3))
                        {
                            for (var offset = 0; offset < targetAffect.Length; offset += 8)
                            {
                                if (targetAffect[offset] != 3)
                                    continue;

                                // SetAffect resets an existing Type 3 record to four ticks.
                                BinaryPrimitives.WriteUInt32LittleEndian(targetAffect.AsSpan(offset + 4), 4);
                                break;
                            }
                        }
                        pursuitBaseResistances = ApplyNpcPursuitAffectResistanceTransition(
                            targetMob, previousAffect, targetAffect, pursuitBaseResistances);
                    }
                }
            }
        }
        else if (skill.Id == 1 &&
                 targetConnectionId == LegacyMobAiPlanner.MaximumPlayerId &&
                 targetMob[LegacyAccountSnapshot.MobMerchantOffset] != 1)
        {
            // TMProject 7.69/W2PP route Toque Sagrado through generic SetAffect.
            // The aggressive leader/guild, RSV_BLOCK, and Clan 6 gates run before
            // the SetAffect bound that admits only the first NPC index.
            var attackerLeader = GetPartyLeaderConnectionIdLocked(attacker.ConnectionId);
            var targetLeader = npc.LeaderId > 0 ? npc.LeaderId : targetConnectionId;
            var attackerGuild = attacker.GuildDisabled ? 0 : attacker.GuildId != 0
                ? attacker.GuildId
                : BinaryPrimitives.ReadUInt16LittleEndian(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset));
            var targetGuild = BinaryPrimitives.ReadUInt16LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobGuildOffset));
            if (attackerGuild == 0 && targetGuild == 0)
                attackerGuild = -1;

            var targetIsClanSix = attacker.ConnectionId < LegacyMobAiPlanner.MaximumPlayerId &&
                targetMob[LegacyAccountSnapshot.MobClanOffset] == 6;
            if (attackerLeader != targetLeader && attackerGuild != targetGuild && !targetIsClanSix)
            {
                var targetClassMaster = 0;
                var targetLevel = targetClassMaster is LegacyAccountSnapshot.ClassMasterMortal or LegacyAccountSnapshot.ClassMasterArch
                    ? targetState.CurrentScore.Level
                    : targetState.CurrentScore.Level + LegacySkillCombatMath.LegacyMaxLevel;
                var attackerLevel = attacker.ClassMaster == LegacyAccountSnapshot.ClassMasterMortal ||
                                    targetClassMaster == LegacyAccountSnapshot.ClassMasterArch
                    ? attackerState.CurrentScore.Level
                    : attackerState.CurrentScore.Level + LegacySkillCombatMath.LegacyMaxLevel;
                var levelDifference = -((targetLevel - attackerLevel) / 2);
                var resistanceLimit = targetState.RegenMp + skill.AffectResist + levelDifference;
                var affectRoll = affectRandomFactor >= 0
                    ? affectRandomFactor
                    : System.Security.Cryptography.RandomNumberGenerator.GetInt32(100);
                if (affectRoll <= resistanceLimit)
                {
                    var previousAffect = targetAffect.ToArray();
                    effectChanged = TryApplyLegacyAffects(targetAffect, skill, effectDelay, effectLevel);
                    if (effectChanged)
                    {
                        if (HasLegacyAffectType(previousAffect, type: 1))
                        {
                            for (var offset = 0; offset < targetAffect.Length; offset += 8)
                            {
                                if (targetAffect[offset] != 1)
                                    continue;

                                // SetAffect resets an existing Type 1 record to four ticks.
                                BinaryPrimitives.WriteUInt32LittleEndian(targetAffect.AsSpan(offset + 4), 4);
                                break;
                            }
                        }
                        holyTouchBaseScore = ApplyNpcHolyTouchAffectScoreTransition(
                            targetMob, previousAffect, targetAffect, holyTouchBaseScore);
                    }
                }
            }
        }

        npcs[targetConnectionId] = npc with
        {
            MobSnapshot = targetMob,
            AffectSnapshot = targetAffect,
            PursuitBaseResistances = pursuitBaseResistances,
            HolyTouchBaseScore = holyTouchBaseScore,
        };
        outcome = new LegacySkillAttackOutcome(targetConnectionId, 0, 0, 0, targetState.CurrentScore.Hp, targetMob, effectChanged ? targetAffect : null);
        return LegacySkillAttackResult.Accepted;
    }

    private bool TryApplyNpcPoisonTickType20Locked(
        Participant attacker,
        LegacyWorldNpc npc,
        int targetConnectionId,
        LegacySkillDefinition skill,
        LegacyMobCombatState attackerState,
        LegacyMobCombatState targetState,
        byte[] targetMob,
        byte[] targetAffect,
        int affectRandomFactor)
    {
        var sameLeaderGuild = true;
        if (skill.Aggressive != 0)
        {
            var attackerLeader = GetPartyLeaderConnectionIdLocked(attacker.ConnectionId);
            var targetLeader = npc.LeaderId > 0 ? npc.LeaderId : targetConnectionId;
            var attackerGuild = attacker.GuildDisabled ? 0 : attacker.GuildId != 0
                ? attacker.GuildId
                : BinaryPrimitives.ReadUInt16LittleEndian(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset));
            var targetGuild = BinaryPrimitives.ReadUInt16LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobGuildOffset));
            if (attackerGuild == 0 && targetGuild == 0)
                attackerGuild = -1;

            sameLeaderGuild = attackerLeader != targetLeader && attackerGuild != targetGuild;
            if (sameLeaderGuild && skill.AffectResist is >= 1 and <= 4)
            {
                // Both references store ordinary NPC Extra.ClassMaster as zero.
                var targetLevel = targetState.CurrentScore.Level + LegacySkillCombatMath.LegacyMaxLevel;
                var attackerLevel = attacker.ClassMaster == LegacyAccountSnapshot.ClassMasterMortal
                    ? attackerState.CurrentScore.Level
                    : attackerState.CurrentScore.Level + LegacySkillCombatMath.LegacyMaxLevel;
                var levelDifference = -((targetLevel - attackerLevel) / 2);
                var resistanceLimit = targetState.RegenMp + skill.AffectResist + levelDifference;
                var roll = affectRandomFactor >= 0
                    ? affectRandomFactor
                    : System.Security.Cryptography.RandomNumberGenerator.GetInt32(100);
                if (roll > resistanceLimit)
                    sameLeaderGuild = false;
            }

            if ((targetState.Rsv & 0x80) != 0 ||
                (attacker.ConnectionId < LegacyMobAiPlanner.MaximumPlayerId &&
                 targetMob[LegacyAccountSnapshot.MobClanOffset] == 6))
                sameLeaderGuild = false;
        }

        // TMProject 7.69 and W2PP SetTick reject only Merchant NPCs above MAX_USER.
        if (!sameLeaderGuild || skill.TickType != 20 ||
            (targetMob[LegacyAccountSnapshot.MobMerchantOffset] == 1 &&
             targetConnectionId > LegacyMobAiPlanner.MaximumPlayerId))
            return false;

        return TrySetLegacyAffect(
            targetAffect,
            skill.TickType,
            skill.TickValue,
            skill.AffectTime,
            LegacySkillCombatMath.GetSkillAffectDelay(skill, attackerState, attacker.ClassMaster),
            LegacySkillCombatMath.GetSkillSpecial(skill, attackerState),
            tick: true);
    }

    private bool TryApplyNpcAggressiveType1AffectLocked(
        Participant attacker,
        LegacyWorldNpc npc,
        int targetConnectionId,
        LegacySkillDefinition skill,
        LegacyMobCombatState attackerState,
        LegacyMobCombatState targetState,
        byte[] targetMob,
        byte[] targetAffect,
        int affectRandomFactor,
        ref LegacyNpcHolyTouchScoreBaseline? baseScore)
    {
        var sameLeaderGuild = true;
        if (skill.Aggressive != 0)
        {
            var attackerLeader = GetPartyLeaderConnectionIdLocked(attacker.ConnectionId);
            var targetLeader = npc.LeaderId > 0 ? npc.LeaderId : targetConnectionId;
            var attackerGuild = attacker.GuildDisabled ? 0 : attacker.GuildId != 0
                ? attacker.GuildId
                : BinaryPrimitives.ReadUInt16LittleEndian(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset));
            var targetGuild = BinaryPrimitives.ReadUInt16LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobGuildOffset));
            if (attackerGuild == 0 && targetGuild == 0)
                attackerGuild = -1;

            sameLeaderGuild = attackerLeader != targetLeader && attackerGuild != targetGuild;
            if (sameLeaderGuild && skill.AffectResist is >= 1 and <= 4)
            {
                // Both C++ sources use class-master zero for an ordinary NPC.
                const int targetClassMaster = LegacyAccountSnapshot.ClassMasterMortal;
                var targetLevel = targetState.CurrentScore.Level;
                var attackerLevel = attacker.ClassMaster == LegacyAccountSnapshot.ClassMasterMortal ||
                                    targetClassMaster == LegacyAccountSnapshot.ClassMasterArch
                    ? attackerState.CurrentScore.Level
                    : attackerState.CurrentScore.Level + LegacySkillCombatMath.LegacyMaxLevel;
                var levelDifference = -((targetLevel - attackerLevel) / 2);
                var resistanceLimit = targetState.RegenMp + skill.AffectResist + levelDifference;
                var roll = affectRandomFactor >= 0
                    ? affectRandomFactor
                    : System.Security.Cryptography.RandomNumberGenerator.GetInt32(100);
                if (roll > resistanceLimit)
                    sameLeaderGuild = false;
            }

            if ((targetState.Rsv & 0x80) != 0 ||
                (attacker.ConnectionId < LegacyMobAiPlanner.MaximumPlayerId &&
                 targetMob[LegacyAccountSnapshot.MobClanOffset] == 6))
                sameLeaderGuild = false;
        }

        // SetAffect itself rejects merchants and every NPC index above MAX_USER.
        if (!sameLeaderGuild || targetConnectionId != LegacyMobAiPlanner.MaximumPlayerId ||
            targetMob[LegacyAccountSnapshot.MobMerchantOffset] == 1)
            return false;

        var previousAffect = targetAffect.ToArray();
        var effectLevel = LegacySkillCombatMath.GetSkillSpecial(skill, attackerState);
        var effectDelay = LegacySkillCombatMath.GetSkillAffectDelay(skill, attackerState, attacker.ClassMaster);
        if (!TryApplyLegacyAffects(targetAffect, skill, effectDelay, effectLevel))
            return false;

        if (HasLegacyAffectType(previousAffect, type: 1))
        {
            for (var offset = 0; offset < targetAffect.Length; offset += 8)
            {
                if (targetAffect[offset] != 1)
                    continue;

                // SetAffect resets an existing Type 1 record to four ticks.
                BinaryPrimitives.WriteUInt32LittleEndian(targetAffect.AsSpan(offset + 4), 4);
                break;
            }
        }

        baseScore = ApplyNpcHolyTouchAffectScoreTransition(
            targetMob, previousAffect, targetAffect, baseScore);
        return true;
    }

    private static LegacyNpcHolyTouchScoreBaseline? ApplyNpcHolyTouchAffectScoreTransition(
        byte[] mob,
        ReadOnlySpan<byte> previousAffect,
        ReadOnlySpan<byte> nextAffect,
        LegacyNpcHolyTouchScoreBaseline? baseScore)
    {
        const byte holyTouchAffectType = 1;
        var wasActive = HasLegacyAffectType(previousAffect, holyTouchAffectType);
        var isActive = HasLegacyAffectType(nextAffect, holyTouchAffectType);
        if (!wasActive && !isActive)
            return baseScore;

        var headItem = LegacyItem.Read(mob.AsSpan(
            LegacyAccountSnapshot.MobEquipmentOffset,
            LegacyItem.SizeInBytes));
        if (baseScore is null)
        {
            if (!wasActive)
            {
                var currentScore = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
                baseScore = new LegacyNpcHolyTouchScoreBaseline(currentScore.AttackRun, currentScore.Intelligence);
            }
            else
            {
                // NPC affect snapshots may be restored without their runtime sidecar.
                // Reconstruct the pre-affect score where the packed/clamped value allows it.
                var currentScore = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
                var previousStage = LegacyCurrentScoreMath.CalculateHolyTouchAffectStage(previousAffect, headItem.Index);
                var attack = Math.Clamp((currentScore.AttackRun >> 4) - previousStage.AttackDelta / 10, 0, 15);
                var run = Math.Clamp((currentScore.AttackRun & 0x0F) - previousStage.RunDelta, 0, 6);
                var intelligence = unchecked((short)(currentScore.Intelligence - previousStage.IntelligenceDelta));
                baseScore = new LegacyNpcHolyTouchScoreBaseline(checked((byte)(attack * 16 + run)), intelligence);
            }
        }

        var baseline = baseScore.Value;
        if (!isActive)
        {
            var restored = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
            {
                AttackRun = baseline.AttackRun,
                Intelligence = baseline.Intelligence,
            };
            restored.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
            return null;
        }

        var stage = LegacyCurrentScoreMath.CalculateHolyTouchAffectStage(nextAffect, headItem.Index);
        var attackNibble = Math.Clamp((baseline.AttackRun >> 4) + stage.AttackDelta / 10, 0, 15);
        var runNibble = Math.Clamp((baseline.AttackRun & 0x0F) + stage.RunDelta, 0, 6);
        var projected = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            AttackRun = checked((byte)(attackNibble * 16 + runNibble)),
            Intelligence = unchecked((short)(baseline.Intelligence + stage.IntelligenceDelta)),
        };
        projected.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return baseline;
    }

    private static void ApplyNpcAffectScoreProjection(
        byte[] mob,
        ReadOnlySpan<byte> affect,
        LegacyItemDataTable itemData)
    {
        var projected = LegacyNpcAffectScoreProjector.Project(mob, affect, itemData);
        var score = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            AttackRun = projected.AttackRun,
            Intelligence = projected.Intelligence,
        };
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    }

    private static void RestoreNpcFrozenBladeBaseline(
        byte[] mob,
        LegacyNpcFrozenBladeScoreBaseline? baseline)
    {
        if (baseline is not { } value)
            return;

        var score = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            AttackRun = value.AttackRun,
            Intelligence = value.Intelligence,
        };
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    }

    private bool TryApplyNpcFrozenBladeType7AffectLocked(
        Participant attacker,
        LegacyWorldNpc npc,
        int targetConnectionId,
        LegacySkillDefinition skill,
        LegacyMobCombatState attackerState,
        LegacyMobCombatState targetState,
        byte[] targetMob,
        byte[] targetAffect,
        LegacyItemDataTable itemData,
        int affectRandomFactor,
        ref LegacyNpcFrozenBladeScoreBaseline? baseScore)
    {
        var sameLeaderGuild = true;
        if (skill.Aggressive != 0)
        {
            var attackerLeader = GetPartyLeaderConnectionIdLocked(attacker.ConnectionId);
            var targetLeader = npc.LeaderId > 0 ? npc.LeaderId : targetConnectionId;
            var attackerGuild = attacker.GuildDisabled ? 0 : attacker.GuildId != 0
                ? attacker.GuildId
                : BinaryPrimitives.ReadUInt16LittleEndian(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset));
            var targetGuild = BinaryPrimitives.ReadUInt16LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobGuildOffset));
            if (attackerGuild == 0 && targetGuild == 0)
                attackerGuild = -1;

            sameLeaderGuild = attackerLeader != targetLeader && attackerGuild != targetGuild;
            if (sameLeaderGuild && skill.AffectResist is >= 1 and <= 4)
            {
                // NPC constructors clear MOBEXTRA, so its source ClassMaster is zero.
                var targetLevel = targetState.CurrentScore.Level + LegacySkillCombatMath.LegacyMaxLevel;
                var attackerLevel = attacker.ClassMaster == LegacyAccountSnapshot.ClassMasterMortal
                    ? attackerState.CurrentScore.Level
                    : attackerState.CurrentScore.Level + LegacySkillCombatMath.LegacyMaxLevel;
                var levelDifference = -((targetLevel - attackerLevel) / 2);
                var resistanceLimit = targetState.RegenMp + skill.AffectResist + levelDifference;
                var roll = affectRandomFactor >= 0
                    ? affectRandomFactor
                    : System.Security.Cryptography.RandomNumberGenerator.GetInt32(100);
                if (roll > resistanceLimit)
                    sameLeaderGuild = false;
            }

            if ((targetState.Rsv & 0x80) != 0 ||
                (attacker.ConnectionId < LegacyMobAiPlanner.MaximumPlayerId &&
                 targetMob[LegacyAccountSnapshot.MobClanOffset] == 6))
                sameLeaderGuild = false;
        }

        // TMProject/W2PP SetAffect admits conn == MAX_USER and rejects later NPC indices.
        if (!sameLeaderGuild || skill.Id != 19 || skill.AffectType != 7 ||
            targetConnectionId != LegacyMobAiPlanner.MaximumPlayerId ||
            targetMob[LegacyAccountSnapshot.MobMerchantOffset] == 1)
            return false;

        var previousAffect = targetAffect.ToArray();
        var scoreBefore = LegacyScore.Read(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        if (!TryApplyLegacyAffects(
                targetAffect,
                skill,
                LegacySkillCombatMath.GetSkillAffectDelay(skill, attackerState, attacker.ClassMaster),
                LegacySkillCombatMath.GetSkillSpecial(skill, attackerState)))
            return false;

        if (baseScore is null && !HasLegacyAffectType(previousAffect, type: 7))
            baseScore = new LegacyNpcFrozenBladeScoreBaseline(scoreBefore.AttackRun, scoreBefore.Intelligence);
        ApplyNpcAffectScoreProjection(targetMob, targetAffect, itemData);
        return true;
    }

    private LegacySkillAttackResult TryApplyNpcFlashLocked(
        LegacyWorldNpc npc,
        int targetConnectionId,
        out LegacySkillAttackOutcome? outcome)
    {
        npcs[targetConnectionId] = npc with
        {
            Mode = LegacyNpcMode.Peace,
            CurrentTarget = LegacyNpcMode.EmptyTarget,
            EnemyList = [],
        };
        var targetState = LegacyMobCombatState.Read(npc.MobSnapshot);
        outcome = new LegacySkillAttackOutcome(targetConnectionId, 0, 0, 0, targetState.CurrentScore.Hp, npc.MobSnapshot.ToArray());
        return LegacySkillAttackResult.Accepted;
    }

    // TMProject 7.69 _MSG_Attack.cpp:1126-1139: target tiles excluded from the InstanceType 12 capture.
    private static readonly (int MinX, int MinY, int MaxX, int MaxY)[] EtherealFlameCaptureExcludedAreas =
    [
        (3725, 2833, 3833, 2908),
        (3837, 2873, 3942, 2894),
        (1676, 1556, 1776, 1636),
        (1676, 1816, 1776, 1892),
        (2445, 1850, 2546, 1920),
    ];

    // TMProject 7.69 _MSG_Attack.cpp:1141-1214: exact strcmp names excluded from the capture.
    private static readonly HashSet<string> EtherealFlameCaptureExcludedNames = new(StringComparer.Ordinal)
    {
        "Cav. Lugefer", "FrenzyDemonLord", "Lich Crunt", "Dragao Lich", "Hezling", "Golem Anciao",
        "LichCrunt", "Lich Batama", "Espelho RainhaG", "VeridRainhaG", "Verid", "Sombra Negra",
        "LichVatama", "Torre Guardia", "Torre Guardia ", "Lich Infernal", "Lugefer", "Torre",
        "Shama Amald", "Ranger Amald", "Mago Amald", "Templario Amald", "Ent Anciao", "Guerreiro Amon",
        "Simio Anciao",
    };

    private const int LegacyMaxParty = 12;

    /// <summary>TMProject 7.69 Language.txt:33 <c>_NN_Party_Full_Cant_Summon</c>.</summary>
    public const string LegacyPartyFullCantSummonMessage = "Grupo está cheio. Não é possível convocar mais membros.";
    private const int LegacyPartyAddHpScaleThreshold = 32000;

    /// <summary>
    /// Capture chance threshold for InstanceType 12 against a mob. The 7.69 reconstruction
    /// (<c>_MSG_Attack.cpp:1219-1224</c>) computes <c>(d &lt;&lt; 2) - 100</c>, which makes mobs 40+ levels
    /// above the caster a guaranteed capture. W2PP and the 7.59 server have no capture branch, so
    /// there is no independent evidence of the original formula; by explicit project decision
    /// (14/10/2026) the port uses <c>100 - 4d</c> so the chance falls as the mob gets stronger.
    /// The capture fails when <c>rand() % 100 &gt; threshold</c>, as in the source.
    /// </summary>
    public static int GetEtherealFlameCaptureThreshold(int targetLevel, int casterLevel)
    {
        var levelGap = (targetLevel + 10) - casterLevel;
        return levelGap > 0 ? 100 - (levelGap << 2) : 100;
    }

    /// <summary>
    /// TMProject 7.69 InstanceType 12 against a server mob (<c>_MSG_Attack.cpp:1115-1316</c>): a
    /// non-merchant mob outside the excluded areas/names, at full HP, not Clan 6 and passing the
    /// level roll joins the caster's party as a Clan 4 summon. W2PP and 7.59 have no NPC branch.
    /// Gate failures <c>continue</c> before the slot damage is written, so the client marker stays.
    /// </summary>
    private LegacySkillAttackResult TryApplyNpcEtherealFlameLocked(
        int attackerConnectionId,
        Participant attacker,
        LegacyWorldNpc npc,
        int targetConnectionId,
        int instanceRandomFactor,
        out LegacySkillAttackOutcome? outcome)
    {
        outcome = null;
        var targetMob = npc.MobSnapshot.ToArray();
        if (attacker.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize || targetMob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
            return LegacySkillAttackResult.CombatStateUnavailable;

        var attackerState = LegacyMobCombatState.Read(attacker.Mob);
        var targetState = LegacyMobCombatState.Read(targetMob);
        if (attackerState.CurrentScore.Hp <= 0)
            return LegacySkillAttackResult.AttackerNotAlive;
        if (targetState.CurrentScore.Hp <= 0)
            return LegacySkillAttackResult.TargetNotAlive;

        var distance = LegacyCombatMath.GetDistance(attacker.PositionX, attacker.PositionY, npc.PositionX, npc.PositionY);
        if (distance > 23)
            return LegacySkillAttackResult.OutOfRange;

        var skipped = new LegacySkillAttackOutcome(targetConnectionId, 0, 0, 0, targetState.CurrentScore.Hp, targetMob, PreserveSkillMarkerOnWire: true);

        // Offline autotrade owners are users in 7.69 (player branch); their NPC record has no
        // character session to burn or cleanse, so the slot is left without effect.
        if (npc.AutoTradeSnapshot is not null)
        {
            outcome = skipped;
            return LegacySkillAttackResult.Accepted;
        }

        // Merchant mobs fall through both branches and the slot is written with dam = 0.
        if (targetMob[LegacyAccountSnapshot.MobMerchantOffset] != 0)
        {
            outcome = skipped with { PreserveSkillMarkerOnWire = false };
            return LegacySkillAttackResult.Accepted;
        }

        foreach (var area in EtherealFlameCaptureExcludedAreas)
        {
            if (npc.PositionX >= area.MinX && npc.PositionY >= area.MinY && npc.PositionX <= area.MaxX && npc.PositionY <= area.MaxY)
            {
                outcome = skipped;
                return LegacySkillAttackResult.Accepted;
            }
        }

        if (EtherealFlameCaptureExcludedNames.Contains(ReadMobName(targetMob)))
        {
            outcome = skipped;
            return LegacySkillAttackResult.Accepted;
        }

        var threshold = GetEtherealFlameCaptureThreshold(targetState.CurrentScore.Level, attackerState.CurrentScore.Level);
        var roll = instanceRandomFactor >= 0
            ? instanceRandomFactor
            : System.Security.Cryptography.RandomNumberGenerator.GetInt32(100);
        if (roll > threshold ||
            targetState.CurrentScore.Hp < targetState.CurrentScore.MaxHp ||
            targetMob[LegacyAccountSnapshot.MobClanOffset] == 6)
        {
            outcome = skipped;
            return LegacySkillAttackResult.Accepted;
        }

        var leaderConnectionId = GetPartyLeaderConnectionIdLocked(attackerConnectionId);
        var partyPlayers = GetPartyMemberIdsLocked(leaderConnectionId).Where(id => id != leaderConnectionId).ToArray();
        var partySummonCount = summons.Values.Count(summon => summon.LeaderConnectionId == leaderConnectionId);
        var occupied = partyPlayers.Length + partySummonCount;
        if (occupied >= LegacyMaxParty)
            return LegacySkillAttackResult.PartyFullCantSummon;

        var viewRecipients = GetParticipantIdsInViewLocked(npc.PositionX, npc.PositionY);

        var cappedLevel = Math.Min(attackerState.BaseScore.Level, LegacySkillCombatMath.LegacyMaxLevel);
        var baseScore = LegacyScore.Read(targetMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset)) with { Level = cappedLevel };
        baseScore.Write(targetMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        AppendLegacyCaptureNameSuffix(targetMob);
        targetMob[LegacyAccountSnapshot.MobClanOffset] = 4;
        // CMob::GetCurrentScore(MAX_USER) is not ported for mobs; like GenerateSummon in this port,
        // the projected score keeps the template values with the capped level and full HP.
        var currentScore = LegacyScore.Read(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        currentScore = currentScore with { Level = cappedLevel, Hp = currentScore.MaxHp };
        currentScore.Write(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var capturedAffect = new byte[LegacyAccountSnapshot.AffectStride];

        npcs.Remove(targetConnectionId);
        ForgetGuildTreasuryNpcRegistrationLocked(targetConnectionId);
        ClearRemovedTargetLocked(targetConnectionId);
        var captured = new LegacySummonedMob(targetConnectionId, leaderConnectionId, attackerConnectionId, npc.PositionX, npc.PositionY, targetMob, capturedAffect);
        summons.Add(targetConnectionId, captured);
        summonTargets[targetConnectionId] = 0;
        attacker.SummonIds.Add(targetConnectionId);

        // SendAddParty only reaches users (Leaderconn < MAX_USER); calls addressed to mobs are no-ops.
        var partyAdds = new List<LegacyPartyAddNotice>();
        if (participants.TryGetValue(leaderConnectionId, out var leaderParticipant))
        {
            if (occupied == 0)
                partyAdds.Add(CreatePartyAddNotice(leaderConnectionId, leaderParticipant.Mob, leaderConnectionId, leaderConnectionId));
            partyAdds.Add(CreatePartyAddNotice(leaderConnectionId, targetMob, targetConnectionId, 30000));
        }
        foreach (var memberConnectionId in partyPlayers)
        {
            if (participants.ContainsKey(memberConnectionId))
                partyAdds.Add(CreatePartyAddNotice(memberConnectionId, targetMob, targetConnectionId, 30000));
        }

        outcome = new LegacySkillAttackOutcome(
            targetConnectionId,
            0,
            0,
            0,
            currentScore.Hp,
            targetMob.ToArray(),
            CapturedSummon: captured,
            CaptureViewRecipients: viewRecipients,
            PartyAddNotices: partyAdds);
        return LegacySkillAttackResult.Accepted;
    }

    private static LegacyPartyAddNotice CreatePartyAddNotice(int recipientConnectionId, ReadOnlySpan<byte> memberMob, int memberConnectionId, int leaderField)
    {
        var score = LegacyScore.Read(memberMob[LegacyAccountSnapshot.MobCurrentScoreOffset..]);
        var maxHp = score.MaxHp > LegacyPartyAddHpScaleThreshold ? (score.MaxHp + 1) / 100 : score.MaxHp;
        var hp = score.Hp > LegacyPartyAddHpScaleThreshold ? (score.Hp + 1) / 100 : score.Hp;
        return new LegacyPartyAddNotice(recipientConnectionId, leaderField, score.Level, maxHp, hp, memberConnectionId, ReadMobName(memberMob));
    }

    /// <summary>
    /// <c>strcat(MobName, "^")</c> followed by the <c>'_' -> ' '</c> pass over NAME_LENGTH. A 15-char
    /// name puts '^' in the last byte and its terminator on Clan, which 7.69 then overwrites with 4.
    /// </summary>
    private static void AppendLegacyCaptureNameSuffix(byte[] mob)
    {
        var name = mob.AsSpan(LegacyAccountSnapshot.MobNameOffset, 16);
        var length = name.IndexOf((byte)0);
        if (length >= 0)
        {
            name[length] = (byte)'^';
            if (length + 1 < name.Length)
                name[length + 1] = 0;
        }

        for (var index = 0; index < name.Length; index++)
        {
            if (name[index] == (byte)'_')
                name[index] = (byte)' ';
        }
    }

    private LegacySkillAttackResult TryApplyNpcSkillAttackLocked(
        Participant attacker,
        LegacyWorldNpc npc,
        int targetConnectionId,
        LegacySkillDefinition skill,
        LegacyItemDataTable itemData,
        int weather,
        int randomFactor,
        int affectRandomFactor,
        int instanceRandomFactor,
        int specialDropRoll,
        int specialItemRoll,
        int eventDropRoll,
        int runeRoll,
        int runeChanceRoll,
        int pistaRandomRoll,
        int attackActionSlotIndex,
        bool deferDeathResolution,
        int judgmentDivineStartingHp,
        int frostRoll,
        int drainRoll,
        out LegacySkillAttackOutcome? outcome)
    {
        outcome = null;
        if (skill.Id != 79 && skill.InstanceType is not (>= 1 and <= 5) and not 0)
            return LegacySkillAttackResult.UnsupportedSkillType;

        var targetMob = npc.MobSnapshot.ToArray();
        if (attacker.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize || targetMob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
            return LegacySkillAttackResult.CombatStateUnavailable;

        var attackerState = LegacyMobCombatState.Read(attacker.Mob);
        var targetState = LegacyMobCombatState.Read(targetMob);
        if (attackerState.CurrentScore.Hp <= 0)
            return LegacySkillAttackResult.AttackerNotAlive;
        if (targetState.CurrentScore.Hp <= 0)
            return LegacySkillAttackResult.TargetNotAlive;

        var distance = LegacyCombatMath.GetDistance(attacker.PositionX, attacker.PositionY, npc.PositionX, npc.PositionY);
        if (distance > 23)
            return LegacySkillAttackResult.OutOfRange;

        var firstWeapon = LegacyItem.Read(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (6 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        var secondWeapon = LegacyItem.Read(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (7 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        var weaponDamage = LegacySkillCombatMath.GetWeaponDamage(attackerState, itemData, firstWeapon, secondWeapon);
        var skillWeather = LegacySkillCombatMath.GetSkillWeather(weather, attacker.PositionX, attacker.PositionY);
        var baseDamage = LegacySkillCombatMath.GetSkillBaseDamage(skill, attackerState, skillWeather, weaponDamage, attackerState.Magic);
        var combat = LegacySkillCombatMath.GetMasterCombat(attackerState);
        var effectiveRandomFactor = randomFactor >= 0
            ? randomFactor
            : System.Security.Cryptography.RandomNumberGenerator.GetInt32(combat + 90, combat + 90 + (21 - Math.Min(combat, 15)));

        if (skill.Id == 79)
        {
            // 7.69 Tempestade de Raios discards the elemental result and uses the target's CurrentScore.Damage.
            var damage = targetState.CurrentScore.Damage;
            return FinishNpcSkillAttackLocked(attacker, npc, targetConnectionId, skill, attackerState, targetState, targetMob, baseDamage, damage, 0, itemData, specialDropRoll, specialItemRoll, eventDropRoll, runeRoll, runeChanceRoll, pistaRandomRoll, affectRandomFactor, frostRoll, drainRoll, attackActionSlotIndex, deferDeathResolution, out outcome);
        }

        var defense = LegacySkillCombatMath.GetSkillDefense(targetState.CurrentScore.Ac, targetIsPlayer: false, targetState.CharacterClass);
        var skillDamage = LegacySkillCombatMath.GetSkillDamage(baseDamage, defense, combat, effectiveRandomFactor);
        skillDamage = LegacySkillCombatMath.ApplySkillResistance(
            skill.Id,
            skill.InstanceType,
            skillDamage,
            targetIsPlayer: false,
            targetState.CurrentScore.Level,
            targetClassMaster: 0,
            targetState.LearnedSkill,
            targetMob.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4),
            out var resistance);
        LegacySkillActionRelay? actionRelay = null;
        if (skill.Id == LegacyFuryDivineMath.SkillId)
        {
            if (!TryCreateFuryDivineActionRelayLocked(
                    attacker,
                    targetConnectionId,
                    targetMob,
                    npc.PositionX,
                    npc.PositionY,
                    LegacyAccountSnapshot.ClassMasterMortal,
                    targetIsNpc: true,
                    targetGenerateIndex: npc.GenerateIndex,
                    instanceRandomFactor,
                    out actionRelay))
            {
                outcome = new LegacySkillAttackOutcome(
                    targetConnectionId,
                    baseDamage,
                    0,
                    resistance,
                    targetState.CurrentScore.Hp,
                    npc.MobSnapshot.ToArray(),
                    StopRemainingTargets: true);
                return LegacySkillAttackResult.Accepted;
            }

            // The 7.69 SetBattle call follows a successful knockback roll and
            // precedes the normal damage tail. Carry this NPC state through the
            // snapshot that FinishNpcSkillAttackLocked persists.
            if (actionRelay is not null && targetConnectionId != attacker.ConnectionId &&
                LegacyCombatMath.GetDistance(attacker.PositionX, attacker.PositionY, npc.PositionX, npc.PositionY) <= 23)
            {
                var enemyList = npc.EnemyList.ToList();
                if (!enemyList.Contains(attacker.ConnectionId))
                    enemyList.Add(attacker.ConnectionId);
                npc = npc with
                {
                    Mode = LegacyNpcMode.Combat,
                    CurrentTarget = attacker.ConnectionId,
                    EnemyList = enemyList,
                };
            }

            skillDamage = 1;
        }
        if (skill.Id == 22)
        {
            skillDamage = LegacySkillCombatMath.AddExterminarManaBonus(
                skillDamage,
                attackerState.CurrentMana,
                attackerState.CurrentScore.Intelligence);
            BinaryPrimitives.WriteInt32LittleEndian(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentMpOffset), 0);
            actionRelay = TryCreateExterminarActionRelayLocked(attacker, npc.MobSnapshot, npc.PositionX, npc.PositionY);
        }
        if (skill.Id == 30)
        {
            skillDamage += judgmentDivineStartingHp;
            ApplyJudgmentDivineHpCost(attacker, attackerState.CurrentScore.Hp);
        }

        var result = FinishNpcSkillAttackLocked(attacker, npc, targetConnectionId, skill, attackerState, targetState, targetMob, baseDamage, skillDamage, resistance, itemData, specialDropRoll, specialItemRoll, eventDropRoll, runeRoll, runeChanceRoll, pistaRandomRoll, affectRandomFactor, frostRoll, drainRoll, attackActionSlotIndex, deferDeathResolution, out outcome);
        if (skill.Id == LegacyFuryDivineMath.SkillId && result == LegacySkillAttackResult.Accepted && outcome is not null)
            outcome = outcome with { ActionRelay = actionRelay };
        else if (skill.Id is 22 or 30 && result == LegacySkillAttackResult.Accepted && outcome is not null)
            outcome = outcome with { AttackerResourceChanged = true, ActionRelay = actionRelay };
        return result;
    }

    private LegacySkillAttackResult TryApplySummonSkillAttackLocked(
        Participant attacker,
        LegacySummonedMob summon,
        int targetConnectionId,
        LegacySkillDefinition skill,
        LegacyItemDataTable itemData,
        int weather,
        int randomFactor,
        int instanceRandomFactor,
        int attackMissRoll,
        int frostRoll,
        int drainRoll,
        bool deferDeathResolution,
        int judgmentDivineStartingHp,
        int attackActionSlotIndex,
        out LegacySkillAttackOutcome? outcome)
    {
        outcome = null;
        if (skill.Id != 79 && skill.InstanceType is not (>= 1 and <= 5) and not 0)
            return LegacySkillAttackResult.UnsupportedSkillType;

        var targetMob = summon.MobSnapshot.ToArray();
        var targetAffect = summon.AffectSnapshot.ToArray();
        if (attacker.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize || targetMob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
            return LegacySkillAttackResult.CombatStateUnavailable;

        var attackerState = LegacyMobCombatState.Read(attacker.Mob);
        var targetState = LegacyMobCombatState.Read(targetMob);
        if (attackerState.CurrentScore.Hp <= 0)
            return LegacySkillAttackResult.AttackerNotAlive;
        if (targetState.CurrentScore.Hp <= 0)
            return LegacySkillAttackResult.TargetNotAlive;

        var distance = LegacyCombatMath.GetDistance(attacker.PositionX, attacker.PositionY, summon.PositionX, summon.PositionY);
        if (distance > 23)
            return LegacySkillAttackResult.OutOfRange;

        var firstWeapon = LegacyItem.Read(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (6 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        var secondWeapon = LegacyItem.Read(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (7 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        var weaponDamage = LegacySkillCombatMath.GetWeaponDamage(attackerState, itemData, firstWeapon, secondWeapon);
        var skillWeather = LegacySkillCombatMath.GetSkillWeather(weather, attacker.PositionX, attacker.PositionY);
        var baseDamage = LegacySkillCombatMath.GetSkillBaseDamage(skill, attackerState, skillWeather, weaponDamage, attackerState.Magic);
        var combat = LegacySkillCombatMath.GetMasterCombat(attackerState);
        var effectiveRandomFactor = randomFactor >= 0
            ? randomFactor
            : System.Security.Cryptography.RandomNumberGenerator.GetInt32(combat + 90, combat + 90 + (21 - Math.Min(combat, 15)));

        var damage = skill.Id == 79
            ? targetState.CurrentScore.Damage
            : LegacySkillCombatMath.GetSkillDamage(
                baseDamage,
                LegacySkillCombatMath.GetSkillDefense(targetState.CurrentScore.Ac, targetIsPlayer: false, targetState.CharacterClass),
                combat,
                effectiveRandomFactor);
        var resistance = 0;
        if (skill.Id != 79)
        {
            damage = LegacySkillCombatMath.ApplySkillResistance(
                skill.Id,
                skill.InstanceType,
                damage,
                targetIsPlayer: false,
                targetState.CurrentScore.Level,
                targetClassMaster: LegacyAccountSnapshot.ClassMasterMortal,
                targetState.LearnedSkill,
                targetMob.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4),
                out resistance);
        }

        LegacySkillActionRelay? actionRelay = null;
        if (skill.Id == LegacyFuryDivineMath.SkillId)
        {
            if (!TryCreateFuryDivineActionRelayLocked(
                    attacker,
                    targetConnectionId,
                    targetMob,
                    summon.PositionX,
                    summon.PositionY,
                    LegacyAccountSnapshot.ClassMasterMortal,
                    targetIsNpc: true,
                    targetGenerateIndex: -1,
                    instanceRandomFactor,
                    out actionRelay))
            {
                outcome = new LegacySkillAttackOutcome(
                    targetConnectionId,
                    baseDamage,
                    0,
                    resistance,
                    targetState.CurrentScore.Hp,
                    targetMob,
                    StopRemainingTargets: true);
                return LegacySkillAttackResult.Accepted;
            }

            damage = 1;
        }
        if (skill.Id == 22)
        {
            damage = LegacySkillCombatMath.AddExterminarManaBonus(
                damage,
                attackerState.CurrentMana,
                attackerState.CurrentScore.Intelligence);
            BinaryPrimitives.WriteInt32LittleEndian(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentMpOffset), 0);
            actionRelay = TryCreateExterminarActionRelayLocked(attacker, targetMob, summon.PositionX, summon.PositionY);
        }
        if (skill.Id == 30)
        {
            damage += judgmentDivineStartingHp;
            ApplyJudgmentDivineHpCost(attacker, attackerState.CurrentScore.Hp);
        }

        damage = ApplyLegacyPostDamageModifiers(
            damage,
            GetLegacyForceDamage(
                attacker.Mob, attacker.ClientEquipment, itemData, attacker.Affect, attackerState.CurrentScore.Special3),
            pvpDamage: 0);
        if (ShouldSuppressLegacyFriendlyFireSkillDamageLocked(attacker, targetConnectionId, null, summon, targetMob) ||
            ShouldSuppressLegacySameClanNpcDamage(
                targetConnectionId,
                attacker.Mob[LegacyAccountSnapshot.MobClanOffset],
                targetMob[LegacyAccountSnapshot.MobClanOffset]))
            damage = 0;

        // The 7.69 handler continues the target loop before PK and miss when
        // an earlier party/guild or MOB gate has already reduced damage to zero.
        if (damage <= 0)
        {
            summons[targetConnectionId] = summon with { MobSnapshot = targetMob, AffectSnapshot = targetAffect };
            outcome = new LegacySkillAttackOutcome(
                targetConnectionId,
                baseDamage,
                damage,
                resistance,
                targetState.CurrentScore.Hp,
                targetMob.ToArray(),
                AttackerResourceChanged: skill.Id is 22 or 30,
                ActionRelay: actionRelay);
            return LegacySkillAttackResult.Accepted;
        }

        var summonOwner = participants.TryGetValue(summon.SummonerConnectionId, out var connectedSummoner) &&
            connectedSummoner.Mob.Length >= WorldHubLegacyOffsets.LegacyAccountMobSize
                ? connectedSummoner
                : null;
        var summonPkGate = LegacySkillPkGate.ResolveSummon(
            ReadLegacyPkPoint(attacker.Mob),
            summonOwner is null ? null : ReadLegacyPkPoint(summonOwner.Mob),
            BinaryPrimitives.ReadUInt16LittleEndian(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset)),
            summonOwner is null
                ? 0
                : BinaryPrimitives.ReadUInt16LittleEndian(summonOwner.Mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset)),
            attacker.PositionX,
            attacker.PositionY,
            skill.Id,
            damage,
            combatWorldState);
        damage = summonPkGate.Damage;
        var privateNotices = summonPkGate.SendCannotKillNotice
            ? new[] { new LegacyPrivateNotice(attacker.ConnectionId, LegacySkillPkNotice.CannotKillUser(ReadLegacyPkPoint(attacker.Mob))) }
            : null;
        IReadOnlyList<LegacyCrimeStateUpdate>? crimeStateUpdates = null;
        if (summonPkGate.MarkAttackerGuilty && SetLegacyGuilty(attacker, 8))
            crimeStateUpdates =
            [
                new LegacyCrimeStateUpdate(attacker.ConnectionId, attacker.PositionX, attacker.PositionY, attacker.Mob.ToArray()),
            ];

        if (damage > 0 || summonPkGate.PkPointGateBlocked || skill.Id == LegacyFuryDivineMath.SkillId)
            damage = ResolveLegacyAttackMiss(attackerState, targetState, attackMissRoll, damage);

        if (damage > 0 && targetState.CharacterClass is 0 or 1 && HasLegacyAffectType(targetAffect, type: 6))
            damage = 0;
        if (LegacyAttackClanSixDamageGate.ShouldSuppressDamage(
                targetMob[LegacyAccountSnapshot.MobClanOffset],
                LegacySkillCombatMath.GetSkillSpecial(skill, attackerState)))
            damage = 0;

        damage = LegacyForceMobDamageMath.ApplyToMobTargetDamage(damage, attacker.Affect);
        damage = LegacyAttackDamageCap.Apply(damage);
        var clearTargetIdOnWire = LegacyAttackSameClanAreaGate.ShouldClearTargetId(
            attacker.PositionX,
            attacker.PositionY,
            attacker.Mob[LegacyAccountSnapshot.MobClanOffset],
            targetMob[LegacyAccountSnapshot.MobClanOffset],
            damage);
        if (clearTargetIdOnWire)
            damage = 0;

        var secondaryAffectChanged = damage > 0 && TryApplyLegacyRsvSecondaryAttackAffects(
            targetAffect,
            targetMob,
            attackerState,
            targetState,
            targetConnectionId,
            frostRoll,
            drainRoll);

        var zardMobLeftCount = ApplyZardQuestLocked(attacker, targetMob, out var zardRewardDrop, out var zardPrivateNotice);
        if (zardPrivateNotice is { } summonZardNotice)
            privateNotices = privateNotices is { Length: > 0 } existingSummonNotices
                ? [.. existingSummonNotices, summonZardNotice]
                : [summonZardNotice];
        IReadOnlyList<LegacyItemDrop>? zardItemDrops = zardRewardDrop is { } summonZardReward ? [summonZardReward] : null;
        if (!zardMobLeftCount.HasValue)
            ApplyPvpJewelAbsorptionLocked(attacker, damage);
        var remainingHp = damage <= 0 ? targetState.CurrentScore.Hp : Math.Max(0, targetState.CurrentScore.Hp - damage);
        BinaryPrimitives.WriteInt32LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), remainingHp);

        var mountOwnerConnectionId = 0;
        LegacyItem? updatedMountItem = null;
        IReadOnlyList<LegacyAttackViewRemovalEffect> summonPartyRemovalEffects = [];
        var targetRemoved = false;
        if (remainingHp > 0)
            TryLinkMountHpLocked(summon, targetMob, out mountOwnerConnectionId, out updatedMountItem);

        if (remainingHp == 0 && !deferDeathResolution)
        {
            TryLinkMountHpLocked(summon, targetMob, out mountOwnerConnectionId, out updatedMountItem);
            targetRemoved = RemoveSummonLocked(targetConnectionId);
        }
        if (!targetRemoved)
            summons[targetConnectionId] = summon with { MobSnapshot = targetMob, AffectSnapshot = targetAffect };
        if (!targetRemoved && remainingHp > 0)
            summonPartyRemovalEffects = ApplySummonAttackAggroLocked(
                targetConnectionId,
                attacker.ConnectionId,
                attackActionSlotIndex);

        outcome = new LegacySkillAttackOutcome(
            targetConnectionId,
            baseDamage,
            damage,
            resistance,
            remainingHp,
            targetMob.ToArray(),
            TargetAffectSnapshot: secondaryAffectChanged ? targetAffect.ToArray() : null,
            TargetRemoved: targetRemoved,
            TargetHpChanged: damage > 0,
            AttackerResourceChanged: skill.Id is 22 or 30,
            ActionRelay: actionRelay,
            ClearTargetIdOnWire: clearTargetIdOnWire,
            ItemDrops: zardItemDrops,
            PrivateNotices: privateNotices,
            CrimeStateUpdates: crimeStateUpdates,
            MountOwnerConnectionId: mountOwnerConnectionId,
            UpdatedMountItem: updatedMountItem,
            ZardMobLeftCount: zardMobLeftCount,
            ViewRemovalEffects: summonPartyRemovalEffects);
        return LegacySkillAttackResult.Accepted;
    }

    private bool TryCreateFuryDivineActionRelayLocked(
        Participant attacker,
        int targetConnectionId,
        byte[] targetMob,
        short targetPositionX,
        short targetPositionY,
        short targetClassMaster,
        bool targetIsNpc,
        int targetGenerateIndex,
        int injectedRoll,
        out LegacySkillActionRelay? actionRelay)
    {
        actionRelay = null;
        if (targetMob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
            return false;

        var targetState = LegacyMobCombatState.Read(targetMob);
        if ((targetIsNpc && targetState.CurrentScore.Merchant != 0) ||
            (targetIsNpc && targetGenerateIndex is 8 or 9) ||
            LegacyItem.Read(targetMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes)).Index is 219 or 220 or 362 ||
            targetMob[LegacyAccountSnapshot.MobClanOffset] == 6)
            return false;

        var actionX = (int)attacker.PositionX;
        var actionY = (int)attacker.PositionY;
        if (actionX < targetPositionX)
            actionX++;
        else if (actionX > targetPositionX)
            actionX--;
        if (actionY < targetPositionY)
            actionY++;
        else if (actionY > targetPositionY)
            actionY--;

        // TMProject 7.69 GetEmptyMobGrid searches radii 1..9 (MAX_SERVER=10)
        // and checks terrain on each candidate cell.
        if (!LegacyMobGridSearch.TryFindEmpty(
                targetConnectionId,
                ref actionX,
                ref actionY,
                GetParticipantAtPosition,
                mapGrid is null ? static (_, _) => false : mapGrid.IsBlocked,
                checkCandidateTerrain: true,
                maximumRadius: 9))
            return false;

        var attackerState = LegacyMobCombatState.Read(attacker.Mob);
        var chance = LegacyFuryDivineMath.GetKnockbackChancePercent(
            targetConnectionId,
            attackerState.CurrentScore.Special1,
            attackerState.CurrentScore.Level,
            attacker.ClassMaster,
            targetState.CurrentScore.Level,
            targetClassMaster);
        var roll = injectedRoll >= 0
            ? injectedRoll
            : System.Security.Cryptography.RandomNumberGenerator.GetInt32(100);
        if (!LegacyFuryDivineMath.IsKnockbackSuccessful(chance, roll))
            return true;

        actionRelay = new LegacySkillActionRelay(
            targetPositionX,
            targetPositionY,
            Effect: 2,
            Speed: 6,
            checked((short)actionX),
            checked((short)actionY),
            ActorConnectionId: targetConnectionId);
        return true;
    }

    private LegacySkillActionRelay? TryCreateExterminarActionRelayLocked(
        Participant attacker,
        byte[] targetMob,
        int targetX,
        int targetY)
    {
        var targetHead = LegacyItem.Read(targetMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        if (targetHead.Index is 219 or 220)
            return null;

        var actionX = targetX;
        var actionY = targetY;
        if (!LegacyMobGridSearch.TryFindEmpty(
                attacker.ConnectionId,
                ref actionX,
                ref actionY,
                GetParticipantAtPosition,
                mapGrid is null ? static (_, _) => false : mapGrid.IsBlocked,
                checkCandidateTerrain: true))
            return null;

        return new LegacySkillActionRelay(
            attacker.PositionX,
            attacker.PositionY,
            Effect: 2,
            Speed: 2,
            checked((short)actionX),
            checked((short)actionY),
            ActorConnectionId: attacker.ConnectionId);
    }

    private static void ApplyJudgmentDivineHpCost(Participant attacker, int currentHp)
    {
        var remainingHp = (currentHp / 6) + 1;
        BinaryPrimitives.WriteInt32LittleEndian(
            attacker.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24),
            remainingHp);
        attacker.RequestedHp = remainingHp;
    }

    private LegacySkillAttackResult FinishNpcSkillAttackLocked(
        Participant attacker,
        LegacyWorldNpc npc,
        int targetConnectionId,
        LegacySkillDefinition skill,
        LegacyMobCombatState attackerState,
        LegacyMobCombatState targetState,
        byte[] targetMob,
        int baseDamage,
        int damage,
        int resistance,
        LegacyItemDataTable itemData,
        int specialDropRoll,
        int specialItemRoll,
        int eventDropRoll,
        int runeRoll,
        int runeChanceRoll,
        int pistaRandomRoll,
        int affectRandomFactor,
        int frostRoll,
        int drainRoll,
        int attackActionSlotIndex,
        bool deferDeathResolution,
        out LegacySkillAttackOutcome? outcome)
    {
        var targetAffect = npc.AffectSnapshot.ToArray();
        var holyTouchBaseScore = npc.HolyTouchBaseScore;
        var frozenBladeBaseScore = npc.FrozenBladeBaseScore;
        // These damaging 7.69 rows are the audited Type 1 producers on this NPC path.
        var isAuditedType1DamageSkill = skill.AffectType == 1 && (skill.Id is 23 or 34 or 36);
        var affectChanged = isAuditedType1DamageSkill && TryApplyNpcAggressiveType1AffectLocked(
            attacker,
            npc,
            targetConnectionId,
            skill,
            attackerState,
            targetState,
            targetMob,
            targetAffect,
            affectRandomFactor,
            ref holyTouchBaseScore);
        if (skill.Id == 19)
            affectChanged = TryApplyNpcFrozenBladeType7AffectLocked(
                attacker,
                npc,
                targetConnectionId,
                skill,
                attackerState,
                targetState,
                targetMob,
                targetAffect,
                itemData,
                affectRandomFactor,
                ref frozenBladeBaseScore);
        if (skill.TickType == 20)
            affectChanged |= TryApplyNpcPoisonTickType20Locked(
                attacker,
                npc,
                targetConnectionId,
                skill,
                attackerState,
                targetState,
                targetMob,
                targetAffect,
                affectRandomFactor);

        // PvPDamage is limited to player targets; the legacy ForceDamage stage
        // also runs for NPC targets and for skill 79's direct-damage branch.
        damage = ApplyLegacyPostDamageModifiers(
            damage,
            GetLegacyForceDamage(
                attacker.Mob, attacker.ClientEquipment, itemData, attacker.Affect, attackerState.CurrentScore.Special3),
            pvpDamage: 0);

        if (ShouldSuppressLegacyFriendlyFireSkillDamageLocked(
                attacker,
                targetConnectionId,
                null,
                null,
                targetMob))
            damage = 0;

        if (ShouldSuppressLegacySameClanNpcDamage(
                targetConnectionId,
                attacker.Mob[LegacyAccountSnapshot.MobClanOffset],
                targetMob[LegacyAccountSnapshot.MobClanOffset]))
            damage = 0;

        if (damage > 0 && targetState.CharacterClass is 0 or 1 && HasLegacyAffectType(npc.AffectSnapshot, type: 6))
            damage = 0;

        if (LegacyAttackClanSixDamageGate.ShouldSuppressDamage(
                targetMob[LegacyAccountSnapshot.MobClanOffset],
                LegacySkillCombatMath.GetSkillSpecial(skill, attackerState)))
            damage = 0;

        damage = LegacyForceMobDamageMath.ApplyToMobTargetDamage(damage, attacker.Affect);

        damage = LegacyAttackDamageCap.Apply(damage);
        var sourceRetaliationTailReached = damage > 0;

        var clearTargetIdOnWire = LegacyAttackSameClanAreaGate.ShouldClearTargetId(
            attacker.PositionX,
            attacker.PositionY,
            attacker.Mob[LegacyAccountSnapshot.MobClanOffset],
            targetMob[LegacyAccountSnapshot.MobClanOffset],
            damage);
        if (clearTargetIdOnWire)
        {
            sourceRetaliationTailReached = false;
            damage = 0;
        }

        var secondaryAffectBeforeProc = targetAffect.ToArray();
        var secondaryAffectChanged = damage > 0 && TryApplyLegacyRsvSecondaryAttackAffects(
            targetAffect,
            targetMob,
            attackerState,
            targetState,
            targetConnectionId,
            frostRoll,
            drainRoll);
        if (secondaryAffectChanged)
        {
            affectChanged = true;
            holyTouchBaseScore = ApplyNpcHolyTouchAffectScoreTransition(
                targetMob,
                secondaryAffectBeforeProc,
                targetAffect,
                holyTouchBaseScore);
        }

        var zardMobLeftCount = ApplyZardQuestLocked(attacker, targetMob, out var zardRewardDrop, out var zardPrivateNotice);
        IReadOnlyList<LegacyItemDrop> zardItemDrops = zardRewardDrop is { } npcSkillZardReward ? [npcSkillZardReward] : [];
        IReadOnlyList<LegacyPrivateNotice> zardPrivateNotices = zardPrivateNotice is { } npcSkillZardNotice ? [npcSkillZardNotice] : [];
        if (!zardMobLeftCount.HasValue)
            ApplyPvpJewelAbsorptionLocked(attacker, damage);

        var remainingHp = damage <= 0 ? targetState.CurrentScore.Hp : Math.Max(0, targetState.CurrentScore.Hp - damage);
        BinaryPrimitives.WriteInt32LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), remainingHp);
        if (deferDeathResolution && remainingHp == 0)
        {
            npcs[targetConnectionId] = npc with
            {
                MobSnapshot = targetMob,
                AffectSnapshot = targetAffect,
                HolyTouchBaseScore = holyTouchBaseScore,
                FrozenBladeBaseScore = frozenBladeBaseScore,
            };
            outcome = new LegacySkillAttackOutcome(
                targetConnectionId,
                baseDamage,
                damage,
                resistance,
                remainingHp,
                targetMob.ToArray(),
                affectChanged ? targetAffect.ToArray() : null,
                TargetHpChanged: true,
                ItemDrops: zardItemDrops,
                PrivateNotices: zardPrivateNotices,
                ZardMobLeftCount: zardMobLeftCount);
            return LegacySkillAttackResult.Accepted;
        }

        var targetRemoved = remainingHp == 0 && RemoveNpcLocked(targetConnectionId);
        IReadOnlyList<LegacyAttackViewRemovalEffect> npcSkillViewRemovalEffects = [];
        if (!targetRemoved)
        {
            npcs[targetConnectionId] = npc with
            {
                MobSnapshot = targetMob,
                AffectSnapshot = targetAffect,
                HolyTouchBaseScore = holyTouchBaseScore,
                FrozenBladeBaseScore = frozenBladeBaseScore,
            };
            if (sourceRetaliationTailReached && remainingHp > 0)
            {
                // TrySetNpcBattle walks the attacker party and can discard dead
                // summons; capture their view removals before aggro propagation.
                npcSkillViewRemovalEffects = PruneDeadPartySummonsForAttackTailLocked(
                    attacker.ConnectionId,
                    targetConnectionId,
                    attackActionSlotIndex);
                if (npc.GenerateIndex >= 0 && npc.AutoTradeSnapshot is null)
                    TrySetNpcBattle(targetConnectionId, attacker.ConnectionId);
            }
        }

        long experienceAwarded = 0;
        byte[]? attackerMobSnapshot = null;
        IReadOnlyList<LegacyExperienceAward> experienceAwards = [];
        IReadOnlyList<LegacyItemDrop> itemDrops = zardItemDrops;
        IReadOnlyList<LegacyCoinUpdate> coinUpdates = [];
        IReadOnlyList<LegacyPrivateNotice> privateNotices = zardPrivateNotices;
        IReadOnlyList<LegacyGlobalNotice> globalNotices = [];
        IReadOnlyList<LegacyAreaNotice> areaNotices = [];
        LegacyPistaTransition? pistaTransition = null;
        if (targetRemoved && targetMob[LegacyAccountSnapshot.MobClanOffset] != 4)
        {
            experienceAwards = AwardNpcExperienceLocked(attacker, attackerState, targetState, npc.PositionX, npc.PositionY);
            var attackerAward = experienceAwards.FirstOrDefault(award => award.ConnectionId == attacker.ConnectionId);
            experienceAwarded = attackerAward?.Experience ?? 0;
            attackerMobSnapshot = attackerAward?.MobSnapshot;
            }
            if (targetRemoved)
            {
                var effectivePistaChanceRoll = runeChanceRoll;
                if (npc.GenerateIndex is 5653 or 5654 && effectivePistaChanceRoll < 0)
                    effectivePistaChanceRoll = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100);
                pistaTransition = ApplyPistaDeathLocked(attacker, npc, pistaRandomRoll, effectivePistaChanceRoll);
                areaNotices = ApplyNpcDeathNoticesLocked(npc, ReadMobName(attacker.Mob));
                var dropResult = AwardNpcDropsLocked(attacker, npc, targetMob, targetState.CurrentScore.Level, specialDropRoll, specialItemRoll, eventDropRoll, runeRoll, effectivePistaChanceRoll);
                if (dropResult.ItemDrops.Count > 0)
                    itemDrops = [.. itemDrops, .. dropResult.ItemDrops];
                globalNotices = dropResult.GlobalNotices;
                if (itemDrops.Count > 0)
                    attackerMobSnapshot = attacker.Mob.ToArray();
                var castleReward = AwardCastleQuestLocked(attacker, npc);
                if (castleReward is not null)
                {
                    itemDrops = [.. itemDrops, .. castleReward.ItemDrops];
                    experienceAwards = [.. experienceAwards, .. castleReward.ExperienceAwards];
                    coinUpdates = castleReward.CoinUpdates;
                    if (castleReward.PrivateNotices.Count > 0)
                        privateNotices = [.. privateNotices, .. castleReward.PrivateNotices];
                    attackerMobSnapshot = castleReward.AttackerMobSnapshot ?? attackerMobSnapshot;
                }
            }

        outcome = new LegacySkillAttackOutcome(
            targetConnectionId,
            baseDamage,
            damage,
            resistance,
            remainingHp,
            targetMob.ToArray(),
            TargetRemoved: targetRemoved,
            TargetAffectSnapshot: affectChanged ? targetAffect.ToArray() : null,
            ExperienceAwarded: experienceAwarded,
            AttackerMobSnapshot: attackerMobSnapshot,
            ExperienceAwards: experienceAwards,
            ItemDrops: itemDrops,
            PistaTransition: pistaTransition,
            GlobalNotices: globalNotices,
            AreaNotices: areaNotices,
            CoinUpdates: coinUpdates,
            PrivateNotices: privateNotices,
            ClearTargetIdOnWire: clearTargetIdOnWire,
            ZardMobLeftCount: zardMobLeftCount,
            ViewRemovalEffects: npcSkillViewRemovalEffects);
        return LegacySkillAttackResult.Accepted;
    }

    private IReadOnlyList<LegacyAreaNotice> ApplyNpcDeathNoticesLocked(LegacyWorldNpc npc, string attackerName)
    {
        return npc.GenerateIndex switch
        {
            8 => ApplyKingdomNoticeLocked(
                kingdom: 1,
                "Reiniciar reino Hekalotia para guerreiro do rei Harbalade.",
                1676,
                1556,
                1776,
                1636),
            9 => ApplyKingdomNoticeLocked(
                kingdom: 2,
                "Reiniciar reino Akeronia para guerreiro do rei Glentowat.",
                1676,
                1816,
                1776,
                1892),
            _ when IsCastleQuestBossLocked(npc) && npc.PositionX >= 2176 && npc.PositionX <= 2300 && npc.PositionY >= 1160 && npc.PositionY <= 1276
                => ApplyCastleQuestNoticeLocked(attackerName),
            _ => [],
        };
    }

    private bool IsCastleQuestBossLocked(LegacyWorldNpc npc)
        => castleQuests.Any(quest => quest.MatchesBoss(npc.GenerateIndex));

    private IReadOnlyList<LegacyAreaNotice> ApplyCastleQuestNoticeLocked(string attackerName)
    {
        combatWorldState = combatWorldState with { CastleQuestClear = 1 };
        return [new LegacyAreaNotice($"{attackerName} derrotou Boss da Quest 2 Castelos..", 2176, 1160, 2300, 1276)];
    }

    private IReadOnlyList<LegacyAreaNotice> ApplyKingdomNoticeLocked(int kingdom, string message, int x1, int y1, int x2, int y2)
    {
        combatWorldState = kingdom == 1
            ? combatWorldState with { Kingdom1Clear = 1 }
            : combatWorldState with { Kingdom2Clear = 1 };
        return [new LegacyAreaNotice(message, x1, y1, x2, y2)];
    }

    private LegacyCastleQuestRewardResult? AwardCastleQuestLocked(Participant attacker, LegacyWorldNpc npc)
    {
        var quest = castleQuests.FirstOrDefault(candidate => candidate.MatchesBoss(npc.GenerateIndex));
        if (quest is null)
            return null;

        var leaderConnectionId = partyLeaders.TryGetValue(attacker.ConnectionId, out var configuredLeader)
            ? configuredLeader
            : attacker.ConnectionId;
        var recipientIds = quest.PartyPrize
            ? partyLeaders.Where(pair => pair.Value == leaderConnectionId).Select(pair => pair.Key).Append(leaderConnectionId).Distinct().ToArray()
            : [leaderConnectionId];

        var itemDrops = new List<LegacyItemDrop>();
        var experienceAwards = new List<LegacyExperienceAward>();
        var coinUpdates = new List<LegacyCoinUpdate>();
        var privateNotices = new List<LegacyPrivateNotice>();
        byte[]? attackerMobSnapshot = null;
        foreach (var recipientId in recipientIds)
        {
            if (!participants.TryGetValue(recipientId, out var recipient) || recipient.Mob.Length < LegacyAccountSnapshot.CharacterStride)
                continue;

            var mutated = false;
            foreach (var prize in quest.Prizes)
            {
                if (prize.Index == 0)
                    continue;
                if (TryInsertItemLocked(recipient, prize, out var carrySlot))
                {
                    itemDrops.Add(new LegacyItemDrop(recipient.ConnectionId, carrySlot, prize));
                    mutated = true;
                }
            }

            var classMaster = recipient.ClassMaster;
            var experience = classMaster >= 0 && classMaster < quest.ExpPrize.Count
                ? quest.ExpPrize[classMaster]
                : 0;
            if (experience > 0)
            {
                var currentExperience = BinaryPrimitives.ReadInt64LittleEndian(recipient.Mob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset));
                BinaryPrimitives.WriteInt64LittleEndian(recipient.Mob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset), checked(currentExperience + experience));
                experienceAwards.Add(new LegacyExperienceAward(recipient.ConnectionId, experience, recipient.Mob.ToArray()));
                privateNotices.Add(new LegacyPrivateNotice(recipient.ConnectionId, $"Você ganhou {experience} EXP."));
                mutated = true;
            }

            if (quest.CoinPrize != 0)
            {
                var currentCoin = BinaryPrimitives.ReadInt32LittleEndian(recipient.Mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset));
                var finalCoin = (long)currentCoin + quest.CoinPrize;
                if (finalCoin <= 2_000_000_000L && finalCoin >= int.MinValue)
                {
                    recipient.Coin = checked((int)finalCoin);
                    BinaryPrimitives.WriteInt32LittleEndian(recipient.Mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), recipient.Coin);
                    coinUpdates.Add(new LegacyCoinUpdate(recipient.ConnectionId, recipient.Coin, quest.CoinPrize, recipient.Mob.ToArray()));
                    privateNotices.Add(new LegacyPrivateNotice(recipient.ConnectionId, $"Você recebeu {quest.CoinPrize} Gold."));
                    mutated = true;
                }
                else
                    privateNotices.Add(new LegacyPrivateNotice(recipient.ConnectionId, "Não é possível armazenar mais que 2 bilhões de Gold."));
            }

            if (recipient.ConnectionId == attacker.ConnectionId && mutated)
                attackerMobSnapshot = recipient.Mob.ToArray();
        }

        return new LegacyCastleQuestRewardResult(itemDrops, experienceAwards, coinUpdates, privateNotices, attackerMobSnapshot);
    }

    private LegacyPistaTransition? ApplyPistaDeathLocked(Participant attacker, LegacyWorldNpc npc, int pistaRandomRoll, int pistaChanceRoll)
    {
        if (pistaRandomRoll is < -1 or >= 7)
            throw new ArgumentOutOfRangeException(nameof(pistaRandomRoll), pistaRandomRoll, "The injected Pista boss roll must be -1 or in the range 0..6.");

        if (npc.GenerateIndex is 5653 or 5654)
        {
            if (pistaChanceRoll is < 0 or >= 100)
                throw new ArgumentOutOfRangeException(nameof(pistaChanceRoll), pistaChanceRoll, "The injected Pista +0 chance roll must be in the range 0..99.");

            var leaderConnectionId = partyLeaders.TryGetValue(attacker.ConnectionId, out var configuredLeader)
                ? configuredLeader
                : attacker.ConnectionId;
            var roomNpcs = npcs.Values
                .Where(static candidate => candidate.GenerateIndex is 5653 or 5654)
                .ToArray();
            foreach (var roomNpc in roomNpcs)
            {
                npcs.Remove(roomNpc.ConnectionId);
                ForgetGuildTreasuryNpcRegistrationLocked(roomNpc.ConnectionId);
            }

            var success = pistaChanceRoll < 20;
            return new LegacyPistaTransition(
                0,
                leaderConnectionId,
                0,
                0,
                0,
                SpawnGenerateIndexes: success ? [] : [5653, 5653, 5654, 5654],
                RemovedNpcConnectionIds: roomNpcs.Select(static roomNpc => roomNpc.ConnectionId).ToArray());
        }

        if (npc.GenerateIndex is >= PistaLv6DespawnInitialGenerateIndex and <= PistaLv6DespawnEndGenerateIndex && pistaLv6State is not null)
        {
            var leaderConnectionId = partyLeaders.TryGetValue(attacker.ConnectionId, out var configuredLeader)
                ? configuredLeader
                : attacker.ConnectionId;
            if (leaderConnectionId != pistaLv6State.LeaderConnectionId)
                return null;

            var previous = pistaLv6MobsLeft;
            if (previous >= 100)
                return null;

            var remaining = previous + 1;
            var spawn = remaining == 100 ? LegacyMobAiPlanner.RunequestLv6BossGenerateIndex : 0;
            pistaLv6MobsLeft = remaining;
            pistaLv6State = pistaLv6State with { MobsLeft = pistaLv6MobsLeft };
            return new LegacyPistaTransition(6, leaderConnectionId, previous, remaining, spawn);
        }

        if (npc.GenerateIndex is >= 5972 and <= 5975)
        {
            var initialRoom = pistaRegistrations.Values.FirstOrDefault(static registration => registration.Level == 3 && registration.PartySlot == 0);
            if (initialRoom is null || !pistaMobCounts.TryGetValue((3, 0), out var current) || current != 0)
                return null;

            var spawn = 5948 + (pistaRandomRoll >= 0 ? pistaRandomRoll : System.Security.Cryptography.RandomNumberGenerator.GetInt32(7));
            pistaMobCounts[(3, 0)] = 1;
            return new LegacyPistaTransition(3, initialRoom.LeaderConnectionId, 0, 1, spawn);
        }

        if (npc.GenerateIndex is >= 5706 and <= 5708)
        {
            var partySlot = npc.GenerateIndex - 5706;
            var key = (1, partySlot);
            if (!pistaRegistrations.ContainsKey(key))
                return null;

            var previous = pistaMobCounts.GetValueOrDefault(key);
            pistaMobCounts[key] = 0;
            return new LegacyPistaTransition(1, pistaRegistrations[key].LeaderConnectionId, previous, 0, 0);
        }

        if (npc.GenerateIndex is >= 5709 and <= 5764)
        {
            var leaderConnectionId = partyLeaders.TryGetValue(attacker.ConnectionId, out var configuredLeader)
                ? configuredLeader
                : attacker.ConnectionId;
            for (var partySlot = 0; partySlot < 3; partySlot++)
            {
                var key = (1, partySlot);
                if (!pistaRegistrations.TryGetValue(key, out var registration) || registration.LeaderConnectionId != leaderConnectionId)
                    continue;
                if (npcs.Values.All(other => other.GenerateIndex != 5706 + partySlot))
                    continue;

                var previous = pistaMobCounts.GetValueOrDefault(key);
                var remaining = previous + 1;
                pistaMobCounts[key] = remaining;
                return new LegacyPistaTransition(1, leaderConnectionId, previous, remaining, 0);
            }
        }

        if (npc.GenerateIndex is >= 5854 and <= 5898)
        {
            var leaderConnectionId = partyLeaders.TryGetValue(attacker.ConnectionId, out var configuredLeader)
                ? configuredLeader
                : attacker.ConnectionId;
            var registration = pistaRegistrations.Values.FirstOrDefault(candidate => candidate.Level == 4 && candidate.LeaderConnectionId == leaderConnectionId);
            if (registration is null)
                return null;

            var key = (4, registration.PartySlot);
            var previous = pistaMobCounts.GetValueOrDefault(key);
            if (previous <= 0 || npcs.Values.Any(other => other.GenerateIndex == npc.GenerateIndex))
                return null;
            if (Enumerable.Range(0, 3).Any(slot => slot != registration.PartySlot && pistaMobCounts.GetValueOrDefault((4, slot)) < 1))
                return null;

            var remaining = previous - 1;
            pistaMobCounts[key] = remaining;
            var teleports = remaining == 0
                ? BuildPistaLabyrinthCompletionTeleportsLocked(registration)
                : [];
            return new LegacyPistaTransition(4, leaderConnectionId, previous, remaining, remaining == 0 ? 5849 : 0, teleports);
        }

        if (npc.GenerateIndex == 5899)
        {
            var leaderConnectionId = partyLeaders.TryGetValue(attacker.ConnectionId, out var configuredLeader)
                ? configuredLeader
                : attacker.ConnectionId;
            var previous = pistaMobCounts.GetValueOrDefault((5, 0));
            pistaMobCounts[(5, 0)] = 1;
            return new LegacyPistaTransition(5, leaderConnectionId, previous, 1, 0);
        }

        if (npc.GenerateIndex is < 5948 or > 5955)
            return null;

        var bossRoom = pistaRegistrations.Values.FirstOrDefault(static registration => registration.Level == 3 && registration.PartySlot == 0);
        if (bossRoom is null || !pistaMobCounts.TryGetValue((3, 0), out var activeCount) || activeCount == 0)
            return null;

        var nextBoss = 5948 + (pistaRandomRoll >= 0 ? pistaRandomRoll : System.Security.Cryptography.RandomNumberGenerator.GetInt32(7));
        var attackerLeader = partyLeaders.TryGetValue(attacker.ConnectionId, out var mappedLeader)
            ? mappedLeader
            : attacker.ConnectionId;
        var attackerRegistration = pistaRegistrations.Values.FirstOrDefault(registration => registration.Level == 3 && registration.LeaderConnectionId == attackerLeader);
        var previousCount = activeCount;
        var remainingCount = activeCount;
        if (attackerRegistration is not null)
        {
            var key = (3, attackerRegistration.PartySlot);
            previousCount = pistaMobCounts.GetValueOrDefault(key);
            remainingCount = previousCount + 1;
            pistaMobCounts[key] = remainingCount;
        }

        return new LegacyPistaTransition(3, attackerLeader, previousCount, remainingCount, nextBoss);
    }

    private IReadOnlyList<LegacyPistaTeleport> BuildPistaLabyrinthCompletionTeleportsLocked(LegacyPistaRegistration registration)
    {
        if (!participants.TryGetValue(registration.LeaderConnectionId, out var leader))
            return [];

        var teleports = new List<LegacyPistaTeleport>();
        AddPistaTeleportLocked(leader, registration, (3351, 1334), teleports);
        var memberX = registration.PartySlot == 0 ? (short)3352 : (short)3351;
        foreach (var memberId in partyLeaders.Where(pair => pair.Value == registration.LeaderConnectionId).Select(pair => pair.Key).ToArray())
        {
            if (memberId == registration.LeaderConnectionId || !participants.TryGetValue(memberId, out var member))
                continue;
            var fromX = member.PositionX;
            var fromY = member.PositionY;
            member.PositionX = memberX;
            member.PositionY = 1334;
            teleports.Add(new LegacyPistaTeleport(member.ConnectionId, registration.Level, registration.PartySlot, fromX, fromY, memberX, 1334));
        }
        return teleports;
    }

    private LegacyNpcDropResult AwardNpcDropsLocked(Participant attacker, LegacyWorldNpc npc, ReadOnlySpan<byte> npcMob, int targetLevel, int specialDropRoll, int specialItemRoll, int eventDropRoll, int runeRoll, int runeChanceRoll)
    {
        if (attacker.Mob.Length < LegacyAccountSnapshot.CharacterStride)
            return new([], []);

        var dropBonus = LegacyExperienceMath.GetEquipmentDropBonus(attacker.Mob, itemData, attacker.ClientEquipment);
        var configuredRolls = new Queue<int>();
        if (specialDropRoll >= 0) configuredRolls.Enqueue(specialDropRoll);
        if (specialItemRoll >= 0) configuredRolls.Enqueue(specialItemRoll);
        var eventRoll = eventDropRoll >= 0
            ? eventDropRoll
            : System.Security.Cryptography.RandomNumberGenerator.GetInt32(Math.Max(1, npcEventDrop?.Rate ?? 1));
        int Roll(int maximum) => configuredRolls.Count > 0
            ? configuredRolls.Dequeue()
            : System.Security.Cryptography.RandomNumberGenerator.GetInt32(maximum);

        var drops = new List<LegacyItemDrop>();
        var globalNotices = new List<LegacyGlobalNotice>();
        drops.AddRange(AwardNpcRuneDropsLocked(attacker, npc, runeRoll, runeChanceRoll));

        var candidates = new List<LegacyItemDropCandidate>();
        candidates.AddRange(LegacyNpcDropMath.SelectBossDrops(npc.GenerateIndex, npc.TerrainHeight, itemData, Roll));
        candidates.AddRange(LegacyNpcDropMath.SelectColiseumDrops(npc.GenerateIndex, npc.TerrainHeight, itemData, Roll));
        if (npcEventDrop is { Enabled: true } eventConfiguration &&
            eventConfiguration.StartIndex != 0 && eventConfiguration.EndIndex != 0 &&
            eventConfiguration.ItemIndex != 0 && eventConfiguration.Rate != 0 &&
            npcEventCurrentIndex < eventConfiguration.EndIndex &&
            eventRoll % eventConfiguration.Rate == 0)
        {
            var eventItem = LegacyNpcDropMath.CreateEventDrop(
                eventConfiguration.ItemIndex,
                npcEventCurrentIndex,
                eventConfiguration.Indexed,
                targetLevel,
                itemData,
                Roll);
            if (eventItem is not null)
            {
                candidates.Add(new LegacyItemDropCandidate(-2, eventItem.Value));
                if (eventConfiguration.Notice)
                {
                    var itemName = itemData?[eventConfiguration.ItemIndex]?.Name ?? $"item {eventConfiguration.ItemIndex}";
                    var mobName = ReadMobName(attacker.Mob);
                    var suffix = eventConfiguration.Indexed ? $" ({npcEventCurrentIndex})" : string.Empty;
                    globalNotices.Add(new LegacyGlobalNotice($"{mobName} recebeu {itemName}{suffix}."));
                }
                npcEventCurrentIndex++;
            }
        }
        candidates.AddRange(LegacyNpcDropMath.SelectCommonDrops(npcMob, targetLevel, dropBonus, itemData: itemData));
        if (candidates.Count == 0)
            return new(drops, globalNotices);

        foreach (var candidate in candidates)
        {
            if (!TryInsertItemLocked(attacker, candidate.Item, out var carrySlot))
                break;
            drops.Add(new LegacyItemDrop(attacker.ConnectionId, carrySlot, candidate.Item));
        }

        return new(drops, globalNotices);
    }

    private IReadOnlyList<LegacyItemDrop> AwardNpcRuneDropsLocked(Participant attacker, LegacyWorldNpc npc, int runeRoll, int runeChanceRoll)
    {
        var random = runeRoll >= 0
            ? (Func<int, int>)(maximum =>
            {
                if (runeRoll < 0 || runeRoll >= maximum)
                    throw new ArgumentOutOfRangeException(nameof(runeRoll), runeRoll, $"Injected rune roll must be in [0, {maximum}).");
                return runeRoll;
            })
            : static maximum => System.Security.Cryptography.RandomNumberGenerator.GetInt32(maximum);
        var chance = runeChanceRoll >= 0
            ? (Func<int, int>)(maximum =>
            {
                if (runeChanceRoll < 0 || runeChanceRoll >= maximum)
                    throw new ArgumentOutOfRangeException(nameof(runeChanceRoll), runeChanceRoll, $"Injected rune chance roll must be in [0, {maximum}).");
                return runeChanceRoll;
            })
            : null;
        var reward = LegacyNpcDropMath.SelectRuneReward(npc.GenerateIndex, npc.TerrainHeight, random, chance);
        if (reward is null)
            return [];

        var leaderConnectionId = partyLeaders.TryGetValue(attacker.ConnectionId, out var configuredLeader)
            ? configuredLeader
            : attacker.ConnectionId;
        var recipientIds = partyLeaders.TryGetValue(attacker.ConnectionId, out _)
            ? partyLeaders.Where(pair => pair.Value == leaderConnectionId).Select(pair => pair.Key).ToArray()
            : [attacker.ConnectionId];
        if (recipientIds.Length == 0)
            recipientIds = [attacker.ConnectionId];

        var drops = new List<LegacyItemDrop>(recipientIds.Length + 1);
        foreach (var recipientId in recipientIds)
        {
            if (!participants.TryGetValue(recipientId, out var recipient))
                continue;
            var rune = new LegacyItem((short)reward.ItemIndex, 0, 0, 0, 0, 0, 0);
            if (TryInsertItemLocked(recipient, rune, out var slot))
                drops.Add(new LegacyItemDrop(recipient.ConnectionId, slot, rune));
        }

        if (reward.ProgressionValue > 0 && participants.TryGetValue(leaderConnectionId, out var leader))
        {
            var nextStage = new LegacyItem(5134, 43, (byte)reward.ProgressionValue, 0, 0, 0, 0);
            if (TryInsertItemLocked(leader, nextStage, out var slot))
                drops.Add(new LegacyItemDrop(leader.ConnectionId, slot, nextStage));
        }

        return drops;
    }

    private static bool TryInsertItemLocked(Participant participant, LegacyItem item, out int carrySlot)
    {
        if (!TryFindEmptyCarrySlotLocked(participant, out carrySlot))
            return false;

        item.Write(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (carrySlot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        return true;
    }

    private static bool TryFindEmptyCarrySlotLocked(Participant participant, out int carrySlot)
    {
        carrySlot = -1;
        var maxCarry = GetLegacyMaxCarry(participant);
        for (var slot = 0; slot < Math.Min(maxCarry, LegacyAccountSnapshot.MobCarryCount); slot++)
        {
            var offset = LegacyAccountSnapshot.MobCarryOffset + (slot * LegacyItem.SizeInBytes);
            if (LegacyItem.Read(participant.Mob.AsSpan(offset, LegacyItem.SizeInBytes)).Index == 0)
            {
                carrySlot = slot;
                return true;
            }
        }

        return false;
    }

    private static bool IsLegacyStackableItem(short itemIndex) => legacyStackableItemIndices.Contains(itemIndex);

    private static bool TryConsumeResurrectionScroll(Participant participant, out int slot, out LegacyItem consumedItem)
    {
        slot = -1;
        consumedItem = default;
        for (var carrySlot = 0; carrySlot < LegacyAccountSnapshot.MobCarryCount; carrySlot++)
        {
            var itemOffset = LegacyAccountSnapshot.MobCarryOffset + (carrySlot * LegacyItem.SizeInBytes);
            var item = LegacyItem.Read(participant.Mob.AsSpan(itemOffset, LegacyItem.SizeInBytes));
            if (item.Index != 3463)
                continue;

            var amount = GetLegacyItemAmount(item);
            var updatedItem = amount > 1 ? SetLegacyItemAmount(item, amount - 1) : default;
            updatedItem.Write(participant.Mob.AsSpan(itemOffset, LegacyItem.SizeInBytes));

            var maxHp = BinaryPrimitives.ReadInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 16));
            var maxMp = BinaryPrimitives.ReadInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 20));
            BinaryPrimitives.WriteInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), maxHp);
            BinaryPrimitives.WriteInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 28), maxMp);
            participant.RequestedHp = maxHp;
            participant.RequestedMana = maxMp;
            slot = carrySlot;
            consumedItem = updatedItem;
            return true;
        }

        return false;
    }

    private static int ApplyPvpJewelAbsorptionLocked(Participant attacker, int damage)
    {
        var state = LegacyMobCombatState.Read(attacker.Mob);
        var absorption = LegacyPvpJewelAttackMath.ApplyAbsorption(
            attacker.Affect,
            damage,
            state.CurrentScore.Hp,
            state.CurrentScore.MaxHp);
        if (absorption.AbsorbedHp <= 0)
            return 0;

        BinaryPrimitives.WriteInt32LittleEndian(
            attacker.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24),
            absorption.CurrentHp);
        var requestedHp = (long)attacker.RequestedHp + absorption.AbsorbedHp;
        attacker.RequestedHp = (int)Math.Min(state.CurrentScore.MaxHp, requestedHp);
        return absorption.AbsorbedHp;
    }

    private int? ApplyZardQuestLocked(
        Participant attacker,
        ReadOnlySpan<byte> targetMob,
        out LegacyItemDrop? rewardDrop,
        out LegacyPrivateNotice? privateNotice)
    {
        rewardDrop = null;
        privateNotice = null;
        var equipmentOffset = LegacyAccountSnapshot.MobEquipmentOffset + (12 * LegacyItem.SizeInBytes);
        if (targetMob.Length < 16 || attacker.Mob.Length < equipmentOffset + LegacyItem.SizeInBytes ||
            !string.Equals(ReadMobName(targetMob), "Zard", StringComparison.Ordinal))
            return null;

        var questItem = LegacyItem.Read(attacker.Mob.AsSpan(equipmentOffset, LegacyItem.SizeInBytes));
        questItem = questItem with
        {
            Effect1 = 51,
            Value1 = unchecked((byte)(questItem.Value1 + 1)),
        };
        questItem.Write(attacker.Mob.AsSpan(equipmentOffset, LegacyItem.SizeInBytes));
        var mobLeftCount = questItem.Value1;
        if (mobLeftCount != 80)
            return mobLeftCount;

        var rune = new LegacyItem(475, 0, 0, 0, 0, 0, 0);
        if (TryInsertItemLocked(attacker, rune, out var carrySlot))
            rewardDrop = new LegacyItemDrop(attacker.ConnectionId, carrySlot, rune);
        else
            privateNotice = new LegacyPrivateNotice(attacker.ConnectionId, "Não há espaço no inventário para realizar transações.");

        questItem = questItem with { Value1 = 0 };
        questItem.Write(attacker.Mob.AsSpan(equipmentOffset, LegacyItem.SizeInBytes));
        return mobLeftCount;
    }

    private static int ApplyLegacyPostDamageModifiers(int damage, int forceDamage, int pvpDamage)
    {
        if (forceDamage != 0)
        {
            if (damage <= 1)
                damage = forceDamage;
            else if (damage > 0)
                damage += forceDamage;
        }

        if (pvpDamage != 0)
        {
            if (damage <= 1)
                damage += damage * pvpDamage / 100;
            else if (damage > 0)
                damage += damage / 100 * pvpDamage;
        }

        return damage;
    }

    private static bool ShouldSuppressLegacySameClanNpcDamage(int targetConnectionId, byte attackerClan, byte targetClan) =>
        targetConnectionId >= LegacyBattleRoyaleGridRefreshRelay.MaxUserConnectionExclusive &&
        ((attackerClan == 7 && targetClan == 7) || (attackerClan == 8 && targetClan == 8));

    private bool ShouldSuppressLegacyFriendlyFireSkillDamageLocked(
        Participant attacker,
        int targetConnectionId,
        Participant? targetParticipant,
        LegacySummonedMob? targetSummon,
        byte[] targetMob)
    {
        var targetGuildId = targetParticipant is null
            ? BinaryPrimitives.ReadUInt16LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobGuildOffset, sizeof(ushort)))
            : targetParticipant.GuildDisabled ? 0 : targetParticipant.GuildId;
        var targetLeaderConnectionId = targetParticipant is not null
            ? GetPartyLeaderConnectionIdLocked(targetConnectionId)
            : targetSummon?.LeaderConnectionId ?? targetConnectionId;
        var attackerGuildId = LegacyPartySkillTargetGate.ResolveAttackerGuildId(
            attacker.GuildId,
            attacker.GuildDisabled,
            targetGuildId,
            combatWorldState.BattleRoyaleState,
            combatWorldState.BattleRoyaleItem,
            attacker.PositionX,
            attacker.PositionY);

        return LegacyAttackFriendlyFireGate.ShouldSuppressDamage(
            GetPartyLeaderConnectionIdLocked(attacker.ConnectionId),
            targetLeaderConnectionId,
            attackerGuildId,
            targetGuildId);
    }

    private static int GetLegacyForceDamage(
        ReadOnlySpan<byte> mob,
        IReadOnlyList<LegacyItem>? clientEquipment,
        LegacyItemDataTable? itemData,
        ReadOnlySpan<byte> affectSnapshot,
        int currentSpecial3)
    {
        var forceDamage = 0;
        if (itemData is not null && HasEquipmentLayout(mob, clientEquipment))
        {
            for (var slot = 0; slot < GetEquipmentCount(clientEquipment); slot++)
            {
                var item = ReadEquipmentItem(mob, clientEquipment, slot);
                if (item.Index <= 0 || item.Index >= LegacyItemDataTable.MaxItemIndex)
                    continue;

                var grade = itemData.GetItemGrade(item);
                var sanctuary = itemData.GetItemSanctuary(item);
                forceDamage += grade switch
                {
                    9 => 20 + sanctuary * 2,
                    20 => 30 + sanctuary * 2,
                    22 => 30 + sanctuary * 2,
                    24 => 60 + sanctuary * 2,
                    25 => 60 + sanctuary * 2,
                    26 => 80 + sanctuary * 2,
                    // The source compares the equipment slot to 20 inside a 16-slot loop.
                    6 when slot == 20 => 1,
                    _ => 0,
                };
                if (itemData.GetItemGem(item) == 1)
                    forceDamage += (grade == 6 ? 80 : 40) * GetLegacySanctuaryLevel(sanctuary);
            }
        }

        var spectralLink = LegacyCurrentScoreMath.CalculateSpectralLinkForceDamageStage(
            affectSnapshot, currentSpecial3);
        return unchecked(forceDamage + spectralLink.ForceDamageAddition);
    }

    private static int GetLegacyPvpDamage(ReadOnlySpan<byte> mob, IReadOnlyList<LegacyItem>? clientEquipment, LegacyItemDataTable? itemData)
    {
        if (itemData is null || !HasEquipmentLayout(mob, clientEquipment))
            return 0;

        var attackPvp = 0;
        for (var slot = 0; slot < GetEquipmentCount(clientEquipment); slot++)
        {
            var item = ReadEquipmentItem(mob, clientEquipment, slot);
            attackPvp += itemData.GetItemAbility(item, LegacyItemEffect.PvpAttack);
        }

        return (attackPvp + 1) / 10;
    }

    private static int GetLegacyReflectDamage(ReadOnlySpan<byte> mob, IReadOnlyList<LegacyItem>? clientEquipment, LegacyMobCombatState state, LegacyItemDataTable? itemData)
    {
        var reflectDamage = state.CharacterClass == 2 && (state.LearnedSkill & (1u << 17)) != 0
            ? (state.CurrentScore.Special4 + 1) / 6
            : 0;
        if (itemData is null || !HasEquipmentLayout(mob, clientEquipment))
            return reflectDamage;

        for (var slot = 0; slot < GetEquipmentCount(clientEquipment); slot++)
        {
            var item = ReadEquipmentItem(mob, clientEquipment, slot);
            var grade = itemData.GetItemGrade(item);
            var sanctuary = itemData.GetItemSanctuary(item);
            reflectDamage += grade switch
            {
                8 => 20,
                10 => 20 + sanctuary * 2,
                21 => 30 + sanctuary * 2,
                22 => 30 + sanctuary * 2,
                25 => 60 + sanctuary * 2,
                26 => 80 + sanctuary * 2,
                _ => 0,
            };

            if (itemData.GetItemGem(item) == 3)
                reflectDamage += (grade == 8 ? 80 : 40) * GetLegacySanctuaryLevel(sanctuary);
        }

        return reflectDamage;
    }

    private static int GetLegacySanctuaryLevel(int sanctuary) => sanctuary switch
    {
        10 => 1,
        12 => 2,
        15 => 3,
        18 => 4,
        22 => 5,
        27 => 6,
        _ => 0,
    };

    private static int GetLegacyReflectPvp(ReadOnlySpan<byte> mob, IReadOnlyList<LegacyItem>? clientEquipment, LegacyItemDataTable? itemData)
    {
        if (itemData is null || !HasEquipmentLayout(mob, clientEquipment))
            return 0;

        var reflectPvp = 0;
        for (var slot = 0; slot < GetEquipmentCount(clientEquipment); slot++)
        {
            var item = ReadEquipmentItem(mob, clientEquipment, slot);
            reflectPvp += itemData.GetItemAbility(item, LegacyItemEffect.PvpDefense);
        }

        return (reflectPvp + 1) / 10;
    }

    private static int GetLegacyItemAmount(LegacyItem item) => item.Effect1 == 61 ? item.Value1
        : item.Effect2 == 61 ? item.Value2
        : item.Effect3 == 61 ? item.Value3
        : 0;

    private static int GetLegacyMountType(int mount) => mount switch
    {
        >= 0 and <= 2 => 0,
        >= 3 and <= 6 or >= 8 and <= 11 => 1,
        7 or 12 or 24 => 2,
        >= 13 and <= 15 => 3,
        >= 18 and <= 20 or 25 => 4,
        >= 21 and <= 23 => 5,
        >= 16 and <= 17 => 6,
        _ => -1,
    };

    private static LegacyItem SetLegacyItemAmount(LegacyItem item, int amount)
    {
        var value = checked((byte)amount);
        if (item.Effect1 == 61) return item with { Value1 = value };
        if (item.Effect2 == 61) return item with { Value2 = value };
        if (item.Effect3 == 61) return item with { Value3 = value };
        if (item.Effect1 == 0) return item with { Effect1 = 61, Value1 = value };
        if (item.Effect2 == 0) return item with { Effect2 = 61, Value2 = value };
        return item with { Effect3 = 61, Value3 = value };
    }

    private static bool HasSanctuaryEffect(LegacyItem item) =>
        item.Effect1 == LegacyItemEffect.Sanctuary || item.Effect2 == LegacyItemEffect.Sanctuary || item.Effect3 == LegacyItemEffect.Sanctuary ||
        item.Effect1 is >= 116 and <= 125 || item.Effect2 is >= 116 and <= 125 || item.Effect3 is >= 116 and <= 125;

    private static int GetEquipmentCount(IReadOnlyList<LegacyItem>? clientEquipment) =>
        clientEquipment?.Count == CharacterMobV769.EquipmentCount
            ? CharacterMobV769.EquipmentCount
            : LegacyCharacterSelection.EquipmentCount;

    private static bool HasEquipmentLayout(ReadOnlySpan<byte> mob, IReadOnlyList<LegacyItem>? clientEquipment) =>
        clientEquipment?.Count == CharacterMobV769.EquipmentCount ||
        mob.Length >= LegacyAccountSnapshot.MobEquipmentOffset + (LegacyCharacterSelection.EquipmentCount * LegacyItem.SizeInBytes);

    private static LegacyItem ReadEquipmentItem(ReadOnlySpan<byte> mob, IReadOnlyList<LegacyItem>? clientEquipment, int slot) =>
        clientEquipment?.Count == CharacterMobV769.EquipmentCount
            ? clientEquipment[slot]
            : LegacyItem.Read(mob.Slice(LegacyAccountSnapshot.MobEquipmentOffset + (slot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));

    private static int ResolveLegacyAttackMiss(
        LegacyMobCombatState attackerState,
        LegacyMobCombatState targetState,
        int attackMissRoll,
        int damage)
    {
        var missRate = LegacyAttackMissMath.GetRatePercent(
            targetState.CurrentScore.Dexterity,
            attackerState.CurrentScore.Dexterity,
            attackerState.Rsv);
        var roll = attackMissRoll >= 0
            ? attackMissRoll
            : System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 100);
        return roll < missRate ? -3 : damage;
    }

    private static int ResolveLegacyParry(
        ReadOnlySpan<byte> targetMob,
        IReadOnlyList<LegacyItem>? clientEquipment,
        int targetParryAdjustment,
        LegacyMobCombatState attackerState,
        LegacyMobCombatState targetState,
        LegacyItemDataTable? itemData,
        int skillId,
        int parryRandomRoll,
        int damage)
    {
        if (itemData is null || damage <= 0)
            return damage;

        var targetParry = 0;
        for (var slot = 0; slot < GetEquipmentCount(clientEquipment); slot++)
        {
            var item = ReadEquipmentItem(targetMob, clientEquipment, slot);
            if (item.Index != 0 || slot == 7)
                targetParry += itemData.GetItemAbility(item, LegacyItemEffect.Parry);
        }
        targetParry += targetParryAdjustment;

        var attackerDex = attackerState.CurrentScore.Dexterity / 5;
        if ((attackerState.LearnedSkill & 0x1000000) != 0)
            attackerDex += 100;
        if ((attackerState.Rsv & 0x40) != 0)
            attackerDex += 500;

        var parryRate = LegacySkillCombatMath.GetParryRate(targetState.CurrentScore.Dexterity, targetParry, attackerDex, attackerState.Rsv);
        if (skillId is 79 or 22)
            parryRate = 30 * parryRate / 100;

        var roll = parryRandomRoll >= 0 ? parryRandomRoll : System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, 1001);
        if (roll >= parryRate)
            return damage;

        return (targetState.Rsv & 0x200) != 0 && roll < 100 ? -4 : -3;
    }

    private (short X, short Y) GetLegacyRecallPosition(
        Participant participant,
        LegacyMobCombatState state,
        int cityRandomX,
        int cityRandomY,
        int newbieRandomX,
        int newbieRandomY)
    {
        return GetLegacyPosition(
            participant.ConnectionId,
            participant.GuildId,
            participant.ClassMaster,
            state,
            cityRandomX,
            cityRandomY,
            newbieRandomX,
            newbieRandomY,
            newbieOriginX: 2100,
            newbieOriginY: 2100);
    }

    private (short X, short Y) GetLegacyPosition(
        int connectionId,
        int guildId,
        short classMaster,
        LegacyMobCombatState state,
        int cityRandomX,
        int cityRandomY,
        int newbieRandomX,
        int newbieRandomY,
        int newbieOriginX,
        int newbieOriginY)
    {
        var cityId = (state.CurrentScore.Merchant & 0xC0) >> 6;
        var zone = LegacyRecallData.Zones[Math.Clamp(cityId, 0, LegacyRecallData.Zones.Length - 1)];
        var x = zone.CitySpawnX + (cityRandomX >= 0 ? Math.Clamp(cityRandomX, 0, 14) : System.Security.Cryptography.RandomNumberGenerator.GetInt32(15));
        var y = zone.CitySpawnY + (cityRandomY >= 0 ? Math.Clamp(cityRandomY, 0, 14) : System.Security.Cryptography.RandomNumberGenerator.GetInt32(15));

        for (var zoneIndex = 0; zoneIndex < LegacyRecallData.Zones.Length; zoneIndex++)
        {
            var guildZone = LegacyRecallData.Zones[zoneIndex];
            if (guildId > 0 && guildId == guildZones?.GetChargeGuild(zoneIndex))
            {
                x = guildZone.GuildSpawnX;
                y = guildZone.GuildSpawnY;
                break;
            }
        }

        if (classMaster == LegacyAccountSnapshot.ClassMasterMortal && state.CurrentScore.Level < LegacyRecallData.LegacyFreeExp)
        {
            x = newbieOriginX + (newbieRandomX >= 0 ? Math.Clamp(newbieRandomX, 0, 4) : System.Security.Cryptography.RandomNumberGenerator.GetInt32(5)) - 2;
            y = newbieOriginY + (newbieRandomY >= 0 ? Math.Clamp(newbieRandomY, 0, 4) : System.Security.Cryptography.RandomNumberGenerator.GetInt32(5)) - 2;
        }

        var gridX = x;
        var gridY = y;
        Func<int, int, bool> blockedAt = mapGrid is null ? static (_, _) => false : mapGrid.IsBlocked;
        LegacyMobGridSearch.TryFindEmpty(
            connectionId,
            ref gridX,
            ref gridY,
            GetParticipantAtPosition,
            blockedAt,
            checkCandidateTerrain: mapCollisionMode == LegacyMapCollisionMode.CandidateAware);

        // The in-memory WorldHub uses participant positions as its occupancy
        // source; terrain comes from the optional legacy height grid.
        return (checked((short)gridX), checked((short)gridY));
    }

    private int GetParticipantAtPosition(int x, int y)
    {
        foreach (var participant in participants.Values)
            if (!battleRoyaleDoorRemovedMobGridIds.Contains(participant.ConnectionId) && participant.PositionX == x && participant.PositionY == y)
                return participant.ConnectionId;

        foreach (var summon in summons.Values)
            if (summon.PositionX == x && summon.PositionY == y)
                return summon.ConnectionId;

        foreach (var npc in npcs.Values)
            if (npc.PositionX == x && npc.PositionY == y)
                return npc.ConnectionId;

        return 0;
    }

    private bool TryFindEmptyGeneratedNpcGridLocked(int npcConnectionId, ref int x, ref int y)
    {
        // TMProject 7.69 GetEmptyMobGrid checks each candidate through radius 9;
        // W2PP stops at radius 4 and checks the origin terrain during its fallback.
        Func<int, int, bool> blockedAt = mapGrid is null ? static (_, _) => false : mapGrid.IsBlocked;
        return LegacyMobGridSearch.TryFindEmpty(
            npcConnectionId,
            ref x,
            ref y,
            GetParticipantAtPosition,
            blockedAt,
            checkCandidateTerrain: true,
            maximumRadius: 9);
    }

    private bool TryGetDoorMobSnapshotLocked(int connectionId, out byte[] mobSnapshot, out short positionX, out short positionY)
    {
        if (participants.TryGetValue(connectionId, out var participant))
        {
            mobSnapshot = participant.Mob;
            positionX = participant.PositionX;
            positionY = participant.PositionY;
            return true;
        }

        if (summons.TryGetValue(connectionId, out var summon))
        {
            mobSnapshot = summon.MobSnapshot;
            positionX = summon.PositionX;
            positionY = summon.PositionY;
            return true;
        }

        if (npcs.TryGetValue(connectionId, out var npc))
        {
            mobSnapshot = npc.MobSnapshot;
            positionX = npc.PositionX;
            positionY = npc.PositionY;
            return true;
        }

        mobSnapshot = [];
        positionX = 0;
        positionY = 0;
        return false;
    }

    private bool TrySetDoorMobPositionLocked(int connectionId, short positionX, short positionY)
    {
        if (participants.TryGetValue(connectionId, out var participant))
        {
            participant.PositionX = positionX;
            participant.PositionY = positionY;
            battleRoyaleDoorRemovedMobGridIds.Remove(connectionId);
            return true;
        }

        if (summons.TryGetValue(connectionId, out var summon))
        {
            summons[connectionId] = summon with { PositionX = positionX, PositionY = positionY };
            return true;
        }

        if (npcs.TryGetValue(connectionId, out var npc))
        {
            npcs[connectionId] = npc with { PositionX = positionX, PositionY = positionY };
            return true;
        }

        return false;
    }

    private int[] GetBattleRoyaleDoorMobRecipientsLocked(int actorId, int positionX, int positionY, bool includeActor = true)
    {
        var recipients = GetParticipantIdsInViewLocked(checked((short)positionX), checked((short)positionY)).ToHashSet();
        if (!includeActor)
            recipients.Remove(actorId);
        else if (actorId is > 0 and < LegacyBattleRoyaleGridRefreshRelay.MaxUserConnectionExclusive && participants.ContainsKey(actorId))
            recipients.Add(actorId);
        return recipients.Order().ToArray();
    }

    private LegacyBattleRoyaleDoorPartyEffect? DeleteBattleRoyaleDoorMobLocked(int connectionId)
    {
        if (participants.TryGetValue(connectionId, out var participant))
        {
            if (participant.Mob.Length >= LegacyAccountSnapshot.MobCurrentScoreOffset + LegacyScore.SizeInBytes)
            {
                var offset = LegacyAccountSnapshot.MobCurrentScoreOffset;
                var score = LegacyScore.Read(participant.Mob.AsSpan(offset, LegacyScore.SizeInBytes));
                (score with { Hp = 0 }).Write(participant.Mob.AsSpan(offset, LegacyScore.SizeInBytes));
            }
            battleRoyaleDoorRemovedMobGridIds.Add(connectionId);

            if (!partyLeaders.TryGetValue(connectionId, out var leaderConnectionId))
                return null;

            var members = GetPartyMemberIdsLocked(leaderConnectionId);

            var disbanded = connectionId == leaderConnectionId;
            PartyFormationPlan? reformedParty = null;
            if (disbanded)
            {
                foreach (var memberId in partyLeaders.Where(pair => pair.Value == leaderConnectionId).Select(static pair => pair.Key).ToArray())
                    partyLeaders.Remove(memberId);
                var remainingMembers = members.Where(memberId => memberId != connectionId && participants.ContainsKey(memberId)).ToArray();
                var newLeaderConnectionId = remainingMembers.FirstOrDefault(static memberId =>
                    memberId is > 0 and < LegacyBattleRoyaleGridRefreshRelay.MaxUserConnectionExclusive);
                if (newLeaderConnectionId > 0 && remainingMembers.Length > 1)
                {
                    foreach (var memberId in remainingMembers)
                        partyLeaders[memberId] = newLeaderConnectionId;
                    reformedParty = new PartyFormationPlan(
                        newLeaderConnectionId,
                        remainingMembers.Select(memberId => CreatePartyMemberSnapshot(participants[memberId])).ToArray());
                }

                foreach (var summon in summons.Values
                             .Where(summon => summon.LeaderConnectionId == leaderConnectionId && summon.SummonerConnectionId != connectionId)
                             .ToArray())
                {
                    if (participants.TryGetValue(summon.SummonerConnectionId, out var owner))
                        owner.SummonIds.Remove(summon.ConnectionId);
                    summons[summon.ConnectionId] = summon with { LeaderConnectionId = 0, SummonerConnectionId = 0 };
                }
            }
            else
                partyLeaders.Remove(connectionId);

            return new LegacyBattleRoyaleDoorPartyEffect(
                new PartyRemovalPlan(
                    leaderConnectionId,
                    connectionId,
                    members.Where(participants.ContainsKey).ToArray(),
                    disbanded),
                reformedParty);
        }

        if (summons.ContainsKey(connectionId))
            RemoveSummonLocked(connectionId);
        else
        {
            npcs.Remove(connectionId);
            ForgetGuildTreasuryNpcRegistrationLocked(connectionId);
        }
        return null;
    }

    private IReadOnlyList<LegacyBattleRoyaleDoorSummonDespawn> RemoveDoorOwnedSummonsLocked(int summonerConnectionId)
    {
        var removed = new List<LegacyBattleRoyaleDoorSummonDespawn>();
        foreach (var summon in summons.Values.Where(summon => summon.SummonerConnectionId == summonerConnectionId).ToArray())
        {
            var recipients = GetBattleRoyaleDoorMobRecipientsLocked(summon.ConnectionId, summon.PositionX, summon.PositionY);
            if (!RemoveSummonLocked(summon.ConnectionId))
                continue;
            removed.Add(new LegacyBattleRoyaleDoorSummonDespawn(
                summon.ConnectionId,
                summon.PositionX,
                summon.PositionY,
                Array.AsReadOnly(recipients)));
        }
        return Array.AsReadOnly(removed.ToArray());
    }

    private bool RemoveNpcLocked(int npcConnectionId)
    {
        if (!npcs.Remove(npcConnectionId))
            return false;
        ForgetGuildTreasuryNpcRegistrationLocked(npcConnectionId);
        ClearRemovedTargetLocked(npcConnectionId);
        return true;
    }

    private IReadOnlyList<LegacyExperienceAward> AwardNpcExperienceLocked(
        Participant attacker,
        LegacyMobCombatState attackerState,
        LegacyMobCombatState targetState,
        short targetPositionX,
        short targetPositionY)
    {
        var targetExperience = Math.Clamp(targetState.Experience, 0, int.MaxValue);
        var recipientIds = partyLeaders.TryGetValue(attacker.ConnectionId, out var leaderConnectionId)
            ? partyLeaders.Where(pair => pair.Value == leaderConnectionId).Select(pair => pair.Key).ToArray()
            : [attacker.ConnectionId];
        if (recipientIds.Length == 0)
            recipientIds = [attacker.ConnectionId];

        var attackerExperienceBonus = LegacyExperienceMath.GetEquipmentExperienceBonus(attacker.Mob, attacker.Affect, itemData, attacker.ClientEquipment);
        var awards = new List<LegacyExperienceAward>(recipientIds.Length);
        foreach (var recipientId in recipientIds)
        {
            if (!participants.TryGetValue(recipientId, out var recipient))
                continue;
            if (recipient.Mob.Length < LegacyAccountSnapshot.CharacterStride)
                continue;
            var recipientState = LegacyMobCombatState.Read(recipient.Mob);
            if (recipientState.CurrentScore.Hp <= 0)
                continue;
            if (recipientId != attacker.ConnectionId && LegacyCombatMath.GetDistance(recipient.PositionX, recipient.PositionY, targetPositionX, targetPositionY) > 23)
                continue;

            var eligibility = new LegacyExperienceEligibility(recipient.ClassMaster);
            var normalizedExperience = LegacyExperienceMath.GetExpApply(
                eligibility,
                (int)targetExperience,
                recipientState.CurrentScore.Level,
                    targetState.CurrentScore.Level);
            var experience = recipientId == attacker.ConnectionId && recipientIds.Length == 1
                ? normalizedExperience
                : LegacyPartyExperienceMath.GetGenericMemberExperience(
                    eligibility,
                    normalizedExperience,
                    recipientState.CurrentScore.Level,
                    (int)targetExperience,
                    targetState.CurrentScore.Level,
                    combatWorldState.NewbieEventServer,
                    combatWorldState.DoubleModeMultiplier,
                    combatWorldState.KefraLive);
            if (attackerExperienceBonus > 0 && attackerExperienceBonus < 500)
                experience += experience * attackerExperienceBonus / 100;
            if (experience > 0 && experience <= 10_000_000)
                UpdateExperienceDayLog(recipient, experience);
            var hold = BinaryPrimitives.ReadUInt32LittleEndian(recipient.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraHoldOffset));
            if (hold > 0)
            {
                if ((ulong)Math.Max(0, experience) >= hold)
                {
                    experience -= checked((int)hold);
                    BinaryPrimitives.WriteUInt32LittleEndian(recipient.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraHoldOffset), 0);
                }
                else
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(recipient.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraHoldOffset), hold - (uint)Math.Max(0, experience));
                    continue;
                }
            }
            if (experience <= 0)
                continue;

            var updatedExperience = checked(recipientState.Experience + experience);
            BinaryPrimitives.WriteInt64LittleEndian(recipient.Mob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset), updatedExperience);
            awards.Add(new LegacyExperienceAward(recipientId, experience, recipient.Mob.ToArray()));
        }

        return awards;
    }

    private static void UpdateExperienceDayLog(Participant participant, int experience)
    {
        var yearDay = DateTime.Now.DayOfYear - 1;
        var storedYearDay = BinaryPrimitives.ReadInt32LittleEndian(participant.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraDayLogYearDayOffset));
        if (storedYearDay != yearDay)
            BinaryPrimitives.WriteInt64LittleEndian(participant.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraDayLogExpOffset), 0);

        BinaryPrimitives.WriteInt32LittleEndian(participant.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraDayLogYearDayOffset), yearDay);
        var currentExperience = BinaryPrimitives.ReadInt64LittleEndian(participant.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraDayLogExpOffset));
        BinaryPrimitives.WriteInt64LittleEndian(participant.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraDayLogExpOffset), checked(currentExperience + experience));
    }

    private bool RemoveSummonLocked(int summonConnectionId)
    {
        if (!summons.Remove(summonConnectionId, out var summon)) return false;
        summonTargets.Remove(summonConnectionId);
        if (participants.TryGetValue(summon.SummonerConnectionId, out var summoner))
            summoner.SummonIds.Remove(summonConnectionId);
        ClearRemovedTargetLocked(summonConnectionId);
        return true;
    }

    private void ClearRemovedTargetLocked(int targetConnectionId)
    {
        foreach (var npc in npcs.Values.Where(npc => npc.CurrentTarget == targetConnectionId || npc.EnemyList.Contains(targetConnectionId)).ToArray())
        {
            var enemyList = NormalizeNpcEnemyList(npc.EnemyList);
            for (var index = 0; index < enemyList.Length; index++)
                if (enemyList[index] == targetConnectionId)
                    enemyList[index] = 0;
            npcs[npc.ConnectionId] = npc with
            {
                CurrentTarget = npc.CurrentTarget == targetConnectionId ? LegacyNpcMode.EmptyTarget : npc.CurrentTarget,
                EnemyList = enemyList,
            };
        }

        foreach (var summon in summons.Values.Where(summon => summon.EnemyList.Contains(targetConnectionId)).ToArray())
        {
            var enemyList = NormalizeNpcEnemyList(summon.EnemyList);
            for (var index = 0; index < enemyList.Length; index++)
                if (enemyList[index] == targetConnectionId)
                    enemyList[index] = 0;
            summons[summon.ConnectionId] = summon with { EnemyList = enemyList };
        }

        foreach (var summonId in summonTargets.Where(pair => pair.Value == targetConnectionId).Select(pair => pair.Key).ToArray())
            summonTargets[summonId] = LegacyNpcMode.EmptyTarget;
    }

    private bool TryLinkMountHpLocked(
        LegacySummonedMob summon,
        ReadOnlySpan<byte> summonMob,
        out int ownerConnectionId,
        out LegacyItem? updatedMountItem)
    {
        ownerConnectionId = 0;
        updatedMountItem = null;
        if (summonMob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize || summonMob[LegacyAccountSnapshot.MobClanOffset] != 4)
            return false;

        var summonFace = ReadEquipmentIndex(summonMob, equipmentSlot: 0);
        if (summonFace is < 315 or >= 345)
            return false;
        if (!participants.TryGetValue(summon.SummonerConnectionId, out var owner) || owner.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
            return false;

        var mountOffset = LegacyAccountSnapshot.MobEquipmentOffset + (14 * LegacyItem.SizeInBytes);
        var mountItem = LegacyItem.Read(owner.Mob.AsSpan(mountOffset, LegacyItem.SizeInBytes));
        var mountId = mountItem.Index - 2330;
        if (mountId != summonFace - 315)
            return false;

        var mountHpItem = BinaryPrimitives.ReadInt16LittleEndian(owner.Mob.AsSpan(mountOffset + 2, sizeof(short)));
        var mountHp = LegacyMobCombatState.Read(summonMob).CurrentScore.Hp;
        if (mountHpItem == mountHp)
            return false;

        BinaryPrimitives.WriteInt16LittleEndian(owner.Mob.AsSpan(mountOffset + 2, sizeof(short)), checked((short)mountHp));
        ownerConnectionId = owner.ConnectionId;
        updatedMountItem = LegacyItem.Read(owner.Mob.AsSpan(mountOffset, LegacyItem.SizeInBytes));
        return true;
    }

    private bool TryProcessAdultMountLocked(Participant target, int hpLost, out LegacyItem? updatedMountItem)
    {
        updatedMountItem = null;
        var mountOffset = LegacyAccountSnapshot.MobEquipmentOffset + (14 * LegacyItem.SizeInBytes);
        if (target.Mob.Length < mountOffset + LegacyItem.SizeInBytes)
            return false;

        var mountItem = LegacyItem.Read(target.Mob.AsSpan(mountOffset, LegacyItem.SizeInBytes));
        if (mountItem.Index is < 2360 or >= 2390)
            return false;

        var maxHp = GetLegacyAdultMountMaxHp(mountItem);
        var mountHp = BinaryPrimitives.ReadInt16LittleEndian(target.Mob.AsSpan(mountOffset + 2, sizeof(short)));
        var feed = mountItem.Effect3;
        if (feed <= 0 && mountHp > 0)
        {
            mountHp = 0;
            BinaryPrimitives.WriteInt16LittleEndian(target.Mob.AsSpan(mountOffset + 2, sizeof(short)), 0);
        }

        var updatedHp = mountHp - hpLost;
        if (updatedHp >= maxHp)
            updatedHp = maxHp;

        BinaryPrimitives.WriteInt16LittleEndian(target.Mob.AsSpan(mountOffset + 2, sizeof(short)), checked((short)updatedHp));
        if (updatedHp <= 0)
            target.Mob[mountOffset + 6] = 0;

        if (mountHp != updatedHp)
            updatedMountItem = LegacyItem.Read(target.Mob.AsSpan(mountOffset, LegacyItem.SizeInBytes));

        return mountHp != updatedHp;
    }

    private int GetLegacyAdultMountMaxHp(LegacyItem mountItem)
    {
        var summonId = mountItem.Index - 2360 + 10;
        if (summonCatalog is null || !summonCatalog.TryGet(summonId, out var template) || template is null)
            return short.MaxValue;

        return Math.Max(1, LegacyMobCombatState.Read(template.MobSnapshot).CurrentScore.MaxHp);
    }

    private LegacySummonGenerationResult TryGenerateSummons(
        int leaderConnectionId,
        int summonerConnectionId,
        int summonId,
        int requestedCount,
        Participant summoner,
        LegacyMobCombatState summonerState,
        out IReadOnlyList<LegacySummonedMob> created,
        out IReadOnlyList<LegacySummonMovement> recalled,
        out IReadOnlyList<LegacyPartyAddNotice> partyAdds)
    {
        // TMProject 7.69 GenerateSummon (Server.cpp:3763-4053); W2PP differs only in the conflict scan.
        var createdSummons = new List<LegacySummonedMob>();
        var recalledSummons = new List<LegacySummonMovement>();
        var partyAddNotices = new List<LegacyPartyAddNotice>();
        created = createdSummons;
        recalled = recalledSummons;
        partyAdds = partyAddNotices;
        if (summonId < 0 || summonId >= LegacySummonCatalog.SlotCount)
            return LegacySummonGenerationResult.InvalidSummonId;
        if (summonCatalog is null || !summonCatalog.TryGet(summonId, out var template) || template is null)
            return LegacySummonGenerationResult.CatalogUnavailable;

        var requestedFace = ReadEquipmentIndex(template.MobSnapshot, equipmentSlot: 0);
        // 7.69 compares every Leader.PartyList member against the first eight BaseSummon faces;
        // W2PP scans the whole list (MAX_SUMMONLIST). The port follows 7.69.
        var conflictFaces = new HashSet<ushort>();
        for (var summonSlot = 0; summonSlot < LegacySummonFaceConflictSlots; summonSlot++)
        {
            if (summonCatalog.TryGet(summonSlot, out var conflictTemplate) && conflictTemplate is not null)
                conflictFaces.Add(ReadEquipmentIndex(conflictTemplate.MobSnapshot, equipmentSlot: 0));
        }

        var existingSameType = 0;
        foreach (var memberConnectionId in GetLegacyPartyListLocked(leaderConnectionId))
        {
            var memberMob = participants.TryGetValue(memberConnectionId, out var memberParticipant)
                ? memberParticipant.Mob
                : summons.TryGetValue(memberConnectionId, out var memberSummon) ? memberSummon.MobSnapshot : null;
            if (memberMob is null || memberMob.Length < LegacyAccountSnapshot.CharacterStride)
                continue;

            var memberFace = ReadEquipmentIndex(memberMob, equipmentSlot: 0);
            if (conflictFaces.Contains(memberFace) && memberFace != requestedFace)
                return LegacySummonGenerationResult.TypeConflict;
            if (memberFace != requestedFace)
                continue;

            existingSameType++;
            // An existing summon outside GetInView (VIEWGRID 33) is pulled next to the caster with Action 8/6.
            if (summons.TryGetValue(memberConnectionId, out var existingSummon) &&
                (Math.Abs(existingSummon.PositionX - summoner.PositionX) > LegacyViewGrid ||
                 Math.Abs(existingSummon.PositionY - summoner.PositionY) > LegacyViewGrid))
            {
                var pullX = (int)summoner.PositionX;
                var pullY = (int)summoner.PositionY;
                if (LegacyMobGridSearch.TryFindEmpty(
                        memberConnectionId,
                        ref pullX,
                        ref pullY,
                        GetParticipantAtPosition,
                        mapGrid is null ? static (_, _) => false : mapGrid.IsBlocked,
                        checkCandidateTerrain: mapCollisionMode == LegacyMapCollisionMode.CandidateAware))
                {
                    recalledSummons.Add(new LegacySummonMovement(memberConnectionId, existingSummon.PositionX, existingSummon.PositionY, (short)pullX, (short)pullY, Effect: 8, Speed: 6));
                    summons[memberConnectionId] = existingSummon with { PositionX = (short)pullX, PositionY = (short)pullY };
                }
            }

            if (existingSameType >= requestedCount)
                return LegacySummonGenerationResult.AlreadyPresent;
        }

        var bonus = summonCatalog.GetBonus(summonId);
        for (var index = existingSameType; index < requestedCount; index++)
        {
            if (GetLegacyPartyListLocked(leaderConnectionId).Count >= LegacyMaxParty)
                return LegacySummonGenerationResult.PartyFull;

            var connectionId = AllocateSummonConnectionId();
            var mob = template.MobSnapshot.ToArray();
            ApplySummonMobState(mob, template.SourceName, summonerState, bonus);
            if (!summoner.GuildDisabled)
                summoner.Mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset, sizeof(ushort)).CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset));
            ApplySummonCapeFromSummoner(mob, summoner.Mob);
            var affect = new byte[LegacyAccountSnapshot.AffectStride];
            affect[0] = 24;
            BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), summonId is >= 28 and <= 37 ? 2_000_000_000u : 20u);

            var positionX = (int)summoner.PositionX;
            var positionY = (int)summoner.PositionY;
            var foundPosition = LegacyMobGridSearch.TryFindEmpty(
                connectionId,
                ref positionX,
                ref positionY,
                GetParticipantAtPosition,
                mapGrid is null ? static (_, _) => false : mapGrid.IsBlocked,
                checkCandidateTerrain: mapCollisionMode == LegacyMapCollisionMode.CandidateAware);
            if (!foundPosition)
                return LegacySummonGenerationResult.PositionUnavailable;

            var summoned = new LegacySummonedMob(connectionId, leaderConnectionId, summonerConnectionId, (short)positionX, (short)positionY, mob, affect)
            {
                RouteType = 5,
            };
            summons.Add(connectionId, summoned);
            summonTargets[connectionId] = 0;
            summoner.SummonIds.Add(connectionId);
            createdSummons.Add(summoned);

            // SendAddParty reaches users only. The leader-self frame is gated by SummonNum, the count of
            // pre-existing same-type summons, which the creation loop never increments.
            if (participants.TryGetValue(leaderConnectionId, out var leaderParticipant))
            {
                if (existingSameType == 0)
                    partyAddNotices.Add(CreatePartyAddNotice(leaderConnectionId, leaderParticipant.Mob, leaderConnectionId, leaderConnectionId));
                partyAddNotices.Add(CreatePartyAddNotice(leaderConnectionId, mob, connectionId, 30000));
            }
            foreach (var memberConnectionId in GetLegacyPartyListLocked(leaderConnectionId))
            {
                if (memberConnectionId != connectionId && participants.ContainsKey(memberConnectionId))
                    partyAddNotices.Add(CreatePartyAddNotice(memberConnectionId, mob, connectionId, 30000));
            }
        }

        return LegacySummonGenerationResult.Created;
    }

    private const int LegacySummonFaceConflictSlots = 8;
    private const int LegacyViewGrid = 33;

    /// <summary>
    /// Approximates <c>pMob[Leader].PartyList</c>: party players other than the leader, then the
    /// summons attached to the leader. 7.69 keeps insertion slots with holes; the port has no holes.
    /// </summary>
    private List<int> GetLegacyPartyListLocked(int leaderConnectionId)
    {
        var members = GetPartyMemberIdsLocked(leaderConnectionId).Where(id => id != leaderConnectionId).ToList();
        members.AddRange(summons.Values.Where(summon => summon.LeaderConnectionId == leaderConnectionId).Select(static summon => summon.ConnectionId));
        return members;
    }

    /// <summary>7.69 GenerateSummon maps the summoner's cape (Equip[15]) to the summon's cape item.</summary>
    private static void ApplySummonCapeFromSummoner(byte[] summonMob, ReadOnlySpan<byte> summonerMob)
    {
        var capeOffset = LegacyAccountSnapshot.MobEquipmentOffset + (15 * LegacyItem.SizeInBytes);
        var summonerCape = BinaryPrimitives.ReadUInt16LittleEndian(summonerMob[capeOffset..]);
        short summonCape = summonerCape switch
        {
            543 or 545 => 734,
            544 or 546 => 735,
            548 or 549 => 550,
            _ => 0,
        };
        if (summonCape == 0)
            return;

        summonMob.AsSpan(capeOffset, LegacyItem.SizeInBytes).Clear();
        BinaryPrimitives.WriteInt16LittleEndian(summonMob.AsSpan(capeOffset), summonCape);
    }

    private int AllocateSummonConnectionId()
    {
        while (participants.ContainsKey(nextSummonConnectionId) || summons.ContainsKey(nextSummonConnectionId) || npcs.ContainsKey(nextSummonConnectionId))
            nextSummonConnectionId++;
        return nextSummonConnectionId++;
    }

    private int AllocateNpcConnectionId()
    {
        while (participants.ContainsKey(nextNpcConnectionId) || summons.ContainsKey(nextNpcConnectionId) || npcs.ContainsKey(nextNpcConnectionId))
            nextNpcConnectionId++;
        return nextNpcConnectionId++;
    }

    private static int GetLegacySummonCount(int instanceValue, int special3) => instanceValue switch
    {
        1 or 2 => Math.Max(0, special3 / 30),
        3 or 4 or 5 => Math.Max(0, special3 / 40),
        6 or 7 => Math.Max(0, special3 / 80),
        8 => 1,
        _ => 0,
    };

    private static ushort ReadEquipmentIndex(ReadOnlySpan<byte> mob, int equipmentSlot)
    {
        var offset = LegacyAccountSnapshot.MobEquipmentOffset + (equipmentSlot * LegacyItem.SizeInBytes);
        return BinaryPrimitives.ReadUInt16LittleEndian(mob[offset..]);
    }

    private static void ApplySummonMobState(byte[] mob, string sourceName, LegacyMobCombatState leaderState, LegacySummonBonus bonus)
    {
        var name = sourceName.Replace('_', ' ') + "^";
        mob.AsSpan(LegacyAccountSnapshot.MobNameOffset, 16).Clear();
        Encoding.ASCII.GetBytes(name[..Math.Min(name.Length, 16)]).CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset, 16));
        mob[LegacyAccountSnapshot.MobClanOffset] = 4;
        mob[LegacyAccountSnapshot.MobGuildLevelOffset] = 0;

        var baseScore = LegacyScore.Read(mob[LegacyAccountSnapshot.MobBaseScoreOffset..]);
        var cappedLevel = Math.Min(Math.Max(0, leaderState.BaseScore.Level), LegacySkillCombatMath.LegacyMaxLevel);
        baseScore = baseScore with
        {
            Level = cappedLevel,
            Damage = baseScore.Damage + (leaderState.CurrentScore.Intelligence * bonus.MinDamage / 100) + (leaderState.CurrentScore.Special3 * bonus.MaxDamage / 100),
            Ac = baseScore.Ac + (leaderState.CurrentScore.Intelligence * bonus.MinAc / 100) + (leaderState.CurrentScore.Special3 * bonus.MaxAc / 100),
            MaxHp = baseScore.MaxHp + (leaderState.CurrentScore.Intelligence * bonus.MinHp / 100) + (leaderState.CurrentScore.Special3 * bonus.MaxHp / 100),
        };
        baseScore.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));

        var currentScore = LegacyScore.Read(mob[LegacyAccountSnapshot.MobCurrentScoreOffset..]) with
        {
            Level = cappedLevel,
            MaxHp = baseScore.MaxHp,
            Hp = baseScore.MaxHp,
        };
        currentScore.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    }

    private static void RefundSkillMana(Participant participant, LegacySkillDefinition skill, LegacyMobCombatState stateAfterConsumption)
    {
        var spent = LegacySkillCombatMath.GetManaSpent(skill, stateAfterConsumption);
        var restoredMana = checked(stateAfterConsumption.CurrentMana + spent);
        BinaryPrimitives.WriteInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentMpOffset), restoredMana);
        participant.RequestedMana = restoredMana;
    }

    private LegacyCharacterLoginData ProjectPlayerScoreLocked(
        Participant target,
        byte[] mob,
        byte[] affect,
        LegacyItemDataTable itemData,
        bool preserveHpMp = false)
    {
        var currentScore = LegacyMobCombatState.Read(mob).CurrentScore;
        var hasPursuit = HasLegacyAffectType(affect, type: 3);
        var pursuitBaseResistances = target.PursuitBaseResistances?.ToArray();
        if (pursuitBaseResistances is null && hasPursuit)
        {
            pursuitBaseResistances = HasLegacyAffectType(target.Affect, type: 3)
                ? CapturePlayerPursuitBaseResistances(mob, target.Affect)
                : mob.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4).ToArray();
        }

        var projectionMob = mob.ToArray();
        if ((HasLegacyAffectType(affect, type: 4) || HasLegacyAffectType(target.Affect, type: 4)) &&
            target.SourceMobMagic is { } sourceMobMagic &&
            projectionMob.Length >= LegacyAccountSnapshot.MobMagicOffset + sizeof(int))
        {
            BinaryPrimitives.WriteInt32LittleEndian(
                projectionMob.AsSpan(LegacyAccountSnapshot.MobMagicOffset),
                sourceMobMagic);
        }
        if (pursuitBaseResistances is { Length: >= 4 })
            pursuitBaseResistances.AsSpan(0, 4).CopyTo(projectionMob.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4));
        var projected = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                projectionMob,
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                affect,
                target.MobExtra,
                target.Donate,
                target.PositionX,
                target.PositionY,
                target.ClientEquipment)
            {
                PursuitBaseResistances = pursuitBaseResistances,
            },
            itemData);
        if (preserveHpMp)
        {
            var projectedScore = LegacyMobCombatState.Read(projected.Mob).CurrentScore with
            {
                Hp = currentScore.Hp,
                Mp = currentScore.Mp,
            };
            projectedScore.Write(projected.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        }

        target.PursuitBaseResistances = hasPursuit ? pursuitBaseResistances : null;
        return projected;
    }

    private static byte[]? CapturePlayerPursuitBaseResistances(ReadOnlySpan<byte> mob, ReadOnlySpan<byte> affect)
    {
        if (mob.Length < LegacyAccountSnapshot.MobResistOffset + 4 ||
            mob.Length < LegacyAccountSnapshot.MobEquipmentOffset + LegacyItem.SizeInBytes)
            return null;

        var headItem = LegacyItem.Read(mob.Slice(
            LegacyAccountSnapshot.MobEquipmentOffset,
            LegacyItem.SizeInBytes));
        var reduction = CalculatePursuitResistanceReduction(affect, headItem.Index);
        var baseline = mob.Slice(LegacyAccountSnapshot.MobResistOffset, 4).ToArray();
        for (var index = 0; index < baseline.Length; index++)
            baseline[index] = checked((byte)Math.Clamp(baseline[index] + reduction, 0, 100));
        return baseline;
    }

    private static bool IsAuditedType1PlayerSkill(LegacySkillDefinition skill) =>
        skill.Id is 1 or 23 or 34 or 36 && skill.Aggressive != 0 && skill.AffectType == 1;

    private static bool IsAuditedType4PlayerSkill(LegacySkillDefinition skill) =>
        skill.Id == 96 && skill.Passive == 0 && skill.TargetType == 0 &&
        skill.InstanceType == 0 && skill.Aggressive == 0 &&
        skill.TickType == 0 && skill.AffectType == 4;

    private static bool IsAuditedType3PlayerSkill(LegacySkillDefinition skill) =>
        (skill.Id == 16 && skill.Aggressive != 0 && skill.AffectType == 3) ||
        IsAuditedResiDecreaseSkill(skill);

    private static bool IsAuditedResiDecreaseSkill(LegacySkillDefinition skill) =>
        skill.Id == 226 && skill.Passive == 0 && skill.TargetType == 1 &&
        skill.InstanceType == 0 && skill.Aggressive != 0 && skill.TickType == 3 &&
        skill.TickValue == 50 && skill.AffectType == 0 && skill.AffectTime == 1 &&
        skill.AffectResist == 1;

    private static bool IsAuditedType7PlayerSkill(LegacySkillDefinition skill) =>
        skill.Id == 19 && skill.Aggressive != 0 && skill.AffectType == 7;

    private static bool IsAuditedType10PlayerSkill(LegacySkillDefinition skill) =>
        skill.Id == 51 && skill.Aggressive != 0 && skill.AffectType == 10;

    private static bool IsAuditedType11PlayerSkill(LegacySkillDefinition skill) =>
        skill.Id == 43 && skill.InstanceType == 0 && skill.Aggressive == 0 && skill.AffectType == 11;

    private static bool IsAuditedType13PlayerSkill(LegacySkillDefinition skill) =>
        skill.Id == 11 && skill.InstanceType == 0 && skill.Aggressive == 0 && skill.AffectType == 13;

    private static bool IsAuditedType14PlayerSkill(LegacySkillDefinition skill) =>
        skill.Id == 3 && skill.InstanceType == 0 && skill.Aggressive == 0 && skill.AffectType == 14;

    private static bool IsAuditedType15PlayerSkill(LegacySkillDefinition skill) =>
        skill.Id == 45 && skill.InstanceType == 0 && skill.Aggressive == 0 && skill.AffectType == 15;

    private static bool IsAuditedType19PlayerSkill(LegacySkillDefinition skill) =>
        skill.Id == 76 && skill.InstanceType == 0 && skill.Aggressive == 0 && skill.AffectType == 19;

    private static bool IsAuditedType21PlayerSkill(LegacySkillDefinition skill) =>
        skill.Id == 77 && skill.InstanceType == 0 && skill.Aggressive == 0 && skill.AffectType == 21;

    private static bool IsAuditedType24SamaritanPlayerSkill(LegacySkillDefinition skill) =>
        skill.Id == 13 && skill.Passive == 0 && skill.TargetType == 0 &&
        skill.InstanceType == 0 && skill.Aggressive == 0 &&
        skill.TickType == 0 && skill.AffectType == 24;

    private static bool IsAuditedType25PlayerSkill(LegacySkillDefinition skill) =>
        skill.Id == 53 && skill.InstanceType == 0 && skill.Aggressive == 0 && skill.AffectType == 25;

    private static bool IsAuditedType31PlayerSkill(LegacySkillDefinition skill) =>
        skill.Id == 85 && skill.InstanceType == 0 && skill.Aggressive == 0 && skill.AffectType == 31;

    private static bool IsAuditedType38PlayerSkill(LegacySkillDefinition skill) =>
        skill.Id == 87 && skill.InstanceType == 0 && skill.Aggressive == 0 && skill.AffectType == 38;

    private static bool IsAuditedType42PlayerSkill(LegacySkillDefinition skill) =>
        skill.Id == 216 && skill.Passive == 0 && skill.TargetType == 0 &&
        skill.InstanceType == 0 && skill.Aggressive == 0 &&
        skill.TickType == 0 && skill.AffectType == 42;

    private static bool IsAuditedType27PlayerSkill(LegacySkillDefinition skill) =>
        skill.Id == 75 && skill.InstanceType == 0 && skill.Aggressive == 0 && skill.AffectType == 27;

    private static bool IsAuditedType28PlayerSkill(LegacySkillDefinition skill) =>
        skill.Id == 95 && skill.InstanceType == 0 && skill.Aggressive == 0 && skill.AffectType == 28;

    private static bool IsAuditedType26PlayerSkill(LegacySkillDefinition skill) =>
        skill.Id == 89 && skill.InstanceType == 0 && skill.Aggressive == 0 && skill.AffectType == 26;

    private static bool IsAuditedType36PlayerSkill(LegacySkillDefinition skill) =>
        skill.Id == 92 && skill.InstanceType == 0 && skill.Aggressive == 0 && skill.AffectType == 36;

    private static bool IsAuditedType29PlayerSkill(LegacySkillDefinition skill) =>
        skill.Id == 102 && skill.Passive == 0 && skill.TargetType == 0 &&
        skill.InstanceType == 0 && skill.Aggressive == 0 &&
        skill.TickType == 0 && skill.AffectType == 29;

    private static byte GetAuditedPlayerScoreAffectType(LegacySkillDefinition skill) =>
        IsAuditedType4PlayerSkill(skill) ? (byte)4 :
        IsAuditedType1PlayerSkill(skill) ? (byte)1 :
        IsAuditedType3PlayerSkill(skill) ? (byte)3 :
        IsAuditedType7PlayerSkill(skill) ? (byte)7 :
        IsAuditedType10PlayerSkill(skill) ? (byte)10 :
        IsAuditedType11PlayerSkill(skill) ? (byte)11 :
        IsAuditedType13PlayerSkill(skill) ? (byte)13 :
        IsAuditedType14PlayerSkill(skill) ? (byte)14 :
        IsAuditedType15PlayerSkill(skill) ? (byte)15 :
        IsAuditedType19PlayerSkill(skill) ? (byte)19 :
        IsAuditedType21PlayerSkill(skill) ? (byte)21 :
        IsAuditedType24SamaritanPlayerSkill(skill) ? (byte)24 :
        IsAuditedType25PlayerSkill(skill) ? (byte)25 :
        IsAuditedType31PlayerSkill(skill) ? (byte)31 :
        IsAuditedType38PlayerSkill(skill) ? (byte)38 :
        IsAuditedType42PlayerSkill(skill) ? (byte)42 :
        IsAuditedType27PlayerSkill(skill) ? (byte)27 :
        IsAuditedType28PlayerSkill(skill) ? (byte)28 :
        IsAuditedType26PlayerSkill(skill) ? (byte)26 :
        IsAuditedType36PlayerSkill(skill) ? (byte)36 :
        IsAuditedType29PlayerSkill(skill) ? (byte)29 :
        (byte)0;

    private static bool CanApplyLegacyAffect(Participant attacker, Participant target, LegacyMobCombatState attackerState, LegacyMobCombatState targetState, LegacySkillDefinition skill, int affectRandomFactor)
    {
        if (skill.AffectType <= 0 && skill.TickType <= 0) return false;
        if (skill.Aggressive == 0) return true;
        if (attacker.GuildId != 0 && attacker.GuildId == target.GuildId) return false;
        if ((targetState.Rsv & 0x80) != 0 || target.Clan == 6) return false;

        if (skill.AffectResist is >= 1 and <= 4)
        {
            var targetLevel = target.ClassMaster is LegacyAccountSnapshot.ClassMasterMortal or LegacyAccountSnapshot.ClassMasterArch
                ? targetState.CurrentScore.Level
                : targetState.CurrentScore.Level + LegacySkillCombatMath.LegacyMaxLevel;
            var attackerLevel = attacker.ClassMaster is LegacyAccountSnapshot.ClassMasterMortal or LegacyAccountSnapshot.ClassMasterArch
                ? attackerState.CurrentScore.Level
                : attackerState.CurrentScore.Level + LegacySkillCombatMath.LegacyMaxLevel;
            var levelDifference = (attackerLevel - targetLevel) / 2;
            var resistanceLimit = targetState.RegenMp + skill.AffectResist + levelDifference;
            var random = affectRandomFactor >= 0 ? affectRandomFactor : System.Security.Cryptography.RandomNumberGenerator.GetInt32(100);
            if (random > resistanceLimit) return false;
        }

        return (targetState.Rsv & 0x80) == 0;
    }

    private bool IsLegacyPkProtectedMap(Participant attacker) =>
        attacker.HasMapAttribute && (attacker.MapAttribute & 0x40) != 0 &&
        !combatWorldState.RvrWarActive && combatWorldState.RvrState == 0 && combatWorldState.CastleState == 0 &&
        combatWorldState.GuildTowerState == 0 && !combatWorldState.NewbieEventServer;

    private static int ReadLegacyPkPoint(ReadOnlySpan<byte> mob)
    {
        var offset = LegacyAccountSnapshot.MobCarryOffset + (LegacyAccountSnapshot.LegacyKillMarkCarrySlot * LegacyItem.SizeInBytes);
        return mob.Length >= offset + LegacyItem.SizeInBytes ? mob[offset + 2] : 0;
    }

    private static int ReadLegacyGuilty(byte[] mob)
    {
        var offset = LegacyAccountSnapshot.MobCarryOffset + (LegacyAccountSnapshot.LegacyKillMarkCarrySlot * LegacyItem.SizeInBytes);
        if (mob.Length < offset + LegacyItem.SizeInBytes)
            return 0;
        var guilty = mob[offset + 4];
        if (guilty > 50)
        {
            mob[offset + 4] = 0;
            return 0;
        }
        return guilty;
    }

    private static bool SetLegacyGuilty(Participant participant, int value)
    {
        var offset = LegacyAccountSnapshot.MobCarryOffset + (LegacyAccountSnapshot.LegacyKillMarkCarrySlot * LegacyItem.SizeInBytes);
        if (participant.Mob.Length < offset + LegacyItem.SizeInBytes)
            return false;
        var current = ReadLegacyGuilty(participant.Mob);
        var next = Math.Clamp(value, 0, 50);
        participant.Mob[offset + 4] = (byte)next;
        participant.Guilty = next > 0;
        return current == 0 && next > 0;
    }

    private static bool TryApplyLegacyAffects(byte[] affect, LegacySkillDefinition skill, int level) =>
        TryApplyLegacyAffects(affect, skill, 100 + level, level);

    private static bool HasLegacyAffectType(ReadOnlySpan<byte> affect, byte type)
    {
        const int affectRecordSize = 8;
        for (var offset = 0; offset + affectRecordSize <= affect.Length; offset += affectRecordSize)
            if (affect[offset] == type)
                return true;

        return false;
    }

    private static bool TryCancelFirstLegacyAffect(byte[] affect, byte[] mob, byte affectType)
    {
        const int affectRecordSize = 8;
        for (var offset = 0; offset + affectRecordSize <= affect.Length; offset += affectRecordSize)
        {
            if (affect[offset] != affectType)
                continue;

            affect.AsSpan(offset, affectRecordSize).Clear();
            var rsv = BinaryPrimitives.ReadUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobRsvOffset));
            var hasType19 = HasLegacyAffectType(affect, type: 19);
            rsv = hasType19
                ? (ushort)(rsv | LegacyCurrentScoreMath.BlockRsvFlag)
                : (ushort)(rsv & ~LegacyCurrentScoreMath.BlockRsvFlag);
            BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), rsv);
            return true;
        }

        return false;
    }

    private static bool TryApplyLegacyAffects(byte[] affect, LegacySkillDefinition? skill, int delay, int level)
    {
        if (skill is null) return false;
        var changed = TrySetLegacyAffect(affect, skill.AffectType, skill.AffectValue, skill.AffectTime, delay, level);
        changed |= TrySetLegacyAffect(affect, skill.TickType, skill.TickValue, skill.AffectTime, delay, level, tick: true);
        return changed;
    }

    private static bool TryApplyLegacySecondaryAttackEffect(
        byte[] affect,
        LegacySkillDefinition? skill,
        int delay,
        int level,
        int targetConnectionId,
        byte[] targetMob,
        LegacyMobCombatState targetState,
        bool tick)
    {
        if (skill is null)
            return false;

        var type = tick ? skill.TickType : skill.AffectType;
        if (type <= 0 || (skill.Aggressive != 0 && (targetState.Rsv & 0x80) != 0))
            return false;

        var isMerchant = targetMob[LegacyAccountSnapshot.MobMerchantOffset] == 1;
        if (tick
                ? isMerchant && targetConnectionId > LegacyMobAiPlanner.MaximumPlayerId
                : isMerchant || targetConnectionId > LegacyMobAiPlanner.MaximumPlayerId)
            return false;

        var hadAffect = HasLegacyAffectType(affect, checked((byte)type));
        if (!TrySetLegacyAffect(
            affect,
            type,
            tick ? skill.TickValue : skill.AffectValue,
            skill.AffectTime,
            delay,
            level,
            tick))
            return false;

        if (!tick && hadAffect && type is 1 or 3 or 10)
        {
            for (var offset = 0; offset + 8 <= affect.Length; offset += 8)
            {
                if (affect[offset] != type)
                    continue;

                BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(offset + 4), 4);
                break;
            }
        }

        return true;
    }

    private bool TryApplyLegacyRsvSecondaryAttackAffects(
        byte[] targetAffect,
        byte[] targetMob,
        LegacyMobCombatState attackerState,
        LegacyMobCombatState targetState,
        int targetConnectionId,
        int frostRoll,
        int drainRoll)
    {
        const ushort frostRsvFlag = 0x0001;
        const ushort drainRsvFlag = 0x0002;
        var effectLevel = attackerState.CurrentScore.Special2;
        var effectDelay = effectLevel + 150;
        var changed = false;

        if ((attackerState.Rsv & frostRsvFlag) != 0 &&
            (frostRoll >= 0 ? frostRoll : System.Security.Cryptography.RandomNumberGenerator.GetInt32(2)) == 0)
        {
            var frostSkill = skillData?[36];
            changed |= TryApplyLegacySecondaryAttackEffect(
                targetAffect, frostSkill, effectDelay, effectLevel, targetConnectionId, targetMob, targetState, tick: false);
            changed |= TryApplyLegacySecondaryAttackEffect(
                targetAffect, frostSkill, effectDelay, effectLevel, targetConnectionId, targetMob, targetState, tick: true);
        }

        if ((attackerState.Rsv & drainRsvFlag) != 0 &&
            (drainRoll >= 0 ? drainRoll : System.Security.Cryptography.RandomNumberGenerator.GetInt32(2)) == 0)
        {
            var drainSkill = skillData?[40];
            changed |= TryApplyLegacySecondaryAttackEffect(
                targetAffect, drainSkill, effectDelay, effectLevel, targetConnectionId, targetMob, targetState, tick: false);
            changed |= TryApplyLegacySecondaryAttackEffect(
                targetAffect, drainSkill, effectDelay, effectLevel, targetConnectionId, targetMob, targetState, tick: true);
        }

        return changed;
    }

    private static void ApplyNpcWeakenAffectDamageDelta(
        byte[] mob,
        ReadOnlySpan<byte> previousAffect,
        ReadOnlySpan<byte> nextAffect)
    {
        var previousReduction = LegacyCurrentScoreMath.CalculateWeakenAffectStage(previousAffect).DamageReduction;
        var nextReduction = LegacyCurrentScoreMath.CalculateWeakenAffectStage(nextAffect).DamageReduction;
        if (previousReduction == nextReduction)
            return;

        var currentScore = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        currentScore = currentScore with
        {
            Damage = unchecked(currentScore.Damage + previousReduction - nextReduction),
        };
        currentScore.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    }

    private static byte[]? ApplyNpcPursuitAffectResistanceTransition(
        byte[] mob,
        ReadOnlySpan<byte> previousAffect,
        ReadOnlySpan<byte> nextAffect,
        byte[]? baseResistances)
    {
        const byte pursuitAffectType = 3;
        var nextIsActive = HasLegacyAffectType(nextAffect, pursuitAffectType);
        if (baseResistances is null &&
            !HasLegacyAffectType(previousAffect, pursuitAffectType) &&
            nextIsActive)
            baseResistances = mob.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4).ToArray();

        if (baseResistances is null || baseResistances.Length < 4)
            return null;

        var headItem = LegacyItem.Read(mob.AsSpan(
            LegacyAccountSnapshot.MobEquipmentOffset,
            LegacyItem.SizeInBytes));
        var reduction = nextIsActive
            ? CalculatePursuitResistanceReduction(nextAffect, headItem.Index)
            : 0;

        for (var index = 0; index < 4; index++)
        {
            var offset = LegacyAccountSnapshot.MobResistOffset + index;
            var resistance = baseResistances[index] - reduction;
            mob[offset] = checked((byte)Math.Clamp(resistance, 0, 100));
        }

        return nextIsActive ? baseResistances : null;
    }

    private static int CalculatePursuitResistanceReduction(ReadOnlySpan<byte> affect, int headItemIndex)
    {
        var reduction = 0;
        for (var offset = 0; offset + 8 <= affect.Length; offset += 8)
        {
            if (affect[offset] != 3)
                continue;

            var value = affect[offset + 1];
            reduction = unchecked(reduction + (headItemIndex < 50 ? value / 2 : value - 10));
        }

        return reduction;
    }

    private static bool TrySetLegacyAffect(byte[] affect, int type, int value, int affectTime, int delay, int level, bool tick = false)
    {
        if (type <= 0) return false;
        var slot = -1;
        for (var index = 0; index < LegacyAccountSnapshot.AffectStride / 8; index++)
        {
            if (affect[index * 8] == type)
            {
                slot = index;
                break;
            }
        }

        if (slot < 0)
        {
            for (var index = 0; index < LegacyAccountSnapshot.AffectStride / 8; index++)
            {
                if (affect[index * 8] == 0)
                {
                    slot = index;
                    break;
                }
            }
        }

        if (slot < 0) return false;
        var offset = slot * 8;
        var time = checked((long)delay * (affectTime + 1) / 100);
        if (tick && time >= 3 && type is 1 or 3 or 10)
            time = 2;
        time = Math.Min(time, tick ? 500_000_000L : 2_139_062_143L);
        if (!tick && type == 7 && affect[offset] == type)
        {
            time = Math.Max(time, BinaryPrimitives.ReadUInt32LittleEndian(affect.AsSpan(offset + 4)));
            level = Math.Max(level, BinaryPrimitives.ReadUInt16LittleEndian(affect.AsSpan(offset + 2)));
        }
        affect[offset] = checked((byte)type);
        affect[offset + 1] = checked((byte)value);
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(offset + 2), checked((ushort)Math.Clamp(level, 0, ushort.MaxValue)));
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(offset + 4), checked((uint)time));
        return true;
    }

    private static void ResetLegacyAffectDuration(byte[] affect, byte type, uint time)
    {
        for (var offset = 0; offset + 8 <= affect.Length; offset += 8)
        {
            if (affect[offset] != type)
                continue;

            BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(offset + 4), time);
            return;
        }
    }

    public GuildInviteCheckResult TryPrepareGuildInvite(int sourceConnectionId, int targetConnectionId, int inviteType, out GuildInvitePlan? plan)
    {
        plan = null;
        if (sourceConnectionId <= 0 || sourceConnectionId >= 1000 || targetConnectionId <= 0 || targetConnectionId >= 1000 || sourceConnectionId == targetConnectionId)
            return GuildInviteCheckResult.InvalidTarget;
        if (inviteType is < 0 or >= 4)
            return GuildInviteCheckResult.InvalidInviteType;
        if (DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
            return GuildInviteCheckResult.Sunday;

        lock (gate)
        {
            if (!participants.TryGetValue(sourceConnectionId, out var source) || !participants.TryGetValue(targetConnectionId, out var target))
                return GuildInviteCheckResult.ParticipantNotFound;
            if (source.GuildId == 0)
                return GuildInviteCheckResult.SourceHasNoGuild;
            if (target.GuildId != 0)
                return GuildInviteCheckResult.TargetAlreadyHasGuild;
            if (source.Clan != target.Clan)
                return GuildInviteCheckResult.ClanMismatch;
            if (source.GuildLevel == 0)
                return GuildInviteCheckResult.SourceNotGuildMember;
            if (inviteType != 0 && source.GuildLevel != 9)
                return GuildInviteCheckResult.LeaderRequired;

            var cost = inviteType == 0 ? 4_000_000 : 100_000_000;
            if (source.Coin < cost)
                return GuildInviteCheckResult.InsufficientCoin;

            plan = new GuildInvitePlan(sourceConnectionId, targetConnectionId, source.AccountName, source.CharacterSlot, target.AccountName, target.CharacterSlot, source.GuildId, source.Coin, target.Coin, cost);
            return GuildInviteCheckResult.Accepted;
        }
    }

    public bool ApplyGuildInvite(GuildInvitePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        lock (gate)
        {
            if (!participants.TryGetValue(plan.SourceConnectionId, out var source) || !participants.TryGetValue(plan.TargetConnectionId, out var target)) return false;
            if (!string.Equals(source.AccountName, plan.SourceAccountName, StringComparison.Ordinal) || source.CharacterSlot != plan.SourceCharacterSlot ||
                !string.Equals(target.AccountName, plan.TargetAccountName, StringComparison.Ordinal) || target.CharacterSlot != plan.TargetCharacterSlot)
                return false;

            var committedSourceCoin = checked(plan.SourceCoin - plan.Cost);
            var alreadyApplied = source.GuildId == plan.GuildId && source.Coin == committedSourceCoin &&
                target.GuildId == plan.GuildId && target.GuildLevel == 0 && target.Coin == plan.TargetCoin;
            if (alreadyApplied)
                return true;
            if (source.GuildId != plan.GuildId || source.Coin != plan.SourceCoin ||
                target.GuildId != 0 || target.Coin != plan.TargetCoin)
                return false;

            source.Coin -= plan.Cost;
            target.GuildId = plan.GuildId;
            target.GuildLevel = 0;
            if (source.Mob.Length >= WorldHubLegacyOffsets.LegacyAccountMobSize)
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(source.Mob.AsSpan(WorldHubLegacyOffsets.LegacyAccountMobCoinOffset), source.Coin);
            if (target.Mob.Length >= WorldHubLegacyOffsets.LegacyAccountMobSize)
                System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(target.Mob.AsSpan(WorldHubLegacyOffsets.LegacyAccountMobGuildOffset), checked((ushort)plan.GuildId));
            return true;
        }
    }

    public bool TryGetParticipantPkInfoState(int connectionId, out int state)
    {
        lock (gate)
        {
            state = 0;
            if (!participants.TryGetValue(connectionId, out var participant))
                return false;

            var guilty = combatWorldState.NewbieEventServer ? 0 : ReadLegacyGuilty(participant.Mob);
            if (!combatWorldState.NewbieEventServer)
                participant.Guilty = guilty != 0;
            state = LegacyTradePkRules.Resolve(
                guilty,
                participant.PkMode,
                participant.PositionX,
                participant.PositionY,
                combatWorldState);
            return true;
        }
    }

    public bool TryBuildCreateMobFrame(int connectionId, LegacyFrameCodec codec, uint clientTick, byte keywordIndex, out byte[]? frame)
    {
        ArgumentNullException.ThrowIfNull(codec);
        lock (gate)
        {
            frame = null;
            if (!participants.TryGetValue(connectionId, out var participant) || participant.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize) return false;
            SyncClientEquipmentFromLegacyMob(participant);
            var sourcePayload = new CreateMobConfirmation(
                (ushort)connectionId,
                participant.PositionX,
                participant.PositionY,
                participant.Mob,
                participant.Affect,
                npc: false)
                .ToPayload();
            var appearance = LegacyAutoTradeVisualRelay.BuildEquipmentAppearance(participant.Mob, participant.ClientEquipment);
            var createMobFrame = W2ppCreateMobV1Adapter.AdaptPayload(sourcePayload, appearance.VisualEquipment, appearance.AncientCodes)
                .ToFrame(codec, clientTick, keywordIndex);
            frame = LegacyBattleRoyaleCreateMobVisualRelay.Apply(createMobFrame, combatWorldState.BattleRoyaleState, codec);
            return true;
        }
    }

    public bool TryBuildUpdateEtcFrame(int connectionId, LegacyFrameCodec codec, uint clientTick, byte keywordIndex, out byte[]? frame)
    {
        ArgumentNullException.ThrowIfNull(codec);
        lock (gate)
        {
            frame = null;
            if (!participants.TryGetValue(connectionId, out var participant) || participant.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize) return false;
            var sourcePayload = new UpdateEtcConfirmation(participant.Mob, participant.MobExtra).ToPayload();
            frame = W2ppUpdateEtcV1Adapter.AdaptPayload(sourcePayload.AsSpan(0, W2ppUpdateEtcV1Adapter.SourcePayloadSize))
                .ToFrame(codec, clientTick, keywordIndex, (ushort)connectionId);
            return true;
        }
    }

    public bool TryBuildUpdateEquipFrame(int connectionId, LegacyFrameCodec codec, uint clientTick, byte keywordIndex, out byte[]? frame)
    {
        ArgumentNullException.ThrowIfNull(codec);
        lock (gate)
        {
            frame = null;
            if (!participants.TryGetValue(connectionId, out var participant) ||
                participant.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                return false;

            try
            {
                SyncClientEquipmentFromLegacyMob(participant);
                frame = LegacyAutoTradeVisualRelay.BuildEquipmentAppearance(participant.Mob, participant.ClientEquipment)
                    .ToFrame(codec, clientTick, keywordIndex, checked((ushort)connectionId));
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }
        }
    }

    public async ValueTask<bool> SendAsync(int connectionId, ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        Participant? participant;
        lock (gate) participants.TryGetValue(connectionId, out participant);
        if (participant is null) return false;
        await participant.SendAsync(frame, cancellationToken);
        return true;
    }

    /// <summary>Sends a frame to an already-captured participant view without holding the WorldHub lock during I/O.</summary>
    public async ValueTask BroadcastToParticipantsAsync(
        IReadOnlyList<int> recipientConnectionIds,
        ReadOnlyMemory<byte> frame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipientConnectionIds);
        Participant[] recipients;
        lock (gate)
        {
            recipients = recipientConnectionIds
                .Distinct()
                .Select(connectionId => participants.TryGetValue(connectionId, out var participant) ? participant : null)
                .Where(static participant => participant is not null)
                .Cast<Participant>()
                .ToArray();
        }

        foreach (var participant in recipients)
            await participant.SendAsync(frame, cancellationToken);
    }

    public async ValueTask BroadcastAsync(int senderConnectionId, ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        Participant[] recipients;
        lock (gate) recipients = participants.Values.Where(p => p.ConnectionId != senderConnectionId).ToArray();

        foreach (var participant in recipients)
            await participant.SendAsync(frame, cancellationToken);
    }

    private sealed class Participant(int connectionId, string accountName, Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> send, ReadOnlyMemory<byte> spawnFrame)
    {
        private readonly SemaphoreSlim sendLock = new(1, 1);
        public int ConnectionId { get; } = connectionId;
        public string AccountName { get; } = accountName;
        public ReadOnlyMemory<byte> SpawnFrame { get; } = spawnFrame;
        public int CharacterSlot { get; set; } = -1;
        public int GuildId { get; set; }
        public bool GuildDisabled { get; set; }
        public int Clan { get; set; }
        public int GuildLevel { get; set; }
        public int Coin { get; set; }
        public int Donate { get; set; }
        public short PositionX { get; set; }
        public short PositionY { get; set; }
        public bool PkMode { get; set; }
        public int CurrentTarget { get; set; }
        public bool Guilty { get; set; }
        public byte MapAttribute { get; set; }
        public bool HasMapAttribute { get; set; }
        public byte[] Mob { get; set; } = [];
        public int? SourceMobMagic { get; set; }
        public LegacyItem[] ClientEquipment { get; set; } = new LegacyItem[CharacterMobV769.EquipmentCount];
        public bool HasPersistedClientEquipment { get; set; }
        public int RuntimeParryAdjustment { get; set; }
        public short ClassMaster { get; set; } = LegacyAccountSnapshot.ClassMasterMortal;
        public int ExperienceSegment { get; set; }
        public ushort CriticalProgress { get; set; }
        public byte[] Affect { get; set; } = new byte[LegacyAccountSnapshot.AffectStride];
        public byte[]? PursuitBaseResistances { get; set; }
        public byte[] MobExtra { get; set; } = new byte[LegacyAccountSnapshot.MobExtraStride];
        public LegacyRecycleBinEntry[] RecycleBin { get; set; } = new LegacyRecycleBinEntry[LegacyRecycleBinRules.Capacity];
        public int RequestedMana { get; set; }
        public int RequestedHp { get; set; }
        public long LastPotionAt { get; set; } = long.MinValue;
        public List<int> SummonIds { get; } = [];

        public async ValueTask SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
        {
            await sendLock.WaitAsync(cancellationToken);
            try { await send(frame, cancellationToken); }
            finally { sendLock.Release(); }
        }
    }
}

public enum LegacyDonatePurchaseResult
{
    Accepted,
    ParticipantNotFound,
    CatalogUnavailable,
    InvalidCoordinates,
    InvalidQuantity,
    InvalidItem,
    InvalidPrice,
    InsufficientDonate,
    InventoryUnavailable,
    InventoryFull,
}

public sealed record LegacyDonatePurchaseOutcome(
    int Store,
    int Page,
    int ItemPosition,
    int ItemIndex,
    int Quantity,
    int TotalPrice,
    int RemainingDonate,
    IReadOnlyList<LegacyItemDrop> ItemDrops,
    byte[] MobSnapshot,
    int PreviousDonate,
    byte[] PreviousMob);

public enum LegacyPhysicalAttackResult
{
    Accepted,
    ParticipantNotFound,
    SameParticipant,
    CombatStateUnavailable,
    PeacefulZoneBlocked,
    FriendlyFireBlocked,
    AttackerNotAlive,
    TargetNotAlive,
    TargetMerchantBlocked,
    TargetOutOfView,
    OutOfRange,
    NpcHeadAreaBlocked,
    TowerAttackBlocked,
    ShortPacketSecondaryTargetBlocked,
}

public sealed record LegacyPhysicalAttackOutcome(
    int TargetConnectionId,
    int Damage,
    int RemainingHp,
    byte[] TargetMobSnapshot,
    bool TargetRemoved = false,
    bool TargetDied = false,
    bool TargetRevived = false,
    int ConsumedItemSlot = -1,
    LegacyItem? ConsumedItem = null,
    long ExperienceAwarded = 0,
    byte[]? AttackerMobSnapshot = null,
    IReadOnlyList<LegacyExperienceAward>? ExperienceAwards = null,
    IReadOnlyList<LegacyItemDrop>? ItemDrops = null,
    bool TargetHpChanged = false,
    bool TargetResourceChanged = false,
    int AttackerHpAbsorbed = 0,
    int AttackerRequestedHp = 0,
    int ForceDamage = 0,
    int PvpDamage = 0,
    int MountOwnerConnectionId = 0,
    LegacyItem? UpdatedMountItem = null,
    bool PkPointGateBlocked = false,
    IReadOnlyList<LegacyCrimeStateUpdate>? CrimeStateUpdates = null,
    byte[]? TargetAffectSnapshot = null,
    int ReflectDamage = 0,
    int ReflectPvp = 0,
    int MountDurabilityLoss = 0,
    LegacyPistaTransition? PistaTransition = null,
    IReadOnlyList<LegacyGlobalNotice>? GlobalNotices = null,
    IReadOnlyList<LegacyAreaNotice>? AreaNotices = null,
    IReadOnlyList<LegacyCoinUpdate>? CoinUpdates = null,
    IReadOnlyList<LegacyPrivateNotice>? PrivateNotices = null,
    byte DoubleCritical = 0,
    int SecondaryDamage = 0,
    ushort NextProgress = 0,
    bool ClearTargetIdOnWire = false,
    int? ZardMobLeftCount = null,
    IReadOnlyList<LegacyAttackViewRemovalEffect>? ViewRemovalEffects = null)
{
    public IReadOnlyList<int> TargetAffectViewRecipients { get; init; } = [];
    public int AttackerHpHealed { get; init; }
    public bool AttackerHpHealProcTriggered { get; init; }
    public IReadOnlyList<int> AttackerHpViewRecipients { get; init; } = [];
}

public sealed record LegacyCrimeStateUpdate(int ConnectionId, short PositionX, short PositionY, byte[] MobSnapshot);

public enum LegacySkillAttackResult
{
    Accepted,
    ParticipantNotFound,
    SameParticipant,
    CombatStateUnavailable,
    PeacefulZoneBlocked,
    FriendlyFireBlocked,
    AttackerNotAlive,
    TargetClanBlocked,
    TargetNotAlive,
    TargetOutOfView,
    OutOfRange,
    SummonNotAllowedHere,
    TargetTooHighToSummon,
    SummonInvalidPosition,
    SummonZoneBlocked,
    UnsupportedSkillType,
    TargetMerchantBlocked,
    TargetLimitExceeded,
    PartySkillBlocked,
    NpcHeadAreaBlocked,
    TowerAttackBlocked,
    PartyFullCantSummon,
}

public sealed record LegacyCombatWorldState(
    bool RvrWarActive = false,
    int RvrState = 0,
    int CastleState = 0,
    int GuildTowerState = 0,
    bool NewbieEventServer = false,
    int DoubleModeMultiplier = 1,
    bool KefraLive = true,
    int Kingdom1Clear = 0,
    int Kingdom2Clear = 0,
    int CastleQuestClear = 0,
    int BattleRoyaleState = 0,
    int BattleRoyaleItem = 0,
    IReadOnlyDictionary<int, int>? GuildWarPartners = null,
    int BattleRoyaleGrid = 0,
    IReadOnlyDictionary<int, int>? GuildAllies = null);

public enum LegacyMapCollisionMode
{
    LegacyCompatible,
    CandidateAware,
}

public sealed record LegacySkillAttackOutcome(
    int TargetConnectionId,
    int BaseDamage,
    int Damage,
    int Resistance,
    int RemainingHp,
    byte[] TargetMobSnapshot,
    byte[]? TargetAffectSnapshot = null,
    bool TargetResourceChanged = false,
    bool UpdatesAttackerState = false,
    bool HasRecallPosition = false,
    short RecallPositionX = 0,
    short RecallPositionY = 0,
    bool HasTargetPosition = false,
    short TargetPositionX = 0,
    short TargetPositionY = 0,
    IReadOnlyList<LegacySummonedMob>? SummonedMobs = null,
    LegacySummonGenerationResult SummonResult = LegacySummonGenerationResult.Created,
    bool AttackerResourceChanged = false,
    bool TargetRevived = false,
    int ConsumedItemSlot = -1,
    LegacyItem? ConsumedItem = null,
    bool TargetRemoved = false,
    long ExperienceAwarded = 0,
    byte[]? AttackerMobSnapshot = null,
    IReadOnlyList<LegacyExperienceAward>? ExperienceAwards = null,
    IReadOnlyList<LegacyItemDrop>? ItemDrops = null,
    bool TargetHpChanged = false,
    LegacyPistaTransition? PistaTransition = null,
    IReadOnlyList<LegacyGlobalNotice>? GlobalNotices = null,
    IReadOnlyList<LegacyAreaNotice>? AreaNotices = null,
    IReadOnlyList<LegacyCoinUpdate>? CoinUpdates = null,
    IReadOnlyList<LegacyPrivateNotice>? PrivateNotices = null,
    IReadOnlyList<LegacyNpcCombatStateUpdate>? NpcCombatStateUpdates = null,
    int? HealingExperienceContribution = null,
    bool StopRemainingTargets = false,
    LegacySkillActionRelay? ActionRelay = null,
    bool TargetEquipmentChanged = false,
    bool ClearTargetIdOnWire = false,
    IReadOnlyList<LegacyCrimeStateUpdate>? CrimeStateUpdates = null,
    int MountOwnerConnectionId = 0,
    LegacyItem? UpdatedMountItem = null,
    int? ZardMobLeftCount = null,
    bool PreserveSkillMarkerOnWire = false,
    LegacySummonedMob? CapturedSummon = null,
    IReadOnlyList<int>? CaptureViewRecipients = null,
    IReadOnlyList<LegacyPartyAddNotice>? PartyAddNotices = null,
    IReadOnlyList<LegacySummonMovement>? RecalledSummons = null,
    IReadOnlyList<LegacyAttackViewRemovalEffect>? ViewRemovalEffects = null);

/// <summary>One 7.69 <c>SendAddParty</c> frame: recipient user plus the member snapshot it describes.</summary>
public sealed record LegacyPartyAddNotice(
    int RecipientConnectionId,
    int LeaderField,
    int Level,
    int MaxHp,
    int Hp,
    int MemberConnectionId,
    string MobName);

public sealed record LegacySkillActionRelay(
    short PositionX,
    short PositionY,
    int Effect,
    int Speed,
    short TargetX,
    short TargetY,
    int ActorConnectionId = 0);

public sealed record LegacyNpcCombatStateUpdate(
    int ConnectionId,
    int Mode,
    int CurrentTarget,
    IReadOnlyList<int> EnemyList);

public sealed record LegacyExperienceAward(int ConnectionId, int Experience, byte[] MobSnapshot);

public sealed record LegacyItemDrop(int ConnectionId, int InventorySlot, LegacyItem Item);

public sealed record LegacyWorldItem(int ItemId, short PositionX, short PositionY, LegacyItem Item, int Rotate = 0, int Delay = 90)
{
    public const int WireIdOffset = 10_000;
    public const int CarryDestinationType = 1;
    public const int MaxGridCoordinate = 4096;
    public static readonly IReadOnlySet<short> ProtectedDropItemIndexes = new HashSet<short> { 446, 508, 509, 522, 526, 527, 528, 529, 530, 531, 532, 533, 534, 535, 536, 537, 747, 3993, 3994 };
}

public sealed record LegacyGroundItemSpawn(
    LegacyWorldItem Item,
    int WireItemId,
    IReadOnlyList<int> RecipientConnectionIds);

public sealed record LegacyGroundItemDecay(
    LegacyWorldItem Item,
    IReadOnlyList<int> RecipientConnectionIds);

public sealed record LegacyGroundItemDecayPlan(
    DateTime SecondSlot,
    IReadOnlyList<LegacyGroundItemDecay> RemovedItems);

public sealed record LegacyUpdateItemOutcome(
    int ConnectionId,
    int WireItemId,
    LegacyMapItemState MapItem,
    bool StateChanged,
    int ConsumedKeySlot,
    LegacyItem ConsumedKey,
    byte[] MobSnapshot,
    bool SuppressMissingKeyNotice,
    LegacyCastleQuestStart? CastleQuestStart = null);

public sealed record LegacyMapItemMinutePlan(
    DateTime MinuteSlot,
    IReadOnlyList<LegacyMapItemState> ClosedItems);

public sealed record LegacyCastleQuestState(
    int QuestLevel,
    int TimeRemaining,
    int LeaderConnectionId,
    IReadOnlyList<int> PartyConnectionIds);

public sealed record LegacyCastleQuestStart(
    int QuestLevel,
    int TimeRemaining,
    int LeaderConnectionId,
    IReadOnlyList<int> PartyConnectionIds,
    IReadOnlyList<LegacyWorldNpc> RemovedNpcs,
    IReadOnlyList<LegacyWorldNpc> SpawnedNpcs);

public enum LegacyUpdateItemResult
{
    Accepted,
    ParticipantNotFound,
    CombatStateUnavailable,
    AttackerNotAlive,
    InvalidState,
    InvalidItemId,
    MapItemNotFound,
    MissingKey,
    MissingCastleQuestKey,
    InvalidCastleQuest,
    CastleQuestActive,
}

public enum LegacyBaseScoreBonusResult
{
    Applied,
    NoPoints,
    ParticipantNotFound,
    CharacterStateUnavailable,
    NotAlive,
    InvalidDetail,
    ItemDataUnavailable,
}

public sealed record LegacyBaseScoreBonusOutcome(
    byte[] Mob,
    byte[] Affect,
    byte[] MobExtra,
    bool SendEtc,
    bool SendScore);

public enum LegacySpecialScoreBonusResult
{
    Applied,
    NoPoints,
    ParticipantNotFound,
    CharacterStateUnavailable,
    NotAlive,
    InvalidDetail,
    LevelCapReached,
    AttributeCapReached,
    ItemDataUnavailable,
}

public sealed record LegacySpecialScoreBonusOutcome(
    byte[] Mob,
    byte[] Affect,
    byte[] MobExtra,
    bool SendEtc,
    bool SendScore);

public sealed record LegacyGetItemOutcome(
    int ConnectionId,
    short WireItemId,
    short PositionX,
    short PositionY,
    int DestinationType,
    int DestinationSlot,
    LegacyItem Item,
    byte[] MobSnapshot);

public enum LegacyGetItemResult
{
    Accepted,
    ParticipantNotFound,
    CombatStateUnavailable,
    AttackerNotAlive,
    InvalidDestinationType,
    InvalidDestinationSlot,
    GroundItemNotFound,
    OutOfRange,
    GridMismatch,
    DestinationOccupied,
}

public sealed record LegacyDropItemOutcome(
    int ConnectionId,
    int SourceType,
    int SourceSlot,
    int Rotate,
    short PositionX,
    short PositionY,
    int WireItemId,
    LegacyItem Item,
    byte[] MobSnapshot);

public enum LegacyTradingItemResult
{
    Accepted,
    ParticipantNotFound,
    CombatStateUnavailable,
    AttackerNotAlive,
    InvalidSlot,
    SameSlot,
    SourceEmpty,
    InvalidPlace,
    CargoUnavailable,
    ItemDataUnavailable,
    EquipmentRejected,
}

public enum LegacyDeleteItemResult
{
    Accepted,
    ParticipantNotFound,
    CombatStateUnavailable,
    InvalidSlot,
    InvalidItemIndex,
    TradeCancelled,
    AccountBlocked,
    SourceEmpty,
    ItemChanged,
    TradeClosePending,
}

public sealed record LegacyTradingItemOutcome(
    int ConnectionId,
    int SourcePlace,
    int SourceSlot,
    int DestinationPlace,
    int DestinationSlot,
    LegacyItem SourceSlotItem,
    LegacyItem DestinationSlotItem,
    bool WasMerged,
    byte[] MobSnapshot);

public static class LegacyItemPlace
{
    public const int Equip = 0;
    public const int Carry = 1;
    public const int Cargo = 2;
}

public enum LegacySplitItemResult
{
    Accepted,
    ParticipantNotFound,
    CombatStateUnavailable,
    InvalidSlot,
    InvalidQuantity,
    SourceEmpty,
    UnsupportedItem,
    InsufficientAmount,
    InventoryFull,
}

public sealed record LegacySplitItemOutcome(
    int ConnectionId,
    int SourceSlot,
    int DestinationSlot,
    int RequestedItemIndex,
    int Quantity,
    LegacyItem UpdatedSourceItem,
    LegacyItem SplitItem,
    byte[] MobSnapshot);

public enum LegacyDropItemResult
{
    Accepted,
    ParticipantNotFound,
    CombatStateUnavailable,
    AttackerNotAlive,
    InvalidGrid,
    UnsupportedSourceType,
    InvalidSourceSlot,
    InvalidItem,
    ProtectedItem,
    NoGroundSpace,
}

public sealed record LegacyCoinUpdate(int ConnectionId, int Coin, int AddedCoin, byte[] MobSnapshot);

public sealed record LegacyPrivateNotice(int ConnectionId, string Message);

public sealed record LegacyGlobalNotice(string Message);

public sealed record LegacyAreaNotice(string Message, int X1, int Y1, int X2, int Y2);

public sealed record LegacyCastleQuestPlan(
    DateTime MinuteSlot,
    int PreviousState,
    int CurrentState,
    IReadOnlyList<LegacyAreaNotice> AreaNotices,
    IReadOnlyList<LegacyWorldNpc> RemovedNpcs);

public sealed record LegacyCastleQuestSecondPlan(
    DateTime SecondSlot,
    int PreviousTimeRemaining,
    int CurrentTimeRemaining,
    bool Expired,
    IReadOnlyList<LegacyWorldNpc> RemovedNpcs);

internal sealed record LegacyNpcDropResult(IReadOnlyList<LegacyItemDrop> ItemDrops, IReadOnlyList<LegacyGlobalNotice> GlobalNotices);

internal sealed record LegacyCastleQuestRewardResult(
    IReadOnlyList<LegacyItemDrop> ItemDrops,
    IReadOnlyList<LegacyExperienceAward> ExperienceAwards,
    IReadOnlyList<LegacyCoinUpdate> CoinUpdates,
    IReadOnlyList<LegacyPrivateNotice> PrivateNotices,
    byte[]? AttackerMobSnapshot);

/// <summary>Authoritative progress of the current Pista level 6 room.</summary>
public sealed record LegacyPistaLv6State(int LeaderConnectionId, int MobCount, int MobsLeft);

public sealed record LegacyPistaMobCountFrame(int ConnectionId, byte[] Frame);

public sealed record LegacyPistaRegistration(int Level, int PartySlot, int LeaderConnectionId, string LeaderName);

public sealed record LegacyPistaRegistrationOutcome(
    int Level,
    int PartySlot,
    int LeaderConnectionId,
    int InventorySlot,
    LegacyItem UpdatedItem,
    string LeaderName);

public sealed record LegacyPerzenExchangeOutcome(
    int NpcConnectionId,
    int InventorySlot,
    int RequiredItemIndex,
    int RewardItemIndex,
    LegacyItem UpdatedItem);

public enum LegacyPerzenExchangeResult
{
    Accepted,
    ParticipantNotFound,
    NpcNotFound,
    NotPerzen,
    InvalidNpc,
    InventoryUnavailable,
    MissingItem,
}

public enum LegacyPistaRegistrationResult
{
    Accepted,
    ParticipantNotFound,
    NpcNotFound,
    WrongNpc,
    NotPartyLeader,
    InventoryUnavailable,
    MissingTicket,
    AlreadyRegistered,
    RoomFull,
}

/// <summary>State change emitted when a Pista level 6 room mob dies.</summary>
public sealed record LegacyPistaTransition(
    int PistaLevel,
    int LeaderConnectionId,
    int PreviousMobCount,
    int RemainingMobCount,
    int SpawnGenerateIndex,
    IReadOnlyList<LegacyPistaTeleport>? Teleports = null,
    IReadOnlyList<int>? SpawnGenerateIndexes = null,
    IReadOnlyList<int>? RemovedNpcConnectionIds = null);

public sealed record LegacyPistaTeleport(
    int ConnectionId,
    int Level,
    int PartySlot,
    short FromX,
    short FromY,
    short ToX,
    short ToY);

public sealed record LegacyPistaEntryPlan(
    DateTime EntrySlot,
    IReadOnlyList<LegacyPistaTeleport> Teleports,
    IReadOnlyList<LegacyWorldNpc> SpawnedNpcs);

public sealed record LegacyPistaExitPlan(
    DateTime ExitSlot,
    IReadOnlyList<LegacyPistaTeleport> Teleports,
    IReadOnlyList<LegacyWorldNpc> RemovedNpcs,
    IReadOnlyList<LegacyItemDrop>? ItemDrops = null);

public sealed record PartyMemberSnapshot(int ConnectionId, string MobName, int CharacterClass, int Level, int MaxHp, int Hp);

public sealed record LegacyGuildDisableUpdate(
    int ConnectionId,
    bool Disabled,
    byte[] ScoreMobSnapshot,
    byte[] AffectSnapshot,
    IReadOnlyList<int> VisibleParticipantIds);

public sealed record LegacyBattleRoyaleDoorUpdate(IReadOnlyList<LegacyBattleRoyaleDoorGateUpdate> Gates);

public sealed record LegacyBattleRoyaleDoorGateUpdate(
    LegacyMapItemState MapItem,
    IReadOnlyList<int> ItemRecipientIds,
    IReadOnlyList<LegacyBattleRoyaleDoorActorEffect> ActorEffects);

public sealed record LegacyBattleRoyaleDoorActorEffect(
    int ConnectionId,
    short PositionX,
    short PositionY,
    short TargetX,
    short TargetY,
    bool Deleted,
    IReadOnlyList<int> RecipientIds,
    LegacyBattleRoyaleDoorPartyEffect? PartyEffect,
    IReadOnlyList<LegacyBattleRoyaleDoorSummonDespawn> DespawnedSummons);

public sealed record LegacyBattleRoyaleDoorPartyEffect(PartyRemovalPlan Removal, PartyFormationPlan? ReformedParty);

public sealed record LegacyBattleRoyaleDoorSummonDespawn(
    int ConnectionId,
    short PositionX,
    short PositionY,
    IReadOnlyList<int> RecipientIds);

public sealed record PartyInvitePlan(int LeaderConnectionId, int TargetConnectionId, PartyMemberSnapshot Leader, PartyMemberSnapshot Target);

public sealed record PartyFormationPlan(int LeaderConnectionId, IReadOnlyList<PartyMemberSnapshot> Members);

public sealed record PartyRemovalPlan(int LeaderConnectionId, int RemovedConnectionId, IReadOnlyList<int> NotifiedConnectionIds, bool Disbanded);

public sealed record LegacyBattleRoyaleSummonDespawnPlan(int ConnectionId, IReadOnlyList<int> NotifiedConnectionIds);

public sealed record LegacyBattleRoyalePartyMemberRemovalPlan(
    PartyRemovalPlan PartyRemoval,
    IReadOnlyList<LegacyBattleRoyaleSummonDespawnPlan> DespawnedSummons);

public enum PartyInviteCheckResult
{
    Accepted,
    ParticipantNotFound,
    SourceIsPartyMember,
    TargetAlreadyInParty,
    PartyFull,
    LevelLimit,
    BattleRoyaleArenaBlocked,
}

public enum PartyAcceptResult
{
    Accepted,
    ParticipantNotFound,
    NoPendingInvite,
    NameMismatch,
    SourceIsPartyMember,
    TargetAlreadyInParty,
    PartyFull,
    LevelLimit,
    BattleRoyaleArenaBlocked,
}

public enum LegacySummonGenerationResult
{
    Created,
    InvalidSummonId,
    CatalogUnavailable,
    TypeConflict,
    AlreadyPresent,
    PartyFull,
    PositionUnavailable,
}

public sealed record LegacySummonedMob(
    int ConnectionId,
    int LeaderConnectionId,
    int SummonerConnectionId,
    short PositionX,
    short PositionY,
    byte[] MobSnapshot,
    byte[] AffectSnapshot)
{
    public IReadOnlyList<int> EnemyList { get; init; } = [];
    public int RouteType { get; init; }
}

public sealed record LegacyWorldNpc(
    int ConnectionId,
    short PositionX,
    short PositionY,
    byte[] MobSnapshot,
    byte[] AffectSnapshot,
    int Mode = LegacyNpcMode.Peace,
    int CurrentTarget = LegacyNpcMode.EmptyTarget,
    IReadOnlyList<int>? initialEnemyList = null,
    int GenerateIndex = -1,
    int? TerrainHeight = null,
    LegacyAutoTradeSnapshot? AutoTradeSnapshot = null,
    string? AutoTradeOwnerAccount = null,
    int AutoTradeCharacterSlot = -1,
    int RouteType = 0)
{
    public IReadOnlyList<int> EnemyList { get; init; } = initialEnemyList ?? [];
    public byte[]? PursuitBaseResistances { get; init; }
    public LegacyNpcHolyTouchScoreBaseline? HolyTouchBaseScore { get; init; }
    public LegacyNpcFrozenBladeScoreBaseline? FrozenBladeBaseScore { get; init; }
    public int GenerationOwnerIndex { get; init; } = GenerateIndex;
    public int LeaderId { get; init; }
    public bool IsGeneratedGroupPartyMember { get; init; }
    public byte CombatAffectOpportunityPhase { get; init; }
    public LegacyNpcGenerationRoute GenerationRoute { get; init; } = LegacyNpcGenerationRoute.Empty;
    public short SegmentX { get; init; } = PositionX;
    public short SegmentY { get; init; } = PositionY;
    public int SegmentProgress { get; init; }
    public int SegmentDirection { get; init; }
    public int WaitSec { get; init; }
}

public readonly record struct LegacyNpcHolyTouchScoreBaseline(byte AttackRun, short Intelligence);
public readonly record struct LegacyNpcFrozenBladeScoreBaseline(byte AttackRun, short Intelligence);

public sealed class LegacyNpcAiPlanningSnapshot
{
    private readonly LegacyMobAiInput input;
    private readonly byte[] mobSnapshot;
    private readonly byte[] affectSnapshot;
    private readonly byte[] worldFingerprint;

    internal LegacyNpcAiPlanningSnapshot(
        int npcConnectionId,
        LegacyWorldNpc instanceToken,
        LegacyMobAiInput input,
        byte[] mobSnapshot,
        byte[] affectSnapshot,
        byte[] worldFingerprint,
        int physicalDamageRandomFactor,
        int attackMissRoll,
        LegacyNpcAiMovementPlan? movementPlan = null)
    {
        NpcConnectionId = npcConnectionId;
        InstanceToken = instanceToken;
        this.input = CloneInput(input);
        this.mobSnapshot = mobSnapshot.ToArray();
        this.affectSnapshot = affectSnapshot.ToArray();
        this.worldFingerprint = worldFingerprint.ToArray();
        PhysicalDamageRandomFactor = physicalDamageRandomFactor;
        AttackMissRoll = attackMissRoll;
        MovementPlan = movementPlan;
    }

    public int NpcConnectionId { get; }
    internal LegacyWorldNpc InstanceToken { get; }
    public byte[] MobSnapshot => mobSnapshot.ToArray();
    public byte[] AffectSnapshot => affectSnapshot.ToArray();
    public IReadOnlyList<int> EnemyList => input.Mob.EnemyIds.ToArray();
    public LegacyMobAiInput PlannerInput => CloneInput(input);

    internal byte[] WorldFingerprint => worldFingerprint.ToArray();
    internal int AttackRange => input.Mob.AttackRange;
    internal LegacyMobAiPoint? RouteStep => input.RouteStep;
    internal int RandomIntRoll => input.RandomIntRoll;
    internal int DexterityRoll => input.DexterityRoll;
    internal int SkillBarSelectionRoll => input.SkillBarSelectionRoll;
    internal int SkillBarMotionRoll => input.SkillBarMotionRoll;
    internal int PhysicalDamageRandomFactor { get; }
    internal int AttackMissRoll { get; }
    internal LegacyNpcAiMovementPlan? MovementPlan { get; }

    private static LegacyMobAiInput CloneInput(LegacyMobAiInput value)
    {
        var mob = value.Mob with
        {
            EnemyIds = value.Mob.EnemyIds.ToArray(),
            SkillBar = value.Mob.SkillBar.ToArray(),
            BaseSpecial = value.Mob.BaseSpecial.ToArray(),
        };
        return value with { Mob = mob, Targets = value.Targets.ToArray() };
    }
}

public enum LegacyNpcAiApplyStatus { Applied, Stale, Blocked, Unsupported, AttackUnsupported }

public sealed record LegacyNpcAiApplicationOutcome(
    LegacyNpcAiApplyStatus Status,
    LegacyMobAiDecisionKind DecisionKind,
    short PositionX,
    short PositionY,
    int Mode,
    int CurrentTarget,
    IReadOnlyList<int> EnemyList,
    short SegmentX,
    short SegmentY,
    int SegmentProgress,
    int SegmentDirection,
    int WaitSec,
    string? ActionText,
    LegacyGeneratedNpcPhysicalAttackOutcome? Attack = null,
    LegacyGeneratedNpcMovementOutcome? Movement = null,
    LegacyGeneratedNpcRemovalOutcome? Removal = null)
{
    public IReadOnlyList<LegacyGeneratedNpcRemovalOutcome> AdditionalRemovals { get; init; } = [];
    public LegacyGeneratedNpcAffectTickOutcome? AffectTick { get; init; }

    internal static LegacyNpcAiApplicationOutcome Empty(LegacyMobAiDecisionKind kind) =>
        new(LegacyNpcAiApplyStatus.Unsupported, kind, 0, 0, LegacyNpcMode.Peace, 0, [], 0, 0, 0, 0, 0, null);
}

public sealed record LegacyPlayerAffectTimerOutcome(
    int ConnectionId,
    byte[] MobSnapshot,
    byte[] AffectSnapshot,
    bool EquipmentChanged,
    IReadOnlyList<int> ViewRecipientConnectionIds)
{
    public bool ScoreRefreshRequired { get; init; } = true;
    public bool HpUpdateRequired { get; init; }
    public bool ResourceRefreshRequired { get; init; }
    public int Hp { get; init; }
    public int Damage { get; init; }
    public IReadOnlyList<LegacyPlayerAffectAreaAttackOutcome> AreaAttacks { get; init; } = [];
}

public sealed record LegacyGeneratedNpcAffectTickOutcome(
    int ConnectionId,
    int Hp,
    int Damage,
    byte[] MobSnapshot,
    byte[] AffectSnapshot,
    IReadOnlyList<int> ViewRecipientConnectionIds,
    bool ScoreRefreshRequired)
{
    public bool HpUpdateRequired { get; init; } = true;
    public int MountOwnerConnectionId { get; init; }
    public LegacyItem? UpdatedMountItem { get; init; }
}

public sealed record LegacyGeneratedNpcRemovalOutcome(
    int ConnectionId,
    IReadOnlyList<int> ViewRecipientConnectionIds,
    int RemoveType = 3);

public sealed record LegacyGeneratedNpcMovementOutcome(
    int ConnectionId,
    short FromX,
    short FromY,
    short ToX,
    short ToY,
    int Speed,
    byte[] MobSnapshot,
    byte[] AffectSnapshot,
    IReadOnlyList<int> OriginViewConnectionIds,
    IReadOnlyList<int> DestinationViewConnectionIds,
    int BattleRoyaleState,
    int Effect = 0);

public sealed record LegacyGeneratedNpcPhysicalAttackOutcome(
    int AttackerConnectionId,
    int TargetConnectionId,
    short AttackerPositionX,
    short AttackerPositionY,
    short TargetPositionX,
    short TargetPositionY,
    int Damage,
    int RemainingHp,
    byte[] TargetMobSnapshot,
    bool TargetDied,
    bool Missed,
    bool TargetRevived = false,
    int ConsumedItemSlot = -1,
    LegacyItem? ConsumedItem = null,
    IReadOnlyList<int>? ViewRecipientConnectionIds = null,
    int SkillIndex = -1,
    int SkillParm = 1,
    int Motion = 4,
    bool TargetIsNpc = false,
    byte[]? TargetAffectSnapshot = null);

public static class LegacyNpcMode
{
    public const int Empty = 0;
    public const int EmptyTarget = 0;
    public const int Idle = 3;
    public const int Combat = 5;
    public const int Peace = 4;
}

public sealed record LegacySummonMovement(
    int ConnectionId,
    short FromX,
    short FromY,
    short ToX,
    short ToY,
    int Effect,
    int Speed);

public sealed record LegacySummonTargetSelection(int SummonConnectionId, int TargetConnectionId, int? Distance);

public enum LegacySummonAttackResult
{
    Accepted,
    SummonNotFound,
    NoTarget,
    TargetNotFound,
    CombatStateUnavailable,
    SummonNotAlive,
    TargetNotAlive,
    OutOfRange,
}

public sealed record LegacySummonAttackOutcome(
    int SummonConnectionId,
    int TargetConnectionId,
    short SummonPositionX,
    short SummonPositionY,
    short TargetPositionX,
    short TargetPositionY,
    int Damage,
    int RemainingHp,
    byte[] TargetMobSnapshot,
    bool TargetDied = false);

public sealed record LegacySummonBattleOpportunityOutcome(
    LegacyNpcAiApplicationOutcome? Movement,
    LegacySummonAttackOutcome? Attack,
    LegacyNpcAiApplicationOutcome? Despawn = null)
{
    public LegacyNpcAiApplicationOutcome? AffectUpdate { get; init; }
}

file readonly record struct LegacyRecallZone(int GuildSpawnX, int GuildSpawnY, int CitySpawnX, int CitySpawnY, int ChargeGuild);

file static class LegacyRecallData
{
    public const int LegacyFreeExp = 35;
    public static readonly LegacyRecallZone[] Zones =
    [
        new(2088, 2148, 2086, 2093, 0), // Armia
        new(2531, 1700, 2494, 1707, 0), // Azran
        new(2460, 1976, 2453, 2000, 0), // Erion
        new(3614, 3124, 3652, 3122, 0), // Nippleheim
        new(1066, 1760, 1050, 1706, 0), // Noatum
    ];
}


file static class LegacyCombatMath
{
    private static readonly int[,] DistanceTable =
    {
        { 0, 1, 2, 3, 4, 5, 6 },
        { 1, 1, 2, 3, 4, 5, 6 },
        { 2, 2, 3, 4, 4, 5, 6 },
        { 3, 3, 4, 4, 5, 5, 6 },
        { 4, 4, 4, 5, 5, 5, 6 },
        { 5, 5, 5, 5, 5, 6, 6 },
        { 6, 6, 6, 6, 6, 6, 6 },
    };

    public static int GetDistance(int x1, int y1, int x2, int y2)
    {
        var dx = Math.Abs(x1 - x2);
        var dy = Math.Abs(y1 - y2);
        return dx <= 6 && dy <= 6 ? DistanceTable[dy, dx] : Math.Max(dx, dy) + 1;
    }
}

file static class LegacyClanRelations
{
    public static readonly byte[][] Table =
    [
        [1, 0, 1, 1, 1, 0, 1, 1, 1],
        [0, 1, 1, 0, 1, 1, 1, 0, 0],
        [1, 1, 1, 0, 1, 1, 1, 1, 1],
        [1, 0, 0, 1, 1, 0, 1, 1, 1],
        [1, 1, 1, 1, 1, 0, 1, 1, 1],
        [0, 1, 1, 0, 0, 1, 0, 0, 0],
        [1, 1, 1, 1, 1, 0, 1, 1, 1],
        [1, 0, 1, 1, 1, 0, 1, 0, 1],
        [1, 0, 1, 1, 1, 0, 1, 0, 1],
    ];
}

file static class WorldHubLegacyOffsets
{
    public const int LegacyAccountMobSize = 816;
    public const int LegacyAccountMobGuildOffset = 18;
    public const int LegacyAccountMobCoinOffset = 28;
}

public enum GuildInviteCheckResult
{
    Accepted,
    InvalidTarget,
    InvalidInviteType,
    Sunday,
    ParticipantNotFound,
    SourceHasNoGuild,
    TargetAlreadyHasGuild,
    ClanMismatch,
    SourceNotGuildMember,
    LeaderRequired,
    InsufficientCoin,
}

public sealed record GuildInvitePlan(
    int SourceConnectionId,
    int TargetConnectionId,
    string SourceAccountName,
    int SourceCharacterSlot,
    string TargetAccountName,
    int TargetCharacterSlot,
    int GuildId,
    int SourceCoin,
    int TargetCoin,
    int Cost);

using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>Shared in-memory presence for the connected players in the current world instance.</summary>
public sealed class WorldHub
{
    private readonly object gate = new();
    private readonly Dictionary<int, Participant> participants = [];
    private readonly Dictionary<int, int> partyLeaders = [];
    private readonly Dictionary<int, int> pendingPartyLeaders = [];
    private readonly Dictionary<int, LegacyWorldNpc> npcs = [];
    private readonly Dictionary<(int Level, int PartySlot), LegacyPistaRegistration> pistaRegistrations = [];
    private readonly Dictionary<(int Level, int PartySlot), int> pistaMobCounts = [];
    private static readonly int[] pistaLevel1Runes = [5114, 5113, 5117, 5111, 5115, 5112];
    private static readonly int[] pistaLevel3Runes = [5122, 5126, 5121, 5116, 5119];
    private DateTime? lastPistaEntrySlot;
    private DateTime? lastPistaExitSlot;
    private DateTime? lastPistaLv6MobLeftAt;
    private DateTime? lastCastleQuestMinute;
    private readonly LegacyMapGrid? mapGrid;
    private readonly LegacyGuildZoneState? guildZones;
    private readonly LegacyMapCollisionMode mapCollisionMode;
    private readonly LegacySummonCatalog? summonCatalog;
    private readonly LegacyItemDataTable? itemData;
    private readonly LegacySkillDataTable? skillData;
    private readonly LegacyNpcGenerationCatalog? npcGenerationCatalog;
    private readonly LegacyServerModePolicy serverModePolicy;
    private LegacyDonateShopCatalog? donateShopCatalog;
    private LegacyNpcEventDropConfiguration? npcEventDrop;
    private IReadOnlyList<LegacyCastleQuestDefinition> castleQuests =
        [new LegacyCastleQuestDefinition(1559, 1604, 0, 3, new LegacyItem[LegacyCastleQuestConfiguration.MaxCarry], 0, new int[6], false, 500)];
    private int npcEventCurrentIndex;
    private LegacyPistaLv6State? pistaLv6State;
    private LegacyCombatWorldState combatWorldState = new();
    private readonly Dictionary<int, LegacySummonedMob> summons = [];
    private readonly Dictionary<int, int> summonTargets = [];
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

    public bool ConfigurePistaLv6State(int leaderConnectionId, int mobCount)
    {
        if (leaderConnectionId <= 0 || mobCount < 0)
            return false;
        lock (gate)
        {
            if (!participants.ContainsKey(leaderConnectionId))
                return false;
            pistaLv6State = new LegacyPistaLv6State(leaderConnectionId, mobCount);
            lastPistaLv6MobLeftAt = null;
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
                pistaLv6State = new LegacyPistaLv6State(connectionId, 0);

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
                    pistaLv6State = new LegacyPistaLv6State(registration.LeaderConnectionId, 100);
                    lastPistaLv6MobLeftAt = null;
                    foreach (var generateIndex in GetPistaEntryGenerators(registration.Level, registration.PartySlot))
                        if (TrySpawnPistaEntryNpc(generateIndex, registration.Level, registration.PartySlot, out var generated) && generated is not null)
                            spawned.Add(generated);
                }
                else
                {
                    if (registration.Level == 4)
                        pistaMobCounts[(4, registration.PartySlot)] = 8 + (pistaLevel4MobRoll >= 0 ? pistaLevel4MobRoll : System.Security.Cryptography.RandomNumberGenerator.GetInt32(8));
                    foreach (var generateIndex in GetPistaEntryGenerators(registration.Level, registration.PartySlot))
                        if (TrySpawnPistaEntryNpc(generateIndex, registration.Level, registration.PartySlot, out var generated) && generated is not null)
                            spawned.Add(generated);
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
                npcs.Remove(npc.ConnectionId);

            pistaRegistrations.Clear();
            pistaMobCounts.Clear();
            pistaLv6State = null;
            lastPistaLv6MobLeftAt = null;

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

    private bool TrySpawnPistaEntryNpc(int generateIndex, int level, int partySlot, out LegacyWorldNpc? spawned)
    {
        var position = GetPistaEntryPosition(level, partySlot, generateIndex);
        return TrySpawnGeneratedNpc(generateIndex, out spawned, positionX: position?.X, positionY: position?.Y);
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
        generateIndex is >= 5653 and <= 5899 or >= 5948 and <= 5955 or >= 5972 and <= 5975;

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
                .Where(static npc => npc.GenerateIndex is >= 5767 and <= 5785)
                .ToArray();
            foreach (var npc in removed)
                npcs.Remove(npc.ConnectionId);

            foreach (var key in pistaRegistrations.Keys.Where(static key => key.Level == 6).ToArray())
                pistaRegistrations.Remove(key);
            pistaLv6State = null;
            lastPistaLv6MobLeftAt = null;
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
    /// Builds the legacy <c>_MSG_MobLeft</c> signal for a player inside the
    /// Pista +6 rectangle. The reference timer sends this as a
    /// <c>SendSignalParmArea(3330, 1475, 3448, 1525, ...)</c> update.
    /// </summary>
    public bool TryBuildPistaLv6MobLeftFrame(int connectionId, LegacyFrameCodec codec, uint clientTick, byte keywordIndex, out byte[]? frame)
    {
        ArgumentNullException.ThrowIfNull(codec);
        lock (gate)
        {
            frame = null;
            if (pistaLv6State is null || !participants.TryGetValue(connectionId, out var participant))
                return false;
            if (participant.PositionX < 3330 || participant.PositionX >= 3448 || participant.PositionY < 1475 || participant.PositionY >= 1525)
                return false;

            frame = new MobLeftConfirmation(pistaLv6State.MobCount).ToFrame(codec, clientTick, keywordIndex);
            return true;
        }
    }

    /// <summary>
    /// Emits the legacy +6 room counter on the same wall-clock cadence as
    /// <c>ProcessSecMinTimer</c>: twelve 200 ms ticks (2.4 seconds). Each
    /// participant is sent a private frame only while inside the original
    /// exclusive rectangle.
    /// </summary>
    public bool TryProcessPistaLv6MobLeft(DateTime localNow, LegacyFrameCodec codec, uint clientTick, byte keywordIndex, out IReadOnlyList<LegacyPistaMobLeftFrame>? frames)
    {
        ArgumentNullException.ThrowIfNull(codec);
        lock (gate)
        {
            frames = null;
            if (pistaLv6State is null)
                return false;
            if (lastPistaLv6MobLeftAt is { } last && localNow - last < TimeSpan.FromMilliseconds(2400))
                return false;

            lastPistaLv6MobLeftAt = localNow;
            var frame = new MobLeftConfirmation(pistaLv6State.MobCount).ToFrame(codec, clientTick, keywordIndex);
            frames = participants.Values
                .Where(static participant => participant.PositionX >= 3330 && participant.PositionX < 3448 && participant.PositionY >= 1475 && participant.PositionY < 1525)
                .Select(participant => new LegacyPistaMobLeftFrame(participant.ConnectionId, frame))
                .ToArray();
            return true;
        }
    }

    /// <summary>Creates one generated leader NPC using the legacy NPCGener/template pair.</summary>
    public bool TrySpawnGeneratedNpc(int generateIndex, out LegacyWorldNpc? spawned, Func<int, int>? randomRoll = null, short? positionX = null, short? positionY = null, int? terrainHeight = null)
    {
        if (positionX.HasValue != positionY.HasValue)
            throw new ArgumentException("A generated NPC position override requires both X and Y.");

        lock (gate)
        {
            spawned = null;
            if (npcGenerationCatalog is null || !npcGenerationCatalog.TryGet(generateIndex, out var definition) || definition is null)
                return false;

            var currentCount = npcs.Values.Count(npc => npc.GenerateIndex == generateIndex);
            if (definition.MaxNumMob <= 0 || currentCount >= definition.MaxNumMob)
                return false;
            if (!npcGenerationCatalog.TryCreateLeader(generateIndex, randomRoll, out var generated) || generated is null)
                return false;

            var connectionId = AllocateNpcConnectionId();
            var spawnX = positionX ?? generated.PositionX;
            var spawnY = positionY ?? generated.PositionY;
            var effectiveTerrainHeight = terrainHeight
                ?? mapGrid?.GetTerrainHeight(spawnX, spawnY)
                ?? (IsPistaGeneratedIndex(generateIndex) ? 0 : null);
            spawned = new LegacyWorldNpc(
                connectionId,
                spawnX,
                spawnY,
                generated.MobSnapshot,
                generated.AffectSnapshot,
                GenerateIndex: generated.GenerateIndex,
                TerrainHeight: effectiveTerrainHeight);
            npcs.Add(connectionId, spawned);
            return true;
        }
    }

    public bool Enter(int connectionId, string accountName, Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> send, ReadOnlyMemory<byte> spawnFrame = default)
    {
        ArgumentNullException.ThrowIfNull(accountName);
        ArgumentNullException.ThrowIfNull(send);
        lock (gate)
        {
            if (participants.ContainsKey(connectionId)) return false;
            participants.Add(connectionId, new Participant(connectionId, accountName, send, spawnFrame));
            return true;
        }
    }

    /// <summary>Registers a persistent non-summon NPC using a full legacy STRUCT_MOB snapshot.</summary>
    public int EnterNpc(ReadOnlyMemory<byte> mob, short positionX, short positionY, ReadOnlyMemory<byte> affect = default, int requestedConnectionId = 0, int generateIndex = -1, int? terrainHeight = null)
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

            npcs.Add(connectionId, new LegacyWorldNpc(
                connectionId,
                positionX,
                positionY,
                mob.ToArray(),
                affect.IsEmpty ? new byte[LegacyAccountSnapshot.AffectStride] : affect.ToArray(),
                GenerateIndex: generateIndex,
                TerrainHeight: terrainHeight));
            return connectionId;
        }
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
            return npcs.Values.OrderBy(static npc => npc.ConnectionId).ToArray();
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
        }

        foreach (var participant in existing)
            if (!participant.SpawnFrame.IsEmpty) await send(participant.SpawnFrame, cancellationToken);
        if (!spawnFrame.IsEmpty) await BroadcastAsync(connectionId, spawnFrame, cancellationToken);
        return true;
    }

    public bool Leave(int connectionId) => Leave(connectionId, out _);

    /// <summary>Removes a player and all NPC summons owned by that player.</summary>
    public bool Leave(int connectionId, out IReadOnlyList<int> despawnedSummonIds)
    {
        lock (gate)
        {
            despawnedSummonIds = [];
            if (!participants.TryGetValue(connectionId, out var participant)) return false;

            var despawned = new List<int>(participant.SummonIds.Count);
            foreach (var summonId in participant.SummonIds)
            {
                if (summons.Remove(summonId))
                {
                    summonTargets.Remove(summonId);
                    despawned.Add(summonId);
                }
            }

            var partyLeader = partyLeaders.TryGetValue(connectionId, out var currentLeader) ? currentLeader : connectionId;
            foreach (var member in partyLeaders.Where(pair => pair.Value == partyLeader).Select(pair => pair.Key).ToArray())
                partyLeaders.Remove(member);
            foreach (var request in pendingPartyLeaders.Where(pair => pair.Key == connectionId || pair.Value == connectionId).Select(pair => pair.Key).ToArray())
                pendingPartyLeaders.Remove(request);
            participants.Remove(connectionId);
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
        lock (gate) combatWorldState = state;
    }

    public LegacyCombatWorldState GetCombatWorldState()
    {
        lock (gate) return combatWorldState;
    }

    /// <summary>Returns connected players inside an inclusive legacy notice rectangle.</summary>
    public IReadOnlyList<int> GetParticipantIdsInArea(int x1, int y1, int x2, int y2)
    {
        lock (gate)
        {
            return participants.Values
                .Where(participant => participant.PositionX >= x1 && participant.PositionX <= x2 && participant.PositionY >= y1 && participant.PositionY <= y2)
                .Select(static participant => participant.ConnectionId)
                .ToArray();
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
                npcs.Remove(npc.ConnectionId);
            combatWorldState = combatWorldState with { CastleQuestClear = 0 };
            plan = new LegacyCastleQuestPlan(minuteSlot, PreviousState: 2, CurrentState: 0, AreaNotices: [], RemovedNpcs: removed);
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

    public bool SetCharacterState(int connectionId, int characterSlot, int guildId, int clan, int guildLevel, int coin, short positionX, short positionY, ReadOnlyMemory<byte> mob, short classMaster, ReadOnlyMemory<byte> affect, ReadOnlyMemory<byte> mobExtra)
    {
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant)) return false;
            participant.CharacterSlot = characterSlot;
            participant.GuildId = guildId;
            participant.Clan = clan;
            participant.GuildLevel = guildLevel;
            participant.Coin = coin;
            participant.PositionX = positionX;
            participant.PositionY = positionY;
            participant.Mob = mob.ToArray();
            participant.ClassMaster = classMaster;
            participant.ExperienceSegment = 0;
            participant.Affect = affect.Length == LegacyAccountSnapshot.AffectStride ? affect.ToArray() : new byte[LegacyAccountSnapshot.AffectStride];
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
            var capacity = 30;
            if (ReadCarryItem(participant, 60).Index == 3467)
                capacity += 15;
            if (ReadCarryItem(participant, 61).Index == 3467)
                capacity += 15;

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

    /// <summary>Applies the legacy Vol 10 family of timed Type 4 affects.</summary>
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
                damage = ResolveLegacyParry(target.Mob, summonState, targetState, itemData, skillId: -1, parryRandomRoll, damage);
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

    public bool TryGetCombatState(int connectionId, out LegacyMobCombatState? state)
    {
        lock (gate)
        {
            state = null;
            if (!participants.TryGetValue(connectionId, out var participant) || participant.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                return false;

            state = LegacyMobCombatState.Read(participant.Mob);
            return true;
        }
    }

    /// <summary>Returns the authoritative STRUCT_MOB and current world position for account persistence.</summary>
    public bool TryGetCharacterSnapshot(int connectionId, out byte[]? mob, out short positionX, out short positionY)
        => TryGetCharacterSnapshot(connectionId, out mob, out positionX, out positionY, out _);

    public bool TryGetCharacterSnapshot(int connectionId, out byte[]? mob, out short positionX, out short positionY, out byte[]? mobExtra)
    {
        lock (gate)
        {
            mob = null;
            mobExtra = null;
            positionX = 0;
            positionY = 0;
            if (!participants.TryGetValue(connectionId, out var participant) || participant.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                return false;

            mob = participant.Mob.ToArray();
            mobExtra = participant.MobExtra.ToArray();
            positionX = participant.PositionX;
            positionY = participant.PositionY;
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

            var before = LegacyMobCombatState.Read(participant.Mob);
            var manaSpent = LegacySkillCombatMath.GetManaSpent(skill, before);
            if (manaSpent < 0 || before.CurrentMana < manaSpent)
                return LegacySkillManaResult.InsufficientMana;

            var newMana = before.CurrentMana - manaSpent;
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(participant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentMpOffset), newMana);
            participant.RequestedMana = Math.Max(0, Math.Max(participant.RequestedMana - manaSpent, newMana));
            state = LegacyMobCombatState.Read(participant.Mob);
            mobSnapshot = participant.Mob.ToArray();
            requestedMana = participant.RequestedMana;
            return LegacySkillManaResult.Accepted;
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

    public LegacyPhysicalAttackResult TryApplyPhysicalAttack(
        int attackerConnectionId,
        int targetConnectionId,
        int randomFactor,
        out LegacyPhysicalAttackOutcome? outcome,
        int parryRandomRoll = -1,
        LegacyItemDataTable? itemData = null,
        int absorptionRoll = -1,
        int frostRoll = -1,
        int drainRoll = -1,
        int specialDropRoll = -1,
        int specialItemRoll = -1,
        int eventDropRoll = -1,
        int runeRoll = -1,
        int runeChanceRoll = -1,
        int pistaRandomRoll = -1)
    {
        if (absorptionRoll is < -1 or > 1)
            throw new ArgumentOutOfRangeException(nameof(absorptionRoll), absorptionRoll, "The injected legacy absorption roll must be -1, 0, or 1.");
        if (frostRoll is < -1 or > 1)
            throw new ArgumentOutOfRangeException(nameof(frostRoll), frostRoll, "The injected legacy frost roll must be -1, 0, or 1.");
        if (drainRoll is < -1 or > 1)
            throw new ArgumentOutOfRangeException(nameof(drainRoll), drainRoll, "The injected legacy drain roll must be -1, 0, or 1.");

        outcome = null;
        lock (gate)
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
                if (targetSummon.SummonerConnectionId == attackerConnectionId)
                    return LegacyPhysicalAttackResult.FriendlyFireBlocked;
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
            if (targetParticipant is not null && IsPeacefulPlayerTargetBlocked(attacker, targetParticipant, aggressive: true))
                return LegacyPhysicalAttackResult.PeacefulZoneBlocked;
            if (targetParticipant is not null && attacker.GuildId != 0 && attacker.GuildId == targetParticipant.GuildId)
                return LegacyPhysicalAttackResult.FriendlyFireBlocked;

            var attackerState = LegacyMobCombatState.Read(attacker.Mob);
            var targetState = LegacyMobCombatState.Read(targetMob);
            var targetAffect = targetParticipant?.Affect ?? targetSummon?.AffectSnapshot.ToArray() ?? targetNpc?.AffectSnapshot.ToArray() ?? new byte[LegacyAccountSnapshot.AffectStride];
            if (attackerState.CurrentScore.Hp <= 0)
                return LegacyPhysicalAttackResult.AttackerNotAlive;
            if (targetState.CurrentScore.Hp <= 0)
                return LegacyPhysicalAttackResult.TargetNotAlive;

            var distance = LegacyCombatMath.GetDistance(attacker.PositionX, attacker.PositionY, targetPositionX, targetPositionY);
            if (distance > 23)
                return LegacyPhysicalAttackResult.OutOfRange;

            var defense = targetParticipant is not null ? targetState.CurrentScore.Ac * 3 : targetState.CurrentScore.Ac;
            var damage = LegacySkillCombatMath.GetPhysicalDamage(attackerState.CurrentScore.Damage, defense, combat: 0, randomFactor);
            if (targetParticipant is not null)
                damage /= 4; // TMSrv's player-target penetration step for physical attacks.
            if (damage <= 0) damage = 1;

            damage = ResolveLegacyParry(targetMob, attackerState, targetState, itemData, skillId: 0, parryRandomRoll, damage);

            var forceDamage = 0;
            var pvpDamage = 0;
            if (damage > 0)
            {
                forceDamage = GetLegacyForceDamage(attacker.Mob, itemData);
                if (forceDamage != 0)
                    damage = damage <= 1 ? forceDamage : damage + forceDamage;

                if (targetParticipant is not null)
                {
                    pvpDamage = GetLegacyPvpDamage(attacker.Mob, itemData);
                    if (pvpDamage != 0)
                        damage = damage <= 1
                            ? damage + (damage * pvpDamage / 100)
                            : damage + (damage / 100 * pvpDamage);
                }
            }

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

            var secondaryEffectsChanged = false;
            if (damage > 0)
            {
                var effectLevel = attackerState.CurrentScore.Special2;
                var effectDelay = effectLevel + 150;
                var effectiveFrostRoll = frostRoll >= 0 ? frostRoll : System.Security.Cryptography.RandomNumberGenerator.GetInt32(2);
                if ((attackerState.Rsv & 0x01) != 0 && effectiveFrostRoll == 0)
                    secondaryEffectsChanged |= TryApplyLegacyAffects(targetAffect, skillData?[36], effectDelay, effectLevel);

                var effectiveDrainRoll = drainRoll >= 0 ? drainRoll : System.Security.Cryptography.RandomNumberGenerator.GetInt32(2);
                if ((attackerState.Rsv & 0x02) != 0 && effectiveDrainRoll == 0)
                    secondaryEffectsChanged |= TryApplyLegacyAffects(targetAffect, skillData?[40], effectDelay, effectLevel);
            }

            var reflectDamage = 0;
            var reflectPvp = 0;
            if (targetParticipant is not null && damage > 0)
            {
                reflectDamage = GetLegacyReflectDamage(targetMob, targetState, itemData);
                reflectPvp = GetLegacyReflectPvp(targetMob, itemData);
                if (reflectDamage > 0)
                {
                    damage -= reflectDamage;
                    if (damage <= 0) damage = 1;
                }

                if (reflectPvp > 0)
                {
                    damage -= damage / 100 * reflectPvp;
                    if (damage <= 0) damage = 1;
                }
            }

            var adultMountDamage = 0;
            var adultMountAttached = false;
            var damageBeforeAdultMount = damage;
            if (targetParticipant is not null && damage > 0)
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

            var remainingHp = damage <= 0 ? targetState.CurrentScore.Hp : Math.Max(0, targetState.CurrentScore.Hp - damage);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), remainingHp);
            var targetRevived = false;
            var consumedItemSlot = -1;
            LegacyItem? consumedItem = null;
            if (remainingHp == 0 && targetParticipant is not null && TryConsumeResurrectionScroll(targetParticipant, out consumedItemSlot, out var revivedItem))
            {
                targetRevived = true;
                consumedItem = revivedItem;
                remainingHp = BinaryPrimitives.ReadInt32LittleEndian(targetParticipant.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24));
            }
            if (targetParticipant is not null)
            {
                targetParticipant.RequestedHp = targetRevived
                    ? remainingHp
                    : damage > 0
                        ? Math.Max(0, targetParticipant.RequestedHp - damage)
                        : targetParticipant.RequestedHp;
            }
            var attackerHpAbsorbed = 0;
            var effectiveAbsorptionRoll = absorptionRoll >= 0
                ? absorptionRoll
                : System.Security.Cryptography.RandomNumberGenerator.GetInt32(2);
            if (damage >= 1 && effectiveAbsorptionRoll == 0)
            {
                var hpAbs = GetLegacyHpAbs(attacker.Affect);
                if (hpAbs != 0)
                {
                    attackerHpAbsorbed = Math.Min(350, (damageBeforeAdultMount * hpAbs + 1) / 100);
                    var requestedHp = attacker.RequestedHp + attackerHpAbsorbed;
                    var attackerMaxHp = LegacyMobCombatState.Read(attacker.Mob).CurrentScore.MaxHp;
                    attacker.RequestedHp = requestedHp > attackerMaxHp ? attackerMaxHp : attackerHpAbsorbed;
                }
            }
            var targetRemoved = false;
            LegacyPistaTransition? pistaTransition = null;
            long experienceAwarded = 0;
            byte[]? attackerMobSnapshot = null;
            IReadOnlyList<LegacyExperienceAward> experienceAwards = [];
            IReadOnlyList<LegacyItemDrop> itemDrops = [];
            IReadOnlyList<LegacyCoinUpdate> coinUpdates = [];
            IReadOnlyList<LegacyPrivateNotice> privateNotices = [];
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
            if (targetSummon is not null)
            {
                TryLinkMountHpLocked(targetSummon, targetMob, out mountOwnerConnectionId, out updatedMountItem);
                targetRemoved = remainingHp == 0 && RemoveSummonLocked(targetConnectionId);
                if (!targetRemoved)
                    summons[targetConnectionId] = targetSummon with { MobSnapshot = targetMob, AffectSnapshot = targetAffect };
            }
                else if (targetNpc is not null)
            {
                targetRemoved = remainingHp == 0 && RemoveNpcLocked(targetConnectionId);
                if (!targetRemoved)
                    npcs[targetConnectionId] = targetNpc with { MobSnapshot = targetMob, AffectSnapshot = targetAffect };
                else if (targetMob[LegacyAccountSnapshot.MobClanOffset] != 4)
                {
                    experienceAwards = AwardNpcExperienceLocked(attacker, attackerState, targetState, targetNpc.PositionX, targetNpc.PositionY);
                    var attackerAward = experienceAwards.FirstOrDefault(award => award.ConnectionId == attackerConnectionId);
                    experienceAwarded = attackerAward?.Experience ?? 0;
                    attackerMobSnapshot = attackerAward?.MobSnapshot;
                }
                if (targetRemoved)
                {
                    var effectivePistaChanceRoll = runeChanceRoll;
                    if (targetNpc.GenerateIndex is 5653 or 5654 && effectivePistaChanceRoll < 0)
                        effectivePistaChanceRoll = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100);
                    pistaTransition = ApplyPistaDeathLocked(attacker, targetNpc, pistaRandomRoll, effectivePistaChanceRoll);
                    areaNotices = ApplyNpcDeathNoticesLocked(targetNpc, ReadMobName(attacker.Mob));
                    var dropResult = AwardNpcDropsLocked(attacker, targetNpc, targetMob, targetState.CurrentScore.Level, specialDropRoll, specialItemRoll, eventDropRoll, runeRoll, effectivePistaChanceRoll);
                    itemDrops = dropResult.ItemDrops;
                    globalNotices = dropResult.GlobalNotices;
                    if (itemDrops.Count > 0)
                        attackerMobSnapshot = attacker.Mob.ToArray();
                    var castleReward = AwardCastleQuestLocked(attacker, targetNpc);
                    if (castleReward is not null)
                    {
                        itemDrops = [.. itemDrops, .. castleReward.ItemDrops];
                        experienceAwards = [.. experienceAwards, .. castleReward.ExperienceAwards];
                        coinUpdates = castleReward.CoinUpdates;
                        privateNotices = castleReward.PrivateNotices;
                        attackerMobSnapshot = castleReward.AttackerMobSnapshot ?? attackerMobSnapshot;
                    }
                }
            }
            else
            {
                targetParticipant!.Mob = targetMob;
                targetParticipant.Affect = targetAffect;
            }

            outcome = new LegacyPhysicalAttackOutcome(targetConnectionId, damage, remainingHp, targetMob.ToArray(), targetRemoved, remainingHp == 0, targetRevived, consumedItemSlot, consumedItem, experienceAwarded, attackerMobSnapshot, experienceAwards, itemDrops, TargetHpChanged: targetParticipant is not null && (damage > 0 || targetRevived), AttackerHpAbsorbed: attackerHpAbsorbed, AttackerRequestedHp: attacker.RequestedHp, ForceDamage: forceDamage, PvpDamage: pvpDamage, MountOwnerConnectionId: mountOwnerConnectionId, UpdatedMountItem: updatedMountItem, PkPointGateBlocked: pkPointGateBlocked, CrimeStateUpdates: crimeStateUpdates, TargetAffectSnapshot: secondaryEffectsChanged ? targetAffect.ToArray() : null, ReflectDamage: reflectDamage, ReflectPvp: reflectPvp, MountDurabilityLoss: mountDurabilityLoss, PistaTransition: pistaTransition, GlobalNotices: globalNotices, AreaNotices: areaNotices, CoinUpdates: coinUpdates, PrivateNotices: privateNotices);
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
        int parryRandomRoll = -1,
        int resurrectionRoll = -1,
        int resurrectionHpRoll = -1,
        int resurrectionMpRoll = -1,
        int recallCityRandomX = -1,
        int recallCityRandomY = -1,
        int recallNewbieRandomX = -1,
        int recallNewbieRandomY = -1,
        int specialDropRoll = -1,
        int specialItemRoll = -1,
        int eventDropRoll = -1,
        int runeRoll = -1,
        int runeChanceRoll = -1,
        int pistaRandomRoll = -1)
    {
        ArgumentNullException.ThrowIfNull(skill);
        outcome = null;
        var isHealing = skill.InstanceType == 6;
        var isDetox = skill.InstanceType == 8;
        var isSummon = skill.InstanceType == 9;
        var isNpcSummon = skill.InstanceType == 11;
        var isEtherealFlame = skill.InstanceType == 12;
        var isInvisibility = skill.InstanceType == 10;
        var isResurrection = skill.Id == 99;
        var isEffectOnly = skill.InstanceType == 0 && (skill.AffectType > 0 || skill.TickType > 0);
        var isSupport = isHealing || isDetox || isSummon || isNpcSummon || isEtherealFlame || isInvisibility || isResurrection || isEffectOnly;
        var isBeneficial = isHealing || isDetox || isSummon || isNpcSummon || isInvisibility || isResurrection || (isEffectOnly && skill.Aggressive == 0);
        if (!isSupport && skill.InstanceType != 7) ArgumentNullException.ThrowIfNull(itemData);

        lock (gate)
        {
            if (!participants.TryGetValue(attackerConnectionId, out var attacker))
                return LegacySkillAttackResult.ParticipantNotFound;
            if (!participants.TryGetValue(targetConnectionId, out var target))
            {
                if (npcs.TryGetValue(targetConnectionId, out var flashNpcTarget) && skill.InstanceType == 7)
                    return TryApplyNpcFlashLocked(flashNpcTarget, targetConnectionId, out outcome);
                if (npcs.TryGetValue(targetConnectionId, out var flameNpcTarget) && isEtherealFlame)
                    return TryApplyNpcEtherealFlameLocked(attacker, flameNpcTarget, targetConnectionId, out outcome);
                if (npcs.TryGetValue(targetConnectionId, out var healingNpcTarget) && isHealing)
                    return TryApplyNpcHealingLocked(attacker, healingNpcTarget, targetConnectionId, skill, out outcome);
                if (npcs.TryGetValue(targetConnectionId, out var detoxNpcTarget) && isDetox)
                    return TryApplyNpcDetoxLocked(attacker, detoxNpcTarget, targetConnectionId, out outcome);
                if (npcs.TryGetValue(targetConnectionId, out var effectNpcTarget) && isEffectOnly)
                    return TryApplyNpcEffectSkillAttackLocked(attacker, effectNpcTarget, targetConnectionId, skill, affectRandomFactor, out outcome);
                if (npcs.TryGetValue(targetConnectionId, out var npcTarget) && !isSupport && !isBeneficial)
                    return TryApplyNpcSkillAttackLocked(attacker, npcTarget, targetConnectionId, skill, itemData!, weather, randomFactor, parryRandomRoll, affectRandomFactor, specialDropRoll, specialItemRoll, eventDropRoll, runeRoll, runeChanceRoll, pistaRandomRoll, out outcome);
                return LegacySkillAttackResult.ParticipantNotFound;
            }
            if (!isBeneficial && attackerConnectionId == targetConnectionId)
                return LegacySkillAttackResult.SameParticipant;
            if (attacker.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize || target.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize)
                return LegacySkillAttackResult.CombatStateUnavailable;
            if (skill.Aggressive != 0 && IsPeacefulPlayerTargetBlocked(attacker, target, aggressive: true))
                return LegacySkillAttackResult.PeacefulZoneBlocked;
            if (!isBeneficial && attacker.GuildId != 0 && attacker.GuildId == target.GuildId)
                return LegacySkillAttackResult.FriendlyFireBlocked;

            var attackerState = LegacyMobCombatState.Read(attacker.Mob);
            var targetState = LegacyMobCombatState.Read(target.Mob);
            if (!isResurrection && attackerState.CurrentScore.Hp <= 0)
                return LegacySkillAttackResult.AttackerNotAlive;
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

                // The first successful branch sets HP=2 and recalls the player. The
                // legacy handler then always applies the final 1..50% HP/MP rolls.
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
                var maxMpPercent = Math.Max(0, (attackerState.CurrentScore.MaxMp + 1) / 100);
                var hpRoll = resurrectionHpRoll >= 0
                    ? resurrectionHpRoll
                    : System.Security.Cryptography.RandomNumberGenerator.GetInt32(50);
                var mpRoll = resurrectionMpRoll >= 0
                    ? resurrectionMpRoll
                    : System.Security.Cryptography.RandomNumberGenerator.GetInt32(50);
                var revivedHp = (Math.Clamp(hpRoll, 0, 49) + 1) * maxHpPercent;
                var revivedMp = (Math.Clamp(mpRoll, 0, 49) + 1) * maxMpPercent;
                BinaryPrimitives.WriteInt32LittleEndian(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), revivedHp);
                BinaryPrimitives.WriteInt32LittleEndian(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 28), revivedMp);
                attacker.RequestedMana = revivedMp;
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

                if (targetState.CurrentScore.Hp > attackerState.CurrentScore.Hp + LegacySkillCombatMath.GetSkillSpecial(skill, attackerState) + 30)
                    return LegacySkillAttackResult.TargetTooHighToSummon;

                if ((target.PositionX & 0xFF00) == 0 && (target.PositionY & 0xFF00) == 0)
                    return LegacySkillAttackResult.SummonInvalidPosition;

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
                var summonResult = TryGenerateSummons(
                    attackerConnectionId,
                    skill.InstanceValue - 1,
                    requestedSummons,
                    attacker,
                    attackerState,
                    out var createdSummons);
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
                    AttackerResourceChanged: refunded);
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
                    BinaryPrimitives.WriteInt32LittleEndian(target.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentMpOffset), remainingMana);
                    target.RequestedMana = remainingMana;
                    outcome = new LegacySkillAttackOutcome(
                        targetConnectionId,
                        0,
                        0,
                        0,
                        targetState.CurrentScore.Hp,
                        target.Mob.ToArray(),
                        TargetResourceChanged: true);
                    return LegacySkillAttackResult.Accepted;
                }

                var affectChanged = false;
                for (var slot = 0; slot < LegacyAccountSnapshot.AffectStride / 8; slot++)
                {
                    var offset = slot * 8;
                    if (target.Affect[offset] is 18 or 16 or 14 or 19)
                    {
                        target.Affect.AsSpan(offset, 8).Clear();
                        affectChanged = true;
                    }
                }

                outcome = new LegacySkillAttackOutcome(
                    targetConnectionId,
                    0,
                    0,
                    0,
                    targetState.CurrentScore.Hp,
                    target.Mob.ToArray(),
                    affectChanged ? target.Affect.ToArray() : null);
                return LegacySkillAttackResult.Accepted;
            }

            if (skill.Id != 79 && skill.InstanceType is not (>= 1 and <= 5) and not 6 and not 8 and not 0 and not 12)
                return LegacySkillAttackResult.UnsupportedSkillType;

            if (isHealing)
            {
                if (target.Clan == 4)
                    return LegacySkillAttackResult.TargetClanBlocked;

                var healing = skill.Id == 27
                    ? (LegacySkillCombatMath.GetSkillSpecial(skill, attackerState) * 2) + skill.InstanceValue
                    : (LegacySkillCombatMath.GetSkillSpecial(skill, attackerState) * 3 / 2) + skill.InstanceValue;
                if (attacker.ClassMaster != LegacyAccountSnapshot.ClassMasterMortal && attacker.ClassMaster != LegacyAccountSnapshot.ClassMasterArch)
                    healing *= 2;
                healing = Math.Min(healing, attacker.ClassMaster != LegacyAccountSnapshot.ClassMasterMortal && attacker.ClassMaster != LegacyAccountSnapshot.ClassMasterArch ? 2200 : 1100);
                healing = Math.Max(6, healing);

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
                outcome = new LegacySkillAttackOutcome(targetConnectionId, healing, -appliedHealing, 0, healingRemainingHp, target.Mob.ToArray(), TargetHpChanged: appliedHealing > 0);
                return LegacySkillAttackResult.Accepted;
            }

            if (isDetox)
            {
                var changed = false;
                for (var slot = 0; slot < LegacyAccountSnapshot.AffectStride / 8; slot++)
                {
                    var offset = slot * 8;
                    var type = target.Affect[offset];
                    if (type is 1 or 3 or 5 or 7 or 10 or 12 or 20 || (type == 32 && (attackerState.LearnedSkill & (1u << 7)) != 0))
                    {
                        target.Affect.AsSpan(offset, 8).Clear();
                        changed = true;
                    }
                }

                outcome = new LegacySkillAttackOutcome(targetConnectionId, 0, 0, 0, targetState.CurrentScore.Hp, target.Mob.ToArray(), changed ? target.Affect.ToArray() : null);
                return LegacySkillAttackResult.Accepted;
            }

            if (isEffectOnly)
            {
                var effectChanged = CanApplyLegacyAffect(attacker, target, attackerState, targetState, skill, affectRandomFactor)
                    && TryApplyLegacyAffects(target.Affect, skill, LegacySkillCombatMath.GetSkillSpecial(skill, attackerState));
                outcome = new LegacySkillAttackOutcome(targetConnectionId, 0, 0, 0, targetState.CurrentScore.Hp, target.Mob.ToArray(), effectChanged ? target.Affect.ToArray() : null);
                return LegacySkillAttackResult.Accepted;
            }

            var firstWeapon = LegacyItem.Read(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (6 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
            var secondWeapon = LegacyItem.Read(attacker.Mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (7 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
            var weaponDamage = LegacySkillCombatMath.GetWeaponDamage(attackerState, itemData!, firstWeapon, secondWeapon);
            var baseDamage = LegacySkillCombatMath.GetSkillBaseDamage(skill, attackerState, weather, weaponDamage, attackerState.Magic);
            var combat = LegacySkillCombatMath.GetMasterCombat(attackerState);
            var effectiveRandomFactor = randomFactor >= 0
                ? randomFactor
                : System.Security.Cryptography.RandomNumberGenerator.GetInt32(combat + 90, combat + 90 + (21 - Math.Min(combat, 15)));

            var defense = targetState.CurrentScore.Ac * (skill.Id == 79 ? 3 : 2);
            if (skill.Id != 79 && targetState.CharacterClass == 1)
                defense = (defense * 3) / 2;

            var damage = LegacySkillCombatMath.GetSkillDamage(baseDamage, defense, combat, effectiveRandomFactor);
            var resistance = 0;
            if (skill.Id == 79)
            {
                damage /= 2;
            }
            else
            {
                var resistanceIndex = skill.InstanceType == 1 ? 0 : skill.InstanceType - 2;
                resistance = unchecked((sbyte)target.Mob[LegacyAccountSnapshot.MobResistOffset + resistanceIndex]);
                damage = ((150 - resistance) * damage) / 100;
            }

            damage = ResolveLegacyParry(target.Mob, attackerState, targetState, itemData, skill.Id, parryRandomRoll, damage);

            var remainingHp = damage <= 0 ? targetState.CurrentScore.Hp : Math.Max(0, targetState.CurrentScore.Hp - damage);
            BinaryPrimitives.WriteInt32LittleEndian(target.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), remainingHp);
            var targetRevived = false;
            var consumedItemSlot = -1;
            LegacyItem? consumedItem = null;
            if (remainingHp == 0 && TryConsumeResurrectionScroll(target, out consumedItemSlot, out var revivedItem))
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
            var combatAffectChanged = CanApplyLegacyAffect(attacker, target, attackerState, targetState, skill, affectRandomFactor)
                && TryApplyLegacyAffects(target.Affect, skill, LegacySkillCombatMath.GetSkillSpecial(skill, attackerState));
            outcome = new LegacySkillAttackOutcome(targetConnectionId, baseDamage, damage, resistance, remainingHp, target.Mob.ToArray(), combatAffectChanged ? target.Affect.ToArray() : null, TargetResourceChanged: targetRevived, TargetRevived: targetRevived, ConsumedItemSlot: consumedItemSlot, ConsumedItem: consumedItem, TargetHpChanged: damage > 0 || targetRevived);
            return LegacySkillAttackResult.Accepted;
        }
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

        var healing = skill.Id == 27
            ? (LegacySkillCombatMath.GetSkillSpecial(skill, attackerState) * 2) + skill.InstanceValue
            : (LegacySkillCombatMath.GetSkillSpecial(skill, attackerState) * 3 / 2) + skill.InstanceValue;
        if (attacker.ClassMaster != LegacyAccountSnapshot.ClassMasterMortal && attacker.ClassMaster != LegacyAccountSnapshot.ClassMasterArch)
            healing *= 2;
        healing = Math.Min(healing, attacker.ClassMaster != LegacyAccountSnapshot.ClassMasterMortal && attacker.ClassMaster != LegacyAccountSnapshot.ClassMasterArch ? 2200 : 1100);
        healing = Math.Max(6, healing);

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
        outcome = new LegacySkillAttackOutcome(targetConnectionId, healing, -appliedHealing, 0, remainingHp, targetMob, TargetHpChanged: appliedHealing > 0);
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

        if (skill.Aggressive != 0 && (targetState.Rsv & 0x80) != 0)
        {
            outcome = new LegacySkillAttackOutcome(targetConnectionId, 0, 0, 0, targetState.CurrentScore.Hp, targetMob);
            return LegacySkillAttackResult.Accepted;
        }

        var effectChanged = false;
        if (skill.TickType > 0 && targetState.CurrentScore.Merchant != 1)
        {
            if (skill.AffectResist is >= 1 and <= 4)
            {
                var levelDifference = (attackerState.CurrentScore.Level - targetState.CurrentScore.Level) / 2;
                var resistanceLimit = targetState.RegenMp + skill.AffectResist + levelDifference;
                var random = affectRandomFactor >= 0
                    ? affectRandomFactor
                    : System.Security.Cryptography.RandomNumberGenerator.GetInt32(100);
                if (random <= resistanceLimit)
                    effectChanged = TrySetLegacyAffect(targetAffect, skill.TickType, skill.TickValue, skill.AffectTime, LegacySkillCombatMath.GetSkillSpecial(skill, attackerState) + 100, LegacySkillCombatMath.GetSkillSpecial(skill, attackerState), tick: true);
            }
            else
            {
                var level = LegacySkillCombatMath.GetSkillSpecial(skill, attackerState);
                effectChanged = TrySetLegacyAffect(targetAffect, skill.TickType, skill.TickValue, skill.AffectTime, level + 100, level, tick: true);
            }
        }

        npcs[targetConnectionId] = npc with { AffectSnapshot = targetAffect };
        outcome = new LegacySkillAttackOutcome(targetConnectionId, 0, 0, 0, targetState.CurrentScore.Hp, targetMob, effectChanged ? targetAffect : null);
        return LegacySkillAttackResult.Accepted;
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

    private LegacySkillAttackResult TryApplyNpcEtherealFlameLocked(
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
            if (targetAffect[offset] is 18 or 16 or 14 or 19)
            {
                targetAffect.AsSpan(offset, 8).Clear();
                changed = true;
            }
        }

        npcs[targetConnectionId] = npc with { AffectSnapshot = targetAffect };
        outcome = new LegacySkillAttackOutcome(targetConnectionId, 0, 0, 0, targetState.CurrentScore.Hp, targetMob, changed ? targetAffect : null);
        return LegacySkillAttackResult.Accepted;
    }

    private LegacySkillAttackResult TryApplyNpcSkillAttackLocked(
        Participant attacker,
        LegacyWorldNpc npc,
        int targetConnectionId,
        LegacySkillDefinition skill,
        LegacyItemDataTable itemData,
        int weather,
        int randomFactor,
        int parryRandomRoll,
        int affectRandomFactor,
        int specialDropRoll,
        int specialItemRoll,
        int eventDropRoll,
        int runeRoll,
        int runeChanceRoll,
        int pistaRandomRoll,
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
        var baseDamage = LegacySkillCombatMath.GetSkillBaseDamage(skill, attackerState, weather, weaponDamage, attackerState.Magic);
        var combat = LegacySkillCombatMath.GetMasterCombat(attackerState);
        var effectiveRandomFactor = randomFactor >= 0
            ? randomFactor
            : System.Security.Cryptography.RandomNumberGenerator.GetInt32(combat + 90, combat + 90 + (21 - Math.Min(combat, 15)));

        var defense = targetState.CurrentScore.Ac;
        if (skill.Id == 79)
        {
            var physicalCombat = Math.Min(combat / 2, 7);
            var physicalRandomFactor = randomFactor >= 0
                ? randomFactor
                : System.Security.Cryptography.RandomNumberGenerator.GetInt32(physicalCombat + 99, physicalCombat + 99 + (12 - physicalCombat));
            var damage = LegacySkillCombatMath.GetPhysicalDamage(baseDamage, defense, combat, physicalRandomFactor) / 2;
            damage = ResolveLegacyParry(targetMob, attackerState, targetState, itemData, skill.Id, parryRandomRoll, damage);
            return FinishNpcSkillAttackLocked(attacker, npc, targetConnectionId, attackerState, targetState, targetMob, baseDamage, damage, 0, specialDropRoll, specialItemRoll, eventDropRoll, runeRoll, runeChanceRoll, pistaRandomRoll, out outcome);
        }

        if (targetState.CharacterClass == 1)
            defense = (defense * 3) / 2;
        var skillDamage = LegacySkillCombatMath.GetSkillDamage(baseDamage, defense, combat, effectiveRandomFactor);
        var resistanceIndex = skill.InstanceType - 2;
        var resistance = 0;
        if (skill.InstanceType == 1)
            resistance = unchecked((sbyte)targetMob[LegacyAccountSnapshot.MobResistOffset]) / 2;
        else if (skill.InstanceType is >= 2 and <= 5)
            resistance = unchecked((sbyte)targetMob[LegacyAccountSnapshot.MobResistOffset + resistanceIndex]) / 2;
        skillDamage = ((150 - resistance) * skillDamage) / 100;
        skillDamage = ResolveLegacyParry(targetMob, attackerState, targetState, itemData, skill.Id, parryRandomRoll, skillDamage);
        return FinishNpcSkillAttackLocked(attacker, npc, targetConnectionId, attackerState, targetState, targetMob, baseDamage, skillDamage, resistance, specialDropRoll, specialItemRoll, eventDropRoll, runeRoll, runeChanceRoll, pistaRandomRoll, out outcome);
    }

    private LegacySkillAttackResult FinishNpcSkillAttackLocked(
        Participant attacker,
        LegacyWorldNpc npc,
        int targetConnectionId,
        LegacyMobCombatState attackerState,
        LegacyMobCombatState targetState,
        byte[] targetMob,
        int baseDamage,
        int damage,
        int resistance,
        int specialDropRoll,
        int specialItemRoll,
        int eventDropRoll,
        int runeRoll,
        int runeChanceRoll,
        int pistaRandomRoll,
        out LegacySkillAttackOutcome? outcome)
    {
        var remainingHp = damage <= 0 ? targetState.CurrentScore.Hp : Math.Max(0, targetState.CurrentScore.Hp - damage);
        BinaryPrimitives.WriteInt32LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), remainingHp);
        var targetRemoved = remainingHp == 0 && RemoveNpcLocked(targetConnectionId);
        if (!targetRemoved)
            npcs[targetConnectionId] = npc with { MobSnapshot = targetMob };

        long experienceAwarded = 0;
        byte[]? attackerMobSnapshot = null;
        IReadOnlyList<LegacyExperienceAward> experienceAwards = [];
        IReadOnlyList<LegacyItemDrop> itemDrops = [];
        IReadOnlyList<LegacyCoinUpdate> coinUpdates = [];
        IReadOnlyList<LegacyPrivateNotice> privateNotices = [];
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
                itemDrops = dropResult.ItemDrops;
                globalNotices = dropResult.GlobalNotices;
                if (itemDrops.Count > 0)
                    attackerMobSnapshot = attacker.Mob.ToArray();
                var castleReward = AwardCastleQuestLocked(attacker, npc);
                if (castleReward is not null)
                {
                    itemDrops = [.. itemDrops, .. castleReward.ItemDrops];
                    experienceAwards = [.. experienceAwards, .. castleReward.ExperienceAwards];
                    coinUpdates = castleReward.CoinUpdates;
                    privateNotices = castleReward.PrivateNotices;
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
            ExperienceAwarded: experienceAwarded,
            AttackerMobSnapshot: attackerMobSnapshot,
            ExperienceAwards: experienceAwards,
            ItemDrops: itemDrops,
            PistaTransition: pistaTransition,
            GlobalNotices: globalNotices,
            AreaNotices: areaNotices,
            CoinUpdates: coinUpdates,
            PrivateNotices: privateNotices);
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
                npcs.Remove(roomNpc.ConnectionId);

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

        if (npc.GenerateIndex is >= 5775 and <= 5785 && pistaLv6State is not null)
        {
            var leaderConnectionId = partyLeaders.TryGetValue(attacker.ConnectionId, out var configuredLeader)
                ? configuredLeader
                : attacker.ConnectionId;
            if (leaderConnectionId != pistaLv6State.LeaderConnectionId)
                return null;

            var previous = pistaLv6State.MobCount;
            var remaining = previous > 0 ? previous - 1 : 0;
            var spawn = previous == 1 ? 5767 : 0;
            pistaLv6State = pistaLv6State with { MobCount = remaining };
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

        var dropBonus = LegacyExperienceMath.GetEquipmentDropBonus(attacker.Mob, itemData);
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
        carrySlot = -1;
        for (var slot = 0; slot < LegacyAccountSnapshot.MobCarryCount; slot++)
        {
            var offset = LegacyAccountSnapshot.MobCarryOffset + (slot * LegacyItem.SizeInBytes);
            if (LegacyItem.Read(participant.Mob.AsSpan(offset, LegacyItem.SizeInBytes)).Index == 0)
            {
                carrySlot = slot;
                item.Write(participant.Mob.AsSpan(offset, LegacyItem.SizeInBytes));
                return true;
            }
        }

        return false;
    }

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

    private static int GetLegacyHpAbs(ReadOnlySpan<byte> affect)
    {
        var hpAbs = 0;
        for (var slot = 0; slot < affect.Length / 8; slot++)
        {
            var offset = slot * 8;
            if (affect[offset] == 8 && (BinaryPrimitives.ReadUInt16LittleEndian(affect.Slice(offset + 2, 2)) & (1 << 3)) != 0)
                hpAbs += 20;
        }

        return hpAbs;
    }

    private static int GetLegacyForceDamage(ReadOnlySpan<byte> mob, LegacyItemDataTable? itemData)
    {
        if (itemData is null || mob.Length < LegacyAccountSnapshot.MobEquipmentOffset + (LegacyCharacterSelection.EquipmentCount * LegacyItem.SizeInBytes))
            return 0;

        var forceDamage = 0;
        for (var slot = 0; slot < LegacyCharacterSelection.EquipmentCount; slot++)
        {
            var item = LegacyItem.Read(mob.Slice(LegacyAccountSnapshot.MobEquipmentOffset + (slot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
            if (item.Index <= 0 || item.Index >= LegacyItemDataTable.MaxItemIndex)
                continue;

            var grade = itemData.GetItemGrade(item);
            var sanctuary = itemData.GetItemSanctuary(item);
            var sanctuaryLevel = sanctuary switch
            {
                10 => 1,
                11 => 2,
                12 => 3,
                13 => 4,
                14 => 5,
                15 => 6,
                _ => 0,
            };
            if (itemData.GetItemGem(item) == 1)
                forceDamage += (grade == 6 ? 80 : 40) * sanctuaryLevel;

            // The C++ loop tests i == 20 although equipment has only 16 slots;
            // preserve that no-op branch explicitly rather than correcting it.
            if (grade == 6 && slot == 20)
                forceDamage++;
        }

        return forceDamage;
    }

    private static int GetLegacyPvpDamage(ReadOnlySpan<byte> mob, LegacyItemDataTable? itemData)
    {
        if (itemData is null || mob.Length < LegacyAccountSnapshot.MobEquipmentOffset + (LegacyCharacterSelection.EquipmentCount * LegacyItem.SizeInBytes))
            return 0;

        var attackPvp = 0;
        for (var slot = 0; slot < LegacyCharacterSelection.EquipmentCount; slot++)
        {
            var item = LegacyItem.Read(mob.Slice(LegacyAccountSnapshot.MobEquipmentOffset + (slot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
            attackPvp += itemData.GetItemAbility(item, LegacyItemEffect.PvpAttack);
        }

        return (attackPvp + 1) / 10;
    }

    private static int GetLegacyReflectDamage(ReadOnlySpan<byte> mob, LegacyMobCombatState state, LegacyItemDataTable? itemData)
    {
        var reflectDamage = state.CharacterClass == 2 && (state.LearnedSkill & (1u << 17)) != 0
            ? (state.CurrentScore.Special4 + 1) / 6
            : 0;
        if (itemData is null || mob.Length < LegacyAccountSnapshot.MobEquipmentOffset + (LegacyCharacterSelection.EquipmentCount * LegacyItem.SizeInBytes))
            return reflectDamage;

        for (var slot = 0; slot < LegacyCharacterSelection.EquipmentCount; slot++)
        {
            var item = LegacyItem.Read(mob.Slice(LegacyAccountSnapshot.MobEquipmentOffset + (slot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
            var grade = itemData.GetItemGrade(item);
            if (grade == 8)
                reflectDamage += 20;

            if (itemData.GetItemGem(item) == 3)
            {
                var sanctuaryLevel = itemData.GetItemSanctuary(item) switch
                {
                    10 => 1,
                    12 => 2,
                    15 => 3,
                    18 => 4,
                    22 => 5,
                    27 => 6,
                    _ => 0,
                };
                reflectDamage += (grade == 8 ? 80 : 40) * sanctuaryLevel;
            }
        }

        return reflectDamage;
    }

    private static int GetLegacyReflectPvp(ReadOnlySpan<byte> mob, LegacyItemDataTable? itemData)
    {
        if (itemData is null || mob.Length < LegacyAccountSnapshot.MobEquipmentOffset + (LegacyCharacterSelection.EquipmentCount * LegacyItem.SizeInBytes))
            return 0;

        var reflectPvp = 0;
        for (var slot = 0; slot < LegacyCharacterSelection.EquipmentCount; slot++)
        {
            var item = LegacyItem.Read(mob.Slice(LegacyAccountSnapshot.MobEquipmentOffset + (slot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
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

    private static int ResolveLegacyParry(
        ReadOnlySpan<byte> targetMob,
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
        for (var slot = 0; slot < LegacyCharacterSelection.EquipmentCount; slot++)
        {
            var item = LegacyItem.Read(targetMob.Slice(LegacyAccountSnapshot.MobEquipmentOffset + (slot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
            if (item.Index != 0 || slot == 7)
                targetParry += itemData.GetItemAbility(item, LegacyItemEffect.Parry);
        }

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
        var cityId = (state.CurrentScore.Merchant & 0xC0) >> 6;
        var zone = LegacyRecallData.Zones[Math.Clamp(cityId, 0, LegacyRecallData.Zones.Length - 1)];
        var x = zone.CitySpawnX + (cityRandomX >= 0 ? Math.Clamp(cityRandomX, 0, 14) : System.Security.Cryptography.RandomNumberGenerator.GetInt32(15));
        var y = zone.CitySpawnY + (cityRandomY >= 0 ? Math.Clamp(cityRandomY, 0, 14) : System.Security.Cryptography.RandomNumberGenerator.GetInt32(15));

        for (var zoneIndex = 0; zoneIndex < LegacyRecallData.Zones.Length; zoneIndex++)
        {
            var guildZone = LegacyRecallData.Zones[zoneIndex];
            if (participant.GuildId > 0 && participant.GuildId == guildZones?.GetChargeGuild(zoneIndex))
            {
                x = guildZone.GuildSpawnX;
                y = guildZone.GuildSpawnY;
                break;
            }
        }

        if (participant.ClassMaster == LegacyAccountSnapshot.ClassMasterMortal && state.CurrentScore.Level < LegacyRecallData.LegacyFreeExp)
        {
            x = 2100 + (newbieRandomX >= 0 ? Math.Clamp(newbieRandomX, 0, 4) : System.Security.Cryptography.RandomNumberGenerator.GetInt32(5)) - 2;
            y = 2100 + (newbieRandomY >= 0 ? Math.Clamp(newbieRandomY, 0, 4) : System.Security.Cryptography.RandomNumberGenerator.GetInt32(5)) - 2;
        }

        var gridX = x;
        var gridY = y;
        Func<int, int, bool> blockedAt = mapGrid is null ? static (_, _) => false : mapGrid.IsBlocked;
        LegacyMobGridSearch.TryFindEmpty(
            participant.ConnectionId,
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
            if (participant.PositionX == x && participant.PositionY == y)
                return participant.ConnectionId;

        foreach (var summon in summons.Values)
            if (summon.PositionX == x && summon.PositionY == y)
                return summon.ConnectionId;

        foreach (var npc in npcs.Values)
            if (npc.PositionX == x && npc.PositionY == y)
                return npc.ConnectionId;

        return 0;
    }

    private bool RemoveNpcLocked(int npcConnectionId) => npcs.Remove(npcConnectionId);

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

        var attackerExperienceBonus = LegacyExperienceMath.GetEquipmentExperienceBonus(attacker.Mob, attacker.Affect, itemData);
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
                    combatWorldState.DoubleMode,
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
        if (participants.TryGetValue(summon.LeaderConnectionId, out var leader))
            leader.SummonIds.Remove(summonConnectionId);
        return true;
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
        int summonId,
        int requestedCount,
        Participant leader,
        LegacyMobCombatState leaderState,
        out IReadOnlyList<LegacySummonedMob> created)
    {
        var createdSummons = new List<LegacySummonedMob>();
        created = createdSummons;
        if (summonId < 0 || summonId >= LegacySummonCatalog.SlotCount)
            return LegacySummonGenerationResult.InvalidSummonId;
        if (requestedCount <= 0)
            return LegacySummonGenerationResult.Created;
        if (summonCatalog is null || !summonCatalog.TryGet(summonId, out var template) || template is null)
            return LegacySummonGenerationResult.CatalogUnavailable;

        var requestedFace = ReadEquipmentIndex(template.MobSnapshot, equipmentSlot: 0);
        var sameTypeCount = 0;
        foreach (var summonConnectionId in leader.SummonIds)
        {
            if (!summons.TryGetValue(summonConnectionId, out var existing)) continue;
            var existingFace = ReadEquipmentIndex(existing.MobSnapshot, equipmentSlot: 0);
            if (existingFace != requestedFace)
                return LegacySummonGenerationResult.TypeConflict;
            sameTypeCount++;
        }

        if (sameTypeCount >= requestedCount)
            return LegacySummonGenerationResult.AlreadyPresent;
        var createCount = requestedCount - sameTypeCount;
        if (leader.SummonIds.Count + createCount > 12)
            return LegacySummonGenerationResult.PartyFull;

        var bonus = summonCatalog.GetBonus(summonId);
        for (var index = 0; index < createCount; index++)
        {
            var connectionId = AllocateSummonConnectionId();
            var mob = template.MobSnapshot.ToArray();
            ApplySummonMobState(mob, template.SourceName, leaderState, bonus);
            var affect = new byte[LegacyAccountSnapshot.AffectStride];
            affect[0] = 24;
            BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), summonId is >= 28 and <= 37 ? 2_000_000_000u : 20u);

            var positionX = (int)leader.PositionX;
            var positionY = (int)leader.PositionY;
            var foundPosition = LegacyMobGridSearch.TryFindEmpty(
                connectionId,
                ref positionX,
                ref positionY,
                GetParticipantAtPosition,
                mapGrid is null ? static (_, _) => false : mapGrid.IsBlocked,
                checkCandidateTerrain: mapCollisionMode == LegacyMapCollisionMode.CandidateAware);
            if (!foundPosition)
                return LegacySummonGenerationResult.PositionUnavailable;

            var summoned = new LegacySummonedMob(connectionId, leaderConnectionId, leaderConnectionId, (short)positionX, (short)positionY, mob, affect);
            summons.Add(connectionId, summoned);
            summonTargets[connectionId] = 0;
            leader.SummonIds.Add(connectionId);
            createdSummons.Add(summoned);
        }

        return LegacySummonGenerationResult.Created;
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

    private bool IsPeacefulPlayerTargetBlocked(Participant attacker, Participant target, bool aggressive)
    {
        if (!aggressive || combatWorldState.RvrWarActive || combatWorldState.RvrState != 0 || combatWorldState.CastleState != 0 || combatWorldState.GuildTowerState != 0 || combatWorldState.NewbieEventServer)
            return false;

        return (attacker.MapAttribute & 0x40) != 0 && !target.PkMode && !target.Guilty;
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

    private static bool TryApplyLegacyAffects(byte[] affect, LegacySkillDefinition? skill, int delay, int level)
    {
        if (skill is null) return false;
        var changed = TrySetLegacyAffect(affect, skill.AffectType, skill.AffectValue, skill.AffectTime, delay, level);
        changed |= TrySetLegacyAffect(affect, skill.TickType, skill.TickValue, skill.AffectTime, delay, level, tick: true);
        return changed;
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
        affect[offset] = checked((byte)type);
        affect[offset + 1] = checked((byte)value);
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(offset + 2), checked((ushort)Math.Clamp(level, 0, ushort.MaxValue)));
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(offset + 4), checked((uint)time));
        return true;
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
        lock (gate)
        {
            if (!participants.TryGetValue(plan.SourceConnectionId, out var source) || !participants.TryGetValue(plan.TargetConnectionId, out var target)) return false;
            if (source.GuildId != plan.GuildId || source.Coin != plan.SourceCoin || target.GuildId != 0) return false;
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

    public bool TryBuildCreateMobFrame(int connectionId, LegacyFrameCodec codec, uint clientTick, byte keywordIndex, out byte[]? frame)
    {
        ArgumentNullException.ThrowIfNull(codec);
        lock (gate)
        {
            frame = null;
            if (!participants.TryGetValue(connectionId, out var participant) || participant.Mob.Length < WorldHubLegacyOffsets.LegacyAccountMobSize) return false;
            frame = new CreateMobConfirmation((ushort)connectionId, participant.PositionX, participant.PositionY, participant.Mob).ToFrame(codec, clientTick, keywordIndex);
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
            frame = new UpdateEtcConfirmation(participant.Mob, participant.MobExtra).ToFrame(codec, clientTick, keywordIndex, (ushort)connectionId);
            return true;
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
        public int Clan { get; set; }
        public int GuildLevel { get; set; }
        public int Coin { get; set; }
        public int Donate { get; set; }
        public short PositionX { get; set; }
        public short PositionY { get; set; }
        public bool PkMode { get; set; }
        public bool Guilty { get; set; }
        public byte MapAttribute { get; set; }
        public bool HasMapAttribute { get; set; }
        public byte[] Mob { get; set; } = [];
        public short ClassMaster { get; set; } = LegacyAccountSnapshot.ClassMasterMortal;
        public int ExperienceSegment { get; set; }
        public byte[] Affect { get; set; } = new byte[LegacyAccountSnapshot.AffectStride];
        public byte[] MobExtra { get; set; } = new byte[LegacyAccountSnapshot.MobExtraStride];
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
    OutOfRange,
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
    IReadOnlyList<LegacyPrivateNotice>? PrivateNotices = null);

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
    OutOfRange,
    SummonNotAllowedHere,
    TargetTooHighToSummon,
    SummonInvalidPosition,
    UnsupportedSkillType,
}

public sealed record LegacyCombatWorldState(
    bool RvrWarActive = false,
    int RvrState = 0,
    int CastleState = 0,
    int GuildTowerState = 0,
    bool NewbieEventServer = false,
    bool DoubleMode = false,
    bool KefraLive = true,
    int Kingdom1Clear = 0,
    int Kingdom2Clear = 0,
    int CastleQuestClear = 0);

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
    IReadOnlyList<LegacyNpcCombatStateUpdate>? NpcCombatStateUpdates = null);

public sealed record LegacyNpcCombatStateUpdate(
    int ConnectionId,
    int Mode,
    int CurrentTarget,
    IReadOnlyList<int> EnemyList);

public sealed record LegacyExperienceAward(int ConnectionId, int Experience, byte[] MobSnapshot);

public sealed record LegacyItemDrop(int ConnectionId, int InventorySlot, LegacyItem Item);

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

internal sealed record LegacyNpcDropResult(IReadOnlyList<LegacyItemDrop> ItemDrops, IReadOnlyList<LegacyGlobalNotice> GlobalNotices);

internal sealed record LegacyCastleQuestRewardResult(
    IReadOnlyList<LegacyItemDrop> ItemDrops,
    IReadOnlyList<LegacyExperienceAward> ExperienceAwards,
    IReadOnlyList<LegacyCoinUpdate> CoinUpdates,
    IReadOnlyList<LegacyPrivateNotice> PrivateNotices,
    byte[]? AttackerMobSnapshot);

/// <summary>Authoritative progress of the current Pista level 6 room.</summary>
public sealed record LegacyPistaLv6State(int LeaderConnectionId, int MobCount);

public sealed record LegacyPistaMobLeftFrame(int ConnectionId, byte[] Frame);

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

public sealed record PartyInvitePlan(int LeaderConnectionId, int TargetConnectionId, PartyMemberSnapshot Leader, PartyMemberSnapshot Target);

public sealed record PartyFormationPlan(int LeaderConnectionId, IReadOnlyList<PartyMemberSnapshot> Members);

public sealed record PartyRemovalPlan(int LeaderConnectionId, int RemovedConnectionId, IReadOnlyList<int> NotifiedConnectionIds, bool Disbanded);

public enum PartyInviteCheckResult
{
    Accepted,
    ParticipantNotFound,
    SourceIsPartyMember,
    TargetAlreadyInParty,
    PartyFull,
    LevelLimit,
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
    byte[] AffectSnapshot);

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
    int? TerrainHeight = null)
{
    public IReadOnlyList<int> EnemyList { get; init; } = initialEnemyList ?? [];
}

public static class LegacyNpcMode
{
    public const int EmptyTarget = 0;
    public const int Combat = 1;
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

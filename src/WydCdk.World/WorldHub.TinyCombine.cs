using System.Buffers.Binary;
using WydCdk.Protocol;

namespace WydCdk.World;

public sealed partial class WorldHub
{
    private const long LegacyTinyCombineGuildTaxExperience = 10_000_000L;
    private const long LegacyGuildTreasuryExperienceLimit = 200_000_000_000L;
    private readonly Dictionary<int, int> guildImpostoIds = [];

    public IReadOnlyList<int> GetGuildTreasuryNpcConnectionIds()
    {
        lock (gate)
            return guildImpostoIds.Values
                .Where(npcs.ContainsKey)
                .Distinct()
                .Order()
                .ToArray();
    }

    public bool TryGetRegisteredGuildTreasuryNpcSnapshot(int connectionId, out LegacyWorldNpc? npc)
    {
        lock (gate)
        {
            if (guildImpostoIds.ContainsValue(connectionId) && npcs.TryGetValue(connectionId, out var current))
            {
                npc = current;
                return true;
            }

            npc = null;
            return false;
        }
    }

    private void ForgetGuildTreasuryNpcRegistrationLocked(int connectionId)
    {
        foreach (var village in guildImpostoIds
                     .Where(pair => pair.Value == connectionId)
                     .Select(static pair => pair.Key)
                     .ToArray())
            guildImpostoIds.Remove(village);
    }

    /// <summary>
    /// Validates a Tiny request against the current character carry while holding
    /// the world lock and returns a plan without changing the participant.
    /// </summary>
    public LegacyTinyCombinePreparationResult TryPrepareTinyCombine(
        int connectionId,
        IReadOnlyList<LegacyItem> requestedItems,
        IReadOnlyList<sbyte> inventoryPositions,
        int tinyBase,
        out LegacyTinyCombinePlan? plan)
    {
        ArgumentNullException.ThrowIfNull(requestedItems);
        ArgumentNullException.ThrowIfNull(inventoryPositions);

        plan = null;
        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyTinyCombinePreparationResult.ParticipantNotFound;
            if (!HasCarryState(participant))
                return LegacyTinyCombinePreparationResult.CarryUnavailable;

            var authoritativeCarry = new LegacyItem[LegacyAccountSnapshot.MobCarryCount];
            for (var slot = 0; slot < authoritativeCarry.Length; slot++)
                authoritativeCarry[slot] = ReadCarryItem(participant, slot);

            return LegacyTinyCombinePlanBuilder.TryBuild(
                new LegacyTinyCombinePreparationContext(
                    connectionId,
                    requestedItems,
                    inventoryPositions,
                    authoritativeCarry,
                    GetLegacyMaxCarry(participant),
                    participant.Coin,
                    itemData,
                    tinyBase,
                    participant.Mob),
                out plan);
        }
    }

    /// <summary>
    /// Applies a prepared Tiny result only while the complete authoritative MOB
    /// and Gold balance still match the snapshot used to build it.
    /// </summary>
    public LegacyTinyCombineApplicationResult TryApplyTinyCombine(
        int connectionId,
        LegacyTinyCombinePlan? plan,
        int rawRoll,
        bool localServer,
        out LegacyTinyCombineOutcome? outcome)
    {
        outcome = null;
        if (plan is null || plan.ConnectionId != connectionId)
            return LegacyTinyCombineApplicationResult.InvalidPlan;

        lock (gate)
        {
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyTinyCombineApplicationResult.ParticipantNotFound;

            if (participant.Coin != plan.CoinBefore ||
                !participant.Mob.AsSpan().SequenceEqual(plan.ExpectedMob.Span))
                return LegacyTinyCombineApplicationResult.StateChanged;

            var buildResult = LegacyTinyCombineOutcomeBuilder.TryBuild(
                plan,
                rawRoll,
                localServer,
                out var preparedOutcome);
            if (buildResult != LegacyTinyCombineOutcomeBuildResult.Accepted || preparedOutcome is null)
                return buildResult == LegacyTinyCombineOutcomeBuildResult.InvalidRoll
                    ? LegacyTinyCombineApplicationResult.InvalidRoll
                    : LegacyTinyCombineApplicationResult.InvalidPlan;

            var guildTaxNpcUpdate = preparedOutcome.Succeeded
                ? TryApplyTinyCombineGuildTaxLocked(participant)
                : null;
            participant.Mob = preparedOutcome.MobSnapshot.ToArray();
            participant.Coin = preparedOutcome.CoinAfter;
            outcome = preparedOutcome with { GuildTaxNpcUpdate = guildTaxNpcUpdate };
            return LegacyTinyCombineApplicationResult.Accepted;
        }
    }

    /// <summary>Restores the pre-combine snapshot only if no newer mutation has replaced its result.</summary>
    public bool TryRollbackTinyCombine(LegacyTinyCombineOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (outcome.ExpectedMob.Length != outcome.MobSnapshot.Length ||
            outcome.MobSnapshot.Length < LegacyAccountSnapshot.MobCarryOffset +
                (LegacyAccountSnapshot.MobCarryCount * LegacyItem.SizeInBytes))
            return false;

        lock (gate)
        {
            if (!participants.TryGetValue(outcome.ConnectionId, out var participant) ||
                participant.Coin != outcome.CoinAfter ||
                !participant.Mob.AsSpan().SequenceEqual(outcome.MobSnapshot.Span))
                return false;

            LegacyWorldNpc? treasuryNpc = null;
            if (outcome.GuildTaxNpcUpdate is { } guildTaxUpdate &&
                (!npcs.TryGetValue(guildTaxUpdate.ConnectionId, out treasuryNpc) ||
                 !treasuryNpc.MobSnapshot.AsSpan().SequenceEqual(guildTaxUpdate.MobSnapshot.Span)))
                return false;

            participant.Mob = outcome.ExpectedMob.ToArray();
            participant.Coin = outcome.CoinBefore;
            if (outcome.GuildTaxNpcUpdate is { } update && treasuryNpc is not null)
                npcs[update.ConnectionId] = treasuryNpc with { MobSnapshot = update.PreviousMobSnapshot.ToArray() };
            return true;
        }
    }

    private LegacyGuildTreasuryNpcUpdate? TryApplyTinyCombineGuildTaxLocked(Participant participant)
    {
        if (guildZones is null)
            return null;

        var village = LegacyMovementMapRules.FindGuildZone(participant.PositionX, participant.PositionY);
        if (village is < 0 or >= LegacyGuildZoneState.ZoneCount ||
            !guildImpostoIds.TryGetValue(village, out var treasuryId) ||
            !npcs.TryGetValue(treasuryId, out var treasuryNpc) ||
            treasuryNpc.Mode == LegacyNpcMode.Empty ||
            treasuryNpc.MobSnapshot.Length < LegacyAccountSnapshot.MobCurrentScoreOffset + LegacyScore.SizeInBytes ||
            BinaryPrimitives.ReadUInt16LittleEndian(treasuryNpc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobGuildOffset)) !=
                unchecked((ushort)guildZones.GetChargeGuild(village)))
            return null;

        var experience = BinaryPrimitives.ReadInt64LittleEndian(
            treasuryNpc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobExperienceOffset));
        if (experience >= LegacyGuildTreasuryExperienceLimit)
            return null;

        var previousMob = treasuryNpc.MobSnapshot.ToArray();
        var updatedMob = previousMob.ToArray();
        BinaryPrimitives.WriteInt64LittleEndian(
            updatedMob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset),
            checked(experience + LegacyTinyCombineGuildTaxExperience));
        npcs[treasuryId] = treasuryNpc with { MobSnapshot = updatedMob };
        return new(treasuryId, previousMob, updatedMob);
    }

    private LegacyWorldNpc RegisterGuildTreasuryNpcLocked(LegacyWorldNpc npc)
    {
        if (guildZones is null ||
            npc.ConnectionId < LegacyMobAiPlanner.MaximumPlayerId ||
            npc.ConnectionId >= LegacyMobAiPlanner.MaximumMobId ||
            npc.MobSnapshot.Length < LegacyAccountSnapshot.MobCurrentScoreOffset + LegacyScore.SizeInBytes ||
            npc.MobSnapshot[LegacyAccountSnapshot.MobMerchantOffset] != 6)
            return npc;

        var village = LegacyScore.Read(npc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)).Level;
        if (village is < 0 or >= LegacyGuildZoneState.ZoneCount)
            return npc;

        var mob = npc.MobSnapshot.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(
            mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset),
            unchecked((ushort)guildZones.GetChargeGuild(village)));
        guildImpostoIds[village] = npc.ConnectionId;
        return npc with { MobSnapshot = mob };
    }
}

public enum LegacyTinyCombineApplicationResult
{
    Accepted,
    ParticipantNotFound,
    StateChanged,
    InvalidPlan,
    InvalidRoll,
}

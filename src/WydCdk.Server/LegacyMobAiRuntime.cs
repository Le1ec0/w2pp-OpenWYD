using System.Security.Cryptography;
using WydCdk.Protocol;
using WydCdk.World;

namespace WydCdk.Server;

    /// <summary>Runs the audited generated-NPC Peace and Combat subsets on stable shards.</summary>
public sealed class LegacyMobAiRuntime
{
    public const int ShardCount = 4;
    public const int PeaceShardCount = 6;
    public static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(200);

    private readonly WorldHub world;
    private readonly LegacyItemDataTable? itemData;
    private readonly Func<LegacyNpcAiApplicationOutcome, CancellationToken, ValueTask> relay;
    private readonly Func<int, int> nextRoll;
    private readonly SemaphoreSlim tickGate = new(1, 1);
    private long nextTick;
    private int runStarted;
    private int activeTickCount;

    public LegacyMobAiRuntime(
        WorldHub world,
        LegacyItemDataTable? itemData,
        Func<LegacyNpcAiApplicationOutcome, CancellationToken, ValueTask> relay,
        Func<int, int>? nextRoll = null)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.itemData = itemData;
        this.relay = relay ?? throw new ArgumentNullException(nameof(relay));
        this.nextRoll = nextRoll ?? RandomNumberGenerator.GetInt32;
    }

    internal int ActiveTickCount => Volatile.Read(ref activeTickCount);
    internal int ActiveWorkerCount => Volatile.Read(ref runStarted);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref runStarted, 1, 0) != 0)
            throw new InvalidOperationException("The generated-NPC AI runtime can only run once.");

        try
        {
            using var timer = new PeriodicTimer(TickInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                await TickNextShardAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref runStarted, 0);
        }
    }

    /// <summary>Consumes one nominal timer turn; the selected shard advances only after taking the tick gate.</summary>
    public async ValueTask<LegacyMobAiTickResult> TickNextShardAsync(CancellationToken cancellationToken = default)
    {
        await tickGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tick = nextTick++;
            var peaceShard = (int)(tick % PeaceShardCount);
            var combatShard = (int)(tick % ShardCount);
            return await TickShardCoreAsync(combatShard, peaceShard, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            tickGate.Release();
        }
    }

    /// <summary>Runs one named shard synchronously with respect to other runtime ticks, for deterministic harnesses.</summary>
    public async ValueTask<LegacyMobAiTickResult> TickShardAsync(int shardIndex, CancellationToken cancellationToken = default)
    {
        if (shardIndex < 0 || shardIndex >= ShardCount)
            throw new ArgumentOutOfRangeException(nameof(shardIndex));

        await tickGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await TickShardCoreAsync(shardIndex, peaceShardIndex: null, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            tickGate.Release();
        }
    }

    private async ValueTask<LegacyMobAiTickResult> TickShardCoreAsync(
        int shardIndex,
        int? peaceShardIndex,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref activeTickCount);
        try
        {
            var peaceVisited = new List<int>();
            if (peaceShardIndex is { } selectedPeaceShard)
            {
                var peaceSnapshots = world.GetGeneratedNpcSnapshotsForShard(
                    selectedPeaceShard,
                    PeaceShardCount,
                    LegacyMobAiPlanner.MaximumPlayerId);
                foreach (var npc in peaceSnapshots)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    peaceVisited.Add(npc.ConnectionId);

                    var pistaLv6AreaDespawn = LegacyMobAiPlanner.IsRunequestLv6AreaDespawnCandidate(
                        npc.GenerateIndex, npc.PositionX, npc.PositionY);
                    if (npc.MobSnapshot.Length < LegacyAccountSnapshot.CharacterStride)
                        continue;

                    if ((npc.Mode is LegacyNpcMode.Idle or LegacyNpcMode.Peace) &&
                        LegacyMobCombatState.Read(npc.MobSnapshot).CurrentScore.Hp <= 0)
                    {
                        if (world.TryRemoveDeadGeneratedNpc(npc.ConnectionId, out var removalOutcome))
                            await relay(removalOutcome, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    if (world.TryProcessGeneratedNpcTimerAffects(npc.ConnectionId, out var affectOutcome))
                    {
                        await relay(affectOutcome, cancellationToken).ConfigureAwait(false);
                        if (affectOutcome.Removal is not null)
                            continue;
                    }

                    if ((npc.MobSnapshot[LegacyAccountSnapshot.MobClanOffset] == 4 && !pistaLv6AreaDespawn) ||
                        npc.Mode != LegacyNpcMode.Peace)
                        continue;

                    if (!world.TryCreateGeneratedNpcAiPeaceSnapshot(
                            npc.ConnectionId,
                            randomSegmentSeedRoll: () => Next(5),
                            out var peaceSnapshot) || peaceSnapshot is null)
                        continue;

                    var peaceDecision = LegacyMobAiPlanner.Plan(peaceSnapshot.PlannerInput);
                    if (peaceDecision.Kind == LegacyMobAiDecisionKind.Unsupported)
                        continue;

                    if (world.TryApplyGeneratedNpcAiDecision(peaceSnapshot, peaceDecision, out var peaceOutcome) !=
                        LegacyNpcAiApplyStatus.Applied)
                        continue;

                    await relay(peaceOutcome, cancellationToken).ConfigureAwait(false);
                }

                var nonGeneratedPeaceAffectNpcs = world.GetNonGeneratedNpcTimerSnapshotsForShard(
                    selectedPeaceShard,
                    PeaceShardCount,
                    LegacyMobAiPlanner.MaximumPlayerId);
                foreach (var npc in nonGeneratedPeaceAffectNpcs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (npc.Mode is not (LegacyNpcMode.Idle or LegacyNpcMode.Peace))
                        continue;

                    peaceVisited.Add(npc.ConnectionId);
                    if (npc.MobSnapshot.Length < LegacyAccountSnapshot.CharacterStride ||
                        !world.TryProcessNonGeneratedNpcTimerAffects(npc.ConnectionId, out var affectOutcome))
                        continue;

                    await relay(affectOutcome, cancellationToken).ConfigureAwait(false);
                }

                var followerSummonIds = world.GetRouteTypeFiveSummonIdsForShard(
                    selectedPeaceShard,
                    PeaceShardCount,
                    LegacyMobAiPlanner.MaximumPlayerId);
                foreach (var summonId in followerSummonIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!world.TryApplyRouteTypeFiveSummonPeaceStep(
                            summonId,
                            randomFallbackOffset: () => Next(5) - 2,
                            out var summonDecision) ||
                        (summonDecision.Movement is null && summonDecision.Removal is null && summonDecision.AffectTick is null))
                        continue;

                    await relay(summonDecision, cancellationToken).ConfigureAwait(false);
                }
            }

            var snapshots = world.GetGeneratedNpcSnapshotsForShard(shardIndex, ShardCount);
            var visited = new List<int>(snapshots.Count);
            var attacks = new List<LegacyGeneratedNpcPhysicalAttackOutcome>();

            var nonGeneratedCombatAffectNpcs = world.GetNonGeneratedNpcTimerSnapshotsForShard(
                shardIndex,
                ShardCount,
                LegacyMobAiPlanner.MaximumPlayerId);
            foreach (var npc in nonGeneratedCombatAffectNpcs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (npc.Mode != LegacyNpcMode.Combat)
                    continue;

                visited.Add(npc.ConnectionId);
                if (npc.MobSnapshot.Length < LegacyAccountSnapshot.CharacterStride ||
                    !world.TryProcessNonGeneratedNpcCombatTimerAffects(npc.ConnectionId, out var affectOutcome))
                    continue;

                await relay(affectOutcome, cancellationToken).ConfigureAwait(false);
            }

            foreach (var npc in snapshots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                visited.Add(npc.ConnectionId);

                if (npc.MobSnapshot.Length < LegacyAccountSnapshot.CharacterStride)
                    continue;

                var combatNpc = npc;
                if (npc.Mode == LegacyNpcMode.Combat &&
                    world.TryProcessGeneratedNpcCombatTimerAffects(npc.ConnectionId, out var affectOutcome))
                {
                    await relay(affectOutcome, cancellationToken).ConfigureAwait(false);
                    if (!world.TryGetNpcSnapshot(npc.ConnectionId, out var refreshedNpc) || refreshedNpc is null)
                        continue;
                    combatNpc = refreshedNpc;
                }

                // Passive acquisition and current-segment route movement run in the earlier
                // six-shard Peace stage. Combat consumes that target, chase, and leash return.
                var pistaLv6AreaDespawn = LegacyMobAiPlanner.IsRunequestLv6AreaDespawnCandidate(
                    combatNpc.GenerateIndex, combatNpc.PositionX, combatNpc.PositionY);
                var invalidRouteFiveLeader = combatNpc.RouteType == 5 &&
                    (combatNpc.LeaderId <= 0 || combatNpc.LeaderId >= LegacyMobAiPlanner.MaximumPlayerId);
                var despawnBeforeCombatRandom = invalidRouteFiveLeader || pistaLv6AreaDespawn;
                if ((combatNpc.MobSnapshot[LegacyAccountSnapshot.MobClanOffset] == 4 && !pistaLv6AreaDespawn) ||
                    (combatNpc.RouteType == 5 && !invalidRouteFiveLeader && !pistaLv6AreaDespawn) ||
                    combatNpc.Mode != LegacyNpcMode.Combat ||
                    (combatNpc.EnemyList.All(static id => id == 0) && !pistaLv6AreaDespawn))
                    continue;

                var attackRange = 0;
                if (!despawnBeforeCombatRandom && !TryGetAttackRange(combatNpc, out attackRange))
                    continue;

                var randomIntRoll = despawnBeforeCombatRandom ? 0 : Next(100);
                var dexterityRoll = despawnBeforeCombatRandom ? 0 : Next(100);
                var skillBarMotionRoll = !despawnBeforeCombatRandom && ConsumesSkillBarMotionRoll(combatNpc.MobSnapshot) ? Next(4) : 0;
                var skillBarSelectionRoll = despawnBeforeCombatRandom ? 0 : Next(100);
                var damageRoll = despawnBeforeCombatRandom ? 99 : 99 + Next(12);
                var missRoll = despawnBeforeCombatRandom ? 1 : 1 + Next(100);
                if (!world.TryCreateGeneratedNpcAiChaseSnapshot(
                        combatNpc.ConnectionId,
                        attackRange,
                        randomIntRoll,
                        dexterityRoll,
                        randomFallbackOffset: () => despawnBeforeCombatRandom ? 0 : Next(5) - 3,
                        randomReturnSeedRoll: () => despawnBeforeCombatRandom ? 0 : Next(5),
                        out var snapshot,
                        damageRoll,
                        missRoll,
                        skillBarSelectionRoll,
                        skillBarMotionRoll,
                        randomRetreatStepRoll: () => despawnBeforeCombatRandom ? 0 : Next(2)) || snapshot is null)
                    continue;

                var decision = LegacyMobAiPlanner.Plan(snapshot.PlannerInput);
                if (decision.Kind == LegacyMobAiDecisionKind.Unsupported)
                    continue;

                if (world.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var outcome) != LegacyNpcAiApplyStatus.Applied)
                    continue;

                await relay(outcome, cancellationToken).ConfigureAwait(false);
                if (outcome.Attack is { Damage: > 0, Missed: false } attack)
                    attacks.Add(attack);
            }

            return new LegacyMobAiTickResult(
                shardIndex,
                visited.AsReadOnly(),
                attacks.AsReadOnly(),
                peaceShardIndex ?? -1,
                peaceVisited.AsReadOnly());
        }
        finally
        {
            Interlocked.Decrement(ref activeTickCount);
        }
    }

    private bool TryGetAttackRange(LegacyWorldNpc npc, out int attackRange)
    {
        attackRange = 0;
        if (npc.GenerateIndex == LegacyMobAiPlanner.KefraBossGenerateIndex)
        {
            attackRange = 25;
            return true;
        }

        Span<LegacyItem> equipment = stackalloc LegacyItem[LegacyCharacterSelection.EquipmentCount];
        var hasEquipment = false;
        for (var slot = 0; slot < equipment.Length; slot++)
        {
            equipment[slot] = LegacyItem.Read(npc.MobSnapshot.AsSpan(
                LegacyAccountSnapshot.MobEquipmentOffset + (slot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
            hasEquipment |= equipment[slot].Index != 0;
        }

        if (itemData is null)
            return !hasEquipment;

        var learnedSkills = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(
            npc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset, sizeof(uint)));
        attackRange = LegacyMobAbilityMath.GetAbility(
            equipment,
            npc.MobSnapshot[LegacyAccountSnapshot.MobClassOffset],
            learnedSkills,
            LegacyItemEffect.Range,
            itemData);
        return true;
    }

    private static bool ConsumesSkillBarMotionRoll(byte[] mobSnapshot)
    {
        if (mobSnapshot.Length < LegacyAccountSnapshot.MobSkillBarOffset + 4)
            return false;

        var skillBar = mobSnapshot.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4);
        return IsRandomMotionSkill(skillBar[1]) || IsRandomMotionSkill(skillBar[3]);

        static bool IsRandomMotionSkill(byte value) => value is 3 or 6 or 7 or 15 or 23 or 24 or 27;
    }

    private int Next(int exclusiveMaximum)
    {
        var value = nextRoll(exclusiveMaximum);
        if (value < 0 || value >= exclusiveMaximum)
            throw new InvalidOperationException($"The generated-NPC random source returned {value} outside [0,{exclusiveMaximum}).");
        return value;
    }
}

public sealed record LegacyMobAiTickResult(
    int ShardIndex,
    IReadOnlyList<int> VisitedNpcIds,
    IReadOnlyList<LegacyGeneratedNpcPhysicalAttackOutcome> Attacks,
    int PeaceShardIndex,
    IReadOnlyList<int> VisitedPeaceNpcIds);

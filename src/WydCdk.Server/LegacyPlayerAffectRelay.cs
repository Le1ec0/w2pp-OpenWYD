using System.Security.Cryptography;
using WydCdk.Protocol;
using WydCdk.World;

namespace WydCdk.Server;

/// <summary>Relays timer-driven player attacks, HP, score, and equipment updates to captured recipients.</summary>
public static class LegacyPlayerAffectRelay
{
    public static async ValueTask RelayAsync(
        WorldHub world,
        LegacyPlayerAffectTimerOutcome outcome,
        LegacyFrameCodec codec,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(codec);

        var clientTick = unchecked((uint)Environment.TickCount64);
        var keywordIndex = (byte)RandomNumberGenerator.GetInt32(256);
        foreach (var areaAttack in outcome.AreaAttacks)
            await RelayAreaAttackAsync(world, areaAttack, codec, cancellationToken).ConfigureAwait(false);

        if (outcome.ResourceRefreshRequired &&
            world.TryGetResourceState(outcome.ConnectionId, out var resourceState, out var requestedHp, out var requestedMp) &&
            resourceState is not null)
        {
            var resourceFrame = new SetHpMpConfirmation(
                    resourceState.CurrentScore.Hp,
                    resourceState.CurrentMana,
                    requestedHp,
                    requestedMp)
                .ToFrame(codec, clientTick, keywordIndex, checked((ushort)outcome.ConnectionId));
            await world.SendAsync(outcome.ConnectionId, resourceFrame, cancellationToken).ConfigureAwait(false);
        }

        if (outcome.HpUpdateRequired)
        {
            var hpFrame = new SetHpDamConfirmation(outcome.Hp, outcome.Damage)
                .ToFrame(codec, clientTick, keywordIndex, checked((ushort)outcome.ConnectionId));
            await world.BroadcastToParticipantsAsync(outcome.ViewRecipientConnectionIds, hpFrame, cancellationToken);
        }

        if (outcome.ScoreRefreshRequired)
        {
            var sourcePayload = new UpdateScoreConfirmation(outcome.MobSnapshot, outcome.AffectSnapshot).ToPayload();
            var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                    sourcePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize),
                    LegacyMobCombatState.Read(outcome.MobSnapshot).Rsv)
                .ToFrame(codec, clientTick, keywordIndex, checked((ushort)outcome.ConnectionId));
            await world.BroadcastToParticipantsAsync(outcome.ViewRecipientConnectionIds, scoreFrame, cancellationToken);
        }

        if (outcome.ScoreRefreshRequired && outcome.EquipmentChanged &&
            world.TryBuildUpdateEquipFrame(outcome.ConnectionId, codec, clientTick, keywordIndex, out var equipmentFrame) &&
            equipmentFrame is not null)
            await world.BroadcastToParticipantsAsync(outcome.ViewRecipientConnectionIds, equipmentFrame, cancellationToken);
    }

    private static async ValueTask RelayAreaAttackAsync(
        WorldHub world,
        LegacyPlayerAffectAreaAttackOutcome areaAttack,
        LegacyFrameCodec codec,
        CancellationToken cancellationToken)
    {
        var tick = ClientTickPolicy.SkipCheckTick;
        var keyword = (byte)RandomNumberGenerator.GetInt32(256);
        var batch = areaAttack.BatchOutcome;
        var removedTargetIds = new HashSet<int>();
        var experienceAwards = new List<LegacyExperienceAward>();
        var itemDrops = new List<LegacyItemDrop>();
        var spawnedNpcs = new List<LegacyWorldNpc>();

        if (batch is not null)
        {
            foreach (var slot in batch.SlotOutcomes.Where(static slot => slot.Kind == LegacyAttackActionSlotKind.Skill))
            {
                if (slot.SkillResult != LegacySkillAttackResult.Accepted || slot.SkillOutcome is not { } skill)
                    continue;

                foreach (var notice in skill.PrivateNotices ?? [])
                {
                    var noticeFrame = new MessagePanelConfirmation(notice.Message).ToFrame(codec, tick, keyword);
                    await world.SendAsync(notice.ConnectionId, noticeFrame, cancellationToken).ConfigureAwait(false);
                }

                if (skill.TargetAffectSnapshot is { } targetAffect)
                {
                    var scorePayload = new UpdateScoreConfirmation(skill.TargetMobSnapshot, targetAffect).ToPayload();
                    var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                            scorePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize),
                            LegacyMobCombatState.Read(skill.TargetMobSnapshot).Rsv)
                        .ToFrame(codec, tick, keyword, checked((ushort)skill.TargetConnectionId));
                    await world.SendAsync(skill.TargetConnectionId, scoreFrame, cancellationToken).ConfigureAwait(false);
                }

                if ((skill.TargetHpChanged || skill.TargetResourceChanged) && !skill.TargetRevived &&
                    world.TryGetResourceState(skill.TargetConnectionId, out var targetState, out var requestedHp, out var requestedMp) &&
                    targetState is not null)
                {
                    var resourceFrame = new SetHpMpConfirmation(
                            targetState.CurrentScore.Hp,
                            targetState.CurrentMana,
                            requestedHp,
                            requestedMp)
                        .ToFrame(codec, tick, keyword, checked((ushort)skill.TargetConnectionId));
                    await world.SendAsync(skill.TargetConnectionId, resourceFrame, cancellationToken).ConfigureAwait(false);
                }

                if (skill.TargetRemoved)
                    removedTargetIds.Add(skill.TargetConnectionId);
                if (skill.ZardMobLeftCount is { } zardMobLeftCount)
                {
                    var mobLeftFrame = new MobLeftConfirmation(zardMobLeftCount).ToFrame(codec, tick, keyword);
                    await world.SendAsync(areaAttack.AttackerConnectionId, mobLeftFrame, cancellationToken).ConfigureAwait(false);
                }
                if (skill.PistaTransition is { } pistaTransition)
                {
                    if (pistaTransition.SpawnGenerateIndex > 0 &&
                        world.TrySpawnGeneratedNpcGroup(pistaTransition.SpawnGenerateIndex, out var spawnGroup) &&
                        spawnGroup is not null)
                        spawnedNpcs.AddRange(spawnGroup);

                    if (pistaTransition.SpawnGenerateIndexes is { Count: > 0 } spawnIndexes)
                    {
                        foreach (var generateIndex in spawnIndexes)
                            if (world.TrySpawnGeneratedNpcGroup(generateIndex, out var generatedGroup) && generatedGroup is not null)
                                spawnedNpcs.AddRange(generatedGroup);
                    }

                    if (pistaTransition.RemovedNpcConnectionIds is { Count: > 0 } removedNpcIds)
                        removedTargetIds.UnionWith(removedNpcIds);

                    if (pistaTransition.Teleports is { Count: > 0 } teleports)
                        await RelayPistaTeleportsAsync(world, teleports, codec, tick, keyword, cancellationToken)
                            .ConfigureAwait(false);
                }
                if (skill.ExperienceAwards is { Count: > 0 } awards)
                    experienceAwards.AddRange(awards.Where(award => award.ConnectionId != areaAttack.AttackerConnectionId));
                if (skill.ItemDrops is { Count: > 0 } drops)
                    itemDrops.AddRange(drops);

                if (skill.CoinUpdates is { Count: > 0 } coinUpdates)
                {
                    foreach (var update in coinUpdates)
                    {
                        var payload = new UpdateEtcConfirmation(update.MobSnapshot, ReadOnlySpan<byte>.Empty).ToPayload();
                        var coinFrame = W2ppUpdateEtcV1Adapter.AdaptPayload(
                                payload.AsSpan(0, W2ppUpdateEtcV1Adapter.SourcePayloadSize))
                            .ToFrame(codec, tick, keyword, checked((ushort)update.ConnectionId));
                        await world.SendAsync(update.ConnectionId, coinFrame, cancellationToken).ConfigureAwait(false);
                    }
                }

                if (skill.GlobalNotices is { Count: > 0 } globalNotices)
                {
                    foreach (var notice in globalNotices)
                    {
                        var noticeFrame = new MessagePanelConfirmation(notice.Message).ToFrame(codec, tick, keyword);
                        await world.BroadcastAsync(-1, noticeFrame, cancellationToken).ConfigureAwait(false);
                    }
                }

                if (skill.AreaNotices is { Count: > 0 } areaNotices)
                {
                    foreach (var notice in areaNotices)
                    {
                        var noticeFrame = new MessagePanelConfirmation(notice.Message).ToFrame(codec, tick, keyword);
                        foreach (var recipient in world.GetParticipantIdsInArea(notice.X1, notice.Y1, notice.X2, notice.Y2))
                            await world.SendAsync(recipient, noticeFrame, cancellationToken).ConfigureAwait(false);
                    }
                }

                if (skill.CrimeStateUpdates is { Count: > 0 } crimeStateUpdates)
                {
                    var battleRoyaleState = world.GetCombatWorldState().BattleRoyaleState;
                    foreach (var crimeState in crimeStateUpdates)
                    {
                        var createPayload = new CreateMobConfirmation(
                                checked((ushort)crimeState.ConnectionId),
                                crimeState.PositionX,
                                crimeState.PositionY,
                                crimeState.MobSnapshot,
                                ReadOnlySpan<byte>.Empty,
                                npc: false)
                            .ToPayload();
                        var createFrame = W2ppCreateMobV1Adapter.AdaptPayload(createPayload)
                            .ToFrame(codec, tick, keyword);
                        createFrame = LegacyBattleRoyaleCreateMobVisualRelay.Apply(createFrame, battleRoyaleState, codec);
                        await world.SendAsync(crimeState.ConnectionId, createFrame, cancellationToken).ConfigureAwait(false);
                        await world.BroadcastAsync(crimeState.ConnectionId, createFrame, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            foreach (var removal in batch.ViewRemovalEffects)
            {
                var removeFrame = new RemoveMobConfirmation(
                        checked((ushort)removal.TargetConnectionId),
                        removeType: 1)
                    .ToFrame(codec, tick, keyword);
                await LegacyAttackViewRemovalRelay.RelayCapturedViewAsync(
                    world,
                    areaAttack.AttackerConnectionId,
                    removal,
                    removeFrame,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        var wireSlots = new List<(ushort TargetId, int Damage)>(areaAttack.TargetConnectionIds.Count);
        for (var index = 0; index < areaAttack.TargetConnectionIds.Count; index++)
        {
            var targetId = areaAttack.TargetConnectionIds[index];
            var slot = batch?.SlotOutcomes.FirstOrDefault(candidate => candidate.SlotIndex == index &&
                candidate.Kind == LegacyAttackActionSlotKind.Skill);
            if (slot?.SkillOutcome is { } skillOutcome)
            {
                wireSlots.Add((
                    skillOutcome.ClearTargetIdOnWire ? (ushort)0 : checked((ushort)targetId),
                    skillOutcome.ClearTargetIdOnWire ? 0 : skillOutcome.PreserveSkillMarkerOnWire ? -1 : skillOutcome.Damage));
            }
            else
            {
                wireSlots.Add((checked((ushort)targetId), -1));
            }
        }

        AttackConfirmation? attackConfirmation = null;
        var hasAttackConfirmation = areaAttack.OneTargetMessage
            ? wireSlots.Count == 1 && AttackConfirmation.TryCreateSyntheticOneTarget(
                areaAttack.AttackerMobSnapshot,
                checked((ushort)areaAttack.AttackerConnectionId),
                areaAttack.TargetX,
                areaAttack.TargetY,
                wireSlots[0].TargetId,
                wireSlots[0].Damage,
                checked((short)areaAttack.SkillIndex),
                motion: 254,
                out attackConfirmation)
            : AttackConfirmation.TryCreateSyntheticArea(
                areaAttack.AttackerMobSnapshot,
                checked((ushort)areaAttack.AttackerConnectionId),
                areaAttack.PositionX,
                areaAttack.PositionY,
                areaAttack.TargetX,
                areaAttack.TargetY,
                checked((short)areaAttack.SkillIndex),
                254,
                wireSlots,
                out attackConfirmation);
        if (batch?.CompletedNormally != false && hasAttackConfirmation && attackConfirmation is not null)
        {
            var attackFrame = attackConfirmation.ToFrame(codec, tick, keyword);
            await world.BroadcastToParticipantsAsync(
                areaAttack.ViewRecipientConnectionIds,
                attackFrame,
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var targetId in removedTargetIds)
        {
            var removeFrame = new RemoveMobConfirmation(checked((ushort)targetId))
                .ToFrame(codec, tick, keyword);
            await world.BroadcastToParticipantsAsync(
                areaAttack.ViewRecipientConnectionIds,
                removeFrame,
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var award in experienceAwards)
        {
            var payload = new UpdateEtcConfirmation(award.MobSnapshot, ReadOnlySpan<byte>.Empty).ToPayload();
            var experienceFrame = W2ppUpdateEtcV1Adapter.AdaptPayload(
                    payload.AsSpan(0, W2ppUpdateEtcV1Adapter.SourcePayloadSize))
                .ToFrame(codec, tick, keyword, checked((ushort)award.ConnectionId));
            await world.SendAsync(award.ConnectionId, experienceFrame, cancellationToken).ConfigureAwait(false);
        }

        foreach (var drop in itemDrops)
        {
            var itemFrame = new SendItemConfirmation(1, checked((short)drop.InventorySlot), drop.Item)
                .ToFrame(codec, tick, keyword, checked((ushort)drop.ConnectionId));
            await world.SendAsync(drop.ConnectionId, itemFrame, cancellationToken).ConfigureAwait(false);
        }

        if (spawnedNpcs.Count > 0)
        {
            var battleRoyaleState = world.GetCombatWorldState().BattleRoyaleState;
            await LegacyNpcGenerationRelay.RelayGroupAsync(
                world,
                spawnedNpcs,
                npc =>
                {
                    var sourcePayload = new CreateMobConfirmation(
                        checked((ushort)npc.ConnectionId),
                        npc.PositionX,
                        npc.PositionY,
                        npc.MobSnapshot,
                        npc.AffectSnapshot,
                        npc: true).ToPayload();
                    var frame = W2ppCreateMobV1Adapter.AdaptPayload(sourcePayload)
                        .ToFrame(codec, tick, keyword);
                    return LegacyBattleRoyaleCreateMobVisualRelay.Apply(frame, battleRoyaleState, codec);
                },
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask RelayPistaTeleportsAsync(
        WorldHub world,
        IReadOnlyList<LegacyPistaTeleport> teleports,
        LegacyFrameCodec codec,
        uint tick,
        byte keyword,
        CancellationToken cancellationToken)
    {
        foreach (var teleport in teleports)
        {
            var frame = new ActionRequest(
                    teleport.FromX,
                    teleport.FromY,
                    Effect: 1,
                    Speed: 0,
                    Route: new byte[24],
                    teleport.ToX,
                    teleport.ToY)
                .ToFrame(codec, tick, keyword, checked((ushort)teleport.ConnectionId));
            await world.SendAsync(teleport.ConnectionId, frame, cancellationToken).ConfigureAwait(false);
            await world.BroadcastAsync(teleport.ConnectionId, frame, cancellationToken).ConfigureAwait(false);
        }
    }
}

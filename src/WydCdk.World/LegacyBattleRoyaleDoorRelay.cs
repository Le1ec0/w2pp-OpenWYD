using WydCdk.Protocol;

namespace WydCdk.World;

public enum LegacyBattleRoyaleDoorPacketKind
{
    ActorAction,
    ActorRemoval,
    PartyRemoval,
    PartyAdd,
    SummonRemoval,
    GateCreateItem,
}

public sealed record LegacyBattleRoyaleDoorPacket(
    int RecipientId,
    int EntityId,
    int GateItemId,
    LegacyBattleRoyaleDoorPacketKind Kind,
    byte[] Frame);

public sealed record LegacyBattleRoyaleDoorPlan(IReadOnlyList<LegacyBattleRoyaleDoorPacket> Packets);

/// <summary>Applies and relays the two Coliseum door slots used by TMProject 7.69 BR.</summary>
public static class LegacyBattleRoyaleDoorRelay
{
    public static LegacyBattleRoyaleDoorPlan? TryCreate(
        WorldHub world,
        LegacyBattleRoyaleTransition transition,
        LegacyBattleRoyaleDoorCallTiming timing,
        LegacyFrameCodec codec,
        uint clientTick,
        byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(codec);

        var calls = transition.DoorCalls.Where(call => call.Timing == timing).ToArray();
        if (calls.Length != 1)
            return null;

        var update = world.TryUpdateBattleRoyaleDoors(calls[0].State);
        if (update is null)
            return null;

        var packets = new List<LegacyBattleRoyaleDoorPacket>();
        foreach (var gate in update.Gates.OrderBy(static gate => gate.MapItem.ItemId))
        {
            foreach (var actor in gate.ActorEffects)
            {
                if (actor.ConnectionId is <= 0 or > ushort.MaxValue ||
                    actor.RecipientIds.Any(static recipient => recipient <= 0))
                    return null;

                if (actor.Deleted)
                {
                    var removeFrame = new RemoveMobConfirmation(checked((ushort)actor.ConnectionId), removeType: 3)
                        .ToFrame(codec, clientTick, keywordIndex);
                    foreach (var recipientId in actor.RecipientIds)
                        packets.Add(new(recipientId, actor.ConnectionId, gate.MapItem.ItemId, LegacyBattleRoyaleDoorPacketKind.ActorRemoval, removeFrame));

                    if (actor.PartyEffect is { } partyEffect)
                    {
                        var partyRemoval = partyEffect.Removal;
                        foreach (var recipientId in partyRemoval.NotifiedConnectionIds)
                        {
                            var removedId = partyRemoval.Disbanded || recipientId == partyRemoval.RemovedConnectionId
                                ? 0
                                : partyRemoval.RemovedConnectionId;
                            var partyFrame = new PartyRemoveConfirmation(checked((short)removedId))
                                .ToFrame(codec, clientTick, keywordIndex);
                            packets.Add(new(recipientId, partyRemoval.RemovedConnectionId, gate.MapItem.ItemId, LegacyBattleRoyaleDoorPacketKind.PartyRemoval, partyFrame));
                        }

                        if (partyEffect.ReformedParty is { } reformedParty)
                        {
                            foreach (var viewer in reformedParty.Members)
                            foreach (var member in reformedParty.Members)
                            {
                                var leaderConnection = member.ConnectionId == reformedParty.LeaderConnectionId
                                    ? member.ConnectionId
                                    : 30_000;
                                var addFrame = new PartyAddConfirmation(
                                        checked((short)leaderConnection),
                                        checked((short)Math.Clamp(member.Level, short.MinValue, short.MaxValue)),
                                        checked((short)Math.Clamp(member.MaxHp > short.MaxValue ? (member.MaxHp + 1) / 100 : member.MaxHp, short.MinValue, short.MaxValue)),
                                        checked((short)Math.Clamp(member.Hp > short.MaxValue ? (member.Hp + 1) / 100 : member.Hp, short.MinValue, short.MaxValue)),
                                        checked((short)member.ConnectionId),
                                        member.MobName,
                                        unchecked((short)52_428))
                                    .ToFrame(codec, clientTick, keywordIndex, 30_000);
                                packets.Add(new(viewer.ConnectionId, member.ConnectionId, gate.MapItem.ItemId, LegacyBattleRoyaleDoorPacketKind.PartyAdd, addFrame));
                            }
                        }
                    }

                    foreach (var summon in actor.DespawnedSummons)
                    {
                        if (summon.ConnectionId is <= 0 or > ushort.MaxValue ||
                            summon.RecipientIds.Any(static recipient => recipient <= 0))
                            return null;
                        var summonFrame = new RemoveMobConfirmation(checked((ushort)summon.ConnectionId), removeType: 3)
                            .ToFrame(codec, clientTick, keywordIndex);
                        foreach (var recipientId in summon.RecipientIds)
                            packets.Add(new(recipientId, summon.ConnectionId, gate.MapItem.ItemId, LegacyBattleRoyaleDoorPacketKind.SummonRemoval, summonFrame));
                    }
                }
                else
                {
                    var actionFrame = new ActionRequest(
                            actor.PositionX,
                            actor.PositionY,
                            Effect: 1,
                            Speed: 2,
                            Route: new byte[24],
                            actor.TargetX,
                            actor.TargetY)
                        .ToFrame(codec, clientTick, keywordIndex, checked((ushort)actor.ConnectionId));
                    foreach (var recipientId in actor.RecipientIds)
                        packets.Add(new(recipientId, actor.ConnectionId, gate.MapItem.ItemId, LegacyBattleRoyaleDoorPacketKind.ActorAction, actionFrame));
                }
            }

            var itemFrame = new CreateItemConfirmation(
                    checked((ushort)gate.MapItem.PositionX),
                    checked((ushort)gate.MapItem.PositionY),
                    checked((ushort)(gate.MapItem.ItemId + LegacyMapItemStateCodes.WireIdOffset)),
                    gate.MapItem.Item,
                    gate.MapItem.Rotate,
                    checked((byte)gate.MapItem.State),
                    checked((byte)Math.Clamp(gate.MapItem.Height, 0, byte.MaxValue)))
                .ToFrame(codec, clientTick, keywordIndex);
            foreach (var recipientId in gate.ItemRecipientIds)
                packets.Add(new(recipientId, gate.MapItem.ItemId, gate.MapItem.ItemId, LegacyBattleRoyaleDoorPacketKind.GateCreateItem, itemFrame));
        }

        return new LegacyBattleRoyaleDoorPlan(Array.AsReadOnly(packets.ToArray()));
    }

    public static async ValueTask SendAsync(WorldHub world, LegacyBattleRoyaleDoorPlan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(plan);
        foreach (var packet in plan.Packets)
            await world.SendAsync(packet.RecipientId, packet.Frame, cancellationToken);
    }
}

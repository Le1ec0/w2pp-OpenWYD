using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class BattleRoyaleDoorTests
{
    public static void ScheduleExposesLegacyDoorCallOrder()
    {
        var schedule = CreateSchedule();
        var ready = schedule.Tick(new DateTime(2026, 10, 5, 19, 0, 0))
            ?? throw new InvalidOperationException("The Battle Royale ready transition was not produced.");
        Check(ready.DoorCalls.Count == 0, "The 0-to-1 ready transition unexpectedly changed the doors.");

        var start = schedule.Tick(new DateTime(2026, 10, 5, 19, 5, 0))
            ?? throw new InvalidOperationException("The Battle Royale start transition was not produced.");
        Check(start.DoorCalls.SequenceEqual([
                new LegacyBattleRoyaleDoorCall(LegacyMapItemStateCodes.Locked, LegacyBattleRoyaleDoorCallTiming.BeforeNotice),
                new LegacyBattleRoyaleDoorCall(LegacyMapItemStateCodes.Locked, LegacyBattleRoyaleDoorCallTiming.AfterAreaClear),
            ]),
            "The 1-to-2 transition did not expose both legacy close calls in their original order.");

        var reward = schedule.Tick(new DateTime(2026, 10, 5, 19, 13, 0))
            ?? throw new InvalidOperationException("The Battle Royale reward transition was not produced.");
        Check(reward.DoorCalls.Count == 0, "The 2-to-3 reward transition unexpectedly changed the doors.");

        var end = schedule.Tick(new DateTime(2026, 10, 5, 19, 14, 0))
            ?? throw new InvalidOperationException("The Battle Royale event-end transition was not produced.");
        Check(end.DoorCalls.SequenceEqual([
                new LegacyBattleRoyaleDoorCall(LegacyMapItemStateCodes.Open, LegacyBattleRoyaleDoorCallTiming.BeforeNotice),
            ]),
            "The 3-to-0 transition did not expose the legacy door-open call.");
    }

    public static void DoorMaskOccupantsAndRelayMatch769()
    {
        var height = new byte[LegacyMapGrid.HeightMapSize];
        Array.Fill(height, (byte)109);
        var mapGrid = new LegacyMapGrid(height, new byte[LegacyMapGrid.AttributeMapSize]);
        var world = new WorldHub(mapGrid);
        world.ConfigureMapItems([
            new LegacyMapItemState(13, new LegacyItem(472, 0, 0, 0, 0, 0, 0), 2_603, 1_717, 1, 1, LegacyMapItemStateCodes.Open, 109, 90),
            new LegacyMapItemState(14, new LegacyItem(472, 0, 0, 0, 0, 0, 0), 2_603, 1_733, 1, 1, LegacyMapItemStateCodes.Open, 109, 90),
        ], LegacyGroundMaskTable.CreateOfficial());

        var codec = LegacyFrameCodec.CreateDefault();
        var sent = new List<(int RecipientId, byte[] Frame)>();
        AddPlayer(world, sent, 1, "BR_DOOR_BOUNDARY", 2_587, 1_701);
        AddPlayer(world, sent, 2, "BR_DOOR_OUTSIDE", 2_586, 1_701);
        AddPlayer(world, sent, 3, "BR_DOOR_OCCUPANT", 2_603, 1_716);
        AddPlayer(world, sent, 4, "BR_DOOR_SECOND_BOUNDARY", 2_587, 1_717);
        AddPlayer(world, sent, 5, "BR_DOOR_SECOND_OUTSIDE", 2_587, 1_716);
        AddPlayer(world, sent, 6, "BR_DOOR_SECOND_CORNER", 2_619, 1_749);
        AddPlayer(world, sent, 7, "BR_DOOR_DELETE_PLAYER", 2_603, 1_733, equip0: 220);
        AddPlayer(world, sent, 8, "BR_DOOR_NEW_LEADER", 2_604, 1_733);
        AddPlayer(world, sent, 9, "BR_DOOR_NEW_MEMBER", 2_605, 1_733);
        Check(world.TrySetParty(7, [7, 8, 9]), "The mounted Battle Royale door party fixture could not be formed.");
        var mountedMob = CreateMob("BR_DOOR_DELETE", equip0: 220);
        var npcId = world.EnterNpc(mountedMob, 2_603, 1_732, new byte[LegacyAccountSnapshot.AffectStride], requestedConnectionId: 20_000);
        Check(npcId == 20_000, "The door deletion fixture NPC was not registered.");

        var schedule = CreateSchedule();
        _ = schedule.Tick(new DateTime(2026, 10, 5, 19, 0, 0));
        var start = schedule.Tick(new DateTime(2026, 10, 5, 19, 5, 0))
            ?? throw new InvalidOperationException("The Battle Royale start transition was not produced.");

        var closeBeforeNotice = LegacyBattleRoyaleDoorRelay.TryCreate(
            world, start, LegacyBattleRoyaleDoorCallTiming.BeforeNotice, codec, clientTick: 41, keywordIndex: 7)
            ?? throw new InvalidOperationException("The first Battle Royale close-door relay was not created.");
        Check(world.TryGetMapItem(10_013, out var closed13) && closed13 is { State: LegacyMapItemStateCodes.Locked, Delay: 0 } &&
              world.TryGetMapItem(10_014, out var closed14) && closed14 is { State: LegacyMapItemStateCodes.Locked, Delay: 0 },
            "The 7.69 static door slots 13/14 did not close and reset Delay.");
        Check(mapGrid.GetTerrainHeight(2_603, 1_716) == 127 && mapGrid.GetTerrainHeight(2_603, 1_732) == 127,
            "Closing either Battle Royale door did not apply its official rotated ground mask.");
        Check(world.TryGetCharacterSnapshot(3, out _, out var actorX, out var actorY) && actorX == 2_603 && actorY == 1_716,
            "The legacy GetEmptyMobGrid self-occupant branch unexpectedly moved the player.");
        Check(!world.TryGetNpcSnapshot(npcId, out _), "The Equip[0]=220 occupant was not removed with DeleteMob type 3.");
        Check(world.TryGetCombatState(7, out var deletedPlayerState) && deletedPlayerState!.CurrentScore.Hp == 0,
            "DeleteMob did not clear the HP of a connected Equip[0]=220 occupant.");
        var partyAdds = closeBeforeNotice.Packets.Where(static packet => packet.Kind == LegacyBattleRoyaleDoorPacketKind.PartyAdd).ToArray();
        Check(partyAdds.Length == 4 && partyAdds.Select(static packet => packet.RecipientId).ToHashSet().SetEquals([8, 9]) &&
              partyAdds.All(packet => codec.Decode(packet.Frame).Header.Type == PartyAddConfirmation.MessageType) &&
              world.TryPreparePartyInvite(9, 8, out _) == PartyInviteCheckResult.SourceIsPartyMember,
            "DeleteMob did not promote the next player and rebuild the remaining party after its leader was removed.");

        var actorPacket = closeBeforeNotice.Packets.FirstOrDefault(packet =>
            packet.Kind == LegacyBattleRoyaleDoorPacketKind.ActorAction && packet.EntityId == 3);
        Check(actorPacket is not null, "The occupied gate did not reset the player route with a legacy Action.");
        var actionFrame = codec.Decode(actorPacket!.Frame);
        Check(ActionRequest.TryParse(actionFrame, out var action) && action is
            { PositionX: 2_603, PositionY: 1_716, TargetX: 2_603, TargetY: 1_716, Effect: 1, Speed: 2 } &&
            action.Route.All(static value => value == 0),
            "The occupant Action did not preserve the same-position, route-reset GetAction layout.");

        var removedNpcPacket = closeBeforeNotice.Packets.FirstOrDefault(packet =>
            packet.Kind == LegacyBattleRoyaleDoorPacketKind.ActorRemoval && packet.EntityId == npcId);
        Check(removedNpcPacket is not null && codec.Decode(removedNpcPacket.Frame) is { Header.Id: 20_000 } removeFrame &&
              BinaryPrimitives.ReadInt32LittleEndian(removeFrame.Payload.Span) == 3,
            "DeleteMob did not relay RemoveMob type 3 before the second gate confirmation.");
        Check(closeBeforeNotice.Packets.Any(packet => packet.Kind == LegacyBattleRoyaleDoorPacketKind.ActorRemoval && packet.EntityId == 7 && packet.RecipientId == 8) &&
              closeBeforeNotice.Packets.All(packet => packet.Kind != LegacyBattleRoyaleDoorPacketKind.ActorRemoval || packet.EntityId != 7 || packet.RecipientId != 7) &&
              closeBeforeNotice.Packets.Any(packet => packet.Kind == LegacyBattleRoyaleDoorPacketKind.PartyRemoval && packet.EntityId == 7 && packet.RecipientId == 7),
            "DeleteMob did not multicast RemoveMob to peers while excluding the actor and keeping its direct PartyRemove.");

        var gate13Recipients = closeBeforeNotice.Packets
            .Where(static packet => packet.Kind == LegacyBattleRoyaleDoorPacketKind.GateCreateItem && packet.GateItemId == 13)
            .Select(static packet => packet.RecipientId).ToHashSet();
        var gate14Recipients = closeBeforeNotice.Packets
            .Where(static packet => packet.Kind == LegacyBattleRoyaleDoorPacketKind.GateCreateItem && packet.GateItemId == 14)
            .Select(static packet => packet.RecipientId).ToHashSet();
        Check(gate13Recipients.Contains(1) && !gate13Recipients.Contains(2) &&
              gate14Recipients.Contains(4) && gate14Recipients.Contains(6) && !gate14Recipients.Contains(5),
            "Gate CreateItem packets did not use the inclusive 33x33 recipient views for both slots.");
        Check(closeBeforeNotice.Packets.ToList().FindIndex(packet => packet.Kind == LegacyBattleRoyaleDoorPacketKind.ActorAction && packet.EntityId == 3) <
              closeBeforeNotice.Packets.ToList().FindIndex(packet => packet.Kind == LegacyBattleRoyaleDoorPacketKind.GateCreateItem && packet.GateItemId == 13) &&
              closeBeforeNotice.Packets.ToList().FindIndex(packet => packet.Kind == LegacyBattleRoyaleDoorPacketKind.ActorRemoval && packet.EntityId == npcId) <
              closeBeforeNotice.Packets.ToList().FindIndex(packet => packet.Kind == LegacyBattleRoyaleDoorPacketKind.GateCreateItem && packet.GateItemId == 14),
            "UpdateItem occupant effects were not sent before each gate's CreateItem confirmation.");
        AssertGateCreateItem(codec, closeBeforeNotice, gateItemId: 13, state: LegacyMapItemStateCodes.Locked);
        AssertGateCreateItem(codec, closeBeforeNotice, gateItemId: 14, state: LegacyMapItemStateCodes.Locked);

        LegacyBattleRoyaleDoorRelay.SendAsync(world, closeBeforeNotice, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        Check(sent.Count == closeBeforeNotice.Packets.Count && sent.Select(static item => item.RecipientId)
                .SequenceEqual(closeBeforeNotice.Packets.Select(static packet => packet.RecipientId)) &&
              sent.Select(static item => item.Frame).Zip(closeBeforeNotice.Packets.Select(static packet => packet.Frame))
                .All(static pair => pair.First.AsSpan().SequenceEqual(pair.Second)),
            "Door relay SendAsync changed packet order, recipients, or bytes.");

        var closeAfterClear = LegacyBattleRoyaleDoorRelay.TryCreate(
            world, start, LegacyBattleRoyaleDoorCallTiming.AfterAreaClear, codec, clientTick: 42, keywordIndex: 8)
            ?? throw new InvalidOperationException("The second Battle Royale close-door relay was not created.");
        Check(closeAfterClear.Packets.All(static packet => packet.Kind == LegacyBattleRoyaleDoorPacketKind.GateCreateItem) &&
              closeAfterClear.Packets.Select(static packet => packet.GateItemId).Distinct().Order().SequenceEqual([13, 14]),
            "The second 7.69 SetColoseumDoor call did not resend only both CreateItem snapshots.");
        Check(mapGrid.GetTerrainHeight(2_603, 1_716) == 127 && mapGrid.GetTerrainHeight(2_603, 1_732) == 127,
            "The duplicate close call applied the ground-mask delta a second time.");

        var scheduleAtEnd = CreateSchedule();
        _ = scheduleAtEnd.Tick(new DateTime(2026, 10, 5, 19, 0, 0));
        _ = scheduleAtEnd.Tick(new DateTime(2026, 10, 5, 19, 5, 0));
        _ = scheduleAtEnd.Tick(new DateTime(2026, 10, 5, 19, 13, 0));
        var end = scheduleAtEnd.Tick(new DateTime(2026, 10, 5, 19, 14, 0))
            ?? throw new InvalidOperationException("The Battle Royale event-end transition was not produced.");
        var open = LegacyBattleRoyaleDoorRelay.TryCreate(
            world, end, LegacyBattleRoyaleDoorCallTiming.BeforeNotice, codec, clientTick: 43, keywordIndex: 9)
            ?? throw new InvalidOperationException("The Battle Royale open-door relay was not created.");
        Check(world.TryGetMapItem(10_013, out var opened13) && opened13 is { State: LegacyMapItemStateCodes.Open, Delay: 0 } &&
              world.TryGetMapItem(10_014, out var opened14) && opened14 is { State: LegacyMapItemStateCodes.Open, Delay: 0 } &&
              mapGrid.GetTerrainHeight(2_603, 1_716) == 109 && mapGrid.GetTerrainHeight(2_603, 1_732) == 109,
            "The 3-to-0 transition did not open both gates and remove their ground-mask deltas.");
        AssertGateCreateItem(codec, open, gateItemId: 13, state: LegacyMapItemStateCodes.Open);
        AssertGateCreateItem(codec, open, gateItemId: 14, state: LegacyMapItemStateCodes.Open);
        Check(open.Packets.Any(static packet => packet.Kind == LegacyBattleRoyaleDoorPacketKind.ActorAction && packet.EntityId == 3) &&
              open.Packets.All(static packet => packet.Kind != LegacyBattleRoyaleDoorPacketKind.ActorRemoval),
            "Opening a door did not preserve GetAction for its occupant or recreated the deleted NPC.");

        var soloHeight = new byte[LegacyMapGrid.HeightMapSize];
        Array.Fill(soloHeight, (byte)109);
        var soloWorld = new WorldHub(new LegacyMapGrid(soloHeight, new byte[LegacyMapGrid.AttributeMapSize]));
        soloWorld.ConfigureMapItems([
            new LegacyMapItemState(13, new LegacyItem(472, 0, 0, 0, 0, 0, 0), 2_603, 1_717, 1, 1, LegacyMapItemStateCodes.Open, 109, 90),
            new LegacyMapItemState(14, new LegacyItem(472, 0, 0, 0, 0, 0, 0), 2_603, 1_733, 1, 1, LegacyMapItemStateCodes.Open, 109, 90),
        ], LegacyGroundMaskTable.CreateOfficial());
        AddPlayer(soloWorld, [], 30, "BR_DOOR_SOLO_DELETE", 2_603, 1_733, equip0: 220);
        var soloSchedule = CreateSchedule();
        _ = soloSchedule.Tick(new DateTime(2026, 10, 5, 19, 0, 0));
        var soloStart = soloSchedule.Tick(new DateTime(2026, 10, 5, 19, 5, 0))
            ?? throw new InvalidOperationException("The solo Battle Royale start transition was not produced.");
        var soloPlan = LegacyBattleRoyaleDoorRelay.TryCreate(
            soloWorld, soloStart, LegacyBattleRoyaleDoorCallTiming.BeforeNotice, codec, clientTick: 44, keywordIndex: 10)
            ?? throw new InvalidOperationException("The solo Battle Royale close-door relay was not created.");
        Check(soloPlan.Packets.All(static packet => packet.Kind != LegacyBattleRoyaleDoorPacketKind.PartyRemoval || packet.EntityId != 30),
            "Deleting an unpartied door occupant emitted a spurious party-removal frame.");
    }

    public static void NonDoorTransitionsCannotMutateStaticDoors()
    {
        var world = new WorldHub();
        world.ConfigureMapItems([
            new LegacyMapItemState(13, new LegacyItem(472, 0, 0, 0, 0, 0, 0), 2_603, 1_717, 1, 1, LegacyMapItemStateCodes.Open, 0, 90),
            new LegacyMapItemState(14, new LegacyItem(472, 0, 0, 0, 0, 0, 0), 2_603, 1_733, 1, 1, LegacyMapItemStateCodes.Open, 0, 90),
        ]);
        var schedule = CreateSchedule();
        _ = schedule.Tick(new DateTime(2026, 10, 5, 19, 0, 0));
        _ = schedule.Tick(new DateTime(2026, 10, 5, 19, 5, 0));
        var reward = schedule.Tick(new DateTime(2026, 10, 5, 19, 13, 0))
            ?? throw new InvalidOperationException("The Battle Royale reward transition was not produced.");
        Check(LegacyBattleRoyaleDoorRelay.TryCreate(
                  world, reward, LegacyBattleRoyaleDoorCallTiming.BeforeNotice, LegacyFrameCodec.CreateDefault(), 44, 10) is null &&
              world.TryGetMapItem(10_013, out var gate) && gate is { State: LegacyMapItemStateCodes.Open, Delay: 90 },
            "A transition without a 7.69 door call mutated a static Coliseum gate.");
        Check(world.TryUpdateBattleRoyaleDoors(LegacyMapItemStateCodes.Closed) is null,
            "The authoritative door mutation accepted a state outside the BR open/locked pair.");
    }

    private static void AssertGateCreateItem(LegacyFrameCodec codec, LegacyBattleRoyaleDoorPlan plan, int gateItemId, int state)
    {
        var packet = plan.Packets.FirstOrDefault(packet =>
            packet.Kind == LegacyBattleRoyaleDoorPacketKind.GateCreateItem && packet.GateItemId == gateItemId);
        Check(packet is not null, $"The door relay omitted the CreateItem frame for gate slot {gateItemId}.");
        var frame = codec.Decode(packet!.Frame);
        Check(frame.Header.Type == CreateItemConfirmation.MessageType &&
              BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[4..]) == gateItemId + LegacyMapItemStateCodes.WireIdOffset &&
              frame.Payload.Span[15] == state,
            $"Gate slot {gateItemId} did not use the 7.69 CreateItem wire with state {state}.");
    }

    private static void AddPlayer(WorldHub world, ICollection<(int RecipientId, byte[] Frame)> sent, int connectionId, string name, short x, short y, short equip0 = 0)
    {
        Check(world.Enter(connectionId, name, (frame, _) =>
        {
            sent.Add((connectionId, frame.ToArray()));
            return ValueTask.CompletedTask;
        }), $"Battle Royale door participant {connectionId} could not enter the world.");
        Check(world.SetCharacterState(
                connectionId,
                characterSlot: 0,
                guildId: 0,
                clan: 1,
                guildLevel: 0,
                coin: 0,
                x,
                y,
                CreateMob(name, equip0),
                LegacyAccountSnapshot.ClassMasterMortal),
            $"Battle Royale door participant {connectionId} could not be initialized.");
    }

    private static byte[] CreateMob(string name, short equip0 = 0)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(50, 0, 0, 0, 0, 0, 0, 1_000, 1_000, 1_000, 250, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        new LegacyItem(equip0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        return mob;
    }

    private static LegacyBattleRoyaleSchedule CreateSchedule() => new(LegacyBattleRoyaleConfiguration.Parse("""
        { "BRItem": 700, "BRHour": 19, "ServerIndex": 0, "NumServerInGroup": 1, "TestServer": true, "LocalServer": false }
        """));

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

using System.Buffers.Binary;
using System.Collections;
using System.Reflection;
using WydCdk.Protocol;
using WydCdk.World;

internal static class BattleRoyaleEnvironmentalTests
{
    public static void EnvironmentalScheduleSelectsLegacyBandsAndCadence()
    {
        Check(LegacyBattleRoyaleEnvironmentalSchedule.TryCreate(2, 700, 6, 16) is null &&
              LegacyBattleRoyaleEnvironmentalSchedule.TryCreate(2, 700, 7, 15) is null &&
              LegacyBattleRoyaleEnvironmentalSchedule.TryCreate(1, 700, 7, 16) is null &&
              LegacyBattleRoyaleEnvironmentalSchedule.TryCreate(2, 0, 7, 16) is null,
            "The environmental schedule ignored its 16-second cadence, active-state, BRItem, or phase gate.");

        var narrow = LegacyBattleRoyaleEnvironmentalSchedule.TryCreate(2, 700, 7, 16)
            ?? throw new InvalidOperationException("The 7-minute BR phase did not produce the outer environmental bands.");
        Check(narrow.Areas.SequenceEqual([
                new LegacyBattleRoyaleEnvironmentalArea(2_608, 1_708, 2_622, 1_711),
                new LegacyBattleRoyaleEnvironmentalArea(2_608, 1_739, 2_622, 1_743),
            ]),
            "BR phases 7-8 did not select the two inclusive outer hazard bands.");

        var widened = LegacyBattleRoyaleEnvironmentalSchedule.TryCreate(2, 700, 9, 32)
            ?? throw new InvalidOperationException("BR phase 9 did not produce the widened environmental bands.");
        Check(widened.Areas.SequenceEqual([
                new LegacyBattleRoyaleEnvironmentalArea(2_608, 1_708, 2_622, 1_718),
                new LegacyBattleRoyaleEnvironmentalArea(2_608, 1_733, 2_622, 1_743),
            ]),
            "BR phase 9 did not widen both environmental hazard bands to their inclusive legacy bounds.");
    }

    public static void EnvironmentalDamageAndEffectFramesMatch769Wire()
    {
        var protocol = typeof(LegacyFrameCodec).Assembly;
        var damageType = protocol.GetType("WydCdk.Protocol.SetHpDamConfirmation");
        var effectType = protocol.GetType("WydCdk.Protocol.EnvironmentEffectConfirmation");
        Check(damageType is not null && effectType is not null,
            "The 7.69 SetHpDam and EnvEffect frame types are missing.");

        var codec = LegacyFrameCodec.CreateDefault();
        var damageFrame = Encode(
            damageType!,
            [3_000, -2_000],
            [codec, 123U, (byte)7, (ushort)77]);
        var damage = codec.Decode(damageFrame);
        Check(damageFrame.Length == 20 && damage.Header.Type == 0x018A && damage.Header.Id == 77 &&
              BinaryPrimitives.ReadInt32LittleEndian(damage.Payload.Span) == 3_000 &&
              BinaryPrimitives.ReadInt32LittleEndian(damage.Payload.Span[4..]) == -2_000,
            "MSG_SetHpDam did not preserve its 20-byte 7.69 wire, target ID, HP, or signed damage.");

        var effectFrame = Encode(
            effectType!,
            [2_608, 1_708, 2_622, 1_711, 32, 0],
            [codec, 124U, (byte)8, (ushort)30_000]);
        var effect = codec.Decode(effectFrame);
        Check(effectFrame.Length == 24 && effect.Header.Type == 0x03A2 && effect.Header.Id == 30_000 &&
              BinaryPrimitives.ReadInt16LittleEndian(effect.Payload.Span) == 2_608 &&
              BinaryPrimitives.ReadInt16LittleEndian(effect.Payload.Span[2..]) == 1_708 &&
              BinaryPrimitives.ReadInt16LittleEndian(effect.Payload.Span[4..]) == 2_622 &&
              BinaryPrimitives.ReadInt16LittleEndian(effect.Payload.Span[6..]) == 1_711 &&
              BinaryPrimitives.ReadInt16LittleEndian(effect.Payload.Span[8..]) == 32 &&
              BinaryPrimitives.ReadInt16LittleEndian(effect.Payload.Span[10..]) == 0,
            "MSG_EnvEffect did not preserve the 7.69 server scene ID, area, type 32, or parameter 0.");
    }

    public static void WorldAppliesEnvironmentalDamageOnlyInsideActiveBands()
    {
        var world = new WorldHub();
        AddPlayer(world, 1, 2_608, 1_708, hp: 5_000);
        AddPlayer(world, 2, 2_622, 1_711, hp: 2_000, mp: 1_500);
        AddPlayer(world, 3, 2_607, 1_708, hp: 5_000);
        AddPlayer(world, 4, 2_615, 1_712, hp: 5_000);
        AddPlayer(world, 5, 2_615, 1_738, hp: 5_000);
        AddPlayer(world, 6, 2_622, 1_743, hp: 1_500);
        AddPlayer(world, 7, 2_610, 1_710, hp: 1);
        AddPlayer(world, 8, 2_611, 1_709, hp: 0);
        AddPlayer(world, 9, 2_631, 1_725, hp: 5_000);
        AddPlayer(world, 10, 2_632, 1_725, hp: 5_000);
        world.SetBattleRoyaleState(2, 700);

        var step = LegacyBattleRoyaleEnvironmentalSchedule.TryCreate(2, 700, 7, 16)
            ?? throw new InvalidOperationException("The narrow environmental step fixture was not produced.");
        var snapshot = world.TryApplyBattleRoyaleEnvironmentalDamage(step)
            ?? throw new InvalidOperationException("Active Battle Royale environmental damage was not applied.");

        Check(snapshot.DamageTargets.Select(static target => target.ConnectionId).SequenceEqual([1, 2, 7, 6]),
            $"Environmental damage target IDs were [{string.Join(',', snapshot.DamageTargets.Select(static target => target.ConnectionId))}], expected [1,2,7,6] in legacy band order.");
        Check(snapshot.DamageTargets.Select(static target => (target.ConnectionId, target.CurrentHp, target.Damage)).SequenceEqual([
                (1, 3_000, -2_000),
                (2, 1, -1_999),
                (7, 1, 0),
                (6, 1, -1_499),
            ]),
            "Environmental damage did not preserve the 2,000 reduction, approved HP floor, or zero-damage living tick.");
        Check(ReadHp(world, 3) == 5_000 && ReadHp(world, 4) == 5_000 && ReadHp(world, 5) == 5_000 &&
              ReadHp(world, 8) == 0 && ReadHp(world, 9) == 5_000 && ReadHp(world, 10) == 5_000,
            "Environmental damage changed an out-of-band, dead, or effect-only participant.");
        Check(ReadScore(world, 2).CurrentScore.Mp == 1_000,
            "The 7.69 SetReqMp side effect did not clamp MP above MaxMp.");
        Check(world.TryGetResourceState(1, out _, out var requestedHp, out _) && requestedHp == 5_000,
            "The 7.69 SetReqHp helper unexpectedly rewrote the player's requested HP.");
        Check(snapshot.Effects.Count == 2 &&
              snapshot.Effects[0].RecipientIds.Contains(9) && !snapshot.Effects[0].RecipientIds.Contains(10) &&
              snapshot.Effects[1].RecipientIds.Contains(9) && !snapshot.Effects[1].RecipientIds.Contains(10),
            "EnvEffect recipients did not use the inclusive 33x33 views around both legacy area centers.");

        world.SetBattleRoyaleState(1, 700);
        Check(world.TryApplyBattleRoyaleEnvironmentalDamage(step) is null && ReadHp(world, 1) == 3_000,
            "The WorldHub environmental mutation ran after BR state left phase 2.");
        world.SetBattleRoyaleState(2, 0);
        Check(world.TryApplyBattleRoyaleEnvironmentalDamage(step) is null && ReadHp(world, 1) == 3_000,
            "The WorldHub environmental mutation ran while BRItem was disabled.");
    }

    public static void EnvironmentalRelayUsesLegacyFramesViewsAndOrder()
    {
        var relayType = typeof(LegacyBattleRoyaleSchedule).Assembly.GetType(
            "WydCdk.World.LegacyBattleRoyaleEnvironmentalRelay");
        Check(relayType is not null, "The Battle Royale environmental frame relay is missing.");
        var tryCreate = relayType!.GetMethod("TryCreate", BindingFlags.Public | BindingFlags.Static);
        var sendAsync = relayType.GetMethod("SendAsync", BindingFlags.Public | BindingFlags.Static);
        if (tryCreate is null || sendAsync is null)
            throw new InvalidOperationException("The Battle Royale environmental relay is missing its plan or send operation.");

        var sent = new List<(int RecipientId, byte[] Frame)>();
        var world = new WorldHub();
        AddPlayer(world, 1, 2_608, 1_708, hp: 5_000, sent: sent);
        AddPlayer(world, 2, 2_624, 1_724, hp: 5_000, sent: sent);
        AddPlayer(world, 3, 2_631, 1_725, hp: 5_000, sent: sent);
        AddPlayer(world, 4, 2_632, 1_725, hp: 5_000, sent: sent);
        AddPlayer(world, 5, 2_622, 1_743, hp: 1_500, sent: sent);
        world.SetBattleRoyaleState(2, 700);
        var step = LegacyBattleRoyaleEnvironmentalSchedule.TryCreate(2, 700, 7, 16)
            ?? throw new InvalidOperationException("The relay environmental step fixture was not produced.");
        var codec = LegacyFrameCodec.CreateDefault();
        var plan = tryCreate.Invoke(null, [world, step, codec, 456U, (byte)9])
            ?? throw new InvalidOperationException("The environmental relay did not create a frame plan.");

        var packets = ReadRelayPackets(plan);
        Check(packets.Select(static packet => packet.RecipientId).SequenceEqual([1, 2, 5, 1, 2, 3, 3, 5]),
            $"Environmental relay recipient order was [{string.Join(',', packets.Select(static packet => packet.RecipientId))}], expected [1,2,5,1,2,3,3,5].");
        var damagePackets = packets.Take(3).ToArray();
        Check(damagePackets.Select(packet => (int)codec.Decode(packet.Frame).Header.Type).All(type => type == 0x018A) &&
              damagePackets.Select(packet => (int)codec.Decode(packet.Frame).Header.Id).SequenceEqual([1, 1, 5]) &&
              BinaryPrimitives.ReadInt32LittleEndian(codec.Decode(damagePackets[0].Frame).Payload.Span) == 3_000 &&
              BinaryPrimitives.ReadInt32LittleEndian(codec.Decode(damagePackets[0].Frame).Payload.Span[4..]) == -2_000 &&
              BinaryPrimitives.ReadInt32LittleEndian(codec.Decode(damagePackets[2].Frame).Payload.Span) == 1 &&
              BinaryPrimitives.ReadInt32LittleEndian(codec.Decode(damagePackets[2].Frame).Payload.Span[4..]) == -1_499,
            "Environmental damage relay frames did not carry each changed HP and signed delta in target-band order.");
        var effectPackets = packets.Skip(3).ToArray();
        Check(effectPackets.Select(packet => (int)codec.Decode(packet.Frame).Header.Type).All(type => type == 0x03A2) &&
              effectPackets.Select(packet => (int)codec.Decode(packet.Frame).Header.Id).All(id => id == 30_000) &&
              effectPackets.Select(packet => (int)BinaryPrimitives.ReadInt16LittleEndian(codec.Decode(packet.Frame).Payload.Span)).SequenceEqual([2_608, 2_608, 2_608, 2_608, 2_608]),
            "Environmental effect frames did not follow damage frames with the 7.69 server-scene wire.");

        var sendResult = sendAsync.Invoke(null, [world, plan, CancellationToken.None]);
        if (sendResult is not ValueTask sendTask)
            throw new InvalidOperationException("The environmental relay did not return its asynchronous send operation.");
        sendTask.AsTask().GetAwaiter().GetResult();
        Check(sent.Count == packets.Length &&
              sent.Select(static packet => packet.RecipientId).SequenceEqual(packets.Select(static packet => packet.RecipientId)) &&
              sent.Select(static packet => packet.Frame).Zip(packets.Select(static packet => packet.Frame))
                  .All(static pair => pair.First.AsSpan().SequenceEqual(pair.Second)),
            "Environmental SendAsync changed the planned recipient order or frame bytes.");
    }

    private static void AddPlayer(
        WorldHub world,
        int connectionId,
        short x,
        short y,
        int hp,
        int mp = 500,
        ICollection<(int RecipientId, byte[] Frame)>? sent = null)
    {
        Check(world.Enter(connectionId, $"BR_ENV_{connectionId}", (frame, _) =>
        {
            sent?.Add((connectionId, frame.ToArray()));
            return ValueTask.CompletedTask;
        }),
            $"Environmental participant {connectionId} could not enter the world.");
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(50, 0, 0, 0, 0, 0, 0, 10_000, 1_000, hp, mp, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(world.SetCharacterState(
                connectionId, characterSlot: 0, guildId: 0, clan: 1, guildLevel: 0, coin: 0, x, y, mob,
                LegacyAccountSnapshot.ClassMasterMortal),
            $"Environmental participant {connectionId} could not be initialized.");
    }

    private static (int RecipientId, byte[] Frame)[] ReadRelayPackets(object plan)
    {
        var packets = plan.GetType().GetProperty("Packets")?.GetValue(plan) as IEnumerable
            ?? throw new InvalidOperationException("The environmental relay plan is missing its packets.");
        return packets.Cast<object>().Select(packet => (
            ReadInt(packet, "RecipientId"),
            ReadFrame(packet))).ToArray();
    }

    private static int ReadInt(object instance, string propertyName) =>
        (int)(instance.GetType().GetProperty(propertyName)?.GetValue(instance)
            ?? throw new InvalidOperationException($"Relay packet is missing '{propertyName}'."));

    private static byte[] ReadFrame(object instance) =>
        (byte[])(instance.GetType().GetProperty("Frame")?.GetValue(instance)
            ?? throw new InvalidOperationException("Relay packet is missing its frame bytes."));

    private static int ReadHp(WorldHub world, int connectionId) => ReadScore(world, connectionId).CurrentScore.Hp;

    private static LegacyMobCombatState ReadScore(WorldHub world, int connectionId)
    {
        Check(world.TryGetCharacterSnapshot(connectionId, out var mob, out _, out _) && mob is not null,
            $"Environmental participant {connectionId} has no character snapshot.");
        return LegacyMobCombatState.Read(mob!);
    }

    private static byte[] Encode(Type confirmationType, object?[] constructorArguments, object?[] frameArguments)
    {
        var confirmation = confirmationType.GetConstructors().Single().Invoke(constructorArguments);
        var toFrame = confirmationType.GetMethod("ToFrame", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"'{confirmationType.Name}' is missing ToFrame.");
        return (byte[])(toFrame.Invoke(confirmation, frameArguments)
            ?? throw new InvalidOperationException($"'{confirmationType.Name}.ToFrame' returned no wire frame."));
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

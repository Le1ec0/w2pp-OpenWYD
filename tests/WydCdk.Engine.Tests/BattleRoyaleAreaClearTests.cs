using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class BattleRoyaleAreaClearTests
{
    public static void AreaClearRecallsOnlyPlayersInTheInclusiveLevelBand()
    {
        var schedule = new LegacyBattleRoyaleSchedule(LegacyBattleRoyaleConfiguration.Parse("""
            { "BRItem": 700, "BRHour": 19, "ServerIndex": 0, "NumServerInGroup": 1, "TestServer": true, "LocalServer": false }
            """));
        var transition = schedule.Tick(new DateTime(2026, 10, 5, 19, 0, 0)) ??
            throw new InvalidOperationException("The Battle Royale ready transition was not produced.");
        var outbound = new List<(int RecipientId, ushort MessageType, DecodedFrame Frame)>();
        var world = new WorldHub();

        AddPlayer(world, outbound, 1, "BR_CLEAR_BELOW_MIN", 99, 500, 2604, 1708);
        AddPlayer(world, outbound, 2, "BR_CLEAR_DEAD_MIN", 100, 0, 2604, 1708);
        AddPlayer(world, outbound, 3, "BR_CLEAR_ALIVE_MAX", 400, 250, 2648, 1744);
        AddPlayer(world, outbound, 4, "BR_CLEAR_ABOVE_MAX", 401, 500, 2621, 1726);
        AddPlayer(world, outbound, 5, "BR_CLEAR_OUTSIDE", 200, 500, 2649, 1726);
        AddPlayer(world, outbound, 6, "BR_CLEAR_VIEWER", 50, 500, 2605, 1708);

        var codec = LegacyFrameCodec.CreateDefault();
        var plan = LegacyBattleRoyaleAreaClearRelay.TryCreate(
            world, transition, codec, clientTick: 33, keywordIndex: 16, cityRandomX: 0, cityRandomY: 0);
        Check(plan is not null && plan.Actors.Select(static actor => actor.ConnectionId).SequenceEqual([2, 3]),
            "Battle Royale ClearAreaLevel did not select only levels 100..400 at inclusive arena boundaries.");

        Check(world.TryGetCombatState(2, out var deadState) && deadState!.CurrentScore.Hp == 2 &&
            world.TryGetCombatState(3, out var livingState) && livingState!.CurrentScore.Hp == 250,
            "Battle Royale area clear did not revive a dead eligible player to 2 HP or preserve an eligible living player's HP.");
        var hasDeadPosition = world.TryGetCharacterSnapshot(2, out _, out var deadX, out var deadY);
        var hasLivingPosition = world.TryGetCharacterSnapshot(3, out _, out var livingX, out var livingY);
        Check(hasDeadPosition && (deadX != 2604 || deadY != 1708) &&
            hasLivingPosition && (livingX != 2648 || livingY != 1744),
            "Eligible Battle Royale players were not recalled from the coliseum.");
        Check(world.TryGetCharacterSnapshot(1, out _, out var belowX, out var belowY) && belowX == 2604 && belowY == 1708 &&
            world.TryGetCharacterSnapshot(4, out _, out var aboveX, out var aboveY) && aboveX == 2621 && aboveY == 1726 &&
            world.TryGetCharacterSnapshot(5, out _, out var outsideX, out var outsideY) && outsideX == 2649 && outsideY == 1726,
            "Battle Royale area clear moved a below-minimum, above-maximum, or out-of-area player.");

        var sessions = new LoginSessionRegistry();
        foreach (var connectionId in Enumerable.Range(1, 6))
            Check(sessions.Open(connectionId), $"Battle Royale clear login session {connectionId} could not be opened.");
        LegacyBattleRoyaleAreaClearRelay.SendAsync(world, sessions, plan!, CancellationToken.None).AsTask().GetAwaiter().GetResult();

        Check(sessions.TryGet(2, out var deadSession) && deadSession!.PositionX == deadX && deadSession.PositionY == deadY &&
            sessions.TryGet(3, out var livingSession) && livingSession!.PositionX == livingX && livingSession.PositionY == livingY,
            "The Battle Royale recall did not synchronize the login-session positions with WorldHub.");
        var deadFrames = outbound.Where(static message => message.RecipientId == 2).ToArray();
        Check(deadFrames.Length >= 2 &&
            deadFrames[0].MessageType == UpdateScoreConfirmationV769.MessageType &&
            BinaryPrimitives.ReadInt32LittleEndian(deadFrames[0].Frame.Payload.Span[(UpdateScoreConfirmationV769.ScoreOffset + 24)..]) == 2 &&
            BinaryPrimitives.ReadInt32LittleEndian(deadFrames[0].Frame.Payload.Span[UpdateScoreConfirmationV769.ReqHpOffset..]) == 2 &&
            deadFrames[1].MessageType == ActionRequest.MessageType,
            "The dead player's 2-HP score was not sent before the recall Action.");
        Check(outbound.Any(static message => message.RecipientId == 1 && message.MessageType == ActionRequest.MessageType) &&
            outbound.Any(static message => message.RecipientId == 6 && message.MessageType == ActionRequest.MessageType) &&
            !outbound.Any(static message => message.RecipientId == 5 && message.MessageType == ActionRequest.MessageType),
            "Battle Royale recall Actions were not relayed only to the actor's spatial viewers.");

        var invalidWorld = new WorldHub();
        AddPlayer(invalidWorld, [], 1, "BR_CLEAR_INVALID", 200, 500, 2621, 1726);
        var invalidTransition = transition with { PreviousState = 2, State = 3 };
        var invalidPlan = LegacyBattleRoyaleAreaClearRelay.TryCreate(
            invalidWorld, invalidTransition, codec, clientTick: 33, keywordIndex: 16, cityRandomX: 0, cityRandomY: 0);
        Check(invalidPlan is null && invalidWorld.TryGetCharacterSnapshot(1, out _, out var invalidX, out var invalidY) &&
            invalidX == 2621 && invalidY == 1726,
            "A non-ready/start Battle Royale transition cleared the area or recalled a player.");
    }

    private static void AddPlayer(
        WorldHub world,
        List<(int RecipientId, ushort MessageType, DecodedFrame Frame)> outbound,
        int connectionId,
        string name,
        int level,
        int hp,
        short x,
        short y)
    {
        var codec = LegacyFrameCodec.CreateDefault();
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(level, 0, 0, 0, 0, 0, 0, 1_000, 1_000, hp, 250, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(world.Enter(connectionId, name, (frame, _) =>
        {
            var decoded = codec.Decode(frame.Span);
            outbound.Add((connectionId, decoded.Header.Type, decoded));
            return ValueTask.CompletedTask;
        }), $"Battle Royale area-clear participant {connectionId} was not registered.");
        Check(world.SetCharacterState(
            connectionId,
            characterSlot: 0,
            guildId: 0,
            clan: 1,
            guildLevel: 0,
            coin: 0,
            x,
            y,
            mob,
            LegacyAccountSnapshot.ClassMasterMortal),
            $"Battle Royale area-clear participant {connectionId} state was not initialized.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

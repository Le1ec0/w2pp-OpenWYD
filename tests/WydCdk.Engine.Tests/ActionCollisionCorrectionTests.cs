using WydCdk.Protocol;
using WydCdk.World;

internal static class ActionCollisionCorrectionTests
{
    public static void RejectedCollisionRecordsMovementType()
    {
        var sessions = new LoginSessionRegistry();
        Check(sessions.Open(1), "Could not open movement session.");
        var login = new AccountLoginRequest("TEST1", "fixture", new byte[52], 7670, 0, new uint[4]);
        Check(sessions.BeginAccountLogin(1, login) == LoginTransitionResult.Accepted &&
            sessions.CompleteAccountLogin(1, "TEST1") == LoginTransitionResult.Accepted &&
            sessions.BeginCharacterWait(1) == LoginTransitionResult.Accepted &&
            sessions.CompleteCharacterLogin(1, 0, 100, 100) == LoginTransitionResult.Accepted,
            "Could not put movement session into USER_PLAY.");

        Check(sessions.TryRecordMovementType(1, ActionRequest.StopMessageType),
            "The playing session rejected its movement-type update.");

        var repeatedStop = new ActionRequest(100, 100, 0, 2, new byte[24], 101, 100)
        {
            WireType = ActionRequest.StopMessageType,
        };
        Check(sessions.TryApplyMovement(1, repeatedStop) == MovementResult.RepeatedStop,
            "The next Action2 was not rejected after the occupied Action2 correction.");
    }

    public static void Run()
    {
        var world = new WorldHub();
        EnterCharacter(world, 1, 100, 100);
        EnterCharacter(world, 2, 102, 100);
        EnterCharacter(world, 3, 101, 99);

        var route = new byte[24];
        route[0] = (byte)'6';
        var request = new ActionRequest(100, 100, Effect: 1, Speed: 2, route, 102, 100)
        {
            WireType = ActionRequest.StopMessageType,
        };

        Check(world.TryCorrectOccupiedAction(1, request, out var response), "An occupied Action destination was not corrected.");
        Check(response is not null && response.Effect == 0 && response.TargetX == 101 && response.TargetY == 99,
            "The occupied-tile correction did not preserve the final 7.69 route endpoint and Effect=0.");
        Check(response!.WireType == ActionRequest.StopMessageType && response.Route[0] == (byte)'3' && response.Route.Skip(1).All(static value => value == 0),
            "The occupied-tile correction changed the Action opcode or route bytes.");
        Check(world.TryGetCharacterSnapshot(1, out _, out var actorX, out var actorY) && actorX == 100 && actorY == 100,
            "A private Effect=0 correction changed the actor's authoritative position.");
    }

    private static void EnterCharacter(WorldHub world, int connectionId, short x, short y)
    {
        Check(world.Enter(connectionId, $"TEST{connectionId}", static (_, _) => ValueTask.CompletedTask),
            $"Could not enter fixture participant {connectionId}.");
        Check(world.SetCharacterState(connectionId, 0, 0, 0, 0, 0, x, y, new byte[LegacyAccountSnapshot.CharacterStride]),
            $"Could not position fixture participant {connectionId}.");
    }

    private static void Check(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }
}

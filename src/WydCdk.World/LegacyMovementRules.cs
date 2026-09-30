namespace WydCdk.World;

/// <summary>Pure movement gates shared with the legacy <c>_MSG_Action</c> handler.</summary>
public static class LegacyMovementRules
{
    public enum ActionGateResult
    {
        CharacterDead,
        AuthoritativeStateUnavailable,
        Ready,
    }

    // TMSrv/W2PP Basedef.h: VIEWGRIDX and VIEWGRIDY.
    public const int ViewGridX = 33;
    public const int ViewGridY = 33;

    /// <summary>Gets the allowed speed nibble only when the server has an authoritative AttackRun score.</summary>
    public static bool TryGetAllowedSpeed(int? attackRun, out int allowedSpeed)
    {
        if (attackRun is null)
        {
            allowedSpeed = 0;
            return false;
        }

        allowedSpeed = attackRun.Value & 0xF;
        return true;
    }

    /// <summary>7.69 high-severity Action detection; a high score alone is not a violation.</summary>
    public static bool RequiresAccountAnalysis(int requestedSpeed, int attackRun) =>
        requestedSpeed > (attackRun & 0xF) && (requestedSpeed >= 20 || attackRun >= 300);

    /// <summary>Resolves the HP-zero gate before movement-speed validation for Action packets.</summary>
    public static ActionGateResult ResolveActionPreconditions(int? hp, int? attackRun, out int allowedSpeed)
    {
        allowedSpeed = 0;
        if (hp == 0)
            return ActionGateResult.CharacterDead;
        if (hp is null || !TryGetAllowedSpeed(attackRun, out allowedSpeed))
            return ActionGateResult.AuthoritativeStateUnavailable;
        return ActionGateResult.Ready;
    }

    /// <summary>Action packets may continue only while the connection remains in USER_PLAY.</summary>
    public static bool ShouldContinueAction(LoginSessionState? state) => state is LoginSessionState.Playing;

    /// <summary>The legacy private correction is sent only beyond twice the inclusive view window.</summary>
    public static bool RequiresPositionCorrection(int positionX, int positionY, int targetX, int targetY) =>
        Math.Abs((long)targetX - positionX) > ViewGridX * 2
        || Math.Abs((long)targetY - positionY) > ViewGridY * 2;

    /// <summary>
    /// Checks the target against the server-authoritative position. The legacy
    /// handler deliberately ignores the client-reported PosX/PosY here.
    /// </summary>
    public static bool IsTargetWithinViewGrid(int positionX, int positionY, int targetX, int targetY) =>
        targetX >= positionX - ViewGridX && targetX <= positionX + ViewGridX
        && targetY >= positionY - ViewGridY && targetY <= positionY + ViewGridY;
}

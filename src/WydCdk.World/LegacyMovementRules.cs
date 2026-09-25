namespace WydCdk.World;

/// <summary>Pure movement gates shared with the legacy <c>_MSG_Action</c> handler.</summary>
public static class LegacyMovementRules
{
    // TMSrv/W2PP Basedef.h: VIEWGRIDX and VIEWGRIDY.
    public const int ViewGridX = 33;
    public const int ViewGridY = 33;

    /// <summary>
    /// Checks the target against the server-authoritative position. The legacy
    /// handler deliberately ignores the client-reported PosX/PosY here.
    /// </summary>
    public static bool IsTargetWithinViewGrid(int positionX, int positionY, int targetX, int targetY) =>
        targetX >= positionX - ViewGridX && targetX <= positionX + ViewGridX
        && targetY >= positionY - ViewGridY && targetY <= positionY + ViewGridY;
}

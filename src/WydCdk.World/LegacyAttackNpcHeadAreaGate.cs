namespace WydCdk.World;

/// <summary>Protects legacy NPC heads 219 and 220 in the 7.69 map tile (26,12) attack gate.</summary>
public static class LegacyAttackNpcHeadAreaGate
{
    public const short HeadIndex219 = 219;
    public const short HeadIndex220 = 220;
    public const short ProtectedTileX = 26;
    public const short ProtectedTileY = 12;
    public const short TileSize = 128;

    public static bool ShouldBlock(bool targetIsNonPlayer, short targetHeadIndex, short targetX, short targetY) =>
        targetIsNonPlayer &&
        (targetHeadIndex is HeadIndex219 or HeadIndex220) &&
        targetX / TileSize == ProtectedTileX &&
        targetY / TileSize == ProtectedTileY;
}

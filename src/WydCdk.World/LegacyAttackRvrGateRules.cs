namespace WydCdk.World;

/// <summary>Authoritative snapshot consumed by the early Attack gates.</summary>
public sealed record LegacyAttackState(
    LegacyMobCombatState CombatState,
    short PositionX,
    short PositionY,
    byte Clan);

/// <summary>7.69 TMSrv restriction on attacks by clanless characters in RvR sector (13,15).</summary>
public static class LegacyAttackRvrGateRules
{
    public const int SectorSize = 128;
    public const int RvrSectorX = 13;
    public const int RvrSectorY = 15;
    public const string BlockedMessage = "Você não pode atacar nessa área.";

    public static bool ShouldReject(int positionX, int positionY, byte clan) =>
        positionX / SectorSize == RvrSectorX &&
        positionY / SectorSize == RvrSectorY &&
        clan == 0;
}

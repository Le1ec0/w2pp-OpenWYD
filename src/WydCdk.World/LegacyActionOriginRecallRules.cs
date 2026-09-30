namespace WydCdk.World;

/// <summary>Origin-sector recall gates from the 7.69 TMSrv <c>Exec_MSG_Action</c> handler.</summary>
public static class LegacyActionOriginRecallRules
{
    private const int SectorSize = 128;
    private const int CelestialRecallLevel = 180;

    public static bool ShouldRecall(short originX, short originY, short classMaster, int level, byte clan)
    {
        var sectorX = originX / SectorSize;
        var sectorY = originY / SectorSize;
        var celestialRestrictedSector =
            (sectorX == 9 && sectorY == 1) ||
            (sectorX == 8 && sectorY == 2) ||
            (sectorX == 10 && sectorY == 2);

        if (celestialRestrictedSector &&
            (classMaster is LegacyExperienceMath.ClassMasterCelestial or
                LegacyExperienceMath.ClassMasterCelestialCs or
                LegacyExperienceMath.ClassMasterSCelestial) &&
            level >= CelestialRecallLevel)
            return true;

        return sectorX == 13 && sectorY == 15 && clan == 0;
    }
}

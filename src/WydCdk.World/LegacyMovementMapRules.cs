namespace WydCdk.World;

/// <summary>Map attributes used by the legacy <c>_MSG_Action</c> movement gate.</summary>
public static class LegacyMovementMapRules
{
    public const byte NewbieZoneAttribute = 0x80;
    public const byte GuildZoneAttribute = 0x20;
    public const int FreeExperienceLevel = 35;
    public const int MaxLevel = 399;
    public const int MaxNewbieZoneLevel = 999;

    private static readonly LegacyGuildZoneBounds[] guildZones =
    [
        new(2052, 2052, 2171, 2163), // Armia
        new(2432, 1672, 2675, 1767), // Azran
        new(2448, 1966, 2476, 2024), // Erion
        new(3605, 3090, 3690, 3260), // Nippleheim
        new(1036, 1700, 1072, 1760), // Noatum
    ];

    /// <summary>Returns the city guild-zone index used by TMSrv's <c>BASE_GetGuild(x,y)</c>.</summary>
    public static int FindGuildZone(int x, int y)
    {
        for (var index = 0; index < guildZones.Length; index++)
        {
            var zone = guildZones[index];
            if (x >= zone.X1 && x <= zone.X2 && y >= zone.Y1 && y <= zone.Y2)
                return index;
        }

        return guildZones.Length;
    }

    /// <summary>
    /// Applies only the target-cell restrictions from the 7.69/W2PP action handler.
    /// Recall, notice delivery, and position mutation remain caller responsibilities.
    /// </summary>
    public static LegacyMovementMapRestriction GetRestriction(
        byte mapAttribute,
        int level,
        short classMaster,
        int guildId,
        int targetX,
        int targetY,
        LegacyGuildZoneState? guildState)
    {
        if ((mapAttribute & NewbieZoneAttribute) != 0
            && ((level >= FreeExperienceLevel && level <= MaxNewbieZoneLevel)
                || classMaster != LegacyAccountSnapshot.ClassMasterMortal))
            return LegacyMovementMapRestriction.NewbieZone;

        if ((mapAttribute & GuildZoneAttribute) != 0 && level <= MaxLevel)
        {
            var zone = FindGuildZone(targetX, targetY);
            if (zone >= 0 && zone < LegacyGuildZoneState.ZoneCount && guildState is not null && guildId != guildState.GetChargeGuild(zone))
                return LegacyMovementMapRestriction.GuildZone;
        }

        return LegacyMovementMapRestriction.None;
    }

    private readonly record struct LegacyGuildZoneBounds(int X1, int Y1, int X2, int Y2);
}

public enum LegacyMovementMapRestriction
{
    None,
    NewbieZone,
    GuildZone,
}

namespace WydCdk.World;

/// <summary>Reproduces the player-target PK gate in TMProject 7.69 TMSrv MSG_Attack.</summary>
public static class LegacySkillPkGate
{
    private static readonly LegacyPkArea[] arenaAreas =
    [
        new(197, 213, 238, 230),
        new(197, 149, 238, 166),
        new(141, 213, 182, 230),
        new(141, 149, 182, 166),
        new(4_000, 4_000, 4_010, 4_010),
    ];

    public static LegacySkillPkGateResult Resolve(
        int attackerPkPoint,
        int targetPkPoint,
        int attackerGuild,
        int targetGuild,
        short attackerX,
        short attackerY,
        int skillId,
        int damage,
        LegacyCombatWorldState worldState)
    {
        ArgumentNullException.ThrowIfNull(worldState);

        if (!IsPkProtected(attackerX, attackerY, attackerGuild, targetGuild, worldState))
            return new(damage, PkPointGateBlocked: false, SendCannotKillNotice: false, MarkAttackerGuilty: false);

        if (attackerPkPoint <= 10 && targetPkPoint > 10)
            return new(0, PkPointGateBlocked: true, SendCannotKillNotice: true, MarkAttackerGuilty: false);

        // Parentheses preserve the source expression's C++ precedence:
        // (target PK > 10 && damage > 0) || skillnum == 6.
        var markAttackerGuilty = (targetPkPoint > 10 && damage > 0) || skillId == 6;
        return new(damage, PkPointGateBlocked: false, SendCannotKillNotice: false, MarkAttackerGuilty: markAttackerGuilty);
    }

    public static LegacySkillPkGateResult ResolveSummon(
        int attackerPkPoint,
        int? summonerPkPoint,
        int attackerGuild,
        int summonerGuild,
        short attackerX,
        short attackerY,
        int skillId,
        int damage,
        LegacyCombatWorldState worldState)
    {
        ArgumentNullException.ThrowIfNull(worldState);

        // TMProject 7.69 resolves a Clan 4 mob to its summoner only while that
        // player is USER_PLAY. With no live owner, idx remains an NPC and the
        // player PK block is skipped altogether.
        if (summonerPkPoint is null || damage <= 0)
            return new(damage, PkPointGateBlocked: false, SendCannotKillNotice: false, MarkAttackerGuilty: false);

        return Resolve(
            attackerPkPoint,
            summonerPkPoint.Value,
            attackerGuild,
            summonerGuild,
            attackerX,
            attackerY,
            skillId,
            damage,
            worldState);
    }

    private static bool IsPkProtected(
        short attackerX,
        short attackerY,
        int attackerGuild,
        int targetGuild,
        LegacyCombatWorldState worldState)
    {
        var inArena = arenaAreas.Any(area => area.Contains(attackerX, attackerY));
        var inVillage = LegacyMovementMapRules.FindGuildZone(attackerX, attackerY) < LegacyGuildZoneState.ZoneCount;
        var mapX = attackerX >> 7;
        var mapY = attackerY >> 7;
        var inPkMap = (mapX == 1 && mapY == 31) || (mapX == 19 && mapY == 30);
        var castleWarException = worldState.CastleState != 0 && mapX == 8 && mapY == 13;
        var guildWarException = IsReciprocalGuildWar(attackerGuild, targetGuild, worldState.GuildWarPartners);

        return !inArena && !inVillage && !inPkMap && !castleWarException && !guildWarException;
    }

    private static bool IsReciprocalGuildWar(
        int attackerGuild,
        int targetGuild,
        IReadOnlyDictionary<int, int>? guildWarPartners)
    {
        const int maxGuildExclusive = 65_536;
        return attackerGuild is > 0 and < maxGuildExclusive &&
               targetGuild is > 0 and < maxGuildExclusive &&
               guildWarPartners is not null &&
               guildWarPartners.TryGetValue(attackerGuild, out var attackerPartner) && attackerPartner == targetGuild &&
               guildWarPartners.TryGetValue(targetGuild, out var targetPartner) && targetPartner == attackerGuild;
    }

    private readonly record struct LegacyPkArea(int X1, int Y1, int X2, int Y2)
    {
        public bool Contains(int x, int y) => x >= X1 && x <= X2 && y >= Y1 && y <= Y2;
    }
}

public sealed record LegacySkillPkGateResult(
    int Damage,
    bool PkPointGateBlocked,
    bool SendCannotKillNotice,
    bool MarkAttackerGuilty);

public static class LegacySkillPkNotice
{
    public static string CannotKillUser(int attackerPkPoint) =>
        $"Você não pode mais derrotar jogadores. (Atual : {attackerPkPoint})";
}

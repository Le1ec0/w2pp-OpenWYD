namespace WydCdk.World;

/// <summary>7.69 MSG_Attack bParty eligibility across registered player and NPC targets.</summary>
public static class LegacyPartySkillTargetGate
{
    public const int CrackErrorPoints = 10;
    public const int CrackErrorType = 27;

    public const short BattleRoyaleMinX = 2_604;
    public const short BattleRoyaleMaxX = 2_648;
    public const short BattleRoyaleMinY = 1_708;
    public const short BattleRoyaleMaxY = 1_744;

    public static int ResolveAttackerGuildId(
        int attackerGuildId,
        bool attackerGuildDisabled,
        int targetGuildId,
        int battleRoyaleState,
        int battleRoyaleItem,
        short attackerTargetX,
        short attackerTargetY)
    {
        var effectiveGuildId = attackerGuildDisabled ? 0 : attackerGuildId;
        if (effectiveGuildId == 0 && targetGuildId == 0)
            effectiveGuildId = -1;

        if (battleRoyaleState != 0 &&
            battleRoyaleItem > 0 &&
            attackerTargetX is >= BattleRoyaleMinX and <= BattleRoyaleMaxX &&
            attackerTargetY is >= BattleRoyaleMinY and <= BattleRoyaleMaxY)
            effectiveGuildId = -1;

        return effectiveGuildId;
    }

    public static bool IsBlocked(
        int partyCheck,
        int attackerPartyLeaderConnectionId,
        int targetPartyLeaderConnectionId,
        int attackerGuildId,
        int targetGuildId)
    {
        if (partyCheck == 0)
            return false;

        var sameParty = attackerPartyLeaderConnectionId == targetPartyLeaderConnectionId;
        var sameGuild = attackerGuildId != 0 && attackerGuildId == targetGuildId;
        return !sameParty && !sameGuild;
    }
}

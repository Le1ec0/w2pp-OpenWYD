using WydCdk.World;

namespace WydCdk.Server;

/// <summary>Maps the 7.69 InstanceType 9/12 rejection results to their private client notices.</summary>
public static class LegacySkillSummonNoticeMapper
{
    public static string? GetMessage(int instanceType, LegacySkillAttackResult result)
    {
        // Language.txt:33 _NN_Party_Full_Cant_Summon, sent before the 7.69 InstanceType 12 capture returns.
        if (instanceType == 12)
            return result == LegacySkillAttackResult.PartyFullCantSummon
                ? WorldHub.LegacyPartyFullCantSummonMessage
                : null;
        if (instanceType != 9)
            return null;

        return result switch
        {
            LegacySkillAttackResult.TargetNotAlive =>
                "Personagem derrotado não pode ser invocado ou transferido.",
            LegacySkillAttackResult.SummonNotAllowedHere =>
                "Você não pode invocar ou transferir nesta zona.",
            LegacySkillAttackResult.TargetTooHighToSummon =>
                "O nível do oponente é muito alto para isto.",
            LegacySkillAttackResult.SummonInvalidPosition or LegacySkillAttackResult.SummonZoneBlocked =>
                "Não está disponível nesta área.",
            _ => null,
        };
    }
}

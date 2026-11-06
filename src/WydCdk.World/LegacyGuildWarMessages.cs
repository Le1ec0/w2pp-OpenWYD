namespace WydCdk.World;

/// <summary>Portuguese GuildWar notice templates from TMProject 7.69 Language.txt.</summary>
public static class LegacyGuildWarMessages
{
    public const int DeclarationLanguageId = 210;
    public const int WarStartedLanguageId = 211;
    public const int WarCancelledLanguageId = 212;
    public const int DeclarationCancelledLanguageId = 221;

    public static string Declaration(string guildName, string enemyGuildName) =>
        $"Guilda {guildName} declarou guerra para a Guilda {enemyGuildName}.";

    public static string WarStarted(string guildName, string enemyGuildName) =>
        $"Guilda {guildName} aceitou o desafio da Guilda {enemyGuildName}.";

    public static string WarCancelled(string guildName, string enemyGuildName) =>
        $"Guilda {guildName} solicitou o cancelamento da guerra com Guilda {enemyGuildName}.";

    public static string DeclarationCancelled(string guildName, string enemyGuildName) =>
        $"Guilda {guildName} cancelou a declaração de guerra à Guilda {enemyGuildName}.";
}

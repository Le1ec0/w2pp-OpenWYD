namespace WydCdk.World;

/// <summary>Portuguese GuildAlly notice templates from TMProject 7.69 Language.txt.</summary>
public static class LegacyGuildAllyMessages
{
    public const int CancelledLanguageId = 286;
    public const int DeclaredLanguageId = 287;

    public static string Declared(string guildName, string allyGuildName) =>
        $"Guilda {guildName} solicitou aliança com Guilda {allyGuildName}.";

    public static string Cancelled(string guildName, string allyGuildName) =>
        $"Guilda {guildName} finalizou aliança com Guilda {allyGuildName}.";
}

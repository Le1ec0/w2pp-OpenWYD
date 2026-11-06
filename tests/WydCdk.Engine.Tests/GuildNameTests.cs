using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class GuildNameTests
{
    public static void Run()
    {
        GuildsFileOverridesTheSourceFallback();
        MissingNamesUseV769GuildIndexAndMarks();
        UnderBarsBecomeSpaces();
        RejectsInvalidServerGroup();
        GuildWarMessagesMatchLanguageTableAndPreserveLatin1();
        GuildAllyMessagesMatchLanguageTableAndPreserveLatin1();
    }

    private static void GuildsFileOverridesTheSourceFallback()
    {
        var catalog = LegacyGuildNameCatalog.FromLines(0, ["0 0 1 TEST_GUILD"]);

        Check(catalog.GetGuildNameBytes(1).SequenceEqual(Encoding.Latin1.GetBytes("TEST GUILD")),
            "Guilds.txt did not override the source fallback for group/server/local guild 0/0/1.");
    }

    private static void MissingNamesUseV769GuildIndexAndMarks()
    {
        var catalog = LegacyGuildNameCatalog.FromLines(0, []);

        Check(catalog.GetGuildNameBytes(1).SequenceEqual(new byte[] { 0xA5, 0xB0, (byte)'-', 0xA5, 0xB0, 0xA5, 0xB1 }),
            "Missing guild names did not use the exact TMProject 7.69 index/mark bytes.");
    }

    private static void UnderBarsBecomeSpaces()
    {
        var catalog = LegacyGuildNameCatalog.FromLines(0, ["0 0 2 UNDER_BAR"]);

        Check(catalog.GetGuildName(2) == "UNDER BAR",
            "Underscores from Guilds.txt were not normalized to spaces.");
    }

    private static void RejectsInvalidServerGroup()
    {
        Expect<ArgumentOutOfRangeException>(() => LegacyGuildNameCatalog.FromLines(-1, []));
        Expect<ArgumentOutOfRangeException>(() => LegacyGuildNameCatalog.FromLines(10, []));
    }

    private static void GuildWarMessagesMatchLanguageTableAndPreserveLatin1()
    {
        Check(LegacyGuildWarMessages.Declaration("Alpha", "Beta") == "Guilda Alpha declarou guerra para a Guilda Beta.",
            "GuildWar declaration differs from Language.txt entry 210.");
        Check(LegacyGuildWarMessages.WarStarted("Alpha", "Beta") == "Guilda Alpha aceitou o desafio da Guilda Beta.",
            "GuildWar acceptance differs from Language.txt entry 211.");
        Check(LegacyGuildWarMessages.WarCancelled("Alpha", "Beta") == "Guilda Alpha solicitou o cancelamento da guerra com Guilda Beta.",
            "GuildWar cancellation differs from Language.txt entry 212.");
        var declarationCancelled = LegacyGuildWarMessages.DeclarationCancelled("Alpha", "Beta");
        Check(declarationCancelled == "Guilda Alpha cancelou a declaração de guerra à Guilda Beta.",
            "Pending GuildWar cancellation differs from Language.txt entry 221.");

        var codec = LegacyFrameCodec.CreateDefault();
        var frame = codec.Decode(new MessagePanelConfirmation(declarationCancelled).ToFrame(codec, 10, 1));
        var expected = Encoding.Latin1.GetBytes(declarationCancelled);
        Check(frame.IsChecksumValid && frame.Header.Type == MessagePanelConfirmation.MessageType &&
            frame.Payload.Span[..expected.Length].SequenceEqual(expected),
            "GuildWar notice text did not preserve Language.txt Latin1 bytes in MSG_MessagePanel.");
    }

    private static void GuildAllyMessagesMatchLanguageTableAndPreserveLatin1()
    {
        Check(LegacyGuildAllyMessages.CancelledLanguageId == 286 &&
              LegacyGuildAllyMessages.Cancelled("Alpha", "Beta") == "Guilda Alpha finalizou aliança com Guilda Beta.",
            "GuildAlly cancellation differs from Language.txt entry 286.");
        Check(LegacyGuildAllyMessages.DeclaredLanguageId == 287 &&
              LegacyGuildAllyMessages.Declared("Alpha", "Beta") == "Guilda Alpha solicitou aliança com Guilda Beta.",
            "GuildAlly declaration differs from Language.txt entry 287.");

        var codec = LegacyFrameCodec.CreateDefault();
        var message = LegacyGuildAllyMessages.Declared("Alpha", "Beta");
        var frame = codec.Decode(new MessagePanelConfirmation(message).ToFrame(codec, 10, 1));
        var expected = Encoding.Latin1.GetBytes(message);
        Check(frame.IsChecksumValid && frame.Header.Type == MessagePanelConfirmation.MessageType &&
            frame.Payload.Span[..expected.Length].SequenceEqual(expected),
            "GuildAlly notice text did not preserve Language.txt Latin1 bytes in MSG_MessagePanel.");
    }

    private static void Expect<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

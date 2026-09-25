namespace WydCdk.World;

/// <summary>
/// Client-facing notices loaded from the legacy <c>Language.txt</c> entries
/// used by the 7.69/W2PP movement restriction branches.
/// </summary>
public static class LegacyMovementMapNotice
{
    public const string NewbieZone = "Somente n\u00EDvel 35 ou inferior pode entrar no campo de treinamento.";
    public const string GuildZone = "Voc\u00EA n\u00E3o pode entrar na zona de outra guilda.";

    public static string? For(LegacyMovementMapRestriction restriction) => restriction switch
    {
        LegacyMovementMapRestriction.NewbieZone => NewbieZone,
        LegacyMovementMapRestriction.GuildZone => GuildZone,
        _ => null,
    };
}

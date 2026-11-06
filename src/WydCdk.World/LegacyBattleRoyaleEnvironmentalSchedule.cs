namespace WydCdk.World;

public static class LegacyBattleRoyaleEnvironmentalSchedule
{
    public const int IntervalSeconds = 16;

    private static readonly LegacyBattleRoyaleEnvironmentalArea[] OuterBands =
    [
        new(2_608, 1_708, 2_622, 1_711),
        new(2_608, 1_739, 2_622, 1_743),
    ];

    private static readonly LegacyBattleRoyaleEnvironmentalArea[] WidenedBands =
    [
        new(2_608, 1_708, 2_622, 1_718),
        new(2_608, 1_733, 2_622, 1_743),
    ];

    public static LegacyBattleRoyaleEnvironmentalStep? TryCreate(
        int battleRoyaleState,
        int battleRoyaleItem,
        int minutePhase,
        long serverSecondCounter)
    {
        if (battleRoyaleState != 2 || battleRoyaleItem <= 0 ||
            minutePhase is < 7 or > 19 || serverSecondCounter < 0 ||
            serverSecondCounter % IntervalSeconds != 0)
            return null;

        var areas = minutePhase < 9 ? OuterBands : WidenedBands;
        return new LegacyBattleRoyaleEnvironmentalStep(minutePhase, Array.AsReadOnly(areas));
    }
}

public sealed record LegacyBattleRoyaleEnvironmentalStep(
    int MinutePhase,
    IReadOnlyList<LegacyBattleRoyaleEnvironmentalArea> Areas);

public readonly record struct LegacyBattleRoyaleEnvironmentalArea(int X1, int Y1, int X2, int Y2)
{
    public int CenterX => X1 + ((X2 - X1) / 2);
    public int CenterY => Y1 + ((Y2 - Y1) / 2);

    public bool Contains(int x, int y) => x >= X1 && x <= X2 && y >= Y1 && y <= Y2;
}

public sealed record LegacyBattleRoyaleEnvironmentalSnapshot(
    IReadOnlyList<LegacyBattleRoyaleEnvironmentalDamageTargetSnapshot> DamageTargets,
    IReadOnlyList<LegacyBattleRoyaleEnvironmentalEffectSnapshot> Effects);

public sealed record LegacyBattleRoyaleEnvironmentalDamageTargetSnapshot(
    int ConnectionId,
    short PositionX,
    short PositionY,
    int CurrentHp,
    int Damage,
    IReadOnlyList<int> RecipientIds);

public sealed record LegacyBattleRoyaleEnvironmentalEffectSnapshot(
    LegacyBattleRoyaleEnvironmentalArea Area,
    IReadOnlyList<int> RecipientIds);

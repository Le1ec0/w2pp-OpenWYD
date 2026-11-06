using System.Text.Json;

namespace WydCdk.World;

public sealed record LegacyBattleRoyaleConfiguration(
    int BRItem,
    int BRHour,
    int ServerIndex,
    int NumServerInGroup,
    bool TestServer,
    bool LocalServer)
{
    public static LegacyBattleRoyaleConfiguration Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Battle Royale configuration must be a JSON object.");

            var configuration = new LegacyBattleRoyaleConfiguration(
                RequiredInt32(root, nameof(BRItem)),
                RequiredInt32(root, nameof(BRHour)),
                RequiredInt32(root, nameof(ServerIndex)),
                RequiredInt32(root, nameof(NumServerInGroup)),
                RequiredBoolean(root, nameof(TestServer)),
                RequiredBoolean(root, nameof(LocalServer)));
            configuration.Validate();
            return configuration;
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Battle Royale configuration is not valid JSON with the required field types.", error);
        }
    }

    public void Validate()
    {
        if (BRItem < 0)
            throw new InvalidDataException("BRItem must be zero or a positive item index.");
        if (BRHour is < 0 or > 23)
            throw new InvalidDataException("BRHour must be between 0 and 23.");
        if (ServerIndex is < 0 or > 9)
            throw new InvalidDataException("ServerIndex must be between 0 and 9.");
        if (NumServerInGroup is < 1 or > 10)
            throw new InvalidDataException("NumServerInGroup must be between 1 and 10.");
    }

    public bool IsEligible(DateTime localNow)
    {
        if (BRItem <= 0)
            return false;
        if (TestServer || LocalServer)
            return true;

        var selectedServerIndex = (localNow.Day - 1) % NumServerInGroup;
        return ServerIndex == selectedServerIndex;
    }

    private static int RequiredInt32(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var parsed))
            throw new InvalidDataException($"Battle Royale configuration requires integer field '{propertyName}'.");

        return parsed;
    }

    private static bool RequiredBoolean(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) ||
            value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidDataException($"Battle Royale configuration requires boolean field '{propertyName}'.");

        return value.GetBoolean();
    }
}

public sealed class LegacyBattleRoyaleSchedule
{
    private static readonly string[] ReadyMessages =
    [
        "Batalha Real para Nv < 100 inicia no coliseu em 2min.",
        "Batalha Real para Nv < 200 incia no coliseu em 2min.",
        "Batalha Real para qualquer nível inicia no coliseu em 2min.",
    ];

    private static readonly string[] StartMessages =
    [
        "Batalha Real está iniciando para Nv < 100.",
        "Batalha Real está iniciando para Nv < 200.",
        "Batalha Real está iniciando para qualquer nível.",
    ];

    private readonly LegacyBattleRoyaleConfiguration configuration;
    private int currentState;

    public LegacyBattleRoyaleSchedule(LegacyBattleRoyaleConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.Validate();
        this.configuration = configuration;
    }

    public int CurrentState => currentState;

    public LegacyBattleRoyaleTransition? Tick(DateTime localNow)
    {
        if (!configuration.IsEligible(localNow) || localNow.Hour != configuration.BRHour)
            return null;

        var phase = localNow.Minute % 20;
        var nextState = currentState switch
        {
            0 when phase == 0 => 1,
            1 when phase == 5 => 2,
            2 when phase == 13 => 3,
            3 when phase >= 14 => 0,
            _ => currentState,
        };
        if (nextState == currentState)
            return null;

        var transition = new LegacyBattleRoyaleTransition(
            currentState,
            nextState,
            configuration.BRItem,
            CreateAreaNotice(currentState, nextState, localNow.Minute),
            CreateAreaClearLevels(currentState, nextState, localNow.Minute),
            RefreshGridAndCleanupParties: (currentState, nextState) is (0, 1) or (1, 2))
        {
            DoorCalls = CreateDoorCalls(currentState, nextState),
        };
        currentState = nextState;
        return transition;
    }

    private static IReadOnlyList<LegacyBattleRoyaleDoorCall> CreateDoorCalls(int previousState, int state) =>
        (previousState, state) switch
        {
            (1, 2) =>
            [
                new LegacyBattleRoyaleDoorCall(LegacyMapItemStateCodes.Locked, LegacyBattleRoyaleDoorCallTiming.BeforeNotice),
                new LegacyBattleRoyaleDoorCall(LegacyMapItemStateCodes.Locked, LegacyBattleRoyaleDoorCallTiming.AfterAreaClear),
            ],
            (3, 0) =>
            [
                new LegacyBattleRoyaleDoorCall(LegacyMapItemStateCodes.Open, LegacyBattleRoyaleDoorCallTiming.BeforeNotice),
            ],
            _ => [],
        };

    private static LegacyAreaNotice? CreateAreaNotice(int previousState, int state, int minuteOfHour)
    {
        var messageBucket = Math.Min(minuteOfHour / 20, 2);
        return (previousState, state) switch
        {
            (0, 1) => new LegacyAreaNotice(ReadyMessages[messageBucket], 2580, 1708, 2665, 1765),
            (1, 2) => new LegacyAreaNotice(StartMessages[messageBucket], 2580, 1688, 2665, 1765),
            (2, 3) => new LegacyAreaNotice("Pr\u00EAmio do vencedor est\u00E1 no altar.", 2580, 1688, 2665, 1765),
            _ => null,
        };
    }

    private static LegacyBattleRoyaleLevelClear? CreateAreaClearLevels(int previousState, int state, int minuteOfHour)
    {
        if ((previousState, state) is not ((0, 1) or (1, 2)))
            return null;

        return (minuteOfHour / 20) switch
        {
            0 => new LegacyBattleRoyaleLevelClear(100, 400),
            1 => new LegacyBattleRoyaleLevelClear(200, 400),
            _ => null,
        };
    }
}

public sealed record LegacyBattleRoyaleTransition(
    int PreviousState,
    int State,
    int BRItem,
    LegacyAreaNotice? AreaNotice = null,
    LegacyBattleRoyaleLevelClear? AreaClearLevels = null,
    bool RefreshGridAndCleanupParties = false)
{
    public IReadOnlyList<LegacyBattleRoyaleDoorCall> DoorCalls { get; init; } = [];
}

public enum LegacyBattleRoyaleDoorCallTiming
{
    BeforeNotice,
    AfterAreaClear,
}

public sealed record LegacyBattleRoyaleDoorCall(int State, LegacyBattleRoyaleDoorCallTiming Timing);

public sealed record LegacyBattleRoyaleLevelClear(int MinLevel, int MaxLevel);

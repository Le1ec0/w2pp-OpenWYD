namespace WydCdk.World;

/// <summary>Session-local AddCrackError policy shared by the 7.69 and W2PP sources.</summary>
public static class LegacyCrackErrorRules
{
    public const int LogoutThreshold = 2_000_000_000;
    public const int QuitTradePoints = 10;
    public const int QuitTradeType = 17;
    public const int AttackDeadPoints = 1;
    public const int AttackDeadType = 8;
    public const int AttackTimingPoints = 1;
    public const int AttackTimingType = 107;
    public const int AttackBackstepPoints = 4;
    public const int AttackBackstepType = 7;
    public const int MotionPoints = 4;
    public const int MotionType = 6;
    public const int TradeListPoints = 10;
    public const int TradeListType = 87;
    public const int ActionPoints = 5;
    public const int ActionType = 3;
    public const int ActionRangePoints = 1;
    public const int ActionRangeType = 5;
    public const int ActionSpeedPoints = 5;
    public const int ActionSpeedType = 4;
    public const int ActionStopPoints = 1;
    public const int ActionStopType = 101;
    public const int ActionTickPoints = 1;
    public const int ActionTickType = 102;
    public const int ActionIllusionTickPoints = 1;
    public const int ActionIllusionTickType = 104;
    public const int ActionIllusionEligibilityPoints = 10;
    public const int ActionIllusionEligibilityType = 28;
    public const string BadNetworkNotice = "Rede está instável. Saindo do jogo.";

    public static LegacyCrackErrorOutcome Add(int currentPoints, int points, int type)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentPoints);
        ArgumentOutOfRangeException.ThrowIfNegative(points);
        // C++ signed overflow is not a useful compatibility contract. Saturate at the
        // logout threshold so repeated invalid packets cannot wrap below the gate.
        var total = (int)Math.Min((long)currentPoints + points, LogoutThreshold);
        return new(total, points, type, type is not (3 or 8 or 15), total >= LogoutThreshold);
    }
}

public sealed record LegacyCrackErrorOutcome(int TotalPoints, int AddedPoints, int Type,
    bool ShouldLog, bool RequiresCharacterLogout);

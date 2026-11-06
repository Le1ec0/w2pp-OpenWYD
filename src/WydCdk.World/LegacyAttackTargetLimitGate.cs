using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>Per-slot MSG_Attack max-target gate from TMProject 7.69 and W2PP.</summary>
public static class LegacyAttackTargetLimitGate
{
    public const int CrackErrorPoints = 10;
    public const int CrackErrorType = 28;

    public static bool IsExceeded(int targetIndex, int maxTarget, int declaredDamage, uint clientTick) =>
        targetIndex >= 0 &&
        declaredDamage == -1 &&
        ClientTickPolicy.IsAllowedFromClient(clientTick) &&
        targetIndex > maxTarget;
}

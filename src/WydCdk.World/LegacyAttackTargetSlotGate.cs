namespace WydCdk.World;

public enum LegacyAttackTargetSlotState : byte
{
    InvalidIndex,
    Empty,
    Active,
}

/// <summary>Classifies MSG_Attack target IDs using the legacy MOB-array bounds and occupancy.</summary>
public static class LegacyAttackTargetSlotGate
{
    public const int MaxMobExclusive = 25_000;

    public static LegacyAttackTargetSlotState Classify(int targetId, bool activeMobExists)
    {
        if (targetId <= 0 || targetId >= MaxMobExclusive)
            return LegacyAttackTargetSlotState.InvalidIndex;

        return activeMobExists
            ? LegacyAttackTargetSlotState.Active
            : LegacyAttackTargetSlotState.Empty;
    }
}

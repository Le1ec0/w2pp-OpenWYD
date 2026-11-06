namespace WydCdk.World;

/// <summary>Target-count rule used by the 7.69 and W2PP MSG_Attack branches for skills 41 and 44.</summary>
public static class LegacySupportSkillTargetLimit
{
    public const int MinimumTargets = 2;
    public const int MaximumTargets = 13;

    public static int? GetTargetCount(int skillId, int special)
    {
        if (skillId is not (41 or 44))
            return null;

        return Math.Clamp((special / 25) + 2, MinimumTargets, MaximumTargets);
    }
}

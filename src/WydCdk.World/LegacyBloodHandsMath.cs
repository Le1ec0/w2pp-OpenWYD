namespace WydCdk.World;

/// <summary>TMProject 7.69 Mãos Sangrentas double-critical healing arithmetic.</summary>
public static class LegacyBloodHandsMath
{
    public static LegacyBloodHandsResolution Resolve(int special3, int amountRoll, int chanceRoll)
    {
        if (amountRoll is < 0 or > 14)
            throw new ArgumentOutOfRangeException(nameof(amountRoll), amountRoll, "The legacy amount roll must be between 0 and 14.");
        if (chanceRoll is < 0 or > 99)
            throw new ArgumentOutOfRangeException(nameof(chanceRoll), chanceRoll, "The legacy chance roll must be between 0 and 99.");

        return chanceRoll < 20
            ? new LegacyBloodHandsResolution(Triggered: true, Healing: ((special3 * 10) / 2) + amountRoll)
            : new LegacyBloodHandsResolution(Triggered: false, Healing: 0);
    }
}

public readonly record struct LegacyBloodHandsResolution(bool Triggered, int Healing);

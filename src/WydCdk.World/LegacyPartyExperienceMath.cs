namespace WydCdk.World;

/// <summary>
/// Ports the generic (non-event-map) party branch from TMSrv/MobKilled.cpp.
/// Event-map, bonus, hold and daily-log modifiers remain explicit inputs for a
/// later step; the defaults reproduce the ordinary 15% legacy adjustment.
/// </summary>
public static class LegacyPartyExperienceMath
{
    public static int GetGenericMemberExperience(
        LegacyExperienceEligibility eligibility,
        int normalizedExperience,
        int memberLevel,
        int targetExperience,
        int targetLevel,
        bool newbieEventServer = false,
        bool doubleMode = false,
        bool kefraLive = true)
    {
        if (normalizedExperience <= 0 || targetExperience <= 0)
            return 0;

        var adjustedLevel = memberLevel;
        if (eligibility.ClassMaster is LegacyExperienceMath.ClassMasterCelestial or LegacyExperienceMath.ClassMasterCelestialCs or LegacyExperienceMath.ClassMasterSCelestial)
            adjustedLevel += LegacyExperienceMath.MaxLevel + 1;

        var experience = 450 * normalizedExperience / (30 + adjustedLevel);
        if (experience <= 0 || experience > 10_000_000)
            return 0;

        if (eligibility.ClassMaster == LegacyAccountSnapshot.ClassMasterMortal)
        {
            experience = adjustedLevel switch
            {
                <= 200 => experience,
                <= 300 => (int)(experience / 1.07f),
                <= 356 => (int)(experience / 1.25f),
                <= 370 => (int)(experience / 1.70f),
                <= 380 => (int)(experience / 2.10f),
                <= 390 => (int)(experience / 2.60f),
                <= 399 => experience / 4,
                _ => experience,
            };
        }
        else if (eligibility.ClassMaster == LegacyExperienceMath.ClassMasterCelestial)
        {
            experience = adjustedLevel switch
            {
                <= 200 => experience,
                <= 300 => (int)(experience / 0.85f),
                <= 356 => (int)(experience / 0.90f),
                <= 360 => (int)(experience / 4.50f),
                <= 370 => (int)(experience / 5.90f),
                <= 380 => experience / 11,
                <= 390 => experience / 17,
                <= 400 => experience / 35,
                _ => experience,
            };
        }
        else
        {
            experience = adjustedLevel switch
            {
                < 120 => experience / 10,
                < 150 => experience / 20,
                < 170 => experience / 40,
                < 180 => experience / 80,
                < 190 => experience / 160,
                _ => experience / 320,
            };
        }

        experience = 6 * experience / 10;
        experience = Math.Min(experience, targetExperience);
        if (doubleMode)
            experience *= 2;
        if (!kefraLive)
            experience /= 2;
        experience = newbieEventServer ? experience + (experience / 4) : experience - ((experience * 15) / 100);
        return Math.Max(0, experience);
    }
}

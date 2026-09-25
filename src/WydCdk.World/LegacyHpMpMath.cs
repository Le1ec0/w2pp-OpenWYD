using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Pure port of the BASE_GetHpMp stage used before character-login confirmation.
/// Only BaseScore/CurrentScore MaxHp and MaxMp are recalculated; current Hp/Mp are preserved.
/// </summary>
public static class LegacyHpMpMath
{
    // TMSrv.vcxproj selects the parent Basedef.cpp/common header, not the duplicate TMSrv/Basedef.cpp.
    public const int MaximumHp = 400_000;
    public const int MaximumMp = 100_000;
    public const int MaximumLevel = 399;

    // The selected 7.69 common BaseSIDCHM intentionally differs from W2PP for FM and HT.
    private static readonly ClassBase[] ClassBases =
    [
        new(4, 6, 80, 45, 3, 1), // TK
        new(10, 5, 60, 65, 1, 3), // FM
        new(6, 5, 70, 55, 1, 2), // BM
        new(9, 6, 70, 55, 2, 1), // HT
    ];

    /// <summary>
    /// Reproduces BASE_GetHpMp. Returns false and leaves the state unchanged for an invalid class,
    /// matching the source helper's FALSE return when Class is outside MAX_CLASS.
    /// </summary>
    public static bool TryApplyBaseHpMp(LegacyMobCombatState mob, short classMaster, out LegacyMobCombatState updated)
    {
        ArgumentNullException.ThrowIfNull(mob);

        if (mob.CharacterClass >= ClassBases.Length)
        {
            updated = mob;
            return false;
        }

        var classBase = ClassBases[mob.CharacterClass];
        var level = classMaster is LegacyAccountSnapshot.ClassMasterArch or LegacyAccountSnapshot.ClassMasterMortal
            ? mob.CurrentScore.Level
            : unchecked(mob.CurrentScore.Level + MaximumLevel);

        var maxHp = unchecked(classBase.BaseHp
            + ((int)mob.BaseScore.Constitution - classBase.BaseConstitution) * 2
            + level * classBase.HpPerLevel);
        if (maxHp >= MaximumHp)
            maxHp = MaximumHp;

        var maxMp = unchecked(classBase.BaseMp
            + ((int)mob.BaseScore.Intelligence - classBase.BaseIntelligence) * 2
            + level * classBase.MpPerLevel);
        if (maxMp >= MaximumMp)
            maxMp = MaximumMp;

        updated = mob with
        {
            BaseScore = mob.BaseScore with { MaxHp = maxHp, MaxMp = maxMp },
            CurrentScore = mob.CurrentScore with { MaxHp = maxHp, MaxMp = maxMp },
        };
        return true;
    }

    private readonly record struct ClassBase(
        int BaseIntelligence,
        int BaseConstitution,
        int BaseHp,
        int BaseMp,
        int HpPerLevel,
        int MpPerLevel);
}

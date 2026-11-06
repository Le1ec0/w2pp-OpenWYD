namespace WydCdk.World;

public enum LegacyAttackDamageMarkerKind
{
    Empty,
    Physical,
    Skill,
    Invalid,
}

/// <summary>Classifies the client-declared damage marker before each 7.69 MSG_Attack target is processed.</summary>
public static class LegacyAttackDamageMarkerGate
{
    public const int PhysicalMarker = -2;
    public const int SkillMarker = -1;
    public const int CrackErrorPoints = 10;
    public const int CrackErrorType = 77;

    public static LegacyAttackDamageMarkerKind Classify(int declaredDamage) => declaredDamage switch
    {
        PhysicalMarker => LegacyAttackDamageMarkerKind.Physical,
        SkillMarker => LegacyAttackDamageMarkerKind.Skill,
        0 => LegacyAttackDamageMarkerKind.Empty,
        _ => LegacyAttackDamageMarkerKind.Invalid,
    };
}

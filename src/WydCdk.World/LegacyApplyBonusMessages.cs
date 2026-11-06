using System.Globalization;

namespace WydCdk.World;

/// <summary>7.69 Language.txt message mappings used by the ApplyBonus branches.</summary>
public static class LegacyApplyBonusMessages
{
    public const string SpecialLevelCap = "Não pode colocar mais pontos.";
    public const string SpecialAttributeCap = "Não pode passar do nível 200.";
    public const string CannotLearnOtherClassSkill = "Não pode aprender habilidades de outra classe.";
    public const string NotEnoughSkillPoints = "Não possui pontos suficientes.";
    public const string AlreadyLearnedSkill = "Você já aprendeu essa habilidade.";
    public const string NotEnoughLevelToLearnSkill = "Não possui nível para aprender essa habilidade.";
    public const string NotEnoughMasteryToLearnSkill = "Não possui aprendizado para obter essa habilidade.";
    public const string OnlyOneFinalSkill = "8ª Skill pode ser somente da 1ª Classe.";
    public const string FinalSkillPrerequisitesMissing = "É necessário aprender todas as skills antes da 8ª Skill.";

    public static string? ForSpecialScoreBonus(LegacySpecialScoreBonusResult result) => result switch
    {
        LegacySpecialScoreBonusResult.LevelCapReached => SpecialLevelCap,
        LegacySpecialScoreBonusResult.AttributeCapReached => SpecialAttributeCap,
        _ => null,
    };

    public static string? ForSkillBonus(LegacySkillBonusResult result, int requiredCoin = 0) => result switch
    {
        LegacySkillBonusResult.ClassMismatch => CannotLearnOtherClassSkill,
        LegacySkillBonusResult.InsufficientSkillPoints => NotEnoughSkillPoints,
        LegacySkillBonusResult.AlreadyLearned => AlreadyLearnedSkill,
        LegacySkillBonusResult.RequiredLevel => NotEnoughLevelToLearnSkill,
        LegacySkillBonusResult.RequiredAttributes => NotEnoughMasteryToLearnSkill,
        LegacySkillBonusResult.OnlyOneFinalSkill => OnlyOneFinalSkill,
        LegacySkillBonusResult.MissingPrerequisites => FinalSkillPrerequisitesMissing,
        LegacySkillBonusResult.InsufficientCoin => string.Format(CultureInfo.InvariantCulture, "Você precisa de {0} Gold.", requiredCoin),
        _ => null,
    };
}

using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class ApplyBonusSkillAllocationTests
{
    private const int SkillMasterId = 20_000;
    private const uint SecondaryLearnedSkillSentinel = 0x8765_4321;

    public static void Run()
    {
        LearnsClassSkillAndPreservesOtherSkillWords();
        AppliesClassAndTargetGates();
        AppliesLevelAndAttributeRequirements();
        AppliesFinalSkillPrerequisitesAndClassFees();
        AppliesCelestialPointOverrideAndUnavailableDataGates();
    }

    private static void LearnsClassSkillAndPreservesOtherSkillWords()
    {
        const int detail = 5025; // Class 1, position 1, SkillData row 25.
        var fixture = CreateFixture(1, detail, characterClass: 1, skillPointCost: 13, skillBonus: 20);

        var result = fixture.World.TryApplySkillBonus(1, detail, SkillMasterId, out var outcome, out var requiredCoin);

        Check(result == LegacySkillBonusResult.Applied && outcome is { SendScore: true, SendEtc: true } && requiredCoin == 0,
            "A valid base-skill allocation did not return the score/ETC update outcome.");
        var learned = BinaryPrimitives.ReadUInt32LittleEndian(outcome!.Mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset));
        var extraLearned = BinaryPrimitives.ReadUInt32LittleEndian(outcome.MobExtra.AsSpan(LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset));
        Check(learned == (1u << 1) && extraLearned == SecondaryLearnedSkillSentinel &&
            outcome.MobExtra.AsSpan().SequenceEqual(fixture.MobExtra),
            "Learning a base skill changed another learned-skill store or MOBEXTRA.");
        Check(BinaryPrimitives.ReadInt16LittleEndian(outcome.Mob.AsSpan(LegacyAccountSnapshot.MobSkillBonusOffset)) == 7,
            "The skill allocation did not debit the signed SkillBonus by the selected row's SkillPoint cost.");
        Check(BinaryPrimitives.ReadInt32LittleEndian(outcome.Mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset)) == 123_456,
            "An ordinary skill unexpectedly charged Gold.");
        var score = LegacyScore.Read(outcome.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(score.Hp == 411 && score.Mp == 222,
            "Recalculating the score after learning a skill changed current HP or MP.");
        var changedOutsideApprovedRanges = Enumerable.Range(0, outcome.Mob.Length)
            .Where(offset => outcome.Mob[offset] != fixture.Mob[offset])
            .Where(offset =>
                !IsInRange(offset, LegacyAccountSnapshot.MobLearnedSkillOffset, sizeof(uint)) &&
                !IsInRange(offset, LegacyAccountSnapshot.MobSkillBonusOffset, sizeof(short)) &&
                !IsInRange(offset, LegacyAccountSnapshot.MobCoinOffset, sizeof(int)) &&
                !IsInRange(offset, LegacyAccountSnapshot.MobCurrentScoreOffset, LegacyScore.SizeInBytes))
            .ToArray();
        Check(changedOutsideApprovedRanges.Length == 0,
            $"The skill learn changed MOB bytes outside LearnedSkill, SkillBonus, final-skill Gold, and the projected CurrentScore: {string.Join(',', changedOutsideApprovedRanges)}.");
    }

    private static void AppliesClassAndTargetGates()
    {
        var wrongClass = CreateFixture(2, detail: 5000, characterClass: 1);
        Check(wrongClass.World.TryApplySkillBonus(2, 5000, SkillMasterId, out var wrongClassOutcome, out _) ==
                LegacySkillBonusResult.ClassMismatch && wrongClassOutcome is null,
            "A character learned a skill from another class tree.");
        Check(LegacyApplyBonusMessages.ForSkillBonus(LegacySkillBonusResult.ClassMismatch) ==
                "Não pode aprender habilidades de outra classe.",
            "The class-tree rejection message differs from TMProject 7.69 Language.txt.");

        foreach (var target in new ushort[] { 999, 25_000 })
        {
            var fixture = CreateFixture(3 + target, detail: 5000);
            Check(fixture.World.TryApplySkillBonus(3 + target, 5000, target, out var outcome, out _) ==
                    LegacySkillBonusResult.InvalidTarget && outcome is null,
                $"A TargetID outside [1000,25000) was accepted: {target}.");
        }

        var invalidDetail = CreateFixture(30, detail: 5096);
        Check(invalidDetail.World.TryApplySkillBonus(30, 5096, SkillMasterId, out var invalidOutcome, out _) ==
                LegacySkillBonusResult.InvalidDetail && invalidOutcome is null,
            "A Detail above the 7.69 5000..5095 range was accepted.");
    }

    private static void AppliesLevelAndAttributeRequirements()
    {
        var skill = CreateFixture(40, detail: 5000, level: 9, requiredLevel: 10);
        Check(skill.World.TryApplySkillBonus(40, 5000, SkillMasterId, out var levelOutcome, out _) ==
                LegacySkillBonusResult.RequiredLevel && levelOutcome is null,
            "A Mortal character learned a skill below ItemList.RequiredLevel.");
        Check(LegacyApplyBonusMessages.ForSkillBonus(LegacySkillBonusResult.RequiredLevel) ==
                "Não possui nível para aprender essa habilidade.",
            "The required-level response differs from TMProject 7.69 Language.txt.");

        var attributes = CreateFixture(41, detail: 5000, special2: 9, requiredIntelligence: 10);
        Check(attributes.World.TryApplySkillBonus(41, 5000, SkillMasterId, out var attributeOutcome, out _) ==
                LegacySkillBonusResult.RequiredAttributes && attributeOutcome is null,
            "A skill learned without meeting ItemList.RequiredInt/ReqDex/ReqCon.");
        Check(LegacyApplyBonusMessages.ForSkillBonus(LegacySkillBonusResult.RequiredAttributes) ==
                "Não possui aprendizado para obter essa habilidade.",
            "The required-mastery response differs from TMProject 7.69 Language.txt.");

        var duplicate = CreateFixture(42, detail: 5000, learnedSkill: 1);
        Check(duplicate.World.TryApplySkillBonus(42, 5000, SkillMasterId, out var duplicateOutcome, out _) ==
                LegacySkillBonusResult.AlreadyLearned && duplicateOutcome is null,
            "An already learned base skill was not rejected.");
        Check(LegacyApplyBonusMessages.ForSkillBonus(LegacySkillBonusResult.AlreadyLearned) ==
                "Você já aprendeu essa habilidade.",
            "The duplicate-skill response differs from TMProject 7.69 Language.txt.");

        var insufficient = CreateFixture(43, detail: 5000, skillPointCost: 11, skillBonus: 10);
        Check(insufficient.World.TryApplySkillBonus(43, 5000, SkillMasterId, out var insufficientOutcome, out _) ==
                LegacySkillBonusResult.InsufficientSkillPoints && insufficientOutcome is null,
            "SkillBonus below the selected SkillPoint cost did not reject the learn request.");
        Check(LegacyApplyBonusMessages.ForSkillBonus(LegacySkillBonusResult.InsufficientSkillPoints) ==
                "Não possui pontos suficientes.",
            "The insufficient-skill-points response differs from TMProject 7.69 Language.txt.");
    }

    private static void AppliesFinalSkillPrerequisitesAndClassFees()
    {
        var oneOnly = CreateFixture(50, detail: 5007, learnedSkill: 1u << 15);
        Check(oneOnly.World.TryApplySkillBonus(50, 5007, SkillMasterId, out var oneOnlyOutcome, out _) ==
                LegacySkillBonusResult.OnlyOneFinalSkill && oneOnlyOutcome is null,
            "The 7.69 one-final-skill restriction was not enforced before prerequisite counting.");
        Check(LegacyApplyBonusMessages.ForSkillBonus(LegacySkillBonusResult.OnlyOneFinalSkill) ==
                "8ª Skill pode ser somente da 1ª Classe.",
            "The one-final-skill response differs from TMProject 7.69 Language.txt.");

        var prerequisites = CreateFixture(51, detail: 5007, learnedSkill: 0x3F);
        Check(prerequisites.World.TryApplySkillBonus(51, 5007, SkillMasterId, out var prerequisiteOutcome, out _) ==
                LegacySkillBonusResult.MissingPrerequisites && prerequisiteOutcome is null,
            "A final skill was available without all seven skills in its branch.");
        Check(LegacyApplyBonusMessages.ForSkillBonus(LegacySkillBonusResult.MissingPrerequisites) ==
                "É necessário aprender todas as skills antes da 8ª Skill.",
            "The final-skill prerequisite response differs from TMProject 7.69 Language.txt.");

        foreach (var (id, classMaster, expectedFee) in new (int Id, short ClassMaster, int ExpectedFee)[]
                 {
                     (52, (short)LegacyAccountSnapshot.ClassMasterMortal, 10_000_000),
                     (53, (short)LegacyAccountSnapshot.ClassMasterArch, 50_000_000),
                     (54, (short)LegacyExperienceMath.ClassMasterCelestial, 100_000_000),
                 })
        {
            var shortCoin = CreateFixture(id, detail: 5007, classMaster: classMaster,
                learnedSkill: 0x7F, coin: expectedFee - 1);
            Check(shortCoin.World.TryApplySkillBonus(id, 5007, SkillMasterId, out var coinOutcome, out var requiredCoin) ==
                    LegacySkillBonusResult.InsufficientCoin && coinOutcome is null && requiredCoin == expectedFee,
                $"Final-skill Gold fee for class-master {classMaster} differed from TMProject 7.69.");
            Check(LegacyApplyBonusMessages.ForSkillBonus(LegacySkillBonusResult.InsufficientCoin, requiredCoin) ==
                    $"Você precisa de {expectedFee} Gold.",
                "The formatted final-skill fee response differs from TMProject 7.69 Language.txt.");
        }

        var final = CreateFixture(55, detail: 5007, learnedSkill: 0x7F,
            skillPointCost: 13, skillBonus: 20, coin: 10_000_000);
        Check(final.World.TryApplySkillBonus(55, 5007, SkillMasterId, out var finalOutcome, out _) ==
                LegacySkillBonusResult.Applied && finalOutcome is not null,
            "A valid final skill with seven prerequisites and the 7.69 Mortal fee was rejected.");
        Check(BinaryPrimitives.ReadUInt32LittleEndian(finalOutcome!.Mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset)) == 0xFF &&
            BinaryPrimitives.ReadInt16LittleEndian(finalOutcome.Mob.AsSpan(LegacyAccountSnapshot.MobSkillBonusOffset)) == 7 &&
            BinaryPrimitives.ReadInt32LittleEndian(finalOutcome.Mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset)) == 0,
            "A successful final skill did not set its bit, debit SkillBonus, and charge Gold exactly once.");

        var tinyCostBoundary = CreateFixture(56, detail: 5007, learnedSkill: 0x7F, coin: LegacyTinyCombineRules.CoinCost);
        Check(tinyCostBoundary.World.TryApplySkillBonus(56, 5007, SkillMasterId, out _, out _) == LegacySkillBonusResult.Applied,
            "A funded final-skill fixture was rejected before the runtime Gold mirror check.");
        Check(tinyCostBoundary.World.TryPrepareTinyCombine(
                56,
                new LegacyItem[LegacyTinyCombineRules.CombineItemCount],
                new sbyte[LegacyTinyCombineRules.CombineItemCount],
                tinyBase: 40,
                out _) ==
                LegacyTinyCombinePreparationResult.InsufficientCoin,
            "Tiny Combine accepted stale pre-fee runtime Gold after the final-skill charge.");
    }

    private static void AppliesCelestialPointOverrideAndUnavailableDataGates()
    {
        var celestial = CreateFixture(60, detail: 5000, classMaster: LegacyExperienceMath.ClassMasterCelestial,
            skillPointCost: 100, skillBonus: 0);
        Check(celestial.World.TryApplySkillBonus(60, 5000, SkillMasterId, out var celestialOutcome, out _) ==
                LegacySkillBonusResult.Applied && celestialOutcome is not null &&
            BinaryPrimitives.ReadInt16LittleEndian(celestialOutcome.Mob.AsSpan(LegacyAccountSnapshot.MobSkillBonusOffset)) == -100,
            "The 7.69 advanced-class 1500 point gate did not remain distinct from the stored SkillBonus debit.");

        var noSkills = CreateFixture(61, detail: 5000, includeSkillData: false);
        Check(noSkills.World.TryApplySkillBonus(61, 5000, SkillMasterId, out var noSkillOutcome, out _) ==
                LegacySkillBonusResult.SkillDataUnavailable && noSkillOutcome is null,
            "Skill acquisition proceeded without the SkillData row that defines SkillPoint cost.");

        var noItems = CreateFixture(62, detail: 5000, includeItemData: false);
        Check(noItems.World.TryApplySkillBonus(62, 5000, SkillMasterId, out var noItemOutcome, out _) ==
                LegacySkillBonusResult.ItemDataUnavailable && noItemOutcome is null,
            "Skill acquisition proceeded without ItemList requirements.");

        var dead = CreateFixture(63, detail: 5000, hp: 0);
        Check(dead.World.TryApplySkillBonus(63, 5000, SkillMasterId, out var deadOutcome, out _) ==
                LegacySkillBonusResult.NotAlive && deadOutcome is null,
            "A dead character learned a skill or returned an update outcome.");
    }

    private static Fixture CreateFixture(
        int connectionId,
        int detail,
        int characterClass = 0,
        short classMaster = LegacyAccountSnapshot.ClassMasterMortal,
        short skillBonus = 20,
        int coin = 123_456,
        uint learnedSkill = 0,
        int level = 50,
        int hp = 411,
        int mp = 222,
        short special2 = 20,
        short requiredLevel = 10,
        short requiredIntelligence = 10,
        short requiredDexterity = 20,
        short requiredConstitution = 30,
        int skillPointCost = 13,
        bool includeItemData = true,
        bool includeSkillData = true)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var baseAndCurrent = new LegacyScore(level, 0, 0, 0, 0, 0, 0, 1_000, 600, hp, mp,
            40, 50, 60, 70, 10, special2, 30, 40);
        baseAndCurrent.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        baseAndCurrent.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = checked((byte)characterClass);
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), coin);
        BinaryPrimitives.WriteUInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), learnedSkill);
        BinaryPrimitives.WriteInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobSkillBonusOffset), skillBonus);

        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteInt16LittleEndian(mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset), classMaster);
        BinaryPrimitives.WriteUInt32LittleEndian(
            mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset), SecondaryLearnedSkillSentinel);
        var itemData = includeItemData
            ? CreateItemData(detail, requiredLevel, requiredIntelligence, requiredDexterity, requiredConstitution)
            : null;
        var skillData = includeSkillData
            ? CreateSkillData(detail - 5000, skillPointCost)
            : LegacySkillDataTable.Load(new StringReader(string.Empty));
        var world = new WorldHub(itemData: itemData, skillData: skillData);
        Check(world.Enter(connectionId, $"APPLY_SKILL_{connectionId}", (_, _) => ValueTask.CompletedTask),
            "Skill-bonus participant could not enter the world.");
        Check(world.SetCharacterState(connectionId, 0, 0, 0, 0, coin, 1_000, 1_000, mob,
                classMaster, new byte[LegacyAccountSnapshot.AffectStride], mobExtra),
            "Skill-bonus participant state could not be installed.");
        return new Fixture(world, mob, mobExtra);
    }

    private static LegacySkillDataTable CreateSkillData(int skillId, int skillPointCost)
    {
        var fields = Enumerable.Repeat("0", 24).ToArray();
        fields[0] = skillId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        fields[1] = skillPointCost.ToString(System.Globalization.CultureInfo.InvariantCulture);
        fields[23] = $"Skill_{skillId}";
        return LegacySkillDataTable.Load(new StringReader(string.Join(',', fields)));
    }

    private static LegacyItemDataTable CreateItemData(
        int itemIndex,
        short requiredLevel,
        short requiredIntelligence,
        short requiredDexterity,
        short requiredConstitution)
    {
        var encoded = Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray();
        Span<byte> decoded = stackalloc byte[LegacyItemDataTable.RecordSizeInBytes];
        BinaryPrimitives.WriteInt16LittleEndian(decoded[70..], requiredLevel);
        BinaryPrimitives.WriteInt16LittleEndian(decoded[74..], requiredIntelligence);
        BinaryPrimitives.WriteInt16LittleEndian(decoded[76..], requiredDexterity);
        BinaryPrimitives.WriteInt16LittleEndian(decoded[78..], requiredConstitution);
        var start = itemIndex * LegacyItemDataTable.RecordSizeInBytes;
        for (var offset = 0; offset < decoded.Length; offset++)
            encoded[start + offset] = (byte)(decoded[offset] ^ 0x5A);
        return LegacyItemDataTable.Load(encoded);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static bool IsInRange(int offset, int start, int length) => offset >= start && offset < start + length;

    private sealed record Fixture(WorldHub World, byte[] Mob, byte[] MobExtra);
}

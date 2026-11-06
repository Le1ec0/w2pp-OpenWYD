using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackMagicMirrorManaTests
{
    public static void Run()
    {
        FoemaWithMagicMirrorPaysHalfOfTheCalculatedSkillCost();
        MagicMirrorUsesIntegerTruncationAfterSaveMana();
        OtherClassesAndUnlearnedFoemaPayOnlyTheBaseCost();
        BaseCostInsufficiencyStillRejectsWithoutMutation();
        MirrorSurchargeDoesNotAddASecondInsufficientManaGate();
    }

    private static void FoemaWithMagicMirrorPaysHalfOfTheCalculatedSkillCost()
    {
        var (world, mobExtra) = CreateWorld(characterClass: 0, secondarySkills: LegacyMobExtra.MagicMirrorLearnedBit, mana: 100);
        var result = world.TryConsumeSkillMana(1, CreateSkill(manaSpent: 5), out var state, out var snapshot, out var requestedMana);

        Check(result == LegacySkillManaResult.Accepted && state?.CurrentMana == 93 &&
            snapshot is not null && LegacyMobCombatState.Read(snapshot).CurrentMana == 93 && requestedMana == 93,
            $"Foema with Magic Mirror did not pay base 5 plus truncated half-cost 2: result={result}, MP={state?.CurrentMana}, requested={requestedMana}.");
        Check(BinaryPrimitives.ReadUInt32LittleEndian(mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset)) ==
            LegacyMobExtra.MagicMirrorLearnedBit,
            "Consuming mana mutated the caller-owned MOBEXTRA fixture.");
    }

    private static void MagicMirrorUsesIntegerTruncationAfterSaveMana()
    {
        var (world, _) = CreateWorld(
            characterClass: 0,
            secondarySkills: LegacyMobExtra.MagicMirrorLearnedBit,
            mana: 100,
            saveMana: 33);
        var result = world.TryConsumeSkillMana(1, CreateSkill(manaSpent: 3), out var state, out _, out var requestedMana);

        // BASE_GetManaSpent computes (100 - 33) * 3 / 100 = 2, then Mirror adds 2 / 2 = 1.
        Check(result == LegacySkillManaResult.Accepted && state?.CurrentMana == 97 && requestedMana == 97,
            $"Magic Mirror did not truncate half of the SaveMana-adjusted base cost: result={result}, MP={state?.CurrentMana}, requested={requestedMana}.");
    }

    private static void OtherClassesAndUnlearnedFoemaPayOnlyTheBaseCost()
    {
        foreach (var testCase in new[]
        {
            (Name: "class 1 with bit 0x100", CharacterClass: (byte)1, SecondarySkills: LegacyMobExtra.MagicMirrorLearnedBit),
            (Name: "class 0 without bit 0x100", CharacterClass: (byte)0, SecondarySkills: 0u),
        })
        {
            var (world, _) = CreateWorld(testCase.CharacterClass, testCase.SecondarySkills, mana: 100);
            var result = world.TryConsumeSkillMana(1, CreateSkill(manaSpent: 5), out var state, out _, out var requestedMana);

            Check(result == LegacySkillManaResult.Accepted && state?.CurrentMana == 95 && requestedMana == 95,
                $"{testCase.Name} incorrectly paid the Mirror surcharge: result={result}, MP={state?.CurrentMana}, requested={requestedMana}.");
        }
    }

    private static void BaseCostInsufficiencyStillRejectsWithoutMutation()
    {
        var (world, _) = CreateWorld(characterClass: 0, secondarySkills: LegacyMobExtra.MagicMirrorLearnedBit, mana: 4);
        var result = world.TryConsumeSkillMana(1, CreateSkill(manaSpent: 5), out var state, out var snapshot, out var requestedMana);

        Check(result == LegacySkillManaResult.InsufficientMana && state is null && snapshot is null && requestedMana == 0,
            "Foema with less than the base mana cost was accepted or returned a spend snapshot.");
        Check(world.TryGetResourceState(1, out var unchanged, out _, out var unchangedRequestedMana) &&
            unchanged?.CurrentMana == 4 && unchangedRequestedMana == 4,
            "Rejecting the base mana cost changed current or requested MP.");
    }

    private static void MirrorSurchargeDoesNotAddASecondInsufficientManaGate()
    {
        var (world, _) = CreateWorld(characterClass: 0, secondarySkills: LegacyMobExtra.MagicMirrorLearnedBit, mana: 5);
        var result = world.TryConsumeSkillMana(1, CreateSkill(manaSpent: 5), out var state, out _, out var requestedMana);

        Check(result == LegacySkillManaResult.Accepted && state?.CurrentMana == -2 && requestedMana == 0,
            $"Mirror added a second mana sufficiency check or clamped the legacy negative MP result: result={result}, MP={state?.CurrentMana}, requested={requestedMana}.");
    }

    private static (WorldHub World, byte[] MobExtra) CreateWorld(byte characterClass, uint secondarySkills, int mana, byte saveMana = 0)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        mob[LegacyAccountSnapshot.MobClassOffset] = characterClass;
        mob[LegacyAccountSnapshot.MobSaveManaOffset] = saveMana;
        new LegacyScore(50, 0, 0, 0, 0, 0, 0, 100, 100, 1_000, 1_000,
            0, 0, 1_000, 0, 0, 100, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteUInt32LittleEndian(
            mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset), secondarySkills);

        var world = new WorldHub();
        Check(world.Enter(1, "MAGIC_MIRROR", (_, _) => ValueTask.CompletedTask),
            "Magic Mirror fixture participant did not enter the world.");
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCurrentMpOffset), mana);
        Check(world.SetCharacterState(
                1, 0, 0, 0, 0, 0, 2_000, 2_000, mob,
                LegacyAccountSnapshot.ClassMasterMortal,
                new byte[LegacyAccountSnapshot.AffectStride], mobExtra),
            "Magic Mirror fixture state was not installed.");
        return (world, mobExtra);
    }

    private static LegacySkillDefinition CreateSkill(int manaSpent) =>
        new(101, 0, 0, manaSpent, 0, 0, 1, 0, 0, 0, 0, 0, 0,
            [], [], 0, 0, 0, 0, 0, 0, 0, 0, "MAGIC_MIRROR_MANA");

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

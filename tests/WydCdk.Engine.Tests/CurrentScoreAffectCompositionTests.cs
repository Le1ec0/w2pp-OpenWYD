using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class CurrentScoreAffectCompositionTests
{
    public static void Run()
    {
        SelectedAffectsPreserveSlotOrderAndSourceMagic();
        Type38UsesCurrentMpInSlotOrderAndIgnoresPayload();
        Type42ComposesSequentiallyWithType38();
        Type4AffectsPreserveSlotOrderAndUseSourceMagic();
        Type4UsesItsOwnHpThreshold();
        CoordinatorAppliesArmorClassAffectsInTargetPassOrder();
        ElementalProtectionArmorClassPreservesThresholdAndSlotOrder();
        Type16ArmorClassPreservesBeastMasterGate();
        MeditationArmorClassPreservesFirstPassSlotOrder();
        TruncatedAffectSnapshotIsRejected();
    }

    private static void CoordinatorAppliesArmorClassAffectsInTargetPassOrder()
    {
        var affects = CreateAffectSnapshot(
            (0, Type: (byte)24, Value: (byte)4, Level: (ushort)0),
            (1, Type: (byte)31, Value: (byte)2, Level: (ushort)5),
            (2, Type: (byte)16, Value: (byte)5, Level: (ushort)200),
            (3, Type: (byte)11, Value: (byte)2, Level: (ushort)5),
            (4, Type: (byte)8, Value: (byte)0, Level: (ushort)(1 << 4)),
            (5, Type: (byte)12, Value: (byte)20, Level: (ushort)0));
        var actual = ProjectArmorClass(100, 2, affects);
        Check(actual == 153,
            $"The coordinator did not apply Type 24/31/16/11 before Type 8/12 by slot: AC={actual}.");

        var type8BeforeType12 = CreateAffectSnapshot(
            (0, Type: (byte)8, Value: (byte)0, Level: (ushort)(1 << 4)),
            (1, Type: (byte)12, Value: (byte)20, Level: (ushort)0));
        var orderedAc = ProjectArmorClass(11, 0, type8BeforeType12);
        Check(orderedAc == 9,
            $"The coordinator grouped Type 12 before Type 8 bit 4 instead of preserving slot order: AC={orderedAc}.");

        var type12BeforeType8 = CreateAffectSnapshot(
            (0, Type: (byte)12, Value: (byte)20, Level: (ushort)0),
            (1, Type: (byte)8, Value: (byte)0, Level: (ushort)(1 << 4)));
        var reversedAc = ProjectArmorClass(11, 0, type12BeforeType8);
        Check(reversedAc == 8,
            $"The coordinator changed the reversed Type 12/Type 8 bit 4 slot result: AC={reversedAc}.");

    }

    private static void ElementalProtectionArmorClassPreservesThresholdAndSlotOrder()
    {
        var belowThreshold = ProjectArmorClass(100, 0, CreateAffectSnapshot(
            (0, Type: (byte)25, Value: (byte)7, Level: (ushort)254)));
        var atThreshold = ProjectArmorClass(100, 0, CreateAffectSnapshot(
            (0, Type: (byte)25, Value: (byte)7, Level: (ushort)255)));
        var elementalProtectionBeforeTransformation = ProjectArmorClass(100, 2, CreateAffectSnapshot(
            (0, Type: (byte)25, Value: (byte)0, Level: (ushort)255),
            (1, Type: (byte)16, Value: (byte)4, Level: (ushort)200)));
        var transformationBeforeElementalProtection = ProjectArmorClass(100, 2, CreateAffectSnapshot(
            (0, Type: (byte)16, Value: (byte)4, Level: (ushort)200),
            (1, Type: (byte)25, Value: (byte)0, Level: (ushort)255)));

        Check(belowThreshold == 234 && atThreshold == 264,
            $"Type 25 AC threshold must add 30 starting at level 255: level 254={belowThreshold}, level 255={atThreshold}.");
        Check(elementalProtectionBeforeTransformation == 372 && transformationBeforeElementalProtection == 302,
            $"Type 25 AC was not composed in affect-slot order with Type 16: Type25-first={elementalProtectionBeforeTransformation}, Type16-first={transformationBeforeElementalProtection}.");
    }

    private static void Type38UsesCurrentMpInSlotOrderAndIgnoresPayload()
    {
        var affects = CreateAffectSnapshot(
            (0, Type: (byte)38, Value: (byte)255, Level: (ushort)0),
            (1, Type: (byte)35, Value: (byte)255, Level: ushort.MaxValue),
            (2, Type: (byte)38, Value: (byte)0, Level: ushort.MaxValue));
        var originalAffects = affects.ToArray();

        var actual = LegacyCurrentScoreMath.CalculateCurrentScoreAffectCompositionStage(
            affects,
            startingMaxHp: 100,
            startingMaxMp: 101,
            startingArmorClass: 40,
            startingDamage: 50,
            sourceMobMagic: 60,
            startingMagic: 70,
            startingLearnedSkills: 0x100);

        Check(actual.MaxHp == 199 && actual.MaxMp == 27,
            $"Type 38 did not transfer half of the current MaxMP sequentially in affect-slot order: HP={actual.MaxHp}, MP={actual.MaxMp}.");
        Check(actual.ArmorClass == 40 && actual.Damage == 50 && actual.Magic == 70 &&
            actual.LearnedSkills == 0x100 && actual.RsvFlagsToAdd == 0,
            "Type 38 changed score fields outside MaxHP and MaxMP.");
        Check(affects.AsSpan().SequenceEqual(originalAffects), "The Type 38 composer mutated the affect snapshot.");
    }

    private static void Type42ComposesSequentiallyWithType38()
    {
        var type38BeforeType42 = CreateAffectSnapshot(
            (0, Type: (byte)38, Value: (byte)0, Level: (ushort)0),
            (1, Type: (byte)42, Value: (byte)0, Level: (ushort)40));
        var type42BeforeType38 = CreateAffectSnapshot(
            (0, Type: (byte)42, Value: (byte)0, Level: (ushort)40),
            (1, Type: (byte)38, Value: (byte)0, Level: (ushort)0));

        LegacyCurrentScoreAffectCompositionStage Compose(byte[] affects) =>
            LegacyCurrentScoreMath.CalculateCurrentScoreAffectCompositionStage(
                affects,
                startingMaxHp: 1_000,
                startingMaxMp: 1_001,
                startingArmorClass: 40,
                startingDamage: 50,
                sourceMobMagic: 60,
                startingMagic: 70,
                startingLearnedSkills: 0,
                currentLevel: 200,
                classMaster: 3);

        var type38First = Compose(type38BeforeType42);
        var type42First = Compose(type42BeforeType38);
        Check(type38First.MaxHp == 1_919 && type38First.MaxMp == 82,
            $"Type 42 did not use the MaxMP remaining after earlier Type 38: MaxHP={type38First.MaxHp}, MaxMP={type38First.MaxMp}.");
        Check(type42First.MaxHp == 1_760 && type42First.MaxMp == 241,
            $"Type 42/38 slot order changed when Type 42 came first: MaxHP={type42First.MaxHp}, MaxMP={type42First.MaxMp}.");
    }

    private static void SelectedAffectsPreserveSlotOrderAndSourceMagic()
    {
        var affects = CreateAffectSnapshot(
            (0, Type: (byte)8, Value: (byte)0, Level: (ushort)(1 << 4)),
            (1, Type: (byte)35, Value: (byte)0, Level: (ushort)0),
            (2, Type: (byte)34, Value: (byte)0, Level: (ushort)0),
            (3, Type: (byte)8, Value: (byte)0, Level: (ushort)239),
            (4, Type: (byte)7, Value: (byte)255, Level: ushort.MaxValue),
            (5, Type: (byte)2, Value: (byte)2, Level: (ushort)0));
        var originalAffects = affects.ToArray();

        var actual = LegacyCurrentScoreMath.CalculateCurrentScoreAffectCompositionStage(
            affects,
            startingMaxHp: 102,
            startingMaxMp: 101,
            startingArmorClass: 101,
            startingDamage: 101,
            sourceMobMagic: 1001,
            startingMagic: 7,
            startingLearnedSkills: 0x100);

        Check(actual.MaxHp == 232, $"Slot order or Type 8 bit 7 transfer differed: MaxHP={actual.MaxHp}; grouped-by-type order would produce 233.");
        Check(actual.MaxMp == 64 && actual.ArmorClass == 111 && actual.Damage == 133,
            $"The score fields were not composed in affect-slot order: MP={actual.MaxMp}, AC={actual.ArmorClass}, Damage={actual.Damage}.");
        Check(actual.Magic == 157,
            $"Type 34 and Type 8 Magic deltas did not both use source MOB.Magic while accumulating locally: Magic={actual.Magic}.");
        Check(actual.LearnedSkills == 0x10000100,
            $"Type 8 bit 6 did not add LearnedSkill bit 28 while preserving prior bits: {actual.LearnedSkills:X8}.");
        Check(actual.RsvFlagsToAdd == 0x0004,
            $"The composer did not return only RSV_VISION from Type 8 bit 2: {actual.RsvFlagsToAdd:X4}.");

        var resistance = LegacyCurrentScoreMath.ApplyResistanceAffectStage(
            holy: 10, thunder: 20, fire: 30, ice: 40, affects,
            characterClass: 0, headItemIndex: 7, learnedSkills: 0);
        Check(resistance.Holy == 35 && resistance.Thunder == 45 && resistance.Fire == 55 && resistance.Ice == 65,
            "Type 8 bit 1 was not left to the separate resistance stage.");
        Check(affects.AsSpan().SequenceEqual(originalAffects), "The score composer or resistance stage mutated the affect snapshot.");
    }

    private static void Type4AffectsPreserveSlotOrderAndUseSourceMagic()
    {
        var affects = CreateAffectSnapshot(
            (0, Type: (byte)34, Value: (byte)0, Level: (ushort)0),
            (1, Type: (byte)4, Value: (byte)255, Level: ushort.MaxValue),
            (2, Type: (byte)4, Value: (byte)0, Level: (ushort)0));

        var actual = LegacyCurrentScoreMath.CalculateCurrentScoreAffectCompositionStage(
            affects,
            startingMaxHp: 9,
            startingMaxMp: 9,
            startingArmorClass: 40,
            startingDamage: 100,
            sourceMobMagic: 105,
            startingMagic: 5,
            startingLearnedSkills: 0);

        Check(actual.MaxHp == 12 && actual.MaxMp == 12,
            $"Type 4 did not apply its HP/MP deltas in affect-slot order: HP={actual.MaxHp}, MP={actual.MaxMp}.");
        Check(actual.Damage == 185 && actual.Magic == 57,
            $"Type 4 did not apply 30% Damage and 20% source-Magic per affect: Damage={actual.Damage}, Magic={actual.Magic}.");
        Check(actual.ArmorClass == 40 && actual.LearnedSkills == 0 && actual.RsvFlagsToAdd == 0,
            "Type 4 changed fields outside its score branch.");
    }

    private static void Type4UsesItsOwnHpThreshold()
    {
        var affects = CreateAffectSnapshot((0, Type: (byte)4, Value: (byte)0, Level: (ushort)0));

        var actual = LegacyCurrentScoreMath.CalculateCurrentScoreAffectCompositionStage(
            affects,
            startingMaxHp: 3_000_000,
            startingMaxMp: 1_000_000,
            startingArmorClass: 0,
            startingDamage: 4_000_000,
            sourceMobMagic: 1_000_000,
            startingMagic: 5,
            startingLearnedSkills: 0);

        Check(actual.MaxHp == 400_000,
            $"Type 4 did not assign its source HP ceiling when the 15% delta reached 400,000: MaxHP={actual.MaxHp}.");
        Check(actual.MaxMp == 100_000 && actual.Damage == 1_000_000 && actual.Magic == 200_005,
            $"Type 4 MP, Damage, or Magic deltas/caps differed: MP={actual.MaxMp}, Damage={actual.Damage}, Magic={actual.Magic}.");
    }

    private static void Type16ArmorClassPreservesBeastMasterGate()
    {
        var affects = CreateAffectSnapshot(
            (0, Type: (byte)16, Value: (byte)4, Level: (ushort)200));
        var beastMaster = LegacyCurrentScoreMath.CalculateCurrentScoreArmorClassAffectCompositionStage(
            affects,
            startingArmorClass: 100,
            characterClass: 2,
            learnedSkills: 0);
        var otherClass = LegacyCurrentScoreMath.CalculateCurrentScoreArmorClassAffectCompositionStage(
            affects,
            startingArmorClass: 100,
            characterClass: 0,
            learnedSkills: 0);

        Check(beastMaster.ArmorClass == 145 && otherClass.ArmorClass == 100,
            $"Type 16 AC scaling or its BeastMaster class gate differed: BeastMaster={beastMaster.ArmorClass}, other={otherClass.ArmorClass}.");
    }

    private static void MeditationArmorClassPreservesFirstPassSlotOrder()
    {
        var meditationBeforeSamaritan = ProjectArmorClass(100, 0, CreateAffectSnapshot(
            (0, Type: (byte)21, Value: (byte)0, Level: (ushort)30),
            (1, Type: (byte)24, Value: (byte)4, Level: (ushort)0)));
        var samaritanBeforeMeditation = ProjectArmorClass(100, 0, CreateAffectSnapshot(
            (0, Type: (byte)24, Value: (byte)4, Level: (ushort)0),
            (1, Type: (byte)21, Value: (byte)0, Level: (ushort)30)));

        Check(meditationBeforeSamaritan == 104 && samaritanBeforeMeditation == 109,
            $"Type 21 AC reduction was not applied in the first AC pass by affect slot: Type21-first={meditationBeforeSamaritan}, Type24-first={samaritanBeforeMeditation}.");
    }

    private static void TruncatedAffectSnapshotIsRejected()
    {
        var rejected = false;
        try
        {
            LegacyCurrentScoreMath.CalculateCurrentScoreAffectCompositionStage(
                new byte[8], startingMaxHp: 100, startingMaxMp: 100, startingArmorClass: 100,
                startingDamage: 100, sourceMobMagic: 100, startingMagic: 0, startingLearnedSkills: 0);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }

        Check(rejected, "The score composer accepted a truncated affect snapshot.");

        rejected = false;
        try
        {
            LegacyCurrentScoreMath.CalculateCurrentScoreArmorClassAffectCompositionStage(
                new byte[8], startingArmorClass: 100, characterClass: 0, learnedSkills: 0);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }

        Check(rejected, "The ordered AC stage accepted a truncated affect snapshot.");
    }

    private static byte[] CreateAffectSnapshot(params (int Slot, byte Type, byte Value, ushort Level)[] records)
    {
        var snapshot = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var record in records)
        {
            if (record.Slot is < 0 or >= LegacyAccountSnapshot.AffectStride / 8)
                throw new ArgumentOutOfRangeException(nameof(records), record.Slot, "Affect slot must be in the 0..31 range.");

            var offset = record.Slot * 8;
            snapshot[offset] = record.Type;
            snapshot[offset + 1] = record.Value;
            BinaryPrimitives.WriteUInt16LittleEndian(snapshot.AsSpan(offset + 2), record.Level);
        }

        return snapshot;
    }

    private static int ProjectArmorClass(int startingArmorClass, byte characterClass, byte[] affectSnapshot)
    {
        var score = new LegacyScore(
            100, startingArmorClass, 100, 0, 0, 0, 0, 1_000, 500, 1_000, 500,
            10, 10, 10, 10, 0, 0, 0, 0);
        var mob = new LegacyMobCombatState(characterClass, 0, 0, score, 0) { BaseScore = score };
        var equipment = new LegacyItem[LegacyCharacterSelection.EquipmentCount];
        var itemData = LegacyItemDataTable.Load(
            Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());
        var stage = LegacyCurrentScoreMath.CalculateCurrentScoreCoordinatorStage(
            mob,
            equipment,
            itemData,
            affectSnapshot,
            classMaster: LegacyAccountSnapshot.ClassMasterMortal,
            soul: 0,
            isSummon: false,
            holy: 0,
            thunder: 0,
            fire: 0,
            ice: 0);
        return stage.AfterScoreAffects.CurrentScore.Ac;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

using System.Buffers.Binary;
using WydCdk.World;

internal static class CurrentScorePvpJewelTests
{
    public static void Run()
    {
        CombinedTargetBitsApplyThe769ScoreMutationsInOrder();
        MultiplePowerBitsReuseSourceMobMagicAndApplySequentially();
        The769CapsAreCheckedAgainstDeltasAndBit7ConvertsHalfMaxMp();
        AttackOnlyBitsDoNotChangeTheScoreStage();
        TruncatedAffectSnapshotIsRejected();
    }

    private static void CombinedTargetBitsApplyThe769ScoreMutationsInOrder()
    {
        const ushort mask = (1 << 2) | (1 << 4) | (1 << 5) | (1 << 6) | (1 << 7);
        var affects = CreateAffectSnapshot((0, Type: (byte)8, Level: mask));

        var actual = LegacyCurrentScoreMath.CalculatePvpJewelAffectStage(
            affects,
            startingMaxHp: 1_099,
            startingMaxMp: 401,
            startingArmorClass: 199,
            startingDamage: 1_001,
            sourceMobMagic: 101,
            startingMagic: 20,
            startingLearnedSkills: 0);

        // The same input in W2PP yields MaxHP=1510, AC=209, Damage=1101 and MaxMP=200;
        // 7.69 instead multiplies before division, adds 20% Damage, and keeps Magic separate.
        Check(actual.MaxHp == 1_528 && actual.MaxMp == 200 && actual.ArmorClass == 218
            && actual.Damage == 1_201 && actual.Magic == 25,
            $"Combined Type 8 bits did not preserve 7.69 score order and integer truncation: {actual}.");
        Check(actual.LearnedSkills == (1 << 28) && actual.RsvFlagsToAdd == 0x04,
            $"Type 8 precision or vision did not produce the 7.69 side effects: {actual}.");
    }

    private static void MultiplePowerBitsReuseSourceMobMagicAndApplySequentially()
    {
        const ushort powerMask = 1 << 5;
        var affects = CreateAffectSnapshot(
            (0, Type: (byte)8, Level: powerMask),
            (31, Type: (byte)8, Level: powerMask));

        var actual = LegacyCurrentScoreMath.CalculatePvpJewelAffectStage(
            affects,
            startingMaxHp: 1_000,
            startingMaxMp: 1_000,
            startingArmorClass: 50,
            startingDamage: 1_000,
            sourceMobMagic: 1_000,
            startingMagic: 0,
            startingLearnedSkills: 0);

        Check(actual.MaxHp == 1_210 && actual.Damage == 1_440 && actual.Magic == 100,
            $"Multiple Type 8 power bits were not sequential or reused the mutated Magic instead of MOB.Magic: {actual}.");
    }

    private static void The769CapsAreCheckedAgainstDeltasAndBit7ConvertsHalfMaxMp()
    {
        var aboveTargetCaps = CreateAffectSnapshot((0, Type: (byte)8, Level: (1 << 4) | (1 << 5)));
        var above = LegacyCurrentScoreMath.CalculatePvpJewelAffectStage(
            aboveTargetCaps,
            startingMaxHp: 95_000,
            startingMaxMp: 20_000,
            startingArmorClass: 100,
            startingDamage: 950_000,
            sourceMobMagic: 180_000_000,
            startingMagic: 185_000_000,
            startingLearnedSkills: 0);

        Check(above.MaxHp == 104_500 && above.ArmorClass == 110 && above.Damage == 1_140_000 && above.Magic == 194_000_000,
            $"Type 8 compared resulting values instead of the 7.69 delta caps or clamped too early: {above}.");

        var reachesDamageDeltaCap = CreateAffectSnapshot((0, Type: (byte)8, Level: 1 << 5));
        var cappedDamage = LegacyCurrentScoreMath.CalculatePvpJewelAffectStage(
            reachesDamageDeltaCap,
            startingMaxHp: 1_000,
            startingMaxMp: 1_000,
            startingArmorClass: 0,
            startingDamage: 5_000_000,
            sourceMobMagic: 0,
            startingMagic: 0,
            startingLearnedSkills: 0);
        Check(cappedDamage.Damage == 1_000_000,
            $"Type 8 did not set the 7.69 Damage cap when the calculated delta reached it: {cappedDamage}.");

        var conversion = CreateAffectSnapshot((0, Type: (byte)8, Level: 1 << 7));
        var converted = LegacyCurrentScoreMath.CalculatePvpJewelAffectStage(
            conversion,
            startingMaxHp: 99_000,
            startingMaxMp: 300_000,
            startingArmorClass: 0,
            startingDamage: 0,
            sourceMobMagic: 0,
            startingMagic: 0,
            startingLearnedSkills: 0);

        Check(converted.MaxHp == 100_000 && converted.MaxMp == 100_000,
            $"Type 8 bit 7 did not transfer half MaxMP and apply the 7.69 caps: {converted}.");
    }

    private static void AttackOnlyBitsDoNotChangeTheScoreStage()
    {
        var affects = CreateAffectSnapshot(
            (0, Type: (byte)8, Level: (1 << 0) | (1 << 1) | (1 << 3)),
            (1, Type: (byte)34, Level: (1 << 2) | (1 << 4) | (1 << 5) | (1 << 6) | (1 << 7)));

        var actual = LegacyCurrentScoreMath.CalculatePvpJewelAffectStage(
            affects,
            startingMaxHp: 12_345,
            startingMaxMp: 23_456,
            startingArmorClass: 345,
            startingDamage: 456,
            sourceMobMagic: 567,
            startingMagic: 678,
            startingLearnedSkills: 0x1234);

        Check(actual.MaxHp == 12_345 && actual.MaxMp == 23_456 && actual.ArmorClass == 345
            && actual.Damage == 456 && actual.Magic == 678 && actual.LearnedSkills == 0x1234
            && actual.RsvFlagsToAdd == 0,
            $"An attack-only or separately handled Type 8 bit changed the score stage: {actual}.");
    }

    private static void TruncatedAffectSnapshotIsRejected()
    {
        var rejected = false;
        try
        {
            LegacyCurrentScoreMath.CalculatePvpJewelAffectStage(
                new byte[8], startingMaxHp: 100, startingMaxMp: 100, startingArmorClass: 10,
                startingDamage: 10, sourceMobMagic: 10, startingMagic: 10, startingLearnedSkills: 0);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }

        Check(rejected, "The Type 8 stage accepted a truncated 32-record affect snapshot.");
    }

    private static byte[] CreateAffectSnapshot(params (int Slot, byte Type, ushort Level)[] records)
    {
        var snapshot = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var record in records)
        {
            if (record.Slot is < 0 or >= LegacyAccountSnapshot.AffectStride / 8)
                throw new ArgumentOutOfRangeException(nameof(records), record.Slot, "Affect slot must be in the 0..31 range.");

            var offset = record.Slot * 8;
            snapshot[offset] = record.Type;
            BinaryPrimitives.WriteUInt16LittleEndian(snapshot.AsSpan(offset + 2), record.Level);
        }

        return snapshot;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

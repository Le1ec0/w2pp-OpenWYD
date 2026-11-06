using System.Buffers.Binary;
using WydCdk.World;

internal static class CurrentScoreDivinaTests
{
    public static void Run()
    {
        Type34Uses7_69IntegerTruncationAndMobMagicInput();
        MultipleType34AffectsApplyInSlotOrderAndReuseMobMagic();
        Type34ComparesTheCalculatedDeltaToEachCap();
        Type34LeavesFinalMagicClampToTheLaterScoreBlock();
        OtherAffectsDoNotChangeTheDivinaStage();
        TruncatedAffectSnapshotIsRejected();
    }

    private static void Type34Uses7_69IntegerTruncationAndMobMagicInput()
    {
        var affects = CreateAffectSnapshot((0, Type: (byte)34, Value: (byte)255, Level: (ushort)65535));

        var actual = LegacyCurrentScoreMath.CalculateDivinaAffectStage(
            affects,
            startingMaxHp: 999,
            startingMaxMp: 101,
            startingDamage: 999,
            sourceMobMagic: 999,
            startingMagic: 5_000);

        Check(actual.MaxHp == 1_198 && actual.MaxMp == 121 && actual.Damage == 1_098 && actual.Magic == 5_099,
            $"Type 34 did not preserve 7.69 integer order or use source MOB.Magic: {actual}.");
    }

    private static void MultipleType34AffectsApplyInSlotOrderAndReuseMobMagic()
    {
        var affects = CreateAffectSnapshot(
            (0, Type: (byte)34, Value: (byte)0, Level: (ushort)0),
            (4, Type: (byte)34, Value: (byte)0, Level: (ushort)0));

        var actual = LegacyCurrentScoreMath.CalculateDivinaAffectStage(
            affects,
            startingMaxHp: 50_000,
            startingMaxMp: 50_001,
            startingDamage: 50_009,
            sourceMobMagic: 1_009,
            startingMagic: 50_000);

        Check(actual.MaxHp == 72_000 && actual.MaxMp == 72_001 && actual.Damage == 60_509 && actual.Magic == 50_200,
            $"Multiple Type 34 affects did not apply sequentially while reusing base MOB.Magic: {actual}.");
    }

    private static void Type34ComparesTheCalculatedDeltaToEachCap()
    {
        var affects = CreateAffectSnapshot((0, Type: (byte)34, Value: (byte)0, Level: (ushort)0));

        var belowDeltaCaps = LegacyCurrentScoreMath.CalculateDivinaAffectStage(
            affects,
            startingMaxHp: 95_000,
            startingMaxMp: 95_000,
            startingDamage: 950_000,
            sourceMobMagic: 0,
            startingMagic: 0);
        Check(belowDeltaCaps.MaxHp == 114_000 && belowDeltaCaps.MaxMp == 114_000 && belowDeltaCaps.Damage == 1_045_000,
            $"Type 34 compared the resulting score rather than the calculated delta to the 7.69 caps: {belowDeltaCaps}.");

        var atDeltaCaps = LegacyCurrentScoreMath.CalculateDivinaAffectStage(
            affects,
            startingMaxHp: 500_000,
            startingMaxMp: 500_000,
            startingDamage: 10_000_000,
            sourceMobMagic: 0,
            startingMagic: 0);
        Check(atDeltaCaps.MaxHp == 100_000 && atDeltaCaps.MaxMp == 100_000 && atDeltaCaps.Damage == 1_000_000,
            $"Type 34 did not set the 7.69 caps when a calculated delta reached its cap: {atDeltaCaps}.");
    }

    private static void Type34LeavesFinalMagicClampToTheLaterScoreBlock()
    {
        var affects = CreateAffectSnapshot((0, Type: (byte)34, Value: (byte)0, Level: (ushort)0));

        var actual = LegacyCurrentScoreMath.CalculateDivinaAffectStage(
            affects,
            startingMaxHp: 1,
            startingMaxMp: 1,
            startingDamage: 1,
            sourceMobMagic: LegacyCurrentScoreMath.TargetMaximumMagic,
            startingMagic: LegacyCurrentScoreMath.TargetMaximumMagic - 10_000_000);

        Check(actual.Magic == 199_000_000,
            $"The Type 34 loop applied the later final Magic clamp too early: Magic={actual.Magic}.");
    }

    private static void OtherAffectsDoNotChangeTheDivinaStage()
    {
        var affects = CreateAffectSnapshot(
            (0, Type: (byte)35, Value: (byte)90, Level: (ushort)200),
            (1, Type: (byte)4, Value: (byte)50, Level: (ushort)100));

        var actual = LegacyCurrentScoreMath.CalculateDivinaAffectStage(
            affects,
            startingMaxHp: 12_345,
            startingMaxMp: 23_456,
            startingDamage: 34_567,
            sourceMobMagic: 45_678,
            startingMagic: 56_789);

        Check(actual.MaxHp == 12_345 && actual.MaxMp == 23_456 && actual.Damage == 34_567 && actual.Magic == 56_789,
            $"A non-Type-34 affect changed the Divina stage: {actual}.");
    }

    private static void TruncatedAffectSnapshotIsRejected()
    {
        var rejected = false;
        try
        {
            LegacyCurrentScoreMath.CalculateDivinaAffectStage(
                new byte[8], startingMaxHp: 100, startingMaxMp: 100, startingDamage: 100,
                sourceMobMagic: 100, startingMagic: 100);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }

        Check(rejected, "The Type 34 stage accepted a truncated 32-record affect snapshot.");
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

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

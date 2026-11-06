using System.Buffers.Binary;
using WydCdk.World;

internal static class CurrentScoreVigorTests
{
    public static void Run()
    {
        Type35UsesTheCompiledSourceFifteenAndSevenPercentages();
        MultipleType35AffectsApplySequentially();
        Type35ComparesTheIncrementWithThe7_69Caps();
        OtherAffectsDoNotChangeTheVigorStage();
        TruncatedAffectSnapshotIsRejected();
    }

    private static void Type35UsesTheCompiledSourceFifteenAndSevenPercentages()
    {
        var affects = CreateAffectSnapshot((0, Type: (byte)35, Value: (byte)255, Level: (ushort)65535));

        var actual = LegacyCurrentScoreMath.CalculateVigorAffectStage(affects, startingMaxHp: 999, startingMaxMp: 101);

        Check(actual.MaxHp == 1_148 && actual.MaxMp == 108,
            $"Type 35 did not apply the compiled 15% HP / 7% MP branch: MaxHP={actual.MaxHp}, MaxMP={actual.MaxMp}.");
    }

    private static void MultipleType35AffectsApplySequentially()
    {
        var affects = CreateAffectSnapshot(
            (0, Type: (byte)35, Value: (byte)0, Level: (ushort)0),
            (4, Type: (byte)35, Value: (byte)0, Level: (ushort)0));

        var actual = LegacyCurrentScoreMath.CalculateVigorAffectStage(affects, startingMaxHp: 50_000, startingMaxMp: 50_001);

        Check(actual.MaxHp == 66_125 && actual.MaxMp == 57_246,
            $"Multiple Type 35 affects were not applied sequentially: MaxHP={actual.MaxHp}, MaxMP={actual.MaxMp}.");
    }

    private static void Type35ComparesTheIncrementWithThe7_69Caps()
    {
        var affects = CreateAffectSnapshot((31, Type: (byte)35, Value: (byte)0, Level: (ushort)0));

        var belowIncrementCap = LegacyCurrentScoreMath.CalculateVigorAffectStage(
            affects, startingMaxHp: 120_000, startingMaxMp: 150_000);
        Check(belowIncrementCap.MaxHp == 138_000 && belowIncrementCap.MaxMp == 160_500,
            $"Type 35 clamped the resulting score instead of comparing the percentage increment: MaxHP={belowIncrementCap.MaxHp}, MaxMP={belowIncrementCap.MaxMp}.");

        var incrementAtCap = LegacyCurrentScoreMath.CalculateVigorAffectStage(
            affects, startingMaxHp: 800_000, startingMaxMp: 1_500_000);
        Check(incrementAtCap.MaxHp == 100_000 && incrementAtCap.MaxMp == 100_000,
            $"Type 35 did not set the score to 100,000 when its percentage increment reached the 7.69 cap: MaxHP={incrementAtCap.MaxHp}, MaxMP={incrementAtCap.MaxMp}.");
    }

    private static void OtherAffectsDoNotChangeTheVigorStage()
    {
        var affects = CreateAffectSnapshot(
            (0, Type: (byte)34, Value: (byte)90, Level: (ushort)200),
            (1, Type: (byte)4, Value: (byte)50, Level: (ushort)100));

        var actual = LegacyCurrentScoreMath.CalculateVigorAffectStage(affects, startingMaxHp: 12_345, startingMaxMp: 23_456);

        Check(actual.MaxHp == 12_345 && actual.MaxMp == 23_456,
            $"A non-Type-35 affect changed the Vigor stage: MaxHP={actual.MaxHp}, MaxMP={actual.MaxMp}.");
    }

    private static void TruncatedAffectSnapshotIsRejected()
    {
        var rejected = false;
        try { LegacyCurrentScoreMath.CalculateVigorAffectStage(new byte[8], startingMaxHp: 100, startingMaxMp: 100); }
        catch (ArgumentException) { rejected = true; }

        Check(rejected, "The Type 35 stage accepted a truncated 32-record affect snapshot.");
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

using System.Buffers.Binary;
using WydCdk.World;

internal static class AttackPvpJewelParryTests
{
    public static void Run()
    {
        PositivePlayerHitSubtractsThirtyPerType8Bit6AffectWithoutClamping();
        NonPositiveDamageAndNpcTargetsDoNotChangeParry();
        TruncatedAffectSnapshotIsRejected();
    }

    private static void PositivePlayerHitSubtractsThirtyPerType8Bit6AffectWithoutClamping()
    {
        var affects = CreateAffectSnapshot(
            (0, (byte)8, (ushort)(1 << 6)),
            (1, (byte)8, (ushort)(1 << 3)),
            (2, (byte)34, (ushort)(1 << 6)),
            (31, (byte)8, (ushort)((1 << 2) | (1 << 6))));

        var actual = LegacyPvpJewelParryMath.ApplyParryPenalty(
            affects, targetParry: 10, damage: 1, isPlayerTarget: true);

        Check(actual == -50,
            $"A positive player hit did not subtract 30 for each Type 8 bit 6 affect without a Parry floor: {actual}.");
    }

    private static void NonPositiveDamageAndNpcTargetsDoNotChangeParry()
    {
        var affects = CreateAffectSnapshot((0, (byte)8, (ushort)(1 << 6)));

        Check(LegacyPvpJewelParryMath.ApplyParryPenalty(
                affects, targetParry: 80, damage: 0, isPlayerTarget: true) == 80,
            "A zero-damage action reduced target Parry.");
        Check(LegacyPvpJewelParryMath.ApplyParryPenalty(
                affects, targetParry: 80, damage: -1, isPlayerTarget: true) == 80,
            "A negative-damage action reduced target Parry.");
        Check(LegacyPvpJewelParryMath.ApplyParryPenalty(
                affects, targetParry: 80, damage: 1, isPlayerTarget: false) == 80,
            "A hit against an NPC reduced target Parry.");
    }

    private static void TruncatedAffectSnapshotIsRejected()
    {
        var rejected = false;
        try
        {
            LegacyPvpJewelParryMath.ApplyParryPenalty(
                new byte[LegacyAccountSnapshot.AffectStride - 1],
                targetParry: 80,
                damage: 1,
                isPlayerTarget: true);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }

        Check(rejected, "A truncated 32-record affect snapshot was accepted.");
    }

    private static byte[] CreateAffectSnapshot(params (int Slot, byte Type, ushort Level)[] entries)
    {
        var affects = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var entry in entries)
        {
            var offset = entry.Slot * 8;
            affects[offset] = entry.Type;
            BinaryPrimitives.WriteUInt16LittleEndian(affects.AsSpan(offset + 2), entry.Level);
        }

        return affects;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

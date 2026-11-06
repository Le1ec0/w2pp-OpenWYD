using System.Buffers.Binary;

namespace WydCdk.World;

/// <summary>Pure attack-side Parry arithmetic for Type 8 bit 6 in TMProject 7.69.</summary>
public static class LegacyPvpJewelParryMath
{
    private const int AffectCount = 32;
    private const int AffectSizeInBytes = 8;
    private const byte PvpJewelAffectType = 8;
    private const ushort ParryPenaltyBit = 1 << 6;
    private const int ParryPenaltyPerAffect = 30;

    /// <summary>
    /// Subtracts 30 Parry for each Type 8 bit 6 affect after positive damage to a
    /// player target. The returned raw Parry is not clamped.
    /// </summary>
    public static int ApplyParryPenalty(
        ReadOnlySpan<byte> attackerAffectSnapshot,
        int targetParry,
        int damage,
        bool isPlayerTarget)
    {
        if (attackerAffectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(attackerAffectSnapshot));

        var parry = targetParry;
        if (damage < 1 || !isPlayerTarget)
            return parry;

        for (var slot = 0; slot < AffectCount; slot++)
        {
            var offset = slot * AffectSizeInBytes;
            if (attackerAffectSnapshot[offset] != PvpJewelAffectType)
                continue;

            var level = BinaryPrimitives.ReadUInt16LittleEndian(attackerAffectSnapshot.Slice(offset + 2, sizeof(ushort)));
            if ((level & ParryPenaltyBit) == 0)
                continue;

            parry = unchecked(parry - ParryPenaltyPerAffect);
        }

        return parry;
    }
}

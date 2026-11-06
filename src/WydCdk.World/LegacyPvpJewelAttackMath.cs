using System.Buffers.Binary;

namespace WydCdk.World;

/// <summary>HP result of the active Type=8 bit 3 attack-side absorb in TMProject 7.69.</summary>
public readonly record struct LegacyPvpJewelAbsorptionStage(int CurrentHp, int AbsorbedHp);

/// <summary>Pure attack-side arithmetic for the active 7.69 PvP-jewel effects.</summary>
public static class LegacyPvpJewelAttackMath
{
    private const int AffectCount = 32;
    private const int AffectSizeInBytes = 8;
    private const byte PvpJewelAffectType = 8;
    private const ushort AbsorbBit = 1 << 3;
    private const int MaximumAbsorbPerAffect = 350;

    /// <summary>
    /// Applies the Type=8 bit 3 recovery after positive MSG_Attack damage. Each active
    /// affect restores integer-truncated 2% of final damage, capped at 350. The
    /// source's MaxHP overflow adjustment is preserved, including its occasional
    /// result above MaxHP.
    /// </summary>
    public static LegacyPvpJewelAbsorptionStage ApplyAbsorption(
        ReadOnlySpan<byte> affectSnapshot,
        int damage,
        int startingHp,
        int maximumHp)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var hp = startingHp;
        var totalAbsorbed = 0;
        if (damage < 1)
            return new LegacyPvpJewelAbsorptionStage(hp, totalAbsorbed);

        for (var slot = 0; slot < AffectCount; slot++)
        {
            var offset = slot * AffectSizeInBytes;
            if (affectSnapshot[offset] != PvpJewelAffectType)
                continue;

            var level = BinaryPrimitives.ReadUInt16LittleEndian(affectSnapshot.Slice(offset + 2, sizeof(ushort)));
            if ((level & AbsorbBit) == 0)
                continue;

            var absorbed = Math.Min(damage * 2 / 100, MaximumAbsorbPerAffect);
            if (hp >= maximumHp)
                absorbed = 0;

            var newHp = unchecked(hp + absorbed);
            if (newHp > maximumHp)
            {
                newHp = unchecked(newHp - maximumHp);
                absorbed = newHp;
            }

            hp = unchecked(hp + absorbed);
            totalAbsorbed = unchecked(totalAbsorbed + absorbed);
        }

        return new LegacyPvpJewelAbsorptionStage(hp, totalAbsorbed);
    }
}

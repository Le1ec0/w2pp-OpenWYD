using System.Buffers.Binary;

namespace WydCdk.World;

/// <summary>Derives the 7.69 Type 30 bonus added to positive MSG_Attack damage against mob targets.</summary>
public static class LegacyForceMobDamageMath
{
    private const byte ForceMobDamageAffectType = 30;
    private const int AffectCount = 32;
    private const int AffectSizeInBytes = 8;

    public static int GetBonus(ReadOnlySpan<byte> affectSnapshot)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var bonus = 0;
        for (var slot = 0; slot < AffectCount; slot++)
        {
            var offset = slot * AffectSizeInBytes;
            if (affectSnapshot[offset] != ForceMobDamageAffectType)
                continue;

            var level = BinaryPrimitives.ReadUInt16LittleEndian(affectSnapshot.Slice(offset + 2, sizeof(ushort)));
            bonus = unchecked(bonus + level);
        }

        return bonus;
    }

    /// <summary>Applies the bonus only when the caller has identified a non-player mob target.</summary>
    public static int ApplyToMobTargetDamage(int damage, ReadOnlySpan<byte> attackerAffect)
    {
        if (damage < 1)
            return damage;

        return unchecked(damage + GetBonus(attackerAffect));
    }
}

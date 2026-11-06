using System.Buffers.Binary;

namespace WydCdk.World;

/// <summary>Measured adapters for fields stored outside the legacy 816-byte MOB.</summary>
public static class LegacyMobExtra
{
    public const uint DivineProtectionBookLearnedBit = 0x1;
    public const uint DivineBlessingLearnedBit = 0x2;
    public const uint BloodHandsLearnedBit = 0x10;
    public const uint MagicMirrorLearnedBit = 0x100;

    /// <summary>Reads the raw 32-bit SecLearnedSkill mask from a complete 7.69 MOBEXTRA slot.</summary>
    public static bool TryReadSecondaryLearnedSkill(ReadOnlySpan<byte> mobExtra, out uint mask)
    {
        mask = 0;
        if (mobExtra.Length < LegacyAccountSnapshot.MobExtraStride)
            return false;

        mask = BinaryPrimitives.ReadUInt32LittleEndian(
            mobExtra[LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset..]);
        return true;
    }

    /// <summary>Changes only Divine Blessing's bit in a complete slot; used to seed isolated test fixtures.</summary>
    public static bool TrySetDivineBlessingLearnedBit(Span<byte> mobExtra, bool enabled)
    {
        if (mobExtra.Length < LegacyAccountSnapshot.MobExtraStride)
            return false;

        var field = mobExtra.Slice(LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset, sizeof(uint));
        var mask = BinaryPrimitives.ReadUInt32LittleEndian(field);
        mask = enabled ? mask | DivineBlessingLearnedBit : mask & ~DivineBlessingLearnedBit;
        BinaryPrimitives.WriteUInt32LittleEndian(field, mask);
        return true;
    }

    /// <summary>Changes only Mãos Sangrentas' bit in a complete slot; used to seed isolated test fixtures.</summary>
    public static bool TrySetBloodHandsLearnedBit(Span<byte> mobExtra, bool enabled)
    {
        if (mobExtra.Length < LegacyAccountSnapshot.MobExtraStride)
            return false;

        var field = mobExtra.Slice(LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset, sizeof(uint));
        var mask = BinaryPrimitives.ReadUInt32LittleEndian(field);
        mask = enabled ? mask | BloodHandsLearnedBit : mask & ~BloodHandsLearnedBit;
        BinaryPrimitives.WriteUInt32LittleEndian(field, mask);
        return true;
    }

    /// <summary>Changes only Espelho Mágico's bit in a complete slot.</summary>
    public static bool TrySetMagicMirrorLearnedBit(Span<byte> mobExtra, bool enabled)
    {
        if (mobExtra.Length < LegacyAccountSnapshot.MobExtraStride)
            return false;

        var field = mobExtra.Slice(LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset, sizeof(uint));
        var mask = BinaryPrimitives.ReadUInt32LittleEndian(field);
        mask = enabled ? mask | MagicMirrorLearnedBit : mask & ~MagicMirrorLearnedBit;
        BinaryPrimitives.WriteUInt32LittleEndian(field, mask);
        return true;
    }

    /// <summary>Changes only item 5500's bit, which differs from Divine Blessing's combat bit.</summary>
    public static bool TrySetDivineProtectionBookLearnedBit(Span<byte> mobExtra, bool enabled)
    {
        if (mobExtra.Length < LegacyAccountSnapshot.MobExtraStride)
            return false;

        var field = mobExtra.Slice(LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset, sizeof(uint));
        var mask = BinaryPrimitives.ReadUInt32LittleEndian(field);
        mask = enabled ? mask | DivineProtectionBookLearnedBit : mask & ~DivineProtectionBookLearnedBit;
        BinaryPrimitives.WriteUInt32LittleEndian(field, mask);
        return true;
    }
}

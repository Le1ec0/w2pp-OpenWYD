using System.Buffers.Binary;
using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>Combat fields read from the 816-byte legacy STRUCT_MOB.</summary>
public sealed record LegacyMobCombatState(byte CharacterClass, uint LearnedSkill, int Magic, LegacyScore CurrentScore, byte SaveMana)
{
    public LegacyScore BaseScore { get; init; }
    public ushort Rsv { get; init; }
    public ushort RegenMp { get; init; }
    public long Experience { get; init; }
    public int CurrentMana => CurrentScore.Mp;

    public static LegacyMobCombatState Read(ReadOnlySpan<byte> mob)
    {
        if (mob.Length < LegacyAccountSnapshot.CharacterStride)
            throw new ArgumentException($"A legacy MOB must contain at least {LegacyAccountSnapshot.CharacterStride} bytes.", nameof(mob));

        return new LegacyMobCombatState(
            mob[LegacyAccountSnapshot.MobClassOffset],
            unchecked((uint)BinaryPrimitives.ReadInt32LittleEndian(mob[LegacyAccountSnapshot.MobLearnedSkillOffset..])),
            BinaryPrimitives.ReadInt32LittleEndian(mob[LegacyAccountSnapshot.MobMagicOffset..]),
            LegacyScore.Read(mob[LegacyAccountSnapshot.MobCurrentScoreOffset..]),
            mob[LegacyAccountSnapshot.MobSaveManaOffset])
        {
            BaseScore = LegacyScore.Read(mob[LegacyAccountSnapshot.MobBaseScoreOffset..]),
            Rsv = BinaryPrimitives.ReadUInt16LittleEndian(mob[LegacyAccountSnapshot.MobRsvOffset..]),
            RegenMp = BinaryPrimitives.ReadUInt16LittleEndian(mob[LegacyAccountSnapshot.MobRegenMpOffset..]),
            Experience = BinaryPrimitives.ReadInt64LittleEndian(mob[LegacyAccountSnapshot.MobExperienceOffset..]),
        };
    }
}

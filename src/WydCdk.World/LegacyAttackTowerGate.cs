using System.Buffers.Binary;

namespace WydCdk.World;

/// <summary>Guild eligibility for the TMProject 7.69 guild tower target.</summary>
public static class LegacyAttackTowerGate
{
    public const int TowerGenerateIndex = 1076;

    public static bool IsBlocked(int targetGenerateIndex, ushort attackerGuild, ushort targetGuild) =>
        targetGenerateIndex == TowerGenerateIndex && (attackerGuild == 0 || attackerGuild == targetGuild);

    public static bool IsBlocked(int targetGenerateIndex, ReadOnlySpan<byte> attackerMob, ReadOnlySpan<byte> targetMob)
    {
        if (targetGenerateIndex != TowerGenerateIndex)
            return false;

        var requiredLength = LegacyAccountSnapshot.MobGuildOffset + sizeof(ushort);
        if (attackerMob.Length < requiredLength || targetMob.Length < requiredLength)
            return true;

        var attackerGuild = BinaryPrimitives.ReadUInt16LittleEndian(attackerMob[LegacyAccountSnapshot.MobGuildOffset..]);
        var targetGuild = BinaryPrimitives.ReadUInt16LittleEndian(targetMob[LegacyAccountSnapshot.MobGuildOffset..]);
        return IsBlocked(targetGenerateIndex, attackerGuild, targetGuild);
    }
}

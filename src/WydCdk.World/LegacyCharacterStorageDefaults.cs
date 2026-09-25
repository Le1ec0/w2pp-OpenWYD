using System.Buffers.Binary;
using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Reproduces the persisted part of the legacy BASE_ClearMob/BASE_ClearMobExtra reset.
/// </summary>
public static class LegacyCharacterStorageDefaults
{
    public const short EmptyCharacterPosition = 2112;
    private const short KillMarkItemIndex = 547;
    private const byte CurrentKillEffect = 75;
    private const byte LowTotalKillEffect = 76;
    private const byte HighTotalKillEffect = 77;

    /// <summary>
    /// Reproduces TMSrv's ProcessDBMessage initialization of Carry[KILL_MARK] when
    /// an older account has no kill-mark item yet. The reference stores the
    /// initial PK/chaos value in stEffect[0].cEffect and starts it at 75.
    /// </summary>
    public static byte[] EnsureKillMark(ReadOnlySpan<byte> mob)
    {
        var normalized = mob.ToArray();
        var offset = LegacyAccountSnapshot.MobCarryOffset + (LegacyAccountSnapshot.LegacyKillMarkCarrySlot * LegacyItem.SizeInBytes);
        if (normalized.Length < offset + LegacyItem.SizeInBytes)
            return normalized;

        if (LegacyItem.Read(normalized.AsSpan(offset, LegacyItem.SizeInBytes)).Index == 0)
            new LegacyItem(KillMarkItemIndex, CurrentKillEffect, 0, LowTotalKillEffect, 0, HighTotalKillEffect, 0)
                .Write(normalized.AsSpan(offset, LegacyItem.SizeInBytes));

        return normalized;
    }

    public static void ResetDeletedCharacterSlot(byte[] accountFile, int slot)
    {
        ArgumentNullException.ThrowIfNull(accountFile);
        if ((uint)slot >= LegacyAccountSnapshot.CharacterCount)
            throw new ArgumentOutOfRangeException(nameof(slot));
        if (accountFile.Length < LegacyAccountSnapshot.RequiredFileLength)
            throw new ArgumentException("The account file is shorter than the legacy account layout.", nameof(accountFile));

        var characterOffset = LegacyAccountSnapshot.CharactersOffset + (slot * LegacyAccountSnapshot.CharacterStride);
        accountFile.AsSpan(characterOffset, LegacyAccountSnapshot.CharacterStride).Clear();
        BinaryPrimitives.WriteInt16LittleEndian(accountFile.AsSpan(characterOffset + LegacyAccountSnapshot.MobSavedPositionXOffset), EmptyCharacterPosition);
        BinaryPrimitives.WriteInt16LittleEndian(accountFile.AsSpan(characterOffset + LegacyAccountSnapshot.MobSavedPositionYOffset), EmptyCharacterPosition);

        accountFile.AsSpan(LegacyAccountSnapshot.ShortSkillOffset + (slot * LegacyAccountSnapshot.ShortSkillStride), LegacyAccountSnapshot.ShortSkillStride).Clear();

        var extraOffset = LegacyAccountSnapshot.MobExtraOffset + (slot * LegacyAccountSnapshot.MobExtraStride);
        accountFile.AsSpan(extraOffset, LegacyAccountSnapshot.MobExtraStride).Clear();
        BinaryPrimitives.WriteInt16LittleEndian(accountFile.AsSpan(extraOffset + LegacyAccountSnapshot.MobExtraClassMasterOffset), LegacyAccountSnapshot.ClassMasterMortal);
    }
}

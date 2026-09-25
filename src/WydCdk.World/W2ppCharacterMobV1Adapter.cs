using System.Buffers.Binary;
using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Maps W2PP MOB state to the client 7.69 layout. The 7.69 second LearnedSkill
/// word stores skills 200-246, but W2PP MOB has no such field; its SecLearnedSkill
/// is a distinct MOBEXTRA field, so the target-only word is initialized to zero.
/// </summary>
public static class W2ppCharacterMobV1Adapter
{
    public const int SourceMobSize = 816;
    private const int SourceNameOffset = 0;
    private const int SourceClanOffset = 16;
    private const int SourceMerchantOffset = 17;
    private const int SourceGuildOffset = 18;
    private const int SourceClassOffset = 20;
    private const int SourceRsvOffset = 22;
    private const int SourceQuestOffset = 24;
    private const int SourceCoinOffset = 28;
    private const int SourceExperienceOffset = 32;
    private const int SourceBaseScoreOffset = 44;
    private const int SourceCurrentScoreOffset = 92;
    private const int SourceEquipmentOffset = 140;
    private const int SourceEquipmentCount = 16;
    private const int SourceCarryOffset = 268;
    private const int SourceLearnedSkillOffset = 780;
    private const int SourceMagicOffset = 784;
    private const int SourceScoreBonusOffset = 788;
    private const int SourceSpecialBonusOffset = 790;
    private const int SourceSkillBonusOffset = 792;
    private const int SourceCriticalOffset = 794;
    private const int SourceSaveManaOffset = 795;
    private const int SourceSkillBarOffset = 796;
    private const int SourceGuildLevelOffset = 800;
    private const int SourceRegenHpOffset = 802;
    private const int SourceRegenMpOffset = 804;
    private const int SourceResistOffset = 806;
    private const int KillMarkIndex = 547;
    private const byte GuiltyMaximum = 50;

    /// <param name="legacyMob">Exact 816-byte W2PP STRUCT_MOB.</param>
    /// <param name="spawnX">Resolved live login spawn, not merely persisted SPX.</param>
    /// <param name="spawnY">Resolved live login spawn, not merely persisted SPY.</param>
    public static CharacterMobV769 Adapt(
        ReadOnlySpan<byte> legacyMob,
        short spawnX,
        short spawnY,
        IReadOnlyList<LegacyItem>? targetEquipment = null)
    {
        if (legacyMob.Length != SourceMobSize)
            throw new ArgumentException($"W2PP STRUCT_MOB requires exactly {SourceMobSize} bytes.", nameof(legacyMob));
        if (targetEquipment is not null && targetEquipment.Count != CharacterMobV769.EquipmentCount)
            throw new ArgumentException($"The client 7.69 equipment projection requires exactly {CharacterMobV769.EquipmentCount} entries.", nameof(targetEquipment));

        // W2PP initializes an absent KILL_MARK before it answers character login.
        // Work on a copy so projection never mutates the persisted source buffer.
        var mob = LegacyCharacterStorageDefaults.EnsureKillMark(legacyMob);
        var rsv = BinaryPrimitives.ReadUInt16LittleEndian(mob.AsSpan(SourceRsvOffset));
        if ((rsv & 0xFF00) != 0)
            throw new InvalidOperationException("W2PP MOB.Rsv has high-byte flags that the one-byte 7.69 client field cannot represent.");

        var equipment = new LegacyItem[CharacterMobV769.EquipmentCount];
        if (targetEquipment is not null)
        {
            for (var index = 0; index < equipment.Length; index++)
                equipment[index] = targetEquipment[index];
        }
        else
        {
            for (var index = 0; index < SourceEquipmentCount; index++)
            {
                var offset = SourceEquipmentOffset + index * LegacyItem.SizeInBytes;
                equipment[index] = LegacyItem.Read(mob.AsSpan(offset, LegacyItem.SizeInBytes));
            }
        }

        var carry = new LegacyItem[CharacterMobV769.CarryCount];
        for (var index = 0; index < carry.Length; index++)
        {
            var offset = SourceCarryOffset + index * LegacyItem.SizeInBytes;
            carry[index] = LegacyItem.Read(mob.AsSpan(offset, LegacyItem.SizeInBytes));
        }

        var markerOffset = SourceCarryOffset + (LegacyAccountSnapshot.LegacyKillMarkCarrySlot * LegacyItem.SizeInBytes);
        var marker = mob.AsSpan(markerOffset, LegacyItem.SizeInBytes);
        if (BinaryPrimitives.ReadInt16LittleEndian(marker) != KillMarkIndex)
            throw new InvalidOperationException("W2PP KILL_MARK normalization did not produce item 547.");

        var currentKill = marker[3]; // GetCurKill: stEffect[0].cValue.
        var totalKill = (ushort)(marker[5] | (marker[7] << 8)); // GetTotKill: effect 1 low + effect 2 high.
        var guilty = marker[4]; // GetGuilty: stEffect[1].cEffect; values >50 are cleared by W2PP.
        var chaos = guilty is > 0 and <= GuiltyMaximum ? (byte)0 : marker[2]; // GetPKPoint then guilt override.

        // The field client reserves name bytes 12..15 for chaos/current/total kills
        // when it creates the logged-in player. W2PP's GetCreateMob emits exactly
        // this suffix for other players; the login path needs the same projection.
        var mobName = new byte[CharacterMobV769.NameLength];
        mob.AsSpan(SourceNameOffset, 12).CopyTo(mobName);
        mobName[12] = chaos;
        mobName[13] = currentKill;
        mobName[14] = marker[5];
        mobName[15] = marker[7];

        var sourceMagic = BinaryPrimitives.ReadUInt32LittleEndian(mob.AsSpan(SourceMagicOffset));
        var sourceRegenHp = BinaryPrimitives.ReadUInt16LittleEndian(mob.AsSpan(SourceRegenHpOffset));
        var sourceRegenMp = BinaryPrimitives.ReadUInt16LittleEndian(mob.AsSpan(SourceRegenMpOffset));
        var sourceScoreBonus = BinaryPrimitives.ReadUInt16LittleEndian(mob.AsSpan(SourceScoreBonusOffset));
        var sourceSpecialBonus = BinaryPrimitives.ReadUInt16LittleEndian(mob.AsSpan(SourceSpecialBonusOffset));
        var sourceSkillBonus = BinaryPrimitives.ReadUInt16LittleEndian(mob.AsSpan(SourceSkillBonusOffset));

        return new CharacterMobV769(
            mobName,
            mob[SourceClanOffset],
            mob[SourceMerchantOffset],
            BinaryPrimitives.ReadUInt16LittleEndian(mob.AsSpan(SourceGuildOffset)),
            mob[SourceClassOffset],
            (byte)rsv,
            mob[SourceQuestOffset], // W2PP byte -> client ushort, zero-extended.
            BinaryPrimitives.ReadInt32LittleEndian(mob.AsSpan(SourceCoinOffset)),
            BinaryPrimitives.ReadInt64LittleEndian(mob.AsSpan(SourceExperienceOffset)),
            checked((ushort)spawnX),
            checked((ushort)spawnY),
            AdaptScore(LegacyScore.Read(mob.AsSpan(SourceBaseScoreOffset, LegacyScore.SizeInBytes))),
            AdaptScore(LegacyScore.Read(mob.AsSpan(SourceCurrentScoreOffset, LegacyScore.SizeInBytes))),
            equipment,
            carry,
            unchecked((uint)BinaryPrimitives.ReadInt32LittleEndian(mob.AsSpan(SourceLearnedSkillOffset))),
            0, // 7.69 skills 200-246 have no source field in W2PP STRUCT_MOB.
            CheckedShort(sourceScoreBonus, nameof(sourceScoreBonus)),
            CheckedShort(sourceSpecialBonus, nameof(sourceSpecialBonus)),
            CheckedShort(sourceSkillBonus, nameof(sourceSkillBonus)),
            mob[SourceCriticalOffset],
            mob[SourceSaveManaOffset],
            mob.AsMemory(SourceSkillBarOffset, 4),
            mob[SourceGuildLevelOffset],
            checked((byte)sourceMagic),
            checked((byte)sourceRegenHp),
            checked((byte)sourceRegenMp),
            mob.AsMemory(SourceResistOffset, 4),
            currentKill,
            totalKill);
    }

    private static ClientScoreV769 AdaptScore(LegacyScore score) => new(
        checked((short)score.Level),
        score.Ac,
        score.Damage,
        Reserved: 0,
        score.AttackRun,
        score.MaxHp,
        score.MaxMp,
        score.Hp,
        score.Mp,
        score.Strength,
        score.Intelligence,
        score.Dexterity,
        score.Constitution,
        unchecked((ushort)score.Special1),
        unchecked((ushort)score.Special2),
        unchecked((ushort)score.Special3),
        unchecked((ushort)score.Special4));

    private static short CheckedShort(ushort value, string fieldName) =>
        value <= short.MaxValue
            ? (short)value
            : throw new OverflowException($"W2PP {fieldName} value {value} does not fit the signed 7.69 MOB field.");
}

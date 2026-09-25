using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Typed client 7.69 STRUCT_MOB. Its offsets are the Win32 layout measured by
/// tools/PortAudit/StructLayoutProbe.cpp; it is not the 816-byte W2PP storage MOB.
/// </summary>
public sealed class CharacterMobV769
{
    public const int SizeInBytes = 1_040;
    public const int NameLength = 16;
    public const int EquipmentCount = 18;
    public const int CarryCount = 64;
    public const int NameOffset = 0;
    public const int ClanOffset = 16;
    public const int MerchantOffset = 17;
    public const int GuildOffset = 18;
    public const int ClassOffset = 20;
    public const int ReservedOffset = 21;
    public const int QuestOffset = 22;
    public const int CoinOffset = 24;
    public const int ExperienceOffset = 32;
    public const int HomeTownXOffset = 40;
    public const int HomeTownYOffset = 42;
    public const int BaseScoreOffset = 44;
    public const int CurrentScoreOffset = 92;
    public const int EquipmentOffset = 140;
    public const int CarryOffset = 284;
    public const int LearnedSkillOffset = 796;
    public const int ScoreBonusOffset = 804;
    public const int SpecialBonusOffset = 806;
    public const int SkillBonusOffset = 808;
    public const int CriticalOffset = 810;
    public const int SaveManaOffset = 811;
    public const int ShortSkillOffset = 812;
    public const int GuildLevelOffset = 816;
    public const int MagicOffset = 817;
    public const int RegenHpOffset = 818;
    public const int RegenMpOffset = 819;
    public const int ResistOffset = 820;
    public const int DummyOffset = 824;
    public const int DummyLength = 212;
    public const int CurrentKillOffset = 1_036;
    public const int TotalKillOffset = 1_038;

    private readonly byte[] mobName;
    private readonly LegacyItem[] equipment;
    private readonly LegacyItem[] carry;
    private readonly byte[] shortSkill;
    private readonly byte[] resist;

    public byte Clan { get; }
    public byte Merchant { get; }
    public ushort Guild { get; }
    public byte CharacterClass { get; }
    public byte Reserved { get; }
    public ushort Quest { get; }
    public int Coin { get; }
    public long Experience { get; }
    public ushort HomeTownX { get; }
    public ushort HomeTownY { get; }
    public ClientScoreV769 BaseScore { get; }
    public ClientScoreV769 CurrentScore { get; }
    public uint LearnedSkill0 { get; }
    public uint LearnedSkill1 { get; }
    public short ScoreBonus { get; }
    public short SpecialBonus { get; }
    public short SkillBonus { get; }
    public byte Critical { get; }
    public byte SaveMana { get; }
    public byte GuildLevel { get; }
    public byte Magic { get; }
    public byte RegenHp { get; }
    public byte RegenMp { get; }
    public ushort CurrentKill { get; }
    public ushort TotalKill { get; }
    public ReadOnlyMemory<byte> MobName => mobName;
    public IReadOnlyList<LegacyItem> Equipment { get; }
    public IReadOnlyList<LegacyItem> Carry { get; }
    public ReadOnlyMemory<byte> ShortSkill => shortSkill;
    public ReadOnlyMemory<byte> Resist => resist;

    public CharacterMobV769(
        ReadOnlyMemory<byte> mobName,
        byte clan,
        byte merchant,
        ushort guild,
        byte characterClass,
        byte reserved,
        ushort quest,
        int coin,
        long experience,
        ushort homeTownX,
        ushort homeTownY,
        ClientScoreV769 baseScore,
        ClientScoreV769 currentScore,
        IReadOnlyList<LegacyItem> equipment,
        IReadOnlyList<LegacyItem> carry,
        uint learnedSkill0,
        uint learnedSkill1,
        short scoreBonus,
        short specialBonus,
        short skillBonus,
        byte critical,
        byte saveMana,
        ReadOnlyMemory<byte> shortSkill,
        byte guildLevel,
        byte magic,
        byte regenHp,
        byte regenMp,
        ReadOnlyMemory<byte> resist,
        ushort currentKill,
        ushort totalKill)
    {
        RequireLength(mobName, NameLength, nameof(mobName));
        RequireLength(shortSkill, 4, nameof(shortSkill));
        RequireLength(resist, 4, nameof(resist));
        if (equipment.Count != EquipmentCount)
            throw new ArgumentException($"Client 7.69 MOB requires {EquipmentCount} equipment entries.", nameof(equipment));
        if (carry.Count != CarryCount)
            throw new ArgumentException($"Client 7.69 MOB requires {CarryCount} carry entries.", nameof(carry));

        this.mobName = mobName.ToArray();
        this.equipment = equipment.ToArray();
        this.carry = carry.ToArray();
        this.shortSkill = shortSkill.ToArray();
        this.resist = resist.ToArray();
        Equipment = Array.AsReadOnly(this.equipment);
        Carry = Array.AsReadOnly(this.carry);
        Clan = clan;
        Merchant = merchant;
        Guild = guild;
        CharacterClass = characterClass;
        Reserved = reserved;
        Quest = quest;
        Coin = coin;
        Experience = experience;
        HomeTownX = homeTownX;
        HomeTownY = homeTownY;
        BaseScore = baseScore;
        CurrentScore = currentScore;
        LearnedSkill0 = learnedSkill0;
        LearnedSkill1 = learnedSkill1;
        ScoreBonus = scoreBonus;
        SpecialBonus = specialBonus;
        SkillBonus = skillBonus;
        Critical = critical;
        SaveMana = saveMana;
        GuildLevel = guildLevel;
        Magic = magic;
        RegenHp = regenHp;
        RegenMp = regenMp;
        CurrentKill = currentKill;
        TotalKill = totalKill;
    }

    public byte[] ToBytes()
    {
        var bytes = new byte[SizeInBytes];
        Write(bytes);
        return bytes;
    }

    public void Write(Span<byte> destination)
    {
        if (destination.Length < SizeInBytes)
            throw new ArgumentException($"Client 7.69 MOB requires {SizeInBytes} bytes.", nameof(destination));

        var mob = destination[..SizeInBytes];
        mob.Clear(); // ABI padding and the 212-byte dummy region are deterministic.
        mobName.CopyTo(mob[NameOffset..]);
        mob[ClanOffset] = Clan;
        mob[MerchantOffset] = Merchant;
        BinaryPrimitives.WriteUInt16LittleEndian(mob[GuildOffset..], Guild);
        mob[ClassOffset] = CharacterClass;
        mob[ReservedOffset] = Reserved;
        BinaryPrimitives.WriteUInt16LittleEndian(mob[QuestOffset..], Quest);
        BinaryPrimitives.WriteInt32LittleEndian(mob[CoinOffset..], Coin);
        BinaryPrimitives.WriteInt64LittleEndian(mob[ExperienceOffset..], Experience);
        BinaryPrimitives.WriteUInt16LittleEndian(mob[HomeTownXOffset..], HomeTownX);
        BinaryPrimitives.WriteUInt16LittleEndian(mob[HomeTownYOffset..], HomeTownY);
        BaseScore.Write(mob[BaseScoreOffset..]);
        CurrentScore.Write(mob[CurrentScoreOffset..]);
        for (var index = 0; index < equipment.Length; index++)
            equipment[index].Write(mob.Slice(EquipmentOffset + index * LegacyItem.SizeInBytes, LegacyItem.SizeInBytes));
        for (var index = 0; index < carry.Length; index++)
            carry[index].Write(mob.Slice(CarryOffset + index * LegacyItem.SizeInBytes, LegacyItem.SizeInBytes));
        BinaryPrimitives.WriteUInt32LittleEndian(mob[LearnedSkillOffset..], LearnedSkill0);
        BinaryPrimitives.WriteUInt32LittleEndian(mob[(LearnedSkillOffset + 4)..], LearnedSkill1);
        BinaryPrimitives.WriteInt16LittleEndian(mob[ScoreBonusOffset..], ScoreBonus);
        BinaryPrimitives.WriteInt16LittleEndian(mob[SpecialBonusOffset..], SpecialBonus);
        BinaryPrimitives.WriteInt16LittleEndian(mob[SkillBonusOffset..], SkillBonus);
        mob[CriticalOffset] = Critical;
        mob[SaveManaOffset] = SaveMana;
        shortSkill.CopyTo(mob[ShortSkillOffset..]);
        mob[GuildLevelOffset] = GuildLevel;
        mob[MagicOffset] = Magic;
        mob[RegenHpOffset] = RegenHp;
        mob[RegenMpOffset] = RegenMp;
        resist.CopyTo(mob[ResistOffset..]);
        BinaryPrimitives.WriteUInt16LittleEndian(mob[CurrentKillOffset..], CurrentKill);
        BinaryPrimitives.WriteUInt16LittleEndian(mob[TotalKillOffset..], TotalKill);
    }

    private static void RequireLength(ReadOnlyMemory<byte> value, int expected, string parameterName)
    {
        if (value.Length != expected)
            throw new ArgumentException($"Client 7.69 MOB {parameterName} requires exactly {expected} bytes.", parameterName);
    }
}

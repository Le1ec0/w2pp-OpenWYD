using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Read-only snapshot of the STRUCT_ACCOUNTFILE fields required to answer login with real data, matching
/// CFileDB's handler for _MSG_DBAccountLogin (W2PP\Source\Code\DBSrv\CFileDB.cpp:611, confirmation built at
/// line ~758-790): the four character slots (via DBGetSelChar), cargo, account coin, whether the welcome
/// item was already received, the daily-quest progress, and the block state. All offsets below were
/// confirmed with a compiled offsetof/sizeof probe against the actual W2PP Basedef.h using this project's
/// x86 toolset (no explicit struct packing in this region; STRUCT_QUEST/STRUCT_MOBEXTRA pull in a 64-bit
/// time_t because this fork's build does not define _USE_32BIT_TIME_T) — not hand-computed from field lists,
/// since MSVC alignment/padding in nested anonymous structs is easy to get wrong by inspection alone.
/// ShortSkill, affect, Donate, and TempKey are skipped: nothing in the login-confirmation flow reads them.
/// </summary>
public sealed record LegacyAccountSnapshot(
    string AccountName,
    LegacyCharacterSelection Characters,
    IReadOnlyList<LegacyItem> Cargo,
    int Coin,
    bool WelcomeItemReceived,
    LegacyDailyQuest DailyQuest,
    string BlockPassword,
    bool IsBlocked)
{
    private const int NameFieldLength = 16;
    public const int NumericTokenOffset = 202; // STRUCT_ACCOUNTINFO.NumericToken (six raw digit bytes, not null-terminated)
    public const int NumericTokenLength = 6;
    public const int CharactersOffset = 216; // sizeof(STRUCT_ACCOUNTINFO)
    public const int CharacterStride = 816; // sizeof(STRUCT_MOB)
    public const int CharacterCount = 4; // MOB_PER_ACCOUNT
    private const int CargoOffset = 3480; // sizeof(STRUCT_ACCOUNTINFO) + 4 * sizeof(STRUCT_MOB)
    private const int CargoCount = 128; // MAX_CARGO
    public const int AccountCoinOffset = 4504;
    // ShortSkill/affect confirmed by the same compiled offsetof probe as the rest of this file (see class remarks):
    // offsetof(STRUCT_ACCOUNTFILE, ShortSkill)=4508, offsetof(..., affect)=4572, sizeof(STRUCT_AFFECT)=8, MAX_AFFECT=32.
    public const int ShortSkillOffset = 4508;
    public const int ShortSkillStride = 16; // per-slot ShortSkill[16]
    public const int AffectOffset = 4572;
    public const int AffectStride = 256; // MAX_AFFECT(32) * sizeof(STRUCT_AFFECT)(8), per slot
    public const int MobExtraOffset = 5600;
    public const int MobExtraStride = 552; // sizeof(STRUCT_MOBEXTRA)
    public const int DonateOffset = 7808;
    private const int ReceivedItemOffset = 7864; // bool
    private const int QuestDiariaOffset = 7872; // STRUCT_QUEST, file layout (56 bytes) differs from the wire's 52-byte format
    private const int BlockPassOffset = 7928; // ACCOUNTNAME_LENGTH
    private const int IsBlockedOffset = 7944; // bool
    public const int RequiredFileLength = IsBlockedOffset + 1; // 7945

    public const int MobNameOffset = 0;
    public const int MobClanOffset = 16;
    public const int MobMerchantOffset = 17; // STRUCT_MOB.Merchant, used by _MSG_Quest NPC dispatch
    public const int MobGuildOffset = 18;
    public const int MobClassOffset = 20; // STRUCT_MOB.Class, confirmed by the x86 layout probe
    public const int MobRsvOffset = 22; // STRUCT_MOB.Rsv, confirmed by the x86 layout probe
    public const int MobCoinOffset = 28;
    public const int MobExperienceOffset = 32;
    public const int MobSavedPositionXOffset = 40; // STRUCT_MOB.SPX
    public const int MobSavedPositionYOffset = 42; // STRUCT_MOB.SPY
    public const int MobSaveManaOffset = 795; // STRUCT_MOB.SaveMana, confirmed by the x86 layout probe
    public const int MobGuildLevelOffset = 800; // STRUCT_MOB.GuildLevel, confirmed by the x86 layout probe
    public const int MobBaseScoreOffset = 44; // STRUCT_MOB.BaseScore, immediately before CurrentScore
    public const int MobCurrentScoreOffset = 92;
    public const int MobCurrentMpOffset = 120; // CurrentScore.Mp, confirmed by the x86 layout probe
    public const int MobEquipmentOffset = 140;
    public const int MobCarryOffset = MobEquipmentOffset + (LegacyCharacterSelection.EquipmentCount * LegacyItem.SizeInBytes);
    public const int MobCarryCount = 64; // MAX_CARRY
    public const int LegacyKillMarkCarrySlot = 63; // KILL_MARK from Basedef.h
    public const int MobLearnedSkillOffset = 780; // STRUCT_MOB.LearnedSkill, confirmed by the x86 layout probe
    public const int MobMagicOffset = 784; // STRUCT_MOB.Magic, confirmed by the x86 layout probe
    public const int MobScoreBonusOffset = 788; // STRUCT_MOB.ScoreBonus, confirmed by STRUCT_MOB layout
    public const int MobSpecialBonusOffset = 790; // STRUCT_MOB.SpecialBonus, confirmed by STRUCT_MOB layout
    public const int MobSkillBonusOffset = 792; // STRUCT_MOB.SkillBonus, confirmed by the x86 layout probe
    public const int MobSkillBarOffset = 796; // offsetof(STRUCT_MOB, SkillBar), confirmed by x86 layout probe
    public const int MobRegenMpOffset = 804; // STRUCT_MOB.RegenMP, confirmed by the x86 layout probe
    public const int MobResistOffset = 806; // STRUCT_MOB.Resist[4], confirmed by the x86 layout probe

    public const int MobExtraClassMasterOffset = 0;
    public const int MobExtraCitizenOffset = 2; // STRUCT_MOBEXTRA.Citizen in the 7.59 account layout
    public const int MobExtraMortalFaceOffset = 14;
    public const int MobExtraPilulaOrcOffset = 19; // QuestInfo.Mortal.PilulaOrc, confirmed by the x86 layout probe
    public const int MobExtraHoldOffset = 476; // offsetof(STRUCT_MOBEXTRA, Hold), confirmed with the x86 probe against Basedef.h
    public const int MobExtraDayLogOffset = 480;
    public const int MobExtraDayLogExpOffset = MobExtraDayLogOffset;
    public const int MobExtraDayLogYearDayOffset = MobExtraDayLogOffset + 8;
    public const int ClassMasterArch = 1; // ARCH in Basedef.h
    public const int ClassMasterMortal = 2; // MORTAL in Basedef.h

    private const int QuestIndexQuestOffset = 0;
    private const int QuestLastTimeQuestOffset = 40; // 64-bit time_t in the file; truncated to the wire's 32-bit field
    private const int QuestMobCount1Offset = 48;
    private const int QuestMobCount2Offset = 50;
    private const int QuestMobCount3Offset = 52;

    public static LegacyAccountSnapshot Read(string accountName, ReadOnlySpan<byte> file)
    {
        if (file.Length < RequiredFileLength) throw new ArgumentException($"Legacy account file requires at least {RequiredFileLength} bytes.", nameof(file));

        var slots = new LegacyCharacterSlot[CharacterCount];
        for (var character = 0; character < CharacterCount; character++)
        {
            var mob = file.Slice(CharactersOffset + (character * CharacterStride), CharacterStride);
            var mobExtra = file.Slice(MobExtraOffset + (character * MobExtraStride), MobExtraStride);
            var name = ReadCString(mob.Slice(MobNameOffset, NameFieldLength));
            var guild = BinaryPrimitives.ReadUInt16LittleEndian(mob[MobGuildOffset..]);
            // Reference759/ServidorSource/Code/DBSrv/CFileDB.cpp: DBGetSelChar copies both saved
            // coordinates into STRUCT_SELCHAR. The older W2PP source in this repository has a stale
            // SPX/SPY typo; it is not the layout used by the 7.59 client/server reference.
            var savedPositionX = BinaryPrimitives.ReadInt16LittleEndian(mob[MobSavedPositionXOffset..]);
            var savedPositionY = BinaryPrimitives.ReadInt16LittleEndian(mob[MobSavedPositionYOffset..]);
            var score = LegacyScore.Read(mob.Slice(MobCurrentScoreOffset, LegacyScore.SizeInBytes));
            var coin = BinaryPrimitives.ReadInt32LittleEndian(mob[MobCoinOffset..]);
            var experience = BinaryPrimitives.ReadInt64LittleEndian(mob[MobExperienceOffset..]);

            var equipment = new LegacyItem[LegacyCharacterSelection.EquipmentCount];
            for (var item = 0; item < equipment.Length; item++)
                equipment[item] = LegacyItem.Read(mob.Slice(MobEquipmentOffset + (item * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));

            // DBGetSelChar also overwrites the helm slot for the five "empty face" item indexes with a
            // face computed from mobExtra, so the client shows the character's face instead of a bare head.
            if (equipment[0].Index is 22 or 23 or 24 or 25 or 32)
            {
                var classMaster = BinaryPrimitives.ReadInt16LittleEndian(mobExtra[MobExtraClassMasterOffset..]);
                var mortalFace = BinaryPrimitives.ReadInt16LittleEndian(mobExtra[MobExtraMortalFaceOffset..]);
                var faceIndex = (short)(classMaster == ClassMasterMortal ? 21 : mortalFace + 7);
                equipment[0] = equipment[0] with { Index = faceIndex };
            }

            // The selection screen uses the third helm effect for the citizen/mantle state. The legacy
            // DBGetSelChar projection writes it for every slot, including an empty/deleted slot.
            equipment[0] = equipment[0] with
            {
                Effect3 = 28,
                Value3 = mobExtra[MobExtraCitizenOffset]
            };

            slots[character] = new LegacyCharacterSlot(savedPositionX, savedPositionY, name, score, equipment, guild, coin, experience);
        }

        var cargo = new LegacyItem[CargoCount];
        for (var index = 0; index < CargoCount; index++)
            cargo[index] = LegacyItem.Read(file.Slice(CargoOffset + (index * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));

        var coinTotal = BinaryPrimitives.ReadInt32LittleEndian(file[AccountCoinOffset..]);
        var welcomeItemReceived = file[ReceivedItemOffset] != 0;

        var quest = file.Slice(QuestDiariaOffset, 56);
        var dailyQuest = new LegacyDailyQuest(
            IndexQuest: BinaryPrimitives.ReadInt16LittleEndian(quest[QuestIndexQuestOffset..]),
            Level: 0, Mob1Id: 0, Mob1Required: 0, Mob2Id: 0, Mob2Required: 0, Mob3Id: 0, Mob3Required: 0,
            ExperienceReward: 0, GoldReward: 0, Reward1: default, Reward2: default,
            LastTimeQuest: unchecked((int)BinaryPrimitives.ReadInt64LittleEndian(quest[QuestLastTimeQuestOffset..])),
            Mob1Count: BinaryPrimitives.ReadInt16LittleEndian(quest[QuestMobCount1Offset..]),
            Mob2Count: BinaryPrimitives.ReadInt16LittleEndian(quest[QuestMobCount2Offset..]),
            Mob3Count: BinaryPrimitives.ReadInt16LittleEndian(quest[QuestMobCount3Offset..]));

        var blockPassword = ReadCString(file.Slice(BlockPassOffset, NameFieldLength));
        var isBlocked = file[IsBlockedOffset] != 0;

        return new LegacyAccountSnapshot(accountName, new LegacyCharacterSelection(slots), cargo, coinTotal, welcomeItemReceived, dailyQuest, blockPassword, isBlocked);
    }

    private static string ReadCString(ReadOnlySpan<byte> source)
    {
        var terminator = source.IndexOf((byte)0);
        return Encoding.ASCII.GetString(terminator < 0 ? source : source[..terminator]);
    }

    /// <summary>
    /// Builds the account-login confirmation from this snapshot. HashKeyTable and Keys stay zero: the
    /// reference DBSrv handler never sets MSG_DBCNFAccountLogin.HashKeyTable/Keys either (it zeroes the
    /// whole message up front and only fills selection/cargo/coin/account/quest/block fields), so an
    /// all-zero table here matches legacy behavior rather than standing in for missing behavior.
    /// </summary>
    public AccountLoginConfirmation ToConfirmation() => new(
        new byte[16],
        WelcomeItemReceived ? 1 : 0,
        Characters,
        Cargo,
        Coin,
        AccountName,
        new byte[12],
        DailyQuest,
        BlockPassword,
        IsBlocked);
}

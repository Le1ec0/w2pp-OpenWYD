// Read-only ABI probe for the WYD character and equipment wire layouts.
// Compile as Win32 with one of PROBE_CLIENT, PROBE_769_DBSRV,
// PROBE_769_TMSRV, or PROBE_W2PP and the matching Basedef.h include path.

#include <Windows.h>
#include <time.h>
#include <cstdint>
#include <cassert>
#include <vector>
#include <mbstring.h>
#include <map>
#include <string>
#include <functional>
#include <tuple>
#include <unordered_map>
#include <array>
#include <ctime>
#include <memory>
#include <fstream>
#include <iostream>
#include <sstream>
#include <WinSock.h>
#include <Rpc.h>

#include "Basedef.h"

#include <cstddef>
#include <cstdio>

#if !defined(PROBE_CLIENT) && !defined(PROBE_769_DBSRV) && !defined(PROBE_769_TMSRV) && !defined(PROBE_W2PP)
#error Define exactly one probe profile.
#endif

#define PRINT_SIZE(type) std::printf("sizeof.%s=%zu\n", #type, sizeof(type))
#define PRINT_OFFSET(type, member) std::printf("offsetof.%s.%s=%zu\n", #type, #member, offsetof(type, member))

int main()
{
#if defined(PROBE_CLIENT)
    std::puts("profile=TMProject2GlobalClient-7.69-client");
#elif defined(PROBE_769_DBSRV)
    std::puts("profile=TMProject2GlobalClient-7.69-DBSrv-common-header");
#elif defined(PROBE_769_TMSRV)
    std::puts("profile=TMProject2GlobalClient-7.69-TMSrv-header");
#elif defined(PROBE_W2PP)
    std::puts("profile=W2PP-TMSrv-shared-header");
#endif

#if defined(MAX_EQUIP)
    std::printf("MAX_EQUIP=%d\n", MAX_EQUIP);
#endif
#if defined(MAX_CARGO)
    std::printf("MAX_CARGO=%d\n", MAX_CARGO);
#endif
#if defined(PROBE_CLIENT)
    std::printf("MAX_CARGO=%d\n", MAX_CARGO);
#endif

    PRINT_SIZE(STRUCT_ITEM);
    PRINT_SIZE(STRUCT_ITEMLIST);
    PRINT_OFFSET(STRUCT_ITEMLIST, Name);
#if defined(PROBE_CLIENT)
    PRINT_OFFSET(STRUCT_ITEMLIST, nPrice);
#else
    PRINT_OFFSET(STRUCT_ITEMLIST, Price);
#endif
    PRINT_OFFSET(STRUCT_ITEMLIST, nUnique);
    PRINT_OFFSET(STRUCT_ITEMLIST, nPos);
#if defined(PROBE_CLIENT)
    PRINT_OFFSET(STRUCT_ITEMLIST, nExtra);
    PRINT_OFFSET(STRUCT_ITEMLIST, nGrade);
#else
    PRINT_OFFSET(STRUCT_ITEMLIST, Extra);
    PRINT_OFFSET(STRUCT_ITEMLIST, Grade);
#endif
    PRINT_SIZE(STRUCT_SCORE);
    PRINT_SIZE(MSG_STANDARD);
    PRINT_SIZE(STRUCT_SELCHAR);
    PRINT_OFFSET(STRUCT_SELCHAR, Equip);
    PRINT_SIZE(STRUCT_MOB);
    PRINT_OFFSET(STRUCT_MOB, Equip);
    PRINT_OFFSET(STRUCT_MOB, Carry);
    PRINT_OFFSET(STRUCT_MOB, LearnedSkill);
#if defined(PROBE_CLIENT)
    PRINT_OFFSET(STRUCT_MOB, HomeTownX);
    PRINT_OFFSET(STRUCT_MOB, HomeTownY);
    PRINT_OFFSET(STRUCT_MOB, BaseScore);
    PRINT_OFFSET(STRUCT_MOB, CurrentScore);
    PRINT_OFFSET(STRUCT_MOB, ScoreBonus);
    PRINT_OFFSET(STRUCT_MOB, SpecialBonus);
    PRINT_OFFSET(STRUCT_MOB, SkillBonus);
    PRINT_OFFSET(STRUCT_MOB, Critical);
    PRINT_OFFSET(STRUCT_MOB, SaveMana);
    PRINT_OFFSET(STRUCT_MOB, ShortSkill);
    PRINT_OFFSET(STRUCT_MOB, GuildLevel);
    PRINT_OFFSET(STRUCT_MOB, Magic);
    PRINT_OFFSET(STRUCT_MOB, RegenHP);
    PRINT_OFFSET(STRUCT_MOB, RegenMP);
    PRINT_OFFSET(STRUCT_MOB, Resist);
    PRINT_OFFSET(STRUCT_MOB, dummy);
    PRINT_OFFSET(STRUCT_MOB, CurrentKill);
    PRINT_OFFSET(STRUCT_MOB, TotalKill);
    PRINT_OFFSET(STRUCT_SCORE, Level);
    PRINT_OFFSET(STRUCT_SCORE, Ac);
    PRINT_OFFSET(STRUCT_SCORE, Damage);
    PRINT_OFFSET(STRUCT_SCORE, Reserved);
    PRINT_OFFSET(STRUCT_SCORE, AttackRun);
    PRINT_OFFSET(STRUCT_SCORE, MaxHp);
    PRINT_OFFSET(STRUCT_SCORE, Str);
    PRINT_OFFSET(STRUCT_SCORE, Special);
    PRINT_SIZE(STRUCT_EXT1);
    PRINT_OFFSET(STRUCT_EXT1, Data);
    PRINT_OFFSET(STRUCT_EXT1, Affect);
    PRINT_SIZE(STRUCT_EXT2);
    PRINT_OFFSET(STRUCT_EXT2, Quest);
    PRINT_OFFSET(STRUCT_EXT2, LastConnectTime);
    PRINT_OFFSET(STRUCT_EXT2, SubClass);
    PRINT_OFFSET(STRUCT_EXT2, ItemPassWord);
    PRINT_OFFSET(STRUCT_EXT2, ItemPos);
    PRINT_OFFSET(STRUCT_EXT2, SendLevItem);
    PRINT_OFFSET(STRUCT_EXT2, AdminGuildItem);
    PRINT_OFFSET(STRUCT_EXT2, Dummy);
#elif defined(PROBE_769_DBSRV)
    PRINT_OFFSET(STRUCT_MOB, SPX);
    PRINT_OFFSET(STRUCT_MOB, SPY);
    PRINT_OFFSET(STRUCT_MOB, BaseScore);
    PRINT_OFFSET(STRUCT_MOB, CurrentScore);
    PRINT_OFFSET(STRUCT_MOB, ScoreBonus);
    PRINT_OFFSET(STRUCT_MOB, SpecialBonus);
    PRINT_OFFSET(STRUCT_MOB, SkillBonus);
    PRINT_OFFSET(STRUCT_MOB, Critical);
    PRINT_OFFSET(STRUCT_MOB, SaveMana);
    PRINT_OFFSET(STRUCT_MOB, SkillBar);
    PRINT_OFFSET(STRUCT_MOB, GuildLevel);
    PRINT_OFFSET(STRUCT_MOB, Magic);
    PRINT_OFFSET(STRUCT_MOB, RegenHP);
    PRINT_OFFSET(STRUCT_MOB, RegenMP);
    PRINT_OFFSET(STRUCT_MOB, Resist);
    PRINT_OFFSET(STRUCT_MOB, dummy);
    PRINT_OFFSET(STRUCT_MOB, CurrentKill);
    PRINT_OFFSET(STRUCT_MOB, TotalKill);
    PRINT_OFFSET(STRUCT_SCORE, Level);
    PRINT_OFFSET(STRUCT_SCORE, Ac);
    PRINT_OFFSET(STRUCT_SCORE, Damage);
    PRINT_OFFSET(STRUCT_SCORE, Merchant);
    PRINT_OFFSET(STRUCT_SCORE, AttackRun);
    PRINT_OFFSET(STRUCT_SCORE, MaxHp);
    PRINT_OFFSET(STRUCT_SCORE, Str);
    PRINT_OFFSET(STRUCT_SCORE, Special);
    PRINT_SIZE(STRUCT_EXT1);
    PRINT_OFFSET(STRUCT_EXT1, Data);
    PRINT_OFFSET(STRUCT_EXT1, Affect);
    PRINT_SIZE(STRUCT_EXT2);
    PRINT_OFFSET(STRUCT_EXT2, Quest);
    PRINT_OFFSET(STRUCT_EXT2, LastConnectTime);
    PRINT_OFFSET(STRUCT_EXT2, SubClass);
    PRINT_OFFSET(STRUCT_EXT2, ItemPassWord);
    PRINT_OFFSET(STRUCT_EXT2, ItemPos);
    PRINT_OFFSET(STRUCT_EXT2, SendLevItem);
    PRINT_OFFSET(STRUCT_EXT2, AdminGuildItem);
    PRINT_OFFSET(STRUCT_EXT2, Dummy);
#else
    PRINT_OFFSET(STRUCT_MOB, SPX);
    PRINT_OFFSET(STRUCT_MOB, SPY);
    PRINT_OFFSET(STRUCT_MOB, BaseScore);
    PRINT_OFFSET(STRUCT_MOB, CurrentScore);
    PRINT_OFFSET(STRUCT_MOB, Magic);
    PRINT_OFFSET(STRUCT_MOB, ScoreBonus);
    PRINT_OFFSET(STRUCT_MOB, SpecialBonus);
    PRINT_OFFSET(STRUCT_MOB, SkillBonus);
    PRINT_OFFSET(STRUCT_MOB, Critical);
    PRINT_OFFSET(STRUCT_MOB, SaveMana);
    PRINT_OFFSET(STRUCT_MOB, SkillBar);
    PRINT_OFFSET(STRUCT_MOB, GuildLevel);
    PRINT_OFFSET(STRUCT_MOB, RegenHP);
    PRINT_OFFSET(STRUCT_MOB, RegenMP);
    PRINT_OFFSET(STRUCT_MOB, Resist);
    PRINT_OFFSET(STRUCT_SCORE, Level);
    PRINT_OFFSET(STRUCT_SCORE, Ac);
    PRINT_OFFSET(STRUCT_SCORE, Damage);
    PRINT_OFFSET(STRUCT_SCORE, Merchant);
    PRINT_OFFSET(STRUCT_SCORE, AttackRun);
    PRINT_OFFSET(STRUCT_SCORE, Direction);
    PRINT_OFFSET(STRUCT_SCORE, ChaosRate);
    PRINT_OFFSET(STRUCT_SCORE, MaxHp);
    PRINT_OFFSET(STRUCT_SCORE, Str);
    PRINT_OFFSET(STRUCT_SCORE, Special);
#endif

    PRINT_SIZE(MSG_CNFCharacterLogin);
#if defined(PROBE_CLIENT) || defined(PROBE_769_DBSRV)
    PRINT_OFFSET(MSG_CNFCharacterLogin, MOB);
    PRINT_OFFSET(MSG_CNFCharacterLogin, ShortSkill);
    PRINT_OFFSET(MSG_CNFCharacterLogin, Ext1);
    PRINT_OFFSET(MSG_CNFCharacterLogin, Ext2);
#else
    PRINT_OFFSET(MSG_CNFCharacterLogin, mob);
    PRINT_OFFSET(MSG_CNFCharacterLogin, ShortSkill);
    PRINT_OFFSET(MSG_CNFCharacterLogin, affect);
    PRINT_OFFSET(MSG_CNFCharacterLogin, mobExtra);
    PRINT_OFFSET(MSG_CNFCharacterLogin, Donate);
#if defined(PROBE_W2PP)
    PRINT_SIZE(MSG_CNFClientCharacterLogin);
    PRINT_OFFSET(MSG_CNFClientCharacterLogin, mob);
    PRINT_OFFSET(MSG_CNFClientCharacterLogin, ShortSkill);
    PRINT_OFFSET(MSG_CNFClientCharacterLogin, Unk2);
#endif
#endif

#if defined(PROBE_CLIENT)
    PRINT_SIZE(MSG_CreateMobTrade);
    PRINT_OFFSET(MSG_CreateMobTrade, Equip);
    PRINT_OFFSET(MSG_CreateMobTrade, Affect);
    PRINT_OFFSET(MSG_CreateMobTrade, Score);
    PRINT_OFFSET(MSG_CreateMobTrade, Equip2);
    PRINT_OFFSET(MSG_CreateMobTrade, Nick);
    PRINT_OFFSET(MSG_CreateMobTrade, Desc);
    PRINT_OFFSET(MSG_CreateMobTrade, Server);
    PRINT_SIZE(MSG_CNFAccountLogin);
    PRINT_OFFSET(MSG_CNFAccountLogin, SecretCode);
    PRINT_OFFSET(MSG_CNFAccountLogin, SelChar);
    PRINT_OFFSET(MSG_CNFAccountLogin, Cargo);
    PRINT_OFFSET(MSG_CNFAccountLogin, Coin);
    PRINT_OFFSET(MSG_CNFAccountLogin, AccountName);
    PRINT_OFFSET(MSG_CNFAccountLogin, SSN1);
    PRINT_SIZE(MSG_CNFNewCharacter);
    PRINT_OFFSET(MSG_CNFNewCharacter, SelChar);
    PRINT_SIZE(MSG_CNFDeleteCharacter);
    PRINT_OFFSET(MSG_CNFDeleteCharacter, SelChar);
    PRINT_OFFSET(MSG_CreateMob, Equip2);
    PRINT_OFFSET(MSG_UpdateEquip, sEquip);
    PRINT_OFFSET(MSG_UpdateEquip, Equip2);
#else
    PRINT_SIZE(MSG_DBCNFAccountLogin);
#if defined(PROBE_769_DBSRV)
    PRINT_OFFSET(MSG_DBCNFAccountLogin, SelChar);
#else
    PRINT_OFFSET(MSG_DBCNFAccountLogin, sel);
#endif
    PRINT_OFFSET(MSG_DBCNFAccountLogin, Cargo);
    PRINT_SIZE(MSG_CNFNewCharacter);
    PRINT_OFFSET(MSG_CNFNewCharacter, sel);
    PRINT_SIZE(MSG_CNFDeleteCharacter);
    PRINT_OFFSET(MSG_CNFDeleteCharacter, sel);
#endif

#if defined(PROBE_769_DBSRV)
    PRINT_SIZE(STRUCT_MOBExtra);
    PRINT_SIZE(STRUCT_ACCOUNTINFO);
    PRINT_OFFSET(STRUCT_ACCOUNTINFO, NumericToken);
    PRINT_SIZE(STRUCT_ACCOUNTFILE);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, Info);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, Char);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, Cargo);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, Coin);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, ShortSkill);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, affect);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, mobExtra);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, Donate);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, TempKey);
    std::printf("STRUCT_ACCOUNTFILE.PostDonateExtensionBytes=%zu\n",
        offsetof(STRUCT_ACCOUNTFILE, TempKey) - (offsetof(STRUCT_ACCOUNTFILE, Donate) + sizeof(((STRUCT_ACCOUNTFILE*)0)->Donate)));
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, ReceivedItem);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, QuestDiaria);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, BlockPass);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, IsBlocked);
#elif defined(PROBE_W2PP)
    PRINT_SIZE(STRUCT_MOBEXTRA);
    PRINT_SIZE(STRUCT_ACCOUNTINFO);
    PRINT_OFFSET(STRUCT_ACCOUNTINFO, NumericToken);
    PRINT_SIZE(STRUCT_ACCOUNTFILE);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, Info);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, Char);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, Cargo);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, Coin);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, ShortSkill);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, affect);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, mobExtra);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, Donate);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, TempKey);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, ReceivedItem);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, QuestDiaria);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, BlockPass);
    PRINT_OFFSET(STRUCT_ACCOUNTFILE, IsBlocked);
#endif

#if defined(PROBE_769_DBSRV)
    PRINT_OFFSET(MSG_CreateMob, Equip2);
    PRINT_OFFSET(MSG_UpdateEquip, Equip);
    PRINT_OFFSET(MSG_UpdateEquip, AnctCode);
#elif !defined(PROBE_CLIENT)
    PRINT_OFFSET(MSG_CreateMob, AnctCode);
    PRINT_OFFSET(MSG_UpdateEquip, Equip);
    PRINT_OFFSET(MSG_UpdateEquip, AnctCode);
#endif

    PRINT_SIZE(MSG_CreateMob);
    PRINT_OFFSET(MSG_CreateMob, Equip);
    PRINT_OFFSET(MSG_CreateMob, Affect);
    PRINT_OFFSET(MSG_CreateMob, Score);
    PRINT_SIZE(MSG_UpdateEquip);

    return 0;
}

#undef PRINT_OFFSET
#undef PRINT_SIZE

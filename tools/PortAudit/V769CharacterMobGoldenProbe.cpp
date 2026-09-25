// Emits a synthetic client 7.69 STRUCT_MOB using its Win32 ABI.
#include <Windows.h>
#include <cstddef>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include "Basedef.h"

static_assert(sizeof(STRUCT_ITEM) == 8);
static_assert(sizeof(STRUCT_SCORE) == 48);
static_assert(sizeof(STRUCT_MOB) == 1040);
static_assert(offsetof(STRUCT_MOB, BaseScore) == 44);
static_assert(offsetof(STRUCT_MOB, CurrentScore) == 92);
static_assert(offsetof(STRUCT_MOB, Equip) == 140);
static_assert(offsetof(STRUCT_MOB, Carry) == 284);
static_assert(offsetof(STRUCT_MOB, LearnedSkill) == 796);
static_assert(offsetof(STRUCT_MOB, CurrentKill) == 1036);
static_assert(offsetof(STRUCT_MOB, TotalKill) == 1038);

static void FillScore(STRUCT_SCORE& score, short level, int ac, int damage, char attackRun,
    int maxHp, int maxMp, int hp, int mp)
{
    score.Level = level;
    score.Ac = ac;
    score.Damage = damage;
    score.Reserved = 0;
    score.AttackRun = attackRun;
    score.MaxHp = maxHp;
    score.MaxMp = maxMp;
    score.Hp = hp;
    score.Mp = mp;
    score.Str = 11;
    score.Int = 12;
    score.Dex = 13;
    score.Con = 14;
    score.Special[0] = 15;
    score.Special[1] = 16;
    score.Special[2] = 17;
    score.Special[3] = 18;
}

int main()
{
    STRUCT_MOB mob;
    std::memset(&mob, 0, sizeof(mob));
    std::memcpy(mob.MobName, "ABCDEFGHIJKL", 12);
    mob.MobName[12] = 75;
    mob.MobName[13] = 7;
    mob.MobName[14] = 0x34;
    mob.MobName[15] = 0x12;
    mob.Clan = 1;
    mob.Merchant = 2;
    mob.Guild = 0x1234;
    mob.Class = 3;
    mob.Rsv = 0x5A;
    mob.Quest = 0x81;
    mob.Coin = 0x10203040;
    mob.Exp = 0x0102030405060708LL;
    mob.HomeTownX = 2100;
    mob.HomeTownY = 2101;
    FillScore(mob.BaseScore, 10, 101, 202, 7, 900, 800, 700, 600);
    FillScore(mob.CurrentScore, 11, 101, 202, 7, 900, 800, 555, 444);

    for (int index = 0; index < 16; ++index)
    {
        mob.Equip[index].sIndex = static_cast<short>(100 + index);
        mob.Equip[index].stEffect[0].cEffect = 1;
        mob.Equip[index].stEffect[0].cValue = static_cast<unsigned char>(index);
        mob.Equip[index].stEffect[1].cEffect = 2;
        mob.Equip[index].stEffect[1].cValue = static_cast<unsigned char>(index + 1);
        mob.Equip[index].stEffect[2].cEffect = 3;
        mob.Equip[index].stEffect[2].cValue = static_cast<unsigned char>(index + 2);
    }

    mob.Carry[63].sIndex = 547;
    mob.Carry[63].stEffect[0].cEffect = 75;
    mob.Carry[63].stEffect[0].cValue = 7;
    mob.Carry[63].stEffect[1].cEffect = 76;
    mob.Carry[63].stEffect[1].cValue = 0x34;
    mob.Carry[63].stEffect[2].cEffect = 77;
    mob.Carry[63].stEffect[2].cValue = 0x12;
    mob.LearnedSkill[0] = 0x89ABCDEF;
    mob.LearnedSkill[1] = 0x12345678;
    mob.ScoreBonus = 123;
    mob.SpecialBonus = 124;
    mob.SkillBonus = 125;
    mob.Critical = 9;
    mob.SaveMana = 8;
    mob.ShortSkill[0] = 1;
    mob.ShortSkill[1] = 2;
    mob.ShortSkill[2] = 3;
    mob.ShortSkill[3] = 4;
    mob.GuildLevel = 7;
    mob.Magic = 6;
    mob.RegenHP = 4;
    mob.RegenMP = 5;
    mob.Resist[0] = 1;
    mob.Resist[1] = 2;
    mob.Resist[2] = 3;
    mob.Resist[3] = 4;
    mob.CurrentKill = 7;
    mob.TotalKill = 0x1234;

    const auto* bytes = reinterpret_cast<const unsigned char*>(&mob);
    for (std::size_t index = 0; index < sizeof(mob); ++index)
        std::printf("%02X", bytes[index]);
    std::putchar('\n');
}

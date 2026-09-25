// Emits deterministic MSG_CreateMob payload bytes from the client 7.69 ABI.
#include <Windows.h>
#include <cstddef>
#include <cstdio>
#include <cstring>
#include "Basedef.h"

static void FillScore(STRUCT_SCORE& score)
{
    score.Level = 321;
    score.Ac = 0x01020304;
    score.Damage = 0x11121314;
    score.Reserved = 0x55;
    score.AttackRun = 0x07;
    score.MaxHp = 0x21222324;
    score.MaxMp = 0x31323334;
    score.Hp = 0x41424344;
    score.Mp = 0x51525354;
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
    static_assert(sizeof(MSG_STANDARD) == 12);
    static_assert(sizeof(STRUCT_SCORE) == 48);
    static_assert(sizeof(MSG_CreateMob) == 236);
    static_assert(offsetof(MSG_CreateMob, PosX) == 12);
    static_assert(offsetof(MSG_CreateMob, MobName) == 18);
    static_assert(offsetof(MSG_CreateMob, Equip) == 34);
    static_assert(offsetof(MSG_CreateMob, Affect) == 70);
    static_assert(offsetof(MSG_CreateMob, Guild) == 134);
    static_assert(offsetof(MSG_CreateMob, Score) == 140);
    static_assert(offsetof(MSG_CreateMob, CreateType) == 188);
    static_assert(offsetof(MSG_CreateMob, Equip2) == 190);
    static_assert(offsetof(MSG_CreateMob, Nick) == 208);
    static_assert(offsetof(MSG_CreateMob, Server) == 234);

    MSG_CreateMob message{};
    message.PosX = -321;
    message.PosY = 654;
    message.MobID = 0x1234;
    std::memcpy(message.MobName, "ABCDEFGHIJKL", 12);
    message.MobName[12] = 0x4B;
    message.MobName[13] = 0x07;
    message.MobName[14] = 0x34;
    message.MobName[15] = 0x12;

    for (int index = 0; index < 18; index++)
    {
        message.Equip[index] = static_cast<unsigned short>(0x1000 + index);
        message.Equip2[index] = static_cast<char>(0x40 + index);
    }
    for (int index = 0; index < 32; index++)
        message.Affect[index] = static_cast<unsigned short>(0x2000 + index);

    message.Guild = 0x3344;
    message.GuildLevel = 0x55;
    FillScore(message.Score);
    message.CreateType = 0x0042;
    std::memcpy(message.Nick, "NICK-7.69", 9);
    message.Server = 0x07;

    const auto* bytes = reinterpret_cast<const unsigned char*>(&message);
    for (std::size_t index = sizeof(MSG_STANDARD); index < sizeof(message); index++)
        std::printf("%02X", static_cast<unsigned int>(bytes[index]));
    std::putchar('\n');
    return 0;
}

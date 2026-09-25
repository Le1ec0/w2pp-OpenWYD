// Emits deterministic MSG_UpdateScore payload bytes from the client 7.69 ABI.
#include <Windows.h>
#include <cstddef>
#include <cstdio>
#include "Basedef.h"

int main()
{
    static_assert(sizeof(MSG_STANDARD) == 12);
    static_assert(sizeof(STRUCT_SCORE) == 48);
    static_assert(sizeof(MSG_UpdateScore) == 152);
    static_assert(offsetof(MSG_UpdateScore, Score) == 12);
    static_assert(offsetof(MSG_UpdateScore, Critical) == 60);
    static_assert(offsetof(MSG_UpdateScore, Affect) == 62);
    static_assert(offsetof(MSG_UpdateScore, Guild) == 126);
    static_assert(offsetof(MSG_UpdateScore, GuildLevel) == 128);
    static_assert(offsetof(MSG_UpdateScore, Resist) == 130);
    static_assert(offsetof(MSG_UpdateScore, ReqHp) == 136);
    static_assert(offsetof(MSG_UpdateScore, ReqMp) == 140);
    static_assert(offsetof(MSG_UpdateScore, Magic) == 144);
    static_assert(offsetof(MSG_UpdateScore, Rsv) == 146);
    static_assert(offsetof(MSG_UpdateScore, LearnedSkill) == 148);

    MSG_UpdateScore message{};
    message.Score.Level = 321;
    message.Score.Ac = 0x11223344;
    message.Score.Damage = 0x55667788;
    message.Score.Reserved = 0x12;
    message.Score.AttackRun = 0x34;
    message.Score.MaxHp = 0x01020304;
    message.Score.MaxMp = 0x11121314;
    message.Score.Hp = 0x21222324;
    message.Score.Mp = 0x31323334;
    message.Score.Str = 101;
    message.Score.Int = 202;
    message.Score.Dex = 303;
    message.Score.Con = 404;
    message.Score.Special[0] = 0x0102;
    message.Score.Special[1] = 0x0304;
    message.Score.Special[2] = 0x0506;
    message.Score.Special[3] = 0x0708;
    message.Critical = 0x45;
    message.SaveMana = 0x56;
    for (int index = 0; index < 32; index++)
        message.Affect[index] = static_cast<unsigned short>(0x1000 + index);
    message.Guild = 0x2345;
    message.GuildLevel = 0x67;
    message.Resist[0] = 0x78;
    message.Resist[1] = static_cast<char>(0x89);
    message.Resist[2] = static_cast<char>(0x9A);
    message.Resist[3] = static_cast<char>(0xAB);
    message.ReqHp = 0x12345678;
    message.ReqMp = 0x23456789;
    message.Magic = 0x3456;
    message.Rsv = 0x789A;
    message.LearnedSkill = 0xBC;

    const auto* bytes = reinterpret_cast<const unsigned char*>(&message);
    for (std::size_t index = sizeof(MSG_STANDARD); index < sizeof(message); index++)
        std::printf("%02X", static_cast<unsigned int>(bytes[index]));
    std::putchar('\n');
    return 0;
}

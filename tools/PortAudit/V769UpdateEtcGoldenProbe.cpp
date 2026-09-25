// Emits deterministic MSG_UpdateEtc payload bytes from the client 7.69 ABI.
#include <Windows.h>
#include <cstddef>
#include <cstdio>
#include "Basedef.h"

int main()
{
    static_assert(sizeof(MSG_STANDARD) == 12);
    static_assert(sizeof(MSG_UpdateEtc) == 48);
    static_assert(offsetof(MSG_UpdateEtc, FakeExp) == 12);
    static_assert(offsetof(MSG_UpdateEtc, Exp) == 16);
    static_assert(offsetof(MSG_UpdateEtc, LearnedSkill) == 24);
    static_assert(offsetof(MSG_UpdateEtc, ScoreBonus) == 32);
    static_assert(offsetof(MSG_UpdateEtc, SpecialBonus) == 34);
    static_assert(offsetof(MSG_UpdateEtc, SkillBonus) == 36);
    static_assert(offsetof(MSG_UpdateEtc, Coin) == 40);

    MSG_UpdateEtc message{};
    message.FakeExp = 0x11223344;
    message.Exp = 0x0102030405060708LL;
    message.LearnedSkill[0] = 0x11121314;
    message.LearnedSkill[1] = 0x21222324;
    message.ScoreBonus = 0x3132;
    message.SpecialBonus = 0x4142;
    message.SkillBonus = 0x5152;
    message.Coin = 0x61626364;

    const auto* bytes = reinterpret_cast<const unsigned char*>(&message);
    for (std::size_t index = sizeof(MSG_STANDARD); index < sizeof(message); index++)
        std::printf("%02X", static_cast<unsigned int>(bytes[index]));
    std::putchar('\n');
    return 0;
}

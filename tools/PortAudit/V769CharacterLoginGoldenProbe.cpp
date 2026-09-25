// Emits a synthetic payload from the client 7.69 MSG_CNFCharacterLogin ABI.
#include <Windows.h>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include "Basedef.h"

static void FillPattern(void* target, std::size_t length, unsigned char seed, unsigned char step)
{
    auto* bytes = static_cast<unsigned char*>(target);
    for (std::size_t index = 0; index < length; ++index)
        bytes[index] = static_cast<unsigned char>(seed + index * step);
}

int main()
{
    static_assert(sizeof(MSG_STANDARD) == 12);
    static_assert(sizeof(STRUCT_MOB) == 1040);
    static_assert(sizeof(STRUCT_EXT1) == 288);
    static_assert(sizeof(STRUCT_EXT2) == 360);
    static_assert(sizeof(MSG_CNFCharacterLogin) == 1728);
    static_assert(offsetof(MSG_CNFCharacterLogin, MOB) == 16);
    static_assert(offsetof(MSG_CNFCharacterLogin, ShortSkill) == 1062);
    static_assert(offsetof(MSG_CNFCharacterLogin, Ext1) == 1080);
    static_assert(offsetof(MSG_CNFCharacterLogin, Ext2) == 1368);

    MSG_CNFCharacterLogin message;
    std::memset(&message, 0, sizeof(message));
    message.PosX = 0x1234;
    message.PosY = 0x5678;
    FillPattern(&message.MOB, sizeof(message.MOB), 0x10, 7);
    message.Slot = 0x9ABC;
    message.ClientID = 0xDEF0;
    message.Weather = 0x1357;
    FillPattern(message.ShortSkill, sizeof(message.ShortSkill), 0x20, 3);
    FillPattern(&message.Ext1, sizeof(message.Ext1), 0x40, 5);
    FillPattern(&message.Ext2, sizeof(message.Ext2), 0x60, 11);

    const auto* bytes = reinterpret_cast<const unsigned char*>(&message);
    for (std::size_t index = sizeof(MSG_STANDARD); index < sizeof(message); ++index)
        std::printf("%02X", bytes[index]);
    std::putchar('\n');
}

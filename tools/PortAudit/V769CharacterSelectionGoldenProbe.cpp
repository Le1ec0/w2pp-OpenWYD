// Emits deterministic STRUCT_SELCHAR bytes from the local 7.69 client header.
// Compile as Win32 against Projects/TMProject/Basedef.h.
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

static_assert(sizeof(STRUCT_SCORE) == 48);
static_assert(sizeof(STRUCT_ITEM) == 8);
static_assert(sizeof(STRUCT_SELCHAR) == 904);
static_assert(offsetof(STRUCT_SELCHAR, Score) == 80);
static_assert(offsetof(STRUCT_SELCHAR, Equip) == 272);
static_assert(offsetof(STRUCT_SELCHAR, Guild) == 848);
static_assert(offsetof(STRUCT_SELCHAR, Coin) == 856);
static_assert(offsetof(STRUCT_SELCHAR, Exp) == 872);
static_assert(sizeof(MSG_CNFNewCharacter) == 920);
static_assert(offsetof(MSG_CNFNewCharacter, SelChar) == 16);
static_assert(sizeof(MSG_CNFDeleteCharacter) == 920);
static_assert(offsetof(MSG_CNFDeleteCharacter, SelChar) == 16);

int main()
{
    STRUCT_SELCHAR selection{};
    for (int character = 0; character < 4; character++)
    {
        selection.HomeTownX[character] = static_cast<unsigned short>(0x1100 + character);
        selection.HomeTownY[character] = static_cast<unsigned short>(0x2200 + character);
        std::snprintf(selection.MobName[character], sizeof(selection.MobName[character]), "CHAR%d", character);

        auto& score = selection.Score[character];
        score.Level = static_cast<short>(100 + character);
        score.Ac = 0x01020304 + character;
        score.Damage = 0x11121314 + character;
        score.Reserved = static_cast<char>(0x30 + character);
        score.AttackRun = static_cast<char>(0x40 + character);
        score.MaxHp = 1000 + character;
        score.MaxMp = 2000 + character;
        score.Hp = 3000 + character;
        score.Mp = 4000 + character;
        score.Str = static_cast<short>(10 + character);
        score.Int = static_cast<short>(20 + character);
        score.Dex = static_cast<short>(30 + character);
        score.Con = static_cast<short>(40 + character);
        for (int special = 0; special < 4; special++)
            score.Special[special] = static_cast<unsigned short>(50 + (character * 4) + special);

        for (int item = 0; item < 18; item++)
        {
            auto& equipment = selection.Equip[character][item];
            equipment.sIndex = static_cast<short>(1000 + (character * 32) + item);
            equipment.stEffect[0].cEffect = static_cast<unsigned char>(0x10 + character);
            equipment.stEffect[0].cValue = static_cast<unsigned char>(0x20 + item);
            equipment.stEffect[1].cEffect = static_cast<unsigned char>(0x30 + character);
            equipment.stEffect[1].cValue = static_cast<unsigned char>(0x40 + item);
            equipment.stEffect[2].cEffect = static_cast<unsigned char>(0x50 + character);
            equipment.stEffect[2].cValue = static_cast<unsigned char>(0x60 + item);
        }

        selection.Guild[character] = static_cast<unsigned short>(0x3300 + character);
        selection.Coin[character] = 0x01010100 + character;
        selection.Exp[character] = 0x0102030405060700LL + character;
    }

    const auto* bytes = reinterpret_cast<const unsigned char*>(&selection);
    for (size_t offset = 0; offset < sizeof(selection); offset++)
        std::printf("%02X", static_cast<unsigned int>(bytes[offset]));
    std::putchar('\n');
    return 0;
}

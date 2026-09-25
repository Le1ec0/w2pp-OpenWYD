// Emits a deterministic MSG_CNFAccountLogin payload from the local 7.69 client header.
// Compile as Win32 against Projects/TMProject/Basedef.h; all values are synthetic.
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
#include <cstring>

static_assert(MAX_CARGO == 120);
static_assert(sizeof(MSG_STANDARD) == 12);
static_assert(sizeof(STRUCT_ITEM) == 8);
static_assert(sizeof(STRUCT_SELCHAR) == 904);
static_assert(sizeof(MSG_CNFAccountLogin) == 1928);
static_assert(offsetof(MSG_CNFAccountLogin, SecretCode) == 12);
static_assert(offsetof(MSG_CNFAccountLogin, SelChar) == 32);
static_assert(offsetof(MSG_CNFAccountLogin, Cargo) == 936);
static_assert(offsetof(MSG_CNFAccountLogin, Coin) == 1896);
static_assert(offsetof(MSG_CNFAccountLogin, AccountName) == 1900);
static_assert(offsetof(MSG_CNFAccountLogin, SSN1) == 1916);
static_assert(offsetof(MSG_CNFAccountLogin, SSN2) == 1920);

int main()
{
    MSG_CNFAccountLogin message{};
    for (int index = 0; index < 16; index++)
        message.SecretCode[index] = static_cast<char>(0xA0 + index);

    for (int index = 0; index < MAX_CARGO; index++)
    {
        auto& item = message.Cargo[index];
        item.sIndex = static_cast<short>(3000 + index);
        item.stEffect[0].cEffect = static_cast<unsigned char>(0x10 + (index % 16));
        item.stEffect[0].cValue = static_cast<unsigned char>(0x20 + (index % 32));
        item.stEffect[1].cEffect = static_cast<unsigned char>(0x30 + (index % 16));
        item.stEffect[1].cValue = static_cast<unsigned char>(0x40 + (index % 32));
        item.stEffect[2].cEffect = static_cast<unsigned char>(0x50 + (index % 16));
        item.stEffect[2].cValue = static_cast<unsigned char>(0x60 + (index % 32));
    }

    message.Coin = 0x11223344;
    std::memcpy(message.AccountName, "SYNTHETIC", 10);
    message.SSN1 = static_cast<int>(0x55667788u);
    message.SSN2 = static_cast<int>(0x12345678u);

    const auto* payload = reinterpret_cast<const unsigned char*>(&message) + sizeof(MSG_STANDARD);
    constexpr size_t payloadSize = sizeof(MSG_CNFAccountLogin) - sizeof(MSG_STANDARD);
    for (size_t offset = 0; offset < payloadSize; offset++)
        std::printf("%02X", static_cast<unsigned int>(payload[offset]));
    std::putchar('\n');
    return 0;
}

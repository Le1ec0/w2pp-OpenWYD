// Emits deterministic MSG_UpdateEquip payload bytes from the client 7.69 ABI.
#include <Windows.h>
#include <cstddef>
#include <cstdio>
#include <cstring>
#include "Basedef.h"

int main()
{
    static_assert(sizeof(MSG_STANDARD) == 12);
    static_assert(sizeof(MSG_UpdateEquip) == 68);
    static_assert(offsetof(MSG_UpdateEquip, sEquip) == 12);
    static_assert(offsetof(MSG_UpdateEquip, Equip2) == 48);

    MSG_UpdateEquip message{};
    for (int index = 0; index < 18; index++)
    {
        message.sEquip[index] = static_cast<unsigned short>(0x1000 + index);
        message.Equip2[index] = static_cast<char>(0x40 + index);
    }

    const auto* bytes = reinterpret_cast<const unsigned char*>(&message);
    for (std::size_t index = sizeof(MSG_STANDARD); index < sizeof(message); index++)
        std::printf("%02X", static_cast<unsigned int>(bytes[index]));
    std::putchar('\n');
    return 0;
}

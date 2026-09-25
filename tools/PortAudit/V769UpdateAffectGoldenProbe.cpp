// Emits deterministic MSG_UpdateAffect payload bytes from the client 7.69 ABI.
#include <Windows.h>
#include <cstddef>
#include <cstdio>
#include "Basedef.h"

int main()
{
    static_assert(sizeof(MSG_STANDARD) == 12);
    static_assert(sizeof(STRUCT_AFFECT) == 8);
    static_assert(sizeof(MSG_UpdateAffect) == 268);
    static_assert(offsetof(MSG_UpdateAffect, Affect) == 12);

    MSG_UpdateAffect message{};
    for (int index = 0; index < 32; index++)
    {
        message.Affect[index].Type = static_cast<char>(0x10 + index);
        message.Affect[index].Level = static_cast<char>(-20 + index);
        message.Affect[index].Value = static_cast<short>(0x2000 + index);
        message.Affect[index].Time = 0x01020300 + index;
    }

    const auto* bytes = reinterpret_cast<const unsigned char*>(&message);
    for (std::size_t index = sizeof(MSG_STANDARD); index < sizeof(message); index++)
        std::printf("%02X", static_cast<unsigned int>(bytes[index]));
    std::putchar('\n');
    return 0;
}

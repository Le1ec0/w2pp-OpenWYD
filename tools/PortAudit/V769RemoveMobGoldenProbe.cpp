// Emits deterministic MSG_RemoveMob payload bytes from the client 7.69 ABI.
#include <Windows.h>
#include <cstddef>
#include <cstdio>
#include "Basedef.h"

int main()
{
    static_assert(sizeof(MSG_STANDARD) == 12);
    static_assert(sizeof(MSG_RemoveMob) == 16);
    static_assert(offsetof(MSG_RemoveMob, RemoveType) == 12);

    MSG_RemoveMob message{};
    message.RemoveType = 0x10203040;

    const auto* bytes = reinterpret_cast<const unsigned char*>(&message);
    for (std::size_t index = sizeof(MSG_STANDARD); index < sizeof(message); index++)
        std::printf("%02X", static_cast<unsigned int>(bytes[index]));
    std::putchar('\n');
    return 0;
}

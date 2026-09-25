using System.Buffers.Binary;
using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Projects the one proven W2PP character-extra value used by the 7.69 login wire.
/// This is deliberately not a byte-copy adapter for STRUCT_MOBEXTRA -> STRUCT_EXT2.
/// </summary>
public static class W2ppCharacterLoginExtensionsV1Adapter
{
    public static CharacterLoginExtensionsV769 Adapt(ReadOnlySpan<byte> mobExtra)
    {
        if (mobExtra.Length != LegacyAccountSnapshot.MobExtraStride)
            throw new ArgumentException($"W2PP STRUCT_MOBEXTRA requires exactly {LegacyAccountSnapshot.MobExtraStride} bytes.", nameof(mobExtra));

        // W2PP TMSrv sends Extra.Hold as the held-experience value; 7.69 names the
        // corresponding login/update field FakeExp. Reject values not representable
        // by the target's signed int instead of silently wrapping them.
        var heldExperience = BinaryPrimitives.ReadUInt32LittleEndian(mobExtra[LegacyAccountSnapshot.MobExtraHoldOffset..]);
        var fakeExp = checked((int)heldExperience);
        return new CharacterLoginExtensionsV769(fakeExp);
    }
}

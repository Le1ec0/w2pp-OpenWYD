using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Composes the currently mapped W2PP character-login data into the client 7.69
/// confirmation. This is a wire projection only: login-time score/equipment/guild
/// derivation from TMSrv and the separate Affect wire are not performed here.
/// </summary>
public static class W2ppCharacterLoginV1Adapter
{
    public static CharacterLoginConfirmationV769 Adapt(
        LegacyCharacterLoginData data,
        short spawnX,
        short spawnY,
        ushort slot,
        ushort clientId,
        ushort weather,
        ushort sceneId = CharacterLoginConfirmationV769.DefaultSceneId)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (slot >= LegacyCharacterSelection.CharacterCount)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "W2PP character slot must be in the range 0..3.");

        var mob = W2ppCharacterMobV1Adapter.Adapt(data.Mob, spawnX, spawnY, data.ClientEquipment).ToBytes();
        var extensions = W2ppCharacterLoginExtensionsV1Adapter.Adapt(data.MobExtra);

        // W2PP sends its 16-byte account short-skill block after STRUCT_MOB.
        // Affect has a distinct follow-up wire contract; it is intentionally not
        // reinterpreted as the differently shaped EXT1.Affect array.
        return new CharacterLoginConfirmationV769(
            spawnX,
            spawnY,
            mob,
            slot,
            clientId,
            weather,
            data.ShortSkill,
            extensions,
            sceneId);
    }
}

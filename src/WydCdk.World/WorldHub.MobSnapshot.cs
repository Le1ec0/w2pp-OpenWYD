namespace WydCdk.World;

public sealed partial class WorldHub
{
    /// <summary>Captures one live MOB actor from the player, NPC, or summon table.</summary>
    public bool TryGetMobSnapshot(int connectionId, out LegacyWorldMobSnapshot? snapshot)
    {
        lock (gate)
        {
            if (participants.TryGetValue(connectionId, out var participant) &&
                participant.Mob.Length >= LegacyAccountSnapshot.CharacterStride)
            {
                snapshot = new LegacyWorldMobSnapshot(
                    connectionId,
                    LegacyWorldMobKind.Player,
                    participant.Mob.ToArray(),
                    participant.PositionX,
                    participant.PositionY);
                return true;
            }

            if (npcs.TryGetValue(connectionId, out var npc) &&
                npc.MobSnapshot.Length >= LegacyAccountSnapshot.CharacterStride)
            {
                snapshot = new LegacyWorldMobSnapshot(
                    connectionId,
                    LegacyWorldMobKind.Npc,
                    npc.MobSnapshot.ToArray(),
                    npc.PositionX,
                    npc.PositionY);
                return true;
            }

            if (summons.TryGetValue(connectionId, out var summon) &&
                summon.MobSnapshot.Length >= LegacyAccountSnapshot.CharacterStride)
            {
                snapshot = new LegacyWorldMobSnapshot(
                    connectionId,
                    LegacyWorldMobKind.Summon,
                    summon.MobSnapshot.ToArray(),
                    summon.PositionX,
                    summon.PositionY);
                return true;
            }

            snapshot = null;
            return false;
        }
    }
}

public enum LegacyWorldMobKind
{
    Player,
    Npc,
    Summon,
}

public sealed record LegacyWorldMobSnapshot(
    int ConnectionId,
    LegacyWorldMobKind Kind,
    byte[] MobSnapshot,
    short PositionX,
    short PositionY);

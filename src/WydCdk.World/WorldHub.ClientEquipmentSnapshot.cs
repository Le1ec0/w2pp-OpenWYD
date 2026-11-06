using WydCdk.Protocol;

namespace WydCdk.World;

public sealed partial class WorldHub
{
    /// <summary>Returns the independent 7.69 equipment state without normalizing it from MOB.</summary>
    public bool TryGetClientEquipmentSnapshot(int connectionId, out IReadOnlyList<LegacyItem>? equipment)
    {
        lock (gate)
        {
            equipment = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return false;

            equipment = Array.AsReadOnly(participant.ClientEquipment.ToArray());
            return true;
        }
    }
}

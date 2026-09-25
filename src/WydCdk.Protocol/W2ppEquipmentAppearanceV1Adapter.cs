namespace WydCdk.Protocol;

/// <summary>
/// Projects the already-computed W2PP or 7.69 visual arrays into the 18-slot
/// 7.69 client DTO. Visual-code calculation remains a separate legacy rule;
/// this adapter only makes the slot-count decision explicit.
/// </summary>
public static class W2ppEquipmentAppearanceV1Adapter
{
    public const int SourceEquipmentCount = 16;

    public static EquipmentAppearanceV769 Adapt(
        IReadOnlyList<ushort> visualEquipment,
        IReadOnlyList<byte> ancientCodes)
    {
        ArgumentNullException.ThrowIfNull(visualEquipment);
        ArgumentNullException.ThrowIfNull(ancientCodes);
        if (visualEquipment.Count is not (SourceEquipmentCount or EquipmentAppearanceV769.EquipmentCount))
            throw new ArgumentException($"The appearance source requires {SourceEquipmentCount} or {EquipmentAppearanceV769.EquipmentCount} visual equipment entries.", nameof(visualEquipment));
        if (ancientCodes.Count != visualEquipment.Count)
            throw new ArgumentException("Visual equipment and ancient-code arrays must have the same slot count.", nameof(ancientCodes));

        var targetEquipment = new ushort[EquipmentAppearanceV769.EquipmentCount];
        var targetAncientCodes = new byte[EquipmentAppearanceV769.EquipmentCount];
        for (var index = 0; index < visualEquipment.Count; index++)
        {
            targetEquipment[index] = visualEquipment[index];
            targetAncientCodes[index] = ancientCodes[index];
        }

        return new EquipmentAppearanceV769(targetEquipment, targetAncientCodes);
    }
}

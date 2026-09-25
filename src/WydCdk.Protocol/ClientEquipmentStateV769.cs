namespace WydCdk.Protocol;

/// <summary>
/// Versioned storage contract for the client 7.69 equipment array.
/// It is deliberately separate from the 816-byte W2PP STRUCT_MOB, which has
/// only sixteen equipment entries in this port's legacy persistence boundary.
/// </summary>
public sealed class ClientEquipmentStateV769
{
    public const int EquipmentCount = CharacterMobV769.EquipmentCount;
    public const int ItemSizeInBytes = LegacyItem.SizeInBytes;
    public const int SizeInBytes = EquipmentCount * ItemSizeInBytes;

    private readonly LegacyItem[] equipment;

    public ClientEquipmentStateV769(IReadOnlyList<LegacyItem> equipment)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        if (equipment.Count != EquipmentCount)
            throw new ArgumentException($"The client 7.69 equipment state requires exactly {EquipmentCount} items.", nameof(equipment));

        this.equipment = equipment.ToArray();
        Equipment = Array.AsReadOnly(this.equipment);
    }

    public IReadOnlyList<LegacyItem> Equipment { get; }

    public byte[] ToBytes()
    {
        var bytes = new byte[SizeInBytes];
        for (var index = 0; index < equipment.Length; index++)
            equipment[index].Write(bytes.AsSpan(index * ItemSizeInBytes, ItemSizeInBytes));
        return bytes;
    }

    public static ClientEquipmentStateV769 FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != SizeInBytes)
            throw new ArgumentException($"The client 7.69 equipment state requires exactly {SizeInBytes} bytes.", nameof(bytes));

        var equipment = new LegacyItem[EquipmentCount];
        for (var index = 0; index < equipment.Length; index++)
            equipment[index] = LegacyItem.Read(bytes.Slice(index * ItemSizeInBytes, ItemSizeInBytes));
        return new ClientEquipmentStateV769(equipment);
    }
}

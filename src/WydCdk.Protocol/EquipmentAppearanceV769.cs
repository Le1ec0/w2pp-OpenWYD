using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Client 7.69 equipment-appearance update (<c>MSG_UpdateEquip</c>).
/// This is the 18-slot client wire contract, not the 16-slot W2PP sender.
/// </summary>
public sealed class EquipmentAppearanceV769
{
    public const ushort MessageType = 0x036B;
    public const int EquipmentCount = 18;
    public const int EquipmentOffset = 0;
    public const int AncientCodeOffset = EquipmentCount * sizeof(ushort);
    public const int PayloadSize = AncientCodeOffset + EquipmentCount + 2; // x86 tail padding
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    private readonly ushort[] visualEquipment;
    private readonly byte[] ancientCodes;

    public EquipmentAppearanceV769(IReadOnlyList<ushort> visualEquipment, IReadOnlyList<byte> ancientCodes)
    {
        ArgumentNullException.ThrowIfNull(visualEquipment);
        ArgumentNullException.ThrowIfNull(ancientCodes);
        if (visualEquipment.Count != EquipmentCount)
            throw new ArgumentException($"The 7.69 appearance wire requires {EquipmentCount} visual equipment entries.", nameof(visualEquipment));
        if (ancientCodes.Count != EquipmentCount)
            throw new ArgumentException($"The 7.69 appearance wire requires {EquipmentCount} ancient-code entries.", nameof(ancientCodes));

        this.visualEquipment = visualEquipment.ToArray();
        this.ancientCodes = ancientCodes.ToArray();
        VisualEquipment = Array.AsReadOnly(this.visualEquipment);
        AncientCodes = Array.AsReadOnly(this.ancientCodes);
    }

    public IReadOnlyList<ushort> VisualEquipment { get; }
    public IReadOnlyList<byte> AncientCodes { get; }

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        for (var index = 0; index < EquipmentCount; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(
                payload.AsSpan(EquipmentOffset + index * sizeof(ushort)), visualEquipment[index]);
            payload[AncientCodeOffset + index] = ancientCodes[index];
        }

        // The native x86 struct is 68 bytes: two bytes remain after the 18-byte
        // secondary-code array. Zero-initialization preserves that ABI padding.
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, id, clientTick, ToPayload(), keywordIndex);
    }
}

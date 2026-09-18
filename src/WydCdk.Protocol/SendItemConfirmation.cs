using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Inventory-slot update matching legacy <c>MSG_SendItem</c>.</summary>
public sealed class SendItemConfirmation
{
    public const ushort MessageType = 0x0182; // 130 | FLAG_GAME2CLIENT
    public const int PacketSize = 24;

    public SendItemConfirmation(short inventoryType, short slot, LegacyItem item)
    {
        InventoryType = inventoryType;
        Slot = slot;
        Item = item;
    }

    public short InventoryType { get; }
    public short Slot { get; }
    public LegacyItem Item { get; }

    public byte[] ToPayload()
    {
        var payload = new byte[PacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteInt16LittleEndian(payload, InventoryType);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(2), Slot);
        Item.Write(payload.AsSpan(4));
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, id, clientTick, ToPayload(), keywordIndex);
    }
}

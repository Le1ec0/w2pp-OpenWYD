using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Legacy <c>MSG_RemoveMob</c> used when an NPC summon leaves the world.</summary>
public sealed class RemoveMobConfirmation
{
    public const ushort MessageType = 0x0165; // 101 | FLAG_GAME2CLIENT
    public const int PacketSize = 16;

    private readonly int removeType;
    private readonly ushort mobId;

    public RemoveMobConfirmation(ushort mobId, int removeType = 3)
    {
        this.mobId = mobId;
        this.removeType = removeType;
    }

    public byte[] ToPayload()
    {
        var payload = new byte[PacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteInt32LittleEndian(payload, removeType);
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, mobId, clientTick, ToPayload(), keywordIndex);
    }
}

using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Server-authored MSG_AttackOne emitted by an autonomous NPC/summon.</summary>
public sealed class NpcAttackConfirmation
{
    public const ushort MessageType = AttackRequest.OneTargetMessageType;
    public const ushort SceneId = 30000;
    public const int PacketSize = AttackRequest.OneTargetPacketSize;

    private readonly byte[] payload;

    public NpcAttackConfirmation(
        short positionX,
        short positionY,
        short targetX,
        short targetY,
        ushort attackerId,
        ushort targetId,
        byte motion,
        short skillIndex,
        int damage)
    {
        payload = new byte[PacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4), -1); // CurrentMp
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(22), positionX);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(24), positionY);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(26), targetX);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(28), targetY);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), attackerId);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(32), 52428); // legacy attack progress
        payload[34] = motion;
        payload[35] = 1; // SkillParm
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(40), -1); // CurrentHp
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), skillIndex);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(46), -1); // ReqMp
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(48), targetId);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(52), damage);
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, SceneId, clientTick, payload, keywordIndex);
    }
}

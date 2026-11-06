using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Server-authored 7.69 MSG_AttackOne for a supported generated-mob attack.</summary>
public sealed class LegacyMobAttackConfirmation
{
    public const ushort MessageType = AttackRequest.OneTargetMessageType;
    public const ushort SceneId = 30000;
    public const int PacketSize = AttackRequest.OneTargetPacketSize;

    private readonly byte[] payload = new byte[PacketSize - PacketHeader.SizeInBytes];

    public LegacyMobAttackConfirmation(
        short positionX,
        short positionY,
        short targetX,
        short targetY,
        int attackerId,
        int targetId,
        int damage,
        short skillIndex = -1,
        byte skillParm = 1,
        byte motion = 4)
    {
        if (positionX is < 0 or >= 4096) throw new ArgumentOutOfRangeException(nameof(positionX));
        if (positionY is < 0 or >= 4096) throw new ArgumentOutOfRangeException(nameof(positionY));
        if (targetX is < 0 or >= 4096) throw new ArgumentOutOfRangeException(nameof(targetX));
        if (targetY is < 0 or >= 4096) throw new ArgumentOutOfRangeException(nameof(targetY));
        if (attackerId is < LegacyNpcMinimumId or >= LegacyMobAiMaximumId)
            throw new ArgumentOutOfRangeException(nameof(attackerId));
        if (targetId is <= 0 or >= LegacyMobAiMaximumId)
            throw new ArgumentOutOfRangeException(nameof(targetId));
        if (damage <= 0)
            throw new ArgumentOutOfRangeException(nameof(damage), damage,
                "The 7.69 normal NPC attack frame is multicast only for positive damage; miss markers are suppressed.");

        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4), -1); // CurrentMp
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(22), positionX);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(24), positionY);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(26), targetX);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(28), targetY);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), checked((ushort)attackerId));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(32), 52428); // 7.69 Progress
        payload[34] = motion;
        payload[35] = skillParm;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(40), -1); // CurrentHp
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), skillIndex);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(46), -1); // ReqMp
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(48), targetId); // STRUCT_DAM.TargetID
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(52), damage); // STRUCT_DAM.Damage
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, SceneId, clientTick, payload, keywordIndex);
    }

    private const int LegacyNpcMinimumId = 1000;
    private const int LegacyMobAiMaximumId = 25000;
}

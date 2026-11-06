using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Server-relayed attack packet with authoritative resource fields filled in.
/// The legacy 7.69 handler sets ReqMp by narrowing its final authoritative CurrentMp to a signed short.
/// Damage slots start cleared and are populated only by WorldHub combat resolution.
/// </summary>
public sealed class AttackConfirmation
{
    private const short SyntheticDivineBlessingHealSkillId = 29;
    public const ushort SceneId = 30000;
    public const int FakeExpOffset = 0;
    public const int CurrentHpOffset = 4;
    public const int CurrentExpOffset = 12;
    public const int CurrentMpOffset = 40;
    public const int RequestedMpOffset = 46;

    private readonly ushort messageType;
    private readonly byte[] payload;

    private AttackConfirmation(ushort messageType, ReadOnlySpan<byte> payload)
    {
        this.messageType = messageType;
        this.payload = payload.ToArray();
    }

    public static bool TryCreate(DecodedFrame input, ReadOnlySpan<byte> mob, out AttackConfirmation? confirmation)
    {
        confirmation = null;
        if (!input.IsChecksumValid ||
            !AttackRequest.IsSupportedPacketSize(input.Header.Type, input.Header.Size) ||
            input.Payload.Length != input.Header.Size - PacketHeader.SizeInBytes ||
            mob.Length < 816)
            return false;

        var result = new AttackConfirmation(input.Header.Type, input.Payload.Span);
        BinaryPrimitives.WriteInt32LittleEndian(result.payload.AsSpan(CurrentHpOffset), BinaryPrimitives.ReadInt32LittleEndian(mob[(92 + 24)..]));
        BinaryPrimitives.WriteInt64LittleEndian(result.payload.AsSpan(CurrentExpOffset), BinaryPrimitives.ReadInt64LittleEndian(mob[32..]));
        var currentMp = BinaryPrimitives.ReadInt32LittleEndian(mob[120..]);
        BinaryPrimitives.WriteInt32LittleEndian(result.payload.AsSpan(CurrentMpOffset), currentMp);
        BinaryPrimitives.WriteInt16LittleEndian(result.payload.AsSpan(RequestedMpOffset), unchecked((short)currentMp));
        result.payload[36] = 0; // DoubleCritical is server-computed, never copied from attack intent.
        for (var offset = 48; offset + 8 <= result.payload.Length && offset < 48 + (13 * 8); offset += 8)
            result.payload.AsSpan(offset, 8).Clear();
        var damageEnd = AttackRequest.DamageArrayOffset + (AttackRequest.GetTargetCount(input.Header.Type) * AttackRequest.DamageSlotSize);
        if (damageEnd < result.payload.Length)
            result.payload.AsSpan(damageEnd).Clear(); // Do not relay client bytes from ABI tail padding.
        confirmation = result;
        return true;
    }

    /// <summary>Creates the server-generated 7.69 Divine Blessing child response as one self-target MSG_AttackOne.</summary>
    public static bool TryCreateSyntheticOneTarget(
        ReadOnlySpan<byte> mob,
        ushort actorId,
        short targetX,
        short targetY,
        ushort wireTargetId,
        int wireDamage,
        out AttackConfirmation? confirmation) =>
        TryCreateSyntheticOneTarget(
            mob,
            actorId,
            targetX,
            targetY,
            wireTargetId,
            wireDamage,
            SyntheticDivineBlessingHealSkillId,
            motion: 255,
            out confirmation);

    /// <summary>Creates a server-generated 7.69 one-target MSG_AttackOne with its selected skill and motion.</summary>
    public static bool TryCreateSyntheticOneTarget(
        ReadOnlySpan<byte> mob,
        ushort actorId,
        short targetX,
        short targetY,
        ushort wireTargetId,
        int wireDamage,
        short skillIndex,
        byte motion,
        out AttackConfirmation? confirmation)
    {
        confirmation = null;
        if (mob.Length < 816)
            return false;

        var result = new AttackConfirmation(
            AttackRequest.OneTargetMessageType,
            new byte[AttackRequest.OneTargetPacketSize - PacketHeader.SizeInBytes]);
        BinaryPrimitives.WriteInt32LittleEndian(result.payload.AsSpan(FakeExpOffset), 0);
        BinaryPrimitives.WriteInt32LittleEndian(result.payload.AsSpan(CurrentHpOffset), BinaryPrimitives.ReadInt32LittleEndian(mob[(92 + 24)..]));
        BinaryPrimitives.WriteInt64LittleEndian(result.payload.AsSpan(CurrentExpOffset), BinaryPrimitives.ReadInt64LittleEndian(mob[32..]));
        var currentMp = BinaryPrimitives.ReadInt32LittleEndian(mob[120..]);
        BinaryPrimitives.WriteInt32LittleEndian(result.payload.AsSpan(CurrentMpOffset), currentMp);
        BinaryPrimitives.WriteInt16LittleEndian(result.payload.AsSpan(RequestedMpOffset), unchecked((short)currentMp));
        BinaryPrimitives.WriteUInt16LittleEndian(result.payload.AsSpan(26), unchecked((ushort)targetX));
        BinaryPrimitives.WriteUInt16LittleEndian(result.payload.AsSpan(28), unchecked((ushort)targetY));
        BinaryPrimitives.WriteUInt16LittleEndian(result.payload.AsSpan(30), actorId);
        result.payload[34] = motion;
        BinaryPrimitives.WriteInt16LittleEndian(result.payload.AsSpan(44), skillIndex);
        BinaryPrimitives.WriteUInt16LittleEndian(result.payload.AsSpan(AttackRequest.DamageArrayOffset), wireTargetId);
        BinaryPrimitives.WriteInt32LittleEndian(result.payload.AsSpan(AttackRequest.DamageArrayOffset + 4), wireDamage);
        confirmation = result;
        return true;
    }

    /// <summary>Creates the server-generated area Attack used by player Type 22/23 timers.</summary>
    public static bool TryCreateSyntheticArea(
        ReadOnlySpan<byte> mob,
        ushort actorId,
        short positionX,
        short positionY,
        short targetX,
        short targetY,
        short skillIndex,
        byte motion,
        IReadOnlyList<(ushort TargetId, int Damage)> damageSlots,
        out AttackConfirmation? confirmation)
    {
        ArgumentNullException.ThrowIfNull(damageSlots);
        confirmation = null;
        if (mob.Length < 816 || damageSlots.Count > AttackRequest.GetTargetCount(AttackRequest.AreaMessageType))
            return false;

        var result = new AttackConfirmation(
            AttackRequest.AreaMessageType,
            new byte[AttackRequest.AreaPacketSize - PacketHeader.SizeInBytes]);
        BinaryPrimitives.WriteUInt16LittleEndian(result.payload.AsSpan(22), unchecked((ushort)positionX));
        BinaryPrimitives.WriteUInt16LittleEndian(result.payload.AsSpan(24), unchecked((ushort)positionY));
        BinaryPrimitives.WriteUInt16LittleEndian(result.payload.AsSpan(26), unchecked((ushort)targetX));
        BinaryPrimitives.WriteUInt16LittleEndian(result.payload.AsSpan(28), unchecked((ushort)targetY));
        BinaryPrimitives.WriteUInt16LittleEndian(result.payload.AsSpan(30), actorId);
        result.payload[AttackRequest.MotionOffset] = motion;
        BinaryPrimitives.WriteInt16LittleEndian(result.payload.AsSpan(44), skillIndex);
        BinaryPrimitives.WriteInt32LittleEndian(result.payload.AsSpan(CurrentHpOffset), BinaryPrimitives.ReadInt32LittleEndian(mob[(92 + 24)..]));
        BinaryPrimitives.WriteInt64LittleEndian(result.payload.AsSpan(CurrentExpOffset), BinaryPrimitives.ReadInt64LittleEndian(mob[32..]));
        var currentMp = BinaryPrimitives.ReadInt32LittleEndian(mob[120..]);
        BinaryPrimitives.WriteInt32LittleEndian(result.payload.AsSpan(CurrentMpOffset), currentMp);
        BinaryPrimitives.WriteInt16LittleEndian(result.payload.AsSpan(RequestedMpOffset), unchecked((short)currentMp));

        for (var index = 0; index < damageSlots.Count; index++)
        {
            var slot = damageSlots[index];
            var offset = AttackRequest.DamageArrayOffset + (index * AttackRequest.DamageSlotSize);
            BinaryPrimitives.WriteUInt16LittleEndian(result.payload.AsSpan(offset), slot.TargetId);
            BinaryPrimitives.WriteInt32LittleEndian(result.payload.AsSpan(offset + 4), slot.Damage);
        }

        confirmation = result;
        return true;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(messageType, SceneId, clientTick, payload, keywordIndex);
    }

    public bool TrySetDamageSlot(int index, ushort targetId, int damage)
    {
        var offset = AttackRequest.DamageArrayOffset + (index * AttackRequest.DamageSlotSize);
        if (index < 0 || index >= 13 || offset + AttackRequest.DamageSlotSize > payload.Length)
            return false;

        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset), targetId);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(offset + 4), damage);
        return true;
    }

    public bool TrySetDoubleCritical(byte value)
    {
        if (payload.Length <= 36)
            return false;

        payload[36] = value;
        return true;
    }

    public bool TrySetProgress(ushort value)
    {
        if (payload.Length < AttackRequest.ProgressOffset + sizeof(ushort))
            return false;

        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(AttackRequest.ProgressOffset), value);
        return true;
    }

    public bool TrySetMotion(byte value)
    {
        if (payload.Length <= AttackRequest.MotionOffset)
            return false;

        payload[AttackRequest.MotionOffset] = value;
        return true;
    }

    public bool TrySetFakeExp(int value)
    {
        if (payload.Length < FakeExpOffset + sizeof(int))
            return false;

        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(FakeExpOffset), value);
        return true;
    }

}

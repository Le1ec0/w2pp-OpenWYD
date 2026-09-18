using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Server-relayed attack packet with authoritative resource fields filled in.
/// Damage slots start cleared and are populated only by WorldHub combat resolution.
/// </summary>
public sealed class AttackConfirmation
{
    public const ushort SceneId = 30000;
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

    public static bool TryCreate(DecodedFrame input, ReadOnlySpan<byte> mob, int requestedMp, out AttackConfirmation? confirmation)
    {
        confirmation = null;
        if (!input.IsChecksumValid || !IsAttackType(input.Header.Type) || !IsExpectedSize(input.Header.Type, input.Header.Size) || mob.Length < 816)
            return false;

        var result = new AttackConfirmation(input.Header.Type, input.Payload.Span);
        BinaryPrimitives.WriteInt32LittleEndian(result.payload.AsSpan(CurrentHpOffset), BinaryPrimitives.ReadInt32LittleEndian(mob[(92 + 24)..]));
        BinaryPrimitives.WriteInt64LittleEndian(result.payload.AsSpan(CurrentExpOffset), BinaryPrimitives.ReadInt64LittleEndian(mob[32..]));
        BinaryPrimitives.WriteInt32LittleEndian(result.payload.AsSpan(CurrentMpOffset), BinaryPrimitives.ReadInt32LittleEndian(mob[120..]));
        BinaryPrimitives.WriteInt16LittleEndian(result.payload.AsSpan(RequestedMpOffset), checked((short)Math.Clamp(requestedMp, short.MinValue, short.MaxValue)));
        for (var offset = 48; offset + 8 <= result.payload.Length && offset < 48 + (13 * 8); offset += 8)
            result.payload.AsSpan(offset, 8).Clear();
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

    private static bool IsAttackType(ushort type) => type is AttackRequest.AreaMessageType or AttackRequest.OneTargetMessageType or AttackRequest.TwoTargetMessageType;

    private static bool IsExpectedSize(ushort type, int size) => type switch
    {
        AttackRequest.AreaMessageType => size == AttackRequest.AreaPacketSize,
        AttackRequest.OneTargetMessageType => size == AttackRequest.OneTargetPacketSize,
        AttackRequest.TwoTargetMessageType => size == AttackRequest.TwoTargetPacketSize,
        _ => false,
    };
}

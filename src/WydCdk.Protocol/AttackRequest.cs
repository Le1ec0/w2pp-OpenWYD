using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Client-declared attack intent from the three legacy attack packet sizes. Damage-related fields are intentionally excluded.</summary>
public sealed record AttackRequest(ushort MessageType, ushort PositionX, ushort PositionY, ushort TargetX, ushort TargetY, ushort AttackerId, ushort Progress, byte Motion, byte SkillParameter, byte DoubleCritical, byte LocalFlag, short Reserved, short SkillIndex, short RequestedMp)
{
    public const ushort AreaMessageType = 0x0367; // 103 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const ushort OneTargetMessageType = 0x039D; // 157 | both directions
    public const ushort TwoTargetMessageType = 0x039E; // 158 | both directions
    public const int AreaPacketSize = 168;
    public const int OneTargetPacketSize = 68;
    public const int OneTargetClientPaddedPacketSize = 72;
    public const int TwoTargetMinimumCompletePacketSize = 76;
    public const int TwoTargetPacketSize = 80;
    public const int DamageArrayOffset = 48;
    public const int DamageSlotSize = 8;
    public const int ProgressOffset = 32;
    public const int MotionOffset = 34;

    /// <summary>The received wire size, including any x86 client tail padding.</summary>
    public int WireSize { get; init; }

    /// <summary>True only for a two-slot packet whose second STRUCT_DAM is complete but whose struct tail is short.</summary>
    public bool IsShortTwoTargetPacket =>
        MessageType == TwoTargetMessageType &&
        WireSize >= TwoTargetMinimumCompletePacketSize &&
        WireSize < TwoTargetPacketSize;

    public static int GetTargetCount(ushort messageType) => messageType switch
    {
        AreaMessageType => 13,
        OneTargetMessageType => 1,
        TwoTargetMessageType => 2,
        _ => 0,
    };

    public static bool TryParse(DecodedFrame frame, out AttackRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid ||
            !IsSupportedPacketSize(frame.Header.Type, frame.Header.Size) ||
            frame.Payload.Length != frame.Header.Size - PacketHeader.SizeInBytes)
            return false;

        // Confirmed with the x86 legacy-layout probe: PosX=34, TargetX=38, AttackerID=42,
        // Motion=46, SkillIndex=56. Payload offsets subtract the 12-byte MSG_STANDARD header.
        var payload = frame.Payload.Span;
        request = new AttackRequest(
            frame.Header.Type,
            BinaryPrimitives.ReadUInt16LittleEndian(payload[22..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[24..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[26..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[28..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[30..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[ProgressOffset..]),
            payload[MotionOffset], payload[35], payload[36], payload[37],
            BinaryPrimitives.ReadInt16LittleEndian(payload[38..]),
            BinaryPrimitives.ReadInt16LittleEndian(payload[44..]),
            BinaryPrimitives.ReadInt16LittleEndian(payload[46..]))
        {
            WireSize = frame.Header.Size,
        };
        return true;
    }

    public static bool IsSupportedPacketSize(ushort messageType, int size) => messageType switch
    {
        AreaMessageType => size == AreaPacketSize,
        // TMProject's x86 client emits sizeof(MSG_AttackOne)=72 because its header is not packed;
        // the actual fields occupy the server's 68-byte MSG_AttackOne and the final four bytes are padding.
        OneTargetMessageType => size is OneTargetPacketSize or OneTargetClientPaddedPacketSize,
        // A short two-target frame is safe to inspect only when both 8-byte STRUCT_DAM slots are present.
        TwoTargetMessageType => size >= TwoTargetMinimumCompletePacketSize && size <= TwoTargetPacketSize,
        _ => false,
    };

    public static bool TryReadDamageSlot(DecodedFrame frame, int index, out ushort targetId, out int declaredDamage)
    {
        targetId = 0;
        declaredDamage = 0;
        var targetCount = GetTargetCount(frame.Header.Type);
        if (!frame.IsChecksumValid || index < 0 || index >= targetCount || frame.Payload.Length < DamageArrayOffset + ((index + 1) * DamageSlotSize))
            return false;

        var slot = frame.Payload.Span.Slice(DamageArrayOffset + (index * DamageSlotSize), DamageSlotSize);
        targetId = BinaryPrimitives.ReadUInt16LittleEndian(slot);
        declaredDamage = BinaryPrimitives.ReadInt32LittleEndian(slot[4..]);
        return true;
    }
}

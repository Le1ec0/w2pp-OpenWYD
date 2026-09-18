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
    public const int TwoTargetPacketSize = 80;
    public const int DamageArrayOffset = 48;
    public const int DamageSlotSize = 8;

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
        var expectedSize = frame.Header.Type switch
        {
            AreaMessageType => AreaPacketSize,
            OneTargetMessageType => OneTargetPacketSize,
            TwoTargetMessageType => TwoTargetPacketSize,
            _ => 0,
        };
        if (!frame.IsChecksumValid || expectedSize == 0 || frame.Header.Size != expectedSize || frame.Payload.Length != expectedSize - PacketHeader.SizeInBytes)
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
            BinaryPrimitives.ReadUInt16LittleEndian(payload[32..]),
            payload[34], payload[35], payload[36], payload[37],
            BinaryPrimitives.ReadInt16LittleEndian(payload[38..]),
            BinaryPrimitives.ReadInt16LittleEndian(payload[44..]),
            BinaryPrimitives.ReadInt16LittleEndian(payload[46..]));
        return true;
    }

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

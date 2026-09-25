using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Parsed 7.69 <c>MSG_SwapItem</c> request (server opcode alias: <c>_MSG_TradingItem</c>).</summary>
public sealed record TradingItemRequest(
    byte SourcePlace,
    byte SourceSlot,
    byte DestinationPlace,
    byte DestinationSlot,
    ushort TargetId,
    ushort OpaquePadding)
{
    public const ushort MessageType = 0x0376; // 118 | FLAG_CLIENT2GAME | FLAG_GAME2CLIENT
    // The native struct has two ABI tail-padding bytes after TargetID. Call sites
    // send sizeof(MSG_SwapItem) == 20 (12-byte header + 8-byte payload).
    public const int PayloadSize = (sizeof(byte) * 4) + sizeof(ushort) + sizeof(ushort);
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public static bool TryParse(DecodedFrame frame, out TradingItemRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != PayloadSize)
            return false;

        var payload = frame.Payload.Span;
        request = new TradingItemRequest(
            payload[0],
            payload[1],
            payload[2],
            payload[3],
            BinaryPrimitives.ReadUInt16LittleEndian(payload[4..]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[6..]));
        return true;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort headerId) =>
        new TradingItemConfirmation(SourcePlace, SourceSlot, DestinationPlace, DestinationSlot, TargetId, OpaquePadding)
            .ToFrame(codec, clientTick, keywordIndex, headerId);
}

/// <summary>7.69 <c>MSG_SwapItem</c> echo sent after an accepted swap.</summary>
public sealed class TradingItemConfirmation(
    byte sourcePlace,
    byte sourceSlot,
    byte destinationPlace,
    byte destinationSlot,
    ushort targetId,
    ushort opaquePadding)
{
    public const ushort MessageType = TradingItemRequest.MessageType;
    public const int PayloadSize = TradingItemRequest.PayloadSize;
    public const int PacketSize = TradingItemRequest.PacketSize;

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort headerId)
    {
        ArgumentNullException.ThrowIfNull(codec);
        var payload = new byte[PayloadSize];
        payload[0] = sourcePlace;
        payload[1] = sourceSlot;
        payload[2] = destinationPlace;
        payload[3] = destinationSlot;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4), targetId);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(6), opaquePadding);
        return codec.Encode(MessageType, headerId, clientTick, payload, keywordIndex);
    }
}

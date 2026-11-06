using WydCdk.Protocol;
using System.Buffers.Binary;

internal static class RepurchaseItemsWireTests
{
    public static void Run()
    {
        ClientPacketUsesTheSevenSixtyNineLayout();
        ParserRejectsWrongSizeAndChecksum();
        ConstructorRequiresTenEntries();
    }

    private static void ClientPacketUsesTheSevenSixtyNineLayout()
    {
        var codec = LegacyFrameCodec.CreateDefault();
        var entries = Enumerable.Range(0, RepurchaseItemsMessage.EntryCount)
            .Select(index => new RepurchaseItemEntry(
                0x11223344 + index,
                new LegacyItem((short)(0x1234 + index), 0x21, 0x22, 0x23, 0x24, 0x25, 0x26),
                0x55667788 + index))
            .ToArray();
        var message = new RepurchaseItemsMessage(0x10203040, entries);

        var payload = message.ToPayload();
        Check(payload.Length == RepurchaseItemsMessage.PayloadSize && RepurchaseItemsMessage.PacketSize == 176,
            "The client 0x03E8 packet did not use its 176-byte native layout.");
        Check(BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(RepurchaseItemsMessage.TargetOffset)) == 0x10203040,
            "The client target field was not written at payload offset zero.");

        var firstEntry = payload.AsSpan(RepurchaseItemsMessage.EntriesOffset, RepurchaseItemEntry.SizeInBytes);
        Check(BinaryPrimitives.ReadInt32LittleEndian(firstEntry) == 0x11223344,
            "The client Order field was not written before STRUCT_ITEM.");
        Check(firstEntry[4..12].SequenceEqual(new byte[] { 0x34, 0x12, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26 }),
            "The client STRUCT_ITEM bytes did not preserve the eight-byte item ABI.");
        Check(BinaryPrimitives.ReadInt32LittleEndian(firstEntry[12..]) == 0x55667788,
            "The client Price field was not written after STRUCT_ITEM.");

        var frame = codec.Decode(message.ToFrame(codec, 0xAABBCCDD, 17, 0x1234));
        Check(frame.IsChecksumValid && frame.Header.Type == RepurchaseItemsMessage.MessageType &&
              frame.Header.Id == 0x1234 && frame.Header.ClientTick == 0xAABBCCDD &&
              frame.Header.Size == RepurchaseItemsMessage.PacketSize,
            "The repurchase frame header did not preserve the legacy metadata.");
        Check(RepurchaseItemsMessage.TryParse(frame, out var parsed) && parsed is not null &&
              parsed.Target == message.Target && parsed.Entries.SequenceEqual(entries),
            "The client 0x03E8 message did not round-trip through its parser.");
    }

    private static void ParserRejectsWrongSizeAndChecksum()
    {
        var codec = LegacyFrameCodec.CreateDefault();
        var wrongSizeFrame = codec.Decode(codec.Encode(
            RepurchaseItemsMessage.MessageType, 1, 2,
            new byte[RepurchaseItemsMessage.PayloadSize - 1], 3));
        Check(!RepurchaseItemsMessage.TryParse(wrongSizeFrame, out _),
            "The repurchase parser accepted a frame with the wrong legacy size.");

        var message = new RepurchaseItemsMessage(0, Enumerable.Repeat(default(RepurchaseItemEntry), RepurchaseItemsMessage.EntryCount).ToArray());
        var corruptedFrameBytes = message.ToFrame(codec, 2, 3, 1);
        corruptedFrameBytes[3] ^= 0x01;
        var corruptedFrame = codec.Decode(corruptedFrameBytes);
        Check(!RepurchaseItemsMessage.TryParse(corruptedFrame, out _),
            "The repurchase parser accepted an invalid frame checksum.");
    }

    private static void ConstructorRequiresTenEntries()
    {
        try
        {
            _ = new RepurchaseItemsMessage(0, new RepurchaseItemEntry[RepurchaseItemsMessage.EntryCount - 1]);
        }
        catch (ArgumentException)
        {
            return;
        }

        throw new InvalidOperationException("The repurchase message accepted an array with fewer than ten entries.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

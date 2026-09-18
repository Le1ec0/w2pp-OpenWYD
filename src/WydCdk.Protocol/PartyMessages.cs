using System.Buffers.Binary;
using System.Text;

namespace WydCdk.Protocol;

/// <summary>Legacy party-invite packet used in both directions.</summary>
public sealed record PartyInviteRequest(
    byte Class,
    byte PartyPosition,
    short Level,
    short MaxHp,
    short Hp,
    short PartyId,
    string MobName,
    int TargetConnectionId,
    short Target)
{
    public const ushort MessageType = 0x037F; // 127 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int PacketSize = PacketHeader.SizeInBytes + 32;
    private const int NameLength = 16;

    public static bool TryParse(DecodedFrame frame, out PartyInviteRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != 32)
            return false;

        var payload = frame.Payload.Span;
        request = new(
            payload[0],
            payload[1],
            BinaryPrimitives.ReadInt16LittleEndian(payload[2..]),
            BinaryPrimitives.ReadInt16LittleEndian(payload[4..]),
            BinaryPrimitives.ReadInt16LittleEndian(payload[6..]),
            BinaryPrimitives.ReadInt16LittleEndian(payload[8..]),
            ReadFixedString(payload.Slice(10, NameLength)),
            BinaryPrimitives.ReadInt32LittleEndian(payload[26..]),
            BinaryPrimitives.ReadInt16LittleEndian(payload[30..]));
        return true;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id = 0)
    {
        ArgumentNullException.ThrowIfNull(codec);
        Span<byte> payload = stackalloc byte[32];
        payload[0] = Class;
        payload[1] = PartyPosition;
        BinaryPrimitives.WriteInt16LittleEndian(payload[2..], Level);
        BinaryPrimitives.WriteInt16LittleEndian(payload[4..], MaxHp);
        BinaryPrimitives.WriteInt16LittleEndian(payload[6..], Hp);
        BinaryPrimitives.WriteInt16LittleEndian(payload[8..], PartyId);
        WriteFixedString(payload.Slice(10, NameLength), MobName);
        BinaryPrimitives.WriteInt32LittleEndian(payload[26..], TargetConnectionId);
        BinaryPrimitives.WriteInt16LittleEndian(payload[30..], Target);
        return codec.Encode(MessageType, id, clientTick, payload, keywordIndex);
    }

    internal static string ReadFixedString(ReadOnlySpan<byte> source)
    {
        var length = source.IndexOf((byte)0);
        if (length < 0) length = source.Length;
        return Encoding.ASCII.GetString(source[..length]);
    }

    internal static void WriteFixedString(Span<byte> destination, string value)
    {
        destination.Clear();
        var bytes = Encoding.ASCII.GetBytes(value ?? string.Empty);
        bytes.AsSpan(0, Math.Min(bytes.Length, destination.Length - 1)).CopyTo(destination);
    }
}

/// <summary>Legacy party member announcement sent after a party is formed.</summary>
public sealed record PartyAddConfirmation(short LeaderConnectionId, short Level, short MaxHp, short Hp, short PartyId, string MobName, short Target)
{
    public const ushort MessageType = 0x037D; // 125 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int PacketSize = PacketHeader.SizeInBytes + 28;

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id = 30000)
    {
        ArgumentNullException.ThrowIfNull(codec);
        Span<byte> payload = stackalloc byte[28];
        BinaryPrimitives.WriteInt16LittleEndian(payload, LeaderConnectionId);
        BinaryPrimitives.WriteInt16LittleEndian(payload[2..], Level);
        BinaryPrimitives.WriteInt16LittleEndian(payload[4..], MaxHp);
        BinaryPrimitives.WriteInt16LittleEndian(payload[6..], Hp);
        BinaryPrimitives.WriteInt16LittleEndian(payload[8..], PartyId);
        PartyInviteRequest.WriteFixedString(payload[10..26], MobName);
        BinaryPrimitives.WriteInt16LittleEndian(payload[26..], Target);
        return codec.Encode(MessageType, id, clientTick, payload, keywordIndex);
    }
}

/// <summary>Party leave/kick request, represented by legacy MSG_STANDARDPARM.</summary>
public sealed record PartyRemoveRequest(int TargetConnectionId)
{
    public const ushort MessageType = 0x037E; // 126 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int PacketSize = PacketHeader.SizeInBytes + 4;

    public static bool TryParse(DecodedFrame frame, out PartyRemoveRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != 4)
            return false;
        request = new(BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span));
        return true;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id = 0)
    {
        ArgumentNullException.ThrowIfNull(codec);
        Span<byte> payload = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(payload, TargetConnectionId);
        return codec.Encode(MessageType, id, clientTick, payload, keywordIndex);
    }
}

public sealed record PartyRemoveConfirmation(short LeaderConnection, short Unknown = 0)
{
    public const ushort MessageType = PartyRemoveRequest.MessageType;
    public const int PacketSize = PacketHeader.SizeInBytes + 4;

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id = 30000)
    {
        ArgumentNullException.ThrowIfNull(codec);
        Span<byte> payload = stackalloc byte[4];
        BinaryPrimitives.WriteInt16LittleEndian(payload, LeaderConnection);
        BinaryPrimitives.WriteInt16LittleEndian(payload[2..], Unknown);
        return codec.Encode(MessageType, id, clientTick, payload, keywordIndex);
    }
}

/// <summary>Legacy accept-party request.</summary>
public sealed record PartyAcceptRequest(short LeaderConnectionId, string MobName)
{
    public const ushort MessageType = 0x03AB; // 171 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int PacketSize = PacketHeader.SizeInBytes + 18;

    public static bool TryParse(DecodedFrame frame, out PartyAcceptRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != 18)
            return false;
        request = new(BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span), PartyInviteRequest.ReadFixedString(frame.Payload.Span[2..18]));
        return true;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id = 0)
    {
        ArgumentNullException.ThrowIfNull(codec);
        Span<byte> payload = stackalloc byte[18];
        BinaryPrimitives.WriteInt16LittleEndian(payload, LeaderConnectionId);
        PartyInviteRequest.WriteFixedString(payload[2..18], MobName);
        return codec.Encode(MessageType, id, clientTick, payload, keywordIndex);
    }
}

using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Client request for declaring or accepting a legacy 7.69 guild war.</summary>
public sealed record GuildWarRequest(int GuildId, int EnemyGuildId)
{
    public const ushort MessageType = 0x0E0E; // 14 | FLAG_CLIENT2GAME | FLAG_DB2GAME | FLAG_GAME2DB
    public const int PacketSize = PacketHeader.SizeInBytes + 8;

    public static bool TryParse(DecodedFrame frame, out GuildWarRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != 8)
            return false;

        var guildId = BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span);
        var enemyGuildId = BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[4..]);
        if (guildId is < 1 or > ushort.MaxValue || enemyGuildId is < 1 or > ushort.MaxValue)
            return false;

        request = new(guildId, enemyGuildId);
        return true;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id = 0)
    {
        ArgumentNullException.ThrowIfNull(codec);
        Span<byte> payload = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(payload, GuildId);
        BinaryPrimitives.WriteInt32LittleEndian(payload[4..], EnemyGuildId);
        return codec.Encode(MessageType, id, clientTick, payload, keywordIndex);
    }
}

using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>7.69 <c>MSG_CNFMobKill</c> used to announce a non-revived player death.</summary>
public sealed class LegacyMobDeathConfirmation
{
    public const ushort MessageType = 0x0338; // 56 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const ushort SceneId = 30000;
    public const int PacketSize = PacketHeader.SizeInBytes + 20;

    private readonly byte[] payload = new byte[PacketSize - PacketHeader.SizeInBytes];

    public LegacyMobDeathConfirmation(
        int killedPlayerId,
        int killerMobId,
        int fakeExperience = 0,
        long experience = 0)
    {
        if (killedPlayerId is <= 0 or >= LegacyMaximumPlayerId)
            throw new ArgumentOutOfRangeException(nameof(killedPlayerId));
        if (killerMobId is < LegacyNpcMinimumId or >= LegacyMobAiMaximumId)
            throw new ArgumentOutOfRangeException(nameof(killerMobId));

        // The selected 7.69 client names this int FakeExp; TMSrv's _MSG
        // structure names the same packed offset Hold.
        BinaryPrimitives.WriteInt32LittleEndian(payload, fakeExperience);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4), checked((ushort)killedPlayerId));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(6), checked((ushort)killerMobId));
        // Default MSVC x86 alignment pads Exp from offset 20 to offset 24.
        BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(12), experience);
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, SceneId, clientTick, payload, keywordIndex);
    }

    private const int LegacyNpcMinimumId = 1000;
    private const int LegacyMobAiMaximumId = 25000;
    private const int LegacyMaximumPlayerId = 1000;
}

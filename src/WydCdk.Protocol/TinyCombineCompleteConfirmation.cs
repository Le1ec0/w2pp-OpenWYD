using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>7.69 <c>MSG_CombineComplete</c> scene signal with a standard integer parameter.</summary>
public sealed class TinyCombineCompleteConfirmation
{
    public const ushort MessageType = 0xC0A7; // 167 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const ushort FieldSceneId = 30_000;
    public const int PayloadSize = sizeof(int);

    public byte[] ToFrame(
        LegacyFrameCodec codec,
        uint clientTick,
        byte keywordIndex,
        int result,
        ushort sceneId = FieldSceneId)
    {
        ArgumentNullException.ThrowIfNull(codec);
        if (result is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(result), result, "Tiny combine completion result must be 0, 1, or 2.");

        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(payload, result);
        return codec.Encode(MessageType, sceneId, clientTick, payload, keywordIndex);
    }
}

namespace WydCdk.Protocol;

/// <summary>Encoder for the empty legacy <c>_MSG_DeleteCharacterFail</c> signal.</summary>
public static class DeleteCharacterFailSignal
{
    public const ushort MessageType = 0x011B; // 27 | FLAG_GAME2CLIENT

    public static byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex) =>
        codec.Encode(MessageType, 0, clientTick, ReadOnlySpan<byte>.Empty, keywordIndex);
}

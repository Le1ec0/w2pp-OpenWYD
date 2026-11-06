namespace WydCdk.Protocol;

/// <summary>
/// Encodes the empty-payload <c>SendClientSignal(ESCENE_FIELD, _MSG_CloseShop)</c>
/// emitted by TMProject 7.69 and W2PP when an NPC shop target leaves the seller's view.
/// </summary>
public static class CloseShopSignal
{
    public const ushort MessageType = 150 | 0x0100; // _MSG_CloseShop | FLAG_GAME2CLIENT
    public const ushort SceneId = 30_000; // ESCENE_FIELD

    public static byte[] ToFrame(LegacyFrameCodec codec)
    {
        ArgumentNullException.ThrowIfNull(codec);
        // SendClientSignal zero-initializes MSG_STANDARD, then sets only Type and ID.
        return codec.Encode(MessageType, SceneId, clientTick: 0, ReadOnlySpan<byte>.Empty, keywordIndex: 0);
    }
}

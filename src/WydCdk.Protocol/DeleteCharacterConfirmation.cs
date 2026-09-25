namespace WydCdk.Protocol;

/// <summary>
/// Encoder for <c>MSG_CNFDeleteCharacter</c>: the 7.670 contract is the 12-byte
/// CPSock header followed directly by the 840-byte <see cref="LegacyCharacterSelection"/>.
/// </summary>
public sealed class DeleteCharacterConfirmation(LegacyCharacterSelection selection)
{
    public const ushort MessageType = 0x0112; // 18 | FLAG_GAME2CLIENT
    public const ushort SceneId = 30001; // ESCENE_FIELD + 1
    public const int PayloadSize = LegacyCharacterSelection.SizeInBytes;
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public LegacyCharacterSelection Selection { get; } = selection ?? throw new ArgumentNullException(nameof(selection));

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        Selection.ToBytes().CopyTo(payload);
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex) =>
        codec.Encode(MessageType, SceneId, clientTick, ToPayload(), keywordIndex);
}

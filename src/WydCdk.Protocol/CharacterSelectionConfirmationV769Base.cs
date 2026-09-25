namespace WydCdk.Protocol;

/// <summary>Shared x86 envelope for 7.69 create/delete responses carrying STRUCT_SELCHAR.</summary>
public abstract class CharacterSelectionConfirmationV769Base
{
    public const int SelectionOffsetInMessage = 16;
    public const int AlignmentSize = SelectionOffsetInMessage - PacketHeader.SizeInBytes;
    public const int PayloadSize = AlignmentSize + CharacterSelectionV769.SizeInBytes;
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public ushort WireMessageType { get; }
    public ushort WireSceneId { get; }
    public CharacterSelectionV769 Selection { get; }

    protected CharacterSelectionConfirmationV769Base(CharacterSelectionV769 selection, ushort messageType, ushort sceneId)
    {
        Selection = selection ?? throw new ArgumentNullException(nameof(selection));
        WireMessageType = messageType;
        WireSceneId = sceneId;
    }

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        Selection.ToBytes().CopyTo(payload, AlignmentSize);
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(WireMessageType, WireSceneId, clientTick, ToPayload(), keywordIndex);
    }
}

namespace WydCdk.Protocol;

/// <summary>7.69 DeleteCharacter response using the shared x86 selection envelope.</summary>
public sealed class DeleteCharacterConfirmationV769(CharacterSelectionV769 selection)
    : CharacterSelectionConfirmationV769Base(selection, MessageType, SceneId)
{
    public const ushort MessageType = 0x0112;
    public const ushort SceneId = 30001;
}

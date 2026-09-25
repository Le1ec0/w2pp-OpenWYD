namespace WydCdk.Protocol;

/// <summary>7.69 NewCharacter response using the shared x86 selection envelope.</summary>
public sealed class NewCharacterConfirmationV769(CharacterSelectionV769 selection)
    : CharacterSelectionConfirmationV769Base(selection, MessageType, SceneId)
{
    public const ushort MessageType = 0x0110;
    public const ushort SceneId = 30001;
}

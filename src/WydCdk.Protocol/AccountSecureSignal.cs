namespace WydCdk.Protocol;

/// <summary>
/// Encoders for the two empty-payload MSG_STANDARD signals TMSrv sends in reply to <see cref="AccountSecureRequest"/>
/// (ProcessDBMessage.cpp:572-583): <see cref="Success"/> reuses <c>_MSG_AccountSecure</c>'s own type as the
/// acknowledgement (the reference server never defines a separate "confirmation" type for this message), and
/// <see cref="Fail"/> uses the distinct <c>_MSG_AccountSecureFail</c>. Both use <c>ESCENE_FIELD</c> (30000) as
/// the frame id, the same signal shape as <see cref="NewCharacterFailSignal"/> elsewhere in this port.
/// </summary>
public static class AccountSecureSignal
{
    public const ushort SuccessType = AccountSecureRequest.MessageType; // 0x0FDE
    public const ushort FailType = 0x0FDF; // 223 | FLAG_DB2GAME | FLAG_GAME2DB | FLAG_CLIENT2GAME | FLAG_GAME2CLIENT
    public const ushort SceneId = 30000; // ESCENE_FIELD

    public static byte[] Success(LegacyFrameCodec codec, uint clientTick, byte keywordIndex) => Encode(codec, SuccessType, clientTick, keywordIndex);

    public static byte[] Fail(LegacyFrameCodec codec, uint clientTick, byte keywordIndex) => Encode(codec, FailType, clientTick, keywordIndex);

    private static byte[] Encode(LegacyFrameCodec codec, ushort type, uint clientTick, byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(type, SceneId, clientTick, ReadOnlySpan<byte>.Empty, keywordIndex);
    }
}

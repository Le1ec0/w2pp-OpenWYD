using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Encoder for the full legacy W2PP <c>MSG_CNFCharacterLogin</c> shape (2648 bytes), not the
/// actual W2PP TMSrv-to-client relay. W2PP's <c>ProcessDBMessage.cpp</c> copies this database response into
/// <c>MSG_CNFClientCharacterLogin</c> and sends only that 1832-byte prefix. This C# type retains the full
/// internal fields and is also not the 7.69 client contract (<see cref="CharacterLoginConfirmationV769"/>).
/// W2PP DBSrv leaves PosX/PosY/ClientID/Weather unset in its intermediate response; its TMSrv fills those before
/// relay. <c>mob</c>, <c>shortSkill</c>, <c>affect</c>, and <c>mobExtra</c> stay raw bytes rather than typed fields,
/// matching the legacy struct copies. Simplified deliberately (see AGENTS.md): <paramref name="posX"/>/<paramref name="posY"/>
/// must be the live position selected by the world before constructing this packet; the character's saved SPX/SPY
/// remains inside <paramref name="mob"/>. <paramref name="weather"/> is expected to
/// be a fixed placeholder (this port tracks no weather state), and every other "unk" padding region is zero -
/// TMSrv memsets those unconditionally, so zero is exact here, not a stand-in.
/// </summary>
public sealed class CharacterLoginConfirmation(ushort slot, ReadOnlyMemory<byte> mob, ReadOnlyMemory<byte> shortSkill, ReadOnlyMemory<byte> affect, ReadOnlyMemory<byte> mobExtra, int donate, short posX, short posY, ushort clientId, ushort weather)
{
    public const ushort MessageType = 0x0114; // 20 | FLAG_GAME2CLIENT
    // ProcessDBMessage.cpp:826-829: sm.ID = ESCENE_FIELD, or ESCENE_FIELD + 1 only under NewbieEventServer == 1
    // (a rare timed-event flag this port does not track); ESCENE_FIELD is not client-visible position data, it is
    // the scene/state identifier the client dispatches this message by - the same role NewCharacterConfirmation's
    // SceneId (ESCENE_FIELD + 1) plays for character creation.
    public const ushort SceneId = 30000; // ESCENE_FIELD
    public const int PayloadSize = 2636;
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    private const int PosXOffset = 0;
    private const int PosYOffset = 2;
    private const int MobOffset = 4;
    private const int MobSize = 816;
    private const int SlotOffset = 1028;
    private const int ClientIdOffset = 1030;
    private const int WeatherOffset = 1032;
    private const int ShortSkillOffset = 1034;
    private const int ShortSkillSize = 16;
    private const int AffectOffset = 1820;
    private const int AffectSize = 256;
    private const int MobExtraOffset = 2076;
    private const int MobExtraSize = 552;
    private const int DonateOffset = 2628;

    public byte[] ToPayload()
    {
        if (mob.Length != MobSize) throw new ArgumentException("Character login confirmation requires a full 816-byte STRUCT_MOB.", nameof(mob));
        if (shortSkill.Length != ShortSkillSize) throw new ArgumentException("Character login confirmation requires sixteen ShortSkill bytes.", nameof(shortSkill));
        if (affect.Length != AffectSize) throw new ArgumentException("Character login confirmation requires 256 affect bytes.", nameof(affect));
        if (mobExtra.Length != MobExtraSize) throw new ArgumentException("Character login confirmation requires 552 STRUCT_MOBEXTRA bytes.", nameof(mobExtra));

        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(PosXOffset), posX);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(PosYOffset), posY);
        mob.Span.CopyTo(payload.AsSpan(MobOffset, MobSize));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(SlotOffset), slot);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(ClientIdOffset), clientId);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(WeatherOffset), weather);
        shortSkill.Span.CopyTo(payload.AsSpan(ShortSkillOffset, ShortSkillSize));
        affect.Span.CopyTo(payload.AsSpan(AffectOffset, AffectSize));
        mobExtra.Span.CopyTo(payload.AsSpan(MobExtraOffset, MobExtraSize));
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(DonateOffset), donate);
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, SceneId, clientTick, ToPayload(), keywordIndex);
    }
}

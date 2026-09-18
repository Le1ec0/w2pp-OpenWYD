using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Encoder for legacy <c>_MSG_CNFCharacterLogin</c> as the CLIENT actually receives it: DBSrv's
/// <c>MSG_CNFCharacterLogin</c> (CFileDB.cpp:1064-1090) only struct-copies mob/ShortSkill/affect/mobExtra/Donate
/// from the account file and leaves PosX/PosY/ClientID/Weather unset (undefined stack garbage - that handler
/// declares <c>MSG_CNFCharacterLogin sm;</c> without a memset); it is TMSrv's <c>_MSG_DBCNFCharacterLogin</c>
/// handler (ProcessDBMessage.cpp:667-928) that fills those in before forwarding to the client - PosX/PosY from
/// the character's own saved position (<c>STRUCT_MOB.SPX/SPY</c>) or, absent one, a computed guild-zone/city
/// spawn; ClientID from the connection index; Weather from the server's current weather. This type reproduces
/// that TMSrv-forwarded shape, not DBSrv's intermediate one. <c>mob</c>, <c>shortSkill</c>, <c>affect</c>, and
/// <c>mobExtra</c> stay raw bytes rather than typed fields, matching how the reference handlers themselves just
/// struct-copy them. Simplified deliberately (see AGENTS.md): <paramref name="posX"/>/<paramref name="posY"/>
/// must be the character's saved SPX/SPY - the guild-zone/city-spawn table and the collision-avoiding
/// <c>GetEmptyMobGrid</c> search (used only when there is no saved position) require a guild-zone and map/grid
/// system this port does not have yet, so a character that never saved a position lands wherever its class
/// template's SPX/SPY happens to be, rather than a real spawn point. <paramref name="weather"/> is expected to
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

using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Client 7.69 wire DTO for MSG_CNFCharacterLogin (1728 bytes total). This is the
/// client contract, not the conflicting private TMSrv or legacy W2PP relay layout.
/// Extensions can be supplied as wire bytes or through CharacterLoginExtensionsV769.
/// </summary>
public sealed class CharacterLoginConfirmationV769
{
    public const ushort MessageType = 0x0114;
    public const ushort DefaultSceneId = 30_000;
    public const int MobSize = 1_040;
    public const int ShortSkillSize = 16;
    public const int Ext1Size = 288;
    public const int Ext2Size = 360;
    public const int PayloadSize = 1_716;
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public const int PosXOffset = 0;
    public const int PosYOffset = 2;
    public const int MobOffset = 4;
    public const int SlotOffset = 1_044;
    public const int ClientIdOffset = 1_046;
    public const int WeatherOffset = 1_048;
    public const int ShortSkillOffset = 1_050;
    public const int Ext1Offset = 1_068;
    public const int Ext2Offset = 1_356;

    private readonly byte[] mob;
    private readonly byte[] shortSkill;
    private readonly byte[] ext1;
    private readonly byte[] ext2;

    public short PosX { get; }
    public short PosY { get; }
    public ushort Slot { get; }
    public ushort ClientId { get; }
    public ushort Weather { get; }
    public ushort SceneId { get; }

    public CharacterLoginConfirmationV769(
        short posX,
        short posY,
        ReadOnlyMemory<byte> mob,
        ushort slot,
        ushort clientId,
        ushort weather,
        ReadOnlyMemory<byte> shortSkill,
        ReadOnlyMemory<byte> ext1,
        ReadOnlyMemory<byte> ext2,
        ushort sceneId = DefaultSceneId)
    {
        RequireLength(mob, MobSize, nameof(mob));
        RequireLength(shortSkill, ShortSkillSize, nameof(shortSkill));
        RequireLength(ext1, Ext1Size, nameof(ext1));
        RequireLength(ext2, Ext2Size, nameof(ext2));

        PosX = posX;
        PosY = posY;
        this.mob = mob.ToArray();
        Slot = slot;
        ClientId = clientId;
        Weather = weather;
        this.shortSkill = shortSkill.ToArray();
        this.ext1 = ext1.ToArray();
        this.ext2 = ext2.ToArray();
        SceneId = sceneId;
    }

    public CharacterLoginConfirmationV769(
        short posX,
        short posY,
        ReadOnlyMemory<byte> mob,
        ushort slot,
        ushort clientId,
        ushort weather,
        ReadOnlyMemory<byte> shortSkill,
        CharacterLoginExtensionsV769 extensions,
        ushort sceneId = DefaultSceneId)
        : this(posX, posY, mob, slot, clientId, weather, shortSkill,
            (extensions ?? throw new ArgumentNullException(nameof(extensions))).ToExt1(),
            extensions.ToExt2(), sceneId)
    {
    }

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(PosXOffset), PosX);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(PosYOffset), PosY);
        mob.CopyTo(payload, MobOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(SlotOffset), Slot);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(ClientIdOffset), ClientId);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(WeatherOffset), Weather);
        shortSkill.CopyTo(payload, ShortSkillOffset);
        // payload bytes 1066..1067 are x86 alignment before STRUCT_EXT1.
        ext1.CopyTo(payload, Ext1Offset);
        ext2.CopyTo(payload, Ext2Offset);
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, SceneId, clientTick, ToPayload(), keywordIndex);
    }

    private static void RequireLength(ReadOnlyMemory<byte> value, int expected, string parameterName)
    {
        if (value.Length != expected)
            throw new ArgumentException($"The 7.69 character-login {parameterName} block requires exactly {expected} bytes.", parameterName);
    }
}

using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Client 7.69 <c>MSG_CreateMobTrade</c> wire contract. The Win32 probe
/// measures 260 bytes: it shares the first part of <c>MSG_CreateMob</c>, then
/// carries 18 secondary appearance bytes and a 24-byte shop description.
/// </summary>
public sealed class CreateMobTradeConfirmationV769
{
    public const ushort MessageType = 0x0363;
    public const ushort SceneId = 30000;
    public const int PacketSize = 260;
    public const int PayloadSize = PacketSize - PacketHeader.SizeInBytes;
    public const int MobNameLength = 16;
    public const int EquipmentCount = 18;
    public const int AffectCount = 32;
    public const int SecondaryAppearanceLength = 18;
    public const int NickLength = 26;
    public const int DescriptionLength = 24;
    public const int PositionXOffset = 0;
    public const int PositionYOffset = 2;
    public const int MobIdOffset = 4;
    public const int MobNameOffset = 6;
    public const int EquipmentOffset = 22;
    public const int AffectOffset = 58;
    public const int GuildOffset = 122;
    public const int GuildLevelOffset = 124;
    public const int ScoreOffset = 128;
    public const int CreateTypeOffset = 176;
    public const int SecondaryAppearanceOffset = 178;
    public const int NickOffset = 196;
    public const int DescriptionOffset = 222;
    public const int ServerOffset = 246;

    private readonly byte[] mobName;
    private readonly ushort[] visualEquipment;
    private readonly ushort[] affects;
    private readonly byte[] secondaryAppearance;
    private readonly byte[] nick;
    private readonly byte[] description;

    public CreateMobTradeConfirmationV769(
        short positionX,
        short positionY,
        ushort mobId,
        ReadOnlyMemory<byte> mobName,
        IReadOnlyList<ushort> visualEquipment,
        IReadOnlyList<ushort> affects,
        ushort guild,
        byte guildLevel,
        ClientScoreV769 score,
        ushort createType,
        ReadOnlyMemory<byte> secondaryAppearance,
        ReadOnlyMemory<byte> nick,
        ReadOnlyMemory<byte> description,
        byte server)
    {
        RequireLength(mobName, MobNameLength, nameof(mobName));
        RequireLength(secondaryAppearance, SecondaryAppearanceLength, nameof(secondaryAppearance));
        RequireLength(nick, NickLength, nameof(nick));
        RequireLength(description, DescriptionLength, nameof(description));
        if (visualEquipment.Count != EquipmentCount)
            throw new ArgumentException($"Client 7.69 CreateMobTrade requires {EquipmentCount} visual equipment entries.", nameof(visualEquipment));
        if (affects.Count != AffectCount)
            throw new ArgumentException($"Client 7.69 CreateMobTrade requires {AffectCount} affect entries.", nameof(affects));

        PositionX = positionX;
        PositionY = positionY;
        MobId = mobId;
        this.mobName = mobName.ToArray();
        this.visualEquipment = visualEquipment.ToArray();
        this.affects = affects.ToArray();
        Guild = guild;
        GuildLevel = guildLevel;
        Score = score;
        CreateType = createType;
        this.secondaryAppearance = secondaryAppearance.ToArray();
        this.nick = nick.ToArray();
        this.description = description.ToArray();
        Server = server;
        VisualEquipment = Array.AsReadOnly(this.visualEquipment);
        Affects = Array.AsReadOnly(this.affects);
        MobName = this.mobName;
        SecondaryAppearance = this.secondaryAppearance;
        Nick = this.nick;
        Description = this.description;
    }

    public short PositionX { get; }
    public short PositionY { get; }
    public ushort MobId { get; }
    public ushort Guild { get; }
    public byte GuildLevel { get; }
    public ClientScoreV769 Score { get; }
    public ushort CreateType { get; }
    public byte Server { get; }
    public ReadOnlyMemory<byte> MobName { get; }
    public IReadOnlyList<ushort> VisualEquipment { get; }
    public IReadOnlyList<ushort> Affects { get; }
    public ReadOnlyMemory<byte> SecondaryAppearance { get; }
    public ReadOnlyMemory<byte> Nick { get; }
    public ReadOnlyMemory<byte> Description { get; }

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(PositionXOffset), PositionX);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(PositionYOffset), PositionY);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(MobIdOffset), MobId);
        mobName.CopyTo(payload.AsMemory(MobNameOffset, MobNameLength));

        for (var index = 0; index < EquipmentCount; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(EquipmentOffset + (index * sizeof(ushort))), visualEquipment[index]);
        for (var index = 0; index < AffectCount; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(AffectOffset + (index * sizeof(ushort))), affects[index]);

        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(GuildOffset), Guild);
        payload[GuildLevelOffset] = GuildLevel;
        Score.Write(payload.AsSpan(ScoreOffset, ClientScoreV769.SizeInBytes));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(CreateTypeOffset), CreateType);
        secondaryAppearance.CopyTo(payload.AsMemory(SecondaryAppearanceOffset, SecondaryAppearanceLength));
        nick.CopyTo(payload.AsMemory(NickOffset, NickLength));
        description.CopyTo(payload.AsMemory(DescriptionOffset, DescriptionLength));
        payload[ServerOffset] = Server;
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
            throw new ArgumentException($"Client 7.69 CreateMobTrade {parameterName} requires exactly {expected} bytes.", parameterName);
    }
}

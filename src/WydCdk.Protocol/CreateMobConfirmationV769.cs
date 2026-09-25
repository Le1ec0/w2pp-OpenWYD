using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Client 7.69 <c>MSG_CreateMob</c> wire contract. This is the 18-slot
/// client message and is intentionally separate from the 16-slot W2PP sender.
/// </summary>
public sealed class CreateMobConfirmationV769
{
    public const ushort MessageType = 0x0364;
    public const ushort SceneId = 30000;
    public const int PacketSize = 236;
    public const int PayloadSize = PacketSize - PacketHeader.SizeInBytes;
    public const int MobNameLength = 16;
    public const int EquipmentCount = 18;
    public const int AffectCount = 32;
    public const int AncientCodeCount = 18;
    public const int NickLength = 26;
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
    public const int AncientCodeOffset = 178;
    public const int NickOffset = 196;
    public const int ServerOffset = 222;

    private readonly byte[] mobName;
    private readonly ushort[] visualEquipment;
    private readonly ushort[] affects;
    private readonly byte[] ancientCodes;
    private readonly byte[] nick;

    public CreateMobConfirmationV769(
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
        ReadOnlyMemory<byte> ancientCodes,
        ReadOnlyMemory<byte> nick,
        byte server)
    {
        RequireLength(mobName, MobNameLength, nameof(mobName));
        RequireLength(ancientCodes, AncientCodeCount, nameof(ancientCodes));
        RequireLength(nick, NickLength, nameof(nick));
        if (visualEquipment.Count != EquipmentCount)
            throw new ArgumentException($"Client 7.69 CreateMob requires {EquipmentCount} visual equipment entries.", nameof(visualEquipment));
        if (affects.Count != AffectCount)
            throw new ArgumentException($"Client 7.69 CreateMob requires {AffectCount} affect entries.", nameof(affects));

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
        this.ancientCodes = ancientCodes.ToArray();
        this.nick = nick.ToArray();
        Server = server;
        VisualEquipment = Array.AsReadOnly(this.visualEquipment);
        Affects = Array.AsReadOnly(this.affects);
        MobName = this.mobName;
        AncientCodes = this.ancientCodes;
        Nick = this.nick;
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
    public ReadOnlyMemory<byte> AncientCodes { get; }
    public ReadOnlyMemory<byte> Nick { get; }

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(PositionXOffset), PositionX);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(PositionYOffset), PositionY);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(MobIdOffset), MobId);
        mobName.CopyTo(payload.AsMemory(MobNameOffset, MobNameLength));

        for (var index = 0; index < EquipmentCount; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(EquipmentOffset + index * sizeof(ushort)), visualEquipment[index]);
        for (var index = 0; index < AffectCount; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(AffectOffset + index * sizeof(ushort)), affects[index]);

        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(GuildOffset), Guild);
        payload[GuildLevelOffset] = GuildLevel;
        Score.Write(payload.AsSpan(ScoreOffset, ClientScoreV769.SizeInBytes));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(CreateTypeOffset), CreateType);
        ancientCodes.CopyTo(payload.AsMemory(AncientCodeOffset, AncientCodeCount));
        nick.CopyTo(payload.AsMemory(NickOffset, NickLength));
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
            throw new ArgumentException($"Client 7.69 CreateMob {parameterName} requires exactly {expected} bytes.", parameterName);
    }
}

using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Client 7.69 MSG_UpdateScore. This is deliberately separate from the older
/// W2PP score confirmation: the target client keeps padding inside STRUCT_SCORE
/// and aligns ReqHp after the four-byte Resist array.
/// </summary>
public sealed class UpdateScoreConfirmationV769
{
    public const ushort MessageType = 0x0336;
    public const int AffectCount = 32;
    public const int PayloadSize = 140;
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public const int ScoreOffset = 0;
    public const int CriticalOffset = 48;
    public const int SaveManaOffset = 49;
    public const int AffectOffset = 50;
    public const int GuildOffset = 114;
    public const int GuildLevelOffset = 116;
    public const int ResistOffset = 118;
    public const int ReqHpOffset = 124;
    public const int ReqMpOffset = 128;
    public const int MagicOffset = 132;
    public const int RsvOffset = 134;
    public const int LearnedSkillOffset = 136;

    private readonly ushort[] affects;
    private readonly byte[] resist;

    public UpdateScoreConfirmationV769(
        ClientScoreV769 score,
        byte critical,
        byte saveMana,
        IReadOnlyList<ushort> affects,
        ushort guild,
        ushort guildLevel,
        IReadOnlyList<byte> resist,
        int reqHp,
        int reqMp,
        ushort magic,
        ushort rsv,
        byte learnedSkill)
    {
        ArgumentNullException.ThrowIfNull(affects);
        ArgumentNullException.ThrowIfNull(resist);
        if (affects.Count != AffectCount)
            throw new ArgumentException($"Client 7.69 UpdateScore requires exactly {AffectCount} affects.", nameof(affects));
        if (resist.Count != 4)
            throw new ArgumentException("Client 7.69 UpdateScore requires exactly four resistances.", nameof(resist));

        Score = score;
        Critical = critical;
        SaveMana = saveMana;
        this.affects = affects.ToArray();
        Guild = guild;
        GuildLevel = guildLevel;
        this.resist = resist.ToArray();
        ReqHp = reqHp;
        ReqMp = reqMp;
        Magic = magic;
        Rsv = rsv;
        LearnedSkill = learnedSkill;
        Affects = Array.AsReadOnly(this.affects);
        Resist = Array.AsReadOnly(this.resist);
    }

    public ClientScoreV769 Score { get; }
    public byte Critical { get; }
    public byte SaveMana { get; }
    public IReadOnlyList<ushort> Affects { get; }
    public ushort Guild { get; }
    public ushort GuildLevel { get; }
    public IReadOnlyList<byte> Resist { get; }
    public int ReqHp { get; }
    public int ReqMp { get; }
    public ushort Magic { get; }
    public ushort Rsv { get; }
    public byte LearnedSkill { get; }

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        Score.Write(payload.AsSpan(ScoreOffset, ClientScoreV769.SizeInBytes));
        payload[CriticalOffset] = Critical;
        payload[SaveManaOffset] = SaveMana;

        for (var index = 0; index < AffectCount; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(AffectOffset + (index * sizeof(ushort))), affects[index]);

        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(GuildOffset), Guild);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(GuildLevelOffset), GuildLevel);
        resist.AsSpan().CopyTo(payload.AsSpan(ResistOffset, resist.Length));
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(ReqHpOffset), ReqHp);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(ReqMpOffset), ReqMp);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(MagicOffset), Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(RsvOffset), Rsv);
        payload[LearnedSkillOffset] = LearnedSkill;
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, id, clientTick, ToPayload(), keywordIndex);
    }
}

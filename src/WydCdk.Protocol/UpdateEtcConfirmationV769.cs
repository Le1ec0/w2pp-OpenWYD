using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Client 7.69 MSG_UpdateEtc. The native struct is 48 bytes: its payload has
/// four bytes of alignment after SkillBonus and four trailing padding bytes.
/// </summary>
public sealed class UpdateEtcConfirmationV769
{
    public const ushort MessageType = 0x0337;
    public const int LearnedSkillCount = 2;
    public const int PayloadSize = 36;
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public const int FakeExpOffset = 0;
    public const int ExpOffset = 4;
    public const int LearnedSkillOffset = 12;
    public const int ScoreBonusOffset = 20;
    public const int SpecialBonusOffset = 22;
    public const int SkillBonusOffset = 24;
    public const int CoinOffset = 28;

    private readonly uint[] learnedSkill;

    public UpdateEtcConfirmationV769(
        int fakeExp,
        long experience,
        IReadOnlyList<uint> learnedSkill,
        short scoreBonus,
        short specialBonus,
        short skillBonus,
        int coin)
    {
        ArgumentNullException.ThrowIfNull(learnedSkill);
        if (learnedSkill.Count != LearnedSkillCount)
            throw new ArgumentException($"Client 7.69 UpdateEtc requires exactly {LearnedSkillCount} learned-skill words.", nameof(learnedSkill));

        FakeExp = fakeExp;
        Experience = experience;
        this.learnedSkill = learnedSkill.ToArray();
        ScoreBonus = scoreBonus;
        SpecialBonus = specialBonus;
        SkillBonus = skillBonus;
        Coin = coin;
        LearnedSkill = Array.AsReadOnly(this.learnedSkill);
    }

    public int FakeExp { get; }
    public long Experience { get; }
    public IReadOnlyList<uint> LearnedSkill { get; }
    public short ScoreBonus { get; }
    public short SpecialBonus { get; }
    public short SkillBonus { get; }
    public int Coin { get; }

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(FakeExpOffset), FakeExp);
        BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(ExpOffset), Experience);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(LearnedSkillOffset), learnedSkill[0]);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(LearnedSkillOffset + sizeof(uint)), learnedSkill[1]);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(ScoreBonusOffset), ScoreBonus);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(SpecialBonusOffset), SpecialBonus);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(SkillBonusOffset), SkillBonus);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(CoinOffset), Coin);
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, id, clientTick, ToPayload(), keywordIndex);
    }
}

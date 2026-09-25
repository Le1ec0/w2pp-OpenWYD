using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Client 7.69 wire representation of STRUCT_SCORE. This is intentionally distinct from
/// LegacyScore: Level is a 16-bit field and the two ABI padding regions are zeroed.
/// </summary>
public readonly record struct ClientScoreV769(
    short Level,
    int Ac,
    int Damage,
    byte Reserved,
    byte AttackRun,
    int MaxHp,
    int MaxMp,
    int Hp,
    int Mp,
    short Strength,
    short Intelligence,
    short Dexterity,
    short Constitution,
    ushort Special1,
    ushort Special2,
    ushort Special3,
    ushort Special4)
{
    public const int SizeInBytes = 48;

    public void Write(Span<byte> destination)
    {
        if (destination.Length < SizeInBytes)
            throw new ArgumentException("Client 7.69 score requires 48 bytes.", nameof(destination));

        var score = destination[..SizeInBytes];
        score.Clear();
        BinaryPrimitives.WriteInt16LittleEndian(score, Level);
        BinaryPrimitives.WriteInt32LittleEndian(score[4..], Ac);
        BinaryPrimitives.WriteInt32LittleEndian(score[8..], Damage);
        score[12] = Reserved;
        score[13] = AttackRun;
        BinaryPrimitives.WriteInt32LittleEndian(score[16..], MaxHp);
        BinaryPrimitives.WriteInt32LittleEndian(score[20..], MaxMp);
        BinaryPrimitives.WriteInt32LittleEndian(score[24..], Hp);
        BinaryPrimitives.WriteInt32LittleEndian(score[28..], Mp);
        BinaryPrimitives.WriteInt16LittleEndian(score[32..], Strength);
        BinaryPrimitives.WriteInt16LittleEndian(score[34..], Intelligence);
        BinaryPrimitives.WriteInt16LittleEndian(score[36..], Dexterity);
        BinaryPrimitives.WriteInt16LittleEndian(score[38..], Constitution);
        BinaryPrimitives.WriteUInt16LittleEndian(score[40..], Special1);
        BinaryPrimitives.WriteUInt16LittleEndian(score[42..], Special2);
        BinaryPrimitives.WriteUInt16LittleEndian(score[44..], Special3);
        BinaryPrimitives.WriteUInt16LittleEndian(score[46..], Special4);
    }
}

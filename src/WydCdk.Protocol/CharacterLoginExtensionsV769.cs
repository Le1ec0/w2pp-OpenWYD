using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Deterministic baseline for the 7.69 login extensions. The client reads Data[0]
/// as FakeExp; unsupported Data entries, login-embedded affects, and Ext2 are zeroed.
/// Effects and subclass/account metadata require separate, explicitly mapped contracts.
/// </summary>
public sealed class CharacterLoginExtensionsV769
{
    public const int Ext1Size = CharacterLoginConfirmationV769.Ext1Size;
    public const int Ext2Size = CharacterLoginConfirmationV769.Ext2Size;
    public int FakeExp { get; }

    public CharacterLoginExtensionsV769(int fakeExp)
    {
        if (fakeExp < 0) throw new ArgumentOutOfRangeException(nameof(fakeExp), "Fake EXP must be non-negative.");
        FakeExp = fakeExp;
    }

    public byte[] ToExt1()
    {
        var ext1 = new byte[Ext1Size];
        BinaryPrimitives.WriteInt32LittleEndian(ext1, FakeExp); // STRUCT_EXT1.Data[0]
        return ext1;
    }

    public byte[] ToExt2() => new byte[Ext2Size];
}

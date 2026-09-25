using System.Buffers.Binary;
using System.Text;

namespace WydCdk.World;

/// <summary>
/// A server-side ItemList definition from the 7.69 DBSrv/TMSrv and W2PP
/// contract. It is deliberately separate from <see cref="LegacyItemDefinition" />:
/// the client/converter record is 164 bytes and has a 32-bit position plus
/// extension fields, while this server record is 140 bytes and has a 16-bit
/// position with no client-only tail.
/// </summary>
public sealed record LegacyServerItemDefinition(
    short Index,
    string Name,
    short MeshIndex,
    short TextureIndex,
    short VisualEffectIndex,
    short RequiredLevel,
    short RequiredStrength,
    short RequiredIntelligence,
    short RequiredDexterity,
    short RequiredConstitution,
    IReadOnlyList<LegacyItemStaticEffect> StaticEffects,
    int Price,
    short Unique,
    short Position,
    short Extra,
    short Grade);

/// <summary>
/// Reader for the server runtime ItemList.bin emitted by BASE_WriteItemList.
/// The 6500 records are XORed with 0x5A and followed by the opaque checksum
/// trailer written by the legacy loader. This class is an isolated format
/// adapter; the active C# combat/equipment paths still use LegacyItemDataTable
/// until the authoritative release input is selected.
/// </summary>
public sealed class LegacyServerItemDataTable
{
    public const int MaxItemIndex = 6500;
    public const int RecordSizeInBytes = 140;
    public const int OpaqueTrailerSizeInBytes = sizeof(int);
    public const int BodySizeInBytes = MaxItemIndex * RecordSizeInBytes;
    public const int FileSizeInBytes = BodySizeInBytes + OpaqueTrailerSizeInBytes;
    private const byte XorKey = 0x5A;
    private const int StaticEffectCount = 12;

    private readonly LegacyServerItemDefinition?[] definitions;

    private LegacyServerItemDataTable(LegacyServerItemDefinition?[] definitions) => this.definitions = definitions;

    public int Count => definitions.Count(static definition => definition is not null);

    public LegacyServerItemDefinition? this[int itemIndex] =>
        itemIndex is >= 0 and < MaxItemIndex ? definitions[itemIndex] : null;

    public static LegacyServerItemDataTable Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Load(File.ReadAllBytes(path));
    }

    public static LegacyServerItemDataTable Load(ReadOnlySpan<byte> encodedFile)
    {
        if (encodedFile.Length != BodySizeInBytes && encodedFile.Length != FileSizeInBytes)
            throw new InvalidDataException($"Server ItemList.bin must contain {BodySizeInBytes} record bytes, optionally followed by {OpaqueTrailerSizeInBytes} opaque bytes.");

        var definitions = new LegacyServerItemDefinition?[MaxItemIndex];
        for (var index = 0; index < MaxItemIndex; index++)
        {
            var encodedRecord = encodedFile.Slice(index * RecordSizeInBytes, RecordSizeInBytes);
            var decoded = new byte[RecordSizeInBytes];
            for (var offset = 0; offset < decoded.Length; offset++)
                decoded[offset] = (byte)(encodedRecord[offset] ^ XorKey);

            var effects = new LegacyItemStaticEffect[StaticEffectCount];
            for (var effect = 0; effect < effects.Length; effect++)
            {
                var effectOffset = 80 + (effect * sizeof(short) * 2);
                effects[effect] = new LegacyItemStaticEffect(
                    BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(effectOffset)),
                    BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(effectOffset + sizeof(short))));
            }

            definitions[index] = new LegacyServerItemDefinition(
                (short)index,
                ReadCString(decoded.AsSpan(0, 64)),
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(64)),
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(66)),
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(68)),
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(70)),
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(72)),
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(74)),
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(76)),
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(78)),
                effects,
                BinaryPrimitives.ReadInt32LittleEndian(decoded.AsSpan(128)),
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(132)),
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(134)),
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(136)),
                BinaryPrimitives.ReadInt16LittleEndian(decoded.AsSpan(138)));
        }

        return new LegacyServerItemDataTable(definitions);
    }

    private static string ReadCString(ReadOnlySpan<byte> source)
    {
        var length = source.IndexOf((byte)0);
        if (length < 0) length = source.Length;
        return Encoding.ASCII.GetString(source[..length]);
    }
}

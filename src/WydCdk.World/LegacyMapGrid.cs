namespace WydCdk.World;

/// <summary>
/// Height/collision view used by the legacy GetEmptyMobGrid path.
/// The server stores heightmap.dat as one byte per cell (4096x4096) and
/// AttributeMap.dat as one byte per 4x4 world-cell tile (1024x1024).
/// </summary>
public sealed class LegacyMapGrid
{
    public const int HeightWidth = 4096;
    public const int HeightHeight = 4096;
    public const int AttributeWidth = 1024;
    public const int AttributeHeight = 1024;
    public const int HeightMapSize = HeightWidth * HeightHeight;
    public const int AttributeMapSize = AttributeWidth * AttributeHeight;

    private readonly byte[] height;
    private readonly byte[] attributes;

    public LegacyMapGrid(ReadOnlyMemory<byte> heightMap, ReadOnlyMemory<byte> attributeMap)
    {
        if (heightMap.Length != HeightMapSize)
            throw new ArgumentException($"heightmap.dat must contain exactly {HeightMapSize} bytes.", nameof(heightMap));
        // The legacy loader calls fread(g_pAttribute, 1024, 1024, fp) and
        // intentionally ignores anything after the first megabyte. Some
        // released TMSrv files carry a four-byte trailer (1,048,580 bytes).
        if (attributeMap.Length < AttributeMapSize)
            throw new ArgumentException($"AttributeMap.dat must contain at least {AttributeMapSize} bytes.", nameof(attributeMap));

        height = heightMap.ToArray();
        attributes = attributeMap.Span[..AttributeMapSize].ToArray();
        for (var y = 0; y < HeightHeight; y++)
        {
            var attributeRow = (y >> 2) * AttributeWidth;
            var heightRow = y * HeightWidth;
            for (var x = 0; x < HeightWidth; x++)
            {
                if ((attributes[attributeRow + (x >> 2)] & 2) != 0)
                    height[heightRow + x] = 127;
            }
        }
    }

    public static LegacyMapGrid Load(string heightMapPath, string attributeMapPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(heightMapPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeMapPath);
        return new LegacyMapGrid(File.ReadAllBytes(heightMapPath), File.ReadAllBytes(attributeMapPath));
    }

    public bool IsBlocked(int x, int y) =>
        x < 0 || y < 0 || x >= HeightWidth || y >= HeightHeight || height[(y * HeightWidth) + x] == 127;

    /// <summary>Matches TMSrv/GetFunc.cpp:GetAttribute, including its four-by-four map tile scale.</summary>
    public byte GetAttribute(int x, int y)
    {
        if (x < 0 || y < 0 || x >= HeightWidth || y >= HeightHeight)
            return 0;

        return attributes[((y >> 2) * AttributeWidth) + (x >> 2)];
    }

    public int? GetTerrainHeight(int x, int y) =>
        x < 0 || y < 0 || x >= HeightWidth || y >= HeightHeight
            ? null
            : height[(y * HeightWidth) + x];
}

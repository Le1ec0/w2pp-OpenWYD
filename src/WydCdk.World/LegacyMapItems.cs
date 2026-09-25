using System.Buffers.Binary;
using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>A decoded STRUCT_INITITEM row used to create static field items.</summary>
public sealed record LegacyMapItemDefinition(short PositionX, short PositionY, short ItemIndex, short Rotate);

/// <summary>The server-side state created from one static map-item row.</summary>
public sealed record LegacyMapItemState(
    int ItemId,
    LegacyItem Item,
    short PositionX,
    short PositionY,
    short Rotate,
    int GridCharge,
    int State,
    int Height,
    int Delay);

/// <summary>
/// Decodes the legacy InitItem.bin tail format and creates the equivalent
/// initial pItem entries. InitItem.bin is XORed with 0xFF and terminates at
/// the first row whose PosX is not positive, matching BASE_ReadInitItem.
/// </summary>
public static class LegacyMapItemCatalog
{
    public const int InitItemRecordSize = 8;
    public const int MaxMapItems = 5000;
    private const byte XorKey = 0xFF;

    public static IReadOnlyList<LegacyMapItemDefinition> LoadDefinitions(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return LoadDefinitions(File.ReadAllBytes(path));
    }

    public static IReadOnlyList<LegacyMapItemDefinition> LoadDefinitions(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length == 0 || encoded.Length % InitItemRecordSize != 0)
            throw new InvalidDataException($"Legacy InitItem.bin must contain a positive multiple of {InitItemRecordSize} bytes.");

        var definitions = new List<LegacyMapItemDefinition>();
        for (var offset = 0; offset < encoded.Length; offset += InitItemRecordSize)
        {
            var positionX = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(encoded[offset..(offset + 2)]) ^ 0xFFFF));
            var positionY = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(encoded[(offset + 2)..(offset + 4)]) ^ 0xFFFF));
            var itemIndex = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(encoded[(offset + 4)..(offset + 6)]) ^ 0xFFFF));
            var rotate = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(encoded[(offset + 6)..(offset + 8)]) ^ 0xFFFF));

            if (positionX <= 0)
                break;
            definitions.Add(new LegacyMapItemDefinition(positionX, positionY, itemIndex, rotate));
        }

        return definitions;
    }

    public static IReadOnlyList<LegacyMapItemState> CreateInitialStates(
        IReadOnlyList<LegacyMapItemDefinition> definitions,
        LegacyItemDataTable itemData,
        LegacyMapGrid? mapGrid = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(itemData);
        if (definitions.Count >= MaxMapItems)
            throw new InvalidDataException($"Legacy InitItem.bin contains too many map items ({definitions.Count} >= {MaxMapItems}).");

        var states = new List<LegacyMapItemState>(definitions.Count);
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            if (definition.ItemIndex <= 0 || definition.ItemIndex >= LegacyItemDataTable.MaxItemIndex)
                throw new InvalidDataException($"InitItem row {index} references invalid item index {definition.ItemIndex}.");
            if (definition.Rotate is < 0 or >= LegacyGroundMaskTable.RotationCount)
                throw new InvalidDataException($"InitItem row {index} references invalid rotation {definition.Rotate}.");

            var item = new LegacyItem(definition.ItemIndex, 0, 0, 0, 0, 0, 0);
            var height = mapGrid?.GetTerrainHeight(definition.PositionX, definition.PositionY) ?? 0;
            states.Add(new LegacyMapItemState(
                ItemId: index + 1,
                item,
                definition.PositionX,
                definition.PositionY,
                definition.Rotate,
                itemData.GetItemAbility(item, LegacyItemEffect.Ground),
                LegacyMapItemStateCodes.Open,
                height,
                Delay: 90));
        }

        return states;
    }
}

public static class LegacyMapItemStateCodes
{
    public const int WireIdOffset = 10_000;
    public const int Nothing = 0;
    public const int Open = 1;
    public const int Closed = 2;
    public const int Locked = 3;
}

/// <summary>Official 6x6x4 legacy ground-mask table for dynamic map-item state.</summary>
public sealed class LegacyGroundMaskTable
{
    public const int MaskCount = 6;
    public const int RotationCount = 4;
    public const int Width = 6;
    public const int Height = 6;

    private readonly int[,,,] values;

    /// <summary>
    /// Builds the six masks consumed by the server's <c>MAX_GROUNDMASK=6</c>.
    /// The values and rotations are the first six entries of the static table
    /// in <c>Backup/Tools/Reference759/ClienteSource/Projects/TMProject/Basedef.h</c>.
    /// The client source contains four additional visual masks (6..9), but the
    /// server rejects those indices and never consumes them.
    /// </summary>
    public static LegacyGroundMaskTable CreateOfficial()
    {
        var masks = new int[MaskCount, RotationCount, Height, Width];

        static void FillRow(int[,,,] table, int mask, int rotation, int y, int startX, int length, int value)
        {
            for (var x = startX; x < startX + length; x++)
                table[mask, rotation, y, x] = value;
        }

        static void FillColumn(int[,,,] table, int mask, int rotation, int startY, int x, int length, int value)
        {
            for (var y = startY; y < startY + length; y++)
                table[mask, rotation, y, x] = value;
        }

        FillRow(masks, 1, 0, 2, 1, 3, 18);
        FillColumn(masks, 1, 1, 1, 2, 3, 18);
        FillRow(masks, 1, 2, 2, 1, 3, 18);
        FillColumn(masks, 1, 3, 1, 2, 3, 18);

        FillRow(masks, 2, 0, 2, 0, 4, 18);
        FillColumn(masks, 2, 1, 1, 2, 4, 18);
        FillRow(masks, 2, 2, 2, 1, 4, 18);
        FillColumn(masks, 2, 3, 1, 2, 4, 18);

        FillRow(masks, 3, 0, 2, 2, 2, 18);
        FillColumn(masks, 3, 1, 2, 2, 2, 18);
        FillRow(masks, 3, 2, 2, 2, 2, 18);
        FillColumn(masks, 3, 3, 2, 2, 2, 18);

        FillRow(masks, 4, 0, 2, 0, 4, 18);
        FillColumn(masks, 4, 1, 1, 2, 4, 18);
        FillRow(masks, 4, 2, 2, 1, 4, 18);
        FillColumn(masks, 4, 3, 1, 2, 4, 18);

        FillRow(masks, 5, 0, 1, 0, 6, 18);
        FillRow(masks, 5, 0, 5, 0, 6, 18);
        FillColumn(masks, 5, 1, 0, 1, 6, 18);
        FillColumn(masks, 5, 1, 0, 4, 6, 18);
        FillRow(masks, 5, 2, 0, 0, 6, 18);
        FillRow(masks, 5, 2, 4, 0, 6, 18);
        FillColumn(masks, 5, 3, 0, 1, 6, 18);
        FillColumn(masks, 5, 3, 0, 4, 6, 18);

        return new LegacyGroundMaskTable(masks);
    }

    public LegacyGroundMaskTable(int[,,,] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.GetLength(0) != MaskCount || values.GetLength(1) != RotationCount || values.GetLength(2) != Height || values.GetLength(3) != Width)
            throw new ArgumentException("Legacy ground masks must have dimensions [6,4,6,6].", nameof(values));
        this.values = (int[,,,])values.Clone();
    }

    public int this[int mask, int rotate, int y, int x] => values[mask, rotate, y, x];

    /// <summary>Applies BASE_UpdateItem's state delta to the mutable height grid.</summary>
    public bool TryApply(LegacyMapGrid mapGrid, int mask, int currentState, int nextState, int positionX, int positionY, int rotate, out int height)
    {
        ArgumentNullException.ThrowIfNull(mapGrid);
        height = 0;
        if (mask is < 0 or >= MaskCount || rotate is < 0 or >= RotationCount)
            return false;

        var delta = currentState == LegacyMapItemStateCodes.Open && nextState is LegacyMapItemStateCodes.Locked or LegacyMapItemStateCodes.Closed
            ? 1
            : nextState == LegacyMapItemStateCodes.Open && currentState is LegacyMapItemStateCodes.Locked or LegacyMapItemStateCodes.Closed
                ? -1
                : 0;
        if (delta == 0)
            return false;

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var maskValue = values[mask, rotate, y, x];
                if (maskValue == 0)
                    continue;

                var worldX = positionX + x - 2;
                var worldY = positionY + y - 2;
                if (!mapGrid.IsInsideDynamicHeight(worldX, worldY))
                    break;

                var last = Math.Clamp(mapGrid.GetMutableTerrainHeight(worldX, worldY) + (maskValue * delta), 0, 255);
                if (maskValue != 0)
                    height = last;
                mapGrid.SetMutableTerrainHeight(worldX, worldY, last);
            }
        }

        return true;
    }
}

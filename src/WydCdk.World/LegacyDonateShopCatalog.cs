using System.Globalization;
using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Server-authoritative Donate Shop catalog. Each non-comment line uses the
/// explicit format: store,page,itemPosition,itemIndex,price,stock.
/// </summary>
public sealed class LegacyDonateShopCatalog
{
    public const int StoreCount = 3;
    public const int PageCount = 5;
    public const int ItemCountPerPage = 15;
    public const int SlotCount = StoreCount * PageCount * ItemCountPerPage;

    private readonly DonateStoreEntry[] entries;

    private LegacyDonateShopCatalog(DonateStoreEntry[] entries) => this.entries = entries;

    public static LegacyDonateShopCatalog Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Parse(File.ReadAllText(path));
    }

    public static LegacyDonateShopCatalog Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var slots = new DonateStoreEntry?[SlotCount];
        var lineNumber = 0;
        foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("//", StringComparison.Ordinal))
                continue;

            var fields = line.Split([',', ';', '\t'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 6)
                throw new FormatException($"Donate Shop catalog line {lineNumber} requires six integer fields.");

            var values = new int[fields.Length];
            for (var index = 0; index < values.Length; index++)
            {
                if (!int.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[index]))
                    throw new FormatException($"Donate Shop catalog line {lineNumber} has a non-integer field.");
            }

            var entry = new DonateStoreEntry(values[0], values[1], values[2], values[3], values[4], values[5]);
            ValidateCoordinates(entry, lineNumber);
            if (entry.ItemIndex < 0 || entry.ItemIndex > short.MaxValue)
                throw new FormatException($"Donate Shop catalog line {lineNumber} has an item index outside STRUCT_ITEM.");
            if (entry.Price < 0 || entry.Stock < 0)
                throw new FormatException($"Donate Shop catalog line {lineNumber} cannot have negative price or stock.");

            var linear = GetLinearIndex(entry.Store, entry.Page, entry.ItemPosition);
            if (slots[linear] is not null)
                throw new FormatException($"Donate Shop catalog line {lineNumber} repeats store/page/position.");
            slots[linear] = entry;
        }

        if (slots.Any(static entry => entry is null))
            throw new FormatException($"Donate Shop catalog requires exactly {SlotCount} unique slots.");
        return FromEntries(slots.Select(static entry => entry!.Value).ToArray());
    }

    public static LegacyDonateShopCatalog FromEntries(IReadOnlyList<DonateStoreEntry> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Count != SlotCount)
            throw new ArgumentException($"Donate Shop catalog requires exactly {SlotCount} entries.", nameof(source));

        var slots = new DonateStoreEntry?[SlotCount];
        for (var index = 0; index < source.Count; index++)
        {
            var entry = source[index];
            ValidateCoordinates(entry, index + 1);
            if (entry.ItemIndex < 0 || entry.ItemIndex > short.MaxValue)
                throw new ArgumentException("Donate Shop item index is outside STRUCT_ITEM.", nameof(source));
            if (entry.Price < 0 || entry.Stock < 0)
                throw new ArgumentException("Donate Shop price and stock cannot be negative.", nameof(source));
            var linear = GetLinearIndex(entry.Store, entry.Page, entry.ItemPosition);
            if (slots[linear] is not null)
                throw new ArgumentException("Donate Shop coordinates must be unique.", nameof(source));
            slots[linear] = entry;
        }

        return new LegacyDonateShopCatalog(slots.Select(static entry => entry!.Value).ToArray());
    }

    public IReadOnlyList<DonateStoreEntry> Entries => entries;

    public DonateStoreCatalogConfirmation ToConfirmation() => new(entries);

    public bool TryGet(int store, int page, int itemPosition, out DonateStoreEntry entry)
    {
        if (store is < 0 or >= StoreCount || page is < 0 or >= PageCount || itemPosition is < 0 or >= ItemCountPerPage)
        {
            entry = default;
            return false;
        }

        entry = entries[GetLinearIndex(store, page, itemPosition)];
        return true;
    }

    private static int GetLinearIndex(int store, int page, int itemPosition) =>
        ((store * PageCount) + page) * ItemCountPerPage + itemPosition;

    private static void ValidateCoordinates(DonateStoreEntry entry, int lineNumber)
    {
        if (entry.Store is < 0 or >= StoreCount || entry.Page is < 0 or >= PageCount || entry.ItemPosition is < 0 or >= ItemCountPerPage)
            throw new FormatException($"Donate Shop catalog line {lineNumber} has coordinates outside the 3x5x15 matrix.");
    }
}

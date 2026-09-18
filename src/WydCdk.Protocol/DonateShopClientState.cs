namespace WydCdk.Protocol;

/// <summary>
/// C# state model for the legacy Donate Shop client flow. Rendering and input
/// dispatch remain client-specific, but selection, quantity and request
/// construction are kept independent of the native WYD executable.
/// </summary>
public sealed class DonateShopClientState
{
    public const int MaxQuantity = 120;

    private readonly DonateStoreEntry[] entries = new DonateStoreEntry[DonateStoreCatalogConfirmation.StoreCount * DonateStoreCatalogConfirmation.PageCount * DonateStoreCatalogConfirmation.ItemCountPerPage];
    private DonateShopOpenConfirmation? currentPage;
    private DonateStoreEntry? selectedEntry;

    public bool IsOpen { get; private set; }
    public int Store { get; private set; }
    public int Page { get; private set; }
    public int ItemPosition { get; private set; } = -1;
    public int Quantity { get; private set; }
    public int DonateBalance { get; private set; }
    public DonateStoreEntry? SelectedEntry => selectedEntry;
    public DonateShopOpenConfirmation? CurrentPage => currentPage;

    public void Open()
    {
        IsOpen = true;
        Store = 0;
        Page = 0;
        ClearSelection();
    }

    public void Close()
    {
        IsOpen = false;
        ClearSelection();
    }

    public void ApplyBalance(DonateBalanceConfirmation balance)
    {
        ArgumentNullException.ThrowIfNull(balance);
        DonateBalance = Math.Max(0, balance.Cash);
        ClampQuantityToBalance();
    }

    public void ApplyCatalog(DonateStoreCatalogConfirmation catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        foreach (var entry in catalog.Entries)
            entries[GetLinearIndex(entry.Store, entry.Page, entry.ItemPosition)] = entry;

        if (selectedEntry is { } selected && TryGet(Store, Page, selected.ItemPosition, out var refreshed))
            selectedEntry = refreshed;
        ClampQuantityToBalance();
    }

    public bool SelectStorePage(int store, int page)
    {
        if (store is < 0 or >= DonateStoreCatalogConfirmation.StoreCount || page is < 0 or >= DonateStoreCatalogConfirmation.PageCount)
            return false;

        Store = store;
        Page = page;
        ClearSelection();
        return true;
    }

    public void ApplyPage(DonateShopOpenConfirmation page)
    {
        ArgumentNullException.ThrowIfNull(page);
        currentPage = page;
    }

    public bool SelectItem(int itemPosition)
    {
        if (!IsOpen || itemPosition is < 0 or >= DonateStoreCatalogConfirmation.ItemCountPerPage || !TryGet(Store, Page, itemPosition, out var entry) || entry.ItemIndex <= 0)
            return false;

        ItemPosition = itemPosition;
        selectedEntry = entry;
        Quantity = GetAffordableQuantity(entry) == 0 ? 0 : 1;
        return true;
    }

    public bool AdjustQuantity(int delta)
    {
        if (selectedEntry is not { } entry || delta == 0)
            return false;

        var maxAffordable = GetAffordableQuantity(entry);
        if (maxAffordable == 0)
            return false;

        var candidate = Math.Clamp(Quantity + delta, 1, maxAffordable);

        var changed = candidate != Quantity;
        Quantity = candidate;
        return changed;
    }

    public bool TryBuildPurchase(out DonatePurchaseRequest? request)
    {
        if (!IsOpen || selectedEntry is null || ItemPosition < 0 || Quantity is < 1 or > MaxQuantity)
        {
            request = null;
            return false;
        }

        request = new DonatePurchaseRequest(Store, Page, ItemPosition, Quantity);
        return true;
    }

    private bool TryGet(int store, int page, int itemPosition, out DonateStoreEntry entry)
    {
        if (store is < 0 or >= DonateStoreCatalogConfirmation.StoreCount || page is < 0 or >= DonateStoreCatalogConfirmation.PageCount || itemPosition is < 0 or >= DonateStoreCatalogConfirmation.ItemCountPerPage)
        {
            entry = default;
            return false;
        }

        entry = entries[GetLinearIndex(store, page, itemPosition)];
        return true;
    }

    private void ClearSelection()
    {
        ItemPosition = -1;
        Quantity = 0;
        selectedEntry = null;
        currentPage = null;
    }

    private void ClampQuantityToBalance()
    {
        if (selectedEntry is not { } entry)
            return;

        var maxAffordable = GetAffordableQuantity(entry);
        Quantity = maxAffordable == 0
            ? 0
            : Math.Clamp(Quantity <= 0 ? 1 : Quantity, 1, maxAffordable);
    }

    private int GetAffordableQuantity(DonateStoreEntry entry) => entry.Price <= 0
        ? MaxQuantity
        : Math.Min(MaxQuantity, Math.Max(0, DonateBalance / entry.Price));

    private static int GetLinearIndex(int store, int page, int itemPosition) =>
        ((store * DonateStoreCatalogConfirmation.PageCount) + page) * DonateStoreCatalogConfirmation.ItemCountPerPage + itemPosition;
}

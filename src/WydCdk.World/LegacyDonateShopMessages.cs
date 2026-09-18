using System.Globalization;

namespace WydCdk.World;

/// <summary>
/// Texts sent by the legacy Donate Shop through SendClientMessage. The
/// transport is encoded by <c>MSG_MessagePanel</c> in the protocol project;
/// this class keeps the purchase-specific wording next to the domain rules.
/// </summary>
public static class LegacyDonateShopMessages
{
    public const string Throttled = "Aguarde 3 segundo para uma nova Tentativa.";
    public const string InsufficientDonate = "Saldo de Rubis Insuficiente";
    public const string InventoryFull = "Não há espaço disponível no Inventário";

    public static string PurchaseAccepted(int quantity, string itemName, int totalPrice) =>
        string.Format(CultureInfo.InvariantCulture, "Comprou [x{0}] {1} por [{2}] Rubis", quantity, itemName, totalPrice);

    public static string? ForRejectedPurchase(LegacyDonatePurchaseResult result) => result switch
    {
        LegacyDonatePurchaseResult.InsufficientDonate => InsufficientDonate,
        LegacyDonatePurchaseResult.InventoryFull => InventoryFull,
        _ => null,
    };
}

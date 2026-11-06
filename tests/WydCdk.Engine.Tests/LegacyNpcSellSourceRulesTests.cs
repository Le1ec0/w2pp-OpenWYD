using WydCdk.World;

internal static class LegacyNpcSellSourceRulesTests
{
    public static void Run()
    {
        EquipmentBoundsAndRefreshFlagMatchLegacy();
        CarryBoundsReserveTheLastFourSlots();
        CargoBoundsAndAutoTradeGateMatchLegacyOrder();
        UnknownSignedTypesAreRejected();
    }

    private static void EquipmentBoundsAndRefreshFlagMatchLegacy()
    {
        Check(Resolve(0, 1).IsValid && Resolve(0, 1).Kind == LegacyNpcSellSourceKind.Equipment && Resolve(0, 1).RequiresEquipmentUpdate,
            "Equipment position 1 did not resolve with the legacy equipment refresh flag.");
        Check(Resolve(0, 7).RequiresEquipmentUpdate && !Resolve(0, 8).RequiresEquipmentUpdate && !Resolve(0, 15).RequiresEquipmentUpdate,
            "The legacy equipment refresh flag changed its strict position < 8 boundary.");
        Check(Resolve(0, 0).Status == LegacyNpcSellSourceStatus.InvalidTypeOrPosition &&
              Resolve(0, -1).Status == LegacyNpcSellSourceStatus.InvalidTypeOrPosition &&
              Resolve(0, 16).Status == LegacyNpcSellSourceStatus.InvalidTypeOrPosition,
            "Equipment positions outside 1..15 were accepted.");
    }

    private static void CarryBoundsReserveTheLastFourSlots()
    {
        Check(Resolve(1, 0).IsValid && Resolve(1, 59).IsValid,
            "The inclusive carry sale bounds 0 and 59 were not accepted.");
        Check(Resolve(1, 60).Status == LegacyNpcSellSourceStatus.InvalidTypeOrPosition &&
              Resolve(1, 63).Status == LegacyNpcSellSourceStatus.InvalidTypeOrPosition &&
              Resolve(1, -1).Status == LegacyNpcSellSourceStatus.InvalidTypeOrPosition,
            "Carry sale accepted a reserved or out-of-range slot.");
        Check(Resolve(1, 59, tradeMode: true).IsValid,
            "Auto-trade incorrectly blocked a carry sale.");
    }

    private static void CargoBoundsAndAutoTradeGateMatchLegacyOrder()
    {
        Check(Resolve(2, 0).IsValid && Resolve(2, 127).IsValid,
            "The inclusive cargo bounds 0 and 127 were not accepted.");
        Check(Resolve(2, -1).Status == LegacyNpcSellSourceStatus.InvalidTypeOrPosition &&
              Resolve(2, 128).Status == LegacyNpcSellSourceStatus.InvalidTypeOrPosition,
            "Cargo sale accepted an out-of-range slot.");
        Check(Resolve(2, 0, tradeMode: true).Status == LegacyNpcSellSourceStatus.CargoBlockedDuringAutoTrade &&
              Resolve(2, 128, tradeMode: true).Status == LegacyNpcSellSourceStatus.CargoBlockedDuringAutoTrade,
            "TradeMode did not reject cargo before validating its position.");
    }

    private static void UnknownSignedTypesAreRejected()
    {
        Check(Resolve(-1, 0).Status == LegacyNpcSellSourceStatus.InvalidTypeOrPosition &&
              Resolve(3, 0).Status == LegacyNpcSellSourceStatus.InvalidTypeOrPosition,
            "An unknown signed item source type was accepted.");
    }

    private static LegacyNpcSellSourceResolution Resolve(short myType, short myPos, bool tradeMode = false) =>
        LegacyNpcSellSourceRules.Resolve(myType, myPos, tradeMode);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

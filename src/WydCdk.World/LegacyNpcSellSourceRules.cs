using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Resolves the source type and slot accepted by TMProject 7.69/W2PP
/// <c>_MSG_Sell</c>. It does not read the selected inventory or validate the
/// item stored there.
/// </summary>
public static class LegacyNpcSellSourceRules
{
    private const int ReservedCarrySlots = 4;
    private const int EquipmentRelayPositionLimit = 8;

    public static LegacyNpcSellSourceResolution Resolve(short myType, short myPos, bool tradeMode)
    {
        // Both legacy handlers reject cargo during TradeMode before checking MyPos.
        if (myType == (short)LegacyNpcSellSourceKind.Cargo && tradeMode)
        {
            return new(
                LegacyNpcSellSourceStatus.CargoBlockedDuringAutoTrade,
                LegacyNpcSellSourceKind.Cargo,
                myPos,
                RequiresEquipmentUpdate: false);
        }

        if (myType == (short)LegacyNpcSellSourceKind.Equipment)
        {
            if (myPos <= 0 || myPos >= LegacyCharacterSelection.EquipmentCount)
                return Invalid(myPos);

            return new(
                LegacyNpcSellSourceStatus.Valid,
                LegacyNpcSellSourceKind.Equipment,
                myPos,
                RequiresEquipmentUpdate: myPos < EquipmentRelayPositionLimit);
        }

        if (myType == (short)LegacyNpcSellSourceKind.Carry)
        {
            if (myPos < 0 || myPos >= LegacyAccountSnapshot.MobCarryCount - ReservedCarrySlots)
                return Invalid(myPos);

            return new(LegacyNpcSellSourceStatus.Valid, LegacyNpcSellSourceKind.Carry, myPos, RequiresEquipmentUpdate: false);
        }

        if (myType == (short)LegacyNpcSellSourceKind.Cargo)
        {
            if (myPos < 0 || myPos >= LegacyAccountSnapshot.CargoCount)
                return Invalid(myPos);

            return new(LegacyNpcSellSourceStatus.Valid, LegacyNpcSellSourceKind.Cargo, myPos, RequiresEquipmentUpdate: false);
        }

        return Invalid(myPos);
    }

    private static LegacyNpcSellSourceResolution Invalid(short myPos) =>
        new(LegacyNpcSellSourceStatus.InvalidTypeOrPosition, null, myPos, RequiresEquipmentUpdate: false);
}

public enum LegacyNpcSellSourceKind : short
{
    Equipment = 0,
    Carry = 1,
    Cargo = 2,
}

public enum LegacyNpcSellSourceStatus
{
    Valid,
    InvalidTypeOrPosition,
    CargoBlockedDuringAutoTrade,
}

public readonly record struct LegacyNpcSellSourceResolution(
    LegacyNpcSellSourceStatus Status,
    LegacyNpcSellSourceKind? Kind,
    short Position,
    bool RequiresEquipmentUpdate)
{
    public bool IsValid => Status == LegacyNpcSellSourceStatus.Valid;
}

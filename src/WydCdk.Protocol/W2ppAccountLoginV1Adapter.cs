namespace WydCdk.Protocol;

/// <summary>
/// Lossy projection from the current W2PP account-login response into the 7.69 client
/// contract. It is characterization-only and is not wired into the listener.
/// </summary>
public static class W2ppAccountLoginV1Adapter
{
    public static AccountLoginConfirmationV769 Adapt(AccountLoginConfirmation source)
    {
        ArgumentNullException.ThrowIfNull(source);

        for (var index = AccountLoginConfirmationV769.CargoCount; index < source.Cargo.Count; index++)
        {
            if (source.Cargo[index] != default)
                throw new InvalidOperationException("W2PP cargo items 120-127 cannot be represented by the 7.69 client contract.");
        }

        var cargo = source.Cargo.Take(AccountLoginConfirmationV769.CargoCount).ToArray();

        // W2PP HashKeyTable is not proven equivalent to the 7.69 SecretCode. The target
        // DBSrv response zero-initializes SecretCode and SSN1/SSN2 and does not populate them.
        // Keep that observed target behavior; do not copy HashKeyTable or private account data.
        return new AccountLoginConfirmationV769(
            new byte[AccountLoginConfirmationV769.SecretCodeLength],
            W2ppCharacterSelectionV1Adapter.Adapt(source.Selection),
            cargo,
            source.Coin,
            source.AccountName,
            ssn1: 0,
            ssn2: 0);
    }
}

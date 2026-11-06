namespace WydCdk.World;

/// <summary>Ports the two truncating tax-share divisions from 7.69/W2PP _MSG_Sell.</summary>
public static class LegacyNpcSellTaxExperienceMath
{
    public static bool TryCalculate(int grossPrice, int netPrice, out int experience)
    {
        experience = 0;
        if (grossPrice < 0 || netPrice < 0 || grossPrice < netPrice)
            return false;

        experience = (grossPrice - netPrice) / 2;
        experience /= 2;
        return true;
    }
}

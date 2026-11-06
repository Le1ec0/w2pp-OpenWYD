using WydCdk.World;

internal static class LegacyNpcSellTaxExperienceTests
{
    public static void Run()
    {
        UsesTheTwoLegacyIntegerDivisionsAndRejectsInvalidAmounts();
    }

    private static void UsesTheTwoLegacyIntegerDivisionsAndRejectsInvalidAmounts()
    {
        Check(LegacyNpcSellTaxExperienceMath.TryCalculate(10_003, 9_000, out var experience) && experience == 250,
            "Sale tax experience did not truncate (gross-net)/2 and then /2 independently.");
        Check(!LegacyNpcSellTaxExperienceMath.TryCalculate(9_000, 10_000, out _) &&
              !LegacyNpcSellTaxExperienceMath.TryCalculate(-1, 0, out _) &&
              !LegacyNpcSellTaxExperienceMath.TryCalculate(0, -1, out _),
            "Invalid gross/net amounts produced a tax experience credit.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

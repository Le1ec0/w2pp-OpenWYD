using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>Faithful date encoding used by the legacy <c>BASE_SetItemDate</c>.</summary>
public static class LegacyItemDateMath
{
    public static LegacyItem SetItemDate(LegacyItem item, int days, DateTime localNow)
    {
        var dayNext = localNow.Day + days;
        var month = localNow.Month - 1;
        var year = localNow.Year - 1900;

        if (localNow.Day + days >= 30)
            month += 1;
        if (month >= 12)
            month = 0;
        if (dayNext >= 30)
            dayNext -= 29;
        if (month == 1 && dayNext >= 27)
            dayNext -= 26;

        return item with
        {
            Effect1 = checked((byte)LegacyItemEffect.WDay),
            Value1 = unchecked((byte)dayNext),
            Effect2 = checked((byte)LegacyItemEffect.WMonth),
            Value2 = checked((byte)(month + 1)),
            Effect3 = checked((byte)LegacyItemEffect.Year),
            Value3 = unchecked((byte)(year - 100)),
        };
    }
}

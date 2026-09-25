using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>Faithful date encoding used by the legacy <c>BASE_SetItemDate</c>.</summary>
public static class LegacyItemDateMath
{
    public static LegacyItem SetItemDate(LegacyItem item, int days, DateTime localNow)
    {
        var dayNext = localNow.Day + days;
        var year = localNow.Year - 1900;
        var month = days >= 30 ? localNow.Month : localNow.Month - 1;

        if (month >= 12)
        {
            month = 0;
            year++;
        }
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

    /// <summary>Reproduces the legacy <c>BASE_CheckItemDate</c> comparisons.</summary>
    public static bool IsExpired(LegacyItem item, DateTime localNow)
    {
        var itemDay = item.Value1;
        var itemMonth = item.Value2;
        var itemYear = item.Value3;
        var currentYear = localNow.Year - 2000;

        if (localNow.Day >= itemDay && localNow.Month - 1 >= itemMonth - 1 && currentYear >= itemYear)
            return true;
        if (localNow.Month - 1 > itemMonth && currentYear >= itemYear)
            return true;
        return currentYear > itemYear;
    }
}

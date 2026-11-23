using PunchPoint.Core.Models;

namespace PunchPoint.Core.TimeCalc;

/// <summary>Creates pay period date ranges from company settings.</summary>
public static class PayPeriodGenerator
{
    public static PayPeriod Create(DateTime start, PayPeriodType type, int splitDay = 16, int monthlyStartDay = 1)
    {
        start = start.Date;

        var period = new PayPeriod { Start = start, Type = type, SplitDay = splitDay };

        period.End = type switch
        {
            PayPeriodType.Weekly => start.AddDays(6),
            PayPeriodType.BiWeekly => start.AddDays(13),
            PayPeriodType.SemiMonthly => start.Day <= splitDay - 1
                ? new DateTime(start.Year, start.Month, Math.Min(splitDay - 1, DateTime.DaysInMonth(start.Year, start.Month)))
                : new DateTime(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month)),
            PayPeriodType.Monthly => new DateTime(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month)),
            _ => start.AddDays(6),
        };

        return period;
    }

    public static DateTime NextStart(PayPeriod period) => period.Type switch
    {
        PayPeriodType.Weekly => period.Start.AddDays(7),
        PayPeriodType.BiWeekly => period.Start.AddDays(14),
        PayPeriodType.SemiMonthly => period.Start.Day <= period.SplitDay - 1
            ? new DateTime(period.Start.Year, period.Start.Month, period.SplitDay)
            : new DateTime(period.Start.Year, period.Start.Month, 1).AddMonths(1),
        PayPeriodType.Monthly => new DateTime(period.Start.Year, period.Start.Month, 1).AddMonths(1),
        _ => period.Start.AddDays(7),
    };

    /// <summary>
    /// Returns the period start on or before <paramref name="anchor"/> for the given type.
    /// </summary>
    public static DateTime AlignStart(DateTime anchor, PayPeriodType type, int splitDay = 16, int monthlyStartDay = 1)
    {
        anchor = anchor.Date;
        switch (type)
        {
            case PayPeriodType.Weekly:
            case PayPeriodType.BiWeekly:
                return anchor;
            case PayPeriodType.SemiMonthly:
                return anchor.Day >= splitDay
                    ? new DateTime(anchor.Year, anchor.Month, splitDay)
                    : new DateTime(anchor.Year, anchor.Month, 1);
            case PayPeriodType.Monthly:
                return new DateTime(anchor.Year, anchor.Month, Math.Min(monthlyStartDay, DateTime.DaysInMonth(anchor.Year, anchor.Month)));
            default:
                return anchor;
        }
    }

    /// <summary>
    /// Generates every period needed to cover <paramref name="from"/>..<paramref name="to"/>,
    /// starting from the settings anchor. Existing periods are returned unchanged.
    /// </summary>
    public static List<PayPeriod> EnsureRange(AppSettings settings, DateTime from, DateTime to)
    {
        var periods = new List<PayPeriod>();

        var start = AlignStart(settings.PeriodAnchor, settings.PeriodType,
            settings.SemiMonthlySplitDay, settings.MonthlyStartDay);

        // Walk backwards to cover the start of the requested range.
        while (true)
        {
            var previous = PreviousStart(start, settings);
            if (previous is null || previous.Value.AddDays(40) < from.Date) break;
            start = previous.Value;
        }

        var cursor = start;
        int guard = 0;
        while (cursor.Date <= to.Date && guard++ < 2000)
        {
            var period = Create(cursor, settings.PeriodType,
                settings.SemiMonthlySplitDay, settings.MonthlyStartDay);
            periods.Add(period);
            cursor = NextStart(period);
        }

        return periods.Where(p => p.End.Date >= from.Date).ToList();
    }

    private static DateTime? PreviousStart(DateTime start, AppSettings settings) => settings.PeriodType switch
    {
        PayPeriodType.Weekly => start.AddDays(-7),
        PayPeriodType.BiWeekly => start.AddDays(-14),
        PayPeriodType.SemiMonthly => start.Day > 1
            ? new DateTime(start.Year, start.Month, 1)
            : new DateTime(start.Year, start.Month, 1).AddMonths(-1).AddDays(settings.SemiMonthlySplitDay - 1),
        PayPeriodType.Monthly => new DateTime(start.Year, start.Month, 1).AddMonths(-1),
        _ => start.AddDays(-7),
    };
}

namespace PunchPoint.Core.Models;

/// <summary>
/// Human labels for pay periods in drop-downs: the period containing today is marked
/// "Current", the one before it "Previous", the one after it "Next"; older and future
/// periods just show their dates.
/// </summary>
public static class PayPeriodLabels
{
    public static string Label(PayPeriod period, IReadOnlyList<PayPeriod> allPeriods, DateTime? today = null)
    {
        DateTime moment = (today ?? DateTime.Today).Date;

        var current = allPeriods.FirstOrDefault(p => p.Contains(moment));
        var previous = current is null
            ? null
            : allPeriods.Where(p => p.End.Date < current.Start.Date)
                .OrderByDescending(p => p.End)
                .FirstOrDefault();
        var next = current is null
            ? null
            : allPeriods.Where(p => p.Start.Date > current.End.Date)
                .OrderBy(p => p.Start)
                .FirstOrDefault();

        string range = Range(period);

        if (current is not null && Same(period, current)) return $"Current · {range}";
        if (previous is not null && Same(period, previous)) return $"Previous · {range}";
        if (next is not null && Same(period, next)) return $"Next · {range}";

        return range;
    }

    public static string Range(PayPeriod period) =>
        $"{period.Start:MMM d} – {period.End:MMM d, yyyy}";

    private static bool Same(PayPeriod a, PayPeriod b) =>
        a.Id != 0 ? a.Id == b.Id : ReferenceEquals(a, b);
}

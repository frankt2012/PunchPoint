namespace PunchPoint.Core.Models;

/// <summary>
/// Human labels for pay periods in drop-downs: the period containing today is marked
/// "Current", the one before it "Previous", the one after it "Next"; older and future
/// periods just show their dates.
/// </summary>
public static class PayPeriodLabels
{
    /// <summary>Marks the period containing <paramref name="today"/> (used by tests and tools).</summary>
    public static string Label(PayPeriod period, IReadOnlyList<PayPeriod> allPeriods, DateTime? today)
        => Label(period, allPeriods, currentPeriodId: null, today);

    public static string Label(PayPeriod period, IReadOnlyList<PayPeriod> allPeriods)
        => Label(period, allPeriods, currentPeriodId: null, today: null);

    public static string Label(PayPeriod period, IReadOnlyList<PayPeriod> allPeriods,
        int? currentPeriodId, DateTime? today = null)
    {
        DateTime moment = (today ?? DateTime.Today).Date;

        // When two periods cover today (leftover grid after an import), the caller tells us
        // which one is real. Falling back to "first one containing today" is ambiguous.
        var current = currentPeriodId is int id
            ? allPeriods.FirstOrDefault(p => p.Id == id) ?? allPeriods.FirstOrDefault(p => p.Contains(moment))
            : allPeriods.FirstOrDefault(p => p.Contains(moment));
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

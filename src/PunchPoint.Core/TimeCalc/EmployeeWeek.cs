using PunchPoint.Core.Models;
using PunchPoint.Core.Reports;
using PunchPoint.Core.Storage;

namespace PunchPoint.Core.TimeCalc;

/// <summary>One day of an employee's own pay period, as shown on the clock.</summary>
public sealed record WeekDay(
    DateTime Date,
    DateTime? FirstIn,
    DateTime? LastOut,
    int BreakMinutes,
    int WorkedMinutes,
    int TotalMinutes,
    bool IsOpen,
    bool IsHoliday,
    string? PaidLabel = null);

/// <summary>An employee's own view of the current pay period.</summary>
public sealed class WeekSummary
{
    public required Employee Employee { get; init; }
    public required PayPeriod Period { get; init; }
    public List<WeekDay> Days { get; } = new();
    public int TotalMinutes { get; set; }
    public int BreakMinutes { get; set; }

    /// <summary>Minutes a supervisor added or removed with "Edit totals" for this period.</summary>
    public int AdjustmentMinutes { get; set; }
    public bool Approved { get; set; }
    public int IssueCount { get; set; }

    /// <summary>True when there is nothing to show yet (a fresh period, or no punches at all).</summary>
    public bool IsEmpty => Days.Count == 0;

    public string TotalLabel(AppSettings settings) => ReportBuilder.Hours(TotalMinutes, settings.TotalsFormat);
}

/// <summary>
/// Builds the "my punches" view an employee can open from the clock: the days of the current pay
/// period with the first clock-in, last clock-out, breaks and the total, plus the period total.
/// </summary>
public static class EmployeeWeek
{
    public static WeekSummary Build(Store store, Employee employee, AppSettings settings, PayPeriod? period = null)
    {
        period ??= store.GetCurrentPayPeriod();
        if (period is null)
            return new WeekSummary
            {
                Employee = employee,
                Period = new PayPeriod { Start = DateTime.Today, End = DateTime.Today },
            };

        var punches = store.ListPunches(employee.Id, period.Start, period.End.AddDays(1));
        var shift = employee.ShiftId is int shiftId ? store.GetShift(shiftId) : null;
        var card = store.GetTimeCard(employee.Id, period.Id);

        var result = TimeCardCalculator.Calculate(employee, shift, period, punches, settings,
            store.ListHolidays(), card);

        var summary = new WeekSummary
        {
            Employee = employee,
            Period = period,
            TotalMinutes = result.TotalMinutes,
            BreakMinutes = result.Days.Sum(d => d.BreakMinutes),
            AdjustmentMinutes = result.AdjustmentMinutes,
            Approved = card?.Approved == true,
            IssueCount = result.Issues.Count,
        };

        foreach (var day in result.Days.OrderBy(d => d.WorkDate))
        {
            bool open = day.Segments.Any(s => s.IsOpen);
            bool hasPunches = day.Segments.Count > 0;

            // Days with no punches and no paid hours are not worth a row on the clock.
            if (!hasPunches && day.TotalMinutes == 0) continue;

            DateTime? firstIn = hasPunches ? day.Segments.Min(s => s.In) : null;

            DateTime? lastOut = null;
            foreach (var segment in day.Segments.Where(s => s.Out is not null).OrderBy(s => s.Out))
                lastOut = segment.Out;

            // A day that only carries paid hours (holiday, vacation, sick) should say so,
            // otherwise it looks like a mistake with no times.
            string? paidLabel = null;
            if (!hasPunches)
            {
                var paid = punches
                    .Where(p => p.Kind == PunchKind.Hours
                                && TimeCardCalculator.WorkDate(p.Timestamp, settings.DayChangeOffsetMinutes) == day.WorkDate.Date)
                    .ToList();

                if (paid.Count > 0)
                    paidLabel = paid[0].HoursCategory.Length > 0 ? paid[0].HoursCategory : "Paid hours";
            }

            summary.Days.Add(new WeekDay(
                day.WorkDate.Date,
                firstIn,
                lastOut,
                day.BreakMinutes,
                day.WorkedMinutes,
                day.TotalMinutes,
                open,
                day.IsHoliday,
                paidLabel));
        }

        return summary;
    }
}

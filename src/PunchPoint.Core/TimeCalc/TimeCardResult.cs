using PunchPoint.Core.Models;

namespace PunchPoint.Core.TimeCalc;

public sealed class WorkSegment
{
    public DateTime In { get; init; }
    public DateTime? Out { get; set; }
    public DateTime PaidIn { get; set; }
    public DateTime? PaidOut { get; set; }
    public int BreakMinutes { get; set; }
    public bool IsOpen => Out is null;
    public bool CrossesMidnight { get; init; }

    public List<string> Adjustments { get; } = new();

    /// <summary>Paid duration of the segment (excluding breaks).</summary>
    public int PaidMinutes
    {
        get
        {
            var end = PaidOut ?? PaidIn;
            int minutes = (int)Math.Round((end - PaidIn).TotalMinutes);
            return Math.Max(0, minutes - BreakMinutes);
        }
    }

    public TimeSpan RawDuration =>
        (Out ?? In) - In;

    public override string ToString() =>
        $"{In:h:mm tt} – {(Out?.ToString("h:mm tt") ?? "open")} ({PaidMinutes} min)";
}

public sealed class DayTotals
{
    public DateTime WorkDate { get; init; }
    public List<WorkSegment> Segments { get; } = new();
    public int RegularMinutes { get; set; }
    public int OverTime1Minutes { get; set; }
    public int OverTime2Minutes { get; set; }
    public int BreakMinutes { get; set; }
    public int HolidayMinutes { get; set; }
    public bool IsHoliday { get; set; }
    public string? HolidayName { get; set; }

    public List<string> Issues { get; } = new();

    public int WorkedMinutes => Segments.Sum(s => s.PaidMinutes);
    public int TotalMinutes => RegularMinutes + OverTime1Minutes + OverTime2Minutes + HolidayMinutes;

    public string FirstInLabel => Segments.Count == 0 ? "" : Segments[0].In.ToString("h:mm tt");
    public string LastOutLabel
    {
        get
        {
            var last = Segments.LastOrDefault(s => s.Out is not null);
            return last?.Out?.ToString("h:mm tt") ?? "";
        }
    }
}

public sealed class TimeCardResult
{
    public required Employee Employee { get; init; }
    public required PayPeriod PayPeriod { get; init; }
    public Shift? Shift { get; init; }
    public List<DayTotals> Days { get; } = new();
    public List<string> Issues { get; } = new();

    public int RegularMinutes { get; set; }
    public int OverTime1Minutes { get; set; }
    public int OverTime2Minutes { get; set; }
    public int HolidayMinutes { get; set; }
    public int BreakMinutes { get; set; }
    public int AdjustmentMinutes { get; set; }

    public int TotalMinutes => RegularMinutes + OverTime1Minutes + OverTime2Minutes + HolidayMinutes + AdjustmentMinutes;

    public decimal Hours(int minutes) => Math.Round(minutes / 60m, 2);

    /// <summary>Time total formatted in hundredths (the payroll convention).</summary>
    public static string Hundredths(int minutes) => (minutes / 60m).ToString("0.00");
}

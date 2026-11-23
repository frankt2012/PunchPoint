namespace PunchPoint.Core.Models;

public enum PayPeriodType
{
    Weekly = 0,
    BiWeekly = 1,
    SemiMonthly = 2,
    Monthly = 3,
}

public sealed class PayPeriod
{
    public int Id { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public PayPeriodType Type { get; set; }
    /// <summary>Day of month used for the second half of a semi-monthly period.</summary>
    public int SplitDay { get; set; } = 15;
    public bool IsClosed { get; set; }

    public string Label => $"{Start:MMM d} – {End:MMM d, yyyy}";
    public int DayCount => (int)(End.Date - Start.Date).TotalDays + 1;
    public bool Contains(DateTime moment) => moment.Date >= Start.Date && moment.Date <= End.Date;

    public override string ToString() => Label;
}

public sealed class Holiday
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime Date { get; set; }
    public bool Paid { get; set; } = true;

    public override string ToString() => $"{Name} ({Date:d})";
}

public sealed class TimeCard
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int PayPeriodId { get; set; }
    public bool Approved { get; set; }
    public string ApprovedBy { get; set; } = "";
    public DateTime? ApprovedAt { get; set; }

    /// <summary>Manual adjustments in minutes, added to the computed totals.</summary>
    public int AdjustRegularMinutes { get; set; }
    public int AdjustOverTime1Minutes { get; set; }
    public int AdjustOverTime2Minutes { get; set; }

    public string Note { get; set; } = "";
}

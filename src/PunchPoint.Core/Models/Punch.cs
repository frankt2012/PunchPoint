namespace PunchPoint.Core.Models;

/// <summary>How a punch time is adjusted before it is paid.</summary>
public enum RoundingMode
{
    /// <summary>No adjustment.</summary>
    None = 0,
    /// <summary>Round to the nearest rounding interval.</summary>
    Nearest = 1,
    /// <summary>Always round the punch later (in the direction that favours the employee).</summary>
    Up = 2,
    /// <summary>Always round the punch earlier.</summary>
    Down = 3,
}

public enum PunchKind
{
    In = 0,
    Out = 1,
    /// <summary>Start of an unpaid break (lunch).</summary>
    BreakOut = 2,
    /// <summary>End of an unpaid break (lunch).</summary>
    BreakIn = 3,
    /// <summary>Miscellaneous paid hours (vacation, holiday, sick, other).</summary>
    Hours = 4,
}

public enum PunchSource
{
    Clock = 0,
    Manual = 1,
    Import = 2,
    TimeSlip = 3,
    Auto = 4,
}

public sealed class Punch
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int? PayPeriodId { get; set; }
    public DateTime Timestamp { get; set; }
    public PunchKind Kind { get; set; }
    public PunchSource Source { get; set; } = PunchSource.Clock;
    public string Comment { get; set; } = "";
    public bool IsVoided { get; set; }

    /// <summary>Paid hours for <see cref="PunchKind.Hours"/> punches.</summary>
    public TimeSpan Hours { get; set; }

    /// <summary>Label for miscellaneous hours: Vacation, Holiday, Sick, Other.</summary>
    public string HoursCategory { get; set; } = "";

    public int? LegacyId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool IsPunch => Kind is PunchKind.In or PunchKind.Out or PunchKind.BreakIn or PunchKind.BreakOut;
}

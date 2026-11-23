using PunchPoint.Core.Models;

namespace PunchPoint.Core.Models;

public sealed class AppSettings
{
    public string CompanyName { get; set; } = "My Company";
    public string CompanyMessage { get; set; } = "";

    /// <summary>PIN required to open the management screens. Empty means no gate.</summary>
    public string AdminPin { get; set; } = "";

    public PayPeriodType PeriodType { get; set; } = PayPeriodType.Weekly;
    /// <summary>Start date of the current (or next) pay period.</summary>
    public DateTime PeriodAnchor { get; set; } = DateTime.Today;
    /// <summary>For semi-monthly periods: the day of the month that starts the second half.</summary>
    public int SemiMonthlySplitDay { get; set; } = 16;
    /// <summary>For monthly periods: day of month a period starts.</summary>
    public int MonthlyStartDay { get; set; } = 1;

    /// <summary>Payroll rounding interval in minutes (0 = none).</summary>
    public int RoundingMinutes { get; set; }

    public RoundingMode RoundInMode { get; set; } = RoundingMode.Nearest;
    public RoundingMode RoundOutMode { get; set; } = RoundingMode.Nearest;

    /// <summary>Daily overtime level 1 threshold in minutes (0 = disabled).</summary>
    public int DailyOverTime1Minutes { get; set; }
    public int DailyOverTime2Minutes { get; set; }
    /// <summary>Weekly overtime threshold in minutes (0 = disabled).</summary>
    public int WeeklyOverTime1Minutes { get; set; }
    public int WeeklyOverTime2Minutes { get; set; }

    /// <summary>Hours after midnight that still belong to the previous work day.</summary>
    public int DayChangeOffsetMinutes { get; set; }

    /// <summary>Maximum continuous time on the clock before a missing OUT is assumed.</summary>
    public int MaxPunchSpanMinutes { get; set; } = 16 * 60;

    public bool RequireApprovalBeforeExport { get; set; }
    public bool RoundDailyTotalInsteadOfPunches { get; set; }

    public string TimeFormat { get; set; } = "h:mm tt";
    public string DateFormat { get; set; } = "M/d/yyyy";

    public string LastBackupPath { get; set; } = "";
    public int SchemaVersion { get; set; } = 1;
}

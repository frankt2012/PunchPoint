using PunchPoint.Core.Models;

namespace PunchPoint.Core.Models;

public enum AppTheme
{
    /// <summary>Follow the Windows light/dark setting.</summary>
    System = 0,
    Light = 1,
    Dark = 2,
}

public enum TotalsFormat
{
    Minutes = 0,
    Hundredths = 1,
}

public sealed class AppSettings
{
    public string CompanyName { get; set; } = "My Company";
    public string CompanyMessage { get; set; } = "";

    /// <summary>Hashed supervisor password. Empty means the admin app is not protected yet.</summary>
    public string AdminPasswordHash { get; set; } = "";

    /// <summary>Legacy plaintext PIN from earlier builds; migrated on first save.</summary>
    public string LegacyAdminPin { get; set; } = "";

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

    /// <summary>How totals are displayed: minutes (8:30) or hundredths (8.50).</summary>
    public TotalsFormat TotalsFormat { get; set; } = TotalsFormat.Minutes;

    /// <summary>Ask for a comment when a punch is added manually (old timeQplus option).</summary>
    public bool ShowPunchCommentDialog { get; set; } = true;

    /// <summary>All hours on a 7th consecutive worked day become overtime.</summary>
    public bool EnableSeventhDayOvertime { get; set; }

    /// <summary>
    /// True: the clock runs full screen, always on top, and cannot be closed without the
    /// supervisor password (dedicated clock PC). False: a normal resizable window that can
    /// sit alongside other programs (shared PC).
    /// </summary>
    public bool ClockFullScreen { get; set; }

    /// <summary>Light, dark, or follow Windows.</summary>
    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>Window size used when the clock is not full screen.</summary>
    public int ClockWidth { get; set; } = 1000;
    public int ClockHeight { get; set; } = 720;

    public string TimeFormat { get; set; } = "h:mm tt";
    public string DateFormat { get; set; } = "M/d/yyyy";

    public string LastBackupPath { get; set; } = "";
    public int SchemaVersion { get; set; } = 1;
}

using PunchPoint.Core.Models;

namespace PunchPoint.Core.Import;

public enum PunchDirectionMode
{
    /// <summary>Try to detect the stored IN/OUT encoding, otherwise alternate.</summary>
    Auto = 0,
    /// <summary>Ignore stored codes: the first punch of a run is IN, then alternate.</summary>
    Alternate = 1,
    /// <summary>Use the legacy TransState column (0 = IN, 1 = OUT).</summary>
    UseState = 2,
}

public sealed class ImportOptions
{
    public bool ImportEmployees { get; set; } = true;
    public bool ImportPunches { get; set; } = true;
    public bool ImportShifts { get; set; } = true;
    public bool ImportPayPeriods { get; set; } = true;
    public bool ImportSettings { get; set; } = true;
    public bool ImportHolidays { get; set; } = true;
    public bool ImportApprovals { get; set; } = true;

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    public PunchDirectionMode DirectionMode { get; set; } = PunchDirectionMode.Auto;

    /// <summary>Employees with no punch in the last N days are imported as inactive. 0 = keep all active.</summary>
    public int InactiveAfterDaysWithoutPunch { get; set; }

    /// <summary>When true, an existing employee with the same legacy id is updated instead of duplicated.</summary>
    public bool MatchExistingEmployees { get; set; } = true;

    /// <summary>
    /// Import transactions that carry an hours value (vacation, holiday, sick) as paid hours.
    /// Off by default: only enable after checking the legacy data, because a value in
    /// TransHours can also describe worked time.
    /// </summary>
    public bool ImportMiscHours { get; set; }
}

public sealed class EmployeeImportRow
{
    public required Employee Employee { get; init; }
    public int PunchCount { get; set; }
    public DateTime? FirstPunch { get; set; }
    public DateTime? LastPunch { get; set; }
    public int OpenShifts { get; set; }
    public int OddPunchDays { get; set; }
    public List<string> Notes { get; } = new();
}

public sealed class ImportPreview
{
    public required string SourcePath { get; init; }
    public int EmployeeCount { get; set; }
    public int TransactionCount { get; set; }
    public int ShiftCount { get; set; }
    public int PayPeriodCount { get; set; }
    public int HolidayCount { get; set; }
    public int MiscHourTransactions { get; set; }
    public double MiscHoursTotal { get; set; }
    public DateTime? FirstPunch { get; set; }
    public DateTime? LastPunch { get; set; }
    public string DetectedDirection { get; set; } = "";
    public int DirectionConfidence { get; set; }
    public List<EmployeeImportRow> Employees { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> TablesFound { get; } = new();
    public List<string> TablesMissing { get; } = new();
}

public sealed class ImportResult
{
    public int EmployeesAdded { get; set; }
    public int EmployeesUpdated { get; set; }
    public int ShiftsAdded { get; set; }
    public int PayPeriodsAdded { get; set; }
    public int PunchesAdded { get; set; }
    public int PunchesSkipped { get; set; }
    public int HolidaysAdded { get; set; }
    public int ApprovalsImported { get; set; }
    public int UnmatchedPunches { get; set; }
    public List<string> Warnings { get; } = new();
    public List<string> Errors { get; } = new();
    public TimeSpan Duration { get; set; }

    public bool Success => Errors.Count == 0;

    public string Summary =>
        $"{EmployeesAdded} employees, {PunchesAdded} punches" +
        (EmployeesUpdated > 0 ? $", {EmployeesUpdated} updated" : "") +
        (PunchesSkipped > 0 ? $", {PunchesSkipped} skipped" : "") +
        (UnmatchedPunches > 0 ? $", {UnmatchedPunches} unmatched" : "");
}

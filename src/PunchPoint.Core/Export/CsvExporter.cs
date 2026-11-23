using System.Globalization;
using System.Text;
using PunchPoint.Core.Models;
using PunchPoint.Core.Storage;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.Core.Export;

public sealed class ExportOptions
{
    public string FieldSeparator { get; set; } = ",";
    public string LineTerminator { get; set; } = "\r\n";
    public bool QuoteFields { get; set; } = true;
    public bool IncludeHeader { get; set; } = true;
    public bool IncludeEmployeeInfo { get; set; } = true;
    /// <summary>Write hours as decimal (8.50) instead of h:mm.</summary>
    public bool DecimalHours { get; set; } = true;
    /// <summary>Include only approved time cards.</summary>
    public bool ApprovedOnly { get; set; }
}

public sealed class TimeCardExportRow
{
    public required Employee Employee { get; init; }
    public required PayPeriod Period { get; init; }
    public required TimeCardResult Result { get; init; }
    public TimeCard? Card { get; init; }
    public bool Approved => Card?.Approved ?? false;

    public string FormatHours(int minutes, bool decimalHours) =>
        decimalHours
            ? (minutes / 60.0).ToString("0.00", CultureInfo.InvariantCulture)
            : $"{(minutes < 0 ? "-" : "")}{Math.Abs(minutes) / 60}:{Math.Abs(minutes) % 60:00}";
}

/// <summary>CSV / delimited exports for payroll and spreadsheets.</summary>
public static class CsvExporter
{
    public static List<TimeCardExportRow> BuildRows(Store store, PayPeriod period, AppSettings settings,
        IEnumerable<Employee>? employees = null)
    {
        var holidays = store.ListHolidays();
        var rows = new List<TimeCardExportRow>();

        foreach (var employee in employees ?? store.ListEmployees(includeInactive: true))
        {
            var punches = store.ListPunches(employee.Id, period.Start, period.End.AddDays(1));
            if (punches.Count == 0) continue;

            var shift = employee.ShiftId is int id ? store.GetShift(id) : null;
            var card = store.GetTimeCard(employee.Id, period.Id);
            var result = TimeCardCalculator.Calculate(employee, shift, period, punches, settings, holidays, card);

            rows.Add(new TimeCardExportRow { Employee = employee, Period = period, Result = result, Card = card });
        }

        return rows;
    }

    /// <summary>Payroll summary: one row per employee with totals.</summary>
    public static void WritePayrollSummary(IEnumerable<TimeCardExportRow> rows, string path, ExportOptions? options = null)
    {
        options ??= new ExportOptions();
        var sb = new StringBuilder();

        void Line(params string[] fields) => sb.Append(string.Join(options.FieldSeparator, fields.Select(f => Field(f, options)))).Append(options.LineTerminator);

        if (options.IncludeHeader)
        {
            var headers = new List<string>();
            if (options.IncludeEmployeeInfo)
                headers.AddRange(new[] { "Employee Number", "Last Name", "First Name", "Badge", "PIN", "Pay Type" });
            headers.AddRange(new[] { "Pay Period Start", "Pay Period End", "Regular", "Overtime 1", "Overtime 2", "Holiday", "Total", "Approved" });
            Line(headers.ToArray());
        }

        foreach (var row in rows)
        {
            if (options.ApprovedOnly && !row.Approved) continue;

            var fields = new List<string>();
            if (options.IncludeEmployeeInfo)
            {
                fields.Add(row.Employee.EmployeeNumber);
                fields.Add(row.Employee.LastName);
                fields.Add(row.Employee.FirstName);
                fields.Add(row.Employee.Badge);
                fields.Add(row.Employee.Pin);
                fields.Add(row.Employee.PayType == PayType.Salary ? "Salary" : "Hourly");
            }

            fields.Add(row.Period.Start.ToString("yyyy-MM-dd"));
            fields.Add(row.Period.End.ToString("yyyy-MM-dd"));
            fields.Add(row.FormatHours(row.Result.RegularMinutes, options.DecimalHours));
            fields.Add(row.FormatHours(row.Result.OverTime1Minutes, options.DecimalHours));
            fields.Add(row.FormatHours(row.Result.OverTime2Minutes, options.DecimalHours));
            fields.Add(row.FormatHours(row.Result.HolidayMinutes, options.DecimalHours));
            fields.Add(row.FormatHours(row.Result.TotalMinutes, options.DecimalHours));
            fields.Add(row.Approved ? "Yes" : "No");
            Line(fields.ToArray());
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    /// <summary>Daily detail: one row per employee per day.</summary>
    public static void WriteDailyDetail(IEnumerable<TimeCardExportRow> rows, string path, ExportOptions? options = null)
    {
        options ??= new ExportOptions();
        var sb = new StringBuilder();

        void Line(params string[] fields) => sb.Append(string.Join(options.FieldSeparator, fields.Select(f => Field(f, options)))).Append(options.LineTerminator);

        if (options.IncludeHeader)
            Line("Employee", "Date", "Day", "In", "Out", "Break (min)", "Regular", "OT1", "OT2", "Holiday", "Total", "Notes");

        foreach (var row in rows)
        {
            if (options.ApprovedOnly && !row.Approved) continue;

            foreach (var day in row.Result.Days)
            {
                if (day.TotalMinutes == 0 && day.Issues.Count == 0) continue;

                string inLabel = day.Segments.Count > 0 ? day.Segments[0].PaidIn.ToString("h:mm tt") : "";
                string outLabel = day.Segments.LastOrDefault(s => s.PaidOut is not null)?.PaidOut?.ToString("h:mm tt") ?? "";

                Line(
                    row.Employee.SortName,
                    day.WorkDate.ToString("yyyy-MM-dd"),
                    day.WorkDate.DayOfWeek.ToString(),
                    inLabel,
                    outLabel,
                    day.BreakMinutes.ToString(CultureInfo.InvariantCulture),
                    row.FormatHours(day.RegularMinutes, options.DecimalHours),
                    row.FormatHours(day.OverTime1Minutes, options.DecimalHours),
                    row.FormatHours(day.OverTime2Minutes, options.DecimalHours),
                    row.FormatHours(day.HolidayMinutes, options.DecimalHours),
                    row.FormatHours(day.TotalMinutes, options.DecimalHours),
                    string.Join(" | ", day.Issues));
            }
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    /// <summary>Raw punches for auditing or moving to another system.</summary>
    public static void WritePunches(Store store, DateTime from, DateTime to, string path, ExportOptions? options = null)
    {
        options ??= new ExportOptions();
        var employees = store.ListEmployees(includeInactive: true).ToDictionary(e => e.Id);
        var punches = store.ListPunches(from, to);
        var sb = new StringBuilder();

        void Line(params string[] fields) => sb.Append(string.Join(options.FieldSeparator, fields.Select(f => Field(f, options)))).Append(options.LineTerminator);

        if (options.IncludeHeader)
            Line("Employee", "Badge", "Date", "Time", "Type", "Source", "Hours", "Category", "Comment");

        foreach (var punch in punches)
        {
            var employee = employees.GetValueOrDefault(punch.EmployeeId);
            Line(
                employee?.SortName ?? punch.EmployeeId.ToString(),
                employee?.Badge ?? "",
                punch.Timestamp.ToString("yyyy-MM-dd"),
                punch.Timestamp.ToString("HH:mm:ss"),
                punch.Kind.ToString(),
                punch.Source.ToString(),
                punch.Hours > TimeSpan.Zero ? punch.Hours.TotalHours.ToString("0.00", CultureInfo.InvariantCulture) : "",
                punch.HoursCategory,
                punch.Comment.Replace("\r", " ").Replace("\n", " "));
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    private static string Field(string? value, ExportOptions options)
    {
        value ??= "";
        if (options.QuoteFields)
            return "\"" + value.Replace("\"", "\"\"") + "\"";

        if (value.Contains(options.FieldSeparator) || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}

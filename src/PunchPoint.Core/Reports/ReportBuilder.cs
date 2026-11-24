using PunchPoint.Core.Models;
using PunchPoint.Core.Storage;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.Core.Reports;

public sealed class ReportTable
{
    public required string Title { get; init; }
    public string Subtitle { get; init; } = "";
    public List<string> Columns { get; } = new();
    public List<List<string>> Rows { get; } = new();

    public void AddRow(params string[] cells) => Rows.Add(cells.ToList());

    public void AddColumn(string title) => Columns.Add(title);
}

/// <summary>
/// The report set offered by the Reports screen, mirroring the list in the original
/// timeQplus software (Employee Profiles, Rules Setup, Shifts, Time Card, Holidays,
/// Hours Summary, Raw Transactions, Time Card Approvals, Daily Totals).
/// </summary>
public static class ReportBuilder
{
    public static string Hours(int minutes, TotalsFormat format) =>
        format == TotalsFormat.Minutes
            ? $"{(minutes < 0 ? "-" : "")}{Math.Abs(minutes) / 60}:{Math.Abs(minutes) % 60:00}"
            : (minutes / 60.0).ToString("0.00");

    public static ReportTable TimeCard(Store store, Employee employee, PayPeriod period, AppSettings settings)
    {
        var punches = store.ListPunches(employee.Id, period.Start, period.End.AddDays(1));
        var shift = employee.ShiftId is int id ? store.GetShift(id) : null;
        var card = store.GetTimeCard(employee.Id, period.Id);
        var result = TimeCardCalculator.Calculate(employee, shift, period, punches, settings,
            store.ListHolidays(), card);

        var table = new ReportTable
        {
            Title = $"Time Card — {employee.SortName}",
            Subtitle = $"{period.Start:MMM d, yyyy} – {period.End:MMM d, yyyy}   ·   {settings.CompanyName}",
        };

        table.AddColumn("Date");
        table.AddColumn("Day");
        table.AddColumn("IN");
        table.AddColumn("OUT");
        table.AddColumn("Break");
        table.AddColumn("Regular");
        table.AddColumn("OT1");
        table.AddColumn("OT2");
        table.AddColumn("Holiday");
        table.AddColumn("Total");
        table.AddColumn("Notes");

        foreach (var day in result.Days)
        {
            table.AddRow(
                day.WorkDate.ToString("MM/dd/yyyy"),
                day.WorkDate.DayOfWeek.ToString(),
                day.FirstInLabel,
                day.LastOutLabel,
                day.BreakMinutes > 0 ? day.BreakMinutes + "m" : "",
                Hours(day.RegularMinutes, settings.TotalsFormat),
                Hours(day.OverTime1Minutes, settings.TotalsFormat),
                Hours(day.OverTime2Minutes, settings.TotalsFormat),
                Hours(day.HolidayMinutes, settings.TotalsFormat),
                Hours(day.TotalMinutes, settings.TotalsFormat),
                string.Join("; ", day.Issues));
        }

        table.AddRow("", "", "", "", "",
            Hours(result.RegularMinutes, settings.TotalsFormat),
            Hours(result.OverTime1Minutes, settings.TotalsFormat),
            Hours(result.OverTime2Minutes, settings.TotalsFormat),
            Hours(result.HolidayMinutes, settings.TotalsFormat),
            Hours(result.TotalMinutes, settings.TotalsFormat),
            card?.Approved == true ? "APPROVED" : "");

        return table;
    }

    public static ReportTable HoursSummary(Store store, PayPeriod period, AppSettings settings,
        IReadOnlyList<Employee> employees)
    {
        var table = new ReportTable
        {
            Title = "Hours Summary",
            Subtitle = $"{period.Start:MMM d} – {period.End:MMM d, yyyy}",
        };

        foreach (string column in new[] { "Employee", "Number", "Regular", "OT1", "OT2", "Holiday", "Total", "Approved" })
            table.AddColumn(column);

        int totalReg = 0, totalOt1 = 0, totalOt2 = 0, totalHol = 0, totalAll = 0;

        foreach (var employee in employees.OrderBy(e => e.SortName))
        {
            var punches = store.ListPunches(employee.Id, period.Start, period.End.AddDays(1));
            if (punches.Count == 0) continue;

            var shift = employee.ShiftId is int id ? store.GetShift(id) : null;
            var card = store.GetTimeCard(employee.Id, period.Id);
            var result = TimeCardCalculator.Calculate(employee, shift, period, punches, settings,
                store.ListHolidays(), card);

            totalReg += result.RegularMinutes;
            totalOt1 += result.OverTime1Minutes;
            totalOt2 += result.OverTime2Minutes;
            totalHol += result.HolidayMinutes;
            totalAll += result.TotalMinutes;

            table.AddRow(
                employee.SortName,
                employee.EmployeeNumber,
                Hours(result.RegularMinutes, settings.TotalsFormat),
                Hours(result.OverTime1Minutes, settings.TotalsFormat),
                Hours(result.OverTime2Minutes, settings.TotalsFormat),
                Hours(result.HolidayMinutes, settings.TotalsFormat),
                Hours(result.TotalMinutes, settings.TotalsFormat),
                card?.Approved == true ? "Yes" : "");
        }

        table.AddRow("TOTAL", "",
            Hours(totalReg, settings.TotalsFormat),
            Hours(totalOt1, settings.TotalsFormat),
            Hours(totalOt2, settings.TotalsFormat),
            Hours(totalHol, settings.TotalsFormat),
            Hours(totalAll, settings.TotalsFormat), "");

        return table;
    }

    /// <summary>
    /// Punctuality per employee: work days, days late, minutes late, early departures and days
    /// with no punches at all. Uses each employee's assigned shift; anyone without one is
    /// measured against 08:00–17:00 and marked in the Shift column.
    /// </summary>
    public static ReportTable Attendance(Store store, PayPeriod period, AppSettings settings,
        IReadOnlyList<Employee> employees)
    {
        var table = new ReportTable
        {
            Title = "Attendance",
            Subtitle = $"{period.Start:MMM d} – {period.End:MMM d, yyyy}",
        };

        foreach (string column in new[]
                 {
                     "Employee", "Shift", "Work days", "Late", "Late time",
                     "Left early", "Left early time", "Absent",
                 })
            table.AddColumn(column);

        var holidays = store.ListHolidays().Select(h => h.Date.Date).ToHashSet();
        // Days still to come are not absences: only count the part of the period that has happened.
        var lastDay = period.End.Date < DateTime.Today ? period.End.Date : DateTime.Today;
        var defaultShift = new Shift { Name = "08:00–17:00", Start = new TimeSpan(8, 0, 0), Stop = new TimeSpan(17, 0, 0) };

        int totalWorkDays = 0, totalLate = 0, totalLateMinutes = 0,
            totalEarly = 0, totalEarlyMinutes = 0, totalAbsent = 0;

        foreach (var employee in employees.OrderBy(e => e.SortName))
        {
            var shift = (employee.ShiftId is int id ? store.GetShift(id) : null) ?? defaultShift;

            // Without a shift there is no schedule to be late for. Counting absences is still
            // useful; inventing a start time would produce numbers that look authoritative and
            // are wrong.
            bool measured = employee.ShiftId is not null && store.GetShift(employee.ShiftId.Value) is not null;

            var punches = store.ListPunches(employee.Id, period.Start, period.End.AddDays(1));
            var byDay = punches
                .Where(p => p.Kind is PunchKind.In or PunchKind.Out)
                .GroupBy(p => TimeCardCalculator.WorkDate(p.Timestamp, settings.DayChangeOffsetMinutes))
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Timestamp).ToList());

            int workDays = 0, late = 0, lateMinutes = 0, early = 0, earlyMinutes = 0, absent = 0;

            for (var day = period.Start.Date; day <= lastDay; day = day.AddDays(1))
            {
                if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                if (holidays.Contains(day)) continue;
                if (employee.HireDate is DateTime hired && day < hired.Date) continue;

                workDays++;

                if (!byDay.TryGetValue(day, out var dayPunches) || dayPunches.Count == 0)
                {
                    absent++;
                    continue;
                }

                var firstIn = dayPunches.FirstOrDefault(p => p.Kind == PunchKind.In)?.Timestamp;
                var lastOut = dayPunches.LastOrDefault(p => p.Kind == PunchKind.Out)?.Timestamp;

                var expectedStart = day.Add(shift.Start).AddMinutes(shift.StartGraceMinutes);
                var expectedStop = day.Add(shift.Stop).AddMinutes(-shift.StopGraceMinutes);
                if (shift.Stop <= shift.Start) expectedStop = expectedStop.AddDays(1);

                if (measured && firstIn is DateTime clockIn && clockIn > expectedStart)
                {
                    late++;
                    lateMinutes += (int)(clockIn - expectedStart).TotalMinutes;
                }

                if (measured && lastOut is DateTime clockOut && clockOut < expectedStop)
                {
                    early++;
                    earlyMinutes += (int)(expectedStop - clockOut).TotalMinutes;
                }
            }

            totalWorkDays += workDays;
            totalLate += late;
            totalLateMinutes += lateMinutes;
            totalEarly += early;
            totalEarlyMinutes += earlyMinutes;
            totalAbsent += absent;

            table.AddRow(
                employee.SortName,
                measured ? shift.Name : "no shift — not measured",
                workDays.ToString(),
                measured ? late.ToString() : "—",
                measured && late > 0 ? Hours(lateMinutes, settings.TotalsFormat) : "",
                measured ? early.ToString() : "—",
                measured && early > 0 ? Hours(earlyMinutes, settings.TotalsFormat) : "",
                absent.ToString());
        }

        table.AddRow("TOTAL", "", totalWorkDays.ToString(), totalLate.ToString(),
            Hours(totalLateMinutes, settings.TotalsFormat), totalEarly.ToString(),
            Hours(totalEarlyMinutes, settings.TotalsFormat), totalAbsent.ToString());

        return table;
    }

    public static ReportTable DailyTotals(Store store, PayPeriod period, AppSettings settings,
        IReadOnlyList<Employee> employees, bool byEmployee)
    {
        var table = new ReportTable
        {
            Title = byEmployee ? "Daily Totals By Employee" : "Daily Totals By Day",
            Subtitle = $"{period.Start:MMM d} – {period.End:MMM d, yyyy}",
        };

        table.AddColumn("Date");
        table.AddColumn("Employee");
        table.AddColumn("Regular");
        table.AddColumn("OT1");
        table.AddColumn("OT2");
        table.AddColumn("Holiday");
        table.AddColumn("Total");

        var rows = new List<(DateTime Date, string Name, TimeCardResult Result)>();

        foreach (var employee in employees.OrderBy(e => e.SortName))
        {
            var punches = store.ListPunches(employee.Id, period.Start, period.End.AddDays(1));
            if (punches.Count == 0) continue;

            var shift = employee.ShiftId is int id ? store.GetShift(id) : null;
            var result = TimeCardCalculator.Calculate(employee, shift, period, punches, settings,
                store.ListHolidays(), store.GetTimeCard(employee.Id, period.Id));

            foreach (var day in result.Days)
                rows.Add((day.WorkDate, employee.SortName, new TimeCardResult
                {
                    Employee = employee,
                    PayPeriod = period,
                    RegularMinutes = day.RegularMinutes,
                    OverTime1Minutes = day.OverTime1Minutes,
                    OverTime2Minutes = day.OverTime2Minutes,
                    HolidayMinutes = day.HolidayMinutes,
                }));
        }

        var ordered = byEmployee
            ? rows.OrderBy(r => r.Name).ThenBy(r => r.Date)
            : rows.OrderBy(r => r.Date).ThenBy(r => r.Name);

        foreach (var row in ordered)
        {
            table.AddRow(
                row.Date.ToString("MM/dd/yyyy"),
                row.Name,
                Hours(row.Result.RegularMinutes, settings.TotalsFormat),
                Hours(row.Result.OverTime1Minutes, settings.TotalsFormat),
                Hours(row.Result.OverTime2Minutes, settings.TotalsFormat),
                Hours(row.Result.HolidayMinutes, settings.TotalsFormat),
                Hours(row.Result.TotalMinutes, settings.TotalsFormat));
        }

        return table;
    }

    public static ReportTable RawTransactions(Store store, PayPeriod period)
    {
        var table = new ReportTable
        {
            Title = "Raw Transactions",
            Subtitle = $"{period.Start:MMM d} – {period.End:MMM d, yyyy}",
        };

        foreach (string column in new[] { "Date", "Time", "Employee", "Punch", "Source", "Comment" })
            table.AddColumn(column);

        var employees = store.ListEmployees(includeInactive: true).ToDictionary(e => e.Id);
        foreach (var punch in store.ListPunches(period.Start, period.End.AddDays(1)))
        {
            table.AddRow(
                punch.Timestamp.ToString("MM/dd/yyyy"),
                punch.Timestamp.ToString("h:mm:ss tt"),
                employees.GetValueOrDefault(punch.EmployeeId)?.SortName ?? punch.EmployeeId.ToString(),
                punch.Kind.ToString(),
                punch.Source.ToString(),
                punch.Comment);
        }

        return table;
    }

    public static ReportTable EmployeeProfiles(Store store)
    {
        var table = new ReportTable { Title = "Employee Profiles" };
        foreach (string column in new[] { "Number", "Last Name", "First Name", "MI", "Badge", "PIN", "Shift", "Pay Type", "Hire Date", "Status" })
            table.AddColumn(column);

        var shifts = store.ListShifts().ToDictionary(s => s.Id);
        foreach (var employee in store.ListEmployees(includeInactive: true).OrderBy(e => e.SortName))
        {
            table.AddRow(
                employee.EmployeeNumber,
                employee.LastName,
                employee.FirstName,
                employee.MiddleInitial,
                employee.Badge,
                employee.Pin,
                employee.ShiftId is int id && shifts.TryGetValue(id, out var shift) ? shift.Name : "",
                employee.PayType.ToString(),
                employee.HireDate?.ToString("MM/dd/yyyy") ?? "",
                employee.IsActive ? "Active" : "Inactive");
        }

        return table;
    }

    public static ReportTable Rules(Store store)
    {
        var s = store.GetSettings();
        var table = new ReportTable { Title = "Rules Setup", Subtitle = s.CompanyName };
        table.AddColumn("Rule");
        table.AddColumn("Value");

        void Row(string name, string value) => table.AddRow(name, value);

        Row("Pay period", s.PeriodType.ToString());
        Row("Pay interval rounding", s.RoundingMinutes == 0 ? "None" : $"{s.RoundingMinutes} minutes ({s.RoundInMode}/{s.RoundOutMode})");
        Row("Daily overtime level 1", s.DailyOverTime1Minutes == 0 ? "Off" : Hours(s.DailyOverTime1Minutes, TotalsFormat.Minutes));
        Row("Daily overtime level 2", s.DailyOverTime2Minutes == 0 ? "Off" : Hours(s.DailyOverTime2Minutes, TotalsFormat.Minutes));
        Row("Weekly overtime level 1", s.WeeklyOverTime1Minutes == 0 ? "Off" : Hours(s.WeeklyOverTime1Minutes, TotalsFormat.Minutes));
        Row("Weekly overtime level 2", s.WeeklyOverTime2Minutes == 0 ? "Off" : Hours(s.WeeklyOverTime2Minutes, TotalsFormat.Minutes));
        Row("7th day overtime", s.EnableSeventhDayOvertime ? "Yes" : "No");
        Row("Day change offset", Hours(s.DayChangeOffsetMinutes, TotalsFormat.Minutes));
        Row("Maximum time on clock", Hours(s.MaxPunchSpanMinutes, TotalsFormat.Minutes));
        Row("Require approval before export", s.RequireApprovalBeforeExport ? "Yes" : "No");
        Row("Totals format", s.TotalsFormat.ToString());

        return table;
    }

    public static ReportTable Shifts(Store store)
    {
        var table = new ReportTable { Title = "Shifts" };
        foreach (string column in new[] { "Shift", "Start", "Stop", "Start Grace", "Stop Grace", "Lunch", "Holiday Hours", "Enabled" })
            table.AddColumn(column);

        foreach (var shift in store.ListShifts())
        {
            table.AddRow(
                shift.Name,
                Shift.FormatTime(shift.Start),
                Shift.FormatTime(shift.Stop),
                $"{shift.StartGraceMinutes}m",
                $"{shift.StopGraceMinutes}m",
                shift.LunchEnabled
                    ? (shift.LunchAutoAfterMinutes > 0 ? $"auto {shift.LunchDurationMinutes}m after {shift.LunchAutoAfterMinutes / 60.0:0.#}h" : "punched")
                    : "none",
                shift.HolidaysPaid ? $"{shift.HolidayHours}h" : "",
                shift.Enabled ? "Yes" : "No");
        }

        return table;
    }

    public static ReportTable Holidays(Store store)
    {
        var table = new ReportTable { Title = "Holidays" };
        foreach (string column in new[] { "Holiday", "Date", "Day", "Paid" })
            table.AddColumn(column);

        foreach (var holiday in store.ListHolidays())
            table.AddRow(holiday.Name, holiday.Date.ToString("MM/dd/yyyy"), holiday.Date.DayOfWeek.ToString(),
                holiday.Paid ? "Yes" : "No");

        return table;
    }

    public static ReportTable Unapproved(Store store, PayPeriod period, AppSettings settings,
        IReadOnlyList<Employee> employees)
    {
        var table = new ReportTable
        {
            Title = "Unapproved Time Cards",
            Subtitle = $"{period.Start:MMM d} – {period.End:MMM d, yyyy}",
        };

        foreach (string column in new[] { "Employee", "Regular", "OT1", "OT2", "Holiday", "Total" })
            table.AddColumn(column);

        foreach (var employee in employees.OrderBy(e => e.SortName))
        {
            var punches = store.ListPunches(employee.Id, period.Start, period.End.AddDays(1));
            if (punches.Count == 0) continue;

            var card = store.GetTimeCard(employee.Id, period.Id);
            if (card?.Approved == true) continue;

            var shift = employee.ShiftId is int id ? store.GetShift(id) : null;
            var result = TimeCardCalculator.Calculate(employee, shift, period, punches, settings,
                store.ListHolidays(), card);

            table.AddRow(
                employee.SortName,
                Hours(result.RegularMinutes, settings.TotalsFormat),
                Hours(result.OverTime1Minutes, settings.TotalsFormat),
                Hours(result.OverTime2Minutes, settings.TotalsFormat),
                Hours(result.HolidayMinutes, settings.TotalsFormat),
                Hours(result.TotalMinutes, settings.TotalsFormat));
        }

        return table;
    }
}

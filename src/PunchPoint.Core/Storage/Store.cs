using System.Globalization;
using Microsoft.Data.Sqlite;
using PunchPoint.Core.Models;

namespace PunchPoint.Core.Storage;

/// <summary>All persistence operations for PunchPoint. SQLite-backed, no external drivers.</summary>
public sealed class Store
{
    public const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";
    public const string DateFormat = "yyyy-MM-dd";

    private readonly Database _db;

    public Store(Database db)
    {
        _db = db;
    }

    public Database Db => _db;

    public static Store OpenDefault() => new(Database.OpenDefault());

    public static string Format(DateTime value) => value.ToString(TimestampFormat, CultureInfo.InvariantCulture);
    public static string FormatDate(DateTime value) => value.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static DateTime ParseTimestamp(object? value) => value switch
    {
        null or DBNull => DateTime.MinValue,
        DateTime dt => dt,
        string s => DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.None),
        _ => Convert.ToDateTime(value, CultureInfo.InvariantCulture),
    };

    private static object DateOrNull(DateTime? value) =>
        value is null ? DBNull.Value : value.Value.ToString(DateFormat, CultureInfo.InvariantCulture);

    private static object TimestampOrNull(DateTime? value) =>
        value is null ? DBNull.Value : value.Value.ToString(TimestampFormat, CultureInfo.InvariantCulture);

    // =====================================================================
    // Settings
    // =====================================================================

    public AppSettings GetSettings()
    {
        var rows = _db.Query("SELECT * FROM settings WHERE id = 1;", r => new AppSettings
        {
            CompanyName = r.GetStringOrNull("company_name") ?? "My Company",
            CompanyMessage = r.GetStringOrNull("company_message") ?? "",
            LegacyAdminPin = r.GetStringOrNull("admin_pin") ?? "",
            AdminPasswordHash = r.HasColumn("admin_password") ? r.GetStringOrNull("admin_password") ?? "" : "",
            PeriodType = (PayPeriodType)r.GetInt("period_type"),
            PeriodAnchor = DateTime.Parse(r.GetStringOrNull("period_anchor") ?? "2000-01-01", CultureInfo.InvariantCulture),
            SemiMonthlySplitDay = r.GetInt("semi_monthly_split", 16),
            MonthlyStartDay = r.GetInt("monthly_start_day", 1),
            RoundingMinutes = r.GetInt("rounding_minutes"),
            RoundInMode = (RoundingMode)r.GetInt("round_in_mode", 1),
            RoundOutMode = (RoundingMode)r.GetInt("round_out_mode", 1),
            DailyOverTime1Minutes = r.GetInt("daily_ot1"),
            DailyOverTime2Minutes = r.GetInt("daily_ot2"),
            WeeklyOverTime1Minutes = r.GetInt("weekly_ot1"),
            WeeklyOverTime2Minutes = r.GetInt("weekly_ot2"),
            DayChangeOffsetMinutes = r.GetInt("day_change_offset"),
            MaxPunchSpanMinutes = r.GetInt("max_punch_span", 960),
            RequireApprovalBeforeExport = r.GetBool("require_approval"),
            RoundDailyTotalInsteadOfPunches = r.GetBool("round_daily_total"),
            TotalsFormat = r.HasColumn("totals_format") ? (TotalsFormat)r.GetInt("totals_format", 0) : TotalsFormat.Minutes,
            ShowPunchCommentDialog = !r.HasColumn("show_punch_comment") || r.GetBool("show_punch_comment", true),
            EnableSeventhDayOvertime = r.HasColumn("enable_7th_day_ot") && r.GetBool("enable_7th_day_ot"),
            Theme = r.HasColumn("app_theme") ? (AppTheme)r.GetInt("app_theme", 0) : AppTheme.System,
            ClockFullScreen = r.HasColumn("clock_fullscreen") && r.GetBool("clock_fullscreen"),
            ClockWidth = r.HasColumn("clock_width") ? Math.Clamp(r.GetInt("clock_width", 1000), 800, 4000) : 1000,
            ClockHeight = r.HasColumn("clock_height") ? Math.Clamp(r.GetInt("clock_height", 720), 600, 3000) : 720,
            TimeFormat = r.GetStringOrNull("time_format") ?? "h:mm tt",
            DateFormat = r.GetStringOrNull("date_format") ?? "M/d/yyyy",
            SmtpHost = r.HasColumn("smtp_host") ? r.GetStringOrNull("smtp_host") ?? "" : "",
            SmtpPort = r.HasColumn("smtp_port") ? r.GetInt("smtp_port", 587) : 587,
            SmtpUseSsl = !r.HasColumn("smtp_ssl") || r.GetBool("smtp_ssl", true),
            SmtpUser = r.HasColumn("smtp_user") ? r.GetStringOrNull("smtp_user") ?? "" : "",
            SmtpPassword = r.HasColumn("smtp_password") ? r.GetStringOrNull("smtp_password") ?? "" : "",
            SmtpFrom = r.HasColumn("smtp_from") ? r.GetStringOrNull("smtp_from") ?? "" : "",
            ReportEmail = r.HasColumn("report_email") ? r.GetStringOrNull("report_email") ?? "" : "",
            EmailPeriodSummary = r.HasColumn("email_period") && r.GetBool("email_period"),
            EmailEmployees = r.HasColumn("email_employees") && r.GetBool("email_employees"),
            EncryptSecrets = r.HasColumn("encrypt_secrets") && r.GetBool("encrypt_secrets"),
            WebAccessEnabled = r.HasColumn("web_access") && r.GetBool("web_access"),
            WebAccessPort = r.HasColumn("web_port") ? r.GetInt("web_port", 8080) : 8080,
            WebAccessUser = r.HasColumn("web_user") ? r.GetStringOrNull("web_user") ?? "punchpoint" : "punchpoint",
            WebAccessPassword = r.HasColumn("web_password") ? r.GetStringOrNull("web_password") ?? "" : "",
            WebBindAddress = r.HasColumn("web_bind_ip") ? r.GetStringOrNull("web_bind_ip") ?? "" : "",
            LastBackupPath = r.GetStringOrNull("last_backup_path") ?? "",
        });

        return rows.Count > 0 ? rows[0] : new AppSettings();
    }

    public void SaveSettings(AppSettings s)
    {
        _db.Execute("""
            UPDATE settings SET
                company_name = $company, company_message = $message,
                admin_pin = $legacyPin, admin_password = $adminPassword,
                period_type = $periodType, period_anchor = $anchor,
                semi_monthly_split = $split, monthly_start_day = $monthly,
                rounding_minutes = $round, round_in_mode = $roundIn, round_out_mode = $roundOut,
                daily_ot1 = $dot1, daily_ot2 = $dot2, weekly_ot1 = $wot1, weekly_ot2 = $wot2,
                day_change_offset = $offset, max_punch_span = $span,
                require_approval = $approval, round_daily_total = $dailyTotal,
                time_format = $timeFormat, date_format = $dateFormat,
                totals_format = $totalsFormat, show_punch_comment = $showComment,
                enable_7th_day_ot = $seventhDay,
                app_theme = $theme,
                clock_fullscreen = $clockFull,
                clock_width = $clockWidth,
                clock_height = $clockHeight,
                smtp_host = $smtpHost, smtp_port = $smtpPort, smtp_ssl = $smtpSsl,
                smtp_user = $smtpUser, smtp_password = $smtpPassword, smtp_from = $smtpFrom,
                report_email = $reportEmail, email_period = $emailPeriod,
                email_employees = $emailEmployees,
                encrypt_secrets = $encryptSecrets,
                web_access = $webAccess, web_port = $webPort,
                web_user = $webUser, web_password = $webPassword, web_bind_ip = $webBind,
                last_backup_path = $backup
            WHERE id = 1;
            """,
            ("$company", s.CompanyName), ("$message", s.CompanyMessage),
            ("$legacyPin", s.LegacyAdminPin), ("$adminPassword", s.AdminPasswordHash),
            ("$periodType", (int)s.PeriodType), ("$anchor", FormatDate(s.PeriodAnchor)),
            ("$split", s.SemiMonthlySplitDay), ("$monthly", s.MonthlyStartDay),
            ("$round", s.RoundingMinutes), ("$roundIn", (int)s.RoundInMode), ("$roundOut", (int)s.RoundOutMode),
            ("$dot1", s.DailyOverTime1Minutes), ("$dot2", s.DailyOverTime2Minutes),
            ("$wot1", s.WeeklyOverTime1Minutes), ("$wot2", s.WeeklyOverTime2Minutes),
            ("$offset", s.DayChangeOffsetMinutes), ("$span", s.MaxPunchSpanMinutes),
            ("$approval", s.RequireApprovalBeforeExport ? 1 : 0),
            ("$dailyTotal", s.RoundDailyTotalInsteadOfPunches ? 1 : 0),
            ("$timeFormat", s.TimeFormat), ("$dateFormat", s.DateFormat),
            ("$totalsFormat", (int)s.TotalsFormat),
            ("$showComment", s.ShowPunchCommentDialog ? 1 : 0),
            ("$seventhDay", s.EnableSeventhDayOvertime ? 1 : 0),
            ("$theme", (int)s.Theme),
            ("$clockFull", s.ClockFullScreen ? 1 : 0),
            ("$clockWidth", s.ClockWidth),
            ("$clockHeight", s.ClockHeight),
            ("$smtpHost", s.SmtpHost), ("$smtpPort", s.SmtpPort), ("$smtpSsl", s.SmtpUseSsl ? 1 : 0),
            ("$smtpUser", s.SmtpUser), ("$smtpPassword", s.SmtpPassword), ("$smtpFrom", s.SmtpFrom),
            ("$reportEmail", s.ReportEmail), ("$emailPeriod", s.EmailPeriodSummary ? 1 : 0),
            ("$emailEmployees", s.EmailEmployees ? 1 : 0),
            ("$encryptSecrets", s.EncryptSecrets ? 1 : 0),
            ("$webAccess", s.WebAccessEnabled ? 1 : 0),
            ("$webPort", s.WebAccessPort),
            ("$webUser", s.WebAccessUser),
            ("$webPassword", s.WebAccessPassword),
            ("$webBind", s.WebBindAddress),
            ("$backup", s.LastBackupPath));
    }

    // =====================================================================
    // Employees
    // =====================================================================

    private static Employee MapEmployee(SqliteDataReader r) => new()
    {
        Id = r.GetInt("id"),
        FirstName = r.GetStringOrNull("first_name") ?? "",
        MiddleInitial = r.GetStringOrNull("middle_initial") ?? "",
        LastName = r.GetStringOrNull("last_name") ?? "",
        Badge = r.GetStringOrNull("badge") ?? "",
        Pin = r.GetStringOrNull("pin") ?? "",
        EmployeeNumber = r.GetStringOrNull("employee_number") ?? "",
        ShiftId = r.GetIntOrNull("shift_id"),
        Message = r.GetStringOrNull("message") ?? "",
        HireDate = r.GetDateOrNull("hire_date"),
        ExportId = r.GetStringOrNull("export_id") ?? "",
        Email = r.HasColumn("email") ? r.GetStringOrNull("email") ?? "" : "",
        Status = (EmployeeStatus)r.GetInt("status"),
        PayType = (PayType)r.GetInt("pay_type"),
        PayRate = r.GetDecimalOrNull("pay_rate"),
        Notes = r.GetStringOrNull("notes") ?? "",
        Custom1 = r.GetStringOrNull("custom1") ?? "",
        Custom2 = r.GetStringOrNull("custom2") ?? "",
        Custom3 = r.GetStringOrNull("custom3") ?? "",
        Custom4 = r.GetStringOrNull("custom4") ?? "",
        Custom5 = r.GetStringOrNull("custom5") ?? "",
        LegacyId = r.GetIntOrNull("legacy_id"),
    };

    public List<Employee> ListEmployees(bool includeInactive = false, int? shiftId = null)
    {
        string sql = "SELECT * FROM employees WHERE 1 = 1";
        var args = new List<(string, object?)>();

        if (!includeInactive)
        {
            sql += " AND status = 0";
        }
        if (shiftId is not null)
        {
            sql += " AND shift_id = $shift";
            args.Add(("$shift", shiftId));
        }
        sql += " ORDER BY last_name COLLATE NOCASE, first_name COLLATE NOCASE;";

        return _db.Query(sql, MapEmployee, args.ToArray());
    }

    public Employee? GetEmployee(int id) =>
        _db.Query("SELECT * FROM employees WHERE id = $id;", MapEmployee, ("$id", id)).FirstOrDefault();

    public Employee? FindByPin(string pin)
    {
        if (string.IsNullOrWhiteSpace(pin)) return null;
        string trimmed = pin.Trim();

        var exact = _db.Query("SELECT * FROM employees WHERE pin = $pin AND status = 0;",
            MapEmployee, ("$pin", trimmed)).FirstOrDefault();
        if (exact is not null) return exact;

        // Employees imported from timeQplus often have PINs stored with leading zeros
        // ("0744"). Accept both forms, but only when the match is unambiguous.
        if (!trimmed.All(char.IsDigit)) return null;

        string normalized = trimmed.TrimStart('0');
        if (normalized.Length == 0) normalized = "0";

        var candidates = _db.Query("SELECT * FROM employees WHERE status = 0;", MapEmployee)
            .Where(e => e.Pin.Length > 0 && e.Pin.All(char.IsDigit))
            .Where(e =>
            {
                string p = e.Pin.TrimStart('0');
                return (p.Length == 0 ? "0" : p) == normalized;
            })
            .ToList();

        return candidates.Count == 1 ? candidates[0] : null;
    }

    public Employee? FindByBadge(string badge) =>
        string.IsNullOrWhiteSpace(badge) ? null :
        _db.Query("SELECT * FROM employees WHERE badge = $badge AND status = 0;", MapEmployee, ("$badge", badge.Trim())).FirstOrDefault();

    public Employee? FindByLegacyId(int legacyId) =>
        _db.Query("SELECT * FROM employees WHERE legacy_id = $id;", MapEmployee, ("$id", legacyId)).FirstOrDefault();

    public int InsertEmployee(Employee e)
    {
        string now = Format(DateTime.Now);
        _db.Execute("""
            INSERT INTO employees
                (first_name, middle_initial, last_name, badge, pin, employee_number, shift_id, message,
                 hire_date, export_id, email, status, pay_type, pay_rate, notes,
                 custom1, custom2, custom3, custom4, custom5, legacy_id, created_at, updated_at)
            VALUES
                ($first, $mi, $last, $badge, $pin, $number, $shift, $message,
                 $hire, $export, $email, $status, $payType, $rate, $notes,
                 $c1, $c2, $c3, $c4, $c5, $legacy, $created, $updated);
            """,
            ("$first", e.FirstName), ("$mi", e.MiddleInitial), ("$last", e.LastName),
            ("$badge", e.Badge), ("$pin", e.Pin), ("$number", e.EmployeeNumber),
            ("$shift", e.ShiftId), ("$message", e.Message), ("$hire", DateOrNull(e.HireDate)),
            ("$export", e.ExportId), ("$email", e.Email), ("$status", (int)e.Status), ("$payType", (int)e.PayType),
            ("$rate", e.PayRate), ("$notes", e.Notes),
            ("$c1", e.Custom1), ("$c2", e.Custom2), ("$c3", e.Custom3),
            ("$c4", e.Custom4), ("$c5", e.Custom5),
            ("$legacy", e.LegacyId), ("$created", now), ("$updated", now));

        e.Id = Convert.ToInt32(_db.Scalar("SELECT last_insert_rowid();"));
        return e.Id;
    }

    public void UpdateEmployee(Employee e)
    {
        _db.Execute("""
            UPDATE employees SET
                first_name = $first, middle_initial = $mi, last_name = $last,
                badge = $badge, pin = $pin, employee_number = $number, shift_id = $shift,
                message = $message, hire_date = $hire, export_id = $export, email = $email,
                status = $status, pay_type = $payType, pay_rate = $rate, notes = $notes,
                custom1 = $c1, custom2 = $c2, custom3 = $c3, custom4 = $c4, custom5 = $c5,
                legacy_id = $legacy, updated_at = $updated
            WHERE id = $id;
            """,
            ("$id", e.Id), ("$first", e.FirstName), ("$mi", e.MiddleInitial), ("$last", e.LastName),
            ("$badge", e.Badge), ("$pin", e.Pin), ("$number", e.EmployeeNumber),
            ("$shift", e.ShiftId), ("$message", e.Message), ("$hire", DateOrNull(e.HireDate)),
            ("$export", e.ExportId), ("$email", e.Email), ("$status", (int)e.Status), ("$payType", (int)e.PayType),
            ("$rate", e.PayRate), ("$notes", e.Notes),
            ("$c1", e.Custom1), ("$c2", e.Custom2), ("$c3", e.Custom3),
            ("$c4", e.Custom4), ("$c5", e.Custom5),
            ("$legacy", e.LegacyId), ("$updated", Format(DateTime.Now)));
    }

    /// <summary>Deletes an employee and all of their punches (cascades in SQL).</summary>
    public void DeleteEmployee(int id) => _db.Execute("DELETE FROM employees WHERE id = $id;", ("$id", id));

    // =====================================================================
    // Shifts
    // =====================================================================

    private static Shift MapShift(SqliteDataReader r) => new()
    {
        Id = r.GetInt("id"),
        Name = r.GetStringOrNull("name") ?? "",
        Enabled = r.GetBool("enabled", true),
        Start = TimeSpan.FromMinutes(r.GetInt("start_minutes", 480)),
        Stop = TimeSpan.FromMinutes(r.GetInt("stop_minutes", 1020)),
        StartGraceMinutes = r.GetInt("start_grace"),
        StopGraceMinutes = r.GetInt("stop_grace"),
        DockLateArrival = r.GetBool("dock_late"),
        DockEarlyLeave = r.GetBool("dock_early"),
        LunchEnabled = r.GetBool("lunch_enabled"),
        LunchStart = TimeSpan.FromMinutes(r.GetInt("lunch_start_minutes", 720)),
        LunchEnd = TimeSpan.FromMinutes(r.GetInt("lunch_end_minutes", 750)),
        LunchAutoAfterMinutes = r.GetInt("lunch_auto_after"),
        LunchDurationMinutes = r.GetInt("lunch_duration", 30),
        HolidaysPaid = r.GetBool("holidays_paid"),
        HolidayHours = r.GetInt("holiday_hours"),
        LegacyId = r.GetIntOrNull("legacy_id"),
    };

    public List<Shift> ListShifts() =>
        _db.Query("SELECT * FROM shifts ORDER BY name COLLATE NOCASE;", MapShift);

    public Shift? GetShift(int id) =>
        _db.Query("SELECT * FROM shifts WHERE id = $id;", MapShift, ("$id", id)).FirstOrDefault();

    public Shift? FindShiftByLegacyId(int legacyId) =>
        _db.Query("SELECT * FROM shifts WHERE legacy_id = $id;", MapShift, ("$id", legacyId)).FirstOrDefault();

    public int SaveShift(Shift s)
    {
        var args = new (string, object?)[]
        {
            ("$name", s.Name), ("$enabled", s.Enabled ? 1 : 0),
            ("$start", (int)s.Start.TotalMinutes), ("$stop", (int)s.Stop.TotalMinutes),
            ("$startGrace", s.StartGraceMinutes), ("$stopGrace", s.StopGraceMinutes),
            ("$dockLate", s.DockLateArrival ? 1 : 0), ("$dockEarly", s.DockEarlyLeave ? 1 : 0),
            ("$lunchEnabled", s.LunchEnabled ? 1 : 0),
            ("$lunchStart", (int)s.LunchStart.TotalMinutes), ("$lunchEnd", (int)s.LunchEnd.TotalMinutes),
            ("$lunchAuto", s.LunchAutoAfterMinutes), ("$lunchDuration", s.LunchDurationMinutes),
            ("$holidays", s.HolidaysPaid ? 1 : 0), ("$holidayHours", s.HolidayHours),
            ("$legacy", s.LegacyId),
        };

        if (s.Id == 0)
        {
            _db.Execute("""
                INSERT INTO shifts
                    (name, enabled, start_minutes, stop_minutes, start_grace, stop_grace,
                     dock_late, dock_early, lunch_enabled, lunch_start_minutes, lunch_end_minutes,
                     lunch_auto_after, lunch_duration, holidays_paid, holiday_hours, legacy_id)
                VALUES
                    ($name, $enabled, $start, $stop, $startGrace, $stopGrace,
                     $dockLate, $dockEarly, $lunchEnabled, $lunchStart, $lunchEnd,
                     $lunchAuto, $lunchDuration, $holidays, $holidayHours, $legacy);
                """, args);
            s.Id = Convert.ToInt32(_db.Scalar("SELECT last_insert_rowid();"));
        }
        else
        {
            _db.Execute("""
                UPDATE shifts SET
                    name = $name, enabled = $enabled, start_minutes = $start, stop_minutes = $stop,
                    start_grace = $startGrace, stop_grace = $stopGrace,
                    dock_late = $dockLate, dock_early = $dockEarly,
                    lunch_enabled = $lunchEnabled, lunch_start_minutes = $lunchStart,
                    lunch_end_minutes = $lunchEnd, lunch_auto_after = $lunchAuto,
                    lunch_duration = $lunchDuration, holidays_paid = $holidays,
                    holiday_hours = $holidayHours, legacy_id = $legacy
                WHERE id = $id;
                """, args.Append(("$id", s.Id)).ToArray());
        }
        return s.Id;
    }

    public void DeleteShift(int id) => _db.Execute("DELETE FROM shifts WHERE id = $id;", ("$id", id));

    // =====================================================================
    // Punches
    // =====================================================================

    private static Punch MapPunch(SqliteDataReader r) => new()
    {
        Id = r.GetInt("id"),
        EmployeeId = r.GetInt("employee_id"),
        PayPeriodId = r.GetIntOrNull("pay_period_id"),
        Timestamp = ParseTimestamp(r.GetStringOrNull("timestamp")),
        Kind = (PunchKind)r.GetInt("kind"),
        Source = (PunchSource)r.GetInt("source"),
        Comment = r.GetStringOrNull("comment") ?? "",
        IsVoided = r.GetBool("is_voided"),
        Hours = TimeSpan.FromMinutes(r.GetInt("hours_minutes")),
        HoursCategory = r.GetStringOrNull("hours_category") ?? "",
        LegacyId = r.GetIntOrNull("legacy_id"),
    };

    public List<Punch> ListPunches(int employeeId, DateTime from, DateTime to)
    {
        return _db.Query("""
            SELECT * FROM punches
            WHERE employee_id = $emp AND timestamp >= $from AND timestamp <= $to AND is_voided = 0
            ORDER BY timestamp;
            """, MapPunch,
            ("$emp", employeeId),
            ("$from", Format(from)), ("$to", Format(to)));
    }

    public List<Punch> ListPunches(DateTime from, DateTime to)
    {
        return _db.Query("""
            SELECT * FROM punches
            WHERE timestamp >= $from AND timestamp <= $to AND is_voided = 0
            ORDER BY employee_id, timestamp;
            """, MapPunch,
            ("$from", Format(from)), ("$to", Format(to)));
    }

    public Punch? GetLastPunch(int employeeId)
    {
        return _db.Query("""
            SELECT * FROM punches
            WHERE employee_id = $emp AND is_voided = 0 AND kind IN (0,1,2,3)
            ORDER BY timestamp DESC LIMIT 1;
            """, MapPunch, ("$emp", employeeId)).FirstOrDefault();
    }

    public Punch? GetPunch(int id) =>
        _db.Query("SELECT * FROM punches WHERE id = $id;", MapPunch, ("$id", id)).FirstOrDefault();

    public int InsertPunch(Punch p)
    {
        _db.Execute("""
            INSERT INTO punches
                (employee_id, pay_period_id, timestamp, kind, source, comment,
                 is_voided, hours_minutes, hours_category, legacy_id, created_at)
            VALUES
                ($emp, $period, $time, $kind, $source, $comment,
                 $voided, $hours, $category, $legacy, $created);
            """,
            ("$emp", p.EmployeeId), ("$period", p.PayPeriodId), ("$time", Format(p.Timestamp)),
            ("$kind", (int)p.Kind), ("$source", (int)p.Source), ("$comment", p.Comment),
            ("$voided", p.IsVoided ? 1 : 0), ("$hours", (int)p.Hours.TotalMinutes),
            ("$category", p.HoursCategory), ("$legacy", p.LegacyId),
            ("$created", Format(DateTime.Now)));

        p.Id = Convert.ToInt32(_db.Scalar("SELECT last_insert_rowid();"));
        return p.Id;
    }

    public void UpdatePunch(Punch p)
    {
        _db.Execute("""
            UPDATE punches SET
                pay_period_id = $period, timestamp = $time, kind = $kind, source = $source,
                comment = $comment, is_voided = $voided, hours_minutes = $hours,
                hours_category = $category
            WHERE id = $id;
            """,
            ("$id", p.Id), ("$period", p.PayPeriodId), ("$time", Format(p.Timestamp)),
            ("$kind", (int)p.Kind), ("$source", (int)p.Source), ("$comment", p.Comment),
            ("$voided", p.IsVoided ? 1 : 0), ("$hours", (int)p.Hours.TotalMinutes),
            ("$category", p.HoursCategory));
    }

    public void DeletePunch(int id) => _db.Execute("DELETE FROM punches WHERE id = $id;", ("$id", id));

    public int CountPunches() => Convert.ToInt32(_db.Scalar("SELECT COUNT(*) FROM punches;"));

    /// <summary>Earliest and latest punch timestamps, or null when there are none.</summary>
    public (DateTime Min, DateTime Max)? GetPunchDateRange()
    {
        return _db.Query("""
            SELECT MIN(timestamp) AS mn, MAX(timestamp) AS mx
            FROM punches WHERE is_voided = 0;
            """, r =>
        {
            string? min = r.GetStringOrNull("mn");
            string? max = r.GetStringOrNull("mx");
            if (min is null || max is null) return ((DateTime?)null, (DateTime?)null);
            return (ParseTimestamp(min), ParseTimestamp(max));
        }).FirstOrDefault() is var (mn, mx) && mn is not null && mx is not null
            ? (mn.Value, mx.Value)
            : null;
    }

    public Punch? FindPunchByLegacyId(int legacyId) =>
        _db.Query("SELECT * FROM punches WHERE legacy_id = $id LIMIT 1;", MapPunch, ("$id", legacyId)).FirstOrDefault();

    /// <summary>True when this exact punch has already been stored (used to make imports idempotent).</summary>
    public bool HasPunch(int employeeId, DateTime timestamp, PunchKind kind) =>
        Convert.ToInt32(_db.Scalar("""
            SELECT COUNT(*) FROM punches
            WHERE employee_id = $emp AND timestamp = $ts AND kind = $kind;
            """, ("$emp", employeeId), ("$ts", Format(timestamp)), ("$kind", (int)kind))) > 0;

    public void AssignPunchesToPeriod(int payPeriodId, DateTime start, DateTime end)
    {
        _db.Execute("""
            UPDATE punches SET pay_period_id = $period
            WHERE timestamp >= $start AND timestamp <= $end AND is_voided = 0;
            """,
            ("$period", payPeriodId), ("$start", Format(start)), ("$end", Format(end)));
    }

    // =====================================================================
    // Pay periods
    // =====================================================================

    private static PayPeriod MapPeriod(SqliteDataReader r) => new()
    {
        Id = r.GetInt("id"),
        Start = DateTime.Parse(r.GetStringOrNull("start_date") ?? "2000-01-01", CultureInfo.InvariantCulture),
        End = DateTime.Parse(r.GetStringOrNull("end_date") ?? "2000-01-01", CultureInfo.InvariantCulture),
        Type = (PayPeriodType)r.GetInt("period_type"),
        SplitDay = r.GetInt("split_day", 15),
        IsClosed = r.GetBool("is_closed"),
    };

    public List<PayPeriod> ListPayPeriods(DateTime? from = null, DateTime? to = null)
    {
        if (from is null && to is null)
            return _db.Query("SELECT * FROM pay_periods ORDER BY start_date DESC;", MapPeriod);

        return _db.Query("""
            SELECT * FROM pay_periods
            WHERE end_date >= $from AND start_date <= $to
            ORDER BY start_date DESC;
            """, MapPeriod,
            ("$from", FormatDate(from ?? DateTime.MinValue)),
            ("$to", FormatDate(to ?? DateTime.MaxValue)));
    }

    public PayPeriod? GetPayPeriod(int id) =>
        _db.Query("SELECT * FROM pay_periods WHERE id = $id;", MapPeriod, ("$id", id)).FirstOrDefault();

    public PayPeriod? GetPayPeriodFor(DateTime moment)
    {
        return _db.Query("""
            SELECT * FROM pay_periods
            WHERE start_date <= $d AND end_date >= $d
            ORDER BY start_date DESC LIMIT 1;
            """, MapPeriod, ("$d", FormatDate(moment))).FirstOrDefault();
    }

    public PayPeriod? GetCurrentPayPeriod(DateTime? now = null)
    {
        var moment = now ?? DateTime.Now;
        return GetPayPeriodFor(moment) ?? ListPayPeriods().FirstOrDefault(p => p.End >= moment.Date);
    }

    public int UpsertPayPeriod(PayPeriod p)
    {
        var existing = _db.Query("SELECT * FROM pay_periods WHERE start_date = $start;", MapPeriod,
            ("$start", FormatDate(p.Start))).FirstOrDefault();

        if (existing is not null)
        {
            _db.Execute("""
                UPDATE pay_periods SET end_date = $end, period_type = $type, split_day = $split
                WHERE id = $id;
                """,
                ("$id", existing.Id), ("$end", FormatDate(p.End)),
                ("$type", (int)p.Type), ("$split", p.SplitDay));
            p.Id = existing.Id;
            return p.Id;
        }

        _db.Execute("""
            INSERT INTO pay_periods (start_date, end_date, period_type, split_day, is_closed)
            VALUES ($start, $end, $type, $split, $closed);
            """,
            ("$start", FormatDate(p.Start)), ("$end", FormatDate(p.End)),
            ("$type", (int)p.Type), ("$split", p.SplitDay), ("$closed", p.IsClosed ? 1 : 0));

        p.Id = Convert.ToInt32(_db.Scalar("SELECT last_insert_rowid();"));
        return p.Id;
    }

    public void SetPayPeriodClosed(int id, bool closed) =>
        _db.Execute("UPDATE pay_periods SET is_closed = $c WHERE id = $id;",
            ("$id", id), ("$c", closed ? 1 : 0));

    // =====================================================================
    // Time cards
    // =====================================================================

    private static TimeCard MapTimeCard(SqliteDataReader r) => new()
    {
        Id = r.GetInt("id"),
        EmployeeId = r.GetInt("employee_id"),
        PayPeriodId = r.GetInt("pay_period_id"),
        Approved = r.GetBool("approved"),
        ApprovedBy = r.GetStringOrNull("approved_by") ?? "",
        ApprovedAt = r.GetDateOrNull("approved_at"),
        AdjustRegularMinutes = r.GetInt("adjust_reg_minutes"),
        AdjustOverTime1Minutes = r.GetInt("adjust_ot1_minutes"),
        AdjustOverTime2Minutes = r.GetInt("adjust_ot2_minutes"),
        Note = r.GetStringOrNull("note") ?? "",
    };

    public TimeCard? GetTimeCard(int employeeId, int payPeriodId) =>
        _db.Query("SELECT * FROM time_cards WHERE employee_id = $emp AND pay_period_id = $period;",
            MapTimeCard, ("$emp", employeeId), ("$period", payPeriodId)).FirstOrDefault();

    public List<TimeCard> ListTimeCards(int payPeriodId) =>
        _db.Query("SELECT * FROM time_cards WHERE pay_period_id = $period;", MapTimeCard,
            ("$period", payPeriodId));

    public TimeCard GetOrCreateTimeCard(int employeeId, int payPeriodId)
    {
        var card = GetTimeCard(employeeId, payPeriodId);
        if (card is not null) return card;

        _db.Execute("""
            INSERT INTO time_cards (employee_id, pay_period_id) VALUES ($emp, $period);
            """, ("$emp", employeeId), ("$period", payPeriodId));

        return GetTimeCard(employeeId, payPeriodId)!;
    }

    public void SaveTimeCard(TimeCard card)
    {
        _db.Execute("""
            UPDATE time_cards SET
                approved = $approved, approved_by = $by, approved_at = $at,
                adjust_reg_minutes = $reg, adjust_ot1_minutes = $ot1,
                adjust_ot2_minutes = $ot2, note = $note
            WHERE id = $id;
            """,
            ("$id", card.Id), ("$approved", card.Approved ? 1 : 0),
            ("$by", card.ApprovedBy), ("$at", TimestampOrNull(card.ApprovedAt)),
            ("$reg", card.AdjustRegularMinutes), ("$ot1", card.AdjustOverTime1Minutes),
            ("$ot2", card.AdjustOverTime2Minutes), ("$note", card.Note));
    }

    // =====================================================================
    // Holidays
    // =====================================================================

    public List<Holiday> ListHolidays()
    {
        return _db.Query("SELECT * FROM holidays ORDER BY date;", r => new Holiday
        {
            Id = r.GetInt("id"),
            Name = r.GetStringOrNull("name") ?? "",
            Date = DateTime.Parse(r.GetStringOrNull("date") ?? "2000-01-01", CultureInfo.InvariantCulture),
            Paid = r.GetBool("paid", true),
        });
    }

    public void SaveHoliday(Holiday h)
    {
        if (h.Id == 0)
        {
            var existing = _db.Query("SELECT * FROM holidays WHERE date = $d;", r => r.GetInt("id"),
                ("$d", FormatDate(h.Date))).FirstOrDefault();
            if (existing != 0)
                h.Id = existing;
        }

        if (h.Id == 0)
        {
            _db.Execute("INSERT INTO holidays (name, date, paid) VALUES ($n, $d, $p);",
                ("$n", h.Name), ("$d", FormatDate(h.Date)), ("$p", h.Paid ? 1 : 0));
            h.Id = Convert.ToInt32(_db.Scalar("SELECT last_insert_rowid();"));
        }
        else
        {
            _db.Execute("UPDATE holidays SET name = $n, date = $d, paid = $p WHERE id = $id;",
                ("$id", h.Id), ("$n", h.Name), ("$d", FormatDate(h.Date)), ("$p", h.Paid ? 1 : 0));
        }
    }

    public void DeleteHoliday(int id) => _db.Execute("DELETE FROM holidays WHERE id = $id;", ("$id", id));

    // =====================================================================
    // Audit + import log
    // =====================================================================

    public void Audit(string action, string detail = "", string user = "")
    {
        _db.Execute("INSERT INTO audit_log (happened_at, user, action, detail) VALUES ($t, $u, $a, $d);",
            ("$t", Format(DateTime.Now)), ("$u", user), ("$a", action), ("$d", detail));
    }

    public int LogImport(string source, string summary, string details)
    {
        _db.Execute("""
            INSERT INTO import_logs (source, imported_at, summary, details)
            VALUES ($s, $t, $sum, $d);
            """,
            ("$s", source), ("$t", Format(DateTime.Now)), ("$sum", summary), ("$d", details));
        return Convert.ToInt32(_db.Scalar("SELECT last_insert_rowid();"));
    }

    public bool WasPeriodEmailed(int payPeriodId, string kind = "period") =>
        Convert.ToInt32(_db.Scalar("""
            SELECT COUNT(*) FROM email_log WHERE period_id = $p AND kind = $k;
            """, ("$p", payPeriodId), ("$k", kind))) > 0;

    public void LogEmail(int payPeriodId, string kind, string recipients, string note = "")
    {
        _db.Execute("""
            INSERT INTO email_log (period_id, kind, sent_at, recipients, note)
            VALUES ($p, $k, $t, $r, $n);
            """,
            ("$p", payPeriodId), ("$k", kind), ("$t", Format(DateTime.Now)),
            ("$r", recipients), ("$n", note));
    }

    public List<(DateTime When, string Source, string Summary)> ListImports(int limit = 50)
    {
        return _db.Query("SELECT * FROM import_logs ORDER BY id DESC LIMIT $n;", r => (
            ParseTimestamp(r.GetStringOrNull("imported_at")),
            r.GetStringOrNull("source") ?? "",
            r.GetStringOrNull("summary") ?? ""), ("$n", limit));
    }
}

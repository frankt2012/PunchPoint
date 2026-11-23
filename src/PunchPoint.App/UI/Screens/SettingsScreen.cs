using PunchPoint.App.UI;
using PunchPoint.Core.Models;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.App.UI.Screens;

public sealed class SettingsScreen : ScreenBase
{
    private readonly TextBox _company = Theme.Input(320);
    private readonly TextBox _message = Theme.Input(520);
    private readonly Label _passwordStatus = new() { AutoSize = true, Font = Theme.Small, ForeColor = Theme.TextMuted };
    private readonly ComboBox _clockMode = Theme.Dropdown(470);

    private readonly ComboBox _periodType = Theme.Dropdown(200);
    private readonly DateTimePicker _anchor = new() { Format = DateTimePickerFormat.Short, Width = 140, Font = Theme.Body };
    private readonly NumericUpDown _splitDay = Number(1, 28, 0, 80);

    private readonly NumericUpDown _rounding = Number(0, 60, 0, 80);
    private readonly ComboBox _roundIn = Theme.Dropdown(170);
    private readonly ComboBox _roundOut = Theme.Dropdown(170);
    private readonly NumericUpDown _dailyOt1 = Number(0, 24, 2, 90, 0.25m);
    private readonly NumericUpDown _dailyOt2 = Number(0, 24, 2, 90, 0.25m);
    private readonly NumericUpDown _weeklyOt1 = Number(0, 168, 2, 90, 0.5m);
    private readonly NumericUpDown _weeklyOt2 = Number(0, 168, 2, 90, 0.5m);
    private readonly NumericUpDown _dayChange = Number(0, 12, 0, 80);
    private readonly NumericUpDown _maxSpan = Number(4, 24, 0, 80);

    private readonly ComboBox _totalsFormat = Theme.Dropdown(180);
    private readonly CheckBox _seventhDay = Theme.Check("Enable 7th day overtime (all hours on a 7th consecutive day are overtime)");
    private readonly CheckBox _punchComment = Theme.Check("Ask for a comment when a supervisor adds a punch");
    private readonly CheckBox _roundDayTotal = Theme.Check("Round the day total instead of each punch");
    private readonly CheckBox _requireApproval = Theme.Check("Warn before exporting a period that is not fully approved");
    private readonly CheckBox _startWithWindows = Theme.Check("Start the clock automatically when Windows starts");
    private readonly DataGridView _shifts = new();
    private readonly DataGridView _holidays = new();
    private readonly Label _dataLabel = new() { AutoSize = true, Font = Theme.Small, ForeColor = Theme.TextMuted };

    public SettingsScreen(AppServices services)
        : base(services, "Settings", "Company rules, shifts and data safety")
    {
        var save = Theme.PrimaryButton("Save settings", 150);
        save.Click += (_, _) => SaveSettings();
        Actions.Controls.Add(save);

        var stack = new StackPanel(14);
        Body.Controls.Add(stack);

        stack.Add(BuildCompanyCard());
        stack.Add(BuildPeriodCard());
        stack.Add(BuildRulesCard());
        stack.Add(BuildShiftCard());
        stack.Add(BuildHolidayCard());
        stack.Add(BuildDataCard());

        _periodType.Items.AddRange(new object[] { "Weekly", "Bi-weekly", "Semi-monthly", "Monthly" });
        _roundIn.Items.AddRange(new object[] { "No rounding", "Nearest", "Always later", "Always earlier" });
        _roundOut.Items.AddRange(new object[] { "No rounding", "Nearest", "Always later", "Always earlier" });
    }

    private static NumericUpDown Number(decimal min, decimal max, int decimals, int width, decimal increment = 1)
        => new()
        {
            Minimum = min,
            Maximum = max,
            DecimalPlaces = decimals,
            Increment = increment,
            Width = width,
            Font = Theme.Body,
            TextAlign = HorizontalAlignment.Right,
        };

    private Panel BuildCompanyCard()
    {
        var card = Theme.Card();
        var stack = new StackPanel(8);
        stack.Add(SectionTitle("Company"));
        stack.Add(Row("Company name", _company));
        stack.Add(Row("Message shown on the clock", _message));
        var passwordRow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
        var changePassword = Theme.SecondaryButton("Change password", 200);
        changePassword.Click += (_, _) => ChangeSupervisorPassword();
        passwordRow.Controls.Add(changePassword);
        passwordRow.Controls.Add(_passwordStatus);
        stack.Add(passwordRow);

        var clockRow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(0, 10, 0, 0) };
        clockRow.Controls.Add(new Label
        {
            Text = "Clock window",
            Width = 300,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 8, 8, 0),
            ForeColor = Theme.TextMuted,
        });
        _clockMode.Items.AddRange(new object[]
        {
            "Windowed — use other programs on this PC",
            "Full screen kiosk — locked to the clock",
        });
        clockRow.Controls.Add(_clockMode);
        stack.Add(clockRow);

        stack.Add(new Label
        {
            Text = "Windowed is right for a shared PC. Use kiosk mode only on a PC that is used for nothing " +
                   "but clocking in; it covers the screen and cannot be closed without the supervisor password. " +
                   "Restart the clock after changing this.",
            Font = Theme.Small,
            ForeColor = Theme.TextMuted,
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            Margin = new Padding(0, 2, 0, 0),
        });

        card.Controls.Add(stack);
        return card;
    }

    private Panel BuildPeriodCard()
    {
        var card = Theme.Card();
        var stack = new StackPanel(8);
        stack.Add(SectionTitle("Pay periods"));
        stack.Add(Row("Period length", _periodType));
        stack.Add(Row("A period starts on", _anchor));
        stack.Add(Row("Semi-monthly split day", _splitDay));

        var generate = Theme.SecondaryButton("Create periods for 1 year back and forward", 330);
        generate.Click += (_, _) => GeneratePeriods();
        stack.Add(generate);
        stack.Add(new Label
        {
            Text = "Creating a period never deletes punches. It only makes sure every week or month has a period to belong to.",
            Font = Theme.Small,
            ForeColor = Theme.TextMuted,
            AutoSize = true,
            MaximumSize = new Size(760, 0),
        });
        card.Controls.Add(stack);
        return card;
    }

    private Panel BuildRulesCard()
    {
        var card = Theme.Card();
        var stack = new StackPanel(8);
        stack.Add(SectionTitle("Rounding and overtime"));

        stack.Add(Row("Round punches to (minutes, 0 = off)", _rounding));
        stack.Add(Row("Round clock-in", _roundIn));
        stack.Add(Row("Round clock-out", _roundOut));
        stack.Add(Row("Daily overtime after (hours)", _dailyOt1));
        stack.Add(Row("Daily double-time after (hours)", _dailyOt2));
        stack.Add(Row("Weekly overtime after (hours)", _weeklyOt1));
        stack.Add(Row("Weekly double-time after (hours)", _weeklyOt2));
        stack.Add(Row("Hours after midnight belong to previous day", _dayChange));
        stack.Add(Row("Warn about shifts longer than (hours)", _maxSpan));

        var totalsRow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
        totalsRow.Controls.Add(new Label { Text = "Totals shown as", Width = 300, AutoSize = false, Margin = new Padding(0, 8, 8, 0), ForeColor = Theme.TextMuted });
        _totalsFormat.Items.AddRange(new object[] { "Hundredths (8.50)", "Minutes (8:30)" });
        totalsRow.Controls.Add(_totalsFormat);
        stack.Add(totalsRow);

        var toggles = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };
        toggles.Controls.Add(_seventhDay);
        toggles.Controls.Add(_punchComment);
        toggles.Controls.Add(_roundDayTotal);
        toggles.Controls.Add(_requireApproval);
        toggles.Controls.Add(_startWithWindows);
        stack.Add(toggles);

        card.Controls.Add(stack);
        return card;
    }

    private Panel BuildShiftCard()
    {
        var card = Theme.Card();
        var stack = new StackPanel(8);
        stack.Add(SectionTitle("Shifts"));

        Theme.StyleGrid(_shifts);
        _shifts.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Shift", FillWeight = 170 });
        _shifts.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Hours", FillWeight = 120 });
        _shifts.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Grace", FillWeight = 90 });
        _shifts.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Lunch", FillWeight = 110 });
        _shifts.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Holidays", FillWeight = 100 });
        _shifts.Height = 190;
        _shifts.Dock = DockStyle.Top;
        stack.Add(_shifts);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
        var add = Theme.PrimaryButton("Add shift", 110);
        add.Click += (_, _) => EditShift(null);
        var edit = Theme.SecondaryButton("Edit", 90);
        edit.Click += (_, _) => EditShift(SelectedShift);
        var remove = Theme.SecondaryButton("Delete", 90);
        remove.Click += (_, _) => DeleteShift();
        buttons.Controls.Add(add);
        buttons.Controls.Add(edit);
        buttons.Controls.Add(remove);
        stack.Add(buttons);

        _shifts.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditShift(SelectedShift); };

        card.Controls.Add(stack);
        return card;
    }

    private Panel BuildHolidayCard()
    {
        var card = Theme.Card();
        var stack = new StackPanel(8);
        stack.Add(SectionTitle("Paid holidays"));

        Theme.StyleGrid(_holidays);
        _holidays.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Holiday", FillWeight = 220 });
        _holidays.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Date", FillWeight = 140 });
        _holidays.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Day", FillWeight = 120 });
        _holidays.Height = 170;
        _holidays.Dock = DockStyle.Top;
        stack.Add(_holidays);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
        var add = Theme.PrimaryButton("Add holiday", 120);
        add.Click += (_, _) => AddHoliday();
        var remove = Theme.SecondaryButton("Remove", 90);
        remove.Click += (_, _) => RemoveHoliday();
        buttons.Controls.Add(add);
        buttons.Controls.Add(remove);
        stack.Add(buttons);

        card.Controls.Add(stack);
        return card;
    }

    private Panel BuildDataCard()
    {
        var card = Theme.Card();
        var stack = new StackPanel(8);
        stack.Add(SectionTitle("Your data"));

        string folder = PunchPoint.Core.Storage.AppPaths.DataDirectory;
        _dataLabel.Text = $"{folder}\nDatabase: punchpoint.db · backups in the Backups folder";
        stack.Add(_dataLabel);

        var counts = new Label { AutoSize = true, Font = Theme.Small, ForeColor = Theme.TextMuted };
        try
        {
            long size = new FileInfo(Services.Store.Db.Path).Length;
            counts.Text =
                $"{Services.Store.ListEmployees(includeInactive: true).Count} employees · " +
                $"{Services.Store.CountPunches()} punches · " +
                $"{Services.Store.ListPayPeriods().Count} pay periods · database {size / 1024.0 / 1024.0:F1} MB";
        }
        catch
        {
            counts.Text = "";
        }
        stack.Add(counts);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
        var check = Theme.SecondaryButton("Check database", 140);
        check.Click += (_, _) => RunIntegrityCheck();
        buttons.Controls.Add(check);
        var backup = Theme.SecondaryButton("Back up now", 120);
        backup.Click += (_, _) => BackupNow();
        var open = Theme.SecondaryButton("Open data folder", 150);
        open.Click += (_, _) => OpenFolder(folder);
        var restore = Theme.SecondaryButton("Restore from backup…", 170);
        restore.Click += (_, _) => RestoreBackup();
        buttons.Controls.Add(backup);
        buttons.Controls.Add(open);
        buttons.Controls.Add(restore);
        stack.Add(buttons);

        card.Controls.Add(stack);
        return card;
    }

    private static Label SectionTitle(string text) => new()
    {
        Text = text,
        Font = Theme.H3,
        ForeColor = Theme.Text,
        AutoSize = true,
        Margin = new Padding(0, 0, 0, 6),
    };

    private static FlowLayoutPanel Row(string label, Control control)
    {
        var row = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = new Padding(0, 2, 0, 2),
        };
        row.Controls.Add(new Label
        {
            Text = label,
            Width = 300,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 8, 8, 0),
            ForeColor = Theme.TextMuted,
        });
        row.Controls.Add(control);
        return row;
    }

    public override void RefreshData()
    {
        var s = Services.Store.GetSettings();

        _company.Text = s.CompanyName;
        _message.Text = s.CompanyMessage;
        _clockMode.SelectedIndex = s.ClockFullScreen ? 1 : 0;
        _passwordStatus.Text = string.IsNullOrWhiteSpace(s.AdminPasswordHash)
            ? "  not set — the admin app is currently unprotected"
            : "  password is set";
        _passwordStatus.ForeColor = string.IsNullOrWhiteSpace(s.AdminPasswordHash) ? Theme.Warning : Theme.TextMuted;

        _periodType.SelectedIndex = (int)s.PeriodType;
        _anchor.Value = s.PeriodAnchor;
        _splitDay.Value = s.SemiMonthlySplitDay;

        _rounding.Value = s.RoundingMinutes;
        _roundIn.SelectedIndex = s.RoundingMinutes == 0 ? 0 : (int)s.RoundInMode;
        _roundOut.SelectedIndex = s.RoundingMinutes == 0 ? 0 : (int)s.RoundOutMode;
        _dailyOt1.Value = Math.Clamp(s.DailyOverTime1Minutes / 60m, 0, 24);
        _dailyOt2.Value = Math.Clamp(s.DailyOverTime2Minutes / 60m, 0, 24);
        _weeklyOt1.Value = Math.Clamp(s.WeeklyOverTime1Minutes / 60m, 0, 168);
        _weeklyOt2.Value = Math.Clamp(s.WeeklyOverTime2Minutes / 60m, 0, 168);
        _dayChange.Value = Math.Clamp(s.DayChangeOffsetMinutes / 60m, 0, 12);
        _maxSpan.Value = Math.Clamp(s.MaxPunchSpanMinutes / 60m, 4, 24);
        _totalsFormat.SelectedIndex = s.TotalsFormat == TotalsFormat.Minutes ? 1 : 0;
        _seventhDay.Checked = s.EnableSeventhDayOvertime;
        _punchComment.Checked = s.ShowPunchCommentDialog;
        _roundDayTotal.Checked = s.RoundDailyTotalInsteadOfPunches;
        _requireApproval.Checked = s.RequireApprovalBeforeExport;
        _startWithWindows.Checked = IsAutoStartEnabled();

        LoadShifts();
        LoadHolidays();
    }

    private Shift? SelectedShift =>
        _shifts.SelectedRows.Count > 0 ? _shifts.SelectedRows[0].Tag as Shift : null;

    private void LoadShifts()
    {
        _shifts.Rows.Clear();
        foreach (var shift in Services.Store.ListShifts())
        {
            int index = _shifts.Rows.Add(
                shift.Name,
                shift.TimeRangeLabel,
                $"{shift.StartGraceMinutes}m",
                shift.LunchEnabled
                    ? (shift.LunchAutoAfterMinutes > 0 ? $"auto {shift.LunchDurationMinutes}m" : "punched")
                    : "none",
                shift.HolidaysPaid ? $"{shift.HolidayHours}h" : "—");
            _shifts.Rows[index].Tag = shift;
        }
    }

    private void LoadHolidays()
    {
        _holidays.Rows.Clear();
        foreach (var holiday in Services.Store.ListHolidays().OrderBy(h => h.Date))
        {
            int index = _holidays.Rows.Add(holiday.Name, holiday.Date.ToString("yyyy-MM-dd"),
                holiday.Date.DayOfWeek.ToString());
            _holidays.Rows[index].Tag = holiday;
        }
    }

    private void EditShift(Shift? shift)
    {
        using var dialog = new ShiftEditDialog(shift);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        Services.Store.SaveShift(dialog.Shift);
        Services.Store.Audit(shift is null ? "Shift added" : "Shift updated", dialog.Shift.Name);
        Services.NotifyDataChanged();
        LoadShifts();
    }

    private void DeleteShift()
    {
        var shift = SelectedShift;
        if (shift is null) return;

        if (!PromptDialog.Confirm(this,
                $"Delete the shift “{shift.Name}”?\n\nEmployees assigned to it will simply have no shift rules."))
            return;

        Services.Store.DeleteShift(shift.Id);
        Services.NotifyDataChanged();
        LoadShifts();
    }

    private void AddHoliday()
    {
        string? name = PromptDialog.AskText(this, "Add holiday", "Holiday name", "Holiday");
        if (string.IsNullOrWhiteSpace(name)) return;

        string? dateText = PromptDialog.AskText(this, "Holiday date", "Date (yyyy-mm-dd)", DateTime.Today.ToString("yyyy-MM-dd"));
        if (dateText is null) return;

        if (!DateTime.TryParse(dateText, out var date))
        {
            PromptDialog.Warn(this, "That date could not be read. Use a format like 2026-12-25.");
            return;
        }

        Services.Store.SaveHoliday(new Holiday { Name = name.Trim(), Date = date.Date });
        Services.NotifyDataChanged();
        LoadHolidays();
    }

    private void RemoveHoliday()
    {
        if (_holidays.SelectedRows.Count == 0) return;
        if (_holidays.SelectedRows[0].Tag is not Holiday holiday) return;

        if (!PromptDialog.Confirm(this, $"Remove {holiday.Name} ({holiday.Date:yyyy-MM-dd})?")) return;

        Services.Store.DeleteHoliday(holiday.Id);
        Services.NotifyDataChanged();
        LoadHolidays();
    }

    private void ChangeSupervisorPassword()
    {
        string? hash = AdminPassword.Change(this, Services.Settings.AdminPasswordHash);
        if (hash is null) return;

        var settings = Services.Store.GetSettings();
        settings.LegacyAdminPin = "";
        settings.AdminPasswordHash = hash;
        Services.Store.SaveSettings(settings);
        Services.Store.Audit("Supervisor password changed", "");
        Services.NotifyDataChanged();

        PromptDialog.Info(this, "Supervisor password updated. It will be required the next time the app starts.");
    }

    private void SaveSettings()
    {
        var s = Services.Store.GetSettings();

        s.CompanyName = _company.Text.Trim();
        s.CompanyMessage = _message.Text.Trim();

        s.PeriodType = (PayPeriodType)Math.Max(0, _periodType.SelectedIndex);
        s.PeriodAnchor = _anchor.Value.Date;
        s.SemiMonthlySplitDay = (int)_splitDay.Value;

        s.RoundingMinutes = (int)_rounding.Value;
        s.RoundInMode = (RoundingMode)Math.Max(1, _roundIn.SelectedIndex);
        s.RoundOutMode = (RoundingMode)Math.Max(1, _roundOut.SelectedIndex);
        s.DailyOverTime1Minutes = (int)(_dailyOt1.Value * 60);
        s.DailyOverTime2Minutes = (int)(_dailyOt2.Value * 60);
        s.WeeklyOverTime1Minutes = (int)(_weeklyOt1.Value * 60);
        s.WeeklyOverTime2Minutes = (int)(_weeklyOt2.Value * 60);
        s.DayChangeOffsetMinutes = (int)(_dayChange.Value * 60);
        s.MaxPunchSpanMinutes = (int)(_maxSpan.Value * 60);
        s.RoundDailyTotalInsteadOfPunches = _roundDayTotal.Checked;
        s.TotalsFormat = _totalsFormat.SelectedIndex == 1 ? TotalsFormat.Minutes : TotalsFormat.Hundredths;
        s.EnableSeventhDayOvertime = _seventhDay.Checked;
        s.ClockFullScreen = _clockMode.SelectedIndex == 1;
        s.ShowPunchCommentDialog = _punchComment.Checked;
        s.RequireApprovalBeforeExport = _requireApproval.Checked;

        Services.Store.SaveSettings(s);
        TrySetAutoStart(_startWithWindows.Checked);
        Services.Store.Audit("Settings saved", s.CompanyName);
        Services.NotifyDataChanged();

        PromptDialog.Info(this, "Settings saved.");
    }

    private void GeneratePeriods()
    {
        var s = Services.Store.GetSettings();
        var from = DateTime.Today.AddYears(-1);
        var to = DateTime.Today.AddYears(1);

        var periods = PayPeriodGenerator.EnsureRange(s, from, to);
        foreach (var period in periods)
            Services.Store.UpsertPayPeriod(period);

        // Make sure every punch points at a period.
        foreach (var period in Services.Store.ListPayPeriods(from, to))
            Services.Store.AssignPunchesToPeriod(period.Id, period.Start, period.End.AddDays(1).AddSeconds(-1));

        Services.NotifyDataChanged();
        PromptDialog.Info(this, $"Created or updated {periods.Count} pay periods.");
    }

    private void RunIntegrityCheck()
    {
        try
        {
            string result = Services.Store.Db.Scalar("PRAGMA integrity_check;")?.ToString() ?? "no result";
            string message = result.Equals("ok", StringComparison.OrdinalIgnoreCase)
                ? "The database is healthy."
                : "The database reported a problem:\n\n" + result +
                  "\n\nRestore the most recent backup from " + PunchPoint.Core.Storage.AppPaths.BackupsDirectory;
            PromptDialog.Info(this, message);
        }
        catch (Exception ex)
        {
            PromptDialog.Warn(this, "The check could not run: " + ex.Message);
        }
    }

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "PunchPoint";

    private static bool IsAutoStartEnabled()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(RunValueName) is string;
        }
        catch
        {
            return false;
        }
    }

    private static void TrySetAutoStart(bool enabled)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key is null) return;

            if (enabled)
            {
                // Start the employee clock, never the password-protected admin app.
                string clock = Path.Combine(AppContext.BaseDirectory, "PunchPointClock.exe");
                string target = File.Exists(clock) ? clock : Application.ExecutablePath;
                key.SetValue(RunValueName, "\"" + target + "\"");
            }
            else
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
        catch
        {
            // registry access can be blocked by policy; the setting simply has no effect
        }
    }

    private void BackupNow()
    {
        try
        {
            string path = Services.Store.Db.Backup();
            PromptDialog.Info(this, "Backup saved to:\n" + path);
        }
        catch (Exception ex)
        {
            PromptDialog.Warn(this, "Backup failed: " + ex.Message);
        }
    }

    private void OpenFolder(string folder)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            PromptDialog.Warn(this, ex.Message);
        }
    }

    private void RestoreBackup()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose a backup to restore",
            Filter = "PunchPoint database (*.db)|*.db",
            InitialDirectory = PunchPoint.Core.Storage.AppPaths.BackupsDirectory,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        if (!PromptDialog.Confirm(this,
                "Restoring replaces everything currently in PunchPoint with the selected backup.\n\n" +
                "A safety copy of the current data will be made first. Continue?"))
            return;

        try
        {
            string safety = Services.Store.Db.Backup();
            Services.Store.Db.Dispose();

            foreach (string suffix in new[] { "", "-wal", "-shm" })
            {
                string target = PunchPoint.Core.Storage.AppPaths.DatabasePath + suffix;
                if (File.Exists(target)) File.Delete(target);
            }

            File.Copy(dialog.FileName, PunchPoint.Core.Storage.AppPaths.DatabasePath, overwrite: true);

            PromptDialog.Info(this, "Backup restored. PunchPoint will now restart.");
            Application.Restart();
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            PromptDialog.Warn(this, "Restore failed: " + ex.Message);
        }
    }
}

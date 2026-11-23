using PunchPoint.App.UI;
using PunchPoint.Core.Models;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.App.UI.Screens;

public sealed class SettingsScreen : ScreenBase
{
    private readonly TextBox _company = Theme.Input(320);
    private readonly TextBox _message = Theme.Input(520);
    private readonly TextBox _adminPin = Theme.Input(140);

    private readonly ComboBox _periodType = Theme.Dropdown(200);
    private readonly DateTimePicker _anchor = new() { Format = DateTimePickerFormat.Short, Width = 140, Font = Theme.Body };
    private readonly NumericUpDown _splitDay = Number(1, 28, 0, 80);
    private readonly NumericUpDown _monthlyDay = Number(1, 28, 0, 80);

    private readonly NumericUpDown _rounding = Number(0, 60, 0, 80);
    private readonly ComboBox _roundIn = Theme.Dropdown(170);
    private readonly ComboBox _roundOut = Theme.Dropdown(170);
    private readonly NumericUpDown _dailyOt1 = Number(0, 24, 2, 90, 0.25m);
    private readonly NumericUpDown _dailyOt2 = Number(0, 24, 2, 90, 0.25m);
    private readonly NumericUpDown _weeklyOt1 = Number(0, 168, 2, 90, 0.5m);
    private readonly NumericUpDown _weeklyOt2 = Number(0, 168, 2, 90, 0.5m);
    private readonly NumericUpDown _dayChange = Number(0, 12, 0, 80);
    private readonly NumericUpDown _maxSpan = Number(4, 24, 0, 80);

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
        stack.Add(Row("Supervisor PIN (blank = no PIN)", _adminPin));
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
        stack.Add(Row("Monthly start day", _monthlyDay));

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

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
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
        _adminPin.Text = s.AdminPin;

        _periodType.SelectedIndex = (int)s.PeriodType;
        _anchor.Value = s.PeriodAnchor;
        _splitDay.Value = s.SemiMonthlySplitDay;
        _monthlyDay.Value = s.MonthlyStartDay;

        _rounding.Value = s.RoundingMinutes;
        _roundIn.SelectedIndex = s.RoundingMinutes == 0 ? 0 : (int)s.RoundInMode;
        _roundOut.SelectedIndex = s.RoundingMinutes == 0 ? 0 : (int)s.RoundOutMode;
        _dailyOt1.Value = Math.Clamp(s.DailyOverTime1Minutes / 60m, 0, 24);
        _dailyOt2.Value = Math.Clamp(s.DailyOverTime2Minutes / 60m, 0, 24);
        _weeklyOt1.Value = Math.Clamp(s.WeeklyOverTime1Minutes / 60m, 0, 168);
        _weeklyOt2.Value = Math.Clamp(s.WeeklyOverTime2Minutes / 60m, 0, 168);
        _dayChange.Value = Math.Clamp(s.DayChangeOffsetMinutes / 60m, 0, 12);
        _maxSpan.Value = Math.Clamp(s.MaxPunchSpanMinutes / 60m, 4, 24);

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

    private void SaveSettings()
    {
        var s = Services.Store.GetSettings();

        s.CompanyName = _company.Text.Trim();
        s.CompanyMessage = _message.Text.Trim();
        s.AdminPin = _adminPin.Text.Trim();

        s.PeriodType = (PayPeriodType)Math.Max(0, _periodType.SelectedIndex);
        s.PeriodAnchor = _anchor.Value.Date;
        s.SemiMonthlySplitDay = (int)_splitDay.Value;
        s.MonthlyStartDay = (int)_monthlyDay.Value;

        s.RoundingMinutes = (int)_rounding.Value;
        s.RoundInMode = (RoundingMode)Math.Max(1, _roundIn.SelectedIndex);
        s.RoundOutMode = (RoundingMode)Math.Max(1, _roundOut.SelectedIndex);
        s.DailyOverTime1Minutes = (int)(_dailyOt1.Value * 60);
        s.DailyOverTime2Minutes = (int)(_dailyOt2.Value * 60);
        s.WeeklyOverTime1Minutes = (int)(_weeklyOt1.Value * 60);
        s.WeeklyOverTime2Minutes = (int)(_weeklyOt2.Value * 60);
        s.DayChangeOffsetMinutes = (int)(_dayChange.Value * 60);
        s.MaxPunchSpanMinutes = (int)(_maxSpan.Value * 60);

        Services.Store.SaveSettings(s);
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

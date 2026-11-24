using PunchPoint.App.UI;
using PunchPoint.Core.Export;
using PunchPoint.Core.Models;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.App.UI.Screens;

/// <summary>
/// Punch editing, modelled on the old timeQplus screen: pick an employee and a pay period,
/// see every punch with day totals, insert/delete punches, adjust totals and approve.
/// </summary>
public sealed class TimeCardsScreen : ScreenBase
{
    private readonly ComboBox _employee = Theme.Dropdown(220);
    private readonly ComboBox _period = Theme.Dropdown(265);
    private readonly DataGridView _grid = new();
    private readonly Label _totals = new() { AutoSize = true, Font = new Font(Theme.Family, 10.5f, FontStyle.Bold), ForeColor = Theme.Text };
    private readonly Label _status = new() { AutoSize = true, Font = Theme.Small, ForeColor = Theme.TextMuted };
    private readonly Button _approve = Theme.PrimaryButton("Approve", 120);
    private readonly Button _delete = Theme.SecondaryButton("Delete", 90);
    private readonly Button _edit = Theme.SecondaryButton("Edit", 90);

    private List<Employee> _employees = new();
    private List<PayPeriod> _periods = new();
    private bool _loading;

    public TimeCardsScreen(AppServices services)
        : base(services, "Punch Editing", "Review and correct punches, then approve the time card")
    {
        Actions.Controls.Add(new Label { Text = "Employee", AutoSize = true, Margin = new Padding(0, 14, 6, 0) });
        _employee.Margin = new Padding(0, 8, 10, 0);
        _employee.SelectedIndexChanged += (_, _) => LoadEmployee();
        Actions.Controls.Add(_employee);

        Actions.Controls.Add(new Label { Text = "Pay period", AutoSize = true, Margin = new Padding(0, 14, 6, 0) });
        _period.Margin = new Padding(0, 8, 10, 0);
        _period.SelectedIndexChanged += (_, _) => LoadEmployee();
        Actions.Controls.Add(_period);

        var add = Theme.PrimaryButton("Add punch", 110);
        add.Margin = new Padding(0, 5, 6, 0);
        add.Click += (_, _) => AddPunch();
        Actions.Controls.Add(add);

        var print = Theme.SecondaryButton("Print", 70);
        print.Margin = new Padding(0, 5, 0, 0);
        print.Click += (_, _) => PrintTimeCard();
        Actions.Controls.Add(print);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        var gridCard = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(14) };
        Theme.StyleGrid(_grid);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Date", FillWeight = 120 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Time", FillWeight = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Type", FillWeight = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Hours", FillWeight = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Day total", FillWeight = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Comment", FillWeight = 300 });
        gridCard.Controls.Add(_grid);
        layout.Controls.Add(gridCard, 0, 0);

        var totalsCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.SurfaceAlt,
            Padding = new Padding(16, 10, 16, 8),
            Margin = new Padding(0, 10, 0, 0),
        };

        var totalsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
        };
        totalsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        totalsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var totalsText = new StackPanel(2);
        totalsText.Add(_totals);
        totalsText.Add(_status);

        var totalsButtons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 6, 0, 0),
        };

        var adjust = Theme.SecondaryButton("Edit totals", 110);
        adjust.Click += (_, _) => AdjustTotals();
        totalsButtons.Controls.Add(adjust);
        totalsButtons.Controls.Add(_delete);
        totalsButtons.Controls.Add(_edit);

        totalsLayout.Controls.Add(totalsText, 0, 0);
        totalsLayout.Controls.Add(totalsButtons, 1, 0);
        totalsCard.Controls.Add(totalsLayout);

        _delete.Click += (_, _) => DeleteSelected();
        _edit.Click += (_, _) => EditSelected();

        layout.Controls.Add(totalsCard, 0, 1);

        var approveRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 0),
            Margin = new Padding(0),
        };
        _approve.Click += (_, _) => ToggleApproval();
        approveRow.Controls.Add(_approve);
        layout.Controls.Add(approveRow, 0, 2);

        Body.Controls.Add(layout);

        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelected(); };
        _grid.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete) DeleteSelected();
            if (e.KeyCode == Keys.Insert) AddPunch();
        };
    }

    public override void RefreshData()
    {
        bool first = _employees.Count == 0;
        _employees = Services.Store.ListEmployees(includeInactive: true);
        _periods = PeriodPickerList.Build(Services.Store, out var currentPeriod);

        _loading = true;
        try
        {
            _employee.Items.Clear();
            foreach (var employee in _employees.OrderBy(e => e.SortName))
                _employee.Items.Add($"{employee.SortName}   ({employee.Pin})");

            _period.Items.Clear();
            foreach (var period in _periods)
                _period.Items.Add(PayPeriodLabels.Label(period, _periods, currentPeriod?.Id));

            int periodIndex = currentPeriod is null ? 0 : Math.Max(0, _periods.FindIndex(p => p.Id == currentPeriod.Id));
            if (_periods.Count > 0) _period.SelectedIndex = periodIndex;

            if (_employee.Items.Count > 0 && (first || _employee.SelectedIndex < 0))
            {
                var ordered = _employees.OrderBy(e => e.SortName).ToList();
                int index = -1;

                // Prefer someone who actually has punches in this period.
                var period = _periods.Count > 0 && _period.SelectedIndex >= 0 ? _periods[_period.SelectedIndex] : null;
                if (period is not null)
                {
                    for (int i = 0; i < ordered.Count; i++)
                    {
                        if (Services.Store.ListPunches(ordered[i].Id, period.Start, period.End.AddDays(1)).Count > 0)
                        {
                            index = i;
                            break;
                        }
                    }
                }

                if (index < 0) index = ordered.FindIndex(e => e.IsActive);
                if (index < 0) index = 0;
                _employee.SelectedIndex = index;
            }
        }
        finally
        {
            _loading = false;
        }

        LoadEmployee();
    }

    private Employee? SelectedEmployee =>
        _employee.SelectedIndex >= 0 && _employee.SelectedIndex < _employees.OrderBy(e => e.SortName).Count()
            ? _employees.OrderBy(e => e.SortName).ElementAt(_employee.SelectedIndex)
            : null;

    private PayPeriod? SelectedPeriod =>
        _period.SelectedIndex >= 0 && _period.SelectedIndex < _periods.Count ? _periods[_period.SelectedIndex] : null;

    private void LoadEmployee()
    {
        if (_loading) return;

        var employee = SelectedEmployee;
        var period = SelectedPeriod;
        _grid.Rows.Clear();

        if (employee is null || period is null)
        {
            _totals.Text = "Select an employee and a pay period.";
            _status.Text = "";
            return;
        }

        var punches = Services.Store.ListPunches(employee.Id, period.Start, period.End.AddDays(1));
        var shift = employee.ShiftId is int id ? Services.Store.GetShift(id) : null;
        var card = Services.Store.GetTimeCard(employee.Id, period.Id);
        var result = TimeCardCalculator.Calculate(employee, shift, period, punches, Services.Settings,
            Services.Store.ListHolidays(), card);

        var dayTotals = result.Days.ToDictionary(d => d.WorkDate.Date, d => d);

        // One row per punch, plus rows for days that only have paid holiday/misc hours.
        var byDay = punches.GroupBy(p => TimeCardCalculator.WorkDate(p.Timestamp, Services.Settings.DayChangeOffsetMinutes))
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Timestamp).ToList());

        foreach (var day in result.Days)
        {
            if (byDay.TryGetValue(day.WorkDate.Date, out var dayPunches) && dayPunches.Count > 0)
            {
                for (int i = 0; i < dayPunches.Count; i++)
                {
                    var punch = dayPunches[i];
                    int index = _grid.Rows.Add(
                        i == 0 ? day.WorkDate.ToString("ddd M/d/yyyy") : "",
                        punch.Timestamp.ToString("h:mm tt"),
                        KindLabel(punch.Kind),
                        punch.Kind == PunchKind.Hours ? (punch.Hours.TotalMinutes / 60.0).ToString("0.00") : "",
                        i == 0 ? HoursText(day.TotalMinutes) : "",
                        punch.Comment);

                    _grid.Rows[index].Tag = punch;
                    if (punch.Kind is PunchKind.BreakOut or PunchKind.BreakIn)
                        _grid.Rows[index].Cells[2].Style.ForeColor = Theme.TextMuted;
                    if (punch.Kind == PunchKind.Hours)
                        _grid.Rows[index].Cells[2].Style.ForeColor = Theme.Primary;
                    if (day.Issues.Count > 0 && i == 0)
                        _grid.Rows[index].Cells[0].Style.ForeColor = Theme.Warning;
                }
            }
            else if (day.TotalMinutes > 0)
            {
                int index = _grid.Rows.Add(day.WorkDate.ToString("ddd M/d/yyyy"), "",
                    day.IsHoliday ? "Holiday" : "Hours", "", HoursText(day.TotalMinutes),
                    day.IsHoliday ? day.HolidayName ?? "" : "");
                _grid.Rows[index].Tag = null;
            }
        }

        int vacation = punches.Where(p => p.Kind == PunchKind.Hours && p.HoursCategory == "Vacation").Sum(p => (int)p.Hours.TotalMinutes);
        int sick = punches.Where(p => p.Kind == PunchKind.Hours && p.HoursCategory == "Sick").Sum(p => (int)p.Hours.TotalMinutes);
        int other = punches.Where(p => p.Kind == PunchKind.Hours && p.HoursCategory == "Other").Sum(p => (int)p.Hours.TotalMinutes);

        var settings = Services.Settings;
        _totals.Text =
            $"Regular {HoursText(result.RegularMinutes)}    " +
            $"OT1 {HoursText(result.OverTime1Minutes)}    " +
            $"OT2 {HoursText(result.OverTime2Minutes)}    " +
            $"Holiday {HoursText(result.HolidayMinutes)}    " +
            $"TOTAL {HoursText(result.TotalMinutes)}\n" +
            $"Vacation {HoursText(vacation)}    " +
            $"Sick {HoursText(sick)}    " +
            $"Other {HoursText(other)}";

        var issues = result.Issues.Count;
        _status.Text = issues == 0
            ? $"{punches.Count} punches · no problems detected"
            : $"{punches.Count} punches · {issues} issue(s) need attention (orange dates)";
        _status.ForeColor = issues == 0 ? Theme.TextMuted : Theme.Warning;

        _approve.Text = card?.Approved == true ? "Approved ✓" : "Approve";
        _approve.BackColor = card?.Approved == true ? Theme.Success : Theme.Primary;
        _approve.ForeColor = Color.White;

        if (_grid.Rows.Count == 0)
            _grid.Rows.Add("", "", "", "", "", "No punches in this period");
    }

    private string HoursText(int minutes) =>
        Services.Settings.TotalsFormat == TotalsFormat.Minutes
            ? $"{(minutes / 60)}:{(minutes % 60):00}"
            : (minutes / 60.0).ToString("0.00");

    private static string KindLabel(PunchKind kind) => kind switch
    {
        PunchKind.In => "IN",
        PunchKind.Out => "OUT",
        PunchKind.BreakOut => "Break out",
        PunchKind.BreakIn => "Break in",
        PunchKind.Hours => "Paid hours",
        _ => kind.ToString(),
    };

    private Punch? SelectedPunch =>
        _grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0].Tag as Punch : null;

    private void AddPunch()
    {
        var employee = SelectedEmployee;
        if (employee is null) return;

        using var dialog = new PunchEditDialog(Services, employee, null);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            Services.NotifyDataChanged();
            RefreshData();
        }
    }

    private void EditSelected()
    {
        var employee = SelectedEmployee;
        var punch = SelectedPunch;
        if (employee is null || punch is null) return;

        using var dialog = new PunchEditDialog(Services, employee, punch);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            Services.NotifyDataChanged();
            RefreshData();
        }
    }

    private void DeleteSelected()
    {
        var employee = SelectedEmployee;
        var punch = SelectedPunch;
        if (employee is null || punch is null) return;

        if (!PromptDialog.Confirm(this,
                $"Delete the {KindLabel(punch.Kind).ToLowerInvariant()} punch at {punch.Timestamp:g}?"))
            return;

        Services.Store.DeletePunch(punch.Id);
        Services.Store.Audit("Punch deleted", $"{employee.DisplayName} {punch.Timestamp:g}");
        Services.NotifyDataChanged();
        RefreshData();
    }

    private void AdjustTotals()
    {
        var employee = SelectedEmployee;
        var period = SelectedPeriod;
        if (employee is null || period is null) return;

        string? input = PromptDialog.AskText(this, "Edit totals",
            "Minutes to add to regular hours (negative to subtract).", "0");
        if (input is null || !int.TryParse(input, out int minutes)) return;

        var card = Services.Store.GetOrCreateTimeCard(employee.Id, period.Id);
        card.AdjustRegularMinutes += minutes;
        Services.Store.SaveTimeCard(card);
        Services.Store.Audit("Hours adjusted", $"{employee.DisplayName} {minutes} min");
        Services.NotifyDataChanged();
        LoadEmployee();
    }

    private void ToggleApproval()
    {
        var employee = SelectedEmployee;
        var period = SelectedPeriod;
        if (employee is null || period is null) return;

        var card = Services.Store.GetOrCreateTimeCard(employee.Id, period.Id);
        card.Approved = !card.Approved;
        card.ApprovedAt = card.Approved ? DateTime.Now : null;
        card.ApprovedBy = Environment.UserName;
        Services.Store.SaveTimeCard(card);
        Services.Store.Audit(card.Approved ? "Time card approved" : "Approval removed",
            $"{employee.DisplayName} {period.Label}");
        Services.NotifyDataChanged();
        LoadEmployee();
    }

    private void PrintTimeCard()
    {
        var employee = SelectedEmployee;
        var period = SelectedPeriod;
        if (employee is null || period is null) return;

        var punches = Services.Store.ListPunches(employee.Id, period.Start, period.End.AddDays(1));
        var shift = employee.ShiftId is int id ? Services.Store.GetShift(id) : null;
        var result = TimeCardCalculator.Calculate(employee, shift, period, punches, Services.Settings,
            Services.Store.ListHolidays(), Services.Store.GetTimeCard(employee.Id, period.Id));

        using var preview = new PrintPreviewDialog
        {
            Document = new TimeCardPrintDocument(Services.Settings, result),
            Width = 1000,
            Height = 760,
            Text = $"Time card — {employee.SortName}",
        };
        preview.ShowDialog(this);
    }
}

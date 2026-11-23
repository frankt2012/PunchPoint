using PunchPoint.App.UI;
using PunchPoint.Core.Export;
using PunchPoint.Core.Models;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.App.UI.Screens;

public sealed class TimeCardsScreen : ScreenBase
{
    private readonly ComboBox _periods = Theme.Dropdown(300);
    private readonly DataGridView _employees = new();
    private readonly DataGridView _days = new();
    private readonly Label _detailTitle = new() { AutoSize = true, Font = Theme.H2, ForeColor = Theme.Text };
    private readonly Label _detailSubtitle = new() { AutoSize = true, Font = Theme.Small, ForeColor = Theme.TextMuted };
    private readonly Label _totalsLabel = new() { AutoSize = true, Font = Theme.Body, ForeColor = Theme.Text };
    private readonly Label _summaryLabel = new() { AutoSize = true, Font = Theme.Body, ForeColor = Theme.TextMuted };
    private readonly Button _approve = Theme.PrimaryButton("Approve time card", 170);
    private readonly Button _adjust = Theme.SecondaryButton("Adjust hours", 130);
    private readonly Button _addPunch = Theme.SecondaryButton("Add punch", 110);

    private List<PayPeriod> _loadedPeriods = new();
    private Employee? _selectedEmployee;

    public TimeCardsScreen(AppServices services)
        : base(services, "Time Cards", "Review, correct and approve hours")
    {
        _periods.Margin = new Padding(0, 8, 8, 0);
        _periods.SelectedIndexChanged += (_, _) => LoadPeriod();
        Actions.Controls.Add(_periods);

        var export = Theme.SecondaryButton("Export CSV", 120);
        export.Click += (_, _) => ExportCsv();
        Actions.Controls.Add(export);

        var print = Theme.SecondaryButton("Print", 90);
        print.Click += (_, _) => PrintTimeCard();
        Actions.Controls.Add(print);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _summaryLabel.Margin = new Padding(0, 0, 0, 12);
        _summaryLabel.Dock = DockStyle.Top;
        layout.Controls.Add(_summaryLabel, 0, 0);

        var split = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
        };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        layout.Controls.Add(split, 0, 1);
        Body.Controls.Add(layout);

        // -------- left: employees --------
        var leftCard = Theme.Card(0);
        leftCard.Dock = DockStyle.Fill;
        leftCard.Margin = new Padding(0, 0, 12, 0);
        leftCard.AutoSize = false;

        Theme.StyleGrid(_employees);
        _employees.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Employee", FillWeight = 190 });
        _employees.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Reg", FillWeight = 70 });
        _employees.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "OT1", FillWeight = 65 });
        _employees.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "OT2", FillWeight = 65 });
        _employees.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Hol", FillWeight = 60 });
        _employees.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Total", FillWeight = 75 });
        _employees.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "✓", FillWeight = 40 });
        _employees.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "!", FillWeight = 35 });

        var leftHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        leftHost.Controls.Add(_employees);
        leftCard.Controls.Add(leftHost);
        _employees.SelectionChanged += (_, _) => ShowSelectedEmployee();

        split.Controls.Add(leftCard, 0, 0);

        // -------- right: detail --------
        var rightCard = Theme.Card(0);
        rightCard.Dock = DockStyle.Fill;
        rightCard.AutoSize = false;

        var detailHeader = new Panel { Dock = DockStyle.Top, Height = 78, Padding = new Padding(18, 14, 18, 0) };
        _detailTitle.Location = new Point(16, 12);
        _detailSubtitle.Location = new Point(18, 44);
        detailHeader.Controls.Add(_detailTitle);
        detailHeader.Controls.Add(_detailSubtitle);

        var detailFooter = new Panel { Dock = DockStyle.Bottom, Height = 96, Padding = new Padding(18, 8, 18, 14) };
        _totalsLabel.Location = new Point(18, 8);
        detailFooter.Controls.Add(_totalsLabel);

        var footerButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.LeftToRight,
            Height = 46,
            WrapContents = false,
        };
        footerButtons.Controls.Add(_addPunch);
        footerButtons.Controls.Add(_adjust);
        footerButtons.Controls.Add(_approve);
        detailFooter.Controls.Add(footerButtons);
        footerButtons.BringToFront();

        Theme.StyleGrid(_days);
        _days.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Day", FillWeight = 110 });
        _days.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "In", FillWeight = 80 });
        _days.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Out", FillWeight = 80 });
        _days.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Break", FillWeight = 65 });
        _days.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Reg", FillWeight = 65 });
        _days.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "OT", FillWeight = 60 });
        _days.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Hol", FillWeight = 55 });
        _days.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Total", FillWeight = 70 });
        _days.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Notes", FillWeight = 240 });

        var detailHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 0, 18, 8) };
        detailHost.Controls.Add(_days);

        rightCard.Controls.Add(detailHost);
        rightCard.Controls.Add(detailFooter);
        rightCard.Controls.Add(detailHeader);
        split.Controls.Add(rightCard, 1, 0);

        _approve.Click += (_, _) => ToggleApproval();
        _adjust.Click += (_, _) => AdjustHours();
        _addPunch.Click += (_, _) => AddPunch();
        _days.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) ShowDayDetails(); };
        _employees.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) ShowDayDetails(); };
    }

    public override void RefreshData()
    {
        _loadedPeriods = Services.Store.ListPayPeriods().OrderByDescending(p => p.Start).ToList();
        var current = Services.Store.GetCurrentPayPeriod();

        int select = current is null ? 0 : Math.Max(0, _loadedPeriods.FindIndex(p => p.Id == current.Id));

        _periods.Items.Clear();
        foreach (var period in _loadedPeriods)
            _periods.Items.Add($"{period.Start:MMM d} – {period.End:MMM d, yyyy}   ({period.Type})");

        if (_loadedPeriods.Count > 0)
        {
            _periods.SelectedIndex = select;
        }

        LoadPeriod();
    }

    private PayPeriod? CurrentPeriod =>
        _periods.SelectedIndex >= 0 && _periods.SelectedIndex < _loadedPeriods.Count
            ? _loadedPeriods[_periods.SelectedIndex]
            : null;

    private void LoadPeriod()
    {
        var period = CurrentPeriod;
        _employees.Rows.Clear();
        _days.Rows.Clear();
        _selectedEmployee = null;

        if (period is null)
        {
            _summaryLabel.Text = "No pay periods yet. Add them in Settings.";
            return;
        }

        var employees = Services.Store.ListEmployees(includeInactive: true);
        int regTotal = 0, otTotal = 0, holTotal = 0, issueTotal = 0, approvedCount = 0, withTime = 0;

        foreach (var employee in employees)
        {
            var summary = Services.SummaryFor(employee, period);
            if (summary.TotalMinutes == 0 && summary.IssueCount == 0) continue;

            withTime++;
            regTotal += summary.RegularMinutes;
            otTotal += summary.OverTime1Minutes + summary.OverTime2Minutes;
            holTotal += summary.HolidayMinutes;
            issueTotal += summary.IssueCount;

            var card = Services.Store.GetTimeCard(employee.Id, period.Id);
            if (card?.Approved == true) approvedCount++;

            int rowIndex = _employees.Rows.Add(
                employee.SortName,
                Fmt.Hours(summary.RegularMinutes),
                Fmt.HoursOrDash(summary.OverTime1Minutes),
                Fmt.HoursOrDash(summary.OverTime2Minutes),
                Fmt.HoursOrDash(summary.HolidayMinutes),
                Fmt.Hours(summary.TotalMinutes),
                card?.Approved == true ? "✓" : "",
                summary.IssueCount > 0 ? summary.IssueCount.ToString() : "");

            _employees.Rows[rowIndex].Tag = employee;
            if (summary.IssueCount > 0)
                _employees.Rows[rowIndex].Cells[7].Style.ForeColor = Theme.Warning;
            if (card?.Approved == true)
                _employees.Rows[rowIndex].Cells[6].Style.ForeColor = Theme.Success;
        }

        string periodLabel = $"{period.Start:MMM d} – {period.End:MMM d}";
        _summaryLabel.Text =
            $"{periodLabel}:   Regular {Fmt.Hours(regTotal)} h   ·   Overtime {Fmt.Hours(otTotal)} h   ·   " +
            $"Holiday {Fmt.Hours(holTotal)} h   ·   Total {Fmt.Hours(regTotal + otTotal + holTotal)} h   ·   " +
            $"{approvedCount}/{withTime} approved" +
            (issueTotal > 0 ? $"   ·   {issueTotal} punch issue(s)" : "");

        if (_employees.Rows.Count > 0)
        {
            _employees.Rows[0].Selected = true;
            _employees.CurrentCell = _employees.Rows[0].Cells[0];
        }

        ShowSelectedEmployee();
    }

    private void ShowSelectedEmployee()
    {
        var employee = _employees.SelectedRows.Count > 0 ? _employees.SelectedRows[0].Tag as Employee : null;
        var period = CurrentPeriod;
        _days.Rows.Clear();

        if (employee is null || period is null)
        {
            _selectedEmployee = null;
            _detailTitle.Text = "";
            _detailSubtitle.Text = "Select an employee to see their days.";
            _totalsLabel.Text = "";
            return;
        }

        _selectedEmployee = employee;
        var punches = Services.Store.ListPunches(employee.Id, period.Start, period.End.AddDays(1));
        var shift = employee.ShiftId is int id ? Services.Store.GetShift(id) : null;
        var card = Services.Store.GetTimeCard(employee.Id, period.Id);

        var result = TimeCardCalculator.Calculate(employee, shift, period, punches, Services.Settings,
            Services.Store.ListHolidays(), card);

        _detailTitle.Text = employee.SortName;
        _detailSubtitle.Text = shift is null
            ? $"{employee.PayType} · no shift assigned"
            : $"{shift.Name} · {shift.TimeRangeLabel}" +
              (shift.LunchEnabled && shift.LunchAutoAfterMinutes > 0 ? $" · auto lunch {shift.LunchDurationMinutes} min" : "");

        foreach (var day in result.Days)
        {
            int index = _days.Rows.Add(
                day.WorkDate.ToString("ddd M/d"),
                day.FirstInLabel,
                day.LastOutLabel,
                day.BreakMinutes > 0 ? day.BreakMinutes.ToString() : "",
                Fmt.HoursOrDash(day.RegularMinutes),
                Fmt.HoursOrDash(day.OverTime1Minutes + day.OverTime2Minutes),
                Fmt.HoursOrDash(day.HolidayMinutes),
                Fmt.HoursOrDash(day.TotalMinutes),
                day.Issues.Count > 0
                    ? day.Issues[0]
                    : day.IsHoliday ? $"{day.HolidayName} (paid holiday)" : "");

            _days.Rows[index].Tag = day;
            if (day.Issues.Count > 0)
            {
                _days.Rows[index].Cells[8].Style.ForeColor = Theme.Warning;
                _days.Rows[index].DefaultCellStyle.BackColor = Color.FromArgb(0xFF, 0xFB, 0xEB);
            }
            else if (day.IsHoliday)
            {
                _days.Rows[index].DefaultCellStyle.BackColor = Theme.SuccessSoft;
            }
        }

        _totalsLabel.Text =
            $"Regular {Fmt.Hours(result.RegularMinutes)} h    Overtime {Fmt.Hours(result.OverTime1Minutes + result.OverTime2Minutes)} h    " +
            $"Holiday {Fmt.Hours(result.HolidayMinutes)} h    Adjustment {Fmt.Hours(result.AdjustmentMinutes)} h    " +
            $"Total {Fmt.Hours(result.TotalMinutes)} h";

        _approve.Text = card?.Approved == true ? "Approved — undo" : "Approve time card";
        _approve.BackColor = card?.Approved == true ? Theme.SurfaceAlt : Theme.Primary;
        _approve.ForeColor = card?.Approved == true ? Theme.Text : Color.White;
    }

    private void ToggleApproval()
    {
        var employee = _selectedEmployee;
        var period = CurrentPeriod;
        if (employee is null || period is null) return;

        var card = Services.Store.GetOrCreateTimeCard(employee.Id, period.Id);
        card.Approved = !card.Approved;
        card.ApprovedAt = card.Approved ? DateTime.Now : null;
        card.ApprovedBy = Environment.UserName;
        Services.Store.SaveTimeCard(card);
        Services.Store.Audit(card.Approved ? "Time card approved" : "Time card approval removed",
            $"{employee.DisplayName} {period.Label}");

        Services.NotifyDataChanged();
        LoadPeriod();
    }

    private void AdjustHours()
    {
        var employee = _selectedEmployee;
        var period = CurrentPeriod;
        if (employee is null || period is null) return;

        string? input = PromptDialog.AskText(this, "Adjust hours",
            "Minutes to add to regular hours (use a negative number to subtract).", "0");

        if (input is null || !int.TryParse(input, out int minutes)) return;

        var card = Services.Store.GetOrCreateTimeCard(employee.Id, period.Id);
        card.AdjustRegularMinutes += minutes;
        Services.Store.SaveTimeCard(card);
        Services.Store.Audit("Hours adjusted", $"{employee.DisplayName} {minutes} min");
        Services.NotifyDataChanged();
        LoadPeriod();
    }

    private void AddPunch()
    {
        var employee = _selectedEmployee;
        if (employee is null) return;

        using var dialog = new PunchEditDialog(Services, employee, null);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            Services.NotifyDataChanged();
            LoadPeriod();
        }
    }

    private void ShowDayDetails()
    {
        var employee = _selectedEmployee;
        var period = CurrentPeriod;
        var day = _days.SelectedRows.Count > 0 ? _days.SelectedRows[0].Tag as DayTotals : null;
        if (employee is null || period is null || day is null) return;

        using var dialog = new DayDetailDialog(Services, employee, period, day);
        dialog.ShowDialog(this);
        Services.NotifyDataChanged();
        LoadPeriod();
    }

    private void ExportCsv()
    {
        var period = CurrentPeriod;
        if (period is null) return;

        using var dialog = new SaveFileDialog
        {
            Title = "Export payroll summary",
            Filter = "CSV file (*.csv)|*.csv",
            FileName = $"payroll-{period.Start:yyyy-MM-dd}.csv",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var rows = CsvExporter.BuildRows(Services.Store, period, Services.Settings);
        CsvExporter.WritePayrollSummary(rows, dialog.FileName);
        PromptDialog.Info(this, $"Exported {rows.Count} employees to\n{dialog.FileName}");
    }

    private void PrintTimeCard()
    {
        var employee = _selectedEmployee;
        var period = CurrentPeriod;
        if (employee is null || period is null) return;

        var punches = Services.Store.ListPunches(employee.Id, period.Start, period.End.AddDays(1));
        var shift = employee.ShiftId is int id ? Services.Store.GetShift(id) : null;
        var result = TimeCardCalculator.Calculate(employee, shift, period, punches, Services.Settings,
            Services.Store.ListHolidays(), Services.Store.GetTimeCard(employee.Id, period.Id));

        var document = new TimeCardPrintDocument(Services.Settings, result);
        using var preview = new PrintPreviewDialog
        {
            Document = document,
            Width = 1000,
            Height = 760,
            Text = $"Time card — {employee.SortName}",
        };
        preview.ShowDialog(this);
    }
}

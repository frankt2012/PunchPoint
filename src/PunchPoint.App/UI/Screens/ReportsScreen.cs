using System.Text;
using PunchPoint.App.UI;
using PunchPoint.Core.Models;
using PunchPoint.Core.Reports;

namespace PunchPoint.App.UI.Screens;

/// <summary>
/// Reports, following the old timeQplus layout: choose a report on the left, choose a pay
/// period and an employee filter, then preview, print or export.
/// </summary>
public sealed class ReportsScreen : ScreenBase
{
    private static readonly (string Name, string Hint)[] Catalog =
    {
        ("Time Card", "Punches and totals for one employee"),
        ("Hours Summary", "Regular, overtime and holiday hours per employee"),
        ("Daily Totals By Day", "Every employee's totals, grouped by date"),
        ("Daily Totals By Employee", "Every employee's totals, grouped by employee"),
        ("Raw Transactions", "Every punch exactly as recorded"),
        ("Time Card Approvals", "Time cards still waiting for approval"),
        ("Employee Profiles", "Names, PINs, badges, shifts and status"),
        ("Shifts", "Shift rules and lunch settings"),
        ("Holidays", "Paid holidays for the year"),
        ("Rules Setup", "Overtime, rounding and period rules"),
    };

    private readonly ListBox _list = new();
    private readonly ComboBox _periods = Theme.Dropdown(240);
    private readonly ComboBox _employeeFilter = Theme.Dropdown(220);
    private readonly DataGridView _grid = new();
    private readonly Label _summary = new() { AutoSize = true, Font = Theme.Small, ForeColor = Theme.TextMuted };

    private List<PayPeriod> _loadedPeriods = new();
    private List<Employee> _employees = new();
    private ReportTable? _table;

    public ReportsScreen(AppServices services)
        : base(services, "Reports", "Preview, print or export")
    {
        _periods.Margin = new Padding(0, 8, 8, 0);
        _periods.SelectedIndexChanged += (_, _) => Build();
        Actions.Controls.Add(_periods);

        _employeeFilter.Margin = new Padding(0, 8, 8, 0);
        _employeeFilter.SelectedIndexChanged += (_, _) => Build();
        Actions.Controls.Add(_employeeFilter);

        var print = Theme.SecondaryButton("Print", 80);
        print.Margin = new Padding(0, 5, 6, 0);
        print.Click += (_, _) => PrintReport();
        Actions.Controls.Add(print);

        var export = Theme.PrimaryButton("Export CSV", 120);
        export.Margin = new Padding(0, 5, 0, 0);
        export.Click += (_, _) => ExportReport();
        Actions.Controls.Add(export);

        var split = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
        };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var listCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 12, 0),
        };
        _list.Dock = DockStyle.Fill;
        _list.BorderStyle = BorderStyle.None;
        _list.Font = new Font(Theme.Family, 10.5f);
        _list.IntegralHeight = false;
        foreach (var (name, _) in Catalog) _list.Items.Add(name);
        _list.SelectedIndexChanged += (_, _) => Build();
        listCard.Controls.Add(_list);
        split.Controls.Add(listCard, 0, 0);

        var previewCard = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(14) };

        Theme.StyleGrid(_grid);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 30, Padding = new Padding(2, 8, 0, 0) };
        bottom.Controls.Add(_summary);

        previewCard.Controls.Add(_grid);
        previewCard.Controls.Add(bottom);
        split.Controls.Add(previewCard, 1, 0);

        Body.Controls.Add(split);
        _list.SelectedIndex = 0;
    }

    public override void RefreshData()
    {
        _loadedPeriods = Services.Store.ListPayPeriods().OrderByDescending(p => p.Start).ToList();
        _employees = Services.Store.ListEmployees(includeInactive: true).OrderBy(e => e.SortName).ToList();

        var current = Services.Store.GetCurrentPayPeriod();
        int periodIndex = current is null ? 0 : Math.Max(0, _loadedPeriods.FindIndex(p => p.Id == current.Id));

        _periods.Items.Clear();
        foreach (var period in _loadedPeriods)
            _periods.Items.Add($"{period.Start:MMM d} – {period.End:MMM d, yyyy}");

        _employeeFilter.Items.Clear();
        _employeeFilter.Items.Add("All employees");
        foreach (var employee in _employees) _employeeFilter.Items.Add(employee.SortName);

        if (_loadedPeriods.Count > 0) _periods.SelectedIndex = periodIndex;
        if (_employeeFilter.Items.Count > 0) _employeeFilter.SelectedIndex = 0;

        Build();
    }

    private PayPeriod? Period =>
        _periods.SelectedIndex >= 0 && _periods.SelectedIndex < _loadedPeriods.Count
            ? _loadedPeriods[_periods.SelectedIndex]
            : null;

    private Employee? FilterEmployee =>
        _employeeFilter.SelectedIndex > 0 && _employeeFilter.SelectedIndex - 1 < _employees.Count
            ? _employees[_employeeFilter.SelectedIndex - 1]
            : null;

    private string SelectedReport =>
        _list.SelectedIndex >= 0 && _list.SelectedIndex < Catalog.Length ? Catalog[_list.SelectedIndex].Name : Catalog[0].Name;

    private void Build()
    {
        if (_periods.Items.Count == 0) return;

        var period = Period;
        var store = Services.Store;
        var settings = Services.Settings;
        var employee = FilterEmployee ?? _employees.FirstOrDefault();

        _table = SelectedReport switch
        {
            "Time Card" => employee is null || period is null ? null
                : ReportBuilder.TimeCard(store, employee, period, settings),
            "Hours Summary" => period is null ? null
                : ReportBuilder.HoursSummary(store, period, settings, _employees),
            "Daily Totals By Day" => period is null ? null
                : ReportBuilder.DailyTotals(store, period, settings, _employees, byEmployee: false),
            "Daily Totals By Employee" => period is null ? null
                : ReportBuilder.DailyTotals(store, period, settings, _employees, byEmployee: true),
            "Raw Transactions" => period is null ? null
                : ReportBuilder.RawTransactions(store, period),
            "Time Card Approvals" => period is null ? null
                : ReportBuilder.Unapproved(store, period, settings, _employees),
            "Employee Profiles" => ReportBuilder.EmployeeProfiles(store),
            "Shifts" => ReportBuilder.Shifts(store),
            "Holidays" => ReportBuilder.Holidays(store),
            "Rules Setup" => ReportBuilder.Rules(store),
            _ => null,
        };

        _grid.Rows.Clear();
        _grid.Columns.Clear();

        if (_table is null)
        {
            _summary.Text = "Nothing to show for this selection.";
            return;
        }

        foreach (string column in _table.Columns)
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = column,
                FillWeight = 100,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            });

        foreach (var row in _table.Rows)
            _grid.Rows.Add(row.ToArray());

        _summary.Text = $"{_table.Rows.Count} row(s) · {_table.Title}";
    }

    private void PrintReport()
    {
        if (_table is null) return;

        using var preview = new PrintPreviewDialog
        {
            Document = new TablePrintDocument(_table, Services.Settings.CompanyName),
            Width = 1000,
            Height = 760,
            Text = _table.Title,
        };
        preview.ShowDialog(this);
    }

    private void ExportReport()
    {
        if (_table is null) return;

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv",
            FileName = SafeFileName(_table.Title) + ".csv",
            Title = "Export report",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", _table.Columns.Select(Csv)));
        foreach (var row in _table.Rows)
            sb.AppendLine(string.Join(",", row.Select(Csv)));

        File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(true));
        PromptDialog.Info(this, "Exported to\n" + dialog.FileName);
    }

    private static string SafeFileName(string title)
    {
        foreach (char bad in Path.GetInvalidFileNameChars())
            title = title.Replace(bad, '-');
        return title.Replace(' ', '-').ToLowerInvariant();
    }

    private static string Csv(string? value)
    {
        value ??= "";
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}

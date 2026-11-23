using PunchPoint.App.UI;
using PunchPoint.Core.Export;
using PunchPoint.Core.Models;
using PunchPoint.Core.Reports;

namespace PunchPoint.App.UI.Screens;

/// <summary>
/// Payroll export, following the old timeQplus Export screen: choose a period, choose who to
/// include, choose an output format, then export. Warns about unapproved time cards.
/// </summary>
public sealed class ExportScreen : ScreenBase
{
    private readonly ComboBox _periods = Theme.Dropdown(260);
    private readonly ComboBox _format = Theme.Dropdown(300);
    private readonly ComboBox _who = Theme.Dropdown(220);
    private readonly CheckBox _approvedOnly = Theme.Check("Only export approved time cards");
    private readonly CheckBox _decimal = Theme.Check("Hours as decimal (8.50) instead of h:mm");
    private readonly CheckBox _quotes = Theme.Check("Put quotes around text fields");
    private readonly CheckBox _titles = Theme.Check("Include a header row");
    private readonly Label _summary = new() { AutoSize = true, Font = Theme.Body, ForeColor = Theme.TextMuted };
    private readonly Label _warning = new() { AutoSize = true, Font = Theme.Small, ForeColor = Theme.Warning };

    private List<PayPeriod> _periodsLoaded = new();
    private List<Employee> _employees = new();

    public ExportScreen(AppServices services)
        : base(services, "Export", "Payroll and spreadsheet files")
    {
        var export = Theme.PrimaryButton("Export…", 120);
        export.Click += (_, _) => RunExport();
        Actions.Controls.Add(export);

        var check = Theme.SecondaryButton("Check approvals", 140);
        check.Click += (_, _) => ShowApprovals();
        Actions.Controls.Add(check);

        var stack = new StackPanel(14);
        Body.Controls.Add(stack);

        var card = Theme.Card();
        var form = new StackPanel(10);

        form.Add(Section("Pay period"));
        _periods.SelectedIndexChanged += (_, _) => RefreshSummary();
        form.Add(_periods);

        form.Add(Section("Employees"));
        _who.Items.AddRange(new object[] { "All employees", "Only employees with salaried pay", "Only employees with hourly pay" });
        _who.SelectedIndex = 0;
        _who.SelectedIndexChanged += (_, _) => RefreshSummary();
        form.Add(_who);

        form.Add(Section("Output format"));
        _format.Items.AddRange(new object[]
        {
            "Payroll summary (CSV)",
            "Generic ASCII payroll file",
            "Daily detail (CSV)",
            "Raw punches (CSV)",
            "QuickBooks-friendly summary (CSV)",
            "Payroll summary (PDF)",
        });
        _format.SelectedIndex = 0;
        _format.SelectedIndexChanged += (_, _) => RefreshSummary();
        form.Add(_format);

        form.Add(Section("Options"));
        var options = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        options.Controls.Add(_approvedOnly);
        options.Controls.Add(_decimal);
        options.Controls.Add(_quotes);
        options.Controls.Add(_titles);
        _decimal.Checked = true;
        _quotes.Checked = true;
        _titles.Checked = true;
        form.Add(options);

        form.Add(_summary);
        form.Add(_warning);

        card.Controls.Add(form);
        stack.Add(card);

        stack.Add(new Label
        {
            Text = "Payroll summary gives one line per employee. Generic ASCII mirrors the old timeQplus file: " +
                   "employee number, name, badge, PIN and the hour columns, with a configurable header and quoting.",
            Font = Theme.Small,
            ForeColor = Theme.TextMuted,
            AutoSize = true,
            MaximumSize = new Size(820, 0),
        });
    }

    private static Label Section(string text) => new()
    {
        Text = text,
        Font = Theme.SmallBold,
        ForeColor = Theme.TextMuted,
        AutoSize = true,
        Margin = new Padding(0, 8, 0, 0),
    };

    public override void RefreshData()
    {
        _periodsLoaded = Services.Store.ListPayPeriods().OrderByDescending(p => p.Start).ToList();
        _employees = Services.Store.ListEmployees(includeInactive: true);

        var current = Services.Store.GetCurrentPayPeriod();
        int index = current is null ? 0 : Math.Max(0, _periodsLoaded.FindIndex(p => p.Id == current.Id));

        _periods.Items.Clear();
        foreach (var period in _periodsLoaded)
            _periods.Items.Add(PayPeriodLabels.Label(period, _periodsLoaded));

        if (_periodsLoaded.Count > 0) _periods.SelectedIndex = index;
        RefreshSummary();
    }

    private PayPeriod? Period =>
        _periods.SelectedIndex >= 0 && _periods.SelectedIndex < _periodsLoaded.Count
            ? _periodsLoaded[_periods.SelectedIndex]
            : null;

    private List<Employee> SelectedEmployees()
    {
        return _who.SelectedIndex switch
        {
            1 => _employees.Where(e => e.PayType == PayType.Salary).ToList(),
            2 => _employees.Where(e => e.PayType == PayType.Hourly).ToList(),
            _ => _employees,
        };
    }

    private void RefreshSummary()
    {
        var period = Period;
        if (period is null)
        {
            _summary.Text = "No pay periods yet.";
            return;
        }

        var rows = CsvExporter.BuildRows(Services.Store, period, Services.Settings, SelectedEmployees());
        int unapproved = rows.Count(r => !r.Approved);

        _summary.Text = $"{rows.Count} employees · {rows.Sum(r => r.Result.TotalMinutes) / 60.0:0.00} hours total";
        _warning.Text = unapproved == 0
            ? ""
            : $"{unapproved} time card(s) in this period are not approved yet.";
    }

    private void RunExport()
    {
        var period = Period;
        if (period is null) return;

        var rows = CsvExporter.BuildRows(Services.Store, period, Services.Settings, SelectedEmployees());
        if (rows.Count == 0)
        {
            PromptDialog.Warn(this, "There is no time to export for this period.");
            return;
        }

        var options = new ExportOptions
        {
            DecimalHours = _decimal.Checked,
            QuoteFields = _quotes.Checked,
            IncludeHeader = _titles.Checked,
            ApprovedOnly = _approvedOnly.Checked,
        };

        int unapproved = rows.Count(r => !r.Approved);
        if (unapproved > 0 && !_approvedOnly.Checked && Services.Settings.RequireApprovalBeforeExport)
        {
            if (!PromptDialog.Confirm(this,
                    $"{unapproved} time card(s) are not approved. Export anyway?"))
                return;
        }

        string baseName = $"payroll-{period.Start:yyyy-MM-dd}";
        using var dialog = new SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv|Text file (*.txt)|*.txt",
            FileName = baseName + ".csv",
            Title = "Export payroll",
        };

        string format = _format.SelectedItem?.ToString() ?? "Payroll summary (CSV)";
        if (format.Contains("ASCII")) dialog.FileName = baseName + ".txt";
        if (format.Contains("PDF"))
        {
            dialog.Filter = "PDF file (*.pdf)|*.pdf";
            dialog.FileName = baseName + ".pdf";
        }

        string rowFormatter(int minutes) => _decimal.Checked
            ? (minutes / 60.0).ToString("0.00")
            : $"{minutes / 60}:{minutes % 60:00}";

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            switch (format)
            {
                case "Generic ASCII payroll file":
                    CsvExporter.WriteGenericAscii(rows, dialog.FileName, options);
                    break;
                case "Daily detail (CSV)":
                    CsvExporter.WriteDailyDetail(rows, dialog.FileName, options);
                    break;
                case "Raw punches (CSV)":
                    CsvExporter.WritePunches(Services.Store, period.Start, period.End.AddDays(1), dialog.FileName, options);
                    break;
                case "QuickBooks-friendly summary (CSV)":
                    WriteQuickBooks(rows, dialog.FileName, options);
                    break;
                case "Payroll summary (PDF)":
                {
                    var table = PdfReports.FromPayrollRows(rows, new PdfReports.PayPeriodLabelContext
                    {
                        Subtitle = $"{period.Start:MMM d} – {period.End:MMM d, yyyy}",
                        Hours = m => rowFormatter(m),
                    });
                    PdfReports.Write(table, dialog.FileName, Services.Settings.CompanyName);
                    break;
                }
                default:
                    CsvExporter.WritePayrollSummary(rows, dialog.FileName, options);
                    break;
            }

            Services.Store.Audit("Payroll exported", $"{format} {period.Label}");
            PromptDialog.Info(this, $"Exported {rows.Count} employee(s) to\n{dialog.FileName}");
        }
        catch (Exception ex)
        {
            PromptDialog.Warn(this, "Export failed: " + ex.Message);
        }
    }

    /// <summary>One line per employee/date in the shape QuickBooks' CSV import expects.</summary>
    private static void WriteQuickBooks(List<TimeCardExportRow> rows, string path, ExportOptions options)
    {
        var sb = new System.Text.StringBuilder();
        void Line(params string[] cells)
        {
            sb.Append(string.Join(options.FieldSeparator, cells.Select(c =>
                options.QuoteFields ? "\"" + c.Replace("\"", "\"\"") + "\"" : c)));
            sb.Append(options.LineTerminator);
        }

        if (options.IncludeHeader) Line("Employee", "Date", "Hours", "Service Item", "Note");

        foreach (var row in rows)
        {
            if (options.ApprovedOnly && !row.Approved) continue;

            foreach (var day in row.Result.Days.Where(d => d.TotalMinutes > 0))
            {
                Line(
                    row.Employee.SortName,
                    day.WorkDate.ToString("MM/dd/yyyy"),
                    row.FormatHours(day.TotalMinutes, options.DecimalHours),
                    "Regular",
                    day.Issues.Count > 0 ? string.Join("; ", day.Issues) : "");
            }
        }

        File.WriteAllText(path, sb.ToString(), new System.Text.UTF8Encoding(true));
    }

    private void ShowApprovals()
    {
        var period = Period;
        if (period is null) return;

        var table = ReportBuilder.Unapproved(Services.Store, period, Services.Settings, _employees);
        if (table.Rows.Count == 0)
        {
            PromptDialog.Info(this, "Every time card in this period is approved.");
            return;
        }

        using var preview = new PrintPreviewDialog
        {
            Document = new TablePrintDocument(table, Services.Settings.CompanyName),
            Width = 900,
            Height = 700,
            Text = "Unapproved time cards",
        };
        preview.ShowDialog(this);
    }
}

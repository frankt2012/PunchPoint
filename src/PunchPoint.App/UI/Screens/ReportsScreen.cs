using PunchPoint.App.UI;
using PunchPoint.Core.Export;
using PunchPoint.Core.Models;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.App.UI.Screens;

public sealed class ReportsScreen : ScreenBase
{
    private readonly ComboBox _periods = Theme.Dropdown(300);
    private readonly DataGridView _grid = new();
    private readonly Label _summary = new() { AutoSize = true, Font = Theme.Body, ForeColor = Theme.TextMuted };
    private List<PayPeriod> _loaded = new();

    public ReportsScreen(AppServices services)
        : base(services, "Reports", "Exports and printable summaries")
    {
        _periods.Margin = new Padding(0, 8, 0, 0);
        _periods.SelectedIndexChanged += (_, _) => LoadPreview();
        Actions.Controls.Add(_periods);

        var stack = new StackPanel(14);
        Body.Controls.Add(stack);

        var exportCard = Theme.Card();
        var exportStack = new StackPanel(10);
        exportStack.Add(new Label
        {
            Text = "Export",
            Font = Theme.H3,
            ForeColor = Theme.Text,
            AutoSize = true,
        });
        exportStack.Add(new Label
        {
            Text = "Files open in Excel, Numbers or Google Sheets. Use the payroll summary for your payroll " +
                   "provider, and the daily detail when you need to check individual days.",
            Font = Theme.Small,
            ForeColor = Theme.TextMuted,
            AutoSize = true,
            MaximumSize = new Size(800, 0),
        });

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
        var payroll = Theme.PrimaryButton("Payroll summary (CSV)", 190);
        payroll.Click += (_, _) => ExportPayroll();
        var daily = Theme.SecondaryButton("Daily detail (CSV)", 160);
        daily.Click += (_, _) => ExportDaily();
        var punches = Theme.SecondaryButton("All punches (CSV)", 160);
        punches.Click += (_, _) => ExportPunches();
        var print = Theme.SecondaryButton("Print all time cards", 170);
        print.Click += (_, _) => PrintAll();
        buttons.Controls.Add(payroll);
        buttons.Controls.Add(daily);
        buttons.Controls.Add(punches);
        buttons.Controls.Add(print);
        exportStack.Add(buttons);
        exportCard.Controls.Add(exportStack);
        stack.Add(exportCard);

        var previewCard = Theme.Card(0);
        previewCard.Height = 440;
        previewCard.AutoSize = false;

        var previewHeader = new Panel { Dock = DockStyle.Top, Height = 66, Padding = new Padding(18, 14, 18, 0) };
        previewHeader.Controls.Add(new Label
        {
            Text = "Period summary",
            Font = Theme.H3,
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(16, 12),
        });
        _summary.Location = new Point(18, 40);
        previewHeader.Controls.Add(_summary);

        Theme.StyleGrid(_grid);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Employee", FillWeight = 190 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Regular", FillWeight = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "OT1", FillWeight = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "OT2", FillWeight = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Holiday", FillWeight = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Total", FillWeight = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Approved", FillWeight = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Notes", FillWeight = 220 });

        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 0, 18, 16) };
        host.Controls.Add(_grid);

        previewCard.Controls.Add(host);
        previewCard.Controls.Add(previewHeader);
        stack.Add(previewCard);
    }

    public override void RefreshData()
    {
        _loaded = Services.Store.ListPayPeriods().OrderByDescending(p => p.Start).ToList();
        var current = Services.Store.GetCurrentPayPeriod();
        int select = current is null ? 0 : Math.Max(0, _loaded.FindIndex(p => p.Id == current.Id));

        _periods.Items.Clear();
        foreach (var period in _loaded)
            _periods.Items.Add($"{period.Start:MMM d} – {period.End:MMM d, yyyy}  ({period.Type})");

        if (_loaded.Count > 0) _periods.SelectedIndex = select;
        LoadPreview();
    }

    private PayPeriod? CurrentPeriod =>
        _periods.SelectedIndex >= 0 && _periods.SelectedIndex < _loaded.Count ? _loaded[_periods.SelectedIndex] : null;

    private void LoadPreview()
    {
        var period = CurrentPeriod;
        _grid.Rows.Clear();
        if (period is null)
        {
            _summary.Text = "No pay periods yet.";
            return;
        }

        var rows = CsvExporter.BuildRows(Services.Store, period, Services.Settings);
        int total = 0;

        foreach (var row in rows)
        {
            total += row.Result.TotalMinutes;
            int index = _grid.Rows.Add(
                row.Employee.SortName,
                Fmt.Hours(row.Result.RegularMinutes),
                Fmt.HoursOrDash(row.Result.OverTime1Minutes),
                Fmt.HoursOrDash(row.Result.OverTime2Minutes),
                Fmt.HoursOrDash(row.Result.HolidayMinutes),
                Fmt.Hours(row.Result.TotalMinutes),
                row.Approved ? "Yes" : "",
                row.Result.Issues.Count > 0 ? $"{row.Result.Issues.Count} punch issue(s)" : "");

            if (row.Result.Issues.Count > 0)
                _grid.Rows[index].Cells[7].Style.ForeColor = Theme.Warning;
        }

        _summary.Text = $"{rows.Count} employees · {Fmt.Hours(total)} hours total";
    }

    private void ExportPayroll()
    {
        var period = CurrentPeriod;
        if (period is null) return;

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv",
            FileName = $"payroll-{period.Start:yyyy-MM-dd}.csv",
            Title = "Save payroll summary",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var rows = CsvExporter.BuildRows(Services.Store, period, Services.Settings);
        if (!ConfirmUnapproved(rows)) return;
        CsvExporter.WritePayrollSummary(rows, dialog.FileName);
        OfferOpen(dialog.FileName);
    }

    private bool ConfirmUnapproved(List<TimeCardExportRow> rows)
    {
        if (!Services.Settings.RequireApprovalBeforeExport) return true;

        int unapproved = rows.Count(r => !r.Approved);
        if (unapproved == 0) return true;

        return PromptDialog.Confirm(this,
            $"{unapproved} of {rows.Count} time cards in this period are not approved. Export anyway?");
    }

    private void ExportDaily()
    {
        var period = CurrentPeriod;
        if (period is null) return;

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv",
            FileName = $"daily-{period.Start:yyyy-MM-dd}.csv",
            Title = "Save daily detail",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var rows = CsvExporter.BuildRows(Services.Store, period, Services.Settings);
        CsvExporter.WriteDailyDetail(rows, dialog.FileName);
        OfferOpen(dialog.FileName);
    }

    private void ExportPunches()
    {
        var period = CurrentPeriod;
        if (period is null) return;

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv",
            FileName = $"punches-{period.Start:yyyy-MM-dd}.csv",
            Title = "Save punches",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        CsvExporter.WritePunches(Services.Store, period.Start, period.End.AddDays(1), dialog.FileName);
        OfferOpen(dialog.FileName);
    }

    private void PrintAll()
    {
        var period = CurrentPeriod;
        if (period is null) return;

        var document = new MultiTimeCardPrintDocument(Services, period);
        using var preview = new PrintPreviewDialog
        {
            Document = document,
            Width = 1000,
            Height = 760,
            Text = $"Time cards — {period.Label}",
        };
        preview.ShowDialog(this);
    }

    private void OfferOpen(string path)
    {
        if (PromptDialog.Confirm(this, "Export finished.\n\nOpen the file now?"))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                PromptDialog.Warn(this, ex.Message);
            }
        }
    }
}

/// <summary>Prints a time card per employee, one after another.</summary>
public sealed class MultiTimeCardPrintDocument : System.Drawing.Printing.PrintDocument
{
    private readonly AppServices _services;
    private readonly PayPeriod _period;
    private readonly List<TimeCardResult> _cards = new();
    private int _index;

    public MultiTimeCardPrintDocument(AppServices services, PayPeriod period)
    {
        _services = services;
        _period = period;
        DocumentName = $"Time cards {period.Label}";
        DefaultPageSettings.Margins = new System.Drawing.Printing.Margins(60, 60, 60, 60);

        foreach (var employee in services.Store.ListEmployees(includeInactive: true))
        {
            var punches = services.Store.ListPunches(employee.Id, period.Start, period.End.AddDays(1));
            if (punches.Count == 0) continue;

            var shift = employee.ShiftId is int id ? services.Store.GetShift(id) : null;
            _cards.Add(TimeCardCalculator.Calculate(employee, shift, period, punches, services.Settings,
                services.Store.ListHolidays(), services.Store.GetTimeCard(employee.Id, period.Id)));
        }
    }

    protected override void OnPrintPage(System.Drawing.Printing.PrintPageEventArgs e)
    {
        if (_index >= _cards.Count)
        {
            e.HasMorePages = false;
            return;
        }

        var single = new TimeCardPrintDocument(_services.Settings, _cards[_index]);
        bool complete = single.DrawPage(e.Graphics!, e.MarginBounds);

        if (complete) _index++;
        e.HasMorePages = _index < _cards.Count;
    }
}

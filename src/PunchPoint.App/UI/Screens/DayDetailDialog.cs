using PunchPoint.App.UI;
using PunchPoint.Core.Models;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.App.UI.Screens;

/// <summary>Explains exactly how a day's paid time was produced.</summary>
public sealed class DayDetailDialog : Form
{
    private readonly AppServices _services;
    private readonly Employee _employee;
    private readonly PayPeriod _period;
    private readonly DayTotals _day;

    public DayDetailDialog(AppServices services, Employee employee, PayPeriod period, DayTotals day)
    {
        Theme.ApplyTitleBar(this);
        _services = services;
        _employee = employee;
        _period = period;
        _day = day;

        Text = $"{employee.SortName} — {day.WorkDate:dddd, MMMM d}";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(780, 620);
        MinimumSize = new Size(700, 520);
        BackColor = Theme.Surface;
        Font = Theme.Body;
        Padding = new Padding(22);

        var header = new Panel { Dock = DockStyle.Top, Height = 64 };
        header.Controls.Add(new Label
        {
            Text = $"{employee.SortName} · {day.WorkDate:dddd, MMMM d}",
            Font = Theme.H2,
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(0, 0),
        });
        header.Controls.Add(new Label
        {
            Text = $"{Fmt.Hours(day.TotalMinutes)} hours paid" +
                   (day.IsHoliday ? $" · {day.HolidayName} (paid holiday)" : ""),
            Font = Theme.Small,
            ForeColor = Theme.TextMuted,
            AutoSize = true,
            Location = new Point(0, 32),
        });

        var segments = new DataGridView();
        Theme.StyleGrid(segments);
        segments.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "IN", FillWeight = 80 });
        segments.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "OUT", FillWeight = 80 });
        segments.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Break", FillWeight = 70 });
        segments.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Paid", FillWeight = 70 });
        segments.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "How it was calculated", FillWeight = 420 });
        segments.RowTemplate.Height = 46;
        segments.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

        foreach (var segment in day.Segments)
        {
            string explanation = segment.Adjustments.Count == 0
                ? "No adjustments"
                : string.Join("  ·  ", segment.Adjustments);

            segments.Rows.Add(
                Fmt.Time(segment.PaidIn),
                segment.PaidOut is null ? "—" : Fmt.Time(segment.PaidOut.Value),
                segment.BreakMinutes > 0 ? $"{segment.BreakMinutes} min" : "—",
                $"{segment.PaidMinutes} min",
                explanation);
        }

        var gridCard = new Panel { Dock = DockStyle.Fill, BackColor = Theme.SurfaceAlt, Padding = new Padding(10) };
        gridCard.Controls.Add(segments);

        var issues = new Panel { Dock = DockStyle.Bottom, Height = 96, Padding = new Padding(0, 10, 0, 0) };
        if (day.Issues.Count > 0)
        {
            issues.Controls.Add(new Label
            {
                Text = "Needs attention:\n• " + string.Join("\n• ", day.Issues),
                ForeColor = Theme.Warning,
                Font = Theme.Small,
                Dock = DockStyle.Fill,
            });
        }
        else
        {
            issues.Controls.Add(new Label
            {
                Text = "No problems detected for this day.",
                ForeColor = Theme.TextMuted,
                Font = Theme.Small,
                Dock = DockStyle.Fill,
            });
        }

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 54,
            Padding = new Padding(0, 12, 0, 0),
        };
        var close = Theme.SecondaryButton("Close", 100);
        close.Click += (_, _) => Close();
        var edit = Theme.PrimaryButton("Edit this day's punches", 200);
        edit.Click += (_, _) => EditPunches();
        buttons.Controls.Add(close);
        buttons.Controls.Add(edit);

        Controls.Add(gridCard);
        Controls.Add(issues);
        Controls.Add(header);
        Controls.Add(buttons);
        CancelButton = close;
    }

    private void EditPunches()
    {
        using var dialog = new DayPunchesDialog(_services, _employee, _day.WorkDate);
        dialog.ShowDialog(this);
        DialogResult = DialogResult.OK;
        Close();
    }
}

/// <summary>Lists and edits the raw punches for one work day.</summary>
public sealed class DayPunchesDialog : Form
{
    private readonly AppServices _services;
    private readonly Employee _employee;
    private readonly DateTime _workDate;
    private readonly DataGridView _grid = new();
    private readonly Label _summary = new() { AutoSize = true, Font = Theme.Small, ForeColor = Theme.TextMuted };

    public DayPunchesDialog(AppServices services, Employee employee, DateTime workDate)
    {
        Theme.ApplyTitleBar(this);
        _services = services;
        _employee = employee;
        _workDate = workDate;

        Text = $"Punches — {employee.SortName} — {workDate:d}";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(680, 480);
        MinimumSize = new Size(600, 420);
        BackColor = Theme.Surface;
        Font = Theme.Body;
        Padding = new Padding(20);

        var header = new Panel { Dock = DockStyle.Top, Height = 56 };
        header.Controls.Add(new Label
        {
            Text = $"{employee.SortName} · {workDate:dddd, MMMM d, yyyy}",
            Font = Theme.H2,
            ForeColor = Theme.Text,
            AutoSize = true,
        });

        _summary.Location = new Point(0, 34);
        header.Controls.Add(_summary);

        Theme.StyleGrid(_grid);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Time", FillWeight = 110 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Type", FillWeight = 120 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Source", FillWeight = 100 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Comment", FillWeight = 220 });

        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 8) };
        host.Controls.Add(_grid);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48,
        };

        var close = Theme.SecondaryButton("Close", 100);
        close.Click += (_, _) => Close();
        var edit = Theme.SecondaryButton("Edit", 100);
        edit.Click += (_, _) => EditSelected();
        var add = Theme.PrimaryButton("Add punch", 120);
        add.Click += (_, _) => AddPunch();
        buttons.Controls.Add(close);
        buttons.Controls.Add(edit);
        buttons.Controls.Add(add);

        Controls.Add(host);
        Controls.Add(header);
        Controls.Add(buttons);
        CancelButton = close;

        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelected(); };
        LoadPunches();
    }

    private Punch? Selected =>
        _grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0].Tag as Punch : null;

    private void LoadPunches()
    {
        // Cover the work-day window including a possible day-change offset.
        var from = _workDate.AddMinutes(_services.Settings.DayChangeOffsetMinutes);
        var to = from.AddDays(1).AddMinutes(-1);

        var punches = _services.Store.ListPunches(_employee.Id, from, to);
        _grid.Rows.Clear();

        foreach (var punch in punches)
        {
            int i = _grid.Rows.Add(Fmt.Time(punch.Timestamp), KindLabel(punch.Kind),
                punch.Source.ToString(), punch.Comment);
            _grid.Rows[i].Tag = punch;
        }

        int total = 0;
        DateTime? open = null;
        foreach (var punch in punches)
        {
            if (punch.Kind == PunchKind.In) open = punch.Timestamp;
            else if (punch.Kind == PunchKind.Out && open is not null)
            {
                total += (int)Math.Round((punch.Timestamp - open.Value).TotalMinutes);
                open = null;
            }
        }

        _summary.Text = punches.Count == 0
            ? "No punches on this day."
            : $"{punches.Count} punches · {Fmt.Hours(total)} hours between first IN and last OUT" +
              (open is not null ? " · an IN punch has no matching OUT" : "");
    }

    private static string KindLabel(PunchKind kind) => kind switch
    {
        PunchKind.In => "Clock IN",
        PunchKind.Out => "Clock OUT",
        PunchKind.BreakOut => "Break start",
        PunchKind.BreakIn => "Break end",
        PunchKind.Hours => "Paid hours",
        _ => kind.ToString(),
    };

    private void AddPunch()
    {
        using var dialog = new PunchEditDialog(_services, _employee, null);
        if (dialog.ShowDialog(this) == DialogResult.OK) LoadPunches();
    }

    private void EditSelected()
    {
        var punch = Selected;
        if (punch is null) return;

        using var dialog = new PunchEditDialog(_services, _employee, punch);
        if (dialog.ShowDialog(this) == DialogResult.OK) LoadPunches();
    }
}

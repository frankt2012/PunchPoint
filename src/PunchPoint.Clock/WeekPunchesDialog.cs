using PunchPoint.App.UI;
using PunchPoint.Core.Models;
using PunchPoint.Core.Reports;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.Clock;

/// <summary>
/// An employee's own punches for the current pay period: first clock-in and last clock-out per
/// day, breaks and the total. Opened from the person card, read-only, and it closes itself so an
/// employee who walks away cannot leave the clock stuck on their card.
/// </summary>
internal sealed class WeekPunchesDialog : Form
{
    private readonly System.Windows.Forms.Timer _tick = new() { Interval = 1000 };
    private readonly Label _closing;
    private DataGridView? _grid;
    private int _secondsLeft = 60;

    public WeekPunchesDialog(WeekSummary week, AppSettings settings)
    {
        Text = "My punches";
        Theme.ApplyTitleBar(this);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        BackColor = Theme.Surface;
        Font = Theme.Body;
        ClientSize = new Size(620, 430);
        Padding = new Padding(22, 16, 22, 12);

        var heading = new Label
        {
            Text = "My punches",
            Font = Theme.H2,
            ForeColor = Theme.Text,
            Dock = DockStyle.Top,
            Height = 34,
        };

        var who = new Label
        {
            Text = $"{week.Employee.FullName}   ·   {week.Period.Start:ddd MMM d} – {week.Period.End:ddd MMM d, yyyy}" +
                   (week.Approved ? "   ·   approved" : ""),
            Font = Theme.Small,
            ForeColor = week.Approved ? Theme.Success : Theme.TextMuted,
            Dock = DockStyle.Top,
            Height = 24,
        };

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 64 };

        string hours = ReportBuilder.Hours(week.TotalMinutes, settings.TotalsFormat);
        var total = new Label
        {
            Text = week.IsEmpty
                ? ""
                : $"Total this period: {hours}" +
                  (settings.TotalsFormat == TotalsFormat.Hundredths ? " hours" : "") +
                  (week.BreakMinutes > 0 ? $"   ·   breaks {week.BreakMinutes} min" : "") +
                  (week.AdjustmentMinutes != 0
                      ? $"   ·   adjustment {(week.AdjustmentMinutes > 0 ? "+" : "−")}" +
                        ReportBuilder.Hours(Math.Abs(week.AdjustmentMinutes), settings.TotalsFormat)
                      : ""),
            Font = Theme.BodyBold,
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(0, 8),
        };

        _closing = new Label
        {
            Text = "",
            Font = Theme.Small,
            ForeColor = Theme.TextFaint,
            AutoSize = true,
            Location = new Point(0, 34),
        };

        var close = Theme.PrimaryButton("Close", 110);
        close.Click += (_, _) => Close();

        // A docked strip keeps the button inside the footer at any size (an anchored
        // position was pushed off the dialog before the panel had its final width).
        var closeStrip = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 118,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 4, 0, 0),
        };
        closeStrip.Controls.Add(close);

        footer.Controls.Add(total);
        footer.Controls.Add(_closing);
        footer.Controls.Add(closeStrip);

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            MultiSelect = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BorderStyle = BorderStyle.None,
        };
        Theme.StyleGrid(grid);
        grid.RowTemplate.Height = 32;

        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Day", FillWeight = 160 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "In", FillWeight = 100 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Out", FillWeight = 100 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Break", FillWeight = 90 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Total", FillWeight = 90 });

        foreach (var day in week.Days)
        {
            string label = day.IsHoliday
                ? $"{day.Date:ddd MMM d} · holiday"
                : day.PaidLabel is not null
                    ? $"{day.Date:ddd MMM d} · {day.PaidLabel.ToLowerInvariant()}"
                    : day.Date.Date == DateTime.Today ? $"{day.Date:ddd MMM d} · today" : day.Date.ToString("ddd MMM d");

            int index = grid.Rows.Add(
                label,
                day.FirstIn?.ToString("h:mm tt") ?? "",
                day.IsOpen ? "on the clock" : day.LastOut?.ToString("h:mm tt") ?? "",
                day.BreakMinutes > 0 ? $"{day.BreakMinutes} min" : "",
                ReportBuilder.Hours(day.TotalMinutes, settings.TotalsFormat));

            if (day.Date.Date == DateTime.Today)
                grid.Rows[index].DefaultCellStyle.Font = Theme.BodyBold;
        }

        // No selection: this is a display, not a list to pick from.
        grid.TabStop = false;
        grid.CurrentCell = null;
        grid.ClearSelection();

        _grid = grid;

        // Fit the window to the number of days (a short week should not be a tall empty box).
        int rows = Math.Max(1, week.Days.Count);
        ClientSize = new Size(620, Math.Clamp(196 + rows * 32, 300, 520));

        if (week.IsEmpty)
        {
            grid.Visible = false;
            var empty = new Label
            {
                Text = "No punches yet.\r\n\r\nYour clock-ins appear here as soon as you use the clock.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Theme.Body,
                ForeColor = Theme.TextMuted,
            };
            Controls.Add(empty);
            empty.BringToFront();
        }

        Controls.Add(grid);
        Controls.Add(footer);
        Controls.Add(who);
        Controls.Add(heading);

        AcceptButton = close;
        CancelButton = close;

        _tick.Tick += (_, _) =>
        {
            _secondsLeft--;
            _closing.Text = $"Closes automatically in {Math.Max(0, _secondsLeft)}s";
            if (_secondsLeft <= 0) Close();
        };
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _closing.Text = $"Closes automatically in {_secondsLeft}s";

        // Adding rows selects the first one; clear it once the window is real.
        if (_grid is not null)
        {
            _grid.CurrentCell = null;
            _grid.ClearSelection();
        }

        _tick.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _tick.Stop();
        _tick.Dispose();
        base.OnFormClosed(e);
    }
}

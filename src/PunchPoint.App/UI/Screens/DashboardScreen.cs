using PunchPoint.App.UI;
using PunchPoint.Core.Models;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.App.UI.Screens;

public sealed class DashboardScreen : ScreenBase
{
    private readonly Label _greeting = new() { Font = Theme.H1, ForeColor = Theme.Text, AutoSize = true };
    private readonly Label _periodLabel = new() { Font = Theme.Body, ForeColor = Theme.TextMuted, AutoSize = true };
    private readonly Label _clockedInValue = StatValue();
    private readonly Label _clockedOutValue = StatValue();
    private readonly Label _hoursValue = StatValue();
    private readonly Label _issuesValue = StatValue();
    private readonly DataGridView _grid = new();
    private readonly Panel _issuesCard = new() { Dock = DockStyle.Top, AutoSize = true, Visible = false };
    private readonly Label _issueText = new()
    {
        Font = Theme.Small,
        ForeColor = Theme.Text,
        AutoSize = true,
        MaximumSize = new Size(900, 0),
    };

    public DashboardScreen(AppServices services)
        : base(services, "Dashboard", "Today at a glance")
    {
        var stack = new StackPanel();
        Body.Controls.Add(stack);

        // Greeting card
        var greetCard = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Surface,
            Padding = new Padding(22, 18, 22, 18),
            Margin = new Padding(0, 0, 0, 14),
        };
        var greetStack = new StackPanel(4);
        greetStack.Add(_greeting);
        greetStack.Add(_periodLabel);
        greetCard.Controls.Add(greetStack);
        stack.Add(greetCard);

        // Stat row
        var stats = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 108,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 14),
        };
        for (int i = 0; i < 4; i++) stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        stats.Controls.Add(StatCard("Clocked in now", _clockedInValue, Theme.Success), 0, 0);
        stats.Controls.Add(StatCard("Not on the clock", _clockedOutValue, Theme.TextMuted), 1, 0);
        stats.Controls.Add(StatCard("Hours this period", _hoursValue, Theme.Primary), 2, 0);
        stats.Controls.Add(StatCard("Punch issues", _issuesValue, Theme.Warning), 3, 0);
        stack.Add(stats);

        // Issues banner
        _issuesCard.BackColor = Theme.WarningSoft;
        _issuesCard.Padding = new Padding(16, 12, 16, 12);
        _issuesCard.Margin = new Padding(0, 0, 0, 14);
        _issuesCard.Controls.Add(_issueText);
        stack.Add(_issuesCard);

        // Period grid card
        var gridCard = Theme.Card(0);
        gridCard.Height = 430;
        gridCard.AutoSize = false;
        var gridHeader = new Label
        {
            Text = "Current pay period",
            Font = Theme.H3,
            ForeColor = Theme.Text,
            Dock = DockStyle.Top,
            Height = 46,
            Padding = new Padding(18, 16, 0, 0),
        };

        Theme.StyleGrid(_grid);
        _grid.Columns.Add(Column("Employee", 2.2f));
        _grid.Columns.Add(Column("Clocked in", 1.1f));
        _grid.Columns.Add(Column("Regular", 0.9f));
        _grid.Columns.Add(Column("Overtime", 0.9f));
        _grid.Columns.Add(Column("Total", 0.9f));
        _grid.Columns.Add(Column("Approved", 0.9f));
        _grid.Columns.Add(Column("Issues", 0.9f));

        var gridHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 0, 18, 16) };
        gridHost.Controls.Add(_grid);

        gridCard.Controls.Add(gridHost);
        gridCard.Controls.Add(gridHeader);
        stack.Add(gridCard);

        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.RowIndex < _grid.Rows.Count)
                (FindForm() as MainForm)?.Show("timecards");
        };
    }

    private static Label StatValue() => new()
    {
        Text = "—",
        Font = new Font(Theme.Family, 26f, FontStyle.Bold),
        ForeColor = Theme.Text,
        AutoSize = true,
    };

    private static DataGridViewColumn Column(string header, float weight) => new DataGridViewTextBoxColumn
    {
        HeaderText = header,
        FillWeight = weight * 100,
        SortMode = DataGridViewColumnSortMode.NotSortable,
    };

    private static Panel StatCard(string caption, Label value, Color accent)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            Margin = new Padding(0, 0, 12, 0),
            Padding = new Padding(18, 14, 18, 12),
        };

        var captionLabel = new Label
        {
            Text = caption.ToUpperInvariant(),
            Font = new Font(Theme.Family, 7.5f, FontStyle.Bold),
            ForeColor = Theme.TextFaint,
            Dock = DockStyle.Top,
            Height = 20,
        };

        var accentBar = new Panel { Dock = DockStyle.Left, Width = 4, BackColor = accent };
        var inner = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 0, 0) };
        value.Dock = DockStyle.Bottom;
        value.Height = 44;
        inner.Controls.Add(value);
        inner.Controls.Add(captionLabel);

        card.Controls.Add(inner);
        card.Controls.Add(accentBar);
        return card;
    }

    public override void RefreshData()
    {
        var now = DateTime.Now;
        var period = Services.Store.GetCurrentPayPeriod(now);

        _greeting.Text = Greeting(now);
        _periodLabel.Text = period is null
            ? "No pay period is set up yet. Add one in Settings."
            : $"Pay period {period.Label} · {period.Type} · {period.DayCount} days";

        var employees = Services.Store.ListEmployees(includeInactive: false);
        int clockedIn = 0, clockedOut = 0, issues = 0, totalMinutes = 0;

        _grid.Rows.Clear();
        var issueLines = new List<string>();

        foreach (var employee in employees)
        {
            var status = Services.Clock.GetStatus(employee, now);
            if (status.IsClockedIn) clockedIn++; else clockedOut++;

            int regular = 0, overtime = 0, total = 0;
            int employeeIssues = 0;

            if (period is not null)
            {
                var summary = Services.SummaryFor(employee, period);
                regular = summary.RegularMinutes;
                overtime = summary.OverTime1Minutes + summary.OverTime2Minutes;
                total = summary.TotalMinutes;
                totalMinutes += total;
                employeeIssues = summary.IssueCount;

                var card = Services.Store.GetTimeCard(employee.Id, period.Id);
                bool approved = card?.Approved ?? false;

                _grid.Rows.Add(
                    employee.SortName,
                    status.IsClockedIn ? $"{Fmt.Time(status.Since ?? now)}" : "—",
                    Fmt.HoursOrDash(regular),
                    Fmt.HoursOrDash(overtime),
                    Fmt.HoursOrDash(total),
                    approved ? "Yes" : "",
                    employeeIssues > 0 ? employeeIssues.ToString() : "");

                if (employeeIssues > 0)
                    issues += employeeIssues;

                foreach (var issue in summary.Result.Issues.Take(3))
                    issueLines.Add($"{employee.SortName}: {issue}");
            }
            else
            {
                _grid.Rows.Add(employee.SortName, status.IsClockedIn ? Fmt.Time(status.Since ?? now) : "—", "", "", "", "", "");
            }

            if (status.IsClockedIn)
                _grid.Rows[_grid.Rows.Count - 1].Cells[1].Style.ForeColor = Theme.Success;
        }

        _grid.ClearSelection();

        _clockedInValue.Text = clockedIn.ToString();
        _clockedOutValue.Text = clockedOut.ToString();
        _hoursValue.Text = Fmt.Hours(totalMinutes);
        _issuesValue.Text = issues.ToString();

        if (issueLines.Count > 0)
        {
            _issuesCard.Visible = true;
            _issueText.Text = "Needs attention:\n• " + string.Join("\n• ", issueLines.Take(6));
        }
        else
        {
            _issuesCard.Visible = false;
        }
    }

    private static string Greeting(DateTime now)
    {
        string part = now.Hour < 12 ? "Good morning" : now.Hour < 17 ? "Good afternoon" : "Good evening";
        return $"{part} · {now:dddd, MMMM d}";
    }
}

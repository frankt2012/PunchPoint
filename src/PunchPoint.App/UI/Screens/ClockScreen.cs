using PunchPoint.App.UI;
using PunchPoint.Core.Models;
using PunchPoint.Core.Services;

namespace PunchPoint.App.UI.Screens;

public sealed class ClockScreen : ScreenBase
{
    private readonly Label _time = new() { AutoSize = true };
    private readonly Label _date = new() { AutoSize = true };
    private readonly Label _message = new() { AutoSize = true };
    private readonly Label _feedback = new() { AutoSize = true, Visible = false };

    private readonly Panel _pinCard = new() { Dock = DockStyle.Fill, BackColor = Theme.Surface };
    private readonly Panel _personCard = new() { Dock = DockStyle.Fill, BackColor = Theme.Surface, Visible = false };

    private readonly TextBox _pin = new();
    private readonly Label _pinError = new() { AutoSize = true, ForeColor = Theme.Danger, Font = Theme.SmallBold };
    private readonly Label _personName = new() { AutoSize = true };
    private readonly Label _personState = new() { AutoSize = true };
    private readonly Label _personDetail = new() { AutoSize = true };
    private readonly Label _countdown = new() { AutoSize = true, ForeColor = Theme.TextFaint, Font = Theme.Small };
    private readonly FlowLayoutPanel _personButtons = new() { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
    private readonly DataGridView _recent = new();
    private readonly Label _recentEmpty = new() { AutoSize = true, ForeColor = Theme.TextMuted, Font = Theme.Small };

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private Employee? _selected;
    private bool _hasEmployees;
    private DateTime _resetAt;
    private bool _showingResult;

    public ClockScreen(AppServices services)
        : base(services, "Clock", "Employees clock in and out here")
    {
        var split = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        Body.Controls.Add(split);

        // ---------------- left: clock + activity ----------------
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 0, 14, 0) };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 250));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var clockCard = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(28, 22, 28, 18) };
        _time.Font = new Font(Theme.Family, 54f, FontStyle.Bold);
        _time.ForeColor = Theme.Text;
        _time.Location = new Point(24, 6);
        _date.Font = new Font(Theme.Family, 13f);
        _date.ForeColor = Theme.TextMuted;
        _date.Location = new Point(28, 96);
        _message.Font = new Font(Theme.Family, 11f, FontStyle.Italic);
        _message.ForeColor = Theme.Primary;

        var clockStack = new Panel { Dock = DockStyle.Fill };
        clockStack.Controls.Add(_time);
        clockStack.Controls.Add(_date);
        _message.Location = new Point(28, 136);
        _message.MaximumSize = new Size(560, 0);
        clockStack.Controls.Add(_message);

        _feedback.Font = new Font(Theme.Family, 12f, FontStyle.Bold);
        _feedback.Location = new Point(0, 190);
        clockStack.Controls.Add(_feedback);
        _feedback.BringToFront();

        clockCard.Controls.Add(clockStack);

        left.Controls.Add(clockCard, 0, 0);

        var recentCard = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(18, 14, 18, 14), Margin = new Padding(0, 14, 0, 0) };
        var recentTitle = new Label
        {
            Text = "Recent activity",
            Font = Theme.H3,
            ForeColor = Theme.Text,
            Dock = DockStyle.Top,
            Height = 34,
        };
        Theme.StyleGrid(_recent);
        _recent.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Employee", FillWeight = 200 });
        _recent.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Punch", FillWeight = 110 });
        _recent.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Time", FillWeight = 110 });
        _recent.RowTemplate.Height = 30;

        var recentHost = new Panel { Dock = DockStyle.Fill };
        recentHost.Controls.Add(_recent);
        _recentEmpty.Location = new Point(8, 48);
        recentHost.Controls.Add(_recentEmpty);

        recentCard.Controls.Add(recentHost);
        recentCard.Controls.Add(recentTitle);
        left.Controls.Add(recentCard, 0, 1);

        // ---------------- right: keypad / person ----------------
        var rightCard = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface };
        rightCard.Controls.Add(_personCard);
        rightCard.Controls.Add(_pinCard);

        BuildPinCard();
        BuildPersonCard();

        split.Controls.Add(left, 0, 0);
        split.Controls.Add(rightCard, 1, 0);

        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        Tick();
    }

    private void BuildPinCard()
    {
        var stack = new StackPanel(10) { Padding = new Padding(26, 22, 26, 20) };
        _pinCard.Controls.Add(stack);

        stack.Add(new Label
        {
            Text = "Enter your PIN",
            Font = Theme.H2,
            ForeColor = Theme.Text,
            AutoSize = true,
        });

        _pin.Font = new Font(Theme.Family, 26f, FontStyle.Bold);
        _pin.TextAlign = HorizontalAlignment.Center;
        _pin.UseSystemPasswordChar = true;
        _pin.MaxLength = 12;
        _pin.Height = 56;
        _pin.BorderStyle = BorderStyle.FixedSingle;
        _pin.Dock = DockStyle.Top;
        _pin.TextChanged += (_, _) =>
        {
            _pinError.Text = "";
            if (_pin.Text.Length >= 4) TryIdentify();
        };
        _pin.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                TryIdentify();
            }
        };
        stack.Add(_pin);
        stack.Add(_pinError);

        var pad = new TableLayoutPanel
        {
            ColumnCount = 3,
            RowCount = 4,
            Dock = DockStyle.Top,
            Height = 300,
            Margin = new Padding(0, 6, 0, 0),
        };
        for (int i = 0; i < 3; i++) pad.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        for (int i = 0; i < 4; i++) pad.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));

        string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "Clear", "0", "⌫" };
        for (int i = 0; i < keys.Length; i++)
        {
            string key = keys[i];
            var button = new Button
            {
                Text = key,
                Dock = DockStyle.Fill,
                Margin = new Padding(5),
                Font = key.Length > 1 ? Theme.Body : new Font(Theme.Family, 20f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = key == "Clear" || key == "⌫" ? Theme.SurfaceAlt : Color.White,
                ForeColor = Theme.Text,
                Cursor = Cursors.Hand,
            };
            button.FlatAppearance.BorderColor = Theme.BorderStrong;
            button.FlatAppearance.BorderSize = 1;
            button.Click += (_, _) => PressKey(key);
            pad.Controls.Add(button, i % 3, i / 3);
        }

        stack.Add(pad);
    }

    private void BuildPersonCard()
    {
        var stack = new StackPanel(8) { Padding = new Padding(26, 26, 26, 20) };
        _personCard.Controls.Add(stack);

        _personName.Font = new Font(Theme.Family, 22f, FontStyle.Bold);
        _personName.ForeColor = Theme.Text;

        _personState.Font = new Font(Theme.Family, 14f, FontStyle.Bold);
        _personState.ForeColor = Theme.TextMuted;

        _personDetail.Font = Theme.Body;
        _personDetail.ForeColor = Theme.TextMuted;

        stack.Add(_personName);
        stack.Add(_personState);
        stack.Add(_personDetail);

        var spacer = new Panel { Height = 18, Dock = DockStyle.Top };
        stack.Add(spacer);

        _personButtons.Dock = DockStyle.Top;
        stack.Add(_personButtons);

        _countdown.Dock = DockStyle.Top;
        _countdown.Margin = new Padding(0, 18, 0, 0);
        stack.Add(_countdown);
    }

    private void PressKey(string key)
    {
        if (_selected is not null || _showingResult) return;

        switch (key)
        {
            case "Clear":
                _pin.Clear();
                break;
            case "⌫":
                if (_pin.Text.Length > 0)
                    _pin.Text = _pin.Text[..^1];
                break;
            default:
                if (_pin.Text.Length < _pin.MaxLength)
                    _pin.Text += key;
                break;
        }
    }

    private void TryIdentify()
    {
        string pin = _pin.Text.Trim();
        if (pin.Length == 0) return;

        var employee = Services.Store.FindByPin(pin) ?? Services.Store.FindByBadge(pin);
        if (employee is null)
        {
            _pinError.Text = "PIN not recognised. Try again or ask your supervisor.";
            _pin.Clear();
            return;
        }

        ShowPerson(employee);
    }

    private void ShowPerson(Employee employee)
    {
        _selected = employee;
        _pin.Clear();
        _pinError.Text = "";
        _feedback.Visible = false;
        _resetAt = DateTime.Now.AddSeconds(30);

        var status = Services.Clock.GetStatus(employee);

        _personName.Text = employee.FullName;
        _personState.Text = status.StateLabel;
        _personState.ForeColor = status.IsClockedIn ? Theme.Success : Theme.TextMuted;

        string detail = status.IsClockedIn
            ? $"On the clock since {Fmt.Time(status.Since ?? DateTime.Now)}"
            : $"Worked today: {Fmt.Hours(status.MinutesToday)} hours";
        if (!string.IsNullOrWhiteSpace(employee.Message))
            detail += $"\n{employee.Message}";
        _personDetail.Text = detail;

        _personButtons.Controls.Clear();

        var primary = Theme.PrimaryButton(status.ActionLabel, 320);
        primary.Height = 58;
        primary.Font = new Font(Theme.Family, 14f, FontStyle.Bold);
        primary.BackColor = status.IsClockedIn ? Theme.Danger : Theme.Success;
        primary.Click += (_, _) => PerformPunch(status.NextKind);
        _personButtons.Controls.Add(primary);

        if (status.IsClockedIn && !status.OnBreak)
        {
            var breakButton = Theme.SecondaryButton("Start unpaid break", 320);
            breakButton.Height = 42;
            breakButton.Click += (_, _) => PerformPunch(PunchKind.BreakOut);
            _personButtons.Controls.Add(breakButton);
        }

        var cancel = Theme.SecondaryButton("Not me", 320);
        cancel.Height = 38;
        cancel.Click += (_, _) => ResetKiosk();
        _personButtons.Controls.Add(cancel);

        _pinCard.Visible = false;
        _personCard.Visible = true;
        _personCard.BringToFront();
    }

    private void PerformPunch(PunchKind kind)
    {
        if (_selected is null) return;

        try
        {
            var now = DateTime.Now;
            var punch = Services.Clock.Punch(_selected, kind, now);

            string label = kind switch
            {
                PunchKind.In => "Clocked in",
                PunchKind.Out => "Clocked out",
                PunchKind.BreakOut => "Break started",
                PunchKind.BreakIn => "Break ended",
                _ => "Punch recorded",
            };

            _showingResult = true;
            _resetAt = DateTime.Now.AddSeconds(5);
            _personButtons.Controls.Clear();
            _personState.Text = $"{label} at {Fmt.Time(punch.Timestamp)}";
            _personState.ForeColor = kind == PunchKind.In ? Theme.Success : Theme.Text;
            _personDetail.Text = kind == PunchKind.Out
                ? "Have a good evening!"
                : "Thank you.";

            _feedback.Visible = true;
            _feedback.ForeColor = Theme.Success;
            _feedback.Text = $"{_selected.FirstName} — {label} {Fmt.Time(punch.Timestamp)}";

            RefreshRecent();
            Services.NotifyDataChanged();
        }
        catch (Exception ex)
        {
            PromptDialog.Warn(this, ex.Message);
            ResetKiosk();
        }
    }

    private void ResetKiosk()
    {
        _selected = null;
        _showingResult = false;
        _pin.Clear();
        _pinError.Text = "";
        _personCard.Visible = false;
        _pinCard.Visible = true;
        _pinCard.BringToFront();
        _pin.Focus();
    }

    private void Tick()
    {
        var now = DateTime.Now;
        _time.Text = now.ToString("h:mm:ss tt");
        _date.Text = now.ToString("dddd, MMMM d, yyyy");
        _message.Text = !_hasEmployees
            ? "No employees yet — open Import to bring your timeQplus data across, or add people in Employees."
            : Services.Settings.CompanyMessage;

        if (_selected is not null && !_showingResult)
        {
            int seconds = (int)Math.Max(0, (_resetAt - now).TotalSeconds);
            _countdown.Text = $"Returns to the PIN screen in {seconds}s";
            if (seconds <= 0) ResetKiosk();
        }
        else if (_showingResult)
        {
            int seconds = (int)Math.Max(0, (_resetAt - now).TotalSeconds);
            _countdown.Text = $"Returns to the PIN screen in {seconds}s";
            if (seconds <= 0) ResetKiosk();
        }
        else if (Visible && _pinCard.Visible && !_pin.Focused)
        {
            _pin.Focus();
        }
    }

    public override void RefreshData()
    {
        _hasEmployees = Services.Store.ListEmployees(includeInactive: true).Count > 0;
        _message.Text = _hasEmployees
            ? Services.Settings.CompanyMessage
            : "No employees yet — open Import to bring your timeQplus data across, or add people in Employees.";
        RefreshRecent();

        // While a confirmation is on screen, leave it alone; the person card is
        // rebuilt when the kiosk returns to the PIN pad.
        if (_selected is not null && !_showingResult && Visible)
        {
            var refreshed = Services.Store.GetEmployee(_selected.Id);
            if (refreshed is not null) ShowPerson(refreshed);
        }
    }

    private void RefreshRecent()
    {
        var employees = Services.Store.ListEmployees(includeInactive: true).ToDictionary(e => e.Id);
        var punches = Services.Store
            .ListPunches(DateTime.Today.AddDays(-1), DateTime.Now.AddHours(1))
            .OrderByDescending(p => p.Timestamp)
            .Take(9)
            .ToList();

        _recent.Rows.Clear();
        foreach (var punch in punches)
        {
            var employee = employees.GetValueOrDefault(punch.EmployeeId);
            _recent.Rows.Add(
                employee?.SortName ?? "—",
                punch.Kind switch
                {
                    PunchKind.In => "IN",
                    PunchKind.Out => "OUT",
                    PunchKind.BreakOut => "Break",
                    PunchKind.BreakIn => "Back",
                    PunchKind.Hours => "Hours",
                    _ => punch.Kind.ToString(),
                },
                Fmt.Time(punch.Timestamp));
        }

        _recentEmpty.Visible = punches.Count == 0;
        _recentEmpty.Text = "No punches yet today.";
    }
}

using PunchPoint.App.UI;
using PunchPoint.Core.Models;
using PunchPoint.Core.Services;
using PunchPoint.Core.Storage;

namespace PunchPoint.Clock;

/// <summary>
/// The employee-facing clock. It is a separate program with no navigation, no employee
/// editing and no time-card screens: employees can only clock in and out.
/// Closing it requires the supervisor password.
/// </summary>
public sealed class KioskForm : Form
{
    private readonly Store _store;
    private readonly ClockService _clock;
    private readonly bool _fullScreen;

    private readonly Label _time = new();
    private readonly Label _date = new();
    private readonly Label _message = new();
    private readonly Label _feedback = new() { Visible = false };

    private readonly Panel _pinPanel = new();
    private readonly Panel _personPanel = new();
    private readonly TextBox _pin = new();
    private readonly Label _pinError = new();
    private readonly Label _personName = new();
    private readonly Label _personState = new();
    private readonly Label _personDetail = new();
    private readonly Label _countdown = new();
    private readonly FlowLayoutPanel _personButtons = new();
    private readonly DataGridView _recent = new();
    private readonly Label _recentEmpty = new();

    private readonly System.Windows.Forms.Timer _tick = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer _refresh = new() { Interval = 5000 };

    private Employee? _selected;
    private DateTime _resetAt;
    private bool _allowClose;
    private bool _hasEmployees;
    private AppSettings _settings;

    public KioskForm(Store store, bool fullScreen)
    {
        _store = store;
        _clock = new ClockService(store);
        _settings = store.GetSettings();
        _fullScreen = fullScreen;

        Text = "PunchPoint Clock";
        BackColor = Theme.Canvas;
        Font = Theme.Body;
        KeyPreview = true;

        if (fullScreen)
        {
            // Dedicated clock PC: locked full screen, above everything.
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
        }
        else
        {
            // Shared PC: an ordinary window that can be minimised while other programs run.
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(
                Math.Clamp(_settings.ClockWidth, 820, 2400),
                Math.Clamp(_settings.ClockHeight, 620, 1600));
            MinimumSize = new Size(820, 620);
            TopMost = false;
            MaximizeBox = true;
            MinimizeBox = true;
        }

        BuildUi();
        _tick.Tick += (_, _) => OnTick();
        _refresh.Tick += (_, _) => RefreshRecent();
        _tick.Start();
        _refresh.Start();

        RefreshRecent();
        OnTick();
        Shown += (_, _) => _pin.Focus();
    }

    // -----------------------------------------------------------------
    // Layout
    // -----------------------------------------------------------------

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(22),
            BackColor = Theme.Canvas,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 10, 0),
        };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 250));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Clock card
        var clockCard = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(28, 20, 28, 16) };

        _time.Font = new Font(Theme.Family, 58f, FontStyle.Bold);
        _time.ForeColor = Theme.Text;
        _time.AutoSize = true;
        _time.Location = new Point(24, 0);

        _date.Font = new Font(Theme.Family, 13f);
        _date.ForeColor = Theme.TextMuted;
        _date.AutoSize = true;
        _date.Location = new Point(28, 100);

        _message.Font = new Font(Theme.Family, 11f, FontStyle.Italic);
        _message.ForeColor = Theme.Primary;
        _message.AutoSize = true;
        _message.MaximumSize = new Size(700, 0);
        _message.Location = new Point(28, 140);

        _feedback.Font = new Font(Theme.Family, 13f, FontStyle.Bold);
        _feedback.AutoSize = true;
        _feedback.Location = new Point(28, 195);

        clockCard.Controls.Add(_time);
        clockCard.Controls.Add(_date);
        clockCard.Controls.Add(_message);
        clockCard.Controls.Add(_feedback);
        left.Controls.Add(clockCard, 0, 0);

        // Recent activity
        var recentCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            Padding = new Padding(16, 12, 16, 12),
            Margin = new Padding(0, 12, 0, 0),
        };

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

        _recentEmpty.AutoSize = true;
        _recentEmpty.Font = Theme.Small;
        _recentEmpty.ForeColor = Theme.TextMuted;
        _recentEmpty.Location = new Point(6, 46);

        var recentHost = new Panel { Dock = DockStyle.Fill };
        recentHost.Controls.Add(_recent);
        recentHost.Controls.Add(_recentEmpty);
        _recentEmpty.BringToFront();

        recentCard.Controls.Add(recentHost);
        recentCard.Controls.Add(recentTitle);
        left.Controls.Add(recentCard, 0, 1);

        // Right side: PIN pad or person card
        var rightCard = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Margin = new Padding(10, 0, 0, 0) };
        BuildPinPanel();
        BuildPersonPanel();
        rightCard.Controls.Add(_personPanel);
        rightCard.Controls.Add(_pinPanel);

        root.Controls.Add(left, 0, 0);
        root.Controls.Add(rightCard, 1, 0);
        Controls.Add(root);

        // Supervisor exit. It always asks for the password once one is set.
        var exit = new Button
        {
            Text = "Supervisor",
            FlatStyle = FlatStyle.Flat,
            Size = new Size(96, 30),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Theme.Canvas,
            ForeColor = Theme.TextFaint,
            Font = Theme.Small,
            Cursor = Cursors.Hand,
            TabStop = false,
        };
        exit.FlatAppearance.BorderSize = 0;
        exit.Location = new Point(ClientSize.Width - 106, 8);
        exit.Click += (_, _) => SupervisorExit();
        Controls.Add(exit);
        exit.BringToFront();
        Resize += (_, _) => exit.Location = new Point(ClientSize.Width - 106, 8);
    }

    private void BuildPinPanel()
    {
        _pinPanel.Dock = DockStyle.Fill;

        var stack = new StackPanel(10)
        {
            Padding = new Padding(30, 14, 30, 12),
            Dock = DockStyle.Fill,
        };
        _pinPanel.Controls.Add(stack);

        stack.Add(new Label { Text = "Enter your PIN", Font = Theme.H2, ForeColor = Theme.Text, AutoSize = true });

        _pin.Font = new Font(Theme.Family, 26f, FontStyle.Bold);
        _pin.TextAlign = HorizontalAlignment.Center;
        _pin.UseSystemPasswordChar = true;
        _pin.MaxLength = 12;
        _pin.Height = 54;
        _pin.BorderStyle = BorderStyle.FixedSingle;
        _pin.BackColor = Theme.InputBackground;
        _pin.ForeColor = Theme.Text;
        _pin.Dock = DockStyle.Top;
        _pin.TextChanged += (_, _) =>
        {
            _pinError.Text = "";
            if (_pin.Text.Length >= 4) Identify();
        };
        _pin.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                Identify();
            }
        };
        stack.Add(_pin);

        _pinError.AutoSize = true;
        _pinError.ForeColor = Theme.Danger;
        _pinError.Font = Theme.SmallBold;
        stack.Add(_pinError);

        var pad = new TableLayoutPanel
        {
            ColumnCount = 3,
            RowCount = 4,
            Dock = DockStyle.Top,
            Height = 320,
            Margin = new Padding(0, 8, 0, 0),
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
                Font = key.Length > 1 ? Theme.Body : new Font(Theme.Family, 22f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.InputBackground,
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

    private void BuildPersonPanel()
    {
        _personPanel.Dock = DockStyle.Fill;
        _personPanel.Visible = false;

        var stack = new StackPanel(8) { Padding = new Padding(26, 30, 26, 18) };
        _personPanel.Controls.Add(stack);

        _personName.Font = new Font(Theme.Family, 24f, FontStyle.Bold);
        _personName.ForeColor = Theme.Text;
        _personName.AutoSize = true;

        _personState.Font = new Font(Theme.Family, 15f, FontStyle.Bold);
        _personState.ForeColor = Theme.TextMuted;
        _personState.AutoSize = true;

        _personDetail.Font = new Font(Theme.Family, 10.5f);
        _personDetail.ForeColor = Theme.TextMuted;
        _personDetail.AutoSize = true;

        stack.Add(_personName);
        stack.Add(_personState);
        stack.Add(_personDetail);
        stack.Add(new Panel { Height = 16, Dock = DockStyle.Top });

        _personButtons.FlowDirection = FlowDirection.TopDown;
        _personButtons.AutoSize = true;
        _personButtons.WrapContents = false;
        _personButtons.Dock = DockStyle.Top;
        stack.Add(_personButtons);

        _countdown.AutoSize = true;
        _countdown.ForeColor = Theme.TextFaint;
        _countdown.Font = Theme.Small;
        _countdown.Margin = new Padding(0, 16, 0, 0);
        stack.Add(_countdown);
    }

    // -----------------------------------------------------------------
    // Interaction
    // -----------------------------------------------------------------

    private void PressKey(string key)
    {
        if (_selected is not null) return;

        switch (key)
        {
            case "Clear":
                _pin.Clear();
                break;
            case "⌫":
                if (_pin.Text.Length > 0) _pin.Text = _pin.Text[..^1];
                break;
            default:
                if (_pin.Text.Length < _pin.MaxLength) _pin.Text += key;
                break;
        }
    }

    private void Identify()
    {
        string pin = _pin.Text.Trim();
        if (pin.Length == 0) return;

        var employee = _store.FindByPin(pin) ?? _store.FindByBadge(pin);
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

        var status = _clock.GetStatus(employee);

        _personName.Text = employee.FullName;
        _personState.Text = status.StateLabel;
        _personState.ForeColor = status.IsClockedIn ? Theme.Success : Theme.TextMuted;

        string detail = status.IsClockedIn
            ? $"On the clock since {status.Since:h:mm tt}"
            : $"Worked today: {(status.MinutesToday / 60.0):0.00} hours";
        if (!string.IsNullOrWhiteSpace(employee.Message))
            detail += $"\n{employee.Message}";
        _personDetail.Text = detail;

        _personButtons.Controls.Clear();

        var primary = new Button
        {
            Text = status.ActionLabel,
            Width = 340,
            Height = 64,
            FlatStyle = FlatStyle.Flat,
            BackColor = status.IsClockedIn ? Theme.Danger : Theme.Success,
            ForeColor = Color.White,
            Font = new Font(Theme.Family, 16f, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
        primary.FlatAppearance.BorderSize = 0;
        primary.Click += (_, _) => PerformPunch(status.NextKind);
        _personButtons.Controls.Add(primary);

        if (status.IsClockedIn && !status.OnBreak)
        {
            var breakButton = Theme.SecondaryButton("Start unpaid break", 340);
            breakButton.Height = 44;
            breakButton.Click += (_, _) => PerformPunch(PunchKind.BreakOut);
            _personButtons.Controls.Add(breakButton);
        }

        var cancel = Theme.SecondaryButton("Not me", 340);
        cancel.Height = 40;
        cancel.Click += (_, _) => Reset();
        _personButtons.Controls.Add(cancel);

        _pinPanel.Visible = false;
        _personPanel.Visible = true;
        _personPanel.BringToFront();
    }

    private void PerformPunch(PunchKind kind)
    {
        if (_selected is null) return;

        try
        {
            var now = DateTime.Now;
            var punch = _clock.Punch(_selected, kind, now);

            string label = kind switch
            {
                PunchKind.In => "Clocked in",
                PunchKind.Out => "Clocked out",
                PunchKind.BreakOut => "Break started",
                PunchKind.BreakIn => "Break ended",
                _ => "Punch recorded",
            };

            _resetAt = DateTime.Now.AddSeconds(5);
            _personButtons.Controls.Clear();
            _personState.Text = $"{label} at {punch.Timestamp:h:mm tt}";
            _personState.ForeColor = kind == PunchKind.In ? Theme.Success : Theme.Text;
            _personDetail.Text = kind == PunchKind.Out ? "Have a good evening!" : "Thank you.";

            _feedback.Text = $"{_selected.FirstName} — {label} {punch.Timestamp:h:mm tt}";
            _feedback.ForeColor = Theme.Success;
            _feedback.Visible = true;

            RefreshRecent();
        }
        catch (Exception ex)
        {
            PromptDialog.Warn(this, ex.Message);
            Reset();
        }
    }

    private void Reset()
    {
        _selected = null;
        _pin.Clear();
        _pinError.Text = "";
        _personPanel.Visible = false;
        _pinPanel.Visible = true;
        _pinPanel.BringToFront();
        _pin.Focus();
    }

    private void OnTick()
    {
        var now = DateTime.Now;
        _time.Text = now.ToString("h:mm:ss tt");
        _date.Text = now.ToString("dddd, MMMM d, yyyy");

        _hasEmployees = _store.ListEmployees(includeInactive: false).Count > 0;
        _message.Text = _hasEmployees
            ? _settings.CompanyMessage
            : "No employees yet. Ask your supervisor to set up PunchPoint.";

        if (_selected is not null)
        {
            int seconds = (int)Math.Max(0, (_resetAt - now).TotalSeconds);
            _countdown.Text = $"Returns to the PIN screen in {seconds}s";
            if (seconds <= 0) Reset();
        }
        else if (Visible && _pinPanel.Visible && !_pin.Focused)
        {
            _pin.Focus();
        }
    }

    private void RefreshRecent()
    {
        _settings = _store.GetSettings();
        var employees = _store.ListEmployees(includeInactive: true).ToDictionary(e => e.Id);
        var punches = _store
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
                punch.Timestamp.ToString("h:mm tt"));
        }

        _recentEmpty.Visible = punches.Count == 0;
        _recentEmpty.Text = "No punches yet today.";
    }

    // -----------------------------------------------------------------
    // Locking down the window
    // -----------------------------------------------------------------

    private void SupervisorExit()
    {
        string? hash = _store.GetSettings().AdminPasswordHash;

        if (string.IsNullOrWhiteSpace(hash))
        {
            if (!PromptDialog.Confirm(this,
                    "No supervisor password is set, so anyone can close the clock.\n\n" +
                    "Set one in the PunchPoint admin app (Settings → Company) to lock this down.\n\n" +
                    "Close the clock now?"))
                return;

            _allowClose = true;
            Close();
            return;
        }

        if (!AdminPassword.Unlock(this, hash, "Enter the supervisor password to close the clock."))
            return;

        _allowClose = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
        {
            // Alt+F4 and the window's close button always ask for the supervisor password.
            e.Cancel = true;
            SupervisorExit();
            return;
        }

        if (_allowClose && !_fullScreen && WindowState == FormWindowState.Normal)
        {
            try
            {
                var settings = _store.GetSettings();
                settings.ClockWidth = Width;
                settings.ClockHeight = Height;
                _store.SaveSettings(settings);
            }
            catch
            {
                // remembering the window size is best effort
            }
        }

        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Ctrl+Shift+E is the discreet supervisor shortcut.
        if (keyData == (Keys.Control | Keys.Shift | Keys.E))
        {
            SupervisorExit();
            return true;
        }

        if (keyData == Keys.Escape && _selected is not null)
        {
            Reset();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }
}

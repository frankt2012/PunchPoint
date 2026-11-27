using System.Diagnostics;
using System.Runtime.InteropServices;
using PunchPoint.App.UI;
using PunchPoint.Core.Models;
using PunchPoint.Core.Services;
using PunchPoint.Core.Storage;
using PunchPoint.Core.TimeCalc;

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
    private bool _compact;
    private Size _appliedSize = Size.Empty;
    private TableLayoutPanel? _root;
    private Control? _clockCard;
    private Control? _recentCard;
    private Control? _rightCard;
    private TableLayoutPanel? _keypad;
    private TableLayoutPanel? _pinGrid;
    private string? _layoutLog;
    private Control? _personStack;
    private Panel? _personSpacer;
    private Control? _clockStack;
    private bool _punching;
    private bool _hasEmployees;
    private readonly bool _sizeFixed;
    private AppSettings _settings;

    public KioskForm(Store store, bool fullScreen, Size? sizeOverride = null, string? weekPreviewPin = null)
    {
        _sizeFixed = sizeOverride is not null;
        _store = store;
        _clock = new ClockService(store);
        _settings = store.GetSettings();
        _fullScreen = fullScreen;
        Theme.ApplyTitleBar(this);

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
            Size = sizeOverride ?? new Size(
                Math.Clamp(_settings.ClockWidth, 640, 2400),
                Math.Clamp(_settings.ClockHeight, 480, 1600));
            MinimumSize = new Size(640, 480);
            TopMost = false;
            MaximizeBox = true;
            MinimizeBox = true;
        }

        if (!fullScreen) _appliedSize = Size;

        BuildUi();
        _tick.Tick += (_, _) => OnTick();
        _refresh.Tick += (_, _) => RefreshRecent();
        _tick.Start();
        _refresh.Start();

        RefreshRecent();
        OnTick();
        Shown += (_, _) =>
        {
            _pin.Focus();

            // Diagnostics: open one employee's week view straight away (used for screenshots
            // and for showing a supervisor what an employee sees).
            if (weekPreviewPin is null) return;

            BeginInvoke(new Action(() =>
            {
                var employee = _store.FindByPin(weekPreviewPin) ?? _store.FindByBadge(weekPreviewPin);
                if (employee is null) return;

                ShowPerson(employee);
                ShowMyPunches(employee);
            }));
        };
    }

    // -----------------------------------------------------------------
    // Layout
    // -----------------------------------------------------------------

    private void BuildUi()
    {
        _root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(22),
            BackColor = Theme.Canvas,
        };
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 250));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Clock
        var clockCard = Theme.Card(28);
        clockCard.Dock = DockStyle.Fill;
        clockCard.AutoSize = false;          // fill the row, do not shrink to the text
        clockCard.MinimumSize = Size.Empty;
        clockCard.Margin = new Padding(0, 0, 10, 0);

        var clockStack = new StackPanel(2) { Dock = DockStyle.Fill, Padding = new Padding(0) };
        clockStack.AutoSize = false; // fill the card; otherwise the rows shrink to the clock text

        _time.ForeColor = Theme.Text;
        _time.AutoSize = true;
        _time.Dock = DockStyle.Top;
        _time.Margin = new Padding(0);

        _date.ForeColor = Theme.TextMuted;
        _date.AutoSize = true;
        _date.Dock = DockStyle.Top;
        _date.Margin = new Padding(0, 8, 0, 6);

        _message.ForeColor = Theme.Primary;
        _message.AutoSize = true;
        _message.MaximumSize = new Size(0, 0);
        _message.Dock = DockStyle.Top;
        _message.Margin = new Padding(0, 0, 0, 6);

        _feedback.Font = new Font(Theme.Family, 13f, FontStyle.Bold);
        _feedback.AutoSize = true;
        _feedback.Dock = DockStyle.Top;
        _feedback.Margin = new Padding(0);

        // Order matters: the first control added is the top row.
        clockStack.Controls.Add(_time);
        clockStack.Controls.Add(_date);
        clockStack.Controls.Add(_message);
        clockStack.Controls.Add(_feedback);
        clockCard.Controls.Add(clockStack);

        // Recent activity
        var recentCard = Theme.Card(16);
        recentCard.Dock = DockStyle.Fill;
        recentCard.AutoSize = false;
        recentCard.MinimumSize = Size.Empty;
        recentCard.Margin = new Padding(0, 12, 10, 0);

        var recentTitle = new Label
        {
            Text = "Recent activity",
            Font = Theme.H3,
            ForeColor = Theme.Text,
            Dock = DockStyle.Top,
            Height = 34,
        };

        if (_recent.Columns.Count == 0)
        {
            Theme.StyleGrid(_recent);
            _recent.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Employee", FillWeight = 200 });
            _recent.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Punch", FillWeight = 110 });
            _recent.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Time", FillWeight = 110 });
            _recent.RowTemplate.Height = 30;
        }

        _recentEmpty.AutoSize = true;
        _recentEmpty.Font = Theme.Small;
        _recentEmpty.ForeColor = Theme.TextMuted;
        _recentEmpty.Dock = DockStyle.Top;
        _recentEmpty.Margin = new Padding(6, 8, 0, 0);

        var recentHost = new Panel { Dock = DockStyle.Fill };
        recentHost.Controls.Add(_recent);
        recentHost.Controls.Add(_recentEmpty);
        _recentEmpty.BringToFront();

        recentCard.Controls.Add(recentHost);
        recentCard.Controls.Add(recentTitle);

        // PIN pad or person card
        var rightCard = Theme.Card(0);
        rightCard.Dock = DockStyle.Fill;
        rightCard.AutoSize = false;          // let the keypad row use the full height
        rightCard.MinimumSize = Size.Empty;
        BuildPinPanel();
        BuildPersonPanel();
        rightCard.Controls.Add(_personPanel);
        rightCard.Controls.Add(_pinPanel);

        _root.Controls.Add(clockCard, 0, 0);
        _root.Controls.Add(recentCard, 0, 1);
        _root.Controls.Add(rightCard, 1, 0);
        _root.SetRowSpan(rightCard, 2);

        _clockCard = clockCard;
        _recentCard = recentCard;
        _rightCard = rightCard;
        _clockStack = clockStack;

        Controls.Add(_root);

        // The Supervisor link sits in the clock card's corner: it opens the management app or
        // closes the clock. The window's own close button and Alt+F4 still close directly.
        AddSupervisorLink(clockCard);

        ApplyLayout();
    }

    private void AddSupervisorLink(Control clockCard)
    {
        var link = new Button
        {
            Text = "Supervisor",
            FlatStyle = FlatStyle.Flat,
            Size = new Size(94, 26),
            BackColor = clockCard.BackColor,
            ForeColor = Theme.TextFaint,
            Font = Theme.Small,
            Cursor = Cursors.Hand,
            TabStop = false,
        };
        link.FlatAppearance.BorderSize = 0;

        void Place() => link.Location = new Point(
            Math.Max(0, clockCard.ClientSize.Width - link.Width - 4), 2);

        link.Click += (_, _) => SupervisorMenu();
        clockCard.Controls.Add(link);
        link.BringToFront();
        clockCard.Resize += (_, _) => Place();
        Place();
    }

    private void SupervisorMenu()
    {
        switch (SupervisorPrompt.Ask(this))
        {
            case SupervisorAction.OpenApp:
                OpenManagementApp();
                break;
            case SupervisorAction.CloseClock:
                SupervisorExit();
                break;
        }
    }

    /// <summary>
    /// Starts PunchPoint.exe from the clock. The clock has just checked the password, so it
    /// hands the app a one-time token instead of making the supervisor type it twice.
    /// </summary>
    private void OpenManagementApp()
    {
        var settings = _store.GetSettings();

        if (!string.IsNullOrWhiteSpace(settings.AdminPasswordHash) &&
            !AdminPassword.Unlock(this, settings.AdminPasswordHash,
                "Enter the supervisor password to open PunchPoint."))
            return;

        string exe = Path.Combine(AppContext.BaseDirectory, "PunchPoint.exe");
        if (!File.Exists(exe))
        {
            PromptDialog.Warn(this,
                "PunchPoint.exe is not in the same folder as the clock, so it cannot be started " +
                "from here.\n\nStart it from the Start menu or its desktop shortcut.");
            return;
        }

        try
        {
            string token = LaunchHandoff.Create();
            Process.Start(new ProcessStartInfo(exe, $"--launch-token {token}")
            {
                UseShellExecute = false,
                WorkingDirectory = AppContext.BaseDirectory,
            });
        }
        catch (Exception ex)
        {
            PromptDialog.Warn(this, $"PunchPoint could not be started: {ex.Message}");
            return;
        }

        _store.Audit("Clock handed over", "PunchPoint opened from the clock");
        _allowClose = true;
        Close();
    }

    /// <summary>
    /// Wide windows use two columns; anything under 760 px collapses to one column with the
    /// clock on top, and the recent-activity panel is hidden so the PIN pad keeps its room.
    /// </summary>
    private void ApplyLayout()
    {
        if (_root is null) return;

        bool compact = !_fullScreen && ClientSize.Width < 760;

        _root.SuspendLayout();
        _compact = compact;

        _root.Padding = new Padding(compact ? 10 : 22);
        _root.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, compact ? 100 : 58);
        _root.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, compact ? 0 : 42);
        _root.RowStyles[0] = new RowStyle(SizeType.Absolute, compact ? 136 : 250);

        if (_clockCard is Control clock)
        {
            _root.SetColumnSpan(clock, compact ? 2 : 1);
            clock.Margin = new Padding(0, 0, compact ? 0 : 10, 0);
            (clock as CardPanel)?.SetPadding(compact ? 12 : 28);
        }

        if (_rightCard is Control right)
        {
            _root.SetColumnSpan(right, compact ? 2 : 1);
            _root.SetRowSpan(right, compact ? 1 : 2);
            _root.SetCellPosition(right, new TableLayoutPanelCellPosition(compact ? 0 : 1, compact ? 1 : 0));
            right.Margin = compact ? new Padding(0, 8, 0, 0) : new Padding(10, 0, 0, 0);
        }

        if (_recentCard is Control recent) recent.Visible = !compact;

        if (_pinGrid is not null)
            _pinGrid.Padding = compact ? new Padding(14, 6, 14, 6) : new Padding(14, 12, 14, 12);

        // The keypad takes whatever height is left after the title, PIN box and error line.
        if (_keypad is not null)
        {
            _keypad.MaximumSize = new Size(0, compact ? 372 : 432);
            _keypad.AutoSize = false;
        }

        if (_personStack is Control personStack)
            personStack.Padding = compact ? new Padding(14, 8, 14, 8) : new Padding(26, 30, 26, 18);

        if (_personSpacer is not null) _personSpacer.Height = compact ? 6 : 16;

        _personDetail.Font = new Font(Theme.Family, compact ? 10f : 10.5f);
        _countdown.Font = compact ? new Font(Theme.Family, 9f) : Theme.Small;
        _countdown.Margin = new Padding(0, compact ? 6 : 16, 0, 0);

        _pin.Height = compact ? 40 : 54;

        ScaleType(compact);

        _root.ResumeLayout();
        PerformLayout();
        FitTimeType();
    }

    /// <summary>
    /// The clock digits shrink until they fit the card: at 58 pt they overflow a narrow
    /// window, and a clipped time is worse than a smaller one.
    /// </summary>
    private void FitTimeType()
    {
        // Measure against the card, not the stack: the stack sizes itself to its content, so
        // using it would chase its own tail and shrink the clock for no reason.
        if (_clockCard is not Control card) return;

        int available = card.ClientSize.Width - card.Padding.Horizontal;
        if (available < 80) return;

        // The card has to fit the time, the date and the company message, so the height caps
        // the size too - otherwise a short window shows the clock and clips everything else.
        int heightAvailable = card.ClientSize.Height - card.Padding.Vertical;
        int reserved = _date.PreferredHeight + _date.Margin.Vertical
                     + _message.PreferredHeight + _message.Margin.Vertical
                     + 8;
        if (_feedback.Visible) reserved += _feedback.PreferredHeight + _feedback.Margin.Vertical;
        float byHeight = Math.Max(18f, (heightAvailable - reserved) / 1.55f);

        float size = _fullScreen ? 58f : Math.Min(58f, Math.Min(available / 7.2f, byHeight));
        while (size > 20f)
        {
            using var font = new Font(Theme.Family, size, FontStyle.Bold);
            if (TextRenderer.MeasureText(_time.Text, font).Width <= available) break;
            size -= 2f;
        }

        if (Math.Abs(_time.Font.Size - size) < 0.5f) return;
        _time.Font = new Font(Theme.Family, size, FontStyle.Bold);
    }

    private void ScaleType(bool compact)
    {
        _date.Font = new Font(Theme.Family, compact ? 10.5f : 13f);
        _message.Font = new Font(Theme.Family, compact ? 9.5f : 11f, FontStyle.Italic);
        _feedback.Font = new Font(Theme.Family, compact ? 11f : 13f, FontStyle.Bold);
        _personName.Font = new Font(Theme.Family, compact ? 19f : 24f, FontStyle.Bold);
        _personState.Font = new Font(Theme.Family, compact ? 13f : 15f, FontStyle.Bold);

        _pin.Font = new Font(Theme.Family, compact ? 22f : 26f, FontStyle.Bold);

        var pad = FindKeypad();
        if (pad is not null)
        {
            pad.Margin = compact ? new Padding(0, 6, 0, 0) : new Padding(0, 8, 0, 0);
            foreach (Control key in pad.Controls)
                key.Margin = compact ? new Padding(3) : new Padding(5);
        }
    }

    /// <summary>
    /// Key labels are sized from the button they sit in, so a short window gets smaller
    /// digits instead of clipped ones. Runs on every keypad layout; the size check stops
    /// the font change from triggering another pass.
    /// </summary>
    private static void FitKeypadType(TableLayoutPanel pad)
    {
        foreach (Control key in pad.Controls)
        {
            bool digit = key.Text.Length == 1;
            float size = Math.Clamp((key.Height - 10) * (digit ? 0.52f : 0.40f), 9f, digit ? 24f : 13f);
            if (Math.Abs(key.Font.Size - size) < 0.6f) continue;
            key.Font = new Font(Theme.Family, size, digit ? FontStyle.Bold : FontStyle.Regular);
        }
    }

    private TableLayoutPanel? FindKeypad()
    {
        if (_keypad is not null) return _keypad;

        // Built inside BuildPinPanel; find it once.
        static TableLayoutPanel? Find(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (child is TableLayoutPanel { ColumnCount: 3 } table) return table;
                if (Find(child) is TableLayoutPanel found) return found;
            }
            return null;
        }

        _keypad = Find(_pinPanel);
        return _keypad;
    }

    public void EnableLayoutLog(string path) => _layoutLog = path;

    private void DumpLayout()
    {
        if (_layoutLog is null) return;
        try
        {
            var lines = new List<string>
            {
                $"client      {ClientSize.Width}x{ClientSize.Height}",
                $"root        {_root?.Size}",
                $"clockCard   {_clockCard?.Bounds}",
                $"rightCard   {_rightCard?.Bounds}",
                $"pinPanel    {_pinPanel.Bounds}",
                $"pinGrid     {_pinGrid?.Bounds} padding={_pinGrid?.Padding}",
                $"pinBox      {_pin.Bounds}",
                $"keypad      {_keypad?.Bounds}",
            };
            if (_pinGrid is not null)
                for (int i = 0; i < _pinGrid.RowStyles.Count; i++)
                    lines.Add($"  gridRow{i} {_pinGrid.RowStyles[i].SizeType} {_pinGrid.RowStyles[i].Height}");
            if (_keypad is not null)
                for (int i = 0; i < _keypad.RowStyles.Count; i++)
                    lines.Add($"  padRow{i}  {_keypad.RowStyles[i].SizeType} {_keypad.RowStyles[i].Height}");
            if (_keypad is not null && _keypad.Controls.Count > 0)
                lines.Add($"  key0 {_keypad.Controls[0].Bounds} font={_keypad.Controls[0].Font.Size}");
            File.WriteAllLines(_layoutLog, lines);
        }
        catch
        {
            // diagnostics only
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (IsHandleCreated) BeginInvoke(new Action(DumpLayout));
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (_fullScreen || _root is null) return;

        bool compact = ClientSize.Width < 760;
        if (compact != _compact) ApplyLayout();
        else if (!compact) _time.Font = new Font(Theme.Family, 58f, FontStyle.Bold);
        if (IsHandleCreated) BeginInvoke(new Action(DumpLayout));
    }

    private void BuildPinPanel()
    {
        _pinPanel.Dock = DockStyle.Fill;

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(30, 14, 30, 12),
        };
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _pinPanel.Controls.Add(grid);
        _pinGrid = grid;

        grid.Controls.Add(new Label
        {
            Text = "Enter your PIN",
            Font = Theme.H2,
            ForeColor = Theme.Text,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6),
        }, 0, 0);

        _pin.Font = new Font(Theme.Family, 26f, FontStyle.Bold);
        _pin.TextAlign = HorizontalAlignment.Center;
        _pin.UseSystemPasswordChar = true;
        _pin.MaxLength = 12;
        _pin.Height = 54;
        _pin.BorderStyle = BorderStyle.FixedSingle;
        _pin.BackColor = Theme.InputBackground;
        _pin.ForeColor = Theme.Text;
        _pin.Dock = DockStyle.Top;
        _pin.Margin = new Padding(0);
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
        grid.Controls.Add(_pin, 0, 1);

        _pinError.AutoSize = true;
        _pinError.ForeColor = Theme.Danger;
        _pinError.Font = Theme.SmallBold;
        _pinError.Margin = new Padding(0, 6, 0, 0);
        grid.Controls.Add(_pinError, 0, 2);

        var pad = new TableLayoutPanel
        {
            ColumnCount = 3,
            RowCount = 4,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 0, 0),
        };
        pad.Layout += (_, _) => FitKeypadType(pad);
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
        grid.Controls.Add(pad, 0, 3);
        _keypad = pad;
    }

    private void BuildPersonPanel()
    {
        _personPanel.Dock = DockStyle.Fill;
        _personPanel.Visible = false;

        var stack = new StackPanel(8) { Padding = new Padding(26, 30, 26, 18) };
        _personPanel.Controls.Add(stack);
        _personStack = stack;

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
        _personSpacer = new Panel { Height = 16, Dock = DockStyle.Top };
        stack.Add(_personSpacer);

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
            Height = _compact ? 48 : 64,
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
            breakButton.Height = _compact ? 34 : 44;
            breakButton.Click += (_, _) => PerformPunch(PunchKind.BreakOut);
            _personButtons.Controls.Add(breakButton);
        }

        // "My punches" and "Not me" share one row so a compact window still fits.
        var sideBySide = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };

        var mine = Theme.SecondaryButton("My punches", 165);
        mine.Height = _compact ? 32 : 40;
        mine.Margin = new Padding(0, 0, 6, 0);
        mine.Click += (_, _) => ShowMyPunches(employee);
        sideBySide.Controls.Add(mine);

        var cancel = Theme.SecondaryButton("Not me", 165);
        cancel.Height = _compact ? 32 : 40;
        cancel.Margin = new Padding(0);
        cancel.Click += (_, _) => Reset();
        sideBySide.Controls.Add(cancel);

        _personButtons.Controls.Add(sideBySide);

        _pinPanel.Visible = false;
        _personPanel.Visible = true;
        _personPanel.BringToFront();
    }

    /// <summary>
    /// Shows this employee their own punches for the current pay period. The countdown is pushed
    /// out while the dialog is open, then restarted, so the card does not reset underneath it.
    /// </summary>
    private void ShowMyPunches(Employee employee)
    {
        WeekSummary week;
        try
        {
            week = EmployeeWeek.Build(_store, employee, _store.GetSettings());
        }
        catch (Exception ex)
        {
            PromptDialog.Warn(this, "Your punches could not be read: " + ex.Message);
            return;
        }

        _resetAt = DateTime.Now.AddMinutes(5);
        try
        {
            using var dialog = new WeekPunchesDialog(week, _store.GetSettings());
            dialog.ShowDialog(this);
        }
        finally
        {
            _resetAt = DateTime.Now.AddSeconds(30);
            if (_selected is not null) _pin.Focus();
        }
    }

    private void PerformPunch(PunchKind kind)
    {
        if (_selected is null || _punching) return;

        _punching = true;
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
            _punching = false;
            Reset();
        }
    }

    private void Reset()
    {
        _punching = false;
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
        FitTimeType();
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
        else if (Visible && _pinPanel.Visible && !_pin.Focused && IsForegroundWindow)
        {
            // Only take the keyboard back when the clock is the window the operator is using.
            // Without this check the clock stole focus from other programs every second: typing
            // in Word would land in the PIN box, and four digits could clock someone in.
            _pin.Focus();
        }
    }

    /// <summary>
    /// Picks up a new window size chosen in Settings without restarting the clock. Dragging the
    /// window is untouched: that only writes to the settings when the clock closes.
    /// </summary>
    private void ApplySettingsSize()
    {
        if (_fullScreen || _sizeFixed || !IsHandleCreated) return;

        var size = new Size(
            Math.Clamp(_settings.ClockWidth, 640, 2400),
            Math.Clamp(_settings.ClockHeight, 480, 1600));

        if (size == _appliedSize) return;

        _appliedSize = size;
        if (WindowState == FormWindowState.Normal && Size != size) Size = size;
    }

    private void RefreshRecent()
    {
        _settings = _store.GetSettings();
        ApplySettingsSize();
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
        var settings = _store.GetSettings();
        string? hash = settings.AdminPasswordHash;

        // Some sites do not want a password on the clock (a PC the supervisor keeps an eye on).
        if (!settings.RequirePasswordToCloseClock)
        {
            if (!PromptDialog.Confirm(this, "Close the employee clock?"))
                return;

            _allowClose = true;
            Close();
            return;
        }

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

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private bool IsForegroundWindow => GetForegroundWindow() == Handle;

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Ctrl+Shift+E is the discreet supervisor shortcut (same menu as the corner link).
        if (keyData == (Keys.Control | Keys.Shift | Keys.E))
        {
            SupervisorMenu();
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

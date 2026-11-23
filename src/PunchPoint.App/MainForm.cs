using PunchPoint.App.UI;
using PunchPoint.App.UI.Screens;

namespace PunchPoint.App;

public sealed class MainForm : Form
{
    private readonly AppServices _services;
    private readonly Panel _content;
    private readonly Dictionary<string, UserControl> _screens = new();
    private readonly Dictionary<string, Button> _navButtons = new();
    private string _current = "";

    /// <summary>Screen to open first (diagnostic/technician option).</summary>
    public string? StartScreen { get; set; }

    /// <summary>Set by the diagnostic tour so it can close without the confirmation dialog.</summary>
    public bool SuppressCloseConfirmation { get; set; }

    private static readonly (string Key, string Label, bool Admin)[] Nav =
    {
        ("clock", "Open clock", false),
        ("dashboard", "Dashboard", false),
        ("employees", "Employees", true),
        ("messages", "Messages", true),
        ("timecards", "Punch Editing", true),
        ("punches", "Punches", true),
        ("import", "Import", true),
        ("reports", "Reports", true),
        ("export", "Export", true),
        ("settings", "Settings", true),
    };

    public MainForm(AppServices services, string? startScreen = null)
    {
        _services = services;

        Text = "PunchPoint — Time & Attendance";
        Theme.ApplyTitleBar(this);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1120, 720);
        Size = new Size(1360, 880);
        BackColor = Theme.Canvas;
        Font = Theme.Body;
        Icon = LoadIcon();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 224));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        root.Controls.Add(BuildNav(), 0, 0);

        _content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas, Padding = new Padding(0) };
        root.Controls.Add(_content, 1, 0);

        Controls.Add(root);

        _services.DataChanged += () => { if (CurrentScreen is IRefreshable r) r.RefreshData(); };

        Show(string.IsNullOrWhiteSpace(startScreen) ? "dashboard" : startScreen!);
    }

    private Control BuildNav()
    {
        var nav = new Panel { Dock = DockStyle.Fill, BackColor = Theme.NavBackground, Padding = new Padding(0) };

        var brand = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22, 22, 18, 8) };
        brand.Controls.Add(new Label
        {
            Text = "PunchPoint",
            Font = new Font(Theme.Family, 16f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(22, 24),
        });
        brand.Controls.Add(new Label
        {
            Text = "TIME & ATTENDANCE",
            Font = new Font(Theme.Family, 7.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0x7C, 0x8A, 0xA3),
            AutoSize = true,
            Location = new Point(24, 54),
        });

        var bottom = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 16, 12) };
        bottom.Controls.Add(new Label
        {
            Text = _services.Settings.CompanyName,
            ForeColor = Color.FromArgb(0x94, 0xA3, 0xB8),
            Font = Theme.Small,
            AutoSize = false,
            Dock = DockStyle.Bottom,
            Height = 40,
            TextAlign = ContentAlignment.BottomLeft,
        });

        var list = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10, 0, 10, 0) };

        int top = 4;
        foreach (var (key, label, _) in Nav)
        {
            var button = new Button
            {
                Text = "   " + label,
                TextAlign = ContentAlignment.MiddleLeft,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.NavBackground,
                ForeColor = Theme.NavText,
                Font = Theme.Body,
                Height = 42,
                Width = 194,
                Left = 5,
                Top = top,
                Cursor = Cursors.Hand,
                TabStop = false,
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Theme.NavHover;
            button.Click += (_, _) => TryShow(key);

            _navButtons[key] = button;
            list.Controls.Add(button);
            top += 46;
        }

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        layout.Controls.Add(brand, 0, 0);
        layout.Controls.Add(list, 0, 1);
        layout.Controls.Add(bottom, 0, 2);

        nav.Controls.Add(layout);

        return nav;
    }

    private void TryShow(string key)
    {
        if (key == "clock")
        {
            LaunchClock();
            return;
        }

        Show(key);
    }

    /// <summary>Opens the separate employee clock window (its own process, no admin UI).</summary>
    private void LaunchClock()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "PunchPointClock.exe");
        if (!File.Exists(path))
        {
            PromptDialog.Warn(this,
                "PunchPointClock.exe was not found next to PunchPoint.exe.\n\n" +
                "Keep both files in the same folder (as in the distributed zip).");
            return;
        }

        try
        {
            var start = new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                WorkingDirectory = AppContext.BaseDirectory,
            };

            if (!string.IsNullOrWhiteSpace(_services.DatabasePath))
            {
                start.Arguments = $"--db \"{_services.DatabasePath}\"";
                start.UseShellExecute = false;
            }

            System.Diagnostics.Process.Start(start);
        }
        catch (Exception ex)
        {
            PromptDialog.Warn(this, "Could not start the clock window: " + ex.Message);
        }
    }

    public void Show(string key)
    {
        if (!_screens.ContainsKey(key))
        {
            UserControl screen = key switch
            {
                "dashboard" => new DashboardScreen(_services),
                "employees" => new EmployeesScreen(_services),
                "messages" => new MessagesScreen(_services),
                "timecards" => new TimeCardsScreen(_services),
                "punches" => new PunchesScreen(_services),
                "import" => new ImportScreen(_services),
                "reports" => new ReportsScreen(_services),
                "export" => new ExportScreen(_services),
                "settings" => new SettingsScreen(_services),
                _ => new DashboardScreen(_services),
            };

            screen.Dock = DockStyle.Fill;
            screen.Visible = false;
            _content.Controls.Add(screen);
            _screens[key] = screen;
        }

        foreach (var (k, control) in _screens)
            control.Visible = k == key;

        foreach (var (k, button) in _navButtons)
        {
            bool active = k == key;
            button.BackColor = active ? Theme.NavActive : Theme.NavBackground;
            button.ForeColor = active ? Theme.NavTextActive : Theme.NavText;
            button.Font = active ? Theme.BodyBold : Theme.Body;
        }

        _current = key;

        if (_screens[key] is IRefreshable refreshable)
            refreshable.RefreshData();

        _screens[key].BringToFront();
    }

    /// <summary>The screen currently on display (diagnostics).</summary>
    internal UserControl? Screen =>
        _current.Length > 0 && _screens.TryGetValue(_current, out var screen) ? screen : null;

    private UserControl? CurrentScreen =>
        _current.Length > 0 && _screens.TryGetValue(_current, out var screen) ? screen : null;

    private static Icon? LoadIcon()
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Resources", "punchpoint.ico");
            if (File.Exists(path)) return new Icon(path);
            return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            return null;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !SuppressCloseConfirmation)
        {
            var answer = MessageBox.Show(this,
                "Close PunchPoint? Employees will not be able to clock in until it is opened again.",
                "PunchPoint", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }

        try
        {
            _services.Store.Audit("Application closed", "");
        }
        catch
        {
            // ignore
        }

        base.OnFormClosing(e);
    }
}

public interface IRefreshable
{
    void RefreshData();
}

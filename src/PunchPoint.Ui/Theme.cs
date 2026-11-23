using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace PunchPoint.App.UI;

/// <summary>Shared visual language: colours, fonts and small layout helpers.</summary>
public static class Theme
{
    /// <summary>True when the dark palette is active.</summary>
    public static bool IsDark { get; private set; }

    // Palette (set by Apply)
    public static Color Canvas { get; private set; }
    public static Color Surface { get; private set; }
    public static Color SurfaceAlt { get; private set; }
    public static Color Border { get; private set; }
    public static Color BorderStrong { get; private set; }

    public static Color NavBackground { get; private set; }
    public static Color NavHover { get; private set; }
    public static Color NavActive { get; private set; }
    public static Color NavText { get; private set; }
    public static Color NavTextActive { get; private set; }

    public static Color Primary { get; private set; }
    public static Color PrimaryHover { get; private set; }
    public static Color PrimarySoft { get; private set; }
    public static Color Success { get; private set; }
    public static Color SuccessSoft { get; private set; }
    public static Color Warning { get; private set; }
    public static Color WarningSoft { get; private set; }
    public static Color Danger { get; private set; }
    public static Color DangerSoft { get; private set; }

    public static Color Text { get; private set; }
    public static Color TextMuted { get; private set; }
    public static Color TextFaint { get; private set; }

    /// <summary>Background for text boxes, combo boxes and keypad keys.</summary>
    public static Color InputBackground => IsDark ? Color.FromArgb(0x0E, 0x16, 0x24) : Color.White;

    static Theme()
    {
        Apply(dark: false);
    }

    /// <summary>Applies the configured theme; "System" follows the Windows app mode.</summary>
    public static void Use(Core.Models.AppTheme theme)
    {
        bool dark = theme == Core.Models.AppTheme.Dark ||
                    (theme == Core.Models.AppTheme.System && WindowsPrefersDark());
        Apply(dark);
    }

    public static void Apply(bool dark)
    {
        IsDark = dark;

        if (dark)
        {
            Canvas = Color.FromArgb(0x0B, 0x12, 0x20);
            Surface = Color.FromArgb(0x12, 0x1A, 0x29);
            SurfaceAlt = Color.FromArgb(0x0E, 0x16, 0x24);
            Border = Color.FromArgb(0x1F, 0x2A, 0x3C);
            BorderStrong = Color.FromArgb(0x2B, 0x3A, 0x52);

            NavBackground = Color.FromArgb(0x07, 0x0B, 0x13);
            NavHover = Color.FromArgb(0x11, 0x1A, 0x29);
            NavActive = Color.FromArgb(0x25, 0x63, 0xEB);
            NavText = Color.FromArgb(0xA9, 0xB8, 0xCC);
            NavTextActive = Color.White;

            Primary = Color.FromArgb(0x3B, 0x82, 0xF6);
            PrimaryHover = Color.FromArgb(0x25, 0x63, 0xEB);
            PrimarySoft = Color.FromArgb(0x1E, 0x3A, 0x5F);
            Success = Color.FromArgb(0x22, 0xC5, 0x5E);
            SuccessSoft = Color.FromArgb(0x12, 0x35, 0x1F);
            Warning = Color.FromArgb(0xF5, 0x9E, 0x0B);
            WarningSoft = Color.FromArgb(0x3B, 0x2A, 0x0B);
            Danger = Color.FromArgb(0xEF, 0x44, 0x44);
            DangerSoft = Color.FromArgb(0x3A, 0x14, 0x14);

            Text = Color.FromArgb(0xE6, 0xED, 0xF7);
            TextMuted = Color.FromArgb(0x93, 0xA4, 0xBD);
            TextFaint = Color.FromArgb(0x6B, 0x7C, 0x96);
        }
        else
        {
            Canvas = Color.FromArgb(0xF4, 0xF6, 0xF9);
            Surface = Color.White;
            SurfaceAlt = Color.FromArgb(0xF8, 0xFA, 0xFC);
            Border = Color.FromArgb(0xE2, 0xE8, 0xF0);
            BorderStrong = Color.FromArgb(0xCB, 0xD5, 0xE1);

            NavBackground = Color.FromArgb(0x0F, 0x17, 0x2A);
            NavHover = Color.FromArgb(0x1E, 0x29, 0x3B);
            NavActive = Color.FromArgb(0x25, 0x63, 0xEB);
            NavText = Color.FromArgb(0xCB, 0xD5, 0xE1);
            NavTextActive = Color.White;

            Primary = Color.FromArgb(0x25, 0x63, 0xEB);
            PrimaryHover = Color.FromArgb(0x1D, 0x4E, 0xD8);
            PrimarySoft = Color.FromArgb(0xDB, 0xEA, 0xFE);
            Success = Color.FromArgb(0x16, 0xA3, 0x4A);
            SuccessSoft = Color.FromArgb(0xDC, 0xFC, 0xE7);
            Warning = Color.FromArgb(0xD9, 0x77, 0x06);
            WarningSoft = Color.FromArgb(0xFE, 0xF3, 0xC7);
            Danger = Color.FromArgb(0xDC, 0x26, 0x26);
            DangerSoft = Color.FromArgb(0xFE, 0xE2, 0xE2);

            Text = Color.FromArgb(0x0F, 0x17, 0x2A);
            TextMuted = Color.FromArgb(0x64, 0x74, 0x8B);
            TextFaint = Color.FromArgb(0x94, 0xA3, 0xB8);
        }
    }

    private static bool WindowsPrefersDark()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }

    // Fonts
    public static readonly string Family = ResolveFamily();

    public static Font Base(float size = 10f, FontStyle style = FontStyle.Regular) => new(Family, size, style);
    public static readonly Font Body = new(Family, 10f);
    public static readonly Font BodyBold = new(Family, 10f, FontStyle.Bold);
    public static readonly Font Small = new(Family, 9f);
    public static readonly Font SmallBold = new(Family, 9f, FontStyle.Bold);
    public static readonly Font H1 = new(Family, 20f, FontStyle.Bold);
    public static readonly Font H2 = new(Family, 14f, FontStyle.Bold);
    public static readonly Font H3 = new(Family, 11.5f, FontStyle.Bold);
    public static readonly Font Mono = new("Consolas", 10f);

    private static string ResolveFamily()
    {
        try
        {
            using var installed = new InstalledFontCollection();
            var names = installed.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (names.Contains("Segoe UI")) return "Segoe UI";
        }
        catch
        {
            // fall through
        }
        return "Tahoma";
    }

    // ---------------------------------------------------------------
    // Controls
    // ---------------------------------------------------------------

    public static Label Title(string text) => new()
    {
        Text = text,
        Font = H1,
        ForeColor = Text,
        AutoSize = true,
        Margin = new Padding(0, 0, 0, 4),
    };

    public static Label Subtitle(string text) => new()
    {
        Text = text,
        Font = Body,
        ForeColor = TextMuted,
        AutoSize = true,
        Margin = new Padding(0, 0, 0, 0),
    };

    public static Label TextLabel(string text) => new()
    {
        Text = text,
        Font = Body,
        ForeColor = Text,
        AutoSize = true,
    };

    public static Label Muted(string text) => new()
    {
        Text = text,
        Font = Small,
        ForeColor = TextMuted,
        AutoSize = true,
    };

    public static Button PrimaryButton(string text, int width = 140) => StyledButton(text, Primary, Color.White, width);

    public static Button SecondaryButton(string text, int width = 120) => new()
    {
        Text = text,
        Width = width,
        Height = 36,
        FlatStyle = FlatStyle.Flat,
        BackColor = Surface,
        ForeColor = Text,
        Font = BodyBold,
        Cursor = Cursors.Hand,
        FlatAppearance = { BorderColor = BorderStrong, BorderSize = 1 },
        Padding = new Padding(8, 0, 8, 0),
        UseVisualStyleBackColor = false,
    };

    public static Button DangerButton(string text, int width = 120) => StyledButton(text, Danger, Color.White, width);

    public static Button StyledButton(string text, Color back, Color fore, int width = 140)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 36,
            FlatStyle = FlatStyle.Flat,
            BackColor = back,
            ForeColor = fore,
            Font = BodyBold,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            Padding = new Padding(8, 0, 8, 0),
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    public static TextBox Input(int width = 260) => new()
    {
        Width = width,
        Font = Body,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = InputBackground,
        ForeColor = Text,
        Margin = new Padding(0, 4, 0, 8),
    };

    public static ComboBox Dropdown(int width = 260) => new()
    {
        Width = width,
        Font = Body,
        DropDownStyle = ComboBoxStyle.DropDownList,
        BackColor = InputBackground,
        ForeColor = Text,
        FlatStyle = IsDark ? FlatStyle.Flat : FlatStyle.Standard,
        Margin = new Padding(0, 4, 0, 8),
    };

    public static CheckBox Check(string text) => new()
    {
        Text = text,
        Font = Body,
        AutoSize = true,
        ForeColor = Text,
        Margin = new Padding(0, 6, 0, 6),
    };

    /// <summary>A white rounded-ish card panel with a subtle border.</summary>
    public static Panel Card(int padding = 18, Color? background = null) => new()
    {
        BackColor = background ?? Surface,
        Padding = new Padding(padding),
        Margin = new Padding(0, 0, 0, 14),
        Dock = DockStyle.Top,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(0, 48),
    };

    public static FlowLayoutPanel Row(int spacing = 8) => new()
    {
        FlowDirection = FlowDirection.LeftToRight,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        WrapContents = false,
        Margin = new Padding(0),
        Padding = new Padding(0),
    };

    public static TableLayoutPanel Stack(int spacing = 10) => new()
    {
        ColumnCount = 1,
        Dock = DockStyle.Top,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Margin = new Padding(0),
        Padding = new Padding(0),
        RowCount = 1,
    };

    /// <summary>A pill-shaped status label.</summary>
    public static Label Pill(string text, Color background, Color foreground)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = false,
            Font = SmallBold,
            ForeColor = foreground,
            BackColor = background,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(10, 5, 10, 5),
            Margin = new Padding(0, 2, 6, 2),
            Height = 26,
            Width = TextRenderer.MeasureText(text, SmallBold).Width + 24,
            MinimumSize = new Size(60, 26),
        };
        return label;
    }

    public static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = Surface;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Border;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceAlt;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextMuted;
        grid.ColumnHeadersDefaultCellStyle.Font = SmallBold;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 8, 6, 8);
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = SurfaceAlt;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextMuted;
        grid.ColumnHeadersHeight = 38;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = Text;
        grid.DefaultCellStyle.Font = Body;
        grid.DefaultCellStyle.SelectionBackColor = PrimarySoft;
        grid.DefaultCellStyle.SelectionForeColor = Text;
        grid.DefaultCellStyle.Padding = new Padding(6, 2, 6, 2);
        grid.RowHeadersVisible = false;
        grid.RowTemplate.Height = 34;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.ReadOnly = true;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.Dock = DockStyle.Fill;
    }

    /// <summary>Draws a rounded rectangle path, used for the kiosk button accents.</summary>
    public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        if (d <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>A horizontal rule.</summary>
    public static Panel Divider() => new()
    {
        Height = 1,
        Dock = DockStyle.Top,
        BackColor = Border,
        Margin = new Padding(0, 8, 0, 12),
    };
}

/// <summary>Helpers for consistently formatted text in the UI.</summary>
public static class Fmt
{
    public static string Hours(int minutes) => (minutes / 60.0).ToString("0.00");
    public static string HoursOrDash(int minutes) => minutes == 0 ? "–" : Hours(minutes);

    public static string Time(DateTime value) => value.ToString("h:mm tt");
    public static string DayLabel(DateTime value) => value.ToString("ddd, MMM d");
    public static string Stamp(DateTime value) => value.ToString("ddd MMM d, h:mm tt");

    public static string Duration(int minutes)
    {
        if (minutes <= 0) return "0m";
        int h = minutes / 60, m = minutes % 60;
        return h == 0 ? $"{m}m" : m == 0 ? $"{h}h" : $"{h}h {m}m";
    }

    public static string Relative(DateTime value)
    {
        var span = DateTime.Now - value;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays} d ago";
        return value.ToString("MMM d");
    }

    public static string Money(decimal? value) => value is null ? "" : value.Value.ToString("$0.00");
}

/// <summary>A vertical stack of controls that grows with its content.</summary>
public sealed class StackPanel : TableLayoutPanel
{
    private readonly int _spacing;

    public StackPanel(int spacing = 14)
    {
        _spacing = spacing;
        ColumnCount = 1;
        RowCount = 0;
        Dock = DockStyle.Top;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Margin = new Padding(0);
        Padding = new Padding(0);
        BackColor = Color.Transparent;
    }

    public T Add<T>(T control) where T : Control
    {
        RowCount++;
        RowStyles.Add(new RowStyle(SizeType.AutoSize));
        // Buttons keep their designed width; panels/labels fill the stack.
        control.Dock = control is Button ? DockStyle.None : DockStyle.Top;
        control.Margin = new Padding(0, 0, 0, _spacing);
        Controls.Add(control, 0, RowCount - 1);
        return control;
    }
}

using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace PunchPoint.App.UI;

/// <summary>Shared visual language: colours, fonts and small layout helpers.</summary>
public static class Theme
{
    // Palette
    public static readonly Color Canvas = Color.FromArgb(0xF4, 0xF6, 0xF9);
    public static readonly Color Surface = Color.White;
    public static readonly Color SurfaceAlt = Color.FromArgb(0xF8, 0xFA, 0xFC);
    public static readonly Color Border = Color.FromArgb(0xE2, 0xE8, 0xF0);
    public static readonly Color BorderStrong = Color.FromArgb(0xCB, 0xD5, 0xE1);

    public static readonly Color NavBackground = Color.FromArgb(0x0F, 0x17, 0x2A);
    public static readonly Color NavHover = Color.FromArgb(0x1E, 0x29, 0x3B);
    public static readonly Color NavActive = Color.FromArgb(0x25, 0x63, 0xEB);
    public static readonly Color NavText = Color.FromArgb(0xCB, 0xD5, 0xE1);
    public static readonly Color NavTextActive = Color.White;

    public static readonly Color Primary = Color.FromArgb(0x25, 0x63, 0xEB);
    public static readonly Color PrimaryHover = Color.FromArgb(0x1D, 0x4E, 0xD8);
    public static readonly Color PrimarySoft = Color.FromArgb(0xDB, 0xEA, 0xFE);
    public static readonly Color Success = Color.FromArgb(0x16, 0xA3, 0x4A);
    public static readonly Color SuccessSoft = Color.FromArgb(0xDC, 0xFC, 0xE7);
    public static readonly Color Warning = Color.FromArgb(0xD9, 0x77, 0x06);
    public static readonly Color WarningSoft = Color.FromArgb(0xFE, 0xF3, 0xC7);
    public static readonly Color Danger = Color.FromArgb(0xDC, 0x26, 0x26);
    public static readonly Color DangerSoft = Color.FromArgb(0xFE, 0xE2, 0xE2);

    public static readonly Color Text = Color.FromArgb(0x0F, 0x17, 0x2A);
    public static readonly Color TextMuted = Color.FromArgb(0x64, 0x74, 0x8B);
    public static readonly Color TextFaint = Color.FromArgb(0x94, 0xA3, 0xB8);

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
            if (names.Contains("Segoe UI Variable Display")) return "Segoe UI Variable Display";
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
        Margin = new Padding(0, 4, 0, 8),
    };

    public static ComboBox Dropdown(int width = 260) => new()
    {
        Width = width,
        Font = Body,
        DropDownStyle = ComboBoxStyle.DropDownList,
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
        control.Dock = DockStyle.Top;
        control.Margin = new Padding(0, 0, 0, _spacing);
        Controls.Add(control, 0, RowCount - 1);
        return control;
    }
}

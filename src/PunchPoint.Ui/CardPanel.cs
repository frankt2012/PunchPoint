using System.Drawing.Drawing2D;

namespace PunchPoint.App.UI;

/// <summary>
/// A card with a hairline border and softly rounded corners. Dark mode needs the border: on a
/// dark canvas a plain panel has no edge, so cards read as one flat sheet.
/// </summary>
public sealed class CardPanel : Panel
{
    private const int Radius = 8;

    private int _padding;

    public CardPanel(int padding = 18, Color? background = null)
    {
        _padding = padding;
        BackColor = background ?? Theme.Surface;
        Padding = new Padding(padding);
        Margin = new Padding(0, 0, 0, 14);
        Dock = DockStyle.Top;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(0, 48);
        Resize += (_, _) => Clip();
    }

    /// <summary>Cards keep their padding by design; this is the one supported way to change it.</summary>
    public void SetPadding(int padding)
    {
        _padding = padding;
        Padding = new Padding(padding);
    }

    protected override void OnPaddingChanged(EventArgs e)
    {
        base.OnPaddingChanged(e);
        Padding = new Padding(_padding);
    }

    private void Clip()
    {
        var bounds = new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height));
        using var path = Path(bounds);
        var previous = Region;
        Region = new Region(path);
        previous?.Dispose();
        Invalidate();
    }

    private static GraphicsPath Path(Rectangle bounds)
    {
        var path = new GraphicsPath();
        int d = Radius * 2;

        if (bounds.Width <= d || bounds.Height <= d)
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

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        using var path = Path(new Rectangle(0, 0, Width - 1, Height - 1));
        using var pen = new Pen(Theme.Border, 1f);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawPath(pen, path);
    }
}

using System.Drawing.Printing;
using PunchPoint.Core.Reports;

namespace PunchPoint.App.UI;

/// <summary>Prints any report table: title, subtitle, columns and rows with page breaks.</summary>
public sealed class TablePrintDocument : PrintDocument
{
    private readonly ReportTable _table;
    private readonly string _company;
    private int _rowIndex;
    private bool _headerPrinted;

    public TablePrintDocument(ReportTable table, string company)
    {
        _table = table;
        _company = company;
        DocumentName = table.Title;
        DefaultPageSettings.Landscape = table.Columns.Count > 6;
        DefaultPageSettings.Margins = new Margins(50, 50, 50, 50);
    }

    protected override void OnBeginPrint(PrintEventArgs e)
    {
        base.OnBeginPrint(e);
        _rowIndex = 0;
        _headerPrinted = false;
    }

    protected override void OnPrintPage(PrintPageEventArgs e)
    {
        base.OnPrintPage(e);
        var g = e.Graphics!;
        var bounds = e.MarginBounds;
        int y = bounds.Top;

        using var titleFont = new Font("Segoe UI", 16, FontStyle.Bold);
        using var subFont = new Font("Segoe UI", 9.5f);
        using var headerFont = new Font("Segoe UI", 9, FontStyle.Bold);
        using var bodyFont = new Font("Segoe UI", 9);
        using var line = new Pen(Color.FromArgb(0xCB, 0xD5, 0xE1));
        using var ink = new SolidBrush(Color.FromArgb(0x0F, 0x17, 0x2A));
        using var muted = new SolidBrush(Color.FromArgb(0x64, 0x74, 0x8B));

        if (!_headerPrinted)
        {
            g.DrawString(_company, subFont, muted, bounds.Left, y);
            y += 18;
            g.DrawString(_table.Title, titleFont, ink, bounds.Left, y);
            y += 30;
            if (_table.Subtitle.Length > 0)
            {
                g.DrawString(_table.Subtitle, subFont, muted, bounds.Left, y);
                y += 20;
            }

            y += 6;
            DrawRow(g, headerFont, _table.Columns.ToArray(), bounds, ref y, ink);
            g.DrawLine(line, bounds.Left, y, bounds.Right, y);
            y += 4;
            _headerPrinted = true;
        }
        else
        {
            g.DrawString(_table.Title + " (continued)", subFont, muted, bounds.Left, y);
            y += 24;
            g.DrawLine(line, bounds.Left, y, bounds.Right, y);
            y += 6;
        }

        while (_rowIndex < _table.Rows.Count)
        {
            if (y > bounds.Bottom - 40)
            {
                e.HasMorePages = true;
                return;
            }

            DrawRow(g, bodyFont, _table.Rows[_rowIndex].ToArray(), bounds, ref y, ink);
            _rowIndex++;
        }

        g.DrawString($"Printed {DateTime.Now:g}", subFont, muted, bounds.Left, bounds.Bottom - 14);
    }

    private static void DrawRow(Graphics g, Font font, string[] cells, Rectangle bounds, ref int y, Brush brush)
    {
        int count = Math.Max(cells.Length, 1);
        int width = bounds.Width / count;
        for (int i = 0; i < cells.Length; i++)
        {
            var rect = new Rectangle(bounds.Left + i * width, y, width - 6, 18);
            g.DrawString(cells[i], font, brush, rect);
        }
        y += 18;
    }
}

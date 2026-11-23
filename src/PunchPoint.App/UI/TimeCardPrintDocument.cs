using System.Drawing.Printing;
using PunchPoint.Core.Models;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.App.UI;

/// <summary>Printable time card: header, one row per day, totals and signatures.</summary>
public sealed class TimeCardPrintDocument : PrintDocument
{
    private readonly AppSettings _settings;
    private readonly TimeCardResult _result;

    public TimeCardPrintDocument(AppSettings settings, TimeCardResult result)
    {
        _settings = settings;
        _result = result;
        DocumentName = $"Time card {result.Employee.SortName} {result.PayPeriod.Start:yyyy-MM-dd}";
        DefaultPageSettings.Margins = new System.Drawing.Printing.Margins(60, 60, 60, 60);
    }

    protected override void OnBeginPrint(PrintEventArgs e)
    {
        base.OnBeginPrint(e);
        DefaultPageSettings.Landscape = false;
    }

    protected override void OnPrintPage(PrintPageEventArgs e)
    {
        base.OnPrintPage(e);

        var g = e.Graphics!;
        var bounds = e.MarginBounds;

        if (!DrawPage(g, bounds))
            e.HasMorePages = true;
    }

    /// <summary>Draws the card. Returns false when it needs another page.</summary>
    public bool DrawPage(Graphics g, Rectangle bounds)
    {
        int y = bounds.Top;

        using var titleFont = new Font("Segoe UI", 18, FontStyle.Bold);
        using var headingFont = new Font("Segoe UI", 11, FontStyle.Bold);
        using var bodyFont = new Font("Segoe UI", 9.5f);
        using var smallFont = new Font("Segoe UI", 8f);
        using var linePen = new Pen(Color.FromArgb(0xCB, 0xD5, 0xE1));
        using var headerBrush = new SolidBrush(Color.FromArgb(0x0F, 0x17, 0x2A));
        using var mutedBrush = new SolidBrush(Color.FromArgb(0x64, 0x74, 0x8B));

        // Header
        g.DrawString(_settings.CompanyName, titleFont, headerBrush, bounds.Left, y);
        y += 34;

        g.DrawString("Time Card", headingFont, mutedBrush, bounds.Left, y);
        g.DrawString($"{_result.PayPeriod.Start:MMM d} – {_result.PayPeriod.End:MMM d, yyyy}  ({_result.PayPeriod.Type})",
            bodyFont, mutedBrush, bounds.Left + 80, y + 1);
        y += 26;

        g.DrawString(_result.Employee.SortName, headingFont, headerBrush, bounds.Left, y);
        string meta = string.Join("   ·   ", new[]
        {
            $"PIN {_result.Employee.Pin}",
            _result.Employee.Badge.Length > 0 ? $"Badge {_result.Employee.Badge}" : "",
            _result.Employee.EmployeeNumber.Length > 0 ? $"Employee #{_result.Employee.EmployeeNumber}" : "",
            _result.QueryShiftLabel(),
        }.Where(s => s.Length > 0));
        g.DrawString(meta, smallFont, mutedBrush, bounds.Left, y + 20);
        y += 46;

        g.DrawLine(linePen, bounds.Left, y, bounds.Right, y);
        y += 8;

        // Column layout
        int[] widths = { 110, 70, 70, 60, 70, 60, 60, 70 };
        string[] headers = { "Day", "IN", "OUT", "Break", "Regular", "OT", "Holiday", "Total" };

        int x = bounds.Left;
        for (int i = 0; i < headers.Length; i++)
        {
            g.DrawString(headers[i], smallFont, mutedBrush, x, y);
            x += widths[i];
        }
        y += 18;
        g.DrawLine(linePen, bounds.Left, y, bounds.Right, y);
        y += 4;

        foreach (var day in _result.Days)
        {
            if (y > bounds.Bottom - 150)
                return false;

            x = bounds.Left;
            string[] values =
            {
                day.WorkDate.ToString("ddd M/d"),
                day.FirstInLabel,
                day.LastOutLabel,
                day.BreakMinutes > 0 ? day.BreakMinutes + "m" : "",
                Fmt.Hours(day.RegularMinutes),
                Fmt.HoursOrDash(day.OverTime1Minutes + day.OverTime2Minutes),
                Fmt.HoursOrDash(day.HolidayMinutes),
                Fmt.Hours(day.TotalMinutes),
            };

            for (int i = 0; i < values.Length; i++)
            {
                g.DrawString(values[i], bodyFont, headerBrush, x, y);
                x += widths[i];
            }

            y += 20;

            if (day.Issues.Count > 0)
            {
                g.DrawString("! " + day.Issues[0], smallFont, new SolidBrush(Color.FromArgb(0xD9, 0x77, 0x06)),
                    bounds.Left + 110, y);
                y += 15;
            }
        }

        y += 6;
        g.DrawLine(linePen, bounds.Left, y, bounds.Right, y);
        y += 10;

        g.DrawString("Totals", headingFont, headerBrush, bounds.Left, y);
        y += 24;

        string totals =
            $"Regular  {Fmt.Hours(_result.RegularMinutes)} h      " +
            $"Overtime  {Fmt.Hours(_result.OverTime1Minutes + _result.OverTime2Minutes)} h      " +
            $"Holiday  {Fmt.Hours(_result.HolidayMinutes)} h";
        g.DrawString(totals, bodyFont, headerBrush, bounds.Left, y);
        y += 22;

        if (_result.AdjustmentMinutes != 0)
        {
            g.DrawString($"Manual adjustment: {Fmt.Hours(_result.AdjustmentMinutes)} h", bodyFont, headerBrush, bounds.Left, y);
            y += 22;
        }

        using var totalFont = new Font("Segoe UI", 13, FontStyle.Bold);
        g.DrawString($"Total paid: {Fmt.Hours(_result.TotalMinutes)} hours", totalFont, headerBrush, bounds.Left, y);
        y += 40;

        g.DrawLine(linePen, bounds.Left, y, bounds.Left + 240, y);
        g.DrawLine(linePen, bounds.Right - 240, y, bounds.Right, y);
        g.DrawString("Employee signature", smallFont, mutedBrush, bounds.Left, y + 4);
        g.DrawString("Supervisor signature", smallFont, mutedBrush, bounds.Right - 240, y + 4);

        g.DrawString($"Printed {DateTime.Now:g}", smallFont, mutedBrush, bounds.Left, bounds.Bottom - 14);
        return true;
    }
}

internal static class TimeCardPrintExtensions
{
    public static string QueryShiftLabel(this TimeCardResult result) =>
        result.Shift is null ? "" : $"{result.Shift.Name} {result.Shift.TimeRangeLabel}";
}

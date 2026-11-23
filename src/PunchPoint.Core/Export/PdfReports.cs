using System.Globalization;
using System.Text;
using PunchPoint.Core.Reports;

namespace PunchPoint.Core.Export;

/// <summary>
/// Minimal, dependency-free PDF writer for reports (Helvetica, A4/Letter, tables, page
/// numbers). Keeping it in-house means the app stays MIT-licensed and the single-file
/// build carries no extra native libraries.
/// </summary>
public static class PdfReports
{
    private const float PageWidth = 612f;    // US Letter, points
    private const float PageHeight = 792f;
    private const float Margin = 46f;
    private const float LineHeight = 14f;

    public static void Write(ReportTable table, string path, string company, string? subtitle = null) =>
        File.WriteAllBytes(path, ToBytes(table, company, subtitle));

    public static byte[] ToBytes(ReportTable table, string company, string? subtitle = null)
    {
        var pages = Layout(table, company, subtitle ?? table.Subtitle);
        return Build(pages);
    }

    /// <summary>Payroll summary: one row per employee plus a totals line.</summary>
    public static ReportTable FromPayrollRows(IEnumerable<TimeCardExportRow> rows, PayPeriodLabelContext context)
    {
        var table = new ReportTable
        {
            Title = "Payroll Summary",
            Subtitle = context.Subtitle,
        };

        foreach (string column in new[] { "Employee", "Number", "Regular", "OT1", "OT2", "Holiday", "Total", "Approved" })
            table.AddColumn(column);

        int reg = 0, ot1 = 0, ot2 = 0, hol = 0, all = 0;
        foreach (var row in rows)
        {
            reg += row.Result.RegularMinutes;
            ot1 += row.Result.OverTime1Minutes;
            ot2 += row.Result.OverTime2Minutes;
            hol += row.Result.HolidayMinutes;
            all += row.Result.TotalMinutes;

            table.AddRow(
                row.Employee.SortName,
                row.Employee.EmployeeNumber,
                context.Hours(row.Result.RegularMinutes),
                context.Hours(row.Result.OverTime1Minutes),
                context.Hours(row.Result.OverTime2Minutes),
                context.Hours(row.Result.HolidayMinutes),
                context.Hours(row.Result.TotalMinutes),
                row.Approved ? "Yes" : "");
        }

        table.AddRow("TOTAL", "", context.Hours(reg), context.Hours(ot1), context.Hours(ot2),
            context.Hours(hol), context.Hours(all), "");

        return table;
    }

    public sealed class PayPeriodLabelContext
    {
        public required string Subtitle { get; init; }
        public required Func<int, string> Hours { get; init; }
    }

    // -----------------------------------------------------------------
    // Layout
    // -----------------------------------------------------------------

    private sealed record TextLine(float X, float Y, string Text, float Size, bool Bold, bool Right);

    private sealed record Page(List<TextLine> Lines, List<(float X1, float Y1, float X2, float Y2)> Rules);

    private static List<Page> Layout(ReportTable table, string company, string subtitle)
    {
        var pages = new List<Page>();
        var lines = new List<TextLine>();
        var rules = new List<(float, float, float, float)>();

        float contentWidth = PageWidth - Margin * 2;
        float y = PageHeight - Margin;

        // Column widths from the widest cell in each column.
        int columns = Math.Max(table.Columns.Count, 1);
        var widths = new float[columns];
        for (int c = 0; c < table.Columns.Count; c++)
        {
            float w = Measure(table.Columns[c], 9, bold: true);
            foreach (var row in table.Rows)
                if (c < row.Count) w = Math.Max(w, Measure(row[c], 9, bold: false));
            widths[c] = w;
        }

        float total = widths.Sum();
        if (total <= 0) widths[0] = contentWidth;
        else
            for (int c = 0; c < columns; c++)
                widths[c] = widths[c] / total * contentWidth;

        // Keep a minimum width so a single long cell cannot squeeze the rest away.
        float minWidth = Math.Min(60f, contentWidth / columns);
        for (int c = 0; c < columns; c++)
            widths[c] = Math.Max(widths[c], minWidth);
        float scale = contentWidth / widths.Sum();
        for (int c = 0; c < columns; c++) widths[c] *= scale;

        void NewPage(bool first)
        {
            lines.Add(new TextLine(Margin, y, company, 9, false, false));
            y -= 16;
            lines.Add(new TextLine(Margin, y, table.Title + (first ? "" : " (continued)"), 15, true, false));
            y -= 16;
            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                lines.Add(new TextLine(Margin, y, subtitle, 9, false, false));
                y -= 14;
            }
            y -= 6;
            DrawHeader();
        }

        void DrawHeader()
        {
            float x = Margin;
            for (int c = 0; c < table.Columns.Count; c++)
            {
                string title = Fit(table.Columns[c], widths[c] - 10, 9, true);
                lines.Add(new TextLine(x + 3, y, title, 9, true, false));
                x += widths[c];
            }
            rules.Add((Margin, y - 4, PageWidth - Margin, y - 4));
            y -= LineHeight;
        }

        void DrawRow(IReadOnlyList<string> cells, bool bold)
        {
            float x = Margin;
            for (int c = 0; c < columns; c++)
            {
                string text = c < cells.Count ? cells[c] ?? "" : "";
                bool right = IsNumeric(c < table.Columns.Count ? table.Columns[c] : "");
                text = Fit(text, widths[c] - 10, 9, bold);
                lines.Add(new TextLine(right ? x + widths[c] - 6 : x + 3, y, text, 9, bold, right));
                x += widths[c];
            }
            y -= LineHeight;
        }

        NewPage(first: true);

        foreach (var row in table.Rows)
        {
            if (y < Margin + 40)
            {
                pages.Add(new Page(lines, rules));
                lines = new List<TextLine>();
                rules = new List<(float, float, float, float)>();
                y = PageHeight - Margin;
                NewPage(first: false);
            }

            bool bold = row.Count > 0 &&
                        (row[0].Equals("TOTAL", StringComparison.OrdinalIgnoreCase) ||
                         row[0].Equals("Totals", StringComparison.OrdinalIgnoreCase));
            DrawRow(row, bold);
        }

        pages.Add(new Page(lines, rules));
        return pages;
    }

    private static bool IsNumeric(string header) =>
        header.Contains("Regular") || header.Contains("OT") || header.Contains("Total") ||
        header.Contains("Holiday") || header.Contains("Hours") || header.Contains("Break") ||
        header.Contains("Amount");

    private static string Fit(string text, float width, float size, bool bold)
    {
        if (Measure(text, size, bold) <= width) return text;

        for (int len = text.Length - 1; len > 0; len--)
        {
            string candidate = text[..len] + "…";
            if (Measure(candidate, size, bold) <= width) return candidate;
        }
        return "";
    }

    /// <summary>Approximate Helvetica widths (per 1000 units) for the characters we use.</summary>
    private static float Measure(string text, float size, bool bold)
    {
        float units = 0;
        foreach (char ch in text)
            units += Width(ch, bold) ;
        return units * size / 1000f;
    }

    private static float Width(char ch, bool bold)
    {
        float w = ch switch
        {
            ' ' => 278,
            '.' or ',' or ':' or ';' or '\'' => 278,
            'i' or 'l' or 'j' or 'I' => 222,
            'f' or 't' or 'r' => 333,
            'm' => 833,
            'w' or 'W' => 778,
            'M' => 833,
            >= '0' and <= '9' => 556,
            >= 'A' and <= 'Z' => 667,
            >= 'a' and <= 'z' => 500,
            '-' => 333,
            '(' or ')' => 333,
            '/' => 278,
            '…' => 1000,
            _ => 556,
        };
        return bold ? w * 1.03f : w;
    }

    // -----------------------------------------------------------------
    // PDF output
    // -----------------------------------------------------------------

    private static byte[] Build(List<Page> pages)
    {
        var objects = new List<string>();
        var buffer = new StringBuilder();
        buffer.Append("%PDF-1.4\n");

        int pageCount = pages.Count;
        // 1 catalog, 2 pages, 3 font regular, 4 font bold, then per page: page + contents
        objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
        objects.Add($"<< /Type /Pages /Kids [{string.Join(" ", Enumerable.Range(0, pageCount).Select(i => $"{5 + i * 2} 0 R"))}] /Count {pageCount} >>");
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");

        for (int i = 0; i < pageCount; i++)
        {
            int pageObj = 5 + i * 2;
            int contentObj = pageObj + 1;
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] " +
                        $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentObj} 0 R >>");

            var content = new StringBuilder();
            foreach (var line in pages[i].Lines)
            {
                string font = line.Bold ? "/F2" : "/F1";
                content.Append("BT ").Append(font).Append(' ')
                       .Append(line.Size.ToString("0.##", CultureInfo.InvariantCulture)).Append(" Tf 1 0 0 1 ")
                       .Append(line.X.ToString("0.##", CultureInfo.InvariantCulture)).Append(' ')
                       .Append(line.Y.ToString("0.##", CultureInfo.InvariantCulture)).Append(" Tm (")
                       .Append(Escape(line.Text)).Append(") Tj ET\n");
            }

            content.Append("BT /F1 8 Tf 1 0 0 1 ")
                   .Append(Margin.ToString("0.##", CultureInfo.InvariantCulture)).Append(' ')
                   .Append((Margin - 16).ToString("0.##", CultureInfo.InvariantCulture))
                   .Append(" Tm (Generated ").Append(Escape(DateTime.Now.ToString("g", CultureInfo.InvariantCulture))).Append(") Tj ET\n");

            foreach (var rule in pages[i].Rules)
            {
                content.Append("0.7 w 0.75 g ")
                       .Append(rule.X1.ToString("0.##", CultureInfo.InvariantCulture)).Append(' ')
                       .Append(rule.Y1.ToString("0.##", CultureInfo.InvariantCulture)).Append(" m ")
                       .Append(rule.X2.ToString("0.##", CultureInfo.InvariantCulture)).Append(' ')
                       .Append(rule.Y2.ToString("0.##", CultureInfo.InvariantCulture)).Append(" l S 0 g\n");
            }

            // Page number, bottom right.
            content.Append("BT /F1 8 Tf 1 0 0 1 ")
                   .Append((PageWidth - Margin - 60).ToString("0.##", CultureInfo.InvariantCulture)).Append(' ')
                   .Append((Margin - 16).ToString("0.##", CultureInfo.InvariantCulture))
                   .Append(" Tm (Page ").Append(i + 1).Append(" of ").Append(pageCount).Append(") Tj ET\n");

            string stream = content.ToString();
            objects.Add($"<< /Length {Encoding.Latin1.GetByteCount(stream)} >>\nstream\n{stream}endstream");
        }

        var offsets = new List<int>();
        int position = Encoding.Latin1.GetByteCount(buffer.ToString());

        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(position);
            string chunk = $"{i + 1} 0 obj\n{objects[i]}\nendobj\n";
            buffer.Append(chunk);
            position += Encoding.Latin1.GetByteCount(chunk);
        }

        int xref = position;
        buffer.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (int offset in offsets)
            buffer.Append(offset.ToString("0000000000")).Append(" 00000 n \n");

        buffer.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(buffer.ToString());
    }

    private static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char ch in text)
        {
            switch (ch)
            {
                case '(': sb.Append("\\("); break;
                case ')': sb.Append("\\)"); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n' or '\r': sb.Append(' '); break;
                case '\u2013': sb.Append((char)0x96); break; // en dash
                case '\u2014': sb.Append((char)0x97); break; // em dash
                case '\u2018': sb.Append((char)0x91); break;
                case '\u2019': sb.Append((char)0x92); break;
                case '\u201C': sb.Append((char)0x93); break;
                case '\u201D': sb.Append((char)0x94); break;
                case '\u2026': sb.Append((char)0x85); break;
                case '\u2022': sb.Append((char)0x95); break;
                case '\u00A0': sb.Append(' '); break;
                default:
                    // WinAnsi covers Latin-1; anything else becomes a placeholder.
                    sb.Append(ch <= 255 ? ch : '?');
                    break;
            }
        }
        return sb.ToString();
    }
}

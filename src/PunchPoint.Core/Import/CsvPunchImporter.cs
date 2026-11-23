using System.Globalization;
using System.Text;
using PunchPoint.Core.Models;
using PunchPoint.Core.Storage;

namespace PunchPoint.Core.Import;

public sealed class CsvImportOptions
{
    public string Delimiter { get; set; } = "";
    public string EmployeeColumn { get; set; } = "";
    public string TimestampColumn { get; set; } = "";
    public string DirectionColumn { get; set; } = "";
    public string CommentColumn { get; set; } = "";
    public bool CreateMissingEmployees { get; set; }
    public PunchDirectionMode DirectionMode { get; set; } = PunchDirectionMode.Auto;
}

public sealed class CsvPunchPreview
{
    public required string Path { get; init; }
    public string Delimiter { get; set; } = ",";
    public List<string> Headers { get; } = new();
    public List<string[]> SampleRows { get; } = new();
    public string EmployeeColumn { get; set; } = "";
    public string TimestampColumn { get; set; } = "";
    public string DirectionColumn { get; set; } = "";
    public string CommentColumn { get; set; } = "";
    public int RowCount { get; set; }
    public int ParsedRows { get; set; }
    public DateTime? FirstPunch { get; set; }
    public DateTime? LastPunch { get; set; }
    public List<string> Warnings { get; } = new();
    public List<(string Key, int Punches)> Employees { get; } = new();
    public bool LooksUsable => EmployeeColumn.Length > 0 && TimestampColumn.Length > 0 && ParsedRows > 0;
}

/// <summary>
/// Imports punches from a delimited text file (CSV/TSV). Columns are detected from the
/// header row and can be overridden. Direction comes from an optional type column
/// (IN/OUT/1/0), otherwise punches alternate per employee.
/// </summary>
public sealed class CsvPunchImporter
{
    private readonly string _path;

    public CsvPunchImporter(string path) => _path = path;

    public CsvPunchPreview Preview(CsvImportOptions? options = null)
    {
        options ??= new CsvImportOptions();
        var preview = new CsvPunchPreview { Path = Path.GetFullPath(_path) };

        var lines = File.ReadAllLines(_path, DetectEncoding(_path));
        if (lines.Length == 0)
        {
            preview.Warnings.Add("The file is empty.");
            return preview;
        }

        string delimiter = options.Delimiter.Length > 0 ? options.Delimiter : DetectDelimiter(lines[0]);
        preview.Delimiter = delimiter;

        var rows = lines.Select(l => SplitLine(l, delimiter)).ToList();
        var header = rows[0];
        preview.Headers.AddRange(header);

        bool headerLooksLikeData = header.Any(cell => TryParseTimestamp(cell) is not null) ||
                                   header.All(cell => double.TryParse(cell, out _));

        if (headerLooksLikeData)
        {
            preview.Warnings.Add(
                "The first row looks like data, not column names. Add a header row (for example " +
                "Employee,Date/Time,Type) so the columns can be identified.");
            return preview;
        }

        foreach (var row in rows.Skip(1).Take(8))
            preview.SampleRows.Add(row);

        preview.EmployeeColumn = options.EmployeeColumn.Length > 0 ? options.EmployeeColumn : FindHeader(header,
            "employee", "badge", "pin", "user", "name", "person", "staff");

        preview.TimestampColumn = options.TimestampColumn.Length > 0 ? options.TimestampColumn : FindHeader(header,
            "datetime", "date/time", "timestamp", "punch", "trans", "when", "clock", "date", "time");

        preview.DirectionColumn = options.DirectionColumn.Length > 0 ? options.DirectionColumn
            : FindHeader(header, "direction", "in/out", "type", "state", "punch type") ?? "";

        preview.CommentColumn = options.CommentColumn.Length > 0 ? options.CommentColumn
            : FindHeader(header, "comment", "note", "memo", "reason") ?? "";

        if (preview.EmployeeColumn.Length == 0)
            preview.Warnings.Add("Could not find an employee column (badge, PIN or name).");
        if (preview.TimestampColumn.Length == 0)
            preview.Warnings.Add("Could not find a date/time column.");

        if (preview.EmployeeColumn.Length == 0 || preview.TimestampColumn.Length == 0)
            return preview;

        int employeeIndex = IndexOf(header, preview.EmployeeColumn);
        int timeIndex = IndexOf(header, preview.TimestampColumn);

        var byEmployee = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int parsed = 0;

        foreach (var row in rows.Skip(1))
        {
            if (row.Length <= Math.Max(employeeIndex, timeIndex)) continue;
            string key = row[employeeIndex].Trim();
            var stamp = TryParseTimestamp(row[timeIndex]);
            if (stamp is null || key.Length == 0) continue;

            parsed++;
            byEmployee[key] = byEmployee.GetValueOrDefault(key) + 1;
            preview.FirstPunch = preview.FirstPunch is null || stamp < preview.FirstPunch ? stamp : preview.FirstPunch;
            preview.LastPunch = preview.LastPunch is null || stamp > preview.LastPunch ? stamp : preview.LastPunch;
        }

        preview.ParsedRows = parsed;
        preview.RowCount = rows.Count - 1;

        foreach (var pair in byEmployee.OrderByDescending(p => p.Value))
            preview.Employees.Add((pair.Key, pair.Value));

        if (parsed == 0)
            preview.Warnings.Add("No rows could be read as a date/time. Check the date format in the file.");

        return preview;
    }

    public ImportResult Import(Store store, CsvImportOptions? options = null,
        Action<int, int, string>? progress = null)
    {
        options ??= new CsvImportOptions();
        var started = DateTime.UtcNow;
        var result = new ImportResult();

        var preview = Preview(options);
        if (!preview.LooksUsable)
        {
            result.Errors.Add(string.Join(" ", preview.Warnings.DefaultIfEmpty("The file could not be read.")));
            return result;
        }

        var lines = File.ReadAllLines(_path, DetectEncoding(_path));
        var rows = lines.Select(l => SplitLine(l, preview.Delimiter)).ToList();
        var header = rows[0];

        int employeeIndex = IndexOf(header, preview.EmployeeColumn);
        int timeIndex = IndexOf(header, preview.TimestampColumn);
        int directionIndex = preview.DirectionColumn.Length > 0 ? IndexOf(header, preview.DirectionColumn) : -1;
        int commentIndex = preview.CommentColumn.Length > 0 ? IndexOf(header, preview.CommentColumn) : -1;

        var employees = store.ListEmployees(includeInactive: true);
        var lookup = new Dictionary<string, Employee>(StringComparer.OrdinalIgnoreCase);
        foreach (var employee in employees)
        {
            void Add(string? key)
            {
                if (string.IsNullOrWhiteSpace(key)) return;
                lookup.TryAdd(key.Trim(), employee);
            }
            Add(employee.Badge);
            Add(employee.Pin);
            Add(employee.EmployeeNumber);
            Add(employee.FullName);
            Add(employee.SortName);
            Add(employee.Id.ToString());
        }

        var parsed = new List<(Employee Employee, DateTime When, PunchKind? Kind, string Comment)>();
        var unknown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows.Skip(1))
        {
            if (row.Length <= Math.Max(employeeIndex, timeIndex)) continue;

            string key = row[employeeIndex].Trim();
            var stamp = TryParseTimestamp(row[timeIndex]);
            if (stamp is null || key.Length == 0) continue;

            if (!lookup.TryGetValue(key, out var employee))
            {
                if (options.CreateMissingEmployees)
                {
                    var created = new Employee
                    {
                        FirstName = key,
                        Pin = MakePin(store, key),
                        Badge = key,
                        Notes = "Created while importing a CSV file",
                    };
                    store.InsertEmployee(created);
                    lookup[key] = created;
                    employee = created;
                    result.EmployeesAdded++;
                }
                else
                {
                    unknown.Add(key);
                    result.UnmatchedPunches++;
                    continue;
                }
            }

            PunchKind? kind = null;
            if (directionIndex >= 0 && directionIndex < row.Length &&
                TryDirection(row[directionIndex], out var parsedKind))
            {
                kind = parsedKind;
            }

            string comment = commentIndex >= 0 && commentIndex < row.Length ? row[commentIndex].Trim() : "";
            parsed.Add((employee, stamp.Value, kind, comment));
        }

        // Fill in direction where the file did not provide it, alternating per employee.
        var final = new List<(Employee Employee, DateTime When, PunchKind Kind, string Comment)>();
        foreach (var group in parsed.GroupBy(p => p.Employee.Id))
        {
            bool expectingIn = true;
            DateTime? open = null;

            foreach (var item in group.OrderBy(p => p.When))
            {
                PunchKind kind;
                if (item.Kind is PunchKind explicitKind)
                {
                    kind = explicitKind;
                    if (kind == PunchKind.In) { expectingIn = false; open = item.When; }
                    else if (kind == PunchKind.Out) { expectingIn = true; open = null; }
                }
                else if (expectingIn)
                {
                    kind = PunchKind.In;
                    expectingIn = false;
                    open = item.When;
                }
                else if (open is not null && (item.When - open.Value).TotalHours > 14)
                {
                    // A missed OUT: restart the session rather than shifting every later punch.
                    kind = PunchKind.In;
                    open = item.When;
                }
                else
                {
                    kind = PunchKind.Out;
                    expectingIn = true;
                    open = null;
                }

                final.Add((item.Employee, item.When, kind, item.Comment));
            }
        }

        store.Db.InTransaction(_ =>
        {
            int total = final.Count;
            int index = 0;

            foreach (var item in final)
            {
                index++;
                if (index % 250 == 0) progress?.Invoke(index, total, $"{index:n0} of {total:n0} punches");

                var period = store.GetPayPeriodFor(item.When);
                store.InsertPunch(new Punch
                {
                    EmployeeId = item.Employee.Id,
                    PayPeriodId = period?.Id,
                    Timestamp = item.When,
                    Kind = item.Kind,
                    Source = PunchSource.Import,
                    Comment = item.Comment,
                });
                result.PunchesAdded++;
            }

            if (unknown.Count > 0)
                result.Warnings.Add(
                    $"{unknown.Count} employee value(s) had no match: {string.Join(", ", unknown.Take(10))}. " +
                    "Enable “create missing employees” or fix the values.");

            store.LogImport("CSV punches", $"{result.PunchesAdded} punches",
                System.Text.Json.JsonSerializer.Serialize(new { source = _path, rows = total }));
        });

        result.Duration = DateTime.UtcNow - started;
        return result;
    }

    private static string MakePin(Store store, string key)
    {
        string candidate = new string(key.Where(char.IsDigit).ToArray());
        if (candidate.Length >= 3 && store.FindByPin(candidate) is null) return candidate;

        for (int i = 1000; i < 100_000; i++)
            if (store.FindByPin(i.ToString()) is null) return i.ToString();

        return Guid.NewGuid().ToString("n")[..6];
    }

    private static bool TryDirection(string value, out PunchKind kind)
    {
        string v = value.Trim().ToUpperInvariant();
        switch (v)
        {
            case "IN" or "I" or "0" or "CHECK-IN" or "CLOCK IN":
                kind = PunchKind.In; return true;
            case "OUT" or "O" or "1" or "CHECK-OUT" or "CLOCK OUT":
                kind = PunchKind.Out; return true;
            case "BREAK OUT" or "2" or "BREAK-OUT" or "LUNCH OUT":
                kind = PunchKind.BreakOut; return true;
            case "BREAK IN" or "3" or "BREAK-IN" or "LUNCH IN":
                kind = PunchKind.BreakIn; return true;
            default:
                kind = PunchKind.In; return false;
        }
    }

    public static DateTime? TryParseTimestamp(string value)
    {
        value = value.Trim().Trim('"');
        if (value.Length == 0) return null;

        string[] formats =
        {
            "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd h:mm:ss tt", "yyyy-MM-dd h:mm tt",
            "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm", "yyyy-MM-dd", "yyyy/MM/dd HH:mm:ss", "yyyy/MM/dd",
            "M/d/yyyy H:mm:ss", "M/d/yyyy H:mm", "M/d/yyyy h:mm:ss tt", "M/d/yyyy h:mm tt", "M/d/yyyy",
            "M/d/yy H:mm", "M/d/yy h:mm tt", "M/d/yy",
            "d/M/yyyy HH:mm", "d/M/yyyy H:mm", "d/M/yyyy", "dd.MM.yyyy HH:mm", "dd.MM.yyyy",
            "ddd MMM d yyyy HH:mm", "MMM d yyyy HH:mm",
        };

        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var exact))
            return exact;

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
            return parsed;

        return null;
    }

    internal static string DetectDelimiter(string headerLine)
    {
        foreach (char candidate in new[] { '\t', ',', ';', '|' })
        {
            if (headerLine.Count(c => c == candidate) > 0) return candidate.ToString();
        }
        return ",";
    }

    internal static string[] SplitLine(string line, string delimiter)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (!inQuotes && delimiter.Length == 1 && ch == delimiter[0])
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        result.Add(current.ToString());
        return result.ToArray();
    }

    private static string FindHeader(IReadOnlyList<string> headers, params string[] hints)
    {
        foreach (var hint in hints)
        {
            for (int i = 0; i < headers.Count; i++)
            {
                if (headers[i].Contains(hint, StringComparison.OrdinalIgnoreCase))
                    return headers[i];
            }
        }
        return "";
    }

    private static int IndexOf(IReadOnlyList<string> headers, string name)
    {
        for (int i = 0; i < headers.Count; i++)
            if (string.Equals(headers[i], name, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    private static Encoding DetectEncoding(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        reader.Peek();
        return reader.CurrentEncoding;
    }
}

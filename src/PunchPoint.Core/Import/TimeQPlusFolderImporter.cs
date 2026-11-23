using PunchPoint.Core.Models;
using PunchPoint.Core.Storage;

namespace PunchPoint.Core.Import;

public sealed class BatchImportResult
{
    public List<(string File, ImportResult Result)> Files { get; } = new();
    public List<string> Warnings { get; } = new();
    public TimeSpan Duration { get; set; }

    public int EmployeesAdded => Files.Sum(f => f.Result.EmployeesAdded);
    public int EmployeesUpdated => Files.Sum(f => f.Result.EmployeesUpdated);
    public int PunchesAdded => Files.Sum(f => f.Result.PunchesAdded);
    public int PunchesSkipped => Files.Sum(f => f.Result.PunchesSkipped);
    public int UnmatchedPunches => Files.Sum(f => f.Result.UnmatchedPunches);
    public int HolidaysAdded => Files.Sum(f => f.Result.HolidaysAdded);
    public int ShiftsAdded => Files.Sum(f => f.Result.ShiftsAdded);
    public int FilesImported => Files.Count(f => f.Result.Success);

    public string Summary =>
        $"{FilesImported} file(s), {EmployeesAdded} employees, {PunchesAdded} punches" +
        (PunchesSkipped > 0 ? $", {PunchesSkipped} already present" : "") +
        (UnmatchedPunches > 0 ? $", {UnmatchedPunches} unmatched" : "");
}

/// <summary>
/// Imports a whole timeQplus data folder: the live database plus every weekly snapshot in
/// its Archives folder, oldest first, so all available history comes across in one go.
/// </summary>
public static class TimeQPlusFolderImporter
{
    /// <summary>
    /// Returns the databases to import, oldest first with arx_active.mdb last.
    /// Accepts either the folder containing arx_active.mdb or its parent.
    /// </summary>
    public static List<string> DiscoverDatabases(string folderOrFile)
    {
        var files = new List<string>();

        if (File.Exists(folderOrFile))
        {
            files.Add(folderOrFile);
            string? dir = Path.GetDirectoryName(folderOrFile);
            if (dir is not null) AddArchives(dir, files, includeActive: false);
            return Order(files, activePath: null);
        }

        if (!Directory.Exists(folderOrFile))
            return files;

        string folder = folderOrFile;
        string nested = Path.Combine(folder, "Data");
        if (Directory.Exists(nested) && File.Exists(Path.Combine(nested, "arx_active.mdb")))
            folder = nested;

        string active = Path.Combine(folder, "arx_active.mdb");
        AddArchives(folder, files, includeActive: false);

        if (File.Exists(active))
            files.Add(active);

        return Order(files, activePath: File.Exists(active) ? active : null);
    }

    private static void AddArchives(string folder, List<string> files, bool includeActive)
    {
        string archives = Path.Combine(folder, "Archives");
        if (!Directory.Exists(archives)) return;

        foreach (string file in Directory.GetFiles(archives, "*.mdb"))
        {
            string name = Path.GetFileName(file);
            if (!includeActive && name.StartsWith("arx_active", StringComparison.OrdinalIgnoreCase))
                continue;
            files.Add(file);
        }
    }

    private static List<string> Order(List<string> files, string? activePath)
    {
        DateTime Key(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            // Archives are named arxMMDDYY: arx111026.mdb = 2026-11-10.
            if (name.Length >= 9 &&
                name.StartsWith("arx", StringComparison.OrdinalIgnoreCase) &&
                name.AsSpan(3, 6).ToString() is var digits &&
                digits.All(char.IsDigit) &&
                int.TryParse(digits[..2], out int month) &&
                int.TryParse(digits.Substring(2, 2), out int day) &&
                int.TryParse(digits.Substring(4, 2), out int year) &&
                month is >= 1 and <= 12 && day is >= 1 and <= 31)
            {
                try
                {
                    return new DateTime(2000 + year, month, day);
                }
                catch
                {
                    // fall through to the file timestamp
                }
            }

            try { return File.GetLastWriteTime(path); }
            catch { return DateTime.MinValue; }
        }

        return files
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => string.Equals(f, activePath, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(Key)
            .ToList();
    }

    public static BatchImportResult Import(Store store, IEnumerable<string> files, ImportOptions? options = null,
        Action<int, int, string>? progress = null)
    {
        options ??= new ImportOptions();
        var started = DateTime.UtcNow;
        var batch = new BatchImportResult();
        var list = files.ToList();

        int index = 0;
        foreach (string file in list)
        {
            index++;
            progress?.Invoke(index - 1, list.Count, $"Importing {Path.GetFileName(file)} ({index} of {list.Count})");

            try
            {
                var importer = new TimeQPlusImporter(file);
                var result = importer.Import(store, options);
                batch.Files.Add((file, result));
                batch.Warnings.AddRange(result.Warnings.Select(w => $"{Path.GetFileName(file)}: {w}"));
            }
            catch (Exception ex)
            {
                var failed = new ImportResult();
                failed.Errors.Add(ex.Message);
                batch.Files.Add((file, failed));
                batch.Warnings.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        progress?.Invoke(list.Count, list.Count, "Finishing");
        batch.Duration = DateTime.UtcNow - started;
        return batch;
    }

    /// <summary>Aggregated preview across several databases.</summary>
    public sealed class BatchPreview
    {
        public List<(string File, ImportPreview Preview)> Files { get; } = new();
        public int EmployeeCount => Files.Select(f => f.Preview.EmployeeCount).DefaultIfEmpty(0).Max();
        public int TransactionCount => Files.Sum(f => f.Preview.TransactionCount);
        public DateTime? FirstPunch => Files.Select(f => f.Preview.FirstPunch).Where(d => d is not null).DefaultIfEmpty(null).Min();
        public DateTime? LastPunch => Files.Select(f => f.Preview.LastPunch).Where(d => d is not null).DefaultIfEmpty(null).Max();
        public List<string> Warnings { get; } = new();
    }

    public static BatchPreview Preview(IEnumerable<string> files)
    {
        var batch = new BatchPreview();

        foreach (string file in files)
        {
            try
            {
                var preview = new TimeQPlusImporter(file).Preview();
                batch.Files.Add((file, preview));
                foreach (string warning in preview.Warnings)
                    batch.Warnings.Add($"{Path.GetFileName(file)}: {warning}");
            }
            catch (Exception ex)
            {
                batch.Warnings.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        return batch;
    }
}

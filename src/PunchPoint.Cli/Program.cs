using System.Globalization;
using System.Text;
using PunchPoint.Core.Import;
using PunchPoint.Core.Jet;
using PunchPoint.Core.Models;
using PunchPoint.Core.Services;
using PunchPoint.Core.Storage;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Length == 0) return Usage();

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "info" => Info(args[1..]),
                "dump" => Dump(args[1..]),
                "hex" => Hex(args[1..]),
                "preview" => Preview(args[1..]),
                "import" => Import(args[1..]),
                "import-folder" => ImportFolder(args[1..]),
                "timecards" => TimeCards(args[1..]),
                "demo" => Demo(args[1..]),
                "doctor" => Doctor(args[1..]),
                "pdf" => Pdf(args[1..]),
                "help" or "-h" or "--help" => Usage(),
                _ => Unknown(args[0]),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            if (Environment.GetEnvironmentVariable("PUNCHPOINT_DEBUG") == "1")
                Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        return Usage();
    }

    private static int Usage()
    {
        Console.WriteLine("""
            PunchPoint command line tools

            Legacy database (timeQplus v3 / Access .mdb)
              punchpoint info <db.mdb>                    Tables, columns and row counts
              punchpoint dump <db.mdb> <table> [limit]    Rows as CSV
              punchpoint preview <db.mdb>                 What an import would bring in

            Import
              punchpoint import-folder <folder> [options]   Import arx_active.mdb + Archives/*.mdb
              punchpoint import <db.mdb> [options]
                --db <punchpoint.db>    Target database (default: app data folder)
                --from <yyyy-mm-dd>     Only punches on/after this date
                --to <yyyy-mm-dd>       Only punches on/before this date
                --no-employees          Do not add/update employees
                --no-settings           Do not copy company rules
                --misc-hours            Import transactions that carry hours (vacation etc.)
                --reset                 Delete the target database first

            Reporting
              punchpoint pdf <out.pdf> [--db <file>] [--period <n>]   Hours summary as PDF

            Verification
              punchpoint timecards [--db <file>] [--period <n>]   Print computed time cards
              punchpoint doctor [--db <file>]                     Startup path + integrity check
              punchpoint demo [--db <file>]                       Seed demo data for a quick look

            """);
        return 0;
    }

    // =================================================================
    // Legacy database inspection
    // =================================================================

    private static int Info(string[] args)
    {
        if (args.Length < 1) return Usage();

        using var db = Jet4Database.Open(args[0]);
        Console.WriteLine($"File:    {Path.GetFullPath(args[0])}");
        Console.WriteLine($"Size:    {new FileInfo(args[0]).Length / 1024.0 / 1024.0:F2} MB");
        Console.WriteLine($"Pages:   {db.PageCount}");
        Console.WriteLine();

        foreach (var name in db.TableNames)
        {
            try
            {
                var table = db.GetTable(name);
                Console.WriteLine($"  {name,-24} rows={table.CountRows()}");
                foreach (var col in table.Columns)
                    Console.WriteLine($"      {col.Type,-10} {col.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  {name,-24} <error: {ex.Message}>");
            }
        }
        return 0;
    }

    private static int Dump(string[] args)
    {
        if (args.Length < 2) return Usage();

        using var db = Jet4Database.Open(args[0]);
        var table = db.GetTable(args[1]);
        int limit = args.Length > 2 && int.TryParse(args[2], out int n) ? n : int.MaxValue;

        Console.WriteLine(string.Join(",", table.Columns.Select(c => Csv(c.Name))));
        int printed = 0;
        foreach (var row in table.Rows())
        {
            if (printed++ >= limit) break;
            Console.WriteLine(string.Join(",", table.Columns.Select(c => Csv(Format(row[c.Name])))));
        }
        return 0;
    }

    private static int Hex(string[] args)
    {
        if (args.Length < 2) return Usage();
        using var db = Jet4Database.Open(args[0]);
        var table = db.GetTable(args[1]);
        int limit = args.Length > 2 && int.TryParse(args[2], out int n) ? n : 2;

        foreach (var col in table.Columns)
            Console.WriteLine($"  {col.Name,-20} {col.Type,-10} num={col.Number} fixed={col.FixedOffset} size={col.FixedSize} var={col.IsVariable} varIdx={col.VarIndex}");

        int i = 0;
        foreach (var row in table.Rows())
        {
            if (i++ >= limit) break;
            Console.WriteLine($"-- row {row.RowNumber}/{row.PageNumber} --");
            foreach (var kv in row.Values)
                Console.WriteLine($"  {kv.Key,-20} = {Format(kv.Value)}");
        }
        return 0;
    }

    // =================================================================
    // Import
    // =================================================================

    private static int Preview(string[] args)
    {
        if (args.Length < 1) return Usage();

        var importer = new TimeQPlusImporter(args[0]);
        var preview = importer.Preview();

        Console.WriteLine($"Legacy database: {preview.SourcePath}");
        Console.WriteLine($"Tables found:    {string.Join(", ", preview.TablesFound)}");
        if (preview.TablesMissing.Count > 0)
            Console.WriteLine($"Tables missing:  {string.Join(", ", preview.TablesMissing)}");
        Console.WriteLine();
        Console.WriteLine($"Employees:    {preview.EmployeeCount}");
        Console.WriteLine($"Transactions: {preview.TransactionCount}");
        Console.WriteLine($"Shifts:       {preview.ShiftCount}");
        Console.WriteLine($"Pay periods:  {preview.PayPeriodCount}");
        Console.WriteLine($"Holidays:     {preview.HolidayCount}");
        Console.WriteLine($"Punch range:  {preview.FirstPunch:g} .. {preview.LastPunch:g}");
        Console.WriteLine($"Direction:    {preview.DetectedDirection}  ({preview.DirectionConfidence}%)");
        if (preview.MiscHourTransactions > 0)
            Console.WriteLine($"Hours rows:   {preview.MiscHourTransactions} ({preview.MiscHoursTotal:0.##} h, excluded by default)");
        Console.WriteLine();

        Console.WriteLine($"{"Employee",-24} {"Punches",7}  {"First day (direction check)",-70} Notes");
        foreach (var row in preview.Employees.OrderBy(e => e.Employee.SortName))
        {
            Console.WriteLine($"{Truncate(row.Employee.SortName, 24),-24} {row.PunchCount,7}  " +
                              $"{Truncate(row.FirstDaySummary, 70),-70} {string.Join("; ", row.Notes)}");
        }

        if (preview.Warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Warnings:");
            foreach (var w in preview.Warnings) Console.WriteLine($"  - {w}");
        }

        return 0;
    }

    private static int Import(string[] args)
    {
        if (args.Length < 1) return Usage();

        string mdb = args[0];
        string? dbPath = null;
        ImportOptions options = new();
        bool reset = false;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--db":
                    dbPath = args[++i];
                    break;
                case "--from":
                    options.FromDate = DateTime.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--to":
                    options.ToDate = DateTime.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--no-employees":
                    options.ImportEmployees = false;
                    break;
                case "--no-settings":
                    options.ImportSettings = false;
                    break;
                case "--misc-hours":
                    options.ImportMiscHours = true;
                    break;
                case "--reset":
                    reset = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unknown option '{args[i]}'.");
                    return 1;
            }
        }

        if (reset && dbPath is not null && File.Exists(dbPath))
        {
            File.Delete(dbPath);
            if (File.Exists(dbPath + "-wal")) File.Delete(dbPath + "-wal");
            if (File.Exists(dbPath + "-shm")) File.Delete(dbPath + "-shm");
        }

        using var database = dbPath is null ? Database.OpenDefault() : Database.Open(dbPath);
        var store = new Store(database);

        var importer = new TimeQPlusImporter(mdb);
        var result = importer.Import(store, options,
            (done, total, message) => Console.Error.WriteLine($"  {message}"));

        Console.WriteLine($"Imported from {Path.GetFileName(mdb)} in {result.Duration.TotalSeconds:0.0}s");
        Console.WriteLine($"  employees added:   {result.EmployeesAdded}");
        Console.WriteLine($"  employees updated: {result.EmployeesUpdated}");
        Console.WriteLine($"  shifts added:      {result.ShiftsAdded}");
        Console.WriteLine($"  periods added:     {result.PayPeriodsAdded}");
        Console.WriteLine($"  punches added:     {result.PunchesAdded}");
        Console.WriteLine($"  punches skipped:   {result.PunchesSkipped}");
        Console.WriteLine($"  unmatched punches: {result.UnmatchedPunches}");
        Console.WriteLine($"  holidays:          {result.HolidaysAdded}");
        Console.WriteLine($"  approvals:         {result.ApprovalsImported}");
        Console.WriteLine($"  target database:   {database.Path}");

        foreach (var warning in result.Warnings) Console.WriteLine($"  ! {warning}");
        foreach (var error in result.Errors) Console.WriteLine($"  ERROR {error}");

        return result.Success ? 0 : 2;
    }

    private static int ImportFolder(string[] args)
    {
        if (args.Length < 1) return Usage();

        string folder = args[0];
        string? dbPath = null;
        bool miscHours = false;
        bool reset = false;
        DateTime? from = null, to = null;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--db": dbPath = args[++i]; break;
                case "--misc-hours": miscHours = true; break;
                case "--reset": reset = true; break;
                case "--from": from = DateTime.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--to": to = DateTime.Parse(args[++i], CultureInfo.InvariantCulture); break;
                default:
                    Console.Error.WriteLine($"Unknown option '{args[i]}'.");
                    return 1;
            }
        }

        if (reset && dbPath is not null && File.Exists(dbPath))
        {
            foreach (string suffix in new[] { "", "-wal", "-shm" })
            {
                if (File.Exists(dbPath + suffix)) File.Delete(dbPath + suffix);
            }
        }

        var files = TimeQPlusFolderImporter.DiscoverDatabases(folder);
        Console.WriteLine($"Found {files.Count} database file(s) under {folder}");
        if (files.Count == 0) return 1;

        using var database = dbPath is null ? Database.OpenDefault() : Database.Open(dbPath);
        var store = new Store(database);

        var options = new ImportOptions
        {
            ImportMiscHours = miscHours,
            FromDate = from,
            ToDate = to,
        };

        var result = TimeQPlusFolderImporter.Import(store, files, options,
            (done, total, message) => Console.Error.WriteLine($"  [{done}/{total}] {message}"));

        Console.WriteLine($"Imported {result.FilesImported} of {files.Count} file(s) in {result.Duration.TotalSeconds:0.0}s");
        Console.WriteLine($"  employees added:   {result.EmployeesAdded}");
        Console.WriteLine($"  employees updated: {result.EmployeesUpdated}");
        Console.WriteLine($"  punches added:     {result.PunchesAdded}");
        Console.WriteLine($"  punches skipped:   {result.PunchesSkipped}");
        Console.WriteLine($"  unmatched punches: {result.UnmatchedPunches}");
        Console.WriteLine($"  target database:   {database.Path}");

        foreach (var warning in result.Warnings.Take(10))
            Console.WriteLine($"  ! {warning}");

        var failures = result.Files.Where(f => !f.Result.Success).ToList();
        foreach (var (file, failed) in failures)
            Console.WriteLine($"  ERROR {Path.GetFileName(file)}: {string.Join("; ", failed.Errors)}");

        return failures.Count == 0 ? 0 : 2;
    }

    // =================================================================
    // Verification
    // =================================================================

    private static int TimeCards(string[] args)
    {
        string? dbPath = null;
        int? periodId = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--db": dbPath = args[++i]; break;
                case "--period": periodId = int.Parse(args[++i]); break;
            }
        }

        using var database = dbPath is null ? Database.OpenDefault() : Database.Open(dbPath);
        var store = new Store(database);
        var settings = store.GetSettings();
        var employees = store.ListEmployees(includeInactive: true);
        var holidays = store.ListHolidays();

        var periods = periodId is int id
            ? new List<PayPeriod> { store.GetPayPeriod(id)! }
            : store.ListPayPeriods().OrderBy(p => p.Start).ToList();

        if (periods.Count == 0)
        {
            Console.WriteLine("No pay periods in the database.");
            return 0;
        }

        foreach (var period in periods)
        {
            Console.WriteLine();
            Console.WriteLine($"=== Pay period {period.Id}: {period.Start:ddd MMM d} – {period.End:ddd MMM d, yyyy} ({period.Type}) ===");

            foreach (var employee in employees.OrderBy(e => e.SortName))
            {
                var punches = store.ListPunches(employee.Id, period.Start, period.End.AddDays(1));
                if (punches.Count == 0) continue;

                var shift = employee.ShiftId is int shiftId ? store.GetShift(shiftId) : null;
                var card = store.GetTimeCard(employee.Id, period.Id);
                var result = TimeCardCalculator.Calculate(employee, shift, period, punches, settings, holidays, card);

                Console.WriteLine($"  {employee.SortName,-26} " +
                                  $"REG {Fmt(result.RegularMinutes),7}  " +
                                  $"OT1 {Fmt(result.OverTime1Minutes),6}  " +
                                  $"OT2 {Fmt(result.OverTime2Minutes),6}  " +
                                  $"HOL {Fmt(result.HolidayMinutes),5}  " +
                                  $"= {Fmt(result.TotalMinutes),7}");

                foreach (var day in result.Days.Where(d => d.Issues.Count > 0))
                    foreach (var issue in day.Issues)
                        Console.WriteLine($"      ! {day.WorkDate:ddd MM-dd}: {issue}");
            }
        }

        return 0;
    }

    private static string Fmt(int minutes) => (minutes / 60.0).ToString("0.00");

    private static int Pdf(string[] args)
    {
        if (args.Length < 1) return Usage();
        string outPath = args[0];
        string? dbPath = null;
        int? periodId = null;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--db": dbPath = args[++i]; break;
                case "--period": periodId = int.Parse(args[++i]); break;
            }
        }

        using var database = dbPath is null ? Database.OpenDefault() : Database.Open(dbPath);
        var store = new Store(database);
        var settings = store.GetSettings();
        var period = periodId is int id ? store.GetPayPeriod(id) : store.GetCurrentPayPeriod();
        if (period is null)
        {
            Console.Error.WriteLine("No pay period found.");
            return 1;
        }

        var employees = store.ListEmployees(includeInactive: true);
        var table = PunchPoint.Core.Reports.ReportBuilder.HoursSummary(store, period, settings, employees);
        PunchPoint.Core.Export.PdfReports.Write(table, outPath, settings.CompanyName);
        Console.WriteLine($"Wrote {outPath}");
        return 0;
    }

    private static int Doctor(string[] args)
    {
        string? dbPath = null;
        for (int i = 0; i < args.Length; i++)
            if (args[i] == "--db") dbPath = args[++i];

        using var database = dbPath is null ? Database.OpenDefault() : Database.Open(dbPath);
        var store = new Store(database);

        int _ = Maintenance.EnsurePeriods(store);
        string? backup = Maintenance.DailyBackup(database);

        Console.WriteLine(Maintenance.Describe(store));
        Console.WriteLine($"Backup:        {backup ?? "(already taken today)"}");
        Console.WriteLine($"Integrity:     {Maintenance.IntegrityCheck(store)}");
        return 0;
    }

    private static int Demo(string[] args)
    {
        string? dbPath = null;
        for (int i = 0; i < args.Length; i++)
            if (args[i] == "--db") dbPath = args[++i];

        using var database = dbPath is null ? Database.OpenDefault() : Database.Open(dbPath);
        var store = new Store(database);
        var clock = new ClockService(store);

        if (store.ListEmployees(includeInactive: true).Count > 0)
        {
            Console.WriteLine("Database already has employees; not seeding demo data.");
            return 0;
        }

        var settings = store.GetSettings();
        settings.CompanyName = "Northwind Print Shop";
        settings.CompanyMessage = "Please clock in when you arrive.";
        settings.PeriodType = PayPeriodType.Weekly;
        settings.PeriodAnchor = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + 1);
        if (settings.PeriodAnchor > DateTime.Today) settings.PeriodAnchor = settings.PeriodAnchor.AddDays(-7);
        settings.DailyOverTime1Minutes = 8 * 60;
        settings.RoundingMinutes = 0;
        store.SaveSettings(settings);

        var day = new Shift { Name = "Day Shift", Start = new TimeSpan(8, 0, 0), Stop = new TimeSpan(17, 0, 0), StartGraceMinutes = 5, StopGraceMinutes = 5 };
        store.SaveShift(day);

        var people = new[]
        {
            ("Maria", "Gonzalez", "1001", "Press operator"),
            ("James", "Okafor", "1002", "Prepress"),
            ("Dana", "Whitfield", "1003", "Shipping"),
            ("Luis", "Ramirez", "1004", "Bindery"),
        };

        var rand = new Random(42);
        int badge = 1001;

        foreach (var (first, last, pin, note) in people)
        {
            var employee = new Employee
            {
                FirstName = first,
                LastName = last,
                Pin = pin,
                Badge = (badge++).ToString(),
                ShiftId = day.Id,
                HireDate = DateTime.Today.AddYears(-2),
                Notes = note,
            };
            store.InsertEmployee(employee);

            for (int d = 9; d >= 0; d--)
            {
                var date = DateTime.Today.AddDays(-d);
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;

                var inTime = date.AddHours(8).AddMinutes(rand.Next(-4, 9));
                var outTime = date.AddHours(17).AddMinutes(rand.Next(-6, 12));

                clock.Punch(employee, PunchKind.In, inTime, source: PunchSource.Clock);
                clock.Punch(employee, PunchKind.Out, outTime, source: PunchSource.Clock);
            }
        }

        var period = store.GetCurrentPayPeriod();
        if (period is null)
        {
            var p = PayPeriodGenerator.Create(settings.PeriodAnchor, settings.PeriodType);
            store.UpsertPayPeriod(p);
        }

        Console.WriteLine($"Seeded demo data into {database.Path}");
        Console.WriteLine($"  {store.ListEmployees().Count} employees, {store.CountPunches()} punches");
        return 0;
    }

    // =================================================================
    // Formatting helpers
    // =================================================================

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..(length - 1)] + "…";

    private static string Format(object? value) => value switch
    {
        null => "",
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        bool b => b ? "1" : "0",
        double d => d.ToString("G17", CultureInfo.InvariantCulture),
        float f => f.ToString("G9", CultureInfo.InvariantCulture),
        decimal m => m.ToString(CultureInfo.InvariantCulture),
        byte[] bytes => $"<{bytes.Length} bytes>",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
    };

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}

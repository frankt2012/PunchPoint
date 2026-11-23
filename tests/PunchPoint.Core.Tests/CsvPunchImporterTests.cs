using PunchPoint.Core.Import;
using PunchPoint.Core.Models;
using PunchPoint.Core.Storage;

namespace PunchPoint.Core.Tests;

public class CsvPunchImporterTests
{
    private static string WriteCsv(params string[] lines)
    {
        string path = Path.Combine(Path.GetTempPath(), $"punchpoint-csv-{Guid.NewGuid():n}.csv");
        File.WriteAllLines(path, lines);
        return path;
    }

    [Fact]
    public void Detects_columns_and_alternates_direction()
    {
        string csv = WriteCsv(
            "Badge,Date/Time,Comment",
            "1001,2026-11-09 08:00,\"on time\"",
            "1001,2026-11-09 17:00,",
            "1001,2026-11-10 08:02,",
            "1001,2026-11-10 17:01,");

        try
        {
            var preview = new CsvPunchImporter(csv).Preview();
            Assert.True(preview.LooksUsable);
            Assert.Equal("Badge", preview.EmployeeColumn);
            Assert.Equal("Date/Time", preview.TimestampColumn);
            Assert.Equal(4, preview.ParsedRows);
            Assert.Single(preview.Employees);
            Assert.Equal(new DateTime(2026, 11, 9, 8, 0, 0), preview.FirstPunch);
        }
        finally
        {
            File.Delete(csv);
        }
    }

    [Fact]
    public void Imports_punches_and_matches_by_badge()
    {
        string csv = WriteCsv(
            "Employee,Date,Time,Type",
            "1001,2026-11-09,08:00,IN",
            "1001,2026-11-09,17:00,OUT",
            "1001,2026-11-10,08:00,IN",
            "1001,2026-11-10,17:00,OUT");

        string dbPath = Path.Combine(Path.GetTempPath(), $"punchpoint-csv-{Guid.NewGuid():n}.db");
        try
        {
            using var database = Database.Open(dbPath);
            var store = new Store(database);

            var employee = new Employee { FirstName = "Maria", LastName = "Gonzalez", Badge = "1001", Pin = "1001" };
            store.InsertEmployee(employee);

            var period = new PayPeriod { Start = new DateTime(2026, 11, 9), End = new DateTime(2026, 11, 15) };
            store.UpsertPayPeriod(period);

            var result = new CsvPunchImporter(csv).Import(store);
            Assert.True(result.Success, string.Join("; ", result.Errors));
            Assert.Equal(4, result.PunchesAdded);

            var punches = store.ListPunches(employee.Id, new DateTime(2026, 11, 9), new DateTime(2026, 11, 16));
            Assert.Equal(4, punches.Count);
            Assert.Equal(PunchKind.In, punches[0].Kind);
            Assert.Equal(PunchKind.Out, punches[1].Kind);
        }
        finally
        {
            File.Delete(csv);
            foreach (string suffix in new[] { "", "-wal", "-shm" })
            {
                try { File.Delete(dbPath + suffix); } catch { }
            }
        }
    }

    [Fact]
    public void Reports_unknown_employees()
    {
        string csv = WriteCsv(
            "PIN,Timestamp",
            "9999,2026-11-09 08:00",
            "9999,2026-11-09 17:00");

        string dbPath = Path.Combine(Path.GetTempPath(), $"punchpoint-csv-{Guid.NewGuid():n}.db");
        try
        {
            using var database = Database.Open(dbPath);
            var store = new Store(database);

            var result = new CsvPunchImporter(csv).Import(store);
            Assert.Equal(2, result.UnmatchedPunches);
            Assert.Equal(0, result.PunchesAdded);
            Assert.Contains(result.Warnings, w => w.Contains("9999"));
        }
        finally
        {
            File.Delete(csv);
            foreach (string suffix in new[] { "", "-wal", "-shm" })
            {
                try { File.Delete(dbPath + suffix); } catch { }
            }
        }
    }

    [Fact]
    public void Handles_tab_separated_files_and_24_hour_times()
    {
        string csv = WriteCsv(
            "Badge\tDate/Time",
            "1001\t11/09/2026 23:55",
            "1001\t11/10/2026 07:30");

        try
        {
            var preview = new CsvPunchImporter(csv).Preview();
            Assert.Equal("\t", preview.Delimiter);
            Assert.Equal(2, preview.ParsedRows);
            Assert.Equal(new DateTime(2026, 11, 9, 23, 55, 0), preview.FirstPunch);
        }
        finally
        {
            File.Delete(csv);
        }
    }

    [Theory]
    [InlineData("2026-11-09 08:00", 2026, 11, 9, 8, 0)]
    [InlineData("11/09/2026 08:00", 2026, 11, 9, 8, 0)]
    [InlineData("2026-11-09T08:00", 2026, 11, 9, 8, 0)]
    [InlineData("2026-11-09", 2026, 11, 9, 0, 0)]
    public void Parses_common_date_formats(string input, int year, int month, int day, int hour, int minute)
    {
        var parsed = CsvPunchImporter.TryParseTimestamp(input);
        Assert.NotNull(parsed);
        Assert.Equal(new DateTime(year, month, day, hour, minute, 0), parsed!.Value);
    }
}

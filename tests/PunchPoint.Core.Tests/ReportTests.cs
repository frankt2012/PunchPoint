using PunchPoint.Core.Export;
using PunchPoint.Core.Models;
using PunchPoint.Core.Reports;
using PunchPoint.Core.Storage;

namespace PunchPoint.Core.Tests;

public class ReportTests
{
    private static (Database Db, Store Store, Employee Employee, PayPeriod Period) Setup(string dbPath)
    {
        var db = Database.Open(dbPath);
        var store = new Store(db);
        var employee = new Employee { FirstName = "Report", LastName = "Tester", Pin = "1234", EmployeeNumber = "0001" };
        store.InsertEmployee(employee);
        var period = new PayPeriod { Start = new DateTime(2026, 11, 9), End = new DateTime(2026, 11, 15) };
        store.UpsertPayPeriod(period);

        store.InsertPunch(new Punch { EmployeeId = employee.Id, Timestamp = new DateTime(2026, 11, 9, 8, 0, 0), Kind = PunchKind.In });
        store.InsertPunch(new Punch { EmployeeId = employee.Id, Timestamp = new DateTime(2026, 11, 9, 17, 0, 0), Kind = PunchKind.Out });

        return (db, store, employee, period);
    }

    [Fact]
    public void Hours_summary_totals_the_period()
    {
        string path = TestPaths.NewDatabasePath();
        try
        {
            var (db, store, _, period) = Setup(path);
            using (db)
            {
                var settings = store.GetSettings();
                var employees = store.ListEmployees(includeInactive: true);
                var table = ReportBuilder.HoursSummary(store, period, settings, employees);

                Assert.Equal("Hours Summary", table.Title);
                Assert.Equal(2, table.Rows.Count); // employee + TOTAL
                Assert.Equal("Tester, Report", table.Rows[0][0]);
                Assert.Equal("9.00", table.Rows[0][6]);
                Assert.Equal("9.00", table.Rows[1][6]);
            }
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void Generic_ascii_export_writes_header_and_rows()
    {
        string path = TestPaths.NewDatabasePath();
        string csv = Path.Combine(Path.GetTempPath(), $"generic-{Guid.NewGuid():n}.txt");
        try
        {
            var (db, store, _, period) = Setup(path);
            using (db)
            {
                var rows = CsvExporter.BuildRows(store, period, store.GetSettings());
                CsvExporter.WriteGenericAscii(rows, csv);

                var lines = File.ReadAllLines(csv);
                Assert.Equal(2, lines.Length);
                Assert.Contains("Regular", lines[0]);
                Assert.Contains("Report", lines[1]);
                Assert.Contains("9.00", lines[1]);
            }
        }
        finally
        {
            Cleanup(path);
            try { File.Delete(csv); } catch { }
        }
    }

    private static void Cleanup(string dbPath)
    {
        foreach (string suffix in new[] { "", "-wal", "-shm" })
        {
            try { File.Delete(dbPath + suffix); } catch { }
        }
    }
}

public class PayPeriodLabelTests
{
    private static PayPeriod P(int id, DateTime start, int days) => new()
    {
        Id = id,
        Start = start,
        End = start.AddDays(days - 1),
    };

    [Fact]
    public void Marks_current_previous_and_next()
    {
        var a = P(1, new DateTime(2026, 11, 4), 7);
        var b = P(2, new DateTime(2026, 11, 11), 7);
        var c = P(3, new DateTime(2026, 11, 18), 7);
        var d = P(4, new DateTime(2026, 11, 25), 7);
        var all = new[] { a, b, c, d };

        Assert.StartsWith("Previous · ", PayPeriodLabels.Label(b, all, new DateTime(2026, 11, 20)));
        Assert.StartsWith("Current · ", PayPeriodLabels.Label(c, all, new DateTime(2026, 11, 20)));
        Assert.StartsWith("Next · ", PayPeriodLabels.Label(d, all, new DateTime(2026, 11, 20)));
        Assert.DoesNotContain("·", PayPeriodLabels.Label(a, all, new DateTime(2026, 11, 20)));
        Assert.Contains("Nov 11 – Nov 17, 2026", PayPeriodLabels.Label(b, all, new DateTime(2026, 11, 20)));
    }
}

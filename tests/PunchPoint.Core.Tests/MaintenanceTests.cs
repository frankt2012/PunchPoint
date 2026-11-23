using PunchPoint.Core.Storage;

namespace PunchPoint.Core.Tests;

public class MaintenanceTests
{
    [Fact]
    public void EnsurePeriods_is_idempotent()
    {
        string dbPath = TestPaths.NewDatabasePath();
        try
        {
            using var database = Database.Open(dbPath);
            var store = new Store(database);

            Maintenance.EnsurePeriods(store, new DateTime(2026, 11, 23));
            int first = store.ListPayPeriods().Count;
            Assert.True(first > 0);

            Maintenance.EnsurePeriods(store, new DateTime(2026, 11, 23));
            Assert.Equal(first, store.ListPayPeriods().Count);

            Maintenance.EnsurePeriods(store, new DateTime(2026, 11, 24));
            Assert.Equal(first, store.ListPayPeriods().Count);

            var current = store.GetCurrentPayPeriod(new DateTime(2026, 11, 24));
            Assert.NotNull(current);
            Assert.True(current!.Contains(new DateTime(2026, 11, 24)));
        }
        finally
        {
            Cleanup(dbPath);
        }
    }

    [Fact]
    public void EnsurePeriods_covers_existing_punch_dates()
    {
        string dbPath = TestPaths.NewDatabasePath();
        try
        {
            using var database = Database.Open(dbPath);
            var store = new Store(database);

            var employee = new Core.Models.Employee { FirstName = "Old", LastName = "Punch", Pin = "4242" };
            store.InsertEmployee(employee);

            var oldDate = new DateTime(2019, 3, 5, 9, 0, 0);
            store.InsertPunch(new Core.Models.Punch
            {
                EmployeeId = employee.Id,
                Timestamp = oldDate,
                Kind = Core.Models.PunchKind.In,
            });

            Maintenance.EnsurePeriods(store, new DateTime(2026, 11, 23));

            var period = store.GetPayPeriodFor(oldDate);
            Assert.NotNull(period);
            Assert.True(period!.Contains(oldDate));
        }
        finally
        {
            Cleanup(dbPath);
        }
    }

    [Fact]
    public void Daily_backup_is_taken_once_and_is_valid()
    {
        string dbPath = TestPaths.NewDatabasePath();
        string backupDir = Path.Combine(Path.GetTempPath(), $"punchpoint-backups-{Guid.NewGuid():n}");
        try
        {
            using var database = Database.Open(dbPath);
            var store = new Store(database);
            store.InsertEmployee(new Core.Models.Employee { FirstName = "A", LastName = "B", Pin = "1" });

            string? first = Maintenance.DailyBackup(database, backupDir);
            Assert.NotNull(first);
            Assert.True(File.Exists(first));

            string? second = Maintenance.DailyBackup(database, backupDir);
            Assert.Null(second);

            Assert.Single(Directory.GetFiles(backupDir, "*.db"));

            using var restored = Database.Open(first!);
            var restoredStore = new Store(restored);
            Assert.Single(restoredStore.ListEmployees(includeInactive: true));
            Assert.Equal("ok", Maintenance.IntegrityCheck(restoredStore));
        }
        finally
        {
            Cleanup(dbPath);
            try { Directory.Delete(backupDir, recursive: true); } catch { }
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

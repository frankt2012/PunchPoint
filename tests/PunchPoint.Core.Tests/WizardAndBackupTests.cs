using PunchPoint.Core.Models;
using PunchPoint.Core.Reports;
using PunchPoint.Core.Storage;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.Core.Tests;

public class MissingPunchFinderTests
{
    private static readonly Shift Day = new()
    {
        Name = "Day",
        Start = new TimeSpan(8, 0, 0),
        Stop = new TimeSpan(17, 0, 0),
    };

    private static Punch P(int employee, DateTime when, PunchKind kind) =>
        new() { EmployeeId = employee, Timestamp = when, Kind = kind };

    [Fact]
    public void Suggests_a_clock_out_at_shift_end_for_an_open_in()
    {
        var employee = new Employee { Id = 1, FirstName = "Maria", LastName = "Gonzalez" };
        var punches = new[] { P(1, new DateTime(2026, 11, 23, 8, 2, 0), PunchKind.In) };

        var found = MissingPunchFinder.Find(employee, Day, punches, 0, new DateTime(2026, 11, 23), new DateTime(2026, 11, 24));

        var item = Assert.Single(found);
        Assert.Equal(PunchKind.Out, item.Kind);
        Assert.Equal(new DateTime(2026, 11, 23, 17, 0, 0), item.Suggested);
        Assert.Contains("no clock-out", item.Reason);
    }

    [Fact]
    public void Suggests_a_clock_in_at_shift_start_for_a_lone_out()
    {
        var employee = new Employee { Id = 2, FirstName = "James", LastName = "Okafor" };
        var punches = new[] { P(2, new DateTime(2026, 11, 23, 17, 4, 0), PunchKind.Out) };

        var found = MissingPunchFinder.Find(employee, Day, punches, 0, new DateTime(2026, 11, 23), new DateTime(2026, 11, 24));

        var item = Assert.Single(found);
        Assert.Equal(PunchKind.In, item.Kind);
        Assert.Equal(new DateTime(2026, 11, 23, 8, 0, 0), item.Suggested);
    }

    [Fact]
    public void Falls_back_to_eight_hours_without_a_shift()
    {
        var employee = new Employee { Id = 3, FirstName = "Luis", LastName = "Ramirez" };
        var punches = new[] { P(3, new DateTime(2026, 11, 23, 9, 0, 0), PunchKind.In) };

        var found = MissingPunchFinder.Find(employee, null, punches, 0, new DateTime(2026, 11, 23), new DateTime(2026, 11, 24));

        var item = Assert.Single(found);
        Assert.Equal(new DateTime(2026, 11, 23, 17, 0, 0), item.Suggested);
    }

    [Fact]
    public void Reports_both_sides_of_a_double_clock_in()
    {
        var employee = new Employee { Id = 4, FirstName = "Dana", LastName = "Whitfield" };
        var punches = new[]
        {
            P(4, new DateTime(2026, 11, 23, 8, 0, 0), PunchKind.In),
            P(4, new DateTime(2026, 11, 23, 12, 0, 0), PunchKind.In),
        };

        var found = MissingPunchFinder.Find(employee, Day, punches, 0, new DateTime(2026, 11, 23), new DateTime(2026, 11, 24));

        Assert.Equal(2, found.Count);
        Assert.Contains(found, f => f.Kind == PunchKind.Out && f.Suggested == new DateTime(2026, 11, 23, 17, 0, 0));
        Assert.Contains(found, f => f.Reason.Contains("Two clock-ins"));
    }
}

public class AttendanceReportTests
{
    private static (string Db, Store Store) Open()
    {
        string path = TestPaths.NewDatabasePath();
        return (path, new Store(Database.Open(path)));
    }

    [Fact]
    public void Counts_late_early_and_absent_days()
    {
        var (path, store) = Open();
        try
        {
            var shift = new Shift { Name = "Day", Start = new TimeSpan(8, 0, 0), Stop = new TimeSpan(17, 0, 0) };
            store.SaveShift(shift);

            var employee = new Employee
            {
                FirstName = "Maria", LastName = "Gonzalez", Pin = "1",
                ShiftId = shift.Id, HireDate = new DateTime(2026, 1, 1),
            };
            store.InsertEmployee(employee);

            // Mon 23rd: 20 minutes late, left on time.
            store.InsertPunch(new Punch { EmployeeId = employee.Id, Timestamp = new DateTime(2026, 11, 16, 8, 20, 0), Kind = PunchKind.In });
            store.InsertPunch(new Punch { EmployeeId = employee.Id, Timestamp = new DateTime(2026, 11, 16, 17, 0, 0), Kind = PunchKind.Out });
            // Tue 24th: on time, left 30 minutes early.
            store.InsertPunch(new Punch { EmployeeId = employee.Id, Timestamp = new DateTime(2026, 11, 17, 7, 58, 0), Kind = PunchKind.In });
            store.InsertPunch(new Punch { EmployeeId = employee.Id, Timestamp = new DateTime(2026, 11, 17, 16, 30, 0), Kind = PunchKind.Out });
            // Wed 25th: nothing (absent).
            // Thu 26th: a paid holiday, skipped.
            store.SaveHoliday(new Holiday { Name = "Holiday", Date = new DateTime(2026, 11, 19) });

            var period = new PayPeriod
            {
                Start = new DateTime(2026, 11, 16),
                End = new DateTime(2026, 11, 20),
                Type = PayPeriodType.Weekly,
            };
            store.UpsertPayPeriod(period);

            var settings = store.GetSettings();
            var table = ReportBuilder.Attendance(store, period, settings, new[] { employee });

            // one row per employee plus a totals row
            Assert.Equal(2, table.Rows.Count);

            var row = table.Rows[0];
            Assert.Equal("Gonzalez, Maria", row[0]);
            Assert.Equal("4", row[2]);   // Mon-Thu; the holiday and the weekend are skipped
            Assert.Equal("1", row[3]);   // one late day
            Assert.Equal("0:20", row[4]); // twenty minutes late
            Assert.Equal("1", row[5]);   // one early departure
            Assert.Equal("0:30", row[6]);
            Assert.Equal("2", row[7]);   // Wednesday and Friday had no punches
        }
        finally
        {
            foreach (string suffix in new[] { "", "-wal", "-shm" })
            {
                try { File.Delete(path + suffix); } catch { }
            }
        }
    }
}

public class BackupServiceTests
{
    [Fact]
    public void Prune_keeps_the_newest_backups()
    {
        string folder = Path.Combine(Path.GetTempPath(), "pp-prune-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            for (int day = 1; day <= 5; day++)
            {
                string file = Path.Combine(folder, $"punchpoint-2026110{day}-120000.db");
                File.WriteAllText(file, "x");
                File.SetLastWriteTimeUtc(file, new DateTime(2026, 11, day, 12, 0, 0, DateTimeKind.Utc));
            }

            int removed = BackupService.Prune(folder, keep: 3);

            Assert.Equal(2, removed);
            Assert.Equal(3, Directory.GetFiles(folder, "*.db").Length);
            Assert.True(File.Exists(Path.Combine(folder, "punchpoint-20261105-120000.db")));
            Assert.False(File.Exists(Path.Combine(folder, "punchpoint-20261101-120000.db")));
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Mirror_copies_the_file_into_the_chosen_folder()
    {
        string root = Path.Combine(Path.GetTempPath(), "pp-mirror-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string file = Path.Combine(root, "punchpoint-20261124-120000.db");
            File.WriteAllText(file, "backup");

            string cloud = Path.Combine(root, "Google Drive", "PunchPoint backups");
            string? copy = BackupService.Mirror(file, cloud);

            Assert.NotNull(copy);
            Assert.True(File.Exists(copy));
            Assert.Equal("backup", File.ReadAllText(copy!));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void EnsureDailyBackup_makes_one_per_day_and_is_idempotent()
    {
        string path = TestPaths.NewDatabasePath();
        string dataDir = Path.Combine(Path.GetTempPath(), "pp-data-" + Guid.NewGuid().ToString("N"));
        string mirrorDir = Path.Combine(dataDir, "mirror");
        string previous = Environment.GetEnvironmentVariable("PUNCHPOINT_DATA") ?? "";

        // Point the backup folders at a temp directory for the duration of the test.
        Environment.SetEnvironmentVariable("PUNCHPOINT_DATA", dataDir);
        try
        {
            using var database = Database.Open(path);
            var store = new Store(database);
            var settings = store.GetSettings();
            settings.BackupRetention = 2;
            settings.BackupMirrorFolder = mirrorDir;
            store.SaveSettings(settings);

            var now = new DateTime(2026, 11, 24, 9, 0, 0);

            string? first = BackupService.EnsureDailyBackup(store, store.GetSettings(), now);
            Assert.NotNull(first);
            Assert.True(File.Exists(first));
            Assert.Single(Directory.GetFiles(AppPaths.BackupsDirectory, "*.db"));
            Assert.Single(Directory.GetFiles(mirrorDir, "*.db"));

            string? second = BackupService.EnsureDailyBackup(store, store.GetSettings(), now.AddHours(5));
            Assert.Null(second); // same day, nothing new
            Assert.Single(Directory.GetFiles(AppPaths.BackupsDirectory, "*.db"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PUNCHPOINT_DATA", previous.Length == 0 ? null : previous);
            try { Directory.Delete(dataDir, recursive: true); } catch { }
            foreach (string suffix in new[] { "", "-wal", "-shm" })
            {
                try { File.Delete(path + suffix); } catch { }
            }
        }
    }
}

public class ClockExitSettingTests
{
    [Fact]
    public void Clock_exit_password_and_backup_preferences_round_trip()
    {
        string path = TestPaths.NewDatabasePath();
        try
        {
            using var database = Database.Open(path);
            var store = new Store(database);

            var defaults = store.GetSettings();
            Assert.True(defaults.RequirePasswordToCloseClock);
            Assert.Equal(14, defaults.BackupRetention);
            Assert.Equal("", defaults.BackupMirrorFolder);

            var settings = store.GetSettings();
            settings.RequirePasswordToCloseClock = false;
            settings.BackupRetention = 30;
            settings.BackupMirrorFolder = @"C:\Users\frank\Google Drive\PunchPoint";
            store.SaveSettings(settings);

            var reloaded = store.GetSettings();
            Assert.False(reloaded.RequirePasswordToCloseClock);
            Assert.Equal(30, reloaded.BackupRetention);
            Assert.Equal(@"C:\Users\frank\Google Drive\PunchPoint", reloaded.BackupMirrorFolder);
        }
        finally
        {
            foreach (string suffix in new[] { "", "-wal", "-shm" })
            {
                try { File.Delete(path + suffix); } catch { }
            }
        }
    }
}

public class AttendanceFutureDaysTests
{
    [Fact]
    public void Days_that_have_not_happened_yet_are_not_absences()
    {
        string path = TestPaths.NewDatabasePath();
        try
        {
            using var database = Database.Open(path);
            var store = new Store(database);

            var employee = new Employee { FirstName = "Future", LastName = "Person", Pin = "9" };
            store.InsertEmployee(employee);

            // A period entirely in the future: no work days should be counted at all.
            var period = new PayPeriod
            {
                Start = DateTime.Today.AddDays(60),
                End = DateTime.Today.AddDays(66),
                Type = PayPeriodType.Weekly,
            };
            store.UpsertPayPeriod(period);

            var table = ReportBuilder.Attendance(store, period, store.GetSettings(), new[] { employee });

            var row = table.Rows[0];
            Assert.Equal("0", row[2]); // work days
            Assert.Equal("0", row[7]); // absent
        }
        finally
        {
            foreach (string suffix in new[] { "", "-wal", "-shm" })
            {
                try { File.Delete(path + suffix); } catch { }
            }
        }
    }
}

public class AttendanceWithoutShiftTests
{
    [Fact]
    public void Employees_without_a_shift_are_not_scored_for_punctuality()
    {
        string path = TestPaths.NewDatabasePath();
        try
        {
            using var database = Database.Open(path);
            var store = new Store(database);

            var employee = new Employee { FirstName = "No", LastName = "Shift", Pin = "7" };
            store.InsertEmployee(employee);

            var period = new PayPeriod
            {
                Start = new DateTime(2026, 11, 16),
                End = new DateTime(2026, 11, 20),
                Type = PayPeriodType.Weekly,
            };
            store.UpsertPayPeriod(period);

            store.InsertPunch(new Punch { EmployeeId = employee.Id, Timestamp = new DateTime(2026, 11, 16, 10, 30, 0), Kind = PunchKind.In });
            store.InsertPunch(new Punch { EmployeeId = employee.Id, Timestamp = new DateTime(2026, 11, 16, 15, 0, 0), Kind = PunchKind.Out });

            var table = ReportBuilder.Attendance(store, period, store.GetSettings(), new[] { employee });
            var row = table.Rows[0];

            Assert.Contains("no shift", row[1]);
            Assert.Equal("—", row[3]);   // late: not measured
            Assert.Equal("—", row[5]);   // early: not measured
            Assert.Equal("4", row[7]);   // the other four days are absences
        }
        finally
        {
            foreach (string suffix in new[] { "", "-wal", "-shm" })
            {
                try { File.Delete(path + suffix); } catch { }
            }
        }
    }
}

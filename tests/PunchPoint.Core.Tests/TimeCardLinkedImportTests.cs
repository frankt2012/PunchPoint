using PunchPoint.Core.Import;
using PunchPoint.Core.Models;
using PunchPoint.Core.Storage;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.Core.Tests;

/// <summary>
/// Regression tests modelled on a real timeQplus installation that used the PC clock:
/// punches carry no badge and are linked through TimeCards.TimeCardID, employees have
/// only a PIN (some with leading zeros), shifts are disabled placeholders, daily
/// overtime is disabled at 1440 and weekly overtime is set to 2400.
/// </summary>
public class TimeCardLinkedImportTests
{
    private static string Fixture => Path.Combine(TestPaths.Fixtures, "legacy-timecard-link.mdb");

    private static (Database Db, Store Store) OpenTemp(out string path)
    {
        path = TestPaths.NewDatabasePath();
        var db = Database.Open(path);
        return (db, new Store(db));
    }

    private static void Cleanup(string path)
    {
        foreach (string suffix in new[] { "", "-wal", "-shm" })
        {
            try { File.Delete(path + suffix); } catch { }
        }
    }

    [Fact]
    public void Preview_resolves_punches_through_time_cards()
    {
        var preview = new TimeQPlusImporter(Fixture).Preview();

        Assert.Equal(3, preview.EmployeeCount);
        Assert.Equal(27, preview.TransactionCount);
        Assert.DoesNotContain(preview.Warnings, w => w.Contains("not in the Employees table"));
        Assert.All(preview.Employees, e => Assert.True(e.PunchCount > 0));

        var wayne = preview.Employees.Single(e => e.Employee.LastName == "Scott");
        Assert.Contains("12:00 PM OUT", wayne.FirstDaySummary);
        Assert.Contains("12:30 PM IN", wayne.FirstDaySummary);
    }

    [Fact]
    public void Import_links_every_punch_and_skips_placeholder_periods_and_disabled_shifts()
    {
        var (db, store) = OpenTemp(out string path);
        try
        {
            using (db)
            {
                var result = new TimeQPlusImporter(Fixture).Import(store);

                Assert.True(result.Success);
                Assert.Equal(3, result.EmployeesAdded);
                Assert.Equal(27, result.PunchesAdded);
                Assert.Equal(0, result.UnmatchedPunches);
                Assert.Equal(0, result.ShiftsAdded);       // the only shift is disabled
                Assert.Equal(2, result.PayPeriodsAdded);   // the 3-month placeholder is skipped
            }
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void Import_copies_the_company_overtime_rules()
    {
        var (db, store) = OpenTemp(out string path);
        try
        {
            using (db)
            {
                new TimeQPlusImporter(Fixture).Import(store);
                var settings = store.GetSettings();

                Assert.Equal(0, settings.DailyOverTime1Minutes);   // 1440 in the legacy file means off
                Assert.Equal(0, settings.DailyOverTime2Minutes);
                Assert.Equal(40 * 60, settings.WeeklyOverTime1Minutes);
                Assert.Equal(0, settings.WeeklyOverTime2Minutes);  // 10080 means off
                Assert.Equal(0, settings.RoundingMinutes);         // PayIntervalRound is an enum, not minutes
                Assert.Equal(12 * 60, settings.MaxPunchSpanMinutes);
            }
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void Imported_employees_keep_pins_and_drop_the_1899_epoch_date()
    {
        var (db, store) = OpenTemp(out string path);
        try
        {
            using (db)
            {
                new TimeQPlusImporter(Fixture).Import(store);
                var smith = store.ListEmployees(includeInactive: true).Single(e => e.LastName == "Smith");

                Assert.Equal("0744", smith.Pin);
                Assert.Null(smith.HireDate);
                Assert.Equal("000000012", smith.EmployeeNumber);
                Assert.Null(smith.ShiftId);
            }
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void Time_cards_match_the_legacy_weekly_overtime_rule()
    {
        var (db, store) = OpenTemp(out string path);
        try
        {
            using (db)
            {
                new TimeQPlusImporter(Fixture).Import(store);

                var period = store.ListPayPeriods().Single(p => p.Start == new DateTime(2026, 11, 11));
                var settings = store.GetSettings();
                var employees = store.ListEmployees(includeInactive: true);

                TimeCardResult Card(string lastName)
                {
                    var employee = employees.Single(e => e.LastName == lastName);
                    var punches = store.ListPunches(employee.Id, period.Start, period.End.AddDays(1));
                    return TimeCardCalculator.Calculate(employee, null, period, punches, settings);
                }

                var patrick = Card("Smith");
                Assert.Equal(40 * 60, patrick.RegularMinutes);
                Assert.Equal(5 * 60, patrick.OverTime1Minutes);

                var ty = Card("Healey");
                Assert.Equal(40 * 60, ty.RegularMinutes);
                Assert.Equal(450, ty.OverTime1Minutes); // 7.5 h

                var wayne = Card("Scott");
                Assert.Equal(17 * 60 + 30, wayne.RegularMinutes);
                Assert.Equal(0, wayne.OverTime1Minutes);
                Assert.Contains(wayne.Issues, i => i.Contains("Missing OUT", StringComparison.OrdinalIgnoreCase));
            }
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void Punches_can_be_found_with_or_without_leading_zeros()
    {
        var (db, store) = OpenTemp(out string path);
        try
        {
            using (db)
            {
                new TimeQPlusImporter(Fixture).Import(store);

                Assert.Equal("Smith", store.FindByPin("0744")?.LastName);
                Assert.Equal("Smith", store.FindByPin("744")?.LastName);
                Assert.Equal("Healey", store.FindByPin("6719")?.LastName);
                Assert.Null(store.FindByPin("9999"));
            }
        }
        finally
        {
            Cleanup(path);
        }
    }
}

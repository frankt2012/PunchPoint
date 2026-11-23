using PunchPoint.Core.Import;
using PunchPoint.Core.Models;
using PunchPoint.Core.Storage;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.Core.Tests;

public class PayPeriodGeneratorTests
{
    [Fact]
    public void Weekly_period_runs_monday_to_sunday()
    {
        var period = PayPeriodGenerator.Create(new DateTime(2026, 11, 9), PayPeriodType.Weekly);

        Assert.Equal(new DateTime(2026, 11, 9), period.Start);
        Assert.Equal(new DateTime(2026, 11, 15), period.End);
        Assert.Equal(new DateTime(2026, 11, 16), PayPeriodGenerator.NextStart(period));
    }

    [Fact]
    public void Biweekly_period_is_fourteen_days()
    {
        var period = PayPeriodGenerator.Create(new DateTime(2026, 11, 9), PayPeriodType.BiWeekly);

        Assert.Equal(new DateTime(2026, 11, 22), period.End);
        Assert.Equal(new DateTime(2026, 11, 23), PayPeriodGenerator.NextStart(period));
    }

    [Fact]
    public void Semi_monthly_periods_split_at_the_configured_day()
    {
        var first = PayPeriodGenerator.Create(new DateTime(2026, 11, 1), PayPeriodType.SemiMonthly, splitDay: 16);
        Assert.Equal(new DateTime(2026, 11, 15), first.End);

        var second = PayPeriodGenerator.Create(new DateTime(2026, 11, 16), PayPeriodType.SemiMonthly, splitDay: 16);
        Assert.Equal(new DateTime(2026, 11, 30), second.End);

        var next = PayPeriodGenerator.Create(new DateTime(2026, 12, 1), PayPeriodType.SemiMonthly, splitDay: 16);
        Assert.Equal(new DateTime(2026, 12, 15), next.End);
    }

    [Fact]
    public void Monthly_period_covers_the_whole_month()
    {
        var period = PayPeriodGenerator.Create(new DateTime(2026, 2, 1), PayPeriodType.Monthly);
        Assert.Equal(new DateTime(2026, 2, 28), period.End);
    }

    [Fact]
    public void EnsureRange_covers_the_requested_window()
    {
        var settings = new AppSettings
        {
            PeriodType = PayPeriodType.Weekly,
            PeriodAnchor = new DateTime(2026, 11, 9),
        };

        var periods = PayPeriodGenerator.EnsureRange(settings, new DateTime(2026, 10, 1), new DateTime(2026, 12, 31));

        Assert.All(periods, p => Assert.Equal(DayOfWeek.Monday, p.Start.DayOfWeek));
        Assert.All(periods, p => Assert.Equal(7, p.DayCount));
        Assert.True(periods.Min(p => p.Start) <= new DateTime(2026, 10, 1));
        Assert.True(periods.Max(p => p.End) >= new DateTime(2026, 12, 31));
    }
}

public class PunchDirectionDetectorTests
{
    private static LegacyTransaction T(int minute, int state = 0, int type = 1, int hours = 0, string badge = "1001") => new()
    {
        Timestamp = new DateTime(2026, 11, 9).AddMinutes(minute),
        Badge = badge,
        State = state,
        Type = type,
        Hours = hours,
    };

    [Fact]
    public void Detects_a_binary_state_column()
    {
        var list = new List<LegacyTransaction>
        {
            T(0, 0), T(480, 1), T(1440, 0), T(1920, 1),
        };

        var detection = PunchDirectionDetector.Detect(list, PunchDirectionMode.Auto);
        Assert.Equal(PunchDirectionMode.UseState, detection.Mode);
        Assert.Equal(100, detection.Confidence);

        PunchDirectionDetector.Assign(list, detection);
        Assert.Equal(PunchKind.In, list[0].Kind);
        Assert.Equal(PunchKind.Out, list[1].Kind);
        Assert.Equal(PunchKind.In, list[2].Kind);
        Assert.Equal(PunchKind.Out, list[3].Kind);
    }

    [Fact]
    public void Falls_back_to_alternation_when_codes_are_unusable()
    {
        var list = new List<LegacyTransaction>
        {
            T(0, 0), T(480, 0), T(1440, 0), T(1920, 0),
        };

        var detection = PunchDirectionDetector.Detect(list, PunchDirectionMode.Alternate);
        Assert.Equal(PunchDirectionMode.Alternate, detection.Mode);

        PunchDirectionDetector.Assign(list, detection);
        Assert.Equal(new[] { PunchKind.In, PunchKind.Out, PunchKind.In, PunchKind.Out },
            list.Select(t => t.Kind).ToArray());
    }

    [Fact]
    public void Recognises_terminal_break_codes()
    {
        var list = new List<LegacyTransaction>
        {
            T(0, 0), T(240, 2), T(270, 3), T(540, 1),
            T(1440, 0), T(1680, 2), T(1710, 3), T(1980, 1),
        };

        var detection = PunchDirectionDetector.Detect(list, PunchDirectionMode.Auto);
        PunchDirectionDetector.Assign(list, detection);

        Assert.Equal(new[]
        {
            PunchKind.In, PunchKind.BreakOut, PunchKind.BreakIn, PunchKind.Out,
            PunchKind.In, PunchKind.BreakOut, PunchKind.BreakIn, PunchKind.Out,
        }, list.Select(t => t.Kind).ToArray());
    }

    [Fact]
    public void Alternation_resyncs_after_a_missed_punch()
    {
        // 8:00 in, 17:00 out, then next morning 8:00 in (no out recorded the day before).
        var list = new List<LegacyTransaction>
        {
            T(0), T(540),
            T(1440), T(1980),
            T(2880),   // missed OUT, then next IN after 14h+
        };

        var detection = PunchDirectionDetector.Detect(list, PunchDirectionMode.Alternate);
        PunchDirectionDetector.Assign(list, detection);

        Assert.Equal(new[] { PunchKind.In, PunchKind.Out, PunchKind.In, PunchKind.Out, PunchKind.In },
            list.Select(t => t.Kind).ToArray());
    }

    [Fact]
    public void Hours_transactions_are_excluded_from_pairing()
    {
        var list = new List<LegacyTransaction>
        {
            T(0, 0), T(240, 0, hours: 480), T(540, 1),
        };

        var detection = PunchDirectionDetector.Detect(list, PunchDirectionMode.Auto);
        PunchDirectionDetector.Assign(list, detection);

        Assert.Equal(PunchKind.Hours, list[1].Kind);
        Assert.Equal(PunchKind.In, list[0].Kind);
        Assert.Equal(PunchKind.Out, list[2].Kind);
    }
}

public class ImporterTests
{
    [Fact]
    public void Preview_reports_what_will_be_imported()
    {
        var importer = new TimeQPlusImporter(Path.Combine(TestPaths.Fixtures, "legacy-timeqplus.mdb"));
        var preview = importer.Preview();

        Assert.Equal(6, preview.EmployeeCount);
        Assert.Equal(86, preview.TransactionCount);
        Assert.Equal(2, preview.ShiftCount);
        Assert.Equal(2, preview.PayPeriodCount);
        Assert.Equal(2, preview.HolidayCount);
        Assert.Equal(1, preview.MiscHourTransactions);
        Assert.NotNull(preview.FirstPunch);
        Assert.NotNull(preview.LastPunch);
        Assert.Equal(6, preview.Employees.Count);
    }

    [Fact]
    public void Import_writes_employees_punches_and_periods()
    {
        string dbPath = TestPaths.NewDatabasePath();
        try
        {
            using var database = Database.Open(dbPath);
            var store = new Store(database);

            var importer = new TimeQPlusImporter(Path.Combine(TestPaths.Fixtures, "legacy-timeqplus.mdb"));
            var result = importer.Import(store);

            Assert.True(result.Success);
            Assert.Equal(6, result.EmployeesAdded);
            Assert.Equal(2, result.ShiftsAdded);
            Assert.Equal(85, result.PunchesAdded); // one vacation-hours row excluded by default
            Assert.Equal(1, result.PunchesSkipped);
            Assert.Equal(0, result.UnmatchedPunches);

            var employees = store.ListEmployees(includeInactive: true);
            Assert.Contains(employees, e => e.LastName == "Gonzalez" && e.Pin == "1001");

            var maria = employees.Single(e => e.Pin == "1001");
            Assert.NotNull(maria.ShiftId);
            var mariaPunches = store.ListPunches(maria.Id, new DateTime(2026, 11, 9), new DateTime(2026, 11, 23));
            Assert.NotEmpty(mariaPunches);
            Assert.All(mariaPunches, p => Assert.Equal(PunchSource.Import, p.Source));
        }
        finally
        {
            Cleanup(dbPath);
        }
    }

    [Fact]
    public void Importing_twice_does_not_duplicate_punches()
    {
        string dbPath = TestPaths.NewDatabasePath();
        try
        {
            using var database = Database.Open(dbPath);
            var store = new Store(database);
            var importer = new TimeQPlusImporter(Path.Combine(TestPaths.Fixtures, "legacy-timeqplus.mdb"));

            importer.Import(store);
            int firstCount = store.CountPunches();
            var second = importer.Import(store);

            Assert.Equal(firstCount, store.CountPunches());
            Assert.Equal(0, second.PunchesAdded);
        }
        finally
        {
            Cleanup(dbPath);
        }
    }

    [Fact]
    public void Importing_misc_hours_adds_paid_hours_rows()
    {
        string dbPath = TestPaths.NewDatabasePath();
        try
        {
            using var database = Database.Open(dbPath);
            var store = new Store(database);
            var importer = new TimeQPlusImporter(Path.Combine(TestPaths.Fixtures, "legacy-timeqplus.mdb"));

            var result = importer.Import(store, new ImportOptions { ImportMiscHours = true });

            Assert.Equal(86, result.PunchesAdded);
            var luis = store.ListEmployees(includeInactive: true).Single(e => e.LastName == "Ramirez");
            var punches = store.ListPunches(luis.Id, new DateTime(2026, 11, 9), new DateTime(2026, 11, 23));
            Assert.Contains(punches, p => p.Kind == PunchKind.Hours && p.Hours == TimeSpan.FromHours(8));
        }
        finally
        {
            Cleanup(dbPath);
        }
    }

    [Fact]
    public void Imported_time_cards_produce_sensible_totals()
    {
        string dbPath = TestPaths.NewDatabasePath();
        try
        {
            using var database = Database.Open(dbPath);
            var store = new Store(database);
            new TimeQPlusImporter(Path.Combine(TestPaths.Fixtures, "legacy-timeqplus.mdb")).Import(store);

            var settings = store.GetSettings();
            var period = store.ListPayPeriods()
                .Single(p => p.Start == new DateTime(2026, 11, 9));
            var james = store.ListEmployees(includeInactive: true).Single(e => e.LastName == "Okafor");
            var shift = store.GetShift(james.ShiftId!.Value);

            var punches = store.ListPunches(james.Id, period.Start, period.End.AddDays(1));
            var result = TimeCardCalculator.Calculate(james, shift, period, punches, settings, store.ListHolidays());

            Assert.True(result.TotalMinutes > 60 * 60, $"expected more than 60 hours, got {result.TotalMinutes} minutes");
            Assert.True(result.TotalMinutes < 100 * 60);
            Assert.Equal(8, result.Days.Count);
        }
        finally
        {
            Cleanup(dbPath);
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

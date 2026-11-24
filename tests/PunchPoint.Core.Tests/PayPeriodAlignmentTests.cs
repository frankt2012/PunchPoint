using PunchPoint.Core.Models;
using PunchPoint.Core.Storage;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.Core.Tests;

public class PayPeriodAlignmentTests
{
    private static (string Path, Store Store) Open()
    {
        string path = TestPaths.NewDatabasePath();
        return (path, new Store(Database.Open(path)));
    }

    private static void Clean(string path)
    {
        foreach (string suffix in new[] { "", "-wal", "-shm" })
        {
            try { File.Delete(path + suffix); } catch { }
        }
    }

    [Fact]
    public void Wednesday_anchor_gives_wednesday_to_tuesday_periods()
    {
        var (path, store) = Open();
        try
        {
            var settings = store.GetSettings();
            settings.PeriodType = PayPeriodType.Weekly;
            settings.PeriodAnchor = new DateTime(2026, 11, 11);   // a Wednesday
            store.SaveSettings(settings);

            Maintenance.RealignPeriods(store, new DateTime(2026, 11, 24)); // a Tuesday

            var period = store.GetPayPeriodFor(new DateTime(2026, 11, 24));
            Assert.NotNull(period);
            Assert.Equal(new DateTime(2026, 11, 18), period!.Start);      // Wednesday
            Assert.Equal(new DateTime(2026, 11, 24), period.End);         // Tuesday
            Assert.All(store.ListPayPeriods(), p => Assert.Equal(DayOfWeek.Wednesday, p.Start.DayOfWeek));
        }
        finally { Clean(path); }
    }

    [Fact]
    public void The_aligned_period_wins_when_two_grids_overlap()
    {
        var (path, store) = Open();
        try
        {
            // What the app does on a first run, anchored on the Tuesday it was installed.
            var settings = store.GetSettings();
            settings.PeriodType = PayPeriodType.Weekly;
            settings.PeriodAnchor = new DateTime(2026, 11, 24);
            store.SaveSettings(settings);
            Maintenance.EnsurePeriods(store, new DateTime(2026, 11, 24));

            // Then the old software's grid arrives, anchored on a Wednesday.
            settings.PeriodAnchor = new DateTime(2026, 11, 11);
            store.SaveSettings(settings);
            Maintenance.RealignPeriods(store, new DateTime(2026, 11, 24));

            // Both grids still exist (nothing is deleted), but the lookup returns the real one.
            var current = store.GetPayPeriodFor(new DateTime(2026, 11, 24));
            Assert.Equal(new DateTime(2026, 11, 18), current!.Start);
            Assert.Equal(new DateTime(2026, 11, 24), current.End);
        }
        finally { Clean(path); }
    }

    [Fact]
    public void Punches_from_a_stale_grid_move_to_the_correct_period()
    {
        var (path, store) = Open();
        try
        {
            var settings = store.GetSettings();
            settings.PeriodType = PayPeriodType.Weekly;
            settings.PeriodAnchor = new DateTime(2026, 11, 11);
            store.SaveSettings(settings);

            var employee = new Employee { FirstName = "Keep", LastName = "Punches", Pin = "5" };
            store.InsertEmployee(employee);

            var stale = new PayPeriod
            {
                Start = new DateTime(2026, 11, 10),   // Tuesday: off the Wednesday grid
                End = new DateTime(2026, 11, 16),
                Type = PayPeriodType.Weekly,
            };
            store.UpsertPayPeriod(stale);

            var punch = new Punch
            {
                EmployeeId = employee.Id,
                Timestamp = new DateTime(2026, 11, 12, 9, 0, 0),
                Kind = PunchKind.In,
                PayPeriodId = stale.Id,
            };
            store.InsertPunch(punch);

            Maintenance.RealignPeriods(store, new DateTime(2026, 11, 24));

            var kept = Assert.Single(store.ListPunches(employee.Id, new DateTime(2026, 11, 1), new DateTime(2026, 12, 1)));
            var period = store.GetPayPeriodFor(kept.Timestamp);
            Assert.Equal(new DateTime(2026, 11, 11), period!.Start);
        }
        finally { Clean(path); }
    }

    [Fact]
    public void An_approved_stale_period_is_never_removed()
    {
        var (path, store) = Open();
        try
        {
            var settings = store.GetSettings();
            settings.PeriodType = PayPeriodType.Weekly;
            settings.PeriodAnchor = new DateTime(2026, 11, 11);
            store.SaveSettings(settings);

            var employee = new Employee { FirstName = "Approved", LastName = "Card", Pin = "6" };
            store.InsertEmployee(employee);

            var stale = new PayPeriod
            {
                Start = new DateTime(2026, 11, 10),
                End = new DateTime(2026, 11, 16),
                Type = PayPeriodType.Weekly,
            };
            store.UpsertPayPeriod(stale);

            var card = store.GetOrCreateTimeCard(employee.Id, stale.Id);
            card.Approved = true;
            store.SaveTimeCard(card);

            Maintenance.RealignPeriods(store, new DateTime(2026, 11, 24));

            Assert.Contains(store.ListPayPeriods(), p => p.Start == new DateTime(2026, 11, 10));
            Assert.True(store.GetTimeCard(employee.Id, stale.Id)!.Approved);
        }
        finally { Clean(path); }
    }

    [Fact]
    public void Alignment_matches_weekday_and_month_rules()
    {
        var settings = new AppSettings
        {
            PeriodType = PayPeriodType.Weekly,
            PeriodAnchor = new DateTime(2026, 11, 11),
        };

        Assert.True(PayPeriodGenerator.IsAligned(new DateTime(2026, 11, 18), settings));
        Assert.True(PayPeriodGenerator.IsAligned(new DateTime(2026, 11, 4), settings));
        Assert.False(PayPeriodGenerator.IsAligned(new DateTime(2026, 11, 17), settings)); // Tuesday

        settings.PeriodType = PayPeriodType.Monthly;
        Assert.True(PayPeriodGenerator.IsAligned(new DateTime(2026, 12, 1), settings));
        Assert.False(PayPeriodGenerator.IsAligned(new DateTime(2026, 11, 18), settings));
    }
}

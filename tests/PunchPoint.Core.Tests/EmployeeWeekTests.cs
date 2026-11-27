using PunchPoint.Core.Models;
using PunchPoint.Core.Storage;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.Core.Tests;

public sealed class EmployeeWeekTests : IDisposable
{
    private readonly string _dbPath = TestPaths.NewDatabasePath();
    private readonly Database _database;
    private readonly Store _store;
    private readonly Employee _employee;

    public EmployeeWeekTests()
    {
        _database = Database.Open(_dbPath);
        _store = new Store(_database);
        Maintenance.EnsurePeriods(_store);

        _employee = new Employee { FirstName = "Maria", LastName = "Gonzalez", Pin = "1001" };
        _store.InsertEmployee(_employee);
    }

    public void Dispose()
    {
        _database.Dispose();
        foreach (string suffix in new[] { "", "-wal", "-shm" })
        {
            try { File.Delete(_dbPath + suffix); } catch { }
        }
    }

    private PayPeriod Period()
    {
        var period = _store.GetCurrentPayPeriod();
        Assert.NotNull(period);
        return period!;
    }

    private void Add(DateTime time, PunchKind kind)
    {
        _store.InsertPunch(new Punch
        {
            EmployeeId = _employee.Id,
            Timestamp = time,
            Kind = kind,
            Source = PunchSource.Manual,
            PayPeriodId = _store.GetPayPeriodFor(time)?.Id,
        });
    }

    [Fact]
    public void A_days_first_in_and_last_out_are_reported_with_the_total()
    {
        var period = Period();
        var day = period.Start.Date.AddDays(1);

        Add(day.AddHours(8), PunchKind.In);
        Add(day.AddHours(12), PunchKind.Out);
        Add(day.AddHours(13), PunchKind.In);
        Add(day.AddHours(17), PunchKind.Out);

        var week = EmployeeWeek.Build(_store, _employee, _store.GetSettings());

        var row = Assert.Single(week.Days);
        Assert.Equal(day, row.Date);
        Assert.Equal(day.AddHours(8), row.FirstIn);
        Assert.Equal(day.AddHours(17), row.LastOut);
        Assert.Equal(8 * 60, row.WorkedMinutes);
        Assert.False(row.IsOpen);
        Assert.Equal(week.TotalMinutes, row.TotalMinutes);
    }

    [Fact]
    public void An_open_session_shows_the_first_in_and_no_out()
    {
        var period = Period();
        var day = period.Start.Date;

        Add(day.AddHours(8), PunchKind.In);

        var week = EmployeeWeek.Build(_store, _employee, _store.GetSettings());

        var row = Assert.Single(week.Days);
        Assert.Equal(day.AddHours(8), row.FirstIn);
        Assert.Null(row.LastOut);
        Assert.True(row.IsOpen);
    }

    [Fact]
    public void Breaks_are_reported_separately()
    {
        var period = Period();
        var day = period.Start.Date;

        Add(day.AddHours(8), PunchKind.In);
        Add(day.AddHours(12), PunchKind.BreakOut);
        Add(day.AddHours(12).AddMinutes(30), PunchKind.BreakIn);
        Add(day.AddHours(17), PunchKind.Out);

        var week = EmployeeWeek.Build(_store, _employee, _store.GetSettings());

        var row = Assert.Single(week.Days);
        Assert.Equal(30, row.BreakMinutes);
        Assert.Equal(8 * 60 + 30, row.WorkedMinutes);
        Assert.Equal(30, week.BreakMinutes);
    }

    [Fact]
    public void Days_without_punches_are_left_out()
    {
        var period = Period();
        Add(period.Start.Date.AddHours(8), PunchKind.In);
        Add(period.Start.Date.AddHours(16), PunchKind.Out);

        var week = EmployeeWeek.Build(_store, _employee, _store.GetSettings());

        Assert.Single(week.Days);
        Assert.False(week.IsEmpty);
    }

    [Fact]
    public void A_period_with_nothing_in_it_is_empty()
    {
        var week = EmployeeWeek.Build(_store, _employee, _store.GetSettings());

        Assert.True(week.IsEmpty);
        Assert.Equal(0, week.TotalMinutes);
    }
}

using PunchPoint.Core.Models;
using PunchPoint.Core.Storage;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.Core.Tests;

/// <summary>
/// A day must alternate IN, OUT, IN, OUT. Someone who forgets to clock in and then presses the
/// clock at the end of the day leaves an IN where an OUT was meant; adding the missing punch has
/// to straighten out the rest of the day.
/// </summary>
public sealed class PunchSequenceTests : IDisposable
{
    private readonly string _dbPath = TestPaths.NewDatabasePath();
    private readonly Database _database;
    private readonly Store _store;
    private readonly Employee _employee;

    public PunchSequenceTests()
    {
        _database = Database.Open(_dbPath);
        _store = new Store(_database);
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

    private Punch Add(DateTime time, PunchKind kind)
    {
        var punch = new Punch
        {
            EmployeeId = _employee.Id,
            Timestamp = time,
            Kind = kind,
            Source = PunchSource.Manual,
            PayPeriodId = _store.GetPayPeriodFor(time)?.Id,
        };
        _store.InsertPunch(punch);
        return punch;
    }

    private List<PunchKind> KindsOn(DateTime day) => _store
        .ListPunches(_employee.Id, day.Date, day.Date.AddDays(1))
        .OrderBy(p => p.Timestamp)
        .Select(p => p.Kind)
        .ToList();

    private List<PunchKindChange> FixDay(DateTime day) =>
        PunchSequence.Normalize(_store, _employee.Id, day.Date, day.Date.AddDays(1));

    [Fact]
    public void A_second_in_becomes_an_out()
    {
        var day = new DateTime(2026, 11, 24);
        Add(day.AddHours(8), PunchKind.In);
        Add(day.AddHours(17), PunchKind.In); // pressed the clock without ever clocking in

        var changes = FixDay(day);

        Assert.Equal(new[] { PunchKind.In, PunchKind.Out }, KindsOn(day));
        Assert.Single(changes);
        Assert.Equal(PunchKind.In, changes[0].From);
        Assert.Equal(PunchKind.Out, changes[0].To);
    }

    [Fact]
    public void Two_ins_and_two_outs_are_reduced_to_the_alternation()
    {
        var day = new DateTime(2026, 11, 24);
        Add(day.AddHours(8), PunchKind.In);
        Add(day.AddHours(12), PunchKind.In);
        Add(day.AddHours(13), PunchKind.Out);
        Add(day.AddHours(17), PunchKind.Out);

        FixDay(day);

        Assert.Equal(new[] { PunchKind.In, PunchKind.Out, PunchKind.In, PunchKind.Out }, KindsOn(day));
    }

    [Fact]
    public void A_lone_out_is_treated_as_the_start_of_the_day()
    {
        var day = new DateTime(2026, 11, 24);
        Add(day.AddHours(17), PunchKind.Out);

        FixDay(day);

        Assert.Equal(new[] { PunchKind.In }, KindsOn(day));
    }

    [Fact]
    public void Break_punches_and_paid_hours_are_left_alone()
    {
        var day = new DateTime(2026, 11, 24);
        Add(day.AddHours(8), PunchKind.In);
        Add(day.AddHours(12), PunchKind.BreakOut);
        Add(day.AddHours(12).AddMinutes(30), PunchKind.BreakIn);
        Add(day.AddHours(13), PunchKind.Hours);
        Add(day.AddHours(17), PunchKind.Out);

        var changes = FixDay(day);

        Assert.Empty(changes);
        Assert.Equal(new[]
        {
            PunchKind.In, PunchKind.BreakOut, PunchKind.BreakIn, PunchKind.Hours, PunchKind.Out,
        }, KindsOn(day));
    }

    [Fact]
    public void An_overnight_shift_keeps_its_out_after_midnight()
    {
        var first = new DateTime(2026, 11, 23);
        var second = new DateTime(2026, 11, 24);

        Add(first.AddHours(22), PunchKind.In);
        Add(second.AddHours(6), PunchKind.Out);

        // Only the second day is normalized: it must not turn the 6 AM OUT into an IN.
        var changes = FixDay(second);

        Assert.Empty(changes);
        Assert.Equal(new[] { PunchKind.Out }, KindsOn(second));
    }

    [Fact]
    public void Adding_the_missing_in_straightens_the_day()
    {
        var day = new DateTime(2026, 11, 24);

        // The employee forgot to clock in and pressed the clock at 5 PM: an IN where an OUT belongs.
        Add(day.AddHours(17), PunchKind.In);

        // The supervisor adds the missing 8 AM punch.
        Add(day.AddHours(8), PunchKind.In);

        FixDay(day);

        Assert.Equal(new[] { PunchKind.In, PunchKind.Out }, KindsOn(day));
    }

    [Fact]
    public void A_day_already_in_sequence_is_untouched()
    {
        var day = new DateTime(2026, 11, 24);
        Add(day.AddHours(8), PunchKind.In);
        Add(day.AddHours(12), PunchKind.Out);
        Add(day.AddHours(13), PunchKind.In);
        Add(day.AddHours(17), PunchKind.Out);

        Assert.Empty(FixDay(day));
    }

    [Fact]
    public void The_work_day_window_follows_the_day_change_offset()
    {
        // A day-change offset of 4 hours puts a 2 AM punch on the previous work date.
        var moment = new DateTime(2026, 11, 24, 2, 0, 0);
        var (from, to) = PunchSequence.WorkDay(moment, 4 * 60);

        Assert.Equal(new DateTime(2026, 11, 23, 4, 0, 0), from);
        Assert.Equal(new DateTime(2026, 11, 24, 4, 0, 0), to);
    }
}

using PunchPoint.Core.Models;
using PunchPoint.Core.Services;
using PunchPoint.Core.Storage;

namespace PunchPoint.Core.Tests;

public class ClockServiceTests
{
    private static (Database Db, Store Store, ClockService Clock, Employee Employee) Setup(string path)
    {
        var db = Database.Open(path);
        var store = new Store(db);
        var employee = new Employee { FirstName = "Clock", LastName = "Tester", Pin = "1234" };
        store.InsertEmployee(employee);
        return (db, store, new ClockService(store), employee);
    }

    private static void Cleanup(string path)
    {
        foreach (string suffix in new[] { "", "-wal", "-shm" })
        {
            try { File.Delete(path + suffix); } catch { }
        }
    }

    [Fact]
    public void State_cycles_in_out()
    {
        string path = TestPaths.NewDatabasePath();
        try
        {
            var (db, store, clock, employee) = Setup(path);
            using (db)
            {
                var day = new DateTime(2026, 11, 9);
                clock.Punch(employee, PunchKind.In, day.AddHours(8));

                var status = clock.GetStatus(employee, day.AddHours(9));
                Assert.True(status.IsClockedIn);
                Assert.False(status.OnBreak);
                Assert.Equal(PunchKind.Out, status.NextKind);
                Assert.Equal("Clock Out", status.ActionLabel);
                Assert.Equal(day.AddHours(8), status.Since);

                clock.Punch(employee, PunchKind.Out, day.AddHours(17));

                status = clock.GetStatus(employee, day.AddHours(18));
                Assert.False(status.IsClockedIn);
                Assert.Equal(PunchKind.In, status.NextKind);
                Assert.Equal(540, status.MinutesToday);
            }
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void Break_does_not_end_the_shift()
    {
        string path = TestPaths.NewDatabasePath();
        try
        {
            var (db, store, clock, employee) = Setup(path);
            using (db)
            {
                var day = new DateTime(2026, 11, 9);
                clock.Punch(employee, PunchKind.In, day.AddHours(8));

                clock.Punch(employee, PunchKind.BreakOut, day.AddHours(12));
                var status = clock.GetStatus(employee, day.AddHours(12).AddMinutes(1));
                Assert.True(status.IsClockedIn);
                Assert.True(status.OnBreak);
                Assert.Equal(PunchKind.BreakIn, status.NextKind);

                clock.Punch(employee, PunchKind.BreakIn, day.AddHours(12).AddMinutes(30));
                status = clock.GetStatus(employee, day.AddHours(12).AddMinutes(31));

                // After a break the employee is still on the clock and the next action is OUT.
                Assert.True(status.IsClockedIn);
                Assert.False(status.OnBreak);
                Assert.Equal(PunchKind.Out, status.NextKind);
                Assert.Equal("Clock Out", status.ActionLabel);
                Assert.Equal(day.AddHours(8), status.Since);
            }
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void Break_time_is_not_counted_as_worked()
    {
        string path = TestPaths.NewDatabasePath();
        try
        {
            var (db, store, clock, employee) = Setup(path);
            using (db)
            {
                var day = new DateTime(2026, 11, 9);
                clock.Punch(employee, PunchKind.In, day.AddHours(8));
                clock.Punch(employee, PunchKind.BreakOut, day.AddHours(12));
                clock.Punch(employee, PunchKind.BreakIn, day.AddHours(12).AddMinutes(30));
                clock.Punch(employee, PunchKind.Out, day.AddHours(17));

                var status = clock.GetStatus(employee, day.AddHours(18));
                Assert.False(status.IsClockedIn);
                Assert.Equal(510, status.MinutesToday); // 9h minus 30m
            }
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void Toggle_uses_the_correct_action_after_a_break()
    {
        string path = TestPaths.NewDatabasePath();
        try
        {
            var (db, store, clock, employee) = Setup(path);
            using (db)
            {
                var day = new DateTime(2026, 11, 9);
                clock.Punch(employee, PunchKind.In, day.AddHours(8));
                clock.Punch(employee, PunchKind.BreakOut, day.AddHours(12));
                clock.Punch(employee, PunchKind.BreakIn, day.AddHours(12).AddMinutes(30));

                var punch = clock.Toggle(employee, day.AddHours(17));
                Assert.Equal(PunchKind.Out, punch.Kind);
            }
        }
        finally
        {
            Cleanup(path);
        }
    }
}

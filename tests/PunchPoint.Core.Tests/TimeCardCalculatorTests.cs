using PunchPoint.Core.Models;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.Core.Tests;

public class TimeCardCalculatorTests
{
    private static readonly AppSettings BaseSettings = new()
    {
        RoundingMinutes = 0,
        DailyOverTime1Minutes = 0,
        WeeklyOverTime1Minutes = 0,
        DayChangeOffsetMinutes = 0,
    };

    private static readonly PayPeriod Week = new()
    {
        Id = 1,
        Start = new DateTime(2026, 11, 9),
        End = new DateTime(2026, 11, 15),
        Type = PayPeriodType.Weekly,
    };

    private static Employee Alice() => new()
    {
        Id = 1,
        FirstName = "Alice",
        LastName = "Nguyen",
        Pin = "1001",
    };

    private static Punch P(DateTime when, PunchKind kind = PunchKind.In, int employeeId = 1) => new()
    {
        EmployeeId = employeeId,
        Timestamp = when,
        Kind = kind,
    };

    private static TimeCardResult Calc(IEnumerable<Punch> punches, AppSettings? settings = null,
        Shift? shift = null, IEnumerable<Holiday>? holidays = null, TimeCard? card = null)
    {
        return TimeCardCalculator.Calculate(Alice(), shift, Week, punches.ToList(),
            settings ?? BaseSettings, holidays?.ToList(), card);
    }

    [Fact]
    public void Simple_day_is_paid_from_in_to_out()
    {
        var result = Calc(new[]
        {
            P(new DateTime(2026, 11, 9, 8, 0, 0), PunchKind.In),
            P(new DateTime(2026, 11, 9, 17, 0, 0), PunchKind.Out),
        });

        Assert.Equal(540, result.RegularMinutes);
        Assert.Equal(9.0m, result.TotalMinutes / 60m);
        Assert.Empty(result.Issues);
        Assert.Single(result.Days);
    }

    [Fact]
    public void Lunch_break_punches_are_unpaid()
    {
        var result = Calc(new[]
        {
            P(new DateTime(2026, 11, 9, 8, 0, 0), PunchKind.In),
            P(new DateTime(2026, 11, 9, 12, 0, 0), PunchKind.BreakOut),
            P(new DateTime(2026, 11, 9, 12, 30, 0), PunchKind.BreakIn),
            P(new DateTime(2026, 11, 9, 17, 0, 0), PunchKind.Out),
        });

        Assert.Equal(510, result.RegularMinutes); // 9h - 30m
        Assert.Equal(30, result.BreakMinutes);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Two_sessions_in_a_day_are_both_paid()
    {
        var result = Calc(new[]
        {
            P(new DateTime(2026, 11, 9, 8, 0, 0), PunchKind.In),
            P(new DateTime(2026, 11, 9, 12, 0, 0), PunchKind.Out),
            P(new DateTime(2026, 11, 9, 12, 30, 0), PunchKind.In),
            P(new DateTime(2026, 11, 9, 17, 0, 0), PunchKind.Out),
        });

        Assert.Equal(510, result.RegularMinutes);
        Assert.Equal(2, result.Days[0].Segments.Count);
    }

    [Fact]
    public void Missing_out_punch_is_reported_and_not_paid_as_a_full_day()
    {
        var result = Calc(new[]
        {
            P(new DateTime(2026, 11, 9, 8, 0, 0), PunchKind.In),
        });

        Assert.Equal(0, result.RegularMinutes);
        Assert.Contains(result.Issues, i => i.Contains("Missing OUT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Orphan_out_punch_is_ignored()
    {
        var result = Calc(new[]
        {
            P(new DateTime(2026, 11, 9, 17, 0, 0), PunchKind.Out),
            P(new DateTime(2026, 11, 10, 8, 0, 0), PunchKind.In),
            P(new DateTime(2026, 11, 10, 12, 0, 0), PunchKind.Out),
        });

        Assert.Contains(result.Issues, i => i.Contains("no matching IN", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(240, result.RegularMinutes);
    }

    [Fact]
    public void Daily_overtime_splits_at_the_threshold()
    {
        var settings = new AppSettings { DailyOverTime1Minutes = 8 * 60 };

        var result = Calc(new[]
        {
            P(new DateTime(2026, 11, 9, 7, 0, 0), PunchKind.In),
            P(new DateTime(2026, 11, 9, 18, 0, 0), PunchKind.Out),
        }, settings);

        Assert.Equal(480, result.RegularMinutes);
        Assert.Equal(180, result.OverTime1Minutes);
    }

    [Fact]
    public void Weekly_overtime_applies_across_the_week()
    {
        var settings = new AppSettings { WeeklyOverTime1Minutes = 40 * 60 };
        var punches = new List<Punch>();

        for (int day = 0; day < 5; day++)
        {
            var date = Week.Start.AddDays(day);
            punches.Add(P(date.AddHours(8), PunchKind.In));
            punches.Add(P(date.AddHours(18), PunchKind.Out)); // 10h per day = 50h
        }

        var result = Calc(punches, settings);

        Assert.Equal(40 * 60, result.RegularMinutes);
        Assert.Equal(10 * 60, result.OverTime1Minutes);
    }

    [Fact]
    public void Daily_and_weekly_overtime_do_not_double_count()
    {
        var settings = new AppSettings
        {
            DailyOverTime1Minutes = 8 * 60,
            WeeklyOverTime1Minutes = 40 * 60,
        };

        var punches = new List<Punch>();
        for (int day = 0; day < 5; day++)
        {
            var date = Week.Start.AddDays(day);
            punches.Add(P(date.AddHours(8), PunchKind.In));
            punches.Add(P(date.AddHours(18), PunchKind.Out));
        }

        var result = Calc(punches, settings);

        Assert.Equal(40 * 60, result.RegularMinutes);
        Assert.Equal(10 * 60, result.OverTime1Minutes);
    }

    [Fact]
    public void Rounding_to_nearest_quarter_hour()
    {
        var settings = new AppSettings { RoundingMinutes = 15, RoundInMode = RoundingMode.Nearest, RoundOutMode = RoundingMode.Nearest };

        var result = Calc(new[]
        {
            P(new DateTime(2026, 11, 9, 8, 7, 0), PunchKind.In),
            P(new DateTime(2026, 11, 9, 17, 8, 0), PunchKind.Out),
        }, settings);

        // 8:07 -> 8:00, 17:08 -> 17:15 = 555 minutes
        Assert.Equal(555, result.RegularMinutes);
    }

    [Fact]
    public void Rounding_can_favour_the_employee()
    {
        var settings = new AppSettings { RoundingMinutes = 6, RoundInMode = RoundingMode.Down, RoundOutMode = RoundingMode.Up };

        var result = Calc(new[]
        {
            P(new DateTime(2026, 11, 9, 8, 5, 0), PunchKind.In),
            P(new DateTime(2026, 11, 9, 17, 1, 0), PunchKind.Out),
        }, settings);

        // 8:05 -> 8:00, 17:01 -> 17:06 = 546 minutes
        Assert.Equal(546, result.RegularMinutes);
    }

    [Fact]
    public void Grace_period_does_not_dock_a_slightly_late_arrival()
    {
        var shift = new Shift { Start = new TimeSpan(8, 0, 0), Stop = new TimeSpan(17, 0, 0), StartGraceMinutes = 10 };

        var result = Calc(new[]
        {
            P(new DateTime(2026, 11, 9, 8, 6, 0), PunchKind.In),
            P(new DateTime(2026, 11, 9, 17, 0, 0), PunchKind.Out),
        }, BaseSettings, shift);

        Assert.Equal(540, result.RegularMinutes);
    }

    [Fact]
    public void Docking_pulls_a_late_arrival_back_to_shift_start()
    {
        var shift = new Shift
        {
            Start = new TimeSpan(8, 0, 0),
            Stop = new TimeSpan(17, 0, 0),
            StartGraceMinutes = 5,
            DockLateArrival = true,
        };

        var result = Calc(new[]
        {
            P(new DateTime(2026, 11, 9, 8, 20, 0), PunchKind.In),
            P(new DateTime(2026, 11, 9, 17, 0, 0), PunchKind.Out),
        }, BaseSettings, shift);

        Assert.Equal(540, result.RegularMinutes);
    }

    [Fact]
    public void Day_change_offset_groups_after_midnight_punches_with_the_previous_day()
    {
        var settings = new AppSettings { DayChangeOffsetMinutes = 4 * 60 };

        var result = Calc(new[]
        {
            P(new DateTime(2026, 11, 9, 22, 0, 0), PunchKind.In),
            P(new DateTime(2026, 11, 10, 2, 0, 0), PunchKind.Out),
        }, settings);

        Assert.Single(result.Days);
        Assert.Equal(new DateTime(2026, 11, 9), result.Days[0].WorkDate);
        Assert.Equal(240, result.RegularMinutes);
    }

    [Fact]
    public void Overnight_shift_punch_pair_is_paid_across_midnight()
    {
        var shift = new Shift { Start = new TimeSpan(22, 0, 0), Stop = new TimeSpan(6, 0, 0) };

        var result = Calc(new[]
        {
            P(new DateTime(2026, 11, 9, 22, 0, 0), PunchKind.In),
            P(new DateTime(2026, 11, 10, 6, 0, 0), PunchKind.Out),
        }, BaseSettings, shift);

        Assert.Equal(480, result.RegularMinutes);
        Assert.True(shift.CrossesMidnight);
    }

    [Fact]
    public void Paid_holiday_adds_configured_hours()
    {
        var shift = new Shift { HolidaysPaid = true, HolidayHours = 8 };
        var holidays = new[] { new Holiday { Name = "Thanksgiving", Date = new DateTime(2026, 11, 12), Paid = true } };

        var result = Calc(Array.Empty<Punch>(), BaseSettings, shift, holidays);

        Assert.Equal(480, result.HolidayMinutes);
        Assert.Single(result.Days);
        Assert.True(result.Days[0].IsHoliday);
    }

    [Fact]
    public void Miscellaneous_hours_are_added_to_regular_time()
    {
        var punch = P(new DateTime(2026, 11, 10, 8, 0, 0), PunchKind.Hours);
        punch.Hours = TimeSpan.FromHours(8);
        punch.HoursCategory = "Vacation";

        var result = Calc(new[] { punch });

        Assert.Equal(480, result.RegularMinutes);
    }

    [Fact]
    public void Time_card_adjustment_minutes_are_included_in_the_total()
    {
        var card = new TimeCard { AdjustRegularMinutes = 30 };

        var result = Calc(new[]
        {
            P(new DateTime(2026, 11, 9, 8, 0, 0), PunchKind.In),
            P(new DateTime(2026, 11, 9, 16, 0, 0), PunchKind.Out),
        }, card: card);

        Assert.Equal(480, result.RegularMinutes);
        Assert.Equal(510, result.TotalMinutes);
    }
}

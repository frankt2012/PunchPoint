using PunchPoint.Core.Models;

namespace PunchPoint.Core.TimeCalc;

/// <summary>
/// Turns raw punches into paid time for a pay period.
///
/// Rules, in the order they are applied:
///   1. Punches are grouped into "work days" using the day-change offset.
///   2. IN/OUT (and optional break) punches are paired in time order.
///   3. Shift grace/docking adjusts the first IN and the last OUT of a day.
///   4. Rounding applies either to each punch time or to the day total.
///   5. Daily overtime thresholds split the day, then weekly thresholds split the week.
///   6. Paid holidays and miscellaneous hours are added.
///
/// Every adjustment is recorded on the segment so the UI can explain any total.
/// </summary>
public static class TimeCardCalculator
{
    public static TimeCardResult Calculate(
        Employee employee,
        Shift? shift,
        PayPeriod period,
        IReadOnlyList<Punch> punches,
        AppSettings settings,
        IReadOnlyList<Holiday>? holidays = null,
        TimeCard? timeCard = null)
    {
        var result = new TimeCardResult
        {
            Employee = employee,
            PayPeriod = period,
            Shift = shift,
        };

        holidays ??= Array.Empty<Holiday>();

        // Pair every punch first, across day boundaries, so overnight shifts work no
        // matter where midnight falls. Segments are then filed under the work date of
        // their IN punch.
        var filtered = punches.Where(p => !p.IsVoided).ToList();
        var segmentBuilds = PairSegments(filtered, settings);

        var byDay = new SortedDictionary<DateTime, List<SegmentBuild>>();
        void FileUnder(DateTime workDate, SegmentBuild build)
        {
            if (!byDay.TryGetValue(workDate, out var list))
                byDay[workDate] = list = new List<SegmentBuild>();
            list.Add(build);
        }

        foreach (var build in segmentBuilds)
            FileUnder(build.WorkDate, build);

        // Miscellaneous hours (vacation, sick, other) file under their own work date.
        foreach (var extra in filtered.Where(p => p.Kind == PunchKind.Hours))
            FileUnder(WorkDate(extra.Timestamp, settings.DayChangeOffsetMinutes),
                new SegmentBuild(WorkDate(extra.Timestamp, settings.DayChangeOffsetMinutes)));

        foreach (var holiday in holidays.Where(h => h.Date.Date >= period.Start.Date && h.Date.Date <= period.End.Date))
            byDay.TryAdd(holiday.Date.Date, new List<SegmentBuild>());

        var weeks = new Dictionary<int, WeekBucket>();
        int consecutiveWorkedDays = 0;

        for (DateTime day = period.Start.Date; day <= period.End.Date; day = day.AddDays(1))
        {
            if (!byDay.TryGetValue(day, out var builds))
                continue;

            var holiday = holidays.FirstOrDefault(h => h.Date.Date == day.Date && h.Paid);
            var totals = new DayTotals { WorkDate = day };

            foreach (var build in builds)
            {
                if (build.Segment is not null)
                    totals.Segments.Add(build.Segment);
                totals.Issues.AddRange(build.Issues);
            }

            var built = FinishDay(totals, filtered, shift, settings);

            if (holiday is not null)
            {
                totals.IsHoliday = true;
                totals.HolidayName = holiday.Name;
                if (ShouldPayHoliday(employee, shift, holiday))
                {
                    int hours = shift is { HolidayHours: > 0 } ? shift.HolidayHours : 8;
                    totals.HolidayMinutes = hours * 60;
                }
            }

            int weekIndex = (int)((day - period.Start.Date).TotalDays / 7);
            if (!weeks.TryGetValue(weekIndex, out var bucket))
                weeks[weekIndex] = bucket = new WeekBucket();

            if (built.PaidMinutes > 0)
                consecutiveWorkedDays++;
            else
                consecutiveWorkedDays = 0;

            bool seventhDay = settings.EnableSeventhDayOvertime && consecutiveWorkedDays >= 7 && built.PaidMinutes > 0;

            SplitOvertime(totals, built.PaidMinutes, built.MiscMinutes, settings, bucket, seventhDay);

            result.Days.Add(totals);

            foreach (var issue in totals.Issues)
                result.Issues.Add($"{day:d}: {issue}");
        }

        result.RegularMinutes = result.Days.Sum(d => d.RegularMinutes);
        result.OverTime1Minutes = result.Days.Sum(d => d.OverTime1Minutes);
        result.OverTime2Minutes = result.Days.Sum(d => d.OverTime2Minutes);
        result.HolidayMinutes = result.Days.Sum(d => d.HolidayMinutes);
        result.BreakMinutes = result.Days.Sum(d => d.BreakMinutes);

        if (timeCard is not null)
        {
            result.AdjustmentMinutes = timeCard.AdjustRegularMinutes
                                       + timeCard.AdjustOverTime1Minutes
                                       + timeCard.AdjustOverTime2Minutes;
        }

        return result;
    }

    /// <summary>Work date for a punch, honouring hours after midnight that belong to the previous day.</summary>
    public static DateTime WorkDate(DateTime timestamp, int dayChangeOffsetMinutes)
        => timestamp.AddMinutes(-dayChangeOffsetMinutes).Date;

    private static bool ShouldPayHoliday(Employee employee, Shift? shift, Holiday holiday)
    {
        // Salaried employees always receive configured holidays; hourly employees only
        // when their shift includes holiday pay. A hire date after the holiday disqualifies.
        if (employee.HireDate is not null && employee.HireDate.Value.Date > holiday.Date.Date)
            return false;

        return shift?.HolidaysPaid ?? employee.PayType == PayType.Salary;
    }

    // =================================================================
    // Day building
    // =================================================================

    private sealed record SegmentBuild(DateTime WorkDate)
    {
        public WorkSegment? Segment { get; init; }
        public List<string> Issues { get; } = new();

        public SegmentBuild(DateTime workDate, WorkSegment? segment, IEnumerable<string>? issues = null) : this(workDate)
        {
            Segment = segment;
            if (issues is not null) Issues.AddRange(issues);
        }
    }

    private readonly record struct BuiltDay(int PaidMinutes, int MiscMinutes);

    /// <summary>
    /// Pairs punches globally in time order. A punch sequence for one employee can span
    /// midnight, so pairing is not limited to a single day.
    /// </summary>
    private static List<SegmentBuild> PairSegments(List<Punch> punches, AppSettings settings)
    {
        var builds = new List<SegmentBuild>();
        var byEmployee = punches.Where(p => p.IsPunch).GroupBy(p => p.EmployeeId);

        foreach (var employeeGroup in byEmployee)
        {
            var ordered = employeeGroup.OrderBy(p => p.Timestamp).ToList();

            WorkSegment? open = null;
            DateTime? breakStart = null;

            void CloseOpen(string reason)
            {
                if (open is null) return;
                builds.Add(new SegmentBuild(
                    WorkDate(open.In, settings.DayChangeOffsetMinutes), open, new[] { reason }));
                open = null;
                breakStart = null;
            }

            foreach (var punch in ordered)
            {
                DateTime workDate = WorkDate(punch.Timestamp, settings.DayChangeOffsetMinutes);

                switch (punch.Kind)
                {
                    case PunchKind.In:
                        if (open is not null)
                            CloseOpen($"Missing OUT after the IN at {open.In:h:mm tt}. That block was not paid.");

                        open = new WorkSegment { In = punch.Timestamp, PaidIn = punch.Timestamp };
                        breakStart = null;
                        break;

                    case PunchKind.Out:
                        if (open is null)
                        {
                            builds.Add(new SegmentBuild(workDate, null,
                                new[] { $"OUT punch at {punch.Timestamp:h:mm tt} has no matching IN and was ignored." }));
                            break;
                        }

                        open.Out = punch.Timestamp;
                        open.PaidOut = punch.Timestamp;

                        if (breakStart is not null)
                        {
                            open.Adjustments.Add($"Break at {breakStart:h:mm tt} was never ended.");
                            breakStart = null;
                        }

                        builds.Add(new SegmentBuild(
                            WorkDate(open.In, settings.DayChangeOffsetMinutes), open));
                        open = null;
                        break;

                    case PunchKind.BreakOut:
                        if (open is null)
                        {
                            builds.Add(new SegmentBuild(workDate, null,
                                new[] { $"Break OUT at {punch.Timestamp:h:mm tt} has no open IN and was ignored." }));
                            break;
                        }
                        breakStart = punch.Timestamp;
                        break;

                    case PunchKind.BreakIn:
                        if (open is null || breakStart is null)
                        {
                            builds.Add(new SegmentBuild(workDate, null,
                                new[] { $"Break IN at {punch.Timestamp:h:mm tt} without a matching break OUT was ignored." }));
                            break;
                        }

                        int breakMinutes = (int)Math.Round((punch.Timestamp - breakStart.Value).TotalMinutes);
                        if (breakMinutes > 0)
                        {
                            open.BreakMinutes += breakMinutes;
                            open.Adjustments.Add($"Break {breakStart:h:mm tt}–{punch.Timestamp:h:mm tt}: {breakMinutes} min unpaid");
                        }
                        breakStart = null;
                        break;
                }
            }

            if (open is not null)
                CloseOpen($"Missing OUT for the IN at {open.In:h:mm tt}. That time is not paid until corrected.");
        }

        builds.Sort((a, b) =>
        {
            var aTime = a.Segment?.In ?? a.WorkDate;
            var bTime = b.Segment?.In ?? b.WorkDate;
            return aTime.CompareTo(bTime);
        });

        return builds;
    }

    /// <summary>
    /// Applies automatic lunch, shift rules and rounding to one work day and returns
    /// the paid minutes.
    /// </summary>
    private static BuiltDay FinishDay(DayTotals day, List<Punch> allPunches, Shift? shift, AppSettings settings)
    {
        int miscMinutes = 0;
        foreach (var punch in allPunches.Where(p => p.Kind == PunchKind.Hours))
        {
            if (WorkDate(punch.Timestamp, settings.DayChangeOffsetMinutes) != day.WorkDate) continue;
            if (punch.Hours > TimeSpan.Zero)
                miscMinutes += (int)Math.Round(punch.Hours.TotalMinutes);
        }

        day.BreakMinutes = day.Segments.Sum(s => s.BreakMinutes);

        // Automatic unpaid lunch when no break was punched.
        if (shift is { LunchEnabled: true, LunchAutoAfterMinutes: > 0, LunchDurationMinutes: > 0 })
        {
            foreach (var segment in day.Segments.Where(s => s.Out is not null && s.BreakMinutes == 0))
            {
                if (segment.RawDuration.TotalMinutes >= shift.LunchAutoAfterMinutes)
                {
                    segment.BreakMinutes += shift.LunchDurationMinutes;
                    day.BreakMinutes += shift.LunchDurationMinutes;
                    segment.Adjustments.Add($"Automatic unpaid lunch ({shift.LunchDurationMinutes} min)");
                }
            }
        }

        if (shift is not null)
            ApplyShiftRules(day, shift);

        int paid;
        if (settings.RoundingMinutes > 0)
        {
            if (settings.RoundDailyTotalInsteadOfPunches)
            {
                int raw = day.Segments.Sum(s => s.PaidMinutes);
                paid = RoundMinutes(raw, settings.RoundingMinutes, RoundingMode.Nearest);
                if (paid != raw && day.Segments.Count > 0)
                    day.Segments[0].Adjustments.Add(
                        $"Day total rounded from {FormatMinutes(raw)} to {FormatMinutes(paid)} ({settings.RoundingMinutes}-minute rule)");
            }
            else
            {
                foreach (var segment in day.Segments)
                {
                    var newIn = RoundTime(segment.PaidIn, settings.RoundingMinutes, settings.RoundInMode);
                    var newOut = segment.PaidOut is null
                        ? (DateTime?)null
                        : RoundTime(segment.PaidOut.Value, settings.RoundingMinutes, settings.RoundOutMode);

                    if (newIn != segment.PaidIn || newOut != segment.PaidOut)
                    {
                        segment.Adjustments.Add(
                            $"Rounded to {settings.RoundingMinutes}-minute interval (" +
                            $"{segment.PaidIn:h:mm tt} → {newIn:h:mm tt}" +
                            (segment.PaidOut is null ? ")" : $", {segment.PaidOut:h:mm tt} → {newOut:h:mm tt})"));
                        segment.PaidIn = newIn;
                        segment.PaidOut = newOut;
                    }
                }
                paid = day.Segments.Sum(s => s.PaidMinutes);
            }
        }
        else
        {
            paid = day.Segments.Sum(s => s.PaidMinutes);
        }

        return new BuiltDay(paid, miscMinutes);
    }

    private static string FormatMinutes(int minutes) =>
        $"{(minutes < 0 ? "-" : "")}{Math.Abs(minutes) / 60}:{Math.Abs(minutes) % 60:00}";

    private static void ApplyShiftRules(DayTotals day, Shift shift)
    {
        if (day.Segments.Count == 0) return;

        var shiftStart = day.WorkDate.Date.Add(shift.Start);
        var shiftStop = day.WorkDate.Date.Add(shift.Stop);
        if (shift.CrossesMidnight) shiftStop = shiftStop.AddDays(1);

        // First IN of the day.
        var first = day.Segments[0];
        int lateMinutes = (int)Math.Round((first.PaidIn - shiftStart).TotalMinutes);

        if (Math.Abs(lateMinutes) <= 8 * 60)
        {
            if (lateMinutes > 0 && lateMinutes <= shift.StartGraceMinutes)
            {
                first.PaidIn = shiftStart;
                first.Adjustments.Add($"Arrived {lateMinutes} min late but inside the {shift.StartGraceMinutes}-minute grace period.");
            }
            else if (lateMinutes > shift.StartGraceMinutes && shift.DockLateArrival)
            {
                first.PaidIn = shiftStart;
                first.Adjustments.Add($"Docked to shift start {shiftStart:h:mm tt} (arrived {lateMinutes} min late).");
            }
        }

        // Last OUT of the day.
        var last = day.Segments.LastOrDefault(s => s.PaidOut is not null);
        if (last?.PaidOut is null) return;

        int earlyMinutes = (int)Math.Round((shiftStop - last.PaidOut.Value).TotalMinutes);

        if (Math.Abs(earlyMinutes) <= 8 * 60)
        {
            if (earlyMinutes > 0 && earlyMinutes <= shift.StopGraceMinutes)
            {
                last.PaidOut = shiftStop;
                last.Adjustments.Add($"Left {earlyMinutes} min early but inside the {shift.StopGraceMinutes}-minute grace period.");
            }
            else if (earlyMinutes > shift.StopGraceMinutes && shift.DockEarlyLeave)
            {
                last.PaidOut = shiftStop;
                last.Adjustments.Add($"Docked to shift end {shiftStop:h:mm tt} (left {earlyMinutes} min early).");
            }
        }
    }

    // =================================================================
    // Overtime
    // =================================================================

    private sealed class WeekBucket
    {
        public int Regular;
        public int OverTime1;
    }

    private static void SplitOvertime(
        DayTotals day, int paidMinutes, int miscMinutes,
        AppSettings settings, WeekBucket week, bool seventhDay = false)
    {
        int remaining = paidMinutes;
        int ot1 = 0;
        int ot2 = 0;

        if (seventhDay)
        {
            // Every hour on a 7th consecutive day is overtime; vacation/sick stay regular.
            week.OverTime1 += remaining;
            day.RegularMinutes = miscMinutes;
            day.OverTime1Minutes = remaining;
            day.OverTime2Minutes = 0;
            return;
        }

        // Daily level 2 first, then level 1.
        if (settings.DailyOverTime2Minutes > 0 && remaining > settings.DailyOverTime2Minutes)
        {
            ot2 = remaining - settings.DailyOverTime2Minutes;
            remaining = settings.DailyOverTime2Minutes;
        }
        if (settings.DailyOverTime1Minutes > 0 && remaining > settings.DailyOverTime1Minutes)
        {
            int moved = remaining - settings.DailyOverTime1Minutes;
            ot1 += moved;
            remaining = settings.DailyOverTime1Minutes;
        }

        // Weekly thresholds apply to the regular hours accumulated this week.
        if (settings.WeeklyOverTime1Minutes > 0)
        {
            int room = settings.WeeklyOverTime1Minutes - week.Regular;
            if (remaining > room)
            {
                int moved = remaining - Math.Max(room, 0);
                remaining -= moved;
                ot1 += moved;
            }
        }
        if (settings.WeeklyOverTime2Minutes > 0)
        {
            int room = settings.WeeklyOverTime2Minutes - (week.Regular + week.OverTime1);
            if (ot1 > room)
            {
                int moved = ot1 - Math.Max(room, 0);
                ot1 -= moved;
                ot2 += moved;
            }
        }

        week.Regular += remaining;
        week.OverTime1 += ot1;

        day.RegularMinutes = remaining + miscMinutes;
        day.OverTime1Minutes = ot1;
        day.OverTime2Minutes = ot2;
    }

    // =================================================================
    // Rounding helpers
    // =================================================================

    public static DateTime RoundTime(DateTime time, int intervalMinutes, RoundingMode mode)
    {
        if (intervalMinutes <= 0) return time;

        double minutes = (time - time.Date).TotalMinutes;
        int rounded = RoundMinutes((int)Math.Round(minutes), intervalMinutes, mode);

        // Rounding can wrap past midnight; keep the date consistent.
        return time.Date.AddMinutes(rounded);
    }

    public static int RoundMinutes(int minutes, int interval, RoundingMode mode)
    {
        if (interval <= 0) return minutes;
        return mode switch
        {
            RoundingMode.Up => (int)(Math.Ceiling(minutes / (double)interval) * interval),
            RoundingMode.Down => (int)(Math.Floor(minutes / (double)interval) * interval),
            RoundingMode.Nearest => (int)(Math.Round(minutes / (double)interval, MidpointRounding.AwayFromZero) * interval),
            _ => minutes,
        };
    }
}

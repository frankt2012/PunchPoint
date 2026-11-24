using PunchPoint.Core.Models;

namespace PunchPoint.Core.TimeCalc;

/// <summary>A punch that is probably missing, with a time to suggest for it.</summary>
public sealed record MissingPunchSuggestion(
    DateTime WorkDate,
    PunchKind Kind,
    DateTime Suggested,
    string EmployeeName,
    string Reason);

/// <summary>
/// Finds days that cannot be calculated cleanly — an OUT with no IN, or an IN with no OUT —
/// and proposes a time for the missing punch (the shift's start or stop, or eight hours from
/// the punch we do have). The supervisor still confirms each one.
/// </summary>
public static class MissingPunchFinder
{
    public static List<MissingPunchSuggestion> Find(
        Employee employee,
        Shift? shift,
        IEnumerable<Punch> punches,
        int dayChangeOffsetMinutes,
        DateTime from,
        DateTime to)
    {
        var results = new List<MissingPunchSuggestion>();

        var byDay = punches
            .Where(p => p.Kind is PunchKind.In or PunchKind.Out)
            .Where(p => p.Timestamp >= from && p.Timestamp <= to)
            .GroupBy(p => TimeCardCalculator.WorkDate(p.Timestamp, dayChangeOffsetMinutes))
            .OrderBy(g => g.Key);

        foreach (var day in byDay)
        {
            DateTime? openIn = null;
            int index = 0;

            foreach (var punch in day.OrderBy(p => p.Timestamp))
            {
                if (punch.Kind == PunchKind.In)
                {
                    if (openIn is not null)
                    {
                        // two INs in a row: the first one never got its OUT
                        results.Add(Suggestion(employee, shift, day.Key, PunchKind.Out, openIn.Value,
                            "Two clock-ins in a row — the first shift has no clock-out."));
                    }

                    openIn = punch.Timestamp;
                    index++;
                    continue;
                }

                if (openIn is null)
                {
                    results.Add(Suggestion(employee, shift, day.Key, PunchKind.In, punch.Timestamp,
                        "Clock-out with no clock-in."));
                }

                openIn = null;
                index++;
            }

            if (openIn is not null)
            {
                results.Add(Suggestion(employee, shift, day.Key, PunchKind.Out, openIn.Value,
                    "Last clock-in has no clock-out."));
            }
        }

        return results;
    }

    private static MissingPunchSuggestion Suggestion(
        Employee employee, Shift? shift, DateTime workDate, PunchKind kind, DateTime anchor, string reason)
    {
        DateTime suggested = kind switch
        {
            PunchKind.In => shift is null
                ? anchor.AddHours(-8)
                : workDate.Add(shift.Start),
            _ => shift is null
                ? anchor.AddHours(8)
                : StopOn(workDate, shift),
        };

        // Never suggest something after the punch that is already there (or before it, for an OUT).
        if (kind == PunchKind.In && suggested >= anchor)
            suggested = anchor.AddHours(-8);
        if (kind == PunchKind.Out && suggested <= anchor)
            suggested = anchor.AddHours(8);

        return new MissingPunchSuggestion(workDate, kind, suggested, employee.SortName, reason);
    }

    private static DateTime StopOn(DateTime workDate, Shift shift)
    {
        var stop = workDate.Add(shift.Stop);
        if (shift.Stop <= shift.Start) stop = stop.AddDays(1);
        return stop;
    }
}

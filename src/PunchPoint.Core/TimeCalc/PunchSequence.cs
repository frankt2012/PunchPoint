using PunchPoint.Core.Models;
using PunchPoint.Core.Storage;

namespace PunchPoint.Core.TimeCalc;

/// <summary>One punch that was re-typed to keep a day alternating in/out.</summary>
public sealed record PunchKindChange(int PunchId, DateTime Timestamp, PunchKind From, PunchKind To)
{
    public string Describe() =>
        $"{Timestamp:h:mm tt} {PunchSequence.Name(From)} → {PunchSequence.Name(To)}";
}

/// <summary>
/// Keeps clock punches alternating IN, OUT, IN, OUT.
///
/// Someone who forgets to clock in and then presses the clock at the end of the day gets an IN
/// where an OUT was meant — the clock cannot know the difference. Once a supervisor adds the
/// missing punch, the rest of the day can be straightened out: the first punch becomes an IN,
/// the next an OUT, and so on. Without this a day ends up with two INs or two OUTs, and the
/// time calculator ignores the leftovers.
/// </summary>
public static class PunchSequence
{
    public static string Name(PunchKind kind) => kind switch
    {
        PunchKind.In => "IN",
        PunchKind.Out => "OUT",
        PunchKind.BreakOut => "Break out",
        PunchKind.BreakIn => "Break in",
        PunchKind.Hours => "Hours",
        _ => kind.ToString(),
    };

    /// <summary>True for the punches that take part in the in/out alternation.</summary>
    public static bool IsClockPunch(PunchKind kind) => kind is PunchKind.In or PunchKind.Out;

    /// <summary>
    /// Re-types IN/OUT punches in [from, to) so they alternate. The window opens with an IN
    /// unless the employee was already clocked in by a punch before it (an overnight shift, for
    /// example). Break punches and paid-hours entries are left exactly as they are.
    /// </summary>
    public static List<PunchKindChange> Normalize(Store store, int employeeId, DateTime from, DateTime to,
        bool save = true)
    {
        var punches = store.ListPunches(employeeId, from, to)
            .Where(p => p.Timestamp >= from && p.Timestamp < to)
            .OrderBy(p => p.Timestamp)
            .ThenBy(p => p.Id)
            .ToList();

        if (punches.Count == 0) return new List<PunchKindChange>();

        var before = store.ListPunches(employeeId, from.AddDays(-3), from)
            .Where(p => p.Timestamp < from)
            .OrderBy(p => p.Timestamp)
            .ThenBy(p => p.Id)
            .LastOrDefault();

        // Clocked in when the window opened? Then the next punch has to be an OUT.
        bool expectIn = before is null || before.Kind == PunchKind.Out;

        var changes = new List<PunchKindChange>();
        foreach (var punch in punches)
        {
            if (!IsClockPunch(punch.Kind)) continue; // breaks stay as they are

            var wanted = expectIn ? PunchKind.In : PunchKind.Out;
            expectIn = !expectIn;

            if (punch.Kind == wanted) continue;

            changes.Add(new PunchKindChange(punch.Id, punch.Timestamp, punch.Kind, wanted));
            punch.Kind = wanted;
            if (save) store.UpdatePunch(punch);
        }

        return changes;
    }

    /// <summary>The work day a punch belongs to, as [from, to).</summary>
    public static (DateTime From, DateTime To) WorkDay(DateTime moment, int dayChangeOffsetMinutes)
    {
        DateTime workDate = TimeCardCalculator.WorkDate(moment, dayChangeOffsetMinutes);
        return (workDate.AddMinutes(dayChangeOffsetMinutes), workDate.AddDays(1).AddMinutes(dayChangeOffsetMinutes));
    }

    /// <summary>
    /// Normalizes the work day around one punch and audits anything it changed. Used right after
    /// a supervisor adds, edits or deletes a punch.
    /// </summary>
    public static List<PunchKindChange> NormalizeAround(Store store, Employee employee, DateTime moment,
        int dayChangeOffsetMinutes)
    {
        var (from, to) = WorkDay(moment, dayChangeOffsetMinutes);
        var changes = Normalize(store, employee.Id, from, to);

        if (changes.Count > 0)
        {
            store.Audit("Punch sequence fixed",
                $"{employee.DisplayName}: " + string.Join("; ", changes.Select(c => c.Describe())));
        }

        return changes;
    }
}

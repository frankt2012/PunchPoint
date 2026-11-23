using PunchPoint.Core.Models;
using PunchPoint.Core.Storage;

namespace PunchPoint.Core.Services;

public sealed class ClockStatus
{
    public required Employee Employee { get; init; }
    public bool IsClockedIn { get; init; }
    public bool OnBreak { get; init; }
    public DateTime? Since { get; init; }
    public Punch? LastPunch { get; init; }
    public int MinutesToday { get; init; }

    public PunchKind NextKind =>
        !IsClockedIn ? PunchKind.In
        : OnBreak ? PunchKind.BreakIn
        : PunchKind.Out;

    public string ActionLabel => NextKind switch
    {
        PunchKind.In => "Clock In",
        PunchKind.Out => "Clock Out",
        PunchKind.BreakIn => "End Break",
        _ => "Punch",
    };

    public string StateLabel =>
        !IsClockedIn ? "Clocked out"
        : OnBreak ? "On break"
        : "Clocked in";

    public string SinceLabel => Since is null ? "" : $"since {Since:h:mm tt}";
}

/// <summary>Clock-in/out operations shared by the kiosk screens.</summary>
public sealed class ClockService
{
    private readonly Store _store;

    public ClockService(Store store) => _store = store;

    public ClockStatus GetStatus(Employee employee, DateTime? now = null)
    {
        var moment = now ?? DateTime.Now;
        var last = _store.GetLastPunch(employee.Id);

        if (last is null)
        {
            return new ClockStatus { Employee = employee, LastPunch = null };
        }

        bool clockedIn = last.Kind is PunchKind.In or PunchKind.BreakOut;
        bool onBreak = last.Kind == PunchKind.BreakOut;

        // Minutes worked today (closed IN→OUT pairs only), for the kiosk display.
        int minutesToday = 0;
        DateTime? openIn = null;
        foreach (var p in _store.ListPunches(employee.Id, moment.Date, moment.Date.AddDays(1).AddSeconds(-1)))
        {
            if (p.Kind == PunchKind.In && openIn is null)
            {
                openIn = p.Timestamp;
            }
            else if (p.Kind == PunchKind.Out && openIn is not null)
            {
                minutesToday += Math.Max(0, (int)Math.Round((p.Timestamp - openIn.Value).TotalMinutes));
                openIn = null;
            }
        }

        DateTime? since = null;
        if (clockedIn)
        {
            since = last.Kind == PunchKind.BreakOut
                ? openIn
                : last.Timestamp;
        }

        return new ClockStatus
        {
            Employee = employee,
            LastPunch = last,
            IsClockedIn = clockedIn,
            OnBreak = onBreak,
            Since = since,
            MinutesToday = minutesToday,
        };
    }

    public Punch Punch(Employee employee, PunchKind kind, DateTime timestamp, string comment = "",
        PunchSource source = PunchSource.Clock)
    {
        var period = _store.GetPayPeriodFor(timestamp);
        var punch = new Punch
        {
            EmployeeId = employee.Id,
            PayPeriodId = period?.Id,
            Timestamp = timestamp,
            Kind = kind,
            Source = source,
            Comment = comment,
        };
        _store.InsertPunch(punch);
        return punch;
    }

    /// <summary>Clocks the employee in or out based on their current state.</summary>
    public Punch Toggle(Employee employee, DateTime timestamp, string comment = "")
    {
        var status = GetStatus(employee, timestamp);
        var punch = Punch(employee, status.NextKind, timestamp, comment);
        _store.Audit($"{status.NextKind}", $"{employee.DisplayName} at {timestamp:g}", "");
        return punch;
    }

    public Punch StartBreak(Employee employee, DateTime timestamp)
    {
        var status = GetStatus(employee, timestamp);
        if (!status.IsClockedIn || status.OnBreak)
            throw new InvalidOperationException($"{employee.DisplayName} is not clocked in.");
        return Punch(employee, PunchKind.BreakOut, timestamp);
    }

    public Punch EndBreak(Employee employee, DateTime timestamp)
    {
        var status = GetStatus(employee, timestamp);
        if (!status.OnBreak)
            throw new InvalidOperationException($"{employee.DisplayName} is not on a break.");
        return Punch(employee, PunchKind.BreakIn, timestamp);
    }
}

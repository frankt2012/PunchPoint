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
            return new ClockStatus { Employee = employee, LastPunch = null };

        // Clocked in unless the most recent punch was a clock-out. A break ending
        // (BreakIn) leaves the employee on the clock, which the old check missed.
        bool clockedIn = last.Kind != PunchKind.Out;
        bool onBreak = last.Kind == PunchKind.BreakOut;

        // Walk the recent punches to find the open session and today's worked minutes.
        // 36 hours back so an overnight session is understood.
        var punches = _store.ListPunches(employee.Id, moment.AddHours(-36), moment.AddHours(1));

        int minutesToday = 0;
        DateTime? sessionStart = null;
        DateTime? breakStart = null;
        int breakMinutes = 0;

        foreach (var punch in punches)
        {
            switch (punch.Kind)
            {
                case PunchKind.In:
                    sessionStart = punch.Timestamp;
                    breakStart = null;
                    breakMinutes = 0;
                    break;

                case PunchKind.BreakOut:
                    if (sessionStart is not null) breakStart = punch.Timestamp;
                    break;

                case PunchKind.BreakIn:
                    if (breakStart is not null)
                    {
                        breakMinutes += Math.Max(0, (int)Math.Round((punch.Timestamp - breakStart.Value).TotalMinutes));
                        breakStart = null;
                    }
                    break;

                case PunchKind.Out:
                    if (sessionStart is not null)
                    {
                        if (sessionStart.Value.Date >= moment.Date)
                        {
                            int worked = (int)Math.Round((punch.Timestamp - sessionStart.Value).TotalMinutes) - breakMinutes;
                            minutesToday += Math.Max(0, worked);
                        }
                        sessionStart = null;
                        breakStart = null;
                        breakMinutes = 0;
                    }
                    break;
            }
        }

        // If the employee is still on the clock, sessionStart is the current IN.
        DateTime? since = clockedIn ? sessionStart ?? last.Timestamp : null;

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
        // Guard against out-of-order break punches (double clicks, stale screens).
        if (kind is PunchKind.BreakOut or PunchKind.BreakIn)
        {
            var state = GetStatus(employee, timestamp);
            if (kind == PunchKind.BreakOut && !state.IsClockedIn)
                throw new InvalidOperationException($"{employee.DisplayName} is not clocked in, so a break cannot be started.");
            if (kind == PunchKind.BreakOut && state.OnBreak)
                throw new InvalidOperationException($"{employee.DisplayName} is already on a break.");
            if (kind == PunchKind.BreakIn && !state.OnBreak)
                throw new InvalidOperationException($"{employee.DisplayName} is not on a break.");
        }

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

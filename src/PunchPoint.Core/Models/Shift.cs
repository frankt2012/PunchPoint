namespace PunchPoint.Core.Models;

public sealed class Shift
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;

    /// <summary>Shift start time of day (only the time component is used).</summary>
    public TimeSpan Start { get; set; } = new(8, 0, 0);
    public TimeSpan Stop { get; set; } = new(17, 0, 0);

    /// <summary>Minutes before the shift start during which an IN punch still counts as on time.</summary>
    public int StartGraceMinutes { get; set; }
    /// <summary>If a punch is later than start + grace, pull it back to exactly shift start.</summary>
    public bool DockLateArrival { get; set; }

    public int StopGraceMinutes { get; set; }
    /// <summary>If a punch is earlier than stop - grace, push it forward to exactly shift stop.</summary>
    public bool DockEarlyLeave { get; set; }

    public bool LunchEnabled { get; set; }
    public TimeSpan LunchStart { get; set; } = new(12, 0, 0);
    public TimeSpan LunchEnd { get; set; } = new(12, 30, 0);
    /// <summary>Unpaid lunch deducted automatically when the shift spans this many hours.</summary>
    public int LunchAutoAfterMinutes { get; set; }
    public int LunchDurationMinutes { get; set; } = 30;

    public bool HolidaysPaid { get; set; }
    public int HolidayHours { get; set; }

    public int? LegacyId { get; set; }

    /// <summary>Length of the shift, handling overnight shifts.</summary>
    public TimeSpan Length
    {
        get
        {
            var len = Stop > Start ? Stop - Start : Stop + TimeSpan.FromDays(1) - Start;
            return len;
        }
    }

    public bool CrossesMidnight => Stop <= Start;

    public string TimeRangeLabel => $"{FormatTime(Start)} – {FormatTime(Stop)}";

    public static string FormatTime(TimeSpan t)
    {
        var dt = DateTime.Today.Add(t);
        return dt.ToString("h:mm tt");
    }
}

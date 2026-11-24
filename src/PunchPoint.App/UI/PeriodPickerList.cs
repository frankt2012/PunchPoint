using PunchPoint.Core.Models;
using PunchPoint.Core.Storage;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.App.UI;

/// <summary>
/// The periods shown in the drop-downs: everything up to the current period and the one after
/// it. Periods further ahead — and leftovers from an earlier anchor — stay in the database but
/// are not offered, so the list stays short and unambiguous.
/// </summary>
internal static class PeriodPickerList
{
    public static List<PayPeriod> Build(Store store, out PayPeriod? current)
    {
        var settings = store.GetSettings();
        current = store.GetCurrentPayPeriod();

        var all = store.ListPayPeriods()
            .OrderByDescending(p => p.Start)
            .ToList();

        var aligned = all
            .Where(p => PayPeriodGenerator.IsAligned(p.Start, settings))
            .ToList();

        // If nothing is aligned yet (first run before periods exist), fall back to everything.
        if (aligned.Count == 0) aligned = all;

        if (current is null) return aligned;

        var live = current;
        if (!aligned.Any(p => p.Id == live.Id)) return aligned;

        var next = aligned
            .Where(p => p.Start.Date > live.End.Date)
            .OrderBy(p => p.Start)
            .FirstOrDefault();

        var limit = next?.Start.Date ?? live.End.Date;

        return aligned.Where(p => p.Start.Date <= limit).ToList();
    }
}

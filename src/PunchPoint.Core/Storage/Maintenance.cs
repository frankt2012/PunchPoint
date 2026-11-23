using PunchPoint.Core.Models;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.Core.Storage;

/// <summary>
/// Startup and housekeeping shared by the app and the command line tools, so the same
/// code path is exercised by tests.
/// </summary>
public static class Maintenance
{
    /// <summary>Makes sure a pay period exists around today and around any existing punches.</summary>
    public static int EnsurePeriods(Store store, DateTime? today = null)
    {
        var settings = store.GetSettings();
        var now = today ?? DateTime.Today;

        var periods = PayPeriodGenerator.EnsureRange(settings, now.AddMonths(-2), now.AddMonths(3));

        // Cover historical and future punches too, so no punch is ever orphaned.
        if (store.GetPunchDateRange() is var range && range is not null)
        {
            periods.AddRange(PayPeriodGenerator.EnsureRange(settings,
                range.Value.Min.Date, range.Value.Max.Date));
        }

        int created = 0;
        foreach (var period in periods)
        {
            if (store.GetPayPeriodFor(period.Start) is null) created++;
            store.UpsertPayPeriod(period);
        }

        return created;
    }

    /// <summary>
    /// Writes at most one backup per calendar day and prunes copies older than 30 days.
    /// Returns the backup path when a new copy was made.
    /// </summary>
    public static string? DailyBackup(Database database, string? directory = null)
    {
        directory ??= AppPaths.BackupsDirectory;
        Directory.CreateDirectory(directory);

        string dbBase = Path.GetFileNameWithoutExtension(database.Path);
        string todayPrefix = $"{dbBase}-{DateTime.Now:yyyyMMdd}";
        bool alreadyTakenToday = Directory.GetFiles(directory, todayPrefix + "*.db").Length > 0;

        string? created = null;
        if (!alreadyTakenToday && new FileInfo(database.Path).Length >= 4096)
            created = database.Backup(directory);

        try
        {
            foreach (string file in Directory.GetFiles(directory, dbBase + "-*.db"))
            {
                if (File.GetLastWriteTime(file) < DateTime.Now.AddDays(-30))
                    File.Delete(file);
            }
        }
        catch
        {
            // pruning is best effort
        }

        return created;
    }

    public static string IntegrityCheck(Store store) =>
        store.Db.Scalar("PRAGMA integrity_check;")?.ToString() ?? "no result";

    /// <summary>Human-readable summary used by the doctor command and the Settings screen.</summary>
    public static string Describe(Store store)
    {
        long size = File.Exists(store.Db.Path) ? new FileInfo(store.Db.Path).Length : 0;
        var settings = store.GetSettings();
        var periods = store.ListPayPeriods();
        var current = store.GetCurrentPayPeriod();

        return string.Join(Environment.NewLine, new[]
        {
            $"Database:      {store.Db.Path}",
            $"Size:          {size / 1024.0 / 1024.0:F2} MB",
            $"Employees:     {store.ListEmployees(includeInactive: true).Count} " +
            $"({store.ListEmployees(includeInactive: false).Count} active)",
            $"Punches:       {store.CountPunches()}",
            $"Pay periods:   {periods.Count}",
            $"Current:       {(current is null ? "none" : current.Label + " (" + current.Type + ")")}",
            $"Company:       {settings.CompanyName}",
            $"Rounding:      {(settings.RoundingMinutes == 0 ? "off" : settings.RoundingMinutes + " min")}",
            $"Overtime:      daily {settings.DailyOverTime1Minutes / 60.0:0.##} h, " +
            $"weekly {settings.WeeklyOverTime1Minutes / 60.0:0.##} h",
            $"Data folder:   {AppPaths.DataDirectory}",
            $"Backups:       {AppPaths.BackupsDirectory}",
        });
    }
}

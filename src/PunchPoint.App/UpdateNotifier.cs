using PunchPoint.App.UI;
using PunchPoint.Core.Storage;
using PunchPoint.Core.Updates;

namespace PunchPoint.App;

/// <summary>
/// Daily, quiet update check against the project's GitHub releases. Nothing is downloaded or
/// installed automatically: when a newer version exists it offers the release page and can be
/// told to skip that version.
/// </summary>
internal static class UpdateNotifier
{
    public static Version CurrentVersion()
    {
        var version = typeof(UpdateNotifier).Assembly.GetName().Version ?? new Version(0, 0, 0);
        return new Version(version.Major, Math.Max(0, version.Minor), Math.Max(0, version.Build));
    }

    public static void Begin(MainForm owner, Store store)
    {
        AppSettingsView settings;
        try { settings = new AppSettingsView(store.GetSettings()); }
        catch { return; }

        if (!settings.CheckForUpdates) return;

        if (settings.LastCheck is DateTime last && DateTime.Now - last < TimeSpan.FromDays(1))
            return;

        Version current = CurrentVersion();

        _ = Task.Run(async () =>
        {
            UpdateResult result;
            try
            {
                result = await UpdateChecker.CheckAsync(settings.Repo, current);
            }
            catch
            {
                return;
            }

            if (owner.IsDisposed) return;

            try
            {
                owner.BeginInvoke(() =>
                {
                    MarkChecked(store);

                    if (!result.HasUpdate || result.Update is null) return;
                    if (string.Equals(result.Update.Display, settings.Skipped, StringComparison.Ordinal))
                        return;

                    Show(owner, store, result.Update, current);
                });
            }
            catch
            {
                // The window closed while the request was in flight.
            }
        });
    }

    /// <summary>Manual check used by the Settings screen.</summary>
    public static async Task<UpdateResult> CheckNowAsync(Store store, string? repo)
    {
        var result = await UpdateChecker.CheckAsync(repo, CurrentVersion());
        MarkChecked(store);
        return result;
    }

    private static void MarkChecked(Store store)
    {
        try
        {
            var settings = store.GetSettings();
            settings.LastUpdateCheck = DateTime.Now;
            store.SaveSettings(settings);
        }
        catch
        {
            // A failed bookkeeping write must not break the check.
        }
    }

    public static void Show(MainForm owner, Store store, UpdateInfo update, Version current)
    {
        using var dialog = new UpdateDialog(update, current);
        dialog.ShowDialog(owner);

        if (!dialog.SkipRequested) return;

        try
        {
            var settings = store.GetSettings();
            settings.SkippedUpdateVersion = update.Display;
            store.SaveSettings(settings);
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>Snapshot taken on the UI thread so the background task only reads plain values.</summary>
    private sealed record AppSettingsView(bool CheckForUpdates, string Repo, DateTime? LastCheck, string Skipped)
    {
        public AppSettingsView(Core.Models.AppSettings settings)
            : this(settings.CheckForUpdates,
                   UpdateChecker.NormaliseRepo(settings.UpdateRepo) ?? UpdateChecker.DefaultRepo,
                   settings.LastUpdateCheck,
                   settings.SkippedUpdateVersion)
        {
        }
    }
}

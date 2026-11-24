using PunchPoint.Core.Models;

namespace PunchPoint.Core.Storage;

/// <summary>
/// Daily backups with retention and an optional second copy in a synced folder — Google
/// Drive, OneDrive, Dropbox or a network share all work, because they are just folders.
/// </summary>
public static class BackupService
{
    /// <summary>
    /// Makes at most one backup per day and prunes old ones. Returns the file that was created,
    /// or null when today already has a backup.
    /// </summary>
    public static string? EnsureDailyBackup(Store store, AppSettings settings, DateTime now)
    {
        Directory.CreateDirectory(AppPaths.BackupsDirectory);

        string today = now.ToString("yyyyMMdd");
        bool exists = Directory
            .EnumerateFiles(AppPaths.BackupsDirectory, "*.db")
            .Any(file => Path.GetFileName(file).Contains("-" + today + "-", StringComparison.Ordinal));

        string? created = null;

        if (!exists)
        {
            created = store.Db.Backup();

            settings.LastBackupPath = created;
            store.SaveSettings(settings);

            if (settings.BackupMirrorFolder.Length > 0)
                Mirror(created, settings.BackupMirrorFolder);
        }

        if (settings.BackupRetention > 0)
        {
            Prune(AppPaths.BackupsDirectory, settings.BackupRetention);
            if (settings.BackupMirrorFolder.Length > 0)
                Prune(settings.BackupMirrorFolder, settings.BackupRetention);
        }

        return created;
    }

    /// <summary>Copies one backup into the mirror folder, ignoring failures (a missing drive).</summary>
    public static string? Mirror(string file, string folder)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folder)) return null;

            Directory.CreateDirectory(folder);
            string target = Path.Combine(folder, Path.GetFileName(file));
            File.Copy(file, target, overwrite: true);
            return target;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Keeps the newest <paramref name="keep"/> backups in a folder, deletes the rest.</summary>
    public static int Prune(string folder, int keep)
    {
        if (keep <= 0 || !Directory.Exists(folder)) return 0;

        try
        {
            var files = Directory
                .EnumerateFiles(folder, "*.db")
                .Select(path => new FileInfo(path))
                // the timestamp is in the name and in the write time; newest first
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ThenByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int removed = 0;
            foreach (var file in files.Skip(keep))
            {
                try
                {
                    file.Delete();
                    removed++;
                }
                catch
                {
                    // a file in use is simply left for the next run
                }
            }

            return removed;
        }
        catch
        {
            return 0;
        }
    }
}

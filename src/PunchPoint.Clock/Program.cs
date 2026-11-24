using PunchPoint.App.UI;
using PunchPoint.Core.Security;
using PunchPoint.Core.Storage;

namespace PunchPoint.Clock;

/// <summary>
/// PunchPointClock.exe — the employee clock. Deliberately has no management features:
/// it can only show the PIN pad and record punches.
/// </summary>
internal static class Program
{
    private static Mutex? _singleInstance;

    [STAThread]
    static void Main(string[] args)
    {
        _singleInstance = new Mutex(true, @"Local\PunchPointClockKiosk", out bool isFirst);
        if (!isFirst)
        {
            MessageBox.Show("The clock is already open.", "PunchPoint Clock",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetDefaultFont(Theme.Body);

        Application.ThreadException += (_, e) => Report(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Report(e.ExceptionObject as Exception);

        try
        {
            AppPaths.EnsureDataDirectory();

            string? dbOverride = ValueOf(args, "--db");
            using var database = dbOverride is null ? Database.OpenDefault() : Database.Open(dbOverride);
            var store = new Store(database);

            Maintenance.DailyBackup(database);
            Maintenance.EnsurePeriods(store);

            // Windowed by default (shared PC); kiosk mode is opt-in and can be forced
            // per shortcut with --kiosk / --windowed.
            bool? forced = args.Any(a => string.Equals(a, "--kiosk", StringComparison.OrdinalIgnoreCase))
                ? true
                : args.Any(a => string.Equals(a, "--windowed", StringComparison.OrdinalIgnoreCase))
                    ? false
                    : null;

            var settings = store.GetSettings();
            Theme.Use(settings.Theme);
            Fmt.Totals = settings.TotalsFormat;
            bool fullScreen = forced ?? settings.ClockFullScreen;

            store.Audit("Clock started", fullScreen ? "kiosk mode" : "windowed mode");

            // The clock usually runs all day, so it takes the daily backup too.
            _ = Task.Run(() =>
            {
                try
                {
                    using var backupDb = Database.Open(database.Path);
                    var backupStore = new Store(backupDb);
                    Core.Storage.BackupService.EnsureDailyBackup(
                        backupStore, backupStore.GetSettings(), DateTime.Now);
                }
                catch
                {
                    // a failed backup must not stop the clock
                }
            });

            // The clock usually runs all day, so it is the natural place to send the
            // end-of-period summary.
            var mailTimer = new System.Windows.Forms.Timer { Interval = 30 * 60 * 1000 };
            mailTimer.Tick += (_, _) => Task.Run(() =>
            {
                try
                {
                    using var mailDb = Database.Open(database.Path);
                    Core.Email.PeriodMailer.RunIfDue(new Store(mailDb));
                }
                catch
                {
                    // ignore
                }
            });
            mailTimer.Start();

            Application.Run(new KioskForm(store, fullScreen));
            store.Audit("Clock closed", Environment.UserName);
        }
        catch (Exception ex)
        {
            Report(ex);
        }
    }

    private static string? ValueOf(string[] args, string name)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                return args[i][(name.Length + 1)..];
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return args[i + 1];
        }
        return null;
    }

    private static void Report(Exception? ex)
    {
        if (ex is null) return;

        try
        {
            AppPaths.EnsureDataDirectory();
            File.AppendAllText(Path.Combine(AppPaths.LogsDirectory, $"clock-error-{DateTime.Now:yyyyMMdd}.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}");
        }
        catch
        {
            // ignore
        }

        MessageBox.Show(
            "The clock hit a problem.\r\n\r\n" + ex.Message +
            "\r\n\r\nDetails were written to:\r\n" + AppPaths.LogsDirectory,
            "PunchPoint Clock", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

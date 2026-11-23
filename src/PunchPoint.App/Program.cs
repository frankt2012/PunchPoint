using System.Text;
using Microsoft.Data.Sqlite;
using PunchPoint.App.UI;
using PunchPoint.App.UI.Screens;
using PunchPoint.Core.Models;
using PunchPoint.Core.Services;
using PunchPoint.Core.Storage;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.App;

internal static class Program
{
    private static Mutex? _singleInstance;

    [STAThread]
    static void Main()
    {
        // Only one copy may run against the database at a time.
        _singleInstance = new Mutex(true, @"Local\PunchPointTimeClock", out bool isFirst);
        if (!isFirst)
        {
            MessageBox.Show("PunchPoint is already running.", "PunchPoint",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetDefaultFont(Theme.Body);

        Application.ThreadException += (_, e) => ReportFatal(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportFatal(e.ExceptionObject as Exception);

        try
        {
            AppPaths.EnsureDataDirectory();
            var database = Database.OpenDefault();
            var store = new Store(database);

            // A daily safety copy, kept for 30 days. Cheap insurance on a work PC.
            Maintenance.DailyBackup(database);

            // Always have a period covering today so the first screen is useful.
            Maintenance.EnsurePeriods(store);

            var services = new AppServices(store);
            Application.Run(new MainForm(services));
            database.Dispose();
        }
        catch (Exception ex)
        {
            ReportFatal(ex);
        }
    }

    private static void ReportFatal(Exception? ex)
    {
        if (ex is null) return;

        try
        {
            AppPaths.EnsureDataDirectory();
            string log = Path.Combine(AppPaths.LogsDirectory, $"error-{DateTime.Now:yyyyMMdd}.log");
            File.AppendAllText(log,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{new string('-', 70)}{Environment.NewLine}",
                Encoding.UTF8);
        }
        catch
        {
            // ignore
        }

        MessageBox.Show(
            "PunchPoint hit an unexpected problem.\r\n\r\n" +
            ex.Message +
            "\r\n\r\nDetails were written to:\r\n" + Path.Combine(AppPaths.LogsDirectory) +
            "\r\n\r\nYour data has not been changed by this error.",
            "PunchPoint", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

/// <summary>Shared state handed to every screen.</summary>
public sealed class AppServices
{
    public AppServices(Store store)
    {
        Store = store;
        Settings = store.GetSettings();
        Clock = new ClockService(store);
    }

    public Store Store { get; }
    public AppSettings Settings { get; set; }
    public ClockService Clock { get; }

    public event Action? DataChanged;

    public void NotifyDataChanged()
    {
        Settings = Store.GetSettings();
        DataChanged?.Invoke();
    }

    public List<Employee> ActiveEmployees() => Store.ListEmployees(includeInactive: false);

    public TimeCardResultSummary SummaryFor(Employee employee, PayPeriod period)
    {
        var punches = Store.ListPunches(employee.Id, period.Start, period.End.AddDays(1));
        var shift = employee.ShiftId is int id ? Store.GetShift(id) : null;
        var card = Store.GetTimeCard(employee.Id, period.Id);
        var result = TimeCardCalculator.Calculate(employee, shift, period, punches, Settings,
            Store.ListHolidays(), card);

        return new TimeCardResultSummary(result);
    }
}

public sealed class TimeCardResultSummary
{
    public TimeCardResultSummary(TimeCardResult result) => Result = result;
    public TimeCardResult Result { get; }
    public int TotalMinutes => Result.TotalMinutes;
    public int RegularMinutes => Result.RegularMinutes;
    public int OverTime1Minutes => Result.OverTime1Minutes;
    public int OverTime2Minutes => Result.OverTime2Minutes;
    public int HolidayMinutes => Result.HolidayMinutes;
    public int IssueCount => Result.Issues.Count;
}

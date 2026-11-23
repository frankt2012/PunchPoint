using PunchPoint.App.UI;
using PunchPoint.App.UI.Screens;
using System.Drawing.Imaging;
using System.Text;

namespace PunchPoint.App;

/// <summary>
/// Diagnostic self-tour: visits every main screen, captures a screenshot of the window and
/// writes a short report. Started with "PunchPoint.exe --tour &lt;folder&gt;".
/// Used to check the UI on a real Windows desktop without anyone clicking.
/// </summary>
internal sealed class ScreenTour
{
    private static readonly (string Key, string Name)[] Screens =
    {
        ("clock", "1-clock"),
        ("dashboard", "2-dashboard"),
        ("employees", "3-employees"),
        ("messages", "4-messages"),
        ("timecards", "5-punchediting"),
        ("punches", "6-punches"),
        ("import", "7-import"),
        ("reports", "8-reports"),
        ("export", "9-export"),
        ("settings", "10-settings"),
    };

    private readonly MainForm _form;
    private readonly string _folder;
    private readonly string _dbPath;
    private readonly AppServices _services;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 900 };
    private int _index = -1;
    private bool _busy;
    private readonly StringBuilder _report = new();

    public ScreenTour(MainForm form, string folder, string dbPath, AppServices services)
    {
        _form = form;
        _folder = folder;
        _dbPath = dbPath;
        _services = services;
        _timer.Tick += (_, _) => Next();
    }

    public void Start()
    {
        _form.TopMost = true;
        _form.Activate();
        _timer.Start();
    }

    private void Next()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            NextCore();
        }
        finally
        {
            _busy = false;
        }
    }

    private void NextCore()
    {
        _index++;
        if (_index >= Screens.Length)
        {
            _timer.Stop();
            CaptureWindowedLayouts();
            WriteReport();
            _form.SuppressCloseConfirmation = true;
            _form.Close();
            return;
        }

        var (key, name) = Screens[_index];

        try
        {
            _form.Show(key);
            _form.Refresh();
            Application.DoEvents();

            // Give late layout/paint a moment before capturing.
            Thread.Sleep(500);
            Capture(Path.Combine(_folder, name + ".png"));
            _report.AppendLine($"{name}: captured");
        }
        catch (Exception ex)
        {
            _report.AppendLine($"{name}: FAILED {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// The screens above are maximised. These extra captures check the layouts people
    /// actually hit: a normal-sized window (where headings used to collide with the
    /// action buttons) and the dialogs with editable fields.
    /// </summary>
    private void CaptureWindowedLayouts()
    {
        _report.AppendLine("windowed: starting");
        try
        {
            _report.AppendLine($"windowed: state={_form.WindowState} size={_form.Size}");
            _form.WindowState = FormWindowState.Normal;
            _form.Size = new Size(1180, 780);
            _form.Location = new Point(40, 40);
            Application.DoEvents();

            foreach (var (key, name) in new[]
                     {
                         ("dashboard", "11-windowed-dashboard"),
                         ("timecards", "12-windowed-punchediting"),
                         ("reports", "13-windowed-reports"),
                         ("settings", "14-windowed-settings"),
                     })
            {
                _form.Show(key);
                _form.Refresh();
                Application.DoEvents();
                Thread.Sleep(450);
                Capture(Path.Combine(_folder, name + ".png"));
                _report.AppendLine($"{name}: captured");
            }

            // The Settings page is long: capture the email and web cards too.
            if (_form.Screen is SettingsScreen settings)
            {
                foreach (var (scroll, name) in new (Action, string)[]
                         {
                             (settings.ScrollToEmailCard, "15-windowed-settings-email"),
                             (settings.ScrollToWebCard, "16-windowed-settings-web"),
                         })
                {
                    scroll();
                    Application.DoEvents();
                    Thread.Sleep(450);
                    Capture(Path.Combine(_folder, name + ".png"));
                    _report.AppendLine($"{name}: captured");
                }
            }

            using var dialog = new EmployeeEditDialog(_services, null)
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(60, 60),
            };
            dialog.Show();
            Application.DoEvents();
            Thread.Sleep(500);
            using (var bitmap = new Bitmap(dialog.Width, dialog.Height))
            {
                dialog.DrawToBitmap(bitmap, new Rectangle(0, 0, dialog.Width, dialog.Height));
                bitmap.Save(Path.Combine(_folder, "17-add-employee.png"), ImageFormat.Png);
            }
            _report.AppendLine("17-add-employee: captured");
            dialog.Close();
        }
        catch (Exception ex)
        {
            _report.AppendLine($"windowed capture FAILED {ex.GetType().Name}: {ex.Message}");
            _report.AppendLine(ex.StackTrace ?? "");
        }
    }

    private void Capture(string path)
    {
        // Draw the window itself rather than the screen: this still works when the
        // window is covered by another window, and produces a clean image.
        int width = Math.Max(_form.Width, 1);
        int height = Math.Max(_form.Height, 1);
        using var bitmap = new Bitmap(width, height);
        try
        {
            _form.DrawToBitmap(bitmap, new Rectangle(0, 0, width, height));
        }
        catch
        {
            using var g = Graphics.FromImage(bitmap);
            g.CopyFromScreen(_form.Bounds.Location, Point.Empty, bitmap.Size);
        }
        bitmap.Save(path, ImageFormat.Png);
    }

    private void WriteReport()
    {
        try
        {
            _report.AppendLine();
            _report.AppendLine($"Generated:      {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _report.AppendLine($"Windows:        {Environment.OSVersion.VersionString} ({Environment.OSVersion.Platform})");
            _report.AppendLine($"64-bit process: {Environment.Is64BitProcess}");
            _report.AppendLine($"Architecture:   {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
            _report.AppendLine($"Screen:         {Screen.PrimaryScreen?.Bounds.Width}x{Screen.PrimaryScreen?.Bounds.Height}");
            _report.AppendLine($"Window:         {_form.Width}x{_form.Height} (DPI {_form.DeviceDpi})");
            _report.AppendLine($"Database:       {_dbPath}");
            _report.AppendLine($"Culture:        {System.Globalization.CultureInfo.CurrentCulture.Name}");

            try
            {
                using var db = Core.Storage.Database.Open(_dbPath);
                var store = new Core.Storage.Store(db);
                _report.AppendLine($"Employees:      {store.ListEmployees(includeInactive: true).Count}");
                _report.AppendLine($"Punches:        {store.CountPunches()}");
                _report.AppendLine($"Periods:        {store.ListPayPeriods().Count}");
                _report.AppendLine($"Integrity:      {Core.Storage.Maintenance.IntegrityCheck(store)}");
            }
            catch (Exception ex)
            {
                _report.AppendLine($"Database check: {ex.Message}");
            }

            File.WriteAllText(Path.Combine(_folder, "report.txt"), _report.ToString(), Encoding.UTF8);
        }
        catch
        {
            // nothing else we can do
        }
    }
}

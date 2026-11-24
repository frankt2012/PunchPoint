using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;
using PunchPoint.App.UI;

namespace PunchPoint.Setup;

internal static class Program
{
    public const string ProductName = "PunchPoint";

    /// <summary>Set from the assembly version (dotnet publish -p:Version=...).</summary>
    public static string Version
    {
        get
        {
            var v = typeof(Program).Assembly.GetName().Version;
            return v is null ? "1.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    [STAThread]
    static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetDefaultFont(Theme.Body);

        if (!IsAdministrator())
        {
            MessageBox.Show("Setup needs administrator rights to install into Program Files.",
                ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Application.Run(new SetupForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show("Setup could not finish:\r\n\r\n" + ex.Message,
                ProductName + " Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}

public sealed class SetupForm : Form
{
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PunchPoint";

    private readonly TextBox _path;
    private readonly CheckBox _desktop;
    private readonly CheckBox _startWithWindows;
    private readonly CheckBox _startNow;
    private readonly ProgressBar _progress;
    private readonly Label _status;
    private readonly Button _install;
    private readonly Button _close;

    public SetupForm()
    {
        Text = "PunchPoint Setup";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Theme.Surface;
        Font = Theme.Body;
        ClientSize = new Size(680, 470);
        Padding = new Padding(28, 22, 28, 18);

        var title = new Label
        {
            Text = "PunchPoint",
            Font = new Font(Theme.Family, 24f, FontStyle.Bold),
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(26, 20),
        };

        var subtitle = new Label
        {
            Text = "Time clock and attendance  ·  version " + Program.Version,
            Font = Theme.Body,
            ForeColor = Theme.TextMuted,
            AutoSize = true,
            Location = new Point(29, 64),
        };

        var blurb = new Label
        {
            Text = "This installs two programs:\r\n" +
                   "    PunchPoint.exe          the password-protected management app\r\n" +
                   "    PunchPointClock.exe     the full-screen employee clock",
            Font = Theme.Small,
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(30, 96),
        };

        var folderLabel = new Label
        {
            Text = "Install folder",
            Font = Theme.SmallBold,
            ForeColor = Theme.TextMuted,
            AutoSize = true,
            Location = new Point(30, 164),
        };

        _path = new TextBox
        {
            Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PunchPoint"),
            Location = new Point(30, 184),
            Width = 530,
            Font = Theme.Body,
        };

        var browse = Theme.SecondaryButton("Browse…", 100);
        browse.Location = new Point(568, 181);
        browse.Click += (_, _) => Browse();

        _desktop = new CheckBox
        {
            Text = "Create desktop shortcuts",
            Checked = true,
            AutoSize = true,
            Location = new Point(30, 224),
            Font = Theme.Body,
        };

        _startWithWindows = new CheckBox
        {
            Text = "Start the employee clock automatically when this user signs in",
            AutoSize = true,
            Location = new Point(30, 250),
            Font = Theme.Body,
        };

        _startNow = new CheckBox
        {
            Text = "Start PunchPoint when setup finishes",
            Checked = true,
            AutoSize = true,
            Location = new Point(30, 276),
            Font = Theme.Body,
        };

        var note = new Label
        {
            Text = "Your data is stored separately in C:\\ProgramData\\PunchPoint, so uninstalling and " +
                   "reinstalling never loses time records unless you ask it to.",
            Font = Theme.Small,
            ForeColor = Theme.TextMuted,
            AutoSize = false,
            Size = new Size(620, 36),
            Location = new Point(30, 306),
        };

        _progress = new ProgressBar
        {
            Location = new Point(30, 352),
            Width = 440,
            Height = 18,
            Style = ProgressBarStyle.Continuous,
        };

        _status = new Label
        {
            Text = "Ready to install.",
            Font = Theme.Small,
            ForeColor = Theme.TextMuted,
            AutoSize = false,
            Size = new Size(440, 20),
            Location = new Point(30, 376),
        };

        _install = Theme.PrimaryButton("Install", 120);
        _install.Location = new Point(480, 348);
        _install.Height = 38;
        _install.Click += (_, _) => Install();

        _close = Theme.SecondaryButton("Close", 100);
        _close.Location = new Point(500, 400);
        _close.Click += (_, _) => Close();

        Controls.AddRange(new Control[]
        {
            title, subtitle, blurb, folderLabel, _path, browse,
            _desktop, _startWithWindows, _startNow, note,
            _progress, _status, _install, _close,
        });

        AcceptButton = _install;
        CancelButton = _close;
    }

    private void Browse()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose where PunchPoint should be installed",
            SelectedPath = _path.Text,
            ShowNewFolderButton = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _path.Text = dialog.SelectedPath;
    }

    private void Install()
    {
        string target = _path.Text.Trim();
        if (target.Length == 0)
        {
            PromptDialog.Warn(this, "Choose an install folder.");
            return;
        }

        try
        {
            _install.Enabled = false;
            _close.Enabled = false;
            CloseRunningPrograms();

            Directory.CreateDirectory(target);
            SetStatus("Unpacking PunchPoint…", 10);
            ExtractPayload(target);

            SetStatus("Creating shortcuts…", 60);
            CreateShortcuts(target);
            RefreshShellIcons();

            SetStatus("Registering with Windows…", 75);
            WriteRegistry(target);
            WriteUninstaller(target);

            if (_startWithWindows.Checked) EnableAutoStart(target);

            SetStatus("Installed.", 100);

            string message = "PunchPoint is installed.\r\n\r\n" +
                             "The first time you start it you will create the supervisor password.\r\n" +
                             "Your existing data folder was left untouched.";

            if (PromptDialog.Confirm(this, message + "\r\n\r\nOpen the folder now?"))
                Process.Start("explorer.exe", target);

            if (_startNow.Checked)
            {
                string app = Path.Combine(target, "PunchPoint.exe");
                if (File.Exists(app))
                    Process.Start(new ProcessStartInfo(app) { UseShellExecute = true, WorkingDirectory = target });
            }

            Close();
        }
        catch (Exception ex)
        {
            _install.Enabled = true;
            _close.Enabled = true;
            _progress.Value = 0;
            SetStatus("Install failed.", 0);
            PromptDialog.Warn(this,
                "The installation did not finish:\r\n\r\n" + ex.Message +
                "\r\n\r\nIf PunchPoint or the clock is running, close it and try again.");
        }
    }

    private void SetStatus(string text, int percent)
    {
        _status.Text = text;
        _progress.Value = Math.Clamp(percent, 0, 100);
        Application.DoEvents();
    }

    private void CloseRunningPrograms()
    {
        foreach (string name in new[] { "PunchPoint", "PunchPointClock" })
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                try { process.CloseMainWindow(); process.WaitForExit(2000); } catch { }
                try { if (!process.HasExited) process.Kill(); } catch { }
            }
        }
    }

    private static void ExtractPayload(string target)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("PunchPoint.Setup.payload.zip")
                           ?? throw new InvalidOperationException("The installer payload is missing. Rebuild the installer.");

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith("/", StringComparison.Ordinal)) continue;

            string destination = Path.Combine(target, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
        }
    }

    private void CreateShortcuts(string target)
    {
        string startMenu = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "PunchPoint");
        Directory.CreateDirectory(startMenu);

        CreateShortcut(Path.Combine(startMenu, "PunchPoint.lnk"), Path.Combine(target, "PunchPoint.exe"));
        CreateShortcut(Path.Combine(startMenu, "Employee Clock.lnk"), Path.Combine(target, "PunchPointClock.exe"));

        if (_desktop.Checked)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            CreateShortcut(Path.Combine(desktop, "PunchPoint.lnk"), Path.Combine(target, "PunchPoint.exe"));
            CreateShortcut(Path.Combine(desktop, "Employee Clock.lnk"), Path.Combine(target, "PunchPointClock.exe"));
        }
    }

    /// <summary>
    /// Installing over an older version replaces the executables, but Explorer keeps the old
    /// icon in its cache and the shortcuts keep showing it. Tell the shell the icons changed
    /// and rebuild the cache so the new artwork appears straight away.
    /// </summary>
    private static void RefreshShellIcons()
    {
        const int SHCNE_ASSOCCHANGED = 0x08000000;
        const uint SHCNF_IDLIST = 0x0000;

        try { SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero); }
        catch { /* cosmetic */ }

        try
        {
            var process = Process.Start(new ProcessStartInfo("ie4uinit.exe", "-show")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            process?.WaitForExit(4000);
        }
        catch
        {
            // Older Windows builds do not have ie4uinit.exe; the notification above is enough.
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

    private static void CreateShortcut(string linkPath, string targetPath)
    {
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type is null) return;

            object shell = Activator.CreateInstance(type)!;
            object shortcut = type.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell,
                new object[] { linkPath })!;

            var shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut,
                new object[] { Path.GetDirectoryName(targetPath) ?? "" });
            shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut,
                new object[] { targetPath + ",0" });
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);

            if (shell is IDisposable disposable) disposable.Dispose();
        }
        catch
        {
            // A missing shortcut must never fail the install.
        }
    }

    private void WriteRegistry(string target)
    {
        using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
            .CreateSubKey(UninstallKey, writable: true);

        key.SetValue("DisplayName", "PunchPoint Time Clock");
        key.SetValue("DisplayVersion", Program.Version);
        key.SetValue("Publisher", "Frank Triolo");
        key.SetValue("DisplayIcon", Path.Combine(target, "PunchPoint.exe"));
        key.SetValue("InstallLocation", target);
        key.SetValue("UninstallString",
            $"powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{Path.Combine(target, "uninstall.ps1")}\"");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", 108_000, RegistryValueKind.DWord);
    }

    private static void EnableAutoStart(string target)
    {
        using var key = Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        key.SetValue("PunchPointClock", "\"" + Path.Combine(target, "PunchPointClock.exe") + "\"");
    }

    private static void WriteUninstaller(string target)
    {
        const string script = """
$ErrorActionPreference = 'SilentlyContinue'
Add-Type -AssemblyName System.Windows.Forms
$dir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$smp  = Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'PunchPoint'
$desk = [Environment]::GetFolderPath('CommonDesktopDirectory')

Remove-Item (Join-Path $smp 'PunchPoint.lnk')
Remove-Item (Join-Path $smp 'Employee Clock.lnk')
Remove-Item $smp
Remove-Item (Join-Path $desk 'PunchPoint.lnk')
Remove-Item (Join-Path $desk 'Employee Clock.lnk')
Remove-Item 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PunchPoint' -Recurse
Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'PunchPointClock'

$data = Join-Path $env:ProgramData 'PunchPoint'
$answer = [System.Windows.Forms.MessageBox]::Show(
  "Also delete your PunchPoint data?`r`n`r`nThis removes the database, backups and logs from:`r`n$data`r`n`r`nChoose No to keep your time records.",
  'Uninstall PunchPoint', 'YesNo', 'Question', 'Button2')

if ($answer -eq 'Yes') { Remove-Item $data -Recurse -Force }

Start-Process -WindowStyle Hidden -FilePath 'cmd.exe' -ArgumentList '/c', ('ping -n 2 127.0.0.1 >nul & rmdir /s /q "' + $dir + '"')
[System.Windows.Forms.MessageBox]::Show('PunchPoint has been removed.', 'PunchPoint')
""";

        File.WriteAllText(Path.Combine(target, "uninstall.ps1"), script, new System.Text.UTF8Encoding(true));
    }
}

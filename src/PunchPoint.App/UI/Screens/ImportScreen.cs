using PunchPoint.App.UI;
using PunchPoint.Core.Import;
using PunchPoint.Core.Models;
using PunchPoint.Core.Storage;

namespace PunchPoint.App.UI.Screens;

public sealed class ImportScreen : ScreenBase
{
    private readonly TextBox _path = Theme.Input(560);
    private readonly Label _status = new() { AutoSize = true, Font = Theme.Body, ForeColor = Theme.TextMuted };
    private readonly DataGridView _employeeGrid = new();
    private readonly CheckBox _doEmployees = Theme.Check("Import employees");
    private readonly CheckBox _doSettings = Theme.Check("Copy company rules (overtime, rounding, day change)");
    private readonly CheckBox _doMisc = Theme.Check("Import paid hours (vacation / holiday / sick rows)");
    private readonly CheckBox _doApprovals = Theme.Check("Import approved time cards");
    private readonly CheckBox _deactivateStale = Theme.Check("Deactivate employees with no punches in the last 90 days");
    private readonly CheckBox _useDates = Theme.Check("Only import punches in this date range");
    private readonly DateTimePicker _from = Theme.Date(130);
    private readonly DateTimePicker _to = Theme.Date(130);
    private readonly ComboBox _direction = Theme.Dropdown(320);
    private readonly ProgressBar _progress = new() { Style = ProgressBarStyle.Marquee, Visible = false, Height = 6 };
    private readonly Button _importButton = Theme.PrimaryButton("Start import", 150);
    private readonly Label _log = new() { AutoSize = true, Font = Theme.Small, ForeColor = Theme.TextMuted };

    private string? _workingPath;
    private string? _csvPath;
    private List<string>? _batchFiles;
    private readonly Label _csvSummary = new() { AutoSize = true, Font = Theme.Small, ForeColor = Theme.TextMuted };
    private readonly CheckBox _csvCreateMissing = Theme.Check("Create employees that are not found (using the badge/PIN as the name)");
    private readonly Button _csvImportButton = Theme.PrimaryButton("Import CSV punches", 170);

    public ImportScreen(AppServices services)
        : base(services, "Import", "Bring your employees and punches over from timeQplus")
    {
        var browse = Theme.SecondaryButton("Choose database…", 170);
        browse.Click += (_, _) => ChooseFile();
        Actions.Controls.Add(browse);

        _path.ReadOnly = true;
        _path.Dock = DockStyle.Top;
        _path.BackColor = Theme.SurfaceAlt;

        var stack = new StackPanel(14);
        Body.Controls.Add(stack);

        // ---- step 1: file ----
        var fileCard = Theme.Card();
        var fileStack = new StackPanel(8);
        fileStack.Add(new Label
        {
            Text = "1.  Select your existing timeQplus database",
            Font = Theme.H3,
            ForeColor = Theme.Text,
            AutoSize = true,
        });
        fileStack.Add(new Label
        {
            Text = "This is normally arx_active.mdb in C:\\ProgramData\\Acroprint\\timeQplusV3\\Data. " +
                   "The file is only read — nothing in the old folder is changed.",
            Font = Theme.Small,
            ForeColor = Theme.TextMuted,
            AutoSize = true,
            MaximumSize = new Size(820, 0),
        });

        var pathRow = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Top, Height = 44, AutoSize = false };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        _path.Dock = DockStyle.Fill;
        _path.Margin = new Padding(0, 6, 8, 0);
        var detect = Theme.SecondaryButton("Find it", 110);
        detect.Margin = new Padding(0, 4, 0, 0);
        detect.Click += (_, _) => AutoDetect();
        pathRow.Controls.Add(_path, 0, 0);
        pathRow.Controls.Add(detect, 1, 0);
        fileStack.Add(pathRow);

        var folderRow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
        var chooseFolder = Theme.PrimaryButton("Import the whole Data folder (all history)…", 330);
        chooseFolder.Click += (_, _) => ChooseFolder();
        folderRow.Controls.Add(chooseFolder);
        folderRow.Controls.Add(new Label
        {
            Text = "Recommended — brings every weekly snapshot in the Archives folder too.",
            Font = Theme.Small,
            ForeColor = Theme.TextMuted,
            AutoSize = true,
            Margin = new Padding(10, 10, 0, 0),
        });
        fileStack.Add(folderRow);
        fileCard.Controls.Add(fileStack);
        stack.Add(fileCard);

        // ---- step 2: preview ----
        var previewCard = Theme.Card();
        var previewStack = new StackPanel(10);
        previewStack.Add(new Label
        {
            Text = "2.  Check what will be imported",
            Font = Theme.H3,
            ForeColor = Theme.Text,
            AutoSize = true,
        });
        _status.Text = "No database selected yet.";
        previewStack.Add(_status);

        Theme.StyleGrid(_employeeGrid);
        _employeeGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Employee", FillWeight = 180 });
        _employeeGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "PIN", FillWeight = 70 });
        _employeeGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Badge", FillWeight = 70 });
        _employeeGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Punches", FillWeight = 70 });
        _employeeGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "First punch", FillWeight = 120 });
        _employeeGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Last punch", FillWeight = 120 });
        _employeeGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "First day (direction check)", FillWeight = 300 });
        _employeeGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Notes", FillWeight = 200 });
        _employeeGrid.Height = 240;
        _employeeGrid.Dock = DockStyle.Top;

        previewStack.Add(_employeeGrid);
        previewCard.Controls.Add(previewStack);
        stack.Add(previewCard);

        // ---- step 3: options ----
        var optionsCard = Theme.Card();
        var optionsStack = new StackPanel(6);
        optionsStack.Add(new Label
        {
            Text = "3.  Choose what to bring across",
            Font = Theme.H3,
            ForeColor = Theme.Text,
            AutoSize = true,
        });

        _doEmployees.Checked = true;
        _doSettings.Checked = true;
        optionsStack.Add(new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false,
            Controls = { _doEmployees, _doSettings, _doMisc, _doApprovals, _deactivateStale },
        });

        var directionRow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
        directionRow.Controls.Add(new Label { Text = "Read IN/OUT from:", AutoSize = true, Margin = new Padding(0, 12, 8, 0) });
        _direction.Items.AddRange(new object[]
        {
            "Detect automatically",
            "Alternate IN, OUT, IN, OUT",
            "Use stored state codes (0 = IN, 1 = OUT)",
        });
        _direction.SelectedIndex = 0;
        _direction.Margin = new Padding(0, 8, 0, 0);
        directionRow.Controls.Add(_direction);
        optionsStack.Add(directionRow);

        var dateRow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(0, 8, 0, 0) };
        dateRow.Controls.Add(_useDates);
        _from.Enabled = false;
        _to.Enabled = false;
        _useDates.CheckedChanged += (_, _) => { _from.Enabled = _to.Enabled = _useDates.Checked; };
        _from.Value = DateTime.Today.AddYears(-2);
        _to.Value = DateTime.Today;
        dateRow.Controls.Add(_from);
        dateRow.Controls.Add(new Label { Text = "to", AutoSize = true, Margin = new Padding(6, 6, 6, 0) });
        dateRow.Controls.Add(_to);
        optionsStack.Add(dateRow);

        optionsCard.Controls.Add(optionsStack);
        stack.Add(optionsCard);

        // ---- step 4: run ----
        var runCard = Theme.Card();
        var runStack = new StackPanel(8);
        runStack.Add(new Label
        {
            Text = "4.  Import",
            Font = Theme.H3,
            ForeColor = Theme.Text,
            AutoSize = true,
        });
        runStack.Add(new Label
        {
            Text = "A backup of the current PunchPoint database is taken before anything is written. " +
                   "Importing again later only adds punches that are not already there.",
            Font = Theme.Small,
            ForeColor = Theme.TextMuted,
            AutoSize = true,
            MaximumSize = new Size(820, 0),
        });

        var runRow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
        _importButton.Enabled = false;
        _importButton.Click += async (_, _) => await RunImportAsync();
        runRow.Controls.Add(_importButton);
        _progress.Margin = new Padding(14, 16, 0, 0);
        _progress.Width = 240;
        runRow.Controls.Add(_progress);
        runStack.Add(runRow);
        runStack.Add(_log);

        runCard.Controls.Add(runStack);
        stack.Add(runCard);

        stack.Add(BuildCsvCard());
    }

    private Panel BuildCsvCard()
    {
        var card = Theme.Card();
        var csvStack = new StackPanel(8);
        csvStack.Add(new Label
        {
            Text = "Only have a spreadsheet or text export?",
            Font = Theme.H3,
            ForeColor = Theme.Text,
            AutoSize = true,
        });
        csvStack.Add(new Label
        {
            Text = "Import punches from a CSV or tab-separated file. The file needs a header row with an " +
                   "employee column (Badge, PIN, Name), a date/time column, and optionally an IN/OUT column " +
                   "and a comment column. Without an IN/OUT column, punches alternate.",
            Font = Theme.Small,
            ForeColor = Theme.TextMuted,
            AutoSize = true,
            MaximumSize = new Size(820, 0),
        });

        var row = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
        var choose = Theme.SecondaryButton("Choose CSV…", 130);
        choose.Click += (_, _) => ChooseCsv();
        _csvImportButton.Enabled = false;
        _csvImportButton.Click += async (_, _) => await RunCsvImportAsync();
        row.Controls.Add(choose);
        row.Controls.Add(_csvImportButton);
        csvStack.Add(row);

        csvStack.Add(_csvCreateMissing);
        csvStack.Add(_csvSummary);

        card.Controls.Add(csvStack);
        return card;
    }

    private void ChooseCsv()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose a punch file",
            Filter = "Delimited text (*.csv;*.txt;*.tsv)|*.csv;*.txt;*.tsv|All files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            _csvPath = dialog.FileName;
            var preview = new CsvPunchImporter(_csvPath).Preview();

            _csvSummary.Text =
                $"{Path.GetFileName(_csvPath)}: {preview.ParsedRows} readable rows, " +
                $"{preview.Employees.Count} employee value(s)" +
                (preview.FirstPunch is not null ? $", {preview.FirstPunch:MMM d, yyyy} to {preview.LastPunch:MMM d, yyyy}" : "") +
                $"\nEmployee column: {preview.EmployeeColumn} · date column: {preview.TimestampColumn}" +
                (preview.DirectionColumn.Length > 0 ? $" · direction column: {preview.DirectionColumn}" : " · direction alternates") +
                (preview.Warnings.Count > 0 ? "\n" + string.Join(" ", preview.Warnings) : "");

            _csvSummary.ForeColor = preview.LooksUsable ? Theme.TextMuted : Theme.Danger;
            _csvImportButton.Enabled = preview.LooksUsable;
        }
        catch (Exception ex)
        {
            _csvPath = null;
            _csvImportButton.Enabled = false;
            _csvSummary.Text = "Could not read that file.";
            _csvSummary.ForeColor = Theme.Danger;
            PromptDialog.Warn(this, ex.Message);
        }
    }

    private async Task RunCsvImportAsync()
    {
        if (_csvPath is null) return;

        if (!PromptDialog.Confirm(this,
                "Import punches from this file?\n\nA backup of the current database is created first."))
            return;

        string backup;
        try
        {
            backup = Services.Store.Db.Backup();
        }
        catch (Exception ex)
        {
            PromptDialog.Warn(this, "Could not create a backup, so the import was stopped.\n\n" + ex.Message);
            return;
        }

        _csvImportButton.Enabled = false;
        _progress.Visible = true;
        _csvSummary.Text = $"Backup saved to {backup}";

        string dbPath = Services.Store.Db.Path;
        string csvPath = _csvPath;
        bool createMissing = _csvCreateMissing.Checked;

        try
        {
            var progress = new Progress<string>(m => _csvSummary.Text = m);

            var result = await Task.Run(() =>
            {
                using var database = Database.Open(dbPath);
                var store = new Store(database);
                return new CsvPunchImporter(csvPath).Import(store,
                    new CsvImportOptions { CreateMissingEmployees = createMissing },
                    (done, total, message) => ((IProgress<string>)progress).Report(message));
            });

            Services.NotifyDataChanged();

            _csvSummary.Text =
                $"Imported {result.PunchesAdded} punches" +
                (result.EmployeesAdded > 0 ? $", {result.EmployeesAdded} new employees" : "") +
                (result.UnmatchedPunches > 0 ? $", {result.UnmatchedPunches} unmatched" : "") +
                $". Backup: {backup}";

            PromptDialog.Info(this,
                "CSV import finished.\n\n" +
                $"Punches added: {result.PunchesAdded}\n" +
                $"Employees created: {result.EmployeesAdded}\n" +
                $"Unmatched rows: {result.UnmatchedPunches}" +
                (result.Warnings.Count > 0 ? "\n\n" + string.Join("\n", result.Warnings) : ""));
        }
        catch (Exception ex)
        {
            _csvSummary.Text = "Import failed: " + ex.Message;
            PromptDialog.Warn(this, "The CSV import did not finish.\n\n" + ex.Message);
        }
        finally
        {
            _progress.Visible = false;
            _csvImportButton.Enabled = true;
        }
    }

    public override void RefreshData()
    {
        if (_workingPath is null)
            AutoDetect(quiet: true);
    }

    private static readonly string[] KnownPaths =
    {
        @"C:\ProgramData\Acroprint\timeQplusV3\Data\arx_active.mdb",
        @"C:\ProgramData\Acroprint\timeQplus\Data\arx_active.mdb",
        @"C:\ProgramData\Acroprint\Attendance Rx\Data\arx_active.mdb",
        @"C:\Program Files\Acroprint\timeQplusV3\Data\arx_active.mdb",
        @"C:\Program Files (x86)\Acroprint\timeQplusV3\Data\arx_active.mdb",
        @"C:\Program Files (x86)\Acroprint\timeQplus\Data\arx_active.mdb",
    };

    private void AutoDetect(bool quiet = false)
    {
        foreach (var candidate in KnownPaths)
        {
            if (File.Exists(candidate))
            {
                LoadFile(candidate);
                return;
            }
        }

        // Fall back to a search of ProgramData for the database name.
        try
        {
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            foreach (var file in Directory.EnumerateFiles(programData, "arx_active.mdb", SearchOption.AllDirectories))
            {
                LoadFile(file);
                return;
            }
        }
        catch
        {
            // permission problems are fine; the user can browse
        }

        if (!quiet)
            PromptDialog.Info(this,
                "I could not find arx_active.mdb automatically.\n\n" +
                "Click “Choose database…” and pick the Data folder inside the old Acroprint/timeQplus folder. " +
                "If the old computer is a different machine, copy the whole timeQplus folder to this PC first.");
    }

    private void ChooseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose the timeQplusV3 data folder (the one containing arx_active.mdb and Archives)",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            Cursor = Cursors.WaitCursor;
            var files = TimeQPlusFolderImporter.DiscoverDatabases(dialog.SelectedPath);
            if (files.Count == 0)
            {
                PromptDialog.Warn(this, "No arx_active.mdb or Archives\\*.mdb files were found in that folder.");
                return;
            }

            _batchFiles = files;
            _workingPath = null;
            _path.Text = $"{dialog.SelectedPath}  —  {files.Count} database file(s)";

            var batch = TimeQPlusFolderImporter.Preview(files);
            var active = batch.Files.LastOrDefault(f => Path.GetFileName(f.File)
                .StartsWith("arx_active", StringComparison.OrdinalIgnoreCase));

            _status.Text =
                $"{files.Count} weekly snapshots + live database · {batch.TransactionCount:n0} punches" +
                (batch.FirstPunch is not null
                    ? $" · {batch.FirstPunch:MMM d, yyyy} to {batch.LastPunch:MMM d, yyyy}"
                    : "") +
                $"\n{batch.EmployeeCount} employees · {batch.Warnings.Count} warning(s)";

            var sample = active.Preview ?? batch.Files.Last().Preview;
            FillEmployeeGrid(sample);
            _log.Text = batch.Warnings.Count == 0
                ? ""
                : "Notes:\n• " + string.Join("\n• ", batch.Warnings.Take(8));

            _importButton.Enabled = batch.TransactionCount > 0;
            _importButton.Text = "Import all history";
        }
        catch (Exception ex)
        {
            _batchFiles = null;
            _importButton.Enabled = false;
            _importButton.Text = "Start import";
            PromptDialog.Warn(this, ex.Message);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void ChooseFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select arx_active.mdb",
            Filter = "Access database (*.mdb)|*.mdb|All files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            LoadFile(dialog.FileName);
    }

    private void LoadFile(string path)
    {
        try
        {
            Cursor = Cursors.WaitCursor;

            // Work on a copy so a running old app cannot lock the file mid-import.
            if (_workingPath is not null)
            {
                try { File.Delete(_workingPath); } catch { }
            }
            string temp = Path.Combine(Path.GetTempPath(), $"punchpoint-legacy-{Guid.NewGuid():n}.mdb");
            File.Copy(path, temp, overwrite: true);

            _batchFiles = null;
            _workingPath = temp;
            _importButton.Text = "Start import";
            _path.Text = path + "  (read from a temporary copy)";

            var preview = new TimeQPlusImporter(temp).Preview();

            _status.Text =
                $"{preview.EmployeeCount} employees · {preview.TransactionCount} punches" +
                (preview.FirstPunch is not null
                    ? $" · {preview.FirstPunch:MMM d, yyyy} to {preview.LastPunch:MMM d, yyyy}"
                    : "") +
                $"\n{preview.DetectedDirection}";
            _status.ForeColor = preview.Warnings.Any(w => w.Contains("does not look like"))
                ? Theme.Danger
                : Theme.TextMuted;

            FillEmployeeGrid(preview);

            _log.Text = preview.Warnings.Count == 0
                ? ""
                : "Notes:\n• " + string.Join("\n• ", preview.Warnings);

            _doMisc.Checked = preview.MiscHourTransactions > 0 && preview.MiscHourTransactions < Math.Max(1, preview.TransactionCount / 4);

            if (preview.FirstPunch is not null) _from.Value = preview.FirstPunch.Value;
            if (preview.LastPunch is not null) _to.Value = preview.LastPunch.Value;

            _importButton.Enabled = preview.EmployeeCount > 0 && preview.TransactionCount >= 0;
        }
        catch (Exception ex)
        {
            _workingPath = null;
            _importButton.Enabled = false;
            _status.Text = "Could not read that file.";
            _status.ForeColor = Theme.Danger;
            PromptDialog.Warn(this, ex.Message);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void FillEmployeeGrid(ImportPreview preview)
    {
        _employeeGrid.Rows.Clear();
        foreach (var row in preview.Employees.OrderBy(e => e.Employee.SortName))
        {
            _employeeGrid.Rows.Add(
                row.Employee.SortName,
                row.Employee.Pin,
                row.Employee.Badge,
                row.PunchCount,
                row.FirstPunch?.ToString("MMM d, yyyy") ?? "",
                row.LastPunch?.ToString("MMM d, yyyy") ?? "",
                row.FirstDaySummary,
                string.Join("; ", row.Notes));
        }
    }

    private async Task RunImportAsync()
    {
        if (_workingPath is null && _batchFiles is null) return;

        if (!PromptDialog.Confirm(this,
                "Import now?\n\nA backup of the current database will be created first, then employees and " +
                "punches will be added. Existing records are matched by badge/PIN so running this twice is safe."))
            return;

        var options = new ImportOptions
        {
            ImportEmployees = _doEmployees.Checked,
            ImportSettings = _doSettings.Checked,
            ImportMiscHours = _doMisc.Checked,
            ImportApprovals = _doApprovals.Checked,
            InactiveAfterDaysWithoutPunch = _deactivateStale.Checked ? 90 : 0,
            ImportHolidays = true,
            ImportPayPeriods = true,
            ImportShifts = true,
            FromDate = _useDates.Checked ? _from.Value.Date : null,
            ToDate = _useDates.Checked ? _to.Value.Date : null,
            DirectionMode = _direction.SelectedIndex switch
            {
                1 => PunchDirectionMode.Alternate,
                2 => PunchDirectionMode.UseState,
                _ => PunchDirectionMode.Auto,
            },
        };

        string backup = "";
        try
        {
            backup = Services.Store.Db.Backup();
        }
        catch (Exception ex)
        {
            PromptDialog.Warn(this, "Could not create a backup, so the import was stopped.\n\n" + ex.Message);
            return;
        }

        _importButton.Enabled = false;
        _progress.Visible = true;
        _log.Text = $"Backup saved to {backup}";
        Cursor = Cursors.WaitCursor;

        string dbPath = Services.Store.Db.Path;
        string? source = _workingPath;
        List<string>? batchFiles = _batchFiles;

        try
        {
            var progress = new Progress<string>(message => _log.Text = message);

            if (batchFiles is not null)
            {
                var batch = await Task.Run(() =>
                {
                    using var database = Database.Open(dbPath);
                    var store = new Store(database);
                    return TimeQPlusFolderImporter.Import(store, batchFiles, options,
                        (done, total, message) => ((IProgress<string>)progress).Report(message));
                });

                Services.NotifyDataChanged();

                _log.Text =
                    $"Done in {batch.Duration.TotalSeconds:0.0}s — {batch.FilesImported} files, " +
                    $"{batch.EmployeesAdded} employees added, {batch.PunchesAdded:n0} punches" +
                    (batch.PunchesSkipped > 0 ? $", {batch.PunchesSkipped:n0} already present" : "") +
                    (batch.UnmatchedPunches > 0 ? $", {batch.UnmatchedPunches:n0} unmatched" : "") + "\n" +
                    $"Backup: {backup}";

                PromptDialog.Info(this,
                    "History import finished.\n\n" +
                    $"Files: {batch.FilesImported} of {batchFiles.Count}\n" +
                    $"Employees added: {batch.EmployeesAdded}\n" +
                    $"Punches: {batch.PunchesAdded:n0}\n" +
                    $"Skipped (already there): {batch.PunchesSkipped:n0}\n" +
                    $"Unmatched punches: {batch.UnmatchedPunches:n0}\n\n" +
                    "Open Time Cards to review the hours. Use the Settings → Show inactive option to " +
                    "see former employees.");
                return;
            }

            var result = await Task.Run(() =>
            {
                using var database = Database.Open(dbPath);
                var store = new Store(database);
                return new TimeQPlusImporter(source!).Import(store, options,
                    (done, total, message) => ((IProgress<string>)progress).Report(message));
            });

            Services.NotifyDataChanged();

            _log.Text =
                $"Done in {result.Duration.TotalSeconds:0.0}s — {result.EmployeesAdded} employees added, " +
                $"{result.EmployeesUpdated} updated, {result.PunchesAdded} punches added" +
                (result.PunchesSkipped > 0 ? $", {result.PunchesSkipped} already present" : "") +
                (result.UnmatchedPunches > 0 ? $", {result.UnmatchedPunches} unmatched" : "") + "\n" +
                $"Backup: {backup}";

            PromptDialog.Info(this,
                "Import finished.\n\n" +
                $"Employees added: {result.EmployeesAdded}\n" +
                $"Employees updated: {result.EmployeesUpdated}\n" +
                $"Shifts: {result.ShiftsAdded}\n" +
                $"Pay periods: {result.PayPeriodsAdded}\n" +
                $"Punches: {result.PunchesAdded}\n" +
                $"Skipped (already there): {result.PunchesSkipped}\n" +
                $"Unmatched punches: {result.UnmatchedPunches}\n\n" +
                "Open Time Cards to review the hours, and check the punch issues shown in orange.");
        }
        catch (Exception ex)
        {
            _log.Text = "Import failed: " + ex.Message;
            PromptDialog.Warn(this, "The import did not finish.\n\n" + ex.Message +
                                    "\n\nYour previous data is intact and a backup was saved to:\n" + backup);
        }
        finally
        {
            _progress.Visible = false;
            _importButton.Enabled = true;
            Cursor = Cursors.Default;
            try { if (_workingPath is not null) File.Delete(_workingPath); } catch { }
            _workingPath = null;
        }
    }
}

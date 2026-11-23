using PunchPoint.App.UI;
using PunchPoint.Core.Models;

namespace PunchPoint.App.UI.Screens;

public sealed class PunchesScreen : ScreenBase
{
    private readonly DataGridView _grid = new();
    private readonly DateTimePicker _from = new() { Format = DateTimePickerFormat.Short, Width = 130, Font = Theme.Body };
    private readonly DateTimePicker _to = new() { Format = DateTimePickerFormat.Short, Width = 130, Font = Theme.Body };
    private readonly ComboBox _employeeFilter = Theme.Dropdown(220);
    private List<Employee> _employees = new();

    public PunchesScreen(AppServices services)
        : base(services, "Punches", "Every clock event, newest first")
    {
        _from.Margin = new Padding(0, 8, 6, 0);
        _to.Margin = new Padding(0, 8, 6, 0);
        _employeeFilter.Margin = new Padding(0, 8, 6, 0);

        _from.ValueChanged += (_, _) => RefreshData();
        _to.ValueChanged += (_, _) => RefreshData();
        _employeeFilter.SelectedIndexChanged += (_, _) => RefreshData();

        Actions.Controls.Add(new Label { Text = "From", AutoSize = true, Margin = new Padding(0, 14, 6, 0) });
        Actions.Controls.Add(_from);
        Actions.Controls.Add(new Label { Text = "to", AutoSize = true, Margin = new Padding(0, 14, 6, 0) });
        Actions.Controls.Add(_to);
        Actions.Controls.Add(_employeeFilter);

        var add = Theme.PrimaryButton("Add punch", 120);
        add.Margin = new Padding(8, 5, 0, 0);
        add.Click += (_, _) => AddPunch();
        Actions.Controls.Add(add);

        Theme.StyleGrid(_grid);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Date", FillWeight = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Day", FillWeight = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Time", FillWeight = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Employee", FillWeight = 190 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Punch", FillWeight = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Source", FillWeight = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Comment", FillWeight = 240 });

        var card = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(16, 12, 16, 12) };
        card.Controls.Add(_grid);
        Body.Controls.Add(card);

        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelected(); };
        _grid.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) DeleteSelected(); };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Edit punch…", null, (_, _) => EditSelected());
        menu.Items.Add("Delete punch…", null, (_, _) => DeleteSelected());
        _grid.ContextMenuStrip = menu;

        var current = services.Store.GetCurrentPayPeriod();
        _from.Value = current?.Start ?? DateTime.Today.AddDays(-14);
        _to.Value = current?.End ?? DateTime.Today;
    }

    public override void RefreshData()
    {
        _employees = Services.Store.ListEmployees(includeInactive: true);

        string? selected = _employeeFilter.SelectedItem?.ToString();
        _employeeFilter.Items.Clear();
        _employeeFilter.Items.Add("All employees");
        foreach (var employee in _employees.OrderBy(e => e.SortName))
            _employeeFilter.Items.Add(employee.SortName);
        _employeeFilter.SelectedIndex = Math.Max(0,
            selected is null ? 0 : Math.Max(0, _employeeFilter.Items.IndexOf(selected)));

        var byId = _employees.ToDictionary(e => e.Id);
        int? filterId = _employeeFilter.SelectedIndex > 0 && _employeeFilter.SelectedIndex - 1 < _employees.Count
            ? _employees.OrderBy(e => e.SortName).ElementAt(_employeeFilter.SelectedIndex - 1).Id
            : null;

        var punches = Services.Store
            .ListPunches(_from.Value.Date, _to.Value.Date.AddDays(1))
            .Where(p => filterId is null || p.EmployeeId == filterId)
            .OrderByDescending(p => p.Timestamp)
            .ToList();

        _grid.Rows.Clear();
        foreach (var punch in punches)
        {
            var employee = byId.GetValueOrDefault(punch.EmployeeId);
            int index = _grid.Rows.Add(
                punch.Timestamp.ToString("yyyy-MM-dd"),
                punch.Timestamp.ToString("ddd"),
                punch.Timestamp.ToString("h:mm:ss tt"),
                employee?.SortName ?? "—",
                KindLabel(punch.Kind),
                punch.Source.ToString(),
                punch.Comment);

            _grid.Rows[index].Tag = punch;
            if (punch.Kind == PunchKind.Out)
                _grid.Rows[index].Cells[4].Style.ForeColor = Theme.TextMuted;
            if (punch.Source == PunchSource.Import)
                _grid.Rows[index].Cells[5].Style.ForeColor = Theme.TextFaint;
        }

        if (punches.Count == 0)
            _grid.Rows.Add("", "", "", "No punches in this range", "", "", "");
    }

    private static string KindLabel(PunchKind kind) => kind switch
    {
        PunchKind.In => "IN",
        PunchKind.Out => "OUT",
        PunchKind.BreakOut => "Break start",
        PunchKind.BreakIn => "Break end",
        PunchKind.Hours => "Paid hours",
        _ => kind.ToString(),
    };

    private Punch? Selected =>
        _grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0].Tag as Punch : null;

    private void AddPunch()
    {
        var employee = _employeeFilter.SelectedIndex > 0 && _employeeFilter.SelectedIndex - 1 < _employees.Count
            ? _employees.OrderBy(e => e.SortName).ElementAt(_employeeFilter.SelectedIndex - 1)
            : null;

        if (employee is null)
        {
            var names = _employees.Where(e => e.IsActive).OrderBy(e => e.SortName).ToList();
            using var picker = new EmployeePickerDialog(Services, names);
            if (picker.ShowDialog(this) != DialogResult.OK || picker.Selected is null) return;
            employee = picker.Selected;
        }

        using var dialog = new PunchEditDialog(Services, employee, null);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            Services.NotifyDataChanged();
            RefreshData();
        }
    }

    private void EditSelected()
    {
        var punch = Selected;
        if (punch is null) return;

        var employee = Services.Store.GetEmployee(punch.EmployeeId);
        if (employee is null) return;

        using var dialog = new PunchEditDialog(Services, employee, punch);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            Services.NotifyDataChanged();
            RefreshData();
        }
    }

    private void DeleteSelected()
    {
        var punch = Selected;
        if (punch is null) return;

        var employee = Services.Store.GetEmployee(punch.EmployeeId);
        if (!PromptDialog.Confirm(this,
                $"Delete the {KindLabel(punch.Kind)} punch for {employee?.DisplayName} at {punch.Timestamp:g}?"))
            return;

        Services.Store.DeletePunch(punch.Id);
        Services.Store.Audit("Punch deleted", $"{employee?.DisplayName} {punch.Timestamp:g}");
        Services.NotifyDataChanged();
        RefreshData();
    }
}

/// <summary>Small modal list for choosing an employee.</summary>
public sealed class EmployeePickerDialog : Form
{
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, Font = Theme.Body, BorderStyle = BorderStyle.None };
    public Employee? Selected => _list.SelectedItem as Employee;

    public EmployeePickerDialog(AppServices services, IEnumerable<Employee> employees)
    {
        Text = "Choose an employee";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(420, 520);
        BackColor = Theme.Surface;
        Font = Theme.Body;
        Padding = new Padding(18);

        foreach (var employee in employees)
            _list.Items.Add(employee);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 50 };
        var ok = Theme.PrimaryButton("Select", 110);
        ok.DialogResult = DialogResult.OK;
        var cancel = Theme.SecondaryButton("Cancel", 100);
        cancel.DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        Controls.Add(_list);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;

        _list.DoubleClick += (_, _) => { if (Selected is not null) { DialogResult = DialogResult.OK; Close(); } };
    }
}

using PunchPoint.App.UI;
using PunchPoint.Core.Models;

namespace PunchPoint.App.UI.Screens;

public sealed class EmployeesScreen : ScreenBase
{
    private readonly DataGridView _grid = new();
    private readonly TextBox _search = Theme.Input(220);
    private readonly CheckBox _showInactive = Theme.Check("Show inactive");

    public EmployeesScreen(AppServices services)
        : base(services, "Employees", "People who clock in and out")
    {
        _showInactive.Margin = new Padding(8, 12, 8, 0);
        _showInactive.CheckedChanged += (_, _) => RefreshData();
        Actions.Controls.Add(_showInactive);

        _search.PlaceholderText = "Search name, PIN or badge";
        _search.Margin = new Padding(8, 8, 8, 0);
        _search.TextChanged += (_, _) => RefreshData();
        Actions.Controls.Add(_search);

        var add = Theme.PrimaryButton("Add employee", 130);
        add.Margin = new Padding(8, 5, 0, 0);
        add.Click += (_, _) => AddEmployee();
        Actions.Controls.Add(add);

        Theme.StyleGrid(_grid);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Employee", FillWeight = 200 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "PIN", FillWeight = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Badge", FillWeight = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Number", FillWeight = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Shift", FillWeight = 130 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Pay type", FillWeight = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", FillWeight = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "On clock", FillWeight = 80 });

        var card = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(18, 14, 18, 14) };
        card.Controls.Add(_grid);
        Body.Controls.Add(card);

        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelected(); };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Edit…", null, (_, _) => EditSelected());
        menu.Items.Add("Punches…", null, (_, _) => OpenPunches());
        menu.Items.Add("Add punch…", null, (_, _) => AddPunch());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Deactivate / reactivate", null, (_, _) => ToggleActive());
        menu.Items.Add("Delete employee…", null, (_, _) => DeleteSelected());
        _grid.ContextMenuStrip = menu;
        _grid.CellMouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
            {
                _grid.ClearSelection();
                _grid.Rows[e.RowIndex].Selected = true;
            }
        };
    }

    private Employee? Selected =>
        _grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Tag is Employee employee ? employee : null;

    public override void RefreshData()
    {
        var employees = Services.Store.ListEmployees(includeInactive: _showInactive.Checked);
        string search = _search.Text.Trim();

        if (search.Length > 0)
        {
            employees = employees.Where(e =>
                e.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.Pin.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.Badge.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.EmployeeNumber.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        _grid.Rows.Clear();
        var shifts = Services.Store.ListShifts().ToDictionary(s => s.Id);

        foreach (var employee in employees)
        {
            int index = _grid.Rows.Add(
                employee.SortName,
                employee.Pin,
                employee.Badge,
                employee.EmployeeNumber,
                employee.ShiftId is int id && shifts.TryGetValue(id, out var shift) ? shift.Name : "—",
                employee.PayType == PayType.Salary ? "Salary" : "Hourly",
                employee.IsActive ? "Active" : "Inactive",
                Services.Clock.GetStatus(employee).IsClockedIn ? "Yes" : "");

            var row = _grid.Rows[index];
            row.Tag = employee;
            row.Cells[6].Style.ForeColor = employee.IsActive ? Theme.TextMuted : Theme.Danger;
            if (Services.Clock.GetStatus(employee).IsClockedIn)
                row.Cells[7].Style.ForeColor = Theme.Success;
        }

        if (employees.Count == 0)
        {
            _grid.Rows.Add(search.Length > 0 ? "No matching employees" : "No employees yet — use “Add employee”",
                "", "", "", "", "", "", "");
        }
    }

    private void AddEmployee()
    {
        using var dialog = new EmployeeEditDialog(Services, null);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            Services.NotifyDataChanged();
            RefreshData();
        }
    }

    private void EditSelected()
    {
        var employee = Selected;
        if (employee is null) return;

        using var dialog = new EmployeeEditDialog(Services, employee);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            Services.NotifyDataChanged();
            RefreshData();
        }
    }

    private void ToggleActive()
    {
        var employee = Selected;
        if (employee is null) return;

        employee.Status = employee.IsActive ? EmployeeStatus.Inactive : EmployeeStatus.Active;
        Services.Store.UpdateEmployee(employee);
        Services.Store.Audit("Employee status changed",
            $"{employee.DisplayName} -> {employee.Status}");
        Services.NotifyDataChanged();
        RefreshData();
    }

    private void DeleteSelected()
    {
        var employee = Selected;
        if (employee is null) return;

        if (!PromptDialog.Confirm(this,
                $"Delete {employee.DisplayName} and all of their punches?\n\n" +
                "This cannot be undone. If the employee has left, deactivate them instead so their " +
                "history stays available for payroll."))
            return;

        Services.Store.DeleteEmployee(employee.Id);
        Services.Store.Audit("Employee deleted", employee.DisplayName);
        Services.NotifyDataChanged();
        RefreshData();
    }

    private void OpenPunches()
    {
        var employee = Selected;
        if (employee is null) return;
        (FindForm() as MainForm)?.Show("punches");
    }

    private void AddPunch()
    {
        var employee = Selected;
        if (employee is null) return;

        using var dialog = new PunchEditDialog(Services, employee, null);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            Services.NotifyDataChanged();
            RefreshData();
        }
    }
}

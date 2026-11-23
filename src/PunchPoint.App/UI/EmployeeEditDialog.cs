using PunchPoint.Core.Models;

namespace PunchPoint.App.UI;

public sealed class EmployeeEditDialog : EditorDialog
{
    private readonly AppServices _services;
    private readonly Employee _employee;
    private readonly bool _isNew;

    private readonly TextBox _first = Theme.Input(240);
    private readonly TextBox _middle = Theme.Input(80);
    private readonly TextBox _last = Theme.Input(240);
    private readonly TextBox _pin = Theme.Input(140);
    private readonly TextBox _badge = Theme.Input(140);
    private readonly TextBox _number = Theme.Input(140);
    private readonly TextBox _email = Theme.Input(360);
    private readonly TextBox _message = Theme.Input(360);
    private readonly TextBox _notes = Theme.Input(360);
    private readonly TextBox _rate = Theme.Input(120);
    private readonly ComboBox _shift = Theme.Dropdown(240);
    private readonly ComboBox _payType = Theme.Dropdown(160);
    private readonly CheckBox _active = Theme.Check("Active employee (can clock in)");
    private readonly DateTimePicker _hireDate = new()
    {
        Format = DateTimePickerFormat.Short,
        ShowCheckBox = true,
        Width = 160,
        Font = Theme.Body,
    };

    public EmployeeEditDialog(AppServices services, Employee? employee)
        : base(employee is null ? "Add employee" : "Edit employee",
            employee is null ? "Create a profile so this person can clock in" : "Update this employee’s details",
            700, 760)
    {
        _services = services;
        _isNew = employee is null;
        _employee = employee ?? new Employee();

        var grid = FormGrid();
        var stack = new StackPanel(0);
        stack.Add(grid);
        Body.Controls.Add(stack);

        var nameRow = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0) };
        nameRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        nameRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _middle.Dock = DockStyle.Fill;
        _last.Dock = DockStyle.Fill;
        nameRow.Controls.Add(_middle, 0, 0);
        nameRow.Controls.Add(_last, 1, 0);

        AddRow(grid, "First name", _first);
        AddRow(grid, "Middle / last", nameRow);
        AddRow(grid, "PIN", _pin);
        AddRow(grid, "Badge", _badge);
        AddRow(grid, "Employee #", _number);
        AddRow(grid, "Shift", _shift);
        AddRow(grid, "Pay type", _payType);
        AddRow(grid, "Pay rate", _rate);
        AddRow(grid, "Hire date", _hireDate);
        AddRow(grid, "Email (optional)", _email);
        AddRow(grid, "Clock message", _message);
        AddRow(grid, "Notes", _notes);
        AddRow(grid, "", _active);

        stack.Add(new Label
        {
            Text = "The PIN is what the employee types on the clock screen. A badge number works too if " +
                   "you have a badge reader.",
            Font = Theme.Small,
            ForeColor = Theme.TextMuted,
            AutoSize = true,
            MaximumSize = new Size(500, 0),
            Padding = new Padding(0, 14, 0, 0),
        });

        _payType.Items.AddRange(new object[] { "Hourly", "Salary" });
        _shift.Items.Add("(no shift)");
        foreach (var shift in _services.Store.ListShifts())
            _shift.Items.Add(shift.Name);

        LoadEmployee();
    }

    private void LoadEmployee()
    {
        _first.Text = _employee.FirstName;
        _middle.Text = _employee.MiddleInitial;
        _last.Text = _employee.LastName;
        _pin.Text = _employee.Pin;
        _badge.Text = _employee.Badge;
        _number.Text = _employee.EmployeeNumber;
        _email.Text = _employee.Email;
        _message.Text = _employee.Message;
        _notes.Text = _employee.Notes;
        _rate.Text = _employee.PayRate?.ToString("0.00") ?? "";
        _payType.SelectedIndex = _employee.PayType == PayType.Salary ? 1 : 0;
        _active.Checked = _employee.IsActive;

        _hireDate.Checked = _employee.HireDate is not null;
        if (_employee.HireDate is not null) _hireDate.Value = _employee.HireDate.Value;

        var shifts = _services.Store.ListShifts();
        int index = _employee.ShiftId is int id ? shifts.FindIndex(s => s.Id == id) : -1;
        _shift.SelectedIndex = index >= 0 ? index + 1 : 0;

        if (_isNew && string.IsNullOrWhiteSpace(_pin.Text))
            _pin.Text = SuggestPin();
    }

    private string SuggestPin()
    {
        var taken = _services.Store.ListEmployees(includeInactive: true)
            .Select(e => e.Pin).Where(p => p.Length > 0).ToHashSet();

        int start = _services.Store.ListEmployees(includeInactive: true).Count + 1001;
        for (int candidate = start; candidate < start + 2000; candidate++)
            if (!taken.Contains(candidate.ToString()))
                return candidate.ToString();

        return "1001";
    }

    protected override void OnSave()
    {
        string pin = _pin.Text.Trim();
        string badge = _badge.Text.Trim();

        if (_first.Text.Trim().Length == 0 && _last.Text.Trim().Length == 0)
        {
            PromptDialog.Warn(this, "Enter at least a first or last name.");
            _first.Focus();
            return;
        }

        if (pin.Length == 0 && badge.Length == 0)
        {
            PromptDialog.Warn(this, "Give the employee a PIN (or a badge number) so they can clock in.");
            _pin.Focus();
            return;
        }

        // PINs must be unique: they are how the clock screen identifies people.
        foreach (var other in _services.Store.ListEmployees(includeInactive: true))
        {
            if (other.Id == _employee.Id) continue;

            if (pin.Length > 0 && string.Equals(other.Pin, pin, StringComparison.OrdinalIgnoreCase))
            {
                PromptDialog.Warn(this, $"PIN {pin} is already used by {other.DisplayName}.");
                _pin.Focus();
                return;
            }

            if (badge.Length > 0 && string.Equals(other.Badge, badge, StringComparison.OrdinalIgnoreCase))
            {
                PromptDialog.Warn(this, $"Badge {badge} is already used by {other.DisplayName}.");
                _badge.Focus();
                return;
            }
        }

        _employee.FirstName = _first.Text.Trim();
        _employee.MiddleInitial = _middle.Text.Trim();
        _employee.LastName = _last.Text.Trim();
        _employee.Pin = pin;
        _employee.Badge = badge;
        _employee.EmployeeNumber = _number.Text.Trim();
        _employee.Email = _email.Text.Trim();
        _employee.Message = _message.Text.Trim();
        _employee.Notes = _notes.Text.Trim();
        _employee.PayType = _payType.SelectedIndex == 1 ? PayType.Salary : PayType.Hourly;
        _employee.PayRate = decimal.TryParse(_rate.Text, out decimal rate) ? rate : null;
        _employee.Status = _active.Checked ? EmployeeStatus.Active : EmployeeStatus.Inactive;
        _employee.HireDate = _hireDate.Checked ? _hireDate.Value.Date : null;

        var shifts = _services.Store.ListShifts();
        _employee.ShiftId = _shift.SelectedIndex > 0 && _shift.SelectedIndex - 1 < shifts.Count
            ? shifts[_shift.SelectedIndex - 1].Id
            : null;

        if (_isNew)
        {
            _services.Store.InsertEmployee(_employee);
            _services.Store.Audit("Employee added", _employee.DisplayName);
        }
        else
        {
            _services.Store.UpdateEmployee(_employee);
            _services.Store.Audit("Employee updated", _employee.DisplayName);
        }

        Saved = true;
        DialogResult = DialogResult.OK;
        Close();
    }
}

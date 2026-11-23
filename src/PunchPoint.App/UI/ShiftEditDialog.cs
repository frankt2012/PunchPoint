using PunchPoint.Core.Models;

namespace PunchPoint.App.UI;

public sealed class ShiftEditDialog : EditorDialog
{
    public Shift Shift { get; }

    private readonly TextBox _name = Theme.Input(260);
    private readonly DateTimePicker _start = TimePicker();
    private readonly DateTimePicker _stop = TimePicker();
    private readonly NumericUpDown _startGrace = Number(0, 240);
    private readonly NumericUpDown _stopGrace = Number(0, 240);
    private readonly CheckBox _dockLate = Theme.Check("Dock a late arrival back to the shift start");
    private readonly CheckBox _dockEarly = Theme.Check("Dock an early departure forward to the shift end");
    private readonly CheckBox _lunch = Theme.Check("Shift includes an unpaid lunch");
    private readonly DateTimePicker _lunchStart = TimePicker();
    private readonly DateTimePicker _lunchEnd = TimePicker();
    private readonly NumericUpDown _lunchAuto = Number(0, 24);
    private readonly NumericUpDown _lunchLength = Number(0, 240);
    private readonly CheckBox _holidays = Theme.Check("Pay holidays for employees on this shift");
    private readonly NumericUpDown _holidayHours = Number(0, 24);
    private readonly Label _lunchHint = new() { AutoSize = true, Font = Theme.Small, ForeColor = Theme.TextMuted };

    public ShiftEditDialog(Shift? existing)
        : base(existing is null ? "Add shift" : "Edit shift",
            "Rules applied to everyone assigned to this shift", 580, 700)
    {
        Shift = existing ?? new Shift { Name = "New shift" };

        var grid = FormGrid();
        var stack = new StackPanel(0);
        stack.Add(grid);
        Body.Controls.Add(stack);

        AddRow(grid, "Shift name", _name);
        AddRow(grid, "Starts", _start);
        AddRow(grid, "Ends", _stop);
        AddRow(grid, "Start grace (minutes)", _startGrace);
        AddRow(grid, "End grace (minutes)", _stopGrace);
        AddRow(grid, "", _dockLate);
        AddRow(grid, "", _dockEarly);
        AddRow(grid, "", _lunch);
        AddRow(grid, "Lunch starts", _lunchStart);
        AddRow(grid, "Lunch ends", _lunchEnd);
        AddRow(grid, "Auto-deduct lunch after (hours, 0 = only if punched)", _lunchAuto);
        AddRow(grid, "Lunch length (minutes)", _lunchLength);
        AddRow(grid, "", _holidays);
        AddRow(grid, "Holiday hours", _holidayHours);

        stack.Add(_lunchHint);
        _lunchHint.Text =
            "If “auto-deduct” is greater than zero, the lunch is subtracted when the shift is longer than that " +
            "many hours and no break punches were recorded.";
        _lunchHint.Padding = new Padding(0, 14, 0, 0);
        _lunchHint.MaximumSize = new Size(520, 0);

        _lunch.CheckedChanged += (_, _) => SyncLunch();
        _name.Text = Shift.Name;
        _start.Value = DateTime.Today.Add(Shift.Start);
        _stop.Value = DateTime.Today.Add(Shift.Stop);
        _startGrace.Value = Math.Clamp(Shift.StartGraceMinutes, 0, 240);
        _stopGrace.Value = Math.Clamp(Shift.StopGraceMinutes, 0, 240);
        _dockLate.Checked = Shift.DockLateArrival;
        _dockEarly.Checked = Shift.DockEarlyLeave;
        _lunch.Checked = Shift.LunchEnabled;
        _lunchStart.Value = DateTime.Today.Add(Shift.LunchStart);
        _lunchEnd.Value = DateTime.Today.Add(Shift.LunchEnd);
        _lunchAuto.Value = Math.Clamp(Shift.LunchAutoAfterMinutes / 60m, 0, 24);
        _lunchLength.Value = Math.Clamp(Shift.LunchDurationMinutes, 0, 240);
        _holidays.Checked = Shift.HolidaysPaid;
        _holidayHours.Value = Math.Clamp(Shift.HolidayHours, 0, 24);

        SyncLunch();
    }

    private static DateTimePicker TimePicker() => new()
    {
        Format = DateTimePickerFormat.Time,
        ShowUpDown = true,
        Width = 130,
        Font = Theme.Body,
    };

    private static NumericUpDown Number(decimal min, decimal max) => new()
    {
        Minimum = min,
        Maximum = max,
        Width = 90,
        Font = Theme.Body,
        TextAlign = HorizontalAlignment.Right,
    };

    private void SyncLunch()
    {
        bool on = _lunch.Checked;
        foreach (var control in new Control[] { _lunchStart, _lunchEnd, _lunchAuto, _lunchLength })
            control.Enabled = on;

        bool paid = _holidays.Checked;
        _holidayHours.Enabled = paid;

        var start = _start.Value.TimeOfDay;
        var stop = _stop.Value.TimeOfDay;
        _lunchHint.Text = stop <= start
            ? "This shift crosses midnight, which is supported."
            : "If “auto-deduct” is greater than zero, the lunch is subtracted when the shift is longer than that " +
              "many hours and no break punches were recorded.";
    }

    protected override void OnSave()
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
        {
            PromptDialog.Warn(this, "Give the shift a name.");
            return;
        }

        Shift.Name = _name.Text.Trim();
        Shift.Start = _start.Value.TimeOfDay;
        Shift.Stop = _stop.Value.TimeOfDay;
        Shift.StartGraceMinutes = (int)_startGrace.Value;
        Shift.StopGraceMinutes = (int)_stopGrace.Value;
        Shift.DockLateArrival = _dockLate.Checked;
        Shift.DockEarlyLeave = _dockEarly.Checked;
        Shift.LunchEnabled = _lunch.Checked;
        Shift.LunchStart = _lunchStart.Value.TimeOfDay;
        Shift.LunchEnd = _lunchEnd.Value.TimeOfDay;
        Shift.LunchAutoAfterMinutes = (int)(_lunchAuto.Value * 60);
        Shift.LunchDurationMinutes = (int)_lunchLength.Value;
        Shift.HolidaysPaid = _holidays.Checked;
        Shift.HolidayHours = (int)_holidayHours.Value;

        Saved = true;
        DialogResult = DialogResult.OK;
        Close();
    }
}

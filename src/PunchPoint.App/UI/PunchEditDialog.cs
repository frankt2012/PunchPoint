using PunchPoint.Core.Models;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.App.UI;

public sealed class PunchEditDialog : EditorDialog
{
    private readonly AppServices _services;
    private readonly Employee _employee;
    private readonly Punch? _punch;

    private readonly DateTimePicker _date = Theme.Date(220, DateTimePickerFormat.Long);
    private readonly DateTimePicker _time = Theme.Date(140, DateTimePickerFormat.Time, timeOnly: true);
    private readonly ComboBox _kind = Theme.Dropdown(200);
    private readonly NumericUpDown _hours = Theme.Number(0, 24, 2, 100, 0.25m);
    private readonly ComboBox _category = Theme.Dropdown(200);
    private readonly TextBox _comment = Theme.Input(340);
    private Label? _hoursLabelRow;
    private Label? _categoryLabelRow;
    private readonly Button _delete = Theme.DangerButton("Delete punch", 140);

    private PunchKind[] _kinds = Array.Empty<PunchKind>();
    private readonly DateTime? _originalTimestamp;

    /// <summary>Punches re-typed so the day alternates in/out, for the caller to report.</summary>
    public List<PunchKindChange> SequenceChanges { get; private set; } = new();

    public PunchEditDialog(AppServices services, Employee employee, Punch? punch)
        : base(punch is null ? "Add punch" : "Edit punch",
            employee.FullName, 540, 520)
    {
        _services = services;
        _employee = employee;
        _punch = punch;
        _originalTimestamp = punch?.Timestamp;

        var grid = FormGrid();
        Body.Controls.Add(grid);

        AddRow(grid, "Employee", new Label
        {
            Text = employee.SortName + $"  (PIN {employee.Pin})",
            Font = Theme.BodyBold,
            AutoSize = true,
        });
        AddRow(grid, "Date", _date);
        AddRow(grid, "Time", _time);
        AddRow(grid, "Type", _kind);
        AddRow(grid, "Hours", _hours);
        AddRow(grid, "Category", _category);
        AddRow(grid, "Comment", _comment);

        _hoursLabelRow = (Label?)grid.GetControlFromPosition(0, 4);
        _categoryLabelRow = (Label?)grid.GetControlFromPosition(0, 5);

        _kinds = new[] { PunchKind.In, PunchKind.Out, PunchKind.BreakOut, PunchKind.BreakIn, PunchKind.Hours };
        foreach (var kind in _kinds)
            _kind.Items.Add(Label(kind));

        _category.Items.AddRange(new object[] { "Vacation", "Holiday", "Sick", "Other" });
        _category.SelectedIndex = 0;

        _kind.SelectedIndexChanged += (_, _) => SyncVisibility();
        _delete.Visible = punch is not null;
        _delete.Click += (_, _) => DeletePunch();
        Buttons.Controls.Add(_delete);

        if (punch is null)
        {
            var now = DateTime.Now;
            _date.Value = now.Date;
            _time.Value = now;
            // Sensible default: the opposite of the employee's current state.
            var status = services.Clock.GetStatus(employee);
            _kind.SelectedIndex = Array.IndexOf(_kinds, status.NextKind);
        }
        else
        {
            _date.Value = punch.Timestamp.Date;
            _time.Value = punch.Timestamp;
            int index = Array.IndexOf(_kinds, punch.Kind);
            _kind.SelectedIndex = index < 0 ? 0 : index;
            _hours.Value = Math.Min(24m, (decimal)punch.Hours.TotalHours);
            _comment.Text = punch.Comment;
            if (punch.HoursCategory.Length > 0)
            {
                int categoryIndex = _category.Items.IndexOf(punch.HoursCategory);
                if (categoryIndex >= 0) _category.SelectedIndex = categoryIndex;
            }
        }

        SyncVisibility();
    }

    private static string Label(PunchKind kind) => kind switch
    {
        PunchKind.In => "Clock IN",
        PunchKind.Out => "Clock OUT",
        PunchKind.BreakOut => "Break start",
        PunchKind.BreakIn => "Break end",
        PunchKind.Hours => "Paid hours (vacation/holiday/sick)",
        _ => kind.ToString(),
    };

    private void SyncVisibility()
    {
        bool misc = (_kind.SelectedIndex >= 0 ? _kinds[_kind.SelectedIndex] : PunchKind.In) == PunchKind.Hours;
        _hours.Visible = misc;
        _category.Visible = misc;
        if (_hoursLabelRow is not null) _hoursLabelRow.Visible = misc;
        if (_categoryLabelRow is not null) _categoryLabelRow.Visible = misc;
    }

    protected override void OnSave()
    {
        if (_kind.SelectedIndex < 0)
        {
            PromptDialog.Warn(this, "Choose a punch type.");
            return;
        }

        var timestamp = _date.Value.Date.Add(_time.Value.TimeOfDay);
        var kind = _kinds[_kind.SelectedIndex];

        if (kind == PunchKind.Hours && _hours.Value <= 0)
        {
            PromptDialog.Warn(this, "Enter the number of paid hours.");
            return;
        }

        if (timestamp > DateTime.Now.AddMinutes(5))
        {
            if (!PromptDialog.Confirm(this, "This punch is in the future. Save it anyway?"))
                return;
        }

        var punch = _punch ?? new Punch { EmployeeId = _employee.Id, Source = PunchSource.Manual };
        punch.Timestamp = timestamp;
        punch.Kind = kind;
        punch.Comment = _comment.Text.Trim();
        punch.Hours = kind == PunchKind.Hours ? TimeSpan.FromHours((double)_hours.Value) : TimeSpan.Zero;
        punch.HoursCategory = kind == PunchKind.Hours ? (_category.SelectedItem?.ToString() ?? "Other") : "";
        punch.PayPeriodId = _services.Store.GetPayPeriodFor(timestamp)?.Id;

        if (_punch is null)
        {
            _services.Store.InsertPunch(punch);
            _services.Store.Audit("Punch added",
                $"{_employee.DisplayName}: {kind} {timestamp:g}");
        }
        else
        {
            _services.Store.UpdatePunch(punch);
            _services.Store.Audit("Punch edited",
                $"{_employee.DisplayName}: {kind} {timestamp:g}");
        }

        // Adding the missing half of a day leaves the other punch with the wrong direction.
        // Straighten the day out now: in, out, in, out.
        SequenceChanges = Normalize(ForDays(timestamp, _originalTimestamp));

        Saved = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    private IEnumerable<DateTime> ForDays(params DateTime?[] moments)
    {
        var seen = new HashSet<DateTime>();
        foreach (var moment in moments)
        {
            if (moment is null) continue;
            var (from, _) = PunchSequence.WorkDay(moment.Value, _services.Settings.DayChangeOffsetMinutes);
            if (seen.Add(from)) yield return moment.Value;
        }
    }

    private List<PunchKindChange> Normalize(IEnumerable<DateTime> moments)
    {
        var changes = new List<PunchKindChange>();
        foreach (var moment in moments)
            changes.AddRange(PunchSequence.NormalizeAround(
                _services.Store, _employee, moment, _services.Settings.DayChangeOffsetMinutes));
        return changes;
    }

    private void DeletePunch()
    {
        if (_punch is null) return;
        if (!PromptDialog.Confirm(this, $"Delete this {Label(_punch.Kind).ToLowerInvariant()} punch at {_punch.Timestamp:g}?"))
            return;

        _services.Store.DeletePunch(_punch.Id);
        _services.Store.Audit("Punch deleted", $"{_employee.DisplayName}: {_punch.Kind} {_punch.Timestamp:g}");
        SequenceChanges = Normalize(ForDays(_punch.Timestamp, null));
        Saved = true;
        DialogResult = DialogResult.OK;
        Close();
    }
}

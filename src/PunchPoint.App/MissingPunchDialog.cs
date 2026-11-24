using PunchPoint.App.UI;
using PunchPoint.Core.Models;
using PunchPoint.Core.TimeCalc;

namespace PunchPoint.App;

/// <summary>
/// Lists the punches that look missing for one employee and period, proposes a time for each,
/// and adds the ones the supervisor ticks.
/// </summary>
public sealed class MissingPunchDialog : Form
{
    private const int FixColumn = 0;

    private readonly DataGridView _grid = new();
    private readonly List<MissingPunchSuggestion> _items;

    public MissingPunchDialog(AppServices services, Employee employee, PayPeriod period)
    {
        Text = "Fix missing punches";
        Theme.ApplyTitleBar(this);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = true;
        ShowInTaskbar = false;
        BackColor = Theme.Surface;
        Font = Theme.Body;
        ClientSize = new Size(760, 460);
        Padding = new Padding(18);

        var settings = services.Store.GetSettings();
        var shift = employee.ShiftId is int id ? services.Store.GetShift(id) : null;

        _items = MissingPunchFinder.Find(
            employee,
            shift,
            services.Store.ListPunches(employee.Id, period.Start, period.End.AddDays(1)),
            settings.DayChangeOffsetMinutes,
            period.Start,
            period.End.AddDays(1));

        var heading = new Label
        {
            Text = $"Fix missing punches — {employee.SortName}",
            Font = Theme.H2,
            ForeColor = Theme.Text,
            Dock = DockStyle.Top,
            Height = 30,
        };

        var note = new Label
        {
            Text = _items.Count == 0
                ? "Nothing is missing: every clock-in in this period has a clock-out."
                : "Suggested times come from the employee's shift. Untick anything that is wrong, then add the rest.",
            Font = Theme.Small,
            ForeColor = Theme.TextMuted,
            Dock = DockStyle.Top,
            Height = 40,
        };

        Theme.StyleGrid(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = false;

        var fix = new DataGridViewCheckBoxColumn
        {
            HeaderText = "Fix",
            FillWeight = 40,
            ReadOnly = false,
        };
        _grid.Columns.Add(fix);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Day", FillWeight = 110, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Missing", FillWeight = 90, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Suggested", FillWeight = 90, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Why", FillWeight = 300, ReadOnly = true });

        foreach (var item in _items)
        {
            int index = _grid.Rows.Add(
                true,
                item.WorkDate.ToString("ddd MMM d"),
                item.Kind == PunchKind.In ? "Clock in" : "Clock out",
                item.Suggested.ToString("h:mm tt"),
                item.Reason);
            _grid.Rows[index].Tag = item;
        }

        // Everything else stays read-only even though the checkbox column does not.
        foreach (DataGridViewRow row in _grid.Rows)
            if (row.Index >= 0) row.ReadOnly = false;

        _grid.ClearSelection();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 52,
            Padding = new Padding(0, 12, 0, 0),
        };

        var add = Theme.PrimaryButton("Add selected punches", 190);
        add.Enabled = _items.Count > 0;
        add.Click += (_, _) => Accept();

        var cancel = Theme.SecondaryButton("Cancel", 110);
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        buttons.Controls.Add(add);
        buttons.Controls.Add(cancel);

        Controls.Add(_grid);
        Controls.Add(note);
        Controls.Add(heading);
        Controls.Add(buttons);

        AcceptButton = add;
        CancelButton = cancel;
    }

    /// <summary>The suggestions the supervisor left ticked.</summary>
    public List<MissingPunchSuggestion> Accepted { get; } = new();

    private void Accept()
    {
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is not MissingPunchSuggestion item) continue;
            if (row.Cells[FixColumn].Value is not bool ticked || !ticked) continue;
            Accepted.Add(item);
        }

        if (Accepted.Count == 0)
        {
            PromptDialog.Warn(this, "Tick at least one punch to add, or cancel.");
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}

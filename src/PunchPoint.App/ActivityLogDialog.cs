using PunchPoint.App.UI;
using PunchPoint.Core.Storage;

namespace PunchPoint.App;

/// <summary>Read-only view of the audit trail: who changed what, and when.</summary>
public sealed class ActivityLogDialog : Form
{
    private readonly DataGridView _grid = new();

    public ActivityLogDialog(Store store)
    {
        Text = "Activity log";
        Theme.ApplyTitleBar(this);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = true;
        ShowInTaskbar = false;
        BackColor = Theme.Surface;
        Font = Theme.Body;
        ClientSize = new Size(940, 560);
        Padding = new Padding(18);

        var heading = new Label
        {
            Text = "Activity log",
            Font = Theme.H2,
            ForeColor = Theme.Text,
            Dock = DockStyle.Top,
            Height = 30,
        };

        var note = new Label
        {
            Text = "Every change PunchPoint records: imports, edits, approvals, settings and backups. Newest first.",
            Font = Theme.Small,
            ForeColor = Theme.TextMuted,
            Dock = DockStyle.Top,
            Height = 24,
        };

        Theme.StyleGrid(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "When", FillWeight = 150 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "User", FillWeight = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Action", FillWeight = 200 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Detail", FillWeight = 330 });

        foreach (var (when, user, action, detail) in store.ListAudit(500))
        {
            _grid.Rows.Add(
                when == DateTime.MinValue ? "" : when.ToString("MMM d, yyyy h:mm tt"),
                user,
                action,
                detail);
        }

        _grid.ClearSelection();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 50,
            Padding = new Padding(0, 10, 0, 0),
        };
        var close = Theme.PrimaryButton("Close", 110);
        close.Click += (_, _) => Close();
        buttons.Controls.Add(close);

        Controls.Add(_grid);
        Controls.Add(note);
        Controls.Add(heading);
        Controls.Add(buttons);

        AcceptButton = close;
        CancelButton = close;
    }
}

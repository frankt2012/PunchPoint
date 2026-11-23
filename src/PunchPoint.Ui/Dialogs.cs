using System.Globalization;

namespace PunchPoint.App.UI;

public static class PromptDialog
{
    public static string? AskText(IWin32Window? owner, string title, string label, string initial = "")
    {
        var (form, input) = Build(title, label, initial, password: false);
        using (form)
            return form.ShowDialog(owner) == DialogResult.OK ? input.Text.Trim() : null;
    }

    public static string? AskPassword(IWin32Window? owner, string title, string label)
    {
        var (form, input) = Build(title, label, "", password: true);
        using (form)
            return form.ShowDialog(owner) == DialogResult.OK ? input.Text : null;
    }

    public static int? AskInt(IWin32Window? owner, string title, string label, int initial, int min = int.MinValue, int max = int.MaxValue)
    {
        while (true)
        {
            string? text = AskText(owner, title, label, initial.ToString(CultureInfo.InvariantCulture));
            if (text is null) return null;

            if (int.TryParse(text, out int value) && value >= min && value <= max)
                return value;

            Warn(owner, $"Enter a whole number between {min} and {max}.");
        }
    }

    public static void Warn(IWin32Window? owner, string message) =>
        MessageBox.Show(owner, message, "PunchPoint", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static void Info(IWin32Window? owner, string message) =>
        MessageBox.Show(owner, message, "PunchPoint", MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static bool Confirm(IWin32Window? owner, string message) =>
        MessageBox.Show(owner, message, "PunchPoint", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    private static (Form Form, TextBox Input) Build(string title, string label, string initial, bool password)
    {
        var form = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            BackColor = Theme.Surface,
            Font = Theme.Body,
            ClientSize = new Size(430, 232),
            Padding = new Padding(22),
        };

        var heading = new Label
        {
            Text = title,
            Font = Theme.H2,
            ForeColor = Theme.Text,
            Dock = DockStyle.Top,
            Height = 34,
        };

        var description = new Label
        {
            Text = label,
            Dock = DockStyle.Top,
            Height = 52,
            ForeColor = Theme.TextMuted,
            Font = Theme.Small,
        };

        var input = new TextBox
        {
            Text = initial,
            Dock = DockStyle.Top,
            Font = new Font(Theme.Family, 12f),
            UseSystemPasswordChar = password,
            BorderStyle = BorderStyle.FixedSingle,
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48,
            Padding = new Padding(0, 8, 0, 0),
        };

        var ok = Theme.PrimaryButton("OK", 110);
        ok.DialogResult = DialogResult.OK;
        var cancel = Theme.SecondaryButton("Cancel", 100);
        cancel.DialogResult = DialogResult.Cancel;

        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        form.Controls.Add(buttons);
        form.Controls.Add(input);
        form.Controls.Add(description);
        form.Controls.Add(heading);
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        form.Shown += (_, _) => { input.Focus(); input.SelectAll(); };
        return (form, input);
    }
}

/// <summary>Convenience base for modal editor dialogs with a standard header and button bar.</summary>
public class EditorDialog : Form
{
    protected readonly Panel Body;
    protected readonly FlowLayoutPanel Buttons;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Saved { get; protected set; }

    public EditorDialog(string title, string subtitle, int width = 560, int height = 620)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        BackColor = Theme.Surface;
        Font = Theme.Body;
        ClientSize = new Size(width, height);
        Padding = new Padding(24, 20, 24, 16);

        var header = new Panel { Dock = DockStyle.Top, Height = 66 };
        header.Controls.Add(new Label
        {
            Text = title,
            Font = Theme.H2,
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(0, 4),
        });
        header.Controls.Add(new Label
        {
            Text = subtitle,
            Font = Theme.Small,
            ForeColor = Theme.TextMuted,
            AutoSize = true,
            Location = new Point(0, 34),
        });

        Buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 52,
            Padding = new Padding(0, 10, 0, 0),
        };

        var save = Theme.PrimaryButton("Save", 120);
        save.Click += (_, _) => OnSave();
        var cancel = Theme.SecondaryButton("Cancel", 100);
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        Buttons.Controls.Add(save);
        Buttons.Controls.Add(cancel);

        Body = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        Controls.Add(Body);
        Controls.Add(Buttons);
        Controls.Add(header);
        AcceptButton = save;
        CancelButton = cancel;
    }

    protected virtual void OnSave() { }

    protected static Label FieldLabel(string text) => new()
    {
        Text = text,
        Font = Theme.SmallBold,
        ForeColor = Theme.TextMuted,
        AutoSize = true,
        Margin = new Padding(0, 8, 0, 0),
    };

    /// <summary>Builds a two-column form grid from label/control pairs.</summary>
    protected static TableLayoutPanel FormGrid()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Padding = new Padding(0),
            Margin = new Padding(0),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return grid;
    }

    protected static void AddRow(TableLayoutPanel grid, string label, Control control)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var text = FieldLabel(label);
        text.Anchor = AnchorStyles.Left;
        text.Margin = new Padding(0, 12, 8, 4);
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        control.Margin = new Padding(0, 8, 0, 4);
        grid.Controls.Add(text, 0, row);
        grid.Controls.Add(control, 1, row);
    }
}

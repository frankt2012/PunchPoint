using PunchPoint.App.UI;

namespace PunchPoint.Clock;

/// <summary>What a supervisor picked on the clock's Supervisor button.</summary>
internal enum SupervisorAction
{
    None,
    OpenApp,
    CloseClock,
}

/// <summary>
/// Small chooser behind the clock's Supervisor button: open the management app, or close the
/// clock. The window's own close button (and Alt+F4) go straight to closing.
/// </summary>
internal static class SupervisorPrompt
{
    public static SupervisorAction Ask(IWin32Window? owner)
    {
        using var form = new Form
        {
            Text = "Supervisor",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            BackColor = Theme.Surface,
            Font = Theme.Body,
            ClientSize = new Size(500, 214),
            Padding = new Padding(22, 16, 22, 14),
        };
        Theme.ApplyTitleBar(form);

        var stack = new StackPanel(10);
        stack.Add(new Label { Text = "Supervisor", Font = Theme.H2, ForeColor = Theme.Text, AutoSize = true });
        stack.Add(new Label
        {
            Text = "Open the management app to edit employees, time cards and settings. " +
                   "The clock closes while it is open; start it again from PunchPoint when you are done.",
            Font = Theme.Small,
            ForeColor = Theme.TextMuted,
            AutoSize = true,
            MaximumSize = new Size(450, 0),
        });

        var result = SupervisorAction.None;

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 52,
            Padding = new Padding(0, 8, 0, 0),
        };

        var open = Theme.PrimaryButton("Open PunchPoint", 168);
        open.Click += (_, _) =>
        {
            result = SupervisorAction.OpenApp;
            form.Close();
        };

        var close = Theme.SecondaryButton("Close the clock", 150);
        close.Click += (_, _) =>
        {
            result = SupervisorAction.CloseClock;
            form.Close();
        };

        var cancel = Theme.SecondaryButton("Cancel", 92);
        cancel.Click += (_, _) => form.Close();

        buttons.Controls.Add(open);
        buttons.Controls.Add(close);
        buttons.Controls.Add(cancel);

        form.Controls.Add(stack);
        form.Controls.Add(buttons);
        form.AcceptButton = open;
        form.CancelButton = cancel;

        form.ShowDialog(owner);
        return result;
    }
}

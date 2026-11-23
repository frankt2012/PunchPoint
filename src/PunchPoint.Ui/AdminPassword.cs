using PunchPoint.Core.Services;

namespace PunchPoint.App.UI;

/// <summary>Supervisor password prompts for the management app and the clock's exit.</summary>
public static class AdminPassword
{
    /// <summary>Asks for the password. Returns true when it matches.</summary>
    public static bool Unlock(IWin32Window? owner, string? hash, string prompt = "Enter the supervisor password.")
    {
        if (string.IsNullOrWhiteSpace(hash))
            return true; // not protected yet

        int attempts = 0;
        while (attempts < 3)
        {
            using var dialog = Build("Supervisor password", prompt, showConfirm: false);
            if (dialog.ShowDialog(owner) != DialogResult.OK)
                return false;

            if (PasswordHasher.Verify(dialog.Password, hash))
                return true;

            attempts++;
            PromptDialog.Warn(owner, attempts >= 3
                ? "That password is not correct. The app will close."
                : "That password is not correct. Try again.");
        }

        return false;
    }

    /// <summary>Asks for a new password (twice). Returns the hash, or null when cancelled.</summary>
    public static string? Create(IWin32Window? owner, bool allowCancel = true)
    {
        using var dialog = Build("Create a supervisor password",
            "This password protects employees, time cards, imports and settings. Use something only supervisors know.",
            showConfirm: true, allowCancel: allowCancel);

        if (dialog.ShowDialog(owner) != DialogResult.OK)
            return null;

        return PasswordHasher.Hash(dialog.Password);
    }

    /// <summary>Change an existing password: verify the old one, then set a new one.</summary>
    public static string? Change(IWin32Window? owner, string? currentHash)
    {
        if (!string.IsNullOrWhiteSpace(currentHash) && !Unlock(owner, currentHash, "Enter the current password."))
            return null;

        return Create(owner);
    }

    private sealed class PasswordForm : Form
    {
        private readonly TextBox _password = Theme.Input(320);
        private readonly TextBox _confirm = Theme.Input(320);
        private readonly Label _error = new() { AutoSize = true, ForeColor = Theme.Danger, Font = Theme.SmallBold };
        public string Password => _password.Text;
        public bool Confirmed => _confirm.Text == _password.Text;

        public PasswordForm(string title, string prompt, bool showConfirm)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            BackColor = Theme.Surface;
            Font = Theme.Body;
            ClientSize = new Size(470, showConfirm ? 300 : 230);
            Padding = new Padding(24, 20, 24, 16);

            var heading = new Label
            {
                Text = title,
                Font = Theme.H2,
                ForeColor = Theme.Text,
                Dock = DockStyle.Top,
                Height = 36,
            };

            var description = new Label
            {
                Text = prompt,
                Font = Theme.Small,
                ForeColor = Theme.TextMuted,
                Dock = DockStyle.Top,
                Height = showConfirm ? 56 : 40,
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 50,
                Padding = new Padding(0, 8, 0, 0),
            };

            var ok = Theme.PrimaryButton("OK", 110);
            var cancel = Theme.SecondaryButton("Cancel", 100);
            cancel.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);

            _password.UseSystemPasswordChar = true;
            _password.Dock = DockStyle.Top;
            _password.Font = new Font(Theme.Family, 12f);
            _confirm.UseSystemPasswordChar = true;
            _confirm.Dock = DockStyle.Top;
            _confirm.Font = new Font(Theme.Family, 12f);

            // Order matters for docking: add bottom-up.
            var stack = new StackPanel(8);
            stack.Add(description);
            stack.Add(_password);
            if (showConfirm)
            {
                stack.Add(new Label { Text = "Repeat the password", Font = Theme.SmallBold, ForeColor = Theme.TextMuted, AutoSize = true });
                stack.Add(_confirm);
            }
            stack.Add(_error);

            Controls.Add(stack);
            Controls.Add(buttons);
            Controls.Add(heading);
            AcceptButton = ok;
            CancelButton = cancel;

            ok.Click += (_, _) =>
            {
                if (_password.Text.Length < 4)
                {
                    _error.Text = "Use at least 4 characters.";
                    return;
                }
                if (showConfirm && !Confirmed)
                {
                    _error.Text = "The two passwords do not match.";
                    return;
                }
                DialogResult = DialogResult.OK;
                Close();
            };

            Shown += (_, _) => _password.Focus();
        }
    }

    private static PasswordForm Build(string title, string prompt, bool showConfirm, bool allowCancel = true)
    {
        var form = new PasswordForm(title, prompt, showConfirm);
        if (!allowCancel)
        {
            foreach (Control control in AllControls(form))
            {
                if (control is Button { Text: "Cancel" })
                    control.Visible = false;
            }
        }
        return form;
    }

    private static IEnumerable<Control> AllControls(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var nested in AllControls(child))
                yield return nested;
        }
    }
}

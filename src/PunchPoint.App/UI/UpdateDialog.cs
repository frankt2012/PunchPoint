using System.Diagnostics;
using PunchPoint.App.UI;
using PunchPoint.Core.Updates;

namespace PunchPoint.App.UI;

/// <summary>Shown when a newer release exists: release notes, a download link and "skip".</summary>
public sealed class UpdateDialog : Form
{
    public bool SkipRequested { get; private set; }

    public UpdateDialog(UpdateInfo update, Version current)
    {
        Text = "PunchPoint update";
        Theme.ApplyTitleBar(this);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        BackColor = Theme.Surface;
        Font = Theme.Body;
        ClientSize = new Size(620, 540);
        Padding = new Padding(24, 20, 24, 16);

        var heading = new Label
        {
            Text = $"Version {update.Display} is available",
            Font = Theme.H2,
            ForeColor = Theme.Text,
            Dock = DockStyle.Top,
            Height = 34,
        };

        var subtitle = new Label
        {
            Text = $"You are running {current}. Downloading opens the release page in your browser.",
            Font = Theme.Small,
            ForeColor = Theme.TextMuted,
            Dock = DockStyle.Top,
            Height = 26,
        };

        var notes = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = Theme.Body,
            BackColor = Theme.InputBackground,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Text = string.IsNullOrWhiteSpace(update.Notes)
                ? "No notes were published with this release."
                : Trim(update.Notes, 4000),
            WordWrap = true,
        };

        var skip = Theme.Check("Skip this version (do not remind me again)");

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 52,
            Padding = new Padding(0, 10, 0, 0),
        };

        var download = Theme.PrimaryButton("Download", 130);
        download.Click += (_, _) =>
        {
            string url = update.ReleaseUrl;
            if (url.Length > 0)
            {
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
                catch { /* no browser; the URL is in the notes footer */ }
            }
            Close();
        };

        var later = Theme.SecondaryButton("Later", 100);
        later.Click += (_, _) => Close();

        buttons.Controls.Add(download);
        buttons.Controls.Add(later);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 34 };
        footer.Controls.Add(skip);

        Controls.Add(notes);
        Controls.Add(footer);
        Controls.Add(buttons);
        Controls.Add(subtitle);
        Controls.Add(heading);

        AcceptButton = download;
        CancelButton = later;

        FormClosing += (_, _) => SkipRequested = skip.Checked;
    }

    private static string Trim(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";
}

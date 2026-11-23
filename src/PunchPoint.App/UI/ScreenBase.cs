namespace PunchPoint.App.UI;

/// <summary>Common frame for every main screen: title, subtitle, action buttons, scrollable body.</summary>
public abstract class ScreenBase : UserControl, IRefreshable
{
    protected readonly AppServices Services;
    protected readonly Panel Body;
    protected readonly FlowLayoutPanel Actions;
    private readonly Label _title;
    private readonly Label _subtitle;

    protected ScreenBase(AppServices services, string title, string subtitle)
    {
        Services = services;

        Dock = DockStyle.Fill;
        BackColor = Theme.Canvas;
        Font = Theme.Body;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0),
            Margin = new Padding(0),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(28, 22, 28, 0),
            Margin = new Padding(0),
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titles = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
        _title = new Label
        {
            Text = title,
            Font = Theme.H1,
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(0, 0),
        };
        _subtitle = new Label
        {
            Text = subtitle,
            Font = Theme.Body,
            ForeColor = Theme.TextMuted,
            AutoSize = true,
            Location = new Point(2, 40),
        };
        titles.Controls.Add(_title);
        titles.Controls.Add(_subtitle);

        Actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0),
            Padding = new Padding(0, 8, 0, 0),
        };

        header.Controls.Add(titles, 0, 0);
        header.Controls.Add(Actions, 1, 0);

        Body = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(28, 18, 28, 28),
            Margin = new Padding(0),
            BackColor = Theme.Canvas,
        };

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(Body, 0, 1);
        Controls.Add(root);
    }

    protected void SetTitle(string title, string? subtitle = null)
    {
        _title.Text = title;
        if (subtitle is not null) _subtitle.Text = subtitle;
    }

    public virtual void RefreshData() { }
}

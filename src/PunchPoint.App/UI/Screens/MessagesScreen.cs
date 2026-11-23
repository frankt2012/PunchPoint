using PunchPoint.App.UI;
using PunchPoint.Core.Models;

namespace PunchPoint.App.UI.Screens;

/// <summary>
/// Messages, as in the old timeQplus: one company-wide message shown on the clock, plus an
/// optional personal message per employee.
/// </summary>
public sealed class MessagesScreen : ScreenBase
{
    private readonly TextBox _company = Theme.Input(620);
    private readonly DataGridView _grid = new();
    private readonly Label _count = new() { AutoSize = true, Font = Theme.Small, ForeColor = Theme.TextMuted };

    public MessagesScreen(AppServices services)
        : base(services, "Messages", "Shown to employees when they punch in or out")
    {
        var stack = new StackPanel(14);
        Body.Controls.Add(stack);

        var companyCard = Theme.Card();
        var companyStack = new StackPanel(8);
        companyStack.Add(new Label
        {
            Text = "Company message",
            Font = Theme.H3,
            ForeColor = Theme.Text,
            AutoSize = true,
        });
        companyStack.Add(new Label
        {
            Text = "Displayed on the clock screen all day, and under the employee's name after they enter a PIN.",
            Font = Theme.Small,
            ForeColor = Theme.TextMuted,
            AutoSize = true,
            MaximumSize = new Size(760, 0),
        });
        _company.Dock = DockStyle.Top;
        companyStack.Add(_company);

        var save = Theme.PrimaryButton("Save message", 130);
        save.Click += (_, _) => SaveCompanyMessage();
        companyStack.Add(save);
        companyCard.Controls.Add(companyStack);
        stack.Add(companyCard);

        var peopleCard = Theme.Card(0);
        peopleCard.Height = 470;
        peopleCard.AutoSize = false;

        var header = new Panel { Dock = DockStyle.Top, Height = 58, Padding = new Padding(18, 14, 18, 0) };
        header.Controls.Add(new Label
        {
            Text = "Personal messages",
            Font = Theme.H3,
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(16, 12),
        });
        _count.Location = new Point(18, 36);
        header.Controls.Add(_count);

        Theme.StyleGrid(_grid);
        _grid.ReadOnly = false;
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Employee",
            FillWeight = 220,
            ReadOnly = true,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Message", FillWeight = 560 });
        _grid.CellEndEdit += (_, e) => SavePersonalMessage(e.RowIndex);
        _grid.DataError += (_, _) => { };

        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 0, 18, 16) };
        host.Controls.Add(_grid);

        peopleCard.Controls.Add(host);
        peopleCard.Controls.Add(header);
        stack.Add(peopleCard);

        stack.Add(new Label
        {
            Text = "Tip: leave a personal message blank to fall back to the company message.",
            Font = Theme.Small,
            ForeColor = Theme.TextMuted,
            AutoSize = true,
        });
    }

    public override void RefreshData()
    {
        _company.Text = Services.Store.GetSettings().CompanyMessage;

        var employees = Services.Store.ListEmployees(includeInactive: false).OrderBy(e => e.SortName).ToList();
        _grid.Rows.Clear();
        foreach (var employee in employees)
        {
            int index = _grid.Rows.Add(employee.SortName, employee.Message);
            _grid.Rows[index].Tag = employee;
        }

        _count.Text = $"{employees.Count} active employee(s)";
    }

    private void SaveCompanyMessage()
    {
        var settings = Services.Store.GetSettings();
        settings.CompanyMessage = _company.Text.Trim();
        Services.Store.SaveSettings(settings);
        Services.Store.Audit("Company message changed", settings.CompanyMessage);
        Services.NotifyDataChanged();
        PromptDialog.Info(this, "Company message saved. It appears on the clock immediately.");
    }

    private void SavePersonalMessage(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _grid.Rows.Count) return;
        if (_grid.Rows[rowIndex].Tag is not Employee employee) return;

        string text = Convert.ToString(_grid.Rows[rowIndex].Cells[1].Value)?.Trim() ?? "";
        if (text == employee.Message) return;

        employee.Message = text;
        Services.Store.UpdateEmployee(employee);
        Services.Store.Audit("Employee message changed", $"{employee.DisplayName}: {text}");
        Services.NotifyDataChanged();
    }
}

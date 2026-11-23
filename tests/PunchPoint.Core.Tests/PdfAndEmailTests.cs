using PunchPoint.Core.Email;
using PunchPoint.Core.Export;
using PunchPoint.Core.Models;
using PunchPoint.Core.Reports;
using PunchPoint.Core.Storage;

namespace PunchPoint.Core.Tests;

public class PdfReportTests
{
    [Fact]
    public void Writes_a_valid_pdf_with_the_report_text()
    {
        var table = new ReportTable { Title = "Payroll Summary", Subtitle = "Nov 11 – Nov 17, 2026" };
        foreach (string column in new[] { "Employee", "Regular", "Total" })
            table.AddColumn(column);
        table.AddRow("Triolo, Frank", "8:30", "8:30");
        table.AddRow("TOTAL", "8:30", "8:30");

        byte[] pdf = PdfReports.ToBytes(table, "Print Shop");
        string text = System.Text.Encoding.Latin1.GetString(pdf);

        Assert.StartsWith("%PDF-1.4", text);
        Assert.EndsWith("%%EOF\n", text);
        Assert.Contains("Payroll Summary", text);
        Assert.Contains("Triolo, Frank", text);
        Assert.Contains("Print Shop", text);
        Assert.Contains("Page 1 of 1", text);
        Assert.Contains("Nov 11 ", text);
        Assert.Contains("Nov 17, 2026", text);
        Assert.Contains(((char)0x96).ToString(), text); // en dash kept as WinAnsi 0x96
    }
}

public class EmailSettingsTests
{
    [Fact]
    public void Smtp_settings_and_employee_email_round_trip()
    {
        string path = TestPaths.NewDatabasePath();
        try
        {
            using var database = Database.Open(path);
            var store = new Store(database);

            var employee = new Employee { FirstName = "Mail", LastName = "Test", Pin = "1", Email = "mail@example.com" };
            store.InsertEmployee(employee);
            Assert.Equal("mail@example.com", store.GetEmployee(employee.Id)!.Email);

            var s = store.GetSettings();
            s.SmtpHost = "smtp.example.com";
            s.SmtpPort = 587;
            s.SmtpUseSsl = true;
            s.SmtpUser = "user";
            s.SmtpPassword = "secret";
            s.SmtpFrom = "clock@example.com";
            s.ReportEmail = "owner@example.com";
            s.EmailPeriodSummary = true;
            s.EmailEmployees = true;
            store.SaveSettings(s);

            var reloaded = store.GetSettings();
            Assert.Equal("smtp.example.com", reloaded.SmtpHost);
            Assert.Equal(587, reloaded.SmtpPort);
            Assert.True(reloaded.SmtpUseSsl);
            Assert.True(reloaded.EmailPeriodSummary);
            Assert.True(EmailService.IsConfigured(reloaded));
        }
        finally
        {
            foreach (string suffix in new[] { "", "-wal", "-shm" })
            {
                try { File.Delete(path + suffix); } catch { }
            }
        }
    }

    [Fact]
    public void Period_email_is_logged_once()
    {
        string path = TestPaths.NewDatabasePath();
        try
        {
            using var database = Database.Open(path);
            var store = new Store(database);

            Assert.False(store.WasPeriodEmailed(7));
            store.LogEmail(7, "period", "owner@example.com");
            Assert.True(store.WasPeriodEmailed(7));
            Assert.False(store.WasPeriodEmailed(8));
        }
        finally
        {
            foreach (string suffix in new[] { "", "-wal", "-shm" })
            {
                try { File.Delete(path + suffix); } catch { }
            }
        }
    }
}

using PunchPoint.Core.Models;
using PunchPoint.Core.Security;
using PunchPoint.Core.Storage;
using PunchPoint.Core.Web;

namespace PunchPoint.Core.Tests;

public class SecretProtectorTests
{
    [Fact]
    public void Plain_text_is_left_alone_when_encryption_is_off()
    {
        Assert.Equal("hunter2", SecretProtector.Protect("hunter2", encrypt: false));
        Assert.Equal("hunter2", SecretProtector.Unprotect("hunter2"));
    }

    [Fact]
    public void Protected_values_round_trip_and_are_not_readable()
    {
        string stored = SecretProtector.Protect("app-password-123", encrypt: true);

        Assert.True(SecretProtector.IsProtected(stored));
        Assert.DoesNotContain("app-password-123", stored);
        Assert.Equal("app-password-123", SecretProtector.Unprotect(stored));
    }

    [Fact]
    public void Empty_values_stay_empty()
    {
        Assert.Equal("", SecretProtector.Protect("", encrypt: true));
        Assert.Equal("", SecretProtector.Unprotect(""));
        Assert.False(SecretProtector.IsProtected(""));
    }
}

public class WebReportServerTests
{
    [Fact]
    public void Page_lists_employees_hours_and_who_is_in()
    {
        string path = TestPaths.NewDatabasePath();
        try
        {
            using (var database = Database.Open(path))
            {
                var store = new Store(database);
                var settings = store.GetSettings();
                settings.CompanyName = "Print Shop";
                store.SaveSettings(settings);

                var employee = new Employee { FirstName = "Maria", LastName = "Gonzalez", Pin = "1001" };
                store.InsertEmployee(employee);

                var period = new PayPeriod { Start = DateTime.Today, End = DateTime.Today.AddDays(6) };
                store.UpsertPayPeriod(period);

                store.InsertPunch(new Punch { EmployeeId = employee.Id, Timestamp = DateTime.Today.AddHours(8), Kind = PunchKind.In });
                store.InsertPunch(new Punch { EmployeeId = employee.Id, Timestamp = DateTime.Today.AddHours(12), Kind = PunchKind.Out });
            }

            using var server = new WebReportServer(path, "punchpoint", "secret", 8099);
            string html = server.BuildPage();

            Assert.Contains("Print Shop", html);
            Assert.Contains("Gonzalez, Maria", html);
            Assert.Contains("4:00", html);           // four hours worked
            Assert.Contains("read-only", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<form", html);     // nothing can be changed
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
    public void Refuses_to_start_without_a_password()
    {
        using var server = new WebReportServer("/tmp/does-not-matter.db", "punchpoint", "", 8099);
        Assert.False(server.Start());
        Assert.Contains("password", server.LastError!, StringComparison.OrdinalIgnoreCase);
    }
}

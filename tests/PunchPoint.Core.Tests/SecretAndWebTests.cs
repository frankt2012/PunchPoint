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

public class WebBindingTests
{
    private static string Request(int port, string? user = null, string? password = null, string path = "/health")
    {
        using var client = new System.Net.Sockets.TcpClient();
        client.Connect(System.Net.IPAddress.Loopback, port);
        using var stream = client.GetStream();

        string auth = user is null
            ? ""
            : "Authorization: Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{user}:{password}")) + "\r\n";

        string request = $"GET {path} HTTP/1.1\r\nHost: localhost\r\n{auth}Connection: close\r\n\r\n";
        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(request);
        stream.Write(bytes, 0, bytes.Length);

        using var reader = new System.IO.StreamReader(stream);
        return reader.ReadLine() ?? "";
    }

    [Fact]
    public void Listens_on_loopback_only_by_default()
    {
        using var server = new WebReportServer("/tmp/punchpoint-web-test.db", "punchpoint", "secret", 0, "");
        Assert.True(server.Start(), server.LastError);

        Assert.True(System.Net.IPAddress.IsLoopback(server.BoundAddress));
        Assert.False(server.IsReachableFromOtherDevices);
        Assert.True(server.Port > 0);

        Assert.Contains("200", Request(server.Port, "punchpoint", "secret"));
        Assert.Contains("401", Request(server.Port));
        Assert.Contains("401", Request(server.Port, "punchpoint", "wrong"));
    }

    [Fact]
    public void Blank_address_means_local_only_and_all_networks_is_flagged()
    {
        using var local = new WebReportServer("/tmp/punchpoint-web-test.db", "punchpoint", "secret", 0, "");
        Assert.True(local.Start(), local.LastError);
        Assert.False(local.IsReachableFromOtherDevices);

        using var any = new WebReportServer("/tmp/punchpoint-web-test.db", "punchpoint", "secret", 0, "0.0.0.0");
        Assert.True(any.Start(), any.LastError);
        Assert.False(any.IsReachableFromOtherDevices); // IPAddress.Any is never advertised
    }

    [Fact]
    public void Rejects_an_invalid_address_with_a_clear_message()
    {
        using var server = new WebReportServer("/tmp/punchpoint-web-test.db", "punchpoint", "secret", 0, "not-an-ip");
        Assert.False(server.Start());
        Assert.Contains("not a valid network address", server.LastError!);
    }
}

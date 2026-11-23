using System.Net;
using System.Net.Sockets;
using System.Text;
using PunchPoint.Core.Models;
using PunchPoint.Core.Reports;
using PunchPoint.Core.Services;
using PunchPoint.Core.Storage;

namespace PunchPoint.Core.Web;

/// <summary>
/// A read-only web page on the local network, served by the clock process. It exists so a
/// supervisor can check hours from a phone or another PC without remote access to Windows.
///
/// Deliberately small: a TcpListener speaking just enough HTTP (no admin rights or URL ACL
/// needed), HTTP Basic authentication, GET only, one HTML page plus a health endpoint.
/// </summary>
public sealed class WebReportServer : IDisposable
{
    private readonly string _dbPath;
    private readonly string _user;
    private readonly string _password;
    private readonly int _port;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public WebReportServer(string dbPath, string user, string password, int port)
    {
        _dbPath = dbPath;
        _user = string.IsNullOrWhiteSpace(user) ? "punchpoint" : user.Trim();
        _password = password;
        _port = port;
    }

    public string? LastError { get; private set; }
    public bool IsRunning => _listener is not null;

    public bool Start()
    {
        if (_password.Length == 0)
        {
            LastError = "Set a web access password before enabling the web page.";
            return false;
        }

        try
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _ = Task.Run(() => AcceptLoop(_cts.Token));
            LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _listener = null;
            return false;
        }
    }

    private async Task AcceptLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener is not null)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(token);
            }
            catch
            {
                break;
            }

            _ = Task.Run(() => Handle(client));
        }
    }

    private void Handle(TcpClient client)
    {
        try
        {
            using (client)
            using (var stream = client.GetStream())
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;

                string? requestLine = ReadLine(stream);
                if (requestLine is null) return;

                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                while (true)
                {
                    string? line = ReadLine(stream);
                    if (string.IsNullOrEmpty(line)) break;
                    int colon = line.IndexOf(':');
                    if (colon > 0)
                        headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
                }

                string[] parts = requestLine.Split(' ');
                if (parts.Length < 2) return;
                string method = parts[0].ToUpperInvariant();
                string path = parts[1].Split('?')[0];

                if (method != "GET" && method != "HEAD")
                {
                    Write(stream, "405 Method Not Allowed", "text/plain", "Read-only.", method == "HEAD");
                    return;
                }

                if (!IsAuthorised(headers))
                {
                    byte[] body = Encoding.UTF8.GetBytes("PunchPoint: authentication required.");
                    string head = "HTTP/1.1 401 Unauthorized\r\n" +
                                  "WWW-Authenticate: Basic realm=\"PunchPoint\"\r\n" +
                                  $"Content-Length: {body.Length}\r\nContent-Type: text/plain; charset=utf-8\r\n" +
                                  "Connection: close\r\n\r\n";
                    stream.Write(Encoding.ASCII.GetBytes(head));
                    if (method != "HEAD") stream.Write(body);
                    return;
                }

                if (path.Equals("/health", StringComparison.OrdinalIgnoreCase))
                {
                    Write(stream, "200 OK", "text/plain", "ok", method == "HEAD");
                    return;
                }

                string html = BuildPage();
                Write(stream, "200 OK", "text/html; charset=utf-8", html, method == "HEAD");
            }
        }
        catch
        {
            // A browser closing the connection must not take the server down.
        }
    }

    private bool IsAuthorised(Dictionary<string, string> headers)
    {
        if (!headers.TryGetValue("Authorization", out string? value)) return false;
        if (!value.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase)) return false;

        try
        {
            string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(value[6..].Trim()));
            int colon = decoded.IndexOf(':');
            if (colon < 0) return false;

            string user = decoded[..colon];
            string password = decoded[(colon + 1)..];

            // Constant-time-ish comparison; these are short strings on a LAN.
            return FixedEquals(user, _user) && FixedEquals(password, _password);
        }
        catch
        {
            return false;
        }
    }

    private static bool FixedEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }

    private static string? ReadLine(Stream stream)
    {
        var sb = new StringBuilder();
        int b;
        while ((b = stream.ReadByte()) != -1)
        {
            if (b == '\n') return sb.ToString().TrimEnd('\r');
            if (sb.Length > 8192) return null;
            sb.Append((char)b);
        }
        return sb.Length == 0 ? null : sb.ToString();
    }

    private static void Write(Stream stream, string status, string contentType, string body, bool headOnly)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(body);
        string head = $"HTTP/1.1 {status}\r\nContent-Type: {contentType}\r\n" +
                      $"Content-Length: {bytes.Length}\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n";
        stream.Write(Encoding.ASCII.GetBytes(head));
        if (!headOnly) stream.Write(bytes);
    }

    // -----------------------------------------------------------------
    // Page
    // -----------------------------------------------------------------

    public string BuildPage()
    {
        using var database = Database.Open(_dbPath);
        var store = new Store(database);
        var settings = store.GetSettings();

        var period = store.GetCurrentPayPeriod() ??
                     store.ListPayPeriods().OrderByDescending(p => p.Start).FirstOrDefault();

        var employees = store.ListEmployees(includeInactive: false);
        var clock = new ClockService(store);

        var sb = new StringBuilder();
        sb.Append("<!doctype html><html><head><meta charset=\"utf-8\">")
          .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
          .Append("<meta http-equiv=\"refresh\" content=\"60\">")
          .Append("<title>").Append(Html(settings.CompanyName)).Append(" — hours</title>")
          .Append("<style>")
          .Append(":root{color-scheme:light dark}")
          .Append("body{font-family:system-ui,Segoe UI,Arial,sans-serif;margin:0;padding:20px;background:#f4f6f9;color:#0f172a}")
          .Append("@media (prefers-color-scheme:dark){body{background:#0b1220;color:#e6edf7}th{background:#0e1624!important;color:#93a4bd!important}td{border-color:#1f2a3c!important}tr:nth-child(even) td{background:#121a29}}")
          .Append("h1{font-size:20px;margin:0 0 4px}h2{font-size:14px;margin:24px 0 8px}")
          .Append(".muted{color:#64748b;font-size:13px}")
          .Append("table{border-collapse:collapse;width:100%;max-width:760px;background:transparent}")
          .Append("th,td{text-align:left;padding:8px 10px;border-bottom:1px solid #e2e8f0;font-size:14px}")
          .Append("th{background:#fff;color:#64748b;font-size:12px;text-transform:uppercase;letter-spacing:.03em}")
          .Append("td.n{text-align:right;font-variant-numeric:tabular-nums}")
          .Append("tr.total td{font-weight:700}")
          .Append(".in{color:#16a34a;font-weight:600}.out{color:#94a3b8}")
          .Append("</style></head><body>");

        sb.Append("<h1>").Append(Html(settings.CompanyName)).Append("</h1>");
        sb.Append("<div class=\"muted\">");
        if (period is not null)
            sb.Append("Pay period ").Append(Html($"{period.Start:MMM d} – {period.End:MMM d, yyyy}"));
        sb.Append(" · updated ").Append(DateTime.Now.ToString("h:mm tt")).Append("</div>");

        sb.Append("<h2>Who is on the clock</h2><table><tr><th>Employee</th><th>Status</th><th>Since</th><th class=\"n\">Today</th></tr>");
        int inCount = 0;
        foreach (var employee in employees.OrderBy(e => e.SortName))
        {
            var status = clock.GetStatus(employee);
            if (!status.IsClockedIn) continue;
            inCount++;
            sb.Append("<tr><td>").Append(Html(employee.SortName)).Append("</td>")
              .Append("<td class=\"in\">").Append(status.OnBreak ? "On break" : "Clocked in").Append("</td>")
              .Append("<td>").Append(status.Since is null ? "" : status.Since.Value.ToString("h:mm tt")).Append("</td>")
              .Append("<td class=\"n\">").Append(ReportBuilder.Hours(status.MinutesToday, settings.TotalsFormat)).Append("</td></tr>");
        }
        if (inCount == 0)
            sb.Append("<tr><td colspan=\"4\" class=\"muted\">Nobody is clocked in right now.</td></tr>");
        sb.Append("</table>");

        if (period is not null)
        {
            sb.Append("<h2>Hours this pay period</h2>");
            sb.Append("<table><tr><th>Employee</th>")
              .Append("<th class=\"n\">Regular</th><th class=\"n\">OT1</th><th class=\"n\">OT2</th>")
              .Append("<th class=\"n\">Holiday</th><th class=\"n\">Total</th><th>Approved</th></tr>");

            int reg = 0, ot1 = 0, ot2 = 0, hol = 0, all = 0;
            foreach (var employee in employees.OrderBy(e => e.SortName))
            {
                var punches = store.ListPunches(employee.Id, period.Start, period.End.AddDays(1));
                if (punches.Count == 0) continue;

                var shift = employee.ShiftId is int id ? store.GetShift(id) : null;
                var card = store.GetTimeCard(employee.Id, period.Id);
                var result = TimeCalc.TimeCardCalculator.Calculate(employee, shift, period, punches, settings,
                    store.ListHolidays(), card);

                reg += result.RegularMinutes;
                ot1 += result.OverTime1Minutes;
                ot2 += result.OverTime2Minutes;
                hol += result.HolidayMinutes;
                all += result.TotalMinutes;

                sb.Append("<tr><td>").Append(Html(employee.SortName)).Append("</td>")
                  .Append(Td(result.RegularMinutes, settings)).Append(Td(result.OverTime1Minutes, settings))
                  .Append(Td(result.OverTime2Minutes, settings)).Append(Td(result.HolidayMinutes, settings))
                  .Append(Td(result.TotalMinutes, settings))
                  .Append("<td>").Append(card?.Approved == true ? "✓" : "").Append("</td></tr>");
            }

            sb.Append("<tr class=\"total\"><td>Total</td>")
              .Append(Td(reg, settings)).Append(Td(ot1, settings)).Append(Td(ot2, settings))
              .Append(Td(hol, settings)).Append(Td(all, settings)).Append("<td></td></tr>");
            sb.Append("</table>");
        }

        sb.Append("<p class=\"muted\" style=\"margin-top:24px\">Read-only view. Refreshes every minute. " +
                  "PunchPoint on ").Append(Html(Environment.MachineName)).Append(".</p>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static string Td(int minutes, AppSettings settings) =>
        $"<td class=\"n\">{ReportBuilder.Hours(minutes, settings.TotalsFormat)}</td>";

    private static string Html(string? text) => WebUtility.HtmlEncode(text ?? "");

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _listener?.Stop(); } catch { }
        _listener = null;
    }
}

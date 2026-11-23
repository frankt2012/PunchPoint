using PunchPoint.Core.Export;
using PunchPoint.Core.Models;
using PunchPoint.Core.Reports;
using PunchPoint.Core.Storage;

namespace PunchPoint.Core.Email;

/// <summary>
/// Builds the pay-period summary (PDF) and emails it. Runs automatically once a period has
/// ended, and on demand from the Reports screen.
/// </summary>
public static class PeriodMailer
{
    public static (bool Sent, string Message) SendPeriodSummary(Store store, PayPeriod period, AppSettings? settings = null)
    {
        settings ??= store.GetSettings();

        if (!EmailService.IsConfigured(settings))
            return (false, "Email is not configured (Settings → Email reports).");

        var rows = CsvExporter.BuildRows(store, period, settings);
        if (rows.Count == 0)
            return (false, "There is no time in that pay period to send.");

        try
        {
            var table = PdfReports.FromPayrollRows(rows, new PdfReports.PayPeriodLabelContext
            {
                Subtitle = $"{period.Start:MMM d} – {period.End:MMM d, yyyy}",
                Hours = minutes => ReportBuilder.Hours(minutes, settings.TotalsFormat),
            });

            byte[] pdf = PdfReports.ToBytes(table, settings.CompanyName);

            int total = rows.Sum(r => r.Result.TotalMinutes);
            int unapproved = rows.Count(r => !r.Approved);

            string body =
                $"{settings.CompanyName}\r\n" +
                $"Pay period {period.Start:MMM d} – {period.End:MMM d, yyyy}\r\n\r\n" +
                $"{rows.Count} employee(s), {ReportBuilder.Hours(total, settings.TotalsFormat)} in total.\r\n" +
                (unapproved > 0 ? $"{unapproved} time card(s) were not approved.\r\n" : "") +
                "\r\nThe attached PDF has one line per employee.\r\n\r\n" +
                $"(Sent automatically by PunchPoint on {DateTime.Now:g}.)";

            EmailService.Send(settings, new[] { settings.ReportEmail },
                $"Payroll summary — {period.Start:MMM d} to {period.End:MMM d, yyyy}",
                body,
                ($"payroll-{period.Start:yyyy-MM-dd}.pdf", pdf));

            store.LogEmail(period.Id, "period", settings.ReportEmail);

            int employeeCopies = 0;
            if (settings.EmailEmployees)
                employeeCopies = SendEmployeeCopies(store, period, settings);

            return (true, $"Sent to {settings.ReportEmail}" +
                          (employeeCopies > 0 ? $" and {employeeCopies} employee(s)." : "."));
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static int SendEmployeeCopies(Store store, PayPeriod period, AppSettings settings)
    {
        int sent = 0;

        foreach (var employee in store.ListEmployees(includeInactive: false))
        {
            if (string.IsNullOrWhiteSpace(employee.Email) || !employee.Email.Contains('@')) continue;

            var punches = store.ListPunches(employee.Id, period.Start, period.End.AddDays(1));
            if (punches.Count == 0) continue;

            try
            {
                var table = ReportBuilder.TimeCard(store, employee, period, settings);
                byte[] pdf = PdfReports.ToBytes(table, settings.CompanyName);

                EmailService.Send(settings, new[] { employee.Email },
                    $"Your time card — {period.Start:MMM d} to {period.End:MMM d, yyyy}",
                    $"{employee.FirstName},\r\n\r\nHere is your time card for " +
                    $"{period.Start:MMM d} – {period.End:MMM d, yyyy}.\r\n\r\n" +
                    "If anything looks wrong, tell your supervisor before payroll runs.\r\n\r\n" +
                    $"{settings.CompanyName}",
                    ($"timecard-{period.Start:yyyy-MM-dd}.pdf", pdf));

                sent++;
            }
            catch
            {
                // One bad address must not stop the rest.
            }
        }

        return sent;
    }

    /// <summary>
    /// Called by the clock and the admin app: if a period has ended and has not been
    /// emailed yet, send it once.
    /// </summary>
    public static (bool Sent, string Message) RunIfDue(Store store)
    {
        var settings = store.GetSettings();
        if (!settings.EmailPeriodSummary || !EmailService.IsConfigured(settings))
            return (false, "");

        var period = store.ListPayPeriods()
            .Where(p => p.End.Date < DateTime.Today)
            .OrderByDescending(p => p.End)
            .FirstOrDefault();

        if (period is null) return (false, "");
        if (store.WasPeriodEmailed(period.Id)) return (false, "");

        return SendPeriodSummary(store, period, settings);
    }
}

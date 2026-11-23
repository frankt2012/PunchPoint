import io.github.spannm.jackcess.*;
import io.github.spannm.jackcess.Database.FileFormat;

import java.io.File;
import java.time.LocalDateTime;
import java.time.ZoneId;
import java.util.*;

/**
 * Builds a Jet 4 .mdb with the same schema as Acroprint timeQplus v3
 * (arx_active.mdb) and a realistic amount of employee/punch data.
 * Used as a fixture to validate PunchPoint's importer.
 */
public class MakeLegacyDb {

    static Date dt(LocalDateTime ldt) {
        return Date.from(ldt.atZone(ZoneId.systemDefault()).toInstant());
    }

    public static void main(String[] args) throws Exception {
        File file = new File(args.length > 0 ? args[0] : "legacy-timeqplus.mdb");
        if (file.exists()) file.delete();

        try (Database db = DatabaseBuilder.create(FileFormat.V2003, file)) {
            Table employees = new TableBuilder("Employees")
                .addColumn(new ColumnBuilder("Firstname", DataType.TEXT))
                .addColumn(new ColumnBuilder("MI", DataType.TEXT))
                .addColumn(new ColumnBuilder("Lastname", DataType.TEXT))
                .addColumn(new ColumnBuilder("Badge", DataType.TEXT))
                .addColumn(new ColumnBuilder("PIN", DataType.TEXT))
                .addColumn(new ColumnBuilder("Number", DataType.TEXT))
                .addColumn(new ColumnBuilder("Shift", DataType.LONG))
                .addColumn(new ColumnBuilder("Message", DataType.TEXT))
                .addColumn(new ColumnBuilder("HireDate", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("ExportID", DataType.TEXT))
                .addColumn(new ColumnBuilder("Status", DataType.LONG))
                .addColumn(new ColumnBuilder("BioSense", DataType.LONG))
                .addColumn(new ColumnBuilder("Custom1", DataType.TEXT))
                .addColumn(new ColumnBuilder("Custom2", DataType.TEXT))
                .addColumn(new ColumnBuilder("Custom3", DataType.TEXT))
                .addColumn(new ColumnBuilder("Custom4", DataType.TEXT))
                .addColumn(new ColumnBuilder("Custom5", DataType.TEXT))
                .addColumn(new ColumnBuilder("EmployeeID", DataType.LONG))
                .addColumn(new ColumnBuilder("TerminalPassword", DataType.TEXT))
                .addColumn(new ColumnBuilder("TerminalPriviledge", DataType.LONG))
                .toTable(db);

            Table transactions = new TableBuilder("Transactions")
                .addColumn(new ColumnBuilder("TransDateTime", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("TransBadge", DataType.TEXT))
                .addColumn(new ColumnBuilder("TransType", DataType.LONG))
                .addColumn(new ColumnBuilder("TransSource", DataType.LONG))
                .addColumn(new ColumnBuilder("TransHours", DataType.LONG))
                .addColumn(new ColumnBuilder("TimeCardID", DataType.LONG))
                .addColumn(new ColumnBuilder("TransState", DataType.LONG))
                .addColumn(new ColumnBuilder("IsDST", DataType.BOOLEAN))
                .addColumn(new ColumnBuilder("Comment", DataType.MEMO))
                .addColumn(new ColumnBuilder("TransactionID", DataType.LONG))
                .toTable(db);

            Table shifts = new TableBuilder("Shifts")
                .addColumn(new ColumnBuilder("ShiftEnabled", DataType.BOOLEAN))
                .addColumn(new ColumnBuilder("ShiftStart", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("StartRound", DataType.LONG))
                .addColumn(new ColumnBuilder("StartGrace", DataType.LONG))
                .addColumn(new ColumnBuilder("StartDock", DataType.LONG))
                .addColumn(new ColumnBuilder("ShiftStop", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("StopRound", DataType.LONG))
                .addColumn(new ColumnBuilder("StopGrace", DataType.LONG))
                .addColumn(new ColumnBuilder("StopDock", DataType.LONG))
                .addColumn(new ColumnBuilder("LunchEnabled", DataType.BOOLEAN))
                .addColumn(new ColumnBuilder("LunchStart", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("LunchEnd", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("LunchDuration", DataType.LONG))
                .addColumn(new ColumnBuilder("LunchAutoAfter", DataType.LONG))
                .addColumn(new ColumnBuilder("ShiftNumber", DataType.LONG))
                .addColumn(new ColumnBuilder("HolidaysPaid", DataType.BOOLEAN))
                .addColumn(new ColumnBuilder("HolidayHours", DataType.LONG))
                .addColumn(new ColumnBuilder("ShiftName", DataType.TEXT))
                .addColumn(new ColumnBuilder("ShiftID", DataType.LONG))
                .toTable(db);

            Table payPeriods = new TableBuilder("PayPeriods")
                .addColumn(new ColumnBuilder("PeriodType", DataType.LONG))
                .addColumn(new ColumnBuilder("PeriodStart", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("PeriodEnd", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("PeriodInterval", DataType.LONG))
                .addColumn(new ColumnBuilder("PayPeriodID", DataType.LONG))
                .toTable(db);

            Table setup = new TableBuilder("Setup")
                .addColumn(new ColumnBuilder("PeriodType", DataType.LONG))
                .addColumn(new ColumnBuilder("PeriodStart", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("MonthlyDay1", DataType.LONG))
                .addColumn(new ColumnBuilder("MonthlyDay2", DataType.LONG))
                .addColumn(new ColumnBuilder("MaxTimeOnClock", DataType.LONG))
                .addColumn(new ColumnBuilder("PayIntervalRound", DataType.LONG))
                .addColumn(new ColumnBuilder("DailyOT1Thresh", DataType.LONG))
                .addColumn(new ColumnBuilder("DailyOT2Thresh", DataType.LONG))
                .addColumn(new ColumnBuilder("WeeklyOT1Thresh", DataType.LONG))
                .addColumn(new ColumnBuilder("WeeklyOT2Thresh", DataType.LONG))
                .addColumn(new ColumnBuilder("DayChangeOffset", DataType.LONG))
                .addColumn(new ColumnBuilder("Enable7thDayOT", DataType.BOOLEAN))
                .addColumn(new ColumnBuilder("CompanyMessage", DataType.TEXT))
                .addColumn(new ColumnBuilder("NoExportUnapprovedHourly", DataType.BOOLEAN))
                .addColumn(new ColumnBuilder("NoExportUnapprovedSalary", DataType.BOOLEAN))
                .addColumn(new ColumnBuilder("SalaryPayHolidays", DataType.BOOLEAN))
                .addColumn(new ColumnBuilder("SalaryHolidayHours", DataType.LONG))
                .addColumn(new ColumnBuilder("HolidayEligible", DataType.LONG))
                .addColumn(new ColumnBuilder("SalaryPeriodHours", DataType.LONG))
                .addColumn(new ColumnBuilder("DOWOTStart", DataType.LONG))
                .addColumn(new ColumnBuilder("SetupID", DataType.LONG))
                .toTable(db);

            Table holidays = new TableBuilder("Holidays")
                .addColumn(new ColumnBuilder("HolidayName", DataType.TEXT))
                .addColumn(new ColumnBuilder("HolidayDate", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("HolidayID", DataType.LONG))
                .toTable(db);

            Table timeCards = new TableBuilder("TimeCards")
                .addColumn(new ColumnBuilder("EmployeeID", DataType.LONG))
                .addColumn(new ColumnBuilder("PayPeriodID", DataType.LONG))
                .addColumn(new ColumnBuilder("EditREGHours", DataType.LONG))
                .addColumn(new ColumnBuilder("EditOT1Hours", DataType.LONG))
                .addColumn(new ColumnBuilder("EditOT2Hours", DataType.LONG))
                .addColumn(new ColumnBuilder("Status", DataType.LONG))
                .addColumn(new ColumnBuilder("CarryREG", DataType.LONG))
                .addColumn(new ColumnBuilder("CarryOT1", DataType.LONG))
                .addColumn(new ColumnBuilder("CarryOT2", DataType.LONG))
                .addColumn(new ColumnBuilder("CarryDays", DataType.LONG))
                .addColumn(new ColumnBuilder("Approved", DataType.BOOLEAN))
                .addColumn(new ColumnBuilder("ApprovedSuper", DataType.TEXT))
                .addColumn(new ColumnBuilder("ApprovedDate", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("TimeCardID", DataType.LONG))
                .toTable(db);

            // ---- data ---------------------------------------------------
            shifts.addRow(true, dt(LocalDateTime.of(2000, 1, 1, 8, 0)), 0, 5, 0,
                dt(LocalDateTime.of(2000, 1, 1, 17, 0)), 0, 5, 0, true,
                dt(LocalDateTime.of(2000, 1, 1, 12, 0)), dt(LocalDateTime.of(2000, 1, 1, 12, 30)),
                30, 360, 1, true, 8, "Day Shift", 1);

            shifts.addRow(true, dt(LocalDateTime.of(2000, 1, 1, 8, 0)), 0, 10, 0,
                dt(LocalDateTime.of(2000, 1, 1, 17, 0)), 0, 10, 0, false,
                dt(LocalDateTime.of(2000, 1, 1, 12, 0)), dt(LocalDateTime.of(2000, 1, 1, 12, 0)),
                0, 0, 2, false, 0, "Day Shift (no lunch)", 2);

            String[][] people = {
                {"Maria", "L", "Gonzalez", "1001", "1001", "A-001", "2"},
                {"James", "", "Okafor", "1002", "1002", "A-002", "1"},
                {"Dana", "R", "Whitfield", "1003", "1003", "A-003", "1"},
                {"Luis", "", "Ramirez", "1004", "1004", "A-004", "1"},
                {"Priya", "", "Shah", "1005", "1005", "A-005", "1"},
                {"Tom", "J", "Baker", "1006", "1006", "A-006", "2"},
            };

            int employeeId = 1;
            for (String[] p : people) {
                employees.addRow(p[0], p[1], p[2], p[3], p[4], p[5], Integer.parseInt(p[6]),
                    "", dt(LocalDateTime.of(2021, 3, 15, 0, 0)), p[5], 1, 0,
                    "", "", "", "", "", employeeId, "", 0);
                employeeId++;
            }

            setup.addRow(1, dt(LocalDateTime.of(2026, 11, 9, 0, 0)), 1, 16, 960, 0,
                480, 720, 2400, 0, 0, false, "Remember to clock in and out.",
                false, false, false, 8, 0, 40, 1, 1);

            holidays.addRow("Thanksgiving", dt(LocalDateTime.of(2026, 11, 26, 0, 0)), 1);
            holidays.addRow("Christmas", dt(LocalDateTime.of(2026, 12, 25, 0, 0)), 2);

            // Two bi-weekly periods: Nov 9 - Nov 22, 2026.
            payPeriods.addRow(1, dt(LocalDateTime.of(2026, 11, 9, 0, 0)), dt(LocalDateTime.of(2026, 11, 22, 0, 0)), 14, 101);
            payPeriods.addRow(1, dt(LocalDateTime.of(2026, 11, 23, 0, 0)), dt(LocalDateTime.of(2026, 12, 6, 0, 0)), 14, 102);

            int transactionId = 1;

            // Maria: lunch-break punches (states 0/2/3/1), full two weeks, two days with OT.
            LocalDateTime monday = LocalDateTime.of(2026, 11, 9, 8, 0);
            for (int day = 0; day < 10; day++) {
                LocalDateTime base = monday.plusDays(day);
                if (base.getDayOfWeek().getValue() > 5) continue;
                transactions.addRow(dt(base.withHour(8).withMinute(1)), "1001", 1, 1, 0, null, 0, false, "", transactionId++);
                transactions.addRow(dt(base.withHour(12).withMinute(0)), "1001", 1, 1, 0, null, 2, false, "", transactionId++);
                transactions.addRow(dt(base.withHour(12).withMinute(31)), "1001", 1, 1, 0, null, 3, false, "", transactionId++);
                LocalDateTime end = base.withHour(day == 2 ? 18 : 17).withMinute(day == 2 ? 15 : 2);
                transactions.addRow(dt(end), "1001", 1, 1, 0, null, 1, false, "", transactionId++);
            }

            // James: simple in/out, one day 10 hours (daily OT).
            for (int day = 0; day < 10; day++) {
                LocalDateTime base = monday.plusDays(day);
                if (base.getDayOfWeek().getValue() > 5) continue;
                boolean longDay = day == 3;
                transactions.addRow(dt(base.withHour(7).withMinute(58)), "1002", 1, 1, 0, null, 0, false, "", transactionId++);
                transactions.addRow(dt(base.withHour(longDay ? 18 : 17).withMinute(longDay ? 58 : 3)), "1002", 1, 1, 0, null, 1, false, "", transactionId++);
            }

            // Dana: no lunch shift, one missing OUT (forgot to clock out).
            for (int day = 0; day < 10; day++) {
                LocalDateTime base = monday.plusDays(day);
                if (base.getDayOfWeek().getValue() > 5) continue;
                transactions.addRow(dt(base.withHour(8).withMinute(0)), "1003", 1, 1, 0, null, 0, false, "", transactionId++);
                if (day == 4) continue; // missing OUT
                transactions.addRow(dt(base.withHour(17).withMinute(0)), "1003", 1, 1, 0, null, 1, false, "", transactionId++);
            }

            // Luis: vacation day recorded as hours (TransHours = 8 hours).
            for (int day = 0; day < 10; day++) {
                LocalDateTime base = monday.plusDays(day);
                if (base.getDayOfWeek().getValue() > 5) continue;
                if (day == 4) {
                    transactions.addRow(dt(base.withHour(8).withMinute(0)), "1004", 3, 1, 480, null, 0, false, "Vacation day", transactionId++);
                    continue;
                }
                transactions.addRow(dt(base.withHour(8).withMinute(2)), "1004", 1, 1, 0, null, 0, false, "", transactionId++);
                transactions.addRow(dt(base.withHour(17).withMinute(1)), "1004", 1, 1, 0, null, 1, false, "", transactionId++);
            }

            // Priya: two punches per day only on the first week (hired mid-period).
            for (int day = 5; day < 10; day++) {
                LocalDateTime base = monday.plusDays(day);
                if (base.getDayOfWeek().getValue() > 5) continue;
                transactions.addRow(dt(base.withHour(9).withMinute(0)), "1005", 1, 1, 0, null, 0, false, "", transactionId++);
                transactions.addRow(dt(base.withHour(15).withMinute(0)), "1005", 1, 1, 0, null, 1, false, "", transactionId++);
            }

            // Tom: inactive/former employee with no punches in the period.
            transactions.addRow(dt(LocalDateTime.of(2026, 9, 1, 8, 0)), "1006", 1, 1, 0, null, 0, false, "", transactionId++);
            transactions.addRow(dt(LocalDateTime.of(2026, 9, 1, 17, 0)), "1006", 1, 1, 0, null, 1, false, "", transactionId++);

            // One approved time card for Maria in the first period.
            timeCards.addRow(1, 101, 0, 0, 0, 1, 0, 0, 0, 0, true, "admin",
                dt(LocalDateTime.of(2026, 11, 23, 9, 0)), 1);

            System.out.println("Wrote " + file.getAbsolutePath());
            System.out.println("Employees: " + employees.getRowCount() + ", transactions: " + transactions.getRowCount());
        }
    }
}

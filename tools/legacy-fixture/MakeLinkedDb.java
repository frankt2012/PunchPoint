import io.github.spannm.jackcess.*;
import io.github.spannm.jackcess.Database.FileFormat;

import java.io.File;
import java.time.LocalDateTime;
import java.time.ZoneId;
import java.util.Date;

/**
 * Reproduces the shape of a real timeQplus v3 database used from the PC clock:
 *   - punches have an EMPTY TransBadge and are linked through Transactions.TimeCardID
 *   - employees have no badge, only a 4-digit PIN (sometimes with a leading zero)
 *   - shifts are disabled placeholders dated 1899
 *   - the setup disables daily overtime with 1440 and enables weekly OT at 2400
 *   - one "period" is a long placeholder that should not be imported
 */
public class MakeLinkedDb {
    static Date dt(LocalDateTime ldt) {
        return Date.from(ldt.atZone(ZoneId.systemDefault()).toInstant());
    }

    static Date none() {
        return dt(LocalDateTime.of(1899, 12, 30, 0, 0));
    }

    public static void main(String[] args) throws Exception {
        File file = new File(args.length > 0 ? args[0] : "linked-timeqplus.mdb");
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
                .addColumn(new ColumnBuilder("HireDate", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("Status", DataType.LONG))
                .addColumn(new ColumnBuilder("EmployeeID", DataType.LONG))
                .toTable(db);

            Table shifts = new TableBuilder("Shifts")
                .addColumn(new ColumnBuilder("ShiftEnabled", DataType.BOOLEAN))
                .addColumn(new ColumnBuilder("ShiftStart", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("StartGrace", DataType.LONG))
                .addColumn(new ColumnBuilder("ShiftStop", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("StopGrace", DataType.LONG))
                .addColumn(new ColumnBuilder("LunchEnabled", DataType.BOOLEAN))
                .addColumn(new ColumnBuilder("HolidaysPaid", DataType.BOOLEAN))
                .addColumn(new ColumnBuilder("HolidayHours", DataType.LONG))
                .addColumn(new ColumnBuilder("ShiftName", DataType.TEXT))
                .addColumn(new ColumnBuilder("ShiftID", DataType.LONG))
                .toTable(db);

            Table timeCards = new TableBuilder("TimeCards")
                .addColumn(new ColumnBuilder("EmployeeID", DataType.LONG))
                .addColumn(new ColumnBuilder("PayPeriodID", DataType.LONG))
                .addColumn(new ColumnBuilder("EditREGHours", DataType.LONG))
                .addColumn(new ColumnBuilder("EditOT1Hours", DataType.LONG))
                .addColumn(new ColumnBuilder("EditOT2Hours", DataType.LONG))
                .addColumn(new ColumnBuilder("Status", DataType.LONG))
                .addColumn(new ColumnBuilder("Approved", DataType.BOOLEAN))
                .addColumn(new ColumnBuilder("ApprovedSuper", DataType.TEXT))
                .addColumn(new ColumnBuilder("ApprovedDate", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("TimeCardID", DataType.LONG))
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

            Table periods = new TableBuilder("PayPeriods")
                .addColumn(new ColumnBuilder("PeriodType", DataType.LONG))
                .addColumn(new ColumnBuilder("PeriodStart", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("PeriodEnd", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("PeriodInterval", DataType.LONG))
                .addColumn(new ColumnBuilder("PayPeriodID", DataType.LONG))
                .toTable(db);

            Table setup = new TableBuilder("Setup")
                .addColumn(new ColumnBuilder("PeriodType", DataType.LONG))
                .addColumn(new ColumnBuilder("PeriodStart", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("MaxTimeOnClock", DataType.LONG))
                .addColumn(new ColumnBuilder("PayIntervalRound", DataType.LONG))
                .addColumn(new ColumnBuilder("DailyOT1Thresh", DataType.LONG))
                .addColumn(new ColumnBuilder("DailyOT2Thresh", DataType.LONG))
                .addColumn(new ColumnBuilder("WeeklyOT1Thresh", DataType.LONG))
                .addColumn(new ColumnBuilder("WeeklyOT2Thresh", DataType.LONG))
                .addColumn(new ColumnBuilder("DayChangeOffset", DataType.LONG))
                .addColumn(new ColumnBuilder("CompanyMessage", DataType.TEXT))
                .addColumn(new ColumnBuilder("SetupID", DataType.LONG))
                .toTable(db);

            Table holidays = new TableBuilder("Holidays")
                .addColumn(new ColumnBuilder("HolidayName", DataType.TEXT))
                .addColumn(new ColumnBuilder("HolidayDate", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("HolidayID", DataType.LONG))
                .toTable(db);

            // Disabled placeholder shift, as found in the real database.
            shifts.addRow(false, none(), 0, none(), 0, false, false, 0, "Shift 1", 1);

            employees.addRow("Patrick", "", "Smith", "", "0744", "000000012", 1, none(), 0, 12);
            employees.addRow("Ty", "", "Healey", "", "6719", "000000006", 1, none(), 0, 6);
            employees.addRow("Wayne", "", "Scott", "", "0734", "000000031", 1, none(), 0, 31);

            setup.addRow(1, dt(LocalDateTime.of(2026, 11, 25, 0, 0)), 720, 1,
                1440, 1440, 2400, 10080, 0, "", 1);

            // Two real weekly periods plus a long placeholder.
            periods.addRow(1, dt(LocalDateTime.of(2026, 11, 11, 0, 0)), dt(LocalDateTime.of(2026, 11, 17, 0, 0)), 1, 247);
            periods.addRow(1, dt(LocalDateTime.of(2026, 11, 18, 0, 0)), dt(LocalDateTime.of(2026, 11, 24, 0, 0)), 1, 248);
            periods.addRow(1, dt(LocalDateTime.of(2026, 11, 25, 0, 0)), dt(LocalDateTime.of(2027, 2, 23, 0, 0)), 1, 249);

            // Time cards link each employee to a period.
            int cardId = 9000;
            int[] employeeIds = { 12, 6, 31 };
            for (int e = 0; e < employeeIds.length; e++) {
                for (int p = 0; p < 2; p++) {
                    cardId++;
                    timeCards.addRow(employeeIds[e], 247 + p, 0, 0, 0, 2, false, "", null, cardId);
                }
            }

            // Punches: empty badge, linked via TimeCardID.
            int transactionId = 50000;
            LocalDateTime monday = LocalDateTime.of(2026, 11, 11, 8, 0);

            // Patrick: 8 hours every weekday (Mon-Fri) in period 1 -> 40 hours, no OT.
            int patrickCard = 9001;
            for (int d = 0; d < 5; d++) {
                transactionId = add(transactions, monday.plusDays(d).withHour(8), patrickCard, transactionId);
                transactionId = add(transactions, monday.plusDays(d).withHour(17), patrickCard, transactionId);
            }

            // Ty: 9.5 hours every weekday -> 47.5 hours, 7.5 overtime.
            int tyCard = 9003;
            for (int d = 0; d < 5; d++) {
                transactionId = add(transactions, monday.plusDays(d).withHour(7).withMinute(58), tyCard, transactionId);
                transactionId = add(transactions, monday.plusDays(d).withHour(17).withMinute(28), tyCard, transactionId);
            }

            // Wayne: four punches on Monday (lunch in the middle), two on Tuesday, and an
            // unmatched IN on Wednesday (a missing OUT) that must be reported, not paid.
            int wayneCard = 9005;
            transactionId = add(transactions, monday.withHour(8), wayneCard, transactionId);
            transactionId = add(transactions, monday.withHour(12), wayneCard, transactionId);
            transactionId = add(transactions, monday.withHour(12).withMinute(30), wayneCard, transactionId);
            transactionId = add(transactions, monday.withHour(17), wayneCard, transactionId);
            transactionId = add(transactions, monday.plusDays(1).withHour(8), wayneCard, transactionId);
            transactionId = add(transactions, monday.plusDays(1).withHour(17), wayneCard, transactionId);
            transactionId = add(transactions, monday.plusDays(2).withHour(8), wayneCard, transactionId);

            System.out.println("Wrote " + file.getAbsolutePath() + " transactions=" + (transactionId - 50000));
        }
    }

    static int add(Table t, LocalDateTime when, int cardId, int id) throws Exception {
        t.addRow(dt(when), "", 1, 1, 0, cardId, 0, false, "", id);
        return id + 1;
    }
}

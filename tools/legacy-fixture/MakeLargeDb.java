import io.github.spannm.jackcess.*;
import io.github.spannm.jackcess.Database.FileFormat;

import java.io.File;
import java.time.LocalDateTime;
import java.time.ZoneId;
import java.util.Date;
import java.util.Random;

/** Generates a large timeQplus-shaped database for performance testing. */
public class MakeLargeDb {
    static Date dt(LocalDateTime ldt) {
        return Date.from(ldt.atZone(ZoneId.systemDefault()).toInstant());
    }

    public static void main(String[] args) throws Exception {
        File file = new File(args.length > 0 ? args[0] : "large-timeqplus.mdb");
        int employeeCount = args.length > 1 ? Integer.parseInt(args[1]) : 40;
        int days = args.length > 2 ? Integer.parseInt(args[2]) : 730;

        if (file.exists()) file.delete();

        try (Database db = DatabaseBuilder.create(FileFormat.V2003, file)) {
            Table employees = new TableBuilder("Employees")
                .addColumn(new ColumnBuilder("Firstname", DataType.TEXT))
                .addColumn(new ColumnBuilder("Lastname", DataType.TEXT))
                .addColumn(new ColumnBuilder("Badge", DataType.TEXT))
                .addColumn(new ColumnBuilder("PIN", DataType.TEXT))
                .addColumn(new ColumnBuilder("Number", DataType.TEXT))
                .addColumn(new ColumnBuilder("Shift", DataType.LONG))
                .addColumn(new ColumnBuilder("HireDate", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("Status", DataType.LONG))
                .addColumn(new ColumnBuilder("EmployeeID", DataType.LONG))
                .toTable(db);

            Table transactions = new TableBuilder("Transactions")
                .addColumn(new ColumnBuilder("TransDateTime", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("TransBadge", DataType.TEXT))
                .addColumn(new ColumnBuilder("TransType", DataType.LONG))
                .addColumn(new ColumnBuilder("TransSource", DataType.LONG))
                .addColumn(new ColumnBuilder("TransHours", DataType.LONG))
                .addColumn(new ColumnBuilder("TransState", DataType.LONG))
                .addColumn(new ColumnBuilder("IsDST", DataType.BOOLEAN))
                .addColumn(new ColumnBuilder("Comment", DataType.MEMO))
                .addColumn(new ColumnBuilder("TransactionID", DataType.LONG))
                .toTable(db);

            Table shifts = new TableBuilder("Shifts")
                .addColumn(new ColumnBuilder("ShiftName", DataType.TEXT))
                .addColumn(new ColumnBuilder("ShiftEnabled", DataType.BOOLEAN))
                .addColumn(new ColumnBuilder("ShiftStart", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("ShiftStop", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("LunchEnabled", DataType.BOOLEAN))
                .addColumn(new ColumnBuilder("LunchDuration", DataType.LONG))
                .addColumn(new ColumnBuilder("ShiftID", DataType.LONG))
                .toTable(db);

            Table periods = new TableBuilder("PayPeriods")
                .addColumn(new ColumnBuilder("PeriodStart", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("PeriodEnd", DataType.SHORT_DATE_TIME))
                .addColumn(new ColumnBuilder("PayPeriodID", DataType.LONG))
                .toTable(db);

            shifts.addRow("Day", true, dt(LocalDateTime.of(2000, 1, 1, 8, 0)),
                dt(LocalDateTime.of(2000, 1, 1, 17, 0)), true, 30, 1);

            for (int e = 1; e <= employeeCount; e++) {
                employees.addRow("Employee" + e, "Test", String.valueOf(1000 + e),
                    String.valueOf(1000 + e), "A-" + e, 1,
                    dt(LocalDateTime.of(2020, 1, 1, 0, 0)), 1, e);
            }

            LocalDateTime start = LocalDateTime.now().minusDays(days);
            Random random = new Random(7);
            int transactionId = 1;
            int payPeriodId = 1;

            for (int d = 0; d < days; d++) {
                LocalDateTime day = start.plusDays(d);
                if (day.getDayOfWeek().getValue() >= 6) continue;

                for (int e = 0; e < employeeCount; e++) {
                    if (random.nextInt(100) < 4) continue; // occasional absence
                    String badge = String.valueOf(1001 + e);
                    transactions.addRow(dt(day.withHour(8).withMinute(random.nextInt(8))),
                        badge, 1, 1, 0, 0, false, "", transactionId++);
                    transactions.addRow(dt(day.withHour(17).withMinute(random.nextInt(8))),
                        badge, 1, 1, 0, 1, false, "", transactionId++);
                }

                if (d % 14 == 0) {
                    periods.addRow(dt(day.withHour(0).withMinute(0)),
                        dt(day.plusDays(13).withHour(0).withMinute(0)), payPeriodId++);
                }
            }

            System.out.println("Wrote " + file.getAbsolutePath());
            System.out.println("employees=" + employeeCount + " transactions=" + (transactionId - 1));
        }
    }
}

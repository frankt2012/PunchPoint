# Importing from timeQplus v3

This document records what the old software actually stores, so the import can be
checked and corrected if a particular installation differs.

## Where the data lives

| Version | Folder |
|---|---|
| 3.0.49 and later | `C:\ProgramData\Acroprint\timeQplusV3\` |
| before 3.0.49 | `C:\ProgramData\Acroprint\timeQplus\` |
| Windows XP / Server 2003 | `C:\Program Files\Acroprint\timeQplusV3\` |

Inside that folder:

```
Data\arx_active.mdb     the live database (Microsoft Access 2000-2003 / Jet 4)
Data\arx_arch.mdb       archive database, same schema
TQTemplates\            fingerprint templates (not imported)
TQLogs\                 terminal logs
```

`arx_active.mdb` is the only file needed. PunchPoint copies it to a temporary folder and
reads the copy, so a running copy of the old software cannot lock or change the file
half-way through an import.

## Schema

Fourteen tables. The ones that matter:

```
Employees     EmployeeID, Firstname, MI, Lastname, Badge, PIN, Number, Shift,
              Message, HireDate, ExportID, Status, Custom1..5, TerminalPassword
Transactions  TransactionID, TransDateTime, TransBadge, TransType, TransSource,
              TransHours, TimeCardID, TransState, IsDST, Comment
Shifts        ShiftID, ShiftName, ShiftEnabled, ShiftStart, ShiftStop,
              StartRound, StartGrace, StartDock, StopRound, StopGrace, StopDock,
              LunchEnabled, LunchStart, LunchEnd, LunchDuration, LunchAutoAfter,
              HolidaysPaid, HolidayHours, ShiftNumber
PayPeriods    PayPeriodID, PeriodType, PeriodStart, PeriodEnd, PeriodInterval
Setup         PeriodType, PeriodStart, PayIntervalRound, DailyOT1Thresh,
              DailyOT2Thresh, WeeklyOT1Thresh, WeeklyOT2Thresh, DayChangeOffset,
              CompanyMessage, SalaryPayHolidays, SalaryHolidayHours, ...
Holidays      HolidayID, HolidayName, HolidayDate
TimeCards     TimeCardID, EmployeeID, PayPeriodID, EditREGHours, EditOT1Hours,
              EditOT2Hours, Approved, ApprovedSuper, ApprovedDate, Carry...
Supervisors   Login, Password, DateFormat, TimeFormat, TotalsFormat
TQDevices, TQUsers, TQUserTemplates, EventLog, Export, Version
```

`Transactions` holds **every terminal event in one table**. There is no "direction"
column with an obvious name.

## How IN and OUT are worked out

The old app has to decide, for each punch, whether it is an IN or an OUT. Three
possibilities exist in different builds:

1. **Stored state** — `TransState` (or `TransType`) contains the direction. The terminal
   SDK this software is built on (ZKTeco) uses `0 = Check-In`, `1 = Check-Out`,
   `2 = Break-Out`, `3 = Break-In`, `4 = OT-In`, `5 = OT-Out`.
2. **Alternating** — no direction is stored; the sequence is IN, OUT, IN, OUT per employee.

PunchPoint does not guess from one sample. It scores each candidate column by how well
it **alternates per employee**: a genuine direction column flips value on every punch
(except after a >14 hour gap, which starts a new session). The best candidate wins if it
is at least 85% consistent; otherwise punches alternate. The preview screen reports which
method was used and with what confidence, and the method can be overridden before import.

Alternating is also correct for four-punch days: IN, OUT(lunch), IN, OUT produces two
segments, so the lunch gap is unpaid, which is what payroll expects.

### Missing punches

With alternating punches, one missed punch would flip the meaning of every later punch.
The importer therefore restarts the sequence with IN whenever more than 14 hours pass
between punches. Time Cards still flags the unpaired day in orange so it can be fixed.

## Values that are translated

| Legacy | New |
|---|---|
| `Employees.Status` | `2` → inactive, anything else → active. All employees are imported; use *Show inactive* to review. |
| `Shifts` grace/dock values | Minutes, when between 0 and 1440. Docking is enabled when the value is greater than 0. |
| `Setup.*Thresh` | Minutes when 0..1440. `PayIntervalRound` is imported as the rounding interval only when 0..60. |
| `PayPeriods` | Imported as-is; period type is inferred from the date range (7 days = weekly, 14 = bi-weekly, 15-17 = semi-monthly, else monthly). |
| `TimeCards.Approved` | Approval flag, approver name and date. |
| `Transactions.TransHours` | Treated as **paid hours** (vacation/holiday/sick), not as worked time. Off by default; the preview shows how many rows and hours this affects. |

Not imported: fingerprint templates, terminal/device configuration, supervisor logins,
custom payroll export definitions (`Export.SetupData`), and the old manual payroll
"carry" numbers.

## Verifying an import

The command line tool can inspect a database without writing anything:

```bash
dotnet run --project src/PunchPoint.Cli -- preview "C:\path\arx_active.mdb"
```

It prints table sizes, the punch date range, the detected direction method, and a table of
employees with punch counts and any odd-punch days. `info` and `dump` show raw tables.

After importing, `timecards` prints the computed totals per employee per period, with any
punch problems listed underneath.

## Jet 4 reader

The importer includes its own read-only Microsoft Access (Jet 4) reader. It reads the
database definition pages, the system catalog (`MSysObjects`), table definitions, usage
maps and row pages directly — no Access, no ODBC, no ACE/JET driver, no Office. That is
deliberate: the missing 32-bit JET engine is one of the reasons the original software fails
on Windows 11.

The reader is validated against 32 real Access databases (118 tables) in the automated
tests, plus a synthetic database with the exact timeQplus schema.
